namespace Hexalith.Folders.Testing.Factories;

public sealed record TestAuthorizationContext(
    string Subject,
    string ManagedTenantId,
    IReadOnlyList<string> Permissions)
{
    public string TenantClaimJson => $"[\"{ManagedTenantId}\"]";
}
