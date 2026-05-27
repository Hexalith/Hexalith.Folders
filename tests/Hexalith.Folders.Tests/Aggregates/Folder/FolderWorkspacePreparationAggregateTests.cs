using Hexalith.Folders.Aggregates.Folder;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderWorkspacePreparationAggregateTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 27, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PrepareWorkspaceShouldAppendMetadataOnlyIntentForConfiguredPreparingWorkspace()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState state = ConfiguredPreparingState(streamName);
        PrepareWorkspace command = FolderCommandFactory.PrepareWorkspace();

        FolderResult result = FolderAggregate.Handle(state, command, Now);

        result.Code.ShouldBe(FolderResultCode.Accepted);
        WorkspacePreparationRequested requested = result.Events.ShouldHaveSingleItem().ShouldBeOfType<WorkspacePreparationRequested>();
        requested.WorkspaceId.ShouldBe("workspace-a");
        requested.RepositoryBindingId.ShouldBe("repository-binding-a");
        requested.BranchRefPolicyRef.ShouldBe("branch-ref-policy-a");
        requested.WorkspacePolicyRef.ShouldBe("workspace-policy-a");

        FolderState applied = state.Apply(result.Events, streamName);
        applied.WorkspaceLifecycleState.ShouldBe(FolderWorkspaceLifecycleState.Preparing);
        applied.WorkspaceId.ShouldBe("workspace-a");
        applied.WorkspacePolicyRef.ShouldBe("workspace-policy-a");
    }

    [Fact]
    public void EquivalentPrepareWorkspaceReplayShouldNotAppendDuplicateIntent()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState prepared = PreparedIntentState(streamName);

        FolderResult result = FolderAggregate.Handle(prepared, FolderCommandFactory.PrepareWorkspace(), Now.AddMinutes(1));

        result.Code.ShouldBe(FolderResultCode.IdempotentReplay);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void SameIdempotencyKeyWithDifferentEquivalencePayloadShouldConflict()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState prepared = PreparedIntentState(streamName);

        FolderResult result = FolderAggregate.Handle(
            prepared,
            FolderCommandFactory.PrepareWorkspace(workspacePolicyRef: "workspace-policy-b"),
            Now.AddMinutes(1));

        result.Code.ShouldBe(FolderResultCode.IdempotencyConflict);
        result.Events.ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(InvalidStates))]
    public void PrepareWorkspaceShouldRejectInvalidFolderBindingOrLifecycleStates(FolderState state, FolderResultCode expectedCode)
    {
        FolderResult result = FolderAggregate.Handle(state, FolderCommandFactory.PrepareWorkspace(), Now);

        result.Code.ShouldBe(expectedCode);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void PrepareWorkspaceShouldRejectRepositoryBindingMismatch()
    {
        FolderResult result = FolderAggregate.Handle(
            ConfiguredPreparingState(FolderStreamName.Create("tenant-a", "folder-a")),
            FolderCommandFactory.PrepareWorkspace(repositoryBindingId: "repository-binding-b"),
            Now);

        result.Code.ShouldBe(FolderResultCode.StateTransitionInvalid);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void PrepareWorkspaceShouldRejectBranchRefPolicyMismatch()
    {
        FolderResult result = FolderAggregate.Handle(
            ConfiguredPreparingState(FolderStreamName.Create("tenant-a", "folder-a")),
            FolderCommandFactory.PrepareWorkspace(branchRefPolicyRef: "branch-ref-policy-b"),
            Now);

        result.Code.ShouldBe(FolderResultCode.StateTransitionInvalid);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void WorkspacePreparedOutcomeShouldAdvancePreparingWorkspaceToReady()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState state = ConfiguredPreparingState(streamName);

        FolderState applied = state.Apply(
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

        applied.WorkspaceLifecycleState.ShouldBe(FolderWorkspaceLifecycleState.Ready);
        applied.WorkspaceOperatorDisposition.ShouldBe(FolderOperatorDisposition.Available);
        applied.WorkspaceLifecycleEvent.ShouldBe(FolderWorkspaceLifecycleEvent.WorkspacePrepared);
    }

    [Fact]
    public void InvalidWorkspacePreparedOutcomeShouldLeaveStateUnchanged()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState state = ReadyState(streamName);

        FolderState applied = state.Apply(
            [
                new FolderWorkspaceLifecycleEventRecorded(
                    "tenant-a",
                    "organization-a",
                    "folder-a",
                    "workspace-a",
                    FolderWorkspaceLifecycleEvent.WorkspacePrepared,
                    DirtyResolution: null,
                    OperationId: "workspace-a",
                    "correlation-prepared-b",
                    "task-a",
                    "idempotency-workspace-outcome-b",
                    "fingerprint-workspace-outcome-b",
                    Now.AddMinutes(1)),
            ],
            streamName);

        applied.ShouldBe(state);
    }

    public static IEnumerable<object[]> InvalidStates()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");

        yield return [FolderState.Empty, FolderResultCode.FolderNotFound];
        yield return [CreatedState(streamName), FolderResultCode.StateTransitionInvalid];
        yield return [BoundState(streamName), FolderResultCode.StateTransitionInvalid];
        yield return [ArchivedState(streamName), FolderResultCode.StateTransitionInvalid];
        yield return [ReadyState(streamName), FolderResultCode.StateTransitionInvalid];
    }

    private static FolderState PreparedIntentState(FolderStreamName streamName)
    {
        FolderState state = ConfiguredPreparingState(streamName);
        FolderResult result = FolderAggregate.Handle(state, FolderCommandFactory.PrepareWorkspace(), Now);
        return state.Apply(result.Events, streamName);
    }

    private static FolderState ReadyState(FolderStreamName streamName)
        => ConfiguredPreparingState(streamName).Apply(
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

    private static FolderState ConfiguredPreparingState(FolderStreamName streamName)
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
        return bound.Apply(configured.Events, streamName);
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
