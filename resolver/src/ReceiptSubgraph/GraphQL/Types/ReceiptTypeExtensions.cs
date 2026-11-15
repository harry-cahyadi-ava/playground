using HotChocolate;
using HotChocolate.Types;
using ReceiptSubgraph.Data;
using ReceiptSubgraph.Domain;
using ReceiptSubgraph.GraphQL.DataLoaders;
using ReceiptSubgraph.Paging;

namespace ReceiptSubgraph.GraphQL.Types;

/// <summary>Adds <c>receipts</c> to the Customer entity owned by the Customer subgraph.</summary>
[ObjectType<Customer>]
public static partial class CustomerNode
{
    /// <summary>
    /// Receipts for the customer, newest first. Lookups for many customers are batched by the DataLoader.
    /// </summary>
    public static async Task<ReceiptConnection?> GetReceiptsAsync(
        [Parent] Customer customer,
        ReceiptResultInput? input,
        IReceiptResultByOwnerDataLoader receiptResults,
        CancellationToken cancellationToken)
        => await receiptResults.LoadAsync(
            new ReceiptResultKey(ReceiptOwnerKind.Customer, customer.Id, input),
            cancellationToken);
}

/// <summary>Adds <c>receipts</c> to the Order entity owned by the Order subgraph.</summary>
[ObjectType<Order>]
public static partial class OrderNode
{
    /// <summary>Receipts for the order, newest first. Lookups for many orders are batched by the DataLoader.</summary>
    public static async Task<ReceiptConnection?> GetReceiptsAsync(
        [Parent] Order order,
        ReceiptResultInput? input,
        IReceiptResultByOwnerDataLoader receiptResults,
        CancellationToken cancellationToken)
        => await receiptResults.LoadAsync(
            new ReceiptResultKey(ReceiptOwnerKind.Order, order.Id, input),
            cancellationToken);
}

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
