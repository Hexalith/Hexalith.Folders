using Hexalith.Folders.Providers.GitHub;

namespace Hexalith.Folders.Tests.Providers.GitHub;

internal sealed class RecordingGitHubApiClient(
    GitHubReadinessResult result,
    GitHubRepositoryCreationResult? repositoryCreationResult = null,
    Exception? repositoryCreationException = null,
    GitHubRepositoryBindingResult? repositoryBindingResult = null,
    Exception? repositoryBindingException = null) : IGitHubApiClient
{
    public int ReadinessCalls { get; private set; }

    public int RepositoryCreationCalls { get; private set; }

    public int RepositoryBindingCalls { get; private set; }

    public GitHubReadinessRequest? LastRequest { get; private set; }

    public GitHubRepositoryCreationRequest? LastRepositoryCreationRequest { get; private set; }

    public GitHubRepositoryBindingRequest? LastRepositoryBindingRequest { get; private set; }

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

    public static RecordingGitHubApiClient RepositoryBindingFailure(GitHubApiFailureCondition condition)
        => new(
            SuccessReadiness(),
            repositoryBindingResult: GitHubRepositoryBindingResult.Failure(
                condition,
                condition is GitHubApiFailureCondition.PrimaryRateLimit or GitHubApiFailureCondition.SecondaryRateLimit
                    ? TimeSpan.FromSeconds(120)
                    : null));

    public static RecordingGitHubApiClient RepositoryBindingEquivalentExisting()
        => new(SuccessReadiness(), repositoryBindingResult: GitHubRepositoryBindingResult.Success(equivalentExisting: true));

    public static RecordingGitHubApiClient RepositoryBindingThrows(Exception exception)
        => new(SuccessReadiness(), repositoryBindingException: exception);

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

    public Task<GitHubRepositoryBindingResult> ValidateRepositoryBindingAsync(
        GitHubRepositoryBindingRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RepositoryBindingCalls++;
        LastRepositoryBindingRequest = request;
        if (repositoryBindingException is not null)
        {
            throw repositoryBindingException;
        }

        return Task.FromResult(repositoryBindingResult ?? GitHubRepositoryBindingResult.Success());
    }
}
