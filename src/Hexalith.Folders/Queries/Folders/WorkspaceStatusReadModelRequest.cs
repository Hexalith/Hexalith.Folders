namespace Hexalith.Folders.Queries.Folders;

public sealed record WorkspaceStatusReadModelRequest(
    string ManagedTenantId,
    string FolderId,
    string WorkspaceId,
    string PrincipalId,
    string ActionToken,
    string? TaskId,
    string? CorrelationId,
    string? AuthorizationWatermark,
    string ReadConsistency);
