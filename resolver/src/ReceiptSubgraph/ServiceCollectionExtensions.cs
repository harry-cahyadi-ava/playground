using HotChocolate.ApolloFederation;
using HotChocolate.Types;
using Microsoft.Extensions.Options;
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

        services.AddOptions<AuthorizationOptions>().Bind(configuration.GetSection(AuthorizationOptions.SectionName));
        services.AddOptions<UserAuthorizationOptions>().Bind(configuration.GetSection(UserAuthorizationOptions.SectionName));
        services.AddSingleton<IAuthorizationHandler, AuthorizationHandler>();
        services.AddSingleton<IUserAuthorizationHandler, UserAuthorizationHandler>();

        // Replace with a real implementation (e.g. Digital Receipt Service client) to leave the sample data behind.
        services.AddSingleton<IReceiptRepository>(sp =>
            new InMemoryReceiptRepository(SampleReceiptData.Create(), sp.GetRequiredService<PaginationOptions>()));

        services
            .AddGraphQLServer()
            // Federation 2.5 matches the @link in the schema (adds @key, @shareable, _entities and _service).
            .AddApolloFederation(FederationVersion.Federation25)
            // Resolvers, DataLoaders and object types discovered by the source generator.
            .AddReceiptTypes()
            // This subgraph has no root fields of its own; federation adds _entities and _service to it.
            .AddQueryType()
            // Types that are not reachable from any field but are part of the subgraph contract.
            .AddType<NotFoundError>()
            .AddType<UnauthorizedError>()
            .AddType<InternalServerError>()
            .AddType<ReceiptItemBag>()
            .AddType<MoneyType>()
            .AddType<FlybuysBarcodeNumberType>()
            .AddType<FlybuysMemberNumberType>()
            .AddDirectiveType<MetadataDirectiveType>()
            .AddDirectiveType<NamespaceDirectiveType>()
            // Every decimal in the schema is a monetary amount.
            .BindRuntimeType<decimal, MoneyType>()
            .ConfigureSchema(schema => schema.SetSchema(d => d.Directive(new NamespaceDirective("Receipt"))))
            .AddErrorFilter<PaginationErrorFilter>()
            // Validates the Authorization and UserAuthorization headers on every HTTP request to the endpoint.
            //AddHttpRequestInterceptor<RequestAuthorizationInterceptor>()
            // Reject abusively deep operations before they are executed.
            .AddMaxExecutionDepthRule(15)
            // Keep the SDL free of @cost annotations; the subgraph contract is defined by schema.graphql.
            .ModifyCostOptions(o => o.ApplyCostDefaults = false)
            .ModifyRequestOptions(o => o.ExecutionTimeout = TimeSpan.FromSeconds(30));

        return services;
    }
}
