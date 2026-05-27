using System.Text.Json.Serialization;

namespace Hexalith.Folders.Aggregates.Folder;

[JsonConverter(typeof(JsonStringEnumConverter<FolderWorkspaceLifecycleEvent>))]
public enum FolderWorkspaceLifecycleEvent
{
    RepositoryBindingRequested,
    RepositoryBound,
    RepositoryBindingFailed,
    ProviderOutcomeUnknown,
    WorkspacePrepared,
    WorkspacePreparationFailed,
    WorkspaceLocked,
    AuthRevocationDetected,
    TenantRevoked,
    RepositoryDeletedAtProvider,
    ReconciliationRequested,
    FileMutated,
    WorkspaceLockReleased,
    LockLeaseExpired,
    CommitSucceeded,
    CommitFailed,
    OperatorDiscardRequested,
    OperatorRetrySucceeded,
    ProviderReadinessValidated,
    ReconciliationCompletedClean,
    ReconciliationCompletedDirty,
    ReconciliationEscalated,
    OperatorMarkedFailed,
}
