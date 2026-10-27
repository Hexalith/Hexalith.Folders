using System.Globalization;
using System.Security.Claims;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService.Content;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Queries.FileContext;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Folders.Server.Authorization;
using Microsoft.AspNetCore.Http;

namespace Hexalith.Folders.Server.Content;

/// <summary>Supplies current domain truth to EventStore; it never writes bodies or trusts projections for publication.</summary>
internal sealed class WorkspaceContentAuthority(LayeredFolderAuthorizationService authorization,
    ITenantContextAccessor tenant, IEventStoreClaimTransformEvidenceAccessor claims,
    ILayeredFolderAuthorizationResultAccessor processAuthorization, WorkspaceContentStateReader source,
    IWorkspaceFilePolicySource policies, IEventStoreGatewayClient gateway, IHttpContextAccessor http, TimeProvider clock) : IContentAuthority
{
    /// <summary>Identifies the current context query task throughout scoped content reauthorization.</summary>
    internal const string TaskItem = "Folders:WorkspaceContentTask";
    internal const string ActionItem = "Folders:WorkspaceContentAction";
    internal const string EvaluationItem = "Folders:WorkspaceContentEvaluation";
    private WorkspaceFilePolicySnapshot? _snapshot;

    /// <summary>Fences a new public admission against source truth, while preserving a source-proven equivalent replay.</summary>
    internal async Task<WorkspaceContentBinding?> ValidateIngressAsync(ContentScope scope, string taskId, string operationId,
        string kind, PathMetadata path, string? hash, long? length, string? mediaType, long fence, long expectedVersion,
        string? ownerFileId, CancellationToken token)
    {
        var current = await source.ReadAsync(scope, token).ConfigureAwait(false);
        var state = current.State;
        var replay = state.ContentPublications.Values.SingleOrDefault(binding => binding.OperationId == operationId);
        if (replay is not null)
        {
            if (replay.WorkspaceId != scope.WorkspaceId || replay.TaskId != taskId || replay.FileOperationKind != kind
                || replay.PathMetadataDigest != WorkspacePathPolicyValidator.ComputePathMetadataDigest(path)
                || replay.ContentHashReference != hash || replay.ByteLength != length || replay.MediaType != mediaType
                || replay.LockFence != fence || replay.Version - 1 != expectedVersion
                || ownerFileId is not null && replay.FileId != ownerFileId) throw new ContentConflictException();
            return replay;
        }
        if (state.LifecycleState != FolderLifecycleState.Active || state.WorkspaceId != scope.WorkspaceId
            || state.WorkspaceLockHolderTaskId != taskId || state.WorkspaceLockFence != fence
            || state.IsTaskClosed(scope.WorkspaceId, taskId) || state.WorkspaceLockExpiresAt is null
            || state.WorkspaceLockExpiresAt <= clock.GetUtcNow()) throw new ContentConflictException();
        string target = WorkspacePathPolicyValidator.ComputeMutationTargetDigest(scope.WorkspaceId, path);
        var file = state.FileIdentities.Values.SingleOrDefault(identity => identity.Active && identity.MutationTargetDigest == target);
        if (kind == "add")
        {
            if (file is not null || expectedVersion != 0) throw new ContentConflictException();
        }
        else
        {
            if (file is null || ownerFileId is not null && ownerFileId != file.OwnerFileId) throw new ContentConflictException();
            long version = state.ContentPublications.Values.Where(binding => binding.FileId == file.OwnerFileId)
                .Select(binding => binding.Version).DefaultIfEmpty(0).Max();
            if (version != expectedVersion) throw new ContentConflictException();
        }
        return null;
    }

    public async Task AuthorizeAsync(ClaimsPrincipal principal, ContentScope scope, ContentOperation operation,
        object resource, CancellationToken cancellationToken)
    {
        if (scope.Domain != "folders" || tenant.AuthoritativeTenantId != scope.TenantId
            || principal.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(tenant.PrincipalId))
            throw new UnauthorizedAccessException();
        string action = resource is ContentTaskSealRequest ? "mutate_files"
            : operation == ContentOperation.Disposition ? "manage_folder_access"
            : operation == ContentOperation.Read ? "mutate_files"
            : processAuthorization.Current?.AllowedContext?.ActionToken
                ?? http.HttpContext?.Items[ActionItem] as string
                ?? (operation == ContentOperation.Read ? "read_file_content" : "mutate_files");
        bool mutation = operation != ContentOperation.Read && (action == "mutate_files" || operation == ContentOperation.Disposition);
        string? readTaskId = http.HttpContext?.Items.TryGetValue(TaskItem, out object? currentTask) == true
            ? currentTask as string : PreauthorizedRequestContext.Current?.TaskId;
        string? taskId = resource switch { ContentUploadRequest upload => upload.TaskId,
            ContentUploadVerification upload => upload.TaskId, ContentPublicationRequest publication => publication.TaskId,
            ContentTaskSealRequest sealing => sealing.TaskId,
            _ => operation is ContentOperation.Read or ContentOperation.Recover ? readTaskId : null };
        var context = new LayeredFolderAuthorizationContext(scope.TenantId, tenant.PrincipalId, tenant.PrincipalId,
            action, mutation ? LayeredFolderOperationPolicy.Mutation() : LayeredFolderOperationPolicy.StrictRead(),
            claims.GetEvidence(action), scope.AggregateId, null, taskId, null, null);
        var result = mutation
            ? await authorization.ReauthorizeTaskMutationAsync(context, cancellationToken).ConfigureAwait(false)
            : await authorization.AuthorizeCurrentContextReadAsync(context, cancellationToken).ConfigureAwait(false);
        if (!result.IsAllowed) throw result.Decision.Retryable ? new ContentUnavailableException() : new UnauthorizedAccessException();
        if (operation != ContentOperation.Disposition)
        {
            _snapshot ??= Capture();
            if (!policies.IsCurrent(_snapshot)) throw new WorkspaceContentPolicyException("file_policy_unavailable");
            if (_snapshot.ProtectionMode != "non_confidential")
                throw new WorkspaceContentPolicyException("confidential_operation_not_durable");
        }
        if (resource is ContentTaskSealRequest seal)
        {
            if (operation != ContentOperation.Prepare || seal.LeaseFence <= 0) throw new UnauthorizedAccessException();
            var lockContext = context with { ActionToken = "lock_workspace", ClaimTransformEvidence = claims.GetEvidence("lock_workspace") };
            var lockAllowed = await authorization.ReauthorizeTaskMutationAsync(lockContext, cancellationToken).ConfigureAwait(false);
            if (!lockAllowed.IsAllowed) throw new UnauthorizedAccessException();
            var current = await source.ReadAsync(scope, cancellationToken).ConfigureAwait(false);
            if (current.State.WorkspaceId != scope.WorkspaceId || !current.State.WorkspaceNativeLease
                || current.State.WorkspaceLockHolderTaskId != seal.TaskId || current.State.WorkspaceLockFence != seal.LeaseFence
                || current.State.IsTaskClosed(scope.WorkspaceId, seal.TaskId) || current.State.WorkspaceNativeProviderWork)
                throw new ContentConflictException();
            // Expiry does not revoke exact owned cleanup/completion; it still forbids new uploads.
        }
        if (operation is ContentOperation.Upload or ContentOperation.InspectUpload && taskId is not null)
        {
            var current = await source.ReadAsync(scope, cancellationToken).ConfigureAwait(false);
            if (current.State.WorkspaceId != scope.WorkspaceId || current.State.WorkspaceLockHolderTaskId != taskId
                || current.State.IsTaskClosed(scope.WorkspaceId, taskId) || current.State.WorkspaceLockExpiresAt is null
                || current.State.WorkspaceLockExpiresAt <= clock.GetUtcNow()
                || resource is ContentUploadRequest { LeaseFence: { } suppliedFence } && suppliedFence != current.State.WorkspaceLockFence
                || http.HttpContext?.Items["Folders:ContentExpectedFence"] is long expected && expected != current.State.WorkspaceLockFence)
                throw new ContentConflictException();
        }
        if (operation == ContentOperation.Read && resource is ContentReadRequest read)
        {
            var current = await source.ReadAsync(scope, cancellationToken).ConfigureAwait(false);
            if (!current.State.FileIdentities.TryGetValue(read.FileId, out var file) || !file.Active
                || read.IncarnationId != read.FileId || !current.State.ContentPublications.Values.Any(binding =>
                    binding.FileId == read.FileId && binding.Version == read.Version && binding.ReceiptId is not null)
                || current.State.LifecycleState != FolderLifecycleState.Active)
                throw new WorkspaceContentPolicyException("resource_unavailable");
        }
    }

    public async Task ValidateUploadAsync(ClaimsPrincipal principal, ContentUploadRequest request, ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var policy = _snapshot ?? Capture();
        if (policy.ProtectionMode != "non_confidential") throw new WorkspaceContentPolicyException("confidential_operation_not_durable");
        if (bytes.Length > 1048576 || !policies.IsCurrent(policy)) throw new WorkspaceContentPolicyException("file_policy_unavailable");
        await AuthorizeAsync(principal, request.Scope, ContentOperation.Upload, request, cancellationToken).ConfigureAwait(false);
        if (!policies.IsCurrent(policy)) throw new WorkspaceContentPolicyException("file_policy_unavailable");
    }

    public async Task ValidatePreparationAsync(ClaimsPrincipal principal, ContentPublicationRequest request, CancellationToken cancellationToken)
    {
        var current = await source.ReadAsync(request.Scope, cancellationToken).ConfigureAwait(false);
        var state = current.State;
        var policy = _snapshot ?? Capture();
        if (!current.ActorOwned || request.ExpectedVersion != current.Head || request.LockFence != state.WorkspaceLockFence
            || request.TaskId != state.WorkspaceLockHolderTaskId || state.IsTaskClosed(request.Scope.WorkspaceId, request.TaskId)
            || state.WorkspaceLockExpiresAt is null || state.WorkspaceLockExpiresAt <= clock.GetUtcNow() || state.WorkspaceId != request.Scope.WorkspaceId
            || state.LifecycleState != FolderLifecycleState.Active || !policies.IsCurrent(policy))
            throw new ContentConflictException();
        foreach (var change in request.Changes)
        {
            var metadata = change.ResourceMetadata ?? throw new ContentConflictException();
            var path = MetadataPath(metadata);
            if (metadata.GetValueOrDefault("policyDigest") != policy.Digest || metadata.GetValueOrDefault("policyVersion") != policy.Version
                || policy.Classify(path) is "excluded" or "restricted"
                || change.ReceiptId is not null && state.ContentErasureGrants.ContainsKey(change.ReceiptId)
                || !WorkspacePathPolicyValidator.Validate(path).IsAccepted || change.FileId != change.IncarnationId
                || metadata.GetValueOrDefault("pathDigest") != WorkspacePathPolicyValidator.ComputePathMetadataDigest(path))
                throw new ContentConflictException();
            long previous = state.ContentPublications.Values.Where(binding => binding.FileId == change.FileId)
                .Select(binding => binding.Version).DefaultIfEmpty(0).Max();
            if (change.Version != previous + 1) throw new ContentConflictException();
        }
    }

    public async Task<ContentPublicationOutcome> GetPublicationOutcomeAsync(ContentPublicationRequest request, CancellationToken cancellationToken)
    {
        var current = await source.ReadAsync(request.Scope, cancellationToken).ConfigureAwait(false);
        var bindings = current.State.ContentPublications.Values.Where(value => value.CommandId == request.CommandId).ToArray();
        if (bindings.Length > 0)
        {
            if (bindings.Length != request.Changes.Count) throw new ContentUnavailableException();
            foreach (var binding in bindings)
            {
            var change = request.Changes.SingleOrDefault(value => value.FileId == binding.FileId) ?? throw new ContentUnavailableException();
            if (binding.ManagedTenantId != request.Scope.TenantId || binding.FolderId != request.Scope.AggregateId
                || binding.WorkspaceId != request.Scope.WorkspaceId || binding.TaskId != request.TaskId
                || binding.LockFence != request.LockFence || binding.ExpectedVersion != request.ExpectedVersion
                || binding.FileId != change.FileId || change.IncarnationId != binding.FileId || binding.Version != change.Version
                || binding.ReceiptId != change.ReceiptId || binding.PathMetadataDigest != change.ResourceMetadata?.GetValueOrDefault("pathDigest")
                || binding.PolicyDigest != change.ResourceMetadata?.GetValueOrDefault("policyDigest")
                || binding.PolicyVersion != change.ResourceMetadata?.GetValueOrDefault("policyVersion")
                || (binding.ContentReadable ? "true" : "false") != change.ResourceMetadata?.GetValueOrDefault("contentReadable")
                || (binding.ReceiptId is not null && (change.ExpectedReceipt is not { } receipt
                    || receipt.TaskId != binding.TaskId || "hashref_" + receipt.Sha256 != binding.ContentHashReference
                    || receipt.ByteLength != binding.ByteLength || receipt.MediaType != binding.MediaType)))
                throw new ContentUnavailableException();
            }
            return ContentPublicationOutcome.Accepted;
        }
        if (current.Events.Any(item => item.CausationId == request.CommandId && FolderEventReplay.IsKnownRejection(item.EventTypeName)))
            return ContentPublicationOutcome.Rejected;
        if (current.ActorOwned) return ContentPublicationOutcome.Pending;
        var status = await gateway.GetCommandStatusAsync(request.CommandId, cancellationToken).ConfigureAwait(false);
        if (status?.TenantId == request.Scope.TenantId && status.Domain == request.Scope.Domain
            && status.AggregateId == request.Scope.AggregateId && status.MessageId == request.CommandId
            && status.IsRejected) return ContentPublicationOutcome.Rejected;
        return ContentPublicationOutcome.Pending;
    }

    public async Task<ContentDisposition> GetDispositionAsync(ContentScope scope, ContentReceipt receipt, CancellationToken cancellationToken)
    {
        var current = await source.ReadAsync(scope, cancellationToken).ConfigureAwait(false);
        var state = current.State;
        bool live = WorkspaceContentErasure.HasLiveReference(state, receipt.Id);
        bool active = WorkspaceContentErasure.HasActiveTask(state);
        return new(current.Head, receipt.TaskId is not null && state.IsTaskClosed(scope.WorkspaceId, receipt.TaskId), active, live, false);
    }

    public async Task<bool> TryAuthorizeErasureAsync(ContentScope scope, ContentReceipt receipt, long generation, CancellationToken cancellationToken)
    {
        // The SDK holds a revocable claim. This source grant permanently forbids this receipt's future references.
        // It does not prohibit unrelated later tasks. Until SDK deletion authorization, recovery must re-read
        // disposition and re-prove the exact current source grant; source changes restart the P7D epoch.
        // The owning aggregate must independently serialize terminal/no-active/source-generation proof.
        return await WorkspaceContentErasure.AuthorizeAsync(gateway, source, scope, receipt, generation,
            http.HttpContext, cancellationToken).ConfigureAwait(false);
    }

    internal static PathMetadata MetadataPath(IReadOnlyDictionary<string, string> metadata)
        => new(metadata.GetValueOrDefault("path") ?? string.Empty, metadata.GetValueOrDefault("displayName") ?? string.Empty,
            metadata.GetValueOrDefault("pathPolicyClass") ?? string.Empty, "NFC");
    private WorkspaceFilePolicySnapshot Capture()
    {
        try { return policies.Capture(); }
        catch (InvalidOperationException) { throw new WorkspaceContentPolicyException("file_policy_unavailable"); }
    }
}
