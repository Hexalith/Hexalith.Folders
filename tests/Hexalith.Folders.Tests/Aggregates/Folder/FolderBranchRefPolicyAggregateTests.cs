using Hexalith.Folders.Aggregates.Folder;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderBranchRefPolicyAggregateTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 27, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ConfigureBranchRefPolicyShouldAppendMetadataOnlyEventForBoundRepository()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState state = BoundState(streamName);
        ConfigureBranchRefPolicy command = Command();

        FolderResult result = FolderAggregate.Handle(state, command, Now);

        result.Code.ShouldBe(FolderResultCode.Accepted);
        BranchRefPolicyConfigured configured = result.Events.ShouldHaveSingleItem().ShouldBeOfType<BranchRefPolicyConfigured>();
        configured.RepositoryBindingId.ShouldBe("repository-binding-a");
        configured.PolicyRef.ShouldBe("opaque_policy_a");
        configured.DefaultRef.ShouldBe("branch_ref_primary");
        configured.AllowedRefPatterns.ShouldBe(["branch_ref_feature"]);
        configured.ProtectedRefPatterns.ShouldBe(["branch_ref_release"]);
        configured.OccurredAt.ShouldBe(Now);

        FolderState applied = state.Apply(result.Events, streamName);
        applied.BranchRefPolicy.ShouldNotBeNull().PolicyRef.ShouldBe("opaque_policy_a");
        applied.BranchRefPolicy.AllowedRefPatterns.ShouldBe(["branch_ref_feature"]);
    }

    [Fact]
    public void ConfigureBranchRefPolicyShouldRejectRepositoryBindingMismatch()
    {
        FolderResult result = FolderAggregate.Handle(
            BoundState(FolderStreamName.Create("tenant-a", "folder-a")),
            Command(repositoryBindingId: "repository-binding-b"),
            Now);

        result.Code.ShouldBe(FolderResultCode.StateTransitionInvalid);
        result.Events.ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(InvalidStates))]
    public void ConfigureBranchRefPolicyShouldRejectInvalidFolderOrBindingStates(
        FolderState state,
        FolderResultCode expectedCode)
    {
        FolderResult result = FolderAggregate.Handle(state, Command(), Now);

        result.Code.ShouldBe(expectedCode);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void EquivalentConfigureReplayShouldNotAppendAnotherEvent()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState configured = ConfiguredState(streamName);

        FolderResult result = FolderAggregate.Handle(configured, Command(), Now.AddMinutes(1));

        result.Code.ShouldBe(FolderResultCode.IdempotentReplay);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void DifferentConfigureWithSameIdempotencyKeyShouldConflict()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState configured = ConfiguredState(streamName);

        FolderResult result = FolderAggregate.Handle(
            configured,
            Command(defaultRef: "branch_ref_secondary"),
            Now.AddMinutes(1));

        result.Code.ShouldBe(FolderResultCode.IdempotencyConflict);
        result.Events.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("branch_ref_primary", "branch_ref_feature", "branch_ref_feature")]
    [InlineData("feature/raw", "branch_ref_feature", "branch_ref_release")]
    [InlineData("branch_ref_primary", "feature/raw", "branch_ref_release")]
    public void ConfigureBranchRefPolicyShouldRejectMalformedOrDuplicatePatterns(
        string defaultRef,
        string allowedRef,
        string protectedRef)
    {
        FolderResult result = FolderAggregate.Handle(
            BoundState(FolderStreamName.Create("tenant-a", "folder-a")),
            Command(defaultRef: defaultRef, allowedRefPatterns: [allowedRef, protectedRef], protectedRefPatterns: [protectedRef]),
            Now);

        result.Code.ShouldBe(FolderResultCode.ValidationFailed);
        result.Events.ShouldBeEmpty();
    }

    private static ConfigureBranchRefPolicy Command(
        string repositoryBindingId = "repository-binding-a",
        string defaultRef = "branch_ref_primary",
        IReadOnlyList<string>? allowedRefPatterns = null,
        IReadOnlyList<string>? protectedRefPatterns = null)
        => new(
            "tenant-a",
            "organization-a",
            "folder-a",
            "v1",
            repositoryBindingId,
            "opaque_policy_a",
            defaultRef,
            allowedRefPatterns ?? ["branch_ref_feature"],
            protectedRefPatterns ?? ["branch_ref_release"],
            "principal-a",
            "correlation-a",
            "task-a",
            "idempotency-policy-a",
            PayloadTenantId: null);

    public static IEnumerable<object[]> InvalidStates()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState created = CreatedState(streamName);

        yield return [FolderState.Empty, FolderResultCode.FolderNotFound];
        yield return [ArchivedState(streamName), FolderResultCode.StateTransitionInvalid];
        yield return [created, FolderResultCode.StateTransitionInvalid];
        yield return [BindingRequestedState(streamName), FolderResultCode.StateTransitionInvalid];
        yield return [FailedState(streamName), FolderResultCode.StateTransitionInvalid];
        yield return [UnknownState(streamName, reconciliationRequired: false), FolderResultCode.StateTransitionInvalid];
        yield return [UnknownState(streamName, reconciliationRequired: true), FolderResultCode.StateTransitionInvalid];
    }

    private static FolderState BoundState(FolderStreamName streamName)
    {
        RepositoryBound bound = new(
            "tenant-a",
            "organization-a",
            "folder-a",
            "repository-binding-a",
            "provider-binding-a",
            "correlation-bound-a",
            "task-bound-a",
            "idempotency-bound-a",
            "fingerprint-bound-a",
            Now);
        return CreatedState(streamName).Apply([bound], streamName);
    }

    private static FolderState ConfiguredState(FolderStreamName streamName)
    {
        FolderState bound = BoundState(streamName);
        FolderResult configured = FolderAggregate.Handle(bound, Command(), Now);
        return bound.Apply(configured.Events, streamName);
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

    private static FolderState BindingRequestedState(FolderStreamName streamName)
    {
        FolderState created = CreatedState(streamName);
        FolderResult requested = FolderAggregate.Handle(created, FolderCommandFactory.CreateRepositoryBackedFolder(), Now);
        return created.Apply(requested.Events, streamName);
    }

    private static FolderState FailedState(FolderStreamName streamName)
        => BindingRequestedState(streamName).Apply(
            [
                new RepositoryBindingFailed(
                    "tenant-a",
                    "organization-a",
                    "folder-a",
                    "repository-binding-a",
                    "provider-binding-a",
                    "repository_conflict",
                    "correlation-failed-a",
                    "task-failed-a",
                    "idempotency-failed-a",
                    "fingerprint-failed-a",
                    Now),
            ],
            streamName);

    private static FolderState UnknownState(FolderStreamName streamName, bool reconciliationRequired)
        => BindingRequestedState(streamName).Apply(
            [
                new ProviderOutcomeUnknown(
                    "tenant-a",
                    "organization-a",
                    "folder-a",
                    "repository-binding-a",
                    "provider-binding-a",
                    reconciliationRequired,
                    reconciliationRequired ? "reconciliation_required" : "unknown_provider_outcome",
                    "correlation-unknown-a",
                    "task-unknown-a",
                    "idempotency-unknown-a",
                    "fingerprint-unknown-a",
                    Now),
            ],
            streamName);
}
