using ReceiptSubgraph.Data;
using ReceiptSubgraph.Domain;
using ReceiptSubgraph.Paging;

namespace ReceiptSubgraph.Tests.Data;

public class ReceiptRepositoryTests
{
    private const string Customer = "customer-1";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly ReceiptRepository _repository = new(new PaginationOptions());

    private async Task<Page<Receipt>> ListAsync(ReceiptResultInput? input = null, string customerId = Customer)
        => await _repository.GetByCustomerAsync(new ReceiptUserContext(customerId), input, Ct);

    [Fact]
    public async Task Receipts_for_the_customer_are_newest_first_and_anonymous_receipts_are_never_listed()
    {
        var page = await ListAsync();

        Assert.Equal(["receipt-3", "receipt-2", "receipt-1"], page.Items.Select(r => r.Id));
        Assert.Equal(3, page.TotalCount);
    }

    [Fact]
    public async Task An_unknown_customer_gets_an_empty_page()
    {
        var page = await ListAsync(customerId: "nobody");

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task Filters_are_combined_and_the_date_range_is_inclusive()
    {
        var filter = new ReceiptFilter
        {
            SalesChannel = TransactionSalesChannel.ColesOnline,
            DateFrom = new DateTimeOffset(2025, 11, 5, 18, 30, 0, TimeSpan.Zero),
            DateTo = new DateTimeOffset(2025, 11, 6, 0, 0, 0, TimeSpan.Zero),
        };

        var page = await ListAsync(new ReceiptResultInput(filter));

        Assert.Equal(["receipt-2"], page.Items.Select(r => r.Id));
    }

    [Fact]
    public async Task An_empty_order_id_list_matches_nothing()
    {
        var page = await ListAsync(new ReceiptResultInput(new ReceiptFilter { OrderIds = [] }));

        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task Only_the_requested_page_is_returned_with_the_total_across_all_pages()
    {
        var page = await ListAsync(new ReceiptResultInput(Pagination: new PageRequest(First: 1)));

        Assert.Equal(["receipt-3"], page.Items.Select(r => r.Id));
        Assert.Equal(3, page.TotalCount);
        Assert.True(page.PageInfo.HasNextPage);
    }

    [Fact]
    public async Task GetByIds_returns_only_the_customers_receipts_that_exist()
    {
        var result = await _repository.GetByIdsAsync(
            new ReceiptUserContext(Customer),
            ["receipt-1", "receipt-4", "missing"],
            Ct);

        // receipt-4 is anonymous, so no customer owns it.
        Assert.Equal(["receipt-1"], result.Keys);
    }
}
