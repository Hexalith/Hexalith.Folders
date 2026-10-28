# Supported source-host qualification resume — 2026-10-28

The original Debug Module.Tests graph and all four original-assembly class gates passed. The required Platform delegation assertion executed and passed with the verified root-declared source mapping; no selected tests were skipped. Step 04 review remains pending. Parent Story 1.17 remains in-progress and the tracker remains backlog.

This is additive evidence for the current approved-scope resume in `spec-1-17-qualify-supported-source-host-graph.md`. It does not replace either historical failed-run receipt. The exact commands, explicit environments, revisions, mapping entries, log hashes, XML totals and artifact bindings are retained in [the receipt](evidence/story-1-17-supported-source-host-qualification-resume-2026-10-28.json).

## Changes

- The original test project conditionally references the verified source-mapped Platform composition project, so normal project-reference output resolution copies its model and dependencies into private test outputs.
- The Platform delegation assertion verifies the materialized mapping with `SourceMapping.LoadVerified`, derives Platform and EventStore paths from its entries, and uses the model copied by the original graph. Missing qualification inputs fail the assertion. Existing ordinary invocation behavior is retained.

## Inputs and controls

- Folders HEAD: `5fb5cfcfcda10044faccc71b48f8a3d6c08c5b87`.
- Builds HEAD: `e4e4568aa59050b5890c60ff4a1f39bcc83399b4`.
- Production mapping: `4ddcfe7a1cae1102c826cbdfb207461b96c4415ba28032ff9d384de04293e29c`; all twelve root-declared entries have source origin.
- SDK: absolute `/home/administrator/.dotnet/dotnet`, version `10.0.402`.
- Private run: `/tmp/story117-source-host-20261028-ep190mgc`.
- Private output root: `/home/administrator/projects/hexalith/folders/references/Hexalith.Builds/.artifacts/story117-source-host-20261028-ep190mgc`.
- Every dependent command first rechecked all thirteen repository HEAD/index identities and all twelve checkout/committed/indexed gitlink alignments. They remained stable.
- HOME remained `/home/administrator`; dedicated CLI, NuGet and temporary paths are private. Explicit environments scrub unrelated inherited values. Certificate generation stayed `false`; MSBuild used `-m:1 -nr:false -p:BuildInParallel=false -p:RestoreBuildInParallel=false -p:UseSharedCompilation=false`, and restore commands alone add `--disable-parallel`.
- Private dotnet/PowerShell interceptors were demonstrated with real PowerShell descendants before relying on the graph result. SDK launch construction and bounded process propagation then passed in the original launcher class.
- The original graph command explicitly sets `HexalithG4FixtureArtifacts` to the private fixture tree. No application or topology was started.

## Commands and results

```text
/home/administrator/.dotnet/dotnet --version
```
Exit `0`; timeout `false`; bound `30s`; log `/tmp/story117-source-host-20261028-ep190mgc/logs/sdk-version.log`; SHA-256 `44ea2595bc5a7acabc030531952071226aa70862fd008ed7327d752b39c8513c`.

```text
/home/administrator/.dotnet/dotnet build /tmp/story117-source-host-20261028-ep190mgc/helper/SourceMappingHelper.csproj --configuration Debug --artifacts-path /home/administrator/projects/hexalith/folders/references/Hexalith.Builds/.artifacts/story117-source-host-20261028-ep190mgc/helper -p:RestoreConfigFile=/tmp/story117-source-host-20261028-ep190mgc/NuGet.Config -p:NuGetAudit=false -m:1 -nr:false -p:BuildInParallel=false -p:RestoreBuildInParallel=false -p:UseSharedCompilation=false
```
Exit `0`; timeout `false`; bound `900s`; log `/tmp/story117-source-host-20261028-ep190mgc/logs/helper-build.log`; SHA-256 `c90619091e8f956f0cc0fab483606ed4e86e2b5f67bd27e40594b7a5d79d0623`.

```text
/home/administrator/projects/hexalith/folders/references/Hexalith.Builds/.artifacts/story117-source-host-20261028-ep190mgc/helper/bin/SourceMappingHelper/debug/SourceMappingHelper probe-only
```
Exit `0`; timeout `false`; bound `60s`; log `/tmp/story117-source-host-20261028-ep190mgc/logs/helper-interception.log`; SHA-256 `75f420934c7f223ea368fb63b05c39ae15e55f1a5fd40b9f5f073346045e2609`.

```text
/home/administrator/projects/hexalith/folders/references/Hexalith.Builds/.artifacts/story117-source-host-20261028-ep190mgc/helper/bin/SourceMappingHelper/debug/SourceMappingHelper /home/administrator/projects/hexalith/folders/references/Hexalith.Builds/test/fixtures/module/executable/hexalith.module-manifest.v1.json /tmp/story117-source-host-20261028-ep190mgc/workspace
```
Exit `0`; timeout `false`; bound `240s`; log `/tmp/story117-source-host-20261028-ep190mgc/logs/production-materialization.log`; SHA-256 `670310b97aed95863f205b59d602d69df12e976d53d3fbc4eb9f3616f071b979`.

```text
/home/administrator/.dotnet/dotnet build test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj --configuration Debug --artifacts-path /home/administrator/projects/hexalith/folders/references/Hexalith.Builds/.artifacts/story117-source-host-20261028-ep190mgc/original -p:RestoreConfigFile=/tmp/story117-source-host-20261028-ep190mgc/NuGet.Config -p:NuGetAudit=false -p:HexalithG4FixtureArtifacts=/home/administrator/projects/hexalith/folders/references/Hexalith.Builds/.artifacts/story117-source-host-20261028-ep190mgc/fixture/ -m:1 -nr:false -p:BuildInParallel=false -p:RestoreBuildInParallel=false -p:UseSharedCompilation=false
```
Exit `1`; timeout `false`; bound `900s`; log `/tmp/story117-source-host-20261028-ep190mgc/logs/original-debug-build-private-fixtures.log`; SHA-256 `1c0e6fbd7c7655320d065d1e507c4c16c911d3972c40ff2c930aeae448eb2f73`.

```text
/home/administrator/.dotnet/dotnet build test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj --configuration Debug --artifacts-path /home/administrator/projects/hexalith/folders/references/Hexalith.Builds/.artifacts/story117-source-host-20261028-ep190mgc/original -p:RestoreConfigFile=/tmp/story117-source-host-20261028-ep190mgc/NuGet.Config -p:NuGetAudit=false -p:HexalithG4FixtureArtifacts=/home/administrator/projects/hexalith/folders/references/Hexalith.Builds/.artifacts/story117-source-host-20261028-ep190mgc/fixture/ -m:1 -nr:false -p:BuildInParallel=false -p:RestoreBuildInParallel=false -p:UseSharedCompilation=false
```
Exit `0`; timeout `false`; bound `900s`; log `/tmp/story117-source-host-20261028-ep190mgc/logs/original-debug-build-private-fixtures-documented-exception.log`; SHA-256 `c45630930b392651a9bf09df622623783ff18961263000f1597853898e5ea8f5`.

```text
/home/administrator/projects/hexalith/folders/references/Hexalith.Builds/.artifacts/story117-source-host-20261028-ep190mgc/original/bin/Hexalith.Builds.Module.Tests/debug/Hexalith.Builds.Module.Tests -class Hexalith.Builds.ModuleTool.Tests.CompositionSdkProcessTests -parallel none -xml /tmp/story117-source-host-20261028-ep190mgc/CompositionSdkProcessTests.xml
```
Exit `0`; timeout `false`; bound `900s`; log `/tmp/story117-source-host-20261028-ep190mgc/logs/CompositionSdkProcessTests.log`; SHA-256 `ae328f2f327198f41f0f969db11cf871bbf3f34f6a8fde6d44c7ff851b7cba0a`.

```text
/home/administrator/projects/hexalith/folders/references/Hexalith.Builds/.artifacts/story117-source-host-20261028-ep190mgc/original/bin/Hexalith.Builds.Module.Tests/debug/Hexalith.Builds.Module.Tests -class Hexalith.Builds.ModuleTool.Tests.Workspace.WorkspaceMsBuildTests -parallel none -xml /tmp/story117-source-host-20261028-ep190mgc/Workspace.WorkspaceMsBuildTests.xml
```
Exit `0`; timeout `false`; bound `900s`; log `/tmp/story117-source-host-20261028-ep190mgc/logs/Workspace.WorkspaceMsBuildTests.log`; SHA-256 `c847bb15d22877472c30f612fd0b3874f2014179ce2f853d5b20380f5e89570d`.

```text
/home/administrator/projects/hexalith/folders/references/Hexalith.Builds/.artifacts/story117-source-host-20261028-ep190mgc/original/bin/Hexalith.Builds.Module.Tests/debug/Hexalith.Builds.Module.Tests -class Hexalith.Builds.ModuleTool.Tests.Workspace.CatalogVersionTests -parallel none -xml /tmp/story117-source-host-20261028-ep190mgc/Workspace.CatalogVersionTests.xml
```
Exit `0`; timeout `false`; bound `900s`; log `/tmp/story117-source-host-20261028-ep190mgc/logs/Workspace.CatalogVersionTests.log`; SHA-256 `92e4ecbc7016a0ad4aa6d77f74c5c2253e48778b59a4573c480aeb2c1521d4ca`.

```text
/home/administrator/projects/hexalith/folders/references/Hexalith.Builds/.artifacts/story117-source-host-20261028-ep190mgc/original/bin/Hexalith.Builds.Module.Tests/debug/Hexalith.Builds.Module.Tests -class Hexalith.Builds.ModuleTool.Tests.Workspace.WorkspaceAppHostTests -parallel none -xml /tmp/story117-source-host-20261028-ep190mgc/Workspace.WorkspaceAppHostTests.xml
```
Exit `0`; timeout `false`; bound `900s`; log `/tmp/story117-source-host-20261028-ep190mgc/logs/Workspace.WorkspaceAppHostTests.log`; SHA-256 `6b9502ade4aba46d5e4721bde367e5a2b095c11573edd837f3e4be9f28a3424e`.

| Original-assembly class | Total | Passed | Failed / errors | Skipped / not run |
| --- | ---: | ---: | ---: | ---: |
| `Hexalith.Builds.ModuleTool.Tests.CompositionSdkProcessTests` | 14 | 14 | 0 / 0 | 0 / 0 |
| `Hexalith.Builds.ModuleTool.Tests.Workspace.WorkspaceMsBuildTests` | 132 | 132 | 0 / 0 | 0 / 0 |
| `Hexalith.Builds.ModuleTool.Tests.Workspace.CatalogVersionTests` | 34 | 34 | 0 / 0 | 0 / 0 |
| `Hexalith.Builds.ModuleTool.Tests.Workspace.WorkspaceAppHostTests` | 12 | 12 | 0 / 0 | 0 / 0 |

Total: **192/192 passed**. All `29` original-graph restored asset files contain zero Hexalith package entries. The mapped AppHost, EventStoreHost, UiHost and Platform composition model are built in private Debug outputs; the copied Platform and SDK Microsoft.Build assemblies match their expected source bytes.

## Preserved attempts and limitations

The first fresh helper/mapping attempt redirected HOME. It returned zero but earns no qualification credit. Its exact environments, commands and logs remain in `/tmp/story117-source-host-20261028-c_px6aaf`; the entire qualification sequence was repeated in a separate fresh run with HOME preserved. Certificate generation remained false throughout.

The first original Debug build failed only on the new test exception’s missing XML documentation (`RCS1140`). The declaration was documented and a uniquely logged retry passed. Four existing `MSB3277` assembly-version conflicts remain in host builds (Redis and IdentityModel); their complete output is retained. No pin, catalog, FrontComposer source or production process semantics were changed. Automatic local source-build package archives stayed private and were never used as fallback or published.

Both Builds and Folders pass `git diff --check`. The root command also emits the existing qualifier-spec CRLF normalization warning. All baseline tracked bytes are preserved except the two intended Builds test files and the qualifier-spec resume append that predates this dispatch; no baseline files are missing and no unexpected tracked changes were observed. Repository indices and HEADs remained unchanged.

No parent closure spec, tracker, prior source-host spec, historical evidence or authority record was changed by this implementation dispatch. V1Only, T0 unset, activation authority null and staging 0/14 remain the scope limits. No Git writes, reference initialization/update, consumed-loader replay, topology/application launch, publication, deployment or installation occurred. Controlled tests create and remove their own private fixture repositories and temporary model data.

Review and successful slice closure are still required separately. Source Debug evidence establishes a local build boundary; it grants no production or packaged-candidate acceptance.
