using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Observability;

public interface IFolderTelemetryEmitter
{
    ValueTask EmitAsync(FolderAuditObservation observation, CancellationToken cancellationToken = default);

    /// <summary>Records the projection-lag signal; threshold-exceeded traces to the pinned C2 500 ms target.</summary>
    void RecordProjectionLag(long ageMilliseconds, string? stateSource);

    /// <summary>Records the dead-letter-depth signal for a bounded domain.</summary>
    void RecordDeadLetterDepth(string? domain, long depth);

    /// <summary>Records the provider-failure signal keyed by the bounded provider-failure taxonomy.</summary>
    void RecordProviderFailure(ProviderFailureCategory category);

    /// <summary>Records the stale-lock signal. Observe-only: never auto-releases.</summary>
    void RecordStaleLock(string? lockState);

    /// <summary>Records the cleanup-failure signal. Observe-only: no repair automation in MVP.</summary>
    void RecordCleanupFailure(string? cleanupStatus, string? reasonCode, bool retryEligible);
}
