using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Projections.OperationalStatus;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Tests.Aggregates.Folder;
using Hexalith.Folders.Tests.OperationalStatus;
using Hexalith.Folders.Tests.Queries.Folders;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Projections.OperationalStatus;

public sealed class OperationalStatusReviewThreeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CorruptOrLegacyReceiptBindingRequiresCompleteSourceRepair(bool missingLegacyBinding)
    {
        var history = OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now).ToList();
        history.Add(history.OfType<WorkspaceLockAcquired>().Single() with { TaskId = "later-task", HolderTaskId = "later-task", LockId = "later-lock" });
        history.Add(history.OfType<WorkspaceLockReleased>().Single() with { TaskId = "later-task", HolderTaskId = "later-task", LockId = "later-lock" });
        var source = OperationalStatusTestData.Sources(history);
        OperationalStatusTestStore store = new();
        OperationalStatusProjectionWriter writer = new(store, store, store);
        (await writer.ApplyAsync("tenant-a", "folder-a", "original", source, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        string receiptKey = OperationalStatusKeys.Receipt("tenant-a", "folder-a", source[1].Sequence);
        string messageKey = OperationalStatusKeys.Message("tenant-a", "folder-a", source[1].MessageId);
        var original = store.Snapshot<OperationalStatusReceipt>("statestore", receiptKey)!;
        var damaged = original with { RequiredViews = missingLegacyBinding ? null! : original.RequiredViews with { TaskId = "later-task" } };
        // Both copies agree, so the canonical binding proof must independently reject this alteration.
        store.SeedRaw("statestore", receiptKey, damaged);
        store.SeedRaw("statestore", messageKey, damaged);
        await Should.ThrowAsync<InvalidOperationException>(() => writer.ApplyAsync("tenant-a", "folder-a", "corrupt-binding",
            [source[1]], TestContext.Current.CancellationToken));
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "repair-binding", source, source.Length,
            TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        store.Snapshot<OperationalStatusReceipt>("statestore", receiptKey).ShouldBe(original);
        (await writer.ApplyAsync("tenant-a", "folder-a", "duplicate-retry", [source[1]], TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ConfirmedReconciliationClearsSupersededFailureInDurableOutcome()
    {
        var history = FolderLifecycleReplayFixture.CommitFailureLifecycle().ToList();
        var failed = history.OfType<WorkspaceCommitFailed>().Single();
        history.Add(new FolderWorkspaceLifecycleEventRecorded(failed.ManagedTenantId, failed.OrganizationId, failed.FolderId,
            failed.WorkspaceId, FolderWorkspaceLifecycleEvent.ReconciliationRequested, null, "reconcile", failed.CorrelationId,
            failed.TaskId, "reconcile-key", "reconcile-fingerprint", failed.OccurredAt.AddSeconds(1)));
        history.Add((FolderWorkspaceLifecycleEventRecorded)history[^1] with
        { WorkspaceLifecycleEvent = FolderWorkspaceLifecycleEvent.ReconciliationCompletedDirty, OperationId = "confirmed", OccurredAt = failed.OccurredAt.AddSeconds(2) });
        OperationalStatusTestStore store = new();
        var source = OperationalStatusTestData.Sources(history);
        (await new OperationalStatusProjectionWriter(store, store, store).ApplyAsync("tenant-a", "folder-a", "recovered", source,
            TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var reader = new EventStoreOperationalStatusReadModel(new(store, store), new OperationalStatusClock(), new OperationalStatusSequenceReader { Head = source.Length });
        var outcome = await reader.GetAsync(new WorkspaceStatusReadModelRequest("tenant-a", "folder-a", "workspace-a", "reader",
            "read_workspace_status", "task-a", "corr", "authority", "read_your_writes"), TestContext.Current.CancellationToken);
        outcome.Status.ShouldBe(WorkspaceStatusReadModelStatus.Available);
        outcome.Snapshot!.ProviderOutcome.State.ShouldBe("known_success");
        outcome.Snapshot.ProviderOutcome.SanitizedStatusClass.ShouldBe("success");
        outcome.Snapshot.LastFailureCategory.ShouldBeNull();
        var task = store.Snapshot<TaskStatusReadModelSnapshot>("statestore", OperationalStatusKeys.Task("tenant-a", "folder-a", "task-a"))!;
        task.TerminalState.ShouldBe("committed");
        task.LastOperationId.ShouldBe("confirmed");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FreshRejectionCannotAcknowledgeMissingOrCorruptPreservedWorkspace(bool missing)
    {
        OperationalStatusTestStore store = new();
        var source = OperationalStatusTestData.Sources(OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now));
        OperationalStatusProjectionWriter writer = new(store, store, store);
        (await writer.ApplyAsync("tenant-a", "folder-a", "original", source, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        string key = OperationalStatusKeys.Workspace("tenant-a", "folder-a", "workspace-a");
        var original = store.Snapshot<OperationalWorkspaceStatus>("statestore", key)!;
        if (missing)
        {
            var row = await store.TryReadEtagAsync("statestore", key, TestContext.Current.CancellationToken);
            (await store.TryEraseAsync("statestore", key, row.Etag, TestContext.Current.CancellationToken)).ShouldBeTrue();
        }
        else store.SeedRaw("statestore", key, original with { Workspace = original.Workspace with { CurrentState = "dirty" } });
        var rejection = OperationalStatusEventReader.ReadStored(source[0].Source! with
        {
            SequenceNumber = source.Length + 1,
            GlobalPosition = source.Length + 1,
            MessageId = "fresh-rejection",
            EventTypeName = "Hexalith.Folders.Server.FolderCommandRejected",
            Payload = Encoding.UTF8.GetBytes("{\"Code\":\"UnsupportedCommandType\",\"CommandType\":\"UnknownCommand\"}"),
        });
        try { (await writer.ApplyAsync("tenant-a", "folder-a", "rejected", [rejection], TestContext.Current.CancellationToken)).IsSuccess.ShouldBeFalse(); }
        catch (OperationalStatusUnavailableException) { }
        store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"))!
            .Sequence.ShouldBe(source.Length);
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "repair", source, source.Length,
            TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        (await writer.ApplyAsync("tenant-a", "folder-a", "retry", [rejection], TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public async Task DuplicateOriginalSourceCannotAcknowledgeMissingHistoricalTask(int originalPosition)
    {
        var history = OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now).ToList();
        history.Add(history.OfType<WorkspaceLockAcquired>().Single() with { TaskId = "later-task", HolderTaskId = "later-task", LockId = "later-lock" });
        history.Add(history.OfType<WorkspaceLockReleased>().Single() with { TaskId = "later-task", HolderTaskId = "later-task", LockId = "later-lock" });
        var source = OperationalStatusTestData.Sources(history);
        OperationalStatusTestStore store = new();
        OperationalStatusProjectionWriter writer = new(store, store, store);
        (await writer.ApplyAsync("tenant-a", "folder-a", "original", source, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        string key = OperationalStatusKeys.Task("tenant-a", "folder-a", "task-a");
        var row = await store.TryReadEtagAsync("statestore", key, TestContext.Current.CancellationToken);
        (await store.TryEraseAsync("statestore", key, row.Etag, TestContext.Current.CancellationToken)).ShouldBeTrue();
        await Should.ThrowAsync<OperationalStatusUnavailableException>(() => writer.ApplyAsync("tenant-a", "folder-a", "historical-duplicate", [source[originalPosition]], TestContext.Current.CancellationToken));
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "repair", source, source.Length,
            TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        (await writer.ApplyAsync("tenant-a", "folder-a", "duplicate-retry", [source[originalPosition]], TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task CompleteSourceRepairsMissingCheckpointInheritedFromAnotherOwnersGeneration()
    {
        OperationalStatusTestStore store = new();
        var source = OperationalStatusTestData.Sources([OperationalStatusTestData.Created()]);
        (await new OperationalStatusProjectionWriter(store, store, store).ApplyAsync("tenant-a", "folder-a", "initial", source,
            TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        string inherited = store.PhysicalKey(OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"));
        var other = OperationalStatusTestData.Sources([OperationalStatusTestData.Created(folder: "folder-b")]);
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-b", "other-owner", other, 1,
            TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var row = await store.Inner.TryReadEtagAsync("statestore", inherited, TestContext.Current.CancellationToken);
        (await store.Inner.TryEraseAsync("statestore", inherited, row.Etag, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "repair-inherited", source, 1,
            TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        (await new OperationalStatusProjectionWriter(store, store, store).ApplyAsync("tenant-a", "folder-a", "duplicate", source,
            TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData("null-workspace")]
    [InlineData("blank-workspace")]
    [InlineData("negative-lease")]
    [InlineData("expiry-before-acquisition")]
    [InlineData("effective-mismatch")]
    [InlineData("duration-mismatch")]
    public async Task MalformedAcquisitionNeverAdvancesDurableViews(string fault)
    {
        var history = OperationalStatusTestData.LockedPrefix(OperationalStatusTestData.Now);
        var acquisition = history.OfType<WorkspaceLockAcquired>().Single();
        acquisition = fault switch
        {
            "null-workspace" => acquisition with { WorkspaceId = null! },
            "blank-workspace" => acquisition with { WorkspaceId = " " },
            "negative-lease" => acquisition with { RequestedLeaseSeconds = -1 },
            "expiry-before-acquisition" => acquisition with { ExpiresAt = acquisition.AcquiredAt.AddSeconds(-1) },
            "effective-mismatch" => acquisition with { EffectiveAt = acquisition.EffectiveAt.AddSeconds(1) },
            _ => acquisition with { ExpiresAt = acquisition.ExpiresAt.AddSeconds(1) },
        };
        OperationalStatusTestStore store = new();
        OperationalStatusProjectionWriter writer = new(store, store, store);
        var prefix = OperationalStatusTestData.Sources(history.Take(history.Count - 1));
        (await writer.ApplyAsync("tenant-a", "folder-a", "prefix", prefix, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        string before = JsonSerializer.Serialize(store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a")));
        await Should.ThrowAsync<InvalidOperationException>(() => writer.ApplyAsync("tenant-a", "folder-a", "malformed",
            [OperationalStatusEventReader.ReadStored(OperationalStatusTestData.Envelope(acquisition, history.Count))], TestContext.Current.CancellationToken));
        JsonSerializer.Serialize(store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"))).ShouldBe(before);
    }

    [Theory]
    [InlineData("failed")]
    [InlineData("unknown_provider_outcome")]
    [InlineData("reconciliation_required")]
    public async Task RepositoryOutcomesTravelFromSourceThroughDurableReaderToPublicQuery(string expected)
    {
        var history = expected == "failed" ? FolderLifecycleReplayFixture.RepositoryFailureLifecycle()
            : FolderLifecycleReplayFixture.RepositoryUnknownOutcomeLifecycle(expected == "reconciliation_required");
        var source = OperationalStatusTestData.Sources(history.Select(value => value switch
        {
            FolderCreated item => (IFolderEvent)(item with { OccurredAt = FolderLifecycleStatusTestSupport.Now }),
            RepositoryBindingRequested item => item with { OccurredAt = FolderLifecycleStatusTestSupport.Now },
            RepositoryBindingFailed item => item with { OccurredAt = FolderLifecycleStatusTestSupport.Now },
            ProviderOutcomeUnknown item => item with { OccurredAt = FolderLifecycleStatusTestSupport.Now },
            _ => throw new InvalidOperationException("Unexpected repository lifecycle source."),
        }));
        OperationalStatusTestStore store = new();
        (await new OperationalStatusProjectionWriter(store, store, store).ApplyAsync("tenant-a", "folder-a", "source", source,
            TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var reader = new EventStoreOperationalStatusReadModel(new(store, store), new OperationalStatusClock(),
            new OperationalStatusSequenceReader { Head = source.Length });
        var handler = FolderLifecycleStatusTestSupport.Handler(new CountingTenantAccessProjectionStore(
            FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-a"])), reader);
        var result = await handler.HandleAsync(FolderLifecycleStatusTestSupport.Query(), TestContext.Current.CancellationToken);
        result.Code.ShouldBe(FolderLifecycleStatusResultCode.Allowed);
        result.LifecycleState.ShouldBe(expected);
    }
}
