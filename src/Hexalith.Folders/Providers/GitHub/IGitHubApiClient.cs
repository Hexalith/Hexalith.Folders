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

    Task<GitHubFileMutationResult> StageFileChangesAsync(
        GitHubFileMutationRequest request,
        CancellationToken cancellationToken = default);

    Task<GitHubCommitResult> CommitAsync(
        GitHubCommitRequest request,
        CancellationToken cancellationToken = default);

    Task<GitHubOperationStatusResult> GetOperationStatusAsync(
        GitHubOperationStatusRequest request,
        CancellationToken cancellationToken = default);
}
