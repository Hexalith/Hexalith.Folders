using Hexalith.EventStore.DomainService.Content;

namespace Hexalith.Folders.Server.Content;

/// <summary>Collects one actor decision's fully validated changes before a single SDK preparation.</summary>
public sealed class WorkspaceContentBatch
{
    internal List<ContentChange>? Changes { get; private set; }
    internal ContentPublicationRequest? Request { get; private set; }
    internal void Begin()
    {
        if (Changes is not null) throw new InvalidOperationException("A content batch is already active.");
        Changes = [];
    }
    internal void Add(ContentPublicationRequest request)
    {
        if (Changes is null) throw new InvalidOperationException("No content batch is active.");
        if (Request is not null && (Request.Scope != request.Scope || Request.CommandId != request.CommandId
            || Request.TaskId != request.TaskId || Request.LockFence != request.LockFence || Request.ExpectedVersion != request.ExpectedVersion))
            throw new ContentConflictException();
        Request ??= request;
        Changes.AddRange(request.Changes);
    }
    internal ContentPublicationRequest Complete() => Request is null || Changes is null || Changes.Count == 0
        ? throw new ContentConflictException() : Request with { Changes = Changes.ToArray() };
    internal void End() { Changes = null; Request = null; }
}
