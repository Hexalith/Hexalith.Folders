using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.SemanticIndexing;
using Hexalith.Folders.Projections.TenantAccess;

namespace Hexalith.Folders.Workers.SemanticIndexing;

internal sealed class FailClosedSemanticIndexingPolicyEvaluator : ISemanticIndexingPolicyEvaluator
{
    private const int MaxPathPolicyClassLength = 80;

    private readonly IFolderPermissionEvidenceProvider _folderPermissionEvidenceProvider;
    private readonly IFolderTenantAccessProjectionStore _tenantAccessStore;

    public FailClosedSemanticIndexingPolicyEvaluator(
        IFolderTenantAccessProjectionStore tenantAccessStore,
        IFolderPermissionEvidenceProvider folderPermissionEvidenceProvider)
    {
        ArgumentNullException.ThrowIfNull(tenantAccessStore);
        ArgumentNullException.ThrowIfNull(folderPermissionEvidenceProvider);
        _tenantAccessStore = tenantAccessStore;
        _folderPermissionEvidenceProvider = folderPermissionEvidenceProvider;
    }

    public async ValueTask<SemanticIndexingPolicyEvaluationResult> EvaluateAsync(
        SemanticIndexingBridgeEntry entry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();

        // Gate 1 (AC3 order): tenant-access authority. Indexing authority comes from the Folders tenant-access
        // projection, not the client-carried event evidence. Fail closed when the tenant is unknown to the worker,
        // disabled, has no authorized principals, or its projection is in a replay-conflict / malformed-evidence state.
        FolderTenantAccessProjection? tenantAccess = await _tenantAccessStore
            .GetAsync(entry.Identity.ManagedTenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenantAccess is null
            || !tenantAccess.Enabled
            || tenantAccess.Principals.Count == 0
            || tenantAccess.ReplayConflict
            || tenantAccess.MalformedEvidence)
        {
            return SemanticIndexingPolicyEvaluationResult.Failed("tenant_access_unavailable", retryable: true);
        }

        // Gate 2 (AC3 order): folder ACL/action authorization freshness, keyed by the actor/action captured on the
        // durable accepted mutation event. Missing or stale evidence fails closed before content materialization.
        if (string.IsNullOrWhiteSpace(entry.Evidence.ActorPrincipalId)
            || string.IsNullOrWhiteSpace(entry.Evidence.AuthorizationActionToken))
        {
            return SemanticIndexingPolicyEvaluationResult.Failed(
                "authorization_evidence_unavailable",
                retryable: true);
        }

        FolderPermissionEvidenceResult folderPermission = await _folderPermissionEvidenceProvider
            .GetEvidenceAsync(
                new FolderPermissionEvidenceRequest(
                    entry.Identity.ManagedTenantId,
                    entry.Evidence.ActorPrincipalId,
                    ActorSafeIdentifier(entry.Evidence.ActorPrincipalId),
                    entry.Evidence.AuthorizationActionToken,
                    entry.Identity.FolderId,
                    entry.CorrelationId,
                    entry.TaskId,
                    FolderOperationPolicyClass.Mutation,
                    AllowBoundedStale: false),
                cancellationToken)
            .ConfigureAwait(false);
        if (folderPermission.Status != FolderPermissionEvidenceStatus.Allowed)
        {
            bool retryable = folderPermission.Retryable || folderPermission.Status == FolderPermissionEvidenceStatus.Stale;
            return folderPermission.Status is FolderPermissionEvidenceStatus.Denied or FolderPermissionEvidenceStatus.NotFoundSafe
                ? SemanticIndexingPolicyEvaluationResult.Skipped("folder_acl_denied", retryable: false)
                : SemanticIndexingPolicyEvaluationResult.Failed(folderPermission.OutcomeCode, retryable);
        }

        if (!string.IsNullOrWhiteSpace(folderPermission.OrganizationId)
            && !string.Equals(folderPermission.OrganizationId, entry.Identity.OrganizationId, StringComparison.Ordinal))
        {
            return SemanticIndexingPolicyEvaluationResult.Failed(
                "authorization_evidence_malformed",
                retryable: false);
        }

        // Gate 3 (AC3 order): path policy evidence carried on the durable accepted event.
        if (string.IsNullOrWhiteSpace(entry.Evidence.PathPolicyClass))
        {
            return SemanticIndexingPolicyEvaluationResult.Failed(
                "authorization_evidence_unavailable",
                retryable: true);
        }

        string pathPolicyClass = entry.Evidence.PathPolicyClass.Trim();
        if (!IsValidPathPolicyClass(pathPolicyClass))
        {
            return SemanticIndexingPolicyEvaluationResult.Skipped(
                "path_policy_denied",
                retryable: false);
        }

        // Gate 4 (AC3 order): sensitivity classification.
        if (IsRedactedSensitivity(pathPolicyClass))
        {
            return SemanticIndexingPolicyEvaluationResult.Skipped(
                "sensitivity_redacted",
                retryable: false);
        }

        // Gate 5 (AC3 order): size / type limits. Require at least one length signal (declared or observed) plus a
        // media type before sizing.
        if ((entry.Evidence.ByteLength ?? entry.Evidence.ObservedByteLength) is null
            || string.IsNullOrWhiteSpace(entry.Evidence.MediaType))
        {
            return SemanticIndexingPolicyEvaluationResult.Failed(
                "content_descriptor_unavailable",
                retryable: true);
        }

        // Reject when EITHER the declared or the observed length exceeds the inline cap; a null field is ignored
        // because a lifted `null > cap` comparison is false.
        if (entry.Evidence.ByteLength > FoldersSemanticIndexingDefaults.MaxInlineIngestionBytes
            || entry.Evidence.ObservedByteLength > FoldersSemanticIndexingDefaults.MaxInlineIngestionBytes)
        {
            return SemanticIndexingPolicyEvaluationResult.Skipped(
                "content_too_large",
                retryable: false);
        }

        if (!IsSupportedInlineContentType(entry.Evidence.MediaType))
        {
            return SemanticIndexingPolicyEvaluationResult.Skipped(
                "content_type_unsupported",
                retryable: false);
        }

        return SemanticIndexingPolicyEvaluationResult.Allowed(
            "tenant_sensitive",
            "accepted_mutation_authorized");
    }

    private static bool IsValidPathPolicyClass(string pathPolicyClass)
        => pathPolicyClass.Length <= MaxPathPolicyClassLength
            && pathPolicyClass.All(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '/' or '-');

    private static bool IsRedactedSensitivity(string pathPolicyClass)
        => pathPolicyClass.Contains("secret", StringComparison.Ordinal)
            || pathPolicyClass.Contains("credential", StringComparison.Ordinal)
            || pathPolicyClass.Contains("redacted", StringComparison.Ordinal);

    private static string ActorSafeIdentifier(string principalId)
        => "sha256:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(principalId))).ToLowerInvariant();

    private static bool IsSupportedInlineContentType(string? contentType)
        => !string.IsNullOrWhiteSpace(contentType)
            && (contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
                || string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase)
                || string.Equals(contentType, "application/xml", StringComparison.OrdinalIgnoreCase)
                || string.Equals(contentType, "application/x-yaml", StringComparison.OrdinalIgnoreCase)
                || string.Equals(contentType, "application/yaml", StringComparison.OrdinalIgnoreCase)
                || string.Equals(contentType, "application/markdown", StringComparison.OrdinalIgnoreCase));
}
