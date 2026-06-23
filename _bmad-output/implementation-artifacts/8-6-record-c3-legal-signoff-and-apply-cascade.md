---
baseline_commit: c7a79ac75a7801d8733fd5c495902f53939f597b
---

# Story 8.6: Record C3 Legal sign-off and apply the in-lockstep C3 retention cascade

Status: ready-for-dev — **BLOCKED-PENDING-LEGAL** (external Legal sign-off not yet in hand). Story is fully contexted and turnkey: apply the documented in-lockstep cascade in one commit the moment Legal evidence arrives. The dev MUST NOT begin (Task 1 STOP) until a recorded Legal sign-off exists — fabricating it is a governance-integrity failure (AD1/R2), not a passing story.

<!-- Created 2026-06-23 via bmad-correct-course (sprint-change-proposal-2026-06-23-story-8-5-legal-blocker-split.md). -->
<!-- 2026-06-23 bmad-create-story 8-6: file:line references re-verified at HEAD c7a79ac (governance YAML :52,:55,:56-61 and RetentionAndTenantDeletionConformanceTests lockstep literals confirmed); checklist validation passed with no critical misses. Status flipped backlog → ready-for-dev per stakeholder decision; the BLOCKED-PENDING-LEGAL guard is retained deliberately — do NOT implement until recorded Legal sign-off (AD1). -->
<!-- This story is the AC1 split-off from Story 8.5. Story 8.5 completed ACs 2–6 (residual-reds honest triage + fail-open mask removal + UI.E2E full-63 e2e-gates lane) and is `done`; its only remaining acceptance criterion — C3 Legal sign-off — was externally blocked on a Legal decision the dev team cannot resolve, so it was extracted here as a dedicated, owner-assigned (Legal via PM), turnkey successor rather than holding all of 8.5 and Epic 8 in `review` indefinitely. The complete, file:line-verified in-lockstep edit set was authored and validated during 8.5 (verified HEAD caa2603; C3 cascade re-confirmed byte-for-byte unchanged at the split, HEAD c7a79ac) and is reproduced verbatim below so this story is self-contained. NOTHING here is applied until a real recorded Legal sign-off exists (AD1) — fabricating the approval is a governance-integrity failure, not a passing story. -->

## Story

As a release stakeholder,
I want C3 retention to receive recorded Legal sign-off and the full in-lockstep C3 cascade applied in one commit,
So that the MVP release rests on a fully-approved governance posture and the `release_blocking_until_legal_approval` posture clears — closing the sole remaining MVP-release blocker.

## Context

This is the **AC1 split-off from Story 8.5** and the **sole remaining Epic-8 MVP-release blocker**. PM approval was recorded 2026-06-22 (Jerome). C3 stays `status: reference_pending` with a `release_blocking_until_legal_approval` posture; **Legal is the sole remaining governance gate** (`architecture.md:181,195,206`; readiness review Critical #1). Story 7.11 already wired the entire machine-validated retention cascade (the parseable per-class table, the gate `run-retention-deletion-gates.ps1`, the conformance suite, the release-package wiring, the D-7 commit-TTL / D-10 audit-projection inheritance) — so **only the approval record + status flip + their lockstep test/gate assertions remain**. The authoritative live status lives in `c0-c13-governance-evidence.yaml`, not the architecture prose table (`architecture.md:195`).

**This is a governance/verification change, not product-feature work.** No spine/OpenAPI/generated-client/aggregate/REST-CLI-MCP-behavior change; the per-class retention values, tenant-deletion dispositions, the `reference_pending_*` class identifiers, and the runtime `RetentionClassToken` TODO markers are all **kept** (AD3) — only the **approval state** flips.

## Acceptance Criteria

1. **C3 Legal sign-off (gated on the external Legal decision).** **Given** PM approval recorded 2026-06-22 and Legal sign-off evidence provided (the external gate — see **AD1**: the dev MUST NOT fabricate it), **when** the dev records the Legal approval and applies the C3 flip, **then** `c0-c13-governance-evidence.yaml` C3 `status` → `approved` with the `C3-legal-approval` placeholder removed, `c3-retention.md`'s machine block (`policy status` / `release posture` / `approval record`) and every required-class "Approval state" cell flip to approved, the `release_blocking_until_legal_approval` posture clears, `run-retention-deletion-gates.ps1` reports a non-blocking status (and `run-release-package-gates.ps1` no longer blocks live publish on `policy_status: reference_pending`), and **all four lockstep conformance assertions in `RetentionAndTenantDeletionConformanceTests` plus the pinned gate-script literals flip in the same commit** (no red contract-spine lane). The per-data-class retention values and tenant-deletion dispositions do **not** change. The runtime `RetentionClassToken` TODO markers are **kept** (AD3).

> **Scope guard:** introduces **no new product FR scope** and **no spine/OpenAPI/generated-client/aggregate/REST-CLI-MCP-behavior change** — only the governance/doc approval-state records, the gate-script literals, and the conformance assertions that gate them, all in one atomic commit.

## C3 in-lockstep edit set (verified file:line; the load-bearing change is the governance YAML — `architecture.md:195` makes it authoritative)

> Apply **all** of these in **one commit** (R1). The contract-spine PR-CI lane (`RetentionAndTenantDeletionConformanceTests`) turns red the instant the doc/gate/test disagree, so they are one atomic change.

- **Governance YAML** `docs/exit-criteria/c0-c13-governance-evidence.yaml`: `:52` `status: reference_pending`→`approved`; `:55` `result_summary` reword (drop "blocks live release publishing" in lockstep with the test at `RetentionAndTenantDeletionConformanceTests.cs:117`); `:56-61` remove the `open_policy_placeholders` block (`id: C3-legal-approval`).
- **C3 doc** `docs/exit-criteria/c3-retention.md`: `:3` status; `:30` `policy status: reference_pending`→approved; `:31` `release posture: release_blocking_until_legal_approval`→approved posture; `:32` `approval record …; Legal sign-off pending`→Legal recorded; "Approval state" cells in the prose table (rows `:17-26`) and machine table (rows `:40-49`, each currently `…; Legal approval pending`); narrative `:36,:67`. **Keep** the machine table's `Retention class identifier` column values (`reference_pending_audit_metadata`, …, `reference_pending_commit_idempotency_records`) UNCHANGED — only the Approval-state cells flip (AD3); renaming those identifiers re-triggers the OpenAPI/client/parity/UI cascade this story avoids and contradicts `retention-and-tenant-deletion.md:46`.
- **Gate script** `tests/tools/run-retention-deletion-gates.ps1` (this IS the gate — edit in lockstep or it goes red): `:261` (`policy status: reference_pending`), `:267` (`release posture: release_blocking_until_legal_approval`), `:288` (`Legal approval pending`), `:339-341` (`status: reference_pending` + `C3-legal-approval`), report literals `:387,:391,:392` (`status='release-blocked' PolicyStatus='reference_pending'`). Re-run → `_bmad-output/gates/retention-deletion/latest.json` reports non-blocking; confirm `run-release-package-gates.ps1:511-516` no longer blocks live publish.
- **Primary lockstep landmine** `tests/Hexalith.Folders.Contracts.Tests/Deployment/RetentionAndTenantDeletionConformanceTests.cs` (PR-CI via baseline allow-list `run-baseline-ci-gates.ps1:31`): `:40,:47,:48` (machine-block literals), `:66` (`Legal approval pending`), `:114,:117,:121-122` (governance status/summary/placeholder), `:210,:211` (report `policy_status`/`status`).
- **NFR traceability** `tests/Hexalith.Folders.Contracts.Tests/Deployment/NfrTraceabilityConformanceTests.cs:281-301` (`GovernanceEvidenceReferencePendingCriteriaStaySurfaced`) requires every YAML `reference_pending` criterion to be surfaced as a `reference-pending` row in the doc — so once C3 flips to approved in the YAML, the stale doc row `docs/exit-criteria/nfr-traceability.md:98` (NFR57 C3 reference-pending) + note `:162` must flip out of reference-pending **in the same commit** or this test fails.
- **Narrating docs** (consistency + one coupled conformance test): `docs/operations/retention-and-tenant-deletion.md:42,46`; `docs/operations/incident-alerting-and-recovery.md:115-116` (coupled — `OperationsAuditDocsConformanceTests.cs:207-209` requires this doc to contain `RetentionClassToken` AND `reference_pending`; since AD3 keeps the TODO tokens, this line can stay, but re-read both before editing); `docs/runbooks/tenant-deletion.md:29`; `docs/operations/audit-and-redaction.md:46-49`; `_bmad-output/planning-artifacts/architecture.md:181,195,206,595`.
- **No edit needed:** `tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs:103,110-126` already accepts `approved` with no placeholders.

## Tasks / Subtasks

- [ ] **Task 1 — Precondition: confirm the external Legal sign-off evidence is in hand (AC: 1) — 🚫 BLOCKED until Legal signs**
  - [ ] Confirm a **recorded** Legal sign-off (from Legal via PM Jerome) exists. **If not yet available, STOP and do NOT flip anything** — fabricating an approval violates the project's honest `reference_pending` discipline (AD1). This story stays `backlog`/blocked.
  - [ ] Re-confirm the C3 cascade files are still byte-for-byte at `reference_pending` at apply-time HEAD (`git diff` against the listed files) before editing, so the edit set still applies cleanly.
- [ ] **Task 2 — Apply the C3 flip in one atomic commit (AC: 1)**
  - [ ] Flip the authoritative governance YAML `c0-c13-governance-evidence.yaml` (`:52,:55,:56-61`) — **edit first** (AD4: it is the single source of truth).
  - [ ] Flip the C3 machine artifact `c3-retention.md` (`:3,:30,:31,:32`, approval-state cells `:17-26` + `:40-49`, narrative `:36,:67`). **Do NOT change** per-class retention values, tenant-deletion dispositions, or the `reference_pending_*` class identifiers (AD3).
  - [ ] Flip the gate-script literals `run-retention-deletion-gates.ps1:261,267,288,339-341,387,391,392`. Re-run → report non-blocking; confirm `run-release-package-gates.ps1` no longer blocks live publish.
  - [ ] Flip the four lockstep conformance assertions in `RetentionAndTenantDeletionConformanceTests.cs` (`:40,47,48,66,114,117,121-122,210,211`) **in the same commit**.
  - [ ] Flip the stale NFR57 C3 row `nfr-traceability.md:98` + pending note `:162` (verified against `NfrTraceabilityConformanceTests.cs:281-301`); update the narrating docs (`retention-and-tenant-deletion.md:42,46`, `runbooks/tenant-deletion.md:29`, `audit-and-redaction.md:46-49`) and `architecture.md:181,195,206,595`. Re-read `incident-alerting-and-recovery.md:115-116` against `OperationsAuditDocsConformanceTests.cs:207-209` before touching it (RetentionClassToken/reference_pending coupling, AD3).
  - [ ] **Keep** the runtime `RetentionClassToken = "TODO(reference-pending):…"` markers in `src/Hexalith.Folders/Queries/Audit/AuditTrailQueryHandler.cs:18` / `OperationTimelineQueryHandler.cs:18` (7.11 AC11 permits; AD3) — confines the change to governance/docs/lockstep and avoids the OpenAPI/generated-client/parity/UI regeneration cascade.
- [ ] **Task 3 — Validate & finalize (AC: 1)**
  - [ ] `run-retention-deletion-gates.ps1` reports non-blocking; `run-contract-spine-gates.ps1` + full `Contracts.Tests` (incl. flipped `RetentionAndTenantDeletionConformanceTests` + `NfrTraceabilityConformanceTests`) green; `run-governance-completeness-gates.ps1` green; `run-release-package-gates.ps1` no longer blocks live publish on `policy_status`.
  - [ ] `dotnet restore` + `dotnet build Hexalith.Folders.slnx` clean (0W/0E, warnings-as-errors); `dotnet format whitespace` + `analyzers` clean over src/tests/samples. No regressions elsewhere.
  - [ ] Update File List / Completion Notes / Change Log with the recorded Legal approval reference. Then move Story 8.6 → `done`, and **Epic 8 → `done`** (the last open Epic-8 story); clear the MVP-release blocker in the readiness tracking.

## Dev Notes

### Architectural Decisions (carried from Story 8.5)

- **AD1 — C3 AC1 is gated on an EXTERNAL Legal decision; never fabricate the approval.** The readiness review explicitly praised this project's "honest `reference_pending` discipline (named owners + release-blocking semantics, not fabricated approvals)". The deliverable is the *recording + cascade + lockstep flips*, applied **only once Legal sign-off evidence exists** (handoff: "Legal (via PM) signs off → flip C3 to approved", `sprint-change-proposal-2026-06-22.md:93`). Flipping C3 without a real Legal record is a governance-integrity failure, not a passing story.
- **AD3 — Keep the runtime `RetentionClassToken` TODO markers and the `reference_pending_*` class identifiers on the flip.** Story 7.11 AC11 permits keeping `TODO(reference-pending):…` markers even after approval. The runtime audit/timeline tests assert the token *symbolically*, so preserving the const value keeps them green. The C3 flip changes only the approval STATE, not the class IDENTIFIERS. Flipping the tokens/identifiers would cascade into OpenAPI + generated client + parity oracle + UI tests + contract fixtures — out of scope (breaches the no-spine/generated-client guard).
- **AD4 — The governance YAML is the authoritative live status; edit it first.** `architecture.md:195` pins `c0-c13-governance-evidence.yaml` as the single source of truth over the architecture prose table. The C3 doc, gate script, and conformance tests all derive from it. Edit the YAML status, then bring the human-readable artifact and every test/gate literal into lockstep in the same commit.
- **AD6 — Do not weaken any guard to make it pass.** Bring docs/gates/tests into lockstep with the *approved* state; never relax an assertion to dodge a red.

### Risks

- **R1 (highest) — C3 lockstep red.** Editing `c3-retention.md` / the governance YAML without flipping `run-retention-deletion-gates.ps1` (`:261,267,288,339-341,387-392`) and `RetentionAndTenantDeletionConformanceTests.cs` (`:40,47,48,66,114,117,121-122,210,211`) in the same commit turns the gate + the PR-CI contract-spine lane red. They are one atomic change.
- **R2 — fabricating Legal approval (AD1).** Flipping C3 before a real Legal record is a governance-integrity failure. Gate Task 1 on the external decision.
- **R6 — incident-alerting doc coupling (AD3).** `incident-alerting-and-recovery.md:115-116` is asserted to contain both `RetentionClassToken` and `reference_pending` (`OperationsAuditDocsConformanceTests.cs:207-209`). Since AD3 keeps the TODO tokens, this line can stay; re-read both before any edit.

### References

- [Source: _bmad-output/implementation-artifacts/8-5-close-c3-legal-signoff-and-residual-reds.md] — the predecessor story; its "C3 in-lockstep edit set" block + Dev Notes AD1/AD3/AD4 are the verified source of the edit set reproduced above; ACs 2–6 are `done` there.
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-06-23-story-8-5-legal-blocker-split.md] — the split that created this story.
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-06-22.md] — `:45` (C3 PM-approved/Legal-only), `:93` (handoff: Legal signs off → flip C3).
- [Source: _bmad-output/planning-artifacts/epics.md#Story-8.6] — authoritative AC.
- [Source: _bmad-output/implementation-artifacts/7-11-enforce-c3-retention-and-tenant-deletion-behavior.md] — the C3 cascade already wired; AC11 permits keeping the TODO markers (AD3).
- **C3 governance:** `docs/exit-criteria/c3-retention.md:3,30-32,17-26,40-49`; `docs/exit-criteria/c0-c13-governance-evidence.yaml:52,55,56-61`; `tests/tools/run-retention-deletion-gates.ps1:261,267,288,339-341,387-392`; `tests/Hexalith.Folders.Contracts.Tests/Deployment/RetentionAndTenantDeletionConformanceTests.cs:40,47,48,66,114,117,121-122,210,211`; `…/Deployment/NfrTraceabilityConformanceTests.cs:281-301` + `docs/exit-criteria/nfr-traceability.md:98,162`; `architecture.md:181,195,206,595`.

## Dev Agent Record

_(empty — story is blocked-pending-Legal; not yet started)_
