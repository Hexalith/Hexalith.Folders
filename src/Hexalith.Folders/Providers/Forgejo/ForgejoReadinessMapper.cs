using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.Forgejo;

internal static class ForgejoReadinessMapper
{
    public static IReadOnlyList<ProviderCapabilityOperationRow> ToOperationRows(ForgejoPermissionEvidence permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        return
        [
            Operation(ProviderOperationCatalog.ReadinessValidation, true),
            Operation(ProviderOperationCatalog.ProviderSupportEvidence, permissions.SupportsMetadata),
            Operation(ProviderOperationCatalog.RepositoryCreation, permissions.SupportsRepositoryCreation),
            Operation(ProviderOperationCatalog.RepositoryBinding, permissions.SupportsRepositoryBinding),
            ProviderCapabilityOperationRow.WithDetails(
                ProviderOperationCatalog.BranchRefInspection,
                permissions.SupportsBranchRefInspection ? ProviderOperationSupport.Supported : ProviderOperationSupport.Unavailable,
                limits: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["ref_model"] = "git_refs",
                    ["pagination"] = permissions.SupportsPagination ? "link_header" : "unknown",
                },
                failureCategory: permissions.SupportsBranchRefInspection ? null : ProviderFailureCategory.ProviderPermissionInsufficient),
            ProviderCapabilityOperationRow.WithDetails(
                ProviderOperationCatalog.FileMutationSupport,
                permissions.SupportsFileMutation ? ProviderOperationSupport.Partial : ProviderOperationSupport.Unavailable,
                constraints: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["contents_api"] = permissions.SupportsContentsApi ? "supported" : "unavailable",
                    ["diff_storage"] = "not_persisted",
                    ["scope_posture"] = permissions.RequiredScopePosture,
                },
                failureCategory: permissions.SupportsFileMutation ? null : ProviderFailureCategory.ProviderPermissionInsufficient),
            Operation(ProviderOperationCatalog.CommitSupport, permissions.SupportsCommit),
            Operation(ProviderOperationCatalog.StatusQuery, permissions.SupportsStatus),
            ProviderCapabilityOperationRow.Unsupported(ProviderOperationCatalog.CleanupExpiration),
        ];
    }

    public static ProviderRateLimitPosture ToRateLimit(ForgejoRateLimitEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        return new(
            evidence.Classification,
            evidence.Retryable,
            evidence.RetryAfter,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["posture"] = evidence.Retryable ? "bounded_retry" : "no_retry",
                ["provider_family"] = ForgejoProviderConstants.ProviderFamily,
                ["api_surface_version"] = ForgejoProviderConstants.ApiSurfaceVersion,
                ["header_posture"] = evidence.HeaderPosture,
            });
    }

    public static IReadOnlyDictionary<string, string> ToEvidence(
        ProviderCapabilityDiscoveryRequest request,
        ProviderCredentialMode credentialMode,
        ForgejoVersionEvidence version,
        string safeTargetFingerprint)
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["profile_source"] = "forgejo_http_seam",
            ["forgejo_product_version"] = version.ProductVersion,
            ["forgejo_snapshot_version"] = version.SnapshotVersion,
            ["forgejo_api_surface_version"] = version.ApiSurfaceVersion,
            ["forgejo_compatibility_posture"] = version.CompatibilityPosture,
            ["forgejo_drift_classification"] = version.DriftClassification,
            ["credential_mode"] = credentialMode.ToString().ToLowerInvariant(),
            ["authorization_freshness"] = request.AuthorizationEvidence.FreshnessClass.ToLowerInvariant(),
            ["safe_target_fingerprint"] = safeTargetFingerprint,
            ["capability_profile_schema"] = ForgejoProviderConstants.CapabilityProfileSchemaVersion,
            ["repository_create_bind_port"] = "capability_only_until_provider_port_expands",
        };

    private static ProviderCapabilityOperationRow Operation(string operationId, bool supported)
        => supported
            ? ProviderCapabilityOperationRow.Supported(operationId)
            : ProviderCapabilityOperationRow.WithDetails(
                operationId,
                ProviderOperationSupport.Unavailable,
                failureCategory: ProviderFailureCategory.ProviderPermissionInsufficient);
}
