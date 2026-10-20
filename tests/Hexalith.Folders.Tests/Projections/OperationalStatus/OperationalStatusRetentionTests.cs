using Hexalith.EventStore.Client.Projections;
using Hexalith.Folders.Projections.OperationalStatus;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Tests.OperationalStatus;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Projections.OperationalStatus;

/// <summary>Exercises source-time expiry through the real SDK selected-generation reader.</summary>
public sealed class OperationalStatusRetentionTests
{
    [Fact]
    public async Task BoundaryExpiresViewsEvidenceAndPositionRowsWithoutRefreshingTerminalMetadataAsync()
    {
        OperationalStatusTestStore store = new();
        OperationalStatusClock clock = new() { UtcNow = OperationalStatusTestData.Now.AddDays(399) };
        var sources = OperationalStatusTestData.Sources(OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now));
        OperationalStatusProjectionWriter writer = new(store, store, store, clock);
        var operations = await writer.PrepareRebuildAsync("tenant-a", "folder-a", sources, sources.Length, TestContext.Current.CancellationToken);
        operations.Where(item => item.Key != OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"))
            .ShouldAllBe(item => item.TimeToLive == TimeSpan.FromDays(1));
        operations.Single(item => item.Key == OperationalStatusKeys.Checkpoint("tenant-a", "folder-a")).TimeToLive.ShouldBeNull();
        (await writer.ApplyAsync("tenant-a", "folder-a", "initial", sources, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var reader = Reader(store, clock, sources.Length);
        clock.UtcNow = OperationalStatusTestData.Now + OperationalStatusKeys.Retention - TimeSpan.FromTicks(1);
        (await reader.GetAsync(TaskRequest(), TestContext.Current.CancellationToken)).Status.ShouldBe(TaskStatusReadModelStatus.Available);
        clock.UtcNow = OperationalStatusTestData.Now + OperationalStatusKeys.Retention;
        (await reader.GetAsync(TaskRequest(), TestContext.Current.CancellationToken)).Status.ShouldBe(TaskStatusReadModelStatus.NotFound);
        (await reader.GetAsync(LifecycleRequest(), TestContext.Current.CancellationToken)).Status.ShouldBe(FolderLifecycleStatusReadModelStatus.NotFound);
        int before = store.Count;
        (await writer.ApplyAsync("tenant-a", "folder-a", "expired-duplicate", [sources[^1]], TestContext.Current.CancellationToken))
            .Status.ShouldBe(ReadModelBatchStatus.AlreadyCompleted);
        store.Count.ShouldBe(before);
        (await writer.PrepareRebuildAsync("tenant-a", "folder-a", sources, sources.Length, TestContext.Current.CancellationToken))
            .ShouldHaveSingleItem().Key.ShouldBe(OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"));
    }

    [Fact]
    public async Task PhysicalExpiryOfBothViewAndEvidenceIsMissingWhileSurvivingEvidenceIsUnavailableAsync()
    {
        OperationalStatusTestStore store = new();
        OperationalStatusClock clock = new();
        var sources = OperationalStatusTestData.Sources(OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now));
        (await new OperationalStatusProjectionWriter(store, store, store, clock).ApplyAsync("tenant-a", "folder-a", "initial",
            sources, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var reader = Reader(store, clock, sources.Length);
        string task = OperationalStatusKeys.Task("tenant-a", "folder-a", "task-a");
        await EraseAsync(store, task);
        (await reader.GetAsync(TaskRequest(), TestContext.Current.CancellationToken)).Status.ShouldBe(TaskStatusReadModelStatus.Unavailable);
        await EraseAsync(store, OperationalStatusKeys.Evidence(task));
        (await reader.GetAsync(TaskRequest(), TestContext.Current.CancellationToken)).Status.ShouldBe(TaskStatusReadModelStatus.NotFound);
        clock.UtcNow = OperationalStatusTestData.Now + OperationalStatusKeys.Retention;
        await EraseAsync(store, OperationalStatusKeys.Lifecycle("tenant-a", "folder-a"));
        await EraseAsync(store, OperationalStatusKeys.Evidence(OperationalStatusKeys.Lifecycle("tenant-a", "folder-a")));
        (await reader.GetAsync(LifecycleRequest(), TestContext.Current.CancellationToken)).Status.ShouldBe(FolderLifecycleStatusReadModelStatus.NotFound);
    }

    [Theory]
    [InlineData(5, TaskStatusReadModelStatus.Available)]
    [InlineData(6, TaskStatusReadModelStatus.Unavailable)]
    public async Task ReaderAllowsOnlyFiveMinutesOfLocalClockLagAsync(int minutes, TaskStatusReadModelStatus expected)
    {
        OperationalStatusTestStore store = new();
        OperationalStatusClock clock = new() { UtcNow = OperationalStatusTestData.Now.AddMinutes(-minutes) };
        var sources = OperationalStatusTestData.Sources(OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now));
        (await new OperationalStatusProjectionWriter(store, store, store, clock).ApplyAsync("tenant-a", "folder-a", "initial",
            sources, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        (await Reader(store, clock, sources.Length).GetAsync(TaskRequest(), TestContext.Current.CancellationToken)).Status.ShouldBe(expected);
    }

    [Fact]
    public async Task DomainHostAheadOfPersistenceClockCanCloseAndRebuildATaskAsync()
    {
        var sources = OperationalStatusTestData.Sources(OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now))
            .Select(item => OperationalStatusEventReader.ReadStored(item.Source! with { Timestamp = item.Timestamp.AddMinutes(-5) })).ToArray();
        OperationalStatusTestStore store = new();
        OperationalStatusClock clock = new() { UtcNow = OperationalStatusTestData.Now.AddMinutes(-5) };
        var writer = new OperationalStatusProjectionWriter(store, store, store, clock);
        (await writer.ApplyAsync("tenant-a", "folder-a", "clock-skew", sources, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        (await writer.PrepareRebuildAsync("tenant-a", "folder-a", sources, sources.Length, TestContext.Current.CancellationToken)).ShouldNotBeEmpty();
        (await Reader(store, clock, sources.Length).GetAsync(TaskRequest(), TestContext.Current.CancellationToken)).Status.ShouldBe(TaskStatusReadModelStatus.Available);
    }

    [Fact]
    public async Task HostAheadTerminalExpiryRetainsItsOriginalPositionEvidenceUntilTheSameBoundaryAsync()
    {
        var sources = OperationalStatusTestData.Sources(OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now))
            .Select(item => OperationalStatusEventReader.ReadStored(item.Source! with { Timestamp = item.Timestamp.AddMinutes(-5) })).ToArray();
        OperationalStatusTestStore store = new();
        OperationalStatusClock clock = new() { UtcNow = OperationalStatusTestData.Now + OperationalStatusKeys.Retention - TimeSpan.FromMinutes(1) };
        var writer = new OperationalStatusProjectionWriter(store, store, store, clock);
        var operations = await writer.PrepareRebuildAsync("tenant-a", "folder-a", sources, sources.Length, TestContext.Current.CancellationToken);
        var task = operations.Single(item => item.Key == OperationalStatusKeys.Task("tenant-a", "folder-a", "task-a"));
        var receipt = operations.Single(item => item.Key == OperationalStatusKeys.Receipt("tenant-a", "folder-a", sources.Length));
        task.TimeToLive.ShouldBe(TimeSpan.FromMinutes(1));
        receipt.TimeToLive.ShouldBe(task.TimeToLive);
        (await writer.ApplyAsync("tenant-a", "folder-a", "almost-expired", sources, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        (await Reader(store, clock, sources.Length).GetAsync(TaskRequest(), TestContext.Current.CancellationToken)).Status.ShouldBe(TaskStatusReadModelStatus.Available);
        clock.UtcNow += TimeSpan.FromMinutes(1);
        (await Reader(store, clock, sources.Length).GetAsync(TaskRequest(), TestContext.Current.CancellationToken)).Status.ShouldBe(TaskStatusReadModelStatus.NotFound);
    }

    [Fact]
    public async Task DiagnosticStagedSdkManifestRetainsRelativeTtlAcrossSourceDeadlineAsync()
    {
        OperationalStatusTestStore store = new();
        OperationalStatusClock clock = new() { UtcNow = OperationalStatusTestData.Now.AddDays(399) };
        var sources = OperationalStatusTestData.Sources([OperationalStatusTestData.Created()]);
        var writer = new OperationalStatusProjectionWriter(store, store, store, clock);
        var operations = await writer.PrepareRebuildAsync("tenant-a", "folder-a", sources, 1, TestContext.Current.CancellationToken);
        ReadModelBatch staged = new(new("statestore", "tenant-a", "folders", "folder-a", "operational-status", "delayed-ttl-diagnostic"), operations)
        { RetainManifestForRecovery = true };
        (await store.Inner.StageAsync(staged, TestContext.Current.CancellationToken)).Status.ShouldBe(ReadModelBatchStagingStatus.Prepared);
        clock.UtcNow = OperationalStatusTestData.Now.AddDays(401);
        // Fresh domain planning correctly omits expired values. SDK recovery reuses the sealed
        // relative TTL, demonstrating the separate missing absolute-deadline capability.
        (await writer.PrepareRebuildAsync("tenant-a", "folder-a", sources, 1, TestContext.Current.CancellationToken))
            .ShouldHaveSingleItem().Key.ShouldBe(OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"));
        (await store.Inner.CommitAsync(staged, TestContext.Current.CancellationToken)).Status.ShouldBe(ReadModelBatchStagingStatus.Committed);
        store.Inner.Snapshot<FolderLifecycleStatusReadModelSnapshot>("statestore", OperationalStatusKeys.Lifecycle("tenant-a", "folder-a"))
            .ShouldNotBeNull();
        staged.Operations.Single(item => item.Key == OperationalStatusKeys.Lifecycle("tenant-a", "folder-a"))
            .TimeToLive.ShouldBe(TimeSpan.FromDays(1));
    }

    private static EventStoreOperationalStatusReadModel Reader(OperationalStatusTestStore store, OperationalStatusClock clock, long head)
        => new(new(store, store), clock, new OperationalStatusSequenceReader { Head = head });
    private static TaskStatusReadModelRequest TaskRequest()
        => new("tenant-a", "task-a", "reader", "read_task_status", "corr", "eventually_consistent")
        { AuthorizedFolderId = "folder-a", AuthorizationWatermark = "authority" };
    private static FolderLifecycleStatusReadModelRequest LifecycleRequest()
        => new("tenant-a", "folder-a", "reader", "read_metadata", null, "corr", "authority", "eventually_consistent");
    private static async Task EraseAsync(OperationalStatusTestStore store, string key)
    {
        var entry = await store.TryReadEtagAsync("statestore", key, TestContext.Current.CancellationToken).ConfigureAwait(false);
        entry.Present.ShouldBeTrue();
        (await store.TryEraseAsync("statestore", key, entry.Etag, TestContext.Current.CancellationToken).ConfigureAwait(false)).ShouldBeTrue();
    }
}
