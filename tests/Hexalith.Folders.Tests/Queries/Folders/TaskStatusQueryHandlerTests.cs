using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.Folders;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Queries.Folders;

public sealed class TaskStatusQueryHandlerTests
{
    private static readonly DateTimeOffset Now = FolderLifecycleStatusTestSupport.Now;

    [Theory]
    [InlineData("failed", "failed", "failed_operation", true, "failed_operation")]
    [InlineData("unknown_provider_outcome", "unknown_provider_outcome", "unknown_provider_outcome", false, "unknown_provider_outcome")]
    [InlineData("reconciliation_required", "reconciliation_required", "reconciliation_required", false, "reconciliation_required")]
    public async Task SuccessfulSnapshotsShouldReturnMetadataOnlyTaskEvidence(
        string currentState,
        string terminalState,
        string lastFailureCategory,
        bool retryEligible,
        string retryReason)
    {
        TaskStatusQueryResult result = await ExecuteAsync(TaskStatusReadModelResult.Available(
            Snapshot(currentState, terminalState, lastFailureCategory, retryEligible, retryReason)));

        result.Code.ShouldBe(TaskStatusQueryResultCode.Allowed);
        result.TaskId.ShouldBe("task-a");
        result.CurrentState.ShouldBe(currentState);
        result.TerminalState.ShouldBe(terminalState);
        result.LastOperationId.ShouldBe("workspace_status_operation");
        result.LastFailureCategory.ShouldBe(lastFailureCategory);
        result.RetryEligibility.Eligible.ShouldBe(retryEligible);
        result.RetryEligibility.ReasonCode.ShouldBe(retryReason);
        result.Freshness.ReadConsistency.ShouldBe("eventually_consistent");
        result.CorrelationId.ShouldBe("corr-a");
    }

    [Fact]
    public async Task AuthenticationFailureShouldNotTouchTenantProjectionOrReadModel()
    {
        CountingTenantAccessProjectionStore tenantStore = new();
        CountingTaskStatusReadModel readModel = new(TaskStatusReadModelResult.Available(Snapshot()));
        TaskStatusQueryHandler handler = Handler(tenantStore, readModel);

        TaskStatusQueryResult result = await handler.HandleAsync(
            Query(tenantId: null, principalId: null, claimTransformEvidence: EventStoreClaimTransformEvidence.Missing()),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBe(TaskStatusQueryResultCode.AuthenticationRequired);
        result.TaskId.ShouldBeNull();
        tenantStore.Gets.ShouldBe(0);
        readModel.Requests.ShouldBe(0);
    }

    [Fact]
    public async Task ClientTenantMismatchShouldRejectBeforeTenantProjectionLookup()
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-a"]));
        CountingTaskStatusReadModel readModel = new(TaskStatusReadModelResult.Available(Snapshot()));
        TaskStatusQueryHandler handler = Handler(tenantStore, readModel);

        TaskStatusQueryResult result = await handler.HandleAsync(
            Query(clientTenantValues: new Dictionary<string, string?>
            {
                ["header_hexalith_tenant_id"] = "tenant-b",
            }),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBe(TaskStatusQueryResultCode.AuthorizationDenied);
        result.TaskId.ShouldBeNull();
        tenantStore.Gets.ShouldBe(0);
        readModel.Requests.ShouldBe(0);
    }

    [Fact]
    public async Task ClaimTransformDenialShouldNotTouchTenantProjectionOrReadModel()
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-a"]));
        CountingTaskStatusReadModel readModel = new(TaskStatusReadModelResult.Available(Snapshot()));
        TaskStatusQueryHandler handler = Handler(tenantStore, readModel);

        TaskStatusQueryResult result = await handler.HandleAsync(
            Query(claimTransformEvidence: EventStoreClaimTransformEvidence.Allowed("tenant-a", "user-a", ["read_workspace_status"])),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBe(TaskStatusQueryResultCode.AuthorizationDenied);
        tenantStore.Gets.ShouldBe(0);
        readModel.Requests.ShouldBe(0);
    }

    [Fact]
    public async Task TenantDenialShouldNotTouchReadModel()
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-b"]));
        CountingTaskStatusReadModel readModel = new(TaskStatusReadModelResult.Available(Snapshot()));
        TaskStatusQueryHandler handler = Handler(tenantStore, readModel);

        TaskStatusQueryResult result = await handler.HandleAsync(
            Query(),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBe(TaskStatusQueryResultCode.NotFoundSafe);
        result.TaskId.ShouldBeNull();
        tenantStore.Gets.ShouldBe(1);
        readModel.Requests.ShouldBe(0);
    }

    [Theory]
    [InlineData("bad task", "corr-a")]
    [InlineData("task-a", "bad correlation")]
    public async Task MalformedIdentifiersShouldNotTouchTenantProjectionOrReadModel(
        string taskId,
        string correlationId)
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-a"]));
        CountingTaskStatusReadModel readModel = new(TaskStatusReadModelResult.Available(Snapshot()));
        TaskStatusQueryHandler handler = Handler(tenantStore, readModel);

        TaskStatusQueryResult result = await handler.HandleAsync(
            Query(taskId: taskId, correlationId: correlationId),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBe(TaskStatusQueryResultCode.NotFoundSafe);
        result.TaskId.ShouldBeNull();
        tenantStore.Gets.ShouldBe(0);
        readModel.Requests.ShouldBe(0);
    }

    [Theory]
    [InlineData(nameof(TaskStatusReadModelStatus.Stale), TaskStatusQueryResultCode.ProjectionStale, "projection_stale")]
    [InlineData(nameof(TaskStatusReadModelStatus.Unavailable), TaskStatusQueryResultCode.ProjectionUnavailable, "projection_unavailable")]
    [InlineData(nameof(TaskStatusReadModelStatus.Malformed), TaskStatusQueryResultCode.ReadModelUnavailable, "projection_malformed")]
    [InlineData(nameof(TaskStatusReadModelStatus.NotFound), TaskStatusQueryResultCode.NotFoundSafe, null)]
    public async Task ReadModelOutcomesShouldReturnSafeResults(
        string status,
        TaskStatusQueryResultCode expectedCode,
        string? expectedReasonCode)
    {
        TaskStatusQueryResult result = await ExecuteAsync(ReadModelOutcome(status));

        result.Code.ShouldBe(expectedCode);
        result.TaskId.ShouldBeNull();
        result.CurrentState.ShouldBe("denied_safe");
        if (expectedReasonCode is not null)
        {
            result.Freshness.ReasonCode.ShouldBe(expectedReasonCode);
        }
    }

    [Theory]
    [InlineData("tenant-b", "task-a", "snapshot_scope_mismatch")]
    [InlineData("tenant-a", "task-b", "snapshot_scope_mismatch")]
    [InlineData("tenant-a", "task-a", "snapshot_stale")]
    public async Task MalformedAvailableSnapshotsShouldFailClosed(
        string tenantId,
        string taskId,
        string expectedReason)
    {
        TaskStatusReadModelSnapshot snapshot = Snapshot() with
        {
            ManagedTenantId = tenantId,
            TaskId = taskId,
            Freshness = expectedReason == "snapshot_stale"
                ? FolderLifecycleStatusTestSupport.Freshness(stale: true, reasonCode: expectedReason)
                : FolderLifecycleStatusTestSupport.Freshness(),
        };

        TaskStatusQueryResult result = await ExecuteAsync(TaskStatusReadModelResult.Available(snapshot));

        result.Code.ShouldBe(TaskStatusQueryResultCode.ReadModelUnavailable);
        result.TaskId.ShouldBeNull();
        result.CurrentState.ShouldBe("denied_safe");
        result.Freshness.Stale.ShouldBeTrue();
        result.Freshness.ReasonCode.ShouldBe(expectedReason);
    }

    [Theory]
    [InlineData("invalid-lifecycle", "lifecycle_state_invalid")]
    [InlineData("invalid-operation", "operation_identifier_invalid")]
    [InlineData("invalid-failure-category", "canonical_category_invalid")]
    [InlineData("invalid-action", "snapshot_scope_mismatch")]
    [InlineData("invalid-retry-after", "projection_malformed")]
    public async Task MalformedContractSnapshotsShouldFailClosed(
        string malformedField,
        string expectedReason)
    {
        TaskStatusQueryResult result = await ExecuteAsync(TaskStatusReadModelResult.Available(MalformedSnapshot(malformedField)));

        result.Code.ShouldBe(TaskStatusQueryResultCode.ReadModelUnavailable);
        result.TaskId.ShouldBeNull();
        result.CurrentState.ShouldBe("denied_safe");
        result.Freshness.Stale.ShouldBeTrue();
        result.Freshness.ReasonCode.ShouldBe(expectedReason);
    }

    [Fact]
    public async Task ReadModelExceptionsShouldFailClosed()
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-a"]));
        ThrowingTaskStatusReadModel readModel = new();
        TaskStatusQueryHandler handler = Handler(tenantStore, readModel);

        TaskStatusQueryResult result = await handler.HandleAsync(
            Query(),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBe(TaskStatusQueryResultCode.ReadModelUnavailable);
        result.TaskId.ShouldBeNull();
        result.Freshness.ReasonCode.ShouldBe("read_model_unavailable");
        readModel.Requests.ShouldBe(1);
    }

    private static async Task<TaskStatusQueryResult> ExecuteAsync(TaskStatusReadModelResult readModelResult)
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-a"]));
        TaskStatusQueryHandler handler = Handler(tenantStore, new CountingTaskStatusReadModel(readModelResult));

        return await handler.HandleAsync(Query(), TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static TaskStatusQueryHandler Handler(
        IFolderTenantAccessProjectionStore tenantStore,
        ITaskStatusReadModel readModel)
        => new(
            new TenantAccessAuthorizer(tenantStore, new FixedUtcClock(Now), new TenantAccessOptions()),
            readModel,
            new FixedUtcClock(Now));

    private static TaskStatusQuery Query(
        string taskId = "task-a",
        string? tenantId = "tenant-a",
        string? principalId = "user-a",
        string? correlationId = "corr-a",
        IReadOnlyDictionary<string, string?>? clientTenantValues = null,
        EventStoreClaimTransformEvidence? claimTransformEvidence = null)
        => new(
            taskId,
            tenantId,
            principalId,
            claimTransformEvidence ?? EventStoreClaimTransformEvidence.Allowed(tenantId, principalId, [TaskStatusQueryHandler.ActionToken]),
            correlationId,
            clientTenantValues ?? new Dictionary<string, string?>(StringComparer.Ordinal));

    private static TaskStatusReadModelSnapshot Snapshot(
        string currentState = "failed",
        string? terminalState = "failed",
        string? lastFailureCategory = "failed_operation",
        bool retryEligible = true,
        string retryReason = "failed_operation")
        => new(
            ManagedTenantId: "tenant-a",
            TaskId: "task-a",
            CurrentState: currentState,
            TerminalState: terminalState,
            LastOperationId: "workspace_status_operation",
            LastFailureCategory: lastFailureCategory,
            RetryEligibility: new WorkspaceStatusRetryEligibility(retryEligible, retryReason),
            RetryAfter: null,
            Freshness: FolderLifecycleStatusTestSupport.Freshness(),
            EvidenceScope: FolderLifecycleStatusTestSupport.EvidenceScope(actionToken: TaskStatusQueryHandler.ActionToken));

    private static TaskStatusReadModelSnapshot MalformedSnapshot(string malformedField)
    {
        TaskStatusReadModelSnapshot snapshot = Snapshot();
        return malformedField switch
        {
            "invalid-lifecycle" => snapshot with
            {
                CurrentState = "refs/heads/main",
            },
            "invalid-operation" => snapshot with
            {
                LastOperationId = "https://provider.example.test/secret",
            },
            "invalid-failure-category" => snapshot with
            {
                LastFailureCategory = "refs/heads/main",
            },
            "invalid-action" => snapshot with
            {
                EvidenceScope = snapshot.EvidenceScope with { ActionToken = "read_workspace_status" },
            },
            "invalid-retry-after" => snapshot with
            {
                RetryAfter = new WorkspaceStatusRetryAfter(0),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(malformedField), malformedField, "Unknown malformed field."),
        };
    }

    private static TaskStatusReadModelResult ReadModelOutcome(string status)
        => status switch
        {
            nameof(TaskStatusReadModelStatus.Stale) => new TaskStatusReadModelResult(
                TaskStatusReadModelStatus.Stale,
                Snapshot: null,
                FolderLifecycleStatusTestSupport.Freshness(stale: true, reasonCode: "projection_stale")),
            nameof(TaskStatusReadModelStatus.Unavailable) => TaskStatusReadModelResult.Unavailable("projection_unavailable", Now),
            nameof(TaskStatusReadModelStatus.Malformed) => new TaskStatusReadModelResult(
                TaskStatusReadModelStatus.Malformed,
                Snapshot: null,
                FolderLifecycleStatusTestSupport.Freshness(stale: true, reasonCode: "projection_malformed")),
            nameof(TaskStatusReadModelStatus.NotFound) => TaskStatusReadModelResult.NotFound(FolderLifecycleStatusTestSupport.Freshness()),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown task status read-model outcome."),
        };

    private sealed class CountingTaskStatusReadModel(TaskStatusReadModelResult result) : ITaskStatusReadModel
    {
        public int Requests { get; private set; }

        public Task<TaskStatusReadModelResult> GetAsync(
            TaskStatusReadModelRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests++;
            return Task.FromResult(result);
        }
    }

    private sealed class ThrowingTaskStatusReadModel : ITaskStatusReadModel
    {
        public int Requests { get; private set; }

        public Task<TaskStatusReadModelResult> GetAsync(
            TaskStatusReadModelRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests++;
            throw new InvalidOperationException("read model unavailable");
        }
    }
}
