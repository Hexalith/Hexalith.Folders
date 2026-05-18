using Hexalith.Folders.Server;
using Microsoft.AspNetCore.Builder;
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
}
