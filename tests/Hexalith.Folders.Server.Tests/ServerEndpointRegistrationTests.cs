using Dapr;

using Hexalith.Folders.Server;
using Hexalith.Folders.Server.Authorization;
using Hexalith.Folders.Testing;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class ServerEndpointRegistrationTests
{
    [Fact]
    public void AddFoldersServerShouldRegisterLayeredAuthorizationAccessorAsScoped()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();

        builder.Services.AddFoldersServer();

        ServiceDescriptor descriptor = builder.Services
            .Single(static descriptor => descriptor.ServiceType == typeof(ILayeredFolderAuthorizationResultAccessor));
        descriptor.ImplementationType.ShouldBe(typeof(ScopedLayeredFolderAuthorizationResultAccessor));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void HostShouldRegisterSdkSidecarChannelPolicyForDomainCallbacks()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
        });
        builder.Services.AddFoldersServerTestDefaults();
        builder.AddFoldersServerHost();
        using WebApplication app = builder.Build();
        using IServiceScope scope = app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IFolderRepository>()
            .ShouldBeOfType<ActorFoldedFolderRepository>();
        scope.ServiceProvider.GetRequiredService<IOrganizationAclRepository>()
            .ShouldBeOfType<ActorFoldedOrganizationProviderBindingRepository>();
        EventStoreFolderEffectivePermissionsReadModel folderView = scope.ServiceProvider
            .GetRequiredService<EventStoreFolderEffectivePermissionsReadModel>();
        scope.ServiceProvider.GetRequiredService<IEffectivePermissionsReadModel>()
            .ShouldBeSameAs(folderView);
        scope.ServiceProvider.GetRequiredService<IFolderAclReadModel>()
            .ShouldBeSameAs(folderView);

        AuthorizationPolicy? policy = app.Services.GetRequiredService<IOptions<AuthorizationOptions>>()
            .Value.GetPolicy(EventStoreWorkloadAuthenticationDefaults.SidecarChannelPolicy);
        policy.ShouldNotBeNull();
        policy.AuthenticationSchemes.ShouldContain(EventStoreWorkloadAuthenticationDefaults.SidecarChannelScheme);

        string processPolicyName = EventStoreWorkloadAuthenticationDefaults.OperationPolicy(
            EventStoreWorkloadAuthenticationDefaults.WorkloadScheme,
            EventStoreWorkloadOperations.DomainServiceProcess);
        AuthorizationPolicy? processPolicy = app.Services.GetRequiredService<IOptions<AuthorizationOptions>>()
            .Value.GetPolicy(processPolicyName);
        processPolicy.ShouldNotBeNull();
        processPolicy.AuthenticationSchemes.ShouldContain(EventStoreWorkloadAuthenticationDefaults.WorkloadScheme);

        app.UseFoldersServerPipeline();
        RouteEndpoint processRoute = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(static source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(endpoint => endpoint.RoutePattern.RawText == FoldersServerModule.ProcessRoute);
        processRoute.Metadata.OfType<IAuthorizeData>()
            .Any(authorize => authorize.Policy == processPolicyName).ShouldBeTrue();
    }

    [Fact]
    public void FoldersServerModuleShouldExposeStableDomainAndTenantRoutes()
    {
        FoldersServerModule.ProcessRoute.ShouldBe("/process");
        FoldersServerModule.ProjectRoute.ShouldBe("/project");
        FoldersServerModule.TenantEventsRoute.ShouldBe("/tenants/events");
        FoldersServerModule.TenantEventsPubSubName.ShouldBe("pubsub");
        FoldersServerModule.TenantEventsTopicName.ShouldBe("tenants.events");
        FoldersServerModule.Description.ShouldContain("server scaffold");
    }

    [Fact]
    public void MapFoldersServerEndpointsShouldRegisterDomainServiceAndTenantSubscriptionRoutes()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddFoldersServerTestDefaults();
        builder.Services.AddFoldersServer();
        builder.Services.AddInMemoryFolderRepository();
        WebApplication app = builder.Build();

        app.UseCloudEvents();
        app.MapSubscribeHandler();
        app.MapFoldersServerEndpoints();

        string[] routes = ((IEndpointRouteBuilder)app)
            .DataSources
            .SelectMany(static source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(static endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .ToArray();

        routes.ShouldContain("/");
        routes.ShouldContain("/process");
        routes.ShouldContain("/project");
        routes.ShouldContain("/api/v1/folders/{folderId}/archive");
        routes.ShouldContain("/api/v1/folders/{folderId}/workspaces/{workspaceId}/preparation");
        routes.ShouldContain("/api/v1/folders/{folderId}/workspaces/{workspaceId}/lock");
        routes.ShouldContain("/tenants/events");
        routes.ShouldContain("dapr/subscribe");
    }

    [Fact]
    public void TenantEventsRouteShouldCarryExpectedTopicMetadata()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddFoldersServerTestDefaults();
        builder.Services.AddFoldersServer();
        builder.Services.AddInMemoryFolderRepository();
        WebApplication app = builder.Build();

        app.UseCloudEvents();
        app.MapSubscribeHandler();
        app.MapFoldersServerEndpoints();

        RouteEndpoint? tenantEndpoint = ((IEndpointRouteBuilder)app)
            .DataSources
            .SelectMany(static source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .FirstOrDefault(static endpoint => string.Equals(endpoint.RoutePattern.RawText, "/tenants/events", StringComparison.Ordinal));

        tenantEndpoint.ShouldNotBeNull();
        ITopicMetadata? topicMetadata = tenantEndpoint.Metadata.GetMetadata<ITopicMetadata>();
        topicMetadata.ShouldNotBeNull();
        topicMetadata.PubsubName.ShouldBe(FoldersServerModule.TenantEventsPubSubName);
        topicMetadata.Name.ShouldBe(FoldersServerModule.TenantEventsTopicName);
    }

    [Fact]
    public async Task ProjectRouteShouldClassifyEmptyStreamsAndReplayOrganizationBaseline()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
        });
        builder.Configuration["urls"] = "http://127.0.0.1:0";
        builder.Services.AddFoldersServerTestDefaults();
        builder.Services.AddFoldersServer();
        builder.Services.AddInMemoryFolderRepository();
        WebApplication app = builder.Build();

        app.UseCloudEvents();
        app.MapSubscribeHandler();
        app.MapFoldersServerEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
            using StringContent body = new(
                System.Text.Json.JsonSerializer.Serialize(new ProjectionRequest("tenant-a", "folders", "folder-a", [])),
                System.Text.Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync(FoldersServerModule.ProjectRoute, body, TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
            string content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            content.ShouldContain("\"projectionType\":\"folder-state\"");

            using StringContent bindingBody = new(
                System.Text.Json.JsonSerializer.Serialize(new ProjectionRequest("tenant-a", "folders",
                    ProviderBindingActorId.Create("binding-a"), [])),
                System.Text.Encoding.UTF8, "application/json");
            HttpResponseMessage binding = await client.PostAsync(FoldersServerModule.ProjectRoute,
                bindingBody, TestContext.Current.CancellationToken);
            binding.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
            (await binding.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
                .ShouldContain("\"projectionType\":\"organization-provider-binding\"");

            using StringContent baselineBody = new(
                System.Text.Json.JsonSerializer.Serialize(new ProjectionRequest("tenant-a", "folders", "tenant-a", [])),
                System.Text.Encoding.UTF8, "application/json");
            HttpResponseMessage baseline = await client.PostAsync(FoldersServerModule.ProjectRoute,
                baselineBody, TestContext.Current.CancellationToken);
            baseline.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
            string baselineContent = await baseline.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            baselineContent.ShouldContain("\"projectionType\":\"organization-provider-binding\"");

            // An unreadable retained event must fail closed at the actual HTTP seam.
            ProjectionEventDto corrupt = new(typeof(FolderCreated).FullName!,
                System.Text.Encoding.UTF8.GetBytes("{\"confidential\":\"secret-sentinel\"}"),
                "json", 1, DateTimeOffset.UtcNow, "correlation-a")
            {
                StoredPayloadVersion = 2,
                MetadataVersion = 1,
            };
            using StringContent unreadableBody = new(
                System.Text.Json.JsonSerializer.Serialize(
                    new ProjectionRequest("tenant-a", "folders", "folder-a", [corrupt])),
                System.Text.Encoding.UTF8, "application/json");
            HttpResponseMessage unreadable = await client.PostAsync(FoldersServerModule.ProjectRoute,
                unreadableBody, TestContext.Current.CancellationToken);
            unreadable.StatusCode.ShouldBe(System.Net.HttpStatusCode.UnprocessableEntity);
            string failure = await unreadable.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            failure.ShouldContain("projection_unreadable");
            failure.ShouldNotContain("secret-sentinel");
            failure.ShouldNotContain(typeof(FolderCreated).FullName!);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }
}
