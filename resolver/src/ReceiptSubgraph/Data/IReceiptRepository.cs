using ReceiptSubgraph.Domain;
using ReceiptSubgraph.Paging;

namespace ReceiptSubgraph.Data;

/// <summary>The kind of entity that receipts can be listed for.</summary>
public enum ReceiptOwnerKind
{
    Customer,
    Order,
}

/// <summary>
/// Data access contract for receipts. Every method is batch oriented so that a request which touches many
/// customers, orders or receipts costs a fixed number of round trips to the backing store.
/// Implementations replace the in-memory sample used for local development (e.g. a client for the Digital Receipt Service).
/// </summary>
public interface IReceiptRepository
{
    /// <summary>Loads receipts by id. Ids without a receipt are absent from the returned dictionary.</summary>
    ValueTask<IReadOnlyDictionary<string, Receipt>> GetByIdsAsync(
        IReadOnlyList<string> receiptIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Loads one page of receipts, newest first, for each of the given owners (customers or orders).
    /// The same filter and paging are applied to every owner. Owners without receipts get an empty page.
    /// </summary>
    ValueTask<IReadOnlyDictionary<string, Page<Receipt>>> GetByOwnersAsync(
        ReceiptOwnerKind ownerKind,
        IReadOnlyList<string> ownerIds,
        ReceiptResultInput? input,
        CancellationToken cancellationToken);
}
