using HotChocolate.ApolloFederation;
using HotChocolate.Types;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using ReceiptSubgraph.Authorization;
using ReceiptSubgraph.Data;
using ReceiptSubgraph.Domain;
using ReceiptSubgraph.GraphQL.Directives;
using ReceiptSubgraph.GraphQL.Scalars;
using ReceiptSubgraph.Infrastructure;
using ReceiptSubgraph.Paging;

namespace ReceiptSubgraph;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the receipt data access, options and the GraphQL subgraph.</summary>
    public static IServiceCollection AddReceiptSubgraph(this IServiceCollection services, IConfiguration configuration)
    {
        // Validate options at startup so a bad configuration fails fast instead of on the first request.
        services
            .AddOptions<PaginationOptions>()
            .Bind(configuration.GetSection(PaginationOptions.SectionName))
            .Validate(o => o.DefaultPageSize > 0 && o.MaxPageSize >= o.DefaultPageSize,
                "GraphQL:Pagination:DefaultPageSize must be positive and not exceed GraphQL:Pagination:MaxPageSize.")
            .ValidateOnStart();

        // Resolvers take the plain options object, which avoids an IOptions lookup on every call.
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<PaginationOptions>>().Value);

        // When validation is on, the authority and audience must be configured, otherwise every request would fail.
        services
            .AddOptions<AuthorizationOptions>()
            .Bind(configuration.GetSection(AuthorizationOptions.SectionName))
            .Validate(
                o => !o.Enabled
                    || (Uri.TryCreate(o.Authority, UriKind.Absolute, out _) && !string.IsNullOrWhiteSpace(o.Audience)),
                "GraphQL:Authorization:Authority must be an absolute URL and GraphQL:Authorization:Audience must be set.")
            .ValidateOnStart();

        // The discovery document and signing keys are fetched on first use and then cached and refreshed by the manager.
        services.AddSingleton<IConfigurationManager<OpenIdConnectConfiguration>>(sp =>
        {
            var authorization = sp.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

            return new ConfigurationManager<OpenIdConnectConfiguration>(
                $"{authorization.Authority.TrimEnd('/')}/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever { RequireHttps = authorization.RequireHttpsMetadata });
        });
        services.AddSingleton<IAuthorizationHandler, AuthorizationHandler>();

        // Replace with a real implementation (e.g. Digital Receipt Service client) to leave the sample data behind.
        services.AddSingleton<IReceiptRepository>(sp =>
            new InMemoryReceiptRepository(SampleReceiptData.Create(), sp.GetRequiredService<PaginationOptions>()));

        services
            .AddGraphQLServer()
            // Federation 2.5 matches the @link in the schema (adds @key, @shareable, _entities and _service).
            .AddApolloFederation(FederationVersion.Federation25)
            // Resolvers, DataLoaders and object types discovered by the source generator.
            .AddReceiptTypes()
            // The root fields (receipt, receipts) come from ReceiptQueries; federation adds _entities and _service.
            // Types that are not reachable from any field but are part of the subgraph contract.
            .AddType<NotFoundError>()
            .AddType<UnauthorizedError>()
            .AddType<InternalServerError>()
            .AddType<ReceiptItemBag>()
            // Only reachable through the ReceiptsResponse union, so it is registered explicitly.
            .AddType<ReceiptConnection>()
            .AddType<MoneyType>()
            .AddType<FlybuysBarcodeNumberType>()
            .AddType<FlybuysMemberNumberType>()
            .AddDirectiveType<MetadataDirectiveType>()
            .AddDirectiveType<NamespaceDirectiveType>()
            // Every decimal in the schema is a monetary amount.
            .BindRuntimeType<decimal, MoneyType>()
            .ConfigureSchema(schema => schema.SetSchema(d => d.Directive(new NamespaceDirective("Receipt"))))
            .AddErrorFilter<PaginationErrorFilter>()
            // Validates the Authorization header on every HTTP request to the endpoint.
            .AddHttpRequestInterceptor<RequestInterceptor>()
            // Reject abusively deep operations before they are executed.
            .AddMaxExecutionDepthRule(15)
            // Keep the SDL free of @cost annotations; the subgraph contract is defined by schema.graphql.
            .ModifyCostOptions(o => o.ApplyCostDefaults = false)
            .ModifyRequestOptions(o => o.ExecutionTimeout = TimeSpan.FromSeconds(30));

        return services;
    }
}
