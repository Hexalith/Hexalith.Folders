using Hexalith.Folders.Projections.TenantAccess;
namespace Hexalith.Folders.NativeContent.Tests;

internal sealed class NativeContentClock : TimeProvider, IUtcClock
{
    internal object Sync { get; } = new();
    private readonly List<NativeContentTimer> _timers = [];
    private DateTimeOffset _now = DateTimeOffset.UtcNow;
    private long _elapsedTicks;

    internal Action<TimeSpan>? TimerScheduled { get; init; }
    internal DateTimeOffset Now { get { lock (Sync) return _now; } set { lock (Sync) _now = value; } }
    public DateTimeOffset UtcNow => Now;
    public override DateTimeOffset GetUtcNow() => Now;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() { lock (Sync) return _elapsedTicks; }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var timer = new NativeContentTimer(this, callback, state);
        lock (Sync)
        {
            _timers.Add(timer);
            timer.Change(dueTime, period);
        }

        TimerScheduled?.Invoke(dueTime);
        return timer;
    }
    internal void Advance(TimeSpan elapsed)
    {
        NativeContentTimer[] due;
        lock (Sync)
        {
            _now += elapsed;
            _elapsedTicks += elapsed.Ticks;
            due = _timers.Where(timer => timer.Due <= _now).ToArray();
        }
        foreach (var timer in due) timer.Fire();
    }
}
