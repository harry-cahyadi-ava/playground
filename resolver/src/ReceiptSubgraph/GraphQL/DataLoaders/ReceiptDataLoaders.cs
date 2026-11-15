using ReceiptSubgraph.Data;
using ReceiptSubgraph.Domain;

namespace ReceiptSubgraph.GraphQL.DataLoaders;

/// <summary>Identifies one receipt listing request: which owner, and with which filter and paging.</summary>
/// <remarks>
/// The input is part of the key so two aliases of the same field with different arguments never share a cached result.
/// </remarks>
public readonly record struct ReceiptResultKey(ReceiptOwnerKind OwnerKind, string OwnerId, ReceiptResultInput? Input);

/// <summary>
/// Source generated DataLoaders. The generator emits <c>IReceiptByIdDataLoader</c> and
/// <c>IReceiptResultByOwnerDataLoader</c> (plus their implementations) at build time.
/// </summary>
public static class ReceiptDataLoaders
{
    /// <summary>Batches all receipt lookups made during one request into a single repository call.</summary>
    [DataLoader]
    public static async Task<IReadOnlyDictionary<string, Receipt>> GetReceiptByIdAsync(
        IReadOnlyList<string> receiptIds,
        IReceiptRepository repository,
        CancellationToken cancellationToken)
        => await repository.GetByIdsAsync(receiptIds, cancellationToken);

    /// <summary>
    /// Batches <c>receipts</c> lookups for many customers/orders. Keys that share the same owner kind and
    /// input (the usual case when a list of owners is selected) are sent to the repository together.
    /// </summary>
    [DataLoader]
    public static async Task<IReadOnlyDictionary<ReceiptResultKey, ReceiptConnection>> GetReceiptResultByOwnerAsync(
        IReadOnlyList<ReceiptResultKey> keys,
        IReceiptRepository repository,
        CancellationToken cancellationToken)
    {
        // The key list is pooled by the DataLoader, so it is only read here and never stored.
        var groups = new Dictionary<(ReceiptOwnerKind Kind, ReceiptResultInput? Input), List<string>>();

        foreach (var key in keys)
        {
            var groupKey = (key.OwnerKind, key.Input);

            if (!groups.TryGetValue(groupKey, out var ownerIds))
            {
                ownerIds = new List<string>(keys.Count);
                groups[groupKey] = ownerIds;
            }

            ownerIds.Add(key.OwnerId);
        }

        // Distinct groups (different filters or paging) are independent, so query them concurrently.
        var loaded = await Task.WhenAll(
            groups.Select(group => LoadGroupAsync(repository, group.Key.Kind, group.Key.Input, group.Value, cancellationToken)));

        var results = new Dictionary<ReceiptResultKey, ReceiptConnection>(keys.Count);

        foreach (var entries in loaded)
        {
            foreach (var (key, result) in entries)
            {
                results[key] = result;
            }
        }

        return results;
    }

    private static async Task<(ReceiptResultKey Key, ReceiptConnection Result)[]> LoadGroupAsync(
        IReceiptRepository repository,
        ReceiptOwnerKind kind,
        ReceiptResultInput? input,
        List<string> ownerIds,
        CancellationToken cancellationToken)
    {
        var pages = await repository.GetByOwnersAsync(kind, ownerIds, input, cancellationToken);
        var entries = new (ReceiptResultKey, ReceiptConnection)[ownerIds.Count];

        for (var i = 0; i < ownerIds.Count; i++)
        {
            var ownerId = ownerIds[i];

            // Owners the repository knows nothing about still get a valid, empty result.
            var page = pages.TryGetValue(ownerId, out var found) ? found : Paging.Page<Receipt>.Empty;

            entries[i] = (new ReceiptResultKey(kind, ownerId, input), ReceiptConnection.From(page));
        }

        return entries;
    }
}
