---
title: 'Story 12.1: complete current-pin durable repository acceptance'
type: 'feature'
created: '2026-10-17'
status: 'in-progress'
baseline_commit: 'fa0668670a913d5f2ca95f43d8bba46716e1e1b4'
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
- [x] `_bmad-output/implementation-artifacts/story-12-1-eventstore-consumer-compatibility-2026-10-17.yaml`: record current-pin evolution, synchronous dispatch, receipt, stream-integrity and actor recovery/conflict checks. Report asynchronous-dispatch results separately; preserve earlier supplements.
- [x] `tests/Hexalith.Folders.AppHost.Tests/FolderDurabilityAcceptanceFixture.cs`: add a dedicated opt-in fixture using the existing AppHost, real identity/tenant bootstrap, discovered endpoints, isolated retained storage and bounded resource restart/failure controls. Keep each helper type in its own file.
- [ ] `tests/Hexalith.Folders.AppHost.Tests/FolderDurabilityAcceptanceTests.cs`: exercise organization baseline/grant, public folder creation, positive ACL read, equivalent/conflicting intent, persisted contents, host restart, empty-checkpoint replay, revocation/foreign-tenant denial and infrastructure failures. Cover the matrix; require zero skips when opted in and verify retained hashes and persisted end state.
- [x] `tests/Hexalith.Folders.AppHost.Tests/README.md`: document prerequisites, exact commands, fixture ownership and evidence interpretation.
- [x] `_bmad-output/implementation-artifacts/evidence/story-12-1-durability-2026-10-17.json`: retain compact source/pin-bound commands, outcomes and acceptance-row mapping without credentials or protected payloads. Update this spec with observed results and blockers; preserve manifest authority and existing sprint status.

**Acceptance Criteria:**
- Given the selected EventStore source, when compatibility runs, then supported Folders contracts pass and unrelated platform failures remain separately identified.
- Given the opted-in local topology, when authorized mutations, restart and replay execute, then retained events and reconstructed persisted state agree without direct domain-state writes or historical-byte changes.
- Given denied authority or failed dependencies, when protected operations execute, then canonical responses and unchanged persisted contents prove safe failure.
- Given final evidence, when each Story 12.1 criterion is audited, then each has current evidence or an explicit blocker; passing source tests alone cannot close delivery acceptance.

## Implementation Notes

- Investigation baseline: clean `main`, Folders `2d19088505ef40c6aaef78480127f11a6077a2c0`; selected EventStore `77d9af18a54d92506e310fe5a45021a395e66f60`. Latest supplement binds `6394d0500e6e277b8975cb397c9f0389f9a16929`.
- Intent gaps: none. External irreversible actions: none; only disposable local test state is mutated. Final footprint: acceptance fixture/tests, compatibility/evidence records and documentation, plus demonstrated bounded permission-delivery and public authentication/gateway repairs; no new HTTP surface.
- Server baseline: stale source/package assets caused initial `CS1704`; explicit Debug/source restore and rebuild passed with zero warnings/errors. No source fix was needed.
- Refreshed stale `epic-12-context.md`. Original execution/review tasks are complete; manifest delivery remains open. Scoped authorization and exposure controls remain unchanged.

- Current execution baseline is Folders `fa0668670a913d5f2ca95f43d8bba46716e1e1b4`, with EventStore gitlink and clean source HEAD `77d9af18a54d92506e310fe5a45021a395e66f60` unchanged. No dependency, submodule, historical acceptance, manifest or sprint-status mutation is part of this change.
- The authenticated baseline exposed concurrent permission callbacks: EventStore retained and published two organization events, while one authenticated Workers callback returned 500 before its predecessor finished. Workers now reevaluate only explicit sequence-gap/CAS concurrency failures within one second; payload, identity, invalid-order and replay conflicts still fail immediately. Cancellation propagates and exhausted delivery returns the existing safe failure without advancing a row. This is bounded callback processing; broker/operator recovery remains outside scope.
- The fixture isolation audit found standalone Dapr 1.18 mDNS announces app IDs without the actor namespace. All earlier live attempts are superseded diagnostic observations, not acceptance. The corrected model uses unique sidecar app IDs, copied component scopes/ACL identities/resiliency targets, explicit domain registrations and matching real Keycloak workload audiences. The signed workload caller must match Dapr's generated caller app ID: the disposable client identity, receiving allow-lists and Folders receipt expected workload are mapped together. Human receipt signatures/bindings and generated app-channel tokens remain enforced.

- The fully isolated, identity-mapped run completed real tenant/owner provisioning, the authenticated organization baseline and its persisted sequence-2 permission row. Public v1 preflight then returned `eventstore_validator_denied`: it required a receipt before reaching the issuing gateway. The validator now accepts bound authenticated human tenant/subject/action evidence only for catalogued direct public v1 mutations; `/process`, unknown routes, v2 and unauthenticated callers still require/deny as before. The domain callback still verifies the gateway-issued receipt.
- Production public gateway composition previously had neither a base address nor human bearer forwarding. The host now uses the configured `EventStore:Gateway:BaseAddress` or Aspire-discovered `http://eventstore` endpoint and relays only a validated known public mutation's bearer. Typed SDK transport tests exercise the actual production DI composition with a recording destination and prove wrong-route/unauthenticated calls never reach it.

- The driver uses the existing v1 gateway denial mapping (`404`, `not_found` category/code) in addition to pre-gateway safe-denial variants, with nondisclosure and stream/row absence checks. Grant/revoke payloads are submitted one operation per authenticated command, as required by the trusted adapter. A failed run retained a second-grant post-`EventsStored` failure caused by EventStore's anonymous-consumer `429`; the acceptance driver now spaces command submissions by one second and polls at 500 ms under the unchanged limiter.

- Canonical Aspire `stop` reproduced the earlier restart stop-phase failure. A dump of the exact task-owned Folders process established `Host.DisposeAsync` waiting in OpenTelemetry batch exporter shutdown while OTLP log/metric/trace transmission retried. The fixture now bounds `OTEL_EXPORTER_OTLP_TIMEOUT` and the `TRACES`, `METRICS`, `LOGS` variants to 1000 ms only on disposable project resources. Host replacement uses canonical separate stop/start commands, observes exit and health, and requires a changed process ID. Failed stop commands never count as restart evidence; production exporter defaults remain unchanged.

- Live implementation is present, but full delivery acceptance remains open. Prior fully isolated candidates observed fresh managed tenant/owner authority, organization baseline plus three single-operation grants through sequence 5, public folder acceptance, matching persisted folder/ACL state, a metadata-only retained event, and unchanged retained hashes on equivalent/conflicting intent. No single full matrix test passed. OTLP bounds let the first project exit; Aspire reported `Finished`, so the fixture now waits for that project state rather than container `Exited`. Three-host PID replacement, restart reconstruction, replay and the later negative rows are still unobserved.
- The final candidate failed initialization (1 failed, 0 skipped, 132.959 s): all six generated Dapr app IDs fatally failed component-directory watcher creation with `no space left on device`. Same-user visible inotify watch entries were 1,048,526 against `max_user_watches=1,048,576`; `/tmp` still had approximately 78 GB and 56 million inodes free. Shared Codex/MCP `fluentui-mcp` processes across repositories consume most watches. Those processes and global limits were preserved. Task-owned topology/Redis were removed; FrontComposer and other unrelated AppHosts remain running. This host prerequisite blocks the final lane and cannot become a skip or acceptance credit.

## Spec Change Log

- 2026-10-17: added current-pin compatibility and the repeatable authenticated lane. The accepted intent is unchanged. Production repairs are bounded re-evaluation of concurrent permission delivery and a configurable exact receipt workload identity (default `eventstore`). The isolated trace established `caller-conflict` before this identity mapping; invalid configured identities fail closed. Default, exact configured identity, caller mismatch and invalid-configuration cases have focused regression coverage. The same accepted path exposed and repaired public v1 bearer preflight and gateway endpoint/bearer composition; callback receipt verification remains required.

## Review Triage Log

## Verification

- Restore Debug/source references: `dotnet restore Hexalith.Folders.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -p:NuGetAudit=false -m:1 -nr:false -v:q`.
- Build each affected project using `dotnet build <project> -c Debug --no-restore -p:UseHexalithProjectReferences=true -m:1 -nr:false -p:UseSharedCompilation=false -v:q`; run its built xUnit assembly directly.
- Execute the new acceptance class with `HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION=true` and `-class Hexalith.Folders.AppHost.Tests.FolderDurabilityAcceptanceTests`; require no failures/skips.
- Run relevant existing regression suites, current-pin platform classes, the Debug solution build and scoped whitespace checks. Record exact expanded commands/results; infrastructure blockers do not become skips or completion.

Observed 2026-10-17 results:

- Explicit Debug/source restore and full solution build passed; final production build reported zero warnings/errors. Current-pin compatibility passed 246 EventStore Server, 35 Client and 12 DomainService checks. The named async dispatch rerun passed and is included in those 12.
- Full Folders Server (998) and Workers (146) regressions passed with zero failures/skips, including receipt, exact workload identity, public route/tenant/action binding, actual typed gateway transport, callback sequence/CAS retry and cancellation. The selected 125/29 lanes are subsets: 1,437 distinct source checks total.
- The final fixture build, scoped whitespace verification and `git diff --check` passed. Exact expanded commands, log hashes, source/binary bindings and superseded candidate diagnoses are retained in the two new evidence records. No submodule source/pin, historical acceptance, manifest authority or sprint status changed.
- Final live command: `HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION=true dotnet tests/Hexalith.Folders.AppHost.Tests/bin/Debug/net10.0/Hexalith.Folders.AppHost.Tests.dll -class Hexalith.Folders.AppHost.Tests.FolderDurabilityAcceptanceTests -showLiveOutput`; exit 1, 1 failed, 0 skipped. Watcher ENOSPC blocked initialization. Leave the full live execution task and restart/replay/remaining-denial/dependency acceptance rows open; spec status remains `in-progress`.

- Parent implementation audit: read the complete tracked/untracked unified diff from baseline `fa0668670a913d5f2ca95f43d8bba46716e1e1b4` without staging. Source, binary, log and authority SHA-256 bindings match; actual summaries confirm 1,437 distinct passing source checks and zero-warning Debug builds. Frozen intent, root gitlinks, manifest, sprint status and empty index are preserved. The matrix audit cannot pass because the opted-in final test failed before its body. A fresh read still found 1,048,541 visible same-user watches against 1,048,576 allowed, with shared `fluentui-mcp` processes consuming 1,044,412. Step 03 remains blocked at live verification; step 04 independent review and completion are not reached. Resume the documented live class after host watch capacity is available; do not change shared processes or global limits as part of this story.
