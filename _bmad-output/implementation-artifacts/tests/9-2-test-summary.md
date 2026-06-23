# Test Automation Summary — Story 9.2 (Memories search-index server in the AppHost topology)

**Workflow:** `bmad-qa-generate-e2e-tests` · **Engineer role:** QA automation · **Date:** 2026-06-23
**Story:** `9-2-add-memories-search-index-server-to-the-apphost-topology.md` (status: review)
**Framework:** xUnit v3 `3.2.2` + Shouldly `4.3.0` + YamlDotNet `18.0.0` (the project's existing test stack — no new framework introduced).

## Scope note

Story 9.2 is a **topology / infrastructure** story — purely additive hosting of the cross-repo Memories
search-index server in the Folders Aspire AppHost. It adds **no REST endpoint and no UI surface**, so the
"E2E tests" for this feature are **structural composition tests** (over the in-memory Aspire `DistributedApplication`
model) and **conformance tests** (over the checked-in Dapr component YAML and the production deploy artifacts).
The dev story already shipped a baseline of these; this run performed a coverage-gap analysis against the 7
acceptance criteria and **auto-applied** the discovered gaps.

## Generated / strengthened tests

### Structural topology tests — `tests/Hexalith.Folders.IntegrationTests/AspireTopologyTests.cs`
- [x] **(strengthened)** `AddHexalithMemoriesSearchIndexServerShouldRegisterMemoriesSidecarComponentsAndContainers` — added assertions that the `memories` Dapr sidecar binds the platform-stable ports **HTTP 3502 / gRPC 50002** and that the helper-owned containers carry the expected images **`redis/redis-stack`** (`memories-vectors`) and **`falkordb/falkordb`** (`memories-graphs`). *(AC4)*
- [x] **(new)** `AddHexalithMemoriesSearchIndexServerShouldReuseSharedStateStoreAndPubSubWithoutCreatingCopies` — proves the Memories server reuses the **same** shared `statestore`/`pubsub` instances (no Folders-local copies): composing the helper keeps the statestore/pubsub component counts at 1 each and adds exactly the two memories-owned components (`memories-secretstore`, `memories-llm`). *(AC1)*
- [x] **(new)** `ComposingMemoriesAlongsideFoldersShouldRemainAdditiveWithStandaloneMemoriesSidecar` — composes the full Folders topology **and** Memories, asserting the five production sidecars are preserved verbatim, `memories` is added as a standalone sixth sidecar, and **no `eventstore-admin*`** resource appears (gateway-only invariant). *(AC1, AC4)*

### Build-wiring + component-YAML conformance — `tests/Hexalith.Folders.Contracts.Tests/Deployment/AppHostPlatformCompositionConformanceTests.cs`
- [x] **(strengthened)** `AppHostProjectShouldReferencePlatformAspireHelpersAndNotRuntimeProjects` — added that the AppHost csproj references `Hexalith.Memories.Aspire` and references **no** Folders-owned Memories runtime/container project. *(AC2)*
- [x] **(new)** `AppHostProgramShouldComposeMemoriesStandaloneWithoutDeferredRoutingOrGeneratedMetadata` — `Program.cs` composes `AddHexalithMemoriesSearchIndexServer` reusing `eventStoreResources.StateStore`/`.PubSub`, uses no `Projects.Hexalith_Memories*` generated metadata, and wires **no** `EventStoreIntegration__Routing*` env vars (the 9.3 deferral boundary). *(AC1, AC2)*
- [x] **(new)** `MemoriesSecretStoreComponentYamlShouldBeUnscopedLocalFileSecretStore` — validates `secretstore.memories.yaml` (`name: secretstore`, `secretstores.local.file`, `secretsFile: DaprComponents/secrets.json`) and that it carries **no** `scopes:` (global). *(AC3)*
- [x] **(new)** `MemoriesLlmComponentYamlShouldBeUnscopedEchoConversationComponent` — validates `llm.memories.yaml` (`name: llm`, `conversation.echo`) and that it is unscoped. *(AC3)*
- [x] **(new)** `MemoriesLocalSecretsFileShouldExistAsEmptyJsonObject` — `secrets.json` exists and is an empty JSON object `{}` (required for the local.file secret store to boot — Critical Note 7). *(AC3)*
- [x] **(new)** `NewMemoriesComponentArtifactsShouldUseLfLineEndings` — the three new YAML/JSON artifacts use LF (no CR). *(AC3, project-context LF rule)*

### Production deny-by-default boundary — `tests/Hexalith.Folders.Contracts.Tests/OpenApi/DaprPolicyConformanceTests.cs`
- [x] **(new)** `MemoriesShouldRemainDenyByDefaultWithNoInvokeAuthorizationOrPubSubTopicsUntilEpic10` — `memories` is `defaultAction: deny` with **zero** caller policies and **empty** publish + subscribe topic scopes; fails closed if a speculative `memories` caller policy or topic scope is added before the Epic 10 producer exists. *(AC6)*

## Coverage

| Acceptance criterion | Coverage after this run |
| --- | --- |
| AC1 — composed via helper, reuses shared components, no routing/Epic-10 wiring | Covered (reuse test + additive test + Program.cs guards) |
| AC2 — build wiring references `.Aspire` helper, no runtime project / generated metadata | Covered (csproj + Program.cs conformance) |
| AC3 — checked-in component files + shared re-scope + secrets.json + LF | Covered (3 YAML/JSON tests + LF test; statestore/pubsub scope already covered) |
| AC4 — `MemoriesAppId` + topology resources (sidecar ports, containers, images) | Covered (constant test + ports + images + additive test) |
| AC5 — local + structural tests updated | Covered (baseline, extended here) |
| AC6 — production deny-by-default + conformance | Covered (closed-set suite + explicit Epic-10 boundary guard) |
| AC7 — build + narrowed suites green | Verified (see results) |

- Structural composition gaps (reuse / standalone / ports / images): **closed**.
- New checked-in Dapr artifacts (`secretstore.memories.yaml`, `llm.memories.yaml`, `secrets.json`): previously **0 conformance coverage** → now covered.
- Deferral boundaries (9.3 routing, Epic 10 invoke/topics): now explicit fail-closed guards.

## Results

- `dotnet build Hexalith.Folders.slnx` → **Build succeeded, 0 Warning(s), 0 Error(s)**.
- `Hexalith.Folders.IntegrationTests` → **608 passed / 0 failed** (was 606; +2 new in `AspireTopologyTests`).
- `Hexalith.Folders.Contracts.Tests` → **274 passed / 0 failed** (was 268; +6 new across the composition + policy suites).
- New tests added: **8 new** test methods + **2 strengthened** existing methods. All green.

## Test quality notes

- Standard xUnit v3 `[Fact]` + Shouldly assertions; no new abstractions or fixtures.
- All tests are hermetic and run with `submodules: false`-style constraints — no Dapr sidecar, Redis, Keycloak, or network. They inspect the in-memory Aspire model and the checked-in YAML/JSON only.
- Tests are independent (each builds its own `DistributedApplication.CreateBuilder()` or reads files fresh); no order dependency, no sleeps/waits.
- Resources are selected by stable app-id / component name / container name, not by index.

## Not covered (out of scope / deferred — intentionally not tested)

- Live `aspire run` boot of the `memories`/`memories-vectors`/`memories-graphs` resources — blocked by the environment-wide Aspire CLI / DCP `--tls-cert-file` certificate-trust mismatch documented in Story 9.1; final live-boot sign-off belongs in a DCP-capable env / CI. Composition correctness is proven by the build + structural/conformance tests.
- `folders`/`folders-workers` → `memories` invoke authorization and `memories` ingestion topics — **Epic 10** (a fail-closed guard for this boundary was added, see AC6 test above).
- Source→index routing env vars — **Story 9.3** (a fail-closed guard for this boundary was added in `AppHostProgramShouldComposeMemoriesStandaloneWithoutDeferredRoutingOrGeneratedMetadata`).
