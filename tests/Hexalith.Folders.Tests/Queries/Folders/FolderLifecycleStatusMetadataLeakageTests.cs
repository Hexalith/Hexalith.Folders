using System.Text.Json;
using Hexalith.Folders.Queries.Folders;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Queries.Folders;

public sealed class FolderLifecycleStatusMetadataLeakageTests
{
    [Fact]
    public async Task SafeDenialShouldNotEchoUnauthorizedResourceIdentifiers()
    {
        CountingTenantAccessProjectionStore tenantStore = new();
        CountingLifecycleStatusReadModel readModel = new(FolderLifecycleStatusReadModelResult.Available(
            FolderLifecycleStatusTestSupport.ActiveBound(
                "repository_binding_secret_victim",
                "provider_binding_secret_victim")));
        FolderLifecycleStatusQueryHandler handler = FolderLifecycleStatusTestSupport.Handler(tenantStore, readModel);

        FolderLifecycleStatusQueryResult result = await handler.HandleAsync(
            FolderLifecycleStatusTestSupport.Query(
                folderId: "folder-secret-victim",
                tenantId: "tenant-attacker",
                principalId: "principal-secret-victim",
                clientTenantValues: new Dictionary<string, string?>
                {
                    ["header"] = "tenant-secret-victim",
                }),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        string serialized = JsonSerializer.Serialize(result);

        result.FolderId.ShouldBeNull();
        serialized.ShouldNotContain("folder-secret-victim", Case.Sensitive);
        serialized.ShouldNotContain("tenant-secret-victim", Case.Sensitive);
        serialized.ShouldNotContain("principal-secret-victim", Case.Sensitive);
        serialized.ShouldNotContain("repository_binding_secret_victim", Case.Sensitive);
        serialized.ShouldNotContain("provider_binding_secret_victim", Case.Sensitive);
        readModel.Requests.ShouldBe(0);
    }

    [Theory]
    [InlineData("raw-auth-header")]
    [InlineData("provider-token")]
    [InlineData("credential-material")]
    [InlineData("https://provider.invalid/org/repository")]
    [InlineData("repo-name")]
    [InlineData("branch-name")]
    [InlineData("C:\\tenant\\workspace\\secret.txt")]
    [InlineData("generated context payload")]
    [InlineData("user@example.test")]
    [InlineData("raw provider payload")]
    public async Task SuccessfulStatusShouldNotExposeDiagnosticOrProviderSentinels(string sentinel)
    {
        FolderLifecycleStatusReadModelSnapshot snapshot = FolderLifecycleStatusTestSupport.ActiveBound(
            "repository_binding_opaque_safe",
            "provider_binding_opaque_safe") with
        {
            DiagnosticSentinels = [sentinel],
        };
        CountingTenantAccessProjectionStore tenantStore = new(
            FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-a"]));
        CountingLifecycleStatusReadModel readModel = new(FolderLifecycleStatusReadModelResult.Available(snapshot));
        FolderLifecycleStatusQueryHandler handler = FolderLifecycleStatusTestSupport.Handler(tenantStore, readModel);

        FolderLifecycleStatusQueryResult result = await handler.HandleAsync(
            FolderLifecycleStatusTestSupport.Query(),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        string serialized = JsonSerializer.Serialize(result);

        result.Code.ShouldBe(FolderLifecycleStatusResultCode.Allowed);
        serialized.ShouldContain("repository_binding_opaque_safe", Case.Sensitive);
        serialized.ShouldContain("provider_binding_opaque_safe", Case.Sensitive);
        serialized.ShouldNotContain(sentinel, Case.Sensitive);
    }
}
