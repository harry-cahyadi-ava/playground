using HotChocolate;
using HotChocolate.Types;
using ReceiptSubgraph.Paging;

namespace ReceiptSubgraph.Domain;

/// <summary>The purchased item contained within the receipt.</summary>
[GraphQLName("TransactionItem")]
public sealed record ReceiptItem
{
    /// <summary>The properties of the receipt item captured at the time of the receipt.</summary>
    public required IReceiptItemDetails Details { get; init; }

    /// <summary>
    /// The promotion applied to the item.
    /// This field will be null if no promotion was applied to the item.
    /// </summary>
    public ReceiptItemPromotion? Promotion { get; init; }

    /// <summary>The substitutions applied to the item.</summary>
    public IReadOnlyList<ReceiptSubstitutedItem> Substitutions { get; init; } = [];
}

/// <summary>Either a purchased product or a carrier bag line-item.</summary>
[UnionType("TransactionItemDetails")]
public interface IReceiptItemDetails;

/// <summary>
/// The details of the purchased product.
/// Static properties (name, prices) reflect what the customer saw at the time of purchase, while the
/// dynamic <c>product</c> reference reflects the current state of the product.
/// </summary>
[GraphQLName("TransactionItemProduct")]
public sealed record ReceiptItemProduct : IReceiptItemDetails
{
    /// <summary>Id of the product in the Product subgraph; exposed through the <c>product</c> reference field.</summary>
    [GraphQLIgnore]
    public string? ProductId { get; init; }

    /// <summary>The (static) name of the product, as shown to the customer at the time of purchase.</summary>
    public required string Name { get; init; }

    /// <summary>The (static) quantity of the product in the receipt.</summary>
    public required double Quantity { get; init; }

    /// <summary>The exact quantity of the product ordered by the customer.</summary>
    public double? QuantityOrdered { get; init; }

    /// <summary>Indicates how a product's quantity is measured.</summary>
    public QuantityType? QuantityType { get; init; }

    /// <summary>The price per single unit of the product.</summary>
    public required decimal UnitPrice { get; init; }

    /// <summary>The standard unit used to measure the item.</summary>
    public ProductUnitOfMeasure? UnitOfMeasure { get; init; }

    /// <summary>The total price charged for the product.</summary>
    public required decimal TotalPrice { get; init; }

    /// <summary>Indicates whether the product is a return.</summary>
    [GraphQLName("return")]
    public bool IsReturn { get; init; }

    /// <summary>Indicates whether the product has an applicable tax rate.</summary>
    public bool Taxable { get; init; }

    /// <summary>The comments captured for this product.</summary>
    public string? Comments { get; init; }

    /// <summary>The compliance text required for tobacco products. Null if it is not a tobacco product.</summary>
    public string? TobaccoText { get; init; }
}

/// <summary>The object representing a bag as a line-item in the receipt.</summary>
[GraphQLName("TransactionItemBag")]
public sealed record ReceiptItemBag : IReceiptItemDetails
{
    /// <summary>The number of bags charged.</summary>
    public required double Quantity { get; init; }

    /// <summary>The price per single bag.</summary>
    public required decimal UnitPrice { get; init; }

    /// <summary>The total price charged for the bags.</summary>
    public required decimal TotalPrice { get; init; }
}

/// <summary>The properties of the promotion attached to a receipt item.</summary>
[GraphQLName("TransactionItemPromotion")]
public sealed record ReceiptItemPromotion
{
    public ReceiptItemPromotionType? Type { get; init; }

    public required decimal OriginalUnitPrice { get; init; }
}

/// <summary>The item that was substituted in place of the item in the receipt.</summary>
[GraphQLName("TransactionSubstitutedItem")]
public sealed record ReceiptSubstitutedItem
{
    /// <summary>The product that was substituted.</summary>
    public ReceiptItemProduct? Product { get; init; }
}

/// <summary>The connection used to fetch receipt items.</summary>
/// <param name="Edges">The receipt items in the requested page, each with its own cursor, in the order they appear on the receipt.</param>
/// <param name="PageInfo">The details about the page of receipt items returned.</param>
/// <param name="TotalCount">The total number of items on the receipt, across all pages.</param>
[GraphQLName("TransactionItemConnection")]
public sealed record ReceiptItemConnection(IReadOnlyList<ReceiptItemEdge> Edges, PageInfo PageInfo, int TotalCount)
{
    public static ReceiptItemConnection From(Page<ReceiptItem> page)
        => new(page.ToEdges(static (cursor, item) => new ReceiptItemEdge(cursor, item)), page.PageInfo, page.TotalCount);
}

/// <summary>A receipt item together with the cursor that identifies its position in the connection.</summary>
/// <param name="Cursor">The cursor of this item, for use as <c>after</c> or <c>before</c>.</param>
/// <param name="Node">The receipt item.</param>
[GraphQLName("TransactionItemEdge")]
public sealed record ReceiptItemEdge(string Cursor, ReceiptItem Node);
