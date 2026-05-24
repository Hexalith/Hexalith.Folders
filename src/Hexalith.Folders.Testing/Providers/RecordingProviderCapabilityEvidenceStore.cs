using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Testing.Providers;

public sealed class RecordingProviderCapabilityEvidenceStore : IProviderCapabilityEvidenceStore
{
    public int Calls { get; private set; }

    public Task RecordAttemptAsync(
        ProviderCapabilityDiscoveryRequest request,
        ProviderAuthorizationEvidenceSnapshot authorizationEvidence,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        return Task.CompletedTask;
    }
}
