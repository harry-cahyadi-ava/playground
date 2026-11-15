namespace ReceiptSubgraph.Paging;

/// <summary>Controls the page sizes that clients may request.</summary>
public sealed class PaginationOptions
{
    public const string SectionName = $"{GraphQLSettings.SectionName}:Pagination";

    /// <summary>Page size used when the client supplies neither <c>first</c> nor <c>last</c>.</summary>
    public int DefaultPageSize { get; set; } = 20;

    /// <summary>Largest value accepted for <c>first</c> or <c>last</c>; protects the server from huge pages.</summary>
    public int MaxPageSize { get; set; } = 100;
}
