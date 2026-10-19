using System.Text.Json;

using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Projections.OperationalStatus;
using Hexalith.Folders.Tests.Aggregates.Folder;
using Hexalith.Folders.Tests.OperationalStatus;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Projections.OperationalStatus;

public sealed class OperationalStatusFoldTests
{
    public static TheoryData<IReadOnlyList<IFolderEvent>> Histories => new()
    {
        OperationalStatusFixture.SuccessfulLifecycle(), FolderLifecycleReplayFixture.RepositoryFailureLifecycle(),
        FolderLifecycleReplayFixture.WorkspaceFailureLifecycle(), FolderLifecycleReplayFixture.DirtyLockExpiredLifecycle(),
        FolderLifecycleReplayFixture.CommitFailureLifecycle(), FolderLifecycleReplayFixture.CommittedLifecycle(),
        FolderLifecycleReplayFixture.CommitUnknownOutcomeLifecycle(false), FolderLifecycleReplayFixture.CommitUnknownOutcomeLifecycle(true),
        FolderLifecycleReplayFixture.RepositoryUnknownOutcomeLifecycle(false), FolderLifecycleReplayFixture.ArchivedLifecycle(),
    };

    [Theory]
    [MemberData(nameof(Histories))]
    public void LiveDeliveryAndEmptyCheckpointReplayConverge(IReadOnlyList<IFolderEvent> history)
    {
        OperationalStatusEvent[] sources = OperationalStatusTestData.Sources(history);
        OperationalStatusFoldResult rebuilt = OperationalStatusFold.Apply("tenant-a", "folder-a", null, sources);
        OperationalStatusFoldResult? live = null;
        foreach (OperationalStatusEvent item in sources)
            live = OperationalStatusFold.Apply("tenant-a", "folder-a", live?.Checkpoint, [item]);
        JsonSerializer.Serialize(live!.Checkpoint).ShouldBe(JsonSerializer.Serialize(rebuilt.Checkpoint));
        live.Lifecycle.ShouldBe(rebuilt.Lifecycle);
        foreach (string workspace in live.Workspaces.Keys)
            JsonSerializer.Serialize(live.Workspaces[workspace]).ShouldBe(JsonSerializer.Serialize(rebuilt.Workspaces[workspace]));
        foreach (string task in live.Tasks.Keys)
            JsonSerializer.Serialize(live.Tasks[task]).ShouldBe(JsonSerializer.Serialize(rebuilt.Tasks[task]));
        OperationalStatusFold.Valid(rebuilt.Checkpoint, "tenant-a", "folder-a").ShouldBeTrue();
    }

    [Fact]
    public void ExactDuplicatesCannotAdvanceCheckpointAndDifferentBytesCannotReplaceThem()
    {
        OperationalStatusEvent[] sources = OperationalStatusTestData.Sources(FolderLifecycleReplayFixture.CreatedEvents());
        var first = OperationalStatusFold.Apply("tenant-a", "folder-a", null, sources);
        var duplicate = OperationalStatusFold.Apply("tenant-a", "folder-a", first.Checkpoint, sources);
        duplicate.Checkpoint.Sequence.ShouldBe(1);
        duplicate.Checkpoint.SourceDigest.ShouldBe(first.Checkpoint.SourceDigest);
        Should.Throw<InvalidOperationException>(() => OperationalStatusFold.Apply("tenant-a", "folder-a", first.Checkpoint,
            [sources[0] with { Digest = new string('A', 64) }]));
        Should.Throw<InvalidOperationException>(() => OperationalStatusFold.Apply("tenant-a", "folder-a", null,
            [sources[0] with { Sequence = 2 }]));
    }

    [Fact]
    public void ForeignIdentityAndCorruptCheckpointFailBeforeCandidates()
    {
        OperationalStatusEvent[] sources = OperationalStatusTestData.Sources(FolderLifecycleReplayFixture.CreatedEvents());
        Should.Throw<InvalidOperationException>(() => OperationalStatusFold.Apply("tenant-b", "folder-a", null, sources));
        var checkpoint = OperationalStatusFold.Apply("tenant-a", "folder-a", null, sources).Checkpoint;
        OperationalStatusFold.Valid(checkpoint with { SourceDigest = new string('A', 64) }, "tenant-a", "folder-a").ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => OperationalStatusFold.Apply("tenant-a", "folder-a",
            checkpoint with { SchemaVersion = 99 }, []));
    }

    [Fact]
    public void CommitTerminalEvidenceSurvivesLaterWorkspaceReleaseWithoutInventingCleanup()
    {
        var result = OperationalStatusFold.Apply("tenant-a", "folder-a", null,
            OperationalStatusTestData.Sources(OperationalStatusFixture.SuccessfulLifecycle()));
        result.Tasks["task-a"].TerminalState.ShouldBe("committed");
        result.Tasks["task-a"].CurrentState.ShouldBe("committed");
        result.Workspaces["workspace-a"].Workspace.CurrentState.ShouldBe("ready");
        result.Workspaces["workspace-a"].Cleanup.Status.ShouldBe("status_only");
        result.Workspaces["workspace-a"].Cleanup.LastAttemptedAt.ShouldBeNull();
        result.Workspaces["workspace-a"].Workspace.ProviderOutcome.ProviderCorrelationReference.ShouldBe("provref_unavailable");
        result.Workspaces["workspace-a"].Workspace.ProjectionLag.AgeMilliseconds.ShouldBeNull();
    }

    [Fact]
    public void CleanExplicitReleaseClosesTaskAndDeniesNewWorkAfterEquivalentReplay()
    {
        FolderStreamName stream = FolderStreamName.Create("tenant-a", "folder-a");
        IReadOnlyList<IFolderEvent> history = FolderLifecycleReplayFixture.LockedLifecycleForTenant("tenant-a");
        FolderState state = FolderState.Empty.Apply(history, stream);
        ReleaseWorkspaceLock command = FolderCommandFactory.ReleaseWorkspaceLock(lockId: "lock-id-a");
        FolderResult release = FolderAggregate.Handle(state, command, FolderLifecycleReplayFixture.OccurredAt.AddMinutes(7));
        release.Code.ShouldBe(FolderResultCode.Accepted);
        state = state.Apply(release.Events, stream);
        state.TaskClosures.Values.ShouldHaveSingleItem().TerminalState.ShouldBe("ready");
        FolderAggregate.Handle(state, command).Code.ShouldBe(FolderResultCode.IdempotentReplay);
        FolderAggregate.Handle(state, FolderCommandFactory.LockWorkspace(idempotencyKey: "new-work")).Code.ShouldBe(FolderResultCode.StateTransitionInvalid);
        FolderAggregate.Handle(state, FolderCommandFactory.LockWorkspace(taskId: "new-task", idempotencyKey: "new-task-work")).Code.ShouldBe(FolderResultCode.Accepted);
        var result = OperationalStatusFold.Apply("tenant-a", "folder-a", null,
            OperationalStatusTestData.Sources([.. history, .. release.Events]));
        result.Tasks["task-a"].TerminalState.ShouldBe("ready");
        result.Checkpoint.State.TaskClosures.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnknownOutcomesRemainNonretryableWithoutTerminalSuccess(bool reconciliationRequired)
    {
        var result = OperationalStatusFold.Apply("tenant-a", "folder-a", null,
            OperationalStatusTestData.Sources(FolderLifecycleReplayFixture.CommitUnknownOutcomeLifecycle(reconciliationRequired)));
        var task = result.Tasks["task-a"];
        task.TerminalState.ShouldBeNull();
        task.RetryEligibility.Eligible.ShouldBeFalse();
        task.CurrentState.ShouldBe("unknown_provider_outcome");
        result.Workspaces["workspace-a"].Workspace.ProviderOutcome.State.ShouldBe(
            reconciliationRequired ? "reconciliation_required" : "unknown_provider_outcome");
    }

    [Fact]
    public void TerminalFailureCannotAdvertiseAnotherMutationAttempt()
    {
        var result = OperationalStatusFold.Apply("tenant-a", "folder-a", null,
            OperationalStatusTestData.Sources(FolderLifecycleReplayFixture.CommitFailureLifecycle()));
        result.Tasks["task-a"].TerminalState.ShouldBe("failed");
        result.Tasks["task-a"].RetryEligibility.Eligible.ShouldBeFalse();
        result.Workspaces["workspace-a"].Workspace.RetryEligibility.Eligible.ShouldBeFalse();
    }

    [Fact]
    public void KeysPreserveTenantAndComponentBoundaries()
    {
        OperationalStatusKeys.Workspace("a", "b.c", "d").ShouldNotBe(OperationalStatusKeys.Workspace("a.b", "c", "d"));
        OperationalStatusKeys.Task("tenant-a", "folder-a", "task-a").ShouldNotBe(OperationalStatusKeys.Task("tenant-b", "folder-a", "task-a"));
        OperationalStatusKeys.Task("tenant-a", "folder-a", "task-a").ShouldNotBe(OperationalStatusKeys.Task("tenant-a", "folder-b", "task-a"));
    }
}
