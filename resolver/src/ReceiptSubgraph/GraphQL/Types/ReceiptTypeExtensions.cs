using HotChocolate;
using HotChocolate.Types;
using ReceiptSubgraph.Domain;
using ReceiptSubgraph.Paging;

namespace ReceiptSubgraph.GraphQL.Types;

/// <summary>Resolvers for the Receipt fields that are not plain properties of the model.</summary>
[ObjectType<Receipt>]
public static partial class ReceiptNode
{
    // The following are pure resolvers (synchronous, no services): Hot Chocolate runs them inline
    // without scheduling any async work.

    /// <summary>The store associated with this receipt (a reference the Store subgraph completes).</summary>
    public static Store GetStore([Parent] Receipt receipt) => new(receipt.StoreId);

    /// <summary>The customer associated with the receipt. Null for anonymous purchases.</summary>
    public static Customer? GetCustomer([Parent] Receipt receipt)
        => receipt.CustomerId is null ? null : new Customer(receipt.CustomerId);

    /// <summary>The online order associated with this receipt. Null if it is not for an online order.</summary>
    public static Order? GetOrder([Parent] Receipt receipt)
        => receipt.OrderId is null ? null : new Order(receipt.OrderId);

    /// <summary>The items within the receipt, paged with cursors, in the order they appear on the receipt.</summary>
    public static ReceiptItemConnection GetItems(
        [Parent] Receipt receipt,
        PageRequest? pagination,
        [Service] PaginationOptions options)
        => ReceiptItemConnection.From(Paginator.Slice(receipt.Items, pagination, options));
}

/// <summary>Resolves the dynamic product reference on a purchased product line.</summary>
[ObjectType<ReceiptItemProduct>]
public static partial class ReceiptItemProductNode
{
    /// <summary>The (dynamic) product details, completed by the Product subgraph.</summary>
    public static Product? GetProduct([Parent] ReceiptItemProduct item)
        => item.ProductId is null ? null : new Product(item.ProductId);
}
