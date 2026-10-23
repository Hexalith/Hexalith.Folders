using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Hexalith.Folders.Providers.Shared;

/// <summary>Encodes canonical evidence with NFC strings, presence markers, and big-endian lengths.</summary>
/// <param name="hash">The externally owned destination hash.</param>
internal sealed class CanonicalEvidenceWriter(IncrementalHash hash)
{
    private readonly IncrementalHash _hash = hash ?? throw new ArgumentNullException(nameof(hash));

    /// <summary>Appends an NFC UTF-8 string, distinguishing absent and empty values.</summary>
    /// <param name="value">The field value.</param>
    internal void AppendString(string? value)
    {
        if (value is null)
        {
            Span<byte> marker = stackalloc byte[1];
            marker[0] = 0;
            _hash.AppendData(marker);
            return;
        }

        AppendBytes(Encoding.UTF8.GetBytes(value.Normalize(NormalizationForm.FormC)));
    }

    /// <summary>Appends a present field's length and bytes.</summary>
    /// <param name="value">The field bytes.</param>
    internal void AppendBytes(ReadOnlySpan<byte> value)
    {
        Span<byte> header = stackalloc byte[5];
        header[0] = 1;
        BinaryPrimitives.WriteUInt32BigEndian(header[1..], checked((uint)value.Length));
        _hash.AppendData(header);
        _hash.AppendData(value);
    }

    /// <summary>Appends a present boolean field.</summary>
    /// <param name="value">The boolean value.</param>
    internal void AppendBoolean(bool value)
    {
        Span<byte> bytes = stackalloc byte[1];
        bytes[0] = value ? (byte)1 : (byte)0;
        AppendBytes(bytes);
    }

    /// <summary>Appends a present unsigned 32-bit integer in big-endian order.</summary>
    /// <param name="value">The integer value.</param>
    internal void AppendUInt32(uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        AppendBytes(bytes);
    }

    /// <summary>Appends a present unsigned 64-bit integer in big-endian order.</summary>
    /// <param name="value">The integer value.</param>
    internal void AppendUInt64(ulong value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        AppendBytes(bytes);
    }

    /// <summary>Appends a collection count without a field presence marker.</summary>
    /// <param name="count">The nonnegative collection count.</param>
    internal void AppendCollectionCount(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, checked((uint)count));
        _hash.AppendData(bytes);
    }
}
