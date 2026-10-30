---
title: '1.17 Qualify the production source model build'
type: 'bugfix'
created: '2026-10-30'
status: 'in-review'
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

## Spec Change Log

- 2026-10-30: Added the reproduced dotnet-only process-control file and original process test class to the agent-owned code/task map. Same-contract repair authorized during implementation; frozen intent unchanged.

## Review Triage Log

## Verification

- Build `T/Hexalith.Builds.Module.Tests.csproj` in private serialized Debug/source mode; invoke its runner with each changed class's full name via `-class`. Bind discovery/execution identities.
- `python3 -B E/qualify_model.py --execute --suffix <unique>` — real preparation/model execution or exact first failure.
- `python3 -B -m unittest discover -s eng/story117-source-distribution -p 'test_*.py'` and `-O` equivalent — relevant guards pass.
- `python3 -B E/verify_model.py --receipt <receipt>` and `-O` equivalent; protected-control audit and owning-repository `git diff --check` pass.

- Implementation verification: real production preparation/model and original source graph passed; 57/57 independently rerun original cases and 68/68 Python guards pass normally and optimized. Completed 1,930-member replay passes with zero command execution. Prior NSS metadata failure remains unchanged; overall qualification stays incomplete. See `story-1-17-production-source-model-results.md` and the additive completed/verification receipts. No automatic tracker or parent transition.
