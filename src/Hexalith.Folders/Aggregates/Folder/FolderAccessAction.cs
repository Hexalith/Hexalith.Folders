namespace Hexalith.Folders.Aggregates.Folder;

public static class FolderAccessAction
{
    private static readonly HashSet<string> SupportedValues = new(StringComparer.Ordinal)
    {
        "configure_provider_binding",
        "manage_folder_access",
        "archive_folder",
        "prepare_workspace",
        "lock_workspace",
        "read_workspace_lock",
        "read_workspace_status",
        "read_metadata",
        "read_file_content",
        "mutate_files",
        "commit",
        "query_status",
        "query_audit",
        "view_operations_console",
    };

    public static bool IsSupported(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && string.Equals(value, value.Trim(), StringComparison.Ordinal)
            && SupportedValues.Contains(value);
}
