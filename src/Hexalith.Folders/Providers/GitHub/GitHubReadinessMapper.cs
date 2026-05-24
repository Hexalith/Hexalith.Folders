using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.GitHub;

internal static class GitHubReadinessMapper
{
    public static IReadOnlyList<ProviderCapabilityOperationRow> ToOperationRows(GitHubPermissionEvidence permissions)
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
                },
                failureCategory: permissions.SupportsBranchRefInspection ? null : ProviderFailureCategory.ProviderPermissionInsufficient),
            ProviderCapabilityOperationRow.WithDetails(
                ProviderOperationCatalog.FileMutationSupport,
                permissions.SupportsFileMutation ? ProviderOperationSupport.Partial : ProviderOperationSupport.Unavailable,
                constraints: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["contents_access"] = permissions.SupportsFileMutation ? "read_write" : "missing",
                    ["diff_storage"] = "not_persisted",
                },
                failureCategory: permissions.SupportsFileMutation ? null : ProviderFailureCategory.ProviderPermissionInsufficient),
            Operation(ProviderOperationCatalog.CommitSupport, permissions.SupportsCommit),
            Operation(ProviderOperationCatalog.StatusQuery, permissions.SupportsStatus),
            ProviderCapabilityOperationRow.Unsupported(ProviderOperationCatalog.CleanupExpiration),
        ];
    }

    public static ProviderRateLimitPosture ToRateLimit(GitHubRateLimitEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        return new(
            evidence.Classification,
            evidence.Retryable,
            evidence.RetryAfter,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["posture"] = evidence.Retryable ? "bounded_retry" : "no_retry",
                ["provider_family"] = GitHubProviderConstants.ProviderFamily,
                ["api_version"] = GitHubProviderConstants.RestApiVersion,
            });
    }

    public static IReadOnlyDictionary<string, string> ToEvidence(
        ProviderCapabilityDiscoveryRequest request,
        ProviderCredentialMode credentialMode,
        string safeTargetFingerprint)
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["profile_source"] = "github_octokit_seam",
            ["github_api_version"] = GitHubProviderConstants.RestApiVersion,
            ["github_permission_schema"] = "github-app-and-fine-grained-v1",
            ["credential_mode"] = credentialMode.ToString().ToLowerInvariant(),
            ["authorization_freshness"] = request.AuthorizationEvidence.FreshnessClass.ToLowerInvariant(),
            ["safe_target_fingerprint"] = safeTargetFingerprint,
            ["capability_profile_schema"] = "v1",
        };

    private static ProviderCapabilityOperationRow Operation(string operationId, bool supported)
        => supported
            ? ProviderCapabilityOperationRow.Supported(operationId)
            : ProviderCapabilityOperationRow.WithDetails(
                operationId,
                ProviderOperationSupport.Unavailable,
                failureCategory: ProviderFailureCategory.ProviderPermissionInsufficient);
}

