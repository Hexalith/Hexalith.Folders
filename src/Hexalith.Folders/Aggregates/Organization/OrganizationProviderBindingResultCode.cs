namespace Hexalith.Folders.Aggregates.Organization;

public enum OrganizationProviderBindingResultCode
{
    Accepted,
    AlreadyApplied,
    DuplicateConflict,
    UnsupportedProviderKind,
    InvalidProviderBindingReference,
    InvalidCredentialReference,
    InvalidPolicy,
    InvalidOrganization,
    InvalidTenant,
    ReservedTenant,
    MissingPermission,
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
    ForbiddenCredentialMaterial,
}
