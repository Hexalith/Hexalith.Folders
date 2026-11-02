using System.Diagnostics;
using Microsoft.Extensions.Hosting;

namespace Task36FoldObserver;

/// <summary>Subscribes to actual handler entry diagnostics without replacing the production handler.</summary>
public sealed class FoldDiagnosticObserver : IHostedService, IObserver<DiagnosticListener>, IDisposable
{
    /// <summary>The diagnostic source owned by the actual operational handler.</summary>
    public const string ListenerName = "Hexalith.Folders.OperationalStatus";

    /// <summary>The exact callback-entry event admitted by this observer.</summary>
    public const string EventName = "FoldCatchUp";

    private readonly object _gate = new();
    private readonly string _path;
    private readonly List<IDisposable> _subscriptions = [];
    private IDisposable? _allListeners;
    private bool _active;
    private bool _disposed;

    /// <summary>Creates an observer bound to the owning fixture's existing metadata sink.</summary>
    /// <param name="path">The exact fixture-owned JSON-lines path.</param>
    public FoldDiagnosticObserver(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_active)
            {
                _active = true;
                _allListeners = DiagnosticListener.AllListeners.Subscribe(this);
            }
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        Stop();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void OnNext(DiagnosticListener value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Name != ListenerName) return;
        lock (_gate)
        {
            if (!_active) return;
            _subscriptions.Add(value.Subscribe(new FoldDiagnosticEventObserver(_path), name => name == EventName));
            FoldObservationSink.Record(_path, "registered", null, null);
        }
    }

    /// <inheritdoc/>
    public void OnError(Exception error) { }

    /// <inheritdoc/>
    public void OnCompleted() { }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_gate) _disposed = true;
        Stop();
    }

    private void Stop()
    {
        IDisposable[] subscriptions;
        IDisposable? listeners;
        lock (_gate)
        {
            _active = false;
            listeners = _allListeners;
            _allListeners = null;
            subscriptions = [.. _subscriptions];
            _subscriptions.Clear();
        }
        listeners?.Dispose();
        foreach (IDisposable subscription in subscriptions) subscription.Dispose();
    }
}
