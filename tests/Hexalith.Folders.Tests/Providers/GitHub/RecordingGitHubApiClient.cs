using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Providers.GitHub;

namespace Hexalith.Folders.Tests.Providers.GitHub;

internal sealed class RecordingGitHubApiClient(
    GitHubReadinessResult result,
    GitHubRepositoryCreationResult? repositoryCreationResult = null,
    Exception? repositoryCreationException = null,
    GitHubRepositoryBindingResult? repositoryBindingResult = null,
    Exception? repositoryBindingException = null,
    GitHubFileMutationResult? fileMutationResult = null,
    Exception? fileMutationException = null,
    GitHubCommitResult? commitResult = null,
    Exception? commitException = null,
    GitHubOperationStatusResult? statusResult = null,
    Exception? statusException = null) : IGitHubApiClient
{
    private const string ObjectId = "3333333333333333333333333333333333333333";

    public int ReadinessCalls { get; private set; }

    public int RepositoryCreationCalls { get; private set; }

    public int RepositoryBindingCalls { get; private set; }

    public int FileMutationCalls { get; private set; }

    public int CommitCalls { get; private set; }

    public int StatusCalls { get; private set; }

    public GitHubReadinessRequest? LastRequest { get; private set; }

    public GitHubRepositoryCreationRequest? LastRepositoryCreationRequest { get; private set; }

    public GitHubRepositoryBindingRequest? LastRepositoryBindingRequest { get; private set; }

    public GitHubFileMutationRequest? LastFileMutationRequest { get; private set; }

    public GitHubCommitRequest? LastCommitRequest { get; private set; }

    public GitHubOperationStatusRequest? LastStatusRequest { get; private set; }

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

    public static RecordingGitHubApiClient FileMutationFailure(GitHubApiFailureCondition condition)
        => new(SuccessReadiness(), fileMutationResult: GitHubFileMutationResult.Failure(condition));

    public static RecordingGitHubApiClient FileMutationThrows(Exception exception)
        => new(SuccessReadiness(), fileMutationException: exception);

    public static RecordingGitHubApiClient MalformedOperationSuccesses()
        => new(
            SuccessReadiness(),
            fileMutationResult: new GitHubFileMutationResult(true, default, null, "not-a-sha"),
            commitResult: new GitHubCommitResult(true, default, null, "not-a-sha", "not-a-sha"),
            statusResult: new GitHubOperationStatusResult(true, (ProviderOperationStatusKind)999, default, null, "not-a-sha", "refs/heads/main", "commit"));

    public static RecordingGitHubApiClient CommitFailure(GitHubApiFailureCondition condition)
        => new(SuccessReadiness(), commitResult: GitHubCommitResult.Failure(condition));

    public static RecordingGitHubApiClient CommitThrows(Exception exception)
        => new(SuccessReadiness(), commitException: exception);

    public static RecordingGitHubApiClient Status(ProviderOperationStatusKind status, string? observedSha = null)
        => new(
            SuccessReadiness(),
            statusResult: GitHubOperationStatusResult.Observed(
                status,
                observedSha ?? status switch
                {
                    ProviderOperationStatusKind.Confirmed => RecordingProviderOperationSourceResolver.CommitSha,
                    ProviderOperationStatusKind.NotApplied => RecordingProviderOperationSourceResolver.HeadSha,
                    _ => RecordingProviderOperationSourceResolver.TreeSha,
                }));

    public static RecordingGitHubApiClient StatusFailure(GitHubApiFailureCondition condition)
        => new(SuccessReadiness(), statusResult: GitHubOperationStatusResult.Failure(condition));

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

    public async Task<GitHubFileMutationResult> StageFileChangesAsync(
        GitHubFileMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FileMutationCalls++;
        LastFileMutationRequest = request;
        if (fileMutationException is not null)
        {
            throw fileMutationException;
        }

        if (!await request.ValidateReservationAsync(cancellationToken).ConfigureAwait(false))
        {
            return GitHubFileMutationResult.Failure(GitHubApiFailureCondition.ReservationInvalidated);
        }

        return fileMutationResult ?? GitHubFileMutationResult.Success(ObjectId);
    }

    public async Task<GitHubCommitResult> CommitAsync(
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

        if (!await request.ValidateReservationAsync(cancellationToken).ConfigureAwait(false))
        {
            return GitHubCommitResult.Failure(GitHubApiFailureCondition.ReservationInvalidated);
        }

        GitHubCommitResult effectiveResult = commitResult ?? GitHubCommitResult.Success(ObjectId);
        string? createdCommitSha = effectiveResult.CreatedCommitSha;
        if (ProviderGitOperationResolvedTarget.IsGitObjectId(createdCommitSha)
            && !await request.RecordCreatedCommitAsync(createdCommitSha!).ConfigureAwait(false))
        {
            return GitHubCommitResult.Failure(GitHubApiFailureCondition.OutcomeRecordingFailed, createdCommitSha: createdCommitSha);
        }

        return effectiveResult;
    }

    public Task<GitHubOperationStatusResult> GetOperationStatusAsync(
        GitHubOperationStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StatusCalls++;
        LastStatusRequest = request;
        if (statusException is not null)
        {
            throw statusException;
        }

        return Task.FromResult(statusResult
            ?? GitHubOperationStatusResult.Observed(ProviderOperationStatusKind.Confirmed, ObjectId));
    }
}
