# Story 1.17: current G-6 catalog acquisition blocker

Story 1.17 remains open. The existing Projects CI run finished with a **G-6 prerequisite failure**, while its product build-and-test and generated-artifact jobs passed. The next action is a CiCd-owned source-preparation correction, followed by fresh exact-source qualification. The retained immutable host artifacts and Platform enrollment remain separate pending owner deliverables.

This packet updates only the CI status and diagnosis in the [post-landing handoff](../story-1-17-post-landing-2026-11-16/README.md). Historical approvals, published packages, local tests, operational requirements and recorded controls retain their original scope.

## Observed CI

The five [original API responses](observations/) were read separately at the UTC times and bounded URLs in [queries.json](queries.json). That acquisition record also identifies the archive download and bounded [initializer job-log excerpt](initializer-job-log.txt) retained during review. Jobs and artifacts are first-page observations with `per_page=100`; the recorded total counts fit those pages. No workflow was dispatched or retried.

| Subject | Exact source | Observed result |
| --- | --- | --- |
| [Projects CI 43881420098](https://github.com/Hexalith/Hexalith.Projects/actions/runs/43881420098), attempt 1 | `981ceef13a1abc88219925e73c51a23eb9ac50c2` | Completed, failure. Build-and-test, workflow policy, P1R replay and generated-artifact jobs succeeded. G-6 failed. Performance, Aspire and scheduled managed AppHost E2E jobs were skipped. |
| [G-6 job 133360595951](https://github.com/Hexalith/Hexalith.Projects/actions/runs/43881420098/job/133360595951) | Same Projects source and attempt | `Restore effective G-6 package graphs` failed. The runtime qualification step did not execute. |
| [Folders CI 43881492421](https://github.com/Hexalith/Hexalith.Folders/actions/runs/43881492421), attempt 1 | `dafa51b0866cc19c18d4448300b7931dad9158f0` | Completed, success. Build-and-test, specialized, accessibility and UI E2E jobs succeeded. Performance and Aspire jobs were skipped. This source precedes the local documentation-only successor; it is not current host-image or Story 1.17 rollout acceptance. |

## Failure and reproduction

[Artifact 14234531847](https://github.com/Hexalith/Hexalith.Projects/actions/runs/43881420098/artifacts/14234531847), retained as [g6-source-mapping.zip](g6-source-mapping.zip), matches GitHub's archive SHA-256 `24d169b2973bd1d16625ca031b96ae0bcfdfd231056758e55dfc89b45ea8a678`. Its `source-mapping/conversations.json` reports `HXW009`, phase `Prerequisite`, category `PrerequisiteUnavailable`: the selected Builds catalog at `e2bbbd0893f586e4d447ef8358ff747ea3d34435` cannot be read. The archive also records a successful zero-warning, zero-error mapper build and empty stderr logs. The mapper returned its failure as JSON on stdout; the CI wrapper printed only stderr, so the useful diagnostic appears in the artifact.

[Source bindings](source-bindings.json) distinguish the reusable initializer revision `6ad5854aa14a9950e1fbfc32d7e27953a1c4b82b`, Projects' checked-out Builds `fdda17c0a4538b0f7c99f813c20ca24b49006530`, and Platform's selected catalog `e2bbbd0893f586e4d447ef8358ff747ea3d34435`. The initializer requests depth-one root submodules. `CatalogVersionMaterializer.MaterializeFileAsync` reads catalog files using `git show <pin>:<path>` without network acquisition.

The [probe](probe.py) acquired only the checked-out Builds revision in a private bare repository under `$TMPDIR`. Git's batch object check explicitly reported the catalog commit missing; reading the selected catalog then failed with exit 128 and retained stderr. Fetching only the exact catalog commit into that disposable repository made the same read pass with exit 0, preserving Builds HEAD. The catalog bytes hash to `d098dab50977041a536508f6df7e934f95678a4a0abedd80b28e9bae19ee0637`. [probe-result.json](probe-result.json) records that result. This reproduces a sufficient Git-object cause consistent with the retained CI diagnostic; it does not inspect the vanished runner object database or execute the mapper/runtime qualifier after acquisition.

## Concrete owner action

**Projects CI maintainer with CiCd and Builds:** deliver a Projects G-6 preparation step through the CiCd-owned lane, before Conversations `prepare-build`. Acquire Platform's exact selected catalog commit into the outer workspace's root-declared Builds object database. Derive the commit from the reviewed direct Platform revision, confirm the approved Builds origin, verify the catalog object after acquisition, and preserve checked-out Builds HEAD and all gitlinks. The existing Projects P1R job's explicit EventStore-history acquisition is a caller-specific precedent.

For this recorded tuple, the narrow proposed acquisition is:

```bash
git -C references/Hexalith.Builds fetch --no-recurse-submodules --no-tags --depth=1 origin e2bbbd0893f586e4d447ef8358ff747ea3d34435
git -C references/Hexalith.Builds cat-file -e e2bbbd0893f586e4d447ef8358ff747ea3d34435:Props/Directory.Packages.props
```

These are owner-lane remediation commands, not commands run against this workspace. A delivered implementation must derive and validate the selected full commit rather than freeze this historical example. No nested initialization, dependency-pin change, global unshallowing or silent mapper fetch is needed for this object prerequisite. The technical module should continue refusing unavailable catalog bytes. The CI wrapper should also surface bounded rule/category diagnostics from a failed JSON result while retaining the original artifact; use existing safe-diagnostic conventions rather than echoing arbitrary result contents.

After the owner correction lands, obtain passing exact-source CI and complete current G-6 and required managed AppHost E2E. Acquisition alone qualifies none of those gates; subsequent failures remain blocking. Then supply the reviewed SDK host-production lane, immutable retention destination, target platform/RID, base-image digest and exact configuration/manifests; complete actual Folders/Projects Platform source/image bindings, authentication/state, separate smoke and critical-flow E2E enrollment. Reuse all other operational gates and conditional target/data branches from the previous handoff.

The [Hexalith baseline](../../../../references/Hexalith.AI.Tools/hexalith-llm-instructions.md) assigns delivered workflows to CiCd and says to report pipeline defects with their failed run. This packet is that local report and proposed correction. No remote issue, PR or message was sent, and no delivered workflow was edited.

## Verification and limits

From the Folders root:

```bash
run-with-temp python3 -B -O _bmad-output/implementation-artifacts/evidence/story-1-17-g6-catalog-diagnosis-2026-11-16/verify.py
run-with-temp python3 -B -O _bmad-output/implementation-artifacts/evidence/story-1-17-g6-catalog-diagnosis-2026-11-16/probe.py
```

The first command rehashes this dated packet, validates the reported original CI/archive/probe subjects and source selection chain, and checks protected files, repository revisions and Git indices against [before.json](before.json). The separate [tracked-status observation](tracked-status.json), captured after creating this additive packet, records clean tracked worktrees; it is not a retroactive hash of every initial file. Verification is offline. It deliberately fails its workspace comparison after legitimate source/index changes. Use `verify.py --packet-only` to verify the retained dated packet on a fresh checkout or after such changes; that mode makes no current-workspace or source-object comparison. The second command repeats only the disposable local Git-object probe and compares its result with the retained result; no network or workspace fetch is performed. Neither command runs application tests, grants hosted acceptance or supplies staging/deployment evidence.

Publication, audit, A8 and Story 11.6 are reused. Recorded controls remain `V1Only`, null migration T0 and activation authority, zero of fourteen staging checks executed, parent `in-progress`, tracker `backlog`. These are preserved repository records, not a live-control observation. No tracking transition, workspace Git mutation, deployment or activation was performed. Immutable host artifacts, current Platform enrollment/tool acceptance, locked target/data disposition, staging/recovery, hosted consumer/schedule/delegation evidence and closure decision remain pending.

Independent review identified seven improvements to replay portability, source selection, probe comparison, failure specificity, claim coverage, tracked-worktree preservation and acquisition metadata. All seven were addressed, with no deferred finding. [Review validation](review-validation.json) confirms that optimized packet-only replay passes in a disposable copy without the original repository and rejects an inconsistent result even when its hash manifest is recalculated. Full local replay and a fresh probe comparison also pass.
