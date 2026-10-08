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
            TargetPermissionOperation(
                ProviderOperationCatalog.RepositoryCreation,
                permissions.SupportsRepositoryCreation,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["allowed_visibility"] = "public,private",
                    ["implicit_initialization"] = "disabled",
                }),
            TargetPermissionOperation(
                ProviderOperationCatalog.RepositoryBinding,
                permissions.SupportsRepositoryBinding,
                null),
            ProviderCapabilityOperationRow.WithDetails(
                ProviderOperationCatalog.BranchRefInspection,
                permissions.SupportsBranchRefInspection
                    ? ProviderOperationSupport.Partial
                    : ProviderOperationSupport.Unavailable,
                limits: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["maximum_branch_name_characters"] = "100",
                    ["ref_model"] = "git_refs",
                    ["pagination"] = permissions.SupportsPagination ? "link_header" : "unknown",
                },
                constraints: permissions.SupportsBranchRefInspection
                    ? TargetPermissionConstraints()
                    : null,
                failureCategory: permissions.SupportsBranchRefInspection ? null : ProviderFailureCategory.ProviderPermissionInsufficient),
            ProviderCapabilityOperationRow.WithDetails(
                ProviderOperationCatalog.FileMutationSupport,
                permissions.SupportsFileMutation ? ProviderOperationSupport.Supported : ProviderOperationSupport.Unavailable,
                limits: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["maximum_aggregate_content_bytes"] = "10485760",
                    ["maximum_change_count"] = "100",
                    ["maximum_file_bytes"] = "1048576",
                    ["maximum_path_characters"] = "500",
                },
                constraints: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["contents_api"] = "read_only_only",
                    ["diff_storage"] = "not_persisted",
                    ["git_object_format"] = "sha1",
                    ["libgit2_version"] = "1.8.6",
                    ["libgit2sharp_version"] = "0.32.0",
                    ["staging"] = "smart_https_fetch_and_local_bare_tree",
                    ["scope_posture"] = permissions.RequiredScopePosture,
                },
                failureCategory: permissions.SupportsFileMutation ? null : ProviderFailureCategory.ProviderPermissionInsufficient),
            ProviderCapabilityOperationRow.WithDetails(
                ProviderOperationCatalog.CommitSupport,
                permissions.SupportsCommit ? ProviderOperationSupport.Supported : ProviderOperationSupport.Unavailable,
                constraints: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["compare_and_swap"] = "receive_pack_expected_old",
                    ["force"] = "disabled",
                    ["new_branch"] = "disabled",
                    ["transport"] = "smart_https_receive_pack",
                },
                failureCategory: permissions.SupportsCommit ? null : ProviderFailureCategory.ProviderPermissionInsufficient),
            ProviderCapabilityOperationRow.WithDetails(
                ProviderOperationCatalog.StatusQuery,
                permissions.SupportsStatus ? ProviderOperationSupport.Supported : ProviderOperationSupport.Unavailable,
                limits: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["maximum_check_count"] = "5",
                    ["reconciliation_window_seconds"] = "900",
                },
                constraints: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["evidence"] = "version_and_exact_rest_ref",
                },
                failureCategory: permissions.SupportsStatus ? null : ProviderFailureCategory.ProviderPermissionInsufficient),
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
            ["file_commit_transport"] = "libgit2sharp_smart_https",
            ["status_transport"] = "forgejo_rest_ref_read",
            ["repository_create_bind_port"] = "production_http_adapter",
        };

    private static ProviderCapabilityOperationRow Operation(string operationId, bool supported)
        => supported
            ? ProviderCapabilityOperationRow.Supported(operationId)
            : ProviderCapabilityOperationRow.WithDetails(
                operationId,
                ProviderOperationSupport.Unavailable,
                failureCategory: ProviderFailureCategory.ProviderPermissionInsufficient);

    private static ProviderCapabilityOperationRow TargetPermissionOperation(
        string operationId,
        bool authenticated,
        IReadOnlyDictionary<string, string>? limits)
        => authenticated
            ? ProviderCapabilityOperationRow.WithDetails(
                operationId,
                ProviderOperationSupport.Partial,
                limits,
                TargetPermissionConstraints(),
                retryable: false)
            : ProviderCapabilityOperationRow.WithDetails(
                operationId,
                ProviderOperationSupport.Unavailable,
                limits,
                failureCategory: ProviderFailureCategory.ProviderPermissionInsufficient);

    private static Dictionary<string, string> TargetPermissionConstraints()
        => new(StringComparer.Ordinal)
        {
            ["permission_posture"] = "authenticated_target_permission_dependent",
        };
}
