namespace ReceiptSubgraph.Data;

/// <summary>
/// The user the receipts are being requested for. Repositories only see this type, so they don't depend on how the
/// user was identified (token, headers, a test).
/// </summary>
/// <param name="CustomerId">The customer whose receipts may be returned. Receipts of other customers are never returned.</param>
/// <param name="LoyaltyAssociationCode">The optional loyalty association code supplied with the request.</param>
public sealed record ReceiptUserContext(string CustomerId, string? LoyaltyAssociationCode = null);
