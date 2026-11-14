using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Projections.OperationalStatus;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Tests.OperationalStatus;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Projections.OperationalStatus;

/// <summary>Checks retained source authority before duplicate delivery acknowledgment.</summary>
public sealed class OperationalStatusRoundSevenTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HistoricalDuplicateCannotAcknowledgeCoherentPreclosureViewsAsync(bool workspace)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        OperationalStatusTestStore store = new();
        OperationalStatusClock clock = new();
        OperationalStatusProjectionWriter writer = new(store, store, store, clock);
        List<IFolderEvent> history = [.. OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now)];
        var source = OperationalStatusTestData.Sources(history);
        (await writer.ApplyAsync("tenant-a", "folder-a", "locked", source[..^1], token)).IsSuccess.ShouldBeTrue();
        string key = workspace ? OperationalStatusKeys.Workspace("tenant-a", "folder-a", "workspace-a")
            : OperationalStatusKeys.Task("tenant-a", "folder-a", "task-a");
        string[] keys = [key, OperationalStatusKeys.Evidence(key), OperationalStatusKeys.Applicability(key)];
        var old = keys.ToDictionary(value => value, value => store.Snapshot<JsonNode>("statestore", value)!);
        (await writer.ApplyAsync("tenant-a", "folder-a", "closed", source[^1..], token)).IsSuccess.ShouldBeTrue();
        history.Add(history.OfType<WorkspacePreparationRequested>().Single() with
        { WorkspaceId = "workspace-b", TaskId = "task-b", OccurredAt = OperationalStatusTestData.Now.AddSeconds(1) });
        source = OperationalStatusTestData.Sources(history);
        (await writer.ApplyAsync("tenant-a", "folder-a", "next-task", source[^1..], token)).IsSuccess.ShouldBeTrue();
        foreach (var pair in old) store.SeedRaw("statestore", pair.Key, pair.Value);
        await Should.ThrowAsync<OperationalStatusUnavailableException>(() =>
            writer.ApplyAsync("tenant-a", "folder-a", "original-acquisition", [source[6]], token));
        store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"))!
            .Sequence.ShouldBe(source.Length);
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "repair-original-authority",
            source, source.Length, token, clock: clock)).IsSuccess.ShouldBeTrue();
        (await writer.ApplyAsync("tenant-a", "folder-a", "repaired-duplicate", [source[6]], token)).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExpiredTailReplayStillRequiresItsRetainedIdentityAsync(bool changedPayload)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        OperationalStatusTestStore store = new();
        OperationalStatusClock clock = new();
        OperationalStatusProjectionWriter writer = new(store, store, store, clock);
        var original = OperationalStatusTestData.Sources([OperationalStatusTestData.Created()])[0];
        (await writer.ApplyAsync("tenant-a", "folder-a", "original", [original], token)).IsSuccess.ShouldBeTrue();
        clock.UtcNow += OperationalStatusKeys.Retention + TimeSpan.FromDays(1);
        var retained = original.Source!;
        var conflict = OperationalStatusEventReader.ReadStored(changedPayload
            ? retained with { Payload = JsonSerializer.SerializeToUtf8Bytes(OperationalStatusTestData.Created() with { DisplayName = "Changed" }) }
            : retained with { MessageId = "different-tail-message" });
        await Should.ThrowAsync<InvalidOperationException>(() => writer.ApplyAsync("tenant-a", "folder-a", "conflict", [conflict], token));
        (await writer.ApplyAsync("tenant-a", "folder-a", "same-expired-tail", [original], token)).IsSuccess.ShouldBeTrue();
        store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"))!
            .MessageIds[1].ShouldBe(original.MessageId);
    }

    [Theory]
    [InlineData("Accepted")]
    [InlineData("Created")]
    [InlineData("IdempotentReplay")]
    [InlineData("AlreadyApplied")]
    public async Task SuccessCodesNeverAdvanceRejectionReceiptsAsync(string code)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        OperationalStatusTestStore store = new();
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new { CommandType = "future_command", Code = code });
        Should.Throw<InvalidOperationException>(() => FoldersRejectionSource.ReadOwner(bytes, "tenant-a", "folder-a", "correlation-a"));
        var source = OperationalStatusTestData.Sources([OperationalStatusTestData.Created()])[0].Source! with
        { EventTypeName = "Hexalith.Folders.Server.FolderCommandRejected", Payload = bytes };
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await new OperationalStatusProjectionWriter(store, store, store).ApplyAsync("tenant-a", "folder-a", "invalid",
                [OperationalStatusEventReader.ReadStored(source)], token).ConfigureAwait(true));
        store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a")).ShouldBeNull();
    }
}
