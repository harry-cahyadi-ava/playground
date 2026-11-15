using System.Buffers;
using System.Buffers.Text;
using System.Globalization;

namespace ReceiptSubgraph.Paging;

/// <summary>
/// Encodes the position of an item as an opaque, URL-safe cursor ("o:{index}" in Base64Url).
/// Works on spans and stack memory so that encoding and decoding do not allocate intermediate strings.
/// </summary>
internal static class CursorCodec
{
    // Longest payload is "o:" + int.MinValue-style 11 chars = 13 bytes; 32 leaves ample headroom.
    private const int MaxPayloadBytes = 32;

    public static string Encode(int index)
    {
        Span<byte> payload = stackalloc byte[MaxPayloadBytes];
        "o:"u8.CopyTo(payload);
        index.TryFormat(payload[2..], out var written, default, CultureInfo.InvariantCulture);
        return Base64Url.EncodeToString(payload[..(2 + written)]);
    }

    public static bool TryDecode(string? cursor, out int index)
    {
        index = 0;

        if (string.IsNullOrEmpty(cursor) || Base64Url.GetMaxDecodedLength(cursor.Length) > MaxPayloadBytes)
        {
            return false;
        }

        // Cursors come from clients, so malformed input must be reported as a status, never thrown.
        Span<byte> payload = stackalloc byte[MaxPayloadBytes];
        if (Base64Url.DecodeFromChars(cursor, payload, out _, out var written) != OperationStatus.Done
            || !payload[..written].StartsWith("o:"u8))
        {
            return false;
        }

        return int.TryParse(payload[2..written], CultureInfo.InvariantCulture, out index) && index >= 0;
    }
}
