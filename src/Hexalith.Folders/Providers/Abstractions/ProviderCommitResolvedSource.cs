namespace Hexalith.Folders.Providers.Abstractions;

internal sealed record ProviderCommitResolvedSource(
    ProviderGitOperationResolvedTarget Target,
    string TreeSha,
    string CommitMessage,
    IReadOnlyList<ProviderResolvedFileChange>? StagedChanges = null)
{
    public override string ToString() => nameof(ProviderCommitResolvedSource);
}
