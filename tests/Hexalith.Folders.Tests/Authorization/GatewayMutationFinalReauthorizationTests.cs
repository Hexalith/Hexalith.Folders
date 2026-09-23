using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Queries.ProviderReadiness;
using Hexalith.Folders.Testing.Providers;
using Hexalith.Folders.Tests.Aggregates.Folder;
using Hexalith.Folders.Tests.Aggregates.Organization;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Authorization;

public sealed class GatewayMutationFinalReauthorizationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public async Task FolderCreationFinalReauthorizationDetectsFolderAclRevocationBetweenChecks()
    {
        RevokingFolderPermissionEvidenceProvider evidence = new();
        RecordingFolderRepository repository = new();
        FolderCreationService service = new(AuthorizationService(evidence), repository);

        FolderResult result = await service.CreateAsync(new FolderCreationRequest(
            "tenant-a", "user-a", Claim(FolderCreationService.ActionToken),
            "folder-a", "Folder A", "correlation-a", "task-a", "idempotency-a", null,
            EmptyValues(), EmptyValues()), TestContext.Current.CancellationToken);

        repository.EventsAppended.ShouldBe(0);
        result.Code.ShouldBe(FolderResultCode.FolderAclDenied);
        AssertFinalEvidenceMatchesInitial(evidence, "organization_baseline", FolderCreationService.ActionToken);
    }

    [Fact]
    public async Task RepositoryBackedFolderCreationFinalReauthorizationDetectsFolderAclRevocationBetweenChecks()
    {
        RevokingFolderPermissionEvidenceProvider evidence = new();
        RecordingFolderRepository repository = new();
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        repository.Seed(streamName, FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create()).Events);
        ReadyRepositoryCreationReadinessValidator readiness = new(Now);
        RepositoryBackedFolderCreationService service = new(AuthorizationService(evidence), readiness, repository);

        FolderResult result = await service.CreateAsync(new RepositoryBackedFolderCreationRequest(
            "tenant-a", "user-a", Claim(RepositoryBackedFolderCreationService.ActionToken),
            "folder-a", "v1", "repository-binding-a", "provider-binding-a", "repository-profile-a",
            "branch-ref-policy-a", "Folder A", "tenant-installation", "correlation-a", "task-a",
            "idempotency-binding-a", null, EmptyValues(), EmptyValues()), TestContext.Current.CancellationToken);

        repository.EventsAppended.ShouldBe(0);
        result.Code.ShouldBe(FolderResultCode.FolderAclDenied);
        readiness.Calls.ShouldBe(1);
        AssertFinalEvidenceMatchesInitial(evidence, "folder-a", RepositoryBackedFolderCreationService.ActionToken);
    }

    [Fact]
    public async Task ConfigureProviderBindingFinalReauthorizationDetectsFolderAclRevocationBetweenChecks()
    {
        RevokingFolderPermissionEvidenceProvider evidence = new();
        RecordingOrganizationProviderBindingRepository repository = new();
        ConfigureProviderBindingService service = new(AuthorizationService(evidence), repository);

        OrganizationProviderBindingResult result = await service.ConfigureAsync(new ConfigureProviderBindingServiceRequest(
            "tenant-a", "user-a", Claim(ConfigureProviderBindingService.ActionToken),
            "provider-binding-a", "github", "credential-a", "correlation-a", "task-a", "idempotency-a",
            null, EmptyValues(), EmptyValues()), TestContext.Current.CancellationToken);

        repository.EventsAppended.ShouldBe(0);
        result.Code.ShouldBe(OrganizationProviderBindingResultCode.MissingPermission);
        AssertFinalEvidenceMatchesInitial(evidence, "provider-binding-a", ConfigureProviderBindingService.ActionToken);
    }

    [Fact]
    public async Task RepositoryBindingFinalReauthorizationDetectsRevocationBetweenChecks()
    {
        RevokingFolderPermissionEvidenceProvider evidence = new();
        RecordingFolderRepository repository = SeededFolder();
        RepositoryBindingService service = new(
            AuthorizationService(evidence), new ReadyBindingValidator(), new ReadyBindingReader(),
            new RecordingProviderCapabilityResolver(FakeGitProvider.GitHubLike()), repository);

        FolderResult result = await service.BindAsync(new BindRepositoryRequest(
            "tenant-a", "user-a", Claim(RepositoryBindingService.ActionToken),
            "folder-a", "v1", "provider-binding-a", "external-repository-a", "branch-ref-policy-a",
            "tenant-installation", "correlation-a", "task-a", "idempotency-bind-a", null,
            EmptyValues(), EmptyValues()), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.FolderAclDenied);
        repository.EventsAppended.ShouldBe(0);
        AssertFinalEvidenceMatchesInitial(evidence, "folder-a", RepositoryBindingService.ActionToken);
    }

    [Fact]
    public async Task BranchRefPolicyConfigurationFinalReauthorizationDetectsRevocationBetweenChecks()
    {
        RevokingFolderPermissionEvidenceProvider evidence = new();
        RecordingFolderRepository repository = BoundFolder();
        BranchRefPolicyConfigurationService service = new(AuthorizationService(evidence), new ReadyBindingValidator(), repository);

        FolderResult result = await service.ConfigureAsync(new BranchRefPolicyConfigurationRequest(
            "tenant-a", "user-a", Claim(BranchRefPolicyConfigurationService.ActionToken),
            "folder-a", "v1", "repository-binding-a", "opaque-policy-a", "branch_ref_primary",
            ["branch_ref_feature"], ["branch_ref_release"], "correlation-a", "task-a", "idempotency-policy-a",
            null, EmptyValues(), EmptyValues()), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.FolderAclDenied);
        repository.EventsAppended.ShouldBe(0);
        AssertFinalEvidenceMatchesInitial(evidence, "folder-a", BranchRefPolicyConfigurationService.ActionToken);
    }

    [Fact]
    public async Task FolderAccessMutationFinalReauthorizationDetectsRevocationBetweenChecks()
    {
        RevokingFolderPermissionEvidenceProvider evidence = new();
        RecordingFolderRepository repository = SeededFolder();
        FolderAccessMutationService service = new(AuthorizationService(evidence), repository);

        FolderResult result = await service.MutateAsync(new FolderAccessMutationRequest(
            "tenant-a", "user-a", Claim(FolderAccessMutationService.ActionToken),
            "folder-a", FolderAccessOperationIntent.Grant,
            [new FolderAccessOperation(FolderAccessOperationIntent.Grant, FolderAccessPrincipalKind.User, "target-user-a", "read_metadata")],
            "correlation-a", "task-a", "idempotency-access-a", null,
            EmptyValues(), EmptyValues()), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.FolderAclDenied);
        repository.EventsAppended.ShouldBe(0);
        AssertFinalEvidenceMatchesInitial(evidence, "folder-a", FolderAccessMutationService.ActionToken);
    }

    private static RecordingFolderRepository SeededFolder()
    {
        RecordingFolderRepository repository = new();
        repository.Seed(FolderStreamName.Create("tenant-a", "folder-a"),
            FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create()).Events);
        return repository;
    }

    private static RecordingFolderRepository BoundFolder()
    {
        FolderStreamName stream = FolderStreamName.Create("tenant-a", "folder-a");
        FolderResult created = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create());
        FolderResult requested = FolderAggregate.Handle(
            FolderState.Empty.Apply(created.Events, stream), FolderCommandFactory.CreateRepositoryBackedFolder(), Now);
        RepositoryBound bound = new(
            "tenant-a", "organization-a", "folder-a", "repository-binding-a", "provider-binding-a",
            "correlation-bound-a", "task-bound-a", "idempotency-bound-a", "fingerprint-bound-a", Now);
        RecordingFolderRepository repository = new();
        repository.Seed(stream, [.. created.Events, .. requested.Events, bound]);
        return repository;
    }

    private sealed class ReadyBindingValidator : IRepositoryBindingReadinessValidator, IBranchRefPolicyReadinessValidator
    {
        public Task<ProviderReadinessValidationResult> ValidateAsync(
            ProviderReadinessValidationRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new ProviderReadinessValidationResult(
                ProviderReadinessResultCode.Allowed, "ready", "success", "none", Retryable: false,
                RetryAfter: null, RemediationCategory: "none", CorrelationId: request.CorrelationId ?? "correlation-a",
                ProviderReference: "provider-binding-a", ProviderBindingRef: "provider-binding-a",
                CapabilityProfileRef: "profile-a", Evidence: null,
                new ProviderReadinessFreshness("snapshot_per_task", Now, "tenant-a:7", Stale: false),
                ProviderFailureCategory.None, ProviderFailureCategory.None.ToCategoryCode()));
    }

    private sealed class ReadyBindingReader : IProviderReadinessBindingReader
    {
        public Task<OrganizationProviderBinding?> GetAsync(
            ProviderReadinessBindingReadRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult<OrganizationProviderBinding?>(new OrganizationProviderBinding(
                "tenant-a", "organization-a", "provider-binding-a", "github", "credential-reference-a",
                new OrganizationProviderBindingPolicy("naming-policy-a", new Dictionary<string, string>(StringComparer.Ordinal)),
                new OrganizationProviderBindingPolicy("branch-policy-a", new Dictionary<string, string>(StringComparer.Ordinal)),
                "correlation-binding-a", "task-binding-a", "idempotency-provider-binding-a", "fingerprint-provider-binding-a",
                "configured", Now.AddMinutes(-1)));
    }

    private static LayeredFolderAuthorizationService AuthorizationService(RevokingFolderPermissionEvidenceProvider evidence)
    {
        InMemoryFolderTenantAccessProjectionStore tenantStore = new();
        tenantStore.SaveAsync(new FolderTenantAccessProjection
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
        FixedUtcClock clock = new(Now);
        return new LayeredFolderAuthorizationService(
            new TenantAccessAuthorizer(tenantStore, clock, new TenantAccessOptions
            {
                MutationFreshnessBudget = TimeSpan.FromMinutes(5),
                DiagnosticStalenessBudget = TimeSpan.FromMinutes(5),
            }),
            evidence,
            new AllowingEventStoreAuthorizationValidator(),
            new ConfigurationDaprPolicyEvidenceProvider(new DaprPolicyEvidenceOptions()),
            clock);
    }

    private static EventStoreClaimTransformEvidence Claim(string actionToken)
        => EventStoreClaimTransformEvidence.Allowed("tenant-a", "user-a", [actionToken]);

    private static IReadOnlyDictionary<string, string?> EmptyValues()
        => new Dictionary<string, string?>(StringComparer.Ordinal);

    private static void AssertFinalEvidenceMatchesInitial(
        RevokingFolderPermissionEvidenceProvider evidence,
        string expectedScope,
        string expectedAction)
    {
        evidence.Requests.Count.ShouldBe(2);
        evidence.Requests[0].OperationScope.ShouldBe(expectedScope);
        evidence.Requests[1].OperationScope.ShouldBe(evidence.Requests[0].OperationScope);
        evidence.Requests[1].ActionToken.ShouldBe(expectedAction);
        evidence.Requests[1].PrincipalId.ShouldBe(evidence.Requests[0].PrincipalId);
        evidence.Requests[1].OperationPolicyClass.ShouldBe(evidence.Requests[0].OperationPolicyClass);
    }
}
