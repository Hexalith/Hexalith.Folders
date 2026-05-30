using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.Folders;

namespace Hexalith.Folders.LoadTests.Scenarios;

public sealed class LifecycleCapacityDriver
{
    private static readonly DateTimeOffset BaselineTime = new(2026, 5, 27, 0, 0, 0, TimeSpan.Zero);

    private readonly LifecycleCapacityIteration _iteration;
    private readonly LifecycleCapacityRunRecorder _recorder;
    private readonly InMemoryFolderRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly WorkspacePreparationService _preparationService;
    private readonly WorkspaceLockAcquisitionService _lockService;
    private readonly WorkspaceFileMutationService _mutationService;
    private readonly WorkspaceCommitService _commitService;
    private readonly InMemoryWorkspaceStatusReadModel _statusReadModel;
    private readonly WorkspaceStatusQueryHandler _statusQueryHandler;

    public LifecycleCapacityDriver(
        LifecycleCapacityIteration iteration,
        LifecycleCapacityRunRecorder recorder)
    {
        _iteration = iteration ?? throw new ArgumentNullException(nameof(iteration));
        _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        _timeProvider = new FixedTimeProvider(BaselineTime);
        _repository = new InMemoryFolderRepository(timeProvider: _timeProvider);
        SeedConfiguredFolder();

        LayeredFolderAuthorizationService authorization = CreateAuthorizationService(iteration);
        ReadyWorkspaceReadinessValidator readiness = new(iteration, BaselineTime);
        _preparationService = new WorkspacePreparationService(authorization, readiness, _repository, _timeProvider);
        _lockService = new WorkspaceLockAcquisitionService(authorization, _repository, _timeProvider);
        _mutationService = new WorkspaceFileMutationService(
            authorization,
            _repository,
            new SafePathPolicyEvidenceProvider(),
            _timeProvider,
            new MetadataOnlyContentStore(_recorder, iteration),
            new MetadataOnlyDeleteOperationStore());
        _commitService = new WorkspaceCommitService(
            authorization,
            readiness,
            _repository,
            new SyntheticCommitExecutor(iteration),
            _timeProvider);
        _statusReadModel = new InMemoryWorkspaceStatusReadModel(new FixedUtcClock(BaselineTime));
        _statusQueryHandler = new WorkspaceStatusQueryHandler(authorization, _statusReadModel, new FixedUtcClock(BaselineTime));
    }

    public async Task<FolderResultCode> PrepareAsync(CancellationToken cancellationToken)
    {
        _recorder.RecordIteration(_iteration);
        _recorder.RecordOperation(_iteration, _iteration.PrepareOperationId, _iteration.PrepareIdempotencyKey);
        FolderResult result = await _preparationService.PrepareAsync(
            new WorkspacePreparationRequest(
                _iteration.TenantId,
                _iteration.PrincipalId,
                ClaimTransform(WorkspacePreparationService.ActionToken),
                _iteration.FolderId,
                "v1",
                _iteration.WorkspaceId,
                _iteration.RepositoryBindingId,
                _iteration.BranchRefPolicyRef,
                _iteration.WorkspacePolicyRef,
                _iteration.PrepareCorrelationId,
                _iteration.TaskId,
                _iteration.PrepareIdempotencyKey,
                PayloadTenantId: null,
                new Dictionary<string, string?>(StringComparer.Ordinal),
                new Dictionary<string, string?>(StringComparer.Ordinal)),
            cancellationToken).ConfigureAwait(false);

        if (result.Code == FolderResultCode.Accepted)
        {
            AppendWorkspacePreparedOutcome();
        }

        return Record(LifecycleCapacityScenario.PrepareStepName, result.Code);
    }

    public async Task<FolderResultCode> AcquireLockAsync(CancellationToken cancellationToken)
    {
        _recorder.RecordOperation(_iteration, _iteration.LockOperationId, _iteration.LockIdempotencyKey);
        FolderResult result = await _lockService.AcquireAsync(
            new WorkspaceLockAcquisitionRequest(
                _iteration.TenantId,
                _iteration.PrincipalId,
                ClaimTransform(WorkspaceLockAcquisitionService.ActionToken),
                _iteration.FolderId,
                "v1",
                _iteration.WorkspaceId,
                "exclusive_write",
                RequestedLeaseSeconds: 300,
                _iteration.LockCorrelationId,
                _iteration.TaskId,
                _iteration.LockIdempotencyKey,
                PayloadTenantId: null,
                new Dictionary<string, string?>(StringComparer.Ordinal),
                new Dictionary<string, string?>(StringComparer.Ordinal)),
            cancellationToken).ConfigureAwait(false);

        return Record(LifecycleCapacityScenario.LockStepName, result.Code);
    }

    public async Task<FolderResultCode> MutateFileAsync(CancellationToken cancellationToken)
    {
        _recorder.RecordOperation(_iteration, _iteration.MutationOperationId, _iteration.MutationIdempotencyKey);
        FolderResult result = await _mutationService.MutateAsync(
            new WorkspaceFileMutationRequest(
                _iteration.TenantId,
                _iteration.PrincipalId,
                ClaimTransform(WorkspaceFileMutationService.ActionToken),
                _iteration.FolderId,
                "v1",
                _iteration.WorkspaceId,
                _iteration.MutationOperationId,
                "add",
                "PutFileInline",
                new PathMetadata(_iteration.RelativePath, "capacity-file.md", "tenant_sensitive_document", "NFC"),
                _iteration.ContentHashReference,
                ByteLength: 32,
                "text/plain",
                "inline_decoded",
                ObservedByteLength: 32,
                _iteration.MutationCorrelationId,
                _iteration.TaskId,
                _iteration.MutationIdempotencyKey,
                PayloadTenantId: null,
                new Dictionary<string, string?>(StringComparer.Ordinal),
                new Dictionary<string, string?>(StringComparer.Ordinal)),
            cancellationToken).ConfigureAwait(false);

        return Record(LifecycleCapacityScenario.MutateStepName, result.Code);
    }

    public async Task<FolderResultCode> CommitAsync(CancellationToken cancellationToken)
    {
        _recorder.RecordOperation(_iteration, _iteration.CommitOperationId, _iteration.CommitIdempotencyKey);
        FolderResult result = await _commitService.CommitAsync(
            new WorkspaceCommitRequest(
                _iteration.TenantId,
                _iteration.PrincipalId,
                ClaimTransform(WorkspaceCommitService.ActionToken),
                _iteration.FolderId,
                "v1",
                _iteration.WorkspaceId,
                _iteration.CommitOperationId,
                "authorref_capacity_000001",
                "branchref_primary",
                "generated_summary",
                _iteration.ChangedPathMetadataDigest,
                _iteration.CommitCorrelationId,
                _iteration.TaskId,
                _iteration.CommitIdempotencyKey,
                PayloadTenantId: null,
                new Dictionary<string, string?>(StringComparer.Ordinal),
                new Dictionary<string, string?>(StringComparer.Ordinal)),
            cancellationToken).ConfigureAwait(false);

        if (result.Code == FolderResultCode.Accepted)
        {
            AppendWorkspaceCommittedStatusSnapshot();
        }

        return Record(LifecycleCapacityScenario.CommitStepName, result.Code);
    }

    public async Task<WorkspaceStatusQueryResultCode> ReadStatusAsync(CancellationToken cancellationToken)
    {
        WorkspaceStatusQueryResult result = await _statusQueryHandler.HandleAsync(
            new WorkspaceStatusQuery(
                _iteration.FolderId,
                _iteration.WorkspaceId,
                _iteration.TenantId,
                _iteration.PrincipalId,
                EventStoreClaimTransformEvidence.Allowed(
                    _iteration.TenantId,
                    _iteration.PrincipalId,
                    [WorkspaceStatusQueryHandler.ActionToken]),
                _iteration.CommitCorrelationId,
                _iteration.TaskId,
                ClientControlledTenantValues: null,
                ClientControlledPrincipalValues: null),
            cancellationToken).ConfigureAwait(false);

        _recorder.RecordMeasuredStep(LifecycleCapacityScenario.StatusStepName);
        _recorder.RecordResult(result.Code.ToString());
        return result.Code;
    }

    private FolderResultCode Record(string stepName, FolderResultCode code)
    {
        _recorder.RecordMeasuredStep(stepName);
        _recorder.RecordResult(code);
        return code;
    }

    private void AppendWorkspacePreparedOutcome()
    {
        FolderStreamName streamName = _repository.CreateStreamName(_iteration.TenantId, _iteration.FolderId);
        _repository.AppendIfFingerprintAbsent(
            streamName,
            _iteration.PreparedOutcomeIdempotencyKey,
            $"fingerprint-{_iteration.PreparedOutcomeIdempotencyKey}",
            [
                new FolderWorkspaceLifecycleEventRecorded(
                    _iteration.TenantId,
                    _iteration.OrganizationId,
                    _iteration.FolderId,
                    _iteration.WorkspaceId,
                    FolderWorkspaceLifecycleEvent.WorkspacePrepared,
                    DirtyResolution: null,
                    OperationId: _iteration.WorkspaceId,
                    _iteration.PrepareCorrelationId,
                    _iteration.TaskId,
                    _iteration.PreparedOutcomeIdempotencyKey,
                    $"fingerprint-{_iteration.PreparedOutcomeIdempotencyKey}",
                    _timeProvider.GetUtcNow()),
            ]);
    }

    private void AppendWorkspaceCommittedStatusSnapshot()
        => _statusReadModel.Save(new WorkspaceStatusReadModelSnapshot(
            ManagedTenantId: _iteration.TenantId,
            FolderId: _iteration.FolderId,
            WorkspaceId: _iteration.WorkspaceId,
            CurrentState: "committed",
            AcceptedCommandState: new WorkspaceAcceptedCommandState(
                _iteration.TaskId,
                _iteration.CommitOperationId,
                "completed",
                BaselineTime),
            ProjectedState: new WorkspaceProjectedState("committed", "projection", BaselineTime),
            ProviderOutcome: new WorkspaceProviderOutcome(
                _iteration.CommitOperationId,
                "known_success",
                "success",
                "provref_capacity_status",
                new WorkspaceStatusRetryEligibility(false, "retry_not_required"),
                RetryAfter: null,
                Freshness: WorkspaceStatusFreshness(),
                _iteration.ChangedPathMetadataDigest,
                CommitReferenceClassification: "opaque_reference",
                ReconciliationReference: null),
            RetryEligibility: new WorkspaceStatusRetryEligibility(false, "retry_not_required"),
            RetryAfter: null,
            Freshness: WorkspaceStatusFreshness(),
            ProjectionLag: new WorkspaceProjectionLag(0, "projection"),
            LastFailureCategory: null,
            EvidenceScope: new FolderLifecycleEvidenceScope(
                _iteration.TenantId,
                _iteration.PrincipalId,
                WorkspaceStatusQueryHandler.ActionToken,
                _iteration.TaskId,
                _iteration.CommitCorrelationId,
                $"{_iteration.TenantId}:folder-permission:7")));

    private static FolderLifecycleFreshness WorkspaceStatusFreshness()
        => new("read_your_writes", BaselineTime, "workspace_status_watermark_v1", Stale: false, ReasonCode: null);

    private void SeedConfiguredFolder()
    {
        FolderStreamName streamName = FolderStreamName.Create(_iteration.TenantId, _iteration.FolderId);
        CreateFolder create = new(
            _iteration.TenantId,
            _iteration.OrganizationId,
            _iteration.FolderId,
            "Capacity Folder",
            "metadata only capacity folder",
            _iteration.FolderId,
            ["capacity", "synthetic"],
            _iteration.PrincipalId,
            "correlation-seed-0001",
            _iteration.TaskId,
            "idempotency-seed-0001",
            PayloadTenantId: null);

        FolderResult created = FolderAggregate.Handle(FolderState.Empty, create, _timeProvider.GetUtcNow());
        FolderState createdState = FolderState.Empty.Apply(created.Events, streamName);
        CreateRepositoryBackedFolder bindRequest = new(
            _iteration.TenantId,
            _iteration.OrganizationId,
            _iteration.FolderId,
            "v1",
            _iteration.RepositoryBindingId,
            _iteration.ProviderBindingRef,
            "repository-profile-0001",
            _iteration.BranchRefPolicyRef,
            "Capacity Folder",
            "tenant_installation",
            _iteration.PrincipalId,
            "correlation-seed-0002",
            _iteration.TaskId,
            "idempotency-seed-0002",
            PayloadTenantId: null);
        FolderResult requested = FolderAggregate.Handle(createdState, bindRequest, _timeProvider.GetUtcNow());
        RepositoryBound bound = new(
            _iteration.TenantId,
            _iteration.OrganizationId,
            _iteration.FolderId,
            _iteration.RepositoryBindingId,
            _iteration.ProviderBindingRef,
            "correlation-seed-0003",
            _iteration.TaskId,
            "idempotency-seed-0003",
            "fingerprint-idempotency-seed-0003",
            _timeProvider.GetUtcNow());
        FolderState boundState = createdState.Apply([.. requested.Events, bound], streamName);
        ConfigureBranchRefPolicy configurePolicy = new(
            _iteration.TenantId,
            _iteration.OrganizationId,
            _iteration.FolderId,
            "v1",
            _iteration.RepositoryBindingId,
            _iteration.BranchRefPolicyRef,
            "branch_ref_primary",
            ["branch_ref_feature"],
            ["branch_ref_release"],
            _iteration.PrincipalId,
            "correlation-seed-0004",
            _iteration.TaskId,
            "idempotency-seed-0004",
            PayloadTenantId: null);
        FolderResult configured = FolderAggregate.Handle(boundState, configurePolicy, _timeProvider.GetUtcNow());

        _repository.Seed(streamName, [.. created.Events, .. requested.Events, bound, .. configured.Events]);
    }

    private EventStoreClaimTransformEvidence ClaimTransform(string actionToken)
        => EventStoreClaimTransformEvidence.Allowed(_iteration.TenantId, _iteration.PrincipalId, [actionToken]);

    private static LayeredFolderAuthorizationService CreateAuthorizationService(LifecycleCapacityIteration iteration)
        => new(
            new TenantAccessAuthorizer(
                TenantStore(iteration),
                new FixedUtcClock(BaselineTime),
                new TenantAccessOptions
                {
                    MutationFreshnessBudget = TimeSpan.FromMinutes(5),
                    DiagnosticStalenessBudget = TimeSpan.FromMinutes(5),
                }),
            new StaticFolderPermissionEvidenceProvider(iteration.OrganizationId),
            new StaticEventStoreAuthorizationValidator(),
            new StaticDaprPolicyEvidenceProvider(),
            new FixedUtcClock(BaselineTime));

    private static IFolderTenantAccessProjectionStore TenantStore(LifecycleCapacityIteration iteration)
    {
        InMemoryFolderTenantAccessProjectionStore store = new();
        store.SaveAsync(new FolderTenantAccessProjection
        {
            TenantId = iteration.TenantId,
            Enabled = true,
            Principals = new Dictionary<string, FolderTenantPrincipalEvidence>(StringComparer.Ordinal)
            {
                [iteration.PrincipalId] = new(iteration.PrincipalId, "Member"),
            },
            Watermark = 7,
            ProjectionWatermark = $"{iteration.TenantId}:7",
            LastEventTimestamp = BaselineTime.AddMinutes(-1),
        }).GetAwaiter().GetResult();
        return store;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
