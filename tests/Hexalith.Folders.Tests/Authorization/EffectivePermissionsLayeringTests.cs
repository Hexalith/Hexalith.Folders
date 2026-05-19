using System.Text.Json;
using Hexalith.Folders.Authorization;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Authorization;

public sealed class EffectivePermissionsLayeringTests
{
    [Fact]
    public async Task OrganizationBaselineGrantShouldProduceReadPermission()
    {
        EffectivePermissionsQueryResult result = await ExecuteAsync(
            EffectivePermissionsTestSupport.OrganizationGrant("read_metadata")).ConfigureAwait(true);

        result.Code.ShouldBe(EffectivePermissionsResultCode.Allowed);
        result.AuthorizationOutcome.ShouldBe("allowed");
        result.Permissions.ShouldBe([EffectivePermissionLevel.Read]);
        result.Freshness.ProjectionWatermark.ShouldBe("folder-a:11");
    }

    [Fact]
    public async Task FolderGrantShouldProduceWriteAndAdministerPermissions()
    {
        EffectivePermissionsQueryResult result = await ExecuteAsync(
            EffectivePermissionsTestSupport.FolderGrant("mutate_files"),
            EffectivePermissionsTestSupport.FolderGrant("configure_provider_binding")).ConfigureAwait(true);

        result.Permissions.ShouldBe([EffectivePermissionLevel.Write, EffectivePermissionLevel.Administer]);
    }

    [Fact]
    public async Task FolderRevokeShouldWinOverOrganizationAndFolderGrantForSameAction()
    {
        EffectivePermissionsQueryResult result = await ExecuteAsync(
            EffectivePermissionsTestSupport.OrganizationGrant("read_metadata"),
            EffectivePermissionsTestSupport.FolderGrant("read_metadata", sequence: 1),
            EffectivePermissionsTestSupport.FolderRevoke("read_metadata", sequence: 2)).ConfigureAwait(true);

        result.Code.ShouldBe(EffectivePermissionsResultCode.DeniedSafe);
        result.AuthorizationOutcome.ShouldBe("denied_safe");
        result.Permissions.ShouldBeEmpty();
    }

    [Fact]
    public async Task EquivalentEvidenceShouldProduceStableCanonicalPermissions()
    {
        EffectivePermissionEvidenceRow[] first =
        [
            EffectivePermissionsTestSupport.FolderGrant("commit", sequence: 3),
            EffectivePermissionsTestSupport.OrganizationGrant("read_metadata"),
            EffectivePermissionsTestSupport.FolderGrant("commit", sequence: 3),
            EffectivePermissionsTestSupport.FolderGrant("read_file_content", sequence: 1),
        ];

        EffectivePermissionEvidenceRow[] second =
        [
            EffectivePermissionsTestSupport.FolderGrant("read_file_content", sequence: 1),
            EffectivePermissionsTestSupport.FolderGrant("commit", sequence: 3),
            EffectivePermissionsTestSupport.OrganizationGrant("read_metadata"),
        ];

        EffectivePermissionsQueryResult resultA = await ExecuteAsync(first).ConfigureAwait(true);
        EffectivePermissionsQueryResult resultB = await ExecuteAsync(second).ConfigureAwait(true);

        resultA.Permissions.ShouldBe(resultB.Permissions);
        JsonSerializer.Serialize(resultA).ShouldBe(JsonSerializer.Serialize(resultB));
    }

    private static async Task<EffectivePermissionsQueryResult> ExecuteAsync(params EffectivePermissionEvidenceRow[] rows)
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            EffectivePermissionsTestSupport.TenantProjection(principals: ["user-a"]));
        RecordingEffectivePermissionsReadModel readModel = new(EffectivePermissionsReadModelResult.Available(
            EffectivePermissionsTestSupport.Snapshot(rows)));
        EffectivePermissionsQueryHandler handler = EffectivePermissionsTestSupport.Handler(tenantStore, readModel);

        return await handler.HandleAsync(
            new EffectivePermissionsQuery(
                FolderId: "folder-a",
                AuthoritativeTenantId: "tenant-a",
                PrincipalId: "user-a",
                CorrelationId: "corr-a"),
            TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
