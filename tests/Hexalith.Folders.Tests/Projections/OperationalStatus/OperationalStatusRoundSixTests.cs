using Hexalith.EventStore.Client.Projections;
using Hexalith.Folders.Projections.OperationalStatus;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Tests.OperationalStatus;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Projections.OperationalStatus;

/// <summary>Exercises current applicability and source-owned terminal repair.</summary>
public sealed class OperationalStatusRoundSixTests
{
    [Fact]
    public async Task MatchingForgedTerminalProofCannotOverrideIndependentSourceCompletionDuringDeliveryAsync()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        OperationalStatusTestStore store = new();
        OperationalStatusClock clock = new();
        OperationalStatusProjectionWriter writer = new(store, store, store, clock, scanner: Hexalith.Folders.Tests.OperationalStatus.OperationalStatusTestData.Scanner);
        List<Hexalith.Folders.Aggregates.Folder.IFolderEvent> history = [.. OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now)];
        var source = OperationalStatusTestData.Sources(history);
        (await writer.ApplyAsync("tenant-a", "folder-a", "closed", source, token)).IsSuccess.ShouldBeTrue();
        string key = OperationalStatusKeys.Task("tenant-a", "folder-a", "task-a");
        var forged = store.Snapshot<TaskStatusReadModelSnapshot>("statestore", key)! with { LastOperationId = "forged-operation" };
        var evidence = store.Snapshot<OperationalStatusViewEvidence>("statestore", OperationalStatusKeys.Evidence(key))! with
        { ValueDigest = OperationalStatusIntegrity.Digest(forged) };
        store.SeedRaw("statestore", key, forged);
        store.SeedRaw("statestore", OperationalStatusKeys.Evidence(key), evidence);
        store.SeedRaw("statestore", OperationalStatusKeys.Applicability(key), OperationalStatusViewApplicability.Create(key, evidence));
        history.Add(RoundFiveOperationalHistory.Grant(OperationalStatusTestData.Now.AddSeconds(1)));
        var grown = OperationalStatusTestData.Sources(history);
        await Should.ThrowAsync<OperationalStatusUnavailableException>(() => writer.ApplyAsync("tenant-a", "folder-a", "next-policy", [grown[^1]], token));
        store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"))!.Sequence.ShouldBe(source.Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CoherentOlderApplicabilityCannotOverrideCurrentOrHistoricalSourceCompletionAsync(bool historical)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        OperationalStatusTestStore store = new();
        OperationalStatusClock clock = new();
        OperationalStatusProjectionWriter writer = new(store, store, store, clock, scanner: Hexalith.Folders.Tests.OperationalStatus.OperationalStatusTestData.Scanner);
        List<Hexalith.Folders.Aggregates.Folder.IFolderEvent> history = [.. OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now)];
        var source = OperationalStatusTestData.Sources(history);
        (await writer.ApplyAsync("tenant-a", "folder-a", "locked", source[..^1], token)).IsSuccess.ShouldBeTrue();
        string taskKey = OperationalStatusKeys.Task("tenant-a", "folder-a", "task-a");
        string workspaceKey = OperationalStatusKeys.Workspace("tenant-a", "folder-a", "workspace-a");
        string[] keys = [taskKey, workspaceKey, OperationalStatusKeys.Evidence(taskKey), OperationalStatusKeys.Evidence(workspaceKey),
            OperationalStatusKeys.Applicability(taskKey), OperationalStatusKeys.Applicability(workspaceKey)];
        var old = keys.ToDictionary(key => key, key => store.Snapshot<System.Text.Json.Nodes.JsonNode>("statestore", key)!);
        (await writer.ApplyAsync("tenant-a", "folder-a", "closed", [source[^1]], token)).IsSuccess.ShouldBeTrue();
        if (historical)
        {
            history.Add(history.OfType<Hexalith.Folders.Aggregates.Folder.WorkspacePreparationRequested>().Single() with
            { WorkspaceId = "workspace-b", TaskId = "task-b", OccurredAt = OperationalStatusTestData.Now.AddSeconds(1) });
            source = OperationalStatusTestData.Sources(history);
            (await writer.ApplyAsync("tenant-a", "folder-a", "next-workspace", [source[^1]], token)).IsSuccess.ShouldBeTrue();
        }
        foreach (var pair in old) store.SeedRaw("statestore", pair.Key, pair.Value);
        (await Reader(store, clock, source.Length).GetAsync(TaskRequest(), token)).Status.ShouldBe(TaskStatusReadModelStatus.Unavailable);
        (await Reader(store, clock, source.Length).GetAsync(WorkspaceRequest(), token)).Status.ShouldBe(WorkspaceStatusReadModelStatus.Unavailable);
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "all-authority-repair", source, source.Length, token, clock: clock)).IsSuccess.ShouldBeTrue();
        (await Reader(store, clock, source.Length).GetAsync(TaskRequest(), token)).Snapshot!.TerminalState.ShouldBe("ready");
        (await Reader(store, clock, source.Length).GetAsync(WorkspaceRequest(), token)).Status.ShouldBe(WorkspaceStatusReadModelStatus.Available);
        var checkpoint = store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"))!;
        checkpoint.TaskEvidence.ShouldHaveSingleItem();
        checkpoint.WorkspaceEvidence.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task MalformedSurvivingApplicabilityCannotOverridePairedAbsenceOrProvenExpiryAsync(bool expired, bool scalar)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        OperationalStatusTestStore store = new();
        OperationalStatusClock clock = new();
        var source = OperationalStatusTestData.Sources(OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now));
        (await new OperationalStatusProjectionWriter(store, store, store, clock, scanner: Hexalith.Folders.Tests.OperationalStatus.OperationalStatusTestData.Scanner).ApplyAsync("tenant-a", "folder-a", "initial", source, token)).IsSuccess.ShouldBeTrue();
        string key = OperationalStatusKeys.Task("tenant-a", "folder-a", "task-a");
        var malformed = System.Text.Json.Nodes.JsonNode.Parse(scalar ? "\"invalid\"" : "{\"sequence\":\"invalid\"}")!;
        store.SeedRaw("statestore", OperationalStatusKeys.Applicability(key), malformed);
        if (expired) clock.UtcNow = OperationalStatusTestData.Now + OperationalStatusKeys.Retention;
        else
        {
            foreach (string removed in new[] { key, OperationalStatusKeys.Evidence(key) })
            {
                var row = await store.TryReadEtagAsync("statestore", removed, token);
                (await store.TryEraseAsync("statestore", removed, row.Etag, token)).ShouldBeTrue();
            }
        }
        (await Reader(store, clock, source.Length).GetAsync(TaskRequest(), token)).Status.ShouldBe(TaskStatusReadModelStatus.NotFound);
        (await Reader(store, clock, source.Length + 1).GetAsync(TaskRequest(), token)).Status
            .ShouldBe(TaskStatusReadModelStatus.Stale); // SDK-expired pairs are absent; a later unprojected head still requires freshness.
        store.SeedRaw("statestore", OperationalStatusKeys.Applicability(OperationalStatusKeys.Lifecycle("tenant-a", "folder-a")), malformed);
        (await Reader(store, clock, source.Length).GetAsync(new FolderLifecycleStatusReadModelRequest("tenant-a", "folder-a", "reader", "read_metadata", null, "correlation", "authority", "eventually_consistent"), token)).Status
            .ShouldBe(expired ? FolderLifecycleStatusReadModelStatus.NotFound : FolderLifecycleStatusReadModelStatus.Available);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrCorruptApplicabilityRequiresCompleteSourceRepairAndKeepsOriginalDeadlineAsync(bool corrupt)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        OperationalStatusTestStore store = new();
        OperationalStatusClock clock = new();
        var source = OperationalStatusTestData.Sources(OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now));
        OperationalStatusProjectionWriter writer = new(store, store, store, clock, scanner: Hexalith.Folders.Tests.OperationalStatus.OperationalStatusTestData.Scanner);
        (await writer.ApplyAsync("tenant-a", "folder-a", "initial", source, token)).IsSuccess.ShouldBeTrue();
        string key = OperationalStatusKeys.Task("tenant-a", "folder-a", "task-a");
        string authorityKey = OperationalStatusKeys.Applicability(key);
        var original = store.Snapshot<OperationalStatusViewApplicability>("statestore", authorityKey)!;
        if (corrupt) store.SeedRaw("statestore", authorityKey, original with { Sequence = source.Length - 1 });
        else
        {
            var retained = await store.TryReadEtagAsync("statestore", authorityKey, token);
            (await store.TryEraseAsync("statestore", authorityKey, retained.Etag, token)).ShouldBeTrue();
        }
        (await Reader(store, clock, source.Length).GetAsync(TaskRequest(), token)).Status.ShouldBe(TaskStatusReadModelStatus.Unavailable);
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "authority-repair", source,
            source.Length, token, clock: clock)).IsSuccess.ShouldBeTrue();
        store.Snapshot<OperationalStatusViewApplicability>("statestore", authorityKey).ShouldBe(original);
        (await Reader(store, clock, source.Length).GetAsync(TaskRequest(), token)).Status.ShouldBe(TaskStatusReadModelStatus.Available);
        var plan = await writer.PrepareRebuildAsync("tenant-a", "folder-a", source, source.Length, token);
        plan.Single(item => item.Key == authorityKey).ExpiresAtUtc.ShouldBe(OperationalStatusTestData.Now + OperationalStatusKeys.Retention);
        clock.UtcNow = OperationalStatusTestData.Now + OperationalStatusKeys.Retention;
        (await Reader(store, clock, source.Length).GetAsync(TaskRequest(), token)).Status.ShouldBe(TaskStatusReadModelStatus.NotFound);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CheckpointOrGenerationChangeBetweenPinnedReadsCannotPublishMixedAuthorityAsync(bool promotion)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        OperationalStatusTestStore store = new();
        OperationalStatusClock clock = new();
        OperationalStatusProjectionWriter writer = new(store, store, store, clock, scanner: Hexalith.Folders.Tests.OperationalStatus.OperationalStatusTestData.Scanner);
        var source = OperationalStatusTestData.Sources(OperationalStatusTestData.LockedPrefix(OperationalStatusTestData.Now));
        (await writer.ApplyAsync("tenant-a", "folder-a", "initial", source, token)).IsSuccess.ShouldBeTrue();
        string receipt = store.PhysicalKey(OperationalStatusKeys.Receipt("tenant-a", "folder-a", source.Length));
        var rejection = source[^1].Source! with
        {
            SequenceNumber = source.Length + 1, GlobalPosition = source.Length + 1, MessageId = "later-rejection",
            EventTypeName = "Hexalith.Folders.Server.FolderCommandRejected",
            Payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { Code = "UnsupportedCommandType", CommandType = "future_command" }),
        };
        OperationalStatusReadInterleavingStore reads = new(store, receipt, async () =>
        {
            if (promotion)
                (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "mid-read-promotion", source,
                    source.Length, token, clock: clock).ConfigureAwait(true)).IsSuccess.ShouldBeTrue();
            else
                (await writer.ApplyAsync("tenant-a", "folder-a", "mid-read-rejection", [OperationalStatusTestData.Stored(rejection)], token).ConfigureAwait(true)).IsSuccess.ShouldBeTrue();
        });
        var reader = new EventStoreOperationalStatusReadModel(new(reads, store, clock), clock,
            new OperationalStatusSequenceReader { Head = promotion ? source.Length : source.Length + 1 }, scanner: Hexalith.Folders.Tests.OperationalStatus.OperationalStatusTestData.Scanner);
        (await reader.GetAsync(TaskRequest(), token)).Status.ShouldBe(TaskStatusReadModelStatus.Unavailable);
        (await reader.GetAsync(TaskRequest(), token)).Status.ShouldBe(TaskStatusReadModelStatus.Available);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnknownAndTerminalResultsKeepApplicabilityAcrossRejectionsAndInheritedGenerationsAsync(bool terminal)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        OperationalStatusTestStore store = new();
        OperationalStatusClock clock = new();
        var history = terminal ? OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now)
            : Hexalith.Folders.Tests.Aggregates.Folder.FolderLifecycleReplayFixture.CommitUnknownOutcomeLifecycle(true);
        var source = OperationalStatusTestData.Sources(history);
        OperationalStatusProjectionWriter writer = new(store, store, store, clock, scanner: Hexalith.Folders.Tests.OperationalStatus.OperationalStatusTestData.Scanner);
        (await writer.ApplyAsync("tenant-a", "folder-a", "initial", source, token)).IsSuccess.ShouldBeTrue();
        var before = (await Reader(store, clock, source.Length).GetAsync(TaskRequest(), token)).Snapshot!;
        var rejection = source[^1].Source! with
        {
            SequenceNumber = source.Length + 1, GlobalPosition = source.Length + 1, MessageId = "pure-rejection",
            EventTypeName = "Hexalith.Folders.Server.FolderCommandRejected",
            Payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { Code = "UnsupportedCommandType", CommandType = "future_command" }),
        };
        (await writer.ApplyAsync("tenant-a", "folder-a", "pure-rejection", [OperationalStatusTestData.Stored(rejection)], token)).IsSuccess.ShouldBeTrue();
        var other = OperationalStatusTestData.Sources([OperationalStatusTestData.Created(folder: "folder-b")]);
        other[0] = OperationalStatusTestData.Stored(other[0].Source! with { GlobalPosition = source.Length + 2, MessageId = "other-owner" });
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-b", "inherited-generation", other,
            source.Length + 2, token, clock: clock)).IsSuccess.ShouldBeTrue();
        var after = await Reader(store, clock, source.Length + 1).GetAsync(TaskRequest(), token);
        after.Status.ShouldBe(TaskStatusReadModelStatus.Available);
        after.Snapshot.ShouldBe(before);
    }

    [Fact]
    public async Task CoherentPriorTaskAndWorkspacePairsCannotRollbackTheCurrentClosureAsync()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        OperationalStatusTestStore store = new();
        OperationalStatusClock clock = new();
        OperationalStatusProjectionWriter writer = new(store, store, store, clock, scanner: Hexalith.Folders.Tests.OperationalStatus.OperationalStatusTestData.Scanner);
        var source = OperationalStatusTestData.Sources(OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now));
        (await writer.ApplyAsync("tenant-a", "folder-a", "before-close", source[..^1], token)).IsSuccess.ShouldBeTrue();
        string taskKey = OperationalStatusKeys.Task("tenant-a", "folder-a", "task-a");
        string workspaceKey = OperationalStatusKeys.Workspace("tenant-a", "folder-a", "workspace-a");
        var oldTask = store.Snapshot<TaskStatusReadModelSnapshot>("statestore", taskKey)!;
        var oldWorkspace = store.Snapshot<OperationalWorkspaceStatus>("statestore", workspaceKey)!;
        var taskEvidence = store.Snapshot<OperationalStatusViewEvidence>("statestore", OperationalStatusKeys.Evidence(taskKey))!;
        var workspaceEvidence = store.Snapshot<OperationalStatusViewEvidence>("statestore", OperationalStatusKeys.Evidence(workspaceKey))!;
        (await writer.ApplyAsync("tenant-a", "folder-a", "close", [source[^1]], token)).IsSuccess.ShouldBeTrue();
        store.SeedRaw("statestore", taskKey, oldTask);
        store.SeedRaw("statestore", OperationalStatusKeys.Evidence(taskKey), taskEvidence);
        store.SeedRaw("statestore", workspaceKey, oldWorkspace);
        store.SeedRaw("statestore", OperationalStatusKeys.Evidence(workspaceKey), workspaceEvidence);
        var reader = Reader(store, clock, source.Length);
        (await reader.GetAsync(TaskRequest(), token)).Status.ShouldBe(TaskStatusReadModelStatus.Unavailable);
        (await reader.GetAsync(WorkspaceRequest(), token)).Status.ShouldBe(WorkspaceStatusReadModelStatus.Unavailable);
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "source-repair", source,
            source.Length, token, clock: clock)).IsSuccess.ShouldBeTrue();
        (await reader.GetAsync(TaskRequest(), token)).Snapshot!.TerminalState.ShouldBe("ready");
        (await reader.GetAsync(WorkspaceRequest(), token)).Status.ShouldBe(WorkspaceStatusReadModelStatus.Available);
    }

    [Fact]
    public async Task MissingPriorEvidenceCannotAttestForgedTerminalFieldsAsync()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        OperationalStatusTestStore store = new();
        OperationalStatusClock clock = new();
        OperationalStatusProjectionWriter writer = new(store, store, store, clock, scanner: Hexalith.Folders.Tests.OperationalStatus.OperationalStatusTestData.Scanner);
        List<Hexalith.Folders.Aggregates.Folder.IFolderEvent> history = [.. OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now)];
        var source = OperationalStatusTestData.Sources(history);
        (await writer.ApplyAsync("tenant-a", "folder-a", "closed", source, token)).IsSuccess.ShouldBeTrue();
        string key = OperationalStatusKeys.Task("tenant-a", "folder-a", "task-a");
        var original = store.Snapshot<TaskStatusReadModelSnapshot>("statestore", key)!;
        store.SeedRaw("statestore", key, original with { LastOperationId = "forged-operation" });
        var evidence = await store.TryReadEtagAsync("statestore", OperationalStatusKeys.Evidence(key), token);
        (await store.TryEraseAsync("statestore", OperationalStatusKeys.Evidence(key), evidence.Etag, token)).ShouldBeTrue();
        history.Add(RoundFiveOperationalHistory.Grant(OperationalStatusTestData.Now.AddSeconds(1)));
        var grown = OperationalStatusTestData.Sources(history);
        (await writer.ApplyAsync("tenant-a", "folder-a", "next-policy", [grown[^1]], token)).IsSuccess.ShouldBeTrue();
        store.Snapshot<TaskStatusReadModelSnapshot>("statestore", key).ShouldBe(original);
        (await Reader(store, clock, grown.Length).GetAsync(TaskRequest(), token)).Snapshot!.LastOperationId.ShouldBe(original.LastOperationId);
    }

    private static EventStoreOperationalStatusReadModel Reader(OperationalStatusTestStore store, OperationalStatusClock clock, long head)
        => new(new SharedProjectionEpochCoordinator(store, store, clock), clock, new OperationalStatusSequenceReader { Head = head }, scanner: Hexalith.Folders.Tests.OperationalStatus.OperationalStatusTestData.Scanner);

    private static TaskStatusReadModelRequest TaskRequest()
        => new("tenant-a", "task-a", "reader", "read_task_status", "correlation", "read_your_writes")
        { AuthorizedFolderId = "folder-a", AuthorizationWatermark = "authority" };

    private static WorkspaceStatusReadModelRequest WorkspaceRequest()
        => new("tenant-a", "folder-a", "workspace-a", "reader", "read_workspace_status", null, "correlation", "authority", "eventually_consistent");
}
