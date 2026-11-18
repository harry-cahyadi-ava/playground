using System.Text.Json;
using HotChocolate;
using HotChocolate.Execution;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReceiptSubgraph.Authorization;
using ReceiptSubgraph.Data;
using ReceiptSubgraph.Domain;
using ReceiptSubgraph.Paging;

namespace ReceiptSubgraph.Tests;

/// <summary>Builds the real subgraph (all registrations from <c>AddReceiptSubgraph</c>) around a chosen set of receipts.</summary>
internal sealed class SubgraphHarness : IAsyncDisposable
{
    private readonly ServiceProvider _services;

    private SubgraphHarness(ServiceProvider services, IRequestExecutor executor, RecordingReceiptRepository repository)
    {
        _services = services;
        Executor = executor;
        Repository = repository;
    }

    public IRequestExecutor Executor { get; }

    public RecordingReceiptRepository Repository { get; }

    public static async Task<SubgraphHarness> CreateAsync(IEnumerable<Receipt>? receipts = null)
    {
        var configuration = new ConfigurationBuilder().Build();
        var repository = new RecordingReceiptRepository(
            new InMemoryReceiptRepository(receipts ?? SampleReceiptData.Create(), new PaginationOptions()));

        var services = new ServiceCollection()
            .AddLogging()
            .AddReceiptSubgraph(configuration)
            // Registered after AddReceiptSubgraph so it replaces the default repository.
            .AddSingleton<IReceiptRepository>(repository)
            .BuildServiceProvider();

        var executor = await services.GetRequestExecutorAsync(cancellationToken: TestContext.Current.CancellationToken);
        return new SubgraphHarness(services, executor, repository);
    }

    /// <summary>Executes a query and returns the JSON response as a document, optionally as a signed-in customer.</summary>
    public async Task<JsonDocument> QueryAsync(
        string query,
        Dictionary<string, object?>? variables = null,
        string? customerId = null,
        string? loyaltyAssociationCode = null)
    {
        var request = OperationRequestBuilder.New().SetDocument(query);

        if (variables is not null)
        {
            request.SetVariableValues(variables);
        }

        if (customerId is not null)
        {
            request.SetGlobalState(
                UserContext.GlobalStateKey,
                new UserContext { CustomerId = customerId, LoyaltyAssociationCode = loyaltyAssociationCode });
        }

        var result = await Executor.ExecuteAsync(request.Build(), TestContext.Current.CancellationToken);
        return JsonDocument.Parse(result.ToJson());
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();
}
