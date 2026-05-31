using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.Folders.Aspire;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.IntegrationTests;

public sealed class AspireTopologyTests
{
    [Fact]
    public void FoldersAspireModuleShouldExposeStableDaprAppIdsAndComponentNames()
    {
        FoldersAspireModule.EventStoreAppId.ShouldBe("eventstore");
        FoldersAspireModule.TenantsAppId.ShouldBe("tenants");
        FoldersAspireModule.FoldersAppId.ShouldBe("folders");
        FoldersAspireModule.FoldersWorkersAppId.ShouldBe("folders-workers");
        FoldersAspireModule.FoldersUiAppId.ShouldBe("folders-ui");
        FoldersAspireModule.StateStoreComponentName.ShouldBe("statestore");
        FoldersAspireModule.PubSubComponentName.ShouldBe("pubsub");
    }

    [Fact]
    public void AddFoldersSharedDaprComponentsShouldRegisterStateStoreAndPubSubInResourceCollection()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        (IResourceBuilder<IDaprComponentResource> stateStore, IResourceBuilder<IDaprComponentResource> pubSub) =
            builder.AddFoldersSharedDaprComponents();

        stateStore.Resource.Name.ShouldBe(FoldersAspireModule.StateStoreComponentName);
        pubSub.Resource.Name.ShouldBe(FoldersAspireModule.PubSubComponentName);

        IResource[] resources = [.. builder.Resources];
        resources.ShouldContain(r => string.Equals(r.Name, FoldersAspireModule.StateStoreComponentName, StringComparison.Ordinal));
        resources.ShouldContain(r => string.Equals(r.Name, FoldersAspireModule.PubSubComponentName, StringComparison.Ordinal));
    }

    [Fact]
    public void HexalithFoldersResourcesShouldExposeAllRequiredProjectAndComponentBuilders()
    {
        // Shape contract: structure tests against this record catch breakage if a future refactor
        // accidentally drops a project or component from the topology surface.
        System.Reflection.PropertyInfo[] properties = typeof(HexalithFoldersResources).GetProperties();
        string[] names = [.. properties.Select(static p => p.Name)];

        names.ShouldContain("StateStore");
        names.ShouldContain("PubSub");
        names.ShouldContain("EventStore");
        names.ShouldContain("Tenants");
        names.ShouldContain("Folders");
        names.ShouldContain("FoldersWorkers");
        names.ShouldContain("FoldersUi");
    }

    [Fact]
    public void AddHexalithFoldersShouldAttachDaprSidecarsForEveryProductionAppId()
    {
        const string daprConfigPath = "DaprComponents/accesscontrol.yaml";
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        IResourceBuilder<ProjectResource> eventStore = builder.AddProject("eventstore-test", RepositoryPath("src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj"));
        IResourceBuilder<ProjectResource> tenants = builder.AddProject("tenants-test", RepositoryPath("src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj"));
        IResourceBuilder<ProjectResource> folders = builder.AddProject("folders-test", RepositoryPath("src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj"));
        IResourceBuilder<ProjectResource> foldersWorkers = builder.AddProject("folders-workers-test", RepositoryPath("src/Hexalith.Folders.Workers/Hexalith.Folders.Workers.csproj"));
        IResourceBuilder<ProjectResource> foldersUi = builder.AddProject("folders-ui-test", RepositoryPath("src/Hexalith.Folders.UI/Hexalith.Folders.UI.csproj"));

        HexalithFoldersResources resources = builder.AddHexalithFolders(
            eventStore,
            tenants,
            folders,
            foldersWorkers,
            foldersUi,
            daprConfigPath);

        AssertSidecarOptions(resources.EventStore.Resource, FoldersAspireModule.EventStoreAppId, daprConfigPath);
        AssertSidecarOptions(resources.Tenants.Resource, FoldersAspireModule.TenantsAppId, daprConfigPath);
        AssertSidecarOptions(resources.Folders.Resource, FoldersAspireModule.FoldersAppId, daprConfigPath);
        AssertSidecarOptions(resources.FoldersWorkers.Resource, FoldersAspireModule.FoldersWorkersAppId, daprConfigPath);
        AssertSidecarOptions(resources.FoldersUi.Resource, FoldersAspireModule.FoldersUiAppId, daprConfigPath);
    }

    private static void AssertSidecarOptions(ProjectResource resource, string expectedAppId, string expectedConfigPath)
    {
        DaprSidecarAnnotation sidecar = resource.Annotations.OfType<DaprSidecarAnnotation>().Single();
        DaprSidecarOptionsAnnotation options = sidecar.Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single();

        options.Options.AppId.ShouldBe(expectedAppId);
        options.Options.Config.ShouldBe(expectedConfigPath);
    }

    private static string RepositoryPath(string relativePath)
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            string candidate = Path.Combine(directory, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            if (File.Exists(Path.Combine(directory, "Hexalith.Folders.slnx")))
            {
                return candidate;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        return Path.Combine(AppContext.BaseDirectory, relativePath);
    }
}
