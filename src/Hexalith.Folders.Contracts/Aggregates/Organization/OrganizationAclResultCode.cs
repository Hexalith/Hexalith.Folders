namespace Hexalith.Folders.Aggregates.Organization;

public enum OrganizationAclResultCode
{
    Accepted,
    AlreadyApplied,
    DuplicateEntry,
    MissingEntry,
    UnsupportedAction,
    InvalidPrincipal,
    InvalidOrganization,
    InvalidTenant,
    ReservedTenant,
    TenantAccessDenied,
    StaleProjection,
    UnavailableProjection,
    UnknownTenant,
    DisabledTenant,
    MalformedEvidence,
    TenantMismatch,
    MissingAuthoritativeTenant,
    ReplayConflict,
    IdempotencyConflict,
}
