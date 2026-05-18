using Dapr;

using Hexalith.Folders.Server;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class ServerEndpointRegistrationTests
{
    [Fact]
    public void FoldersServerModuleShouldExposeStableDomainAndTenantRoutes()
    {
        FoldersServerModule.ProcessRoute.ShouldBe("/process");
        FoldersServerModule.ProjectRoute.ShouldBe("/project");
        FoldersServerModule.TenantEventsRoute.ShouldBe("/tenants/events");
        FoldersServerModule.TenantEventsPubSubName.ShouldBe("pubsub");
        FoldersServerModule.TenantEventsTopicName.ShouldBe("system.tenants.events");
        FoldersServerModule.Description.ShouldContain("server scaffold");
    }

    [Fact]
    public void MapFoldersServerEndpointsShouldRegisterDomainServiceAndTenantSubscriptionRoutes()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddFoldersServer();
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
        routes.ShouldContain("/tenants/events");
        routes.ShouldContain("dapr/subscribe");
    }

    [Fact]
    public void TenantEventsRouteShouldCarryExpectedTopicMetadata()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddFoldersServer();
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
    public async Task ProjectRouteShouldReturn501NotImplemented()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
        });
        builder.Configuration["urls"] = "http://127.0.0.1:0";
        builder.Services.AddFoldersServer();
        WebApplication app = builder.Build();

        app.UseCloudEvents();
        app.MapSubscribeHandler();
        app.MapFoldersServerEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
            using StringContent body = new("{}", System.Text.Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync(FoldersServerModule.ProjectRoute, body, TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotImplemented);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }
}
