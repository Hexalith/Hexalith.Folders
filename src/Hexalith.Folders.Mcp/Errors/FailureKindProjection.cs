using Hexalith.Folders.Client.Generated;

namespace Hexalith.Folders.Mcp.Errors;

/// <summary>
/// The single canonical <see cref="CanonicalErrorCategory"/> → MCP failure-kind projection used by every
/// tool and resource. Encoded verbatim from the deduplicated <c>outcome_mapping.mcp_failure_kind</c> column
/// of the parity oracle (<c>tests/fixtures/parity-contract.yaml</c>), where <b>kind == the canonical
/// category name verbatim</b> (one-to-one, 43 post-SDK values). Story 5.4 proves this map against that
/// oracle; this story encodes and unit-tests it directly.
/// </summary>
/// <remarks>
/// <para>This is the authoritative set — NOT the abbreviated 13-row summary in architecture
/// §"Adapter Parity Contract" (which also misspells <c>unknown_provider_outcome</c> as
/// <c>provider_outcome_unknown</c>). Distinct categories are never collapsed for adapter convenience
/// (project-context Critical Don't-Miss rule).</para>
/// <para><see cref="CanonicalErrorCategory.Range_unsatisfiable"/> (enum 43) is <b>absent</b> from the
/// oracle <c>mcp_failure_kind</c> set, so it falls through to <see cref="InternalError"/> — exactly as
/// Story 5.2 handled it for CLI exit codes. That fall-through is a deliberate spine/oracle drift signal,
/// not a silent collapse. The two pre-SDK kinds (<c>usage_error</c>, <c>credential_missing</c>) are layered
/// on top by the tool pipeline and never produced by this projection.</para>
/// </remarks>
internal static class FailureKindProjection
{
    /// <summary>The catch-all kind for unmapped/unexpected categories and bare API exceptions.</summary>
    public const string InternalError = "internal_error";

    /// <summary>The pre-SDK kind for a missing required field or invalid configuration (no HTTP call).</summary>
    public const string UsageError = "usage_error";

    /// <summary>The pre-SDK kind for an unresolved bearer token (no HTTP call).</summary>
    public const string CredentialMissing = "credential_missing";

    /// <summary>
    /// Projects a server-returned canonical error category to its authoritative MCP failure kind.
    /// </summary>
    /// <param name="category">The category carried by the server's RFC 9457 problem response.</param>
    /// <returns>The canonical failure-kind string (kind == category name) for the category.</returns>
    public static string Project(CanonicalErrorCategory category) => category switch
    {
        CanonicalErrorCategory.Success => "success",
        CanonicalErrorCategory.Authentication_failure => "authentication_failure",
        CanonicalErrorCategory.Client_configuration_error => "client_configuration_error",
        CanonicalErrorCategory.Credential_missing => "credential_missing",
        CanonicalErrorCategory.Credential_reference_invalid => "credential_reference_invalid",
        CanonicalErrorCategory.Tenant_access_denied => "tenant_access_denied",
        CanonicalErrorCategory.Cross_tenant_access_denied => "cross_tenant_access_denied",
        CanonicalErrorCategory.Folder_acl_denied => "folder_acl_denied",
        CanonicalErrorCategory.Audit_access_denied => "audit_access_denied",
        CanonicalErrorCategory.Validation_error => "validation_error",
        CanonicalErrorCategory.Idempotency_conflict => "idempotency_conflict",
        CanonicalErrorCategory.Provider_readiness_failed => "provider_readiness_failed",
        CanonicalErrorCategory.Provider_permission_insufficient => "provider_permission_insufficient",
        CanonicalErrorCategory.Provider_unavailable => "provider_unavailable",
        CanonicalErrorCategory.Provider_rate_limited => "provider_rate_limited",
        CanonicalErrorCategory.Repository_binding_unavailable => "repository_binding_unavailable",
        CanonicalErrorCategory.Branch_ref_policy_invalid => "branch_ref_policy_invalid",
        CanonicalErrorCategory.Workspace_not_ready => "workspace_not_ready",
        CanonicalErrorCategory.Workspace_preparation_failed => "workspace_preparation_failed",
        CanonicalErrorCategory.Workspace_locked => "workspace_locked",
        CanonicalErrorCategory.Lock_conflict => "lock_conflict",
        CanonicalErrorCategory.Lock_expired => "lock_expired",
        CanonicalErrorCategory.Lock_not_owned => "lock_not_owned",
        CanonicalErrorCategory.Stale_workspace => "stale_workspace",
        CanonicalErrorCategory.Authorization_revocation_detected => "authorization_revocation_detected",
        CanonicalErrorCategory.Repository_conflict => "repository_conflict",
        CanonicalErrorCategory.Duplicate_binding => "duplicate_binding",
        CanonicalErrorCategory.Unsupported_provider_capability => "unsupported_provider_capability",
        CanonicalErrorCategory.Path_validation_failed => "path_validation_failed",
        CanonicalErrorCategory.File_operation_failed => "file_operation_failed",
        CanonicalErrorCategory.Dirty_workspace => "dirty_workspace",
        CanonicalErrorCategory.Commit_failed => "commit_failed",
        CanonicalErrorCategory.Provider_failure_known => "provider_failure_known",
        CanonicalErrorCategory.Unknown_provider_outcome => "unknown_provider_outcome",
        CanonicalErrorCategory.Reconciliation_required => "reconciliation_required",
        CanonicalErrorCategory.Not_found => "not_found",
        CanonicalErrorCategory.State_transition_invalid => "state_transition_invalid",
        CanonicalErrorCategory.Input_limit_exceeded => "input_limit_exceeded",
        CanonicalErrorCategory.Response_limit_exceeded => "response_limit_exceeded",
        CanonicalErrorCategory.Query_timeout => "query_timeout",
        CanonicalErrorCategory.Read_model_unavailable => "read_model_unavailable",
        CanonicalErrorCategory.Projection_stale => "projection_stale",
        CanonicalErrorCategory.Projection_unavailable => "projection_unavailable",
        CanonicalErrorCategory.Failed_operation => "failed_operation",
        CanonicalErrorCategory.Redacted => "redacted",
        CanonicalErrorCategory.Internal_error => "internal_error",

        // range_unsatisfiable (enum 43) is absent from the oracle mcp_failure_kind set → internal_error
        // as a documented drift signal; mirrors Story 5.2's CLI exit-code handling. Any future unmapped
        // category likewise falls through here rather than being collapsed into a convenient neighbour.
        _ => InternalError,
    };

    /// <summary>
    /// Converts a typed <see cref="ProblemDetailsClientAction"/> to its canonical wire token. Done explicitly
    /// (rather than via reflection) so the client-action vocabulary stays an auditable public contract.
    /// </summary>
    /// <param name="action">The client action carried by the problem response.</param>
    /// <returns>The snake_case client-action token.</returns>
    public static string ClientAction(ProblemDetailsClientAction action) => action switch
    {
        ProblemDetailsClientAction.Retry => "retry",
        ProblemDetailsClientAction.Revise_request => "revise_request",
        ProblemDetailsClientAction.Check_credentials => "check_credentials",
        ProblemDetailsClientAction.Wait_for_reconciliation => "wait_for_reconciliation",
        ProblemDetailsClientAction.Contact_operator => "contact_operator",
        ProblemDetailsClientAction.No_action => "no_action",
        _ => "no_action",
    };
}
