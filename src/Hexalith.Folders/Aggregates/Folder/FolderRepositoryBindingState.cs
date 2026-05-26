namespace Hexalith.Folders.Aggregates.Folder;

public enum FolderRepositoryBindingState
{
    Unbound,
    BindingRequested,
    Bound,
    Failed,
    UnknownProviderOutcome,
    ReconciliationRequired,
}
