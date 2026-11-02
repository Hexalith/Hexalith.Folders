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

- `src/Hexalith.Folders/Queries/Folders/WorkspaceTransitionEvidenceQueryHandler.cs` — authorization precedes read; preserve safe denial. `WorkspaceTransitionEvidenceSnapshot.cs` permits timestamp audit values only.
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs` — transition reader is in memory; other operational readers use EventStore.
- `src/Hexalith.Folders.Server/OperationalStatusProjectionHandler.cs` — SDK streaming rebuild/batch pattern; keep projections separate. `FoldersServerHostComposition.cs` registers host services.
- `src/Hexalith.Folders.Server/FolderEventReplay.cs` — ordered replay/identity checks. `FoldersDomainServiceEndpoints.cs` maps the safe GET result.

## Tasks & Acceptance

**Execution:**
- [ ] `_bmad-output/planning-artifacts/planning-story-manifest.yaml` — verify accepted EventStore evolution and 12.1, 12.2, 4.22, 11.15 evidence before code.
- [ ] `src/Hexalith.Folders/Projections/WorkspaceTransitionEvidence/WorkspaceTransitionEvidenceProjection.cs` — fold guarded C6 events in order; reject identity, sequence and duplicate errors.
- [ ] `src/Hexalith.Folders/Projections/WorkspaceTransitionEvidence/EventStoreWorkspaceTransitionEvidenceStore.cs` — persist scoped snapshots through the SDK, one type per file.
- [ ] `src/Hexalith.Folders/Queries/Folders/WorkspaceTransitionEvidenceSnapshot.cs` and `WorkspaceTransitionEvidenceQueryHandler.cs` — support mixed audit values; suppress stale, foreign and corrupt evidence.
- [ ] `src/Hexalith.Folders.Server/WorkspaceTransitionEvidenceProjectionHandler.cs` and `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs` — register projector/reader; map store failure to canonical unavailable.
- [ ] `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs` and `src/Hexalith.Folders.Client/nswag.json` — regenerate the mixed-value Client from the contract.
- [ ] `tests/Hexalith.Folders.Tests/Projections/WorkspaceTransitionEvidence/WorkspaceTransitionEvidenceProjectionTests.cs` and `tests/Hexalith.Folders.Tests/Queries/Folders/WorkspaceTransitionEvidenceQueryHandlerTests.cs` — cover folding and the edge matrix.
- [ ] `tests/Hexalith.Folders.Server.Tests/GetWorkspaceTransitionEvidenceEndpointTests.cs` and `tests/Hexalith.Folders.Client.Tests/WorkspaceTransitionEvidenceClientTests.cs` — prove safe routing and mixed-value round trip.
- [ ] `tests/Hexalith.Folders.IntegrationTests/WorkspaceTransitionEvidenceDurabilityTests.cs` — assert persisted contents, restart, empty-checkpoint replay and faults.

**Acceptance Criteria:**
- Given accepted prerequisites, when deployed projection, restart, and empty-checkpoint replay run, then persisted evidence is deterministic and durable.
- Given current correct-tenant authority, when evidence is read, then C6, task, operation, retry, failure, reconciliation, and freshness agree with the durable events.
- Given denial, corruption, staleness, failure, timeout, or conflict, when the operation runs, then the canonical safe result discloses no protected data.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

**Readiness recheck (2026-11-02):** Folders `88b44fdefd5f0512ebc89bb213ead18e66eb7337`; EventStore checkout `f2be45ede245211f549978358872875c5df28dc8`.

- `sprint-status.yaml`: 12.1 `done`; 12.2 `in-progress`; 4.22 and 11.15 `backlog`.
- `evidence/story-12-2-durability.json`: `full_acceptance: false`; the 2026-11-01 Story 12.2 checkpoint leaves live qualification and whole-story review open.
- `story-12-1-eventstore-6-6-acceptance-2026-10-11.yaml`: EventStore evolution accepted at `ead4949d30b37d3a976f5c9197789a91701f1a72` for source/SDK compatibility only.

The frozen defer decision keeps this spec `draft`. The reader remains in memory; no projector, implementation or build ran.

**Commands:**
- `dotnet build Hexalith.Folders.slnx -c Debug` after prerequisite acceptance.
- Build/run core, Server, Client and integration projects separately; invoke built xUnit v3 assemblies with `-class` and assert stored end-state.
- `pwsh -File tests/tools/run-contract-spine-gates.ps1` and `pwsh -File tests/tools/run-safety-invariant-gates.ps1`.
- `git diff --check`.
