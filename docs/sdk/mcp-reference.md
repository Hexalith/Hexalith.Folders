# Hexalith.Folders MCP Reference

Status: Story 7.13 consumer reference.

`Hexalith.Folders.Mcp` is a standalone **Model Context Protocol stdio server** (ModelContextProtocol 1.3.0)
that exposes the canonical Folders surface to MCP clients. It is an executable sidecar (`IsPackable=false`) — a
process you launch, **not** a NuGet library — and it wraps the typed SDK (`Hexalith.Folders.Client`); it adds
no behavior absent from the [Contract Spine](./api-reference.md).

The four-surface parity this reference relies on is **wire-exercised** end-to-end (REST/SDK/CLI/MCP), gated on
Stories 8.1–8.3 — including canonical cross-surface error parity (`idempotency_conflict` → MCP failure kind
`idempotency_conflict`). See [Surface conventions](./api-reference.md#surface-conventions) and the
[contract & parity CI gates](../contract/contract-parity-ci-gates.md).

All examples are **metadata-only**: identifiers are opaque, synthetic references. Never place secrets, bearer
tokens, raw file contents, base64 file bytes, diffs, provider payloads, real issuer/audience values, or local
absolute paths in tool arguments, results, logs, or examples.

See also: [API & SDK reference](./api-reference.md) · [CLI reference](./cli-reference.md) ·
[authentication guidance](./authentication.md) · lifecycle diagrams
([workspace](../diagrams/workspace-lifecycle.md), [file→commit](../diagrams/file-commit-flow.md),
[auth/ACL](../diagrams/auth-acl-decision-flow.md)).

## Transport and discovery

- **Transport: stdio.** `stdout` carries the JSON-RPC channel; **all logging goes to `stderr` only**. Never
  write diagnostics to `stdout` — it would corrupt the protocol stream.
- **Discovery: assembly attributes.** Tools and resources are discovered by scanning assembly attributes
  (`[McpServerTool]`/`[McpServerToolType]`, `[McpServerResource]`/`[McpServerResourceType]`) via
  `WithToolsFromAssembly()` / `WithResourcesFromAssembly()` in `Program.cs`. There is **no
  `server-manifest.json`** — the architecture source-tree sketch that implies one is illustrative; assembly
  discovery is the actual mechanism.

## Result envelope

Every tool result carries `correlationId`. Failure results add four more fields:

| Field | Present on | Meaning |
|---|---|---|
| `correlationId` | every result | Opaque correlation reference. |
| `kind` | failures | The failure kind (see the [catalog](#failure-kind-catalog)). |
| `code` | failures | Stable lowercase canonical error code. |
| `retryable` | failures | Whether the caller may retry. |
| `clientAction` | failures | Recommended client action (`retry`, `revise_request`, `check_credentials`, `wait_for_reconciliation`, `contact_operator`, `no_action`). |

- **Mutating tools** take a caller-supplied `idempotencyKey`; there is **no auto-key path** in the MCP server
  (unlike the CLI's `--allow-auto-key`). The caller always supplies the key.
- **Task-scoped tools** require a `taskId` argument.

## Tools (49)

Tools are defined across **8 tool-type files**. The tool name is the kebab-case form of the canonical
`operation_id`. Counts: Provider 4, Folder 11, Workspace 8, File 3, Context 7, Commit 5, Diagnostics 7,
Audit 4 = **49**.

### Provider tools (`ProviderTools.cs`)

| Tool | Operation | Mutating | Task-scoped |
|---|---|---|---|
| `configure-provider-binding` | `ConfigureProviderBinding` | yes | yes |
| `get-provider-binding` | `GetProviderBinding` | no | no |
| `validate-provider-readiness` | `ValidateProviderReadiness` | no | no |
| `get-provider-support-evidence` | `GetProviderSupportEvidence` | no | no |

### Folder tools (`FolderTools.cs`)

| Tool | Operation | Mutating | Task-scoped |
|---|---|---|---|
| `create-folder` | `CreateFolder` | yes | yes |
| `create-repository-backed-folder` | `CreateRepositoryBackedFolder` | yes | yes |
| `bind-repository` | `BindRepository` | yes | yes |
| `get-repository-binding` | `GetRepositoryBinding` | no | no |
| `get-folder-lifecycle-status` | `GetFolderLifecycleStatus` | no | no |
| `archive-folder` | `ArchiveFolder` | yes | yes |
| `list-folder-acl-entries` | `ListFolderAclEntries` | no | no |
| `update-folder-acl-entry` | `UpdateFolderAclEntry` | yes | yes |
| `get-effective-permissions` | `GetEffectivePermissions` | no | no |
| `configure-branch-ref-policy` | `ConfigureBranchRefPolicy` | yes | yes |
| `get-branch-ref-policy` | `GetBranchRefPolicy` | no | no |

### Workspace tools (`WorkspaceTools.cs`)

| Tool | Operation | Mutating | Task-scoped |
|---|---|---|---|
| `prepare-workspace` | `PrepareWorkspace` | yes | yes |
| `lock-workspace` | `LockWorkspace` | yes | yes |
| `get-workspace-lock` | `GetWorkspaceLock` | no | no |
| `release-workspace-lock` | `ReleaseWorkspaceLock` | yes | yes |
| `get-workspace-retry-eligibility` | `GetWorkspaceRetryEligibility` | no | yes |
| `get-workspace-transition-evidence` | `GetWorkspaceTransitionEvidence` | no | yes |
| `get-workspace-status` | `GetWorkspaceStatus` | no | no |
| `get-workspace-cleanup-status` | `GetWorkspaceCleanupStatus` | no | yes |

### File tools (`FileTools.cs`)

| Tool | Operation | Mutating | Task-scoped |
|---|---|---|---|
| `add-file` | `AddFile` | yes | yes |
| `change-file` | `ChangeFile` | yes | yes |
| `remove-file` | `RemoveFile` | yes | yes |

### Context tools (`ContextTools.cs`)

| Tool | Operation | Mutating | Task-scoped |
|---|---|---|---|
| `list-folder-files` | `ListFolderFiles` | no | yes |
| `get-folder-file-metadata` | `GetFolderFileMetadata` | no | yes |
| `search-folder-files` | `SearchFolderFiles` | no | yes |
| `search-folder-indexed-files` | `SearchFolderIndexedFiles` | no | yes |
| `glob-folder-files` | `GlobFolderFiles` | no | yes |
| `read-file-range` | `ReadFileRange` | no | yes |
| `get-folder-indexing-status` | `GetFolderIndexingStatus` | no | no |

### Commit tools (`CommitTools.cs`)

| Tool | Operation | Mutating | Task-scoped |
|---|---|---|---|
| `commit-workspace` | `CommitWorkspace` | yes | yes |
| `get-commit-evidence` | `GetCommitEvidence` | no | no |
| `get-provider-outcome` | `GetProviderOutcome` | no | no |
| `get-reconciliation-status` | `GetReconciliationStatus` | no | no |
| `get-task-status` | `GetTaskStatus` | no | no |

### Diagnostics tools (`DiagnosticsTools.cs`)

| Tool | Operation | Mutating | Task-scoped |
|---|---|---|---|
| `get-readiness-diagnostics` | `GetReadinessDiagnostics` | no | no |
| `get-provider-status-diagnostics` | `GetProviderStatusDiagnostics` | no | no |
| `get-sync-status-diagnostics` | `GetSyncStatusDiagnostics` | no | no |
| `get-lock-diagnostics` | `GetLockDiagnostics` | no | no |
| `get-dirty-state-diagnostics` | `GetDirtyStateDiagnostics` | no | no |
| `get-failed-operation-diagnostics` | `GetFailedOperationDiagnostics` | no | no |
| `get-projection-freshness` | `GetProjectionFreshness` | no | no |

### Audit tools (`AuditTools.cs`)

| Tool | Operation | Mutating | Task-scoped |
|---|---|---|---|
| `list-audit-trail` | `ListAuditTrail` | no | no |
| `get-audit-record` | `GetAuditRecord` | no | no |
| `list-operation-timeline` | `ListOperationTimeline` | no | no |
| `get-operation-timeline-entry` | `GetOperationTimelineEntry` | no | no |

## Resources (2)

Two read-only resources are exposed via `[McpServerResource]` URI templates:

| Resource | URI template |
|---|---|
| `folder-tree` | `folders://folder-tree/{folderId}/{workspaceId}/{taskId}` |
| `audit-trail` | `folders://audit-trail/{folderId}` |

## Failure-kind catalog

The authoritative MCP failure-kind catalog is the **43** `outcome_mapping.mcp_failure_kind` values from the
parity oracle (`tests/fixtures/parity-contract.yaml`) — each equal verbatim to its `CanonicalErrorCategory`
name in snake_case — **plus the 2 pre-SDK kinds** `usage_error` and `credential_missing` (emitted before any
HTTP call, for client-side usage and missing-credential failures). That is **45** kinds total.

Do not use the abridged 13-row architecture summary; it misspells `unknown_provider_outcome`. The success
mapping (`none`) is not a failure kind. `range_unsatisfiable` is intentionally **absent** from the oracle and
maps to `internal_error` (the documented spine/oracle drift fallback), mirroring the CLI exit-code projection.

<!-- failure-kind-catalog -->

```text
usage_error
credential_missing
audit_access_denied
authentication_failure
authorization_revocation_detected
branch_ref_policy_invalid
commit_failed
credential_reference_invalid
cross_tenant_access_denied
dirty_workspace
duplicate_binding
failed_operation
file_operation_failed
folder_acl_denied
idempotency_conflict
input_limit_exceeded
internal_error
lock_conflict
lock_expired
lock_not_owned
not_found
path_validation_failed
projection_stale
projection_unavailable
provider_failure_known
provider_permission_insufficient
provider_rate_limited
provider_readiness_failed
provider_unavailable
query_timeout
read_model_unavailable
reconciliation_required
redacted
repository_binding_unavailable
repository_conflict
response_limit_exceeded
stale_workspace
state_transition_invalid
tenant_access_denied
unknown_provider_outcome
unsupported_provider_capability
validation_error
workspace_locked
workspace_not_ready
workspace_preparation_failed
```
