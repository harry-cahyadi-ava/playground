using ReceiptSubgraph.Data;
using ReceiptSubgraph.Domain;
using ReceiptSubgraph.Paging;

namespace ReceiptSubgraph.Tests.Data;

public class InMemoryReceiptRepositoryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Deliberately unordered so the tests prove the repository sorts newest first.
    private static readonly Receipt[] Receipts =
    [
        TestReceipts.Create("r1", TestReceipts.Day(1), orderId: "o1", storeId: "s1"),
        TestReceipts.Create("r3", TestReceipts.Day(3), orderId: "o1", storeId: "s2", channel: StoreSalesChannel.ColesOnline),
        TestReceipts.Create("r2", TestReceipts.Day(2), orderId: "o2", storeId: "s1", brand: StoreBrand.Liquorland),
        TestReceipts.Create("r4", TestReceipts.Day(4), customerId: "customer-2", orderId: "o3", storeId: "s1"),
        TestReceipts.Create("anon", TestReceipts.Day(5), customerId: null),
    ];

    private readonly InMemoryReceiptRepository _repository = new(Receipts, new PaginationOptions());

    private async Task<string[]> CustomerReceiptIdsAsync(ReceiptResultInput? input, string customerId = "customer-1")
    {
        var result = await _repository.GetByOwnersAsync(ReceiptOwnerKind.Customer, [customerId], input, Ct);
        return result[customerId].Items.Select(r => r.Id).ToArray();
    }

    [Fact]
    public async Task GetByIds_returns_found_receipts_and_omits_unknown_ids()
    {
        var result = await _repository.GetByIdsAsync(["r1", "missing", "r2"], Ct);

        Assert.Equal(["r1", "r2"], result.Keys.Order());
    }

    [Fact]
    public async Task Receipts_for_an_owner_are_newest_first()
    {
        Assert.Equal(["r3", "r2", "r1"], await CustomerReceiptIdsAsync(null));
    }

    [Fact]
    public async Task Receipts_can_be_listed_per_order()
    {
        var result = await _repository.GetByOwnersAsync(ReceiptOwnerKind.Order, ["o1"], null, Ct);

        Assert.Equal(["r3", "r1"], result["o1"].Items.Select(r => r.Id));
    }

    [Fact]
    public async Task Many_owners_are_served_in_one_call_and_unknown_owners_get_an_empty_page()
    {
        var result = await _repository.GetByOwnersAsync(
            ReceiptOwnerKind.Customer, ["customer-1", "customer-2", "nobody"], null, Ct);

        Assert.Equal(3, result.Count);
        Assert.Equal(3, result["customer-1"].Items.Count);
        Assert.Equal(["r4"], result["customer-2"].Items.Select(r => r.Id));
        Assert.Empty(result["nobody"].Items);
    }

    [Fact]
    public async Task Receipts_without_a_customer_are_not_listed_for_anyone()
    {
        var result = await _repository.GetByOwnersAsync(ReceiptOwnerKind.Customer, ["customer-1", "customer-2"], null, Ct);

        Assert.DoesNotContain(result.Values.SelectMany(p => p.Items), r => r.Id == "anon");
    }

    [Fact]
    public async Task Filter_by_store_id()
    {
        var ids = await CustomerReceiptIdsAsync(Input(new ReceiptFilter { StoreId = "s1" }));

        Assert.Equal(["r2", "r1"], ids);
    }

    [Fact]
    public async Task Filter_by_brand()
    {
        var ids = await CustomerReceiptIdsAsync(Input(new ReceiptFilter { StoreBrand = StoreBrand.Liquorland }));

        Assert.Equal(["r2"], ids);
    }

    [Fact]
    public async Task Filter_by_sales_channel()
    {
        var ids = await CustomerReceiptIdsAsync(Input(new ReceiptFilter { StoreSalesChannel = StoreSalesChannel.ColesOnline }));

        Assert.Equal(["r3"], ids);
    }

    [Fact]
    public async Task Filter_by_order_ids()
    {
        var ids = await CustomerReceiptIdsAsync(Input(new ReceiptFilter { OrderIds = ["o1", "o2"] }));

        Assert.Equal(["r3", "r2", "r1"], ids);
    }

    [Fact]
    public async Task Filter_by_an_empty_order_id_list_matches_nothing()
    {
        Assert.Empty(await CustomerReceiptIdsAsync(Input(new ReceiptFilter { OrderIds = [] })));
    }

    [Fact]
    public async Task Date_range_is_inclusive_at_both_ends()
    {
        var filter = new ReceiptFilter { DateFrom = TestReceipts.Day(1), DateTo = TestReceipts.Day(2) };

        Assert.Equal(["r2", "r1"], await CustomerReceiptIdsAsync(Input(filter)));
    }

    [Fact]
    public async Task Date_range_excludes_receipts_outside_the_range()
    {
        var filter = new ReceiptFilter { DateFrom = TestReceipts.Day(2).AddSeconds(1), DateTo = TestReceipts.Day(3).AddSeconds(-1) };

        Assert.Empty(await CustomerReceiptIdsAsync(Input(filter)));
    }

    [Fact]
    public async Task Multiple_filter_properties_are_combined_with_AND()
    {
        var filter = new ReceiptFilter { StoreId = "s1", DateFrom = TestReceipts.Day(2), OrderIds = ["o1", "o2"] };

        Assert.Equal(["r2"], await CustomerReceiptIdsAsync(Input(filter)));
    }

    [Fact]
    public async Task Pagination_is_applied_after_filtering()
    {
        var input = new ReceiptResultInput(new ReceiptFilter { StoreId = "s1" }, new PageRequest(First: 1));

        var result = await _repository.GetByOwnersAsync(ReceiptOwnerKind.Customer, ["customer-1"], input, Ct);
        var page = result["customer-1"];

        Assert.Equal(["r2"], page.Items.Select(r => r.Id));
        Assert.True(page.PageInfo.HasNextPage);
        Assert.Equal(2, page.TotalCount);
    }

    [Fact]
    public async Task Ties_on_created_at_are_ordered_by_id_for_stable_cursors()
    {
        var sameTime = TestReceipts.Day(1);
        var repository = new InMemoryReceiptRepository(
            [TestReceipts.Create("b", sameTime), TestReceipts.Create("a", sameTime)],
            new PaginationOptions());

        var result = await repository.GetByOwnersAsync(ReceiptOwnerKind.Customer, ["customer-1"], null, Ct);

        Assert.Equal(["a", "b"], result["customer-1"].Items.Select(r => r.Id));
    }

    private static ReceiptResultInput Input(ReceiptFilter filter) => new(filter);
}
