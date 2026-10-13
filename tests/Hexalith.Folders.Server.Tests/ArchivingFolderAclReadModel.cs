using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Supplies active authorization evidence, then an archived row at the protected read.</summary>
internal sealed class ArchivingFolderAclReadModel(FolderPermissionProjection projection)
    : IEffectivePermissionsReadModel, IFolderAclReadModel
{
    /// <summary>Gets the number of authorization evidence reads.</summary>
    internal int AuthorizationReads { get; private set; }

    /// <summary>Gets the number of protected ACL reads.</summary>
    internal int AclReads { get; private set; }

    /// <inheritdoc />
    public Task<EffectivePermissionsReadModelResult> GetAsync(EffectivePermissionsReadModelRequest request,
        CancellationToken cancellationToken = default)
    {
        AuthorizationReads++;
        return Task.FromResult(EffectivePermissionsReadModelResult.Available(new(
            projection.TenantId, projection.OrganizationId, projection.FolderId,
            EffectivePermissionsFolderLifecycleState.Active,
            [new(EffectivePermissionEvidenceSource.FolderOverrideGrant,
                EffectivePermissionPrincipal.User("user-a"), "read_metadata", 1, projection.ProjectedAt)],
            new(request.ReadConsistency, projection.ProjectedAt, "1", false, null), true, null)));
    }

    /// <inheritdoc />
    public Task<FolderPermissionProjection?> GetCurrentAsync(string tenantId, string folderId,
        CancellationToken cancellationToken = default)
    {
        AclReads++;
        return Task.FromResult<FolderPermissionProjection?>(projection with { IsArchived = true, LastSequence = 2 });
    }
}
