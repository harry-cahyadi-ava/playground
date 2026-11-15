namespace ReceiptSubgraph.Authorization;

/// <summary>Adapter between the subgraph and the reusable role-validation library.</summary>
public sealed class AuthorizationHandler : IAuthorizationHandler
{
    public ValueTask<AuthorizationResult> ValidateAsync(
        string token,
        IReadOnlyCollection<string> requiredRoles,
        CancellationToken cancellationToken)
    {
        // TODO: call the reusable library here, e.g. var result = await library.ValidateAsync(token, requiredRoles);
        // then map its outcome to AuthorizationResult.Success / AuthorizationResult.Fail(...).
        // Not implemented: fail closed until wired up so the endpoint is never accidentally open.
        throw new NotImplementedException("Role authorisation library is not wired up.");
    }
}

/// <summary>Adapter between the subgraph and the reusable user-validation library.</summary>
public sealed class UserAuthorizationHandler : IUserAuthorizationHandler
{
    public ValueTask<UserAuthorizationResult> ValidateAsync(string token, CancellationToken cancellationToken)
    {
        // TODO: call the reusable library here, map the extracted properties onto UserContext
        // and return UserAuthorizationResult.Success(user) or UserAuthorizationResult.Fail(...).
        // Not implemented: fail closed until wired up so the endpoint is never accidentally open.
        throw new NotImplementedException("User authorisation library is not wired up.");
    }
}
