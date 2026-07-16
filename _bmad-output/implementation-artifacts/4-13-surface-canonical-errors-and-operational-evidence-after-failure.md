---
discovery:
  generated_at: 2026-05-27T13:05:39+02:00
  story_key: 4-13-surface-canonical-errors-and-operational-evidence-after-failure
  loaded_inputs:
    - _bmad-output/planning-artifacts/epics.md
    - _bmad-output/planning-artifacts/prd.md
    - _bmad-output/planning-artifacts/architecture.md
    - _bmad-output/planning-artifacts/ux-design-specification.md
    - _bmad-output/project-context.md
    - Hexalith.Commons/_bmad-output/project-context.md
    - Hexalith.EventStore/_bmad-output/project-context.md
    - Hexalith.Tenants/_bmad-output/project-context.md
    - Hexalith.Memories/_bmad-output/project-context.md
    - Hexalith.FrontComposer/_bmad-output/project-context.md
    - _bmad-output/implementation-artifacts/4-12-commit-workspace-changes-with-unknown-outcome-reconciliation.md
baseline_commit: 12a2b44588ba9ad3184cdf612a88eb1cf4837ca4
---

# Story 4.13: Surface canonical errors and operational evidence after failure

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a caller using REST, SDK, CLI, or MCP,
I want failures reported through the canonical error taxonomy and workspace states,
so that final state, retry eligibility, and client action are explainable.

## Acceptance Criteria

1. Given provider readiness, repository binding, workspace preparation, lock acquisition or release, file mutation, context query, commit, cleanup-status, read-model freshness, or authorization evaluation returns a failure, when REST or `/process` produces the response/result, then the failure maps to the canonical category/code vocabulary with stable HTTP status, `retryable`, `clientAction`, and `details.visibility = metadata_only`.
2. Given a lifecycle failure has workspace/task/operation context, when the response or projection evidence is produced, then it includes final C6 state, retry eligibility, retry-after hint when known, correlation ID, operation ID where available, task ID where applicable, sanitized reason category, client action, and metadata-only supporting details.
3. Given unknown provider outcome or reconciliation-required state is reached, when a caller receives Problem Details, workspace status, provider outcome, task status, commit evidence, or reconciliation status, then no surface recommends blind retry and the client action is `wait_for_reconciliation` with final-state evidence preserved.
4. Given authorization, tenant, folder ACL, path policy, and read-model freshness failures occur, when safe denial or read-model-unavailable responses are emitted, then protected tenant/folder/workspace/provider/commit/resource existence is not revealed and the response still carries correlation/retry/action evidence.
5. Given audit and projection consumers process failure, denial, duplicate, retry, conflict, unknown-outcome, or read-model-unavailable records, when events/read models/log-safe payloads are emitted, then required evidence fields are available without changing the canonical error shape or exposing file contents, diffs, raw paths, raw branch names, raw commit messages, repository URLs, provider payloads, tokens, credentials, emails, or unauthorized-resource hints.
6. Given Story 4.13 is complete, when focused domain/server/contract tests run, then existing Story 4.2-4.12 lifecycle behavior remains green, read-only operations still reject `Idempotency-Key`, mutating operations still require mutation-envelope IDs, and no CLI, MCP, SDK convenience helper, UI page, repair automation, package upgrade, generated-client hand edit, or nested submodule initialization is introduced.

## Tasks / Subtasks

- [x] Define a single canonical lifecycle failure mapping surface. (AC: 1, 2, 4, 6)
  - [x] Audit current mappings in `FoldersDomainServiceEndpoints.SafeProblem`, `ToArchiveGatewayProblem`, `ProviderReadinessEndpoints`, `FolderAuthorizationDenialMapper`, `FolderDomainProcessor.CreateRejectionEvent`, and query endpoint `ToHttpResult` methods.
  - [x] Add or extend a shared internal mapper only if it reduces current duplication without weakening endpoint-specific safe-denial rules. Preserve the existing RFC 9457 `ProblemDetails` extension shape: `category`, `code`, `message`, `correlationId`, optional `taskId`, `retryable`, `clientAction`, and `details.visibility`.
  - [x] Map `FolderResultCode` and read-model result codes to snake_case canonical categories, including validation/auth/tenant/folder ACL/credential/provider/capability/repository/branch/lock/workspace/path/commit/read-model/duplicate/transient cases.
  - [x] Ensure known provider failure, unknown provider outcome, and reconciliation-required are distinct categories; do not collapse them into generic provider or internal errors.

- [x] Enrich REST and `/process` failure evidence without leaking sensitive metadata. (AC: 1, 2, 3, 4, 5)
  - [x] Extend Problem Details `details` for lifecycle failures with safe fields only: `finalState`, `retryReasonCode`, `operationId` when canonical, `taskId` when applicable, `reasonCategory`, `evidenceSource`, and optional advisory retry-after metadata when known.
  - [x] Keep safe denial bodies non-enumerating: authorization and not-found-to-caller failures must not echo tenant, folder, workspace, provider, commit, path, projection watermark, or protected resource IDs.
  - [x] Extend `/process` rejection payloads or rejection event special cases only through canonicalized metadata. Reuse `FolderCommandRejected`, `WorkspaceTransitionInvalidRejected`, and `DuplicateWorkspaceLockRejected`; add a new rejection record only if the existing records cannot carry required evidence safely.
  - [x] Ensure EventStore gateway exception mapping recognizes commit failures, unknown provider outcome, reconciliation-required, lock conflicts, stale/unavailable projection, provider rate limit/unavailable, and idempotency conflict using stable categories and client actions.

- [x] Surface operational evidence through existing and contract-declared status projections. (AC: 2, 3, 5)
  - [x] Preserve existing `GetWorkspaceStatus` and `GetWorkspaceCleanupStatus` shapes and add missing safe failure evidence rather than replacing them.
  - [x] Implement runtime query/read-model support for contract-declared failure evidence endpoints only as needed for this story: `GetTaskStatus`, `GetCommitEvidence`, `GetProviderOutcome`, and `GetReconciliationStatus`.
  - [x] Store projection snapshots from durable lifecycle/commit events using metadata already available in `FolderState`, commit outcome events, reconciliation references, task/correlation/operation IDs, retry eligibility, freshness, and redaction metadata.
  - [x] Keep read models read-only and deterministic. They must not perform Git/provider/filesystem repair, reconciliation, lock release, or cleanup mutation.

- [x] Preserve canonical C6 state and retry semantics. (AC: 2, 3, 6)
  - [x] Source final/intermediate state from `FolderStateTransitions`/`FolderState` and existing projection snapshots; do not add a parallel state machine.
  - [x] For `unknown_provider_outcome` and `reconciliation_required`, set retry eligibility/client action to wait for reconciliation, not blind retry. Provider rate-limit and transient-unavailable cases may be retryable only when the outcome is known not to have performed the mutation.
  - [x] Keep `state_transition_invalid` as the default invalid C6 pair category and ensure the state is unchanged.
  - [x] Preserve idempotency replay/conflict semantics from Story 4.11 and Story 4.12; replay must not produce duplicate events, provider calls, logs, audit records, or projections.

- [x] Add metadata-only audit/projection evidence guardrails. (AC: 4, 5, 6)
  - [x] Classify failure evidence fields before they enter Problem Details, rejection events, read models, logs, traces, or test diagnostics.
  - [x] Treat file paths, branch names, commit messages, repository names/URLs, provider payloads, local paths, author metadata, emails, and commit references as sensitive unless represented by approved opaque references, digests, classifications, or redaction metadata.
  - [x] Include correlation ID and task ID where safe, and include operation ID only after canonical identifier validation.
  - [x] Ensure `Retry-After` header or body metadata, if added, is advisory and bounded; do not implement scheduler behavior in this story.

- [x] Add focused tests and contract/regression coverage. (AC: 1-6)
  - [x] Domain tests for `FolderResultCode`/provider/readiness/read-model mappings to canonical categories, retry flags, client actions, and no side effects before authorization/validation.
  - [x] Server tests for REST mutation and `/process` failures: commit failed, unknown provider outcome, reconciliation-required, lock conflict/not-owned/expired, path denial, provider unavailable/rate-limited, idempotency conflict, read-model unavailable, and safe authorization denial.
  - [x] Query endpoint tests for workspace/task/commit/provider/reconciliation evidence, including stale/unavailable read model, final state, retry eligibility, retry-after when known, and metadata-only details.
  - [x] Metadata leakage tests using `tests/fixtures/audit-leakage-corpus.json` over Problem Details, rejection event JSON, status/evidence query payloads, logs/test diagnostics touched by this story, and docs examples if updated.
  - [x] Contract tests against `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` for canonical Problem Details examples, error category allow-lists, and commit-status evidence schemas; update the Contract Spine only if runtime discovers real drift.
  - [x] Suggested gates: focused core/server/contract builds, focused xUnit in-process runner classes for new tests, `tests/Hexalith.Folders.Contracts.Tests/OpenApi/CommitStatusContractGroupTests`, metadata leakage tests, and `git diff --check`.

## Dev Notes

### Source Context

- Epic 4 owns the repository-backed workspace task lifecycle and specifically requires deterministic failure/status/idempotency/redaction behavior across prepare, lock, file mutation, context query, commit, cleanup, and status inspection. [Source: `_bmad-output/planning-artifacts/epics.md#Epic 4: Repository-Backed Workspace Task Lifecycle`]
- Story 4.13 requires failures from provider readiness, repository binding, preparation, lock, file mutation, context query, commit, cleanup-status, read-model freshness, and authorization evaluation to report final C6 state, retry eligibility, correlation/task/operation IDs, sanitized reason category, client action, and metadata-only supporting details. [Source: `_bmad-output/planning-artifacts/epics.md#Story 4.13: Surface canonical errors and operational evidence after failure`]
- PRD FR40-FR46 require stable failed/incomplete/duplicate/retried/conflicting operation reporting, idempotent lifecycle retry identity, canonical error taxonomy, canonical workspace/task states, and explainable final state/retry/evidence after lifecycle failures. [Source: `_bmad-output/planning-artifacts/prd.md#Commit, Evidence, and Idempotency`; `_bmad-output/planning-artifacts/prd.md#Error, Status, and Diagnostics Contract`]
- PRD error requirements include provider readiness failed, credential invalid, permission insufficient, provider unavailable/rate-limited, repository conflict, workspace/lock/path/commit failures, unknown provider outcome, reconciliation required, idempotency conflict, unsupported capability, read-model unavailable, and audit access denied. [Source: `_bmad-output/planning-artifacts/prd.md#Error Codes`]
- Architecture A-8 defines the canonical RFC 9457 Problem Details extension fields: `category`, `code`, `message`, `correlationId`, `retryable`, `clientAction`, and `details`. [Source: `_bmad-output/planning-artifacts/architecture.md#Architecture Decisions`]
- Architecture C6 is the source of truth for final/intermediate states and invalid transition behavior. Unlisted `(state, event)` pairs reject with `state_transition_invalid` and leave state unchanged. [Source: `_bmad-output/planning-artifacts/architecture.md#Workspace State Transition Matrix (C6 -- Enumerated)`]
- UX requires stable failure summaries with reason category, affected scope, correlation/task metadata, retry or escalation posture, last known safe state, freshness labels, and no raw log/file/provider payload dependency. This story prepares the server/read-model evidence Epic 6 will consume. [Source: `_bmad-output/planning-artifacts/ux-design-specification.md#User Needs`; `_bmad-output/planning-artifacts/ux-design-specification.md#Component Design Guidance`]

### Previous Story Intelligence

- Story 4.12 implemented runtime commit command/service/events, a commit executor side-effect boundary, REST `/commits` route, `/process` dispatch, state/projection application, and focused aggregate/service/server tests.
- Story 4.12 established `WorkspaceCommitService` ordering: authorize, validate metadata, load stream, aggregate preflight, idempotency lookup, provider capability/readiness validation, commit executor side effect, aggregate outcome mapping, append with fingerprint, append-conflict reread.
- Story 4.12 added `WorkspaceCommitSucceeded`, `WorkspaceCommitFailed`, and `WorkspaceCommitOutcomeUnknown`; use those events and `FolderState` fields as operational evidence sources instead of adding a second commit-evidence store.
- Story 4.12 senior review fixed missing provider capability readiness before commit execution. This story must not bypass readiness or turn unsupported capability into provider side effects.
- Story 4.11 hardened mutation envelopes: mutating REST routes require `Idempotency-Key`, `X-Correlation-Id`, and `X-Hexalith-Task-Id`; read-only routes must still reject `Idempotency-Key`.
- Recent commits show the active pattern is narrow, story-scoped aggregate/service/server/query test additions with no generated-client edits and no package upgrades.

### Existing Implementation State

- `FolderResultCode` already contains most lifecycle categories as enum values and serializes by name to avoid ordinal wire drift. Add enum values only when the canonical category truly has no existing representation. [Source: `src/Hexalith.Folders/Aggregates/Folder/FolderResultCode.cs`]
- `FolderResult` carries managed tenant, organization, folder, actor, correlation, task, idempotency, and events for command outcomes. Rejection helper paths already sanitize malformed command values; keep that contract. [Source: `src/Hexalith.Folders/Aggregates/Folder/FolderResult.cs`]
- `FolderCommandRejected` canonicalizes `/process` rejection identifiers and collapses unsafe command types to `unknown_command_type`. Reuse this sanitizer for any new rejection evidence. [Source: `src/Hexalith.Folders.Server/FolderCommandRejected.cs`]
- `WorkspaceTransitionInvalidRejected` and `DuplicateWorkspaceLockRejected` are existing special-case rejection records for C6 and lock conflict semantics. Extend cautiously if Story 4.13 needs more evidence. [Source: `src/Hexalith.Folders.Server/WorkspaceTransitionInvalidRejected.cs`; `src/Hexalith.Folders.Server/DuplicateWorkspaceLockRejected.cs`]
- `FoldersDomainServiceEndpoints.SafeProblem` is the main REST Problem Details helper. It currently sets `category`, `code`, `message`, `correlationId`, optional `taskId`, `retryable`, `clientAction`, and `details.visibility`. [Source: `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`]
- `ToArchiveGatewayProblem` currently contains the widest EventStore gateway reason mapping for mutating routes, including provider/readiness/workspace/lock/path/commit-adjacent categories. Story 4.13 should make this mapping complete and less drift-prone. [Source: `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`]
- `ProviderReadinessEndpoints` has a separate Problem Details mapping path. Check for category/action drift before duplicating logic. [Source: `src/Hexalith.Folders.Server/ProviderReadinessEndpoints.cs`]
- `FolderAuthorizationDenialMapper` intentionally hides protected tenant/folder identifiers while preserving category/code/correlation/task/retry/action. Do not make safe denials more descriptive by leaking resource identity. [Source: `src/Hexalith.Folders.Server/FolderAuthorizationDenialMapper.cs`; `tests/Hexalith.Folders.Server.Tests/SafeAuthorizationDenialMappingTests.cs`]
- `WorkspaceStatusQueryResult` and `WorkspaceStatusReadModelSnapshot` already model current state, accepted command state, projected state, provider outcome, retry eligibility, retry-after, freshness, projection lag, last failure category, correlation ID, and task ID. Prefer extending these over inventing another workspace status DTO. [Source: `src/Hexalith.Folders/Queries/Folders/WorkspaceStatusQueryResult.cs`; `src/Hexalith.Folders/Queries/Folders/WorkspaceStatusReadModelSnapshot.cs`]
- `InMemoryFolderRepository.SaveWorkspaceStatusSnapshot` currently maps `failed`, `inaccessible`, `unknown_provider_outcome`, and `reconciliation_required` into provider outcome and last failure category. Use this as the projection path for new evidence. [Source: `src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs`]
- The Contract Spine already declares `GetTaskStatus`, `GetCommitEvidence`, `GetProviderOutcome`, and `GetReconciliationStatus`; runtime handlers/read models for these routes are not present in `FoldersDomainServiceEndpoints` yet. [Source: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`; `docs/contract/commit-status-contract-groups.md`]
- `CommitStatusContractGroupTests` already validates commit/status operations, canonical examples, metadata-only examples, and generated contract boundaries. Keep this test as a guardrail. [Source: `tests/Hexalith.Folders.Contracts.Tests/OpenApi/CommitStatusContractGroupTests.cs`]

### Architecture Compliance

- Aggregates remain deterministic and side-effect free. Do not put HTTP status, Problem Details, provider calls, Dapr, filesystem, clocks, random values, logging, or projection writes inside aggregate handlers.
- REST and `/process` must preserve authorization-before-observation. Safe denials may expose correlation and safe retry/action evidence but must not confirm protected resource existence.
- Canonical Problem Details are public contract. Changing category names, codes, client actions, or retry flags requires corresponding OpenAPI, parity fixtures/oracle, tests, and generated artifacts as applicable.
- SDK/CLI/MCP parity matters, but this story should expose canonical server/contract evidence for adapters to consume. Do not implement CLI commands, MCP tools, or SDK convenience helpers here.
- Problem Details, rejection events, projections, logs, traces, metrics, and docs/test examples must remain metadata-only.
- Use current repo package pins and .NET 10 SDK `10.0.302`. No dependency upgrade is required.

### Project Structure Notes

- Expected core/query files are under `src/Hexalith.Folders/Aggregates/Folder/`, `src/Hexalith.Folders/Queries/Folders/`, and provider/readiness query folders.
- Expected REST and `/process` files are under `src/Hexalith.Folders.Server/`.
- Expected tests are under `tests/Hexalith.Folders.Tests/`, `tests/Hexalith.Folders.Server.Tests/`, and `tests/Hexalith.Folders.Contracts.Tests/`.
- Contract changes, if needed, belong in `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` and related docs/tests. Do not hand-edit `src/Hexalith.Folders.Client/Generated`.
- No conflicts detected with the unified project structure. The main implementation risk is introducing a second error/evidence vocabulary beside the existing `FolderResultCode`, `SafeProblem`, Contract Spine schemas, and C6 state model.

### Files To Inspect First

- `src/Hexalith.Folders/Aggregates/Folder/FolderResultCode.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderResult.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs`
- `src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceCommitService.cs`
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs`
- `src/Hexalith.Folders.Server/FolderCommandRejected.cs`
- `src/Hexalith.Folders.Server/FolderAuthorizationDenialMapper.cs`
- `src/Hexalith.Folders.Server/ProviderReadinessEndpoints.cs`
- `src/Hexalith.Folders/Queries/Folders/*WorkspaceStatus*`
- `src/Hexalith.Folders/Queries/Folders/*WorkspaceCleanupStatus*`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`
- `docs/contract/commit-status-contract-groups.md`
- `tests/Hexalith.Folders.Server.Tests/WorkspaceStatusEndpointTests.cs`
- `tests/Hexalith.Folders.Server.Tests/WorkspaceCleanupStatusEndpointTests.cs`
- `tests/Hexalith.Folders.Server.Tests/CommitWorkspaceProcessEndpointTests.cs`
- `tests/Hexalith.Folders.Server.Tests/MutationEnvelopeEndpointMatrixTests.cs`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/CommitStatusContractGroupTests.cs`

### Do Not Touch

- Do not hand-edit generated client files under `src/Hexalith.Folders.Client/Generated`.
- Do not implement CLI commands, MCP tools, SDK convenience helpers, UI pages, audit timeline UI, repair/discard automation, blind retries, provider drift workflows, release packaging, or production deployment.
- Do not add a second error taxonomy, second workspace state model, second idempotency ledger, or route-level tenant authority.
- Do not expose raw file content, diffs, local paths, repository URLs/names, raw branch names, raw commit messages, provider payloads, credentials, tokens, emails, projection watermarks in safe denials, or unauthorized-resource hints.
- Do not initialize or update nested submodules recursively.

### Latest Technical Context

- Local repository pins .NET SDK `10.0.302`, `net10.0`, C# latest, central package management, xUnit v3, Shouldly, Dapr `1.17.7`, NSwag `14.7.1`, Octokit `14.0.0`, and repository-owned package versions. Use these pins; no dependency upgrade is required. [Source: `_bmad-output/project-context.md#Technology Stack & Versions`]
- No external web research is required for this story. The work relies on local Contract Spine schemas, existing runtime mappings, C6 rules, and provider/read-model abstractions rather than latest external library behavior.

### Testing

- Prefer focused builds/tests first. Prior Story 4.12 validation used local SDK `10.0.302` via `DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH`; VSTest may hit sandbox socket permission issues, so use xUnit v3 in-process runners when needed.
- Minimum focused coverage: canonical mapper unit tests, server Problem Details tests, `/process` rejection tests, workspace/task/commit/provider/reconciliation evidence query tests, metadata leakage scans, `CommitStatusContractGroupTests`, and `git diff --check`.
- Keep tests hermetic: no provider credentials, live GitHub/Forgejo calls, Dapr sidecars, Keycloak, Redis, production secrets, network calls, or nested submodule initialization.

### Regression Traps

- Do not mark unknown provider outcome as retryable commit. The client action must wait for reconciliation.
- Do not leak protected identifiers in safe denials just to satisfy evidence requirements. Correlation/task IDs are safe only after canonical validation; operation ID is safe only when it is already an opaque/canonical operation identifier.
- Do not accept `Idempotency-Key` on read-only status/evidence endpoints.
- Do not break existing REST mutation-envelope validation; malformed headers/body must fail before EventStore gateway submission.
- Do not convert known provider failure, unsupported provider capability, provider unavailable, provider rate-limited, unknown outcome, and reconciliation-required into one generic provider failure.
- Do not expose raw branch/ref target, changed paths, raw commit reference, provider diagnostics, or raw commit message through Problem Details or evidence queries.
- Do not create operational actions from evidence endpoints. They are read-only status/evidence surfaces.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-27: Created via BMAD create-story workflow in YOLO mode with inputs from sprint status, Epic 4 story 4.13, PRD, architecture, UX specification, project context, Story 4.12, current aggregate/server/query/provider code, commit-status contract tests, and recent git history.
- 2026-05-27: Discovery loaded whole planning artifacts from `_bmad-output/planning-artifacts`; no sharded planning docs were present. Persistent facts loaded from project context files, with root `_bmad-output/project-context.md` providing the in-scope Hexalith.Folders implementation rules.
- 2026-05-27: Validation checklist applied during drafting; guardrails added for canonical error shape, C6 state preservation, metadata-only evidence, safe-denial non-enumeration, unknown-outcome no-retry behavior, and avoiding CLI/MCP/SDK/UI/generated-client scope creep.
- 2026-05-27: Dev-story execution loaded BMAD workflow/config/project contexts, captured baseline commit `12a2b44588ba9ad3184cdf612a88eb1cf4837ca4`, audited REST/provider/read-model/authorization failure surfaces, and moved sprint tracking to in-progress.
- 2026-05-27: Validation passed focused no-parallel builds plus xUnit v3 in-process runs: server workspace/evidence/canonical mapper tests (45 passed), core workspace-status query tests (33 passed), and commit-status contract group tests (6 passed).
- 2026-05-27: `dotnet test` via VSTest is blocked by sandbox socket permissions. Broader solution build was attempted and reached unrelated projects, but AppHost SDK/package vulnerability restore required blocked NuGet network access.
- 2026-05-27: Senior developer review auto-fixed reconciliation reference scoping, commit evidence digest propagation, and task-status projection validation. Validation passed focused single-worker builds, xUnit v3 in-process runs, contract group tests, and `git diff --check`.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Story file prepared for dev-story execution.
- Story status set to ready-for-dev.
- Added `FolderCanonicalErrorMapper` as the shared server-side mapping surface for `FolderResultCode` categories, statuses, retryability, and client actions.
- Enriched Problem Details metadata with metadata-only reason/evidence details while preserving the existing extension shape and safe-denial non-enumeration.
- Added runtime support for `GetTaskStatus`, `GetCommitEvidence`, `GetProviderOutcome`, and `GetReconciliationStatus` using deterministic read-model data and existing workspace status evidence.
- Changed unknown provider outcome and reconciliation-required retry eligibility to non-retryable wait-for-reconciliation semantics across status/evidence surfaces.
- Added focused core/server/contract tests and metadata-leakage assertions for status and evidence payloads.
- Senior review fixed `GetReconciliationStatus` so unknown reconciliation IDs remain safe-not-found instead of echoing caller-supplied references.
- Senior review propagated changed-path digest and commit reference classification through workspace provider outcome evidence instead of returning placeholder commit evidence.
- Senior review hardened task-status read-model snapshots so malformed lifecycle state, operation ID, canonical category, action scope, or retry-after metadata fails closed.

### File List

- `_bmad-output/implementation-artifacts/4-13-surface-canonical-errors-and-operational-evidence-after-failure.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Folders.Server/FolderAuthorizationDenialMapper.cs`
- `src/Hexalith.Folders.Server/FolderCanonicalErrorMapper.cs`
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`
- `src/Hexalith.Folders.Server/ProviderReadinessEndpoints.cs`
- `src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs`
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs`
- `src/Hexalith.Folders/Queries/Folders/ITaskStatusReadModel.cs`
- `src/Hexalith.Folders/Queries/Folders/InMemoryTaskStatusReadModel.cs`
- `src/Hexalith.Folders/Queries/Folders/TaskStatusQuery.cs`
- `src/Hexalith.Folders/Queries/Folders/TaskStatusQueryHandler.cs`
- `src/Hexalith.Folders/Queries/Folders/TaskStatusQueryResult.cs`
- `src/Hexalith.Folders/Queries/Folders/TaskStatusQueryResultCode.cs`
- `src/Hexalith.Folders/Queries/Folders/TaskStatusReadModelRequest.cs`
- `src/Hexalith.Folders/Queries/Folders/TaskStatusReadModelResult.cs`
- `src/Hexalith.Folders/Queries/Folders/TaskStatusReadModelSnapshot.cs`
- `src/Hexalith.Folders/Queries/Folders/TaskStatusReadModelStatus.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceProviderOutcome.cs`
- `tests/Hexalith.Folders.Server.Tests/FolderCanonicalErrorMapperTests.cs`
- `tests/Hexalith.Folders.Server.Tests/WorkspaceStatusEndpointTests.cs`
- `tests/Hexalith.Folders.Tests/Queries/Folders/TaskStatusQueryHandlerTests.cs`
- `tests/Hexalith.Folders.Tests/Queries/Folders/WorkspaceStatusQueryHandlerTests.cs`

### Senior Developer Review (AI)

Reviewer: GPT-5 Codex on 2026-05-27

Outcome: Approved after automatic fixes. No critical issues remain.

Findings fixed:

- HIGH: `GetReconciliationStatus` accepted any canonical `reconciliationId` once workspace status was authorized, which failed reconciliation-scope validation and could make arbitrary caller-supplied references look real. Fixed by carrying the projected reconciliation reference and returning safe `404 not_found` for mismatches.
- HIGH: `GetCommitEvidence` returned a placeholder changed-path digest instead of projected commit evidence, weakening the required operational evidence chain. Fixed by propagating changed-path digest and commit-reference classification through `WorkspaceProviderOutcome`.
- MEDIUM: `TaskStatusQueryHandler` returned available snapshots without validating the public contract shape or action/task scope. Fixed by failing closed for malformed lifecycle state, operation ID, canonical category, action mismatch, and invalid retry metadata.

Validation:

- `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -m:1 -v:m`
- `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -m:1 -v:m`
- `tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -noLogo -parallel none -class Hexalith.Folders.Tests.Queries.Folders.WorkspaceStatusQueryHandlerTests -class Hexalith.Folders.Tests.Queries.Folders.TaskStatusQueryHandlerTests` (55 passed)
- `tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests -noLogo -parallel none -class Hexalith.Folders.Server.Tests.WorkspaceStatusEndpointTests -class Hexalith.Folders.Server.Tests.FolderCanonicalErrorMapperTests` (46 passed)
- `dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore -m:1 -v:m`
- `tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -noLogo -parallel none -class Hexalith.Folders.Contracts.Tests.OpenApi.CommitStatusContractGroupTests` (6 passed)
- `git diff --check`

### Change Log

- 2026-05-27: Created Story 4.13 context package for canonical lifecycle errors, metadata-only operational evidence, final-state/retry/client-action reporting, and safe-denial guardrails.
- 2026-05-27: Implemented canonical lifecycle failure mapping, metadata-only Problem Details evidence, task/commit/provider/reconciliation status evidence endpoints, wait-for-reconciliation retry semantics, and focused regression coverage.
- 2026-05-27: Senior developer review auto-fixed reconciliation scoping, commit evidence propagation, task-status projection validation, and focused regression coverage; story moved to done.
