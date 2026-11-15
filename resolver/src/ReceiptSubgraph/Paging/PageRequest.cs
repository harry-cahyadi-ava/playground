using HotChocolate;

namespace ReceiptSubgraph.Paging;

/// <summary>
/// Cursor pagination arguments supplied by the client (the <c>PaginationInput</c> input type).
/// </summary>
[GraphQLName("PaginationInput")]
[GraphQLDescription(
    "Cursor-based pagination (Relay semantics).\n" +
    "Cursors are opaque: pass back values taken from an edge or from PageInfo, and do not parse or construct them.\n" +
    "Cursors are positional, so they are only stable while the underlying results do not change.\n" +
    "\n" +
    "- Use `first` (optionally with `after`) to page forwards, or `last` (optionally with `before`) to page backwards.\n" +
    "- If neither `first` nor `last` is supplied, a page of 20 items is returned.\n" +
    "- `first` and `last` must be between 0 and 100, otherwise the request is rejected.")]
public sealed record PageRequest(
    [property: GraphQLDescription("The number of items to return, counting forwards from `after` (or from the start).")]
    int? First = null,
    [property: GraphQLDescription("Return the items that come after this cursor.")]
    string? After = null,
    [property: GraphQLDescription("The number of items to return, counting backwards from `before` (or from the end).")]
    int? Last = null,
    [property: GraphQLDescription("Return the items that come before this cursor.")]
    string? Before = null);
