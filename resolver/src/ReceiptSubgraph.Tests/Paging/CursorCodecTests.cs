using ReceiptSubgraph.Paging;

namespace ReceiptSubgraph.Tests.Paging;

public class CursorCodecTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(int.MaxValue)]
    public void Encode_then_decode_round_trips(int index)
    {
        var cursor = CursorCodec.Encode(index);

        Assert.True(CursorCodec.TryDecode(cursor, out var decoded));
        Assert.Equal(index, decoded);
    }

    [Fact]
    public void Encoded_cursor_is_url_safe()
    {
        var cursor = CursorCodec.Encode(123456);

        Assert.DoesNotContain('+', cursor);
        Assert.DoesNotContain('/', cursor);
        Assert.DoesNotContain('=', cursor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a cursor!")]
    [InlineData("YWJj")] // "abc": valid Base64Url, but not our payload format
    [InlineData("bzotMQ")] // "o:-1": negative offsets are rejected
    [InlineData("bzphYmM")] // "o:abc": offset is not a number
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")] // longer than any valid cursor
    public void Decode_rejects_malformed_cursors(string? cursor)
    {
        Assert.False(CursorCodec.TryDecode(cursor, out _));
    }
}
