using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderCreationTenantEvidenceGateTests
{
    [Theory]
    [InlineData(TenantAccessOutcome.Denied, FolderResultCode.TenantAccessDenied)]
    [InlineData(TenantAccessOutcome.StaleProjection, FolderResultCode.StaleProjection)]
    [InlineData(TenantAccessOutcome.UnavailableProjection, FolderResultCode.UnavailableProjection)]
    [InlineData(TenantAccessOutcome.UnknownTenant, FolderResultCode.UnknownTenant)]
    [InlineData(TenantAccessOutcome.DisabledTenant, FolderResultCode.DisabledTenant)]
    [InlineData(TenantAccessOutcome.MalformedEvidence, FolderResultCode.MalformedEvidence)]
    [InlineData(TenantAccessOutcome.TenantMismatch, FolderResultCode.TenantMismatch)]
    [InlineData(TenantAccessOutcome.MissingAuthoritativeTenant, FolderResultCode.MissingAuthoritativeTenant)]
    [InlineData(TenantAccessOutcome.ReplayConflict, FolderResultCode.ReplayConflict)]
    public void RejectedTenantEvidenceShouldPreventAllSideEffects(
        TenantAccessOutcome outcome,
        FolderResultCode expectedCode)
    {
        RecordingFolderRepository repository = new();
        FolderCreateTenantGate gate = new(repository);

        FolderResult result = gate.Handle(
            FolderCommandFactory.Create(),
            Evidence(outcome),
            FolderCreateAclEvidence.Allowed("tenant-a", "organization-a", "principal-a"));

        result.Code.ShouldBe(expectedCode);
        repository.StreamNamesConstructed.ShouldBe(0);
        repository.StreamsLoaded.ShouldBe(0);
        repository.EventsAppended.ShouldBe(0);
        repository.DiagnosticsQueried.ShouldBe(0);
        repository.AuditResourcesQueried.ShouldBe(0);
        repository.ProviderReadinessChecked.ShouldBe(0);
        repository.RepositoriesCreated.ShouldBe(0);
    }

    [Fact]
    public void AllowedTenantEvidenceShouldUseAuthoritativeTenantInsteadOfPayloadTenant()
    {
        RecordingFolderRepository repository = new();
        FolderCreateTenantGate gate = new(repository);

        FolderResult result = gate.Handle(
            FolderCommandFactory.Create(managedTenantId: "payload-tenant", payloadTenantId: "tenant-a"),
            Evidence(TenantAccessOutcome.Allowed, tenantId: "tenant-a"),
            FolderCreateAclEvidence.Allowed("tenant-a", "organization-a", "principal-a"));

        result.Code.ShouldBe(FolderResultCode.Created);
        repository.LastStreamName.ShouldBe("tenant-a:folders:folder-a");
    }

    [Fact]
    public void PayloadTenantMismatchShouldRejectBeforeStreamConstruction()
    {
        RecordingFolderRepository repository = new();
        FolderCreateTenantGate gate = new(repository);

        FolderResult result = gate.Handle(
            FolderCommandFactory.Create(payloadTenantId: "tenant-from-payload"),
            Evidence(TenantAccessOutcome.Allowed, tenantId: "tenant-a"),
            FolderCreateAclEvidence.Allowed("tenant-a", "organization-a", "principal-a"));

        result.Code.ShouldBe(FolderResultCode.TenantMismatch);
        repository.StreamNamesConstructed.ShouldBe(0);
        repository.StreamsLoaded.ShouldBe(0);
        repository.EventsAppended.ShouldBe(0);
    }

    [Fact]
    public void InvalidMetadataShouldRejectBeforeDurableKeyOrStreamConstruction()
    {
        RecordingFolderRepository repository = new();
        FolderCreateTenantGate gate = new(repository);

        FolderResult result = gate.Handle(
            FolderCommandFactory.Create(displayName: "github_pat_credential_material"),
            Evidence(TenantAccessOutcome.Allowed),
            FolderCreateAclEvidence.Allowed("tenant-a", "organization-a", "principal-a"));

        result.Code.ShouldBe(FolderResultCode.InvalidFolderMetadata);
        repository.LastDurableKey.ShouldBeNull();
        repository.StreamNamesConstructed.ShouldBe(0);
        repository.StreamsLoaded.ShouldBe(0);
        repository.EventsAppended.ShouldBe(0);
    }

    private static TenantAccessAuthorizationResult Evidence(TenantAccessOutcome outcome, string? tenantId = "tenant-a")
        => new(
            outcome,
            outcome == TenantAccessOutcome.Allowed ? "allowed" : "denied",
            tenantId,
            "tenant-a:7",
            new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero),
            TimeSpan.FromMinutes(1),
            TenantProjectionFreshnessStatus.Fresh,
            "local-projection");
}
