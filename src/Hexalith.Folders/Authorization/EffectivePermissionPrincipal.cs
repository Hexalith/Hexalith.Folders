namespace Hexalith.Folders.Authorization;

public sealed record EffectivePermissionPrincipal(
    EffectivePermissionPrincipalKind Kind,
    string PrincipalId)
{
    public static EffectivePermissionPrincipal User(string principalId)
        => new(EffectivePermissionPrincipalKind.User, principalId);

    public static EffectivePermissionPrincipal Group(string principalId)
        => new(EffectivePermissionPrincipalKind.Group, principalId);

    public static EffectivePermissionPrincipal Role(string principalId)
        => new(EffectivePermissionPrincipalKind.Role, principalId);

    public static EffectivePermissionPrincipal DelegatedServiceAgent(string principalId)
        => new(EffectivePermissionPrincipalKind.DelegatedServiceAgent, principalId);
}
