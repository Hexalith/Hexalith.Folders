namespace Hexalith.Folders.Projections.TenantAccess;

/// <summary>
/// Dedup-identity evidence for a Tenants envelope. <c>CorrelationId</c> is deliberately excluded
/// because Dapr's at-least-once redelivery typically rotates correlation ids per attempt; including
/// it would flip <see cref="FolderTenantAccessProjection.ReplayConflict"/> on every legitimate retry.
/// </summary>
public sealed record FolderTenantEventEvidence(
    string MessageId,
    string TenantId,
    string EventTypeName,
    long SequenceNumber,
    DateTimeOffset Timestamp,
    string PayloadFingerprint);
