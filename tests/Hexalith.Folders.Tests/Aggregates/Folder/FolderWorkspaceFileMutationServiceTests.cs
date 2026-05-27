using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderWorkspaceFileMutationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 27, 22, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MissingAuthenticationShouldDenyBeforePathPolicyAndFolderObservation()
    {
        RecordingFolderRepository repository = new();
        RecordingPathPolicyEvidenceProvider evidence = new();
        WorkspaceFileMutationService service = Service(repository, evidence);

        FolderResult result = await service.MutateAsync(
            Request(authoritativeTenantId: null),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.MissingAuthoritativeTenant);
        evidence.Requests.ShouldBe(0);
        repository.StreamsLoaded.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
    }

    [Fact]
    public async Task SyntacticPathPolicyShouldDenyBeforeEvidenceStreamLoadAndAppend()
    {
        RecordingFolderRepository repository = LockedRepository();
        RecordingPathPolicyEvidenceProvider evidence = new();
        WorkspaceFileMutationService service = Service(repository, evidence);

        FolderResult result = await service.MutateAsync(
            Request(normalizedPath: "../secret.txt"),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.PathPolicyDenied);
        evidence.Requests.ShouldBe(0);
        repository.StreamsLoaded.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
    }

    [Theory]
    [InlineData(WorkspacePathPolicyEvidenceDecision.SymlinkEscape, FolderResultCode.PathPolicyDenied)]
    [InlineData(WorkspacePathPolicyEvidenceDecision.Unavailable, FolderResultCode.PolicyEvidenceUnavailable)]
    public async Task EvidenceDenialsShouldRejectBeforeStreamLoadAndAppend(
        WorkspacePathPolicyEvidenceDecision decision,
        FolderResultCode expectedCode)
    {
        RecordingFolderRepository repository = LockedRepository();
        RecordingPathPolicyEvidenceProvider evidence = new(decision);
        RecordingContentStore contentStore = new();
        WorkspaceFileMutationService service = Service(repository, evidence, contentStore);

        FolderResult result = await service.MutateAsync(Request(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(expectedCode);
        repository.StreamsLoaded.ShouldBe(1);
        contentStore.Requests.ShouldBeEmpty();
        repository.AppendsAttempted.ShouldBe(0);
    }

    [Fact]
    public async Task AcceptedPathPolicyShouldAppendMetadataOnlyMutationEvent()
    {
        RecordingFolderRepository repository = LockedRepository();
        RecordingPathPolicyEvidenceProvider evidence = new();
        RecordingContentStore contentStore = new();
        WorkspaceFileMutationService service = Service(repository, evidence, contentStore);

        FolderResult result = await service.MutateAsync(Request(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.Accepted);
        evidence.Requests.ShouldBe(1);
        contentStore.Requests.Count.ShouldBe(1);
        repository.IdempotencyLookups.ShouldBe(1);
        repository.AppendsAttempted.ShouldBe(1);
        WorkspaceFileMutationAccepted accepted = repository.LastAppendedEvents.ShouldHaveSingleItem().ShouldBeOfType<WorkspaceFileMutationAccepted>();
        accepted.MediaType.ShouldBe("text/plain");
        accepted.TransportEvidenceKind.ShouldBe("inline_decoded");
        accepted.ObservedByteLength.ShouldBe(12);
    }

    [Theory]
    [InlineData("workspace-b", "task-a", FolderResultCode.StateTransitionInvalid)]
    [InlineData("workspace-a", "task-b", FolderResultCode.LockNotOwned)]
    public async Task AggregateRejectionsShouldNotAppend(string workspaceId, string taskId, FolderResultCode expectedCode)
    {
        RecordingFolderRepository repository = LockedRepository();
        RecordingPathPolicyEvidenceProvider evidence = new();
        RecordingContentStore contentStore = new();
        WorkspaceFileMutationService service = Service(repository, evidence, contentStore);

        FolderResult result = await service.MutateAsync(
            Request(workspaceId: workspaceId, taskId: taskId),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(expectedCode);
        evidence.Requests.ShouldBe(0);
        contentStore.Requests.ShouldBeEmpty();
        repository.AppendsAttempted.ShouldBe(0);
    }

    [Fact]
    public async Task EquivalentReplayShouldReturnBeforeDuplicatePathEvidence()
    {
        RecordingFolderRepository repository = LockedRepository();
        RecordingPathPolicyEvidenceProvider evidence = new();
        RecordingContentStore contentStore = new();
        WorkspaceFileMutationService service = Service(repository, evidence, contentStore);

        FolderResult first = await service.MutateAsync(Request(), TestContext.Current.CancellationToken);
        evidence.Decision = WorkspacePathPolicyEvidenceDecision.Unavailable;

        FolderResult replay = await service.MutateAsync(Request(), TestContext.Current.CancellationToken);

        first.Code.ShouldBe(FolderResultCode.Accepted);
        replay.Code.ShouldBe(FolderResultCode.IdempotentReplay);
        evidence.Requests.ShouldBe(1);
        contentStore.Requests.Count.ShouldBe(1);
        repository.AppendsAttempted.ShouldBe(1);
    }

    [Fact]
    public async Task IdempotencyConflictShouldReturnBeforeContentStaging()
    {
        RecordingFolderRepository repository = LockedRepository();
        RecordingPathPolicyEvidenceProvider evidence = new();
        RecordingContentStore contentStore = new();
        WorkspaceFileMutationService service = Service(repository, evidence, contentStore);

        FolderResult first = await service.MutateAsync(Request(), TestContext.Current.CancellationToken);

        FolderResult conflict = await service.MutateAsync(
            Request(operationId: "operation-b"),
            TestContext.Current.CancellationToken);

        first.Code.ShouldBe(FolderResultCode.Accepted);
        conflict.Code.ShouldBe(FolderResultCode.IdempotencyConflict);
        evidence.Requests.ShouldBe(1);
        contentStore.Requests.Count.ShouldBe(1);
        repository.AppendsAttempted.ShouldBe(1);
    }

    [Fact]
    public async Task ContentStoreUnavailableShouldFailBeforeAppend()
    {
        RecordingFolderRepository repository = LockedRepository();
        RecordingPathPolicyEvidenceProvider evidence = new();
        RecordingContentStore contentStore = new() { Accepted = false };
        WorkspaceFileMutationService service = Service(repository, evidence, contentStore);

        FolderResult result = await service.MutateAsync(Request(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.FileOperationFailed);
        evidence.Requests.ShouldBe(1);
        contentStore.Requests.Count.ShouldBe(1);
        repository.AppendsAttempted.ShouldBe(0);
    }

    private static WorkspaceFileMutationService Service(
        IFolderRepository repository,
        IWorkspacePathPolicyEvidenceProvider evidence,
        IWorkspaceFileContentStore? contentStore = null)
        => new(AuthorizationService(), repository, evidence, new FixedTimeProvider(Now), contentStore ?? new RecordingContentStore());

    private static WorkspaceFileMutationRequest Request(
        string? authoritativeTenantId = "tenant-a",
        string workspaceId = "workspace-a",
        string operationId = "operation-a",
        string taskId = "task-a",
        string normalizedPath = "docs/readme.md")
        => new(
            authoritativeTenantId ?? string.Empty,
            PrincipalId: "user-a",
            string.IsNullOrWhiteSpace(authoritativeTenantId)
                ? EventStoreClaimTransformEvidence.Missing()
                : EventStoreClaimTransformEvidence.Allowed(authoritativeTenantId, "user-a", [WorkspaceFileMutationService.ActionToken]),
            FolderId: "folder-a",
            RequestSchemaVersion: "v1",
            WorkspaceId: workspaceId,
            OperationId: operationId,
            FileOperationKind: "add",
            TransportOperation: "PutFileInline",
            new PathMetadata(normalizedPath, "readme.md", "tenant_sensitive_document", "NFC"),
            ContentHashReference: "hashref-a",
            ByteLength: 12,
            MediaType: "text/plain",
            TransportEvidenceKind: "inline_decoded",
            ObservedByteLength: 12,
            CorrelationId: "correlation-file-a",
            TaskId: taskId,
            IdempotencyKey: "idempotency-file-a",
            PayloadTenantId: null,
            ClientControlledTenantValues: new Dictionary<string, string?>(StringComparer.Ordinal),
            ClientControlledPrincipalValues: new Dictionary<string, string?>(StringComparer.Ordinal));

    private static RecordingFolderRepository LockedRepository()
    {
        RecordingFolderRepository repository = new();
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        repository.Seed(streamName, SeedReadyEvents());
        FolderState ready = repository.Load(streamName);
        repository = new RecordingFolderRepository();
        repository.Seed(streamName, [.. SeedReadyEvents(), .. FolderAggregate.Handle(ready, FolderCommandFactory.LockWorkspace(), Now).Events]);
        return repository;
    }

    private static IReadOnlyList<IFolderEvent> SeedReadyEvents()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderResult created = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create(), Now);
        FolderState createdState = FolderState.Empty.Apply(created.Events, streamName);
        FolderResult requested = FolderAggregate.Handle(createdState, FolderCommandFactory.CreateRepositoryBackedFolder(), Now);
        FolderState boundState = createdState.Apply(
            [
                .. requested.Events,
                new RepositoryBound(
                    "tenant-a",
                    "organization-a",
                    "folder-a",
                    "repository-binding-a",
                    "provider-binding-a",
                    "correlation-bound-a",
                    "task-bound-a",
                    "idempotency-bound-a",
                    "fingerprint-bound-a",
                    Now),
            ],
            streamName);
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

        return
        [
            .. created.Events,
            .. requested.Events,
            new RepositoryBound(
                "tenant-a",
                "organization-a",
                "folder-a",
                "repository-binding-a",
                "provider-binding-a",
                "correlation-bound-a",
                "task-bound-a",
                "idempotency-bound-a",
                "fingerprint-bound-a",
                Now),
            .. configured.Events,
            .. prepare.Events,
            new FolderWorkspaceLifecycleEventRecorded(
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
                Now),
        ];
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

    private sealed class RecordingPathPolicyEvidenceProvider(
        WorkspacePathPolicyEvidenceDecision decision = WorkspacePathPolicyEvidenceDecision.NoEscape)
        : IWorkspacePathPolicyEvidenceProvider
    {
        public WorkspacePathPolicyEvidenceDecision Decision { get; set; } = decision;

        public int Requests { get; private set; }

        public Task<WorkspacePathPolicyEvidenceResult> GetEvidenceAsync(
            WorkspacePathPolicyEvidenceRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests++;
            return Task.FromResult(new WorkspacePathPolicyEvidenceResult(Decision));
        }
    }

    private sealed class RecordingContentStore : IWorkspaceFileContentStore
    {
        public bool Accepted { get; init; } = true;

        public List<WorkspaceFileContentStoreRequest> Requests { get; } = [];

        public Task<WorkspaceFileContentStoreResult> StageAsync(
            WorkspaceFileContentStoreRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            Requests.Add(request);
            return Task.FromResult(Accepted
                ? WorkspaceFileContentStoreResult.Succeeded
                : WorkspaceFileContentStoreResult.Failed);
        }
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
