using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Queries.ProviderReadiness;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderLifecycleSecurityBoundaryTests
{
    private static readonly DateTimeOffset Now = FolderLifecycleReplayFixture.OccurredAt.AddMinutes(30);

    [Fact]
    public void OverlappingLifecycleStreamsShouldRemainTenantScoped()
    {
        IReadOnlyList<IFolderEvent> tenantAEvents = FolderLifecycleReplayFixture.LockedLifecycleForTenant("tenant-a");
        IReadOnlyList<IFolderEvent> tenantBEvents = FolderLifecycleReplayFixture.LockedLifecycleForTenant("tenant-b");

        FolderStreamName tenantAStream = FolderStreamName.Create("tenant-a", "folder-a");
        FolderStreamName tenantBStream = FolderStreamName.Create("tenant-b", "folder-a");
        FolderState tenantAState = FolderState.Empty.Apply(tenantAEvents, tenantAStream);
        FolderState tenantBState = FolderState.Empty.Apply(tenantBEvents, tenantBStream);

        tenantAStream.Value.ShouldNotBe(tenantBStream.Value);
        tenantAState.ManagedTenantId.ShouldBe("tenant-a");
        tenantBState.ManagedTenantId.ShouldBe("tenant-b");
        tenantAState.FolderId.ShouldBe(tenantBState.FolderId);
        tenantAState.WorkspaceId.ShouldBe(tenantBState.WorkspaceId);
        tenantAState.WorkspaceTaskId.ShouldBe(tenantBState.WorkspaceTaskId);
        tenantAState.WorkspaceLifecycleState.ShouldBe(FolderWorkspaceLifecycleState.Locked);
        tenantBState.WorkspaceLifecycleState.ShouldBe(FolderWorkspaceLifecycleState.Locked);
    }

    [Fact]
    public async Task SameIdsUnderDifferentTenantsShouldWriteSeparateStreamsAndLedgers()
    {
        RecordingFolderRepository repository = new();
        repository.Seed(FolderStreamName.Create("tenant-a", "folder-a"), FolderLifecycleReplayFixture.LockedLifecycleForTenant("tenant-a"));
        repository.Seed(FolderStreamName.Create("tenant-b", "folder-a"), FolderLifecycleReplayFixture.LockedLifecycleForTenant("tenant-b"));
        RecordingContentStore contentStore = new();
        WorkspaceFileMutationService service = Service(repository, contentStore);

        FolderResult tenantAResult = await service.MutateAsync(Request("tenant-a"), TestContext.Current.CancellationToken);
        FolderResult tenantBResult = await service.MutateAsync(Request("tenant-b"), TestContext.Current.CancellationToken);

        tenantAResult.Code.ShouldBe(FolderResultCode.Accepted);
        tenantBResult.Code.ShouldBe(FolderResultCode.Accepted);
        contentStore.Requests.Select(request => request.ManagedTenantId).ShouldBe(["tenant-a", "tenant-b"]);
        repository.Load(FolderStreamName.Create("tenant-a", "folder-a")).WorkspaceLifecycleState.ShouldBe(FolderWorkspaceLifecycleState.ChangesStaged);
        repository.Load(FolderStreamName.Create("tenant-b", "folder-a")).WorkspaceLifecycleState.ShouldBe(FolderWorkspaceLifecycleState.ChangesStaged);
        repository.LastDurableKey.ShouldNotBeNull();
        repository.LastDurableKey.ShouldStartWith(FolderStreamName.Create("tenant-b", "folder-a").Value);
    }

    [Fact]
    public async Task WrongTenantAttemptShouldNotTouchProtectedTenantStream()
    {
        RecordingFolderRepository repository = new();
        repository.Seed(FolderStreamName.Create("tenant-a", "folder-a"), FolderLifecycleReplayFixture.LockedLifecycleForTenant("tenant-a"));
        RecordingContentStore contentStore = new();
        WorkspaceFileMutationService service = Service(repository, contentStore);

        FolderResult result = await service.MutateAsync(Request("tenant-b"), TestContext.Current.CancellationToken);

        result.Code.ShouldNotBe(FolderResultCode.Accepted);
        repository.LastStreamName.ShouldBe(FolderStreamName.Create("tenant-b", "folder-a").Value);
        contentStore.Requests.ShouldBeEmpty();
        repository.AppendsAttempted.ShouldBe(0);
        repository.Load(FolderStreamName.Create("tenant-a", "folder-a")).WorkspaceLifecycleState.ShouldBe(FolderWorkspaceLifecycleState.Locked);
    }

    [Fact]
    public async Task LockAcquisitionWithOverlappingIdsShouldWriteTenantScopedLedgers()
    {
        RecordingFolderRepository repository = new();
        repository.Seed(FolderStreamName.Create("tenant-a", "folder-a"), FolderLifecycleReplayFixture.ReadyLifecycleForTenant("tenant-a"));
        repository.Seed(FolderStreamName.Create("tenant-b", "folder-a"), FolderLifecycleReplayFixture.ReadyLifecycleForTenant("tenant-b"));
        WorkspaceLockAcquisitionService service = LockService(repository);

        FolderResult tenantAResult = await service.AcquireAsync(LockRequest("tenant-a"), TestContext.Current.CancellationToken);
        FolderResult tenantBResult = await service.AcquireAsync(LockRequest("tenant-b"), TestContext.Current.CancellationToken);

        tenantAResult.Code.ShouldBe(FolderResultCode.Accepted);
        tenantBResult.Code.ShouldBe(FolderResultCode.Accepted);
        repository.Load(FolderStreamName.Create("tenant-a", "folder-a")).WorkspaceLifecycleState.ShouldBe(FolderWorkspaceLifecycleState.Locked);
        repository.Load(FolderStreamName.Create("tenant-b", "folder-a")).WorkspaceLifecycleState.ShouldBe(FolderWorkspaceLifecycleState.Locked);
        repository.LastDurableKey.ShouldStartWith(FolderStreamName.Create("tenant-b", "folder-a").Value);
    }

    [Fact]
    public async Task LockReleaseWithOverlappingIdsShouldOnlyReleaseRequestedTenant()
    {
        RecordingFolderRepository repository = new();
        repository.Seed(FolderStreamName.Create("tenant-a", "folder-a"), FolderLifecycleReplayFixture.LockedLifecycleForTenant("tenant-a"));
        repository.Seed(FolderStreamName.Create("tenant-b", "folder-a"), FolderLifecycleReplayFixture.LockedLifecycleForTenant("tenant-b"));
        WorkspaceLockReleaseService service = ReleaseService(repository);

        FolderResult result = await service.ReleaseAsync(ReleaseRequest("tenant-a"), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.Accepted);
        repository.Load(FolderStreamName.Create("tenant-a", "folder-a")).WorkspaceLifecycleState.ShouldBe(FolderWorkspaceLifecycleState.Ready);
        repository.Load(FolderStreamName.Create("tenant-b", "folder-a")).WorkspaceLifecycleState.ShouldBe(FolderWorkspaceLifecycleState.Locked);
        repository.LastDurableKey.ShouldStartWith(FolderStreamName.Create("tenant-a", "folder-a").Value);
    }

    [Fact]
    public async Task CommitWithOverlappingIdsShouldExecuteOnlyRequestedTenant()
    {
        RecordingFolderRepository repository = new();
        repository.Seed(FolderStreamName.Create("tenant-a", "folder-a"), FolderLifecycleReplayFixture.ChangesStagedLifecycleForTenant("tenant-a"));
        repository.Seed(FolderStreamName.Create("tenant-b", "folder-a"), FolderLifecycleReplayFixture.ChangesStagedLifecycleForTenant("tenant-b"));
        RecordingCommitExecutor executor = new();
        WorkspaceCommitService service = CommitService(repository, executor);

        FolderResult result = await service.CommitAsync(CommitRequest("tenant-b"), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.Accepted);
        WorkspaceCommitExecutionRequest execution = executor.Requests.ShouldHaveSingleItem();
        execution.ManagedTenantId.ShouldBe("tenant-b");
        execution.FolderId.ShouldBe("folder-a");
        execution.WorkspaceId.ShouldBe("workspace-a");
        repository.Load(FolderStreamName.Create("tenant-a", "folder-a")).WorkspaceLifecycleState.ShouldBe(FolderWorkspaceLifecycleState.ChangesStaged);
        repository.Load(FolderStreamName.Create("tenant-b", "folder-a")).WorkspaceLifecycleState.ShouldBe(FolderWorkspaceLifecycleState.Committed);
    }

    private static WorkspaceFileMutationService Service(RecordingFolderRepository repository, RecordingContentStore contentStore)
        => new(
            AuthorizationService(),
            repository,
            new RecordingPathPolicyEvidenceProvider(),
            new FixedTimeProvider(Now),
            contentStore,
            new RecordingDeleteOperationStore());

    private static WorkspaceLockAcquisitionService LockService(RecordingFolderRepository repository)
        => new(AuthorizationService(), repository, new FixedTimeProvider(Now));

    private static WorkspaceLockReleaseService ReleaseService(RecordingFolderRepository repository)
        => new(AuthorizationService(), repository, new FixedTimeProvider(Now));

    private static WorkspaceCommitService CommitService(RecordingFolderRepository repository, RecordingCommitExecutor executor)
        => new(
            AuthorizationService(),
            new RecordingCommitReadinessValidator(),
            repository,
            executor,
            new FixedTimeProvider(Now));

    private static WorkspaceFileMutationRequest Request(string tenantId)
        => new(
            tenantId,
            PrincipalId: "principal-a",
            EventStoreClaimTransformEvidence.Allowed(tenantId, "principal-a", [WorkspaceFileMutationService.ActionToken]),
            FolderId: "folder-a",
            RequestSchemaVersion: "v1",
            WorkspaceId: "workspace-a",
            OperationId: "operation-a",
            FileOperationKind: "add",
            TransportOperation: "PutFileInline",
            new PathMetadata("docs/readme.md", "readme.md", "tenant_sensitive_document", "NFC"),
            ContentHashReference: "hashref-a",
            ByteLength: 12,
            MediaType: "text/plain",
            TransportEvidenceKind: "inline_decoded",
            ObservedByteLength: 12,
            CorrelationId: "correlation-a",
            TaskId: "task-a",
            IdempotencyKey: "idempotency-file-a",
            PayloadTenantId: null,
            ClientControlledTenantValues: new Dictionary<string, string?>(StringComparer.Ordinal),
            ClientControlledPrincipalValues: new Dictionary<string, string?>(StringComparer.Ordinal));

    private static WorkspaceLockAcquisitionRequest LockRequest(string tenantId)
        => new(
            tenantId,
            PrincipalId: "principal-a",
            EventStoreClaimTransformEvidence.Allowed(tenantId, "principal-a", [WorkspaceLockAcquisitionService.ActionToken]),
            FolderId: "folder-a",
            RequestSchemaVersion: "v1",
            WorkspaceId: "workspace-a",
            LockIntent: "exclusive_write",
            RequestedLeaseSeconds: 3600,
            CorrelationId: "correlation-a",
            TaskId: "task-a",
            IdempotencyKey: "idempotency-lock-a",
            PayloadTenantId: null,
            ClientControlledTenantValues: new Dictionary<string, string?>(StringComparer.Ordinal),
            ClientControlledPrincipalValues: new Dictionary<string, string?>(StringComparer.Ordinal));

    private static WorkspaceLockReleaseRequest ReleaseRequest(string tenantId)
    {
        const string lockId = "lock-id-a";
        return new(
            tenantId,
            PrincipalId: "principal-a",
            EventStoreClaimTransformEvidence.Allowed(tenantId, "principal-a", [WorkspaceLockReleaseService.ActionToken]),
            FolderId: "folder-a",
            RequestSchemaVersion: "v1",
            WorkspaceId: "workspace-a",
            LockId: lockId,
            LockOwnershipProof: FolderCommandValidator.DeriveWorkspaceLockOwnershipProof(
                tenantId,
                "folder-a",
                "workspace-a",
                "task-a",
                lockId),
            ReleaseReasonCode: "caller_completed",
            CorrelationId: "correlation-a",
            TaskId: "task-a",
            IdempotencyKey: "idempotency-release-a",
            PayloadTenantId: null,
            ClientControlledTenantValues: new Dictionary<string, string?>(StringComparer.Ordinal),
            ClientControlledPrincipalValues: new Dictionary<string, string?>(StringComparer.Ordinal));
    }

    private static WorkspaceCommitRequest CommitRequest(string tenantId)
        => new(
            tenantId,
            PrincipalId: "principal-a",
            EventStoreClaimTransformEvidence.Allowed(tenantId, "principal-a", [WorkspaceCommitService.ActionToken]),
            FolderId: "folder-a",
            RequestSchemaVersion: "v1",
            WorkspaceId: "workspace-a",
            OperationId: "operation-a",
            AuthorMetadataReference: "authorref_service",
            BranchRefTarget: "branchref_primary",
            CommitMessageClassification: "generated_summary",
            ChangedPathMetadataDigest: "digest_workspace_a",
            CorrelationId: "correlation-a",
            TaskId: "task-a",
            IdempotencyKey: "idempotency-commit-a",
            PayloadTenantId: null,
            ClientControlledTenantValues: new Dictionary<string, string?>(StringComparer.Ordinal),
            ClientControlledPrincipalValues: new Dictionary<string, string?>(StringComparer.Ordinal));

    private static ProviderReadinessValidationResult Readiness()
        => new(
            ProviderReadinessResultCode.Allowed,
            Status: "ready",
            ReasonCode: "success",
            SafeRemediationCode: "metadata_only_remediation",
            Retryable: false,
            RetryAfter: null,
            RemediationCategory: "contact_operator",
            CorrelationId: "correlation-a",
            ProviderReference: "provider-binding-a",
            ProviderBindingRef: "provider-binding-a",
            CapabilityProfileRef: "capability-profile-a",
            Evidence: null,
            Freshness: new ProviderReadinessFreshness(
                "snapshot_per_task",
                Now,
                ProjectionWatermark: "tenant:7",
                Stale: false),
            ProviderFailureCategory.None,
            CategoryCode: "success");

    private static LayeredFolderAuthorizationService AuthorizationService()
        => new(
            new TenantAccessAuthorizer(
                TenantStore(),
                new FixedUtcClock(Now),
                new TenantAccessOptions
                {
                    MutationFreshnessBudget = TimeSpan.FromMinutes(5),
                    DiagnosticStalenessBudget = TimeSpan.FromMinutes(5),
                }),
            new RecordingFolderPermissionEvidenceProvider(),
            new RecordingEventStoreAuthorizationValidator(),
            new RecordingDaprPolicyEvidenceProvider(),
            new FixedUtcClock(Now));

    private static IFolderTenantAccessProjectionStore TenantStore()
    {
        InMemoryFolderTenantAccessProjectionStore store = new();
        foreach (string tenantId in new[] { "tenant-a", "tenant-b" })
        {
            store.SaveAsync(new FolderTenantAccessProjection
            {
                TenantId = tenantId,
                Enabled = true,
                Principals = new Dictionary<string, FolderTenantPrincipalEvidence>(StringComparer.Ordinal)
                {
                    ["principal-a"] = new("principal-a", "Member"),
                },
                Watermark = 7,
                ProjectionWatermark = tenantId + ":7",
                LastEventTimestamp = Now.AddMinutes(-1),
            }).GetAwaiter().GetResult();
        }

        return store;
    }

    private sealed class RecordingPathPolicyEvidenceProvider : IWorkspacePathPolicyEvidenceProvider
    {
        public Task<WorkspacePathPolicyEvidenceResult> GetEvidenceAsync(
            WorkspacePathPolicyEvidenceRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new WorkspacePathPolicyEvidenceResult(WorkspacePathPolicyEvidenceDecision.NoEscape));
    }

    private sealed class RecordingContentStore : IWorkspaceFileContentStore
    {
        public List<WorkspaceFileContentStoreRequest> Requests { get; } = [];

        public Task<WorkspaceFileContentStoreResult> StageAsync(
            WorkspaceFileContentStoreRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(WorkspaceFileContentStoreResult.Succeeded);
        }
    }

    private sealed class RecordingDeleteOperationStore : IWorkspaceFileDeleteOperationStore
    {
        public Task<WorkspaceFileDeleteOperationStoreResult> StageAsync(
            WorkspaceFileDeleteOperationStoreRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(WorkspaceFileDeleteOperationStoreResult.Succeeded);
    }

    private sealed class RecordingCommitReadinessValidator : IWorkspaceCommitReadinessValidator
    {
        public Task<ProviderReadinessValidationResult> ValidateAsync(
            ProviderReadinessValidationRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Readiness());
    }

    private sealed class RecordingCommitExecutor : IWorkspaceCommitExecutor
    {
        public List<WorkspaceCommitExecutionRequest> Requests { get; } = [];

        public Task<WorkspaceCommitExecutionResult> CommitAsync(
            WorkspaceCommitExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(WorkspaceCommitExecutionResult.Succeeded("commitref_boundary_a"));
        }
    }

    private sealed class RecordingFolderPermissionEvidenceProvider : IFolderPermissionEvidenceProvider
    {
        public Task<FolderPermissionEvidenceResult> GetEvidenceAsync(
            FolderPermissionEvidenceRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(FolderPermissionEvidenceResult.Allowed(
                request.ManagedTenantId + ":folder:7",
                organizationId: "organization-a"));
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
