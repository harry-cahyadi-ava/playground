using HotChocolate.Types;

namespace ReceiptSubgraph.GraphQL.Directives;

/// <summary>Runtime value of the <c>@namespace(name)</c> schema directive.</summary>
public sealed record NamespaceDirective(string Name);

/// <summary>Declares <c>directive @namespace(name: String!) on SCHEMA</c>.</summary>
public sealed class NamespaceDirectiveType : DirectiveType<NamespaceDirective>
{
    protected override void Configure(IDirectiveTypeDescriptor<NamespaceDirective> descriptor)
    {
        descriptor.Name("namespace");
        descriptor.Description("Declares the namespace of the subgraph.");
        descriptor.Location(DirectiveLocation.Schema);
        descriptor.Argument(d => d.Name).Type<NonNullType<StringType>>();
    }
}
