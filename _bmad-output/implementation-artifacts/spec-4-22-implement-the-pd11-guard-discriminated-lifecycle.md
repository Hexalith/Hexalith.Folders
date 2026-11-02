---
title: 'Story 4.22: Implement the PD11 guard-discriminated lifecycle'
type: 'feature'
created: '2026-11-02'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
story_key: '4-22-implement-the-pd11-guard-discriminated-lifecycle'
context:
  - '_bmad-output/implementation-artifacts/epic-4-context.md'
  - 'docs/exit-criteria/c6-transition-matrix-mapping.md'
  - 'docs/exit-criteria/c3-retention.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The shipped pair-oriented lifecycle accepts reserved operator events and can misclassify or lose staged work. Recovery, cleanup, and disposition diverge from approved PD11.

**Approach:** Implement approved C6 `(state, event, guard)` transitions across code, contract, UI, documentation, and tests. Preserve staged work through retry and authorization loss; separate recovery and cleanup clocks.

## Boundaries & Constraints

**Always:** Begin code after Story 1.17 acceptance and hold release; verify A7/C6 and A7b/C3 digests. Derive guards from durable server-authorized state. Unlisted triples/branches return `state_transition_invalid`, unchanged state and inspectable idempotent rejection. Preserve metadata-only evidence, tenant isolation, approved native task completion, and EventStore persistence/cleanup ownership.

**Never:** Delete on recovery expiry, lock loss, cancellation, or inaccessibility. Transfer ownership, auto-commit, clear staged work, trust caller task locators, duplicate EventStore cleanup, or publish `OperatorDiscardRequested`, `OperatorRetrySucceeded`, or `OperatorMarkedFailed`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Commit resolution | Unknown outcome; confirmed or refused | `committed` or `failed` | Missing resolution rejects |
| Commit failure | `changes_staged`; retryable with no confirmed remote effect or known non-retryable | `dirty` with staged work or `failed` | Unknown classification rejects |
| Restored readiness | `inaccessible`; staged before deadline, clean, or staged late | `dirty`, `ready`, or unchanged | Late staged branch rejects until cleanup completes |
| Resume | `dirty` + `WorkspaceLocked`; staged and same server-resolved task | `changes_staged`; cancel cleanup epoch | Other callers safely reject |
| Stale lease | `dirty` + `LockLeaseBecameStale` at inclusive C7 boundary | Clean goes `ready` and unlocked | Staged branch rejects; no other event makes an expired lease stale |
| Reconciliation budget | Unknown outcome; ≤5 read-only checks/15 minutes | `auto-recovering`; then `reconciliation_required` / `awaiting-human` | No false success |
| Cleanup | Terminal/no-active, P7D elapsed, no hold | EventStore deletes disposable copy; retains metadata proof | Resume or missing predicate blocks |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs`, `FolderWorkspaceLifecycleEvent.cs` — pair-oriented switch and vocabulary drift.
- `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs`, `FolderStateApply.cs`, `FolderAggregate.cs` — state/fold seams; reuse shared provider failure classification.
- `src/Hexalith.Folders.Server/Content/WorkspaceContentAuthority.cs` — disposition seam; legal hold currently false.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/Content/ContentStoredUpload.cs`, `DaprAuthoritativeContentStore.cs` — durable epoch; computed `NotBefore`.
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v2.yaml`, `tests/fixtures/previous-spine.yaml` — published vocabulary/C13; regenerate Client.

## Tasks & Acceptance

**Execution:**
- [ ] `_bmad-output/implementation-artifacts/sprint-status.yaml` and `_bmad-output/planning-artifacts/planning-authority-relock-approval-register.yaml` — verify accepted Story 1.17, lifted hold, and matching A7/A7b payload digests before code.
- [ ] `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs` and `FolderWorkspaceLifecycleEvent.cs` — implement approved triples and stale event; remove reserved enum values; update disposition.
- [ ] `src/Hexalith.Folders.Server/FolderDomainProcessor.cs` — reject reserved raw event identifiers without state change.
- [ ] `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs`, `FolderStateApply.cs`, `FolderAggregate.cs` and event types — fold `stagedByTaskId`, `stagedByPrincipal`, `stagedRecoveryStartedAt/Deadline`; preserve work on retry/revocation and gate same-task resume.
- [ ] `src/Hexalith.Folders.Server/Content/WorkspaceContentAuthority.cs` — supply current hold/task predicates and metadata-only cleanup proof.
- [ ] `references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/Content/ContentStoredUpload.cs`, `DaprAuthoritativeContentStore.cs` — persist `stagedCleanupStartedAt/NotBefore` for terminal/no-active epochs; cancel on resume and restart fresh, with hold rechecks.
- [ ] `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v2.yaml`, `tests/fixtures/previous-spine.yaml`, generated client and C13 inventory — publish stale event, remove reserved events via generator.
- [ ] `src/Hexalith.Folders.UI/Services/DispositionLabelMapper.cs`, `docs/diagrams/workspace-lifecycle.md`, `docs/exit-criteria/c6-transition-matrix-mapping.md` — align labels and diagram.
- [ ] `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderStateTransitionsTests.cs`, `DispositionLabelParityTests.cs`, `tests/Hexalith.Folders.UI.Tests/DispositionLabelMapperTests.cs`, contract/Server tests — cover every triple, reserved identifier, boundary, denial, deletion negative, digest and persisted end state.

**Acceptance Criteria:**
- Given approved C3/C6 digests and accepted Story 1.17, when contract, code, docs, UI and tests are compared, then they express one guarded MVP vocabulary and all eleven states.
- Given a duplicate rejected transition, when the same intent is inspected or retried, then the same sanitized rejection is returned without a state change or protected-data disclosure.
- Given restart or replay, when staged work and both clocks are reconstructed, then task binding, retention eligibility and metadata evidence match durable events.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `dotnet build Hexalith.Folders.slnx -c Debug` — local source graph builds.
- `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj -c Debug` — then run built xUnit v3 assemblies with `-class`; repeat for UI, Contracts and Server projects.
- `pwsh -File tests/tools/run-contract-spine-gates.ps1` and `pwsh -File tests/tools/run-retention-deletion-gates.ps1` — conformance passes.
- `git diff --check` — no whitespace errors.
