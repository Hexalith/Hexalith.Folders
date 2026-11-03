namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Exercises the real whole-operation timeout callback without waiting ninety seconds in a unit control.</summary>
internal sealed class FolderDurabilityTenantMaintenanceTimer : TimeProvider
{
    private Action? _expire;
    /// <summary>Gets the timeout actually requested by the maintenance coordinator.</summary>
    internal TimeSpan DueTime { get; private set; }
    /// <summary>Captures the timer callback while preserving its normal disposal and scheduling implementation.</summary>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        DueTime = dueTime;
        _expire = () => callback(state);
        return base.CreateTimer(callback, state, dueTime, period);
    }
    /// <summary>Signals exactly the captured deadline callback.</summary>
    internal void Expire() => (_expire ?? throw new InvalidOperationException("No maintenance deadline was scheduled."))();
}
