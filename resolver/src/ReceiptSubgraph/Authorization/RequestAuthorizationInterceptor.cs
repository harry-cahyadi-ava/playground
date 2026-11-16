using HotChocolate;
using HotChocolate.AspNetCore;
using HotChocolate.Execution;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ReceiptSubgraph.Authorization;

/// <summary>
/// Authorises every GraphQL HTTP request before it executes: the <c>Authorization</c> header is validated for the
/// required roles and the user's properties are stored in the request's global state under
/// <see cref="UserContext.GlobalStateKey"/>.
/// </summary>
public sealed class RequestAuthorizationInterceptor : DefaultHttpRequestInterceptor
{
    public const string AuthorizationHeader = "Authorization";
    public const string UnauthenticatedCode = "UNAUTHENTICATED";
    public const string ForbiddenCode = "FORBIDDEN";

    private const string BearerPrefix = "Bearer ";

    public override async ValueTask OnCreateAsync(
        HttpContext context,
        IRequestExecutor requestExecutor,
        OperationRequestBuilder requestBuilder,
        CancellationToken cancellationToken)
    {
        // Application services come from the request scope because interceptors are built from the schema services.
        var services = context.RequestServices;
        var options = services.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

        if (options.Enabled)
        {
            var token = GetBearerToken(context, AuthorizationHeader);
            var result = await services
                .GetRequiredService<IAuthorizationHandler>()
                .ValidateAsync(token, options.RequiredRoles, cancellationToken);

            if (!result.Succeeded)
            {
                throw Reject(result.Failure, result.Message);
            }

            if (result.User is not null)
            {
                requestBuilder.SetGlobalState(UserContext.GlobalStateKey, result.User);
            }
        }

        await base.OnCreateAsync(context, requestExecutor, requestBuilder, cancellationToken);
    }

    private static string GetBearerToken(HttpContext context, string headerName)
    {
        var value = context.Request.Headers[headerName].ToString();

        if (!value.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase)
            || value.AsSpan(BearerPrefix.Length).Trim().IsEmpty)
        {
            throw Reject(AuthorizationFailure.Unauthenticated, $"A Bearer token is required in the {headerName} header.");
        }

        return value[BearerPrefix.Length..].Trim();
    }

    private static GraphQLException Reject(AuthorizationFailure? failure, string? message)
    {
        var forbidden = failure == AuthorizationFailure.Forbidden;

        return new GraphQLException(
            ErrorBuilder.New()
                .SetMessage(message ?? (forbidden ? "The caller is not permitted to perform this request." : "Authentication failed."))
                .SetCode(forbidden ? ForbiddenCode : UnauthenticatedCode)
                .Build());
    }
}
