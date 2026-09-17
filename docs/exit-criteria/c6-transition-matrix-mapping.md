# C6 Transition Matrix Mapping

status: approved architecture mapping plan; implementation deferred to Phase 2 aggregate story
decision owner: Architect
approval authority: Architecture team
source inputs: Architecture Workspace State Transition Matrix (C6 — Enumerated), Exit Criteria Operations Plan, error catalog, operations-console disposition model
last reviewed: 2026-05-11
open questions: Phase 2 implementation may discover edge cases, but any vocabulary change must update architecture and aggregate tests in the same change.

## Decision

The source of truth is `_bmad-output/planning-artifacts/architecture.md#Workspace State Transition Matrix (C6 — Enumerated)`. Future implementation must translate that matrix 1:1 into `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs`. This story documents the mapping only and does not create the target source file or aggregate tests.

The matrix is keyed on `(state, event, guard)`, not on `(state, event)`. PD11 (2026-09-15) makes four pairs guard-discriminated, so their outcome is a function of the guard and never of the pair alone; a pair-keyed reading lets an implementation handle one branch, silently take the other, and still satisfy "this pair has a defined outcome" while destroying staged work.

Every unlisted `(state, event, guard)` triple rejects with canonical category `state_transition_invalid`, leaves state unchanged, maps CLI exit code 74, maps MCP failure kind `state_transition_invalid`, and remains inspectable through idempotency record behavior. For a guard-discriminated pair, a guard branch that is not enumerated rejects under that same rule and is never silently routed to the sibling branch's outcome.

### Guard Discriminators (PD11 — approval-pending under A7b)

Guards are evaluated server-side from durable state, never from caller input (architecture S-8). The retryable/non-retryable classification is owned by the shared provider-outcome classifier so the GitHub and Forgejo adapters cannot disagree about the same failure.

| Guard-discriminated pair | Guard | Outcome per branch | Durable field read |
|---|---|---|---|
| `changes_staged` + `CommitFailed` | Retryable with no confirmed remote effect *vs* known non-retryable | `dirty` (staged changes preserved) *vs* `failed` | Shared provider-outcome classification |
| `inaccessible` + `ProviderReadinessValidated` | Staged content still inside the C3 window *vs* none | `dirty` *vs* `ready` | Staged-content presence |
| `dirty` + `WorkspaceLocked` | Staged changes still present **AND** `stagedByTaskId` equals the server-resolved task of the re-acquiring command — both conjuncts *vs* any other case | `changes_staged` (resume without re-staging) *vs* rejected | `stagedByTaskId` plus staged-content presence — never the `X-Hexalith-Task-Id` request header |
| `dirty` + `LockLeaseBecameStale` | Workspace clean *vs* holds staged changes | `ready` *vs* **rejected** — staged work is never discarded by a lock timer | Staged-content presence |

`stagedByTaskId` is declared by the architecture as the durable field this guard reads; `stagedByPrincipal` is audit evidence and is read by no transition. Neither exists in `src/` today and both are part of the owning PD11 story. The guard predicate above is stated identically in `architecture.md`; a divergence between the two is a defect in whichever was edited last, never an alternative reading.

### State Catalog

| State | Operator disposition | Provenance | Approval state | Consuming future artifact | Review date |
|---|---|---|---|---|---|
| `requested` | `auto-recovering` | Architecture C6 state catalog | approved | Story 4.1 `FolderStateTransitions.cs` and Story 6.3 disposition labels | 2026-05-11 |
| `preparing` | `auto-recovering` | Architecture C6 state catalog | approved | Story 4.1 `FolderStateTransitions.cs` and Story 6.3 disposition labels | 2026-05-11 |
| `ready` | available, or `degraded-but-serving` when projection lag exceeds C2 | Architecture C6 state catalog | approved | Story 4.1 `FolderStateTransitions.cs` and Story 6.3 disposition labels | 2026-05-11 |
| `locked` | `degraded-but-serving` | Architecture C6 state catalog | approved | Story 4.1 `FolderStateTransitions.cs` and Story 6.3 disposition labels | 2026-05-11 |
| `changes_staged` | `degraded-but-serving` | Architecture C6 state catalog | approved | Story 4.1 `FolderStateTransitions.cs` and Story 6.3 disposition labels | 2026-05-11 |
| `dirty` | `degraded-but-serving` while the originating task can resume or the workspace is clean, `awaiting-human` once staged changes are orphaned | Architecture C6 state catalog | approved | Story 4.1 `FolderStateTransitions.cs` and Story 6.3 disposition labels | 2026-09-15 |
| `committed` | `auto-recovering` | Architecture C6 state catalog | approved | Story 4.1 `FolderStateTransitions.cs` and Story 6.3 disposition labels | 2026-05-11 |
| `failed` | `terminal-until-intervention` | Architecture C6 state catalog | approved | Story 4.1 `FolderStateTransitions.cs` and Story 6.3 disposition labels | 2026-05-11 |
| `inaccessible` | `terminal-until-intervention` | Architecture C6 state catalog | approved | Story 4.1 `FolderStateTransitions.cs` and Story 6.3 disposition labels | 2026-05-11 |
| `unknown_provider_outcome` | `awaiting-human` | Architecture C6 state catalog | approved | Story 4.1 `FolderStateTransitions.cs` and Story 6.3 disposition labels | 2026-05-11 |
| `reconciliation_required` | `awaiting-human` | Architecture C6 state catalog | approved | Story 4.1 `FolderStateTransitions.cs` and Story 6.3 disposition labels | 2026-05-11 |

### Event Vocabulary

The architecture event vocabulary copied for drift checking is:

`RepositoryBindingRequested`, `RepositoryBound`, `RepositoryBindingFailed`, `ProviderOutcomeUnknown`, `WorkspacePrepared`, `WorkspacePreparationFailed`, `WorkspaceLocked`, `AuthRevocationDetected`, `TenantRevoked`, `RepositoryDeletedAtProvider`, `ReconciliationRequested`, `FileMutated`, `WorkspaceLockReleased`, `LockLeaseExpired`, `LockLeaseBecameStale`, `CommitSucceeded`, `CommitFailed`, `OperatorDiscardRequested`, `OperatorRetrySucceeded`, `ProviderReadinessValidated`, `ReconciliationCompletedClean`, `ReconciliationCompletedDirty`, `ReconciliationEscalated`, `OperatorMarkedFailed`.

| Mapping area | Rule | Provenance | Approval state | Consuming future artifact | Review date |
|---|---|---|---|---|---|
| Positive transitions | Implement every Architecture C6 listed `(from, event, guard) -> to` row as a total switch expression or equivalent total mapping, with the guard as a first-class key | Architecture C6 valid transitions | approved; guard key approval-pending under A7b | Story 4.1 `FolderStateTransitions.cs` and aggregate tests | 2026-05-11 |
| Default rejection | Reject every unlisted `(state, event, guard)` triple with `state_transition_invalid`; state remains unchanged; an unenumerated guard branch rejects rather than falling through to its sibling | Architecture C6 default rejection rule | approved; guard branch rule approval-pending under A7b | Story 4.1 aggregate tests and Story 5 CLI/MCP parity | 2026-05-11 |
| Operator disposition | Source labels from the C6 state catalog; UI mapping must be generated from or tested against this catalog | Architecture C6 and F-4 operations-console model | approved | Story 6.3 `OperatorDispositionBadge` mapping | 2026-05-11 |
| Idempotency inspection | Persist rejection/result visibility through idempotency behavior so duplicate requests return the same logical result | Architecture C6 and A-9 idempotency record behavior | approved | Story 4.11 idempotency propagation and Story 4.12 commit reconciliation | 2026-05-11 |
| Aggregate coverage | Every state, every event, and every guard branch requires either positive transition coverage or explicit rejection coverage; a pair-keyed gate is insufficient because it passes a one-branch implementation of a guard-discriminated pair | Architecture C6 implementation enforcement | approved; guard branch coverage approval-pending under A7b | Story 4.1 aggregate test suite and future CI gate | 2026-05-11 |

## Rationale

C6 is already enumerated in architecture. Repeating the vocabulary here gives Phase 2 implementers a small drift checkpoint before they write aggregate code, while keeping the architecture document as the source of truth.

The default rejection rule is as important as positive transitions because invalid workspace operations must fail closed without changing state or hiding the result from idempotency inspection.

## Verification impact

Verification must prove this document includes the full 11-state catalog, the event vocabulary, the four PD11 guard discriminators, the future implementation path, the canonical rejection category, operator-disposition mapping, and aggregate-test expectations. Later implementation must add matrix coverage that fails when a state, an event, or a guard branch is added without a documented transition or explicit rejection.

## Deferred implementation

This document does not create or modify `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs`, aggregate classes, aggregate tests, UI disposition code, CLI/MCP adapters, OpenAPI files, worker behavior, or CI workflow gates.
