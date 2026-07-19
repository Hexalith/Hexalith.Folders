---
source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-governance-approval-freshness.md
source_date: 2026-07-07T19:49:32+02:00
source_status: approved-and-applied
reconciled_against:
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/.memlog.md
addendum: absent
disposition: no-prd-change
---

# Reconciliation — Governance Approval Freshness

## Source purpose and status

This moderate correct-course proposal hardens the generic C0–C13 governance-completeness gate so an approval-backed criterion cannot pass on vague approval prose, missing authorities, malformed or future dates, or an approval older than the mandatory global freshness window. It generalizes a previously C3-specific exact-approval check and records the work as a pre-delivered Story 11.3 acceptance criterion.

The source is explicitly **approved and applied**. Jerome pre-authorized immediate application and the global 365-day maximum approval age. The coordinated evidence-YAML, conformance-test, gate-documentation, and Story 11.3 edits were completed and verified in that session. It states repeatedly that there is no product, FR/NFR, architecture, wire-contract, surface, or runtime-behavior change.

## PRD-relevant product and release-governance decisions

1. Human approval-backed release evidence must fail closed unless each required authority has a structured record with a specific approver identity and a valid approval date.
2. Approval dates must be well formed, non-future, and fresh. The approved global maximum age is 365 days; expiry intentionally reopens governance review rather than being treated as a CI defect.
3. C3 retention requires PM and Legal approval; C4 bounded-input limits require PM approval. Both were approved in June 2026 and were current when this source was applied.
4. Generic approval wording such as “Legal-approved,” an authority name used as its own approver, or placeholder values is not sufficient governance evidence.
5. The generalized rule is a minimum floor. A criterion-specific gate such as the C3 retention/deletion gate may remain stricter.
6. Future approval-backed criteria must add both the structured approval evidence and the corresponding generic-gate coverage in lockstep.
7. A passing implementation test or delivered story does not by itself establish or preserve approval-backed release acceptance; the governance record must remain current.

## Already covered in the current PRD

- **Deferred Quantitative Targets — Architecture Exit Criteria** states that approved numeric targets and their canonical evidence are summarized in the PRD, changes require documented approval in the governance record, and linked artifacts remain authoritative for measurement detail.
- Exit criterion **C3** already records PM approval on 2026-06-22 and Legal approval on 2026-06-24 and points to `docs/exit-criteria/c3-retention.md` as canonical evidence.
- Exit criterion **C4** already records approval on 2026-06-22 and points to `docs/exit-criteria/c4-input-limits.md` as canonical evidence.
- The **Open Release Items** closing rule already requires every accountable approver to record identity and approval date and requires the governance record to store approved status plus evidence version/digest.
- That same closing rule already prevents a passing test or completed delivery from closing an item by itself and reopens approval when evidence changes.
- **OQ1–OQ10** each name accountable owners/approvers and canonical evidence, which is the product-document consequence of exact approval accountability without duplicating the evidence schema.
- **OQ10** specifically requires the release-calibration plan to freeze approval rules before metric results are accepted.
- The current memlog records that C3 and C4 were reconciled as approved governance constraints; that every open item has canonical evidence and accountable approvers; and that closure requires identity/date plus evidence version/digest and reopens on evidence change.
- The PRD's brownfield authority statement already makes current approved governance artifacts authoritative over historical delivery-status prose.

## Genuine PRD gaps

None.

The 365-day freshness window and exact approval-record schema are governance-control mechanics, not product requirements. The PRD already carries the stable C3/C4 outcomes, current approval dates, canonical evidence links, and the general identity/date/version/digest closure rule. Duplicating token blacklists, evaluator rules, or CI time windows in the PRD would create a second governance schema and a new drift risk.

The PRD's C3 and C4 “Approved” summaries remain accurate as of the current document date, 2026-07-14. Around 2027-06-22/24, the canonical governance gate is intentionally expected to require refreshed approvals. If they are not refreshed, the PRD's status summary should be reconciled then; that future state is not a present gap.

## Conflicts and supersession

### 1. No product-scope conflict

The source expressly says there is no FR/NFR or product-behavior change. It must not be converted into a new FR, NFR, user journey, or product feature.

### 2. Canonical evidence, not the proposal, governs current approval state

The proposal records the June 2026 approval facts and the then-current `ApprovalBackedCriteria = [C3, C4]` set. These are historical applied-state facts. The current governance evidence and gate are authoritative if approvers, dates, supported criteria, or freshness policy later change.

### 3. The 365-day rule does not make approval dates permanent PRD constants

The source intentionally causes annual re-review. Do not pin Jerome, Jérôme Piquot, Louveciennes, or the June 2026 dates into normative FR/NFR prose. The PRD should summarize current status and link canonical evidence; refreshed records belong in governance artifacts.

### 4. The source's pinned approval-backed set is intentionally operational

The hardcoded C3/C4 coverage list is part of a fail-closed conformance implementation. It is not a product inventory. Future criteria can become approval-backed without a PRD structural change, provided their product outcome and release evidence are already represented or separately reconciled.

## Implementation, test, story, and artifact detail that stays out of the PRD

- The `approval_policy` YAML shape, `max_age_days`, `generic_approver_tokens`, per-row `approval` blocks, `required_authorities`, records, optional `location`, and optional `review_by` are evidence-schema details.
- The exact generic-token blacklist and specific C3/C4 approver records belong in canonical governance evidence, not the product contract.
- `GovernanceCompletenessGateTests`, `ApprovalBackedCriteria`, `EvaluateApprovalRecords`, synthetic negative controls, `GateDiagnostic`, and the six diagnostic category names are test implementation.
- PowerShell filter behavior, the retention-specific placeholder guard, cache-key-exception precedent, document `ShouldContain` locks, and unchanged pinned inventories are repository/gate mechanics.
- Story 11.3 acceptance-criterion placement and “delivered ahead” handling are epic/story execution governance.
- Build/test counts, gate JSON status, formatting evidence, HEAD SHA, and LF status are session verification evidence.
- The exact annual red dates and maintainer lockstep procedure belong in the gate documentation and operational calendar rather than PRD narrative.

## Recommended stable-ID edits or additions

- **Add no FR, NFR, OQ, journey, or success metric; renumber nothing.**
- Retain exit-criteria IDs **C3** and **C4** and their current PRD outcomes and canonical evidence links.
- Retain **OQ1–OQ10** and the shared closure paragraph requiring approver identity/date, approved status, evidence version/digest, and reopening on evidence change.
- Do not add a PRD-owned `max_age_days` field or approver-token vocabulary. If the governance policy changes materially, update the canonical governance artifacts and reconcile only the PRD's current status summaries or product consequences.
- If C3 or C4 approval expires without refresh, update its existing status under the same stable ID; do not create a replacement criterion.

## Qualitative ideas at risk

- Human approval is structured evidence, not a reassuring phrase.
- Freshness is part of correctness: a once-valid sign-off can stop being sufficient without any code regression.
- The intentional annual red gate creates an accountable re-review moment and must not be “fixed” by weakening the test.
- Generic governance checks establish a reusable floor while criterion-specific checks may enforce stricter domain rules.
- Gate diagnostics must stay bounded and metadata-only even when approval data is malformed.
- Documentation, evidence schema, and executable checks must evolve in lockstep so governance prose cannot drift away from enforcement.
- Current product outcomes should remain stable while the evidence supporting their release acceptance is periodically renewed.

## Disposition

**No PRD change.** Treat the proposal as approved governance-gate and verification provenance. Its product-document consequences are already captured by C3/C4 status and evidence links, the Open Release Items' accountable-approval closure rule, OQ10's approval-rule requirement, and the corresponding memlog decisions. Preserve the current PRD and keep the 365-day freshness mechanics, exact records, diagnostics, and future-criterion lockstep procedure in governance artifacts and tests.
