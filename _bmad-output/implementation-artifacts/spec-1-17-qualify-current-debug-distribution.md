---
title: '1.17 Qualify the current Debug CLI and host distribution'
type: 'chore'
created: '2026-10-29'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'c3d295b15cd3e0c5f675d014033256458860603f'
story_key: '1-17-publish-the-pd10-v2-authorization-contract-spine'
tracker_policy: 'no-automatic-transition'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/story-1-17-supported-source-host-qualification-final-2026-10-28.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The completed source graph does not qualify the CLI/AppHost distribution ordinary commands execute. Dependencies have advanced beyond historical loader evidence.

**Approach:** Qualify a fresh Debug/source CLI, host and Platform model using production mapping and explicit host selection. Produce an artifact handoff for later execution. This selects a diagnostic distribution; accepted-tool replacement remains pending.

## Boundaries & Constraints

**Always:** Bind revisions, root gitlinks, dirty bytes, selected catalog, SDK and outputs. Recheck identities before dependent execution. Use SDK 10.0.402, private outputs/caches, certificate prevention, bounded processes and serial SDK controls. Preserve parent in-progress, tracker backlog, V1Only, T0 unset, activation authority null and staging 0/14.

**Never:** Start applications/topologies, invoke ComposeRun or `--validate-platform`, replay loader cases, or initialize references. Change no production semantics, pins, tools, history, parent spec or tracker. No Release packaging, installation, publication, deployment or Git writes. Label wrapper/API evidence separately from public-command behavior. Production repairs require a separate proposal.

## I/O & Edge-Case Matrix

| Scenario | Required behavior |
| --- | --- |
| Private source outputs | Explicit HEXALITH_G4_APPHOST selects the sealed host; ambient installed outputs confer no credit. |
| Factory precedence | Verify explicit override, adjacent packaged host and conventional Debug/Release fallback without launching them. |
| Model SDK child | Capture original controls and qualification additions separately; missing mandatory controls prevent SDK execution. |
| Drift, fallback or incompatible artifacts | Stop dependent qualification, preserve the first failure and identify its owner; never substitute roots or packages. |


### Approved synchronization amendment - 2026-10-29

Jerome instructed "sync submodules to latest and approve". This explicitly
authorizes root-declared dependency fetch/update and the narrow object restoration,
and supersedes the earlier no-Git-writes/no-pin-change constraints only for that
synchronization. Stage only the updated root gitlinks so the existing production
resolver can use the checked-out commits; preserve every implementation edit and
local branch. Do not commit, push, initialize nested references or start applications.
Bind both the older committed root gitlinks and the newly indexed checkout identities.
The supported index may be ahead of root HEAD: require each checkout to equal its
stage-zero gitlink, and preserve both identities during qualification. Keep the
original baseline and historical failed receipts unchanged. Resume the approved
Debug build-only qualification on the current synchronized tuple with fresh private
outputs and additive evidence, then complete the build workflow review when its
gates pass. No new per-attempt approval is required for routine harness corrections.


### Approved parser repair amendment - 2026-10-29

Jerome answered "yes" to applying the concrete parser repair proposal and
continuing qualification. This authorizes the exact production change in
[the proposal](story-1-17-additional-properties-empty-fields-proposal-2026-10-29.md)
and its bound patch, plus table-driven regression coverage in the existing
workspace class. It supersedes the earlier no-production-semantics-change
boundary only for this source-mapping validation repair: preserve blank metadata
and one optional trailing terminator; reject leading, interior, repeated trailing
and separator-only empty fields before child evaluation. Preserve all existing
version, mapping, duplicate-name, unknown-property and root-escape guards and
assertions. Implement and verify the approved repair in Hexalith.Builds, then
finish the original Debug/model/selection/PE gates and workflow review on fresh
private outputs. Routine same-contract implementation and review corrections
remain authorized. Keep every historical receipt/proposal immutable and record
new execution additively. Preserve the synchronized stage-zero gitlinks, local
branches, original baseline, parent tracker and all operational authority holds.
No unrelated production repair, commit, push, dependency update, nested
initialization, application startup, publication or deployment is authorized.

</frozen-after-approval>

## Code Map

Builds paths are relative to `references/Hexalith.Builds`. `R` = `src/libraries/Hexalith.Builds.Tooling/Runtime`; `T` = `test/Hexalith.Builds.Module.Tests`. Folders paths: `E` = `eng/story117-source-distribution`; `A` = `_bmad-output/implementation-artifacts`.

- `R/CompositionCommandOptions.cs` — reuse selection; private artifacts require the explicit override.
- `R/PlatformManifestCommand.cs` — BuildModelAsync compiles without composition; its child omits mapping/certificate/serial controls.
- `src/libraries/Hexalith.Builds.Tooling/Workspace/SourceMappingMaterializer.cs` — reuse verified imports/environment and guards.
- `T/Hexalith.Builds.Module.Tests.csproj`, `CompositionSdkProcess.cs`, `PlatformManifestTestGrouping.cs` — reuse original graph, SDK launcher and environment-test serialization.

## Tasks & Acceptance

**Execution:**
- [x] `E/qualify.py` — capture preservation baseline/commands; resolve/materialize production mapping; build the original Debug graph privately without reference-replacement imports.
- [x] `T/CompositionCommandOptionsTests.cs` — cover selection precedence/fallback in the nonparallel collection; restore environment state.
- [x] `E/qualify.py` — exercise the compilation-only model API with controlled SDK execution; retain incoming and added controls. Bind CLI, Tooling, three hosts and model outputs, assets, compiler inputs, deps/runtimeconfig and catalog. Inspect PE references without loading application assemblies; identity checks grant no runtime compatibility.
- [x] `E/test_qualification.py`, `E/verify.py` — cover matrix failures and corruption; replay executes no recorded command and rejects corruption under optimized Python.
- [x] `A/story-1-17-current-debug-distribution-results.md`, `A/evidence/story-1-17-current-debug-distribution-2026-10-29*` — retain evidence, proposed ordinary invocation and remaining prerequisites under the existing archive policy.

**Acceptance Criteria:**
- Given current verified inputs, when compilation and selection pass, then the supported factory names the exact hashed Debug host, all required Hexalith source origins are demonstrated, and the model's actual reference identities are recorded.
- Given historical checks, when reused, then their relevant inputs are proven unchanged; otherwise they remain dated evidence.
- Given a failed gate, when reporting, then qualification stays incomplete and the exact command/result and required repair are retained.
- Given either outcome, when handing off, then ordinary startup, signed distribution and operational acceptance remain explicitly pending.

## Implementation Notes

Planning: thirteen clean repositories, twelve aligned gitlinks. Folders `c3d295b15cd3e0c5f675d014033256458860603f`; Builds `aa46f0c534206b4645b8d9eef08c9a90e1ab540e`. Recheck before execution. No SDK/runtime ran during planning.

## Spec Change Log

## Review Triage Log

### Independent review - 2026-10-29

All three review layers reported before triage. Every finding is retained separately below.

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| B1: added source inputs escape preservation | medium | check_sources only walks the initial rows; a later untracked source/import is absent from that list. Compare the complete current source path inventory before dependent execution; patch. |
| B2: shared SDK AST omits constants/helpers | medium | shared_sdk_control_ast serializes only controlled_child. Changed CONTROLS/SERIAL/PRIVATE and referenced helper definitions can leave the reuse comparison equal. Bind shared dependencies; group with E1; patch. |
| B3: alternate/default Release packaging | medium | controlled_child checks three exact strings. Alternate MSBuild/CLI configuration spellings and default pack bypass the Debug boundary. Normalize supported configuration forms and require/force Debug where the lane requires it; group with E2; patch. |
| B4: duplicate theory case can replace another | medium | Driver and replay compare method Counters, so A/A and A/B from one theory match. Match full case identities with the actual xUnit XML escaping decoded; parent verified JSON-decoded XML names equal all 241 discovery DisplayNames. Reject duplicate/substituted cases; patch. |
| B5: partial runner prevents failure receipt | medium | archive binds every companion when the runner DLL exists; a missing companion raises before sealing. Preserve available bytes, record missing companions and incomplete capture instead; patch. |
| B6: response-file compiler inputs ignored | medium | auxiliary_inputs skips arguments without a colon, including @response files supported by Csc. Refuse unsupported response vectors or bind/parse them explicitly; current actual vectors contain none. Patch with negative coverage. |
| B7: auxiliary capture timing | false | The collector explicitly labels its output a read-only post-execution audit, not a compiler-consumption-time observation. The final original-graph refresh additionally binds all direct/auxiliary files before compilation and verifies them afterward (refresh_original_graph), while fresh-model post-build observations retain their stated timing. No actual pipeline input mutation or false timing claim was demonstrated. Preserve these limits; no code change. |
| B8: passing replay omits mandatory provenance | medium | Optional get/empty loops accept a constructed passed capsule with no command, SDK, compiler or runner evidence. Require the already-collected mandatory nonempty evidence and successful required command records, including explicit reused provenance; patch. |
| B9: six output roles can alias one payload | medium | The verifier checks role keys and manifest equality without requiring distinct canonical files or companions; the synthetic single-payload bundle passes. Enforce role paths, companions, managed-PE structure and complete seal/inventory agreement; patch. |
| B10: model identity matches can be unrelated/incomplete | medium | references.model need not equal outputs.Model, and hexalithMatches need not cover the model reference set. Derive that complete set from the selected model record and verify each adjacent bound output; patch. |
| E1: changed shared controls reused | medium | The disposable constant-change probe reproduces B2 at the same AST-only guard. Retain separately; group with B2; patch. |
| E2: Release option spellings accepted | medium | Disposable model argv probes accept --configuration=Release, -c=Release, /p:Configuration=Release and -property:Configuration=Release. Retain separately; group with B3; patch. |
| E3: focused diagnostics returns zero after later failure | medium | focused_passed uses only the earlier eight tests and preservation, ignoring supplementaryFailure/capture failure. A caller sees success after a later failed lane step. Require every invoked focused gate/capture to succeed; patch. |
| V1: passed-host rejection lacks regression | medium | Pre-verified gap: every replay fixture is incomplete; deleting only the selected-host equality guard leaves all 37 tests green. Add a valid disposable passed capsule and rebound wrong-host rejection under normal and optimized Python; patch. |

Shared roots are B2/E1 and B3/E2 only. B7 is refuted against the documented timing and actual before/after refresh. The other survivors are direct implementation/test corrections with no new public surface or intent choice; nothing is deferred. Existing target-execution receipts remain immutable.

### Review resolutions - 2026-10-29

All thirteen actionable findings are corrected in eleven groups, with no deferrals.
B1 now compares the complete tracked/untracked source inventory before dependent
execution and rejects new source/import paths during reuse. B2/E1 bind shared
constants, imports and transitive helper definitions. B3/E2 normalize supported
CLI/MSBuild configuration spellings, refuse conflicting/Release model or pack
controls and explicitly select Debug for default pack; deliberate synthetic
Release build fixtures remain scoped to their existing test purpose.

B4 matches full class/method/decoded display-name identities and rejects duplicate
or substituted theory rows. B5 retains available runner bytes and the first
failure when companions are absent. B6 refuses unsupported compiler response
vectors. E3 requires every invoked focused gate and capture to succeed. B8
requires successful nonempty command, SDK, compiler and runner provenance,
including explicit reuse proof. B9 enforces distinct canonical output roles,
managed PE/CLI structure, companions and complete seal/inventory equality. B10
binds the selected model and every Hexalith reference to adjacent sealed outputs.
V1 adds valid disposable passed/reused capsules and rebound wrong-host rejection
under normal and optimized Python. B7 remains refuted for the recorded reasons;
the auxiliary audit retains its explicit post-execution limitation.

The implementation agent and parent each pass all 53 final guards normally and
optimized. Both parent replays accept the immutable actual capsule: 4,952 members,
241 exact original cases and zero recorded commands executed. C# source/tests,
child launcher, six sealed output roles and protected authority are unchanged.
The target builds and 241-case run were not rerun after these Python-only review
corrections. The additive review verification records the preservation audit and
read-only recomputation of the revised wrapper on observed child controls.

The bounded spec is done. Explicit approved constraints supersede generic Step 05
commit/tracker instructions: no commit, push or tracker transition is performed.
Parent Story 1.17 remains in-progress and the tracker remains backlog; ordinary
startup, signed distribution and operational acceptance are still pending.

## Verification

Build original Module.Tests in Debug with production imports/private artifacts. Invoke its assembly separately for CompositionCommandOptionsTests, CompositionSdkProcessTests and Workspace.PlatformModelCatalogTests using fully qualified `-class` and `-parallel none`; required assertions must run. Rerun affected workspace classes when inputs changed. Run harness guards and normal/optimized replay. Compare protected files, HEADs/indices and owned diffs; run `git diff --check` in both owners.

Approved by Jerome with "approve and continue" on 2026-10-29. Frozen intent is locked. The explicit no-Git-writes and no-tracker-transition constraints take precedence over generic workflow staging and tracker instructions.

## Parent implementation audit - 2026-10-29

The new factory test class, qualification harness, guards, replay verifier and
concrete Parties handoff are implemented. Tasks 1 and 3 remain unchecked: catalog
materialization stopped before the original Debug graph, required original-assembly
classes, model API compilation and sealed distribution/PE gates. This spec stays
in-progress; Step 04 review and completion were not entered.

Platform selects Builds commit `4ebd4d869fe1d3958d73e586bcaa74bd86e2ce42`; the root
Builds object database lacks it. Independent Git observations confirm the failure.
The source mapping resolved twelve source origins before catalog materialization
failed; that partial mapping grants no distribution acceptance. No fallback was used.

Parent verification independently reran the focused actual factory test class:
8/8 passed, zero failures/errors/skips/unrun cases. Guards passed 16/16 under
normal and optimized Python; focused capsule replay passed in both modes,
verifying 469 retained members and the 8-test XML without executing commands.
The audit rehashed all fifteen receipt/archive/manifest bindings, checked thirteen
repository HEAD/index identities and 80,090 source bindings, and confirmed protected
authority. The sole existing post-run source difference is the documented
replay-only verifier hardening. This task audit and restoration proposal are
subsequent metadata changes. No production source or historical receipt changed.

Matrix audit: private-output/precedence behavior passes in eight focused factory
cases; SDK-control refusal and drift/package/identity failures pass in the sixteen
guards. Actual model-child and exact sealed-host assertions remain blocked.
Supplementary evidence does not replace the original-assembly qualification.

See [results](story-1-17-current-debug-distribution-results.md),
[parent verification](evidence/story-1-17-current-debug-distribution-2026-10-29-parent-verification.json)
and the [unexecuted object-restoration proposal](story-1-17-catalog-object-restoration-proposal-2026-10-29.md).
The missing object requires separate authorization for the proposed narrow fetch;
all other approved build-only and authority constraints remain in force.

## Authorized synchronization result - 2026-10-29

Eight root-declared submodules advanced and four were already current. Builds now
checks out fetched origin/main with its divergent local main preserved. All 24 local
change files were byte-identical after synchronization. Only the eight changed root
gitlinks were staged. The previously missing catalog object is now present through
the approved fetch. See [sync evidence](evidence/story-1-17-current-debug-distribution-2026-10-29-submodule-sync.json).

## Parent synchronized-resume audit - 2026-10-29

Task 1 is now complete: production mapping/materialization and the original
Debug graph passed on the user-approved synchronized tuple. The original
assembly passed all three required classes: factory 8/8, SDK launcher 14/14 and
model-catalog 24/24. Current discovery supplies the denominator; historical test
counts remain dated. The test-only model fixture selects/restores its private
NuGet cache in a nonparallel collection, with unchanged assertions.

Affected workspace checks found the remaining production blocker: 132/133
WorkspaceMsBuild cases pass; the empty interior assignment case returns build
success rather than HXW006. Independent catalog checks pass 34/34 and permitted
AppHost metadata checks pass 11/11. The ComposeRun integration assertion is
explicitly excluded by the frozen boundary and receives no compatibility credit.
Across executed cases, 223/224 pass with zero errors, skips or unrun cases.

Task 3 remains unchecked. Actual Platform model compilation, exact sealed-host
selection and model PE gates stopped behind the first production failure. Five
compiled output trees and static PE metadata are retained as unqualified. The
existing assertion and production parser are unchanged. The
[unapplied parser proposal](story-1-17-additional-properties-empty-fields-proposal-2026-10-29.md)
preserves one trailing terminator while rejecting leading/interior empty fields
and separator-only lists; its patch passes the owning-repository apply dry run.
Applying this production repair requires the separate scope amendment specified
in the frozen intent. Do not enter Step 04 or mark this spec done yet.

Parent verification independently rehashed all current harness/test and handoff
bindings, the unchanged production source and protected authority; checked thirteen
repository identities, twelve indexed checkout identities, the preserved local
Builds main and eight authorized staged gitlinks; and audited 82,982 source
bindings against the documented post-execution recording changes. All 28 guards
passed normally and optimized. Normal and optimized final replay verified 4,353
members and the exact 223/224 outcome without executing recorded commands. Both
owning repositories pass git diff --check. This task-note append occurs after
those checks and changes no frozen intent. An initial parent checker path-format
comparison was corrected; actual handoff bytes and hashes matched throughout.

See [current results](story-1-17-current-debug-distribution-results.md),
[final receipt](evidence/story-1-17-current-debug-distribution-2026-10-29-synchronized-final.json),
[handoff](evidence/story-1-17-current-debug-distribution-2026-10-29-synchronized-handoff.json)
and [parent verification](evidence/story-1-17-current-debug-distribution-2026-10-29-synchronized-parent-verification.json).
Parent story/tracker/routing/activation/staging controls remain unchanged.

## Parent completed-implementation audit - 2026-10-29

All five implementation tasks and the bounded acceptance criteria now pass.
The approved parser repair preserves one optional trailing terminator and rejects
malformed empty fields; all 150 discovered workspace cases pass. The six
permitted original-assembly classes total 241/241, with no failures, errors,
skips or unrun cases. The forbidden ComposeRun integration remains outside this
build-only scope and receives no current runtime credit.

The actual model API compiled using explicitly recorded qualification-only
AlternateCommonProps composition: exact pin, genuine SDK common props, genuine
mapping imports/guards and unchanged owner props. Production API semantics and
catalog validation were preserved; early import and duplicate-version failures
remain dated evidence. Exact factory selection and model PE identity checks pass.

The original Debug graph was really recompiled with 26 identical Csc vectors and
4,926 relevant inputs unchanged. Complete test/helper output trees remained
byte-identical, supporting explicit reuse of the 241-case observation. Recorded
EventStore Server/Gateway implementation/PDB variations are confined to debug
identities; refreshed artifact/model/selection/PE gates bind the final bytes.

Parent independently passed all 37 guards normally and optimized, both final
replays over 4,952 members, current source/artifact/document bindings, thirteen
repository identities, protected authority and 83,009 source bindings (apart
from documented post-execution README/results recording). Both owning diffs
pass whitespace checks. The six sealed artifact roles and selected host hash
match the final receipt. This task-note append is later metadata, not a change
to the approved frozen intent or target-executed source bytes.

See [parent verification](evidence/story-1-17-current-debug-distribution-2026-10-29-qualified-parent-verification.json)
and [qualified handoff](evidence/story-1-17-current-debug-distribution-2026-10-29-qualified-handoff.json).
Proceed to Step 04 review. Parent story/tracker and every operational hold remain
unchanged; this is local diagnostic Debug qualification only.
