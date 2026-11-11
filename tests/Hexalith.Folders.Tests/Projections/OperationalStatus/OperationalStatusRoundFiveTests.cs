using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Projections.OperationalStatus;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Server;
using Hexalith.Folders.Tests.OperationalStatus;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Projections.OperationalStatus;

/// <summary>Exercises current projection bounds, closure authority and fresh source precedence.</summary>
public sealed class OperationalStatusRoundFiveTests
{
    [Fact]
    public async Task NativeReleaseAndDistinctPublicationHistoriesRemainBoundedAndContinueThroughSdkAsync()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var history = RoundFiveOperationalHistory.Create(4000, 256);
        // Independently prove this history is admitted by the unchanged primary fold.
        FolderState primary = FolderState.Empty.Apply(history, FolderStreamName.Create("tenant-a", "folder-a"));
        primary.NativeReleasedLeaseFences.Count.ShouldBe(4000);
        primary.ContentPublications.Count.ShouldBe(256);
        var sources = OperationalStatusTestData.Sources(history);
        string original = JsonSerializer.Serialize(sources.Select(item => item.Source));
        OperationalStatusTestStore store = new();
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "native-file-bounds",
            sources, sources.Length, token)).IsSuccess.ShouldBeTrue();
        OperationalStatusProjectionWriter writer = new(store, store, store);
        var plan = await writer.PrepareRebuildAsync("tenant-a", "folder-a", sources, sources.Length, token);
        plan.Max(item => item.CanonicalValue.Length).ShouldBeLessThan(256 * 1024);
        var checkpoint = store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"))!;
        JsonSerializer.SerializeToUtf8Bytes(checkpoint).Length.ShouldBeLessThan(16 * 1024);
        checkpoint.State.NativeReleasedLeaseFences.ShouldBeEmpty();
        checkpoint.State.ContentPublications.ShouldBeEmpty();
        checkpoint.State.FileIdentities.ShouldBeEmpty();
        checkpoint.State.ContentErasureGrants.ShouldBeEmpty();
        checkpoint.State.CommittedContentReferences.ShouldBeEmpty();
        checkpoint.State.TaskClosures.ShouldBeEmpty();
        // Changing an older incarnation needs its primary history, but only its lifecycle affects status.
        history.Add(RoundFiveOperationalHistory.Native("acquire", 4257, "continuation"));
        history.Add(RoundFiveOperationalHistory.File(4257, "continuation", 0, 2));
        history.Add(RoundFiveOperationalHistory.Native("renew", 4257, "continuation") with
        { EffectiveAt = OperationalStatusTestData.Now.AddSeconds(4258), ExpiresAt = OperationalStatusTestData.Now.AddSeconds(4318), OccurredAt = OperationalStatusTestData.Now.AddSeconds(4258) });
        history.Add(RoundFiveOperationalHistory.Native("complete", 4257, "continuation") with
        {
            PublicationCommandId = "publication-4257",
            EffectiveAt = OperationalStatusTestData.Now.AddSeconds(4259),
            ExpiresAt = OperationalStatusTestData.Now.AddSeconds(4319),
            OccurredAt = OperationalStatusTestData.Now.AddSeconds(4259)
        });
        primary.Apply(history.Skip(sources.Length), FolderStreamName.Create("tenant-a", "folder-a"))
            .IsTaskClosed("workspace-a", "continuation").ShouldBeTrue();
        var grown = OperationalStatusTestData.Sources(history);
        (await writer.ApplyAsync("tenant-a", "folder-a", "native-file-continuation", grown[sources.Length..], token)).IsSuccess.ShouldBeTrue();
        var before = store.Snapshot<TaskStatusReadModelSnapshot>("statestore", OperationalStatusKeys.Task("tenant-a", "folder-a", "continuation"))!;
        before.TerminalState.ShouldBe("ready");
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "native-file-grown",
            grown, grown.Length, token)).IsSuccess.ShouldBeTrue();
        store.Snapshot<TaskStatusReadModelSnapshot>("statestore", OperationalStatusKeys.Task("tenant-a", "folder-a", "continuation")).ShouldBe(before);
        (await writer.ApplyAsync("tenant-a", "folder-a", "native-file-duplicate", [grown[^1]], token)).IsSuccess.ShouldBeTrue();
        JsonSerializer.Serialize(sources.Select(item => item.Source)).ShouldBe(original);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PairedPhysicalExpiryCannotResurrectOriginalLegacyOrNativeCompletionAsync(bool native)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        List<IFolderEvent> history = native ? RoundFiveOperationalHistory.Create(0, 1)
            : [.. OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now)];
        string taskId = native ? "published-task-0" : "task-a";
        var sources = OperationalStatusTestData.Sources(history);
        OperationalStatusTestStore store = new();
        OperationalStatusClock clock = new();
        OperationalStatusProjectionWriter writer = new(store, store, store, clock);
        (await writer.ApplyAsync("tenant-a", "folder-a", "initial", sources, token)).IsSuccess.ShouldBeTrue();
        string key = OperationalStatusKeys.Task("tenant-a", "folder-a", taskId);
        var original = store.Snapshot<TaskStatusReadModelSnapshot>("statestore", key)!;
        var completion = store.Snapshot<OperationalStatusTaskCompletion>("statestore", OperationalStatusKeys.TaskCompletion("tenant-a", "folder-a", taskId))!;
        foreach (string expired in new[] { key, OperationalStatusKeys.Evidence(key) })
        {
            var row = await store.TryReadEtagAsync("statestore", expired, token);
            (await store.TryEraseAsync("statestore", expired, row.Etag, token)).ShouldBeTrue();
        }
        clock.UtcNow = OperationalStatusTestData.Now.AddDays(401);
        history.Add(RoundFiveOperationalHistory.Grant(clock.UtcNow));
        var grown = OperationalStatusTestData.Sources(history);
        (await writer.ApplyAsync("tenant-a", "folder-a", "unrelated-grant", [grown[^1]], token)).IsSuccess.ShouldBeTrue();
        store.Snapshot<TaskStatusReadModelSnapshot>("statestore", key).ShouldBeNull();
        store.Snapshot<OperationalStatusTaskCompletion>("statestore", OperationalStatusKeys.TaskCompletion("tenant-a", "folder-a", taskId)).ShouldBe(completion);
        (await Reader(store, clock, grown.Length).GetAsync(Request(taskId), token)).Status.ShouldBe(TaskStatusReadModelStatus.NotFound);
        var rebuilt = await writer.PrepareRebuildAsync("tenant-a", "folder-a", grown, grown.Length, token);
        rebuilt.ShouldNotContain(item => item.Key == key || item.Key == OperationalStatusKeys.Evidence(key));
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "expired-rebuild",
            grown, grown.Length, token, clock: clock)).IsSuccess.ShouldBeTrue();
        store.Snapshot<TaskStatusReadModelSnapshot>("statestore", key).ShouldBeNull();
        store.Snapshot<OperationalStatusTaskCompletion>("statestore", OperationalStatusKeys.TaskCompletion("tenant-a", "folder-a", taskId))!.Closure.ClosedAt.ShouldBe(original.Freshness.ObservedAt);
        (await writer.ApplyAsync("tenant-a", "folder-a", "expired-replay", [sources[^1]], token)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task AbsentNewTaskAtALaggingCheckpointIsStaleRatherThanMissingAsync()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        OperationalStatusTestStore store = new();
        var sources = OperationalStatusTestData.Sources(OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now));
        (await new OperationalStatusProjectionWriter(store, store, store).ApplyAsync("tenant-a", "folder-a", "initial", sources, token)).IsSuccess.ShouldBeTrue();
        (await Reader(store, new(), 9).GetAsync(Request("new-task"), token)).Status.ShouldBe(TaskStatusReadModelStatus.Stale);
        (await Reader(store, new(), 8).GetAsync(Request("new-task"), token)).Status.ShouldBe(TaskStatusReadModelStatus.NotFound);
        (await Reader(store, new(), 9).GetAsync(new WorkspaceStatusReadModelRequest("tenant-a", "folder-a", "new-workspace",
            "reader", "read_workspace_status", null, "correlation", "authority", "eventually_consistent"), token))
            .Status.ShouldBe(WorkspaceStatusReadModelStatus.Stale);
    }

    [Fact]
    public async Task OldCompactedCheckpointRequiresNamedCompleteSourceRepairBeforeResumingLiveWritesAsync()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        OperationalStatusTestStore store = new();
        var sources = OperationalStatusTestData.Sources(OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now));
        OperationalStatusProjectionWriter writer = new(store, store, store);
        (await writer.ApplyAsync("tenant-a", "folder-a", "initial", sources, token)).IsSuccess.ShouldBeTrue();
        string checkpointKey = OperationalStatusKeys.Checkpoint("tenant-a", "folder-a");
        var old = store.Snapshot<OperationalStatusCheckpoint>("statestore", checkpointKey)! with { SchemaVersion = 2 };
        old = old with { IntegrityDigest = OperationalStatusIntegrity.Checkpoint(old) };
        store.SeedRaw("statestore", checkpointKey, old);
        OperationalStatusFold.Valid(old, "tenant-a", "folder-a").ShouldBeTrue();
        // Reading an intact source-bound old row is safe, but old compacted state proves no historical closure inventory.
        (await Reader(store, new(), sources.Length).GetAsync(Request("task-a"), token)).Status.ShouldBe(TaskStatusReadModelStatus.Available);
        var grant = OperationalStatusEventReader.ReadStored(OperationalStatusTestData.Envelope(RoundFiveOperationalHistory.Grant(OperationalStatusTestData.Now), 9));
        await Should.ThrowAsync<OperationalStatusUnavailableException>(() => writer.ApplyAsync("tenant-a", "folder-a", "old-live", [grant], token));
        (await new OperationalStatusProjectionHandler(writer).ProjectAsync(new("tenant-a", "folders", "folder-a", [grant.Source!]),
            "old-named-live", token)).Status.ShouldBe(Hexalith.EventStore.Contracts.Projections.ProjectionDispatchStatus.Indeterminate);
        JsonSerializer.Serialize(store.Snapshot<OperationalStatusCheckpoint>("statestore", checkpointKey)).ShouldBe(JsonSerializer.Serialize(old));
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "upgrade-source", sources,
            sources.Length, token)).IsSuccess.ShouldBeTrue();
        store.Snapshot<OperationalStatusCheckpoint>("statestore", checkpointKey)!.SchemaVersion.ShouldBe(3);
        (await writer.ApplyAsync("tenant-a", "folder-a", "resumed", [grant], token)).IsSuccess.ShouldBeTrue();
        var grown = sources.Append(grant).ToArray();
        store.Snapshot<OperationalStatusCheckpoint>("statestore", checkpointKey)!.SourceDigest
            .ShouldBe(OperationalStatusFold.Apply("tenant-a", "folder-a", null, grown).Checkpoint.SourceDigest);
        (await Reader(store, new(), grown.Length).GetAsync(Request("task-a"), token)).Snapshot!.LastOperationId.ShouldBe("lock-a");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EarliestCompletionCannotBeRefreshedByReusedTaskInALaterWorkspaceAsync(bool native)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        List<IFolderEvent> history = native ? RoundFiveOperationalHistory.Create(0, 1)
            : [.. OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now)];
        string task = native ? "published-task-0" : "task-a";
        var original = OperationalStatusTestData.Sources(history);
        OperationalStatusClock clock = new();
        OperationalStatusTestStore store = new();
        OperationalStatusProjectionWriter writer = new(store, store, store, clock);
        (await writer.ApplyAsync("tenant-a", "folder-a", "original", original, token)).IsSuccess.ShouldBeTrue();
        string key = OperationalStatusKeys.Task("tenant-a", "folder-a", task);
        var closed = store.Snapshot<OperationalStatusTaskCompletion>("statestore", OperationalStatusKeys.TaskCompletion("tenant-a", "folder-a", task));
        clock.UtcNow = OperationalStatusTestData.Now.AddDays(401);
        var prep = history.OfType<WorkspacePreparationRequested>().Single();
        history.Add(prep with { WorkspaceId = "workspace-b", TaskId = task, OccurredAt = clock.UtcNow });
        history.Add(RoundFiveOperationalHistory.Native("acquire", 2, task) with
        { WorkspaceId = "workspace-b", EffectiveAt = clock.UtcNow, ExpiresAt = clock.UtcNow.AddMinutes(1), OccurredAt = clock.UtcNow });
        var publication = RoundFiveOperationalHistory.File(2, task, 1);
        history.Add(publication with
        {
            WorkspaceId = "workspace-b",
            OccurredAt = clock.UtcNow,
            ContentBinding = publication.ContentBinding! with { WorkspaceId = "workspace-b" }
        });
        history.Add(RoundFiveOperationalHistory.Native("complete", 2, task) with
        {
            WorkspaceId = "workspace-b",
            EffectiveAt = clock.UtcNow,
            ExpiresAt = clock.UtcNow.AddMinutes(1),
            OccurredAt = clock.UtcNow,
            PublicationCommandId = "publication-2"
        });
        FolderState.Empty.Apply(history, FolderStreamName.Create("tenant-a", "folder-a"))
            .IsTaskClosed("workspace-b", task).ShouldBeTrue();
        var grown = OperationalStatusTestData.Sources(history);
        (await writer.ApplyAsync("tenant-a", "folder-a", "later-binding", grown[original.Length..], token)).IsSuccess.ShouldBeTrue();
        // The in-memory fake retains physical bytes without sweeping TTL; no new write may refresh that row.
        store.Snapshot<TaskStatusReadModelSnapshot>("statestore", key)!.Freshness.ObservedAt.ShouldBe(closed!.Closure.ClosedAt);
        store.Snapshot<OperationalStatusTaskCompletion>("statestore", OperationalStatusKeys.TaskCompletion("tenant-a", "folder-a", task)).ShouldBe(closed);
        (await Reader(store, clock, grown.Length).GetAsync(Request(task), token)).Status.ShouldBe(TaskStatusReadModelStatus.NotFound);
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "reused-task-rebuild", grown,
            grown.Length, token, clock: clock)).IsSuccess.ShouldBeTrue();
        store.Snapshot<OperationalStatusTaskCompletion>("statestore", OperationalStatusKeys.TaskCompletion("tenant-a", "folder-a", task)).ShouldBe(closed);
        store.Snapshot<TaskStatusReadModelSnapshot>("statestore", key).ShouldBeNull();
    }

    [Fact]
    public async Task CancellationDuringAdmittedSourceEnumerationStopsBeforeMaterializationOrPersistenceAsync()
    {
        var sources = OperationalStatusTestData.Sources(RoundFiveOperationalHistory.Create(4000, 0));
        using CancellationTokenSource cancellation = new();
        CancelingOperationalEvents source = new(sources, cancellation, 40);
        OperationalStatusTestStore store = new();
        await Should.ThrowAsync<OperationCanceledException>(() => new OperationalStatusProjectionWriter(store, store, store)
            .PrepareRebuildAsync("tenant-a", "folder-a", source, source.Count, cancellation.Token));
        source.Visited.ShouldBe(40);
        store.Count.ShouldBe(0);
    }

    [Fact]
    public async Task CancellationDuringAdmittedHistoryMaterializationStopsRemainingOperationsAsync()
    {
        var sources = OperationalStatusTestData.Sources(RoundFiveOperationalHistory.Create(4000, 0));
        using CancellationTokenSource cancellation = new();
        CancelingOperationalClock clock = new(cancellation, 12);
        OperationalStatusTestStore store = new();
        await Should.ThrowAsync<OperationCanceledException>(() => new OperationalStatusProjectionWriter(store, store, store, clock)
            .PrepareRebuildAsync("tenant-a", "folder-a", sources, sources.Length, cancellation.Token));
        clock.Reads.ShouldBeInRange(12, 13);
        store.Count.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrCorruptCompletionAfterPairedExpiryCannotAcknowledgeAndNamedSourceRepairsItAsync(bool corrupt)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        OperationalStatusTestStore store = new();
        OperationalStatusClock clock = new();
        OperationalStatusProjectionWriter writer = new(store, store, store, clock);
        var sources = OperationalStatusTestData.Sources(OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now));
        (await writer.ApplyAsync("tenant-a", "folder-a", "original", sources, token)).IsSuccess.ShouldBeTrue();
        string key = OperationalStatusKeys.Task("tenant-a", "folder-a", "task-a");
        string completionKey = OperationalStatusKeys.TaskCompletion("tenant-a", "folder-a", "task-a");
        var original = store.Snapshot<OperationalStatusTaskCompletion>("statestore", completionKey)!;
        foreach (string expired in new[] { key, OperationalStatusKeys.Evidence(key) })
        {
            var row = await store.TryReadEtagAsync("statestore", expired, token);
            (await store.TryEraseAsync("statestore", expired, row.Etag, token)).ShouldBeTrue();
        }
        if (corrupt) store.SeedRaw("statestore", completionKey, original with { Sequence = 999 });
        else
        {
            var row = await store.TryReadEtagAsync("statestore", completionKey, token);
            (await store.TryEraseAsync("statestore", completionKey, row.Etag, token)).ShouldBeTrue();
            // The SDK remembers selected logical evidence; lost physical authority cannot become a null never-closed result.
            await Should.ThrowAsync<SharedProjectionReadUnavailableException>(() => new SharedProjectionEpochCoordinator(store, store)
                .ReadAsync<OperationalStatusTaskCompletion>(OperationalStatusGeneration.Scope("tenant-a"), completionKey, token));
        }
        clock.UtcNow = OperationalStatusTestData.Now.AddDays(401);
        var grant = OperationalStatusEventReader.ReadStored(OperationalStatusTestData.Envelope(RoundFiveOperationalHistory.Grant(clock.UtcNow), 9));
        var named = new OperationalStatusProjectionHandler(writer);
        (await named.ProjectAsync(new("tenant-a", "folders", "folder-a", [grant.Source!]), "lost-authority", token))
            .Status.ShouldBe(Hexalith.EventStore.Contracts.Projections.ProjectionDispatchStatus.Indeterminate);
        store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"))!.Sequence.ShouldBe(8);
        store.Snapshot<TaskStatusReadModelSnapshot>("statestore", key).ShouldBeNull();
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "repair-closure", sources,
            sources.Length, token, clock: clock)).IsSuccess.ShouldBeTrue();
        store.Snapshot<OperationalStatusTaskCompletion>("statestore", completionKey).ShouldBe(original);
        (await writer.ApplyAsync("tenant-a", "folder-a", "after-repair", [grant], token)).IsSuccess.ShouldBeTrue();
        store.Snapshot<TaskStatusReadModelSnapshot>("statestore", key).ShouldBeNull();
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("folder")]
    [InlineData("workspace")]
    [InlineData("task")]
    [InlineData("command")]
    [InlineData("file")]
    [InlineData("operation")]
    [InlineData("kind")]
    [InlineData("path")]
    [InlineData("fence")]
    [InlineData("version")]
    [InlineData("expected-version")]
    [InlineData("content-hash")]
    [InlineData("length")]
    [InlineData("media")]
    [InlineData("policy-version")]
    [InlineData("policy-digest")]
    [InlineData("receipt")]
    public async Task MalformedPublicationMetadataCannotAdvanceSourceOrAcknowledgeAsync(string defect)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var history = RoundFiveOperationalHistory.Create(0, 0);
        history.Add(RoundFiveOperationalHistory.Native("acquire", 1, "native-task"));
        var source = OperationalStatusTestData.Sources(history);
        var file = RoundFiveOperationalHistory.File(1, "native-task", 0);
        var binding = file.ContentBinding!;
        binding = defect switch
        {
            "tenant" => binding with { ManagedTenantId = "foreign" },
            "folder" => binding with { FolderId = "foreign" },
            "workspace" => binding with { WorkspaceId = "foreign" },
            "task" => binding with { TaskId = "foreign" },
            "command" => binding with { CommandId = "foreign" },
            "file" => binding with { FileId = "foreign" },
            "operation" => binding with { OperationId = "foreign" },
            "kind" => binding with { FileOperationKind = "remove" },
            "path" => binding with { PathMetadataDigest = "foreign" },
            "fence" => binding with { LockFence = 2 },
            "version" => binding with { Version = 0 },
            "expected-version" => binding with { ExpectedVersion = -1 },
            "content-hash" => binding with { ContentHashReference = "foreign" },
            "length" => binding with { ByteLength = 2 },
            "media" => binding with { MediaType = "foreign" },
            "policy-version" => binding with { PolicyVersion = "other" },
            "policy-digest" => binding with { PolicyDigest = " " },
            _ => binding with { ReceiptId = null },
        };
        file = file with { ContentBinding = binding };
        OperationalStatusTestStore store = new();
        OperationalStatusProjectionWriter writer = new(store, store, store);
        (await writer.ApplyAsync("tenant-a", "folder-a", "prefix", source, token)).IsSuccess.ShouldBeTrue();
        string checkpoint = OperationalStatusKeys.Checkpoint("tenant-a", "folder-a");
        string before = JsonSerializer.Serialize(store.Snapshot<OperationalStatusCheckpoint>("statestore", checkpoint));
        var invalid = OperationalStatusEventReader.ReadStored(OperationalStatusTestData.Envelope(file, source.Length + 1));
        await Should.ThrowAsync<InvalidOperationException>(() => writer.ApplyAsync("tenant-a", "folder-a", "malformed-publication", [invalid], token));
        (await new OperationalStatusProjectionHandler(writer).ProjectAsync(new("tenant-a", "folders", "folder-a", [invalid.Source!]),
            "named-malformed-publication", token)).Status.ShouldBe(Hexalith.EventStore.Contracts.Projections.ProjectionDispatchStatus.Failed);
        JsonSerializer.Serialize(store.Snapshot<OperationalStatusCheckpoint>("statestore", checkpoint)).ShouldBe(before);
    }

    [Theory]
    [InlineData("lock")]
    [InlineData("lease")]
    [InlineData("interval")]
    [InlineData("duration")]
    [InlineData("task")]
    public async Task MalformedNativeAcquisitionCannotAdvanceDirectOrNamedPersistenceAsync(string defect)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var history = RoundFiveOperationalHistory.Create(0, 0);
        var acquired = RoundFiveOperationalHistory.Native("acquire", 1, "native-task");
        history.Add(defect switch
        {
            "lock" => acquired with { LockId = " " },
            "lease" => acquired with { LeaseSeconds = -1 },
            "interval" => acquired with { ExpiresAt = acquired.EffectiveAt.AddSeconds(-1) },
            "duration" => acquired with { LeaseSeconds = 2 },
            _ => acquired with { TaskId = " " },
        });
        var prefix = OperationalStatusTestData.Sources(history.Take(6));
        OperationalStatusTestStore store = new();
        OperationalStatusProjectionWriter writer = new(store, store, store);
        (await writer.ApplyAsync("tenant-a", "folder-a", "initial", prefix, token)).IsSuccess.ShouldBeTrue();
        var before = store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"));
        if (defect == "task")
            Should.Throw<InvalidOperationException>(() => OperationalStatusTestData.Sources(history));
        else
        {
            var sources = OperationalStatusTestData.Sources(history);
            await Should.ThrowAsync<InvalidOperationException>(() => writer.ApplyAsync("tenant-a", "folder-a", "invalid", [sources[^1]], token));
            await Should.ThrowAsync<InvalidOperationException>(() => NamedOperationalStatusRebuildFixture.RebuildAsync(store,
                "tenant-a", "folder-a", "invalid-native-rebuild", sources, sources.Length, token, retryPartial: false));
        }
        JsonSerializer.Serialize(store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a")))
            .ShouldBe(JsonSerializer.Serialize(before));
        var named = new OperationalStatusProjectionHandler(writer);
        (await named.ProjectAsync(OperationalStatusTestData.Projection(history), "named-invalid", token)).Status
            .ShouldBe(Hexalith.EventStore.Contracts.Projections.ProjectionDispatchStatus.Failed);
    }

    private static EventStoreOperationalStatusReadModel Reader(OperationalStatusTestStore store, OperationalStatusClock clock, long head)
        => new(new(store, store), clock, new OperationalStatusSequenceReader { Head = head });
    private static TaskStatusReadModelRequest Request(string task)
        => new("tenant-a", task, "reader", "read_task_status", "correlation", "eventually_consistent")
        { AuthorizedFolderId = "folder-a", AuthorizationWatermark = "authority" };
}
