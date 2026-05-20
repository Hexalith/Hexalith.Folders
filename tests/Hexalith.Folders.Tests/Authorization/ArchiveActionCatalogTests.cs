using Hexalith.Folders.Authorization;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Authorization;

public sealed class ArchiveActionCatalogTests
{
    [Fact]
    public void ArchiveFolderActionShouldRequireAdministerPermission()
    {
        EffectivePermissionsActionCatalog.IsSupported("archive_folder").ShouldBeTrue();

        IReadOnlyList<EffectivePermissionLevel> levels = EffectivePermissionsActionCatalog.ToPermissionLevels(["archive_folder"]);

        levels.ShouldBe([EffectivePermissionLevel.Administer]);
    }

    [Fact]
    public void FolderAclMutationActionShouldRequireAdministerPermission()
    {
        EffectivePermissionsActionCatalog.IsSupported("manage_folder_access").ShouldBeTrue();

        IReadOnlyList<EffectivePermissionLevel> levels = EffectivePermissionsActionCatalog.ToPermissionLevels(["manage_folder_access"]);

        levels.ShouldBe([EffectivePermissionLevel.Administer]);
    }
}
