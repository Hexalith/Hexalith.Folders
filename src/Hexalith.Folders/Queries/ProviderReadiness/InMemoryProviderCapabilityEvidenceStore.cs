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

        _attempts.Enqueue(Sanitize(request with { AuthorizationEvidence = authorizationEvidence }));
        return Task.CompletedTask;
    }

    private static ProviderCapabilityDiscoveryRequest Sanitize(ProviderCapabilityDiscoveryRequest request)
    {
        IReadOnlyDictionary<string, string> metadata = request.TargetEvidence.Metadata
            .Where(static pair => IsSafeMetadata(pair.Key) && IsSafeMetadata(pair.Value))
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);

        return request with
        {
            TargetEvidence = request.TargetEvidence with
            {
                Product = SafeMetadataOrRedacted(request.TargetEvidence.Product),
                ProductVersion = SafeMetadataOrRedacted(request.TargetEvidence.ProductVersion),
                ApiSurfaceVersion = SafeMetadataOrRedacted(request.TargetEvidence.ApiSurfaceVersion),
                EvidenceVersion = SafeMetadataOrRedacted(request.TargetEvidence.EvidenceVersion),
                Metadata = metadata,
            },
        };
    }

    private static string SafeMetadataOrRedacted(string value)
        => IsSafeMetadata(value) ? value : "metadata_redacted";

    private static bool IsSafeMetadata(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string canonical = value.Trim().ToLowerInvariant();
        return !canonical.Contains("://", StringComparison.Ordinal)
            && !canonical.Contains("@", StringComparison.Ordinal)
            && !canonical.Contains("token", StringComparison.Ordinal)
            && !canonical.Contains("secret", StringComparison.Ordinal)
            && !canonical.Contains("password", StringComparison.Ordinal)
            && !canonical.Contains("privatekey", StringComparison.Ordinal)
            && !canonical.Contains("private key", StringComparison.Ordinal)
            && !canonical.Contains("diff --git", StringComparison.Ordinal)
            && !canonical.Contains("providerpayload", StringComparison.Ordinal);
    }
}
