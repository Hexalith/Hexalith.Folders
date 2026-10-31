---
title: '1.17 Qualify the production source model build'
type: 'bugfix'
created: '2026-10-30'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'a5f1cd8bebba3f09d698ea6682b1531179bb3a3b'
story_key: '1-17-publish-the-pd10-v2-authorization-contract-spine'
tracker_policy: 'no-automatic-transition'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Prior Debug qualification compiled the Platform model using qualification-only SDK arguments/imports. The production `BuildModelAsync` child remains unqualified, and Builds/Platform/SDK inputs have advanced.

**Approach:** Rebuild current source privately, prepare mapping through the supported command, and compile the actual model through the unwrapped production API. Reproduce failures before narrow import/process-control repairs; retain current evidence and the startup handoff.

## Boundaries & Constraints

**Always:** Follow owning-repository guidance. Bind full revisions, root HEAD/index gitlinks, changed source, SDK/catalog, commands, imports and artifacts. Honor `global.json` using installed SDKs; Builds requires installed 10.0.403. Use private outputs/caches, preserved HOME, certificate prevention and bounded serial SDK work. Preserve historical bytes, parent in-progress, tracker backlog, V1Only, T0 unset, activation authority null and staging 0/14.

**Never:** Start applications/topologies, call ComposeRun/`--validate-platform`, execute debug/run, replay loaders, publish/deploy, install tools, change pins, initialize references or write Git state. Preserve owner props, mapping/catalog guards and assertions. Qualification must not rewrite model-child arguments/imports or substitute executables. Limit production repairs to reproduced model-build import/control defects; unrelated failures stop dependent gates with an owner handoff.

## I/O & Edge-Case Matrix

| Scenario | Required behavior |
| --- | --- |
| Current tuple | Actual model compiles in Debug through production mapping/API. |
| Missing/tampered authority | Refuse before dependent compilation. |
| Early imports | Selected catalog resolves once before owner use; mapping guards execute. |
| Unsafe controls/drift | Refuse execution or invalidate reuse; inject no qualification repair. |
| Other failure | Preserve exact command/result/owner; qualification stays incomplete. |

</frozen-after-approval>

## Code Map

Aliases: `B=references/Hexalith.Builds`, `R=B/src/libraries/Hexalith.Builds.Tooling/Runtime`, `T=B/test/Hexalith.Builds.Module.Tests`, `E=eng/story117-source-distribution`, `A=_bmad-output/implementation-artifacts`.

- `R/PlatformManifestCommand.cs` — model API and shared McpCli catalog arguments.
- `R/CompositionProcess.cs`, `T/CompositionProcessTests.cs` — reproduced dotnet-only loss of explicit nonsecret SDK/cache controls; retain the original scrubbed-helper boundary.
- `B/src/libraries/Hexalith.Builds.Tooling/Workspace/SourceMappingMaterializer.cs` — genuine imports/environment/guards.
- `B/src/libraries/Hexalith.Builds.Module.Cli/ModuleCommandApplication.cs` — nonlaunch `prepare-build`; no public plan command.
- `E/Probe.cs`, `E/qualify.py`, `E/verify.py` — reuse API/PE/integrity helpers; exclude historical wrapper/SDK assumptions.
- `E/model_replay_fixture.py` — synthetic native-format complete capsule for executable semantic regression tests; no SDK or ignored-archive dependency.
- `references/Hexalith.Parties/module/hexalith.module-manifest.v2.json` — real, read-only direct manifest.

## Tasks & Acceptance

**Execution:**

- [x] `E/qualify_model.py`, `E/Probe.cs` — precheck root checkouts; rebuild CLI/hosts/helper privately in Debug/source mode. Run real `prepare-build` with the Parties manifest, private workspace, `--mode source --output json`; invoke unwrapped `BuildModelAsync`. Capture the first outcome.
- [x] `R/PlatformManifestCommand.cs`, `R/CompositionProcess.cs`, `B/src/libraries/Hexalith.Builds.Tooling/Workspace/SourceMappingMaterializer.cs` — repair reproduced import/control defects without changing catalog authority, conditional versions or McpCli semantics; rebuild before final execution.
- [x] `T/Workspace/PlatformModelCatalogTests.cs`, `T/CompositionSdkProcessTests.cs`, `T/CompositionProcessTests.cs` — cover reproduced failures/matrix edges and run original classes, including source/package fixtures and refusal assertions.
- [x] `E/verify_model.py`, `E/test_model_preflight.py` — verify command/control/reference/preservation evidence; reject corruption, false authority and drift normally and optimized. Replay executes nothing.
- [x] `A/story-1-17-production-source-model-results.md`, `A/evidence/story-1-17-production-source-model-2026-10-30*` — retain additive portable evidence and exact next invocation/prerequisites; distinguish historical reuse.

**Acceptance Criteria:**

- Given verified inputs, when the production child runs, then the actual model compiles without qualification-added arguments/imports; compiler/assets/PE evidence identifies its source dependencies and genuine guards.
- Given shared consumers, when focused classes run, then conditional source/package fixtures and McpCli guards retain behavior.
- Given retained evidence, when replay runs normally or optimized, then intact evidence passes and corruption fails without execution.
- Given any result, when reporting, then API/fixture evidence grants no public-startup, runtime, accepted-tool, signed-release or operational credit.

## Implementation Notes

Approved by Jerome with "approve and continue" on 2026-10-30. Frozen intent is locked. The explicit no-Git-writes and no-automatic-tracker-transition constraints override generic workflow staging and tracker synchronization.

The bounded implementation and review are complete. Full qualification remains incomplete because the retained NSS metadata change has no proven writer; this status does not close Story 1.17 or grant startup/release authority. Parent audit passes 82 Python tests per mode, both 1,930-member/57-case replays and the verify_child mutation-red check. Seven executed C# sources, 484 compiler inputs, 40 API producer inputs and 1,074 runner-copy bindings still match. External root/Builds commits are recorded separately from the original execution baseline; four protected authority files remain unchanged. No Git write or tracker transition was performed by this workflow.

## Spec Change Log

- 2026-10-30: Added the reproduced dotnet-only process-control file and original process test class to the agent-owned code/task map. Same-contract repair authorized during implementation; frozen intent unchanged.

## Review Triage Log

### Independent review — 2026-10-30

All three required layers ran. Each finding is recorded before grouping. These are private qualifier/replay corrections and regression coverage within the accepted intent; production sources and recorded SDK/model executions remain unchanged.

| Finding | Verdict | Verified evidence and route |
| --- | --- | --- |
| B01 | high | Passed status can overwrite the preserved failed NSS consequence; complete replay checks only status strings and ignores underlying certificate observations/errors. Verified by tracing replay/verify_correction/verify_complete. Patch preservation evidence consistency; keep the historical failed receipt immutable. |
| B02 | medium | Parent direct probe accepted a credited class with exitCode 17/timedOut true. Successful diagnostic credit is not coupled to successful required command records. Patch command completeness/success checks. |
| B03 | medium | Parent direct probe accepted /usr/bin/false for a credited class; code also accepts an unrelated assembly substring/filter. Patch exact executable/project/assembly/filter and observation bindings. |
| B04 | medium | Parent direct probe accepted appended -t:Publish on cli-build. Partial build-argument checks miss target/property injection. Patch a closed argument contract with B03. |
| B05 | medium | Parent direct probe accepted DOTNET_STARTUP_HOOKS. Replay checks three controls while execution enforces the full prevention policy. Patch the complete environment policy. |
| B06 | medium | Complete replay couples XML to self-reported discovery but has no required method/theory matrix, allowing the demonstrated eight-to-one McpCli reduction. Patch the intended selection binding. |
| B07 | medium | XML verification checks total/passed and test results but omits assembly errors/failed/skipped/not-run counters; the demonstrated errors=4 remains accepted. Patch XML-to-summary consistency. |
| B08 | medium | Compiler validation only requires three input kinds and per-listed-file integrity; it neither reconstructs response arguments nor proves input completeness/output identity. Patch the retained command/input/output binding. |
| B09 | medium | Mapping validation searches token bytes, so comments can satisfy it. Patch genuine resource-byte and executable XML structure checks. |
| B10 | medium | Parent direct probe accepted CLI bytes as AppHost and still credited 57 cases. Role paths and reported PE metadata lack sufficient retained-byte identity checks. Patch canonical roles and PE identity/reference validation. |
| B11 | medium | source_inputs excludes an initially linked input, so its target changes are absent from both inventories. Patch explicit linked-input refusal and regression coverage. |
| B12 | medium | verify_commands requires launched=true unconditionally, so a legitimate captured Popen failure cannot replay as intact incomplete evidence. Patch failed-launch handling without granting execution credit. |
| E01 | medium | Independently identifies B11: initially linked build inputs escape preservation. Same source-input root cause; patch with B11. |
| E02 | medium | Parent direct probe removed all discovery/class/McpCli records and verify_commands still accepted. Bind every credited case scope to its required successful commands; patch with B02. |
| E03 | medium | Parent direct probe added nonempty preservation errors and verify_complete still credited 57 cases. Patch evidence/status consistency with B01. |
| V01 | medium | Pre-verified regression gap: replacing verify_child with a no-op leaves all 68 tests green. Existing fixtures are incomplete/no-command capsules. Add a valid complete diagnostic fixture and rebound executable/other semantic regressions; patch. |

### Review resolutions and parent verification — 2026-10-30

All 16 findings are corrected; none were deferred. The production C# code and recorded SDK/model/graph executions were preserved.

| Findings | Resolution and executable coverage |
| --- | --- |
| B01, E03 | Actual certificate observations, prior failed interval and preservation errors are checked; relabelled success refuses. |
| B02, E02 | Every credited command/discovery scope must exist and succeed, with matching actual process observations. |
| B03, B04 | Closed SDK/helper/CLI/runner, project, argument and filter contracts reject substitution and publication overrides. |
| B05 | Complete prevention, mapping and private-cache policy rejects unsafe or injected environment controls. |
| B06 | Original fact/theory selections are derived from retained test-assembly attributes and matched to discovery/XML. |
| B07 | Every assembly/collection XML counter is checked against the claimed outcome. |
| B08 | Response bytes reconstruct compiler arguments and the full input inventory; model bytes and CodeView output identity are bound. |
| B09 | Mapping resources match executed Tooling embedded bytes and generated XML must carry executable guard tasks. |
| B10 | Canonical output roles and identities/references decoded from retained PE bytes reject aliases and invented metadata. |
| B11, E01 | Linked repositories, files and ancestors refuse before dependent execution. |
| B12 | Intact failed-Popen evidence replays only as incomplete, uncredited execution. |
| V01 | Portable passing/incomplete capsule fixtures exercise the full path and semantic mutations. Parent removal of verify_child causes the expected regression failure. |

Final parent checks: 82/82 Python tests normally and optimized; both unchanged real-capsule replays pass with 1,930 members, 57 cases and zero execution. Global qualification stays incomplete. The current source-hash, compiler/API/runner, repository-checkpoint and protected-authority audit is [retained separately](evidence/story-1-17-production-source-model-2026-10-30-parent-final-verification.json). This completes the local implementation/review artifact only.

## Verification

- Build `T/Hexalith.Builds.Module.Tests.csproj` in private serialized Debug/source mode; invoke its runner with each changed class's full name via `-class`. Bind discovery/execution identities.
- `python3 -B E/qualify_model.py --execute --suffix <unique>` — real preparation/model execution or exact first failure.
- `python3 -B -m unittest discover -s eng/story117-source-distribution -p 'test_*.py'` and `-O` equivalent — relevant guards pass.
- `python3 -B E/verify_model.py --receipt <receipt>` and `-O` equivalent; protected-control audit and owning-repository `git diff --check` pass.

- Implementation verification: real production preparation/model and original source graph passed; 57/57 independently rerun original cases and 68/68 Python guards pass normally and optimized. Completed 1,930-member replay passes with zero command execution. Prior NSS metadata failure remains unchanged; overall qualification stays incomplete. See `story-1-17-production-source-model-results.md` and the additive completed/verification receipts. No automatic tracker or parent transition.
