using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

using CommunityToolkit.Aspire.Hosting.Dapr;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.Tenants.IntegrationTests;

/// <summary>
/// Verifies the package-mode AppHost model binds Memories to the consumer-owned secret store.
/// </summary>
[Trait("Category", "Integration")]
public sealed class MemoriesSecretStoreResourceGraphTests
{
    private const string MemoriesResourceName = "memories";
    private const string SecretStoreResourceName = "memories-secretstore";

    /// <summary>
    /// Builds the model without starting resources and verifies the exact Memories secret-store relationships.
    /// </summary>
    [Fact]
    public async Task PackageModeAppHostUsesTheConsumerOwnedMemoriesSecretStore()
    {
        await using IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Hexalith_Tenants_AppHost>();
        await using DistributedApplication app = await builder.BuildAsync();

        DistributedApplicationModel model = app.Services.GetRequiredService<DistributedApplicationModel>();
        DaprComponentResource secretStore = model.Resources
            .OfType<DaprComponentResource>()
            .Where(static resource => resource.Name == SecretStoreResourceName)
            .ShouldHaveSingleItem();

        string expectedPath = Path.GetFullPath(
            Path.Combine(builder.AppHostDirectory, "DaprComponents", "secretstore.memories.yaml"));
        secretStore.Type.ShouldBe("secretstores.local.file");
        Path.GetFullPath(secretStore.Options.ShouldNotBeNull().LocalPath.ShouldNotBeNull()).ShouldBe(expectedPath);

        ProjectResource memories = model.Resources
            .OfType<ProjectResource>()
            .Where(static resource => resource.Name == MemoriesResourceName)
            .ShouldHaveSingleItem();
        IDaprSidecarResource sidecar = memories.Annotations
            .OfType<DaprSidecarAnnotation>()
            .ShouldHaveSingleItem()
            .Sidecar;

        memories.Annotations
            .OfType<DaprComponentReferenceAnnotation>()
            .Where(annotation => ReferenceEquals(annotation.Component, secretStore))
            .ShouldHaveSingleItem();
        sidecar.Annotations
            .OfType<DaprComponentReferenceAnnotation>()
            .Where(annotation => ReferenceEquals(annotation.Component, secretStore))
            .ShouldHaveSingleItem();
        memories.Annotations
            .OfType<WaitAnnotation>()
            .Where(annotation => ReferenceEquals(annotation.Resource, secretStore))
            .ShouldHaveSingleItem();
    }
}
