using System.Text.Json;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Tests.Shared.AuditLeakage;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Organization;

public sealed class OrganizationAclMetadataLeakageTests
{
    [Fact]
    public void SingleAndBatchGrantsEmitMetadataOnlyResultsAndConcreteEvents()
    {
        IOrganizationAclCommand[] commands =
        [
            AclCommandFactory.Grant(),
            AclCommandFactory.Initialize(
                AclCommandFactory.Operation(OrganizationAclOperationIntent.Grant, principalId: "principal-a"),
                AclCommandFactory.Operation(OrganizationAclOperationIntent.Grant, principalId: "principal-b")),
        ];

        foreach (IOrganizationAclCommand command in commands)
        {
            OrganizationAclResult result = OrganizationAggregate.Handle(OrganizationState.Empty, command);
            result.Code.ShouldBe(OrganizationAclResultCode.Accepted);
            result.Events.Count.ShouldBe(command.Operations.Count + 1);
            AssertMetadataOnly(JsonSerializer.Serialize(result), "console-payloads");

            foreach (IOrganizationAclEvent domainEvent in result.Events)
            {
                string payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType());
                AssertMetadataOnly(payload, "events");
                payload.ShouldContain("tenant-a");
                payload.ShouldContain("organization-a");
            }

            result.Events.OfType<OrganizationAclPrincipalGranted>().Select(grant => grant.PrincipalId)
                .ShouldBe(command.Operations.Select(operation => operation.PrincipalId));
        }
    }

    [Theory]
    [InlineData("secret-shaped-access-key")]
    [InlineData("unauthorized-resource-existence")]
    [InlineData("credential-shaped-provider-token")]
    [InlineData("production-url-marker")]
    public void RejectionsSuppressContaminatedEvidenceAndDoNotAppend(string sampleId)
    {
        string sentinel = AuditLeakageCorpus.Get(sampleId).Value;
        RecordingOrganizationAclRepository repository = new();
        OrganizationAclTenantGate gate = new(repository);
        TenantAccessAuthorizationResult denied = new(TenantAccessOutcome.Denied, "denied", sentinel, null, null, null,
            TenantProjectionFreshnessStatus.Fresh, "test");
        GrantOrganizationAclPrincipal contaminated = AclCommandFactory.Grant(organizationId: sentinel, correlationId: sentinel);

        OrganizationAclResult tenantRejection = gate.Handle(contaminated, denied);
        OrganizationAclResult payloadRejection = OrganizationAggregate.Handle(OrganizationState.Empty, AclCommandFactory.Initialize(
            AclCommandFactory.Operation(OrganizationAclOperationIntent.Grant),
            AclCommandFactory.Operation(OrganizationAclOperationIntent.Grant, principalId: "principal-b", action: sentinel)));

        tenantRejection.Code.ShouldBe(OrganizationAclResultCode.TenantAccessDenied);
        payloadRejection.Code.ShouldBe(OrganizationAclResultCode.UnsupportedAction);
        payloadRejection.FailingOperationIndex.ShouldBe(1);
        payloadRejection.FailingOperation.ShouldBeNull();
        foreach (OrganizationAclResult rejection in new[] { tenantRejection, payloadRejection })
        {
            rejection.Events.ShouldBeEmpty();
            string payload = JsonSerializer.Serialize(rejection);
            AssertMetadataOnly(payload, "console-payloads");
            AuditLeakageMatcher.Contains(payload, sentinel).ShouldBeFalse($"Rejection contained {sampleId}.");
        }

        repository.StreamNamesConstructed.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
    }

    [Theory]
    [InlineData("secret-shaped-access-key")]
    [InlineData("credential-shaped-provider-token")]
    public void ContaminatedCorrelationIsRejectedBeforeItCanEnterAnEvent(string sampleId)
    {
        string sentinel = AuditLeakageCorpus.Get(sampleId).Value;
        OrganizationAclResult rejection = OrganizationAggregate.Handle(
            OrganizationState.Empty, AclCommandFactory.Grant(correlationId: sentinel));

        rejection.Code.ShouldBe(OrganizationAclResultCode.MalformedEvidence);
        rejection.Events.ShouldBeEmpty();
        rejection.CorrelationId.ShouldBeNull();
        AssertMetadataOnly(JsonSerializer.Serialize(rejection), "console-payloads");
    }

    private static void AssertMetadataOnly(string payload, string channel)
    {
        foreach (AuditLeakageSample sample in AuditLeakageCorpus.Samples)
        {
            AuditLeakageMatcher.LeaksOn(sample, channel, payload).ShouldBeFalse($"Unexpected {sample.Id} in {channel}.");
        }
    }
}
