using HotChocolate.Resolvers;
using ReceiptSubgraph.Authorization;
using ReceiptSubgraph.Data;

namespace ReceiptSubgraph.GraphQL;

public static class ResolverContextExtensions
{
    /// <summary>
    /// Builds the <see cref="ReceiptUserContext"/> from the request's <see cref="UserContext"/>, or returns null when the
    /// request carries no customer. This is the only place that translates request state into what repositories see.
    /// A blank <paramref name="loyaltyAssociationCode"/> (taken from the query input) is treated as absent.
    /// </summary>
    public static ReceiptUserContext? GetReceiptUserContext(this IResolverContext context, string? loyaltyAssociationCode = null)
        => context.GetGlobalStateOrDefault<UserContext>(UserContext.GlobalStateKey) is { CustomerId: { Length: > 0 } customerId }
            ? new ReceiptUserContext(customerId, string.IsNullOrWhiteSpace(loyaltyAssociationCode) ? null : loyaltyAssociationCode.Trim())
            : null;
}
