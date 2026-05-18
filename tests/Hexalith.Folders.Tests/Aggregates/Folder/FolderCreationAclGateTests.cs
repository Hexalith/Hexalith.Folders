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
    public void AclTenantMismatchShouldRejectBeforeStreamConstruction()
    {
        RecordingFolderRepository repository = new();
        FolderCreateTenantGate gate = new(repository);

        FolderResult result = gate.Handle(
            FolderCommandFactory.Create(),
            TenantEvidence(),
            FolderCreateAclEvidence.Allowed("tenant-b", "organization-a", "principal-a"));

        result.Code.ShouldBe(FolderResultCode.FolderAclDenied);
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
