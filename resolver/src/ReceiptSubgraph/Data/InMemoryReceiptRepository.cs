using System.Collections.Frozen;
using ReceiptSubgraph.Domain;
using ReceiptSubgraph.Paging;

namespace ReceiptSubgraph.Data;

/// <summary>
/// Read-only, in-memory <see cref="IReceiptRepository"/> used for local development and tests.
/// Receipts are indexed once at construction, so every lookup afterwards is a dictionary hit and the
/// instance can be shared as a singleton across concurrent requests without locking.
/// </summary>
public sealed class InMemoryReceiptRepository : IReceiptRepository
{
    private readonly PaginationOptions _paginationOptions;
    private readonly FrozenDictionary<string, Receipt> _byId;
    private readonly FrozenDictionary<string, Receipt[]> _byCustomerId;

    public InMemoryReceiptRepository(IEnumerable<Receipt> receipts, PaginationOptions paginationOptions)
    {
        _paginationOptions = paginationOptions;

        // Newest first, with the id as tie-breaker so offset cursors stay stable between requests.
        var ordered = receipts
            .OrderByDescending(r => r.CreatedAt)
            .ThenBy(r => r.Id, StringComparer.Ordinal)
            .ToArray();

        _byId = ordered.ToFrozenDictionary(r => r.Id);

        // Receipts without a customer (anonymous in-store purchases) are not listed for anyone.
        _byCustomerId = ordered
            .Where(r => r.CustomerId is not null)
            .GroupBy(r => r.CustomerId!)
            .ToFrozenDictionary(g => g.Key, g => g.ToArray());
    }

    public ValueTask<IReadOnlyDictionary<string, Receipt>> GetByIdsAsync(
        ReceiptUserContext user,
        IReadOnlyList<string> receiptIds,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, Receipt>(receiptIds.Count);

        // A database-backed repository should fetch all ids in one query (WHERE CustomerId = ... AND Id IN ...)
        // and map only the rows that come back; ids without a row stay out of the dictionary.
        foreach (var id in receiptIds)
        {
            if (_byId.TryGetValue(id, out var receipt) && receipt.CustomerId == user.CustomerId)
            {
                result[id] = receipt;
            }
        }

        return ValueTask.FromResult<IReadOnlyDictionary<string, Receipt>>(result);
    }

    public ValueTask<Page<Receipt>> GetByCustomerAsync(
        ReceiptUserContext user,
        ReceiptResultInput? input,
        CancellationToken cancellationToken)
    {
        var filter = input?.Filter;

        // An empty order id list means "no order matches", which is different from not filtering by order.
        if (!_byCustomerId.TryGetValue(user.CustomerId, out var candidates) || filter?.OrderIds is { Count: 0 })
        {
            return ValueTask.FromResult(Page.Empty<Receipt>());
        }

        // Skip the copy entirely when there is nothing to filter on.
        var matching = filter is null ? candidates : Filter(candidates, filter);

        // A database-backed repository should filter, order and page in the query, and only then map the fetched
        // rows to Receipt, so the mapping cost is bounded by the page size instead of the customer's full history.
        return ValueTask.FromResult(Paginator.Slice(matching, input?.Pagination, _paginationOptions));
    }

    private static Receipt[] Filter(Receipt[] candidates, ReceiptFilter filter)
    {
        var orderIds = filter.OrderIds is { Count: > 0 } ids ? ids.ToHashSet(StringComparer.Ordinal) : null;
        var matching = new List<Receipt>(candidates.Length);

        foreach (var receipt in candidates)
        {
            if (Matches(receipt, filter, orderIds))
            {
                matching.Add(receipt);
            }
        }

        return [.. matching];
    }

    // Every property that is set must match (intersection); unset properties are ignored.
    private static bool Matches(Receipt receipt, ReceiptFilter filter, HashSet<string>? orderIds)
        => (orderIds is null || (receipt.OrderId is not null && orderIds.Contains(receipt.OrderId)))
            && (filter.StoreId is null || receipt.StoreId == filter.StoreId)
            && (filter.StoreBrand is null || receipt.StoreBrand == filter.StoreBrand)
            && (filter.StoreSalesChannel is null || receipt.StoreSalesChannel == filter.StoreSalesChannel)
            && (filter.DateFrom is null || receipt.CreatedAt >= filter.DateFrom)
            && (filter.DateTo is null || receipt.CreatedAt <= filter.DateTo);
}
