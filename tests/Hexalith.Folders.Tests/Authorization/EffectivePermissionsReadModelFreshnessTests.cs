using Hexalith.Folders.Authorization;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Authorization;

public sealed class EffectivePermissionsReadModelFreshnessTests
{
    [Fact]
    public async Task DoesNotFallbackToProviderWhenPermissionProjectionUnavailable()
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            EffectivePermissionsTestSupport.TenantProjection(principals: ["user-a"]));
        RecordingEffectivePermissionsReadModel readModel = new(EffectivePermissionsReadModelResult.Unavailable(
            "read_model_unavailable",
            EffectivePermissionsTestSupport.Now));
        EffectivePermissionsQueryHandler handler = EffectivePermissionsTestSupport.Handler(tenantStore, readModel);

        EffectivePermissionsQueryResult result = await handler.HandleAsync(
            new EffectivePermissionsQuery("folder-a", "tenant-a", "user-a", "corr-a"),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBe(EffectivePermissionsResultCode.ReadModelUnavailable);
        result.Permissions.ShouldBeEmpty();
        result.Freshness.Stale.ShouldBeTrue();
        readModel.Requests.ShouldBe(1);
    }

    [Fact]
    public async Task StalePermissionProjectionFailsClosedWithFreshnessEvidence()
    {
        EffectivePermissionsReadModelSnapshot stale = EffectivePermissionsTestSupport.Snapshot(
            EffectivePermissionsTestSupport.OrganizationGrant("read_metadata")) with
        {
            Freshness = new EffectivePermissionsFreshness(
                ReadConsistency: "read_your_writes",
                ObservedAt: EffectivePermissionsTestSupport.Now.AddMinutes(-10),
                ProjectionWatermark: "folder-a:stale",
                Stale: true,
                ReasonCode: "projection_stale"),
        };

        EffectivePermissionsQueryResult result = await ExecuteAsync(EffectivePermissionsReadModelResult.Stale(stale)).ConfigureAwait(true);

        result.Code.ShouldBe(EffectivePermissionsResultCode.ProjectionStale);
        result.AuthorizationOutcome.ShouldBe("denied_safe");
        result.Permissions.ShouldBeEmpty();
        result.Freshness.ProjectionWatermark.ShouldBe("folder-a:stale");
        result.Freshness.Stale.ShouldBeTrue();
    }

    private static async Task<EffectivePermissionsQueryResult> ExecuteAsync(EffectivePermissionsReadModelResult readModelResult)
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            EffectivePermissionsTestSupport.TenantProjection(principals: ["user-a"]));
        RecordingEffectivePermissionsReadModel readModel = new(readModelResult);
        EffectivePermissionsQueryHandler handler = EffectivePermissionsTestSupport.Handler(tenantStore, readModel);

        return await handler.HandleAsync(
            new EffectivePermissionsQuery("folder-a", "tenant-a", "user-a", "corr-a"),
            TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
