using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.10 — a hand-rolled controllable <see cref="TimeProvider"/> for deterministic SkeletonState timer
/// tests. Modelled on the repo's existing <c>FixedTimeProvider : TimeProvider</c> pattern
/// (tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs); NO package is added (the
/// <c>Microsoft.Extensions.Time.Testing.FakeTimeProvider</c> add is forbidden — Directory.Packages.props is a
/// forbidden touch). <see cref="CreateTimer"/> captures each callback + due time; <see cref="Advance"/> moves a
/// virtual clock forward and fires every active one-shot timer whose due time has elapsed.
/// </summary>
internal sealed class ControllableTimeProvider : TimeProvider
{
    private readonly List<CapturedTimer> _timers = [];
    private DateTimeOffset _now = DateTimeOffset.UnixEpoch;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _now;

    /// <summary>Count of timers still registered (i.e. created and not yet disposed) — used to assert disposal.</summary>
    public int RegisteredTimerCount => _timers.Count;

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);
        CapturedTimer timer = new(this, callback, state, _now + dueTime);
        _timers.Add(timer);
        return timer;
    }

    /// <summary>Advance the virtual clock and fire every active timer whose (absolute) due time has elapsed.</summary>
    public void Advance(TimeSpan delta)
    {
        _now += delta;
        foreach (CapturedTimer timer in _timers
            .Where(t => t.IsActive && t.DueAt <= _now)
            .OrderBy(t => t.DueAt)
            .ToList())
        {
            timer.Fire();
        }
    }

    private void Remove(CapturedTimer timer) => _timers.Remove(timer);

    private sealed class CapturedTimer(
        ControllableTimeProvider provider,
        TimerCallback callback,
        object? state,
        DateTimeOffset dueAt) : ITimer
    {
        public DateTimeOffset DueAt { get; private set; } = dueAt;

        public bool IsActive { get; private set; } = true;

        public void Fire()
        {
            if (!IsActive)
            {
                return;
            }

            // SkeletonState schedules one-shot timers (period = Timeout.InfiniteTimeSpan): deactivate on fire so
            // a callback never runs twice.
            IsActive = false;
            callback(state);
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            DueAt = provider.GetUtcNow() + dueTime;
            IsActive = dueTime != Timeout.InfiniteTimeSpan;
            return true;
        }

        public void Dispose() => provider.Remove(this);

        public ValueTask DisposeAsync()
        {
            provider.Remove(this);
            return ValueTask.CompletedTask;
        }
    }
}
