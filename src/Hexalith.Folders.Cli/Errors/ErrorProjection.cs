using Hexalith.Folders.Client.Generated;

namespace Hexalith.Folders.Cli.Errors;

/// <summary>
/// The single canonical <see cref="CanonicalErrorCategory"/> → CLI exit-code projection used by every
/// command. Encoded verbatim from the deduplicated <c>outcome_mapping</c> rows of the parity oracle
/// (<c>tests/fixtures/parity-contract.yaml</c>); Story 5.4 proves this map against that oracle.
/// </summary>
/// <remarks>
/// Distinct categories are never collapsed for adapter convenience (project-context Critical Don't-Miss
/// rule). Any category absent from the oracle <c>outcome_mapping</c> — including the SDK enum member
/// <c>range_unsatisfiable</c>, which the oracle does not list — falls through to
/// <see cref="FoldersExitCodes.InternalError"/> (1), the documented fallback for spine/oracle drift.
/// </remarks>
internal static class ErrorProjection
{
    /// <summary>
    /// Projects a server-returned canonical error category to its Folders CLI exit code.
    /// </summary>
    /// <param name="category">The category carried by the server's RFC 9457 problem response.</param>
    /// <returns>The canonical exit code for the category.</returns>
    public static int Project(CanonicalErrorCategory category) => category switch
    {
        CanonicalErrorCategory.Success => FoldersExitCodes.Success,

        // Credential family → 65 (authentication failures map here, not to a separate auth code).
        CanonicalErrorCategory.Authentication_failure => FoldersExitCodes.CredentialMissing,
        CanonicalErrorCategory.Credential_missing => FoldersExitCodes.CredentialMissing,
        CanonicalErrorCategory.Credential_reference_invalid => FoldersExitCodes.CredentialMissing,

        // Pre-SDK usage error category, should the server ever echo it → 64.
        CanonicalErrorCategory.Client_configuration_error => FoldersExitCodes.UsageError,

        // Tenant / folder / audit access denial → 66.
        CanonicalErrorCategory.Tenant_access_denied => FoldersExitCodes.AccessDenied,
        CanonicalErrorCategory.Cross_tenant_access_denied => FoldersExitCodes.AccessDenied,
        CanonicalErrorCategory.Folder_acl_denied => FoldersExitCodes.AccessDenied,
        CanonicalErrorCategory.Audit_access_denied => FoldersExitCodes.AccessDenied,

        // Lock contention → 67.
        CanonicalErrorCategory.Workspace_locked => FoldersExitCodes.LockConflict,
        CanonicalErrorCategory.Lock_conflict => FoldersExitCodes.LockConflict,
        CanonicalErrorCategory.Lock_expired => FoldersExitCodes.LockConflict,
        CanonicalErrorCategory.Lock_not_owned => FoldersExitCodes.LockConflict,
        CanonicalErrorCategory.Stale_workspace => FoldersExitCodes.LockConflict,

        // Idempotency conflict → 68.
        CanonicalErrorCategory.Idempotency_conflict => FoldersExitCodes.IdempotencyConflict,

        // Validation / input-shape → 69.
        CanonicalErrorCategory.Validation_error => FoldersExitCodes.ValidationError,
        CanonicalErrorCategory.Input_limit_exceeded => FoldersExitCodes.ValidationError,
        CanonicalErrorCategory.Path_validation_failed => FoldersExitCodes.ValidationError,
        CanonicalErrorCategory.Branch_ref_policy_invalid => FoldersExitCodes.ValidationError,
        CanonicalErrorCategory.Response_limit_exceeded => FoldersExitCodes.ValidationError,

        // Provider / repository operation failures → 70.
        CanonicalErrorCategory.Provider_failure_known => FoldersExitCodes.ProviderFailure,
        CanonicalErrorCategory.Provider_unavailable => FoldersExitCodes.ProviderFailure,
        CanonicalErrorCategory.Provider_rate_limited => FoldersExitCodes.ProviderFailure,
        CanonicalErrorCategory.Provider_readiness_failed => FoldersExitCodes.ProviderFailure,
        CanonicalErrorCategory.Provider_permission_insufficient => FoldersExitCodes.ProviderFailure,
        CanonicalErrorCategory.Repository_binding_unavailable => FoldersExitCodes.ProviderFailure,
        CanonicalErrorCategory.Repository_conflict => FoldersExitCodes.ProviderFailure,
        CanonicalErrorCategory.Duplicate_binding => FoldersExitCodes.ProviderFailure,
        CanonicalErrorCategory.Unsupported_provider_capability => FoldersExitCodes.ProviderFailure,
        CanonicalErrorCategory.Failed_operation => FoldersExitCodes.ProviderFailure,
        CanonicalErrorCategory.Commit_failed => FoldersExitCodes.ProviderFailure,
        CanonicalErrorCategory.File_operation_failed => FoldersExitCodes.ProviderFailure,

        // Unknown provider outcome → 71 (surfaced truthfully, never retried into duplication).
        CanonicalErrorCategory.Unknown_provider_outcome => FoldersExitCodes.UnknownProviderOutcome,

        // Reconciliation / read-model freshness pending → 72.
        CanonicalErrorCategory.Reconciliation_required => FoldersExitCodes.ReconciliationRequired,
        CanonicalErrorCategory.Read_model_unavailable => FoldersExitCodes.ReconciliationRequired,
        CanonicalErrorCategory.Projection_stale => FoldersExitCodes.ReconciliationRequired,
        CanonicalErrorCategory.Projection_unavailable => FoldersExitCodes.ReconciliationRequired,
        CanonicalErrorCategory.Workspace_not_ready => FoldersExitCodes.ReconciliationRequired,
        CanonicalErrorCategory.Workspace_preparation_failed => FoldersExitCodes.ReconciliationRequired,
        CanonicalErrorCategory.Dirty_workspace => FoldersExitCodes.ReconciliationRequired,

        // Not found / authorization revoked → 73.
        CanonicalErrorCategory.Not_found => FoldersExitCodes.NotFound,
        CanonicalErrorCategory.Authorization_revocation_detected => FoldersExitCodes.NotFound,

        // Invalid lifecycle transition → 74.
        CanonicalErrorCategory.State_transition_invalid => FoldersExitCodes.StateTransitionInvalid,

        // Redacted result → 75.
        CanonicalErrorCategory.Redacted => FoldersExitCodes.Redacted,

        // query_timeout and internal_error are the only post-SDK categories mapping to 1.
        CanonicalErrorCategory.Query_timeout => FoldersExitCodes.InternalError,
        CanonicalErrorCategory.Internal_error => FoldersExitCodes.InternalError,

        // Any category not present in the oracle outcome_mapping (e.g. range_unsatisfiable) falls through
        // to 1; this implies oracle/spine drift and is surfaced with the correlation ID by the caller.
        _ => FoldersExitCodes.InternalError,
    };
}
