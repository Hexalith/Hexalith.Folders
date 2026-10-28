---
title: '1.17 Qualify the supported source-host graph'
type: 'bugfix'
created: '2026-10-27'
status: 'done'
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
- [x] `T/Hexalith.Builds.Module.Tests.csproj`, `T/Workspace/WorkspaceAppHostTests.cs` — after checkout/gitlink prerequisites are restored, pass the original source graph and all original-assembly class gates. This run stopped before mapping materialization; Git realignment is outside this approved scope.

**Acceptance Criteria:**
- Given production-resolved imports, when the original Module.Tests project builds, then actual hosts use mapped Debug projects without duplicate Hexalith packages or private reference replacement.
- Given controlled SDK invocation, when launcher tests and all three workspace classes execute, then every selected test passes and child controls are demonstrated.
- Given failed prerequisites, when recording results, then dependent execution stops and neither this slice nor Story 1.17 earns completion.

## Implementation Notes

2026-10-27: Jerome approved this specification with `approve and continue`. Its frozen intent is now locked; tracker transitions remain excluded.

No intent gaps or irreversible actions. Footprint: Builds reconciliation/tests/docs and Folders evidence. No SDK ran during planning.

## Spec Change Log

## Review Triage Log

### 2026-10-28 independent review

All three review layers completed before triage. Each finding is retained below before grouping. Fixes remain inside the approved source-host qualification intent; no new runtime exposure, pin, public product surface or approval is introduced.

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| Blind 1 | medium | The launcher falls back to ambient cache/temp variables at CompositionSdkProcess.cs:73–75, so a normal invocation can use shared locations. Default to its owned private tree; keep explicit fixture selections. Patch. |
| Blind 2 | medium | The actual task probe confirms percent escapes are decoded again when canonical metadata is cloned. Literal semicolons currently fail closed rather than enabling an extra assignment; preserve literal percent roots and cover encoded-path round trips. Group with Edge 1/2; patch. |
| Blind 3 | medium | The qualification interception test returns success without probing when its required flag is absent. Explicitly skip that qualification-only assertion when no harness is supplied; the qualified run still requires it to execute. Patch. |
| Blind 4 | medium | DOTNET_ROOT uses the lexical executable parent, which is wrong for a symlink such as /usr/bin/dotnet. Derive the installation directory from the resolved executable while retaining the selected launch path. Patch. |
| Blind 5 | medium | A verified package mapping enters the new source-only Platform assertion and fails despite the conditional project reference excluding that model. Preserve ordinary package invocation by explicitly excluding this source-model assertion. Patch. |
| Blind 6 | medium | The old version-only parser removed empty segments; the replacement rejects catalog-matching Version=1.0.0; metadata. Preserve that established version-only behavior without accepting empty segments in new forwarding lists. Patch. |
| Blind 7 | false | The frozen intent qualifies a Debug source build boundary and explicitly excludes application launch/ordinary execution. The replacement checks compiler success and actual restored project/package origins, including the real graph; runtime output is outside that intent. No incorrect selected origin was demonstrated. |
| Blind 8 | medium | A one-second timeout can expire during SDK startup before the waiting descendant exists; the test also checks no descendant identity. Require an observed child-start/PID marker and prove that owned child terminates. Patch. |
| Blind 9 | medium | The launcher-owned default temporary cache tree has no lifetime cleanup and can accumulate downloaded packages across ordinary test processes. Clean only that owned tree at test-process disposal; preserve caller-selected directories. Patch. |
| Blind 10 | medium | All unsafe-assignment rows create a symbolic link, so Windows without symlink privileges loses unrelated coverage during fixture setup. Create it only for the escape row and explicitly skip only that row when unavailable. Patch. |
| Edge 1 | medium | The retained in-process task probe accepted a mapped %20 root but emitted a different space-containing root. Literal metadata copying fixes the observed double decoding. Group with Blind 2; patch. |
| Edge 2 | medium | The same actual task probe contradicts the approved canonical-root claim: successful Execute changed the root bytes. This distinct claim finding shares Edge 1/Blind 2 root cause. Patch. |
| Verification 1 | medium | The pre-verified child-evaluation marker is ineffective: NewDirectory contains an apostrophe and File.WriteAllText is unavailable under ordinary MSBuild property-function policy. Repair it and demonstrate a positive control under the same fixture/environment before asserting early refusal. Patch. |
| Parent 1 | medium | Review-patch inspection found vacuous All on the filtered version-only list, which would accept separator-only metadata such as ;;;. Require at least one real version assignment and preserve rejection coverage. Group with Blind 6; patch refinement. |
| Parent 2 | medium | The new ambient-cache regression mutates process-wide temp/NuGet variables and could redirect another test class during normal parallel execution. Put its class in the existing non-parallel environment/process collection. Group with Blind 1; patch refinement. |

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

## Current approved-scope resume — 2026-10-28

The `bmad-build 1.17` invocation resumes this approved local source-host qualification gate. Its original baseline, frozen intent and existing task values remain unchanged. The current clean Folders HEAD is `5fb5cfcfcda10044faccc71b48f8a3d6c08c5b87`; Builds HEAD is `e4e4568aa59050b5890c60ff4a1f39bcc83399b4`. All twelve root-declared checkout HEADs match both committed and indexed root gitlinks, and all thirteen repositories are clean. A fresh session preservation snapshot is `/tmp/story117-20261028-preflight-_ulgq2s5/repositories.json`; tracked-file before-images are hashed in the adjacent `tracked-files.json`. This is a new-session observation, not a rewrite of any historical failed-run result.

Resume production resolution/materialization, the original Debug Module.Tests graph build and all four original-assembly class gates. Recheck stable repository HEAD/index identities before dependent execution. Override the existing global `HexalithG4FixtureArtifacts` property so executable fixtures stay in fresh private outputs. Demonstrate serial/certificate/environment controls for SDK descendants, including catalog PowerShell children, before relying on their results. The remaining test project/WorkspaceAppHostTests task includes repairing private-output discovery so the required Platform assertion executes against the verified root-declared source mapping instead of skipping. Preserve existing production process semantics and never initialize nested references or replace mapped roots.

Use additive 2026-10-28 evidence and exact command/results. Read-only diffs replace generic staging. Preserve the parent closure spec, tracker, historical evidence and authority records; no automatic tracker transition is authorized. Keep V1Only, T0 unset, activation authority null, and staging 0/14. No application/topology launch, publication, deployment, installation, Git write or dependency update is authorized. Review this slice only after the original graph and all required class gates pass without skipped required assertions; otherwise retain the precise failed gate and leave it in-progress.

## Reviewed completion — 2026-10-28

All three review layers completed. The triage log retains all fifteen findings: fourteen medium findings were corrected in ten patch groups, and one finding was refuted against the frozen build-only intent. Nothing was deferred. Parent inspection also closed the separator-only metadata and process-wide test-environment refinements. The frozen intent and original baseline remain unchanged.

Independent final verification in `/tmp/story117-parent-final-20261028-2y6gpdgk` rebuilt the current production mapping and original Debug Module.Tests graph, then passed every required original-assembly class: 17 launcher, 139 workspace, 34 catalog and 12 AppHost assertions, totaling **202/202**, with zero errors, failures, skips or not-run cases. The required Platform delegation assertion executed. All 29 original/fixture restored graphs contain zero Hexalith package libraries; the six tested source files remained byte-identical through final execution. The earlier 192-test implementation result is preserved separately and is not used as final-byte evidence.

The current slice and preceding source-host mapping slice now satisfy their local build-boundary acceptance. See [reviewed final results](story-1-17-supported-source-host-qualification-final-2026-10-28.md) and [final receipt](evidence/story-1-17-supported-source-host-qualification-resume-2026-10-28.review-final.json). Four pre-existing MSB3277 host assembly-conflict warnings remain. A broad historical-root diff also retains its pre-existing Parties object hash-mismatch limitation; current owned diffs, exact source bindings and all selected gates are independently verified.

The explicit no-Git-writes and no-automatic-tracker-transition constraints override generic workflow commit/tracker steps. No commit, staging, push, publication, deployment, reference update or application/topology startup occurred. Parent Story 1.17 remains in-progress, tracker backlog, routing policy V1Only, T0 unset, activation authority null and staging 0/14. Distribution/ordinary-execution and release/hosted acceptance remain separate prerequisites.
