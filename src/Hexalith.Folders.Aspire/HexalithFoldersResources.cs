using Aspire.Hosting.ApplicationModel;

using CommunityToolkit.Aspire.Hosting.Dapr;

namespace Hexalith.Folders.Aspire;

public sealed record HexalithFoldersResources(
    IResourceBuilder<IDaprComponentResource> StateStore,
    IResourceBuilder<IDaprComponentResource> PubSub,
    IResourceBuilder<ProjectResource> EventStore,
    IResourceBuilder<ProjectResource> Tenants,
    IResourceBuilder<ProjectResource> Folders,
    IResourceBuilder<ProjectResource> FoldersWorkers,
    IResourceBuilder<ProjectResource> FoldersUi);
