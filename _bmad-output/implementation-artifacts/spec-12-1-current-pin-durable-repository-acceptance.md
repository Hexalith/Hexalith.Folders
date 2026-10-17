---
title: 'Story 12.1: complete current-pin durable repository acceptance'
type: 'feature'
created: '2026-10-17'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '_bmad-output/implementation-artifacts/epic-12-context.md'
  - '_bmad-output/implementation-artifacts/spec-12-1-eventstore-backed-folder-repository-and-replay.md'
  - '_bmad-output/planning-artifacts/epics.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 12.1's repository, event emission and replay implementation is reviewed, but delivery acceptance remains open. Its latest compatibility supplement predates the selected EventStore revision; previous live observations lack a tracked, repeatable acceptance lane.

**Approach:** Establish current-pin compatibility and an authenticated acceptance scenario against the existing local Aspire topology. Verify persisted organization/folder state, restart reconstruction, replay and safe failures; retain sanitized evidence tied to exact source revisions. Repair only defects exposed in this path.

## Boundaries & Constraints

**Always:** Use real JWT/human-receipt authorization, public Folders mutations, EventStore-owned append, SDK stream/read-model seams and authenticated Workers. Provision a fresh managed tenant through Tenants commands. Isolate local test storage and lifecycle operations. Preserve immutable event bytes and historical approvals. Retain the owner's accepted conflict proof: supported-replica serialization plus EventStore actor fault-injection checks for bounded reevaluation/exhaustion.

**Never:** Substitute seeded authority, hand-published grants, memory repositories or skipped live tests for acceptance. Change dependency pins, weaken authentication, expose v2, mutate shared deployments or rewrite completed specs. Keep broker/operator recovery and downstream Stories 12.2–12.7 with their owners. Leave unsuccessful acceptance rows open.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Mutation | Current tenant and explicit grant | Metadata-only retained events and matching persisted folds | Equivalent key replays; different intent conflicts |
| Restart/replay | Retained streams, empty replay checkpoint | Equivalent organization/folder state; unchanged retained hashes | Unknown/version-invalid events fail before advancement |
| Authority | Missing/revoked grant or foreign tenant | Canonical denial; persisted state unchanged | No protected existence disclosure |
| Infrastructure | Unavailable/corrupt evidence or timeout | Canonical safe failure; no accepted append | Bound waits and restore only task-owned resources |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Folders.Server/FoldersServerHostComposition.cs`, `FoldersDomainServiceRequestHandler.cs`, `FolderDomainProcessor.cs`: reuse durable registrations, receipt checks and event-bearing results.
- `src/Hexalith.Folders.Server/FolderEventReplay.cs`, `FolderDomainProjectionHandler.cs`: reuse exact event inventory and SDK projection folding.
- `src/Hexalith.Folders.Workers/FoldersWorkersModule.cs`: reuse authenticated callbacks and SDK permission stores.
- `tests/Hexalith.Folders.AppHost.Tests/AspireFoldersAppHostFixture.cs`: reuse topology discovery/lifecycle patterns; its Keycloak-disabled diagnostic setup cannot establish acceptance.
- `ProductionProcessWireEventTests`, `FolderEventReplayTests`, `WorkersOrganizationAclRouteTests`: retain existing process, replay and persisted-row regression coverage.

## Tasks & Acceptance

**Execution:**
- [ ] `_bmad-output/implementation-artifacts/story-12-1-eventstore-consumer-compatibility-2026-10-17.yaml`: record current-pin evolution, synchronous dispatch, receipt, stream-integrity and actor recovery/conflict checks. Report asynchronous-dispatch results separately; preserve earlier supplements.
- [ ] `tests/Hexalith.Folders.AppHost.Tests/FolderDurabilityAcceptanceFixture.cs`: add a dedicated opt-in fixture using the existing AppHost, real identity/tenant bootstrap, discovered endpoints, isolated retained storage and bounded resource restart/failure controls. Keep each helper type in its own file.
- [ ] `tests/Hexalith.Folders.AppHost.Tests/FolderDurabilityAcceptanceTests.cs`: exercise organization baseline/grant, public folder creation, positive ACL read, equivalent/conflicting intent, persisted contents, host restart, empty-checkpoint replay, revocation/foreign-tenant denial and infrastructure failures. Cover the matrix; require zero skips when opted in and verify retained hashes and persisted end state.
- [ ] `tests/Hexalith.Folders.AppHost.Tests/README.md`: document prerequisites, exact commands, fixture ownership and evidence interpretation.
- [ ] `_bmad-output/implementation-artifacts/evidence/story-12-1-durability-2026-10-17.json`: retain compact source/pin-bound commands, outcomes and acceptance-row mapping without credentials or protected payloads. Update this spec with observed results and blockers; preserve manifest authority and existing sprint status.

**Acceptance Criteria:**
- Given the selected EventStore source, when compatibility runs, then supported Folders contracts pass and unrelated platform failures remain separately identified.
- Given the opted-in local topology, when authorized mutations, restart and replay execute, then retained events and reconstructed persisted state agree without direct domain-state writes or historical-byte changes.
- Given denied authority or failed dependencies, when protected operations execute, then canonical responses and unchanged persisted contents prove safe failure.
- Given final evidence, when each Story 12.1 criterion is audited, then each has current evidence or an explicit blocker; passing source tests alone cannot close delivery acceptance.

## Implementation Notes

- Investigation baseline: clean `main`, Folders `2d19088505ef40c6aaef78480127f11a6077a2c0`; selected EventStore `77d9af18a54d92506e310fe5a45021a395e66f60`. Latest supplement binds `6394d0500e6e277b8975cb397c9f0389f9a16929`.
- Intent gaps: none. External irreversible actions: none; only disposable local test state is mutated. Footprint: acceptance fixture/tests, compatibility/evidence records and documentation; no new production interface is planned.
- Server baseline: stale source/package assets caused initial `CS1704`; explicit Debug/source restore and rebuild passed with zero warnings/errors. No source fix was needed.
- Refreshed stale `epic-12-context.md`. Original execution/review tasks are complete; manifest delivery remains open. Scoped authorization and exposure controls remain unchanged.

## Spec Change Log

## Review Triage Log

## Verification

- Restore Debug/source references: `dotnet restore Hexalith.Folders.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -p:NuGetAudit=false -m:1 -nr:false -v:q`.
- Build each affected project using `dotnet build <project> -c Debug --no-restore -p:UseHexalithProjectReferences=true -m:1 -nr:false -p:UseSharedCompilation=false -v:q`; run its built xUnit assembly directly.
- Execute the new acceptance class with `HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION=true` and `-class Hexalith.Folders.AppHost.Tests.FolderDurabilityAcceptanceTests`; require no failures/skips.
- Run relevant existing regression suites, current-pin platform classes, the Debug solution build and scoped whitespace checks. Record exact expanded commands/results; infrastructure blockers do not become skips or completion.
