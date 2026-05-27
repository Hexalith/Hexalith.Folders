using System.Text.Json;

using Hexalith.Folders.Aggregates.Folder;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderLifecycleReplayDeterminismTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    public static TheoryData<string, IReadOnlyList<IFolderEvent>, FolderWorkspaceLifecycleState?> DeterministicStreams()
        =>
        new()
        {
            { "successful_lifecycle", FolderLifecycleReplayFixture.SuccessfulLifecycle(), FolderWorkspaceLifecycleState.Ready },
            { "repository_binding_failed", FolderLifecycleReplayFixture.RepositoryFailureLifecycle(), FolderWorkspaceLifecycleState.Failed },
            { "provider_outcome_unknown", FolderLifecycleReplayFixture.RepositoryUnknownOutcomeLifecycle(false), FolderWorkspaceLifecycleState.UnknownProviderOutcome },
            { "repository_reconciliation_required", FolderLifecycleReplayFixture.RepositoryUnknownOutcomeLifecycle(true), FolderWorkspaceLifecycleState.UnknownProviderOutcome },
            { "workspace_preparation_failed", FolderLifecycleReplayFixture.WorkspaceFailureLifecycle(), FolderWorkspaceLifecycleState.Failed },
            { "lock_expired_dirty", FolderLifecycleReplayFixture.DirtyLockExpiredLifecycle(), FolderWorkspaceLifecycleState.Dirty },
            { "committed", FolderLifecycleReplayFixture.CommittedLifecycle(), FolderWorkspaceLifecycleState.Committed },
            { "commit_failed", FolderLifecycleReplayFixture.CommitFailureLifecycle(), FolderWorkspaceLifecycleState.Failed },
            { "commit_unknown_provider_outcome", FolderLifecycleReplayFixture.CommitUnknownOutcomeLifecycle(false), FolderWorkspaceLifecycleState.UnknownProviderOutcome },
            { "commit_reconciliation_required", FolderLifecycleReplayFixture.CommitUnknownOutcomeLifecycle(true), FolderWorkspaceLifecycleState.UnknownProviderOutcome },
            { "archived_folder", FolderLifecycleReplayFixture.ArchivedLifecycle(), null },
            { "existing_repository_binding", FolderLifecycleReplayFixture.ExistingRepositoryBindingLifecycle(), FolderWorkspaceLifecycleState.Preparing },
        };

    [Theory]
    [MemberData(nameof(DeterministicStreams))]
    public void OrderedLifecycleReplayShouldRebuildEquivalentState(
        string scenario,
        IReadOnlyList<IFolderEvent> events,
        FolderWorkspaceLifecycleState? expectedWorkspaceState)
    {
        FolderState first = FolderState.Empty.Apply(events, FolderLifecycleReplayFixture.StreamName);
        FolderState second = FolderState.Empty.Apply(events, FolderLifecycleReplayFixture.StreamName);

        Snapshot(first).ShouldBe(Snapshot(second), scenario);
        first.ManagedTenantId.ShouldBe(FolderLifecycleReplayFixture.ManagedTenantId, scenario);
        first.FolderId.ShouldBe(FolderLifecycleReplayFixture.FolderId, scenario);
        first.WorkspaceLifecycleState.ShouldBe(expectedWorkspaceState, scenario);
        if (string.Equals(scenario, "repository_reconciliation_required", StringComparison.Ordinal))
        {
            first.RepositoryBindingState.ShouldBe(FolderRepositoryBindingState.ReconciliationRequired, scenario);
            first.RepositoryBindingOutcomeCategory.ShouldBe("reconciliation_required", scenario);
        }

        if (string.Equals(scenario, "commit_reconciliation_required", StringComparison.Ordinal))
        {
            first.WorkspaceCommitReconciliationRequired.ShouldBeTrue(scenario);
            first.WorkspaceCommitOutcomeCategory.ShouldBe("reconciliation_required", scenario);
        }
    }

    [Fact]
    public void DuplicateDomainEventDeliveryShouldNotApplyStateMutationTwice()
    {
        IReadOnlyList<IFolderEvent> events = FolderLifecycleReplayFixture.DuplicateAccessDeliveryLifecycle();
        FolderState withDuplicate = FolderState.Empty.Apply(events, FolderLifecycleReplayFixture.StreamName);
        FolderState withoutDuplicate = FolderState.Empty.Apply(events.Take(events.Count - 1), FolderLifecycleReplayFixture.StreamName);

        Snapshot(withDuplicate).ShouldBe(Snapshot(withoutDuplicate));
        withDuplicate.AccessSequence.ShouldBe(1);
        withDuplicate.IdempotencyFingerprints["idempotency-duplicate-grant-a"].ShouldBe("fingerprint-duplicate-grant-a");
    }

    [Fact]
    public void ConcreteProductionFolderEventsShouldHaveExplicitReplayExpectation()
    {
        Type[] expected =
        [
            typeof(FolderCreated),
            typeof(FolderAccessGranted),
            typeof(FolderAccessRevoked),
            typeof(FolderArchived),
            typeof(RepositoryBindingRequested),
            typeof(ExistingRepositoryBindingRequested),
            typeof(RepositoryBound),
            typeof(BranchRefPolicyConfigured),
            typeof(RepositoryBindingFailed),
            typeof(ProviderOutcomeUnknown),
            typeof(WorkspacePreparationRequested),
            typeof(FolderWorkspaceLifecycleEventRecorded),
            typeof(WorkspaceLockAcquired),
            typeof(WorkspaceLockReleased),
            typeof(WorkspaceFileMutationAccepted),
            typeof(WorkspaceCommitSucceeded),
            typeof(WorkspaceCommitFailed),
            typeof(WorkspaceCommitOutcomeUnknown),
        ];
        Type[] actual = typeof(IFolderEvent).Assembly
            .GetTypes()
            .Where(static type => !type.IsAbstract && typeof(IFolderEvent).IsAssignableFrom(type))
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        actual.ShouldBe(expected.OrderBy(static type => type.FullName, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void ForeignTenantOrWrongFolderReplayShouldFailWithStableTenantMismatch()
    {
        FolderCreated created = (FolderCreated)FolderLifecycleReplayFixture.CreatedEvents()[0];
        IFolderEvent foreignTenant = created with
        {
            ManagedTenantId = "tenant-b",
        };
        IFolderEvent wrongFolder = created with
        {
            FolderId = "folder-b",
        };

        Should.Throw<InvalidOperationException>(() => FolderState.Empty.Apply([foreignTenant], FolderLifecycleReplayFixture.StreamName))
            .Message.ShouldBe($"Foreign folder event in Apply: result code {FolderResultCode.TenantMismatch}.");
        Should.Throw<InvalidOperationException>(() => FolderState.Empty.Apply([wrongFolder], FolderLifecycleReplayFixture.StreamName))
            .Message.ShouldBe($"Foreign folder event in Apply: result code {FolderResultCode.TenantMismatch}.");
    }

    [Fact]
    public void UnknownFolderEventFamilyShouldFailLoudInsteadOfNoOping()
    {
        IFolderEvent unknownEvent = new UnknownFolderEvent(
            FolderLifecycleReplayFixture.ManagedTenantId,
            FolderLifecycleReplayFixture.OrganizationId,
            FolderLifecycleReplayFixture.FolderId,
            FolderLifecycleReplayFixture.CorrelationId,
            FolderLifecycleReplayFixture.TaskId,
            "idempotency-unknown-event-a",
            "fingerprint-unknown-event-a",
            FolderLifecycleReplayFixture.OccurredAt);

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => FolderState.Empty.Apply([.. FolderLifecycleReplayFixture.CreatedEvents(), unknownEvent], FolderLifecycleReplayFixture.StreamName));

        exception.Message.ShouldBe($"Unhandled folder event type: result code {FolderResultCode.StateTransitionInvalid}.");
    }

    private static string Snapshot(FolderState state)
        => JsonSerializer.Serialize(new
        {
            State = state with
            {
                AccessOverrides = new Dictionary<FolderAccessEntryKey, FolderAccessOverride>(),
                IdempotencyFingerprints = new Dictionary<string, string>(StringComparer.Ordinal),
            },
            AccessOverrides = state.AccessOverrides
                .OrderBy(static entry => entry.Key.CanonicalValue, StringComparer.Ordinal)
                .Select(static entry => new { Key = entry.Key.CanonicalValue, Value = entry.Value })
                .ToArray(),
            IdempotencyFingerprints = state.IdempotencyFingerprints
                .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
                .Select(static entry => new { entry.Key, entry.Value })
                .ToArray(),
        }, JsonOptions);

    private sealed record UnknownFolderEvent(
        string ManagedTenantId,
        string OrganizationId,
        string FolderId,
        string CorrelationId,
        string TaskId,
        string IdempotencyKey,
        string IdempotencyFingerprint,
        DateTimeOffset OccurredAt) : IFolderEvent;
}
