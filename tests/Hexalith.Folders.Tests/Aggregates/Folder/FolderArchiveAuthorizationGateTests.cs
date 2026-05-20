using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderArchiveAuthorizationGateTests
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
    public void RejectedTenantEvidenceShouldPreventArchiveObservation(
        TenantAccessOutcome outcome,
        FolderResultCode expectedCode)
    {
        RecordingFolderRepository repository = new();
        FolderArchiveTenantGate gate = new(repository);

        FolderResult result = gate.Handle(
            FolderCommandFactory.Archive(),
            Evidence(outcome),
            FolderArchiveAclEvidence.Allowed("tenant-a", "organization-a", "folder-a", "principal-a"),
            FolderArchivePolicyEvidence.Allowed("tenant-a", "folder-a", "policy-v1"));

        result.Code.ShouldBe(expectedCode);
        repository.StreamNamesConstructed.ShouldBe(0);
        repository.IdempotencyLookups.ShouldBe(0);
        repository.StreamsLoaded.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
        repository.EventsAppended.ShouldBe(0);
        repository.DiagnosticsQueried.ShouldBe(0);
        repository.AuditResourcesQueried.ShouldBe(0);
        repository.ProviderReadinessChecked.ShouldBe(0);
        repository.RepositoriesCreated.ShouldBe(0);
    }

    [Fact]
    public void ClientControlledTenantMismatchShouldRejectBeforeStreamConstruction()
    {
        RecordingFolderRepository repository = new();
        FolderArchiveTenantGate gate = new(repository);

        FolderResult result = gate.Handle(
            FolderCommandFactory.Archive(payloadTenantId: "tenant-from-payload"),
            Evidence(TenantAccessOutcome.Allowed, "tenant-a"),
            FolderArchiveAclEvidence.Allowed("tenant-a", "organization-a", "folder-a", "principal-a"),
            FolderArchivePolicyEvidence.Allowed("tenant-a", "folder-a", "policy-v1"));

        result.Code.ShouldBe(FolderResultCode.TenantMismatch);
        repository.StreamNamesConstructed.ShouldBe(0);
        repository.StreamsLoaded.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
    }

    [Fact]
    public void AclDeniedShouldRejectBeforeStreamConstruction()
    {
        RecordingFolderRepository repository = new();
        FolderArchiveTenantGate gate = new(repository);

        FolderResult result = gate.Handle(
            FolderCommandFactory.Archive(),
            Evidence(TenantAccessOutcome.Allowed, "tenant-a"),
            FolderArchiveAclEvidence.Denied("tenant-a", "organization-a", "folder-a", "principal-a"),
            FolderArchivePolicyEvidence.Allowed("tenant-a", "folder-a", "policy-v1"));

        result.Code.ShouldBe(FolderResultCode.FolderAclDenied);
        repository.StreamNamesConstructed.ShouldBe(0);
        repository.StreamsLoaded.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
    }

    [Fact]
    public void AllowedAclEvidenceForDifferentPrincipalShouldFailClosedBeforeStreamConstruction()
    {
        RecordingFolderRepository repository = new();
        FolderArchiveTenantGate gate = new(repository);

        FolderResult result = gate.Handle(
            FolderCommandFactory.Archive(),
            Evidence(TenantAccessOutcome.Allowed, "tenant-a"),
            FolderArchiveAclEvidence.Allowed("tenant-a", "organization-a", "folder-a", "principal-b"),
            FolderArchivePolicyEvidence.Allowed("tenant-a", "folder-a", "policy-v1"));

        result.Code.ShouldBe(FolderResultCode.AclEvidenceUnavailable);
        repository.StreamNamesConstructed.ShouldBe(0);
        repository.StreamsLoaded.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
    }

    [Fact]
    public void PolicyDeniedShouldLeaveLifecycleStateUnchangedWithoutAppend()
    {
        RecordingFolderRepository repository = SeededRepository();
        FolderArchiveTenantGate gate = new(repository);

        FolderResult result = gate.Handle(
            FolderCommandFactory.Archive(),
            Evidence(TenantAccessOutcome.Allowed, "tenant-a"),
            FolderArchiveAclEvidence.Allowed("tenant-a", "organization-a", "folder-a", "principal-a"),
            FolderArchivePolicyEvidence.Denied("tenant-a", "folder-a", "policy-v1"));

        result.Code.ShouldBe(FolderResultCode.ArchivePolicyDenied);
        result.Events.ShouldBeEmpty();
        repository.StreamNamesConstructed.ShouldBe(1);
        repository.StreamsLoaded.ShouldBe(1);
        repository.AppendsAttempted.ShouldBe(0);
        repository.EventsAppended.ShouldBe(0);
    }

    [Fact]
    public void PolicyEvidenceForDifferentTenantShouldFailClosedWithoutAppend()
    {
        RecordingFolderRepository repository = SeededRepository();
        FolderArchiveTenantGate gate = new(repository);

        FolderResult result = gate.Handle(
            FolderCommandFactory.Archive(),
            Evidence(TenantAccessOutcome.Allowed, "tenant-a"),
            FolderArchiveAclEvidence.Allowed("tenant-a", "organization-a", "folder-a", "principal-a"),
            FolderArchivePolicyEvidence.Allowed("tenant-b", "folder-a", "policy-v1"));

        result.Code.ShouldBe(FolderResultCode.AclEvidenceUnavailable);
        result.Events.ShouldBeEmpty();
        repository.StreamsLoaded.ShouldBe(1);
        repository.AppendsAttempted.ShouldBe(0);
        repository.EventsAppended.ShouldBe(0);
    }

    private static RecordingFolderRepository SeededRepository()
    {
        RecordingFolderRepository repository = new();
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderResult created = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create());
        repository.Seed(streamName, created.Events);
        return repository;
    }

    private static TenantAccessAuthorizationResult Evidence(
        TenantAccessOutcome outcome,
        string? tenantId = "tenant-a")
        => new(
            outcome,
            outcome == TenantAccessOutcome.Allowed ? "allowed" : "denied",
            tenantId,
            tenantId is null ? null : $"{tenantId}:7",
            new DateTimeOffset(2026, 5, 20, 8, 0, 0, TimeSpan.Zero),
            TimeSpan.FromMinutes(1),
            TenantProjectionFreshnessStatus.Fresh,
            "local-projection");
}
