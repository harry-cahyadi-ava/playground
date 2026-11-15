namespace ReceiptSubgraph.Authorization;

/// <summary>Controls validation of the <c>Authorization</c> header (role flow).</summary>
public sealed class AuthorizationOptions
{
    public const string SectionName = $"{GraphQLSettings.SectionName}:Authorization";

    /// <summary>When false the <c>Authorization</c> header is not validated (local development only).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Roles the caller must hold, passed to the role handler.</summary>
    public string[] RequiredRoles { get; set; } = [];
}

/// <summary>Controls validation of the <c>UserAuthorization</c> header (user flow).</summary>
public sealed class UserAuthorizationOptions
{
    public const string SectionName = $"{GraphQLSettings.SectionName}:UserAuthorization";

    /// <summary>When false the <c>UserAuthorization</c> header is not validated (local development only).</summary>
    public bool Enabled { get; set; } = true;
}
