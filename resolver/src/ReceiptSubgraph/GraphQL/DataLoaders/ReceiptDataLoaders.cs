using ReceiptSubgraph.Data;
using ReceiptSubgraph.Domain;

namespace ReceiptSubgraph.GraphQL.DataLoaders;

/// <summary>
/// Source generated DataLoaders. The generator emits <c>IReceiptByIdDataLoader</c> (plus its implementation) at build time.
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
}
