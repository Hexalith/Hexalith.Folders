namespace Hexalith.Folders.Aggregates.Folder;

public static class FolderStateTransitions
{
    public static IReadOnlyList<FolderWorkspaceLifecycleState> StateCatalog { get; } =
    [
        FolderWorkspaceLifecycleState.Requested,
        FolderWorkspaceLifecycleState.Preparing,
        FolderWorkspaceLifecycleState.Ready,
        FolderWorkspaceLifecycleState.Locked,
        FolderWorkspaceLifecycleState.ChangesStaged,
        FolderWorkspaceLifecycleState.Dirty,
        FolderWorkspaceLifecycleState.Committed,
        FolderWorkspaceLifecycleState.Failed,
        FolderWorkspaceLifecycleState.Inaccessible,
        FolderWorkspaceLifecycleState.UnknownProviderOutcome,
        FolderWorkspaceLifecycleState.ReconciliationRequired,
    ];

    public static IReadOnlyList<FolderWorkspaceLifecycleEvent> EventVocabulary { get; } =
    [
        FolderWorkspaceLifecycleEvent.RepositoryBindingRequested,
        FolderWorkspaceLifecycleEvent.RepositoryBound,
        FolderWorkspaceLifecycleEvent.RepositoryBindingFailed,
        FolderWorkspaceLifecycleEvent.ProviderOutcomeUnknown,
        FolderWorkspaceLifecycleEvent.WorkspacePrepared,
        FolderWorkspaceLifecycleEvent.WorkspacePreparationFailed,
        FolderWorkspaceLifecycleEvent.WorkspaceLocked,
        FolderWorkspaceLifecycleEvent.AuthRevocationDetected,
        FolderWorkspaceLifecycleEvent.TenantRevoked,
        FolderWorkspaceLifecycleEvent.RepositoryDeletedAtProvider,
        FolderWorkspaceLifecycleEvent.ReconciliationRequested,
        FolderWorkspaceLifecycleEvent.FileMutated,
        FolderWorkspaceLifecycleEvent.WorkspaceLockReleased,
        FolderWorkspaceLifecycleEvent.LockLeaseExpired,
        FolderWorkspaceLifecycleEvent.CommitSucceeded,
        FolderWorkspaceLifecycleEvent.CommitFailed,
        FolderWorkspaceLifecycleEvent.OperatorDiscardRequested,
        FolderWorkspaceLifecycleEvent.OperatorRetrySucceeded,
        FolderWorkspaceLifecycleEvent.ProviderReadinessValidated,
        FolderWorkspaceLifecycleEvent.ReconciliationCompletedClean,
        FolderWorkspaceLifecycleEvent.ReconciliationCompletedDirty,
        FolderWorkspaceLifecycleEvent.ReconciliationEscalated,
        FolderWorkspaceLifecycleEvent.OperatorMarkedFailed,
    ];

    public static FolderWorkspaceTransitionResult Transition(
        FolderWorkspaceLifecycleState? currentState,
        FolderWorkspaceLifecycleEvent attemptedEvent,
        FolderWorkspaceDirtyResolution? dirtyResolution = null)
    {
        FolderWorkspaceLifecycleState? nextState = (currentState, attemptedEvent, dirtyResolution) switch
        {
            (null, FolderWorkspaceLifecycleEvent.RepositoryBindingRequested, null)
                => FolderWorkspaceLifecycleState.Requested,
            (FolderWorkspaceLifecycleState.Requested, FolderWorkspaceLifecycleEvent.RepositoryBound, null)
                => FolderWorkspaceLifecycleState.Preparing,
            (FolderWorkspaceLifecycleState.Requested, FolderWorkspaceLifecycleEvent.RepositoryBindingFailed, null)
                => FolderWorkspaceLifecycleState.Failed,
            (FolderWorkspaceLifecycleState.Requested, FolderWorkspaceLifecycleEvent.ProviderOutcomeUnknown, null)
                => FolderWorkspaceLifecycleState.UnknownProviderOutcome,
            (FolderWorkspaceLifecycleState.Preparing, FolderWorkspaceLifecycleEvent.WorkspacePrepared, null)
                => FolderWorkspaceLifecycleState.Ready,
            (FolderWorkspaceLifecycleState.Preparing, FolderWorkspaceLifecycleEvent.WorkspacePreparationFailed, null)
                => FolderWorkspaceLifecycleState.Failed,
            (FolderWorkspaceLifecycleState.Preparing, FolderWorkspaceLifecycleEvent.ProviderOutcomeUnknown, null)
                => FolderWorkspaceLifecycleState.UnknownProviderOutcome,
            (FolderWorkspaceLifecycleState.Ready, FolderWorkspaceLifecycleEvent.WorkspaceLocked, null)
                => FolderWorkspaceLifecycleState.Locked,
            (FolderWorkspaceLifecycleState.Ready, FolderWorkspaceLifecycleEvent.AuthRevocationDetected, null)
                => FolderWorkspaceLifecycleState.Inaccessible,
            (FolderWorkspaceLifecycleState.Ready, FolderWorkspaceLifecycleEvent.TenantRevoked, null)
                => FolderWorkspaceLifecycleState.Inaccessible,
            (FolderWorkspaceLifecycleState.Ready, FolderWorkspaceLifecycleEvent.RepositoryDeletedAtProvider, null)
                => FolderWorkspaceLifecycleState.Inaccessible,
            (FolderWorkspaceLifecycleState.Ready, FolderWorkspaceLifecycleEvent.ReconciliationRequested, null)
                => FolderWorkspaceLifecycleState.ReconciliationRequired,
            (FolderWorkspaceLifecycleState.Locked, FolderWorkspaceLifecycleEvent.FileMutated, null)
                => FolderWorkspaceLifecycleState.ChangesStaged,
            (FolderWorkspaceLifecycleState.Locked, FolderWorkspaceLifecycleEvent.WorkspaceLockReleased, null)
                => FolderWorkspaceLifecycleState.Ready,
            (FolderWorkspaceLifecycleState.Locked, FolderWorkspaceLifecycleEvent.LockLeaseExpired, null)
                => FolderWorkspaceLifecycleState.Dirty,
            (FolderWorkspaceLifecycleState.Locked, FolderWorkspaceLifecycleEvent.AuthRevocationDetected, null)
                => FolderWorkspaceLifecycleState.Inaccessible,
            (FolderWorkspaceLifecycleState.ChangesStaged, FolderWorkspaceLifecycleEvent.FileMutated, null)
                => FolderWorkspaceLifecycleState.ChangesStaged,
            (FolderWorkspaceLifecycleState.ChangesStaged, FolderWorkspaceLifecycleEvent.CommitSucceeded, null)
                => FolderWorkspaceLifecycleState.Committed,
            (FolderWorkspaceLifecycleState.ChangesStaged, FolderWorkspaceLifecycleEvent.CommitFailed, null)
                => FolderWorkspaceLifecycleState.Failed,
            (FolderWorkspaceLifecycleState.ChangesStaged, FolderWorkspaceLifecycleEvent.ProviderOutcomeUnknown, null)
                => FolderWorkspaceLifecycleState.UnknownProviderOutcome,
            (FolderWorkspaceLifecycleState.ChangesStaged, FolderWorkspaceLifecycleEvent.LockLeaseExpired, null)
                => FolderWorkspaceLifecycleState.Dirty,
            (FolderWorkspaceLifecycleState.Committed, FolderWorkspaceLifecycleEvent.WorkspaceLockReleased, null)
                => FolderWorkspaceLifecycleState.Ready,
            (FolderWorkspaceLifecycleState.Dirty, FolderWorkspaceLifecycleEvent.ReconciliationRequested, null)
                => FolderWorkspaceLifecycleState.ReconciliationRequired,
            (FolderWorkspaceLifecycleState.Dirty, FolderWorkspaceLifecycleEvent.OperatorDiscardRequested, null)
                => FolderWorkspaceLifecycleState.Failed,
            (FolderWorkspaceLifecycleState.Failed, FolderWorkspaceLifecycleEvent.ReconciliationRequested, null)
                => FolderWorkspaceLifecycleState.ReconciliationRequired,
            (FolderWorkspaceLifecycleState.Failed, FolderWorkspaceLifecycleEvent.OperatorRetrySucceeded, null)
                => FolderWorkspaceLifecycleState.Ready,
            (FolderWorkspaceLifecycleState.Inaccessible, FolderWorkspaceLifecycleEvent.ProviderReadinessValidated, null)
                => FolderWorkspaceLifecycleState.Ready,
            (FolderWorkspaceLifecycleState.UnknownProviderOutcome, FolderWorkspaceLifecycleEvent.ReconciliationCompletedClean, null)
                => FolderWorkspaceLifecycleState.Ready,
            (FolderWorkspaceLifecycleState.UnknownProviderOutcome, FolderWorkspaceLifecycleEvent.ReconciliationCompletedDirty, FolderWorkspaceDirtyResolution.CommitConfirmed)
                => FolderWorkspaceLifecycleState.Committed,
            (FolderWorkspaceLifecycleState.UnknownProviderOutcome, FolderWorkspaceLifecycleEvent.ReconciliationCompletedDirty, FolderWorkspaceDirtyResolution.CommitRejected)
                => FolderWorkspaceLifecycleState.Failed,
            (FolderWorkspaceLifecycleState.UnknownProviderOutcome, FolderWorkspaceLifecycleEvent.ReconciliationEscalated, null)
                => FolderWorkspaceLifecycleState.ReconciliationRequired,
            (FolderWorkspaceLifecycleState.ReconciliationRequired, FolderWorkspaceLifecycleEvent.ReconciliationCompletedClean, null)
                => FolderWorkspaceLifecycleState.Ready,
            (FolderWorkspaceLifecycleState.ReconciliationRequired, FolderWorkspaceLifecycleEvent.ReconciliationCompletedDirty, null)
                => FolderWorkspaceLifecycleState.Committed,
            (FolderWorkspaceLifecycleState.ReconciliationRequired, FolderWorkspaceLifecycleEvent.OperatorMarkedFailed, null)
                => FolderWorkspaceLifecycleState.Failed,
            _ => null,
        };

        if (nextState is null)
        {
            return new(
                false,
                currentState,
                attemptedEvent,
                currentState,
                FolderResultCode.StateTransitionInvalid,
                currentState is null ? null : GetOperatorDisposition(currentState.Value));
        }

        return new(
            true,
            currentState,
            attemptedEvent,
            nextState.Value,
            FolderResultCode.Accepted,
            GetOperatorDisposition(nextState.Value));
    }

    public static FolderOperatorDisposition GetOperatorDisposition(
        FolderWorkspaceLifecycleState state,
        bool hasProjectionLagEvidence = false)
        => state switch
        {
            FolderWorkspaceLifecycleState.Requested => FolderOperatorDisposition.AutoRecovering,
            FolderWorkspaceLifecycleState.Preparing => FolderOperatorDisposition.AutoRecovering,
            FolderWorkspaceLifecycleState.Ready => hasProjectionLagEvidence
                ? FolderOperatorDisposition.DegradedButServing
                : FolderOperatorDisposition.Available,
            FolderWorkspaceLifecycleState.Locked => FolderOperatorDisposition.DegradedButServing,
            FolderWorkspaceLifecycleState.ChangesStaged => FolderOperatorDisposition.DegradedButServing,
            FolderWorkspaceLifecycleState.Dirty => FolderOperatorDisposition.AwaitingHuman,
            FolderWorkspaceLifecycleState.Committed => FolderOperatorDisposition.AutoRecovering,
            FolderWorkspaceLifecycleState.Failed => FolderOperatorDisposition.TerminalUntilIntervention,
            FolderWorkspaceLifecycleState.Inaccessible => FolderOperatorDisposition.TerminalUntilIntervention,
            FolderWorkspaceLifecycleState.UnknownProviderOutcome => FolderOperatorDisposition.AwaitingHuman,
            FolderWorkspaceLifecycleState.ReconciliationRequired => FolderOperatorDisposition.AwaitingHuman,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown workspace lifecycle state."),
        };

    public static string ToWireName(FolderWorkspaceLifecycleState state)
        => state switch
        {
            FolderWorkspaceLifecycleState.Requested => "requested",
            FolderWorkspaceLifecycleState.Preparing => "preparing",
            FolderWorkspaceLifecycleState.Ready => "ready",
            FolderWorkspaceLifecycleState.Locked => "locked",
            FolderWorkspaceLifecycleState.ChangesStaged => "changes_staged",
            FolderWorkspaceLifecycleState.Dirty => "dirty",
            FolderWorkspaceLifecycleState.Committed => "committed",
            FolderWorkspaceLifecycleState.Failed => "failed",
            FolderWorkspaceLifecycleState.Inaccessible => "inaccessible",
            FolderWorkspaceLifecycleState.UnknownProviderOutcome => "unknown_provider_outcome",
            FolderWorkspaceLifecycleState.ReconciliationRequired => "reconciliation_required",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown workspace lifecycle state."),
        };
}
