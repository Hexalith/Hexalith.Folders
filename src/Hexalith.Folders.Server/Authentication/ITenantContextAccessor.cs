namespace Hexalith.Folders.Server.Authentication;

public interface ITenantContextAccessor
{
    string? AuthoritativeTenantId { get; }

    string? PrincipalId { get; }
}
