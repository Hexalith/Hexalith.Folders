using System.Diagnostics;

namespace Task36FoldObserver;

/// <summary>Observes fixed late hosting notifications without enabling activity creation or reading event values.</summary>
internal sealed class LifecycleActivityObservation : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
{
    private const string Start = "Microsoft.AspNetCore.Hosting.HttpRequestIn.Start";
    private const string Stop = "Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop";
    private readonly object _gate = new();
    private readonly List<IDisposable> _subscriptions = [];
    private readonly Action<bool> _observe;
    private readonly IDisposable _listeners;
    private int _disposed;
    private long _subscriptionFailures;

    /// <summary>Subscribes only to an existing hosting listener; every callback remains advisory.</summary>
    internal LifecycleActivityObservation(Action<bool> observe)
    {
        _observe = observe;
        _listeners = DiagnosticListener.AllListeners.Subscribe(this);
    }

    /// <summary>Refuses the unsuffixed creation predicate and every notification except the fixed late pair.</summary>
    internal static bool IsEnabled(string name) => name is Start or Stop;

    /// <inheritdoc/>
    public void OnNext(DiagnosticListener listener)
    {
        if (Volatile.Read(ref _disposed) != 0 || listener is null || listener.Name != "Microsoft.AspNetCore") return;
        if (!Monitor.TryEnter(_gate)) { Interlocked.Increment(ref _subscriptionFailures); return; }
        try
        {
            if (_disposed == 0 && _subscriptions.Count < 8)
                _subscriptions.Add(listener.Subscribe(this, IsEnabled));
            else if (_disposed == 0) Interlocked.Increment(ref _subscriptionFailures);
        }
        catch { Interlocked.Increment(ref _subscriptionFailures); }
        finally
        {
            if (Volatile.Read(ref _disposed) != 0) DisposeSubscriptions();
            Monitor.Exit(_gate);
        }
    }

    /// <inheritdoc/>
    public void OnNext(KeyValuePair<string, object?> notification)
    {
        // Never read Value: hosting notifications carry request/context objects.
        if (Volatile.Read(ref _disposed) != 0 || !IsEnabled(notification.Key)) return;
        try { _observe(notification.Key == Start); }
        catch { /* Correlation must not affect the existing request or activity. */ }
    }

    /// <inheritdoc/>
    public void OnCompleted() { }

    /// <inheritdoc/>
    public void OnError(Exception error) { }

    /// <summary>Reports incomplete subscription explicitly without resolving application objects.</summary>
    internal object Snapshot()
    {
        if (!Monitor.TryEnter(_gate)) return new { status = "unavailable", reason = "observation_busy" };
        try
        {
            long failures = Interlocked.Read(ref _subscriptionFailures);
            return new
            {
                status = failures == 0 && _subscriptions.Count > 0 ? "available" : "unavailable",
                reason = failures > 0 ? "subscription_incomplete" : _subscriptions.Count == 0 ? "listener_not_observed" : "fixed_late_notifications",
                subscribedListeners = _subscriptions.Count, subscriptionFailures = failures, listenerCapacity = 8,
            };
        }
        finally { Monitor.Exit(_gate); }
    }

    /// <summary>Unsubscribes without waiting on hosting, requests or the private file sink.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try { _listeners.Dispose(); } catch { /* Advisory cleanup. */ }
        if (!Monitor.TryEnter(_gate)) return;
        try
        {
            DisposeSubscriptions();
        }
        finally { Monitor.Exit(_gate); }
    }

    private void DisposeSubscriptions()
    {
        foreach (IDisposable subscription in _subscriptions)
            try { subscription.Dispose(); } catch { /* Advisory cleanup. */ }
        _subscriptions.Clear();
    }
}
