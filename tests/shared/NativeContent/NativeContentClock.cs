using Hexalith.Folders.Projections.TenantAccess;
namespace Hexalith.Folders.NativeContent.Tests;

internal sealed class NativeContentClock : TimeProvider, IUtcClock
{
    internal object Sync { get; } = new();
    private readonly List<NativeContentTimer> _timers = [];
    private DateTimeOffset _now = DateTimeOffset.UtcNow;
    internal DateTimeOffset Now { get { lock (Sync) return _now; } set { lock (Sync) _now = value; } }
    public DateTimeOffset UtcNow => Now;
    public override DateTimeOffset GetUtcNow() => Now;
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);
        lock (Sync)
        {
            var timer = new NativeContentTimer(this, callback, state); _timers.Add(timer); timer.Change(dueTime, period); return timer;
        }
    }
    internal void Advance(TimeSpan elapsed)
    {
        NativeContentTimer[] due;
        lock (Sync) { _now += elapsed; due = _timers.Where(timer => timer.Due <= _now).ToArray(); }
        foreach (var timer in due) timer.Fire();
    }
}
