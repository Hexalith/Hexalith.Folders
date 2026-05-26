using Hexalith.Folders.Aggregates.Folder;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderBindRepositoryAggregateTests
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 5, 26, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ActiveFolderShouldAcceptExistingRepositoryBindingWithoutProvisioningEvent()
    {
        FolderState active = CreatedState();
        BindRepository command = FolderCommandFactory.BindRepository();

        FolderResult result = FolderAggregate.Handle(active, command, OccurredAt);

        result.Code.ShouldBe(FolderResultCode.Accepted);
        ExistingRepositoryBindingRequested requested = result.Events.ShouldHaveSingleItem().ShouldBeOfType<ExistingRepositoryBindingRequested>();
        requested.RepositoryBindingId.ShouldBe("repository-binding-a");
        requested.ProviderBindingRef.ShouldBe("provider-binding-a");
        requested.BranchRefPolicyRef.ShouldBe("branch-ref-policy-a");
        requested.ExternalRepositoryRefFingerprint.ShouldNotBe("external-repository-a");
        result.Events.Any(static folderEvent => folderEvent is RepositoryBindingRequested).ShouldBeFalse();

        FolderState applied = active.Apply(result.Events, FolderStreamName.Create("tenant-a", "folder-a"));
        applied.RepositoryBindingState.ShouldBe(FolderRepositoryBindingState.BindingRequested);
        applied.RepositoryBindingId.ShouldBe("repository-binding-a");
        applied.ProviderBindingRef.ShouldBe("provider-binding-a");
        applied.RepositoryProfileRef.ShouldBeNull();
        applied.ExternalRepositoryRefFingerprint.ShouldBe(requested.ExternalRepositoryRefFingerprint);
        applied.BranchRefPolicyRef.ShouldBe("branch-ref-policy-a");
    }

    [Fact]
    public void EquivalentExistingRepositoryBindingReplayShouldShortCircuitBeforeNewEvent()
    {
        FolderState requested = RequestedState();

        FolderResult result = FolderAggregate.Handle(requested, FolderCommandFactory.BindRepository(), OccurredAt);

        result.Code.ShouldBe(FolderResultCode.IdempotentReplay);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void DifferentExistingRepositoryBindingWithSameIdempotencyKeyShouldConflict()
    {
        FolderState requested = RequestedState();

        FolderResult result = FolderAggregate.Handle(
            requested,
            FolderCommandFactory.BindRepository(externalRepositoryRef: "external-repository-b"),
            OccurredAt);

        result.Code.ShouldBe(FolderResultCode.IdempotencyConflict);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void ExistingRepositoryBindingAgainstMissingFolderShouldRejectWithoutEvents()
    {
        FolderResult result = FolderAggregate.Handle(
            FolderState.Empty,
            FolderCommandFactory.BindRepository(),
            OccurredAt);

        result.Code.ShouldBe(FolderResultCode.FolderNotFound);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void ExistingRepositoryBindingAgainstArchivedFolderShouldRejectWithoutEvents()
    {
        FolderState active = CreatedState();
        FolderResult archived = FolderAggregate.Handle(active, FolderCommandFactory.Archive(), OccurredAt);
        FolderState state = active.Apply(archived.Events, FolderStreamName.Create("tenant-a", "folder-a"));

        FolderResult result = FolderAggregate.Handle(
            state,
            FolderCommandFactory.BindRepository(idempotencyKey: "idempotency-bind-b"),
            OccurredAt);

        result.Code.ShouldBe(FolderResultCode.StateTransitionInvalid);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void ExistingRepositoryBindingInProgressWithDifferentKeyShouldRejectWithoutEvents()
    {
        FolderState requested = RequestedState();

        FolderResult result = FolderAggregate.Handle(
            requested,
            FolderCommandFactory.BindRepository(idempotencyKey: "idempotency-bind-b"),
            OccurredAt);

        result.Code.ShouldBe(FolderResultCode.StateTransitionInvalid);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void BoundRepositoryWithDifferentKeyShouldRejectDuplicateBindingWithoutEvents()
    {
        FolderState requested = RequestedState();
        FolderState bound = requested.Apply(
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
                    OccurredAt.AddMinutes(1)),
            ],
            FolderStreamName.Create("tenant-a", "folder-a"));

        FolderResult result = FolderAggregate.Handle(
            bound,
            FolderCommandFactory.BindRepository(idempotencyKey: "idempotency-bind-b"),
            OccurredAt.AddMinutes(2));

        result.Code.ShouldBe(FolderResultCode.StateTransitionInvalid);
        result.Events.ShouldBeEmpty();
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
        FolderResult requested = FolderAggregate.Handle(active, FolderCommandFactory.BindRepository(), OccurredAt);
        return active.Apply(requested.Events, FolderStreamName.Create("tenant-a", "folder-a"));
    }
}
