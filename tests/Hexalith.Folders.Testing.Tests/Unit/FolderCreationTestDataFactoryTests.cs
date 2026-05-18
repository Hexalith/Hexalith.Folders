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
