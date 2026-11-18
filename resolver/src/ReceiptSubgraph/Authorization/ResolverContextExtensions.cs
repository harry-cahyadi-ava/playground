using HotChocolate.Resolvers;

namespace ReceiptSubgraph.Authorization;

public static class ResolverContextExtensions
{
    /// <summary>
    /// The id of the signed-in customer taken from the request's <see cref="UserContext"/>,
    /// or null when the request carries no customer.
    /// </summary>
    public static string? GetCustomerId(this IResolverContext context)
        => context.GetGlobalStateOrDefault<UserContext>(UserContext.GlobalStateKey)?.CustomerId is { Length: > 0 } customerId
            ? customerId
            : null;
}
