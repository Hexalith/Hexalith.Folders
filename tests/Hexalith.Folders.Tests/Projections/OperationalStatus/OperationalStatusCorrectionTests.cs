using System.Collections.Frozen;
using System.Text.Json;

using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Projections.OperationalStatus;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Tests.Aggregates.Folder;
using Hexalith.Folders.Tests.OperationalStatus;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Projections.OperationalStatus;

public sealed class OperationalStatusCorrectionTests
{
    [Fact]
    public void CheckpointRejectsAlteredStateEnumsClosuresAndPositionsAfterJsonRoundTrip()
    {
        var sources = OperationalStatusTestData.Sources(OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now));
        var original = OperationalStatusFold.Apply("tenant-a", "folder-a", null, sources).Checkpoint;
        var checkpoint = JsonSerializer.Deserialize<OperationalStatusCheckpoint>(JsonSerializer.Serialize(original))!;
        OperationalStatusFold.Valid(checkpoint, "tenant-a", "folder-a").ShouldBeTrue();
        string binding = checkpoint.State.TaskClosures.Keys.Single();
        var closure = checkpoint.State.TaskClosures[binding];
        OperationalStatusCheckpoint[] damaged =
        [
            checkpoint with { State = checkpoint.State with { LifecycleState = (FolderLifecycleState)999 } },
            checkpoint with { State = checkpoint.State with { WorkspaceLifecycleState = (FolderWorkspaceLifecycleState)999 } },
            checkpoint with { State = checkpoint.State with { WorkspaceCommitReference = "fabricated" } },
            checkpoint with { TaskClosedSequences = new Dictionary<string, long> { [binding] = 0 } },
            checkpoint with { TaskClosedSequences = new Dictionary<string, long> { [binding] = checkpoint.Sequence + 1 } },
            checkpoint with { TaskClosedSequences = new Dictionary<string, long>() },
            checkpoint with { State = checkpoint.State with { TaskClosures = new Dictionary<string, FolderTaskClosure>
                { [binding] = closure with { TerminalState = "unknown_provider_outcome" } }.ToFrozenDictionary() } },
            checkpoint with { State = checkpoint.State with { TaskClosures = new Dictionary<string, FolderTaskClosure>
                { ["wrong-binding"] = closure }.ToFrozenDictionary() } },
        ];
        foreach (var value in damaged)
        {
            OperationalStatusFold.Valid(value, "tenant-a", "folder-a").ShouldBeFalse();
            Should.Throw<InvalidOperationException>(() => OperationalStatusFold.Apply("tenant-a", "folder-a", value, []));
        }
    }

    [Fact]
    public async Task RebuildRepairsEveryDamagedTerminalFieldAndRestoresAvailableResult()
    {
        OperationalStatusTestStore store = new();
        var writer = new OperationalStatusProjectionWriter(store, store, store);
        var sources = OperationalStatusTestData.Sources(OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now));
        (await writer.ApplyAsync("tenant-a", "folder-a", "initial", sources, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        string key = OperationalStatusKeys.Task("tenant-a", "folder-a", "task-a");
        var original = store.Snapshot<TaskStatusReadModelSnapshot>("statestore", key)!;
        var reader = Reader(store, sources.Length);
        TaskStatusReadModelSnapshot[] damaged =
        [
            original with { CurrentState = "dirty" },
            original with { LastOperationId = "changed" },
            original with { LastFailureCategory = "fabricated_failure" },
            original with { RetryEligibility = new(true, "dirty_workspace") },
            original with { Freshness = original.Freshness with { ObservedAt = original.Freshness.ObservedAt.AddSeconds(-1) } },
            original with { EvidenceScope = original.EvidenceScope with { CorrelationId = "altered" } },
        ];
        int repair = 0;
        foreach (var value in damaged)
        {
            store.SeedRaw("statestore", key, value);
            (await reader.GetAsync(TaskRequest(), TestContext.Current.CancellationToken)).Status.ShouldBe(TaskStatusReadModelStatus.Unavailable);
            (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "repair-" + repair++, sources, sources.Length,
                TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
            JsonSerializer.Serialize(store.Snapshot<TaskStatusReadModelSnapshot>("statestore", key)).ShouldBe(JsonSerializer.Serialize(original));
            (await reader.GetAsync(TaskRequest(), TestContext.Current.CancellationToken)).Status.ShouldBe(TaskStatusReadModelStatus.Available);
        }
    }

    [Fact]
    public async Task OldDirtyTaskReadCannotAdvertiseRetryWhenCheckpointAdvancesToUnknownOutcome()
    {
        OperationalStatusTestStore store = new();
        var writer = new OperationalStatusProjectionWriter(store, store, store);
        var history = FolderLifecycleReplayFixture.CommitUnknownOutcomeLifecycle(false);
        int stagedIndex = history.ToList().FindIndex(static value => value is WorkspaceFileMutationAccepted);
        var staged = history.Take(stagedIndex + 1).ToList();
        var last = staged.Last();
        staged.Add(new FolderWorkspaceLifecycleEventRecorded("tenant-a", "tenant-a", "folder-a", "workspace-a",
            FolderWorkspaceLifecycleEvent.LockLeaseExpired, null, "expiry", "correlation-a", "task-a", "expiry-key", "expiry-fp", last.OccurredAt));
        int dirtyCount = staged.Count;
        var expired = (FolderWorkspaceLifecycleEventRecorded)staged.Last();
        staged.Add(expired with { WorkspaceLifecycleEvent = FolderWorkspaceLifecycleEvent.ReconciliationRequested });
        staged.Add(expired with { WorkspaceLifecycleEvent = FolderWorkspaceLifecycleEvent.ReconciliationCompletedClean });
        staged.Add(history.OfType<WorkspaceLockAcquired>().Single() with { LockId = "new-lock" });
        staged.Add(history.OfType<WorkspaceFileMutationAccepted>().First());
        staged.Add(history.OfType<WorkspaceCommitOutcomeUnknown>().Single());
        var source = OperationalStatusTestData.Sources(staged);
        (await writer.ApplyAsync("tenant-a", "folder-a", "dirty", source[..dirtyCount], TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        string key = OperationalStatusKeys.Task("tenant-a", "folder-a", "task-a");
        var old = store.Snapshot<TaskStatusReadModelSnapshot>("statestore", key)!;
        old.CurrentState.ShouldBe("dirty");
        old.RetryEligibility.Eligible.ShouldBeTrue();
        bool advanced = false;
        OperationalStatusReadObserver racing = new(store, async readKey =>
        {
            if (readKey != store.PhysicalKey(key) || advanced) return;
            advanced = true;
            (await writer.ApplyAsync("tenant-a", "folder-a", "unknown", source[dirtyCount..], TestContext.Current.CancellationToken)
                .ConfigureAwait(false)).IsSuccess.ShouldBeTrue();
        });
        var reader = new EventStoreOperationalStatusReadModel(new(racing, store), new OperationalStatusClock(),
            new OperationalStatusSequenceReader { Head = source.Length });
        var result = await reader.GetAsync(TaskRequest(), TestContext.Current.CancellationToken);
        advanced.ShouldBeTrue();
        if (result.Status == TaskStatusReadModelStatus.Available)
        {
            result.Snapshot!.CurrentState.ShouldBe("unknown_provider_outcome");
            result.Snapshot.RetryEligibility.Eligible.ShouldBeFalse();
        }
        else result.Status.ShouldBe(TaskStatusReadModelStatus.Unavailable);
    }

    [Fact]
    public async Task EarliestTaskClosureSurvivesEverySliceJsonRestartAndLaterBindingClosure()
    {
        var history = RepeatedTaskHistory();
        var sources = OperationalStatusTestData.Sources(history);
        var complete = OperationalStatusFold.Apply("tenant-a", "folder-a", null, sources);
        var expected = complete.Tasks["task-a"];
        expected.TerminalState.ShouldBe("ready");
        expected.LastOperationId.ShouldBe("lock-a");
        complete.Checkpoint.State.TaskClosures.Count.ShouldBe(2);
        complete.Checkpoint.State.IsTaskClosed("workspace-a", "task-a").ShouldBeTrue();
        complete.Checkpoint.State.IsTaskClosed("workspace-b", "task-a").ShouldBeTrue();
        var reordered = complete.Checkpoint with
        {
            State = complete.Checkpoint.State with
            {
                TaskClosures = complete.Checkpoint.State.TaskClosures.OrderByDescending(item => complete.Checkpoint.TaskClosedSequences[item.Key])
                    .ToDictionary(static item => item.Key, static item => item.Value, StringComparer.Ordinal),
            },
        };
        OperationalStatusFold.Valid(reordered, "tenant-a", "folder-a").ShouldBeTrue();
        var release = OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now).OfType<WorkspaceLockReleased>().Single()
            with
        { WorkspaceId = "workspace-b", LockId = "lock-b", OccurredAt = OperationalStatusTestData.Now.AddSeconds(5) };
        var reorderedTail = OperationalStatusFold.Apply("tenant-a", "folder-a", reordered,
            [OperationalStatusEventReader.ReadStored(OperationalStatusTestData.Envelope(release, sources.Length + 1))]);
        reorderedTail.Tasks["task-a"].LastOperationId.ShouldBe("lock-a");
        for (int split = 1; split < sources.Length; split++)
        {
            var prefix = OperationalStatusFold.Apply("tenant-a", "folder-a", null, sources[..split]);
            var checkpoint = JsonSerializer.Deserialize<OperationalStatusCheckpoint>(JsonSerializer.Serialize(prefix.Checkpoint));
            var tail = OperationalStatusFold.Apply("tenant-a", "folder-a", checkpoint, sources[split..]);
            JsonSerializer.Serialize(tail.Tasks["task-a"]).ShouldBe(JsonSerializer.Serialize(expected));
            tail.Checkpoint.IntegrityDigest.ShouldBe(complete.Checkpoint.IntegrityDigest);
        }
        OperationalStatusTestStore store = new();
        foreach (var item in sources)
            (await new OperationalStatusProjectionWriter(store, store, store).ApplyAsync("tenant-a", "folder-a", "live-" + item.Sequence,
                [item], TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        (await Reader(store, sources.Length).GetAsync(TaskRequest(), TestContext.Current.CancellationToken)).Snapshot!.LastOperationId.ShouldBe("lock-a");
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "rebuild", sources,
            sources.Length, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        store.Snapshot<TaskStatusReadModelSnapshot>("statestore", OperationalStatusKeys.Task("tenant-a", "folder-a", "task-a"))!.LastOperationId.ShouldBe("lock-a");
    }

    [Fact]
    public async Task AlteredHistoricalWorkspaceFailsAllConcernReadsAndRebuildRestoresIt()
    {
        OperationalStatusTestStore store = new();
        var source = OperationalStatusTestData.Sources(RepeatedTaskHistory());
        var writer = new OperationalStatusProjectionWriter(store, store, store);
        (await writer.ApplyAsync("tenant-a", "folder-a", "delivery", source, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        string key = OperationalStatusKeys.Workspace("tenant-a", "folder-a", "workspace-a");
        var original = store.Snapshot<OperationalWorkspaceStatus>("statestore", key)!;
        OperationalWorkspaceStatus[] damaged =
        [
            original with { Workspace = original.Workspace with { CurrentState = "dirty" } },
            original with { Lock = original.Lock with { LockId = "fabricated" } },
            original with { Cleanup = original.Cleanup with { Status = "completed" } },
            original with { Workspace = original.Workspace with { ProviderOutcome = original.Workspace.ProviderOutcome with { State = "known_success" } } },
        ];
        var reader = Reader(store, source.Length);
        foreach (var value in damaged)
        {
            store.SeedRaw("statestore", key, value);
            (await reader.GetAsync(new WorkspaceStatusReadModelRequest("tenant-a", "folder-a", "workspace-a", "reader", "read_workspace_status",
                "task-a", "corr", "authority", "read_your_writes"), TestContext.Current.CancellationToken)).Status.ShouldBe(WorkspaceStatusReadModelStatus.Unavailable);
            (await reader.GetAsync(new WorkspaceLockStatusReadModelRequest("tenant-a", "folder-a", "workspace-a", "reader", "read_workspace_lock",
                "task-a", "corr", "authority", "read_your_writes"), TestContext.Current.CancellationToken)).Status.ShouldBe(WorkspaceLockStatusReadModelStatus.Unavailable);
            (await reader.GetAsync(new WorkspaceCleanupStatusReadModelRequest("tenant-a", "folder-a", "workspace-a", "reader", "read_workspace_cleanup_status",
                "task-a", "corr", "authority", "read_your_writes"), TestContext.Current.CancellationToken)).Status.ShouldBe(WorkspaceCleanupStatusReadModelStatus.Unavailable);
        }
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "repair", source, source.Length, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        JsonSerializer.Serialize(store.Snapshot<OperationalWorkspaceStatus>("statestore", key)).ShouldBe(JsonSerializer.Serialize(original));
    }

    [Fact]
    public void NewWorkspaceCannotInheritPreviousWorkspaceProviderSuccess()
    {
        var history = OperationalStatusFixture.SuccessfulLifecycle().ToList();
        var prep = history.OfType<WorkspacePreparationRequested>().Single();
        history.Add(prep with { WorkspaceId = "new-workspace", TaskId = "new-task", OccurredAt = history.Last().OccurredAt.AddSeconds(1) });
        var result = OperationalStatusFold.Apply("tenant-a", "folder-a", null, OperationalStatusTestData.Sources(history));
        result.Workspaces["workspace-a"].Workspace.ProviderOutcome.State.ShouldBe("known_success");
        result.Workspaces["new-workspace"].Workspace.ProviderOutcome.State.ShouldBe("pending");
        result.Checkpoint.State.WorkspaceCommitReference.ShouldBeNull();
        result.Tasks["task-a"].TerminalState.ShouldBe("committed");
        result.Tasks["new-task"].TerminalState.ShouldBeNull();
    }

    [Fact]
    public async Task EmptyCheckpointRebuildRejectsTruncatedSourceBeforeAnyWrite()
    {
        OperationalStatusTestStore store = new();
        var source = OperationalStatusTestData.Sources(OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now));
        await Should.ThrowAsync<InvalidOperationException>(() => new OperationalStatusProjectionWriter(store, store, store)
            .PrepareRebuildAsync("tenant-a", "folder-a", source[..^1], source.Length, TestContext.Current.CancellationToken));
        store.Count.ShouldBe(0);
    }

    private static IReadOnlyList<IFolderEvent> RepeatedTaskHistory()
    {
        var history = OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now).ToList();
        var prep = history.OfType<WorkspacePreparationRequested>().Single();
        var locked = history.OfType<WorkspaceLockAcquired>().Single();
        history.Add(prep with { WorkspaceId = "workspace-b", OccurredAt = OperationalStatusTestData.Now.AddSeconds(1) });
        history.Add(locked with
        {
            WorkspaceId = "workspace-b",
            LockId = "lock-b",
            OccurredAt = OperationalStatusTestData.Now.AddSeconds(2),
            AcquiredAt = OperationalStatusTestData.Now.AddSeconds(2),
            EffectiveAt = OperationalStatusTestData.Now.AddSeconds(2),
            ExpiresAt = OperationalStatusTestData.Now.AddSeconds(2).AddHours(1)
        });
        var committed = OperationalStatusFixture.SuccessfulLifecycle();
        history.Add(committed.OfType<WorkspaceFileMutationAccepted>().First() with { WorkspaceId = "workspace-b", OccurredAt = OperationalStatusTestData.Now.AddSeconds(3) });
        history.Add(committed.OfType<WorkspaceCommitSucceeded>().Single() with { WorkspaceId = "workspace-b", OccurredAt = OperationalStatusTestData.Now.AddSeconds(4) });
        return history;
    }

    private static EventStoreOperationalStatusReadModel Reader(OperationalStatusTestStore store, long head)
        => new(new(store, store), new OperationalStatusClock { UtcNow = OperationalStatusTestData.Now.AddHours(1) }, new OperationalStatusSequenceReader { Head = head });
    private static TaskStatusReadModelRequest TaskRequest()
        => new("tenant-a", "task-a", "reader", "read_task_status", "corr", "eventually_consistent")
        { AuthorizedFolderId = "folder-a", AuthorizationWatermark = "authority" };
}
