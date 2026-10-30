# Current Debug distribution qualification — synchronized 2026-10-29

**Qualification remains incomplete: 223/224 current original-assembly cases
passed.** The original private Debug graph, production materialization and all
three required test classes passed. The remaining failure is a production
reconciliation guard: `HexalithDepFromSource=true;;Version=1.0.0` builds successfully
instead of being refused with `HXW006`. The dependent actual Platform model,
sealed-host selection and model PE gates stopped at that failure.

| Current original-assembly class | Discovered / run | Passed | Failed | Errors / skipped / unrun |
| --- | ---: | ---: | ---: | ---: |
| CompositionCommandOptionsTests | 8 | 8 | 0 | 0 / 0 / 0 |
| CompositionSdkProcessTests | 14 | 14 | 0 | 0 / 0 / 0 |
| Workspace.PlatformModelCatalogTests | 24 | 24 | 0 | 0 / 0 / 0 |
| Workspace.WorkspaceMsBuildTests | 133 | 132 | 1 | 0 / 0 / 0 |
| Workspace.CatalogVersionTests | 34 | 34 | 0 | 0 / 0 / 0 |
| Workspace.WorkspaceAppHostTests, permitted cases | 11 | 11 | 0 | 0 / 0 / 0 |
| Total | 224 | 223 | 1 | 0 / 0 / 0 |

Denominators come from current pre-enumerated JSON discovery; executed method-case
counts and XML match. The historical 202-test source-host result remains dated.
Upstream synchronization removed three old launcher cases and changed workspace
coverage; no old denominator was reused.

The exact AppHost method `PlatformPlanDelegatesToThePinnedModelAsync` is excluded
by a method filter because it reaches forbidden `ComposeRun`. It is not counted
as a discovered/run case and supplies no model/runtime compatibility credit. The
other two affected workspace classes and eleven permitted AppHost cases ran as
independent diagnostics after the failed gate; dependent qualification did not
resume. No required assertion was skipped.

The [final receipt](evidence/story-1-17-current-debug-distribution-2026-10-29-synchronized-final.json)
retains the command, first failure, discovery/XML and source diagnosis. The
failed command was:

```text
/home/administrator/projects/hexalith/folders/references/Hexalith.Builds/.artifacts/story-1-17-current-debug-distribution-2026-10-29-synchronized-fixtures-pxrrixld/original/bin/Hexalith.Builds.Module.Tests/debug/Hexalith.Builds.Module.Tests -class Hexalith.Builds.ModuleTool.Tests.Workspace.WorkspaceMsBuildTests -parallel none -xml /tmp/story-1-17-current-debug-distribution-2026-10-29-synchronized-fixtures-pxrrixld/Workspace.WorkspaceMsBuildTests.xml
```

It exited `1`. The failing assertion's child build returned `0` for the malformed
list; the other 132 workspace cases passed. The owner is Builds'
`SourceMappingReconciliationTask.TryCanonicalizeRetainedProperties`.
The [concrete unapplied proposal](story-1-17-additional-properties-empty-fields-proposal-2026-10-29.md)
preserves the trailing-semicolon case introduced by upstream commit
`992850be21cc8b7b10e06cde85e9cc0d0f1f9e1f`, while rejecting leading/interior empty
fields and separator-only lists. Its bound patch passes `git apply --check`;
no production patch or failing expectation has been changed.

The [authorized synchronization](evidence/story-1-17-current-debug-distribution-2026-10-29-submodule-sync.json)
updated eight root references and restored the earlier missing catalog object.
Folders HEAD remains `c3d295b15cd3e0c5f675d014033256458860603f`; Builds now checks out
`e38700b3a8de8ef57342688e9dfbbe3f573201ff`, equal to its staged root gitlink. Its
older committed gitlink `aa46f0c534206b4645b8d9eef08c9a90e1ab540e` remains recorded
separately. All thirteen HEAD/index identities and both committed/indexed versions
of the twelve root gitlinks are bound and rechecked. Production materialization
selected all twelve source origins; all 26 original/fixture graph assets contain
zero Hexalith package libraries.

The mapping keeps committed Platform identity
`3659a44d86c08645215689eaeff8b3624d357dd5`, indexed composition
`965b00249b473d602a663f1f48be39424f996337`, and catalog pin
`4ebd4d869fe1d3958d73e586bcaa74bd86e2ce42`. HXW008/009/010/011 source-mode findings
remain retained, including a diagnostic tool version outside the accepted Platform
range. Source materialization and metadata grant no accepted-tool/runtime credit.

SDK 10.0.402 used a private layout without tool installation/update. Outputs/caches
are private, HOME is preserved, certificates are suppressed, SDK work is bounded
and serial, and diagnostic VersionSuffix `20261029171614` keeps CLI/Tooling/AppHost/
helper Tooling bytes identical. The graph built in 190.8 seconds; its matching
helper build took 4.0 seconds.

The final capsule retains **4,353 members** and binds 123,197,376 bytes with
SHA-256 `a64b8e7b910d208a77b3a809bc47970cf5ba46a1e62f955ba919e87552eb139d`.
It contains original runner bytes, source/reference compiler bytes, 29 Csc vectors
and 323 direct auxiliary compiler inputs (compiler, analyzer, config, resource,
embedded-source and SourceLink files), full logs/binlogs, assets and pinned imports.
Auxiliary capture is a read-only post-execution audit with a retained pre-enrichment
result snapshot. Capture/replay additions alter evidence handling, not the
SDK-executed source graph.

Five complete CLI/Tooling/AppHost/EventStoreHost/UiHost output trees and static PE
metadata for 22 Hexalith assemblies are retained as **compiled, unqualified**,
including deps/runtimeconfig and symbols. They are not an accepted distribution.
The 24 model-catalog fixture cases exercise controlled small model APIs; the actual
Platform model API build and its reference identities remain unexecuted after the
production failure. The PE inspector loaded none of the inspected application
assemblies; metadata supplies no loader/runtime compatibility credit.

The [current handoff](evidence/story-1-17-current-debug-distribution-2026-10-29-synchronized-handoff.json)
binds real unqualified output paths, the direct Parties manifest and supported
`debug --mode source --output json` arguments. It remains unexecuted and blocked
by the repair/full-requalification prerequisite. Ordinary startup, accepted-tool
replacement, signed distribution, actual tool/catalog/composition/module intake
and operational acceptance remain pending.

All 28 guards pass under normal and optimized Python. Final replay passes in both
modes, checking 223/224 outcomes, discovery, runner/output and compiler bytes without
executing commands. Corruption checks reject changed archive/member bytes under
optimized Python. Archives remain ignored/local under the existing policy; text
receipts/manifests are additive. No upload or external archive copy was made.

Historical receipts remain unchanged. The [initial results](story-1-17-current-debug-distribution-initial-results-2026-10-29.md)
preserve the missing-object outcome. Four first synchronized fixture cache failures
were corrected by selecting/restoring the fixture's private cache in a serialized
test; assertions stayed unchanged. The preliminary post-processing capsule's result
pointer is superseded by the final retained snapshot and receives no final credit.

Parent remains **in-progress**, tracker **backlog**, routing **V1Only**, **T0 unset**,
activation authority **null**, staging **0/14**. No production parser edit, commit,
push, nested initialization, application/topology startup, `ComposeRun`,
`--validate-platform`, loader replay, Release packaging, installation, publication
or deployment occurred. Authorized staged gitlinks remain preserved.
