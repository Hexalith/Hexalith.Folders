# Sprint Change Proposal — Seed-Backed Ops-Console Diagnostics & Transition-Evidence Read Models

- **Date:** 2026-07-07
- **Author:** Amelia (Developer) via `bmad-correct-course`
- **Decision maker:** Jerome (approved 2026-07-07)
- **Trigger source:** `sprint-status.yaml` action item (Epic 8, line 405) — _"Decide whether seed-backed MVP diagnostics and transition-evidence read models become projection-backed production implementations or remain explicitly owned limitations."_
- **Scope classification:** Moderate (backlog/artifact reorganization; PO/DEV) — no code behavior change
- **Mode:** Batch

---

## Section 1 — Issue Summary

The MVP registers two read models as production defaults that are **seed-backed dev/test seams**, not projection-backed production implementations:

| Read model | Interface | Default registration | Backs |
|---|---|---|---|
| Ops-console diagnostics (7 views: readiness, lock, dirty-state, failed-operation, provider-status, sync-status, projection-freshness) | `IOpsConsoleDiagnosticsReadModel` | `TryAddSingleton<…, InMemoryOpsConsoleDiagnosticsReadModel>` (`FoldersServiceCollectionExtensions.cs:104`) | Epic 6/8 ops console + REST ops-console diagnostics routes |
| Workspace transition-evidence (C6 lifecycle) | `IWorkspaceTransitionEvidenceReadModel` | `TryAddSingleton<…, InMemoryWorkspaceTransitionEvidenceReadModel>` (`FoldersServiceCollectionExtensions.cs:86`) | Epic 4 lifecycle transition evidence; FR46 operational-evidence-after-failure |

**How the issue was discovered.** The item has been an open governance ledger question since Epic 2/4 code review (`deferred-work.md:66`, deferred to "Epic 7 production wiring" — Epic 7 closed without it). It was promoted to a formal decision after the parallel Memories query-facade read-model limitation was resolved earlier the same day (`sprint-change-proposal-2026-07-07-190839.md`).

**Runtime evidence (what the code actually does).**

- Both `InMemory*ReadModel` classes are populated only via their `Save(...)` overloads. A repo-wide search shows `Save(...)` on these two read models is called **only in test code** — never in a production or worker composition path.
- Because registration uses `TryAddSingleton` and no deployed host overrides it, a production host resolves the empty in-memory seam. Every query therefore returns a safe empty result:
  - `FolderScopedDiagnosticsQueryHandler` / `TenantScopedDiagnosticsQueryHandler` → `NotFoundSafe` (metadata-only, no leak).
  - `WorkspaceTransitionEvidenceQueryHandler` → `NotFoundSafe`.
  - `ReadModelUnavailable` is returned only if the read model *throws*; empty is modelled as safe not-found, which is correct fail-safe behavior.
- **Sharpening finding (materially narrows scope):** the in-memory event-replay projection that _does_ exist — `InMemoryFolderRepository` — feeds **six** other read models on append/seed (lifecycle status, branch/ref policy, workspace lock, workspace status, cleanup status, task status; `InMemoryFolderRepository.cs:18-85, 233-289`). It does **not** feed the two read models named in this trigger. Story 4.15's determinism AC (`4-15-…:35`) likewise enumerates those six-plus read models and **not** diagnostics or transition-evidence. So the two trigger read models have **no projection logic anywhere** — production or determinism-tested; they are pure seed seams.

**Documentation drift this exposes.**

- `architecture.md:155` states the MVP console "remains read-only and **projection-backed**." That is aspirational — the ops-console diagnostics read model is seed-backed and empty in production.
- The 2026-07-07 readiness report echoes "read-only, **projection-backed** console design" (line 409).
- The drift must be reconciled regardless of which branch is chosen.

---

## Section 2 — Impact Analysis

**Epic impact.**

- **Epic 6 (Read-Only Trust Console) / Epic 8 (ops-console diagnostics REST routes):** the console/REST diagnostics surface renders no live data in a deployed host. Architecturally read-model-based and safe, but empty. No code defect; a documented population gap.
- **Epic 4 (Workspace Task Lifecycle):** transition-evidence reads return safe not-found in production. FR46 ("explain final state, retry eligibility, and operational evidence after … failure") is satisfied for the error-taxonomy/explanation path but the transition-evidence _read model_ is unpopulated.
- **Epic 11 (Domain-Focus Refactor & Governance Closure):** Epic 11 is explicitly wire-preserving ("must not change product behavior"). Building live projections mid-Epic-11 would itself be a behavior change and is out of charter. Epic 11 **Story 11.10** ("Align Server and Workers with EventStore/Memories SDK seams") already owns wiring the EventStore-backed Memories bridge read model and replacing a fail-safe default — the natural home for this wiring commitment.

**Artifact conflicts.**

- `architecture.md` — "projection-backed" console wording (line 155); no limitation record for these two read models.
- `epics.md` — Epic 6 intro implies a populated console; Story 11.10 ACs do not yet cover ops-console/transition-evidence projections.
- `deferred-work.md` — the family entry (line 66) points at a closed Epic 7; needs a current owner story.
- `sprint-status.yaml` — action item 405 is open.
- `docs/exit-criteria/nfr-traceability.md` — NFR51/NFR52 rows (see NFR posture below).

**Technical impact.** None to shipped behavior. This proposal changes documentation, backlog ownership, and one Story's ACs. No source edit is part of this proposal.

**NFR posture (honest reconciliation).**

- **NFR51** ("Operations-console views must be read-model-based, read-only, limited to metadata") — remains **covered**. The console _is_ read-model-based and read-only; an empty read model does not violate the property.
- **NFR52** ("Rebuilding read-model views from an empty read model must produce deterministic results from the same ordered event stream") — remains **covered**. Determinism is proven by Story 4.15 for the six projection-backed read models; for the two seed-only read models the production result is deterministically empty (empty in → empty out). NFR52 is not violated.
- Neither NFR is flipped. `nfr-traceability.md` carries machine-checked fingerprints and a governance conformance gate, so this proposal does **not** edit those rows. The honest nuance (diagnostics/transition-evidence sit outside the Story 4.15 projection set and are unpopulated in production) is recorded in `architecture.md` and `deferred-work.md`, and flagged here as an **optional governance follow-up** (owner Murat) if the traceability doc should surface the deferral explicitly and in gate-lockstep.

---

## Section 3 — Recommended Approach

**Selected path: Direct Adjustment (Option 1) — record an explicitly owned MVP limitation, reconcile the documentation drift, and re-home the production-projection wiring to Epic 11 Story 11.10.**

This mirrors the "record-limitation branch" resolution taken the same day for the directly-parallel Memories query-facade bridge read model (`sprint-status.yaml:296`; `architecture.md` §"Query Facade (Story 10.5)"). Choosing the same branch keeps every "seed → EventStore-backed read model" decision consistent across the MVP.

**Rationale.**

- **Charter fit.** Epic 11 is wire-preserving; building live projections now would violate "no behavior change." The owned-limitation branch keeps MVP honest without re-opening scope.
- **Precedent + consistency.** Identical to the Memories-bridge resolution hours earlier; same owners (Amelia / Winston); same consuming story (11.10).
- **Provability.** The live `folders-index` and DCP round-trips are already BLOCKED-PENDING the DCP-capable AppHost lane; a production projection could not be proven live today regardless.
- **Cost/risk.** Building 8+ EventStore projections (7 diagnostics views + transition-evidence, from scratch — no existing projection logic to relocate) is high effort and high risk against an MVP whose release blockers lie elsewhere.

**Alternatives considered.**

- **Option 2 (Rollback):** N/A — nothing to revert; these seams are load-bearing for tests.
- **Option 3 / "build projections now":** Rejected for MVP — net-new behavior inside a wire-preserving epic, unprovable live, high effort. Deferred to Story 11.10 as the forward commitment.
- **Hybrid (transition-evidence now, diagnostics deferred):** Rejected — no projection logic exists for either, so "now" carries the same build cost; splitting the posture adds documentation surface for no MVP benefit.

**Effort / risk / timeline.** Documentation + backlog only: **Low** effort, **Low** risk, no MVP timeline impact. The deferred wiring inherits Story 11.10's existing effort and the standing DCP-lane blocker.

---

## Section 4 — Detailed Change Proposals

### 4.1 Architecture (`architecture.md`)

**Edit A — line 155, correct the "projection-backed" claim.**

> OLD: The MVP operations console remains read-only and projection-backed. Do not generate or route mutation command forms through FrontComposer during MVP.
>
> NEW: The MVP operations console remains read-only and read-model-based (projection-capable). Its ops-console diagnostics and workspace transition-evidence read models are seed-backed dev/test seams in MVP — see §"Ops Console & Transition-Evidence Read Models (MVP limitation)". Do not generate or route mutation command forms through FrontComposer during MVP.

**Edit B — new subsection after §"Query Facade (Story 10.5)"** (mirrors that section's deferral-note format): §"Ops Console & Transition-Evidence Read Models (MVP limitation)" — records the seed-backed default, the deployed safe-empty effect, the no-projection-yet finding, the NFR posture, and owner/consuming Story 11.10.

### 4.2 Epics (`epics.md`)

**Edit C — Epic 6 intro note** (after line 1363): explicit MVP limitation that the deployed ops-console diagnostics read model is seed-backed (safe-empty in production), production projection wiring owned by Story 11.10.

**Edit D — Epic 4 cross-reference** (transition-evidence): a one-line note that the workspace transition-evidence read model shares the same seed-backed MVP limitation and Story 11.10 ownership.

**Edit E — Story 11.10 ACs** (after line 2133): add an `**And**` clause requiring the ops-console diagnostics + workspace transition-evidence read models to be given EventStore-backed projections and wired in Server (replacing the seed-backed in-memory defaults), closing this deferral, with REST/console behavior preserved and live proof carried on the DCP lane or re-carried with evidence.

### 4.3 Deferred-work ledger (`deferred-work.md`)

**Edit F — new dated section** "Deferred from: bmad-correct-course seed-backed read-model decision (2026-07-07)": promotes the stale `:66` family entry, records that diagnostics + transition-evidence have **no** projection (need projection + wiring, unlike the six that need wiring only), names owner Amelia/Winston and consuming Story 11.10.

### 4.4 Sprint status (`sprint-status.yaml`)

**Edit G — action item (line 405) → `status: done`** with a resolution comment (branch chosen, artifacts updated, wiring re-homed to Story 11.10). Update `last_updated`.

### 4.5 NFR traceability (`docs/exit-criteria/nfr-traceability.md`)

**No edit** in this proposal (fingerprinted rows + governance gate). NFR51/NFR52 remain honestly covered per Section 2. Optional gate-lockstep follow-up flagged for Murat if explicit surfacing is desired.

---

## Section 5 — Implementation Handoff

- **Scope:** Moderate (backlog reorganization + artifact sync; no code).
- **Handoff:**
  - **Amelia (DEV):** apply Edits A–G (architecture, epics, deferred-work, sprint-status).
  - **Winston (Architect):** co-owner of the §limitation note and Story 11.10 AC wording; owns the eventual EventStore projection design.
  - **Murat (TEA):** optional NFR-traceability gate-lockstep follow-up if the deferral should be surfaced on NFR51/NFR52 rows.
  - **Story 11.10 (Epic 11, `backlog`):** carries the production-projection wiring for both read models as an added AC; inherits the DCP-lane live-proof blocker.
- **Success criteria:** documentation states the truth (seed-backed, safe-empty, projection-capable); the wiring commitment is owned by a named backlog story; action item 405 closed; no shipped behavior changed.
