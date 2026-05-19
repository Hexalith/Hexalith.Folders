namespace Hexalith.Folders.Aggregates.Folder;

public enum FolderResultCode
{
    Created,
    IdempotentReplay,
    IdempotencyConflict,
    IdempotencyUnavailable,
    DuplicateFolder,
    AppendConflict,
    InvalidFolderId,
    InvalidFolderMetadata,
    InvalidTenant,
    ReservedTenant,
    MissingAuthoritativeTenant,
    TenantAccessDenied,
    FolderAclDenied,
    AclEvidenceUnavailable,
    AclEvidenceMismatch,
    ValidationFailed,
    StaleProjection,
    UnavailableProjection,
    UnknownTenant,
    DisabledTenant,
    MalformedEvidence,
    TenantMismatch,
    ReplayConflict,
    StateTransitionInvalid,
}
