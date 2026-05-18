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
}
