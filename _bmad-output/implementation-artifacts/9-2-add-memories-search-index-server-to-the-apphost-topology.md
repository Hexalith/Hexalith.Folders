---
baseline_commit: 8ffaa13b268dfc639bcf2dd03ade395593b90774
---

# Story 9.2: Add the Memories search-index server to the AppHost topology

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a platform engineer,
I want the Memories search-index server hosted in the Folders topology,
so that the platform semantic search-index service runs alongside Folders locally and in release validation.

## Context & Scope Boundary

This is the **second story of Epic 9** (AppHost Platform Alignment). Story 9.1 already migrated the AppHost to the platform Aspire helpers (EventStore gateway-only + Tenants) and added the checked-in `statestore.yaml`/`pubsub.yaml`/`resiliency.yaml`. **9.2 is purely additive hosting** — it adds the Memories search-index server to the same topology, reusing the shared EventStore state store + pub/sub. It does **not** change `folders`/`folders-workers`/`folders-ui` behavior, and it does **not** make anything flow end-to-end (no producer exists until Epic 10).

- **In scope (9.2):** Reference `Hexalith.Memories.Aspire` (add `HexalithMemoriesRoot` to `Directory.Build.props` + the AppHost csproj ref); call `AddHexalithMemoriesSearchIndexServer(stateStore, pubSub, secretStorePath, llmPath, serverName: "memories")` in `Program.cs` reusing the shared `eventStoreResources.StateStore`/`.PubSub`; add `DaprComponents/secretstore.memories.yaml` + `llm.memories.yaml` (+ the `secrets.json` they reference); add `memories` to the `scopes:` of the shared `statestore.yaml`/`pubsub.yaml`; add a `MemoriesAppId = "memories"` constant; register `memories` as a deny-by-default app-id in the production access-control + sidecar-binding + pub/sub-scope artifacts; update the structural-topology, local-composition-conformance, and production Dapr-policy conformance tests so they stay green.
- **Deferred to 9.3:** The `hexalith-folders → folders-index` source→index routing config (`EventStoreIntegration__Routing__SourceToTenantMap__hexalith-folders=folders-index` + `AutoProvisionRoutedTenants=true`) on the `memories` resource; the architecture.md / project-context.md doc edits and the Memories search-index handoff note. **Do NOT add the routing env vars in 9.2** (the canonical Tenants AppHost adds its `hexalith-tenants → tenants-index` routing inline — that is the 9.3 analog; omit it here).
- **Deferred to Epic 10:** The worker-side producer, the `folders`/`folders-workers` → `memories` *invoke* authorization (production caller policies + their negative-test rows), and any `memories-events` pub/sub topic scope. **Do NOT add folders→memories invoke allow-rules or pub/sub topic scopes in 9.2** — there is no producer yet, and the conformance suite's empty-scope assertions actively require `memories` to carry no production pub/sub topics (see Critical Implementation Notes).

## Acceptance Criteria

1. **Memories server composed via the platform helper, reusing the shared components.** `src/Hexalith.Folders.AppHost/Program.cs` resolves `secretstore.memories.yaml` + `llm.memories.yaml` paths through the existing fail-fast `ResolveDaprConfigPath(builder.AppHostDirectory, …)` helper and calls `builder.AddHexalithMemoriesSearchIndexServer(eventStoreResources.StateStore, eventStoreResources.PubSub, memoriesSecretStorePath, memoriesLlmConfigPath, serverName: FoldersAspireModule.MemoriesAppId)`. The Memories server reuses the **same** shared `statestore`/`pubsub` component instances returned by `AddHexalithEventStore`; it creates no Folders-local copies. **No source→index routing env vars** (`EventStoreIntegration__Routing__*`) are set (deferred to 9.3). The `memories` resource is hosted standalone — `folders`/`folders-workers`/`folders-ui` do **not** gain `.WithReference(memories)`/`.WaitFor(memories)` (deferred to Epic 10), and `memories` is **not** added to the Keycloak/JWT wiring block (parity with the Tenants AppHost, which does not JWT-wire its Memories server).
2. **Build wiring resolves the Memories submodule and references the `.Aspire` helper.** `Directory.Build.props` resolves `HexalithMemoriesRoot` sibling-first then parent (mirroring `HexalithEventStoreRoot`/`HexalithTenantsRoot`, probing an existing folder such as `Hexalith.Memories\src\Hexalith.Memories.Contracts`). `Hexalith.Folders.AppHost.csproj` adds `<ProjectReference Include="$(HexalithMemoriesRoot)\src\Hexalith.Memories.Aspire\Hexalith.Memories.Aspire.csproj" IsAspireProjectResource="false" />` alongside the EventStore/Tenants `.Aspire` refs. **No Folders-owned `memories` container/runtime project is added** — the `memories` project is referenced cross-repo by the helper via `SuppressBuild` metadata (Aspire runs children `--no-build`), exactly as `eventstore`/`tenants` are. `Program.cs` does not use any `Projects.Hexalith_Memories*` generated metadata.
3. **Checked-in Memories Dapr component files; shared components re-scoped for `memories`.** `src/Hexalith.Folders.AppHost/DaprComponents/secretstore.memories.yaml` (component `name: secretstore`, `type: secretstores.local.file`, `secretsFile: "DaprComponents/secrets.json"`), `llm.memories.yaml` (component `name: llm`, `type: conversation.echo`), and `secrets.json` (`{}`) exist (adapted from the Tenants AppHost), LF line endings. The shared `statestore.yaml` **and** `pubsub.yaml` add `memories` to their `scopes:` lists (now `[eventstore, tenants, folders, folders-workers, folders-ui, memories]`) — this is **mandatory**: the Memories sidecar reuses both shared components, and a Dapr component `scopes:` allow-list blocks any unlisted app-id. The `memories-secretstore`/`memories-llm` components stay unscoped (global, like the Tenants AppHost). `resiliency.yaml` is unchanged.
4. **`MemoriesAppId` constant added; topology gains exactly the Memories resources.** `FoldersAspireModule` gains `public const string MemoriesAppId = "memories";` (the eighth stable constant). When composed, the running topology adds exactly: `memories` (project + Dapr sidecar on HTTP `3502` / gRPC `50002`), `memories-vectors` (`redis/redis-stack` container), `memories-graphs` (`falkordb/falkordb` container), `memories-secretstore` + `memories-llm` Dapr components. `folders`/`folders-workers`/`folders-ui` keep identical app-ids, sidecars, references, and JWT wiring (behavior-preserving); the gateway-only EventStore composition still produces **no** `eventstore-admin*` resources.
5. **Local-dev composition + structural topology tests updated and green.** In `tests/Hexalith.Folders.Contracts.Tests/Deployment/AppHostPlatformCompositionConformanceTests.cs`, move `"memories"` out of `ForbiddenScopes` and into `ExpectedFoldersScopes` and update the `// no memories (deferred to 9.2)` comment — so the `statestore.yaml`/`pubsub.yaml` scope assertions accept the new `memories` scope. In `tests/Hexalith.Folders.IntegrationTests/AspireTopologyTests.cs`, add `FoldersAspireModule.MemoriesAppId.ShouldBe("memories")` to `FoldersAspireModuleShouldExposeStableDaprAppIdsAndComponentNames`, and add a **new** test that composes `AddHexalithMemoriesSearchIndexServer` and asserts the `memories` sidecar app-id plus the `memories-secretstore`/`memories-llm` components and `memories-vectors`/`memories-graphs` containers are present. The existing closed-set `GatewayOnlyCompositionShouldNotProduceAnyEventStoreAdminResources` test is **left unchanged** (it deliberately does not compose Memories, so its 5-sidecar list stays valid). The IntegrationTests csproj gains a test-only `Hexalith.Memories.Aspire` reference (mirroring the test-only EventStore.Aspire ref 9.1 added).
6. **Production deny-by-default policy + conformance updated and green (deny-by-default, no invoke authorization).** `MemoriesAppId` is added to `StableAppIds` in `tests/Hexalith.Folders.Contracts.Tests/OpenApi/DaprPolicyConformanceTests.cs`. To keep the closed-set assertions green: `deploy/dapr/production/accesscontrol.yaml` gains a sixth deny-by-default Configuration doc `hexalith-folders-production-accesscontrol-memories` (`defaultAction: deny`, `policies: []`, a `kubernetes` secret scope `defaultAccess: deny` / `allowedSecrets: []`, with the `hexalith.io/target-app-id: memories` + `hexalith.io/trust-domain-template: DAPR_TRUST_DOMAIN` annotations); `deploy/dapr/production/sidecar-config-bindings.yaml` gains a sixth `memories` Deployment patch wiring `dapr.io/config: hexalith-folders-production-accesscontrol-memories`; `deploy/dapr/production/pubsub.yaml` adds `;memories=` (empty topics) to **both** `publishingScopes` and `subscriptionScopes`; `tests/fixtures/dapr-policy-conformance.yaml` has its `policyProvenance.semanticSha256` regenerated (the failing test prints the new hash) and a `- targetAppId: memories / allowRules: []` entry added to `targetPolicies`. **No new caller/invoke allow-rules and no new conformance cases** are added (memories invoke authorization + its 7-category negative tests are Epic 10). `ContainerImageConformanceTests` (Folders-owned images only — `memories` is cross-repo) and `AppHostBootSmokeTests` (a UI Keycloak-composition test) require **no** change; confirm both stay green.
7. **Builds and the narrowed suites pass.** `dotnet build Hexalith.Folders.slnx` succeeds (0/0). The narrowed test set is green: `Hexalith.Folders.IntegrationTests` (incl. the updated/new `AspireTopologyTests`), `Hexalith.Folders.Contracts.Tests` (incl. `DaprPolicyConformanceTests`, `AppHostPlatformCompositionConformanceTests`, `ContainerImageConformanceTests`), and `Hexalith.Folders.UI.Tests` (incl. `AppHostBootSmokeTests`). `aspire run` brings the topology up healthy with `memories` (+ `memories-vectors`/`memories-graphs`) alongside the 9.1 resources, **with no `eventstore-admin*` resources**. (Live boot may again be blocked by the environment-wide Aspire CLI/DCP `--tls-cert-file` mismatch documented in 9.1 — if so, record it and rely on build + structural tests for composition correctness; do not treat it as a topology defect.)

## 🚩 Critical Implementation Notes (read before writing any code)

The Memories.Aspire helper is **already complete** — unlike 9.1, **no cross-submodule change is required**. The risks here are entirely in the conformance tests, which use **closed sets** derived from `FoldersAspireModule` constants. Get these right and the story is mechanical:

1. **The local-composition conformance test currently FORBIDS `memories`.** `AppHostPlatformCompositionConformanceTests.cs:40` lists `"memories"` in `ForbiddenScopes` with the comment *"no memories (deferred to 9.2)"* (`:30`). Adding `- memories` to `statestore.yaml`/`pubsub.yaml` scopes will fail this test on **two** assertions (`scopes.ShouldBe(ExpectedFoldersScopes)` and `scopes.ShouldNotContain("memories")`) until you move `memories` from `ForbiddenScopes` (`:40`) into `ExpectedFoldersScopes` (`:31-38`). This is the load-bearing local change.
2. **The production pub/sub scope test FORCES `memories` to EMPTY topics.** `DaprPolicyConformanceTests.ProductionPubSubComponentShouldConstrainTenantEventTopicScopes` (`:219-233`) requires `publishingScopes`/`subscriptionScopes` keys to **equal** `StableAppIds`, AND asserts every app except `tenants` (publish) / `folders`+`folders-workers` (subscribe) has **empty** scopes. So you must add `;memories=` (empty) to both scope strings — and you must **not** give `memories` a `memories-events` (or any) topic. The ingestion topic is Epic 10. Do not add `memories-events` to `protectedTopics`.
3. **The semantic hash is auto-printed — never hand-compute it.** Adding the sixth deny-by-default Configuration doc changes `ComputeSemanticHash` output. `ProductionAccessControlPolicyShouldBeDenyByDefaultAndMatchFixtureProvenance` (`:79-83`) fails with the message *"New normalized hash: {hash}"*. Run the test once, copy that hash into `tests/fixtures/dapr-policy-conformance.yaml` `policyProvenance.semanticSha256`. Because `memories` has `policies: []`, **no new allow-rules and no new conformance `cases`** are introduced (`PolicyConformanceFixtureShouldCoverAllowedAndDeniedTriples` iterates `policy.AllowRules()`, which is unchanged), so the fixture's `cases:` block stays as-is; only the hash and the `targetPolicies` list change.
4. **Do NOT touch the existing closed gateway-only topology test.** `AspireTopologyTests.GatewayOnlyCompositionShouldNotProduceAnyEventStoreAdminResources` (`:104-150`) asserts an exact 5-sidecar list and deliberately does **not** compose Memories. Leave it. Add Memories coverage in a **new** test method instead — conflating the two would force you to weaken the no-admin invariant.
5. **Do NOT image `memories`.** `ContainerImageConformanceTests` enumerates a closed set of exactly the three Folders-owned images (`server`/`workers`/`ui`); `eventstore`/`tenants`/`memories` are cross-repo and intentionally absent. Adding `memories` as a Folders-owned container project would break this test and the SuppressBuild contract. Reference only `Hexalith.Memories.Aspire`.
6. **Do NOT re-edit `project-context.md` or `architecture.md`.** Both already list `memories` in the stable app-id set and document `AddHexalithMemoriesSearchIndexServer` (pre-applied by commit `cbf0db3`). Doc consistency is 9.3's scope.
7. **`secrets.json` is required for boot.** `secretstore.memories.yaml` references `secretsFile: "DaprComponents/secrets.json"`; the `secretstores.local.file` component fails to initialize at boot if it is missing. Add `DaprComponents/secrets.json` = `{}` (the Tenants AppHost has the identical file).

## Tasks / Subtasks

- [x] **Task 1 — Build wiring: resolve the Memories submodule + reference the `.Aspire` helper (AC: 2).**
  - [x] In `Directory.Build.props`, add two `HexalithMemoriesRoot` conditions mirroring the EventStore/Tenants pattern (`:4-7`): sibling `$(MSBuildThisFileDirectory)Hexalith.Memories` when `Exists('…Hexalith.Memories\src\Hexalith.Memories.Contracts')`, then parent `$(MSBuildThisFileDirectory)..\Hexalith.Memories`.
  - [x] In `src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj`, add `<ProjectReference Include="$(HexalithMemoriesRoot)\src\Hexalith.Memories.Aspire\Hexalith.Memories.Aspire.csproj" IsAspireProjectResource="false" />` next to the EventStore/Tenants `.Aspire` refs (`:17-18`). Do **not** add a Memories runtime/container project.
- [x] **Task 2 — Add the `MemoriesAppId` constant (AC: 4).** In `src/Hexalith.Folders.Aspire/FoldersAspireModule.cs`, add `public const string MemoriesAppId = "memories";` after `PubSubComponentName`. Do not change `AddHexalithFolders` (Memories is composed in `Program.cs`, not in the Folders helper).
- [x] **Task 3 — Add the Memories Dapr component files + re-scope the shared components (AC: 3).**
  - [x] Create `src/Hexalith.Folders.AppHost/DaprComponents/secretstore.memories.yaml` (adapt from `Hexalith.Tenants/src/Hexalith.Tenants.AppHost/DaprComponents/secretstore.memories.yaml`: component `name: secretstore`, `type: secretstores.local.file`, `secretsFile: "DaprComponents/secrets.json"`, `nestedSeparator: ":"`).
  - [x] Create `src/Hexalith.Folders.AppHost/DaprComponents/llm.memories.yaml` (adapt from the Tenants `llm.memories.yaml`: component `name: llm`, `type: conversation.echo`, `responseCacheTTL: "0s"`).
  - [x] Create `src/Hexalith.Folders.AppHost/DaprComponents/secrets.json` = `{}` (referenced by `secretstore.memories.yaml`).
  - [x] Add `- memories` to the `scopes:` of **both** `statestore.yaml` and `pubsub.yaml`. Update their component-scoping header comments (statestore currently says *"There is no eventstore-admin or memories app-id in the Folders topology"* — fix it).
  - [x] Ensure all new YAML/JSON files use LF endings (`.editorconfig` pins YAML/container artifacts to LF) and are copied to output via the same mechanism as the existing `DaprComponents` files.
- [x] **Task 4 — Compose the Memories server in the AppHost (AC: 1, 4).** In `src/Hexalith.Folders.AppHost/Program.cs`: add `using Hexalith.Memories.Aspire;`; resolve `string memoriesSecretStorePath = ResolveDaprConfigPath(builder.AppHostDirectory, "secretstore.memories.yaml");` and `string memoriesLlmConfigPath = ResolveDaprConfigPath(builder.AppHostDirectory, "llm.memories.yaml");`; after the Tenants/Folders composition (and before or after the `AddHexalithFolders` block, but **outside** the Keycloak block) call `HexalithMemoriesSearchIndexServerResources memories = builder.AddHexalithMemoriesSearchIndexServer(eventStoreResources.StateStore, eventStoreResources.PubSub, memoriesSecretStorePath, memoriesLlmConfigPath, serverName: FoldersAspireModule.MemoriesAppId);`. Do **not** chain any `EventStoreIntegration__Routing__*` env vars (9.3) and do **not** JWT-wire `memories`. The `memories` variable can be left unconsumed (no folders→memories references in 9.2).
- [x] **Task 5 — Update local-composition + structural topology tests (AC: 5).**
  - [x] `AppHostPlatformCompositionConformanceTests.cs`: move `"memories"` from `ForbiddenScopes` (`:40`) into `ExpectedFoldersScopes` (`:31-38`, e.g. append `FoldersAspireModule.MemoriesAppId`); update the `:29-30` comment.
  - [x] `tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj`: add a test-only `<ProjectReference Include="$(HexalithMemoriesRoot)\src\Hexalith.Memories.Aspire\Hexalith.Memories.Aspire.csproj" />` (mirror the existing test-only EventStore.Aspire ref).
  - [x] `AspireTopologyTests.cs`: add `FoldersAspireModule.MemoriesAppId.ShouldBe("memories");` to `FoldersAspireModuleShouldExposeStableDaprAppIdsAndComponentNames`. Add a new test (e.g. `AddHexalithMemoriesSearchIndexServerShouldRegisterMemoriesSidecarComponentsAndContainers`) that, over the `BuildGatewayOnlyComposition` builder, calls `builder.AddHexalithMemoriesSearchIndexServer(…)` with the checked-in `secretstore.memories.yaml`/`llm.memories.yaml` paths (resolved via the existing `RepositoryPath` helper) and asserts: a `ProjectResource` carries a Dapr sidecar with `AppId == "memories"`; `IDaprComponentResource` resources named `memories-secretstore` and `memories-llm` exist; `ContainerResource` resources named `memories-vectors` and `memories-graphs` exist. Leave `GatewayOnlyCompositionShouldNotProduceAnyEventStoreAdminResources` unchanged.
- [x] **Task 6 — Register `memories` as a deny-by-default production app-id (AC: 6).**
  - [x] `DaprPolicyConformanceTests.cs`: add `FoldersAspireModule.MemoriesAppId` to `StableAppIds` (`:28-35`).
  - [x] `deploy/dapr/production/accesscontrol.yaml`: append a sixth `---` Configuration doc `hexalith-folders-production-accesscontrol-memories` modeled on the `tenants` doc (`:56-74`): `defaultAction: deny`, `trustDomain: hexalith-production`, `namespace: hexalith-production`, `policies: []`, annotations `hexalith.io/target-app-id: memories` + `hexalith.io/trust-domain-template: DAPR_TRUST_DOMAIN`, and a `secrets.scopes` entry `storeName: kubernetes` / `defaultAccess: deny` / `allowedSecrets: []`.
  - [x] `deploy/dapr/production/sidecar-config-bindings.yaml`: append a sixth `---` Deployment patch (model on the `tenants` one, `:16-27`) with `dapr.io/app-id: memories` and `dapr.io/config: hexalith-folders-production-accesscontrol-memories` (choose a deployment `metadata.name`, e.g. `hexalith-memories`).
  - [x] `deploy/dapr/production/pubsub.yaml`: append `;memories=` to **both** the `publishingScopes` value (`:15`) and the `subscriptionScopes` value (`:17`). Leave `protectedTopics` unchanged.
  - [x] `tests/fixtures/dapr-policy-conformance.yaml`: add `- targetAppId: memories\n    allowRules: []` to `targetPolicies`. Run the policy test, copy the printed *"New normalized hash"* into `policyProvenance.semanticSha256`. Add **no** new `cases`.
  - [x] Confirm `ContainerImageConformanceTests` and `AppHostBootSmokeTests` still pass without edits.
- [x] **Task 7 — Build + boot verification (AC: 7).** Run `dotnet build Hexalith.Folders.slnx`, then `dotnet test` for `Hexalith.Folders.IntegrationTests`, `Hexalith.Folders.Contracts.Tests`, and `Hexalith.Folders.UI.Tests`. Then attempt `aspire run` and confirm `memories`/`memories-vectors`/`memories-graphs` come up healthy alongside the 9.1 topology with no `eventstore-admin*` resources. **AppHost/topology changes require an Aspire restart before the wiring can be trusted** (per project-context). If `aspire run` is blocked by the known environmental DCP/CLI `--tls-cert-file` mismatch, document it in the Dev Agent Record and rely on the build + structural tests.

## Dev Notes

### The helper you are calling (pin params against this — `Hexalith.Memories/src/Hexalith.Memories.Aspire/HexalithMemoriesServerExtensions.cs`)

`AddHexalithMemoriesSearchIndexServer(this IDistributedApplicationBuilder builder, IResourceBuilder<IDaprComponentResource> stateStore, IResourceBuilder<IDaprComponentResource> pubSub, string secretStoreComponentPath, string llmComponentPath, string? redisConnectionString = null, string eventStoreTopic = "memories-events", string serverName = "memories", int daprHttpPort = 3502, int daprGrpcPort = 50002, string? daprPlacementHostAddress = null, string? daprSchedulerHostAddress = null)` → `HexalithMemoriesSearchIndexServerResources(Server, FalkorDb, SecretStore, Llm)`.

What the helper creates internally (you do not — and must not — re-create any of these):
- `memories-secretstore` Dapr component (`secretstores.local.file`, `LocalPath = secretStoreComponentPath`).
- `memories-llm` Dapr component (`conversation.echo`, `LocalPath = llmComponentPath`).
- `memories-graphs` container (`falkordb/falkordb`, endpoint `falkordb` on 6379).
- `memories-vectors` container (`redis/redis-stack`, endpoint `redis` on 6379) — created only because you pass no `redisConnectionString` (leave it null; Folders owns no Redis Stack dependency).
- the `memories` project (`MemoriesServerProjectMetadata`, **SuppressBuild = true**, launch profile `http`) with a Dapr sidecar on `DaprHttpPort 3502`/`DaprGrpcPort 50002` that `.WithReference(stateStore).WithReference(pubSub).WithReference(secretStore).WithReference(llm)`, plus `ConnectionStrings__falkordb`/`ConnectionStrings__redis` and `MEMORIES_EVENTSTORE_TOPIC = "memories-events"` env, and `WaitFor` on the containers + components.

Cross-repo project resolution (`RepositoryProjectPaths.GetRepositoryRoot()`): the `memories` project path is computed at runtime as **five levels up** from `AppContext.BaseDirectory` (the standard `<repo>/src/<Module>.AppHost/bin/<config>/<tfm>/` layout) → `Hexalith.Memories/src/Hexalith.Memories.Server/Hexalith.Memories.Server.csproj`. The same five-up rule resolves correctly from a test assembly's `bin/<config>/<tfm>/`, so the structural test in Task 5 composes the helper without building the Memories project (SuppressBuild + registration-only inspection).

### Current AppHost composition (what you are extending — `src/Hexalith.Folders.AppHost/Program.cs`)

Post-9.1, `Program.cs` composes EventStore gateway-only (`AddHexalithEventStoreGatewayProject` + `AddHexalithEventStore(eventStoreProject, adminServer: null, adminUI: null, …, stateStoreComponentPath, pubSubComponentPath)` → `HexalithEventStoreResources eventStoreResources`), Tenants (`AddHexalithTenantsServer(eventStoreResources, accessControlConfigPath, FoldersAspireModule.TenantsAppId)`), then `folders`/`folders-workers`/`folders-ui` via `AddHexalithFolders(eventStoreResources.StateStore, eventStoreResources.PubSub, eventStore, tenants, folders, foldersWorkers, foldersUi, accessControlConfigPath)`, then a Keycloak/JWT block. `ResolveDaprConfigPath(appHostDirectory, fileName)` resolves `DaprComponents/<file>` under the AppHost dir (fallback CWD, else `FileNotFoundException`). **Reuse `eventStoreResources.StateStore`/`.PubSub` for Memories** — they are the exact shared component instances.

### Reference pattern — canonical Tenants AppHost (`Hexalith.Tenants/src/Hexalith.Tenants.AppHost/Program.cs:104-126`)

The Tenants AppHost is your composition reference. It resolves `secretstore.memories.yaml`/`llm.memories.yaml`, calls `AddHexalithMemoriesSearchIndexServer(eventStoreResources.StateStore, eventStoreResources.PubSub, memoriesSecretStorePath, memoriesLlmConfigPath, serverName: "memories", daprPlacementHostAddress: …, daprSchedulerHostAddress: …)`, then **applies routing** on `memories.Server` (`EventStoreIntegration__Routing__SourceToTenantMap__hexalith-tenants = tenants-index` + `AutoProvisionRoutedTenants = true`). **For Folders 9.2, copy the composition call but OMIT the routing chain** (that is the 9.3 analog) and omit the placement/scheduler args (the Folders AppHost does not thread `Dapr:PlacementHostAddress`/`SchedulerHostAddress` for any service — keep parity; do not introduce them for `memories` alone). The Tenants AppHost also wires `tenants-ui`/`sample` to reference `memories` — **Folders has no such consumer in 9.2; do not add one.**

### Dapr component scoping (the easy-to-miss correctness requirement)

A Dapr component `scopes:` field is a hard allow-list. The Memories sidecar reuses the shared `statestore`/`pubsub` (the helper calls `.WithReference(stateStore).WithReference(pubSub)` on the `memories` sidecar), so `memories` **must** be in both components' `scopes:` or daprd denies the Memories sidecar access at boot. The Tenants AppHost does exactly this (`statestore.yaml` scopes `[eventstore, tenants, eventstore-admin, memories]`; `pubsub.yaml` scopes `[eventstore, sample, memories]`). The `memories-secretstore`/`memories-llm` component YAMLs carry **no** `scopes:` (global), matching Tenants — leave them unscoped.

### Production policy conformance machinery (AC6 detail)

`DaprPolicyConformanceTests` derives every expected set from the closed `StableAppIds` array. Adding `memories` to it forces, and is satisfied by, exactly:
- a sixth deny-by-default Configuration doc (`ProductionAccessControlPolicyShouldBeDenyByDefault…` asserts `policy.Targets … == StableAppIds`),
- a sixth sidecar binding (`ProductionSidecarBindingsShouldAttachEveryAppToItsAccessControlConfiguration` asserts `documents.Length == StableAppIds.Length` and binding app-ids `== StableAppIds`),
- `memories=` (empty) in both pub/sub scope strings (`ProductionPubSubComponentShouldConstrainTenantEventTopicScopes` asserts scope keys `== StableAppIds` and that non-tenant/non-folders apps have empty scopes),
- a regenerated `semanticSha256` (printed by the failing test; the policy-without-allow-rules approach means **no** `cases` change).

Because `memories` has `policies: []` (no allowed callers), it is genuinely deny-by-default — nothing may invoke it until Epic 10 adds the producer + its scoped caller policies + the 7-category negative-test rows. This is the correct, honest posture for a topology-only story; do not invent speculative `folders → memories` allow-rules the negative suite cannot meaningfully exercise.

### Guardrails the change must not break

- **App-ID stability** (architecture I-4, `DaprPolicyConformanceTests`, `ContainerImageConformanceTests`): `eventstore`, `tenants`, `folders`, `folders-workers`, `folders-ui` keep their exact values; `memories` is **added**, never substituted.
- **Behavior-preserving for folders/workers/ui:** their sidecars, references, `WaitFor` edges, and JWT/Keycloak wiring are untouched.
- **No Folders-owned `memories` image** (cross-repo SuppressBuild) — keeps `ContainerImageConformanceTests` (closed 3-image set) green.
- **System working end-to-end:** `aspire run` must stay healthy. Routing is dormant (9.3) and ingestion is gated (Epic 10), so hosting `memories` adds resources but changes no existing flow.
- **Submodule policy (`CLAUDE.md`/project-context):** root-level submodules only; never `--init --recursive`. `Hexalith.Memories` is already a root submodule and physically present — no submodule pointer change is needed (unlike 9.1, this story makes **no** cross-submodule code change).

### Versions (do NOT reconcile here)

`Directory.Packages.props` pins Aspire `13.4.6`, `CommunityToolkit.Aspire.Hosting.Dapr 13.4.0-preview.1.260602-0230` (aligned by 9.1), MessagePack `2.5.302`. The Memories submodule pins the **same** CommunityToolkit version, so 9.1's NU1605 is not expected to recur. `Hexalith.Memories.Aspire` builds under the Memories repo's own central package management (MessagePack `3.1.7`), isolated from Folders' graph (same cross-repo pattern as EventStore.Aspire/Tenants.Aspire). If a transitive-downgrade `NU1605` does surface on restore, resolve it by aligning the Folders central pin (user-approved), exactly as 9.1 did — do not bump versions speculatively.

### Previous Story Intelligence (9.1 — `9-1-adopt-eventstore-and-tenants-platform-aspire-helpers.md`, status done)

- 9.1 established the platform-helper composition, the checked-in `statestore.yaml`/`pubsub.yaml`/`resiliency.yaml`, the gateway-only EventStore decision, and the `AppHostPlatformCompositionConformanceTests` guardrail. 9.2 builds directly on that AppHost.
- 9.1 needed a cross-submodule EventStore.Aspire change (Option A) for gateway-only support. **9.2 needs none** — `AddHexalithMemoriesSearchIndexServer` already exists and supports everything required.
- 9.1 added a test-only `Hexalith.EventStore.Aspire` ref to `Hexalith.Folders.IntegrationTests.csproj` to drive the composition in tests — **mirror this** with a test-only `Hexalith.Memories.Aspire` ref (Task 5).
- 9.1's `aspire run` boot was blocked by an **environment-wide** Aspire CLI 13.4.5 / DCP `--tls-cert-file` mismatch (reproduces for any Aspire app here; not a topology bug). Expect the same; final live-boot sign-off belongs in a DCP-capable env / CI. Composition correctness was proven by build + structural/conformance tests — do the same.
- 9.1's review confirmed the production-policy + conformance tests are strict and closed-set; treat them as the highest-risk surface (Critical Implementation Notes above).
- `cbf0db3` pre-applied the architecture.md / project-context.md `memories` doc text (no `src` change). Do not re-edit those docs (9.3 owns doc consistency).

### Git Intelligence (recent topology-relevant work)

- `8ffaa13 feat(story-9.1): Adopt EventStore and Tenants platform Aspire helpers` — the immediate predecessor and the foundation for this story (current `HEAD`).
- `cbf0db3 feat: Align AppHost with platform helpers and integrate Memories search-index` — **docs/planning only** (architecture.md, project-context.md, epics.md, sprint-status, change-proposals); no `src`. The `memories` app-id + topology doc text is already in place.
- Epics 8.x commits (REST routes / a11y / C3) are unrelated to AppHost composition.

### Project Structure Notes

Files this story touches (all in `Hexalith.Folders`; **no** submodule changes):
- `Directory.Build.props` (add `HexalithMemoriesRoot`).
- `src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj` (add `Hexalith.Memories.Aspire` ref).
- `src/Hexalith.Folders.AppHost/Program.cs` (compose Memories).
- `src/Hexalith.Folders.AppHost/DaprComponents/{secretstore.memories.yaml, llm.memories.yaml, secrets.json}` (new), `statestore.yaml` + `pubsub.yaml` (add `memories` scope).
- `src/Hexalith.Folders.Aspire/FoldersAspireModule.cs` (add `MemoriesAppId`).
- `tests/Hexalith.Folders.Contracts.Tests/Deployment/AppHostPlatformCompositionConformanceTests.cs` (move `memories` forbidden→expected).
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/DaprPolicyConformanceTests.cs` (add `memories` to `StableAppIds`).
- `tests/Hexalith.Folders.IntegrationTests/AspireTopologyTests.cs` (+ const assertion + new Memories test) and `…/Hexalith.Folders.IntegrationTests.csproj` (test-only Memories.Aspire ref).
- `deploy/dapr/production/{accesscontrol.yaml, sidecar-config-bindings.yaml, pubsub.yaml}` (register `memories` deny-by-default).
- `tests/fixtures/dapr-policy-conformance.yaml` (regenerate hash + add `memories` targetPolicy).

`Hexalith.Folders.slnx` already includes the AppHost + Aspire + the affected test projects; no solution change is needed (the Memories.Aspire ref is cross-repo, not added to the solution). `Hexalith.Folders.IntegrationTests` hosts `AspireTopologyTests` (composes via `DistributedApplication.CreateBuilder()`); `Hexalith.Folders.Contracts.Tests` hosts the Dapr-policy + container-image + platform-composition conformance suites; `AppHostBootSmokeTests` lives in `Hexalith.Folders.UI.Tests` and is a UI composition-root test (topology-agnostic).

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 9.2 (lines 1834-1845); Epic 9 (1815-1819); AR-INFRA-01/02 (328-329)]
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-06-22-apphost-memories-platform-alignment.md#Section 4.1 (9-2 bullet), 4.2 (I-3/I-4), 4.4-4.5 (code/tests), Section 5 success criteria]
- [Source: _bmad-output/planning-artifacts/architecture.md#AppHost Composition (§396-399); I-3 (§561); I-4 (§562); §756; Memories integration track (§130-156)]
- [Source: _bmad-output/implementation-artifacts/9-1-adopt-eventstore-and-tenants-platform-aspire-helpers.md (predecessor; test-only .Aspire ref pattern; gateway-only; boot/DCP note; NU1605 resolution)]
- [Source: Hexalith.Memories/src/Hexalith.Memories.Aspire/HexalithMemoriesServerExtensions.cs:70-166 (helper signature + created resources); MemoriesServerProjectMetadata.cs (SuppressBuild); RepositoryProjectPaths.cs:30-39 (five-up resolution); README.md (usage)]
- [Source: Hexalith.Tenants/src/Hexalith.Tenants.AppHost/Program.cs:104-126 (Memories composition reference + the routing chain to OMIT); DaprComponents/{secretstore.memories.yaml, llm.memories.yaml, secrets.json, statestore.yaml, pubsub.yaml} (adapt/scope reference)]
- [Source: src/Hexalith.Folders.AppHost/Program.cs (current composition); .../Hexalith.Folders.AppHost.csproj:17-18 (.Aspire refs); .../DaprComponents/{statestore.yaml:32-37, pubsub.yaml:27-32, accesscontrol.yaml, resiliency.yaml}; src/Hexalith.Folders.Aspire/FoldersAspireModule.cs:10-22; Directory.Build.props:4-7; Directory.Packages.props:12,22,71]
- [Source: tests/Hexalith.Folders.Contracts.Tests/Deployment/AppHostPlatformCompositionConformanceTests.cs:29-40,128-137 (ForbiddenScopes/ExpectedFoldersScopes)]
- [Source: tests/Hexalith.Folders.Contracts.Tests/OpenApi/DaprPolicyConformanceTests.cs:28-35 (StableAppIds), 37-88 (deny-by-default + semanticSha256), 124-168 (cases), 181-234 (sidecar bindings + pub/sub scopes)]
- [Source: tests/Hexalith.Folders.IntegrationTests/AspireTopologyTests.cs:18-28 (constants test), 104-150 (closed gateway-only test — leave unchanged), 213-268 (BuildGatewayOnlyComposition + RepositoryPath helpers)]
- [Source: deploy/dapr/production/{accesscontrol.yaml, sidecar-config-bindings.yaml, pubsub.yaml}; tests/fixtures/dapr-policy-conformance.yaml; tests/tools/run-dapr-policy-conformance-gates.ps1]
- [Source: _bmad-output/project-context.md (app-IDs incl. memories line 71; platform-helper topology rule line 73; LF/CRLF rule line 105; Aspire-restart rule line 130)]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.8 (claude-opus-4-8[1m]) — BMAD dev-story workflow.

### Debug Log References

- `dotnet build Hexalith.Folders.slnx --configuration Debug` → **Build succeeded, 0 Warning(s), 0 Error(s)** (AC7 0/0). The Memories.Aspire cross-repo reference resolved via the new `HexalithMemoriesRoot` probe. (First attempt failed transiently with an MSB3026 / `GenerateDepsFile` file-in-use error caused by the concurrently-running VS Code C# Dev Kit build host locking `bin`/`obj`; a clean retry succeeded — not a code defect.)
- `dotnet test tests/Hexalith.Folders.Contracts.Tests` (filtered `DaprPolicyConformance`) → printed the regenerated semantic hash `3619cdd96bde2f73ff46a9058ab648aa74490bb0c16873796b5ab8a5aa6329e3` (only the provenance-hash assertion failed pre-update; the deny-by-default, 6-doc sidecar-binding, and pub/sub-scope assertions already accepted `memories`). Copied the hash into `tests/fixtures/dapr-policy-conformance.yaml`.
- `dotnet test tests/Hexalith.Folders.Contracts.Tests` (full) → **274/274 passed** (incl. `DaprPolicyConformanceTests`, `AppHostPlatformCompositionConformanceTests`, `ContainerImageConformanceTests`; the count rose from the pre-9.2 baseline because six new guard tests were added — five Memories-component/artifact tests + the standalone-compose check in `AppHostPlatformCompositionConformanceTests`, and `MemoriesShouldRemainDenyByDefault…` in `DaprPolicyConformanceTests`).
- `dotnet test tests/Hexalith.Folders.IntegrationTests` (full) → **608/608 passed** (incl. the updated constants test + the three new Memories-composition structural tests — `AddHexalithMemoriesSearchIndexServerShouldRegisterMemoriesSidecarComponentsAndContainers`, `…ShouldReuseSharedStateStoreAndPubSubWithoutCreatingCopies`, and `ComposingMemoriesAlongsideFoldersShouldRemainAdditiveWithStandaloneMemoriesSidecar`; the closed gateway-only `GatewayOnlyCompositionShouldNotProduceAnyEventStoreAdminResources` stayed green untouched).
- `dotnet test tests/Hexalith.Folders.UI.Tests` (full) → **521/521 passed** (incl. `AppHostBootSmokeTests` — no edit required).
- `aspire run` (bounded) → AppHost composed, but the launch died in the DCP orchestration layer (`PartiallyFailedToTrustTheCertificate` → `DcpExecutor.CreateDcpObjectsAsync` / `KubernetesService` → "JSON-RPC connection with the remote party was lost"). This is the **environment-wide Aspire CLI 13.4.5 / DCP certificate-trust blocker documented in Story 9.1**, reproducing for any Aspire app here — **not** a topology defect. Composition correctness is proven by the build + structural/conformance tests, exactly as 9.1 did. Final live-boot sign-off belongs in a DCP-capable env / CI. (Removed the CLI-generated `aspire.config.json` artifact afterwards.)

### Completion Notes List

Topology-only, purely-additive story — no cross-submodule change required (`Hexalith.Memories.Aspire` already complete). All 7 ACs satisfied:

- **AC1/AC4 (compose Memories):** `Program.cs` resolves `secretstore.memories.yaml`/`llm.memories.yaml` via the existing fail-fast `ResolveDaprConfigPath(builder.AppHostDirectory, …)` and calls `builder.AddHexalithMemoriesSearchIndexServer(eventStoreResources.StateStore, eventStoreResources.PubSub, …, serverName: FoldersAspireModule.MemoriesAppId)`, reusing the **same** shared component instances. No `EventStoreIntegration__Routing__*` env vars (deferred to 9.3); `memories` is not `.WithReference`/`.WaitFor`'d by folders/workers/ui and is not JWT-wired (deferred to Epic 10); result left as a discard (`_ =`, matching the existing `AddHexalithFolders` idiom) since 9.2 has no consumer.
- **AC2 (build wiring):** `Directory.Build.props` resolves `HexalithMemoriesRoot` sibling-first then parent (probing `Hexalith.Memories\src\Hexalith.Memories.Contracts`); the AppHost csproj adds the `Hexalith.Memories.Aspire` ref with `IsAspireProjectResource="false"`. No Folders-owned `memories` container/runtime project; no `Projects.Hexalith_Memories*` usage.
- **AC3 (component files + re-scope):** added `secretstore.memories.yaml` (`name: secretstore`, `secretstores.local.file`, `secretsFile: DaprComponents/secrets.json`), `llm.memories.yaml` (`name: llm`, `conversation.echo`), and `secrets.json` (`{}`), all LF. Added `memories` to the `scopes:` of both shared `statestore.yaml` and `pubsub.yaml` (now `[eventstore, tenants, folders, folders-workers, folders-ui, memories]`) and fixed the scoping header comments; `memories-secretstore`/`memories-llm` stay unscoped (global); `resiliency.yaml` unchanged.
- **AC5 (local + structural tests):** moved `memories` from `ForbiddenScopes` into `ExpectedFoldersScopes` (+ comment) in `AppHostPlatformCompositionConformanceTests`; added the `MemoriesAppId.ShouldBe("memories")` constant assertion + the new Memories-composition structural test in `AspireTopologyTests`; added the test-only `Hexalith.Memories.Aspire` ref to the IntegrationTests csproj.
- **AC6 (deny-by-default production):** added `MemoriesAppId` to `StableAppIds`; appended the sixth deny-by-default Configuration doc (`…-accesscontrol-memories`, `policies: []`, `kubernetes` secret scope deny/`[]`), the sixth sidecar binding (`hexalith-memories` → `…-accesscontrol-memories`), and `;memories=` (empty) to both production pub/sub scope strings; added `- targetAppId: memories / allowRules: []` to the fixture and regenerated the semantic hash. **No** new invoke allow-rules / conformance `cases` (Epic 10). `ContainerImageConformanceTests` + `AppHostBootSmokeTests` unchanged and green.
- **AC7 (build + suites + boot):** build 0/0 (one transient MSB3026 `bin`/`obj` file-lock warning surfaces while the VS Code C# Dev Kit build host is running concurrently — environmental, clears on a clean retry, not a code warning); the three narrowed suites green (274/608/521); `aspire run` blocked only by the documented env DCP/TLS blocker.
- **Did not touch** `project-context.md` / `architecture.md` (9.3 scope, pre-applied by `cbf0db3`); made **no** cross-submodule change.

### Change Log

- 2026-06-23 — Story 9.2 implemented: hosted the Memories search-index server in the Folders AppHost topology (build wiring, `MemoriesAppId` constant, checked-in Memories Dapr component files + shared-component re-scoping, AppHost composition, local/structural/production conformance updates). Build 0/0; Contracts.Tests 274/274, IntegrationTests 608/608, UI.Tests 521/521. `aspire run` blocked by the env-wide DCP certificate-trust mismatch documented in 9.1 (not a topology defect). Status → review.
- 2026-06-23 — Senior Developer Review (AI, jpiquot): adversarial review passed — all 7 ACs verified IMPLEMENTED against code + a re-run green build and the three narrowed suites (Contracts.Tests 274/274, IntegrationTests 608/608, UI.Tests 521/521). No CRITICAL/HIGH/MEDIUM findings. Auto-fixed three LOW documentation-accuracy issues: corrected the stale test counts (268→274, 606→608), enriched the under-described test-file File List entries, and documented the transient MSB3026 build warning. Status → done.

### File List

**Modified (source/build):**
- `Directory.Build.props` — added `HexalithMemoriesRoot` sibling+parent probes.
- `src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj` — added the `Hexalith.Memories.Aspire` project reference (`IsAspireProjectResource="false"`).
- `src/Hexalith.Folders.AppHost/Program.cs` — composed the Memories search-index server (using + path resolution + `AddHexalithMemoriesSearchIndexServer`).
- `src/Hexalith.Folders.Aspire/FoldersAspireModule.cs` — added `MemoriesAppId = "memories"`.
- `src/Hexalith.Folders.AppHost/DaprComponents/statestore.yaml` — added `memories` scope + comment.
- `src/Hexalith.Folders.AppHost/DaprComponents/pubsub.yaml` — added `memories` scope + comment.

**New (Dapr component artifacts):**
- `src/Hexalith.Folders.AppHost/DaprComponents/secretstore.memories.yaml`
- `src/Hexalith.Folders.AppHost/DaprComponents/llm.memories.yaml`
- `src/Hexalith.Folders.AppHost/DaprComponents/secrets.json`

**Modified (tests):**
- `tests/Hexalith.Folders.Contracts.Tests/Deployment/AppHostPlatformCompositionConformanceTests.cs` — moved `memories` forbidden→expected scope; added five new guard tests (standalone-compose-without-routing, secret-store/llm YAML shape, secrets.json empty-object, LF-line-endings) + a `System.Text.Json` import + `AssertUnscoped` helper.
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/DaprPolicyConformanceTests.cs` — added `MemoriesAppId` to `StableAppIds`; added the `MemoriesShouldRemainDenyByDefaultWithNoInvokeAuthorizationOrPubSubTopicsUntilEpic10` guard test.
- `tests/Hexalith.Folders.IntegrationTests/AspireTopologyTests.cs` — added the constant assertion + three new Memories-composition structural tests (register sidecar/components/containers; reuse-shared-statestore/pubsub; additive-standalone-sidecar alongside Folders) + `CountComponentsNamed`/`ContainerImage` helpers.
- `tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj` — added the test-only `Hexalith.Memories.Aspire` reference.
- `tests/fixtures/dapr-policy-conformance.yaml` — added the `memories` targetPolicy + regenerated `semanticSha256`.

**Modified (production Dapr deploy):**
- `deploy/dapr/production/accesscontrol.yaml` — added the deny-by-default `…-accesscontrol-memories` Configuration doc.
- `deploy/dapr/production/sidecar-config-bindings.yaml` — added the `hexalith-memories` sidecar binding.
- `deploy/dapr/production/pubsub.yaml` — added `;memories=` (empty) to both publishing/subscription scopes.

## Senior Developer Review (AI)

**Reviewer:** jpiquot — **Date:** 2026-06-23 — **Outcome:** ✅ Approve (status → done)

### Scope & method

Adversarial review per the story-automator review workflow. Cross-referenced the story's File List against `git status`/`git diff` reality, validated every Acceptance Criterion and `[x]` task against the actual implementation, then re-ran the build and the three narrowed suites to verify the green claims (not just trust them).

### Git vs Story File List

No discrepancies. Every source/test/deploy file the dev claimed maps to a real git change, and every changed source/test/deploy file is documented. The only extra working-tree changes are under `_bmad-output/` (story/sprint/orchestration artifacts), correctly excluded from review.

### Acceptance Criteria — all IMPLEMENTED

- **AC1** ✅ `Program.cs` resolves `secretstore.memories.yaml`/`llm.memories.yaml` via `ResolveDaprConfigPath(builder.AppHostDirectory, …)` and calls `AddHexalithMemoriesSearchIndexServer(eventStoreResources.StateStore, eventStoreResources.PubSub, …, serverName: FoldersAspireModule.MemoriesAppId)` — reusing the shared component instances, no `EventStoreIntegration__Routing__*`, composed outside the Keycloak block, left as a discard. Reuse + standalone-sidecar asserted by two new integration tests.
- **AC2** ✅ `Directory.Build.props` resolves `HexalithMemoriesRoot` sibling-then-parent; AppHost csproj adds the `Hexalith.Memories.Aspire` ref with `IsAspireProjectResource="false"`; no Folders-owned `memories` runtime project; no `Projects.Hexalith_Memories*` usage.
- **AC3** ✅ `secretstore.memories.yaml` (`secretstore`/`secretstores.local.file`/`secretsFile: DaprComponents/secrets.json`), `llm.memories.yaml` (`llm`/`conversation.echo`), `secrets.json` (`{}`) — all LF-verified. `memories` added to both `statestore.yaml`/`pubsub.yaml` `scopes:` with corrected header comments; `memories-secretstore`/`memories-llm` stay unscoped.
- **AC4** ✅ `MemoriesAppId = "memories"` constant added; new structural test pins the `memories` sidecar (HTTP 3502 / gRPC 50002), the two components, and the `redis/redis-stack` + `falkordb/falkordb` containers.
- **AC5** ✅ `memories` moved `ForbiddenScopes`→`ExpectedFoldersScopes`; constant assertion + new Memories tests added; test-only `Hexalith.Memories.Aspire` ref added to the IntegrationTests csproj; closed gateway-only test left unchanged.
- **AC6** ✅ `MemoriesAppId` added to `StableAppIds`; sixth deny-by-default access-control doc (structurally identical to the `tenants` sibling), sixth `hexalith-memories` sidecar binding, `;memories=` (empty) in both production pub/sub scope strings, `memories` targetPolicy + regenerated `semanticSha256` — provenance hash verified by the passing `ProductionAccessControlPolicyShouldBeDenyByDefault…` test. No invoke allow-rules / `cases` added.
- **AC7** ✅ Re-verified locally: `dotnet build Hexalith.Folders.slnx` → 0 errors; Contracts.Tests **274/274**, IntegrationTests **608/608**, UI.Tests **521/521**. `aspire run` not exercised — same env-wide DCP/TLS blocker as 9.1.

### Findings (all LOW — auto-fixed)

1. **[LOW][Docs] Stale test counts** — Dev Agent Record reported 268/606/521; actual is 274/608/521 (the dev added six Contracts guard tests and three Integration tests beyond the minimum and under-counted them). Reality is strictly better; counts corrected in Debug Log, Completion Notes, and Change Log.
2. **[LOW][Docs] Under-described test-file File List entries** — the two test files were listed but their descriptions omitted the added test methods/helpers/import. Enriched.
3. **[LOW][Env] Transient MSB3026 warning** — a `bin`/`obj` file-lock warning surfaces while the VS Code build host runs concurrently (clears on clean retry); documented against the "0 Warning(s)" claim. Not a code defect.

No HIGH/MEDIUM/CRITICAL issues. The implementation is correct, complete, behavior-preserving for `folders`/`folders-workers`/`folders-ui`, and the conformance guardrails (closed-set scopes, deny-by-default policy, gateway-only no-admin invariant) all hold. The dev exceeded the story's test requirements with strong additive coverage.
