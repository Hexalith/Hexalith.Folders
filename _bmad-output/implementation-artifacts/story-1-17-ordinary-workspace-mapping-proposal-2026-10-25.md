# Story 1.17 — ordinary Platform workspace mapping decision

**Decision requested from Jerome: approve the exact direct Parties addition below for a later, bounded application. Status: proposed; nothing applied; acceptance remains null.**

Add `references/Hexalith.Parties` to the Folders root `.gitmodules` and index as a mode-160000 gitlink at `986a369ec26f6c5194593eb7bce12955eda54149`, then materialize only that direct checkout at the same revision. This is the smallest change that makes the qualified Parties owner source available to ordinary source selection in this workspace. It does not require advancing an existing dependency, changing a manifest, or changing Platform code.

The [exact proposed patch](story-1-17-ordinary-workspace-mapping-proposal-2026-10-25.patch) affects only `.gitmodules` and `references/Hexalith.Parties`. Patch SHA-256: `1df81ae96d9fbb0e5927e12d68ca41c8e82afd26539defa9c9de8d58feb9ee9e`. The [inspection and evidence record](evidence/story-1-17-ordinary-workspace-mapping-proposal-2026-10-25.json) retains full commands, outcomes, revisions, source hashes, prior receipt bindings and reused verifier results.

## What approval would authorize

One clear approval would authorize a follow-up application of this exact patch, including **staging only its `.gitmodules` delta and new Parties gitlink**, and initializing/checking out **only the newly declared direct Parties submodule** at the specified revision. Stage zero is necessary because the ordinary resolver reads the Git index. A declaration-only edit without its gitlink and physical checkout cannot deliver the requested mapping.

The conditional sequence is: reconfirm this tuple and existing edits; apply the two-path addition; materialize the exact direct checkout; verify its HEAD, declaration/project bytes, physical containment and absence of initialized nested submodules; retain the resulting source-selection evidence. Stop dependent work if a check fails. Do not advance another pin or substitute another Parties revision to make the check pass.

This decision authorizes source intake and local inspection only. It supplies no commit/push/publication/deployment authority, no permission to stage existing edits, no ordinary runtime launch, no registration or adoption acceptance, no acceptance of the current tool/catalog/composition tuple, no hosted-test layout change, and no nested initialization. A later ordinary `run`/`debug` attempt requires its remaining prerequisites and authority; there is no public mapping-only CLI switch. Existing declaration approval is preserved in its original scope and is not used as permission for this new dependency tuple.

**Focused decision:** approve this two-path addition and its narrowly scoped index/direct-checkout materialization, or retain HOLD. No declaration reapproval or new ownership attestation is requested.

## Current baseline and unchanged dependencies

The final proposal baseline is Folders `09707e19ab7024b88b34d89651e15a45bc895eeb` on `main`. During preparation, external Git operations advanced the root from `7c2c3c6acba97db4f1d3c42a304b16d70b0850c0` through `05cc3af566817f9e22f9e19a2abb2393ac50eee6`. The latter was the investigation snapshot. The final external commit, titled `build: sync local changes via /pushall`, also recorded the draft versions of this packet's four artifacts. This task issued no staging, commit or push commands; subsequent review corrections remain local. External commits do not constitute Jerome's acceptance of the proposal.

The following are the final root stage-zero gitlinks, preserved by the proposed patch. Ten checkout HEADs agree. **Platform's checkout is `9869094acc21f9054960a573f884c60ddf3235cd`, ahead of its recorded `30673fdab03b5c5d527eb9eb596bfa6645ad6649` gitlink.** This mismatch is retained explicitly; this proposal authorizes neither moving the checkout nor advancing that pin.

| Root path | Full revision retained by the proposal |
| --- | --- |
| `references/Hexalith.AI.Tools` | `3f194e17174994d308ec84af9ee2b5aa68674d0d` |
| `references/Hexalith.Builds` | `74932ad0247203797274aba8b7b6366a4bbf92b9` |
| `references/Hexalith.Commons` | `1663028fb6d9f46a0bf17919edeb21bb99be1587` |
| `references/Hexalith.EventStore` | `c5d1c280d8c1c1a207cd159d817f614e93c93130` |
| `references/Hexalith.FrontComposer` | `cfa9febc2942666265f23f87b26112c846f625ad` |
| `references/Hexalith.McpCli` | `dc282889e624a4c990faa3f005ad7eaa8db7a473` |
| `references/Hexalith.Memories` | `7b0c8b5692600e293346eea7a84652eb87e4c346` |
| `references/Hexalith.Platform` | `30673fdab03b5c5d527eb9eb596bfa6645ad6649` |
| `references/Hexalith.PolymorphicSerializations` | `221ac4592ba908ae7661ba1fa9e2d1fcc23baee0` |
| `references/Hexalith.Projects` | `298f6f6a41b9ce7b67f14c21c724d76832b957a9` |
| `references/Hexalith.Tenants` | `24fb584b8d2b8a3bc7a51fcf8b5a6266e21e53e7` |
| **Proposed new** `references/Hexalith.Parties` | **`986a369ec26f6c5194593eb7bce12955eda54149`** |

At investigation start, Platform had eleven modified composition-test files and Projects had its modified `src/Hexalith.Projects.Client/Hexalith.Projects.Client.csproj`. Their bytes remain preserved, although external commits have recorded those edits. The initial comparison with owner-intake found 48 changed prior Platform files and 5 Projects files; further external changes are enumerated separately in `finalPreservation`. The strict whole-workspace preservation check correctly failed when Tenants advanced. The final report distinguishes unchanged original user-edit bytes from externally changed revisions, indices and tracked sources; it does not claim an unchanged workspace or current qualification.

The **recorded Platform identity** `30673fdab03b5c5d527eb9eb596bfa6645ad6649` pins Builds `fd4b1a54ae3da4e1dde4063058b747f4156137df`, replacing the packet's `0c735503728e18de93937b1495abebcfa6e33f9a`. The newer **checked-out Platform** `9869094acc21f9054960a573f884c60ddf3235cd` pins Builds `022668ac2dd3b0ec4b1b8e99b243debef0eee374`; this does not replace the committed identity's catalog selection. Both retain Parties `986a369ec26f6c5194593eb7bce12955eda54149` and Tenants `60f9dfd28e7b134785cb00a30bb8acb8889e4b2e`, while ordinary source selection uses Folders' direct Tenants revision in the table. All Platform and Projects gitlinks, including nested ones that will stay uninitialized, are recorded in the evidence.

The proposed Parties revision itself pins Builds `1783c0a377615063f3222c1d28f3ab513af4e72f`, EventStore `694e37bad3e667931fbafc6a1ed9f85e4a0b4912`, Tenants `14133e96d2c5efb13270580f920133079707398b`, and Platform `1b7a00381794bb7ecf40caf643c63c50758a81eb`. Those nested pins are provenance, not instructions to initialize them or replace the root selections. Intake therefore creates a new combination that still needs technical qualification.

## Exact patch and source provenance

The patch appends:

```gitconfig
[submodule "Hexalith.Parties"]
	path = references/Hexalith.Parties
	url = https://github.com/Hexalith/Hexalith.Parties.git
	update = none
```

It adds a mode-160000 gitlink whose complete content is `Subproject commit 986a369ec26f6c5194593eb7bce12955eda54149`. There is no change to any existing gitlink, `.slnx`, build/package selector, runtime dependency, Platform reference directory, or manifest. `update = none` follows the existing domain-reference policy; it is **not** a guarantee against CLI initialization, because the initializer explicitly uses `--checkout`.

Owner provenance is bounded:

- Folders `docs/governance/approval-policy.md` names Jerome as the sole decision maker; SHA-256 `156929f0695d364d515dc71adfdf3292903b4f08538e723f6015633655ebd8a4`.
- Platform `.github/CODEOWNERS` identifies `@jpiquot`; SHA-256 `2b338e90f00d9d29582065320a7a7d38bd68e7b33598516d894be9d966eefc21`. This identifies the code owner, not acceptance of this proposal.
- Composition capsule `provenance/approved-integration.json` retains the actual owner approval and integration of Parties `45c033ced0cbdf3edfec12bba427361e2abe6b95`; `provenance/upstream-integration.json` retains the earlier `35d891d202a87fd4f494f2cadd26c277df44b84a` integration. Both are ancestors of the proposed pin, freshly confirmed with `git merge-base --is-ancestor` (exit 0).
- The exact Parties declaration at the proposed pin has Git blob `c96bb9746220da7e11c309f8a2897816b72449a9`, SHA-256 `0fd4457a1801fa13053ef8821eb16a0cfcc6a5edac94b6ce89e8f220e8a30511`, 12,797 bytes. It equals the receipt-bound owner-qualified export and both approved integrations. This establishes declaration provenance, not blanket approval of every intervening source change.
- The actual executable project `src/Hexalith.Parties/Hexalith.Parties.csproj` exists in that Git tree; SHA-256 `f73aaaaf8f48095fdbf0e137c24f02f63bccf51eea87c068048812f143f8c715`. The existing sibling Parties checkout is newer and dirty. Only immutable `git show` objects were used; its checkout is not imported, moved, symlinked or treated as the ordinary source root.

## Ordinary source-selection behavior

The resolver walks to the outermost superproject, so commands issued inside `Folders/references/Hexalith.Platform` still resolve Folders as the workspace. It reads the working root `.gitmodules` and requires one stage-zero mode-160000 entry for every declared direct reference. In source mode, dependencies map to physical direct owner roots. A manifest under the proposed direct Parties root selects Parties as the active owner. No sibling checkout or nested Platform `.gitmodules` entry expands that root mapping.

Ordinary owner checkouts such as Parties must match their stage-zero gitlinks; mismatches are rejected rather than moved. Existing Platform and Builds checkouts are handled separately: source mode may retain their identity mismatches as HXW008/HXW009 warnings. Physical path escapes and initialized nested references are rejected. Before any future ordinary invocation, inventory all direct checkout HEADs and nested markers and retain all mismatch findings; otherwise the command may initialize an absent direct reference. The present Platform mismatch remains a qualification blocker, not permission to move it. No such command was executed in this preparation.

Platform identity comes from the committed root Platform gitlink; composition selection comes from stage zero. Those two recorded revisions agree at `30673fdab03b5c5d527eb9eb596bfa6645ad6649`; the separately recorded checkout HEAD is `9869094acc21f9054960a573f884c60ddf3235cd`. Catalog selection still uses the committed Platform identity's Builds pin, `fd4b1a54ae3da4e1dde4063058b747f4156137df`, read from the direct Builds object database. The object is available. Its `Props/Directory.Packages.props` hash is `553796eef36f2789f60bb47bc8902b5fb9f1df5b3a942cde7f0523768bf1d2a1`, equal to the catalog at the investigation's direct Builds `9c8f722a4bbe6c1a256d258d754bbc3ddee87257` but different from the accepted tool-source catalog hash `f10372ba382108f8c35b3ee922c76fe8608dbec336c399278709345ae255078d`. Final direct Builds `74932ad0247203797274aba8b7b6366a4bbf92b9` has catalog SHA-256 `140e161c0ac14183be0cca8babce5cc896355174e1c621979b92d49826bdadb2`, also different. Equal catalog-file bytes where observed do not erase a revision mismatch or qualify the whole tuple.

CLI 4.35.2 remains the accepted released identity: source `4248b8c70cb35ba79fdd7e4dd63049d4c73211a0`, package SHA-256 `637bebd547f45b5f2012c2f43af79b9b01772f1e81af02809babe62f4aa7cf1d`. The cached package was rehashed. Platform's tool range remains `[4.35.2, 5.0.0-0)`. The prior dirty-checkout warning stays in historical evidence; the final checkout/pin mismatch and no-local-release-tag observation need fresh command findings when execution is permitted. Source mode preserves identity warnings, while package mode has stricter gates. No warning is suppressed and this proposal updates no existing dependency to silence one.

The future public command, **documented only and not authorized for runtime execution by this decision**, is:

```text
cwd: /home/administrator/projects/hexalith/folders
dotnet /home/administrator/.nuget/packages/hexalith.builds.module.cli/4.35.2/tools/net10.0/any/Hexalith.Builds.Module.Cli.dll run --manifest references/Hexalith.Parties/module/hexalith.module-manifest.v2.json --mode source --output json
```

This corrects the retained refusal's invocation: it ran from `.../Folders/references/Hexalith.Platform` with the same relative argument, which points to **nested** Parties. Adding the root dependency does not repair that exact old invocation. An absolute direct manifest path also avoids the ambiguity. The public CLI provides no supplied-map/project override; `Platform:SourceMapping:Path` and its hash are internal transport of the command-owned map. Capture the actual `<run workspace>/source-mapping/mapping.json` (retention may call it `selected-source-mapping.json`), ContentHash, warnings and invocation provenance when an ordinary attempt is eventually authorized. The mapping ContentHash covers paths/identity metadata, not Parties' revision or source bytes: also capture contemporaneous selected-owner gitlinks, checkout HEADs, relevant dirty-file hashes and declaration/project hashes. No mapping is fabricated now.

Source locations: Builds `WorkspaceRootResolver.cs:74`, `SubmoduleInitializer.cs:24,89,166`, `PlatformIdentityResolver.cs:92,144,189`, `CatalogVersionMaterializer.cs:158`, and `SourceMapping.cs:33`; Platform `PlatformAppModel.cs:62`. The evidence binds both current source files and the released CLI source revision; raw-byte differences are retained rather than called identical. Released/current source comparison confirms the direct Parties selection and ordinary lifecycle mapping path agree. Newer standalone-root identity derivation, `list`, repeated profiles and manifest-free `down` are not attributed to CLI 4.35.2.

## Enabled checks and remaining blockers

| Check | Effect of the proposed addition after approval/materialization |
| --- | --- |
| Direct Parties gitlink/HEAD, physical path, declaration and executable project checks | Enabled. These establish availability at the named owner root, not application success. |
| Ordinary mapping and its command-produced ContentHash | Missing direct Parties input is supplied. Actual public command evidence remains pending the safe execution prerequisites; no mapping-only public command exists. |
| Four-owner declaration closure and mapped server-project validation | The Parties path prerequisite is supplied. Existing declarations and Parties/EventStore/Tenants/Memories bindings can be investigated against the new tuple. This is not proof of source/package builds or live readiness. |
| Folders and Projects enrollment | Still blocked by absent owner declarations and absent `PlatformServerBindings`, local-profile/gateway enrollment. Mapping Parties does not register Folders or Projects. Projects' separate direct mapping/dependency needs, including Conversations, also remain unresolved. |
| Current CLI/catalog/composition qualification | Still pending. Accepted CLI identity, available pinned catalog object and historical tests do not accept the new source tuple or preserved dirty edits. |
| Adoption audits and hosting accountability | Existing five failed audits/267 diagnostics and fourteen hosting candidates remain. Accountable inventories must supply real owners, consumers, frozen state and replacement/retirement plans. No owner or empty consumer set is inferred. |
| Ordinary run/debug/down, readiness and human SM-2 | Separate conditional checks after mapping, registrations, prerequisites and execution authority are satisfied. No resources started by this proposal. |
| Hosted canonical graph/chart tests | Root addition does not change their path contract. Their `ReferenceOptions(PlatformRoot)` explicitly maps four owners under **PlatformRoot/references** and sets `HasGit=false`. Retained supplied-export graph passes remain bounded historical fixture evidence. |
| Released/package qualification, publication, hosted acceptance | Remain separate and unexecuted for this proposed tuple. Prior package identities, first-install conditions and protected operation gates remain intact. All fourteen staging checks remain unexecuted. |

`PlatformHostedPublishing.cs:39` and `PlatformHostedPublishingTests.cs:15,46` define the hosted fixture contract. `PlatformServerBindings.cs:11,63` registers Parties and six EventStore/Tenants/Memories servers, and validates project containment; Folders and Projects are absent. A retained log named `platform-debug-ordinary-graph` is a hosted test against ordinary filesystem layout, **not** the ordinary public CLI.

## Separate unresolved dependency-policy decision

EventStore declares `[]`, Tenants declares `[eventstore]`, and Memories declares `[eventstore]`. The adoption policy asks for the other minimum server identities as dependencies, while composition loads core owner declarations separately and evaluates the declared runtime closure with cycle rejection. Adding EventStore→Tenants/Memories would create cycles; Memories also documents operation with EventStore without Tenants. The mapping proposal adds no runtime edges. The owner/policy decision must distinguish platform presence from actual runtime dependencies and reconcile the validator/contract without inventing dependencies to clear diagnostics. See `PlatformAdoptionValidator.cs:21,74`, `CompositionDeclarationClosure.cs:79,193`, and Memories `README.md:97`.

## Rollback and scope risks

Before later application, retain `.gitmodules` bytes, exact index entries and any pre-existing local submodule configuration. Resolve the module Git directory through `git rev-parse --git-path modules/Hexalith.Parties` as well: deinitialization can leave it behind even when the checkout/config/index entry is absent. If that Git directory, direct Parties path, index entry or local submodule config already exists, preserve its configuration and refs and stop for reconciliation; do not overwrite it or let initialization silently reuse it. Reconfirm the exact recorded/checked-out tuple above. This approval supplies no authority to restore externally advanced checkouts or advance their pins. The material risk is a new source combination: root owners supersede Parties' nested dependency pins for ordinary mapping. Qualify that combination before runtime use.

If the approved addition must be rolled back before commit, first verify that only the new Parties checkout is involved and that it has no modified/untracked work or unique local commits. If it does, preserve it and stop the destructive part. Otherwise deinitialize only that newly added direct checkout, remove only the proposed stanza and new stage-zero gitlink, and restore only local config entries created by this addition. Compare the remaining index and `.gitmodules` bytes with the captured pre-application state. Retain the Git object store and evidence; no cleanup of other references, broad reset, forced deletion, nested operation or change to the sibling Parties checkout is authorized. If subsequently committed under separate authority, prepare a reviewed inverse change instead of rewriting history.

## Evidence reused and validation performed

Both receipt-bound archives and their handoff/decision/verification bindings were rehashed. Selected member hashes were checked before reading. Prior normal/optimized replay and verifier-test results were reused without executing them again. Their exact commands, exit codes and stream hashes remain in the evidence record and original capsules. Owner-intake archive SHA-256: `504012dfebd645e604f55469d3369b2b262817479cd6532c11cb867d0516509c`; composition archive SHA-256: `d9ba51c07b2fce5725bad06e2678d287b6e7e32fc203a8230ad6511cf8e29de9`.

The historical ordinary command remains exit 3/HXW001 with two HXW011 warnings and HXW009 at its original tuple. Owner-intake's 42 verifier tests and composition's recorded builds/graph results remain historical; none was rerun or relabeled as current qualification. Relevant changed inputs are documented for a later targeted check rather than triggering broad reruns during proposal preparation.

Executed here: read-only Git/source/hash inspection and `git apply --check /home/administrator/projects/hexalith/folders/_bmad-output/implementation-artifacts/story-1-17-ordinary-workspace-mapping-proposal-2026-10-25.patch` from the Folders root, exit **0**, empty stdout/stderr. This checks patch applicability without applying it. The strict preservation command `python3 -B /tmp/story117-ordinary-map-proposal-YECHTShw/final_check.py` initially exited **1** on external Tenants drift. Its exact failure and subsequent complete drift inventory are retained; the later inventory's exit 0 means inspection completed, not unchanged-source qualification. Original user-edit bytes, parent/tracker bytes and prior receipt-bound artifacts remain preserved. No mapping or operational change was applied by this task.

Parent/tracker values stay as recorded: parent `in-progress`, tracker `backlog`. Route mode remains `V1Only`; T0 and activation authority remain null; publication/deployment/tracker authority remains false; staging checks remain **0/14**. This packet completes decision preparation only. The owner decision remains pending.
