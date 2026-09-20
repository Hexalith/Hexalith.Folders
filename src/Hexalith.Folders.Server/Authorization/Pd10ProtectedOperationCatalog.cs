using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Server.Authorization;

/// <summary>
/// Resolves the complete PD10 v2 candidate surface by exact method and route structure. Opaque route values
/// are captured only after the literal route identity has been selected and can never select an operation.
/// </summary>
internal static class Pd10ProtectedOperationCatalog
{
    private static readonly Pd10ProtectedOperationDescriptor[] AllDescriptors =
    [
        D("CreateFolder", "POST", "/api/v2/folders", "/api/v1/folders", V2ProtectedOperationFamily.FolderCreation, "create_folder", FolderOperationPolicyClass.Mutation, Pd10FolderScopeRule.None),
        D("GetFolderLifecycleStatus", "GET", "/api/v2/folders/{folderId}/lifecycle-status", "/api/v1/folders/{folderId}/lifecycle-status", V2ProtectedOperationFamily.StatusPermissionAndLockInspection, "read_metadata", FolderOperationPolicyClass.StrictRead),
        D("ArchiveFolder", "POST", "/api/v2/folders/{folderId}/archive", "/api/v1/folders/{folderId}/archive", V2ProtectedOperationFamily.FolderAdministration, "archive_folder", FolderOperationPolicyClass.Mutation),
        D("ListFolderAclEntries", "GET", "/api/v2/folders/{folderId}/acl", "/api/v1/folders/{folderId}/acl", V2ProtectedOperationFamily.FolderAdministration, "manage_folder_access", FolderOperationPolicyClass.StrictRead),
        D("UpdateFolderAclEntry", "PUT", "/api/v2/folders/{folderId}/acl/{aclEntryId}", "/api/v1/folders/{folderId}/acl/{aclEntryId}", V2ProtectedOperationFamily.FolderAdministration, "manage_folder_access", FolderOperationPolicyClass.Mutation),
        D("GetEffectivePermissions", "GET", "/api/v2/folders/{folderId}/effective-permissions", "/api/v1/folders/{folderId}/effective-permissions", V2ProtectedOperationFamily.StatusPermissionAndLockInspection, "read_metadata", FolderOperationPolicyClass.StrictRead),
        D("ConfigureProviderBinding", "PUT", "/api/v2/provider-bindings/{providerBindingRef}", "/api/v1/provider-bindings/{providerBindingRef}", V2ProtectedOperationFamily.ProviderConfiguration, "configure_provider_binding", FolderOperationPolicyClass.Mutation, Pd10FolderScopeRule.None),
        D("GetProviderBinding", "GET", "/api/v2/provider-bindings/{providerBindingRef}", "/api/v1/provider-bindings/{providerBindingRef}", V2ProtectedOperationFamily.ProviderConfiguration, "tenant-context-and-provider-binding-read", FolderOperationPolicyClass.StrictRead, Pd10FolderScopeRule.None),
        D("ValidateProviderReadiness", "POST", "/api/v2/provider-readiness/validations", "/api/v1/provider-readiness/validations", V2ProtectedOperationFamily.ReadinessAndProviderEvidence, "provider_readiness_read", FolderOperationPolicyClass.StrictRead, Pd10FolderScopeRule.None),
        D("GetProviderSupportEvidence", "GET", "/api/v2/provider-readiness/support-evidence", "/api/v1/provider-readiness/support-evidence", V2ProtectedOperationFamily.ReadinessAndProviderEvidence, "tenant-context-and-provider-support-read", FolderOperationPolicyClass.StrictRead, Pd10FolderScopeRule.None),
        D("CreateRepositoryBackedFolder", "POST", "/api/v2/folders/repository-backed", "/api/v1/folders/repository-backed", V2ProtectedOperationFamily.FolderAdministration, "create_repository_backed_folder", FolderOperationPolicyClass.Mutation, Pd10FolderScopeRule.RequestFolder),
        D("BindRepository", "POST", "/api/v2/folders/{folderId}/repository-bindings", "/api/v1/folders/{folderId}/repository-bindings", V2ProtectedOperationFamily.FolderAdministration, "bind_repository", FolderOperationPolicyClass.Mutation),
        D("GetRepositoryBinding", "GET", "/api/v2/folders/{folderId}/repository-bindings/{repositoryBindingId}", "/api/v1/folders/{folderId}/repository-bindings/{repositoryBindingId}", V2ProtectedOperationFamily.StatusPermissionAndLockInspection, "read_metadata", FolderOperationPolicyClass.StrictRead),
        D("ConfigureBranchRefPolicy", "PUT", "/api/v2/folders/{folderId}/branch-ref-policy", "/api/v1/folders/{folderId}/branch-ref-policy", V2ProtectedOperationFamily.FolderAdministration, "configure_branch_ref_policy", FolderOperationPolicyClass.Mutation),
        D("GetBranchRefPolicy", "GET", "/api/v2/folders/{folderId}/branch-ref-policy", "/api/v1/folders/{folderId}/branch-ref-policy", V2ProtectedOperationFamily.StatusPermissionAndLockInspection, "read_branch_ref_policy", FolderOperationPolicyClass.StrictRead),
        D("PrepareWorkspace", "POST", "/api/v2/folders/{folderId}/workspaces/{workspaceId}/preparation", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/preparation", V2ProtectedOperationFamily.TaskMutation, "prepare_workspace", FolderOperationPolicyClass.Mutation),
        D("LockWorkspace", "POST", "/api/v2/folders/{folderId}/workspaces/{workspaceId}/lock", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/lock", V2ProtectedOperationFamily.TaskMutation, "lock_workspace", FolderOperationPolicyClass.Mutation),
        D("GetWorkspaceLock", "GET", "/api/v2/folders/{folderId}/workspaces/{workspaceId}/lock", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/lock", V2ProtectedOperationFamily.StatusPermissionAndLockInspection, "read_workspace_lock", FolderOperationPolicyClass.StrictRead),
        D("ReleaseWorkspaceLock", "POST", "/api/v2/folders/{folderId}/workspaces/{workspaceId}/lock/release", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/lock/release", V2ProtectedOperationFamily.TaskMutation, "lock_workspace", FolderOperationPolicyClass.Mutation),
        D("GetWorkspaceRetryEligibility", "GET", "/api/v2/folders/{folderId}/workspaces/{workspaceId}/retry-eligibility", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/retry-eligibility", V2ProtectedOperationFamily.StatusPermissionAndLockInspection, "read_workspace_status", FolderOperationPolicyClass.StrictRead),
        D("GetWorkspaceTransitionEvidence", "GET", "/api/v2/folders/{folderId}/workspaces/{workspaceId}/transition-evidence", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/transition-evidence", V2ProtectedOperationFamily.StatusPermissionAndLockInspection, "read_metadata", FolderOperationPolicyClass.StrictRead),
        D("AddFile", "POST", "/api/v2/folders/{folderId}/workspaces/{workspaceId}/files/add", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/files/add", V2ProtectedOperationFamily.TaskMutation, "mutate_files", FolderOperationPolicyClass.Mutation),
        D("ChangeFile", "PUT", "/api/v2/folders/{folderId}/workspaces/{workspaceId}/files/change", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/files/change", V2ProtectedOperationFamily.TaskMutation, "mutate_files", FolderOperationPolicyClass.Mutation),
        D("RemoveFile", "POST", "/api/v2/folders/{folderId}/workspaces/{workspaceId}/files/remove", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/files/remove", V2ProtectedOperationFamily.TaskMutation, "mutate_files", FolderOperationPolicyClass.Mutation),
        D("ListFolderFiles", "GET", "/api/v2/folders/{folderId}/workspaces/{workspaceId}/context/tree", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/context/tree", V2ProtectedOperationFamily.ContextRead, "read_metadata", FolderOperationPolicyClass.StrictRead),
        D("GetFolderFileMetadata", "POST", "/api/v2/folders/{folderId}/workspaces/{workspaceId}/context/metadata", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/context/metadata", V2ProtectedOperationFamily.ContextRead, "read_metadata", FolderOperationPolicyClass.StrictRead),
        D("SearchFolderFiles", "POST", "/api/v2/folders/{folderId}/workspaces/{workspaceId}/context/search", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/context/search", V2ProtectedOperationFamily.ContextRead, "read_context_search", FolderOperationPolicyClass.StrictRead),
        D("SearchFolderIndexedFiles", "POST", "/api/v2/folders/{folderId}/workspaces/{workspaceId}/context/index-search", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/context/index-search", V2ProtectedOperationFamily.IndexSearch, "read_context_search", FolderOperationPolicyClass.StrictRead),
        D("GetFolderIndexingStatus", "GET", "/api/v2/folders/{folderId}/indexing-status", "/api/v1/folders/{folderId}/indexing-status", V2ProtectedOperationFamily.IndexSearch, "read_context_search", FolderOperationPolicyClass.StrictRead),
        D("GlobFolderFiles", "POST", "/api/v2/folders/{folderId}/workspaces/{workspaceId}/context/glob", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/context/glob", V2ProtectedOperationFamily.ContextRead, "read_metadata", FolderOperationPolicyClass.StrictRead),
        D("ReadFileRange", "POST", "/api/v2/folders/{folderId}/workspaces/{workspaceId}/context/range-read", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/context/range-read", V2ProtectedOperationFamily.ContextRead, "read_file_content", FolderOperationPolicyClass.StrictRead),
        D("CommitWorkspace", "POST", "/api/v2/folders/{folderId}/workspaces/{workspaceId}/commits", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/commits", V2ProtectedOperationFamily.TaskMutation, "commit", FolderOperationPolicyClass.Mutation),
        D("GetWorkspaceStatus", "GET", "/api/v2/folders/{folderId}/workspaces/{workspaceId}/status", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/status", V2ProtectedOperationFamily.StatusPermissionAndLockInspection, "read_workspace_status", FolderOperationPolicyClass.StrictRead),
        D("GetWorkspaceCleanupStatus", "GET", "/api/v2/folders/{folderId}/workspaces/{workspaceId}/cleanup/status", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/cleanup/status", V2ProtectedOperationFamily.StatusPermissionAndLockInspection, "read_workspace_cleanup_status", FolderOperationPolicyClass.StrictRead),
        D("GetTaskStatus", "GET", "/api/v2/folders/{folderId}/tasks/{taskId}/status", "/api/v1/tasks/{taskId}/status", V2ProtectedOperationFamily.StatusPermissionAndLockInspection, "query_status", FolderOperationPolicyClass.StrictRead, Pd10FolderScopeRule.RouteFolder, Pd10TaskBindingRule.RouteTaskBelongsToRouteFolder),
        D("GetCommitEvidence", "GET", "/api/v2/folders/{folderId}/workspaces/{workspaceId}/commits/{operationId}/evidence", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/commits/{operationId}/evidence", V2ProtectedOperationFamily.StatusPermissionAndLockInspection, "query_status", FolderOperationPolicyClass.StrictRead),
        D("GetProviderOutcome", "GET", "/api/v2/folders/{folderId}/workspaces/{workspaceId}/commits/{operationId}/provider-outcome", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/commits/{operationId}/provider-outcome", V2ProtectedOperationFamily.IncidentEvidence, "query_status", FolderOperationPolicyClass.StrictRead),
        D("GetReconciliationStatus", "GET", "/api/v2/folders/{folderId}/workspaces/{workspaceId}/reconciliation/{reconciliationId}/status", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/reconciliation/{reconciliationId}/status", V2ProtectedOperationFamily.IncidentEvidence, "query_status", FolderOperationPolicyClass.StrictRead),
        D("ListAuditTrail", "GET", "/api/v2/folders/{folderId}/audit-trail", "/api/v1/folders/{folderId}/audit-trail", V2ProtectedOperationFamily.AuditRead, "query_audit", FolderOperationPolicyClass.StrictRead),
        D("GetAuditRecord", "GET", "/api/v2/folders/{folderId}/audit-trail/{auditRecordId}", "/api/v1/folders/{folderId}/audit-trail/{auditRecordId}", V2ProtectedOperationFamily.AuditRead, "query_audit", FolderOperationPolicyClass.StrictRead),
        D("ListOperationTimeline", "GET", "/api/v2/folders/{folderId}/operation-timeline", "/api/v1/folders/{folderId}/operation-timeline", V2ProtectedOperationFamily.AuditRead, "query_audit", FolderOperationPolicyClass.StrictRead),
        D("GetOperationTimelineEntry", "GET", "/api/v2/folders/{folderId}/operation-timeline/{timelineEntryId}", "/api/v1/folders/{folderId}/operation-timeline/{timelineEntryId}", V2ProtectedOperationFamily.AuditRead, "query_audit", FolderOperationPolicyClass.StrictRead),
        D("GetReadinessDiagnostics", "GET", "/api/v2/folders/{folderId}/ops-console/readiness-diagnostics", "/api/v1/ops-console/readiness-diagnostics", V2ProtectedOperationFamily.ConsoleView, "view_operations_console", FolderOperationPolicyClass.StrictRead),
        D("GetLockDiagnostics", "GET", "/api/v2/folders/{folderId}/workspaces/{workspaceId}/ops-console/lock-diagnostics", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/ops-console/lock-diagnostics", V2ProtectedOperationFamily.ConsoleView, "view_operations_console", FolderOperationPolicyClass.StrictRead),
        D("GetDirtyStateDiagnostics", "GET", "/api/v2/folders/{folderId}/workspaces/{workspaceId}/ops-console/dirty-state-diagnostics", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/ops-console/dirty-state-diagnostics", V2ProtectedOperationFamily.ConsoleView, "view_operations_console", FolderOperationPolicyClass.StrictRead),
        D("GetFailedOperationDiagnostics", "GET", "/api/v2/folders/{folderId}/workspaces/{workspaceId}/ops-console/failed-operation-diagnostics", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/ops-console/failed-operation-diagnostics", V2ProtectedOperationFamily.ConsoleView, "view_operations_console", FolderOperationPolicyClass.StrictRead),
        D("GetProviderStatusDiagnostics", "GET", "/api/v2/folders/{folderId}/ops-console/provider-status-diagnostics", "/api/v1/folders/{folderId}/ops-console/provider-status-diagnostics", V2ProtectedOperationFamily.ConsoleView, "view_operations_console", FolderOperationPolicyClass.StrictRead),
        D("GetSyncStatusDiagnostics", "GET", "/api/v2/folders/{folderId}/workspaces/{workspaceId}/ops-console/sync-status-diagnostics", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/ops-console/sync-status-diagnostics", V2ProtectedOperationFamily.ConsoleView, "view_operations_console", FolderOperationPolicyClass.StrictRead),
        D("GetProjectionFreshness", "GET", "/api/v2/folders/{folderId}/ops-console/projection-freshness", "/api/v1/ops-console/projection-freshness", V2ProtectedOperationFamily.ConsoleView, "view_operations_console", FolderOperationPolicyClass.StrictRead),
    ];

    /// <summary>Gets the complete immutable descriptor set for focused conformance tests.</summary>
    internal static IReadOnlyList<Pd10ProtectedOperationDescriptor> Descriptors => AllDescriptors;

    /// <summary>Resolves one exact method-and-route identity and its decoded route values.</summary>
    /// <param name="method">Request method.</param>
    /// <param name="path">Request path without query string.</param>
    /// <param name="descriptor">Resolved descriptor.</param>
    /// <param name="routeValues">Decoded placeholder values.</param>
    /// <returns><see langword="true"/> only for one declared identity.</returns>
    internal static bool TryResolve(
        string method,
        string path,
        out Pd10ProtectedOperationDescriptor? descriptor,
        out IReadOnlyDictionary<string, string> routeValues)
    {
        foreach (Pd10ProtectedOperationDescriptor candidate in AllDescriptors)
        {
            if (string.Equals(candidate.Method, method, StringComparison.OrdinalIgnoreCase)
                && TryMatch(candidate.CandidateRoute, path, out Dictionary<string, string>? values))
            {
                descriptor = candidate;
                routeValues = values;
                return true;
            }
        }

        descriptor = null;
        routeValues = new Dictionary<string, string>(StringComparer.Ordinal);
        return false;
    }

    /// <summary>Builds the exact historical path for a resolved descriptor and route-value set.</summary>
    internal static string HistoricalPath(
        Pd10ProtectedOperationDescriptor descriptor,
        IReadOnlyDictionary<string, string> routeValues)
    {
        string result = descriptor.HistoricalRoute;
        foreach ((string key, string value) in routeValues)
        {
            result = result.Replace($"{{{key}}}", Uri.EscapeDataString(value), StringComparison.Ordinal);
        }

        return result;
    }

    private static Pd10ProtectedOperationDescriptor D(
        string operationId,
        string method,
        string candidateRoute,
        string historicalRoute,
        V2ProtectedOperationFamily family,
        string actionToken,
        FolderOperationPolicyClass policyClass,
        Pd10FolderScopeRule folderScope = Pd10FolderScopeRule.RouteFolder,
        Pd10TaskBindingRule taskBinding = Pd10TaskBindingRule.None)
        => new(operationId, method, candidateRoute, historicalRoute, family, actionToken, policyClass, folderScope, taskBinding);

    private static bool TryMatch(string template, string path, out Dictionary<string, string> values)
    {
        values = new(StringComparer.Ordinal);
        string[] templateSegments = template.Split('/');
        string[] pathSegments = path.Split('/');
        if (templateSegments.Length != pathSegments.Length)
        {
            return false;
        }

        for (int index = 0; index < templateSegments.Length; index++)
        {
            string templateSegment = templateSegments[index];
            string pathSegment = pathSegments[index];
            if (templateSegment.Length > 2 && templateSegment[0] == '{' && templateSegment[^1] == '}')
            {
                if (pathSegment.Length == 0)
                {
                    return false;
                }

                try
                {
                    values[templateSegment[1..^1]] = Uri.UnescapeDataString(pathSegment);
                }
                catch (UriFormatException)
                {
                    return false;
                }
            }
            else if (!string.Equals(templateSegment, pathSegment, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
