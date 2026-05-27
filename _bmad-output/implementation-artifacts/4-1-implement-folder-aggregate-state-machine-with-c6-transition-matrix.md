---
baseline_commit: 8b0fa019978f04b8c1c69b2dad75a4e97234cf5e
---

# Story 4.1: Implement Folder aggregate state machine with C6 transition matrix

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a domain developer,
I want the Folder aggregate to implement the C6 transition matrix,
so that every lifecycle command produces a defined transition or explicit rejection.

## Acceptance Criteria

1. Given the C6 matrix is documented, when folder workspace-lifecycle events are evaluated, then every listed positive transition returns the documented target state and every unlisted state/event pair rejects with `state_transition_invalid` while leaving state unchanged.
2. Given a valid transition is accepted, when aggregate state is applied, then the emitted event remains metadata-only and records only approved tenant/folder/workspace/task/operation/correlation/retry/failure evidence required by the Contract Spine.
3. Given the aggregate receives an invalid workspace-lifecycle event or command for the current state, when the result is produced, then no provider, filesystem, Dapr, worker, REST, SDK, CLI, MCP, UI, or read-model side effect occurs from aggregate code.
4. Given the C6 state catalog includes operator-disposition labels, when the implementation exposes or maps lifecycle state metadata, then `requested`, `preparing`, `ready`, `locked`, `changes_staged`, `dirty`, `committed`, `failed`, `inaccessible`, `unknown_provider_outcome`, and `reconciliation_required` stay aligned with architecture and OpenAPI vocabulary.
5. Given aggregate tests run, when they enumerate every C6 state and every C6 event vocabulary member, then each state/event pair has either an accepted transition assertion or an explicit `StateTransitionInvalid` rejection assertion.
6. Given existing folder creation, ACL, archive, repository binding, and branch/ref policy behavior exists, when Story 4.1 is implemented, then those behaviors and tests continue to pass without changing public Contract Spine shapes unless a documented drift is found and all generated artifacts/fixtures/tests are updated together.

## Tasks / Subtasks

- [x] Add the C6 workspace lifecycle vocabulary to the core aggregate model. (AC: 1, 4)
  - [x] Add a dedicated workspace lifecycle state enum under `src/Hexalith.Folders/Aggregates/Folder/` instead of overloading `FolderLifecycleState` (`Active`/`Archived`) or `FolderRepositoryBindingState`.
  - [x] Add a dedicated C6 event vocabulary enum or equivalent typed event discriminator for: `RepositoryBindingRequested`, `RepositoryBound`, `RepositoryBindingFailed`, `ProviderOutcomeUnknown`, `WorkspacePrepared`, `WorkspacePreparationFailed`, `WorkspaceLocked`, `AuthRevocationDetected`, `TenantRevoked`, `RepositoryDeletedAtProvider`, `ReconciliationRequested`, `FileMutated`, `WorkspaceLockReleased`, `LockLeaseExpired`, `CommitSucceeded`, `CommitFailed`, `OperatorDiscardRequested`, `OperatorRetrySucceeded`, `ProviderReadinessValidated`, `ReconciliationCompletedClean`, `ReconciliationCompletedDirty`, `ReconciliationEscalated`, and `OperatorMarkedFailed`.
  - [x] Add the operator-disposition vocabulary using contract-compatible names: `auto_recovering`, `available`, `degraded_but_serving`, `awaiting_human`, and `terminal_until_intervention`.
  - [x] Keep JSON/wire enum conversion name-based where any new enum can cross a serialized boundary.

- [x] Implement `FolderStateTransitions.cs` as the pure C6 matrix. (AC: 1, 3, 4)
  - [x] Create `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs`.
  - [x] Implement the matrix from `_bmad-output/planning-artifacts/architecture.md#Workspace State Transition Matrix (C6 - Enumerated)` and `docs/exit-criteria/c6-transition-matrix-mapping.md` as a total switch expression or equivalent total mapping.
  - [x] Return a typed result that carries accepted/rejected outcome, current state, attempted event, next state when accepted, canonical result code, and operator disposition; do not throw for invalid business transitions.
  - [x] Use `FolderResultCode.StateTransitionInvalid` for every unlisted pair and preserve the incoming state unchanged.
  - [x] Keep this helper deterministic and side-effect free: no I/O, provider calls, Dapr calls, logging, filesystem access, clock reads, or service resolution.

- [x] Integrate the C6 state into `FolderState` and event replay without breaking existing folder lifecycle semantics. (AC: 1, 2, 6)
  - [x] Extend `FolderState` with workspace lifecycle fields only where the aggregate needs durable replay state; do not rename or reinterpret existing `FolderLifecycleState.Active/Archived`.
  - [x] Update `FolderStateApply.Apply` so supported C6 metadata-only events advance the workspace lifecycle through `FolderStateTransitions`.
  - [x] Preserve stream identity validation, idempotency fingerprint dedupe, safe exception messages for foreign events, and loud failure on unknown event types.
  - [x] Do not store file contents, diffs, provider payloads, repository URLs, credential material, raw branch names, raw path text, raw exception text, or unauthorized resource identifiers in any C6 event/state field.

- [x] Add or adjust aggregate result mapping for lifecycle transition rejections. (AC: 1, 3, 5)
  - [x] Ensure invalid C6 command/event attempts surface `FolderResultCode.StateTransitionInvalid` or an existing canonical code only after validation/authorization ordering already required by the relevant caller path.
  - [x] Do not introduce exceptions for expected invalid transitions.
  - [x] Do not add runtime workspace command handlers beyond the matrix integration needed for this story; Stories 4.2 through 4.14 own prepare, lock, mutation, commit, cleanup, query, status, and audit behaviors.

- [x] Add exhaustive C6 aggregate tests. (AC: 1, 4, 5, 6)
  - [x] Add `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderStateTransitionsTests.cs`.
  - [x] Use xUnit v3 and Shouldly; keep tests hermetic and offline.
  - [x] Test every positive transition from the architecture table.
  - [x] Test every unlisted state/event pair rejects with `StateTransitionInvalid` and keeps state unchanged.
  - [x] Test operator-disposition mapping for all 11 states, including `ready` as `available` by default and `degraded_but_serving` only when projection-lag evidence is explicitly supplied by later read-model code.
  - [x] Test replay determinism for accepted transition event sequences and rejection non-mutation.

- [x] Add focused regression and drift guards. (AC: 4, 5, 6)
  - [x] Add a test that compares the implemented state catalog and event vocabulary to `docs/exit-criteria/c6-transition-matrix-mapping.md` or a local expected list derived from that document.
  - [x] Add regression tests proving existing folder creation, ACL, archive, repository-backed creation/binding, and branch/ref policy aggregate tests still pass.
  - [x] Run focused tests for `tests/Hexalith.Folders.Tests/Aggregates/Folder/*` and the existing contract test `WorkspaceLockContractGroupTests`.

## Dev Notes

### Source Context

- Epic 4 owns repository-backed workspace task lifecycle behavior: prepare workspaces, acquire locks, mutate files safely, query bounded context, commit, and expose deterministic failure/status/idempotency/redaction behavior. Story 4.1 is the matrix foundation that downstream Epic 4 stories consume. [Source: `_bmad-output/planning-artifacts/epics.md#Epic 4: Repository-Backed Workspace Task Lifecycle`]
- Story 4.1 requires the Folder aggregate to implement C6 so every lifecycle command has a defined transition or explicit rejection and aggregate tests cover every state/event pair. [Source: `_bmad-output/planning-artifacts/epics.md#Story 4.1: Implement Folder aggregate state machine with C6 transition matrix`]
- PRD FR45 requires canonical workspace/task states: `ready`, `locked`, `dirty`, `committed`, `failed`, and `inaccessible`; architecture C6 extends the implementation catalog with `requested`, `preparing`, `changes_staged`, `unknown_provider_outcome`, and `reconciliation_required`. [Source: `_bmad-output/planning-artifacts/prd.md#Functional Requirements`; `_bmad-output/planning-artifacts/architecture.md#Workspace State Transition Matrix (C6 - Enumerated)`]
- The Contract Spine already exposes `LifecycleState` with all 11 C6 values and `WorkspaceTransitionResult` including `state_transition_invalid`, `authorization_revoked`, `provider_outcome_unknown`, and `reconciliation_required`. Runtime names must match these serialized snake_case values. [Source: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#LifecycleState`; `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#WorkspaceTransitionResult`]
- `docs/exit-criteria/c6-transition-matrix-mapping.md` explicitly says architecture remains the source of truth and Story 4.1 must translate it 1:1 into `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs`. [Source: `docs/exit-criteria/c6-transition-matrix-mapping.md#Decision`]

### C6 State Catalog

| State | Default operator disposition | Notes |
|---|---|---|
| `requested` | `auto_recovering` | Repository binding submitted; provider activity not yet started. |
| `preparing` | `auto_recovering` | Workspace materialization in flight. |
| `ready` | `available` | Workspace usable, no lock, no uncommitted changes; later read-model projection lag can render as `degraded_but_serving`. |
| `locked` | `degraded_but_serving` | Lock held by a task; no mutations yet. |
| `changes_staged` | `degraded_but_serving` | Lock held; one or more file mutations applied; commit pending. |
| `dirty` | `awaiting_human` | Uncommitted changes outside active task or orphaned lock with staged mutations. |
| `committed` | `auto_recovering` | Commit succeeded; lock release/projection update in flight. |
| `failed` | `terminal_until_intervention` | Known categorized failure; human or reconciler intervention required. |
| `inaccessible` | `terminal_until_intervention` | Provider unreachable, repository deleted, or credentials revoked. |
| `unknown_provider_outcome` | `awaiting_human` | Provider call did not confirm outcome. |
| `reconciliation_required` | `awaiting_human` | Reconciler or operator must inspect upstream truth. |

### C6 Positive Transition Table

| From | Event | To |
|---|---|---|
| `(none)` | `RepositoryBindingRequested` | `requested` |
| `requested` | `RepositoryBound` | `preparing` |
| `requested` | `RepositoryBindingFailed` | `failed` |
| `requested` | `ProviderOutcomeUnknown` | `unknown_provider_outcome` |
| `preparing` | `WorkspacePrepared` | `ready` |
| `preparing` | `WorkspacePreparationFailed` | `failed` |
| `preparing` | `ProviderOutcomeUnknown` | `unknown_provider_outcome` |
| `ready` | `WorkspaceLocked` | `locked` |
| `ready` | `AuthRevocationDetected` | `inaccessible` |
| `ready` | `TenantRevoked` | `inaccessible` |
| `ready` | `RepositoryDeletedAtProvider` | `inaccessible` |
| `ready` | `ReconciliationRequested` | `reconciliation_required` |
| `locked` | `FileMutated` | `changes_staged` |
| `locked` | `WorkspaceLockReleased` | `ready` |
| `locked` | `LockLeaseExpired` | `dirty` |
| `locked` | `AuthRevocationDetected` | `inaccessible` |
| `changes_staged` | `FileMutated` | `changes_staged` |
| `changes_staged` | `CommitSucceeded` | `committed` |
| `changes_staged` | `CommitFailed` | `failed` |
| `changes_staged` | `ProviderOutcomeUnknown` | `unknown_provider_outcome` |
| `changes_staged` | `LockLeaseExpired` | `dirty` |
| `committed` | `WorkspaceLockReleased` | `ready` |
| `dirty` | `ReconciliationRequested` | `reconciliation_required` |
| `dirty` | `OperatorDiscardRequested` | `failed` |
| `failed` | `ReconciliationRequested` | `reconciliation_required` |
| `failed` | `OperatorRetrySucceeded` | `ready` |
| `inaccessible` | `ProviderReadinessValidated` | `ready` |
| `unknown_provider_outcome` | `ReconciliationCompletedClean` | `ready` |
| `unknown_provider_outcome` | `ReconciliationCompletedDirty` | `committed` or `failed` depending on confirmed upstream outcome |
| `unknown_provider_outcome` | `ReconciliationEscalated` | `reconciliation_required` |
| `reconciliation_required` | `ReconciliationCompletedClean` | `ready` |
| `reconciliation_required` | `ReconciliationCompletedDirty` | `committed` |
| `reconciliation_required` | `OperatorMarkedFailed` | `failed` |

The ambiguous `ReconciliationCompletedDirty` row from `unknown_provider_outcome` is intentionally documented in architecture as two outcomes: commit confirmed upstream goes to `committed`; commit refused upstream goes to `failed`. Model this with an outcome discriminator, distinct event values, or validated transition metadata. Do not guess from provider text.

### Existing Implementation State

- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs` currently handles folder creation, ACL grant/revoke, archive, repository-backed folder creation, existing repository binding, and branch/ref policy configuration. It checks validation, idempotency, folder existence, and active mutation guards before emitting metadata-only events.
- `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs` currently tracks folder creation metadata, `FolderLifecycleState` (`Active`/`Archived`), `FolderRepositoryBindingState`, repository binding metadata, branch/ref policy metadata, archive evidence, ACL overrides, access sequence, and idempotency fingerprints. It has no C6 workspace lifecycle state field yet.
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs` currently validates stream identity before applying events, dedupes identical idempotency replays, applies known folder/repository/branch events, records idempotency fingerprints, and throws with a safe `StateTransitionInvalid` message for unknown event types.
- `FolderRepositoryBindingState` currently has `Unbound`, `BindingRequested`, `Bound`, `Failed`, `UnknownProviderOutcome`, and `ReconciliationRequired`; these are repository binding status values, not the full C6 workspace lifecycle. Do not make it carry `ready`, `locked`, `dirty`, `committed`, or workspace lock semantics.
- `FolderResultCode` already includes `StateTransitionInvalid`, `UnknownProviderOutcome`, `ReconciliationRequired`, provider failure categories, authorization categories, projection categories, and idempotency categories. Prefer existing codes unless the Contract Spine already requires a new one.
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs` does not exist at story creation time. Architecture and exit-criteria docs name it as the target artifact.

### Previous Story Intelligence

- There is no previous Story 4.x file. Epic 4 is starting from `4.1`.
- Story 3.9 finished provider support evidence and reinforced these patterns: authorization-before-observation, metadata-only public evidence, safe denial without provider/resource enumeration, contract-first behavior, and hermetic tests with no live providers or network calls. [Source: `_bmad-output/implementation-artifacts/3-9-inspect-tenant-and-per-provider-readiness-evidence.md`]
- Recent commits show Epic 3 work established repository binding, branch/ref policy, and provider readiness evidence before workspace lifecycle begins. Story 4.1 should consume those semantics but must not reopen provider readiness or binding scope.

### Architecture Guardrails

- Use .NET 10, nullable-aware C#, file-scoped namespaces, one primary public type per file, PascalCase public members, camelCase locals/parameters, and central package versions. [Source: `_bmad-output/project-context.md#Technology Stack & Versions`; `_bmad-output/project-context.md#Language-Specific Rules`]
- Aggregate handlers must remain deterministic and side-effect free. Pass timestamps in from callers; never perform I/O, provider calls, Dapr calls, logging, filesystem work, clock reads, or service resolution inside aggregate transitions. [Source: `_bmad-output/project-context.md#Language-Specific Rules`; `Hexalith.EventStore/_bmad-output/project-context.md#Framework-Specific Rules`]
- Domain rejections are expected business outcomes. Return typed result codes or rejection results; do not throw for invalid transitions, idempotency replay, duplicate operations, tenant denial, or missing ACL entries. [Source: `_bmad-output/project-context.md#Language-Specific Rules`; `Hexalith.EventStore/_bmad-output/project-context.md#Critical Don't-Miss Rules`]
- Every C6 unlisted `(state, event)` pair rejects with `state_transition_invalid`, leaves state unchanged, and must remain inspectable through idempotency behavior. [Source: `_bmad-output/planning-artifacts/architecture.md#Workspace State Transition Matrix (C6 - Enumerated)`; `docs/exit-criteria/c6-transition-matrix-mapping.md#Decision`]
- Metadata-only is non-negotiable across events, projections, logs, traces, metrics, audit records, Problem Details, docs examples, and test failures. [Source: `_bmad-output/project-context.md#Critical Don't-Miss Rules`]
- Do not change generated client output manually. Contract changes start in `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` and flow through generated artifacts, parity rows, previous-spine, docs, and tests together. [Source: `_bmad-output/project-context.md#Framework-Specific Rules`; `_bmad-output/project-context.md#Critical Don't-Miss Rules`]
- Preserve root-level submodule policy. Do not initialize nested submodules or use recursive submodule commands. [Source: `AGENTS.md`; `_bmad-output/project-context.md#Development Workflow Rules`]

### Files To Touch

- `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs` (new)
- `src/Hexalith.Folders/Aggregates/Folder/*WorkspaceLifecycle*.cs` or equivalent focused one-type-per-file C6 state/event/result models (new)
- `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderResult.cs` and `FolderResultCode.cs` only if existing result shape cannot carry transition outcome cleanly
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderStateTransitionsTests.cs` (new)
- Existing focused aggregate tests under `tests/Hexalith.Folders.Tests/Aggregates/Folder/` only when regression expectations need explicit preservation
- `docs/exit-criteria/c6-transition-matrix-mapping.md` only if implementation discovers a legitimate architecture vocabulary edge case; update architecture and tests in the same change if this happens
- OpenAPI, parity, previous-spine, generated SDK, and contract docs only if public vocabulary changes; avoid such changes unless unavoidable

### Do Not Touch

- Do not implement `PrepareWorkspace`, `LockWorkspace`, `ReleaseWorkspaceLock`, file mutation, context query, commit, cleanup, retry eligibility, transition evidence endpoints, CLI, MCP, SDK convenience helpers, workers, read models, or UI pages in this story.
- Do not add provider calls, Git operations, filesystem mutation, Dapr service invocation, EventStore persistence logic, live readiness validation, or reconciliation worker behavior to the aggregate transition helper.
- Do not overload repository binding state to represent workspace lifecycle.
- Do not expose file contents, diffs, raw paths, provider payloads, repository URLs, credential references, tokens, emails, display names, raw claim bags, or unauthorized resource existence in events, state, test output, logs, or Problem Details.
- Do not hand-edit generated files under `src/Hexalith.Folders.Client/Generated/*`.
- Do not add package versions directly to `.csproj` files.
- Do not add network-dependent, live GitHub, live Forgejo, Aspire, Dapr sidecar, Docker, Keycloak, Redis, or nested-submodule requirements to the aggregate tests.

### Testing

- Use xUnit v3 and Shouldly. Keep tests hermetic and offline. [Source: `_bmad-output/project-context.md#Testing Rules`]
- Recommended focused validation:
  - `dotnet test tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --filter FullyQualifiedName~Aggregates.Folder`
  - `dotnet test tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --filter FullyQualifiedName~WorkspaceLockContractGroupTests`
- Add a matrix coverage test that enumerates the state catalog and event vocabulary from implementation constants and fails if any pair lacks an explicit accepted/rejected outcome.
- Add metadata-safety assertions for accepted transition events and invalid transition outputs so unsafe values cannot leak through C6 evidence.
- If public Contract Spine vocabulary changes, also run the contract/parity/generated-client gates required by the existing project context.

### Regression Traps

- The architecture table has one ambiguous row: `unknown_provider_outcome` + `ReconciliationCompletedDirty` can lead to `committed` or `failed`. Implement that ambiguity explicitly with safe typed outcome evidence; do not infer from raw provider messages.
- Existing `FolderLifecycleState` means folder active/archived, not workspace C6 lifecycle. Renaming or repurposing it can break Epic 2 lifecycle behavior.
- Existing `FolderRepositoryBindingState` means repository binding/provisioning status, not workspace readiness/lock/dirty/commit status.
- Invalid transition handling must not throw and must not create events unless the accepted design has an explicit metadata-only rejection event and idempotency semantics for it.
- `FolderStateApply` currently throws on unknown event types to protect replay correctness. Do not convert unknown event types to silent no-ops.
- Aggregate tests must not rely on current wall-clock time. Pass deterministic timestamps as existing tests do.
- `ready` operator disposition is `available` by default; only read-model/projection lag evidence can make it `degraded_but_serving`.
- Any C6 vocabulary drift must update architecture, `docs/exit-criteria/c6-transition-matrix-mapping.md`, OpenAPI schema, parity fixtures, tests, and generated artifacts together.

### Project Structure Notes

- This story aligns with the architecture source tree mapping for FR24-FR31: `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs` owns the C6 matrix and later workspace/lock lifecycle code consumes it. [Source: `_bmad-output/planning-artifacts/architecture.md#Requirements-to-Structure-Mapping`]
- Keep all new aggregate C6 types in `src/Hexalith.Folders/Aggregates/Folder/` with one primary public type per file.
- Keep tests in `tests/Hexalith.Folders.Tests/Aggregates/Folder/`, mirroring the production concept area.
- At story creation time the worktree already had unrelated modifications in `Hexalith.Builds` and `_bmad-output/story-automator/orchestration-3-20260526-203745.md`; do not revert or mix those changes into this story implementation.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 4.1: Implement Folder aggregate state machine with C6 transition matrix`
- `_bmad-output/planning-artifacts/epics.md#Epic 4: Repository-Backed Workspace Task Lifecycle`
- `_bmad-output/planning-artifacts/prd.md#Functional Requirements`
- `_bmad-output/planning-artifacts/architecture.md#Workspace State Transition Matrix (C6 - Enumerated)`
- `_bmad-output/planning-artifacts/architecture.md#Requirements-to-Structure-Mapping`
- `docs/exit-criteria/c6-transition-matrix-mapping.md`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#LifecycleState`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#WorkspaceTransitionResult`
- `docs/contract/idempotency-and-parity-rules.md`
- `_bmad-output/project-context.md#Critical Don't-Miss Rules`
- `Hexalith.EventStore/_bmad-output/project-context.md#Framework-Specific Rules`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderResult.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderResultCode.cs`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/WorkspaceLockContractGroupTests.cs`
- `_bmad-output/implementation-artifacts/3-9-inspect-tenant-and-per-provider-readiness-evidence.md`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-27: Resolved `bmad-create-story` customization; no activation prepend/append steps. Loaded `_bmad/bmm/config.yaml`, sprint status, root/sibling project-context facts, planning artifacts, Epic 4, architecture C6 matrix, exit-criteria C6 mapping, OpenAPI lifecycle schemas, current aggregate code, current tests, and recent git history.
- 2026-05-27: Discovery loaded planning artifacts from `_bmad-output/planning-artifacts/`: PRD, architecture, epics, and UX specification. No sharded planning docs were present.
- 2026-05-27: Previous story intelligence: no prior 4.x story exists. Story 3.9 and recent commits establish provider readiness evidence and branch/ref policy foundations, with authorization-before-observation and metadata-only evidence patterns to preserve.
- 2026-05-27: Latest external research was not added because Story 4.1 introduces no new external library/API version and project-local `global.json`, `Directory.Packages.props`, project context, architecture, and OpenAPI spine are the authoritative implementation sources.
- 2026-05-27: Dev recovery implemented C6 aggregate vocabulary, pure transition matrix, workspace replay state, metadata-only lifecycle event, and hermetic matrix tests. Initial child validation hit sandbox MSBuild/VSTest pipe restrictions; focused parent validation passed.
- 2026-05-27: Broad solution build in child sandbox was not accepted as a story gate because `Hexalith.Folders.AppHost` attempted blocked NuGet access to `api.nuget.org`. Server/client broad assembly runs also exposed unrelated environment/generated-client drift outside Story 4.1 scope.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Story 4.1 file created at `_bmad-output/implementation-artifacts/4-1-implement-folder-aggregate-state-machine-with-c6-transition-matrix.md`.
- Sprint status updated to mark `epic-4` as `in-progress` and `4-1-implement-folder-aggregate-state-machine-with-c6-transition-matrix` as `ready-for-dev`.
- Added dedicated C6 workspace lifecycle state, lifecycle event, dirty-resolution, operator-disposition, transition-result, and metadata-only recorded event types.
- Implemented `FolderStateTransitions` as the deterministic C6 transition matrix with explicit `StateTransitionInvalid` rejection behavior and typed dirty reconciliation resolution.
- Integrated workspace lifecycle replay into `FolderState`/`FolderStateApply` while preserving folder lifecycle and repository binding semantics.
- Updated branch/ref and read-model tests to seed the repository-backed requested transition before repository-bound replay.
- Updated scaffold/exit-criteria checks now that Story 4.1 intentionally introduces `FolderStateTransitions.cs`.
- Focused validation passed from parent shell:
  - `dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore --tlp:off -v:minimal`
  - `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - `dotnet build tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - `tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -parallel none -noLogo -namespace Hexalith.Folders.Tests.Aggregates.Folder`
  - `tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -parallel none -noLogo -class Hexalith.Folders.Tests.Queries.BranchRefPolicyReadModelTests`
  - `tests/Hexalith.Folders.Testing.Tests/bin/Debug/net10.0/Hexalith.Folders.Testing.Tests -parallel none -noLogo -class Hexalith.Folders.Testing.Tests.ExitCriteriaDecisionArtifactTests -class Hexalith.Folders.Testing.Tests.ScaffoldContractTests`

### File List

- `_bmad-output/implementation-artifacts/4-1-implement-folder-aggregate-state-machine-with-c6-transition-matrix.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Folders/Aggregates/Folder/FolderOperatorDisposition.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderWorkspaceDirtyResolution.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderWorkspaceLifecycleEvent.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderWorkspaceLifecycleEventRecorded.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderWorkspaceLifecycleState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderWorkspaceTransitionResult.cs`
- `tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs`
- `tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs`
- `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderBranchRefPolicyAggregateTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderBranchRefPolicyConfigurationServiceTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderRepositoryBackedAggregateTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderStateTransitionsTests.cs`
- `tests/Hexalith.Folders.Tests/Queries/BranchRefPolicyReadModelTests.cs`

## Change Log

- 2026-05-27: Senior developer review completed. Added C6 vocabulary drift guards and repository outcome replay assertions for workspace lifecycle state/disposition alignment.

## Senior Developer Review (AI)

Reviewer: Codex on 2026-05-27

Outcome: Approved after automatic fixes.

Findings fixed:

- [Medium] `FolderStateTransitions.StateCatalog` and `EventVocabulary` were asserted only against hard-coded expected lists, so a future enum addition could bypass the exposed catalog drift guard. Added enum-to-catalog assertions in `FolderStateTransitionsTests`.
- [Medium] Repository-bound, repository-failed, and provider-outcome replay tests only asserted legacy repository binding state. Added workspace lifecycle state, operator disposition, and attempted-event assertions in `FolderRepositoryBackedAggregateTests` so Story 4.1 replay integration is covered directly.

Validation:

- `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
- `tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -parallel none -noLogo -namespace Hexalith.Folders.Tests.Aggregates.Folder`
- `tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -parallel none -noLogo -class Hexalith.Folders.Tests.Queries.BranchRefPolicyReadModelTests`
- `dotnet build tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
- `tests/Hexalith.Folders.Testing.Tests/bin/Debug/net10.0/Hexalith.Folders.Testing.Tests -parallel none -noLogo -class Hexalith.Folders.Testing.Tests.ExitCriteriaDecisionArtifactTests -class Hexalith.Folders.Testing.Tests.ScaffoldContractTests`
- `dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
- `tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -parallel none -noLogo -class Hexalith.Folders.Contracts.Tests.OpenApi.WorkspaceLockContractGroupTests`
- `dotnet build tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
- `tests/Hexalith.Folders.Workers.Tests/bin/Debug/net10.0/Hexalith.Folders.Workers.Tests -parallel none -noLogo -class Hexalith.Folders.Workers.Tests.RepositoryProvisioningProcessManagerTests`

Environmental/out-of-scope:

- Full `Hexalith.Folders.Workers.Tests` run hit sandbox socket binding permission failures in `WorkersTenantEventTests`, matching the requested environmental exclusion.
