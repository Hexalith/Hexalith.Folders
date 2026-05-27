using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Queries.ProviderReadiness;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderBranchRefPolicyConfigurationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 27, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MissingAuthenticationShouldDenyBeforeFolderOrReadinessObservation()
    {
        RecordingFolderRepository repository = new();
        RecordingBranchRefPolicyReadinessValidator readiness = new(ReadinessReady());
        BranchRefPolicyConfigurationService service = Service(repository, readiness);

        FolderResult result = await service.ConfigureAsync(
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
    public async Task EquivalentReplayShouldReturnBeforeReadinessObservation()
    {
        RecordingFolderRepository repository = ConfiguredRepository();
        RecordingBranchRefPolicyReadinessValidator readiness = new(ReadinessFailed());
        BranchRefPolicyConfigurationService service = Service(repository, readiness);

        FolderResult result = await service.ConfigureAsync(
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
    public async Task NonBoundFolderShouldRejectBeforeReadinessObservation()
    {
        RecordingFolderRepository repository = SeededRepository();
        RecordingBranchRefPolicyReadinessValidator readiness = new(ReadinessReady());
        BranchRefPolicyConfigurationService service = Service(repository, readiness);

        FolderResult result = await service.ConfigureAsync(
            Request(),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.StateTransitionInvalid);
        repository.StreamsLoaded.ShouldBe(1);
        repository.IdempotencyLookups.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
        readiness.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task ReadyRequestShouldValidateBranchRefReadinessThenAppendPolicyEvent()
    {
        RecordingFolderRepository repository = BoundRepository();
        RecordingBranchRefPolicyReadinessValidator readiness = new(ReadinessReady());
        BranchRefPolicyConfigurationService service = Service(repository, readiness);

        FolderResult result = await service.ConfigureAsync(
            Request(),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.Accepted);
        repository.IdempotencyLookups.ShouldBe(1);
        repository.AppendsAttempted.ShouldBe(1);
        repository.LastAppendedEvents.ShouldHaveSingleItem().ShouldBeOfType<BranchRefPolicyConfigured>();
        readiness.Calls.ShouldBe(1);
        readiness.LastRequest.ShouldNotBeNull().RequestedCapability.ShouldBe(ProviderReadinessRequestedCapability.BranchRefPolicy);
        readiness.LastRequest.ProviderBindingRef.ShouldBe("provider-binding-a");
    }

    private static BranchRefPolicyConfigurationService Service(
        IFolderRepository repository,
        IBranchRefPolicyReadinessValidator readiness)
        => new(AuthorizationService(), readiness, repository, new FixedTimeProvider(Now));

    private static BranchRefPolicyConfigurationRequest Request(
        string? authoritativeTenantId = "tenant-a",
        string principalId = "user-a",
        string idempotencyKey = "idempotency-policy-a")
        => new(
            authoritativeTenantId,
            principalId,
            EventStoreClaimTransformEvidence.Allowed(
                authoritativeTenantId,
                principalId,
                [BranchRefPolicyConfigurationService.ActionToken, ProviderReadinessValidationService.ReadActionToken]),
            FolderId: "folder-a",
            RequestSchemaVersion: "v1",
            RepositoryBindingId: "repository-binding-a",
            PolicyRef: "opaque-policy-a",
            DefaultRef: "branch_ref_primary",
            AllowedRefPatterns: ["branch_ref_feature"],
            ProtectedRefPatterns: ["branch_ref_release"],
            CorrelationId: "correlation-a",
            TaskId: "task-a",
            IdempotencyKey: idempotencyKey,
            PayloadTenantId: null,
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

    private static RecordingFolderRepository BoundRepository()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        RecordingFolderRepository repository = new();
        FolderResult created = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create());
        FolderResult requested = FolderAggregate.Handle(
            FolderState.Empty.Apply(created.Events, streamName),
            FolderCommandFactory.CreateRepositoryBackedFolder(),
            Now);
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
        repository.Seed(streamName, [.. created.Events, .. requested.Events, bound]);
        return repository;
    }

    private static RecordingFolderRepository ConfiguredRepository()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderResult created = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create());
        FolderResult requested = FolderAggregate.Handle(
            FolderState.Empty.Apply(created.Events, streamName),
            FolderCommandFactory.CreateRepositoryBackedFolder(),
            Now);
        RepositoryBound boundEvent = new(
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
        FolderState bound = FolderState.Empty.Apply([.. created.Events, .. requested.Events, boundEvent], streamName);
        FolderResult configured = FolderAggregate.Handle(
            bound,
            new ConfigureBranchRefPolicy(
                "tenant-a",
                "organization-a",
                "folder-a",
                "v1",
                "repository-binding-a",
                "opaque-policy-a",
                "branch_ref_primary",
                ["branch_ref_feature"],
                ["branch_ref_release"],
                "user-a",
                "correlation-a",
                "task-a",
                "idempotency-policy-a",
                PayloadTenantId: null),
            Now);
        RecordingFolderRepository repository = new();
        repository.Seed(streamName, [.. created.Events, .. requested.Events, boundEvent, .. configured.Events]);
        return repository;
    }

    private static ProviderReadinessValidationResult ReadinessReady()
        => Readiness("ready", ProviderFailureCategory.None);

    private static ProviderReadinessValidationResult ReadinessFailed()
        => Readiness("failed", ProviderFailureCategory.ProviderReadinessFailed);

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

    private sealed class RecordingBranchRefPolicyReadinessValidator(ProviderReadinessValidationResult result)
        : IBranchRefPolicyReadinessValidator
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
