# Story 1.17 — supported source-host mapping implementation

The Builds change is implemented, but qualification is incomplete. The required ordinary source build stopped before compilation with `HXW006` on unchanged FrontComposer reference metadata. A separate focused runner passed **115/115 assertions**, including all 21 host cases, with an observed child SDK environment gap. Neither result closes this spec or the parent story.

The [verification receipt](evidence/story-1-17-supported-source-host-mapping-2026-10-27.json) retains exact commands, explicit environments, SDK/import/output identities, the actual mapping, the failing build log, focused test observations and preservation comparisons. Private raw logs, XML, source-import helper and outputs remain under `/tmp/story117-supported-source-host-qinmz7ig/` and Builds `.artifacts/story117-supported-source-host-qinmz7ig/`. The receipt records selected identities rather than retaining the complete output/cache bytes.

## Changes

- [SourceMappingMaterializer.cs](../../references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Workspace/SourceMappingMaterializer.cs): source-tree EventStoreHost/UiHost exclusions apply only in package mode. Generated `hosts/` projects stay excluded in both modes; hash validation and reconciliation rules are unchanged.
- [AppHost project](../../references/Hexalith.Builds/src/hosts/Hexalith.Builds.Module.AppHost/Hexalith.Builds.Module.AppHost.csproj): the EventStore.Aspire dependency declares `IsAspireProjectResource="false"`, preserved by existing metadata cloning.
- [WorkspaceMsBuildTests.cs](../../references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/Workspace/WorkspaceMsBuildTests.cs): controlled projects copy the three real host dependency declarations, restore through materialized imports, assert Debug project origins/assets without duplicate packages, and check metadata. Twelve invalid graphs cover missing/ambiguous sources, escaped references and mixed package origins before dependent compilation. Generated-host reference retention and source/package scope checks are also covered.
- [CatalogVersionTests.cs](../../references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/Workspace/CatalogVersionTests.cs): catalog override refusal now covers EventStoreHost, UiHost and a generated host. This class has not run in this attempt.
- [README.md](../../references/Hexalith.Builds/README.md): documents mapping scope, package/generated exclusions and the prebuilt AppHost limit. A final wording edit labels building the graph as a prerequisite; no production or test code changed after focused verification.

## Commands and results

All outer SDK operations used the owning Builds root, SDK **10.0.402**, private outputs/cache/feed and bounded Debug commands. `run.py` uses a 900-second bound. The wrapper adds `-m:1 -nr:false -p:RestoreBuildInParallel=false -p:BuildInParallel=false` for build/restore/pack/msbuild. Its explicit environment includes `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`, telemetry/first-run controls and private CLI/NuGet/temp directories. Exact environment and wrapper/driver source are retained in the receipt. These controls were **not established for every child**; see the observation below.

SDK executable SHA-256: `594657cba5f6f34b80e927951f144881ff875df269b6fb5eba1c595730267753`. Owning `global.json` SHA-256: `4ec97d7c7e6bee864d2da2c965f18b94b0bd84e71058427f1f6fa69ee6824652`. Both rehashed unchanged after verification.

| Operation | Observed result |
| --- | --- |
| SDK version preflight | 10.0.402 |
| Source Tooling Debug bootstrap | Exit 0, 31.023 s, zero warnings/errors |
| Source resolver/materializer helper build | Exit 0, 73.895 s |
| Ordinary resolver and materializer | Exit 0, 2.270 s |
| Required source test-project build | Exit 1, 10.340 s, HXW006 before compilation |
| Initial temporary focused runner build | Exit 1, CA1812: incomplete selection of existing shared fixture users |
| Focused runner with original fixture users included | Exit 0, 67.830 s, zero warnings/errors; no analyzer relaxation |
| Focused WorkspaceMsBuildTests | Exit 0, 95.642 s; 115 passed, zero failed/skipped; child environment prerequisite unmet |
| CatalogVersionTests | Unrun |
| WorkspaceAppHostTests | Unrun |
| `git diff --check` in Builds and Folders | Exit 0 in both |

Two package feed enumeration collisions were corrected before SDK/build use. The final prepared feed contains 742 third-party packages, with 30 byte-identical duplicates recorded, and no Hexalith package payload. Only the selected EventStore.Aspire 3.128.0 nuspec was seeded as metadata (SHA-256 `36d376bab8683eaf72702f4df328b3e4ae839c5b0e06892bb6ae6fd9ab5a06bc`). This is no continuous feed-integrity or package-equivalence guarantee.

The ordinary helper called `WorkspaceRootResolver.ResolveAsync` using Builds' existing executable-fixture manifest, then `SourceMappingMaterializer.WriteAsync`. All twelve root references were already initialized/aligned before resolution. Roots/origins and ContentHash came from those production APIs. The resulting mapping hash is `b8d8aa7057a26a72bc145437c28663a2bc9276cec245c9219bab8f90a0d43931`. No private reference-replacement import was used. Existing source-mode warnings remain: HXW011 (untagged Platform), HXW009 (current/dirty Builds versus Platform catalog pin), HXW010 (bootstrap Tooling version outside Platform range).

The required build command was:

```bash
/tmp/story117-supported-source-host-qinmz7ig/bin/dotnet build test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj --configuration Debug --artifacts-path /home/administrator/projects/hexalith/folders/references/Hexalith.Builds/.artifacts/story117-supported-source-host-qinmz7ig -p:HexalithG4FixtureArtifacts=/tmp/story117-supported-source-host-qinmz7ig/fixture-artifacts/ -p:RestoreConfigFile=/tmp/story117-supported-source-host-qinmz7ig/NuGet.Config -p:NuGetAudit=false -m:1
```

It ran from `references/Hexalith.Builds`, with the receipt's explicit environment plus the generated mapping environment/imports and `Configuration=Debug`. It stopped on:

```text
HXW006: Retained ProjectReference '.../Hexalith.FrontComposer.Contracts.csproj' cannot override the mapped configuration because metadata 'AdditionalProperties' is set.
```

The failing consumer is unchanged `Hexalith.FrontComposer.Shell`; FrontComposer's `deps.local.props` sets the inherited metadata. The build log SHA-256 is `360d3d2ef0692b8728ca61bfb3a9d1b14de2115d12ded970d15399711c5a5353`. No upstream metadata, mapping validation, dependency policy or pin was changed to bypass it, and this graph build was not retried.

The independent fallback compiles the actual changed workspace/catalog test files with source Tooling and their existing fixture helpers, excluding the blocked real AppHost graph. It does not replace the original test project gate. Its direct class invocation was:

```bash
/tmp/story117-supported-source-host-qinmz7ig/bin/dotnet exec /home/administrator/projects/hexalith/folders/references/Hexalith.Builds/.artifacts/story117-supported-source-host-qinmz7ig/focused-output/bin/FocusedModuleTests/debug/Hexalith.Builds.Module.Tests.dll -class Hexalith.Builds.ModuleTool.Tests.Workspace.WorkspaceMsBuildTests -parallel none -xml /tmp/story117-supported-source-host-qinmz7ig/WorkspaceMsBuildTests.xml
```

The XML records 115/115 assertions passing on .NET 10.0.13; XML SHA-256 `bdd81fce42077bad0e8741d91ce631ecee82f7c1a3b52ea59ed153cd398574d4`. A sampled `CompositionProcess` child resolved the real `dotnet` host despite the wrapper-first PATH; certificate prevention was absent and argv lacked the wrapper's serial switches. The existing child-environment allowlist excludes those controls. Resolution precedence is inferred from sampled executable/argv/environment, not a complete process trace. No certificate-store equality check was performed. The class had completed naturally before the attempted stop inventory found any owned process (`owned=[]`); no termination signal or relaunch occurred. This supplies assertion evidence with a prerequisite failure, not a compliant verification pass.

## Matrix and acceptance audit

| Frozen matrix row | Focused evidence | Remaining gate |
| --- | --- | --- |
| Mapped source hosts | Three real-declaration controlled restores passed; Debug/project assets/no package duplicates asserted | Real source test-project build blocked; child environment incomplete |
| Aspire library | AppHost controlled restore preserved `IsAspireProjectResource=false` | Original project/AppHost class gate unrun |
| Package hosts | Both package-mode source-host scope cases passed | CatalogVersionTests, including all three host refusal cases, unrun |
| Generated hosts | Both modes passed, including retained package references | Compliant child environment run outstanding |
| Invalid source | Twelve categorized refusals passed before compile/restore output | Compliant child environment run outstanding |

Acceptance is incomplete: controlled restore assertions support AC1, AC2 lacks the required catalog class run, and AC3's evidence separation is recorded without claiming successful qualification. Step 03 remains open; review/completion steps were not entered.

## Preservation and remaining prerequisites

All thirteen revisions, raw index hashes, staged-entry hashes and index-flag hashes matched before/after verification. Protected tracked bytes matched across the workspace and all twelve root references. The initial capture began after three intended Builds edits (materializer, AppHost project and README); initial owning Git status was clean, and these files were separately audited against HEAD. Only the five intended Builds files differ from HEAD. The two new Folders result/evidence files and this spec's progress edits are additive. Full before-capture SHA-256: `51b7aa838e2429af4538fbf0f232a84538b85d1bbd0111372c6cd95597aabac3`; comparison SHA-256: `e7e9a2345d233134ef01e7c004a99edd82e5cfede80167744e57915c109366ab`.

| Repository | Unchanged revision |
| --- | --- |
| `.` | `98222637c0e5df4dbb753a51845bb32a06282da9` |
| `references/Hexalith.Tenants` | `24fb584b8d2b8a3bc7a51fcf8b5a6266e21e53e7` |
| `references/Hexalith.AI.Tools` | `3f194e17174994d308ec84af9ee2b5aa68674d0d` |
| `references/Hexalith.EventStore` | `c5d1c280d8c1c1a207cd159d817f614e93c93130` |
| `references/Hexalith.FrontComposer` | `cfa9febc2942666265f23f87b26112c846f625ad` |
| `references/Hexalith.Memories` | `7b0c8b5692600e293346eea7a84652eb87e4c346` |
| `references/Hexalith.Commons` | `1663028fb6d9f46a0bf17919edeb21bb99be1587` |
| `references/Hexalith.Builds` | `74932ad0247203797274aba8b7b6366a4bbf92b9` |
| `references/Hexalith.PolymorphicSerializations` | `221ac4592ba908ae7661ba1fa9e2d1fcc23baee0` |
| `references/Hexalith.Projects` | `298f6f6a41b9ce7b67f14c21c724d76832b957a9` |
| `references/Hexalith.Platform` | `9869094acc21f9054960a573f884c60ddf3235cd` |
| `references/Hexalith.McpCli` | `dc282889e624a4c990faa3f005ad7eaa8db7a473` |
| `references/Hexalith.Parties` | `986a369ec26f6c5194593eb7bce12955eda54149` |

The active parent closure spec remains **in-progress**, tracker **backlog**, routing **V1Only**, **T0 unset**, activation authority **null**, and staging **0/14**. Historical receipts/loader cases, catalogs, dependency/native/provider pins and approval records are unchanged. No real AppHost/application entrypoint, ComposeRun, topology, consumed loader replay, production Release qualification, publication/deployment, installation, reference initialization, staging, commit or push ran. Synthetic package fixtures confer no production distribution acceptance.

Before this build boundary can qualify, resolve the FrontComposer metadata/source-mapping incompatibility within an approved follow-up scope, establish child SDK environment/serial controls before execution, then pass the original supported-source test-project build and all three required class runs. Preserve validation and package/source separation while doing so. Distribution selection, ordinary-command execution, signed-package/native/layout checks, dependency-policy resolution and hosted/operational acceptance remain separate later prerequisites; the [dated source-loader observations](story-1-17-current-builds-host-results.md) are unchanged and were not replayed.
