# Story 1.17 — supported source-host graph qualification

Implementation is present, but qualification is **incomplete**. Production resolution refused a checkout/gitlink mismatch before materialization. The original Debug test-project graph and its required class gates were not executed. A reused independent focused runner passed **180/180 tests**, with zero failed or skipped tests. This does not close this slice, the preceding source-host slice, or Story 1.17.

The [verification receipt](evidence/story-1-17-supported-source-host-qualification-2026-10-27.json) retains every actual command and explicit environment, all failed setup outputs, SDK/global.json identities, observed revisions/index identities, test totals/XML identities, source hashes, process observations and private log identities. Raw outputs remain under `/tmp/story117-source-host-qualification-y4qfm8vi/` and Builds `.artifacts/story117-source-host-qualification-y4qfm8vi/`. The receipt binds selected files rather than retaining the full output/cache byte closure.

## Implementation

- Builds reconciliation validates retained source-reference `AdditionalProperties` against `SourceMapping.LoadVerified`: only mapped identity roots/origins and an exact catalog `Version` are permitted. It resolves physical identity, interprets relative roots from the defining project directory, and clones canonical mapped root spellings. Unknown, malformed, duplicate, conflicting, unmapped and configuration assignments refuse with HXW006. Input task metadata, Aspire metadata, existing conversion refusals and materialized child guards are preserved.
- The test-only `CompositionSdkProcess` automatically resolves a validated absolute SDK executable or accepts `HEXALITH_TEST_DOTNET_EXECUTABLE`. It preserves HOME/USERPROFILE, scrubs unrelated inherited environment values, explicitly prevents certificate generation, uses private CLI/cache/temp directories, and supplies supported command-specific serial arguments. Production process behavior is unchanged.
- Workspace and catalog SDK children use that launcher. Seventeen new forwarding cases cover canonical child evaluation, restore assets, mixed origins, unchanged input metadata and early refusal. Existing toy consumer checks now build and inspect assets instead of launching applications.
- The original test project adds SDK `Microsoft.Build.dll` beside its existing Framework and Utilities SDK references. The first focused class run demonstrated its absence at the in-process task assertion; no project reference or package pin was replaced.
- Builds README documents forwarding and its limits. FrontComposer source, materializer imports/guards, catalogs, pins and production process code were not edited by this implementation.

## Production prerequisite and stop

At preflight, Folders was `f48886763a1b57b30f7767deb9909f741dc812f3` and Builds was `cbe58fa72f92327752fe0d75617ae91fe18da5d9`. The read-only forwarding input is FrontComposer `8d2b47bd206ffd5c5e77a5ac0461b9cfe52ea4fd`.

A native helper first passed a bounded, non-SDK bare-dotnet interception probe, then called the production `WorkspaceRootResolver.ResolveAsync` for the existing executable-fixture manifest. Its exact command was:

```text
/home/administrator/projects/hexalith/folders/references/Hexalith.Builds/.artifacts/story117-source-host-qualification-y4qfm8vi/bin/SourceMappingHelper/debug/SourceMappingHelper /home/administrator/projects/hexalith/folders/references/Hexalith.Builds/test/fixtures/module/executable/hexalith.module-manifest.v2.json /tmp/story117-source-host-qualification-y4qfm8vi/workspace
```

It exited `-6` after 2.537 seconds with `WorkspaceMappingException`:

```text
Required source 'Hexalith.EventStore' at '/home/administrator/projects/hexalith/folders/references/Hexalith.EventStore' is unavailable: Checkout HEAD '092abef078b4a70a77831fe0f60d76b3a85a8f1c' does not match the root's staged gitlink '4dc256c4db7a0ba88efdbbe0f6bcbcf047c09d34'.
```

The same observation found Platform HEAD `9ead86f9061695bf3e30bcb0e020c0b830ddaf7e` versus staged composition gitlink `ef56bc48e8f328ec36eb68c47216060fcb8abd97`, and Builds HEAD `f914752cf8223b683799d4b25b4b0f742863297b` versus staged `cbe58fa72f92327752fe0d75617ae91fe18da5d9`. Existing source-mode tag/catalog/tool-range findings are retained verbatim in the receipt. The resolver failure log SHA-256 is `69deb9f5978de067af3ed628c51b50996ad39f8104dd1b251f5d1cc8b971c555`.

No production mapping/hash/import identity was produced: the mapping is **null**, not the preceding attempt's mapping. `WriteAsync` and dependent original graph execution did not occur. No checkout/gitlink was fixed, no resolver replay ran, and no replacement root/mapping or Hexalith package fallback was used. Observed external repository drift prevents a claim that all revisions or indices remained unchanged; the parent workflow owns the final preservation comparison.

## Controlled fallback evidence

SDK **10.0.402** preflight passed. Executable SHA-256 is `594657cba5f6f34b80e927951f144881ff875df269b6fb5eba1c595730267753`; Builds global.json SHA-256 is `4ec97d7c7e6bee864d2da2c965f18b94b0bd84e71058427f1f6fa69ee6824652`. The driver rechecked both before each outer command, bounded execution to 900 seconds and killed its process group on timeout. Launcher children have a two-minute bound, with a separate one-second timeout probe. MSBuild commands receive `-m:1 -nr:false -p:BuildInParallel=false -p:RestoreBuildInParallel=false`; restore also receives `--disable-parallel`. Compilation in the focused lane disables shared compilation.

The previous slice's prepared focused runner was reused with the two new launcher source files, explicit early imports of the same Builds conventions/catalog after moving outside the repository, and the same SDK test-support reference. The transient project is `/tmp/story117-source-host-qualification-y4qfm8vi/focused/FocusedModuleTests.csproj`. It references source Tooling and the actual changed test files; it omits the blocked original AppHost graph and cannot substitute for it. No original project reference was replaced.

Before SDK-child tests, launcher construction passed 10/10 and the native runner's non-SDK catalog interception probe passed 1/1. The native xUnit executable was used so the bare-dotnet production catalog probe reaches the private wrapper; the prior dotnet muxer resolution gap is not relied upon.

| Verification | Result |
| --- | --- |
| Final source Tooling/helper build | Exit 0; zero warnings/errors |
| Final focused runner build | Exit 0; zero warnings/errors |
| `CompositionSdkProcessTests` | 14 passed; zero failed/skipped; includes actual environment propagation and timeout |
| `WorkspaceMsBuildTests` | 132 passed; zero failed/skipped; 147.134 seconds |
| `CatalogVersionTests` | 34 passed; zero failed/skipped; 56.990 seconds |
| Original Module.Tests Debug graph build | Unrun: failed production resolution prerequisite |
| All original-assembly class gates, including `WorkspaceAppHostTests` | Unrun |
| `git diff --check` in Builds and Folders | Exit 0 in both; Folders reported its existing CRLF-to-LF normalization warning for the prior spec |

Final class commands use the following native executable, with each fully qualified class selected separately and `-parallel none -xml <private-report>`:

```text
/home/administrator/projects/hexalith/folders/references/Hexalith.Builds/.artifacts/story117-source-host-qualification-y4qfm8vi/focused-output/bin/FocusedModuleTests/debug/Hexalith.Builds.Module.Tests
```

The receipt contains each exact argument vector, environment, duration and log hash. The final workspace XML is `WorkspaceMsBuildTests-final.xml`; final launcher XML is `CompositionSdkProcessTests-final.xml`; catalog XML is `CatalogVersionTests.xml`. Counts above are final class totals and exclude repeated construction/probe/recheck assertions.

A 20-second process sample captured 25 direct SDK children with certificate prevention, private paths and all applicable serial arguments. The wrapper retained 47 MSBuild/catalog SDK invocations with those controls, plus six pwsh.dll runtime-tool invocations for which MSBuild serial arguments do not apply. All 53 intercepted invocations had certificate prevention. These are explicit construction/propagation checks and sampled/intercepted observations, not a whole-machine process trace or certificate-store comparison.

The private feed inventory contains 742 third-party package entries and no Hexalith package payload. Only the existing EventStore.Aspire 3.128.0 nuspec was copied as catalog metadata. Isolated package fixtures establish no production distribution or Release qualification.

## Failed setup attempts retained

The receipt preserves full outputs and exact command identities for every failed attempt:

- Tooling bootstrap first omitted existing pwsh from the explicit PATH; after correcting PATH, StyleCop caught member ordering. Both were corrected before the final successful helper build.
- Focused compilation caught a Task name collision and formatting/documentation analyzer errors. These were corrected without suppressing analyzers. A formatting/relocation script raised IndexError before moving the temporary project; the following build failed MSB1009 because its destination did not yet exist.
- Moving the existing focused project outside the repository initially lost automatic central-version discovery. Two attempts failed NU1015; placing the same conventions/catalog paths before SDK props imports restored the original import timing. A remaining duplicate blank line then failed SA1507 and was removed.
- The first full workspace run passed 128/132 and failed four new in-process assertions because Microsoft.Build.dll was absent. The narrow SDK reference addition passed a 4/4 recheck and the full corrected 132/132 class run.
- Initial outer commands used private HOME/USERPROFILE values. Audit corrected subsequent runs to preserve them and isolate through DOTNET_CLI_HOME and explicit caches/temp paths. The final launcher asserts preservation.

## Remaining gates and authority

The original supported source graph must resolve and build successfully under an authorized, stable checkout/gitlink state before the original assembly's launcher and all three workspace class gates can execute. This attempt deliberately stops that dependent path. The fallback's 180 passes establish assertion evidence only; the real host graph, original `WorkspaceAppHostTests`, distribution/loader and hosted/operational acceptance remain unqualified.

The parent remains **in-progress**, tracker **backlog**, routing **V1Only**, **T0 unset**, activation authority **null**, and staging **0/14**. This implementation performed no Git mutation, reference initialization, tool installation, application/topology startup, consumed loader replay, publication or deployment. Final preservation, task/matrix audit and spec status remain with the parent workflow; neither slice is transitioned to review/done here.

## Independent parent audit

The [parent verification](evidence/story-1-17-supported-source-host-qualification-2026-10-27.parent-verification.json) rehashed all 44 retained artifact bindings and seven implementation files, parsed the final 180 passing XML cases, and found passing focused coverage for each matrix row. The original graph/assembly acceptance gates remain unrun; Step 03 stays incomplete and no review/completion transition occurred.

Comparison with the pre-implementation capture covered all thirteen repositories. Nine dependency HEADs advanced externally, with additional unrelated tracked changes; those changes were preserved. The root tracker, parent closure spec, approval policy, planning manifest, candidate conformance set and .gitmodules are unchanged. These are dated observations, not a claim that the concurrently changing workspace is qualified. Both owning-repository whitespace checks pass.

A concluding source check still matches all seven implementation hashes. Concurrent work advanced Builds again and its working tree became clean; this workflow issued no Git mutation or remote operation. The final root diff command returned zero but emitted `error: hash mismatch 6ba02654ca390b3333183962355bd5c5857b8224` for the changing Parties reference. That observation is retained without repair; no whole-workspace diff/review or clean-baseline claim is made.
