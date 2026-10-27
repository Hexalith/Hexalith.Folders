using Hexalith.EventStore.DomainService.Content;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Queries.FileContext;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Folders.Server.Authorization;
using Microsoft.AspNetCore.Http;

namespace Hexalith.Folders.Server.Content;

/// <summary>Reads current owner evidence independently of a live lease for already accepted operations.</summary>
internal sealed class WorkspaceOperationEvidence(WorkspaceContentStateReader source, IAuthoritativeContentStore store,
    IWorkspaceFilePolicySource policies, IWorkspaceFileSensitivityClassifier sensitivity, LayeredFolderAuthorizationService authorization,
    ITenantContextAccessor tenant, IEventStoreClaimTransformEvidenceAccessor claims, IHttpContextAccessor http, TimeProvider clock)
{
    internal async Task AuthorizeAsync(ContentScope scope, string taskId, CancellationToken token, string action = "mutate_files")
    {
        if (tenant.AuthoritativeTenantId != scope.TenantId || http.HttpContext?.User.Identity?.IsAuthenticated != true)
            throw new UnauthorizedAccessException();
        http.HttpContext.Items[WorkspaceContentAuthority.TaskItem] = taskId;
        http.HttpContext.Items[WorkspaceContentAuthority.ActionItem] = action;
        var context = new LayeredFolderAuthorizationContext(scope.TenantId, tenant.PrincipalId, tenant.PrincipalId,
            action, LayeredFolderOperationPolicy.Mutation(), claims.GetEvidence(action), scope.AggregateId,
            FolderHttpHeaderReader.ReadHeader(http.HttpContext, "X-Correlation-Id"), taskId,
            HistoricalMutationAuthorization.ClientControlledValues(http.HttpContext,
                ("query_tenant_id", false, "tenantId"), ("query_managed_tenant_id", false, "managedTenantId"),
                ("header_hexalith_tenant_id", true, "X-Hexalith-Tenant-Id"), ("header_tenant_id", true, "X-Tenant-Id"),
                ("forwarded_tenant_id", true, "X-Forwarded-Tenant")),
            HistoricalMutationAuthorization.ClientControlledValues(http.HttpContext,
                ("query_principal_id", false, "principalId"), ("header_principal_id", true, "X-Principal-Id"),
                ("forwarded_principal_id", true, "X-Forwarded-Principal")));
        var allowed = await authorization.ReauthorizeTaskMutationAsync(context, token).ConfigureAwait(false);
        if (!allowed.IsAllowed) throw allowed.Decision.Retryable ? new ContentUnavailableException() : new UnauthorizedAccessException();
    }

    internal async Task<WorkspaceOperationEvidenceResult> ReadAsync(ContentScope scope, string task,
        WorkspaceOperationEvidenceRequest request, CancellationToken token)
    {
        await AuthorizeAsync(scope, task, token).ConfigureAwait(false);
        var policy = policies.Capture();
        var path = request.PathMetadata;
        if (path is null || path.NormalizedPath.Length is < 1 or > 1024 || path.DisplayName.Length is < 1 or > 256
            || path.NormalizedPath.Any(char.IsControl) || path.NormalizedPath.Split('/').Any(part => part is "" or "." or "..")
            || path.NormalizedPath.Contains('\\') || path.UnicodeNormalization != "NFC"
            || (request.Legacy ? policy.ClassifyHistoricalEvidence(path) : policy.Classify(path)) is "excluded" or "restricted") throw new UnauthorizedAccessException();
        if (!request.Legacy && !WorkspacePathPolicyValidator.Validate(path).IsAccepted) throw new ArgumentException("Invalid path.");
        if (policy.ProtectionMode != "non_confidential") throw new WorkspaceContentPolicyException("confidential_operation_not_durable");
        var classified = await sensitivity.ClassifyAsync(path, token).ConfigureAwait(false);
        await AuthorizeAsync(scope, task, token).ConfigureAwait(false);
        if (classified.Decision == WorkspaceFileSensitivityDecision.Redacted) throw new UnauthorizedAccessException();
        if (classified.Decision != WorkspaceFileSensitivityDecision.Allowed || !policies.IsCurrent(policy)) throw new ContentUnavailableException();
        var current = await source.ReadAsync(scope, token).ConfigureAwait(false);
        RequireScope(current.State, scope);
        string digest = WorkspacePathPolicyValidator.ComputePathMetadataDigest(path);
        var matches = current.State.ContentPublications.Values.Where(value => value.WorkspaceId == scope.WorkspaceId
            && (value.OperationId == request.OperationId || value.AcceptedSdkKey == request.AcceptedKey)).ToArray();
        WorkspaceOperationEvidenceResult result;
        if (matches.Length > 0)
        {
            if (matches.Length != 1) throw new ContentUnavailableException();
            var binding = matches[0];
            if (binding.AcceptedSdkKey is null) throw new ContentUnavailableException();
            if (binding.TaskId != task || binding.OperationId != request.OperationId || binding.AcceptedSdkKey != request.AcceptedKey
                || binding.FileOperationKind != "add" || binding.PathMetadataDigest != digest
                || binding.ContentHashReference != request.ContentHashReference || binding.ByteLength != request.ByteLength
                || binding.MediaType != request.MediaType) throw new WorkspaceNativeConflictException("idempotency_conflict");
            if (!current.State.IdempotencyFingerprints.TryGetValue(binding.CommandId, out string? fingerprint))
                throw new ContentUnavailableException();
            if (await store.RecoverAsync(http.HttpContext!.User, scope, binding.CommandId, token).ConfigureAwait(false) != ContentPublicationOutcome.Accepted)
                throw new ContentUnavailableException();
            var visible = await store.ListAsync(http.HttpContext.User, scope, token).ConfigureAwait(false);
            if (!current.State.FileIdentities.TryGetValue(binding.FileId, out var identity) || !identity.Active
                || !visible.Any(item => item.Change.FileId == binding.FileId && item.Change.IncarnationId == binding.FileId
                    && item.Change.Version == binding.Version && item.Receipt.Id == binding.ReceiptId
                    && "hashref_" + item.Receipt.Sha256 == binding.ContentHashReference && item.Receipt.ByteLength == binding.ByteLength))
                throw new ContentUnavailableException();
            result = new("accepted", binding.Version - 1, binding.LockFence, current.State.IsTaskClosed(scope.WorkspaceId, task),
                binding.FileId, binding.FileId, fingerprint, binding.AcceptedSdkKey, binding.OperationId, binding.ContentHashReference,
                binding.MediaType, task, binding.CommandId, binding.Version, binding.ByteLength ?? 0,
                current.State.WorkspaceNativeLease && current.State.WorkspaceLockHolderTaskId == task && current.State.WorkspaceLockFence == binding.LockFence ? current.State.WorkspaceLockId : null,
                current.State.WorkspaceNativeLease && current.State.WorkspaceLockHolderTaskId == task && current.State.WorkspaceLockFence == binding.LockFence
                    ? WorkspaceNativeTaskService.OwnershipProof(scope.TenantId, scope.AggregateId, scope.WorkspaceId, task, current.State.WorkspaceLockId!, binding.LockFence) : null);
        }
        else if (request.LookupKind == "accepted") result = new("missing", 0, 0, false);
        else if (request.LookupKind == "absence")
        {
            RequireLiveLease(current.State, scope, task, request.LockFence, clock.GetUtcNow());
            string target = WorkspacePathPolicyValidator.ComputeMutationTargetDigest(scope.WorkspaceId, path);
            if (current.State.FileIdentities.Values.Any(file => file.Active && file.MutationTargetDigest == target)) throw new WorkspaceNativeConflictException("target_conflict");
            var visible = await store.ListAsync(http.HttpContext!.User, scope, token).ConfigureAwait(false);
            if (visible.Any(item => item.Change.ResourceMetadata?.GetValueOrDefault("path")?.Equals(path.NormalizedPath, StringComparison.OrdinalIgnoreCase) == true))
                throw new WorkspaceNativeConflictException("target_conflict");
            result = new("absent", 0, current.State.WorkspaceLockFence, false);
        }
        else throw new ArgumentException("Invalid evidence kind.");
        var final = await source.ReadAsync(scope, token).ConfigureAwait(false);
        await AuthorizeAsync(scope, task, token).ConfigureAwait(false);
        if (final.Head != current.Head || !policies.IsCurrent(policy)) throw new WorkspaceNativeConflictException("native_evidence_changed");
        if (request.LookupKind == "absence") RequireLiveLease(final.State, scope, task, request.LockFence, clock.GetUtcNow());
        return result;
    }

    internal static void RequireScope(FolderState state, ContentScope scope)
    {
        if (!state.IsCreated || state.ManagedTenantId != scope.TenantId || state.FolderId != scope.AggregateId
            || state.WorkspaceId != scope.WorkspaceId || state.LifecycleState != FolderLifecycleState.Active)
            throw new UnauthorizedAccessException();
    }
    internal static void RequireLiveLease(FolderState state, ContentScope scope, string task, long fence, DateTimeOffset now)
    {
        RequireScope(state, scope);
        if (state.WorkspaceLockHolderTaskId != task || state.WorkspaceLockFence != fence || state.WorkspaceLockEffectiveAt > now
            || state.WorkspaceLockExpiresAt is null || state.WorkspaceLockExpiresAt <= now || state.IsTaskClosed(scope.WorkspaceId, task))
            throw new ContentConflictException();
    }
}
