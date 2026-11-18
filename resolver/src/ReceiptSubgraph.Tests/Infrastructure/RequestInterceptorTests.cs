using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using HotChocolate.Execution;
using ReceiptSubgraph.Authorization;
using ReceiptSubgraph.Infrastructure;

namespace ReceiptSubgraph.Tests.Infrastructure;

public sealed class RequestInterceptorTests
{
    private const string TypenameQuery = """{"query":"{ __typename }"}""";
    private const string ReceiptsQuery = """{"query":"{ receipts { __typename ... on ReceiptConnection { totalCount } } }"}""";

    private static AuthorizationResult SignedIn(string customerId = "customer-1")
        => AuthorizationResult.Success(new UserContext { CustomerId = customerId });

    [Fact]
    public async Task Valid_header_executes_the_request_and_passes_token_and_roles_to_the_handler()
    {
        var roles = new FakeHandler(SignedIn());

        await using var host = await StartAsync(roles);
        var response = await host.PostAsync("bearer role-token");

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("Query", response.Json.RootElement.GetProperty("data").GetProperty("__typename").GetString());
        Assert.Equal("role-token", roles.Token);
        Assert.Equal(["receipts.read"], roles.RequiredRoles);
    }

    [Fact]
    public async Task The_user_from_the_token_is_available_to_the_resolvers()
    {
        await using var host = await StartAsync(new FakeHandler(SignedIn("customer-1")));
        var response = await host.PostAsync("Bearer role-token", ReceiptsQuery);

        var receipts = response.Json.RootElement.GetProperty("data").GetProperty("receipts");
        Assert.Equal("ReceiptConnection", receipts.GetProperty("__typename").GetString());
        Assert.Equal(3, receipts.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task A_valid_token_without_a_customer_is_not_allowed_to_list_receipts()
    {
        await using var host = await StartAsync(new FakeHandler(AuthorizationResult.Success(new UserContext())));
        var response = await host.PostAsync("Bearer role-token", ReceiptsQuery);

        Assert.Equal(
            "UnauthorizedError",
            response.Json.RootElement.GetProperty("data").GetProperty("receipts").GetProperty("__typename").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Basic abc")]
    [InlineData("Bearer ")]
    public async Task Missing_or_non_bearer_authorization_header_is_unauthenticated(string? header)
    {
        var roles = new FakeHandler(SignedIn());

        await using var host = await StartAsync(roles);
        var response = await host.PostAsync(header);

        AssertError(response, RequestInterceptor.UnauthenticatedCode);
        Assert.Null(roles.Token);
    }

    [Fact]
    public async Task Missing_role_is_forbidden()
    {
        var roles = new FakeHandler(AuthorizationResult.Fail(AuthorizationFailure.Forbidden, "Missing role."));

        await using var host = await StartAsync(roles);
        var response = await host.PostAsync("Bearer role-token");

        AssertError(response, RequestInterceptor.ForbiddenCode, "Missing role.");
    }

    [Fact]
    public async Task Invalid_token_is_unauthenticated()
    {
        var roles = new FakeHandler(AuthorizationResult.Fail(AuthorizationFailure.Unauthenticated, "Bad token."));

        await using var host = await StartAsync(roles);
        var response = await host.PostAsync("Bearer role-token");

        AssertError(response, RequestInterceptor.UnauthenticatedCode, "Bad token.");
    }

    [Fact]
    public async Task Disabled_authorization_skips_validation()
    {
        var roles = new FakeHandler(AuthorizationResult.Fail(AuthorizationFailure.Forbidden, "Should not be called."));

        await using var host = await StartAsync(roles, enabled: false);
        var response = await host.PostAsync(null);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Null(roles.Token);
    }

    [Theory]
    [InlineData("gold", "gold")]
    [InlineData("  gold  ", "gold")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public async Task The_optional_loyalty_association_header_is_added_to_the_user_context_as_the_loyalty_association_code(string? header, string? expected)
    {
        var user = await CreateUserContextAsync(header);

        Assert.Equal(expected, user.LoyaltyAssociationCode);
        Assert.Equal("customer-1", user.CustomerId);
    }

    private static async Task<UserContext> CreateUserContextAsync(string? loyaltyAssociationHeader)
    {
        var services = new ServiceCollection()
            .AddSingleton(Options.Create(new AuthorizationOptions()))
            .AddSingleton<IAuthorizationHandler>(new FakeHandler(SignedIn()))
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = services };
        httpContext.Request.Headers["Authorization"] = "Bearer role-token";

        if (loyaltyAssociationHeader is not null)
        {
            httpContext.Request.Headers[RequestInterceptor.LoyaltyAssociationHeader] = loyaltyAssociationHeader;
        }

        var builder = OperationRequestBuilder.New().SetDocument("{ __typename }");
        await new RequestInterceptor().OnCreateAsync(
            httpContext, null!, builder, TestContext.Current.CancellationToken);

        var request = Assert.IsType<OperationRequest>(builder.Build());
        return Assert.IsType<UserContext>(request.ContextData![UserContext.GlobalStateKey]);
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

    private static async Task<TestHost> StartAsync(FakeHandler roles, bool enabled = true)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["GraphQL:Authorization:Enabled"] = enabled.ToString(),
            ["GraphQL:Authorization:Authority"] = "https://issuer.test",
            ["GraphQL:Authorization:Audience"] = "receipts",
            ["GraphQL:Authorization:RequiredRoles:0"] = "receipts.read",
        });
        builder.Services.AddReceiptSubgraph(builder.Configuration);
        // Registered after AddReceiptSubgraph so it replaces the default adapter.
        builder.Services.AddSingleton<IAuthorizationHandler>(roles);

        var app = builder.Build();
        app.MapGraphQL();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return new TestHost(app);
    }

    private sealed record Response(HttpStatusCode Status, JsonDocument Json);

    private sealed class TestHost(WebApplication app) : IAsyncDisposable
    {
        public async Task<Response> PostAsync(string? authorization, string query = TypenameQuery)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
            {
                Content = new StringContent(query, Encoding.UTF8, new MediaTypeHeaderValue("application/json")),
            };

            if (authorization is not null)
            {
                request.Headers.TryAddWithoutValidation("Authorization", authorization);
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

    private sealed class FakeHandler(AuthorizationResult result) : IAuthorizationHandler
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
}
