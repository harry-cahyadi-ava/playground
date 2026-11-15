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
    private readonly FrozenDictionary<string, Receipt[]> _byOrderId;

    public InMemoryReceiptRepository(IEnumerable<Receipt> receipts, PaginationOptions paginationOptions)
    {
        _paginationOptions = paginationOptions;

        // Newest first, with the id as tie-breaker so offset cursors stay stable between requests.
        var ordered = receipts
            .OrderByDescending(r => r.CreatedAt)
            .ThenBy(r => r.Id, StringComparer.Ordinal)
            .ToArray();

        _byId = ordered.ToFrozenDictionary(r => r.Id);
        _byCustomerId = GroupBy(ordered, r => r.CustomerId);
        _byOrderId = GroupBy(ordered, r => r.OrderId);
    }

    public ValueTask<IReadOnlyDictionary<string, Receipt>> GetByIdsAsync(
        IReadOnlyList<string> receiptIds,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, Receipt>(receiptIds.Count);

        foreach (var id in receiptIds)
        {
            if (_byId.TryGetValue(id, out var receipt))
            {
                result[id] = receipt;
            }
        }

        return ValueTask.FromResult<IReadOnlyDictionary<string, Receipt>>(result);
    }

    public ValueTask<IReadOnlyDictionary<string, Page<Receipt>>> GetByOwnersAsync(
        ReceiptOwnerKind ownerKind,
        IReadOnlyList<string> ownerIds,
        ReceiptResultInput? input,
        CancellationToken cancellationToken)
    {
        var index = ownerKind == ReceiptOwnerKind.Customer ? _byCustomerId : _byOrderId;
        var filter = input?.Filter;

        // Build the order id lookup once per call instead of once per owner or per receipt.
        var orderIds = filter?.OrderIds is { Count: > 0 } ids ? ids.ToHashSet(StringComparer.Ordinal) : null;
        var emptyOrderFilter = filter?.OrderIds is { Count: 0 };

        var result = new Dictionary<string, Page<Receipt>>(ownerIds.Count);

        foreach (var ownerId in ownerIds)
        {
            if (!index.TryGetValue(ownerId, out var candidates) || emptyOrderFilter)
            {
                result[ownerId] = Page<Receipt>.Empty;
                continue;
            }

            // Skip the copy entirely when there is nothing to filter on.
            var matching = filter is null ? candidates : Filter(candidates, filter, orderIds);
            result[ownerId] = Paginator.Slice(matching, input?.Pagination, _paginationOptions);
        }

        return ValueTask.FromResult<IReadOnlyDictionary<string, Page<Receipt>>>(result);
    }

    private static Receipt[] Filter(Receipt[] candidates, ReceiptFilter filter, HashSet<string>? orderIds)
    {
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

    private static FrozenDictionary<string, Receipt[]> GroupBy(Receipt[] ordered, Func<Receipt, string?> key)
        => ordered
            .Where(r => key(r) is not null)
            .GroupBy(r => key(r)!)
            .ToFrozenDictionary(g => g.Key, g => g.ToArray());
}
