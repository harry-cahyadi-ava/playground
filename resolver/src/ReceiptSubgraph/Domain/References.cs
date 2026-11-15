using HotChocolate.ApolloFederation.Resolvers;
using HotChocolate.ApolloFederation.Types;
using HotChocolate.Types;

namespace ReceiptSubgraph.Domain;

// Entities owned by other subgraphs. Receipt only needs their id, so these are lightweight stubs
// that the router can use to continue the query in the owning subgraph.

/// <summary>Customer entity, extended here with the customer's receipts.</summary>
[Key("id")]
public sealed record Customer([property: ID] string Id)
{
    /// <summary>The router only sends the key, which is all this subgraph needs - no lookup required.</summary>
    [ReferenceResolver]
    public static Customer ResolveReference(string id) => new(id);
}

/// <summary>Order entity, extended here with the order's receipts.</summary>
[Key("id")]
public sealed record Order([property: ID] string Id)
{
    [ReferenceResolver]
    public static Order ResolveReference(string id) => new(id);
}

/// <summary>Reference to a Product owned by the Product subgraph (not resolvable here).</summary>
[Key("id", resolvable: false)]
public sealed record Product([property: ID] string Id);

/// <summary>Reference to a Store owned by the Store subgraph (not resolvable here).</summary>
[Key("id", resolvable: false)]
public sealed record Store([property: ID] string Id);
