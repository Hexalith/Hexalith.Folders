---
title: '1.17 Ordinary Platform workspace mapping decision'
type: 'chore'
created: '2026-10-25'
status: 'in-progress'
route: 'oneshot'
baseline_commit: '05cc3af566817f9e22f9e19a2abb2393ac50eee6'
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
