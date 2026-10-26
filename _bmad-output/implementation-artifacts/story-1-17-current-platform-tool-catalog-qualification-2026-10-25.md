# Story 1.17 — current Platform/tool/catalog qualification decision

**Current Platform cannot be qualified with accepted CLI 4.35.2: its model references four APIs absent from that released tool. No additional repository pin change is justified. The smallest supported next candidate is CLI 4.38.0, used in an isolated, non-launch qualification lane. It is not accepted yet.**

The [conditional qualification plan](story-1-17-current-platform-tool-catalog-qualification-plan-2026-10-25.json) names the exact tuple, package and command arguments. The [evidence receipt](evidence/story-1-17-current-platform-tool-catalog-qualification-2026-10-25.json) binds the retained commands, source inspection, package/API inspection, prior capsules and preservation checks. This is decision preparation, not current-tuple or hosted acceptance.

## One decision for Jerome

Authorize **only isolated non-launch technical qualification of the hash-bound CLI 4.38.0 package below against the current Platform and selected catalog**, using the plan's bounded model build and dependency-reference inspection. Keep CLI 4.35.2 as the historical accepted identity until a subsequent disposition records the new results. Stop on failure; do not change pins, downgrade source, broaden tests or install another version to make it pass.

This permits temporary extraction and explicit use of the candidate's tooling assembly for qualification. The plan sets private build/cache/temp locations, permits restore from existing feeds without version changes, and checks for unexpected writes outside the qualification directory. It does not authorize replacing a global/local tool selection, repository edits, an ordinary `run`/`debug` attempt, runtime prerequisite installation, mapping fabrication, package-mode acceptance, or hosted acceptance. No new runtime/declaration/registration decision is bundled into it.

The requested authority is new because the Parties approval covered source intake only, and CLI 4.35.2 acceptance does not cover this different executable. A passing technical check is evidence, not automatic acceptance. If declined, retain HOLD with the API incompatibility recorded.

## Reconfirmed state and external transition

At the first observation, Folders was `09707e19ab7024b88b34d89651e15a45bc895eeb`: `.gitmodules` and Parties were staged exactly as approved, Platform was the sole checkout/gitlink mismatch, three proposal files were edited, and the application receipt/approval note were untracked. During read-only investigation, external commits `6566b154ac940f4274780665818396e689036298` and `b5ae26e4ce2df1bc4a275f1272bf3d147eef2a2b` recorded that work and advanced the Platform gitlink. This task issued no staging, commit, push, checkout or submodule mutation. It preserves the externally recorded state instead of reconstructing the former index.

Current root is **`b5ae26e4ce2df1bc4a275f1272bf3d147eef2a2b`**, branch `main`. The captured preflight has no staged or unstaged changes. All twelve direct checkouts match their root gitlinks and are clean; no nested Git markers are initialized. New decision artifacts remain unstaged. Historical approval/application bytes and Parties' exact checkout remain preserved. External commits are provenance, not inferred owner acceptance.

| Identity | Full revision |
| --- | --- |
| Platform, committed/index/checkout | `9869094acc21f9054960a573f884c60ddf3235cd` |
| Previous recorded Platform identity | `30673fdab03b5c5d527eb9eb596bfa6645ad6649` |
| Selected Builds catalog, from current committed Platform | `022668ac2dd3b0ec4b1b8e99b243debef0eee374` |
| Previous identity-selected Builds catalog | `fd4b1a54ae3da4e1dde4063058b747f4156137df` |
| Direct Builds, root gitlink/index/checkout | `74932ad0247203797274aba8b7b6366a4bbf92b9` |
| Approved direct Parties, root gitlink/index/checkout | `986a369ec26f6c5194593eb7bce12955eda54149` |
| Accepted CLI 4.35.2 source | `4248b8c70cb35ba79fdd7e4dd63049d4c73211a0` |
| Proposed CLI 4.38.0 source | `e171c5a788eab183b32683431b084198c9b8d373` |

Every other root and nested recorded dependency revision is retained in the capsule. Nested pins are provenance only. Platform's old identity is an ancestor of its current checkout; the delta has 22 paths, including the Builds pin, EventStore AppHost package declarations `3.127.0 → 3.128.0`, tests, Custody guards and Works evidence helpers. The composition production source did not change across those two Platform revisions. The incompatible API use predates both, at Platform `dcb01b1bcbcfe678781674841697de383f25165d`. Restoring the old recorded Platform gitlink would not solve it.

## What is a warning, and what blocks progress

These are source-derived classifications, not diagnostics from a new public CLI attempt. No such attempt was made.

| Finding | Source-mode consequence | Package-mode consequence |
| --- | --- | --- |
| Platform checkout/index/committed identity mismatch, HXW008 | Identity warning; formerly present, now resolved by the external commit | Identity findings refuse package mode |
| Direct Builds revision differs from Platform's pinned catalog, identity HXW009 | Warning retained; the pinned Git object supplies the catalog, not direct HEAD | Refusal until the required clean/aligned identity contract holds |
| No local release tag at selected Platform identity, HXW011 | Warning; no synthetic tag/version is added | Release identity is required |
| Missing catalog object/import, malformed or ambiguous catalog, failed MSBuild evaluation, materializer HXW009 | Actual prerequisite failure even in source mode | Actual prerequisite failure |
| Tool outside Platform's declared range, HXW010 | Identity warning; requires a supported qualification decision, not automatic proof of incompatibility | Identity finding refuses package mode |
| Missing tooling APIs used by Platform | Actual model compilation incompatibility | Same incompatibility; identity fixes do not repair APIs |

The current Platform range `[4.35.2, 5.0.0-0)` admits both tool versions syntactically. That range does not prove API compatibility. Released `PlatformIdentityResolver.cs:52–64` converts identity findings to source warnings; `WorkspaceRootResolver.cs:221` refuses them in package mode. `PlatformManifestCommand.BuildModelAsync` explicitly passes the running tool's `BuildsToolingAssemblyPath`; the model cannot quietly substitute direct Builds source.

Package mode additionally enables catalog checks before restore and against resolved assets before compilation (`SourceMappingMaterializer.cs:216–222`), and must satisfy package selection/version validation. Those package-only targets are not enabled in source mode. Both modes still require valid materialization of the selected catalog and their own mapping/project validation. Repository policy separately requires Release/package-only consumption for CI. No package-mode pin alignment or release-tag work is proposed as a prerequisite for this local Debug/source qualification.

Current selected and direct catalog files have identical SHA-256 **`140e161c0ac14183be0cca8babce5cc896355174e1c621979b92d49826bdadb2`**, with no XML imports. The old selected catalog hash is `553796eef36f2789f60bb47bc8902b5fb9f1df5b3a942cde7f0523768bf1d2a1`; its only change to current selection is `HexalithEventStoreVersion` `3.127.0 → 3.128.0`. The accepted tool's embedded catalog hash is `f10372ba382108f8c35b3ee922c76fe8608dbec336c399278709345ae255078d`. Equal selected/direct bytes do not erase the revision warning or qualify the whole tuple. There is no reason to move the direct Builds pin merely to silence it.

## Decisive API evidence and candidate provenance

Current `PlatformAppModel.cs:407–418` references:

- `CompositionRunPlan.ResourceClaimStateDirectory`;
- `CompositionResourceClaimStore`, including `Acquire` and `ClaimedPorts`;
- `CompositionResourceClaimConflictException`;
- `CompositionPortAllocator.Allocate(int, bool, IReadOnlySet<int>?)`, named argument `excludedPorts`.

Inspection of the accepted package's actual Tooling DLL finds the first two types/property absent (three inspection commands exit 1 with explicit not-found diagnostics), and only the two-argument `Allocate` overload (exit 0). Source inspection independently agrees. DLL SHA-256: `a923a77ea4488d1b7162134ed89d951dfebd2398ccae92f74d0a087df336fd9a`; accepted package SHA-256: `637bebd547f45b5f2012c2f43af79b9b01772f1e81af02809babe62f4aa7cf1d`. This establishes a static compile incompatibility; no compiler failure or runtime result is claimed.

The resource-claim APIs first appear at Builds `db5abf96d730d82fd6c1ccfa20ffd9c2262b9838`; the needed overload appears at `ba5e371472c832261ae2c3f6f99c1928b6cac780`. Local release-tag inspection shows 4.36.0 lacks the claim APIs and 4.37.0 lacks the overload. **4.38.0 is the first locally identified tagged candidate containing all four**, confirmed against its downloaded package by four successful metadata inspections. This removes the identified API gap, not every possible compatibility gap.

The official [4.38.0 release](https://github.com/Hexalith/Hexalith.Builds/releases/tag/v4.38.0) and [NuGet package](https://www.nuget.org/packages/Hexalith.Builds.Module.Cli/4.38.0) identify the proposed candidate. Release publication was observed at `2026-10-25T13:51:06Z`; it is neither draft nor prerelease. Remote tag commit and package nuspec repository commit both equal `e171c5a788eab183b32683431b084198c9b8d373`. The signed NuGet archive is 37,220,356 bytes, SHA-256 **`f352d3e0f03f21dbfe42c89adb7a8765cb073a2351281acaa3cf304235426ab1`**. `dotnet nuget verify /tmp/hexalith.builds.module.cli.4.38.0.nupkg --all` exits 0. It is a NuGet repository signature, not a claimed maintainer signature. The release inventory's presigning package hash differs; those bytes are not equated to the signed NuGet archive.

Candidate CLI DLL SHA-256: `6798dce33fc2531079f312f8fda542df77dedcde8d30341cd4493f381acd1f54`. Candidate Tooling DLL SHA-256: `0266257a74c1dbc49407c61b07d90f7c95b806d937ea4f338b98bf8d128256fd`, equal in the CLI and packaged `g4-host`. The candidate was downloaded to `/tmp` and inspected only; it was not installed, selected as the accepted tool, or invoked as a CLI.

## Smallest supported qualification path

No operational patch accompanies this proposal because **no repository, dependency gitlink, tool manifest or global tool installation needs changing for the proposed qualification**. The exact change is confined to the qualification invocation: reference the isolated candidate DLL instead of the accepted 4.35.2 DLL. The plan records both identities and the complete proposed argv. Do not edit historical accepted-tool receipts or add an unsolicited tool manifest.

After the focused authorization, recheck the captured tuple, selected SDK/MSBuild and package hashes, extract the candidate into a new private directory, and export the exact selected catalog from `022668ac2dd3b0ec4b1b8e99b243debef0eee374`. If the temporary package is gone, reacquire only the recorded URL and require the same bytes/hash/signature before extraction. Run only the changed-input Debug composition-model build based on the candidate's `BuildModelAsync`: current Platform source, explicit candidate Tooling DLL, selected catalog overrides, and isolated output/cache directories. The plan adds explicit global source/root properties because Platform's local source-reference default does not propagate to EventStore's independently evaluated defaults. These are qualification overrides, not a claim that the ordinary helper already passes them. An exact property/item evaluation and resulting assets must demonstrate the intended source graph. Retain actual invocation, evaluated identities, build log and output hashes. The build has a 600-second process-group deadline, with 10 seconds before forced termination. This check is needed because historical composition builds loaded newer source-built tooling, not this released package.

Then compare the model's direct Tooling/Aspire references with actual candidate `g4-host` DLL identities, and record the model's EventStore Aspire/ServiceDefaults identities and assets. Missing or mismatched required direct identities fail this bounded check. Do not enter AppHost startup, allocate a run plan or supply a synthetic ordinary mapping. This metadata check does not establish transitive runtime loader compatibility; actual loading remains a gate in the later authorized ordinary attempt. Candidate and selected catalog agree on Aspire `13.6.3`, Dapr CLI `1.18.2`, runtime `1.18.7`; candidate embedded EventStore version `3.127.0` differs from selected `3.128.0`. Preserve that difference as unresolved unless its relevance is established; do not demand an invented equality or infer full catalog compatibility. If a build/reference failure requires source or dependency edits, stop and prepare a separate exact patch with full revisions, affected paths, provenance, risks and rollback. Do not repair it under this decision.

Risks: new released tooling changes run ownership, cleanup and test orchestration; catalog selection, model dependencies and packaged host may still disagree despite the four APIs existing. Source-built historical passes do not qualify this package. No runtime executes in the proposed lane. Rollback is abandonment of the candidate lane and retention of its evidence; the accepted 4.35.2 identity, repo checkouts and indices never change. Remove only newly generated private outputs after checking for unexpected content; do not clean source trees or shared caches. A later permanent tool/dependency change must be proposed separately if any configuration file needs editing.

## Prerequisites for a later ordinary public CLI attempt

1. Complete candidate model/tool/catalog qualification and obtain explicit disposition of that exact tuple, plus separate ordinary-runtime authority. Reconfirm revisions, dirty bytes, stage-zero entries, package/DLL hashes, declared tool range and expected source warnings immediately before the attempt. Preserve all existing work; do not auto-initialize missing direct references or any nested reference.
2. Use the outer Folders root and the **direct** Parties manifest. All selected owner checkouts must physically remain within that root and match their root gitlinks; all declared root references must have one stage-zero gitlink. Platform and Builds identity findings have their special source-warning handling. Root `.gitmodules` uses `update = none`, but the initializer can override it with `--checkout`; therefore inventory all roots before execution.
3. Require genuine command catalog materialization/evaluation, tool-supported declarations, four-owner closure and registered server-project validation. Reuse unchanged declaration validation now; rerun only when its inputs change. Do not add policy-driven dependency edges to clear unrelated adoption diagnostics. Folders/Projects are still unenrolled; a Parties-only attempt cannot demonstrate their readiness or remove their owner/registration holds.
4. Confirm Platform's .NET SDK `10.0.402` policy; required source restore/build inputs; a functioning Docker server; Aspire CLI equal to the candidate's `13.6.3`; and a configured absolute `HEXALITH_DAPR_HOME` with Dapr CLI `1.18.2`, runtime `1.18.7`, and verified `daprd`, `placement`, `scheduler`. The captured global tool inventory lists Aspire `13.5.3`, so environment compatibility is not established. Runtime prerequisite installation is not authorized here. Dapr control-plane verification can briefly start processes: leave that probe for the explicitly authorized runtime lane.
5. Supply the selected local profile/required secrets through supported mechanisms, required image/feed access and declared preflight checks. Establish run-owned state/output/port ownership and cleanup/`down` handling. Capture readiness and later human SM-2 separately; do not promote an emitted model or started process to acceptance.
6. Once all preceding gates and runtime authority hold, the public invocation is the plan's **unexecuted** `dotnet <qualified-tool-directory>/tools/net10.0/any/Hexalith.Builds.Module.Cli.dll run --manifest /home/administrator/projects/hexalith/folders/references/Hexalith.Parties/module/hexalith.module-manifest.v2.json --mode source --output json`, from the Folders root. There is no public mapping-only switch. Capture the actual `<run workspace>/source-mapping/mapping.json`, its `ContentHash`, exact command, selected paths/identity, diagnostics, outcome and run provenance. Also retain contemporaneous owner gitlinks/HEADs, dirty bytes and declaration/project hashes: mapping `ContentHash` alone does not bind owner source bytes. On refusal, retain the real failure and only artifacts actually produced.

`PlatformHostedPublishing.ReferenceOptions(PlatformRoot)` creates a different fixture mapping under **PlatformRoot/references**, with `HasGit=false`. Hosted graph/chart tests and retained supplied-export passes do not exercise the ordinary outer-root resolver. Neither the new direct Parties checkout nor this qualification proposal changes that fixture contract. A log named `platform-debug-ordinary-graph` is a hosted test against a filesystem layout, not public CLI mapping evidence.

## Reused evidence and preserved holds

Owner-intake's 331 and composition's 2,107 archived file members and receipt/handoff/verifier bindings match their retained hashes. Prior normal/optimized replay outcomes and 42/31 verifier-test results are reused without rerunning them. All four current direct declarations match the composition capsule's `exports-current-owner` lane, so its released CLI joint validation remains reusable declaration evidence: exit 0, log SHA-256 `724d6ace8bd53e2658aa456dc61b065be48a3fe24e71ebb7c93b76c056cd0d53`. Do not substitute the earlier failed four-owner attempt or older pinned Memories export. The broader descendant scan performed during review found no nested Git markers or directory symlinks in any direct checkout. Preservation proves existing tracked/untracked bytes and indices; it does not retrospectively attest ignored build-output bytes. The proposed qualification adds before/after ignored-file and new-file coverage.

Historical builds and graph passes remain bounded to their actual source/tool/profile tuples. No build, graph test, adoption audit, ordinary runtime or capsule verifier was repeated in this preparation. The current ordinary mapping and `ContentHash` remain **null**. No current tuple, package or hosted acceptance is recorded.

The EventStore/Tenants/Memories dependency-policy conflict remains separate: EventStore `[]`, Tenants `[eventstore]`, Memories `[eventstore]`; reciprocal edges would introduce cycles. No dependency, owner declaration, hosting accountability, consumer inventory, registration or retirement decision is invented. Prior adoption/hosting holds remain open.

Parent **in-progress**, tracker **backlog**, **V1Only**, unset **T0**, null **activation authority**, and **0/14 staging checks executed** remain unchanged. Publication, deployment and tracker-transition authority remain false.
