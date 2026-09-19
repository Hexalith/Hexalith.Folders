namespace Hexalith.Folders.Server.Authorization;

/// <summary>PD10 v2 access-state denominator used by the unrouted candidate authorization seam.</summary>
internal enum V2AccessState
{
    TenantAdministrator,
    TenantMember,
    DelegatedServiceAgent,
    TenantScopedOperator,
    AuditReviewer,
    IncidentAdministrator,
    WrongTenant,
    Revoked,
    Stale,
    Disabled,
    Unknown,
    HiddenResource,
    AbsentResource,
    InsufficientScope,
}
