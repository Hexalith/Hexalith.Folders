---
title: 'Story 4.18: EventStore-backed workspace transition-evidence projection'
type: 'feature'
created: '2026-10-06'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
story_key: '4-18-eventstore-backed-workspace-transition-evidence-projection'
context:
  - '_bmad-output/implementation-artifacts/epic-4-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Transition evidence is manually seeded in memory and cannot survive restart.

**Approach:** Project ordered durable workspace events into metadata-only snapshots through EventStore's read-model SDK. Consume Stories 12.1–12.2's durable pipeline and Story 4.22's approved guarded C6 model; register the projector and reader in Server.

**Decision (2026-10-06, Jerome; scope question 1 in this spec):** Keep Story 4.18 queued until the accepted EventStore evolution release and Stories 12.1, 12.2, and 4.22 land. Preparatory component work is deferred. Verify those prerequisite acceptance records and runtime evidence before resuming; the spec remains draft while they are outstanding.

## Boundaries & Constraints

**Always:** Authorize before reads; scope identities by tenant/folder/workspace; preserve task binding, canonical failures and cancellation. Derive transition timestamps/watermarks from events. Require persisted end-state, deployed population, restart and empty-checkpoint replay evidence for completion.

**Never:** Duplicate lifecycle/platform plumbing, hand-edit generated clients, initialize nested submodules, or count fake/seed evidence as completion. Prerequisite implementation and external release remain separately owned.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Population | Ordered durable events | C6 transition, task/operation, timestamps, retry/failure/reconciliation and lock evidence | Approved vocabulary only |
| Replay | Empty checkpoint, duplicate delivery, restart | Equivalent persisted snapshots | No duplicate/regressive writes |
| Conflict/gap | Concurrent writers, conflicting sequence, incomplete stream | No false checkpoint/freshness | Canonical safe retry/unavailable |
| Denial | Wrong tenant, absent authority, foreign scope | No protected read/existence disclosure | Existing safe-denial mapping |
| Bad evidence | Stale, corrupt, mismatched identity/version | Suppress unusable metadata | Canonical stale/unavailable/safe missing |
| Failure | Store unavailable or timeout | No false success/sensitive diagnostics | Canonical availability; propagate cancellation |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Folders/Queries/Folders/WorkspaceTransitionEvidenceQueryHandler.cs` — keep authorization-before-read and the current safe-denial mapping.
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs` — line 87 is the in-memory reader; lines 100–118 move the other operational readers to EventStore. `Save` only seeds tests.
- `src/Hexalith.Folders/Queries/Folders/WorkspaceTransitionEvidenceSnapshot.cs` and `src/Hexalith.Folders.Client` — audit values are timestamps. `hexalith.folders.v2.yaml` also allows string and boolean; regenerate from `nswag.json`.
- `src/Hexalith.Folders/Projections/SemanticIndexing/EventStoreSemanticIndexingBridgeStore.cs` — reuse `IReadModelStore` and `ReadModelWritePolicy`. Leave that store unchanged.
- `src/Hexalith.Folders.Server/OperationalStatusProjectionHandler.cs`, `FolderEventReplay.cs`, `FoldersServerServiceCollectionExtensions.cs` line 201, and `FoldersDomainServiceEndpoints.cs` line 475 — replay, store registration, and the existing GET route. Keep this projection separate from operational status.
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs` — unguarded C6 remains Story 4.22's. `LockLeaseBecameStale` and the new projection types are absent.

## Tasks & Acceptance

**Execution:**
- [ ] `_bmad-output/planning-artifacts/planning-story-manifest.yaml` — require acceptance records and runtime evidence for the EventStore evolution release and Stories 12.1, 12.2, and 4.22 before production code.
- [ ] `src/Hexalith.Folders/Projections/WorkspaceTransitionEvidence/WorkspaceTransitionEvidenceProjection.cs` — fold approved C6 events in envelope order; reject bad identity, sequence, and duplicates.
- [ ] `src/Hexalith.Folders/Projections/WorkspaceTransitionEvidence/EventStoreWorkspaceTransitionEvidenceStore.cs` — read and write scoped snapshots through the EventStore read-model SDK, one type per file.
- [ ] `src/Hexalith.Folders/Queries/Folders/WorkspaceTransitionEvidenceSnapshot.cs` and `WorkspaceTransitionEvidenceQueryHandler.cs` — accept the approved audit values and suppress stale, foreign, or corrupt metadata.
- [ ] `src/Hexalith.Folders.Server/WorkspaceTransitionEvidenceProjectionHandler.cs` and `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs` — register the durable reader beside the operational-status swap; return canonical unavailable when the store fails.
- [ ] `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs` and `src/Hexalith.Folders.Client/nswag.json` — regenerate the mixed audit-value client.
- [ ] `tests/Hexalith.Folders.Tests/Projections/WorkspaceTransitionEvidence/WorkspaceTransitionEvidenceProjectionTests.cs` and `tests/Hexalith.Folders.Tests/Queries/Folders/WorkspaceTransitionEvidenceQueryHandlerTests.cs` — cover the matrix, including denial before read.
- [ ] `tests/Hexalith.Folders.Server.Tests/GetWorkspaceTransitionEvidenceEndpointTests.cs` and `tests/Hexalith.Folders.Client.Tests/WorkspaceTransitionEvidenceClientTests.cs` — cover route safety and the mixed-value round-trip.
- [ ] `tests/Hexalith.Folders.IntegrationTests/WorkspaceTransitionEvidenceDurabilityTests.cs` — assert stored contents, restart, empty-checkpoint replay, and faults on the real processor and store.

**Acceptance Criteria:**
- Given accepted prerequisites, when deployed projection, restart, and empty-checkpoint replay run, then persisted evidence is deterministic and durable.
- Given current correct-tenant authority, when evidence is read, then C6, task, operation, retry, failure, reconciliation, and freshness agree with the durable events.
- Given denial, corruption, staleness, failure, timeout, or conflict, when the operation runs, then the canonical safe result discloses no protected data.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

Keep domain evidence apart from reader authority, and widen timestamp, string, and boolean audit values in one generation. Preparatory work stays deferred. No migration or external side effect is proposed.

## Verification

**Readiness recheck (2026-10-30):** Folders `20034c1170c9f044f1f247abba87c0b17f94eebf`; EventStore `7b1df62102efb19da03553a890c62eb5ad6aeafe`.

- 12.1 `done`; retain `evidence/story-12-1-durability-2026-10-18-complete.json`.
- 12.2 `in-progress`, review loop 4; `evidence/story-12-2-durability.json` `full_acceptance: false`; `evidence/story-12-2-v2-source-requalification-pending.yaml` `story_closure_claimed: false`.
- 4.22 `backlog`, no spec. 11.15 `backlog` is on the manifest edge and outside the frozen resume list.
- `EXT-ES-EVENT-EVOLUTION` accepted only at `ead4949d30b37d3a976f5c9197789a91701f1a72` in `story-12-1-eventstore-6-6-acceptance-2026-10-11.yaml` for 12.1 source/SDK compatibility.

12.2 is open and 4.22 has not started, so this spec stays draft. No implementation or build ran.

**Commands:**
- `dotnet build Hexalith.Folders.slnx -c Debug` after prerequisite acceptance.
- Build and run core, Server, Client, and integration projects individually; use built xUnit v3 assemblies with `-class` and assert persisted end-state.
- `pwsh -File tests/tools/run-contract-spine-gates.ps1` and `pwsh -File tests/tools/run-safety-invariant-gates.ps1`.
- `git diff --check`.
