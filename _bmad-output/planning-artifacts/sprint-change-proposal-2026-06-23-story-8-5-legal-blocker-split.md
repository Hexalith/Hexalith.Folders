# Sprint Change Proposal — Story 8.5 externally-blocked AC split

- **Date:** 2026-06-23
- **Author / decision-maker:** Jerome (jpiquot@itaneo.com)
- **Workflow:** bmad-correct-course
- **Mode:** Batch
- **Scope classification:** Moderate (backlog reorganization — PO/DEV)
- **Status:** Approved & applied
- **Triggering story:** 8.5 — "Close C3 Legal sign-off and drive the residual test baseline honestly green"

---

## Section 1 — Issue Summary

Story **8.5** (the final Epic-8 release-acceptance closure story) was parked in `review` and could not advance to `done`. Its acceptance criteria split cleanly into two natures:

- **ACs 2–6 (dev-completable) — complete and adversarially review-approved.** Residual non-composition reds were honestly triaged (verified green at HEAD: `Testing.Tests` 60/60, `Contracts.Tests` 256/256, provider-boundary guards 3/3); the obsolete fail-open `--filter` masks in `run-baseline-ci-gates.ps1` (L27, L47) were removed; the full 63-test UI.E2E lane was stood up as a Chromium-provisioned `e2e-gates` CI job; the honest-green baseline was recorded as release evidence.
- **AC1 (C3 Legal sign-off) — externally blocked.** C3 retention is PM-approved (Jerome, 2026-06-22) with Legal as the **sole remaining governance gate**. The recorded external Legal sign-off is **not in hand**, and per the story's own AD1 the dev correctly refused to fabricate it — C3 stays `reference_pending` under a `release_blocking_until_legal_approval` posture, byte-for-byte unchanged.

**Core problem (issue type: external dependency / blocked approach):** a single acceptance criterion depends on an external decision-maker (Legal) the dev team cannot resolve, holding the whole story, Epic 8, and the MVP-release gate in indefinite `review`. This is the sole remaining MVP-release blocker.

**Evidence:** `8-5-…md` status + Dev Agent Record (ACs 2–6 ✅, AC1 ⛔ blocked-pending-Legal, C3 cascade git-verified unchanged); `c0-c13-governance-evidence.yaml:52` (`reference_pending`); `run-retention-deletion-gates.ps1` emits `status=release-blocked policy_status=reference_pending`; `architecture.md:181,195,206`; readiness review 2026-06-22 Critical #1; `sprint-change-proposal-2026-06-22.md:45,93` (PM-approved, Legal-only, handoff "Legal signs off → flip C3").

## Section 2 — Impact Analysis

- **Epic Impact (Epic 8 — Action-needed → resolved by split).** Epic 8 could not reach `done` solely because 8.5 could not, and 8.5 could not solely because of one externally-gated AC. The split isolates that external dependency so Epic 8's entire dev scope (8-1..8-5) is complete and the residual blocker is one clean, owner-assigned story.
- **Story Impact.** 8.5 rescoped to ACs 2–6 → `done`. New successor **8.6** carries the original AC1 → `backlog` / blocked-pending-Legal.
- **Artifact Conflicts.** None in PRD / Architecture / UX. The product scope, per-class C3 retention values, and tenant-deletion dispositions are settled and PM-approved. The only artifact correctly "in tension" is the governance posture (`release_blocking_until_legal_approval`), which is *intentionally* blocking and is preserved.
- **Technical Impact.** None. No spine/OpenAPI/generated-client/aggregate/REST-CLI-MCP-behavior change. This is a backlog/governance-tracking reorganization only.

## Section 3 — Recommended Approach

**Selected: Option 1 — Direct Adjustment (story split).** Effort **Low**, risk **Low**.

Extract AC1 into a dedicated turnkey successor story (8.6), credit 8.5's completed scope as `done`, and track the external gate as a single named, owner-assigned (Legal via PM) backlog story whose complete in-lockstep edit set is already documented.

**Alternatives considered:**
- **Option 2 — Rollback: rejected (not viable).** ACs 2–6 are correct and valuable; reverting would re-introduce the exact 7.18 fail-open-mask anti-pattern. The blocker is external, not a failed approach.
- **Option 1b — Keep 8.5 in `review`: rejected.** Honest but buries five completed, review-approved ACs behind one external gate and holds the whole epic open indefinitely on a single signature.
- **Option 3 — MVP Review (descope C3 as release-blocking): rejected here.** Releasing without Legal sign-off changes the release-acceptance definition and is a PM/Legal compliance decision, not a process tidy-up. Not chosen; the release-blocking posture is preserved.

**Rationale.** The split is the standard BMAD pattern for a story mixing done + externally-blocked ACs. It produces an honest status (5 ACs credited; 1 isolated), unblocks the epic's dev scope, preserves the praised honest-`reference_pending` discipline (no fabricated approval), and turns the MVP blocker into a single turnkey story. **Important:** the split does **not** unblock the release — the MVP stays release-blocked on C3 until Legal signs and 8.6 is applied; the split only makes the tracking honest and isolates the blocker.

## Section 4 — Detailed Change Proposals

**Stories**
- `8-6-record-c3-legal-signoff-and-apply-cascade.md` — **NEW.** Self-contained, turnkey successor carrying original AC1 + the verbatim file:line in-lockstep C3 edit set + ADs (AD1/AD3/AD4/AD6) + risks (R1/R2/R6). Status `backlog` (blocked-pending-Legal); baseline `c7a79ac`.
- `8-5-close-c3-legal-signoff-and-residual-reds.md` — status `review` → **`done`**; AC1 marked split-off to 8.6; Change Log entry added. (Filename/key retained for traceability; rescope recorded in-file.)

**Epics** (`epics.md`)
- Story 8.5 retitled "Drive the residual test baseline honestly green"; AC rewritten to the residual-reds/honest-green-baseline scope; split note added.
- Story 8.6 added "Record C3 Legal sign-off and apply the in-lockstep C3 retention cascade" with the extracted AC1 (blocked-pending-Legal).
- Epic 8 intro note updated to record the split.

**Sprint status** (`sprint-status.yaml`)
- `8-5-…: review` → `done`; `8-6-record-c3-legal-signoff-and-apply-cascade: backlog` added; Epic 8 comment block updated. Epic 8 stays `in-progress` (8-6 open).

## Section 5 — Implementation Handoff

- **Scope:** Moderate (backlog reorganization) — applied within this correct-course run.
- **Now (PO/DEV):** backlog reorg complete (story split, epics, sprint-status) — see File List below.
- **Pending external (Legal → PM Jerome → DEV):** when recorded Legal sign-off arrives, the dev executes **Story 8.6** — apply the documented in-lockstep C3 cascade in one atomic commit, re-run the retention/contract-spine/governance gates green, then move 8.6 → `done` and **Epic 8 → `done`**, clearing the MVP-release blocker.
- **Success criteria:** 8.5 `done`; 8.6 tracked as the single, owner-assigned MVP-release blocker; no fabricated approval; C3 cascade unchanged until Legal signs.

**Files changed by this proposal:**
- `_bmad-output/implementation-artifacts/8-6-record-c3-legal-signoff-and-apply-cascade.md` (new)
- `_bmad-output/implementation-artifacts/8-5-close-c3-legal-signoff-and-residual-reds.md` (status + change log)
- `_bmad-output/planning-artifacts/epics.md` (8.5 rescope + 8.6 add + Epic 8 note)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (8-5 → done; 8-6 → backlog; Epic 8 note)
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-23-story-8-5-legal-blocker-split.md` (this document)
