using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

internal sealed class WorkspaceFileMutationBindingContent : IWorkspaceFileContentStore
{
    public bool Prepared { get; private set; }
    public Task<WorkspaceFileContentStoreResult> StageAsync(WorkspaceFileContentStoreRequest request, CancellationToken cancellationToken = default)
    {
        Prepared = true;
        return Task.FromResult(WorkspaceFileContentStoreResult.Succeeded with { Binding = new(request.ManagedTenantId, request.FolderId,
            request.WorkspaceId, request.TaskId, "command", request.OperationId, request.FileId!, request.FileVersion, request.ExpectedVersion,
            request.LockFence, "content_" + new string('a', 64), request.ContentHashReference, request.ByteLength, request.MediaType,
            "1.1.0", "policy", request.FileOperationKind, "path", true) });
    }
}

