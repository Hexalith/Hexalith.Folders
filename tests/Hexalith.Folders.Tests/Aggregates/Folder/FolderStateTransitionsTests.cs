using System.Text.Json;

using Hexalith.Folders.Aggregates.Folder;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderStateTransitionsTests
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 5, 27, 9, 0, 0, TimeSpan.Zero);

    private static readonly (FolderWorkspaceLifecycleState? CurrentState, FolderWorkspaceLifecycleEvent Event, FolderWorkspaceLifecycleState NextState, FolderWorkspaceDirtyResolution? DirtyResolution)[] PositiveTransitionCases =
    [
        (null, FolderWorkspaceLifecycleEvent.RepositoryBindingRequested, FolderWorkspaceLifecycleState.Requested, null),
        (FolderWorkspaceLifecycleState.Requested, FolderWorkspaceLifecycleEvent.RepositoryBound, FolderWorkspaceLifecycleState.Preparing, null),
        (FolderWorkspaceLifecycleState.Requested, FolderWorkspaceLifecycleEvent.RepositoryBindingFailed, FolderWorkspaceLifecycleState.Failed, null),
        (FolderWorkspaceLifecycleState.Requested, FolderWorkspaceLifecycleEvent.ProviderOutcomeUnknown, FolderWorkspaceLifecycleState.UnknownProviderOutcome, null),
        (FolderWorkspaceLifecycleState.Preparing, FolderWorkspaceLifecycleEvent.WorkspacePrepared, FolderWorkspaceLifecycleState.Ready, null),
        (FolderWorkspaceLifecycleState.Preparing, FolderWorkspaceLifecycleEvent.WorkspacePreparationFailed, FolderWorkspaceLifecycleState.Failed, null),
        (FolderWorkspaceLifecycleState.Preparing, FolderWorkspaceLifecycleEvent.ProviderOutcomeUnknown, FolderWorkspaceLifecycleState.UnknownProviderOutcome, null),
        (FolderWorkspaceLifecycleState.Ready, FolderWorkspaceLifecycleEvent.WorkspaceLocked, FolderWorkspaceLifecycleState.Locked, null),
        (FolderWorkspaceLifecycleState.Ready, FolderWorkspaceLifecycleEvent.AuthRevocationDetected, FolderWorkspaceLifecycleState.Inaccessible, null),
        (FolderWorkspaceLifecycleState.Ready, FolderWorkspaceLifecycleEvent.TenantRevoked, FolderWorkspaceLifecycleState.Inaccessible, null),
        (FolderWorkspaceLifecycleState.Ready, FolderWorkspaceLifecycleEvent.RepositoryDeletedAtProvider, FolderWorkspaceLifecycleState.Inaccessible, null),
        (FolderWorkspaceLifecycleState.Ready, FolderWorkspaceLifecycleEvent.ReconciliationRequested, FolderWorkspaceLifecycleState.ReconciliationRequired, null),
        (FolderWorkspaceLifecycleState.Locked, FolderWorkspaceLifecycleEvent.FileMutated, FolderWorkspaceLifecycleState.ChangesStaged, null),
        (FolderWorkspaceLifecycleState.Locked, FolderWorkspaceLifecycleEvent.WorkspaceLockReleased, FolderWorkspaceLifecycleState.Ready, null),
        (FolderWorkspaceLifecycleState.Locked, FolderWorkspaceLifecycleEvent.LockLeaseExpired, FolderWorkspaceLifecycleState.Dirty, null),
        (FolderWorkspaceLifecycleState.Locked, FolderWorkspaceLifecycleEvent.AuthRevocationDetected, FolderWorkspaceLifecycleState.Inaccessible, null),
        (FolderWorkspaceLifecycleState.ChangesStaged, FolderWorkspaceLifecycleEvent.FileMutated, FolderWorkspaceLifecycleState.ChangesStaged, null),
        (FolderWorkspaceLifecycleState.ChangesStaged, FolderWorkspaceLifecycleEvent.CommitSucceeded, FolderWorkspaceLifecycleState.Committed, null),
        (FolderWorkspaceLifecycleState.ChangesStaged, FolderWorkspaceLifecycleEvent.CommitFailed, FolderWorkspaceLifecycleState.Failed, null),
        (FolderWorkspaceLifecycleState.ChangesStaged, FolderWorkspaceLifecycleEvent.ProviderOutcomeUnknown, FolderWorkspaceLifecycleState.UnknownProviderOutcome, null),
        (FolderWorkspaceLifecycleState.ChangesStaged, FolderWorkspaceLifecycleEvent.LockLeaseExpired, FolderWorkspaceLifecycleState.Dirty, null),
        (FolderWorkspaceLifecycleState.Committed, FolderWorkspaceLifecycleEvent.WorkspaceLockReleased, FolderWorkspaceLifecycleState.Ready, null),
        (FolderWorkspaceLifecycleState.Dirty, FolderWorkspaceLifecycleEvent.ReconciliationRequested, FolderWorkspaceLifecycleState.ReconciliationRequired, null),
        (FolderWorkspaceLifecycleState.Dirty, FolderWorkspaceLifecycleEvent.OperatorDiscardRequested, FolderWorkspaceLifecycleState.Failed, null),
        (FolderWorkspaceLifecycleState.Failed, FolderWorkspaceLifecycleEvent.ReconciliationRequested, FolderWorkspaceLifecycleState.ReconciliationRequired, null),
        (FolderWorkspaceLifecycleState.Failed, FolderWorkspaceLifecycleEvent.OperatorRetrySucceeded, FolderWorkspaceLifecycleState.Ready, null),
        (FolderWorkspaceLifecycleState.Inaccessible, FolderWorkspaceLifecycleEvent.ProviderReadinessValidated, FolderWorkspaceLifecycleState.Ready, null),
        (FolderWorkspaceLifecycleState.UnknownProviderOutcome, FolderWorkspaceLifecycleEvent.ReconciliationCompletedClean, FolderWorkspaceLifecycleState.Ready, null),
        (FolderWorkspaceLifecycleState.UnknownProviderOutcome, FolderWorkspaceLifecycleEvent.ReconciliationCompletedDirty, FolderWorkspaceLifecycleState.Committed, FolderWorkspaceDirtyResolution.CommitConfirmed),
        (FolderWorkspaceLifecycleState.UnknownProviderOutcome, FolderWorkspaceLifecycleEvent.ReconciliationCompletedDirty, FolderWorkspaceLifecycleState.Failed, FolderWorkspaceDirtyResolution.CommitRejected),
        (FolderWorkspaceLifecycleState.UnknownProviderOutcome, FolderWorkspaceLifecycleEvent.ReconciliationEscalated, FolderWorkspaceLifecycleState.ReconciliationRequired, null),
        (FolderWorkspaceLifecycleState.ReconciliationRequired, FolderWorkspaceLifecycleEvent.ReconciliationCompletedClean, FolderWorkspaceLifecycleState.Ready, null),
        (FolderWorkspaceLifecycleState.ReconciliationRequired, FolderWorkspaceLifecycleEvent.ReconciliationCompletedDirty, FolderWorkspaceLifecycleState.Committed, null),
        (FolderWorkspaceLifecycleState.ReconciliationRequired, FolderWorkspaceLifecycleEvent.OperatorMarkedFailed, FolderWorkspaceLifecycleState.Failed, null),
    ];

    public static TheoryData<FolderWorkspaceLifecycleState?, FolderWorkspaceLifecycleEvent, FolderWorkspaceLifecycleState, FolderWorkspaceDirtyResolution?> PositiveTransitions()
    {
        TheoryData<FolderWorkspaceLifecycleState?, FolderWorkspaceLifecycleEvent, FolderWorkspaceLifecycleState, FolderWorkspaceDirtyResolution?> data = [];
        foreach ((FolderWorkspaceLifecycleState? currentState, FolderWorkspaceLifecycleEvent attemptedEvent, FolderWorkspaceLifecycleState nextState, FolderWorkspaceDirtyResolution? dirtyResolution) in PositiveTransitionCases)
        {
            data.Add(currentState, attemptedEvent, nextState, dirtyResolution);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(PositiveTransitions))]
    public void PositiveTransitionsShouldReturnDocumentedTargetState(
        FolderWorkspaceLifecycleState? currentState,
        FolderWorkspaceLifecycleEvent attemptedEvent,
        FolderWorkspaceLifecycleState expectedNextState,
        FolderWorkspaceDirtyResolution? dirtyResolution)
    {
        FolderWorkspaceTransitionResult result = FolderStateTransitions.Transition(currentState, attemptedEvent, dirtyResolution);

        result.IsAccepted.ShouldBeTrue();
        result.CurrentState.ShouldBe(currentState);
        result.AttemptedEvent.ShouldBe(attemptedEvent);
        result.NextState.ShouldBe(expectedNextState);
        result.Code.ShouldBe(FolderResultCode.Accepted);
        result.OperatorDisposition.ShouldBe(FolderStateTransitions.GetOperatorDisposition(expectedNextState));
    }

    [Fact]
    public void EveryUnlistedStateEventPairShouldRejectWithoutChangingState()
    {
        HashSet<(FolderWorkspaceLifecycleState? State, FolderWorkspaceLifecycleEvent Event, FolderWorkspaceDirtyResolution? DirtyResolution)> acceptedCases = PositiveTransitionCases
            .Select(static row => (row.CurrentState, row.Event, row.DirtyResolution))
            .ToHashSet();

        foreach (FolderWorkspaceLifecycleState state in Enum.GetValues<FolderWorkspaceLifecycleState>())
        {
            foreach (FolderWorkspaceLifecycleEvent attemptedEvent in Enum.GetValues<FolderWorkspaceLifecycleEvent>())
            {
                if (acceptedCases.Contains((state, attemptedEvent, null)))
                {
                    continue;
                }

                FolderWorkspaceTransitionResult result = FolderStateTransitions.Transition(state, attemptedEvent);

                result.IsAccepted.ShouldBeFalse();
                result.CurrentState.ShouldBe(state);
                result.AttemptedEvent.ShouldBe(attemptedEvent);
                result.NextState.ShouldBe(state);
                result.Code.ShouldBe(FolderResultCode.StateTransitionInvalid);
                result.OperatorDisposition.ShouldBe(FolderStateTransitions.GetOperatorDisposition(state));
            }
        }

        foreach (FolderWorkspaceLifecycleEvent attemptedEvent in Enum.GetValues<FolderWorkspaceLifecycleEvent>())
        {
            if (acceptedCases.Contains((null, attemptedEvent, null)))
            {
                continue;
            }

            FolderWorkspaceTransitionResult result = FolderStateTransitions.Transition(null, attemptedEvent);

            result.IsAccepted.ShouldBeFalse();
            result.CurrentState.ShouldBeNull();
            result.AttemptedEvent.ShouldBe(attemptedEvent);
            result.NextState.ShouldBeNull();
            result.Code.ShouldBe(FolderResultCode.StateTransitionInvalid);
            result.OperatorDisposition.ShouldBeNull();
        }
    }

    [Fact]
    public void UnknownOutcomeDirtyReconciliationShouldRejectWithoutExplicitResolution()
    {
        FolderWorkspaceTransitionResult result = FolderStateTransitions.Transition(
            FolderWorkspaceLifecycleState.UnknownProviderOutcome,
            FolderWorkspaceLifecycleEvent.ReconciliationCompletedDirty);

        result.IsAccepted.ShouldBeFalse();
        result.CurrentState.ShouldBe(FolderWorkspaceLifecycleState.UnknownProviderOutcome);
        result.NextState.ShouldBe(FolderWorkspaceLifecycleState.UnknownProviderOutcome);
        result.Code.ShouldBe(FolderResultCode.StateTransitionInvalid);
    }

    [Fact]
    public void StateCatalogAndEventVocabularyShouldMatchC6MappingDocument()
    {
        FolderStateTransitions.StateCatalog.ShouldBe(Enum.GetValues<FolderWorkspaceLifecycleState>());
        FolderStateTransitions.StateCatalog.Select(FolderStateTransitions.ToWireName).ShouldBe(
            [
                "requested",
                "preparing",
                "ready",
                "locked",
                "changes_staged",
                "dirty",
                "committed",
                "failed",
                "inaccessible",
                "unknown_provider_outcome",
                "reconciliation_required",
            ]);

        FolderStateTransitions.EventVocabulary.ShouldBe(Enum.GetValues<FolderWorkspaceLifecycleEvent>());
        FolderStateTransitions.EventVocabulary.Select(static value => value.ToString()).ShouldBe(
            [
                "RepositoryBindingRequested",
                "RepositoryBound",
                "RepositoryBindingFailed",
                "ProviderOutcomeUnknown",
                "WorkspacePrepared",
                "WorkspacePreparationFailed",
                "WorkspaceLocked",
                "AuthRevocationDetected",
                "TenantRevoked",
                "RepositoryDeletedAtProvider",
                "ReconciliationRequested",
                "FileMutated",
                "WorkspaceLockReleased",
                "LockLeaseExpired",
                "CommitSucceeded",
                "CommitFailed",
                "OperatorDiscardRequested",
                "OperatorRetrySucceeded",
                "ProviderReadinessValidated",
                "ReconciliationCompletedClean",
                "ReconciliationCompletedDirty",
                "ReconciliationEscalated",
                "OperatorMarkedFailed",
            ]);
    }

    [Theory]
    [InlineData(FolderWorkspaceLifecycleState.Requested, FolderOperatorDisposition.AutoRecovering)]
    [InlineData(FolderWorkspaceLifecycleState.Preparing, FolderOperatorDisposition.AutoRecovering)]
    [InlineData(FolderWorkspaceLifecycleState.Ready, FolderOperatorDisposition.Available)]
    [InlineData(FolderWorkspaceLifecycleState.Locked, FolderOperatorDisposition.DegradedButServing)]
    [InlineData(FolderWorkspaceLifecycleState.ChangesStaged, FolderOperatorDisposition.DegradedButServing)]
    [InlineData(FolderWorkspaceLifecycleState.Dirty, FolderOperatorDisposition.AwaitingHuman)]
    [InlineData(FolderWorkspaceLifecycleState.Committed, FolderOperatorDisposition.AutoRecovering)]
    [InlineData(FolderWorkspaceLifecycleState.Failed, FolderOperatorDisposition.TerminalUntilIntervention)]
    [InlineData(FolderWorkspaceLifecycleState.Inaccessible, FolderOperatorDisposition.TerminalUntilIntervention)]
    [InlineData(FolderWorkspaceLifecycleState.UnknownProviderOutcome, FolderOperatorDisposition.AwaitingHuman)]
    [InlineData(FolderWorkspaceLifecycleState.ReconciliationRequired, FolderOperatorDisposition.AwaitingHuman)]
    public void OperatorDispositionShouldMatchC6StateCatalog(
        FolderWorkspaceLifecycleState state,
        FolderOperatorDisposition expected)
    {
        FolderStateTransitions.GetOperatorDisposition(state).ShouldBe(expected);
    }

    [Fact]
    public void ReadyDispositionShouldOnlyDegradeWhenProjectionLagEvidenceIsExplicit()
    {
        FolderStateTransitions.GetOperatorDisposition(FolderWorkspaceLifecycleState.Ready).ShouldBe(FolderOperatorDisposition.Available);
        FolderStateTransitions.GetOperatorDisposition(
            FolderWorkspaceLifecycleState.Ready,
            hasProjectionLagEvidence: true).ShouldBe(FolderOperatorDisposition.DegradedButServing);
    }

    [Fact]
    public void WorkspaceLifecycleEnumsShouldSerializeWithContractCompatibleNames()
    {
        JsonSerializer.Serialize(FolderWorkspaceLifecycleState.ChangesStaged).ShouldBe("\"changes_staged\"");
        JsonSerializer.Serialize(FolderWorkspaceLifecycleState.UnknownProviderOutcome).ShouldBe("\"unknown_provider_outcome\"");
        JsonSerializer.Serialize(FolderOperatorDisposition.AutoRecovering).ShouldBe("\"auto_recovering\"");
        JsonSerializer.Serialize(FolderOperatorDisposition.TerminalUntilIntervention).ShouldBe("\"terminal_until_intervention\"");
    }

    [Fact]
    public void ReplayShouldAdvanceWorkspaceLifecycleDeterministicallyForAcceptedSequences()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState created = CreatedState();

        FolderState requested = created.Apply([RepositoryBindingRequested()], streamName);
        FolderState preparing = requested.Apply([RepositoryBound()], streamName);
        FolderState ready = preparing.Apply([LifecycleEvent(FolderWorkspaceLifecycleEvent.WorkspacePrepared, "idempotency-workspace-prepared")], streamName);
        FolderState locked = ready.Apply([LifecycleEvent(FolderWorkspaceLifecycleEvent.WorkspaceLocked, "idempotency-workspace-locked")], streamName);
        FolderState staged = locked.Apply([LifecycleEvent(FolderWorkspaceLifecycleEvent.FileMutated, "idempotency-file-mutated")], streamName);
        FolderState committed = staged.Apply([LifecycleEvent(FolderWorkspaceLifecycleEvent.CommitSucceeded, "idempotency-commit-succeeded")], streamName);
        FolderState released = committed.Apply([LifecycleEvent(FolderWorkspaceLifecycleEvent.WorkspaceLockReleased, "idempotency-lock-released")], streamName);

        requested.WorkspaceLifecycleState.ShouldBe(FolderWorkspaceLifecycleState.Requested);
        preparing.WorkspaceLifecycleState.ShouldBe(FolderWorkspaceLifecycleState.Preparing);
        ready.WorkspaceLifecycleState.ShouldBe(FolderWorkspaceLifecycleState.Ready);
        locked.WorkspaceLifecycleState.ShouldBe(FolderWorkspaceLifecycleState.Locked);
        staged.WorkspaceLifecycleState.ShouldBe(FolderWorkspaceLifecycleState.ChangesStaged);
        committed.WorkspaceLifecycleState.ShouldBe(FolderWorkspaceLifecycleState.Committed);
        released.WorkspaceLifecycleState.ShouldBe(FolderWorkspaceLifecycleState.Ready);
    }

    [Fact]
    public void ReplayShouldNotMutateStateForInvalidWorkspaceLifecycleEvents()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState ready = CreatedState()
            .Apply([RepositoryBindingRequested()], streamName)
            .Apply([RepositoryBound()], streamName)
            .Apply([LifecycleEvent(FolderWorkspaceLifecycleEvent.WorkspacePrepared, "idempotency-workspace-prepared")], streamName);

        FolderState rejected = ready.Apply(
            [LifecycleEvent(FolderWorkspaceLifecycleEvent.CommitSucceeded, "idempotency-invalid-commit")],
            streamName);

        rejected.ShouldBe(ready);
    }

    [Fact]
    public void WorkspaceLifecycleEventsShouldRemainMetadataOnly()
    {
        IEnumerable<string> propertyNames = typeof(FolderWorkspaceLifecycleEventRecorded)
            .GetProperties()
            .Select(static property => property.Name);

        propertyNames.ShouldNotContain("FileContents");
        propertyNames.ShouldNotContain("Diff");
        propertyNames.ShouldNotContain("ProviderPayload");
        propertyNames.ShouldNotContain("RepositoryUrl");
        propertyNames.ShouldNotContain("CredentialMaterial");
        propertyNames.ShouldNotContain("RawBranchName");
        propertyNames.ShouldNotContain("RawPathText");
        propertyNames.ShouldNotContain("RawExceptionText");
    }

    private static FolderState CreatedState()
    {
        FolderState empty = FolderState.Empty;
        FolderResult created = FolderAggregate.Handle(empty, FolderCommandFactory.Create(), OccurredAt);
        return empty.Apply(created.Events, FolderStreamName.Create("tenant-a", "folder-a"));
    }

    private static RepositoryBindingRequested RepositoryBindingRequested()
        => new(
            "tenant-a",
            "organization-a",
            "folder-a",
            "repository-binding-a",
            "provider-binding-a",
            "repository-profile-a",
            "branch-ref-policy-a",
            "principal-a",
            "correlation-a",
            "task-a",
            "idempotency-binding-a",
            "fingerprint-binding-a",
            OccurredAt);

    private static RepositoryBound RepositoryBound()
        => new(
            "tenant-a",
            "organization-a",
            "folder-a",
            "repository-binding-a",
            "provider-binding-a",
            "correlation-a",
            "task-a",
            "idempotency-bound-a",
            "fingerprint-bound-a",
            OccurredAt);

    private static FolderWorkspaceLifecycleEventRecorded LifecycleEvent(
        FolderWorkspaceLifecycleEvent workspaceLifecycleEvent,
        string idempotencyKey)
        => new(
            "tenant-a",
            "organization-a",
            "folder-a",
            "workspace-a",
            workspaceLifecycleEvent,
            null,
            "operation-a",
            "correlation-a",
            "task-a",
            idempotencyKey,
            $"fingerprint-{idempotencyKey}",
            OccurredAt);
}
