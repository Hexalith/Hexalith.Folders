using System.Diagnostics;
using System.Diagnostics.Metrics;

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
}
