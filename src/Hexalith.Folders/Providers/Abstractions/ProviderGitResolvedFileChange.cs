namespace Hexalith.Folders.Providers.Abstractions;

internal sealed record ProviderGitResolvedFileChange(
    string OperationReference,
    string Path,
    ProviderFileChangeKind Kind,
    byte[]? Content)
{
    public override string ToString() => nameof(ProviderGitResolvedFileChange);
}
