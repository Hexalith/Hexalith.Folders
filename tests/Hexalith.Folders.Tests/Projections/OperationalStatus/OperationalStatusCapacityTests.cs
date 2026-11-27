using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Projections;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Projections.OperationalStatus;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Server;
using Hexalith.Folders.Tests.OperationalStatus;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Projections.OperationalStatus;

public sealed class OperationalStatusCapacityTests
{
    /// <summary>Rebuilds complete original owners and preserves bounded identity proofs and historical task views after growth.</summary>
    /// <param name="count">The complete original owner history length.</param>
    [Theory]
    [InlineData(10_001)]
    [InlineData(32_769)]
    public async Task CompleteOriginalOwnersRebuildGrowAndPreserveHistoricalTasksWithBoundedValues(int count)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
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
        while (sources.Count < count) sources.Add(Rejection(sources[0], sources.Count + 1));
        string originalHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(sources.Select(item => item.Source))));
        OperationalStatusTestStore store = new();
        OperationalStatusProjectionWriter writer = new(store, store, store, scanner: Hexalith.Folders.Tests.OperationalStatus.OperationalStatusTestData.Scanner);
        var other = OperationalStatusTestData.Sources([OperationalStatusTestData.Created(folder: "folder-b")]);
        other[0] = OperationalStatusTestData.Stored(other[0].Source! with { GlobalPosition = count + 1 });
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "large-rebuild", sources, count,
            token, additionalOwners: [("folder-b", other)])).IsSuccess.ShouldBeTrue();
        var coordinator = new Hexalith.EventStore.Client.Projections.SharedProjectionEpochCoordinator(store, store);
        var scope = OperationalStatusGeneration.Scope("tenant-a");
        var sdkCheckpoint = (await coordinator.GetCheckpointAsync(scope, "folder-a", token))!;
        sdkCheckpoint.OwnerSequenceNumber.ShouldBe(count);
        sdkCheckpoint.CapturedEventDigests.ShouldBeNull();
        sdkCheckpoint.CapturedIdentityReference!.Count.ShouldBe(count);
        JsonSerializer.SerializeToUtf8Bytes(sdkCheckpoint).Length.ShouldBeLessThan(4096);
        OperationalStatusCheckpoint checkpoint = (await coordinator.ReadAsync<OperationalStatusCheckpoint>(scope,
            OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"), token)).Value!;
        checkpoint.Sequence.ShouldBe(count);
        JsonSerializer.SerializeToUtf8Bytes(checkpoint).Length.ShouldBeLessThan(16_384);
        checkpoint.State.TaskClosures.ShouldBeEmpty();
        checkpoint.EventDigests.Count.ShouldBe(1);
        var sequence = new OperationalStatusSequenceReader { Head = count };
        var reader = new EventStoreOperationalStatusReadModel(coordinator, new OperationalStatusClock(), sequence, scanner: Hexalith.Folders.Tests.OperationalStatus.OperationalStatusTestData.Scanner);
        foreach (string taskId in new[] { "task-a", "historical-97" })
        {
            var result = await reader.GetAsync(new TaskStatusReadModelRequest("tenant-a", taskId, "reader",
                "read_task_status", "corr-a", "eventually_consistent")
            { AuthorizedFolderId = "folder-a", AuthorizationWatermark = "authority" }, token);
            result.Status.ShouldBe(TaskStatusReadModelStatus.Available);
            result.Snapshot!.TerminalState.ShouldBe("ready");
        }
        // A later independent owner generation inherits the complete original owner's proof and views.
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-b", "other-owner", other,
            other[0].Source!.GlobalPosition, token)).IsSuccess.ShouldBeTrue();
        (await coordinator.ReadAsync<OperationalStatusCheckpoint>(scope,
            OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"), token)).Value!.SourceDigest.ShouldBe(checkpoint.SourceDigest);
        foreach (int index in new[] { 0, 7, count / 2, count - 1 })
            (await writer.ApplyAsync("tenant-a", "folder-a", "duplicate-" + index, [sources[index]], token)).IsSuccess.ShouldBeTrue();
        var growth = Rejection(sources[0], count + 1);
        growth = OperationalStatusTestData.Stored(growth.Source! with { GlobalPosition = count + 2 });
        string lifecycleKey = store.PhysicalKey(OperationalStatusKeys.Lifecycle("tenant-a", "folder-a"));
        bool competingWriterCompleted = false;
        OperationalStatusReadObserver racingStore = new(store, beforeRead: async key =>
        {
            if (key != lifecycleKey || competingWriterCompleted
                || (await coordinator.GetStatusAsync(scope, token).ConfigureAwait(true)).PendingDeliveryCount == 0)
                return;

            // Replica A has read the old checkpoint during catch-up. Replica B drains its
            // journal entry before A reads the updated lifecycle row.
            (await writer.ApplyAsync("tenant-a", "folder-a", "catch-up-b", [growth], token).ConfigureAwait(true)).IsSuccess.ShouldBeTrue();
            competingWriterCompleted = true;
        });
        OperationalStatusProjectionHandler racingHandler = new(
            new(racingStore, store, store, scanner: Hexalith.Folders.Tests.OperationalStatus.OperationalStatusTestData.Scanner));
        ProjectionRequest growthRequest = new("tenant-a", "folders", "folder-a", [growth.Source!]);
        var raced = await racingHandler.ProjectAsync(growthRequest, "catch-up-a", token);
        competingWriterCompleted.ShouldBeTrue();
        raced.Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
        raced.ReasonCode.ShouldBe("operational_store_unavailable");
        (await racingHandler.ProjectAsync(growthRequest, "catch-up-a", token)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await coordinator.GetStatusAsync(scope, token)).PendingDeliveryCount.ShouldBe(0);
        (await coordinator.GetCheckpointAsync(scope, "folder-a", token))!.OwnerSequenceNumber.ShouldBe(count + 1);
        var grownCheckpoint = (await coordinator.ReadAsync<OperationalStatusCheckpoint>(scope,
            OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"), token)).Value!;
        grownCheckpoint.Sequence.ShouldBe(count + 1);
        grownCheckpoint.SourceDigest.ShouldBe(OperationalStatusKeys.Digest(checkpoint.SourceDigest, growth.Digest));
        sources.Add(growth);
        sequence.Head++;
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "grown-rebuild", sources,
            growth.Source!.GlobalPosition, token)).IsSuccess.ShouldBeTrue();
        (await coordinator.GetCheckpointAsync(scope, "folder-a", token))!.CapturedIdentityReference!.Count.ShouldBe(count + 1);
        foreach (int historical in Enumerable.Range(0, 98))
        {
            string taskId = historical == 0 ? "task-a" : "historical-" + historical;
            var result = await reader.GetAsync(new TaskStatusReadModelRequest("tenant-a", taskId, "reader",
                "read_task_status", "corr-a", "eventually_consistent")
            { AuthorizedFolderId = "folder-a", AuthorizationWatermark = "authority" }, token);
            result.Status.ShouldBe(TaskStatusReadModelStatus.Available);
            result.Snapshot!.TerminalState.ShouldBe("ready");
        }
        foreach (int index in new[] { 0, count / 2, count })
            (await writer.ApplyAsync("tenant-a", "folder-a", "grown-duplicate-" + index, [sources[index]], token)).IsSuccess.ShouldBeTrue();
        string before = JsonSerializer.Serialize((await coordinator.ReadAsync<OperationalStatusCheckpoint>(scope,
            OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"), token)).Value);
        // Every conflict is awaited below; preserve selected checkpoint and all source bytes.
        foreach (int index in new[] { 0, count / 2, count })
        {
            foreach (bool message in new[] { true, false })
            {
                var original = sources[index].Source!;
                var changed = OperationalStatusTestData.Stored(message
                    ? original with { MessageId = "conflicting-" + index }
                    : original with
                    {
                        Payload = index == 0
                        ? JsonSerializer.SerializeToUtf8Bytes(OperationalStatusTestData.Created() with { DisplayName = "contradictory" })
                        : Encoding.UTF8.GetBytes("{\"Code\":\"UnsupportedCommandType\",\"CommandType\":\"ChangedUnknownCommand\"}")
                    });
                await Should.ThrowAsync<InvalidOperationException>(() => writer.ApplyAsync("tenant-a", "folder-a", "conflict-" + index, [changed], token));
            }
        }
        JsonSerializer.Serialize((await coordinator.ReadAsync<OperationalStatusCheckpoint>(scope,
            OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"), token)).Value).ShouldBe(before);
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(sources.Take(count).Select(item => item.Source))))
            .ShouldBe(originalHash);
        (await coordinator.GetStatusAsync(scope, token)).IsBuilding.ShouldBeFalse();
        store.BatchOptions.MaxOperations.ShouldBe(100);
        store.BatchOptions.MaxCanonicalManifestBytes.ShouldBe(1_048_576);
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
        var creation = OperationalStatusTestData.Stored(original.Source! with
        { SequenceNumber = 2, GlobalPosition = 2, MessageId = "created-2" });
        OperationalStatusTestStore store = new();
        OperationalStatusProjectionWriter writer = new(store, store, store, scanner: Hexalith.Folders.Tests.OperationalStatus.OperationalStatusTestData.Scanner);
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
        OperationalStatusProjectionWriter writer = new(store, store, store, scanner: Hexalith.Folders.Tests.OperationalStatus.OperationalStatusTestData.Scanner);
        (await writer.ApplyAsync("tenant-a", "folder-a", "locked", source, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        string before = JsonSerializer.Serialize(store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a")));
        var release = OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now).OfType<WorkspaceLockReleased>().Single()
            with
        { TaskId = "other-task", HolderTaskId = "other-task" };
        var invalid = OperationalStatusTestData.Stored(OperationalStatusTestData.Envelope(release, source.Length + 1));
        await Should.ThrowAsync<InvalidOperationException>(() => writer.ApplyAsync("tenant-a", "folder-a", "invalid-holder", [invalid], TestContext.Current.CancellationToken));
        Should.Throw<InvalidOperationException>(() => OperationalStatusTestData.Stored(source[0].Source! with
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
        OperationalStatusProjectionWriter writer = new(store, store, store, scanner: Hexalith.Folders.Tests.OperationalStatus.OperationalStatusTestData.Scanner);
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
        OperationalStatusProjectionWriter writer = new(store, store, store, scanner: Hexalith.Folders.Tests.OperationalStatus.OperationalStatusTestData.Scanner);
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
        => OperationalStatusTestData.Stored(original.Source! with
        {
            SequenceNumber = ordinal,
            GlobalPosition = ordinal,
            MessageId = "rejected-" + ordinal,
            EventTypeName = "Hexalith.Folders.Server.FolderCommandRejected",
            Payload = Encoding.UTF8.GetBytes("{\"Code\":\"UnsupportedCommandType\",\"CommandType\":\"UnknownButLegitimate\"}"),
        });
}
