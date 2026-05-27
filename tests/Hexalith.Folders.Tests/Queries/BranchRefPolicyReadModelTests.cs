using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Tests.Aggregates.Folder;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Queries;

public sealed class BranchRefPolicyReadModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task InMemoryRepositoryShouldProjectBranchRefPolicyMetadataOnlySnapshot()
    {
        InMemoryBranchRefPolicyReadModel readModel = new(new FixedClock(Now));
        InMemoryFolderRepository repository = new(
            lifecycleReadModel: null,
            branchRefPolicyReadModel: readModel,
            timeProvider: new FixedTimeProvider(Now));
        FolderStreamName streamName = repository.CreateStreamName("tenant-a", "folder-a");
        FolderResult created = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create());
        FolderResult requested = FolderAggregate.Handle(
            FolderState.Empty.Apply(created.Events, streamName),
            FolderCommandFactory.CreateRepositoryBackedFolder(),
            Now);
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
        repository.Seed(streamName, [.. created.Events, .. requested.Events, bound]);
        FolderResult configured = FolderAggregate.Handle(
            repository.Load(streamName),
            new ConfigureBranchRefPolicy(
                "tenant-a",
                "organization-a",
                "folder-a",
                "v1",
                "repository-binding-a",
                "opaque-policy-a",
                "branch_ref_primary",
                ["branch_ref_feature"],
                ["branch_ref_release"],
                "principal-a",
                "correlation-a",
                "task-a",
                "idempotency-policy-a",
                PayloadTenantId: null),
            Now);

        repository.AppendIfFingerprintAbsent(
            streamName,
            "idempotency-policy-a",
            configured.Events.ShouldHaveSingleItem().ShouldBeOfType<BranchRefPolicyConfigured>().IdempotencyFingerprint,
            configured.Events);

        BranchRefPolicyReadModelResult result = await readModel.GetAsync(
            new BranchRefPolicyReadModelRequest(
                "tenant-a",
                "folder-a",
                "principal-a",
                "read_branch_ref_policy",
                "task-a",
                "correlation-a",
                AuthorizationWatermark: null,
                "eventually_consistent"),
            TestContext.Current.CancellationToken);

        BranchRefPolicyReadModelSnapshot snapshot = result.Snapshot.ShouldNotBeNull();
        snapshot.RepositoryBindingId.ShouldBe("repository-binding-a");
        snapshot.PolicyRef.ShouldBe("opaque-policy-a");
        snapshot.DefaultRef.ShouldBe("branch_ref_primary");
        snapshot.AllowedRefPatterns.ShouldBe(["branch_ref_feature"]);
        snapshot.ProtectedRefPatterns.ShouldBe(["branch_ref_release"]);
    }

    [Fact]
    public async Task InMemoryRepositoryShouldUseNonDecreasingObservedAtForBranchRefPolicySnapshot()
    {
        MutableTimeProvider timeProvider = new(Now);
        InMemoryBranchRefPolicyReadModel readModel = new(new FixedClock(Now.AddMinutes(5)));
        InMemoryFolderRepository repository = new(
            lifecycleReadModel: null,
            branchRefPolicyReadModel: readModel,
            timeProvider);
        FolderStreamName streamName = repository.CreateStreamName("tenant-a", "folder-a");
        FolderResult created = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create());
        FolderResult requested = FolderAggregate.Handle(
            FolderState.Empty.Apply(created.Events, streamName),
            FolderCommandFactory.CreateRepositoryBackedFolder(),
            Now);
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
        repository.Seed(streamName, [.. created.Events, .. requested.Events, bound]);

        timeProvider.UtcNow = Now.AddMinutes(2);
        FolderResult configured = FolderAggregate.Handle(
            repository.Load(streamName),
            new ConfigureBranchRefPolicy(
                "tenant-a",
                "organization-a",
                "folder-a",
                "v1",
                "repository-binding-a",
                "opaque-policy-a",
                "branch_ref_primary",
                ["branch_ref_feature"],
                ["branch_ref_release"],
                "principal-a",
                "correlation-a",
                "task-a",
                "idempotency-policy-a",
                PayloadTenantId: null),
            Now);

        repository.AppendIfFingerprintAbsent(
            streamName,
            "idempotency-policy-a",
            configured.Events.ShouldHaveSingleItem().ShouldBeOfType<BranchRefPolicyConfigured>().IdempotencyFingerprint,
            configured.Events);

        BranchRefPolicyReadModelResult result = await readModel.GetAsync(
            new BranchRefPolicyReadModelRequest(
                "tenant-a",
                "folder-a",
                "principal-a",
                "read_branch_ref_policy",
                "task-a",
                "correlation-a",
                AuthorizationWatermark: null,
                "eventually_consistent"),
            TestContext.Current.CancellationToken);

        result.Snapshot.ShouldNotBeNull().Freshness.ObservedAt.ShouldBe(Now.AddMinutes(2));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class FixedClock(DateTimeOffset now) : IUtcClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
