namespace Hexalith.Folders.Providers.Abstractions;

public sealed class ProviderCapabilityDiscoveryService(
    IProviderCapabilityAuthorizer authorizer,
    IProviderCapabilityResolver resolver,
    IProviderCapabilityEvidenceStore evidenceStore)
{
    private readonly IProviderCapabilityAuthorizer _authorizer = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
    private readonly IProviderCapabilityResolver _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    private readonly IProviderCapabilityEvidenceStore _evidenceStore = evidenceStore ?? throw new ArgumentNullException(nameof(evidenceStore));

    public async Task<ProviderCapabilityDiscoveryResult> DiscoverCapabilitiesAsync(
        ProviderCapabilityDiscoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        ProviderCapabilityAuthorizationResult authorization = await _authorizer.AuthorizeAsync(request, cancellationToken).ConfigureAwait(false);
        if (!authorization.IsAllowed)
        {
            return ProviderCapabilityDiscoveryResult.Failure(
                authorization.FailureCategory,
                authorization.ReasonCode,
                request.CorrelationId);
        }

        ProviderAuthorizationEvidenceSnapshot snapshot = authorization.Snapshot!;
        ProviderCapabilityDiscoveryRequest authorizedRequest = request with { AuthorizationEvidence = snapshot };

        await _evidenceStore.RecordAttemptAsync(authorizedRequest, snapshot, cancellationToken).ConfigureAwait(false);

        string providerFamily = ProviderIdentityIdentifier.Normalize(authorizedRequest.ProviderFamily);
        string providerKey = ProviderIdentityIdentifier.Normalize(authorizedRequest.ProviderKey);
        IGitProvider? provider = await _resolver.ResolveAsync(providerFamily, providerKey, cancellationToken).ConfigureAwait(false);
        if (provider is null)
        {
            return ProviderCapabilityDiscoveryResult.Failure(
                ProviderFailureCategory.UnsupportedProviderCapability,
                "unsupported_provider_family",
                authorizedRequest.CorrelationId);
        }

        return await provider.DiscoverCapabilitiesAsync(authorizedRequest, cancellationToken).ConfigureAwait(false);
    }
}
