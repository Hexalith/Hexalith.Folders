using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.Forgejo;

public sealed class ForgejoProvider : IGitProvider
{
    private readonly IForgejoCredentialResolver _credentialResolver;
    private readonly IForgejoApiClientFactory _apiClientFactory;

    public ForgejoProvider()
        : this(new UnconfiguredForgejoCredentialResolver(), new ForgejoHttpApiClientFactory())
    {
    }

    internal ForgejoProvider(
        IForgejoCredentialResolver credentialResolver,
        IForgejoApiClientFactory apiClientFactory)
    {
        _credentialResolver = credentialResolver ?? throw new ArgumentNullException(nameof(credentialResolver));
        _apiClientFactory = apiClientFactory ?? throw new ArgumentNullException(nameof(apiClientFactory));
    }

    public string ProviderFamily => ForgejoProviderConstants.ProviderFamily;

    public string ProviderKey => ForgejoProviderConstants.ProviderKey;

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

        if (!ForgejoCredentialModeValidator.TryGetSupportedMode(
            request.CredentialModeRequirements,
            out ProviderCredentialMode credentialMode,
            out string? credentialFailure))
        {
            return Failure(
                ProviderFailureCategory.ProviderValidationFailed,
                credentialFailure ?? "unsupported_forgejo_credential_mode",
                request);
        }

        if (!ForgejoAuthorizedBaseUrl.TryCanonicalize(
            request.TargetEvidence.Metadata.TryGetValue("authorized_base_url", out string? baseUrl) ? baseUrl : null,
            out Uri canonicalBaseUri,
            out string? baseUrlFailure))
        {
            return Failure(
                ProviderFailureCategory.ProviderValidationFailed,
                baseUrlFailure ?? "forgejo_base_url_invalid",
                request);
        }

        if (!ForgejoSafeTargetFingerprint.TryValidateMetadata(request.TargetEvidence, out string? targetMetadataFailure))
        {
            return Failure(
                ProviderFailureCategory.ProviderValidationFailed,
                targetMetadataFailure ?? "unsafe_forgejo_target_metadata",
                request);
        }

        if (!ForgejoSupportedVersionCatalog.TryFind(
            request.TargetEvidence.ProductVersion,
            out ForgejoSupportedVersionEntry supportedVersion))
        {
            return Failure(
                ProviderFailureCategory.ReconciliationRequired,
                "forgejo_target_version_unsupported",
                request);
        }

        if (!ForgejoSafeTargetFingerprint.TryCreate(
            request,
            credentialMode,
            canonicalBaseUri,
            supportedVersion.Version,
            out ProviderTargetEvidence? safeTargetEvidence,
            out string? targetFailure))
        {
            return Failure(
                ProviderFailureCategory.ProviderValidationFailed,
                targetFailure ?? "unsafe_forgejo_target_metadata",
                request);
        }

        ForgejoCredentialResolutionResult credentialResult = await _credentialResolver.ResolveAsync(
            new ForgejoCredentialResolutionRequest(
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

        ForgejoCredentialLease credential = credentialResult.Credential.ShouldNotBeNullForProvider();
        ForgejoReadinessResult readiness;
        try
        {
            IForgejoApiClient client = await _apiClientFactory.CreateAsync(
                new ForgejoApiClientRequest(
                    ForgejoProviderConstants.ProductHeader,
                    canonicalBaseUri,
                    ForgejoProviderConstants.ApiSurfaceVersion,
                    credentialMode,
                    request.ProviderBindingRef,
                    request.CorrelationId),
                credential,
                cancellationToken).ConfigureAwait(false);

            readiness = await client.GetReadinessAsync(
                new ForgejoReadinessRequest(
                    request.ManagedTenantId,
                    request.OrganizationId,
                    request.ProviderBindingRef,
                    credentialMode,
                    ForgejoProviderConstants.ApiSurfaceVersion,
                    supportedVersion.Version,
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
            return ForgejoFailureMapper.ToProviderFailure(readiness, request);
        }

        ForgejoVersionEvidence version = readiness.Version.ShouldNotBeNullForProvider();
        if (!ForgejoSupportedVersionCatalog.TryFind(
            version.SnapshotVersion,
            out ForgejoSupportedVersionEntry observedVersion))
        {
            return Failure(
                ProviderFailureCategory.ReconciliationRequired,
                "forgejo_snapshot_version_unsupported",
                request);
        }

        if (!string.Equals(observedVersion.Version, supportedVersion.Version, StringComparison.Ordinal))
        {
            return Failure(
                ProviderFailureCategory.ReconciliationRequired,
                "forgejo_snapshot_version_mismatch",
                request);
        }

        ProviderCapabilityDiscoveryRequest effectiveRequest = request with
        {
            ProviderFamily = ProviderFamily,
            ProviderKey = ProviderKey,
            ProfileSchemaVersion = ForgejoProviderConstants.CapabilityProfileSchemaVersion,
            TargetEvidence = safeTargetEvidence,
            CredentialModeRequirements = [credentialMode],
        };

        return ProviderCapabilityProfileFactory.Create(
            effectiveRequest,
            ProviderFamily,
            ProviderKey,
            ForgejoReadinessMapper.ToOperationRows(readiness.Permissions.ShouldNotBeNullForProvider()),
            ForgejoReadinessMapper.ToRateLimit(readiness.RateLimit.ShouldNotBeNullForProvider()),
            ForgejoFailureMapper.KnownFailureMappings,
            ForgejoReadinessMapper.ToEvidence(
                request,
                credentialMode,
                version,
                safeTargetEvidence.Metadata["safe_target_fingerprint"]));
    }

    public ProviderCapabilityComparisonResult CompareCapabilityProfiles(
        ProviderCapabilityProfile current,
        ProviderCapabilityProfile candidate)
        => ProviderCapabilityProfileFactory.Compare(current, candidate);

    public async Task<ProviderRepositoryCreationResult> CreateRepositoryAsync(
        ProviderRepositoryCreationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        ProviderRepositoryCreationResult? boundaryFailure = ValidateBoundary(request);
        if (boundaryFailure is not null)
        {
            return boundaryFailure;
        }

        if (!ForgejoCredentialModeValidator.TryGetSupportedMode(
            request.CredentialModeRequirements,
            out ProviderCredentialMode credentialMode,
            out string? credentialFailure))
        {
            return RepositoryFailure(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                credentialFailure ?? "unsupported_forgejo_credential_mode");
        }

        if (!ForgejoAuthorizedBaseUrl.TryCanonicalize(
            request.TargetEvidence.Metadata.TryGetValue("authorized_base_url", out string? baseUrl) ? baseUrl : null,
            out Uri canonicalBaseUri,
            out string? baseUrlFailure))
        {
            return RepositoryFailure(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                baseUrlFailure ?? "forgejo_base_url_invalid");
        }

        if (!ForgejoSafeTargetFingerprint.TryValidateMetadata(request.TargetEvidence, out string? targetMetadataFailure))
        {
            return RepositoryFailure(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                targetMetadataFailure ?? "unsafe_forgejo_target_metadata");
        }

        if (!ForgejoSupportedVersionCatalog.TryFind(
            request.TargetEvidence.ProductVersion,
            out ForgejoSupportedVersionEntry supportedVersion))
        {
            return RepositoryFailure(
                request,
                ProviderFailureCategory.ReconciliationRequired,
                "forgejo_target_version_unsupported");
        }

        if (!ForgejoSafeTargetFingerprint.TryCreate(
            request,
            credentialMode,
            canonicalBaseUri,
            supportedVersion.Version,
            out ProviderTargetEvidence? safeTargetEvidence,
            out string? targetFailure))
        {
            return RepositoryFailure(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                targetFailure ?? "unsafe_forgejo_target_metadata");
        }

        ForgejoCredentialResolutionResult credentialResult = await _credentialResolver.ResolveAsync(
            new ForgejoCredentialResolutionRequest(
                request.ManagedTenantId,
                request.OrganizationId,
                request.ProviderBindingRef,
                credentialMode,
                request.AuthorizationEvidence.Fingerprint,
                request.CorrelationId),
            cancellationToken).ConfigureAwait(false);

        if (!credentialResult.IsSuccess)
        {
            return RepositoryFailure(
                request,
                credentialResult.FailureCategory,
                credentialResult.ReasonCode,
                credentialResult.RetryAfter);
        }

        ForgejoCredentialLease credential = credentialResult.Credential.ShouldNotBeNullForProvider();
        ForgejoRepositoryCreationResult result;
        try
        {
            IForgejoApiClient client = await _apiClientFactory.CreateAsync(
                new ForgejoApiClientRequest(
                    ForgejoProviderConstants.ProductHeader,
                    canonicalBaseUri,
                    ForgejoProviderConstants.ApiSurfaceVersion,
                    credentialMode,
                    request.ProviderBindingRef,
                    request.CorrelationId),
                credential,
                cancellationToken).ConfigureAwait(false);

            result = await client.CreateRepositoryAsync(
                new ForgejoRepositoryCreationRequest(
                    request.ManagedTenantId,
                    request.OrganizationId,
                    request.ProviderBindingRef,
                    request.RepositoryBindingId,
                    credentialMode,
                    ForgejoProviderConstants.ApiSurfaceVersion,
                    supportedVersion.Version,
                    safeTargetEvidence.Metadata["safe_target_fingerprint"],
                    request.CorrelationId,
                    request.IdempotencyKey),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return RepositoryFailure(
                request,
                ProviderFailureCategory.UnknownProviderOutcome,
                "forgejo_repository_creation_outcome_unknown");
        }
        catch (Exception)
        {
            return RepositoryFailure(
                request,
                ProviderFailureCategory.UnknownProviderOutcome,
                "forgejo_repository_creation_outcome_unknown");
        }
        finally
        {
            await credential.DisposeAsync().ConfigureAwait(false);
        }

        return result.IsSuccess
            ? ProviderRepositoryCreationResult.Success(
                request,
                result.EquivalentExisting,
                safeTargetEvidence.Metadata["safe_target_fingerprint"])
            : ForgejoFailureMapper.ToProviderFailure(result, request);
    }

    private static ProviderCapabilityDiscoveryResult? ValidateBoundary(ProviderCapabilityDiscoveryRequest request)
    {
        try
        {
            string providerFamily = ProviderIdentityIdentifier.Normalize(request.ProviderFamily);
            string providerKey = ProviderIdentityIdentifier.Normalize(request.ProviderKey);
            if (!string.Equals(providerFamily, ForgejoProviderConstants.ProviderFamily, StringComparison.Ordinal)
                || !string.Equals(providerKey, ForgejoProviderConstants.ProviderKey, StringComparison.Ordinal))
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

    private static ProviderRepositoryCreationResult? ValidateBoundary(ProviderRepositoryCreationRequest request)
    {
        try
        {
            string providerFamily = ProviderIdentityIdentifier.Normalize(request.ProviderFamily);
            string providerKey = ProviderIdentityIdentifier.Normalize(request.ProviderKey);
            if (!string.Equals(providerFamily, ForgejoProviderConstants.ProviderFamily, StringComparison.Ordinal)
                || !string.Equals(providerKey, ForgejoProviderConstants.ProviderKey, StringComparison.Ordinal))
            {
                return RepositoryFailure(request, ProviderFailureCategory.UnsupportedProviderCapability, "unsupported_provider_family");
            }
        }
        catch (ArgumentException)
        {
            return RepositoryFailure(request, ProviderFailureCategory.ProviderValidationFailed, "provider_identity_malformed");
        }

        if (!string.Equals(request.AuthorizationEvidence.FreshnessClass, "fresh", StringComparison.OrdinalIgnoreCase))
        {
            return RepositoryFailure(request, ProviderFailureCategory.ReconciliationRequired, "authorization_evidence_stale");
        }

        if (request.TargetEvidence.IsStale)
        {
            return RepositoryFailure(request, ProviderFailureCategory.ReconciliationRequired, "target_evidence_stale");
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
            safeRemediationCode: category == ProviderFailureCategory.UnknownProviderOutcome
                ? "reconciliation_required_metadata_only"
                : $"{category.ToCategoryCode()}_remediation");

    private static ProviderRepositoryCreationResult RepositoryFailure(
        ProviderRepositoryCreationRequest request,
        ProviderFailureCategory category,
        string reasonCode,
        TimeSpan? retryAfter = null)
        => ProviderRepositoryCreationResult.Failure(
            request,
            category,
            reasonCode,
            retryAfter,
            safeRemediationCode: category == ProviderFailureCategory.UnknownProviderOutcome
                ? "reconciliation_required_metadata_only"
                : $"{category.ToCategoryCode()}_remediation");
}
