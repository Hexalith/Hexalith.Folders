# Canonical Error Catalog

This document is the cross-surface map of the Hexalith.Folders **canonical error vocabulary**: the generated
`CanonicalErrorCategory`, the REST Problem Details (RFC 9457) shape, HTTP status mapping, retryability,
`retryAfter` behavior, client-action guidance, CLI exit codes, SDK error/result behavior, and MCP failure
kinds. It **reflects** the contracts — the generated client enums in
`src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs`, the parity oracle
`tests/fixtures/parity-contract.yaml`, the server mapper `FolderCanonicalErrorMapper`, the CLI projection
(`FoldersExitCodes` / `ErrorProjection`), and the MCP projection (`FailureKindProjection`) — it never
redefines them. Every example uses opaque synthetic identifiers and placeholder hosts only.

This catalog **summarizes** cross-surface behavior; it does **not** re-author the Story 7.13 consumer
manuals. For full surface detail see [`docs/sdk/api-reference.md`](../sdk/api-reference.md),
[`docs/sdk/cli-reference.md`](../sdk/cli-reference.md), [`docs/sdk/mcp-reference.md`](../sdk/mcp-reference.md),
and [`docs/sdk/authentication.md`](../sdk/authentication.md). For provider failure evidence see
[`provider-integration-and-testing.md`](provider-integration-and-testing.md); for audit/redaction output see
[`audit-and-redaction.md`](audit-and-redaction.md) and
[`incident-alerting-and-recovery.md`](incident-alerting-and-recovery.md).

## Generated canonical category vocabulary (47)

The wire vocabulary is the generated `CanonicalErrorCategory` enum — **exactly 47 members**, emitted on the
RFC 9457 `category` extension. This is the authoritative SDK-facing set; the catalog never invents categories
the generated enum does not declare.

<!-- generated-canonical-categories -->

| Category |
|---|
| `success` |
| `authentication_failure` |
| `client_configuration_error` |
| `credential_missing` |
| `credential_reference_invalid` |
| `tenant_access_denied` |
| `cross_tenant_access_denied` |
| `folder_acl_denied` |
| `audit_access_denied` |
| `validation_error` |
| `idempotency_conflict` |
| `provider_readiness_failed` |
| `provider_permission_insufficient` |
| `provider_unavailable` |
| `provider_rate_limited` |
| `repository_binding_unavailable` |
| `branch_ref_policy_invalid` |
| `workspace_not_ready` |
| `workspace_preparation_failed` |
| `workspace_locked` |
| `lock_conflict` |
| `lock_expired` |
| `lock_not_owned` |
| `stale_workspace` |
| `authorization_revocation_detected` |
| `repository_conflict` |
| `duplicate_binding` |
| `unsupported_provider_capability` |
| `path_validation_failed` |
| `file_operation_failed` |
| `dirty_workspace` |
| `commit_failed` |
| `provider_failure_known` |
| `unknown_provider_outcome` |
| `reconciliation_required` |
| `not_found` |
| `state_transition_invalid` |
| `input_limit_exceeded` |
| `response_limit_exceeded` |
| `query_timeout` |
| `read_model_unavailable` |
| `projection_stale` |
| `projection_unavailable` |
| `range_unsatisfiable` |
| `failed_operation` |
| `redacted` |
| `internal_error` |

## Parity oracle outcome mappings (43)

The parity oracle `tests/fixtures/parity-contract.yaml` is the source of truth for cross-surface outcome
mappings. Its `outcome_mapping` rows currently carry **43 distinct canonical categories**. The **four**
generated categories intentionally outside the oracle path are `success` (the non-error outcome),
`client_configuration_error` and `credential_missing` (pre-SDK behavior with no HTTP call), and
`range_unsatisfiable` (the documented fallback below).

<!-- oracle-carried-categories -->

| Category |
|---|
| `audit_access_denied` |
| `authentication_failure` |
| `authorization_revocation_detected` |
| `branch_ref_policy_invalid` |
| `commit_failed` |
| `credential_reference_invalid` |
| `cross_tenant_access_denied` |
| `dirty_workspace` |
| `duplicate_binding` |
| `failed_operation` |
| `file_operation_failed` |
| `folder_acl_denied` |
| `idempotency_conflict` |
| `input_limit_exceeded` |
| `internal_error` |
| `lock_conflict` |
| `lock_expired` |
| `lock_not_owned` |
| `not_found` |
| `path_validation_failed` |
| `projection_stale` |
| `projection_unavailable` |
| `provider_failure_known` |
| `provider_permission_insufficient` |
| `provider_rate_limited` |
| `provider_readiness_failed` |
| `provider_unavailable` |
| `query_timeout` |
| `read_model_unavailable` |
| `reconciliation_required` |
| `redacted` |
| `repository_binding_unavailable` |
| `repository_conflict` |
| `response_limit_exceeded` |
| `stale_workspace` |
| `state_transition_invalid` |
| `tenant_access_denied` |
| `unknown_provider_outcome` |
| `unsupported_provider_capability` |
| `validation_error` |
| `workspace_locked` |
| `workspace_not_ready` |
| `workspace_preparation_failed` |

## REST Problem Details shape and HTTP status mapping

Errors are returned as RFC 9457 Problem Details. Beyond the standard `type`, `title`, `status`, `detail`, and
`instance` members, the server emits the extensions `category` (a canonical category above), `clientAction`,
`retryable`, `retryAfterSeconds` (when a bounded hint is available), and `correlationId`. The server mapper
`FolderCanonicalErrorMapper.StatusFor` maps categories to HTTP status: `authentication_failure` to `401`;
`not_found` to `404`; idempotency/repository/lock/reconciliation conflicts to `409`; `lock_expired` to `410`;
`provider_rate_limited` to `429`; `validation_error` to `400`; readiness/capability/transition/path/file
classes to `422`; unavailable/projection/internal classes to `503`; and all remaining denials to `403`.

## Retryability and client-action guidance

`FolderCanonicalErrorMapper.RetryableFor` marks a small set retryable: `provider_rate_limited`,
`provider_unavailable`, `read_model_unavailable`, `projection_stale`, `projection_unavailable`, `lock_expired`,
and `query_timeout`. The two ambiguous-outcome categories `unknown_provider_outcome` and
`reconciliation_required` are deliberately **not** retryable — retrying them could duplicate a repository,
file change, or commit. The typed client-action vocabulary is the generated `ProblemDetailsClientAction`
enum — **exactly 6 wire tokens**.

<!-- client-action-tokens -->

| Client action | Meaning |
|---|---|
| `retry` | The same request may be retried, optionally after `retryAfter` |
| `revise_request` | The caller must change the request before retrying |
| `check_credentials` | The caller must resolve a credential/authentication problem |
| `wait_for_reconciliation` | The outcome is pending; wait, do not blindly retry |
| `contact_operator` | An operator must act before the request can succeed |
| `no_action` | No client action is useful for this outcome |

## CLI exit-code behavior (14)

The CLI projects each canonical category to a sysexits-style exit code through `ErrorProjection`, drawing on
the canonical table in `FoldersExitCodes` — **14 distinct values**. These are the Folders projection of the
oracle `cli_exit_code` column and are deliberately not the EventStore admin CLI scheme.

<!-- cli-exit-codes -->

| Exit code | Constant | Class |
|---|---|---|
| `0` | Success | success |
| `64` | UsageError | pre-SDK usage / `client_configuration_error` (no HTTP call) |
| `65` | CredentialMissing | credential family (`credential_missing` / `authentication_failure` / `credential_reference_invalid`) |
| `66` | AccessDenied | tenant / folder / audit access denial |
| `67` | LockConflict | lock contention and stale workspace |
| `68` | IdempotencyConflict | `idempotency_conflict` |
| `69` | ValidationError | validation / input-shape failures |
| `70` | ProviderFailure | provider / repository operation failures |
| `71` | UnknownProviderOutcome | `unknown_provider_outcome`, surfaced never hidden |
| `72` | ReconciliationRequired | reconciliation / read-model freshness pending |
| `73` | NotFound | `not_found` / `authorization_revocation_detected` |
| `74` | StateTransitionInvalid | `state_transition_invalid` |
| `75` | Redacted | `redacted`, visibly distinct from missing/unknown |
| `1` | InternalError | `internal_error` / `query_timeout` / unmapped fallback |

## SDK error/result behavior

The typed SDK surfaces the canonical category, `clientAction`, `retryable`, and `retryAfter` from the Problem
Details response, plus two **pre-SDK** classes that never reach the wire because no HTTP call is made:
`client_configuration_error` (exit `64`) and `credential_missing` (exit `65`). See
[`docs/sdk/cli-reference.md`](../sdk/cli-reference.md) and [`docs/sdk/api-reference.md`](../sdk/api-reference.md)
for the full surface; this catalog does not duplicate them.

## MCP failure-kind behavior

The MCP projection `FailureKindProjection` projects each post-SDK oracle category to a failure kind where
**the kind equals the category name verbatim** (the 43 oracle values). Two pre-SDK kinds are layered by the
tool pipeline and never produced by the projection, and the one generated category absent from the oracle
falls through to `internal_error` as a documented spine/oracle drift signal — never a silent collapse.

<!-- mcp-failure-kind-projection -->

| Rule | Behavior |
|---|---|
| `verbatim` | Oracle category name equals the MCP failure kind (43 post-SDK values) |
| `usage_error` | Pre-SDK only; layered by the tool pipeline; never produced by the projection |
| `credential_missing` | Pre-SDK only; layered by the tool pipeline; never produced by the projection |
| `range_unsatisfiable` | Absent from the oracle set; falls through to `internal_error` |

## Retry-after behavior is advisory-only

`WorkspaceStatusRetryAfter` and `WorkspaceStatusRetryEligibility` are **advisory-only**
(`AdvisoryOnly = true`), bounded, metadata-only signals. They **never** trigger mutation, repair,
auto-unlock, or implicit retry. Provider rate-limit retry hints are preserved where the provider supplies
them, but live alerting and runbook procedures stay outside this story (Story 7.17).

<!-- retry-after-advisory-fields -->

| Field | Record | Behavior |
|---|---|---|
| `RetryAfterSeconds` | `WorkspaceStatusRetryAfter` | Bounded advisory hint; never an instruction to mutate |
| `AdvisoryOnly` | both retry records | Defaults to `true`; never triggers mutation, repair, auto-unlock, or implicit retry |
| `Eligible` | `WorkspaceStatusRetryEligibility` | Whether a retry is advisable, advisory-only |
| `ReasonCode` | `WorkspaceStatusRetryEligibility` | Bounded reason metadata for the eligibility signal |

## Audit and logging expectations

Canonical categories appear in audit records as the `sanitizedErrorCategory` field and in operator surfaces as
metadata only. No error response, audit record, or log line may carry secrets, tokens, raw provider payloads,
diffs, stack traces, or tenant data. See [`audit-and-redaction.md`](audit-and-redaction.md) for the
metadata-only audit contract and the visibly-distinct redaction rule.

## Metadata-only policy

This document, the generated enums, the parity oracle, and the gate report are output channels subject to the
metadata-only invariant: no secrets, bearer tokens, credential material, raw file contents, base64 file bytes,
diffs, provider payloads, real issuer/audience values, production URLs, environment dumps, stack traces,
tenant data, or host-absolute paths. Examples use opaque synthetic identifiers and placeholder hosts only.

## Local validation

Run the focused gate from the repository root:

```text
pwsh ./tests/tools/run-provider-error-docs-gates.ps1
```

The gate runs the `ProviderErrorDocsConformanceTests` and writes a metadata-only report to
`_bmad-output/gates/provider-error-docs/latest.json`. Pass `-SkipRestoreBuild` (alias `-NoRestore`) when the
shared restore/build lane already ran. If the sandbox denies VSTest socket creation, the gate falls back to
the xUnit v3 in-process runner and still enforces the non-vacuous test-count guard.

If submodule working trees are missing, initialize only the root-level modules:

```text
git submodule update --init Hexalith.AI.Tools Hexalith.Commons Hexalith.EventStore Hexalith.FrontComposer Hexalith.Memories Hexalith.Tenants
```

Do not initialize nested submodules.

## Reviewer handoff and rerun rules

A reviewer should run the local validation command above, confirm the report reports `status: passed` with
`diagnostic_policy: metadata-only`, and confirm the generated canonical categories, oracle-carried categories,
client-action tokens, CLI exit codes, and MCP failure-kind rules stay synchronized with their sources. Rerun
the gate after any change to the generated client enums, the parity oracle, `FolderCanonicalErrorMapper`,
`FoldersExitCodes` / `ErrorProjection`, or `FailureKindProjection`. The static gate runs in the
`contract-spine` CI lane and through the baseline CI Contracts.Tests filter; it is not promoted to a new
top-level `ci.yml` lane, to `release-packages.yml`, or to scheduled workflows.
