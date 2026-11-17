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
    private readonly List<IReadOnlyList<string>> _idLookups = [];
    private readonly List<(string CustomerId, ReceiptResultInput? Input)> _customerLookups = [];
    private Exception? _failure;

    public IReadOnlyList<IReadOnlyList<string>> IdLookups => _idLookups;

    public IReadOnlyList<(string CustomerId, ReceiptResultInput? Input)> CustomerLookups => _customerLookups;

    /// <summary>Makes every following lookup throw, to simulate the backing store being unavailable.</summary>
    public void FailWith(Exception failure) => _failure = failure;

    public ValueTask<IReadOnlyDictionary<string, Receipt>> GetByIdsAsync(
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
            _idLookups.Add(receiptIds.ToArray());
        }

        return inner.GetByIdsAsync(receiptIds, cancellationToken);
    }

    public ValueTask<Page<Receipt>> GetByCustomerAsync(
        string customerId,
        ReceiptResultInput? input,
        CancellationToken cancellationToken)
    {
        if (_failure is not null)
        {
            throw _failure;
        }

        lock (_customerLookups)
        {
            _customerLookups.Add((customerId, input));
        }

        return inner.GetByCustomerAsync(customerId, input, cancellationToken);
    }
}
