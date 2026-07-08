# Sprint Change Proposal — Codify the C3/C4 Governance-Approval / NFR-Traceability Decoupling Precedent

- **Date:** 2026-07-07
- **Author:** Jerome (via bmad-correct-course)
- **Change scope classification:** Minor (documentation codification of an already-enforced precedent)
- **Mode:** Batch — drafted, applied, and verified in one pass
- **Status:** Applied and verified (conformance gates green)

---

## Section 1 — Issue Summary

**Problem statement.** The project has an established, test-enforced precedent that a governance exit
criterion can be `approved` while one or more NFR traceability rows that *cite* that criterion remain
`reference-pending`. That precedent lived only implicitly — in a conformance-test hard-pin, in per-row prose,
and in a story-file deviation note — and was **never stated as a first-class principle** in the planning
artifacts. As a result, a future governance cascade could reasonably (and wrongly) "fix" a reference-pending
NFR row by flipping it to `covered` once its criterion is approved, which would redden the hard-pin gate; or
conversely treat a lingering reference-pending row as evidence that an approved criterion is not really
approved.

**Where it was discovered.** During the C3 Legal sign-off cascade (Story 8.6), the natural cascade instinct
was to flip `NFR57` (which cites C3) to `covered`. The story instead recorded "keep `NFR57` reference-pending"
as an **accepted deviation**, because `NfrTraceabilityConformanceTests.ReferencePendingRowsAreOwnedAndSurfaceKnownGaps`
hard-pins `C3`/`C4`/`C7`/`C12` as reference-pending gaps. The same tension recurs for C4 (`NFR26`, `NFR28`).

**Evidence.**

- `docs/exit-criteria/c0-c13-governance-evidence.yaml`: `C3` = `approved` (PM 2026-06-22; Legal 2026-06-24),
  `C4` = `approved` (PM 2026-06-22); `C7`, `C12` = `reference_pending`.
- `docs/exit-criteria/nfr-traceability.md`: `NFR57` (cites `C3`, owner Legal + PM), `NFR26`/`NFR28` (cite
  `C4`, owner PM) all carry status `reference-pending`, with per-row notes "remains reference-pending only for
  downstream evidence and conformance-guard visibility."
- `tests/Hexalith.Folders.Contracts.Tests/Deployment/NfrTraceabilityConformanceTests.cs:188-211`
  (`ReferencePendingRowsAreOwnedAndSurfaceKnownGaps`) hard-pins `{ "C3", "C4", "C7", "C12" }` as
  reference-pending gaps that must stay surfaced (line 202).
- Story 8.6 (`8-6-record-c3-legal-signoff-and-apply-cascade.md`) records the `NFR57` reference-pending
  deviation as accepted.

## Section 2 — Impact Analysis

| Track | Meaning | Owner | Source of truth |
| --- | --- | --- | --- |
| Governance-criterion status | Is the exit criterion signed off? | Per-criterion owner in the YAML (e.g. Tech Lead / Architect / Legal + PM) | `docs/exit-criteria/c0-c13-governance-evidence.yaml` |
| NFR-traceability row status | Is the downstream NFR evidence current, or is an owned gap still open? | Per-row `Owner` column (may differ from the criterion owner) | `docs/exit-criteria/nfr-traceability.md` + `NfrTraceabilityConformanceTests` |

The two tracks are **separately owned and independently gated**. Approving a criterion never auto-converts
its cited NFR rows; a lingering `reference-pending` NFR row never reopens the criterion's approval.

- **Epic impact:** None. No epic scope, sequencing, or acceptance criteria change. (Story 11.1's governance
  pin map already §12-annotates `NFR57`; this codification complements it — see optional follow-up.)
- **Story impact:** None. No story added, removed, or re-scoped.
- **Artifact conflicts / updates (all documentation):**
  - `_bmad-output/planning-artifacts/architecture.md` — the C0–C13 governance section had no statement of the
    decoupling principle.
  - `docs/exit-criteria/nfr-traceability.md` — the `reference-pending` status legend described "not converted
    by narrative" but not the governance-approval decoupling.
  - `docs/exit-criteria/c0-c13-governance-evidence.yaml` — no in-file pointer explaining why approved C3/C4
    still own reference-pending rows.
- **Technical impact:** None. No code, contract spine, generated artifact, test logic, or CI wiring change.
  The precedent was already enforced by `NfrTraceabilityConformanceTests`; this proposal only documents it.

## Section 3 — Recommended Approach

**Selected path: Option 1 — Direct Adjustment (documentation codification only).** Effort: Low. Risk: Low.

- Rollback (Option 2) — not applicable; nothing to revert.
- MVP review (Option 3) — not applicable; MVP scope unchanged.

**Rationale.** The precedent is real, correct, and already test-enforced. The only gap is that it was not
written down as a durable principle, creating a foreseeable trap for the next governance cascade or refactor
baseline. Codifying it in the three chosen homes (spine + operator-facing doc + the evidence file itself)
removes the trap with zero behavior change and zero test risk.

## Section 4 — Detailed Change Proposals

### 4.1 `_bmad-output/planning-artifacts/architecture.md` (Architecture Exit Criteria section)

Added a new blockquote immediately after the "Status reconciliation (2026-06-22)" note:

> **Governance-approval / NFR-traceability decoupling precedent (2026-07-07, codifies the C3/C4 case).** A
> criterion's `approved` status in `c0-c13-governance-evidence.yaml` and the status of the NFR rows that
> *cite* that criterion in `nfr-traceability.md` are **two separately-owned tracks**. Approving a criterion
> does **not** auto-convert its cited NFR rows to `covered`, and a still-open `reference-pending` NFR row does
> **not** reopen or contradict the criterion's approval. Precedent: C3 and C4 are both `approved`, yet
> `NFR57` (C3), `NFR26`/`NFR28` (C4) stay `reference-pending`. **Rule for governance cascades:** never flip a
> cited NFR row to `covered` just because its criterion was approved (that reddens the hard-pin gate); to
> retire a reference-pending NFR row, change the row, the traceability doc's "Reference-pending
> release-blocking gaps" section, and the test's `{ C3, C4, C7, C12 }` hard-pin set together, owned separately
> from the governance YAML status.

### 4.2 `docs/exit-criteria/nfr-traceability.md` ("Status semantics" — `reference-pending` bullet)

Appended to the `reference-pending` legend bullet:

> A row that cites a governance criterion already marked `approved` in
> `docs/exit-criteria/c0-c13-governance-evidence.yaml` still stays `reference-pending` while a
> separately-owned downstream evidence or conformance-guard gap remains: criterion approval and NFR-row status
> are distinct tracks, so approving the criterion never auto-converts its cited rows (precedent: `C3` with
> `NFR57`, and `C4` with `NFR26` / `NFR28`).

### 4.3 `docs/exit-criteria/c0-c13-governance-evidence.yaml` (comment before `criteria:`)

Added a parser-ignored comment block cross-linking the precedent and naming the hard-pinned criteria/rows and
the enforcing test (`ReferencePendingRowsAreOwnedAndSurfaceKnownGaps`), so a reader editing the evidence file
sees why approved C3/C4 still own reference-pending rows.

## Section 5 — Implementation Handoff

- **Scope:** Minor — implemented directly and verified in this session; no downstream dev handoff required.
- **Verification performed:** `dotnet test` filtered to `NfrTraceabilityConformanceTests` +
  `GovernanceCompletenessGateTests` → **29/29 passed, 0 failed**. All three edited files confirmed pure-LF
  (no mixed line endings). No code/spine/generated-artifact/test-logic change.
- **sprint-status.yaml:** No change — no epic or story added/removed/renumbered (Checklist 6.4 = N/A).
- **Optional follow-up (not in this change):** Cross-reference this precedent from Story 11.1's governance pin
  map (§12) and/or the `deferred-work.md` ledger so the Epic 11 refactor baseline explicitly protects the
  decoupling. Owner: whoever next touches the 11.1 pin map.

---

**Approval:** Codification homes and apply-mode selected by Jerome via AskUserQuestion on 2026-07-07 (all three
homes; "draft, apply, verify, then report"). Changes applied and verified in this session.
