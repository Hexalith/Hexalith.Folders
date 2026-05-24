using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.Forgejo;

internal sealed class UnconfiguredForgejoCredentialResolver : IForgejoCredentialResolver
{
    public ValueTask<ForgejoCredentialResolutionResult> ResolveAsync(
        ForgejoCredentialResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(ForgejoCredentialResolutionResult.Failure(
            ProviderFailureCategory.ProviderConfigurationMissing,
            "forgejo_credential_resolver_unconfigured"));
    }
}
