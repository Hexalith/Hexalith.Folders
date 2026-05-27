using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.Folders;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Queries.Folders;

public sealed class WorkspaceCleanupStatusQueryHandlerTests
{
    private static readonly DateTimeOffset Now = FolderLifecycleStatusTestSupport.Now;

    [Theory]
    [InlineData("pending", "workspace_lifecycle_in_progress", false)]
    [InlineData("succeeded", "workspace_committed", false)]
    [InlineData("failed", "failed_operation", true)]
    [InlineData("status_only", "dirty_workspace", true)]
    public async Task SuccessfulSnapshotsShouldReturnMetadataOnlyCleanupStatus(
        string status,
        string reasonCode,
        bool retryEligible)
    {
        WorkspaceCleanupStatusQueryResult result = await ExecuteAsync(
            WorkspaceCleanupStatusReadModelResult.Available(Snapshot(status, reasonCode, retryEligible)));

        result.Code.ShouldBe(WorkspaceCleanupStatusQueryResultCode.Allowed);
        result.FolderId.ShouldBe("folder-a");
        result.WorkspaceId.ShouldBe("workspace-a");
        result.TaskId.ShouldBe("task-a");
        result.Status.ShouldBe(status);
        result.ReasonCode.ShouldBe(reasonCode);
        result.RetryEligibility.Eligible.ShouldBe(retryEligible);
        result.RetryEligibility.AdvisoryOnly.ShouldBeTrue();
        result.Freshness.ReadConsistency.ShouldBe("read_your_writes");
        result.CorrelationId.ShouldBe("corr-a");
        result.ObservedAt.ShouldBe(Now);
        result.LastAttemptedAt.ShouldBe(Now);
    }

    [Fact]
    public async Task AuthenticationFailureShouldNotTouchReadModel()
    {
        CountingWorkspaceCleanupStatusReadModel readModel = new(
            WorkspaceCleanupStatusReadModelResult.Available(Snapshot()));
        WorkspaceCleanupStatusQueryHandler handler = Handler(new CountingTenantAccessProjectionStore(), readModel);

        WorkspaceCleanupStatusQueryResult result = await handler.HandleAsync(
            Query(tenantId: null, principalId: null, claimTransformEvidence: EventStoreClaimTransformEvidence.Missing()),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBe(WorkspaceCleanupStatusQueryResultCode.AuthenticationRequired);
        readModel.Requests.ShouldBe(0);
        result.FolderId.ShouldBeNull();
        result.WorkspaceId.ShouldBeNull();
    }

    [Fact]
    public async Task AuthorizationMustCompleteBeforeReadModelAccess()
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-b"]));
        CountingWorkspaceCleanupStatusReadModel readModel = new(
            WorkspaceCleanupStatusReadModelResult.Available(Snapshot()));
        WorkspaceCleanupStatusQueryHandler handler = Handler(tenantStore, readModel);

        WorkspaceCleanupStatusQueryResult result = await handler.HandleAsync(
            Query(),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBe(WorkspaceCleanupStatusQueryResultCode.AuthorizationDenied);
        tenantStore.Gets.ShouldBe(1);
        readModel.Requests.ShouldBe(0);
    }

    [Theory]
    [InlineData("folder a", "workspace-a", "corr-a", "task-a")]
    [InlineData("folder-a", "workspace a", "corr-a", "task-a")]
    [InlineData("folder-a", "workspace-a", "bad correlation", "task-a")]
    [InlineData("folder-a", "workspace-a", "corr-a", "bad task")]
    public async Task MalformedQueryIdentifiersShouldNotTouchAuthorizationOrReadModel(
        string folderId,
        string workspaceId,
        string correlationId,
        string taskId)
    {
        CountingTenantAccessProjectionStore tenantStore = new();
        CountingWorkspaceCleanupStatusReadModel readModel = new(
            WorkspaceCleanupStatusReadModelResult.Available(Snapshot()));
        WorkspaceCleanupStatusQueryHandler handler = Handler(tenantStore, readModel);

        WorkspaceCleanupStatusQueryResult result = await handler.HandleAsync(
            Query(folderId: folderId, workspaceId: workspaceId, correlationId: correlationId, taskId: taskId),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBe(WorkspaceCleanupStatusQueryResultCode.NotFoundSafe);
        tenantStore.Gets.ShouldBe(0);
        readModel.Requests.ShouldBe(0);
        result.FolderId.ShouldBeNull();
        result.WorkspaceId.ShouldBeNull();
    }

    [Theory]
    [InlineData("stale", WorkspaceCleanupStatusQueryResultCode.ProjectionStale, "projection_stale")]
    [InlineData("unavailable", WorkspaceCleanupStatusQueryResultCode.ProjectionUnavailable, "projection_unavailable")]
    [InlineData("malformed", WorkspaceCleanupStatusQueryResultCode.ReadModelUnavailable, "projection_malformed")]
    public async Task SafeReadModelOutcomesShouldFailClosed(
        string outcome,
        WorkspaceCleanupStatusQueryResultCode expectedCode,
        string expectedReasonCode)
    {
        WorkspaceCleanupStatusQueryResult result = await ExecuteAsync(outcome switch
        {
            "stale" => new WorkspaceCleanupStatusReadModelResult(
                WorkspaceCleanupStatusReadModelStatus.Stale,
                Snapshot: null,
                Freshness: Freshness(stale: true, reasonCode: "projection_stale")),
            "unavailable" => WorkspaceCleanupStatusReadModelResult.Unavailable("projection_unavailable", Now),
            "malformed" => new WorkspaceCleanupStatusReadModelResult(
                WorkspaceCleanupStatusReadModelStatus.Malformed,
                Snapshot: null,
                Freshness: Freshness(stale: true, reasonCode: "projection_malformed")),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown cleanup outcome."),
        });

        result.Code.ShouldBe(expectedCode);
        result.Status.ShouldBe("denied_safe");
        result.Freshness.ReasonCode.ShouldBe(expectedReasonCode);
        result.FolderId.ShouldBeNull();
        result.WorkspaceId.ShouldBeNull();
    }

    [Fact]
    public async Task MissingCleanupProjectionShouldFailClosedAsNotFound()
    {
        WorkspaceCleanupStatusQueryResult result = await ExecuteAsync(
            WorkspaceCleanupStatusReadModelResult.NotFound(Freshness(stale: true, reasonCode: "cleanup_status_projection_missing")));

        result.Code.ShouldBe(WorkspaceCleanupStatusQueryResultCode.NotFoundSafe);
        result.Status.ShouldBe("denied_safe");
        result.FolderId.ShouldBeNull();
        result.WorkspaceId.ShouldBeNull();
        result.Freshness.ReasonCode.ShouldBe("cleanup_status_projection_missing");
    }

    [Fact]
    public async Task InMemoryReadModelShouldIsolateTaskAndCorrelationScopedSnapshots()
    {
        InMemoryFolderTenantAccessProjectionStore tenantStore = new();
        await tenantStore.SaveAsync(
            FolderLifecycleStatusTestSupport.TenantProjection("tenant-a", "user-a"),
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        InMemoryWorkspaceCleanupStatusReadModel readModel = new(new FixedUtcClock(Now));
        readModel.Save(Snapshot(
            status: "status_only",
            reasonCode: "dirty_workspace",
            retryEligible: true,
            taskId: "task-a",
            correlationId: "corr-a"));
        readModel.Save(Snapshot(
            status: "failed",
            reasonCode: "failed_operation",
            retryEligible: true,
            taskId: "task-b",
            correlationId: "corr-b"));
        WorkspaceCleanupStatusQueryHandler handler = Handler(tenantStore, readModel);

        WorkspaceCleanupStatusQueryResult first = await handler.HandleAsync(
            Query(taskId: "task-a", correlationId: "corr-a"),
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        WorkspaceCleanupStatusQueryResult second = await handler.HandleAsync(
            Query(taskId: "task-b", correlationId: "corr-b"),
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        WorkspaceCleanupStatusQueryResult unscoped = await handler.HandleAsync(
            Query(taskId: null, correlationId: null),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        first.Code.ShouldBe(WorkspaceCleanupStatusQueryResultCode.Allowed);
        first.TaskId.ShouldBe("task-a");
        first.CorrelationId.ShouldBe("corr-a");
        first.Status.ShouldBe("status_only");
        second.Code.ShouldBe(WorkspaceCleanupStatusQueryResultCode.Allowed);
        second.TaskId.ShouldBe("task-b");
        second.CorrelationId.ShouldBe("corr-b");
        second.Status.ShouldBe("failed");
        unscoped.Code.ShouldBe(WorkspaceCleanupStatusQueryResultCode.NotFoundSafe);
        unscoped.TaskId.ShouldBeNull();
    }

    [Theory]
    [InlineData("invalid-status", "cleanup_status_invalid")]
    [InlineData("invalid-reason-code", "cleanup_reason_invalid")]
    [InlineData("invalid-retry-reason", "retry_eligibility_invalid")]
    [InlineData("future-time", "freshness_observed_in_future")]
    [InlineData("future-last-attempted", "freshness_observed_in_future")]
    [InlineData("freshness-mismatch", "freshness_read_consistency_mismatch")]
    [InlineData("freshness-watermark", "freshness_watermark_invalid")]
    [InlineData("freshness-reason", "freshness_reason_invalid")]
    [InlineData("scope-identifier", "cleanup_scope_identifier_invalid")]
    [InlineData("tenant-scope", "snapshot_scope_mismatch")]
    [InlineData("folder-scope", "snapshot_scope_mismatch")]
    [InlineData("workspace-scope", "snapshot_scope_mismatch")]
    [InlineData("task-scope", "task_mismatch")]
    [InlineData("correlation-scope", "correlation_mismatch")]
    [InlineData("principal-scope", "principal_mismatch")]
    [InlineData("evidence-tenant", "evidence_tenant_mismatch")]
    [InlineData("action-scope", "action_mismatch")]
    [InlineData("authorization-watermark", "incompatible_authorization_watermark")]
    public async Task MalformedOrMismatchedSnapshotsShouldFailClosed(string malformedField, string expectedReason)
    {
        WorkspaceCleanupStatusReadModelSnapshot snapshot = MalformedSnapshot(malformedField);

        WorkspaceCleanupStatusQueryResult result = await ExecuteAsync(
            WorkspaceCleanupStatusReadModelResult.Available(snapshot));

        result.Code.ShouldBe(WorkspaceCleanupStatusQueryResultCode.ReadModelUnavailable);
        result.Status.ShouldBe("denied_safe");
        result.FolderId.ShouldBeNull();
        result.WorkspaceId.ShouldBeNull();
        result.Freshness.Stale.ShouldBeTrue();
        result.Freshness.ReasonCode.ShouldBe(expectedReason);
    }

    private static async Task<WorkspaceCleanupStatusQueryResult> ExecuteAsync(WorkspaceCleanupStatusReadModelResult readModelResult)
    {
        InMemoryFolderTenantAccessProjectionStore tenantStore = new();
        await tenantStore.SaveAsync(
            FolderLifecycleStatusTestSupport.TenantProjection("tenant-a", "user-a"),
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        WorkspaceCleanupStatusQueryHandler handler = Handler(tenantStore, new CountingWorkspaceCleanupStatusReadModel(readModelResult));
        return await handler.HandleAsync(Query(), TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static WorkspaceCleanupStatusQueryHandler Handler(
        IFolderTenantAccessProjectionStore tenantStore,
        IWorkspaceCleanupStatusReadModel readModel)
    {
        LayeredFolderAuthorizationService authorization = new(
            new TenantAccessAuthorizer(tenantStore, new FixedUtcClock(Now), new TenantAccessOptions()),
            new RecordingFolderPermissionEvidenceProvider(FolderPermissionEvidenceResult.Allowed(FolderLifecycleStatusTestSupport.AuthorizationWatermark)),
            new RecordingEventStoreAuthorizationValidator(EventStoreAuthorizationValidationResult.Allowed("validator_watermark_v1")),
            new RecordingDaprPolicyEvidenceProvider(DaprPolicyEvidenceResult.Allowed("folders", "dapr_policy_v1")),
            new FixedUtcClock(Now));

        return new WorkspaceCleanupStatusQueryHandler(authorization, readModel, new FixedUtcClock(Now));
    }

    private static WorkspaceCleanupStatusQuery Query(
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
            claimTransformEvidence ?? EventStoreClaimTransformEvidence.Allowed(tenantId, principalId, [WorkspaceCleanupStatusQueryHandler.ActionToken]),
            correlationId,
            taskId,
            ClientControlledTenantValues: null,
            ClientControlledPrincipalValues: null);

    private static WorkspaceCleanupStatusReadModelSnapshot Snapshot(
        string status = "status_only",
        string reasonCode = "dirty_workspace",
        bool retryEligible = true,
        string? taskId = "task-a",
        string? correlationId = "corr-a")
        => new(
            ManagedTenantId: "tenant-a",
            FolderId: "folder-a",
            WorkspaceId: "workspace-a",
            TaskId: taskId,
            Status: status,
            ReasonCode: reasonCode,
            RetryEligibility: new WorkspaceStatusRetryEligibility(retryEligible, reasonCode),
            Freshness: Freshness(),
            CorrelationId: correlationId,
            ObservedAt: Now,
            LastAttemptedAt: Now,
            EvidenceScope: FolderLifecycleStatusTestSupport.EvidenceScope(
                actionToken: WorkspaceCleanupStatusQueryHandler.ActionToken,
                taskId: taskId,
                correlationId: correlationId));

    private static WorkspaceCleanupStatusReadModelSnapshot MalformedSnapshot(string malformedField)
    {
        WorkspaceCleanupStatusReadModelSnapshot snapshot = Snapshot();
        return malformedField switch
        {
            "invalid-status" => snapshot with { Status = "cleaned" },
            "invalid-reason-code" => snapshot with { ReasonCode = "refs/heads/main" },
            "invalid-retry-reason" => snapshot with { RetryEligibility = new WorkspaceStatusRetryEligibility(true, "refs/heads/main") },
            "future-time" => snapshot with { ObservedAt = Now.AddMinutes(1) },
            "future-last-attempted" => snapshot with { LastAttemptedAt = Now.AddMinutes(1) },
            "freshness-mismatch" => snapshot with
            {
                Freshness = new FolderLifecycleFreshness("eventually_consistent", Now, "cleanup_status_watermark_v1", Stale: false, ReasonCode: null),
            },
            "freshness-watermark" => snapshot with
            {
                Freshness = new FolderLifecycleFreshness("read_your_writes", Now, "refs/heads/main", Stale: false, ReasonCode: null),
            },
            "freshness-reason" => snapshot with
            {
                Freshness = new FolderLifecycleFreshness("read_your_writes", Now, "cleanup_status_watermark_v1", Stale: false, ReasonCode: "refs/heads/main"),
            },
            "scope-identifier" => snapshot with { TaskId = "bad task" },
            "tenant-scope" => snapshot with { ManagedTenantId = "tenant-b" },
            "folder-scope" => snapshot with { FolderId = "folder-b" },
            "workspace-scope" => snapshot with { WorkspaceId = "workspace-b" },
            "task-scope" => snapshot with { TaskId = "task-b" },
            "correlation-scope" => snapshot with { CorrelationId = "corr-b" },
            "principal-scope" => snapshot with
            {
                EvidenceScope = FolderLifecycleStatusTestSupport.EvidenceScope(
                    principalId: "user-b",
                    actionToken: WorkspaceCleanupStatusQueryHandler.ActionToken),
            },
            "evidence-tenant" => snapshot with
            {
                EvidenceScope = FolderLifecycleStatusTestSupport.EvidenceScope(
                    tenantId: "tenant-b",
                    actionToken: WorkspaceCleanupStatusQueryHandler.ActionToken),
            },
            "action-scope" => snapshot with
            {
                EvidenceScope = FolderLifecycleStatusTestSupport.EvidenceScope(actionToken: "read_workspace_status"),
            },
            "authorization-watermark" => snapshot with
            {
                EvidenceScope = FolderLifecycleStatusTestSupport.EvidenceScope(
                    actionToken: WorkspaceCleanupStatusQueryHandler.ActionToken,
                    authorizationWatermark: "auth_folder_watermark_v9999"),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(malformedField), malformedField, "Unknown malformed field."),
        };
    }

    private static FolderLifecycleFreshness Freshness(
        bool stale = false,
        string? reasonCode = null)
        => new("read_your_writes", Now, "cleanup_status_watermark_v1", stale, reasonCode);
}

internal sealed class CountingWorkspaceCleanupStatusReadModel(WorkspaceCleanupStatusReadModelResult result)
    : IWorkspaceCleanupStatusReadModel
{
    public int Requests { get; private set; }

    public Task<WorkspaceCleanupStatusReadModelResult> GetAsync(
        WorkspaceCleanupStatusReadModelRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests++;
        return Task.FromResult(result);
    }
}
