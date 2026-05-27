namespace Hexalith.Folders.Observability;

public enum FolderAuditOperationKind
{
    Unknown = 0,
    RestMutation = 1,
    RestQuery = 2,
    ProcessCommand = 3,
    ProviderReadiness = 4,
    EventStoreGateway = 5,
    ReadModel = 6,
    StateTransition = 7,
    FileOperation = 8,
    CommitOperation = 9,
    LockOperation = 10,
    CleanupStatus = 11,
    ContextQuery = 12,
}
