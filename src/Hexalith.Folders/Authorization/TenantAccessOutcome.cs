namespace Hexalith.Folders.Authorization;

public enum TenantAccessOutcome
{
    Allowed,
    Denied,
    StaleProjection,
    UnavailableProjection,
    UnknownTenant,
    DisabledTenant,
    MalformedEvidence,
    TenantMismatch,
    MissingAuthoritativeTenant,
    ReplayConflict,
}
