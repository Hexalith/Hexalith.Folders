using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Tests.Aggregates.Organization;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FinalAclReauthorizationTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 26, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PlainFolderCreationRejectsAclRevocationBeforeAppend()
    {
        RecordingFolderRepository repository = new();
        FinalAclRevokingPermissions permissions = new();
        FolderCreationService service = new(AuthorizationService(permissions), repository, new FinalAclFixedTimeProvider(Now));
        FolderCreationRequest request = new(
            "tenant-a", "principal-a",
            EventStoreClaimTransformEvidence.Allowed("tenant-a", "principal-a", [FolderCreationService.ActionToken]),
            "folder-a", "Folder A", "correlation-a", "task-a", "idempotency-a", null,
            new Dictionary<string, string?>(), new Dictionary<string, string?>());

        FolderResult result = await service.CreateAsync(request, TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.FolderAclDenied);
        permissions.Calls.ShouldBe(2);
        repository.AppendsAttempted.ShouldBe(0);
    }

    [Fact]
    public async Task ProviderBindingRejectsAclRevocationBeforeAppend()
    {
        RecordingOrganizationProviderBindingRepository repository = new(ProviderBindingCommandFactory.StateWithConfigurePermission());
        FinalAclRevokingPermissions permissions = new();
        ConfigureProviderBindingService service = new(AuthorizationService(permissions), repository, new FinalAclFixedTimeProvider(Now));
        ConfigureProviderBindingServiceRequest request = new(
            "tenant-a", "principal-a",
            EventStoreClaimTransformEvidence.Allowed("tenant-a", "principal-a", [ConfigureProviderBindingService.ActionToken]),
            "binding-a", "github", "credential-a", "correlation-a", "task-a", "provider-binding-new-key", null,
            new Dictionary<string, string?>(), new Dictionary<string, string?>());

        OrganizationProviderBindingResult result = await service.ConfigureAsync(request, TestContext.Current.CancellationToken);

        result.Code.ShouldBe(OrganizationProviderBindingResultCode.MissingPermission);
        permissions.Calls.ShouldBe(2);
        repository.EventsAppended.ShouldBe(0);
    }

    private static LayeredFolderAuthorizationService AuthorizationService(IFolderPermissionEvidenceProvider permissions)
    {
        InMemoryFolderTenantAccessProjectionStore store = new();
        store.SaveAsync(new FolderTenantAccessProjection
        {
            TenantId = "tenant-a",
            Enabled = true,
            Principals = new Dictionary<string, FolderTenantPrincipalEvidence>(StringComparer.Ordinal)
            {
                ["principal-a"] = new("principal-a", "Member"),
            },
            Watermark = 7,
            ProjectionWatermark = "tenant-a:7",
            LastEventTimestamp = Now.AddMinutes(-1),
        }).GetAwaiter().GetResult();
        FixedUtcClock clock = new(Now);
        return new LayeredFolderAuthorizationService(
            new TenantAccessAuthorizer(store, clock, new TenantAccessOptions
            {
                MutationFreshnessBudget = TimeSpan.FromMinutes(5),
                DiagnosticStalenessBudget = TimeSpan.FromMinutes(5),
            }),
            permissions,
            new FinalAclAllowingValidator(),
            new FinalAclAllowingDapr(),
            clock);
    }
}
