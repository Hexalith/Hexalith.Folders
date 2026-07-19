---
source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-nfr-traceability-decoupling.md
source_date: 2026-07-07
source_status: applied-and-verified
reconciled_against:
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/.memlog.md
addendum: absent
disposition: no-prd-change
---

# Reconciliation — Governance Approval / NFR Traceability Decoupling

## Source purpose and status

This minor correct-course proposal codifies an already enforced governance precedent: approval of an exit criterion and closure of downstream NFR-traceability rows that cite it are separate, independently owned states. Its immediate aim was to stop future governance cascades from automatically marking an NFR row `covered` merely because its cited criterion became `approved`, or from treating a still-visible downstream evidence gap as proof that criterion approval was invalid.

The source is **applied and verified**. Jerome selected all three documentation homes and direct apply/verify mode. The architecture note, NFR-traceability status semantics, and governance-evidence comment were updated; conformance gates passed. The source explicitly records no epic/story, product, FR/NFR, runtime, contract, generated-artifact, or CI-logic change.

## PRD-relevant product and release-governance decisions

1. Governance-criterion approval answers whether the criterion itself has the required sign-off. NFR-traceability status answers whether separately owned downstream evidence and conformance coverage are current.
2. Those tracks are independently owned and gated. Criterion approval does not auto-close cited traceability gaps; a cited `reference-pending` row does not automatically reopen or contradict the criterion approval.
3. C3 and C4 are the governing precedent: both criteria are approved, while NFR57 (C3) and NFR26/NFR28 (C4) remain intentionally visible as downstream `reference-pending` evidence/conformance gaps.
4. A traceability row may move to `covered` only when its own downstream evidence gap is actually closed and its canonical traceability inventory and conformance guard are updated together.
5. Visible pending evidence is a release-readiness signal, not necessarily a product-scope decision or an invalidation of already approved numeric/policy outcomes.

## Already covered in the current PRD

- **Deferred Quantitative Targets — Architecture Exit Criteria** records **C3** and **C4** as approved and links their canonical evidence. This preserves their criterion status independently of downstream traceability mechanics.
- The same section records **C7** as `Reference-pending` and tracks its product/release consequence through **OQ1**, showing that criterion status is explicitly represented when the criterion itself is not approved.
- The statement “Each target must be validated through its named release-calibration evidence” already separates an approved target from completion of the evidence required for release use.
- **Open Release Items** distinguishes bounded decisions/inventories (**OQ1–OQ4**) from implementation and release evidence (**OQ5–OQ10**), which is the PRD-level expression that approved scope/outcomes and downstream readiness can have different states.
- The shared Open Release Items closure rule requires canonical evidence, accountable approval, and version/digest governance; a passing test or delivery alone cannot close a gap.
- The PRD's authority statement makes current approved governance artifacts authoritative for approved criterion outcomes, while canonical evidence/traceability artifacts own their own detailed status.
- The memlog records **C3** and **C4** as approved constraints, keeps **C7** as an owned release item, and explicitly separates bounded parameter/inventory closure from implementation/release-evidence closure.

## Genuine PRD gaps

None.

The decoupling rule governs how architecture, governance evidence, NFR traceability, and conformance gates interpret one another. The PRD already carries the product outcomes, approved/current criterion status, canonical evidence links, and independently owned release blockers. Adding NFR26, NFR28, NFR57, or the hard-pin set to the PRD would duplicate the traceability inventory and make the product contract depend on test implementation.

## Conflicts and supersession

### 1. No FR/NFR or product-scope conflict

The source expressly says it changes documentation semantics only. Do not turn it into a new requirement or reopen C3/C4 product decisions.

### 2. The memlog's “evidence change reopens approval” rule remains valid but scoped

The current memlog and PRD say a later change to an open item's canonical evidence reopens approval. That does not conflict with this source: a separately owned NFR row remaining `reference-pending` is not itself a change to C3/C4's canonical approval evidence. If the criterion's canonical evidence or approved outcome changes, reapproval is still required.

### 3. The hard-pinned row and criterion set is historical operational state

The source names NFR57, NFR26, NFR28 and the `{C3, C4, C7, C12}` conformance set as of July 7. Those exact memberships belong to the current traceability document and tests. They can change through their own lockstep governance without renumbering PRD requirements or editing the PRD unless a product outcome changes.

### 4. `reference-pending` must not become a permanent exemption

Decoupling means “do not auto-convert,” not “never close.” When the downstream evidence exists and the separately owned traceability/gate changes are approved, the row should move to `covered`. The source's precedent must not be used to hide a real release gap indefinitely.

## Implementation, test, story, and artifact detail that stays out of the PRD

- `NfrTraceabilityConformanceTests.ReferencePendingRowsAreOwnedAndSurfaceKnownGaps`, its hard-pinned set, line references, and the 29/29 verification result are test details.
- NFR26, NFR28, and NFR57 row ownership, notes, and status transitions belong in `docs/exit-criteria/nfr-traceability.md`, not in the PRD's stable requirements inventory.
- The YAML parser comment, architecture blockquote, traceability legend wording, pure-LF check, and optional Story 11.1/deferred-ledger cross-reference are artifact-maintenance details.
- Story 8.6's accepted-deviation note and C3 Legal-signoff cascade are delivery/governance provenance.
- The three-file lockstep process for retiring a hard-pinned row is conformance governance, not product behavior.

## Recommended stable-ID edits or additions

- **Add no FR, NFR, OQ, journey, metric, or exit criterion; renumber nothing.**
- Retain **C3** and **C4** as approved under their current stable IDs and canonical evidence links.
- Retain **C7** and **OQ1** as the current criterion-level pending decision; do not infer C7 approval from unrelated NFR traceability work.
- Keep downstream NFR row IDs and their `reference-pending`/`covered` status in the canonical traceability artifact and tests.
- If a downstream gap later changes a product outcome rather than evidence only, reconcile the affected existing FR/NFR or add a new stable requirement through normal PRD governance; do not overload C3/C4 status.

## Qualitative ideas at risk

- “Approved policy” and “complete delivery evidence” are different questions; collapsing them produces false green or false red reporting.
- A visible `reference-pending` row is valuable: it preserves ownership and shows what remains without erasing an approved governing decision.
- Governance cascades should update only the artifacts whose underlying truth changed, not mechanically propagate a status word across every citation.
- Separate ownership matters. Criterion approvers and downstream evidence owners may be different people with different closure conditions.
- Decoupling protects both directions: it prevents premature closure of evidence gaps and prevents completed governance approval from being accidentally undone by an unrelated traceability state.
- Canonical traceability prose and executable hard-pin guards must change together when a gap genuinely closes.

## Disposition

**No PRD change.** Treat this source as applied governance/traceability interpretation. Its PRD-level meaning is already represented by the C3/C4 approved statuses, C7/OQ1 pending status, named canonical evidence, and the distinction between bounded decisions and downstream release evidence. Preserve current stable IDs and keep row-level decoupling mechanics in architecture, traceability, governance evidence, and conformance tests.
