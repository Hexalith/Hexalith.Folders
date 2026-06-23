---
baseline_commit: c7a79ac75a7801d8733fd5c495902f53939f597b
---

# Story 9.3: Apply Folders→Memories routing config and synchronize artifacts

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a platform engineer,
I want `hexalith-folders → folders-index` routing configured on the `memories` resource and the architecture/context artifacts synchronized,
so that routing is in place (dormant until Epic 10) and the planning artifacts reflect the new topology.

## Context & Scope Boundary

This is the **third and final story of Epic 9** (AppHost Platform Alignment) — the **Phase-1 close-out**. Story 9.1 migrated the AppHost to the platform Aspire helpers (EventStore gateway-only + Tenants); Story 9.2 added the Memories search-index server to the same topology, hosted **standalone** with its routing **deliberately omitted** ("the canonical Tenants AppHost adds its `hexalith-tenants → tenants-index` routing inline — that is the 9.3 analog; omit it here"). **9.3 adds exactly that routing** and finishes the artifact synchronization.

The change is tiny in code and surgical in docs. The risk is **not** complexity — it is (a) wiring the routing as **testable production code** (not a circular test), (b) NOT re-doing doc edits that commit `cbf0db3` already applied, and (c) NOT over-reaching into Epic 10 scope (producer, invoke authorization, pub/sub topics).

- **In scope (9.3):**
  1. Set the source→index routing on the `memories` resource in `src/Hexalith.Folders.AppHost/Program.cs`: `EventStoreIntegration__Routing__SourceToTenantMap__hexalith-folders = folders-index` and `EventStoreIntegration__Routing__AutoProvisionRoutedTenants = true` (the canonical Tenants AppHost analog). Capture the `AddHexalithMemoriesSearchIndexServer(...)` result (currently a discard `_ =`) so the routing can be chained onto `memories.Server`.
  2. Make those two contract strings stable, testable constants on `FoldersAspireModule` (`MemoriesSourceId = "hexalith-folders"`, `MemoriesIndexTenant = "folders-index"`) and encapsulate the routing in a small **production helper** (`WithFoldersMemoriesSourceRouting`) that Program.cs and the test both call — so the test exercises real production wiring, not a re-implementation.
  3. A structural test (in `AspireTopologyTests.cs`) that asserts the composed `memories` resource carries both routing env vars, plus constant assertions pinning the two contract values.
  4. Doc-sync: add the routing line to `architecture.md` **AppHost Composition** and the **Epic-10-gating note** to the Memories integration track; **verify** (do not re-edit) that I-3 / I-4 / §756 and `project-context.md` already include `memories`.
  5. Author the **Memories search-index handoff doc** recording that end-to-end ingestion/search is gated on the Epic 10 producer.

- **Already done by `cbf0db3` — DO NOT re-apply (verify only):** `architecture.md` I-3 (`folders`/`folders-workers` may invoke `memories` + memories ingestion topic for Epic 10), I-4/§756 (app-ID list includes `memories`), the AppHost Composition bullet listing `memories` + `AddHexalithMemoriesSearchIndexServer`; `project-context.md` app-ID line (`memories` present, line 71) + topology rule (platform-helper composition incl. `AddHexalithMemoriesSearchIndexServer`, line 73). Re-editing these risks tripping doc-cluster conformance tests for zero benefit (see the Epic 7 doc-gate raw-`ShouldContain` trap).

- **Deferred to Epic 10 — DO NOT add in 9.3:** the worker-side producer; the `SearchIndexEntryChanged` CloudEvents emission; the `folders`/`folders-workers` → `memories` **invoke** authorization (production caller policies + their negative-test rows); any `memories`-topic pub/sub scope. The production deny-by-default policy artifacts (`deploy/dapr/production/*`) and `DaprPolicyConformanceTests` stay **unchanged** — routing env vars are a local AppHost concern and add **no** caller, topic, or invoke permission. The conformance suite's empty-scope / deny-by-default assertions for `memories` must stay green untouched.

## Acceptance Criteria

1. **Routing configured on the `memories` resource (the core change).** `src/Hexalith.Folders.AppHost/Program.cs` captures the `AddHexalithMemoriesSearchIndexServer(...)` result into a `HexalithMemoriesSearchIndexServerResources memories` variable (replacing the current `_ =` discard at `Program.cs:71`) and applies, on `memories.Server`, exactly two environment variables: `EventStoreIntegration__Routing__SourceToTenantMap__hexalith-folders = "folders-index"` and `EventStoreIntegration__Routing__AutoProvisionRoutedTenants = "true"`. This mirrors the canonical Tenants AppHost (`Hexalith.Tenants/src/Hexalith.Tenants.AppHost/Program.cs:121-126`), substituting the Folders source/tenant. No other behavior changes: `folders`/`folders-workers`/`folders-ui` keep identical app-ids, sidecars, references, `WaitFor` edges, and JWT wiring; `memories` is still **not** `.WithReference`/`.WaitFor`'d by them and is **not** JWT-wired (parity with Tenants); the gateway-only EventStore composition still produces **no** `eventstore-admin*` resources.

2. **Routing values are stable, testable constants + a production helper.** `FoldersAspireModule` gains `public const string MemoriesSourceId = "hexalith-folders";` and `public const string MemoriesIndexTenant = "folders-index";` (after `MemoriesAppId`). The two `.WithEnvironment(...)` calls are encapsulated in a single production helper — `public static IResourceBuilder<ProjectResource> WithFoldersMemoriesSourceRouting(this IResourceBuilder<ProjectResource> memoriesServer)` on `FoldersAspireModule` — that Program.cs invokes as `memories.Server.WithFoldersMemoriesSourceRouting()`. The helper builds the routing key from `MemoriesSourceId` (`$"EventStoreIntegration__Routing__SourceToTenantMap__{MemoriesSourceId}"`) and the value from `MemoriesIndexTenant`, validates its argument with `ArgumentNullException.ThrowIfNull`, and sets `EventStoreIntegration__Routing__AutoProvisionRoutedTenants = "true"`. (This keeps `Hexalith.Folders.Aspire` as the home for "Folders-specific helpers and stable app-ID/component-name constants" per project-context, and lets the test exercise the **same** code path Program.cs uses — non-circular coverage, matching the 9.2 "compose the helper, assert the result" model.)

3. **Structural test proves the routing is applied and pins the contract values.** In `tests/Hexalith.Folders.IntegrationTests/AspireTopologyTests.cs`: (a) extend the existing constants test (`FoldersAspireModuleShouldExposeStableDaprAppIdsAndComponentNames`) with `FoldersAspireModule.MemoriesSourceId.ShouldBe("hexalith-folders");` and `FoldersAspireModule.MemoriesIndexTenant.ShouldBe("folders-index");`; (b) add a **new** test (e.g. `MemoriesServerShouldCarryFoldersToFoldersIndexSourceRoutingWhenRoutingApplied`) that composes `AddHexalithMemoriesSearchIndexServer(...)` over the shared composition (reusing the `RepositoryPath(...)` helper for the checked-in `secretstore.memories.yaml`/`llm.memories.yaml` paths), calls `memories.Server.WithFoldersMemoriesSourceRouting()` (the **production** helper), resolves the resource's environment, and asserts both `EventStoreIntegration__Routing__SourceToTenantMap__hexalith-folders == "folders-index"` and `EventStoreIntegration__Routing__AutoProvisionRoutedTenants == "true"`. The closed-set `GatewayOnlyCompositionShouldNotProduceAnyEventStoreAdminResources` test and the 9.2 Memories-composition tests stay **unchanged and green**.

4. **`architecture.md` doc-sync (net-new only).** Two edits, both small: (a) in the **AppHost Composition** section (≈§396-401), add one bullet documenting that the `memories` resource is configured with `hexalith-folders → folders-index` source→index routing (`AutoProvisionRoutedTenants=true`), dormant until the Epic 10 producer emits `SearchIndexEntryChanged`; (b) in the **Memories integration track** (≈§130-156), add a sentence that the Epic 9 AppHost routing config ships in Phase 1 (dormant) and is activated end-to-end by the Epic 10 worker-side producer. **Verify** (and make no change to) I-3 (≈§561), I-4 (≈§562), and the §756 app-ID enumeration — all already include `memories`.

5. **`project-context.md` consistency verified — NOT re-edited.** Confirm `memories` is already in the stable Dapr app-ID line (line 71) and that the topology rule (line 73) already documents `AddHexalithMemoriesSearchIndexServer` platform-helper composition (both pre-applied by `cbf0db3`). No edit is required; record the verification. The AC's "app-ID lists include `memories`" internal-consistency check passes against the existing text.

6. **Memories search-index handoff doc authored.** A new doc at `_bmad-output/planning-artifacts/memories-search-index-handoff-2026-06-23.md` (mirroring the Tenants precedent `Hexalith.Tenants/_bmad-output/planning-artifacts/memories-search-index-handoff-2026-06-21.md` — same numbered-section + verification-checklist structure) records: what the Folders AppHost now ships (9.1 topology + 9.2 Memories hosting + 9.3 routing config); the exact env vars set; that the Memories server-side router already supports this contract (proven by the live `hexalith-tenants → tenants-index` Tenants integration); and that **end-to-end ingestion/search is gated on the Epic 10 producer** (which emits `SearchIndexEntryChanged` CloudEvents with source `hexalith-folders`). It uses LF line endings (`.editorconfig` pins `.md`? — see Note 7; markdown follows the repo default, verify the rule and match sibling docs).

7. **Builds and the narrowed suites pass; production policy untouched.** `dotnet build Hexalith.Folders.slnx` succeeds (0/0). `tests/Hexalith.Folders.IntegrationTests` is green incl. the updated constants test + the new routing test. `tests/Hexalith.Folders.Contracts.Tests` is green with **no change** to `DaprPolicyConformanceTests` / `AppHostPlatformCompositionConformanceTests` / `ContainerImageConformanceTests` and **no change** to `deploy/dapr/production/*` (routing adds no caller/topic/invoke permission — the `memories` deny-by-default posture is unchanged). `tests/Hexalith.Folders.UI.Tests` (incl. `AppHostBootSmokeTests`) stays green untouched. `aspire run` brings the topology up healthy with the `folders-index` tenant auto-provisioning in the background; if blocked by the **environment-wide Aspire CLI/DCP `--tls-cert-file` certificate-trust mismatch** documented in 9.1/9.2, record it and rely on build + structural tests — it is not a topology defect.

## 🚩 Critical Implementation Notes (read before writing any code)

1. **Why `AutoProvisionRoutedTenants=true` is mandatory, not optional.** The Memories router drops a `SearchIndexEntryChanged` event as `TenantNotFound` if its target index tenant (`folders-index`) is not yet `Active`. `AutoProvisionRoutedTenants=true` makes `RoutedTenantProvisioningStartupService` (a `BackgroundService` in the Memories server) provision `folders-index` at startup and wait for it to reach `Active`. It ALSO makes `EventStoreRoutingConfigValidator` **defer** its fail-fast "all routed tenants must exist" check (instead of failing boot) — so the `memories` server boots cleanly even though no producer exists yet. Setting only the `SourceToTenantMap` entry **without** `AutoProvisionRoutedTenants=true` would risk a fail-fast boot in non-Development environments. Set both. (Source: `Hexalith.Memories/.../EventStoreRoutingConfigValidator.cs`, `RoutedTenantProvisioningStartupService.cs`.)

2. **Capture the discard — `Program.cs:71` is currently `_ = builder.AddHexalithMemoriesSearchIndexServer(...)`.** 9.2 intentionally left the result unconsumed. 9.3 must change it to `HexalithMemoriesSearchIndexServerResources memories = builder.AddHexalithMemoriesSearchIndexServer(...)` and then chain the routing helper on `memories.Server`. The returned record is `HexalithMemoriesSearchIndexServerResources(IResourceBuilder<ProjectResource> Server, IResourceBuilder<ContainerResource> FalkorDb, IResourceBuilder<IDaprComponentResource> SecretStore, IResourceBuilder<IDaprComponentResource> Llm)` — `.Server` is the `ProjectResource` builder that supports `.WithEnvironment(...)`.

3. **Use `__` (double underscore), not `:`, in the env-var keys.** .NET config maps `EventStoreIntegration:Routing:SourceToTenantMap:hexalith-folders` (appsettings) to `EventStoreIntegration__Routing__SourceToTenantMap__hexalith-folders` (env var). Dictionary keys are appended after the section path. The source id `hexalith-folders` is the **dictionary key segment** (the CloudEvents source the Epic 10 producer will stamp); `folders-index` is the **value** (the curated index tenant). Do NOT colon-encode.

4. **Avoid the circular test.** A test that re-adds the routing inline and then asserts it proves nothing about Program.cs. Route both Program.cs **and** the test through the single `FoldersAspireModule.WithFoldersMemoriesSourceRouting(...)` production helper (AC2) so the test exercises real wiring — this is the repo's established pattern (the 9.2 tests compose the production `AddHexalithMemoriesSearchIndexServer` helper directly and assert its output; do the same here). The constant assertions (AC3a) are the non-circular contract lock and are the guaranteed-green floor.

5. **Resolving env vars in the test — there is no in-repo precedent; here is the idiom.** No existing Folders test reads environment variables off a composed resource. Primary approach: `IDictionary<string, string> env = await memories.Server.Resource.GetEnvironmentVariableValuesAsync(DistributedApplicationOperation.Publish);` (the public `Aspire.Hosting.ApplicationModel.ResourceExtensions` extension; `ProjectResource` implements `IResourceWithEnvironment`). Use **`Publish`** mode, not `Run`, so endpoint-backed `ReferenceExpression` env vars on the `memories` server resolve to manifest placeholders instead of throwing on an unallocated endpoint; literal routing strings resolve to their literal values regardless. Then `env.ShouldSatisfyAllConditions(...)` / `ShouldContainKeyAndValue(...)`. If that API is unavailable on the pinned Aspire build, fall back to invoking the resource's `EnvironmentCallbackAnnotation`s against an `EnvironmentCallbackContext` built with a `DistributedApplicationExecutionContext(DistributedApplicationOperation.Publish)` and reading `context.EnvironmentVariables`. Whichever idiom compiles, keep the constant assertions (AC3a) as the primary guarantee.

6. **Do NOT touch production policy / deploy artifacts.** Routing is a local AppHost env-var concern. It adds **no** invoke allow-rule, **no** pub/sub topic, **no** caller. `deploy/dapr/production/{accesscontrol.yaml,sidecar-config-bindings.yaml,pubsub.yaml}`, `tests/fixtures/dapr-policy-conformance.yaml`, and `DaprPolicyConformanceTests` must be **unchanged**. The `memories` deny-by-default posture (no callers, empty topic scopes) is correct and stays. Adding speculative `folders → memories` invoke rules here is Epic 10 scope and would break the closed-set conformance assertions.

7. **Do NOT re-edit `project-context.md` or the already-`memories`-bearing `architecture.md` sections.** `cbf0db3` pre-applied the app-ID + topology doc text. Re-editing risks the doc-cluster `ShouldContain` trap (line-wrapped prose phrases fail raw-substring assertions; reflow onto one line, never reword). Your only architecture.md edits are the two **net-new** additions in AC4 (routing line + Epic-10-gating sentence). When you write those, keep each asserted phrase on one physical line if any doc-conformance test references it.

8. **Handoff doc placement — `_bmad-output/planning-artifacts/`, not `docs/`.** Mirror the Tenants precedent location. `docs/operations/` and `docs/contract/` are covered by closed-set doc-conformance tests (`OperationsAuditDocsConformanceTests`, `GovernanceCompletenessGateTests`, `AdrRunbookDocsConformanceTests`, …) — dropping a new file there could trip an enumeration assertion for zero benefit. The handoff note is a planning/integration artifact; `_bmad-output/planning-artifacts/memories-search-index-handoff-2026-06-23.md` matches Tenants and is outside the conformance closed sets.

## Tasks / Subtasks

- [x] **Task 1 — Add the routing constants + production helper to `FoldersAspireModule` (AC: 2).**
  - [x] In `src/Hexalith.Folders.Aspire/FoldersAspireModule.cs`, after `public const string MemoriesAppId = "memories";`, add `public const string MemoriesSourceId = "hexalith-folders";` and `public const string MemoriesIndexTenant = "folders-index";`.
  - [x] Add `public static IResourceBuilder<ProjectResource> WithFoldersMemoriesSourceRouting(this IResourceBuilder<ProjectResource> memoriesServer)` that: `ArgumentNullException.ThrowIfNull(memoriesServer)`; returns `memoriesServer.WithEnvironment($"EventStoreIntegration__Routing__SourceToTenantMap__{MemoriesSourceId}", MemoriesIndexTenant).WithEnvironment("EventStoreIntegration__Routing__AutoProvisionRoutedTenants", "true")`. Add a doc-comment explaining the routing contract and the Epic-10-gating (cite the Tenants analog). Ensure the required Aspire `using` directives are present (the file/module already lives in `Hexalith.Folders.Aspire`; confirm `IResourceBuilder<ProjectResource>` + `WithEnvironment` resolve — check whether the routing helper belongs in `FoldersAspireModule` itself or a sibling extensions class matching the existing `AddHexalithFolders` location, and keep it next to the existing module extensions).
  - [x] First check whether a `hexalith-folders` CloudEvents-source constant already exists elsewhere (e.g. Contracts). If a canonical source constant exists, reference it instead of redefining the literal; otherwise the new `MemoriesSourceId` is the home for it.

- [x] **Task 2 — Apply the routing in the AppHost (AC: 1).** In `src/Hexalith.Folders.AppHost/Program.cs`: change `Program.cs:71` from `_ = builder.AddHexalithMemoriesSearchIndexServer(...)` to `HexalithMemoriesSearchIndexServerResources memories = builder.AddHexalithMemoriesSearchIndexServer(...)`; immediately after, add `_ = memories.Server.WithFoldersMemoriesSourceRouting();`. Update the 9.2 comment block (`Program.cs:61-67`) that says routing is "intentionally deferred"/"left unconsumed here" to reflect that 9.3 now applies it (cite the Tenants analog + Epic-10 gating). Do **not** JWT-wire `memories`, do **not** `.WithReference`/`.WaitFor` it from folders/workers/ui, do **not** add placement/scheduler args (Folders threads none — keep parity).

- [x] **Task 3 — Add the structural routing test (AC: 3).** In `tests/Hexalith.Folders.IntegrationTests/AspireTopologyTests.cs`:
  - [x] Add `FoldersAspireModule.MemoriesSourceId.ShouldBe("hexalith-folders");` and `FoldersAspireModule.MemoriesIndexTenant.ShouldBe("folders-index");` to `FoldersAspireModuleShouldExposeStableDaprAppIdsAndComponentNames`.
  - [x] Add a new `[Fact]` (e.g. `MemoriesServerShouldCarryFoldersToFoldersIndexSourceRoutingWhenRoutingApplied`): build a composition, compose `AddHexalithMemoriesSearchIndexServer(...)` (reusing the existing `RepositoryPath(...)` resolution of `src/Hexalith.Folders.AppHost/DaprComponents/{secretstore.memories.yaml,llm.memories.yaml}` as the 9.2 Memories tests do), call `memories.Server.WithFoldersMemoriesSourceRouting()`, resolve env via `GetEnvironmentVariableValuesAsync(DistributedApplicationOperation.Publish)` (or the annotation fallback — Note 5), and assert both env vars equal `folders-index` / `true`. Keep all 9.2 Memories tests + the closed gateway-only test untouched.
  - [x] No csproj change is needed — the test-only `Hexalith.Memories.Aspire` reference was added in 9.2.

- [x] **Task 4 — `architecture.md` net-new doc-sync (AC: 4).** Read `_bmad-output/planning-artifacts/architecture.md` AppHost Composition (≈§396-401) and Memories integration track (≈§130-156). Add (a) the routing bullet to AppHost Composition and (b) the Phase-1-dormant / Epic-10-activates sentence to the Memories integration track. **Verify** I-3 (≈§561), I-4 (≈§562), §756 already list `memories` and make **no** change to them. Keep any phrase a doc-conformance test might assert on one physical line.

- [x] **Task 5 — Verify `project-context.md` consistency, no edit (AC: 5).** Confirm line 71 (app-ID list includes `memories`) and line 73 (topology rule names `AddHexalithMemoriesSearchIndexServer`). Record the verification in the Dev Agent Record. Do not edit.

- [x] **Task 6 — Author the Memories search-index handoff doc (AC: 6).** Create `_bmad-output/planning-artifacts/memories-search-index-handoff-2026-06-23.md` modeled on `Hexalith.Tenants/_bmad-output/planning-artifacts/memories-search-index-handoff-2026-06-21.md`. Sections: What this enables; What Folders already ships (9.1/9.2/9.3 — no further Folders AppHost work for routing); The exact AppHost env vars; What Memories already implements server-side (router proven by the live `tenants-index` integration; same router handles `hexalith-folders → folders-index`); What activates it end-to-end (Epic 10 worker emits `SearchIndexEntryChanged`, source `hexalith-folders`); Verification (deferred to Epic 10 — a representative checklist); Status (routing config present 2026-06-23, dormant; end-to-end gated on Epic 10). Match sibling-doc line-ending/formatting conventions.

- [x] **Task 7 — Build + suites + boot verification (AC: 7).** `dotnet build Hexalith.Folders.slnx`; then `dotnet test tests/Hexalith.Folders.IntegrationTests`, `tests/Hexalith.Folders.Contracts.Tests`, `tests/Hexalith.Folders.UI.Tests`. Confirm Contracts/UI suites are green with **no** production-policy or deploy edits. Attempt `aspire run`; confirm the topology composes with `memories` + routing and the `folders-index` tenant provisions in the background. If the env-wide DCP/TLS blocker prevents live boot, document it (per 9.1/9.2) and rely on build + structural tests. **AppHost changes require an Aspire restart before wiring can be trusted** (project-context).

## Dev Notes

### The exact change in Program.cs (current state — `src/Hexalith.Folders.AppHost/Program.cs:61-76`)

Post-9.2, the Memories composition is a discard:

```csharp
// Memories search-index server (Story 9.2): hosted standalone ... Source->index routing (9.3) ...
string memoriesSecretStorePath = ResolveDaprConfigPath(builder.AppHostDirectory, "secretstore.memories.yaml");
string memoriesLlmConfigPath = ResolveDaprConfigPath(builder.AppHostDirectory, "llm.memories.yaml");

_ = builder.AddHexalithMemoriesSearchIndexServer(
    eventStoreResources.StateStore,
    eventStoreResources.PubSub,
    memoriesSecretStorePath,
    memoriesLlmConfigPath,
    serverName: FoldersAspireModule.MemoriesAppId);
```

9.3 turns it into (capture + routing via the production helper):

```csharp
HexalithMemoriesSearchIndexServerResources memories = builder.AddHexalithMemoriesSearchIndexServer(
    eventStoreResources.StateStore,
    eventStoreResources.PubSub,
    memoriesSecretStorePath,
    memoriesLlmConfigPath,
    serverName: FoldersAspireModule.MemoriesAppId);

// Story 9.3: route the Folders producer's CloudEvents (source "hexalith-folders", emitted by the Epic 10
// worker) into the curated folders-index partition, auto-provisioning that index tenant at startup so it is
// Active before the first event arrives. Dormant until the Epic 10 producer ships. Tenants AppHost analog.
_ = memories.Server.WithFoldersMemoriesSourceRouting();
```

### Canonical reference — Tenants AppHost routing chain (`Hexalith.Tenants/src/Hexalith.Tenants.AppHost/Program.cs:113-126`)

```csharp
HexalithMemoriesSearchIndexServerResources memories = builder.AddHexalithMemoriesSearchIndexServer(
    eventStoreResources.StateStore, eventStoreResources.PubSub,
    memoriesSecretStorePath, memoriesLlmConfigPath, serverName: "memories",
    daprPlacementHostAddress: daprPlacementHostAddress, daprSchedulerHostAddress: daprSchedulerHostAddress);
IResourceBuilder<ProjectResource> memoriesService = memories.Server
    .WithEnvironment("EventStoreIntegration__Routing__SourceToTenantMap__hexalith-tenants", "tenants-index")
    .WithEnvironment("EventStoreIntegration__Routing__AutoProvisionRoutedTenants", "true");
```

Folders substitutes `hexalith-folders → folders-index` and **omits** the placement/scheduler args (Folders threads none for any service — keep parity; do not introduce them for `memories` alone). Folders encapsulates the two `.WithEnvironment` calls in `WithFoldersMemoriesSourceRouting()` for testability; Tenants inlines them — either is correct, but the helper is what makes the AC3 test non-circular here.

### How the env vars are consumed (Memories server side — for understanding, not editing)

- Bound by `services.AddOptions<TenantEventRoutingOptions>().Bind(configuration.GetSection("EventStoreIntegration:Routing")).ValidateOnStart()` (`Hexalith.Memories/.../EventStoreIntegrationServiceCollectionExtensions.cs`).
- `TenantEventRoutingOptions.SourceToTenantMap` (`Dictionary<string,string>`, longest-prefix, case-insensitive) maps the CloudEvents `source` → index tenant; `AutoProvisionRoutedTenants` (`bool`) gates background provisioning.
- `EventStoreRoutingConfigValidator`: defers the fail-fast "routed tenants must exist" check when `AutoProvisionRoutedTenants` is true (or Development) → clean boot pre-producer.
- `RoutedTenantProvisioningStartupService` (`BackgroundService`): when `AutoProvisionRoutedTenants` is true, provisions each distinct `SourceToTenantMap` value and waits for `TenantStatus.Active`.
- Net effect for 9.3: setting both env vars makes `memories` boot healthy AND ready to accept `hexalith-folders`-sourced events into `folders-index` the moment the Epic 10 producer exists. Nothing flows until then.

### The helper return type (`Hexalith.Memories/src/Hexalith.Memories.Aspire/HexalithMemoriesServerExtensions.cs`)

```csharp
public sealed record HexalithMemoriesSearchIndexServerResources(
    IResourceBuilder<ProjectResource> Server,
    IResourceBuilder<ContainerResource> FalkorDb,
    IResourceBuilder<IDaprComponentResource> SecretStore,
    IResourceBuilder<IDaprComponentResource> Llm);
```

`.Server` is the routing target. The helper signature (unchanged from 9.2): `AddHexalithMemoriesSearchIndexServer(this IDistributedApplicationBuilder builder, IResourceBuilder<IDaprComponentResource> stateStore, IResourceBuilder<IDaprComponentResource> pubSub, string secretStoreComponentPath, string llmComponentPath, string? redisConnectionString = null, string eventStoreTopic = "memories-events", string serverName = "memories", int daprHttpPort = 3502, int daprGrpcPort = 50002, string? daprPlacementHostAddress = null, string? daprSchedulerHostAddress = null)`.

### Doc-sync precision (architecture.md — what is already there vs. what 9.3 adds)

Already present (verify, **do not** re-edit): AppHost Composition (≈§396-401) lists `memories` + `AddHexalithMemoriesSearchIndexServer`; I-3 (≈§561) has `folders`/`folders-workers` may invoke `memories` + "Memories ingestion topic for Epic 10"; I-4 (≈§562) and §756 list `eventstore, tenants, memories, folders, folders-ui, folders-workers`. 9.3 adds only: (a) one AppHost-Composition bullet for the `hexalith-folders → folders-index` routing (`AutoProvisionRoutedTenants=true`, dormant until Epic 10); (b) one Memories-integration-track sentence on the Phase-1-ships-routing / Epic-10-activates phasing.

### Known artifact drift — Aspire version (OUT of 9.3 scope; documented, not chased)

`architecture.md` (≈§418, §559, §1486) still says Aspire `13.3.0`; `epics.md` AR-INFRA-01 and `project-context.md` say `13.4.3`; `Directory.Packages.props` (authoritative, aligned by 9.1) pins a `13.4.x` line. The sprint-change-proposal listed a §1486 version-note reconciliation as Phase-1 doc-sync, but it is **not** in this story's epics AC and is a three-way drift better handled as a focused follow-up against the authoritative `Directory.Packages.props`. **Do not** fold speculative version edits into 9.3 — it is unrelated to routing and risks misalignment. Flag it; leave it.

### Previous Story Intelligence (9.1 → 9.2)

- 9.1 established the platform-helper composition, the checked-in `statestore.yaml`/`pubsub.yaml`/`resiliency.yaml`, gateway-only EventStore, and the closed-set conformance guardrails. 9.2 hosted `memories` standalone and **deferred routing to 9.3 by name** (9.2 Critical Note 6 / Scope Boundary). 9.3 is the final, smallest Epic-9 increment.
- 9.2 added the `MemoriesAppId` constant, the Memories Dapr component files, the shared-component re-scoping, the deny-by-default production registration, and the test-only `Hexalith.Memories.Aspire` ref in the IntegrationTests csproj — **all already in place**; 9.3 needs none of them again.
- Both 9.1 and 9.2 hit the **environment-wide Aspire CLI / DCP `--tls-cert-file` certificate-trust blocker** on `aspire run` (reproduces for any Aspire app here; not a topology bug). Expect the same; prove composition by build + structural tests, defer live-boot sign-off to a DCP-capable env/CI.
- 9.2's production-policy conformance suite is strict closed-set; 9.3 deliberately makes **zero** change there (routing ≠ policy). Resist any urge to "register" the routing in the deny-by-default artifacts.
- `cbf0db3` pre-applied the `memories` app-ID + topology doc text in architecture.md/project-context.md (docs/planning only, no `src`). 9.3 owns only the **routing** doc text + the handoff doc.

### Git Intelligence (recent topology-relevant work)

- `c7a79ac` (current HEAD) — ScaffoldContractTests reference updates (submodule pointer hygiene); not topology.
- `32c2b94 feat(story-9.2): Add Memories search-index server to AppHost topology` — the immediate functional predecessor; 9.3 extends its `Program.cs` Memories block.
- `8ffaa13 feat(story-9.1): Adopt EventStore and Tenants platform Aspire helpers` — the platform-helper foundation.
- `cbf0db3 ...integrate Memories search-index` — docs/planning only; the `memories` app-ID + topology doc text already in place.

### Guardrails the change must not break

- **Behavior-preserving for folders/workers/ui** — sidecars, references, `WaitFor`, JWT/Keycloak untouched.
- **App-ID stability** — `eventstore, tenants, memories, folders, folders-workers, folders-ui` unchanged; routing adds env vars, not identities.
- **Gateway-only invariant** — still no `eventstore-admin*` resources; the closed gateway-only test stays green untouched.
- **Deny-by-default `memories`** — no caller, no topic, no invoke rule added (Epic 10). Production deploy + `DaprPolicyConformanceTests` unchanged.
- **System working end-to-end** — `aspire run` stays healthy; `folders-index` auto-provisions in the background; routing is dormant (no producer) so no existing flow changes.
- **Submodule policy** — root-level only; no submodule code change (this is a Folders-only story; `Hexalith.Memories` server already supports the routing contract).

### Project Structure Notes

Files 9.3 touches (all in `Hexalith.Folders`; **no** submodule changes):
- `src/Hexalith.Folders.Aspire/FoldersAspireModule.cs` — add `MemoriesSourceId` + `MemoriesIndexTenant` constants + `WithFoldersMemoriesSourceRouting` helper.
- `src/Hexalith.Folders.AppHost/Program.cs` — capture the Memories result + apply routing; refresh the 9.2 deferral comment.
- `tests/Hexalith.Folders.IntegrationTests/AspireTopologyTests.cs` — constant assertions + new routing test (no csproj change).
- `_bmad-output/planning-artifacts/architecture.md` — AppHost-Composition routing bullet + Memories-track Epic-10-gating sentence (2 small edits).
- `_bmad-output/planning-artifacts/memories-search-index-handoff-2026-06-23.md` — new handoff doc.
- `project-context.md` — **verify only, no edit**.

`Hexalith.Folders.slnx` already includes the AppHost + Aspire + IntegrationTests projects; no solution change.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 9.3 (lines 1863-1874); Epic 9 (1831-1835); Story 9.2 (1850-1861); Epic 10 Story 10.4 routing-activation (1900-1904); AR-INFRA-01/02 (328-329)]
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-06-22-apphost-memories-platform-alignment.md#§4.1 (9-3 bullet, lines 145-149), §4.2 (I-3/I-4/§756/version-note), §4.3 (project-context), §5 success criteria (251-255)]
- [Source: _bmad-output/planning-artifacts/architecture.md#AppHost Composition (≈§396-401); I-3 (≈§561); I-4 (≈§562); §756 app-ID list; Memories integration track (≈§130-156); Aspire-version refs (≈§418, §559, §1486)]
- [Source: _bmad-output/implementation-artifacts/9-2-add-memories-search-index-server-to-the-apphost-topology.md (predecessor; routing deferral by name; helper return type; conformance closed-set warnings; DCP/TLS boot blocker)]
- [Source: src/Hexalith.Folders.AppHost/Program.cs:61-76 (Memories composition discard to capture), :8-12 (ResolveDaprConfigPath), :78-90 (Keycloak block — do not touch)]
- [Source: src/Hexalith.Folders.Aspire/FoldersAspireModule.cs (MemoriesAppId constant + AddHexalithFolders extension location for the new routing helper)]
- [Source: Hexalith.Tenants/src/Hexalith.Tenants.AppHost/Program.cs:113-126 (canonical routing chain to mirror); Hexalith.Tenants/_bmad-output/planning-artifacts/memories-search-index-handoff-2026-06-21.md (handoff-doc structure to mirror)]
- [Source: Hexalith.Memories/src/Hexalith.Memories.Aspire/HexalithMemoriesServerExtensions.cs:17-21 (return record), :70-166 (helper signature); Hexalith.Memories/src/Hexalith.Memories.EventStore/{TenantEventRoutingOptions.cs, EventStoreIntegrationServiceCollectionExtensions.cs:44-46}; Hexalith.Memories/src/Hexalith.Memories.Server/EventStoreIntegration/{EventStoreRoutingConfigValidator.cs, RoutedTenantProvisioningStartupService.cs} (routing semantics)]
- [Source: tests/Hexalith.Folders.IntegrationTests/AspireTopologyTests.cs (constants test; 9.2 Memories-composition tests; BuildGatewayOnlyComposition + RepositoryPath helpers; closed gateway-only test to leave unchanged)]
- [Source: _bmad-output/project-context.md (app-ID line 71; platform-helper topology rule line 73; LF/CRLF rule line 105; Aspire-restart rule line 130; "authoritative repo config when planning drifts" line 36)]

## Dev Agent Record

### Agent Model Used

claude-opus-4-8[1m] (Opus 4.8, 1M context) — BMAD dev-story workflow.

### Debug Log References

- `dotnet build Hexalith.Folders.slnx` → Build succeeded, **0 Warning(s) / 0 Error(s)**.
- `dotnet test tests/Hexalith.Folders.IntegrationTests` → **Passed 610 / Failed 0 / Skipped 0** (incl. the extended `FoldersAspireModuleShouldExposeStableDaprAppIdsAndComponentNames` constants test, the `MemoriesServerShouldCarryFoldersToFoldersIndexSourceRoutingWhenRoutingApplied` routing test — extended with a `routedMemories.ShouldBeSameAs(memories.Server)` return-contract assertion — and the new error-case `WithFoldersMemoriesSourceRoutingShouldThrowArgumentNullExceptionWhenServerIsNull` test added by the QA automation pass; all 9.2 Memories-composition tests + the closed `GatewayOnlyCompositionShouldNotProduceAnyEventStoreAdminResources` test stayed green untouched).
- `dotnet test tests/Hexalith.Folders.Contracts.Tests` → **Passed 274 / Failed 0** with **no** change to `DaprPolicyConformanceTests` / `AppHostPlatformCompositionConformanceTests` / `ContainerImageConformanceTests` and **no** change to `deploy/dapr/production/*` or `tests/fixtures/dapr-policy-conformance.yaml`.
- `dotnet test tests/Hexalith.Folders.UI.Tests` → **Passed 521 / Failed 0** (incl. `AppHostBootSmokeTests`), untouched.
- **Env-var resolution idiom note:** the primary `IResourceWithEnvironment.GetEnvironmentVariableValuesAsync(DistributedApplicationOperation.Publish)` API is `[Obsolete]` on the pinned Aspire 13.4.6 build ("Use ExecutionConfigurationBuilder instead") and the repo treats warnings as errors, so the test uses **Note 5's documented fallback**: invoke the resource's `EnvironmentCallbackAnnotation`s against an `EnvironmentCallbackContext` built with a `DistributedApplicationExecutionContext(DistributedApplicationOperation.Publish)` and read `context.EnvironmentVariables`. The constant assertions (AC3a) remain the guaranteed-green contract floor.
- `aspire run --project src/Hexalith.Folders.AppHost` → AppHost **built successfully**, then DCP/dashboard connection failed with `⚠️ Developer certificates may not be fully trusted (trust exit code was: PartiallyFailedToTrustTheCertificate)` and Polly retry exhaustion — the **environment-wide Aspire CLI/DCP `--tls-cert-file` certificate-trust blocker documented in Stories 9.1/9.2**, not a topology defect. Composition is proven by build + the structural topology tests; live-boot sign-off deferred to a DCP-capable env/CI. Transient `aspire.config.json` removed after the bounded run.

### Completion Notes List

- **AC1 (routing on `memories`) — met.** `Program.cs` now captures the Memories composition into a `HexalithMemoriesSearchIndexServerResources memories` variable (replacing the `_ =` discard) and applies `_ = memories.Server.WithFoldersMemoriesSourceRouting();`, setting exactly `EventStoreIntegration__Routing__SourceToTenantMap__hexalith-folders = "folders-index"` and `EventStoreIntegration__Routing__AutoProvisionRoutedTenants = "true"`. The 9.2 deferral comment was refreshed to reflect that 9.3 applies the routing (Tenants analog + Epic-10 gating). No JWT wiring, no `.WithReference`/`.WaitFor` from folders/workers/ui, no placement/scheduler args — `folders`/`folders-workers`/`folders-ui` keep identical app-ids/sidecars/refs/JWT wiring; the gateway-only EventStore composition still yields no `eventstore-admin*` resources (asserted by the untouched closed-set test).
- **AC2 (constants + production helper) — met.** `FoldersAspireModule` gained `MemoriesSourceId = "hexalith-folders"` and `MemoriesIndexTenant = "folders-index"` (after `MemoriesAppId`) and the production extension `WithFoldersMemoriesSourceRouting` (argument-null-validated; key built from `MemoriesSourceId`, value from `MemoriesIndexTenant`) with a doc-comment citing the Tenants analog and Epic-10 gating. Program.cs and the test both call this single helper (non-circular coverage). No pre-existing `hexalith-folders` CloudEvents-source constant exists in the repo, so `MemoriesSourceId` is its canonical home.
- **AC3 (structural test) — met.** Extended the constants test with the two new contract-value assertions and added `MemoriesServerShouldCarryFoldersToFoldersIndexSourceRoutingWhenRoutingApplied`, which composes `AddHexalithMemoriesSearchIndexServer(...)` (reusing the `RepositoryPath(...)` helper), drives the production `WithFoldersMemoriesSourceRouting()`, resolves env via the annotation fallback, and asserts both env vars equal `folders-index` / `true`. No csproj change. A subsequent QA automation pass (`_bmad-output/implementation-artifacts/tests/9-3-test-summary.md`) hardened the helper's public surface with two tests-only additions: a `routedMemories.ShouldBeSameAs(memories.Server)` return-contract assertion on the routing test, and a new error-case `[Fact]` `WithFoldersMemoriesSourceRoutingShouldThrowArgumentNullExceptionWhenServerIsNull` covering the `ArgumentNullException.ThrowIfNull` guard (`ParamName == "memoriesServer"`). No production code changed; suite total rose 609 → 610.
- **AC4 (`architecture.md` net-new) — met.** Added (a) the AppHost-Composition routing bullet (`hexalith-folders → folders-index`, `AutoProvisionRoutedTenants=true`, dormant until Epic 10) and (b) the Memories-integration-track Phase-1-ships / Epic-10-activates sentence. **Verified** I-3 (line 561), I-4 (line 562), and the §756 app-ID enumeration (line 756) already include `memories` — left unchanged. Each new asserted phrase kept on one physical line; additions mint no new `[ADIS]-\d+|C\d+` decision IDs (AdrRunbook/ExitCriteria conformance presence-checks stay satisfied).
- **AC5 (`project-context.md` verified, not edited) — met.** Confirmed line 71 stable Dapr app-ID list already includes `memories` and line 73 topology rule already names `AddHexalithMemoriesSearchIndexServer` platform-helper composition (both pre-applied by `cbf0db3`). No edit made.
- **AC6 (handoff doc) — met.** Authored `_bmad-output/planning-artifacts/memories-search-index-handoff-2026-06-23.md` (LF line endings, matching all sibling `_bmad-output` markdown docs) mirroring the Tenants precedent's numbered-section + verification-checklist structure: what it enables; what Folders ships (9.1/9.2/9.3); the exact env vars; what Memories already implements server-side (router proven by the live `tenants-index` integration); what activates it end-to-end (Epic 10 producer); a deferred verification checklist; and status (routing present 2026-06-23, dormant; end-to-end gated on Epic 10).
- **AC7 (build + suites + boot) — met (boot blocked by env, per 9.1/9.2).** Full solution builds 0/0; the three narrowed suites are green with zero production-policy/deploy edits; `aspire run` reproduces the documented DCP/TLS cert-trust blocker (AppHost builds, DCP refuses) — recorded, not a topology defect.
- **Out-of-scope drift left untouched (per Dev Notes):** the three-way Aspire-version note (`architecture.md` §418/§559/§1486 says 13.3.0 vs `Directory.Packages.props` 13.4.6) was flagged but **not** chased — unrelated to routing; better handled as a focused follow-up against the authoritative `Directory.Packages.props`.

### Change Log

- 2026-06-23 — Story 9.3 implemented (Opus 4.8). Added `MemoriesSourceId`/`MemoriesIndexTenant` constants + `WithFoldersMemoriesSourceRouting` production helper to `FoldersAspireModule`; captured the Memories composition in `Program.cs` and applied the `hexalith-folders → folders-index` source→index routing (`AutoProvisionRoutedTenants=true`, dormant until Epic 10); added the structural routing test + constant assertions in `AspireTopologyTests`; doc-synced two net-new `architecture.md` lines; authored the Memories search-index handoff doc. Build 0/0; IntegrationTests 609/0, Contracts.Tests 274/0, UI.Tests 521/0; production policy + deploy artifacts unchanged. Status → review.
- 2026-06-23 — QA automation pass (`9-3-test-summary.md`) added two tests-only hardening cases to `AspireTopologyTests` (return-contract `ShouldBeSameAs` assertion + `WithFoldersMemoriesSourceRoutingShouldThrowArgumentNullExceptionWhenServerIsNull` null-guard `[Fact]`); IntegrationTests 609 → 610/0. No production code change.
- 2026-06-23 — Automated review (Opus 4.8). Re-verified build 0/0 and the three narrowed suites (IntegrationTests 610/0, Contracts.Tests 274/0, UI.Tests 521/0) with production policy + deploy artifacts unchanged; all 7 ACs confirmed met. Synced the Dev Agent Record / File List to the QA pass's second test + corrected the 609 → 610 count. 0 Critical findings → Status → done.

### File List

- `src/Hexalith.Folders.Aspire/FoldersAspireModule.cs` (modified — `MemoriesSourceId` + `MemoriesIndexTenant` constants + `WithFoldersMemoriesSourceRouting` helper)
- `src/Hexalith.Folders.AppHost/Program.cs` (modified — capture Memories result + apply routing; refreshed 9.2 deferral comment)
- `tests/Hexalith.Folders.IntegrationTests/AspireTopologyTests.cs` (modified — constant assertions + new routing test `MemoriesServerShouldCarryFoldersToFoldersIndexSourceRoutingWhenRoutingApplied` incl. its `ShouldBeSameAs` return-contract assertion + the QA-pass null-guard `[Fact]` `WithFoldersMemoriesSourceRoutingShouldThrowArgumentNullExceptionWhenServerIsNull`)
- `_bmad-output/implementation-artifacts/tests/9-3-test-summary.md` (new — QA automation summary documenting the two tests-only hardening additions)
- `_bmad-output/planning-artifacts/architecture.md` (modified — AppHost-Composition routing bullet + Memories-track Epic-10-gating sentence)
- `_bmad-output/planning-artifacts/memories-search-index-handoff-2026-06-23.md` (new — handoff doc)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified — 9-3 status ready-for-dev → in-progress → review)
- `_bmad-output/implementation-artifacts/9-3-apply-folders-to-memories-routing-config-and-sync-artifacts.md` (modified — checkboxes, Dev Agent Record, Status)
