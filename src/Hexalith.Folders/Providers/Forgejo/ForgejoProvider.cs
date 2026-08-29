using System.Globalization;
using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.Forgejo;

public sealed class ForgejoProvider : IGitProvider
{
    private static readonly TimeSpan MaximumAuthorizationAge = TimeSpan.FromMinutes(5);
    private readonly IForgejoCredentialResolver _credentialResolver;
    private readonly IForgejoApiClientFactory _apiClientFactory;
    private readonly IProviderRepositoryTargetResolver _targetResolver;
    private readonly TimeProvider _timeProvider;

    public ForgejoProvider()
        : this(
            new UnconfiguredForgejoCredentialResolver(),
            new ForgejoHttpApiClientFactory(),
            new UnconfiguredProviderRepositoryTargetResolver(),
            TimeProvider.System)
    {
    }

    internal ForgejoProvider(
        IForgejoCredentialResolver credentialResolver,
        IForgejoApiClientFactory apiClientFactory)
        : this(
            credentialResolver,
            apiClientFactory,
            new UnconfiguredProviderRepositoryTargetResolver(),
            TimeProvider.System)
    {
    }

    internal ForgejoProvider(
        IForgejoCredentialResolver credentialResolver,
        IForgejoApiClientFactory apiClientFactory,
        IProviderRepositoryTargetResolver targetResolver)
        : this(credentialResolver, apiClientFactory, targetResolver, TimeProvider.System)
    {
    }

    internal ForgejoProvider(
        IForgejoCredentialResolver credentialResolver,
        IForgejoApiClientFactory apiClientFactory,
        IProviderRepositoryTargetResolver targetResolver,
        TimeProvider timeProvider)
    {
        _credentialResolver = credentialResolver ?? throw new ArgumentNullException(nameof(credentialResolver));
        _apiClientFactory = apiClientFactory ?? throw new ArgumentNullException(nameof(apiClientFactory));
        _targetResolver = targetResolver ?? throw new ArgumentNullException(nameof(targetResolver));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
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
                request.CredentialReferenceId,
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

        ProviderRepositoryCreationResult? admissionResult = ReplayOrReject(
            request,
            safeTargetEvidence.Metadata["safe_target_fingerprint"]);
        if (admissionResult is not null)
        {
            return admissionResult;
        }

        ProviderRepositoryTargetResolutionResult targetResolution;
        try
        {
            targetResolution = await _targetResolver.ResolveCreationAsync(
                new ProviderRepositoryCreationTargetResolutionRequest(
                    request.ManagedTenantId,
                    request.OrganizationId,
                    request.ProviderBindingRef,
                    request.RepositoryBindingId,
                    request.RepositoryProfileRef,
                    request.AuthorizationEvidence.Fingerprint,
                    request.CorrelationId),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return RepositoryFailure(
                request,
                ProviderFailureCategory.ProviderUnavailable,
                "forgejo_repository_target_resolution_unavailable");
        }

        if (targetResolution is null)
        {
            return RepositoryFailure(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                "resolved_provider_target_malformed");
        }

        if (!targetResolution.IsSuccess)
        {
            ProviderFailureCategory resolutionCategory = SafeRepositoryCategory(targetResolution.FailureCategory);
            return RepositoryFailure(
                request,
                resolutionCategory,
                SafeRepositoryReason(targetResolution.ReasonCode, resolutionCategory.ToCategoryCode()),
                SafeRetryAfter(targetResolution.RetryAfter));
        }

        if (targetResolution.Target is not { } resolvedTarget
            || !IsForgejoRepositoryTargetValid(resolvedTarget))
        {
            return RepositoryFailure(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                "resolved_provider_target_malformed");
        }

        ForgejoCredentialResolutionResult credentialResult = await _credentialResolver.ResolveAsync(
            new ForgejoCredentialResolutionRequest(
                request.ManagedTenantId,
                request.OrganizationId,
                request.ProviderBindingRef,
                request.CredentialReferenceId,
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
            IForgejoApiClient client;
            try
            {
                client = await _apiClientFactory.CreateAsync(
                    new ForgejoApiClientRequest(
                        ForgejoProviderConstants.ProductHeader,
                        canonicalBaseUri,
                        ForgejoProviderConstants.ApiSurfaceVersion,
                        credentialMode,
                        request.ProviderBindingRef,
                        request.CorrelationId),
                    credential,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return RepositoryFailure(
                    request,
                    ProviderFailureCategory.ProviderFailureKnown,
                    "forgejo_cancellation_before_dispatch");
            }
            catch (Exception)
            {
                return RepositoryFailure(
                    request,
                    ProviderFailureCategory.ProviderUnavailable,
                    "forgejo_server_unavailable");
            }

            try
            {
                result = await client.CreateRepositoryAsync(
                    new ForgejoRepositoryCreationRequest(
                        request.ManagedTenantId,
                        request.OrganizationId,
                        request.ProviderBindingRef,
                        request.RepositoryBindingId,
                        resolvedTarget,
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
        }
        finally
        {
            await credential.DisposeAsync().ConfigureAwait(false);
        }

        if (result.IsSuccess)
        {
            string? canonicalRepositoryId = SafeCanonicalRepositoryId(result.CanonicalRepositoryId);
            return canonicalRepositoryId is not null
                ? ProviderRepositoryCreationResult.Success(
                request,
                result.EquivalentExisting,
                safeTargetEvidence.Metadata["safe_target_fingerprint"],
                    canonicalRepositoryId)
                : ProviderRepositoryCreationResult.Failure(
                    request,
                    ProviderFailureCategory.ProviderFailureKnown,
                    "forgejo_canonical_repository_identity_malformed",
                    safeTargetFingerprint: safeTargetEvidence.Metadata["safe_target_fingerprint"]);
        }

        return ForgejoFailureMapper.ToProviderFailure(
            result,
            request,
            safeTargetEvidence.Metadata["safe_target_fingerprint"]);
    }

    public async Task<ProviderRepositoryBindingResult> ValidateRepositoryBindingAsync(
        ProviderRepositoryBindingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        ProviderRepositoryBindingResult? boundaryFailure = ValidateBoundary(request);
        if (boundaryFailure is not null)
        {
            return boundaryFailure;
        }

        if (!ForgejoCredentialModeValidator.TryGetSupportedMode(
            request.CredentialModeRequirements,
            out ProviderCredentialMode credentialMode,
            out string? credentialFailure))
        {
            return RepositoryBindingFailure(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                credentialFailure ?? "unsupported_forgejo_credential_mode");
        }

        if (!ForgejoAuthorizedBaseUrl.TryCanonicalize(
            request.TargetEvidence.Metadata.TryGetValue("authorized_base_url", out string? baseUrl) ? baseUrl : null,
            out Uri canonicalBaseUri,
            out string? baseUrlFailure))
        {
            return RepositoryBindingFailure(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                baseUrlFailure ?? "forgejo_base_url_invalid");
        }

        if (!ForgejoSafeTargetFingerprint.TryValidateMetadata(request.TargetEvidence, out string? targetMetadataFailure))
        {
            return RepositoryBindingFailure(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                targetMetadataFailure ?? "unsafe_forgejo_target_metadata");
        }

        if (!ForgejoSupportedVersionCatalog.TryFind(
            request.TargetEvidence.ProductVersion,
            out ForgejoSupportedVersionEntry supportedVersion))
        {
            return RepositoryBindingFailure(
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
            return RepositoryBindingFailure(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                targetFailure ?? "unsafe_forgejo_target_metadata");
        }

        ProviderRepositoryBindingResult? admissionResult = ReplayOrReject(
            request,
            safeTargetEvidence.Metadata["safe_target_fingerprint"]);
        if (admissionResult is not null)
        {
            return admissionResult;
        }

        ProviderRepositoryTargetResolutionResult targetResolution;
        try
        {
            targetResolution = await _targetResolver.ResolveBindingAsync(
                new ProviderRepositoryBindingTargetResolutionRequest(
                    request.ManagedTenantId,
                    request.OrganizationId,
                    request.ProviderBindingRef,
                    request.RepositoryBindingId,
                    request.ExternalRepositoryRef,
                    request.ExternalRepositoryRefFingerprint,
                    request.BranchRefPolicyRef,
                    request.AuthorizationEvidence.Fingerprint,
                    request.CorrelationId),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return RepositoryBindingFailure(
                request,
                ProviderFailureCategory.ProviderUnavailable,
                "forgejo_repository_target_resolution_unavailable");
        }

        if (targetResolution is null)
        {
            return RepositoryBindingFailure(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                "resolved_provider_target_malformed");
        }

        if (!targetResolution.IsSuccess)
        {
            ProviderFailureCategory resolutionCategory = SafeRepositoryCategory(targetResolution.FailureCategory);
            return RepositoryBindingFailure(
                request,
                resolutionCategory,
                SafeRepositoryReason(targetResolution.ReasonCode, resolutionCategory.ToCategoryCode()),
                SafeRetryAfter(targetResolution.RetryAfter));
        }

        if (targetResolution.Target is not { } resolvedTarget
            || !IsForgejoRepositoryTargetValid(resolvedTarget))
        {
            return RepositoryBindingFailure(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                "resolved_provider_target_malformed");
        }

        ForgejoCredentialResolutionResult credentialResult = await _credentialResolver.ResolveAsync(
            new ForgejoCredentialResolutionRequest(
                request.ManagedTenantId,
                request.OrganizationId,
                request.ProviderBindingRef,
                request.CredentialReferenceId,
                credentialMode,
                request.AuthorizationEvidence.Fingerprint,
                request.CorrelationId),
            cancellationToken).ConfigureAwait(false);

        if (!credentialResult.IsSuccess)
        {
            return RepositoryBindingFailure(
                request,
                credentialResult.FailureCategory,
                credentialResult.ReasonCode,
                credentialResult.RetryAfter);
        }

        ForgejoCredentialLease credential = credentialResult.Credential.ShouldNotBeNullForProvider();
        ForgejoRepositoryBindingResult result;
        try
        {
            IForgejoApiClient client;
            try
            {
                client = await _apiClientFactory.CreateAsync(
                    new ForgejoApiClientRequest(
                        ForgejoProviderConstants.ProductHeader,
                        canonicalBaseUri,
                        ForgejoProviderConstants.ApiSurfaceVersion,
                        credentialMode,
                        request.ProviderBindingRef,
                        request.CorrelationId),
                    credential,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return RepositoryBindingFailure(
                    request,
                    ProviderFailureCategory.ProviderFailureKnown,
                    "forgejo_observation_cancelled");
            }
            catch (Exception)
            {
                return RepositoryBindingFailure(
                    request,
                    ProviderFailureCategory.ProviderUnavailable,
                    "forgejo_server_unavailable");
            }

            try
            {
                result = await client.ValidateRepositoryBindingAsync(
                    new ForgejoRepositoryBindingRequest(
                        request.ManagedTenantId,
                        request.OrganizationId,
                        request.ProviderBindingRef,
                        request.RepositoryBindingId,
                        resolvedTarget,
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
                return RepositoryBindingFailure(
                    request,
                    ProviderFailureCategory.ProviderFailureKnown,
                    "forgejo_observation_cancelled");
            }
            catch (Exception)
            {
                return RepositoryBindingFailure(
                    request,
                    ProviderFailureCategory.ProviderUnavailable,
                    "forgejo_server_unavailable");
            }
        }
        finally
        {
            await credential.DisposeAsync().ConfigureAwait(false);
        }

        if (result.IsSuccess)
        {
            string? canonicalRepositoryId = SafeCanonicalRepositoryId(result.CanonicalRepositoryId);
            return canonicalRepositoryId is not null
                ? ProviderRepositoryBindingResult.Success(
                request,
                result.EquivalentExisting,
                safeTargetEvidence.Metadata["safe_target_fingerprint"],
                    canonicalRepositoryId)
                : ProviderRepositoryBindingResult.Failure(
                    request,
                    ProviderFailureCategory.ProviderFailureKnown,
                    "forgejo_canonical_repository_identity_malformed",
                    safeTargetFingerprint: safeTargetEvidence.Metadata["safe_target_fingerprint"]);
        }

        return ForgejoFailureMapper.ToProviderFailure(
            result,
            request,
            safeTargetEvidence.Metadata["safe_target_fingerprint"]);
    }

    private static readonly HashSet<string> AllowedRepositoryReasonCodes = new(StringComparer.Ordinal)
    {
        "authorization_evidence_stale",
        "authorization_evidence_malformed",
        "forgejo_administration_permission_insufficient",
        "forgejo_authentication_required",
        "forgejo_branch_or_path_missing",
        "forgejo_branch_protection_conflict",
        "forgejo_cancellation_before_dispatch",
        "forgejo_canonical_repository_identity_malformed",
        "forgejo_capability_unsupported",
        "forgejo_contents_permission_insufficient",
        "forgejo_cross_origin_redirect_rejected",
        "forgejo_default_branch_conflict",
        "forgejo_malformed_response",
        "forgejo_mutation_cancellation_outcome_unknown",
        "forgejo_mutation_outcome_unknown",
        "forgejo_operation_evidence_malformed",
        "forgejo_operation_scope_mismatch",
        "forgejo_observation_cancelled",
        "forgejo_permission_insufficient",
        "forgejo_rate_limited",
        "forgejo_ref_operation_unsupported",
        "forgejo_repository_binding_outcome_unknown",
        "forgejo_repository_conflict",
        "forgejo_repository_creation_outcome_unknown",
        "forgejo_repository_missing",
        "forgejo_repository_target_resolution_unavailable",
        "forgejo_replay_evidence_malformed",
        "forgejo_resource_hidden_or_missing",
        "forgejo_schema_drift_breaking",
        "forgejo_server_unavailable",
        "forgejo_transport_outcome_unknown",
        "forgejo_unmapped_outcome",
        "forgejo_validation_failed",
        "forgejo_version_incompatible",
        "provider_identity_malformed",
        "provider_repository_binding_target_unconfigured",
        "provider_repository_creation_target_unconfigured",
        "resolved_provider_target_malformed",
        "target_evidence_stale",
        "target_evidence_malformed",
        "unsafe_forgejo_target_metadata",
        "unsupported_forgejo_credential_mode",
        "unsupported_provider_family",
    };

    private static bool IsAdmissionWellFormed(ProviderIdempotencyAdmission? admission)
        => admission is not null
            && Enum.IsDefined(admission.Disposition)
            && IsSafeOpaqueValue(admission.IntentFingerprint)
            && (admission.Disposition == ProviderIdempotencyDisposition.EquivalentReplay
                || HasNoPriorOutcomeFields(admission));

    private static bool IsReplayEvidenceWellFormed(ProviderIdempotencyAdmission admission)
    {
        if (admission.Disposition != ProviderIdempotencyDisposition.EquivalentReplay)
        {
            return true;
        }

        if (admission.PriorOutcomeDisposition is null
            || !Enum.IsDefined(admission.PriorOutcomeDisposition.Value)
            || !IsSafeOpaqueValue(admission.PriorOperationReference))
        {
            return false;
        }

        return admission.PriorOutcomeDisposition switch
        {
            ProviderPriorOutcomeDisposition.Success => IsSafeFingerprint(admission.PriorSafeOutcomeFingerprint)
                && SafeCanonicalRepositoryId(admission.PriorCanonicalRepositoryId) is not null
                && admission.PriorReconciliationReference is null
                && admission.PriorFailureCategory == ProviderFailureCategory.None
                && admission.PriorReasonCode is null
                && admission.PriorRemediationCode is null
                && !admission.PriorRetryable
                && admission.PriorRetryAfter is null,
            ProviderPriorOutcomeDisposition.Unknown => IsSafeOpaqueValue(admission.PriorReconciliationReference)
                && admission.PriorCanonicalRepositoryId is null
                && (admission.PriorSafeOutcomeFingerprint is null || IsSafeFingerprint(admission.PriorSafeOutcomeFingerprint))
                && admission.PriorFailureCategory == ProviderFailureCategory.None
                && admission.PriorReasonCode is null
                && admission.PriorRemediationCode is null
                && !admission.PriorRetryable
                && admission.PriorRetryAfter is null,
            ProviderPriorOutcomeDisposition.KnownFailure => admission.PriorFailureCategory is not (ProviderFailureCategory.None or ProviderFailureCategory.UnknownProviderOutcome)
                && Enum.IsDefined(admission.PriorFailureCategory)
                && IsSafeFingerprint(admission.PriorSafeOutcomeFingerprint)
                && admission.PriorCanonicalRepositoryId is null
                && admission.PriorReconciliationReference is null
                && IsRepositoryReasonAllowed(admission.PriorReasonCode)
                && admission.PriorRemediationCode is not null
                && IsSafeRemediation(admission.PriorRemediationCode, admission.PriorFailureCategory)
                && (admission.PriorRetryable || admission.PriorRetryAfter is null)
                && (admission.PriorRetryAfter is null || SafeRetryAfter(admission.PriorRetryAfter) == admission.PriorRetryAfter),
            _ => false,
        };

    private static bool HasNoPriorOutcomeFields(ProviderIdempotencyAdmission admission)
        => admission.PriorSafeOutcomeFingerprint is null
            && admission.PriorReconciliationReference is null
            && admission.PriorOperationReference is null
            && admission.PriorOutcomeDisposition is null
            && admission.PriorFailureCategory == ProviderFailureCategory.None
            && admission.PriorReasonCode is null
            && admission.PriorRemediationCode is null
            && !admission.PriorRetryable
            && admission.PriorRetryAfter is null
            && admission.PriorCanonicalRepositoryId is null;
    }

    private static ProviderRepositoryCreationResult? ReplayOrReject(
        ProviderRepositoryCreationRequest request,
        string safeTargetFingerprint)
        => request.IdempotencyAdmission.Disposition switch
        {
            ProviderIdempotencyDisposition.Fresh => null,
            ProviderIdempotencyDisposition.EquivalentReplay => Replay(request, safeTargetFingerprint, request.IdempotencyAdmission),
            ProviderIdempotencyDisposition.Conflict => ProviderRepositoryCreationResult.Failure(
                request,
                ProviderFailureCategory.ProviderConflict,
                "idempotency_conflict"),
            _ => ProviderRepositoryCreationResult.Failure(
                request,
                ProviderFailureCategory.ProviderConflict,
                "idempotency_key_expired"),
        };

    private static ProviderRepositoryCreationResult Replay(
        ProviderRepositoryCreationRequest request,
        string safeTargetFingerprint,
        ProviderIdempotencyAdmission admission)
        => admission.PriorOutcomeDisposition switch
        {
            ProviderPriorOutcomeDisposition.Unknown => ProviderRepositoryCreationResult.Failure(
                request,
                ProviderFailureCategory.UnknownProviderOutcome,
                SafeRepositoryReason(admission.PriorReasonCode, "forgejo_repository_creation_outcome_unknown"),
                retryAfter: null,
                safeRemediationCode: "reconciliation_required_metadata_only",
                retryable: false,
                safeTargetFingerprint: safeTargetFingerprint,
                priorSafeOutcomeFingerprint: admission.PriorSafeOutcomeFingerprint,
                priorOperationReference: admission.PriorOperationReference,
                priorReconciliationReference: admission.PriorReconciliationReference),
            ProviderPriorOutcomeDisposition.KnownFailure => ProviderRepositoryCreationResult.Failure(
                request,
                admission.PriorFailureCategory,
                SafeRepositoryReason(admission.PriorReasonCode, admission.PriorFailureCategory.ToCategoryCode()),
                SafeRetryAfter(admission.PriorRetryAfter),
                SafeRemediation(admission.PriorRemediationCode, admission.PriorFailureCategory),
                admission.PriorRetryable,
                safeTargetFingerprint,
                admission.PriorSafeOutcomeFingerprint,
                admission.PriorOperationReference),
            _ => ProviderRepositoryCreationResult.Success(
                request,
                equivalentExisting: true,
                safeTargetFingerprint,
                canonicalRepositoryId: admission.PriorCanonicalRepositoryId,
                priorSafeOutcomeFingerprint: admission.PriorSafeOutcomeFingerprint,
                priorOperationReference: admission.PriorOperationReference),
        };

    private static ProviderRepositoryBindingResult? ReplayOrReject(
        ProviderRepositoryBindingRequest request,
        string safeTargetFingerprint)
        => request.IdempotencyAdmission.Disposition switch
        {
            ProviderIdempotencyDisposition.Fresh => null,
            ProviderIdempotencyDisposition.EquivalentReplay => Replay(request, safeTargetFingerprint, request.IdempotencyAdmission),
            ProviderIdempotencyDisposition.Conflict => ProviderRepositoryBindingResult.Failure(
                request,
                ProviderFailureCategory.ProviderConflict,
                "idempotency_conflict"),
            _ => ProviderRepositoryBindingResult.Failure(
                request,
                ProviderFailureCategory.ProviderConflict,
                "idempotency_key_expired"),
        };

    private static ProviderRepositoryBindingResult Replay(
        ProviderRepositoryBindingRequest request,
        string safeTargetFingerprint,
        ProviderIdempotencyAdmission admission)
        => admission.PriorOutcomeDisposition switch
        {
            ProviderPriorOutcomeDisposition.Unknown => ProviderRepositoryBindingResult.Failure(
                request,
                ProviderFailureCategory.UnknownProviderOutcome,
                SafeRepositoryReason(admission.PriorReasonCode, "forgejo_repository_binding_outcome_unknown"),
                retryAfter: null,
                safeRemediationCode: "reconciliation_required_metadata_only",
                retryable: false,
                safeTargetFingerprint: safeTargetFingerprint,
                priorSafeOutcomeFingerprint: admission.PriorSafeOutcomeFingerprint,
                priorOperationReference: admission.PriorOperationReference,
                priorReconciliationReference: admission.PriorReconciliationReference),
            ProviderPriorOutcomeDisposition.KnownFailure => ProviderRepositoryBindingResult.Failure(
                request,
                admission.PriorFailureCategory,
                SafeRepositoryReason(admission.PriorReasonCode, admission.PriorFailureCategory.ToCategoryCode()),
                SafeRetryAfter(admission.PriorRetryAfter),
                SafeRemediation(admission.PriorRemediationCode, admission.PriorFailureCategory),
                admission.PriorRetryable,
                safeTargetFingerprint,
                admission.PriorSafeOutcomeFingerprint,
                admission.PriorOperationReference),
            _ => ProviderRepositoryBindingResult.Success(
                request,
                equivalentExisting: true,
                safeTargetFingerprint,
                canonicalRepositoryId: admission.PriorCanonicalRepositoryId,
                priorSafeOutcomeFingerprint: admission.PriorSafeOutcomeFingerprint,
                priorOperationReference: admission.PriorOperationReference),
        };

    private static bool IsRepositoryReasonAllowed(string? reasonCode)
        => reasonCode is not null
            && (AllowedRepositoryReasonCodes.Contains(reasonCode)
                || reasonCode is "idempotency_conflict" or "idempotency_key_expired");

    private static string SafeRepositoryReason(string? reasonCode, string fallback)
        => IsRepositoryReasonAllowed(reasonCode) ? reasonCode! : fallback;

    private static ProviderFailureCategory SafeRepositoryCategory(ProviderFailureCategory category)
        => Enum.IsDefined(category) && category != ProviderFailureCategory.None
            ? category
            : ProviderFailureCategory.ProviderFailureKnown;

    private static bool IsSafeRemediation(string? remediationCode, ProviderFailureCategory category)
        => remediationCode is null
            || string.Equals(remediationCode, "reconciliation_required_metadata_only", StringComparison.Ordinal)
            || string.Equals(remediationCode, $"{category.ToCategoryCode()}_remediation", StringComparison.Ordinal);

    private static string SafeRemediation(string? remediationCode, ProviderFailureCategory category)
        => IsSafeRemediation(remediationCode, category) && remediationCode is not null
            ? remediationCode
            : $"{category.ToCategoryCode()}_remediation";

    private static TimeSpan? SafeRetryAfter(TimeSpan? retryAfter)
        => retryAfter is null
            ? null
            : retryAfter.Value < TimeSpan.Zero
                ? TimeSpan.Zero
                : retryAfter.Value > TimeSpan.FromHours(24)
                    ? TimeSpan.FromHours(24)
                    : retryAfter;

    private static bool IsSafeOpaqueValue(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length <= 512
            && ProviderGitOperationResolvedTarget.IsCanonicalUnicode(value)
            && !value.Contains("://", StringComparison.Ordinal)
            && !value.Any(char.IsControl);

    private static bool IsReservedTenant(string? managedTenantId)
        => string.Equals(managedTenantId?.Trim(), "system", StringComparison.OrdinalIgnoreCase);

    private static bool IsSafeFingerprint(string? value)
        => value is { Length: 64 }
            && value.All(static character => char.IsAsciiDigit(character) || character is >= 'a' and <= 'f');

    private static string? SafeCanonicalRepositoryId(string? canonicalRepositoryId)
    {
        if (canonicalRepositoryId is not { Length: > 0 and <= 64 }
            || !long.TryParse(canonicalRepositoryId, NumberStyles.None, CultureInfo.InvariantCulture, out long numericId)
            || numericId <= 0)
        {
            return null;
        }

        string normalized = numericId.ToString(CultureInfo.InvariantCulture);
        return string.Equals(canonicalRepositoryId, normalized, StringComparison.Ordinal) ? normalized : null;
    }

    private static bool IsForgejoRepositoryTargetValid(ProviderRepositoryResolvedTarget target)
        => target.TryValidate(out _)
            && (target.ExpectedCanonicalRepositoryId is null
                || SafeCanonicalRepositoryId(target.ExpectedCanonicalRepositoryId) is not null);

    private ProviderCapabilityDiscoveryResult? ValidateBoundary(ProviderCapabilityDiscoveryRequest request)
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

        if (request.AuthorizationEvidence is null
            || request.TargetEvidence is null
            || request.TargetEvidence.Metadata is null)
        {
            return Failure(ProviderFailureCategory.ProviderValidationFailed, "forgejo_operation_evidence_malformed", request);
        }

        string? evidenceFailure = ValidateOperationEvidence(
            request.AuthorizationEvidence,
            request.TargetEvidence,
            "readiness");
        if (evidenceFailure is not null)
        {
            ProviderFailureCategory category = evidenceFailure is "authorization_evidence_stale" or "target_evidence_stale"
                ? ProviderFailureCategory.ReconciliationRequired
                : ProviderFailureCategory.ProviderValidationFailed;
            return Failure(category, evidenceFailure, request);
        }

        return null;
    }

    private ProviderRepositoryCreationResult? ValidateBoundary(ProviderRepositoryCreationRequest request)
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

        if (!IsSafeOpaqueValue(request.ManagedTenantId)
            || !IsSafeOpaqueValue(request.OrganizationId)
            || IsReservedTenant(request.ManagedTenantId))
        {
            return RepositoryFailure(request, ProviderFailureCategory.ProviderValidationFailed, "forgejo_operation_evidence_malformed");
        }

        if (request.AuthorizationEvidence is null
            || request.TargetEvidence is null
            || request.TargetEvidence.Metadata is null)
        {
            return RepositoryFailure(request, ProviderFailureCategory.ProviderValidationFailed, "forgejo_operation_evidence_malformed");
        }

        string? evidenceFailure = ValidateOperationEvidence(
            request.AuthorizationEvidence,
            request.TargetEvidence,
            "repository_creation");
        if (evidenceFailure is not null)
        {
            ProviderFailureCategory category = evidenceFailure is "authorization_evidence_stale" or "target_evidence_stale"
                ? ProviderFailureCategory.ReconciliationRequired
                : ProviderFailureCategory.ProviderValidationFailed;
            return RepositoryFailure(request, category, evidenceFailure);
        }

        if (!IsAdmissionWellFormed(request.IdempotencyAdmission))
        {
            return RepositoryFailure(request, ProviderFailureCategory.ProviderValidationFailed, "forgejo_mutation_intent_malformed");
        }

        if (!IsReplayEvidenceWellFormed(request.IdempotencyAdmission))
        {
            return RepositoryFailure(request, ProviderFailureCategory.ProviderValidationFailed, "forgejo_replay_evidence_malformed");
        }

        return null;
    }

    private ProviderRepositoryBindingResult? ValidateBoundary(ProviderRepositoryBindingRequest request)
    {
        try
        {
            string providerFamily = ProviderIdentityIdentifier.Normalize(request.ProviderFamily);
            string providerKey = ProviderIdentityIdentifier.Normalize(request.ProviderKey);
            if (!string.Equals(providerFamily, ForgejoProviderConstants.ProviderFamily, StringComparison.Ordinal)
                || !string.Equals(providerKey, ForgejoProviderConstants.ProviderKey, StringComparison.Ordinal))
            {
                return RepositoryBindingFailure(request, ProviderFailureCategory.UnsupportedProviderCapability, "unsupported_provider_family");
            }
        }
        catch (ArgumentException)
        {
            return RepositoryBindingFailure(request, ProviderFailureCategory.ProviderValidationFailed, "provider_identity_malformed");
        }

        if (!IsSafeOpaqueValue(request.ManagedTenantId)
            || !IsSafeOpaqueValue(request.OrganizationId)
            || IsReservedTenant(request.ManagedTenantId))
        {
            return RepositoryBindingFailure(request, ProviderFailureCategory.ProviderValidationFailed, "forgejo_operation_evidence_malformed");
        }

        if (request.AuthorizationEvidence is null
            || request.TargetEvidence is null
            || request.TargetEvidence.Metadata is null)
        {
            return RepositoryBindingFailure(request, ProviderFailureCategory.ProviderValidationFailed, "forgejo_operation_evidence_malformed");
        }

        string? evidenceFailure = ValidateOperationEvidence(
            request.AuthorizationEvidence,
            request.TargetEvidence,
            "existing_repository_binding");
        if (evidenceFailure is not null)
        {
            ProviderFailureCategory category = evidenceFailure is "authorization_evidence_stale" or "target_evidence_stale"
                ? ProviderFailureCategory.ReconciliationRequired
                : ProviderFailureCategory.ProviderValidationFailed;
            return RepositoryBindingFailure(request, category, evidenceFailure);
        }

        if (!IsAdmissionWellFormed(request.IdempotencyAdmission))
        {
            return RepositoryBindingFailure(request, ProviderFailureCategory.ProviderValidationFailed, "forgejo_mutation_intent_malformed");
        }

        if (!IsReplayEvidenceWellFormed(request.IdempotencyAdmission))
        {
            return RepositoryBindingFailure(request, ProviderFailureCategory.ProviderValidationFailed, "forgejo_replay_evidence_malformed");
        }

        return null;
    }

    private string? ValidateOperationEvidence(
        ProviderAuthorizationEvidenceSnapshot authorizationEvidence,
        ProviderTargetEvidence targetEvidence,
        string requiredScope)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (authorizationEvidence.CapturedAt == default
            || authorizationEvidence.CapturedAt > now)
        {
            return "authorization_evidence_malformed";
        }

        if (!string.Equals(authorizationEvidence.FreshnessClass, "fresh", StringComparison.OrdinalIgnoreCase)
            || now - authorizationEvidence.CapturedAt > MaximumAuthorizationAge)
        {
            return "authorization_evidence_stale";
        }

        if (targetEvidence.ObservedAt is { } observedAt && observedAt > now)
        {
            return "target_evidence_malformed";
        }

        if (targetEvidence.IsStale)
        {
            return "target_evidence_stale";
        }

        return targetEvidence.Metadata.TryGetValue("operation_scope", out string? operationScope)
            && string.Equals(operationScope, requiredScope, StringComparison.Ordinal)
                ? null
                : "forgejo_operation_scope_mismatch";
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

    private static ProviderRepositoryBindingResult RepositoryBindingFailure(
        ProviderRepositoryBindingRequest request,
        ProviderFailureCategory category,
        string reasonCode,
        TimeSpan? retryAfter = null)
        => ProviderRepositoryBindingResult.Failure(
            request,
            category,
            reasonCode,
            retryAfter,
            safeRemediationCode: category == ProviderFailureCategory.UnknownProviderOutcome
                ? "reconciliation_required_metadata_only"
                : $"{category.ToCategoryCode()}_remediation");
}
