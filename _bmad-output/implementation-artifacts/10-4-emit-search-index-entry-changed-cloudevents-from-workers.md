---
baseline_commit: 4d9cd21
---

# Story 10.4: Emit search-index removal & archive CloudEvents (hybrid hard + soft delete) and prove the live folders-index round-trip

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

> [!IMPORTANT]
> **Three decisions are pre-resolved by Jerome (2026-06-23) and are binding for this story:**
>
> 1. **Hybrid removal model.** A **file removal** (`WorkspaceFileMutationAccepted` kind `remove` → bridge reason `folder_file_removed`) is a **hard delete**: publish `SearchIndexEntryRemoved` (Memories `KeyDeleteAsync` drops the doc). A **folder archive** (`FolderArchived` → bridge reason `folder_archived`) is a **soft delete**: publish `SearchIndexEntryChanged` with `Attributes["folders.status"] = "archived"` (the doc stays, filterable — the proven `Hexalith.Tenants` precedent for `TenantDisabled`). Both event types are the canonical, event-based ("global") way the Memories search index is maintained — never `IngestAsync`.
> 2. **Live verification = the real `aspire run` 6-service boot.** No hermetic Testcontainers substitute. The live round-trip is proven by **extending the existing opt-in Tier-3 harness** (`tests/Hexalith.Folders.AppHost.Tests`), and that acceptance (**AC9**) is **BLOCKED-PENDING the DCP-capable lane** (the env-wide Aspire CLI / DCP `--tls-cert-file` blocker — open Epic 9 action item). All other ACs are implementable + verifiable now.
> 3. **End-to-end ownership.** The "D1 inbound wiring" (EventStore→worker `folders.events`) is **already implemented** at HEAD (commits `27c22ff` + `3dc0db4`; see 10.3's changelog). 10.4 does **NOT** re-implement it — it **verifies it is present and sufficient for removals** and **extends the existing cross-process harness** for the full round-trip. The removal publish reuses the SAME `memories-events` outbound topic 10.3 already scoped for `folders-workers`, so **no new Dapr pub/sub scope is required**.
>
> **State note (verified at HEAD `4d9cd21`):** 10.3 is at `review` with its D1 production follow-ups (#1/#2/#3) committed in `27c22ff`/`3dc0db4`; 10.5's story file already exists (`ready-for-dev`). Treat the committed code at HEAD as authoritative; 10.4 builds on it.

## Story

As a Folders platform engineer,
I want Folders workers to prune the Memories `folders-index` when file versions are removed and to mark archived folders' entries as archived, then prove the full publish→route→index→search→remove round-trip on the live topology,
so that a syntactic search of `folders-index` returns exactly one hit per live indexed file version, removed units leave no stale searchable entry, and the dormant Epic 9 routing is proven activated end-to-end.

## Context & Scope Boundary

Epic 10 activates the dormant Epic 9 Memories topology. Story 10.1 added the worker-only `Hexalith.Memories.Contracts` dependency and `ISemanticIndexingPort`; Story 10.2 added the Folders-owned bridge projection/read model; Story 10.3 published `SearchIndexEntryChanged` (the create/update path) into `folders-index` after authorization, closed its D1 production follow-ups (the inbound `folders.events` wiring + the Tier-3 harness), and is at `review`. Story 10.5 (already authored, `ready-for-dev`) is the read facade and **depends on 10.4** landing a populated, correctly-pruned index.

Story 10.4 closes the **deletion half** of the producer and proves the pipeline live.

**In scope:**

1. **Removal egress on the port (net-new).** Extend `ISemanticIndexingPort` and `MemoriesSemanticIndexingPort` with a removal path that publishes `SearchIndexEntryRemoved` (hard delete) and a soft-delete path that publishes `SearchIndexEntryChanged` with `Attributes["folders.status"] = "archived"`. Reuse the established `DaprClient.PublishEventAsync(PubSubName, EventsTopicName, …)` pattern, constants, and CloudEvent-metadata shape from 10.3.
2. **Route tombstones instead of skipping them.** `SemanticIndexingProcessManager` currently **silently skips** tombstoned entries (it `continue`s when `Identity.ContentHashReference is null`). Replace that skip with reason-code routing: `folder_file_removed` → hard delete, `folder_archived` → soft delete. Emit one CloudEvent **per previously-indexed file-version entry**, reconstructing identity from the **tombstoned entry's preserved `Identity`** (not from the remove event, which carries no file-version id).
3. **Identity equivalence (load-bearing correctness).** The removal/soft-delete `AggregateId` and `cloudevent.id` MUST be **byte-identical** to the values the original `SearchIndexEntryChanged` upsert used for that file version (`AggregateId = {ManagedTenantId}/{OrganizationId}/{FolderId}/{FileVersionId}`; `cloudevent.id` = the stable source URI). A mismatch deletes nothing (or the wrong doc) under the `(TenantId, AggregateId)` composite key.
4. **`folders.status` attribute on the upsert path (cross-story enabler for 10.5).** Add `Attributes["folders.status"]` to the 10.3 `SearchIndexEntryChanged` upsert as well — `"active"` for live indexing, `"archived"` for the archive soft-delete — so the 10.5 query facade can filter soft-deleted units. Keep all attribute values metadata-only/C9-safe.
5. **Bridge records removal/soft-delete evidence without status regression.** Record each removal/soft-delete publish outcome (the published `cloudevent.id` as `PublishedEventId`, retryable failures, reconciliation) on the **Tombstoned** entry, preserving the Story 10.2/10.3 invariant that a Tombstoned entry never regresses to Indexed and that older results never overwrite newer versions.
6. **Outage behavior never rolls back Folders operations.** A Dapr publish failure / timeout / `DaprException` / cancellation on a removal or soft-delete is recorded as a retryable `failed` / `reconciliation_required` bridge result with a stable reason code; it is never thrown past the event processor in a way that makes a durable Folders file/folder operation appear rejected or rolled back.
7. **Verify (do NOT re-implement) the inbound D1 wiring is present and sufficient.** Confirm the removal publish needs no new Dapr scope (same `memories-events` topic; `publishingScopes` already has `folders-workers=memories-events`). Confirm `deploy/dapr/production/pubsub.yaml`, `sidecar-config-bindings.yaml`, and `DaprPolicyConformanceTests` already model the `folders.events` inbound path. Only touch Dapr policy artifacts if the removal path demonstrably requires a change (it should not).
8. **Live end-to-end round-trip proof (AC9 — BLOCKED-PENDING the DCP lane).** Extend the existing `AspireFoldersAppHostFixture` / `FoldersTopologyCrossProcessTests` (opt-in `HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION`) from "topology boots" to a real round-trip: mutate → `folders-index` returns exactly one hit (`ScoredResult.SourceUri` echoes `cloudevent.id`); remove → search returns zero; archive → the doc remains with `folders.status=archived`. The harness must keep skipping cleanly where the opt-in / DCP capability is absent.

**Out of scope:**

1. **The read side.** No REST/SDK/CLI/MCP/UI search surface, no security-trimming, no `MemoriesClient.SearchAsync`, no `Hexalith.Memories.Client.Rest` reference in any Folders project — that is **Story 10.5** (already authored). 10.4 only proves the index is correctly pruned by querying Memories directly **inside the gated AppHost integration test**.
2. **Re-implementing the D1 inbound wiring** (`27c22ff`/`3dc0db4`) — it is committed; 10.4 verifies and extends it.
3. **A general-purpose content reader.** The fail-closed materializer is addressed only to the extent AC9 needs the index actually populated (see the **Critical dependency** in Dev Notes); do not add unbounded large-file ingestion, chunking, or reference-based ingestion beyond the approved C4/C9 policy.
4. **Changing the create/update gate order, the bridge schema beyond the minimal status/evidence additions, or the Tenants event subscription path.**
5. **The `SemanticIndexing → SearchIndexing` namespace rename** (tracked follow-up) and the RAG/`IngestAsync` subsystem.

## Acceptance Criteria

1. **Tombstoned entries are routed to removal egress, not skipped.** `SemanticIndexingProcessManager` no longer `continue`s past tombstoned entries. After applying events through `ISemanticIndexingBridgeWriter.ApplyFolderEventsAsync`, every entry that became `Tombstoned` is routed by `ReasonCode`: `folder_file_removed` → hard-delete egress; `folder_archived` → soft-delete egress. Non-tombstoned `Stale` entries keep the Story 10.3 upsert behavior unchanged.

2. **File removal publishes `SearchIndexEntryRemoved` (hard delete) — only for previously-indexed units.** For each tombstoned entry with reason `folder_file_removed` **that carries prior index-publish evidence (`PublishedEventId` is non-null)**, the worker publishes one `SearchIndexEntryRemoved { TenantId = "folders-index", AggregateId = <per-file-version id>, CorrelationId, CausationId }` via `DaprClient.PublishEventAsync("pubsub", "memories-events", …)` with metadata `cloudevent.type = nameof(SearchIndexEntryRemoved)`, `cloudevent.id = <the same stable source URI used at index time>`, `cloudevent.source = "hexalith-folders"`. Memories `KeyDeleteAsync` removes the doc (re-delivery is an idempotent no-op). A tombstoned entry with **no** `PublishedEventId` (never indexed — `Skipped`/`Failed`, or no content was ever published) **does not publish** (the doc was never in the index) and records a metadata-only no-op outcome (a stable reason such as `removal_not_required`); the path must null-guard `PublishedEventId`/source identity so a never-indexed removal never throws.

3. **Folder archive publishes `SearchIndexEntryChanged{status=archived}` (soft delete) — the Memories upsert is destructive, so the full document is re-sent.** For each tombstoned entry with reason `folder_archived` **that was previously indexed (`PublishedEventId` non-null)**, the worker publishes a `SearchIndexEntryChanged` carrying the **same** `TenantId`/`AggregateId`/`cloudevent.id` as the original upsert. Because Memories applies the upsert via `HashSetAsync` (a **full-hash overwrite** — there is no field-level patch), the worker MUST re-send the **complete** document: the original curated `Text` and the original `Attributes`, with **only** `folders.status` overwritten to `"archived"`. The original `Text`/`Attributes` are taken from the tombstoned entry's **preserved index-time `Evidence`** (no folder-state re-evaluation, no content re-read). If the entry's `Evidence` does not retain the index-time `Text`/`Attributes` (see the Resolved design decision in Dev Notes), the archive re-send falls back to a C9-safe descriptor form (`"{typeClassification} {fileVersionId}"`) — and this loss of the original rich searchable text is an accepted, documented consequence of the destructive upsert, not a silent bug. The doc remains in `folders-index`, filterable by `folders.status`. Never-indexed archived entries record a no-op outcome (as in AC2).

4. **`folders.status` is emitted on the live-index path too.** The Story 10.3 `SearchIndexEntryChanged` upsert path now sets `Attributes["folders.status"] = "active"` in `MemoriesSemanticIndexingPort.BuildAttributes` (which today has no such key), so the 10.5 facade can distinguish live vs archived without inferring it. The attribute key/values are stable, ordinal, lowercase, and metadata-only. This addition is **additive** — no existing test asserts the exact `Attributes` key-set (`SemanticIndexingWorkerRegistrationTests` asserts only individual keys and absences), so adding the key is non-breaking; add a positive assertion for `folders.status == "active"`.

5. **Identity equivalence is enforced and per-file-version.** The removal/soft-delete `AggregateId` and `cloudevent.id` are reconstructed from the tombstoned entry's preserved `Identity` (`ManagedTenantId/OrganizationId/FolderId/FileVersionId` + source URI) and are byte-identical to the upsert's values. When multiple file versions share a removed path, one `SearchIndexEntryRemoved` is emitted **per version**. No raw paths, file bytes, drive letters, diffs, or provider payloads appear in any event field, metadata, log, trace, metric, bridge record, test name, or failure message.

6. **The bridge records removal/soft-delete evidence without regression — via an evidence-only path.** Each removal/soft-delete outcome is recorded against the Tombstoned entry (published `cloudevent.id` → `PublishedEventId`; retryable failures; reconciliation). `ApplyIndexingResult` currently returns the entry **unchanged** when `Status is Tombstoned`, so this needs an explicit allowance: add a dedicated writer path (e.g. `RecordRemovalEvidenceAsync`) — OR a narrowly-scoped branch in `ApplyIndexingResult` — that updates **only** evidence fields (`PublishedEventId`/reason/retryable/observed-at) and **never** the `Status`, and only when the update's `Freshness.Watermark` is **≥** the current entry's (so an older removal cannot overwrite a newer file version's state). The Story 10.2/10.3 invariants must hold under test: a Tombstoned entry never regresses to `Indexed`, and stale/out-of-order results never overwrite newer ones.

7. **Outage / remote-error behavior never rolls back Folders operations.** A Dapr publish timeout, `DaprException`, invalid result, cancellation, or unavailable evidence on a removal/soft-delete is recorded as retryable `failed` or `reconciliation_required` with a stable reason code (e.g. `memories_publish_error`); `OperationCanceledException` on caller cancellation is rethrown. The worker never makes a durable Folders file/folder operation appear rejected because of a Memories/pub-sub outage.

8. **Dependency boundaries and DI validation stay closed.** `AddFoldersSemanticIndexingWorkers` registers the extended port under `ValidateOnBuild`/`ValidateScopes`; `Hexalith.Folders.Workers` remains the **only** Folders project referencing `Hexalith.Memories.Contracts` and still does **not** reference `Hexalith.Memories.Client.Rest`. `ScaffoldContractTests` continues to fail if Memories leaks elsewhere. (The 10.5 facade's single `Hexalith.Folders.Server` Memories.Client.Rest reference is out of 10.4's scope.)

9. **Live end-to-end round-trip proven on the real topology (BLOCKED-PENDING the DCP-capable `aspire run` lane).** Extending `FoldersTopologyCrossProcessTests`/`AspireFoldersAppHostFixture` (opt-in `HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION`, all 6 resources Running), the test proves the publish→route→index→search→remove round-trip against the live `folders-index`. To avoid being blocked twice (DCP lane **and** the unpopulated index from the fail-closed materializer), the gated test **seeds the index by publishing a real `SearchIndexEntryChanged` through the worker port** (not by relying on a real folder-mutation→content-read, which stays fail-closed), then asserts: (a) `GET /api/search?tenantId=folders-index&axis=syntactic&query=…` returns exactly one hit whose `ScoredResult.SourceUri` equals the published `cloudevent.id`; (b) after a `SearchIndexEntryRemoved` for that unit, search returns zero hits (no stale entry); (c) after a `SearchIndexEntryChanged{folders.status=archived}`, the doc remains with `folders.status=archived`. The deeper **real folder-mutation → content-materialization → index** proof remains separately blocked on a real (metadata-derived) materializer — tracked, not in 10.4 scope. This whole AC cannot run in the current local environment (env-wide Aspire CLI/DCP `--tls-cert-file` blocker, open Epic 9 action item); the test must skip cleanly everywhere the opt-in/DCP capability is absent — no lane goes red.

10. **Tests cover hard delete, soft delete, never-indexed, identity, evidence, outage, and boundaries.** Update `SemanticIndexingProcessManagerTests.ProcessFolderEventsAsyncShouldSkipTombstonedEntriesWithoutCallingMemories` (tombstoned **indexed** entries are no longer skipped — assert the correct removal/soft-delete egress instead). Add `MemoriesSemanticIndexingPort` tests (mocked `DaprClient`) asserting the published `SearchIndexEntryRemoved` shape + metadata (hard) and the full-document `SearchIndexEntryChanged{folders.status=archived}` re-send (soft, with original `Text`/`Attributes` preserved). Add a **never-indexed removal/archive** test (`PublishedEventId == null` → no CloudEvent published, metadata-only no-op outcome, no crash, no raw-path leak). Add bridge evidence-recording + no-regression/watermark tests; identity-equivalence (remove targets the same `AggregateId`/`cloudevent.id` the upsert used) and per-version tests; the additive `folders.status == "active"` assertion on the upsert path; outage/reconciliation and metadata-only tests; and extend the gated `FoldersTopologyCrossProcessTests` for AC9.

11. **Verification passes.** `dotnet build Hexalith.Folders.slnx` succeeds (0W/0E). Narrowed lanes green: `tests/Hexalith.Folders.Workers.Tests`, `tests/Hexalith.Folders.Tests`, `tests/Hexalith.Folders.Testing.Tests`, and `tests/Hexalith.Folders.Contracts.Tests` (incl. the Dapr policy conformance gate — expected unchanged). `tests/Hexalith.Folders.AppHost.Tests` skips cleanly (opt-in unset). `dotnet format whitespace --verify-no-changes` clean over Folders-owned `src`/`tests`. If a command is blocked by local environment state, record the blocker and do not mark implementation complete. The AC9 live proof is recorded only when run in a DCP-capable lane.

## Tasks / Subtasks

- [x] **Task 1 — Add the removal + soft-delete egress on the port (AC: 2, 3, 4, 5, 7)**
  - [x] Extend `ISemanticIndexingPort` with a removal method (e.g. `RemoveFileVersionAsync(SemanticIndexingRemovalRequest, CancellationToken)`) and either a soft-delete method or a reuse of `IndexFileVersionAsync` parameterized with a status (choose one; document the choice). Keep the interface minimal and symmetric with `IndexFileVersionAsync`. **Chose two dedicated methods — `RemoveFileVersionAsync(SemanticIndexingRemovalRequest)` + `SoftDeleteFileVersionAsync(SemanticIndexingArchiveRequest)` — for symmetry and because the soft delete re-sends stored evidence text/attributes, not a freshly-materialized request.**
  - [x] In `MemoriesSemanticIndexingPort`, publish `SearchIndexEntryRemoved` for hard delete; for soft delete re-send the **full** `SearchIndexEntryChanged` (original `Text` + `Attributes` from preserved evidence) with only `Attributes["folders.status"]="archived"` overwritten (the upsert is a destructive full-hash overwrite — decision (A)). Reuse `FoldersSemanticIndexingDefaults` constants and the existing CloudEvent-metadata helper. Add `["folders.status"]="active"` to `BuildAttributes` for the live upsert path.
  - [x] Reconstruct `AggregateId` (`{tenant}/{org}/{folder}/{fileVersionId}`) and `cloudevent.id` (stable source URI) from the **tombstoned entry's preserved `Identity`** (not the remove event — decision (C)) so removal targets exactly the upserted doc. Gate emission on `PublishedEventId != null` (decision (B)); never-indexed tombstones record `removal_not_required` with a null-guard. Add a removal request record carrying that preserved identity + correlation/causation, with no raw path/bytes. **`cloudevent.id` reuses the stored `Evidence.PublishedEventId` (= the upsert's cloudevent.id) — guaranteed byte-identical.**
  - [x] Map Dapr publish failures to retryable `Failed`/`ReconciliationRequired` (`memories_publish_error`); rethrow `OperationCanceledException` on caller cancellation. Mirror 10.3's error handling.

- [x] **Task 2 — Route tombstoned entries in the process manager (AC: 1, 5, 6)**
  - [x] Replace the `Identity.ContentHashReference is null` skip with reason-code routing over tombstoned entries (`folder_file_removed` → hard, `folder_archived` → soft). **The loop now processes `Stale or Tombstoned`; Stale keeps the `ContentHashReference is null` skip + upsert; Tombstoned routes by reason.**
  - [x] Emit one CloudEvent per previously-indexed file-version entry; gate emission on prior index-publish evidence (`PublishedEventId` present) so never-indexed units don't publish needlessly (idempotent delete makes this an optimization, not a correctness requirement — document the rule).
  - [x] Record each outcome through the bridge writer without regressing the Tombstoned status.

- [x] **Task 3 — Allow evidence-only updates on Tombstoned entries (AC: 6)**
  - [x] Add an **evidence-only** path (a dedicated `RecordRemovalEvidenceAsync`, or a narrow branch in `ApplyIndexingResult`) that updates `PublishedEventId`/reason/retryable/observed-at on a Tombstoned entry but **never** changes `Status`, and only when the update watermark is ≥ the current entry's. **Added `RecordRemovalEvidenceAsync` + `SemanticIndexingRemovalEvidenceUpdate` + `SemanticIndexingBridgeProjection.ApplyRemovalEvidence` (status frozen, watermark-gated).**
  - [x] Confirm `InMemorySemanticIndexingBridgeStore` and `EventStoreSemanticIndexingBridgeStore` both honor the new path with tenant-prefixed keys intact; add tests proving no Tombstoned→Indexed regression and no stale/out-of-order overwrite.

- [x] **Task 4 — Verify (do NOT re-implement) the inbound D1 wiring is present and sufficient for removals (AC: 8)**
  - [x] Confirm `publishingScopes` already authorizes `folders-workers=memories-events` (the removal/soft-delete publish reuses this exact outbound topic) — **no new scope expected**. **Verified in `deploy/dapr/production/pubsub.yaml`: `publishingScopes` line has `folders-workers=memories-events`. No change made.**
  - [x] Confirm the inbound `folders.events` subscription (`subscriptionScopes` `folders-workers=system.tenants.events,folders.events`) routes **both** `WorkspaceFileMutationAccepted` (remove) and `FolderArchived` to the worker on the **same** topic — there is no separate removal topic. Record this verification. **Verified: `subscriptionScopes` has `folders-workers=system.tenants.events,folders.events`; `allowedTopics` includes `folders.events`. Both remove + archive ride the same topic.**
  - [x] Treat `deploy/dapr/production/pubsub.yaml`, `sidecar-config-bindings.yaml`, and `DaprPolicyConformanceTests` as **already-correct committed artifacts** (`27c22ff`); do **not** re-edit them unless the removal path demonstrably requires a change (it should not). **No Dapr policy artifact edited; `DaprPolicyConformanceTests` (in Contracts.Tests) remains green (275/275).**

- [x] **Task 5 — Extend the gated cross-process round-trip proof (AC: 9)**
  - [x] Extend `FoldersTopologyCrossProcessTests`/`AspireFoldersAppHostFixture`: **seed** the index by publishing a real `SearchIndexEntryChanged` through the worker port (option (b) — do not depend on the fail-closed materializer), then assert seed→search (1 hit, `SourceUri == cloudevent.id`), remove→search (0 hits), archive→search (`folders.status=archived`). Use a Memories `GET /api/search?tenantId=folders-index&axis=syntactic&query=…` client for verification; `App.CreateHttpClient("folders")` is available for the eventual real-mutation path (tracked, not asserted here). **Added `SeedRemoveAndArchiveRoundTripAgainstFoldersIndex`; publishes through the worker's `pubsub`/`memories-events` component via a DaprClient bound to the folders-workers sidecar; AppHost.Tests now references `Hexalith.Folders.Workers` (ScaffoldContractTests dependency-direction pin reconciled accordingly).**
  - [x] Keep the opt-in `HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION` skip semantics (verified clean SKIP when unset). Document in the test/README that AC9 runs only on a DCP-capable lane and depends on a populated index (Critical dependency). **AppHost.Tests = 3 SKIP clean; README updated with the AC9 round-trip section.**

- [x] **Task 6 — Tests (AC: 10) and verification (AC: 11)**
  - [x] Update the skip-tombstoned process-manager test; add port hard/soft-delete shape+metadata tests; add bridge evidence/no-regression tests; add identity-equivalence + per-version tests; add outage/reconciliation + metadata-only tests.
  - [x] Run the build + narrowed lanes; confirm `AppHost.Tests` skips; run `dotnet format`. Record any environment blocker; do not mark complete on a blocked command. **Build 0W/0E; Workers.Tests 61/0, Folders.Tests 1331/0, Contracts.Tests 275/0, Testing.Tests 59/1 (the 1 fail = `SolutionContainsOnlyCanonicalBuildableProjects`, PRE-EXISTING at HEAD `4d9cd21` — Epic 9/10.1/10.5 solution-inventory drift, not 10.4); AppHost.Tests 3 SKIP; `dotnet format whitespace --verify-no-changes` clean (EXIT=0).**

## Dev Notes

### Mechanism (verified against the Memories submodule at HEAD)

The Memories search index is maintained by **two distinct CloudEvent types** routed by `cloudevent.type` (there is no single union record; `CuratedSearchIndexEventTypes.IsCuratedType` recognizes both):

- `SearchIndexEntryChanged` (`Hexalith.Memories.Contracts.V1`): required `TenantId`, `AggregateId`, `Text`; optional `Attributes` (`Dictionary<string,string>`), `CorrelationId`, `CausationId`. → `HashSetAsync` **upsert** at Redis key `{TenantId}:mu:{AggregateId}` (`RedisSearchIndexMaintenanceAdapter`). Doc present + BM25-searchable.
- `SearchIndexEntryRemoved` (`Hexalith.Memories.Contracts.V1`): required `TenantId`, `AggregateId`; optional `CorrelationId`, `CausationId`. → `KeyDeleteAsync({TenantId}:mu:{AggregateId})` — **idempotent** (deleting a missing key is a no-op). Doc gone, drops from RediSearch.

Both upsert/delete by the composite key `(TenantId, AggregateId)`. `cloudevent.id` is echoed verbatim as `ScoredResult.SourceUri`. The proven `Hexalith.Tenants` precedent (`MemoriesSearchIndexEventPublisher`) publishes **only** `SearchIndexEntryChanged` — even `TenantDisabled` becomes `Changed{status=Disabled}`. The Folders hybrid intentionally adds the hard-delete `Removed` for file removals (a removed file genuinely no longer exists) while keeping the Tenants soft-delete shape for archive (recoverable).

**Verbatim contracts:**

```csharp
public sealed record SearchIndexEntryChanged {
    public required string TenantId { get; init; }     // = "folders-index"
    public required string AggregateId { get; init; }  // {tenant}/{org}/{folder}/{fileVersionId}
    public required string Text { get; init; }          // curated, C9-safe (descriptor-derived)
    public Dictionary<string, string> Attributes { get; init; } = new(StringComparer.Ordinal);
    public string? CorrelationId { get; init; }
    public string? CausationId { get; init; }
}
public sealed record SearchIndexEntryRemoved {
    public required string TenantId { get; init; }     // = "folders-index"
    public required string AggregateId { get; init; }  // MUST equal the upsert's AggregateId
    public string? CorrelationId { get; init; }
    public string? CausationId { get; init; }
}
```

**Hard-delete publish pattern (reuse 10.3's `DaprClient` + constants):**

```csharp
var removed = new SearchIndexEntryRemoved {
    TenantId = FoldersSemanticIndexingDefaults.IndexTenant,   // "folders-index"
    AggregateId = aggregateId,                                 // reconstructed, == upsert value
    CorrelationId = tombstone.CorrelationId,
    CausationId = tombstone.TaskId,
};
var metadata = new Dictionary<string, string>(StringComparer.Ordinal) {
    ["cloudevent.id"] = sourceUri,                             // == the upsert's cloudevent.id
    ["cloudevent.type"] = nameof(SearchIndexEntryRemoved),
    ["cloudevent.source"] = FoldersSemanticIndexingDefaults.CloudEventsSource, // "hexalith-folders"
};
await _daprClient.PublishEventAsync(
    FoldersSemanticIndexingDefaults.PubSubName,                // "pubsub"
    FoldersSemanticIndexingDefaults.EventsTopicName,           // "memories-events"
    removed, metadata, cancellationToken).ConfigureAwait(false);
```

### Current code state to extend (verified at HEAD `4d9cd21`)

- `src/Hexalith.Folders.Workers/SemanticIndexing/ISemanticIndexingPort.cs` — **only** `IndexFileVersionAsync(SemanticIndexingRequest, CancellationToken)` today. Add the removal method here.
- `src/Hexalith.Folders.Workers/SemanticIndexing/MemoriesSemanticIndexingPort.cs` — `IndexFileVersionAsync` builds `SearchIndexEntryChanged`, `FileVersionAggregateId(request)` = `{ManagedTenantId}/{OrganizationId}/{FolderId}/{FileVersionId}`, `cloudevent.id` = the stable source URI, returns `SemanticIndexingResult(status, reasonCode, retryable, publishedEventId)`. Reuse its metadata builder, constants, and failure mapping (`DaprException`/timeout → retryable `Failed`; `OperationCanceledException` rethrow).
- `src/Hexalith.Folders.Workers/SemanticIndexing/SemanticIndexingProcessManager.cs` — after `ApplyFolderEventsAsync`, iterates entries with `Status == Stale` and **skips** those where `Identity.ContentHashReference is null`. **This skip is the gap**: tombstoned removed/archived entries are currently never propagated to Memories, so the index keeps stale docs forever. Replace the skip with reason-code routing.
- `src/Hexalith.Folders.Workers/SemanticIndexing/FoldersSemanticIndexingDefaults.cs` — `MaxInlineIngestionBytes=262144`, `CloudEventsSource="hexalith-folders"`, `IndexTenant="folders-index"`, `DomainEventsRoute="/folders/events"`, `DomainEventsTopicName="folders.events"`, `PubSubName="pubsub"`, `EventsTopicName="memories-events"`. Reuse — do not duplicate literals.
- `src/Hexalith.Folders/Projections/SemanticIndexing/` — `SemanticIndexingBridgeStatus` enum (`Unknown, Indexed, Stale, Skipped, Failed, Tombstoned, ReconciliationRequired`; codes `indexed/stale/skipped/failed/tombstoned/reconciliation_required`). `SemanticIndexingBridgeProjection`:
  - `ApplyFileMutation` tombstones on `FileOperationKind == "remove"` (reason `"folder_file_removed"`), matching existing path-keyed entries (`PathMetadataDigest`) and **preserving each entry's `Identity`** (incl. the original `ContentHashReference`, `FileVersionId`, `SourceUri`). This preserved identity is what the removal egress must reuse.
  - `ApplyFolderArchived` tombstones all folder-keyed entries (reason `"folder_archived"`) with a watermark guard.
  - `ApplyIndexingResult` **returns `current` unchanged when `current.Status is Tombstoned`** — so recording removal evidence needs an explicitly-allowed path (Task 3).
  - `ISemanticIndexingBridgeWriter`: `ApplyFolderEventsAsync(envelopes, ct)` → entries that became Stale; `RecordIndexingResultAsync(SemanticIndexingResultUpdate, ct)` → entry?. `SemanticIndexingEvidence.PublishedEventId` is the post-publish handle (renamed from `WorkflowId`/`MemoryUnitId` in 10.3).
- Events: `WorkspaceFileMutationAccepted` (kind `"remove"` → `ContentHashReference == null` on the remove event itself; safe metadata only) and `FolderArchived(ManagedTenantId, OrganizationId, FolderId, ArchiveReasonCode, ActorPrincipalId, CorrelationId, TaskId, IdempotencyKey, IdempotencyFingerprint, OccurredAt)`. **Do not read file-version identity from the remove event** — read it from the tombstoned bridge entries it matched.
- `src/Hexalith.Folders.Workers/FoldersWorkersModule.cs` — `AddFoldersSemanticIndexingWorkers` registers `ISemanticIndexingPort → MemoriesSemanticIndexingPort` (transient), `SemanticIndexingProcessManager`, `FoldersSemanticIndexingEventProcessor`, bridge writer/read model (singleton), `FailClosed*` policy/materializer, `DaprClient`. `Hexalith.Folders.Workers.csproj` references `Hexalith.Memories.Contracts`, **not** `Client.Rest` — keep it that way.

### Resolved design decisions (adversarial review, 2026-06-23)

- **(A) Destructive upsert ⇒ archive re-sends the full document.** Memories applies `SearchIndexEntryChanged` via `HashSetAsync` (`RedisSearchIndexMaintenanceAdapter`), a **full-hash overwrite** — there is no field-level patch. So the archive soft-delete cannot flip `folders.status` alone; it must re-send the complete `Text` + `Attributes`, overwriting only `folders.status="archived"`. **Source of those values:** the tombstoned entry's preserved index-time `Evidence`. This implies the bridge `Evidence` must retain the index-time curated `Text` + `Attributes` (extend `SemanticIndexingEvidence` if it does not). If they are not retained, the archive re-send falls back to the C9-safe descriptor form and the original rich searchable `Text` is lost — an **accepted, documented** consequence (archived units are filtered out by 10.5 anyway), not a silent defect. Do not frame archive as fully "recoverable in-index."
- **(B) Emit only for previously-indexed units.** Removal/soft-delete publishes only when the tombstoned entry has `PublishedEventId` (prior index evidence). Never-indexed tombstones (`Skipped`/`Failed`/no prior publish) skip the egress and record a metadata-only no-op (`removal_not_required`). Idempotent `KeyDeleteAsync` makes emitting-anyway harmless, but skipping avoids needless pub/sub traffic and the null-source-identity edge.
- **(C) Identity comes from the tombstoned entry, not the remove event.** `WorkspaceFileMutationAccepted(remove)` carries no `FileVersionId`/source URI (and `ContentHashReference == null`); reconstruct `AggregateId`/`cloudevent.id` from each matched tombstoned entry's preserved `Identity`. One `SearchIndexEntryRemoved` per matched file-version.
- **(D) Idempotency / ordering.** Hard delete (`SearchIndexEntryRemoved` → `KeyDeleteAsync`) is order-independent and re-delivery-safe. Soft delete (`SearchIndexEntryChanged` upsert) is safe **in order**; out-of-order is prevented at the projection (`ApplyFolderArchived`'s `Freshness.Watermark > envelope.Sequence` guard) and at evidence-recording (decision in AC6: watermark-gated, status-frozen). Folders assumes Dapr per-subscriber ordering; it does not rely on Memories-side dedup.
- **(E) Evidence-only recording on Tombstoned.** See AC6 — never change `Status`, watermark-gate the update.

### Critical dependency — the index is not populated yet (affects AC9 only)

The content materializer is `FailClosedSemanticIndexingContentMaterializer`, which returns unavailable → the upsert path records `skipped` and **publishes nothing**. So today **nothing real reaches `folders-index`**, and AC9's "exactly one hit" cannot be observed from a real mutation until the index is populated. 10.3's changelog states the deeper mutation→receipt assertion "is moot end-to-end until a real content reader replaces the fail-closed materializer."

**Chosen resolution (avoids double-blocking AC9):** AC9's gated test uses **option (b)** — it seeds the index by publishing a real `SearchIndexEntryChanged` **through the worker port** within the test, then proves the remove (→ 0 hits) and archive (→ `folders.status=archived`) mechanics against the live `folders-index`. This exercises the full publish→route→index→search→remove path without depending on the fail-closed materializer. The deeper **real folder-mutation → content-read → index** proof is option (a) and stays a **tracked, separate prerequisite** (a metadata-derived materializer):
- **(a) Metadata-derived materializer (the eventual real-population path, NOT 10.4 scope):** because the curated `Text` is descriptor-derived and bytes are read only for the size-eligibility gate (10.3 removed `ContentBytes` from the request), a future materializer can produce the curated `Text` + size/type classification **from the accepted event's safe metadata** (`ByteLength`, `MediaType`, `PathPolicyClass`, `PathMetadataDigest`, `ContentHashReference`) without reading file content. Track it; do not implement in 10.4 unless the PO expands scope.
- Either way, AC9 stays **BLOCKED-PENDING the DCP-capable lane**; option (b) only proves the removal/archive mechanics there, not real-mutation population.

### Cross-story coordination with 10.5 (already authored)

10.5 (the read facade) security-trims and filters `folders-index`. The hybrid soft-delete means **archived docs remain searchable**, so 10.5 must filter `folders.status != live`. 10.4's job is only to **emit** `folders.status` (`active`/`archived`) reliably; whether 10.5's ACs already include the status filter is 10.5's concern — flag it if it does not, but do not modify 10.5 from here.

### Architecture & guardrails

- Repo config is authoritative when prose drifts: .NET `10.0.302`, Dapr packages pinned in `Directory.Packages.props`, Aspire `13.4.6`, xUnit v3 `3.2.2`, Shouldly `4.3.0`, NSubstitute `5.3.0`. Central package management — no inline versions.
- File-scoped namespaces; `ArgumentNullException.ThrowIfNull` on public boundaries; ordinal comparisons + invariant formatting for ids/keys; `CancellationToken` propagation; `ConfigureAwait(false)` where nearby worker code uses it.
- Workers own external side effects; aggregates stay deterministic/side-effect-free; expected remote outcomes are result-shaped + bridge-recorded, not thrown.
- Metadata-only is non-negotiable across events, attributes, logs, traces, metrics, bridge records, Problem Details, docs, and **test names/failure messages**.
- Tests are hermetic: no running Dapr sidecars, Memories server, Redis, Keycloak, provider credentials, secrets, network, tenant seed data, or nested submodule init — except the explicitly opt-in/DCP-gated `AppHost.Tests` lane (AC9), which skips cleanly otherwise.
- Dapr at-least-once delivery + CloudEvents wrapping → the removal path must stay idempotent (it is — `KeyDeleteAsync` + upsert-by-key).

### Project Structure Notes

- New/changed worker source under `src/Hexalith.Folders.Workers/SemanticIndexing/` (port interface + adapter + process manager). Bridge evidence path under `src/Hexalith.Folders/Projections/SemanticIndexing/`. No new project.
- Tests extend the existing `tests/Hexalith.Folders.Workers.Tests/` files and the existing `tests/Hexalith.Folders.Tests/Projections/SemanticIndexing/`; AC9 extends `tests/Hexalith.Folders.AppHost.Tests/` (do not create a parallel harness).
- No Contract Spine / OpenAPI change (no public REST/SDK surface in 10.4). No Dapr policy change expected (Task 4 verifies).

### References

- `_bmad-output/planning-artifacts/epics.md` — Epic 10 + Story 10.4 (`SearchIndexEntryRemoved` on removal/archive + prove end-to-end routing) and the 2026-06-23 mechanism correction note.
- `_bmad-output/implementation-artifacts/10-3-author-authorized-async-indexing-on-file-write-and-commit.md` — the upsert publish pattern, the per-file-version `AggregateId`, the bridge/projection vocabulary, the D1 closure changelog, and the fail-closed-materializer caveat.
- `_bmad-output/implementation-artifacts/10-5-expose-authorized-folders-query-facade-over-memories.md` — the read facade that depends on 10.4 and consumes `folders.status`.
- `_bmad-output/planning-artifacts/memories-search-index-handoff-2026-06-23.md` — §4 (Memories ingestion recognizes `SearchIndexEntryChanged`/`SearchIndexEntryRemoved`, upserts/deletes by `(TenantId, AggregateId)`) and §6 (verification checklist).
- `_bmad-output/planning-artifacts/architecture.md` §130–156 / §401 — Memories integration track, `SearchIndexEntryChanged`/`SearchIndexEntryRemoved` over pub/sub, dormant-until-Epic-10 routing.
- `Hexalith.Tenants/samples/Hexalith.Tenants.Sample/Handlers/MemoriesSearchIndexEventPublisher.cs` (+ its test) — the canonical publish precedent (soft-delete via `status`; never `IngestAsync`).
- `Hexalith.Memories/src/Hexalith.Memories.Contracts/V1/SearchIndexEntryChanged.cs`, `SearchIndexEntryRemoved.cs`; `…/Hexalith.Memories.EventStore/{CuratedSearchIndexEventTypes,ISearchIndexMaintenance}.cs`; `…/Hexalith.Memories.Server/EventStoreIntegration/RedisSearchIndexMaintenanceAdapter.cs`; `…/Search/SyntacticSearchService.cs`; `…/Contracts/V1/{SearchResult,ScoredResult}.cs` — the Memories ingestion/search side.
- `src/Hexalith.Folders.Workers/SemanticIndexing/*` and `src/Hexalith.Folders/Projections/SemanticIndexing/*` — current worker/bridge code.
- `tests/Hexalith.Folders.AppHost.Tests/{AspireFoldersAppHostFixture,FoldersTopologyCrossProcessTests}.cs` — the opt-in Tier-3 harness AC9 extends (`HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION`).
- `deploy/dapr/production/pubsub.yaml`, `sidecar-config-bindings.yaml`, `tests/Hexalith.Folders.Contracts.Tests/OpenApi/DaprPolicyConformanceTests.cs` — the committed D1 inbound wiring 10.4 verifies (does not re-implement).
- `docs/exit-criteria/c4-input-limits.md`, `docs/operations/audit-and-redaction.md` — C4 bounds and S-6/C9 sensitive-metadata rules.

## Dev Agent Record

### Agent Model Used

claude-opus-4-8[1m] (Claude Opus 4.8, 1M context; ultracode session) via bmad-dev-story.

### Debug Log References

- Build: `dotnet build Hexalith.Folders.slnx` → succeeded, 0 Warning(s) / 0 Error(s).
- `dotnet format whitespace Hexalith.Folders.slnx --verify-no-changes --include <changed files>` → EXIT=0 (clean).
- Narrowed lanes (`--no-build`):
  - `tests/Hexalith.Folders.Workers.Tests` → 61/61 passed.
  - `tests/Hexalith.Folders.Tests` → 1331/1331 passed.
  - `tests/Hexalith.Folders.Contracts.Tests` (incl. `DaprPolicyConformanceTests`) → 275/275 passed.
  - `tests/Hexalith.Folders.Testing.Tests` → 59 passed, **1 PRE-EXISTING failure** (`ScaffoldContractTests.SolutionContainsOnlyCanonicalBuildableProjects`). Proven failing at HEAD `4d9cd21` BEFORE this story: the `.slnx` already carries 6 submodule projects (`Hexalith.Memories.Contracts`, `Hexalith.Memories.Aspire`, `Hexalith.EventStore.Aspire`, `Hexalith.Tenants.Aspire`, `Hexalith.Commons.ServiceDefaults`) that `ExpectedSolutionProjects` (last reconciled before Epic 9/10.1/10.5) does not list. Neither the `.slnx` nor `ScaffoldContractTests.cs`'s `ExpectedSolutionProjects` is in this story's changeset. This is Epic 9 / Story 10.1 / Story 10.5 solution-inventory governance debt, NOT a 10.4 regression — flagged for those owners, deliberately not reconciled here to avoid blessing another epic's solution changes from 10.4.
  - `tests/Hexalith.Folders.AppHost.Tests` → 3 SKIP (opt-in `HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION` unset; AC9 BLOCKED-PENDING the DCP-capable lane).

### Completion Notes List

- **AC1** — `SemanticIndexingProcessManager.ProcessFolderEventsAsync` now iterates `Stale or Tombstoned` entries: Stale → unchanged Story 10.3 upsert (`ProcessStaleAsync`, keeping the `ContentHashReference is null` skip); Tombstoned → `ProcessTombstoneAsync` routes by reason (`folder_file_removed` → hard, `folder_archived` → soft). No more silent skip.
- **AC2** — Hard delete publishes `SearchIndexEntryRemoved{TenantId=folders-index, AggregateId=<per-file-version>}` with `cloudevent.type=SearchIndexEntryRemoved`, `cloudevent.id`=the stored upsert source URI, `cloudevent.source=hexalith-folders`. Emission gated on `Evidence.PublishedEventId != null` (decision B); never-indexed tombstones record `removal_not_required` (no publish, null-guarded).
- **AC3** — Archive soft delete re-sends the **full** `SearchIndexEntryChanged` (original `Text` + `Attributes` from preserved `Evidence.IndexedText`/`IndexedAttributes`) with only `folders.status` overwritten to `archived` (decision A, destructive `HashSetAsync` overwrite). C9-safe descriptor fallback when evidence retains no text/attributes (documented loss).
- **AC4** — `MemoriesSemanticIndexingPort.BuildAttributes` now stamps `folders.status=active` on the live upsert (positive assertion added; additive — no exact-key-set test existed).
- **AC5** — Removal/archive `AggregateId` reconstructed identically (`{tenant}/{org}/{folder}/{fileVersionId}`) via a shared `FileVersionAggregateId`, and `cloudevent.id` = the stored `PublishedEventId`; a dedicated port test proves the removal's `AggregateId`/`cloudevent.id` are byte-identical to the upsert's. Identity comes from the tombstoned entry's preserved `Identity`; one event per file version.
- **AC6** — `SemanticIndexingEvidence` extended with `IndexedText`/`IndexedAttributes`; `Tombstone()` now preserves the matched entry's `Evidence`; `ApplyIndexingResult` persists the curated document; new `RecordRemovalEvidenceAsync` / `ApplyRemovalEvidence` update only evidence/outcome fields, freeze `Status=Tombstoned`, and are watermark-gated (≥) — proven no Tombstoned→Indexed regression and no stale overwrite in both stores.
- **AC7** — Removal/soft-delete Dapr failures map to retryable `Failed` (`memories_publish_error`) recorded as evidence; `OperationCanceledException` on caller cancellation rethrows; never thrown past the processor.
- **AC8** — `AddFoldersSemanticIndexingWorkers` still validates under `ValidateOnBuild`/`ValidateScopes` (registration tests green). `ForbiddenReferencesAreNotIntroduced` still green: no PRODUCTION Folders project references `Hexalith.Memories.Contracts`/`Client.Rest`; the AC9 harness's transitive Memories.Contracts is on the **test** project `Hexalith.Folders.AppHost.Tests`, which is not subject to the production isolation rule.
- **AC9** — `SeedRemoveAndArchiveRoundTripAgainstFoldersIndex` extends the gated Tier-3 harness: seeds via a real `SearchIndexEntryChanged` published through the worker `pubsub`/`memories-events` component (option b), then asserts seed→1 hit (`SourceUri == cloudevent.id`), remove→0 hits, archive→remains filterable as `folders.status=archived`. BLOCKED-PENDING the DCP-capable lane; skips cleanly everywhere the opt-in/DCP capability is absent (verified 3 SKIP). The deeper real folder-mutation→content-materialization→index proof stays tracked (fail-closed materializer).
- **AC10/AC11** — Tests + verification as recorded in the Debug Log.
- **Decision on re-routing/re-publish:** removal/archive outcome is recorded as the entry's `ReasonCode` (e.g. `memories_accepted`/`removal_not_required`/`memories_publish_error`) so a re-delivered same event (duplicate fingerprint → projection ignores it) is not re-routed; genuinely idempotent re-delivery (`KeyDeleteAsync` no-op / upsert-by-key) keeps it safe regardless (decision D).

### File List

Production (src):
- `src/Hexalith.Folders.Workers/SemanticIndexing/FoldersSemanticIndexingDefaults.cs` (status attribute constants)
- `src/Hexalith.Folders.Workers/SemanticIndexing/ISemanticIndexingPort.cs` (removal + soft-delete methods)
- `src/Hexalith.Folders.Workers/SemanticIndexing/MemoriesSemanticIndexingPort.cs` (egress + folders.status=active + helpers)
- `src/Hexalith.Folders.Workers/SemanticIndexing/SemanticIndexingResult.cs` (IndexedText/IndexedAttributes)
- `src/Hexalith.Folders.Workers/SemanticIndexing/SemanticIndexingRemovalRequest.cs` (new)
- `src/Hexalith.Folders.Workers/SemanticIndexing/SemanticIndexingArchiveRequest.cs` (new)
- `src/Hexalith.Folders.Workers/SemanticIndexing/SemanticIndexingProcessManager.cs` (Stale/Tombstoned routing)
- `src/Hexalith.Folders.Workers/SemanticIndexing/EventStoreSemanticIndexingBridgeStore.cs` (RecordRemovalEvidenceAsync)
- `src/Hexalith.Folders/Projections/SemanticIndexing/SemanticIndexingEvidence.cs` (IndexedText/IndexedAttributes)
- `src/Hexalith.Folders/Projections/SemanticIndexing/SemanticIndexingResultUpdate.cs` (IndexedText/IndexedAttributes)
- `src/Hexalith.Folders/Projections/SemanticIndexing/SemanticIndexingRemovalEvidenceUpdate.cs` (new)
- `src/Hexalith.Folders/Projections/SemanticIndexing/ISemanticIndexingBridgeWriter.cs` (RecordRemovalEvidenceAsync)
- `src/Hexalith.Folders/Projections/SemanticIndexing/SemanticIndexingBridgeProjection.cs` (ApplyRemovalEvidence, ApplyIndexingResult evidence, Tombstone preserves evidence, IdentityMatches)
- `src/Hexalith.Folders/Projections/SemanticIndexing/InMemorySemanticIndexingBridgeStore.cs` (RecordRemovalEvidenceAsync)

Tests:
- `tests/Hexalith.Folders.Workers.Tests/SemanticIndexingProcessManagerTests.cs` (no-op/hard-delete/soft-delete/outage routing tests + fakes)
- `tests/Hexalith.Folders.Workers.Tests/SemanticIndexingWorkerRegistrationTests.cs` (port hard/soft-delete shape, identity equivalence, fallback, failure, cancel, folders.status=active)
- `tests/Hexalith.Folders.Workers.Tests/SemanticIndexingEndpointE2ETests.cs` (fake port new methods)
- `tests/Hexalith.Folders.Workers.Tests/EventStoreSemanticIndexingBridgeStoreTests.cs` (RecordRemovalEvidenceAsync)
- `tests/Hexalith.Folders.Tests/Projections/SemanticIndexing/SemanticIndexingBridgeProjectionTests.cs` (evidence preservation, ApplyRemovalEvidence freeze/watermark/non-tombstoned)
- `tests/Hexalith.Folders.AppHost.Tests/FoldersTopologyCrossProcessTests.cs` (AC9 round-trip)
- `tests/Hexalith.Folders.AppHost.Tests/Hexalith.Folders.AppHost.Tests.csproj` (Workers + Dapr.Client refs)
- `tests/Hexalith.Folders.AppHost.Tests/README.md` (AC9 documentation)
- `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs` (AppHost.Tests dependency-direction pin reconciled for the Workers ref)

Sprint tracking:
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (10-4 → in-progress → review)

## Change Log

- 2026-06-24: Code review via bmad-code-review (Claude Opus 4.8, ultracode). 5 adversarial reviewers (Blind Hunter ×2, Edge Case Hunter, Acceptance Auditor ×2) → 10 raw findings → per-finding adversarial verification against the real working tree → 3 confirmed (1 HIGH, 1 MEDIUM, 1 LOW), 7 dismissed false positives. All 3 patches applied & verified (see Review Findings → Resolution): hard-delete-vs-archive resurrection guard in `ApplyFolderArchived` (+2 regression tests), AC9 `attr:`→`attribute.` query-param fix, and the never-indexed-archive no-op test. Build 0E; Workers 62/62, Folders 1354/1354, AppHost 3 SKIP; format clean. Status review → done (AC9 live proof stays BLOCKED-PENDING the DCP lane — environment limitation, not a review finding).
- 2026-06-23: Implemented via bmad-dev-story (Claude Opus 4.8, ultracode). Hybrid removal/archive egress shipped: hard `SearchIndexEntryRemoved` for file removals, soft `SearchIndexEntryChanged{folders.status=archived}` full-document re-send for folder archive, plus `folders.status=active` on the live upsert. Bridge extended with index-time evidence (`IndexedText`/`IndexedAttributes`) preserved across tombstoning, and an evidence-only `RecordRemovalEvidenceAsync` (status frozen, watermark-gated). Task 4 verified the committed D1 inbound wiring needs no Dapr scope change. AC9 round-trip added to the opt-in Tier-3 harness (BLOCKED-PENDING the DCP lane; skips clean). Build 0W/0E; Workers/Folders/Contracts lanes green; format clean; AppHost.Tests 3 SKIP. One PRE-EXISTING Testing.Tests failure (`SolutionContainsOnlyCanonicalBuildableProjects`) confirmed failing at HEAD `4d9cd21` — Epic 9/10.1/10.5 solution-inventory drift, not a 10.4 regression. Status → review.
- 2026-06-23: Story context created by bmad-create-story (Claude Opus 4.8, ultracode). Scope corrected from the epics.md seed after reconciling repo HEAD `4d9cd21`: Jerome chose the **hybrid** removal model (file-remove → hard `SearchIndexEntryRemoved`; archive → soft `SearchIndexEntryChanged{status=archived}`), **block-on-`aspire run`** live verification (AC9 BLOCKED-PENDING the DCP lane, extending the existing opt-in Tier-3 harness), and end-to-end ownership — but the D1 inbound wiring was found **already committed** (`27c22ff`/`3dc0db4`), so 10.4 **verifies + extends** it rather than re-implementing. Flagged the fail-closed-materializer dependency for AC9 and the `folders.status` coordination with the already-authored 10.5.
- 2026-06-23: Hardened via a 4-agent adversarial review workflow (fact-verifier confirmed all repo facts; mechanism-verifier confirmed idempotency/`SourceUri`/Tenants-precedent/no-new-scope and caught the destructive `HashSetAsync` overwrite; dev-trap-critic found underspecified paths). Applied: decision (A) archive re-sends the full document (destructive upsert) + documented fallback loss; (B) emit only for previously-indexed units (`PublishedEventId`-gated) + never-indexed no-op; (C) reconstruct identity from the tombstoned entry, not the remove event; (D) hard-delete order-safe / soft-delete watermark-gated; (E) evidence-only recording on Tombstoned (status frozen). Resolved the AC9 double-block by seeding via the worker port (option b); confirmed adding `folders.status="active"` is additive (no exact-set test assertion).

### Review Findings

_Code review 2026-06-24 (bmad-code-review, ultracode): 5 adversarial reviewers (Blind Hunter ×2, Edge Case Hunter, Acceptance Auditor ×2) → 10 raw findings → per-finding adversarial verification against the real working tree → **3 confirmed, 7 dismissed as false positives**. No review layer failed._

- [x] [Review][Patch] **FIXED** Folder archive resurrects a file-version a prior remove already hard-deleted [src/Hexalith.Folders/Projections/SemanticIndexing/SemanticIndexingBridgeProjection.cs:ApplyFolderArchived]
  - **HIGH / correctness (AC1+AC4 hybrid-removal invariant).** `ApplyFolderArchived` fans out over *every* folder key with only an out-of-order guard (`Freshness.Watermark > envelope.Sequence`). An entry already `Tombstoned` with `ReasonCode == "folder_file_removed"` (hard-deleted from the index via `SearchIndexEntryRemoved`) keeps its key + preserved `Evidence` (`PublishedEventId`/`IndexedText`/`IndexedAttributes`). A later `FolderArchived` (higher sequence) passes the guard and flips its `ReasonCode` to `folder_archived`; `ProcessTombstoneAsync` then routes it to `SoftDeleteFileVersionAsync`, which re-publishes `SearchIndexEntryChanged{folders.status=archived}` — re-materializing in Memories the doc that was just hard-deleted. Verified real on both the in-memory and EventStore store paths; no test exercises cross-batch remove-then-later-archive on the same entry. Blast radius reduced (10.5 filters `folders.status != live`) but it is a genuine data-retention regression: "a removed file genuinely no longer exists" is violated. **Fix:** in `ApplyFolderArchived`, skip entries already `Tombstoned` with `ReasonCode == "folder_file_removed"` (hard-delete wins over a later archive).
- [x] [Review][Patch] **FIXED** AC9 gated round-trip uses a non-existent search query-param syntax (`attr:` instead of `attribute.`) [tests/Hexalith.Folders.AppHost.Tests/FoldersTopologyCrossProcessTests.cs:SearchAsync]
  - **MEDIUM / test-soundness (AC9c / AC11).** The status filter is built as `&attr:{StatusAttributeKey}=…`, but the Memories `/api/search` endpoint binds attribute filters ONLY from `attribute.`-prefixed keys (`ReadAttributeFilters`). `attr:…` is silently ignored → no `@attributeTags:{…}` filter applied → the `active → 0 hits` assertion would return 1 and **FAIL** on the live DCP lane, while `archived → 1 hit` passes only vacuously. AC9(c) ("filterable by `folders.status`") is therefore not actually exercised. Zero current blast radius (test is opt-in/DCP-gated and currently SKIPPED; product code is correct). **Fix:** change to `&attribute.{StatusAttributeKey}={value}` and confirm the worker upsert writes `folders.status` into the Memories `attributeTags` field so the filter matches.
- [x] [Review][Patch] **FIXED** Never-indexed archive no-op asserted only for the file-remove reason, not `folder_archived` [tests/Hexalith.Folders.Workers.Tests/SemanticIndexingProcessManagerTests.cs]
  - **LOW / test-gap (AC2/AC3/AC10).** The only never-indexed no-op test uses `Mutation(fileOperationKind:"remove", contentHashReference:null)` (reason `folder_file_removed`) and asserts `removal_not_required`. The production gate (`PublishedEventId is null` checked before the reason branch) covers a never-indexed `folder_archived` by construction, so this is correctness-safe — but AC10 explicitly calls for a never-indexed *archive* case and the matrix is incomplete. **Fix:** add a never-indexed `FolderArchived` case asserting the `removal_not_required` no-op (no publish).

_Dismissed as false positives (verified non-issues — for the record): RecordRemovalEvidenceAsync "resurrects a concurrently-deleted entry" (×2 — no delete path exists in `IReadModelStore`); removal-evidence watermark gate "allows out-of-order outcome races" (single synchronous egress per batch; idempotent by design); `IndexedAttributes` "persisted without validation" (curated `folders.*` set by construction; `IndexedText` gets only a whitespace check, not safety validation); re-delivered failed tombstone "silently abandoned" (design records-and-acks; no retry-on-redelivery path exists for any path); `ApplyFolderArchived` "equal-sequence flip without dup guard" (consistent with sibling folder-scoped paths; byte-identical idempotent re-apply); unrecognized-reason tombstone "silently records no evidence" (unreachable — projection only ever sets the two known reasons)._

**Resolution 2026-06-24 — all 3 patches applied & verified (`apply every patch`):**
- HIGH: `ApplyFolderArchived` now skips entries already `Tombstoned` (keys off `Status`, not the egress-mutated `ReasonCode`) → a hard delete is never resurrected by a later archive. +2 regression tests (single-batch remove→archive, and cross-batch remove→removal-evidence→archive).
- MEDIUM: AC9 `SearchAsync` filter corrected from `&attr:…` to `&attribute.folders.status=…` (the only prefix `ReadAttributeFilters` binds; matches the worker's `BuildAttributeTag` key). Still DCP-gated/SKIP locally, but now sound on the live lane.
- LOW: added `ProcessFolderEventsAsyncShouldRecordNoOpForNeverIndexedArchivedTombstoneWithoutCallingMemories` (closes the AC10 never-indexed *archive* matrix cell).
- Verification: build 0E; Workers.Tests 62/62, Folders.Tests 1354/1354, AppHost.Tests 3 SKIP (clean); `dotnet format whitespace --verify-no-changes` EXIT 0. AC9 live proof remains BLOCKED-PENDING the DCP lane (unchanged environment limitation, not a review finding).
