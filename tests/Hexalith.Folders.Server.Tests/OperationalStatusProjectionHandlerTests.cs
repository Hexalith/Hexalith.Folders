using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Projections.OperationalStatus;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Server;
using Hexalith.Folders.Tests.OperationalStatus;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class OperationalStatusProjectionHandlerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NamedIntakeRejectsMalformedWorkspaceOrLeaseBeforePersistence(bool malformedLease)
    {
        var history = OperationalStatusTestData.LockedPrefix(OperationalStatusTestData.Now);
        OperationalStatusTestStore store = new();
        var handler = new OperationalStatusProjectionHandler(new(store, store, store));
        var prefix = OperationalStatusTestData.Projection(history.Take(history.Count - 1));
        (await handler.ProjectAsync(prefix, "prefix", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var acquired = history.OfType<WorkspaceLockAcquired>().Single();
        acquired = malformedLease ? acquired with { ExpiresAt = acquired.AcquiredAt.AddSeconds(-1) } : acquired with { WorkspaceId = null! };
        var invalid = OperationalStatusTestData.Projection([acquired]).Events[0] with
        { SequenceNumber = history.Count, GlobalPosition = history.Count, MessageId = "malformed" };
        (await handler.ProjectAsync(prefix with { Events = [invalid] }, "invalid", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Failed);
        store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"))!.Sequence.ShouldBe(history.Count - 1);
    }

    [Fact]
    public async Task NamedHandlerPersistsAndPreparesSideEffectFreeRebuildThroughSdk()
    {
        OperationalStatusTestStore store = new();
        var handler = new OperationalStatusProjectionHandler(new(store, store, store));
        ProjectionRequest request = OperationalStatusTestData.Projection([OperationalStatusTestData.Created()]);
        (await handler.ProjectAsync(request, "delivery-a", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await handler.ProjectAsync(request, "delivery-a", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        store.Snapshot<OperationalStatusCheckpoint>(OperationalStatusKeys.StoreName, OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"))!.Sequence.ShouldBe(1);
        int before = store.Count;
        var plan = await handler.PrepareRebuildAsync(request, "rebuild-a", TestContext.Current.CancellationToken);
        plan.StoreName.ShouldBe(OperationalStatusKeys.StoreName);
        plan.Operations.Where(static operation => operation.TimeToLive is not null).ShouldAllBe(static operation => operation.TimeToLive > TimeSpan.Zero && operation.TimeToLive <= OperationalStatusKeys.Retention);
        store.Count.ShouldBe(before);
        handler.RebuildSemantics.ShouldBe(DomainProjectionRebuildSemantics.FullReplay);
    }

    [Fact]
    public void ProductionHostRegistersDurableStatusAndKeepsLegacyResponseShape()
    {
        WebApplicationBuilder builder = FoldersProductionTestHost.CreateBuilder();
        using WebApplication app = builder.Build();
        app.Services.GetRequiredService<IFolderLifecycleStatusReadModel>().ShouldBeOfType<EventStoreOperationalStatusReadModel>();
        app.Services.GetRequiredService<IWorkspaceStatusReadModel>().ShouldBeSameAs(app.Services.GetRequiredService<ITaskStatusReadModel>());
        app.Services.GetServices<IAsyncDomainProjectionHandler>().ShouldContain(static handler => handler is OperationalStatusProjectionHandler);
        var response = new FolderDomainProjectionHandler().Project(OperationalStatusTestData.Projection([OperationalStatusTestData.Created()]));
        response.ProjectionType.ShouldBe("folder-state");
        response.State.TryGetProperty("taskClosures", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task UnknownOrUnsupportedSourceDoesNotPersistCompletion()
    {
        OperationalStatusTestStore store = new();
        var handler = new OperationalStatusProjectionHandler(new(store, store, store));
        ProjectionRequest valid = OperationalStatusTestData.Projection([OperationalStatusTestData.Created()]);
        var invalid = valid with { Events = [valid.Events[0] with { EventTypeName = "Unknown.Event" }] };
        (await handler.ProjectAsync(invalid, "bad", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Failed);
        store.Count.ShouldBe(0);
        Should.Throw<EventPayloadEvolutionException>(() => OperationalStatusEventReader.ReadStored(valid.Events[0] with { StoredPayloadVersion = 2 }));
    }

    [Fact]
    public async Task NamedRebuildPlansSealedPrefixWithoutReadingALiveHeadAsync()
    {
        OperationalStatusTestStore store = new();
        var handler = new OperationalStatusProjectionHandler(new(store, store, store));
        ProjectionRequest request = OperationalStatusTestData.Projection([OperationalStatusTestData.Created()]);
        (await handler.PrepareRebuildAsync(request, "captured-prefix", TestContext.Current.CancellationToken)).Operations.ShouldNotBeEmpty();
        var truncated = request with { Events = [request.Events[0] with { SequenceNumber = 2 }] };
        await Should.ThrowAsync<InvalidOperationException>(() => handler.PrepareRebuildAsync(truncated, "gap", TestContext.Current.CancellationToken));
        store.Count.ShouldBe(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NamedIntakeRejectsReusedMessagePositionsAndUndefinedRejectionCodes(bool reusedMessage)
    {
        OperationalStatusTestStore store = new();
        var handler = new OperationalStatusProjectionHandler(new(store, store, store));
        ProjectionRequest request = OperationalStatusTestData.Projection([OperationalStatusTestData.Created()]);
        (await handler.ProjectAsync(request, "initial", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var rejected = request.Events[0] with
        {
            SequenceNumber = 2,
            MessageId = reusedMessage ? request.Events[0].MessageId : "new-rejection",
            EventTypeName = typeof(FolderCommandRejected).FullName!,
            Payload = System.Text.Encoding.UTF8.GetBytes(reusedMessage
                ? "{\"Code\":\"StateTransitionInvalid\",\"CommandType\":\"LockWorkspace\"}"
                : "{\"Code\":\"999\",\"CommandType\":\"LockWorkspace\"}"),
        };
        (await handler.ProjectAsync(request with { Events = [rejected] }, "invalid", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Failed);
        store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"))!.Sequence.ShouldBe(1);
    }

    [Fact]
    public void RetainedNameVersionAndClosedMissingVersionInventoryIsExhaustive()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "event-evolution");
        using JsonDocument inventory = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "operational-status-inventory.json")));
        var root = inventory.RootElement;
        root.GetProperty("historicalPayloadGenerations").GetArrayLength().ShouldBe(0);
        root.GetProperty("applicableUpcasterChains").GetArrayLength().ShouldBe(0);
        EventPayloadEvolutionRegistry.DiscoverUpcasters(typeof(IFolderEvent).Assembly).ShouldBeEmpty();
        string[] expected = typeof(IFolderEvent).Assembly.GetTypes().Where(static type => type.IsClass && !type.IsAbstract
            && typeof(IFolderEvent).IsAssignableFrom(type)).Select(static type => type.FullName!)
            .Concat([typeof(FolderCommandRejected).FullName!, typeof(DuplicateWorkspaceLockRejected).FullName!, typeof(WorkspaceTransitionInvalidRejected).FullName!])
            .Order(StringComparer.Ordinal).ToArray();
        string[] declared = root.GetProperty("currentPayloads").EnumerateArray().Select(static value => value.GetProperty("eventTypeName").GetString()!)
            .Order(StringComparer.Ordinal).ToArray();
        declared.ShouldBe(expected);
        root.GetProperty("currentPayloads").EnumerateArray().ShouldAllBe(static value => value.GetProperty("payloadVersion").GetInt32() == 1);
        root.GetProperty("missingVersionRegistry").EnumerateArray().Select(static value => value.GetProperty("eventTypeName").GetString()!)
            .Order(StringComparer.Ordinal).ShouldBe(expected);
        root.GetProperty("missingVersionRegistry").EnumerateArray().ShouldAllBe(static value => value.GetProperty("readAsVersion").GetInt32() == 1
            && value.GetProperty("status").GetString() == "closed-legacy-v1");
        foreach (string entry in File.ReadAllLines(Path.Combine(directory, "retained-fixtures-sha256.txt")))
        {
            string[] parts = entry.Split("  ", StringSplitOptions.None);
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(directory, parts[1])))).ShouldBe(parts[0]);
        }
    }
}
