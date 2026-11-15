using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReceiptSubgraph.Authorization;

namespace ReceiptSubgraph.Tests.Authorization;

public sealed class RequestAuthorizationInterceptorTests
{
    private const string Query = """{"query":"{ __typename }"}""";

    [Fact]
    public async Task Valid_headers_execute_the_request_and_pass_tokens_and_roles_to_the_handlers()
    {
        var roles = new FakeRoleHandler(AuthorizationResult.Success);
        var users = new FakeUserHandler(UserAuthorizationResult.Success(new UserContext()));

        await using var host = await StartAsync(roles, users);
        var response = await host.PostAsync(role: "Bearer role-token", user: "bearer user-token");

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("Query", response.Json.RootElement.GetProperty("data").GetProperty("__typename").GetString());
        Assert.Equal("role-token", roles.Token);
        Assert.Equal(["receipts.read"], roles.RequiredRoles);
        Assert.Equal("user-token", users.Token);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Basic abc")]
    [InlineData("Bearer ")]
    public async Task Missing_or_non_bearer_authorization_header_is_unauthenticated(string? header)
    {
        var roles = new FakeRoleHandler(AuthorizationResult.Success);
        var users = new FakeUserHandler(UserAuthorizationResult.Success(new UserContext()));

        await using var host = await StartAsync(roles, users);
        var response = await host.PostAsync(role: header, user: "Bearer user-token");

        AssertError(response, RequestAuthorizationInterceptor.UnauthenticatedCode);
        Assert.Null(roles.Token);
    }

    [Fact]
    public async Task Missing_user_authorization_header_is_unauthenticated()
    {
        var roles = new FakeRoleHandler(AuthorizationResult.Success);
        var users = new FakeUserHandler(UserAuthorizationResult.Success(new UserContext()));

        await using var host = await StartAsync(roles, users);
        var response = await host.PostAsync(role: "Bearer role-token", user: null);

        AssertError(response, RequestAuthorizationInterceptor.UnauthenticatedCode);
        Assert.Null(users.Token);
    }

    [Fact]
    public async Task Missing_role_is_forbidden_and_the_user_header_is_not_evaluated()
    {
        var roles = new FakeRoleHandler(AuthorizationResult.Fail(AuthorizationFailure.Forbidden, "Missing role."));
        var users = new FakeUserHandler(UserAuthorizationResult.Success(new UserContext()));

        await using var host = await StartAsync(roles, users);
        var response = await host.PostAsync(role: "Bearer role-token", user: "Bearer user-token");

        AssertError(response, RequestAuthorizationInterceptor.ForbiddenCode, "Missing role.");
        Assert.Null(users.Token);
    }

    [Fact]
    public async Task Invalid_user_token_is_unauthenticated()
    {
        var roles = new FakeRoleHandler(AuthorizationResult.Success);
        var users = new FakeUserHandler(UserAuthorizationResult.Fail(AuthorizationFailure.Unauthenticated, "Bad user token."));

        await using var host = await StartAsync(roles, users);
        var response = await host.PostAsync(role: "Bearer role-token", user: "Bearer user-token");

        AssertError(response, RequestAuthorizationInterceptor.UnauthenticatedCode, "Bad user token.");
    }

    [Fact]
    public async Task Disabled_authorization_skips_validation()
    {
        var roles = new FakeRoleHandler(AuthorizationResult.Fail(AuthorizationFailure.Forbidden, "Should not be called."));
        var users = new FakeUserHandler(UserAuthorizationResult.Fail(AuthorizationFailure.Forbidden, "Should not be called."));

        await using var host = await StartAsync(roles, users, roleEnabled: false, userEnabled: false);
        var response = await host.PostAsync(role: null, user: null);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Null(roles.Token);
        Assert.Null(users.Token);
    }

    [Fact]
    public async Task Each_flow_can_be_disabled_independently()
    {
        var roles = new FakeRoleHandler(AuthorizationResult.Success);
        var users = new FakeUserHandler(UserAuthorizationResult.Success(new UserContext()));

        await using var host = await StartAsync(roles, users, roleEnabled: false);
        var response = await host.PostAsync(role: null, user: "Bearer user-token");

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Null(roles.Token);
        Assert.Equal("user-token", users.Token);
    }

    private static void AssertError(Response response, string code, string? message = null)
    {
        Assert.False(response.Json.RootElement.TryGetProperty("data", out var data) && data.ValueKind != JsonValueKind.Null);
        var error = response.Json.RootElement.GetProperty("errors")[0];
        Assert.Equal(code, error.GetProperty("extensions").GetProperty("code").GetString());

        if (message is not null)
        {
            Assert.Equal(message, error.GetProperty("message").GetString());
        }
    }

    private static async Task<TestHost> StartAsync(
        FakeRoleHandler roles,
        FakeUserHandler users,
        bool roleEnabled = true,
        bool userEnabled = true)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["GraphQL:Authorization:Enabled"] = roleEnabled.ToString(),
            ["GraphQL:Authorization:RequiredRoles:0"] = "receipts.read",
            ["GraphQL:UserAuthorization:Enabled"] = userEnabled.ToString(),
        });
        builder.Services.AddReceiptSubgraph(builder.Configuration);
        // Registered after AddReceiptSubgraph so they replace the default adapters.
        builder.Services.AddSingleton<IAuthorizationHandler>(roles);
        builder.Services.AddSingleton<IUserAuthorizationHandler>(users);

        var app = builder.Build();
        app.MapGraphQL();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return new TestHost(app);
    }

    private sealed record Response(HttpStatusCode Status, JsonDocument Json);

    private sealed class TestHost(WebApplication app) : IAsyncDisposable
    {
        public async Task<Response> PostAsync(string? role, string? user)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
            {
                Content = new StringContent(Query, Encoding.UTF8, new MediaTypeHeaderValue("application/json")),
            };

            if (role is not null)
            {
                request.Headers.TryAddWithoutValidation("Authorization", role);
            }

            if (user is not null)
            {
                request.Headers.TryAddWithoutValidation("UserAuthorization", user);
            }

            using var response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            return new Response(response.StatusCode, JsonDocument.Parse(body));
        }

        public async ValueTask DisposeAsync()
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    private sealed class FakeRoleHandler(AuthorizationResult result) : IAuthorizationHandler
    {
        public string? Token { get; private set; }

        public IReadOnlyCollection<string>? RequiredRoles { get; private set; }

        public ValueTask<AuthorizationResult> ValidateAsync(
            string token,
            IReadOnlyCollection<string> requiredRoles,
            CancellationToken cancellationToken)
        {
            Token = token;
            RequiredRoles = requiredRoles;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FakeUserHandler(UserAuthorizationResult result) : IUserAuthorizationHandler
    {
        public string? Token { get; private set; }

        public ValueTask<UserAuthorizationResult> ValidateAsync(string token, CancellationToken cancellationToken)
        {
            Token = token;
            return ValueTask.FromResult(result);
        }
    }
}
