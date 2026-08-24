using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.GitHub;

/// <summary>
/// Gives dependency injection a distinct implementation type for enumerable provider registration.
/// </summary>
internal sealed class GitHubProviderServiceRegistration : IGitProvider
{
    private readonly GitHubProvider _provider;

    public GitHubProviderServiceRegistration(
        IGitHubCredentialResolver credentialResolver,
        IProviderRepositoryTargetResolver targetResolver,
        IProviderGitOperationResolver gitOperationResolver)
    {
        _provider = new GitHubProvider(
            credentialResolver,
            new OctokitGitHubApiClientFactory(),
            targetResolver,
            gitOperationResolver);
    }

    public string ProviderFamily => _provider.ProviderFamily;

    public string ProviderKey => _provider.ProviderKey;

    public Task<ProviderCapabilityDiscoveryResult> DiscoverCapabilitiesAsync(
        ProviderCapabilityDiscoveryRequest request,
        CancellationToken cancellationToken = default)
        => _provider.DiscoverCapabilitiesAsync(request, cancellationToken);

    public Task<ProviderRepositoryCreationResult> CreateRepositoryAsync(
        ProviderRepositoryCreationRequest request,
        CancellationToken cancellationToken = default)
        => _provider.CreateRepositoryAsync(request, cancellationToken);

    public Task<ProviderRepositoryBindingResult> ValidateRepositoryBindingAsync(
        ProviderRepositoryBindingRequest request,
        CancellationToken cancellationToken = default)
        => _provider.ValidateRepositoryBindingAsync(request, cancellationToken);

    public Task<ProviderFileChangeSetResult> StageFileChangesAsync(
        ProviderFileChangeSetRequest request,
        CancellationToken cancellationToken = default)
        => _provider.StageFileChangesAsync(request, cancellationToken);

    public Task<ProviderCommitResult> CommitAsync(
        ProviderCommitRequest request,
        CancellationToken cancellationToken = default)
        => _provider.CommitAsync(request, cancellationToken);

    public Task<ProviderMutationStatusResult> GetMutationStatusAsync(
        ProviderMutationStatusRequest request,
        CancellationToken cancellationToken = default)
        => _provider.GetMutationStatusAsync(request, cancellationToken);

    public ProviderCapabilityComparisonResult CompareCapabilityProfiles(
        ProviderCapabilityProfile current,
        ProviderCapabilityProfile candidate)
        => _provider.CompareCapabilityProfiles(current, candidate);
}
