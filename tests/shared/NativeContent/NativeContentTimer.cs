namespace Hexalith.Folders.NativeContent.Tests;
internal sealed class NativeContentTimer(NativeContentClock clock, TimerCallback callback, object? state) : ITimer
{
    internal DateTimeOffset Due { get; private set; } = DateTimeOffset.MaxValue;
    private TimeSpan _period;
    private bool _disposed;
    public bool Change(TimeSpan dueTime, TimeSpan period)
    {
        lock (clock.Sync)
        {
            if (_disposed) return false;
            _period = period; Due = dueTime == Timeout.InfiniteTimeSpan ? DateTimeOffset.MaxValue : clock.Now + dueTime; return true;
        }
    }
    internal void Fire()
    {
        lock (clock.Sync)
        {
            if (_disposed || Due > clock.Now) return;
            Due = _period > TimeSpan.Zero ? clock.Now + _period : DateTimeOffset.MaxValue;
        }
        callback(state);
    }
    public void Dispose() { lock (clock.Sync) { _disposed = true; Due = DateTimeOffset.MaxValue; } }
    public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
}
