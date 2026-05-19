using System.Text.Json;
using Hexalith.Folders.Authorization;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Authorization;

public sealed class EffectivePermissionsMetadataLeakageTests
{
    [Fact]
    public async Task SafeDenialShouldNotEchoUnauthorizedResourceIdentifiers()
    {
        CountingTenantAccessProjectionStore tenantStore = new();
        RecordingEffectivePermissionsReadModel readModel = new(EffectivePermissionsReadModelResult.Available(
            EffectivePermissionsTestSupport.Snapshot()));
        EffectivePermissionsQueryHandler handler = EffectivePermissionsTestSupport.Handler(tenantStore, readModel);

        EffectivePermissionsQueryResult result = await handler.HandleAsync(
            new EffectivePermissionsQuery(
                FolderId: "folder-secret-victim",
                AuthoritativeTenantId: "tenant-attacker",
                PrincipalId: "principal-secret-victim",
                CorrelationId: "corr-a",
                ClientControlledTenantIds: new Dictionary<string, string?>
                {
                    ["header"] = "tenant-secret-victim",
                }),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        string serialized = JsonSerializer.Serialize(result);

        result.FolderId.ShouldBeNull();
        serialized.ShouldNotContain("folder-secret-victim", Case.Sensitive);
        serialized.ShouldNotContain("tenant-secret-victim", Case.Sensitive);
        serialized.ShouldNotContain("principal-secret-victim", Case.Sensitive);
        readModel.Requests.ShouldBe(0);
    }

    [Theory]
    [InlineData("raw-auth-header")]
    [InlineData("provider-token")]
    [InlineData("repo-name")]
    [InlineData("branch-name")]
    [InlineData("file-path")]
    [InlineData("user@example.test")]
    [InlineData("group-display-name")]
    public async Task SafeResultsShouldNotExposePrincipalSourceLabelsOrSensitiveSentinels(string sentinel)
    {
        EffectivePermissionsReadModelSnapshot snapshot = EffectivePermissionsTestSupport.Snapshot(
            EffectivePermissionsTestSupport.OrganizationGrant("read_metadata", principalId: "user-a")) with
        {
            DiagnosticSentinels = [sentinel],
        };

        CountingTenantAccessProjectionStore tenantStore = new(
            EffectivePermissionsTestSupport.TenantProjection(principals: ["user-a"]));
        RecordingEffectivePermissionsReadModel readModel = new(EffectivePermissionsReadModelResult.Available(snapshot));
        EffectivePermissionsQueryHandler handler = EffectivePermissionsTestSupport.Handler(tenantStore, readModel);

        EffectivePermissionsQueryResult result = await handler.HandleAsync(
            new EffectivePermissionsQuery("folder-a", "tenant-a", "user-a", "corr-a"),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        JsonSerializer.Serialize(result).ShouldNotContain(sentinel, Case.Sensitive);
    }
}
