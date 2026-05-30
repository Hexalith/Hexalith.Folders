# Workspace Lifecycle & Lock State Machine

Status: Story 7.13 consumer reference (metadata-only).

This diagram renders the canonical **C6 workspace state machine** with **operator-disposition labels as the
primary vocabulary** (per architecture rule F-4) and the technical state name as secondary metadata. States
and events trace 1:1 to
[`docs/exit-criteria/c6-transition-matrix-mapping.md`](../exit-criteria/c6-transition-matrix-mapping.md),
whose source of truth is the architecture Workspace State Transition Matrix (C6 — Enumerated). Every unlisted
`(state, event)` pair rejects with `state_transition_invalid` (CLI exit `74`, MCP failure kind
`state_transition_invalid`) and leaves state unchanged. No spine operation appears as a state or event.

## Operator disposition per state (F-4)

| Technical state | Operator disposition |
|---|---|
| `requested` | `auto-recovering` |
| `preparing` | `auto-recovering` |
| `ready` | available, or `degraded-but-serving` when projection lag exceeds C2 |
| `locked` | `degraded-but-serving` |
| `changes_staged` | `degraded-but-serving` |
| `dirty` | `awaiting-human` |
| `committed` | `auto-recovering` |
| `failed` | `terminal-until-intervention` |
| `inaccessible` | `terminal-until-intervention` |
| `unknown_provider_outcome` | `awaiting-human` |
| `reconciliation_required` | `awaiting-human` |

## State machine

```mermaid
stateDiagram-v2
    state "auto-recovering · requested" as requested
    state "auto-recovering · preparing" as preparing
    state "degraded-but-serving · ready" as ready
    state "degraded-but-serving · locked" as locked
    state "degraded-but-serving · changes_staged" as changes_staged
    state "auto-recovering · committed" as committed
    state "awaiting-human · dirty" as dirty
    state "awaiting-human · reconciliation_required" as reconciliation_required
    state "awaiting-human · unknown_provider_outcome" as unknown_provider_outcome
    state "terminal-until-intervention · failed" as failed
    state "terminal-until-intervention · inaccessible" as inaccessible

    [*] --> requested : RepositoryBindingRequested
    requested --> preparing : RepositoryBound
    requested --> failed : RepositoryBindingFailed
    requested --> unknown_provider_outcome : ProviderOutcomeUnknown
    preparing --> ready : WorkspacePrepared
    preparing --> failed : WorkspacePreparationFailed
    preparing --> unknown_provider_outcome : ProviderOutcomeUnknown
    ready --> locked : WorkspaceLocked
    ready --> inaccessible : AuthRevocationDetected
    ready --> inaccessible : TenantRevoked
    ready --> inaccessible : RepositoryDeletedAtProvider
    ready --> reconciliation_required : ReconciliationRequested
    locked --> changes_staged : FileMutated
    locked --> ready : WorkspaceLockReleased
    locked --> dirty : LockLeaseExpired
    locked --> inaccessible : AuthRevocationDetected
    changes_staged --> changes_staged : FileMutated
    changes_staged --> committed : CommitSucceeded
    changes_staged --> failed : CommitFailed
    changes_staged --> unknown_provider_outcome : ProviderOutcomeUnknown
    changes_staged --> dirty : LockLeaseExpired
    committed --> ready : WorkspaceLockReleased
    dirty --> reconciliation_required : ReconciliationRequested
    dirty --> failed : OperatorDiscardRequested
    failed --> reconciliation_required : ReconciliationRequested
    failed --> ready : OperatorRetrySucceeded
    inaccessible --> ready : ProviderReadinessValidated
    unknown_provider_outcome --> ready : ReconciliationCompletedClean
    unknown_provider_outcome --> committed : ReconciliationCompletedDirty
    unknown_provider_outcome --> failed : ReconciliationCompletedDirty
    unknown_provider_outcome --> reconciliation_required : ReconciliationEscalated
    reconciliation_required --> ready : ReconciliationCompletedClean
    reconciliation_required --> committed : ReconciliationCompletedDirty
    reconciliation_required --> failed : OperatorMarkedFailed
```

The lock sub-states are `ready` (unlocked, serving), `locked` (held), and `changes_staged` (mutations pending
under the held lock). Lock release returns to `ready`; lease expiry escalates to `dirty` for human
disposition.
