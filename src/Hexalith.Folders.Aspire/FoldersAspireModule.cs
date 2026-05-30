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
    /// Registers the shared Dapr state-store and pub/sub components used by every Folders sidecar.
    /// Extracted from <see cref="AddHexalithFolders"/> so structural tests can verify component
    /// registration without needing real <see cref="ProjectResource"/>s.
    /// </summary>
    public static (IResourceBuilder<IDaprComponentResource> StateStore, IResourceBuilder<IDaprComponentResource> PubSub)
        AddFoldersSharedDaprComponents(this IDistributedApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        IResourceBuilder<IDaprComponentResource> stateStore = builder
            .AddDaprComponent(StateStoreComponentName, "state.redis")
            .WithMetadata("actorStateStore", "true")
            .WithMetadata("redisHost", "localhost:6379")
            .WithMetadata("keyPrefix", "none");
        IResourceBuilder<IDaprComponentResource> pubSub = builder.AddDaprPubSub(PubSubComponentName);
        return (stateStore, pubSub);
    }

    public static HexalithFoldersResources AddHexalithFolders(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> eventStore,
        IResourceBuilder<ProjectResource> tenants,
        IResourceBuilder<ProjectResource> folders,
        IResourceBuilder<ProjectResource> foldersWorkers,
        IResourceBuilder<ProjectResource> foldersUi,
        string? daprConfigPath = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(eventStore);
        ArgumentNullException.ThrowIfNull(tenants);
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentNullException.ThrowIfNull(foldersWorkers);
        ArgumentNullException.ThrowIfNull(foldersUi);

        (IResourceBuilder<IDaprComponentResource> stateStore, IResourceBuilder<IDaprComponentResource> pubSub) = builder.AddFoldersSharedDaprComponents();

        _ = eventStore
            .WithDaprSidecar(sidecar => sidecar
                .WithOptions(new DaprSidecarOptions
                {
                    AppId = EventStoreAppId,
                    Config = daprConfigPath,
                })
                .WithReference(stateStore)
                .WithReference(pubSub));

        _ = tenants
            .WithDaprSidecar(sidecar => sidecar
                .WithOptions(new DaprSidecarOptions
                {
                    AppId = TenantsAppId,
                    Config = daprConfigPath,
                })
                .WithReference(stateStore)
                .WithReference(pubSub));

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

        _ = foldersUi
            .WithReference(folders)
            .WaitFor(folders)
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
