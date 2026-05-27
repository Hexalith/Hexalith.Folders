using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Authorization;

public sealed class BranchRefPolicyActionCatalogTests
{
    [Fact]
    public void ConfigureBranchRefPolicyActionShouldRequireAdministerPermission()
    {
        EffectivePermissionsActionCatalog.IsSupported(BranchRefPolicyConfigurationService.ActionToken).ShouldBeTrue();

        IReadOnlyList<EffectivePermissionLevel> levels =
            EffectivePermissionsActionCatalog.ToPermissionLevels([BranchRefPolicyConfigurationService.ActionToken]);

        levels.ShouldBe([EffectivePermissionLevel.Administer]);
    }

    [Fact]
    public void ReadBranchRefPolicyActionShouldRequireReadPermission()
    {
        EffectivePermissionsActionCatalog.IsSupported("read_branch_ref_policy").ShouldBeTrue();

        IReadOnlyList<EffectivePermissionLevel> levels =
            EffectivePermissionsActionCatalog.ToPermissionLevels(["read_branch_ref_policy"]);

        levels.ShouldBe([EffectivePermissionLevel.Read]);
    }
}
