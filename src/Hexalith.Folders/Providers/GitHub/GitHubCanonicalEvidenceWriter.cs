using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Hexalith.Folders.Providers.GitHub;

internal sealed class GitHubCanonicalEvidenceWriter(IncrementalHash hash) : IDisposable
{
    private readonly IncrementalHash _hash = hash ?? throw new ArgumentNullException(nameof(hash));

    public void AppendString(string? value)
    {
        if (value is null)
        {
            AppendAbsent();
            return;
        }

        AppendPresent(Encoding.UTF8.GetBytes(value.Normalize(NormalizationForm.FormC)));
    }

    public void AppendBytes(ReadOnlySpan<byte> value)
        => AppendPresent(value);

    public void AppendBoolean(bool value)
    {
        Span<byte> bytes = stackalloc byte[1];
        bytes[0] = value ? (byte)1 : (byte)0;
        AppendPresent(bytes);
    }

    public void AppendUInt32(uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        AppendPresent(bytes);
    }

    public void AppendUInt64(ulong value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        AppendPresent(bytes);
    }

    public void AppendCollectionCount(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, checked((uint)count));
        _hash.AppendData(bytes);
    }

    public void Dispose()
    {
    }

    private void AppendAbsent()
    {
        Span<byte> marker = stackalloc byte[1];
        marker[0] = 0;
        _hash.AppendData(marker);
    }

    private void AppendPresent(ReadOnlySpan<byte> bytes)
    {
        Span<byte> header = stackalloc byte[5];
        header[0] = 1;
        BinaryPrimitives.WriteUInt32BigEndian(header[1..], checked((uint)bytes.Length));
        _hash.AppendData(header);
        _hash.AppendData(bytes);
    }
}
