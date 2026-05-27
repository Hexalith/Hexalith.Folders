using Hexalith.Folders.Aggregates.Folder;

using Microsoft.AspNetCore.Http;

namespace Hexalith.Folders.Server;

internal static class FolderCanonicalErrorMapper
{
    public static string CategoryFor(FolderResultCode code)
        => code switch
        {
            FolderResultCode.Accepted or FolderResultCode.Created or FolderResultCode.IdempotentReplay or FolderResultCode.AlreadyApplied => "success",
            FolderResultCode.AlreadyArchived => "already_archived",
            FolderResultCode.ArchivePolicyDenied => "policy_denied",
            FolderResultCode.MissingEntry or FolderResultCode.FolderNotFound => "not_found",
            FolderResultCode.DuplicateEntry or FolderResultCode.DuplicateFolder => "duplicate_binding",
            FolderResultCode.ConflictingEntry or FolderResultCode.AppendConflict or FolderResultCode.ReplayConflict => "repository_conflict",
            FolderResultCode.IdempotencyConflict => "idempotency_conflict",
            FolderResultCode.IdempotencyUnavailable => "read_model_unavailable",
            FolderResultCode.InvalidFolderId
                or FolderResultCode.InvalidFolderMetadata
                or FolderResultCode.InvalidTenant
                or FolderResultCode.ReservedTenant
                or FolderResultCode.InvalidPrincipal
                or FolderResultCode.ValidationFailed
                or FolderResultCode.MalformedJsonPayload
                or FolderResultCode.MalformedEvidence
                or FolderResultCode.TenantMismatch => "validation_error",
            FolderResultCode.MissingAuthoritativeTenant => "authentication_failure",
            FolderResultCode.TenantAccessDenied or FolderResultCode.UnknownTenant or FolderResultCode.DisabledTenant => "tenant_access_denied",
            FolderResultCode.FolderAclDenied
                or FolderResultCode.AclEvidenceMismatch
                or FolderResultCode.AclEvidenceForeignFolder
                or FolderResultCode.AclEvidenceUnsupportedAction => "folder_acl_denied",
            FolderResultCode.AclEvidenceUnavailable or FolderResultCode.PolicyEvidenceUnavailable => "read_model_unavailable",
            FolderResultCode.PolicyEvidenceStale or FolderResultCode.StaleProjection => "projection_stale",
            FolderResultCode.PolicyEvidenceMalformed => "authorization_denied",
            FolderResultCode.PolicyEvidenceScopeMismatch => "policy_denied",
            FolderResultCode.ProviderReadinessFailed => "provider_readiness_failed",
            FolderResultCode.UnsupportedProviderCapability => "unsupported_provider_capability",
            FolderResultCode.ProviderUnavailable => "provider_unavailable",
            FolderResultCode.ProviderRateLimited => "provider_rate_limited",
            FolderResultCode.ProviderPermissionInsufficient => "provider_permission_insufficient",
            FolderResultCode.RepositoryConflict => "repository_conflict",
            FolderResultCode.LockConflict => "lock_conflict",
            FolderResultCode.LockNotOwned => "lock_not_owned",
            FolderResultCode.LockExpired => "lock_expired",
            FolderResultCode.PathPolicyDenied => "path_policy_denied",
            FolderResultCode.PathValidationFailed => "path_validation_failed",
            FolderResultCode.FileOperationFailed => "file_operation_failed",
            FolderResultCode.UnknownProviderOutcome => "unknown_provider_outcome",
            FolderResultCode.ReconciliationRequired => "reconciliation_required",
            FolderResultCode.UnsupportedAction or FolderResultCode.UnsupportedCommandType => "unsupported_command_type",
            FolderResultCode.UnavailableProjection => "projection_unavailable",
            FolderResultCode.StateTransitionInvalid => "state_transition_invalid",
            _ => "internal_error",
        };

    public static int StatusFor(string category)
        => category switch
        {
            "authentication_failure" => StatusCodes.Status401Unauthorized,
            "not_found" or "not_found_to_caller" => StatusCodes.Status404NotFound,
            "idempotency_conflict"
                or "repository_conflict"
                or "duplicate_binding"
                or "lock_conflict"
                or "workspace_locked"
                or "lock_not_owned"
                or "reconciliation_required" => StatusCodes.Status409Conflict,
            "lock_expired" => StatusCodes.Status410Gone,
            "provider_rate_limited" => StatusCodes.Status429TooManyRequests,
            "validation_error" => StatusCodes.Status400BadRequest,
            "provider_readiness_failed"
                or "unsupported_provider_capability"
                or "workspace_preparation_failed"
                or "workspace_transition_invalid"
                or "state_transition_invalid"
                or "path_policy_denied"
                or "path_validation_failed"
                or "file_operation_failed" => StatusCodes.Status422UnprocessableEntity,
            "provider_unavailable"
                or "unknown_provider_outcome"
                or "read_model_unavailable"
                or "projection_stale"
                or "projection_unavailable"
                or "internal_error" => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status403Forbidden,
        };

    public static bool RetryableFor(string category)
        => category switch
        {
            "provider_rate_limited"
                or "provider_unavailable"
                or "read_model_unavailable"
                or "projection_stale"
                or "projection_unavailable"
                or "lock_expired"
                or "query_timeout" => true,
            "unknown_provider_outcome" or "reconciliation_required" => false,
            _ => false,
        };

    public static string ClientActionFor(string category, bool retryable)
        => category switch
        {
            "unknown_provider_outcome" or "reconciliation_required" => "wait_for_reconciliation",
            "provider_readiness_failed" or "unsupported_provider_capability" => "contact_operator",
            "workspace_preparation_failed" or "workspace_transition_invalid" or "state_transition_invalid" => "revise_request",
            "lock_conflict" or "workspace_locked" => "retry_after_release",
            "lock_expired" or "query_timeout" => "retry",
            "lock_not_owned" or "path_policy_denied" or "path_validation_failed" => "revise_request",
            "input_limit_exceeded" or "response_limit_exceeded" or "range_unsatisfiable" => "revise_request",
            _ => retryable ? "retry" : "no_action",
        };
}
