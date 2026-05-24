namespace Hexalith.Folders.Providers.Abstractions;

public interface IProviderCapabilityEvidenceStore
{
    Task RecordAttemptAsync(
        ProviderCapabilityDiscoveryRequest request,
        ProviderAuthorizationEvidenceSnapshot authorizationEvidence,
        CancellationToken cancellationToken = default);
}
