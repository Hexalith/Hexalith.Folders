---
baseline_commit: b0dbe61b1190ea1c53516c371aa6bfdfe30cc871
---

# Story 4.9: Inspect workspace and projection currency

Status: done

## Story

As an authorized actor,
I want to inspect workspace, lock, dirty state, last commit, failed operation, and projection currency,
so that callers and operators have one trustworthy status answer.

## Acceptance Criteria

1. Given an authorized caller requests workspace status, when `GetWorkspaceStatus` runs, then the runtime uses the existing Contract Spine route `GET /api/v1/folders/{folderId}/workspaces/{workspaceId}/status` and operation ID `GetWorkspaceStatus`.
2. Given a workspace status request is read-only, when the request includes `Idempotency-Key`, unsupported freshness, malformed folder/workspace/correlation/task identifiers, or missing authentication context, then the request is rejected before any read-model, aggregate, provider, Git, filesystem, worker, or projection observation.
3. Given authorization succeeds, when status is read, then tenant access, folder ACL, workspace scope, and read-model status are evaluated in the documented authorization-before-observation order before any state distinction is exposed.
4. Given lifecycle events have been emitted, when a compatible read-model snapshot is available, then the response returns contract-shaped workspace status with canonical `LifecycleState`, accepted command state when known, projected state, provider outcome, retry eligibility, freshness, projection lag, and last failure category when present.
5. Given the workspace is locked, dirty, changes-staged, committed, failed, inaccessible, unknown-provider-outcome, or reconciliation-required, when the response is shaped, then it preserves the C6 lifecycle vocabulary and exposes only metadata that is allowed by the existing `WorkspaceStatus` schema unless the OpenAPI Contract Spine is intentionally updated with matching contract tests.
6. Given stale, unavailable, malformed, missing, future-dated, or scope-incompatible projection evidence is encountered, when the response is produced, then it uses explicit safe result categories (`projection_stale`, `projection_unavailable`, `read_model_unavailable`, `not_found`, or safe denial) without leaking folder, workspace, lock, task, provider, repository, commit, path, credential, or file-content details.
7. Given status evidence includes commit or provider outcome metadata, when last commit or failed operation information is represented, then raw commit messages, branch names, repository URLs, provider payloads, local paths, changed file contents, diffs, credential material, emails, tokens, and unauthorized-resource hints are excluded from response bodies, Problem Details, logs, traces, metrics, audit evidence, docs examples, and test diagnostics.
8. Given Story 4.9 is implemented, when existing folder lifecycle status, workspace lock, file mutation, file context, Contract Spine, generated-client guardrails, safe-denial tests, and focused workspace-status tests run, then they continue to pass without manually editing generated client files.

## Tasks / Subtasks

- [x] Preserve the existing workspace-status contract unless a deliberate contract change is required. (AC: 1, 4, 5, 8)
  - [x] Reuse `/api/v1/folders/{folderId}/workspaces/{workspaceId}/status` and operation ID `GetWorkspaceStatus` from `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`.
  - [x] Do not create a second workspace status endpoint or rename the existing operation.
  - [x] Do not add idempotency-key requirements to this read-only query.
  - [x] If explicit lock lease metadata, dirty changed-path detail, or direct last-commit reference is required beyond the current schema, update the OpenAPI spine, examples, `CommitStatusContractGroupTests`, generated outputs, and parity fixtures together.

- [x] Add a workspace-status query runtime boundary. (AC: 3, 4, 5, 6)
  - [x] Add `WorkspaceStatusQuery`, result, read-model request/result/snapshot/status, and handler types under `src/Hexalith.Folders/Queries/Folders` or the nearest existing query namespace.
  - [x] Follow the `FolderLifecycleStatusQueryHandler` and `WorkspaceLockStatusQueryHandler` pattern: validate request shape, authorize first, call a read-model port, then compute a metadata-only result.
  - [x] Keep the query side-effect free; no provider, Git, filesystem, worker, aggregate mutation, reconciliation, cleanup, or commit execution belongs in this handler.
  - [x] Model accepted command state and projected state separately, matching `WorkspaceStatus.acceptedCommandState` and `WorkspaceStatus.projectedState`.

- [x] Define and populate the workspace-status read model. (AC: 4, 5, 6)
  - [x] Add an `IWorkspaceStatusReadModel` abstraction plus an in-memory implementation for tests/dev, tenant-scoped by managed tenant, folder ID, and workspace ID.
  - [x] Capture safe fields needed by the current schema: folder ID, workspace ID, current state, accepted command state, projected state, provider outcome, retry eligibility, optional retry-after, freshness, projection lag, and optional last failure category.
  - [x] Extend `InMemoryFolderRepository` snapshot writes narrowly if needed so lifecycle events materialize workspace-status snapshots alongside existing lifecycle and lock snapshots.
  - [x] Preserve monotonic freshness watermark behavior already used in `InMemoryFolderRepository`; stale/future/incompatible snapshots must fail closed.

- [x] Enforce authorization and safe-denial boundaries. (AC: 2, 3, 6, 7)
  - [x] Use authoritative tenant/principal values from `ITenantContextAccessor` and claim-transform evidence from `IEventStoreClaimTransformEvidenceAccessor`.
  - [x] Treat client-controlled tenant/principal values as comparison inputs only.
  - [x] Do not touch read models on authentication failure, malformed IDs, disallowed idempotency header, or unsupported read consistency.
  - [x] Return safe Problem Details that do not echo protected IDs or requested values in denied cases.

- [x] Wire REST endpoint and DI. (AC: 1, 2, 4, 8)
  - [x] Map `GET /api/v1/folders/{folderId}/workspaces/{workspaceId}/status` in `FoldersDomainServiceEndpoints`.
  - [x] Emit `X-Hexalith-Freshness: read_your_writes` on successful workspace-status responses.
  - [x] Register query handler and read model in `FoldersServiceCollectionExtensions` and server composition as needed.
  - [x] Reuse existing HTTP result mapping helpers where possible; do not hand-roll a conflicting Problem Details shape.

- [x] Add focused workspace-status tests. (AC: 1-8)
  - [x] Add query handler tests for successful committed, locked, dirty/changes-staged, failed, inaccessible, unknown-provider-outcome, and reconciliation-required snapshots.
  - [x] Add query handler tests for projection stale, projection unavailable, malformed snapshot, future observed time, tenant/folder/workspace mismatch, action mismatch, task/correlation mismatch, and authorization watermark mismatch.
  - [x] Add endpoint tests proving route registration, contract-shaped success JSON, freshness header behavior, idempotency-key rejection before read-model access, unsupported freshness rejection before read-model access, malformed identifier rejection before read-model access, and safe denial without protected ID echo.
  - [x] Add metadata leakage tests using `tests/fixtures/audit-leakage-corpus.json` for workspace status results, Problem Details, logs/test diagnostics, and docs examples touched by this story.
  - [x] Keep `CommitStatusContractGroupTests` green; update only if the Contract Spine intentionally changes.

- [x] Preserve focused regression gates. (AC: 8)
  - [x] `dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore --tlp:off -v:minimal`
  - [x] `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - [x] Run focused query/authorization/projection tests added or touched by this story.
  - [x] `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - [x] Run focused server endpoint tests added or touched by this story.
  - [x] `dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - [x] `tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -parallel none -noLogo -class Hexalith.Folders.Contracts.Tests.OpenApi.CommitStatusContractGroupTests`
  - [x] `git diff --check`

## Dev Notes

### Source Context

- Epic 4 story 4.9 requires workspace, lock, dirty state, last commit, failed operation, and projection currency in one trustworthy status answer. [Source: `_bmad-output/planning-artifacts/epics.md#Story 4.9: Inspect workspace and projection currency`]
- PRD FR31 requires actors to inspect whether workspace, task, audit, or provider status is current, delayed, failed, or unavailable. [Source: `_bmad-output/planning-artifacts/prd.md#Functional Requirements`]
- PRD FR39-FR46 require task/commit evidence, failed/incomplete/retried operation reporting, canonical workspace states, canonical error taxonomy, and final-state explanation with retry eligibility and operational evidence. [Source: `_bmad-output/planning-artifacts/prd.md#Functional Requirements`]
- PRD endpoint requirements include workspace state, provider readiness, folder status, dirty state, failed operations, projection status, last commit, and read-only operations-console projections. [Source: `_bmad-output/planning-artifacts/prd.md#Endpoint Specifications`]
- The Contract Spine already declares `GetWorkspaceStatus` as `GET /api/v1/folders/{folderId}/workspaces/{workspaceId}/status`, `read_your_writes`, with safe denial and explicit stale/unavailable projection categories. [Source: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#/api/v1/folders/{folderId}/workspaces/{workspaceId}/status`]
- `WorkspaceStatus` currently permits `folderId`, `workspaceId`, `currentState`, optional `acceptedCommandState`, `projectedState`, `providerOutcome`, `retryEligibility`, optional `retryAfter`, `freshness`, `projectionLag`, and optional `lastFailureCategory`. It has `additionalProperties: false`. [Source: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#WorkspaceStatus`]
- Commit/status contract notes say accepted command state and projected/read-model state must stay separate, safe-denial and read-model-unavailable shapes must not reveal existence, and retry metadata is advisory only. [Source: `docs/contract/commit-status-contract-groups.md#Operation Mapping`]
- Non-mutating parity rules require `GetWorkspaceStatus` to be read-your-writes, omit idempotency, distinguish accepted command state from projection state, expose freshness, and avoid cross-tenant task leaks. [Source: `docs/contract/idempotency-and-parity-rules.md#Non-Mutating Read Consistency`]
- C6 defines canonical lifecycle states and operator dispositions; unlisted state/event pairs reject with `state_transition_invalid`. [Source: `_bmad-output/planning-artifacts/architecture.md#Workspace State Transition Matrix (C6 -- Enumerated)`]

### Previous Story Intelligence

- Story 4.8 added the context-query runtime boundary using handler/read-model/source ports and fail-closed safe-denial behavior. Reuse that boundary style for workspace status instead of putting status assembly directly in endpoint lambdas.
- Story 4.8 endpoint work rejected `Idempotency-Key` on read operations before body parsing or source observation; apply the same read-only discipline to `GetWorkspaceStatus`.
- Story 4.8 review found contract-shape validation gaps can pass focused tests unless endpoint-level tests assert required fields and bounds. Add both handler and endpoint tests for workspace-status shape.
- Previous Epic 4 commits show the local pattern: core query/aggregate code in `src/Hexalith.Folders`, REST mapping in `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`, DI in service collection extensions, focused tests in matching `tests/Hexalith.Folders.*.Tests` projects.

### Existing Implementation State

- `FoldersDomainServiceEndpoints` maps `GetWorkspaceLock` and `GetFolderLifecycleStatus`, but there is no mapped `GetWorkspaceStatus` runtime endpoint yet.
- `WorkspaceLockStatusQueryHandler` already implements read-your-writes lock status, freshness header support, safe request validation, authorization-before-read-model, and fail-closed stale/unavailable handling. Use it as the nearest implementation pattern.
- `FolderLifecycleStatusQueryHandler` already implements eventually-consistent lifecycle status, compatibility checks, safe projection stale/unavailable handling, and metadata-only logging on read-model failure.
- `InMemoryFolderRepository` currently saves lifecycle, branch/ref policy, and workspace-lock snapshots after append/seed. It does not yet save a full `WorkspaceStatus` projection snapshot for the `GetWorkspaceStatus` schema.
- `InMemoryWorkspaceLockStatusReadModel` stores snapshots by `(managedTenantId, folderId, workspaceId)` and scopes evidence for the caller request. Workspace-status should follow the same tenant-scoped storage shape.
- `CommitStatusContractGroupTests` already verifies `GetWorkspaceStatus` route, read consistency, canonical error categories, and schema/example presence. Keep this contract gate green.

### Contract Tension To Resolve Deliberately

- The story asks for lock metadata, dirty evidence, and last commit in one status answer. The current `WorkspaceStatus` schema does not include an explicit lease block, changed-path list, raw commit SHA, or arbitrary last-commit field.
- Do not silently add undeclared JSON properties. Either represent status through existing contract-approved fields (`currentState`, `acceptedCommandState`, `projectedState`, `providerOutcome`, `retryEligibility`, `freshness`, `projectionLag`, `lastFailureCategory`) or intentionally update the OpenAPI spine and contract tests.
- Raw commit references belong behind the commit evidence contract unless the spine is changed. Even then, commit references are tenant-sensitive and must be classified/redacted, not copied as raw provider payload.

### Files To Touch

- `src/Hexalith.Folders/Queries/Folders/` for workspace-status query/read-model/result/snapshot/handler types.
- `src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs` only if the in-memory repository must populate workspace-status snapshots from state.
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs` for core DI registration.
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs` for the REST route.
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs` if server-side DI also needs scoped status handler/read-model defaults.
- `tests/Hexalith.Folders.Tests/Queries/Folders/` for workspace-status handler/projection tests.
- `tests/Hexalith.Folders.Server.Tests/` for endpoint tests.
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/CommitStatusContractGroupTests.cs` and `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` only if a deliberate contract update is required.

### Do Not Touch

- Do not implement commit execution, provider push, reconciliation workers, cleanup repair automation, CLI commands, MCP tools, UI pages, or operations-console routes in this story.
- Do not use workspace status as a back door to expose file contents, diffs, changed-path raw lists, raw commit messages, raw branch names, repository URLs, provider payloads, credential material, local filesystem paths, or unauthorized-resource hints.
- Do not make the read-only endpoint accept `Idempotency-Key`.
- Do not mutate aggregate state while answering workspace status.
- Do not hand-edit generated client files under `src/Hexalith.Folders.Client/Generated`.
- Do not initialize or update nested submodules recursively.

### Latest Technical Context

- Local project context pins the target SDK to .NET SDK `10.0.300`, `net10.0`, C# latest, warnings-as-errors, and central package management. [Source: `_bmad-output/project-context.md#Technology Stack & Versions`]
- Official Microsoft documentation identifies .NET 10 as an LTS release and the active official support policy lists .NET 10 as active. No library upgrade is required for this story; use the repository pins rather than normalizing package versions. [Source: `https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview`, `https://dotnet.microsoft.com/en-us/platform/support/policy`]

### Testing

- Prefer focused tests first because broad full suites have known unrelated environmental failures from earlier story notes.
- Required focused validation:
  - `dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore --tlp:off -v:minimal`
  - `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - focused workspace-status, lifecycle-status, lock-status, authorization, and projection tests added or touched by this story
  - `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - focused workspace-status endpoint tests added by this story
  - `dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - `tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -parallel none -noLogo -class Hexalith.Folders.Contracts.Tests.OpenApi.CommitStatusContractGroupTests`
  - `git diff --check`

### Regression Traps

- Search or read models must not be touched before authorization passes; protected IDs must not leak in denied Problem Details.
- `GetWorkspaceStatus` is read-only and must reject `Idempotency-Key`.
- `GetWorkspaceStatus` is `read_your_writes`; do not silently downgrade to `eventually_consistent`.
- Accepted command state and projected state must stay visibly separate; do not overwrite projection lag by presenting accepted command state as projection truth.
- Stale and unavailable projections are explicit operational outcomes, not generic `internal_error`.
- Last commit evidence must be metadata-only and contract-shaped; do not return raw commit SHAs or changed paths unless the Contract Spine explicitly supports the classified/redacted field.
- Projection age, retry eligibility, and retry-after are advisory client metadata only; they must not trigger scheduler behavior or provider retries.
- Adding response fields without updating OpenAPI will break generated clients and parity gates.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-27: Created via BMAD create-story workflow with inputs from sprint status, Epic 4 story 4.9, PRD, architecture, Contract Spine, commit/status notes, parity rules, root and sibling project contexts, Story 4.8, current workspace status/lock/lifecycle source files, and recent git history.
- 2026-05-27: External technical check used official Microsoft .NET 10 documentation and support policy only; no dependency changes are recommended.
- 2026-05-27: Implemented workspace-status query boundary, in-memory projection, REST endpoint, DI, and focused query/endpoint tests without modifying the OpenAPI spine or generated client files.
- 2026-05-27: Added `read_workspace_status` to folder access and effective-permission action catalogs so workspace-status authorization can complete before read-model observation.
- 2026-05-27: `dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore --tlp:off -v:minimal` passed with 0 warnings and 0 errors using SDK `10.0.300`.
- 2026-05-27: `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal` passed with 0 warnings and 0 errors.
- 2026-05-27: Focused core tests passed: `WorkspaceStatusQueryHandlerTests` (21), `WorkspaceLockStatusProjectionTests` (6), `FolderLifecycleStatusProjectionTests` (24), `FolderLifecycleStatusAuthorizationGateTests` (4), `FolderLifecycleStatusNoFallbackTests` (3), `LayeredFolderAuthorizationServiceTests` (23), `WorkspaceLockActionCatalogTests` (3), `WorkspaceFileContextQueryHandlerTests` (25), `FolderWorkspaceFileMutationServiceTests` (21), `FolderWorkspaceFileMutationAggregateTests` (24), `FolderLifecycleStatusMetadataLeakageTests` (11), and `EffectivePermissionsMetadataLeakageTests` (8).
- 2026-05-27: `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal` passed with 0 warnings and 0 errors.
- 2026-05-27: Focused server tests passed: `WorkspaceStatusEndpointTests` (10) and `FileContextEndpointTests` (19). An extra non-required run of `FolderLifecycleStatusEndpointTests` was blocked by sandbox socket binding (`SocketException: Permission denied`) while starting Kestrel.
- 2026-05-27: `dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal` passed with 0 warnings and 0 errors.
- 2026-05-27: `CommitStatusContractGroupTests` executable passed: 6 tests, 0 failures.
- 2026-05-27: `git diff --check` passed.
- 2026-05-27: Senior review found and auto-fixed fail-closed gaps for malformed workspace-status snapshots and malformed query identifiers at the handler boundary.
- 2026-05-27: Senior review validation reran focused gates: core build, test build, server test build, `WorkspaceStatusQueryHandlerTests` (33), `WorkspaceLockActionCatalogTests` (3), `WorkspaceStatusEndpointTests` (21), contracts test build, `CommitStatusContractGroupTests` (6), and `git diff --check`; all passed.
- 2026-05-27: Web fallback documentation check used official Microsoft .NET 10 overview and .NET support policy; no dependency or SDK changes were required.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Story file prepared for dev-story execution.
- Added metadata-only `GetWorkspaceStatus` runtime support using the existing Contract Spine route and operation ID.
- Added authorization-before-observation query handling, tenant/folder/workspace scoped read-model abstraction, in-memory projection materialization, REST response shaping, freshness header emission, and safe Problem Details mapping.
- Added focused query and endpoint tests for success states, stale/unavailable/fail-closed projection handling, pre-read-model request rejection, and audit leakage corpus coverage.
- Added workspace-status action-catalog support so `read_workspace_status` is treated as a read permission by folder ACL and effective-permission checks.
- Required focused build, test, contract, and diff gates passed; story is ready for review.
- Senior review auto-fixes added handler-level malformed identifier short-circuiting before authorization/read-model access.
- Senior review auto-fixes added fail-closed validation for available workspace-status snapshots so invalid lifecycle, projection, provider, retry, freshness, and failure-category metadata cannot be returned as a successful contract response.

### Senior Developer Review (AI)

Reviewer: GPT-5 Codex on 2026-05-27

Outcome: Approved after auto-fixes. Story status moved to done.

Findings fixed:

- [HIGH] `WorkspaceStatusQueryHandler` accepted malformed `Available` snapshots and could return 200 with contract-invalid lifecycle/provider/retry/freshness metadata instead of failing closed. Fixed by validating workspace lifecycle vocabulary, projected-state source, provider outcome state/reference/category, retry metadata, freshness, projection lag, and last-failure category before computing success.
- [MEDIUM] `WorkspaceStatusQueryHandler` only rejected blank query identifiers, so malformed folder/workspace/correlation/task IDs could reach authorization before being rejected. Fixed by adding canonical identifier validation before authorization and read-model access.

Validation:

- `dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore --tlp:off -v:minimal`
- `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
- `tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -parallel none -noLogo -class Hexalith.Folders.Tests.Queries.Folders.WorkspaceStatusQueryHandlerTests`
- `tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -parallel none -noLogo -class Hexalith.Folders.Tests.Authorization.WorkspaceLockActionCatalogTests`
- `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
- `tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests -parallel none -noLogo -class Hexalith.Folders.Server.Tests.WorkspaceStatusEndpointTests`
- `dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
- `tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -parallel none -noLogo -class Hexalith.Folders.Contracts.Tests.OpenApi.CommitStatusContractGroupTests`
- `git diff --check`

### File List

- `_bmad-output/implementation-artifacts/4-9-inspect-workspace-and-projection-currency.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAccessAction.cs`
- `src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs`
- `src/Hexalith.Folders/Authorization/EffectivePermissionsActionCatalog.cs`
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs`
- `src/Hexalith.Folders/Queries/Folders/IWorkspaceStatusReadModel.cs`
- `src/Hexalith.Folders/Queries/Folders/InMemoryWorkspaceStatusReadModel.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceAcceptedCommandState.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceProjectedState.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceProjectionLag.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceProviderOutcome.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceStatusQuery.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceStatusQueryHandler.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceStatusQueryResult.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceStatusQueryResultCode.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceStatusReadModelRequest.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceStatusReadModelResult.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceStatusReadModelSnapshot.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceStatusReadModelStatus.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceStatusRetryAfter.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceStatusRetryEligibility.cs`
- `tests/Hexalith.Folders.Server.Tests/WorkspaceStatusEndpointTests.cs`
- `tests/Hexalith.Folders.Tests/Authorization/WorkspaceLockActionCatalogTests.cs`
- `tests/Hexalith.Folders.Tests/Queries/Folders/WorkspaceStatusQueryHandlerTests.cs`

### Change Log

- 2026-05-27: Added workspace-status query/read-model runtime, endpoint wiring, in-memory projection materialization, safe result mapping, and focused query/server tests.
- 2026-05-27: Registered `read_workspace_status` as a read-scoped folder access/effective-permission action and completed required focused regression gates.
- 2026-05-27: Senior review auto-fixed workspace-status handler fail-closed validation for malformed query identifiers and malformed available snapshots; story marked done.
