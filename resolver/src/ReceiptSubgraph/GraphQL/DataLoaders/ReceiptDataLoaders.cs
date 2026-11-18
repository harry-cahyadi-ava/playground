using ReceiptSubgraph.Data;
using ReceiptSubgraph.Domain;

namespace ReceiptSubgraph.GraphQL.DataLoaders;

/// <summary>Identifies one receipt lookup: the receipt, and the user it must be returned to.</summary>
public readonly record struct ReceiptByIdKey(ReceiptUserContext User, string ReceiptId);

/// <summary>
/// Source generated DataLoaders. The generator emits <c>IReceiptByIdDataLoader</c> (plus its implementation) at build time.
/// </summary>
public static class ReceiptDataLoaders
{
    /// <summary>
    /// Batches all receipt lookups made during one request. The user is part of the key, so a receipt is only
    /// ever returned to the customer that owns it.
    /// </summary>
    [DataLoader]
    public static async Task<IReadOnlyDictionary<ReceiptByIdKey, Receipt>> GetReceiptByIdAsync(
        IReadOnlyList<ReceiptByIdKey> keys,
        IReceiptRepository repository,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<ReceiptByIdKey, Receipt>(keys.Count);

        // A request carries a single user, so this is normally one repository call.
        foreach (var userKeys in keys.GroupBy(key => key.User))
        {
            var receiptIds = userKeys.Select(key => key.ReceiptId).ToArray();
            var receipts = await repository.GetByIdsAsync(userKeys.Key, receiptIds, cancellationToken);

            foreach (var (receiptId, receipt) in receipts)
            {
                results[new ReceiptByIdKey(userKeys.Key, receiptId)] = receipt;
            }
        }

        return results;
    }
}
