namespace Hexalith.Folders.Providers.Abstractions;

internal sealed record ProviderGitCommitResolvedInput(
    ProviderRepositoryResolvedTarget Target,
    string ExpectedHeadSha,
    string StagedTreeSha,
    string CommitMessage)
{
    public override string ToString() => nameof(ProviderGitCommitResolvedInput);
}
