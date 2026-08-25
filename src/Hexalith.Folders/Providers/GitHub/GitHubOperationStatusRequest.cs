using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.GitHub;

internal sealed record GitHubOperationStatusRequest(
    ProviderGitOperationResolvedTarget Target,
    string IntendedCommitSha)
{
    public override string ToString() => nameof(GitHubOperationStatusRequest);
}
