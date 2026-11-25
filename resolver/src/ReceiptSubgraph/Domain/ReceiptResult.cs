using HotChocolate;
using HotChocolate.Types;
using ReceiptSubgraph.Paging;

namespace ReceiptSubgraph.Domain;

/// <summary>
/// The filter to apply when selecting receipts. When multiple properties are set, selection is performed
/// against the intersection of the inputs.
/// </summary>
/// <remarks>
/// Equality is value based (including the id list).
/// </remarks>
[GraphQLName("TransactionResultFilterInput")]
public sealed record ReceiptFilter : IEquatable<ReceiptFilter>
{
    /// <summary>The ids of the orders that returned receipts must have.</summary>
    [ID]
    public IReadOnlyList<string>? OrderIds { get; init; }

    /// <summary>The id of the store that returned receipts must have.</summary>
    [ID]
    public string? StoreId { get; init; }

    /// <summary>The brand of the store that returned receipts must have.</summary>
    public StoreBrand? StoreBrand { get; init; }

    /// <summary>The sales channel of the store that returned receipts must have.</summary>
    public TransactionSalesChannel? SalesChannel { get; init; }

    /// <summary>The earliest date (inclusive) that returned receipts must have.</summary>
    public DateTimeOffset? DateFrom { get; init; }

    /// <summary>The latest date (inclusive) that returned receipts must have.</summary>
    public DateTimeOffset? DateTo { get; init; }

    public bool Equals(ReceiptFilter? other)
        => other is not null
            && StoreId == other.StoreId
            && StoreBrand == other.StoreBrand
            && SalesChannel == other.SalesChannel
            && DateFrom == other.DateFrom
            && DateTo == other.DateTo
            && SequenceEqual(OrderIds, other.OrderIds);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(StoreId);
        hash.Add(StoreBrand);
        hash.Add(SalesChannel);
        hash.Add(DateFrom);
        hash.Add(DateTo);

        if (OrderIds is not null)
        {
            foreach (var orderId in OrderIds)
            {
                hash.Add(orderId);
            }
        }

        return hash.ToHashCode();
    }

    private static bool SequenceEqual(IReadOnlyList<string>? left, IReadOnlyList<string>? right)
        => ReferenceEquals(left, right)
            || (left is not null && right is not null && left.SequenceEqual(right));
}

/// <summary>The input for the transaction query.</summary>
[GraphQLName("TransactionInput")]
public sealed record ReceiptByIdInput
{
    /// <summary>The transaction ID to find.</summary>
    [ID]
    public required string Id { get; init; }

    /// <summary>Encoded value of the loyalty association number (e.g. flybuys).</summary>
    public string? LoyaltyAssociationCode { get; init; }
}

/// <summary>The input for the transactions query.</summary>
/// <param name="Filter">The filter to apply when selecting receipts.</param>
/// <param name="Pagination">The page of receipts to return.</param>
/// <param name="LoyaltyAssociationCode">Encoded value of the loyalty association number (e.g. flybuys).</param>
[GraphQLName("TransactionsInput")]
public sealed record ReceiptResultInput(
    ReceiptFilter? Filter = null,
    PageRequest? Pagination = null,
    string? LoyaltyAssociationCode = null);

/// <summary>The receipts in the requested page, with the details about the page.</summary>
/// <param name="Edges">The receipts in the requested page, each with its own cursor. Ordered by creation date, newest first.</param>
/// <param name="PageInfo">The details about the page of receipts returned.</param>
/// <param name="TotalCount">The total number of receipts matching the filter, across all pages.</param>
[GraphQLName("TransactionConnection")]
public sealed record ReceiptConnection(IReadOnlyList<ReceiptEdge> Edges, PageInfo PageInfo, int TotalCount) : IReceiptsResponse
{
    public static ReceiptConnection From(Page<Receipt> page)
        => new(page.ToEdges(static (cursor, receipt) => new ReceiptEdge(cursor, receipt)), page.PageInfo, page.TotalCount);
}

/// <summary>A receipt together with the cursor that identifies its position in the connection.</summary>
/// <param name="Cursor">The cursor of this receipt, for use as <c>after</c> or <c>before</c>.</param>
/// <param name="Node">The receipt.</param>
[GraphQLName("TransactionEdge")]
public sealed record ReceiptEdge(string Cursor, Receipt Node);
