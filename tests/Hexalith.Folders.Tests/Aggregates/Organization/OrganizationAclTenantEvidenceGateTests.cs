using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Tests.Authorization;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Organization;

public sealed class OrganizationAclTenantEvidenceGateTests
{
    [Theory]
    [InlineData(TenantAccessOutcome.Denied, OrganizationAclResultCode.TenantAccessDenied)]
    [InlineData(TenantAccessOutcome.StaleProjection, OrganizationAclResultCode.StaleProjection)]
    [InlineData(TenantAccessOutcome.UnavailableProjection, OrganizationAclResultCode.UnavailableProjection)]
    [InlineData(TenantAccessOutcome.UnknownTenant, OrganizationAclResultCode.UnknownTenant)]
    [InlineData(TenantAccessOutcome.DisabledTenant, OrganizationAclResultCode.DisabledTenant)]
    [InlineData(TenantAccessOutcome.MalformedEvidence, OrganizationAclResultCode.MalformedEvidence)]
    [InlineData(TenantAccessOutcome.TenantMismatch, OrganizationAclResultCode.TenantMismatch)]
    [InlineData(TenantAccessOutcome.MissingAuthoritativeTenant, OrganizationAclResultCode.MissingAuthoritativeTenant)]
    [InlineData(TenantAccessOutcome.ReplayConflict, OrganizationAclResultCode.ReplayConflict)]
    public void RejectedTenantEvidenceShouldPreventAllStreamSideEffects(
        TenantAccessOutcome outcome,
        OrganizationAclResultCode expectedCode)
    {
        RecordingOrganizationAclRepository repository = new();
        OrganizationAclTenantGate gate = new(repository);

        OrganizationAclResult result = gate.Handle(
            AclCommandFactory.Grant(),
            Evidence(outcome));

        result.Code.ShouldBe(expectedCode);
        repository.StreamNamesConstructed.ShouldBe(0);
        repository.StreamsLoaded.ShouldBe(0);
        repository.EventsAppended.ShouldBe(0);
        repository.DiagnosticsQueried.ShouldBe(0);
        repository.AuditResourcesQueried.ShouldBe(0);
    }

    [Fact]
    public async Task FutureTenantProjectionShouldRejectBeforeAnyStreamAccess()
    {
        DateTimeOffset now = EffectivePermissionsTestSupport.Now;
        FolderTenantAccessProjection projection = EffectivePermissionsTestSupport.TenantProjection("tenant-a", "user-a");
        projection.LastEventTimestamp = now.AddTicks(1);
        TenantAccessAuthorizer authorizer = new(
            new CountingTenantAccessProjectionStore(projection),
            new FixedUtcClock(now),
            new TenantAccessOptions());
        TenantAccessAuthorizationResult evidence = await authorizer.AuthorizeMutationAsync(
            new TenantAccessAuthorizationContext("tenant-a", "user-a", "tenant-a"),
            TestContext.Current.CancellationToken);
        RecordingOrganizationAclRepository repository = new();

        OrganizationAclResult result = new OrganizationAclTenantGate(repository).Handle(AclCommandFactory.Grant(), evidence);

        evidence.FreshnessStatus.ShouldBe(TenantProjectionFreshnessStatus.Future);
        evidence.Outcome.ShouldBe(TenantAccessOutcome.MalformedEvidence);
        result.Code.ShouldBe(OrganizationAclResultCode.MalformedEvidence);
        repository.IdempotencyLookups.ShouldBe(0);
        repository.StreamNamesConstructed.ShouldBe(0);
        repository.StreamsLoaded.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
    }

    [Fact]
    public void AllowedTenantEvidenceShouldUseAuthoritativeTenantInsteadOfPayloadTenant()
    {
        RecordingOrganizationAclRepository repository = new();
        OrganizationAclTenantGate gate = new(repository);

        OrganizationAclResult result = gate.Handle(
            AclCommandFactory.Grant(payloadTenantId: "tenant-a"),
            Evidence(TenantAccessOutcome.Allowed, tenantId: "tenant-a"));

        result.Code.ShouldBe(OrganizationAclResultCode.Accepted);
        repository.LastStreamName.ShouldBe("tenant-a:organizations:organization-a");
    }

    [Fact]
    public void PayloadTenantMismatchShouldRejectBeforeStreamConstruction()
    {
        RecordingOrganizationAclRepository repository = new();
        OrganizationAclTenantGate gate = new(repository);

        OrganizationAclResult result = gate.Handle(
            AclCommandFactory.Grant(payloadTenantId: "tenant-from-payload"),
            Evidence(TenantAccessOutcome.Allowed, tenantId: "tenant-a"));

        result.Code.ShouldBe(OrganizationAclResultCode.TenantMismatch);
        repository.StreamNamesConstructed.ShouldBe(0);
        repository.StreamsLoaded.ShouldBe(0);
        repository.EventsAppended.ShouldBe(0);
    }

    [Fact]
    public void ConflictingCommandEntriesShouldRejectBeforeStreamConstruction()
    {
        RecordingOrganizationAclRepository repository = new();
        OrganizationAclTenantGate gate = new(repository);
        OrganizationAclOperation grant = AclCommandFactory.Operation(OrganizationAclOperationIntent.Grant);
        OrganizationAclOperation revoke = grant with { Intent = OrganizationAclOperationIntent.Revoke };

        OrganizationAclResult result = gate.Handle(
            AclCommandFactory.Initialize(grant, revoke),
            Evidence(TenantAccessOutcome.Allowed));

        result.Code.ShouldBe(OrganizationAclResultCode.ReplayConflict);
        repository.StreamNamesConstructed.ShouldBe(0);
        repository.StreamsLoaded.ShouldBe(0);
        repository.EventsAppended.ShouldBe(0);
    }

    [Fact]
    public void IdempotencyConflictShouldRejectBeforeStreamConstruction()
    {
        RecordingOrganizationAclRepository repository = new();
        OrganizationAclTenantGate gate = new(repository);
        GrantOrganizationAclPrincipal original = AclCommandFactory.Grant(idempotencyKey: "idem-a", principalId: "principal-a");
        string fingerprint = OrganizationAclCommandValidator.Validate(original).IdempotencyFingerprint;
        repository.RecordIdempotency("tenant-a", "organization-a", "idem-a", fingerprint);

        OrganizationAclResult result = gate.Handle(
            AclCommandFactory.Grant(idempotencyKey: "idem-a", principalId: "principal-b"),
            Evidence(TenantAccessOutcome.Allowed));

        result.Code.ShouldBe(OrganizationAclResultCode.IdempotencyConflict);
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
