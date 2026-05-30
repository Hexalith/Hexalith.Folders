using System.Diagnostics;
using System.Diagnostics.Metrics;

using Hexalith.Folders.Providers.Abstractions;

using Microsoft.Extensions.Logging;

namespace Hexalith.Folders.Observability;

public sealed class FolderTelemetryEmitter(
    IEnumerable<IFolderAuditObserver> observers,
    ILogger<FolderTelemetryEmitter> logger) : IFolderTelemetryEmitter
{
    private static readonly ActivitySource ActivitySource = new(FolderTelemetryNames.ActivitySourceName);
    private static readonly Meter Meter = new(FolderTelemetryNames.MeterName);
    private static readonly Counter<long> ObservationCounter =
        Meter.CreateCounter<long>(FolderTelemetryNames.ObservationsCounter);
    private static readonly Histogram<double> DurationHistogram =
        Meter.CreateHistogram<double>(FolderTelemetryNames.DurationHistogram, "ms");

    // Story 7.12 alert-worthy operational signals share the single existing Meter so the
    // ServiceDefaults AddMeter registration captures them. Production exporter/alert intent is
    // declared in deploy/observability/production. These instruments are observe-only.
    private static readonly Histogram<double> ProjectionLagHistogram =
        Meter.CreateHistogram<double>(FolderTelemetryNames.ProjectionLagHistogram, "ms");
    private static readonly Histogram<long> DeadLetterDepthHistogram =
        Meter.CreateHistogram<long>(FolderTelemetryNames.DeadLetterDepthHistogram);
    private static readonly Counter<long> ProviderFailureCounter =
        Meter.CreateCounter<long>(FolderTelemetryNames.ProviderFailureCounter);
    private static readonly Counter<long> StaleLockCounter =
        Meter.CreateCounter<long>(FolderTelemetryNames.StaleLockCounter);
    private static readonly Counter<long> CleanupFailureCounter =
        Meter.CreateCounter<long>(FolderTelemetryNames.CleanupFailureCounter);

    private readonly IReadOnlyList<IFolderAuditObserver> _observers = [.. observers];
    private readonly ILogger<FolderTelemetryEmitter> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async ValueTask EmitAsync(FolderAuditObservation observation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observation);

        using Activity? activity = ActivitySource.StartActivity(FolderTelemetryNames.OperationSpan);
        activity?.SetTag(FolderTelemetryNames.OperationKindTag, observation.OperationKind.ToString());
        activity?.SetTag(FolderTelemetryNames.ResultTag, observation.Result.ToString());
        activity?.SetTag(FolderTelemetryNames.CategoryTag, observation.SanitizedCategory ?? "none");
        activity?.SetTag(FolderTelemetryNames.RedactionStateTag, observation.RedactionState.ToString());
        activity?.SetTag(FolderTelemetryNames.CorrelationPresentTag, observation.CorrelationId is not null);
        activity?.SetTag(FolderTelemetryNames.TaskPresentTag, observation.TaskId is not null);
        activity?.SetTag(FolderTelemetryNames.TenantPresentTag, !string.IsNullOrWhiteSpace(observation.TenantId));
        activity?.SetTag(FolderTelemetryNames.ActorReferencePresentTag, observation.ActorReference is not null);
        activity?.SetTag(FolderTelemetryNames.RetryTag, observation.IsRetry);
        activity?.SetTag(FolderTelemetryNames.ReplayTag, observation.IsIdempotentReplay);
        activity?.SetTag(FolderTelemetryNames.DuplicateTag, observation.IsDuplicate);
        activity?.SetStatus(observation.IsFailure ? ActivityStatusCode.Error : ActivityStatusCode.Ok);

        TagList metricTags = new()
        {
            { FolderTelemetryNames.OperationKindTag, observation.OperationKind.ToString() },
            { FolderTelemetryNames.ResultTag, observation.Result.ToString() },
            { FolderTelemetryNames.CategoryTag, observation.SanitizedCategory ?? "none" },
            { FolderTelemetryNames.RedactionStateTag, observation.RedactionState.ToString() },
        };
        ObservationCounter.Add(1, metricTags);
        DurationHistogram.Record(observation.Duration.TotalMilliseconds, metricTags);

        _logger.LogInformation(
            "Folder operation observed {OperationKind} {Result} {Category} {CorrelationId} {TaskId} {RedactionState}",
            observation.OperationKind,
            observation.Result,
            observation.SanitizedCategory ?? "none",
            observation.CorrelationId ?? "correlation_absent",
            observation.TaskId ?? "task_absent",
            observation.RedactionState);

        foreach (IFolderAuditObserver observer in _observers)
        {
            try
            {
                await observer.ObserveAsync(observation, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _logger.LogWarning(
                    "Folder audit observer failed {ObserverResult} {OperationKind} {Result}",
                    "observer_unavailable",
                    observation.OperationKind,
                    observation.Result);
            }
        }
    }

    /// <summary>
    /// Records the projection-lag signal (severity Warning). The threshold-exceeded flag traces to the
    /// pinned C2 500 ms target. Lag is clock-derived and never persisted to replayable projection state.
    /// </summary>
    public void RecordProjectionLag(long ageMilliseconds, string? stateSource)
    {
        double lag = ageMilliseconds < 0 ? 0 : ageMilliseconds;
        TagList tags = new()
        {
            { FolderTelemetryNames.SignalTag, FolderTelemetryNames.ProjectionLagSignal },
            { FolderTelemetryNames.SeverityTag, FolderTelemetryNames.SeverityWarning },
            { FolderTelemetryNames.StateSourceTag, BoundedCategory(stateSource) },
            { FolderTelemetryNames.ThresholdExceededTag, lag > FolderTelemetryNames.C2ProjectionLagBudgetMilliseconds },
        };
        ProjectionLagHistogram.Record(lag, tags);
    }

    /// <summary>Records the dead-letter-depth signal (severity Warning) for a bounded domain.</summary>
    public void RecordDeadLetterDepth(string? domain, long depth)
    {
        long bounded = depth < 0 ? 0 : depth;
        TagList tags = new()
        {
            { FolderTelemetryNames.SignalTag, FolderTelemetryNames.DeadLetterDepthSignal },
            { FolderTelemetryNames.SeverityTag, FolderTelemetryNames.SeverityWarning },
            { FolderTelemetryNames.DomainTag, BoundedCategory(domain) },
        };
        DeadLetterDepthHistogram.Record(bounded, tags);
    }

    /// <summary>Records the provider-failure signal keyed by the bounded provider-failure taxonomy.</summary>
    public void RecordProviderFailure(ProviderFailureCategory category)
    {
        TagList tags = new()
        {
            { FolderTelemetryNames.SignalTag, FolderTelemetryNames.ProviderFailureSignal },
            { FolderTelemetryNames.SeverityTag, FolderTelemetryNames.SeverityError },
            { FolderTelemetryNames.ProviderFailureCategoryTag, category.ToCategoryCode() },
        };
        ProviderFailureCounter.Add(1, tags);
    }

    /// <summary>Records the stale/abandoned/interrupted-lock signal. Observe-only: never auto-releases.</summary>
    public void RecordStaleLock(string? lockState)
    {
        TagList tags = new()
        {
            { FolderTelemetryNames.SignalTag, FolderTelemetryNames.StaleLockSignal },
            { FolderTelemetryNames.SeverityTag, FolderTelemetryNames.SeverityWarning },
            { FolderTelemetryNames.LockStateTag, BoundedCategory(lockState) },
        };
        StaleLockCounter.Add(1, tags);
    }

    /// <summary>Records the cleanup-failure signal. Observe-only: no repair automation in MVP.</summary>
    public void RecordCleanupFailure(string? cleanupStatus, string? reasonCode, bool retryEligible)
    {
        TagList tags = new()
        {
            { FolderTelemetryNames.SignalTag, FolderTelemetryNames.CleanupFailureSignal },
            { FolderTelemetryNames.SeverityTag, FolderTelemetryNames.SeverityError },
            { FolderTelemetryNames.CleanupStatusTag, BoundedCategory(cleanupStatus) },
            { FolderTelemetryNames.ReasonCodeTag, BoundedCategory(reasonCode) },
            { FolderTelemetryNames.RetryEligibleTag, retryEligible },
        };
        CleanupFailureCounter.Add(1, tags);
    }

    // Caller-supplied string labels are bounded to the sanitizer's low-cardinality category grammar
    // and fall back to "unknown" so a leaked identifier or sentinel can never reach a metric tag.
    private static string BoundedCategory(string? value)
        => FolderAuditSanitizer.TrySanitizeCategory(value, out string? sanitized) && sanitized is not null
            ? sanitized
            : "unknown";
}
