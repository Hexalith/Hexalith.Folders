---
title: 'Story 3.14: Complete asynchronous repository creation and binding'
type: 'feature'
created: '2026-09-12'
status: blocked
review_loop_iteration: 0
followup_review_recommended: false
context:
    - '_bmad-output/implementation-artifacts/epic-3-context.md'
    - '_bmad-output/planning-artifacts/planning-story-manifest.yaml'
    - '_bmad-output/planning-artifacts/architecture.md'
    - '_bmad-output/planning-artifacts/architecture/validation-2026-09-08/reviews/review-adversarial.md'
warnings: []
deferred: []
baseline_revision: df0b636a6194b5529b566f0f579f1cebfa7f48c6
---

<intent-contract>

## Intent

**Problem:** Accepted repository-creation and existing-repository-binding requests do not reach a durable asynchronous terminal result. Accepted folder events are currently discarded at the EventStore boundary, no production subscriber invokes the provisioning manager, existing binding performs provider I/O synchronously, and all current replay evidence is fake or in-memory.

**Approach:** After the declared Stories 12.1, 12.2, and 12.4 prerequisites land, consume their durable stream, projection/task, and executor wiring to run both request kinds through one tenant-scoped, fenced provisioning process. Persist sanitized progress and reconciliation evidence, then append exactly one canonical terminal outcome without duplicating provider effects.

**Entry gate:** Story 3.14 MUST NOT enter implementation until Stories 12.1, 12.2, and 12.4 are complete, including Story 12.4's Story 12.3 prerequisite and all applicable OQ1-OQ4 decisions. Story 3.14 consumes those completed production capabilities and does not absorb, duplicate, or supersede their ownership.

## Boundaries & Constraints

**Always:** Re-authorize before protected lookup or mutation; fence each canonical intent durably before provider access; preserve creation and binding as distinct operations; use the selected production `IGitProvider`; record retry eligibility and metadata-only evidence; perform at most five read-only reconciliation checks within fifteen minutes after an unknown outcome; reconstruct the same result after host restart or empty subscriber checkpoint; retain originating operation and phase so reconciliation resumes the equivalent normal transition.

**Never:** Reimplement EventStore persistence, durable projections, or the real executor inside this story; perform provider work synchronously on the acceptance path; blind-retry an ambiguous mutation; treat in-memory, NoOp, seed, unavailable, safe-empty, or fake-only behavior as completion; persist credentials, provider bodies, endpoints, raw repository/ref locators, file content, diffs, or exceptions; write or revert `_bmad-output/implementation-artifacts/sprint-status.yaml`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Durable success | Authorized accepted create or bind request with current provider evidence | Return operation identity promptly, execute once asynchronously, and persist one terminal binding/task result | No error expected |
| Equivalent or conflicting replay | Duplicate delivery or reused key | Equivalent replay returns the recorded result; conflicting replay is rejected before protected access | Canonical idempotency conflict without prior-intent disclosure |
| Known provider failure | Conclusive no-effect provider result | Persist one terminal failure with exact retry eligibility and sanitized evidence | No mutation retry |
| Unknown provider outcome | Mutation may have occurred | Persist auto-recovering state and schedule bounded read-only checks | Resolve to the equivalent known outcome or `reconciliation_required` after the governed budget |
| Restart or empty checkpoint | Durable event and orchestration state remain, consumer checkpoint does not | Rebuild and resume without a second eligible provider effect | Fail closed on corrupt or unavailable durable state |
| Unauthorized or wrong tenant | Stale, revoked, or cross-tenant request | Deny before context, credential, target, or provider access | Metadata-only safe denial |

</intent-contract>

## Code Map

- `_bmad-output/planning-artifacts/planning-story-manifest.yaml:631` -- declares 3.10, 3.12, 12.1, 12.2, and 12.4 as Story 3.14 prerequisites.
- `_bmad-output/planning-artifacts/epics.md:2616` -- Story 12.1 owns durable folder streams, real `IFolderRepository`, NoOp retirement, production boot, and empty-checkpoint replay.
- `_bmad-output/planning-artifacts/epics.md:2630` -- Story 12.2 owns durable lifecycle/task projections and terminal task completion.
- `_bmad-output/planning-artifacts/epics.md:2658` -- Story 12.4 owns the real executor/write path and provisioning-process-manager wiring where required.
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs:1257` -- accepted `FolderResult.Events` become an eventless `PayloadNoOpDomainResult`.
- `src/Hexalith.Folders/Aggregates/Folder/RepositoryBindingService.cs:121` -- existing binding still performs provider validation synchronously before appending request and outcome.
- `src/Hexalith.Folders.Workers/RepositoryProvisioning/RepositoryProvisioningProcessManager.cs:25` -- reusable creation-only manager; lacks durable fencing, binding dispatch, scheduling, and post-restart reconciliation.
- `src/Hexalith.Folders.Workers/FoldersWorkersModule.cs:87` -- registers the manager but no folder-event subscriber or durable repository invokes it.
- `src/Hexalith.Folders/Infrastructure/Persistence/InMemoryFolderRepository.cs:12` -- only current repository implementation is explicitly restart-volatile.
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs:266` -- unknown/reconciliation folding loses the originating provisioning phase.
- `tests/Hexalith.Folders.Workers.Tests/RepositoryProvisioningProcessManagerTests.cs:15` -- component coverage uses a fake provider and retained in-memory dictionary, not deployed durable restart evidence.

## Tasks & Acceptance

**Execution:**
- `Prerequisite entry gate (outside Story 3.14 scope)` -- do not begin Story 3.14 implementation until Stories 12.1, 12.2, and 12.4 are complete in their owned scopes, including Story 12.4's Story 12.3 prerequisite and all applicable OQ1-OQ4 decisions; consume only their completed production outputs -- prevents duplicate platform plumbing and false production claims.
- `src/Hexalith.Folders/Aggregates/Folder/RepositoryBindingService.cs` -- make existing-repository binding request-only after acceptance -- keeps provider work asynchronous.
- `src/Hexalith.Folders.Workers/RepositoryProvisioning/` -- dispatch both request event types through a tenant-scoped ETag-fenced orchestration record and production provider -- guarantees one eligible effect across concurrent delivery and restart.
- `src/Hexalith.Folders/Providers/Abstractions/` -- add the narrow read-only creation/binding observation needed for reconciliation -- avoids blind mutation retry.
- `src/Hexalith.Folders/Aggregates/Folder/` and durable projections -- preserve full sanitized result, retry budget, and originating phase through terminal folding -- keeps C6 and task outcomes deterministic.
- `src/Hexalith.Folders.Workers/FoldersWorkersModule.cs`, `deploy/dapr/production/`, and AppHost wiring -- register the real subscription, stores, credentials, target resolver, reconciler, scopes, resiliency, and dead-letter behavior -- makes the path deployable and restart-safe.
- `tests/Hexalith.Folders.AppHost.Tests/` -- prove durable success, empty-checkpoint restart, duplicate/conflict fencing, tenant denial, known failure, unknown resolution/exhaustion, and state-store end state with real Dapr and production-composed adapters -- supplies the required outer evidence.

**Acceptance Criteria:**
- Given any declared prerequisite Story 12.1, 12.2, or 12.4, Story 12.4's Story 12.3 prerequisite, or an applicable OQ1-OQ4 decision is incomplete, when Story 3.14 is dispatched, then implementation does not begin, the story remains blocked, and none of that prerequisite ownership is absorbed, duplicated, or superseded.
- Given an authorized Story 3.6 creation request or Story 3.7 binding request and the applicable production provider behavior, when the durable worker subscribes, executes, persists, restarts, retries, or reconciles, then the operation follows the canonical C6 lifecycle and records exactly one terminal task/binding result with current retry eligibility and sanitized evidence.
- Given equivalent replay, when the request or event is delivered again, then no duplicate repository or binding effect occurs and the durable recorded result is returned.
- Given conflicting replay or wrong-tenant authority, when processing begins, then the request is rejected before credential, protected target, or provider access without disclosing prior intent or existence.
- Given a known provider failure, when the operation completes, then the failure is terminal as defined and its precise retry eligibility is preserved.
- Given an unknown provider outcome, when automatic reconciliation runs, then no mutation is retried, no more than five read-only checks occur within fifteen minutes, and the process records the equivalent confirmed outcome or `reconciliation_required` only after exhaustion or conflicting evidence.
- Given a deployed durable topology and an intentionally empty consumer checkpoint, when retained events and state replay after restart, then the same terminal result is reconstructed without another provider effect and no in-memory, NoOp, seed-only, unavailable, safe-empty, or fake-only path can satisfy the evidence gate.

## Spec Change Log

- 2026-09-12: Human resolution preserved Stories 12.1, 12.2, 12.3, and 12.4 ownership and made their completion plus applicable OQ1-OQ4 decisions an explicit entry gate; Story 3.14 remains blocked until those prerequisites land.

## Review Triage Log

## Design Notes

The dependency declaration and source state admit two observably different plans. The recorded plan makes Story 3.14 consume Stories 12.1, 12.2, and 12.4. Implementing now instead requires this story to absorb those separately owned epics, including the entire durable folder source, terminal projection pipeline, and real executor/wiring. That is a material scope and ownership change not authorized by the invocation. The workflow cannot select between “run prerequisites first” and “supersede their ownership here” without changing the planning contract.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj -c Release -m:1 -p:UseHexalithProjectReferences=true -p:MinVerVersionOverride=1.0.0 -p:NuGetAudit=false` -- expected: focused worker test assembly builds cleanly after implementation.
- `tests/Hexalith.Folders.Workers.Tests/bin/Release/net10.0/Hexalith.Folders.Workers.Tests -noLogo -noColor -class Hexalith.Folders.Workers.Tests.RepositoryProvisioningProcessManagerTests` -- expected: concurrency, replay, denial, failure, and reconciliation component tests pass.
- `HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION=true tests/Hexalith.Folders.AppHost.Tests/bin/Release/net10.0/Hexalith.Folders.AppHost.Tests -noLogo -noColor -class Hexalith.Folders.AppHost.Tests.Story314RepositoryProvisioningE2ETests` -- expected: production-composed durable outer acceptance lane passes against real Dapr state/pub-sub and provider adapters.

## Auto Run Result

Status: blocked
Blocking condition: prerequisite entry gate incomplete — Stories 12.1, 12.2, 12.3, and 12.4 and applicable OQ1-OQ4 decisions are not complete.

No implementation changes were made and no verification commands were run. The current source still discards accepted folder events at the EventStore boundary, performs existing-repository provider validation synchronously, registers no repository-provisioning event subscriber, and relies on the in-memory folder repository for the existing process manager. Implementing Story 3.14 in that state would absorb the durable stream, projection/task, executor, and production wiring owned by the prerequisite stories, contrary to the read-only intent contract. `_bmad-output/implementation-artifacts/sprint-status.yaml` was not modified.
