using System.Security.Cryptography;

using Hexalith.Folders.Providers.Shared;

namespace Hexalith.Folders.Providers.Forgejo;

internal sealed class ForgejoCanonicalEvidenceWriter(IncrementalHash hash) : IDisposable
{
    private readonly CanonicalEvidenceWriter _writer = new(hash);

    public void AppendString(string? value)
        => _writer.AppendString(value);

    public void AppendBytes(ReadOnlySpan<byte> value)
        => _writer.AppendBytes(value);

    public void AppendUInt32(uint value)
        => _writer.AppendUInt32(value);

    public void AppendCollectionCount(int count)
        => _writer.AppendCollectionCount(count);

    public void Dispose()
    {
    }
}
