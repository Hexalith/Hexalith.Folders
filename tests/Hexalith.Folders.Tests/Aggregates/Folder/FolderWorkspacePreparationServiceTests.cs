using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Queries.ProviderReadiness;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderWorkspacePreparationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 27, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MissingAuthenticationShouldDenyBeforeFolderOrReadinessObservation()
    {
        RecordingFolderRepository repository = new();
        RecordingWorkspaceReadinessValidator readiness = new(ReadinessReady());
        WorkspacePreparationService service = Service(repository, readiness);

        FolderResult result = await service.PrepareAsync(
            Request(authoritativeTenantId: null),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.MissingAuthoritativeTenant);
        repository.StreamNamesConstructed.ShouldBe(0);
        repository.StreamsLoaded.ShouldBe(0);
        repository.IdempotencyLookups.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
        readiness.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task PayloadTenantMismatchShouldDenyBeforeFolderOrReadinessObservation()
    {
        RecordingFolderRepository repository = ConfiguredPreparingRepository();
        RecordingWorkspaceReadinessValidator readiness = new(ReadinessReady());
        WorkspacePreparationService service = Service(repository, readiness);

        FolderResult result = await service.PrepareAsync(
            Request(payloadTenantId: "tenant-b"),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.TenantAccessDenied);
        repository.StreamNamesConstructed.ShouldBe(0);
        repository.StreamsLoaded.ShouldBe(0);
        repository.IdempotencyLookups.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
        readiness.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task EquivalentReplayShouldReturnBeforeReadinessObservation()
    {
        RecordingFolderRepository repository = PreparedRepository();
        RecordingWorkspaceReadinessValidator readiness = new(ReadinessFailed(ProviderFailureCategory.ProviderUnavailable));
        WorkspacePreparationService service = Service(repository, readiness);

        FolderResult result = await service.PrepareAsync(Request(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.IdempotentReplay);
        repository.StreamsLoaded.ShouldBe(1);
        repository.IdempotencyLookups.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
        readiness.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task ReadyRequestShouldValidateWorkspacePreparationReadinessThenAppendIntentOnly()
    {
        RecordingFolderRepository repository = ConfiguredPreparingRepository();
        RecordingWorkspaceReadinessValidator readiness = new(ReadinessReady());
        WorkspacePreparationService service = Service(repository, readiness);

        FolderResult result = await service.PrepareAsync(Request(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.Accepted);
        repository.IdempotencyLookups.ShouldBe(1);
        repository.AppendsAttempted.ShouldBe(1);
        repository.LastAppendedEvents.ShouldHaveSingleItem().ShouldBeOfType<WorkspacePreparationRequested>();
        readiness.Calls.ShouldBe(1);
        readiness.LastRequest.ShouldNotBeNull().RequestedCapability.ShouldBe(ProviderReadinessRequestedCapability.WorkspacePreparation);
        readiness.LastRequest.ProviderBindingRef.ShouldBe("provider-binding-a");
    }

    [Fact]
    public async Task ReadinessUnknownOutcomeShouldRejectWithoutAppendingIntent()
    {
        RecordingFolderRepository repository = ConfiguredPreparingRepository();
        RecordingWorkspaceReadinessValidator readiness = new(ReadinessFailed(ProviderFailureCategory.UnknownProviderOutcome));
        WorkspacePreparationService service = Service(repository, readiness);

        FolderResult result = await service.PrepareAsync(Request(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.UnknownProviderOutcome);
        repository.AppendsAttempted.ShouldBe(1);
        FolderWorkspaceLifecycleEventRecorded recorded = repository.LastAppendedEvents
            .ShouldHaveSingleItem()
            .ShouldBeOfType<FolderWorkspaceLifecycleEventRecorded>();
        recorded.WorkspaceLifecycleEvent.ShouldBe(FolderWorkspaceLifecycleEvent.ProviderOutcomeUnknown);
        repository.Load(FolderStreamName.Create("tenant-a", "folder-a"))
            .WorkspaceLifecycleState
            .ShouldBe(FolderWorkspaceLifecycleState.UnknownProviderOutcome);
        readiness.Calls.ShouldBe(1);
    }

    [Theory]
    [InlineData(nameof(ProviderFailureCategory.ProviderUnavailable), nameof(FolderResultCode.ProviderUnavailable))]
    [InlineData(nameof(ProviderFailureCategory.ProviderTransientFailure), nameof(FolderResultCode.ProviderUnavailable))]
    [InlineData(nameof(ProviderFailureCategory.ProviderRateLimited), nameof(FolderResultCode.ProviderRateLimited))]
    [InlineData(nameof(ProviderFailureCategory.ReconciliationRequired), nameof(FolderResultCode.ReconciliationRequired))]
    [InlineData(nameof(ProviderFailureCategory.ProviderReadinessFailed), nameof(FolderResultCode.ProviderReadinessFailed))]
    public async Task ReadinessFailuresShouldMapToCanonicalResults(string failureCategoryName, string expectedCodeName)
    {
        ProviderFailureCategory failureCategory = Enum.Parse<ProviderFailureCategory>(failureCategoryName);
        FolderResultCode expectedCode = Enum.Parse<FolderResultCode>(expectedCodeName);
        RecordingFolderRepository repository = ConfiguredPreparingRepository();
        RecordingWorkspaceReadinessValidator readiness = new(ReadinessFailed(failureCategory));
        WorkspacePreparationService service = Service(repository, readiness);

        FolderResult result = await service.PrepareAsync(Request(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(expectedCode);
        repository.AppendsAttempted.ShouldBe(failureCategory == ProviderFailureCategory.ReconciliationRequired ? 1 : 0);
        readiness.Calls.ShouldBe(1);
    }

    private static WorkspacePreparationService Service(
        IFolderRepository repository,
        IWorkspacePreparationReadinessValidator readiness)
        => new(AuthorizationService(), readiness, repository, new FixedTimeProvider(Now));

    private static WorkspacePreparationRequest Request(
        string? authoritativeTenantId = "tenant-a",
        string principalId = "user-a",
        string idempotencyKey = "idempotency-workspace-a",
        string? payloadTenantId = null)
        => new(
            authoritativeTenantId ?? string.Empty,
            principalId,
            string.IsNullOrWhiteSpace(authoritativeTenantId)
                ? EventStoreClaimTransformEvidence.Missing()
                : EventStoreClaimTransformEvidence.Allowed(
                    authoritativeTenantId,
                    principalId,
                    [WorkspacePreparationService.ActionToken, ProviderReadinessValidationService.ReadActionToken]),
            FolderId: "folder-a",
            RequestSchemaVersion: "v1",
            WorkspaceId: "workspace-a",
            RepositoryBindingId: "repository-binding-a",
            BranchRefPolicyRef: "branch-ref-policy-a",
            WorkspacePolicyRef: "workspace-policy-a",
            CorrelationId: "correlation-a",
            TaskId: "task-a",
            IdempotencyKey: idempotencyKey,
            PayloadTenantId: payloadTenantId,
            ClientControlledTenantValues: new Dictionary<string, string?>(StringComparer.Ordinal),
            ClientControlledPrincipalValues: new Dictionary<string, string?>(StringComparer.Ordinal));

    private static RecordingFolderRepository PreparedRepository()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState state = ConfiguredPreparingState(streamName);
        FolderResult prepared = FolderAggregate.Handle(state, FolderCommandFactory.PrepareWorkspace(), Now);
        RecordingFolderRepository repository = new();
        repository.Seed(streamName, [.. SeedEvents(), .. prepared.Events]);
        return repository;
    }

    private static RecordingFolderRepository ConfiguredPreparingRepository()
    {
        RecordingFolderRepository repository = new();
        repository.Seed(FolderStreamName.Create("tenant-a", "folder-a"), SeedEvents());
        return repository;
    }

    private static FolderState ConfiguredPreparingState(FolderStreamName streamName)
        => FolderState.Empty.Apply(SeedEvents(), streamName);

    private static IReadOnlyList<IFolderEvent> SeedEvents()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderResult created = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create());
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

        return [.. created.Events, .. requested.Events, bound, .. configured.Events];
    }

    private static ProviderReadinessValidationResult ReadinessReady()
        => Readiness("ready", ProviderFailureCategory.None);

    private static ProviderReadinessValidationResult ReadinessFailed(ProviderFailureCategory category)
        => Readiness("failed", category);

    private static ProviderReadinessValidationResult Readiness(string status, ProviderFailureCategory category)
        => new(
            ProviderReadinessResultCode.Allowed,
            status,
            category == ProviderFailureCategory.None ? "success" : category.ToCategoryCode(),
            category == ProviderFailureCategory.None ? "none" : $"{category.ToCategoryCode()}_remediation",
            Retryable: false,
            RetryAfter: null,
            RemediationCategory: category == ProviderFailureCategory.None ? "none" : "contact_operator",
            CorrelationId: "correlation-a",
            ProviderReference: "provider-binding-a",
            ProviderBindingRef: "provider-binding-a",
            CapabilityProfileRef: "profile-a",
            Evidence: null,
            new ProviderReadinessFreshness("snapshot_per_task", Now, "tenant-a:7", Stale: false),
            category,
            category.ToCategoryCode());

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

    private sealed class RecordingWorkspaceReadinessValidator(ProviderReadinessValidationResult result)
        : IWorkspacePreparationReadinessValidator
    {
        public int Calls { get; private set; }

        public ProviderReadinessValidationRequest? LastRequest { get; private set; }

        public Task<ProviderReadinessValidationResult> ValidateAsync(
            ProviderReadinessValidationRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRequest = request;
            return Task.FromResult(result);
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
