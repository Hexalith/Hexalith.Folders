using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Text.Json;
using System.Threading.Channels;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Task36FoldObserver;

/// <summary>Observes framework lifecycle IDs without reading request payloads or replacing host services.</summary>
internal sealed class LifecycleDiagnosticObserver : EventListener, IHostedService
{
    private readonly Channel<string> _records = Channel.CreateBounded<string>(new BoundedChannelOptions(64)
    { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    private readonly long _started = Stopwatch.GetTimestamp();
    private readonly IServiceCollection _services;
    private readonly LifecycleRequestCorrelation _requests = new();
    private readonly LifecycleRequestCorrelation _requestActivities = new();
    private LifecycleActivityObservation? _activityObservation;
    private readonly string _path;
    private IServiceProvider? _provider;
    private ExistingHostedServiceObservation? _existingServices;
    private IHostApplicationLifetime? _lifetime;
    private HostOptions? _options;
    private CancellationTokenRegistration _startedRegistration;
    private CancellationTokenRegistration _stoppingRegistration;
    private CancellationTokenRegistration _stoppedRegistration;
    private Timer? _heartbeat;
    private int _active;
    private int _stopping;
    private int _hostStopped;
    private int _applicationStopped;
    private long _inFlight;
    private long _activeRequestActivities;
    private long _dropped;
    private int _sinkFailed;

    private LifecycleDiagnosticObserver(string path, IServiceCollection services)
    {
        _path = path;
        _services = services;
        Completion = Task.Run(WriteAsync);
    }

    /// <summary>Gets writer completion for test qualification only; host stop and disposal never await it.</summary>
    internal Task Completion { get; }

    /// <summary>Resolves only canonical host options/lifetime; optional provider failures remain advisory.</summary>
    internal static LifecycleDiagnosticObserver Create(IServiceProvider provider, IServiceCollection services, string path)
    {
        LifecycleDiagnosticObserver observer = new(path, services);
        observer._provider = provider;
        try
        {
            observer._lifetime = provider.GetService<IHostApplicationLifetime>();
            observer._options = provider.GetService<IOptions<HostOptions>>()?.Value;
        }
        catch { observer.Record("setup_unavailable"); }
        return observer;
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (Interlocked.Exchange(ref _active, 1) != 0) return Task.CompletedTask;
            _activityObservation = new LifecycleActivityObservation(ObserveRequestActivity);
            RecordConfiguration();
            Record("observer_registered");
            if (_lifetime is not null)
            {
                _startedRegistration = _lifetime.ApplicationStarted.Register(() =>
                {
                    try
                    {
                        object? capture = _existingServices?.Capture(_lifetime.ApplicationStarted.IsCancellationRequested);
                        Enqueue(JsonSerializer.Serialize(new { phase = "existing_hosted_services", processId = Environment.ProcessId,
                            utc = DateTimeOffset.UtcNow, capture }));
                    }
                    catch { Record("capture_unavailable"); }
                });
                _stoppingRegistration = _lifetime.ApplicationStopping.Register(OnStopping);
                _stoppedRegistration = _lifetime.ApplicationStopped.Register(() =>
                {
                    Volatile.Write(ref _applicationStopped, 1);
                    Record("application_stopped");
                    _heartbeat?.Dispose();
                });
            }
        }
        catch { Record("setup_unavailable"); }
        return Task.CompletedTask;
    }

    /// <summary>Keeps observing later hosted stops and disposal without awaiting or changing host cancellation.</summary>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        try
        {
            if (eventSource.Name == "Microsoft.AspNetCore.Hosting")
                EnableEvents(eventSource, EventLevel.Informational);
        }
        catch { /* EventSource discovery is advisory. */ }
    }

    /// <inheritdoc/>
    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        // Deliberately never read Payload, PayloadNames, Message or any request object.
        ObserveEvent(eventData.EventId, eventData.ActivityId, eventData.RelatedActivityId);
    }

    /// <summary>Consumes only the four admitted primitive event IDs.</summary>
    internal void ObserveEvent(int eventId, Guid activityId = default, Guid relatedActivityId = default)
    {
        try
        {
            if (Volatile.Read(ref _active) == 0) return;
            switch (eventId)
            {
                case 1: Record("host_start"); break;
                case 2:
                    Volatile.Write(ref _hostStopped, 1);
                    Record("host_stop");
                    break;
                case 3:
                    RecordRequest("request_start", _requests.Observe(true, Interlocked.Increment(ref _inFlight), activityId, relatedActivityId));
                    break;
                case 4:
                    RecordRequest("request_end", _requests.Observe(false, Interlocked.Decrement(ref _inFlight), activityId, relatedActivityId));
                    break;
            }
        }
        catch { /* No event or sink failure may affect framework execution. */ }
    }

    private void OnStopping()
    {
        try
        {
            if (Interlocked.Exchange(ref _stopping, 1) != 0) return;
            Record("application_stopping");
            _heartbeat = new Timer(_ => Record("stopping_snapshot"), null, 1000, 1000);
        }
        catch { /* This callback must never fail or wait for the sink. */ }
    }

    private void RecordConfiguration()
    {
        // Registration observation never resolves services. The later ApplicationStarted
        // callback has separate gates before it may reuse the cached singleton enumerable.
        ServiceDescriptor[] registrations = _services.Where(descriptor => !descriptor.IsKeyedService
            && descriptor.ServiceType == typeof(IHostedService)).ToArray();
        if (_provider is not null) _existingServices = new(_provider, _services, registrations);
        string[] order = registrations.Take(128).Select(descriptor => Label(descriptor.ImplementationType
            ?? descriptor.ImplementationInstance?.GetType(), descriptor.ImplementationFactory is not null)).ToArray();
        Enqueue(JsonSerializer.Serialize(new
        {
            phase = "host_configuration", processId = Environment.ProcessId, utc = DateTimeOffset.UtcNow,
            stopConcurrently = _options?.ServicesStopConcurrently,
            shutdownTimeoutMilliseconds = _options?.ShutdownTimeout.TotalMilliseconds,
            serviceRegistrationOrder = order, serviceRegistrationCount = registrations.Length,
            serviceOrderTruncated = registrations.Length > 128,
            serverRegisteredLast = registrations.Length > 0 && Label(registrations[^1].ImplementationType, false) == "web_server",
            factoryRegistrationCount = registrations.Count(descriptor => descriptor.ImplementationFactory is not null),
            nonSingletonRegistrationCount = registrations.Count(descriptor => descriptor.Lifetime != ServiceLifetime.Singleton),
            knownLifecycleRegistrationCount = registrations.Count(descriptor => descriptor.ImplementationType is { } type
                && typeof(IHostedLifecycleService).IsAssignableFrom(type)),
        }));
    }

    private static string Label(Type? type, bool factory) => type?.FullName switch
    {
        "Microsoft.AspNetCore.Hosting.GenericWebHostService" => "web_server",
        "Hexalith.EventStore.ServiceDefaults.BoundedTelemetryShutdownService" => "telemetry_shutdown",
        "Hexalith.EventStore.Indexes.AdminOperationalIndexHostedService" => "admin_indexes",
        "Hexalith.EventStore.Configuration.DaprRateLimitConfigSync" => "rate_limit_sync",
        "Hexalith.EventStore.Server.Projections.ProjectionDiscoveryHostedService" => "projection_discovery",
        "Hexalith.EventStore.Server.Configuration.FoldedSnapshotQualificationHostedService" => "snapshot_qualification",
        "Task36FoldObserver.LifecycleDiagnosticObserver" => "fixture_lifecycle",
        _ => factory ? "opaque_factory" : "other",
    };

    private void Record(string phase)
    {
        try
        {
            Enqueue(JsonSerializer.Serialize(new
            {
                phase, processId = Environment.ProcessId, utc = DateTimeOffset.UtcNow,
                elapsedMilliseconds = Stopwatch.GetElapsedTime(_started).TotalMilliseconds,
                inFlightRequests = Interlocked.Read(ref _inFlight),
                hostStopSeen = Volatile.Read(ref _hostStopped) != 0,
                applicationStoppedSeen = Volatile.Read(ref _applicationStopped) != 0,
                droppedRecords = Interlocked.Read(ref _dropped), sinkFailed = Volatile.Read(ref _sinkFailed) != 0,
                hostedServiceStates = _existingServices?.States(),
                requestCorrelation = _requests.Snapshot(),
                requestActivityCorrelation = _requestActivities.Snapshot(),
                requestActivityObservation = _activityObservation?.Snapshot(),
            }));
        }
        catch { /* Serialization and queue failures are advisory. */ }
    }

    private void RecordRequest(string phase, object correlation) => Enqueue(JsonSerializer.Serialize(new
    {
        phase, processId = Environment.ProcessId, utc = DateTimeOffset.UtcNow,
        inFlightRequests = Interlocked.Read(ref _inFlight), correlation,
    }));

    private void ObserveRequestActivity(bool started)
    {
        if (Volatile.Read(ref _active) == 0) return;
        long active = started ? Interlocked.Increment(ref _activeRequestActivities) : Interlocked.Decrement(ref _activeRequestActivities);
        RecordRequest(started ? "request_activity_start" : "request_activity_end",
            _requestActivities.Observe(started, active, Guid.Empty, Guid.Empty));
    }

    private void Enqueue(string value)
    {
        if (!_records.Writer.TryWrite(value)) Interlocked.Increment(ref _dropped);
    }

    private async Task WriteAsync()
    {
        try
        {
            FileStreamOptions options = new() { Mode = FileMode.Append, Access = FileAccess.Write,
                Share = FileShare.ReadWrite, Options = FileOptions.Asynchronous };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            FileStream stream = new(_path, options);
            await using var streamLease = stream.ConfigureAwait(false);
            StreamWriter writer = new(stream) { AutoFlush = true };
            await using var writerLease = writer.ConfigureAwait(false);
            await foreach (string value in _records.Reader.ReadAllAsync().ConfigureAwait(false))
                await writer.WriteLineAsync(value).ConfigureAwait(false);
        }
        catch { Volatile.Write(ref _sinkFailed, 1); _records.Writer.TryComplete(); }
    }

    /// <summary>Releases subscriptions without waiting for file I/O or changing host stop results.</summary>
    public override void Dispose()
    {
        try
        {
            Volatile.Write(ref _active, 0);
            _activityObservation?.Dispose();
            _startedRegistration.Unregister();
            _stoppingRegistration.Unregister();
            _stoppedRegistration.Unregister();
            _heartbeat?.Dispose();
            base.Dispose();
        }
        catch { /* Cleanup is advisory and non-draining. */ }
        finally { _records.Writer.TryComplete(); }
    }
}
