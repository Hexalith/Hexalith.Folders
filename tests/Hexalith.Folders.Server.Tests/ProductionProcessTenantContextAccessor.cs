using Hexalith.Folders.Server.Authentication;

namespace Hexalith.Folders.Server.Tests;

internal sealed class ProductionProcessTenantContextAccessor : ITenantContextAccessor
{
    public string? AuthoritativeTenantId => "tenant-a";

    public string? PrincipalId => "user-a";
}
