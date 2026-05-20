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
            FolderArchivePolicyEvidence.Allowed("tenant-a", "organization-a", "folder-a", "policy-v1"));

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
            FolderArchivePolicyEvidence.Allowed("tenant-a", "organization-a", "folder-a", "policy-v1"));

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
            FolderArchivePolicyEvidence.Allowed("tenant-a", "organization-a", "folder-a", "policy-v1"));

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
            FolderArchivePolicyEvidence.Allowed("tenant-a", "organization-a", "folder-a", "policy-v1"));

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
            FolderArchivePolicyEvidence.Denied("tenant-a", "organization-a", "folder-a", "policy-v1"));

        result.Code.ShouldBe(FolderResultCode.ArchivePolicyDenied);
        result.Events.ShouldBeEmpty();
        repository.StreamNamesConstructed.ShouldBe(0);
        repository.StreamsLoaded.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
        repository.EventsAppended.ShouldBe(0);
    }

    [Theory]
    [InlineData(FolderArchivePolicyOutcome.Stale, FolderResultCode.PolicyEvidenceStale)]
    [InlineData(FolderArchivePolicyOutcome.Unavailable, FolderResultCode.PolicyEvidenceUnavailable)]
    [InlineData(FolderArchivePolicyOutcome.Malformed, FolderResultCode.PolicyEvidenceMalformed)]
    public void StaleUnavailableOrMalformedPolicyEvidenceShouldRejectBeforeStreamConstruction(
        FolderArchivePolicyOutcome outcome,
        FolderResultCode expectedCode)
    {
        RecordingFolderRepository repository = SeededRepository();
        FolderArchiveTenantGate gate = new(repository);

        FolderResult result = gate.Handle(
            FolderCommandFactory.Archive(),
            Evidence(TenantAccessOutcome.Allowed, "tenant-a"),
            FolderArchiveAclEvidence.Allowed("tenant-a", "organization-a", "folder-a", "principal-a"),
            new FolderArchivePolicyEvidence(outcome, "tenant-a", "organization-a", "folder-a", "policy-v1"));

        result.Code.ShouldBe(expectedCode);
        result.Events.ShouldBeEmpty();
        repository.StreamNamesConstructed.ShouldBe(0);
        repository.IdempotencyLookups.ShouldBe(0);
        repository.StreamsLoaded.ShouldBe(0);
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
            FolderArchivePolicyEvidence.Allowed("tenant-b", "organization-a", "folder-a", "policy-v1"));

        result.Code.ShouldBe(FolderResultCode.PolicyEvidenceMalformed);
        result.Events.ShouldBeEmpty();
        repository.StreamNamesConstructed.ShouldBe(0);
        repository.StreamsLoaded.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
        repository.EventsAppended.ShouldBe(0);
    }

    [Theory]
    [InlineData("tenant_denied", FolderResultCode.TenantAccessDenied, 0, 0)]
    [InlineData("acl_denied", FolderResultCode.FolderAclDenied, 0, 0)]
    [InlineData("policy_denied", FolderResultCode.ArchivePolicyDenied, 0, 0)]
    [InlineData("policy_unavailable", FolderResultCode.PolicyEvidenceUnavailable, 0, 0)]
    [InlineData("idempotency_unavailable", FolderResultCode.IdempotencyUnavailable, 1, 1)]
    public void PreObservationArchiveDenialsShouldNotDependOnFolderExistence(
        string scenario,
        FolderResultCode expectedCode,
        int expectedStreamNames,
        int expectedIdempotencyLookups)
    {
        ArchiveDiscoveryShape emptyShape = EvaluateDiscoveryShape(new RecordingFolderRepository(), scenario);
        ArchiveDiscoveryShape seededShape = EvaluateDiscoveryShape(SeededRepository(), scenario);

        emptyShape.ShouldBe(seededShape);
        emptyShape.Code.ShouldBe(expectedCode);
        emptyShape.StreamNamesConstructed.ShouldBe(expectedStreamNames);
        emptyShape.IdempotencyLookups.ShouldBe(expectedIdempotencyLookups);
        emptyShape.StreamsLoaded.ShouldBe(0);
        emptyShape.AppendsAttempted.ShouldBe(0);
        emptyShape.EventsAppended.ShouldBe(0);
        emptyShape.DiagnosticsQueried.ShouldBe(0);
        emptyShape.AuditResourcesQueried.ShouldBe(0);
        emptyShape.ProviderReadinessChecked.ShouldBe(0);
    }

    [Fact]
    public void SafeNotFoundArchiveShouldNotEmitSideEffectOrDiagnosticSignals()
    {
        RecordingFolderRepository repository = new();
        FolderArchiveTenantGate gate = new(repository);

        FolderResult result = gate.Handle(
            FolderCommandFactory.Archive(),
            Evidence(TenantAccessOutcome.Allowed, "tenant-a"),
            FolderArchiveAclEvidence.Allowed("tenant-a", "organization-a", "folder-a", "principal-a"),
            FolderArchivePolicyEvidence.Allowed("tenant-a", "organization-a", "folder-a", "policy-v1"));

        result.Code.ShouldBe(FolderResultCode.FolderNotFound);
        result.Events.ShouldBeEmpty();
        repository.StreamNamesConstructed.ShouldBe(1);
        repository.IdempotencyLookups.ShouldBe(1);
        repository.StreamsLoaded.ShouldBe(1);
        repository.AppendsAttempted.ShouldBe(0);
        repository.EventsAppended.ShouldBe(0);
        repository.DiagnosticsQueried.ShouldBe(0);
        repository.AuditResourcesQueried.ShouldBe(0);
        repository.ProviderReadinessChecked.ShouldBe(0);
    }

    [Fact]
    public void AlreadyArchivedArchiveShouldNotEmitSideEffectOrDiagnosticSignals()
    {
        RecordingFolderRepository repository = ArchivedRepository();
        FolderArchiveTenantGate gate = new(repository);

        FolderResult result = gate.Handle(
            FolderCommandFactory.Archive(idempotencyKey: "idempotency-archive-b"),
            Evidence(TenantAccessOutcome.Allowed, "tenant-a"),
            FolderArchiveAclEvidence.Allowed("tenant-a", "organization-a", "folder-a", "principal-a"),
            FolderArchivePolicyEvidence.Allowed("tenant-a", "organization-a", "folder-a", "policy-v1"));

        result.Code.ShouldBe(FolderResultCode.AlreadyArchived);
        result.Events.ShouldBeEmpty();
        repository.AppendsAttempted.ShouldBe(0);
        repository.EventsAppended.ShouldBe(0);
        repository.DiagnosticsQueried.ShouldBe(0);
        repository.AuditResourcesQueried.ShouldBe(0);
        repository.ProviderReadinessChecked.ShouldBe(0);
    }

    private static RecordingFolderRepository SeededRepository()
    {
        RecordingFolderRepository repository = new();
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderResult created = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create());
        repository.Seed(streamName, created.Events);
        return repository;
    }

    private static RecordingFolderRepository ArchivedRepository()
    {
        RecordingFolderRepository repository = new();
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderResult created = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create());
        FolderState createdState = FolderState.Empty.Apply(created.Events, streamName);
        FolderResult archived = FolderAggregate.Handle(createdState, FolderCommandFactory.Archive());
        repository.Seed(streamName, [.. created.Events, .. archived.Events]);
        return repository;
    }

    private static ArchiveDiscoveryShape EvaluateDiscoveryShape(RecordingFolderRepository repository, string scenario)
    {
        if (scenario == "idempotency_unavailable")
        {
            repository.IdempotencyUnavailable = true;
        }

        TenantAccessAuthorizationResult tenantAccess = scenario == "tenant_denied"
            ? Evidence(TenantAccessOutcome.Denied, "tenant-a")
            : Evidence(TenantAccessOutcome.Allowed, "tenant-a");
        FolderArchiveAclEvidence aclEvidence = scenario == "acl_denied"
            ? FolderArchiveAclEvidence.Denied("tenant-a", "organization-a", "folder-a", "principal-a")
            : FolderArchiveAclEvidence.Allowed("tenant-a", "organization-a", "folder-a", "principal-a");
        FolderArchivePolicyEvidence policyEvidence = scenario switch
        {
            "policy_denied" => FolderArchivePolicyEvidence.Denied("tenant-a", "organization-a", "folder-a", "policy-v1"),
            "policy_unavailable" => new FolderArchivePolicyEvidence(
                FolderArchivePolicyOutcome.Unavailable,
                "tenant-a",
                "organization-a",
                "folder-a",
                "policy-v1"),
            _ => FolderArchivePolicyEvidence.Allowed("tenant-a", "organization-a", "folder-a", "policy-v1"),
        };

        FolderArchiveTenantGate gate = new(repository);
        FolderResult result = gate.Handle(FolderCommandFactory.Archive(), tenantAccess, aclEvidence, policyEvidence);
        return new ArchiveDiscoveryShape(
            result.Code,
            repository.StreamNamesConstructed,
            repository.IdempotencyLookups,
            repository.StreamsLoaded,
            repository.AppendsAttempted,
            repository.EventsAppended,
            repository.DiagnosticsQueried,
            repository.AuditResourcesQueried,
            repository.ProviderReadinessChecked);
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

    private sealed record ArchiveDiscoveryShape(
        FolderResultCode Code,
        int StreamNamesConstructed,
        int IdempotencyLookups,
        int StreamsLoaded,
        int AppendsAttempted,
        int EventsAppended,
        int DiagnosticsQueried,
        int AuditResourcesQueried,
        int ProviderReadinessChecked);
}
