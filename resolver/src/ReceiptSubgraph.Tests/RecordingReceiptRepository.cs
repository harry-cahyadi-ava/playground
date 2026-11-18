using ReceiptSubgraph.Data;
using ReceiptSubgraph.Domain;
using ReceiptSubgraph.Paging;

namespace ReceiptSubgraph.Tests;

/// <summary>
/// Wraps a real repository and records how often it is called, so tests can assert which customer was
/// queried and that DataLoaders really collapse many lookups into few round trips.
/// </summary>
internal sealed class RecordingReceiptRepository(IReceiptRepository inner) : IReceiptRepository
{
    private readonly List<(ReceiptUserContext User, string[] ReceiptIds)> _idLookups = [];
    private readonly List<(ReceiptUserContext User, ReceiptResultInput? Input)> _customerLookups = [];
    private Exception? _failure;

    public IReadOnlyList<(ReceiptUserContext User, string[] ReceiptIds)> IdLookups => _idLookups;

    public IReadOnlyList<(ReceiptUserContext User, ReceiptResultInput? Input)> CustomerLookups => _customerLookups;

    /// <summary>Makes every following lookup throw, to simulate the backing store being unavailable.</summary>
    public void FailWith(Exception failure) => _failure = failure;

    public ValueTask<IReadOnlyDictionary<string, Receipt>> GetByIdsAsync(
        ReceiptUserContext user,
        IReadOnlyList<string> receiptIds,
        CancellationToken cancellationToken)
    {
        if (_failure is not null)
        {
            throw _failure;
        }

        // The DataLoader reuses its key list, so keep a copy.
        lock (_idLookups)
        {
            _idLookups.Add((user, receiptIds.ToArray()));
        }

        return inner.GetByIdsAsync(user, receiptIds, cancellationToken);
    }

    public ValueTask<Page<Receipt>> GetByCustomerAsync(
        ReceiptUserContext user,
        ReceiptResultInput? input,
        CancellationToken cancellationToken)
    {
        if (_failure is not null)
        {
            throw _failure;
        }

        lock (_customerLookups)
        {
            _customerLookups.Add((user, input));
        }

        return inner.GetByCustomerAsync(user, input, cancellationToken);
    }
}
