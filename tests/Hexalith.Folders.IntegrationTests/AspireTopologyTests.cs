using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.EventStore.Aspire;
using Hexalith.Folders.Aspire;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.IntegrationTests;

public sealed class AspireTopologyTests
{
    private const string DaprConfigPath = "DaprComponents/accesscontrol.yaml";

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
    public void AddHexalithFoldersShouldReusePlatformComponentsWithoutCreatingNewDaprComponents()
    {
        // Epic 9 / AC3: the shared statestore + pubsub components are created by the platform EventStore
        // composition, not in Folders code. AddHexalithFolders must add zero new Dapr components and reuse the
        // exact platform-provided component instances (the previous AddFoldersSharedDaprComponents in-code
        // creation is gone).
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        FoldersTopology topology = BuildGatewayOnlyComposition(builder);

        int daprComponentsBeforeFolders = builder.Resources.OfType<IDaprComponentResource>().Count();

        HexalithFoldersResources resources = builder.AddHexalithFolders(
            topology.EventStore.StateStore,
            topology.EventStore.PubSub,
            topology.EventStore.EventStore,
            topology.Tenants,
            topology.Folders,
            topology.FoldersWorkers,
            topology.FoldersUi,
            DaprConfigPath);

        int daprComponentsAfterFolders = builder.Resources.OfType<IDaprComponentResource>().Count();

        daprComponentsAfterFolders.ShouldBe(daprComponentsBeforeFolders);
        resources.StateStore.ShouldBeSameAs(topology.EventStore.StateStore);
        resources.PubSub.ShouldBeSameAs(topology.EventStore.PubSub);
        resources.StateStore.Resource.Name.ShouldBe(FoldersAspireModule.StateStoreComponentName);
        resources.PubSub.Resource.Name.ShouldBe(FoldersAspireModule.PubSubComponentName);
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
        // Drive the new composition end-to-end: the eventstore + tenants sidecars are attached by the platform
        // helpers (gateway-only AddHexalithEventStore + AddEventStoreDomainModule), and folders/folders-workers/
        // folders-ui by AddHexalithFolders. All five production sidecars must carry the correct AppId + Config.
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        FoldersTopology topology = BuildGatewayOnlyComposition(builder);

        HexalithFoldersResources resources = builder.AddHexalithFolders(
            topology.EventStore.StateStore,
            topology.EventStore.PubSub,
            topology.EventStore.EventStore,
            topology.Tenants,
            topology.Folders,
            topology.FoldersWorkers,
            topology.FoldersUi,
            DaprConfigPath);

        AssertSidecarOptions(topology.EventStore.EventStore.Resource, FoldersAspireModule.EventStoreAppId, DaprConfigPath);
        AssertSidecarOptions(topology.Tenants.Resource, FoldersAspireModule.TenantsAppId, DaprConfigPath);
        AssertSidecarOptions(resources.Folders.Resource, FoldersAspireModule.FoldersAppId, DaprConfigPath);
        AssertSidecarOptions(resources.FoldersWorkers.Resource, FoldersAspireModule.FoldersWorkersAppId, DaprConfigPath);
        AssertSidecarOptions(resources.FoldersUi.Resource, FoldersAspireModule.FoldersUiAppId, DaprConfigPath);
    }

    [Fact]
    public void GatewayOnlyCompositionShouldNotProduceAnyEventStoreAdminResources()
    {
        // AC1: Folders composes EventStore gateway-only (adminServer: null, adminUI: null), so the running
        // topology must contain NO eventstore-admin or eventstore-admin-ui resource (today's topology has
        // neither — this must be preserved). Prove it two ways: the platform record exposes no admin-server /
        // admin-UI project, and the composed Dapr sidecars are exactly the five stable production app-ids with
        // no admin app-id leaking in.
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        FoldersTopology topology = BuildGatewayOnlyComposition(builder);

        _ = builder.AddHexalithFolders(
            topology.EventStore.StateStore,
            topology.EventStore.PubSub,
            topology.EventStore.EventStore,
            topology.Tenants,
            topology.Folders,
            topology.FoldersWorkers,
            topology.FoldersUi,
            DaprConfigPath);

        // Gateway-only: the platform composition exposes no admin server / admin UI project resource.
        topology.EventStore.AdminServer.ShouldBeNull();
        topology.EventStore.AdminUI.ShouldBeNull();

        string[] sidecarAppIds = [.. builder.Resources
            .OfType<ProjectResource>()
            .SelectMany(static resource => resource.Annotations.OfType<DaprSidecarAnnotation>())
            .SelectMany(static sidecar => sidecar.Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>())
            .Select(static options => options.Options.AppId)
            .Where(static appId => appId is not null)
            .Select(static appId => appId!)
            .Order(StringComparer.Ordinal)];

        // Exactly the five stable production sidecars — no eventstore-admin / eventstore-admin-ui sidecar.
        sidecarAppIds.ShouldBe(
        [
            FoldersAspireModule.EventStoreAppId,
            FoldersAspireModule.FoldersAppId,
            FoldersAspireModule.FoldersUiAppId,
            FoldersAspireModule.FoldersWorkersAppId,
            FoldersAspireModule.TenantsAppId,
        ]);

        sidecarAppIds.ShouldNotContain("eventstore-admin");
        sidecarAppIds.ShouldNotContain("eventstore-admin-ui");
    }

    [Fact]
    public void AddHexalithEventStoreWithCheckedInYamlPathsShouldSourceReusableStateStoreAndPubSubComponents()
    {
        // AC3 production wiring: the AppHost passes the checked-in DaprComponents YAML files to the platform
        // helper via stateStoreComponentPath / pubSubComponentPath (the LocalPath branch). The other topology
        // tests drive the in-code component-generation branch (null paths) instead, so this test pins the exact
        // branch Program.cs depends on at boot — proving the helper, when handed the real statestore.yaml /
        // pubsub.yaml, still yields non-null statestore/pubsub components that AddHexalithFolders reuses without
        // creating new ones.
        string stateStorePath = RepositoryPath("src/Hexalith.Folders.AppHost/DaprComponents/statestore.yaml");
        string pubSubPath = RepositoryPath("src/Hexalith.Folders.AppHost/DaprComponents/pubsub.yaml");
        File.Exists(stateStorePath).ShouldBeTrue($"Expected the checked-in state-store YAML at {stateStorePath}.");
        File.Exists(pubSubPath).ShouldBeTrue($"Expected the checked-in pub/sub YAML at {pubSubPath}.");

        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        IResourceBuilder<ProjectResource> eventStoreProject = builder.AddProject("eventstore-test", RepositoryPath("src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj"));
        IResourceBuilder<ProjectResource> tenants = builder.AddProject("tenants-test", RepositoryPath("src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj"));
        IResourceBuilder<ProjectResource> folders = builder.AddProject("folders-test", RepositoryPath("src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj"));
        IResourceBuilder<ProjectResource> foldersWorkers = builder.AddProject("folders-workers-test", RepositoryPath("src/Hexalith.Folders.Workers/Hexalith.Folders.Workers.csproj"));
        IResourceBuilder<ProjectResource> foldersUi = builder.AddProject("folders-ui-test", RepositoryPath("src/Hexalith.Folders.UI/Hexalith.Folders.UI.csproj"));

        // Source the shared components from the checked-in YAML (LocalPath) exactly like the AppHost does.
        HexalithEventStoreResources eventStore = builder.AddHexalithEventStore(
            eventStoreProject,
            adminServer: null,
            adminUI: null,
            eventStoreDaprConfigPath: DaprConfigPath,
            adminServerDaprConfigPath: null,
            resiliencyConfigPath: null,
            stateStoreComponentPath: stateStorePath,
            pubSubComponentPath: pubSubPath);

        eventStore.StateStore.ShouldNotBeNull();
        eventStore.PubSub.ShouldNotBeNull();
        eventStore.StateStore.Resource.Name.ShouldBe(FoldersAspireModule.StateStoreComponentName);
        eventStore.PubSub.Resource.Name.ShouldBe(FoldersAspireModule.PubSubComponentName);
        eventStore.AdminServer.ShouldBeNull();
        eventStore.AdminUI.ShouldBeNull();

        int daprComponentsBeforeFolders = builder.Resources.OfType<IDaprComponentResource>().Count();

        HexalithFoldersResources resources = builder.AddHexalithFolders(
            eventStore.StateStore,
            eventStore.PubSub,
            eventStore.EventStore,
            tenants,
            folders,
            foldersWorkers,
            foldersUi,
            DaprConfigPath);

        builder.Resources.OfType<IDaprComponentResource>().Count().ShouldBe(daprComponentsBeforeFolders);
        resources.StateStore.ShouldBeSameAs(eventStore.StateStore);
        resources.PubSub.ShouldBeSameAs(eventStore.PubSub);
    }

    /// <summary>
    /// Builds the gateway-only platform composition the Folders AppHost uses (EventStore command gateway with
    /// no admin server / admin UI, plus the Tenants domain-module sidecar) over fake project resources.
    /// </summary>
    private static FoldersTopology BuildGatewayOnlyComposition(IDistributedApplicationBuilder builder)
    {
        IResourceBuilder<ProjectResource> eventStoreProject = builder.AddProject("eventstore-test", RepositoryPath("src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj"));
        IResourceBuilder<ProjectResource> tenants = builder.AddProject("tenants-test", RepositoryPath("src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj"));
        IResourceBuilder<ProjectResource> folders = builder.AddProject("folders-test", RepositoryPath("src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj"));
        IResourceBuilder<ProjectResource> foldersWorkers = builder.AddProject("folders-workers-test", RepositoryPath("src/Hexalith.Folders.Workers/Hexalith.Folders.Workers.csproj"));
        IResourceBuilder<ProjectResource> foldersUi = builder.AddProject("folders-ui-test", RepositoryPath("src/Hexalith.Folders.UI/Hexalith.Folders.UI.csproj"));

        // Gateway-only EventStore composition (adminServer/adminUI null) — mirrors the AppHost. With null
        // component paths the helper generates the in-code statestore/pubsub the assertions rely on.
        HexalithEventStoreResources eventStore = builder.AddHexalithEventStore(
            eventStoreProject,
            adminServer: null,
            adminUI: null,
            eventStoreDaprConfigPath: DaprConfigPath,
            adminServerDaprConfigPath: null,
            resiliencyConfigPath: null,
            stateStoreComponentPath: null,
            pubSubComponentPath: null);

        // Tenants sidecar shares the EventStore state store + pub/sub, exactly as AddHexalithTenantsServer does.
        _ = tenants.AddEventStoreDomainModule(eventStore, FoldersAspireModule.TenantsAppId, DaprConfigPath);

        return new FoldersTopology(eventStore, tenants, folders, foldersWorkers, foldersUi);
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

    private sealed record FoldersTopology(
        HexalithEventStoreResources EventStore,
        IResourceBuilder<ProjectResource> Tenants,
        IResourceBuilder<ProjectResource> Folders,
        IResourceBuilder<ProjectResource> FoldersWorkers,
        IResourceBuilder<ProjectResource> FoldersUi);
}
