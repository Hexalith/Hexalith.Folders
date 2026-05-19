using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderAccessTenantEvidenceGateTests
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
    public void RejectedTenantEvidenceShouldPreventAllProtectedResourceTouches(
        TenantAccessOutcome outcome,
        FolderResultCode expectedCode)
    {
        RecordingFolderRepository repository = new();
        FolderAccessTenantGate gate = new(repository);

        FolderResult result = gate.Handle(
            FolderCommandFactory.GrantAccess(),
            Evidence(outcome),
            FolderAccessAclEvidence.Allowed("tenant-a", "organization-a", "folder-a", "principal-a"));

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

    [Theory]
    [InlineData("payload")]
    [InlineData("route")]
    [InlineData("query")]
    [InlineData("header")]
    [InlineData("forwarded")]
    [InlineData("metadata")]
    [InlineData("envelope")]
    public void ClientControlledTenantIdsShouldRejectBeforeStreamConstruction(string source)
    {
        RecordingFolderRepository repository = new();
        FolderAccessTenantGate gate = new(repository);

        Dictionary<string, string?> clientTenantIds = new(StringComparer.Ordinal)
        {
            [source] = "tenant-from-client",
        };

        GrantFolderAccess command = source == "payload"
            ? FolderCommandFactory.GrantAccess(payloadTenantId: "tenant-from-client")
            : FolderCommandFactory.GrantAccess(clientTenantIds: clientTenantIds);

        FolderResult result = gate.Handle(
            command,
            Evidence(TenantAccessOutcome.Allowed, "tenant-a"),
            FolderAccessAclEvidence.Allowed("tenant-a", "organization-a", "folder-a", "principal-a"));

        result.Code.ShouldBe(FolderResultCode.TenantMismatch);
        repository.StreamNamesConstructed.ShouldBe(0);
        repository.IdempotencyLookups.ShouldBe(0);
        repository.StreamsLoaded.ShouldBe(0);
    }

    [Fact]
    public void TenantGateShouldUseAuthoritativeTenantForStreamName()
    {
        RecordingFolderRepository repository = SeededRepository();
        FolderAccessTenantGate gate = new(repository);

        FolderResult result = gate.Handle(
            FolderCommandFactory.GrantAccess(managedTenantId: "payload-tenant", payloadTenantId: "tenant-a"),
            Evidence(TenantAccessOutcome.Allowed, "tenant-a"),
            FolderAccessAclEvidence.Allowed("tenant-a", "organization-a", "folder-a", "principal-a"));

        result.Code.ShouldBe(FolderResultCode.Accepted);
        repository.LastStreamName.ShouldBe("tenant-a:folders:folder-a");
    }

    internal static TenantAccessAuthorizationResult Evidence(
        TenantAccessOutcome outcome,
        string? tenantId = "tenant-a")
        => new(
            outcome,
            outcome == TenantAccessOutcome.Allowed ? "allowed" : "denied",
            tenantId,
            tenantId is null ? null : $"{tenantId}:7",
            new DateTimeOffset(2026, 5, 19, 8, 0, 0, TimeSpan.Zero),
            TimeSpan.FromMinutes(1),
            TenantProjectionFreshnessStatus.Fresh,
            "local-projection");

    internal static RecordingFolderRepository SeededRepository()
    {
        RecordingFolderRepository repository = new();
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderResult created = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create());
        repository.Seed(streamName, created.Events);
        return repository;
    }
}
