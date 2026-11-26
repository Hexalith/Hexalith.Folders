using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.DomainService.DurableTasks;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Folders.Server.Provisioning;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hexalith.Folders.Server.Tests.Provisioning;

/// <summary>Runs the actual EventStore aggregate actor and authenticated task lookup against the fixture sidecar.</summary>
internal sealed class ProvisioningQualificationActorHost(WebApplication app, ActorProxyFactory proxyFactory) : IAsyncDisposable
{
    internal const string ApplicationId = "g2-folders-fixture";
    internal const string Domain = "folders";
    internal const string Operation = "fixture.activate-project@1";
    internal const string Tenant = "tenant-a";
    internal const string Worker = "projects-fixture";
    internal const int StepNumber = 1;
    internal const string Issuer = "https://g2-fixture.invalid";
    internal const string SigningKey = "isolated-g2-fixture-signing-key-asymmetric-authority-outside-scope";

    internal static async Task<ProvisioningQualificationActorHost> StartAsync(
        DurableTaskAdmission admission, ProvisioningQualificationInvoker invoker, int appPort, string daprEndpoint,
        string channelToken, CancellationToken token)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.ConfigureKestrel(options => options.ListenLocalhost(appPort));
        builder.Logging.ClearProviders();
        const string section = DurableTaskStepLookupExtensions.WorkloadSection;
        const string ownerSection = FolderProvisioningEndpoints.WorkloadSection;
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [DaprAppChannelToken.ConfigurationKey] = channelToken,
            [$"{section}:Audience"] = ApplicationId,
            [$"{section}:AllowedCallers:0"] = ApplicationId,
            [$"{ownerSection}:Audience"] = ApplicationId,
            [$"{ownerSection}:AllowedCallers:0"] = Worker,
            [FolderProvisioningService.Section + "WorkerWorkload"] = Worker,
            ["Authentication:WorkloadJwtBearer:Issuer"] = Issuer,
            ["Authentication:WorkloadJwtBearer:Audience"] = ApplicationId,
            ["Authentication:WorkloadJwtBearer:SigningKey"] = SigningKey,
            ["Authentication:WorkloadJwtBearer:AllowedAlgorithms:0"] = "HS256",
        });
        builder.Services.AddSingleton(admission).AddEventStoreHttpHostSecurity().AddDurableTaskStepLookup();
        builder.Services.AddAuthentication().AddEventStoreWorkloadScheme(FolderProvisioningEndpoints.Scheme, ownerSection);
        builder.Services.AddEventStoreWorkloadPolicies(FolderProvisioningEndpoints.Scheme, [FolderProvisioningContract.LookupOperation]);
        builder.Services.AddScoped(_ => invoker.Service?.Invoke()
            ?? throw new InvalidOperationException("No Folders fixture owner lookup."));
        builder.Services.AddActors(options => options.Actors.RegisterActor<AggregateActor>());
        builder.Services.AddSingleton<IDomainServiceInvoker>(invoker);
        builder.Services.AddSingleton<ISnapshotManager, FakeSnapshotManager>();
        builder.Services.AddSingleton<IEventPayloadProtectionService, NoOpEventPayloadProtectionService>();
        builder.Services.AddSingleton<IEventPublisher, FakeEventPublisher>();
        builder.Services.AddSingleton<IDeadLetterPublisher, FakeDeadLetterPublisher>();
        builder.Services.AddSingleton<ICommandStatusStore, InMemoryCommandStatusStore>();
        builder.Services.AddOptions<EventDrainOptions>();
        builder.Services.AddOptions<BackpressureOptions>();

        WebApplication app = builder.Build();
        app.UseRouting();
        app.UseEventStoreHttpHostSecurity();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapActorsHandlers().RequireEventStoreChannel();
        app.MapFoldersProvisioning();
        app.MapDurableTaskStepLookup(new DurableTaskStepLookupGrant(ApplicationId, Operation, StepNumber, Domain,
            (task, key) => FolderProvisioningContract.DeriveFolderId(task.Scope.TenantId, key)));
        try
        {
            await app.StartAsync(token).ConfigureAwait(true);
            var proxy = new ActorProxyFactory(new ActorProxyOptions { HttpEndpoint = daprEndpoint });
            return new(app, proxy);
        }
        catch
        {
            await app.DisposeAsync().ConfigureAwait(true);
            throw;
        }
    }

    internal IAggregateActor Actor(string tenant, string folder)
        => proxyFactory.CreateActorProxy<IAggregateActor>(new ActorId($"{tenant}:{Domain}:{folder}"), nameof(AggregateActor));

    public ValueTask DisposeAsync() => app.DisposeAsync();
}
