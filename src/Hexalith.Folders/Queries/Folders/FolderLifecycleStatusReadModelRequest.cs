namespace Hexalith.Folders.Queries.Folders;

public sealed record FolderLifecycleStatusReadModelRequest(
    string ManagedTenantId,
    string FolderId,
    string PrincipalId,
    string ActionToken,
    string? TaskId,
    string? CorrelationId,
    string? AuthorizationWatermark,
    string ReadConsistency);
