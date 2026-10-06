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

- `src/Hexalith.Folders/Queries/Folders/WorkspaceTransitionEvidenceQueryHandler.cs` — reuse strict authorization-before-read and identity checks; add freshness/scope/schema validation.
- `src/Hexalith.Folders/Projections/SemanticIndexing/EventStoreSemanticIndexingBridgeStore.cs` — persistence precedent using `IReadModelStore` and `ReadModelWritePolicy`.
- `src/Hexalith.Folders/Projections/FolderList/FolderProjectionEnvelope.cs` — ordered tenant envelope; use prerequisite-approved event evolution and domain folds.
- `_bmad-output/implementation-artifacts/spec-12-1-eventstore-backed-folder-repository-and-replay.md` — frozen platform-first decision. Required external acceptance record is absent; folder processing still returns NoOp, Production lacks a durable repository, and `/project` returns 501.
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs` — historical C6; 4.22 owns its guarded correction.

## Tasks & Acceptance

**Execution:**
- [ ] `_bmad-output/planning-artifacts/planning-story-manifest.yaml` — verify prerequisite release/runtime acceptance before production integration; do not infer acceptance from tracking.
- [ ] `src/Hexalith.Folders/Projections/WorkspaceTransitionEvidence/WorkspaceTransitionEvidenceProjection.cs` — deterministically fold approved C6 events, validating envelope identity/order/duplicates.
- [ ] `src/Hexalith.Folders/Projections/WorkspaceTransitionEvidence/EventStoreWorkspaceTransitionEvidenceStore.cs` — persist/read scoped evidence through EventStore SDK policies; supporting types each get a named file.
- [ ] `src/Hexalith.Folders/Queries/Folders/WorkspaceTransitionEvidenceSnapshot.cs` and `WorkspaceTransitionEvidenceQueryHandler.cs` there — support allowlisted audit values; validate freshness, scope and corruption.
- [ ] `src/Hexalith.Folders.Server/WorkspaceTransitionEvidenceProjectionHandler.cs` and `FoldersServerServiceCollectionExtensions.cs` there — consume prerequisite SDK dispatch and register durable reader, retaining honest degraded behavior.
- [ ] `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`, `src/Hexalith.Folders.Client/nswag.json` — support existing mixed audit-value schema; regenerate SDK output without manual edits.
- [ ] `tests/Hexalith.Folders.Tests/Projections/WorkspaceTransitionEvidence/WorkspaceTransitionEvidenceProjectionTests.cs` and `tests/Hexalith.Folders.Tests/Queries/Folders/WorkspaceTransitionEvidenceQueryHandlerTests.cs` — cover matrix and denial-before-read.
- [ ] `tests/Hexalith.Folders.Server.Tests/GetWorkspaceTransitionEvidenceEndpointTests.cs`, `tests/Hexalith.Folders.Client.Tests/WorkspaceTransitionEvidenceClientTests.cs` — verify route safety and mixed-value SDK round-trip.
- [ ] `tests/Hexalith.Folders.IntegrationTests/WorkspaceTransitionEvidenceDurabilityTests.cs` — real REST/gateway/processor/persistence/projector; assert stored contents, restart, empty replay and fault boundaries.

**Acceptance Criteria:**
- Given accepted prerequisites, when deployed projection/restart/empty-checkpoint replay runs, then persisted transition evidence is deterministic and durable.
- Given current correct-tenant authority, when read, then C6, task/operation, retry/failure/reconciliation and freshness metadata agree with durable events.
- Given denial, corruption, staleness, failure, timeout or conflict, when executed, then canonical safe results disclose no protected or sensitive data.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

Persist domain evidence separately from reader authority. Both OpenAPI versions allow timestamp/string/boolean audit values; snapshot, response and generated SDK currently assume timestamps. Widen them together through generation. Story 4.17's hermetic harness remains component evidence. Local changes span core, Server, SDK generation and tests; no irreversible action is proposed.

## Verification

Planning prerequisite check: `test -f references/Hexalith.EventStore/_bmad-output/implementation-artifacts/ext-es-event-evolution-v1.yaml` returned exit 1 (missing record). No implementation tests ran.

- `dotnet build Hexalith.Folders.slnx -c Debug` — build after prerequisite acceptance.
- Build/run relevant core, Server, Client and integration projects individually; use built xUnit v3 assemblies with `-class` for focused execution. Require persisted end-state in the real lane.
- `pwsh -File tests/tools/run-contract-spine-gates.ps1` and `pwsh -File tests/tools/run-safety-invariant-gates.ps1` — contract/generation parity and metadata safety.
- `git diff --check` — no whitespace errors.
