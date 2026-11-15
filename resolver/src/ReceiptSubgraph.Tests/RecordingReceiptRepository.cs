using ReceiptSubgraph.Data;
using ReceiptSubgraph.Domain;
using ReceiptSubgraph.Paging;

namespace ReceiptSubgraph.Tests;

/// <summary>
/// Wraps a real repository and records how often it is called, so tests can assert that DataLoaders
/// really collapse many lookups into few round trips.
/// </summary>
internal sealed class RecordingReceiptRepository(IReceiptRepository inner) : IReceiptRepository
{
    private readonly List<IReadOnlyList<string>> _idLookups = [];
    private readonly List<(ReceiptOwnerKind Kind, string[] OwnerIds, ReceiptResultInput? Input)> _ownerLookups = [];

    public IReadOnlyList<IReadOnlyList<string>> IdLookups => _idLookups;

    public IReadOnlyList<(ReceiptOwnerKind Kind, string[] OwnerIds, ReceiptResultInput? Input)> OwnerLookups => _ownerLookups;

    public ValueTask<IReadOnlyDictionary<string, Receipt>> GetByIdsAsync(
        IReadOnlyList<string> receiptIds,
        CancellationToken cancellationToken)
    {
        // The DataLoader reuses its key list, so keep a copy.
        lock (_idLookups)
        {
            _idLookups.Add(receiptIds.ToArray());
        }

        return inner.GetByIdsAsync(receiptIds, cancellationToken);
    }

    public ValueTask<IReadOnlyDictionary<string, Page<Receipt>>> GetByOwnersAsync(
        ReceiptOwnerKind ownerKind,
        IReadOnlyList<string> ownerIds,
        ReceiptResultInput? input,
        CancellationToken cancellationToken)
    {
        lock (_ownerLookups)
        {
            _ownerLookups.Add((ownerKind, ownerIds.ToArray(), input));
        }

        return inner.GetByOwnersAsync(ownerKind, ownerIds, input, cancellationToken);
    }
}
