using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Hexalith.Folders.Providers.Forgejo;

internal sealed class ForgejoCanonicalEvidenceWriter(IncrementalHash hash) : IDisposable
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

    public void AppendBytes(ReadOnlySpan<byte> value) => AppendPresent(value);

    public void AppendUInt32(uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        AppendPresent(bytes);
    }

    public void AppendCollectionCount(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
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
