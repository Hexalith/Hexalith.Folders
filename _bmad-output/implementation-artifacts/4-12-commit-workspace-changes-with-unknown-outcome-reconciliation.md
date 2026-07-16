---
baseline_commit: 33cb7abccc023c1e406a3a6c528c501248b3e706
---

# Story 4.12: Commit workspace changes with unknown-outcome reconciliation

Status: done

## Story

As a developer or AI agent,
I want to commit workspace changes with task, actor, author, branch/ref, commit message, changed-path, operation, and correlation metadata,
so that repository-backed work reaches a clean committed state or an inspectable failure state.

## Acceptance Criteria

1. Given changes are staged and the caller owns the active workspace lock, when `CommitWorkspace` is submitted through REST or `/process`, then request envelope validation requires `Idempotency-Key`, `X-Correlation-Id`, and `X-Hexalith-Task-Id`, rejects malformed route/body metadata before gateway submission or domain processing, and preserves the Story 4.11 replay/conflict semantics for equivalent and non-equivalent payloads.
2. Given the folder is repository-backed, bound, prepared, in `changes_staged`, and the lock holder task matches the commit task, when commit validation runs, then the command accepts only metadata-safe `operation_id`, `workspace_id`, `author_metadata_reference`, `branch_ref_target`, `commit_message_classification`, and `changed_path_metadata_digest` values and rejects missing lock, expired lock, wrong task, non-staged state, wrong workspace, unavailable idempotency ledger, and unsupported capability without provider/Git side effects.
3. Given a commit operation completes with a known successful provider/working-copy outcome, when the outcome is recorded, then durable events record commit reference and metadata-only evidence, the workspace transitions `changes_staged -> committed`, workspace status exposes committed state/provider outcome/operation metadata, and no raw file contents, diffs, local paths, raw commit messages, branch names, repository URLs, provider payloads, tokens, or credential material are emitted.
4. Given the provider or working-copy outcome is known failed, when the outcome is recorded, then durable events transition `changes_staged -> failed`, map the stable category to canonical commit/provider failure evidence, preserve correlation/task/operation metadata, and do not retry or duplicate commits behind the caller's back.
5. Given the provider response is ambiguous, timed out after mutation started, cancelled during mutation, malformed after mutation, or otherwise cannot prove whether a commit was created, when the outcome is recorded, then the workspace transitions `changes_staged -> unknown_provider_outcome`, reconciliation is scheduled/represented by metadata-only evidence, the result maps to `unknown_provider_outcome` or `reconciliation_required`, and no silent retry is attempted.
6. Given the same commit idempotency key is replayed with equivalent payload, when observed through REST, `/process`, service orchestration, aggregate handling, worker outcome handling, or append-conflict resolution, then the same logical result is returned without a duplicate commit, provider call, Git write, lifecycle event, projection write, audit/log/trace record, or reconciliation task.
7. Given the same commit idempotency key is reused with a non-equivalent payload, when validation or the durable ledger runs, then the command returns canonical `idempotency_conflict`/`FolderResultCode.IdempotencyConflict`, emits no state-changing event, and touches no provider, Git, working-copy, commit evidence, or reconciliation path.
8. Given Story 4.12 is complete, when focused aggregate/service/server/contract/workers tests run, then existing Story 4.2-4.11 behavior remains green, read-only status/context operations still reject `Idempotency-Key`, and no CLI, MCP, SDK convenience helper, UI mutation, repair automation, package upgrade, or nested submodule initialization is introduced.

## Tasks / Subtasks

- [x] Add the runtime commit contract types and endpoint wiring. (AC: 1, 2, 8)
  - [x] Add `CommitWorkspace` and `WorkspaceCommitRequest` domain/service records under `src/Hexalith.Folders/Aggregates/Folder/` following the existing `PrepareWorkspace`, `LockWorkspace`, and `MutateWorkspaceFile` shape.
  - [x] Add `FoldersServerModule.CommitWorkspaceCommandType` with the contract command type name, route mapping for `POST /api/v1/folders/{folderId}/workspaces/{workspaceId}/commits`, and a `CommitWorkspacePayload` in `FolderDomainProcessor`.
  - [x] Use the existing shared mutation-envelope validation in `FoldersDomainServiceEndpoints`; add the commit route to `MutationEnvelopeEndpointMatrixTests`.
  - [x] Preserve route-authoritative `folderId` and `workspaceId`; tenant and actor authority must come from authenticated/EventStore context, not request body fields.

- [x] Implement `CommitWorkspaceService` with the existing mutation ordering. (AC: 1, 2, 6, 7)
  - [x] Authorize with `LayeredFolderAuthorizationService` using action token `commit` or the established mapped commit token; fail before repository load on tenant/folder authorization denial.
  - [x] Validate command metadata with `FolderCommandValidator` before side effects. Add the commit fingerprint fields exactly as the Contract Spine declares: `author_metadata_reference`, `branch_ref_target`, `changed_path_metadata_digest`, `commit_message_classification`, `operation_id`, `task_id`, `workspace_id`.
  - [x] Load the folder stream and require repository bound, matching workspace, `changes_staged` lifecycle state, non-expired lock, and matching `WorkspaceLockHolderTaskId == TaskId`.
  - [x] Check `TryGetIdempotencyFingerprint` before provider/working-copy commit execution. Replay returns before duplicate side effects; conflict returns before side effects.
  - [x] Use the same append outcome handling pattern as `WorkspaceFileMutationService`: appended, fingerprint matched, fingerprint conflict, append conflict re-read, and idempotency unavailable.

- [x] Add commit outcome events and state application. (AC: 3, 4, 5)
  - [x] Add metadata-only events for commit success and commit known failure, or extend `FolderWorkspaceLifecycleEventRecorded` only if it can carry commit reference/evidence without weakening type safety.
  - [x] On success, transition with `FolderWorkspaceLifecycleEvent.CommitSucceeded` and record commit reference, operation ID, correlation ID, task ID, author metadata reference, branch/ref policy or target classification, commit message classification, changed-path metadata digest, and safe provider outcome category.
  - [x] On known failure, transition with `FolderWorkspaceLifecycleEvent.CommitFailed` and stable failure category; do not record raw provider payloads or raw commit message text.
  - [x] On ambiguous outcome, transition with `FolderWorkspaceLifecycleEvent.ProviderOutcomeUnknown` from `changes_staged`; mark reconciliation required/scheduled in metadata and do not retry the commit.
  - [x] Extend `FolderState`, `FolderStateApply`, `InMemoryFolderRepository` workspace status/cleanup snapshots, and any status DTOs only as needed to expose committed/failed/unknown provider outcome evidence.

- [x] Add a side-effect boundary for commit execution and reconciliation scheduling. (AC: 3, 4, 5, 6)
  - [x] Keep aggregate handlers pure. Do not put Git, provider, Dapr, filesystem, clocks, random values, or secret-store calls inside `FolderAggregate`.
  - [x] Prefer a port/service boundary analogous to file content/delete staging and repository provisioning process managers. The implementation may be a worker process manager or a commit executor abstraction, but provider/Git side effects must be isolated and testable.
  - [x] Add safe result types for success, known failure, unknown outcome, and reconciliation required. Map provider categories using existing `ProviderFailureCategory` and `ProviderFailureCategoryExtensions`.
  - [x] Schedule/represent reconciliation with deterministic metadata derived from commit operation/correlation/task identity. Reconciliation records must be idempotent by causation/correlation/operation identity.

- [x] Wire `/process` and domain result mapping. (AC: 1, 3, 4, 5)
  - [x] Add commit command dispatch to `FolderDomainProcessor.ProcessAsync` after file mutation and before unsupported-command rejection.
  - [x] Deserialize `CommitWorkspacePayload` with `UnmappedMemberHandling.Disallow`; reject malformed JSON and missing required body values as metadata-only validation errors.
  - [x] Pass sanitized task ID from the `/process` extension into the service request; do not downgrade missing/unsafe task IDs to an empty string.
  - [x] Extend `CreateRejectionEvent` special cases if commit needs canonical `lock_not_owned`, `lock_expired`, `state_transition_invalid`, `unknown_provider_outcome`, or `reconciliation_required` evidence beyond generic `FolderCommandRejected`.
  - [x] Ensure accepted/no-op replay payloads expose safe replay evidence and do not require duplicate events.

- [x] Preserve Contract Spine and generated-output boundaries. (AC: 1, 2, 8)
  - [x] Verify `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` already declares `CommitWorkspace` with commit-tier TTL and equivalence fields; update only if runtime discovers real contract drift.
  - [x] Extend `CommitStatusContractGroupTests` only for actual new runtime/contract alignment checks. Do not hand-edit generated client files under `src/Hexalith.Folders.Client/Generated`.
  - [x] Keep commit-tier idempotency at `commit` / retention-period(C3); do not introduce free-form TTL knobs.

- [x] Add focused tests and regression gates. (AC: 1-8)
  - [x] Aggregate tests for commit success, known failure, unknown outcome, wrong state, wrong workspace, missing/expired/wrong lock, replay, conflict, metadata-only event shape, and C6 transition coverage.
  - [x] Service tests proving authorization denial, validation denial, idempotency replay/conflict/unavailable, append conflict, provider success, known failure, unknown outcome, and reconciliation scheduling ordering.
  - [x] Server endpoint tests proving the commit route rejects missing/malformed mutation headers before gateway submission and `/process` rejects malformed commit payload/envelope before domain mutation.
  - [x] Worker/process-manager tests if a worker boundary is added, mirroring `RepositoryProvisioningProcessManager` result mapping and append idempotency.
  - [x] Metadata leakage tests using `tests/fixtures/audit-leakage-corpus.json` for success payloads, Problem Details, events, projection/status payloads, logs/test diagnostics, and docs examples touched by this story.
  - [x] Suggested gates: focused `dotnet build` for core/server/workers/tests, focused xUnit runner classes, `tests/Hexalith.Folders.Contracts.Tests/OpenApi/CommitStatusContractGroupTests`, and `git diff --check`.

## Dev Notes

### Source Context

- Epic 4 owns repository-backed workspace task lifecycle, including prepare, lock, file mutation, context query, commit, status, idempotency, failure visibility, and redaction. [Source: `_bmad-output/planning-artifacts/epics.md#Epic 4: Repository-Backed Workspace Task Lifecycle`]
- Story 4.12 requires committing staged workspace changes with task/actor/author/branch/ref/commit-message/changed-path/operation/correlation metadata and handling success or inspectable failure states. [Source: `_bmad-output/planning-artifacts/epics.md#Story 4.12: Commit workspace changes with unknown-outcome reconciliation`]
- PRD FR37-FR42 require commit, task/commit evidence, failed/incomplete/duplicate/conflicting operation status, idempotent lifecycle retries, and duplicate logical operation rejection. [Source: `_bmad-output/planning-artifacts/prd.md#Commit, Evidence, and Idempotency`]
- Architecture C6 defines the allowed commit transitions: `changes_staged -> committed` on `CommitSucceeded`, `changes_staged -> failed` on `CommitFailed`, and `changes_staged -> unknown_provider_outcome` on `ProviderOutcomeUnknown during commit`. [Source: `_bmad-output/planning-artifacts/architecture.md#Workspace State Transition Matrix (C6 -- Enumerated)`]
- Unknown provider outcomes must enter `unknown_provider_outcome` or `reconciliation_required`; silent retry is forbidden because it can duplicate repositories, file mutations, commits, audits, or idempotency records. [Source: `_bmad-output/project-context.md#Critical Don't-Miss Rules`; `_bmad-output/planning-artifacts/architecture.md#Failure handling`]
- Commit Workspace is already declared in the Contract Spine with commit-tier idempotency and equivalence fields. Runtime behavior belongs to Epic 4 and is not generated-client, CLI, MCP, or UI work. [Source: `docs/contract/commit-status-contract-groups.md#Deferred Owners`; `tests/Hexalith.Folders.Contracts.Tests/OpenApi/CommitStatusContractGroupTests.cs`]

### Previous Story Intelligence

- Story 4.11 hardened mutation envelopes across REST and `/process`: mutating routes require `Idempotency-Key`, `X-Correlation-Id`, and `X-Hexalith-Task-Id`; `/process` rejects malformed message/correlation/aggregate/user/task metadata before processor mutation.
- Story 4.11 fixed replay evidence loss by preserving no-op result payloads through the EventStore wire path. Commit replay must keep that behavior instead of producing duplicate events or losing replay evidence.
- Story 4.11 explicitly deferred runtime commit execution to Story 4.12 while allowing commit idempotency metadata guardrails. This story should now implement runtime commit behavior, not just contract assertions.
- Stories 4.2-4.7 established the service ordering to preserve: authorize, validate, load stream, aggregate guard, idempotency lookup, side-effect boundary, append with fingerprint, append-conflict re-read.
- Stories 4.9 and 4.10 made workspace status/cleanup task/correlation scope meaningful. Commit status updates must keep task/correlation/operation metadata coherent rather than best-effort echoes.

### Existing Implementation State

- `FolderStateTransitions` already contains commit and reconciliation lifecycle events and the required transitions. Use it; do not add a parallel state machine. [Source: `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs`]
- `FolderStateApply` currently handles repository binding, workspace preparation, lock acquisition/release, file mutation, and generic lifecycle events, but it does not yet carry commit reference fields. Extend state/application deliberately. [Source: `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs`; `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs`]
- `WorkspaceFileMutationService` is the closest command-service pattern for held-lock mutations: it validates before side effects, performs idempotency lookup before staging, and appends through `AppendIfFingerprintAbsent`. Mirror the ordering. [Source: `src/Hexalith.Folders/Aggregates/Folder/WorkspaceFileMutationService.cs`]
- `WorkspacePreparationService.AppendUnknownWorkspaceOutcome` already records provider unknown outcome through `FolderWorkspaceLifecycleEventRecorded`; commit can reuse the concept but needs commit-specific evidence and operation identity. [Source: `src/Hexalith.Folders/Aggregates/Folder/WorkspacePreparationService.cs`]
- `InMemoryFolderRepository` updates workspace status and cleanup snapshots from `FolderState`. Commit success/failure/unknown outcome must flow through this projection path so read-your-writes status is useful. [Source: `src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs`]
- `RepositoryProvisioningProcessManager` is the existing worker/process-manager pattern for external provider outcomes and idempotent outcome appends. Use it as the pattern if this story adds a commit worker boundary. [Source: `src/Hexalith.Folders.Workers/RepositoryProvisioning/RepositoryProvisioningProcessManager.cs`]
- Provider abstractions currently expose repository creation/binding and capability/readiness evidence, not a commit port. Add a commit-specific port/result shape only if needed; do not overload repository readiness abstractions to perform commits. [Source: `src/Hexalith.Folders/Providers/Abstractions/IGitProvider.cs`; `src/Hexalith.Folders/Providers/Abstractions/ProviderOperationCatalog.cs`]

### Architecture Compliance

- Aggregates must remain deterministic and side-effect free. All provider/Git/working-copy/reconciliation side effects belong in services, ports, or worker process managers.
- Tenant/principal authority is contextual: authenticated context and EventStore claim-transform evidence are authoritative; request body/header tenant and principal values are comparison evidence only.
- Events, projections, problem details, logs, traces, metrics, tests, and docs must stay metadata-only. Treat branch names, repository names, commit messages, changed paths, provider diagnostics, local paths, and commit references as sensitive unless explicitly classified.
- The C6 matrix is canonical. Every added event/state pair requires corresponding positive or explicit `state_transition_invalid` tests.
- Commit idempotency TTL is the commit tier tied to C3 retention. Do not add per-command TTL options or a second ledger key shape.

### Files To Touch

- `src/Hexalith.Folders/Aggregates/Folder/CommitWorkspace.cs` and `WorkspaceCommitRequest.cs` (new) for domain/service input.
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceCommitService.cs` (new) for authorization, validation, idempotency, outcome mapping, and append ordering.
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceCommitSucceeded.cs` / `WorkspaceCommitFailed.cs` or a carefully extended lifecycle event record for commit outcome evidence.
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs`, `FolderAggregate.cs`, `FolderState.cs`, `FolderStateApply.cs`, `FolderResultCode.cs`, and `FolderActiveMutationGuard.cs` as needed for commit validation, state updates, and public result codes.
- `src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs` and `src/Hexalith.Folders/Queries/Folders/*Workspace*` only as needed to expose committed/failed/unknown outcome status evidence.
- `src/Hexalith.Folders.Server/FoldersServerModule.cs`, `FoldersDomainServiceEndpoints.cs`, and `FolderDomainProcessor.cs` for route, command type, payload, and `/process` dispatch.
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs` for service/port registration if a new commit service or default commit executor is introduced.
- `src/Hexalith.Folders.Workers/` and `tests/Hexalith.Folders.Workers.Tests/` if the commit outcome/reconciliation boundary is implemented as a worker process manager.
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/*Commit*`, `tests/Hexalith.Folders.Server.Tests/*`, `tests/Hexalith.Folders.Contracts.Tests/OpenApi/CommitStatusContractGroupTests.cs`, and metadata leakage tests for focused coverage.

### Do Not Touch

- Do not hand-edit generated client files under `src/Hexalith.Folders.Client/Generated`.
- Do not implement CLI commands, MCP tools, SDK convenience helpers, UI pages, audit timeline UI, repair automation, discard automation, scheduled blind retries, live provider drift workflows, or release packaging in this story.
- Do not add a second idempotency ledger, a tenant-controlled OpenAPI equivalence field, or a non-stream-scoped idempotency key lookup.
- Do not expose raw file content, diffs, local paths, repository URLs, raw branch names, raw commit messages, provider payloads, credentials, tokens, emails, or unauthorized-resource hints.
- Do not initialize or update nested submodules recursively.

### Latest Technical Context

- Local repository pins .NET SDK `10.0.300`, `net10.0`, C# latest, central package management, xUnit v3, Shouldly, Dapr `1.17.7`, NSwag `14.7.1`, Octokit `14.0.0`, and repository-owned package versions. Use these pins; no dependency upgrade is required for this story. [Source: `_bmad-output/project-context.md#Technology Stack & Versions`]
- OpenAPI commit status contracts and tests are already present. Treat them as the source for operation IDs, routes, idempotency equivalence, error categories, and metadata-only examples. [Source: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`; `tests/Hexalith.Folders.Contracts.Tests/OpenApi/CommitStatusContractGroupTests.cs`]
- No external web research is required for implementation because this story relies on local contracts, state-machine rules, and provider ports rather than a library/API upgrade.

### Testing

- Prefer focused builds/tests first. Broad `dotnet test` may hit known sandbox/VSTest socket issues; use the repo's xUnit v3 in-process runners when needed.
- Minimum focused coverage: `FolderStateTransitionsTests`, new commit aggregate/service tests, workspace status projection tests, mutation envelope matrix tests including commit route, `/process` commit payload tests, commit contract tests, worker process-manager tests if added, metadata leakage scans, and `git diff --check`.
- Keep tests hermetic: no provider credentials, live GitHub/Forgejo calls, Dapr sidecars, Keycloak, Redis, production secrets, network calls, or nested submodule initialization.

### Regression Traps

- Do not accept commit from `locked`, `ready`, `dirty`, `failed`, `unknown_provider_outcome`, or `reconciliation_required`; commit requires `changes_staged`.
- Do not release or clear the lock silently on known failure or unknown outcome. Follow C6 and make the final/intermediate state inspectable.
- Do not retry ambiguous commit outcomes. Record unknown outcome and schedule/represent reconciliation.
- Do not let replay run provider/Git/working-copy commit code. The idempotency ledger must short-circuit before side effects.
- Do not collapse `unknown_provider_outcome` or `reconciliation_required` into generic provider failure or internal error.
- Do not break read-only query behavior by accepting `Idempotency-Key` on status, cleanup, context, branch/ref, lifecycle, provider readiness, or effective-permissions reads.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-27: Created via BMAD create-story workflow in YOLO mode with inputs from sprint status, Epic 4 story 4.12, PRD, architecture, UX specification, project context, Story 4.11, current aggregate/server/worker/provider code, commit contract tests, and recent git history.
- 2026-05-27: Discovery loaded whole planning artifacts from `_bmad-output/planning-artifacts`; no sharded planning docs were present. Persistent facts loaded from project context files, with root `_bmad-output/project-context.md` providing the in-scope Hexalith.Folders implementation rules.
- 2026-05-27: Validation checklist applied during drafting; critical guardrails added for unknown-outcome no-retry behavior, commit-tier idempotency, metadata-only evidence, and avoiding CLI/MCP/SDK/UI/generated-client scope creep.
- 2026-05-27: Dev-story implementation added runtime commit command/service/events, commit executor side-effect port, REST `/commits` route, `/process` dispatch, state/projection application, and focused aggregate/service/envelope tests.
- 2026-05-27: `dotnet build Hexalith.Folders.slnx --no-restore` could not run because the environment has .NET SDK `10.0.108` while `global.json` requires `10.0.300`; `git diff --check` passed.
- 2026-05-27: Added focused REST commit malformed-body coverage before gateway submission and tightened service success coverage for safe executor metadata. Focused `dotnet test` commands for aggregate/service/server coverage could not start because SDK resolution still requires .NET SDK `10.0.300`; `git diff --check` passed.
- 2026-05-27: Re-ran BMAD dev-story validation gates at 2026-05-27T12:16:56+02:00. `dotnet build Hexalith.Folders.slnx --no-restore`, focused core commit tests, focused server commit/envelope tests, `CommitStatusContractGroupTests`, and full `dotnet test Hexalith.Folders.slnx --no-restore` all stopped before execution because SDK resolution requires .NET SDK `10.0.300` and only `10.0.108` is installed. `git diff --check` passed.
- 2026-05-27: Re-ran Story 4.12 validation gates at 2026-05-27T12:21:40+02:00. `dotnet build Hexalith.Folders.slnx --no-restore`, focused core commit tests, focused server commit/envelope tests, `CommitStatusContractGroupTests`, and full `dotnet test Hexalith.Folders.slnx --no-restore` all stopped before build/test execution because SDK resolution requires .NET SDK `10.0.300` and only `10.0.108` is installed. `git diff --check` passed.
- 2026-05-27: Parent recovery validation at 2026-05-27T12:27:55+02:00 used `DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH`, which exposes SDK `10.0.300`. Core/server/contract focused builds passed, commit aggregate/service tests passed (18), commit REST/process tests passed (14), commit OpenAPI contract tests passed (6), and `git diff --check` passed.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Story file prepared for dev-story execution.
- Story status set to ready-for-dev.
- Runtime commit implementation is in progress: core command, validation, service ordering, outcome events, state application, commit executor boundary, REST route, and `/process` wiring are implemented.
- Focused tests were added for aggregate outcome mapping, service idempotency/side-effect ordering, and REST mutation-envelope coverage; full regression gates are blocked until SDK `10.0.300` is available.
- Added commit REST malformed-body coverage that rejects unknown raw metadata before gateway submission and asserts the Problem Details payload does not leak sentinel content.
- Tightened service success coverage to assert safe metadata passed to the commit executor and replay short-circuits without a second executor request.
- Parent recovery fixed the archive accepted-response regression and projection timestamp test setup, then completed focused validation gates with the local SDK `10.0.300`; story is ready for code review.
- Senior review fixed the missing commit capability-readiness gate so unsupported provider capability is rejected before commit executor/provider side effects, then re-ran focused build and xUnit in-process validation.

### File List

- `_bmad-output/implementation-artifacts/4-12-commit-workspace-changes-with-unknown-outcome-reconciliation.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Folders/Aggregates/Folder/CommitWorkspace.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderResult.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs`
- `src/Hexalith.Folders/Aggregates/Folder/IWorkspaceCommitExecutor.cs`
- `src/Hexalith.Folders/Aggregates/Folder/IWorkspaceCommitReadinessValidator.cs`
- `src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs`
- `src/Hexalith.Folders/Aggregates/Folder/ProviderReadinessWorkspaceCommitValidator.cs`
- `src/Hexalith.Folders/Aggregates/Folder/UnavailableWorkspaceCommitExecutor.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceCommitExecutionRequest.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceCommitExecutionResult.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceCommitExecutionStatus.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceCommitFailed.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceCommitOutcomeUnknown.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceCommitRequest.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceCommitService.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceCommitSucceeded.cs`
- `src/Hexalith.Folders.Server/Authorization/FolderCommandActionTokenMapper.cs`
- `src/Hexalith.Folders.Server/FolderCommandRejected.cs`
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs`
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`
- `src/Hexalith.Folders.Server/FoldersServerModule.cs`
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderReadinessValidationService.cs`
- `tests/Hexalith.Folders.Server.Tests/MutationEnvelopeEndpointMatrixTests.cs`
- `tests/Hexalith.Folders.Server.Tests/CommitWorkspaceProcessEndpointTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderWorkspaceCommitAggregateTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderWorkspaceCommitServiceTests.cs`

### Change Log

- 2026-05-27: Created Story 4.12 context package for runtime commit behavior, commit evidence, idempotent replay/conflict handling, and unknown-outcome reconciliation guardrails.
- 2026-05-27: Implemented runtime commit command path with metadata-only outcome events, commit executor boundary, idempotency ordering, REST and `/process` wiring, and focused tests; validation remains blocked by missing .NET SDK `10.0.300`.
- 2026-05-27: Added remaining focused service/server/metadata-leakage assertions; `git diff --check` passes, but .NET build/test gates remain blocked by missing SDK `10.0.300`.
- 2026-05-27: Re-attempted focused and full validation gates under BMAD dev-story; .NET gates remain blocked by missing SDK `10.0.300`, and `git diff --check` passes.
- 2026-05-27: Re-attempted Story 4.12 validation gates; .NET build/test execution remains blocked by missing SDK `10.0.300`, and `git diff --check` passes.
- 2026-05-27: Parent recovery completed focused validation using the local SDK `10.0.300` and moved story to review.
- 2026-05-27: Senior review auto-fixed missing commit provider-readiness gating, added regression coverage, passed focused builds/tests, and moved story to done.

## Senior Developer Review (AI)

Reviewer: Jerome on 2026-05-27

Outcome: Approved after auto-fix.

### Findings

- [x] [HIGH] Commit service executed the commit side-effect boundary without first validating provider commit capability. Unsupported provider capability could be recorded as a known failed commit event instead of rejecting before provider/Git side effects, contrary to AC2. Fixed by adding `IWorkspaceCommitReadinessValidator`, wiring `ProviderReadinessWorkspaceCommitValidator`, checking commit readiness before executor invocation, and adding regression coverage in `FolderWorkspaceCommitServiceTests`.
- [x] [MEDIUM] Provider readiness `CommitStatus` requests fell through to the default required-operation branch, adding repository creation as an unrelated prerequisite. Fixed `ProviderReadinessValidationService.RequiredOperations` so commit/status/readiness query capabilities use the base commit/status support requirements without adding repository creation.

### Validation

- Passed: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore -tl:false`
- Passed: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet build src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj --no-restore -tl:false -m:1`
- Passed: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -tl:false -m:1`
- Passed: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -tl:false -m:1`
- Passed: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore -tl:false -m:1`
- Passed: `dotnet tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests.dll -class Hexalith.Folders.Tests.Aggregates.Folder.FolderWorkspaceCommitAggregateTests -class Hexalith.Folders.Tests.Aggregates.Folder.FolderWorkspaceCommitServiceTests` (21 tests)
- Passed: `dotnet tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests.dll -class Hexalith.Folders.Server.Tests.CommitWorkspaceProcessEndpointTests -class Hexalith.Folders.Server.Tests.MutationEnvelopeEndpointMatrixTests` (15 tests)
- Passed: `dotnet tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests.dll -class Hexalith.Folders.Contracts.Tests.OpenApi.CommitStatusContractGroupTests` (6 tests)
- Passed: `git diff --check`

Note: `dotnet test` via VSTest remains blocked in this sandbox by `System.Net.Sockets.SocketException (13): Permission denied`; focused tests were run through the xUnit v3 in-process runner.
