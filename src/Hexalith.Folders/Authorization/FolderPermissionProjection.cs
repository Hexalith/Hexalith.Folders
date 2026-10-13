using System.Text.Json.Serialization;

namespace Hexalith.Folders.Authorization;

/// <summary>Durable metadata-only permission view for one folder actor stream.</summary>
/// <param name="TenantId">The authoritative tenant identifier.</param>
/// <param name="OrganizationId">The organization whose baseline permissions are inherited.</param>
/// <param name="FolderId">The folder actor identifier.</param>
/// <param name="LastSequence">The last contiguous event sequence applied to the projection.</param>
/// <param name="IsCreated">Whether the retained stream created the folder.</param>
/// <param name="IsArchived">Whether the retained stream archived the folder.</param>
/// <param name="ProjectedAt">The time the projection was updated.</param>
/// <param name="Overrides">The current folder ACL overrides.</param>
/// <param name="RecentMessages">The bounded event identities used to detect conflicting redelivery.</param>
public sealed record FolderPermissionProjection(
    string TenantId,
    string OrganizationId,
    string FolderId,
    long LastSequence,
    [property: JsonRequired] bool IsCreated,
    [property: JsonRequired] bool IsArchived,
    DateTimeOffset ProjectedAt,
    IReadOnlyList<FolderPermissionOverrideEntry> Overrides,
    IReadOnlyList<FolderProjectionMessage> RecentMessages);
