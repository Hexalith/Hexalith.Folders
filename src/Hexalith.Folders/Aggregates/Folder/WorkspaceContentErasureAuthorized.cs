namespace Hexalith.Folders.Aggregates.Folder;

/// <summary>Metadata-only source reservation for irreversible body erasure after fresh terminal/reference checks.</summary>
/// <param name="ManagedTenantId">Owning tenant.</param>
/// <param name="OrganizationId">Owning organization.</param>
/// <param name="FolderId">Owning folder.</param>
/// <param name="WorkspaceId">Owning workspace.</param>
/// <param name="ReceiptId">Exact opaque body receipt.</param>
/// <param name="SourceGeneration">Checked pre-append source head.</param>
/// <param name="ActorPrincipalId">Authorized administrator.</param>
/// <param name="CorrelationId">Current request correlation.</param>
/// <param name="TaskId">Retention operation identifier.</param>
/// <param name="IdempotencyKey">Exact command identity.</param>
/// <param name="IdempotencyFingerprint">Stable receipt/generation intent.</param>
/// <param name="OccurredAt">Reservation instant.</param>
public sealed record WorkspaceContentErasureAuthorized(string ManagedTenantId, string OrganizationId, string FolderId,
    string WorkspaceId, string ReceiptId, long SourceGeneration, string ActorPrincipalId, string CorrelationId,
    string TaskId, string IdempotencyKey, string IdempotencyFingerprint, DateTimeOffset OccurredAt) : IFolderEvent;
