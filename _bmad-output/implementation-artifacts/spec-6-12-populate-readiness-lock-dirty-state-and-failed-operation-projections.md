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

1. Prerequisite scope: keep 6.12 draft until 4.22 and 12.2 are delivered (recommended; preserves scope), or authorize prerequisite-first work as separately scoped builds (expands scope; 6.12 still waits for acceptance)? Story 12.1 is delivered.

## Code Map

- `src/Hexalith.Folders/Projections/OperationalStatus/` and `src/Hexalith.Folders.Server/OperationalStatusProjectionHandler.cs` — reuse source identity, coordinated generations, atomic batches, duplicate/gap/conflict checks and replay.
- `src/Hexalith.Folders/Queries/OpsConsole/` — existing views/authorized reads; readiness lookup is tenant-only. Persist ownership separately from JsonIgnored wire fields.
- `src/Hexalith.Folders/Queries/ProviderReadiness/` — observations remain memory-backed; host health defaults are not retained folder readiness evidence.
- `tests/Hexalith.Folders.AppHost.Tests/FolderDurabilityAcceptanceFixture.cs` — actual persisted-state/restart/rebuild harness. Seeded endpoint tests prove contracts only.

## Tasks & Acceptance

**Execution (after prerequisite delivery):**
- [ ] `_bmad-output/planning-artifacts/planning-story-manifest.yaml` — verify 4.22/12.2 acceptance and current event evolution; bind retained readiness source, deterministic aggregation, failure resolution and checkpoint coverage. Preserve historical approvals.
- [ ] `src/Hexalith.Folders/Projections/OpsConsole/` — add `ReadinessDiagnosticsProjectionHandler.cs`, `LockDiagnosticsProjectionHandler.cs`, `DirtyStateDiagnosticsProjectionHandler.cs` and `FailedOperationDiagnosticsProjectionHandler.cs`; fold approved source semantics with coordinated replay.
- [ ] `src/Hexalith.Folders/Projections/OpsConsole/OpsConsoleDiagnosticRecord.cs` — persist scoped metadata, schema, source identity, timestamps and checkpoint atomically; separate additional types.
- [ ] `src/Hexalith.Folders/Queries/OpsConsole/` — add `EventStoreOpsConsoleDiagnosticsReadModel.cs`; validate committed generations, ownership/disclosure and persisted freshness. Carry authorized folder scope through readiness interface/handlers; preserve v1 compatibility and disabled public v2.
- [ ] `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs` and `FoldersServerHostComposition.cs` — register four named projections and durable reads through SDK composition; prevent production memory substitution.
- [ ] `tests/Hexalith.Folders.Tests/Projections/OpsConsole/OpsConsoleDiagnosticsProjectionTests.cs` and `tests/Hexalith.Folders.Server.Tests/OpsConsoleDiagnosticsEndpointTests.cs` — cover the matrix, deterministic folds, sanitized failures, independent dimensions and authorization before observation.
- [ ] `tests/Hexalith.Folders.AppHost.Tests/DurableOpsConsoleDiagnosticsTests.cs` — use the real persistence fixture; assert populated stored records/checkpoints, restart, empty rebuild equivalence, source-byte preservation, corruption/conflict, outage, timeout and redaction.

**Acceptance Criteria:**
- Given accepted prerequisites and retained/new events, when the Server consumes them, then all four scoped views populate deterministically and survive restart.
- Given authorized/denied callers, when populated diagnostics are queried, then canonical contracts, folder authority, availability and redaction satisfy the matrix.
- Given empty checkpoints, when real persistence replay completes, then stored records/checkpoints equal live consumption without seed/fake authority.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

Reinvestigated 2026-11-11 at `78e2b9387536229199b8bbc24ecd177daaf46db8`. Story 12.1 now supplies repository/event/replay delivery. Story 12.2 supplies substantial operational persistence, but its November 4 spec/handoff retain failed current-source strict shutdown acceptance and open package/Release, PD10, SM4 and review gates. Story 4.22 is ready-for-dev but unimplemented: guarded failure/recovery, staged-task fields and stale-lease vocabulary are absent. Neither tracker labels nor historical blanket holds replace these technical checks.

Readiness still lacks a retained observation source and complete folder aggregation mapping. FR17 forbids ready for unsupported/unknown required capabilities; NFR81 requires actual dependency health. Bind these before readiness implementation. Do not freeze pre-PD11 dirty/failure semantics. Current scope changes only this draft; no source, tracker, dependencies or deployment changed.

## Verification

After prerequisites, build owning Domain, Server and AppHost test projects individually in Debug with `-p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -m:1 -nr:false`. Run built xUnit v3 assemblies with single-dash `-class`, including `*DurableOpsConsoleDiagnosticsTests`. Record real stored end-state, restart/rebuild comparisons and exact blockers; a skipped or unavailable lane cannot establish completion.
