using HotChocolate.ApolloFederation.Types;
using HotChocolate.Types;

namespace ReceiptSubgraph.Domain;

// Entities owned by other subgraphs. Receipt only needs their id, so these are lightweight stubs
// that the router can use to continue the query in the owning subgraph.

/// <summary>Reference to a Customer owned by the Customer subgraph (not resolvable here).</summary>
[Key("id", resolvable: false)]
public sealed record Customer([property: ID] string Id);

/// <summary>Reference to an Order owned by the Order subgraph (not resolvable here).</summary>
[Key("id", resolvable: false)]
public sealed record Order([property: ID] string Id);

/// <summary>Reference to a Product owned by the Product subgraph (not resolvable here).</summary>
[Key("id", resolvable: false)]
public sealed record Product([property: ID] string Id);

/// <summary>Reference to a Store owned by the Store subgraph (not resolvable here).</summary>
[Key("id", resolvable: false)]
public sealed record Store([property: ID] string Id);
