using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Queries.ProviderReadiness;

public sealed class ProviderReadinessCapabilityAuthorizer : IProviderCapabilityAuthorizer
{
    public Task<ProviderCapabilityAuthorizationResult> AuthorizeAsync(
        ProviderCapabilityDiscoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.AuthorizationEvidence.Fingerprint))
        {
            return Task.FromResult(ProviderCapabilityAuthorizationResult.Denied(
                ProviderFailureCategory.ProviderPermissionInsufficient,
                "provider_readiness_authorization_missing"));
        }

        if (!string.Equals(request.AuthorizationEvidence.FreshnessClass, "fresh", StringComparison.Ordinal))
        {
            return Task.FromResult(ProviderCapabilityAuthorizationResult.Denied(
                ProviderFailureCategory.ReconciliationRequired,
                "authorization_evidence_stale"));
        }

        return Task.FromResult(ProviderCapabilityAuthorizationResult.Allowed(request.AuthorizationEvidence));
    }
}
