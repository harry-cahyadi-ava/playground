using HotChocolate;
using HotChocolate.ApolloFederation.Resolvers;
using HotChocolate.ApolloFederation.Types;
using HotChocolate.Types;
using ReceiptSubgraph.GraphQL.DataLoaders;

namespace ReceiptSubgraph.Domain;

/// <summary>
/// A receipt for an in-store or online purchase.
/// Storage details (ids of other entities, the item list) are hidden from the schema and exposed through
/// resolvers in <c>ReceiptNode</c> instead.
/// </summary>
[Key("id")]
public sealed record Receipt
{
    /// <summary>The unique id of the receipt (called "transaction id" within the Digital Receipt Service).</summary>
    [ID]
    public required string Id { get; init; }

    [GraphQLIgnore]
    public required string StoreId { get; init; }

    [GraphQLIgnore]
    public string? CustomerId { get; init; }

    [GraphQLIgnore]
    public string? OrderId { get; init; }

    /// <summary>Brand of the store; only used to filter receipts.</summary>
    [GraphQLIgnore]
    public StoreBrand StoreBrand { get; init; }

    /// <summary>Sales channel of the store; only used to filter receipts.</summary>
    [GraphQLIgnore]
    public StoreSalesChannel StoreSalesChannel { get; init; }

    [GraphQLIgnore]
    public IReadOnlyList<ReceiptItem> Items { get; init; } = [];

    /// <summary>The date when the receipt was processed.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>The receipt amount (broken down into its various components).</summary>
    public required ReceiptAmount Amount { get; init; }

    /// <summary>The amount of the total that was paid for through the application of existing credit.</summary>
    public decimal CreditApplied { get; init; }

    /// <summary>The payments associated with the receipt.</summary>
    public IReadOnlyList<ReceiptPayment> Payments { get; init; } = [];

    /// <summary>The tenders associated with the receipt for in-store purchases.</summary>
    public IReadOnlyList<ReceiptTender> Tenders { get; init; } = [];

    /// <summary>The invoice type for Coles online orders. Null if it is not a Coles online order.</summary>
    public ReceiptInvoiceType? InvoiceType { get; init; }

    /// <summary>The details captured for tax invoice purposes. Null if no such details were supplied.</summary>
    public ReceiptInvoiceDetails? InvoiceDetails { get; init; }

    /// <summary>True if a tax invoice (downloadable as PDF) is available for this receipt.</summary>
    public bool IsTaxInvoiceAvailable { get; init; }

    /// <summary>The properties of the receipt related to Flybuys.</summary>
    public ReceiptFlybuys? Flybuys { get; init; }

    /// <summary>True if this receipt contains only items being returned.</summary>
    [GraphQLName("return")]
    public bool IsReturn { get; init; }

    /// <summary>
    /// Resolves the Receipt entity when another subgraph references it.
    /// Uses a DataLoader so that many references in one request become a single batched lookup.
    /// </summary>
    [ReferenceResolver]
    public static async Task<Receipt?> ResolveReferenceAsync(
        string id,
        IReceiptByIdDataLoader receiptById,
        CancellationToken cancellationToken)
        => await receiptById.LoadAsync(id, cancellationToken);
}
