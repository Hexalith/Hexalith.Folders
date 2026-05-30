using System.Security.Claims;

namespace Hexalith.Folders.Server.Authentication;

public sealed class TenantContextOptions
{
    public const string SectionName = "Folders:TenantContext";

    public const string EventStoreTenantClaimType = "eventstore:tenant";

    public const string SubjectClaimType = "sub";

    public string TenantClaimType { get; set; } = EventStoreTenantClaimType;

    public string PrincipalClaimType { get; set; } = SubjectClaimType;
}
