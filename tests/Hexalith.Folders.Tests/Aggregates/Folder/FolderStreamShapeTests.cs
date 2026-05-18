using Hexalith.Folders.Aggregates.Folder;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderStreamShapeTests
{
    [Fact]
    public void StreamNameShouldUseAuthoritativeTenantAndOpaqueFolderId()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");

        streamName.Value.ShouldBe("tenant-a:folders:folder-a");
    }

    [Theory]
    [InlineData("system", "folder-a", FolderResultCode.ReservedTenant)]
    [InlineData("System", "folder-a", FolderResultCode.ReservedTenant)]
    [InlineData("tenant:a", "folder-a", FolderResultCode.InvalidTenant)]
    [InlineData("Tenant-A", "folder-a", FolderResultCode.InvalidTenant)]
    [InlineData("tenant-a", "folder:a", FolderResultCode.InvalidFolderId)]
    [InlineData("tenant-a", "Folder-A", FolderResultCode.InvalidFolderId)]
    public void InvalidSegmentsShouldRejectBeforeStreamNameIsCreated(
        string managedTenantId,
        string folderId,
        FolderResultCode expectedCode)
    {
        bool created = FolderStreamName.TryCreate(managedTenantId, folderId, out FolderStreamName? _, out FolderResultCode code);

        created.ShouldBeFalse();
        code.ShouldBe(expectedCode);
    }
}
