using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Hexalith.Folders.Hashing;

/// <summary>Appends legacy UTF-8 fields with little-endian lengths and no Unicode normalization.</summary>
internal static class LengthPrefixedHash
{
    /// <summary>Appends a string, treating null as the empty string.</summary>
    /// <param name="hash">The destination hash.</param>
    /// <param name="value">The field value.</param>
    internal static void AppendField(IncrementalHash hash, string? value)
    {
        ReadOnlySpan<byte> bytes = value is null
            ? ReadOnlySpan<byte>.Empty
            : Encoding.UTF8.GetBytes(value);
        AppendInt32(hash, bytes.Length);
        hash.AppendData(bytes);
    }

    /// <summary>Appends a signed 32-bit integer in little-endian order.</summary>
    /// <param name="hash">The destination hash.</param>
    /// <param name="value">The integer value.</param>
    internal static void AppendInt32(IncrementalHash hash, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        hash.AppendData(buffer);
    }
}
