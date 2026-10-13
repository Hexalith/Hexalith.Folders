using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Testing.Factories;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Testing.Tests.Unit;

public sealed class FolderCreationTestDataFactoryTests
{
    [Fact]
    public void CreateShouldReturnProductionValidatedCommand()
    {
        CreateFolder command = FolderCreationTestDataFactory.Create();

        FolderCommandValidator.Validate(command).IsAccepted.ShouldBeTrue();
    }

    [Fact]
    public void TenantOverrideShouldAlsoSupplyTheDefaultOrganizationIdentity()
    {
        CreateFolder command = FolderCreationTestDataFactory.Create(managedTenantId: "tenant-b");

        command.OrganizationId.ShouldBe("tenant-b");
        FolderCommandValidator.Validate(command).IsAccepted.ShouldBeTrue();
        Should.Throw<ArgumentException>(() => FolderCreationTestDataFactory.Create(
            managedTenantId: "tenant-b", organizationId: "tenant-a"));
    }

    [Fact]
    public void CreateShouldRejectUnsafeDefaultsOverrides()
    {
        Should.Throw<ArgumentException>(
            () => FolderCreationTestDataFactory.Create(displayName: "github_pat_credential_material"));
    }

    [Fact]
    public void FolderStreamNameShouldUseProductionStreamShape()
    {
        FolderCreationTestDataFactory.FolderStreamName("tenant-a", "folder-a").Value
            .ShouldBe("tenant-a:folders:folder-a");
    }
}
