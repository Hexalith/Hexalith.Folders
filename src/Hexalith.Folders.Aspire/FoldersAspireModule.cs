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

        IResourceBuilder<IDaprComponentResource> stateStore = builder
            .AddDaprComponent(StateStoreComponentName, "state.redis")
            .WithMetadata("actorStateStore", "true")
            .WithMetadata("redisHost", "localhost:6379")
            .WithMetadata("keyPrefix", "none");
        IResourceBuilder<IDaprComponentResource> pubSub = builder.AddDaprPubSub(PubSubComponentName);

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
            .WithExternalHttpEndpoints();

        return new HexalithFoldersResources(stateStore, pubSub, eventStore, tenants, folders, foldersWorkers, foldersUi);
    }
}
