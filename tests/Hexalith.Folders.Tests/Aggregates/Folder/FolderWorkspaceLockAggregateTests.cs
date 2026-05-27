using Hexalith.Folders.Aggregates.Folder;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderWorkspaceLockAggregateTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 27, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public void LockWorkspaceShouldAppendMetadataOnlyLockEventForReadyWorkspace()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState state = ReadyState(streamName);
        LockWorkspace command = FolderCommandFactory.LockWorkspace();

        FolderResult result = FolderAggregate.Handle(state, command, Now);

        result.Code.ShouldBe(FolderResultCode.Accepted);
        WorkspaceLockAcquired acquired = result.Events.ShouldHaveSingleItem().ShouldBeOfType<WorkspaceLockAcquired>();
        acquired.WorkspaceLifecycleEvent.ShouldBe(FolderWorkspaceLifecycleEvent.WorkspaceLocked);
        acquired.WorkspaceId.ShouldBe("workspace-a");
        acquired.LockIntent.ShouldBe("exclusive_write");
        acquired.RequestedLeaseSeconds.ShouldBe(3600);
        acquired.HolderTaskId.ShouldBe("task-a");
        acquired.CorrelationId.ShouldBe("correlation-a");
        acquired.IdempotencyKey.ShouldBe("idempotency-lock-a");
        acquired.ExpiresAt.ShouldBe(Now.AddSeconds(3600));

        FolderState applied = state.Apply(result.Events, streamName);
        applied.WorkspaceLifecycleState.ShouldBe(FolderWorkspaceLifecycleState.Locked);
        applied.WorkspaceLifecycleEvent.ShouldBe(FolderWorkspaceLifecycleEvent.WorkspaceLocked);
        applied.WorkspaceLockId.ShouldBe(acquired.LockId);
        applied.WorkspaceLockHolderTaskId.ShouldBe("task-a");
        applied.WorkspaceLockRequestedLeaseSeconds.ShouldBe(3600);
    }

    [Fact]
    public void EquivalentLockWorkspaceReplayShouldNotAppendDuplicateLockEvent()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState locked = LockedState(streamName);

        FolderResult result = FolderAggregate.Handle(locked, FolderCommandFactory.LockWorkspace(), Now.AddMinutes(1));

        result.Code.ShouldBe(FolderResultCode.IdempotentReplay);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void SameLockIdempotencyKeyWithDifferentEquivalencePayloadShouldConflict()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState locked = LockedState(streamName);

        FolderResult result = FolderAggregate.Handle(
            locked,
            FolderCommandFactory.LockWorkspace(requestedLeaseSeconds: 7200),
            Now.AddMinutes(1));

        result.Code.ShouldBe(FolderResultCode.IdempotencyConflict);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void LockWorkspaceShouldRejectWrongWorkspaceId()
    {
        FolderResult result = FolderAggregate.Handle(
            ReadyState(FolderStreamName.Create("tenant-a", "folder-a")),
            FolderCommandFactory.LockWorkspace(workspaceId: "workspace-b"),
            Now);

        result.Code.ShouldBe(FolderResultCode.StateTransitionInvalid);
        result.Events.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("v2", "exclusive_write", 3600)]
    [InlineData("v1", "shared_read", 3600)]
    [InlineData("v1", "exclusive_write", 0)]
    [InlineData("v1", "exclusive_write", 86401)]
    public void LockWorkspaceShouldRejectMalformedCommand(string schemaVersion, string lockIntent, int leaseSeconds)
    {
        FolderResult result = FolderAggregate.Handle(
            ReadyState(FolderStreamName.Create("tenant-a", "folder-a")),
            FolderCommandFactory.LockWorkspace(
                requestSchemaVersion: schemaVersion,
                lockIntent: lockIntent,
                requestedLeaseSeconds: leaseSeconds),
            Now);

        result.Code.ShouldBe(FolderResultCode.ValidationFailed);
        result.Events.ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(InvalidStates))]
    public void LockWorkspaceShouldRejectNonReadyStates(FolderState state, FolderResultCode expectedCode)
    {
        ArgumentNullException.ThrowIfNull(state);

        LockWorkspace command = state.IdempotencyFingerprints.ContainsKey("idempotency-lock-a")
            ? FolderCommandFactory.LockWorkspace(taskId: "task-b", idempotencyKey: "idempotency-lock-b")
            : FolderCommandFactory.LockWorkspace();

        FolderResult result = FolderAggregate.Handle(state, command, Now);

        result.Code.ShouldBe(expectedCode);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void WorkspaceLockedReplayShouldAdvanceReadyWorkspaceToLocked()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState state = ReadyState(streamName);
        FolderResult result = FolderAggregate.Handle(state, FolderCommandFactory.LockWorkspace(), Now);

        FolderState applied = state.Apply(result.Events, streamName);

        applied.WorkspaceLifecycleState.ShouldBe(FolderWorkspaceLifecycleState.Locked);
        applied.WorkspaceOperatorDisposition.ShouldBe(FolderOperatorDisposition.DegradedButServing);
        applied.WorkspaceLockExpiresAt.ShouldBe(Now.AddHours(1));
    }

    [Fact]
    public void InvalidWorkspaceLockedReplayShouldLeaveStateUnchanged()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState locked = LockedState(streamName);
        WorkspaceLockAcquired secondLock = FolderAggregate
            .Handle(ReadyState(streamName), FolderCommandFactory.LockWorkspace(idempotencyKey: "idempotency-lock-b"), Now.AddMinutes(1))
            .Events
            .ShouldHaveSingleItem()
            .ShouldBeOfType<WorkspaceLockAcquired>();

        FolderState applied = locked.Apply([secondLock], streamName);

        applied.ShouldBe(locked);
    }

    public static IEnumerable<object[]> InvalidStates()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");

        yield return [FolderState.Empty, FolderResultCode.FolderNotFound];
        yield return [CreatedState(streamName), FolderResultCode.StateTransitionInvalid];
        yield return [PreparingState(streamName), FolderResultCode.StateTransitionInvalid];
        yield return [ArchivedState(streamName), FolderResultCode.StateTransitionInvalid];
        yield return [LockedState(streamName), FolderResultCode.LockConflict];
        yield return [ChangesStagedState(streamName), FolderResultCode.StateTransitionInvalid];
        yield return [DirtyState(streamName), FolderResultCode.StateTransitionInvalid];
        yield return [FailedState(streamName), FolderResultCode.StateTransitionInvalid];
        yield return [InaccessibleState(streamName), FolderResultCode.StateTransitionInvalid];
        yield return [UnknownProviderOutcomeState(streamName), FolderResultCode.StateTransitionInvalid];
        yield return [ReconciliationRequiredState(streamName), FolderResultCode.StateTransitionInvalid];
    }

    private static FolderState LockedState(FolderStreamName streamName)
    {
        FolderState ready = ReadyState(streamName);
        FolderResult result = FolderAggregate.Handle(ready, FolderCommandFactory.LockWorkspace(), Now);
        return ready.Apply(result.Events, streamName);
    }

    private static FolderState ChangesStagedState(FolderStreamName streamName)
        => LockedState(streamName).Apply(
            [
                WorkspaceLifecycleEvent(streamName, FolderWorkspaceLifecycleEvent.FileMutated, "file-mutated-a"),
            ],
            streamName);

    private static FolderState DirtyState(FolderStreamName streamName)
        => LockedState(streamName).Apply(
            [
                WorkspaceLifecycleEvent(streamName, FolderWorkspaceLifecycleEvent.LockLeaseExpired, "lock-expired-a"),
            ],
            streamName);

    private static FolderState FailedState(FolderStreamName streamName)
        => PreparingState(streamName).Apply(
            [
                WorkspaceLifecycleEvent(streamName, FolderWorkspaceLifecycleEvent.WorkspacePreparationFailed, "workspace-failed-a"),
            ],
            streamName);

    private static FolderState InaccessibleState(FolderStreamName streamName)
        => ReadyState(streamName).Apply(
            [
                WorkspaceLifecycleEvent(streamName, FolderWorkspaceLifecycleEvent.AuthRevocationDetected, "auth-revoked-a"),
            ],
            streamName);

    private static FolderState UnknownProviderOutcomeState(FolderStreamName streamName)
        => PreparingState(streamName).Apply(
            [
                WorkspaceLifecycleEvent(streamName, FolderWorkspaceLifecycleEvent.ProviderOutcomeUnknown, "provider-unknown-a"),
            ],
            streamName);

    private static FolderState ReconciliationRequiredState(FolderStreamName streamName)
        => ReadyState(streamName).Apply(
            [
                WorkspaceLifecycleEvent(streamName, FolderWorkspaceLifecycleEvent.ReconciliationRequested, "reconciliation-a"),
            ],
            streamName);

    private static FolderWorkspaceLifecycleEventRecorded WorkspaceLifecycleEvent(
        FolderStreamName streamName,
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

    private static FolderState ReadyState(FolderStreamName streamName)
        => PreparingState(streamName).Apply(
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

    private static FolderState PreparingState(FolderStreamName streamName)
    {
        FolderState bound = BoundState(streamName);
        FolderResult configured = FolderAggregate.Handle(
            bound,
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
        FolderState configuredState = bound.Apply(configured.Events, streamName);
        FolderResult requested = FolderAggregate.Handle(configuredState, FolderCommandFactory.PrepareWorkspace(), Now);
        return configuredState.Apply(requested.Events, streamName);
    }

    private static FolderState BoundState(FolderStreamName streamName)
    {
        FolderState requested = BindingRequestedState(streamName);
        return requested.Apply(
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
    }

    private static FolderState BindingRequestedState(FolderStreamName streamName)
    {
        FolderState created = CreatedState(streamName);
        FolderResult requested = FolderAggregate.Handle(created, FolderCommandFactory.CreateRepositoryBackedFolder(), Now);
        return created.Apply(requested.Events, streamName);
    }

    private static FolderState CreatedState(FolderStreamName streamName)
    {
        FolderResult created = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create());
        return FolderState.Empty.Apply(created.Events, streamName);
    }

    private static FolderState ArchivedState(FolderStreamName streamName)
    {
        FolderState created = CreatedState(streamName);
        FolderResult archived = FolderAggregate.Handle(created, FolderCommandFactory.Archive(), Now);
        return created.Apply(archived.Events, streamName);
    }
}
