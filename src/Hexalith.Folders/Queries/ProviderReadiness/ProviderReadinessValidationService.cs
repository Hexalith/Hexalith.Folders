using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Queries.ProviderReadiness;

public sealed partial class ProviderReadinessValidationService(
    TenantAccessAuthorizer tenantAccessAuthorizer,
    IProviderReadinessBindingReader bindingReader,
    ProviderCapabilityDiscoveryService discoveryService,
    IProviderReadinessEvidenceStore evidenceStore,
    IUtcClock clock)
{
    public const string ReadActionToken = "provider_readiness_read";

    private const string SnapshotPerTask = "snapshot_per_task";
    private const string Ready = "ready";
    private const string Degraded = "degraded";
    private const string Failed = "failed";
    private const string Supported = "supported";
    private const string Unsupported = "unsupported";
    private const string TemporarilyUnavailable = "temporarily_unavailable";
    private const string DocumentedFailureBehavior = "documented";
    private const string RetryAfterBackoffFailureBehavior = "retry_after_backoff";
    private const string ProviderProfileSchemaVersion = "v1";

    private static readonly JsonSerializerOptions DiagnosticJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly TenantAccessAuthorizer _tenantAccessAuthorizer = tenantAccessAuthorizer ?? throw new ArgumentNullException(nameof(tenantAccessAuthorizer));
    private readonly IProviderReadinessBindingReader _bindingReader = bindingReader ?? throw new ArgumentNullException(nameof(bindingReader));
    private readonly ProviderCapabilityDiscoveryService _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
    private readonly IProviderReadinessEvidenceStore _evidenceStore = evidenceStore ?? throw new ArgumentNullException(nameof(evidenceStore));
    private readonly IUtcClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public async Task<ProviderReadinessValidationResult> ValidateAsync(
        ProviderReadinessValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ClaimTransformEvidence);

        string correlationId = SafeCorrelationId(request.CorrelationId);
        ProviderReadinessFreshness freshness = Freshness(correlationId, projectionWatermark: null, stale: false);

        if (!IsCanonicalIdentifier(request.ProviderBindingRef))
        {
            return Result(
                ProviderReadinessResultCode.ValidationFailed,
                Failed,
                ProviderFailureCategory.ProviderValidationFailed,
                "malformed_provider_readiness_request",
                retryable: false,
                retryAfter: null,
                remediationCategory: "no_action",
                correlationId,
                providerReference: null,
                providerBindingRef: null,
                capabilityProfileRef: null,
                evidence: null,
                freshness);
        }

        if (string.IsNullOrWhiteSpace(request.AuthoritativeTenantId)
            || string.IsNullOrWhiteSpace(request.PrincipalId))
        {
            return Result(
                ProviderReadinessResultCode.AuthenticationRequired,
                Failed,
                ProviderFailureCategory.ProviderPermissionInsufficient,
                "authentication_required",
                retryable: false,
                retryAfter: null,
                remediationCategory: "contact_operator",
                correlationId,
                providerReference: null,
                providerBindingRef: null,
                capabilityProfileRef: null,
                evidence: null,
                freshness);
        }

        string managedTenantId = request.AuthoritativeTenantId.Trim();
        string principalId = request.PrincipalId.Trim();
        if (string.Equals(managedTenantId, "system", StringComparison.OrdinalIgnoreCase)
            || HasClientControlledMismatch(managedTenantId, request.ClientControlledTenantValues)
            || !IsClaimTransformEvidenceValid(request.ClaimTransformEvidence, managedTenantId, principalId))
        {
            return Result(
                ProviderReadinessResultCode.AuthorizationDenied,
                Failed,
                ProviderFailureCategory.ProviderPermissionInsufficient,
                "provider_readiness_read_denied",
                retryable: false,
                retryAfter: null,
                remediationCategory: "contact_operator",
                correlationId,
                providerReference: null,
                providerBindingRef: null,
                capabilityProfileRef: null,
                evidence: null,
                freshness);
        }

        TenantAccessAuthorizationResult tenantAccess = await _tenantAccessAuthorizer.AuthorizeMutationAsync(
            new TenantAccessAuthorizationContext(managedTenantId, principalId, RequestedTenantId: managedTenantId),
            cancellationToken).ConfigureAwait(false);

        if (!tenantAccess.IsAllowed)
        {
            return TenantDeniedResult(tenantAccess, correlationId);
        }

        string providerBindingRef = request.ProviderBindingRef!.Trim();
        OrganizationProviderBinding? binding = await _bindingReader.GetAsync(
            new ProviderReadinessBindingReadRequest(managedTenantId, providerBindingRef, correlationId),
            cancellationToken).ConfigureAwait(false);

        if (binding is null)
        {
            ProviderReadinessValidationResult missing = Result(
                ProviderReadinessResultCode.Allowed,
                Failed,
                ProviderFailureCategory.ProviderConfigurationMissing,
                ProviderFailureCategory.ProviderConfigurationMissing.ToCategoryCode(),
                retryable: false,
                retryAfter: null,
                remediationCategory: "fix_provider_configuration",
                correlationId,
                providerReference: providerBindingRef,
                providerBindingRef,
                capabilityProfileRef: null,
                evidence: null,
                Freshness(correlationId, tenantAccess.ProjectionWatermark, stale: false));

            await StoreAsync(missing, managedTenantId, organizationId: null, providerFamily: null, providerKey: null, cancellationToken).ConfigureAwait(false);
            return missing;
        }

        if (!string.Equals(binding.ManagedTenantId, managedTenantId, StringComparison.Ordinal)
            || !string.Equals(binding.ProviderBindingRef, providerBindingRef, StringComparison.Ordinal))
        {
            ProviderReadinessValidationResult mismatched = Result(
                ProviderReadinessResultCode.Allowed,
                Failed,
                ProviderFailureCategory.ReconciliationRequired,
                ProviderFailureCategory.ReconciliationRequired.ToCategoryCode(),
                retryable: false,
                retryAfter: null,
                remediationCategory: "reconciliation_required",
                correlationId,
                providerReference: providerBindingRef,
                providerBindingRef,
                capabilityProfileRef: null,
                evidence: null,
                Freshness(correlationId, tenantAccess.ProjectionWatermark, stale: true));

            await StoreAsync(mismatched, managedTenantId, binding.OrganizationId, null, null, cancellationToken).ConfigureAwait(false);
            return mismatched;
        }

        ProviderCapabilityDiscoveryRequest discoveryRequest = BuildDiscoveryRequest(
            binding,
            tenantAccess,
            principalId,
            correlationId);

        ProviderCapabilityDiscoveryResult discovery = await _discoveryService.DiscoverCapabilitiesAsync(
            discoveryRequest,
            cancellationToken).ConfigureAwait(false);

        ProviderReadinessValidationResult result = discovery.IsSuccess
            ? MapProfile(discovery.Profile!, request.RequestedCapability, correlationId, tenantAccess.ProjectionWatermark)
            : MapFailure(discovery, providerBindingRef, tenantAccess.ProjectionWatermark);

        await StoreAsync(
            result,
            managedTenantId,
            binding.OrganizationId,
            binding.ProviderKind,
            binding.ProviderKind,
            cancellationToken).ConfigureAwait(false);

        return result;
    }

    private ProviderReadinessValidationResult TenantDeniedResult(TenantAccessAuthorizationResult tenantAccess, string correlationId)
    {
        ProviderReadinessResultCode code = tenantAccess.Outcome switch
        {
            TenantAccessOutcome.StaleProjection => ProviderReadinessResultCode.ProjectionStale,
            TenantAccessOutcome.UnavailableProjection => ProviderReadinessResultCode.ProjectionUnavailable,
            _ => ProviderReadinessResultCode.AuthorizationDenied,
        };

        string reasonCode = tenantAccess.Outcome switch
        {
            TenantAccessOutcome.StaleProjection => "projection_stale",
            TenantAccessOutcome.UnavailableProjection => "projection_unavailable",
            TenantAccessOutcome.MalformedEvidence or TenantAccessOutcome.ReplayConflict => "authorization_evidence_malformed",
            _ => "tenant_access_denied",
        };

        bool retryable = tenantAccess.Outcome is TenantAccessOutcome.StaleProjection or TenantAccessOutcome.UnavailableProjection;
        return Result(
            code,
            Failed,
            retryable ? ProviderFailureCategory.ProviderTransientFailure : ProviderFailureCategory.ProviderPermissionInsufficient,
            reasonCode,
            retryable,
            retryAfter: null,
            remediationCategory: retryable ? "retry_later" : "contact_operator",
            correlationId,
            providerReference: null,
            providerBindingRef: null,
            capabilityProfileRef: null,
            evidence: null,
            Freshness(correlationId, tenantAccess.ProjectionWatermark, stale: retryable));
    }

    private static ProviderCapabilityDiscoveryRequest BuildDiscoveryRequest(
        OrganizationProviderBinding binding,
        TenantAccessAuthorizationResult tenantAccess,
        string principalId,
        string correlationId)
        => new(
            binding.ManagedTenantId,
            binding.OrganizationId,
            binding.ProviderBindingRef,
            binding.ProviderKind,
            binding.ProviderKind,
            ProviderProfileSchemaVersion,
            BuildTargetEvidence(binding),
            CredentialModes(binding),
            new ProviderAuthorizationEvidenceSnapshot(
                AuthorizationFingerprint(binding, tenantAccess, principalId),
                tenantAccess.LastEventTimestamp ?? binding.OccurredAt,
                tenantAccess.FreshnessStatus == TenantProjectionFreshnessStatus.Fresh ? "fresh" : "stale"),
            correlationId);

    private static ProviderTargetEvidence BuildTargetEvidence(OrganizationProviderBinding binding)
    {
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["binding_status"] = SafeToken(binding.ConfiguredStatus),
            ["credential_mode"] = CredentialModes(binding)[0].ToString().ToLowerInvariant(),
            ["naming_policy_fingerprint"] = PolicyFingerprint(binding.NamingPolicy),
            ["branch_policy_fingerprint"] = PolicyFingerprint(binding.BranchPolicy),
        };

        AddPolicyRef(metadata, "naming_policy_ref", binding.NamingPolicy.PolicyRef);
        AddPolicyRef(metadata, "branch_policy_ref", binding.BranchPolicy.PolicyRef);
        AddSafeMetadataFlag(metadata, "naming_policy_metadata_present", binding.NamingPolicy.Metadata);
        AddSafeMetadataFlag(metadata, "branch_policy_metadata_present", binding.BranchPolicy.Metadata);
        AddAllowedTargetMetadata(metadata, binding);

        return new ProviderTargetEvidence(
            Product: SafeToken(binding.ProviderKind),
            ProductVersion: TargetProductVersion(binding),
            ApiSurfaceVersion: "provider_api_metadata_only",
            EvidenceVersion: "provider_readiness_v1",
            IsStale: !string.Equals(binding.ConfiguredStatus, "configured", StringComparison.Ordinal),
            ObservedAt: binding.OccurredAt,
            Metadata: metadata);
    }

    private static ProviderReadinessValidationResult MapProfile(
        ProviderCapabilityProfile profile,
        ProviderReadinessRequestedCapability requestedCapability,
        string correlationId,
        string? projectionWatermark)
    {
        IReadOnlyList<string> required = RequiredOperations(requestedCapability);
        IReadOnlyDictionary<string, ProviderOperationCapability> operations = profile.Operations
            .GroupBy(static operation => operation.OperationId, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);

        bool failed = false;
        bool degraded = false;
        foreach (string operationId in required)
        {
            if (!operations.TryGetValue(operationId, out ProviderOperationCapability? operation))
            {
                failed = true;
                continue;
            }

            switch (operation.Support)
            {
                case ProviderOperationSupport.Supported:
                    break;
                case ProviderOperationSupport.Partial:
                case ProviderOperationSupport.Emulated:
                    degraded = true;
                    break;
                default:
                    failed = true;
                    break;
            }
        }

        string status = failed ? Failed : degraded ? Degraded : Ready;
        if (status == Ready && !HasSafeRateLimitPosture(profile.RateLimit))
        {
            status = Failed;
        }

        ProviderFailureCategory category = status switch
        {
            Ready => ProviderFailureCategory.None,
            Degraded => ProviderFailureCategory.ProviderReadinessFailed,
            _ => ProviderFailureCategory.UnsupportedProviderCapability,
        };
        string reasonCode = status switch
        {
            Ready => "success",
            Degraded => "required_capability_degraded",
            _ when !HasSafeRateLimitPosture(profile.RateLimit) => "rate_limit_posture_missing",
            _ => "unsupported_provider_capability",
        };

        bool retryable = false;
        TimeSpan? retryAfter = null;
        string remediationCategory = status switch
        {
            Ready => "none",
            Degraded => "fix_provider_configuration",
            _ => "contact_operator",
        };

        string capabilityProfileRef = ToCapabilityProfileRef(profile.Fingerprint);
        return Result(
            ProviderReadinessResultCode.Allowed,
            status,
            category,
            reasonCode,
            retryable,
            retryAfter,
            remediationCategory,
            correlationId,
            providerReference: profile.ProviderBindingRef,
            providerBindingRef: profile.ProviderBindingRef,
            capabilityProfileRef,
            ToEvidence(profile),
            new ProviderReadinessFreshness(
                SnapshotPerTask,
                profile.TargetEvidence.ObservedAt ?? DateTimeOffset.UtcNow,
                projectionWatermark ?? profile.AuthorizationEvidenceFingerprint,
                Stale: false));
    }

    private static ProviderReadinessValidationResult MapFailure(
        ProviderCapabilityDiscoveryResult discovery,
        string providerBindingRef,
        string? projectionWatermark)
    {
        string status = discovery.FailureCategory.IsRetryableByDefault() ? Degraded : Failed;
        return Result(
            ProviderReadinessResultCode.Allowed,
            status,
            discovery.FailureCategory,
            discovery.ReasonCode,
            discovery.Retryable,
            discovery.RetryAfter,
            ToRemediationCategory(discovery.FailureCategory),
            discovery.CorrelationId,
            providerReference: providerBindingRef,
            providerBindingRef,
            capabilityProfileRef: discovery.ProfileVersion is null ? null : ToCapabilityProfileRef(discovery.ProfileVersion.ProfileFingerprint),
            evidence: null,
            new ProviderReadinessFreshness(
                SnapshotPerTask,
                DateTimeOffset.UtcNow,
                projectionWatermark,
                Stale: discovery.FailureCategory is ProviderFailureCategory.ReconciliationRequired));
    }

    private static ProviderReadinessValidationResult Result(
        ProviderReadinessResultCode code,
        string status,
        ProviderFailureCategory category,
        string reasonCode,
        bool retryable,
        TimeSpan? retryAfter,
        string remediationCategory,
        string correlationId,
        string? providerReference,
        string? providerBindingRef,
        string? capabilityProfileRef,
        ProviderReadinessCapabilityEvidence? evidence,
        ProviderReadinessFreshness freshness)
    {
        string categoryCode = category.ToCategoryCode();
        string safeReasonCode = string.IsNullOrWhiteSpace(reasonCode) ? categoryCode : SafeReasonCode(reasonCode);
        string safeRemediationCode = category == ProviderFailureCategory.None
            ? "none"
            : $"{categoryCode}_remediation";

        return new(
            code,
            status,
            safeReasonCode,
            safeRemediationCode,
            retryable,
            retryAfter,
            remediationCategory,
            correlationId,
            providerReference,
            providerBindingRef,
            capabilityProfileRef,
            evidence,
            freshness,
            category,
            categoryCode);
    }

    private async Task StoreAsync(
        ProviderReadinessValidationResult result,
        string managedTenantId,
        string? organizationId,
        string? providerFamily,
        string? providerKey,
        CancellationToken cancellationToken)
    {
        ProviderReadinessEvidenceRecord record = new(
            managedTenantId,
            organizationId,
            result.ProviderBindingRef ?? "provider_binding_unavailable",
            providerFamily is null ? null : SafeToken(providerFamily),
            providerKey is null ? null : SafeToken(providerKey),
            result.CapabilityProfileRef,
            result.Status,
            result.ReasonCode,
            result.Retryable,
            result.RemediationCategory,
            result.Freshness.ObservedAt,
            result.Freshness.ProjectionWatermark,
            result.CorrelationId,
            JsonSerializer.Serialize(result, DiagnosticJsonOptions));

        await _evidenceStore.StoreAsync(record, cancellationToken).ConfigureAwait(false);
    }

    private ProviderReadinessFreshness Freshness(string correlationId, string? projectionWatermark, bool stale)
    {
        _ = correlationId;
        return new(SnapshotPerTask, _clock.UtcNow, projectionWatermark, stale);
    }

    private static IReadOnlyList<string> RequiredOperations(ProviderReadinessRequestedCapability requestedCapability)
    {
        List<string> required =
        [
            ProviderOperationCatalog.ReadinessValidation,
            ProviderOperationCatalog.BranchRefInspection,
            ProviderOperationCatalog.FileMutationSupport,
            ProviderOperationCatalog.CommitSupport,
            ProviderOperationCatalog.StatusQuery,
            ProviderOperationCatalog.ProviderSupportEvidence,
        ];

        switch (requestedCapability)
        {
            case ProviderReadinessRequestedCapability.ExistingRepositoryBinding:
                required.Add(ProviderOperationCatalog.RepositoryBinding);
                break;
            case ProviderReadinessRequestedCapability.RepositoryCreation:
                required.Add(ProviderOperationCatalog.RepositoryCreation);
                break;
            case ProviderReadinessRequestedCapability.BranchRefPolicy:
                break;
            case ProviderReadinessRequestedCapability.WorkspacePreparation:
                required.Add(ProviderOperationCatalog.WorkspacePreparation);
                break;
            default:
                required.Add(ProviderOperationCatalog.RepositoryCreation);
                break;
        }

        return required;
    }

    private static ProviderReadinessCapabilityEvidence ToEvidence(ProviderCapabilityProfile profile)
        => new(
            OperationState(profile, ProviderOperationCatalog.RepositoryCreation),
            OperationState(profile, ProviderOperationCatalog.RepositoryBinding),
            OperationState(profile, ProviderOperationCatalog.BranchRefInspection),
            OperationState(profile, ProviderOperationCatalog.FileMutationSupport),
            CombinedOperationState(profile, ProviderOperationCatalog.CommitSupport, ProviderOperationCatalog.StatusQuery),
            OperationState(profile, ProviderOperationCatalog.ProviderSupportEvidence),
            profile.RateLimit.Retryable ? RetryAfterBackoffFailureBehavior : DocumentedFailureBehavior);

    private static string OperationState(ProviderCapabilityProfile profile, string operationId)
    {
        ProviderOperationCapability? operation = profile.Operations.FirstOrDefault(
            x => string.Equals(x.OperationId, operationId, StringComparison.Ordinal));
        return operation?.Support switch
        {
            ProviderOperationSupport.Supported => Supported,
            ProviderOperationSupport.Partial or ProviderOperationSupport.Emulated or ProviderOperationSupport.Unavailable => TemporarilyUnavailable,
            _ => Unsupported,
        };
    }

    private static string CombinedOperationState(ProviderCapabilityProfile profile, string first, string second)
    {
        string firstState = OperationState(profile, first);
        string secondState = OperationState(profile, second);
        if (firstState == Supported && secondState == Supported)
        {
            return Supported;
        }

        return firstState == Unsupported || secondState == Unsupported ? Unsupported : TemporarilyUnavailable;
    }

    private static string ToRemediationCategory(ProviderFailureCategory category)
        => category switch
        {
            ProviderFailureCategory.None => "none",
            ProviderFailureCategory.ProviderUnavailable
                or ProviderFailureCategory.ProviderRateLimited
                or ProviderFailureCategory.ProviderTransientFailure => "retry_later",
            ProviderFailureCategory.ProviderAuthenticationRequired => "fix_credential_reference",
            ProviderFailureCategory.ProviderConfigurationMissing
                or ProviderFailureCategory.ProviderValidationFailed => "fix_provider_configuration",
            ProviderFailureCategory.ReconciliationRequired => "reconciliation_required",
            _ => "contact_operator",
        };

    private static IReadOnlyList<ProviderCredentialMode> CredentialModes(OrganizationProviderBinding binding)
        => string.Equals(binding.ProviderKind, "forgejo", StringComparison.Ordinal)
            ? [ProviderCredentialMode.UserDelegatedReference]
            : [ProviderCredentialMode.AppInstallationReference];

    private static string TargetProductVersion(OrganizationProviderBinding binding)
    {
        foreach (string key in new[] { "provider_product_version", "product_version", "snapshot_version", "forgejo_snapshot_version" })
        {
            if (TryGetSafePolicyMetadata(binding, key, out string? value))
            {
                return value!;
            }
        }

        return "provider_binding_v1";
    }

    private static void AddAllowedTargetMetadata(Dictionary<string, string> metadata, OrganizationProviderBinding binding)
    {
        AddAllowedTargetMetadata(metadata, binding, "safe_target_fingerprint");
        AddAllowedTargetMetadata(metadata, binding, "operation_scope");

        if (string.Equals(binding.ProviderKind, "forgejo", StringComparison.Ordinal))
        {
            AddAllowedTargetMetadata(metadata, binding, "authorized_base_url");
        }
    }

    private static void AddAllowedTargetMetadata(
        Dictionary<string, string> metadata,
        OrganizationProviderBinding binding,
        string key)
    {
        if (TryGetSafePolicyMetadata(binding, key, out string? value))
        {
            metadata[key] = value!;
        }
    }

    private static bool TryGetSafePolicyMetadata(
        OrganizationProviderBinding binding,
        string key,
        out string? value)
    {
        if (TryGetSafePolicyMetadata(binding.NamingPolicy.Metadata, key, out value)
            || TryGetSafePolicyMetadata(binding.BranchPolicy.Metadata, key, out value))
        {
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryGetSafePolicyMetadata(
        IReadOnlyDictionary<string, string> metadata,
        string key,
        out string? value)
    {
        if (metadata.TryGetValue(key, out string? candidate)
            && IsSafeTargetMetadataValue(key, candidate))
        {
            value = candidate.Trim();
            return true;
        }

        value = null;
        return false;
    }

    private static bool HasSafeRateLimitPosture(ProviderRateLimitPosture rateLimit)
        => IsSafeTargetMetadataValue("rate_limit_classification", rateLimit.Classification);

    private static bool IsSafeTargetMetadataValue(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmed = value.Trim();
        if (trimmed.Length > 256)
        {
            return false;
        }

        return key == "authorized_base_url"
            ? Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri)
                && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
                && string.IsNullOrEmpty(uri.UserInfo)
                && string.IsNullOrEmpty(uri.Query)
                && string.IsNullOrEmpty(uri.Fragment)
            : !ContainsUnsafeTargetMetadataValue(trimmed)
                && (CanonicalIdentifierPattern().IsMatch(trimmed)
                || SafeMetadataTokenPattern().IsMatch(trimmed));
    }

    private static bool ContainsUnsafeTargetMetadataValue(string value)
        => value.Contains("token", StringComparison.OrdinalIgnoreCase)
            || value.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || value.Contains("password", StringComparison.OrdinalIgnoreCase)
            || value.Contains("://", StringComparison.Ordinal)
            || value.Contains("@", StringComparison.Ordinal)
            || value.Contains("diff --git", StringComparison.OrdinalIgnoreCase)
            || value.Contains("providerpayload", StringComparison.OrdinalIgnoreCase)
            || value.Contains("privatekey", StringComparison.OrdinalIgnoreCase)
            || value.Contains("private key", StringComparison.OrdinalIgnoreCase)
            || ProviderTokenPattern().IsMatch(value)
            || JwtPattern().IsMatch(value)
            || PemPattern().IsMatch(value);

    private static bool IsClaimTransformEvidenceValid(
        EventStoreClaimTransformEvidence evidence,
        string authoritativeTenantId,
        string principalId)
        => evidence.IsPresent
            && !evidence.Malformed
            && string.Equals(evidence.TenantId?.Trim(), authoritativeTenantId, StringComparison.Ordinal)
            && string.Equals(evidence.PrincipalId?.Trim(), principalId, StringComparison.Ordinal)
            && evidence.HasPermissionFor(ReadActionToken);

    private static bool HasClientControlledMismatch(
        string authoritativeTenantId,
        IReadOnlyDictionary<string, string?>? comparisonValues)
    {
        if (comparisonValues is null || comparisonValues.Count == 0)
        {
            return false;
        }

        foreach (KeyValuePair<string, string?> value in comparisonValues)
        {
            if (value.Value is null)
            {
                continue;
            }

            string trimmed = value.Value.Trim();
            if (trimmed.Length == 0 || !string.Equals(trimmed, authoritativeTenantId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddPolicyRef(Dictionary<string, string> metadata, string key, string? value)
    {
        if (IsCanonicalIdentifier(value))
        {
            metadata[key] = value!.Trim();
        }
    }

    private static void AddSafeMetadataFlag(Dictionary<string, string> metadata, string key, IReadOnlyDictionary<string, string> values)
        => metadata[key] = values.Count == 0 ? "false" : "true";

    private static string AuthorizationFingerprint(
        OrganizationProviderBinding binding,
        TenantAccessAuthorizationResult tenantAccess,
        string principalId)
        => Sha256(string.Join(
            "|",
            binding.ManagedTenantId,
            binding.OrganizationId,
            binding.ProviderBindingRef,
            tenantAccess.ProjectionWatermark ?? "tenant_projection_unavailable",
            tenantAccess.FreshnessStatus,
            principalId,
            ReadActionToken));

    private static string PolicyFingerprint(OrganizationProviderBindingPolicy policy)
    {
        IEnumerable<string> entries = policy.Metadata
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => $"{pair.Key.Length}:{pair.Key}={pair.Value.Length}:{pair.Value}");
        return Sha256($"{policy.PolicyRef ?? string.Empty}|{string.Join("|", entries)}");
    }

    private static string ToCapabilityProfileRef(string fingerprint)
        => $"profile_{SafeToken(fingerprint)}";

    private static string SafeCorrelationId(string? value)
    {
        if (IsCanonicalIdentifier(value) && !IsSensitiveDiagnosticValue(value))
        {
            return value!.Trim();
        }

        return $"correlation_{Guid.NewGuid():N}";
    }

    private static string SafeReasonCode(string value)
    {
        string safe = SafeToken(value);
        return safe.Length == 0 ? "provider_readiness_failed" : safe;
    }

    private static string SafeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string normalized = value.Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        StringBuilder builder = new(normalized.Length);
        foreach (char c in normalized)
        {
            builder.Append(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-' ? c : '_');
        }

        return builder.ToString();
    }

    private static bool IsCanonicalIdentifier(string? value)
        => !string.IsNullOrWhiteSpace(value)
        && value.Length <= 256
        && CanonicalIdentifierPattern().IsMatch(value);

    private static bool IsSensitiveDiagnosticValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string canonical = value.Trim().ToLowerInvariant();
        return canonical.Contains("token", StringComparison.Ordinal)
            || canonical.Contains("secret", StringComparison.Ordinal)
            || canonical.Contains("password", StringComparison.Ordinal)
            || canonical.Contains("credential", StringComparison.Ordinal)
            || canonical.Contains("repository", StringComparison.Ordinal)
            || canonical.Contains("repo_", StringComparison.Ordinal)
            || canonical.Contains("repo-", StringComparison.Ordinal)
            || canonical.Contains("://", StringComparison.Ordinal)
            || canonical.Contains("@", StringComparison.Ordinal)
            || canonical.Contains("diff --git", StringComparison.Ordinal)
            || canonical.Contains("providerpayload", StringComparison.Ordinal)
            || canonical.Contains("privatekey", StringComparison.Ordinal)
            || canonical.Contains("private key", StringComparison.Ordinal)
            || canonical.Contains("installation", StringComparison.Ordinal)
            || ProviderTokenPattern().IsMatch(value)
            || JwtPattern().IsMatch(value)
            || PemPattern().IsMatch(value);
    }

    private static string Sha256(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]{0,255}$", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalIdentifierPattern();

    [GeneratedRegex("^[a-z0-9][a-z0-9._:/-]{0,255}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeMetadataTokenPattern();

    [GeneratedRegex("gh[pousr]_[a-zA-Z0-9_]{20,}", RegexOptions.CultureInvariant)]
    private static partial Regex ProviderTokenPattern();

    [GeneratedRegex("eyJ[a-zA-Z0-9_-]{10,}\\.[a-zA-Z0-9_-]{5,}\\.[a-zA-Z0-9_-]{5,}", RegexOptions.CultureInvariant)]
    private static partial Regex JwtPattern();

    [GeneratedRegex("-----BEGIN [A-Z ]*PRIVATE KEY-----", RegexOptions.CultureInvariant)]
    private static partial Regex PemPattern();
}
