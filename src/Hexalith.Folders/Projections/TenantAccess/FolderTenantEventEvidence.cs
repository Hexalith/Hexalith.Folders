namespace Hexalith.Folders.Projections.TenantAccess;

public sealed record FolderTenantEventEvidence(
    string MessageId,
    string TenantId,
    string EventTypeName,
    long SequenceNumber,
    DateTimeOffset Timestamp,
    string CorrelationId,
    string PayloadFingerprint);
