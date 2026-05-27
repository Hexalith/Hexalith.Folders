using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.Folders;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Queries.Folders;

public sealed class WorkspaceStatusQueryHandlerTests
{
    private static readonly DateTimeOffset Now = FolderLifecycleStatusTestSupport.Now;

    [Theory]
    [InlineData("committed", "completed", "known_success", false, "retry_not_required", null)]
    [InlineData("locked", "accepted", "known_success", false, "workspace_locked", null)]
    [InlineData("dirty", "accepted", "known_success", true, "dirty_workspace", null)]
    [InlineData("changes_staged", "accepted", "known_success", false, "retry_not_required", null)]
    [InlineData("failed", "failed", "known_failure", true, "failed_operation", "failed_operation")]
    [InlineData("inaccessible", "failed", "known_failure", false, "tenant_access_denied", "tenant_access_denied")]
    [InlineData("unknown_provider_outcome", "accepted", "unknown_provider_outcome", false, "unknown_provider_outcome", "unknown_provider_outcome")]
    [InlineData("reconciliation_required", "accepted", "reconciliation_required", false, "reconciliation_required", "reconciliation_required")]
    public async Task SuccessfulSnapshotsShouldReturnContractShapedMetadataOnlyStatus(
        string state,
        string acceptedState,
        string providerState,
        bool retryEligible,
        string retryReason,
        string? lastFailureCategory)
    {
        WorkspaceStatusQueryResult result = await ExecuteAsync(WorkspaceStatusReadModelResult.Available(Snapshot(state)));

        result.Code.ShouldBe(WorkspaceStatusQueryResultCode.Allowed);
        result.FolderId.ShouldBe("folder-a");
        result.WorkspaceId.ShouldBe("workspace-a");
        result.CurrentState.ShouldBe(state);
        result.AcceptedCommandState.ShouldNotBeNull();
        result.AcceptedCommandState.State.ShouldBe(acceptedState);
        result.AcceptedCommandState.TaskId.ShouldBe("task-a");
        result.ProjectedState.ShouldNotBeNull();
        result.ProjectedState.State.ShouldBe(state);
        result.ProjectedState.StateSource.ShouldBe("projection");
        result.ProviderOutcome.ShouldNotBeNull();
        result.ProviderOutcome.State.ShouldBe(providerState);
        result.ProviderOutcome.ProviderCorrelationReference.ShouldBe("provref_workspace_status");
        result.ProviderOutcome.RetryEligibility.Eligible.ShouldBe(retryEligible);
        result.RetryEligibility.ReasonCode.ShouldBe(retryReason);
        result.LastFailureCategory.ShouldBe(lastFailureCategory);
        result.Freshness.ReadConsistency.ShouldBe("read_your_writes");
        result.ProjectionLag.StateSource.ShouldBe("projection");
    }

    [Fact]
    public async Task AuthenticationFailureShouldNotTouchReadModel()
    {
        CountingWorkspaceStatusReadModel readModel = new(WorkspaceStatusReadModelResult.Available(Snapshot("committed")));
        WorkspaceStatusQueryHandler handler = Handler(new CountingTenantAccessProjectionStore(), readModel);

        WorkspaceStatusQueryResult result = await handler.HandleAsync(
            Query(tenantId: null, principalId: null, claimTransformEvidence: EventStoreClaimTransformEvidence.Missing()),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBe(WorkspaceStatusQueryResultCode.AuthenticationRequired);
        readModel.Requests.ShouldBe(0);
        result.FolderId.ShouldBeNull();
        result.WorkspaceId.ShouldBeNull();
    }

    [Fact]
    public async Task AuthorizationMustCompleteBeforeReadModelAccess()
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-b"]));
        CountingWorkspaceStatusReadModel readModel = new(WorkspaceStatusReadModelResult.Available(Snapshot("committed")));
        WorkspaceStatusQueryHandler handler = Handler(tenantStore, readModel);

        WorkspaceStatusQueryResult result = await handler.HandleAsync(
            Query(),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBe(WorkspaceStatusQueryResultCode.AuthorizationDenied);
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
        CountingWorkspaceStatusReadModel readModel = new(WorkspaceStatusReadModelResult.Available(Snapshot("committed")));
        WorkspaceStatusQueryHandler handler = Handler(tenantStore, readModel);

        WorkspaceStatusQueryResult result = await handler.HandleAsync(
            Query(folderId: folderId, workspaceId: workspaceId, correlationId: correlationId, taskId: taskId),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBe(WorkspaceStatusQueryResultCode.NotFoundSafe);
        tenantStore.Gets.ShouldBe(0);
        readModel.Requests.ShouldBe(0);
        result.FolderId.ShouldBeNull();
        result.WorkspaceId.ShouldBeNull();
    }

    [Fact]
    public async Task ProjectionStaleShouldReturnExplicitSafeCategory()
    {
        WorkspaceStatusQueryResult result = await ExecuteAsync(new WorkspaceStatusReadModelResult(
            WorkspaceStatusReadModelStatus.Stale,
            Snapshot: null,
            Freshness: Freshness(stale: true, reasonCode: "projection_stale")));

        result.Code.ShouldBe(WorkspaceStatusQueryResultCode.ProjectionStale);
        result.CurrentState.ShouldBe("denied_safe");
        result.Freshness.Stale.ShouldBeTrue();
        result.Freshness.ReasonCode.ShouldBe("projection_stale");
    }

    [Fact]
    public async Task ProjectionUnavailableShouldReturnExplicitSafeCategory()
    {
        WorkspaceStatusQueryResult result = await ExecuteAsync(
            WorkspaceStatusReadModelResult.Unavailable("projection_unavailable", Now));

        result.Code.ShouldBe(WorkspaceStatusQueryResultCode.ProjectionUnavailable);
        result.CurrentState.ShouldBe("denied_safe");
        result.ProviderOutcome.ShouldBeNull();
    }

    [Fact]
    public async Task MalformedProjectionShouldReturnReadModelUnavailable()
    {
        WorkspaceStatusQueryResult result = await ExecuteAsync(new WorkspaceStatusReadModelResult(
            WorkspaceStatusReadModelStatus.Malformed,
            Snapshot: null,
            Freshness: Freshness(stale: true, reasonCode: "projection_malformed")));

        result.Code.ShouldBe(WorkspaceStatusQueryResultCode.ReadModelUnavailable);
        result.Freshness.ReasonCode.ShouldBe("projection_malformed");
        result.ProjectedState.ShouldBeNull();
    }

    [Theory]
    [InlineData("invalid-lifecycle", "lifecycle_state_invalid")]
    [InlineData("null-projected-state", "projection_malformed")]
    [InlineData("invalid-projected-source", "projected_state_source_invalid")]
    [InlineData("unsafe-provider-reference", "provider_correlation_reference_invalid")]
    [InlineData("invalid-retry-after", "retry_after_invalid")]
    [InlineData("freshness-mismatch", "freshness_read_consistency_mismatch")]
    [InlineData("null-freshness", "projection_malformed")]
    [InlineData("invalid-last-failure", "canonical_category_invalid")]
    public async Task AvailableSnapshotsWithMalformedContractMetadataShouldFailClosed(
        string malformedField,
        string expectedReason)
    {
        WorkspaceStatusQueryResult result = await ExecuteAsync(
            WorkspaceStatusReadModelResult.Available(MalformedSnapshot(malformedField)));

        result.Code.ShouldBe(WorkspaceStatusQueryResultCode.ReadModelUnavailable);
        result.CurrentState.ShouldBe("denied_safe");
        result.FolderId.ShouldBeNull();
        result.WorkspaceId.ShouldBeNull();
        result.Freshness.Stale.ShouldBeTrue();
        result.Freshness.ReasonCode.ShouldBe(expectedReason);
        result.ProviderOutcome.ShouldBeNull();
    }

    [Theory]
    [InlineData("tenant-b", "folder-a", "workspace-a", "snapshot_scope_mismatch")]
    [InlineData("tenant-a", "folder-b", "workspace-a", "snapshot_scope_mismatch")]
    [InlineData("tenant-a", "folder-a", "workspace-b", "snapshot_scope_mismatch")]
    public async Task ScopeMismatchesShouldFailClosed(
        string tenantId,
        string folderId,
        string workspaceId,
        string reasonCode)
    {
        WorkspaceStatusReadModelSnapshot snapshot = Snapshot("committed") with
        {
            ManagedTenantId = tenantId,
            FolderId = folderId,
            WorkspaceId = workspaceId,
        };

        WorkspaceStatusQueryResult result = await ExecuteAsync(WorkspaceStatusReadModelResult.Available(snapshot));

        result.Code.ShouldBe(WorkspaceStatusQueryResultCode.ReadModelUnavailable);
        result.Freshness.Stale.ShouldBeTrue();
        result.Freshness.ReasonCode.ShouldBe(reasonCode);
        result.FolderId.ShouldBeNull();
        result.WorkspaceId.ShouldBeNull();
    }

    [Theory]
    [InlineData("read_workspace_lock", "action_mismatch")]
    [InlineData("read_workspace_status", "task_mismatch")]
    public async Task EvidenceScopeMismatchesShouldFailClosed(string actionToken, string expectedReason)
    {
        FolderLifecycleEvidenceScope scope = FolderLifecycleStatusTestSupport.EvidenceScope(
            actionToken: actionToken,
            taskId: actionToken == WorkspaceStatusQueryHandler.ActionToken ? "task-b" : "task-a");
        WorkspaceStatusReadModelSnapshot snapshot = Snapshot("committed") with
        {
            EvidenceScope = scope,
        };

        WorkspaceStatusQueryResult result = await ExecuteAsync(WorkspaceStatusReadModelResult.Available(snapshot));

        result.Code.ShouldBe(WorkspaceStatusQueryResultCode.ReadModelUnavailable);
        result.Freshness.ReasonCode.ShouldBe(expectedReason);
    }

    [Theory]
    [InlineData("corr-b", "auth_folder_watermark_v0011", "correlation_mismatch")]
    [InlineData("corr-a", "auth_folder_watermark_v9999", "incompatible_authorization_watermark")]
    public async Task CorrelationAndAuthorizationWatermarkMismatchesShouldFailClosed(
        string correlationId,
        string authorizationWatermark,
        string expectedReason)
    {
        WorkspaceStatusReadModelSnapshot snapshot = Snapshot("committed") with
        {
            EvidenceScope = FolderLifecycleStatusTestSupport.EvidenceScope(
                actionToken: WorkspaceStatusQueryHandler.ActionToken,
                correlationId: correlationId,
                authorizationWatermark: authorizationWatermark),
        };

        WorkspaceStatusQueryResult result = await ExecuteAsync(WorkspaceStatusReadModelResult.Available(snapshot));

        result.Code.ShouldBe(WorkspaceStatusQueryResultCode.ReadModelUnavailable);
        result.Freshness.ReasonCode.ShouldBe(expectedReason);
    }

    [Fact]
    public async Task FutureObservedTimeShouldFailClosed()
    {
        WorkspaceStatusReadModelSnapshot snapshot = Snapshot("committed") with
        {
            Freshness = Freshness(observedAt: Now.AddMinutes(1)),
        };

        WorkspaceStatusQueryResult result = await ExecuteAsync(WorkspaceStatusReadModelResult.Available(snapshot));

        result.Code.ShouldBe(WorkspaceStatusQueryResultCode.ReadModelUnavailable);
        result.Freshness.ReasonCode.ShouldBe("freshness_observed_in_future");
    }

    private static async Task<WorkspaceStatusQueryResult> ExecuteAsync(WorkspaceStatusReadModelResult readModelResult)
    {
        InMemoryFolderTenantAccessProjectionStore tenantStore = new();
        await tenantStore.SaveAsync(
            FolderLifecycleStatusTestSupport.TenantProjection("tenant-a", "user-a"),
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        WorkspaceStatusQueryHandler handler = Handler(tenantStore, new CountingWorkspaceStatusReadModel(readModelResult));
        return await handler.HandleAsync(Query(), TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static WorkspaceStatusQueryHandler Handler(
        IFolderTenantAccessProjectionStore tenantStore,
        IWorkspaceStatusReadModel readModel)
    {
        LayeredFolderAuthorizationService authorization = new(
            new TenantAccessAuthorizer(tenantStore, new FixedUtcClock(Now), new TenantAccessOptions()),
            new RecordingFolderPermissionEvidenceProvider(FolderPermissionEvidenceResult.Allowed(FolderLifecycleStatusTestSupport.AuthorizationWatermark)),
            new RecordingEventStoreAuthorizationValidator(EventStoreAuthorizationValidationResult.Allowed("validator_watermark_v1")),
            new RecordingDaprPolicyEvidenceProvider(DaprPolicyEvidenceResult.Allowed("folders", "dapr_policy_v1")),
            new FixedUtcClock(Now));

        return new WorkspaceStatusQueryHandler(authorization, readModel, new FixedUtcClock(Now));
    }

    private static WorkspaceStatusQuery Query(
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
            claimTransformEvidence ?? EventStoreClaimTransformEvidence.Allowed(tenantId, principalId, [WorkspaceStatusQueryHandler.ActionToken]),
            correlationId,
            taskId,
            ClientControlledTenantValues: null,
            ClientControlledPrincipalValues: null);

    private static WorkspaceStatusReadModelSnapshot Snapshot(string state)
    {
        WorkspaceStatusRetryEligibility retryEligibility = state switch
        {
            "dirty" => new(true, "dirty_workspace"),
            "failed" => new(true, "failed_operation"),
            "unknown_provider_outcome" => new(false, "unknown_provider_outcome"),
            "reconciliation_required" => new(false, "reconciliation_required"),
            "locked" => new(false, "workspace_locked"),
            "inaccessible" => new(false, "tenant_access_denied"),
            _ => new(false, "retry_not_required"),
        };
        string providerState = state switch
        {
            "failed" or "inaccessible" => "known_failure",
            "unknown_provider_outcome" => "unknown_provider_outcome",
            "reconciliation_required" => "reconciliation_required",
            _ => "known_success",
        };
        string? failureCategory = state switch
        {
            "failed" => "failed_operation",
            "inaccessible" => "tenant_access_denied",
            "unknown_provider_outcome" => "unknown_provider_outcome",
            "reconciliation_required" => "reconciliation_required",
            _ => null,
        };

        return new(
            ManagedTenantId: "tenant-a",
            FolderId: "folder-a",
            WorkspaceId: "workspace-a",
            CurrentState: state,
            AcceptedCommandState: new WorkspaceAcceptedCommandState(
                "task-a",
                "workspace_status_operation",
                state == "committed" ? "completed" : state is "failed" or "inaccessible" ? "failed" : "accepted",
                Now),
            ProjectedState: new WorkspaceProjectedState(state, "projection", Now),
            ProviderOutcome: new WorkspaceProviderOutcome(
                "workspace_status_operation",
                providerState,
                failureCategory ?? "success",
                "provref_workspace_status",
                retryEligibility,
                RetryAfter: null,
                Freshness: Freshness(),
                ChangedPathMetadataDigest: "digest_workspace_status",
                CommitReferenceClassification: state == "committed" ? "opaque_reference" : null,
                ReconciliationReference: state is "unknown_provider_outcome" or "reconciliation_required" ? "reconciliation-a" : null),
            RetryEligibility: retryEligibility,
            RetryAfter: null,
            Freshness: Freshness(),
            ProjectionLag: new WorkspaceProjectionLag(0, "projection"),
            LastFailureCategory: failureCategory,
            EvidenceScope: FolderLifecycleStatusTestSupport.EvidenceScope(actionToken: WorkspaceStatusQueryHandler.ActionToken));
    }

    private static WorkspaceStatusReadModelSnapshot MalformedSnapshot(string malformedField)
    {
        WorkspaceStatusReadModelSnapshot snapshot = Snapshot("committed");
        return malformedField switch
        {
            "invalid-lifecycle" => snapshot with
            {
                CurrentState = "refs/heads/main",
            },
            "null-projected-state" => snapshot with
            {
                ProjectedState = null!,
            },
            "invalid-projected-source" => snapshot with
            {
                ProjectedState = snapshot.ProjectedState with { StateSource = "raw_provider_payload" },
            },
            "unsafe-provider-reference" => snapshot with
            {
                ProviderOutcome = snapshot.ProviderOutcome with
                {
                    ProviderCorrelationReference = "https://provider.example.test/secret",
                },
            },
            "invalid-retry-after" => snapshot with
            {
                RetryAfter = new WorkspaceStatusRetryAfter(0),
            },
            "freshness-mismatch" => snapshot with
            {
                Freshness = new FolderLifecycleFreshness(
                    "eventually_consistent",
                    Now,
                    "workspace_status_watermark_v1",
                    Stale: false,
                    ReasonCode: null),
            },
            "null-freshness" => snapshot with
            {
                Freshness = null!,
            },
            "invalid-last-failure" => snapshot with
            {
                LastFailureCategory = "refs/heads/main",
            },
            _ => throw new ArgumentOutOfRangeException(nameof(malformedField), malformedField, "Unknown malformed field."),
        };
    }

    private static FolderLifecycleFreshness Freshness(
        bool stale = false,
        string? reasonCode = null,
        DateTimeOffset? observedAt = null)
        => new("read_your_writes", observedAt ?? Now, "workspace_status_watermark_v1", stale, reasonCode);
}

internal sealed class CountingWorkspaceStatusReadModel(WorkspaceStatusReadModelResult result)
    : IWorkspaceStatusReadModel
{
    public int Requests { get; private set; }

    public Task<WorkspaceStatusReadModelResult> GetAsync(
        WorkspaceStatusReadModelRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests++;
        return Task.FromResult(result);
    }
}
