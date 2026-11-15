using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Projections.OperationalStatus;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Tests.OperationalStatus;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Projections.OperationalStatus;

/// <summary>Operational Status Native Source Authority Tests.</summary>
public sealed class OperationalStatusNativeSourceAuthorityTests
{
    /// <summary>Duplicate acknowledgments reject historical row rollback or generation promotion during the independent scan.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HistoricalDuplicateRechecksRowsAndGenerationAfterScan(bool promoteGeneration)
    {
        var token = TestContext.Current.CancellationToken;
        var store = new OperationalStatusTestStore(); var clock = new OperationalStatusClock();
        var writer = new OperationalStatusProjectionWriter(store, store, store, clock, OperationalStatusTestData.Scanner);
        var history = OperationalStatusTestData.LockedPrefix(OperationalStatusTestData.Now).Take(6).ToList();
        var acquire = new WorkspaceNativeTaskChanged("tenant-a", "tenant-a", "folder-a", "workspace-a", "acquire", "native-lock-1", 1, 60,
            OperationalStatusTestData.Now.AddSeconds(1), OperationalStatusTestData.Now.AddSeconds(61), null,
            "principal-a", "correlation-a", "task-a", "native-acquire-1", "fp", OperationalStatusTestData.Now.AddSeconds(1));
        history.Add(acquire); var sources = OperationalStatusTestData.Sources(history);
        (await writer.ApplyAsync("tenant-a", "folder-a", "before", sources, token)).IsSuccess.ShouldBeTrue();
        string key = OperationalStatusKeys.Task("tenant-a", "folder-a", "task-a");
        var old = new[] { key, OperationalStatusKeys.Evidence(key), OperationalStatusKeys.Applicability(key) }
            .ToDictionary(value => value, value => store.Snapshot<JsonNode>("statestore", value)!);
        history.Add(acquire with { Operation = "release", ExpiresAt = acquire.EffectiveAt, IdempotencyKey = "native-release" });
        history.Add(acquire with
        {
            TaskId = "task-b",
            LockId = "native-lock-2",
            LockFence = 2,
            IdempotencyKey = "native-acquire-2",
            EffectiveAt = OperationalStatusTestData.Now.AddSeconds(2),
            ExpiresAt = OperationalStatusTestData.Now.AddSeconds(62),
            OccurredAt = OperationalStatusTestData.Now.AddSeconds(2)
        });
        sources = OperationalStatusTestData.Sources(history);
        (await writer.ApplyAsync("tenant-a", "folder-a", "continued", sources[7..], token)).IsSuccess.ShouldBeTrue();
        var other = OperationalStatusTestData.Sources([OperationalStatusTestData.Created(folder: "folder-b")]);
        other[0] = OperationalStatusTestData.Stored(other[0].Source! with { GlobalPosition = sources.Length + 1 });
        OperationalStatusTestData.NativeSource.OnRead = () =>
        {
            OperationalStatusTestData.NativeSource.OnRead = null;
            if (promoteGeneration)
                NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-b", "promotion-during-duplicate", other,
                    sources.Length + 1, token, clock: clock).GetAwaiter().GetResult().IsSuccess.ShouldBeTrue();
            else foreach (var value in old) store.SeedRaw("statestore", value.Key, value.Value);
        };
        await Should.ThrowAsync<OperationalStatusUnavailableException>(() => writer.ApplyAsync("tenant-a", "folder-a", "duplicate-during-change", [sources[6]], token));
    }

    /// <summary>Native Release Historical Triple Requires Current Source And Named Repair Restores Lawful Delivery.</summary>
    [Fact]
    public async Task NativeReleaseHistoricalTripleRequiresCurrentSourceAndNamedRepairRestoresLawfulDelivery()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new OperationalStatusTestStore(); var clock = new OperationalStatusClock();
        var writer = new OperationalStatusProjectionWriter(store, store, store, clock, OperationalStatusTestData.Scanner);
        var history = OperationalStatusTestData.LockedPrefix(OperationalStatusTestData.Now).Take(6).ToList();
        var acquire = new WorkspaceNativeTaskChanged("tenant-a", "tenant-a", "folder-a", "workspace-a", "acquire", "native-lock-1", 1, 60,
            OperationalStatusTestData.Now.AddSeconds(1), OperationalStatusTestData.Now.AddSeconds(61), null,
            "principal-a", "correlation-a", "task-a", "native-acquire-1", "fp", OperationalStatusTestData.Now.AddSeconds(1));
        history.Add(acquire); var sources = OperationalStatusTestData.Sources(history);
        (await writer.ApplyAsync("tenant-a", "folder-a", "initial", sources, token)).IsSuccess.ShouldBeTrue();
        string key = OperationalStatusKeys.Task("tenant-a", "folder-a", "task-a");
        var old = new[] { key, OperationalStatusKeys.Evidence(key), OperationalStatusKeys.Applicability(key) }
            .ToDictionary(value => value, value => store.Snapshot<JsonNode>("statestore", value)!);
        history.Add(acquire with { Operation = "release", ExpiresAt = acquire.EffectiveAt, IdempotencyKey = "native-release" });
        history.Add(acquire with
        {
            TaskId = "task-b",
            LockId = "native-lock-2",
            LockFence = 2,
            IdempotencyKey = "native-acquire-2",
            EffectiveAt = OperationalStatusTestData.Now.AddSeconds(2),
            ExpiresAt = OperationalStatusTestData.Now.AddSeconds(62),
            OccurredAt = OperationalStatusTestData.Now.AddSeconds(2)
        });
        sources = OperationalStatusTestData.Sources(history);
        (await writer.ApplyAsync("tenant-a", "folder-a", "continued", sources[7..], token)).IsSuccess.ShouldBeTrue();
        var reader = new EventStoreOperationalStatusReadModel(new(store, store, clock), clock,
            new OperationalStatusSequenceReader { Head = sources.Length }, OperationalStatusTestData.Scanner);
        (await reader.GetAsync(Request(), token)).Status.ShouldBe(TaskStatusReadModelStatus.Available);
        store.Snapshot<OperationalStatusTaskCompletion>("statestore", OperationalStatusKeys.TaskCompletion("tenant-a", "folder-a", "task-a")).ShouldBeNull();
        foreach (var value in old) store.SeedRaw("statestore", value.Key, value.Value);
        (await reader.GetAsync(Request(), token)).Status.ShouldBe(TaskStatusReadModelStatus.Unavailable);
        await Should.ThrowAsync<OperationalStatusUnavailableException>(() => writer.ApplyAsync("tenant-a", "folder-a", "obsolete-historical-duplicate", [sources[6]], token));
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "source-authorized-repair", sources,
            sources.Length, token, clock: clock)).IsSuccess.ShouldBeTrue();
        (await reader.GetAsync(Request(), token)).Status.ShouldBe(TaskStatusReadModelStatus.Available);
        (await writer.ApplyAsync("tenant-a", "folder-a", "lawful-duplicate-after-repair", [sources[6]], token)).IsSuccess.ShouldBeTrue();
    }

    /// <summary>Expired Identity Is Independently Checked Before Journal And New Identity Remains Lawful.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExpiredIdentityIsIndependentlyCheckedBeforeJournalAndNewIdentityRemainsLawful(bool reused)
    {
        var token = TestContext.Current.CancellationToken;
        var store = new OperationalStatusTestStore(); var clock = new OperationalStatusClock();
        OperationalStatusEvent Source(IFolderEvent value, long sequence, string message)
        {
            var item = OperationalStatusEventReader.ReadStored(OperationalStatusTestData.Envelope(value, sequence) with { MessageId = message });
            OperationalStatusTestData.NativeSource.Remember("tenant-a", "folder-a", item.Source!); return item;
        }
        var first = Source(OperationalStatusTestData.Created(), 1, "original-message");
        FolderAccessGranted Grant(long version, DateTimeOffset at) => new("tenant-a", "tenant-a", "folder-a", FolderAccessPrincipalKind.User,
            "reader-" + version, "read_metadata", "principal-a", "correlation-a", "task-a", "key-" + version, "fp-" + version, version, at);
        var second = Source(Grant(1, OperationalStatusTestData.Now), 2, "second-message");
        var writer = new OperationalStatusProjectionWriter(store, store, store, clock, OperationalStatusTestData.Scanner);
        (await writer.ApplyAsync("tenant-a", "folder-a", "initial", [first, second], token)).IsSuccess.ShouldBeTrue();
        clock.UtcNow = OperationalStatusTestData.Now.AddDays(401);
        foreach (string key in new[] { OperationalStatusKeys.Message("tenant-a", "folder-a", first.MessageId), OperationalStatusKeys.Receipt("tenant-a", "folder-a", 1) })
        {
            var entry = await store.TryReadEtagAsync("statestore", key, token);
            (await store.TryEraseAsync("statestore", key, entry.Etag, token)).ShouldBeTrue();
        }
        var third = Source(Grant(2, clock.UtcNow), 3, reused ? first.MessageId : "new-message");
        if (reused)
            await Should.ThrowAsync<InvalidOperationException>(() => writer.ApplyAsync("tenant-a", "folder-a", "reused", [third], token));
        else
            (await writer.ApplyAsync("tenant-a", "folder-a", "new", [third], token)).IsSuccess.ShouldBeTrue();
        store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"))!
            .Sequence.ShouldBe(reused ? 2 : 3);
        if (reused) store.Snapshot<OperationalStatusReceipt>("statestore", OperationalStatusKeys.Receipt("tenant-a", "folder-a", 3)).ShouldBeNull();
        else (await writer.ApplyAsync("tenant-a", "folder-a", "exact-duplicate", [third], token)).IsSuccess.ShouldBeTrue();
    }

    /// <summary>Missing Scanner Or Unavailable Current Source Cannot Admit New Identity.</summary>
    [Fact]
    public async Task MissingScannerOrUnavailableCurrentSourceCannotAdmitNewIdentity()
    {
        var source = OperationalStatusTestData.Sources([OperationalStatusTestData.Created()]);
        var store = new OperationalStatusTestStore();
        await Should.ThrowAsync<OperationalStatusUnavailableException>(() => new OperationalStatusProjectionWriter(store, store, store)
            .ApplyAsync("tenant-a", "folder-a", "missing-scanner", source, TestContext.Current.CancellationToken));
        OperationalStatusTestData.NativeSource.Unavailable = true;
        await Should.ThrowAsync<OperationalStatusUnavailableException>(() => new OperationalStatusProjectionWriter(store, store, store,
            scanner: OperationalStatusTestData.Scanner).ApplyAsync("tenant-a", "folder-a", "unavailable-source", source, TestContext.Current.CancellationToken));
        store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a")).ShouldBeNull();
    }

    /// <summary>Conflicting Original Position Cannot Pass Native Identity Before Admission Or Expired Replay.</summary>
    [Theory]
    [InlineData("payload", false)]
    [InlineData("global", false)]
    [InlineData("message", false)]
    [InlineData("payload", true)]
    [InlineData("message", true)]
    [InlineData("global", true)]
    [InlineData("format", false)]
    [InlineData("metadata", false)]
    [InlineData("payload-version", false)]
    [InlineData("format", true)]
    [InlineData("metadata", true)]
    [InlineData("payload-version", true)]
    public async Task ConflictingOriginalPositionCannotPassNativeIdentityBeforeAdmissionOrExpiredReplay(string change, bool expired)
    {
        var token = TestContext.Current.CancellationToken;
        var sources = OperationalStatusTestData.Sources([OperationalStatusTestData.Created(),
            new FolderAccessGranted("tenant-a", "tenant-a", "folder-a", FolderAccessPrincipalKind.User, "reader", "read_metadata",
                "principal-a", "correlation-a", "task-a", "grant", "fp", 1, OperationalStatusTestData.Now)]);
        var store = new OperationalStatusTestStore(); var clock = new OperationalStatusClock();
        var writer = new OperationalStatusProjectionWriter(store, store, store, clock, OperationalStatusTestData.Scanner);
        if (expired)
        {
            (await writer.ApplyAsync("tenant-a", "folder-a", "original", sources, token)).IsSuccess.ShouldBeTrue();
            clock.UtcNow += TimeSpan.FromDays(401);
            foreach (string key in new[] { OperationalStatusKeys.Message("tenant-a", "folder-a", sources[0].MessageId), OperationalStatusKeys.Receipt("tenant-a", "folder-a", 1) })
            {
                var receipt = await store.TryReadEtagAsync("statestore", key, token);
                (await store.TryEraseAsync("statestore", key, receipt.Etag, token)).ShouldBeTrue();
            }
        }
        var original = sources[0].Source!;
        var altered = OperationalStatusEventReader.ReadStored(change switch
        {
            "payload" => original with { Payload = JsonSerializer.SerializeToUtf8Bytes(OperationalStatusTestData.Created() with { DisplayName = "conflicting" }) },
            "global" => original with { GlobalPosition = 999 },
            "format" => original with { SerializationFormat = "JSON" },
            "metadata" => original with { MetadataVersion = null },
            "payload-version" => original with { StoredPayloadVersion = 1 },
            _ => original with { MessageId = "different-position-identity" },
        });
        await Should.ThrowAsync<InvalidOperationException>(() => writer.ApplyAsync("tenant-a", "folder-a", "conflict", [altered], token));
        if (expired)
        {
            string before = JsonSerializer.Serialize(store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a")));
            (await writer.ApplyAsync("tenant-a", "folder-a", "lawful-expired-replay", [sources[0]], token)).IsSuccess.ShouldBeTrue();
            JsonSerializer.Serialize(store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"))).ShouldBe(before);
            store.Snapshot<OperationalStatusReceipt>("statestore", OperationalStatusKeys.Receipt("tenant-a", "folder-a", 1)).ShouldBeNull();
        }
        else store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a")).ShouldBeNull();
    }

    /// <summary>Historical Failed Unknown Task And Workspace Reject Coherent Old Triples.</summary>
    [Theory]
    [InlineData("failed", false)]
    [InlineData("unknown", false)]
    [InlineData("reconciliation", false)]
    [InlineData("failed", true)]
    [InlineData("unknown", true)]
    [InlineData("reconciliation", true)]
    public async Task HistoricalFailedUnknownTaskAndWorkspaceRejectCoherentOldTriples(string outcome, bool workspace)
    {
        var token = TestContext.Current.CancellationToken;
        var history = (outcome == "failed" ? Hexalith.Folders.Tests.Aggregates.Folder.FolderLifecycleReplayFixture.CommitFailureLifecycle()
            : Hexalith.Folders.Tests.Aggregates.Folder.FolderLifecycleReplayFixture.CommitUnknownOutcomeLifecycle(outcome == "reconciliation")).ToList();
        var sources = OperationalStatusTestData.Sources(history);
        var store = new OperationalStatusTestStore(); var clock = new OperationalStatusClock();
        var writer = new OperationalStatusProjectionWriter(store, store, store, clock, OperationalStatusTestData.Scanner);
        (await writer.ApplyAsync("tenant-a", "folder-a", "before-outcome", sources[..^1], token)).IsSuccess.ShouldBeTrue();
        string key = workspace ? OperationalStatusKeys.Workspace("tenant-a", "folder-a", "workspace-a") : OperationalStatusKeys.Task("tenant-a", "folder-a", "task-a");
        var old = new[] { key, OperationalStatusKeys.Evidence(key), OperationalStatusKeys.Applicability(key) }
            .ToDictionary(value => value, value => store.Snapshot<JsonNode>("statestore", value)!);
        (await writer.ApplyAsync("tenant-a", "folder-a", "outcome", sources[^1..], token)).IsSuccess.ShouldBeTrue();
        history.Add(history.OfType<WorkspacePreparationRequested>().Single() with { WorkspaceId = "workspace-b", TaskId = "task-b", OccurredAt = history[^1].OccurredAt.AddSeconds(1) });
        sources = OperationalStatusTestData.Sources(history);
        (await writer.ApplyAsync("tenant-a", "folder-a", "next-task", sources[^1..], token)).IsSuccess.ShouldBeTrue();
        var reader = new EventStoreOperationalStatusReadModel(new(store, store, clock), clock, new OperationalStatusSequenceReader { Head = sources.Length }, OperationalStatusTestData.Scanner);
        async Task<bool> Available() => workspace
            ? (await reader.GetAsync(new WorkspaceStatusReadModelRequest("tenant-a", "folder-a", "workspace-a", "reader", "read_workspace_status", "task-a", "corr", "authority", "eventually_consistent"), token).ConfigureAwait(true)).Status == WorkspaceStatusReadModelStatus.Available
            : (await reader.GetAsync(Request(), token).ConfigureAwait(true)).Status == TaskStatusReadModelStatus.Available;
        (await Available()).ShouldBeTrue();
        foreach (var value in old) store.SeedRaw("statestore", value.Key, value.Value);
        (await Available()).ShouldBeFalse();
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "historical-outcome-repair", sources, sources.Length, token, clock: clock)).IsSuccess.ShouldBeTrue();
        (await Available()).ShouldBeTrue();
        var rejection = OperationalStatusTestData.Stored(sources[0].Source! with
        {
            SequenceNumber = sources.Length + 1,
            GlobalPosition = sources.Length + 1,
            MessageId = "lawful-rejection",
            EventTypeName = "Hexalith.Folders.Server.FolderCommandRejected",
            Payload = JsonSerializer.SerializeToUtf8Bytes(new { Code = "UnsupportedCommandType", CommandType = "future_command" })
        });
        (await writer.ApplyAsync("tenant-a", "folder-a", "lawful-after-repair", [rejection], token)).IsSuccess.ShouldBeTrue();
    }

    /// <summary>Changed Native Predecessor Cannot Be Stitched To Existing Checkpoint.</summary>
    [Fact]
    public async Task ChangedNativePredecessorCannotBeStitchedToExistingCheckpoint()
    {
        var token = TestContext.Current.CancellationToken;
        var first = OperationalStatusTestData.Sources([OperationalStatusTestData.Created()])[0];
        var store = new OperationalStatusTestStore(); var writer = new OperationalStatusProjectionWriter(store, store, store, scanner: OperationalStatusTestData.Scanner);
        (await writer.ApplyAsync("tenant-a", "folder-a", "first", [first], token)).IsSuccess.ShouldBeTrue();
        OperationalStatusTestData.NativeSource.Remember("tenant-a", "folder-a", first.Source! with { Payload = JsonSerializer.SerializeToUtf8Bytes(OperationalStatusTestData.Created() with { DisplayName = "changed-native-prefix" }) }, force: true);
        var next = OperationalStatusTestData.Stored(first.Source! with
        {
            SequenceNumber = 2,
            GlobalPosition = 2,
            MessageId = "next-message",
            EventTypeName = "Hexalith.Folders.Server.FolderCommandRejected",
            Payload = JsonSerializer.SerializeToUtf8Bytes(new { Code = "UnsupportedCommandType", CommandType = "future_command" })
        });
        await Should.ThrowAsync<InvalidOperationException>(() => writer.ApplyAsync("tenant-a", "folder-a", "changed-prefix", [next], token));
        store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"))!.Sequence.ShouldBe(1);
    }

    /// <summary>Inherited Historical Selection Rechecks Generation And Current Head After Scan.</summary>
    [Fact]
    public async Task InheritedHistoricalSelectionRechecksGenerationAndCurrentHeadAfterScan()
    {
        var token = TestContext.Current.CancellationToken;
        var history = OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now).ToList();
        history.Add(history.OfType<WorkspacePreparationRequested>().Single() with { WorkspaceId = "workspace-b", TaskId = "task-b" });
        var sources = OperationalStatusTestData.Sources(history); var store = new OperationalStatusTestStore(); var clock = new OperationalStatusClock();
        var writer = new OperationalStatusProjectionWriter(store, store, store, clock, OperationalStatusTestData.Scanner);
        (await writer.ApplyAsync("tenant-a", "folder-a", "initial", sources, token)).IsSuccess.ShouldBeTrue();
        var other = OperationalStatusTestData.Sources([OperationalStatusTestData.Created(folder: "folder-b")]);
        other[0] = OperationalStatusTestData.Stored(other[0].Source! with { GlobalPosition = sources.Length + 1 });
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-b", "inherit", other, sources.Length + 1, token, clock: clock)).IsSuccess.ShouldBeTrue();
        var sequence = new OperationalStatusSequenceReader { Head = sources.Length };
        var reader = new EventStoreOperationalStatusReadModel(new(store, store, clock), clock, sequence, OperationalStatusTestData.Scanner);
        (await reader.GetAsync(Request(), token)).Status.ShouldBe(TaskStatusReadModelStatus.Available);
        OperationalStatusTestData.NativeSource.OnRead = () =>
        {
            OperationalStatusTestData.NativeSource.OnRead = null;
            NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-b", "mid-scan-promotion", other,
                sources.Length + 1, token, clock: clock).GetAwaiter().GetResult().IsSuccess.ShouldBeTrue();
        };
        (await reader.GetAsync(Request(), token)).Status.ShouldBe(TaskStatusReadModelStatus.Unavailable);
        (await reader.GetAsync(Request(), token)).Status.ShouldBe(TaskStatusReadModelStatus.Available);
        OperationalStatusEvent? growth = null;
        OperationalStatusTestData.NativeSource.OnRead = () =>
        {
            OperationalStatusTestData.NativeSource.OnRead = null;
            growth = OperationalStatusTestData.Stored(sources[0].Source! with
            {
                SequenceNumber = sources.Length + 1,
                GlobalPosition = sources.Length + 2,
                MessageId = "mid-scan-growth",
                EventTypeName = "Hexalith.Folders.Server.FolderCommandRejected",
                Payload = JsonSerializer.SerializeToUtf8Bytes(new { Code = "UnsupportedCommandType", CommandType = "future_command" })
            });
        };
        (await reader.GetAsync(Request(), token)).Status.ShouldBe(TaskStatusReadModelStatus.Unavailable);
        sequence.Head++;
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "growth-repair", [.. sources, growth!], sources.Length + 2, token, clock: clock)).IsSuccess.ShouldBeTrue();
        (await reader.GetAsync(Request(), token)).Status.ShouldBe(TaskStatusReadModelStatus.Available);
        using var caller = new CancellationTokenSource();
        OperationalStatusTestData.NativeSource.OnRead = () => { OperationalStatusTestData.NativeSource.OnRead = null; caller.Cancel(); };
        await Should.ThrowAsync<OperationCanceledException>(() => reader.GetAsync(Request(), caller.Token));
        (await reader.GetAsync(Request(), token)).Status.ShouldBe(TaskStatusReadModelStatus.Available);
    }

    /// <summary>Historical Source Fold Keeps Earliest Completion Across Repeated Task Bindings.</summary>
    [Fact]
    public async Task HistoricalSourceFoldKeepsEarliestCompletionAcrossRepeatedTaskBindings()
    {
        var token = TestContext.Current.CancellationToken;
        var history = OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now).ToList();
        var prepared = history.OfType<WorkspacePreparationRequested>().Single(); var locked = history.OfType<WorkspaceLockAcquired>().Single();
        var released = history.OfType<WorkspaceLockReleased>().Single();
        history.Add(prepared with { WorkspaceId = "workspace-b", OccurredAt = OperationalStatusTestData.Now.AddSeconds(1) });
        history.Add(locked with
        {
            WorkspaceId = "workspace-b",
            LockId = "lock-b",
            OccurredAt = OperationalStatusTestData.Now.AddSeconds(2),
            AcquiredAt = OperationalStatusTestData.Now.AddSeconds(2),
            EffectiveAt = OperationalStatusTestData.Now.AddSeconds(2),
            ExpiresAt = OperationalStatusTestData.Now.AddHours(1).AddSeconds(2)
        });
        history.Add(released with { WorkspaceId = "workspace-b", LockId = "lock-b", OccurredAt = OperationalStatusTestData.Now.AddSeconds(3) });
        history.Add(prepared with { WorkspaceId = "workspace-c", TaskId = "task-c", OccurredAt = OperationalStatusTestData.Now.AddSeconds(4) });
        var source = OperationalStatusTestData.Sources(history); var store = new OperationalStatusTestStore(); var clock = new OperationalStatusClock();
        var writer = new OperationalStatusProjectionWriter(store, store, store, clock, OperationalStatusTestData.Scanner);
        (await writer.ApplyAsync("tenant-a", "folder-a", "reused-task", source, token)).IsSuccess.ShouldBeTrue();
        var reader = new EventStoreOperationalStatusReadModel(new(store, store, clock), clock, new OperationalStatusSequenceReader { Head = source.Length }, OperationalStatusTestData.Scanner);
        var result = await reader.GetAsync(Request(), token);
        result.Status.ShouldBe(TaskStatusReadModelStatus.Available);
        result.Snapshot!.LastOperationId.ShouldBe("lock-a"); result.Snapshot.Freshness.ProjectionWatermark.ShouldBe("8");
        result.Snapshot.Freshness.ObservedAt.ShouldBe(OperationalStatusTestData.Now);
        clock.UtcNow += OperationalStatusKeys.Retention;
        (await reader.GetAsync(Request(), token)).Status.ShouldBe(TaskStatusReadModelStatus.NotFound);
    }

    /// <summary>A shared persistence/domain clock crossing natural C3 expiry during a historical scan yields missing immediately.</summary>
    [Fact]
    public async Task NaturalHistoricalExpiryDuringScanIsMissingOnTheFirstRead()
    {
        var token = TestContext.Current.CancellationToken;
        var clock = new OperationalStatusClock(); var store = new OperationalStatusTestStore(); store.BatchOptions.TimeProvider = clock;
        var history = OperationalStatusTestData.LockedPrefix(OperationalStatusTestData.Now).Take(6).ToList();
        var acquire = new WorkspaceNativeTaskChanged("tenant-a", "tenant-a", "folder-a", "workspace-a", "acquire", "native-lock-1", 1, 60,
            OperationalStatusTestData.Now.AddSeconds(1), OperationalStatusTestData.Now.AddSeconds(61), null,
            "principal-a", "correlation-a", "task-a", "native-acquire-1", "fp", OperationalStatusTestData.Now.AddSeconds(1));
        history.Add(acquire);
        history.Add(acquire with { Operation = "release", ExpiresAt = acquire.EffectiveAt, IdempotencyKey = "native-release" });
        history.Add(acquire with
        {
            TaskId = "task-b",
            LockId = "native-lock-2",
            LockFence = 2,
            IdempotencyKey = "native-acquire-2",
            EffectiveAt = OperationalStatusTestData.Now.AddSeconds(2),
            ExpiresAt = OperationalStatusTestData.Now.AddSeconds(62),
            OccurredAt = OperationalStatusTestData.Now.AddSeconds(2)
        });
        var sources = OperationalStatusTestData.Sources(history);
        (await new OperationalStatusProjectionWriter(store, store, store, clock, OperationalStatusTestData.Scanner)
            .ApplyAsync("tenant-a", "folder-a", "source", sources, token)).IsSuccess.ShouldBeTrue();
        string key = OperationalStatusKeys.Task("tenant-a", "folder-a", "task-a");
        DateTimeOffset expiry = store.Snapshot<TaskStatusReadModelSnapshot>("statestore", key)!.Freshness.ObservedAt + OperationalStatusKeys.Retention;
        clock.UtcNow = expiry.AddTicks(-1);
        var reader = new EventStoreOperationalStatusReadModel(new(store, store, clock), clock,
            new OperationalStatusSequenceReader { Head = sources.Length }, OperationalStatusTestData.Scanner);
        (await reader.GetAsync(Request(), token)).Status.ShouldBe(TaskStatusReadModelStatus.Available);
        OperationalStatusTestData.NativeSource.OnRead = () => { OperationalStatusTestData.NativeSource.OnRead = null; clock.UtcNow = expiry; };
        (await reader.GetAsync(Request(), token)).Status.ShouldBe(TaskStatusReadModelStatus.NotFound);
        store.Snapshot<TaskStatusReadModelSnapshot>("statestore", key).ShouldBeNull();
        (await reader.GetAsync(Request(), token)).Status.ShouldBe(TaskStatusReadModelStatus.NotFound);
    }

    private static TaskStatusReadModelRequest Request() => new("tenant-a", "task-a", "reader", "read_task_status", "correlation", "read_your_writes")
    { AuthorizedFolderId = "folder-a", AuthorizationWatermark = "authority" };
}
