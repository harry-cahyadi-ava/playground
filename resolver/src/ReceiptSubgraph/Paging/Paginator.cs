namespace ReceiptSubgraph.Paging;

/// <summary>
/// Applies Relay-style cursor pagination (first/after/last/before) to an in-memory, ordered list.
/// Cursors are positional offsets, so the source must use a stable ordering between requests.
/// </summary>
public static class Paginator
{
    public static Page<T> Slice<T>(IReadOnlyList<T> source, PageRequest? request, PaginationOptions options)
    {
        var first = Validate(request?.First, "first", options);
        var last = Validate(request?.Last, "last", options);

        // Without an explicit size, fall back to the default page size (unless paging backwards).
        if (first is null && last is null)
        {
            first = options.DefaultPageSize;
        }

        var total = source.Count;

        // The window is [start, end). Cursors narrow it, then first/last trim it, in the order Relay specifies.
        var start = 0;
        var end = total;

        if (request?.After is { } after)
        {
            // An "after" cursor at or beyond the end yields an empty window rather than an error.
            start = (int)Math.Min((long)DecodeCursor(after, "after") + 1, total);
        }

        if (request?.Before is { } before)
        {
            end = Math.Min(DecodeCursor(before, "before"), total);
        }

        end = Math.Max(start, end);

        if (first is { } firstCount)
        {
            end = Math.Min(end, start + firstCount);
        }

        if (last is { } lastCount)
        {
            start = Math.Max(start, end - lastCount);
        }

        var items = Copy(source, start, end - start);
        var pageInfo = new PageInfo(
            HasNextPage: end < total,
            HasPreviousPage: start > 0,
            StartCursor: items.Count > 0 ? CursorCodec.Encode(start) : null,
            EndCursor: items.Count > 0 ? CursorCodec.Encode(end - 1) : null);

        return new Page<T>(items, pageInfo, total, start);
    }

    private static int? Validate(int? size, string argument, PaginationOptions options)
    {
        if (size is null)
        {
            return null;
        }

        if (size < 0 || size > options.MaxPageSize)
        {
            throw new InvalidPaginationException(
                $"The argument '{argument}' must be between 0 and {options.MaxPageSize}.");
        }

        return size;
    }

    private static int DecodeCursor(string cursor, string argument)
        => CursorCodec.TryDecode(cursor, out var index)
            ? index
            : throw new InvalidPaginationException($"The cursor supplied for '{argument}' is not valid.");

    // Arrays are exposed as a zero-copy segment; any other list is copied once so the page is stable.
    private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source, int offset, int count)
    {
        if (count == 0)
        {
            return [];
        }

        if (source is T[] array)
        {
            return new ArraySegment<T>(array, offset, count);
        }

        var result = new T[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = source[offset + i];
        }

        return result;
    }
}
