namespace Hexalith.Folders.Providers.Forgejo;

internal interface IForgejoApiClientFactory
{
    ValueTask<IForgejoApiClient> CreateAsync(
        ForgejoApiClientRequest request,
        ForgejoCredentialLease credential,
        CancellationToken cancellationToken = default);
}
