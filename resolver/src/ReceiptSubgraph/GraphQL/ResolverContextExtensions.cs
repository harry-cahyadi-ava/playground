using HotChocolate.Resolvers;
using ReceiptSubgraph.Authorization;
using ReceiptSubgraph.Data;

namespace ReceiptSubgraph.GraphQL;

public static class ResolverContextExtensions
{
    /// <summary>
    /// Builds the <see cref="ReceiptUserContext"/> from the request's <see cref="UserContext"/>, or returns null when the
    /// request carries no customer. This is the only place that translates request state into what repositories see.
    /// </summary>
    public static ReceiptUserContext? GetReceiptUserContext(this IResolverContext context)
        => context.GetGlobalStateOrDefault<UserContext>(UserContext.GlobalStateKey) is { CustomerId: { Length: > 0 } customerId } user
            ? new ReceiptUserContext(customerId, user.LoyaltyAssociationCode)
            : null;
}
