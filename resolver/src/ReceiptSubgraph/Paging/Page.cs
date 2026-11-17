using HotChocolate;
using HotChocolate.ApolloFederation.Types;

namespace ReceiptSubgraph.Paging;

/// <summary>Raised when the client supplies pagination arguments that cannot be honoured.</summary>
public sealed class InvalidPaginationException(string message) : Exception(message);

/// <summary>A page of items together with the details describing its position in the full result set.</summary>
/// <param name="Items">The items in the page.</param>
/// <param name="PageInfo">The details about the page.</param>
/// <param name="TotalCount">The number of items in the full result set, across all pages.</param>
/// <param name="Offset">The position of the first item of the page within the full result set.</param>
public sealed record Page<T>(IReadOnlyList<T> Items, PageInfo PageInfo, int TotalCount, int Offset)
{
    /// <summary>Wraps every item in an edge that carries the cursor of that item.</summary>
    public IReadOnlyList<TEdge> ToEdges<TEdge>(Func<string, T, TEdge> createEdge)
    {
        if (Items.Count == 0)
        {
            return [];
        }

        var edges = new TEdge[Items.Count];
        for (var i = 0; i < edges.Length; i++)
        {
            edges[i] = createEdge(CursorCodec.Encode(Offset + i), Items[i]);
        }

        return edges;
    }
}

/// <summary>Factory for <see cref="Page{T}"/>, kept non-generic so callers don't have to spell out the type twice.</summary>
public static class Page
{
    /// <summary>A shared page with no items, for results that matched nothing.</summary>
    public static Page<T> Empty<T>() => EmptyPage<T>.Instance;

    private static class EmptyPage<T>
    {
        public static readonly Page<T> Instance = new([], new PageInfo(false, false, null, null), 0, 0);
    }
}

/// <summary>The details about the page of items returned.</summary>
[GraphQLName("PageInfo")]
[Shareable]
public sealed record PageInfo(
    bool HasNextPage,
    bool HasPreviousPage,
    string? StartCursor,
    string? EndCursor);
