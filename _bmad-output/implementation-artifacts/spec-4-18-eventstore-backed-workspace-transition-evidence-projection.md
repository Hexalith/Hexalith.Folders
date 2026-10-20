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
- `src/Hexalith.Folders/Projections/FolderList/FolderProjectionEnvelope.cs`, `src/Hexalith.Folders.Server/FolderEventReplay.cs` — ordered tenant envelope and version-aware domain replay.
- `src/Hexalith.Folders.Server/ActorFoldedFolderRepository.cs`, `FolderDomainProcessor.cs`, `FolderDomainProjectionHandler.cs` there — Story 12.1 now emits actor-persisted events and registers full-replay `/project`; reuse these seams. Its original completed spec preserves historical live proof and current startup limitations.
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs` — lifecycle registration still uses in-memory transition evidence and task status; 12.2 owns the missing durable pipeline.
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs` — historical unguarded C6 remains; 4.22 owns its guarded correction.

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

Persist domain evidence separately from reader authority. Both OpenAPI versions allow timestamp/string/boolean audit values; snapshot, response and generated SDK currently assume timestamps. Widen them together through generation. Story 4.17's hermetic harness remains component evidence. Future integration spans core, Server, SDK generation and tests; no irreversible action is proposed. The existing queued decision leaves no new scope question: preserve draft/backlog until prerequisite acceptance.

## Verification

**Readiness recheck (2026-10-19; supersedes 2026-10-14):** Folders `b49b0b206238056b3aa3c963edc44b1b6fc0f5cc`; EventStore root gitlink `84bb115a0ced2497ca4315644a677b273cb44edf` (uncommitted working HEAD `d4d05f871991ec2af305267f59dcefeae3f127ed`).

- Story 12.1 is `done` (closed `65f34e7c`); retain its persisted-state/restart evidence.
- Story 12.2 is `in-progress` (spec `review_loop_iteration: 3`). Its operational-status code is on main (`EventStoreOperationalStatusReadModel.cs`, `Projections/OperationalStatus/`, landed in `0fa612f1`), but the story has not closed, and `story-12-2-v2-source-requalification-pending.yaml` remains pending. 4.18 must not consume it as accepted yet.
- Story 4.22 is `backlog` and has no spec. Story 11.15 is `backlog`.
- EventStore: `story-1-17-eventstore-release-prerequisite-2026-10-18.json` records read-only discovery of `v3.124.0` and an undispatched release workflow, not an evolution-compatibility acceptance for the current gitlink. The 12.1 consumer-compatibility records (through 2026-10-17) are scoped to Story 12.1.
- `rg -n 'IWorkspaceTransitionEvidenceReadModel|InMemoryTaskStatusReadModel' src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs src/Hexalith.Folders.Server src/Hexalith.Folders.Workers` — exit 0; transition evidence is still only the core in-memory `TryAdd` at line 86.
- `rg -n 'LockLeaseBecameStale|stagedRecoveryStartedAt|stagedCleanupNotBefore|stagedByTaskId' src/Hexalith.Folders/Aggregates/Folder src/Hexalith.Folders.Server` — exit 1; the guarded C6 events are absent.

Outcome: the prerequisites are still unmet (12.2 not closed; 4.22 not started). The frozen intent is unchanged, the spec stays draft/backlog, and no implementation was started. No build or runtime tests ran.

- `dotnet build Hexalith.Folders.slnx -c Debug` — build after prerequisite acceptance.
- Build/run relevant core, Server, Client and integration projects individually; use built xUnit v3 assemblies with `-class` for focused execution. Require persisted end-state in the real lane.
- `pwsh -File tests/tools/run-contract-spine-gates.ps1` and `pwsh -File tests/tools/run-safety-invariant-gates.ps1` — contract/generation parity and metadata safety.
- `git diff --check` — no whitespace errors.
