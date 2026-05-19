using System.Reflection;
using Hexalith.Folders.Client.Generated;
using Newtonsoft.Json;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Client.Tests;

public sealed class LifecycleStatusClientConformanceTests
{
    [Fact]
    public void GeneratedClientExposesGetFolderLifecycleStatusOperation()
    {
        MethodInfo method = typeof(IClient).GetMethods()
            .Single(method => method.Name == "GetFolderLifecycleStatusAsync" && method.GetParameters().Length == 4);

        method.ReturnType.ShouldBe(typeof(Task<FolderLifecycleStatus>));
        method.GetParameters().Select(static parameter => parameter.Name).ToArray()
            .ShouldBe(["folderId", "x_Correlation_Id", "x_Hexalith_Freshness", "cancellationToken"]);
    }

    [Fact]
    public void GeneratedFolderLifecycleStatusCarriesActiveUnboundShapeWithoutOptionalBindingFields()
    {
        var status = new FolderLifecycleStatus
        {
            FolderId = "folder_opaque_active_unbound",
            LifecycleState = LifecycleState.Ready,
            Archived = false,
            Freshness = new FreshnessMetadata
            {
                ReadConsistency = ReadConsistencyClass.Eventually_consistent,
                ObservedAt = new DateTimeOffset(2026, 5, 19, 12, 0, 0, TimeSpan.Zero),
                ProjectionWatermark = "projection_watermark_opaque",
                Stale = false,
            },
        };

        string json = JsonConvert.SerializeObject(status);

        json.ShouldContain("\"folderId\":\"folder_opaque_active_unbound\"");
        json.ShouldContain("\"lifecycleState\":\"ready\"");
        json.ShouldContain("\"archived\":false");
        json.ShouldContain("\"readConsistency\":\"eventually_consistent\"");
        json.ShouldNotContain("repositoryBindingId", Case.Sensitive);
        json.ShouldNotContain("providerBindingRef", Case.Sensitive);
    }

    [Fact]
    public void GeneratedFolderLifecycleStatusCarriesActiveBoundOpaqueBindingFields()
    {
        var status = new FolderLifecycleStatus
        {
            FolderId = "folder_opaque_active_bound",
            LifecycleState = LifecycleState.Ready,
            Archived = false,
            RepositoryBindingId = "repository_binding_opaque_safe",
            ProviderBindingRef = "provider_binding_opaque_safe",
            Freshness = new FreshnessMetadata
            {
                ReadConsistency = ReadConsistencyClass.Eventually_consistent,
                ObservedAt = new DateTimeOffset(2026, 5, 19, 12, 0, 0, TimeSpan.Zero),
                ProjectionWatermark = "projection_watermark_opaque",
                Stale = false,
            },
        };

        string json = JsonConvert.SerializeObject(status);

        json.ShouldContain("\"repositoryBindingId\":\"repository_binding_opaque_safe\"");
        json.ShouldContain("\"providerBindingRef\":\"provider_binding_opaque_safe\"");
        json.ShouldNotContain("https://provider.invalid", Case.Sensitive);
        json.ShouldNotContain("branch-name", Case.Sensitive);
        json.ShouldNotContain("credential-material", Case.Sensitive);
    }
}
