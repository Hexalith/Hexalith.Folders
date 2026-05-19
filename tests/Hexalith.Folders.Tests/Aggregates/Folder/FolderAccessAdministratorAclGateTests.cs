using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderAccessAdministratorAclGateTests
{
    [Theory]
    [InlineData(FolderAccessAclOutcome.Denied, FolderResultCode.FolderAclDenied)]
    [InlineData(FolderAccessAclOutcome.Unavailable, FolderResultCode.AclEvidenceUnavailable)]
    [InlineData(FolderAccessAclOutcome.Malformed, FolderResultCode.AclEvidenceUnavailable)]
    [InlineData(FolderAccessAclOutcome.Stale, FolderResultCode.AclEvidenceUnavailable)]
    [InlineData(FolderAccessAclOutcome.UnsupportedAction, FolderResultCode.AclEvidenceUnavailable)]
    [InlineData(FolderAccessAclOutcome.TenantMismatch, FolderResultCode.TenantMismatch)]
    // FolderMismatch is now folded into AclEvidenceUnavailable so denial vs scope-mismatch
    // are indistinguishable to a caller probing folder existence.
    [InlineData(FolderAccessAclOutcome.FolderMismatch, FolderResultCode.AclEvidenceUnavailable)]
    public void RejectedAclEvidenceShouldPreventStreamAndIdempotencySideEffects(
        FolderAccessAclOutcome outcome,
        FolderResultCode expectedCode)
    {
        RecordingFolderRepository repository = new();
        FolderAccessTenantGate gate = new(repository);

        FolderResult result = gate.Handle(
            FolderCommandFactory.GrantAccess(),
            TenantEvidence(),
            new FolderAccessAclEvidence(outcome, "tenant-a", "organization-a", "folder-a", "principal-a", "configure_provider_binding"));

        result.Code.ShouldBe(expectedCode);
        repository.StreamNamesConstructed.ShouldBe(0);
        repository.IdempotencyLookups.ShouldBe(0);
        repository.StreamsLoaded.ShouldBe(0);
        repository.EventsAppended.ShouldBe(0);
    }

    [Theory]
    [InlineData("tenant-b", "organization-a", "folder-a", "principal-a")]
    [InlineData("tenant-a", "organization-b", "folder-a", "principal-a")]
    [InlineData("tenant-a", "organization-a", "folder-b", "principal-a")]
    [InlineData("tenant-a", "organization-a", "folder-a", "principal-b")]
    public void AllowedAclWithContextMismatchShouldReturnEvidenceUnavailable(
        string tenantId,
        string organizationId,
        string folderId,
        string principalId)
    {
        RecordingFolderRepository repository = new();
        FolderAccessTenantGate gate = new(repository);

        FolderResult result = gate.Handle(
            FolderCommandFactory.GrantAccess(),
            TenantEvidence(),
            FolderAccessAclEvidence.Allowed(tenantId, organizationId, folderId, principalId));

        // Allowed-but-mismatched evidence is collapsed into AclEvidenceUnavailable so it is
        // indistinguishable from denial paths — folder/principal existence is not revealed.
        result.Code.ShouldBe(FolderResultCode.AclEvidenceUnavailable);
        repository.StreamNamesConstructed.ShouldBe(0);
    }

    [Fact]
    public void AllowedAclEvidenceWithWrongActionShouldThrowAtConstruction()
    {
        // The ctor pins Allowed evidence to FolderAccessAclEvidence.ManagementAction so a
        // deserializer or test seam cannot construct foreign-action Allowed evidence and
        // bypass EvaluateAcl's Action equality check.
        Should.Throw<ArgumentException>(() =>
            new FolderAccessAclEvidence(
                FolderAccessAclOutcome.Allowed,
                "tenant-a",
                "organization-a",
                "folder-a",
                "principal-a",
                "create_folder"));
    }

    [Fact]
    public void AllowedAclEvidenceWithNullOrWhitespaceActionShouldThrowAtConstruction()
    {
        Should.Throw<ArgumentException>(() =>
            new FolderAccessAclEvidence(
                FolderAccessAclOutcome.Allowed,
                "tenant-a",
                "organization-a",
                "folder-a",
                "principal-a",
                "   "));
    }

    [Fact]
    public void TenantFailureShouldShortCircuitBeforeAclEvidenceCanMatter()
    {
        RecordingFolderRepository repository = new();
        FolderAccessTenantGate gate = new(repository);

        FolderResult result = gate.Handle(
            FolderCommandFactory.GrantAccess(),
            FolderAccessTenantEvidenceGateTests.Evidence(TenantAccessOutcome.Denied),
            new FolderAccessAclEvidence(FolderAccessAclOutcome.Denied, "tenant-a", "organization-a", "folder-a", "principal-a", "configure_provider_binding"));

        result.Code.ShouldBe(FolderResultCode.TenantAccessDenied);
        repository.StreamNamesConstructed.ShouldBe(0);
        repository.IdempotencyLookups.ShouldBe(0);
    }

    private static TenantAccessAuthorizationResult TenantEvidence()
        => FolderAccessTenantEvidenceGateTests.Evidence(TenantAccessOutcome.Allowed);
}
