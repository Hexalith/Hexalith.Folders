using Hexalith.Folders.Aggregates.Organization;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Organization;

public sealed class OrganizationAclCommandValidationTests
{
    [Theory]
    [InlineData(OrganizationAclPrincipalKind.User)]
    [InlineData(OrganizationAclPrincipalKind.Group)]
    [InlineData(OrganizationAclPrincipalKind.Role)]
    [InlineData(OrganizationAclPrincipalKind.DelegatedServiceAgent)]
    public void GrantShouldAppendMetadataOnlyPrincipalGrantForEveryPrincipalKind(OrganizationAclPrincipalKind principalKind)
    {
        OrganizationState state = OrganizationState.Empty;
        GrantOrganizationAclPrincipal command = AclCommandFactory.Grant(principalKind: principalKind);

        OrganizationAclResult result = OrganizationAggregate.Handle(state, command);

        result.Code.ShouldBe(OrganizationAclResultCode.Accepted);
        result.Events.Count.ShouldBe(2);
        result.Events[0].ShouldBeOfType<OrganizationAclBaselineInitialized>();
        OrganizationAclPrincipalGranted grant = result.Events[1].ShouldBeOfType<OrganizationAclPrincipalGranted>();
        grant.ManagedTenantId.ShouldBe(command.ManagedTenantId);
        grant.OrganizationId.ShouldBe(command.OrganizationId);
        grant.PrincipalKind.ShouldBe(principalKind);
        grant.PrincipalId.ShouldBe(command.PrincipalId);
        grant.Action.ShouldBe(command.Action);
    }

    [Theory]
    [InlineData("read file content")]
    [InlineData("Read_File_Content")]
    [InlineData("read-file-content")]
    [InlineData("delete_repository")]
    public void GrantShouldRejectUnsupportedOrNonCanonicalActions(string action)
    {
        GrantOrganizationAclPrincipal command = AclCommandFactory.Grant(action: action);

        OrganizationAclResult result = OrganizationAggregate.Handle(OrganizationState.Empty, command);

        result.Code.ShouldBe(OrganizationAclResultCode.UnsupportedAction);
        result.Events.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("principal:with-colon")]
    [InlineData("principal\u0001with-control")]
    public void GrantShouldRejectMalformedPrincipalIds(string principalId)
    {
        GrantOrganizationAclPrincipal command = AclCommandFactory.Grant(principalId: principalId);

        OrganizationAclResult result = OrganizationAggregate.Handle(OrganizationState.Empty, command);

        result.Code.ShouldBe(OrganizationAclResultCode.InvalidPrincipal);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void GrantShouldRejectReservedManagedTenantBeforeEvents()
    {
        GrantOrganizationAclPrincipal command = AclCommandFactory.Grant(managedTenantId: " system ");

        OrganizationAclResult result = OrganizationAggregate.Handle(OrganizationState.Empty, command);

        result.Code.ShouldBe(OrganizationAclResultCode.ReservedTenant);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void RevokeMissingEntryShouldReturnStableMissingEntryCode()
    {
        RevokeOrganizationAclPrincipal command = AclCommandFactory.Revoke();

        OrganizationAclResult result = OrganizationAggregate.Handle(OrganizationState.Empty, command);

        result.Code.ShouldBe(OrganizationAclResultCode.MissingEntry);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void InitializeShouldCollapseExactDuplicateOperationsBeforeEvents()
    {
        OrganizationAclOperation operation = AclCommandFactory.Operation(OrganizationAclOperationIntent.Grant);
        InitializeOrganizationAclBaseline command = AclCommandFactory.Initialize(operation, operation);

        OrganizationAclResult result = OrganizationAggregate.Handle(OrganizationState.Empty, command);

        result.Code.ShouldBe(OrganizationAclResultCode.Accepted);
        result.Events.OfType<OrganizationAclPrincipalGranted>().Count().ShouldBe(1);
    }

    [Fact]
    public void InitializeShouldRejectConflictingSameTupleOperationsBeforeEvents()
    {
        OrganizationAclOperation grant = AclCommandFactory.Operation(OrganizationAclOperationIntent.Grant);
        OrganizationAclOperation revoke = grant with { Intent = OrganizationAclOperationIntent.Revoke };
        InitializeOrganizationAclBaseline command = AclCommandFactory.Initialize(grant, revoke);

        OrganizationAclResult result = OrganizationAggregate.Handle(OrganizationState.Empty, command);

        result.Code.ShouldBe(OrganizationAclResultCode.ReplayConflict);
        result.Events.ShouldBeEmpty();
    }
}
