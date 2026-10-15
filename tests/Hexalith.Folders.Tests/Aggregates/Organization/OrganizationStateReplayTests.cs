using Hexalith.Folders.Aggregates.Organization;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Organization;

public sealed class OrganizationStateReplayTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UndefinedRetainedPrincipalKindCannotEnterFoldedGrants(bool revoke)
    {
        OrganizationState state = OrganizationState.Empty.Apply([
            new OrganizationAclBaselineInitialized("tenant-a", "tenant-a", "corr", "task", "key", "fingerprint")]);
        IOrganizationEvent invalid = revoke
            ? new OrganizationAclPrincipalRevoked("tenant-a", "tenant-a", (OrganizationAclPrincipalKind)4,
                "user-a", "read_metadata", "corr", "task", "event-key", "event-fingerprint")
            : new OrganizationAclPrincipalGranted("tenant-a", "tenant-a", (OrganizationAclPrincipalKind)4,
                "user-a", "read_metadata", "corr", "task", "event-key", "event-fingerprint");

        Should.Throw<InvalidOperationException>(() => state.Apply([invalid]))
            .Message.ShouldBe("Organization ACL event principal kind is invalid.");
        state.Grants.ShouldBeEmpty();
        state.IdempotencyFingerprints.ShouldNotContainKey("event-key");
    }

    [Theory]
    [InlineData("grant", "Organization ACL event requires an initialized baseline.")]
    [InlineData("revoke", "Organization ACL event requires an initialized baseline.")]
    [InlineData("repeated-baseline", "Organization baseline is already initialized.")]
    [InlineData("unknown", "Unknown organization event in replay.")]
    public void InvalidRetainedOrderAndUnknownEventsFailBeforeChangingOriginalState(string scenario, string message)
    {
        OrganizationAclBaselineInitialized baseline = new("tenant-a", "tenant-a", "correlation-a",
            "task-a", "baseline-key", "baseline-fingerprint");
        OrganizationAclPrincipalGranted grant = new("tenant-a", "tenant-a", OrganizationAclPrincipalKind.User,
            "user-a", "read_metadata", "correlation-a", "task-a", "grant-key", "grant-fingerprint");
        OrganizationAclPrincipalRevoked revoke = new("tenant-a", "tenant-a", OrganizationAclPrincipalKind.User,
            "user-a", "read_metadata", "correlation-a", "task-a", "revoke-key", "revoke-fingerprint");
        IOrganizationEvent unknown = new UnknownOrganizationEvent("tenant-a", "tenant-a", "correlation-a",
            "task-a", "unknown-key", "unknown-fingerprint");
        IOrganizationEvent[] history = scenario switch
        {
            "grant" => [grant],
            "revoke" => [revoke],
            "repeated-baseline" => [baseline, grant, baseline with { IdempotencyKey = "other-key" }],
            _ => [baseline, unknown],
        };

        Should.Throw<InvalidOperationException>(() => OrganizationState.Empty.Apply(history)).Message.ShouldBe(message);
        OrganizationState.Empty.IsInitialized.ShouldBeFalse();
        OrganizationState.Empty.Grants.ShouldBeEmpty();
        OrganizationState.Empty.IdempotencyFingerprints.ShouldBeEmpty();
    }
}
