using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Dapr;

using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Server;
using Hexalith.Folders.Workers;
using Hexalith.Tenants.Client.Configuration;
using Hexalith.Tenants.Client.Handlers;
using Hexalith.Tenants.Client.Subscription;
using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.Contracts.Events;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using Shouldly;
using Xunit;

using ServerTenantEventHandler = Hexalith.Folders.Server.FoldersTenantEventHandler;
using WorkerTenantEventHandler = Hexalith.Folders.Workers.Tenants.TenantEventHandlers.FoldersTenantEventHandler;

namespace Hexalith.Folders.Workers.Tests;

public sealed class WorkersTenantEventTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void WorkersModuleShouldExposeStableTenantSubscriptionMetadata()
    {
        FoldersWorkersModule.Name.ShouldBe("Hexalith.Folders.Workers");
        FoldersWorkersModule.AppId.ShouldBe("folders-workers");
        FoldersWorkersModule.TenantEventsRoute.ShouldBe("/tenants/events");
        FoldersWorkersModule.TenantEventsPubSubName.ShouldBe("pubsub");
        FoldersWorkersModule.TenantEventsTopicName.ShouldBe("system.tenants.events");
    }

    [Fact]
    public void AddFoldersTenantEventWorkersShouldRegisterSupportedTenantHandlersAndOptions()
    {
        ServiceCollection services = CreateServiceCollection();
        services.AddFoldersTenantEventWorkers();

        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<FolderTenantAccessHandler>().ShouldNotBeNull();
        provider.GetRequiredService<IFolderTenantAccessProjectionStore>().ShouldNotBeNull();
        provider.GetRequiredService<IOptions<FoldersTenantEventOptions>>().Value.ProjectionWriter.ShouldBe(FoldersTenantEventProjectionWriter.Workers);
        provider.GetRequiredService<IOptions<HexalithTenantsOptions>>().Value.PubSubName.ShouldBe(FoldersWorkersModule.TenantEventsPubSubName);
        provider.GetRequiredService<IOptions<HexalithTenantsOptions>>().Value.TopicName.ShouldBe(FoldersWorkersModule.TenantEventsTopicName);

        provider.GetServices<ITenantEventHandler<TenantCreated>>().OfType<WorkerTenantEventHandler>().ShouldHaveSingleItem();
        provider.GetServices<ITenantEventHandler<TenantUpdated>>().OfType<WorkerTenantEventHandler>().ShouldHaveSingleItem();
        provider.GetServices<ITenantEventHandler<TenantDisabled>>().OfType<WorkerTenantEventHandler>().ShouldHaveSingleItem();
        provider.GetServices<ITenantEventHandler<TenantEnabled>>().OfType<WorkerTenantEventHandler>().ShouldHaveSingleItem();
        provider.GetServices<ITenantEventHandler<UserAddedToTenant>>().OfType<WorkerTenantEventHandler>().ShouldHaveSingleItem();
        provider.GetServices<ITenantEventHandler<UserRemovedFromTenant>>().OfType<WorkerTenantEventHandler>().ShouldHaveSingleItem();
        provider.GetServices<ITenantEventHandler<UserRoleChanged>>().OfType<WorkerTenantEventHandler>().ShouldHaveSingleItem();
        provider.GetServices<ITenantEventHandler<TenantConfigurationSet>>().OfType<WorkerTenantEventHandler>().ShouldHaveSingleItem();
        provider.GetServices<ITenantEventHandler<TenantConfigurationRemoved>>().OfType<WorkerTenantEventHandler>().ShouldHaveSingleItem();
    }

    [Fact]
    public void MapFoldersTenantEventWorkerEndpointsShouldRegisterOnlyTrustedSubscriptionRoute()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddFoldersTenantEventWorkers();
        WebApplication app = builder.Build();

        app.UseCloudEvents();
        app.MapSubscribeHandler();
        app.MapFoldersTenantEventWorkerEndpoints();

        RouteEndpoint[] endpoints = ((IEndpointRouteBuilder)app)
            .DataSources
            .SelectMany(static source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();

        string[] routes = endpoints.Select(static endpoint => endpoint.RoutePattern.RawText ?? string.Empty).ToArray();
        routes.ShouldContain("/");
        routes.ShouldContain("/tenants/events");
        routes.ShouldContain("dapr/subscribe");
        routes.ShouldNotContain(route => route.Contains("raw", StringComparison.OrdinalIgnoreCase));

        RouteEndpoint tenantEndpoint = endpoints.Single(static endpoint => string.Equals(endpoint.RoutePattern.RawText, "/tenants/events", StringComparison.Ordinal));
        ITopicMetadata? topicMetadata = tenantEndpoint.Metadata.GetMetadata<ITopicMetadata>();
        topicMetadata.ShouldNotBeNull();
        topicMetadata.PubsubName.ShouldBe(FoldersWorkersModule.TenantEventsPubSubName);
        topicMetadata.Name.ShouldBe(FoldersWorkersModule.TenantEventsTopicName);
    }

    [Fact]
    public async Task WorkerHandlerShouldProjectLifecycleMembershipAndFoldersConfiguration()
    {
        InMemoryFolderTenantAccessProjectionStore store = new();
        WorkerTenantEventHandler subject = CreateWorkerHandler(store);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await subject.HandleAsync(new TenantCreated("tenant-a", "Tenant A", null, Now), Context(1), cancellationToken);
        await subject.HandleAsync(new UserAddedToTenant("tenant-a", "user-a", TenantRole.TenantReader), Context(2), cancellationToken);
        await subject.HandleAsync(new UserRoleChanged("tenant-a", "user-a", TenantRole.TenantReader, TenantRole.TenantOwner), Context(3), cancellationToken);
        await subject.HandleAsync(new TenantConfigurationSet("tenant-a", "billing.secret", "secret-value"), Context(4), cancellationToken);
        await subject.HandleAsync(new TenantConfigurationSet("tenant-a", "folders.create.enabled", "secret-value"), Context(5), cancellationToken);
        await subject.HandleAsync(new TenantConfigurationRemoved("tenant-a", "folders.create.enabled"), Context(6), cancellationToken);
        await subject.HandleAsync(new TenantDisabled("tenant-a", Now), Context(7), cancellationToken);

        FolderTenantAccessProjection? projection = await store.GetAsync("tenant-a", cancellationToken);

        projection.ShouldNotBeNull();
        projection.Enabled.ShouldBeFalse();
        projection.Principals["user-a"].Role.ShouldBe(nameof(TenantRole.TenantOwner));
        projection.ConfigurationKeys.ShouldNotContain("billing.secret");
        projection.ConfigurationKeys.ShouldNotContain("folders.create.enabled");
        projection.RemovedConfigurationKeys.ShouldContain("folders.create.enabled");
        projection.ProcessedMessages.Values.Select(static evidence => evidence.PayloadFingerprint).ShouldNotContain("secret-value");
    }

    [Fact]
    public async Task WorkerAndServerHandlersShouldProduceSameProjectionWhenEachOwnsWrites()
    {
        InMemoryFolderTenantAccessProjectionStore workerStore = new();
        InMemoryFolderTenantAccessProjectionStore serverStore = new();
        WorkerTenantEventHandler worker = CreateWorkerHandler(workerStore, FoldersTenantEventProjectionWriter.Workers);
        ServerTenantEventHandler server = CreateServerHandler(serverStore, FoldersTenantEventProjectionWriter.Server);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await ApplyScenarioAsync(worker, cancellationToken);
        await ApplyScenarioAsync(server, cancellationToken);

        FolderTenantAccessProjection? workerProjection = await workerStore.GetAsync("tenant-a", cancellationToken);
        FolderTenantAccessProjection? serverProjection = await serverStore.GetAsync("tenant-a", cancellationToken);

        workerProjection.ShouldNotBeNull();
        serverProjection.ShouldNotBeNull();
        workerProjection.Enabled.ShouldBe(serverProjection.Enabled);
        workerProjection.Watermark.ShouldBe(serverProjection.Watermark);
        workerProjection.Principals.Select(static item => (item.Key, item.Value.Role)).ShouldBe(serverProjection.Principals.Select(static item => (item.Key, item.Value.Role)));
        workerProjection.ConfigurationKeys.ShouldBe(serverProjection.ConfigurationKeys);
        workerProjection.RemovedConfigurationKeys.ShouldBe(serverProjection.RemovedConfigurationKeys);
        workerProjection.ProcessedMessages.ShouldBe(serverProjection.ProcessedMessages);
    }

    [Fact]
    public async Task ProjectionWriterOptionShouldDisableTheNonOwningHostBeforeMutation()
    {
        InMemoryFolderTenantAccessProjectionStore serverOwnedStore = new();
        ServerTenantEventHandler serverWriter = CreateServerHandler(serverOwnedStore, FoldersTenantEventProjectionWriter.Server);
        WorkerTenantEventHandler disabledWorker = CreateWorkerHandler(serverOwnedStore, FoldersTenantEventProjectionWriter.Server);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await disabledWorker.HandleAsync(new TenantCreated("tenant-a", "Tenant A", null, Now), Context(1), cancellationToken);
        (await serverOwnedStore.GetAsync("tenant-a", cancellationToken)).ShouldBeNull();

        await serverWriter.HandleAsync(new TenantCreated("tenant-a", "Tenant A", null, Now), Context(1), cancellationToken);
        (await serverOwnedStore.GetAsync("tenant-a", cancellationToken)).ShouldNotBeNull();

        InMemoryFolderTenantAccessProjectionStore workerOwnedStore = new();
        ServerTenantEventHandler disabledServer = CreateServerHandler(workerOwnedStore, FoldersTenantEventProjectionWriter.Workers);
        WorkerTenantEventHandler workerWriter = CreateWorkerHandler(workerOwnedStore, FoldersTenantEventProjectionWriter.Workers);

        await disabledServer.HandleAsync(new TenantCreated("tenant-b", "Tenant B", null, Now), Context(1, "tenant-b"), cancellationToken);
        (await workerOwnedStore.GetAsync("tenant-b", cancellationToken)).ShouldBeNull();

        await workerWriter.HandleAsync(new TenantCreated("tenant-b", "Tenant B", null, Now), Context(1, "tenant-b"), cancellationToken);
        (await workerOwnedStore.GetAsync("tenant-b", cancellationToken)).ShouldNotBeNull();
    }

    [Fact]
    public async Task TenantEventProcessorShouldSkipUnsupportedEventTypeWithoutMutatingProjection()
    {
        ServiceCollection services = CreateServiceCollection();
        services.AddFoldersTenantEventWorkers();
        using ServiceProvider provider = services.BuildServiceProvider();
        TenantEventProcessor processor = provider.GetRequiredService<TenantEventProcessor>();

        TenantEventProcessingResult result = await processor.ProcessAsync(
            new TenantEventEnvelope(
                "01J00000000000000000000999",
                "tenant-a",
                "system",
                "Hexalith.Tenants.Contracts.Events.UnrelatedEvent",
                1,
                Now,
                "corr-999",
                "json",
                "{}"u8.ToArray()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(TenantEventProcessingResult.SkippedUnknownEventType);
        IFolderTenantAccessProjectionStore store = provider.GetRequiredService<IFolderTenantAccessProjectionStore>();
        (await store.GetAsync("tenant-a", TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public void InvalidProjectionWriterOptionsShouldFailValidation()
    {
        FoldersTenantEventOptionsValidator validator = new();

        ValidateOptionsResult result = validator.Validate(null, new FoldersTenantEventOptions
        {
            ProjectionWriter = (FoldersTenantEventProjectionWriter)42,
        });

        result.Failed.ShouldBeTrue();
        result.Failures.Single().ShouldContain("ProjectionWriter");
    }

    [Fact]
    public async Task TenantSubscriptionEndpointShouldProcessKnownEventsEndToEnd()
    {
        WorkerTestHost host = await StartWorkerHostAsync().ConfigureAwait(true);
        try
        {
            CancellationToken cancellationToken = TestContext.Current.CancellationToken;

            HttpResponseMessage created = await host.Client
                .PostAsJsonAsync(
                    FoldersWorkersModule.TenantEventsRoute,
                    Envelope("01J00000000000000000001001", 1, new TenantCreated("tenant-a", "Tenant A", null, Now)),
                    cancellationToken)
                .ConfigureAwait(true);
            HttpResponseMessage added = await host.Client
                .PostAsJsonAsync(
                    FoldersWorkersModule.TenantEventsRoute,
                    Envelope("01J00000000000000000001002", 2, new UserAddedToTenant("tenant-a", "user-a", TenantRole.TenantOwner)),
                    cancellationToken)
                .ConfigureAwait(true);
            HttpResponseMessage configured = await host.Client
                .PostAsJsonAsync(
                    FoldersWorkersModule.TenantEventsRoute,
                    Envelope("01J00000000000000000001003", 3, new TenantConfigurationSet("tenant-a", "folders.audit.mode", "sentinel-secret-value")),
                    cancellationToken)
                .ConfigureAwait(true);

            created.StatusCode.ShouldBe(HttpStatusCode.OK);
            added.StatusCode.ShouldBe(HttpStatusCode.OK);
            configured.StatusCode.ShouldBe(HttpStatusCode.OK);

            FolderTenantAccessProjection? projection = await host.Store.GetAsync("tenant-a", cancellationToken).ConfigureAwait(true);

            projection.ShouldNotBeNull();
            projection.Enabled.ShouldBeTrue();
            projection.Principals["user-a"].Role.ShouldBe(nameof(TenantRole.TenantOwner));
            projection.ConfigurationKeys.ShouldContain("folders.audit.mode");
            projection.ProcessedMessages.Values
                .Select(static evidence => evidence.PayloadFingerprint)
                .ShouldNotContain("sentinel-secret-value");
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task TenantSubscriptionEndpointShouldRejectInvalidPayloadWithoutMutatingProjectionOrEchoingPayload()
    {
        WorkerTestHost host = await StartWorkerHostAsync().ConfigureAwait(true);
        try
        {
            TenantEventEnvelope envelope = new(
                "01J00000000000000000001010",
                "tenant-a",
                "system",
                typeof(TenantConfigurationSet).FullName!,
                1,
                Now,
                "corr-invalid",
                "json",
                Encoding.UTF8.GetBytes("{\"tenantId\":\"tenant-a\",\"key\":\"folders.audit.mode\",\"value\":\"sentinel-secret-value\""));

            HttpResponseMessage response = await host.Client
                .PostAsJsonAsync(FoldersWorkersModule.TenantEventsRoute, envelope, TestContext.Current.CancellationToken)
                .ConfigureAwait(true);
            string responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
            responseBody.ShouldNotContain("sentinel-secret-value");
            (await host.Store.GetAsync("tenant-a", TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeNull();
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task TenantSubscriptionEndpointShouldAcknowledgeUnknownEventTypesWithoutMutatingProjection()
    {
        WorkerTestHost host = await StartWorkerHostAsync().ConfigureAwait(true);
        try
        {
            HttpResponseMessage response = await host.Client
                .PostAsJsonAsync(
                    FoldersWorkersModule.TenantEventsRoute,
                    new TenantEventEnvelope(
                        "01J00000000000000000001020",
                        "tenant-a",
                        "system",
                        "Hexalith.Tenants.Contracts.Events.UnrelatedEvent",
                        1,
                        Now,
                        "corr-unknown",
                        "json",
                        "{}"u8.ToArray()),
                    TestContext.Current.CancellationToken)
                .ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await host.Store.GetAsync("tenant-a", TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeNull();
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    private static async Task ApplyScenarioAsync(WorkerTenantEventHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new TenantCreated("tenant-a", "Tenant A", null, Now), Context(1), cancellationToken).ConfigureAwait(false);
        await handler.HandleAsync(new UserAddedToTenant("tenant-a", "user-a", TenantRole.TenantReader), Context(2), cancellationToken).ConfigureAwait(false);
        await handler.HandleAsync(new UserRoleChanged("tenant-a", "user-a", TenantRole.TenantReader, TenantRole.TenantOwner), Context(3), cancellationToken).ConfigureAwait(false);
        await handler.HandleAsync(new TenantConfigurationSet("tenant-a", "folders.create.enabled", "true"), Context(4), cancellationToken).ConfigureAwait(false);
        await handler.HandleAsync(new TenantConfigurationRemoved("tenant-a", "folders.create.enabled"), Context(5), cancellationToken).ConfigureAwait(false);
    }

    private static async Task ApplyScenarioAsync(ServerTenantEventHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new TenantCreated("tenant-a", "Tenant A", null, Now), Context(1), cancellationToken).ConfigureAwait(false);
        await handler.HandleAsync(new UserAddedToTenant("tenant-a", "user-a", TenantRole.TenantReader), Context(2), cancellationToken).ConfigureAwait(false);
        await handler.HandleAsync(new UserRoleChanged("tenant-a", "user-a", TenantRole.TenantReader, TenantRole.TenantOwner), Context(3), cancellationToken).ConfigureAwait(false);
        await handler.HandleAsync(new TenantConfigurationSet("tenant-a", "folders.create.enabled", "true"), Context(4), cancellationToken).ConfigureAwait(false);
        await handler.HandleAsync(new TenantConfigurationRemoved("tenant-a", "folders.create.enabled"), Context(5), cancellationToken).ConfigureAwait(false);
    }

    private static WorkerTenantEventHandler CreateWorkerHandler(
        IFolderTenantAccessProjectionStore store,
        FoldersTenantEventProjectionWriter writer = FoldersTenantEventProjectionWriter.Workers)
        => new(
            new FolderTenantAccessHandler(store, new FixedUtcClock(Now.AddMinutes(1)), new TenantAccessOptions()),
            new FoldersTenantAccessEventMapper(),
            Options.Create(new FoldersTenantEventOptions { ProjectionWriter = writer }));

    private static ServerTenantEventHandler CreateServerHandler(
        IFolderTenantAccessProjectionStore store,
        FoldersTenantEventProjectionWriter writer)
        => new(
            new FolderTenantAccessHandler(store, new FixedUtcClock(Now.AddMinutes(1)), new TenantAccessOptions()),
            new FoldersTenantAccessEventMapper(),
            Options.Create(new FoldersTenantEventOptions { ProjectionWriter = writer }));

    private static TenantEventContext Context(long sequence, string tenantId = "tenant-a")
        => new(tenantId, $"01J00000000000000000000{sequence:D3}", sequence, Now, $"corr-{sequence}");

    private static ServiceCollection CreateServiceCollection()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        return services;
    }

    private static TenantEventEnvelope Envelope<TEvent>(string messageId, long sequenceNumber, TEvent @event)
        => new(
            messageId,
            "tenant-a",
            "system",
            typeof(TEvent).FullName!,
            sequenceNumber,
            Now,
            $"corr-{sequenceNumber}",
            "json",
            JsonSerializer.SerializeToUtf8Bytes(@event));

    private static async Task<WorkerTestHost> StartWorkerHostAsync()
    {
        InMemoryFolderTenantAccessProjectionStore store = new();
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
        });
        builder.Configuration["urls"] = "http://127.0.0.1:0";
        builder.Services.AddFoldersTenantEventWorkers();
        builder.Services.RemoveAll<IUtcClock>();
        builder.Services.AddSingleton<IUtcClock>(new FixedUtcClock(Now.AddMinutes(1)));
        builder.Services.RemoveAll<IFolderTenantAccessProjectionStore>();
        builder.Services.AddSingleton<IFolderTenantAccessProjectionStore>(store);

        WebApplication app = builder.Build();
        app.UseCloudEvents();
        app.MapSubscribeHandler();
        app.MapFoldersTenantEventWorkerEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        return new WorkerTestHost(
            app,
            new HttpClient { BaseAddress = new Uri(app.Urls.First()) },
            store);
    }

    private sealed record WorkerTestHost(
        WebApplication App,
        HttpClient Client,
        InMemoryFolderTenantAccessProjectionStore Store) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await App.DisposeAsync().ConfigureAwait(true);
        }
    }
}
