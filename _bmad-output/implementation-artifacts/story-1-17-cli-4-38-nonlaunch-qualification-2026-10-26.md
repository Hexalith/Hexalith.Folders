# Story 1.17 — isolated CLI 4.38.0 qualification results

**The authorized Debug model build passed: exit 0, zero errors, one MSB3277 Redis assembly-conflict warning. Required Tooling and Aspire direct-reference checks passed. EventStore/Redis runtime compatibility remains unresolved, so current-tuple acceptance stays HOLD.**

Jerome's [approval](story-1-17-cli-4-38-nonlaunch-qualification-approval-2026-10-26.md) authorized the [bounded plan](story-1-17-current-platform-tool-catalog-qualification-plan-2026-10-25.json). The specified build and reference inspection ran, but the immediate pre-build SDK identity gate was incomplete; this is **partial procedural conformance**, not full qualification or acceptance. The [execution receipt](evidence/story-1-17-cli-4-38-nonlaunch-qualification-2026-10-26.json) binds actual command records, stdout/stderr, filesystem inventories, package/source provenance, resolved assets, metadata, review limitations and retained binaries. Historical proposal and owner-intake/composition receipts were preserved.

## Executed tuple and preflight

| Input | Exact identity |
| --- | --- |
| Folders | `b8c66d649ccfd0700392ca65840a24e4cacdeecc` |
| Platform | `9869094acc21f9054960a573f884c60ddf3235cd` |
| Selected Builds catalog | `022668ac2dd3b0ec4b1b8e99b243debef0eee374` |
| Direct Builds | `74932ad0247203797274aba8b7b6366a4bbf92b9` |
| Direct Parties | `986a369ec26f6c5194593eb7bce12955eda54149` |
| Direct EventStore | `c5d1c280d8c1c1a207cd159d817f614e93c93130` |
| CLI 4.38.0 source | `e171c5a788eab183b32683431b084198c9b8d373` |
| .NET SDK | `10.0.402`, resolved under Platform's unchanged `global.json` |

Root advanced from the proposal's `b5ae26e4ce2df1bc4a275f1272bf3d147eef2a2b` only through a commit adding the six proposal artifacts. Their hashes, all direct gitlinks, submodule indices and prior 73,719 source-file bindings matched. No new pin or source combination was substituted. Root and all twelve direct repositories were clean before execution, with no staged entries.

The initial collector comparison mishandled a tracked documentation symlink, and its second marker assertion incorrectly included a pre-existing standalone fixture under root `artifacts/`. These were collector errors, not source drift: the symlink target bytes and index matched, and the fixture's superproject query returned empty. Original driver versions, the subsequent symlink diagnostic and the marker-failure record are retained. The first symlink-failure receipt was overwritten by the second failure; the two retained `failure-preflight*.json` files both contain the marker failure, not two original failures. The diagnostic is not a reconstruction of the missing original record. The corrected check still rejects additional Git markers under every direct checkout. A guarded preparation call before preflight completion refused without running SDK/package/build commands.

Private workspace: `/tmp/story117-cli438-qualification-20261026-fk5qKp`. Build outputs, restore/helper caches and temporary paths used the approved private environment overrides. Only the existing nuget.org feed was observed. No SDK installation, accepted-tool selection or dependency change was performed. External configuration/certificate stores were not covered by the before/after inventory; the SDK's certificate message is disclosed below.

Initial SDK selection, executable/global.json hashes and SDK/MSBuild versions were captured. Immediately before building, `dotnet --version` exited 0 and reported `10.0.402`, but the driver did not assert that output or rehash the executable/global.json at that gate. Post-execution reconciliation confirms their current hashes match the initial ones; repository snapshots also preserve global.json bytes. This does not retroactively complete the planned immediate check. No rebuild was performed to hide that gap.

## Package, catalog and build

The existing candidate archive was rehashed, copied into the private workspace, signature-verified and safely extracted. Archive SHA-256: `f352d3e0f03f21dbfe42c89adb7a8765cb073a2351281acaa3cf304235426ab1`, 37,220,356 bytes. `dotnet nuget verify ... --all` exited 0 with a NuGet repository signature. Recorded nuspec, CLI, Tooling and packaged-host member hashes matched the approved package. No candidate public CLI command was invoked.

`git show 022668ac2dd3b0ec4b1b8e99b243debef0eee374:Props/Directory.Packages.props` exported the catalog unchanged: SHA-256 `140e161c0ac14183be0cca8babce5cc896355174e1c621979b92d49826bdadb2`, no imports. The pre-build MSBuild query exited 0 and confirmed Debug, explicit source-reference/root properties, the candidate Tooling DLL and this catalog. It evaluated 307 package-version items, including Aspire.Hosting 13.6.3 and EventStore.Aspire/ServiceDefaults 3.128.0.

The full actual argv and explicit environment overrides are retained in `logs/model-build.command.json`; `sdk.json` records selected inherited SDK variables. The rest of the inherited environment was not captured, which limits reproducibility. It is the approved `timeout --signal=TERM --kill-after=10s 600s dotnet build src/Hexalith.Platform.Composition/Hexalith.Platform.Composition.csproj --configuration Debug ...` invocation from the Platform repository, with the plan's unchanged selectors and private paths substituted. It ran from `2026-10-26T05:37:35.439492+00:00` to `2026-10-26T05:37:55.409121+00:00`, exit **0**, approximately 19.97 seconds. There was one attempt; no rebuild or corrective flags followed.

Build stdout SHA-256: `411048c355da86c8e5dbf8927c9b48e2f309e0e23942b7dce1a4fe8440872c9e`. Stderr was empty. Emitted model SHA-256: `3d793745d43e16e47e056a18e41279abb5f6512666388e11d04dee8629d85ad8`; informational version binds Platform `9869094acc21f9054960a573f884c60ddf3235cd`.

All nine `project.assets.json` files were retained. Their Hexalith library dependencies are project references, with no unexpected Hexalith NuGet fallback; packages resolve through the private cache. EventStore source assets identify version 3.128.0. Commons' normal build targets also emitted diagnostic 1.0.0 packages inside the private artifacts directory; nothing was published or treated as a release. The helper-compatible `NuGetAudit=false` and `MinVerVersionOverride=1.0.0` remain bounded diagnostic inputs, not security-audit or release evidence.

These explicit source/root globals are the approved qualification overrides. This build does not establish the graph selected by a later ordinary public command, whose model build and genuine mapping still require their own evidence.

## Reference findings and remaining limits

| Check | Actual result |
| --- | --- |
| Model → Tooling | Requests 4.38.0.0. Model copy, candidate CLI directory and packaged host Tooling bytes all equal approved SHA-256 `0266257a74c1dbc49407c61b07d90f7c95b806d937ea4f338b98bf8d128256fd`. |
| Model → Aspire.Hosting | Requests 13.6.3.0 with token `cc7b13ffcd2ddd51`; packaged host DLL has the same identity. `Aspire.Hosting.dll` is absent from the model output. This compares the model's AssemblyRef against the host DLL, not a resolved runtime load. |
| Model → EventStore.Aspire | Requests/source-builds 3.128.0.0; packaged host carries and references 3.127.0.0. Runtime resolution/compatibility is unresolved. |
| Model EventStore.ServiceDefaults | Source-builds 3.128.0.0. It is not present in the packaged host directory; it exists in the model output. No full loader result is inferred. |
| Redis | Model restore selects package 2.13.17, assembly 2.0.0.0. Tooling requests assembly 3.0.0.0; the packaged host carries package 3.4.5/assembly 3.0.0.0. MSBuild chose the primary 2.0.0.0 reference and emitted MSB3277. |

The selected catalog already lists Redis 3.4.5. A focused read-only query with the same build selectors confirmed `CentralPackageTransitivePinningEnabled` is unset and `ImportDirectoryPackagesProps=false`; restored assets show Aspire.Hosting.Redis requesting Redis 2.13.17. Platform's bypassed `Directory.Packages.props` normally sets transitive pinning true. This explains the compile-warning path without requiring a different catalog pin. No transitive-pinning flag, package reference or pin was changed to suppress it.

All eight planned assembly-metadata commands and two additional Redis metadata commands exited 0. Packaged EventStore's missing portable PDB prevented optional source-document/compilation-reference inspection; required assembly metadata was available. These are metadata observations only. Neither the model nor the packaged AppHost was loaded into its runtime context or started. No failure or success of their actual EventStore/Redis binding is claimed.

The four missing 4.35.2 API references are now covered by an actual successful 4.38.0 model compile. That resolves the identified compile blocker only. No necessary source/dependency repair has been established for the remaining runtime differences, so there is no speculative patch or pin change. Any later repair must be separately proposed exactly; ordinary execution remains unauthorized. The existing source HXW009 revision warning and missing-release-tag condition remain distinct from these assembly findings and from package-mode requirements.

## Preservation and handoff

Before/after inventories covered 169,980 paths **under the Folders repository tree**, including ignored files, hashing 13,844,637,897 regular-file bytes. At `2026-10-26T05:43:32.154465+00:00`, comparison found **zero added, removed or changed inventoried paths**. The collector excludes `.git` internals and marker contents, records marker names, and records symlink target strings without traversing them. It does not establish whole-machine preservation or byte/ownership preservation of Git markers. Separate Git queries show all thirteen repositories retained their revisions and indices and remained clean. The raw collector's `outsidePrivateDirectoryPreserved` and `gitMarkersPreserved` labels must be read with these narrower limits.

SDK first-use stderr (`logs/sdk-3.stderr`) reports **“Installed an ASP.NET Core HTTPS development certificate.”** The post-execution directory/lstat observation found one real-user certificate-store entry with May 2026 mtime/ctime, predating this run, and no store at the inspected private-home location. No certificate contents were read or cleanup attempted. There is no before-store inventory: the message is retained as a reported side effect, not proof of newly created certificate bytes or proof that all external state was unchanged.

The earlier `survivors: []` summary has no retained observation timestamp/method and is not independently auditable. A separate timestamped post-review `ps -eo pid,ppid,pgid,stat,comm` record found no process in the owned build group `38222`; this establishes that later observation only and does not cover processes that escaped that group. The new approval/report/receipt/archive/manifest were written after the repository comparison as unstaged evidence only. Seven review findings were addressed through evidence clarification and read-only follow-up; no build, model or host execution was repeated.

No completed historical verifier, graph test or adoption audit was rerun. No ordinary mapping or `ContentHash` exists for this attempt. CLI 4.35.2 remains the historical accepted tool; 4.38.0 remains an unaccepted candidate with bounded compile/reference evidence. Current-tuple, package and hosted acceptance remain HOLD.

The EventStore/Tenants/Memories dependency-policy conflict is unchanged and separate from the EventStore assembly-version observation here. No runtime edge, declaration, registration, hosting accountability or retirement decision was invented. Parent **in-progress**, tracker **backlog**, **V1Only**, unset **T0**, null **activation authority**, and **0/14 staging checks executed** remain unchanged. No staging, commit, push, publication, deployment, dependency update or nested initialization was performed.
