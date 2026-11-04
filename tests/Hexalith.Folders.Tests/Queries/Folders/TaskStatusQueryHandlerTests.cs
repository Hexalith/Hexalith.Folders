using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.Folders;

using Microsoft.Extensions.Logging;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Queries.Folders;

// These tests follow the production ConfigureAwait(false) convention.
#pragma warning disable xUnit1030

public sealed class TaskStatusQueryHandlerTests
{
    private static readonly DateTimeOffset Now = FolderLifecycleStatusTestSupport.Now;

    [Theory]
    [InlineData("read_task_status", "read_metadata", true)]
    [InlineData("read_metadata", "read_metadata", false)]
    [InlineData("read_task_status", "read_workspace_status", false)]
    public async Task TaskInspectionRequiresItsOwnClaimAndExistingFolderReadGrant(
        string identityClaim, string folderGrant, bool allowed)
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-a"]));
        CountingTaskStatusReadModel taskModel = new(TaskStatusReadModelResult.Available(Snapshot()));
        InMemoryEffectivePermissionsReadModel permissions = new();
        permissions.Save(new EffectivePermissionsReadModelSnapshot(
            "tenant-a", "organization-a", "folder-a", EffectivePermissionsFolderLifecycleState.Active,
            [new EffectivePermissionEvidenceRow(EffectivePermissionEvidenceSource.FolderOverrideGrant,
                EffectivePermissionPrincipal.User("user-a"), folderGrant, 1, Now.AddSeconds(-1))],
            new EffectivePermissionsFreshness("read_your_writes", Now,
                FolderLifecycleStatusTestSupport.AuthorizationWatermark, Stale: false, ReasonCode: null),
            RevocationFreshnessEstablished: true, TaskScope: null));
        RecordingEventStoreAuthorizationValidator validator = new(EventStoreAuthorizationValidationResult.Allowed("validator_watermark_v1"));
        TaskStatusQueryHandler handler = new(
            new TenantAccessAuthorizer(tenantStore, new FixedUtcClock(Now), new TenantAccessOptions()),
            taskModel, new FixedUtcClock(Now), folderAuthorization: new LayeredFolderAuthorizationService(
                new TenantAccessAuthorizer(tenantStore, new FixedUtcClock(Now), new TenantAccessOptions()),
                new EffectivePermissionsFolderPermissionEvidenceProvider(permissions, new FixedUtcClock(Now)),
                validator, new RecordingDaprPolicyEvidenceProvider(DaprPolicyEvidenceResult.Allowed("folders", "dapr_policy_v1")),
                new FixedUtcClock(Now)));

        TaskStatusQueryResult result = await handler.HandleAsync(Query(claimTransformEvidence:
            EventStoreClaimTransformEvidence.Allowed("tenant-a", "user-a", [identityClaim])),
            TestContext.Current.CancellationToken).ConfigureAwait(false);

        (result.Code == TaskStatusQueryResultCode.Allowed).ShouldBe(allowed);
        taskModel.Requests.ShouldBe(allowed ? 1 : 0);
        if (allowed)
        {
            validator.Requests.ShouldHaveSingleItem().SafeContext.ActionToken.ShouldBe("read_task_status");
            result.TaskId.ShouldBe("task-a");
        }
    }

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
    public async Task MissingFolderScopeShouldFailBeforeProtectedTaskLookup()
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-a"]));
        CountingTaskStatusReadModel readModel = new(TaskStatusReadModelResult.Available(Snapshot()));
        TaskStatusQueryResult result = await Handler(tenantStore, readModel).HandleAsync(
            Query() with { FolderId = null }, TestContext.Current.CancellationToken);
        result.Code.ShouldBe(TaskStatusQueryResultCode.ReadModelUnavailable);
        readModel.Requests.ShouldBe(0);
    }

    /// <summary>Unsupported native absence cannot supply folder authority or expose a protected task.</summary>
    [Fact]
    public async Task UnsupportedNativeFolderAbsenceNeverBecomesZeroOrReachesTaskStorage()
    {
        var clock = new FixedUtcClock(Now);
        CountingTenantAccessProjectionStore tenant = new(FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-a"]));
        var metadata = new UnsupportedTaskScopeMetadataReader();
        var sequence = new GatewayOrganizationStreamSequenceReader(metadata);
        var store = new InMemoryReadModelStore();
        var organization = new EventStoreOrganizationEffectivePermissionsReadModel(store, clock, sequence);
        var permissions = new EventStoreFolderEffectivePermissionsReadModel(store, clock, sequence, organization);
        var unavailable = await permissions.GetAsync(new EffectivePermissionsReadModelRequest("tenant-a", "absent-folder",
            [EffectivePermissionPrincipal.User("user-a")], "task-a", null, "read_your_writes"), TestContext.Current.CancellationToken);
        unavailable.Status.ShouldBe(EffectivePermissionsReadModelStatus.Unavailable);
        unavailable.Freshness.ReasonCode.ShouldBe("folder_stream_unavailable");
        CountingTaskStatusReadModel tasks = new(TaskStatusReadModelResult.Available(Snapshot()));
        var validator = new RecordingEventStoreAuthorizationValidator(EventStoreAuthorizationValidationResult.Allowed("validator_watermark_v1"));
        var authorizer = new TenantAccessAuthorizer(tenant, clock, new TenantAccessOptions());
        var handler = new TaskStatusQueryHandler(authorizer, tasks, clock, folderAuthorization: new LayeredFolderAuthorizationService(
            authorizer, new EffectivePermissionsFolderPermissionEvidenceProvider(permissions, clock), validator,
            new RecordingDaprPolicyEvidenceProvider(DaprPolicyEvidenceResult.Allowed("folders", "dapr_policy_v1")), clock));

        TaskStatusQueryResult result = await handler.HandleAsync(Query() with { FolderId = "absent-folder" }, TestContext.Current.CancellationToken);

        result.Code.ShouldBe(TaskStatusQueryResultCode.ReadModelUnavailable);
        result.FolderAuthorizationDenial!.Decision.TerminalLayer.ShouldBe(AuthorizationLayer.FolderAcl);
        result.TaskId.ShouldBeNull();
        result.TerminalState.ShouldBeNull();
        tasks.Requests.ShouldBe(0);
        validator.Requests.ShouldBeEmpty();
        metadata.Requests.Count.ShouldBe(2);
        metadata.Requests.ShouldAllBe(identity => identity.TenantId == "tenant-a" && identity.Domain == "folders" && identity.AggregateId == "absent-folder");
    }

    [Fact]
    public async Task AuthenticationFailureShouldNotTouchTenantProjectionOrReadModel()
    {
        CountingTenantAccessProjectionStore tenantStore = new();
        CountingTaskStatusReadModel readModel = new(TaskStatusReadModelResult.Available(Snapshot()));
        TaskStatusQueryHandler handler = Handler(tenantStore, readModel);

        TaskStatusQueryResult result = await handler.HandleAsync(
            Query(tenantId: null, principalId: null, claimTransformEvidence: EventStoreClaimTransformEvidence.Missing()),
            TestContext.Current.CancellationToken).ConfigureAwait(false);

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
            TestContext.Current.CancellationToken).ConfigureAwait(false);

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
            TestContext.Current.CancellationToken).ConfigureAwait(false);

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
            TestContext.Current.CancellationToken).ConfigureAwait(false);

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
            TestContext.Current.CancellationToken).ConfigureAwait(false);

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
            TestContext.Current.CancellationToken).ConfigureAwait(false);

        result.Code.ShouldBe(TaskStatusQueryResultCode.ReadModelUnavailable);
        result.TaskId.ShouldBeNull();
        result.Freshness.ReasonCode.ShouldBe("read_model_unavailable");
        readModel.Requests.ShouldBe(1);
    }

    [Theory]
    [InlineData(299, TaskStatusQueryResultCode.Allowed, null)]
    [InlineData(300, TaskStatusQueryResultCode.Allowed, null)]
    [InlineData(301, TaskStatusQueryResultCode.ReadModelUnavailable, "tenant_projection_stale")]
    [InlineData(1801, TaskStatusQueryResultCode.ProjectionStale, null)]
    public async Task DiagnosticsShouldDistinguishInitialTenantAndNestedStrictFreshnessWithoutExtraReads(
        int ageSeconds, TaskStatusQueryResultCode expected, string? folderOutcome)
    {
        FolderTenantAccessProjection projection = FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-a"]);
        projection.LastEventTimestamp = Now.AddSeconds(-ageSeconds);
        CountingTenantAccessProjectionStore tenant = new(projection);
        CountingTaskStatusReadModel model = new(TaskStatusReadModelResult.Available(Snapshot()));
        TaskStatusDiagnosticLogger logger = new();

        TaskStatusQueryResult result = await Handler(tenant, model, logger).HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(expected);
        tenant.Gets.ShouldBe(ageSeconds > 1800 ? 1 : 2);
        model.Requests.ShouldBe(expected == TaskStatusQueryResultCode.Allowed ? 1 : 0);
        logger.Entries.Count.ShouldBe(2);
        logger.Entries[0]["Outcome"].ShouldBe(ageSeconds > 1800 ? TenantAccessOutcome.StaleProjection : TenantAccessOutcome.Allowed);
        logger.Entries[0]["Freshness"].ShouldBe(ageSeconds > 300 ? TenantProjectionFreshnessStatus.Stale : TenantProjectionFreshnessStatus.Fresh);
        logger.Entries[0]["ProjectionAgeSeconds"].ShouldBe(ageSeconds);
        logger.Entries[1]["ResultCode"].ShouldBe(expected);
        logger.Entries[1]["FolderDenialOutcome"].ShouldBe(folderOutcome ?? "none");
        if (folderOutcome is not null)
        {
            logger.Entries[1]["FolderDenialLayer"].ShouldBe(AuthorizationLayer.TenantAccessFreshness);
            logger.Entries[1]["FolderDenialFreshness"].ShouldBe("stale");
        }
        string fields = string.Join("|", logger.Entries.SelectMany(entry => entry.Values));
        foreach (string identity in new[] { "tenant-a", "user-a", "task-a", "folder-a", "corr-a", FolderLifecycleStatusTestSupport.TenantWatermark })
            fields.ShouldNotContain(identity);
        logger.ExceptionObjects.ShouldBe(0);
    }

    [Theory]
    [InlineData("disabled", false)]
    [InlineData("disabled", true)]
    [InlineData("throw_is_enabled", false)]
    [InlineData("throw_is_enabled", true)]
    [InlineData("throw_log", false)]
    [InlineData("throw_log", true)]
    public async Task OptionalDiagnosticProviderFailuresShouldPreserveResultsAndObservations(string mode, bool stale)
    {
        FolderTenantAccessProjection projection = FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-a"]);
        if (stale) projection.LastEventTimestamp = Now.AddSeconds(-301);
        CountingTenantAccessProjectionStore tenant = new(projection);
        CountingTaskStatusReadModel model = new(TaskStatusReadModelResult.Available(Snapshot()));
        TaskStatusDiagnosticLogger logger = new(mode);

        TaskStatusQueryResult result = await Handler(tenant, model, logger).HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(stale ? TaskStatusQueryResultCode.ReadModelUnavailable : TaskStatusQueryResultCode.Allowed);
        tenant.Gets.ShouldBe(2);
        model.Requests.ShouldBe(stale ? 0 : 1);
        logger.Entries.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("enabled")]
    [InlineData("throw_is_enabled")]
    [InlineData("throw_log")]
    public async Task ExceptionDiagnosticShouldPreserveFailClosedOutcomeWithoutExceptionObject(string mode)
    {
        CountingTenantAccessProjectionStore tenant = new(FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-a"]));
        ThrowingTaskStatusReadModel model = new();
        TaskStatusDiagnosticLogger logger = new(mode);

        TaskStatusQueryResult result = await Handler(tenant, model, logger).HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(TaskStatusQueryResultCode.ReadModelUnavailable);
        tenant.Gets.ShouldBe(2);
        model.Requests.ShouldBe(1);
        logger.ExceptionObjects.ShouldBe(0);
        if (mode == "enabled")
            logger.Entries.Single(entry => entry.ContainsKey("ExceptionClass"))["ExceptionClass"].ShouldBe("invalid_operation");
    }

    [Fact]
    public async Task DiagnosticProviderShouldNotObservePreCanceledRequestsOrChangeTheirToken()
    {
        CountingTenantAccessProjectionStore tenant = new();
        CountingTaskStatusReadModel model = new(TaskStatusReadModelResult.Unavailable("unavailable", Now));
        TaskStatusDiagnosticLogger logger = new("throw_is_enabled");
        using CancellationTokenSource canceled = new();
        canceled.Cancel();

        OperationCanceledException failure = await Should.ThrowAsync<OperationCanceledException>(
            () => Handler(tenant, model, logger).HandleAsync(Query(), canceled.Token));

        failure.CancellationToken.ShouldBe(canceled.Token);
        tenant.Gets.ShouldBe(0);
        model.Requests.ShouldBe(0);
        logger.Entries.ShouldBeEmpty();
    }

    [Fact]
    public void DiagnosticFieldsShouldClampAgeAndReplaceUnrecognizedStrings()
    {
        TaskStatusDiagnosticLogger logger = new();
        TaskStatusQueryDiagnostics.TenantObserved(logger, new TenantAccessAuthorizationResult(
            TenantAccessOutcome.Allowed, "private-code", "private-tenant", "private-watermark", Now,
            TimeSpan.MaxValue, TenantProjectionFreshnessStatus.Stale, "private-source"));
        TaskStatusQueryResult result = new(TaskStatusQueryResultCode.ReadModelUnavailable,
            "private-task", "private-state", null, null, null, new(false, "private-reason"), null,
            FolderLifecycleStatusTestSupport.Freshness(), "private-correlation", null)
        {
            FolderAuthorizationDenial = LayeredFolderAuthorizationResult.Denied(new(
                AuthorizationLayer.FolderAcl, "private-outcome", true, "private-freshness", "private-watermark",
                "private-correlation", "private-task", "private-actor", "private-policy", "private-timing", Now)),
        };
        TaskStatusQueryDiagnostics.Completed(logger, result);
        TaskStatusQueryDiagnostics.ReadModelFailed(logger, new Exception("private-exception-message"));

        logger.Entries[0]["ProjectionAgeSeconds"].ShouldBe(86400);
        logger.Entries[1]["FolderDenialOutcome"].ShouldBe("other");
        logger.Entries[1]["FolderDenialFreshness"].ShouldBe("other");
        logger.Entries[2]["ExceptionClass"].ShouldBe("other");
        string.Join("|", logger.Entries.SelectMany(entry => entry.Values)).ShouldNotContain("private-");
        logger.ExceptionObjects.ShouldBe(0);
    }

    private static async Task<TaskStatusQueryResult> ExecuteAsync(TaskStatusReadModelResult readModelResult)
    {
        CountingTenantAccessProjectionStore tenantStore = new(
            FolderLifecycleStatusTestSupport.TenantProjection(principals: ["user-a"]));
        TaskStatusQueryHandler handler = Handler(tenantStore, new CountingTaskStatusReadModel(readModelResult));

        return await handler.HandleAsync(Query(), TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private static TaskStatusQueryHandler Handler(
        IFolderTenantAccessProjectionStore tenantStore,
        ITaskStatusReadModel readModel,
        ILogger<TaskStatusQueryHandler>? logger = null)
        => new(
            new TenantAccessAuthorizer(tenantStore, new FixedUtcClock(Now), new TenantAccessOptions()),
            readModel,
            new FixedUtcClock(Now),
            logger,
            folderAuthorization: new LayeredFolderAuthorizationService(
                new TenantAccessAuthorizer(tenantStore, new FixedUtcClock(Now), new TenantAccessOptions()),
                new RecordingFolderPermissionEvidenceProvider(FolderPermissionEvidenceResult.Allowed(FolderLifecycleStatusTestSupport.AuthorizationWatermark)),
                new RecordingEventStoreAuthorizationValidator(EventStoreAuthorizationValidationResult.Allowed("validator_watermark_v1")),
                new RecordingDaprPolicyEvidenceProvider(DaprPolicyEvidenceResult.Allowed("folders", "dapr_policy_v1")),
                new FixedUtcClock(Now)));

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
            clientTenantValues ?? new Dictionary<string, string?>(StringComparer.Ordinal),
            FolderId: "folder-a");

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
            EvidenceScope: FolderLifecycleStatusTestSupport.EvidenceScope(actionToken: TaskStatusQueryHandler.ActionToken),
            FolderId: "folder-a");

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
#pragma warning restore xUnit1030
