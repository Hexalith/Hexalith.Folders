using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Organization;

public sealed class OrganizationProviderBindingTenantGateTests
{
    [Theory]
    [InlineData(TenantAccessOutcome.Denied, OrganizationProviderBindingResultCode.TenantAccessDenied)]
    [InlineData(TenantAccessOutcome.StaleProjection, OrganizationProviderBindingResultCode.StaleProjection)]
    [InlineData(TenantAccessOutcome.UnavailableProjection, OrganizationProviderBindingResultCode.UnavailableProjection)]
    [InlineData(TenantAccessOutcome.UnknownTenant, OrganizationProviderBindingResultCode.UnknownTenant)]
    [InlineData(TenantAccessOutcome.DisabledTenant, OrganizationProviderBindingResultCode.DisabledTenant)]
    [InlineData(TenantAccessOutcome.MalformedEvidence, OrganizationProviderBindingResultCode.MalformedEvidence)]
    [InlineData(TenantAccessOutcome.TenantMismatch, OrganizationProviderBindingResultCode.TenantMismatch)]
    [InlineData(TenantAccessOutcome.MissingAuthoritativeTenant, OrganizationProviderBindingResultCode.MissingAuthoritativeTenant)]
    [InlineData(TenantAccessOutcome.ReplayConflict, OrganizationProviderBindingResultCode.ReplayConflict)]
    public void RejectedTenantEvidenceShouldPreventAllStreamAndBindingSideEffects(
        TenantAccessOutcome outcome,
        OrganizationProviderBindingResultCode expectedCode)
    {
        RecordingOrganizationProviderBindingRepository repository = new(ProviderBindingCommandFactory.StateWithConfigurePermission());
        OrganizationProviderBindingTenantGate gate = new(repository);

        OrganizationProviderBindingResult result = gate.Handle(
            ProviderBindingCommandFactory.Configure(),
            Evidence(outcome));

        result.Code.ShouldBe(expectedCode);
        repository.StreamNamesConstructed.ShouldBe(0);
        repository.StreamsLoaded.ShouldBe(0);
        repository.IdempotencyLookups.ShouldBe(0);
        repository.EventsAppended.ShouldBe(0);
    }

    [Fact]
    public void MissingConfigureProviderBindingPermissionShouldDenyBeforeIdempotencyOrBindingChecks()
    {
        RecordingOrganizationProviderBindingRepository repository = new();
        OrganizationProviderBindingTenantGate gate = new(repository);

        OrganizationProviderBindingResult result = gate.Handle(
            ProviderBindingCommandFactory.Configure(providerKind: "missing-provider-family", credentialReferenceId: "credential-a"),
            Evidence(TenantAccessOutcome.Allowed));

        result.Code.ShouldBe(OrganizationProviderBindingResultCode.MissingPermission);
        repository.StreamNamesConstructed.ShouldBe(1);
        repository.StreamsLoaded.ShouldBe(1);
        repository.IdempotencyLookups.ShouldBe(0);
        repository.EventsAppended.ShouldBe(0);
    }

    [Fact]
    public void AllowedTenantAndAclShouldUseAuthoritativeTenantInsteadOfPayloadTenant()
    {
        RecordingOrganizationProviderBindingRepository repository = new(ProviderBindingCommandFactory.StateWithConfigurePermission());
        OrganizationProviderBindingTenantGate gate = new(repository);

        OrganizationProviderBindingResult result = gate.Handle(
            ProviderBindingCommandFactory.Configure(payloadTenantId: "tenant-a"),
            Evidence(TenantAccessOutcome.Allowed, tenantId: "tenant-a"));

        result.Code.ShouldBe(OrganizationProviderBindingResultCode.Accepted);
        repository.LastStreamName.ShouldBe("tenant-a:organizations:organization-a");
        repository.EventsAppended.ShouldBe(1);
    }

    [Fact]
    public void PayloadTenantMismatchShouldRejectBeforeStreamConstruction()
    {
        RecordingOrganizationProviderBindingRepository repository = new(ProviderBindingCommandFactory.StateWithConfigurePermission());
        OrganizationProviderBindingTenantGate gate = new(repository);

        OrganizationProviderBindingResult result = gate.Handle(
            ProviderBindingCommandFactory.Configure(payloadTenantId: "tenant-from-payload"),
            Evidence(TenantAccessOutcome.Allowed, tenantId: "tenant-a"));

        result.Code.ShouldBe(OrganizationProviderBindingResultCode.TenantMismatch);
        repository.StreamNamesConstructed.ShouldBe(0);
        repository.StreamsLoaded.ShouldBe(0);
        repository.EventsAppended.ShouldBe(0);
    }

    [Fact]
    public void RetryAfterPermissionRevocationShouldFailClosedBeforeDuplicateDetection()
    {
        ConfigureProviderBinding command = ProviderBindingCommandFactory.Configure(idempotencyKey: "idem-a");
        RecordingOrganizationProviderBindingRepository repository = new(ProviderBindingCommandFactory.StateWithConfigurePermission());
        OrganizationProviderBindingTenantGate gate = new(repository);

        gate.Handle(command, Evidence(TenantAccessOutcome.Allowed)).Code.ShouldBe(OrganizationProviderBindingResultCode.Accepted);

        RecordingOrganizationProviderBindingRepository revokedRepository = new(OrganizationState.Empty);
        OrganizationProviderBindingTenantGate revokedGate = new(revokedRepository);
        OrganizationProviderBindingResult retry = revokedGate.Handle(command, Evidence(TenantAccessOutcome.Allowed));

        retry.Code.ShouldBe(OrganizationProviderBindingResultCode.MissingPermission);
        revokedRepository.IdempotencyLookups.ShouldBe(0);
        revokedRepository.EventsAppended.ShouldBe(0);
    }

    [Fact]
    public void MissingPermissionShouldNotRevealWhetherBindingAlreadyExists()
    {
        ConfigureProviderBinding command = ProviderBindingCommandFactory.Configure();
        OrganizationState existingBindingWithoutAcl = OrganizationState.Empty.Apply(
            OrganizationAggregate.Handle(OrganizationState.Empty, command).Events);

        RecordingOrganizationProviderBindingRepository missingRepository = new(OrganizationState.Empty);
        RecordingOrganizationProviderBindingRepository existingRepository = new(existingBindingWithoutAcl);

        OrganizationProviderBindingResult missingResult = new OrganizationProviderBindingTenantGate(missingRepository)
            .Handle(command, Evidence(TenantAccessOutcome.Allowed));
        OrganizationProviderBindingResult existingResult = new OrganizationProviderBindingTenantGate(existingRepository)
            .Handle(command, Evidence(TenantAccessOutcome.Allowed));

        existingResult.Code.ShouldBe(missingResult.Code);
        existingResult.ProviderBindingRef.ShouldBe(missingResult.ProviderBindingRef);
        existingResult.CredentialReferenceId.ShouldBe(missingResult.CredentialReferenceId);
        existingRepository.IdempotencyLookups.ShouldBe(missingRepository.IdempotencyLookups);
        existingRepository.EventsAppended.ShouldBe(missingRepository.EventsAppended);
    }

    [Fact]
    public void RetryWithStaleTenantEvidenceShouldNotTouchPreviouslyConfiguredBinding()
    {
        ConfigureProviderBinding command = ProviderBindingCommandFactory.Configure(idempotencyKey: "idem-a");
        RecordingOrganizationProviderBindingRepository repository = new(ProviderBindingCommandFactory.StateWithConfigurePermission());
        OrganizationProviderBindingTenantGate gate = new(repository);

        gate.Handle(command, Evidence(TenantAccessOutcome.Allowed)).Code.ShouldBe(OrganizationProviderBindingResultCode.Accepted);
        int streamsLoadedAfterSuccess = repository.StreamsLoaded;
        int eventsAppendedAfterSuccess = repository.EventsAppended;

        OrganizationProviderBindingResult staleRetry = gate.Handle(command, Evidence(TenantAccessOutcome.StaleProjection));

        staleRetry.Code.ShouldBe(OrganizationProviderBindingResultCode.StaleProjection);
        repository.StreamsLoaded.ShouldBe(streamsLoadedAfterSuccess);
        repository.EventsAppended.ShouldBe(eventsAppendedAfterSuccess);
    }

    [Theory]
    [InlineData(OrganizationAclAppendOutcome.FingerprintMatched, OrganizationProviderBindingResultCode.AlreadyApplied)]
    [InlineData(OrganizationAclAppendOutcome.FingerprintConflict, OrganizationProviderBindingResultCode.IdempotencyConflict)]
    public void AppendRaceOutcomesShouldReturnStableIdempotencyResults(
        OrganizationAclAppendOutcome appendOutcome,
        OrganizationProviderBindingResultCode expectedCode)
    {
        RecordingOrganizationProviderBindingRepository repository = new(ProviderBindingCommandFactory.StateWithConfigurePermission())
        {
            ForcedAppendOutcome = appendOutcome,
        };
        OrganizationProviderBindingTenantGate gate = new(repository);

        OrganizationProviderBindingResult result = gate.Handle(
            ProviderBindingCommandFactory.Configure(),
            Evidence(TenantAccessOutcome.Allowed));

        result.Code.ShouldBe(expectedCode);
        result.Events.ShouldBeEmpty();
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
