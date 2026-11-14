# Story 12.2 — review six disposition

Shutdown qualification passes on the current source. Whole-story acceptance remains open after independent review. No feature code was changed or reverted by review.

Three reviewers returned 14 findings. Parent isolated probes reproduced coherent task rollback, terminal metadata re-attestation, valid provider/organization partition rejection and unknown-command Worker classification. The probe sources, commands, outputs and binary hashes are retained in the ignored private artifact directory.

## Corrections required

1. **Current task authority:** A sequence-7 locked row and matching evidence remain readable beside a valid sequence-8 ready closure. Reject coherent rollback using bounded source-derived authority while preserving legitimate historical rows, pure rejections and frozen paired absence.
2. **Terminal repair:** A ready row with forged LastOperationId and missing evidence is preserved and given fresh proof after a valid sequence-9 event. Trust validated prior evidence or restore from the existing source-derived completion; never attest forged metadata.
3. **Worker rejection intake:** A lawful normalized unknown-command rejection is refused by the permission classifier before operational persistence. Preserve source-only rejection prefixes, correct owner scope, and no protected effects on invalid input.
4. **Named non-Folder inputs:** Valid provider-binding partitions and organization rejection positions fail the domain-wide operational handler. Validate the legitimate owner/contract/prefix before returning no Folder operations; retain all malformed/cross-scope refusals.

A separate SDK test patch must independently hold successful-dispatch final lifecycle completion during host stopping. The existing two shutdown repairs, actual-call ownership, unknown fences, pending work and original deadline must survive.

## Individual triage

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| blind-1 | medium | bad_spec | Reader accepts coherent older task row and evidence beside newer closure. Parent probe returns Available/locked/nonterminal for seq7 after seq8 ready closure. Same old receipt is genuine but is insufficient current-view authority; distinct from prior paired-absence/dead-callback triage. |
| blind-2 | medium | patch | Writer preserves a forged LastOperationId from a ready row with missing evidence, then writes fresh proof after a lawful next event. Parent probe returns Completed/forged-operation. Existing source-derived completion can restore the row; prior terminal state alone must not override it. |
| blind-3 | false | reject | The frozen 2026-10-20 decision explicitly permits not-found when view and evidence are both absent; round-five instructions also preserve paired absence at a current checkpoint. Requiring an independent expiry timestamp would change that decision. |
| blind-4 | medium | bad_spec | FolderDomainProcessor emits normalized unknown-command UnsupportedCommandType rejections. Folder permission processing calls the strict command-name classifier before operational persistence and throws for that lawful position. Parent classifier probe confirms the refusal; later source positions can remain stale. |
| blind-5 | maybe-false | carried-defer | carried 5/blind-5: same Messages append/validation and direct single-value store path remain. No new lawful-history measurement against actual transport/store limits was supplied. Retain potential medium unverified disposition; do not duplicate its patch or defer. |
| blind-6 | medium | reject-out-of-scope | Acknowledgment p95 8642.996ms exceeds 1000ms and remains an open gate. Existing instructions require recording rather than general performance redesign; the full retry-wrapper metric does not attribute cost to an approved native/bootstrap subpath. No passing SM4 claim is made. |
| blind-7 | medium | reject-out-of-scope | Status p95 1291.141ms exceeds 500ms and remains open. Preserve this real finding under the same explicit record-only/general-redesign exclusion; Workers timing does not close it. |
| blind-8 | false | reject | The specialized job explicitly runs git submodule update --init --checkout, rather than a strategy-free update. Git documents that --checkout overrides submodule.<name>.update; update=none therefore does not establish this predicted failure. No submodule was changed to verify it. |
| blind-9 | maybe-false | defer | No checked-in invocation was found in Root/Platform workflow sources, and the default CI whitelist is explicit. The current producer separately executes all 11+5 issuer cases. A mandatory automatic-gate requirement or the external owner automation/policy would settle whether this deliberate manual/component partition creates a false-green defect; potential medium remains unverified. |
| blind-10 | low | patch-artifact | The canonical preserved-baseline diff is 1788748446 bytes/10443 sections, chiefly historical evidence. A supplementary 4650235-byte runtime/test view and immutable index/hash were retained for human use without changing the original baseline or pretending exhaustive historical review. |
| edge-1 | medium | bad_spec | Operational handler is registered for the whole folders domain, but no-ops only three ACL contracts. A valid configured provider-binding partition reaches Unknown operational source event; parent probe validates the provider command then reproduces this refusal. Workers already skip the reserved binding owner. |
| edge-2 | medium | bad_spec | A lawful organization FolderCommandRejected alone is not recognized as ACL and reaches FolderStreamName tenant==folder rejection; parent probe reproduces InvalidFolderId. Mixed ACL/rejection histories also fail the three-contract-only loop. Validate legitimate non-Folder prefixes before an empty fold without weakening bad-scope refusal. |
| edge-3 | medium | bad_spec | Same verified coherent rollback as blind-1. Independent old evidence and receipt only prove a former valid value; ReadAsync never applies the supplied sourceEvidence predicate or retained task completion authority to reject the rolled-back nonterminal row. |
| verification-1 | medium | patch | Pre-verified gap accepted as filed: new host-stop test forces Retryable admission and independently holds only the early completion call. Successful dispatch cleanup at line1186 lacks a held-call cancellation assertion. Add that case without changing production semantics or relaxing actual-call/fence/work ownership. |

The submodule CI claim is refuted by the workflow’s explicit `--checkout`, which overrides the configured update strategy. [Git documentation](https://git-scm.com/docs/git-submodule#Documentation/git-submodule.txt---checkout). The paired-row claim contradicts the frozen human decision. Provider-history growth carries its existing unverified disposition; automatic issuer CI remains separately unverified. SM4 remains open and no general performance redesign is authorized.

## Preservation and decision

Keep the 571 SDK, 5673 consumer, 500 package and 11+5 issuer passes and the unchanged tracing-off strict pass (1/1, 443.780s), each bound to its exact source/producer. Preserve the two causal shutdown regressions and all failed/canceled attempts. Keep 100-operation/one-MiB SDK limits, bounded checkpoints, source-time 400-day expiry, terminal authority, real source proofs, five-second service deadline, leases, restart order, public-v2-off and all three protected test bodies unchanged.

The nonfrozen input/authority gaps require another implementation cycle; captured intent needs no change. This review began with counter 5. Before the next loopback it becomes 6, exceeding the rendered workflow’s limit. The [bmad-build skill](../../.agents/skills/bmad-build/SKILL.md) routes to the [review step](../../_bmad/render/bmad-build/folders-02f7d5d6a8fa/d3ba1c521d4f7fe7c437/step-04-review.md), which states: **“If it exceeds 5, HALT and escalate to the human.”**

Requested decision: allow one additional correction and review cycle within the existing intent. No baseline rewrite, physical rollback, dependency/pin update, publication, deployment, commit or push is proposed. Lower code patches/defers await that loopback.

[Machine-readable triage](evidence/story-12-2-review6-triage-20261114.json) · [Private probe and indexed diff artifacts](../../.artifacts/story-12-2-review6-20261114/)

The canonical diff is 1.79 GB because the original baseline is preserved; the supplementary runtime/test view is 4.65 MB. All reviewers acknowledge selective behavioral coverage rather than an exhaustive claim over every historical artifact.
