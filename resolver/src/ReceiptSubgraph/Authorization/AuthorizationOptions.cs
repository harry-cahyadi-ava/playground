namespace ReceiptSubgraph.Authorization;

/// <summary>Controls validation of the <c>Authorization</c> header (JWT bearer token).</summary>
public sealed class AuthorizationOptions
{
    public const string SectionName = $"{GraphQLSettings.SectionName}:Authorization";

    /// <summary>When false the <c>Authorization</c> header is not validated (local development only).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The OpenID Connect authority that issues the tokens, e.g. <c>https://login.example.com</c>.
    /// Its discovery document supplies the signing keys, and the issuer unless <see cref="Issuer"/> is set.
    /// </summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// The issuer (<c>iss</c> claim) that tokens must carry, compared exactly. Leave empty to use the issuer
    /// from the authority's discovery document, which is right for most providers.
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>The audience (<c>aud</c> claim) that tokens must be issued for.</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>Whether the discovery document must be fetched over HTTPS. Only turn this off for local development.</summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>The claim that holds the caller's roles.</summary>
    public string RoleClaimType { get; set; } = "roles";

    /// <summary>Roles the caller must hold. All of them are required.</summary>
    public string[] RequiredRoles { get; set; } = [];
}
