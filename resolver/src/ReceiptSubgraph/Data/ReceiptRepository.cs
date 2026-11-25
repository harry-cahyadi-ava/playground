using ReceiptSubgraph.Domain;
using ReceiptSubgraph.Paging;

namespace ReceiptSubgraph.Data;

/// <summary>
/// <see cref="IReceiptRepository"/> that simulates a database with a hard-coded set of receipts
/// (<see cref="SimulatedReceiptData"/>). Filtering, ordering and paging are written as a single LINQ query over the
/// rows, with only the page that is returned being materialised, so replacing it with a real data source later
/// only means swapping <see cref="Query"/>.
/// </summary>
public sealed class ReceiptRepository : IReceiptRepository
{
    private readonly PaginationOptions _paginationOptions;

    public ReceiptRepository(PaginationOptions paginationOptions)
    {
        _paginationOptions = paginationOptions;
    }

    // Stands in for the receipts table. Each call starts a new query, as a DbSet or table would.
    private static IQueryable<Receipt> Query => SimulatedReceiptData.Receipts.AsQueryable();

    public ValueTask<IReadOnlyDictionary<string, Receipt>> GetByIdsAsync(
        ReceiptUserContext user,
        IReadOnlyList<string> receiptIds,
        CancellationToken cancellationToken)
    {
        // One query for all ids (WHERE CustomerId = @customer AND Id IN (...)). Receipts owned by another customer
        // (or by no customer) never match, and ids without a row are absent from the dictionary.
        var receipts = Query
            .Where(r => r.CustomerId == user.CustomerId && receiptIds.Contains(r.Id))
            .ToDictionary(r => r.Id);

        return ValueTask.FromResult<IReadOnlyDictionary<string, Receipt>>(receipts);
    }

    public ValueTask<Page<Receipt>> GetByCustomerAsync(
        ReceiptUserContext user,
        ReceiptResultInput? input,
        CancellationToken cancellationToken)
    {
        var filter = input?.Filter;

        // An empty order id list means "match nothing", not "no order filter".
        if (filter?.OrderIds is { Count: 0 })
        {
            return ValueTask.FromResult(Page.Empty<Receipt>());
        }

        // Receipts without a customer are never listed. The loyalty association code is not used by this simulated store.
        var query = Query.Where(r => r.CustomerId == user.CustomerId);

        if (filter is not null)
        {
            query = ApplyFilter(query, filter);
        }

        // Newest first, with the id as tie-breaker so offset cursors stay stable between requests.
        var ordered = query
            .OrderByDescending(r => r.CreatedAt)
            .ThenBy(r => r.Id);

        var totalCount = ordered.Count();

        if (totalCount == 0)
        {
            return ValueTask.FromResult(Page.Empty<Receipt>());
        }

        // Invalid paging arguments throw InvalidPaginationException.
        var window = Paginator.ComputeWindow(input?.Pagination, _paginationOptions, totalCount);

        // OFFSET window.Start FETCH window.Count
        var receipts = ordered
            .Skip(window.Start)
            .Take(window.Count)
            .ToList();

        return ValueTask.FromResult(Paginator.BuildPage(receipts, window.Start, totalCount));
    }

    // Every property that is set must match (intersection); unset properties are ignored.
    private static IQueryable<Receipt> ApplyFilter(IQueryable<Receipt> query, ReceiptFilter filter)
    {
        if (filter.OrderIds is { } orderIds)
        {
            query = query.Where(r => r.OrderId != null && orderIds.Contains(r.OrderId));
        }

        if (filter.StoreId is { } storeId)
        {
            query = query.Where(r => r.StoreId == storeId);
        }

        if (filter.StoreBrand is { } storeBrand)
        {
            query = query.Where(r => r.StoreBrand == storeBrand);
        }

        if (filter.SalesChannel is { } salesChannel)
        {
            query = query.Where(r => r.SalesChannel == salesChannel);
        }

        if (filter.DateFrom is { } dateFrom)
        {
            query = query.Where(r => r.CreatedAt >= dateFrom);
        }

        if (filter.DateTo is { } dateTo)
        {
            query = query.Where(r => r.CreatedAt <= dateTo);
        }

        return query;
    }
}
