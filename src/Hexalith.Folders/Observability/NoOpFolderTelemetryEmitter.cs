using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Observability;

public sealed class NoOpFolderTelemetryEmitter : IFolderTelemetryEmitter
{
    public ValueTask EmitAsync(FolderAuditObservation observation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observation);
        return ValueTask.CompletedTask;
    }

    public void RecordProjectionLag(long ageMilliseconds, string? stateSource)
    {
    }

    public void RecordDeadLetterDepth(string? domain, long depth)
    {
    }

    public void RecordProviderFailure(ProviderFailureCategory category)
    {
    }

    public void RecordStaleLock(string? lockState)
    {
    }

    public void RecordCleanupFailure(string? cleanupStatus, string? reasonCode, bool retryEligible)
    {
    }
}
