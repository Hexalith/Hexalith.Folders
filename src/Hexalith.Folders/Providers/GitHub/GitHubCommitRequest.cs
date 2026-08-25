using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.GitHub;

internal sealed record GitHubCommitRequest(
    ProviderGitOperationResolvedTarget Target,
    string TreeSha,
    string CommitMessage,
    Func<CancellationToken, ValueTask<bool>> ValidateReservationAsync,
    Func<string, ValueTask<bool>> RecordCreatedCommitAsync)
{
    public override string ToString() => nameof(GitHubCommitRequest);
}
