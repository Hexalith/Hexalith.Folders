---
project_name: Hexalith.Folders
workflow: bmad-correct-course
source_change_trigger: "Epic 10 open action item (Amelia/Winston, high): Wire the Server-side EventStore-backed semantic-indexing bridge read model for the query facade and indexing-status paths, or record an explicit release limitation."
date: 2026-07-07T19:08:39+02:00
mode: batch
status: proposed
scope_classification: moderate
prepared_for: Jerome
decision: record-explicit-release-limitation + bind-wiring-to-Story-11.10
---

# Sprint Change Proposal — Epic 10 Server Bridge-Read Deferral: Record Limitation + Bind to Story 11.10

## 1. Issue Summary

Epic 10 (Folders worker-side semantic-indexing producer + Folders-owned bridge projection + authorized query facade) is `done` with its retrospective complete. One high-priority action item remained open:

> **Epic 10 (Amelia/Winston, high):** "Wire the Server-side EventStore-backed semantic-indexing bridge read model for the query facade and indexing-status paths, or record an explicit release limitation."
> **Success criterion:** "The Server facade no longer depends on the fail-safe unavailable bridge for live search/status, *or the limitation is visible in release readiness.*"

This is the loose end of a decision Jerome already made during the Story 10.5 code review on 2026-06-24: presented "wire now (Option B) vs accept the deferral (Option A)", he accepted the deferral as a tracked follow-up. This proposal closes the loop by taking the **record-an-explicit-release-limitation** branch and re-homing the actual wiring to Epic 11 Story 11.10.

### Evidence (verified at HEAD `40cc5e1`, working tree clean)

Code-level trace of the deployed `Hexalith.Folders.Server`:

- **The real read model exists but is not in the Server.** `EventStoreSemanticIndexingBridgeStore` (the EventStore/`IReadModelStore`-backed, event-replayed implementation of `ISemanticIndexingBridgeReadModel`) lives in `Hexalith.Folders.Workers` (`src/Hexalith.Folders.Workers/SemanticIndexing/EventStoreSemanticIndexingBridgeStore.cs`). The Server does **not** reference the Workers project.
- **The Server injects the fail-safe default.** `AddFoldersContextSearchFacade` registers the live `MemoriesFolderSearchSource` but does **not** register any EventStore-backed bridge read model; the core default `UnavailableSemanticIndexingBridgeReadModel` (`IsAvailable => false`) stays in place. The gap is stated verbatim in the code: `FoldersServerServiceCollectionExtensions.cs:84-86` ("stays the fail-safe `Unavailable` default until a Server-side EventStore-backed read model is wired on a DCP-capable lane").
- **Deployed effect (real, fail-safe, no leak):**
  - Context-search → live Memories source returns candidates, every candidate fails hydration through the `Unavailable` read model and is dropped → response is `Allowed` with **zero items**.
  - Indexing-status → `IsAvailable == false` → returns **`ReadModelUnavailable`** (honest; the Story 10.5 review already hardened this to "visible as unavailable, not healthy empty").
- **Verification is independently blocked.** The live `folders-index` round-trip inherits the Epic 9 DCP-capable-`aspire run` boot blocker (a separate open high-priority action item). A freshly wired read model would be inert-but-unprovable today.
- **Real data is also gated elsewhere.** Workers still register `FailClosedSemanticIndexingContentMaterializer`; real folder-mutation→index population is a separate open high-priority action item.
- **The relocation is already scheduled.** Epic 11 Story 11.10 ("Align Server and Workers with EventStore/Memories SDK seams", status `backlog`) is exactly where the Server/Workers seam realignment and shared Memories publication/search wrappers land; `epic-11-context.md` states stories 11.8–11.12 must not relocate/delete local implementations until Story 11.2 pins the shared APIs.

Sources: `epic-10-retro-2026-06-27.md` (lines 63, 89, 105, 121, 129), `10-5-expose-authorized-folders-query-facade-over-memories.md` (lines 256, 362, 391), `architecture.md` §"Query Facade (Story 10.5)", `sprint-status.yaml` action items, `epic-11-context.md`.

## 2. Impact Analysis

### Epic Impact

- **Epic 10** (`done`) — not reopened. Its remaining scope was always "release-readiness evidence and follow-up closure captured by sprint action items," and this is one of those items being closed by decision rather than by code.
- **Epic 11** (`backlog`) — Story 11.10 gains one explicit acceptance criterion (the Server-side bridge-read wiring), which was previously implied by "Memories publication/search wrappers are shared" but not spelled out. No new epic or story is created.
- **Epics 1–9** — no impact.

### Story Impact

- **Story 11.10** — one AC added binding the wiring, cross-referencing the recorded limitation. No renumbering, no split.
- **Stories 10.1–10.5** — unchanged; already `done`.

### Artifact Conflicts

| Artifact | Change |
| --- | --- |
| `architecture.md` | Add the explicit deployed-Server limitation to §"Query Facade (Story 10.5)" (durable record); cross-reference it from the Memories-integration release-readiness bullet. |
| `epics.md` | Add the wiring AC to Story 11.10; add a one-line "known release limitation" pointer to the Epic 10 section note. |
| `sprint-status.yaml` | Resolve the Epic-10 action item: `status: open` → `done`, record the record-limitation branch, re-home wiring to Story 11.10. |
| `prd.md` | No change. FR58 stays a current PRD requirement; the limitation is an implementation/release-readiness fact, not a scope change. |
| `ux-design-specification.md` | No change. The console already renders indexing-status as an explicit "unavailable" state (UX-DR20). |
| OpenAPI / SDK / Client / CLI / MCP / fixtures | No change. No wire-contract, parity-oracle, or generated-artifact impact. |

### Technical Impact

- **No code changes** in this proposal. The facade already fails closed correctly; recording the limitation does not alter runtime behavior.
- No OpenAPI, generated client, aggregate, server, worker, CLI, MCP, or UI behavior change. No submodule change.
- The only downstream code work — relocating `EventStoreSemanticIndexingBridgeStore` and registering it in the Server — is deferred into Story 11.10 on the DCP-capable lane.

## 3. Recommended Approach

**Direct Adjustment** — record the explicit release limitation in durable planning/readiness artifacts and bind the deferred wiring to the story that already owns the seam realignment (Story 11.10). Do not roll back; do not create a new epic; do not wire prematurely ahead of Story 11.2's shared-API pinning and the DCP lane.

- **Effort:** Low (documentation + one action-item resolution).
- **Risk:** Low. The facade is already fail-safe and leak-free; this makes the deferral honest and traceable. The only residual risk is traceability drift if the limitation is recorded in one artifact but not the others — mitigated by editing architecture, epics, and sprint-status in lockstep.
- **Timeline:** One documentation task; readiness can be rerun immediately.

**Alternatives considered:**
- *Wire now (Option B):* rejected — unverifiable on the current (non-DCP) lane, would be reworked by Story 11.10, and requires relocating a Workers-only class ahead of the 11.2 shared-API pinning that Epic 11 sequencing forbids.
- *Rollback:* N/A — nothing to revert; the deferral was an accepted design outcome.
- *PRD MVP review:* N/A — FR58 product scope is unchanged; this is Phase-2 release-readiness evidence, not MVP scope.

## 4. Detailed Change Proposals (Batch)

### Edit A — `architecture.md` §"Query Facade (Story 10.5)": add the explicit limitation

Insert a new bullet immediately after the existing **Live verification** bullet (currently the last bullet of the section).

NEW (inserted bullet):

```markdown
- **Deployed-Server limitation (release readiness — reference-pending).** The *core* facade hydrates from `ISemanticIndexingBridgeReadModel`, but the deployed `Hexalith.Folders.Server` composition does **not** register an EventStore-backed bridge read model: `AddFoldersContextSearchFacade` leaves the fail-safe `UnavailableSemanticIndexingBridgeReadModel` default in place (`FoldersServerServiceCollectionExtensions.cs:84-86`), because the concrete `EventStoreSemanticIndexingBridgeStore` currently lives in `Hexalith.Folders.Workers` (which the Server does not reference) and the live `folders-index` round-trip cannot be proven off a DCP-capable lane. **Deployed effect:** context-search returns `Allowed` with **zero items** (every candidate fails hydration and is dropped — fail-safe, no leak) and indexing-status returns `ReadModelUnavailable` (honest; the read model fails closed and never mis-states data). This is a documented deferral, not a defect. Owner **Amelia / Winston**; consuming story **Epic 11 Story 11.10** (relocate `EventStoreSemanticIndexingBridgeStore` into a Server-referenceable project and register it in `AddFoldersContextSearchFacade`, replacing the `Unavailable` default, then prove the live search/status round-trip on the DCP lane). Accepted by Jerome on 2026-06-24 (Story 10.5 review) and re-affirmed 2026-07-07 (bmad-correct-course).
```

Rationale: Makes the deployed reality — not just the intended design — explicit and release-readiness-visible, with the repo's canonical bounded-limitation fields (owner, reason, consuming story). Architecture.md is a primary input to the implementation-readiness assessment, so a rerun will surface this.

### Edit B — `architecture.md` Memories-integration release-readiness bullet: cross-reference

OLD (lines 140–144):

```markdown
- Epic 10 implemented the worker-side producer, removal/archive bridge, and Folders-owned read facade
  that activate the Epic 9 `hexalith-folders -> folders-index` routing. Release readiness still
  requires live DCP-capable AppHost evidence for topology boot, index auto-provisioning,
  `SearchIndexEntryChanged`/`SearchIndexEntryRemoved` publication, archive filtering, and query
  facade hydration.
```

NEW:

```markdown
- Epic 10 implemented the worker-side producer, removal/archive bridge, and Folders-owned read facade
  that activate the Epic 9 `hexalith-folders -> folders-index` routing. Release readiness still
  requires live DCP-capable AppHost evidence for topology boot, index auto-provisioning,
  `SearchIndexEntryChanged`/`SearchIndexEntryRemoved` publication, archive filtering, and query
  facade hydration. It also carries one explicit release limitation: the deployed Server facade
  runs on the fail-safe `Unavailable` bridge read model (context-search returns zero items;
  indexing-status returns `ReadModelUnavailable`) until the Server-side EventStore-backed read model
  is wired under Epic 11 Story 11.10 — see §"Query Facade (Story 10.5)".
```

Rationale: Surfaces the limitation at the integration-summary altitude and links it to the detailed record and the consuming story.

### Edit C — `epics.md` Story 11.10: add the wiring acceptance criterion

OLD (lines 2103–2104):

```markdown
**Then** authorization moves into `IDomainServiceAdmissionStage` or equivalent, `FoldersDomainServiceRequestHandler` is deleted where safe, `MapEventStoreDomainEvents` replaces local mapping, and Memories publication/search wrappers are shared
**And** REST parity and worker semantic-indexing behavior remain unchanged.
```

NEW:

```markdown
**Then** authorization moves into `IDomainServiceAdmissionStage` or equivalent, `FoldersDomainServiceRequestHandler` is deleted where safe, `MapEventStoreDomainEvents` replaces local mapping, and Memories publication/search wrappers are shared
**And** the Server-side EventStore-backed semantic-indexing bridge read model is wired for the query facade and indexing-status paths — `EventStoreSemanticIndexingBridgeStore` is relocated from `Hexalith.Folders.Workers` into a Server-referenceable project and registered in `AddFoldersContextSearchFacade` (replacing the fail-safe `UnavailableSemanticIndexingBridgeReadModel` default), closing the Epic 10 deferral recorded in `architecture.md` §"Query Facade (Story 10.5)", and the live `folders-index` search/status round-trip is proven on the DCP-capable lane (or the residual is re-carried with evidence)
**And** REST parity and worker semantic-indexing behavior remain unchanged.
```

Rationale: Turns the passive Epic 10 action item into an owned, scoped acceptance criterion in the story that already owns the Server/Workers seam realignment — the "bind to 11.10" half of the decision.

### Edit D — `epics.md` Epic 10 section note: add the known-limitation pointer

OLD (line 1904):

```markdown
_Phase 2 capability track for PRD FR58. Epic 9 is complete; C4 and C9 are approved policy constraints. Stories 10.1-10.5 have implementation story files and are marked done in sprint status; remaining Epic 10 work is release-readiness evidence and follow-up closure captured by sprint action items, not seed-level story discovery. Architecture inputs: `architecture.md` Memories integration track._
```

NEW:

```markdown
_Phase 2 capability track for PRD FR58. Epic 9 is complete; C4 and C9 are approved policy constraints. Stories 10.1-10.5 have implementation story files and are marked done in sprint status; remaining Epic 10 work is release-readiness evidence and follow-up closure captured by sprint action items, not seed-level story discovery. Architecture inputs: `architecture.md` Memories integration track. **Known release limitation:** the deployed Server query facade runs on the fail-safe `Unavailable` bridge read model (context-search returns zero items; indexing-status returns `ReadModelUnavailable`) until the Server-side EventStore-backed read model is wired under Epic 11 Story 11.10; recorded in `architecture.md` §"Query Facade (Story 10.5)"._
```

Rationale: Keeps the limitation visible in the other primary readiness input (epics.md) alongside the existing Epic 10 status note.

### Edit E — `sprint-status.yaml`: resolve the Epic-10 action item

OLD (lines 278–282):

```yaml
  - epic: 10
    action: "Wire the Server-side EventStore-backed semantic-indexing bridge read model for the query facade and indexing-status paths, or record an explicit release limitation."
    owner: "Amelia / Winston"
    priority: high
    status: open
```

NEW:

```yaml
  - epic: 10
    # Resolved 2026-07-07 via bmad-correct-course (sprint-change-proposal-2026-07-07-190839):
    # "record an explicit release limitation" branch taken. The deployed Server facade runs on the
    # fail-safe Unavailable bridge read model (context-search -> zero items; indexing-status ->
    # ReadModelUnavailable); limitation recorded in architecture.md Query Facade (Story 10.5)
    # section + epics.md Epic 10 note. The wiring itself is re-homed to Epic 11 Story 11.10 (AC added).
    action: "Wire the Server-side EventStore-backed semantic-indexing bridge read model for the query facade and indexing-status paths, or record an explicit release limitation. RESOLVED (record-limitation branch): limitation recorded in architecture.md + epics.md; wiring re-homed to Epic 11 Story 11.10."
    owner: "Amelia / Winston"
    priority: high
    status: done
```

Rationale: Closes the action item honestly (the record-limitation branch is satisfied) while preserving traceability to where the code work now lives (Story 11.10).

## 5. Change Navigation Checklist Results

- 1.1 Triggering item: [x] No implementation story triggered this — the trigger is the open Epic 10 action item passed to bmad-correct-course.
- 1.2 Core problem: [x] Category: **technical limitation discovered during implementation**, formalized as a deferral. The deployed Server facade depends on the fail-safe `Unavailable` bridge read model.
- 1.3 Supporting evidence: [x] Code trace (Server DI, Workers store, code comment `:84-86`), Epic 10 retro, Story 10.5 review record, architecture §Query Facade, sprint-status.
- 2.1 Current-epic impact: [x] Epic 10 not reopened; this is release-readiness closure it always owned.
- 2.2 Epic-level changes: [x] Story 11.10 gains one AC; no new/removed/redefined epic.
- 2.3 Remaining epics: [x] Epic 11 (Story 11.10) already owns the seam realignment; sequencing (11.2 → 11.8–11.12) is respected.
- 2.4 Obsolete/new epics: [N/A] None.
- 2.5 Priority/order: [x] No resequencing; wiring stays gated behind 11.2 pinning + DCP lane.
- 3.1 PRD conflicts: [N/A] FR58 unchanged; MVP scope unaffected.
- 3.2 Architecture conflicts: [!] Add the explicit limitation to §Query Facade + a cross-reference (Edits A, B).
- 3.3 UX conflicts: [N/A] Console already renders an explicit unavailable state (UX-DR20).
- 3.4 Other artifacts: [!] epics.md Story 11.10 AC + Epic 10 note (Edits C, D); sprint-status action item (Edit E). No OpenAPI/SDK/fixtures.
- 4.1 Direct Adjustment: [x] Viable. Effort Low, Risk Low.
- 4.2 Rollback: [N/A] Nothing to revert.
- 4.3 PRD MVP review: [N/A] No scope reduction.
- 4.4 Recommended path: [x] Direct Adjustment — record limitation + bind wiring to Story 11.10.
- 5.1 Issue summary: [x] Included.
- 5.2 Impact + artifact needs: [x] Included.
- 5.3 Recommended path + rationale: [x] Included with alternatives.
- 5.4 PRD MVP impact/action plan: [x] MVP unaffected; wiring plan owned by Story 11.10.
- 5.5 Handoff plan: [x] Below.
- 6.1 Checklist completion: [x] Complete except explicit approval.
- 6.2 Proposal accuracy: [x] Drafted from HEAD `40cc5e1` code evidence and loaded artifacts.
- 6.3 User approval: [!] Pending.
- 6.4 Sprint-status update: [x] One action-item resolution (Edit E); no story/epic state additions.
- 6.5 Handoff confirmation: [!] Pending approval.

## 6. Implementation Handoff

**Scope classification: Moderate** — coordinated planning-artifact edits plus an action-item resolution; no code change, no product-behavior change.

- **Technical Writer / Developer (Paige / Amelia):** apply Edits A–E in lockstep across `architecture.md`, `epics.md`, and `sprint-status.yaml`.
- **Architect (Winston):** confirm the §Query Facade limitation wording and the Memories-integration cross-reference accurately state the runtime control (fail-safe `Unavailable`, no leak).
- **Product Owner / PM (John / Jerome):** confirm the Story 11.10 AC binding and that FR58 product scope is unchanged.
- **QA / Test Architect (Murat):** on the next implementation-readiness rerun, verify the Epic 10 "bridge read wiring" item no longer reads as an unresolved open gap and the limitation is visibly recorded.

### Success criteria

- `architecture.md` §"Query Facade (Story 10.5)" states the deployed-Server `Unavailable` limitation with owner + consuming story (Edit A).
- `architecture.md` Memories-integration bullet cross-references the limitation (Edit B).
- `epics.md` Story 11.10 has an explicit AC to wire the Server-side EventStore-backed bridge read model (Edit C); the Epic 10 note carries the known-limitation pointer (Edit D).
- `sprint-status.yaml` Epic-10 action item is `done` with the record-limitation branch and 11.10 re-homing recorded (Edit E).
- No OpenAPI, SDK, generated-client, CLI, MCP, UI, worker, or aggregate behavior changed; working tree otherwise clean.

## 7. Approval Status

Pending Jerome's explicit approval. On approval, apply Edits A–E and (optionally) rerun implementation-readiness.
