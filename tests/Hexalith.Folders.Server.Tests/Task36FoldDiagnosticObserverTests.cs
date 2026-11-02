using System.Diagnostics;
using System.Text.Json;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.Folders.Projections.OperationalStatus;
using Hexalith.Folders.Server;
using Hexalith.Folders.Tests.OperationalStatus;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Task36FoldObserver;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Qualifies real owner identity and exact callback entry evidence without a handler decorator.</summary>
[Collection("Task36 fold observation")]
public sealed class Task36FoldDiagnosticObserverTests
{
    [Fact]
    public async Task HostingStartupPreservesTheRealHandlerAndItsSdkReceiverMatch()
    {
        WebApplicationBuilder builder = FoldersProductionTestHost.CreateBuilder(useCallbackCatalog: false);
        new Task36HostingStartup().Configure(builder.WebHost);
        await using WebApplication app = builder.Build();
        app.UseFoldersServerPipeline();
        using IServiceScope scope = app.Services.CreateScope();
        var handler = scope.ServiceProvider.GetServices<IAsyncDomainProjectionHandler>()
            .Single(candidate => candidate.ProjectionType == OperationalStatusKeys.ProjectionType);
        handler.GetType().ShouldBe(typeof(OperationalStatusProjectionHandler));
        var declarations = CompiledRouteCatalogDeclarations.Read([typeof(FoldersRuntimeCatalogDeclarations).Assembly]);
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints);
        var targets = DevelopmentDomainRouteCatalog.Targets(scope.ServiceProvider, endpoints, declarations);
        targets.Single(target => target.RouteId == "folders-operational-status").Method.ShouldBe("project/v2");
        declarations.Single(declaration => declaration.RouteId == "folders-operational-status").Execution!.HandlerType
            .ShouldBe(handler.GetType().FullName);
    }

    [Fact]
    public async Task ObserverCountsOnlyActualSdkHandlerEntryAndPreservesReplayAndConflictOutcomes()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        string directory = Directory.CreateTempSubdirectory("folders-fold-observer-").FullName;
        string path = Path.Combine(directory, "folds.jsonl");
        try
        {
            OperationalStatusTestStore store = new();
            var writer = new OperationalStatusProjectionWriter(store, store, store);
            var handler = new OperationalStatusProjectionHandler(writer);
            ServiceCollection services = new();
            services.AddSingleton<IAsyncDomainProjectionHandler>(handler);
            services.AddSingleton(new SharedProjectionEpochCoordinator(store, store, store.BatchOptions.TimeProvider));
            using ServiceProvider provider = services.BuildServiceProvider();
            var registry = new DomainProjectionCatalogRegistry();
            registry.Register("observed-catalog", [new("folders", OperationalStatusKeys.ProjectionType)]);
            ProjectionRequest first = Request("folder-a", 1);
            using var observer = new FoldDiagnosticObserver(path);
            await observer.StartAsync(token);
            await observer.StartAsync(token); // Hosted startup must not double-subscribe.

            (await DispatchAsync(first, "first")).Status.ShouldBe(ProjectionDispatchStatus.Completed);
            FoldRows(path).Count.ShouldBe(1);
            (await DispatchAsync(first, "first")).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
            FoldRows(path).Count.ShouldBe(1);

            ProjectionRequest conflict = first with
            {
                Events = [first.Events[0] with
            {
                Payload = JsonSerializer.SerializeToUtf8Bytes(OperationalStatusTestData.Created() with { DisplayName = "conflicting-source" }),
            }]
            };
            var rejected = await DispatchAsync(conflict, "conflict");
            rejected.Status.ShouldBe(ProjectionDispatchStatus.Failed);
            rejected.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            FoldRows(path).Count.ShouldBe(1);

            // Ordinary writer/Workers catch-up uses its own fold and must not be counted as SDK handler entry.
            (await writer.ApplyAsync("tenant-a", "worker-folder", "worker-delivery",
                [OperationalStatusEventReader.ReadStored(Request("worker-folder", 2).Events[0])], token)).IsSuccess.ShouldBeTrue();
            store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "worker-folder"))!
                .Sequence.ShouldBe(1);
            FoldRows(path).Count.ShouldBe(1);

            (await DispatchAsync(Request("second-folder", 3), "second")).Status.ShouldBe(ProjectionDispatchStatus.Completed);
            FoldRows(path).ShouldBe(new[] { ("folder-a", 1L), ("second-folder", 3L) });
            using (var unrelated = new DiagnosticListener("unrelated-qualification-source"))
                unrelated.Write(FoldDiagnosticObserver.EventName, new KeyValuePair<string, long>("unrelated", 4));
            FoldRows(path).Count.ShouldBe(2);

            string[] rows = File.ReadAllLines(path);
            rows.Select(row => JsonDocument.Parse(row)).ShouldContain(row => row.RootElement.GetProperty("action").GetString() == "registered");
            foreach (string row in rows)
            {
                using JsonDocument document = JsonDocument.Parse(row);
                document.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)
                    .ShouldBe(new[] { "action", "position", "processId", "source" });
                document.RootElement.GetProperty("processId").GetInt32().ShouldBe(Environment.ProcessId);
                row.ShouldNotContain("not-observed-payload");
            }

            await observer.StopAsync(token);
            (await DispatchAsync(Request("unobserved-folder", 4), "after-stop")).Status.ShouldBe(ProjectionDispatchStatus.Completed);
            File.ReadAllLines(path).ShouldBe(rows);
            provider.GetServices<IAsyncDomainProjectionHandler>().Single().ShouldBeSameAs(handler);

            async Task<ProjectionDispatchOutcome> DispatchAsync(ProjectionRequest request, string id)
                => (await DomainProjectionDispatcher.DispatchAsync(provider,
                    new(request, [OperationalStatusKeys.ProjectionType], id, "observed-catalog"), new(), registry, token).ConfigureAwait(false)).Outcomes.Single();
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static ProjectionRequest Request(string folder, long position)
    {
        var request = OperationalStatusTestData.Projection([OperationalStatusTestData.Created(folder: folder) with { DisplayName = "not-observed-payload" }]);
        return request with { AggregateId = folder, Events = [request.Events[0] with { GlobalPosition = position }] };
    }

    private static IReadOnlyList<(string Source, long Position)> FoldRows(string path)
        => File.ReadAllLines(path).Select(row => JsonDocument.Parse(row)).Where(row => row.RootElement.GetProperty("action").GetString() == "fold")
            .Select(row => (row.RootElement.GetProperty("source").GetString()!, row.RootElement.GetProperty("position").GetInt64())).ToArray();
}
