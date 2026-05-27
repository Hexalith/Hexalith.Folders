namespace Hexalith.Folders.Queries.FileContext;

public sealed record WorkspaceFileContextSourceRequest(
    WorkspaceFileContextQueryKind Kind,
    string ManagedTenantId,
    string FolderId,
    string WorkspaceId,
    string PrincipalId,
    string ActionToken,
    string? TaskId,
    string? CorrelationId,
    string? AuthorizationWatermark,
    IReadOnlyList<WorkspaceFileContextQueryPath> Paths,
    string? QueryText,
    string? GlobPattern,
    int Limit,
    string? Cursor,
    long? StartOffset,
    long? EndOffset);
