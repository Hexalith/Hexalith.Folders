namespace Hexalith.Folders.Providers.Abstractions;

internal interface IProviderRepositoryTargetResolver
{
    ValueTask<ProviderRepositoryTargetResolutionResult> ResolveCreationAsync(
        ProviderRepositoryCreationTargetResolutionRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<ProviderRepositoryTargetResolutionResult> ResolveBindingAsync(
        ProviderRepositoryBindingTargetResolutionRequest request,
        CancellationToken cancellationToken = default);
}
