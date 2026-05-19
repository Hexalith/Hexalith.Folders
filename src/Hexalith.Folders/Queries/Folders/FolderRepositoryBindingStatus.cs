namespace Hexalith.Folders.Queries.Folders;

public enum FolderRepositoryBindingStatus
{
    Unbound,
    BindingRequested,
    Bound,
    Failed,
    UnknownProviderOutcome,
    ReconciliationRequired,
    Unsupported,
    Unknown,
}
