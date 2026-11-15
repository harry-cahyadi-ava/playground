namespace ReceiptSubgraph.Authorization;

/// <summary>Why a token was rejected.</summary>
public enum AuthorizationFailure
{
    /// <summary>The token is missing, malformed, expired or otherwise not valid.</summary>
    Unauthenticated,

    /// <summary>The token is valid but the caller lacks a required role.</summary>
    Forbidden,
}

public sealed record AuthorizationResult(bool Succeeded, AuthorizationFailure? Failure = null, string? Message = null)
{
    public static AuthorizationResult Success { get; } = new(true);

    public static AuthorizationResult Fail(AuthorizationFailure failure, string message) => new(false, failure, message);
}

public sealed record UserAuthorizationResult(
    bool Succeeded,
    UserContext? User = null,
    AuthorizationFailure? Failure = null,
    string? Message = null)
{
    public static UserAuthorizationResult Success(UserContext user) => new(true, user);

    public static UserAuthorizationResult Fail(AuthorizationFailure failure, string message) => new(false, null, failure, message);
}

/// <summary>
/// Properties extracted from the <c>UserAuthorization</c> token. Add the properties the reusable library returns.
/// </summary>
public sealed record UserContext
{
    /// <summary>The request global-state key under which the context is available to resolvers.</summary>
    public const string GlobalStateKey = "UserContext";
}

/// <summary>Validates the standard <c>Authorization</c> bearer token and checks the caller's roles.</summary>
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

/// <summary>Validates the custom <c>UserAuthorization</c> bearer token and extracts the user's properties.</summary>
public interface IUserAuthorizationHandler
{
    /// <param name="token">The bearer token, without the <c>Bearer </c> prefix.</param>
    /// <param name="cancellationToken">Cancels the validation.</param>
    ValueTask<UserAuthorizationResult> ValidateAsync(string token, CancellationToken cancellationToken);
}
