using Hexalith.Folders.Authorization;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Authorization;

public sealed class EffectivePermissionsRevocationFreshnessTests
{
    [Fact]
    public async Task MissingRevocationFreshnessProofFailsClosedForAllowedAnswers()
    {
        EffectivePermissionsReadModelSnapshot snapshot = EffectivePermissionsTestSupport.Snapshot(
            EffectivePermissionsTestSupport.OrganizationGrant("read_metadata")) with
        {
            RevocationFreshnessEstablished = false,
            Freshness = new EffectivePermissionsFreshness(
                ReadConsistency: "read_your_writes",
                ObservedAt: EffectivePermissionsTestSupport.Now,
                ProjectionWatermark: "folder-a:unknown-revoke-watermark",
                Stale: true,
                ReasonCode: "revocation_freshness_unproven"),
        };

        EffectivePermissionsQueryResult result = await ExecuteAsync(snapshot).ConfigureAwait(true);

        result.Code.ShouldBe(EffectivePermissionsResultCode.ProjectionStale);
        result.AuthorizationOutcome.ShouldBe("denied_safe");
        result.Permissions.ShouldBeEmpty();
        result.Freshness.ReasonCode.ShouldBe("revocation_freshness_unproven");
    }

    [Fact]
    public async Task FreshRevokeRemovesActionAndCarriesRevocationWatermark()
    {
        EffectivePermissionsReadModelSnapshot snapshot = EffectivePermissionsTestSupport.Snapshot(
            EffectivePermissionsTestSupport.OrganizationGrant("read_metadata"),
            EffectivePermissionsTestSupport.FolderGrant("read_metadata", sequence: 1),
            EffectivePermissionsTestSupport.FolderRevoke("read_metadata", sequence: 2)) with
        {
            Freshness = new EffectivePermissionsFreshness(
                ReadConsistency: "read_your_writes",
                ObservedAt: EffectivePermissionsTestSupport.Now,
                ProjectionWatermark: "folder-a:revoke-2",
                Stale: false,
                ReasonCode: null),
        };

        EffectivePermissionsQueryResult result = await ExecuteAsync(snapshot).ConfigureAwait(true);

        result.Code.ShouldBe(EffectivePermissionsResultCode.DeniedSafe);
        result.Permissions.ShouldBeEmpty();
        result.Freshness.ProjectionWatermark.ShouldBe("folder-a:revoke-2");
    }

    private static async Task<EffectivePermissionsQueryResult> ExecuteAsync(EffectivePermissionsReadModelSnapshot snapshot)
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            EffectivePermissionsTestSupport.TenantProjection(principals: ["user-a"]));
        RecordingEffectivePermissionsReadModel readModel = new(EffectivePermissionsReadModelResult.Available(snapshot));
        EffectivePermissionsQueryHandler handler = EffectivePermissionsTestSupport.Handler(tenantStore, readModel);

        return await handler.HandleAsync(
            new EffectivePermissionsQuery("folder-a", "tenant-a", "user-a", "corr-a"),
            TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
