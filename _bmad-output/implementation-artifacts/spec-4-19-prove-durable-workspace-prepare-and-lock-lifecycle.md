---
title: 'Story 4.19: Prove durable workspace prepare and lock lifecycle'
type: 'feature'
created: '2026-10-06'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
story_key: '4-19-prove-durable-workspace-prepare-and-lock-lifecycle'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Prepare and lock behavior is proven only against in-memory repositories, mocked or capturing gateways, and query-time clock derivation. Production cannot boot durably, so nothing shows that workspace ownership and lock state survive restart or keep colliding writers apart.

**Approach:** Prove prepare, acquire, inspect, release, expiry, stale, and revocation through the deployed REST → gateway → processor → authorization gate → EventStore/projection path. Stories 4.18, 4.22, 12.1–12.3, and 12.6 must first supply transition evidence, durable state and idempotency, and the approved C6 guarded lifecycle. The evidence includes restart and empty-checkpoint replay.

**Decisions (2026-10-06, Jerome; spec Open Questions 1–3 and size gate):**
1. **Queue.** Keep 4.19 queued in draft until the EventStore event-evolution acceptance record exists and Stories 4.18, 4.22, 12.1–12.3, and 12.6 land, plus the prerequisites added by decisions 2 and 3. No preparatory component work. Before resuming, verify prerequisite acceptance records and runtime evidence.
2. **Proof-only.** 4.19 does not implement the canonical lock identity, alias collision, `revoked` production, or C7 renewal/revalidation. A `bmad-correct-course` run assigns AR-CURRENT-15 and AR-AUTHZ-04 to a new story (working title "Enforce canonical lock identity and C7 lock timing", depending on 12.1 and on 12.7 for confidential writer identity). That story becomes a 4.19 prerequisite.
3. **Shared lane.** The same correct-course adds Story 11.15 as a prerequisite of Stories 4.18–4.21. 4.19 adds only its test classes and evidence record on that lane; no private production-mode fixture.
- **Size.** Keep the full spec.
- **Approval (2026-10-06, Jerome).** Spec content approved, and this frozen intent is locked. Status stays `draft` under decision 1. On resumption, re-verify prerequisites and refresh the Code Map with the new story's identifier, then set `ready-for-dev`. Outstanding checks: the correct-course in decisions 2–3, and the prerequisite acceptance records.

## Boundaries & Constraints

**Always:** Authorize before lookup, append, and replay, and scope every key by tenant. Lock state reports exactly `unlocked`, `locked`, `expired`, `stale`, or `revoked`, kept separate from lifecycle and disposition. Serialize writers on managed tenant + canonical provider/repository + normalized-ref token. Folder, workspace, and task IDs stay metadata, and aliases collide. Apply C7 `1.0.0` timing: 30 s renewal, 15 s revalidation, 60 s revocation effect, and an inclusive 60 s expired→stale threshold. A revoked lock instance never reactivates; recovery creates a new instance under the same identity. Assert the persisted end state, not only the response.

**Never:** Count NoOp, in-memory, seed, unavailable, safe-empty, mocked-gateway, or fake evidence as completion. Never hand-roll Dapr state or local persistence, author EventStore or platform changes from Folders, hand-edit generated clients, or initialize nested submodules. Prerequisite stories keep their own implementation ownership.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Prepare + acquire | Authorized task, ready repository-backed folder | Durable prepared/locked state; inspect returns `locked` | Operation/correlation/task identity preserved |
| Restart / replay | Host restart; empty-checkpoint rebuild | Same lock state, owner, identity, expiry | No duplicate or regressive events |
| Idempotent replay | Same key with equivalent intent, changed intent, or expired retention | Original result; conflict; expired rejection | No second execution |
| Alias collision | Another folder/workspace on the same tenant/repository/ref or an alias of it | Canonical lock conflict | No takeover or disclosure |
| Expiry / stale | Lease end, then the inclusive +60 s boundary | `expired`, then `stale` only via `LockLeaseBecameStale` | Release after expiry gets `LockExpired` plus retry eligibility |
| Revocation | Authority revoked while locked | `revoked` within 60 s; later protected work denied | Instance never reactivates |
| Denial | Wrong tenant, unauthorized, or locator-only caller | Safe non-disclosing denial | No protected read or event |
| Failure | Known failure, timeout/unknown outcome, store unavailable | Canonical error, terminal status, retry eligibility | Cancellation propagates; audit stays metadata-only |

</frozen-after-approval>

## Code Map

- `_bmad-output/planning-artifacts/planning-story-manifest.yaml`: 4.19 depends on DEC-A8-HOLD and DEC-A7-C6 (both approved) plus 4.18, 4.22, 4.23, 11.15, 12.1–12.3, and 12.6. The header has `ordinary_story_execution_authorized: false`. Story 4.23 and the 11.15 edge were added by the 2026-10-06 correct-course (decisions 2–3; `sprint-change-proposal-2026-10-06.md`).
- Lock-semantics gaps owned by Story 4.23, not 4.19: the lock id hashes tenant/folder/workspace/task; `FolderState` keeps only `BranchRefPolicy`, while `CanonicalRepositoryId` exists only on `Providers/Abstractions/ProviderRepositoryBindingResult.cs`; there is no renewal route (only v2 contract prose); and nothing produces `revoked`.
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`: prepare `POST …/preparation` (~213), acquire `POST …/lock` (~232; `exclusive_write`, lease 1–86400 s), release `POST …/lock/release` (~710), inspect `GET …/lock` (~251), retry eligibility (~322).
- `src/Hexalith.Folders.Server/FoldersDomainServiceRequestHandler.cs` and `FolderDomainProcessor.cs`: `/process` runs the gate, then `Process{PrepareWorkspace,LockWorkspace,ReleaseWorkspaceLock}Async`. Accepted results still return `PayloadNoOpDomainResult`; 12.1 owns the replacement.
- `src/Hexalith.Folders.Server/FoldersServerHostComposition.cs:37-44` and `FolderRepositoryStartupAssertion.cs`: the in-memory repository is registered only in Development and Staging, so Production refuses to boot until 12.1.
- `src/Hexalith.Folders/Aggregates/Folder/{WorkspacePreparationService,WorkspaceLockAcquisitionService,WorkspaceLockReleaseService,FolderAggregate,FolderCommandValidator}.cs`: the lifecycle to reuse; do not fork it. The validator's lock id and proof (~446/463) hash tenant/folder/workspace/task.
- `src/Hexalith.Folders/Queries/Folders/WorkspaceLockStatusQueryHandler.cs` (~128 derives `expired` from the clock) and `InMemoryWorkspaceLockStatusReadModel.cs`: the read side that 4.18/12.2 make durable.
- `src/Hexalith.Folders.EventStore/{Prepare,Lock,ReleaseWorkspaceLock}*IdempotencyIntentAdapter.cs`: 12.6 admission adapters; reuse them.
- `src/Hexalith.Folders/Observability/FolderTelemetryEmitter.cs:142` has an uncalled `RecordStaleLock`. The audit observer defaults to `NoOpFolderAuditObserver`.
- `tests/Hexalith.Folders.AppHost.Tests/AspireFoldersAppHostFixture.cs`: opt-in via `HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION`; Development with Keycloak off. This is not 4.19 evidence; use the Story 11.15 lane fixture instead.
- `tests/shared/Parity/InProcessRejectionPropagatingGatewayClient.cs`: an in-process round trip, so component evidence only. Keep the existing `FolderWorkspace*ServiceTests`, `WorkspaceLockEndpointTests`, `FolderLifecycle{ReplayDeterminism,SecurityBoundary}Tests`, and `GoldenLifecycleParityTests` green.

## Tasks & Acceptance

**Execution:**
- [ ] `_bmad-output/planning-artifacts/planning-story-manifest.yaml`: read-only. Verify each prerequisite's acceptance record and runtime evidence, including the new lock-semantics story and 11.15; never infer acceptance from sprint-status.
- [ ] `tests/Hexalith.Folders.AppHost.Tests/DurableWorkspaceLockLifecycleTests.cs`: on the Story 11.15 lane, run prepare, acquire, inspect, and release over deployed REST. Assert the persisted stream and projection, restart, and empty-checkpoint replay.
- [ ] `tests/Hexalith.Folders.AppHost.Tests/DurableWorkspaceLockBoundaryTests.cs`: one test per matrix row from idempotent replay through failure, plus a metadata-only audit assertion.
- [ ] `_bmad-output/implementation-artifacts/4-19-durable-prepare-lock-evidence.yaml`: record pins, composition, commands, results, persisted-state assertions, and restart boundaries.

**Acceptance Criteria:**
- Given accepted prerequisites and a production-mode deployment, when the lifecycle runs and the host restarts or replays from an empty checkpoint, then persisted events, projection, and inspect results agree.
- Given any matrix row on the deployed path with no mocked gateway, when it executes, then its outcome matches the matrix and the audit and diagnostics stay metadata-only.
- Given only component, in-memory, or degraded-lane evidence, when completion is evaluated, then the story stays open.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `test -f references/Hexalith.EventStore/_bmad-output/implementation-artifacts/ext-es-event-evolution-v1.yaml` -- expected: exit 0 before implementation starts (exit 1 on 2026-10-06).
- `dotnet build Hexalith.Folders.slnx -c Debug -p:UseNuGetDeps=false` -- expected: 0 errors.
- `HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION=true dotnet tests/Hexalith.Folders.AppHost.Tests/bin/Debug/net10.0/Hexalith.Folders.AppHost.Tests.dll -class <new classes>` -- expected: pass with no skips, and with persisted-state assertions executed.
- `pwsh -File tests/tools/run-safety-invariant-gates.ps1` and `git diff --check` -- expected: clean.
