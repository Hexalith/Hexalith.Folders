using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Tests.Providers.GitHub;

/// <summary>
/// A target resolver that faults. Proves a resolver defect is mapped to a provider-neutral result
/// rather than escaping the port as an unmapped exception.
/// </summary>
internal sealed class ThrowingProviderRepositoryTargetResolver : IProviderRepositoryTargetResolver
{
    public ValueTask<ProviderRepositoryTargetResolutionResult> ResolveCreationAsync(
        ProviderRepositoryCreationTargetResolutionRequest request,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("owner-acme-repo-secret");

    public ValueTask<ProviderRepositoryTargetResolutionResult> ResolveBindingAsync(
        ProviderRepositoryBindingTargetResolutionRequest request,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("owner-acme-repo-secret");
}
