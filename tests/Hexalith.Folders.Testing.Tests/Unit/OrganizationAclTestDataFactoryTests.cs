using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Testing.Factories;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Testing.Tests.Unit;

public sealed class OrganizationAclTestDataFactoryTests
{
    [Fact]
    public void GrantShouldUseProductionAclValidation()
    {
        GrantOrganizationAclPrincipal command = OrganizationAclTestDataFactory.Grant(action: "read_metadata");

        OrganizationAclCommandValidator.Validate(command).Code.ShouldBe(OrganizationAclResultCode.Accepted);
    }

    [Fact]
    public void OrganizationStreamNameShouldUseProductionStreamShape()
    {
        OrganizationStreamName streamName = OrganizationAclTestDataFactory.OrganizationStreamName();

        streamName.Value.ShouldBe("tenant-a:organizations:organization-a");
    }

    [Fact]
    public void GrantShouldRejectInvalidInputsThroughProductionAclValidation()
    {
        Should.Throw<ArgumentException>(() => OrganizationAclTestDataFactory.Grant(action: "Read_Metadata"))
            .Message.ShouldContain(nameof(OrganizationAclResultCode.UnsupportedAction));
    }
}
