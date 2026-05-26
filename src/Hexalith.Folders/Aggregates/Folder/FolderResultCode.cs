using System.Text.Json.Serialization;

namespace Hexalith.Folders.Aggregates.Folder;

// Name-based JSON conversion is mandatory so the integer ordinal of these members remains
// internal implementation detail. Wire shape (rejection events, parity fixtures, log
// records, problem details) must serialize the enum NAME. This keeps the contract
// stable when members are inserted, renamed, or renumbered.
[JsonConverter(typeof(JsonStringEnumConverter<FolderResultCode>))]
public enum FolderResultCode
{
    Accepted,
    Created,
    IdempotentReplay,
    AlreadyApplied,
    AlreadyArchived,
    ArchivePolicyDenied,
    MissingEntry,
    DuplicateEntry,
    ConflictingEntry,
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
    AclEvidenceForeignFolder,
    AclEvidenceUnsupportedAction,
    PolicyEvidenceUnavailable,
    PolicyEvidenceStale,
    PolicyEvidenceMalformed,
    PolicyEvidenceScopeMismatch,
    ProviderReadinessFailed,
    UnsupportedProviderCapability,
    ProviderUnavailable,
    ProviderPermissionInsufficient,
    RepositoryConflict,
    UnknownProviderOutcome,
    ReconciliationRequired,
    FolderNotFound,
    UnsupportedAction,
    InvalidPrincipal,
    ValidationFailed,
    MalformedJsonPayload,
    StaleProjection,
    UnavailableProjection,
    UnknownTenant,
    DisabledTenant,
    MalformedEvidence,
    TenantMismatch,
    ReplayConflict,
    StateTransitionInvalid,
    UnsupportedCommandType,
}
