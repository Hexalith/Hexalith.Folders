---
title: 'Story 6.12: Durable readiness, lock, dirty-state, and failure diagnostics'
type: 'feature'
created: '2026-10-04'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '160906a9e6b98dc495f1092378661b92084fc59f'
context:
  - '_bmad-output/implementation-artifacts/epic-6-context.md'
  - 'docs/governance/approval-policy.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Readiness, lock, dirty-state, and failed-operation diagnostics use seeded memory records. Operators cannot rely on populated production diagnostics after restart.

**Approach:** After Stories 4.22, 12.1, and 12.2 supply lifecycle authority and durable ordered events/checkpoints, register four EventStore-backed projections and serve their persisted records through existing authorized diagnostic reads.

## Boundaries & Constraints

**Always:** Use EventStore SDK projection/storage infrastructure, validated event versions, tenant/folder/workspace keys, atomic record/checkpoint writes, and authoritative replay coverage. Preserve authorization before observation and the v2 folder boundary. Keep lifecycle, lock, disposition, freshness/availability, and disclosure independent. Expose sanitized categories, advisory retry posture, opaque references, and digest-only changed-path evidence. Derive freshness from persisted evidence rather than response time.

**Never:** Introduce domain-owned Dapr plumbing/upcasters, source persistence, provider effects, seed authority, content/credentials/raw paths, or lifecycle changes. Provider/sync/freshness projections and browser journeys belong to 6.13–6.14. Memory/fake/NoOp/safe-empty evidence cannot complete 6.12.

## I/O & Edge-Case Matrix

| Scenario | Input/state | Expected behavior | Failure handling |
| --- | --- | --- | --- |
| Authorized read | Populated scoped record | Existing metadata-only contract | Check stored ownership |
| Restart/rebuild | Retained events, empty checkpoint | Same deterministic records | No historical byte rewrite |
| Replay boundary | Duplicate, gap, conflicting identity | Equivalent duplicate is idempotent | Conflict/corruption cannot advance checkpoint |
| Denial | Wrong tenant or missing authority | Canonical non-disclosing denial | No protected lookup |
| Source/store failure | Corruption, outage, timeout | Honest unavailable result | No empty/healthy fallback |
| Disclosure | Sensitive event metadata | Redaction/classification agrees with values | Exclude forbidden values everywhere |

</frozen-after-approval>

## Open Questions

1. Prerequisite scope: keep 6.12 draft until 4.22/12.1/12.2 are delivered (preserves this story's scope), or authorize separate prerequisite-first work (expands the work and still requires platform delivery before dependent execution)?

## Code Map

- `src/Hexalith.Folders/Queries/OpsConsole/` — reuse existing views, query handlers, and `IOpsConsoleDiagnosticsReadModel`. Views hide scope keys with `JsonIgnore`; persist dedicated records instead.
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs` — diagnostic and prerequisite status registrations currently use memory.
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs`, `FoldersDomainServiceEndpoints.cs`, `FoldersServerHostComposition.cs` — accepted results emit no events, `/project` returns 501, and Production lacks a durable repository.
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs` — pre-PD11 rules; consume 4.22's replacement.
- `references/Hexalith.EventStore/src/` — reuse `IAsyncDomainProjectionHandler`, `SharedProjectionEpochCoordinator`, read-model batch storage, and freshness helpers. Global position alone does not prove complete consumption.

## Tasks & Acceptance

**Execution:**
- [ ] `_bmad-output/planning-artifacts/planning-story-manifest.yaml` — verify prerequisite delivery/admission and accepted event evolution; bind readiness source/aggregation, failure-resolution, and checkpoint semantics before implementation. Do not rewrite historical approvals.
- [ ] `src/Hexalith.Folders/Projections/OpsConsole/ReadinessDiagnosticsProjectionHandler.cs`, `LockDiagnosticsProjectionHandler.cs`, `DirtyStateDiagnosticsProjectionHandler.cs`, `FailedOperationDiagnosticsProjectionHandler.cs` — add four named deterministic handlers using prerequisite semantics and coordinated replay.
- [ ] `src/Hexalith.Folders/Projections/OpsConsole/OpsConsoleDiagnosticRecord.cs` — persist scoped metadata, source identity/checkpoint, schema, timestamps, and availability atomically through EventStore; put additional types in separate files.
- [ ] `src/Hexalith.Folders/Queries/OpsConsole/EventStoreOpsConsoleDiagnosticsReadModel.cs` — read committed projection generations, validate ownership/disclosure, and materialize existing views with honest freshness/failure behavior.
- [ ] `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs` — register durable reads and four named projections through prerequisite SDK composition; reject production memory substitution.
- [ ] `tests/Hexalith.Folders.Tests/Projections/OpsConsole/OpsConsoleDiagnosticsProjectionTests.cs` — cover the matrix, deterministic folding, redaction, and lifecycle/freshness boundaries.
- [ ] `tests/Hexalith.Folders.Server.Tests/OpsConsoleDiagnosticsEndpointTests.cs` — preserve canonical contract and denial ordering, including folder-scoped v2 readiness.
- [ ] `tests/Hexalith.Folders.IntegrationTests/DurableOpsConsoleDiagnosticsTests.cs` — use the platform's real persistence harness; assert stored contents/checkpoints, populated reads after host restart, empty rebuild, conflicts, corruption, outage, timeout, and redaction.

**Acceptance Criteria:**
- Given delivered prerequisites, when the deployed Server consumes retained/new events, then all four scoped views populate deterministically and survive restart.
- Given authorized and denied callers, when populated diagnostics are read, then contract, authority, availability, and metadata-redaction evidence passes the matrix.
- Given a real empty-checkpoint rebuild, when replay completes, then persisted records/checkpoints match live consumption without relying on seed or fake-only evidence.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

Investigation on 2026-10-04 confirms missing 4.22/12.1/12.2 delivery, not merely stale tracker labels. EventStore projection/storage APIs exist, but its declared event-evolution acceptance record is absent. Readiness observations currently use memory; their durable source and aggregation rules require prerequisite delivery. Blanket A8 hold was removed; historical per-story holds do not replace the current owner's decision. This draft cannot satisfy ready-for-development until the technical gaps close. No production code, tracker, dependency, or deployment changes are part of this planning pass.

## Verification

After prerequisite delivery:

- `dotnet build tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj -c Debug`
- `dotnet tests/Hexalith.Folders.IntegrationTests/bin/Debug/net10.0/Hexalith.Folders.IntegrationTests.dll -class '*DurableOpsConsoleDiagnosticsTests'`

Also build/run owning unit and Server projects individually. Record persisted end-state, restart/replay comparisons, and blockers from the real platform persistence lane. Seed-backed endpoint tests establish contract behavior only.
