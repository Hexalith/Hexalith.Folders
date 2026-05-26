namespace Hexalith.Folders.Workers.RepositoryProvisioning;

public enum RepositoryProvisioningResultCode
{
    Bound,
    Failed,
    UnknownProviderOutcome,
    ReconciliationRequired,
    AlreadyProcessed,
    ContextMismatch,
    ProviderUnavailable,
    StateUnavailable,
    AppendConflict,
}
