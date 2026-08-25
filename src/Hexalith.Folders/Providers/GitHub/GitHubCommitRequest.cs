using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.GitHub;

internal sealed record GitHubCommitRequest(
    ProviderGitOperationResolvedTarget Target,
    string TreeSha,
    string CommitMessage)
{
    public override string ToString() => nameof(GitHubCommitRequest);
}
