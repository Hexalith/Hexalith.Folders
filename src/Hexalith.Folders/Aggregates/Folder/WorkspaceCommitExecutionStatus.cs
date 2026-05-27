namespace Hexalith.Folders.Aggregates.Folder;

public enum WorkspaceCommitExecutionStatus
{
    Succeeded,
    KnownFailure,
    UnknownOutcome,
    ReconciliationRequired,
}
