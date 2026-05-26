using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Queries.ProviderReadiness;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderRepositoryBackedCreationGateTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 26, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MissingAuthenticationShouldDenyBeforeFolderOrReadinessObservation()
    {
        RecordingFolderRepository repository = new();
        RecordingRepositoryCreationReadinessValidator readiness = new(ReadinessReady());
        RepositoryBackedFolderCreationService service = Service(repository, readiness);

        FolderResult result = await service.CreateAsync(
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
    public async Task ArchivedFolderShouldRejectBeforeReadinessOrIdempotencyObservation()
    {
        RecordingFolderRepository repository = ArchivedRepository();
        RecordingRepositoryCreationReadinessValidator readiness = new(ReadinessReady());
        RepositoryBackedFolderCreationService service = Service(repository, readiness);

        FolderResult result = await service.CreateAsync(
            Request(idempotencyKey: "idempotency-binding-b"),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.StateTransitionInvalid);
        repository.StreamNamesConstructed.ShouldBe(1);
        repository.StreamsLoaded.ShouldBe(1);
        repository.IdempotencyLookups.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
        readiness.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task PayloadTenantMismatchShouldDenyBeforeFolderOrReadinessObservation()
    {
        RecordingFolderRepository repository = SeededRepository();
        RecordingRepositoryCreationReadinessValidator readiness = new(ReadinessReady());
        RepositoryBackedFolderCreationService service = Service(repository, readiness);

        FolderResult result = await service.CreateAsync(
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
    public async Task FailedReadinessShouldRejectBeforeIdempotencyOrAppend()
    {
        RecordingFolderRepository repository = SeededRepository();
        RecordingRepositoryCreationReadinessValidator readiness = new(ReadinessFailed());
        RepositoryBackedFolderCreationService service = Service(repository, readiness);

        FolderResult result = await service.CreateAsync(
            Request(),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.ProviderReadinessFailed);
        repository.StreamNamesConstructed.ShouldBe(1);
        repository.StreamsLoaded.ShouldBe(1);
        repository.IdempotencyLookups.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
        readiness.Calls.ShouldBe(1);
        readiness.LastRequest.ShouldNotBeNull().RequestedCapability.ShouldBe(ProviderReadinessRequestedCapability.RepositoryCreation);
    }

    [Theory]
    [InlineData(nameof(ProviderReadinessResultCode.ProjectionStale), nameof(ProviderFailureCategory.ProviderReadinessFailed), "failed", nameof(FolderResultCode.StaleProjection))]
    [InlineData(nameof(ProviderReadinessResultCode.ProjectionUnavailable), nameof(ProviderFailureCategory.ProviderReadinessFailed), "failed", nameof(FolderResultCode.UnavailableProjection))]
    [InlineData(nameof(ProviderReadinessResultCode.ReadModelUnavailable), nameof(ProviderFailureCategory.ProviderReadinessFailed), "failed", nameof(FolderResultCode.UnavailableProjection))]
    [InlineData(nameof(ProviderReadinessResultCode.AuthenticationRequired), nameof(ProviderFailureCategory.ProviderAuthenticationRequired), "failed", nameof(FolderResultCode.TenantAccessDenied))]
    [InlineData(nameof(ProviderReadinessResultCode.AuthorizationDenied), nameof(ProviderFailureCategory.ProviderPermissionInsufficient), "failed", nameof(FolderResultCode.TenantAccessDenied))]
    [InlineData(nameof(ProviderReadinessResultCode.Allowed), nameof(ProviderFailureCategory.UnsupportedProviderCapability), "failed", nameof(FolderResultCode.UnsupportedProviderCapability))]
    [InlineData(nameof(ProviderReadinessResultCode.Allowed), nameof(ProviderFailureCategory.UnknownProviderOutcome), "failed", nameof(FolderResultCode.UnknownProviderOutcome))]
    [InlineData(nameof(ProviderReadinessResultCode.Allowed), nameof(ProviderFailureCategory.ReconciliationRequired), "failed", nameof(FolderResultCode.ReconciliationRequired))]
    [InlineData(nameof(ProviderReadinessResultCode.Allowed), nameof(ProviderFailureCategory.ProviderUnavailable), "degraded", nameof(FolderResultCode.ProviderUnavailable))]
    [InlineData(nameof(ProviderReadinessResultCode.Allowed), nameof(ProviderFailureCategory.ProviderRateLimited), "degraded", nameof(FolderResultCode.ProviderUnavailable))]
    [InlineData(nameof(ProviderReadinessResultCode.Allowed), nameof(ProviderFailureCategory.ProviderPermissionInsufficient), "failed", nameof(FolderResultCode.ProviderPermissionInsufficient))]
    [InlineData(nameof(ProviderReadinessResultCode.Allowed), nameof(ProviderFailureCategory.ProviderConflict), "failed", nameof(FolderResultCode.RepositoryConflict))]
    public async Task ReadinessFailuresShouldMapToCanonicalFolderResultBeforeIdempotencyOrAppend(
        string readinessCodeName,
        string failureCategoryName,
        string status,
        string expectedResultName)
    {
        RecordingFolderRepository repository = SeededRepository();
        ProviderReadinessResultCode readinessCode = Enum.Parse<ProviderReadinessResultCode>(readinessCodeName);
        ProviderFailureCategory failureCategory = Enum.Parse<ProviderFailureCategory>(failureCategoryName);
        FolderResultCode expectedResult = Enum.Parse<FolderResultCode>(expectedResultName);
        RecordingRepositoryCreationReadinessValidator readiness = new(
            Readiness(readinessCode, status, failureCategory));
        RepositoryBackedFolderCreationService service = Service(repository, readiness);

        FolderResult result = await service.CreateAsync(
            Request(),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(expectedResult);
        repository.IdempotencyLookups.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
        readiness.Calls.ShouldBe(1);
        readiness.LastRequest.ShouldNotBeNull().RequestedCapability.ShouldBe(ProviderReadinessRequestedCapability.RepositoryCreation);
    }

    [Fact]
    public async Task RepositoryBindingMutationAlreadyInProgressShouldRejectBeforeReadinessOrIdempotencyObservation()
    {
        RecordingFolderRepository repository = RequestedRepository();
        RecordingRepositoryCreationReadinessValidator readiness = new(ReadinessReady());
        RepositoryBackedFolderCreationService service = Service(repository, readiness);

        FolderResult result = await service.CreateAsync(
            Request(idempotencyKey: "idempotency-binding-b"),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.StateTransitionInvalid);
        repository.StreamNamesConstructed.ShouldBe(1);
        repository.StreamsLoaded.ShouldBe(1);
        repository.IdempotencyLookups.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
        readiness.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task EquivalentRepositoryBindingReplayShouldReturnReplayBeforeReadinessObservation()
    {
        RecordingFolderRepository repository = RequestedRepository();
        RecordingRepositoryCreationReadinessValidator readiness = new(ReadinessFailed());
        RepositoryBackedFolderCreationService service = Service(repository, readiness);

        FolderResult result = await service.CreateAsync(
            Request(),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.IdempotentReplay);
        repository.StreamNamesConstructed.ShouldBe(1);
        repository.StreamsLoaded.ShouldBe(1);
        repository.IdempotencyLookups.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
        readiness.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task ReadyFolderShouldAppendRepositoryBindingRequestAfterReadinessPasses()
    {
        RecordingFolderRepository repository = SeededRepository();
        RecordingRepositoryCreationReadinessValidator readiness = new(ReadinessReady());
        RepositoryBackedFolderCreationService service = Service(repository, readiness);

        FolderResult result = await service.CreateAsync(
            Request(),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.Accepted);
        repository.IdempotencyLookups.ShouldBe(1);
        repository.AppendsAttempted.ShouldBe(1);
        repository.EventsAppended.ShouldBe(1);
        repository.LastAppendedEvents.ShouldHaveSingleItem().ShouldBeOfType<RepositoryBindingRequested>();
        readiness.Calls.ShouldBe(1);
    }

    private static RepositoryBackedFolderCreationService Service(
        IFolderRepository repository,
        IRepositoryCreationReadinessValidator readiness)
        => new(
            AuthorizationService(),
            readiness,
            repository,
            new FixedTimeProvider(Now));

    private static RepositoryBackedFolderCreationRequest Request(
        string? authoritativeTenantId = "tenant-a",
        string principalId = "user-a",
        string idempotencyKey = "idempotency-binding-a",
        string? payloadTenantId = null)
        => new(
            authoritativeTenantId,
            principalId,
            EventStoreClaimTransformEvidence.Allowed(
                authoritativeTenantId,
                principalId,
                [RepositoryBackedFolderCreationService.ActionToken, ProviderReadinessValidationService.ReadActionToken]),
            FolderId: "folder-a",
            RequestSchemaVersion: "v1",
            RepositoryBindingId: "repository-binding-a",
            ProviderBindingRef: "provider-binding-a",
            RepositoryProfileRef: "repository-profile-a",
            BranchRefPolicyRef: "branch-ref-policy-a",
            FolderMetadataDisplayName: "Folder A",
            CredentialScopeClass: "tenant-installation",
            CorrelationId: "correlation-a",
            TaskId: "task-a",
            IdempotencyKey: idempotencyKey,
            PayloadTenantId: payloadTenantId,
            ClientControlledTenantValues: new Dictionary<string, string?>(StringComparer.Ordinal),
            ClientControlledPrincipalValues: new Dictionary<string, string?>(StringComparer.Ordinal));

    private static RecordingFolderRepository SeededRepository()
    {
        RecordingFolderRepository repository = new();
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderResult created = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create());
        repository.Seed(streamName, created.Events);
        return repository;
    }

    private static RecordingFolderRepository RequestedRepository()
    {
        RecordingFolderRepository repository = new();
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderResult created = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create());
        FolderState active = FolderState.Empty.Apply(created.Events, streamName);
        FolderResult requested = FolderAggregate.Handle(
            active,
            FolderCommandFactory.CreateRepositoryBackedFolder(
                credentialScopeClass: "tenant-installation",
                actorPrincipalId: "user-a"),
            Now);
        repository.Seed(streamName, [.. created.Events, .. requested.Events]);
        return repository;
    }

    private static RecordingFolderRepository ArchivedRepository()
    {
        RecordingFolderRepository repository = SeededRepository();
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState active = repository.Load(streamName);
        repository = new RecordingFolderRepository();
        FolderResult created = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create());
        FolderResult archived = FolderAggregate.Handle(active, FolderCommandFactory.Archive());
        repository.Seed(streamName, [.. created.Events, .. archived.Events]);
        return repository;
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

    private static ProviderReadinessValidationResult ReadinessReady()
        => Readiness("ready", ProviderFailureCategory.None);

    private static ProviderReadinessValidationResult ReadinessFailed()
        => Readiness("failed", ProviderFailureCategory.ProviderReadinessFailed);

    private static ProviderReadinessValidationResult Readiness(string status, ProviderFailureCategory category)
        => Readiness(ProviderReadinessResultCode.Allowed, status, category);

    private static ProviderReadinessValidationResult Readiness(
        ProviderReadinessResultCode code,
        string status,
        ProviderFailureCategory category)
        => new(
            code,
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

    private sealed class RecordingRepositoryCreationReadinessValidator(ProviderReadinessValidationResult result)
        : IRepositoryCreationReadinessValidator
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
