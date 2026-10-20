using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Serialization;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Queries.ProviderReadiness;
using Hexalith.Folders.Server;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Folders.Testing;
using Hexalith.Folders.Tests.OperationalStatus;
using Hexalith.Folders.Workers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Workers.Tests;

/// <summary>Checks original provider delivery and the legacy repair route against the same retained bytes.</summary>
public sealed class ProviderBindingDeliveryRepairTests
{
    [Fact]
    public async Task WorkerBindingBytesEqualProviderProjectRebuildAndMalformedRejectionsNeverAdvanceAsync()
    {
        WorkerRouteReadModelStore store = new();
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        { EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development });
        builder.Configuration["urls"] = "http://127.0.0.1:0";
        builder.Configuration[DaprAppChannelToken.ConfigurationKey] = "test-sidecar-token";
        builder.Services.AddFoldersServerTestDefaults();
        builder.Services.AddFoldersServer();
        builder.Services.AddFoldersTenantEventWorkers();
        builder.Services.AddSingleton<IFolderRepository>(new InMemoryFolderRepository());
        builder.Services.AddFoldersProviderBindingPersistence();
        builder.Services.RemoveAll<IReadModelStore>();
        builder.Services.AddSingleton<IReadModelStore>(store);
        builder.Services.RemoveAll<IOrganizationStreamSequenceReader>();
        builder.Services.AddSingleton<IOrganizationStreamSequenceReader>(new OperationalStatusSequenceReader { Head = 1 });
        builder.Services.AddSingleton<HumanAuthorityReceiptVerifier>();
        // Production workload assertions are exercised by Server tests; this in-process composition
        // uses the same authenticated sidecar caller for both routes and never publishes externally.
        builder.Services.AddAuthorization(options => options.AddPolicy(EventStoreWorkloadAuthenticationDefaults.OperationPolicy(
            EventStoreWorkloadAuthenticationDefaults.WorkloadScheme, EventStoreWorkloadOperations.DomainServiceProject),
            policy => policy.AddAuthenticationSchemes(EventStoreWorkloadAuthenticationDefaults.SidecarChannelScheme).RequireAuthenticatedUser()));
        await using WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapFoldersTenantEventWorkerEndpoints();
        app.MapFoldersDomainServiceEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
        client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, "test-sidecar-token");
        ProviderBindingConfigured configured = new("tenant-a", "tenant-a", "binding-a", "forgejo", "credential-ref",
            OrganizationProviderBindingPolicy.Empty, OrganizationProviderBindingPolicy.Empty,
            "corr", "task", "key", "pending", "configured", DateTimeOffset.UtcNow);
        configured = configured with
        {
            IdempotencyFingerprint = OrganizationProviderBindingCommandValidator.Validate(new(
            configured.ManagedTenantId, configured.OrganizationId, OrganizationAclPrincipalKind.User, "principal", configured.ProviderBindingRef,
            configured.ProviderKind, configured.CredentialReferenceId, configured.NamingPolicy, configured.BranchPolicy,
            configured.CorrelationId, configured.TaskId, configured.IdempotencyKey, configured.OccurredAt, null)).IdempotencyFingerprint
        };
        string actor = ProviderBindingActorId.Create("binding-a");
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(configured, EventStorePayloadSerialization.Options);
        EventStoreDomainEventEnvelope envelope = new("source-1", actor, "tenant-a", typeof(ProviderBindingConfigured).FullName!,
            1, configured.OccurredAt, "corr", "json", payload)
        { Domain = "folders", MetadataVersion = 1, GlobalPosition = 1 };
        using (HttpResponseMessage delivered = await client.PostAsJsonAsync("/folders/events", envelope, TestContext.Current.CancellationToken))
            delivered.StatusCode.ShouldBe(HttpStatusCode.OK);
        string key = EventStoreProviderReadinessBindingReadModel.Key("tenant-a", actor);
        string live = JsonSerializer.Serialize((await store.GetAsync<ProviderBindingProjection>("statestore", key, TestContext.Current.CancellationToken)).Value);
        ProjectionRequest history = new("tenant-a", "folders", actor,
            [new(typeof(ProviderBindingConfigured).FullName!, payload, "json", 1, configured.OccurredAt, "corr", "source-1")
            { MetadataVersion = 1, GlobalPosition = 1 }]);
        using (HttpResponseMessage rebuilt = await client.PostAsJsonAsync("/project", history, TestContext.Current.CancellationToken))
            rebuilt.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonSerializer.Serialize((await store.GetAsync<ProviderBindingProjection>("statestore", key, TestContext.Current.CancellationToken)).Value).ShouldBe(live);
        foreach (string code in new[] { "Accepted", "999" })
        {
            ProjectionRequest malformed = history with
            {
                Events = [history.Events[0] with
            {
                EventTypeName = "Hexalith.Folders.Server.FolderCommandRejected",
                Payload = JsonSerializer.SerializeToUtf8Bytes(new { Code = code, CommandType = "Hexalith.Folders.Commands.ConfigureProviderBinding" }),
            }]
            };
            using HttpResponseMessage rejected = await client.PostAsJsonAsync("/project", malformed, TestContext.Current.CancellationToken);
            rejected.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            JsonSerializer.Serialize((await store.GetAsync<ProviderBindingProjection>("statestore", key, TestContext.Current.CancellationToken)).Value).ShouldBe(live);
        }
        await app.StopAsync(TestContext.Current.CancellationToken);
    }
}
