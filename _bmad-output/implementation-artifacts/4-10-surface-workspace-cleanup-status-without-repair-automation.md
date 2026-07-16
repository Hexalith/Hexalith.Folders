---
baseline_commit: ac9906303d64ce5a67c2d137ffd2b64ad5508763
---

# Story 4.10: Surface workspace cleanup status without repair automation

Status: done

## Story

As an operator or developer,
I want cleanup status visible after completed, failed, interrupted, or abandoned tasks,
so that working-copy state is understandable without MVP repair controls.

## Acceptance Criteria

1. Given an authorized caller queries cleanup status for a folder/workspace/task lifecycle, when the runtime handles the query, then it returns only metadata-only cleanup status after authentication, tenant access, folder ACL, workspace scope, and task/correlation scope are validated before any read-model or workspace observation.
2. Given cleanup status is available, when the response is shaped, then it exposes one of `pending`, `succeeded`, `failed`, or `status_only` with reason code, advisory retry eligibility, timestamp/freshness metadata, and correlation ID.
3. Given cleanup status is unavailable, stale, malformed, denied, hidden, wrong-tenant, wrong-folder, wrong-workspace, or task-incompatible, when the query is handled, then the response fails closed using safe denial, `projection_stale`, `projection_unavailable`, `read_model_unavailable`, or `not_found` without leaking workspace, task, repository, path, provider, lock, commit, or credential details.
4. Given a lifecycle is completed, failed, interrupted, abandoned, dirty, locked, committed, unknown-provider-outcome, or reconciliation-required, when cleanup status is materialized, then status explains cleanup visibility without changing the C6 lifecycle state unless the Contract Spine and C6 matrix are intentionally updated together.
5. Given cleanup metadata is returned, when response bodies, Problem Details, logs, traces, metrics, audit evidence, docs examples, and test diagnostics are produced, then raw file contents, diffs, local filesystem paths, repository URLs, branch names, commit messages, provider payloads, credential material, tokens, emails, and unauthorized-resource hints are excluded.
6. Given the MVP excludes repair workflows, when this story is implemented, then no repair, discard, unlock, cleanup request, provider write, Git operation, filesystem mutation, scheduler, hidden mutation, CLI command, MCP tool, SDK convenience helper, or UI repair control is exposed.
7. Given cleanup is a lifecycle visibility concern, when contracts and parity fixtures are updated, then read consistency, correlation/task behavior, canonical error categories, metadata sensitivity, examples, and parity rows are updated consistently and generated client files are not hand-edited.
8. Given Story 4.10 is implemented, when existing workspace status, lifecycle status, lock, file mutation, context query, Contract Spine, safe-denial, metadata-leakage, and focused cleanup-status tests run, then they pass without regressing Story 4.9 status semantics.

## Tasks / Subtasks

- [x] Define the canonical cleanup-status contract deliberately. (AC: 1, 2, 3, 7)
  - [x] Add a read-only Contract Spine operation for cleanup status, unless architectural review decides to extend `WorkspaceStatus` instead. Recommended route: `GET /api/v1/folders/{folderId}/workspaces/{workspaceId}/cleanup/status`; operation ID: `GetWorkspaceCleanupStatus`.
  - [x] Define `WorkspaceCleanupStatus` with `folderId`, `workspaceId`, optional `taskId`, `status`, `reasonCode`, `retryEligibility`, `freshness`, optional `correlationId`, optional `observedAt`, and optional `lastAttemptedAt`; keep `additionalProperties: false`.
  - [x] Define cleanup status enum exactly as `pending`, `succeeded`, `failed`, `status_only`.
  - [x] Add `x-hexalith-read-consistency`, `x-hexalith-correlation`, `x-hexalith-authorization`, `x-hexalith-canonical-error-categories`, `x-hexalith-audit-metadata-keys`, and parity dimensions matching neighboring status queries.
  - [x] Update contract tests and parity fixtures for the new operation; regenerate generated artifacts through the existing pipeline only if the build requires it. Do not hand-edit generated client files.

- [x] Add a cleanup-status query boundary in core. (AC: 1, 2, 3, 5)
  - [x] Add query, result, read-model request/result/snapshot/status, retry metadata, and handler types under `src/Hexalith.Folders/Queries/Folders`.
  - [x] Reuse the `WorkspaceStatusQueryHandler` pattern: validate identifiers and unsupported headers before authorization, authorize before read-model access, then compute a metadata-only result.
  - [x] Add an action token such as `read_workspace_cleanup_status`, then register it in `FolderAccessAction` and `EffectivePermissionsActionCatalog` as a read permission.
  - [x] Validate snapshot contract shape before returning success: cleanup status enum, reason-code shape, retry metadata, freshness/read consistency, timestamp not in the future, correlation/task scope, and authorization watermark compatibility.
  - [x] Return explicit safe outcomes for stale, unavailable, malformed, scope-mismatched, and not-found cleanup projections.

- [x] Materialize cleanup-status projection data without performing cleanup. (AC: 2, 4, 6)
  - [x] Add `IWorkspaceCleanupStatusReadModel` and an in-memory implementation for tests/dev, scoped by managed tenant, folder ID, workspace ID, and task/correlation scope where present.
  - [x] Extend `InMemoryFolderRepository` narrowly to save cleanup-status snapshots from existing folder/workspace state where safe. Do not invoke provider, Git, filesystem, worker, scheduler, or cleanup request behavior.
  - [x] Map current lifecycle states to status-only cleanup visibility conservatively: committed/ready with no cleanup implication may be `succeeded` or `status_only` only when evidence exists; locked/changes-staged/dirty/failed/unknown-provider-outcome/reconciliation-required should expose status visibility without pretending cleanup completed.
  - [x] Keep C6 lifecycle states unchanged. Do not add `cleanup_pending` or `cleaned` to `FolderWorkspaceLifecycleState` unless the OpenAPI LifecycleState enum, `FolderStateTransitions`, C6 tests, docs, examples, and parity fixtures are updated in the same change.

- [x] Wire REST endpoint and DI. (AC: 1, 3, 6, 8)
  - [x] Register the cleanup read model and query handler in `FoldersServiceCollectionExtensions.AddFoldersLifecycleStatus`.
  - [x] Map the cleanup-status GET route in `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`.
  - [x] Reject `Idempotency-Key` on the cleanup-status read before authorization or read-model access.
  - [x] Reject unsupported `X-Hexalith-Freshness` before read-model access; use the contract-declared read consistency only.
  - [x] Reuse existing safe Problem Details helpers and success header patterns; do not hand-roll a conflicting error shape.

- [x] Add focused cleanup-status tests and regression coverage. (AC: 1-8)
  - [x] Add query handler tests for `pending`, `succeeded`, `failed`, and `status_only`.
  - [x] Add fail-closed tests for malformed identifiers, authentication failure, tenant denial, folder ACL denial, stale projection, unavailable projection, malformed snapshot, future timestamps, task/correlation mismatch, scope mismatch, and authorization watermark mismatch.
  - [x] Add endpoint tests proving route registration, contract-shaped JSON, freshness/correlation headers, idempotency-key rejection before read-model access, unsupported freshness rejection before read-model access, malformed identifier rejection before read-model access, and safe denial without protected ID echo.
  - [x] Add metadata leakage tests using `tests/fixtures/audit-leakage-corpus.json` against success payloads, Problem Details, logs/test diagnostics, and docs examples touched by this story.
  - [x] Update Contract Spine tests, parity oracle generator tests, and previous-spine/parity fixture checks as required by the new contract operation.

- [x] Preserve focused regression gates. (AC: 8)
  - [x] `dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore --tlp:off -v:minimal`
  - [x] `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - [x] Focused cleanup-status query tests plus existing `WorkspaceStatusQueryHandlerTests`, `WorkspaceLockStatusProjectionTests`, `FolderLifecycleStatusProjectionTests`, `FolderLifecycleStatusAuthorizationGateTests`, and `WorkspaceLockActionCatalogTests`.
  - [x] `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - [x] Focused cleanup-status endpoint tests plus existing `WorkspaceStatusEndpointTests`.
  - [x] `dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - [x] Focused OpenAPI tests covering cleanup-status contract and affected commit/status group tests.
  - [x] `git diff --check`

## Dev Notes

### Source Context

- Epic 4 story 4.10 requires cleanup status after completed, failed, interrupted, or abandoned tasks, with pending/succeeded/failed/status-only state, reason, retryability, timestamp, correlation ID, and no repair/discard/hidden mutation action. [Source: `_bmad-output/planning-artifacts/epics.md#Story 4.10: Surface workspace cleanup status without repair automation`]
- PRD FR30 assigns workspace cleanup status visibility for completed/failed/interrupted/abandoned task lifecycles to Epic 4. [Source: `_bmad-output/planning-artifacts/epics.md#FR Coverage Map`]
- NFR11-NFR20 require lifecycle terminal/non-terminal visibility, cleanup pending/cleaned observability, inspectable intermediate states after interruption or failure, idempotency for cleanup request operations, deterministic retry metadata, and no automated remediation in MVP. [Source: `_bmad-output/planning-artifacts/epics.md#Reliability, Idempotency, and Failure Visibility`]
- PRD Journey 3 explicitly says interrupted tasks must leave visible locked/dirty/task-associated state and that MVP does not silently repair, discard, or commit changes. [Source: `_bmad-output/planning-artifacts/prd.md#Journey 3: Agent Task Is Interrupted and Leaves Inspectable State`]
- UX requirements preserve the read-only MVP boundary and forbid mutation, repair, file editing, raw diff display, credential reveal, and hidden operational side effects in console flows. [Source: `_bmad-output/planning-artifacts/ux-design-specification.md#Stable UX Design Requirements`]
- Architecture requires workers to own external provider, Git, working-copy, reconciliation, and rate-limit side effects; aggregates and query handlers must not perform provider/filesystem mutations. [Source: `_bmad-output/planning-artifacts/architecture.md#Workers / Reconciliation / Rate Limiting`]
- The current Contract Spine includes `GetWorkspaceStatus`, commit evidence, provider outcome, and reconciliation status, but no cleanup-status operation or cleanup-status schema. [Source: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#GetWorkspaceStatus`; `docs/contract/commit-status-contract-groups.md#Deferred Owners`]
- Provider capability inventory already includes `cleanup_expiration`, and fake/provider readiness tests expect cleanup capability metadata. Story 4.10 should not confuse provider capability evidence with runtime repair or provider mutation. [Source: `src/Hexalith.Folders/Providers/Abstractions/ProviderOperationCatalog.cs`; `tests/Hexalith.Folders.Tests/Providers/Abstractions/ProviderCapabilityDiscoveryWorkflowTests.cs`]

### Previous Story Intelligence

- Story 4.9 implemented metadata-only `GetWorkspaceStatus` using the existing Contract Spine route, authorization-before-observation, a tenant-scoped read-model abstraction, in-memory projection materialization, REST response shaping, freshness header emission, and focused query/endpoint tests.
- Story 4.9 added `read_workspace_status` to folder ACL and effective-permission action catalogs. Story 4.10 should mirror this for cleanup-status reads instead of bypassing authorization catalogs.
- Story 4.9 senior review found two important traps: malformed available snapshots must fail closed before returning contract-invalid success, and malformed query identifiers must short-circuit before authorization/read-model access.
- Story 4.9 validation passed focused core/server/contracts gates plus `CommitStatusContractGroupTests`; keep those tests green when adding cleanup status.
- Recent Epic 4 commits establish the pattern: core query/read-model code in `src/Hexalith.Folders/Queries/Folders`, in-memory projection materialization in `InMemoryFolderRepository`, REST mapping in `FoldersDomainServiceEndpoints`, DI in `FoldersServiceCollectionExtensions`, and focused tests in matching `tests/Hexalith.Folders.*.Tests` projects.

### Existing Implementation State

- `WorkspaceStatusQueryHandler` currently validates identifiers before authorization, uses `LayeredFolderAuthorizationService`, calls `IWorkspaceStatusReadModel` only after authorization, validates snapshot contract shape, and maps stale/unavailable/malformed/not-found outcomes to safe results. Reuse this pattern for cleanup status.
- `WorkspaceStatusReadModelSnapshot` currently contains status fields only for workspace status: folder/workspace/current state, accepted command state, projected state, provider outcome, retry eligibility, retry-after, freshness, projection lag, last failure category, and evidence scope. It does not carry cleanup-specific status/reason/timestamp fields.
- `InMemoryFolderRepository.SaveWorkspaceStatusSnapshot` maps existing C6 lifecycle state to workspace-status projection and currently uses placeholder `provref_workspace_status` provider correlation metadata. Cleanup status should be separate or deliberately added to the contract; do not overload provider outcome as cleanup state.
- `FolderStateTransitions.StateCatalog` currently contains `requested`, `preparing`, `ready`, `locked`, `changes_staged`, `dirty`, `committed`, `failed`, `inaccessible`, `unknown_provider_outcome`, and `reconciliation_required`. It does not contain `cleanup_pending` or `cleaned`.
- `FoldersDomainServiceEndpoints` currently maps workspace lock, workspace status, file context, branch/ref policy, lifecycle status, and other endpoints. Add cleanup status beside workspace status if a new operation is introduced.
- `CommitStatusContractGroupTests` currently uses a fixed allow-list for commit/status operations. If adding `GetWorkspaceCleanupStatus`, update or add the relevant contract test intentionally so the operation is not treated as accidental drift.

### Contract Tensions To Resolve Deliberately

- Planning artifacts mention `CleanupPending` and `Cleaned` as required observable lifecycle states, but current C6 and OpenAPI lifecycle enums do not include cleanup states. The lower-risk implementation is an orthogonal cleanup-status projection, not new C6 lifecycle states.
- Story 4.10 says cleanup status is queried. Because no existing `GetWorkspaceCleanupStatus` contract exists, the implementation likely needs a Contract Spine addition before runtime wiring. Do not add an undocumented runtime endpoint.
- Cleanup request operations are mutating and idempotent, but this story is status visibility only. Do not implement a cleanup request command or accept `Idempotency-Key` on the read query.
- Retry eligibility in this story is advisory metadata only. It must not schedule cleanup, retry provider calls, unlock workspaces, discard changes, or repair working copies.

### Files To Touch

- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` for cleanup-status operation/schema/examples if a new contract operation is added.
- `docs/contract/commit-status-contract-groups.md` and `docs/contract/idempotency-and-parity-rules.md` for cleanup-status operation notes and parity/read-consistency rows.
- `tests/fixtures/parity-contract.yaml` and related generator tests only through the established parity generation/update process.
- `src/Hexalith.Folders/Queries/Folders/` for cleanup-status query/read-model/result/snapshot/handler types.
- `src/Hexalith.Folders/Aggregates/Folder/FolderAccessAction.cs` and `src/Hexalith.Folders/Authorization/EffectivePermissionsActionCatalog.cs` for the new read action token.
- `src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs` only for status projection materialization.
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs` for DI registration.
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs` for the REST route and result mapping.
- `tests/Hexalith.Folders.Tests/Queries/Folders/` for cleanup-status handler/projection tests.
- `tests/Hexalith.Folders.Server.Tests/` for cleanup-status endpoint tests.
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/` for contract tests around cleanup status, operation allow-lists, schema examples, safe-denial, and metadata-only requirements.

### Do Not Touch

- Do not implement repair, discard, unlock, cleanup request, cleanup scheduler, provider cleanup, Git cleanup, filesystem cleanup, retry worker, or operations-console mutation behavior.
- Do not mutate aggregate state from a cleanup-status query.
- Do not add CLI commands, MCP tools, SDK convenience helpers, UI pages, or generated client hand edits in this story.
- Do not expose local paths, changed paths, file contents, diffs, branch names, commit messages, repository URLs, provider payloads, credential material, tokens, emails, or unauthorized-resource hints.
- Do not initialize or update nested submodules recursively.

### Latest Technical Context

- Local project context pins .NET SDK `10.0.302`, `net10.0`, C# latest, central package management, warnings-as-errors, xUnit v3, Shouldly, and repository-owned package versions. Use repo pins rather than normalizing versions. [Source: `_bmad-output/project-context.md#Technology Stack & Versions`]
- Official .NET docs identify .NET 10 as an LTS release, and the official .NET support policy lists .NET 10 as active with support ending November 14, 2028. No package upgrade is required for this story. [Source: `https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview`; `https://dotnet.microsoft.com/en-us/platform/support/policy`]

### Testing

- Prefer focused gates first; broad suites may hit unrelated environment constraints.
- Required focused validation is listed in the tasks above.
- Include `tests/fixtures/audit-leakage-corpus.json` coverage for every cleanup-status output channel touched by this story.
- If the Contract Spine changes, run the affected OpenAPI tests and parity generator/drift tests that enforce operation rows and generated artifact consistency.

### Regression Traps

- Do not read cleanup projections, workspace status, files, provider state, Git state, or filesystem state before authorization passes.
- Read-only cleanup status must reject `Idempotency-Key`; cleanup request idempotency belongs to a future mutating operation, not this query.
- Do not use cleanup status as a back door to expose raw working-copy state or local filesystem paths.
- Do not collapse `pending`, `failed`, `status_only`, stale projection, unavailable projection, and denied/not-found into generic `internal_error`.
- Do not treat `retryEligibility` or `retryAfter` as scheduler instructions.
- Do not add fields to `WorkspaceStatus` or a new cleanup schema without matching OpenAPI, examples, contract tests, parity rows, and generated artifact handling.
- Do not add `cleanup_pending` or `cleaned` to C6 state names casually; this would be a public contract change.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-27: Created via BMAD create-story workflow with inputs from sprint status, Epic 4 story 4.10, PRD, architecture, UX spec, root project context, Story 4.9, current workspace-status runtime files, Contract Spine, commit/status notes, parity rules, provider capability catalog, and recent git history.
- 2026-05-27: External technical check used official Microsoft .NET 10 overview and .NET support policy only; no dependency or SDK changes are recommended.
- 2026-05-27: Validation checklist applied; critical contract gap identified and addressed in the story guidance by requiring a deliberate cleanup-status contract/schema/parity update before runtime exposure.
- 2026-05-27: Added `GetWorkspaceCleanupStatus` contract/schema/example, regenerated parity row via local parity generator DLL, regenerated NSwag client via `GenerateHexalithFoldersClient`, and regenerated idempotency helper via local helper generator DLL after the MSBuild helper target was blocked by network restore.
- 2026-05-27: `dotnet test` focused core/server/contracts runs were attempted but VSTest aborted before execution because the sandbox denies local socket creation: `System.Net.Sockets.SocketException (13): Permission denied`.
- 2026-05-27: Re-ran focused regression tests with the xUnit v3 in-process runners to avoid the sandbox-blocked VSTest socket path; core query/action, server endpoint, contract/OpenAPI/parity, and testing artifact checks passed.
- 2026-05-27: Restored the parity-oracle generator from the existing local NuGet package cache with audit disabled for offline execution, then updated `previous-spine.yaml` operations to include the intentional `GetWorkspaceCleanupStatus` addition.
- 2026-05-27: Senior review found the in-memory cleanup status read model was keyed only by tenant/folder/workspace and did not preserve task/correlation-scoped snapshots independently; fixed the key shape and added regression coverage.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Story file prepared for dev-story execution.
- Story status set to ready-for-dev.
- Implemented metadata-only cleanup status query path with authorization-before-observation, fail-closed read-model outcomes, task/correlation scope validation, and no repair/provider/Git/filesystem mutation behavior.
- Added Contract Spine operation/schema/example, docs rows, generated parity fixture, generated client artifacts, core/server/contract-focused tests, and read-action catalog registration.
- Focused regression gates pass through the xUnit v3 in-process runners: core cleanup/status regression set, server cleanup/status endpoint set, OpenAPI/contract/parity set, and contract rules artifact check.
- `dotnet test` remains blocked by sandbox-local VSTest socket creation, but the same focused test assemblies were executed successfully through their in-process runners.
- Senior review fixed task/correlation scoping for the in-memory cleanup status read model and reran focused regression gates.
- Story status set to `done`.

### File List

- `_bmad-output/implementation-artifacts/4-10-surface-workspace-cleanup-status-without-repair-automation.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/tests/test-summary.md`
- `docs/contract/commit-status-contract-groups.md`
- `docs/contract/idempotency-and-parity-rules.md`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`
- `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs`
- `src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs`
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAccessAction.cs`
- `src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs`
- `src/Hexalith.Folders/Authorization/EffectivePermissionsActionCatalog.cs`
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs`
- `src/Hexalith.Folders/Queries/Folders/IWorkspaceCleanupStatusReadModel.cs`
- `src/Hexalith.Folders/Queries/Folders/InMemoryWorkspaceCleanupStatusReadModel.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceCleanupStatusQuery.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceCleanupStatusQueryHandler.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceCleanupStatusQueryResult.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceCleanupStatusQueryResultCode.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceCleanupStatusReadModelRequest.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceCleanupStatusReadModelResult.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceCleanupStatusReadModelSnapshot.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceCleanupStatusReadModelStatus.cs`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/CommitStatusContractGroupTests.cs`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/TenantFolderProviderContractGroupTests.cs`
- `tests/Hexalith.Folders.Server.Tests/WorkspaceCleanupStatusEndpointTests.cs`
- `tests/Hexalith.Folders.Testing.Tests/ContractRulesArtifactTests.cs`
- `tests/Hexalith.Folders.Tests/Authorization/WorkspaceLockActionCatalogTests.cs`
- `tests/Hexalith.Folders.Tests/Queries/Folders/WorkspaceCleanupStatusQueryHandlerTests.cs`
- `tests/fixtures/parity-contract.yaml`
- `tests/fixtures/previous-spine.yaml`

### Senior Developer Review (AI)

#### Review Date

2026-05-27

#### Outcome

Approved after automatic fix.

#### Findings

- HIGH: `InMemoryWorkspaceCleanupStatusReadModel` keyed snapshots only by managed tenant, folder ID, and workspace ID, so task/correlation-scoped cleanup snapshots for the same workspace overwrote each other. That violated the story requirement to scope by task/correlation where present and could turn a valid task query into a fail-closed unavailable result after another task wrote a snapshot. Fixed in `src/Hexalith.Folders/Queries/Folders/InMemoryWorkspaceCleanupStatusReadModel.cs` by including optional task ID and correlation ID in the snapshot key.

#### Fixes Applied

- Added exact task/correlation scoping to the in-memory cleanup-status read model key.
- Added regression coverage proving two task/correlation-scoped cleanup snapshots for the same workspace remain independently readable, and that an unscoped request does not receive a task-scoped snapshot.

#### Validation

- `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
- `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
- `dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
- `dotnet build tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
- `tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -parallel none -noLogo -class Hexalith.Folders.Tests.Queries.Folders.WorkspaceCleanupStatusQueryHandlerTests`
- `tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests -parallel none -noLogo -class Hexalith.Folders.Server.Tests.WorkspaceCleanupStatusEndpointTests`
- `tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -parallel none -noLogo -class Hexalith.Folders.Contracts.Tests.OpenApi.CommitStatusContractGroupTests -class Hexalith.Folders.Contracts.Tests.OpenApi.TenantFolderProviderContractGroupTests`
- `tests/Hexalith.Folders.Testing.Tests/bin/Debug/net10.0/Hexalith.Folders.Testing.Tests -parallel none -noLogo -class Hexalith.Folders.Testing.Tests.ContractRulesArtifactTests`
- `tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -parallel none -noLogo -class Hexalith.Folders.Tests.Queries.Folders.WorkspaceStatusQueryHandlerTests -class Hexalith.Folders.Tests.Queries.Folders.WorkspaceLockStatusProjectionTests -class Hexalith.Folders.Tests.Queries.Folders.FolderLifecycleStatusProjectionTests -class Hexalith.Folders.Tests.Queries.Folders.FolderLifecycleStatusAuthorizationGateTests -class Hexalith.Folders.Tests.Authorization.WorkspaceLockActionCatalogTests`
- `tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests -parallel none -noLogo -class Hexalith.Folders.Server.Tests.WorkspaceStatusEndpointTests`
- `git diff --check`

### Change Log

- 2026-05-27: Added read-only workspace cleanup status contract, core query/read model, REST endpoint, in-memory materialization, generated artifacts, parity/docs updates, and focused coverage; story remains in progress because VSTest execution is blocked by sandbox socket restrictions.
- 2026-05-27: Completed focused regression validation via xUnit v3 in-process runners, pinned `GetWorkspaceCleanupStatus` in previous-spine operations, and moved story to review.
- 2026-05-27: Senior review fixed task/correlation-scoped cleanup snapshot isolation, added regression coverage, reran focused gates, and moved story to done.
