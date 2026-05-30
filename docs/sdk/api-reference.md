# Hexalith.Folders API & SDK Reference

Status: Story 7.13 consumer reference.

This is the consumer-facing reference for the Hexalith.Folders REST surface and the typed SDK that
mirrors it. It is **rendered from the single Contract Spine**
`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` (OpenAPI 3.1.0, title
"Hexalith.Folders API", version `v1`). No server-side Swagger/Redoc/`MapOpenApi` middleware exists and none
is added; the spine YAML is the only source. The operation and tag inventory below is asserted **equal to the
parsed spine** by `ConsumerDocsConformanceTests`, so this document cannot silently drift from the contract.

All examples are **metadata-only**: identifiers are opaque, synthetic references and URLs use `.invalid`
placeholders. Never place secrets, bearer tokens, raw file contents, base64 file bytes, diffs, provider
payloads, real issuer/audience values, production URLs, or local absolute paths in requests, logs, or
examples.

## Related references

- [SDK quickstart](./quickstart.md) — DI registration, correlation/task-ID sourcing, the upload helper, and
  the runnable lifecycle sample.
- [CLI reference](./cli-reference.md) — the `folders` .NET tool over the same surface.
- [MCP reference](./mcp-reference.md) — the standalone MCP stdio server tools/resources.
- [Authentication guidance](./authentication.md) — bearer-token handlers, OIDC validation, and claim
  provenance.
- Lifecycle diagrams: [workspace lifecycle](../diagrams/workspace-lifecycle.md) ·
  [file → commit flow](../diagrams/file-commit-flow.md) ·
  [auth/ACL decision flow](../diagrams/auth-acl-decision-flow.md).
- Contract authoring notes (normative source — cross-linked, not duplicated here):
  [contract spine foundation](../contract/contract-spine-foundation.md),
  [tenant/folder/provider/repository groups](../contract/tenant-folder-provider-repository-contract-groups.md),
  [workspace & lock groups](../contract/workspace-lock-contract-groups.md),
  [file & context groups](../contract/file-context-contract-groups.md),
  [commit & status groups](../contract/commit-status-contract-groups.md),
  [audit & ops-console groups](../contract/audit-ops-console-contract-groups.md).

## Surface conventions

- **Base server path:** `/api/v1`. Every operation path below is rooted there.
- **Transports are parallel over one contract.** REST, the typed SDK (`Hexalith.Folders.Client`), the CLI, and
  the MCP server are parallel adapters of the same 47 canonical operations; the SDK is the canonical typed
  client and the CLI/MCP wrap it. Vocabularies (error categories, exit codes, failure kinds) are 1:1 with the
  parity oracle and are never collapsed or renamed.

### Security

- A single security scheme, `oidcBearer` (`type: openIdConnect`), is applied **globally** to every operation.
  Callers present a validated OIDC bearer JWT. The SDK leaves authentication to a bearer-token
  `DelegatingHandler` (see [authentication guidance](./authentication.md)); the issuer/audience are deployment
  configuration and appear in docs only as `.invalid` placeholders.
- Authoritative tenant and principal come from the authenticated context and EventStore claim-transform
  evidence — never from a request payload, header, or query value.

### Request headers on mutating operations

Mutating operations (`POST`/`PUT`) carry the canonical header triple:

| Header | Required | Purpose |
|---|---|---|
| `Idempotency-Key` | **required** | Caller-computed canonical key; replays return the same logical result. |
| `X-Correlation-Id` | optional | Correlation reference propagated across the operation and audit trail. |
| `X-Hexalith-Task-Id` | optional | Caller-provided task reference; the platform never generates it. |

- **Query rule:** non-mutating (`GET`) operations MUST NOT accept `Idempotency-Key`. Supplying one is a
  client-side usage error.
- **POST-as-query operations:** `ValidateProviderReadiness`
  (`POST /api/v1/provider-readiness/validations`) and the context-query POSTs
  (`GetFolderFileMetadata`, `SearchFolderFiles`, `GlobFolderFiles`, `ReadFileRange`) accept request bodies for
  evidence/query input but are read-only — they are **not** idempotency-keyed and exhibit snapshot-per-task
  read-consistency semantics.

### Canonical error shape (Problem Details)

Error responses follow RFC 9457 Problem Details extended with the canonical metadata-only Hexalith fields. The
spine `ProblemDetails` schema requires, in addition to `type`/`title`/`status`:

| Field | Meaning |
|---|---|
| `category` | Canonical error category (`CanonicalErrorCategory`); never collapsed across adapters. |
| `code` | Stable lowercase error code. |
| `message` | Metadata-only human message; no secrets, file contents, or existence hints. |
| `correlationId` | Opaque correlation reference for the failed operation. |
| `retryable` | Whether the caller may retry. |
| `clientAction` | One of `retry`, `revise_request`, `check_credentials`, `wait_for_reconciliation`, `contact_operator`, `no_action`. |
| `details.visibility` | Visibility class for the metadata-only `details` map; file contents, diffs, tokens, and unauthorized existence hints are forbidden. |

Task-scoped failures may include `taskId` as optional metadata-only additional evidence, but it is not a
required `ProblemDetails` property in the spine.

The CLI projects `category` to an [exit code](./cli-reference.md#exit-codes) and the MCP server projects it to
a [failure kind](./mcp-reference.md#failure-kind-catalog); both maps are 1:1 with the parity oracle.

## REST operations by tag group

The surface is organized into **9 operation tag groups**. Every `operationId`, method, and path below is
taken verbatim from the spine. The summaries cross-reference the contract authoring notes for normative
behavior; they are not re-specified here.

### Tag: `provider-readiness`

Provider binding configuration and read-only readiness/support evidence. See
[tenant/folder/provider/repository groups](../contract/tenant-folder-provider-repository-contract-groups.md).

| Operation | Method | Path | Notes |
|---|---|---|---|
| `ConfigureProviderBinding` | PUT | `/api/v1/provider-bindings/{providerBindingRef}` | Mutating; idempotency-keyed. |
| `GetProviderBinding` | GET | `/api/v1/provider-bindings/{providerBindingRef}` | Read-only. |
| `ValidateProviderReadiness` | POST | `/api/v1/provider-readiness/validations` | POST-as-query; read-only, not idempotency-keyed. |
| `GetProviderSupportEvidence` | GET | `/api/v1/provider-readiness/support-evidence` | Read-only. |

### Tag: `folders`

Folder identity, repository binding, ACL, branch-ref policy, and lifecycle status. See
[tenant/folder/provider/repository groups](../contract/tenant-folder-provider-repository-contract-groups.md).

| Operation | Method | Path | Notes |
|---|---|---|---|
| `CreateFolder` | POST | `/api/v1/folders` | Mutating; idempotency-keyed. |
| `CreateRepositoryBackedFolder` | POST | `/api/v1/folders/repository-backed` | Mutating; idempotency-keyed. |
| `BindRepository` | POST | `/api/v1/folders/{folderId}/repository-bindings` | Mutating; idempotency-keyed. |
| `GetRepositoryBinding` | GET | `/api/v1/folders/{folderId}/repository-bindings/{repositoryBindingId}` | Read-only. |
| `ArchiveFolder` | POST | `/api/v1/folders/{folderId}/archive` | Mutating; idempotency-keyed. |
| `GetFolderLifecycleStatus` | GET | `/api/v1/folders/{folderId}/lifecycle-status` | Read-only. |
| `GetEffectivePermissions` | GET | `/api/v1/folders/{folderId}/effective-permissions` | Read-only. |
| `ListFolderAclEntries` | GET | `/api/v1/folders/{folderId}/acl` | Read-only. |
| `UpdateFolderAclEntry` | PUT | `/api/v1/folders/{folderId}/acl/{aclEntryId}` | Mutating; idempotency-keyed. |
| `ConfigureBranchRefPolicy` | PUT | `/api/v1/folders/{folderId}/branch-ref-policy` | Mutating; idempotency-keyed. |
| `GetBranchRefPolicy` | GET | `/api/v1/folders/{folderId}/branch-ref-policy` | Read-only. |

### Tag: `workspaces`

Workspace preparation, locking, and transition/retry evidence. See
[workspace & lock groups](../contract/workspace-lock-contract-groups.md).

| Operation | Method | Path | Notes |
|---|---|---|---|
| `PrepareWorkspace` | POST | `/api/v1/folders/{folderId}/workspaces/{workspaceId}/preparation` | Mutating; idempotency-keyed. |
| `LockWorkspace` | POST | `/api/v1/folders/{folderId}/workspaces/{workspaceId}/lock` | Mutating; idempotency-keyed. |
| `ReleaseWorkspaceLock` | POST | `/api/v1/folders/{folderId}/workspaces/{workspaceId}/lock/release` | Mutating; idempotency-keyed. |
| `GetWorkspaceLock` | GET | `/api/v1/folders/{folderId}/workspaces/{workspaceId}/lock` | Read-only. |
| `GetWorkspaceRetryEligibility` | GET | `/api/v1/folders/{folderId}/workspaces/{workspaceId}/retry-eligibility` | Read-only. |
| `GetWorkspaceTransitionEvidence` | GET | `/api/v1/folders/{folderId}/workspaces/{workspaceId}/transition-evidence` | Read-only. |

### Tag: `files`

File add/change/remove mutations against a locked workspace. See
[file & context groups](../contract/file-context-contract-groups.md).

| Operation | Method | Path | Notes |
|---|---|---|---|
| `AddFile` | POST | `/api/v1/folders/{folderId}/workspaces/{workspaceId}/files/add` | Mutating; idempotency-keyed. |
| `ChangeFile` | PUT | `/api/v1/folders/{folderId}/workspaces/{workspaceId}/files/change` | Mutating; idempotency-keyed. |
| `RemoveFile` | POST | `/api/v1/folders/{folderId}/workspaces/{workspaceId}/files/remove` | Mutating; idempotency-keyed. |

### Tag: `commits`

Workspace commit and commit/provider evidence. See
[commit & status groups](../contract/commit-status-contract-groups.md).

| Operation | Method | Path | Notes |
|---|---|---|---|
| `CommitWorkspace` | POST | `/api/v1/folders/{folderId}/workspaces/{workspaceId}/commits` | Mutating; idempotency-keyed. |
| `GetCommitEvidence` | GET | `/api/v1/folders/{folderId}/workspaces/{workspaceId}/commits/{operationId}/evidence` | Read-only. |
| `GetProviderOutcome` | GET | `/api/v1/folders/{folderId}/workspaces/{workspaceId}/commits/{operationId}/provider-outcome` | Read-only. |

### Tag: `query-status`

Workspace, task, reconciliation, and cleanup status queries. See
[commit & status groups](../contract/commit-status-contract-groups.md).

| Operation | Method | Path | Notes |
|---|---|---|---|
| `GetWorkspaceStatus` | GET | `/api/v1/folders/{folderId}/workspaces/{workspaceId}/status` | Read-only. |
| `GetWorkspaceCleanupStatus` | GET | `/api/v1/folders/{folderId}/workspaces/{workspaceId}/cleanup/status` | Read-only. |
| `GetReconciliationStatus` | GET | `/api/v1/folders/{folderId}/workspaces/{workspaceId}/reconciliation/{reconciliationId}/status` | Read-only. |
| `GetTaskStatus` | GET | `/api/v1/tasks/{taskId}/status` | Read-only. |

### Tag: `audit`

Audit trail and operation-timeline reads. See
[audit & ops-console groups](../contract/audit-ops-console-contract-groups.md).

| Operation | Method | Path | Notes |
|---|---|---|---|
| `ListAuditTrail` | GET | `/api/v1/folders/{folderId}/audit-trail` | Read-only. |
| `GetAuditRecord` | GET | `/api/v1/folders/{folderId}/audit-trail/{auditRecordId}` | Read-only. |
| `ListOperationTimeline` | GET | `/api/v1/folders/{folderId}/operation-timeline` | Read-only. |
| `GetOperationTimelineEntry` | GET | `/api/v1/folders/{folderId}/operation-timeline/{timelineEntryId}` | Read-only. |

### Tag: `ops-console`

Read-only operations-console diagnostics (metadata-only; the consumer console surface itself is documented in
Story 7.14). See [audit & ops-console groups](../contract/audit-ops-console-contract-groups.md).

| Operation | Method | Path | Notes |
|---|---|---|---|
| `GetReadinessDiagnostics` | GET | `/api/v1/ops-console/readiness-diagnostics` | Read-only. |
| `GetProjectionFreshness` | GET | `/api/v1/ops-console/projection-freshness` | Read-only. |
| `GetProviderStatusDiagnostics` | GET | `/api/v1/folders/{folderId}/ops-console/provider-status-diagnostics` | Read-only. |
| `GetLockDiagnostics` | GET | `/api/v1/folders/{folderId}/workspaces/{workspaceId}/ops-console/lock-diagnostics` | Read-only. |
| `GetDirtyStateDiagnostics` | GET | `/api/v1/folders/{folderId}/workspaces/{workspaceId}/ops-console/dirty-state-diagnostics` | Read-only. |
| `GetSyncStatusDiagnostics` | GET | `/api/v1/folders/{folderId}/workspaces/{workspaceId}/ops-console/sync-status-diagnostics` | Read-only. |
| `GetFailedOperationDiagnostics` | GET | `/api/v1/folders/{folderId}/workspaces/{workspaceId}/ops-console/failed-operation-diagnostics` | Read-only. |

### Tag: `context-queries`

Read-only file-context queries (tree, metadata, search, glob, range-read). See
[file & context groups](../contract/file-context-contract-groups.md).

| Operation | Method | Path | Notes |
|---|---|---|---|
| `ListFolderFiles` | GET | `/api/v1/folders/{folderId}/workspaces/{workspaceId}/context/tree` | Read-only. |
| `GetFolderFileMetadata` | POST | `/api/v1/folders/{folderId}/workspaces/{workspaceId}/context/metadata` | POST-as-query; read-only. |
| `SearchFolderFiles` | POST | `/api/v1/folders/{folderId}/workspaces/{workspaceId}/context/search` | POST-as-query; read-only. |
| `GlobFolderFiles` | POST | `/api/v1/folders/{folderId}/workspaces/{workspaceId}/context/glob` | POST-as-query; read-only. |
| `ReadFileRange` | POST | `/api/v1/folders/{folderId}/workspaces/{workspaceId}/context/range-read` | POST-as-query; read-only. |

## SDK operation reference (typed client)

The typed SDK `Hexalith.Folders.Client` exposes `IClient` (namespace
`Hexalith.Folders.Client.Generated`), generated from the same spine. Every REST operation above has a matching
`…Async` method; the SDK is the canonical typed client that the CLI and MCP adapters wrap. Convenience helpers
(`Hexalith.Folders.Client.Convenience`) add the upload selector, correlation/task-ID sourcing, and the
idempotency-key helpers without introducing any field, state, or behavior absent from the spine. See the
[SDK quickstart](./quickstart.md) for runnable DI and upload examples.

### `IClient` operations by tag group

| Tag group | `IClient` operations |
|---|---|
| `provider-readiness` | `ConfigureProviderBindingAsync`, `GetProviderBindingAsync`, `ValidateProviderReadinessAsync`, `GetProviderSupportEvidenceAsync` |
| `folders` | `CreateFolderAsync`, `CreateRepositoryBackedFolderAsync`, `BindRepositoryAsync`, `GetRepositoryBindingAsync`, `ArchiveFolderAsync`, `GetFolderLifecycleStatusAsync`, `GetEffectivePermissionsAsync`, `ListFolderAclEntriesAsync`, `UpdateFolderAclEntryAsync`, `ConfigureBranchRefPolicyAsync`, `GetBranchRefPolicyAsync` |
| `workspaces` | `PrepareWorkspaceAsync`, `LockWorkspaceAsync`, `ReleaseWorkspaceLockAsync`, `GetWorkspaceLockAsync`, `GetWorkspaceRetryEligibilityAsync`, `GetWorkspaceTransitionEvidenceAsync` |
| `files` | `AddFileAsync`, `ChangeFileAsync`, `RemoveFileAsync` (and the `UploadFileAsync`/`UploadStreamedFileAsync` convenience helpers) |
| `commits` | `CommitWorkspaceAsync`, `GetCommitEvidenceAsync`, `GetProviderOutcomeAsync` |
| `query-status` | `GetWorkspaceStatusAsync`, `GetWorkspaceCleanupStatusAsync`, `GetReconciliationStatusAsync`, `GetTaskStatusAsync` |
| `audit` | `ListAuditTrailAsync`, `GetAuditRecordAsync`, `ListOperationTimelineAsync`, `GetOperationTimelineEntryAsync` |
| `ops-console` | `GetReadinessDiagnosticsAsync`, `GetProjectionFreshnessAsync`, `GetProviderStatusDiagnosticsAsync`, `GetLockDiagnosticsAsync`, `GetDirtyStateDiagnosticsAsync`, `GetSyncStatusDiagnosticsAsync`, `GetFailedOperationDiagnosticsAsync` |
| `context-queries` | `ListFolderFilesAsync`, `GetFolderFileMetadataAsync`, `SearchFolderFilesAsync`, `GlobFolderFilesAsync`, `ReadFileRangeAsync` |

### Golden lifecycle ordering

The canonical end-to-end consumer flow runs the operations in this **9-step order** (also exercised by the
hermetic example tests in `samples/Hexalith.Folders.Sample.Tests` and the compile-checked
`tests/tools/pattern-examples` snippet):

1. `ConfigureProviderBinding`
2. `ValidateProviderReadiness`
3. `CreateRepositoryBackedFolder`
4. `PrepareWorkspace`
5. `LockWorkspace`
6. `UploadFile` (convenience helper over `AddFile`/`ChangeFile`)
7. `CommitWorkspace`
8. `GetWorkspaceStatus`
9. `ListAuditTrail`

See the [workspace lifecycle diagram](../diagrams/workspace-lifecycle.md) and
[file → commit flow](../diagrams/file-commit-flow.md) for the state/event view.

### Idempotency-helper signature contract (parameter-order trap)

Generated request DTOs expose a `ComputeIdempotencyHash(...)` helper whose parameter order follows the **spine
path declaration**, not any older convention. For example:

```text
PrepareWorkspaceRequest.ComputeIdempotencyHash(folderId, workspaceId, taskId)
```

> **Trap:** the order is `(folderId, workspaceId, taskId)`. The earlier `(folderId, taskId, workspaceId)`
> order is **no longer emitted**; positional callers that still pass `(folderId, taskId, workspaceId)` compile
> but silently compute a diverging key. Always pass arguments in spine path order, or use named arguments.

Generated helpers are compatibility-gated by `HexalithFoldersGeneratedArtifacts.HelperSchemaVersion`; pin or
assert it when you cache or cross-version computed keys. The SDK never auto-generates or substitutes an
idempotency key — supplying one is an explicit caller decision. See
[SDK generation and idempotency helpers](../contract/sdk-generation-and-idempotency-helpers.md) and
[idempotency and parity rules](../contract/idempotency-and-parity-rules.md) for the hash format and
replay/conflict semantics.
