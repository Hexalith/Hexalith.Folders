using Hexalith.Folders.Providers.GitHub;

namespace Hexalith.Folders.Tests.Providers.GitHub;

internal sealed class RecordingGitHubApiClient(
    GitHubReadinessResult result,
    GitHubRepositoryCreationResult? repositoryCreationResult = null,
    Exception? repositoryCreationException = null,
    GitHubRepositoryBindingResult? repositoryBindingResult = null,
    Exception? repositoryBindingException = null,
    GitHubFileChangeSetResult? fileChangeSetResult = null,
    Exception? fileChangeSetException = null,
    GitHubCommitResult? commitResult = null,
    Exception? commitException = null,
    GitHubMutationStatusResult? mutationStatusResult = null,
    Exception? mutationStatusException = null) : IGitHubApiClient
{
    public int ReadinessCalls { get; private set; }

    public int RepositoryCreationCalls { get; private set; }

    public int RepositoryBindingCalls { get; private set; }

    public int FileChangeSetCalls { get; private set; }

    public int CommitCalls { get; private set; }

    public int MutationStatusCalls { get; private set; }

    public GitHubReadinessRequest? LastRequest { get; private set; }

    public GitHubRepositoryCreationRequest? LastRepositoryCreationRequest { get; private set; }

    public GitHubRepositoryBindingRequest? LastRepositoryBindingRequest { get; private set; }

    public GitHubFileChangeSetRequest? LastFileChangeSetRequest { get; private set; }

    public GitHubCommitRequest? LastCommitRequest { get; private set; }

    public GitHubMutationStatusRequest? LastMutationStatusRequest { get; private set; }

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

    public static RecordingGitHubApiClient FileChangeSetFailure(GitHubApiFailureCondition condition)
        => new(SuccessReadiness(), fileChangeSetResult: GitHubFileChangeSetResult.Failure(condition));

    public static RecordingGitHubApiClient CommitFailure(GitHubApiFailureCondition condition)
        => new(SuccessReadiness(), commitResult: GitHubCommitResult.Failure(condition));

    public static RecordingGitHubApiClient CommitFailure(
        GitHubApiFailureCondition condition,
        string commitSha)
        => new(SuccessReadiness(), commitResult: GitHubCommitResult.Failure(condition, commitSha: commitSha));

    public static RecordingGitHubApiClient FileChangeSetThrows(Exception exception)
        => new(SuccessReadiness(), fileChangeSetException: exception);

    public static RecordingGitHubApiClient CommitThrows(Exception exception)
        => new(SuccessReadiness(), commitException: exception);

    public static RecordingGitHubApiClient MutationStatus(GitHubMutationStatusDisposition disposition)
        => new(SuccessReadiness(), mutationStatusResult: GitHubMutationStatusResult.Available(disposition));

    public static RecordingGitHubApiClient MutationStatusFailure(
        GitHubApiFailureCondition condition,
        TimeSpan? retryAfter = null)
        => new(SuccessReadiness(), mutationStatusResult: GitHubMutationStatusResult.Unavailable(condition, retryAfter));

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

    public Task<GitHubFileChangeSetResult> StageFileChangesAsync(
        GitHubFileChangeSetRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FileChangeSetCalls++;
        LastFileChangeSetRequest = request;
        if (fileChangeSetException is not null)
        {
            throw fileChangeSetException;
        }

        return Task.FromResult(fileChangeSetResult ?? GitHubFileChangeSetResult.Success("2222222222222222222222222222222222222222"));
    }

    public Task<GitHubCommitResult> CommitAsync(
        GitHubCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommitCalls++;
        LastCommitRequest = request;
        if (commitException is not null)
        {
            throw commitException;
        }

        return Task.FromResult(commitResult ?? GitHubCommitResult.Success("3333333333333333333333333333333333333333"));
    }

    public Task<GitHubMutationStatusResult> GetMutationStatusAsync(
        GitHubMutationStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        MutationStatusCalls++;
        LastMutationStatusRequest = request;
        if (mutationStatusException is not null)
        {
            throw mutationStatusException;
        }

        return Task.FromResult(mutationStatusResult ?? GitHubMutationStatusResult.Available(GitHubMutationStatusDisposition.Confirmed));
    }
}
