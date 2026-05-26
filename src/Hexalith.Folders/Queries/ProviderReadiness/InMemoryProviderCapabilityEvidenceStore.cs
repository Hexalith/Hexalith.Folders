using System.Collections.Concurrent;

using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Queries.ProviderReadiness;

public sealed class InMemoryProviderCapabilityEvidenceStore : IProviderCapabilityEvidenceStore
{
    private readonly ConcurrentQueue<ProviderCapabilityDiscoveryRequest> _attempts = new();

    public IReadOnlyList<ProviderCapabilityDiscoveryRequest> Attempts => _attempts.ToArray();

    public Task RecordAttemptAsync(
        ProviderCapabilityDiscoveryRequest request,
        ProviderAuthorizationEvidenceSnapshot authorizationEvidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(authorizationEvidence);
        cancellationToken.ThrowIfCancellationRequested();

        _attempts.Enqueue(request with { AuthorizationEvidence = authorizationEvidence });
        return Task.CompletedTask;
    }
}
