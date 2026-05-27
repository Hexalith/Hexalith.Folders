namespace Hexalith.Folders.Cli;

/// <summary>
/// Canonical sysexits-style exit codes for the Folders CLI adapter.
/// </summary>
/// <remarks>
/// These are the Folders projection of the parity oracle's <c>cli_exit_code</c> column
/// (<c>tests/fixtures/parity-contract.yaml</c>). They are deliberately NOT the
/// <c>Hexalith.EventStore.Admin.Cli.ExitCodes</c> <c>Success=0/Degraded=1/Error=2</c> scheme — that is a
/// different adapter's UX-DR52 convention and is wrong for Folders. The full canonical table is
/// <c>{0, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 1}</c>. See <see cref="ErrorProjection"/> for the
/// single category-to-code projection map.
/// </remarks>
internal static class FoldersExitCodes
{
    /// <summary>Operation succeeded.</summary>
    public const int Success = 0;

    /// <summary>Pre-SDK usage / configuration error (<c>client_configuration_error</c>); no HTTP call was made.</summary>
    public const int UsageError = 64;

    /// <summary>Credential family failure (<c>credential_missing</c> / <c>authentication_failure</c> / <c>credential_reference_invalid</c>).</summary>
    public const int CredentialMissing = 65;

    /// <summary>Tenant or folder access denied (<c>tenant_access_denied</c>, <c>cross_tenant_access_denied</c>, <c>folder_acl_denied</c>, <c>audit_access_denied</c>).</summary>
    public const int AccessDenied = 66;

    /// <summary>Workspace lock contention (<c>workspace_locked</c>, <c>lock_conflict</c>, <c>lock_expired</c>, <c>lock_not_owned</c>, <c>stale_workspace</c>).</summary>
    public const int LockConflict = 67;

    /// <summary>Idempotency conflict (<c>idempotency_conflict</c>).</summary>
    public const int IdempotencyConflict = 68;

    /// <summary>Request validation / input-shape failure (<c>validation_error</c>, <c>input_limit_exceeded</c>, <c>path_validation_failed</c>, <c>branch_ref_policy_invalid</c>, <c>response_limit_exceeded</c>).</summary>
    public const int ValidationError = 69;

    /// <summary>Provider / repository operation failure (<c>provider_*</c>, <c>repository_*</c>, <c>duplicate_binding</c>, <c>unsupported_provider_capability</c>, <c>failed_operation</c>, <c>commit_failed</c>, <c>file_operation_failed</c>).</summary>
    public const int ProviderFailure = 70;

    /// <summary>Unknown provider outcome that must be surfaced, never hidden (<c>unknown_provider_outcome</c>).</summary>
    public const int UnknownProviderOutcome = 71;

    /// <summary>Reconciliation / read-model freshness pending (<c>reconciliation_required</c>, <c>read_model_unavailable</c>, <c>projection_*</c>, <c>workspace_not_ready</c>, <c>workspace_preparation_failed</c>, <c>dirty_workspace</c>).</summary>
    public const int ReconciliationRequired = 72;

    /// <summary>Resource not found / authorization revoked (<c>not_found</c>, <c>authorization_revocation_detected</c>).</summary>
    public const int NotFound = 73;

    /// <summary>Invalid lifecycle state transition (<c>state_transition_invalid</c>).</summary>
    public const int StateTransitionInvalid = 74;

    /// <summary>Result was redacted and is visibly distinct from missing/unknown (<c>redacted</c>).</summary>
    public const int Redacted = 75;

    /// <summary>Unexpected/unmapped server outcome or transport failure (<c>internal_error</c>, <c>query_timeout</c>, unmapped categories).</summary>
    public const int InternalError = 1;
}
