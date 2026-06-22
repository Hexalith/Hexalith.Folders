namespace Hexalith.Folders.Queries.Folders;

/// <summary>
/// Projected, metadata-only evidence of a workspace's most recent C6 lifecycle transition.
/// </summary>
/// <param name="ManagedTenantId">Owning managed tenant id.</param>
/// <param name="FolderId">Folder id.</param>
/// <param name="WorkspaceId">Workspace id.</param>
/// <param name="CurrentState">Current lifecycle state (canonical wire value).</param>
/// <param name="FromState">Source lifecycle state of the most recent transition (canonical wire value).</param>
/// <param name="EventName">Lifecycle event that drove the most recent transition (PascalCase wire value).</param>
/// <param name="Result">Transition result (canonical wire value).</param>
/// <param name="ReasonCode">Transition reason code (canonical wire value).</param>
/// <param name="EvidenceAt">When the transition evidence was recorded.</param>
/// <param name="CorrelationId">Correlation id of the driving operation.</param>
/// <param name="TaskId">Task id of the driving operation.</param>
/// <param name="LockEvidence">Lock lease evidence when the workspace holds a lease; otherwise null.</param>
/// <param name="AuditMetadata">Metadata-only audit timestamps.</param>
/// <param name="Freshness">Read freshness metadata.</param>
/// <param name="EvidenceScope">Evidence scope used for compatibility checks.</param>
public sealed record WorkspaceTransitionEvidenceSnapshot(
    string ManagedTenantId,
    string FolderId,
    string WorkspaceId,
    string CurrentState,
    string FromState,
    string EventName,
    string Result,
    string ReasonCode,
    DateTimeOffset EvidenceAt,
    string? CorrelationId,
    string? TaskId,
    WorkspaceLockLeaseMetadata? LockEvidence,
    IReadOnlyDictionary<string, DateTimeOffset> AuditMetadata,
    FolderLifecycleFreshness Freshness,
    FolderLifecycleEvidenceScope EvidenceScope);
