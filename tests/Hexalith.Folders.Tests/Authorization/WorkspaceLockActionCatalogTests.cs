using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Queries.Folders;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Authorization;

public sealed class WorkspaceLockActionCatalogTests
{
    [Fact]
    public void LockWorkspaceActionShouldRequireWritePermission()
    {
        EffectivePermissionsActionCatalog.IsSupported(WorkspaceLockAcquisitionService.ActionToken).ShouldBeTrue();

        IReadOnlyList<EffectivePermissionLevel> levels =
            EffectivePermissionsActionCatalog.ToPermissionLevels([WorkspaceLockAcquisitionService.ActionToken]);

        levels.ShouldBe([EffectivePermissionLevel.Write]);
    }

    [Fact]
    public void ReadWorkspaceLockActionShouldRequireReadPermission()
    {
        EffectivePermissionsActionCatalog.IsSupported(WorkspaceLockStatusQueryHandler.ActionToken).ShouldBeTrue();
        FolderAccessAction.IsSupported(WorkspaceLockStatusQueryHandler.ActionToken).ShouldBeTrue();

        IReadOnlyList<EffectivePermissionLevel> levels =
            EffectivePermissionsActionCatalog.ToPermissionLevels([WorkspaceLockStatusQueryHandler.ActionToken]);

        levels.ShouldBe([EffectivePermissionLevel.Read]);
    }

    [Fact]
    public void ReadWorkspaceStatusActionShouldRequireReadPermission()
    {
        EffectivePermissionsActionCatalog.IsSupported(WorkspaceStatusQueryHandler.ActionToken).ShouldBeTrue();
        FolderAccessAction.IsSupported(WorkspaceStatusQueryHandler.ActionToken).ShouldBeTrue();

        IReadOnlyList<EffectivePermissionLevel> levels =
            EffectivePermissionsActionCatalog.ToPermissionLevels([WorkspaceStatusQueryHandler.ActionToken]);

        levels.ShouldBe([EffectivePermissionLevel.Read]);
    }
}
