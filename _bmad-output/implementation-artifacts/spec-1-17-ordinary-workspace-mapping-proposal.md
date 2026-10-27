---
title: '1.17 Ordinary Platform workspace mapping decision'
type: 'chore'
created: '2026-10-25'
status: 'done'
route: 'oneshot'
baseline_commit: '09707e19ab7024b88b34d89651e15a45bc895eeb'
review_loop_iteration: 0
---

<frozen-after-approval reason="User explicitly authorized preparation of the next concrete mapping decision, without applying it">

## Intent

Prepare one reviewable decision for Jerome: the smallest permitted change exposing the owner-qualified Parties source to the ordinary public Platform CLI. Reconfirm current revisions, working bytes and dependency pins; retain exact commands, outcomes and hashes. Supply a separate exact proposed patch, full revisions, provenance, source-selection behavior, rollback and a bounded downstream-check matrix. Distinguish ordinary CLI mapping from hosted graph-test fixtures. Present one focused approval scope for a later application; prior declaration approval does not approve this new workspace tuple.

Preserve existing edits, historical approvals, parent/tracker values, V1Only, unset T0, null activation authority and fourteen unexecuted staging checks. Do not apply mapping changes, stage, commit, push, publish, deploy, update dependencies or initialize nested submodules. Retain the EventStore/Tenants/Memories policy conflict as a separate unresolved decision. Reuse unchanged evidence and completed verifier results; do not repeat builds, graph tests or adoption audits. Do not invent owners, declarations, runtime dependencies, retirement or acceptance.

</frozen-after-approval>

## Implementation Notes

- Scope is a small, reversible documentation/patch-artifact change with no unresolved intent for preparation. The pending owner decision governs later application, not preparation. User authorization permits working around existing edits; the explicit preservation/no-commit instructions override workflow status synchronization and commit defaults.
- Retain the completed owner-intake and composition capsules by receipt hashes. Compare their relevant bytes and current Git state without rerunning their verifiers. Read-only investigation delegated to ordinary-mapping and hosted-contract reviewers as directed by build step 2.
- External commits advanced root from initial observation `7c2c3c6acba97db4f1d3c42a304b16d70b0850c0` to `05cc3af566817f9e22f9e19a2abb2393ac50eee6`. The proposal uses the latter snapshot. Earlier observation is informational only; all accepted tuples remain in their original records.
- New artifacts: `story-1-17-ordinary-workspace-mapping-proposal-2026-10-25.md`, matching `.patch`, and `evidence/story-1-17-ordinary-workspace-mapping-proposal-2026-10-25.json` under this directory. Source/configuration and parent/tracker files are unchanged.
- Narrow validation: receipt/member hashes, current tracked/index/gitlink preservation, and `git apply --check` on the proposed patch. No production test is needed for proposal-only artifacts; unchanged executable evidence is reused within its recorded scope.
- External `/pushall` commit `09707e19ab7024b88b34d89651e15a45bc895eeb` captured the draft artifacts and advanced four root gitlinks during review. The strict preservation command failed on external Tenants drift; retain its exact failure and the complete later drift report. Original modified Platform/Projects file bytes remain preserved. Platform checkout `9869094acc21f9054960a573f884c60ddf3235cd` differs from root gitlink `30673fdab03b5c5d527eb9eb596bfa6645ad6649`; no implicit pin update, checkout restoration or tuple acceptance follows. Updated the review proposal to the final root tuple without applying its patch.
- The configured blind-review layer ran; no configured review layer was skipped. Five concrete findings were verified and corrected in the proposal/evidence only. No additional application verification was warranted. Status done covers preparation, review and presentation only; Jerome's mapping decision remains pending, parent remains in-progress and tracker remains backlog.

## Review Triage Log

- Medium, patched: generic checkout-mismatch rejection overstated the source contract. Released resolver treats existing Platform/Builds separately and retains source identity warnings. Qualified the statement and kept explicit preflight and qualification limits.
- Medium, patched: requested mapping filename did not match the actual materializer. Corrected to `<run workspace>/source-mapping/mapping.json` with retained `selected-source-mapping.json` naming distinguished.
- Medium, patched: final preservation wording preceded completion of the comparison; external Git changes then made whole-workspace preservation false. Retained the failed strict command and explicit complete drift inventory, keeping original edit-byte preservation distinct from changed revisions/indices.
- Medium, patched: mapping ContentHash alone does not bind Parties source revision/bytes. Added required contemporaneous owner gitlinks, HEADs and dirty/declaration/project hashes to future evidence collection.
- Medium, patched: pre-application inventory omitted a retained submodule Git directory. Added `git rev-parse --git-path modules/Hexalith.Parties` and preservation/reconciliation of existing configuration and refs before any initialization.
