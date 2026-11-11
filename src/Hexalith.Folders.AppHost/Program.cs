using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Hexalith.EventStore.Aspire;
using Hexalith.Folders.Aspire;
using Hexalith.Memories.Aspire;
using Hexalith.Tenants.Aspire;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// Dapr configuration + component paths resolved relative to the AppHost dir (fail-fast if missing).
string accessControlConfigPath = ResolveDaprConfigPath(builder.AppHostDirectory, "accesscontrol.yaml");
string resiliencyConfigPath = ResolveDaprConfigPath(builder.AppHostDirectory, "resiliency.yaml");
string stateStoreComponentPath = ResolveDaprConfigPath(builder.AppHostDirectory, "statestore.yaml");
string pubSubComponentPath = ResolveDaprConfigPath(builder.AppHostDirectory, "pubsub.yaml");

// Shared local security resource used by HTTP services during Aspire orchestration.
HexalithEventStoreSecurityResources? security = builder.AddHexalithEventStoreSecurity();

// Folders-owned EventStore command host: platform gateway-only sidecar wiring, plus Folders trusted
// idempotency adapters registered in Hexalith.Folders.EventStore. Keep AddHexalithEventStore so the
// helper still owns the eventstore sidecar and the shared statestore/pubsub Dapr components.
IResourceBuilder<ProjectResource> eventStoreProject = builder.AddProject<Projects.Hexalith_Folders_EventStore>(FoldersAspireModule.EventStoreAppId);
HexalithEventStoreResources eventStoreResources = builder.AddHexalithEventStore(
    eventStoreProject,
    adminServer: null,
    adminUI: null,
    eventStoreDaprConfigPath: accessControlConfigPath,
    adminServerDaprConfigPath: null,
    resiliencyConfigPath: resiliencyConfigPath,
    stateStoreComponentPath: stateStoreComponentPath,
    pubSubComponentPath: pubSubComponentPath,
    eventStoreDaprHttpPort: builder.Configuration.GetValue<int?>("Folders:EventStoreDaprHttpPort") ?? 3501);
IResourceBuilder<ProjectResource> eventStore = eventStoreResources.EventStore;

// Keep local admission identities stable across AppHost restarts. The generated
// secret uses only base64 alphabet characters and decodes to at least 32 bytes;
// Aspire persists it outside source control. Production uses the DaprSecret key-ring references in deploy/dapr/production/sidecar-config-bindings.yaml.
IResourceBuilder<ParameterResource> idempotencyDigestKey = builder.AddParameter(
    "folders-idempotency-digest-key",
    new GenerateParameterDefault
    {
        MinLength = 44,
        Lower = true,
        MinLower = 1,
        Numeric = true,
        MinNumeric = 1,
        Upper = true,
        MinUpper = 1,
        Special = false,
    },
    secret: true,
    persist: true);
_ = eventStore
    .WithEnvironment("EventStore__IdempotencyAdmission__Enabled", "true")
    .WithEnvironment("EventStore__IdempotencyAdmission__DigestKeySource", "Configuration")
    .WithEnvironment("EventStore__IdempotencyAdmission__ActiveDigestKeyVersion", "local1")
    .WithEnvironment("EventStore__IdempotencyAdmission__DigestKeys__local1", idempotencyDigestKey);

// The receipt is issued by EventStore after authenticating the human caller and
// verified by Folders alongside EventStore's workload assertion. Both services
// receive one persistent local key; production supplies its own secret.
IResourceBuilder<ParameterResource> humanReceiptSigningKey = builder.AddParameter(
    "folders-human-authority-receipt-key",
    new GenerateParameterDefault
    {
        MinLength = 44,
        Lower = true,
        MinLower = 1,
        Numeric = true,
        MinNumeric = 1,
        Upper = true,
        MinUpper = 1,
        Special = false,
    },
    secret: true,
    persist: true);
_ = eventStore.WithEnvironment("Authentication__HumanAuthorityReceipt__SigningKey", humanReceiptSigningKey);

// Story 10.3 (D1): the EventStore actor host publishes folder domain events to `{tenantId}.folders.events` by the
// D6 convention, but the folders-workers semantic-indexing subscriber listens on the single, tenant-agnostic
// `folders.events` topic (FoldersSemanticIndexingDefaults.DomainEventsTopicName). Redirect the `folders` domain's
// publish topic to that fixed topic so every managed tenant's folder events reach the worker on one subscription.
// Cross-tenant isolation is preserved in the worker by the bridge projection's per-tenant keys. The production
// EventStore deployment must carry the same override (deploy/dapr/production/sidecar-config-bindings.yaml).
_ = eventStore.WithFoldersDomainEventTopicOverride();

// Tenants domain service on the shared EventStore platform (appId "tenants"); its sidecar shares the
// EventStore state store + pub/sub via the platform helper.
IResourceBuilder<ProjectResource> tenants = builder.AddHexalithTenantsServer(
    eventStoreResources,
    accessControlConfigPath,
    FoldersAspireModule.TenantsAppId);
_ = tenants.WithGeneratedEventStoreAppChannelToken();

IResourceBuilder<ProjectResource> folders = builder.AddProject<Projects.Hexalith_Folders_Server>(FoldersAspireModule.FoldersAppId);
_ = folders.WithEnvironment("Authentication__HumanAuthorityReceipt__SigningKey", humanReceiptSigningKey);
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
_ = folders.WithGeneratedEventStoreAppChannelToken();
_ = foldersWorkers.WithGeneratedEventStoreAppChannelToken();

// Memories search-index server (Story 9.2): hosted standalone on the shared EventStore topology, reusing
// the same statestore/pubsub Dapr components. The reusable Memories hosting recipe (memories-vectors Redis
// Stack store + memories-graphs FalkorDB store + secret store + conversation/LLM component + the memories
// project and its Dapr sidecar) lives in Hexalith.Memories.Aspire; this AppHost owns only the component
// YAML paths. The worker-side producer / folders->memories invoke authorization stays deferred to Epic 10,
// and memories is not JWT-wired (parity with the canonical Tenants AppHost's Memories composition).
string memoriesSecretStorePath = ResolveDaprConfigPath(builder.AppHostDirectory, "secretstore.memories.yaml");
string memoriesLlmConfigPath = ResolveDaprConfigPath(builder.AppHostDirectory, "llm.memories.yaml");

IResourceBuilder<IDaprComponentResource> memoriesSecretStore = builder.AddDaprComponent(
    "memories-secretstore",
    "secretstores.local.file",
    new DaprComponentOptions { LocalPath = memoriesSecretStorePath });

HexalithMemoriesSearchIndexServerResources memories = builder.AddHexalithMemoriesSearchIndexServer(
    eventStoreResources.StateStore,
    eventStoreResources.PubSub,
    memoriesSecretStore,
    memoriesLlmConfigPath,
    serverName: FoldersAspireModule.MemoriesAppId);

// The independently observed catalog requires every participating receiver to select its actual
// Dapr configuration. Memories retains its existing application middleware and pub/sub contracts.
IDaprSidecarResource memoriesSidecar = memories.Server.Resource.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;
DaprSidecarOptionsAnnotation memoriesOptions = memoriesSidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single();
memoriesSidecar.Annotations.Remove(memoriesOptions);
memoriesSidecar.Annotations.Add(new DaprSidecarOptionsAnnotation(memoriesOptions.Options with { Config = accessControlConfigPath }));

// Story 9.3: route the Folders producer's CloudEvents (source "hexalith-folders", emitted by the Epic 10
// worker) into the curated folders-index partition, auto-provisioning that index tenant at startup so it is
// Active before the first event arrives. Dormant until the Epic 10 producer ships. Mirrors the canonical
// Tenants AppHost's hexalith-tenants -> tenants-index routing chain.
_ = memories.Server.WithFoldersMemoriesSourceRouting();

if (security is not null)
{
    // The local realm's confidential EventStore workload client signs short-lived
    // domain-service assertions. Keycloak and EventStore share one generated secret.
    bool persistWorkloadSecret = bool.TryParse(
        builder.Configuration[HexalithEventStoreSecurityOptions.DefaultPersistentConfigurationKey]?.Trim(),
        out bool keycloakPersistent) && keycloakPersistent;
    IResourceBuilder<ParameterResource> workloadClientSecret = builder.AddParameter(
        "eventstore-workload-client-secret",
        new GenerateParameterDefault
        {
            MinLength = 32,
            Lower = true,
            MinLower = 1,
            Numeric = true,
            MinNumeric = 1,
            Upper = true,
            MinUpper = 1,
            Special = false,
        },
        secret: true,
        persist: persistWorkloadSecret);
    _ = security.Keycloak.WithEnvironment("HEXALITH_EVENTSTORE_WORKLOAD_CLIENT_SECRET", workloadClientSecret);
    _ = eventStore.WithEventStoreWorkloadClientCredentials("eventstore", workloadClientSecret);

    _ = eventStore.WithJwtBearerSecurity(security);
    _ = tenants.WithJwtBearerSecurity(security);
    _ = folders.WithJwtBearerSecurity(security);
    _ = foldersWorkers.WithJwtBearerSecurity(security);

    // These existing hosts both reach fresh folder/organization source heads. Each has
    // its own issuer credential and RSA resource key, and only metadata-read enrollment.
    foreach ((IResourceBuilder<ProjectResource> host, string name, string secretVariable) in new[]
    {
        (folders, "folders", "HEXALITH_FOLDERS_SOURCE_CLIENT_SECRET"),
        (foldersWorkers, "folders-workers", "HEXALITH_FOLDERS_WORKERS_SOURCE_CLIENT_SECRET"),
    })
    {
        var sourceSecret = builder.AddParameter(name + "-source-client-secret", new GenerateParameterDefault
        {
            MinLength = 32,
            Lower = true,
            MinLower = 1,
            Upper = true,
            MinUpper = 1,
            Numeric = true,
            MinNumeric = 1,
            Special = false,
        }, secret: true, persist: persistWorkloadSecret);
        using RSA sourceKey = RSA.Create(2048);
        string privateKey = Convert.ToBase64String(sourceKey.ExportPkcs8PrivateKey());
        var sourcePrivateKey = builder.AddParameter(name + "-source-proof-key", () => privateKey, secret: true);
        security.Keycloak.WithEnvironment(secretVariable, sourceSecret);
        host.WithEventStoreWorkloadClientCredentials(name, sourceSecret)
            .WithEnvironment("Authentication__WorkloadIssuer__Workload", name)
            .WithEnvironment("Authentication__BoundStreamRead__PrivateKey", sourcePrivateKey);
        eventStore.WithEnvironment("Authentication__BoundStreamRead__PublicKeys__" + name, Convert.ToBase64String(sourceKey.ExportSubjectPublicKeyInfo()))
            .WithEnvironment("Authentication__BoundStreamRead__AllowedDomains__" + name, "folders")
            .WithEnvironment("Authentication__BoundStreamRead__MetadataOnlyCallers__" + name, "true")
            .WithEventStoreTrustedEffectSubmitter(name);
    }

    _ = foldersUi
        .WithSecurityDependency(security)
        .WithEnvironment("Folders__Authentication__Authority", security.RealmUrl)
        .WithEnvironment("Folders__Authentication__ClientId", "hexalith-folders")
        .WithEnvironment("Folders__Authentication__RequireHttpsMetadata", security.RequireHttpsMetadata ? "true" : "false");
}

// The final caller/receiver cohort must consume owner-approved enrollment before catalog capture.
// Acceptance finishes its isolated identities and replicas before invoking the same Platform consumer.
Hexalith.Platform.Composition.PlatformWorkloadEnrollment.Disable(builder);
if (!builder.Configuration.GetValue<bool>("Folders:WorkloadEnrollment:DeferToCohort"))
    _ = await Hexalith.Platform.Composition.PlatformWorkloadEnrollment.ConfigureAsync(builder).ConfigureAwait(false);

// A retained local backend requires its owner's private custody profile. A disposable acceptance
// runner composes after selecting its isolated backend and final AppIds instead.
DevelopmentRouteCatalogComposition? catalog = null;
if (builder.Configuration["Folders:DevelopmentCatalog:CustodyDirectory"] is { Length: > 0 } custody)
{
    string retainedDigestKey = await idempotencyDigestKey.Resource.GetValueAsync(default).ConfigureAwait(false)
        ?? throw new InvalidOperationException("The retained local admission digest key is unavailable.");
    catalog = Hexalith.Folders.AppHost.FoldersDevelopmentCatalog.Compose(builder,
        new(custody, "folders-routing-authority", "folders-routing-authority-v1", "local1",
            new Dictionary<string, string> { ["local1"] = retainedDigestKey }, "local1"), stateStoreComponentPath);
}
try { builder.Build().Run(); }
finally { if (catalog is not null) await catalog.DisposeAsync().ConfigureAwait(false); }

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
