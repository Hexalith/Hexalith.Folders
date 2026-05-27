using Hexalith.Folders.Aggregates.Folder;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderWorkspaceLockReleaseAggregateTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 27, 19, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ReleaseWorkspaceLockShouldAppendMetadataOnlyReleaseEventForOwnedActiveLock()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState locked = LockedState(streamName);

        FolderResult result = FolderAggregate.Handle(locked, FolderCommandFactory.ReleaseWorkspaceLock(), Now.AddMinutes(5));

        result.Code.ShouldBe(FolderResultCode.Accepted);
        WorkspaceLockReleased released = result.Events.ShouldHaveSingleItem().ShouldBeOfType<WorkspaceLockReleased>();
        released.WorkspaceLifecycleEvent.ShouldBe(FolderWorkspaceLifecycleEvent.WorkspaceLockReleased);
        released.WorkspaceId.ShouldBe("workspace-a");
        released.LockId.ShouldBe(FolderCommandFactory.DefaultLockId());
        released.HolderTaskId.ShouldBe("task-a");
        released.ReleaseReasonCode.ShouldBe("caller_completed");
        released.LeaseStatusBasis.ShouldBe("active");
        released.IdempotencyKey.ShouldBe("idempotency-release-a");

        FolderState applied = locked.Apply(result.Events, streamName);
        applied.WorkspaceLifecycleState.ShouldBe(FolderWorkspaceLifecycleState.Ready);
        applied.WorkspaceLifecycleEvent.ShouldBe(FolderWorkspaceLifecycleEvent.WorkspaceLockReleased);
        applied.WorkspaceLockId.ShouldBeNull();
        applied.WorkspaceLockHolderTaskId.ShouldBeNull();
        applied.WorkspaceLockExpiresAt.ShouldBeNull();
    }

    [Fact]
    public void EquivalentReleaseReplayShouldNotAppendDuplicateReleaseEvent()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState released = ReleasedState(streamName);

        FolderResult result = FolderAggregate.Handle(released, FolderCommandFactory.ReleaseWorkspaceLock(), Now.AddMinutes(6));

        result.Code.ShouldBe(FolderResultCode.IdempotentReplay);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void ReleaseReasonCodeShouldNotParticipateInIdempotencyEquivalence()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState released = ReleasedState(streamName);

        FolderResult result = FolderAggregate.Handle(
            released,
            FolderCommandFactory.ReleaseWorkspaceLock(releaseReasonCode: "operator_requested"),
            Now.AddMinutes(6));

        result.Code.ShouldBe(FolderResultCode.IdempotentReplay);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void SameReleaseIdempotencyKeyWithDifferentEquivalentPayloadShouldConflict()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState released = ReleasedState(streamName);

        FolderResult result = FolderAggregate.Handle(
            released,
            FolderCommandFactory.ReleaseWorkspaceLock(workspaceId: "workspace-b"),
            Now.AddMinutes(6));

        result.Code.ShouldBe(FolderResultCode.IdempotencyConflict);
        result.Events.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("v2", "caller_completed")]
    [InlineData("v1", "unsafe_reason")]
    public void ReleaseWorkspaceLockShouldRejectMalformedCommand(string schemaVersion, string releaseReasonCode)
    {
        FolderResult result = FolderAggregate.Handle(
            LockedState(FolderStreamName.Create("tenant-a", "folder-a")),
            FolderCommandFactory.ReleaseWorkspaceLock(
                requestSchemaVersion: schemaVersion,
                releaseReasonCode: releaseReasonCode),
            Now.AddMinutes(5));

        result.Code.ShouldBe(FolderResultCode.ValidationFailed);
        result.Events.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("workspace-b", "task-a", "workspace_lock_missing", null, FolderResultCode.StateTransitionInvalid)]
    [InlineData("workspace-a", "task-b", null, null, FolderResultCode.LockNotOwned)]
    [InlineData("workspace-a", "task-a", "workspace_lock_wrong", null, FolderResultCode.LockNotOwned)]
    [InlineData("workspace-a", "task-a", null, "lock_proof_wrong", FolderResultCode.LockNotOwned)]
    public void ReleaseWorkspaceLockShouldRejectWrongWorkspaceLockTaskOrProof(
        string workspaceId,
        string taskId,
        string? lockId,
        string? proof,
        FolderResultCode expected)
    {
        FolderResult result = FolderAggregate.Handle(
            LockedState(FolderStreamName.Create("tenant-a", "folder-a")),
            FolderCommandFactory.ReleaseWorkspaceLock(
                workspaceId: workspaceId,
                taskId: taskId,
                lockId: lockId,
                lockOwnershipProof: proof),
            Now.AddMinutes(5));

        result.Code.ShouldBe(expected);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void ReleaseWorkspaceLockShouldRejectExpiredLockWithoutStateChange()
    {
        FolderState locked = LockedState(FolderStreamName.Create("tenant-a", "folder-a"));

        FolderResult result = FolderAggregate.Handle(locked, FolderCommandFactory.ReleaseWorkspaceLock(), Now.AddHours(2));

        result.Code.ShouldBe(FolderResultCode.LockExpired);
        result.Events.ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(NonReleaseableStates))]
    public void ReleaseWorkspaceLockShouldRejectNonLockedStates(FolderState state, FolderResultCode expectedCode)
    {
        FolderResult result = FolderAggregate.Handle(state, FolderCommandFactory.ReleaseWorkspaceLock(), Now.AddMinutes(5));

        result.Code.ShouldBe(expectedCode);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void InvalidWorkspaceLockReleasedReplayShouldLeaveStateUnchanged()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState ready = ReadyState(streamName);
        WorkspaceLockReleased released = FolderAggregate
            .Handle(LockedState(streamName), FolderCommandFactory.ReleaseWorkspaceLock(), Now.AddMinutes(5))
            .Events
            .ShouldHaveSingleItem()
            .ShouldBeOfType<WorkspaceLockReleased>();

        FolderState applied = ready.Apply([released], streamName);

        applied.ShouldBe(ready);
    }

    public static IEnumerable<object[]> NonReleaseableStates()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");

        yield return [FolderState.Empty, FolderResultCode.FolderNotFound];
        yield return [ArchivedState(streamName), FolderResultCode.StateTransitionInvalid];
        yield return [ReadyState(streamName), FolderResultCode.LockNotOwned];
        yield return [ChangesStagedState(streamName), FolderResultCode.StateTransitionInvalid];
        yield return [CommittedState(streamName), FolderResultCode.StateTransitionInvalid];
        yield return [DirtyState(streamName), FolderResultCode.StateTransitionInvalid];
        yield return [FailedState(streamName), FolderResultCode.StateTransitionInvalid];
        yield return [InaccessibleState(streamName), FolderResultCode.StateTransitionInvalid];
        yield return [UnknownProviderOutcomeState(streamName), FolderResultCode.StateTransitionInvalid];
        yield return [ReconciliationRequiredState(streamName), FolderResultCode.StateTransitionInvalid];
    }

    private static FolderState ReleasedState(FolderStreamName streamName)
    {
        FolderState locked = LockedState(streamName);
        FolderResult released = FolderAggregate.Handle(locked, FolderCommandFactory.ReleaseWorkspaceLock(), Now.AddMinutes(5));
        return locked.Apply(released.Events, streamName);
    }

    private static FolderState LockedState(FolderStreamName streamName)
    {
        FolderState ready = ReadyState(streamName);
        FolderResult locked = FolderAggregate.Handle(ready, FolderCommandFactory.LockWorkspace(), Now);
        return ready.Apply(locked.Events, streamName);
    }

    private static FolderState ChangesStagedState(FolderStreamName streamName)
        => LockedState(streamName).Apply([WorkspaceLifecycleEvent(FolderWorkspaceLifecycleEvent.FileMutated, "file-mutated-a")], streamName);

    private static FolderState CommittedState(FolderStreamName streamName)
        => ChangesStagedState(streamName).Apply([WorkspaceLifecycleEvent(FolderWorkspaceLifecycleEvent.CommitSucceeded, "commit-succeeded-a")], streamName);

    private static FolderState DirtyState(FolderStreamName streamName)
        => LockedState(streamName).Apply([WorkspaceLifecycleEvent(FolderWorkspaceLifecycleEvent.LockLeaseExpired, "lock-expired-a")], streamName);

    private static FolderState FailedState(FolderStreamName streamName)
        => PreparingState(streamName).Apply([WorkspaceLifecycleEvent(FolderWorkspaceLifecycleEvent.WorkspacePreparationFailed, "workspace-failed-a")], streamName);

    private static FolderState InaccessibleState(FolderStreamName streamName)
        => ReadyState(streamName).Apply([WorkspaceLifecycleEvent(FolderWorkspaceLifecycleEvent.AuthRevocationDetected, "auth-revoked-a")], streamName);

    private static FolderState UnknownProviderOutcomeState(FolderStreamName streamName)
        => PreparingState(streamName).Apply([WorkspaceLifecycleEvent(FolderWorkspaceLifecycleEvent.ProviderOutcomeUnknown, "provider-unknown-a")], streamName);

    private static FolderState ReconciliationRequiredState(FolderStreamName streamName)
        => ReadyState(streamName).Apply([WorkspaceLifecycleEvent(FolderWorkspaceLifecycleEvent.ReconciliationRequested, "reconciliation-a")], streamName);

    private static FolderState ArchivedState(FolderStreamName streamName)
        => LockedState(streamName).Apply(
            [
                new FolderArchived(
                    "tenant-a",
                    "organization-a",
                    "folder-a",
                    FolderArchiveReasonCode.OperatorReview,
                    "principal-a",
                    "correlation-archive-a",
                    "task-a",
                    "idempotency-archive-a",
                    "fingerprint-archive-a",
                    Now),
            ],
            streamName);

    private static FolderState ReadyState(FolderStreamName streamName)
    {
        FolderState preparing = PreparingState(streamName);
        return preparing.Apply(
            [
                new FolderWorkspaceLifecycleEventRecorded(
                    "tenant-a",
                    "organization-a",
                    "folder-a",
                    "workspace-a",
                    FolderWorkspaceLifecycleEvent.WorkspacePrepared,
                    DirtyResolution: null,
                    OperationId: "workspace-a",
                    "correlation-prepared-a",
                    "task-a",
                    "idempotency-workspace-outcome-a",
                    "fingerprint-workspace-outcome-a",
                    Now),
            ],
            streamName);
    }

    private static FolderState PreparingState(FolderStreamName streamName)
    {
        FolderState state = FolderState.Empty;
        FolderResult created = FolderAggregate.Handle(state, FolderCommandFactory.Create(), Now);
        state = state.Apply(created.Events, streamName);
        FolderResult requested = FolderAggregate.Handle(state, FolderCommandFactory.CreateRepositoryBackedFolder(), Now);
        state = state.Apply(requested.Events, streamName);
        state = state.Apply(
            [
                new RepositoryBound(
                    "tenant-a",
                    "organization-a",
                    "folder-a",
                    "repository-binding-a",
                    "provider-binding-a",
                    "correlation-bound-a",
                    "task-bound-a",
                    "idempotency-bound-a",
                    "fingerprint-bound-a",
                    Now),
            ],
            streamName);
        FolderResult configured = FolderAggregate.Handle(
            state,
            new ConfigureBranchRefPolicy(
                "tenant-a",
                "organization-a",
                "folder-a",
                "v1",
                "repository-binding-a",
                "branch-ref-policy-a",
                "branch_ref_primary",
                ["branch_ref_feature"],
                ["branch_ref_release"],
                "principal-a",
                "correlation-policy-a",
                "task-a",
                "idempotency-policy-a",
                PayloadTenantId: null),
            Now);
        state = state.Apply(configured.Events, streamName);
        FolderResult prepare = FolderAggregate.Handle(state, FolderCommandFactory.PrepareWorkspace(), Now);
        return state.Apply(prepare.Events, streamName);
    }

    private static FolderWorkspaceLifecycleEventRecorded WorkspaceLifecycleEvent(
        FolderWorkspaceLifecycleEvent lifecycleEvent,
        string idempotencyKey)
        => new(
            "tenant-a",
            "organization-a",
            "folder-a",
            "workspace-a",
            lifecycleEvent,
            DirtyResolution: null,
            OperationId: "workspace-a",
            $"correlation-{idempotencyKey}",
            "task-a",
            idempotencyKey,
            $"fingerprint-{idempotencyKey}",
            Now);
}
