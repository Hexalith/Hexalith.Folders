---
baseline_commit: 037779256ca0f4e556d57c1da3337ff0977a37f4
---

# Story 5.2: Implement CLI commands with behavioral-parity rules

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a CLI user,
I want commands that mirror the canonical lifecycle,
so that terminal workflows behave like SDK and REST workflows.

## Acceptance Criteria

Source epic AC (epics.md#Story-5.2):

> **Given** the SDK client is available
> **When** CLI commands are implemented
> **Then** provider, folder, workspace, file, commit, context, and audit commands wrap SDK behavior
> **And** pre-SDK errors, idempotency-key sourcing, correlation sourcing, and exit codes follow the Adapter Parity Contract.

Decomposed, testable acceptance criteria:

1. **CLI wraps the SDK — no parallel behavior.** `Hexalith.Folders.Cli` is a thin adapter over `Hexalith.Folders.Client` (`IClient` + the existing `Convenience/` helpers). Every command's effect reduces to an `IClient` call; the CLI introduces **no** new request fields, lifecycle states, error categories, idempotency/canonicalization logic, or operations absent from the Contract Spine. The CLI must not reference `Hexalith.Folders.Server`, aggregates, workers, providers, EventStore, or Dapr (dependency direction: CLI → Client → Contracts only).

2. **Command surface covers the seven canonical groups.** A hierarchical `System.CommandLine` 2.0.8 command tree exposes the canonical lifecycle across these groups, each subcommand wrapping the matching `IClient` operation (see **§Command-to-SDK operation map**): `provider`, `folder`, `workspace`, `file`, `commit`, `context`, `audit`. At minimum every operation on the **golden lifecycle path** (configure provider binding → validate provider readiness → create repository-backed folder / bind repository → prepare workspace → lock → add/change file → commit → query context/status → release lock → inspect audit) is implemented end-to-end; remaining query operations in each group are wired with the same sourcing/exit-code rules.

3. **Idempotency-key sourcing (Adapter Parity Contract).** For every **mutating** command, the idempotency key is supplied via `--idempotency-key <key>` **OR** the opt-in `--allow-auto-key` flag (CLI generates a ULID and prints it to **stderr** for retry traceability). A mutating command invoked with **neither** flag is a pre-SDK usage error → **exit code 64** (`client_configuration_error`), and **no HTTP call is made**. The CLI **never** silently auto-generates a key; `--allow-auto-key` is the only path to a generated key, and the generated value is surfaced so the caller can retry idempotently. Non-mutating (query) commands must **reject** `--idempotency-key`/`--allow-auto-key` (they do not accept idempotency keys per the contract).

4. **Correlation-ID sourcing (Adapter Parity Contract).** Each CLI invocation generates **one** fresh ULID correlation ID and propagates it to **all** SDK sub-calls within that invocation. `--correlation-id <id>` overrides it for cross-tool tracing; when supplied it is echoed **unchanged** to the SDK (and therefore to the wire `X-Correlation-Id`). The correlation ID used is always observable to the caller (printed to stderr or included in output). Reuse the existing `CorrelationAndTaskId.ResolveCorrelationId` / `NewCorrelationId` helper — do not write a second ULID generator.

5. **Task-ID sourcing (Adapter Parity Contract).** Task-scoped commands (prepare, lock, release, file add/change/remove, commit, and any operation whose `IClient` signature requires `x_Hexalith_Task_Id`) require `--task-id <id>`. The CLI **never** generates a task ID. Missing `--task-id` on a task-scoped command is a pre-SDK usage error → **exit code 64**, no HTTP call. Reuse `CorrelationAndTaskId.ResolveTaskId` (fail-closed) rather than re-implementing the check.

6. **Credential sourcing (Adapter Parity Contract) with precedence.** Token resolution precedence is **(1)** `HEXALITH_TOKEN` env var, **(2)** `~/.hexalith/credentials.json` (per-tenant section), **(3)** `--token <jwt>` flag. When **no** token is found at all, the command fails **before any HTTP call** → **exit code 65** (`credential_missing`). The resolved token is attached as a bearer `DelegatingHandler` on the SDK `HttpClient` (mirror the Story 5.1 sample's `BearerTokenHandler` pattern — do not invent a new auth scheme). Tokens, file paths, and credential file contents must never appear in stdout/stderr/output (metadata-only).

7. **Pre-SDK vs post-SDK error mapping is exact and mutually exclusive.**
   - **Pre-SDK** (before any HTTP call): bad usage / missing required flag → `client_configuration_error` → **exit 64**; missing credentials → `credential_missing` → **exit 65**. These never reach the SDK.
   - **Post-SDK** (server returned RFC 9457 + canonical category): the CLI catches `HexalithFoldersApiException<ProblemDetails>` and maps `ProblemDetails.Category` → exit code via the **canonical category→exit-code projection** (see **§Canonical exit-code projection** — sourced verbatim from `tests/fixtures/parity-contract.yaml` `outcome_mapping`). A bare `HexalithFoldersApiException` (unexpected/unmapped status, null typed result) → **exit 1** (`internal_error`) with the correlation ID always emitted to stderr.
   - A single operation can never produce both a pre-SDK and a post-SDK error class.

8. **Exit codes match the canonical projection (not the EventStore 0/1/2 scheme).** The CLI uses the **sysexits-style canonical table** `{0, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 1}`. Do **not** reuse `Hexalith.EventStore.Admin.Cli.ExitCodes` (that module's `Success=0/Degraded=1/Error=2` is its own UX-DR52 convention and is **wrong** for Folders). The mapping must be the same single canonical projection for every command, consistent with the per-operation `cli_exit_code` and `outcome_mapping` rows in `parity-contract.yaml`.

9. **Output modes are metadata-only.** A global `--output <human|json>` (default `human`) controls rendering. `json` emits the SDK result/`ProblemDetails` shape; `human` emits a readable summary. **Both** modes are metadata-only: never print file bytes, base64 content, diffs, provider payloads, secrets/tokens, local absolute paths, or unauthorized-resource-existence hints. `AcceptedCommand` responses surface `correlationId`, `taskId`, `status`, and `idempotentReplay` truthfully (replay is not hidden). Unknown/reconciliation outcomes (`unknown_provider_outcome`, `reconciliation_required`) are surfaced truthfully, never papered over.

10. **File commands reuse the SDK upload convenience.** `file add`/`file change` use the existing `FoldersFileUploadExtensions.UploadFileAsync` / `FileUpload` helpers (inline ≤ 262144 bytes, streamed above; `FileUploadStreamingRequiredException` handled) rather than hand-building `FileMutationRequest`. The CLI reads file content from a caller-provided path/stdin and passes bytes/stream + metadata to the helper. No file content ever appears in logs, errors, or output.

11. **Hermetic build/test.** The CLI and its tests build and run with **no** provider credentials, **no** running Dapr sidecars, **no** Keycloak/Redis, **no** network calls, and **no** nested submodule initialization. Behavioral-parity assertions (sourcing precedence, pre-SDK exit codes, category→exit-code projection, `--allow-auto-key` stderr emission, metadata-only output) run against a fake `HttpMessageHandler`/`IClient` — never a live server. (Oracle-driven test consumption is **Story 5.4**, not this story; here, encode the mapping and unit-test it directly.)

## Tasks / Subtasks

- [x] **Task 1 — CLI composition root & global options** (AC: #1, #2, #4, #6, #9)
  - [x] Replace `src/Hexalith.Folders.Cli/Program.cs` scaffold with a `System.CommandLine` 2.0.8 `RootCommand` that registers the seven group subcommands and recursive global options. Use the GA API (`new Option<T>("--name","-n"){ Recursive = true, DefaultValueFactory = ... }`, `rootCommand.Parse(args).InvokeAsync()`, `command.SetAction(...)`, `parseResult.GetValue(option)`) — mirror `Hexalith.EventStore.Admin.Cli/Program.cs` + `GlobalOptionsBinding.cs` for the option-binding shape, but **do not** copy its exit-code scheme or its profile store.
  - [x] Global options: `--base-address|-b` (folders REST base URL; env fallback e.g. `HEXALITH_FOLDERS_BASE_ADDRESS`), `--token|-t`, `--correlation-id`, `--output|-o` (`human` default, `json`; constrain via `AcceptOnlyFromAmong`). Build a `GlobalOptions` record + a binding resolver analogous to the EventStore reference.
  - [x] Compose an `IClient` from `AddFoldersClient(...)` with `BaseAddress` set from the resolved option and a bearer `DelegatingHandler` carrying the resolved token (reuse the Story 5.1 `BearerTokenHandler` shape; do not duplicate `AddFoldersClient` wiring).
  - [x] Add `--version`/`-v` short-circuit (assembly informational version) as in the reference Program.cs.

- [x] **Task 2 — Credential resolution with precedence + pre-SDK exit 65** (AC: #6, #7, #11)
  - [x] Implement token precedence `HEXALITH_TOKEN` env → `~/.hexalith/credentials.json` (per-tenant section) → `--token`. Make the credentials-file path injectable for hermetic tests (constructor/optional param), like the EventStore `ProfilePath` test seam.
  - [x] When no token resolves, fail **before** constructing/calling the SDK → exit **65** (`credential_missing`), with a metadata-only stderr message (no path leak, no token).
  - [x] Unit-test all three precedence layers + the missing-token exit-65 path with a temp `HOME`/injected path; assert no token/path text leaks to output.

- [x] **Task 3 — Canonical exit-code projection + error handling** (AC: #7, #8, #9, #11)
  - [x] Add a `FoldersExitCodes` static type (constants `Success=0`, `UsageError=64`, `CredentialMissing=65`, … `InternalError=1`) and a single `CanonicalErrorCategory → int` projection map encoding **§Canonical exit-code projection** verbatim. Do not collapse distinct categories.
  - [x] Implement a top-level handler wrapper: catch `HexalithFoldersApiException<ProblemDetails>` → project `Result.Category`; catch bare `HexalithFoldersApiException` (null/untyped) → exit 1 and emit `correlationId` to stderr; pre-SDK usage/credential failures short-circuit to 64/65 before any call.
  - [x] Render errors metadata-only in both `human` and `json` modes from `ProblemDetails` (`category`, `code`, `message`, `correlationId`, `retryable`, `clientAction`, `details`). Never include `Response` body raw text that could carry leaked content — project from the typed `ProblemDetails` only.
  - [x] Unit-test the projection for every category in the map (theory over the category enum) and the unexpected-status → exit 1 path.

- [x] **Task 4 — Idempotency-key, correlation, task-ID sourcing** (AC: #3, #4, #5, #7, #11)
  - [x] Add per-command shared options: mutating commands get `--idempotency-key` and `--allow-auto-key`; task-scoped commands get `--task-id`. Queries get **neither** idempotency option.
  - [x] Resolve correlation once per invocation via `CorrelationAndTaskId.ResolveCorrelationId(explicit, provider:null)`; thread the same value to all sub-calls; echo it (stderr/output).
  - [x] Resolve task ID via `CorrelationAndTaskId.ResolveTaskId(--task-id)`; on the fail-closed `InvalidOperationException`, map to **pre-SDK usage error exit 64** (do not let it become an unhandled exception/exit 1).
  - [x] Idempotency: if `--idempotency-key` present, use it; else if `--allow-auto-key`, generate a ULID (`CorrelationAndTaskId.NewCorrelationId()` shape is a valid `OpaqueIdentifier`) and print `idempotency-key: <key>` to **stderr**; else (mutating, neither) → exit **64** with no HTTP call.
  - [x] Unit-test: missing key on mutating cmd → 64 (no call); `--allow-auto-key` prints key to stderr and proceeds; query cmd rejects `--idempotency-key`; missing `--task-id` on task-scoped cmd → 64; explicit `--correlation-id` echoed unchanged to the captured request header.

- [x] **Task 5 — Implement the seven command groups over IClient** (AC: #1, #2, #9, #10)
  - [x] Create `Commands/{Provider,Folder,Workspace,File,Commit,Context,Audit}/` command classes, each exposing a `Create(GlobalOptionsBinding, Func<IClient>)`-style factory returning a `System.CommandLine.Command`. Map subcommands per **§Command-to-SDK operation map**.
  - [x] `file add`/`file change` use `FoldersFileUploadExtensions.UploadFileAsync` (read bytes/stream from a `--file <path>` or stdin); `file remove` builds the metadata-only removal via the helper/`FileUpload` builder. Handle `FileUploadStreamingRequiredException` as a retryable, content-safe outcome.
  - [x] Query commands (`context *`, `audit *`, `workspace status/lock/cleanup/...`, `provider get/...`, `commit evidence/outcome/reconciliation/task-status`) pass `--freshness` (maps to `ReadConsistencyClass?`) and pagination (`--cursor`, `--limit`, `--filter`) where the `IClient` signature has them. Queries take correlation + (where required by signature) task-id, but **never** idempotency keys.
  - [x] Every command returns the projected exit code and respects `--output`.

- [x] **Task 6 — Tests (hermetic, behavioral-parity-oriented)** (AC: #3–#9, #11)
  - [x] Replace/extend `tests/Hexalith.Folders.Cli.Tests/CliSmokeTests.cs` with focused suites: exit-code projection map; pre-SDK usage (64) and credential (65) paths; idempotency-key sourcing (missing→64, `--allow-auto-key`→stderr key, query rejection); correlation default + override echo; task-id fail-closed→64; metadata-only output (assert forbidden substrings never appear); `file add` transport-shape selection via helper.
  - [x] Drive commands through `rootCommand.Parse(args).InvokeAsync()` with a fake `HttpMessageHandler` (return canned `ProblemDetails`/`AcceptedCommand` JSON) or a fake `IClient` (NSubstitute) — no live server, no Dapr/Keycloak/Redis/network. Capture stdout/stderr and exit codes.
  - [x] Use xUnit v3 + Shouldly; `TestContext.Current.CancellationToken` for async; reuse `src/Hexalith.Folders.Testing` factories/HTTP helpers where they fit.

- [x] **Task 7 — Verify build + focused tests** (AC: #1, #11)
  - [x] `dotnet restore Hexalith.Folders.slnx`; `dotnet build Hexalith.Folders.slnx --no-restore` (0 warnings / 0 errors); then `dotnet test tests/Hexalith.Folders.Cli.Tests` (and Client tests if helper surface touched).
  - [x] Confirm: no edits under `Generated/`; no inline package `Version` attributes; no recursive submodule commands; CLI csproj still references only `Hexalith.Folders.Client`.

## Dev Notes

### Scope boundaries (read first)

- **In scope:** Implement `src/Hexalith.Folders.Cli` as a thin adapter over `Hexalith.Folders.Client`: the seven command groups, the Adapter Parity Contract behavioral rules (idempotency-key/correlation/task-id/credential sourcing), the pre-SDK error classes, and the canonical category→exit-code projection. Hermetic CLI unit tests asserting those behaviors.
- **OUT of scope (do NOT implement here):**
  - **Story 5.3** — MCP tools/resources/failure-kinds (`src/Hexalith.Folders.Mcp`). Leave the MCP scaffold untouched.
  - **Story 5.4** — wiring `tests/Hexalith.Folders.Cli.Tests` to *consume* `parity-contract.yaml` as xUnit theory data. **This story uses the oracle as the source of truth for the mapping and encodes it; it does not build the oracle-consumption test harness.** (Encode the map; 5.4 proves it against the oracle.)
  - **Stories 5.5–5.7** — golden/behavioral/mixed-surface parity validation runs.
  - Any Contract Spine change, regeneration, server/aggregate/worker behavior, or new SDK convenience helper beyond what 5.1 shipped.
- **Negative scope note for the dev:** if you find yourself adding a request field, a new error category, a second ULID generator, a new auth scheme, or a category the oracle doesn't list — stop; that is out of scope and almost certainly a mistake.

### Critical guardrail — thin adapter, no behavioral invention

The defining constraint (epic AC + architecture §"Adapter Parity Contract"): wrapping the SDK collapses **transport** parity but **not behavioral** parity. The CLI must encode exactly the per-adapter behavioral rules below and nothing more. Every command effect must reduce to an `IClient` call with arguments already present in `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`.

### Current state — what already exists (do not reinvent)

- **CLI is a 3-line scaffold.** `src/Hexalith.Folders.Cli/Program.cs` prints `"{FoldersClientModule.Name} CLI scaffold"`. The csproj already: `OutputType=Exe`, `PackAsTool=true`, `ToolCommandName=folders`, `IsPackable=true`, references `System.CommandLine` (pinned **2.0.8** centrally) and `Hexalith.Folders.Client`, and has `InternalsVisibleTo Hexalith.Folders.Cli.Tests`. Both projects are already in `Hexalith.Folders.slnx`. The test project references the CLI + `Hexalith.Folders.Testing` and has only `CliSmokeTests` (a compile guard).
- **Typed SDK (`IClient`).** `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs` — async-only with `CancellationToken` overloads; mutation methods take the header triple as **explicit string parameters** `(… , string idempotency_Key, string x_Correlation_Id, string x_Hexalith_Task_Id, <Request> body, CancellationToken)`. Queries take `string x_Correlation_Id, ReadConsistencyClass? x_Hexalith_Freshness` (some also `x_Hexalith_Task_Id`, plus `cursor`/`limit`/`filter`). DI: `FoldersClientServiceCollectionExtensions.AddFoldersClient(...)` + `FoldersClientOptions` (`BaseAddress`, config section `"Folders"`); auth is attached by the caller via a bearer `DelegatingHandler` on the returned `IHttpClientBuilder` (the module does not own auth).
- **Convenience helpers (Story 5.1, `src/Hexalith.Folders.Client/Convenience/`) — REUSE, do not duplicate:**
  - `CorrelationAndTaskId.ResolveCorrelationId(explicit, ICorrelationIdProvider?)` (explicit → provider → fresh ULID), `ResolveTaskId(taskId)` (fail-closed `InvalidOperationException` when blank — **never** SDK-generated), `NewCorrelationId()` (self-contained 26-char Crockford ULID, valid `OpaqueIdentifier`).
  - `FoldersFileUploadExtensions.UploadFileAsync` (`ReadOnlyMemory<byte>` + bounded `Stream` overloads) and `UploadStreamedFileAsync` over `IClient.AddFileAsync`/`ChangeFileAsync`; `FileUpload` pure builders (inline boundary **262144** bytes; `FileUploadStreamingRequiredException` on inline-over-boundary / server `413` + `X-Hexalith-Retry-Transport: stream`).
  - `ComputeIdempotencyKey` on `FileUpload` delegates to the generated `ComputeIdempotencyHash()` — use it; never reimplement canonicalization.
- **SDK error model.** The generated client throws `HexalithFoldersApiException<ProblemDetails>` for categorized failures (401/403/404/409/422/503/etc.) and bare `HexalithFoldersApiException` for unexpected statuses / null bodies. `ProblemDetails` (generated) carries: `Category` (`CanonicalErrorCategory` enum, string-serialized), `Code` (string), `Message`, `CorrelationId`, `Retryable` (bool), `ClientAction` (`ProblemDetailsClientAction` enum), `Details` (`Dictionary<string,string>`, metadata-only). `AcceptedCommand` carries `AcceptedAt`, `CorrelationId`, `TaskId`, `Status`, `IdempotentReplay`.
- **Reference adapter:** `Hexalith.EventStore.Admin.Cli` (sibling submodule) is the **structural** template for System.CommandLine 2.0.8 (Program.cs root + subcommands, `GlobalOptionsBinding.Create()`/`Resolve(parseResult)`, recursive options, env-var `DefaultValueFactory`, `--version` short-circuit, `AcceptOnlyFromAmong`). **Borrow the wiring shape only.** Its `ExitCodes` (0/1/2) and profile store are EventStore-specific and must NOT be copied — Folders uses the canonical sysexits table below.

### Adapter Parity Contract — the behavioral rules the CLI MUST honor

[Source: architecture.md#Adapter-Parity-Contract, decisions A-4/A-9/A-10; tests/fixtures/parity-contract.yaml]

| Dimension | CLI rule |
| --- | --- |
| **Idempotency-Key** | `--idempotency-key <key>` (required for mutating cmds) OR `--allow-auto-key` (CLI generates ULID, prints to **stderr**). Neither, on a mutating cmd → **exit 64**, no HTTP call. Never silently auto-generated. Queries reject the flag. |
| **CorrelationId** | Fresh ULID per invocation, propagated to all sub-calls; `--correlation-id` overrides and is echoed **unchanged**. Always observable to caller. |
| **TaskId** | `--task-id` required for task-scoped cmds (prepare, lock, release, file mutation, commit, + any op whose signature needs `x_Hexalith_Task_Id`). Never CLI-generated. Missing → **exit 64**, no HTTP call. |
| **Credential** | Precedence `HEXALITH_TOKEN` env → `~/.hexalith/credentials.json` (per-tenant) → `--token`. None → **exit 65**, no HTTP call. |
| **Pre-SDK error** | `client_configuration_error` → 64; `credential_missing` → 65. Before any HTTP call. Mutually exclusive with post-SDK classes. |
| **Post-SDK error** | Catch `HexalithFoldersApiException<ProblemDetails>` → project `Category` → exit code per table below + render per `--output`. |

**Cross-adapter invariants (will be asserted by 5.4/5.6 — keep them true now):** same input + same authoritative tenant + same correlation/task/idempotency triple ⇒ identical (category, code, retryable, clientAction) across SDK/REST/CLI/MCP; correlation echoed unchanged; replay semantics identical.

### Canonical exit-code projection (authoritative — from `parity-contract.yaml` `outcome_mapping`)

The architecture's §"CLI exit-code mapping" lists 13 *summary* rows, but the wire `category` field uses the **finer** `CanonicalErrorCategory` enum (40+ values). Encode the full projection below (deduplicated union of every `outcome_mapping` row in `tests/fixtures/parity-contract.yaml` — that file is the source of truth; reconcile against it if it changes):

| Canonical category | Exit | | Canonical category | Exit |
| --- | --- | --- | --- | --- |
| `success` | 0 | | `provider_failure_known` | 70 |
| `client_configuration_error` (pre-SDK) | 64 | | `provider_unavailable` | 70 |
| `authentication_failure` | 65 | | `provider_rate_limited` | 70 |
| `credential_missing` | 65 | | `provider_readiness_failed` | 70 |
| `credential_reference_invalid` | 65 | | `provider_permission_insufficient` | 70 |
| `tenant_access_denied` | 66 | | `repository_binding_unavailable` | 70 |
| `cross_tenant_access_denied` | 66 | | `repository_conflict` | 70 |
| `folder_acl_denied` | 66 | | `duplicate_binding` | 70 |
| `audit_access_denied` | 66 | | `unsupported_provider_capability` | 70 |
| `workspace_locked` | 67 | | `failed_operation` / `commit_failed` / `file_operation_failed` | 70 |
| `lock_conflict` | 67 | | `unknown_provider_outcome` | 71 |
| `lock_expired` | 67 | | `reconciliation_required` | 72 |
| `lock_not_owned` | 67 | | `read_model_unavailable` | 72 |
| `stale_workspace` | 67 | | `projection_stale` / `projection_unavailable` | 72 |
| `idempotency_conflict` | 68 | | `workspace_not_ready` / `workspace_preparation_failed` | 72 |
| `validation_error` | 69 | | `dirty_workspace` | 72 |
| `input_limit_exceeded` | 69 | | `not_found` | 73 |
| `path_validation_failed` | 69 | | `authorization_revocation_detected` | 73 |
| `branch_ref_policy_invalid` | 69 | | `state_transition_invalid` | 74 |
| `response_limit_exceeded` | 69 | | `redacted` | 75 |
| `query_timeout` | 1 | | `internal_error` | 1 |

Notes: `authentication_failure`/`credential_*` map to **65** (credential family), not a separate auth code. `query_timeout` and `internal_error` are the only post-SDK categories mapping to **1**. Any category not present in the SDK enum that the server could still return must fall through to **1** with the correlation ID emitted — and be flagged, because it implies oracle/spine drift.

### Command-to-SDK operation map (`IClient`)

| CLI group | Subcommands → `IClient` operation (mutating ⇒ needs idempotency-key + task-id) |
| --- | --- |
| **provider** | `configure-binding` → `ConfigureProviderBindingAsync` (M); `get-binding` → `GetProviderBindingAsync`; `validate-readiness` → `ValidateProviderReadinessAsync` (correlation+freshness, no idempotency); `support-evidence` → `GetProviderSupportEvidenceAsync` |
| **folder** | `create` → `CreateFolderAsync` (M); `create-repo-backed` → `CreateRepositoryBackedFolderAsync` (M); `bind-repo` → `BindRepositoryAsync` (M); `get-repo-binding` → `GetRepositoryBindingAsync`; `status` → `GetFolderLifecycleStatusAsync`; `archive` → `ArchiveFolderAsync` (M); `acl list` → `ListFolderAclEntriesAsync`; `acl update` → `UpdateFolderAclEntryAsync` (M); `effective-permissions` → `GetEffectivePermissionsAsync`; `branch-policy set` → `ConfigureBranchRefPolicyAsync` (M); `branch-policy get` → `GetBranchRefPolicyAsync` |
| **workspace** | `prepare` → `PrepareWorkspaceAsync` (M); `lock` → `LockWorkspaceAsync` (M); `get-lock` → `GetWorkspaceLockAsync`; `release` → `ReleaseWorkspaceLockAsync` (M); `retry-eligibility` → `GetWorkspaceRetryEligibilityAsync`; `transition-evidence` → `GetWorkspaceTransitionEvidenceAsync`; `status` → `GetWorkspaceStatusAsync`; `cleanup-status` → `GetWorkspaceCleanupStatusAsync` |
| **file** | `add` → `AddFileAsync` (M, via upload helper); `change` → `ChangeFileAsync` (M, via upload helper); `remove` → `RemoveFileAsync` (M) |
| **context** | `list` → `ListFolderFilesAsync`; `metadata` → `GetFolderFileMetadataAsync`; `search` → `SearchFolderFilesAsync`; `glob` → `GlobFolderFilesAsync`; `read-range` → `ReadFileRangeAsync` (all queries; carry task-id where the signature requires it; never idempotency keys) |
| **commit** | `commit` → `CommitWorkspaceAsync` (M); `evidence` → `GetCommitEvidenceAsync`; `provider-outcome` → `GetProviderOutcomeAsync`; `reconciliation-status` → `GetReconciliationStatusAsync`; `task-status` → `GetTaskStatusAsync` |
| **audit** | `list` → `ListAuditTrailAsync`; `get` → `GetAuditRecordAsync`; `timeline list` → `ListOperationTimelineAsync`; `timeline get` → `GetOperationTimelineEntryAsync` |

Priority: implement the **golden lifecycle path** operations end-to-end first (provider configure-binding/validate-readiness → folder create-repo-backed/bind-repo → workspace prepare/lock → file add/change → commit → context list/status → workspace release → audit list). The remaining queries follow the identical sourcing/exit-code pattern; structure the code so adding them is mechanical.

### Previous-story intelligence (lessons to carry in)

- **Story 5.1 (SDK convenience):** generated files are off-limits; helpers live outside `Generated/`. `CorrelationAndTaskId` + `FileUpload`/`FoldersFileUploadExtensions` already implement the exact sourcing/upload behavior the CLI needs — wrap them, don't reimplement. The `FileMutationRequest` wire shape is subtle (string `transportOperation` discriminator + `inlineContent`/`streamDescriptor` via extension data); going through `UploadFileAsync` avoids that entirely. The implemented **spine is authoritative** when prose drifts. One pre-existing environmental test (`ClientGenerationTests.GeneratedClientAndHelpersMatchIsolatedRegeneration`, whitespace-only) fails regardless of this story — ignore it; it is not a CLI regression.
- **Story 4.11 (correlation/task/idempotency):** task ID must **fail closed** when missing on task-scoped ops (never coerced to empty); replay evidence (`idempotentReplay`) must survive to the caller. The CLI surfaces it via `AcceptedCommand.IdempotentReplay`.
- **Story 4.12 (commit):** unknown provider outcomes become `unknown_provider_outcome`/`reconciliation_required` — the CLI must surface those states/exit codes (71/72) truthfully, never retry-loop or hide them.
- **Epic 1 retro:** prefer executable boundaries over prose; record negative scope explicitly; metadata-only is mandatory everywhere including CLI stdout/stderr and test output.

### Git intelligence

- `baseline_commit`: `0377792` (`feat(story-5.1): Ship SDK convenience helpers, samples, and quickstart`). Recent history is Epic 4 lifecycle validation + Story 5.1; the Client/Convenience surface is freshly settled and is the base this story builds on. Commit convention: `feat(story-5.2): <imperative summary>`. Do **not** touch submodules (the `chore: update submodule references` commits are unrelated). The working tree has one unrelated modified file under `_bmad-output/story-automator/` — leave it.

### Testing requirements

[Source: project-context.md#Testing-Rules]

- Tests live in `tests/Hexalith.Folders.Cli.Tests/`; xUnit v3 + Shouldly; NSubstitute for focused doubles (fake `IClient` or fake `HttpMessageHandler`). Use `TestContext.Current.CancellationToken` in async tests.
- **Hermetic, no live anything:** drive `rootCommand.Parse(args).InvokeAsync()` and assert exit code + captured stdout/stderr against canned `ProblemDetails`/`AcceptedCommand` JSON. Make the credentials-file path and the `IClient` factory injectable so tests never read `~/.hexalith` or open sockets.
- Assert the **behavioral-parity** dimensions directly (this story owns encoding them; 5.4 owns proving them against the oracle): exit-code projection per category; pre-SDK 64/65; idempotency missing→64 / `--allow-auto-key` stderr key / query rejection; correlation default + override echo; task-id fail-closed→64; metadata-only output (assert tokens/paths/file-bytes never appear).
- Reuse `src/Hexalith.Folders.Testing` factories/HTTP helpers where they fit (the 5.1 sample tests used a fake `HttpMessageHandler` — mirror that).

### Project Structure Notes

- New CLI source under `src/Hexalith.Folders.Cli/`: `Program.cs` (root + composition), `GlobalOptions.cs` + `GlobalOptionsBinding.cs` (mirror EventStore shape), `FoldersExitCodes.cs`, `ErrorProjection.cs` (category→exit + metadata-only render), `Credentials/` (token precedence resolver), `Commands/{Provider,Folder,Workspace,File,Commit,Context,Audit}/*.cs`. Keep one primary public type per file, file-scoped namespaces, `Async` suffix, `ConfigureAwait(false)` on library-style awaits.
- **Dependency direction preserved:** CLI references `Hexalith.Folders.Client` only (already so). No Server/aggregate/worker/provider/EventStore/Dapr references. No inline package `Version` attributes (central `Directory.Packages.props`).
- `PackAsTool=true`/`ToolCommandName=folders` stays; `--version` should report the assembly informational version.
- No conflicts with the unified structure detected; the layout mirrors `Hexalith.EventStore.Admin.Cli` (the architecture's chosen adapter template).

### References

- [Source: epics.md#Epic-5 / #Story-5.2] — epic objective (cross-surface parity) and the verbatim Story 5.2 acceptance criteria.
- [Source: architecture.md#Adapter-Parity-Contract (lines ~498–539)] — per-adapter behavioral table, CLI exit-code mapping (13-row summary), MCP failure-kind set, cross-adapter invariants.
- [Source: architecture.md#Decision-A-4] — System.CommandLine 2.x as the CLI framework wrapping `Hexalith.Folders.Client`.
- [Source: architecture.md#Decision-A-9 / #A-10] — idempotency-key per-command rule; correlation + task-ID propagation across REST/SDK/CLI/MCP.
- [Source: architecture.md#C13 / Selected-Starter] — parity oracle columns; `Hexalith.Tenants` baseline + `Hexalith.EventStore.Admin.{Cli,Mcp,UI}` adapter template; `tests/Hexalith.Folders.Cli.Tests` consumes `parity-contract.yaml` (that consumption is Story 5.4).
- [Source: tests/fixtures/parity-contract.yaml] — per-operation `behavioral_parity` + `outcome_mapping` (canonical category → `cli_exit_code` / `mcp_failure_kind` / `pre_sdk_error_class`). **Authoritative source for §Canonical exit-code projection.**
- [Source: project-context.md] — CLI wraps the Client (never duplicates business behavior); CLI exit codes map 1:1 to canonical categories (do not collapse); metadata-only everywhere; central package management; generated-file edit prohibition; submodule policy; hermetic-gate rules.
- Real code anchors:
  - `src/Hexalith.Folders.Cli/Program.cs` (scaffold to replace), `src/Hexalith.Folders.Cli/Hexalith.Folders.Cli.csproj`.
  - `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs` (`IClient` signatures, `ProblemDetails`, `CanonicalErrorCategory`, `AcceptedCommand`, `HexalithFoldersApiException<T>`).
  - `src/Hexalith.Folders.Client/Convenience/CorrelationAndTaskId.cs`, `.../FoldersFileUploadExtensions.cs`, `.../FileUpload.cs`.
  - `src/Hexalith.Folders.Client/FoldersClientServiceCollectionExtensions.cs`, `.../FoldersClientOptions.cs`.
  - `samples/Hexalith.Folders.Sample/BearerTokenHandler.cs` (bearer `DelegatingHandler` pattern to reuse).
  - Reference adapter: `Hexalith.EventStore/src/Hexalith.EventStore.Admin.Cli/{Program.cs,GlobalOptions.cs,GlobalOptionsBinding.cs}` (wiring shape only — NOT its `ExitCodes`).
  - `tests/Hexalith.Folders.Cli.Tests/CliSmokeTests.cs` (extend), `src/Hexalith.Folders.Testing` (fakes/factories).

### Latest technical notes (pinned versions — do not bump in this story)

Versions are centrally managed in `Directory.Packages.props`; treat repo config as authoritative. Relevant pins: **System.CommandLine 2.0.8** (GA-era API — constructor `new Option<T>("--name","-n")`, `Recursive`, `DefaultValueFactory`, `AcceptOnlyFromAmong`, `command.SetAction(...)`, `rootCommand.Parse(args).InvokeAsync()`, `parseResult.GetValue(...)`; **not** the old beta `Handler.SetHandler` API). xUnit v3 `3.2.2`, Shouldly `4.3.0`, NSubstitute `5.3.0`, Newtonsoft.Json `13.0.4` (SDK serialization). Do not add packages; do not normalize Aspire/Dapr versions; do not regenerate the client. Verify the exact System.CommandLine 2.0.8 surface against the EventStore reference (same pin) before coding — the API differs sharply between System.CommandLine beta/rc/GA, and the reference is known-good for this pin.

## Dev Agent Record

### Agent Model Used

claude-opus-4-7[1m] (Opus 4.7, 1M context)

### Debug Log References

- Build/test run via the Windows .NET SDK 10.0.300 (`/mnt/c/Program Files/dotnet/dotnet.exe`) because the WSL SDK is 10.0.108 and `global.json` pins 10.0.300 with `rollForward=latestPatch`.
- `dotnet build Hexalith.Folders.slnx`: **0 Warning(s), 0 Error(s)**.
- `dotnet test tests/Hexalith.Folders.Cli.Tests`: **102 passed, 0 failed** (re-verified at review).
- `dotnet test tests/Hexalith.Folders.Client.Tests`: 71 passed, 1 failed — the failure is the pre-existing whitespace-only `ClientGenerationTests.GeneratedClientAndHelpersMatchIsolatedRegeneration` documented in Dev Notes as environmental and unrelated to this story (no Client code was touched).

### Completion Notes List

- **Thin adapter, no behavioral invention (AC #1):** the CLI references only `Hexalith.Folders.Client`; every command effect reduces to one `IClient` call. No new request fields, error categories, idempotency/canonicalization logic, or auth scheme were introduced. Request bodies are passed through verbatim via `--request` (inline JSON / `@file` / `-` stdin); file content goes through the Story 5.1 upload convenience.
- **Seven command groups (AC #2):** `provider`, `folder`, `workspace`, `file`, `commit`, `context`, `audit` — the full Command-to-SDK operation map is wired (including the golden lifecycle path end-to-end). A shared `CommandFactory`/`CommandPipeline` makes each subcommand a few lines so adding the remaining queries is mechanical and behaviorally uniform.
- **Canonical exit-code projection (AC #7/#8):** `FoldersExitCodes` uses the sysexits-style table `{0,64,65,66,67,68,69,70,71,72,73,74,75,1}` (NOT the EventStore 0/1/2 scheme). `ErrorProjection` encodes the full `CanonicalErrorCategory`→exit map verbatim from `parity-contract.yaml outcome_mapping`; the SDK-only `range_unsatisfiable` (absent from the oracle) falls through to 1 as a documented drift signal.
- **Sourcing (AC #3/#4/#5/#6):** correlation resolved once per invocation via `CorrelationAndTaskId.ResolveCorrelationId` and echoed to stderr; task-id via `ResolveTaskId` (fail-closed → exit 64); idempotency via `--idempotency-key` or opt-in `--allow-auto-key` (ULID via `NewCorrelationId`, echoed to stderr) else exit 64 with no HTTP call; credential precedence `HEXALITH_TOKEN` → `~/.hexalith/credentials.json` (per-tenant, injectable path) → `--token`, else exit 65 with no HTTP call. Queries never register idempotency options, so passing one is a parse error → exit 64.
- **Metadata-only output (AC #9):** centralized `MetadataOnlyJson` drops content-bearing fields (`contentBytes`, `inlineContent`, `streamDescriptor`) at any depth, so even an authorized `context read-range` result never leaks file bytes in `human` or `json` mode. Errors render from typed `ProblemDetails` only (never the raw `Response` body). `AcceptedCommand` surfaces `idempotentReplay` truthfully.
- **File uploads (AC #10):** `file add`/`change` use `FoldersFileUploadExtensions.UploadFileAsync`; over-boundary content surfaces `FileUploadStreamingRequiredException` as a content-safe `input_limit_exceeded` (exit 69). `file remove` passes a metadata-only removal body via `--request` (the generated `FileMutationRequestFileOperationKind` enum has only `add`/`change`, so no removal shape is invented).
- **Hermetic tests (AC #11):** 102 CLI tests drive `rootCommand.Parse(args).InvokeAsync()` through an injected console + credential resolver (temp path, in-memory env) and either a fake `IClient` (NSubstitute) or the real generated client over a fake `HttpMessageHandler`. No server, Dapr, Keycloak, Redis, network, or submodule init.
- **Decision — request-body sourcing:** rather than re-encode the large, evolving spine schemas as per-field flags, mutating/body-carrying commands accept the exact spine body via `--request`. This keeps the adapter thin and faithful (zero invented fields) while the behavioral-parity dimensions this story owns are fully exercised independent of body content.
- **Decision — base-address env fallback** reads the real process `HEXALITH_FOLDERS_BASE_ADDRESS` (a URL, not a secret); the security-sensitive credential resolver uses an injected environment reader for hermeticity.

### File List

**New — `src/Hexalith.Folders.Cli/`:**
- `FoldersExitCodes.cs`, `OutputMode.cs`, `GlobalOptions.cs`, `GlobalOptionsBinding.cs`, `CliApplication.cs`
- `Errors/ErrorProjection.cs`, `Errors/CliUsageException.cs`
- `Infrastructure/ICliConsole.cs`, `Infrastructure/SystemCliConsole.cs`, `Infrastructure/MetadataOnlyJson.cs`
- `Rendering/ResultRenderer.cs`
- `Credentials/CredentialStore.cs`, `Credentials/CredentialResolver.cs`
- `Composition/BearerTokenHandler.cs`, `Composition/CliDependencies.cs`, `Composition/FoldersClientFactory.cs`
- `Commands/CommandPipeline.cs`, `Commands/CommandOptions.cs`, `Commands/CommandFactory.cs`
- `Commands/Provider/ProviderCommand.cs`, `Commands/Folder/FolderCommand.cs`, `Commands/Workspace/WorkspaceCommand.cs`, `Commands/File/FileCommand.cs`, `Commands/Commit/CommitCommand.cs`, `Commands/Context/ContextCommand.cs`, `Commands/Audit/AuditCommand.cs`

**Modified — `src/Hexalith.Folders.Cli/`:**
- `Program.cs` (replaced the 3-line scaffold with the composition root + `--version` short-circuit)

**New — `tests/Hexalith.Folders.Cli.Tests/`:**
- `TestSupport/TestCliConsole.cs`, `TestSupport/CapturingHttpHandler.cs`, `TestSupport/CliTestHarness.cs`, `TestSupport/TestData.cs`
- `ErrorProjectionTests.cs`, `CredentialResolverTests.cs`, `BehavioralParityTests.cs`, `ExitCodeWiringTests.cs`, `MetadataOnlyOutputTests.cs`, `FileUploadTransportTests.cs`, `OutputRenderingTests.cs`, `CommandSurfaceE2ETests.cs`, `CredentialSourcingE2ETests.cs`

**Modified — `tests/Hexalith.Folders.Cli.Tests/`:**
- `Hexalith.Folders.Cli.Tests.csproj` (added `NSubstitute` + `Newtonsoft.Json` package references)
- `CliSmokeTests.cs` retained unchanged as the adapter compile guard

**Modified — sprint tracking:**
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (story 5-2: ready-for-dev → in-progress → review)

### Change Log

| Date | Change |
| --- | --- |
| 2026-05-27 | Implemented `Hexalith.Folders.Cli` as a thin System.CommandLine 2.0.8 adapter over `Hexalith.Folders.Client`: seven command groups, Adapter Parity Contract sourcing (idempotency-key/correlation/task-id/credential), canonical category→exit-code projection, and metadata-only output. Added hermetic behavioral-parity tests. Full solution builds with 0 warnings/0 errors. Story status → review. |
| 2026-05-27 | **Senior Developer Review (AI), Jerome:** auto-fixed review findings. Made `json`-mode problem output camelCase to match the wire/SDK `ProblemDetails` shape (AC #9); corrected the File List and stale test-count claims (78 → actual 102); documented the `commit create` subcommand naming and the unrelated submodule-pointer drift. Re-verified: build 0/0, **102 tests pass**. Story status → done. |

## Senior Developer Review (AI)

**Reviewer:** Jerome · **Date:** 2026-05-27 · **Outcome:** Approve (auto-fix applied) · **Status:** done

### Summary

Adversarial review of `Hexalith.Folders.Cli` against all 11 acceptance criteria and the seven-group Command-to-SDK map. The implementation is a genuine thin adapter: the CLI csproj references only `Hexalith.Folders.Client` + `System.CommandLine` (no Server/EventStore/Dapr/aggregate/worker references — the only "EventStore" mentions are doc-comment rationale), every command reduces to one `IClient` call, and the canonical category→exit-code projection (`ErrorProjection`) was checked **row-by-row against `tests/fixtures/parity-contract.yaml` and matches all 43 oracle `outcome_mapping` categories exactly**. Build is 0 warnings / 0 errors; the hermetic suite passes 102/102. No CRITICAL or HIGH issues; no task marked `[x]` was found undone.

### Findings and resolution (all auto-fixed)

- **[MEDIUM] AC #9 — `json`-mode problem output was PascalCase.** `ResultRenderer.RenderProblem` serialized a hand-rolled `ProjectedProblem` record with no naming strategy, emitting `"Category"`/`"Code"`/`"CorrelationId"` while success results (generated types) emit camelCase. AC #9 requires `json` to emit the `ProblemDetails` shape, and Epic 5 hinges on cross-adapter wire parity. **Fixed:** added `CamelCaseNamingStrategy` to `MetadataOnlyJson` (preserves explicit `[JsonProperty]` names on generated types, so the forbidden-content filter is unaffected); updated `ExitCodeWiringTests` accordingly. Re-verified 102/102.
- **[MEDIUM] File List incomplete.** `CommandSurfaceE2ETests.cs` and `CredentialSourcingE2ETests.cs` existed in git but were absent from the File List. **Fixed:** added both.
- **[MEDIUM] Stale test-count claims.** Dev Agent Record/Completion Notes/Change Log claimed "78 passed"; the suite actually contains and passes **102**. **Fixed:** corrected throughout.
- **[MEDIUM] Submodule-pointer drift (transparency).** The working tree has `Hexalith.Commons` and `Hexalith.EventStore` checked out at commits newer than the superproject records — unrelated to this story and against its explicit "do not touch submodules" guardrail. Left the submodule working trees untouched (modifying them risks a passing build and is out of scope). **Action required of the committer:** do **not** stage these gitlink changes with the Story 5.2 commit.
- **[LOW] `commit` subcommand named `create`, not `commit`.** A `commit` child under the `commit` group collides in the System.CommandLine 2.0.8 token table; the dev correctly renamed it to `create` and added a regression test. Sound decision, but the §Command-to-SDK map lists it as `commit`. **Documented** here so the 5.4–5.6 parity runs invoke `commit create` → `CommitWorkspaceAsync`.

### AC verification (evidence)

All 11 ACs IMPLEMENTED. Highlights: pre-SDK 64 (missing idempotency/task-id/base-address) and 65 (missing credential) fail before the client factory is ever invoked (`BehavioralParityTests`, `ClientFactoryInvoked == false`); credential precedence env→file→flag proven end-to-end (`CredentialSourcingE2ETests`); correlation default ULID + `--correlation-id` echoed unchanged to the wire header (`BehavioralParityTests`); metadata-only output drops `contentBytes` even on an authorized `read-range` and never leaks the token or the raw RFC 9457 body (`MetadataOnlyOutputTests`); `file add`/`change` route through `UploadFileAsync` with over-boundary content surfacing the content-safe `input_limit_exceeded` (exit 69) (`FileUploadTransportTests`); golden lifecycle path executes end-to-end over a fake `IClient` (`CommandSurfaceE2ETests`).
