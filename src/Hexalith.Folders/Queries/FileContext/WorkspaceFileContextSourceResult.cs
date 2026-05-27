using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Queries.Folders;

namespace Hexalith.Folders.Queries.FileContext;

public sealed record WorkspaceFileContextSourceResult(
    WorkspaceFileContextSourceStatus Status,
    IReadOnlyList<WorkspaceFileContextItem> Items,
    PathMetadata? RangePath,
    WorkspaceFileContextRange? Range,
    string? ContentBytes,
    WorkspaceFileContextPage? Page,
    WorkspaceFileContextLimits Limits,
    FolderLifecycleFreshness Freshness)
{
    public static WorkspaceFileContextSourceResult Unavailable(DateTimeOffset observedAt)
        => new(
            WorkspaceFileContextSourceStatus.Unavailable,
            [],
            null,
            null,
            null,
            null,
            new WorkspaceFileContextLimits("metadata", 0, 0, 0, 0, false, "not_truncated"),
            new FolderLifecycleFreshness("snapshot_per_task", observedAt, null, Stale: true, "context_source_unavailable"));
}
