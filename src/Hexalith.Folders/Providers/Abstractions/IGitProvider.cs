namespace Hexalith.Folders.Providers.Abstractions;

public interface IGitProvider
{
    string ProviderFamily { get; }

    string ProviderKey { get; }

    Task<ProviderCapabilityDiscoveryResult> DiscoverCapabilitiesAsync(
        ProviderCapabilityDiscoveryRequest request,
        CancellationToken cancellationToken = default);

    Task<ProviderRepositoryCreationResult> CreateRepositoryAsync(
        ProviderRepositoryCreationRequest request,
        CancellationToken cancellationToken = default);

    Task<ProviderRepositoryBindingResult> ValidateRepositoryBindingAsync(
        ProviderRepositoryBindingRequest request,
        CancellationToken cancellationToken = default);

    Task<ProviderFileChangeSetResult> StageFileChangesAsync(
        ProviderFileChangeSetRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ProviderFileChangeSetResult.Failure(
                request,
                ProviderFailureCategory.UnsupportedProviderCapability,
                "provider_file_mutation_unsupported"));
    }

    Task<ProviderCommitResult> CommitAsync(
        ProviderCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ProviderCommitResult.Failure(
                request,
                ProviderFailureCategory.UnsupportedProviderCapability,
                "provider_commit_unsupported"));
    }

    Task<ProviderMutationStatusResult> GetMutationStatusAsync(
        ProviderMutationStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ProviderMutationStatusResult.Unavailable(
                request,
                ProviderFailureCategory.UnsupportedProviderCapability,
                "provider_status_unsupported"));
    }

    ProviderCapabilityComparisonResult CompareCapabilityProfiles(
        ProviderCapabilityProfile current,
        ProviderCapabilityProfile candidate);
}
