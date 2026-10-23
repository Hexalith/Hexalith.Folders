using System.Security.Cryptography;

using Hexalith.Folders.Providers.Shared;

namespace Hexalith.Folders.Providers.GitHub;

internal sealed class GitHubCanonicalEvidenceWriter(IncrementalHash hash) : IDisposable
{
    private readonly CanonicalEvidenceWriter _writer = new(hash);

    public void AppendString(string? value)
        => _writer.AppendString(value);

    public void AppendBytes(ReadOnlySpan<byte> value)
        => _writer.AppendBytes(value);

    public void AppendBoolean(bool value)
        => _writer.AppendBoolean(value);

    public void AppendUInt32(uint value)
        => _writer.AppendUInt32(value);

    public void AppendUInt64(ulong value)
        => _writer.AppendUInt64(value);

    public void AppendCollectionCount(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        _writer.AppendCollectionCount(count);
    }

    public void Dispose()
    {
    }
}
