# C6 Transition Matrix Mapping

status: approved architecture mapping plan; implementation deferred to Phase 2 aggregate story
decision owner: Architect
approval authority: Architecture team
source inputs: Architecture Workspace State Transition Matrix (C6 — Enumerated), Exit Criteria Operations Plan, error catalog, operations-console disposition model
last reviewed: 2026-05-11
open questions: Phase 2 implementation may discover edge cases, but any vocabulary change must update architecture and aggregate tests in the same change.

## Decision

The source of truth is `_bmad-output/planning-artifacts/architecture.md#Workspace State Transition Matrix (C6 — Enumerated)`. Future implementation must translate that matrix 1:1 into `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs`. This story documents the mapping only and does not create the target source file or aggregate tests.

Every unlisted `(state, event)` pair rejects with canonical category `state_transition_invalid`, leaves state unchanged, maps CLI exit code 74, maps MCP failure kind `state_transition_invalid`, and remains inspectable through idempotency record behavior.

### State Catalog

| State | Operator disposition | Provenance | Approval state | Consuming future artifact | Review date |
|---|---|---|---|---|---|
| `requested` | `auto-recovering` | Architecture C6 state catalog | approved | Story 4.1 `FolderStateTransitions.cs` and Story 6.3 disposition labels | 2026-05-11 |
| `preparing` | `auto-recovering` | Architecture C6 state catalog | approved | Story 4.1 `FolderStateTransitions.cs` and Story 6.3 disposition labels | 2026-05-11 |
| `ready` | available, or `degraded-but-serving` when projection lag exceeds C2 | Architecture C6 state catalog | approved | Story 4.1 `FolderStateTransitions.cs` and Story 6.3 disposition labels | 2026-05-11 |
| `locked` | `degraded-but-serving` | Architecture C6 state catalog | approved | Story 4.1 `FolderStateTransitions.cs` and Story 6.3 disposition labels | 2026-05-11 |
| `changes_staged` | `degraded-but-serving` | Architecture C6 state catalog | approved | Story 4.1 `FolderStateTransitions.cs` and Story 6.3 disposition labels | 2026-05-11 |
| `dirty` | `awaiting-human` | Architecture C6 state catalog | approved | Story 4.1 `FolderStateTransitions.cs` and Story 6.3 disposition labels | 2026-05-11 |
| `committed` | `auto-recovering` | Architecture C6 state catalog | approved | Story 4.1 `FolderStateTransitions.cs` and Story 6.3 disposition labels | 2026-05-11 |
| `failed` | `terminal-until-intervention` | Architecture C6 state catalog | approved | Story 4.1 `FolderStateTransitions.cs` and Story 6.3 disposition labels | 2026-05-11 |
| `inaccessible` | `terminal-until-intervention` | Architecture C6 state catalog | approved | Story 4.1 `FolderStateTransitions.cs` and Story 6.3 disposition labels | 2026-05-11 |
| `unknown_provider_outcome` | `awaiting-human` | Architecture C6 state catalog | approved | Story 4.1 `FolderStateTransitions.cs` and Story 6.3 disposition labels | 2026-05-11 |
| `reconciliation_required` | `awaiting-human` | Architecture C6 state catalog | approved | Story 4.1 `FolderStateTransitions.cs` and Story 6.3 disposition labels | 2026-05-11 |

### Event Vocabulary

The architecture event vocabulary copied for drift checking is:

`RepositoryBindingRequested`, `RepositoryBound`, `RepositoryBindingFailed`, `ProviderOutcomeUnknown`, `WorkspacePrepared`, `WorkspacePreparationFailed`, `WorkspaceLocked`, `AuthRevocationDetected`, `TenantRevoked`, `RepositoryDeletedAtProvider`, `ReconciliationRequested`, `FileMutated`, `WorkspaceLockReleased`, `LockLeaseExpired`, `CommitSucceeded`, `CommitFailed`, `OperatorDiscardRequested`, `OperatorRetrySucceeded`, `ProviderReadinessValidated`, `ReconciliationCompletedClean`, `ReconciliationCompletedDirty`, `ReconciliationEscalated`, `OperatorMarkedFailed`.

| Mapping area | Rule | Provenance | Approval state | Consuming future artifact | Review date |
|---|---|---|---|---|---|
| Positive transitions | Implement every Architecture C6 listed `(from, event) -> to` row as a total switch expression or equivalent total mapping | Architecture C6 valid transitions | approved | Story 4.1 `FolderStateTransitions.cs` and aggregate tests | 2026-05-11 |
| Default rejection | Reject every unlisted pair with `state_transition_invalid`; state remains unchanged | Architecture C6 default rejection rule | approved | Story 4.1 aggregate tests and Story 5 CLI/MCP parity | 2026-05-11 |
| Operator disposition | Source labels from the C6 state catalog; UI mapping must be generated from or tested against this catalog | Architecture C6 and F-4 operations-console model | approved | Story 6.3 `OperatorDispositionBadge` mapping | 2026-05-11 |
| Idempotency inspection | Persist rejection/result visibility through idempotency behavior so duplicate requests return the same logical result | Architecture C6 and A-9 idempotency record behavior | approved | Story 4.11 idempotency propagation and Story 4.12 commit reconciliation | 2026-05-11 |
| Aggregate coverage | Every state and every event requires either positive transition coverage or explicit rejection coverage | Architecture C6 implementation enforcement | approved | Story 4.1 aggregate test suite and future CI gate | 2026-05-11 |

## Rationale

C6 is already enumerated in architecture. Repeating the vocabulary here gives Phase 2 implementers a small drift checkpoint before they write aggregate code, while keeping the architecture document as the source of truth.

The default rejection rule is as important as positive transitions because invalid workspace operations must fail closed without changing state or hiding the result from idempotency inspection.

## Verification impact

Verification must prove this document includes the full 11-state catalog, the event vocabulary, the future implementation path, the canonical rejection category, operator-disposition mapping, and aggregate-test expectations. Later implementation must add matrix coverage that fails when a state or event is added without a documented transition or explicit rejection.

## Deferred implementation

This document does not create or modify `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs`, aggregate classes, aggregate tests, UI disposition code, CLI/MCP adapters, OpenAPI files, worker behavior, or CI workflow gates.
