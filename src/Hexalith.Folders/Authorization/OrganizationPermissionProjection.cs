namespace Hexalith.Folders.Authorization;

/// <summary>Durable metadata-only ACL baseline derived from one organization actor stream.</summary>
/// <param name="TenantId">The authoritative tenant identifier.</param>
/// <param name="OrganizationId">The organization actor identifier.</param>
/// <param name="LastSequence">The last contiguous event sequence applied to the projection.</param>
/// <param name="IsInitialized">Whether the retained stream initialized the ACL baseline.</param>
/// <param name="ProjectedAt">The time the projection was updated.</param>
/// <param name="Grants">The current organization ACL grants.</param>
/// <param name="ProcessedMessages">The bounded message identities and their event sequences.</param>
/// <param name="RecentMessages">The bounded event identities used to detect conflicting redelivery.</param>
public sealed record OrganizationPermissionProjection(string TenantId, string OrganizationId,
    long LastSequence, bool IsInitialized, DateTimeOffset ProjectedAt, IReadOnlyList<OrganizationPermissionGrant> Grants,
    IReadOnlyDictionary<string, long> ProcessedMessages,
    IReadOnlyList<FolderProjectionMessage>? RecentMessages = null);
