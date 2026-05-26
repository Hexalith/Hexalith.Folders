using Hexalith.Folders.Providers.GitHub;

namespace Hexalith.Folders.Tests.Providers.GitHub;

internal sealed class RecordingGitHubApiClient(
    GitHubReadinessResult result,
    GitHubRepositoryCreationResult? repositoryCreationResult = null,
    Exception? repositoryCreationException = null) : IGitHubApiClient
{
    public int ReadinessCalls { get; private set; }

    public int RepositoryCreationCalls { get; private set; }

    public GitHubReadinessRequest? LastRequest { get; private set; }

    public GitHubRepositoryCreationRequest? LastRepositoryCreationRequest { get; private set; }

    public static RecordingGitHubApiClient Success()
        => new(SuccessReadiness());

    private static GitHubReadinessResult SuccessReadiness()
        => GitHubReadinessResult.Success(
            new GitHubPermissionEvidence(
                SupportsRepositoryCreation: true,
                SupportsRepositoryBinding: true,
                SupportsBranchRefInspection: true,
                SupportsFileMutation: true,
                SupportsCommit: true,
                SupportsStatus: true,
                SupportsMetadata: true),
            new GitHubRateLimitEvidence("bounded", true, TimeSpan.FromSeconds(90)));

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

    public static RecordingGitHubApiClient RepositoryCreationFailure(GitHubApiFailureCondition condition)
        => new(
            SuccessReadiness(),
            GitHubRepositoryCreationResult.Failure(
                condition,
                condition is GitHubApiFailureCondition.PrimaryRateLimit or GitHubApiFailureCondition.SecondaryRateLimit
                    ? TimeSpan.FromSeconds(120)
                    : null));

    public static RecordingGitHubApiClient RepositoryCreationEquivalentExisting()
        => new(SuccessReadiness(), GitHubRepositoryCreationResult.Success(equivalentExisting: true));

    public static RecordingGitHubApiClient RepositoryCreationThrows(Exception exception)
        => new(SuccessReadiness(), repositoryCreationException: exception);

    public Task<GitHubReadinessResult> GetReadinessAsync(
        GitHubReadinessRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReadinessCalls++;
        LastRequest = request;
        return Task.FromResult(result);
    }

    public Task<GitHubRepositoryCreationResult> CreateRepositoryAsync(
        GitHubRepositoryCreationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RepositoryCreationCalls++;
        LastRepositoryCreationRequest = request;
        if (repositoryCreationException is not null)
        {
            throw repositoryCreationException;
        }

        return Task.FromResult(repositoryCreationResult ?? GitHubRepositoryCreationResult.Success());
    }
}
