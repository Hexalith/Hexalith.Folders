namespace Hexalith.Folders.Queries.Folders;

/// <summary>
/// Read-model request for workspace transition evidence.
/// </summary>
/// <param name="ManagedTenantId">Authoritative managed tenant id.</param>
/// <param name="FolderId">Folder id.</param>
/// <param name="WorkspaceId">Workspace id.</param>
/// <param name="PrincipalId">Caller principal id.</param>
/// <param name="ActionToken">Read action token.</param>
/// <param name="TaskId">Task id.</param>
/// <param name="CorrelationId">Correlation id.</param>
/// <param name="AuthorizationWatermark">Authorization freshness watermark.</param>
/// <param name="ReadConsistency">Requested read-consistency class.</param>
public sealed record WorkspaceTransitionEvidenceReadModelRequest(
    string ManagedTenantId,
    string FolderId,
    string WorkspaceId,
    string PrincipalId,
    string ActionToken,
    string? TaskId,
    string? CorrelationId,
    string? AuthorizationWatermark,
    string ReadConsistency);
