using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Authorization;

public sealed class EffectivePermissionsCrossPrincipalTests
{
    [Fact]
    public async Task EvidenceRowsForOtherPrincipalsAreFilteredOut()
    {
        EffectivePermissionsReadModelSnapshot snapshot = EffectivePermissionsTestSupport.Snapshot(
            EffectivePermissionsTestSupport.OrganizationGrant("read_metadata", principalId: "user-a"),
            EffectivePermissionsTestSupport.FolderGrant("commit", principalId: "user-a", sequence: 1),
            EffectivePermissionsTestSupport.OrganizationGrant("configure_provider_binding", principalId: "user-b"),
            EffectivePermissionsTestSupport.FolderGrant("mutate_files", principalId: "user-b", sequence: 1));

        EffectivePermissionsQueryResult resultA = await ExecuteAsync(snapshot, principalId: "user-a").ConfigureAwait(true);
        EffectivePermissionsQueryResult resultB = await ExecuteAsync(snapshot, principalId: "user-b").ConfigureAwait(true);

        resultA.Code.ShouldBe(EffectivePermissionsResultCode.Allowed);
        resultA.Permissions.ShouldBe([EffectivePermissionLevel.Read, EffectivePermissionLevel.Write]);

        resultB.Code.ShouldBe(EffectivePermissionsResultCode.Allowed);
        resultB.Permissions.ShouldBe([EffectivePermissionLevel.Write, EffectivePermissionLevel.Administer]);
    }

    [Fact]
    public async Task PerPrincipalRevokeDoesNotAffectOtherPrincipalsActions()
    {
        EffectivePermissionsReadModelSnapshot snapshot = EffectivePermissionsTestSupport.Snapshot(
            EffectivePermissionsTestSupport.OrganizationGrant("read_metadata", principalId: "user-a"),
            EffectivePermissionsTestSupport.FolderRevoke("read_metadata", principalId: "user-a", sequence: 2),
            EffectivePermissionsTestSupport.OrganizationGrant("read_metadata", principalId: "user-b"));

        EffectivePermissionsQueryResult resultA = await ExecuteAsync(snapshot, principalId: "user-a").ConfigureAwait(true);
        EffectivePermissionsQueryResult resultB = await ExecuteAsync(snapshot, principalId: "user-b").ConfigureAwait(true);

        resultA.Code.ShouldBe(EffectivePermissionsResultCode.DeniedSafe);
        resultA.Permissions.ShouldBeEmpty();

        resultB.Code.ShouldBe(EffectivePermissionsResultCode.Allowed);
        resultB.Permissions.ShouldBe([EffectivePermissionLevel.Read]);
    }

    [Fact]
    public async Task CrossTenantSameFolderIdShouldNotLeakEvidenceAcrossTenants()
    {
        InMemoryEffectivePermissionsReadModel readModel = new();
        readModel.Save(new EffectivePermissionsReadModelSnapshot(
            ManagedTenantId: "tenant-a",
            OrganizationId: "organization-a",
            FolderId: "folder-shared-id",
            LifecycleState: EffectivePermissionsFolderLifecycleState.Active,
            EvidenceRows:
            [
                EffectivePermissionsTestSupport.OrganizationGrant("read_metadata", principalId: "user-shared"),
            ],
            Freshness: new EffectivePermissionsFreshness(
                ReadConsistency: "read_your_writes",
                ObservedAt: EffectivePermissionsTestSupport.Now,
                ProjectionWatermark: "tenant_a_folder_shared_v01",
                Stale: false,
                ReasonCode: null),
            RevocationFreshnessEstablished: true,
            TaskScope: null));
        readModel.Save(new EffectivePermissionsReadModelSnapshot(
            ManagedTenantId: "tenant-b",
            OrganizationId: "organization-b",
            FolderId: "folder-shared-id",
            LifecycleState: EffectivePermissionsFolderLifecycleState.Active,
            EvidenceRows:
            [
                EffectivePermissionsTestSupport.FolderGrant("mutate_files", principalId: "user-shared"),
                EffectivePermissionsTestSupport.FolderGrant("configure_provider_binding", principalId: "user-shared"),
            ],
            Freshness: new EffectivePermissionsFreshness(
                ReadConsistency: "read_your_writes",
                ObservedAt: EffectivePermissionsTestSupport.Now,
                ProjectionWatermark: "tenant_b_folder_shared_v01",
                Stale: false,
                ReasonCode: null),
            RevocationFreshnessEstablished: true,
            TaskScope: null));

        CountingTenantAccessProjectionStore storeA = new(
            EffectivePermissionsTestSupport.TenantProjection(tenantId: "tenant-a", principals: ["user-shared"]));
        CountingTenantAccessProjectionStore storeB = new(
            EffectivePermissionsTestSupport.TenantProjection(tenantId: "tenant-b", principals: ["user-shared"]));

        EffectivePermissionsQueryResult resultA = await EffectivePermissionsTestSupport.Handler(storeA, readModel).HandleAsync(
            new EffectivePermissionsQuery("folder-shared-id", "tenant-a", "user-shared", "corr-a"),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        EffectivePermissionsQueryResult resultB = await EffectivePermissionsTestSupport.Handler(storeB, readModel).HandleAsync(
            new EffectivePermissionsQuery("folder-shared-id", "tenant-b", "user-shared", "corr-b"),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        resultA.Permissions.ShouldBe([EffectivePermissionLevel.Read]);
        resultA.Freshness.ProjectionWatermark.ShouldBe("tenant_a_folder_shared_v01");

        resultB.Permissions.ShouldBe([EffectivePermissionLevel.Write, EffectivePermissionLevel.Administer]);
        resultB.Freshness.ProjectionWatermark.ShouldBe("tenant_b_folder_shared_v01");
    }

    private static async Task<EffectivePermissionsQueryResult> ExecuteAsync(
        EffectivePermissionsReadModelSnapshot snapshot,
        string principalId)
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            EffectivePermissionsTestSupport.TenantProjection(principals: [principalId]));
        RecordingEffectivePermissionsReadModel readModel = new(EffectivePermissionsReadModelResult.Available(snapshot));
        EffectivePermissionsQueryHandler handler = EffectivePermissionsTestSupport.Handler(tenantStore, readModel);

        return await handler.HandleAsync(
            new EffectivePermissionsQuery("folder-a", "tenant-a", principalId, "corr-a"),
            TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
