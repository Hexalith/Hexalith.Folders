using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Queries.Folders;

namespace Hexalith.Folders.Queries.FileContext;

public sealed record WorkspaceFileContextQueryResult(
    WorkspaceFileContextResultCode Code,
    WorkspaceFileContextQueryKind Kind,
    IReadOnlyList<WorkspaceFileContextItem> Items,
    PathMetadata? RangePath,
    WorkspaceFileContextRange? Range,
    string? ContentBytes,
    WorkspaceFileContextPage? Page,
    WorkspaceFileContextLimits Limits,
    FolderLifecycleFreshness Freshness,
    string? CorrelationId,
    string? TaskId,
    LayeredFolderAuthorizationResult? AuthorizationDenial);
