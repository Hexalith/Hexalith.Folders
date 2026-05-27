---
baseline_commit: c9e39ca584b8a1893b332bb9c25261e1f8a7225f
---

# Story 4.2: Prepare workspace from a ready repository-backed folder

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer or AI agent,
I want to prepare a workspace from a ready repository-backed folder,
so that file work starts from a known provider and branch/ref state.

## Acceptance Criteria

1. Given provider readiness, repository binding, branch/ref policy, workspace policy, task context, authorization, and idempotency evidence are valid, when `PrepareWorkspace` is accepted, then the aggregate/runtime records a metadata-only workspace preparation intent and the lifecycle can advance from `preparing` to `ready` through `WorkspacePrepared`.
2. Given `PrepareWorkspace` is replayed with the same idempotency key and equivalent payload (`branch_ref_policy_ref`, `folder_id`, `repository_binding_id`, `task_id`, `workspace_id`, `workspace_policy_ref`), when the command is observed again, then the same logical result is returned without duplicate workspace preparation events or provider work.
3. Given a non-equivalent payload reuses an idempotency key, or the folder is missing, archived, not repository-bound, not in a valid C6 state, has mismatched repository binding or branch/ref policy evidence, or is denied by tenant/folder authorization, when `PrepareWorkspace` is attempted, then the command rejects with the canonical result category and emits no provider, filesystem, Dapr, worker, read-model, or audit side effects from aggregate code.
4. Given provider readiness or workspace preparation outcome is unknown, when `PrepareWorkspace` cannot safely confirm preparation, then the lifecycle enters `unknown_provider_outcome` or `reconciliation_required` using metadata-only evidence; the implementation must not silently retry, repair, or infer outcome from raw provider text.
5. Given the public Contract Spine already declares `POST /api/v1/folders/{folderId}/workspaces/{workspaceId}/preparation`, when server/client/contract behavior is touched, then REST, generated client idempotency helpers, problem categories, and focused contract tests remain aligned without manually editing generated files.
6. Given existing folder creation, repository binding, branch/ref policy, provider readiness, C6 matrix, lifecycle status, and worker provisioning behavior exists, when Story 4.2 is implemented, then those behaviors and focused tests continue to pass.

## Tasks / Subtasks

- [x] Add aggregate command and event vocabulary for workspace preparation. (AC: 1, 2, 3, 4)
  - [x] Add a `PrepareWorkspace` folder command or equivalent domain command under `src/Hexalith.Folders/Aggregates/Folder/` with request schema version, tenant/folder/workspace/task/correlation/idempotency evidence, repository binding reference, branch/ref policy reference, and workspace policy reference.
  - [x] Add metadata-only events for accepted preparation intent and preparation outcome where needed; reuse `FolderWorkspaceLifecycleEventRecorded` for `WorkspacePrepared`, `WorkspacePreparationFailed`, and `ProviderOutcomeUnknown` when it cleanly carries the C6 state transition.
  - [x] Do not store repository URLs, branch names, paths, provider payloads, filesystem paths, credential material, file contents, diffs, raw exception text, user emails, or display names.
  - [x] Keep enum and result serialization name-based if any new type can cross a serialized boundary.

- [x] Implement deterministic aggregate handling for `PrepareWorkspace`. (AC: 1, 2, 3, 4, 6)
  - [x] Validate command shape with existing `FolderCommandValidator` patterns; add canonical idempotency fingerprinting using the Contract Spine equivalence fields in lexicographic order.
  - [x] Require created, active, repository-bound state with matching `RepositoryBindingId` and configured branch/ref policy evidence before accepting preparation.
  - [x] Require C6 `WorkspaceLifecycleState` to be `Preparing` before `WorkspacePrepared` can move to `Ready`; reject invalid state/event pairs with `FolderResultCode.StateTransitionInvalid`.
  - [x] Preserve existing ordering: validation and authorization evidence before resource observation; idempotent replay before duplicate side effects; expected business rejections return typed results instead of throwing.
  - [x] Keep aggregate code pure: no provider calls, Git calls, filesystem mutation, Dapr service invocation, logging, service resolution, clock reads, or read-model writes.

- [x] Wire the accepted preparation request to the runtime boundary without doing provider work in the aggregate. (AC: 1, 3, 4, 5)
  - [x] Extend the domain service/gate path that dispatches folder commands so `PrepareWorkspace` can be accepted from the REST route and persisted through the existing EventStore flow.
  - [x] Ensure any worker/process-manager handoff is metadata-only and task-scoped; actual clone/materialization behavior may be represented as a pending worker affordance if the current worker contracts do not yet support it.
  - [x] Map unknown provider outcome and preparation failure to canonical result/problem categories already declared by the Contract Spine.
  - [x] Do not implement workspace lock, file mutation, context query, commit, cleanup, SDK convenience helpers, CLI tools, MCP tools, UI pages, or generated-client regeneration in this story unless contract drift is unavoidable and fully updated.

- [x] Add or update REST/server tests for `PrepareWorkspace`. (AC: 1, 2, 3, 4, 5)
  - [x] Add focused endpoint tests for accepted request shape, required `Idempotency-Key`, required `X-Hexalith-Task-Id`, safe authorization denial, malformed payload, idempotency conflict, invalid lifecycle state, provider/readiness unavailable, and unknown outcome/reconciliation mapping.
  - [x] Verify route `folderId` and `workspaceId` are authoritative and client-controlled tenant/body values cannot override authenticated/envelope tenant evidence.
  - [x] Verify no response or problem detail leaks provider binding existence, repository identity, branch names, paths, credential references, provider payloads, or raw exception text before authorization.

- [x] Add aggregate and worker/process-manager tests for workspace preparation. (AC: 1, 2, 3, 4, 6)
  - [x] Add aggregate tests for accepted prepare intent, equivalent replay, conflicting replay, missing/archived/unbound folder, mismatched repository binding, mismatched branch/ref policy, invalid C6 state, unknown provider outcome, and preparation failure.
  - [x] Add replay tests proving `WorkspacePrepared` advances `preparing -> ready` and invalid preparation events leave state unchanged.
  - [x] Add focused worker/process-manager tests only for metadata handoff and outcome event recording; do not require live GitHub, Forgejo, Aspire, Dapr sidecars, Docker, Redis, Keycloak, network access, or nested submodules.

- [x] Preserve contract and regression gates. (AC: 5, 6)
  - [x] Run focused aggregate tests under `tests/Hexalith.Folders.Tests/Aggregates/Folder`.
  - [x] Run `WorkspaceLockContractGroupTests` because `PrepareWorkspace` belongs to the workspace/lock contract group.
  - [x] Run focused server tests for the new preparation endpoint.
  - [x] Run focused worker/process-manager tests if worker handoff code is changed.
  - [x] Run `git diff --check`.

## Dev Notes

### Source Context

- Epic 4 owns the repository-backed workspace task lifecycle: prepare workspaces, acquire locks, mutate files safely, query bounded context, commit changes, and expose deterministic failure/status/idempotency/redaction behavior. [Source: `_bmad-output/planning-artifacts/epics.md#Epic 4: Repository-Backed Workspace Task Lifecycle`]
- Story 4.2 specifically prepares a workspace from a ready repository-backed folder so file work starts from known provider and branch/ref state. [Source: `_bmad-output/planning-artifacts/epics.md#Story 4.2: Prepare workspace from a ready repository-backed folder`]
- PRD FR24 requires authorized actors to prepare a workspace only when provider readiness, repository binding, branch/ref policy, and task context are valid. [Source: `_bmad-output/planning-artifacts/prd.md#Functional Requirements`]
- Architecture defines the canonical lifecycle as provider-readiness -> folder/repository binding -> workspace prepare -> task lock -> file mutation -> commit -> query -> audit, with tenant authorization evaluated before workspace/provider access. [Source: `_bmad-output/planning-artifacts/architecture.md#Introduction`]
- The C6 matrix states `preparing -> ready` on `WorkspacePrepared`, `preparing -> failed` on `WorkspacePreparationFailed`, and `preparing -> unknown_provider_outcome` on `ProviderOutcomeUnknown`; unlisted pairs reject with `state_transition_invalid`. [Source: `_bmad-output/planning-artifacts/architecture.md#Workspace State Transition Matrix (C6 - Enumerated)`]
- The Contract Spine already declares `PrepareWorkspace` at `POST /api/v1/folders/{folderId}/workspaces/{workspaceId}/preparation` and requires idempotency equivalence fields `branch_ref_policy_ref`, `folder_id`, `repository_binding_id`, `task_id`, `workspace_id`, and `workspace_policy_ref`. [Source: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#PrepareWorkspace`]

### Previous Story Intelligence

- Story 4.1 added C6 lifecycle primitives in `src/Hexalith.Folders/Aggregates/Folder/`: `FolderWorkspaceLifecycleState`, `FolderWorkspaceLifecycleEvent`, `FolderWorkspaceDirtyResolution`, `FolderOperatorDisposition`, `FolderWorkspaceTransitionResult`, `FolderWorkspaceLifecycleEventRecorded`, and `FolderStateTransitions`.
- Story 4.1 integrated workspace lifecycle replay into `FolderState`/`FolderStateApply`; accepted repository binding moves workspace lifecycle into `preparing`.
- Review for Story 4.1 added tests that assert repository-bound, repository-failed, and provider-outcome replay also updates workspace lifecycle state, operator disposition, and attempted event.
- Known environmental failures: broad solution/AppHost builds can try blocked NuGet network access; full server/worker test assemblies can fail in sandbox on Kestrel socket binding. Prefer focused project builds and in-process xUnit classes unless the story explicitly needs live hosts.

### Existing Implementation State

- `FolderAggregate` currently handles folder creation, ACL mutation, archive, repository-backed creation, existing repository binding, and branch/ref policy configuration. It does not yet expose `PrepareWorkspace`.
- `FolderState` now carries workspace lifecycle fields: lifecycle state, operator disposition, workspace id, last lifecycle event, operation id, correlation id, task id, and updated timestamp.
- `FolderStateApply` applies `RepositoryBindingRequested`, `RepositoryBound`, `RepositoryBindingFailed`, `ProviderOutcomeUnknown`, and `FolderWorkspaceLifecycleEventRecorded` through `FolderStateTransitions`.
- `ProviderOperationCatalog.WorkspacePreparation` already exists as provider readiness capability vocabulary.
- The generated client already contains `PrepareWorkspaceRequest` and `ComputeIdempotencyHash`; do not hand-edit `src/Hexalith.Folders.Client/Generated/*`.

### Architecture Guardrails

- Use .NET 10, nullable-aware C#, file-scoped namespaces, one primary public type per file, PascalCase public members, camelCase locals/parameters, central package versions, xUnit v3, and Shouldly. [Source: `_bmad-output/project-context.md#Technology Stack & Versions`; `_bmad-output/project-context.md#Testing Rules`]
- Aggregate handlers must stay deterministic and side-effect free. Pass timestamps from callers; never perform I/O, provider calls, Dapr calls, logging, filesystem work, clock reads, or service resolution inside aggregate transitions. [Source: `_bmad-output/project-context.md#Language-Specific Rules`]
- Domain rejections are expected business outcomes. Return typed result codes or rejection results; do not throw for invalid lifecycle state, idempotency replay, duplicate operations, tenant denial, or missing ACL entries. [Source: `_bmad-output/project-context.md#Language-Specific Rules`]
- Metadata-only is mandatory across events, projections, logs, traces, metrics, audit records, problem details, docs examples, and tests. [Source: `_bmad-output/project-context.md#Critical Don't-Miss Rules`]
- Do not initialize nested submodules or use recursive submodule commands. [Source: `AGENTS.md`]

### Files To Touch

- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs`
- New focused aggregate command/event types under `src/Hexalith.Folders/Aggregates/Folder/`
- Server/domain endpoint files under `src/Hexalith.Folders.Server/` only as needed to route `PrepareWorkspace`
- Worker/process-manager files under `src/Hexalith.Folders.Workers/` only as needed for metadata handoff
- Focused tests under `tests/Hexalith.Folders.Tests/Aggregates/Folder/`, `tests/Hexalith.Folders.Server.Tests/`, `tests/Hexalith.Folders.Workers.Tests/`, and `tests/Hexalith.Folders.Contracts.Tests/OpenApi/`

### Do Not Touch

- Do not implement acquire lock, release lock, file add/change/remove, context query, commit, cleanup, retry eligibility UI, diagnostics pages, SDK convenience helpers, CLI, MCP, or generated client regeneration unless a compile-time contract break proves it is unavoidable.
- Do not add package versions directly to `.csproj` files.
- Do not use live GitHub, Forgejo, Aspire, Dapr sidecars, Docker, Keycloak, Redis, network-dependent tests, or nested-submodule initialization.
- Do not expose or persist file contents, diffs, raw paths, provider payloads, repository URLs, credential material, tokens, emails, display names, raw claim bags, or unauthorized resource existence.

### Testing

- Recommended parent-shell validation:
  - `dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore --tlp:off -v:minimal`
  - `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - `tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -parallel none -noLogo -namespace Hexalith.Folders.Tests.Aggregates.Folder`
  - `tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -parallel none -noLogo -class Hexalith.Folders.Contracts.Tests.OpenApi.WorkspaceLockContractGroupTests`
  - focused server/worker test classes changed by this story
  - `git diff --check`

### Regression Traps

- `RepositoryBound` currently moves workspace lifecycle to `preparing`; Story 4.2 must not skip directly from repository binding to lockable workspace without `WorkspacePrepared`.
- `ready` means no lock and no uncommitted changes; do not overload repository binding `Bound` as workspace-ready.
- `WorkspacePrepared` must be metadata-only; it may identify tenant/folder/workspace/task/operation/correlation and safe policy references, not provider clone details or filesystem paths.
- Unknown provider outcome must enter reconciliation/unknown state and must not trigger silent retry or guessed success.
- Existing `FolderStateApply` fails loud on unknown event types; do not convert unknown events to silent no-ops.
- Authorization and safe-denial ordering must prevent hidden folder/repository/provider/workspace existence leaks.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 4.2: Prepare workspace from a ready repository-backed folder`
- `_bmad-output/planning-artifacts/epics.md#Epic 4: Repository-Backed Workspace Task Lifecycle`
- `_bmad-output/planning-artifacts/prd.md#Functional Requirements`
- `_bmad-output/planning-artifacts/architecture.md#Workspace State Transition Matrix (C6 - Enumerated)`
- `_bmad-output/planning-artifacts/architecture.md#Requirements-to-Structure-Mapping`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#PrepareWorkspace`
- `src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs#PrepareWorkspaceRequest`
- `_bmad-output/implementation-artifacts/4-1-implement-folder-aggregate-state-machine-with-c6-transition-matrix.md`
- `docs/exit-criteria/c6-transition-matrix-mapping.md`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-27: Two automated create-story tmux attempts stalled before writing the story artifact; recovered by creating this story directly from planning artifacts, OpenAPI contract, Story 4.1 outputs, and local source/test structure.
- 2026-05-27: Latest external research was not added because this story uses existing project-local .NET/OpenAPI/EventStore patterns and introduces no new external library/API version.
- 2026-05-27: Implemented `PrepareWorkspace` as a metadata-only aggregate command and `WorkspacePreparationRequested` intent event; outcome transitions continue through `FolderWorkspaceLifecycleEventRecorded`.
- 2026-05-27: Wired `PrepareWorkspace` through REST, `/process` domain processor, authorization/readiness service, and provider readiness capability vocabulary without generated-client edits or provider/filesystem work.
- 2026-05-27: Full `ServerEndpointRegistrationTests` class hit the known sandbox Kestrel socket bind failure in `ProjectRouteShouldReturn501NotImplemented`; reran the non-Kestrel route registration method successfully.
- 2026-05-27: Senior review fixed readiness unknown/reconciliation handling so workspace lifecycle records metadata-only `ProviderOutcomeUnknown` evidence instead of returning without a C6 transition.
- 2026-05-27: Senior review fixed REST problem detail retry guidance for `unknown_provider_outcome` and `reconciliation_required` to require reconciliation rather than retry.

### Completion Notes List

- Story 4.2 file created at `_bmad-output/implementation-artifacts/4-2-prepare-workspace-from-a-ready-repository-backed-folder.md`.
- Sprint status updated to mark `4-2-prepare-workspace-from-a-ready-repository-backed-folder` as `ready-for-dev`.
- Added deterministic prepare-workspace aggregate validation, lexicographic idempotency fingerprinting, safe replay/conflict handling, and C6 lifecycle gate checks.
- Added runtime service and REST/domain processor wiring for `POST /api/v1/folders/{folderId}/workspaces/{workspaceId}/preparation`.
- Added focused aggregate, service, server endpoint, route registration, provider readiness, and contract regression coverage.
- Review fixes record unknown/reconciliation readiness outcomes as metadata-only workspace lifecycle evidence and align REST retry/client-action metadata with the Contract Spine.

### Change Log

- 2026-05-27: Implemented Story 4.2 prepare workspace command, metadata intent persistence, REST/domain wiring, readiness mapping, and focused test coverage.
- 2026-05-27: Marked story ready for review after focused builds/tests and `git diff --check` passed.
- 2026-05-27: Senior review auto-fixed lifecycle evidence for unknown readiness outcomes and REST retry guidance for unknown/reconciliation problems; story marked done.

### File List

- `_bmad-output/implementation-artifacts/4-2-prepare-workspace-from-a-ready-repository-backed-folder.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderResult.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs`
- `src/Hexalith.Folders/Aggregates/Folder/IWorkspacePreparationReadinessValidator.cs`
- `src/Hexalith.Folders/Aggregates/Folder/PrepareWorkspace.cs`
- `src/Hexalith.Folders/Aggregates/Folder/ProviderReadinessWorkspacePreparationValidator.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspacePreparationRequest.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspacePreparationRequested.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspacePreparationService.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderReadinessRequestedCapability.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderReadinessValidationService.cs`
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs`
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`
- `src/Hexalith.Folders.Server/FoldersServerModule.cs`
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs`
- `src/Hexalith.Folders.Server/ProviderReadinessEndpoints.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderCommandFactory.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderWorkspacePreparationAggregateTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderWorkspacePreparationServiceTests.cs`
- `tests/Hexalith.Folders.Server.Tests/RepositoryBackedFolderEndpointTests.cs`
- `tests/Hexalith.Folders.Server.Tests/ServerEndpointRegistrationTests.cs`

## Senior Developer Review (AI)

Reviewer: Jerome on 2026-05-27

Outcome: Approved after auto-fixes.

Findings fixed:

- [HIGH] AC4 was only partially satisfied: provider readiness `unknown_provider_outcome` / `reconciliation_required` returned a result without recording any C6 workspace lifecycle evidence. Fixed `WorkspacePreparationService` to append metadata-only `FolderWorkspaceLifecycleEventRecorded(ProviderOutcomeUnknown)` before returning those canonical outcomes, with focused service assertions.
- [MEDIUM] `unknown_provider_outcome` REST problem details were marked `retryable: true`, contradicting the Contract Spine's wait-for-reconciliation guidance. Fixed the gateway problem mapping and added assertions for `retryable` and `clientAction`.

Validation:

- `dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore --tlp:off -v:minimal`
- `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
- `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
- `dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
- `tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -parallel none -noLogo -namespace Hexalith.Folders.Tests.Aggregates.Folder`
- `tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests -parallel none -noLogo -class Hexalith.Folders.Server.Tests.RepositoryBackedFolderEndpointTests`
- `tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -parallel none -noLogo -class Hexalith.Folders.Contracts.Tests.OpenApi.WorkspaceLockContractGroupTests`
- `tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests -parallel none -noLogo -method Hexalith.Folders.Server.Tests.ServerEndpointRegistrationTests.MapFoldersServerEndpointsShouldRegisterDomainServiceAndTenantSubscriptionRoutes`
- `git diff --check`
