namespace Hexalith.Folders.Projections.SemanticIndexing;

/// <summary>
/// An evidence-only update recorded against a <c>Tombstoned</c> bridge entry after a removal (hard delete) or archive
/// (soft delete) egress attempt. Unlike <see cref="SemanticIndexingResultUpdate"/>, this update is deliberately
/// allowed to target a Tombstoned entry: it updates only the evidence/outcome fields (published cloudevent id, reason,
/// retryable, observed-at) and never the <c>Status</c>, preserving the Story 10.2/10.3 invariant that a Tombstoned
/// entry never regresses to <c>Indexed</c>. The update is applied only when its <see cref="Watermark"/> is greater than
/// or equal to the current entry's freshness watermark, so an older/out-of-order removal cannot overwrite a newer
/// file-version state (Story 10.4 AC6).
/// </summary>
public sealed record SemanticIndexingRemovalEvidenceUpdate
{
    public SemanticIndexingRemovalEvidenceUpdate(
        SemanticIndexingFileVersionIdentity identity,
        string reasonCode,
        bool retryable,
        string correlationId,
        string taskId,
        string? publishedEventId,
        string? resultFingerprint,
        long watermark,
        DateTimeOffset observedAt)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        Identity = identity;
        ReasonCode = reasonCode;
        Retryable = retryable;
        CorrelationId = correlationId;
        TaskId = taskId;
        PublishedEventId = publishedEventId;
        ResultFingerprint = resultFingerprint;
        Watermark = watermark;
        ObservedAt = observedAt;
    }

    public SemanticIndexingFileVersionIdentity Identity { get; }

    public string ReasonCode { get; }

    public bool Retryable { get; }

    public string CorrelationId { get; }

    public string TaskId { get; }

    public string? PublishedEventId { get; }

    public string? ResultFingerprint { get; }

    public long Watermark { get; }

    public DateTimeOffset ObservedAt { get; }
}
