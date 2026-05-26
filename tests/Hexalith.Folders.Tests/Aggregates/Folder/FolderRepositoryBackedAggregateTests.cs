using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Projections.FolderList;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderRepositoryBackedAggregateTests
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 5, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ActiveFolderShouldAcceptRepositoryBindingRequestWithMetadataOnlyEvent()
    {
        FolderState active = CreatedState();
        CreateRepositoryBackedFolder command = FolderCommandFactory.CreateRepositoryBackedFolder();

        FolderResult result = FolderAggregate.Handle(active, command, OccurredAt);

        result.Code.ShouldBe(FolderResultCode.Accepted);
        RepositoryBindingRequested requested = result.Events.ShouldHaveSingleItem().ShouldBeOfType<RepositoryBindingRequested>();
        requested.RepositoryBindingId.ShouldBe("repository-binding-a");
        requested.ProviderBindingRef.ShouldBe("provider-binding-a");
        requested.RepositoryProfileRef.ShouldBe("repository-profile-a");
        requested.BranchRefPolicyRef.ShouldBe("branch-ref-policy-a");

        FolderState applied = active.Apply(result.Events, FolderStreamName.Create("tenant-a", "folder-a"));
        applied.RepositoryBindingState.ShouldBe(FolderRepositoryBindingState.BindingRequested);
        applied.RepositoryBindingId.ShouldBe("repository-binding-a");
        applied.ProviderBindingRef.ShouldBe("provider-binding-a");
        applied.RepositoryProfileRef.ShouldBe("repository-profile-a");
        applied.BranchRefPolicyRef.ShouldBe("branch-ref-policy-a");
        applied.RepositoryBindingActorPrincipalId.ShouldBe("principal-a");
        applied.RepositoryBindingCorrelationId.ShouldBe("correlation-a");
        applied.RepositoryBindingTaskId.ShouldBe("task-a");
        applied.RepositoryBindingIdempotencyKey.ShouldBe("idempotency-binding-a");
        applied.RepositoryBindingIdempotencyFingerprint.ShouldBe(requested.IdempotencyFingerprint);
    }

    [Fact]
    public void RepositoryBindingRequestAgainstMissingFolderShouldRejectBeforeEvents()
    {
        FolderResult result = FolderAggregate.Handle(
            FolderState.Empty,
            FolderCommandFactory.CreateRepositoryBackedFolder());

        result.Code.ShouldBe(FolderResultCode.FolderNotFound);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void RepositoryBindingRequestAgainstArchivedFolderShouldRejectBeforeEvents()
    {
        FolderResult result = FolderAggregate.Handle(
            ArchivedState(),
            FolderCommandFactory.CreateRepositoryBackedFolder(idempotencyKey: "idempotency-binding-b"));

        result.Code.ShouldBe(FolderResultCode.StateTransitionInvalid);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void EquivalentRepositoryBindingRequestReplayShouldNotAppendAnotherEvent()
    {
        FolderState requested = RequestedState();

        FolderResult result = FolderAggregate.Handle(
            requested,
            FolderCommandFactory.CreateRepositoryBackedFolder());

        result.Code.ShouldBe(FolderResultCode.IdempotentReplay);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void DifferentRepositoryBindingRequestWithSameIdempotencyKeyShouldConflict()
    {
        FolderState requested = RequestedState();

        FolderResult result = FolderAggregate.Handle(
            requested,
            FolderCommandFactory.CreateRepositoryBackedFolder(repositoryBindingId: "repository-binding-b"));

        result.Code.ShouldBe(FolderResultCode.IdempotencyConflict);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void ProviderOutcomeEventsShouldReplayC6BindingStates()
    {
        FolderState requested = RequestedState();
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");

        FolderState bound = requested.Apply(
            [RepositoryBound()],
            streamName);
        FolderState failed = requested.Apply(
            [RepositoryBindingFailed()],
            streamName);
        FolderState unknown = requested.Apply(
            [ProviderOutcomeUnknown(reconciliationRequired: false)],
            streamName);
        FolderState reconciliation = requested.Apply(
            [ProviderOutcomeUnknown(reconciliationRequired: true)],
            streamName);

        bound.RepositoryBindingState.ShouldBe(FolderRepositoryBindingState.Bound);
        bound.RepositoryBindingActorPrincipalId.ShouldBe("principal-a");
        bound.RepositoryBindingCorrelationId.ShouldBe("correlation-a");
        bound.RepositoryBindingTaskId.ShouldBe("task-a");
        bound.RepositoryBindingIdempotencyKey.ShouldBe("idempotency-bound-a");
        failed.RepositoryBindingState.ShouldBe(FolderRepositoryBindingState.Failed);
        failed.RepositoryBindingFailureCategory.ShouldBe("repository_conflict");
        failed.RepositoryBindingActorPrincipalId.ShouldBe("principal-a");
        failed.RepositoryBindingIdempotencyKey.ShouldBe("idempotency-failed-a");
        unknown.RepositoryBindingState.ShouldBe(FolderRepositoryBindingState.UnknownProviderOutcome);
        unknown.RepositoryBindingActorPrincipalId.ShouldBe("principal-a");
        unknown.RepositoryBindingIdempotencyKey.ShouldBe("idempotency-unknown-a");
        reconciliation.RepositoryBindingState.ShouldBe(FolderRepositoryBindingState.ReconciliationRequired);
    }

    [Fact]
    public void FolderListProjectionShouldReplayRepositoryBindingRequestDeterministically()
    {
        FolderCreated created = Created();
        RepositoryBindingRequested requested = RequestedEvent(idempotencyKey: "idempotency-binding-a");

        FolderListProjection projection = FolderListProjection.Empty.Apply(
            [
                new FolderProjectionEnvelope("tenant-a", 1, created),
                new FolderProjectionEnvelope("tenant-a", 2, requested),
                new FolderProjectionEnvelope("tenant-a", 2, requested),
            ]);

        FolderListItem item = projection.Get("tenant-a", "folder-a").ShouldNotBeNull();
        item.RepositoryBindingState.ShouldBe(FolderRepositoryBindingState.BindingRequested);
        item.RepositoryBindingId.ShouldBe("repository-binding-a");
        item.ProviderBindingRef.ShouldBe("provider-binding-a");
    }

    private static FolderState CreatedState()
    {
        FolderState empty = FolderState.Empty;
        FolderResult created = FolderAggregate.Handle(empty, FolderCommandFactory.Create());
        return empty.Apply(created.Events, FolderStreamName.Create("tenant-a", "folder-a"));
    }

    private static FolderState RequestedState()
    {
        FolderState active = CreatedState();
        FolderResult requested = FolderAggregate.Handle(active, FolderCommandFactory.CreateRepositoryBackedFolder(), OccurredAt);
        return active.Apply(requested.Events, FolderStreamName.Create("tenant-a", "folder-a"));
    }

    private static FolderState ArchivedState()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState active = CreatedState();
        FolderResult archived = FolderAggregate.Handle(active, FolderCommandFactory.Archive());
        return active.Apply(archived.Events, streamName);
    }

    private static FolderCreated Created()
        => new(
            "tenant-a",
            "organization-a",
            "folder-a",
            "Folder A",
            null,
            null,
            [],
            FolderLifecycleState.Active,
            FolderRepositoryBindingState.Unbound,
            "principal-a",
            "correlation-a",
            "task-a",
            "idempotency-create-a",
            "fingerprint-create-a",
            OccurredAt);

    private static RepositoryBindingRequested RequestedEvent(string idempotencyKey)
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
            idempotencyKey,
            "fingerprint-repository-a",
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

    private static RepositoryBindingFailed RepositoryBindingFailed()
        => new(
            "tenant-a",
            "organization-a",
            "folder-a",
            "repository-binding-a",
            "provider-binding-a",
            "repository_conflict",
            "correlation-a",
            "task-a",
            "idempotency-failed-a",
            "fingerprint-failed-a",
            OccurredAt);

    private static ProviderOutcomeUnknown ProviderOutcomeUnknown(bool reconciliationRequired)
        => new(
            "tenant-a",
            "organization-a",
            "folder-a",
            "repository-binding-a",
            "provider-binding-a",
            reconciliationRequired,
            "unknown_provider_outcome",
            "correlation-a",
            "task-a",
            "idempotency-unknown-a",
            "fingerprint-unknown-a",
            OccurredAt);
}
