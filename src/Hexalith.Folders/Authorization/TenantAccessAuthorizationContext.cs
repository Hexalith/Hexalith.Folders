namespace Hexalith.Folders.Authorization;

public sealed record TenantAccessAuthorizationContext(
    string? AuthoritativeTenantId,
    string PrincipalId,
    string? RequestedTenantId);
