using Hexalith.Folders.Aggregates.Organization;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Organization;

public sealed class OrganizationAclEffectivePermissionTests
{
    [Fact]
    public void StateShouldDerivePermissionsByTenantOrganizationKindPrincipalAndAction()
    {
        OrganizationState state = OrganizationState.Empty;
        OrganizationAclResult grantUser = OrganizationAggregate.Handle(
            state,
            AclCommandFactory.Grant(principalKind: OrganizationAclPrincipalKind.User, principalId: "subject-a"));
        state = state.Apply(grantUser.Events);

        OrganizationAclResult grantGroup = OrganizationAggregate.Handle(
            state,
            AclCommandFactory.Grant(
                principalKind: OrganizationAclPrincipalKind.Group,
                principalId: "subject-a",
                idempotencyKey: "idempotency-b"));
        state = state.Apply(grantGroup.Events);

        state.HasPermission("tenant-a", "organization-a", OrganizationAclPrincipalKind.User, "subject-a", "read_metadata").ShouldBeTrue();
        state.HasPermission("tenant-a", "organization-a", OrganizationAclPrincipalKind.Group, "subject-a", "read_metadata").ShouldBeTrue();
        state.HasPermission("tenant-a", "organization-a", OrganizationAclPrincipalKind.Role, "subject-a", "read_metadata").ShouldBeFalse();
    }

    [Fact]
    public void RevokeShouldRemoveOnlyMatchingPrincipalKindTuple()
    {
        OrganizationState state = OrganizationState.Empty
            .Apply(OrganizationAggregate.Handle(
                OrganizationState.Empty,
                AclCommandFactory.Grant(principalKind: OrganizationAclPrincipalKind.User, principalId: "same-id")).Events);
        state = state.Apply(OrganizationAggregate.Handle(
            state,
            AclCommandFactory.Grant(
                principalKind: OrganizationAclPrincipalKind.Group,
                principalId: "same-id",
                idempotencyKey: "idempotency-b")).Events);

        OrganizationAclResult revokeUser = OrganizationAggregate.Handle(
            state,
            AclCommandFactory.Revoke(
                principalKind: OrganizationAclPrincipalKind.User,
                principalId: "same-id",
                idempotencyKey: "idempotency-c"));
        state = state.Apply(revokeUser.Events);

        state.HasPermission("tenant-a", "organization-a", OrganizationAclPrincipalKind.User, "same-id", "read_metadata").ShouldBeFalse();
        state.HasPermission("tenant-a", "organization-a", OrganizationAclPrincipalKind.Group, "same-id", "read_metadata").ShouldBeTrue();
    }
}
