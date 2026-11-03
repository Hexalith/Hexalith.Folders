using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Requires fresh independent observations and owns their complete failure/cancellation lifecycle.</summary>
public sealed class ParallelPermissionObservationTests
{
    [Fact]
    public async Task BothCurrentSourcesStartTogetherAndNeitherAloneCanAuthorize()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TaskCompletionSource folderStarted = NewSignal(), organizationStarted = NewSignal();
        TaskCompletionSource<long> folderHead = NewHead(), organizationHead = NewHead();
        int folderReads = 0, organizationReads = 0;
        var reader = new PermissionObservationSequenceReader(async (aggregate, cancellation) =>
        {
            if (aggregate == "folder-a")
            {
                Interlocked.Increment(ref folderReads);
                folderStarted.TrySetResult();
                return await folderHead.Task.WaitAsync(cancellation).ConfigureAwait(false);
            }
            aggregate.ShouldBe("tenant-a");
            Interlocked.Increment(ref organizationReads);
            organizationStarted.TrySetResult();
            return await organizationHead.Task.WaitAsync(cancellation).ConfigureAwait(false);
        });
        var (model, store) = await CreateAsync(reader, token);
        var before = store.Snapshot();
        try
        {
            Task<EffectivePermissionsReadModelResult> pending = model.GetAsync(Request(), token);
            await Task.WhenAll(folderStarted.Task, organizationStarted.Task).WaitAsync(TimeSpan.FromSeconds(5), token);
            pending.IsCompleted.ShouldBeFalse();
            folderHead.SetResult(2);
            pending.IsCompleted.ShouldBeFalse();
            organizationHead.SetResult(1);
            EffectivePermissionsReadModelResult result = await pending.ConfigureAwait(true);
            result.Status.ShouldBe(EffectivePermissionsReadModelStatus.Available);
            result.Snapshot!.PermissionVersion.ShouldBe("[\"2\",\"1\"]");
            result.Snapshot.ResourceVersion.ShouldBe("2");
            result.Snapshot.RevocationFreshnessEstablished.ShouldBeTrue();
            (await model.GetAsync(Request(), token)).Status.ShouldBe(EffectivePermissionsReadModelStatus.Available);
            folderReads.ShouldBe(2);
            organizationReads.ShouldBe(2);
            store.Snapshot().ShouldBe(before);
        }
        finally
        {
            folderHead.TrySetResult(2);
            organizationHead.TrySetResult(1);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task FolderFailureOrMalformedHeadCancelsAndDrainsStartedOrganizationRead(bool malformed, bool throwingCallback)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TaskCompletionSource folderStarted = NewSignal(), organizationStarted = NewSignal(), organizationStopped = NewSignal();
        TaskCompletionSource organizationCanceled = NewSignal(), cleanupRelease = NewSignal();
        TaskCompletionSource<long> folderHead = NewHead();
        var reader = new PermissionObservationSequenceReader(async (aggregate, cancellation) =>
        {
            if (aggregate == "folder-a")
            {
                folderStarted.TrySetResult();
                return await folderHead.Task.WaitAsync(cancellation).ConfigureAwait(false);
            }
            organizationStarted.TrySetResult();
            using CancellationTokenRegistration callback = throwingCallback
                ? cancellation.Register(() => throw new InvalidOperationException("Cancellation callback failed.")) : default;
            try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellation).ConfigureAwait(false); return 1; }
            finally
            {
                organizationCanceled.TrySetResult();
                await cleanupRelease.Task.ConfigureAwait(false);
                organizationStopped.TrySetResult();
            }
        });
        var (model, store) = await CreateAsync(reader, token);
        var before = store.Snapshot();
        try
        {
            Task<EffectivePermissionsReadModelResult> pending = model.GetAsync(Request(), token);
            await Task.WhenAll(folderStarted.Task, organizationStarted.Task).WaitAsync(TimeSpan.FromSeconds(5), token);
            if (malformed) folderHead.SetResult(0);
            else folderHead.SetException(new IOException("Current Folder source unavailable."));
            await organizationCanceled.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
            pending.IsCompleted.ShouldBeFalse();
            cleanupRelease.SetResult();
            EffectivePermissionsReadModelResult result = await pending.WaitAsync(TimeSpan.FromSeconds(5), token);
            result.Status.ShouldBe(malformed ? EffectivePermissionsReadModelStatus.Malformed : EffectivePermissionsReadModelStatus.Unavailable);
            result.Snapshot.ShouldBeNull();
            if (!malformed) result.Freshness.ReasonCode.ShouldBe("folder_stream_unavailable");
            organizationStopped.Task.IsCompletedSuccessfully.ShouldBeTrue();
            store.Snapshot().ShouldBe(before);
        }
        finally { folderHead.TrySetResult(2); cleanupRelease.TrySetResult(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrganizationFailureCannotOvertakeFolderOutcome(bool malformedFolder)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TaskCompletionSource organizationObserved = NewSignal();
        TaskCompletionSource<long> folderHead = NewHead();
        var reader = new PermissionObservationSequenceReader(async (aggregate, cancellation) =>
        {
            if (aggregate == "folder-a") return await folderHead.Task.WaitAsync(cancellation).ConfigureAwait(false);
            organizationObserved.TrySetResult();
            throw new IOException("Current Organization source unavailable.");
        });
        var (model, _) = await CreateAsync(reader, token);
        try
        {
            Task<EffectivePermissionsReadModelResult> pending = model.GetAsync(Request(), token);
            await organizationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
            pending.IsCompleted.ShouldBeFalse();
            folderHead.SetResult(malformedFolder ? 0 : 2);
            EffectivePermissionsReadModelResult result = await pending.ConfigureAwait(true);
            result.Status.ShouldBe(malformedFolder ? EffectivePermissionsReadModelStatus.Malformed : EffectivePermissionsReadModelStatus.Unavailable);
            result.Snapshot.ShouldBeNull();
            if (!malformedFolder) result.Freshness.ReasonCode.ShouldBe("organization_projection_unavailable");
        }
        finally { folderHead.TrySetResult(2); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallerOrCatchupCancellationDrainsBothSourcesAndAcceptsNoLateAuthority(bool catchupDeadline)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(token);
        TaskCompletionSource folderStarted = NewSignal(), organizationStarted = NewSignal();
        TaskCompletionSource folderStopped = NewSignal(), organizationStopped = NewSignal();
        var reader = new PermissionObservationSequenceReader(async (aggregate, cancellation) =>
        {
            bool folder = aggregate == "folder-a";
            (folder ? folderStarted : organizationStarted).TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellation).ConfigureAwait(false); return folder ? 2 : 1; }
            finally { (folder ? folderStopped : organizationStopped).TrySetResult(); }
        });
        var (model, store) = await CreateAsync(reader, token);
        var before = store.Snapshot();
        Task<EffectivePermissionsReadModelResult> pending = model.GetAsync(Request() with { WaitForCatchUp = catchupDeadline }, caller.Token);
        await Task.WhenAll(folderStarted.Task, organizationStarted.Task).WaitAsync(TimeSpan.FromSeconds(5), token);
        if (catchupDeadline)
        {
            EffectivePermissionsReadModelResult result = await pending.WaitAsync(TimeSpan.FromSeconds(2), token);
            result.Status.ShouldBe(EffectivePermissionsReadModelStatus.Unavailable);
            result.Snapshot.ShouldBeNull();
            result.Freshness.ReasonCode.ShouldBe("permission_projection_catch_up_timeout");
        }
        else
        {
            await caller.CancelAsync();
            (await Should.ThrowAsync<OperationCanceledException>(() => pending)).CancellationToken.ShouldBe(caller.Token);
        }
        await Task.WhenAll(folderStopped.Task, organizationStopped.Task).WaitAsync(TimeSpan.FromSeconds(2), token);
        store.Snapshot().ShouldBe(before);
    }

    private static EffectivePermissionsReadModelRequest Request() => new("tenant-a", "folder-a",
        [EffectivePermissionPrincipal.User("actor")], null, null, "read_your_writes");

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static TaskCompletionSource<long> NewHead() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task<(EventStoreFolderEffectivePermissionsReadModel Model, FolderOwnerReadStore Store)> CreateAsync(
        IOrganizationStreamSequenceReader reader, CancellationToken token)
    {
        var clock = new FixedUtcClock(new DateTimeOffset(2026, 11, 3, 15, 0, 0, TimeSpan.Zero));
        var store = new FolderOwnerReadStore();
        var organization = new EventStoreOrganizationEffectivePermissionsReadModel(store, clock, reader);
        var model = new EventStoreFolderEffectivePermissionsReadModel(store, clock, reader, organization);
        await organization.ApplyAsync("tenant-a", "tenant-a", "baseline", 1,
            new OrganizationAclBaselineInitialized("tenant-a", "tenant-a", "corr", "task", "key", "fingerprint"), token).ConfigureAwait(false);
        await model.ApplyAsync("tenant-a", "folder-a", "created", 1, "created", new FolderCreated("tenant-a", "tenant-a", "folder-a",
            "Folder", null, null, [], FolderLifecycleState.Active, FolderRepositoryBindingState.Unbound,
            "actor", "corr", "task", "create", "create", clock.UtcNow), token).ConfigureAwait(false);
        await model.ApplyAsync("tenant-a", "folder-a", "granted", 2, "granted", new FolderAccessGranted("tenant-a", "tenant-a", "folder-a",
            FolderAccessPrincipalKind.User, "actor", "read_metadata", "actor", "corr", "task", "grant", "grant", 2, clock.UtcNow), token).ConfigureAwait(false);
        store.ForbidWrites = true;
        return (model, store);
    }
}
