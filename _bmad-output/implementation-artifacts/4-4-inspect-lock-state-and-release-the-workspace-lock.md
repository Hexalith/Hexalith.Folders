---
baseline_commit: 88cfb08
---

# Story 4.4: Inspect lock state and release the workspace lock

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an authorized actor,
I want to inspect and release a workspace lock when policy allows,
so that completed or abandoned task ownership is visible and controlled.

## Acceptance Criteria

1. Given an authorized caller requests `GET /api/v1/folders/{folderId}/workspaces/{workspaceId}/lock` with a valid `X-Correlation-Id`, no `Idempotency-Key`, and an optional supported freshness header, when the workspace lock state is inspected, then the response returns metadata-only `WorkspaceLockStatus` with workspace reference, lock state, optional lease metadata, retry eligibility, and freshness.
2. Given the caller is missing authentication, tenant access, folder ACL, workspace-lock read permission, or a valid scope, when lock state is inspected, then the response uses the safe-denial envelope and does not reveal whether the folder, workspace, lock, holder, task, repository binding, lease history, or provider state exists.
3. Given an authorized caller provides a valid `Idempotency-Key`, `X-Correlation-Id`, required `X-Hexalith-Task-Id`, canonical route `folderId`/`workspaceId`, and a valid `ReleaseWorkspaceLockRequest`, when `POST /api/v1/folders/{folderId}/workspaces/{workspaceId}/lock/release` is accepted for an active lock owned by that task scope, then the command is submitted and the aggregate records metadata-only `WorkspaceLockReleased` evidence.
4. Given the workspace lifecycle state is `locked`, no file mutations have been applied, and the supplied lock ID and non-secret `lockOwnershipProof` match the authorized tenant/folder/workspace/task scope, when release is accepted, then the C6 transition records `WorkspaceLockReleased`, updates workspace state from `locked` to `ready`, and clears active lock metadata from current state while preserving release evidence in the emitted event.
5. Given the same release idempotency key is replayed with an equivalent payload (`folder_id`, `lock_id`, `lock_ownership_proof`, `task_id`, `workspace_id` in lexicographic order), when `ReleaseWorkspaceLock` is observed again, then the same logical result is returned without duplicate release events or duplicate lifecycle transitions. `release_reason_code` is audit metadata and must not participate in the equivalence hash.
6. Given the same release idempotency key is reused with a non-equivalent payload, or the request has an invalid schema version, malformed identifier, missing required header, missing or mismatched lock ID, mismatched ownership proof, wrong task, wrong workspace, missing folder/workspace, archived folder, expired lock, non-locked C6 state, `changes_staged`, `dirty`, `failed`, `inaccessible`, `unknown_provider_outcome`, or `reconciliation_required` state, when release is attempted, then the operation rejects with the canonical result/problem category and emits no state-changing release event.
7. Given file mutations have been applied and the workspace is `changes_staged`, when release is requested, then release is rejected because the state model requires commit before clean release or expiry into `dirty`; the implementation must not silently unlock or discard staged changes.
8. Given existing folder creation, repository binding, branch/ref policy, provider readiness, workspace preparation, Story 4.3 lock acquisition, C6 replay, authorization, safe denial, Contract Spine, generated client, and focused tests exist, when Story 4.4 is implemented, then those behaviors and tests continue to pass without manually editing generated client files.

## Tasks / Subtasks

- [x] Add lock inspection query/read-model support. (AC: 1, 2, 8)
  - [x] Add `WorkspaceLockStatusQuery`, result, handler, read-model request/result/snapshot, and in-memory read model under `src/Hexalith.Folders/Queries/Folders/`, following `FolderLifecycleStatusQueryHandler` and `BranchRefPolicyQueryHandler` patterns.
  - [x] Extend the in-memory repository snapshot path so `WorkspaceLockAcquired` and `WorkspaceLockReleased` materialize tenant-scoped lock status snapshots with freshness, correlation, task, operation, lock ID, lease status, and C6 state metadata.
  - [x] Compute `active`, `unlocked`, and `expired` status from existing state and caller time without mutating aggregate state from a read path.
  - [x] Reject `Idempotency-Key` on the GET route before read-model access, preserve `X-Correlation-Id`, and accept only the Contract Spine freshness value for this operation.

- [x] Add release-lock domain vocabulary. (AC: 3, 4, 5, 6, 7)
  - [x] Add `ReleaseWorkspaceLock` command and `WorkspaceLockReleaseRequest` under `src/Hexalith.Folders/Aggregates/Folder/` with tenant, organization, folder, workspace, schema version, lock ID, lock ownership proof, release reason code, actor, correlation, task, idempotency, and optional payload-tenant evidence.
  - [x] Add `WorkspaceLockReleased` metadata-only event carrying workspace ID, lock ID, holder/task reference, release reason code, lease status basis, correlation, task, idempotency key, idempotency fingerprint, and occurred-at timestamp.
  - [x] Store or derive the non-secret `lockOwnershipProof` only as scoped metadata needed to validate release. Do not treat it as an auth credential, do not expose it in unauthorized diagnostics, and do not log it.
  - [x] Keep release, inspection, and proof code metadata-only; do not add file content, diffs, provider payloads, repository URLs, branch names, emails, display names, tokens, or raw exception text.

- [x] Implement deterministic aggregate handling for release. (AC: 4, 5, 6, 7, 8)
  - [x] Extend `FolderCommandValidator` for `ReleaseWorkspaceLock`: require `requestSchemaVersion == "v1"`, canonical `workspaceId`, canonical `lockId`, canonical `lockOwnershipProof`, valid release reason code, and safe task/correlation/idempotency identifiers.
  - [x] Compute the release fingerprint from the Contract Spine equivalence fields in lexicographic order: `folder_id`, `lock_id`, `lock_ownership_proof`, `task_id`, `workspace_id`. Exclude `release_reason_code`.
  - [x] Require created, active, repository-bound state and the exact current `WorkspaceId` before accepting release.
  - [x] Require matching active lock ID and task/holder metadata; reject proof mismatch or task mismatch with a canonical lock-not-owned or validation-compatible result that maps consistently through REST, EventStore, and tests.
  - [x] Require C6 `WorkspaceLifecycleState.Locked` for clean release unless this story explicitly supports the later `committed -> ready` release-finalization path; reject `changes_staged` and `dirty` without clearing lock metadata.
  - [x] Apply `WorkspaceLockReleased` through the C6 matrix and clear current active lock fields (`WorkspaceLockId`, intent, holder task, lease timestamps, retry basis) only when the transition is accepted.
  - [x] Preserve ordering: validation and authorization evidence before resource observation; idempotent replay before duplicate side effects; expected business rejections return typed results instead of throwing.
  - [x] Keep aggregate code pure: no provider calls, Git calls, filesystem mutation, Dapr service invocation, logging, service resolution, clock reads, read-model writes, lock timers, background renewal, or expiry processing.

- [x] Wire release and inspection through REST and the domain processor. (AC: 1, 2, 3, 5, 6, 8)
  - [x] Add `FoldersServerModule.ReleaseWorkspaceLockCommandType = "Hexalith.Folders.Commands.ReleaseWorkspaceLock"` and action-token mapping if not already covered by the existing `lock_workspace` permission model.
  - [x] Add `GET /api/v1/folders/{folderId}/workspaces/{workspaceId}/lock` matching OpenAPI response shape, safe denial, freshness behavior, and no-idempotency rule.
  - [x] Add `POST /api/v1/folders/{folderId}/workspaces/{workspaceId}/lock/release` matching the OpenAPI route, required headers, route-authoritative `workspaceId`, payload shape, safe problem mapping, and accepted-command response.
  - [x] Add `WorkspaceLockReleaseService` following `WorkspaceLockAcquisitionService`: layered authorization first, command validation, stream load, aggregate handling, repository idempotency lookup, atomic append, append-conflict reread, and metadata-only rejections.
  - [x] Extend `FolderDomainProcessor` to deserialize release payloads and dispatch through the release service with authoritative tenant, actor, task, correlation, and idempotency evidence from the EventStore envelope.
  - [x] Normalize gateway reason codes for release failures: `lock_not_owned`, `lock_expired`, `workspace_transition_invalid`, `reconciliation_required`, `idempotency_conflict`, and safe-denial categories.
  - [x] Do not manually edit `src/Hexalith.Folders.Client/Generated/*`; if generated artifacts drift, update the OpenAPI spine/generator inputs and regenerate through the established pipeline.

- [x] Add focused lock inspection and release tests. (AC: 1, 2, 3, 4, 5, 6, 7, 8)
  - [x] Add aggregate tests for accepted clean release from locked, replay, idempotency conflict, invalid schema, wrong workspace ID, missing lock, wrong lock ID, wrong task, proof mismatch, archived folder, expired lock, and non-locked C6 states.
  - [x] Add replay tests proving `WorkspaceLockReleased` advances `locked -> ready`, clears active lock state, and leaves state unchanged for invalid release events.
  - [x] Add query/read-model tests for active lock, unlocked lock, expired lock, stale/unavailable projection, safe denial, no idempotency on GET, and metadata-only response fields.
  - [x] Add service tests for authorization-first safe denial, idempotency lookup unavailable, append conflict reread, release-after-append race, lock-not-owned, expired-lock, and no protected resource details in rejections.
  - [x] Add REST endpoint tests for accepted release request shape, route-authoritative `workspaceId`, required headers, malformed JSON, unsupported schema version, invalid body, unknown fields, idempotency conflict, lock-not-owned, expired lock, transition invalid, gateway reason-code normalization, and safe denial.
  - [x] Add or update action-token registration tests so `ReleaseWorkspaceLock` maps consistently and the domain processor accepts the command type.

- [x] Preserve contract and regression gates. (AC: 8)
  - [x] Run focused aggregate tests under `tests/Hexalith.Folders.Tests/Aggregates/Folder`.
  - [x] Run focused query/read-model tests changed by this story.
  - [x] Run focused server endpoint tests changed by this story.
  - [x] Run `WorkspaceLockContractGroupTests`.
  - [x] Run focused `FolderCommandActionTokenMapperTests` and route registration tests if command mapping or endpoints change.
  - [x] Run `git diff --check`.

## Dev Notes

### Source Context

- Epic 4 owns the repository-backed workspace task lifecycle: prepare workspaces, acquire locks, mutate files safely, query bounded context, commit changes, and expose deterministic failure/status/idempotency/redaction behavior. [Source: `_bmad-output/planning-artifacts/epics.md#Epic 4: Repository-Backed Workspace Task Lifecycle`]
- Story 4.4 requires inspection and release of workspace locks. Permitted lock metadata must be returned, valid release must follow C6, and release after mutations must be rejected because commit is required before clean release. [Source: `_bmad-output/planning-artifacts/epics.md#Story 4.4: Inspect lock state and release the workspace lock`]
- PRD FR26 requires authorized actors to inspect permitted lock state, owner, task, age, expiry, and retry-eligibility metadata. FR29 requires authorized release when ownership and policy allow. [Source: `_bmad-output/planning-artifacts/prd.md#Functional Requirements`]
- PRD NFR17-NFR19 require deterministic tenant-scoped locking, explicit lease/expiry/release behavior, and deterministic status/retry evidence for lock contention, stale locks, abandoned locks, and interrupted tasks. [Source: `_bmad-output/planning-artifacts/prd.md#Non-Functional Requirements`]
- C6 defines `locked -> ready` on `WorkspaceLockReleased`, `locked -> changes_staged` on `FileMutated`, `changes_staged -> committed` on `CommitSucceeded`, `changes_staged -> dirty` on `LockLeaseExpired`, and `committed -> ready` on `WorkspaceLockReleased`. Unlisted state/event pairs reject with `state_transition_invalid`. [Source: `_bmad-output/planning-artifacts/architecture.md#Workspace State Transition Matrix (C6 -- Enumerated)`]
- The Contract Spine already declares `GET /api/v1/folders/{folderId}/workspaces/{workspaceId}/lock` as `GetWorkspaceLock`, no idempotency key, `read_your_writes` freshness, safe denial, and `WorkspaceLockStatus` responses. [Source: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#/api/v1/folders/{folderId}/workspaces/{workspaceId}/lock`]
- The Contract Spine already declares `POST /api/v1/folders/{folderId}/workspaces/{workspaceId}/lock/release` as `ReleaseWorkspaceLock`, required `Idempotency-Key`, `X-Correlation-Id`, `X-Hexalith-Task-Id`, and request body `ReleaseWorkspaceLockRequest`. [Source: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#/api/v1/folders/{folderId}/workspaces/{workspaceId}/lock/release`]
- `ReleaseWorkspaceLockRequest` currently contains `requestSchemaVersion`, `lockId`, `lockOwnershipProof`, and `releaseReasonCode`; release reason values are `caller_completed`, `caller_abandoned`, `operator_requested`, `authorization_revoked`, `task_cancelled`, and `lock_revoked`. [Source: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#ReleaseWorkspaceLockRequest`]
- Contract notes state `release_reason_code` is audit metadata and intentionally excluded from idempotency equivalence. [Source: `docs/contract/workspace-lock-contract-groups.md#Operation Mapping`]
- Contract notes state `lockOwnershipProof` is a non-secret opaque proof scoped to authenticated tenant, folder, workspace, task, and lock; unauthorized Problem Details and audit diagnostics must omit it. [Source: `docs/contract/workspace-lock-contract-groups.md#Lock Ownership Proof`]
- Generated SDK methods and idempotency helper classes already exist for `GetWorkspaceLockAsync`, `ReleaseWorkspaceLockAsync`, and `ReleaseWorkspaceLockRequest.ComputeIdempotencyHash`. Do not hand-edit generated files. [Source: `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs`; `src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs`]

### Previous Story Intelligence

- Story 4.3 implemented `LockWorkspace` as a metadata-only aggregate command and `WorkspaceLockAcquired` event with lock ID, holder task, lease timing basis, retry-eligibility basis, correlation, task, idempotency key, and fingerprint evidence.
- Story 4.3 wired acquisition through `FoldersDomainServiceEndpoints`, `FoldersServerModule.LockWorkspaceCommandType`, `FolderDomainProcessor`, `WorkspaceLockAcquisitionService`, and focused aggregate/server/contract tests.
- Story 4.3 senior review fixed EventStore-derived lock rejection reason-code drift by adding specific rejection event types and normalizing hyphenated gateway reason codes back to canonical REST problem categories. Repeat this for release-specific reason codes.
- Story 4.3 explicitly left release, expiry scheduler, renewal, file mutation, commit, status query, cleanup, CLI, MCP, and UI behavior to later stories. Story 4.4 may implement lock inspection and release only.
- Known environmental failures from Story 4.3: broad solution/AppHost builds can try blocked NuGet network access, and full server route tests can fail in this sandbox on Kestrel socket binding. Prefer focused project builds and in-process xUnit classes unless this story explicitly needs live hosts.

### Existing Implementation State

- `FolderStateTransitions` already includes `WorkspaceLockReleased` and accepts `Locked -> Ready` and `Committed -> Ready`. It does not accept release from `ChangesStaged`, `Dirty`, `Failed`, `Inaccessible`, `UnknownProviderOutcome`, or `ReconciliationRequired`.
- `FolderState` already stores active lock fields: `WorkspaceLockId`, `WorkspaceLockIntent`, `WorkspaceLockRequestedLeaseSeconds`, `WorkspaceLockHolderTaskId`, `WorkspaceLockAcquiredAt`, `WorkspaceLockEffectiveAt`, `WorkspaceLockExpiresAt`, and `WorkspaceLockRetryEligibilityBasis`.
- `FolderStateApply` applies `WorkspaceLockAcquired` and sets active lock fields. It does not yet apply a release-specific event or clear active lock fields on `WorkspaceLockReleased`.
- `FolderAggregate.Handle(LockWorkspace, occurredAt)` already validates idempotency and emits `WorkspaceLockAcquired`. Add release handling beside this pattern instead of creating a parallel aggregate path.
- `FolderCommandValidator` already validates lock acquisition and computes the lock acquisition fingerprint. Add release validation/fingerprinting in the same validator to preserve canonical hashing behavior.
- `FolderResultCode` currently has `LockConflict`, `StateTransitionInvalid`, and related result codes, but does not visibly include `LockNotOwned` or `LockExpired`. If adding these codes, update server mapping, rejection events, tests, and contract category mapping together.
- `FoldersDomainServiceEndpoints` currently maps `POST /lock`; it does not map `GET /lock` or `POST /lock/release`.
- `FolderDomainProcessor` currently handles `LockWorkspaceCommandType`; it does not handle release.
- Query/read-model support exists for lifecycle status and branch/ref policy, but no workspace lock status query/read model exists yet. Reuse the existing authorization-first read model pattern.

### Architecture Guardrails

- Use .NET 10, nullable-aware C#, file-scoped namespaces, one primary public type per file, PascalCase public members, camelCase locals/parameters, central package versions, xUnit v3, and Shouldly. [Source: `_bmad-output/project-context.md#Technology Stack & Versions`; `_bmad-output/project-context.md#Testing Rules`]
- Aggregate handlers must stay deterministic and side-effect free. Pass timestamps from callers; never perform I/O, provider calls, Dapr calls, logging, filesystem work, service resolution, or clock reads inside aggregate transitions. [Source: `_bmad-output/project-context.md#Language-Specific Rules`]
- Authorization order is contractual: JWT validation, EventStore claim transform, tenant-access freshness, folder ACL, EventStore validator, then Dapr deny-by-default policy. Do not reorder or skip layers for release or inspection. [Source: `_bmad-output/project-context.md#Critical Don't-Miss Rules`]
- Client-controlled tenant/principal values from headers, query strings, or payloads are comparison inputs only. Authoritative tenant and principal values come from authenticated context and EventStore claim-transform evidence. [Source: `_bmad-output/project-context.md#Framework-Specific Rules`]
- Non-mutating operations must not accept `Idempotency-Key`; they still require correlation behavior, authorization parity, safe denial shape, audit metadata, and read-consistency classification. [Source: `_bmad-output/project-context.md#Critical Don't-Miss Rules`]
- Metadata-only is mandatory across events, projections, logs, traces, metrics, audit records, problem details, docs examples, and tests. [Source: `_bmad-output/project-context.md#Critical Don't-Miss Rules`]
- EventStore domain logic should return `DomainResult` with rejection events/results rather than throwing for business rule violations; EventStore owns envelope metadata, and command extension metadata must be sanitized at the API boundary. [Source: `Hexalith.EventStore/_bmad-output/project-context.md#Critical Don't-Miss Rules`]
- Tenants remains the source of tenant membership truth; consumers must preserve tenant isolation and never trust user-supplied claims for authorization. [Source: `Hexalith.Tenants/_bmad-output/project-context.md#Critical Don't-Miss Rules`]
- Do not initialize nested submodules or use recursive submodule commands. [Source: `AGENTS.md`]

### Files To Touch

- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderResult.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderResultCode.cs` if new release-specific codes are required.
- `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs`
- `src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs`
- New release command/request/event/service types under `src/Hexalith.Folders/Aggregates/Folder/`
- New lock status query/read-model types under `src/Hexalith.Folders/Queries/Folders/`
- `src/Hexalith.Folders.Server/FoldersServerModule.cs`
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs`
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs`
- Focused tests under `tests/Hexalith.Folders.Tests/Aggregates/Folder/`, `tests/Hexalith.Folders.Server.Tests/`, and `tests/Hexalith.Folders.Contracts.Tests/OpenApi/`

### Do Not Touch

- Do not implement file add/change/remove, path policy enforcement, context query, commit execution, cleanup automation, lock renewal scheduler, background expiry processing, CLI commands, MCP tools, UI pages, or repair workflows.
- Do not make GET lock mutate aggregate state or append `LockLeaseExpired`; reads may report expired based on time and projection evidence, but state transitions remain command/event driven.
- Do not release from `changes_staged` or `dirty`; staged changes require commit or an explicit future recovery path.
- Do not add package versions directly to `.csproj` files.
- Do not use live GitHub, Forgejo, Aspire, Dapr sidecars, Docker, Keycloak, Redis, network-dependent tests, or nested-submodule initialization.
- Do not expose or persist file contents, diffs, raw paths, provider payloads, repository URLs, credential material, tokens, emails, display names, raw claim bags, `lockOwnershipProof` in unauthorized diagnostics, or unauthorized resource existence.

### Testing

- Recommended parent-shell validation:
  - `dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore --tlp:off -v:minimal`
  - `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - `tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -parallel none -noLogo -namespace Hexalith.Folders.Tests.Aggregates.Folder`
  - focused query/read-model tests added for lock status
  - `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - focused server endpoint tests changed by this story
  - `tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -parallel none -noLogo -class Hexalith.Folders.Contracts.Tests.OpenApi.WorkspaceLockContractGroupTests`
  - `git diff --check`

### Regression Traps

- The release route must use the route `workspaceId` as authoritative, just as Story 4.3 did for lock acquisition. A body value must not override the route.
- `release_reason_code` is audit metadata. Including it in the idempotency fingerprint would make harmless retry reason wording changes look like idempotency conflicts.
- `lockOwnershipProof` is non-secret but still sensitive metadata. It must not appear in unauthorized Problem Details, safe-denial bodies, logs, traces, test failure messages, or examples outside the release request itself.
- Do not infer lock ownership from client-controlled tenant, principal, or payload fields. Ownership must be scoped to authorized tenant/folder/workspace/task state.
- `WorkspaceLockReleased` must clear active lock state only after C6 accepts the transition. Clearing lock fields before transition validation can lose evidence on invalid releases.
- Expired-lock reporting must not silently break or take over locks. If release sees an expired lock, return the canonical expired-lock outcome unless this story deliberately records a C6 expiry event with tests.
- Existing `FolderStateApply` fails loudly on unknown event types; keep fail-loud behavior for new release events.
- Existing contract tests already protect generated SDK methods, lock status schemas, safe problem details, and proof leakage. Do not bypass them by adding local-only response shapes that drift from the Contract Spine.

### Latest Technical Context

- No new external library or live provider API is required for Story 4.4. Implementation should use repository-pinned .NET 10, EventStore, Dapr, OpenAPI, NSwag, xUnit v3, and Shouldly versions already captured in repository configuration and `_bmad-output/project-context.md`.
- Network research was not needed for this story because the relevant contract and runtime behavior are project-local and already specified by the Contract Spine, architecture C6 matrix, PRD, and prior implementation.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 4.4: Inspect lock state and release the workspace lock`
- `_bmad-output/planning-artifacts/epics.md#Epic 4: Repository-Backed Workspace Task Lifecycle`
- `_bmad-output/planning-artifacts/prd.md#Functional Requirements`
- `_bmad-output/planning-artifacts/prd.md#Non-Functional Requirements`
- `_bmad-output/planning-artifacts/architecture.md#Workspace State Transition Matrix (C6 -- Enumerated)`
- `docs/contract/workspace-lock-contract-groups.md#Operation Mapping`
- `docs/contract/workspace-lock-contract-groups.md#Lock Ownership Proof`
- `docs/contract/idempotency-and-parity-rules.md#Operation Inventory`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#GetWorkspaceLock`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#ReleaseWorkspaceLock`
- `src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs#ReleaseWorkspaceLockRequest`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/WorkspaceLockContractGroupTests.cs`
- `_bmad-output/implementation-artifacts/4-3-acquire-task-scoped-workspace-lock.md`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-27: Story created via `bmad-create-story` workflow using explicit Story ID 4.4, sprint status, Epic 4 source, PRD/architecture/UX context, root and sibling project-context facts, Story 4.3 implementation record, Contract Spine, contract notes, local source, focused tests, and recent git history.
- 2026-05-27: Latest external research was not added because Story 4.4 introduces no new external library/API version and should use repository-pinned project-local versions.
- 2026-05-27: Checklist validation applied during authoring: added release idempotency equivalence, called out the existing generated SDK methods, prohibited generated-client edits, identified the current runtime gap for GET/release routes, added proof-handling traps, and required read paths not to mutate aggregate state.
- 2026-05-27: Implemented lock inspection query/read-model, release domain vocabulary, aggregate release handling, REST routes, domain processor dispatch, and focused tests.
- 2026-05-27: Validation passed: core/server builds, aggregate namespace tests, workspace lock query/action tests, workspace lock endpoint/mapper tests, WorkspaceLockContractGroupTests, and git diff whitespace check.

### Completion Notes List

- Story 4.4 context created for `4-4-inspect-lock-state-and-release-the-workspace-lock`.
- Sprint status updated to mark `4-4-inspect-lock-state-and-release-the-workspace-lock` as `done`.
- Implementation code and focused tests were changed by this workflow.
- Added authorized workspace lock inspection with tenant-scoped in-memory projection snapshots and read-your-writes freshness.
- Added deterministic workspace lock release command/service/event handling, scoped proof derivation, idempotency equivalence, C6 release transition, and safe typed rejections.
- Wired GET lock and POST lock release REST routes plus EventStore domain processor dispatch without editing generated client files.
- Added focused aggregate, service, query/read-model, action-token, server endpoint, and contract regression tests.
- Senior review auto-fixes: release now rejects committed/non-locked lifecycle state for Story 4.4, and GET lock rejects malformed route/correlation/task identifiers before read-model access.
- Senior review validation passed: affected test projects built successfully; focused aggregate, query/read-model, action catalog, endpoint, action-token mapper, contract, and whitespace gates passed.

### File List

- `_bmad-output/implementation-artifacts/4-4-inspect-lock-state-and-release-the-workspace-lock.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/tests/test-summary.md`
- `_bmad-output/story-automator/orchestration-3-20260526-203745.md`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAccessAction.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderResult.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderResultCode.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs`
- `src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs`
- `src/Hexalith.Folders/Aggregates/Folder/ReleaseWorkspaceLock.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceLockReleaseRequest.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceLockReleaseService.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceLockReleased.cs`
- `src/Hexalith.Folders/Authorization/EffectivePermissionsActionCatalog.cs`
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs`
- `src/Hexalith.Folders/Queries/Folders/IWorkspaceLockStatusReadModel.cs`
- `src/Hexalith.Folders/Queries/Folders/InMemoryWorkspaceLockStatusReadModel.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceLockLeaseMetadata.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceLockRetryEligibility.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceLockStatusQuery.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceLockStatusQueryHandler.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceLockStatusQueryResult.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceLockStatusQueryResultCode.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceLockStatusReadModelRequest.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceLockStatusReadModelResult.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceLockStatusReadModelSnapshot.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceLockStatusReadModelStatus.cs`
- `src/Hexalith.Folders.Server/Authorization/FolderCommandActionTokenMapper.cs`
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs`
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`
- `src/Hexalith.Folders.Server/FoldersServerModule.cs`
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs`
- `tests/Hexalith.Folders.Server.Tests/FolderCommandActionTokenMapperTests.cs`
- `tests/Hexalith.Folders.Server.Tests/WorkspaceLockEndpointTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderCommandFactory.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderWorkspaceLockReleaseAggregateTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderWorkspaceLockReleaseServiceTests.cs`
- `tests/Hexalith.Folders.Tests/Authorization/WorkspaceLockActionCatalogTests.cs`
- `tests/Hexalith.Folders.Tests/Queries/Folders/WorkspaceLockStatusProjectionTests.cs`

### Change Log

- 2026-05-27: Implemented Story 4.4 lock inspection and release workflow; updated sprint/story status to review after focused validation passed.
- 2026-05-27: Senior developer review auto-fixed release-state validation and GET lock identifier validation; added focused regression coverage; updated story and sprint status to done after focused validation passed.

## Senior Developer Review (AI)

Reviewer: GPT-5 Codex on 2026-05-27

### Findings

- HIGH: `ReleaseWorkspaceLock` accepted `committed -> ready` when active lock metadata remained on state. Story 4.4 AC6 requires non-locked C6 states to reject; auto-fixed by accepting only `locked` plus existing ready/missing-lock compatibility, and adding committed-state regression coverage in `FolderWorkspaceLockReleaseAggregateTests`.
- MEDIUM: `GET /lock` allowed malformed route, correlation, and task identifiers to reach authorization/read-model handling. Auto-fixed with pre-query canonical identifier validation and endpoint tests proving read-model access is skipped.
- MEDIUM: Git reality included story-automator/test-summary artifact updates that were missing from the story File List. Auto-fixed by recording those artifacts; `Hexalith.Builds` has pre-existing submodule working-tree changes and was not reviewed as application source for this story.

### Outcome

- Auto-fixes applied. No critical issues remain after review. Story status set to done.

### Validation

- `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`: passed.
- `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`: passed.
- `Hexalith.Folders.Tests.Aggregates.Folder.FolderWorkspaceLockReleaseAggregateTests`: passed, 22 total.
- `Hexalith.Folders.Tests.Queries.Folders.WorkspaceLockStatusProjectionTests`: passed, 6 total.
- `Hexalith.Folders.Tests.Authorization.WorkspaceLockActionCatalogTests`: passed, 2 total.
- `Hexalith.Folders.Server.Tests.WorkspaceLockEndpointTests`: passed, 26 total.
- `Hexalith.Folders.Server.Tests.FolderCommandActionTokenMapperTests`: passed, 7 total.
- `Hexalith.Folders.Contracts.Tests.OpenApi.WorkspaceLockContractGroupTests`: passed, 6 total.
- `git diff --check`: passed.
