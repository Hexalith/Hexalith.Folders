# Hexalith.Folders CLI Reference

Status: Story 7.13 consumer reference.

The `folders` command-line interface is a thin adapter over the typed SDK (`Hexalith.Folders.Client`). It is
distributed as a **.NET tool** (`PackAsTool=true`, `ToolCommandName=folders`) — install it with
`dotnet tool install`, not as a library reference. Every command maps 1:1 to a canonical operation; the CLI
adds no behavior absent from the [Contract Spine](./api-reference.md).

All examples are **metadata-only**: identifiers are opaque, synthetic references. Never place secrets, bearer
tokens, raw file contents, base64 file bytes, diffs, provider payloads, real base addresses, or local absolute
paths in commands, scripts, logs, or examples. Command and option names are sourced from
`src/Hexalith.Folders.Cli` (System.CommandLine 2.0.8), not from running the binary.

See also: [API & SDK reference](./api-reference.md) · [MCP reference](./mcp-reference.md) ·
[authentication guidance](./authentication.md) · lifecycle diagrams
([workspace](../diagrams/workspace-lifecycle.md), [file→commit](../diagrams/file-commit-flow.md),
[auth/ACL](../diagrams/auth-acl-decision-flow.md)).

## Global options (recursive)

These options are recursive — valid on every leaf command:

| Option | Alias | Environment fallback | Constraint |
|---|---|---|---|
| `--base-address` | `-b` | `HEXALITH_FOLDERS_BASE_ADDRESS` | Absolute Folders REST base URL (transport endpoint only; never tenant authority). |
| `--token` | `-t` | see credential precedence below | Bearer JWT. |
| `--correlation-id` | | | Correlation reference propagated to every SDK sub-call; a fresh ULID is generated per invocation when omitted. |
| `--output` | `-o` | | Rendering mode, constrained to `human` (default) or `json`. |

`--output json` is filtered metadata-only: `contentBytes`, `inlineContent`, and `streamDescriptor` are
stripped at any depth so file bytes never reach stdout.

### Credential precedence (three layers)

The bearer token is resolved in this order (highest precedence first):

1. `HEXALITH_TOKEN` environment variable.
2. `~/.hexalith/credentials.json` — the per-tenant section selected by `HEXALITH_TENANT` (or the `default`
   section). Shape (synthetic placeholder — never commit a real token):

   ```json
   {
     "default": { "token": "<bearer-jwt-placeholder>" },
     "tenant_01HZY7Z6N7J4Q2X8Y9V0TEN001": { "token": "<bearer-jwt-placeholder>" }
   }
   ```

3. The `--token` / `-t` flag.

See [authentication guidance](./authentication.md#cli-and-mcp-credential-sourcing) for the full sourcing
contract.

## Mutation vs query options

Mutating commands (create/change/lock/commit/…) require explicit caller intent:

- `--task-id` — **required**, caller-provided; the CLI never generates a task ID.
- `--idempotency-key` **xor** `--allow-auto-key` — supply exactly one. `--idempotency-key` is your
  explicit caller-computed key; `--allow-auto-key` opts into CLI-generated ULID key emission (echoed to
  stderr).
- `--request` — the request body source: inline JSON, `@path` to read a JSON file, or `-` to read JSON from
  stdin.

Query commands are read-only: they **reject `--idempotency-key`** and exit `64` (usage error) if it is
supplied. Query commands accept `--correlation-id` and freshness/pagination options but never an idempotency
key.

## Command tree (7 groups)

The root `folders` command exposes **7 top-level groups**. Each leaf maps to one canonical operation.

### `folders provider`

| Command | Operation | Kind |
|---|---|---|
| `provider configure-binding` | `ConfigureProviderBinding` | mutating |
| `provider get-binding` | `GetProviderBinding` | query |
| `provider validate-readiness` | `ValidateProviderReadiness` | query (POST-as-query) |
| `provider support-evidence` | `GetProviderSupportEvidence` | query |

### `folders folder`

| Command | Operation | Kind |
|---|---|---|
| `folder create` | `CreateFolder` | mutating |
| `folder create-repo-backed` | `CreateRepositoryBackedFolder` | mutating |
| `folder bind-repo` | `BindRepository` | mutating |
| `folder get-repo-binding` | `GetRepositoryBinding` | query |
| `folder status` | `GetFolderLifecycleStatus` | query |
| `folder archive` | `ArchiveFolder` | mutating |
| `folder effective-permissions` | `GetEffectivePermissions` | query |
| `folder acl list` | `ListFolderAclEntries` | query |
| `folder acl update` | `UpdateFolderAclEntry` | mutating |
| `folder branch-policy set` | `ConfigureBranchRefPolicy` | mutating |
| `folder branch-policy get` | `GetBranchRefPolicy` | query |

### `folders workspace`

| Command | Operation | Kind |
|---|---|---|
| `workspace prepare` | `PrepareWorkspace` | mutating |
| `workspace lock` | `LockWorkspace` | mutating |
| `workspace release` | `ReleaseWorkspaceLock` | mutating |
| `workspace get-lock` | `GetWorkspaceLock` | query |
| `workspace status` | `GetWorkspaceStatus` | query |
| `workspace retry-eligibility` | `GetWorkspaceRetryEligibility` | query |
| `workspace transition-evidence` | `GetWorkspaceTransitionEvidence` | query |
| `workspace cleanup-status` | `GetWorkspaceCleanupStatus` | query |

### `folders file`

| Command | Operation | Kind |
|---|---|---|
| `file add` | `AddFile` | mutating |
| `file change` | `ChangeFile` | mutating |
| `file remove` | `RemoveFile` | mutating |

### `folders commit`

> **Naming quirk:** the mutating verb is **`create`**, not `commit`. A `commit` child under the `commit` group
> collides with the parent in the System.CommandLine 2.0.8 token table and crashes the parser for every
> `commit <subcommand>` invocation, so `commit create` maps to `CommitWorkspace`.

| Command | Operation | Kind |
|---|---|---|
| `commit create` | `CommitWorkspace` | mutating |
| `commit evidence` | `GetCommitEvidence` | query |
| `commit provider-outcome` | `GetProviderOutcome` | query |
| `commit reconciliation-status` | `GetReconciliationStatus` | query |
| `commit task-status` | `GetTaskStatus` | query |

### `folders context`

| Command | Operation | Kind |
|---|---|---|
| `context list` | `ListFolderFiles` | query |
| `context metadata` | `GetFolderFileMetadata` | query (POST-as-query) |
| `context search` | `SearchFolderFiles` | query (POST-as-query) |
| `context glob` | `GlobFolderFiles` | query (POST-as-query) |
| `context read-range` | `ReadFileRange` | query (POST-as-query) |

### `folders audit`

| Command | Operation | Kind |
|---|---|---|
| `audit list` | `ListAuditTrail` | query |
| `audit get` | `GetAuditRecord` | query |
| `audit timeline list` | `ListOperationTimeline` | query |
| `audit timeline get` | `GetOperationTimelineEntry` | query |

## Exit codes

The CLI uses the canonical sysexits-style table `{0, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 1}` from
`src/Hexalith.Folders.Cli/FoldersExitCodes.cs`, projected per canonical error category by
`src/Hexalith.Folders.Cli/Errors/ErrorProjection.cs`. This is deliberately **not** the
`Hexalith.EventStore.Admin.Cli` `Success=0/Degraded=1/Error=2` scheme. Every row is verified against the parity
oracle (`tests/fixtures/parity-contract.yaml`).

| Exit code | Constant | Canonical categories projected to this code |
|---|---|---|
| `0` | `Success` | `success` |
| `64` | `UsageError` | `client_configuration_error` (pre-SDK usage/config error; no HTTP call made — also the exit for a query command given `--idempotency-key`) |
| `65` | `CredentialMissing` | `credential_missing`, `authentication_failure`, `credential_reference_invalid` |
| `66` | `AccessDenied` | `tenant_access_denied`, `cross_tenant_access_denied`, `folder_acl_denied`, `audit_access_denied` |
| `67` | `LockConflict` | `workspace_locked`, `lock_conflict`, `lock_expired`, `lock_not_owned`, `stale_workspace` |
| `68` | `IdempotencyConflict` | `idempotency_conflict` |
| `69` | `ValidationError` | `validation_error`, `input_limit_exceeded`, `path_validation_failed`, `branch_ref_policy_invalid`, `response_limit_exceeded` |
| `70` | `ProviderFailure` | `provider_failure_known`, `provider_unavailable`, `provider_rate_limited`, `provider_readiness_failed`, `provider_permission_insufficient`, `repository_binding_unavailable`, `repository_conflict`, `duplicate_binding`, `unsupported_provider_capability`, `failed_operation`, `commit_failed`, `file_operation_failed` |
| `71` | `UnknownProviderOutcome` | `unknown_provider_outcome` (surfaced truthfully, never hidden) |
| `72` | `ReconciliationRequired` | `reconciliation_required`, `read_model_unavailable`, `projection_stale`, `projection_unavailable`, `workspace_not_ready`, `workspace_preparation_failed`, `dirty_workspace` |
| `73` | `NotFound` | `not_found`, `authorization_revocation_detected` |
| `74` | `StateTransitionInvalid` | `state_transition_invalid` |
| `75` | `Redacted` | `redacted` (visibly distinct from missing/unknown) |
| `1` | `InternalError` | `internal_error`, `query_timeout`, and any category not present in the oracle (e.g. `range_unsatisfiable`) — the documented drift fallback |

## Examples

```text
# Set the base address and token via environment (preferred over flags in scripts).
export HEXALITH_FOLDERS_BASE_ADDRESS=https://folders.internal/
export HEXALITH_TOKEN=<bearer-jwt-placeholder>

# Query: get folder lifecycle status as JSON (no idempotency key on queries).
folders folder status --folder-id folder_01HZY7Z6N7J4Q2X8Y9V0FLD001 --output json

# Mutating: prepare a workspace with an explicit task ID and caller-computed idempotency key,
# reading the request body from a file.
folders workspace prepare \
  --folder-id folder_01HZY7Z6N7J4Q2X8Y9V0FLD001 \
  --workspace-id workspace_01HZY7Z6N7J4Q2X8Y9V0WKS001 \
  --task-id task_01HZY7Z6N7J4Q2X8Y9V0TSK001 \
  --idempotency-key sha256:<hex-placeholder> \
  --request @prepare-workspace.json

# Mutating: commit a workspace (note 'commit create', not 'commit commit'), reading the body from stdin.
echo '{ ... }' | folders commit create \
  --folder-id folder_01HZY7Z6N7J4Q2X8Y9V0FLD001 \
  --workspace-id workspace_01HZY7Z6N7J4Q2X8Y9V0WKS001 \
  --task-id task_01HZY7Z6N7J4Q2X8Y9V0TSK001 \
  --allow-auto-key \
  --request -
```
