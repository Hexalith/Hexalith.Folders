using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Folders.Projections.OperationalStatus;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Tests.Aggregates.Folder;
using Hexalith.Folders.Tests.OperationalStatus;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Projections.OperationalStatus;

#pragma warning disable xUnit1030 // Protocol fault-hook continuation follows library conventions.

public sealed class OperationalStatusPersistenceTests
{
    [Theory]
    [InlineData(ReadModelBatchPhase.AfterInstallOperation, 0)]
    [InlineData(ReadModelBatchPhase.AfterInstallOperation, 1)]
    [InlineData(ReadModelBatchPhase.AfterCommit, -1)]
    [InlineData(ReadModelBatchPhase.BeforeCompaction, 1)]
    [InlineData(ReadModelBatchPhase.BeforeReceipt, -1)]
    public async Task InterruptedBatchRecoversItsOriginalManifestBeforeAcknowledgment(ReadModelBatchPhase interruption, int interruptedOrdinal)
    {
        OperationalStatusTestStore store = new();
        store.BatchFaultHook = (phase, ordinal, _) =>
            phase == interruption && ordinal == interruptedOrdinal
                || phase == ReadModelBatchPhase.Reconcile ? throw new IOException("Injected interruption") : Task.CompletedTask;
        OperationalStatusProjectionWriter first = new(store, store, store);
        OperationalStatusEvent[] events = OperationalStatusTestData.Sources([OperationalStatusTestData.Created()]);
        ReadModelBatchResult interrupted = await first.ApplyAsync("tenant-a", "folder-a", "delivery-a", events, TestContext.Current.CancellationToken);
        interrupted.IsSuccess.ShouldBeFalse();
        var checkpoint = await store.GetAsync<OperationalStatusCheckpoint>(OperationalStatusKeys.StoreName,
            OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"), TestContext.Current.CancellationToken);
        if (interruption == ReadModelBatchPhase.AfterInstallOperation) checkpoint.Value.ShouldBeNull();
        else if (interruption == ReadModelBatchPhase.BeforeReceipt) checkpoint.Value!.Sequence.ShouldBe(1);
        else checkpoint.ETag.ShouldBeNullOrEmpty();
        var reader = new EventStoreOperationalStatusReadModel(new(store, store), new OperationalStatusClock(), new OperationalStatusSequenceReader());
        var during = await reader.GetAsync(LifecycleRequest(), TestContext.Current.CancellationToken);
        during.Status.ShouldNotBe(FolderLifecycleStatusReadModelStatus.Available);
        store.BatchFaultHook = null;
        OperationalStatusProjectionWriter restarted = new(store, store, store);
        (await restarted.ApplyAsync("tenant-a", "folder-a", "delivery-a", events, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        checkpoint = await store.GetAsync<OperationalStatusCheckpoint>(OperationalStatusKeys.StoreName,
            OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"), TestContext.Current.CancellationToken);
        checkpoint.Value!.Sequence.ShouldBe(1);
        checkpoint.ETag.ShouldNotBeNullOrEmpty();
        store.HasPendingEnvelope(OperationalStatusKeys.StoreName, OperationalStatusKeys.Checkpoint("tenant-a", "folder-a")).ShouldBeFalse();
        (await reader.GetAsync(LifecycleRequest(), TestContext.Current.CancellationToken)).Status.ShouldBe(FolderLifecycleStatusReadModelStatus.Available);
    }

    [Fact]
    public async Task IndependentWritersForTheSameDeliveryConvergeWithoutDoubleAdvance()
    {
        OperationalStatusTestStore store = new();
        var source = OperationalStatusTestData.Sources([OperationalStatusTestData.Created()]);
        var outcomes = await Task.WhenAll(new OperationalStatusProjectionWriter(store, store, store).ApplyAsync("tenant-a", "folder-a", "replica-a", source, TestContext.Current.CancellationToken),
            new OperationalStatusProjectionWriter(store, store, store).ApplyAsync("tenant-a", "folder-a", "replica-a", source, TestContext.Current.CancellationToken));
        outcomes.ShouldContain(static result => result.IsSuccess);
        store.BatchFaultHook = null;
        (await new OperationalStatusProjectionWriter(store, store, store).ApplyAsync("tenant-a", "folder-a", "replica-retry", source, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        store.Snapshot<OperationalStatusCheckpoint>(OperationalStatusKeys.StoreName, OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"))!
            .Sequence.ShouldBe(1);
    }

    [Fact]
    public async Task DerivedViewLossRequiresCompleteSourceRebuildWithoutErasingItsCompletedReceipt()
    {
        OperationalStatusTestStore store = new();
        ObservedOperationalBatchStore batches = new(store);
        var writer = new OperationalStatusProjectionWriter(store, batches, store);
        var source = OperationalStatusTestData.Sources([OperationalStatusTestData.Created()]);
        (await writer.ApplyAsync("tenant-a", "folder-a", "delivery-a", source, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        ReadModelBatch original = batches.Batches.Single();
        string receiptKey = original.Scope.ComputeMarkerKey();
        string originalReceipt = System.Text.Json.JsonSerializer.Serialize(store.Snapshot<System.Text.Json.Nodes.JsonObject>("statestore", receiptKey));
        foreach (string key in new[] { OperationalStatusKeys.Lifecycle("tenant-a", "folder-a"), OperationalStatusKeys.Checkpoint("tenant-a", "folder-a") })
        {
            var row = await store.TryReadEtagAsync("statestore", key, TestContext.Current.CancellationToken);
            (await store.TryEraseAsync("statestore", key, row.Etag, TestContext.Current.CancellationToken)).ShouldBeTrue();
        }

        (await writer.ApplyAsync("tenant-a", "folder-a", "delivery-a", source, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeFalse();
        (await OperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "restore-source", source, 1,
            TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        System.Text.Json.JsonSerializer.Serialize(store.Snapshot<System.Text.Json.Nodes.JsonObject>("statestore", receiptKey)).ShouldBe(originalReceipt);
        store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"))!.Sequence.ShouldBe(1);
        (await writer.ApplyAsync("tenant-a", "folder-a", "delivery-a", source, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        batches.Batches.Count.ShouldBe(1);
    }

    [Fact]
    public async Task DuplicateDeliveryCannotAcknowledgeMissingCurrentWorkspaceOrCorruptTaskWithLifecycleIntact()
    {
        OperationalStatusTestStore store = new();
        var source = OperationalStatusTestData.Sources(OperationalStatusFixture.SuccessfulLifecycle());
        var writer = new OperationalStatusProjectionWriter(store, store, store);
        (await writer.ApplyAsync("tenant-a", "folder-a", "delivery", source, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        string workspaceKey = OperationalStatusKeys.Workspace("tenant-a", "folder-a", "workspace-a");
        string taskKey = OperationalStatusKeys.Task("tenant-a", "folder-a", "task-a");
        var workspace = store.Snapshot<OperationalWorkspaceStatus>("statestore", workspaceKey)!;
        var task = store.Snapshot<TaskStatusReadModelSnapshot>("statestore", taskKey)!;
        var row = await store.TryReadEtagAsync("statestore", workspaceKey, TestContext.Current.CancellationToken);
        (await store.TryEraseAsync("statestore", workspaceKey, row.Etag, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await writer.ApplyAsync("tenant-a", "folder-a", "missing-workspace", [source[^1]], TestContext.Current.CancellationToken)).IsSuccess.ShouldBeFalse();
        store.SeedRaw("statestore", workspaceKey, workspace);
        store.SeedRaw("statestore", taskKey, task with { TerminalState = "ready" });
        await Should.ThrowAsync<OperationalStatusUnavailableException>(() => writer.ApplyAsync("tenant-a", "folder-a", "corrupt-task", [source[^1]], TestContext.Current.CancellationToken));
        store.Snapshot<FolderLifecycleStatusReadModelSnapshot>("statestore", OperationalStatusKeys.Lifecycle("tenant-a", "folder-a")).ShouldNotBeNull();
    }

    [Fact]
    public async Task PersistedTaskResultSurvivesNewReaderAndEmptyCheckpointRebuild()
    {
        OperationalStatusTestStore store = new();
        var history = OperationalStatusFixture.SuccessfulLifecycle();
        var source = OperationalStatusTestData.Sources(history);
        var writer = new OperationalStatusProjectionWriter(store, store, store);
        (await writer.ApplyAsync("tenant-a", "folder-a", "live", source, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var task = store.Snapshot<TaskStatusReadModelSnapshot>(OperationalStatusKeys.StoreName, OperationalStatusKeys.Task("tenant-a", "folder-a", "task-a"));
        task!.TerminalState.ShouldBe("committed");
        var workspace = store.Snapshot<OperationalWorkspaceStatus>(OperationalStatusKeys.StoreName,
            OperationalStatusKeys.Workspace("tenant-a", "folder-a", "workspace-a"));
        workspace!.Cleanup.Status.ShouldBe("status_only");
        var reader = new EventStoreOperationalStatusReadModel(new(store, store), new OperationalStatusClock(), new OperationalStatusSequenceReader { Head = history.Count });
        var result = await reader.GetAsync(new TaskStatusReadModelRequest("tenant-a", "task-a", "reader-b", "read_task_status", "request-correlation", "eventually_consistent")
        { AuthorizedFolderId = "folder-a", AuthorizationWatermark = "authority-2" }, TestContext.Current.CancellationToken);
        result.Snapshot!.TerminalState.ShouldBe("committed");
        result.Snapshot.EvidenceScope.PrincipalId.ShouldBe("reader-b");
        result.Snapshot.EvidenceScope.CorrelationId.ShouldBe("request-correlation");

        OperationalStatusTestStore empty = new();
        var operations = await new OperationalStatusProjectionWriter(empty, empty, empty).PrepareRebuildAsync("tenant-a", "folder-a", source, source.Length, TestContext.Current.CancellationToken);
        operations.Where(static operation => operation.TimeToLive is not null).ShouldAllBe(static operation => operation.TimeToLive == OperationalStatusKeys.Retention);
        var rebuilt = await OperationalStatusRebuildFixture.RebuildAsync(empty, "tenant-a", "folder-a", "rebuild", source, source.Length, TestContext.Current.CancellationToken);
        rebuilt.IsSuccess.ShouldBeTrue();
        JsonSerializer.Serialize(empty.Snapshot<TaskStatusReadModelSnapshot>(OperationalStatusKeys.StoreName, OperationalStatusKeys.Task("tenant-a", "folder-a", "task-a")))
            .ShouldBe(JsonSerializer.Serialize(task));
    }

    [Fact]
    public async Task CleanupReadsRebindAuthorizedRequestTracingWithoutChangingRetainedSourceContext()
    {
        OperationalStatusTestStore store = new();
        var source = OperationalStatusTestData.Sources(OperationalStatusFixture.SuccessfulLifecycle());
        (await new OperationalStatusProjectionWriter(store, store, store).ApplyAsync("tenant-a", "folder-a", "delivery", source,
            TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var reader = new EventStoreOperationalStatusReadModel(new(store, store), new OperationalStatusClock(),
            new OperationalStatusSequenceReader { Head = source.Length });
        var request = new WorkspaceCleanupStatusReadModelRequest("tenant-a", "folder-a", "workspace-a", "reader-b",
            "read_workspace_cleanup_status", TaskId: null, "read-correlation", "new-authority", "read_your_writes");

        var result = await reader.GetAsync(request, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(WorkspaceCleanupStatusReadModelStatus.Available);
        result.Snapshot!.TaskId.ShouldBeNull();
        result.Snapshot.CorrelationId.ShouldBe("read-correlation");
        result.Snapshot.EvidenceScope.TaskId.ShouldBeNull();
        result.Snapshot.EvidenceScope.CorrelationId.ShouldBe("read-correlation");
        (await reader.GetAsync(request with { TaskId = "foreign-task" }, TestContext.Current.CancellationToken))
            .Status.ShouldBe(WorkspaceCleanupStatusReadModelStatus.Unavailable);
        var retained = store.Snapshot<OperationalWorkspaceStatus>("statestore", OperationalStatusKeys.Workspace("tenant-a", "folder-a", "workspace-a"))!;
        retained.Cleanup.TaskId.ShouldBe("task-a");
        retained.Cleanup.CorrelationId.ShouldNotBe("read-correlation");
    }

    [Fact]
    public async Task MissingKnownWorkspaceAndTaskViewsFailClosedUntilRebuilt()
    {
        OperationalStatusTestStore store = new();
        var source = OperationalStatusTestData.Sources(OperationalStatusFixture.SuccessfulLifecycle());
        var writer = new OperationalStatusProjectionWriter(store, store, store);
        (await writer.ApplyAsync("tenant-a", "folder-a", "delivery", source, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        foreach (string key in new[] { OperationalStatusKeys.Workspace("tenant-a", "folder-a", "workspace-a"),
            OperationalStatusKeys.Task("tenant-a", "folder-a", "task-a") })
        {
            var row = await store.TryReadEtagAsync("statestore", key, TestContext.Current.CancellationToken);
            (await store.TryEraseAsync("statestore", key, row.Etag, TestContext.Current.CancellationToken)).ShouldBeTrue();
        }
        var reader = new EventStoreOperationalStatusReadModel(new(store, store), new OperationalStatusClock(),
            new OperationalStatusSequenceReader { Head = source.Length });
        var task = new TaskStatusReadModelRequest("tenant-a", "task-a", "reader", "read_task_status", null, "eventually_consistent")
        { AuthorizedFolderId = "folder-a", AuthorizationWatermark = "authority-2" };
        var workspace = new WorkspaceStatusReadModelRequest("tenant-a", "folder-a", "workspace-a", "reader",
            "read_workspace_status", "task-a", null, "authority-2", "read_your_writes");
        (await reader.GetAsync(task, TestContext.Current.CancellationToken)).Status.ShouldBe(TaskStatusReadModelStatus.Unavailable);
        (await reader.GetAsync(workspace, TestContext.Current.CancellationToken)).Status.ShouldBe(WorkspaceStatusReadModelStatus.Unavailable);
        (await reader.GetAsync(task with { TaskId = "absent-task" }, TestContext.Current.CancellationToken)).Status.ShouldBe(TaskStatusReadModelStatus.NotFound);
        (await reader.GetAsync(workspace with { WorkspaceId = "absent-workspace" }, TestContext.Current.CancellationToken)).Status.ShouldBe(WorkspaceStatusReadModelStatus.NotFound);
        (await OperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "repair", source, source.Length, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        (await reader.GetAsync(task, TestContext.Current.CancellationToken)).Snapshot!.TerminalState.ShouldBe("committed");
        (await reader.GetAsync(workspace, TestContext.Current.CancellationToken)).Status.ShouldBe(WorkspaceStatusReadModelStatus.Available);
    }

    [Fact]
    public async Task OldValidLifecycleCannotBePairedWithAnArchivedCurrentCheckpoint()
    {
        OperationalStatusTestStore store = new();
        var writer = new OperationalStatusProjectionWriter(store, store, store);
        var source = OperationalStatusTestData.Sources([OperationalStatusTestData.Created(),
            new Hexalith.Folders.Aggregates.Folder.FolderArchived("tenant-a", "tenant-a", "folder-a",
                Hexalith.Folders.Aggregates.Folder.FolderArchiveReasonCode.CallerRequested, "principal-a", "correlation-a",
                "task-a", "archive-key", "archive-fingerprint", OperationalStatusTestData.Now)]);
        (await writer.ApplyAsync("tenant-a", "folder-a", "created", [source[0]], TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        string key = OperationalStatusKeys.Lifecycle("tenant-a", "folder-a");
        var old = store.Snapshot<FolderLifecycleStatusReadModelSnapshot>("statestore", key)!;
        (await writer.ApplyAsync("tenant-a", "folder-a", "archived", [source[1]], TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var current = await store.GetAsync<FolderLifecycleStatusReadModelSnapshot>("statestore", key, TestContext.Current.CancellationToken);
        (await store.TrySaveAsync("statestore", key, old, current.ETag!, TestContext.Current.CancellationToken)).ShouldBeTrue();
        var reader = new EventStoreOperationalStatusReadModel(new(store, store), new OperationalStatusClock(), new OperationalStatusSequenceReader { Head = 2 });

        (await reader.GetAsync(LifecycleRequest(), TestContext.Current.CancellationToken)).Status.ShouldBe(FolderLifecycleStatusReadModelStatus.Unavailable);
        var staleRow = await store.GetAsync<FolderLifecycleStatusReadModelSnapshot>("statestore", key, TestContext.Current.CancellationToken);
        (await store.TrySaveAsync("statestore", key, old with { Freshness = current.Value!.Freshness }, staleRow.ETag!,
            TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await reader.GetAsync(LifecycleRequest(), TestContext.Current.CancellationToken)).Status.ShouldBe(FolderLifecycleStatusReadModelStatus.Unavailable);

        (await OperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "repair", source, source.Length, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        (await reader.GetAsync(LifecycleRequest(), TestContext.Current.CancellationToken)).Snapshot!.LifecycleState.ShouldBe(FolderLifecycleProjectionState.Archived);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WorkspaceWrapperMustMatchItsSnapshotWatermarksAndRetainedPrefixDigest(bool corruptSequence)
    {
        OperationalStatusTestStore store = new();
        var source = OperationalStatusTestData.Sources(OperationalStatusFixture.SuccessfulLifecycle());
        (await new OperationalStatusProjectionWriter(store, store, store).ApplyAsync("tenant-a", "folder-a", "delivery", source,
            TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        string key = OperationalStatusKeys.Workspace("tenant-a", "folder-a", "workspace-a");
        var row = await store.GetAsync<OperationalWorkspaceStatus>("statestore", key, TestContext.Current.CancellationToken);
        OperationalWorkspaceStatus corrupt = corruptSequence ? row.Value! with { Sequence = row.Value!.Sequence - 1 }
            : row.Value! with { SourceDigest = new string('0', 64) };
        (await store.TrySaveAsync("statestore", key, corrupt, row.ETag!, TestContext.Current.CancellationToken)).ShouldBeTrue();
        var reader = new EventStoreOperationalStatusReadModel(new(store, store), new OperationalStatusClock(), new OperationalStatusSequenceReader { Head = source.Length });

        (await reader.GetAsync(new WorkspaceLockStatusReadModelRequest("tenant-a", "folder-a", "workspace-a", "reader",
            "read_workspace_lock", "task-a", "correlation", "authority", "read_your_writes"), TestContext.Current.CancellationToken)).Status.ShouldBe(WorkspaceLockStatusReadModelStatus.Unavailable);
        (await reader.GetAsync(new WorkspaceStatusReadModelRequest("tenant-a", "folder-a", "workspace-a", "reader",
            "read_workspace_status", "task-a", "correlation", "authority", "read_your_writes"), TestContext.Current.CancellationToken)).Status.ShouldBe(WorkspaceStatusReadModelStatus.Unavailable);
        (await reader.GetAsync(new WorkspaceCleanupStatusReadModelRequest("tenant-a", "folder-a", "workspace-a", "reader",
            "read_workspace_cleanup_status", "task-a", "correlation", "authority", "read_your_writes"), TestContext.Current.CancellationToken)).Status.ShouldBe(WorkspaceCleanupStatusReadModelStatus.Unavailable);
    }

    [Fact]
    public async Task RejectionMayAdvanceCheckpointWithoutRewritingHistoricalWorkspaceOrTerminalTask()
    {
        OperationalStatusTestStore store = new();
        var source = OperationalStatusTestData.Sources(OperationalStatusFixture.SuccessfulLifecycle());
        var writer = new OperationalStatusProjectionWriter(store, store, store);
        (await writer.ApplyAsync("tenant-a", "folder-a", "delivery", source, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var workspace = store.Snapshot<OperationalWorkspaceStatus>("statestore", OperationalStatusKeys.Workspace("tenant-a", "folder-a", "workspace-a"));
        var terminal = store.Snapshot<TaskStatusReadModelSnapshot>("statestore", OperationalStatusKeys.Task("tenant-a", "folder-a", "task-a"));
        OperationalStatusEvent rejection = OperationalStatusEventReader.ReadStored(source[0].Source! with
        {
            SequenceNumber = source.Length + 1,
            GlobalPosition = source.Length + 1,
            MessageId = "rejection",
            EventTypeName = "Hexalith.Folders.Server.FolderCommandRejected",
            Payload = System.Text.Encoding.UTF8.GetBytes("{\"Code\":\"StateTransitionInvalid\",\"CommandType\":\"LockWorkspace\"}"),
        });
        (await writer.ApplyAsync("tenant-a", "folder-a", "rejected", [rejection], TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var reader = new EventStoreOperationalStatusReadModel(new(store, store), new OperationalStatusClock(), new OperationalStatusSequenceReader { Head = rejection.Sequence });

        (await reader.GetAsync(new WorkspaceStatusReadModelRequest("tenant-a", "folder-a", "workspace-a", "reader",
            "read_workspace_status", "task-a", "correlation", "authority", "read_your_writes"), TestContext.Current.CancellationToken)).Status.ShouldBe(WorkspaceStatusReadModelStatus.Available);
        (await reader.GetAsync(new TaskStatusReadModelRequest("tenant-a", "task-a", "reader", "read_task_status", "correlation", "read_your_writes")
        { AuthorizedFolderId = "folder-a", AuthorizationWatermark = "authority" }, TestContext.Current.CancellationToken)).Snapshot!.TerminalState.ShouldBe("committed");
        JsonSerializer.Serialize(store.Snapshot<OperationalWorkspaceStatus>("statestore", OperationalStatusKeys.Workspace("tenant-a", "folder-a", "workspace-a"))).ShouldBe(JsonSerializer.Serialize(workspace));
        JsonSerializer.Serialize(store.Snapshot<TaskStatusReadModelSnapshot>("statestore", OperationalStatusKeys.Task("tenant-a", "folder-a", "task-a"))).ShouldBe(JsonSerializer.Serialize(terminal));
    }

    [Fact]
    public async Task OldCoherentWorkspacePrefixCannotConcealTheCurrentSourceLock()
    {
        OperationalStatusTestStore store = new();
        var source = OperationalStatusTestData.Sources(OperationalStatusTestData.LockedPrefix(OperationalStatusTestData.Now));
        var writer = new OperationalStatusProjectionWriter(store, store, store);
        (await writer.ApplyAsync("tenant-a", "folder-a", "ready", source[..^1], TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        string key = OperationalStatusKeys.Workspace("tenant-a", "folder-a", "workspace-a");
        var ready = store.Snapshot<OperationalWorkspaceStatus>("statestore", key)!;
        ready.Workspace.CurrentState.ShouldBe("ready");
        (await writer.ApplyAsync("tenant-a", "folder-a", "locked", [source[^1]], TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var current = await store.GetAsync<OperationalWorkspaceStatus>("statestore", key, TestContext.Current.CancellationToken);
        (await store.TrySaveAsync("statestore", key, ready, current.ETag!, TestContext.Current.CancellationToken)).ShouldBeTrue();
        var reader = new EventStoreOperationalStatusReadModel(new(store, store), new OperationalStatusClock(), new OperationalStatusSequenceReader { Head = source.Length });
        var request = new WorkspaceStatusReadModelRequest("tenant-a", "folder-a", "workspace-a", "reader", "read_workspace_status",
            "task-a", "correlation", "authority", "read_your_writes");

        (await reader.GetAsync(request, TestContext.Current.CancellationToken)).Status.ShouldBe(WorkspaceStatusReadModelStatus.Unavailable);

        (await OperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "repair", source, source.Length, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        (await reader.GetAsync(request, TestContext.Current.CancellationToken)).Snapshot!.CurrentState.ShouldBe("locked");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TerminalTaskMustAgreeWithItsOriginalSourceClosure(bool corruptState)
    {
        OperationalStatusTestStore store = new();
        var source = OperationalStatusTestData.Sources(OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now));
        (await new OperationalStatusProjectionWriter(store, store, store).ApplyAsync("tenant-a", "folder-a", "delivery", source,
            TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        string key = OperationalStatusKeys.Task("tenant-a", "folder-a", "task-a");
        var row = await store.GetAsync<TaskStatusReadModelSnapshot>("statestore", key, TestContext.Current.CancellationToken);
        row.Value!.TerminalState.ShouldBe("ready");
        TaskStatusReadModelSnapshot corrupt = corruptState ? row.Value with { CurrentState = "committed", TerminalState = "committed" }
            : row.Value with { LastOperationId = "another-valid-operation" };
        (await store.TrySaveAsync("statestore", key, corrupt, row.ETag!, TestContext.Current.CancellationToken)).ShouldBeTrue();
        var reader = new EventStoreOperationalStatusReadModel(new(store, store), new OperationalStatusClock(), new OperationalStatusSequenceReader { Head = source.Length });

        (await reader.GetAsync(new TaskStatusReadModelRequest("tenant-a", "task-a", "reader", "read_task_status", "correlation", "read_your_writes")
        { AuthorizedFolderId = "folder-a", AuthorizationWatermark = "authority" }, TestContext.Current.CancellationToken)).Status.ShouldBe(TaskStatusReadModelStatus.Unavailable);
    }

    [Fact]
    public async Task TaskLookupCannotObserveTaskBoundToAnotherFolder()
    {
        OperationalStatusTestStore store = new();
        var source = OperationalStatusTestData.Sources(OperationalStatusFixture.SuccessfulLifecycle());
        (await new OperationalStatusProjectionWriter(store, store, store).ApplyAsync("tenant-a", "folder-a", "delivery", source,
            TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var reader = new EventStoreOperationalStatusReadModel(new(store, store), new OperationalStatusClock(), new OperationalStatusSequenceReader { Head = 0 });
        var request = new TaskStatusReadModelRequest("tenant-a", "task-a", "reader-b", "read_task_status", null, "eventually_consistent")
        { AuthorizedFolderId = "folder-b", AuthorizationWatermark = "folder-b-authority" };
        (await reader.GetAsync(request, TestContext.Current.CancellationToken)).Status.ShouldBe(TaskStatusReadModelStatus.NotFound);
        store.Snapshot<TaskStatusReadModelSnapshot>(OperationalStatusKeys.StoreName,
            OperationalStatusKeys.Task("tenant-a", "folder-a", "task-a"))!.TerminalState.ShouldBe("committed");
    }

    [Fact]
    public async Task TaskLookupRequiresCurrentFolderAuthorityBeforeStoreObservation()
    {
        OperationalStatusReadObserver store = new(new OperationalStatusTestStore());
        var reader = new EventStoreOperationalStatusReadModel(new(store, new OperationalStatusTestStore()), new OperationalStatusClock(), new OperationalStatusSequenceReader());
        var request = new TaskStatusReadModelRequest("tenant-a", "task-a", "reader-b", "read_task_status", null, "eventually_consistent");
        (await reader.GetAsync(request, TestContext.Current.CancellationToken)).Status.ShouldBe(TaskStatusReadModelStatus.Unavailable);
        (await reader.GetAsync(request with { AuthorizedFolderId = "folder-a" }, TestContext.Current.CancellationToken)).Status.ShouldBe(TaskStatusReadModelStatus.Unavailable);
        store.Observations.ShouldBe(0);
    }

    [Fact]
    public async Task FreshRebuildRepairsMissingViewsAndRepeatedEmptyCheckpoints()
    {
        OperationalStatusTestStore store = new();
        var writer = new OperationalStatusProjectionWriter(store, store, store);
        var events = OperationalStatusTestData.Sources([OperationalStatusTestData.Created()]);
        (await writer.ApplyAsync("tenant-a", "folder-a", "delivery", events, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        string lifecycleKey = OperationalStatusKeys.Lifecycle("tenant-a", "folder-a");
        string checkpointKey = OperationalStatusKeys.Checkpoint("tenant-a", "folder-a");
        var reader = new EventStoreOperationalStatusReadModel(new(store, store), new OperationalStatusClock(), new OperationalStatusSequenceReader());

        for (int rebuild = 0; rebuild < 3; rebuild++)
        {
            string[] removed = rebuild == 0 ? [lifecycleKey] : [lifecycleKey, checkpointKey];
            foreach (string key in removed)
            {
                var current = await store.TryReadEtagAsync(OperationalStatusKeys.StoreName, key, TestContext.Current.CancellationToken);
                current.Present.ShouldBeTrue();
                (await store.TryEraseAsync(OperationalStatusKeys.StoreName, key, current.Etag, TestContext.Current.CancellationToken)).ShouldBeTrue();
            }

            if (rebuild == 0)
                (await reader.GetAsync(LifecycleRequest(), TestContext.Current.CancellationToken)).Status.ShouldBe(FolderLifecycleStatusReadModelStatus.Unavailable);

            (await OperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "rebuild-" + rebuild, events, events.Length, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
            store.Snapshot<FolderLifecycleStatusReadModelSnapshot>(OperationalStatusKeys.StoreName, lifecycleKey).ShouldNotBeNull();
            store.Snapshot<OperationalStatusCheckpoint>(OperationalStatusKeys.StoreName, checkpointKey)!.Sequence.ShouldBe(1);
            (await reader.GetAsync(LifecycleRequest(), TestContext.Current.CancellationToken)).Status.ShouldBe(FolderLifecycleStatusReadModelStatus.Available);
        }
    }

    [Fact]
    public async Task ReadersDistinguishMissingLaggingUnavailableCorruptAndExpiredEvidence()
    {
        OperationalStatusTestStore store = new();
        OperationalStatusClock clock = new();
        OperationalStatusSequenceReader sequence = new() { Head = 0 };
        var reader = new EventStoreOperationalStatusReadModel(new(store, store), clock, sequence);
        (await reader.GetAsync(LifecycleRequest(), TestContext.Current.CancellationToken)).Status.ShouldBe(FolderLifecycleStatusReadModelStatus.NotFound);
        sequence.Head = 1;
        (await reader.GetAsync(LifecycleRequest(), TestContext.Current.CancellationToken)).Status.ShouldBe(FolderLifecycleStatusReadModelStatus.Stale);
        await new OperationalStatusProjectionWriter(store, store, store).ApplyAsync("tenant-a", "folder-a", "live",
            OperationalStatusTestData.Sources([OperationalStatusTestData.Created()]), TestContext.Current.CancellationToken);
        var result = await reader.GetAsync(LifecycleRequest(), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(FolderLifecycleStatusReadModelStatus.Available);
        result.Snapshot!.EvidenceScope.PrincipalId.ShouldBe("reader-b");
        result.Snapshot.EvidenceScope.AuthorizationWatermark.ShouldBe("authority-2");
        result.Snapshot.Freshness.ObservedAt.ShouldBe(OperationalStatusTestData.Now);
        sequence.Head = 2;
        (await reader.GetAsync(LifecycleRequest(), TestContext.Current.CancellationToken)).Status.ShouldBe(FolderLifecycleStatusReadModelStatus.Stale);
        sequence.Head = 1;
        sequence.Unavailable = true;
        (await reader.GetAsync(LifecycleRequest(), TestContext.Current.CancellationToken)).Status.ShouldBe(FolderLifecycleStatusReadModelStatus.Unavailable);
        sequence.Unavailable = false;
        clock.UtcNow = OperationalStatusTestData.Now + OperationalStatusKeys.Retention - TimeSpan.FromTicks(1);
        (await reader.GetAsync(LifecycleRequest(), TestContext.Current.CancellationToken)).Status.ShouldBe(FolderLifecycleStatusReadModelStatus.Available);
        clock.UtcNow += TimeSpan.FromTicks(1);
        (await reader.GetAsync(LifecycleRequest(), TestContext.Current.CancellationToken)).Status.ShouldBe(FolderLifecycleStatusReadModelStatus.Unavailable);
        clock.UtcNow = OperationalStatusTestData.Now.AddSeconds(1);
        var original = store.Snapshot<FolderLifecycleStatusReadModelSnapshot>(OperationalStatusKeys.StoreName, OperationalStatusKeys.Lifecycle("tenant-a", "folder-a"))!;
        store.SeedRaw(OperationalStatusKeys.StoreName, OperationalStatusKeys.Lifecycle("tenant-a", "folder-a"), original with { ManagedTenantId = "tenant-b" });
        (await reader.GetAsync(LifecycleRequest(), TestContext.Current.CancellationToken)).Status.ShouldBe(FolderLifecycleStatusReadModelStatus.Unavailable);
    }

    [Fact]
    public async Task InvalidDeliveryCannotAdvancePersistedCheckpoint()
    {
        OperationalStatusTestStore store = new();
        var writer = new OperationalStatusProjectionWriter(store, store, store);
        var events = OperationalStatusTestData.Sources([OperationalStatusTestData.Created()]);
        await writer.ApplyAsync("tenant-a", "folder-a", "first", events, TestContext.Current.CancellationToken);
        await Should.ThrowAsync<InvalidOperationException>(() => writer.ApplyAsync("tenant-a", "folder-a", "different", [events[0] with { Digest = new string('A', 64) }], TestContext.Current.CancellationToken));
        var stored = store.Snapshot<OperationalStatusCheckpoint>(OperationalStatusKeys.StoreName, OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"))!;
        stored.Sequence.ShouldBe(1);
        stored.SourceDigest.ShouldBe(OperationalStatusFold.Apply("tenant-a", "folder-a", null, events).Checkpoint.SourceDigest);
    }

    private static FolderLifecycleStatusReadModelRequest LifecycleRequest()
        => new("tenant-a", "folder-a", "reader-b", "read_metadata", null, "request-correlation", "authority-2", "eventually_consistent");
}
