namespace Hexalith.Folders.Authorization;

public static class EffectivePermissionsActionCatalog
{
    private static readonly string[] OrderedActions =
    [
        "configure_provider_binding",
        "prepare_workspace",
        "lock_workspace",
        "archive_folder",
        "read_metadata",
        "read_file_content",
        "mutate_files",
        "commit",
        "query_status",
        "query_audit",
        "view_operations_console",
        "create_folder",
    ];

    private static readonly Dictionary<string, EffectivePermissionLevel> PermissionByAction = new(StringComparer.Ordinal)
    {
        ["read_metadata"] = EffectivePermissionLevel.Read,
        ["read_file_content"] = EffectivePermissionLevel.Read,
        ["query_status"] = EffectivePermissionLevel.Read,
        ["query_audit"] = EffectivePermissionLevel.Read,
        ["view_operations_console"] = EffectivePermissionLevel.Read,
        ["prepare_workspace"] = EffectivePermissionLevel.Write,
        ["lock_workspace"] = EffectivePermissionLevel.Write,
        ["mutate_files"] = EffectivePermissionLevel.Write,
        ["commit"] = EffectivePermissionLevel.Write,
        ["archive_folder"] = EffectivePermissionLevel.Administer,
        ["configure_provider_binding"] = EffectivePermissionLevel.Administer,
        ["create_folder"] = EffectivePermissionLevel.Administer,
    };

    public static bool IsSupported(string action)
        => !string.IsNullOrWhiteSpace(action)
            && action.Length == action.AsSpan().Trim().Length
            && PermissionByAction.ContainsKey(action);

    public static IReadOnlyList<EffectivePermissionLevel> ToPermissionLevels(IEnumerable<string> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        HashSet<EffectivePermissionLevel> levels = [];
        foreach (string action in actions)
        {
            if (PermissionByAction.TryGetValue(action, out EffectivePermissionLevel level))
            {
                levels.Add(level);
            }
        }

        return levels
            .OrderBy(static level => level switch
            {
                EffectivePermissionLevel.Read => 0,
                EffectivePermissionLevel.Write => 1,
                EffectivePermissionLevel.Administer => 2,
                _ => 3,
            })
            .ToArray();
    }

    public static int CompareActions(string left, string right)
    {
        int leftIndex = Array.IndexOf(OrderedActions, left);
        int rightIndex = Array.IndexOf(OrderedActions, right);

        leftIndex = leftIndex < 0 ? int.MaxValue : leftIndex;
        rightIndex = rightIndex < 0 ? int.MaxValue : rightIndex;

        int result = leftIndex.CompareTo(rightIndex);
        return result == 0 ? string.CompareOrdinal(left, right) : result;
    }
}
