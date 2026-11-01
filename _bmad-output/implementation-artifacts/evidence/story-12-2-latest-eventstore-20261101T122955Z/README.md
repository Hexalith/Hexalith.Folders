# Story 12.2 latest EventStore intake

EventStore is current at `f704401ff9033919b1e9925564d814d7410bfe3d`.
`git -c submodule.recurse=false pull --ff-only --no-recurse-submodules` returned
`Already up to date.` The SDK's sibling Tenants resolution is exercised by the passing
authoring/guardrail selection. Nested Tenants source is absent; the root-declared sibling source is
present. No nested submodule was initialized.

The previous missing `RouteOperationContract` source-build blocker is cleared. Current Debug/source
builds of Server.Tests, IntegrationTests, AppHost.Tests, Workers.Tests, core Tests, SDK DomainService.Tests
and Contracts.Tests pass with zero warnings/errors. All inspected Hexalith dependencies are project
references; no `BuildProjectReferences=false` fallback is used.

## Build selection

Use `-p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:HexalithEventStoreVersion=3.130.2`.
The last property consistently applies the existing catalog selection. Initial default builds succeeded
but copied SDK assemblies at 3.129.0 beside consumers requesting 3.130.2. The original failed runtime
controls and `identity-*.txt` retain that mismatch. The corrected source build uses no package/catalog
file edits or artificial all-library Version override.

SDK guardrail output must reside under the EventStore repository so source-root discovery works.
Integration output uses a standard single directory below `.artifacts/`: the first extra `coherent/`
level broke the existing Memories topology helper's fixed-depth lookup. Correcting this task-owned
layout restored all 14 topology cases without changing Memories or topology source. Actual output
paths and expanded commands are retained in the receipts and `build-selection.json`.

## Executed results

| Selection | Result | Receipt/XML prefix |
| --- | --- | --- |
| SDK sibling/authoring/public composition | 38 passed, zero failures/skips | `sdk-sibling-guardrails-corrected` |
| SDK HTTP security | 6 passed, zero failures/skips | `sdk-http-security` |
| Production Server enrollment/composition | 38 passed, zero failures/skips | `server-enrollment-coherent` |
| Previously failing signed-command positive | 1 passed, zero failures/skips | `signed-command-positive-coherent` |
| Full Server after assertion correction | 1,315 passed, zero failures/errors/skips | `server-full-corrected` |
| Full Workers | 162 passed, zero failures/errors/skips | `workers-full` |
| Operational histories | 87 passed, including 10,001/32,769 positions; zero failures/skips | `operational-histories` |
| Integration authentication/behavior focus | 170 passed, zero failures/skips | `integration-fixture-authentication` |
| Integration topology focus | 14 passed, zero failures/skips | `integration-topology-standard-layout` |
| Full Integration after fixture corrections | 853 passed, zero failures/errors/skips | `integration-full-corrected` |
| AppHost HTTP/rebuild helpers | 22 passed, zero failures/skips | `apphost-helpers` |
| Full Contracts | 623 passed, 11 failed, zero errors/skips | `contracts-current-full` |

These overlapping selections are not summed into a unique-case total. The 11 Contracts failures are
the pending PD10 candidate/inventory/generator and immutable-CI-pin conformance gates; their exact
names and messages are retained in `failed-attempts-and-gates.json`. No frozen manifest or CI pin was
changed to make them pass. Earlier artifact-placement, assembly-identity and fixture failures remain
retained as diagnostics rather than passing coverage.

## Fixture corrections and limits

The source changes correct one stale Server assertion identifier and four Integration fixtures.
The pipeline-order assertion still requires authentication before authorization before endpoint mapping.
Integration hosts now compose authentication before the compatibility seam and authorization after
routing. The routing fixture binds its existing explicit test scheme to the named human policy while
retaining the SDK's original credential-kind requirements. Existing behavior, authority-call-count,
state, correlation and privacy assertions remain intact. Synthetic fixture authentication supplies no
production JWT or live conformance claim.

`source-inputs.json`, `runtime-inputs.json`, `project-graphs.json` and `qualification.json` capture the
final selected source/config inputs, separate runtime groups, project-reference assets and full Git
heads. These are final fingerprints, not a pre-run whole-source audit or a claim that every output group
is byte-identical. The Integration fixture receipts separately retain their bounded late-run hash check.

No current strict real-provider acceptance command or topology was started. AppHost compilation and
helper tests do not establish live persistence/restart/provider qualification. Story 12.2 remains in
progress for full live acceptance, pending contract/governance and Release/package/PD10 gates, SM4
acknowledgement performance, SDK broad-regression qualification and independent whole-story review.
Earlier sealed evidence packets keep their historical exact-source dispositions.
