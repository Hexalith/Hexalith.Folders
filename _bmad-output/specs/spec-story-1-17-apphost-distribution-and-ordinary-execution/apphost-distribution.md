# AppHost distribution contract

## Inspected starting state

Read-only observations on 2026-10-29; no fresh SDK/build/runtime result is claimed.

| Subject | Earlier final receipt | Current observation |
| --- | --- | --- |
| Folders HEAD | `5fb5cfcfcda10044faccc71b48f8a3d6c08c5b87` | `5a2e2f95a4d7032d8d8889b6b0b32310654e20d6` |
| Direct Builds HEAD | `e4e4568aa59050b5890c60ff4a1f39bcc83399b4` plus six edits | `505230a24339070aa2dbf5ac927cd430050bb902` plus the same six edited-file bytes |
| Direct Builds catalog defaults | EventStore `3.128.3`; Tenants `5.11.0` | EventStore `3.128.4`; Tenants `5.11.1` |
| Platform / its Builds catalog pin | `8de90958bc41b7fd01b7ed22b8ab364447a800e4` / `9f85d8e66e5c16675cee8c224f1fd88430d96007` | Unchanged; tool range `[4.35.2, 5.0.0-0)` |

All twelve root-declared checkouts currently match both committed and indexed root gitlinks. The eleven direct dependency HEADs other than Builds match the final receipt. This alignment permits later preflight; it grants no current tuple acceptance. Direct catalog bytes, Platform's pinned catalog snapshot and Tooling's embedded generated catalog must remain separately identified. The current direct catalog working-file SHA-256 is `443beb3037f215fc602fb0a2bc7eca06ae1bb46fc16ef1df0a2c3fb3b468d2b0`; Git stores normalized bytes, so label each hash's representation.

The six existing uncommitted paths below are relative to `references/Hexalith.Builds`. Their current SHA-256 values match the final capsule manifest exactly:

| Path | SHA-256 |
| --- | --- |
| `src/libraries/Hexalith.Builds.Tooling/Workspace/SourceMappingReconciliationTask.cs` | `66a6263b7a3d05a740f1474ee1c3525085d165495824e52f4e97585ccf593d1a` |
| `test/Hexalith.Builds.Module.Tests/CompositionSdkProcess.cs` | `f80442d68459374377716a7995bfb0305ec9cbd4030ae4c84d4b8759e7cc6434` |
| `test/Hexalith.Builds.Module.Tests/CompositionSdkProcessTests.cs` | `5cf262deb55a2ac68fc8d40de5db76b6c360234e9fc4ba23bbf1b655c7723369` |
| `test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj` | `9e71dda75656fd497b27e6a94e3bc9c6e6d3892213beef6053747ae524e1d74b` |
| `test/Hexalith.Builds.Module.Tests/Workspace/WorkspaceAppHostTests.cs` | `c3a8fa7d5130f35f05ebfb77242929e88f5cb23f98cfa6ce67ba696d1c7a8ead` |
| `test/Hexalith.Builds.Module.Tests/Workspace/WorkspaceMsBuildTests.cs` | `8542f32de0aa76faae0c64e4f0cf75dcefbc8904f64f0d53682914ae349c2d3a` |

The final manifest and ignored local archive were rehashed against the receipt: `08b839c0f35a427e691d1947abd14c8630f95b7f4b71df161bcb5698869bf132` and `5bc2187b88f7e6dfb95c634345da104f9f0d4330339eea4a5e7d48578c5b4292`. This verifies these byte comparisons, without rerunning the 202 tests. Their 17/139/34/12 class totals, 29 package-free restored assets and mapping `83e97d9576a14774675d2431a52df23ed1204b0cc57fa309ee5f291852ae00b6` remain historical build/model evidence. A current mapping/hash has not been produced by this spec run.

Retain four existing MSB3277 Redis/IdentityModel conflicts. The earlier broad diff against `f48886763a1b57b30f7767deb9909f741dc812f3` exited 0 while reporting `error: hash mismatch 6ba02654ca390b3333183962355bd5c5857b8224` for a historical Parties object. Do not rerun or repair that audit to qualify this gate; current scoped diffs and byte bindings are independent. Builds' new historical SDK-split disposition explicitly remains unqualified and grants no runtime/package/release acceptance.

## Current production boundaries

Paths in this section are relative to `references/Hexalith.Builds`.

- `Runtime/` below means `src/libraries/Hexalith.Builds.Tooling/Runtime/`. `CompositionCommandOptions.Create` currently prefers `HEXALITH_G4_APPHOST`, then adjacent `g4-host/Hexalith.Builds.Module.AppHost.dll`, then a source path found by walking runner ancestors for `Hexalith.Builds.slnx`. Its Debug/Release inference reads the runner path; it has no mapping input.
- `Runtime/ModuleCommandExecutionService.cs` attaches the resolved mapping later in legacy, v2 run/debug and selected-test branches. `Runtime/CompositionEngine.cs` checks only an absolute existing AppHost path and executes it unchanged with `dotnet exec`, first with `--validate-platform` for v2, then for startup. It never builds that host.
- `Runtime/PlatformManifestCommand.BuildModelAsync` builds a run-owned model with pinned catalog arguments and the runner's actual Tooling `HintPath`, but passes no mapping environment. The preflight environment also lacks the imports added by `StartAppHost`. Ambient source probes or a test-only launcher's controls cannot prove these ordinary boundaries.
- `Workspace/SourceMappingMaterializer.cs` supplies verified imports/hash, Debug/source roots and reconciliation guards. The three source-tree hosts participate in source mode; generated `hosts/` projects stay excluded. Package-mode EventStoreHost/UiHost isolation and catalog validation remain required.
- `src/libraries/Hexalith.Builds.Module.Cli/Hexalith.Builds.Module.Cli.csproj` packages **Release** AppHost/host outputs under `tools/net10.0/any/g4-host/`, with packaged host shims and `RequirePackagedHosts`. A loose Debug output is a source distribution, without package qualification.
- `src/hosts/Hexalith.Builds.Module.AppHost/AppHostRunner.cs` executes the real composition branch. `RunTopology.ComposePlatform` loads the model and invokes `ComposeRun` through the existing Default-context resolver. `Runtime/CompositionDiagnosticMirror.cs` retains stream-observed tokens, which do not identify loaded binaries.
- Folders' own `src/Hexalith.Folders.AppHost` and its platform-composition conformance tests describe a different topology. They cannot substitute for executing the Builds-owned runner.

## Required selection

Resolve the mapping first, then select one prebuilt distribution for the command. Keep `AppHostAssemblyPath` as the single effective path used by the engine. Do not build an AppHost automatically.

| Input | Required result |
| --- | --- |
| Source mode, explicit `HEXALITH_G4_APPHOST` | Select that existing absolute AppHost path, including a private `.artifacts/` output physically under the mapped Builds root. Require its source/Debug provenance in qualification evidence; the variable alone proves neither. |
| Source mode, override absent | Use the single source-origin `Hexalith.Builds` mapping entry, including when Builds is active. Select `<mapped Builds>/src/hosts/Hexalith.Builds.Module.AppHost/bin/Debug/net10.0/Hexalith.Builds.Module.AppHost.dll`. |
| Source mode, adjacent packaged host or runner under a Release/private output path | Source selection above still wins. Runner directory names do not select host configuration. |
| Missing/ambiguous source Builds entry, missing binary/companions, relative override or physical escape | Refuse before AppHost execution with the applicable mapping diagnostic or `HXR014`/exit 2 and the expected path. No ancestor/sibling/nested search or package fallback. |
| Package mode, override absent | Select the adjacent `g4-host` distribution. Missing distribution refuses; never substitute the source Debug output. |
| Explicit package-mode override | Preserve explicit selection with path checks; label it configured. It earns package acceptance only in the separate package lane. |
| Explicit `CompositionEngineOptions` in existing fixture tests | Preserve the test seam. Such an invocation supplies fixture evidence only. |
| Selected file changes between selection, preflight and startup | Refuse dependent execution and retain the mismatch; do not reselect or retry. |

Use existing physical-path utilities and diagnostics. The build receipt establishes Debug/source provenance; a `Debug` directory name or successful existence check cannot establish it. An explicit source output must bind the same mapped Builds checkout and current source bytes. Check the selected assembly plus its unchanged `.deps.json`, `.runtimeconfig.json` and required adjacent closure; never patch or copy dependencies over another distribution.

## Smallest production change

1. Make the existing selector mapping-aware and defer default selection until the public service has resolved the mapping. Apply it consistently to ordinary run/debug and selected-test branches; retain explicit test injection. Freeze the canonical path/hash once per command and use it for both engine launch sites.
2. Carry the command's verified mapping into the source-mode model-build child and both AppHost launch environments. In `PlatformManifestCommand`, compose the existing early pinned-catalog import with mapping imports without weakening their exact guards, catalog ownership or Tooling binding. A narrow import-composition helper is permissible; private replacement targets are not. Forward the required safe SDK controls at these touched boundaries with supported arguments and explicit environment values, preserving the general scrubber. Prove real evaluation before treating this as solved.
3. Add bounded metadata observations at the existing engine/AppHost diagnostic boundaries. Record selection before execution and actual process/loaded-assembly identity after real composition and at readiness. Use one focused AppHost observation type and retain preflight/run records before workspace cleanup. No extra loader, forced dependency loads or additional composition invocation.

The likely edit surface is `CompositionCommandOptions`, mapping-attachment sites in `ModuleCommandExecutionService`, the two launch/diagnostic sites in `CompositionEngine`, source model launch in `PlatformManifestCommand`, and `AppHostRunner` plus its observation type. New focused test files should preserve the six existing edits. Keep packaging layout, source reconciliation behavior, upstream sources and pins unchanged. Any newly discovered prerequisite outside these boundaries remains a named blocker for a follow-up scope.
