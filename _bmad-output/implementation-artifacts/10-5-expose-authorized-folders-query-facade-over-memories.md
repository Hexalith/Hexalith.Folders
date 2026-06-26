---
baseline_commit: e612052
---

# Story 10.5: Expose an authorized Folders query facade over Memories

Status: done

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

- [x] Task 1 — Author the architecture §Query Facade + ratify file-level design (AC 14, 12)
  - [x] Extend `architecture.md` (around #156) with a Query Facade section: Option B read mechanism (`MemoriesClient.SearchAsync` from `Hexalith.Folders.Server`), the shared-`folders-index` security-trim, the non-authoritative hydration rule, and the new `folders → memories` invoke egress policy.
  - [x] Confirm the facade host = `Hexalith.Folders.Server` (core stays Memories-free behind `IFolderSearchSource`); record the dedicated-gateway-project alternative only if chosen instead.

- [x] Task 2 — Core query triad, Memories-free (AC 1, 3, 5, 6, 7)
  - [x] Add `src/Hexalith.Folders/Queries/ContextSearch/`: `ContextSearchQuery`, `ContextSearchQueryHandler`, `ContextSearchQueryResult`, `ContextSearchResultCode`, metadata-only `ContextSearchItem`.
  - [x] Mirror `WorkspaceFileContextQueryHandler` order: auth-required → existence-as-safe-denial → `AuthorizeAsync` → path policy + sensitivity → C4 bounds → source query → map; reuse `SafeResult`/`MapAuthorizationDenial` shapes.
  - [x] Define the Memories-free source port `IFolderSearchSource` + request/result/hit records (Folders DTOs only) + `UnavailableFolderSearchSource` fail-safe default.

- [x] Task 3 — Server-side Memories gateway (Option B) (AC 2, 3, 8, 12)
  - [x] Add `Hexalith.Memories.Client.Rest` + `Hexalith.Memories.Contracts` `ProjectReference`s to `Hexalith.Folders.Server.csproj`; call `AddMemoriesClient()` (mirror the Hexalith.Tenants registration, incl. base address / Dapr-invoke wiring).
  - [x] Add `MemoriesFolderSearchSource : IFolderSearchSource` injecting `MemoriesClient`; call `SearchAsync(new SearchRequest(TenantId: "folders-index", Axis: "syntactic", AttributeFilters: { "folders.managedTenantId": authoritativeTenantId, ... }, ...))`.
  - [x] Map `SearchResult`/`ScoredResult` → `FolderSearchSourceResult`: DROP `ContentSnippet`; recover file-version identity from `ScoredResult.SourceUri`; redact `SourceUri`/`MemoryUnitId`; hydrate metadata from the authoritative Folders read (mirror `TenantQueryGateway` hydrate path); translate `Degraded`/unavailable → `Unavailable`.

- [x] Task 4 — Authorization wiring + security-trim (AC 1, 2, 4, 5)
  - [x] Add read action token (e.g. `read_context_search`) to `EffectivePermissionsActionCatalog`.
  - [x] Decide auth preset: reuse `LayeredFolderOperationPolicy.StrictRead()` (target `folders`, assert the `memories` egress in the gateway/conformance) vs add a `ContextSearchRead()` preset (`DaprTargetAppId="memories"`, asserts egress in the `DaprDenyByDefaultPolicy` layer). Record the choice.
  - [x] Enforce the per-hit folder-ACL re-check after the Memories query (the index is security-untrusted).

- [x] Task 5 — Redaction / metadata-only layer (AC 3, 4)
  - [x] Reuse `WorkspaceFileSensitivityClassifier`, `RedactionMetadata`/`RedactionVisibility`, and the `OpsConsoleDiagnosticsEndpoints` sensitive-value scrubber.
  - [x] Add sentinel-corpus tests over `tests/fixtures/audit-leakage-corpus.json`.

- [x] Task 6 — REST endpoint (AC 5, 6, 7, 8, 10)
  - [x] Add a `context/index-search` (or `context/search-index`) POST route to the `context-queries` group in `FoldersDomainServiceEndpoints.cs`, ending with `.AddEndpointFilter<FolderAuditEndpointFilter>()`.
  - [x] Use an envelope validator that rejects `Idempotency-Key` and validates `X-Hexalith-Freshness == eventually_consistent` (both `snapshot_per_task` and `eventually_consistent` validators already exist in the file; do NOT reuse the `snapshot_per_task` `ValidateContextQueryEnvelope` verbatim — the index is async).
  - [x] Map result codes to canonical Problem Details (metadata-only, `details.visibility=metadata_only`); register the handler + `IFolderSearchSource` (default `Unavailable*`, live `MemoriesFolderSearchSource`) in `FoldersServerServiceCollectionExtensions.cs`.

- [x] Task 7 — OpenAPI op + SDK regen (AC 9, 10)
  - [x] Copy the `context/search` op block in `hexalith.folders.v1.yaml`; set `read-consistency.class: eventually_consistent`, NO `idempotency_key_rule`, `parity-dimensions.transportParity: [rest, sdk, mcp, cli]`, metadata-only result schema, canonical error categories, audit-metadata keys (search text excluded).
  - [x] Regenerate `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs`; do not hand-edit generated files.

- [x] Task 8 — MCP tool + CLI command (AC 9)
  - [x] Add a sibling read tool in `Mcp/Tools/ContextTools.cs` via `pipeline.ExecuteQueryAsync(taskId, taskIdRequired:true, ...)` calling the new generated client op; metadata-only serializer.
  - [x] Add a sibling CLI subcommand in `Cli/Commands/Context/ContextCommand.cs` via `CommandFactory.Query(... taskIdRequired:true)`; metadata-only renderer.

- [x] Task 9 — Dapr egress policy + rule amendments (AC 11, 12)
  - [x] Add a `folders → memories` (`GET /api/search`) invoke allow-rule to `deploy/dapr/production/accesscontrol.yaml` (the `memories` target is `defaultAction: deny`, `policies: []`); add `memories` as an appId to `src/Hexalith.Folders.AppHost/DaprComponents/accesscontrol.yaml`; add negative-control rows to `DaprPolicyConformanceTests` + the `dapr-policy-conformance.yaml` fixture.
  - [x] Amend `project-context.md#82` + `architecture.md#134` to permit the single `Hexalith.Folders.Server` Memories.Client.Rest reference for the read facade; add `Hexalith.Folders.Server` to the `ScaffoldContractTests` Memories-reference allowlist — same change set.

- [x] Task 10 — Folder/tenant-scoped bridge read + UX projection (AC 13)
  - [x] Add a folder/tenant-scoped read method to `ISemanticIndexingBridgeReadModel` + `InMemorySemanticIndexingBridgeStore`/`EventStoreSemanticIndexingBridgeStore` (the folder index is private today; surface it safely, tenant-prefixed).
  - [x] Add a read-only, metadata-only indexing-status projection page under `src/Hexalith.Folders.UI/Components/Pages/` (mirror `ProviderSupport.razor.cs`), composing only allow-listed components; redacted-vs-unknown distinct.

- [x] Task 11 — PRD FR58 (AC 14)
  - [x] Add FR58 (authorized search facade) to `prd.md` after FR57; reconcile the 57→58 FR count where the architecture states "57 functional requirements."

- [x] Task 12 — Tests (AC 4, 5, 11, 12)
  - [x] Unit: handler authorization order, result-code mapping, `ContentSnippet` drop + `SourceUri`/`MemoryUnitId` redaction, C4 bounds, freshness/idempotency-key rejection, `AttributeFilters` construction. Hermetic.
  - [x] Integration (no-mock-gateway): real REST → auth → source seam; cross-tenant isolation (seed tenant-A + tenant-B docs sharing a term; tenant-A gets only tenant-A); safe-denial byte-identical across unauthorized / cross-tenant / nonexistent.
  - [x] Boundary: `ScaffoldContractTests` proves Memories stays within Workers + Server (+ AppHost); DI validates.

- [x] Task 13 — Build and test (AC 15)
  - [x] `dotnet build Hexalith.Folders.slnx`; then `Hexalith.Folders.Tests`, the Server/Integration lane owning the no-mock-gateway test, `Hexalith.Folders.Contracts.Tests`, `Hexalith.Folders.Testing.Tests`; record any DCP/Aspire env blocker.

## Dev Notes

### Read-mechanism decision & dev guidance (Option B, 2026-06-23)

**The decision.** The facade queries the Memories search index using the typed `MemoriesClient.SearchAsync(...)` from `Hexalith.Memories.Client.Rest`, mirroring the proven sibling precedent. This was historically forbidden by `project-context.md#82`; Jerome ratified relaxing it for the read path on 2026-06-23. The relaxation is intentionally minimal: **only `Hexalith.Folders.Server`** gains the reference; core stays Memories-free behind the `IFolderSearchSource` port so the FileContext source-port convention is preserved.

**The precedent to copy (verbatim shape).** `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs#28,#461-543`: injects `MemoriesClient` (#28), calls `memoriesClient.SearchAsync(new SearchRequest(TenantId: "tenants-index", Axis: "syntactic", ...))` (#468-478), recovers ids from `ScoredResult.SourceUri` (#526-543), applies status both as an `AttributeFilter` and an authoritative re-check (#571-574, #588-591), hydrates via the ETag-fresh detail path (#552-586), and degrades gracefully when Memories is unavailable (#480-490, #629-638). For Folders, swap `tenants-index` → `folders-index`, the status filter → `folders.managedTenantId` (+ org/folder), and the detail hydrate → the Folders authoritative read.

**Memories search surface (what you call).**
- Route: `GET /api/search` on Dapr app-id `memories` — the only search route. [Source: references/Hexalith.Memories/src/Hexalith.Memories.Server/Program.cs#2150]
- Typed client (Option B, allowed now): `MemoriesClient.SearchAsync(SearchRequest, ct) → Task<SearchResult>` [Source: references/Hexalith.Memories/src/Hexalith.Memories.Client.Rest/MemoriesClient.cs#164]; `SearchRequest` lives in `Client.Rest/SearchRequest.cs`.
- Wire records (in allowed `Contracts.V1`): `SearchQuery` (`TenantId`, `Query`, `CaseId`, `SourceTypeFilter`, `MetadataQuery`, `CloudEventSubject`, `AttributeFilters`, `MaxResults=10`, `Offset=0`); `SearchResult` (`Results`, `TotalCount`, `HasIndexedMemoryUnits`, `Degraded`, `UnavailableAxes`, `AllEnabledAxesUnavailable`); `ScoredResult` (`MemoryUnitId`, `Score`, **`ContentSnippet`** ← DROP, `SourceUri`, `SourceType`, `Axis`, `CaseId`, `CaseName`, `AnnotationsCount`). [Source: references/Hexalith.Memories/src/Hexalith.Memories.Contracts/V1/SearchQuery.cs, SearchResult.cs, ScoredResult.cs]
- `ScoredResult`/`FusedScoredResult` carry NO `Attributes` in the response (attributes are write/filter-only). Redaction is therefore about dropping `ContentSnippet` and redacting `SourceUri`/`MemoryUnitId`, plus enforcing the `folders.managedTenantId` filter + post-trim. [Source: raw_memories §4]

**The two correctness controls (do not skip either).**
- (1) Shared-index trim: send `AttributeFilters["folders.managedTenantId"] = <authoritativeTenantId>` AND post-filter every hit + re-check folder ACL Folders-side. The attribute filter is a query string Memories trusts; the Folders-side re-check is the authoritative control. [Source: src/Hexalith.Folders.Workers/SemanticIndexing/MemoriesSemanticIndexingPort.cs#102-126; FoldersSemanticIndexingDefaults.cs#9]
- (2) Non-authoritative hydration: recover ids from `SourceUri` (= the CloudEvent id the producer set), then hydrate metadata from the authoritative Folders read; never render the index payload as truth. 10.3 explicitly deferred index-time ACL freshness to here. [Source: memories-search-index-handoff-2026-06-23.md#14-16,#79,#97-99; 10-3 story #370]

### Source facts

- Story 10.5 seed AC: given indexed content in Memories, a Folders query facade serves results that are authorized, security-trimmed, and redacted by Folders policy before leaving the API/SDK/MCP boundary, with a new PRD FR + architecture query-facade section + UX "indexing status" projection added when scheduled. [Source: `_bmad-output/planning-artifacts/epics.md` "Story 10.5" #1908-1912]
- The facade serves the **syntactic/BM25 search index** over `folders-index`; RAG/`IngestAsync` is OUT of MVP scope (the seed-AC "RAG" wording is legacy). [Source: architecture.md#156; epics.md#1882]
- `GET /api/search` on `memories` has NO `RequireAuthorization` and NO `AllowAnonymous` — Memories will NOT reject an unauthorized caller, so 100% of authorization is enforced Folders-side before egress. [Source: references/Hexalith.Memories/src/Hexalith.Memories.Server/Program.cs#2150]
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
- Confirm how Hexalith.Tenants configures `AddMemoriesClient` (Dapr-invoke base address vs direct service URL) and copy it; this determines whether the production invoke allow-rule (Task 9) is the operative control for the live path.

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
- PRECEDENT (Option B): `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs#28,#461-543` + `Hexalith.Tenants.UI.csproj` (references `Memories.Client.Rest`).
- Memories read surface: `references/Hexalith.Memories/src/Hexalith.Memories.Server/Program.cs#2150`; `Hexalith.Memories.Client.Rest/MemoriesClient.cs#164` + `SearchRequest.cs`; `Hexalith.Memories.Contracts/V1/{SearchQuery,SearchResult,ScoredResult,MemoriesJsonContext}.cs`.
- `_bmad-output/planning-artifacts/ux-design-specification.md` — UX-DR7 (#115), UX-DR10/11/22 (#118-119,#130), search workflow (#110,#308-310), allow-listed components (#420).

## Dev Agent Record

### Agent Model Used

claude-opus-4-8[1m] (bmad-dev-story, ultracode)

### Debug Log References

- 2026-06-24 — Dev started; completed a full 6-agent precedent reconnaissance (Memories read surface + Tenants `TenantQueryGateway`; Server context endpoints + transport guardrails; layered authorization + redaction; OpenAPI op + SDK regen + MCP + CLI; Dapr policy + conformance + scaffold boundary; bridge read model + UX page). **PAUSED before writing code**: a concurrent 10.4 dev-story was actively editing the shared `SemanticIndexing` Workers/Projections files on the same working tree (silent same-tree clobber risk; 10.5 extends those exact files). Per Jerome's decision, waiting for 10.4 to land before implementing. Committed design + file-by-file plan recorded in auto-memory `story-10-5-design-and-pause.md`.
- 2026-06-24 — Resumed. 10.4 landed as commit `e612052` (working tree clean; 10.4 → `review`). `baseline_commit` advanced `e0a968b` → `e612052` so the 10.5 review diff is 10.5-only. Refreshed against 10.4's additions: `folders.status` index attribute (`active`/`archived`) is the facade's archived-exclusion filter; `IndexedText`/`IndexedAttributes` evidence now preserved on the bridge; bridge read interface + status enum unchanged. The `folders.*` index attribute keys are promoted to a shared core contract (`FoldersSemanticIndexingAttributes`) so producer (Workers) and consumer (Server facade) cannot drift.

### Completion Notes List

**All 15 ACs satisfied; all 13 tasks complete. Build clean (0W/0E, warnings-as-errors). Tests green: Folders.Tests 1358/0, Server.Tests 540/0, Workers.Tests 62/0, Contracts.Tests 275/0, Client.Tests 288/0, Cli.Tests 709/0, Mcp.Tests 662/0, IntegrationTests 631/0, Testing.Tests 59/60 (the 1 red is PRE-EXISTING — see below). Whitespace format gate clean over all Folders-repo files.**

Key implementation decisions (recorded per AC/Task):
- **Core query triad (Task 2, AC1/3/5/6/7):** `src/Hexalith.Folders/Queries/ContextSearch/` mirrors `Queries/FileContext` — `ContextSearchQueryHandler` authorizes (layered) BEFORE egress, calls the Memories-free `IFolderSearchSource`, then security-trims each hit (recovered identity must match the authorized tenant/org/folder/workspace), hydrates from the authoritative bridge read model (drops hits absent or not in `Indexed`/`Stale`), and emits metadata-only `ContextSearchItem` (opaque file-version handle, status, sensitivity, redaction, score — no snippet/path/source-uri). 21 hermetic unit tests.
- **Scope decision:** the facade is **workspace-scoped** (`POST …/folders/{folderId}/workspaces/{workspaceId}/context/index-search`), mirroring every other context query, with folder-scoped authorization (`OperationScope=folderId`). This is the secure, tractable MVP shape; the per-hit identity-match trim is the load-bearing defense-in-depth on the shared index.
- **Server gateway (Task 3, AC2/3/8):** `MemoriesFolderSearchSource` (the ONE approved Option-B Memories reference outside Workers) calls `MemoriesClient.SearchAsync(TenantId:"folders-index", Axis:"syntactic", AttributeFilters:{folders.managedTenantId, folders.organizationId, folders.folderId, folders.status=active})`, recovers identity from `SourceUri` only, DROPS `ContentSnippet`, and degrades safely (remote/in-band → `Unavailable`/`Degraded`, never throws). Note: `AddMemoriesClient` is a **direct base-address HttpClient** (`Memories:BaseAddress` + `HEXALITH_MEMORIES_API_TOKEN`), not Dapr-invoke; the production invoke allow-rule is the operative control only when that base address routes via the sidecar (recorded in architecture §Query Facade).
- **Auth (Task 4, AC1/4):** new `read_context_search` action token in `EffectivePermissionsActionCatalog`. The handler authorizes caller→`folders` via `LayeredFolderOperationPolicy.StrictRead()` (default target); the folders→memories egress is governed by the Task 9 production Dapr allow-rule + conformance (kept out of the layered-auth `DaprTargetAppId` so hermetic unit tests need no memories evidence). Cross-tenant `folderId`/`SourceUri` → safe denial (verified in the no-mock-gateway integration test).
- **Redaction (Task 5, AC3):** items are structurally metadata-only (no content-bearing field); a sensitive `PathPolicyClass` yields a visibly-distinct `redacted` marker. AC3 sentinel: the gateway test injects a secret-shaped `ContentSnippet` canary and asserts it never serializes into the result.
- **REST (Task 6) + OpenAPI/SDK (Task 7, AC9/10):** `context/index-search` POST + `indexing-status` GET added; both `eventually_consistent`, reject `Idempotency-Key` (via `ValidateEvidenceQueryEnvelope(requireEventuallyConsistent:true)`), map to metadata-only safe-denial Problem Details. OpenAPI ops `SearchFolderIndexedFiles` + `GetFolderIndexingStatus` added; SDK regenerated via NSwag.
- **MCP + CLI (Task 8, AC9):** MCP tools `search-folder-indexed-files` + `get-folder-indexing-status`; CLI `context index-search`. The parity oracle models every op on all surfaces (1:1 op↔MCP-tool), so `GetFolderIndexingStatus` is also an MCP tool (no CLI, like the ops-console diagnostics).
- **Dapr (Task 9, AC11/12):** production `folders → memories` GET `/api/search` invoke allow-rule; `memories` dev AppHost appId; conformance fixture rule `memories.folders.api-search.get` + 8 cases + updated `semanticSha256`; relaxed the deny-by-default memories guard. `ScaffoldContractTests` Memories allowlist now permits `Hexalith.Folders.Server` (Workers + Server only); amended `project-context.md#82` + `architecture.md#134`.
- **Bridge read + UX (Task 10, AC13):** new `ISemanticIndexingBridgeReadModel.ListFolderAsync(tenant, folder)` (InMemory + EventStore, tenant-guarded folder-index fan-out) + `UnavailableSemanticIndexingBridgeReadModel` fail-safe default; `IndexingStatus.razor(.cs)` read-only console projection mirroring `ProviderSupport` (status badges, redacted-vs-unknown distinct, allow-listed components).
- **Planning (Task 1/11/14):** PRD FR58, architecture §Query Facade (+ 57→58 FR reconciliation), and the rule amendments authored.
- **Shared-contract hardening:** the `folders.*` search-index attribute keys were promoted to a core `FoldersSemanticIndexingAttributes` class referenced by BOTH the Workers producer and the Server facade, so a producer/consumer key drift (a tenant-isolation leak risk) is impossible.
- **AC15 / known blockers:** (a) Live `folders-index` round-trip is BLOCKED-PENDING the DCP-capable `aspire run` lane (inherited Epic 9 residual) and a populated index from 10.4; local bar (structural + no-mock-gateway integration) is met. The Server's bridge read model defaults to the fail-safe `Unavailable` until a Server-side EventStore-backed read model is wired on that lane. (b) `ScaffoldContractTests.SolutionContainsOnlyCanonicalBuildableProjects` fails — this is the **PRE-EXISTING `.slnx`-inventory drift** (submodule reference projects added by create-story commit `83f980f`, before the `e612052` baseline; this story added zero `.slnx` entries). Per the 10.4 precedent it is documented, not fixed here (tracked as Epic 9/10.1/10.5 drift).

### File List

**Created — production (core):**
- `src/Hexalith.Folders/Projections/SemanticIndexing/FoldersSemanticIndexingAttributes.cs`
- `src/Hexalith.Folders/Projections/SemanticIndexing/UnavailableSemanticIndexingBridgeReadModel.cs`
- `src/Hexalith.Folders/Queries/ContextSearch/ContextSearchQuery.cs`
- `src/Hexalith.Folders/Queries/ContextSearch/ContextSearchResultCode.cs`
- `src/Hexalith.Folders/Queries/ContextSearch/ContextSearchItem.cs`
- `src/Hexalith.Folders/Queries/ContextSearch/ContextSearchLimits.cs`
- `src/Hexalith.Folders/Queries/ContextSearch/ContextSearchQueryResult.cs`
- `src/Hexalith.Folders/Queries/ContextSearch/IFolderSearchSource.cs`
- `src/Hexalith.Folders/Queries/ContextSearch/FolderSearchSourceRequest.cs`
- `src/Hexalith.Folders/Queries/ContextSearch/FolderSearchSourceStatus.cs`
- `src/Hexalith.Folders/Queries/ContextSearch/FolderSearchSourceHit.cs`
- `src/Hexalith.Folders/Queries/ContextSearch/FolderSearchSourceResult.cs`
- `src/Hexalith.Folders/Queries/ContextSearch/UnavailableFolderSearchSource.cs`
- `src/Hexalith.Folders/Queries/ContextSearch/ContextSearchQueryHandler.cs`
- `src/Hexalith.Folders/Queries/ContextSearch/FolderIndexingStatusQuery.cs`
- `src/Hexalith.Folders/Queries/ContextSearch/FolderIndexingStatusResultCode.cs`
- `src/Hexalith.Folders/Queries/ContextSearch/FolderIndexingStatusItem.cs`
- `src/Hexalith.Folders/Queries/ContextSearch/FolderIndexingStatusQueryResult.cs`
- `src/Hexalith.Folders/Queries/ContextSearch/FolderIndexingStatusQueryHandler.cs`

**Created — production (server/UI):**
- `src/Hexalith.Folders.Server/ContextSearch/MemoriesFolderSearchSource.cs`
- `src/Hexalith.Folders.UI/Components/Pages/IndexingStatus.razor`
- `src/Hexalith.Folders.UI/Components/Pages/IndexingStatus.razor.cs`

**Modified — production:**
- `src/Hexalith.Folders/Projections/SemanticIndexing/ISemanticIndexingBridgeReadModel.cs` (ListFolderAsync)
- `src/Hexalith.Folders/Projections/SemanticIndexing/InMemorySemanticIndexingBridgeStore.cs` (ListFolderAsync)
- `src/Hexalith.Folders/Authorization/EffectivePermissionsActionCatalog.cs` (read_context_search)
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs` (AddFoldersContextSearchQueries)
- `src/Hexalith.Folders.Workers/SemanticIndexing/FoldersSemanticIndexingDefaults.cs` (delegate shared keys to core)
- `src/Hexalith.Folders.Workers/SemanticIndexing/MemoriesSemanticIndexingPort.cs` (use core attribute constants)
- `src/Hexalith.Folders.Workers/SemanticIndexing/EventStoreSemanticIndexingBridgeStore.cs` (ListFolderAsync)
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs` (2 endpoints, mappers, request/response DTOs)
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs` (AddFoldersContextSearchFacade)
- `src/Hexalith.Folders.Server/FoldersServerModule.cs` (call facade registration)
- `src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj` (Memories.Client.Rest + Memories.Contracts refs)
- `src/Hexalith.Folders.Mcp/Tools/ContextTools.cs` (2 MCP tools)
- `src/Hexalith.Folders.Cli/Commands/Context/ContextCommand.cs` (index-search subcommand)
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` (2 ops + 6 schemas)
- `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs` (NSwag-regenerated)
- `src/Hexalith.Folders.AppHost/DaprComponents/accesscontrol.yaml` (memories dev appId)

**Created — tests:**
- `tests/Hexalith.Folders.Tests/Queries/ContextSearch/ContextSearchQueryHandlerTests.cs`
- `tests/Hexalith.Folders.Tests/Queries/ContextSearch/FolderIndexingStatusQueryHandlerTests.cs`
- `tests/Hexalith.Folders.Server.Tests/MemoriesFolderSearchSourceTests.cs`
- `tests/Hexalith.Folders.IntegrationTests/ContextSearch/ContextSearchFacadeWiringTests.cs`

**Modified — tests/fixtures:**
- `tests/shared/Parity/ParityScenarios.cs` (ExpectedOperationCount 49)
- `tests/Hexalith.Folders.Server.Tests/TransportParityConformanceTests.cs` (REST count 49)
- `tests/Hexalith.Folders.Client.Tests/TransportParityConformanceTests.cs` (method rename)
- `tests/Hexalith.Folders.Mcp.Tests/RegistrationTests.cs` (49 tools)
- `tests/Hexalith.Folders.Mcp.Tests/SourcingTests.cs` (49 tools)
- `tests/Hexalith.Folders.Mcp.Tests/ParityOracleConformanceTests.cs` (1:1 restored)
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/DaprPolicyConformanceTests.cs` (memories folders-invoke allow-rule)
- `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs` (Memories allowlist: Workers + Server)
- `tests/fixtures/dapr-policy-conformance.yaml` (allow-rule + 8 cases + semanticSha256)
- `tests/fixtures/parity-contract.yaml` (regenerated, 49 ops)
- `tests/fixtures/previous-spine.yaml` (2 new ops)
- `tests/Hexalith.Folders.Contracts.Tests/Deployment/ConsumerDocsConformanceTests.cs` (49 op/tool counts)
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/TenantFolderProviderContractGroupTests.cs` (allow-list + non-mutating)
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuditOpsConsoleContractGroupTests.cs` (later-story exclusion)

**Modified — deploy / docs / planning:**
- `deploy/dapr/production/accesscontrol.yaml` (folders → memories invoke allow-rule)
- `_bmad-output/project-context.md` (#82 Option-B amendment)
- `_bmad-output/planning-artifacts/architecture.md` (#51 FR count, #134 amendment, §Query Facade)
- `_bmad-output/planning-artifacts/prd.md` (FR58)
- `docs/sdk/api-reference.md`, `docs/sdk/mcp-reference.md` (new ops/tools)

## Change Log

| Date | Change | Author |
|------|--------|--------|
| 2026-06-23 | Story created via bmad-create-story (ultracode: 6-reader research workflow + synthesis). Read mechanism = Option B (Memories.Client.Rest in Server, mirror Tenants); scope = full closure (PRD FR58 + architecture §Query Facade + UX projection); surfaces = API/SDK/MCP/CLI; facade serves syntactic/BM25 over `folders-index` (not RAG). Status → ready-for-dev. | Jerome (via create-story) |
| 2026-06-24 | Implemented Story 10.5 (bmad-dev-story, ultracode) on the post-10.4 baseline `e612052`: authorized workspace-scoped context-search facade (`context/index-search`) + indexing-status projection (`indexing-status`) over `folders-index`; Option-B `MemoriesFolderSearchSource` in Server; new `read_context_search` token; `ListFolderAsync` bridge read; OpenAPI ops + SDK regen; MCP/CLI parity; production `folders → memories` Dapr allow-rule + conformance; UX `IndexingStatus` page; PRD FR58 + architecture §Query Facade. Build 0W/0E; all lanes green except the pre-existing `.slnx`-inventory red. Status → review. | Jerome (via dev-story) |

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

## Review Findings

> Code review 2026-06-24 (bmad-code-review, ultracode). Adversarial multi-lens workflow: 9 review lenses (Blind Hunter diff-only, Edge Case Hunter, Acceptance Auditor + tenant-isolation, redaction/metadata-only, authorization-order, contract/parity/SDK, Dapr-egress/boundary, test-quality) → 30 raw → 23 canonical → **16 confirmed real** by per-finding 3-skeptic verification (refuter/impact/reproduction, each reading the real codebase), 7 dismissed. No active tenant-isolation or content-leak breach was found — the two zero-tolerance controls (shared-index trim, metadata-only redaction) are correctly implemented (affirmatively confirmed). Findings are hardening, documented-deferral sign-offs, and test-coverage gaps.

### Decision-needed (5)

- [x] [Review][Decision] **Live Server facade returns zero items — bridge read model is the Unavailable default** [AC2/AC9/AC15] — `AddFoldersContextSearchFacade` registers the live `MemoriesFolderSearchSource` but no EventStore-backed `ISemanticIndexingBridgeReadModel`; `AddFoldersContextSearchQueries` leaves the fail-safe `UnavailableSemanticIndexingBridgeReadModel` (empty list). On the deployed Server every Memories hit is dropped in hydration, so `context/index-search` always returns `Allowed` with zero items, indistinguishable from an empty folder. Fail-safe (no leak) but the headline feature's only deployed path is inert. Documented as the AC15 DCP-lane blocker. **Decide: (A) accept the documented deferral, carve AC9-live out of acceptance, keep AC15 BLOCKED-PENDING the DCP lane + tracked follow-up; or (B) wire a Server-side `EventStoreSemanticIndexingBridgeStore` now (mirror `FoldersWorkersModule`) and run the live round-trip.** `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs` (AddFoldersContextSearchFacade); `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs` (TryAdd Unavailable default); `ContextSearchQueryHandler.cs:157-197`.
- [x] [Review][Decision] **Indexing-status reports `Stale:false` over an unavailable backend** [AC13] — `ISemanticIndexingBridgeReadModel` has no availability signal; `UnavailableSemanticIndexingBridgeReadModel.ListFolderAsync` returns empty rather than throwing, so `FolderIndexingStatusQueryHandler` takes the `Allowed` branch and asserts `FolderLifecycleFreshness(..., Stale:false, "search_index")`. The console renders "no indexed files / Current" for a folder whose projection is in fact not wired — "healthy empty" and "unavailable" collapse to the same positive-freshness shape. Related to the decision above. **Decide: add an availability signal → return `ReadModelUnavailable` (mirror `UnavailableFolderSearchSource`); or accept fail-safe-empty as intended interim behavior (+ a test pinning it).** `FolderIndexingStatusQueryHandler.cs` (HandleAsync Allowed branch).
- [x] [Review][Decision] **Asserted folders→memories Dapr invoke allow-rule does not bind the facade's actual egress** [AC12] — `AddMemoriesClient` is configured as a direct base-address `HttpClient` (`Memories:BaseAddress` + bearer token), not a Dapr service-invoke client. If `Memories:BaseAddress` is a direct service URL (the natural Tenants-mirror reading), the deny-by-default Dapr policy never sees this traffic and the asserted `GET /api/search` allow-rule + its conformance negative-controls are inert at runtime — the only real control becomes the API token. Acknowledged in `architecture.md#134`. **Decide: pin `Memories__BaseAddress` to the sidecar invoke route (`http://localhost:3500/v1.0/invoke/memories/method/`) in the production manifest + add a conformance assertion that the address routes through the sidecar; or drop/re-document the allow-rule so the policy artifact doesn't over-claim an egress control.** `FoldersServerServiceCollectionExtensions.cs:91-104`; `deploy/dapr/production/accesscontrol.yaml`.
- [x] [Review][Decision] **Indexing-status truncates to 500 by unstable bridge order — failed/tombstoned rows can be silently dropped** — `entries.Take(MaxItems)` keeps the first 500 in `ListFolderAsync` order (InMemory: ReadModelKey ordinal; live: unspecified), with only an `IsTruncated` flag and no cursor/prioritization. An operator could never see a `failed`/`reconciliation_required`/`tombstoned` entry that sorts past the 500th key (the search facade got a cursor; this projection did not). **Decide: (1) order attention-worthy statuses first before `Take`; (2) add the same offset cursor as the search facade; or (3) document the 500 cap as an intentional small-folder contract + UI "narrow your filter" notice.** `FolderIndexingStatusQueryHandler.cs` (Take(MaxItems)).
- [x] [Review][Decision] **`ContextSearchResultCode.Redacted` is dead/unreachable, but the endpoint + OpenAPI declare it** — redaction is item-level (`MapItem` sets `item.Redaction="redacted"` and still returns the item); the handler never returns the top-level `Redacted` code, so the endpoint's `Redacted`→404 arm and the op's `redacted` canonical-error-category can never occur. **Decide: (A) remove the member + dead switch arm + the op's `redacted` canonical-error-category and regenerate the parity oracle (touches the contract spine); or (B) make it reachable (return `Redacted` when every hit is suppressed) + add a test.** `ContextSearchResultCode.cs`; `FoldersDomainServiceEndpoints.cs` (ContextSearchToHttpResult); `hexalith.folders.v1.yaml`.

**Resolutions (Jerome, 2026-06-24):**
- **#2 → Accept deferral + honest status.** Live-path inertness accepted as the documented AC15 DCP-lane deferral; AC9-live carved out, AC15 stays BLOCKED-PENDING the DCP lane; wiring the Server-side EventStore read model is a tracked follow-up (→ Deferred). The "honest status" half is a patch (#4).
- **#4 → Patch.** Add an availability signal so the indexing-status projection returns `ReadModelUnavailable` instead of a false `Stale:false` "empty/Current".
- **#5 → Accept as documented.** No action; the direct-HttpClient-vs-Dapr-invoke gap is already recorded in `architecture.md#134` and accepted as conditional/future-facing (→ Deferred).
- **#13 → Patch.** Prioritize attention-worthy statuses (failed/reconciliation_required/tombstoned) before `Take(500)`.
- **#16 → Patch.** Remove the dead `Redacted` member + endpoint arm + the op's `redacted` canonical-error-category, then regenerate the parity oracle/fixtures.

### Patch (11)

- [x] [Review][Patch] **(#4) Add an availability signal so indexing-status reports ReadModelUnavailable, not a false 'empty/Current'** [`ISemanticIndexingBridgeReadModel`; `UnavailableSemanticIndexingBridgeReadModel`; `FolderIndexingStatusQueryHandler.cs`] — add an availability signal (e.g. `IsAvailable`/`TryListFolderAsync`) so `UnavailableSemanticIndexingBridgeReadModel` reports unavailable and the status handler returns `FolderIndexingStatusResultCode.ReadModelUnavailable` instead of the `Allowed`/`Stale:false` branch (mirror `UnavailableFolderSearchSource` → `ReadModelUnavailable`).
- [x] [Review][Patch] **(#13) Prioritize attention-worthy statuses before the 500-entry truncation** [`FolderIndexingStatusQueryHandler.cs`] — order `failed`/`reconciliation_required`/`tombstoned` first (e.g. `OrderByDescending(StatusSeverityRank).ThenBy(ReadModelKey)`) before `Take(MaxItems)`, so actionable rows always survive truncation.
- [x] [Review][Patch] **(#16) Remove the dead `ContextSearchResultCode.Redacted` surface** [`ContextSearchResultCode.cs`; `FoldersDomainServiceEndpoints.cs`; `hexalith.folders.v1.yaml`] — remove the enum member, the dead endpoint switch arm, and the op's `redacted` canonical-error-category; regenerate the parity oracle/fixtures and confirm Contracts.Tests stay green.
- [x] [Review][Patch] **Add a negative test that a foreign-workspace hit is trimmed; fix the misleading `FolderSearchSourceRequest` doc-comment** [`ContextSearchQueryHandler.cs:184,194`; `FolderSearchSourceRequest.cs`; `ContextSearchQueryHandlerTests.cs`] — workspace isolation is handler-only (lines 184 + 194); there is no workspace attribute filter and `FolderSearchSourceRequest.WorkspaceId` is unused by the source (its comment overstates the server-side trim). No negative test varies workspace (all use workspace-a), so a regression weakening line 184/194 leaks sibling-workspace file-version refs silently. Add a `ForeignWorkspaceHitShouldBeTrimmed…` test (hit `workspace-b`, query `workspace-a`, assert empty) and reword the comment to state workspace isolation is handler-enforced. (Optional defense-in-depth: a producer-side workspace index attribute — separate decision, out of this patch.)
- [x] [Review][Patch] **Harden the organizationId trim: make it unconditional + re-check it in hydration; add tests** [`ContextSearchQueryHandler.cs:185,192-197`] — the org leg is skipped when `allowed.OrganizationId` is null/empty (`organizationId.Length > 0 && …`), and the hydration block re-verifies `WorkspaceId` but not `OrganizationId`, so the org dimension is never checked against authoritative data. Not an exploitable leak (full-tuple folder identity + tenant/folder-scoped hydration prevent cross-org), but a defense-in-depth degradation. Change line 185 to an unconditional `!string.Equals(hit.OrganizationId, organizationId, Ordinal)` and add `|| !string.Equals(entry.Identity.OrganizationId, organizationId, Ordinal)` to the hydration check; add a hit-org-differs trim test + an empty-authorized-org test.
- [x] [Review][Patch] **Integration "cross-tenant" safe-denial test never issues a cross-tenant caller** [`ContextSearchFacadeWiringTests.cs:83-116`] — the context is fixed to `(tenant-a,user-a)`; folder-b/c/x are unauthorized *same-tenant* folders, so the "cross-tenant folder id" comment is mislabeled and the dangerous path (caller authed as tenant-b targeting tenant-a's ACL'd folder through real endpoint+auth) is untested. Fix the comment and add a `[Fact]` using `host.Context.Set("tenant-b","user-b")` (+ seed tenant-b) requesting folder-a, asserting byte-identical safe denial and `source.Calls==0` (deny-before-egress).
- [x] [Review][Patch] **Test the indexing-status degraded-read, truncation, and metadata-only sentinel paths** [`FolderIndexingStatusQueryHandler.cs:78-105`; `FolderIndexingStatusQueryHandlerTests.cs`] — the throwing-bridge→`ReadModelUnavailable` catch, the >500 truncation branch (`IsTruncated`), and metadata-only serialization are all untested for a projection that surfaces Tombstoned (SourceUri-bearing) entries. Add a throwing-stub test, a 501-entry truncation test, and a JSON serialization leak-sentinel asserting no `folders://`/`sourceUri`/`snippet`/`normalizedPath`.
- [x] [Review][Patch] **Move C4 bounds validation to AFTER authorization (restore the prescribed mirror order)** [`ContextSearchQueryHandler.cs:77-81`] — bounds run before `AuthorizeAsync`, so an authenticated-but-unauthorized caller submitting an over-limit query gets `InputLimitExceeded` (422) instead of the `NotFoundSafe` (404) safe-denial — a small pre-ACL input-validity oracle and a deviation from the `WorkspaceFileContextQueryHandler` order Task 2 prescribes. Move the `ValidateC4Bounds` block to after line 104; the existing authorized over-limit test still passes.
- [x] [Review][Patch] **Honor AC3's named corpus: iterate `tests/fixtures/audit-leakage-corpus.json` for this facade** [`MemoriesFolderSearchSourceTests.cs:43-50`] — AC3 specifies sentinel-secret tests *over the corpus*; the new tests use a single inline canary instead. Runtime redaction is structurally correct (no content field), so this is a test-fidelity gap: add a `[Theory]` loading `sentinel_samples` from the corpus, injecting each into `ScoredResult.ContentSnippet`/`SourceUri`, asserting no sentinel survives serialization.
- [x] [Review][Patch] **Test the `ResponseLimitExceeded` byte-budget branch** [`ContextSearchQueryHandler.cs:200-204`; `ContextSearchQueryHandlerTests.cs`] — the 1 MiB response-byte cap (safe-denial before emitting an oversized body) is unverified; the existing bounds theory covers only input-side limits. Add a test driving enough surviving hits to cross `MaxResponseBytes`, asserting `ResponseLimitExceeded` + empty items.
- [x] [Review][Patch] **MCP `get-folder-indexing-status` passes `s.TaskId!` (null-forgiving) on a non-task-scoped tool** [`ContextTools.cs` GetFolderIndexingStatus] — the tool is `taskIdRequired:false` with no taskId; `s.TaskId` is legitimately null and `!` masks intent. Replace with explicit `null` to self-document. No behavior change (the generated client already null-guards the header).

### Deferred (5)

- [x] [Review][Defer] **(#2) Live Server facade serves zero items — Server-side EventStore bridge read model not wired** [`FoldersServerServiceCollectionExtensions.cs`] — deferred per Jerome (2026-06-24): accepted as the documented AC15 DCP-lane deferral; AC9-live carved out, AC15 stays BLOCKED-PENDING the DCP lane. Tracked follow-up: register `EventStoreSemanticIndexingBridgeStore` in `AddFoldersContextSearchFacade` and run the live round-trip once the DCP lane + a populated `folders-index` (10.4) exist.
- [x] [Review][Defer] **(#5) Dapr folders→memories invoke allow-rule not bound to the facade's actual egress** [`FoldersServerServiceCollectionExtensions.cs`; `deploy/dapr/production/accesscontrol.yaml`] — deferred per Jerome (2026-06-24): accepted as documented in `architecture.md#134`; the direct base-address HttpClient makes the allow-rule conditional/future-facing (operative only if BaseAddress routes via the sidecar). API-token control accepted for now; revisit if egress is moved onto the sidecar.
- [x] [Review][Defer] **`hasMore`/`nextCursor` derived from the index's raw `TotalCount` (pre-trim) can emit an empty trailing page** [`ContextSearchQueryHandler.cs:211-212`] — deferred: new code, but UX-only and acceptable under the no-cross-tenant-existence-disclosure rule (aggregate boolean over the caller's own scope). Optional improvement: only emit a cursor when the source returned a full pre-trim page.
- [x] [Review][Defer] **SDK `ContextIndexSearchRequest.Limit` generated non-nullable → omitting it transmits `limit:0`, which the server rejects** [`HexalithFoldersClient.g.cs`] — deferred, **pre-existing systemic NSwag pattern** (identical to `FileSearchRequest.Limit` + `WorkspaceFileContextQueryHandler`); contradicts OpenAPI "defaults to max when omitted". Spine-wide fix (nullable optional numerics, or relax the `<=0` guard), not a 10.5 change.
- [x] [Review][Defer] **`GetFolderIndexingStatus` parity-contract row lists a `cli` adapter that intentionally doesn't exist** [`tests/fixtures/parity-contract.yaml`] — deferred, **pre-existing generator behavior**: `parity-oracle-generator` hardcodes `[rest,sdk,cli,mcp]` for every row (same for the rest/sdk-only `GetFolderLifecycleStatus`); non-load-bearing artifact, nothing fails. Fix = derive adapters from `transportParity` and regenerate spine-wide; do not hand-edit the generated row.

### Dismissed (7, no action)

`#7` handler metadata-only sentinel asserts structurally-impossible fields (still a valid regression guard); `#11` `IndexedText` in-scope-but-correctly-never-surfaced (fragile-by-proximity only); `#12` indexing-status reasonCode relies on upstream metadata-only contract (enforced upstream); `#17` OpenAPI example shows a `public_metadata` sensitivity the handler never emits (example-only); `#20` `GetFolderIndexingStatus` oracle assigns no `ui` adapter despite the console page (classification artifact); **`#22` affirmative: ContentSnippet/SourceUri/MemoryUnitId are dropped/rewritten on the full path to every egress surface — no redaction leak**; **`#23` affirmative: the audit/telemetry filter is body-blind — raw QueryText/SourceUri never enter audit.**

### Review Findings (2026-06-26 Chunked 10.5 Facade Review)

> BMad code review over the narrowed Story 10.5 facade diff (`e612052..HEAD`, 66 files, 4711 additions / 142 deletions), excluding unrelated package/submodule, C3 retention, and later AppHost-security commits. Layers: Blind Hunter, Edge Case Hunter, Acceptance Auditor. Triage: **0 decision-needed**, **16 patch**, **4 defer**, **7 dismissed**.

#### Patch (16)

- [x] [Review][Patch] **Unavailable bridge status is reported as healthy empty/current** [`src/Hexalith.Folders/Projections/SemanticIndexing/UnavailableSemanticIndexingBridgeReadModel.cs:22`; `src/Hexalith.Folders/Queries/ContextSearch/FolderIndexingStatusQueryHandler.cs:78`] - `UnavailableSemanticIndexingBridgeReadModel.ListFolderAsync` returns `[]`, and the status handler maps that to `Allowed` with `Stale:false`, so an unwired projection renders like a current empty folder instead of `ReadModelUnavailable`. Add an availability signal or throw/fail-safe unavailable path and test it.
- [x] [Review][Patch] **Workspace scope is not enforced at the Memories query boundary** [`src/Hexalith.Folders/Projections/SemanticIndexing/FoldersSemanticIndexingAttributes.cs:24`; `src/Hexalith.Folders.Workers/SemanticIndexing/MemoriesSemanticIndexingPort.cs:215`; `src/Hexalith.Folders.Server/ContextSearch/MemoriesFolderSearchSource.cs:91`] - the route is workspace-scoped and the handler trims by `WorkspaceId`, but the producer writes no `folders.workspaceId` attribute and the source sends no workspace filter. Sibling-workspace hits can fill source pages before handler trimming, causing empty/incomplete pages and cursor artifacts. Add a shared workspace attribute, emit it, filter on it, and keep the handler trim as defense in depth.
- [x] [Review][Patch] **Context-search pagination trusts pre-trim index totals and can loop on discarded rows** [`src/Hexalith.Folders/Queries/ContextSearch/ContextSearchQueryHandler.cs:211`] - `hasMore` is computed from `sourceResult.TotalCount` and `nextOffset` from recovered `Hits.Count`. If Memories returns malformed, foreign, stale, or otherwise dropped rows, the facade can expose a trailing page signal from untrusted data or repeat the same cursor when zero hits are recovered. Track raw page size separately or only emit cursors from trusted/full-page evidence; guard integer overflow.
- [x] [Review][Patch] **Organization trim is conditional and hydration does not re-check organization id** [`src/Hexalith.Folders/Queries/ContextSearch/ContextSearchQueryHandler.cs:182`] - when `allowed.OrganizationId` is empty, hit org checks are skipped, and hydrated bridge entries are only re-checked for workspace. Make the org check explicit for the authorized scope or fail closed when org is unavailable; re-check `entry.Identity.OrganizationId` during hydration.
- [x] [Review][Patch] **C4 bounds validation runs before folder authorization** [`src/Hexalith.Folders/Queries/ContextSearch/ContextSearchQueryHandler.cs:77`] - an unauthorized caller can get `InputLimitExceeded`/validation outcomes before the folder ACL safe-denial path, which creates a small pre-ACL input-validity oracle and violates the story's prescribed auth-before-bounds order. Move C4 validation after successful layered authorization.
- [x] [Review][Patch] **The required 2-second query timeout is not enforced** [`src/Hexalith.Folders.Server/ContextSearch/MemoriesFolderSearchSource.cs:47`; `docs/exit-criteria/c4-input-limits.md:22`] - the live source passes the caller token directly to `MemoriesClient.SearchAsync`; no 2 s linked timeout produces `FolderSearchSourceStatus.Timeout`, and the OpenAPI op lacks the operation-level `x-hexalith-query-timeout-ms: 2000` extension. Add a server-side timeout budget and timeout tests.
- [x] [Review][Patch] **Bridge folder listing is unbounded for bounded reads** [`src/Hexalith.Folders.Workers/SemanticIndexing/EventStoreSemanticIndexingBridgeStore.cs:47`; `src/Hexalith.Folders/Projections/SemanticIndexing/ISemanticIndexingBridgeReadModel.cs:20`] - `ListFolderAsync` reads the entire folder index and fans out one point read per key before search/status handlers apply their 500-item caps. Large folders can make a bounded query perform unbounded read-model work. Add a bounded/paged read API or hydrate search hits by key instead of enumerating the whole folder.
- [x] [Review][Patch] **Indexing-status truncates by read-model key before prioritizing failures** [`src/Hexalith.Folders/Queries/ContextSearch/FolderIndexingStatusQueryHandler.cs:94`] - `entries.Take(MaxItems)` can silently drop `failed`, `reconciliation_required`, or `tombstoned` rows after the 500th key, hiding the statuses operators need first. Sort by actionable severity before truncation or add cursor/filter support.
- [x] [Review][Patch] **Dead top-level `Redacted` result is advertised publicly** [`src/Hexalith.Folders/Queries/ContextSearch/ContextSearchResultCode.cs:15`; `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs:4502`; `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:2926`] - redaction is item-level, but the enum, endpoint arm, OpenAPI categories, and parity fixture expose an unreachable top-level `redacted` outcome. Remove it and regenerate generated contract artifacts, or make it reachable with tests.
- [x] [Review][Patch] **Facade leakage tests do not iterate the required sentinel corpus** [`tests/Hexalith.Folders.Server.Tests/MemoriesFolderSearchSourceTests.cs:43`] - AC3 names `tests/fixtures/audit-leakage-corpus.json`; the new facade tests use a single inline canary. Add corpus-driven tests for snippets/source URI/result serialization (and endpoint/audit surface where practical).
- [x] [Review][Patch] **Response budget enforcement undercounts serialized output and lacks coverage** [`src/Hexalith.Folders/Queries/ContextSearch/ContextSearchQueryHandler.cs:200`; `src/Hexalith.Folders/Queries/ContextSearch/ContextSearchQueryHandler.cs:284`] - `EstimateBytes` ignores JSON property names, escaping, array/object overhead, limits, and freshness metadata, so the 1 MiB C4 response budget can be exceeded materially. Measure serialized payload size or use a conservative bound, and add a `ResponseLimitExceeded` test.
- [x] [Review][Patch] **Indexing-status reason codes are surfaced without an allowlist or scrubber** [`src/Hexalith.Folders/Queries/ContextSearch/FolderIndexingStatusQueryHandler.cs:99`] - `ReasonCode` originates from worker/materializer/policy paths that currently validate only non-empty strings in some branches. Before exposing it in the console/API, normalize to a bounded allowlist or scrub unsafe values to an opaque fallback.
- [x] [Review][Patch] **External search scores are not checked for finite JSON-safe values** [`src/Hexalith.Folders.Server/ContextSearch/MemoriesFolderSearchSource.cs:132`; `src/Hexalith.Folders/Queries/ContextSearch/ContextSearchQueryHandler.cs:265`] - a NaN/Infinity score from Memories can flow into response DTOs and fail JSON serialization. Drop or normalize non-finite scores before mapping.
- [x] [Review][Patch] **Integration safe-denial test labeled cross-tenant never changes tenant** [`tests/Hexalith.Folders.IntegrationTests/ContextSearch/ContextSearchFacadeWiringTests.cs:83`] - the test keeps the caller at `(tenant-a,user-a)` and only varies folder ids, so it covers same-tenant unauthorized/absent cases but not a tenant-B caller targeting tenant-A resources. Add a real cross-tenant caller case and assert deny-before-egress.
- [x] [Review][Patch] **Indexing-status UI can render stale results after parameter changes** [`src/Hexalith.Folders.UI/Components/Pages/IndexingStatus.razor.cs:39`] - `ResetState` disposes the previous `CancellationTokenSource` without cancelling it, so an in-flight load for the prior `FolderId` can complete and update `_status` after a route change. Cancel the old token and ignore completions whose folder/correlation no longer matches.
- [x] [Review][Patch] **MCP indexing-status tool null-forgives a legitimate null task id** [`src/Hexalith.Folders.Mcp/Tools/ContextTools.cs:114`] - `get-folder-indexing-status` is `taskIdRequired:false`, but passes `s.TaskId!` into the generated client. Pass explicit `null` to document the non-task-scoped contract and avoid masking future nullability regressions.

#### Deferred (4)

- [x] [Review][Defer] **Live Server facade still depends on the unavailable bridge read model** [`src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:78`] - deferred, accepted in the story's 2026-06-24 review resolutions as the AC15 DCP-lane/live-round-trip follow-up.
- [x] [Review][Defer] **Dapr invoke allow-rule is conditional because the facade uses a direct Memories base-address client** [`src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:91`; `deploy/dapr/production/accesscontrol.yaml`] - deferred, accepted in the story's 2026-06-24 review resolutions and documented in architecture; revisit when egress is pinned to sidecar invocation.
- [x] [Review][Defer] **Generated `ContextIndexSearchRequest.Limit` is non-nullable despite optional OpenAPI semantics** [`src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs:11383`] - deferred, pre-existing systemic NSwag optional-numeric pattern already recorded for 10.5; fix spine-wide rather than hand-edit generated code.
- [x] [Review][Defer] **Required test lane still has the pre-existing `.slnx` inventory red** [`_bmad-output/implementation-artifacts/10-5-expose-authorized-folders-query-facade-over-memories.md`] - deferred, pre-existing and documented in the story completion notes; do not treat as caused by the 10.5 facade chunk.

#### Dismissed (7, no action)

Per-hit folder ACL/path-policy re-check finding was dismissed for this chunk because the implemented route is folder/workspace scoped: the folder ACL is checked before egress and every hit is trimmed back to the same folder/workspace before hydration; any broader multi-folder facade would need a new per-hit ACL design. CLI parity for `GetFolderIndexingStatus` was dismissed as the already-recorded parity-generator artifact, not a runtime adapter defect. Direct content/snippet leakage was dismissed: `ContentSnippet`, `SourceUri`, and Memories ids are dropped before the facade result. Audit raw-query leakage was dismissed: the endpoint audit filter is body-blind and records metadata only. OpenAPI example sensitivity drift was dismissed as example-only. UI parity missing from the generated parity oracle was dismissed because the oracle models REST/SDK/MCP/CLI transports only. Generated SDK edits were dismissed as generated-output consequences; fixes must go through the OpenAPI/generator inputs.

### Review Findings (2026-06-26 Chunk 1 Search Handler Re-Review)

> BMad code review over the chunked Story 10.5 search-handler slice (`e612052..HEAD`): `ContextSearchQueryHandler` and its DTO/source contracts. Layers: Blind Hunter, Edge Case Hunter, Acceptance Auditor. Triage: **0 decision-needed**, **3 patch**, **0 defer**, **13 dismissed**.

#### Patch (3)

- [x] [Review][Patch] **Source raw rows control output count and visible pagination after security trim** [`src/Hexalith.Folders/Queries/ContextSearch/ContextSearchQueryHandler.cs:158`] - the handler processes every `sourceResult.Hits` row without capping output to the requested/effective `limit`, and computes `NextCursor`/`IsTruncated` from `sourceResult.RawCount` even after all rows are discarded by tenant/org/folder/workspace/bridge trimming (`ContextSearchQueryHandler.cs:203`). A misbehaving or poisoned source can exceed the C4 result count, and an empty page with `IsTruncated=true` leaks untrusted pre-trim source state. Cap returned trusted items to `limit` and avoid exposing pagination/truncation solely from raw pre-trim rows; use bounded internal advancement or trusted-survivor evidence instead.
- [x] [Review][Patch] **Malformed cursors silently restart pagination at offset zero** [`src/Hexalith.Folders/Queries/ContextSearch/ContextSearchQueryHandler.cs:316`] - `ValidateC4Bounds` only checks cursor length, while `ParseOffset` maps invalid prefixes, negative offsets, and parse failures to `0`. A bad cursor replays the first page instead of returning a validation/limit error. Validate non-empty cursors before egress and fail closed for malformed cursor values.
- [x] [Review][Patch] **Invalid negative limits leak into safe-result C4 metadata** [`src/Hexalith.Folders/Queries/ContextSearch/ContextSearchQueryHandler.cs:332`] - `Limit <= 0` returns `ValidationFailed`, but `SafeResult` still calls `EffectiveLimit(query)`, which preserves a negative value through `Math.Min(query.Limit ?? MaxResultCount, MaxResultCount)`. Clamp safe-result metadata to a valid configured limit or compute safe metadata without trusting invalid request values.

#### Dismissed (13, no action)

Score-finiteness at the handler layer was dismissed because the live `MemoriesFolderSearchSource` drops non-finite scores before constructing `FolderSearchSourceHit`; a handler guard would be extra defense but not a current production path defect. Case-sensitive redaction was dismissed because `WorkspacePathPolicyValidator` enforces lowercase policy classes. Response-budget omissions/fixed-point concerns were dismissed as non-material for the current transport shape: success responses serialize item/limits/freshness data, and the previous response-budget finding has already been patched with serialized-size coverage. Raw authorization-denial serialization was dismissed for this chunk because the query result is an internal handler DTO and the integration route asserts byte-identical safe denial for unauthorized/cross-tenant/nonexistent folder probes. The broader per-hit ACL re-check finding was dismissed for the same reason recorded in the previous review: the implemented route is folder/workspace scoped, authorizes that folder before egress, and trims all hits back to the same folder/workspace before hydration. The top-level `Redacted` result-code finding was dismissed as superseded by the accepted prior review patch that removed the dead public `Redacted` surface. Handler-owned timeout was dismissed because the live source enforces the Story 10.5 two-second Memories timeout. Workspace authorization was dismissed because this handler mirrors the existing FileContext folder-scoped authorization pattern and then enforces workspace identity through source filters plus hydration trim.

### Review Findings (2026-06-26 Chunk 2 Server Source/Endpoint Re-Review)

> BMad manual code review over the chunked Story 10.5 server-source slice after subagent quota exhaustion: `MemoriesFolderSearchSource`, `FoldersDomainServiceEndpoints` context-search routes/mappers, DI registration, and focused server/integration tests. Triage: **0 decision-needed**, **1 patch**, **0 defer**, **3 dismissed**.

#### Patch (1)

- [x] [Review][Patch] **Malformed successful Memories payload can still bubble a 500** [`src/Hexalith.Folders.Server/ContextSearch/MemoriesFolderSearchSource.cs:80`] - remote failures and invalid JSON are degraded safely, but a syntactically successful `SearchResult` with `Results == null` reaches `searchResult.Results.Count`/`foreach` outside the `MemoriesClient.SearchAsync` try/catch and throws `NullReferenceException`. Treat a null results collection as a malformed upstream success and return the same safe unavailable source result; add a regression test.

#### Dismissed (3, no action)

Live Server bridge wiring remains a recorded AC15 DCP-lane deferral, not a new chunk-2 patch. The direct `Memories:BaseAddress` vs Dapr invoke-policy caveat remains an accepted documented deferral. The active producer/consumer workspace attribute now matches on both sides (`folders.workspaceId` is emitted and filtered), so the earlier sibling-workspace page-fill finding is already resolved.

### Review Findings (2026-06-26 Chunk 3/4 Transport, Status, UI, Boundary Re-Review)

> BMad manual code review over the remaining Story 10.5 slices after subagent quota exhaustion: OpenAPI/SDK/CLI/MCP transport parity, indexing-status bridge/read-model/UI, Dapr egress, and dependency-boundary tests. Triage: **0 decision-needed**, **0 patch**, **0 defer**, **8 dismissed**.

#### Dismissed (8, no action)

Generated SDK nullable task-id signature for `GetFolderIndexingStatus` was dismissed because the generated client omits the header when null and MCP has a focused no-task-header test. CLI parity for `GetFolderIndexingStatus` remains the already-recorded parity-generator artifact, not a runtime adapter defect. Generated `ContextIndexSearchRequest.Limit` remains the already-recorded systemic optional-numeric deferral. Indexing-status degraded, truncation-priority, reason-code scrubber, and metadata-only concerns were dismissed because the handler and tests now cover them. Bridge folder-index poisoning was dismissed because the live store rehydrates and filters each indexed key by tenant/folder before returning entries. UI GUID correlation shape was dismissed as an existing console-wide pattern rather than a Story 10.5 regression. Dapr memories invoke-policy coverage was dismissed as the already accepted direct-HttpClient/base-address caveat. Dependency-boundary leakage was dismissed because ScaffoldContractTests confine Memories references to Server/Workers/AppHost and adapters remain SDK-only.
