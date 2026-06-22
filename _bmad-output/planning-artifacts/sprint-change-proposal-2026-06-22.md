---
workflow: 'bmad-correct-course'
date: '2026-06-22'
project_name: 'Hexalith.Folders'
user_name: 'Jerome'
trigger: 'implementation-readiness-report-2026-06-22.md (retrospective MVP-release gate)'
mode: 'incremental'
scope_classification: 'Moderate (backlog reorg + governance sign-off; one Major sub-item: Epic 5 REST parity)'
status: 'applied — pending final approval'
---

# Sprint Change Proposal — 2026-06-22

**Project:** Hexalith.Folders · **Author:** Jerome (PM) with Claude (Developer, correct-course) · **Mode:** Incremental

## Section 1 — Issue Summary

The 2026-06-22 implementation-readiness review (`implementation-readiness-report-2026-06-22.md`) ran retrospectively over already-implemented Epics 1–7 and concluded: **planning artifacts are READY** (57/57 FR coverage, 70/70 NFR, clean cross-epic independence, zero critical structural defects across 88 stories), but **MVP *release acceptance* is NEEDS-WORK** on a bounded set of completeness + governance conditions — none of which are specification defects.

This course correction addresses all 10 findings (4 critical, 3 major, 3 minor classes). During analysis, **two of the report's own claims were found inaccurate and have been corrected** (see Section 2): Story 7.18 was reported "open" but is in fact resolved/green, and the Epic-5 parity counts were materially wrong (28/47 → verified **32/47**).

## Section 2 — Impact Analysis

### 2.1 Corrections to the readiness report (discovered during analysis)

1. **Story 7.18 is resolved, not open.** The report (Critical #3) cited *project memory* — a stale snapshot. The 7.18 Dev Agent Record records a green run (`Server.Tests` 434/0/0, `IntegrationTests` 592/0, `Folders.Tests` 1314/0). The only live residue was a missed close-out: `sprint-status.yaml` still read `epic-7: in-progress`. Corrected to `done`.
2. **Epic-5 parity counts were wrong.** An adversarial verification workflow (enumerating OpenAPI spine + parity oracle + server routes + CLI/MCP adapters; refutation-tested, high confidence) found **REST 32/47** (not 28), **15 missing** (not 19). The **audit family and several status queries are fully implemented on all four surfaces** — the report's "missing" claim was a false negative. The report also *missed* `GetProviderBinding`/`GetRepositoryBinding`/`GetWorkspaceRetryEligibility`/`GetWorkspaceTransitionEvidence`, and there are **7** diagnostics ops, not 6.

### 2.2 Epic impact

- **Epics 1–6:** `done`, unaffected.
- **Epic 7:** close-out completed (`in-progress` → `done`); 7.18 AC3 corrected to measured counts; CI submodule-posture guardrail pinned.
- **New Epic 8 — MVP Release Acceptance Closure** (`backlog`): owns the genuine remaining work so Epics 1–7 stay closed. 5 stories (8-1…8-5).

### 2.3 Story impact

- **New:** 8-1 (Bucket-A REST routes ×8), 8-2 (diagnostics REST routes ×7), 8-3 (wire-exercise parity + claim gate), 8-4 (axe/WCAG CI gate), 8-5 (C3 Legal sign-off + residual reds).
- **AC clarifications (completed stories):** 4.2 AC4, 4.7 AC2, 7.3 (+AC9), 7.18 AC3 — tightened to verified, code-accurate predicates.
- **Backlog reconciliation:** 2.8b enumerated in `epics.md`; `storyCount` 86 → 93; `epicCount` 7 → 8; sprint-status typo fixed.

### 2.4 Artifact conflicts

- **PRD:** no conflict — MVP scope unchanged.
- **Architecture:** stale C1–C5 "TBD" exit-criteria text reconciled to live governance status; Spine-Authoring checklist boxes updated.
- **Governance evidence:** C4 → `approved`; C3 → PM-approved (Legal sole remaining gate).
- **UX:** no scope creep; the axe/WCAG CI gate is the one genuine verification gap (now Story 8-4).
- **project-context.md:** new standing "no-gateway-mock integration test" testing rule.

### 2.5 Technical impact

- The parity gap is a **REST server-route gap** (spine + SDK + CLI + MCP are ahead of the server); CLI/MCP calls to the 15 ops would 404 at runtime — a **latent correctness gap**, now scheduled (8-1/8-2).
- No rollback required; no production behavior changed by this proposal (artifact/governance/tracking edits + backlog stubs only).

## Section 3 — Recommended Approach

**Selected: Hybrid — Direct Adjustment + targeted new stories.** Rationale:

- **Option 1 (Direct Adjustment)** — viable for governance sign-off, documentation reconciliation, AC tightening, standing rule (effort Med / risk Low). Applied now.
- **Option 2 (Rollback)** — not viable / unnecessary; nothing to revert, no failed approach.
- **Option 3 (MVP Review / scope reduction)** — not needed; scope is sound. This is completeness + governance, not redefinition.

New completeness work (REST parity, a11y gate, C3 Legal, residual reds) is bounded and isolated into **Epic 8** so the historical epics retain honest `done` status. **Effort:** Epic 8 is the only substantial engineering (15 server routes + wire-exercise + a11y gate); the rest is governance/docs. **Risk:** Low — routes are spec-led (spine already declares them). **Timeline:** governance + docs immediate; Epic 8 is a normal sprint of closure work.

## Section 4 — Detailed Change Proposals (all APPLIED)

### Governance (apply now)
- **EP-1 — PM sign-off on C3/C4.** `c4-input-limits.md` → `status: approved` (PM Jerome 2026-06-22; 9 row cells + closing line updated). `c3-retention.md` → `pending Legal approval (PM approved 2026-06-22)`; machine block `release_blocking_until_legal_approval`; row cells updated. `c0-c13-governance-evidence.yaml` → C4 `approved` (placeholder cleared); C3 placeholder narrowed `C3-legal-pm-approval` → `C3-legal-approval` (owner Legal).

### Backlog (stubs now, Dev later)
- **EP-2 — Epic 8 created.** 5 story stub files `8-1…8-5-*.md`; `epics.md` Epic List entry + full breakdown; `epicCount` 7→8, `storyCount` 88→93; `sprint-status.yaml` `epic-8: backlog` + 5 stories + retro; report parity-correction addendum; `readiness-2026-06-22-epic5-parity-gap` memory + `MEMORY.md` corrected to 32/47.

### Tracking (apply now)
- **EP-3 — 7.18 close-out + correction.** `sprint-status.yaml` `epic-7` → `done` (+ refreshed comment); readiness-report correction addendum; `epic7-done-mvp-release-readiness` memory + `MEMORY.md` index updated (7.18 resolved; Epic 8 noted; C4 approved / C3 Legal-only).

### Policy (apply now)
- **EP-4 — Standing rule.** `project-context.md` Testing Rules: every mutation-command story MUST include an in-process integration test that does NOT mock `IEventStoreGatewayClient` (the 2.8/2.8b "green tests, broken wiring" trap); complemented by the central `ValidateOnBuild` composition smoke (7.18).

### Story-AC precision (apply now)
- **EP-5 — Tighten 4 ACs to verified behavior.** 4.2 AC4 (discriminator = provider failure category: indeterminate → `unknown_provider_outcome`; known-divergent → `reconciliation_required`); 4.7 AC2 (**fail-closed** `validation_failed`, corrected field list — `inlineContent`/`streamDescriptor` don't exist); 7.3 +AC9 (coarse pass/fail contract; per-mode publish failures = explicit MVP non-goal); 7.18 AC3 (measured 592/0/0 · 1314/0/0; provider-boundary reds are a distinct non-composition cause → 8-5).

### Docs (apply now)
- **EP-6 — Doc currency.** `epics.md`: `storyCount` 86→88 (later 93 via EP-2), Story 2.8b enumerated, as-built AC-drift note. `sprint-status.yaml`: `2-8-…-npxpreservation` typo → `preservation`.
- **EP-7 — Architecture exit-criteria reconciliation.** C1–C5 "TBD" → live status; governance-YAML-is-authoritative note; Spine-Authoring checklist C3/C4 boxes checked with honest annotations.
- **EP-8 — Minor hygiene.** Epic 7 submodule-posture guardrail pinned. Recorded as no-action retro lessons: oversized-but-completed stories, 6.8 non-BDD ACs, hedged 5.3 AC3.

## Section 5 — Implementation Handoff

**Scope classification: Moderate** (backlog reorganization + governance sign-off; one Major sub-item — Epic 5 REST parity — needs PM/Architect scope confirmation, captured in Epic 8).

| Recipient | Responsibility |
|---|---|
| **Product Owner / Dev** | Run `create-story` refinement on the 8-1…8-5 stubs, then implement. 8-1/8-2 (15 REST routes) → 8-3 (wire-exercise + claim gate) → 8-4 (axe gate) ‖ 8-5. |
| **Legal** (via PM) | Sign off C3 retention values → flip C3 to `approved` (8-5 AC1). Sole remaining governance gate. |
| **PM (Jerome)** | Owns the four-surface-parity public claim; assert only after 8-3 passes (47/47 wire-exercised). |
| **Architect** | Confirm Epic 8 REST-route scope vs spine; review 503→403 / 409 parity-evidence fixes (8-3). |

**Release-gating status after this proposal:**
- ✅ C4 input limits — approved.
- ✅ Story 7.18 / epic-7 — closed green.
- ⏳ C3 retention — PM-approved; **Legal sign-off** outstanding (8-5).
- ⏳ Epic 5 four-surface parity — **15 REST routes** + wire-exercise outstanding (8-1/8-2/8-3).
- ⏳ Accessibility — **axe/WCAG CI gate** outstanding (8-4).
- ⏳ Honest-green baseline — residual non-composition reds outstanding (8-5).

**Success criteria:** Epic 8 stories reach `done`; REST 47/47 with wire-exercised parity; axe gate green in CI; C3 `approved`; `dotnet test Hexalith.Folders.slnx` honestly green. On all five, MVP release acceptance is unblocked.

---

## Appendix A — Change Navigation Checklist

| § | Item | Status |
|---|---|---|
| 1 | Trigger & context (readiness report; misunderstanding/completeness categories; evidence cross-checked) | ✅ Done |
| 2 | Epic impact (Epic 7 close-out; new Epic 8) | ✅ Done |
| 3 | Artifact conflicts (architecture TBD, governance, docs, project-context) | ✅ Done |
| 4 | Path forward (Hybrid; rollback/MVP-review rejected) | ✅ Done |
| 5 | Proposal components (issue, impact, approach, plan, handoff) | ✅ Done |
| 6 | Final review + sprint-status update (epic-7 done; epic-8 backlog +5 stories) | ✅ Done |

## Appendix B — Verification evidence

- **Parity (workflow `verify-epic5-parity-gap`, 2026-06-22):** 6 agents, adversarially verified, high confidence. Canonical 47; REST 32, SDK 47, MCP 47, CLI 40 (diagnostics MCP-only). 15 missing server routes (Bucket A ×8, Bucket B ×7). No canonical op stubbed.
- **Shipped-behavior agent (2026-06-22):** 4.2 discriminator = provider failure category (`WorkspacePreparationService.cs:258-262`); 4.7 fail-closed `ValidationFailed` (`FolderCommandValidator.cs:601-606`); 7.3 publish-failure modes genuinely unspecified (coarse exit-code gate only).
