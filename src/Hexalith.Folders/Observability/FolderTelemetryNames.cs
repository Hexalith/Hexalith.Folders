namespace Hexalith.Folders.Observability;

public static class FolderTelemetryNames
{
    public const string ServiceName = "Hexalith.Folders";
    public const string ActivitySourceName = "Hexalith.Folders.Observability";
    public const string MeterName = "Hexalith.Folders.Observability";
    public const string OperationSpan = "folders.operation";
    public const string ObservationsCounter = "folders.audit.observations";
    public const string DurationHistogram = "folders.audit.duration";
    public const string OperationKindTag = "folders.operation.kind";
    public const string ResultTag = "folders.result";
    public const string CategoryTag = "folders.category";
    public const string RedactionStateTag = "folders.redaction_state";
    public const string RetryTag = "folders.retry";
    public const string ReplayTag = "folders.replay";
    public const string DuplicateTag = "folders.duplicate";
    public const string CorrelationPresentTag = "folders.correlation_present";
    public const string TaskPresentTag = "folders.task_present";
    public const string TenantPresentTag = "folders.tenant_present";
    public const string ActorReferencePresentTag = "folders.actor_reference_present";

    // Story 7.12 alert-worthy operational-signal instruments. All share the single existing
    // Meter (MeterName) so the AddMeter registration captures them; production exporter/alert
    // intent is declared in deploy/observability/production. These are observe-only.
    // C2 status-freshness ceiling (ms) pinned in docs/exit-criteria/c2-freshness.md; the projection-lag
    // alert threshold traces to this value rather than an engineering guess.
    public const long C2ProjectionLagBudgetMilliseconds = 500;

    public const string ProjectionLagHistogram = "folders.projection.lag";
    public const string DeadLetterDepthHistogram = "folders.deadletter.depth";
    public const string ProviderFailureCounter = "folders.provider.failures";
    public const string StaleLockCounter = "folders.lock.stale";
    public const string CleanupFailureCounter = "folders.cleanup.failures";

    // Bounded signal names (low-cardinality enum-shaped labels).
    public const string ProjectionLagSignal = "projection_lag";
    public const string DeadLetterDepthSignal = "dead_letter_depth";
    public const string ProviderFailureSignal = "provider_failure";
    public const string StaleLockSignal = "stale_lock";
    public const string CleanupFailureSignal = "cleanup_failure";

    // Bounded severity labels mirroring the architecture log-level convention.
    public const string SeverityInformation = "information";
    public const string SeverityWarning = "warning";
    public const string SeverityError = "error";
    public const string SeverityCritical = "critical";

    // Low-cardinality signal tags: bounded categories and presence booleans only.
    public const string SignalTag = "folders.signal";
    public const string SeverityTag = "folders.severity";
    public const string StateSourceTag = "folders.state_source";
    public const string ThresholdExceededTag = "folders.threshold_exceeded";
    public const string DomainTag = "folders.domain";
    public const string ProviderFailureCategoryTag = "folders.provider_failure_category";
    public const string LockStateTag = "folders.lock_state";
    public const string CleanupStatusTag = "folders.cleanup_status";
    public const string ReasonCodeTag = "folders.reason_code";
    public const string RetryEligibleTag = "folders.retry_eligible";
}
