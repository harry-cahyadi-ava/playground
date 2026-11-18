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
    private readonly List<(string CustomerId, string[] ReceiptIds)> _idLookups = [];
    private readonly List<(string CustomerId, ReceiptResultInput? Input)> _customerLookups = [];
    private Exception? _failure;

    public IReadOnlyList<(string CustomerId, string[] ReceiptIds)> IdLookups => _idLookups;

    public IReadOnlyList<(string CustomerId, ReceiptResultInput? Input)> CustomerLookups => _customerLookups;

    /// <summary>Makes every following lookup throw, to simulate the backing store being unavailable.</summary>
    public void FailWith(Exception failure) => _failure = failure;

    public ValueTask<IReadOnlyDictionary<string, Receipt>> GetByIdsAsync(
        string customerId,
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
            _idLookups.Add((customerId, receiptIds.ToArray()));
        }

        return inner.GetByIdsAsync(customerId, receiptIds, cancellationToken);
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
