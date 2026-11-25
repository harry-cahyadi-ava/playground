using ReceiptSubgraph.Domain;
using ReceiptSubgraph.Paging;

namespace ReceiptSubgraph.Data;

/// <summary>
/// Data access contract for receipts (e.g. implemented by a client for the Digital Receipt Service).
/// </summary>
public interface IReceiptRepository
{
    /// <summary>
    /// Loads the user's receipts by id. Ids without a receipt, or whose receipt belongs to another customer
    /// (or to no customer), are absent from the returned dictionary.
    /// </summary>
    ValueTask<IReadOnlyDictionary<string, Receipt>> GetByIdsAsync(
        ReceiptUserContext user,
        IReadOnlyList<string> receiptIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Loads one page of the user's receipts, newest first, narrowed by the optional filter and paging in
    /// <paramref name="input"/>. A customer without matching receipts gets an empty page.
    /// </summary>
    ValueTask<Page<Receipt>> GetByCustomerAsync(
        ReceiptUserContext user,
        ReceiptResultInput? input,
        CancellationToken cancellationToken);
}
