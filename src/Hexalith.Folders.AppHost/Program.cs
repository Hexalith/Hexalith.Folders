using Hexalith.EventStore.Aspire;
using Hexalith.Folders.Aspire;
using Hexalith.Memories.Aspire;
using Hexalith.Tenants.Aspire;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// Dapr configuration + component paths resolved relative to the AppHost dir (fail-fast if missing).
string accessControlConfigPath = ResolveDaprConfigPath(builder.AppHostDirectory, "accesscontrol.yaml");
string resiliencyConfigPath = ResolveDaprConfigPath(builder.AppHostDirectory, "resiliency.yaml");
string stateStoreComponentPath = ResolveDaprConfigPath(builder.AppHostDirectory, "statestore.yaml");
string pubSubComponentPath = ResolveDaprConfigPath(builder.AppHostDirectory, "pubsub.yaml");

IResourceBuilder<KeycloakResource>? keycloak = null;
ReferenceExpression? realmUrl = null;
if (!string.Equals(builder.Configuration["EnableKeycloak"], "false", StringComparison.OrdinalIgnoreCase))
{
    keycloak = builder.AddKeycloak("keycloak", 8180)
        .WithRealmImport("./KeycloakRealms");
    EndpointReference keycloakEndpoint = keycloak.GetEndpoint("http");
    realmUrl = ReferenceExpression.Create($"{keycloakEndpoint}/realms/hexalith");
}

// EventStore command gateway, composed gateway-only (no admin server / admin UI) via the platform Aspire
// helper. The helper owns the eventstore sidecar plus the shared statestore/pubsub Dapr components, sourced
// from the checked-in DaprComponents YAML so no component is created in Folders code (Epic 9).
IResourceBuilder<ProjectResource> eventStoreProject = builder.AddHexalithEventStoreGatewayProject(FoldersAspireModule.EventStoreAppId);
HexalithEventStoreResources eventStoreResources = builder.AddHexalithEventStore(
    eventStoreProject,
    adminServer: null,
    adminUI: null,
    eventStoreDaprConfigPath: accessControlConfigPath,
    adminServerDaprConfigPath: null,
    resiliencyConfigPath: resiliencyConfigPath,
    stateStoreComponentPath: stateStoreComponentPath,
    pubSubComponentPath: pubSubComponentPath);
IResourceBuilder<ProjectResource> eventStore = eventStoreResources.EventStore;

// Tenants domain service on the shared EventStore platform (appId "tenants"); its sidecar shares the
// EventStore state store + pub/sub via the platform helper.
IResourceBuilder<ProjectResource> tenants = builder.AddHexalithTenantsServer(
    eventStoreResources,
    accessControlConfigPath,
    FoldersAspireModule.TenantsAppId);

IResourceBuilder<ProjectResource> folders = builder.AddProject<Projects.Hexalith_Folders_Server>(FoldersAspireModule.FoldersAppId);
IResourceBuilder<ProjectResource> foldersWorkers = builder.AddProject<Projects.Hexalith_Folders_Workers>(FoldersAspireModule.FoldersWorkersAppId);
IResourceBuilder<ProjectResource> foldersUi = builder.AddProject<Projects.Hexalith_Folders_UI>(FoldersAspireModule.FoldersUiAppId);

// Wire the Folders services onto the shared topology, reusing the platform statestore/pubsub components.
_ = builder.AddHexalithFolders(
    eventStoreResources.StateStore,
    eventStoreResources.PubSub,
    eventStore,
    tenants,
    folders,
    foldersWorkers,
    foldersUi,
    accessControlConfigPath);

// Memories search-index server (Story 9.2): hosted standalone on the shared EventStore topology, reusing
// the same statestore/pubsub Dapr components. The reusable Memories hosting recipe (memories-vectors Redis
// Stack store + memories-graphs FalkorDB store + secret store + conversation/LLM component + the memories
// project and its Dapr sidecar) lives in Hexalith.Memories.Aspire; this AppHost owns only the component
// YAML paths. Source->index routing (9.3) and the worker-side producer / folders->memories invoke
// authorization (Epic 10) are intentionally deferred, so the memories resource is left unconsumed here and
// is not JWT-wired (parity with the canonical Tenants AppHost's Memories composition).
string memoriesSecretStorePath = ResolveDaprConfigPath(builder.AppHostDirectory, "secretstore.memories.yaml");
string memoriesLlmConfigPath = ResolveDaprConfigPath(builder.AppHostDirectory, "llm.memories.yaml");

_ = builder.AddHexalithMemoriesSearchIndexServer(
    eventStoreResources.StateStore,
    eventStoreResources.PubSub,
    memoriesSecretStorePath,
    memoriesLlmConfigPath,
    serverName: FoldersAspireModule.MemoriesAppId);

if (keycloak is not null && realmUrl is not null)
{
    ConfigureJwt(eventStore, keycloak, realmUrl);
    ConfigureJwt(tenants, keycloak, realmUrl);
    ConfigureJwt(folders, keycloak, realmUrl);
    ConfigureJwt(foldersWorkers, keycloak, realmUrl);

    _ = foldersUi
        .WithReference(keycloak)
        .WaitFor(keycloak)
        .WithEnvironment("Folders__Authentication__Authority", realmUrl)
        .WithEnvironment("Folders__Authentication__ClientId", "hexalith-folders");
}

builder.Build().Run();

static void ConfigureJwt(
    IResourceBuilder<ProjectResource> resource,
    IResourceBuilder<KeycloakResource> keycloak,
    ReferenceExpression realmUrl)
{
    _ = resource
        .WithReference(keycloak)
        .WaitFor(keycloak)
        .WithEnvironment("Authentication__JwtBearer__Authority", realmUrl)
        .WithEnvironment("Authentication__JwtBearer__Issuer", realmUrl)
        .WithEnvironment("Authentication__JwtBearer__Audience", "hexalith-eventstore")
        .WithEnvironment("Authentication__JwtBearer__RequireHttpsMetadata", "false")
        .WithEnvironment("Authentication__JwtBearer__SigningKey", "");
}

static string ResolveDaprConfigPath(string appHostDirectory, string fileName)
{
    string configPath = Path.Combine(appHostDirectory, "DaprComponents", fileName);
    if (File.Exists(configPath))
    {
        return configPath;
    }

    configPath = Path.Combine(Directory.GetCurrentDirectory(), "DaprComponents", fileName);
    if (File.Exists(configPath))
    {
        return configPath;
    }

    throw new FileNotFoundException(
        "DAPR access control configuration not found. "
        + $"Ensure {fileName} exists in the DaprComponents directory.",
        configPath);
}
