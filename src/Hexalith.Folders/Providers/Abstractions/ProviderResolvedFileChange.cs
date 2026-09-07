namespace Hexalith.Folders.Providers.Abstractions;

internal sealed record ProviderResolvedFileChange(
    int Sequence,
    ProviderFileChangeKind Kind,
    string Path,
    ReadOnlyMemory<byte> Content,
    ProviderFileContentType ContentType,
    string? SourceObjectId = null)
{
    public override string ToString() => nameof(ProviderResolvedFileChange);
}
