using Hexalith.Folders.Authorization;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Authorization;

public sealed class EffectivePermissionsAuthorizationGateTests
{
    [Theory]
    [InlineData("tenant-a")]
    [InlineData("organization_baseline")]
    [InlineData("provider-binding-a")]
    public async Task ReservedActorIdsReturnSafeAbsenceBeforeTenantOrProjectionLookup(string folderId)
    {
        CountingTenantAccessProjectionStore tenants = new();
        RecordingEffectivePermissionsReadModel readModel = new(EffectivePermissionsReadModelResult.Available(
            EffectivePermissionsTestSupport.Snapshot()));
        var result = await EffectivePermissionsTestSupport.Handler(tenants, readModel).HandleAsync(
            new EffectivePermissionsQuery(folderId, "tenant-a", "user-a", "corr"),
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        result.Code.ShouldBe(EffectivePermissionsResultCode.NotFoundSafe);
        result.FolderId.ShouldBeNull();
        result.Permissions.ShouldBeEmpty();
        tenants.Gets.ShouldBe(0);
        readModel.Requests.ShouldBe(0);
    }

    [Fact]
    public async Task RejectsBeforeFolderProjectionWhenTenantMissing()
    {
        CountingTenantAccessProjectionStore tenantStore = new();
        RecordingEffectivePermissionsReadModel readModel = new(EffectivePermissionsReadModelResult.Available(
            EffectivePermissionsTestSupport.Snapshot()));
        EffectivePermissionsQueryHandler handler = EffectivePermissionsTestSupport.Handler(tenantStore, readModel);

        EffectivePermissionsQueryResult result = await handler.HandleAsync(
            new EffectivePermissionsQuery(
                FolderId: "folder-a",
                AuthoritativeTenantId: null,
                PrincipalId: "user-a",
                CorrelationId: "corr-a"),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBe(EffectivePermissionsResultCode.AuthenticationRequired);
        result.FolderId.ShouldBeNull();
        tenantStore.Gets.ShouldBe(0);
        readModel.Requests.ShouldBe(0);
    }

    [Fact]
    public async Task RejectsBeforeAclProjectionWhenTenantDenied()
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            EffectivePermissionsTestSupport.TenantProjection(principals: ["user-b"]));
        RecordingEffectivePermissionsReadModel readModel = new(EffectivePermissionsReadModelResult.Available(
            EffectivePermissionsTestSupport.Snapshot()));
        EffectivePermissionsQueryHandler handler = EffectivePermissionsTestSupport.Handler(tenantStore, readModel);

        EffectivePermissionsQueryResult result = await handler.HandleAsync(
            new EffectivePermissionsQuery(
                FolderId: "folder-a",
                AuthoritativeTenantId: "tenant-a",
                PrincipalId: "user-a",
                CorrelationId: "corr-a"),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBe(EffectivePermissionsResultCode.AuthorizationDenied);
        result.FolderId.ShouldBeNull();
        tenantStore.Gets.ShouldBe(1);
        readModel.Requests.ShouldBe(0);
    }

    [Fact]
    public async Task ClientControlledTenantMismatchRejectsBeforeTenantProjectionLookup()
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            EffectivePermissionsTestSupport.TenantProjection(principals: ["user-a"]));
        RecordingEffectivePermissionsReadModel readModel = new(EffectivePermissionsReadModelResult.Available(
            EffectivePermissionsTestSupport.Snapshot()));
        EffectivePermissionsQueryHandler handler = EffectivePermissionsTestSupport.Handler(tenantStore, readModel);

        EffectivePermissionsQueryResult result = await handler.HandleAsync(
            new EffectivePermissionsQuery(
                FolderId: "folder-a",
                AuthoritativeTenantId: "tenant-a",
                PrincipalId: "user-a",
                CorrelationId: "corr-a",
                ClientControlledTenantIds: new Dictionary<string, string?>
                {
                    ["header"] = "tenant-b",
                }),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBe(EffectivePermissionsResultCode.AuthorizationDenied);
        result.FolderId.ShouldBeNull();
        tenantStore.Gets.ShouldBe(0);
        readModel.Requests.ShouldBe(0);
    }
}
