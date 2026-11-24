using Hexalith.Folders.Authorization;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Authorization;

public sealed class EffectivePermissionsSnapshotScopeTests
{
    [Theory]
    [InlineData("tenant-b", "folder-a")]
    [InlineData("tenant-a", "folder-b")]
    [InlineData("TENANT-A", "folder-a")]
    public async Task HandlerShouldNotComputePermissionsFromAForeignScopeSnapshot(string snapshotTenantId, string snapshotFolderId)
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            EffectivePermissionsTestSupport.TenantProjection(principals: ["user-a"]));
        EffectivePermissionsReadModelSnapshot foreign = EffectivePermissionsTestSupport.Snapshot(
            EffectivePermissionsTestSupport.FolderGrant("mutate_files")) with
        {
            ManagedTenantId = snapshotTenantId,
            FolderId = snapshotFolderId,
        };
        RecordingEffectivePermissionsReadModel readModel = new(EffectivePermissionsReadModelResult.Available(foreign));

        EffectivePermissionsQueryResult result = await EffectivePermissionsTestSupport.Handler(tenantStore, readModel)
            .HandleAsync(
                new EffectivePermissionsQuery(
                    FolderId: "folder-a",
                    AuthoritativeTenantId: "tenant-a",
                    PrincipalId: "user-a",
                    CorrelationId: "corr-a"),
                TestContext.Current.CancellationToken).ConfigureAwait(true);

        readModel.Requests.ShouldBe(1);
        result.Code.ShouldBe(EffectivePermissionsResultCode.ReadModelUnavailable);
        result.Permissions.ShouldBeEmpty();
        result.Freshness.ProjectionWatermark.ShouldNotBe(EffectivePermissionsTestSupport.FolderWatermark);
    }
}
