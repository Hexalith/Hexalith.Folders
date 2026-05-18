using Hexalith.Folders.Aggregates.Organization;
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
}
