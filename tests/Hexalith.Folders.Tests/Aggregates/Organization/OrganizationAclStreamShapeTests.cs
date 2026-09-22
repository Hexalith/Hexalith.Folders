using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Organization;

public sealed class OrganizationAclStreamShapeTests
{
    [Fact]
    public void StreamNameShouldUseManagedTenantOrganizationShape()
    {
        OrganizationStreamName streamName = OrganizationStreamName.Create("tenant-a", "organization-a");

        streamName.Value.ShouldBe("tenant-a:organizations:organization-a");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("tenant:a")]
    [InlineData("tenant\u0001a")]
    [InlineData("Tenant-A")]
    public void StreamNameShouldRejectInvalidManagedTenantSegments(string tenantId)
    {
        ArgumentNullException.ThrowIfNull(tenantId);

        OrganizationStreamName.TryCreate(tenantId, "organization-a", out _, out OrganizationAclResultCode code).ShouldBeFalse();

        code.ShouldBe(tenantId.Trim().Equals("system", StringComparison.OrdinalIgnoreCase)
            ? OrganizationAclResultCode.ReservedTenant
            : OrganizationAclResultCode.InvalidTenant);
    }

    [Theory]
    [InlineData("system")]
    [InlineData(" System ")]
    public void StreamNameShouldRejectReservedSystemTenant(string tenantId)
    {
        OrganizationStreamName.TryCreate(tenantId, "organization-a", out _, out OrganizationAclResultCode code).ShouldBeFalse();

        code.ShouldBe(OrganizationAclResultCode.ReservedTenant);
    }

    [Fact]
    public void UppercaseOpaqueIdentifierRemainsValidAfterSeamStateEnds()
    {
        const string organizationId = "Organization_00000001";
        PreauthorizedRequestContext.Begin(new PreauthorizedRequestState(
            "tenant-a",
            "user-a",
            FolderId: null,
            FreshnessWatermark: null,
            OrganizationId: organizationId,
            CandidateActionToken: "manage_folder_access",
            HistoricalActionToken: "create_repository_backed_folder",
            DelegatorPrincipalId: null));
        try
        {
            PreauthorizedRequestContext.IsCandidateOpaqueIdentifier(organizationId).ShouldBeTrue();
        }
        finally
        {
            PreauthorizedRequestContext.End();
        }

        PreauthorizedRequestContext.Current.ShouldBeNull();
        OrganizationStreamName.TryCreate(
                "tenant-a",
                organizationId,
                out OrganizationStreamName? streamName,
                out OrganizationAclResultCode code)
            .ShouldBeTrue();
        code.ShouldBe(OrganizationAclResultCode.Accepted);
        streamName!.Value.ShouldBe($"tenant-a:organizations:{organizationId}");
        OrganizationAclCommandValidator.IsValidIdentifier(organizationId).ShouldBeTrue();
    }
}
