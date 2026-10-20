using System.Text;
using System.Text.Json;

using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Projections.OperationalStatus;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Tests.OperationalStatus;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Projections.OperationalStatus;

public sealed class OperationalStatusCapacityTests
{
    [Fact]
    public async Task TenThousandPositionsAndNinetyEightHistoricalTasksRebuildAndCatchUpWithBoundedValues()
    {
        var history = OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now).ToList();
        var locked = history.OfType<WorkspaceLockAcquired>().Single();
        var released = history.OfType<WorkspaceLockReleased>().Single();
        for (int task = 1; task < 98; task++)
        {
            string taskId = "historical-" + task;
            string lockId = "historical-lock-" + task;
            history.Add(locked with { TaskId = taskId, HolderTaskId = taskId, LockId = lockId });
            history.Add(released with { TaskId = taskId, HolderTaskId = taskId, LockId = lockId });
        }
        List<OperationalStatusEvent> sources = [.. OperationalStatusTestData.Sources(history)];
        while (sources.Count < 10_000)
            sources.Add(Rejection(sources[0], sources.Count + 1));
        OperationalStatusTestStore store = new();
        OperationalStatusProjectionWriter writer = new(store, store, store);
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "large-rebuild", sources, 10_000,
            TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        OperationalStatusCheckpoint checkpoint = store.Snapshot<OperationalStatusCheckpoint>("statestore",
            OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"))!;
        checkpoint.Sequence.ShouldBe(10_000);
        JsonSerializer.SerializeToUtf8Bytes(checkpoint).Length.ShouldBeLessThan(16_384);
        checkpoint.State.TaskClosures.ShouldBeEmpty();
        checkpoint.EventDigests.Count.ShouldBe(1);
        var reader = new EventStoreOperationalStatusReadModel(new(store, store), new OperationalStatusClock(),
            new OperationalStatusSequenceReader { Head = 10_000 });
        foreach (string taskId in new[] { "task-a", "historical-97" })
        {
            var result = await reader.GetAsync(new TaskStatusReadModelRequest("tenant-a", taskId, "reader",
                "read_task_status", "corr-a", "eventually_consistent")
            { AuthorizedFolderId = "folder-a", AuthorizationWatermark = "authority" }, TestContext.Current.CancellationToken);
            result.Status.ShouldBe(TaskStatusReadModelStatus.Available);
            result.Snapshot!.TerminalState.ShouldBe("ready");
        }
        (await new OperationalStatusProjectionWriter(store, store, store).ApplyAsync("tenant-a", "folder-a", "duplicate",
            [sources[0]], TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        (await writer.ApplyAsync("tenant-a", "folder-a", "catch-up", [Rejection(sources[0], 10_001)],
            TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"))!.Sequence.ShouldBe(10_001);
        sources.Add(Rejection(sources[0], 10_001));
        (await writer.PrepareRebuildAsync("tenant-a", "folder-a", sources, 10_001, TestContext.Current.CancellationToken))
            .Count.ShouldBeGreaterThan(10_000);
        // Acceptance remains open: the production SDK captured-identity handoff rejects this lawful history.
        var blocked = await Should.ThrowAsync<InvalidOperationException>(() => NamedOperationalStatusRebuildFixture.RebuildAsync(
            store, "tenant-a", "folder-a", "above-sdk-identity-boundary", sources, 10_001, TestContext.Current.CancellationToken));
        blocked.Message.ShouldContain("projection_handler_failure");
        var identityBoundary = await Should.ThrowAsync<ArgumentException>(() => new Hexalith.EventStore.Client.Projections.SharedProjectionEpochCoordinator(store, store)
            .SeedCapturedOwnerSequenceAsync(OperationalStatusGeneration.Scope("tenant-a"), "above-sdk-identity-boundary", "folder-a", 10_001,
                TestContext.Current.CancellationToken, new byte[10_001 * 32]));
        identityBoundary.Message.ShouldContain("Captured event identities must cover the complete owner history");
        identityBoundary.ParamName.ShouldBe("capturedEventDigests");
    }

    [Fact]
    public async Task NamedRebuildAbortsPreDispatchFailureWithoutPublishingRowsAsync()
    {
        OperationalStatusTestStore store = new();
        var sources = OperationalStatusTestData.Sources([OperationalStatusTestData.Created()]);
        var failure = await Should.ThrowAsync<InvalidOperationException>(() => NamedOperationalStatusRebuildFixture.RebuildAsync(store,
            "tenant-a", "folder-a", "pre-dispatch-failure", sources, 1, TestContext.Current.CancellationToken,
            afterBegin: () => store.BatchFaultHook = (phase, _, _) =>
            {
                if (phase == Hexalith.EventStore.Client.Projections.ReadModelBatchPhase.BeforeDispatch)
                {
                    store.BatchFaultHook = null;
                    throw new IOException("injected pre-dispatch batch failure");
                }
                return Task.CompletedTask;
            }, retryPartial: false));
        failure.Message.ShouldContain("Abort=Aborted:Completed");
        var coordinator = new Hexalith.EventStore.Client.Projections.SharedProjectionEpochCoordinator(store, store);
        (await coordinator.GetStatusAsync(OperationalStatusGeneration.Scope("tenant-a"), TestContext.Current.CancellationToken))
            .IsBuilding.ShouldBeFalse();
        (await coordinator.ReadAsync<OperationalStatusCheckpoint>(OperationalStatusGeneration.Scope("tenant-a"),
            OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"), TestContext.Current.CancellationToken)).Value.ShouldBeNull();
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "healthy-named", sources,
            1, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InterruptedNamedScratchBatchRetriesOrAbortsWithoutLeavingAnActiveSessionAsync(bool retry)
    {
        OperationalStatusTestStore store = new();
        var sources = OperationalStatusTestData.Sources([OperationalStatusTestData.Created()]);
        Task<Hexalith.EventStore.Client.Projections.ReadModelBatchResult> RebuildAsync() =>
            NamedOperationalStatusRebuildFixture.RebuildAsync(store,
                "tenant-a", "folder-a", "interrupted-scratch", sources, 1, TestContext.Current.CancellationToken,
                afterBegin: () => store.BatchFaultHook = (phase, _, _) =>
                {
                    if (phase == Hexalith.EventStore.Client.Projections.ReadModelBatchPhase.BeforeCommit)
                    {
                        store.BatchFaultHook = null;
                        throw new IOException("injected private scratch batch failure");
                    }
                    return Task.CompletedTask;
                }, retryPartial: retry);
        if (retry)
        {
            (await RebuildAsync()).IsSuccess.ShouldBeTrue();
            store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"))!
                .Sequence.ShouldBe(1);
        }
        else
        {
            var failure = await Should.ThrowAsync<InvalidOperationException>(RebuildAsync);
            failure.Message.ShouldContain("Abort=Aborted:Completed");
            store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"))
                .ShouldBeNull();
        }
        var coordinator = new Hexalith.EventStore.Client.Projections.SharedProjectionEpochCoordinator(store, store);
        (await coordinator.GetStatusAsync(OperationalStatusGeneration.Scope("tenant-a"), TestContext.Current.CancellationToken))
            .IsBuilding.ShouldBeFalse();
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "after-recovery",
            sources, 1, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"))!
            .Sequence.ShouldBe(1);
    }

    [Fact]
    public async Task RejectionOnlyPrefixPublishesNoViewsThenCreationConvergesWithCompleteReplay()
    {
        var original = OperationalStatusTestData.Sources([OperationalStatusTestData.Created()])[0];
        var rejected = Rejection(original, 1);
        var creation = OperationalStatusEventReader.ReadStored(original.Source! with
        { SequenceNumber = 2, GlobalPosition = 2, MessageId = "created-2" });
        OperationalStatusTestStore store = new();
        OperationalStatusProjectionWriter writer = new(store, store, store);
        (await writer.ApplyAsync("tenant-a", "folder-a", "rejection", [rejected], TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        store.Snapshot<FolderLifecycleStatusReadModelSnapshot>("statestore", OperationalStatusKeys.Lifecycle("tenant-a", "folder-a")).ShouldBeNull();
        (await writer.ApplyAsync("tenant-a", "folder-a", "creation", [creation], TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var live = store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"))!;
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "complete", [rejected, creation], 2, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        JsonSerializer.Serialize(store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"))).ShouldBe(JsonSerializer.Serialize(live));
    }

    [Fact]
    public async Task MalformedHolderAndSuccessRejectionCannotAdvanceCheckpointOrViews()
    {
        var source = OperationalStatusTestData.Sources(OperationalStatusTestData.LockedPrefix(OperationalStatusTestData.Now));
        OperationalStatusTestStore store = new();
        OperationalStatusProjectionWriter writer = new(store, store, store);
        (await writer.ApplyAsync("tenant-a", "folder-a", "locked", source, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        string before = JsonSerializer.Serialize(store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a")));
        var release = OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now).OfType<WorkspaceLockReleased>().Single()
            with
        { TaskId = "other-task", HolderTaskId = "other-task" };
        var invalid = OperationalStatusEventReader.ReadStored(OperationalStatusTestData.Envelope(release, source.Length + 1));
        await Should.ThrowAsync<InvalidOperationException>(() => writer.ApplyAsync("tenant-a", "folder-a", "invalid-holder", [invalid], TestContext.Current.CancellationToken));
        Should.Throw<InvalidOperationException>(() => OperationalStatusEventReader.ReadStored(source[0].Source! with
        {
            EventTypeName = "Hexalith.Folders.Server.FolderCommandRejected",
            Payload = Encoding.UTF8.GetBytes("{\"Code\":\"Accepted\",\"CommandType\":\"LockWorkspace\"}"),
        }));
        JsonSerializer.Serialize(store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"))).ShouldBe(before);
    }

    [Fact]
    public async Task RebuildCannotReplaceValidEarlierPrefixWhenTailEventIsIdentical()
    {
        var created = OperationalStatusTestData.Sources([OperationalStatusTestData.Created()])[0];
        var tail = Rejection(created, 2);
        OperationalStatusTestStore store = new();
        OperationalStatusProjectionWriter writer = new(store, store, store);
        (await writer.ApplyAsync("tenant-a", "folder-a", "original", [created, tail], TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var replacement = OperationalStatusTestData.Sources([OperationalStatusTestData.Created() with { DisplayName = "Contradictory retained prefix" }])[0];
        replacement.Digest.ShouldNotBe(created.Digest);
        var error = await Should.ThrowAsync<InvalidOperationException>(() => writer.PrepareRebuildAsync(
            "tenant-a", "folder-a", [replacement, tail], 2, TestContext.Current.CancellationToken));
        error.Message.ShouldContain("valid retained source anchor");
        store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"))!
            .SourceDigest.ShouldBe(OperationalStatusFold.Apply("tenant-a", "folder-a", null, [created, tail]).Checkpoint.SourceDigest);
    }

    [Fact]
    public async Task CompleteCurrentSourceConditionallyRepairsCorruptCheckpointAndKeepsCommandKeysOutOfStorage()
    {
        var history = OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now);
        var source = OperationalStatusTestData.Sources(history);
        OperationalStatusTestStore store = new();
        OperationalStatusProjectionWriter writer = new(store, store, store);
        (await writer.ApplyAsync("tenant-a", "folder-a", "initial", source, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        string key = OperationalStatusKeys.Checkpoint("tenant-a", "folder-a");
        OperationalStatusCheckpoint original = store.Snapshot<OperationalStatusCheckpoint>("statestore", key)!;
        store.SeedRaw("statestore", key, original with { State = original.State with { WorkspaceLifecycleState = (FolderWorkspaceLifecycleState)999 } });
        await Should.ThrowAsync<InvalidOperationException>(() => writer.ApplyAsync("tenant-a", "folder-a", "corrupt", [source[^1]], TestContext.Current.CancellationToken));
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "repair-corrupt", source,
            source.Length, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        JsonSerializer.Serialize(store.Snapshot<OperationalStatusCheckpoint>("statestore", key)).ShouldBe(JsonSerializer.Serialize(original));
        string serialized = JsonSerializer.Serialize(original);
        foreach (string commandKey in new[] { "create-key", "binding-key", "policy-key", "prepare-key", "release-key", "binding-fingerprint", "policy-fingerprint" })
            serialized.ShouldNotContain(commandKey, Case.Sensitive);
        (await writer.ApplyAsync("tenant-a", "folder-a", "duplicate-after-rebuild", [source[0]], TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
    }

    private static OperationalStatusEvent Rejection(OperationalStatusEvent original, long ordinal)
        => OperationalStatusEventReader.ReadStored(original.Source! with
        {
            SequenceNumber = ordinal,
            GlobalPosition = ordinal,
            MessageId = "rejected-" + ordinal,
            EventTypeName = "Hexalith.Folders.Server.FolderCommandRejected",
            Payload = Encoding.UTF8.GetBytes("{\"Code\":\"UnsupportedCommandType\",\"CommandType\":\"UnknownButLegitimate\"}"),
        });
}
