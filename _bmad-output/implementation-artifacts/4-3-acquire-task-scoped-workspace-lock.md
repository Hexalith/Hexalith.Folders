---
baseline_commit: 52173f7
---

# Story 4.3: Acquire task-scoped workspace lock

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer or AI agent,
I want to acquire a task-scoped workspace lock,
so that concurrent work cannot create mixed writes or lost updates.

## Acceptance Criteria

1. Given an authorized caller provides a valid `Idempotency-Key`, `X-Correlation-Id`, required `X-Hexalith-Task-Id`, canonical `folderId`/`workspaceId`, and a valid `LockWorkspaceRequest`, when `POST /api/v1/folders/{folderId}/workspaces/{workspaceId}/lock` is accepted for a ready workspace with no conflicting active lock, then the command is submitted and the aggregate records metadata-only lock acquisition evidence.
2. Given the workspace lifecycle state is `ready`, when `LockWorkspace` is accepted, then the C6 transition records `WorkspaceLocked` and updates `FolderState` from `Ready` to `Locked` without provider, Git, filesystem, Dapr, worker, projection, or audit side effects inside aggregate code.
3. Given the same idempotency key is replayed with an equivalent payload (`folder_id`, `lock_intent`, `requested_lease_seconds`, `task_id`, `workspace_id` in lexicographic order), when `LockWorkspace` is observed again, then the same logical result is returned without duplicate lock events or duplicate lifecycle transitions.
4. Given the same idempotency key is reused with a non-equivalent payload, or the request has an invalid schema version, unsupported lock intent, invalid lease duration, malformed identifier, missing task/correlation/idempotency header, missing folder/workspace, archived folder, unprepared workspace, stale/inaccessible/reconciliation state, or non-ready C6 state, when `LockWorkspace` is attempted, then the operation rejects with the canonical result/problem category and emits no state-changing event.
5. Given another active lock already exists for the workspace scope, when a different task or non-equivalent operation attempts to acquire the lock, then the operation returns deterministic lock-conflict/workspace-locked evidence only after authorization succeeds; unauthorized callers must receive externally indistinguishable safe-denial responses.
6. Given a lock is acquired, when state and event metadata are inspected by later projections, then metadata captures lock ID, holder/task reference, requested lease seconds, acquired/effective/expiry timestamp basis, correlation ID, operation/idempotency evidence, and retry-eligibility basis without exposing tokens, repository URLs, branch names, filesystem paths, file contents, diffs, provider payloads, emails, display names, or raw exception text.
7. Given the public Contract Spine already declares `LockWorkspace`, when server/client/contract behavior is touched, then REST, generated client idempotency helpers, problem categories, and `WorkspaceLockContractGroupTests` remain aligned without manually editing generated client files.
8. Given existing folder creation, repository binding, branch/ref policy, provider readiness, workspace preparation, C6 replay, authorization, safe denial, and Story 4.2 tests exist, when Story 4.3 is implemented, then those behaviors and focused tests continue to pass.

## Tasks / Subtasks

- [x] Add lock acquisition domain vocabulary. (AC: 1, 2, 3, 4, 6)
  - [x] Add `LockWorkspace` or equivalent command under `src/Hexalith.Folders/Aggregates/Folder/` with tenant, organization, folder, workspace, schema version, lock intent, requested lease seconds, actor, correlation, task, idempotency, and optional payload-tenant evidence.
  - [x] Add a metadata-only lock event or extend the existing workspace lifecycle event path so `WorkspaceLocked` carries lock ID, holder/task reference, lease timing basis, retry-eligibility basis, correlation, task, idempotency key, and idempotency fingerprint.
  - [x] Add only metadata needed by Story 4.3; leave release, expiry processing, renewal, file mutation, commit, status query, cleanup, CLI, MCP, and UI behavior to later stories.
  - [x] Keep enum and result serialization name-based if any new type can cross a serialized boundary.

- [x] Implement deterministic aggregate handling for lock acquisition. (AC: 2, 3, 4, 5, 6, 8)
  - [x] Extend `FolderCommandValidator` for `LockWorkspace`: require `requestSchemaVersion == "v1"`, `lockIntent == "exclusive_write"`, canonical `workspaceId`, safe task/correlation/idempotency identifiers, and `requestedLeaseSeconds` in the Contract Spine range `1..86400`.
  - [x] Compute the idempotency fingerprint from the Contract Spine equivalence fields in lexicographic order: `folder_id`, `lock_intent`, `requested_lease_seconds`, `task_id`, `workspace_id`.
  - [x] Require created, active, repository-bound state and the exact prepared `WorkspaceId` before accepting lock acquisition.
  - [x] Require C6 `WorkspaceLifecycleState.Ready` before emitting `WorkspaceLocked`; reject invalid state/event pairs with `FolderResultCode.StateTransitionInvalid` unless a more specific authorized lock-conflict result is added and mapped consistently.
  - [x] Preserve existing ordering: validation and authorization evidence before resource observation; idempotent replay before duplicate side effects; expected business rejections return typed results instead of throwing.
  - [x] Keep aggregate code pure: no provider calls, Git calls, filesystem mutation, Dapr service invocation, logging, service resolution, clock reads, read-model writes, lock timers, background renewal, or expiry processing.

- [x] Wire `LockWorkspace` through REST and the domain processor. (AC: 1, 4, 5, 7, 8)
  - [x] Add `FoldersServerModule.LockWorkspaceCommandType = "Hexalith.Folders.Commands.LockWorkspace"` and reuse the existing `FolderCommandActionTokenMapper` action token `lock_workspace`.
  - [x] Add a server endpoint for `POST /api/v1/folders/{folderId}/workspaces/{workspaceId}/lock` matching the OpenAPI route, required headers, payload shape, safe problem mapping, and accepted-command response.
  - [x] Add a `WorkspaceLockAcquisitionService` or equivalent service following `WorkspacePreparationService`: layered authorization first, command validation, stream load, aggregate handling, repository idempotency lookup, atomic append, append-conflict reread, and metadata-only rejections.
  - [x] Extend `FolderDomainProcessor` to deserialize a lock payload and dispatch to the lock service with authoritative tenant and task/correlation/idempotency evidence from the EventStore envelope.
  - [x] Do not manually edit `src/Hexalith.Folders.Client/Generated/*`; if generated artifacts drift, update the OpenAPI spine/generator inputs and regenerate through the established pipeline.

- [x] Add focused lock acquisition tests. (AC: 1, 2, 3, 4, 5, 6, 7, 8)
  - [x] Add aggregate tests for accepted lock from ready, replay, idempotency conflict, malformed command, wrong workspace ID, missing workspace, unprepared/preparing/locked/dirty/failed/reconciliation states, archived folder, and metadata-only event fields.
  - [x] Add replay tests proving `WorkspaceLocked` advances `ready -> locked` and invalid lock events leave state unchanged or reject through the established C6 matrix path.
  - [x] Add service tests for authorization-first safe denial, idempotency lookup unavailable, append conflict reread, lock contention after append race, and no protected resource details in rejections.
  - [x] Add REST endpoint tests for accepted request shape, authoritative route `workspaceId`, required `Idempotency-Key`, required `X-Correlation-Id`, required `X-Hexalith-Task-Id`, malformed JSON, unsupported schema version, invalid lease, unsupported intent, idempotency conflict, lock conflict/workspace locked, transition invalid, and safe denial.
  - [x] Add or update action-token registration tests so `LockWorkspace` maps to `lock_workspace` and the domain processor accepts the command type.

- [x] Preserve contract and regression gates. (AC: 7, 8)
  - [x] Run focused aggregate tests under `tests/Hexalith.Folders.Tests/Aggregates/Folder`.
  - [x] Run focused server endpoint tests changed by this story.
  - [x] Run `WorkspaceLockContractGroupTests`.
  - [x] Run focused `FolderCommandActionTokenMapperTests` and route registration tests if command mapping or endpoints change.
  - [x] Run `git diff --check`.

## Dev Notes

### Source Context

- Epic 4 owns the repository-backed workspace task lifecycle: prepare workspaces, acquire locks, mutate files safely, query bounded context, commit changes, and expose deterministic failure/status/idempotency/redaction behavior. [Source: `_bmad-output/planning-artifacts/epics.md#Epic 4: Repository-Backed Workspace Task Lifecycle`]
- Story 4.3 requires `AcquireWorkspaceLock` to transition folder state from `ready` to `locked` and capture owner, age/expiry basis, and retry-eligibility metadata for later projections. [Source: `_bmad-output/planning-artifacts/epics.md#Story 4.3: Acquire task-scoped workspace lock`]
- PRD FR25 requires authorized actors to acquire a task-scoped workspace lock; FR27 requires competing operations to be denied when lock ownership or workspace state is unsafe; FR28 requires active/expired/stale/abandoned/interrupted/released lock states to become observable. [Source: `_bmad-output/planning-artifacts/prd.md#Functional Requirements`]
- NFR17 requires lock acquisition to be deterministic, tenant-scoped, and limited to one active write lock per tenant/repository/workspace scope. NFR18 requires conflict response, lease duration, renewal behavior, expiry behavior, cleanup after failed commit, and commit-release semantics to be defined. For Story 4.3, implement acquisition and metadata basis only; do not implement later renewal/release/cleanup semantics. [Source: `_bmad-output/planning-artifacts/prd.md#Non-Functional Requirements`]
- The C6 matrix states `ready -> locked` on `WorkspaceLocked`, and unlisted state/event pairs reject with canonical `state_transition_invalid` while state remains unchanged. [Source: `_bmad-output/planning-artifacts/architecture.md#Workspace State Transition Matrix (C6 - Enumerated)`]
- The Contract Spine already declares `POST /api/v1/folders/{folderId}/workspaces/{workspaceId}/lock` with operation ID `LockWorkspace`, required `Idempotency-Key`, required `X-Correlation-Id`, required `X-Hexalith-Task-Id`, and request body `LockWorkspaceRequest`. [Source: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#/api/v1/folders/{folderId}/workspaces/{workspaceId}/lock`]
- `LockWorkspaceRequest` currently contains `requestSchemaVersion`, `lockIntent`, and `requestedLeaseSeconds`; the schema allows only `exclusive_write` and lease seconds from `1` to `86400`. [Source: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#LockWorkspaceRequest`]
- Contract tests require mutating workspace/lock operations to declare lexicographically ordered idempotency equivalence, include `task_id`, and require `X-Hexalith-Task-Id`. [Source: `tests/Hexalith.Folders.Contracts.Tests/OpenApi/WorkspaceLockContractGroupTests.cs#WorkspaceLockOperations_DeclareRequiredMetadataAndIdempotencyRules`]

### Previous Story Intelligence

- Story 4.2 implemented `PrepareWorkspace` as a metadata-only aggregate command and `WorkspacePreparationRequested` intent event, then used `FolderWorkspaceLifecycleEventRecorded(WorkspacePrepared)` for the C6 transition to `Ready`.
- Story 4.2 wired preparation through `FoldersDomainServiceEndpoints`, `FoldersServerModule.PrepareWorkspaceCommandType`, `FolderDomainProcessor`, `WorkspacePreparationService`, `ProviderReadinessWorkspacePreparationValidator`, and focused aggregate/server/contract tests.
- Story 4.2 review fixed unknown provider readiness handling so metadata-only `ProviderOutcomeUnknown` lifecycle evidence is recorded instead of returning without a C6 transition. Preserve that pattern: if Story 4.3 introduces any unknown/reconciliation path, it must record C6-compatible evidence and not silently retry.
- Known environmental failures from Story 4.2: broad solution/AppHost builds can try blocked NuGet network access, and full server/worker test assemblies can fail in sandbox on Kestrel socket binding. Prefer focused project builds and in-process xUnit classes unless the story explicitly needs live hosts.

### Existing Implementation State

- `FolderStateTransitions` already includes `FolderWorkspaceLifecycleEvent.WorkspaceLocked` and accepts `FolderWorkspaceLifecycleState.Ready` to `FolderWorkspaceLifecycleState.Locked`; no new C6 state is needed for basic acquisition.
- `FolderState` currently stores workspace lifecycle fields (`WorkspaceLifecycleState`, `WorkspaceOperatorDisposition`, `WorkspaceId`, `WorkspacePolicyRef`, `WorkspaceLifecycleEvent`, `WorkspaceOperationId`, `WorkspaceCorrelationId`, `WorkspaceTaskId`, `WorkspaceLifecycleUpdatedAt`) but does not yet store lock-specific lease metadata. Story 4.3 must add enough state/event fields for later lock projections.
- `FolderStateApply` already routes `FolderWorkspaceLifecycleEventRecorded` through the C6 matrix and records idempotency. Any enriched lock event must preserve dedupe behavior and fail loudly on unhandled event types.
- `FolderCommandValidator` already validates `PrepareWorkspace` and computes its idempotency fingerprint from Contract Spine fields. Extend this validator rather than adding a parallel hashing path.
- `FolderResultCode` does not currently include `WorkspaceLocked`, `LockConflict`, or `LockExpired`. If this story adds a specific lock-conflict result code, update server problem mapping, rejection events, tests, and contract categories together.
- `FolderCommandActionTokenMapper` already maps `"Hexalith.Folders.Commands.LockWorkspace"` to `lock_workspace`, but `FoldersServerModule` does not yet expose a lock command constant and `FolderDomainProcessor` does not yet handle it.
- The generated client already includes `LockWorkspaceAsync` and `LockWorkspaceRequest.ComputeIdempotencyHash`; do not hand-edit generated client files.

### Architecture Guardrails

- Use .NET 10, nullable-aware C#, file-scoped namespaces, one primary public type per file, PascalCase public members, camelCase locals/parameters, central package versions, xUnit v3, and Shouldly. [Source: `_bmad-output/project-context.md#Technology Stack & Versions`; `_bmad-output/project-context.md#Testing Rules`]
- Aggregate handlers must stay deterministic and side-effect free. Pass timestamps from callers; never perform I/O, provider calls, Dapr calls, logging, filesystem work, clock reads, or service resolution inside aggregate transitions. [Source: `_bmad-output/project-context.md#Language-Specific Rules`]
- Authorization order is contractual: JWT validation, EventStore claim transform, tenant-access freshness, folder ACL, EventStore validator, then Dapr deny-by-default policy. Do not reorder or skip layers for lock acquisition. [Source: `_bmad-output/project-context.md#Critical Don't-Miss Rules`]
- Client-controlled tenant/principal values from headers, query strings, or payloads are comparison inputs only. Authoritative tenant and principal values come from authenticated context and EventStore claim-transform evidence. [Source: `_bmad-output/project-context.md#Framework-Specific Rules`]
- Metadata-only is mandatory across events, projections, logs, traces, metrics, audit records, problem details, docs examples, and tests. [Source: `_bmad-output/project-context.md#Critical Don't-Miss Rules`]
- Do not initialize nested submodules or use recursive submodule commands. [Source: `AGENTS.md`]

### Files To Touch

- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderResult.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderResultCode.cs` only if a new lock-specific code is required.
- `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs`
- New lock command/request/event/service types under `src/Hexalith.Folders/Aggregates/Folder/`
- `src/Hexalith.Folders.Server/FoldersServerModule.cs`
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs`
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs`
- Focused tests under `tests/Hexalith.Folders.Tests/Aggregates/Folder/`, `tests/Hexalith.Folders.Server.Tests/`, and `tests/Hexalith.Folders.Contracts.Tests/OpenApi/`

### Do Not Touch

- Do not implement release lock, lock inspection query, lock expiry scheduler, renewal, auth-revalidation timer, file add/change/remove, context query, commit, cleanup, SDK convenience helpers, CLI tools, MCP tools, UI pages, or generated-client edits unless a compile-time contract break proves it is unavoidable and fully updated.
- Do not add package versions directly to `.csproj` files.
- Do not use live GitHub, Forgejo, Aspire, Dapr sidecars, Docker, Keycloak, Redis, network-dependent tests, or nested-submodule initialization.
- Do not expose or persist file contents, diffs, raw paths, provider payloads, repository URLs, credential material, tokens, emails, display names, raw claim bags, lock ownership proof values in unauthorized diagnostics, or unauthorized resource existence.

### Testing

- Recommended parent-shell validation:
  - `dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore --tlp:off -v:minimal`
  - `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - `tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -parallel none -noLogo -namespace Hexalith.Folders.Tests.Aggregates.Folder`
  - `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - focused server test class/methods changed by this story
  - `tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -parallel none -noLogo -class Hexalith.Folders.Contracts.Tests.OpenApi.WorkspaceLockContractGroupTests`
  - `git diff --check`

### Regression Traps

- `ready` means workspace prepared, no lock, and no uncommitted changes. Do not allow locking from `preparing`, `requested`, `changes_staged`, `dirty`, `failed`, `unknown_provider_outcome`, `reconciliation_required`, or `inaccessible`.
- Do not treat `requestedLeaseSeconds` as permission to start timers, renewals, cleanup, or background expiry processing in this story. Capture the basis metadata only.
- Do not derive lock ownership from client-controlled tenant, principal, or payload fields. Holder/owner evidence must be scoped to authorized tenant/folder/workspace/task context.
- Do not put `lockOwnershipProof` or equivalent opaque proof values into unauthorized Problem Details, logs, traces, audit diagnostics, or test failure messages. Release proof is a later story.
- Do not collapse lock contention, idempotency conflict, and C6 invalid transition into one vague result at the authorized boundary if the Contract Spine expects distinct categories.
- Authorization and safe-denial ordering must prevent hidden folder/repository/provider/workspace/lock existence leaks.
- Existing `FolderStateApply` fails loudly on unknown event types; keep that behavior.

### Latest Technical Context

- No new external library or live provider API is required for Story 4.3. Implementation should use the project-local .NET 10, EventStore, Dapr, OpenAPI, NSwag, xUnit v3, and Shouldly versions already pinned in repository configuration and `_bmad-output/project-context.md`.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 4.3: Acquire task-scoped workspace lock`
- `_bmad-output/planning-artifacts/epics.md#Epic 4: Repository-Backed Workspace Task Lifecycle`
- `_bmad-output/planning-artifacts/prd.md#Functional Requirements`
- `_bmad-output/planning-artifacts/prd.md#Non-Functional Requirements`
- `_bmad-output/planning-artifacts/architecture.md#Workspace State Transition Matrix (C6 - Enumerated)`
- `docs/contract/workspace-lock-contract-groups.md#Operation Mapping`
- `docs/contract/workspace-lock-contract-groups.md#Lock Ownership Proof`
- `docs/contract/idempotency-and-parity-rules.md#Mutating Command Equivalence`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#LockWorkspace`
- `src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs#LockWorkspaceRequest`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/WorkspaceLockContractGroupTests.cs`
- `_bmad-output/implementation-artifacts/4-2-prepare-workspace-from-a-ready-repository-backed-folder.md`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-27: Story created via `bmad-create-story` workflow using sprint status, Epic 4 source, PRD/architecture/UX context, root and sibling project-context facts, Story 4.2 implementation record, Contract Spine, contract notes, local source, focused tests, and recent git history.
- 2026-05-27: Latest external research was not added because Story 4.3 introduces no new external library/API version and should use repository-pinned project-local versions.
- 2026-05-27: Checklist validation applied during authoring: tightened idempotency equivalence to the Contract Spine field list, called out the existing generated SDK methods, prohibited generated-client edits, and added specific lock metadata and safe-denial traps.
- 2026-05-27: Red phase confirmed with `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`; build failed on missing `LockWorkspace` before implementation.
- 2026-05-27: Implemented lock aggregate vocabulary, deterministic validation/fingerprinting, C6 `ready -> locked` replay, layered acquisition service, REST endpoint, domain processor dispatch, and focused tests.
- 2026-05-27: Validation passed for focused aggregate namespace, lock endpoint/action/route tests, `WorkspaceLockContractGroupTests`, and `git diff --check`; broader server class run still hits the known sandbox Kestrel socket bind failure in `ProjectRouteShouldReturn501NotImplemented`.
- 2026-05-27: Senior review found and fixed gateway-derived domain rejection reason-code drift for lock conflict and workspace transition invalid outcomes; validation passed for focused aggregate, service, endpoint, action-token, route-registration non-socket methods, contract, and diff-check gates. Full `ServerEndpointRegistrationTests` still hits the known sandbox Kestrel socket bind failure in `ProjectRouteShouldReturn501NotImplemented`.

### Completion Notes List

- Story 4.3 context created for `4-3-acquire-task-scoped-workspace-lock`.
- Sprint status updated to mark `4-3-acquire-task-scoped-workspace-lock` as `ready-for-dev`.
- Added task-scoped `LockWorkspace` aggregate command and metadata-only `WorkspaceLockAcquired` event with lock ID, holder task, lease timing basis, retry eligibility, correlation, task, idempotency key, and fingerprint evidence.
- Added deterministic aggregate handling for validation, Contract Spine idempotency equivalence, ready-workspace-only C6 lock transition, idempotent replay, idempotency conflict, and authorized lock conflict.
- Added `WorkspaceLockAcquisitionService`, REST `/lock` endpoint, EventStore command type constant, domain processor dispatch, and safe lock-conflict problem mapping without generated-client edits.
- Added focused aggregate, service, endpoint, route, and action-token tests covering accepted acquisition, replay/conflict, validation failures, safe denial ordering, append race reread, and route-authoritative workspace ID.
- Senior review fixed actual EventStore-derived rejection mapping by adding lock/workspace-specific rejection event types and normalizing hyphenated gateway reason codes back to canonical REST problem categories.

### File List

- `_bmad-output/implementation-artifacts/4-3-acquire-task-scoped-workspace-lock.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderResult.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderResultCode.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs`
- `src/Hexalith.Folders/Aggregates/Folder/LockWorkspace.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceLockAcquired.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceLockAcquisitionRequest.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceLockAcquisitionService.cs`
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs`
- `src/Hexalith.Folders.Server/DuplicateWorkspaceLockRejected.cs`
- `src/Hexalith.Folders.Server/WorkspaceTransitionInvalidRejected.cs`
- `src/Hexalith.Folders.Server/FolderCommandRejected.cs`
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`
- `src/Hexalith.Folders.Server/FoldersServerModule.cs`
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderCommandFactory.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderWorkspaceLockAggregateTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderWorkspaceLockAcquisitionServiceTests.cs`
- `tests/Hexalith.Folders.Server.Tests/FolderCommandActionTokenMapperTests.cs`
- `tests/Hexalith.Folders.Server.Tests/RepositoryBackedFolderEndpointTests.cs`
- `tests/Hexalith.Folders.Server.Tests/ServerEndpointRegistrationTests.cs`

### Change Log

- 2026-05-27: Implemented Story 4.3 workspace lock acquisition and moved story to review.
- 2026-05-27: Senior review fixed lock/workspace domain rejection reason-code mapping and moved story to done.

## Senior Developer Review (AI)

Reviewer: GPT-5 Codex on 2026-05-27

Outcome: Approved after automatic fixes.

### Findings Fixed

- HIGH: Actual EventStore-derived domain rejection reason codes for workspace lock conflicts and workspace transition failures could degrade to the generic REST fallback because the endpoint mapper only handled simulated lowercase underscore codes. Fixed by emitting specific workspace lock/transition rejection event types and normalizing EventStore hyphenated reason codes to `lock_conflict`, `workspace_locked`, and `workspace_transition_invalid`.
- MEDIUM: Lock endpoint tests covered mocked contract reason codes but not gateway-derived hyphenated reason codes. Expanded the endpoint matrix to cover both forms.

### Validation

- `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
- `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
- `dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
- `tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -parallel none -noLogo -class Hexalith.Folders.Tests.Aggregates.Folder.FolderWorkspaceLockAggregateTests`
- `tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -parallel none -noLogo -class Hexalith.Folders.Tests.Aggregates.Folder.FolderWorkspaceLockAcquisitionServiceTests`
- `tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests -parallel none -noLogo -class Hexalith.Folders.Server.Tests.RepositoryBackedFolderEndpointTests`
- `tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests -parallel none -noLogo -class Hexalith.Folders.Server.Tests.FolderCommandActionTokenMapperTests`
- `tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests -parallel none -noLogo -method Hexalith.Folders.Server.Tests.ServerEndpointRegistrationTests.FoldersServerModuleShouldExposeStableDomainAndTenantRoutes`
- `tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests -parallel none -noLogo -method Hexalith.Folders.Server.Tests.ServerEndpointRegistrationTests.MapFoldersServerEndpointsShouldRegisterDomainServiceAndTenantSubscriptionRoutes`
- `tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests -parallel none -noLogo -method Hexalith.Folders.Server.Tests.ServerEndpointRegistrationTests.TenantEventsRouteShouldCarryExpectedTopicMetadata`
- `tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -parallel none -noLogo -class Hexalith.Folders.Contracts.Tests.OpenApi.WorkspaceLockContractGroupTests`
- `git diff --check`

Known environment limitation: full `ServerEndpointRegistrationTests` still fails in this sandbox at `ProjectRouteShouldReturn501NotImplemented` because Kestrel socket binding returns permission denied.
