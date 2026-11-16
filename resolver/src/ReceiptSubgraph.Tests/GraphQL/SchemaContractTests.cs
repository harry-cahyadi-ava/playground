using HotChocolate.Language;

namespace ReceiptSubgraph.Tests.GraphQL;

/// <summary>
/// Verifies that the subgraph built from the C# types still honours the contract in <c>schema.graphql</c>:
/// every type, field, argument, nullability, enum value, union member and federation key must match.
/// Extra types that federation adds (Query, _Service, ...) are allowed.
/// </summary>
public class SchemaContractTests : IAsyncLifetime
{
    private SubgraphHarness _harness = null!;

    public async ValueTask InitializeAsync() => _harness = await SubgraphHarness.CreateAsync();

    public async ValueTask DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task Built_schema_satisfies_every_type_in_the_contract()
    {
        var contract = Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "schema.graphql")));
        var actual = await LoadActualDefinitionsAsync();

        var contractTypes = contract.Definitions.OfType<ITypeDefinitionNode>().ToList();
        Assert.NotEmpty(contractTypes);

        foreach (var expected in contractTypes)
        {
            Assert.True(actual.TryGetValue(expected.Name.Value, out var found), $"Type '{expected.Name.Value}' is missing from the subgraph.");

            // Federation adds _entities and _service to Query, so only the fields the contract declares are compared.
            if (expected is ObjectTypeDefinitionNode { Name.Value: "Query" } query)
            {
                var actualFields = ((ObjectTypeDefinitionNode)found!).Fields.Select(Field).ToHashSet();

                foreach (var field in query.Fields.Select(Field))
                {
                    Assert.Contains(field, actualFields);
                }

                continue;
            }

            Assert.Equal(Describe(expected), Describe(found!));
        }
    }

    [Fact]
    public void Contract_file_is_not_empty()
    {
        // Guards the contract test against silently checking nothing if the file is replaced by an empty one.
        var contract = Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "schema.graphql")));

        Assert.True(contract.Definitions.OfType<ITypeDefinitionNode>().Count() > 25);
    }

    [Fact]
    public async Task Federation_entities_are_exposed()
    {
        var sdl = await LoadSdlAsync();

        // Customer and Order are references only, so Receipt is the sole resolvable entity.
        Assert.Matches(@"union _Entity = Receipt\s*(\r?\n|$)", sdl);
    }

    [Fact]
    public async Task Subgraph_sdl_links_federation_2_5_and_the_namespace_directive()
    {
        var sdl = await LoadSdlAsync();

        Assert.Contains("https://specs.apollo.dev/federation/v2.5", sdl);
        Assert.Contains("@namespace(name: \"Receipt\")", sdl);
        Assert.DoesNotContain("@cost", sdl);
    }

    private async Task<string> LoadSdlAsync()
    {
        using var result = await _harness.QueryAsync("{ _service { sdl } }");
        return result.RootElement.GetProperty("data").GetProperty("_service").GetProperty("sdl").GetString()!;
    }

    private async Task<Dictionary<string, ITypeDefinitionNode>> LoadActualDefinitionsAsync()
        => Parse(await LoadSdlAsync()).Definitions
            .OfType<ITypeDefinitionNode>()
            .ToDictionary(d => d.Name.Value);

    private static DocumentNode Parse(string sdl) => Utf8GraphQLParser.Parse(sdl);

    // Reduces a definition to the facts that make up the contract so that two definitions compare as plain text.
    private static string Describe(ITypeDefinitionNode node)
    {
        var lines = new List<string>();

        switch (node)
        {
            case ObjectTypeDefinitionNode obj:
                lines.Add($"type {obj.Name.Value} implements {string.Join(",", obj.Interfaces.Select(i => i.Name.Value).Order())} {Directives(obj.Directives)}");
                lines.AddRange(obj.Fields.Select(Field).Order());
                break;
            case InterfaceTypeDefinitionNode iface:
                lines.Add($"interface {iface.Name.Value}");
                lines.AddRange(iface.Fields.Select(Field).Order());
                break;
            case InputObjectTypeDefinitionNode input:
                lines.Add($"input {input.Name.Value}");
                lines.AddRange(input.Fields.Select(f => $"{f.Name.Value}: {f.Type}").Order());
                break;
            case EnumTypeDefinitionNode enumType:
                lines.Add($"enum {enumType.Name.Value}");
                lines.AddRange(enumType.Values.Select(v => $"{v.Name.Value} {Directives(v.Directives)}").Order());
                break;
            case UnionTypeDefinitionNode union:
                lines.Add($"union {union.Name.Value} = {string.Join("|", union.Types.Select(t => t.Name.Value).Order())}");
                break;
            default:
                lines.Add($"{node.Kind} {node.Name.Value}");
                break;
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string Field(FieldDefinitionNode field)
    {
        var arguments = string.Join(", ", field.Arguments.Select(a => $"{a.Name.Value}: {a.Type}"));
        return $"{field.Name.Value}({arguments}): {field.Type}";
    }

    // Only directives that are part of the contract (federation and @metadata) are compared, in a stable order.
    private static string Directives(IReadOnlyList<DirectiveNode> directives)
        => string.Join(" ", directives
            .Where(d => d.Name.Value is "key" or "shareable" or "metadata")
            .Select(d => $"@{d.Name.Value}({string.Join(",", d.Arguments.Select(a => $"{a.Name.Value}={a.Value.Value}"))})")
            .Order());
}
