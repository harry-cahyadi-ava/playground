using ReceiptSubgraph.Domain;
using ReceiptSubgraph.Paging;

namespace ReceiptSubgraph.Data;

/// <summary>
/// Database-backed <see cref="IReceiptRepository"/>. Filtering, ordering and paging happen in the query, and the
/// fetched rows are mapped to <see cref="Receipt"/> afterwards so the mapping cost is bounded by the page size.
/// </summary>
// TODO: Register this in ServiceCollectionExtensions in place of InMemoryReceiptRepository (decide the lifetime
// based on the data access library, e.g. scoped for a DbContext, singleton for a connection factory).
public sealed class ReceiptRepository : IReceiptRepository
{
    private readonly PaginationOptions _paginationOptions;

    // TODO: Inject the data access dependency (e.g. a DbContext, IDbConnectionFactory or DataSource), the row
    // mapper and a logger as needed.
    public ReceiptRepository(PaginationOptions paginationOptions)
    {
        _paginationOptions = paginationOptions;
    }

    public ValueTask<IReadOnlyDictionary<string, Receipt>> GetByIdsAsync(
        ReceiptUserContext user,
        IReadOnlyList<string> receiptIds,
        CancellationToken cancellationToken)
    {
        // TODO: Fetch all ids in a single query (WHERE CustomerId = @user.CustomerId AND Id IN (...)), not one query per id.
        // Receipts owned by another customer (or by no customer) must not be returned or mapped.
        // TODO: Load child data (items, payments, etc.) with batched queries keyed by the fetched receipt ids.
        // TODO: Map only the returned rows to Receipt.
        // TODO: Return a dictionary keyed by receipt id. Ids without a row must be absent from the dictionary.
        throw new NotImplementedException();
    }

    public ValueTask<Page<Receipt>> GetByCustomerAsync(
        ReceiptUserContext user,
        ReceiptResultInput? input,
        CancellationToken cancellationToken)
    {
        // TODO: Translate input.Filter into WHERE clauses (OrderIds, StoreId, StoreBrand, StoreSalesChannel,
        // DateFrom/DateTo, both inclusive). An empty OrderIds list means "match nothing", not "no order filter".
        // TODO: Restrict to the customer (WHERE CustomerId = @user.CustomerId). Receipts without a customer are never listed.
        // TODO: Use user.LoyaltyAssociationCode if the query depends on it.
        // TODO: Order by CreatedAt DESC, then Id, so offset cursors stay stable between requests.
        // TODO: Run a COUNT for TotalCount (or COUNT(*) OVER() in the same query).
        // TODO: Work out the page window from input.Pagination and _paginationOptions. Paginator.Slice needs the whole
        // list in memory, so extract the window and page building into reusable methods (validation, cursor decoding,
        // PageInfo) before using them here. Invalid arguments must throw InvalidPaginationException.
        // TODO: Fetch only that window (OFFSET/FETCH), then map those rows to Receipt.
        // TODO: Return Page<Receipt> with the items, PageInfo, TotalCount and Offset. Use Page.Empty<Receipt>() when
        // nothing matches.
        throw new NotImplementedException();
    }
}
