# Sprint Change Proposal — Story 10.3 Memories Index-Update Mechanism Correction

- **Date:** 2026-06-23
- **Author:** Jerome (with BMAD `correct-course`)
- **Trigger:** "Hexalith Memories indexes are updated using `SearchIndexEntryChanged` event"
- **Triggering story:** Story 10.3 (`review`) — `Author authorized asynchronous indexing on file-write and commit`
- **Change type:** Misunderstanding of the integration contract discovered during implementation review
- **Scope classification:** **Moderate** (story rework + backlog re-scope + governance-test touch) → PO/Dev handoff
- **Mode:** Incremental
- **Evidence base:** Epic 9 handoff doc §4–§5; `architecture.md` L140/L401; Memories submodule source; `Hexalith.Tenants` `MemoriesSearchIndexEventPublisher`; a 6-agent repo impact sweep (44 findings, synthesized + verified)

---

## 1. Issue Summary

The in-review Story 10.3 producer updates Memories via **`MemoriesClient.IngestAsync(...)`** — the **wrong Memories subsystem**.

Memories exposes two independent ingestion paths:

| Path | What it is | Status |
|------|-----------|--------|
| **`IngestAsync`** (`Hexalith.Memories.Client.Rest`) | RAG **memory-ingestion**: `POST /api/ingest` → async LLM-embedding workflow → "memory units"; returns a workflow instance id | Experimental (`HXL001`) |
| **`SearchIndexEntryChanged` CloudEvent** (`Hexalith.Memories.Contracts.V1`) | **Search-index** update: published to `pubsub`/`memories-events`, routed by `SourceToTenantMap` into the index partition, upserted by `(TenantId, AggregateId)` into RediSearch/BM25 | Stable, canonical |

Epic 9 shipped the `hexalith-folders → folders-index` routing, which **only ingests `SearchIndexEntryChanged` / `SearchIndexEntryRemoved` CloudEvents**. As built, Story 10.3 would therefore **never populate `folders-index`** and never activate end-to-end through the routing Epic 9 set up. The architecture spine was always correct (`architecture.md` L140/L401 name `SearchIndexEntryChanged`); the drift was localized to the 10.3 implementation and its detailed ACs.

**Canonical precedent:** `Hexalith.Tenants` `MemoriesSearchIndexEventPublisher` implements `IEventStoreDomainEventHandler<T>` and publishes `SearchIndexEntryChanged` on every lifecycle event — and *explicitly never calls `IngestAsync`* (it notes the call would fail warnings-as-errors). The `hexalith-tenants → tenants-index` integration proves the exact router Folders targets.

---

## 2. Impact Analysis

### Epic impact
- **Epic 10** completes as planned **in intent**; only the producer's **egress + content/metadata model** change. No epic added/removed/resequenced.

### Story impact
- **10.1 (done):** dependency corrected — `Hexalith.Memories.Contracts` only (drop `Hexalith.Memories.Client.Rest`); the port is a *search-index publication* port. (Retroactive note in `epics.md`; the actual `.csproj`/registration drop lands with the 10.3 rework.)
- **10.2 (done):** bridge tracks `file version → Memories **search-index entry**/status` (was "workflow/memory unit"). Projection vocabulary unchanged; reusable as-is.
- **10.3 (review → ready-for-dev):** **reworked.** Egress `IngestAsync` → publish `SearchIndexEntryChanged` via `DaprClient.PublishEventAsync`; content model bytes-upload → curated C9-safe `Text` + flat `Attributes`. **Preserved:** `/folders/events` subscription, `SemanticIndexingProcessManager` orchestration, authorization gating, bridge writer, deterministic idempotency.
- **10.4 (backlog, re-scoped):** the `SearchIndexEntryChanged` emission folds **forward into 10.3**; 10.4 now owns **`SearchIndexEntryRemoved`** (removal/archive/tombstone) + **live `folders-index` end-to-end verification**.
- **10.5 (backlog):** unaffected (query facade consumes mechanism-agnostic bridge status).

### Artifact conflicts
- **PRD:** no change (the Memories FR is added with 10.5 "when scheduled").
- **Architecture:** disambiguated (residual RAG language at L132–138/L156). Spine activation lines L140/L401 were already correct.
- **UI/UX:** no change ("indexing status" console projection is mechanism-agnostic).
- **Governance/tests:** the **only** Dapr change is the stale conformance-test expectations (production access-control is already correct). Worker `.csproj` drops `Hexalith.Memories.Client.Rest`; `ScaffoldContractTests` allowlist + several worker tests update.

### Technical impact (idempotency model shift)
- From `IngestAsync`'s returned workflow id → **upsert by `(TenantId, AggregateId)`** at Memories. Re-publishing the same state is harmless; no Memories-side workflow/memory-unit id is returned.

---

## 3. Recommended Approach

**Option 1 — Direct Adjustment, with a partial rollback of the 10.3 egress (Hybrid).** Re-scope stories 10.3/10.4 and rework the 10.3 egress + content model; reuse the bridge/subscription/orchestration/gating.

- **Effort:** Medium · **Risk:** Low · **Timeline:** contained within Epic 10; no downstream epic slip.
- **Why:** the architecture spine and Epic 9 routing already prescribe the event; this *restores* alignment rather than re-planning. The most valuable in-review work (bridge projection, subscription, orchestration, gating) survives untouched. Rejected alternatives: full rollback of 10.1–10.3 (wasteful — most code is correct); MVP scope reduction (unnecessary — no product scope changes).

---

## 4. Detailed Change Proposals

### 4A. Planning artifacts — **APPLIED in this session**

| Artifact | Change |
|----------|--------|
| `epics.md` | Epic 10 correction note; 10.1 dep → `Contracts` only; 10.2 "memory unit" → "search-index entry"; 10.3 → "publish curated `SearchIndexEntryChanged` after gates"; 10.4 → `SearchIndexEntryRemoved` + live verification |
| `architecture.md` | §130–156 disambiguation (L132/134/135/136/137/138/156): RAG `IngestAsync`/"memory unit"/`Client.Rest` language → `SearchIndexEntryChanged` publish + `(TenantId, AggregateId)` upsert; Contracts-only dependency |
| `sprint-status.yaml` | 10.3 `review → ready-for-dev`; Epic 10 + 10.4 correction comments; Epic 9 retro action #4 reframed (invoke auth not needed → production policy already correct; only conformance test changes) |
| `project-context.md` | New don't-miss rule: publish `SearchIndexEntryChanged`/`SearchIndexEntryRemoved`; never `IngestAsync`; never re-introduce `Client.Rest`/`AddMemoriesClient()` |
| `10-3-...md` (story) | Status revert + rework banner; scope in/out; ACs 4/5/6/9; Tasks 5/6; embedded "Mechanism Correction & Dev Guidance"; superseded prior Dev Agent Record |
| `technical-...-rag-research-2026-05-11.md` | Superseding banner (do not cite as current guidance) |

### 4B. Code rework inventory — **for the Story 10.3 dev** (must-change)

| File | Change |
|------|--------|
| `…/SemanticIndexing/MemoriesSemanticIndexingPort.cs` | Inject `DaprClient` (drop `MemoriesClient`); `PublishEventAsync("pubsub","memories-events", entry, metadata, ct)`; `MetadataField` map → flat `Dictionary<string,string>`; remove `HXL001`/`IngestedBy`; single retryable `memories_publish_error` catch; keep `OperationCanceledException` rethrow |
| `…/Workers/FoldersWorkersModule.cs` | Remove `using Hexalith.Memories.Client.Rest;` + `AddMemoriesClient();` (DaprClient already registered) |
| `…/Workers/Hexalith.Folders.Workers.csproj` | Remove `Hexalith.Memories.Client.Rest` `ProjectReference`; keep `Hexalith.Memories.Contracts`; confirm `Dapr.Client` reachable |
| `…/Testing.Tests/ScaffoldContractTests.cs` (L156) | Remove `Hexalith.Memories.Client.Rest` from the Workers allowlist — **ship coupled with the `.csproj` drop** |
| `…/Workers.Tests/SemanticIndexingWorkerRegistrationTests.cs` | Assert `DaprClient` registration; rewrite the port test to mock `PublishEventAsync` and assert the published `SearchIndexEntryChanged` shape + CloudEvent metadata; keep the Memories-DTO-non-exposure test |
| `…/Workers.Tests/SemanticIndexingProcessManagerTests.cs`, `EventStoreSemanticIndexingBridgeStoreTests.cs`, `SemanticIndexingEndpointE2ETests.cs` | `WorkflowId`/`MemoryUnitId` → `ShouldBeNull()` (no returned workflow id) |
| `…/Contracts.Tests/OpenApi/DaprPolicyConformanceTests.cs` | Keep `memories.DefaultAction == deny` + empty `folders-workers` invoke; assert active `memories-events` publish/subscribe scopes; drop "deferred to Epic 10" |

Non-blocking / tracked follow-ups: bridge-evidence field rename `WorkflowInstanceId`/`MemoryUnitId → PublishedEventId`; `SemanticIndexing → SearchIndexing` namespace rename (~426 occurrences); `MaxInlineIngestionBytes` doc reframe.

### 4C. Resolved design decisions
- **(a) `Text`:** descriptor-derived, C9-safe — `Content.IndexingTextDescriptor` + non-sensitive identity tokens + `TypeClassification`; never raw bytes/paths; empty → `"{typeClassification} {fileVersionId}"`.
- **(b) `MaxInlineIngestionBytes`:** kept as a curated-unit **eligibility** threshold (bytes stay in-worker for the size gate).
- **(c) Evidence fields:** `WorkflowInstanceId`/`MemoryUnitId` → single `PublishedEventId` (= `cloudevent.id`); coordinated edit, else leave null + track.
- **(d) Materializer:** **repurpose** `ISemanticIndexingContentMaterializer` to emit curated `Text` + `Attributes` (stays the only byte-touching, fail-closed seam).
- **(e) `Attributes`:** plain strings; drop `MetadataOrigin`/confidence; re-confirm no raw path.

### 4D. Dapr policy verdict
Producer **publishes** to `memories-events` via the `pubsub` **component** — **no** service-invoke to `memories`. `deploy/dapr/production/accesscontrol.yaml`, `sidecar-config-bindings.yaml`, and `tests/fixtures/dapr-policy-conformance.yaml` are **already correct** (deny-by-default; pub/sub-component model) and need no edit. Target conformance rows: `memories.DefaultAction == deny` (unchanged); `folders-workers` invoke == empty (unchanged); `publishingScopes[folders-workers] ⊇ memories-events`; `subscriptionScopes[memories] ⊇ memories-events`; `publishingScopes[memories]` empty.

### 4E. Contract + publish template (dev handoff)
See the embedded "Mechanism Correction & Dev Guidance" section in `10-3-author-authorized-async-indexing-on-file-write-and-commit.md` for the verbatim `SearchIndexEntryChanged`/`SearchIndexEntryRemoved` records and the `DaprClient.PublishEventAsync` pattern (mirroring the `Hexalith.Tenants` precedent).

---

## 5. Implementation Handoff

**Scope: Moderate → Product Owner / Developer.**

- **Developer (`bmad-dev-story` on 10-3):** execute §4B + §4C against the corrected ACs. Single change set must couple the `.csproj` `Client.Rest` drop with the `ScaffoldContractTests` allowlist edit and the `DaprPolicyConformanceTests` update. Verify: `dotnet build Hexalith.Folders.slnx`; `Hexalith.Folders.Workers.Tests`; `Hexalith.Folders.Tests`; `Hexalith.Folders.Testing.Tests`; the Dapr policy conformance gate.
- **PO / John:** when `create-story` runs for the re-scoped **10.4**, follow the corrected `epics.md` scope (`SearchIndexEntryRemoved` + live verification). Murat/Amelia: close Epic 9 retro action #4 per §4D.
- **Success criteria:** worker publishes one `SearchIndexEntryChanged` per indexed unit into `folders-index`; no `Hexalith.Memories.Client.Rest` reference remains in any Folders project; bridge status semantics intact; all narrowed test lanes green; conformance suite reflects the active pub/sub path.

---

## Appendix — Change-analysis checklist status

§1 Trigger ✅ · §2 Epic impact ✅ · §3 Artifact conflicts ✅ (PRD N/A, UI/UX N/A, Architecture/Tests action-taken) · §4 Path forward ✅ (Option 1 hybrid) · §5 Proposal components ✅ · §6 Handoff ✅. Consistency gaps surfaced by the sweep (10.4 file rework on create-story; research-doc banner; conformance-test naming; `.csproj`/allowlist coupling; project-context regression guard; evidence-field rename tracked) — all addressed above.
