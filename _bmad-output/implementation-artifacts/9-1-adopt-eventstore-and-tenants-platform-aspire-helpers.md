---
baseline_commit: 87163d461adf4e892dc7f81d1c375e6bbe2351d8
---

# Story 9.1: Adopt the EventStore and Tenants platform Aspire helpers

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a platform engineer,
I want the Folders AppHost to compose EventStore and Tenants via the shared platform Aspire helpers,
so that Folders stops re-implementing shared Dapr topology and matches the canonical Tenants AppHost.

## Context & Scope Boundary

This is the **first story of Epic 9** (AppHost Platform Alignment). It is a **behavior-preserving infrastructure refactor** — it changes *how* the local Aspire topology is composed, not *what* it does. No product FR scope.

- **In scope (9.1):** Replace the hand-rolled `FoldersAspireModule` EventStore/Tenants + shared-Dapr wiring with the platform helpers `AddHexalithEventStore(...)` (gateway-only) + `AddHexalithTenantsServer(...)`; switch the AppHost csproj to the `.Aspire` helper references; add checked-in `statestore.yaml` / `pubsub.yaml` / `resiliency.yaml` Dapr component files; keep `folders` / `folders-workers` / `folders-ui` app-IDs, sidecars, and wiring **identical**; update the Aspire/AppHost tests.
- **Deferred to 9.2:** Adding the Memories search-index server (`memories` app-ID, `memories-vectors`/`memories-graphs` containers, `secretstore.memories.yaml` + `llm.memories.yaml`, `HexalithMemoriesRoot`). **Do NOT add `memories` in 9.1.**
- **Deferred to 9.3:** `hexalith-folders → folders-index` routing config; architecture.md / project-context.md doc edits (note: those doc edits were **already pre-applied** by commit `cbf0db3` — leave them).

## Acceptance Criteria

1. **Gateway-only EventStore composition.** The AppHost composes EventStore through `AddHexalithEventStore(eventStore, adminServer: null, …)` (command **gateway-only**) and Tenants through `AddHexalithTenantsServer(eventStoreResources, …)`. The running topology contains **no** `eventstore-admin` or `eventstore-admin-ui` resource (today's topology has neither — this must be preserved).
2. **AppHost csproj references the `.Aspire` helpers, not the runtime projects.** `Hexalith.Folders.AppHost.csproj` references `Hexalith.EventStore.Aspire` and `Hexalith.Tenants.Aspire` with `IsAspireProjectResource="false"`, and the two direct EventStore/Tenants **runtime** project references (`$(HexalithEventStoreRoot)\src\Hexalith.EventStore\Hexalith.EventStore.csproj` and `$(HexalithTenantsRoot)\src\Hexalith.Tenants\Hexalith.Tenants.csproj`) are removed. The `Projects.Hexalith_EventStore` / `Projects.Hexalith_Tenants` generated metadata types are no longer used in `Program.cs`.
3. **Checked-in Dapr component YAMLs.** `statestore.yaml`, `pubsub.yaml`, and `resiliency.yaml` exist under `src/Hexalith.Folders.AppHost/DaprComponents/`, are **scoped to the Folders app-IDs** (`eventstore`, `tenants`, `folders`, `folders-workers`, `folders-ui` — **not** `eventstore-admin`, **not** `memories`), preserve today's state-store semantics (`type: state.redis`, `actorStateStore: "true"`, `redisHost: localhost:6379`, `keyPrefix: none`), and are consumed by the platform helper via its `stateStoreComponentPath` / `pubSubComponentPath` / `resiliencyConfigPath` parameters. No in-code Dapr component creation (`AddDaprComponent` / `AddDaprPubSub`) remains in `FoldersAspireModule`.
4. **Folders services unchanged; no hand-rolled platform wiring remains.** `folders`, `folders-workers`, and `folders-ui` keep identical app-IDs, Dapr sidecars, project references (`folders`/`folders-workers` reference `eventstore` + `tenants` and `WaitFor` them; `folders-ui` references `folders`), and JWT/Keycloak environment wiring. The hand-rolled EventStore/Tenants sidecar wiring and shared-component creation are gone from `FoldersAspireModule`. The seven stable constants in `FoldersAspireModule` (`EventStoreAppId`, `TenantsAppId`, `FoldersAppId`, `FoldersWorkersAppId`, `FoldersUiAppId`, `StateStoreComponentName`, `PubSubComponentName`) are **retained** (still asserted by tests).
5. **Tests updated and green.** `AspireTopologyTests` (all four methods) and `AppHostBootSmokeTests` pass. The `HexalithFoldersResources` shape contract (seven properties: `StateStore`, `PubSub`, `EventStore`, `Tenants`, `Folders`, `FoldersWorkers`, `FoldersUi`) is preserved, or — if the topology surface legitimately changes — its structure test is updated in lockstep. `DaprPolicyConformanceTests` and `ContainerImageConformanceTests` stay green (app-IDs unchanged in 9.1).
6. **Builds and boots.** `dotnet build Hexalith.Folders.slnx` succeeds and `aspire run` brings the topology up healthy (`eventstore`, `tenants`, `folders`, `folders-workers`, `folders-ui` reach a running/healthy state), with no `eventstore-admin*` resources present.

## 🚩 CRITICAL Implementation Decision (read before writing any code)

**The platform helper does NOT yet support `adminServer: null`.** The epic AC text shows `AddHexalithEventStore(eventStore, adminServer: null, …)`, but the helper as it exists today will throw at runtime. Verified evidence:

- `Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs:141` — `ArgumentNullException.ThrowIfNull(adminServer);`
- Both public overloads (`:68` and `:126`) declare `IResourceBuilder<ProjectResource> adminServer` (**non-nullable**, required positional) and unconditionally dereference it (`:221` `adminServer.WithReference(eventStore)`, `:245` `adminServer.WithDaprSidecar(...)`, `:287` `new HexalithEventStoreResources(..., adminServer, adminUI)`).
- `HexalithEventStoreResources.cs:20` — the record's `AdminServer` member is **non-nullable** (`AdminUI` at `:21` is already `?`-nullable).
- The project-metadata classes (`EventStoreProjectMetadata`, `EventStoreAdminServerHostProjectMetadata`, `EventStoreAdminUIProjectMetadata`) are `internal sealed` — **not** callable from the Folders AppHost. `AddHexalithEventStorePlatformProjects()` (`HexalithEventStorePlatformExtensions.cs:57`) always returns **all three** (EventStore + AdminServer + AdminUI), so it cannot give you an admin-less `eventStore` project.

**The good news (de-risks the fix):** `AddHexalithTenantsServer` → `AddEventStoreDomainModule` reads **only** `eventStore.StateStore` and `eventStore.PubSub` (`HexalithEventStoreDomainModuleExtensions.cs:89-90`); it never touches `AdminServer`. So Tenants composition is unaffected by a null admin server.

**Therefore you must FIRST resolve how to obtain a gateway-only `eventstore` project + an admin-less `HexalithEventStoreResources`. Pick one and record the decision in the Dev Agent Record:**

- **Option A — RECOMMENDED: surgical gateway-only support in `Hexalith.EventStore.Aspire` (platform-aligned).** In the EventStore submodule, (a) add a **public** way to add just the `eventstore` command-server project (e.g., an overload `AddHexalithEventStorePlatformProjects(includeAdmin: false)` or a new `AddHexalithEventStoreGatewayProject()` returning `IResourceBuilder<ProjectResource>`), and (b) make `AddHexalithEventStore`'s `adminServer`/`adminUI` parameters nullable and guard all admin/UI wiring (`:221`, `:238-265`, `:273-277`, `:287`) behind `if (adminServer is not null)`, making the record's `AdminServer` nullable. This is small, backward-compatible (existing non-null callers unchanged), benefits all consumers, and is the literal intent of the epic AC. **Cost/risk:** it modifies the `Hexalith.EventStore` **git submodule** (separate repo) — you must commit there, keep that repo's tests green, and bump the submodule pointer in this repo. **Confirm this cross-submodule change is authorized** (see Open Questions) before doing it; per `CLAUDE.md` do not initialize nested submodules.
- **Option B — fallback if cross-submodule edits are out of scope: Folders-local gateway adaptation reusing the platform component YAMLs.** Keep a thin Folders-scoped helper that adds the `eventstore` command-server project and wires only its sidecar + the shared `statestore`/`pubsub`/`resiliency` components **sourced from the new checked-in YAML files** (not created in code), then call `AddHexalithTenantsServer(...)`. This satisfies "no hand-rolled component creation; components come from checked-in YAML" but **partially contradicts** the epic's "compose purely through platform helpers" goal — flag it explicitly if chosen. Note you still need a way to add the `eventstore` project without the runtime ref (AC2); reconcile AC2 vs. the `internal` metadata classes here.

Do **not** silently pass `adminServer: null` to the current helper — it will `ArgumentNullException` on boot.

## Tasks / Subtasks

- [x] **Task 0 — Resolve the gateway-only approach (AC: 1).** Read `HexalithEventStoreExtensions.cs`, `HexalithEventStorePlatformExtensions.cs`, `HexalithEventStoreResources.cs`, `HexalithEventStoreDomainModuleExtensions.cs`, and `HexalithTenantsServerExtensions.cs` in full. Decide Option A vs. B (above), confirm authorization for any cross-submodule change, and record the decision + rationale in the Dev Agent Record before editing Folders code.
  - [x] If Option A: implement gateway-only support in `Hexalith.EventStore.Aspire`; run that repo's Aspire/topology tests; commit in the submodule; bump the submodule pointer.
- [x] **Task 1 — Rewrite AppHost resource composition (AC: 1, 4).** In `src/Hexalith.Folders.AppHost/Program.cs`, replace the five `builder.AddProject<Projects.Hexalith_*>(…)` calls + `builder.AddHexalithFolders(…)` with: obtain the gateway `eventStore` project → `HexalithEventStoreResources eventStoreResources = builder.AddHexalithEventStore(eventStore, adminServer: null, adminUI: null, eventStoreDaprConfigPath: accessControlConfigPath, resiliencyConfigPath: …, stateStoreComponentPath: …, pubSubComponentPath: …)` → `var tenants = builder.AddHexalithTenantsServer(eventStoreResources, accessControlConfigPath)` → wire `folders`/`folders-workers`/`folders-ui` via the (slimmed) Folders helper. Preserve the Dapr-config path resolution block (`DaprComponents/accesscontrol.yaml`, fail-fast if missing) and the Keycloak/JWT wiring for every backend service (`ConfigureJwt` on eventstore/tenants/folders/folders-workers; Authority/ClientId env on folders-ui).
- [x] **Task 2 — Slim `FoldersAspireModule` to Folders-only concerns (AC: 3, 4).** In `src/Hexalith.Folders.Aspire/FoldersAspireModule.cs`: remove `AddFoldersSharedDaprComponents` (in-code `statestore`/`pubsub` creation) and the hand-rolled EventStore/Tenants sidecar wiring inside `AddHexalithFolders`. Re-shape `AddHexalithFolders` (or a replacement) to accept the platform `HexalithEventStoreResources` (or its `StateStore`/`PubSub`/`EventStore` + the Tenants resource) and wire **only** `folders`/`folders-workers`/`folders-ui` sidecars (still using the shared `stateStore`/`pubSub`). **Keep all seven public constants** and the `HexalithFoldersResources` record (or update its consumers + test in lockstep if the surface changes).
- [x] **Task 3 — Add Dapr component YAMLs (AC: 3).** Create `src/Hexalith.Folders.AppHost/DaprComponents/statestore.yaml`, `pubsub.yaml`, `resiliency.yaml` adapted from the Tenants AppHost versions, but with `scopes:` limited to `eventstore`, `tenants`, `folders`, `folders-workers`, `folders-ui` (no `eventstore-admin`, no `memories`, no `sample`). `statestore.yaml` must keep `metadata: actorStateStore "true"`, `redisHost "localhost:6379"`, **and `keyPrefix "none"`** to match today's in-code component. Ensure they are copied to output / discoverable at the path passed to the helper (mirror how `accesscontrol.yaml` is located in `Program.cs`).
- [x] **Task 4 — Update the AppHost csproj (AC: 2).** In `Hexalith.Folders.AppHost.csproj`: add `<ProjectReference Include="$(HexalithEventStoreRoot)\src\Hexalith.EventStore.Aspire\Hexalith.EventStore.Aspire.csproj" IsAspireProjectResource="false" />` and `<ProjectReference Include="$(HexalithTenantsRoot)\src\Hexalith.Tenants.Aspire\Hexalith.Tenants.Aspire.csproj" IsAspireProjectResource="false" />`; remove the two direct EventStore/Tenants **runtime** project refs. Keep the `Hexalith.Folders.Aspire` ref (`IsAspireProjectResource="false"`) and the `Hexalith.Folders.Server/UI/Workers` refs. (Do **not** add `Hexalith.Memories.Aspire` or `HexalithMemoriesRoot` — that is 9.2.)
- [x] **Task 5 — Update tests (AC: 5).** Update `tests/Hexalith.Folders.IntegrationTests/AspireTopologyTests.cs`: keep the constants test (5 app-IDs + 2 components) asserting the **same** values; replace/retire the `AddFoldersSharedDaprComponentsShouldRegisterStateStoreAndPubSubInResourceCollection` test if that helper is removed (assert the components now come from the platform composition instead); keep `HexalithFoldersResourcesShouldExposeAllRequiredProjectAndComponentBuilders`; update `AddHexalithFoldersShouldAttachDaprSidecarsForEveryProductionAppId` to drive the new composition and still assert all five production sidecars carry the correct `AppId` + `Config`. Confirm `AppHostBootSmokeTests`, `DaprPolicyConformanceTests`, `ContainerImageConformanceTests` are unaffected/green.
- [x] **Task 6 — Build + boot verification (AC: 6).** Run `dotnet build Hexalith.Folders.slnx`, then the narrowed test set (`Hexalith.Folders.IntegrationTests`, `Hexalith.Folders.UI.Tests`, `Hexalith.Folders.Contracts.Tests`), then `aspire run` and confirm `eventstore`/`tenants`/`folders`/`folders-workers`/`folders-ui` come up healthy with no `eventstore-admin*` resources. **AppHost/topology changes require an Aspire restart before the wiring can be trusted** (per project-context).

## Dev Notes

### Current state — what you are replacing (exact)

`src/Hexalith.Folders.AppHost/Program.cs` (today, ~75 lines):
```csharp
IResourceBuilder<ProjectResource> eventStore     = builder.AddProject<Projects.Hexalith_EventStore>(FoldersAspireModule.EventStoreAppId);
IResourceBuilder<ProjectResource> tenants        = builder.AddProject<Projects.Hexalith_Tenants>(FoldersAspireModule.TenantsAppId);
IResourceBuilder<ProjectResource> folders        = builder.AddProject<Projects.Hexalith_Folders_Server>(FoldersAspireModule.FoldersAppId);
IResourceBuilder<ProjectResource> foldersWorkers = builder.AddProject<Projects.Hexalith_Folders_Workers>(FoldersAspireModule.FoldersWorkersAppId);
IResourceBuilder<ProjectResource> foldersUi      = builder.AddProject<Projects.Hexalith_Folders_UI>(FoldersAspireModule.FoldersUiAppId);
_ = builder.AddHexalithFolders(eventStore, tenants, folders, foldersWorkers, foldersUi, accessControlConfigPath);
```
- Keycloak is added conditionally unless `EnableKeycloak == "false"`; realm URL = `{keycloakEndpoint}/realms/hexalith`. `ConfigureJwt(...)` is applied to **eventstore, tenants, folders, folders-workers** (not folders-ui); folders-ui gets `Folders__Authentication__Authority` = realmUrl + `Folders__Authentication__ClientId` = `"hexalith-folders"`. Backend services also receive `Authentication__JwtBearer__{Authority,Issuer,Audience,RequireHttpsMetadata,SigningKey}`. **Preserve all of this** — it must land on the resources now produced by the platform helpers.
- Dapr config path: `Program.cs` looks for `DaprComponents/accesscontrol.yaml` under the AppHost dir, falls back to CWD, throws `FileNotFoundException` if absent. Keep this pattern; reuse it for the new component-YAML paths.

`src/Hexalith.Folders.Aspire/FoldersAspireModule.cs` (today):
- Constants (KEEP, test-pinned): `EventStoreAppId="eventstore"`, `TenantsAppId="tenants"`, `FoldersAppId="folders"`, `FoldersWorkersAppId="folders-workers"`, `FoldersUiAppId="folders-ui"`, `StateStoreComponentName="statestore"`, `PubSubComponentName="pubsub"`.
- `AddFoldersSharedDaprComponents` builds the state store in code: `AddDaprComponent("statestore","state.redis").WithMetadata("actorStateStore","true").WithMetadata("redisHost","localhost:6379").WithMetadata("keyPrefix","none")` + `AddDaprPubSub("pubsub")`. **This moves to checked-in YAML** (Task 3) — preserve those three metadata values, especially `keyPrefix: none`.
- `AddHexalithFolders(builder, eventStore, tenants, folders, foldersWorkers, foldersUi, daprConfigPath?)` hand-rolls a `WithDaprSidecar(... DaprSidecarOptions{ AppId, Config } ...).WithReference(stateStore).WithReference(pubSub)` for **each** of the five projects, plus `folders`/`folders-workers` get `.WithReference(eventStore).WithReference(tenants).WaitFor(eventStore).WaitFor(tenants)`, and `folders-ui` gets `.WithReference(folders).WaitFor(folders).WithEnvironment("Folders__Client__BaseAddress", folders http endpoint).WithExternalHttpEndpoints()`. The EventStore/Tenants sidecar halves of this are **deleted** (the platform helper does them); the folders/workers/ui halves are **preserved**.

`src/Hexalith.Folders.Aspire/HexalithFoldersResources.cs`: `record HexalithFoldersResources(StateStore, PubSub, EventStore, Tenants, Folders, FoldersWorkers, FoldersUi)` — all seven names are reflection-asserted by a test; keep them or update the test together.

`Hexalith.Folders.AppHost.csproj` (today) references — to be changed (AC2):
```xml
<ProjectReference Include="..\Hexalith.Folders.Aspire\Hexalith.Folders.Aspire.csproj" IsAspireProjectResource="false" />
<ProjectReference Include="..\Hexalith.Folders.Server\Hexalith.Folders.Server.csproj" />
<ProjectReference Include="..\Hexalith.Folders.UI\Hexalith.Folders.UI.csproj" />
<ProjectReference Include="..\Hexalith.Folders.Workers\Hexalith.Folders.Workers.csproj" />
<ProjectReference Include="$(HexalithEventStoreRoot)\src\Hexalith.EventStore\Hexalith.EventStore.csproj" />   <!-- REMOVE -->
<ProjectReference Include="$(HexalithTenantsRoot)\src\Hexalith.Tenants\Hexalith.Tenants.csproj" />            <!-- REMOVE -->
```
`Directory.Build.props` already resolves `HexalithEventStoreRoot` and `HexalithTenantsRoot` (sibling-first, then parent). It does **not** define `HexalithMemoriesRoot` — leave it that way for 9.1.

### Target pattern — canonical Tenants AppHost (your reference)

`Hexalith.Tenants/src/Hexalith.Tenants.AppHost/Program.cs` composes via the helpers (lines ~44-92). The Tenants build uses the **full** EventStore (admin + UI); Folders wants **gateway-only** (the deviation that requires the Task 0 decision). The key calls Folders mirrors:
```csharp
HexalithEventStoreResources eventStoreResources = builder.AddHexalithEventStore(
    eventStore,
    adminServer: null,            // gateway-only — see CRITICAL decision; not supported by helper as-is
    adminUI: null,
    eventStoreDaprConfigPath: accessControlConfigPath,
    resiliencyConfigPath: resiliencyConfigPath,
    stateStoreComponentPath: stateStoreComponentPath,
    pubSubComponentPath: pubSubComponentPath);

IResourceBuilder<ProjectResource> tenants = builder.AddHexalithTenantsServer(
    eventStoreResources,
    accessControlConfigPath);     // appId defaults to "tenants"
```
Tenants AppHost csproj references the helpers, not the runtime projects:
```xml
<ProjectReference Include="$(HexalithEventStoreRoot)\src\Hexalith.EventStore.Aspire\Hexalith.EventStore.Aspire.csproj" IsAspireProjectResource="false" />
<ProjectReference Include="..\Hexalith.Tenants.Aspire\Hexalith.Tenants.Aspire.csproj" IsAspireProjectResource="false" />
```

### Helper signatures (pin exact params against these)

- `AddHexalithEventStore(this IDistributedApplicationBuilder, IResourceBuilder<ProjectResource> eventStore, IResourceBuilder<ProjectResource> adminServer, IResourceBuilder<ProjectResource>? adminUI = null, string? eventStoreDaprConfigPath = null, string? adminServerDaprConfigPath = null, string? resiliencyConfigPath = null, int eventStoreDaprHttpPort = 3501, string? daprPlacementHostAddress = null, string? daprSchedulerHostAddress = null, string? pubSubComponentPath = null)` — and a second overload that also takes `string? stateStoreComponentPath`. Returns `HexalithEventStoreResources(StateStore, PubSub, EventStore, AdminServer, AdminUI?)`. **Today rejects null `adminServer` — see CRITICAL decision.**
- `AddHexalithTenantsServer(this IDistributedApplicationBuilder, HexalithEventStoreResources eventStore, string daprConfigPath, string appId = "tenants", string? daprPlacementHostAddress = null, string? daprSchedulerHostAddress = null)` → `IResourceBuilder<ProjectResource>`. Internally `AddProject<TenantsServerProjectMetadata>(appId).AddEventStoreDomainModule(eventStore, appId, daprConfigPath, …)`; consumes only `eventStore.StateStore` + `eventStore.PubSub`.
- (For 9.2, not now) `AddHexalithMemoriesSearchIndexServer(builder, stateStore, pubSub, secretStoreComponentPath, llmComponentPath, redisConnectionString? = null, eventStoreTopic = "memories-events", serverName = "memories", daprHttpPort = 3502, daprGrpcPort = 50002, …)`.

### Dapr component YAML — adapt from Tenants, re-scope for Folders

Tenants `statestore.yaml` is `type: state.redis` with `redisHost localhost:6379`, `actorStateStore "true"`; `pubsub.yaml` is `type: pubsub.redis`; `resiliency.yaml` defines retry/timeout/circuit-breaker policies for `eventstore` + `statestore`/`pubsub`. For Folders:
- `statestore.yaml` scopes: `[eventstore, tenants, folders, folders-workers, folders-ui]`; **add `keyPrefix: none`** metadata (Tenants omits it; today's Folders code sets it — preserve to keep state-key layout unchanged).
- `pubsub.yaml` scopes: `[eventstore, tenants, folders, folders-workers, folders-ui]` (drop Tenants' `sample`/`memories`).
- `resiliency.yaml`: adapt as-is; targets reference `eventstore`/`statestore`/`pubsub`.

### Versions (do NOT reconcile here)

`Directory.Packages.props` pins **Aspire 13.4.6** + `CommunityToolkit.Aspire.Hosting.Dapr 13.0.0`. The architecture doc (13.3.0) and project-context/epics (13.4.3) are drifted from the actual pin — that doc reconciliation is **not** part of 9.1 (and partly belongs to 9.3). Use the existing 13.4.6 pin; do not bump or "fix" version numbers.

### Guardrails the refactor must not break

- **App-ID stability** is a hard contract (architecture I-4, `DaprPolicyConformanceTests`, `ContainerImageConformanceTests`): `eventstore`, `tenants`, `folders`, `folders-workers`, `folders-ui` must keep their exact values.
- **System working end-to-end:** the refactor must leave `aspire run` healthy — folders/workers reach eventstore + tenants, folders-ui reaches folders, JWT/Keycloak still wired. Satisfying the ACs is not enough if boot regresses.
- **Submodule policy (`CLAUDE.md`):** root-level submodules only; never `--init --recursive`. If Option A touches the `Hexalith.EventStore` submodule, work only at that root.

### Project Structure Notes

- Files touched (this repo): `src/Hexalith.Folders.AppHost/Program.cs`, `src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj`, `src/Hexalith.Folders.AppHost/DaprComponents/{statestore,pubsub,resiliency}.yaml` (new), `src/Hexalith.Folders.Aspire/FoldersAspireModule.cs`, possibly `src/Hexalith.Folders.Aspire/HexalithFoldersResources.cs`, `tests/Hexalith.Folders.IntegrationTests/AspireTopologyTests.cs`.
- If Option A: also `Hexalith.EventStore/src/Hexalith.EventStore.Aspire/*` (separate submodule + its tests + pointer bump).
- Solution: both AppHost and Aspire projects are in `Hexalith.Folders.slnx` (lines 19-20). Tests construct the AppHost with `DistributedApplication.CreateBuilder()` (not `DistributedApplicationTestingBuilder`); `AspireTopologyTests` lives in `Hexalith.Folders.IntegrationTests`; `AppHostBootSmokeTests` lives in `Hexalith.Folders.UI.Tests` and is a UI-composition-root test, independent of topology.

### Git Intelligence (recent AppHost/Aspire-relevant work)

- `cbf0db3 feat: Align AppHost with platform helpers and integrate Memories search-index` — **docs/planning only** (sprint-status, architecture.md, epics.md, project-context.md, change-proposals). **No `src` code changed.** The architecture/project-context "platform helper" + `memories` text is already in place; do not re-edit it.
- `fe2e1de feat: update Aspire SDK version and add launch settings` — touched AppHost csproj, `Properties/launchSettings.json`, `FoldersAspireModule.cs`, Keycloak realm json.
- `0dc927b` / `a2a301e` — established the container-image app-ID contract and the original `Program.cs` / `FoldersAspireModule.cs` / `HexalithFoldersResources.cs` / `accesscontrol.yaml`. These are the patterns you are refactoring; honor their app-ID and Keycloak conventions.
- This is the first Epic-9 story (no prior in-epic story). Epic 8 (immediately prior) was REST routes / a11y / C3 — unrelated to AppHost composition.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 9.1 (lines 1821-1832); AR-INFRA-01/02 (lines 328-329)]
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-06-22-apphost-memories-platform-alignment.md#Section 4.1/4.4/4.5 + Section 5 success criteria]
- [Source: _bmad-output/planning-artifacts/architecture.md#AppHost Composition (§396-400); I-1 (§559); I-3 (§561); I-4 (§562)]
- [Source: src/Hexalith.Folders.AppHost/Program.cs; src/Hexalith.Folders.Aspire/FoldersAspireModule.cs; src/Hexalith.Folders.Aspire/HexalithFoldersResources.cs; src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj; Directory.Build.props; Directory.Packages.props]
- [Source: Hexalith.Tenants/src/Hexalith.Tenants.AppHost/Program.cs (composition reference); Hexalith.Tenants/src/Hexalith.Tenants.AppHost/DaprComponents/{statestore,pubsub,resiliency}.yaml]
- [Source: Hexalith.EventStore/src/Hexalith.EventStore.Aspire/{HexalithEventStoreExtensions.cs:68,126,141,221,245,287, HexalithEventStoreResources.cs:16-21, HexalithEventStorePlatformExtensions.cs:57, HexalithEventStoreDomainModuleExtensions.cs:89-90}]
- [Source: Hexalith.Tenants/src/Hexalith.Tenants.Aspire/HexalithTenantsServerExtensions.cs:48-69]
- [Source: tests/Hexalith.Folders.IntegrationTests/AspireTopologyTests.cs; tests/Hexalith.Folders.UI.Tests/AppHostBootSmokeTests.cs; tests/Hexalith.Folders.Contracts.Tests/OpenApi/DaprPolicyConformanceTests.cs; tests/Hexalith.Folders.Contracts.Tests/Deployment/ContainerImageConformanceTests.cs]
- [Source: _bmad-output/project-context.md (stable app-IDs + Aspire-topology-via-platform-helpers rule, lines 71/73/130)]

## Open Questions (for PO / human review — do not block dev start; resolve at Task 0)

1. **Cross-submodule authorization (Option A):** Is the dev authorized to modify the `Hexalith.EventStore` submodule (add public gateway-only support + nullable `adminServer`) and bump its pointer, or must 9.1 stay within this repo (Option B)? The epic AC implies the helper should accept `adminServer: null`, which today it does not — so one of these must give.
2. **Acceptable topology surface change:** If Option B changes the `HexalithFoldersResources` shape (e.g., EventStore now arrives via the platform record), confirm it is OK to update the reflection shape-contract test accordingly (vs. preserving the exact seven properties).

## Dev Agent Record

### Agent Model Used

claude-opus-4-8[1m] (Claude Opus 4.8, 1M context)

### Debug Log References

Validation evidence (2026-06-23):
- `dotnet build Hexalith.Folders.slnx -c Debug` → **Build succeeded, 0 Warning(s), 0 Error(s)** (47 projects, incl. AppHost composing via the platform helpers). AC6 build half ✅.
- `dotnet build Hexalith.EventStore/src/Hexalith.EventStore.Aspire/...` → **0/0** (the modified helper library).
- `dotnet build Hexalith.EventStore/src/Hexalith.EventStore.AppHost/...` → **0/0** — proves backward-compat: the canonical **non-null** `adminServer` caller (full admin topology) still composes through the modified helper.
- `dotnet test Hexalith.Folders.IntegrationTests` → **603 passed / 0 failed** (incl. the 4 updated `AspireTopologyTests`).
- `dotnet test Hexalith.Folders.Contracts.Tests` → **263 passed / 0 failed** (incl. `DaprPolicyConformanceTests` + `ContainerImageConformanceTests` — AC5 conformance stays green).
- `dotnet test Hexalith.Folders.UI.Tests` → **521 passed / 0 failed** (incl. `AppHostBootSmokeTests`).
- First solution build hit a transient MSB3026/MSB4018 file-lock race on a `deps.json`; clean rebuild (`-m:1`) succeeded — not a code error.
- First solution restore hit **NU1605** (CommunityToolkit.Aspire.Hosting.Dapr 13.0.0 vs the platform's 13.4.0-preview); resolved by aligning the central pin (user-approved). See Change Log.

`aspire run` (AC6 boot half) — **blocked by a local Aspire CLI/DCP toolchain mismatch, NOT a topology defect:**
- The Aspire **CLI is 13.4.5** while the app/repo's `Aspire.Hosting` is **13.4.6** (`CliUpdateNotifier: Current version 13.4.5 … older than 13.4.6`).
- The CLI launches DCP with `start-apiserver … --tls-cert-file … --tls-key-file …`, but the bundled DCP rejects it: `the program finished with an error {"ExitCode": 1, "error": "unknown flag: --tls-cert-file"}` → `KubernetesService.EnsureKubernetesAsync` aborts (SIGABRT, exit 134) **before any resource is created**.
- The identical `unknown flag: --tls-cert-file` error appears in unrelated AppHost logs from 2026-06-01/06-02 → environment-wide and pre-existing; it reproduces for any Aspire app here regardless of topology.
- Composition correctness is instead proven by the build + `AspireTopologyTests` (all five sidecars carry the correct AppId+Config; statestore/pubsub are reused from the platform with zero Folders-local component creation) and by AC1 being structurally guaranteed (gateway-only never adds admin projects → no `eventstore-admin*` resources). Per instruction, **no source behavior changed** — final live-boot sign-off belongs in a DCP-capable environment / CI (or after `aspire update` aligns the CLI+DCP to 13.4.6).

### Completion Notes List

Ultimate context engine analysis completed — comprehensive developer guide created.

**Task 0 — Gateway-only approach decision: OPTION A (cross-submodule, platform-aligned).**
User explicitly authorized the cross-submodule change (Open Question #1) on 2026-06-23.

Rationale (confirmed by reading the full EventStore.Aspire surface):
- AC1 literally requires `AddHexalithEventStore(eventStore, adminServer: null, …)`. The helper today hard-requires
  a non-null `adminServer` (`HexalithEventStoreExtensions.cs:141` `ArgumentNullException.ThrowIfNull(adminServer)`)
  and unconditionally wires its reference/sidecar.
- The only public way to add the `eventstore` project is `AddHexalithEventStorePlatformProjects()` which adds **all
  three** projects (eventstore + eventstore-admin + eventstore-admin-ui) via `internal sealed` metadata classes
  unreachable from Folders. So Option B could not satisfy AC1 (must use the helper) *and* AC2 (no runtime ref) *and*
  AC1's "no eventstore-admin* resources" simultaneously. Option A is the only path that satisfies all six ACs.
- Verified **no consumer dereferences `HexalithEventStoreResources.AdminServer`** (the `.AdminServer` hits are an
  unrelated test fixture + the distinct `HexalithEventStorePlatformProjects.AdminServer` record). The only
  Aspire-hosting callers of `AddHexalithEventStore` are EventStore.AppHost and Tenants.AppHost, both passing
  non-null adminServer → making the param nullable is fully backward-compatible.

Option A implementation in `Hexalith.EventStore.Aspire` (submodule, separate commit + pointer bump):
1. `HexalithEventStoreResources.AdminServer` → nullable.
2. `AddHexalithEventStore` (both overloads) `adminServer`/`adminUI` → nullable; dropped the `ThrowIfNull(adminServer)`
   guard; wrapped all admin-server + admin-UI wiring in `if (adminServer is not null) { … }` (no-op when gateway-only).
3. Added public `AddHexalithEventStoreGatewayProject(name = "eventstore")` returning only the command-gateway
   `IResourceBuilder<ProjectResource>` (uses the existing internal `EventStoreProjectMetadata`).

The EventStore.Aspire change was committed in the submodule as `79fb952b` ("feat: enhance documentation for admin
server and add gateway-only project support") on top of `68ae83b` (v3.17.0). The Folders submodule pointer now
targets the EventStore release commit `c6e40e6b` (v3.18.0), which includes `79fb952b`.

**Folders-side implementation (Tasks 1–6):**
- **Task 1 (AC 1,4):** `Program.cs` now composes EventStore gateway-only via `AddHexalithEventStoreGatewayProject` +
  `AddHexalithEventStore(eventStore, adminServer: null, adminUI: null, …, stateStoreComponentPath, pubSubComponentPath)`
  and Tenants via `AddHexalithTenantsServer`, then wires folders/workers/ui through the slimmed `AddHexalithFolders`.
  The `accesscontrol.yaml` fail-fast path resolution and the full Keycloak/JWT wiring (`ConfigureJwt` on
  eventstore/tenants/folders/folders-workers; Authority/ClientId env on folders-ui) are preserved verbatim.
- **Task 2 (AC 3,4):** `FoldersAspireModule` lost `AddFoldersSharedDaprComponents` and the EventStore/Tenants sidecar
  halves; `AddHexalithFolders` now takes the shared `stateStore`/`pubSub` + `eventStore`/`tenants`/folders projects
  (decomposed params keep the **packable** `Hexalith.Folders.Aspire` free of an EventStore.Aspire dependency) and wires
  only the three Folders sidecars. All seven public constants and the seven-property `HexalithFoldersResources` record
  are retained (`HexalithFoldersResources.cs` unchanged).
- **Task 3 (AC 3):** Added `DaprComponents/{statestore,pubsub,resiliency}.yaml`, scoped to
  `[eventstore, tenants, folders, folders-workers, folders-ui]` (no eventstore-admin / memories / sample). statestore
  keeps `actorStateStore "true"`, `redisHost "localhost:6379"`, **and `keyPrefix "none"`**. LF line endings.
- **Task 4 (AC 2):** AppHost csproj swaps the two runtime EventStore/Tenants project refs for
  `Hexalith.EventStore.Aspire` + `Hexalith.Tenants.Aspire` with `IsAspireProjectResource="false"`; `Program.cs` no
  longer uses `Projects.Hexalith_EventStore` / `Projects.Hexalith_Tenants`.
- **Task 5 (AC 5):** Updated `AspireTopologyTests` — kept the constants + `HexalithFoldersResources` shape tests;
  replaced the removed-`AddFoldersSharedDaprComponents` test with one asserting AddHexalithFolders creates **zero** new
  Dapr components and reuses the platform statestore/pubsub instances; reworked the sidecar test to drive the new
  composition and assert all five sidecars' AppId+Config. Added a test-only `Hexalith.EventStore.Aspire` ref.
- **Task 6 (AC 6):** Build + narrowed test set green (see Debug Log). `aspire run` boot blocked by a local
  CLI/DCP toolchain mismatch (documented in Debug Log) — not a topology bug; no source behavior changed.

### File List

**This repo (Hexalith.Folders):**
- `src/Hexalith.Folders.AppHost/Program.cs` (modified)
- `src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj` (modified)
- `src/Hexalith.Folders.AppHost/DaprComponents/statestore.yaml` (new)
- `src/Hexalith.Folders.AppHost/DaprComponents/pubsub.yaml` (new)
- `src/Hexalith.Folders.AppHost/DaprComponents/resiliency.yaml` (new)
- `src/Hexalith.Folders.Aspire/FoldersAspireModule.cs` (modified)
- `Directory.Packages.props` (modified — CommunityToolkit.Aspire.Hosting.Dapr 13.0.0 → 13.4.0-preview.1.260602-0230)
- `tests/Hexalith.Folders.IntegrationTests/AspireTopologyTests.cs` (modified)
- `tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj` (modified — test-only EventStore.Aspire ref)
- `tests/Hexalith.Folders.Contracts.Tests/Deployment/AppHostPlatformCompositionConformanceTests.cs` (new — AC2/AC3 guardrails)
- `_bmad-output/implementation-artifacts/tests/9-1-test-summary.md` (new — durable QA automation summary)
- `_bmad-output/implementation-artifacts/tests/test-summary.md` (modified — canonical latest-run summary)
- `Hexalith.EventStore` (submodule pointer → `c6e40e6b` / v3.18.0, includes implementation commit `79fb952b`)
- `_bmad-output/implementation-artifacts/9-1-adopt-eventstore-and-tenants-platform-aspire-helpers.md` (story tracking)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (status tracking)

**Hexalith.EventStore submodule (implementation committed as `79fb952b`, released at gitlink target `c6e40e6b`):**
- `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs` (nullable adminServer/adminUI + guarded admin wiring)
- `src/Hexalith.EventStore.Aspire/HexalithEventStorePlatformExtensions.cs` (new `AddHexalithEventStoreGatewayProject`)
- `src/Hexalith.EventStore.Aspire/HexalithEventStoreResources.cs` (nullable `AdminServer`)

_Not modified (intentionally): `src/Hexalith.Folders.Aspire/HexalithFoldersResources.cs` — seven-property shape preserved._

**Review-added (Senior Developer Review, 2026-06-23):**
- `tests/Hexalith.Folders.IntegrationTests/AspireTopologyTests.cs` (modified — added `AddHexalithEventStoreWithCheckedInYamlPathsShouldSourceReusableStateStoreAndPubSubComponents` covering the production YAML-`LocalPath` component-sourcing branch).

## Change Log

| Date | Change |
| --- | --- |
| 2026-06-23 | Story 9.1 implemented (Option A). EventStore.Aspire gains gateway-only support (nullable adminServer/adminUI + `AddHexalithEventStoreGatewayProject`), committed in the submodule (`79fb952b`) and consumed via the `c6e40e6b` / v3.18.0 gitlink target. Folders AppHost composes EventStore (gateway-only) + Tenants via the platform helpers; `FoldersAspireModule` slimmed to Folders-only sidecars; checked-in `statestore`/`pubsub`/`resiliency` Dapr YAMLs added; AppHost csproj switched to the `.Aspire` helper refs. Aligned `CommunityToolkit.Aspire.Hosting.Dapr` 13.0.0 → 13.4.0-preview.1.260602-0230 to the platform pin (user-approved) to resolve an NU1605 transitive downgrade. Tests updated (AspireTopologyTests). Build + narrowed test suites green (603/263/521); `aspire run` boot blocked by a local Aspire CLI(13.4.5)/DCP `--tls-cert-file` mismatch (environmental, not a topology bug). |
| 2026-06-23 | Senior Developer Review (AI, auto-fix mode). Adversarial review re-verified independently: build `Hexalith.Folders.slnx` 0/0; IntegrationTests 604/0, Contracts.Tests 268/0, UI.Tests 521/0 (all higher than the dev's reported counts because of the net-new tests — all green). All six ACs confirmed implemented; every `[x]` task verified done; the Option A submodule change (`79fb952b`) confirmed present and the admin wiring correctly guarded behind `if (adminServer is not null)`; File List confirmed complete vs `git status` (only the excluded `_bmad-output` orchestration log is undocumented). No CRITICAL/HIGH/MEDIUM defects. Auto-fix applied: added `AddHexalithEventStoreWithCheckedInYamlPathsShouldSourceReusableStateStoreAndPubSubComponents` to `AspireTopologyTests` to cover the YAML-`LocalPath` component-sourcing branch the production AppHost uses (the existing topology tests only drove the in-code/null-path branch) — IntegrationTests now 605/0. Three LOW observations recorded (see review section). Status → done. |

## Senior Developer Review (AI)

**Reviewer:** jpiquot (automated adversarial review, auto-fix mode) · **Date:** 2026-06-23 · **Outcome:** ✅ Approve (status → done)

### Scope & method

Adversarial validation of every story claim against the actual implementation: read all File-List files plus the cross-repo `Hexalith.EventStore.Aspire` submodule diff, cross-checked the File List against `git status`, and **independently re-ran** the build and the three claimed test suites (not trusting the dev's reported counts). `_bmad/` and `_bmad-output/` excluded from code review per workflow policy.

### Independent verification (re-run, not trusted from the record)

| Gate | Story claim | Re-run result |
| --- | --- | --- |
| `dotnet build Hexalith.Folders.slnx` | 0/0 | ✅ 0 Warning / 0 Error |
| IntegrationTests | 603/0 | ✅ 604/0 → **605/0** after review test |
| Contracts.Tests | 263/0 | ✅ 268/0 (incl. 5 new conformance tests) |
| UI.Tests (incl. `AppHostBootSmokeTests`) | 521/0 | ✅ 521/0 |

Counts run higher than the dev's because the net-new conformance/topology tests are real and green — not a discrepancy.

### Acceptance Criteria audit

- **AC1 (gateway-only EventStore):** ✅ `Program.cs` uses `AddHexalithEventStoreGatewayProject` + `AddHexalithEventStore(adminServer: null, adminUI: null, …)`; the submodule guards all admin/UI wiring behind `if (adminServer is not null)`. New `GatewayOnlyCompositionShouldNotProduceAnyEventStoreAdminResources` proves no `eventstore-admin*` sidecars.
- **AC2 (.Aspire helper refs):** ✅ csproj swaps the two runtime refs for `Hexalith.EventStore.Aspire` + `Hexalith.Tenants.Aspire` (`IsAspireProjectResource="false"`); `Program.cs` no longer uses `Projects.Hexalith_EventStore`/`Projects.Hexalith_Tenants`. Pinned by `AppHostPlatformCompositionConformanceTests`.
- **AC3 (checked-in Dapr YAMLs):** ✅ `statestore`/`pubsub`/`resiliency` present, scoped to the five Folders app-ids (no `eventstore-admin`/`memories`/`sample`); statestore keeps `actorStateStore "true"`, `redisHost "localhost:6379"`, `keyPrefix "none"`. Helper sources them via `LocalPath` and still yields non-null components (verified — see LOW-2).
- **AC4 (Folders unchanged; no hand-rolled wiring):** ✅ `FoldersAspireModule` lost `AddFoldersSharedDaprComponents` and the EventStore/Tenants sidecar halves; folders/workers reference+`WaitFor` eventstore+tenants, folders-ui references folders; all seven constants retained.
- **AC5 (tests green):** ✅ Shape contract (7 properties) preserved; sidecar + zero-new-component tests reworked; conformance suites green.
- **AC6 (builds & boots):** ⚠️ Build half ✅. Live-boot half **unverified** — blocked by an environment-wide Aspire CLI 13.4.5 / DCP `--tls-cert-file` mismatch (reproduces for any Aspire app here, pre-dates this story). Composition correctness proven structurally + by tests; final live-boot sign-off belongs in a DCP-capable env / CI.

### Findings

No CRITICAL / HIGH / MEDIUM findings. Every `[x]` task is genuinely done; the Option A submodule change (`79fb952b`) is real, committed, and included in the current EventStore gitlink target (`c6e40e6b`); the File List is complete vs git.

- **LOW-1 (behavior-neutral, out of story scope):** In gateway-only mode the helper consumes `resiliencyConfigPath` **only** to set the admin-server `AdminServer__ResiliencyConfigPath` env var (the admin UI's `/dapr/resiliency` viewer). With `adminServer: null` that block is skipped, so the checked-in `resiliency.yaml` is passed but **wired into no sidecar's resources** — functionally inert. This is behavior-preserving (the original Folders topology had no resiliency at all), so **not** auto-fixed: actually applying it would be a behavior change outside this story's "behavior-preserving refactor" boundary. Recommend a follow-up (helper enhancement to load resiliency for gateway-only sidecars, or an Epic-9 story) if active resiliency policies are wanted.
- **LOW-2 (auto-fixed):** The topology tests drove only the helper's **in-code** component branch (null paths); the production AppHost uses the **YAML `LocalPath`** branch. Verified the YAML branch returns non-null `StateStore`/`PubSub` (so no boot null-deref), and **added** `AddHexalithEventStoreWithCheckedInYamlPathsShouldSourceReusableStateStoreAndPubSubComponents` to exercise the exact production wiring — IntegrationTests 604 → 605, green.
- **LOW-3 (environmental, can't fix here):** AC6 live-boot unverifiable locally (DCP `--tls-cert-file`). Tracked above; defer to CI / a DCP-capable environment.

### Decision

0 CRITICAL issues remaining → **Status: done**. LOW-1 and LOW-3 are non-blocking and recorded for follow-up.
