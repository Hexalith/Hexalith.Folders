using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Providers.GitHub;
using Hexalith.Folders.Queries.ProviderReadiness;
using Hexalith.Folders.Testing.Providers;
using Hexalith.Folders.Tests.OperationalStatus;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Providers.GitHub;

public sealed partial class GitHubProviderTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task PreparationCapabilityAndReadinessFollowActualGitHubPermissionMapping(bool metadata, bool bindingPermission)
    {
        RecordingGitHubApiClient api = RecordingGitHubApiClient.Success(new GitHubPermissionEvidence(
            true, bindingPermission, true, true, true, true, metadata));
        GitHubProvider provider = new(RecordingGitHubCredentialResolver.SuccessPerCall("test-token"), new RecordingGitHubApiClientFactory(api));
        var discovered = await provider.DiscoverCapabilitiesAsync(Request(), TestContext.Current.CancellationToken);
        discovered.IsSuccess.ShouldBeTrue();
        var preparation = discovered.Profile!.Operations.Single(static operation => operation.OperationId == ProviderOperationCatalog.WorkspacePreparation);
        bool expected = metadata && bindingPermission;
        preparation.Support.ShouldBe(expected ? ProviderOperationSupport.Supported : ProviderOperationSupport.Unavailable);
        preparation.FailureCategory.ShouldBe(expected ? (ProviderFailureCategory?)null : ProviderFailureCategory.ProviderPermissionInsufficient);
        preparation.Constraints["preparation_scope"].ShouldBe("authorized_bound_repository_metadata");

        OperationalStatusClock clock = new();
        InMemoryFolderTenantAccessProjectionStore tenants = new();
        await tenants.SaveAsync(new FolderTenantAccessProjection
        {
            TenantId = "tenant-a",
            Enabled = true,
            Watermark = 1,
            ProjectionWatermark = "tenant-a:1",
            LastEventTimestamp = clock.UtcNow,
            Principals = new Dictionary<string, FolderTenantPrincipalEvidence> { ["reader"] = new("reader", "Member") },
        }, TestContext.Current.CancellationToken);
        InMemoryProviderReadinessBindingReadModel bindings = new();
        bindings.Save(new OrganizationProviderBinding("tenant-a", "tenant-a", "binding-a", "github", "credential-ref-a",
            OrganizationProviderBindingPolicy.Empty, OrganizationProviderBindingPolicy.Empty,
            "corr", "task", "key", "fp", "configured", clock.UtcNow));
        ProviderReadinessValidationService readiness = new(new TenantAccessAuthorizer(tenants, clock, new TenantAccessOptions()), bindings,
            new ProviderCapabilityDiscoveryService(RecordingProviderCapabilityAuthorizer.Allowed("fresh"),
                new RecordingProviderCapabilityResolver(provider), new InMemoryProviderCapabilityEvidenceStore()),
            new InMemoryProviderReadinessEvidenceStore(clock), clock);
        var result = await readiness.ValidateAsync(new ProviderReadinessValidationRequest("tenant-a", "reader", "binding-a",
            ProviderReadinessRequestedCapability.WorkspacePreparation, "corr",
            EventStoreClaimTransformEvidence.Allowed("tenant-a", "reader", [ProviderReadinessValidationService.ReadActionToken]),
            new Dictionary<string, string?>()), TestContext.Current.CancellationToken);
        result.Code.ShouldBe(ProviderReadinessResultCode.Allowed);
        result.Status.ShouldBe(expected ? "ready" : "failed", result.ReasonCode);
        result.FailureCategory.ShouldBe(expected ? ProviderFailureCategory.None : ProviderFailureCategory.UnsupportedProviderCapability);
        api.ReadinessCalls.ShouldBe(2);
    }
}
