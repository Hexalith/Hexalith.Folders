namespace Hexalith.Folders.Providers.Abstractions;

internal sealed class UnconfiguredProviderRepositoryTargetResolver : IProviderRepositoryTargetResolver
{
    public ValueTask<ProviderRepositoryTargetResolutionResult> ResolveCreationAsync(
        ProviderRepositoryCreationTargetResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(ProviderRepositoryTargetResolutionResult.Failure(
            ProviderFailureCategory.ProviderConfigurationMissing,
            "provider_repository_creation_target_unconfigured"));
    }

    public ValueTask<ProviderRepositoryTargetResolutionResult> ResolveBindingAsync(
        ProviderRepositoryBindingTargetResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(ProviderRepositoryTargetResolutionResult.Failure(
            ProviderFailureCategory.ProviderConfigurationMissing,
            "provider_repository_binding_target_unconfigured"));
    }
}
