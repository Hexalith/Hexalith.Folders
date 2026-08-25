namespace Hexalith.Folders.Providers.Abstractions;

internal sealed record ProviderFileMutationResolvedSource(
    ProviderGitOperationResolvedTarget Target,
    IReadOnlyList<ProviderResolvedFileChange> Changes)
{
    public override string ToString() => nameof(ProviderFileMutationResolvedSource);
}
