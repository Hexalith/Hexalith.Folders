using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using CommunityToolkit.Aspire.Hosting.Dapr;

namespace Hexalith.Folders.Aspire;

public static class FoldersAspireModule
{
    public const string EventStoreAppId = "eventstore";

    public const string TenantsAppId = "tenants";

    public const string FoldersAppId = "folders";

    public const string FoldersWorkersAppId = "folders-workers";

    public const string FoldersUiAppId = "folders-ui";

    public const string StateStoreComponentName = "statestore";

    public const string PubSubComponentName = "pubsub";

    public const string MemoriesAppId = "memories";

    /// <summary>
    /// CloudEvents <c>source</c> the Epic 10 Folders worker-side producer stamps on its
    /// <c>SearchIndexEntryChanged</c> events. It is the dictionary-key segment of the Memories router's
    /// <c>SourceToTenantMap</c> routing entry (no in-repo CloudEvents-source constant exists yet, so this is its
    /// canonical home). The Tenants analog is <c>hexalith-tenants</c>.
    /// </summary>
    public const string MemoriesSourceId = "hexalith-folders";

    /// <summary>
    /// Curated Memories index tenant the <see cref="MemoriesSourceId"/>-sourced events route into. It is the
    /// value of the Memories router's <c>SourceToTenantMap</c> routing entry. The Tenants analog is
    /// <c>tenants-index</c>.
    /// </summary>
    public const string MemoriesIndexTenant = "folders-index";

    /// <summary>
    /// The single, tenant-agnostic Dapr topic the EventStore actor host publishes managed-tenant folder domain
    /// events to (via <see cref="EventStorePublisherFolderTopicOverrideKey"/>), and the topic the Folders worker
    /// semantic-indexing subscriber (<c>/folders/events</c>) listens on. Must stay in lockstep with the worker's
    /// <c>FoldersSemanticIndexingDefaults.DomainEventsTopicName</c> (a Workers-side test pins this equality).
    /// </summary>
    public const string FolderDomainEventsTopic = "folders.events";

    /// <summary>
    /// The EventStore publisher topic-override configuration key (double-underscore env form of
    /// <c>EventStore:Publisher:TopicOverrides:folders</c>) that redirects the <c>folders</c> domain's per-tenant
    /// <c>{tenantId}.folders.events</c> publish topic to the fixed <see cref="FolderDomainEventsTopic"/> so every
    /// managed tenant's folder events reach the worker on one subscription.
    /// </summary>
    public const string EventStorePublisherFolderTopicOverrideKey = "EventStore__Publisher__TopicOverrides__folders";

    /// <summary>
    /// Configures source-&gt;index routing on the standalone Memories search-index server so the Folders
    /// producer's CloudEvents (source <see cref="MemoriesSourceId"/>, emitted by the Epic 10 worker) are routed
    /// into the curated <see cref="MemoriesIndexTenant"/> partition, and that index tenant is auto-provisioned at
    /// startup so it is <c>Active</c> before the first event arrives.
    /// </summary>
    /// <remarks>
    /// Sets exactly two environment variables on the Memories server resource, mirroring the canonical Tenants
    /// AppHost (<c>Hexalith.Tenants.AppHost/Program.cs</c>, which wires <c>hexalith-tenants → tenants-index</c>):
    /// <list type="bullet">
    /// <item><c>EventStoreIntegration__Routing__SourceToTenantMap__hexalith-folders = folders-index</c> — the
    /// router's longest-prefix, case-insensitive source-&gt;tenant map entry.</item>
    /// <item><c>EventStoreIntegration__Routing__AutoProvisionRoutedTenants = true</c> — makes the Memories
    /// <c>RoutedTenantProvisioningStartupService</c> provision <see cref="MemoriesIndexTenant"/> at startup and
    /// makes <c>EventStoreRoutingConfigValidator</c> defer its fail-fast "all routed tenants must exist" check so
    /// the server boots cleanly even though no producer exists yet.</item>
    /// </list>
    /// The routing is dormant until the Epic 10 worker-side producer ships; until then no event flows, but the
    /// contract is in place. The two double-underscore keys map the appsettings path
    /// <c>EventStoreIntegration:Routing:...</c> to environment variables (dictionary keys are appended after the
    /// section path; the source id is the dictionary-key segment, not colon-encoded).
    /// </remarks>
    /// <param name="memoriesServer">The Memories search-index server project resource builder
    /// (<c>HexalithMemoriesSearchIndexServerResources.Server</c>).</param>
    /// <returns>The same resource builder for chaining.</returns>
    public static IResourceBuilder<ProjectResource> WithFoldersMemoriesSourceRouting(
        this IResourceBuilder<ProjectResource> memoriesServer)
    {
        ArgumentNullException.ThrowIfNull(memoriesServer);

        return memoriesServer
            .WithEnvironment($"EventStoreIntegration__Routing__SourceToTenantMap__{MemoriesSourceId}", MemoriesIndexTenant)
            .WithEnvironment("EventStoreIntegration__Routing__AutoProvisionRoutedTenants", "true");
    }

    /// <summary>
    /// Redirects the EventStore actor host's <c>folders</c>-domain publish topic to the single, tenant-agnostic
    /// <see cref="FolderDomainEventsTopic"/> so every managed tenant's folder domain events
    /// (<c>{tenantId}.folders.events</c> by the D6 convention) reach the Folders worker semantic-indexing
    /// subscriber on one fixed <c>/folders/events</c> subscription.
    /// </summary>
    /// <remarks>
    /// Sets exactly the <see cref="EventStorePublisherFolderTopicOverrideKey"/> environment variable
    /// (<c>EventStore:Publisher:TopicOverrides:folders = folders.events</c>) on the EventStore resource;
    /// <c>EventPublisherOptions.GetPubSubTopic</c> honours it with no EventStore SDK change. Cross-tenant isolation
    /// is preserved in the worker by the bridge projection's per-tenant keys, not by per-topic separation. The
    /// production EventStore deployment must carry the same override (recorded in
    /// <c>deploy/dapr/production/sidecar-config-bindings.yaml</c> and
    /// <c>docs/operations/container-images-and-dapr-app-ids.md</c>; pinned by deployment conformance tests).
    /// </remarks>
    /// <param name="eventStore">The EventStore actor-host project resource builder (the folder-events publisher).</param>
    /// <returns>The same resource builder for chaining.</returns>
    public static IResourceBuilder<ProjectResource> WithFoldersDomainEventTopicOverride(
        this IResourceBuilder<ProjectResource> eventStore)
    {
        ArgumentNullException.ThrowIfNull(eventStore);

        return eventStore.WithEnvironment(EventStorePublisherFolderTopicOverrideKey, FolderDomainEventsTopic);
    }

    /// <summary>
    /// Wires the Folders-owned services (<c>folders</c>, <c>folders-workers</c>, <c>folders-ui</c>) onto the
    /// shared Hexalith platform topology.
    /// </summary>
    /// <remarks>
    /// The EventStore command gateway and the Tenants service — and the shared <c>statestore</c>/<c>pubsub</c>
    /// Dapr components — are composed upstream by the platform Aspire helpers
    /// (<c>AddHexalithEventStore</c> gateway-only + <c>AddHexalithTenantsServer</c>). This helper no longer
    /// creates any Dapr component in code (Epic 9): it reuses the platform-provided <paramref name="stateStore"/>
    /// and <paramref name="pubSub"/> components and attaches only the three Folders sidecars plus their
    /// references — <c>folders</c>/<c>folders-workers</c> invoke and wait for the EventStore gateway and Tenants;
    /// <c>folders-ui</c> invokes and waits for <c>folders</c>.
    /// </remarks>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="stateStore">The shared Dapr state-store component from the platform EventStore composition.</param>
    /// <param name="pubSub">The shared Dapr pub/sub component from the platform EventStore composition.</param>
    /// <param name="eventStore">The EventStore command-gateway project the Folders backends invoke and wait for.</param>
    /// <param name="tenants">The Tenants service project the Folders backends reference and wait for.</param>
    /// <param name="folders">The Folders REST server project.</param>
    /// <param name="foldersWorkers">The Folders workers project.</param>
    /// <param name="foldersUi">The Folders read-only operations console UI project.</param>
    /// <param name="daprConfigPath">Optional Dapr access-control configuration path applied to each Folders sidecar.</param>
    /// <returns>The composed Folders topology resources (shared components + the five platform/Folders projects).</returns>
    public static HexalithFoldersResources AddHexalithFolders(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<IDaprComponentResource> stateStore,
        IResourceBuilder<IDaprComponentResource> pubSub,
        IResourceBuilder<ProjectResource> eventStore,
        IResourceBuilder<ProjectResource> tenants,
        IResourceBuilder<ProjectResource> folders,
        IResourceBuilder<ProjectResource> foldersWorkers,
        IResourceBuilder<ProjectResource> foldersUi,
        string? daprConfigPath = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(stateStore);
        ArgumentNullException.ThrowIfNull(pubSub);
        ArgumentNullException.ThrowIfNull(eventStore);
        ArgumentNullException.ThrowIfNull(tenants);
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentNullException.ThrowIfNull(foldersWorkers);
        ArgumentNullException.ThrowIfNull(foldersUi);

        _ = folders
            .WithReference(eventStore)
            .WithReference(tenants)
            .WaitFor(eventStore)
            .WaitFor(tenants)
            .WithDaprSidecar(sidecar => sidecar
                .WithOptions(new DaprSidecarOptions
                {
                    AppId = FoldersAppId,
                    Config = daprConfigPath,
                })
                .WithReference(stateStore)
                .WithReference(pubSub));

        _ = foldersWorkers
            .WithReference(eventStore)
            .WithReference(tenants)
            .WaitFor(eventStore)
            .WaitFor(tenants)
            .WithDaprSidecar(sidecar => sidecar
                .WithOptions(new DaprSidecarOptions
                {
                    AppId = FoldersWorkersAppId,
                    Config = daprConfigPath,
                })
                .WithReference(stateStore)
                .WithReference(pubSub));

        EndpointReference foldersHttp = folders.GetEndpoint("http");

        _ = foldersUi
            .WithReference(folders)
            .WaitFor(folders)
            .WithEnvironment("Folders__Client__BaseAddress", ReferenceExpression.Create($"{foldersHttp}"))
            .WithExternalHttpEndpoints()
            .WithDaprSidecar(sidecar => sidecar
                .WithOptions(new DaprSidecarOptions
                {
                    AppId = FoldersUiAppId,
                    Config = daprConfigPath,
                })
                .WithReference(stateStore)
                .WithReference(pubSub));

        return new HexalithFoldersResources(stateStore, pubSub, eventStore, tenants, folders, foldersWorkers, foldersUi);
    }
}
