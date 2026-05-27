using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Queries.ProviderReadiness;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderWorkspaceCommitServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 27, 23, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task MissingAuthenticationShouldDenyBeforeStreamLoadAndCommitExecutor()
    {
        RecordingFolderRepository repository = new();
        RecordingCommitExecutor executor = new();
        WorkspaceCommitService service = Service(repository, executor);

        FolderResult result = await service.CommitAsync(
            Request(authoritativeTenantId: null),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.MissingAuthoritativeTenant);
        repository.StreamsLoaded.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
        executor.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task FolderAuthorizationDenialShouldDenyBeforeStreamLoadAndCommitExecutor()
    {
        RecordingFolderRepository repository = StagedRepository();
        RecordingCommitExecutor executor = new();
        WorkspaceCommitService service = Service(
            repository,
            executor,
            folderPermissionEvidenceProvider: new RecordingFolderPermissionEvidenceProvider(
                FolderPermissionEvidenceResult.FromStatus(
                    FolderPermissionEvidenceStatus.Denied,
                    "folder-watermark-denied")));

        FolderResult result = await service.CommitAsync(Request(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.FolderAclDenied);
        repository.StreamsLoaded.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
        executor.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ValidationDenialShouldHappenBeforeStreamLoadAndCommitExecutor()
    {
        RecordingFolderRepository repository = StagedRepository();
        RecordingCommitExecutor executor = new();
        WorkspaceCommitService service = Service(repository, executor);

        FolderResult result = await service.CommitAsync(
            Request(branchRefTarget: "refs/heads/main"),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.ValidationFailed);
        repository.StreamsLoaded.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
        executor.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task CommitSuccessShouldExecuteOnceAppendAndShortCircuitEquivalentReplay()
    {
        RecordingFolderRepository repository = StagedRepository();
        RecordingCommitExecutor executor = new();
        WorkspaceCommitService service = Service(repository, executor);

        FolderResult first = await service.CommitAsync(Request(), TestContext.Current.CancellationToken);
        executor.Result = WorkspaceCommitExecutionResult.KnownFailure("provider_unavailable");
        FolderResult replay = await service.CommitAsync(Request(), TestContext.Current.CancellationToken);

        first.Code.ShouldBe(FolderResultCode.Accepted);
        replay.Code.ShouldBe(FolderResultCode.IdempotentReplay);
        executor.Requests.Count.ShouldBe(1);
        WorkspaceCommitExecutionRequest executionRequest = executor.Requests.ShouldHaveSingleItem();
        executionRequest.ManagedTenantId.ShouldBe("tenant-a");
        executionRequest.FolderId.ShouldBe("folder-a");
        executionRequest.WorkspaceId.ShouldBe("workspace-a");
        executionRequest.OperationId.ShouldBe("operation-a");
        executionRequest.CorrelationId.ShouldBe("correlation-commit-a");
        executionRequest.TaskId.ShouldBe("task-a");
        executionRequest.AuthorMetadataReference.ShouldBe("authorref_service");
        executionRequest.BranchRefTarget.ShouldBe("branchref_primary");
        executionRequest.CommitMessageClassification.ShouldBe("generated_summary");
        executionRequest.ChangedPathMetadataDigest.ShouldBe("digest_workspace_a");
        repository.IdempotencyLookups.ShouldBe(1);
        repository.AppendsAttempted.ShouldBe(1);
        repository.LastAppendedEvents.ShouldHaveSingleItem().ShouldBeOfType<WorkspaceCommitSucceeded>();
    }

    [Fact]
    public async Task UnsupportedCommitCapabilityShouldRejectBeforeCommitExecutorAndAppend()
    {
        RecordingFolderRepository repository = StagedRepository();
        RecordingCommitExecutor executor = new();
        RecordingCommitReadinessValidator readiness = new()
        {
            Result = Readiness(ProviderFailureCategory.UnsupportedProviderCapability),
        };
        WorkspaceCommitService service = Service(repository, executor, readinessValidator: readiness);

        FolderResult result = await service.CommitAsync(Request(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.UnsupportedProviderCapability);
        readiness.Requests.Count.ShouldBe(1);
        readiness.Requests[0].RequestedCapability.ShouldBe(ProviderReadinessRequestedCapability.CommitStatus);
        executor.Requests.ShouldBeEmpty();
        repository.AppendsAttempted.ShouldBe(0);
        repository.EventsAppended.ShouldBe(0);
    }

    [Fact]
    public async Task SameIdempotencyKeyWithDifferentPayloadShouldConflictBeforeCommitExecutor()
    {
        RecordingFolderRepository repository = StagedRepository();
        RecordingCommitExecutor executor = new();
        WorkspaceCommitService service = Service(repository, executor);

        FolderResult first = await service.CommitAsync(Request(), TestContext.Current.CancellationToken);
        FolderResult conflict = await service.CommitAsync(Request(operationId: "operation-b"), TestContext.Current.CancellationToken);

        first.Code.ShouldBe(FolderResultCode.Accepted);
        conflict.Code.ShouldBe(FolderResultCode.IdempotencyConflict);
        executor.Requests.Count.ShouldBe(1);
        repository.AppendsAttempted.ShouldBe(1);
    }

    [Fact]
    public async Task AppendConflictShouldRereadAndSurfaceIdempotentReplayWhenCommitWonRace()
    {
        RecordingFolderRepository repository = StagedRepository();
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        CommitWorkspace command = CommitCommand();
        repository.ConcurrentAppendEvents = FolderAggregate
            .Handle(
                repository.Load(streamName),
                command,
                WorkspaceCommitExecutionResult.Succeeded("commitref_abc123"),
                Now)
            .Events;
        repository.SimulateAppendConflict = true;
        RecordingCommitExecutor executor = new();
        WorkspaceCommitService service = Service(repository, executor);

        FolderResult result = await service.CommitAsync(Request(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.IdempotentReplay);
        executor.Requests.Count.ShouldBe(1);
        repository.AppendsAttempted.ShouldBe(1);
        repository.ConcurrentEventsApplied.ShouldBe(1);
    }

    [Fact]
    public async Task AppendConflictWithoutWinningCommitShouldSurfaceAppendConflict()
    {
        RecordingFolderRepository repository = StagedRepository();
        repository.SimulateAppendConflict = true;
        RecordingCommitExecutor executor = new();
        WorkspaceCommitService service = Service(repository, executor);

        FolderResult result = await service.CommitAsync(Request(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.AppendConflict);
        executor.Requests.Count.ShouldBe(1);
        repository.AppendsAttempted.ShouldBe(1);
        repository.LastAppendedEvents.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(WorkspaceCommitExecutionStatus.KnownFailure, typeof(WorkspaceCommitFailed), FolderWorkspaceLifecycleState.Failed)]
    [InlineData(WorkspaceCommitExecutionStatus.UnknownOutcome, typeof(WorkspaceCommitOutcomeUnknown), FolderWorkspaceLifecycleState.UnknownProviderOutcome)]
    [InlineData(WorkspaceCommitExecutionStatus.ReconciliationRequired, typeof(WorkspaceCommitOutcomeUnknown), FolderWorkspaceLifecycleState.UnknownProviderOutcome)]
    public async Task CommitOutcomeMappingShouldAppendMetadataOnlyOutcomeEvents(
        WorkspaceCommitExecutionStatus status,
        Type eventType,
        FolderWorkspaceLifecycleState expectedState)
    {
        RecordingFolderRepository repository = StagedRepository();
        RecordingCommitExecutor executor = new()
        {
            Result = status == WorkspaceCommitExecutionStatus.KnownFailure
                ? WorkspaceCommitExecutionResult.KnownFailure("provider_unavailable")
                : status == WorkspaceCommitExecutionStatus.UnknownOutcome
                    ? WorkspaceCommitExecutionResult.UnknownOutcome("reconcile_commit_a")
                    : WorkspaceCommitExecutionResult.ReconciliationRequired("reconcile_commit_a"),
        };
        WorkspaceCommitService service = Service(repository, executor);

        FolderResult result = await service.CommitAsync(Request(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.Accepted);
        IFolderEvent appended = repository.LastAppendedEvents.ShouldHaveSingleItem();
        appended.GetType().ShouldBe(eventType);
        if (status == WorkspaceCommitExecutionStatus.ReconciliationRequired)
        {
            WorkspaceCommitOutcomeUnknown unknown = appended.ShouldBeOfType<WorkspaceCommitOutcomeUnknown>();
            unknown.ProviderOutcomeCategory.ShouldBe("reconciliation_required");
            unknown.ReconciliationRequired.ShouldBeTrue();
        }

        repository.Load(FolderStreamName.Create("tenant-a", "folder-a")).WorkspaceLifecycleState.ShouldBe(expectedState);
    }

    [Fact]
    public async Task UnknownOutcomeShouldScheduleDeterministicReconciliationWithoutRetry()
    {
        RecordingFolderRepository repository = StagedRepository();
        RecordingCommitExecutor executor = new()
        {
            Result = WorkspaceCommitExecutionResult.UnknownOutcome("reconcile_provider_timeout"),
        };
        WorkspaceCommitService service = Service(repository, executor);

        FolderResult result = await service.CommitAsync(Request(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.Accepted);
        WorkspaceCommitOutcomeUnknown unknown = repository.LastAppendedEvents
            .ShouldHaveSingleItem()
            .ShouldBeOfType<WorkspaceCommitOutcomeUnknown>();
        unknown.ReconciliationReference.ShouldBe(
            FolderCommandValidator.DeriveCommitReconciliationReference(CommitCommand()));
        unknown.ReconciliationRequired.ShouldBeFalse();
        executor.Requests.Count.ShouldBe(1);
        repository.AppendsAttempted.ShouldBe(1);
    }

    [Fact]
    public async Task IdempotencyUnavailableShouldNotExecuteCommit()
    {
        RecordingFolderRepository repository = StagedRepository();
        repository.IdempotencyUnavailable = true;
        RecordingCommitExecutor executor = new();
        WorkspaceCommitService service = Service(repository, executor);

        FolderResult result = await service.CommitAsync(Request(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.IdempotencyUnavailable);
        executor.Requests.ShouldBeEmpty();
        repository.AppendsAttempted.ShouldBe(0);
    }

    private static WorkspaceCommitService Service(
        IFolderRepository repository,
        IWorkspaceCommitExecutor executor,
        IFolderPermissionEvidenceProvider? folderPermissionEvidenceProvider = null,
        IWorkspaceCommitReadinessValidator? readinessValidator = null)
        => new(
            AuthorizationService(folderPermissionEvidenceProvider),
            readinessValidator ?? new RecordingCommitReadinessValidator(),
            repository,
            executor,
            new FixedTimeProvider(Now));

    private static ProviderReadinessValidationResult Readiness(ProviderFailureCategory failureCategory)
        => new(
            ProviderReadinessResultCode.Allowed,
            failureCategory == ProviderFailureCategory.None ? "ready" : "failed",
            failureCategory == ProviderFailureCategory.None ? "success" : failureCategory.ToCategoryCode(),
            "metadata_only_remediation",
            Retryable: false,
            RetryAfter: null,
            RemediationCategory: "contact_operator",
            CorrelationId: "correlation-commit-a",
            ProviderReference: "provider-binding-a",
            ProviderBindingRef: "provider-binding-a",
            CapabilityProfileRef: "capability-profile-a",
            Evidence: null,
            Freshness: new ProviderReadinessFreshness(
                "snapshot_per_task",
                Now,
                ProjectionWatermark: "tenant-a:7",
                Stale: false),
            failureCategory,
            failureCategory.ToCategoryCode());

    private static WorkspaceCommitRequest Request(
        string? authoritativeTenantId = "tenant-a",
        string operationId = "operation-a",
        string branchRefTarget = "branchref_primary")
        => new(
            authoritativeTenantId ?? string.Empty,
            PrincipalId: "user-a",
            string.IsNullOrWhiteSpace(authoritativeTenantId)
                ? EventStoreClaimTransformEvidence.Missing()
                : EventStoreClaimTransformEvidence.Allowed(authoritativeTenantId, "user-a", [WorkspaceCommitService.ActionToken]),
            FolderId: "folder-a",
            RequestSchemaVersion: "v1",
            WorkspaceId: "workspace-a",
            OperationId: operationId,
            AuthorMetadataReference: "authorref_service",
            BranchRefTarget: branchRefTarget,
            CommitMessageClassification: "generated_summary",
            ChangedPathMetadataDigest: "digest_workspace_a",
            CorrelationId: "correlation-commit-a",
            TaskId: "task-a",
            IdempotencyKey: "idempotency-commit-a",
            PayloadTenantId: null,
            ClientControlledTenantValues: new Dictionary<string, string?>(StringComparer.Ordinal),
            ClientControlledPrincipalValues: new Dictionary<string, string?>(StringComparer.Ordinal));

    private static CommitWorkspace CommitCommand()
        => new(
            "tenant-a",
            "organization-a",
            "folder-a",
            "v1",
            "workspace-a",
            "operation-a",
            "authorref_service",
            "branchref_primary",
            "generated_summary",
            "digest_workspace_a",
            "user-a",
            "correlation-commit-a",
            "task-a",
            "idempotency-commit-a",
            PayloadTenantId: null);

    private static RecordingFolderRepository StagedRepository()
    {
        RecordingFolderRepository repository = new();
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        repository.Seed(streamName, StagedEvents(streamName));
        return repository;
    }

    private static IReadOnlyList<IFolderEvent> StagedEvents(FolderStreamName streamName)
    {
        IReadOnlyList<IFolderEvent> readyEvents = SeedReadyEvents(streamName);
        FolderState ready = FolderState.Empty.Apply(readyEvents, streamName);
        FolderResult locked = FolderAggregate.Handle(ready, FolderCommandFactory.LockWorkspace(), Now);
        FolderState lockedState = ready.Apply(locked.Events, streamName);
        FolderResult mutated = FolderAggregate.Handle(lockedState, Mutation(), Now);
        return [.. readyEvents, .. locked.Events, .. mutated.Events];
    }

    private static MutateWorkspaceFile Mutation()
        => new(
            "tenant-a",
            "organization-a",
            "folder-a",
            "v1",
            "workspace-a",
            "operation-file-a",
            "add",
            "PutFileInline",
            new PathMetadata("docs/readme.md", "readme.md", "tenant_sensitive_document", "NFC"),
            "hashref-a",
            12,
            "text/plain",
            "inline_decoded",
            12,
            "principal-a",
            "correlation-file-a",
            "task-a",
            "idempotency-file-a",
            PayloadTenantId: null);

    private static IReadOnlyList<IFolderEvent> SeedReadyEvents(FolderStreamName streamName)
    {
        FolderResult created = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create(), Now);
        FolderState createdState = FolderState.Empty.Apply(created.Events, streamName);
        FolderResult requested = FolderAggregate.Handle(createdState, FolderCommandFactory.CreateRepositoryBackedFolder(), Now);
        FolderState boundState = createdState.Apply(
            [
                .. requested.Events,
                new RepositoryBound("tenant-a", "organization-a", "folder-a", "repository-binding-a", "provider-binding-a", "correlation-bound-a", "task-bound-a", "idempotency-bound-a", "fingerprint-bound-a", Now),
            ],
            streamName);
        FolderResult configured = FolderAggregate.Handle(
            boundState,
            new ConfigureBranchRefPolicy("tenant-a", "organization-a", "folder-a", "v1", "repository-binding-a", "branch-ref-policy-a", "branch_ref_primary", ["branch_ref_feature"], ["branch_ref_release"], "principal-a", "correlation-policy-a", "task-a", "idempotency-policy-a", PayloadTenantId: null),
            Now);
        FolderState configuredState = boundState.Apply(configured.Events, streamName);
        FolderResult prepare = FolderAggregate.Handle(configuredState, FolderCommandFactory.PrepareWorkspace(), Now);

        return
        [
            .. created.Events,
            .. requested.Events,
            new RepositoryBound("tenant-a", "organization-a", "folder-a", "repository-binding-a", "provider-binding-a", "correlation-bound-a", "task-bound-a", "idempotency-bound-a", "fingerprint-bound-a", Now),
            .. configured.Events,
            .. prepare.Events,
            new FolderWorkspaceLifecycleEventRecorded("tenant-a", "organization-a", "folder-a", "workspace-a", FolderWorkspaceLifecycleEvent.WorkspacePrepared, DirtyResolution: null, OperationId: "workspace-a", "correlation-prepared-a", "task-a", "idempotency-workspace-outcome-a", "fingerprint-workspace-outcome-a", Now),
        ];
    }

    private static LayeredFolderAuthorizationService AuthorizationService(
        IFolderPermissionEvidenceProvider? folderPermissionEvidenceProvider = null)
        => new(
            new TenantAccessAuthorizer(
                TenantStore(),
                new FixedUtcClock(Now),
                new TenantAccessOptions
                {
                    MutationFreshnessBudget = TimeSpan.FromMinutes(5),
                    DiagnosticStalenessBudget = TimeSpan.FromMinutes(5),
                }),
            folderPermissionEvidenceProvider ?? new RecordingFolderPermissionEvidenceProvider(),
            new RecordingEventStoreAuthorizationValidator(),
            new RecordingDaprPolicyEvidenceProvider(),
            new FixedUtcClock(Now));

    private static IFolderTenantAccessProjectionStore TenantStore()
    {
        InMemoryFolderTenantAccessProjectionStore store = new();
        store.SaveAsync(new FolderTenantAccessProjection
        {
            TenantId = "tenant-a",
            Enabled = true,
            Principals = new Dictionary<string, FolderTenantPrincipalEvidence>(StringComparer.Ordinal)
            {
                ["user-a"] = new("user-a", "Member"),
            },
            Watermark = 7,
            ProjectionWatermark = "tenant-a:7",
            LastEventTimestamp = Now.AddMinutes(-1),
        }).GetAwaiter().GetResult();
        return store;
    }

    private sealed class RecordingCommitExecutor : IWorkspaceCommitExecutor
    {
        public WorkspaceCommitExecutionResult Result { get; set; } =
            WorkspaceCommitExecutionResult.Succeeded("commitref_abc123");

        public List<WorkspaceCommitExecutionRequest> Requests { get; } = [];

        public Task<WorkspaceCommitExecutionResult> CommitAsync(
            WorkspaceCommitExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(Result);
        }
    }

    private sealed class RecordingCommitReadinessValidator : IWorkspaceCommitReadinessValidator
    {
        public ProviderReadinessValidationResult Result { get; set; } = Readiness(ProviderFailureCategory.None);

        public List<ProviderReadinessValidationRequest> Requests { get; } = [];

        public Task<ProviderReadinessValidationResult> ValidateAsync(
            ProviderReadinessValidationRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(Result);
        }
    }

    private sealed class RecordingFolderPermissionEvidenceProvider(
        FolderPermissionEvidenceResult? result = null) : IFolderPermissionEvidenceProvider
    {
        public Task<FolderPermissionEvidenceResult> GetEvidenceAsync(
            FolderPermissionEvidenceRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(result ?? FolderPermissionEvidenceResult.Allowed("folder-a:7", organizationId: "organization-a"));
    }

    private sealed class RecordingEventStoreAuthorizationValidator : IEventStoreAuthorizationValidator
    {
        public Task<EventStoreAuthorizationValidationResult> ValidateAsync(
            EventStoreAuthorizationValidationRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(EventStoreAuthorizationValidationResult.Allowed("validator-a"));
    }

    private sealed class RecordingDaprPolicyEvidenceProvider : IDaprPolicyEvidenceProvider
    {
        public Task<DaprPolicyEvidenceResult> GetEvidenceAsync(
            DaprPolicyEvidenceRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(DaprPolicyEvidenceResult.Allowed("folders", "service_invocation_v1"));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
