using System.Text.Json;

namespace Hexalith.Folders.Testing.Factories;

public sealed record TestAuthorizationContext(
    string Subject,
    string ManagedTenantId,
    IReadOnlyList<string> Permissions)
{
    public string TenantClaimJson => JsonSerializer.Serialize(new[] { ManagedTenantId });
}
