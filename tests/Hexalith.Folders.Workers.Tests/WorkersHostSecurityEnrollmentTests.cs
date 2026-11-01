using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Tenants.Contracts.Events;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Workers.Tests;

/// <summary>Qualifies the actual Workers HTTP boundary with receiver-token authentication.</summary>
public sealed class WorkersHostSecurityEnrollmentTests
{
    /// <summary>Qualifies both inventories, exact probes and a persisted tenant event using actual receiver-channel authentication.</summary>
    [Fact]
    public async Task ProductionPipelineHasEmptyInventoriesExactProbesAndAuthenticatedChannelCallbacks()
    {
        var store = new InMemoryFolderTenantAccessProjectionStore();
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration[DaprAppChannelToken.ConfigurationKey] = "workers-enrollment-channel";
        builder.AddFoldersWorkersHost();
        builder.Services.RemoveAll<IFolderTenantAccessProjectionStore>();
        builder.Services.AddSingleton<IFolderTenantAccessProjectionStore>(store);
        await using WebApplication app = builder.Build();
        app.UseFoldersWorkersPipeline();
        await app.StartAsync(TestContext.Current.CancellationToken);
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).ToArray();
        (await EventStoreHttpEndpointInventory.ValidateAsync(endpoints, app.Services)).ShouldBeEmpty();
        EventStoreDomainServiceEndpointInventory.Validate(endpoints, null).ShouldBeEmpty();
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.Single()) };
        foreach (string probe in new[] { "/health", "/alive", "/ready" })
        {
            using HttpResponseMessage response = await client.GetAsync(probe, TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        foreach (string route in new[] { "/", "/health/live", "/health/ready", "/dapr/subscribe" })
        {
            using HttpResponseMessage denied = await client.GetAsync(route, TestContext.Current.CancellationToken);
            denied.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, route);
            using var valid = new HttpRequestMessage(HttpMethod.Get, route);
            valid.Headers.Add(DaprAppChannelToken.HeaderName, "workers-enrollment-channel");
            using HttpResponseMessage admitted = await client.SendAsync(valid, TestContext.Current.CancellationToken);
            admitted.StatusCode.ShouldBe(HttpStatusCode.OK, route);
        }
        var created = new TenantCreated("enrollment-tenant", "Enrollment", null, DateTimeOffset.UtcNow);
        var envelope = new EventStoreDomainEventEnvelope("01J00000000000000000000001", "enrollment-tenant", "system",
            typeof(TenantCreated).FullName!, 1, DateTimeOffset.UtcNow, "enrollment-correlation", "json", JsonSerializer.SerializeToUtf8Bytes(created));
        foreach (string kind in new[] { "absent", "wrong", "mixed", "valid" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, FoldersWorkersModule.TenantEventsRoute) { Content = JsonContent.Create(envelope) };
            if (kind != "absent") request.Headers.Add(DaprAppChannelToken.HeaderName, kind == "wrong" ? "wrong" : "workers-enrollment-channel");
            if (kind == "mixed") request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, "untrusted");
            using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(kind == "valid" ? HttpStatusCode.OK : HttpStatusCode.Unauthorized, kind);
            if (kind != "valid") (await store.GetAsync("enrollment-tenant", TestContext.Current.CancellationToken)).ShouldBeNull();
        }
        (await store.GetAsync("enrollment-tenant", TestContext.Current.CancellationToken)).ShouldNotBeNull();
        await app.StopAsync(TestContext.Current.CancellationToken);
    }
}
