using System.Globalization;
using Hexalith.EventStore.DomainService.Content;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Queries.FileContext;
using Microsoft.AspNetCore.Http;

namespace Hexalith.Folders.Server.Content;

/// <summary>Prepares immutable SDK receipts before the owning actor's transactional metadata append.</summary>
internal sealed class WorkspaceContentMutationAdapter(IAuthoritativeContentStore store, IWorkspaceFilePolicySource policies,
    IHttpContextAccessor http, WorkspaceContentBatch batch) : IWorkspaceFileContentStore, IWorkspaceFileDeleteOperationStore
{
    public async Task<WorkspaceFileContentStoreResult> StageAsync(WorkspaceFileContentStoreRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (request.ReceiptId is null) return Failed(FolderResultCode.MalformedEvidence);
            var scope = new ContentScope(request.ManagedTenantId, "folders", request.FolderId, request.WorkspaceId);
            var principal = http.HttpContext?.User ?? throw new UnauthorizedAccessException();
            var receipt = await store.VerifyUploadAsync(principal, new(scope, request.TaskId, request.ReceiptId), cancellationToken).ConfigureAwait(false);
            if ("hashref_" + receipt.Sha256 != request.ContentHashReference || receipt.ByteLength != request.ByteLength
                || receipt.MediaType != request.MediaType || receipt.TaskId != request.TaskId)
                return Failed(FolderResultCode.MalformedEvidence);
            var inspected = await store.InspectUploadAsync(principal, new(scope, request.TaskId, request.ReceiptId), cancellationToken).ConfigureAwait(false);
            bool readable = WorkspaceFileContentClassification.IsContentReadable(inspected.Bytes);
            var binding = await PrepareAsync(scope, request.TaskId, request.OperationId, request.FileOperationKind,
                request.FileId, request.FileVersion, request.ExpectedVersion, request.LockFence, request.RequestedLockFence,
                request.RequestedFileVersion, request.PathMetadata, request.PolicyVersion, request.PolicyDigest, receipt, readable,
                cancellationToken).ConfigureAwait(false);
            return WorkspaceFileContentStoreResult.Succeeded with { Binding = binding };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (ContentConflictException) { return Failed(FolderResultCode.AppendConflict); }
        catch (Exception) { return Failed(FolderResultCode.PolicyEvidenceUnavailable); }
    }

    public async Task<WorkspaceFileDeleteOperationStoreResult> StageAsync(WorkspaceFileDeleteOperationStoreRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var scope = new ContentScope(request.ManagedTenantId, "folders", request.FolderId, request.WorkspaceId);
            var binding = await PrepareAsync(scope, request.TaskId, request.OperationId, request.FileOperationKind,
                request.FileId, request.FileVersion, request.ExpectedVersion, request.LockFence, request.RequestedLockFence,
                request.RequestedFileVersion, request.PathMetadata, request.PolicyVersion, request.PolicyDigest, null, false,
                cancellationToken).ConfigureAwait(false);
            return WorkspaceFileDeleteOperationStoreResult.Succeeded with { Binding = binding };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (ContentConflictException) { return WorkspaceFileDeleteOperationStoreResult.Failed with { FailureCode = FolderResultCode.AppendConflict }; }
        catch (Exception) { return WorkspaceFileDeleteOperationStoreResult.Failed with { FailureCode = FolderResultCode.PolicyEvidenceUnavailable }; }
    }

    private async Task<WorkspaceContentBinding> PrepareAsync(ContentScope scope, string taskId, string operationId,
        string kind, string? fileId, long version, long expectedHead, long fence, long? requestedFence, long? requestedVersion,
        PathMetadata? path, string? policyVersion, string? policyDigest, ContentReceipt? receipt, bool readable, CancellationToken token)
    {
        var policy = policies.Capture();
        if (path is null || string.IsNullOrEmpty(fileId) || fence <= 0 || requestedFence != fence || requestedVersion != version - 1
            || policy.Version != policyVersion || policy.Digest != policyDigest || !policies.IsCurrent(policy))
            throw new ContentConflictException();
        string commandId = http.HttpContext?.Items["Folders:WorkspaceContentCommand"] as string
            ?? throw new ContentUnavailableException();
        var principal = http.HttpContext!.User;
        var current = await store.ListAsync(principal, scope, token).ConfigureAwait(false);
        if (kind == "add" && current.Any(binding => WorkspaceContentAuthority.MetadataPath(binding.Change.ResourceMetadata!).NormalizedPath.Equals(path.NormalizedPath, StringComparison.OrdinalIgnoreCase))
            || kind != "add" && !current.Any(binding => binding.Change.FileId == fileId && binding.Change.Version == version - 1
                && WorkspaceContentAuthority.MetadataPath(binding.Change.ResourceMetadata!).NormalizedPath == path.NormalizedPath))
            throw new ContentConflictException();
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["path"] = path.NormalizedPath, ["displayName"] = path.DisplayName, ["pathPolicyClass"] = path.PathPolicyClass,
            ["pathDigest"] = WorkspacePathPolicyValidator.ComputePathMetadataDigest(path),
            ["policyVersion"] = policy.Version, ["policyDigest"] = policy.Digest, ["contentReadable"] = readable ? "true" : "false",
        };
        var publication = new ContentPublicationRequest(scope, commandId, taskId, fence, expectedHead,
            [new(fileId, fileId, version, receipt?.Id, metadata, receipt)]);
        if (batch.Changes is not null) batch.Add(publication);
        else await store.PrepareAsync(principal, publication, token).ConfigureAwait(false);
        if (!policies.IsCurrent(policy)) throw new WorkspaceContentPolicyException("file_policy_unavailable");
        return new(scope.TenantId, scope.AggregateId, scope.WorkspaceId, taskId, commandId, operationId, fileId,
            version, expectedHead, fence, receipt?.Id, receipt is null ? null : "hashref_" + receipt.Sha256,
            receipt?.ByteLength, receipt?.MediaType, policy.Version, policy.Digest, kind,
            WorkspacePathPolicyValidator.ComputePathMetadataDigest(path), readable)
        { AcceptedSdkKey = http.HttpContext?.Items["Folders:AcceptedSdkKey"] as string };
    }
    private static WorkspaceFileContentStoreResult Failed(FolderResultCode code)
        => WorkspaceFileContentStoreResult.Failed with { FailureCode = code };
}
