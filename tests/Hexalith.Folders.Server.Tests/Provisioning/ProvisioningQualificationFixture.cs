using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

using Dapr.Client;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.DomainService.DurableTasks;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Server.Provisioning;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.Folders.Server.Tests.Provisioning;

/// <summary>Composes persisted Folder and Durable Task state for the two native qualification phases.</summary>
internal sealed class ProvisioningQualificationFixture(
    ProvisioningQualificationActorHost host, DaprClient dapr, ServiceProvider services, IConfiguration config,
    ProvisioningQualificationInvoker invoker, DurableTaskAdmission admission, ProvisioningQualificationClock clock, string channel) : IAsyncDisposable
{
    internal const string Actor = "original-actor";
    internal const string Other = "tenant-reader";
    internal const string DisplayName = "Shared name";
    internal const string Component = "statestore";
    internal const string ApplicationId = ProvisioningQualificationActorHost.ApplicationId;
    internal const string Operation = ProvisioningQualificationActorHost.Operation;
    internal const string Domain = ProvisioningQualificationActorHost.Domain;
    internal const string Tenant = ProvisioningQualificationActorHost.Tenant;
    internal static readonly JsonSerializerOptions FixtureJson = JsonSerializerOptions.Web;

    internal DurableTaskExecution Execution { get; } = new(admission, clock, TimeSpan.FromSeconds(30), 5);

    internal static async Task<ProvisioningQualificationFixture> StartAsync(bool frozen, CancellationToken token)
    {
        const string daprHttp = "http://127.0.0.1:3500";
        const string daprGrpc = "http://127.0.0.1:50001";
        var dapr = new DaprClientBuilder().UseHttpEndpoint(daprHttp).UseGrpcEndpoint(daprGrpc).Build();
        var clock = new ProvisioningQualificationClock();
        if (frozen)
        {
            clock.UtcNow = clock.UtcNow.AddSeconds(31);
        }

        var store = new DaprReadModelStore(dapr);
        var admission = new DurableTaskAdmission(store, Component, new ProvisioningQualificationAdmission(), clock);
        var accessor = new HttpContextAccessor();
        var invoker = new ProvisioningQualificationInvoker(accessor);
        string channel = Environment.GetEnvironmentVariable("G2_QUALIFICATION_CHANNEL")
            ?? throw new InvalidOperationException("Run this test with the isolated G-2 probe.");
        var host = await ProvisioningQualificationActorHost.StartAsync(admission, invoker, 8080, daprHttp, channel, token).ConfigureAwait(true);
        ServiceProvider? provider = null;
        try
        {
            using var http = new HttpClient();
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    using var health = await http.GetAsync(daprHttp + "/v1.0/healthz", token).ConfigureAwait(true);
                    if (health.IsSuccessStatusCode)
                    {
                        break;
                    }
                }
                catch (HttpRequestException) when (attempt < 30)
                {
                    // The candidate actor host and its sidecar start independently.
                }

                if (attempt >= 30)
                {
                    throw new InvalidOperationException("The fixture sidecar did not become ready.");
                }

                await Task.Delay(TimeSpan.FromSeconds(1), token).ConfigureAwait(true);
            }

            using var presenceKey = RSA.Create(2048);
            string privateKey = Convert.ToBase64String(presenceKey.ExportPkcs8PrivateKey());
            string publicKey = Convert.ToBase64String(presenceKey.ExportSubjectPublicKeyInfo());
            IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EventStore:Gateway:AppId"] = ApplicationId,
                ["Authentication:WorkloadIssuer:Workload"] = ApplicationId,
                ["Authentication:BoundStreamRead:PrivateKey"] = privateKey,
                [FolderProvisioningService.Section + "WorkerWorkload"] = ProvisioningQualificationActorHost.Worker,
                [FolderProvisioningService.Section + "DurableTaskAppId"] = ApplicationId,
                [FolderProvisioningService.Section + "Enabled"] = frozen ? "false" : "true",
                [FolderProvisioningService.Section + "FreezeMutations"] = frozen ? "true" : "false",
            }).Build();
            var registry = new ServiceCollection();
            registry.AddSingleton(config).AddSingleton(dapr).AddSingleton<TimeProvider>(clock).AddSingleton<IUtcClock>(clock);
            registry.AddSingleton<IReadModelStore>(store).AddSingleton<IHttpContextAccessor>(accessor);
            registry.AddSingleton<IWorkloadAssertionIssuer, ProvisioningQualificationIssuer>();
            registry.AddHttpClient(nameof(DurableTaskStepLookupClient));
            registry.AddHttpClient(nameof(FolderProvisioningStreamPresence))
                .ConfigurePrimaryHttpMessageHandler(() => new ProvisioningQualificationPresenceHandler(host, publicKey, clock));
            registry.AddSingleton<FolderProvisioningStreamPresence>();
            registry.AddSingleton(new ProvisioningQualificationStreams(host));
            registry.AddSingleton<IEventStoreGatewayClient>(sp => sp.GetRequiredService<ProvisioningQualificationStreams>());
            registry.AddSingleton<IOrganizationStreamSequenceReader>(sp => sp.GetRequiredService<ProvisioningQualificationStreams>());
            registry.AddSingleton<IFolderTenantAccessProjectionStore, EventStoreFolderTenantAccessProjectionStore>();
            registry.AddSingleton(new TenantAccessOptions());
            registry.AddSingleton<TenantAccessAuthorizer>();
            registry.AddSingleton<EventStoreOrganizationEffectivePermissionsReadModel>();
            registry.AddSingleton<EventStoreFolderEffectivePermissionsReadModel>();
            registry.AddSingleton<IEffectivePermissionsReadModel>(sp => sp.GetRequiredService<EventStoreFolderEffectivePermissionsReadModel>());
            registry.AddSingleton<IFolderPermissionEvidenceProvider, EffectivePermissionsFolderPermissionEvidenceProvider>();
            registry.AddSingleton(ProvisioningQualificationInvoker.Replay);
            registry.AddSingleton<IFolderRepository, ActorFoldedFolderRepository>();
            registry.AddSingleton<IAuthoritativeEventStreamReader>(sp => new AuthoritativeEventStreamReader(
                sp.GetRequiredService<IEventStoreGatewayClient>(), clock, requireNativeProof: true));
            registry.AddSingleton<DurableTaskStepLookupClient>();
            registry.AddSingleton<FolderProvisioningService>();
            provider = registry.BuildServiceProvider();
            invoker.Service = provider.GetRequiredService<FolderProvisioningService>;
            return new(host, dapr, provider, config, invoker, admission, clock, channel);
        }
        catch
        {
            if (provider is not null)
            {
                await provider.DisposeAsync().ConfigureAwait(true);
            }

            await host.DisposeAsync().ConfigureAwait(true);
            dapr.Dispose();
            throw;
        }
    }

    internal async Task<DurableTaskLease> AdmitAsync(string key, string actor, CancellationToken token)
    {
        var scope = new DurableTaskScope(Tenant, actor, Operation, key);
        var admitted = await admission.AdmitAsync(scope, FolderProvisioningContract.ComputeRequestSha256(DisplayName), cancellationToken: token).ConfigureAwait(true);
        admitted.Outcome.ShouldBe(DurableTaskAdmissionOutcome.Admitted);
        return (await Execution.TryAcquireAsync(admitted.Task.ShouldNotBeNull(), token).ConfigureAwait(true)).ShouldNotBeNull();
    }

    internal static FolderProvisioningRequest Request(DurableTaskEffectStep step)
        => new(step.Task.TaskId, step.Key, step.Task.Scope.Operation, step.Task.RequestSha256, step.Fence, 0, DisplayName);

    internal static string FolderId(FolderProvisioningRequest request, string tenant = Tenant)
        => FolderProvisioningContract.DeriveFolderId(tenant, request.StepKey);

    internal async Task<(int Status, int EventCount, string? Outcome)> SubmitAsync(FolderProvisioningRequest request, string tenant = Tenant)
    {
        string message = UniqueIdHelper.GenerateSortableUniqueStringId().ToLowerInvariant();
        string folder = FolderId(request, tenant);
        var command = new CommandEnvelope(message, tenant, Domain, folder, FolderProvisioningContract.CommandType,
            JsonSerializer.SerializeToUtf8Bytes(request, FixtureJson), message, null, ProvisioningQualificationActorHost.Worker,
            new() { [EventStoreGatewayVerifiedOrigin.ExtensionKey] = ProvisioningQualificationActorHost.Worker });
        CommandProcessingResult result = await host.Actor(tenant, folder).ProcessCommandAsync(command).ConfigureAwait(true);
        invoker.Observations.TryRemove(message, out var observed).ShouldBeTrue($"Actor returned {result.ErrorMessage}.");
        return (observed.Status, result.EventCount, observed.Outcome);
    }

    internal async Task SeedIncumbentAsync(FolderProvisioningRequest request, CancellationToken token)
    {
        string folder = FolderId(request);
        string message = UniqueIdHelper.GenerateSortableUniqueStringId().ToLowerInvariant();
        var command = new CommandEnvelope(message, Tenant, Domain, folder, ProvisioningQualificationInvoker.IncumbentCommand,
            [], message, null, Actor, null);
        CommandProcessingResult result = await host.Actor(Tenant, folder).ProcessCommandAsync(command).ConfigureAwait(true);
        result.EventCount.ShouldBe(1);
        await ProjectAsync(request, token).ConfigureAwait(true);
    }

    internal async Task RevokeManageAsync(FolderProvisioningRequest request, CancellationToken token)
    {
        string folder = FolderId(request);
        string message = UniqueIdHelper.GenerateSortableUniqueStringId().ToLowerInvariant();
        var command = new CommandEnvelope(message, Tenant, Domain, folder, ProvisioningQualificationInvoker.RevokeCommand,
            [], message, null, Actor, null);
        CommandProcessingResult result = await host.Actor(Tenant, folder).ProcessCommandAsync(command).ConfigureAwait(true);
        result.EventCount.ShouldBe(1);
        await ProjectAsync(request, token).ConfigureAwait(true);
    }

    internal Task<DurableTaskProgress?> ProgressAsync(CommittedDurableTaskAdmission task, CancellationToken token)
        => admission.GetProgressAsync(task.Scope, task.RequestSha256, token);

    internal async Task SetMembershipAsync(bool allowed, CancellationToken token)
    {
        var store = services.GetRequiredService<IFolderTenantAccessProjectionStore>();
        FolderTenantAccessProjection row = await store.GetAsync(Tenant, token).ConfigureAwait(true)
            ?? new FolderTenantAccessProjection { TenantId = Tenant, Enabled = true };
        row.Watermark++;
        row.ProjectionWatermark = Tenant + ":" + row.Watermark.ToString(CultureInfo.InvariantCulture);
        row.LastEventTimestamp = clock.UtcNow;
        if (allowed)
        {
            row.Principals[Actor] = new(Actor, "TenantMember");
        }
        else
        {
            row.Principals.Remove(Actor).ShouldBeTrue();
        }

        await store.SaveAsync(row, token).ConfigureAwait(true);
    }

    internal async Task SeedBaselineAsync(CancellationToken token)
    {
        var model = services.GetRequiredService<EventStoreOrganizationEffectivePermissionsReadModel>();
        IOrganizationAclEvent[] events =
        [
            new OrganizationAclBaselineInitialized(Tenant, Tenant, "org", "org", "org", "org"),
            Grant(Actor, "create_folder"),
            Grant(Actor, "read_metadata"),
            Grant(Actor, "manage_folder_access"),
            Grant(Other, "read_metadata"),
            Grant(Other, "manage_folder_access"),
        ];
        invoker.OrganizationEvents = events;
        string message = UniqueIdHelper.GenerateSortableUniqueStringId().ToLowerInvariant();
        var command = new CommandEnvelope(message, Tenant, Domain, Tenant, ProvisioningQualificationInvoker.OrganizationCommand,
            [], message, null, Actor, null);
        (await host.Actor(Tenant, Tenant).ProcessCommandAsync(command).ConfigureAwait(true)).EventCount.ShouldBe(events.Length);
        foreach (EventEnvelope item in await host.Actor(Tenant, Tenant).GetEventsAsync(0).ConfigureAwait(true))
        {
            await model.ApplyAsync(Tenant, Tenant, item.MessageId, item.SequenceNumber,
                events[checked((int)item.SequenceNumber - 1)], token).ConfigureAwait(true);
        }

        static OrganizationAclPrincipalGranted Grant(string actor, string action)
            => new(Tenant, Tenant, OrganizationAclPrincipalKind.User, actor, action, action, action, action, action);
    }

    internal async Task ProjectAsync(FolderProvisioningRequest request, CancellationToken token)
    {
        var model = services.GetRequiredService<EventStoreFolderEffectivePermissionsReadModel>();
        foreach (EventEnvelope item in await HistoryAsync(request).ConfigureAwait(true))
        {
            await model.ApplyAsync(Tenant, item.AggregateId, item.MessageId, item.SequenceNumber,
                Convert.ToHexStringLower(SHA256.HashData(item.Payload)),
                ProvisioningQualificationInvoker.ReadFolderEvent(item.EventTypeName, item.Payload), token).ConfigureAwait(true);
        }
    }

    internal Task<SourceProofInspection> InspectSourceAsync(FolderProvisioningRequest request)
        => host.Actor(Tenant, FolderId(request)).InspectSourceProofAsync();

    internal ProvisioningQualificationStreams Streams => services.GetRequiredService<ProvisioningQualificationStreams>();

    internal Task<EventEnvelope[]> HistoryAsync(FolderProvisioningRequest request, string tenant = Tenant)
        => host.Actor(tenant, FolderId(request, tenant)).GetEventsAsync(0);

    internal async Task<IReadOnlyList<string>> GrantsAsync(FolderProvisioningRequest request, CancellationToken token)
    {
        var model = services.GetRequiredService<EventStoreFolderEffectivePermissionsReadModel>();
        EffectivePermissionsReadModelResult result = await model.GetAsync(new(Tenant, FolderId(request),
            [EffectivePermissionPrincipal.User(Actor), EffectivePermissionPrincipal.User(Other)],
            null, null, "read_your_writes"), token).ConfigureAwait(true);
        result.Status.ShouldBe(EffectivePermissionsReadModelStatus.Available);
        return result.Snapshot.ShouldNotBeNull().EvidenceRows.Select(row => $"{row.Principal.PrincipalId}:{row.Action}")
            .Order(StringComparer.Ordinal).ToArray();
    }

    internal async Task<(int Status, JsonElement? Payload)> LookupAsync(FolderProvisioningRequest intent, CancellationToken token)
    {
        var assertion = new WorkloadAssertionRequest(ApplicationId, FolderProvisioningContract.LookupOperation,
            new Dictionary<string, string>
            {
                [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = Tenant,
                [EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] = Domain,
            });
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:8080" + FolderProvisioningContract.LookupPath)
        {
            Content = JsonContent.Create(intent, options: FixtureJson),
        };
        request.Headers.Add(DaprAppChannelToken.HeaderName, channel);
        request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName,
            ProvisioningQualificationIssuer.Sign(assertion, ProvisioningQualificationActorHost.Worker));
        using var client = new HttpClient();
        using HttpResponseMessage response = await client.SendAsync(request, token).ConfigureAwait(true);
        JsonElement? payload = response.StatusCode == HttpStatusCode.OK
            ? await response.Content.ReadFromJsonAsync<JsonElement>(FixtureJson, token).ConfigureAwait(true)
            : null;
        if (response.StatusCode != HttpStatusCode.OK)
        {
            (await response.Content.ReadAsByteArrayAsync(token).ConfigureAwait(true)).ShouldBeEmpty();
        }

        return ((int)response.StatusCode, payload);
    }

    internal void SetLookupDependency(bool available)
        => config[FolderProvisioningService.Section + "DurableTaskAppId"] = available ? ApplicationId : "g2-unavailable";

    public async ValueTask DisposeAsync()
    {
        await host.DisposeAsync().ConfigureAwait(true);
        await services.DisposeAsync().ConfigureAwait(true);
        dapr.Dispose();
    }
}
