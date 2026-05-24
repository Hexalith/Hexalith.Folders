using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.GitHub;

public sealed class GitHubProvider : IGitProvider
{
    private readonly IGitHubCredentialResolver _credentialResolver;
    private readonly IGitHubApiClientFactory _apiClientFactory;

    public GitHubProvider()
        : this(new UnconfiguredGitHubCredentialResolver(), new OctokitGitHubApiClientFactory())
    {
    }

    internal GitHubProvider(
        IGitHubCredentialResolver credentialResolver,
        IGitHubApiClientFactory apiClientFactory)
    {
        _credentialResolver = credentialResolver ?? throw new ArgumentNullException(nameof(credentialResolver));
        _apiClientFactory = apiClientFactory ?? throw new ArgumentNullException(nameof(apiClientFactory));
    }

    public string ProviderFamily => GitHubProviderConstants.ProviderFamily;

    public string ProviderKey => GitHubProviderConstants.ProviderKey;

    public async Task<ProviderCapabilityDiscoveryResult> DiscoverCapabilitiesAsync(
        ProviderCapabilityDiscoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        ProviderCapabilityDiscoveryResult? boundaryFailure = ValidateBoundary(request);
        if (boundaryFailure is not null)
        {
            return boundaryFailure;
        }

        if (!GitHubCredentialModeValidator.TryGetSupportedMode(
            request.CredentialModeRequirements,
            out ProviderCredentialMode credentialMode,
            out string? credentialFailure))
        {
            return Failure(
                ProviderFailureCategory.ProviderValidationFailed,
                credentialFailure ?? "unsupported_github_credential_mode",
                request);
        }

        if (!GitHubSafeTargetFingerprint.TryCreate(request, credentialMode, out ProviderTargetEvidence? safeTargetEvidence, out string? targetFailure))
        {
            return Failure(
                ProviderFailureCategory.ProviderValidationFailed,
                targetFailure ?? "unsafe_github_target_metadata",
                request);
        }

        GitHubCredentialResolutionResult credentialResult = await _credentialResolver.ResolveAsync(
            new GitHubCredentialResolutionRequest(
                request.ManagedTenantId,
                request.OrganizationId,
                request.ProviderBindingRef,
                credentialMode,
                request.AuthorizationEvidence.Fingerprint,
                request.CorrelationId),
            cancellationToken).ConfigureAwait(false);

        if (!credentialResult.IsSuccess)
        {
            return Failure(
                credentialResult.FailureCategory,
                credentialResult.ReasonCode,
                request,
                credentialResult.RetryAfter);
        }

        GitHubCredentialLease credential = credentialResult.Credential.ShouldNotBeNullForProvider();
        GitHubReadinessResult readiness;
        try
        {
            IGitHubApiClient client = await _apiClientFactory.CreateAsync(
                new GitHubApiClientRequest(
                    GitHubProviderConstants.ProductHeader,
                    GitHubProviderConstants.RestApiVersion,
                    credentialMode,
                    request.ProviderBindingRef,
                    request.CorrelationId),
                credential,
                cancellationToken).ConfigureAwait(false);

            readiness = await client.GetReadinessAsync(
                new GitHubReadinessRequest(
                    request.ManagedTenantId,
                    request.OrganizationId,
                    request.ProviderBindingRef,
                    credentialMode,
                    GitHubProviderConstants.RestApiVersion,
                    safeTargetEvidence.Metadata["safe_target_fingerprint"],
                    request.CorrelationId),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await credential.DisposeAsync().ConfigureAwait(false);
        }

        if (!readiness.IsSuccess)
        {
            return GitHubFailureMapper.ToProviderFailure(readiness, request);
        }

        ProviderCapabilityDiscoveryRequest effectiveRequest = request with
        {
            ProviderFamily = ProviderFamily,
            ProviderKey = ProviderKey,
            ProfileSchemaVersion = "v1",
            TargetEvidence = safeTargetEvidence,
            CredentialModeRequirements = [credentialMode],
        };

        return ProviderCapabilityProfileFactory.Create(
            effectiveRequest,
            ProviderFamily,
            ProviderKey,
            GitHubReadinessMapper.ToOperationRows(readiness.Permissions.ShouldNotBeNullForProvider()),
            GitHubReadinessMapper.ToRateLimit(readiness.RateLimit.ShouldNotBeNullForProvider()),
            GitHubFailureMapper.KnownFailureMappings,
            GitHubReadinessMapper.ToEvidence(
                request,
                credentialMode,
                safeTargetEvidence.Metadata["safe_target_fingerprint"]));
    }

    public ProviderCapabilityComparisonResult CompareCapabilityProfiles(
        ProviderCapabilityProfile current,
        ProviderCapabilityProfile candidate)
        => ProviderCapabilityProfileFactory.Compare(current, candidate);

    private static ProviderCapabilityDiscoveryResult? ValidateBoundary(ProviderCapabilityDiscoveryRequest request)
    {
        try
        {
            string providerFamily = ProviderIdentityIdentifier.Normalize(request.ProviderFamily);
            string providerKey = ProviderIdentityIdentifier.Normalize(request.ProviderKey);
            if (!string.Equals(providerFamily, GitHubProviderConstants.ProviderFamily, StringComparison.Ordinal)
                || !string.Equals(providerKey, GitHubProviderConstants.ProviderKey, StringComparison.Ordinal))
            {
                return Failure(ProviderFailureCategory.UnsupportedProviderCapability, "unsupported_provider_family", request);
            }
        }
        catch (ArgumentException)
        {
            return Failure(ProviderFailureCategory.ProviderValidationFailed, "provider_identity_malformed", request);
        }

        if (!string.Equals(request.AuthorizationEvidence.FreshnessClass, "fresh", StringComparison.OrdinalIgnoreCase))
        {
            return Failure(ProviderFailureCategory.ReconciliationRequired, "authorization_evidence_stale", request);
        }

        if (request.TargetEvidence.IsStale)
        {
            return Failure(ProviderFailureCategory.ReconciliationRequired, "target_evidence_stale", request);
        }

        return null;
    }

    private static ProviderCapabilityDiscoveryResult Failure(
        ProviderFailureCategory category,
        string reasonCode,
        ProviderCapabilityDiscoveryRequest request,
        TimeSpan? retryAfter = null)
        => ProviderCapabilityDiscoveryResult.Failure(
            category,
            reasonCode,
            request.CorrelationId,
            retryAfter,
            safeRemediationCode: $"{category.ToCategoryCode()}_remediation");
}
