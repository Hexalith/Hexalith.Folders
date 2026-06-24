---
baseline_commit: c7a79ac75a7801d8733fd5c495902f53939f597b
---

# Story 8.6: Record C3 Legal sign-off and apply the in-lockstep C3 retention cascade

Status: review — Legal sign-off **recorded 2026-06-24 (Jérôme Piquot, Louveciennes; PM Jerome 2026-06-22)**; the in-lockstep C3 cascade was applied and validated (Contracts.Tests 275/275, governance-completeness 11/11, retention gate non-blocking `status=passed`/`policy_status=approved`, `dotnet format` clean on the changed `.cs`). One scoped deviation from the documented edit set is recorded in the Dev Agent Record (NFR57 kept reference-pending to satisfy the contract-spine guard). Per the bmad-dev-story workflow the final 8-6 → `done` and the Epic-8 close-out are gated on code-review approval.

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

- [x] **Task 1 — Precondition: confirm the external Legal sign-off evidence is in hand (AC: 1) — ✅ Legal signed 2026-06-24**
  - [x] Confirmed a **recorded** Legal sign-off: **Jérôme Piquot, 2026-06-24, Louveciennes** (provided by PM Jerome via the dev-story session). Transcribed verbatim from the recorded approval — **not fabricated** (AD1/R2 satisfied). Verified the repo had **no** prior recorded approval before recording (exhaustive sweep: every `reference_pending` hit was a pending/missing reference).
  - [x] Re-confirmed the C3 cascade files were byte-for-byte at `reference_pending` at apply-time HEAD `e6120525` before editing (repo-wide lockstep-string sweep across docs/tests/src/architecture/.github/deploy).
- [x] **Task 2 — Apply the C3 flip in one atomic edit set (AC: 1)**
  - [x] Flipped the authoritative governance YAML `c0-c13-governance-evidence.yaml` C3 (`status` → `approved`; `result_summary` reworded to drop "blocks live release publishing" → "approved for live release publishing"; `open_policy_placeholders: []`) — **edited first** (AD4).
  - [x] Flipped the C3 machine artifact `c3-retention.md` (`status`, `policy status`, `release posture` → `approved_for_live_release`, `approval record`; **all 20** prose `:17-26` + machine `:40-49` Approval-state cells; narratives `:9,:36,:67`). Per-class retention values, tenant-deletion dispositions, and `reference_pending_*` class identifiers **UNCHANGED** (AD3).
  - [x] Flipped the gate-script literals `run-retention-deletion-gates.ps1` (policy-status `:261`, posture `:267` → `approved_for_live_release`, approval-state `:288` → `Legal approved`, governance-evidence expectations `:338,:341`, report emission `:387,:391,:392` → `status=passed`/`policy_status=approved`). Re-ran → `latest.json` non-blocking (exit 0); confirmed `run-release-package-gates.ps1` no longer blocks live publish (`policy_status=approved` skips the `:511` block; `:526` satisfied by `status=passed`).
  - [x] Flipped the four lockstep conformance assertions in `RetentionAndTenantDeletionConformanceTests.cs` (`:40,47,48,66,114,117,121-122,210,211`) **plus** the gate-vocabulary tokens in `RetentionDeletionGateScriptShouldFailClosedAndEmitBoundedEvidence` (`missing-release-blocking-posture`→`missing-approved-release-posture`; `release-blocked`→`passed`) — a necessary lockstep consequence the original edit list missed (caught by the red test, then fixed).
  - [x] Updated narrating docs (`retention-and-tenant-deletion.md:42`, `incident-alerting-and-recovery.md:115`, `runbooks/tenant-deletion.md:29`, `runbooks/retention.md:12,28`, `audit-and-redaction.md:46`) and `architecture.md:182,196,597`, each verified against its coupled gate/test (required strings preserved: `pwsh ./tests/tools/run-retention-deletion-gates.ps1`, `pending approval blocks live release` [ops-doc line 44, kept verbatim], `RetentionClassToken`, `reference_pending`). **DEVIATION:** `nfr-traceability.md` NFR57 (`:98`) + note (`:162`) were **NOT** flipped — see Dev Agent Record → Completion Notes.
  - [x] **Kept** the runtime `RetentionClassToken = "TODO(reference-pending):…"` markers in `AuditTrailQueryHandler.cs:18` / `OperationTimelineQueryHandler.cs:18` (7.11 AC11; AD3) — confines the change to governance/docs/lockstep and avoids the OpenAPI/generated-client/parity/UI regeneration cascade. No Dapr scope change.
- [x] **Task 3 — Validate & finalize (AC: 1)**
  - [x] `run-retention-deletion-gates.ps1` non-blocking (exit 0, `status=passed policy_status=approved`); full `Contracts.Tests` green incl. flipped `RetentionAndTenantDeletionConformanceTests` + `NfrTraceabilityConformanceTests` + `OperationsAuditDocsConformanceTests` (**275/275**); `run-governance-completeness-gates.ps1` green (**11/11**); `run-release-package-gates.ps1` no longer blocks live publish on `policy_status`.
  - [x] `dotnet format whitespace` + `analyzers` clean on the changed `.cs` (exit 0). Full-solution `dotnet build`/format **deferred**: the working tree carries unrelated concurrent Story 10.4/10.5 changes (Server/Workers/Projections/test projects + untracked ContextSearch); the only 8.6 `.cs` edit compiled and passed in the Contracts.Tests run.
  - [x] File List / Completion Notes / Change Log updated with the recorded Legal approval reference; Story 8.6 → `review`. **Per the bmad-dev-story workflow (Step 9), the final 8-6 → `done` and Epic 8 → `done` are gated on code-review approval** (intentionally NOT set here; the story's Task-3 "→ done/epic-8 done" wording predates the standardized dev→review→code-review→done flow).

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

### Legal approval reference (the recorded evidence that unblocked Task 1)

- **Criterion:** C3 — Retention policy evidence (per-data-class retention durations + tenant-deletion dispositions).
- **PM approval:** Jerome — 2026-06-22 (via bmad-correct-course Sprint Change Proposal).
- **Legal approval:** **Jérôme Piquot — 2026-06-24, Louveciennes** (recorded by the PM during the bmad-dev-story session). Transcribed verbatim; **not fabricated** (AD1/R2).

### Implementation Plan / Debug Log

1. Verified Task-1 precondition honestly: at HEAD `e6120525` C3 was still `reference_pending` with the `C3-legal-approval` placeholder, and a repo-wide search found **no** prior recorded Legal approval. Did **not** proceed until the recorded evidence was provided.
2. Read every lockstep enforcer before editing (the contract-spine conformance tests, the gate scripts, the package gate, and every narrating doc + its coupled test) and ran an exhaustive repo-wide sweep of all seven C3 lockstep strings to bound the edit set and separate it from the ~unrelated `reference_pending` usages (accessibility / capacity / observability / provider-drift), which were left untouched.
3. Applied the cascade governance-YAML-first (AD4), then c3-retention.md, gate script, conformance test, and the narrating docs/architecture.
4. Chose the approved-state report tokens by reading the package gate: `run-release-package-gates.ps1:519/:526` require the retention report `status` to be exactly `passed` once `policy_status≠reference_pending`, so the gate emits `status=passed` / `policy_status=approved`.
5. Re-ran `run-retention-deletion-gates.ps1` to regenerate the git-tracked `latest.json` with the approved tokens (exit 0).
6. First Deployment test run surfaced **one** red (`RetentionDeletionGateScriptShouldFailClosedAndEmitBoundedEvidence`) — a gate-vocabulary lockstep the story's edit list missed; fixed the two tokens and re-ran green.

### Completion Notes

- **AC1 satisfied.** Governance YAML C3 → `approved` (placeholder cleared); `c3-retention.md` machine block + all 20 Approval-state cells flipped; the gate is non-blocking (`status=passed`/`policy_status=approved`, exit 0); `run-release-package-gates.ps1` no longer blocks live publish; **all** lockstep conformance assertions flipped in the same working-tree change (no red contract-spine lane). Per-class retention values, tenant-deletion dispositions, and the `reference_pending_*` class identifiers + runtime `RetentionClassToken` TODO markers are **kept** (AD3). No spine/OpenAPI/generated-client/aggregate/REST-CLI-MCP-behavior change; no Dapr scope change.
- **Validation:** `Contracts.Tests` **275/275**; `run-governance-completeness-gates.ps1` **11/11**; `run-retention-deletion-gates.ps1` exit 0 non-blocking; `dotnet format whitespace`+`analyzers` clean on the changed `.cs`.
- **DEVIATION 1 (NFR57 NOT flipped — corrects a faulty premise in the story's edit set).** Task 2 instructed flipping `nfr-traceability.md:98` (NFR57, C3) out of reference-pending, citing `NfrTraceabilityConformanceTests.GovernanceEvidenceReferencePendingCriteriaStaySurfaced` (`:281-301`). That test only requires governance-`reference_pending` criteria to be **surfaced** (forward implication) — flipping C3 to approved removes that obligation but does **not** forbid the row; it does not force the edit. The actually-binding test is `ReferencePendingRowsAreOwnedAndSurfaceKnownGaps` (`:202`), which **hard-pins** `C3, C4, C7, C12` as surfaced reference-pending gaps. NFR57 is the only reference-pending row citing `C3`, so flipping it would red the contract-spine lane and **violate AC1's "no red contract-spine lane".** Precedent confirms this is intentional: **C4 is already `approved` in governance yet deliberately kept reference-pending here.** Net: `nfr-traceability.md` left unchanged; the NFR suite is green. This deviation **strengthens** AC1 compliance rather than relaxing a guard (AD6).
- **DEVIATION 2 (in-scope lockstep the edit list missed).** Flipping the gate's posture/status literals required updating two tokens in `RetentionDeletionGateScriptShouldFailClosedAndEmitBoundedEvidence` (`missing-release-blocking-posture`→`missing-approved-release-posture`; `release-blocked`→`passed`) — caught by the red test, fixed, re-verified green. No guard weakened (AD6).
- **Atomic-commit note (R1).** All cascade edits are in the working tree together (no partial/red state). The tree **also** carries unrelated concurrent Story 10.4/10.5 work and a concurrent `sprint-status.yaml` 10.4 bump — these must be **excluded** from the atomic C3 commit. The C3-only file set is in the File List below.
- **Close-out gating.** Story status set to `review` (bmad-dev-story Step 9). The final `8-6 → done` + `epic-8 → done` + MVP-blocker clear happen after code-review approval (recommended: a different LLM).

### File List (the atomic C3 cascade — commit these together, excluding concurrent 10.x work)

- `docs/exit-criteria/c0-c13-governance-evidence.yaml` (C3 → approved; placeholder cleared) — authoritative source (AD4)
- `docs/exit-criteria/c3-retention.md` (status/posture/approval-record/cells/narratives; identifiers kept per AD3)
- `tests/tools/run-retention-deletion-gates.ps1` (gate literals + report emission → passed/approved)
- `tests/Hexalith.Folders.Contracts.Tests/Deployment/RetentionAndTenantDeletionConformanceTests.cs` (lockstep assertions + gate-vocabulary tokens)
- `docs/operations/retention-and-tenant-deletion.md`
- `docs/operations/incident-alerting-and-recovery.md`
- `docs/operations/audit-and-redaction.md`
- `docs/runbooks/tenant-deletion.md`
- `docs/runbooks/retention.md`
- `_bmad-output/planning-artifacts/architecture.md` (C3 status sites :182, :196, :597)
- `_bmad-output/gates/retention-deletion/latest.json` (regenerated, git-tracked)
- `_bmad-output/implementation-artifacts/8-6-record-c3-legal-signoff-and-apply-cascade.md` (this story)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (8-6 → review; carries a concurrent 10.4 bump — see note)

### Change Log

| Date | Change |
|---|---|
| 2026-06-24 | Recorded C3 Legal sign-off (Jérôme Piquot, 2026-06-24, Louveciennes; PM Jerome 2026-06-22) and applied the in-lockstep C3 retention cascade: governance YAML C3 → approved (placeholder cleared), c3-retention.md + gate + conformance + narrating docs + architecture flipped; retention gate now non-blocking (status=passed/policy_status=approved). NFR57 deliberately kept reference-pending (contract-spine guard, DEVIATION 1). Validated: Contracts.Tests 275/275, governance-completeness 11/11, format clean. Status → review (final done/epic-8 close gated on code-review). |
