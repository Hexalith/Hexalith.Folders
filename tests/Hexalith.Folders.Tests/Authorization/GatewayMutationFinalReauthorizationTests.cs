using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
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
