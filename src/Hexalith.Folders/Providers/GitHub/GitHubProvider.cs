using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.GitHub;

public sealed partial class GitHubProvider : IGitProvider
{
    private readonly IGitHubCredentialResolver _credentialResolver;
    private readonly IGitHubApiClientFactory _apiClientFactory;
    private readonly IProviderRepositoryTargetResolver _targetResolver;
    private readonly IProviderOperationSourceResolver _operationSourceResolver;
    private readonly IProviderOperationOutcomeStore _operationOutcomeStore;
    private readonly TimeProvider _timeProvider;

    public GitHubProvider()
        : this(
            new UnconfiguredGitHubCredentialResolver(),
            new OctokitGitHubApiClientFactory(),
            new UnconfiguredProviderRepositoryTargetResolver(),
            new UnconfiguredProviderOperationSourceResolver(),
            new UnconfiguredProviderOperationOutcomeStore(),
            TimeProvider.System)
    {
    }

    internal GitHubProvider(
        IGitHubCredentialResolver credentialResolver,
        IGitHubApiClientFactory apiClientFactory)
        : this(
            credentialResolver,
            apiClientFactory,
            new UnconfiguredProviderRepositoryTargetResolver(),
            new UnconfiguredProviderOperationSourceResolver(),
            new UnconfiguredProviderOperationOutcomeStore(),
            TimeProvider.System)
    {
    }

    internal GitHubProvider(
        IGitHubCredentialResolver credentialResolver,
        IGitHubApiClientFactory apiClientFactory,
        IProviderRepositoryTargetResolver targetResolver)
        : this(
            credentialResolver,
            apiClientFactory,
            targetResolver,
            new UnconfiguredProviderOperationSourceResolver(),
            new UnconfiguredProviderOperationOutcomeStore(),
            TimeProvider.System)
    {
    }

    internal GitHubProvider(
        IGitHubCredentialResolver credentialResolver,
        IGitHubApiClientFactory apiClientFactory,
        IProviderRepositoryTargetResolver targetResolver,
        IProviderOperationSourceResolver operationSourceResolver,
        IProviderOperationOutcomeStore? operationOutcomeStore = null,
        TimeProvider? timeProvider = null)
    {
        _credentialResolver = credentialResolver ?? throw new ArgumentNullException(nameof(credentialResolver));
        _apiClientFactory = apiClientFactory ?? throw new ArgumentNullException(nameof(apiClientFactory));
        _targetResolver = targetResolver ?? throw new ArgumentNullException(nameof(targetResolver));
        _operationSourceResolver = operationSourceResolver ?? throw new ArgumentNullException(nameof(operationSourceResolver));
        _operationOutcomeStore = operationOutcomeStore ?? new UnconfiguredProviderOperationOutcomeStore();
        _timeProvider = timeProvider ?? TimeProvider.System;
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

        if (!GitHubCredentialModeValidator.TryGetSupportedMode(
            request.CredentialModeRequirements,
            out ProviderCredentialMode credentialMode,
            out string? credentialFailure))
        {
            return RepositoryFailure(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                credentialFailure ?? "unsupported_github_credential_mode");
        }

        if (!GitHubSafeTargetFingerprint.TryCreate(request, credentialMode, out ProviderTargetEvidence? safeTargetEvidence, out string? targetFailure))
        {
            return RepositoryFailure(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                targetFailure ?? "unsafe_github_target_metadata");
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
                "github_repository_target_resolution_unavailable");
        }

        if (!targetResolution.IsSuccess)
        {
            // The target resolver is an open seam that handles raw owner/repository/ref material.
            // Nothing it returns may reach a public result unfiltered (AC11).
            ProviderFailureCategory resolutionCategory = SafeRepositoryCategory(targetResolution.FailureCategory);
            return RepositoryFailure(
                request,
                resolutionCategory,
                SafeRepositoryReason(targetResolution.ReasonCode, resolutionCategory.ToCategoryCode()),
                SafeRetryAfter(targetResolution.RetryAfter));
        }

        // A success result with no target is a broken resolver, not an ambiguous provider outcome:
        // map it rather than throwing out of a method whose contract is to return a mapped result.
        if (targetResolution.Target is not { } resolvedTarget
            || !resolvedTarget.TryValidate(out string? resolvedTargetFailure))
        {
            return RepositoryFailure(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                "resolved_provider_target_malformed");
        }

        GitHubCredentialResolutionResult credentialResult = await _credentialResolver.ResolveAsync(
            new GitHubCredentialResolutionRequest(
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

        GitHubCredentialLease credential = credentialResult.Credential.ShouldNotBeNullForProvider();
        GitHubRepositoryCreationResult result;
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

            result = await client.CreateRepositoryAsync(
                new GitHubRepositoryCreationRequest(
                    request.ManagedTenantId,
                    request.OrganizationId,
                    request.ProviderBindingRef,
                    request.RepositoryBindingId,
                    resolvedTarget,
                    credentialMode,
                    GitHubProviderConstants.RestApiVersion,
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
                "github_repository_creation_outcome_unknown");
        }
        catch (Exception)
        {
            return RepositoryFailure(
                request,
                ProviderFailureCategory.UnknownProviderOutcome,
                "github_repository_creation_outcome_unknown");
        }
        finally
        {
            await credential.DisposeAsync().ConfigureAwait(false);
        }

        return result.IsSuccess
            ? ProviderRepositoryCreationResult.Success(
                request,
                result.EquivalentExisting,
                safeTargetEvidence.Metadata["safe_target_fingerprint"],
                SafeCanonicalRepositoryId(result.CanonicalRepositoryId))
            : GitHubFailureMapper.ToProviderFailure(result, request);
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

        if (!GitHubCredentialModeValidator.TryGetSupportedMode(
            request.CredentialModeRequirements,
            out ProviderCredentialMode credentialMode,
            out string? credentialFailure))
        {
            return RepositoryBindingFailure(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                credentialFailure ?? "unsupported_github_credential_mode");
        }

        if (!GitHubSafeTargetFingerprint.TryCreate(request, credentialMode, out ProviderTargetEvidence? safeTargetEvidence, out string? targetFailure))
        {
            return RepositoryBindingFailure(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                targetFailure ?? "unsafe_github_target_metadata");
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
                "github_repository_target_resolution_unavailable");
        }

        if (!targetResolution.IsSuccess)
        {
            // The target resolver is an open seam that handles raw owner/repository/ref material.
            // Nothing it returns may reach a public result unfiltered (AC11).
            ProviderFailureCategory resolutionCategory = SafeRepositoryCategory(targetResolution.FailureCategory);
            return RepositoryBindingFailure(
                request,
                resolutionCategory,
                SafeRepositoryReason(targetResolution.ReasonCode, resolutionCategory.ToCategoryCode()),
                SafeRetryAfter(targetResolution.RetryAfter));
        }

        if (targetResolution.Target is not { } resolvedTarget
            || !resolvedTarget.TryValidate(out string? resolvedTargetFailure))
        {
            return RepositoryBindingFailure(
                request,
                ProviderFailureCategory.ProviderValidationFailed,
                "resolved_provider_target_malformed");
        }

        GitHubCredentialResolutionResult credentialResult = await _credentialResolver.ResolveAsync(
            new GitHubCredentialResolutionRequest(
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

        GitHubCredentialLease credential = credentialResult.Credential.ShouldNotBeNullForProvider();
        GitHubRepositoryBindingResult result;
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

            result = await client.ValidateRepositoryBindingAsync(
                new GitHubRepositoryBindingRequest(
                    request.ManagedTenantId,
                    request.OrganizationId,
                    request.ProviderBindingRef,
                    request.RepositoryBindingId,
                    resolvedTarget,
                    credentialMode,
                    GitHubProviderConstants.RestApiVersion,
                    safeTargetEvidence.Metadata["safe_target_fingerprint"],
                    request.CorrelationId,
                    request.IdempotencyKey),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return RepositoryBindingFailure(
                request,
                ProviderFailureCategory.UnknownProviderOutcome,
                "github_repository_binding_outcome_unknown");
        }
        catch (Exception)
        {
            return RepositoryBindingFailure(
                request,
                ProviderFailureCategory.UnknownProviderOutcome,
                "github_repository_binding_outcome_unknown");
        }
        finally
        {
            await credential.DisposeAsync().ConfigureAwait(false);
        }

        return result.IsSuccess
            ? ProviderRepositoryBindingResult.Success(
                request,
                result.EquivalentExisting,
                safeTargetEvidence.Metadata["safe_target_fingerprint"],
                SafeCanonicalRepositoryId(result.CanonicalRepositoryId))
            : GitHubFailureMapper.ToProviderFailure(result, request);
    }

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

    /// <summary>
    /// Mirrors the Story 3.11 mutation boundary: a malformed admission must become a canonical,
    /// metadata-only validation failure rather than dereferencing into an unmapped exception.
    /// </summary>
    private static bool IsAdmissionWellFormed(ProviderIdempotencyAdmission? admission)
        => admission is not null
            && Enum.IsDefined(admission.Disposition)
            && IsSafeOpaqueValue(admission.IntentFingerprint);

    /// <summary>
    /// Repository-scoped reason codes that may be replayed from a durable admission. The Story 3.11
    /// operation allow-list covers every shared code; this set adds only the create/bind additions.
    /// Both sets are consulted together so one durable producer satisfies every gate on this provider.
    /// </summary>
    private static readonly HashSet<string> AllowedRepositoryReasonCodes = new(StringComparer.Ordinal)
    {
        "authorization_evidence_stale",
        "github_administration_permission_insufficient",
        "github_branch_or_ref_missing",
        "github_contents_permission_insufficient",
        "github_default_branch_conflict",
        "github_ref_operation_unsupported",
        "github_repository_binding_outcome_unknown",
        "github_repository_conflict",
        "github_repository_creation_outcome_unknown",
        "github_repository_target_resolution_unavailable",
        "github_repository_target_unavailable",
        "github_replay_evidence_malformed",
        "github_unmapped_outcome",
        "provider_identity_malformed",
        "provider_repository_binding_target_unconfigured",
        "provider_repository_creation_target_unconfigured",
        "resolved_provider_target_malformed",
        "target_evidence_stale",
        "unsafe_github_target_metadata",
        "unsupported_github_credential_mode",
        "unsupported_provider_family",
    };

    /// <summary>
    /// Determines whether a reason code is safe to surface on a repository result. Consults the
    /// Story 3.11 operation allow-list first so the two slices cannot drift apart (DW-299).
    /// </summary>
    private static bool IsRepositoryReasonAllowed(string? reasonCode)
        => reasonCode is not null
            && (AllowedOperationReasonCodes.Contains(reasonCode) || AllowedRepositoryReasonCodes.Contains(reasonCode));

    /// <summary>
    /// Reduces an untrusted reason code to an allow-listed one. The target resolver is an open seam
    /// whose implementations handle raw owner/repository/ref material, so nothing it returns may
    /// reach a public result unfiltered (AC11).
    /// </summary>
    private static string SafeRepositoryReason(string? reasonCode, string fallback)
        => IsRepositoryReasonAllowed(reasonCode) ? reasonCode! : fallback;

    /// <summary>
    /// Reduces an untrusted failure category to a defined, non-success one.
    /// </summary>
    private static ProviderFailureCategory SafeRepositoryCategory(ProviderFailureCategory category)
        => Enum.IsDefined(category) && category != ProviderFailureCategory.None
            ? category
            : ProviderFailureCategory.ProviderFailureKnown;

    /// <summary>
    /// Replay evidence must prove the exact prior terminal outcome before it can be trusted. These are
    /// the same per-disposition rules the Story 3.11 gate applies, so a single durable admission from
    /// Story 12.6 is accepted or rejected identically by every gate on this provider (DW-299).
    /// </summary>
    private static bool IsReplayEvidenceWellFormed(ProviderIdempotencyAdmission admission)
    {
        if (admission.Disposition != ProviderIdempotencyDisposition.EquivalentReplay)
        {
            return true;
        }

        if (admission.PriorOutcomeDisposition is null
            || !Enum.IsDefined(admission.PriorOutcomeDisposition.Value)
            || !IsSafeOpaqueReference(admission.PriorOperationReference))
        {
            return false;
        }

        return admission.PriorOutcomeDisposition switch
        {
            ProviderPriorOutcomeDisposition.Success => IsSafeFingerprint(admission.PriorSafeOutcomeFingerprint)
                && admission.PriorReconciliationReference is null,
            ProviderPriorOutcomeDisposition.Unknown => IsSafeOpaqueReference(admission.PriorReconciliationReference),
            ProviderPriorOutcomeDisposition.KnownFailure => admission.PriorFailureCategory != ProviderFailureCategory.None
                && Enum.IsDefined(admission.PriorFailureCategory)
                && IsSafeFingerprint(admission.PriorSafeOutcomeFingerprint)
                && IsRepositoryReasonAllowed(admission.PriorReasonCode)
                && (admission.PriorRemediationCode is null || AllowedOperationRemediationCodes.Contains(admission.PriorRemediationCode))
                && (admission.PriorRetryAfter is null || SafeRetryAfter(admission.PriorRetryAfter) == admission.PriorRetryAfter),
            _ => false,
        };
    }

    /// <summary>
    /// Determines whether an opaque admission value is safe to process at the provider boundary.
    /// </summary>
    private static bool IsSafeOpaqueValue(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length <= 512
            && !value.Contains("://", StringComparison.Ordinal)
            && !value.Any(char.IsControl);

    /// <summary>
    /// The reserved platform tenant must never own a managed folder stream, so it must never reach a
    /// provider mutation either.
    /// </summary>
    private static bool IsReservedTenant(string? managedTenantId)
        => string.Equals(managedTenantId, "system", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reduces an observed provider repository identity to a bounded, metadata-only token. GitHub
    /// repository ids are numeric; anything else is dropped rather than surfaced.
    /// </summary>
    private static string? SafeCanonicalRepositoryId(string? canonicalRepositoryId)
        => canonicalRepositoryId is { Length: > 0 and <= 64 }
            && canonicalRepositoryId.All(char.IsAsciiDigit)
                ? canonicalRepositoryId
                : null;

    /// <summary>
    /// Determines whether a value is a canonical lowercase SHA-256 fingerprint.
    /// </summary>
    private static bool IsSafeFingerprint(string? value)
        => value is { Length: 64 }
            && value.All(static character => char.IsAsciiDigit(character) || character is >= 'a' and <= 'f');

    /// <summary>
    /// Enforces the caller-supplied durable idempotency admission before any target, credential,
    /// client, or GitHub access. Story 3.10 AC7; mirrors the Story 3.11 mutation gate.
    /// </summary>
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

    /// <summary>
    /// Returns the exact prior terminal outcome carried by an equivalent replay. A prior ambiguous
    /// mutation must stay ambiguous: collapsing it to success would assert that the repository exists
    /// when no evidence proves it, which is precisely what AC9 forbids.
    /// </summary>
    private static ProviderRepositoryCreationResult Replay(
        ProviderRepositoryCreationRequest request,
        string safeTargetFingerprint,
        ProviderIdempotencyAdmission admission)
        => admission.PriorOutcomeDisposition switch
        {
            ProviderPriorOutcomeDisposition.Unknown => ProviderRepositoryCreationResult.Failure(
                request,
                ProviderFailureCategory.UnknownProviderOutcome,
                SafeRepositoryReason(admission.PriorReasonCode, "github_repository_creation_outcome_unknown"),
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
                canonicalRepositoryId: null,
                priorSafeOutcomeFingerprint: admission.PriorSafeOutcomeFingerprint,
                priorOperationReference: admission.PriorOperationReference),
        };

    /// <summary>
    /// Enforces the caller-supplied durable idempotency admission for existing-repository binding
    /// before any target, credential, client, or GitHub access.
    /// </summary>
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

    /// <summary>
    /// Returns the exact prior terminal outcome carried by an equivalent replay of a binding intent.
    /// </summary>
    private static ProviderRepositoryBindingResult Replay(
        ProviderRepositoryBindingRequest request,
        string safeTargetFingerprint,
        ProviderIdempotencyAdmission admission)
        => admission.PriorOutcomeDisposition switch
        {
            ProviderPriorOutcomeDisposition.Unknown => ProviderRepositoryBindingResult.Failure(
                request,
                ProviderFailureCategory.UnknownProviderOutcome,
                SafeRepositoryReason(admission.PriorReasonCode, "github_repository_binding_outcome_unknown"),
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
                canonicalRepositoryId: null,
                priorSafeOutcomeFingerprint: admission.PriorSafeOutcomeFingerprint,
                priorOperationReference: admission.PriorOperationReference),
        };

    private static ProviderRepositoryCreationResult? ValidateBoundary(ProviderRepositoryCreationRequest request)
    {
        try
        {
            string providerFamily = ProviderIdentityIdentifier.Normalize(request.ProviderFamily);
            string providerKey = ProviderIdentityIdentifier.Normalize(request.ProviderKey);
            if (!string.Equals(providerFamily, GitHubProviderConstants.ProviderFamily, StringComparison.Ordinal)
                || !string.Equals(providerKey, GitHubProviderConstants.ProviderKey, StringComparison.Ordinal))
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
            return RepositoryFailure(request, ProviderFailureCategory.ProviderValidationFailed, "github_operation_evidence_malformed");
        }

        if (!string.Equals(request.AuthorizationEvidence.FreshnessClass, "fresh", StringComparison.OrdinalIgnoreCase))
        {
            return RepositoryFailure(request, ProviderFailureCategory.ReconciliationRequired, "authorization_evidence_stale");
        }

        if (request.TargetEvidence.IsStale)
        {
            return RepositoryFailure(request, ProviderFailureCategory.ReconciliationRequired, "target_evidence_stale");
        }
        if (!IsAdmissionWellFormed(request.IdempotencyAdmission))
        {
            return RepositoryFailure(request, ProviderFailureCategory.ProviderValidationFailed, "github_mutation_intent_malformed");
        }

        if (!IsReplayEvidenceWellFormed(request.IdempotencyAdmission))
        {
            return RepositoryFailure(request, ProviderFailureCategory.ProviderValidationFailed, "github_replay_evidence_malformed");
        }

        return null;
    }

    private static ProviderRepositoryBindingResult? ValidateBoundary(ProviderRepositoryBindingRequest request)
    {
        try
        {
            string providerFamily = ProviderIdentityIdentifier.Normalize(request.ProviderFamily);
            string providerKey = ProviderIdentityIdentifier.Normalize(request.ProviderKey);
            if (!string.Equals(providerFamily, GitHubProviderConstants.ProviderFamily, StringComparison.Ordinal)
                || !string.Equals(providerKey, GitHubProviderConstants.ProviderKey, StringComparison.Ordinal))
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
            return RepositoryBindingFailure(request, ProviderFailureCategory.ProviderValidationFailed, "github_operation_evidence_malformed");
        }

        if (!string.Equals(request.AuthorizationEvidence.FreshnessClass, "fresh", StringComparison.OrdinalIgnoreCase))
        {
            return RepositoryBindingFailure(request, ProviderFailureCategory.ReconciliationRequired, "authorization_evidence_stale");
        }

        if (request.TargetEvidence.IsStale)
        {
            return RepositoryBindingFailure(request, ProviderFailureCategory.ReconciliationRequired, "target_evidence_stale");
        }
        if (!IsAdmissionWellFormed(request.IdempotencyAdmission))
        {
            return RepositoryBindingFailure(request, ProviderFailureCategory.ProviderValidationFailed, "github_mutation_intent_malformed");
        }

        if (!IsReplayEvidenceWellFormed(request.IdempotencyAdmission))
        {
            return RepositoryBindingFailure(request, ProviderFailureCategory.ProviderValidationFailed, "github_replay_evidence_malformed");
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
