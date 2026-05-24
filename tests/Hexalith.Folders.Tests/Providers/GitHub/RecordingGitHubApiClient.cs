using Hexalith.Folders.Providers.GitHub;

namespace Hexalith.Folders.Tests.Providers.GitHub;

internal sealed class RecordingGitHubApiClient(GitHubReadinessResult result) : IGitHubApiClient
{
    public int ReadinessCalls { get; private set; }

    public GitHubReadinessRequest? LastRequest { get; private set; }

    public static RecordingGitHubApiClient Success()
        => new(GitHubReadinessResult.Success(
            new GitHubPermissionEvidence(
                SupportsRepositoryCreation: true,
                SupportsRepositoryBinding: true,
                SupportsBranchRefInspection: true,
                SupportsFileMutation: true,
                SupportsCommit: true,
                SupportsStatus: true,
                SupportsMetadata: true),
            new GitHubRateLimitEvidence("bounded", true, TimeSpan.FromSeconds(90))));

    public static RecordingGitHubApiClient Success(
        GitHubPermissionEvidence permissions,
        GitHubRateLimitEvidence? rateLimit = null)
        => new(GitHubReadinessResult.Success(
            permissions,
            rateLimit ?? new GitHubRateLimitEvidence("bounded", true, TimeSpan.FromSeconds(90))));

    public static RecordingGitHubApiClient Failure(GitHubApiFailureCondition condition)
        => new(GitHubReadinessResult.Failure(
            condition,
            condition is GitHubApiFailureCondition.PrimaryRateLimit or GitHubApiFailureCondition.SecondaryRateLimit
                ? TimeSpan.FromSeconds(120)
                : null));

    public Task<GitHubReadinessResult> GetReadinessAsync(
        GitHubReadinessRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReadinessCalls++;
        LastRequest = request;
        return Task.FromResult(result);
    }
}
