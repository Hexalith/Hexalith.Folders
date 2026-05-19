namespace Hexalith.Folders.Authorization;

public static class AuthorizationOrder
{
    public static IReadOnlyList<string> EffectivePermissions { get; } =
    [
        "authoritative_tenant_context",
        "client_tenant_comparison",
        "tenant_access_projection",
        "folder_permission_projection",
        "task_scope_narrowing",
    ];
}
