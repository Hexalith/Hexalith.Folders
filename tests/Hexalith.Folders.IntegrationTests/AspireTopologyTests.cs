using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.EventStore.Aspire;
using Hexalith.Folders.Aspire;
using Hexalith.Memories.Aspire;

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
        FoldersAspireModule.MemoriesAppId.ShouldBe("memories");
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
    public void AddHexalithMemoriesSearchIndexServerShouldRegisterMemoriesSidecarComponentsAndContainers()
    {
        // Story 9.2 / AC4-AC5: composing the platform Memories search-index helper over the shared
        // statestore/pubsub adds exactly the memories sidecar (AppId "memories"), the memories-secretstore /
        // memories-llm Dapr components, and the memories-vectors (Redis Stack) / memories-graphs (FalkorDB)
        // containers. The helper references the cross-repo memories project with SuppressBuild, so this
        // registration-only inspection composes the topology without building the Memories server.
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        FoldersTopology topology = BuildGatewayOnlyComposition(builder);

        string memoriesSecretStorePath = RepositoryPath("src/Hexalith.Folders.AppHost/DaprComponents/secretstore.memories.yaml");
        string memoriesLlmConfigPath = RepositoryPath("src/Hexalith.Folders.AppHost/DaprComponents/llm.memories.yaml");
        File.Exists(memoriesSecretStorePath).ShouldBeTrue($"Expected the checked-in Memories secret-store YAML at {memoriesSecretStorePath}.");
        File.Exists(memoriesLlmConfigPath).ShouldBeTrue($"Expected the checked-in Memories LLM YAML at {memoriesLlmConfigPath}.");

        HexalithMemoriesSearchIndexServerResources memories = builder.AddHexalithMemoriesSearchIndexServer(
            topology.EventStore.StateStore,
            topology.EventStore.PubSub,
            memoriesSecretStorePath,
            memoriesLlmConfigPath,
            serverName: FoldersAspireModule.MemoriesAppId);

        // The memories project carries a Dapr sidecar whose AppId is exactly "memories".
        string[] sidecarAppIds = [.. builder.Resources
            .OfType<ProjectResource>()
            .SelectMany(static resource => resource.Annotations.OfType<DaprSidecarAnnotation>())
            .SelectMany(static sidecar => sidecar.Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>())
            .Select(static options => options.Options.AppId)
            .Where(static appId => appId is not null)
            .Select(static appId => appId!)];
        sidecarAppIds.ShouldContain(FoldersAspireModule.MemoriesAppId);

        // The memories-secretstore / memories-llm Dapr components are registered by the helper.
        string[] componentNames = [.. builder.Resources.OfType<IDaprComponentResource>().Select(static c => c.Name)];
        componentNames.ShouldContain("memories-secretstore");
        componentNames.ShouldContain("memories-llm");
        memories.SecretStore.Resource.Name.ShouldBe("memories-secretstore");
        memories.Llm.Resource.Name.ShouldBe("memories-llm");

        // The memories-vectors (Redis Stack) / memories-graphs (FalkorDB) containers are registered by the helper.
        string[] containerNames = [.. builder.Resources.OfType<ContainerResource>().Select(static c => c.Name)];
        containerNames.ShouldContain("memories-vectors");
        containerNames.ShouldContain("memories-graphs");

        // AC4: the memories sidecar binds the platform-stable ports (HTTP 3502 / gRPC 50002) — distinct from the
        // EventStore platform's 3501 — so the helper's port contract is pinned, not assumed.
        DaprSidecarOptions memoriesSidecarOptions = builder.Resources
            .OfType<ProjectResource>()
            .SelectMany(static resource => resource.Annotations.OfType<DaprSidecarAnnotation>())
            .SelectMany(static sidecar => sidecar.Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>())
            .Select(static options => options.Options)
            .Single(options => string.Equals(options.AppId, FoldersAspireModule.MemoriesAppId, StringComparison.Ordinal));
        memoriesSidecarOptions.DaprHttpPort.ShouldBe(3502);
        memoriesSidecarOptions.DaprGrpcPort.ShouldBe(50002);

        // AC4: the helper-owned containers carry the expected upstream images (redis/redis-stack for the vector
        // store, falkordb/falkordb for the graph store).
        ContainerImage("memories-vectors", builder).ShouldBe("redis/redis-stack");
        ContainerImage("memories-graphs", builder).ShouldBe("falkordb/falkordb");
    }

    [Fact]
    public void AddHexalithMemoriesSearchIndexServerShouldReuseSharedStateStoreAndPubSubWithoutCreatingCopies()
    {
        // Story 9.2 / AC1: the Memories server reuses the SAME shared statestore/pubsub component instances the
        // platform EventStore composition created — it must create no Folders-local copies. Composing the helper
        // adds exactly the two memories-owned components (memories-secretstore / memories-llm) and leaves the
        // single shared statestore + pubsub instances untouched.
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        FoldersTopology topology = BuildGatewayOnlyComposition(builder);

        string memoriesSecretStorePath = RepositoryPath("src/Hexalith.Folders.AppHost/DaprComponents/secretstore.memories.yaml");
        string memoriesLlmConfigPath = RepositoryPath("src/Hexalith.Folders.AppHost/DaprComponents/llm.memories.yaml");

        int stateStoreBefore = CountComponentsNamed(builder, FoldersAspireModule.StateStoreComponentName);
        int pubSubBefore = CountComponentsNamed(builder, FoldersAspireModule.PubSubComponentName);
        int componentsBefore = builder.Resources.OfType<IDaprComponentResource>().Count();

        // The shared statestore/pubsub must already be single instances before Memories is composed.
        stateStoreBefore.ShouldBe(1);
        pubSubBefore.ShouldBe(1);

        HexalithMemoriesSearchIndexServerResources memories = builder.AddHexalithMemoriesSearchIndexServer(
            topology.EventStore.StateStore,
            topology.EventStore.PubSub,
            memoriesSecretStorePath,
            memoriesLlmConfigPath,
            serverName: FoldersAspireModule.MemoriesAppId);

        // No second statestore/pubsub component is created — the shared singletons are reused verbatim.
        CountComponentsNamed(builder, FoldersAspireModule.StateStoreComponentName).ShouldBe(stateStoreBefore);
        CountComponentsNamed(builder, FoldersAspireModule.PubSubComponentName).ShouldBe(pubSubBefore);

        // The only new Dapr components are the two memories-owned ones.
        string[] componentNames = [.. builder.Resources.OfType<IDaprComponentResource>().Select(static c => c.Name)];
        componentNames.Length.ShouldBe(componentsBefore + 2);
        componentNames.ShouldContain("memories-secretstore");
        componentNames.ShouldContain("memories-llm");
        memories.SecretStore.Resource.Name.ShouldBe("memories-secretstore");
        memories.Llm.Resource.Name.ShouldBe("memories-llm");
    }

    [Fact]
    public void ComposingMemoriesAlongsideFoldersShouldRemainAdditiveWithStandaloneMemoriesSidecar()
    {
        // Story 9.2 / AC1+AC4: hosting memories is purely additive. Composed alongside the full Folders topology
        // it adds the standalone "memories" sidecar without perturbing the five production sidecars and without
        // introducing any eventstore-admin resource (the gateway-only invariant is preserved). memories is hosted
        // standalone — it is the sixth sidecar, not a reference target of folders/folders-workers/folders-ui
        // (folders -> memories invoke wiring is deferred to Epic 10).
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

        string memoriesSecretStorePath = RepositoryPath("src/Hexalith.Folders.AppHost/DaprComponents/secretstore.memories.yaml");
        string memoriesLlmConfigPath = RepositoryPath("src/Hexalith.Folders.AppHost/DaprComponents/llm.memories.yaml");

        _ = builder.AddHexalithMemoriesSearchIndexServer(
            topology.EventStore.StateStore,
            topology.EventStore.PubSub,
            memoriesSecretStorePath,
            memoriesLlmConfigPath,
            serverName: FoldersAspireModule.MemoriesAppId);

        string[] sidecarAppIds = [.. builder.Resources
            .OfType<ProjectResource>()
            .SelectMany(static resource => resource.Annotations.OfType<DaprSidecarAnnotation>())
            .SelectMany(static sidecar => sidecar.Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>())
            .Select(static options => options.Options.AppId)
            .Where(static appId => appId is not null)
            .Select(static appId => appId!)
            .Order(StringComparer.Ordinal)];

        // The five production sidecars are preserved verbatim; memories is added as the sixth, standalone sidecar.
        sidecarAppIds.ShouldBe(
        [
            FoldersAspireModule.EventStoreAppId,
            FoldersAspireModule.FoldersAppId,
            FoldersAspireModule.FoldersUiAppId,
            FoldersAspireModule.FoldersWorkersAppId,
            FoldersAspireModule.MemoriesAppId,
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

    private static int CountComponentsNamed(IDistributedApplicationBuilder builder, string name)
        => builder.Resources.OfType<IDaprComponentResource>().Count(c => string.Equals(c.Name, name, StringComparison.Ordinal));

    private static string ContainerImage(string containerName, IDistributedApplicationBuilder builder)
    {
        ContainerResource container = builder.Resources
            .OfType<ContainerResource>()
            .Single(c => string.Equals(c.Name, containerName, StringComparison.Ordinal));
        return container.Annotations.OfType<ContainerImageAnnotation>().Single().Image;
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
