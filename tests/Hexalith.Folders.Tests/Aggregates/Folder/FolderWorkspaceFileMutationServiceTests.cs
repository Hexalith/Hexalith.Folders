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
    [InlineData("../secret.txt")]
    [InlineData("/absolute.txt")]
    [InlineData("docs\\mixed.txt")]
    [InlineData("docs/%2e%2e/secret.txt")]
    [InlineData("docs%2fsecret.txt")]
    [InlineData("docs/NUL.md")]
    [InlineData("docs/")]
    [InlineData("docs/name .txt")]
    [InlineData("docs/readme\u200b.md")]
    [InlineData("docs/cafe\u0301.txt")]
    public async Task UnsafePathInputsShouldDenyBeforeLifecycleSideEffectsAndNotEchoPath(string normalizedPath)
    {
        RecordingFolderRepository repository = LockedRepository();
        RecordingPathPolicyEvidenceProvider evidence = new();
        RecordingContentStore contentStore = new();
        RecordingDeleteOperationStore deleteStore = new();
        WorkspaceFileMutationService service = Service(repository, evidence, contentStore, deleteStore);

        FolderResult result = await service.MutateAsync(
            Request(normalizedPath: normalizedPath),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.PathPolicyDenied);
        evidence.Requests.ShouldBe(0);
        contentStore.Requests.ShouldBeEmpty();
        deleteStore.Requests.ShouldBeEmpty();
        repository.StreamNamesConstructed.ShouldBe(0);
        repository.StreamsLoaded.ShouldBe(0);
        repository.IdempotencyLookups.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
        result.ToString().ShouldNotContain(normalizedPath, Case.Sensitive);
    }

    [Fact]
    public async Task MalformedIdempotencyKeyShouldFailBeforePathEvidenceAndSideEffects()
    {
        RecordingFolderRepository repository = LockedRepository();
        RecordingPathPolicyEvidenceProvider evidence = new();
        RecordingContentStore contentStore = new();
        WorkspaceFileMutationService service = Service(repository, evidence, contentStore);

        FolderResult result = await service.MutateAsync(
            Request(idempotencyKey: "synthetic-key-\u0001"),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.MalformedEvidence);
        evidence.Requests.ShouldBe(0);
        contentStore.Requests.ShouldBeEmpty();
        repository.StreamNamesConstructed.ShouldBe(0);
        repository.StreamsLoaded.ShouldBe(0);
        repository.IdempotencyLookups.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
    }

    [Fact]
    public async Task PayloadTenantMismatchShouldDenyBeforePathEvidenceAndRepositoryObservation()
    {
        RecordingFolderRepository repository = LockedRepository();
        RecordingPathPolicyEvidenceProvider evidence = new();
        RecordingContentStore contentStore = new();
        WorkspaceFileMutationService service = Service(repository, evidence, contentStore);

        FolderResult result = await service.MutateAsync(
            Request(payloadTenantId: "tenant-b"),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.TenantAccessDenied);
        evidence.Requests.ShouldBe(0);
        contentStore.Requests.ShouldBeEmpty();
        repository.StreamNamesConstructed.ShouldBe(0);
        repository.StreamsLoaded.ShouldBe(0);
        repository.IdempotencyLookups.ShouldBe(0);
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

    [Fact]
    public async Task RemoveShouldOrderDeleteOperationAfterEvidenceAndIdempotencyLookupBeforeAppend()
    {
        RecordingFolderRepository repository = LockedRepository();
        RecordingPathPolicyEvidenceProvider evidence = new();
        RecordingContentStore contentStore = new();
        RecordingDeleteOperationStore deleteStore = new(() => repository.IdempotencyLookups);
        WorkspaceFileMutationService service = Service(repository, evidence, contentStore, deleteStore);

        FolderResult result = await service.MutateAsync(
            Request(fileOperationKind: "remove"),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.Accepted);
        evidence.Requests.ShouldBe(1);
        repository.IdempotencyLookups.ShouldBe(1);
        contentStore.Requests.ShouldBeEmpty();
        WorkspaceFileDeleteOperationStoreRequest deleteRequest = deleteStore.Requests.ShouldHaveSingleItem();
        deleteStore.LookupCountsAtRequest.ShouldHaveSingleItem().ShouldBe(1);
        deleteRequest.ManagedTenantId.ShouldBe("tenant-a");
        deleteRequest.FolderId.ShouldBe("folder-a");
        deleteRequest.WorkspaceId.ShouldBe("workspace-a");
        deleteRequest.TaskId.ShouldBe("task-a");
        deleteRequest.OperationId.ShouldBe("operation-a");
        deleteRequest.FileOperationKind.ShouldBe("remove");
        deleteRequest.TransportOperation.ShouldBe("metadataOnlyRemoval");
        deleteRequest.PathMetadataDigest.ShouldNotBeNullOrWhiteSpace();
        deleteRequest.PathPolicyClass.ShouldBe("tenant_sensitive_document");
        repository.AppendsAttempted.ShouldBe(1);
    }

    [Fact]
    public async Task RemoveEquivalentReplayShouldReturnBeforeDuplicateDeleteOrdering()
    {
        RecordingFolderRepository repository = LockedRepository();
        RecordingPathPolicyEvidenceProvider evidence = new();
        RecordingDeleteOperationStore deleteStore = new();
        WorkspaceFileMutationService service = Service(repository, evidence, deleteOperationStore: deleteStore);

        FolderResult first = await service.MutateAsync(Request(fileOperationKind: "remove"), TestContext.Current.CancellationToken);
        evidence.Decision = WorkspacePathPolicyEvidenceDecision.Unavailable;

        FolderResult replay = await service.MutateAsync(Request(fileOperationKind: "remove"), TestContext.Current.CancellationToken);

        first.Code.ShouldBe(FolderResultCode.Accepted);
        replay.Code.ShouldBe(FolderResultCode.IdempotentReplay);
        evidence.Requests.ShouldBe(1);
        deleteStore.Requests.Count.ShouldBe(1);
        repository.AppendsAttempted.ShouldBe(1);
    }

    [Fact]
    public async Task RemoveIdempotencyConflictShouldReturnBeforeDeleteOrdering()
    {
        RecordingFolderRepository repository = LockedRepository();
        RecordingPathPolicyEvidenceProvider evidence = new();
        RecordingDeleteOperationStore deleteStore = new();
        WorkspaceFileMutationService service = Service(repository, evidence, deleteOperationStore: deleteStore);

        FolderResult first = await service.MutateAsync(Request(fileOperationKind: "remove"), TestContext.Current.CancellationToken);

        FolderResult conflict = await service.MutateAsync(
            Request(fileOperationKind: "remove", operationId: "operation-b"),
            TestContext.Current.CancellationToken);

        first.Code.ShouldBe(FolderResultCode.Accepted);
        conflict.Code.ShouldBe(FolderResultCode.IdempotencyConflict);
        evidence.Requests.ShouldBe(1);
        deleteStore.Requests.Count.ShouldBe(1);
        repository.AppendsAttempted.ShouldBe(1);
    }

    [Fact]
    public async Task RemoveDeleteOrderUnavailableShouldFailBeforeAppend()
    {
        RecordingFolderRepository repository = LockedRepository();
        RecordingPathPolicyEvidenceProvider evidence = new();
        RecordingDeleteOperationStore deleteStore = new() { Accepted = false };
        WorkspaceFileMutationService service = Service(repository, evidence, deleteOperationStore: deleteStore);

        FolderResult result = await service.MutateAsync(Request(fileOperationKind: "remove"), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.FileOperationFailed);
        deleteStore.Requests.Count.ShouldBe(1);
        repository.AppendsAttempted.ShouldBe(0);
    }

    [Fact]
    public async Task RemoveDefaultDeleteOrderBoundaryShouldFailClosedBeforeAppend()
    {
        RecordingFolderRepository repository = LockedRepository();
        RecordingPathPolicyEvidenceProvider evidence = new();
        WorkspaceFileMutationService service = new(
            AuthorizationService(),
            repository,
            evidence,
            new FixedTimeProvider(Now),
            new RecordingContentStore());

        FolderResult result = await service.MutateAsync(Request(fileOperationKind: "remove"), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.FileOperationFailed);
        repository.AppendsAttempted.ShouldBe(0);
    }

    [Theory]
    [InlineData(WorkspacePathPolicyEvidenceDecision.SymlinkEscape, FolderResultCode.PathPolicyDenied)]
    [InlineData(WorkspacePathPolicyEvidenceDecision.Unavailable, FolderResultCode.PolicyEvidenceUnavailable)]
    public async Task RemovePathEvidenceDenialShouldNotOrderDeleteOrAppend(
        WorkspacePathPolicyEvidenceDecision decision,
        FolderResultCode expectedCode)
    {
        RecordingFolderRepository repository = LockedRepository();
        RecordingPathPolicyEvidenceProvider evidence = new(decision);
        RecordingDeleteOperationStore deleteStore = new();
        WorkspaceFileMutationService service = Service(repository, evidence, deleteOperationStore: deleteStore);

        FolderResult result = await service.MutateAsync(Request(fileOperationKind: "remove"), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(expectedCode);
        deleteStore.Requests.ShouldBeEmpty();
        repository.AppendsAttempted.ShouldBe(0);
    }

    [Fact]
    public async Task RemoveIdempotencyUnavailableShouldNotOrderDeleteOrAppend()
    {
        RecordingFolderRepository repository = LockedRepository();
        repository.IdempotencyUnavailable = true;
        RecordingPathPolicyEvidenceProvider evidence = new();
        RecordingDeleteOperationStore deleteStore = new();
        WorkspaceFileMutationService service = Service(repository, evidence, deleteOperationStore: deleteStore);

        FolderResult result = await service.MutateAsync(Request(fileOperationKind: "remove"), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.IdempotencyUnavailable);
        deleteStore.Requests.ShouldBeEmpty();
        repository.AppendsAttempted.ShouldBe(0);
    }

    [Theory]
    [InlineData(null, "workspace-a", "task-a", FolderResultCode.MissingAuthoritativeTenant)]
    [InlineData("tenant-a", "workspace-b", "task-a", FolderResultCode.StateTransitionInvalid)]
    [InlineData("tenant-a", "workspace-a", "task-b", FolderResultCode.LockNotOwned)]
    public async Task RemoveDenialsShouldNotOrderDeleteOrAppend(
        string? authoritativeTenantId,
        string workspaceId,
        string taskId,
        FolderResultCode expectedCode)
    {
        RecordingFolderRepository repository = LockedRepository();
        RecordingPathPolicyEvidenceProvider evidence = new();
        RecordingDeleteOperationStore deleteStore = new();
        WorkspaceFileMutationService service = Service(repository, evidence, deleteOperationStore: deleteStore);

        FolderResult result = await service.MutateAsync(
            Request(
                authoritativeTenantId: authoritativeTenantId,
                workspaceId: workspaceId,
                taskId: taskId,
                fileOperationKind: "remove"),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(expectedCode);
        deleteStore.Requests.ShouldBeEmpty();
        repository.AppendsAttempted.ShouldBe(0);
    }

    private static WorkspaceFileMutationService Service(
        IFolderRepository repository,
        IWorkspacePathPolicyEvidenceProvider evidence,
        IWorkspaceFileContentStore? contentStore = null,
        IWorkspaceFileDeleteOperationStore? deleteOperationStore = null)
        => new(
            AuthorizationService(),
            repository,
            evidence,
            new FixedTimeProvider(Now),
            contentStore ?? new RecordingContentStore(),
            deleteOperationStore ?? new RecordingDeleteOperationStore());

    private static WorkspaceFileMutationRequest Request(
        string? authoritativeTenantId = "tenant-a",
        string workspaceId = "workspace-a",
        string operationId = "operation-a",
        string taskId = "task-a",
        string normalizedPath = "docs/readme.md",
        string fileOperationKind = "add",
        string? transportOperation = null,
        string? contentHashReference = null,
        long? byteLength = null,
        string idempotencyKey = "idempotency-file-a",
        string? payloadTenantId = null)
    {
        transportOperation ??= fileOperationKind == "remove" ? "metadataOnlyRemoval" : "PutFileInline";
        contentHashReference ??= fileOperationKind == "remove" ? null : "hashref-a";
        byteLength ??= fileOperationKind == "remove" ? null : 12;

        return new(
            authoritativeTenantId ?? string.Empty,
            PrincipalId: "user-a",
            string.IsNullOrWhiteSpace(authoritativeTenantId)
                ? EventStoreClaimTransformEvidence.Missing()
                : EventStoreClaimTransformEvidence.Allowed(authoritativeTenantId, "user-a", [WorkspaceFileMutationService.ActionToken]),
            FolderId: "folder-a",
            RequestSchemaVersion: "v1",
            WorkspaceId: workspaceId,
            OperationId: operationId,
            FileOperationKind: fileOperationKind,
            TransportOperation: transportOperation,
            new PathMetadata(normalizedPath, "readme.md", "tenant_sensitive_document", "NFC"),
            ContentHashReference: contentHashReference,
            ByteLength: byteLength,
            MediaType: fileOperationKind is "add" or "change" ? "text/plain" : null,
            TransportEvidenceKind: fileOperationKind is "add" or "change" ? "inline_decoded" : null,
            ObservedByteLength: fileOperationKind is "add" or "change" ? byteLength : null,
            CorrelationId: "correlation-file-a",
            TaskId: taskId,
            IdempotencyKey: idempotencyKey,
            PayloadTenantId: payloadTenantId,
            ClientControlledTenantValues: new Dictionary<string, string?>(StringComparer.Ordinal),
            ClientControlledPrincipalValues: new Dictionary<string, string?>(StringComparer.Ordinal));
    }

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

    private sealed class RecordingDeleteOperationStore(Func<int>? idempotencyLookupCount = null) : IWorkspaceFileDeleteOperationStore
    {
        public bool Accepted { get; init; } = true;

        public List<WorkspaceFileDeleteOperationStoreRequest> Requests { get; } = [];

        public List<int> LookupCountsAtRequest { get; } = [];

        public Task<WorkspaceFileDeleteOperationStoreResult> StageAsync(
            WorkspaceFileDeleteOperationStoreRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            Requests.Add(request);
            LookupCountsAtRequest.Add(idempotencyLookupCount?.Invoke() ?? -1);
            return Task.FromResult(Accepted
                ? WorkspaceFileDeleteOperationStoreResult.Succeeded
                : WorkspaceFileDeleteOperationStoreResult.Failed);
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
