using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using ReceiptSubgraph.Authorization;

namespace ReceiptSubgraph.Tests.Authorization;

public sealed class AuthorizationHandlerTests : IDisposable
{
    private const string Issuer = "https://issuer.test";
    private const string Audience = "receipts";

    private readonly System.Security.Cryptography.RSA _rsa = System.Security.Cryptography.RSA.Create(2048);
    private readonly RsaSecurityKey _key;

    public AuthorizationHandlerTests() => _key = new RsaSecurityKey(_rsa) { KeyId = "key-1" };

    public void Dispose() => _rsa.Dispose();

    private AuthorizationHandler CreateHandler(IConfigurationManager<OpenIdConnectConfiguration>? manager = null, string issuer = "")
        => new(
            Options.Create(new AuthorizationOptions { Authority = Issuer, Issuer = issuer, Audience = Audience }),
            manager ?? new StaticConfigurationManager<OpenIdConnectConfiguration>(ConfigurationWith(_key)),
            NullLogger<AuthorizationHandler>.Instance);

    private static OpenIdConnectConfiguration ConfigurationWith(SecurityKey key)
    {
        var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
        configuration.SigningKeys.Add(key);
        return configuration;
    }

    private string CreateToken(
        string? subject = "customer-1",
        string[]? roles = null,
        string issuer = Issuer,
        string audience = Audience,
        TimeSpan? expiresIn = null,
        SecurityKey? signingKey = null)
    {
        var claims = new Dictionary<string, object> { ["roles"] = roles ?? ["receipts.read"] };

        if (subject is not null)
        {
            claims["sub"] = subject;
        }

        var now = DateTime.UtcNow;
        var expires = now + (expiresIn ?? TimeSpan.FromMinutes(10));

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Claims = claims,
            NotBefore = expires - TimeSpan.FromHours(1),
            Expires = expires,
            SigningCredentials = new SigningCredentials(signingKey ?? _key, SecurityAlgorithms.RsaSha256),
        });
    }

    [Fact]
    public async Task Valid_token_succeeds_and_the_sub_claim_becomes_the_customer_id()
    {
        var result = await CreateHandler().ValidateAsync(CreateToken("customer-42"), ["receipts.read"], CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("customer-42", result.User!.CustomerId);
    }

    [Fact]
    public async Task Token_without_a_sub_claim_is_valid_but_has_no_customer()
    {
        var result = await CreateHandler().ValidateAsync(CreateToken(subject: null), [], CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(result.User!.CustomerId);
    }

    [Fact]
    public async Task Token_holding_every_required_role_succeeds()
    {
        var token = CreateToken(roles: ["receipts.read", "receipts.write", "other"]);

        var result = await CreateHandler().ValidateAsync(token, ["receipts.read", "receipts.write"], CancellationToken.None);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Token_missing_a_required_role_is_forbidden()
    {
        var token = CreateToken(roles: ["receipts.read"]);

        var result = await CreateHandler().ValidateAsync(token, ["receipts.read", "receipts.write"], CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(AuthorizationFailure.Forbidden, result.Failure);
    }

    [Fact]
    public async Task Token_signed_with_an_unknown_key_is_unauthenticated()
    {
        using var other = System.Security.Cryptography.RSA.Create(2048);
        var token = CreateToken(signingKey: new RsaSecurityKey(other) { KeyId = "other" });

        await AssertUnauthenticatedAsync(token);
    }

    [Theory]
    [InlineData("wrong-audience", Issuer)]
    [InlineData(Audience, "https://evil.test")]
    public async Task Token_for_another_audience_or_issuer_is_unauthenticated(string audience, string issuer)
        => await AssertUnauthenticatedAsync(CreateToken(audience: audience, issuer: issuer));

    [Fact]
    public async Task A_configured_issuer_replaces_the_one_from_the_discovery_document()
    {
        const string configured = "https://configured-issuer.test/";
        var handler = CreateHandler(issuer: configured);

        var accepted = await handler.ValidateAsync(CreateToken(issuer: configured), [], CancellationToken.None);
        var rejected = await handler.ValidateAsync(CreateToken(issuer: Issuer), [], CancellationToken.None);

        Assert.True(accepted.Succeeded);
        Assert.False(rejected.Succeeded);
    }

    [Fact]
    public async Task Expired_token_is_unauthenticated()
        => await AssertUnauthenticatedAsync(CreateToken(expiresIn: TimeSpan.FromMinutes(-10)));

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("a.b.c")]
    public async Task Malformed_token_is_unauthenticated(string token)
        => await AssertUnauthenticatedAsync(token);

    [Fact]
    public async Task Token_signed_with_a_rotated_key_is_accepted_after_the_keys_are_reloaded()
    {
        using var rotated = System.Security.Cryptography.RSA.Create(2048);
        var newKey = new RsaSecurityKey(rotated) { KeyId = "key-2" };
        var manager = new RotatingConfigurationManager(ConfigurationWith(_key), ConfigurationWith(newKey));

        var result = await CreateHandler(manager).ValidateAsync(CreateToken(signingKey: newKey), [], CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(1, manager.Refreshes);
    }

    private async Task AssertUnauthenticatedAsync(string token)
    {
        var result = await CreateHandler().ValidateAsync(token, [], CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(AuthorizationFailure.Unauthenticated, result.Failure);
        // The reason must not leak to the caller.
        Assert.Equal("The access token is not valid.", result.Message);
    }

    // Serves the old keys until a refresh is requested, like an authority that has just rotated its signing keys.
    private sealed class RotatingConfigurationManager(OpenIdConnectConfiguration before, OpenIdConnectConfiguration after)
        : IConfigurationManager<OpenIdConnectConfiguration>
    {
        public int Refreshes { get; private set; }

        public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel)
            => Task.FromResult(Refreshes == 0 ? before : after);

        public void RequestRefresh() => Refreshes++;
    }
}
