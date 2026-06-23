---
baseline_commit: e0a968b
---

# Story 10.5: Expose an authorized Folders query facade over Memories

Status: in-progress

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

> [!IMPORTANT]
> **Read-mechanism decision (Jerome, 2026-06-23): Option B — relax the Memories dependency ban for ONE Folders project and mirror the proven `Hexalith.Tenants.UI` pattern.** The facade reaches the search index through `Hexalith.Memories.Client.Rest`'s typed `MemoriesClient.SearchAsync(...)`, exactly as `Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs` already does for `tenants-index`. This **reverses** the "Critical Don't-Miss" rule in `project-context.md#82` ("do not re-introduce a `Hexalith.Memories.Client.Rest` reference or `AddMemoriesClient()` in any Folders project") for the read path only. The relaxation MUST be (a) localized to a single project (`Hexalith.Folders.Server` — core/Client/CLI/MCP/UI stay Memories-free), and (b) shipped atomically with the amendments to `project-context.md#82`, `architecture.md#134`, and the `ScaffoldContractTests` dependency allowlist, or the build/boundary tests go red. See "Read-mechanism decision & dev guidance" in Dev Notes. **Other resolved scope decisions:** full closure (this story authors PRD FR58 + the architecture §Query Facade + the UX indexing-status console projection), full surface parity (API + SDK + MCP + CLI), and the facade serves the **syntactic/BM25 search index over `folders-index`** — NOT RAG/`IngestAsync` (out of MVP scope; the seed-AC "RAG" wording is legacy).

## Story

As a developer or AI agent working through the Folders API/SDK/MCP/CLI,
I want to search the content that Folders has indexed into the Memories search index and get back only results I am authorized to see, security-trimmed and redacted by Folders policy,
so that I can discover authorized file versions across my folders without Folders ever leaking another managed tenant's content, raw paths, snippets, or hidden-resource existence.

## Context & Scope Boundary

Epic 10 stands up the Folders→Memories search-index pipeline. Story 10.1 added the worker-only Memories dependency and the `ISemanticIndexingPort`; Story 10.2 added the Folders-owned bridge projection/read model; Story 10.3 publishes `SearchIndexEntryChanged` on file-write/commit; Story 10.4 publishes `SearchIndexEntryRemoved` on removal/archive and proves the live `folders-index` round-trip. Story 10.5 is the **capstone read side**: an authorized Folders query facade that lets callers search `folders-index` and surfaces indexing status, with Folders authorization + security-trimming + redaction applied before any result crosses the API/SDK/MCP/CLI boundary.

Producer (10.3/10.4) and consumer (10.5) are asymmetric: the producer **publishes** via Dapr pub/sub (`pubsub`/`memories-events`) and never needed a service-invoke authorization; the facade **reads synchronously** over `GET /api/search` on Dapr app-id `memories`, which is a different egress class that re-opens the invoke-authorization question the producer closed.

Two non-negotiable correctness controls dominate this story:

1. **Shared-tenant trap.** All Folders content lives under ONE physical Memories tenant `folders-index` (`FoldersSemanticIndexingDefaults.IndexTenant`). Memories' own `tenantId=folders-index` does NOT isolate Folders managed tenants — a raw `api/search?tenantId=folders-index&query=...` returns hits across ALL managed tenants/orgs/folders. The facade MUST security-trim every hit by the caller's authoritative `(managedTenantId, organizationId, folderId)` — both as a server-side `AttributeFilters["folders.managedTenantId"]` constraint AND as a Folders-side post-filter with a per-hit folder-ACL re-check (defense in depth). The Folders-side trim, not Memories', is the load-bearing tenant-isolation control.
2. **Index is security-untrusted and non-authoritative.** Memories decides *which* rows appear, never *what each row shows*. 10.3 deferred per-folder-ACL freshness at index time to the query facade, so indexed docs are NOT guaranteed to reflect current ACLs. 10.5 MUST re-run tenant + folder-ACL + path-policy at query time, recover ids from `ScoredResult.SourceUri`, and hydrate each surviving hit from the authoritative Folders read before returning. `ScoredResult.ContentSnippet` (a 200-char content snippet) MUST be dropped to satisfy the metadata-only contract.

In scope:

1. **Core query triad** (`Hexalith.Folders/Queries/ContextSearch/`, Memories-free): `ContextSearchQuery`, `ContextSearchQueryHandler` (authorize → bounds → query source → security-trim → per-hit ACL re-check → redact → map), `ContextSearchQueryResult`, `ContextSearchResultCode`, metadata-only `ContextSearchItem`, plus a Memories-free source port `IFolderSearchSource` (+ `UnavailableFolderSearchSource` fail-safe default). Mirror `Queries/FileContext/` exactly.
2. **Server-side Memories gateway** (`Hexalith.Folders.Server`, Option B home): `MemoriesFolderSearchSource : IFolderSearchSource` injecting `MemoriesClient` (from `Hexalith.Memories.Client.Rest`), calling `SearchAsync(new SearchRequest(TenantId: "folders-index", Axis: "syntactic", ...))`, applying the `folders.managedTenantId` attribute filter, mapping `SearchResult`/`ScoredResult` → Memories-free `FolderSearchSourceResult` (dropping `ContentSnippet`; recovering file-version identity from `SourceUri`), hydrating from the authoritative Folders read model, and degrading gracefully (`Degraded`/`UnavailableAxes`/unavailable → `ReadModelUnavailable`). Mirror `Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs`.
3. **REST endpoint** in the `context-queries` group (`FoldersDomainServiceEndpoints.cs`): a non-mutating search route that rejects `Idempotency-Key`, requires/honors `X-Hexalith-Freshness` (class `eventually_consistent`), honors `X-Correlation-Id`/`X-Hexalith-Task-Id`, enforces C4 bounds, and emits metadata-only safe-denial Problem Details + `FolderAuditEndpointFilter` audit.
4. **OpenAPI Contract Spine op** (copy the `context/search` block) with `x-hexalith-read-consistency.class: eventually_consistent`, NO `idempotency_key_rule`, `x-hexalith-parity-dimensions.transportParity: [rest, sdk, mcp, cli]`; regenerate the SDK client.
5. **Full surface parity**: MCP tool (`ContextTools.cs`) + CLI subcommand (`Commands/Context/ContextCommand.cs`), task-scoped, no idempotency key, metadata-only output.
6. **New read action token** (e.g. `read_context_search`) added to `EffectivePermissionsActionCatalog` so the `FolderAcl` authorization layer accepts it.
7. **Dapr egress policy** for the live read path: add a `folders → memories` (`GET /api/search`) invoke allow-rule to `deploy/dapr/production/accesscontrol.yaml` (currently `memories` is `defaultAction: deny`, `policies: []`), add `memories` as an appId in the dev AppHost `accesscontrol.yaml` (currently absent), and add negative-control rows to `DaprPolicyConformanceTests`.
8. **Rule amendments (Option B)**: amend `project-context.md#82` and `architecture.md#134` to permit the single `Hexalith.Folders.Server` Memories.Client.Rest reference for the read facade, and add `Hexalith.Folders.Server` to the `ScaffoldContractTests` Memories-reference allowlist — shipped in the SAME change set as the reference.
9. **Indexing-status console projection** (UX, full-closure): a read-only, metadata-only projection page surfacing `indexed/stale/skipped/failed/tombstoned/reconciliation_required` per file version, composing only allow-listed console components; requires a new folder/tenant-scoped read method on `ISemanticIndexingBridgeReadModel` + store impls (the public interface is single-entry today).
10. **Planning deliverables (full-closure)**: author PRD **FR58** (authorized search facade) after FR57, and an architecture **§Query Facade** section recording the chosen read mechanism, the shared-index trim, and the non-authoritative hydration rule.

Out of scope:

1. RAG / `Hexalith.Memories.Client.Rest.IngestAsync` / memory-unit embeddings (experimental `HXL001`) — the facade serves the syntactic/BM25 search index only.
2. Adding Memories references to any Folders project other than `Hexalith.Folders.Server` (core, Contracts, Client, CLI, MCP, UI, Testing stay Memories-free; they reach the facade only through the generated SDK over REST).
3. Mutation, repair, file browsing, content/snippet/diff display, or credential reveal in the console (MVP read-only boundary).
4. Re-architecting the producer (10.3/10.4) or the bridge write path; this story only adds a folder/tenant-scoped READ to the bridge read model.
5. Live `folders-index` round-trip sign-off in a non-DCP-capable local env (inherits the Epic 9 `aspire run` DCP/`--tls-cert-file` blocker); structural + integration coverage is the bar locally, full live verification runs in a DCP-capable CI lane and depends on Story 10.4 landing a populated, correctly-pruned index.

## Acceptance Criteria

1. **Authorization before any result observation.** The facade runs authorization in canonical order — JWT validation → EventStore claim transform → tenant-access projection freshness (fail-closed-on-stale) → folder ACL → path policy — BEFORE issuing the Memories query; any deny short-circuits before egress. Client-controlled tenant/principal/path values are comparison inputs only; authority comes from authenticated context + EventStore claim-transform evidence. The handler reuses `LayeredFolderAuthorizationService.AuthorizeAsync`. [CF-9, CF-10]

2. **Security-trimming on the shared `folders-index`.** The Memories query is constrained server-side with `AttributeFilters["folders.managedTenantId"] = <authoritativeTenantId>` (and org/folder when the query is folder-scoped), AND every returned hit is Folders-side post-filtered and re-checked against current folder ACL. A caller can NEVER retrieve, infer, or enumerate any hit outside their authorized `(managedTenantId, organizationId, folderId)`, even by supplying a cross-tenant `folderId`/`SourceUri`. The Folders-side trim is the authoritative control; the attribute filter is defense-in-depth, not trusted alone. [CF-6, CF-9]

3. **Metadata-only / redaction.** Mapping `ScoredResult` → facade result DROPS `ContentSnippet`; `SourceUri`/`MemoryUnitId`/`AggregateId` (which encode `managedTenant/org/folder/fileVersion` path structure) are redacted or rewritten to non-disclosing handles. No raw paths, file contents, snippets, matched-line text, previews, diffs, provider payloads, or raw search text leave the boundary or enter audit. Sentinel-secret tests over `tests/fixtures/audit-leakage-corpus.json` find no match in results, audit, logs, traces, or test output. [CF-7, CF-8, CF-17]

4. **Tenant isolation (zero-tolerance).** Given tenant B's indexed entries, a tenant-A caller using any identifier (including a cross-tenant `folderId`/`SourceUri`/attribute value) retrieves NO tenant-B object; cross-tenant identifiers return non-disclosing failures indistinguishable from "absent." [Source: prd.md#689-690]

5. **Safe denial, externally indistinguishable.** Unauthorized, cross-tenant, and nonexistent targets all return the same safe-denial shape (empty `Items`, `NotFoundSafe`/`AuthorizationDenied`, `Stale:true`, `visibility=metadata_only`) that does not disclose hidden resource existence and is byte-identical (modulo correlation id) across "denied" vs "absent." Result-code enum follows the `<Query>ResultCode` convention (first member `Allowed`; includes `NotFoundSafe`, `Redacted`, `ReadModelUnavailable`). [CF-13; Source: prd.md#693, #387]

6. **Non-mutating transport: rejects Idempotency-Key; declares read-consistency + correlation.** A request carrying `Idempotency-Key` → 400 `idempotency_key_not_allowed`. The op declares `read_consistency_class: eventually_consistent` (the index is async pub/sub-fed), requires/honors `X-Hexalith-Freshness`, and honors `X-Correlation-Id`/`X-Hexalith-Task-Id` (causation: query-correlation-only). No `idempotency_key_rule` is declared anywhere for this op. [CF-11, CF-12]

7. **C4 bounds enforced.** Max **500** results with `isTruncated=true` on overflow; **2 s** `query_timeout` (`x-hexalith-query-timeout-ms: 2000`); **1 MB** (1048576) aggregate response budget → `ResponseLimitExceeded`; query-text length bounded → `InputLimitExceeded`. Raw search text is excluded from audit (only query family, result count, truncation flag, limit tier are audited). [CF-16]

8. **Graceful degradation.** When Memories is unavailable/degraded, the facade returns `ReadModelUnavailable` (503) or a degraded result honoring `SearchResult.Degraded`/`UnavailableAxes`/`AllEnabledAxesUnavailable` — never a 500 or poison-loop — with metadata-only evidence. The default DI registration is the fail-safe `UnavailableFolderSearchSource`. [Source: SearchResult.cs#52,#61; 10-3 D4]

9. **Full surface parity (API + SDK + MCP + CLI).** The new REST op has equivalent operation identity, errors, authorization, audit, and read-consistency behavior across REST, the generated SDK, the MCP tool, and the CLI command. Each is task-scoped, accepts NO idempotency key, and emits metadata-only output (MCP/CLI metadata-only serializers drop any content-bearing field). Satisfies the 100% parity gate (FR47–FR51). [Source: prd.md#669-673, #476]

10. **OpenAPI + parity-oracle sync.** The OpenAPI op declares `x-hexalith-read-consistency.class: eventually_consistent`, NO `idempotency_key_rule` (C13 gate (d) "query without read_consistency_class → fail" passes; gate (c) not triggered), `x-hexalith-parity-dimensions.transportParity: [rest, sdk, mcp, cli]`, the canonical error categories, and the metadata-only result schema; the generated SDK client compiles and the parity oracle / contract-spine gates pass. [CF-12; Source: yaml#2730-2854]

11. **Integration test does NOT mock the gateway.** An in-process integration test exercises the real REST → authorization → `IFolderSearchSource` egress seam end-to-end (NOT a hand-called handler, NOT a stubbed gateway at the boundary), avoiding the 10.3 "green tests, broken wiring" trap; cross-tenant isolation and safe-denial indistinguishability are asserted through the real path. The Dapr `folders → memories` invoke allow-rule + negative-control conformance rows are present and asserted. [Source: 10-3 D1; CF-4, CF-5]

12. **Dependency boundary stays closed except the one approved relaxation.** `Hexalith.Folders.Server` is the ONLY non-Worker, non-AppHost Folders project referencing Memories (`Client.Rest` + `Contracts`); core, Contracts, Client, CLI, MCP, UI, Testing reference ZERO Memories assemblies. `project-context.md#82`, `architecture.md#134`, and the `ScaffoldContractTests` allowlist are amended in the same change set; `ScaffoldContractTests` fails if Memories leaks beyond Workers + Server (+ AppHost). DI is `ValidateOnBuild`/`ValidateScopes`-clean. [CF-1, CF-2]

13. **Indexing-status console projection (full-closure).** A read-only, metadata-only console projection surfaces `indexed/stale/skipped/failed/tombstoned/reconciliation_required` per file version (folder/tenant-scoped), with redacted-vs-unknown visually + semantically distinct, composing only allow-listed components and exposing no content/snippets/diffs. Backed by a new folder/tenant-scoped read method on `ISemanticIndexingBridgeReadModel` + store impls. [CF-14, CF-20; Source: ux-design-specification.md#115,#118-119]

14. **Planning deliverables authored (full-closure).** A new PRD **FR58** (authorized search facade), an architecture **§Query Facade** section (recording the Option B read mechanism + shared-index trim + non-authoritative hydration + the `memories` invoke egress policy), and the UX indexing-status projection spec are authored and internally consistent with this story. [CF-18, CF-19, CF-20]

15. **Verification passes.** `dotnet build Hexalith.Folders.slnx` succeeds. Narrowed tests pass: `Hexalith.Folders.Tests`, `Hexalith.Folders.Server.Tests` (or the Server/Integration lane that owns the no-mock-gateway test), `Hexalith.Folders.Contracts.Tests` (OpenAPI/parity/Dapr-policy conformance), and `Hexalith.Folders.Testing.Tests`. If exact commands are blocked by local env state (DCP/Aspire), record the blocker and do not mark implementation complete.

## Tasks / Subtasks

- [ ] Task 1 — Author the architecture §Query Facade + ratify file-level design (AC 14, 12)
  - [ ] Extend `architecture.md` (around #156) with a Query Facade section: Option B read mechanism (`MemoriesClient.SearchAsync` from `Hexalith.Folders.Server`), the shared-`folders-index` security-trim, the non-authoritative hydration rule, and the new `folders → memories` invoke egress policy.
  - [ ] Confirm the facade host = `Hexalith.Folders.Server` (core stays Memories-free behind `IFolderSearchSource`); record the dedicated-gateway-project alternative only if chosen instead.

- [ ] Task 2 — Core query triad, Memories-free (AC 1, 3, 5, 6, 7)
  - [ ] Add `src/Hexalith.Folders/Queries/ContextSearch/`: `ContextSearchQuery`, `ContextSearchQueryHandler`, `ContextSearchQueryResult`, `ContextSearchResultCode`, metadata-only `ContextSearchItem`.
  - [ ] Mirror `WorkspaceFileContextQueryHandler` order: auth-required → existence-as-safe-denial → `AuthorizeAsync` → path policy + sensitivity → C4 bounds → source query → map; reuse `SafeResult`/`MapAuthorizationDenial` shapes.
  - [ ] Define the Memories-free source port `IFolderSearchSource` + request/result/hit records (Folders DTOs only) + `UnavailableFolderSearchSource` fail-safe default.

- [ ] Task 3 — Server-side Memories gateway (Option B) (AC 2, 3, 8, 12)
  - [ ] Add `Hexalith.Memories.Client.Rest` + `Hexalith.Memories.Contracts` `ProjectReference`s to `Hexalith.Folders.Server.csproj`; call `AddMemoriesClient()` (mirror how `Hexalith.Tenants` registers it, incl. base address / Dapr-invoke wiring).
  - [ ] Add `MemoriesFolderSearchSource : IFolderSearchSource` injecting `MemoriesClient`; call `SearchAsync(new SearchRequest(TenantId: "folders-index", Axis: "syntactic", AttributeFilters: { "folders.managedTenantId": authoritativeTenantId, ... }, ...))`.
  - [ ] Map `SearchResult`/`ScoredResult` → `FolderSearchSourceResult`: DROP `ContentSnippet`; recover file-version identity from `ScoredResult.SourceUri`; redact `SourceUri`/`MemoryUnitId`; hydrate metadata from the authoritative Folders read (mirror `TenantQueryGateway` hydrate path); translate `Degraded`/unavailable → `Unavailable`.

- [ ] Task 4 — Authorization wiring + security-trim (AC 1, 2, 4, 5)
  - [ ] Add read action token (e.g. `read_context_search`) to `EffectivePermissionsActionCatalog`.
  - [ ] Decide auth preset: reuse `LayeredFolderOperationPolicy.StrictRead()` (target `folders`, assert the `memories` egress in the gateway/conformance) vs add a `ContextSearchRead()` preset (`DaprTargetAppId="memories"`, asserts egress in the `DaprDenyByDefaultPolicy` layer). Record the choice.
  - [ ] Enforce the per-hit folder-ACL re-check after the Memories query (the index is security-untrusted).

- [ ] Task 5 — Redaction / metadata-only layer (AC 3, 4)
  - [ ] Reuse `WorkspaceFileSensitivityClassifier`, `RedactionMetadata`/`RedactionVisibility`, and the `OpsConsoleDiagnosticsEndpoints` sensitive-value scrubber.
  - [ ] Add sentinel-corpus tests over `tests/fixtures/audit-leakage-corpus.json`.

- [ ] Task 6 — REST endpoint (AC 5, 6, 7, 8, 10)
  - [ ] Add a `context/index-search` (or `context/search-index`) POST route to the `context-queries` group in `FoldersDomainServiceEndpoints.cs`, ending with `.AddEndpointFilter<FolderAuditEndpointFilter>()`.
  - [ ] Use an envelope validator that rejects `Idempotency-Key` and validates `X-Hexalith-Freshness == eventually_consistent` (both `snapshot_per_task` and `eventually_consistent` validators already exist in the file; do NOT reuse the `snapshot_per_task` `ValidateContextQueryEnvelope` verbatim — the index is async).
  - [ ] Map result codes to canonical Problem Details (metadata-only, `details.visibility=metadata_only`); register the handler + `IFolderSearchSource` (default `Unavailable*`, live `MemoriesFolderSearchSource`) in `FoldersServerServiceCollectionExtensions.cs`.

- [ ] Task 7 — OpenAPI op + SDK regen (AC 9, 10)
  - [ ] Copy the `context/search` op block in `hexalith.folders.v1.yaml`; set `read-consistency.class: eventually_consistent`, NO `idempotency_key_rule`, `parity-dimensions.transportParity: [rest, sdk, mcp, cli]`, metadata-only result schema, canonical error categories, audit-metadata keys (search text excluded).
  - [ ] Regenerate `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs`; do not hand-edit generated files.

- [ ] Task 8 — MCP tool + CLI command (AC 9)
  - [ ] Add a sibling read tool in `Mcp/Tools/ContextTools.cs` via `pipeline.ExecuteQueryAsync(taskId, taskIdRequired:true, ...)` calling the new generated client op; metadata-only serializer.
  - [ ] Add a sibling CLI subcommand in `Cli/Commands/Context/ContextCommand.cs` via `CommandFactory.Query(... taskIdRequired:true)`; metadata-only renderer.

- [ ] Task 9 — Dapr egress policy + rule amendments (AC 11, 12)
  - [ ] Add a `folders → memories` (`GET /api/search`) invoke allow-rule to `deploy/dapr/production/accesscontrol.yaml` (the `memories` target is `defaultAction: deny`, `policies: []`); add `memories` as an appId to `src/Hexalith.Folders.AppHost/DaprComponents/accesscontrol.yaml`; add negative-control rows to `DaprPolicyConformanceTests` + the `dapr-policy-conformance.yaml` fixture.
  - [ ] Amend `project-context.md#82` + `architecture.md#134` to permit the single `Hexalith.Folders.Server` Memories.Client.Rest reference for the read facade; add `Hexalith.Folders.Server` to the `ScaffoldContractTests` Memories-reference allowlist — same change set.

- [ ] Task 10 — Folder/tenant-scoped bridge read + UX projection (AC 13)
  - [ ] Add a folder/tenant-scoped read method to `ISemanticIndexingBridgeReadModel` + `InMemorySemanticIndexingBridgeStore`/`EventStoreSemanticIndexingBridgeStore` (the folder index is private today; surface it safely, tenant-prefixed).
  - [ ] Add a read-only, metadata-only indexing-status projection page under `src/Hexalith.Folders.UI/Components/Pages/` (mirror `ProviderSupport.razor.cs`), composing only allow-listed components; redacted-vs-unknown distinct.

- [ ] Task 11 — PRD FR58 (AC 14)
  - [ ] Add FR58 (authorized search facade) to `prd.md` after FR57; reconcile the 57→58 FR count where the architecture states "57 functional requirements."

- [ ] Task 12 — Tests (AC 4, 5, 11, 12)
  - [ ] Unit: handler authorization order, result-code mapping, `ContentSnippet` drop + `SourceUri`/`MemoryUnitId` redaction, C4 bounds, freshness/idempotency-key rejection, `AttributeFilters` construction. Hermetic.
  - [ ] Integration (no-mock-gateway): real REST → auth → source seam; cross-tenant isolation (seed tenant-A + tenant-B docs sharing a term; tenant-A gets only tenant-A); safe-denial byte-identical across unauthorized / cross-tenant / nonexistent.
  - [ ] Boundary: `ScaffoldContractTests` proves Memories stays within Workers + Server (+ AppHost); DI validates.

- [ ] Task 13 — Build and test (AC 15)
  - [ ] `dotnet build Hexalith.Folders.slnx`; then `Hexalith.Folders.Tests`, the Server/Integration lane owning the no-mock-gateway test, `Hexalith.Folders.Contracts.Tests`, `Hexalith.Folders.Testing.Tests`; record any DCP/Aspire env blocker.

## Dev Notes

### Read-mechanism decision & dev guidance (Option B, 2026-06-23)

**The decision.** The facade queries the Memories search index using the typed `MemoriesClient.SearchAsync(...)` from `Hexalith.Memories.Client.Rest`, mirroring the proven sibling precedent. This was historically forbidden by `project-context.md#82`; Jerome ratified relaxing it for the read path on 2026-06-23. The relaxation is intentionally minimal: **only `Hexalith.Folders.Server`** gains the reference; core stays Memories-free behind the `IFolderSearchSource` port so the FileContext source-port convention is preserved.

**The precedent to copy (verbatim shape).** `Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs#28,#461-543`: injects `MemoriesClient` (#28), calls `memoriesClient.SearchAsync(new SearchRequest(TenantId: "tenants-index", Axis: "syntactic", ...))` (#468-478), recovers ids from `ScoredResult.SourceUri` (#526-543), applies status both as an `AttributeFilter` and an authoritative re-check (#571-574, #588-591), hydrates via the ETag-fresh detail path (#552-586), and degrades gracefully when Memories is unavailable (#480-490, #629-638). For Folders, swap `tenants-index` → `folders-index`, the status filter → `folders.managedTenantId` (+ org/folder), and the detail hydrate → the Folders authoritative read.

**Memories search surface (what you call).**
- Route: `GET /api/search` on Dapr app-id `memories` — the only search route. [Source: Hexalith.Memories/src/Hexalith.Memories.Server/Program.cs#2150]
- Typed client (Option B, allowed now): `MemoriesClient.SearchAsync(SearchRequest, ct) → Task<SearchResult>` [Source: Hexalith.Memories/src/Hexalith.Memories.Client.Rest/MemoriesClient.cs#164]; `SearchRequest` lives in `Client.Rest/SearchRequest.cs`.
- Wire records (in allowed `Contracts.V1`): `SearchQuery` (`TenantId`, `Query`, `CaseId`, `SourceTypeFilter`, `MetadataQuery`, `CloudEventSubject`, `AttributeFilters`, `MaxResults=10`, `Offset=0`); `SearchResult` (`Results`, `TotalCount`, `HasIndexedMemoryUnits`, `Degraded`, `UnavailableAxes`, `AllEnabledAxesUnavailable`); `ScoredResult` (`MemoryUnitId`, `Score`, **`ContentSnippet`** ← DROP, `SourceUri`, `SourceType`, `Axis`, `CaseId`, `CaseName`, `AnnotationsCount`). [Source: Hexalith.Memories/src/Hexalith.Memories.Contracts/V1/SearchQuery.cs, SearchResult.cs, ScoredResult.cs]
- `ScoredResult`/`FusedScoredResult` carry NO `Attributes` in the response (attributes are write/filter-only). Redaction is therefore about dropping `ContentSnippet` and redacting `SourceUri`/`MemoryUnitId`, plus enforcing the `folders.managedTenantId` filter + post-trim. [Source: raw_memories §4]

**The two correctness controls (do not skip either).**
- (1) Shared-index trim: send `AttributeFilters["folders.managedTenantId"] = <authoritativeTenantId>` AND post-filter every hit + re-check folder ACL Folders-side. The attribute filter is a query string Memories trusts; the Folders-side re-check is the authoritative control. [Source: src/Hexalith.Folders.Workers/SemanticIndexing/MemoriesSemanticIndexingPort.cs#102-126; FoldersSemanticIndexingDefaults.cs#9]
- (2) Non-authoritative hydration: recover ids from `SourceUri` (= the CloudEvent id the producer set), then hydrate metadata from the authoritative Folders read; never render the index payload as truth. 10.3 explicitly deferred index-time ACL freshness to here. [Source: memories-search-index-handoff-2026-06-23.md#14-16,#79,#97-99; 10-3 story #370]

### Source facts

- Story 10.5 seed AC: given indexed content in Memories, a Folders query facade serves results that are authorized, security-trimmed, and redacted by Folders policy before leaving the API/SDK/MCP boundary, with a new PRD FR + architecture query-facade section + UX "indexing status" projection added when scheduled. [Source: `_bmad-output/planning-artifacts/epics.md` "Story 10.5" #1908-1912]
- The facade serves the **syntactic/BM25 search index** over `folders-index`; RAG/`IngestAsync` is OUT of MVP scope (the seed-AC "RAG" wording is legacy). [Source: architecture.md#156; epics.md#1882]
- `GET /api/search` on `memories` has NO `RequireAuthorization` and NO `AllowAnonymous` — Memories will NOT reject an unauthorized caller, so 100% of authorization is enforced Folders-side before egress. [Source: Hexalith.Memories/src/Hexalith.Memories.Server/Program.cs#2150]
- Dapr egress is CLOSED today: production `memories` target is `defaultAction: deny`, `policies: []`; dev AppHost `accesscontrol.yaml` does not list `memories` as an appId. A `folders → memories` invoke allow-rule + dev appId + conformance rows are NEW work in this story. [Source: deploy/dapr/production/accesscontrol.yaml#119-137; src/Hexalith.Folders.AppHost/DaprComponents/accesscontrol.yaml#9-25]
- C4 (`approved`) and C9 (`approved`) gate Epic 10's precondition, which is satisfied (Epic 9 `done`); 10.5 is NOT gate-blocked. The broader MVP release C3 Legal sign-off (Story 8-6) does not block 10.5 authoring/dev. [Source: docs/exit-criteria/c0-c13-governance-evidence.yaml#62-69,#107-114; architecture.md#196]
- C4 bounds binding on the facade: max 500 results (`isTruncated=true`), 2 s `query_timeout` (`x-hexalith-query-timeout-ms: 2000`), 1 MB aggregate response, search text excluded from audit. [Source: docs/exit-criteria/c4-input-limits.md#17-22]
- C9/S-6: paths + repo + branch + commit messages are `tenant-sensitive` by default; keep raw path metadata out of results unless C9 explicitly allows; results must be path-exposure-safe before crossing the boundary. [Source: architecture.md#484, #137, #136]

### Current code state to preserve

- `Queries/FileContext/` is the canonical metadata-only, security-trimmed, policy-redacted query triad — copy its handler order, `SafeResult`/`MapAuthorizationDenial` shapes, result-code conventions, and source-port (`IWorkspaceFileContextSource` + `UnavailableWorkspaceFileContextSource` fail-safe default). [Source: src/Hexalith.Folders/Queries/FileContext/]
- `LayeredFolderAuthorizationService.AuthorizeAsync` (the single authorize entrypoint) and `LayeredFolderOperationPolicy` presets (`StrictRead()`, `BoundedDiagnosticRead()`) — reuse, do not fork. The `FolderAcl` layer rejects action tokens absent from `EffectivePermissionsActionCatalog`. [Source: src/Hexalith.Folders/Authorization/LayeredFolderAuthorizationService.cs#15-218; LayeredFolderOperationPolicy.cs#31-53]
- `FoldersDomainServiceEndpoints.cs` `context-queries` group + the transport guardrails: `ValidateContextQueryEnvelope` (Idempotency-Key reject) and the OpsConsole `eventually_consistent` preflight. Both `snapshot_per_task` and `eventually_consistent` freshness validators already exist in this file; the facade uses `eventually_consistent`. [Source: FoldersDomainServiceEndpoints.cs#4054-4127; OpsConsoleDiagnosticsEndpoints.cs#340-451]
- `OpenApi`: existing `context/search` op `hexalith.folders.v1.yaml#2730-2853` is the copy template; the description #2736-2741 is the metadata-only contract `ScoredResult.ContentSnippet` would violate.
- MCP `ContextTools.cs` (`pipeline.ExecuteQueryAsync(... taskIdRequired:true)`, metadata-only serializer) + CLI `ContextCommand.cs` (`CommandFactory.Query(... taskIdRequired:true)`, metadata-only renderer) — the parity templates.
- Bridge read model: `ISemanticIndexingBridgeReadModel.GetFileVersionAsync(...)` is single-entry only on the public interface; `SemanticIndexingBridgeStatus` enum = `Unknown, Indexed, Stale, Skipped, Failed, Tombstoned, ReconciliationRequired` with `.ToStatusCode()` → the seed-AC status vocabulary. A folder/tenant-scoped read is NEW (the folder index is private to `EventStoreSemanticIndexingBridgeStore`). [Source: src/Hexalith.Folders/Projections/SemanticIndexing/ISemanticIndexingBridgeReadModel.cs, SemanticIndexingBridgeStatus.cs#7-29]
- Producer curation precedent (what is already safe in the index): `MemoriesSemanticIndexingPort.BuildText/BuildAttributes#87-115` — descriptor + non-sensitive identity tokens + classifications only; the facade trims/filters on these `folders.*` attributes and never echoes `ContentSnippet`.

### Architecture and dependency guardrails

- Repository configuration is authoritative when prose drifts: `global.json` (.NET SDK `10.0.300`, `net10.0`), `Directory.Packages.props` (central versions; Dapr, Aspire `13.4.6`, xUnit v3 `3.2.2`, Shouldly `4.3.0`, NSubstitute `5.3.0`). Never add inline `Version` attributes.
- Dependency direction (post-amendment): Contracts → no behavior; core → Contracts only (Memories-free); **Server → core + Contracts + ServiceDefaults + EventStore + Tenants + (NEW, Option B) Memories.Client.Rest + Memories.Contracts**; Client → Contracts; CLI/MCP/UI → Client (Memories-free); Workers → Memories.Contracts. Keep core/Client/CLI/MCP/UI/Testing Memories-free.
- C# file-scoped namespaces, nullable-safe public boundaries, `ArgumentNullException.ThrowIfNull`, ordinal comparisons + invariant formatting for identifiers, cancellation propagation, `ConfigureAwait(false)` where nearby code uses it.
- Non-mutating reads must NOT accept `Idempotency-Key`; they require correlation behavior, authorization parity, safe-denial shape, audit metadata, and a read-consistency class. [Source: project-context.md#144]
- Never search/glob/list/read paths before tenant access, folder ACL, and path policy pass; deny before touching any resource. Redacted ≠ unknown ≠ missing — keep them visibly distinct. [Source: project-context.md#145,#153]
- Tests hermetic: no running Dapr sidecars, Memories server, Redis, Keycloak, provider creds, secrets, network, tenant seed data, or nested submodule init.

### Previous story intelligence

- 10.1 hardened source identity (fixed a raw drive-letter leak); keep source-identity hardening — the facade recovers ids from `SourceUri` and must not re-expose raw path structure.
- 10.2 established the bridge projection/read model + stale-result protection + remove→tombstone semantics; the indexing-status console reads from it. 10.2 verification: solution build clean; `Hexalith.Folders.Tests` 1327/1327, `Workers.Tests` 36/36, `Testing.Tests` 60/60.
- 10.3 D1 trap (load-bearing): its suite passed green while the live publish→subscribe binding was non-functional because tests asserted declared metadata / called the processor directly. AC11's no-mock-gateway integration test is the explicit guard against repeating this on the read side.
- 10.3 mechanism correction: producer PUBLISHES (no service-invoke); the read facade is a different egress and reintroduces the `memories` invoke-policy work (Task 9). The producer's "service-invoke NOT required" reasoning (sprint-status.yaml#245) is publish-only and does NOT cover the read path.
- Sequencing: 10.4 (`backlog`) owns the live `folders-index` round-trip + `SearchIndexEntryRemoved` pruning. 10.5 can be authored/dev'd now against stubs + the `IFolderSearchSource` seam; full live verification depends on 10.4 landing a populated, correctly-pruned index and inherits the Epic 9 DCP/`--tls-cert-file` boot blocker.

### Testing guidance

- Unit boundary (hermetic, xUnit v3 + Shouldly + NSubstitute, `TestContext.Current.CancellationToken`): handler authorization order, result-code mapping, redaction (`ContentSnippet` drop, `SourceUri`/`MemoryUnitId` redaction), C4 bounds, freshness + idempotency-key rejection, `SearchRequest.AttributeFilters` construction, graceful-degradation mapping.
- **No-mock-gateway integration test (AC11):** drive the real REST → authorization → `IFolderSearchSource` seam end-to-end; do NOT call the handler directly and do NOT stub the egress at the gateway boundary. Use a controllable `IFolderSearchSource` fake that still flows through the real endpoint + auth, OR a fake `MemoriesClient`/`HttpMessageHandler` behind the real `MemoriesFolderSearchSource` — never bypass the endpoint/auth wiring.
- Cross-tenant isolation: seed `folders-index` (or the source fake) with tenant-A AND tenant-B docs sharing a query term; assert a tenant-A caller gets ONLY tenant-A hits even when the raw `tenantId=folders-index` query would return both — proving the Folders-side trim, not Memories', is the control.
- Safe-denial indistinguishability: assert byte-identical (modulo correlation id) safe-denial Problem Details across (a) unauthorized tenant, (b) cross-tenant `folderId`/`SourceUri`, (c) nonexistent folder; `visibility=metadata_only`; no enumeration signal.
- Sentinel redaction: iterate `tests/fixtures/audit-leakage-corpus.json` over results/audit/logs/test output; CI fails on any match.
- Boundary: `ScaffoldContractTests` proves Memories references stay within Workers + Server (+ AppHost) after the allowlist amendment; Dapr policy conformance asserts the new `folders → memories` invoke allow-rule + negative controls.

### Latest technical notes

- Dapr service invocation provides discovery + mTLS + access control applied at the called app's sidecar; absent policy defaults allow, explicit policies can deny-by-default and allow only specific app/operation/verb. The `memories` target is deny-by-default with no caller policy, so the facade's `GET /api/search` invoke is blocked until Task 9 adds the allow-rule. [Source: https://docs.dapr.io/operations/configuration/invoke-allowlist/]
- Dapr pub/sub is at-least-once; the search index is async/eventually-consistent, which is why the facade declares `eventually_consistent` (not `snapshot_per_task`). [Source: https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-overview/]
- Confirm how `Hexalith.Tenants` configures `AddMemoriesClient` (Dapr-invoke base address vs direct service URL) and copy it; this determines whether the production invoke allow-rule (Task 9) is the operative control for the live path.

### References

- `_bmad-output/planning-artifacts/epics.md` — Epic 10 (#1876-1912), Story 10.5 (#1908-1912), Phase-2 gating (#1880), new-FR note (#521).
- `_bmad-output/planning-artifacts/architecture.md` — Memories integration (#130-157), query-facade forward-refs (#136, #156), authorization order (#798-808), C4 (#183, #208), C9/S-6 (#484, #213), status reconciliation (#196), console constraints (#167-169), projection list (#146).
- `_bmad-output/planning-artifacts/memories-search-index-handoff-2026-06-23.md` — non-authoritative index (#14-16), read path (#97-99), deny-by-default unchanged (#94-96), live-boot blocker (#120-122).
- `_bmad-output/planning-artifacts/prd.md` — FR34/FR47-FR51/FR55 (#647, #669-673, #680), tenant isolation + no generated payloads (#688-691), 2 s p95 (#716), unbounded-scan protection (#719-720); add FR58 after #682.
- `docs/exit-criteria/c0-c13-governance-evidence.yaml` (C4 #62-69, C9 #107-114, C3 #49-61); `docs/exit-criteria/c4-input-limits.md#17-22`.
- `_bmad-output/implementation-artifacts/10-1..10-3*.md` — port/dependency boundary, bridge model/review learnings, mechanism correction + D1/D4 dev decisions.
- `src/Hexalith.Folders/Queries/FileContext/` — query triad + source-port template; `src/Hexalith.Folders/Authorization/LayeredFolderAuthorizationService.cs`, `LayeredFolderOperationPolicy.cs`, `EffectivePermissionsActionCatalog.cs`.
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs` (#2730-2853 op, #4054-4127 validator), `OpsConsoleDiagnosticsEndpoints.cs` (#340-451), `FoldersServerServiceCollectionExtensions.cs#62-64`.
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#2730-2853`; `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs`.
- `src/Hexalith.Folders.Mcp/Tools/ContextTools.cs`; `src/Hexalith.Folders.Cli/Commands/Context/ContextCommand.cs`.
- `src/Hexalith.Folders/Projections/SemanticIndexing/` (bridge read model + status); `src/Hexalith.Folders.Workers/SemanticIndexing/MemoriesSemanticIndexingPort.cs#87-126`, `FoldersSemanticIndexingDefaults.cs#9`.
- `deploy/dapr/production/accesscontrol.yaml#119-137`; `src/Hexalith.Folders.AppHost/DaprComponents/accesscontrol.yaml`; `DaprPolicyConformanceTests` + `tests/fixtures/dapr-policy-conformance.yaml`.
- PRECEDENT (Option B): `Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs#28,#461-543` + `Hexalith.Tenants.UI.csproj` (references `Memories.Client.Rest`).
- Memories read surface: `Hexalith.Memories/src/Hexalith.Memories.Server/Program.cs#2150`; `Hexalith.Memories.Client.Rest/MemoriesClient.cs#164` + `SearchRequest.cs`; `Hexalith.Memories.Contracts/V1/{SearchQuery,SearchResult,ScoredResult,MemoriesJsonContext}.cs`.
- `_bmad-output/planning-artifacts/ux-design-specification.md` — UX-DR7 (#115), UX-DR10/11/22 (#118-119,#130), search workflow (#110,#308-310), allow-listed components (#420).

## Dev Agent Record

### Agent Model Used

claude-opus-4-8[1m] (bmad-dev-story, ultracode)

### Debug Log References

- 2026-06-24 — Dev started; completed a full 6-agent precedent reconnaissance (Memories read surface + Tenants `TenantQueryGateway`; Server context endpoints + transport guardrails; layered authorization + redaction; OpenAPI op + SDK regen + MCP + CLI; Dapr policy + conformance + scaffold boundary; bridge read model + UX page). **PAUSED before writing code**: a concurrent 10.4 dev-story was actively editing the shared `SemanticIndexing` Workers/Projections files on the same working tree (silent same-tree clobber risk; 10.5 extends those exact files). Per Jerome's decision, waiting for 10.4 to land before implementing. Committed design + file-by-file plan recorded in auto-memory `story-10-5-design-and-pause.md`. Resume once 10.4 is committed/stable.

### Completion Notes List

### File List

## Change Log

| Date | Change | Author |
|------|--------|--------|
| 2026-06-23 | Story created via bmad-create-story (ultracode: 6-reader research workflow + synthesis). Read mechanism = Option B (Memories.Client.Rest in Server, mirror Tenants); scope = full closure (PRD FR58 + architecture §Query Facade + UX projection); surfaces = API/SDK/MCP/CLI; facade serves syntactic/BM25 over `folders-index` (not RAG). Status → ready-for-dev. | Jerome (via create-story) |

## Open Questions / Decisions

**Resolved (Jerome, 2026-06-23):**
- **Read mechanism = Option B** — relax the Memories ban for `Hexalith.Folders.Server` only and call `MemoriesClient.SearchAsync` (mirror `Hexalith.Tenants.UI/TenantQueryGateway`). Requires amending `project-context.md#82`, `architecture.md#134`, and the `ScaffoldContractTests` allowlist atomically (AC12, Task 9).
- **Scope = full closure** — this story authors PRD FR58 + the architecture §Query Facade + the UX indexing-status console projection (AC13, AC14).
- **Surfaces = full parity** — API + SDK + MCP + CLI (AC9).
- **Search type = syntactic/BM25 over `folders-index`** — not RAG/`IngestAsync` (the seed-AC "RAG" wording is legacy; architecture.md#156 puts RAG out of MVP).
- **Gates** — C4 + C9 are `approved`; 10.5 is NOT gate-blocked.

**Remaining (dev-level, decide during implementation and record in the Dev Agent Record):**
1. **Facade host** — confirmed `Hexalith.Folders.Server` (core stays Memories-free behind `IFolderSearchSource`). A dedicated `Hexalith.Folders.QueryFacade`/gateway assembly is an acceptable alternative if Memories isolation from Server transport code is preferred; if chosen, update Task 3/9 paths and the allowlist accordingly.
2. **Auth preset** — reuse `LayeredFolderOperationPolicy.StrictRead()` (target `folders`; assert the `memories` egress in the gateway + Dapr conformance) vs add a `ContextSearchRead()` preset (`DaprTargetAppId="memories"`; assert egress in the `DaprDenyByDefaultPolicy` layer). [CF-10]
3. **`AddMemoriesClient` transport** — confirm whether the Tenants registration routes via Dapr service invocation (so the Task 9 production invoke allow-rule is the operative live-path control) or a direct service URL; mirror Tenants exactly.
4. **Open action item** — sprint-status.yaml#249-253 (John: "confirm C4 + C9 gating readiness before Epic 10 kickoff", `open`/high) is effectively a no-op now that both gates are `approved`; confirm/close.
5. **Live verification** — depends on Story 10.4 landing a populated, correctly-pruned `folders-index` and a DCP-capable CI lane (Epic 9 `aspire run` boot blocker). Author/dev 10.5 against the `IFolderSearchSource` seam + stubs now; gate full round-trip sign-off on 10.4.
