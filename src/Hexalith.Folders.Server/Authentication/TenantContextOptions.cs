using System.Security.Claims;

namespace Hexalith.Folders.Server.Authentication;

public sealed class TenantContextOptions
{
    public const string SectionName = "Folders:TenantContext";

    public string TenantClaimType { get; set; } = "tenant_id";

    public string PrincipalClaimType { get; set; } = ClaimTypes.NameIdentifier;
}
