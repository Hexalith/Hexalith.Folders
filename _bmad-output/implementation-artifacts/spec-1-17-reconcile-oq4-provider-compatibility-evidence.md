---
title: 'Reconcile OQ4 current provider evidence for Story 1.17'
type: 'bugfix'
created: '2026-10-08'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The corrected Forgejo live-evidence paragraph makes the current catalog differ from the historical September 15 OQ4 approval digest, blocking the governance gate and Story 1.17 technical readiness.

**Approach:** Apply `docs/governance/approval-policy.md` to record and verify a current technical evidence binding for the bounded documentation correction in commit `6b7274ac06f1bdcb03bc6b10071384406bfd7386`. Preserve historical approvals, C12 fixtures, and corrected live-evidence documentation. Refresh the affected current planning bindings and generated candidate inventory, rerun governance and affected contract checks, and append a precise readiness result. No new owner decision is required unless accepted behavior, risk, consumer scope, production exposure, or rollback changes materially. Keep `V1Only`, T0 unset, and Story 1.17 open.

</frozen-after-approval>

## Implementation Notes

- Investigation resolved the policy boundary: the catalog change accurately separates live observations from production admission results and changes no compatibility profile, ceiling, consumer scope, exposure, rollback, or live-execution claim. There are no intent gaps, external effects, or public API changes.
- Preserve original OQ4 top-level catalog identity, date, reopen policy, and all three approval records; append `current_evidence` with an exact current digest, policy binding, source revision, refresh date, and technical-only scope. Keep the entire historical C12 row and planning history unchanged.
- `GovernanceCompletenessGateTests.cs` must validate historical and current identities separately, retain fixed current digest protection against unreviewed drift, and cover missing/tampered refresh evidence and attempts to rewrite historical approvals. Current OQ4 evidence is governed by the September 24 policy; historical role wording does not require another signature round for this correction.
- Clarify the OQ4 policy paragraph in `docs/operations/provider-integration-and-testing.md` and current binding semantics in `docs/contract/governance-and-completeness-ci-gates.md`, preserving their live-evidence descriptions.
- Regenerate `generated-v2-conformance-set-2026-09-17.yaml` twice through the existing Python generator; preserve the 224-path allowlist. Refresh only `planning-story-manifest.yaml` current OQ4 manifest hash and top-level provenance for governance test source and conformance inventory.
- Append commands, outcomes, exact identities, policy reasoning, and remaining migration gates to `story-1-17-current-candidate-technical-readiness-2026-09-24.md`. This is a bounded reconciliation, not completion of the parent epic story; no sprint status transition or Git recording operation is authorized.
- Verify with a focused Release Contracts build, governance script, conformance/authorization/candidate contract classes, and full Contracts executable using the existing offline Python validator. Independently audit all 20 current planning hashes, generated inventory, historical preservation, and control flags. Complete the skill's independent review before finalizing this spec.
- Implemented the historical/current OQ4 split and fixed-current-digest protection. The entire original evidence prefix, corrected catalog, C12 fixture, approvals, tracker, and control/history fields were independently verified as preserved. Exactly three current planning digests changed.
- Review corrections added the policy LF pin and canonical report input, rejected extra complex YAML keys with negative controls, and restored all existing task-binding, reconciliation, parity-mapping, and Story 4.23 context constraints. The final 224-path inventory changes only the governance test source and report writer. No review layer was skipped; all six independent findings were patched, with none deferred.
- Final validation: focused Release build passes with zero warnings/errors; governance passes 23/23, matrix 7/7, catalog 3/3; full Contracts passes 347/347 with zero skips; two isolated generations match; all 20 current provenance bindings match. The readiness record contains final identities and remaining migration entry gates. V1Only, unset T0, and Story 1.17 backlog are preserved. No commit was made, following the user's repository instruction to avoid Git recording operations unless explicitly required.

## Review Triage Log

- Medium, patch: the newly byte-hashed policy lacked a checkout LF pin. Added `.gitattributes` pin and inclusion in `Oq4LineEndingPinnedPaths`; `git check-attr` confirms LF and governance passes.
- Low, patch: the report's canonical input inventory omitted the policy. Added it to the report writer, owned-input documentation, and existing report contract assertion; the final passing report lists it.
- Medium, patch: `OfType<YamlScalarNode>()` silently ignored extra complex keys. Added a total-key-count check and both sequence-key and mapping-key negative controls; full Contracts passes.
- Medium, patch: context regeneration dropped proof that a task belongs to its authorized folder. Restored the exact existing task-binding constraint and audited it against the starting context.
- Medium, patch: context regeneration omitted bounded checks before human reconciliation. Restored the original unknown-outcome/auto-recovery/reconciliation statement and audited it.
- Medium, patch: context regeneration dropped Story 4.23 ownership and post-1.17 renewal dependencies. Restored the exact existing dependency paragraph and audited it; no story execution or lifecycle change occurred.
