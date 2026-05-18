using Hexalith.Folders.Aggregates.Organization;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Organization;

public sealed class OrganizationAclIdempotencyTests
{
    [Fact]
    public void SameIdempotencyKeyAndEquivalentPayloadShouldNotAppendDuplicateEvents()
    {
        GrantOrganizationAclPrincipal command = AclCommandFactory.Grant(idempotencyKey: "idem-a");
        OrganizationAclResult first = OrganizationAggregate.Handle(OrganizationState.Empty, command);
        OrganizationState state = OrganizationState.Empty.Apply(first.Events);

        OrganizationAclResult replay = OrganizationAggregate.Handle(state, command);

        replay.Code.ShouldBe(OrganizationAclResultCode.AlreadyApplied);
        replay.Events.ShouldBeEmpty();
        replay.IdempotencyKey.ShouldBe("idem-a");
    }

    [Fact]
    public void SameIdempotencyKeyAndDifferentPayloadShouldRejectAsConflict()
    {
        GrantOrganizationAclPrincipal command = AclCommandFactory.Grant(idempotencyKey: "idem-a", principalId: "principal-a");
        OrganizationState state = OrganizationState.Empty.Apply(OrganizationAggregate.Handle(OrganizationState.Empty, command).Events);

        OrganizationAclResult conflict = OrganizationAggregate.Handle(
            state,
            AclCommandFactory.Grant(idempotencyKey: "idem-a", principalId: "principal-b"));

        conflict.Code.ShouldBe(OrganizationAclResultCode.IdempotencyConflict);
        conflict.Events.ShouldBeEmpty();
    }

    [Fact]
    public void DifferentIdempotencyKeyForAlreadyPresentGrantShouldReturnNoOpEvidence()
    {
        OrganizationState state = OrganizationState.Empty.Apply(
            OrganizationAggregate.Handle(OrganizationState.Empty, AclCommandFactory.Grant(idempotencyKey: "idem-a")).Events);

        OrganizationAclResult result = OrganizationAggregate.Handle(
            state,
            AclCommandFactory.Grant(idempotencyKey: "idem-b"));

        result.Code.ShouldBe(OrganizationAclResultCode.AlreadyApplied);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void GrantAndRevokeWithSameKeyShouldNeverShareEquivalenceClass()
    {
        OrganizationState state = OrganizationState.Empty.Apply(
            OrganizationAggregate.Handle(OrganizationState.Empty, AclCommandFactory.Grant(idempotencyKey: "idem-a")).Events);

        OrganizationAclResult result = OrganizationAggregate.Handle(
            state,
            AclCommandFactory.Revoke(idempotencyKey: "idem-a"));

        result.Code.ShouldBe(OrganizationAclResultCode.IdempotencyConflict);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void ReorderedExactDuplicateEntriesShouldBeIdempotencyEquivalent()
    {
        OrganizationAclOperation first = AclCommandFactory.Operation(OrganizationAclOperationIntent.Grant, principalId: "principal-a");
        OrganizationAclOperation second = AclCommandFactory.Operation(OrganizationAclOperationIntent.Grant, principalId: "principal-b");
        InitializeOrganizationAclBaseline command = AclCommandFactory.Initialize("idem-a", first, first, second);
        OrganizationState state = OrganizationState.Empty.Apply(OrganizationAggregate.Handle(OrganizationState.Empty, command).Events);

        InitializeOrganizationAclBaseline replay = AclCommandFactory.Initialize("idem-a", second, first);
        OrganizationAclResult result = OrganizationAggregate.Handle(state, replay);

        result.Code.ShouldBe(OrganizationAclResultCode.AlreadyApplied);
        result.Events.ShouldBeEmpty();
    }
}
