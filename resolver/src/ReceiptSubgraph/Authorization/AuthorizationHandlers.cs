using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace ReceiptSubgraph.Authorization;

/// <summary>
/// Validates a JWT bearer token against the configured OpenID Connect authority: the signature (using the
/// authority's signing keys), issuer, audience and lifetime. It then checks the required roles and takes the
/// customer id from the <c>sub</c> claim.
/// </summary>
public sealed class AuthorizationHandler(
    IOptions<AuthorizationOptions> options,
    IConfigurationManager<OpenIdConnectConfiguration> configurationManager,
    ILogger<AuthorizationHandler> logger) : IAuthorizationHandler
{
    private const string SubjectClaim = "sub";
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(1);

    private readonly JsonWebTokenHandler _tokenHandler = new();

    public async ValueTask<AuthorizationResult> ValidateAsync(
        string token,
        IReadOnlyCollection<string> requiredRoles,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var result = await ValidateTokenAsync(token, settings, forceKeyRefresh: false, cancellationToken);

        // An unknown signing key usually means the authority rotated its keys: reload them and try once more.
        if (!result.IsValid && result.Exception is SecurityTokenSignatureKeyNotFoundException)
        {
            result = await ValidateTokenAsync(token, settings, forceKeyRefresh: true, cancellationToken);
        }

        // The reason is only logged, so a rejected token reveals nothing about why to the caller.
        if (!result.IsValid)
        {
            logger.LogWarning("Access token rejected: {Reason}", result.Exception?.Message ?? "unknown reason");
            return AuthorizationResult.Fail(AuthorizationFailure.Unauthenticated, "The access token is not valid.");
        }

        var roles = result.ClaimsIdentity.FindAll(settings.RoleClaimType).Select(claim => claim.Value).ToHashSet(StringComparer.Ordinal);

        if (!requiredRoles.All(roles.Contains))
        {
            return AuthorizationResult.Fail(AuthorizationFailure.Forbidden, "The caller is not permitted to perform this request.");
        }

        return AuthorizationResult.Success(new UserContext { CustomerId = result.ClaimsIdentity.FindFirst(SubjectClaim)?.Value });
    }

    private async Task<TokenValidationResult> ValidateTokenAsync(
        string token,
        AuthorizationOptions settings,
        bool forceKeyRefresh,
        CancellationToken cancellationToken)
    {
        if (forceKeyRefresh)
        {
            configurationManager.RequestRefresh();
        }

        var configuration = await configurationManager.GetConfigurationAsync(cancellationToken);

        return await _tokenHandler.ValidateTokenAsync(
            token,
            new TokenValidationParameters
            {
                ValidIssuer = string.IsNullOrWhiteSpace(settings.Issuer) ? configuration.Issuer : settings.Issuer,
                IssuerSigningKeys = configuration.SigningKeys,
                ValidAudience = settings.Audience,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                RequireSignedTokens = true,
                RequireExpirationTime = true,
                ClockSkew = ClockSkew,
                RoleClaimType = settings.RoleClaimType,
            });
    }
}
