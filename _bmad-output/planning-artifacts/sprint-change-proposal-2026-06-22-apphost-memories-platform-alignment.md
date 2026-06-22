# Sprint Change Proposal — AppHost Platform Alignment & Memories Search-Index Integration

- **Date:** 2026-06-22
- **Author:** Jerome (via BMAD Correct Course)
- **Project:** Hexalith.Folders
- **Trigger (verbatim):** *"use eventstore, tenants, memories for app host initialization (like tenants app host)"*
- **Mode:** Incremental
- **Change scope classification:** **Moderate** (backlog reorganization: two new epics; no rework of completed epics)

---

## Section 1 — Issue Summary

The `Hexalith.Folders.AppHost` was authored **before** the Hexalith platform extracted its
reusable Aspire hosting recipes into shared `*.Aspire` libraries. As a result it diverges from the
canonical **Tenants AppHost** pattern in three ways:

1. **Hand-rolled topology.** `Hexalith.Folders.Aspire.FoldersAspireModule.AddHexalithFolders(...)`
   manually re-implements the EventStore + Tenants Dapr sidecar wiring and shared `statestore` /
   `pubsub` components, and `Program.cs` adds EventStore/Tenants with raw
   `builder.AddProject<Projects.Hexalith_EventStore>` / `<Projects.Hexalith_Tenants>`. The platform
   now provides `AddHexalithEventStore(...)` and `AddHexalithTenantsServer(...)` for exactly this.

2. **No Memories.** `Hexalith.Memories` is registered in `.gitmodules` and physically present, but the
   Folders AppHost never hosts it, `Directory.Build.props` defines no `HexalithMemoriesRoot`, and the
   AppHost csproj references no Memories assembly. The platform's `AddHexalithMemoriesSearchIndexServer(...)`
   helper is unused by Folders.

3. **No source→index routing.** The Tenants AppHost routes its producer's CloudEvents into a curated
   Memories index (`hexalith-tenants → tenants-index` + auto-provision). Folders has no equivalent
   `hexalith-folders → folders-index` routing.

**Why this matters now:** the hand-rolled approach actively conflicts with the repository rule
*"Do not add boilerplate common to domain modules… reuse shared implementations / Aspire topology is
centralized"* (CLAUDE.md, `project-context.md`). Aligning Folders to the platform helpers removes
duplicated infrastructure, restores parity with Tenants, and adds the Memories search-index service to
the local topology.

### Discovery context & evidence

| Evidence | Location |
|---|---|
| Hand-rolled EventStore/Tenants/Dapr wiring | `src/Hexalith.Folders.Aspire/FoldersAspireModule.cs` (`AddHexalithFolders`, `AddFoldersSharedDaprComponents`) |
| Raw `AddProject<…>` for EventStore/Tenants | `src/Hexalith.Folders.AppHost/Program.cs:17–18` |
| Canonical reference pattern | `Hexalith.Tenants/src/Hexalith.Tenants.AppHost/Program.cs` (`AddHexalithEventStore`, `AddHexalithTenantsServer`, `AddHexalithMemoriesSearchIndexServer`) |
| Helpers exist | `Hexalith.EventStore.Aspire`, `Hexalith.Tenants.Aspire`, `Hexalith.Memories.Aspire` (all initialized) |
| Memories registered but unwired | `.gitmodules` has `Hexalith.Memories`; `Directory.Build.props` has **no** `HexalithMemoriesRoot` |
| Memories = optional, worker-owned, async track | `architecture.md` §130–156 |
| Producer absent | `src/Hexalith.Folders.Workers/` — **zero** Memories references |

---

## Section 2 — Impact Analysis

### Epic Impact
- **Epic 8 (MVP Release Acceptance Closure):** unaffected — it is a release-gating epic, not a feature
  workstream. This change must **not** be folded into it.
- **Epics 1–7:** all `done`; none impacted (Phase 1 is a behavior-preserving refactor for
  folders/workers/ui plus additive Memories hosting).
- **New epics required:** two (see Section 3).

### Story Impact
- **No existing story changes.** All work is net-new.
- New stories created under Epic 9 (Phase 1) and Epic 10 (Phase 2 — gated).

### Artifact Conflicts / Updates
| Artifact | Change | Phase |
|---|---|---|
| `epics.md` AR-INFRA-01/02 (embedded reqs, §328–329) | Topology + Dapr-components description: platform-helper composition; add `memories` + Memories components | 1 |
| `architecture.md` AppHost Composition (§398) | Add `memories`; note platform-helper composition | 1 |
| `architecture.md` I-4 (§562), §756 | App-ID list: add `memories` | 1 |
| `architecture.md` I-3 (§561) | Access-control: add `folders`/`folders-workers` → `memories` invoke + memories pub/sub topic + negative tests | 1 |
| `architecture.md` §130–156 | Memories "optional track" → active scope for Phase 2 | 2 |
| `architecture.md` §1486 | Aspire/CommunityToolkit version drift note (arch 13.3.0 vs project-context 13.4.3) | 1 |
| `prd.md` | New FR for authorized context-query/RAG facade | 2 only |
| `ux-design-specification.md` | "Semantic-indexing status" console projection (arch §145) | 2 only |
| Dapr components (`src/Hexalith.Folders.AppHost/DaprComponents/`) | Add `statestore.yaml`, `pubsub.yaml`, `resiliency.yaml`, `secretstore.memories.yaml`, `llm.memories.yaml`; update `accesscontrol.yaml` | 1 |
| `Directory.Build.props` | Add `HexalithMemoriesRoot` resolution | 1 |
| AppHost / Aspire csproj | Reference `Hexalith.EventStore.Aspire`, `Hexalith.Tenants.Aspire`, `Hexalith.Memories.Aspire` | 1 |
| `project-context.md` | App-ID list (add `memories`); "topology centralized" rule now delegates to platform helpers | 1 |
| Tests | `AspireTopologyTests`, `AppHostBootSmokeTests`, `DaprPolicyConformanceTests`, `ContainerImageConformanceTests` | 1 |
| Docs | New Memories search-index handoff note | 1 |

### Technical Impact
- **Behavior-preserving** for folders/workers/ui sidecars (same app-IDs: `folders`, `folders-workers`,
  `folders-ui`).
- **Decision applied:** EventStore **gateway-only** — `AddHexalithEventStore(eventStore, adminServer: null, …)`;
  no `eventstore-admin` / `eventstore-admin-ui` added (preserves today's topology).
- **New runtime resources:** `memories` (project + sidecar), `memories-vectors` (redis/redis-stack),
  `memories-graphs` (falkordb), `memories-secretstore`, `memories-llm` Dapr components.
- **Shared-component shift:** the hand-rolled `statestore`/`pubsub` (created in `FoldersAspireModule`) are
  replaced by the EventStore platform's component-YAML-backed shared components reused by every sidecar,
  including Memories — requiring the new component YAML files above.
- **Gating reality:** Phase 1 wires the **routing config**; events only flow once the Phase 2 worker
  **producer** emits `SearchIndexEntryChanged` (source `hexalith-folders`). This mirrors the Tenants
  AppHost, which itself flags end-to-end as *"gated on the Memories handoff."*

---

## Section 3 — Recommended Approach

**Selected path: Option 1 — Direct Adjustment**, delivered as **two new epics** (per decision):

### Epic 9 — AppHost Platform Alignment & Memories Search-Index Topology *(Phase 1 — closes the request)*
Behavior-preserving adoption of the platform Aspire helpers + additive Memories hosting + AppHost-level
routing config. Effort **Medium**, Risk **Low–Medium**.

### Epic 10 — Folders Worker-Side Semantic-Indexing Producer & Bridge Projection *(Phase 2 — gated)*
The producer/bridge-projection feature that makes routing flow end-to-end. The architecture's "optional
integration track." Effort **High**, Risk **Medium** (couples to C4 large-file + C9 path-exposure policy).
Stories created as **backlog stubs**, scheduled separately.

**Rationale:** nothing to roll back; MVP scope is untouched (Memories was never an MVP FR); splitting keeps
the requested AppHost change shippable and verifiable on its own while the larger producer feature is
sequenced behind its C4/C9 dependencies.

| Option | Verdict |
|---|---|
| 1 — Direct Adjustment | ✅ Selected |
| 2 — Rollback | ❌ N/A (nothing to revert) |
| 3 — MVP Review | ➖ Not needed (MVP unaffected) |

---

## Section 4 — Detailed Change Proposals

### 4.1 Epics (`epics.md` + `sprint-status.yaml`)

**Add to `epics.md` → "## Epic List":**
```
### Epic 9: AppHost Platform Alignment And Memories Search-Index Topology
### Epic 10: Folders Worker-Side Semantic-Indexing Producer And Bridge Projection
```

**Epic 9 stories (Phase 1):**
- **9-1 Adopt EventStore + Tenants platform Aspire helpers.** Replace `FoldersAspireModule` hand-rolled
  EventStore/Tenants + shared-Dapr wiring with `AddHexalithEventStore(eventStore, adminServer: null, …)`
  + `AddHexalithTenantsServer(eventStoreResources, …)`. Add `.Aspire` refs; add `statestore.yaml`,
  `pubsub.yaml`, `resiliency.yaml` Dapr components. Keep `folders`/`folders-workers`/`folders-ui` wiring
  and app-IDs identical. Update `AspireTopologyTests` + `AppHostBootSmokeTests`.
- **9-2 Add Memories search-index server to the topology.** `AddHexalithMemoriesSearchIndexServer(stateStore,
  pubSub, secretStorePath, llmPath, …)` reusing the shared state/pubsub; add `secretstore.memories.yaml` +
  `llm.memories.yaml`; add `HexalithMemoriesRoot` + `Hexalith.Memories.Aspire` ref. Register `memories`
  app-ID. Update access-control + structural/container-image conformance tests.
- **9-3 Apply Folders→Memories routing config + sync artifacts.** Set
  `EventStoreIntegration__Routing__SourceToTenantMap__hexalith-folders=folders-index` and
  `AutoProvisionRoutedTenants=true` on the `memories` resource. Update `architecture.md`
  (AR-INFRA-01 / I-4 / I-3), `project-context.md`, and author a Memories search-index handoff doc noting
  end-to-end is gated on Epic 10.

**Epic 10 stories (Phase 2 — backlog stubs, gated):**
- **10-1** Worker-side semantic-indexing port; `Hexalith.Folders.Workers` → `Hexalith.Memories.Client.Rest`/`Contracts` (Workers only).
- **10-2** Folders-owned bridge projection (`file version → workflow/memory-unit/status`: indexed/stale/skipped/failed/tombstoned/reconciliation-required).
- **10-3** Authorized async indexing on file-write/commit (authorize: tenant→ACL→path→sensitivity→size/type→Memories; stable source URIs + idempotency keys; outage = retryable status, never rollback).
- **10-4** Emit `SearchIndexEntryChanged` CloudEvents (source `hexalith-folders`) → **activates** Epic 9 routing.
- **10-5** Authorized, security-trimmed/redacted Folders query facade over Memories (new PRD FR + arch query-facade + UX "indexing status" view).

**Add to `sprint-status.yaml` → `development_status:`:**
```yaml
  # Epic 9: AppHost Platform Alignment And Memories Search-Index Topology
  # Created 2026-06-22 via bmad-correct-course
  # (sprint-change-proposal-2026-06-22-apphost-memories-platform-alignment).
  epic-9: backlog
  9-1-adopt-eventstore-and-tenants-platform-aspire-helpers: backlog
  9-2-add-memories-search-index-server-to-the-apphost-topology: backlog
  9-3-apply-folders-to-memories-routing-config-and-sync-artifacts: backlog
  epic-9-retrospective: optional

  # Epic 10: Folders Worker-Side Semantic-Indexing Producer And Bridge Projection
  # Phase 2 (gated on Epic 9 + C4/C9). Stubs pending create-story.
  epic-10: backlog
  10-1-define-worker-side-semantic-indexing-port-and-memories-dependency: backlog
  10-2-build-folders-owned-indexing-bridge-projection: backlog
  10-3-author-authorized-async-indexing-on-file-write-and-commit: backlog
  10-4-emit-search-index-entry-changed-cloudevents-from-workers: backlog
  10-5-expose-authorized-folders-query-facade-over-memories: backlog
  epic-10-retrospective: optional
```

### 4.2 Architecture requirements (`epics.md` embedded list + `architecture.md`)

> Note: AR-INFRA-01/02 live in `epics.md`'s embedded architecture-requirements list (§328–329); the AppHost Composition bullet, I-3, I-4 and §756 live in `architecture.md`.

**AR-INFRA-01 (`epics.md` §328) —**
```
OLD: AR-INFRA-01: `Hexalith.Folders.AppHost` composes EventStore (`AppId=eventstore`) + Tenants
     (`AppId=tenants`) + Folders.Server (`AppId=folders`) + Folders.Workers (`AppId=folders-workers`)
     + Folders.UI (`AppId=folders-ui`) + Keycloak. Aspire 13.3.0 + CommunityToolkit.Aspire.Hosting.Dapr 13.0.0.

NEW: AR-INFRA-01: `Hexalith.Folders.AppHost` composes the platform via the shared Aspire helpers —
     EventStore command gateway (`AppId=eventstore`, gateway-only via `AddHexalithEventStore(adminServer:null)`)
     + Tenants (`AppId=tenants`, `AddHexalithTenantsServer`) + Memories search-index server
     (`AppId=memories`, `AddHexalithMemoriesSearchIndexServer`; `memories-vectors` + `memories-graphs`
     containers) + Folders.Server (`AppId=folders`) + Folders.Workers (`AppId=folders-workers`)
     + Folders.UI (`AppId=folders-ui`) + Keycloak. Aspire 13.4.3 + CommunityToolkit.Aspire.Hosting.Dapr 13.0.0.
```

**I-4 (§562) + §756 — app-ID list:**
```
OLD: `eventstore`, `tenants`, `folders`, `folders-ui`, `folders-workers`
NEW: `eventstore`, `tenants`, `memories`, `folders`, `folders-ui`, `folders-workers`
```

**I-3 (§561) — access control:** append memories triples to the production deny-by-default policy
(`folders`/`folders-workers` may invoke `memories`; declare the memories pub/sub topic) plus the matching
negative-test rows. (Local `defaultAction: allow` needs no change.)

### 4.3 `project-context.md`
```
OLD (app IDs): - Dapr app IDs and component names are stable contracts: `eventstore`, `tenants`,
     `folders`, `folders-workers`, `folders-ui`, `statestore`, and `pubsub`.
NEW: - Dapr app IDs and component names are stable contracts: `eventstore`, `tenants`, `memories`,
     `folders`, `folders-workers`, `folders-ui`, `statestore`, and `pubsub`.

OLD (topology): - Aspire topology is centralized in `Hexalith.Folders.Aspire`; keep shared Dapr
     state-store/pub-sub sidecar wiring there and AppHost environment concerns in `Hexalith.Folders.AppHost`.
NEW: - Aspire topology is composed in `Hexalith.Folders.AppHost` via the platform helpers
     (`AddHexalithEventStore`, `AddHexalithTenantsServer`, `AddHexalithMemoriesSearchIndexServer`); do not
     re-implement EventStore/Tenants/Memories Dapr wiring locally. `Hexalith.Folders.Aspire` retains only
     Folders-specific helpers and the stable app-ID/component-name constants.
```

### 4.4 Code & config (Phase 1 — specs; dev pins exact params against helper signatures)
- `src/Hexalith.Folders.AppHost/Program.cs` — rewrite resource composition to the Tenants pattern.
- `src/Hexalith.Folders.Aspire/FoldersAspireModule.cs` — remove hand-rolled `AddHexalithFolders` /
  `AddFoldersSharedDaprComponents`; keep the app-ID/component-name constants (still asserted by tests).
- `src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj` — add the three `.Aspire` refs;
  drop direct EventStore/Tenants **runtime** project refs (now resolved cross-repo by the helpers' ProjectMetadata).
- `Directory.Build.props` — add `HexalithMemoriesRoot` (mirror the EventStore/Tenants resolution pattern).
- New Dapr component YAMLs under `DaprComponents/` (adapt from Tenants AppHost): `statestore.yaml`,
  `pubsub.yaml`, `resiliency.yaml`, `secretstore.memories.yaml`, `llm.memories.yaml`.

### 4.5 Tests (Phase 1)
- `AspireTopologyTests.cs` — update for the new composition; assert the `memories` app-ID + Memories
  components/containers; keep stable app-ID assertions for folders/workers/ui.
- `AppHostBootSmokeTests`, `DaprPolicyConformanceTests`, `ContainerImageConformanceTests` — extend for `memories`.

---

## Section 5 — Implementation Handoff

**Scope classification: Moderate** → Product Owner + Developer.

| Role | Responsibility |
|---|---|
| **Product Owner / DEV** | Apply the backlog reorg: add Epic 9 + Epic 10 to `epics.md` and `sprint-status.yaml` (§4.1). |
| **Architect (light)** | Apply `architecture.md` edits (§4.2) — AR-INFRA-01, I-4/§756, I-3, version note. |
| **Developer** | Implement Epic 9 stories 9-1 → 9-2 → 9-3 (§4.3–4.5). Verify: `dotnet build Hexalith.Folders.slnx`, the narrowed Aspire/AppHost test set, then `aspire run` smoke (AppHost changes require an Aspire restart). |
| **Deferred** | Epic 10 stubs await `create-story` + C4/C9 readiness. |

**Success criteria (Epic 9 / Phase 1):**
1. Folders AppHost composes EventStore (gateway-only) + Tenants + Memories purely via platform helpers; no hand-rolled Dapr topology remains.
2. `aspire run` brings up `eventstore`, `tenants`, `memories` (+ `memories-vectors`/`memories-graphs`), `folders`, `folders-workers`, `folders-ui` healthy.
3. `hexalith-folders → folders-index` routing config present on the `memories` resource (dormant until Epic 10 producer).
4. Architecture, project-context, Dapr components, and Aspire/AppHost tests updated and green.

**Sequencing:** Epic 9 is independent of Epic 8 (release closure) and can proceed in parallel. Epic 10 follows Epic 9 and its C4/C9 dependencies.
