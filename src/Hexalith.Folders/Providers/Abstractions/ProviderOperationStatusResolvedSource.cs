namespace Hexalith.Folders.Providers.Abstractions;

internal sealed record ProviderOperationStatusResolvedSource(
    ProviderGitOperationResolvedTarget Target,
    string? IntendedCommitSha,
    IReadOnlyList<ProviderResolvedFileChange>? StagedChanges = null,
    string? CommitMessage = null)
{
    public override string ToString() => nameof(ProviderOperationStatusResolvedSource);
}
