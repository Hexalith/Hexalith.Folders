using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Projections.FolderList;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.Folders;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Projections.FolderList;

public sealed class FolderRepositoryBindingProjectionReplayTests
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 5, 26, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ExistingRepositoryBindingReplayShouldRemainDeterministicAndMetadataOnly()
    {
        FolderProjectionEnvelope[] envelopes =
        [
            new("tenant-a", 1, Created()),
            new("tenant-a", 2, ExistingRequested()),
            new("tenant-a", 3, Bound()),
        ];

        FolderListItem first = FolderListProjection.Empty.Apply(envelopes).Get("tenant-a", "folder-a").ShouldNotBeNull();
        FolderListItem second = FolderListProjection.Empty.Apply(envelopes.Reverse()).Get("tenant-a", "folder-a").ShouldNotBeNull();

        first.Tags.ShouldBe(second.Tags);
        (first with { Tags = [] }).ShouldBe(second with { Tags = [] });
        first.RepositoryBindingState.ShouldBe(FolderRepositoryBindingState.Bound);
        first.RepositoryBindingId.ShouldBe("repository-binding-a");
        first.ProviderBindingRef.ShouldBe("provider-binding-a");
        first.ExternalRepositoryRefFingerprint.ShouldBe("external-ref-fingerprint-a");
        first.ExternalRepositoryRefFingerprint.ShouldNotBe("external-repository-a");
        first.RepositoryProfileRef.ShouldBeNull();
        first.BranchRefPolicyRef.ShouldBe("branch-ref-policy-a");
        first.RepositoryBindingFailureCategory.ShouldBeNull();
        first.RepositoryBindingOutcomeCategory.ShouldBeNull();
        first.Sequence.ShouldBe(3);
    }

    [Fact]
    public async Task ExistingRepositoryBindingLifecycleSnapshotShouldRemainDeterministicAndMetadataOnly()
    {
        InMemoryFolderLifecycleStatusReadModel firstReadModel = new(new FixedUtcClock(OccurredAt));
        InMemoryFolderRepository firstRepository = new(firstReadModel);
        InMemoryFolderLifecycleStatusReadModel secondReadModel = new(new FixedUtcClock(OccurredAt));
        InMemoryFolderRepository secondRepository = new(secondReadModel);
        IFolderEvent[] events = [Created(), ExistingRequested(), Bound()];
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");

        firstRepository.Seed(streamName, events);
        secondRepository.Seed(streamName, events);

        FolderLifecycleStatusReadModelSnapshot first = (await firstReadModel.GetAsync(
            LifecycleRequest(),
            TestContext.Current.CancellationToken).ConfigureAwait(true)).Snapshot.ShouldNotBeNull();
        FolderLifecycleStatusReadModelSnapshot second = (await secondReadModel.GetAsync(
            LifecycleRequest(),
            TestContext.Current.CancellationToken).ConfigureAwait(true)).Snapshot.ShouldNotBeNull();

        first.ShouldBe(second);
        first.LifecycleState.ShouldBe(FolderLifecycleProjectionState.Active);
        first.BindingStatus.ShouldBe(FolderRepositoryBindingStatus.Bound);
        first.RepositoryBindingId.ShouldBe("repository-binding-a");
        first.ProviderBindingRef.ShouldBe("provider-binding-a");
        first.EvidenceScope.ManagedTenantId.ShouldBe("tenant-a");
        first.EvidenceScope.PrincipalId.ShouldBe("principal-a");
        first.EvidenceScope.TaskId.ShouldBe("task-bound-a");
        first.EvidenceScope.CorrelationId.ShouldBe("correlation-bound-a");
        first.DiagnosticSentinels.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false, FolderRepositoryBindingState.UnknownProviderOutcome, "unknown_provider_outcome")]
    [InlineData(true, FolderRepositoryBindingState.ReconciliationRequired, "reconciliation_required")]
    public void UnknownProviderOutcomeReplayShouldExposeOnlyCanonicalOutcomeMetadata(
        bool reconciliationRequired,
        FolderRepositoryBindingState expectedState,
        string expectedOutcomeCategory)
    {
        FolderListProjection projection = FolderListProjection.Empty.Apply(
            [
                new FolderProjectionEnvelope("tenant-a", 1, Created()),
                new FolderProjectionEnvelope("tenant-a", 2, ExistingRequested()),
                new FolderProjectionEnvelope("tenant-a", 3, Unknown(reconciliationRequired)),
            ]);

        FolderListItem item = projection.Get("tenant-a", "folder-a").ShouldNotBeNull();
        item.RepositoryBindingState.ShouldBe(expectedState);
        item.RepositoryBindingId.ShouldBe("repository-binding-a");
        item.ProviderBindingRef.ShouldBe("provider-binding-a");
        item.ExternalRepositoryRefFingerprint.ShouldBe("external-ref-fingerprint-a");
        item.RepositoryBindingOutcomeCategory.ShouldBe(expectedOutcomeCategory);
        item.RepositoryBindingFailureCategory.ShouldBeNull();
    }

    [Fact]
    public void FailedRepositoryBindingReplayShouldPreserveFailureCategoryWithoutRawRepositoryTarget()
    {
        FolderListProjection projection = FolderListProjection.Empty.Apply(
            [
                new FolderProjectionEnvelope("tenant-a", 1, Created()),
                new FolderProjectionEnvelope("tenant-a", 2, ExistingRequested()),
                new FolderProjectionEnvelope("tenant-a", 3, Failed()),
            ]);

        FolderListItem item = projection.Get("tenant-a", "folder-a").ShouldNotBeNull();
        item.RepositoryBindingState.ShouldBe(FolderRepositoryBindingState.Failed);
        item.RepositoryBindingFailureCategory.ShouldBe("provider_permission_insufficient");
        item.RepositoryBindingOutcomeCategory.ShouldBeNull();
        item.ExternalRepositoryRefFingerprint.ShouldBe("external-ref-fingerprint-a");
        item.ExternalRepositoryRefFingerprint.ShouldNotBe("external-repository-a");
    }

    [Fact]
    public void RepositoryBindingEventBeforeFolderCreatedShouldFailLoudly()
    {
        Action replay = () => FolderListProjection.Empty.Apply(
            [
                new FolderProjectionEnvelope("tenant-a", 1, ExistingRequested()),
            ]);

        replay.ShouldThrow<InvalidOperationException>()
            .Message.ShouldContain("before any FolderCreated event", Case.Sensitive);
    }

    private static FolderCreated Created()
        => new(
            "tenant-a",
            "organization-a",
            "folder-a",
            "Folder A",
            "safe description",
            "folder-a",
            ["alpha"],
            FolderLifecycleState.Active,
            FolderRepositoryBindingState.Unbound,
            "principal-a",
            "correlation-a",
            "task-a",
            "idempotency-create-a",
            "fingerprint-create-a",
            OccurredAt);

    private static ExistingRepositoryBindingRequested ExistingRequested()
        => new(
            "tenant-a",
            "organization-a",
            "folder-a",
            "repository-binding-a",
            "provider-binding-a",
            "external-ref-fingerprint-a",
            "branch-ref-policy-a",
            "principal-a",
            "correlation-bind-a",
            "task-bind-a",
            "idempotency-bind-a",
            "fingerprint-bind-a",
            OccurredAt.AddMinutes(1));

    private static RepositoryBound Bound()
        => new(
            "tenant-a",
            "organization-a",
            "folder-a",
            "repository-binding-a",
            "provider-binding-a",
            "correlation-bound-a",
            "task-bound-a",
            "idempotency-bound-a",
            "fingerprint-bound-a",
            OccurredAt.AddMinutes(2));

    private static RepositoryBindingFailed Failed()
        => new(
            "tenant-a",
            "organization-a",
            "folder-a",
            "repository-binding-a",
            "provider-binding-a",
            "provider_permission_insufficient",
            "correlation-failed-a",
            "task-failed-a",
            "idempotency-failed-a",
            "fingerprint-failed-a",
            OccurredAt.AddMinutes(2));

    private static ProviderOutcomeUnknown Unknown(bool reconciliationRequired)
        => new(
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
            OccurredAt.AddMinutes(2));

    private static FolderLifecycleStatusReadModelRequest LifecycleRequest()
        => new(
            "tenant-a",
            "folder-a",
            "principal-a",
            "read_metadata",
            "task-bound-a",
            "correlation-bound-a",
            AuthorizationWatermark: null,
            ReadConsistency: "eventually_consistent");
}
