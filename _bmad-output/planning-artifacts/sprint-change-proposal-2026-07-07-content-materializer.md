# Sprint Change Proposal — Real Content Materializer for Search-Index Population

- **Date:** 2026-07-07
- **Author / Facilitator:** Amelia (Developer) via `bmad-correct-course`
- **Requested by:** Jerome
- **Trigger directive:** *"Replace or extend the fail-closed content materializer so real folder mutation evidence can populate the search index under C4/C9 policy."*
- **Scope classification:** **Moderate** (backlog reorganization — one new story + sprint-status/planning-artifact updates; then story-context + implementation handoff)
- **Change mode:** Incremental

---

## Section 1 — Issue Summary

**Problem statement.** Folders' worker-side semantic-indexing pipeline (built in Epic 10) is complete end-to-end *except* for the one component that produces indexable content. The registered `ISemanticIndexingContentMaterializer` is `FailClosedSemanticIndexingContentMaterializer`, which **always** returns `Unavailable("content_materializer_unavailable", retryable: true)`. Consequently every authorized, policy-passing folder mutation dead-ends at materialization, is recorded as a retryable `Failed` bridge entry, and **the Memories search index is never populated with real content.** The live round-trip can only be *seeded through the worker port* in tests.

**Discovery / context.** This is not a newly-found defect. It was a **deliberate deferral** recorded as an open, high-priority Epic 10 retrospective action item (`sprint-status.yaml`):

> *epic: 10 — owner: Amelia / Murat — priority: high — status: open — "Replace or extend the fail-closed content materializer so real folder mutation evidence can populate the search index under C4/C9 policy."*

Jerome invoked correct-course to pull it into the active plan.

**Evidence.**
- `src/Hexalith.Folders.Workers/SemanticIndexing/FailClosedSemanticIndexingContentMaterializer.cs:12-14` — hard `Unavailable` return.
- `src/Hexalith.Folders.Workers/FoldersWorkersModule.cs:79` — the fail-closed type is the registered default.
- `src/Hexalith.Folders.Workers/SemanticIndexing/MemoriesSemanticIndexingPort.cs:42-43` — the `SearchIndexEntryChanged.Text/Attributes` are published **directly** from the materializer's `CuratedText`/`CuratedAttributes`; `ContentBytes` is used only for C4 size/type gating.
- `epic-10-retro-2026-06-27.md` §"Runtime population": *"Not complete for real folder mutation content materialization."*
- PRD **FR58** (`prd.md:686`) is the governing requirement.

---

## Section 2 — Impact Analysis

### Epic Impact
- **Epic 10** was marked `done`, but this FR58-supporting closure item was never delivered. **Reopen Epic 10** (mirrors the Epic 7 / Story 7.18 reopen precedent) and add **Story 10.6**. Stories 10.1–10.5 are unaffected.
- **Epic 11** (Domain-Focus Platform Refactoring; wire-preserving, non-product) is `in-progress`. **Story 11.1** (baseline + governance pin map) is **already `done`** — it captured the *fail-closed* behavior as the pinned baseline. **Story 11.10** ("Align Server and Workers with EventStore/Memories SDK seams") both rewrites the same Workers indexing code *and* is where the Server-side EventStore-backed bridge read model gets wired (`architecture.md` §Query-Facade). This creates a real **code-ownership overlap** between 10.6 and 11.10.

### Story Impact
- **New Story 10.6** — *Replace the fail-closed content materializer with a metadata-derived materializer under C4/C9.*
- **Workers tests** that pin current behavior update in lockstep: `SemanticIndexingWorkerRegistrationTests`, `SemanticIndexingProcessManagerTests`, `SemanticIndexingEndpointE2ETests` (`tests/Hexalith.Folders.Workers.Tests/`).

### Artifact Conflicts
| Artifact | Impact | Change |
| --- | --- | --- |
| PRD `prd.md` (FR58 scope note) | Over-claims "search the content" vs. what 10.6 ships | Scope note records two-increment delivery (metadata now / C9-gated content later) |
| Architecture `architecture.md` (Memories track) | Line 133 permits worker content-read; 10.6 defers it | New bullet records metadata-derived decision + C9-gated follow-up |
| Epics `epics.md` (Epic 10) | Missing Story 10.6 + reopen note | Added |
| Story 11.1 pin map artifact | Pinned fail-closed behavior as baseline | Appended §12 annotation recording the intentional pending delta |
| `sprint-status.yaml` | Epic 10 `done`; action item `open` | Epic 10 → `in-progress`; +10-6 `backlog`; action item → `in-progress` |
| UX spec | **None** — search results stay metadata-only; UX FR58 backend-discovery addendum already covers this. Recall quality is a backend concern. |

### Technical Impact
- **C4** (bounded input limits) — already enforced by the policy evaluator / process manager; unchanged. The materializer returns bytes (UTF-8 of the curated descriptor) for size/type gating.
- **C9** (sensitive-metadata classification) — the new materializer must build `CuratedText`/`CuratedAttributes` with **no raw path, body, snippet, or source URI**; asserted against a sensitive-path corpus.
- **Live proof** — the real `aspire run` 6-service round-trip remains **BLOCKED-PENDING the DCP-capable lane** (standing Epic 9/10 environment blocker). 10.6 proves the path at the worker/port boundary (unit + Tier-3 opt-in harness); the live leg carries the same blocker. *No new blocker introduced.*

---

## Section 3 — Recommended Approach

**Selected: Option 1 — Direct Adjustment (new Story 10.6 in a reopened Epic 10), Hybrid materialization strategy.**

- **Materialization strategy (Jerome's decision): Hybrid.** Ship a **metadata-derived** materializer now (curated text/attributes from mutation metadata evidence — zero content egress, fully unblocks a real mutation→index→search round-trip). Defer **authorized real-content materialization** to an explicit **C9-gated follow-up** (Security + PM sign-off) for full body-text recall.
- **Placement (Jerome's decision): Reopen Epic 10 → Story 10.6.**
- **Sequencing (Jerome's decision, re-anchored to ground truth): land 10.6 before Epic 11 Story 11.10**, and annotate Story 11.1's already-captured pin map with the pending delta so 11.10 preserves the *new* metadata-derived behavior rather than re-freezing the placeholder.

**Effort:** Medium · **Risk:** Low (metadata-derived = no content-exposure surface; C4/C9 gates already exist).

**Alternatives considered.**
- *Option 2 (Rollback)* — N/A; nothing to revert.
- *Option 3 (MVP/PRD review)* — N/A; FR58 stays. Only its scope note is clarified to reflect metadata-token recall now vs. body-text recall later.
- *Fold into Epic 11 Story 11.10* — rejected: Epic 11 is charter-bound to be non-product and wire-preserving; 10.6 is a product behavior change.
- *Authorized real-content now* — deferred: needs C9 content-exposure sign-off (Security + PM) before any file body may enter the CloudEvent, and adds a content-access path to the worker. Carried as the explicit follow-up.

---

## Section 4 — Detailed Change Proposals (all APPLIED)

**1. `epics.md`** — Epic 10 "Reopened 2026-07-07" note + new **Story 10.6** with 4 acceptance-criteria blocks (real materializer replaces fail-closed default; C9 sanitization + C4 gates + idempotency; 10.6-before-11.10 sequencing with lockstep test updates and the C9-gated content follow-up recorded).

**2. `sprint-status.yaml`** — `epic-10: done → in-progress` (with reopen comment); `+ 10-6-replace-fail-closed-content-materializer-with-metadata-derived: backlog`; materializer action item `status: open → in-progress` (now owned by 10.6); `last_updated` refreshed.

**3. `11-1-…-governance-pin-map.md`** — appended **§12 post-baseline annotation** recording the intentional worker-indexing behavior delta and the 10.6-before-11.10 constraint; §§1–11 left byte-intact.

**4. `prd.md`** — FR58 scope note expanded to describe two-increment delivery (metadata-derived now; C9-gated real-content later); FR58 statement itself unchanged.

**5. `architecture.md`** — new Memories-track bullet recording the metadata-derived materializer decision and deferral of the content-read path to a C9-gated follow-up; resolves ambiguity with the existing "workers may read file content" line.

---

## Section 5 — Implementation Handoff

**Scope: Moderate.** Planning changes above are **already applied** by this workflow. Remaining work is story-context + code implementation.

| Step | Owner | Action |
| --- | --- | --- |
| 1. Context Story 10.6 | Amelia (Dev) | `bmad-create-story 10.6` — classic flow (Epic 10 story; **not** the Epic 11 spec-kernel/bmad-loop pipeline). No `spec-10-6`/committed 10.6 deliverable exists, so no overwrite trap. |
| 2. Implement | Amelia (Dev) | `bmad-dev-story` — metadata-derived `ISemanticIndexingContentMaterializer`, register in `FoldersWorkersModule`, keep fail-closed as fallback; update the 3 Workers test classes in lockstep. |
| 3. Test evidence | Murat (Test) | C9 sensitive-path corpus assertion (no raw path/body/snippet/source URI), C4 size/type gate regression, idempotent/replay-stable CloudEvent id; Tier-3 opt-in harness path. |
| 4. Code review | Fresh reviewer | `bmad-code-review` before 10.6 → `done`. |

**Success criteria.** An authorized, policy-passing mutation yields `Available` curated text/attributes → a real `SearchIndexEntryChanged` into `folders-index`; C9 corpus test green; C4 gates green; Workers lanes green; live `aspire run` proof explicitly carried as the standing DCP blocker; **authorized real-content materialization recorded as a C9-gated follow-up (Security + PM sign-off)**.

**Sequencing guardrail.** Story 10.6 must reach `done` **before** Epic 11 Story 11.10 begins, or 11.10 must rebase on 10.6's Workers changes and preserve the new behavior. Story 11.1 §12 annotation records this.

**Related open items (NOT in this change; tracked separately).**
- Epic 10 action item — *"Wire the Server-side EventStore-backed semantic-indexing bridge read model…"* (architecture ties this to Story 11.10). 10.6 populates the index; that item makes the deployed Server facade actually read it. FR58 end-to-end needs both + the DCP live lane.
- Standing **DCP-capable `aspire run`** lane (Epics 9/10) — unblocks the live proof.
