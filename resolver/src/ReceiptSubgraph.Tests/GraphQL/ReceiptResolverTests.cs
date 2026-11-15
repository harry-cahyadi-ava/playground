using System.Text.Json;
using ReceiptSubgraph.Data;
using ReceiptSubgraph.Domain;

namespace ReceiptSubgraph.Tests.GraphQL;

public class ReceiptResolverTests : IAsyncLifetime
{
    private SubgraphHarness _harness = null!;

    public async ValueTask InitializeAsync() => _harness = await SubgraphHarness.CreateAsync();

    public async ValueTask DisposeAsync() => await _harness.DisposeAsync();

    private static Dictionary<string, object?> Reps(params (string Type, string Id)[] representations)
        => new()
        {
            ["reps"] = representations
                .Select(r => new Dictionary<string, object?> { ["__typename"] = r.Type, ["id"] = r.Id })
                .ToList(),
        };

    private const string EntitiesQuery = "query($reps: [_Any!]!) { _entities(representations: $reps) { %SELECTION% } }";

    private async Task<JsonElement> EntitiesAsync(string selection, params (string Type, string Id)[] representations)
    {
        var result = await _harness.QueryAsync(EntitiesQuery.Replace("%SELECTION%", selection), Reps(representations));
        Assert.False(result.RootElement.TryGetProperty("errors", out var errors), errors.ToString());
        return result.RootElement.GetProperty("data").GetProperty("_entities");
    }

    [Fact]
    public async Task Receipt_entity_resolves_scalar_and_nested_fields()
    {
        var entities = await EntitiesAsync(
            "... on Receipt { id createdAt return isTaxInvoiceAvailable amount { total gst } creditApplied }",
            ("Receipt", "receipt-2"));

        var receipt = entities[0];
        Assert.Equal("receipt-2", receipt.GetProperty("id").GetString());
        Assert.Equal("2025-11-05T18:30:00Z", receipt.GetProperty("createdAt").GetString());
        Assert.False(receipt.GetProperty("return").GetBoolean());
        Assert.True(receipt.GetProperty("isTaxInvoiceAvailable").GetBoolean());
        Assert.Equal(86.45m, receipt.GetProperty("amount").GetProperty("total").GetDecimal());
        Assert.Equal(5m, receipt.GetProperty("creditApplied").GetDecimal());
    }

    [Fact]
    public async Task Unknown_receipt_reference_resolves_to_null()
    {
        var entities = await EntitiesAsync("... on Receipt { id }", ("Receipt", "missing"));

        Assert.Equal(JsonValueKind.Null, entities[0].ValueKind);
    }

    [Fact]
    public async Task Many_receipt_references_are_loaded_with_a_single_repository_call()
    {
        await EntitiesAsync(
            "... on Receipt { id }",
            ("Receipt", "receipt-1"), ("Receipt", "receipt-2"), ("Receipt", "receipt-3"), ("Receipt", "missing"));

        var lookup = Assert.Single(_harness.Repository.IdLookups);
        Assert.Equal(["missing", "receipt-1", "receipt-2", "receipt-3"], lookup.Order());
    }

    [Fact]
    public async Task Receipt_exposes_references_to_store_customer_and_order()
    {
        var entities = await EntitiesAsync(
            "... on Receipt { store { id } customer { id } order { id } }",
            ("Receipt", "receipt-2"));

        Assert.Equal("store-0100", entities[0].GetProperty("store").GetProperty("id").GetString());
        Assert.Equal("customer-1", entities[0].GetProperty("customer").GetProperty("id").GetString());
        Assert.Equal("order-1001", entities[0].GetProperty("order").GetProperty("id").GetString());
    }

    [Fact]
    public async Task Anonymous_in_store_receipt_has_no_customer_or_order()
    {
        var entities = await EntitiesAsync(
            "... on Receipt { store { id } customer { id } order { id } }",
            ("Receipt", "receipt-4"));

        Assert.Equal("store-9001", entities[0].GetProperty("store").GetProperty("id").GetString());
        Assert.Equal(JsonValueKind.Null, entities[0].GetProperty("customer").ValueKind);
        Assert.Equal(JsonValueKind.Null, entities[0].GetProperty("order").ValueKind);
    }

    [Fact]
    public async Task Item_details_resolve_to_the_matching_union_member()
    {
        var entities = await EntitiesAsync(
            """
            ... on Receipt {
              items {
                edges {
                  node {
                    details {
                      __typename
                      ... on ReceiptItemProduct { name quantity unitPrice product { id } }
                      ... on ReceiptItemBag { quantity totalPrice }
                    }
                  }
                }
              }
            }
            """,
            ("Receipt", "receipt-1"));

        var items = entities[0].GetProperty("items").GetProperty("edges").EnumerateArray().Select(e => e.GetProperty("node").GetProperty("details")).ToList();

        Assert.Equal(["ReceiptItemProduct", "ReceiptItemProduct", "ReceiptItemBag"], items.Select(d => d.GetProperty("__typename").GetString()));
        Assert.Equal("Full Cream Milk 2L", items[0].GetProperty("name").GetString());
        Assert.Equal("product-milk", items[0].GetProperty("product").GetProperty("id").GetString());
        Assert.Equal(0.8, items[1].GetProperty("quantity").GetDouble());
        Assert.Equal(0.15m, items[2].GetProperty("totalPrice").GetDecimal());
    }

    [Fact]
    public async Task Items_are_paged_with_cursors()
    {
        var first = (await EntitiesAsync(
            "... on Receipt { items(pagination: { first: 2 }) { edges { cursor node { details { ... on ReceiptItemProduct { name } } } } pageInfo { hasNextPage hasPreviousPage startCursor endCursor } totalCount } }",
            ("Receipt", "receipt-1")))[0].GetProperty("items");

        var firstEdges = first.GetProperty("edges");
        var firstPageInfo = first.GetProperty("pageInfo");
        Assert.Equal(2, firstEdges.GetArrayLength());
        Assert.True(firstPageInfo.GetProperty("hasNextPage").GetBoolean());
        Assert.Equal(3, first.GetProperty("totalCount").GetInt32());
        Assert.Equal(firstPageInfo.GetProperty("startCursor").GetString(), firstEdges[0].GetProperty("cursor").GetString());
        Assert.Equal(firstPageInfo.GetProperty("endCursor").GetString(), firstEdges[1].GetProperty("cursor").GetString());

        // Resuming from an individual edge cursor (not only the page's end cursor) continues right after that item.
        var cursor = firstEdges[0].GetProperty("cursor").GetString();
        var second = (await EntitiesAsync(
            $"... on Receipt {{ items(pagination: {{ first: 2, after: \"{cursor}\" }}) {{ edges {{ node {{ details {{ __typename }} }} }} pageInfo {{ hasNextPage hasPreviousPage }} }} }}",
            ("Receipt", "receipt-1")))[0].GetProperty("items");

        var secondEdges = second.GetProperty("edges");
        Assert.Equal(2, secondEdges.GetArrayLength());
        Assert.Equal("ReceiptItemProduct", secondEdges[0].GetProperty("node").GetProperty("details").GetProperty("__typename").GetString());
        Assert.Equal("ReceiptItemBag", secondEdges[1].GetProperty("node").GetProperty("details").GetProperty("__typename").GetString());
        Assert.False(second.GetProperty("pageInfo").GetProperty("hasNextPage").GetBoolean());
        Assert.True(second.GetProperty("pageInfo").GetProperty("hasPreviousPage").GetBoolean());
    }

    [Fact]
    public async Task Substitutions_promotions_and_invoice_details_resolve()
    {
        var entities = await EntitiesAsync(
            """
            ... on Receipt {
              invoiceType
              invoiceDetails { name abn poNumber email }
              items { edges { node {
                promotion { type originalUnitPrice }
                substitutions { product { name unitPrice } }
              } } }
            }
            """,
            ("Receipt", "receipt-2"));

        var receipt = entities[0];
        Assert.Equal("CREATED", receipt.GetProperty("invoiceType").GetString());
        Assert.Equal("51 824 753 556", receipt.GetProperty("invoiceDetails").GetProperty("abn").GetString());
        Assert.Equal(JsonValueKind.Null, receipt.GetProperty("invoiceDetails").GetProperty("email").ValueKind);

        var items = receipt.GetProperty("items").GetProperty("edges").EnumerateArray().Select(e => e.GetProperty("node")).ToList();
        Assert.Equal("COLES_PLUS_SAVER_DISCOUNT", items[0].GetProperty("promotion").GetProperty("type").GetString());
        Assert.Equal(5.00m, items[0].GetProperty("promotion").GetProperty("originalUnitPrice").GetDecimal());
        Assert.Equal("Almond Milk 1L", items[1].GetProperty("substitutions")[0].GetProperty("product").GetProperty("name").GetString());
    }

    [Fact]
    public async Task Payments_tenders_and_flybuys_resolve()
    {
        var entities = await EntitiesAsync(
            """
            ... on Receipt {
              payments { purchaseAmount refundAmount paymentMethod { type scheme truncatedCard } metadata { merchantName transactionDate responseMessage } }
              tenders { id type amount changeReturned }
              flybuys { barcode memberNumber basePointsEarned bonusPointsEarned pointsDeducted }
            }
            """,
            ("Receipt", "receipt-2"), ("Receipt", "receipt-1"));

        var online = entities[0];
        var payment = online.GetProperty("payments")[0];
        Assert.Equal(81.45m, payment.GetProperty("purchaseAmount").GetDecimal());
        Assert.Equal(JsonValueKind.Null, payment.GetProperty("refundAmount").ValueKind);
        Assert.Equal("CREDIT", payment.GetProperty("paymentMethod").GetProperty("type").GetString());
        Assert.Equal("VISA", payment.GetProperty("paymentMethod").GetProperty("scheme").GetString());
        Assert.Equal("Approved", payment.GetProperty("metadata").GetProperty("responseMessage").GetString());

        var inStore = entities[1];
        Assert.Equal("tender-1", inStore.GetProperty("tenders")[0].GetProperty("id").GetString());
        Assert.Equal("EFTPOS", inStore.GetProperty("tenders")[0].GetProperty("type").GetString());
        Assert.Equal("9876543210123", inStore.GetProperty("flybuys").GetProperty("barcode").GetString());
        Assert.Equal("1234567890123456", inStore.GetProperty("flybuys").GetProperty("memberNumber").GetString());
        Assert.Equal(10, inStore.GetProperty("flybuys").GetProperty("bonusPointsEarned").GetInt32());
    }

    [Fact]
    public async Task Money_keeps_its_decimal_precision()
    {
        var entities = await EntitiesAsync("... on Receipt { amount { total savings } }", ("Receipt", "receipt-1"));

        Assert.Equal(14.80m, entities[0].GetProperty("amount").GetProperty("total").GetDecimal());
        // Raw text keeps the trailing zero, proving the value was never converted to a binary double.
        Assert.Equal("14.80", entities[0].GetProperty("amount").GetProperty("total").GetRawText());
    }

    [Fact]
    public async Task Customer_receipts_are_newest_first_and_report_page_info()
    {
        var entities = await EntitiesAsync(
            "... on Customer { receipts { edges { cursor node { id } } pageInfo { hasNextPage startCursor endCursor } totalCount } }",
            ("Customer", "customer-1"));

        var result = entities[0].GetProperty("receipts");
        var edges = result.GetProperty("edges");
        Assert.Equal(["receipt-3", "receipt-2", "receipt-1"], edges.EnumerateArray().Select(e => e.GetProperty("node").GetProperty("id").GetString()));
        Assert.False(result.GetProperty("pageInfo").GetProperty("hasNextPage").GetBoolean());
        Assert.Equal(3, result.GetProperty("totalCount").GetInt32());
        Assert.Equal(result.GetProperty("pageInfo").GetProperty("startCursor").GetString(), edges[0].GetProperty("cursor").GetString());
        Assert.Equal(result.GetProperty("pageInfo").GetProperty("endCursor").GetString(), edges[2].GetProperty("cursor").GetString());
    }

    [Fact]
    public async Task Customer_receipts_honour_filter_and_pagination()
    {
        var entities = await EntitiesAsync(
            """
            ... on Customer {
              receipts(input: {
                filter: { storeSalesChannel: COLES_ONLINE, dateFrom: "2025-11-01T00:00:00Z" }
                pagination: { first: 1 }
              }) { edges { node { id } } pageInfo { hasNextPage } totalCount }
            }
            """,
            ("Customer", "customer-1"));

        var result = entities[0].GetProperty("receipts");
        Assert.Equal(["receipt-3"], result.GetProperty("edges").EnumerateArray().Select(e => e.GetProperty("node").GetProperty("id").GetString()));
        Assert.True(result.GetProperty("pageInfo").GetProperty("hasNextPage").GetBoolean());
        Assert.Equal(2, result.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Filter_variables_are_coerced_for_ids_enums_and_dates()
    {
        var variables = new Dictionary<string, object?>
        {
            ["input"] = new Dictionary<string, object?>
            {
                ["filter"] = new Dictionary<string, object?>
                {
                    ["orderIds"] = new[] { "order-1001" },
                    ["storeBrand"] = "COLES_SUPERMARKET",
                    ["dateTo"] = "2025-11-06T00:00:00Z",
                },
            },
        };

        using var result = await _harness.QueryAsync(
            """
            query($input: ReceiptsInput) {
              _entities(representations: [{ __typename: "Customer", id: "customer-1" }]) {
                ... on Customer { receipts(input: $input) { edges { node { id } } } }
              }
            }
            """,
            variables);

        var receipts = result.RootElement.GetProperty("data").GetProperty("_entities")[0]
            .GetProperty("receipts").GetProperty("edges");
        Assert.Equal(["receipt-2"], receipts.EnumerateArray().Select(e => e.GetProperty("node").GetProperty("id").GetString()));
    }

    [Fact]
    public async Task Order_receipts_include_returns_against_the_same_order()
    {
        var entities = await EntitiesAsync(
            "... on Order { receipts { edges { node { id return } } } }",
            ("Order", "order-1001"));

        var receipts = entities[0].GetProperty("receipts").GetProperty("edges").EnumerateArray().Select(e => e.GetProperty("node")).ToList();
        Assert.Equal(["receipt-3", "receipt-2"], receipts.Select(r => r.GetProperty("id").GetString()));
        Assert.True(receipts[0].GetProperty("return").GetBoolean());
    }

    [Fact]
    public async Task Customer_without_receipts_gets_an_empty_result_not_an_error()
    {
        var entities = await EntitiesAsync(
            "... on Customer { receipts { edges { node { id } } pageInfo { hasNextPage } totalCount } }",
            ("Customer", "nobody"));

        var result = entities[0].GetProperty("receipts");
        Assert.Equal(0, result.GetProperty("edges").GetArrayLength());
        Assert.Equal(0, result.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Resolving_the_entity_reference_itself_does_not_touch_the_repository()
    {
        await EntitiesAsync("... on Customer { id } ... on Order { id }", ("Customer", "c1"), ("Order", "o1"));

        Assert.Empty(_harness.Repository.IdLookups);
        Assert.Empty(_harness.Repository.OwnerLookups);
    }

    [Fact]
    public async Task Receipt_lists_for_many_customers_are_batched_into_one_repository_call()
    {
        await EntitiesAsync(
            "... on Customer { receipts { edges { node { id } } } }",
            ("Customer", "customer-1"), ("Customer", "customer-2"), ("Customer", "customer-3"));

        var lookup = Assert.Single(_harness.Repository.OwnerLookups);
        Assert.Equal(ReceiptOwnerKind.Customer, lookup.Kind);
        Assert.Equal(["customer-1", "customer-2", "customer-3"], lookup.OwnerIds.Order());
    }

    [Fact]
    public async Task Customers_and_orders_are_looked_up_separately_but_each_only_once()
    {
        await EntitiesAsync(
            "... on Customer { receipts { edges { node { id } } } } ... on Order { receipts { edges { node { id } } } }",
            ("Customer", "customer-1"), ("Customer", "customer-2"), ("Order", "order-1001"), ("Order", "order-2"));

        Assert.Equal(2, _harness.Repository.OwnerLookups.Count);
        Assert.Contains(_harness.Repository.OwnerLookups, l => l.Kind == ReceiptOwnerKind.Customer && l.OwnerIds.Length == 2);
        Assert.Contains(_harness.Repository.OwnerLookups, l => l.Kind == ReceiptOwnerKind.Order && l.OwnerIds.Length == 2);
    }

    [Fact]
    public async Task Same_field_with_different_arguments_is_not_served_from_the_wrong_cache_entry()
    {
        using var result = await _harness.QueryAsync(
            """
            {
              _entities(representations: [{ __typename: "Customer", id: "customer-1" }]) {
                ... on Customer {
                  newest: receipts(input: { pagination: { first: 1 } }) { edges { node { id } } }
                  oldest: receipts(input: { pagination: { last: 1 } }) { edges { node { id } } }
                }
              }
            }
            """);

        var customer = result.RootElement.GetProperty("data").GetProperty("_entities")[0];
        Assert.Equal("receipt-3", customer.GetProperty("newest").GetProperty("edges")[0].GetProperty("node").GetProperty("id").GetString());
        Assert.Equal("receipt-1", customer.GetProperty("oldest").GetProperty("edges")[0].GetProperty("node").GetProperty("id").GetString());
    }

    [Fact]
    public async Task Invalid_pagination_returns_a_coded_error_without_exception_details()
    {
        using var result = await _harness.QueryAsync(
            """
            {
              _entities(representations: [{ __typename: "Customer", id: "customer-1" }]) {
                ... on Customer { receipts(input: { pagination: { first: 1000 } }) { edges { node { id } } } }
              }
            }
            """);

        var error = result.RootElement.GetProperty("errors")[0];
        Assert.Equal("INVALID_PAGINATION", error.GetProperty("extensions").GetProperty("code").GetString());
        Assert.Contains("between 0 and 100", error.GetProperty("message").GetString());
        Assert.DoesNotContain("InvalidPaginationException", result.RootElement.GetRawText());
    }

    [Fact]
    public async Task Invalid_cursor_returns_a_coded_error()
    {
        var result = await _harness.QueryAsync(
            "query($reps: [_Any!]!) { _entities(representations: $reps) { ... on Receipt { items(pagination: { after: \"nope\" }) { edges { node { __typename } } } } } }",
            Reps(("Receipt", "receipt-1")));

        Assert.Equal(
            "INVALID_PAGINATION",
            result.RootElement.GetProperty("errors")[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Abusively_deep_queries_are_rejected()
    {
        var deep = string.Concat(Enumerable.Repeat("customer { receipts { edges { node { ", 8))
            + "id"
            + string.Concat(Enumerable.Repeat(" } } } }", 8));

        using var result = await _harness.QueryAsync(
            $"{{ _entities(representations: [{{ __typename: \"Receipt\", id: \"receipt-1\" }}]) {{ ... on Receipt {{ {deep} }} }} }}");

        Assert.True(result.RootElement.TryGetProperty("errors", out _));
    }
}
