namespace Hexalith.Folders.Providers.Forgejo;

internal interface IForgejoApiClient
{
    Task<ForgejoReadinessResult> GetReadinessAsync(
        ForgejoReadinessRequest request,
        CancellationToken cancellationToken = default);

    Task<ForgejoRepositoryCreationResult> CreateRepositoryAsync(
        ForgejoRepositoryCreationRequest request,
        CancellationToken cancellationToken = default);
}
