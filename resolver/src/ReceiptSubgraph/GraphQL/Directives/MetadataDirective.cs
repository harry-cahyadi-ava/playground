using System.Reflection;
using HotChocolate.Configuration;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;

namespace ReceiptSubgraph.GraphQL.Directives;

/// <summary>Runtime value of the <c>@metadata(key, value)</c> directive.</summary>
public sealed record MetadataDirective(string Key, string Value);

/// <summary>Declares <c>directive @metadata(key: String!, value: String!) repeatable on ENUM_VALUE</c>.</summary>
public sealed class MetadataDirectiveType : DirectiveType<MetadataDirective>
{
    protected override void Configure(IDirectiveTypeDescriptor<MetadataDirective> descriptor)
    {
        descriptor.Name("metadata");
        descriptor.Description("Attaches a key/value pair of descriptive metadata to an enum value.");
        descriptor.Location(DirectiveLocation.EnumValue);
        descriptor.Repeatable();
        descriptor.Argument(d => d.Key).Type<NonNullType<StringType>>();
        descriptor.Argument(d => d.Value).Type<NonNullType<StringType>>();
    }
}

/// <summary>
/// Annotates a C# enum member so that the matching GraphQL enum value carries a <c>@metadata</c> directive.
/// Keeps the metadata next to the enum definition instead of in a separate schema configuration.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
public sealed class MetadataAttribute(string key, string value) : EnumValueDescriptorAttribute
{
    protected override void OnConfigure(
        IDescriptorContext context,
        IEnumValueDescriptor descriptor,
        FieldInfo? field)
        => descriptor.Directive(new MetadataDirective(key, value));
}
