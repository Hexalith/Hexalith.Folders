using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderWorkspaceLockReleaseServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 27, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MissingAuthenticationShouldDenyBeforeFolderObservation()
    {
        RecordingFolderRepository repository = new();
        WorkspaceLockReleaseService service = Service(repository);

        FolderResult result = await service.ReleaseAsync(
            Request(authoritativeTenantId: null),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.MissingAuthoritativeTenant);
        repository.StreamNamesConstructed.ShouldBe(0);
        repository.StreamsLoaded.ShouldBe(0);
        repository.IdempotencyLookups.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
    }

    [Fact]
    public async Task OwnedActiveLockShouldAppendReleaseEvent()
    {
        RecordingFolderRepository repository = LockedRepository();
        WorkspaceLockReleaseService service = Service(repository);

        FolderResult result = await service.ReleaseAsync(Request(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.Accepted);
        repository.IdempotencyLookups.ShouldBe(1);
        repository.AppendsAttempted.ShouldBe(1);
        repository.LastAppendedEvents.ShouldHaveSingleItem().ShouldBeOfType<WorkspaceLockReleased>();
        repository.Load(FolderStreamName.Create("tenant-a", "folder-a"))
            .WorkspaceLifecycleState
            .ShouldBe(FolderWorkspaceLifecycleState.Ready);
    }

    [Fact]
    public async Task IdempotencyLookupUnavailableShouldRejectBeforeAppend()
    {
        RecordingFolderRepository repository = LockedRepository();
        repository.IdempotencyUnavailable = true;
        WorkspaceLockReleaseService service = Service(repository);

        FolderResult result = await service.ReleaseAsync(Request(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.IdempotencyUnavailable);
        repository.IdempotencyLookups.ShouldBe(1);
        repository.AppendsAttempted.ShouldBe(0);
    }

    [Fact]
    public async Task AppendConflictShouldRereadAndSurfaceIdempotentReplayWhenReleaseWon()
    {
        RecordingFolderRepository repository = LockedRepository();
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState locked = repository.Load(streamName);
        FolderResult competing = FolderAggregate.Handle(locked, FolderCommandFactory.ReleaseWorkspaceLock(), Now.AddMinutes(1));
        repository.SimulateAppendConflict = true;
        repository.ConcurrentAppendEvents = competing.Events;
        WorkspaceLockReleaseService service = Service(repository);

        FolderResult result = await service.ReleaseAsync(Request(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.IdempotentReplay);
        repository.ConcurrentEventsApplied.ShouldBe(1);
        repository.AppendsAttempted.ShouldBe(1);
    }

    [Fact]
    public async Task WrongProofShouldRejectWithoutAppend()
    {
        RecordingFolderRepository repository = LockedRepository();
        WorkspaceLockReleaseService service = Service(repository);

        FolderResult result = await service.ReleaseAsync(
            Request(lockOwnershipProof: "lock_proof_wrong"),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.LockNotOwned);
        repository.AppendsAttempted.ShouldBe(0);
    }

    [Fact]
    public async Task ExpiredLockShouldRejectWithoutAppend()
    {
        RecordingFolderRepository repository = LockedRepository(lockAcquiredAt: Now.AddHours(-2));
        WorkspaceLockReleaseService service = Service(repository);

        FolderResult result = await service.ReleaseAsync(Request(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.LockExpired);
        repository.AppendsAttempted.ShouldBe(0);
    }

    private static WorkspaceLockReleaseService Service(IFolderRepository repository)
        => new(AuthorizationService(), repository, new FixedTimeProvider(Now));

    private static WorkspaceLockReleaseRequest Request(
        string? authoritativeTenantId = "tenant-a",
        string principalId = "user-a",
        string idempotencyKey = "idempotency-release-a",
        string? lockOwnershipProof = null)
    {
        string lockId = FolderCommandFactory.DefaultLockId();
        return new(
            authoritativeTenantId ?? string.Empty,
            principalId,
            string.IsNullOrWhiteSpace(authoritativeTenantId)
                ? EventStoreClaimTransformEvidence.Missing()
                : EventStoreClaimTransformEvidence.Allowed(
                    authoritativeTenantId,
                    principalId,
                    [WorkspaceLockReleaseService.ActionToken]),
            FolderId: "folder-a",
            RequestSchemaVersion: "v1",
            WorkspaceId: "workspace-a",
            LockId: lockId,
            LockOwnershipProof: lockOwnershipProof ?? FolderCommandValidator.DeriveWorkspaceLockOwnershipProof(
                "tenant-a",
                "folder-a",
                "workspace-a",
                "task-a",
                lockId),
            ReleaseReasonCode: "caller_completed",
            CorrelationId: "correlation-release-a",
            TaskId: "task-a",
            IdempotencyKey: idempotencyKey,
            PayloadTenantId: null,
            ClientControlledTenantValues: new Dictionary<string, string?>(StringComparer.Ordinal),
            ClientControlledPrincipalValues: new Dictionary<string, string?>(StringComparer.Ordinal));
    }

    private static RecordingFolderRepository LockedRepository(DateTimeOffset? lockAcquiredAt = null)
    {
        RecordingFolderRepository repository = ReadyRepository();
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState ready = repository.Load(streamName);
        FolderResult locked = FolderAggregate.Handle(ready, FolderCommandFactory.LockWorkspace(), lockAcquiredAt ?? Now);
        RecordingFolderRepository lockedRepository = new();
        lockedRepository.Seed(streamName, [.. SeedReadyEvents(), .. locked.Events]);
        return lockedRepository;
    }

    private static RecordingFolderRepository ReadyRepository()
    {
        RecordingFolderRepository repository = new();
        repository.Seed(FolderStreamName.Create("tenant-a", "folder-a"), SeedReadyEvents());
        return repository;
    }

    private static IReadOnlyList<IFolderEvent> SeedReadyEvents()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderResult created = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create(), Now);
        FolderState createdState = FolderState.Empty.Apply(created.Events, streamName);
        FolderResult requested = FolderAggregate.Handle(createdState, FolderCommandFactory.CreateRepositoryBackedFolder(), Now);
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
        FolderState boundState = createdState.Apply([.. requested.Events, bound], streamName);
        FolderResult configured = FolderAggregate.Handle(
            boundState,
            new ConfigureBranchRefPolicy(
                "tenant-a",
                "organization-a",
                "folder-a",
                "v1",
                "repository-binding-a",
                "branch-ref-policy-a",
                "branch_ref_primary",
                ["branch_ref_feature"],
                ["branch_ref_release"],
                "user-a",
                "correlation-policy-a",
                "task-a",
                "idempotency-policy-a",
                PayloadTenantId: null),
            Now);
        FolderState configuredState = boundState.Apply(configured.Events, streamName);
        FolderResult prepare = FolderAggregate.Handle(configuredState, FolderCommandFactory.PrepareWorkspace(), Now);
        FolderWorkspaceLifecycleEventRecorded prepared = new(
            "tenant-a",
            "organization-a",
            "folder-a",
            "workspace-a",
            FolderWorkspaceLifecycleEvent.WorkspacePrepared,
            DirtyResolution: null,
            OperationId: "workspace-a",
            "correlation-prepared-a",
            "task-a",
            "idempotency-workspace-outcome-a",
            "fingerprint-workspace-outcome-a",
            Now);

        return [.. created.Events, .. requested.Events, bound, .. configured.Events, .. prepare.Events, prepared];
    }

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

    private sealed class RecordingFolderPermissionEvidenceProvider : IFolderPermissionEvidenceProvider
    {
        public Task<FolderPermissionEvidenceResult> GetEvidenceAsync(
            FolderPermissionEvidenceRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(FolderPermissionEvidenceResult.Allowed(
                "folder-a:7",
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
