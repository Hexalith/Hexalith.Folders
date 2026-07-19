---
review: authority-and-supersession-consistency
artifact: _bmad-output/planning-artifacts/prd.md
reviewed_at: 2026-07-15
verdict: pass-with-provenance-correction
severity_counts:
  critical: 0
  high: 0
  medium: 1
  low: 1
---

# Authority and Supersession Reviewer Gate

## Overall verdict

**PASS WITH PROVENANCE CORRECTION.** The normative PRD and memlog apply the approved 2026-07-15 authority correctly: stable IDs are preserved, document finality is separated from not-ready implementation status, UJ2/FR56/OQ8 are corrected, FR58 remains metadata-token recall, and OQ11–OQ13 remain excluded as unratified proposals. No product-scope, safety-boundary, or stable-ID defect was found.

One medium audit-trail issue remains: the PRD cites the internally contradictory synthesis but omits the later delta review that explains why the synthesis's unratified “exact patch” sections were not applied. One low wording issue could cause a casual reader to mistake “19 proposals reconciled under an approved authority” for “19 approved proposals.”

## Critical findings

None.

## High findings

None.

## Medium findings

### M1 — The applied supersession adjudication is missing from PRD provenance

**PRD locations:** frontmatter `inputDocuments` and `documentCounts` at `prd.md:20–58`; July 15 edit-history entry at `prd.md:79–80`.

The PRD lists `reconcile-july-2026-synthesis.md` as its sole reconciliation input. That synthesis correctly says the current patch must stay narrow and that OQ11–OQ13 need future PM ratification, but its later “Exact PRD patch plan” contradicts that authority analysis by prescribing FR2/FR32/FR34–FR35 edits, audit-derived NFR additions, OQ5/OQ10 changes, and OQ11–OQ13 insertion. The subsequent `reconcile-july-2026-delta-review.md` resolves this conflict in favor of the approved July 15 authority and explicitly instructs that those edits remain excluded pending approval.

The applied PRD follows the delta review correctly, and `.memlog.md:94–104` records the resulting precedence. However, a future reviewer following only the PRD's declared inputs will reach a reconciliation document whose detailed patch instructions do not match the PRD, without seeing the adjudication artifact that explains the difference.

**Fix recommendation:** either:

1. add `_bmad-output/planning-artifacts/reconcile-july-2026-delta-review.md` immediately after the synthesis in `inputDocuments`, set `documentCounts.reconciliations: 2`, and mention the delta adjudication in the July 15 edit-history entry; or
2. amend the synthesis so its detailed patch plan consistently labels FR2/FR32/FR34–FR35, the extra NFRs, OQ5/OQ10 edits, and OQ11–OQ13 as non-applied proposals.

The first option preserves the immutable synthesis and provides the clearest audit chain.

## Low findings

### L1 — Edit-history wording does not explicitly distinguish pending/proposed inputs

**PRD location:** `prd.md:79–80`.

The entry says, “Reconciled 19 July sprint-change proposals under the approved 2026-07-15 authority.” This is technically accurate—the authority is approved, not necessarily every source—but at least one reconciliation records its source as pending approval (`2026-07-07-193110`), and other sources are proposed, deferred, or no-op. In an authority-focused audit, the sentence can be misread as retroactive approval of all 19 proposals.

**Fix recommendation:** revise the opening clause to:

> Reconciled 19 July sprint-change inputs—including approved, applied, pending, proposed, deferred, and no-op sources—under the approved 2026-07-15 authority...

This is provenance wording only; no requirement or memlog decision changes.

## Mechanical notes

- **Chronology/status precedence:** Correct. The approved July 15 proposal governs; July 14 remains amended provenance. Frontmatter uses the 2026-07-15 readiness report and retains the 2026-07-14 durable-MVP ratification date (`prd.md:67–77`).
- **Final versus readiness:** Correct. `status: final` and `implementationReadiness: not-ready` coexist, and Current Delivery Posture explains the distinction (`prd.md:67–77`, `prd.md:130–132`).
- **Stable IDs:** Correct. UJ1–UJ9 occur once, FR1–FR58 occur once, and OQ1–OQ10 occur once. No identifier was renumbered or reused.
- **UJ2:** Correct. Tenant policy belongs to the tenant administrator; the scoped platform engineer only validates/diagnoses and unauthorized observation fails closed (`prd.md:218–226`).
- **FR56:** Correct. The same actor must hold incident-admin and fresh tenant/folder authorization before lookup, count, checkpoint, filter, or shaping; denial precedes observation and emits safe audit (`prd.md:828`).
- **OQ8:** Correct. It covers all mutations, read-key rejection, expired-key precedence, and minimal consumed-key persistence/retention evidence without dictating the storage implementation (`prd.md:952`).
- **FR58:** Correct and preserved. It remains authorized metadata-token recall with authoritative hydration, stale/archived/revoked/unauthorized/hidden-hit removal, metadata-only egress, and fail-safe unavailability. Indexed body recall remains outside the current release and requires Security/PM approval plus a future stable FR (`prd.md:831–835`; `.memlog.md:103`).
- **OQ11–OQ13:** Correctly excluded. The PRD retains OQ1–OQ10, the edit history identifies the July 14 audit recommendations as unratified, and the memlog explicitly keeps OQ11–OQ13 as reconciliation evidence only (`prd.md:79–80`, `prd.md:939–956`; `.memlog.md:104`).
- **Approved versus proposed authority:** Normative content reflects only the latest approved source. Pending/proposed earlier sources are used as provenance and downstream evidence, not silently promoted into requirements.
- **Input coverage:** All 19 original July sprint-change proposals and both readiness reports are listed. Counts match that inventory. The sole gap is the missing delta-review pointer described in M1.
- **Qualitative product ideas:** No approved qualitative product idea was silently dropped. Completed-foundation value, safe-empty versus positive capability, durable-MVP truth, tenant-policy ownership, dual incident authorization, expired-key persistence, FR58 trust boundaries, and planning-honesty principles are present in the PRD or memlog. Unratified exact-byte/edge-security/operational-truth proposals are explicitly retained in the synthesis/delta evidence rather than being presented as approved scope.
- **Addendum:** None is required for this authority review; technical mechanisms, alternatives, test counts, and story ownership remain preserved in their source proposals and reconciliation artifacts.
