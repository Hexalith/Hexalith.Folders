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
}
