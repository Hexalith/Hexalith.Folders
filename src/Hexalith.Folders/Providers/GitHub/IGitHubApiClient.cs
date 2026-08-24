namespace Hexalith.Folders.Providers.GitHub;

internal interface IGitHubApiClient
{
    Task<GitHubReadinessResult> GetReadinessAsync(
        GitHubReadinessRequest request,
        CancellationToken cancellationToken = default);

    Task<GitHubRepositoryCreationResult> CreateRepositoryAsync(
        GitHubRepositoryCreationRequest request,
        CancellationToken cancellationToken = default);

    Task<GitHubRepositoryBindingResult> ValidateRepositoryBindingAsync(
        GitHubRepositoryBindingRequest request,
        CancellationToken cancellationToken = default);

    Task<GitHubFileChangeSetResult> StageFileChangesAsync(
        GitHubFileChangeSetRequest request,
        CancellationToken cancellationToken = default);

    Task<GitHubCommitResult> CommitAsync(
        GitHubCommitRequest request,
        CancellationToken cancellationToken = default);

    Task<GitHubMutationStatusResult> GetMutationStatusAsync(
        GitHubMutationStatusRequest request,
        CancellationToken cancellationToken = default);
}
