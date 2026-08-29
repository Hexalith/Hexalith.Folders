using Hexalith.Folders.Authorization;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Authorization;

public sealed class EffectivePermissionsActionCatalogTests
{
    [Fact]
    public void CompareActionsShouldRestoreCanonicalCatalogOrder()
    {
        string[] expectedActions =
        [
            "configure_provider_binding",
            "configure_branch_ref_policy",
            "bind_repository",
            "create_repository_backed_folder",
            "provider_readiness_read",
            "manage_folder_access",
            "prepare_workspace",
            "lock_workspace",
            "read_workspace_lock",
            "read_workspace_status",
            "read_workspace_cleanup_status",
            "archive_folder",
            "read_branch_ref_policy",
            "read_metadata",
            "read_file_content",
            "mutate_files",
            "commit",
            "query_status",
            "query_audit",
            "read_context_search",
            "view_operations_console",
            "create_folder",
        ];
        IReadOnlyList<string> productionActions = EffectivePermissionsActionCatalog.OrderedActionsForTesting;
        string[] reorderedActions = [.. productionActions.Reverse()];

        Array.Sort(reorderedActions, EffectivePermissionsActionCatalog.CompareActions);

        productionActions.ShouldBe(expectedActions);
        productionActions.Count.ShouldBe(EffectivePermissionsActionCatalog.MappedActionCountForTesting);
        productionActions.Distinct(StringComparer.Ordinal).Count().ShouldBe(productionActions.Count);
        foreach (string action in productionActions)
        {
            EffectivePermissionsActionCatalog.HasPermissionMappingForTesting(action).ShouldBeTrue();
        }

        reorderedActions.ShouldBe(expectedActions);
    }
}
