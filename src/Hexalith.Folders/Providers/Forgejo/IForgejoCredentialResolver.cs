namespace Hexalith.Folders.Providers.Forgejo;

internal interface IForgejoCredentialResolver
{
    ValueTask<ForgejoCredentialResolutionResult> ResolveAsync(
        ForgejoCredentialResolutionRequest request,
        CancellationToken cancellationToken = default);
}
