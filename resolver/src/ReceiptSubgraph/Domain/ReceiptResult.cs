using HotChocolate;
using HotChocolate.Types;
using ReceiptSubgraph.Paging;

namespace ReceiptSubgraph.Domain;

/// <summary>
/// The filter to apply when selecting receipts. When multiple properties are set, selection is performed
/// against the intersection of the inputs.
/// </summary>
/// <remarks>
/// Instances are used as part of a DataLoader key, so equality is value based (including the id list).
/// </remarks>
[GraphQLName("ReceiptResultFilterInput")]
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
    public StoreSalesChannel? StoreSalesChannel { get; init; }

    /// <summary>The earliest date (inclusive) that returned receipts must have.</summary>
    public DateTimeOffset? DateFrom { get; init; }

    /// <summary>The latest date (inclusive) that returned receipts must have.</summary>
    public DateTimeOffset? DateTo { get; init; }

    public bool Equals(ReceiptFilter? other)
        => other is not null
            && StoreId == other.StoreId
            && StoreBrand == other.StoreBrand
            && StoreSalesChannel == other.StoreSalesChannel
            && DateFrom == other.DateFrom
            && DateTo == other.DateTo
            && SequenceEqual(OrderIds, other.OrderIds);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(StoreId);
        hash.Add(StoreBrand);
        hash.Add(StoreSalesChannel);
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

/// <summary>The input for the receipts query.</summary>
[GraphQLName("ReceiptsInput")]
public sealed record ReceiptResultInput(ReceiptFilter? Filter = null, PageRequest? Pagination = null);

/// <summary>The receipts in the requested page, with the details about the page.</summary>
/// <param name="Edges">The receipts in the requested page, each with its own cursor. Ordered by creation date, newest first.</param>
/// <param name="PageInfo">The details about the page of receipts returned.</param>
/// <param name="TotalCount">The total number of receipts matching the filter, across all pages.</param>
public sealed record ReceiptConnection(IReadOnlyList<ReceiptEdge> Edges, PageInfo PageInfo, int TotalCount) : IReceiptsResponse
{
    public static ReceiptConnection From(Page<Receipt> page)
        => new(page.ToEdges(static (cursor, receipt) => new ReceiptEdge(cursor, receipt)), page.PageInfo, page.TotalCount);
}

/// <summary>A receipt together with the cursor that identifies its position in the connection.</summary>
/// <param name="Cursor">The cursor of this receipt, for use as <c>after</c> or <c>before</c>.</param>
/// <param name="Node">The receipt.</param>
public sealed record ReceiptEdge(string Cursor, Receipt Node);
