using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Server.Authorization;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

// Regression coverage for the round-4 hardening of LayeredAuthBackedFolderArchiveAclEvidenceProvider.
// The provider must NEVER call FolderArchiveAclEvidence.Allowed(...) with null/whitespace
// identifiers — that factory throws, and a throw inside the processor's evidence call
// would bubble through the gateway response as a 500 instead of a safe denial. The
// reordered pre-checks in the provider live in three new lines without dedicated coverage;
// these tests pin the behavior so a future refactor cannot silently regress it.
public sealed class LayeredAuthBackedFolderArchiveAclEvidenceProviderTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ShouldReturnDeniedWhenAllowedOrganizationIdIsNullOrWhitespace(string? organizationId)
    {
        FixedAccessor accessor = new(AllowedContext(
            authoritativeTenantId: "tenant-a",
            organizationId: organizationId,
            actorSafeIdentifier: "user-a"));
        LayeredAuthBackedFolderArchiveAclEvidenceProvider provider = new(accessor);

        FolderArchiveAclEvidence evidence = await provider.GetEvidenceAsync(
            ArchiveCommand("tenant-a", "org-a", "folder-a", "user-a"),
            TestContext.Current.CancellationToken);

        evidence.Outcome.ShouldBe(FolderArchiveAclOutcome.Denied);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ShouldReturnDeniedWhenAllowedAuthoritativeTenantIdIsNullOrWhitespace(string? tenantId)
    {
        FixedAccessor accessor = new(AllowedContext(
            authoritativeTenantId: tenantId,
            organizationId: "org-a",
            actorSafeIdentifier: "user-a"));
        LayeredAuthBackedFolderArchiveAclEvidenceProvider provider = new(accessor);

        FolderArchiveAclEvidence evidence = await provider.GetEvidenceAsync(
            ArchiveCommand("tenant-a", "org-a", "folder-a", "user-a"),
            TestContext.Current.CancellationToken);

        evidence.Outcome.ShouldBe(FolderArchiveAclOutcome.Denied);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ShouldReturnDeniedWhenAllowedActorSafeIdentifierIsNullOrWhitespace(string? actorId)
    {
        FixedAccessor accessor = new(AllowedContext(
            authoritativeTenantId: "tenant-a",
            organizationId: "org-a",
            actorSafeIdentifier: actorId));
        LayeredAuthBackedFolderArchiveAclEvidenceProvider provider = new(accessor);

        FolderArchiveAclEvidence evidence = await provider.GetEvidenceAsync(
            ArchiveCommand("tenant-a", "org-a", "folder-a", "user-a"),
            TestContext.Current.CancellationToken);

        evidence.Outcome.ShouldBe(FolderArchiveAclOutcome.Denied);
    }

    [Fact]
    public async Task ShouldReturnAllowedWhenAllInvariantsHold()
    {
        FixedAccessor accessor = new(AllowedContext(
            authoritativeTenantId: "tenant-a",
            organizationId: "org-a",
            actorSafeIdentifier: "user-a"));
        LayeredAuthBackedFolderArchiveAclEvidenceProvider provider = new(accessor);

        FolderArchiveAclEvidence evidence = await provider.GetEvidenceAsync(
            ArchiveCommand("tenant-a", "org-a", "folder-a", "user-a"),
            TestContext.Current.CancellationToken);

        evidence.Outcome.ShouldBe(FolderArchiveAclOutcome.Allowed);
        evidence.ManagedTenantId.ShouldBe("tenant-a");
        evidence.OrganizationId.ShouldBe("org-a");
        evidence.FolderId.ShouldBe("folder-a");
        evidence.PrincipalId.ShouldBe("user-a");
    }

    [Fact]
    public async Task ShouldReturnDeniedWhenAccessorHasNoCurrentScope()
    {
        FixedAccessor accessor = new(authorization: null);
        LayeredAuthBackedFolderArchiveAclEvidenceProvider provider = new(accessor);

        FolderArchiveAclEvidence evidence = await provider.GetEvidenceAsync(
            ArchiveCommand("tenant-a", "org-a", "folder-a", "user-a"),
            TestContext.Current.CancellationToken);

        evidence.Outcome.ShouldBe(FolderArchiveAclOutcome.Denied);
    }

    private static ArchiveFolder ArchiveCommand(
        string managedTenantId,
        string organizationId,
        string folderId,
        string actorPrincipalId)
        => new(
            managedTenantId,
            organizationId,
            folderId,
            RequestSchemaVersion: "v1",
            ArchiveReasonCode: "caller_requested",
            actorPrincipalId,
            CorrelationId: "corr-1",
            TaskId: "task-1",
            IdempotencyKey: "idem-1",
            PayloadTenantId: null);

    private static LayeredFolderAuthorizationResult AllowedContext(
        string? authoritativeTenantId,
        string? organizationId,
        string? actorSafeIdentifier)
    {
        LayeredFolderAuthorizationAllowedContext context = new(
            AuthoritativeTenantId: authoritativeTenantId ?? string.Empty,
            ActorSafeIdentifier: actorSafeIdentifier ?? string.Empty,
            ActionToken: FolderArchiveAclEvidence.ArchiveAction,
            OperationScope: "folder-a",
            CorrelationId: "corr-1",
            TaskId: "task-1",
            FreshnessWatermark: "watermark-1",
            PolicyLayers: [AuthorizationLayer.FolderAcl])
        {
            OrganizationId = organizationId,
        };

        return new LayeredFolderAuthorizationResult(
            IsAllowed: true,
            Decision: new LayeredFolderAuthorizationDecisionSnapshot(
                TerminalLayer: AuthorizationLayer.FolderAcl,
                OutcomeCode: "allowed",
                Retryable: false,
                FreshnessClass: "fresh",
                FreshnessWatermark: "watermark-1",
                CorrelationId: "corr-1",
                TaskId: "task-1",
                ActorSafeIdentifier: actorSafeIdentifier ?? string.Empty,
                OperationPolicyClass: "mutation",
                TimingBucket: "fast",
                DecidedAt: DateTimeOffset.UtcNow),
            AllowedContext: context,
            EvaluatedLayers: [AuthorizationLayer.FolderAcl]);
    }

    private sealed class FixedAccessor(LayeredFolderAuthorizationResult? authorization) : ILayeredFolderAuthorizationResultAccessor
    {
        public LayeredFolderAuthorizationResult? Current => authorization;

        public void BeginScope(LayeredFolderAuthorizationResult result) =>
            throw new NotSupportedException("FixedAccessor only exposes Current for unit-test use.");

        public void EndScope() =>
            throw new NotSupportedException("FixedAccessor only exposes Current for unit-test use.");
    }
}
