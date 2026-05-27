using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.Folders;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Queries.Folders;

public sealed class WorkspaceLockStatusProjectionTests
{
    private static readonly DateTimeOffset Now = FolderLifecycleStatusTestSupport.Now;

    [Fact]
    public async Task ActiveLockShouldReturnMetadataOnlyLeaseStatus()
    {
        WorkspaceLockStatusQueryResult result = await ExecuteAsync(WorkspaceLockStatusReadModelResult.Available(ActiveSnapshot()));

        result.Code.ShouldBe(WorkspaceLockStatusQueryResultCode.Allowed);
        result.WorkspaceId.ShouldBe("workspace-a");
        result.LockState.ShouldBe("locked");
        result.Lease.ShouldNotBeNull();
        result.Lease.LockId.ShouldBe("workspace_lock_a");
        result.Lease.LeaseStatus.ShouldBe("active");
        result.Lease.HolderRef.ShouldBe("task-a");
        result.RetryEligibility.Retryable.ShouldBeFalse();
        result.RetryEligibility.ReasonCode.ShouldBe("lock_active");
        result.Freshness.ReadConsistency.ShouldBe("read_your_writes");
    }

    [Fact]
    public async Task ReleasedLockShouldReturnUnlockedWithoutLease()
    {
        WorkspaceLockStatusQueryResult result = await ExecuteAsync(WorkspaceLockStatusReadModelResult.Available(UnlockedSnapshot()));

        result.Code.ShouldBe(WorkspaceLockStatusQueryResultCode.Allowed);
        result.LockState.ShouldBe("unlocked");
        result.Lease.ShouldBeNull();
        result.RetryEligibility.Retryable.ShouldBeTrue();
        result.RetryEligibility.ReasonCode.ShouldBe("retry_not_required");
        result.RetryEligibility.TaskId.ShouldBeNull();
    }

    [Fact]
    public async Task ExpiredLockShouldBeReportedFromCallerTimeWithoutMutatingProjection()
    {
        WorkspaceLockStatusReadModelSnapshot snapshot = ActiveSnapshot(expiresAt: Now.AddSeconds(-1));
        WorkspaceLockStatusQueryResult result = await ExecuteAsync(WorkspaceLockStatusReadModelResult.Available(snapshot));

        result.Code.ShouldBe(WorkspaceLockStatusQueryResultCode.Allowed);
        result.LockState.ShouldBe("expired");
        result.Lease.ShouldNotBeNull();
        result.Lease.LeaseStatus.ShouldBe("expired");
        result.RetryEligibility.Retryable.ShouldBeTrue();
        result.RetryEligibility.ReasonCode.ShouldBe("lock_conflict_retry");
    }

    [Fact]
    public async Task SafeDenialShouldNotTouchReadModel()
    {
        CountingWorkspaceLockStatusReadModel readModel = new(WorkspaceLockStatusReadModelResult.Available(ActiveSnapshot()));
        CountingTenantAccessProjectionStore tenantStore = new();
        WorkspaceLockStatusQueryHandler handler = Handler(tenantStore, readModel);

        WorkspaceLockStatusQueryResult result = await handler.HandleAsync(
            Query(tenantId: null, principalId: null, claimTransformEvidence: EventStoreClaimTransformEvidence.Missing()),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(WorkspaceLockStatusQueryResultCode.AuthenticationRequired);
        readModel.Requests.ShouldBe(0);
    }

    [Fact]
    public async Task ProjectionUnavailableShouldReturnSafeUnavailableResult()
    {
        WorkspaceLockStatusQueryResult result = await ExecuteAsync(
            WorkspaceLockStatusReadModelResult.Unavailable("projection_unavailable", Now));

        result.Code.ShouldBe(WorkspaceLockStatusQueryResultCode.ProjectionUnavailable);
        result.Lease.ShouldBeNull();
        result.LockState.ShouldBe("denied_safe");
    }

    [Fact]
    public async Task StaleProjectionShouldReturnSafeStaleResult()
    {
        WorkspaceLockStatusQueryResult result = await ExecuteAsync(new WorkspaceLockStatusReadModelResult(
            WorkspaceLockStatusReadModelStatus.Stale,
            Snapshot: null,
            Freshness: new FolderLifecycleFreshness(
                "read_your_writes",
                Now.AddMinutes(-10),
                "lock-watermark-stale",
                Stale: true,
                "projection_stale")));

        result.Code.ShouldBe(WorkspaceLockStatusQueryResultCode.ProjectionStale);
        result.Lease.ShouldBeNull();
        result.LockState.ShouldBe("denied_safe");
        result.Freshness.Stale.ShouldBeTrue();
        result.Freshness.ReasonCode.ShouldBe("projection_stale");
    }

    private static async Task<WorkspaceLockStatusQueryResult> ExecuteAsync(WorkspaceLockStatusReadModelResult readModelResult)
    {
        InMemoryFolderTenantAccessProjectionStore tenantStore = new();
        await tenantStore.SaveAsync(
            FolderLifecycleStatusTestSupport.TenantProjection("tenant-a", "user-a"),
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        WorkspaceLockStatusQueryHandler handler = Handler(tenantStore, new CountingWorkspaceLockStatusReadModel(readModelResult));
        return await handler.HandleAsync(Query(), TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static WorkspaceLockStatusQueryHandler Handler(
        IFolderTenantAccessProjectionStore tenantStore,
        IWorkspaceLockStatusReadModel readModel)
    {
        LayeredFolderAuthorizationService authorization = new(
            new TenantAccessAuthorizer(tenantStore, new FixedUtcClock(Now), new TenantAccessOptions()),
            new RecordingFolderPermissionEvidenceProvider(FolderPermissionEvidenceResult.Allowed(FolderLifecycleStatusTestSupport.AuthorizationWatermark)),
            new RecordingEventStoreAuthorizationValidator(EventStoreAuthorizationValidationResult.Allowed("validator_watermark_v1")),
            new RecordingDaprPolicyEvidenceProvider(DaprPolicyEvidenceResult.Allowed("folders", "dapr_policy_v1")),
            new FixedUtcClock(Now));

        return new WorkspaceLockStatusQueryHandler(authorization, readModel, new FixedUtcClock(Now));
    }

    private static WorkspaceLockStatusQuery Query(
        string folderId = "folder-a",
        string workspaceId = "workspace-a",
        string? tenantId = "tenant-a",
        string? principalId = "user-a",
        string? taskId = "task-a",
        string? correlationId = "corr-a",
        EventStoreClaimTransformEvidence? claimTransformEvidence = null)
        => new(
            folderId,
            workspaceId,
            tenantId,
            principalId,
            claimTransformEvidence ?? EventStoreClaimTransformEvidence.Allowed(tenantId, principalId, [WorkspaceLockStatusQueryHandler.ActionToken]),
            correlationId,
            taskId,
            ClientControlledTenantValues: null,
            ClientControlledPrincipalValues: null);

    private static WorkspaceLockStatusReadModelSnapshot ActiveSnapshot(DateTimeOffset? expiresAt = null)
        => new(
            ManagedTenantId: "tenant-a",
            FolderId: "folder-a",
            WorkspaceId: "workspace-a",
            WorkspaceState: "locked",
            LockState: "locked",
            LockId: "workspace_lock_a",
            HolderTaskId: "task-a",
            AcquiredAt: Now.AddMinutes(-5),
            EffectiveAt: Now.AddMinutes(-5),
            ExpiresAt: expiresAt ?? Now.AddMinutes(55),
            RetryEligibilityBasis: "lease_until_expiry",
            CorrelationId: "corr-a",
            TaskId: "task-a",
            Freshness: Freshness(),
            EvidenceScope: EvidenceScope());

    private static WorkspaceLockStatusReadModelSnapshot UnlockedSnapshot()
        => ActiveSnapshot() with
        {
            WorkspaceState = "ready",
            LockState = "unlocked",
            LockId = null,
            HolderTaskId = null,
            AcquiredAt = null,
            EffectiveAt = null,
            ExpiresAt = null,
        };

    private static FolderLifecycleFreshness Freshness()
        => new("read_your_writes", Now, "lock-watermark-a", Stale: false, ReasonCode: null);

    private static FolderLifecycleEvidenceScope EvidenceScope()
        => new(
            "tenant-a",
            "user-a",
            WorkspaceLockStatusQueryHandler.ActionToken,
            "task-a",
            "corr-a",
            FolderLifecycleStatusTestSupport.AuthorizationWatermark);
}

internal sealed class CountingWorkspaceLockStatusReadModel(WorkspaceLockStatusReadModelResult result)
    : IWorkspaceLockStatusReadModel
{
    public int Requests { get; private set; }

    public Task<WorkspaceLockStatusReadModelResult> GetAsync(
        WorkspaceLockStatusReadModelRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests++;
        return Task.FromResult(result);
    }
}
