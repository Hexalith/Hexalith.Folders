namespace Hexalith.Folders.Aggregates.Folder;

public interface IFolderEvent
{
    string ManagedTenantId { get; }

    string OrganizationId { get; }

    string FolderId { get; }

    string CorrelationId { get; }

    string TaskId { get; }

    string IdempotencyKey { get; }

    string IdempotencyFingerprint { get; }

    // Wall-clock instant supplied by the gate via TimeProvider when the event was produced.
    // Persisted on the event so projections and downstream authorization can evaluate
    // C7 freshness budgets without re-deriving time from sequence or watermark.
    DateTimeOffset OccurredAt { get; }
}
