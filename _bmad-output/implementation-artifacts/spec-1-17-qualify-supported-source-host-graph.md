---
title: '1.17 Qualify the supported source-host graph'
type: 'bugfix'
created: '2026-10-27'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'f48886763a1b57b30f7767deb9909f741dc812f3'
story_key: '1-17-publish-the-pd10-v2-authorization-contract-spine'
tracker_policy: 'no-automatic-transition'
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/references/Hexalith.Builds/DEVELOPMENT.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
  - '{project-root}/docs/governance/approval-policy.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The committed source-host change remains unqualified: FrontComposer forwarding triggers HXW006, and previous fallback assertions lacked child SDK controls.

**Approach:** Accept only forwarding that preserves the verified source mapping, canonicalize equivalent roots, and qualify the original Debug graph using controlled SDK children.

## Boundaries & Constraints

**Always:** Derive roots/origins from verified mapping. Preserve catalog Version, Aspire metadata, configuration guards and conversion refusals. Use bounded SDK 10.0.402 commands, explicit certificate-prevention, private outputs/caches and serial controls throughout. Parent remains in-progress; tracker backlog; V1Only; T0 unset; activation authority null; staging 0/14.

**Never:** Accept arbitrary overrides, probe replacement roots, fabricate hashes or use Hexalith package fallback. Change no FrontComposer source, pins, production process semantics or historical evidence. No application/topology launch, consumed loader replay, publication/deployment, installation, reference initialization or Git writes.

## I/O & Edge-Case Matrix

| Scenario | Input | Expected behavior | Failure |
| --- | --- | --- | --- |
| Forwarding | Retained source reference with mapped roots/origins | Accept; clone canonical root spelling | Child guards remain effective |
| Equivalent paths | Relative segments/trailing separators | Verify physical identity; canonicalize before child evaluation | Different/escaped roots refuse |
| Unsafe metadata | Unknown, malformed, duplicate, conflicting, unmapped or configuration assignment | HXW006 before dependent restore/compilation | No silent removal/fallback |
| Existing behavior | Version-only, package/generated hosts, Aspire library | Preserve catalog/isolation/metadata rules | Existing refusals survive |
| SDK children | Build/restore/pack/msbuild or probe | Explicit executable/environment; supported serial options; unrelated secrets scrubbed | Missing controls block execution |

</frozen-after-approval>

## Code Map

Paths are relative to `references/Hexalith.Builds`, base `cbe58fa72f92327752fe0d75617ae91fe18da5d9`; `T` means `test/Hexalith.Builds.Module.Tests`.

- `src/libraries/Hexalith.Builds.Tooling/Workspace/SourceMappingReconciliationTask.cs`: reuse `SourceMapping.LoadVerified`, path utilities and reference cloning.
- `src/libraries/Hexalith.Builds.Tooling/Workspace/SourceMappingMaterializer.cs`: retain imports and exact child guards.
- `T/CompositionTestFiles.cs`, `T/CompositionProcessTests.cs`: reuse fixtures and scrubbing patterns.
- FrontComposer `deps.local.props` at `8d2b47bd206ffd5c5e77a5ac0461b9cfe52ea4fd`: read-only forwarding input.

## Tasks & Acceptance

**Execution:**
- [x] `src/libraries/Hexalith.Builds.Tooling/Workspace/SourceMappingReconciliationTask.cs` — validate source-mode retained assignments against mapping, preserve permitted Version, and canonicalize accepted roots in cloned metadata.
- [x] `T/CompositionSdkProcess.cs`, `T/CompositionSdkProcessTests.cs` — add test-only validated absolute SDK selection, explicit safe environment and command-specific serial options. Test start descriptions and bounded child propagation; preserve normal test invocation defaults.
- [x] `T/Workspace/WorkspaceMsBuildTests.cs` — use launcher; cover every matrix row with materialized imports, child evaluation and restore assets, including mixed origins and unchanged input metadata.
- [x] `T/Workspace/CatalogVersionTests.cs` — use launcher; retain host/catalog refusal coverage.
- [x] `README.md` — document forwarding and limits.
- [x] Folders `_bmad-output/implementation-artifacts/story-1-17-supported-source-host-qualification-results.md` — retain exact commands, revisions, mapping, totals/log identities and blockers; append verification to the prior source-host spec without changing frozen intent.

- [x] `T/Hexalith.Builds.Module.Tests.csproj` — copy the SDK Microsoft.Build assembly beside existing SDK references; the focused run demonstrated its absence, and the corrected in-process assertions passed.
- [ ] `T/Hexalith.Builds.Module.Tests.csproj`, `T/Workspace/WorkspaceAppHostTests.cs` — after checkout/gitlink prerequisites are restored, pass the original source graph and all original-assembly class gates. This run stopped before mapping materialization; Git realignment is outside this approved scope.

**Acceptance Criteria:**
- Given production-resolved imports, when the original Module.Tests project builds, then actual hosts use mapped Debug projects without duplicate Hexalith packages or private reference replacement.
- Given controlled SDK invocation, when launcher tests and all three workspace classes execute, then every selected test passes and child controls are demonstrated.
- Given failed prerequisites, when recording results, then dependent execution stops and neither this slice nor Story 1.17 earns completion.

## Implementation Notes

2026-10-27: Jerome approved this specification with `approve and continue`. Its frozen intent is now locked; tracker transitions remain excluded.

No intent gaps or irreversible actions. Footprint: Builds reconciliation/tests/docs and Folders evidence. No SDK ran during planning.

## Spec Change Log

## Review Triage Log

## Verification

Recheck Git state and SDK/global.json identities. Resolve/materialize imports through production APIs; use fresh private paths, explicit environment and bounded serial Debug commands. Prove launcher construction before SDK child tests.

From Builds, build `test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj --configuration Debug -m:1` with those imports. Invoke its assembly directly using `-class` and serial test execution for `Hexalith.Builds.ModuleTool.Tests.CompositionSdkProcessTests`, then `Hexalith.Builds.ModuleTool.Tests.Workspace.WorkspaceMsBuildTests`, `CatalogVersionTests` and `WorkspaceAppHostTests` separately with fully qualified names. Existing controlled listeners/model inspection are allowed; application startup is excluded.

Run `git diff --check` in Builds/Folders; compare protected tracker, approval, catalog, pin, history and index state. Record exact blockers; fallback cannot replace the graph gate. Close the prior slice after checks/review; parent closure stays separate.

2026-10-27 implementation handoff: Builds reconciliation, the test-only SDK launcher, workspace/catalog coverage and README are implemented. Focused verification exposed a missing SDK Microsoft.Build runtime assembly for in-process task tests; the original test project now copies it beside its existing SDK Framework/Utilities references, without replacing project references or pins. The reused independent lane passed 180 final class tests. Production resolution failed before materialization on checkout/gitlink drift; original graph/original-assembly gates remain unrun. All setup failures and the unchanged authority limits are retained in [results](story-1-17-supported-source-host-qualification-results.md) and [receipt](evidence/story-1-17-supported-source-host-qualification-2026-10-27.json). No review/done transition is made by this handoff.

## Parent implementation audit — 2026-10-27

Implemented code/documentation tasks are complete. The independent focused lane passed 180/180 tests (14 launcher, 132 workspace, 34 catalog), and each matrix row has passing coverage there. The original source graph and original-assembly gates remain unrun: production resolution refused checkout/gitlink drift before creating a mapping. Acceptance is incomplete; status stays in-progress and Step 04 review/completion were not entered.

The [parent verification](evidence/story-1-17-supported-source-host-qualification-2026-10-27.parent-verification.json) independently rehashes 44 retained artifacts and seven implementation files, parses final XML totals, and compares the pre-implementation baseline across all thirteen repositories. Nine dependency HEADs advanced externally during this run; unrelated source/index changes were retained. The root tracker, parent closure spec, authority policy, planning manifest, candidate conformance set and .gitmodules match their initial hashes. Both repositories pass git diff --check. The frozen approved intent is unchanged.

## Approved resume — 2026-10-27

Jerome explicitly requested resumption of the approved implementation: first verify all root-declared submodule checkout HEADs match both committed and indexed root gitlinks and repository revisions remain stable; then run production mapping, the original Debug Module.Tests build, and all four original-assembly class gates, including WorkspaceAppHostTests. The prior 180 focused passes remain fallback evidence only. Complete Step 04 review only if those gates pass. Preserve unrelated work and historical evidence. Do not commit, push, deploy, initialize/update references, change Git state, or update the parent tracker. If alignment fails, stop dependent execution and report exact mismatches.

Resume preflight found all 12 root-declared checkouts aligned and all 13 repositories clean, with unchanged HEAD/index identities on repeat observation. Folders HEAD is `c8a727269f87c8daf61c303bfedc07fc795cbe1a`; Builds HEAD is `05ae0a23adfb516231b31da6d83989bdf229f2f9`. The original baseline_commit and frozen intent remain unchanged. A new preservation baseline records all repository identities and 7,777 protected file hashes in `/tmp/story117-resume-yog4l9xp/baseline.json`; repeat observation is `/tmp/story117-resume-yog4l9xp/stability-pre-dispatch.json`. Use a fresh private execution directory and additive resume evidence rather than overwriting prior receipts/results. Do not change the parent closure spec or sprint-status.yaml. Append to the preceding source-host spec only after successful checks/review as required above.

## Resume verification outcome — 2026-10-27

Production resolution/materialization passed with mapping `4f249512816016269112568ced0e4468c50fa7cef133c4690627c763e0e0773c`. The original Debug build was interrupted when existing executable-fixture output overrides escaped the private tree; its zero exit code earns no pass and no original assembly exists. Before the corrected retry, the stability gate observed external Folders HEAD drift from `c8a727269f87c8daf61c303bfedc07fc795cbe1a` to `30024478b78a87c7fb55bf4f6d8b29c4bf4c2091` and index SHA-256 drift from `74961aeb9c943fdf90976af41567ce5edd66e644f5e5829770a893bc8eef2640` to `87370445654dd0060c32f4bff6dc31fa18d84e30a932fa804a1657a936b61c37`. All 12 submodules remained aligned and unchanged. The retry stopped before SDK execution; all four original-assembly class gates and Step 04 review remain unrun. The prior 180 passes remain fallback evidence only. No task/status or parent-tracker transition is earned.

Additive [resume results](story-1-17-supported-source-host-qualification-resume-2026-10-27.md) and [resume receipt](evidence/story-1-17-supported-source-host-qualification-resume-2026-10-27.json) preserve exact commands, interrupted-build semantics, missing assembly, child-control/output/cache limits and source identities. The independent [resume parent verification](evidence/story-1-17-supported-source-host-qualification-resume-2026-10-27.parent-verification.json) rehashes the 48 new bindings and 44 historical bindings and checks preservation. The parent closure spec, tracker and frozen approved intent remain unchanged; unrelated external changes were retained.
