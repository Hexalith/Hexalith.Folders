namespace Hexalith.Folders.Providers.Forgejo;

internal interface IForgejoApiClient : IAsyncDisposable
{
    Task<ForgejoReadinessResult> GetReadinessAsync(
        ForgejoReadinessRequest request,
        CancellationToken cancellationToken = default);

    Task<ForgejoRepositoryCreationResult> CreateRepositoryAsync(
        ForgejoRepositoryCreationRequest request,
        CancellationToken cancellationToken = default);

    Task<ForgejoRepositoryBindingResult> ValidateRepositoryBindingAsync(
        ForgejoRepositoryBindingRequest request,
        CancellationToken cancellationToken = default);

    Task<ForgejoFileMutationResult> StageFileChangesAsync(
        ForgejoFileMutationRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ForgejoFileMutationResult.Failure(ForgejoApiFailureCondition.UnsupportedCapability));

    Task<ForgejoCommitResult> CommitAsync(
        ForgejoCommitRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ForgejoCommitResult.Failure(ForgejoApiFailureCondition.UnsupportedCapability));

    Task<ForgejoOperationStatusResult> GetOperationStatusAsync(
        ForgejoOperationStatusRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ForgejoOperationStatusResult.Failure(ForgejoApiFailureCondition.UnsupportedCapability));
}
