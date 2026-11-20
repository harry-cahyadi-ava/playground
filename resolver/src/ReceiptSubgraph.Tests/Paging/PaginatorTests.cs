using ReceiptSubgraph.Paging;

namespace ReceiptSubgraph.Tests.Paging;

public class PaginatorTests
{
    private static readonly PaginationOptions Options = new() { DefaultPageSize = 5, MaxPageSize = 10 };

    private static int[] Numbers(int count) => Enumerable.Range(1, count).ToArray();

    private static Page<int> Slice(IReadOnlyList<int> source, PageRequest? request = null)
        => Paginator.Slice(source, request, Options);

    private static string CursorOf(int index) => CursorCodec.Encode(index);

    [Fact]
    public void Without_arguments_returns_default_page_size()
    {
        var page = Slice(Numbers(12));

        Assert.Equal([1, 2, 3, 4, 5], page.Items);
        Assert.True(page.PageInfo.HasNextPage);
        Assert.False(page.PageInfo.HasPreviousPage);
        Assert.Equal(12, page.TotalCount);
    }

    [Fact]
    public void First_limits_the_page_and_reports_cursors()
    {
        var page = Slice(Numbers(6), new PageRequest(First: 3));

        Assert.Equal([1, 2, 3], page.Items);
        Assert.Equal(CursorOf(0), page.PageInfo.StartCursor);
        Assert.Equal(CursorOf(2), page.PageInfo.EndCursor);
        Assert.True(page.PageInfo.HasNextPage);
    }

    [Fact]
    public void After_continues_from_the_cursor()
    {
        var page = Slice(Numbers(6), new PageRequest(First: 2, After: CursorOf(1)));

        Assert.Equal([3, 4], page.Items);
        Assert.True(page.PageInfo.HasPreviousPage);
        Assert.True(page.PageInfo.HasNextPage);
    }

    [Fact]
    public void Walking_pages_with_end_cursor_visits_every_item_once()
    {
        var visited = new List<int>();
        string? after = null;

        Page<int> page;
        do
        {
            page = Slice(Numbers(11), new PageRequest(First: 4, After: after));
            visited.AddRange(page.Items);
            after = page.PageInfo.EndCursor;
        }
        while (page.PageInfo.HasNextPage);

        Assert.Equal(Numbers(11), visited);
    }

    [Fact]
    public void Last_returns_the_final_items()
    {
        var page = Slice(Numbers(6), new PageRequest(Last: 2));

        Assert.Equal([5, 6], page.Items);
        Assert.True(page.PageInfo.HasPreviousPage);
        Assert.False(page.PageInfo.HasNextPage);
    }

    [Fact]
    public void Last_with_before_returns_the_items_preceding_the_cursor()
    {
        var page = Slice(Numbers(6), new PageRequest(Last: 2, Before: CursorOf(4)));

        Assert.Equal([3, 4], page.Items);
        Assert.True(page.PageInfo.HasPreviousPage);
        Assert.True(page.PageInfo.HasNextPage);
    }

    [Fact]
    public void Before_without_size_uses_the_default_page_size()
    {
        var page = Slice(Numbers(20), new PageRequest(Before: CursorOf(10)));

        Assert.Equal([1, 2, 3, 4, 5], page.Items);
    }

    [Fact]
    public void First_and_last_are_applied_in_that_order()
    {
        var page = Slice(Numbers(10), new PageRequest(First: 6, Last: 2));

        Assert.Equal([5, 6], page.Items);
    }

    [Fact]
    public void After_the_final_item_gives_an_empty_page_without_cursors()
    {
        var page = Slice(Numbers(3), new PageRequest(First: 2, After: CursorOf(2)));

        Assert.Empty(page.Items);
        Assert.Null(page.PageInfo.StartCursor);
        Assert.Null(page.PageInfo.EndCursor);
        Assert.False(page.PageInfo.HasNextPage);
        Assert.Equal(3, page.TotalCount);
    }

    [Fact]
    public void After_far_beyond_the_end_gives_an_empty_page()
    {
        var page = Slice(Numbers(3), new PageRequest(After: CursorOf(int.MaxValue)));

        Assert.Empty(page.Items);
    }

    [Fact]
    public void Empty_source_gives_an_empty_page()
    {
        var page = Slice([], new PageRequest(First: 3));

        Assert.Empty(page.Items);
        Assert.False(page.PageInfo.HasNextPage);
        Assert.False(page.PageInfo.HasPreviousPage);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public void Each_edge_carries_the_cursor_of_its_own_position()
    {
        var page = Slice(Numbers(6), new PageRequest(First: 2, After: CursorOf(1)));

        var edges = page.ToEdges((cursor, number) => (cursor, number));

        Assert.Equal([(CursorOf(2), 3), (CursorOf(3), 4)], edges);
        Assert.Equal(page.PageInfo.StartCursor, edges[0].cursor);
        Assert.Equal(page.PageInfo.EndCursor, edges[^1].cursor);
    }

    [Fact]
    public void Edges_for_backward_paging_use_the_position_in_the_full_list()
    {
        var page = Slice(Numbers(6), new PageRequest(Last: 2));

        var edges = page.ToEdges((cursor, number) => (cursor, number));

        Assert.Equal([(CursorOf(4), 5), (CursorOf(5), 6)], edges);
    }

    [Fact]
    public void First_zero_returns_no_items_but_reports_more_are_available()
    {
        var page = Slice(Numbers(3), new PageRequest(First: 0));

        Assert.Empty(page.Items);
        Assert.True(page.PageInfo.HasNextPage);
    }

    [Fact]
    public void Non_array_sources_are_copied_so_the_page_is_independent_of_the_source()
    {
        var source = new List<int>(Numbers(4));

        var page = Slice(source, new PageRequest(First: 2));
        source[0] = 99;

        Assert.Equal([1, 2], page.Items);
    }

    [Fact]
    public void Window_for_a_total_selects_only_the_requested_positions()
    {
        var window = Paginator.ComputeWindow(new PageRequest(First: 2, After: CursorOf(1)), Options, total: 6);

        Assert.Equal(new PageWindow(Start: 2, Count: 2), window);
    }

    [Fact]
    public void Window_beyond_the_total_is_empty()
    {
        var window = Paginator.ComputeWindow(new PageRequest(After: CursorOf(int.MaxValue)), Options, total: 3);

        Assert.Equal(0, window.Count);
    }

    [Fact]
    public void Window_rejects_invalid_arguments()
    {
        Assert.Throws<InvalidPaginationException>(
            () => Paginator.ComputeWindow(new PageRequest(First: 11), Options, total: 3));
    }

    [Fact]
    public void A_page_built_from_a_fetched_window_matches_slicing_the_whole_list()
    {
        var source = Numbers(11);
        var request = new PageRequest(Last: 3, Before: CursorOf(8));

        var window = Paginator.ComputeWindow(request, Options, source.Length);
        var fetched = source.Skip(window.Start).Take(window.Count).ToArray();

        Assert.Equal(Slice(source, request), Paginator.BuildPage(fetched, window.Start, source.Length), PageComparer.Instance);
    }

    private sealed class PageComparer : IEqualityComparer<Page<int>>
    {
        public static readonly PageComparer Instance = new();

        public bool Equals(Page<int>? x, Page<int>? y)
            => x is not null && y is not null
                && x.Items.SequenceEqual(y.Items)
                && x.PageInfo == y.PageInfo
                && x.TotalCount == y.TotalCount
                && x.Offset == y.Offset;

        public int GetHashCode(Page<int> obj) => obj.TotalCount;
    }

    [Theory]
    [InlineData(-1, null)]
    [InlineData(11, null)]
    [InlineData(null, -1)]
    [InlineData(null, 11)]
    public void Sizes_outside_the_allowed_range_are_rejected(int? first, int? last)
    {
        var exception = Assert.Throws<InvalidPaginationException>(
            () => Slice(Numbers(3), new PageRequest(First: first, Last: last)));

        Assert.Contains("between 0 and 10", exception.Message);
    }

    [Fact]
    public void Max_page_size_itself_is_allowed()
    {
        var page = Slice(Numbers(20), new PageRequest(First: 10));

        Assert.Equal(10, page.Items.Count);
    }

    [Theory]
    [InlineData("garbage", null)]
    [InlineData(null, "garbage")]
    public void Invalid_cursors_are_rejected(string? after, string? before)
    {
        var exception = Assert.Throws<InvalidPaginationException>(
            () => Slice(Numbers(3), new PageRequest(After: after, Before: before)));

        Assert.Contains(after is null ? "before" : "after", exception.Message);
    }
}
