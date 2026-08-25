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

    Task<ProviderFileMutationResult> StageFileChangesAsync(
        ProviderFileMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ProviderFileMutationResult(
            IsSuccess: false,
            EquivalentReplay: false,
            ProviderFailureCategory.UnsupportedProviderCapability,
            ProviderFailureCategory.UnsupportedProviderCapability.ToCategoryCode(),
            "file_mutation_unsupported",
            "unsupported_provider_capability_remediation",
            Retryable: false,
            RetryAfter: null,
            request.CorrelationId,
            SafeTargetFingerprint: null,
            SafeOutcomeFingerprint: null,
            OpaqueOperationReference: null,
            ReconciliationReference: null));
    }

    Task<ProviderCommitResult> CommitAsync(
        ProviderCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ProviderCommitResult(
            IsSuccess: false,
            EquivalentReplay: false,
            ProviderFailureCategory.UnsupportedProviderCapability,
            ProviderFailureCategory.UnsupportedProviderCapability.ToCategoryCode(),
            "commit_unsupported",
            "unsupported_provider_capability_remediation",
            Retryable: false,
            RetryAfter: null,
            request.CorrelationId,
            SafeTargetFingerprint: null,
            SafeCommitFingerprint: null,
            OpaqueOperationReference: null,
            ReconciliationReference: null));
    }

    Task<ProviderOperationStatusResult> GetOperationStatusAsync(
        ProviderOperationStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ProviderOperationStatusResult(
            IsSuccess: false,
            ProviderOperationStatusKind.Unavailable,
            ProviderFailureCategory.UnsupportedProviderCapability,
            ProviderFailureCategory.UnsupportedProviderCapability.ToCategoryCode(),
            "status_query_unsupported",
            "unsupported_provider_capability_remediation",
            Retryable: false,
            RetryAfter: null,
            request.CorrelationId,
            request.CheckNumber,
            SafeObservedFingerprint: null,
            ReconciliationReference: null));
    }

    ProviderCapabilityComparisonResult CompareCapabilityProfiles(
        ProviderCapabilityProfile current,
        ProviderCapabilityProfile candidate);
}
