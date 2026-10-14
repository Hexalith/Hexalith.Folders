namespace Hexalith.Folders.Workers;

/// <summary>Identifies a derived permission row and an optional expected ETag for conditional reset.</summary>
/// <param name="TenantId">The authoritative stream tenant.</param>
/// <param name="AggregateId">The folder or tenant ACL actor identifier.</param>
/// <param name="ExpectedEtag">The captured ETag; omit it to inspect the current row ETag.</param>
public sealed record FolderPermissionResetRequest(string TenantId, string AggregateId, string? ExpectedEtag = null);
