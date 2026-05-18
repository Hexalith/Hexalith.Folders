namespace Hexalith.Folders.Projections.TenantAccess;

public sealed record FolderTenantAccessEvent(
    FolderTenantAccessEventKind Kind,
    string TenantId,
    string MessageId,
    long SequenceNumber,
    DateTimeOffset Timestamp,
    string CorrelationId,
    string? PrincipalId = null,
    string? Role = null,
    string? PreviousRole = null,
    string? ConfigurationKey = null,
    string PayloadFingerprint = "");
