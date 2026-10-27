---
title: '1.17 Qualify a current Builds source host against EventStore 3.128'
type: 'chore'
created: '2026-10-26'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '19d8ddc9d64b0af4986b2a8fafe859b8b4e8231a'
story_key: '1-17-publish-the-pd10-v2-authorization-contract-spine'
tracker_policy: 'no-automatic-transition'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The signed CLI 4.38 host failed both retained loader cases when EventStore 3.128 was demanded. Current Builds already selects 3.128; its source host/model combination remains unqualified.

**Approach:** Build an isolated Debug source host and matching Platform model, then conditionally execute two bounded loader cases. Produce a diagnostic bundle and owner handoff. This new source tuple supplies binding evidence only.

## Boundaries & Constraints

**Always:** Preserve sources, revisions, indices, pins and historical records. Use fresh private outputs. Keep parent in-progress, tracker backlog, V1Only, T0 unset, activation authority null and staging checks 0/14. Approval covers the conditional sequence; derived hashes need checks, not another signature.

**Never:** Reuse consumed approvals, patch the signed package, downgrade the model, change operational code/configuration, initialize submodules, install tools, stage/commit/push, publish/deploy, invoke ComposeRun/application entrypoints, or invent ordinary mapping/ContentHash. Retain all historical procedural limits.

## I/O & Edge-Case Matrix

| Condition | Required result |
| --- | --- |
| Source graph/builds verified | Seal output identities; run cold and host-first once each. |
| SDK/source/isolation drift, package fallback or build failure | Stop dependent execution; retain exact failure. |
| Binding succeeds | Report actual identity/path/hash/MVID; no broader acceptance. |
| Binding fails or times out | Preserve failure; no automatic retry or repair. |

</frozen-after-approval>

## Code Map

- `story-1-17-current-builds-host-investigation-2026-10-26.json` (beside this spec) binds thirteen full revisions/indices, source/evidence hashes and the selected catalog. Recheck before execution. Current Builds has nineteen production/catalog changes since signed 4.38.
- Builds `src/hosts/Hexalith.Builds.Module.{AppHost,EventStoreHost,UiHost}/`: unconditional EventStore.Aspire/Gateway and FrontComposer.Shell packages; source flags do not convert them. SourceMappingMaterializer excludes two hosts. AppHost `RunTopology.ComposePlatform` uses the Default-context resolver.
- Builds `src/libraries/Hexalith.Builds.Module.Cli/` packages Release host directories. Platform `src/Hexalith.Platform.Composition/` consumes Tooling. Use a Debug bundle with rebuilt model; package/layout qualification stays open.

## Tasks & Acceptance

Harness files below live under `eng/story117-current-builds-host/`; records under `_bmad-output/implementation-artifacts/`.

- [x] `plan.json`, `qualify.py` — bind inputs, exact commands/environments and ordered gates. Allow recording-only root descendants after protected-byte checks; retain actual revisions/indices.
- [x] `source-references.targets` — replace the three host packages in private snapshots; propagate supported transitive source/root selectors. Verify evaluated references/assets and reject Hexalith package fallback. Claim no ordinary mapping.
- [x] `qualify.py` — build AppHost/CLI and Platform Composition in Debug, serially, using private artifacts/caches and matching Tooling. Capture compiler inputs, assets, dependency files and outputs. No Release build or pack.
- [x] `LoaderProbe.cs`, `qualify.py` — seal outputs, then run cold/host-first with unchanged generated host deps/runtimeconfig. Inspect ComposeRun metadata and demand bound EventStore/Aspire/Tooling/Redis identities; label optional demands. Explicitly invoke no library method/constructor.
- [x] `tests/test_qualification_guards.py` — check drift, package fallback, stale outputs, launch gates, descriptor reuse, timeout and corruption before SDK use.
- [x] `story-1-17-current-builds-host-results.md`, `evidence/story-1-17-current-builds-host-*` — retain portable evidence, read-only verifier, preservation checks and release-owner handoff.

**Acceptance Criteria:**

- Given approval and valid inputs, when reporting, then every new result binds exact host/model bytes and remains distinct from historical evidence.
- Given either outcome, when reporting, then prior 4.38 failure, package/API/ordinary/hosted acceptance limits and parent/tracker state remain intact.
- Given a failed gate, when stopping, then retain its exact command/result and owner prerequisite without weakening checks.

## Implementation Notes

- 2026-10-26 parent task audit: all six tasks and four matrix rows verified against the diff, retained guard receipts and raw sealed execution records. Three Debug builds, probe compilation and cold/host-first each passed; 31 guards passed normally and optimized. Independent optimized integrity replay verified all 17,208 members. Four preparation/build failures remain immutable. The approved frozen scope is unchanged; full parent story and tracker remain open.

- 2026-10-26: Jerome approved this spec with `approve and continue`. Approval covers the conditional private Debug build and two loader cases in its frozen scope; no previous one-shot approval is reused. The explicit tracker and no-staging restrictions override generic workflow defaults.

## Spec Change Log

## Review Triage Log

All three layers completed. Every finding was verified or accepted under the verification-gap evidence contract before grouping; all route to bounded implementation/test/report patches. No frozen intent change or target rerun is required.

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| B1 | medium | Final plan includes MSBuild.dll but omits Microsoft.Build.dll, Microsoft.Build.Tasks.Core.dll and NuGet.Build.Tasks.dll. Current binding observations stand; expand future input coverage and disclose the historical hash boundary. Patch. |
| B2 | medium | Execution preflight checks precede the try/finally capture block, so input drift can exit without a portable stop record. Retain such failures without launching targets. Patch. |
| B3 | medium | Parent benign-process reproduction wrote a command receipt then failed its post-check; Qualification.commands remained empty. Record launched work even when a later check raises. Patch. |
| B4 | medium | Parent benign child deliberately changed groups; it remained alive after the original group cleanup. Its observed PID/start identity was recorded and parent cleaned it afterward. Terminate known survivors and verify final state. Patch. |
| B5 | medium | Parent mocked-stream reproduction with runtime description 10.0.13 and a preflight failure launched both cases. Classify runtime/preflight failures as fatal and stop the second case. Patch. |
| B6 | medium | Preparation checks feed bytes, but later inputs omit them and the sandbox mounts their work tree writable. Enforce read-only prepared feed and retain its bindings. Patch. |
| B7 | medium | Package/assets guards do not inspect direct Reference/HintPath binaries. The import records ReferencePath without rejecting external Hexalith assemblies; validate those existing compiler inputs against verified source outputs. Patch. |
| B8 | medium | The probe records returned identities but the driver accepts complete without comparing resolved path/hash to the seal. Validate existing resolved observations; independently observed successful bindings already match the sealed files. Patch. |
| B9 | medium | Current verifier tests stop at receipt validation or call safe_name only. Add valid synthetic outer-hash fixtures exercising duplicate manifest/tar paths, special/missing/corrupt members and a valid capsule. Patch. |
| B10 | medium | text=auto can convert new harness/JSON/Markdown bytes on checkout, breaking fixed hashes. Add narrow serialization attributes for this artifact family; operational build/runtime configuration stays unchanged. Patch. |
| E1 | medium | Parent Popen-OSError reproduction emitted no command receipt. Capture argv/environment and explicit not-launched status before returning the failed launch. Patch. |
| E2 | medium | Parent polling-interruption reproduction left its benign child running until parent cleanup. Ensure unconditional owned-child cleanup and retain interruption evidence before archiving. Patch. |
| E3 | medium | main finally calls finish then archive without handling finish failure; state/store observation errors can prevent outcome/archive creation. Retain observation failure as unknown and still seal available evidence. Patch. |
| V1 | medium | Pre-verified gap: testVerifierRejectsUnsafeAndDuplicatePaths never invokes verify with duplicate tar members. Removing the duplicate-member check would not fail existing tests; add a valid-hash duplicate-member fixture. Patch. |

Shared roots: B4/E2 owned-child finalization; B9/V1 archive negative coverage. Other findings remain separate. Historical sealed attempts remain immutable; later fixes and historical procedural limits receive a review supplement.

- 2026-10-26 resolution: all fourteen reviewed findings fixed and verified through the 49-test focused suite in normal and optimized Python, retained-event/reference audits and fixed-hash integrity verification. Original 17,208-member execution and 31-member review capsules remain unchanged. The post-review Python/MSBuild guards have not been executed with SDK/build/compiler/loader commands; historical coverage limits remain explicit in the results report. Root .gitattributes changed only to preserve this artifact family. No finding was deferred.

## Design Notes

Loading can trigger implicit initialization. Private imports and rebuilt Tooling limit conclusions to this diagnostic. Rollback abandons new outputs while retaining evidence/shared state.

## Verification

Assert SDK 10.0.402; rehash executable/global.json/compiler/runtime inputs immediately before dependent commands. Bind runtime 10.0.13 or stop. Set certificate prevention before first SDK use; retain explicit environments and store metadata. Require read-only external mounts and network/socket isolation for loader processes, with no fallback. Enforce 30-second SDK, 600-second build and 60-second case deadlines; terminate owned process groups and retain timestamped observations. Archive before private-output cleanup. Run narrow harness/build checks, normal/optimized integrity verification and preservation checks. Never run solution-level tests.
