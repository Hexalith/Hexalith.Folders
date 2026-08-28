using Hexalith.Folders.Aggregates.Folder;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class InMemoryFolderRepositoryConcurrencyTests
{
    private const string IdempotencyKey = "idempotency-concurrent-a";
    // How long a reader is watched while the writer is parked inside the gate before concluding it is
    // genuinely blocked. Long enough to survive scheduler jitter, short enough to keep the unit lane fast.
    private static readonly TimeSpan BlockingObservationWindow = TimeSpan.FromMilliseconds(250);

    // Upper bound on every wait in this class. A regression that reintroduces un-gated reads or deadlocks
    // the gate must fail the test here rather than hang the CI lane indefinitely.
    private static readonly TimeSpan CoordinationTimeout = TimeSpan.FromSeconds(5);
    private static readonly DateTimeOffset OccurredAt = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ConcurrentEquivalentAppendsShouldPublishOneEventAndMatchEveryReplay()
    {
        InMemoryFolderRepository repository = new();
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderCreated folderEvent = CreateFolderEvent("Folder A");
        const int racerCount = 8;
        using CountdownEvent ready = new(racerCount);
        using ManualResetEventSlim start = new(false);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        Task<FolderAppendOutcome>[] racers = Enumerable.Range(0, racerCount)
            .Select(_ => StartRacer(
                ready,
                start,
                () => repository.AppendIfFingerprintAbsent(
                    streamName,
                    IdempotencyKey,
                    folderEvent.IdempotencyFingerprint,
                    [folderEvent]),
                cancellationToken))
            .ToArray();

        ready.Wait(cancellationToken);
        start.Set();
        FolderAppendOutcome[] outcomes = await Task.WhenAll(racers)
            .WaitAsync(CoordinationTimeout, cancellationToken)
            .ConfigureAwait(true);

        outcomes.Count(outcome => outcome == FolderAppendOutcome.Appended).ShouldBe(1);
        outcomes.Count(outcome => outcome == FolderAppendOutcome.FingerprintMatched).ShouldBe(racerCount - 1);
        outcomes.ShouldNotContain(FolderAppendOutcome.FingerprintConflict);
        repository.EventsAppended.ShouldBe(1);

        FolderState loaded = repository.Load(streamName);
        loaded.IsCreated.ShouldBeTrue();
        loaded.DisplayName.ShouldBe(folderEvent.DisplayName);
        repository.TryGetIdempotencyFingerprint(streamName, IdempotencyKey, out string? storedFingerprint)
            .ShouldBe(FolderIdempotencyLookupResult.Found);
        storedFingerprint.ShouldBe(folderEvent.IdempotencyFingerprint);
    }

    [Fact]
    public async Task ConcurrentConflictingAppendsShouldKeepStateAndFingerprintFromOneWinner()
    {
        InMemoryFolderRepository repository = new();
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderCreated[] candidates =
        [
            CreateFolderEvent("Folder First"),
            CreateFolderEvent("Folder Second"),
        ];
        using CountdownEvent ready = new(candidates.Length);
        using ManualResetEventSlim start = new(false);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        Task<FolderAppendOutcome>[] racers = candidates
            .Select(candidate => StartRacer(
                ready,
                start,
                () => repository.AppendIfFingerprintAbsent(
                    streamName,
                    IdempotencyKey,
                    candidate.IdempotencyFingerprint,
                    [candidate]),
                cancellationToken))
            .ToArray();

        ready.Wait(cancellationToken);
        start.Set();
        FolderAppendOutcome[] outcomes = await Task.WhenAll(racers)
            .WaitAsync(CoordinationTimeout, cancellationToken)
            .ConfigureAwait(true);

        outcomes.Count(outcome => outcome == FolderAppendOutcome.Appended).ShouldBe(1);
        outcomes.Count(outcome => outcome == FolderAppendOutcome.FingerprintConflict).ShouldBe(1);
        outcomes.ShouldNotContain(FolderAppendOutcome.FingerprintMatched);
        repository.EventsAppended.ShouldBe(1);

        int winnerIndex = Array.FindIndex(outcomes, outcome => outcome == FolderAppendOutcome.Appended);
        FolderCreated winner = candidates[winnerIndex];
        FolderState loaded = repository.Load(streamName);
        loaded.DisplayName.ShouldBe(winner.DisplayName);
        repository.TryGetIdempotencyFingerprint(streamName, IdempotencyKey, out string? storedFingerprint)
            .ShouldBe(FolderIdempotencyLookupResult.Found);
        storedFingerprint.ShouldBe(winner.IdempotencyFingerprint);
    }

    [Fact]
    public async Task ConcurrentSeedAndReadersShouldPublishStateWithItsFingerprintAtomically()
    {
        InMemoryFolderRepository repository = new();
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderCreated folderEvent = CreateFolderEvent("Seeded Folder");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CoordinatedSeedEventList seedEvents = new(
            [folderEvent],
            CoordinationTimeout,
            cancellationToken);
        Task<bool> seed = StartOperation(
            () =>
            {
                repository.Seed(streamName, seedEvents);
                return true;
            },
            cancellationToken);
        TaskCompletionSource loadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource lookupStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<FolderState> load = null!;
        Task<(FolderIdempotencyLookupResult Lookup, string? Fingerprint)> lookup = null!;
        bool loadCompletedBeforeRelease = false;
        bool lookupCompletedBeforeRelease = false;

        try
        {
            await seedEvents.WriterHoldingGate
                .WaitAsync(CoordinationTimeout, cancellationToken)
                .ConfigureAwait(true);
            load = StartOperation(
                () =>
                {
                    loadStarted.TrySetResult();
                    return repository.Load(streamName);
                },
                cancellationToken);
            lookup = StartOperation(
                () =>
                {
                    lookupStarted.TrySetResult();
                    FolderIdempotencyLookupResult result = repository.TryGetIdempotencyFingerprint(
                        streamName,
                        IdempotencyKey,
                        out string? fingerprint);
                    return (result, fingerprint);
                },
                cancellationToken);
            await Task.WhenAll(loadStarted.Task, lookupStarted.Task)
                .WaitAsync(CoordinationTimeout, cancellationToken)
                .ConfigureAwait(true);
            await Task.Delay(BlockingObservationWindow, cancellationToken).ConfigureAwait(true);
            loadCompletedBeforeRelease = load.IsCompleted;
            lookupCompletedBeforeRelease = lookup.IsCompleted;
        }
        finally
        {
            seedEvents.ReleaseWriter();
        }

        (await seed.WaitAsync(CoordinationTimeout, cancellationToken).ConfigureAwait(true)).ShouldBeTrue();
        seedEvents.EnumerationCount.ShouldBe(CoordinatedSeedEventList.BlockOnEnumeration);
        FolderState loaded = await load
            .WaitAsync(CoordinationTimeout, cancellationToken)
            .ConfigureAwait(true);
        (FolderIdempotencyLookupResult lookupResult, string? fingerprint) =
            await lookup.WaitAsync(CoordinationTimeout, cancellationToken).ConfigureAwait(true);

        loadCompletedBeforeRelease.ShouldBeFalse();
        lookupCompletedBeforeRelease.ShouldBeFalse();
        loaded.IsCreated.ShouldBeTrue();
        loaded.DisplayName.ShouldBe(folderEvent.DisplayName);
        lookupResult.ShouldBe(FolderIdempotencyLookupResult.Found);
        fingerprint.ShouldBe(folderEvent.IdempotencyFingerprint);
    }

    [Fact]
    public async Task ConcurrentDuplicateSeedsShouldFailOneAttemptWithoutOverwritingTheWinner()
    {
        InMemoryFolderRepository repository = new();
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderCreated[] candidates =
        [
            CreateFolderEvent("Seed First"),
            CreateFolderEvent("Seed Second"),
        ];
        using CountdownEvent ready = new(candidates.Length);
        using ManualResetEventSlim start = new(false);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        Task<(bool Succeeded, InvalidOperationException? Error)>[] racers = candidates
            .Select(candidate => StartRacer(
                ready,
                start,
                () =>
                {
                    try
                    {
                        repository.Seed(streamName, [candidate]);
                        return (Succeeded: true, Error: (InvalidOperationException?)null);
                    }
                    catch (InvalidOperationException exception)
                    {
                        return (Succeeded: false, Error: (InvalidOperationException?)exception);
                    }
                },
                cancellationToken))
            .ToArray();

        ready.Wait(cancellationToken);
        start.Set();
        (bool Succeeded, InvalidOperationException? Error)[] outcomes =
            await Task.WhenAll(racers).WaitAsync(CoordinationTimeout, cancellationToken).ConfigureAwait(true);

        outcomes.Count(outcome => outcome.Succeeded).ShouldBe(1);
        outcomes.Count(outcome => outcome.Error is not null).ShouldBe(1);
        int winnerIndex = Array.FindIndex(outcomes, outcome => outcome.Succeeded);
        FolderCreated winner = candidates[winnerIndex];
        repository.Load(streamName).DisplayName.ShouldBe(winner.DisplayName);
        repository.TryGetIdempotencyFingerprint(streamName, IdempotencyKey, out string? storedFingerprint)
            .ShouldBe(FolderIdempotencyLookupResult.Found);
        storedFingerprint.ShouldBe(winner.IdempotencyFingerprint);
    }

    private static FolderCreated CreateFolderEvent(string displayName)
    {
        FolderResult result = FolderAggregate.Handle(
            FolderState.Empty,
            FolderCommandFactory.Create(displayName: displayName, idempotencyKey: IdempotencyKey),
            OccurredAt);

        result.Code.ShouldBe(FolderResultCode.Created);
        return result.Events.ShouldHaveSingleItem().ShouldBeOfType<FolderCreated>();
    }

    // LongRunning is load-bearing on both starters: every racer blocks synchronously (on the start gate and
    // then on the repository gate), so scheduling them on pooled threads could starve the pool and turn a
    // contention test into a serialized one.
    private static Task<T> StartRacer<T>(
        CountdownEvent ready,
        ManualResetEventSlim start,
        Func<T> action,
        CancellationToken cancellationToken)
        => Task.Factory.StartNew(
            () =>
            {
                ready.Signal();
                start.Wait(cancellationToken);
                return action();
            },
            cancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

    private static Task<T> StartOperation<T>(Func<T> action, CancellationToken cancellationToken)
        => Task.Factory.StartNew(
            action,
            cancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
}
