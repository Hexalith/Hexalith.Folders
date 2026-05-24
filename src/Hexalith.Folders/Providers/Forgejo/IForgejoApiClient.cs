namespace Hexalith.Folders.Providers.Forgejo;

internal interface IForgejoApiClient
{
    Task<ForgejoReadinessResult> GetReadinessAsync(
        ForgejoReadinessRequest request,
        CancellationToken cancellationToken = default);
}
