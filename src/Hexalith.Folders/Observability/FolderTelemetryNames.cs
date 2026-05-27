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
}
