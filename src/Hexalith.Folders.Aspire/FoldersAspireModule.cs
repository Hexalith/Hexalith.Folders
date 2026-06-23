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
