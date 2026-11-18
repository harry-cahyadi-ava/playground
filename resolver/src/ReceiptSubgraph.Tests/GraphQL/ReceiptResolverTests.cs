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

    private Task<JsonDocument> EntitiesResponseAsync(string? customerId, string selection, params (string Type, string Id)[] representations)
        => _harness.QueryAsync(EntitiesQuery.Replace("%SELECTION%", selection), Reps(representations), customerId);

    // Entity references are resolved as the customer that owns the sample receipts.
    private async Task<JsonElement> EntitiesAsync(string selection, params (string Type, string Id)[] representations)
    {
        var result = await EntitiesResponseAsync("customer-1", selection, representations);
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
    public async Task Receipt_references_that_are_missing_or_not_owned_resolve_to_null_with_an_identical_not_found_error()
    {
        // receipt-4 is anonymous, so no customer owns it; receipt-1 is only resolved when it is the signed-in customer's.
        var result = await EntitiesResponseAsync(
            "customer-1",
            "... on Receipt { id }",
            ("Receipt", "receipt-1"), ("Receipt", "missing"), ("Receipt", "receipt-4"));

        var root = result.RootElement;
        var entities = root.GetProperty("data").GetProperty("_entities");
        Assert.Equal("receipt-1", entities[0].GetProperty("id").GetString());
        Assert.Equal(JsonValueKind.Null, entities[1].ValueKind);
        Assert.Equal(JsonValueKind.Null, entities[2].ValueKind);

        var errors = root.GetProperty("errors").EnumerateArray().ToList();
        Assert.Equal(2, errors.Count);
        AssertNotFound(errors[0], "missing");
        AssertNotFound(errors[1], "receipt-4");
    }

    [Fact]
    public async Task A_receipt_reference_owned_by_another_customer_is_reported_like_a_missing_one()
    {
        var result = await EntitiesResponseAsync("customer-2", "... on Receipt { id }", ("Receipt", "receipt-1"));

        var root = result.RootElement;
        Assert.Equal(JsonValueKind.Null, root.GetProperty("data").GetProperty("_entities")[0].ValueKind);
        AssertNotFound(Assert.Single(root.GetProperty("errors").EnumerateArray()), "receipt-1");
    }

    [Fact]
    public async Task Receipt_references_without_a_signed_in_customer_are_unauthenticated_and_never_reach_the_repository()
    {
        var result = await EntitiesResponseAsync(null, "... on Receipt { id }", ("Receipt", "receipt-1"));

        var error = result.RootElement.GetProperty("errors")[0];
        Assert.Equal("UNAUTHENTICATED", error.GetProperty("extensions").GetProperty("code").GetString());
        Assert.Empty(_harness.Repository.IdLookups);
    }

    // The path stops at the _entities field; the receipt is identified by the message.
    private static void AssertNotFound(JsonElement error, string receiptId)
    {
        Assert.Equal($"Receipt '{receiptId}' was not found.", error.GetProperty("message").GetString());
        Assert.Equal("NOT_FOUND", error.GetProperty("extensions").GetProperty("code").GetString());
        Assert.Equal(["_entities"], error.GetProperty("path").EnumerateArray().Select(p => p.GetString()));
    }

    [Fact]
    public async Task Many_receipt_references_are_loaded_with_a_single_repository_call()
    {
        await EntitiesResponseAsync(
            "customer-1",
            "... on Receipt { id }",
            ("Receipt", "receipt-1"), ("Receipt", "receipt-2"), ("Receipt", "receipt-3"), ("Receipt", "missing"));

        var lookup = Assert.Single(_harness.Repository.IdLookups);
        Assert.Equal("customer-1", lookup.User.CustomerId);
        Assert.Equal(["missing", "receipt-1", "receipt-2", "receipt-3"], lookup.ReceiptIds.Order());
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
    public async Task Receipt_without_an_order_has_no_order_reference()
    {
        await using var harness = await SubgraphHarness.CreateAsync([TestReceipts.Create("r1", orderId: null, storeId: "store-9")]);

        var result = await harness.QueryAsync(
            EntitiesQuery.Replace("%SELECTION%", "... on Receipt { store { id } customer { id } order { id } }"),
            Reps(("Receipt", "r1")),
            customerId: "customer-1");

        var receipt = result.RootElement.GetProperty("data").GetProperty("_entities")[0];
        Assert.Equal("store-9", receipt.GetProperty("store").GetProperty("id").GetString());
        Assert.Equal("customer-1", receipt.GetProperty("customer").GetProperty("id").GetString());
        Assert.Equal(JsonValueKind.Null, receipt.GetProperty("order").ValueKind);
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

    private async Task<JsonElement> ReceiptsAsync(string query, string? customerId = "customer-1", Dictionary<string, object?>? variables = null)
    {
        var result = await _harness.QueryAsync(query, variables, customerId);
        Assert.False(result.RootElement.TryGetProperty("errors", out var errors), errors.ToString());
        return result.RootElement.GetProperty("data");
    }

    [Fact]
    public async Task Receipts_are_newest_first_and_report_page_info()
    {
        var data = await ReceiptsAsync(
            "{ receipts { ... on ReceiptConnection { edges { cursor node { id } } pageInfo { hasNextPage startCursor endCursor } totalCount } } }");

        var result = data.GetProperty("receipts");
        var edges = result.GetProperty("edges");
        Assert.Equal(["receipt-3", "receipt-2", "receipt-1"], edges.EnumerateArray().Select(e => e.GetProperty("node").GetProperty("id").GetString()));
        Assert.False(result.GetProperty("pageInfo").GetProperty("hasNextPage").GetBoolean());
        Assert.Equal(3, result.GetProperty("totalCount").GetInt32());
        Assert.Equal(result.GetProperty("pageInfo").GetProperty("startCursor").GetString(), edges[0].GetProperty("cursor").GetString());
        Assert.Equal(result.GetProperty("pageInfo").GetProperty("endCursor").GetString(), edges[2].GetProperty("cursor").GetString());
    }

    [Fact]
    public async Task Receipts_honour_filter_and_pagination()
    {
        var data = await ReceiptsAsync(
            """
            {
              receipts(input: {
                filter: { storeSalesChannel: COLES_ONLINE, dateFrom: "2025-11-01T00:00:00Z" }
                pagination: { first: 1 }
              }) { ... on ReceiptConnection { edges { node { id } } pageInfo { hasNextPage } totalCount } }
            }
            """);

        var result = data.GetProperty("receipts");
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

        var data = await ReceiptsAsync(
            "query($input: ReceiptsInput) { receipts(input: $input) { ... on ReceiptConnection { edges { node { id } } } } }",
            variables: variables);

        var receipts = data.GetProperty("receipts").GetProperty("edges");
        Assert.Equal(["receipt-2"], receipts.EnumerateArray().Select(e => e.GetProperty("node").GetProperty("id").GetString()));
    }

    [Fact]
    public async Task Receipts_include_returns_against_the_same_order()
    {
        var data = await ReceiptsAsync(
            "{ receipts(input: { filter: { orderIds: [\"order-1001\"] } }) { ... on ReceiptConnection { edges { node { id return } } } } }");

        var receipts = data.GetProperty("receipts").GetProperty("edges").EnumerateArray().Select(e => e.GetProperty("node")).ToList();
        Assert.Equal(["receipt-3", "receipt-2"], receipts.Select(r => r.GetProperty("id").GetString()));
        Assert.True(receipts[0].GetProperty("return").GetBoolean());
    }

    [Fact]
    public async Task Customer_without_receipts_gets_an_empty_result_not_an_error()
    {
        var data = await ReceiptsAsync(
            "{ receipts { ... on ReceiptConnection { edges { node { id } } pageInfo { hasNextPage } totalCount } } }",
            customerId: "nobody");

        var result = data.GetProperty("receipts");
        Assert.Equal(0, result.GetProperty("edges").GetArrayLength());
        Assert.Equal(0, result.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Receipts_only_ever_come_from_the_signed_in_customer()
    {
        await ReceiptsAsync("{ receipts { __typename } }", customerId: "customer-2");

        var lookup = Assert.Single(_harness.Repository.CustomerLookups);
        Assert.Equal("customer-2", lookup.User.CustomerId);
    }

    [Fact]
    public async Task Aliases_with_different_arguments_are_resolved_independently()
    {
        var data = await ReceiptsAsync(
            """
            {
              newest: receipts(input: { pagination: { first: 1 } }) { ... on ReceiptConnection { edges { node { id } } } }
              oldest: receipts(input: { pagination: { last: 1 } }) { ... on ReceiptConnection { edges { node { id } } } }
            }
            """);

        Assert.Equal("receipt-3", data.GetProperty("newest").GetProperty("edges")[0].GetProperty("node").GetProperty("id").GetString());
        Assert.Equal("receipt-1", data.GetProperty("oldest").GetProperty("edges")[0].GetProperty("node").GetProperty("id").GetString());
    }

    [Fact]
    public async Task Receipt_by_id_returns_the_customers_receipt()
    {
        var data = await ReceiptsAsync(
            "{ receipt(id: \"receipt-2\") { __typename ... on Receipt { id customer { id } order { id } } } }");

        var receipt = data.GetProperty("receipt");
        Assert.Equal("Receipt", receipt.GetProperty("__typename").GetString());
        Assert.Equal("receipt-2", receipt.GetProperty("id").GetString());
        Assert.Equal("customer-1", receipt.GetProperty("customer").GetProperty("id").GetString());
    }

    [Fact]
    public async Task Receipts_requested_by_id_in_one_query_are_loaded_with_a_single_repository_call()
    {
        var data = await ReceiptsAsync(
            """
            {
              first: receipt(id: "receipt-1") { ... on Receipt { id } }
              second: receipt(id: "receipt-2") { ... on Receipt { id } }
              again: receipt(id: "receipt-1") { ... on Receipt { id } }
            }
            """);

        Assert.Equal("receipt-1", data.GetProperty("first").GetProperty("id").GetString());
        Assert.Equal("receipt-2", data.GetProperty("second").GetProperty("id").GetString());
        Assert.Equal("receipt-1", data.GetProperty("again").GetProperty("id").GetString());

        // One batch, and the repeated id is only asked for once.
        var lookup = Assert.Single(_harness.Repository.IdLookups);
        Assert.Equal("customer-1", lookup.User.CustomerId);
        Assert.Equal(["receipt-1", "receipt-2"], lookup.ReceiptIds.Order());
    }

    [Fact]
    public async Task The_loyalty_association_code_reaches_the_repository_with_the_customer()
    {
        await _harness.QueryAsync(
            "{ receipt(id: \"receipt-1\") { __typename } receipts { __typename } }",
            customerId: "customer-1",
            loyaltyAssociationCode: "ABC123");

        var expected = new ReceiptUserContext("customer-1", "ABC123");
        Assert.Equal(expected, Assert.Single(_harness.Repository.IdLookups).User);
        Assert.Equal(expected, Assert.Single(_harness.Repository.CustomerLookups).User);
    }

    [Fact]
    public async Task Receipt_and_receipts_in_one_query_use_the_matching_repository_method_once_each()
    {
        await ReceiptsAsync(
            """
            {
              receipt(id: "receipt-1") { __typename }
              receipts(input: { filter: { storeId: "store-1" } }) { __typename }
            }
            """);

        Assert.Equal(["receipt-1"], Assert.Single(_harness.Repository.IdLookups).ReceiptIds);

        var listing = Assert.Single(_harness.Repository.CustomerLookups);
        Assert.Equal("customer-1", listing.User.CustomerId);
        Assert.Equal("store-1", listing.Input?.Filter?.StoreId);
    }

    [Theory]
    [InlineData("customer-2", "receipt-1")] // belongs to another customer
    [InlineData("customer-1", "missing")] // does not exist
    [InlineData("customer-1", "receipt-4")] // anonymous in-store receipt: no customer to own it
    public async Task Receipt_by_id_is_not_found_unless_the_signed_in_customer_owns_it(string customerId, string receiptId)
    {
        var data = await ReceiptsAsync(
            $"{{ receipt(id: \"{receiptId}\") {{ __typename ... on NotFoundError {{ message }} }} }}",
            customerId);

        var result = data.GetProperty("receipt");
        Assert.Equal("NotFoundError", result.GetProperty("__typename").GetString());
        Assert.Equal($"Receipt '{receiptId}' was not found.", result.GetProperty("message").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Queries_without_a_signed_in_customer_are_unauthorized_and_never_reach_the_repository(string? customerId)
    {
        var data = await ReceiptsAsync(
            "{ receipt(id: \"receipt-1\") { __typename } receipts { __typename ... on UnauthorizedError { message } } }",
            customerId);

        Assert.Equal("UnauthorizedError", data.GetProperty("receipt").GetProperty("__typename").GetString());
        Assert.Equal("UnauthorizedError", data.GetProperty("receipts").GetProperty("__typename").GetString());
        Assert.Empty(_harness.Repository.IdLookups);
        Assert.Empty(_harness.Repository.CustomerLookups);
    }

    [Fact]
    public async Task Unexpected_repository_failures_become_an_internal_server_error()
    {
        _harness.Repository.FailWith(new InvalidOperationException("database is down"));

        var data = await ReceiptsAsync(
            "{ receipt(id: \"receipt-1\") { __typename } receipts { __typename ... on InternalServerError { message } } }");

        Assert.Equal("InternalServerError", data.GetProperty("receipt").GetProperty("__typename").GetString());
        Assert.Equal("InternalServerError", data.GetProperty("receipts").GetProperty("__typename").GetString());
        // The internal reason is logged, never sent to the client.
        Assert.DoesNotContain("database is down", data.GetRawText());
    }

    [Fact]
    public async Task Invalid_pagination_returns_a_coded_error_without_exception_details()
    {
        using var result = await _harness.QueryAsync(
            "{ receipts(input: { pagination: { first: 1000 } }) { ... on ReceiptConnection { edges { node { id } } } } }",
            customerId: "customer-1");

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
            Reps(("Receipt", "receipt-1")),
            customerId: "customer-1");

        Assert.Equal(
            "INVALID_PAGINATION",
            result.RootElement.GetProperty("errors")[0].GetProperty("extensions").GetProperty("code").GetString());
    }
}
