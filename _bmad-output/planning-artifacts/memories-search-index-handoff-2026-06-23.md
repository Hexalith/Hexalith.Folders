# Handoff: Memories Search-Index Routing for Folders Search (2026-06-23)

**From:** Hexalith.Folders (Epic 9 — AppHost Platform Alignment, Story 9.3, Phase-1 close-out)
**To:** Hexalith.Folders Epic 10 (worker-side producer + folders→memories invoke authorization)
**Status:** Folders AppHost routing config shipped (dormant); end-to-end ingestion/search **gated on the Epic 10 producer**.
**Precedent:** Mirrors `Hexalith.Tenants/_bmad-output/planning-artifacts/memories-search-index-handoff-2026-06-21.md` (the `hexalith-tenants → tenants-index` integration that proves the same Memories server-side router).

---

## 1. What this enables

Cross-set Folders search backed by the standalone Memories search-index server: once the Epic 10 worker-side
producer emits one curated `SearchIndexEntryChanged` per folder/file-version into the `folders-index`
partition, the Folders read path can ask Memories *which* folders match a term and hydrate each row from the
ETag-fresh authoritative detail read. Memories decides **which** entries appear; it never supplies **what each
row shows** — so a stale index can only momentarily mis-state result-set membership, never render wrong data.

Story 9.3 lands the **AppHost routing config half** of that contract. It is deliberately dormant: with no
producer yet, nothing flows, but the `memories` server boots ready to accept `hexalith-folders`-sourced events
into `folders-index` the moment the Epic 10 producer ships.

## 2. What Folders already ships (no further Folders AppHost work for routing)

- **Story 9.1** — migrated the Folders AppHost to the platform Aspire helpers: gateway-only EventStore
  (`AddHexalithEventStore`, no `eventstore-admin*` resources) + Tenants (`AddHexalithTenantsServer`), with the
  shared `statestore`/`pubsub` Dapr components sourced from checked-in YAML.
- **Story 9.2** — hosted the Memories search-index server standalone on that shared topology via
  `AddHexalithMemoriesSearchIndexServer` (AppId `memories`, ports HTTP 3502 / gRPC 50002), adding the
  `memories-secretstore`/`memories-llm` Dapr components and the `memories-vectors` (Redis Stack) /
  `memories-graphs` (FalkorDB) containers. Routing was deferred to 9.3 by name; `memories` is hosted standalone
  and is **not** referenced/waited-on/JWT-wired by `folders`/`folders-workers`/`folders-ui` (parity with the
  canonical Tenants AppHost's Memories composition).
- **Story 9.3 (this handoff)** — applies the `hexalith-folders → folders-index` source→index routing on the
  `memories` server via the production helper `FoldersAspireModule.WithFoldersMemoriesSourceRouting()`, with the
  two contract values pinned as stable constants `FoldersAspireModule.MemoriesSourceId = "hexalith-folders"` and
  `FoldersAspireModule.MemoriesIndexTenant = "folders-index"`. A structural test composes the real helper and
  asserts both env vars resolve on the composed resource.

**No further Folders AppHost work is required for routing.** The remaining work is the Epic 10 producer (§5).

## 3. The exact AppHost env vars (set on the `memories` server resource)

Set by `FoldersAspireModule.WithFoldersMemoriesSourceRouting()` (invoked from
`src/Hexalith.Folders.AppHost/Program.cs` as `memories.Server.WithFoldersMemoriesSourceRouting()`):

- `EventStoreIntegration__Routing__SourceToTenantMap__hexalith-folders` = `folders-index`
- `EventStoreIntegration__Routing__AutoProvisionRoutedTenants` = `true`

Notes:

- The keys use `__` (double underscore), not `:`, mapping the appsettings path
  `EventStoreIntegration:Routing:SourceToTenantMap:hexalith-folders` to environment variables. The source id
  `hexalith-folders` is the **dictionary-key segment** (the CloudEvents `source` the Epic 10 producer stamps);
  `folders-index` is the **value** (the curated index tenant). The source id is not colon-encoded.
- `AutoProvisionRoutedTenants=true` is mandatory, not optional (see §4): without it, a non-Development boot
  risks a fail-fast "all routed tenants must exist" error, and the first event would be dropped as
  `TenantNotFound` because `folders-index` would not yet be `Active`.
- This mirrors the canonical Tenants AppHost, substituting `hexalith-folders → folders-index` for
  `hexalith-tenants → tenants-index` and omitting the placement/scheduler args (Folders threads none for any
  service — parity).

## 4. What Memories already implements server-side (no Memories change needed)

The Memories search-index server already supports this exact routing contract — proven in production by the
live `hexalith-tenants → tenants-index` Tenants integration. The same router handles
`hexalith-folders → folders-index` with no Memories code change:

- `TenantEventRoutingOptions.SourceToTenantMap` (`Dictionary<string,string>`, longest-prefix, case-insensitive)
  maps the CloudEvents `source` → index tenant; bound from `EventStoreIntegration:Routing` with
  `ValidateOnStart`.
- `RoutedTenantProvisioningStartupService` (a `BackgroundService`) provisions each distinct `SourceToTenantMap`
  value at startup and waits for `TenantStatus.Active` when `AutoProvisionRoutedTenants` is true — so
  `folders-index` is `Active` before the first event arrives.
- `EventStoreRoutingConfigValidator` **defers** its fail-fast "routed tenants must exist" check when
  `AutoProvisionRoutedTenants` is true (or Development) — so `memories` boots cleanly even though no producer
  exists yet.
- Ingestion recognizes `SearchIndexEntryChanged` / `SearchIndexEntryRemoved` CloudEvent types, upserts by
  composite key `(TenantId, AggregateId)`, indexes the curated `Text` for BM25 (syntactic axis) and `Attributes`
  as exact-match filterable metadata, and echoes the CloudEvent `id` verbatim as `ScoredResult.SourceUri`.

## 5. What activates it end-to-end (Epic 10 — NOT in 9.3)

End-to-end Folders search is gated on the Epic 10 worker-side producer. Epic 10 must add, on the Folders side:

1. **A worker-side producer** that, on durable Folders file/folder lifecycle events, publishes **one curated**
   `SearchIndexEntryChanged` per indexed unit via Dapr pub/sub:
   - pub/sub component `pubsub`, topic `memories-events`
   - CloudEvent metadata: `cloudevent.type = "SearchIndexEntryChanged"`, `cloudevent.source = "hexalith-folders"`
     (matching `FoldersAspireModule.MemoriesSourceId`), a stable `cloudevent.id`
   - payload `data`: `{ TenantId: "folders-index", AggregateId: "{stable-id}", Text: "{curated searchable text}",
     Attributes: { … } }` (here `TenantId` is the **index name**, i.e. the Memories tenant partition)
   - idempotent + upsert-shaped (re-publishing the same state is harmless).
2. **The `folders`/`folders-workers` → `memories` invoke authorization** (production deny-by-default caller
   policies + their negative-test rows) and any `memories`-topic pub/sub scope. **None of this is in 9.3** — the
   `memories` deny-by-default posture (no callers, empty topic scopes) is intentionally unchanged; routing env
   vars add no caller, topic, or invoke permission.
3. **The Folders read path** that calls `MemoriesClient.SearchAsync(TenantId: "folders-index", …)`, recovers ids
   from `ScoredResult.SourceUri`, hydrates via the authoritative detail path, applies any structured attribute
   filter, and degrades gracefully when Memories is unavailable.

## 6. Verification (deferred to Epic 10 — representative checklist)

Once the Epic 10 producer ships, verify end-to-end:

1. Publish a Folders create event → confirm one `folders-index` doc with `SourceUri` echoing the CloudEvent
   `id`, searchable `Text`, and the expected `Attributes`.
2. Publish an update/rename → confirm the doc's `Text` is **overwritten** (no stale text, no duplicate doc).
3. `GET /api/search?tenantId=folders-index&axis=syntactic&query={term}` → exactly one hit per matching unit.
4. With a structured attribute filter → only entries matching the filter are returned.
5. Confirm `folders-index` was auto-provisioned to `Active` at `memories` startup before the first event.

## 7. Status (2026-06-23)

- **Routing config: present and dormant.** The `hexalith-folders → folders-index` routing
  (`AutoProvisionRoutedTenants=true`) is set on the `memories` server in the Folders AppHost; the Memories
  server-side router already supports it (proven by the live `tenants-index` integration). With no producer,
  nothing flows — `memories` boots healthy and `folders-index` auto-provisions in the background.
- **End-to-end ingestion/search: gated on Epic 10** (worker-side producer emitting `SearchIndexEntryChanged`
  with source `hexalith-folders`, plus the `folders → memories` invoke authorization and the Folders read path).
- **Live `aspire run` boot sign-off** is deferred to a DCP-capable environment/CI per the environment-wide Aspire
  CLI / DCP `--tls-cert-file` certificate-trust blocker documented in Stories 9.1/9.2 — composition is proven by
  build + the structural topology tests.
