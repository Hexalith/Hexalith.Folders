using Hexalith.Folders.Authorization;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Tests.Authorization;

/// <summary>
/// Pins gap <c>G4</c> of the authorization matrix to the deployed effective-permission action catalog. The matrix is
/// approval-bound, so a catalog change that fixes a recorded drift or adds a new one fails here and needs a governed
/// matrix revision.
/// </summary>
public sealed class EffectivePermissionsActionCatalogMatrixGapTests
{
    private const string MatrixRelativePath = "docs/contract/authorization-matrix.md";
    private const string CatalogRelativePath = "src/Hexalith.Folders/Authorization/EffectivePermissionsActionCatalog.cs";

    private static readonly string RepositoryRoot = FindRepositoryRoot();

    // The actions the approved G4 row counts.
    private static readonly string[] G4RecordedActions =
    [
        "archive_folder",
        "bind_repository",
        "commit",
        "configure_branch_ref_policy",
        "configure_provider_binding",
        "create_folder",
        "create_repository_backed_folder",
        "lock_workspace",
        "manage_folder_access",
        "mutate_files",
        "prepare_workspace",
        "provider_readiness_read",
        "query_audit",
        "query_status",
        "read_branch_ref_policy",
        "read_context_search",
        "read_file_content",
        "read_metadata",
        "read_workspace_cleanup_status",
        "read_workspace_lock",
        "read_workspace_status",
        "view_operations_console",
    ];

    // Added after the matrix 2.0.0 approval. G4 still counts 22 actions until the next governed matrix revision.
    private static readonly string[] ActionsAddedAfterG4 = ["manage_organization_acl", "read_task_status"];

    // Every action's reviewed level. A level change on any action is either a G4 fix or a new drift, so it must be
    // reviewed against the matrix before this table changes.
    private static readonly Dictionary<string, EffectivePermissionLevel> ReviewedLevels = new(StringComparer.Ordinal)
    {
        ["configure_provider_binding"] = EffectivePermissionLevel.Administer,
        ["configure_branch_ref_policy"] = EffectivePermissionLevel.Administer,
        ["bind_repository"] = EffectivePermissionLevel.Write,
        ["create_repository_backed_folder"] = EffectivePermissionLevel.Write,
        ["provider_readiness_read"] = EffectivePermissionLevel.Read,
        ["manage_folder_access"] = EffectivePermissionLevel.Administer,
        ["manage_organization_acl"] = EffectivePermissionLevel.Administer,
        ["prepare_workspace"] = EffectivePermissionLevel.Write,
        ["lock_workspace"] = EffectivePermissionLevel.Write,
        ["read_workspace_lock"] = EffectivePermissionLevel.Read,
        ["read_workspace_status"] = EffectivePermissionLevel.Read,
        ["read_task_status"] = EffectivePermissionLevel.Read,
        ["read_workspace_cleanup_status"] = EffectivePermissionLevel.Read,
        ["archive_folder"] = EffectivePermissionLevel.Administer,
        ["read_branch_ref_policy"] = EffectivePermissionLevel.Read,
        ["read_metadata"] = EffectivePermissionLevel.Read,
        ["read_file_content"] = EffectivePermissionLevel.Read,
        ["mutate_files"] = EffectivePermissionLevel.Write,
        ["commit"] = EffectivePermissionLevel.Write,
        ["query_status"] = EffectivePermissionLevel.Read,
        ["query_audit"] = EffectivePermissionLevel.Read,
        ["read_context_search"] = EffectivePermissionLevel.Read,
        ["view_operations_console"] = EffectivePermissionLevel.Read,
        ["create_folder"] = EffectivePermissionLevel.Administer,
    };

    [Fact]
    public void G4ShouldCiteTheCatalogAndCountItsApprovedActions()
    {
        string row = ReadG4Row();

        row.ShouldContain($"`{CatalogRelativePath}`");
        row.ShouldContain($"maps {G4RecordedActions.Length} actions onto three folder levels");
    }

    [Fact]
    public void CatalogShouldHoldTheG4ActionsPlusOnlyTheKnownLaterAdditions()
        => EffectivePermissionsActionCatalog.OrderedActionsForTesting
            .Order(StringComparer.Ordinal)
            .ShouldBe([.. G4RecordedActions.Concat(ActionsAddedAfterG4).Order(StringComparer.Ordinal)]);

    [Theory]
    [InlineData("read_file_content", EffectivePermissionLevel.Read, "its file-content read and context-search actions map to read")]
    [InlineData("read_context_search", EffectivePermissionLevel.Read, "its file-content read and context-search actions map to read")]
    [InlineData("bind_repository", EffectivePermissionLevel.Write, "its repository-bind and repository-backed-folder-create actions map to write")]
    [InlineData("create_repository_backed_folder", EffectivePermissionLevel.Write, "its repository-bind and repository-backed-folder-create actions map to write")]
    [InlineData("create_folder", EffectivePermissionLevel.Administer, "its folder-create action maps to administer")]
    [InlineData("configure_provider_binding", EffectivePermissionLevel.Administer, "its provider-binding-configure action maps to administer")]
    [InlineData("provider_readiness_read", EffectivePermissionLevel.Read, "its provider-readiness-read action maps to read")]
    public void CatalogShouldStillCarryTheDriftG4Records(string action, EffectivePermissionLevel level, string g4Claim)
    {
        ReadG4Row().ShouldContain(g4Claim);
        EffectivePermissionsActionCatalog.ToPermissionLevels([action]).ShouldBe([level]);
    }

    [Fact]
    public void CatalogShouldKeepEveryActionAtItsReviewedLevel()
    {
        ReviewedLevels.Keys.Order(StringComparer.Ordinal)
            .ShouldBe(EffectivePermissionsActionCatalog.OrderedActionsForTesting.Order(StringComparer.Ordinal));
        foreach ((string action, EffectivePermissionLevel level) in ReviewedLevels)
        {
            EffectivePermissionsActionCatalog.ToPermissionLevels([action]).ShouldBe([level], action);
        }
    }

    [Fact]
    public void CatalogShouldCarryNoRoleOrPermissionActionG4RecordsAsMissing()
    {
        ReadG4Row().ShouldContain(
            "the catalog carries no audit-reviewer role, operator permission, incident-admin permission, or incident-evidence action");
        foreach (string action in EffectivePermissionsActionCatalog.OrderedActionsForTesting)
        {
            action.ShouldNotContain("reviewer", Case.Insensitive);
            action.ShouldNotContain("operator", Case.Insensitive);
            action.ShouldNotContain("incident", Case.Insensitive);
        }
    }

    private static string ReadG4Row()
    {
        string[] rows =
        [
            .. File.ReadAllLines(Path.Combine(RepositoryRoot, MatrixRelativePath.Replace('/', Path.DirectorySeparatorChar)))
                .Where(static line => line.StartsWith("| `G4` |", StringComparison.Ordinal)),
        ];
        rows.Length.ShouldBe(1, "The authorization matrix must record gap G4 exactly once.");
        return rows[0];
    }

    private static string FindRepositoryRoot()
    {
        string current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "Hexalith.Folders.slnx")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName ?? string.Empty;
        }

        throw new InvalidOperationException("Unable to locate the repository root from the test base directory.");
    }
}
