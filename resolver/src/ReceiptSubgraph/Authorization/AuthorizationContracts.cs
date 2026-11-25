namespace ReceiptSubgraph.Authorization;

/// <summary>Why a token was rejected.</summary>
public enum AuthorizationFailure
{
    /// <summary>The token is missing, malformed, expired or otherwise not valid.</summary>
    Unauthenticated,

    /// <summary>The token is valid but the caller lacks a required role.</summary>
    Forbidden,
}

/// <summary>The outcome of validating the <c>Authorization</c> token. A successful result carries the signed-in user.</summary>
public sealed record AuthorizationResult(
    bool Succeeded,
    UserContext? User = null,
    AuthorizationFailure? Failure = null,
    string? Message = null)
{
    public static AuthorizationResult Success(UserContext user) => new(true, user);

    public static AuthorizationResult Fail(AuthorizationFailure failure, string message) => new(false, null, failure, message);
}

/// <summary>
/// Properties of the signed-in user and request, extracted from the <c>Authorization</c> token and the optional
/// request headers. Add the properties the reusable library returns.
/// </summary>
public sealed record UserContext
{
    /// <summary>The request global-state key under which the context is available to resolvers.</summary>
    public const string GlobalStateKey = "UserContext";

    /// <summary>The id of the signed-in customer (the JWT <c>sub</c> claim). Receipts are only ever returned for this customer.</summary>
    public string? CustomerId { get; init; }

}

/// <summary>Validates the standard <c>Authorization</c> bearer token, checks the caller's roles and extracts the user.</summary>
public interface IAuthorizationHandler
{
    /// <param name="token">The bearer token, without the <c>Bearer </c> prefix.</param>
    /// <param name="requiredRoles">Roles configured under <c>GraphQL:Authorization:RequiredRoles</c>.</param>
    /// <param name="cancellationToken">Cancels the validation.</param>
    ValueTask<AuthorizationResult> ValidateAsync(
        string token,
        IReadOnlyCollection<string> requiredRoles,
        CancellationToken cancellationToken);
}
