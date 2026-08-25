using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.GitHub;

internal sealed record GitHubFileMutationRequest(
    ProviderGitOperationResolvedTarget Target,
    IReadOnlyList<ProviderResolvedFileChange> Changes)
{
    public override string ToString() => nameof(GitHubFileMutationRequest);
}
