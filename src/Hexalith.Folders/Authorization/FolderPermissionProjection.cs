namespace Hexalith.Folders.Authorization;

/// <summary>Durable metadata-only permission view for one folder actor stream.</summary>
public sealed record FolderPermissionProjection(
    string TenantId,
    string OrganizationId,
    string FolderId,
    long LastSequence,
    bool IsCreated,
    bool IsArchived,
    DateTimeOffset ProjectedAt,
    IReadOnlyList<FolderPermissionOverrideEntry> Overrides,
    IReadOnlyList<FolderProjectionMessage> RecentMessages);
