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
    [InlineData(FolderAccessAclOutcome.FolderMismatch, FolderResultCode.AclEvidenceMismatch)]
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
    [InlineData("tenant-b", "organization-a", "folder-a", "principal-a", "configure_provider_binding")]
    [InlineData("tenant-a", "organization-b", "folder-a", "principal-a", "configure_provider_binding")]
    [InlineData("tenant-a", "organization-a", "folder-b", "principal-a", "configure_provider_binding")]
    [InlineData("tenant-a", "organization-a", "folder-a", "principal-b", "configure_provider_binding")]
    [InlineData("tenant-a", "organization-a", "folder-a", "principal-a", "create_folder")]
    public void AllowedAclWithContextMismatchShouldReturnEvidenceMismatch(
        string tenantId,
        string organizationId,
        string folderId,
        string principalId,
        string action)
    {
        RecordingFolderRepository repository = new();
        FolderAccessTenantGate gate = new(repository);

        FolderResult result = gate.Handle(
            FolderCommandFactory.GrantAccess(),
            TenantEvidence(),
            new FolderAccessAclEvidence(FolderAccessAclOutcome.Allowed, tenantId, organizationId, folderId, principalId, action));

        result.Code.ShouldBe(FolderResultCode.AclEvidenceMismatch);
        repository.StreamNamesConstructed.ShouldBe(0);
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
