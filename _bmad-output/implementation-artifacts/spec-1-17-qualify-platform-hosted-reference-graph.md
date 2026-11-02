---
title: '1.17 Qualify the Platform hosted reference graph at direct revisions'
type: 'bugfix'
created: '2026-11-02'
baseline_commit: 'fae4ba02523bb26c18d4439cb67677366493f261'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
tracker_policy: 'no-automatic-transition'
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
---

<frozen-after-approval reason="Platform owner approval binds this exact four-owner tuple and this limited qualification scope">

## Intent

**Problem:** Hosted graph code reads uninitialized nested references; Debug build defaults select different sibling revisions. The retained strict Release composition build has 258 CA1062/CA2007 errors, and the diagnostic hosted graph test fails on the absent nested Parties declaration.

**Approach:** Bind the four Platform direct revisions below as the approved executable source tuple. Export and verify them outside nested submodules; use the tuple for build and hosted graph inputs. Repair strict build errors, run canonical hosted tests, and record results.

## Boundaries & Constraints

**Always:** Bind Platform `3d344e850471b6a21fc4649df7ede9a0390689b1` to this exact owner tuple (revision; declaration SHA-256):

| Owner | Revision | Declaration SHA-256 |
|-------|----------|--------------------|
| EventStore | `7c93a1234d14c66f5ee85ed48caaee1d30a671f3` | `21eda6da824d5ae6135ff342dd8a5aae950b02420319b953a836dddeb00ce495` |
| Tenants | `a01e89854f711d1a93ae15695b438424c1ed4f0f` | `1588f3428ec29296c452fdd35bf95146142315aad3ad8ceb934e7a791f1fb522` |
| Memories | `e8956eb5c1bd49297f86702f9606eb9b013bc2f1` | `56f6dc8134d3f7cea06910e923a4736a450e00241e8e4a565ac9226897881840` |
| Parties | `f4285584dce0dfa08e3336fca3c203a1b5e748c6` | `86002eeb916c77bd753a3c618ce394125794baf421315604c9f551b762624504` |

Recheck gitlinks and source bytes. Preserve V1Only, unset T0, null activation authority, tracker backlog, and historical approvals.

**Never:** Substitute sibling revisions, initialize nested submodules, disable/suppress analyzers or warnings-as-errors, change pins, publish, deploy, activate, stage, commit, push, or claim release/operational acceptance.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Exact mapping | Four verified direct revisions | Build and hosted graph use those roots | Record identities |
| Drift | Missing/mismatched revision, digest or gitlink | No graph | Refuse before resource addition |
| Sibling | Equal manifest, different revision | No implicit selection | Refuse |
| Hosted probe | Publish-mode model test | Chart graph and native resources | Run/deploy/mixed selection refused |

</frozen-after-approval>

## Code Map

- `references/Hexalith.Platform/Directory.Build.props`, `tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj` -- source roots and test metadata.
- `references/Hexalith.Platform/src/Hexalith.Platform.Composition/PlatformHostedPublishing.cs` -- nested roots in `ReferenceOptions` and `ComposeNativeProbe`; reuse declaration closure.
- `references/Hexalith.Platform/tests/Hexalith.Platform.Composition.Tests/PlatformHostedPublishingTests.cs` -- canonical 13-case hosted class.
- `references/Hexalith.Platform/src/Hexalith.Platform.{Composition,Protection}/` -- retained 258 CA1062/CA2007 errors in 69 files; fix source, retain analyzer rules.
- `evidence/story-1-17-platform-reference-mapping-proposal-2026-11-02.json` and `evidence/story-1-17-current-tuple-local-2026-11-02/` -- prior proposal and failures.

## Tasks & Acceptance

**Execution:**
- [x] `references/Hexalith.Platform/eng/story-1-17-reference-mapping.json` and `docs/implementation/story-1-17-reference-mapping-approval-2026-11-02.md` -- bind tuple and owner approval; verify exact exports before use.
- [x] `references/Hexalith.Platform/src/Hexalith.Platform.Protection/` and `src/Hexalith.Platform.Composition/PlatformRoutingCatalogMetadata.cs` -- repair each error in the retained strict build log without suppression.
- [x] `references/Hexalith.Platform/src/Hexalith.Platform.Composition/PlatformHostedPublishing.cs` and `tests/Hexalith.Platform.Composition.Tests/PlatformHostedPublishingTests.cs` -- use verified mapping; test drift refusal.
- [x] `_bmad-output/implementation-artifacts/evidence/story-1-17-platform-hosted-graph-qualification-2026-11-02.json` -- record exact commands, identities, results, blockers and controls.

**Acceptance Criteria:**
- Given approved roots, when verified, then each revision and declaration digest matches before qualification.
- Given mapping drift, when composing, then no graph or resources are produced.
- Given strict Release settings, when built, then analyzers remain enabled and the composition project has zero warnings/errors.
- Given verified roots, when canonical hosted tests run, then 13 cases pass with no skips, failures or unrun cases, and the graph matches the chart.
- Given any outcome, when recorded, then local evidence stays distinct from release and operational acceptance.

## Implementation Notes

All direct commits are in owner Git stores. The November 2 receipt reports 258 strict Release errors and 0/1 hosted graph from a diagnostic build. Platform owner approval binds only this local tuple.

2026-11-02: The user answered "approve and continue" to the explicit checkpoint stating that approval binds the exact Platform direct tuple for local qualification only. Implementation is authorized; publication, deployment, and operational acceptance remain excluded.

2026-11-02: Platform HEAD and all direct gitlinks matched the approved tuple. Four exact Git checkouts were exported outside nested submodules and verified against Git blobs and declaration hashes. Strict Release composition and final exact-root Debug source builds both passed with zero warnings/errors. The canonical hosted class passed 16/16 with no failures/skips/unrun cases: the original 13 plus environment-selection, dirty-source, and mapping-digest checks. GraphExport built and executed locally with explicit approved roots. Missing-root and same-manifest/different-revision refusal ran in the hosted class. The separate strict Protection.Tests build stopped on 186 analyzer errors in test/shared fixture sources before tests ran. Exact commands, hashes, source diff, logs and remaining handoffs are in [the additive qualification receipt](evidence/story-1-17-platform-hosted-graph-qualification-2026-11-02.json). The Folders root HEAD advanced externally during this run; the Platform HEAD and approved four source revisions stayed stable. No tracker, pin or historical approval was changed.

2026-11-02 push continuation: The reviewed Platform source was captured in candidate commit `d891af161e9eb6eabc535e0ba50f910d0b323ca8`, whose direct parent is the approved baseline. The committed candidate passed the same strict Release and exact-source Debug composition builds with zero warnings/errors; all 16 hosted tests passed, and the local graph export matched the chart. [The committed-candidate addendum](evidence/story-1-17-committed-candidate-2026-11-02.json) records those commands and hashes separately from the earlier working-tree qualification. The four direct owner revisions and tracker controls remain unchanged. Integration onto newer Platform main requires a fresh mapping decision and qualification.

## Spec Change Log

## Review Triage Log

| Layer / finding | Verdict | Evidence and route |
| --- | --- | --- |
| Blind 1: ignored logs | medium | Root `.gitignore` ignored the six receipt logs; the byte-bound packet would be incomplete. Patched with a scoped exception. |
| Blind 2: other submodule diffs absent | low | EventStore and Tenants worktree changes belong to concurrent Story 12.2 work and were not selected as Story 1.17 source inputs. Reject for this packet; those changes need their own review. |
| Blind 3: GraphExport command fails | medium | The documented command omitted newly required roots and failed before export. Patched instructions and executed export successfully. |
| Blind 4: tests fail without root variables | false | Explicit owner roots are a required qualification input; the recorded canonical command supplies all four. A fallback would silently select sibling revisions. |
| Blind 5: sibling checkout fixture | medium | The old negative test required a particular umbrella layout. Patched with an isolated clone and empty commit retaining identical declaration bytes. |
| Blind 6: Tenants source in package CI | medium | MSBuild includes a concurrent Folders AppHost.Tests Tenants reference under package mode. Defer to the separate Folders change owner; Story 1.17 did not create it. |
| Blind 7: mutable Platform working diff | medium | HEAD is pinned but local Platform working bytes can change after this approval. The receipt hashes this candidate and requires new qualification for subsequent edits; defer immutable candidate binding to its release handoff. |
| Blind 8: staged gitlink drift | medium | Checking only `HEAD:path` missed an index change. Patched verifier to compare the current index gitlink too. |
| Blind 9: ignored executable files | medium | `--exclude-standard` omitted ignored source candidates. Patched verifier to reject ignored source inputs outside generated build output. |
| Blind 10: checkout line endings | low | Mapping and receipt hashes are byte bound; default text conversion could alter them. Patched scoped `.gitattributes` rules. |
| Blind 11: drift test coverage | medium | Existing tests proved missing roots and alternate revision, but not changed mapping bytes. Added a digest refusal test; index gitlink check is directly verified by the implementation and exact gitlink inventory. |
| Blind 12: source changes after verification | low | The path remains mutable, but local qualification uses dedicated clean checkouts and resolves immediately; a concurrent mutator would also invalidate the evidence snapshot. Reject an immutable snapshot subsystem as disproportionate for this local probe. |
| Edge 1: Platform working diff | medium | Same mutable candidate limitation as Blind 7; defer to immutable release handoff. |
| Edge 2: ignored source | medium | Same reachable gap as Blind 9; patched ignored executable-source check. |
| Edge 3: verification race | low | Same local concurrent mutation condition as Blind 12; reject an immutable snapshot subsystem for this probe. |
| Edge 4: Folders catalog load | false | The finding names no owner dependency that fails to load; the focused catalog model test passes with current artifacts. No demonstrated bad outcome in this change. |
| Edge 5: sibling fixture | medium | Same standalone checkout failure as Blind 5; patched fixture. |
| Verification 1: environment overload untested | medium | Hosted tests used the dictionary overload while GraphExport used the environment overload. Added a direct hosted test and ran GraphExport. |
| Verification 2: dirty source untested | medium | No test modified source at approved HEAD. Added a cloned dirty checkout refusal test before graph creation. |
| Verification other 1: README export | medium | Same observed command failure as Blind 3; patched and ran export. |
| Verification other 2: package CI source | medium | Same concurrent Folders MSBuild issue as Blind 6; defer to that change owner. |

## Design Notes

Export paths are session inputs; revisions and digests are authority. Verify archives against commits. Ordinary hosted selection must refuse unverified mapping.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj --configuration Release -p:UseHexalithProjectReferences=false -m:1 -warnaserror` -- strict Release composition build succeeds with analyzers enabled.
- `dotnet build tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -m:1 -warnaserror` with four verified root properties -- exact source graph builds without sibling fallback.
- `dotnet tests/Hexalith.Platform.Composition.Tests/bin/Release/net10.0/Hexalith.Platform.Composition.Tests.dll -class Hexalith.Platform.Composition.Tests.PlatformHostedPublishingTests` -- all 13 hosted cases pass; record actual count.
- `git diff --check` in each changed owner repository; verify root and nested gitlinks, tracker and historical bytes are unchanged.
