using ReceiptSubgraph.Domain;
using ReceiptSubgraph.Paging;

namespace ReceiptSubgraph.Data;

/// <summary>
/// Data access contract for receipts. Implementations replace the in-memory sample used for local development
/// (e.g. a client for the Digital Receipt Service).
/// </summary>
public interface IReceiptRepository
{
    /// <summary>Loads receipts by id. Ids without a receipt are absent from the returned dictionary.</summary>
    ValueTask<IReadOnlyDictionary<string, Receipt>> GetByIdsAsync(
        IReadOnlyList<string> receiptIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Loads one page of the customer's receipts, newest first, narrowed by the optional filter and paging in
    /// <paramref name="input"/>. A customer without matching receipts gets an empty page.
    /// </summary>
    ValueTask<Page<Receipt>> GetByCustomerAsync(
        string customerId,
        ReceiptResultInput? input,
        CancellationToken cancellationToken);
}
