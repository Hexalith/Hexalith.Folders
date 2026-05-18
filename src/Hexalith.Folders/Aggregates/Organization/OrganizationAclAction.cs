namespace Hexalith.Folders.Aggregates.Organization;

public static class OrganizationAclAction
{
    private static readonly HashSet<string> SupportedValues = new(StringComparer.Ordinal)
    {
        "create_folder",
        "configure_provider_binding",
        "prepare_workspace",
        "lock_workspace",
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
