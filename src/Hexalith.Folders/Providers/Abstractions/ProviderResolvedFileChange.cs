namespace Hexalith.Folders.Providers.Abstractions;

internal sealed record ProviderResolvedFileChange(
    int Sequence,
    ProviderFileChangeKind Kind,
    string Path,
    ReadOnlyMemory<byte> Content,
    ProviderFileContentType ContentType)
{
    public override string ToString() => nameof(ProviderResolvedFileChange);
}
