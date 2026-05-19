using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderCreationAclGateTests
{
    [Theory]
    [InlineData(FolderCreateAclOutcome.Denied, FolderResultCode.FolderAclDenied)]
    [InlineData(FolderCreateAclOutcome.Unavailable, FolderResultCode.AclEvidenceUnavailable)]
    [InlineData(FolderCreateAclOutcome.Malformed, FolderResultCode.AclEvidenceUnavailable)]
    [InlineData(FolderCreateAclOutcome.Stale, FolderResultCode.AclEvidenceUnavailable)]
    public void RejectedAclEvidenceShouldPreventStreamSideEffects(
        FolderCreateAclOutcome aclOutcome,
        FolderResultCode expectedCode)
    {
        RecordingFolderRepository repository = new();
        FolderCreateTenantGate gate = new(repository);

        FolderResult result = gate.Handle(
            FolderCommandFactory.Create(),
            TenantEvidence(),
            new FolderCreateAclEvidence(aclOutcome, "tenant-a", "organization-a", "principal-a", "create_folder"));

        result.Code.ShouldBe(expectedCode);
        repository.LastDurableKey.ShouldBeNull();
        repository.StreamNamesConstructed.ShouldBe(0);
        repository.StreamsLoaded.ShouldBe(0);
        repository.EventsAppended.ShouldBe(0);
    }

    [Fact]
    public void AclTenantMismatchShouldRejectAsEvidenceMismatch()
    {
        // ACL outcome is `Allowed` but the evidence is for a different tenant. This is
        // distinct from a genuine deny (replay/stale-cache/misrouted-projection signal)
        // and now surfaces as `AclEvidenceMismatch` rather than collapsing into `FolderAclDenied`.
        RecordingFolderRepository repository = new();
        FolderCreateTenantGate gate = new(repository);

        FolderResult result = gate.Handle(
            FolderCommandFactory.Create(),
            TenantEvidence(),
            FolderCreateAclEvidence.Allowed("tenant-b", "organization-a", "principal-a"));

        result.Code.ShouldBe(FolderResultCode.AclEvidenceMismatch);
        repository.StreamNamesConstructed.ShouldBe(0);
        repository.LastDurableKey.ShouldBeNull();
    }

    [Theory]
    [InlineData("tenant-b", "organization-a", "principal-a", "create_folder")] // tenant mismatch
    [InlineData("tenant-a", "organization-b", "principal-a", "create_folder")] // org mismatch
    [InlineData("tenant-a", "organization-a", "principal-b", "create_folder")] // principal mismatch
    [InlineData("tenant-a", "organization-a", "principal-a", "delete_folder")] // action mismatch
    public void AllowedAclWithAnyContextMismatchShouldReturnEvidenceMismatch(
        string aclTenant, string aclOrg, string aclPrincipal, string aclAction)
    {
        RecordingFolderRepository repository = new();
        FolderCreateTenantGate gate = new(repository);

        FolderResult result = gate.Handle(
            FolderCommandFactory.Create(),
            TenantEvidence(),
            new FolderCreateAclEvidence(FolderCreateAclOutcome.Allowed, aclTenant, aclOrg, aclPrincipal, aclAction));

        result.Code.ShouldBe(FolderResultCode.AclEvidenceMismatch);
        repository.StreamNamesConstructed.ShouldBe(0);
    }

    private static TenantAccessAuthorizationResult TenantEvidence()
        => new(
            TenantAccessOutcome.Allowed,
            "allowed",
            "tenant-a",
            "tenant-a:7",
            new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero),
            TimeSpan.FromMinutes(1),
            TenantProjectionFreshnessStatus.Fresh,
            "local-projection");
}
