# Test Automation Summary — Story 9.1 (Adopt EventStore & Tenants platform Aspire helpers)

**Workflow:** `bmad-qa-generate-e2e-tests`
**Date:** 2026-06-23
**Story:** `_bmad-output/implementation-artifacts/9-1-adopt-eventstore-and-tenants-platform-aspire-helpers.md`
**Test framework:** xUnit v3 + Shouldly + YamlDotNet (existing project stack — no new framework introduced)
**Mode:** Auto-apply all discovered gaps.

## Scope note

Story 9.1 is a **behavior-preserving AppHost/Aspire topology refactor** (compose EventStore gateway-only +
Tenants via the platform helpers; move shared Dapr components to checked-in YAML). It adds **no REST API and no
UI** — `folders-ui` is preserved unchanged. The relevant test surface is therefore the **Aspire composition
contract** and the **Dapr component artifacts**, not HTTP endpoints or Playwright flows. Tests were generated
against the six Acceptance Criteria to fill the coverage gaps the existing suites left open.

## Coverage gap analysis (what existing tests did NOT cover)

| AC | Requirement | Pre-existing coverage | Gap filled |
| --- | --- | --- | --- |
| AC1 | Gateway-only EventStore — **no** `eventstore-admin` / `eventstore-admin-ui` resource | `AspireTopologyTests` built the gateway-only composition but **never asserted** the absence of admin resources (the `adminServer: null` arg was an input, not a checked outcome) | ✅ new topology test |
| AC3 | Checked-in `statestore.yaml` / `pubsub.yaml` / `resiliency.yaml`, scoped to the 5 Folders app-ids, preserving `actorStateStore`/`redisHost`/`keyPrefix: none` | The three net-new local-dev component YAMLs had **zero** test references; only `deploy/dapr/production/*` and the local `accesscontrol.yaml` were validated | ✅ 3 new YAML conformance tests |
| AC2 | AppHost csproj references the `.Aspire` helpers (not runtime projects); `Projects.Hexalith_EventStore`/`_Tenants` no longer used in `Program.cs` | Functionally enforced only by a successful build; **no test** guarded against re-adding the runtime refs | ✅ 2 new csproj/program shape tests |
| AC4/AC5 | Folders services unchanged; 7 constants + `HexalithFoldersResources` shape; sidecars correct | Already covered by the 4 updated `AspireTopologyTests` | No gap — left as-is |
| AC6 | Build + boot | Build green in CI; live `aspire run` boot blocked by an environmental Aspire CLI/DCP `--tls-cert-file` mismatch (documented in the story, not a topology defect) | Not automatable here (env-gated) |

## Generated tests

### Topology composition (AC1)

- [x] `tests/Hexalith.Folders.IntegrationTests/AspireTopologyTests.cs` →
  `GatewayOnlyCompositionShouldNotProduceAnyEventStoreAdminResources`
  - Asserts `HexalithEventStoreResources.AdminServer` and `AdminUI` are **null** (gateway-only record).
  - Asserts the composed Dapr sidecar app-ids are **exactly** the five stable production app-ids
    (`eventstore`, `folders`, `folders-ui`, `folders-workers`, `tenants`) — and explicitly **not**
    `eventstore-admin` / `eventstore-admin-ui`.

### Dapr component & AppHost conformance (AC2, AC3)

New file: `tests/Hexalith.Folders.Contracts.Tests/Deployment/AppHostPlatformCompositionConformanceTests.cs`
(reuses the existing `ContainerImageYamlNodeExtensions` YAML helpers already in the `Deployment` namespace —
no duplicate helper introduced).

- [x] `LocalStateStoreComponentShouldPreserveRedisActorSemanticsAndFoldersScopes` (AC3) — `kind: Component`,
  `name: statestore`, `type: state.redis`, `version: v1`; metadata `redisHost=localhost:6379`,
  `actorStateStore=true`, **`keyPrefix=none`**; scopes == the 5 Folders app-ids; **no** `eventstore-admin` /
  `eventstore-admin-ui` / `memories` / `sample` scope.
- [x] `LocalPubSubComponentShouldBeRedisAndScopedToFoldersTopology` (AC3) — `kind: Component`, `name: pubsub`,
  `type: pubsub.redis`, `version: v1`; scopes == the 5 Folders app-ids; no forbidden scopes.
- [x] `LocalResiliencyComponentShouldTargetEventStoreAppAndSharedComponentsWithoutScopes` (AC3) —
  `kind: Resiliency`, `name: resiliency`; **no `scopes:` field** (Resiliency CRD is not app-scoped); targets the
  `eventstore` app and the `statestore` + `pubsub` components.
- [x] `AppHostProjectShouldReferencePlatformAspireHelpersAndNotRuntimeProjects` (AC2) — csproj references
  `Hexalith.EventStore.Aspire` + `Hexalith.Tenants.Aspire`; the two direct runtime project refs are gone.
- [x] `AppHostProgramShouldNotUseGeneratedEventStoreOrTenantsProjectMetadata` (AC2) — `Program.cs` no longer
  uses `Projects.Hexalith_EventStore` / `Projects.Hexalith_Tenants`.

## Coverage

- **Acceptance Criteria:** 6/6 considered. AC1, AC2, AC3 newly automated (6 tests). AC4/AC5 already covered.
  AC6 build half is green in CI; live-boot half remains environment-gated (documented, not automatable here).
- **New tests added:** 6 (1 topology + 5 conformance).
- **Net-new artifacts now under test:** `statestore.yaml`, `pubsub.yaml`, `resiliency.yaml` (previously 0 coverage).

## Validation (2026-06-23)

| Suite | Result |
| --- | --- |
| `AspireTopologyTests` (filtered) | 5 passed / 0 failed |
| `AppHostPlatformCompositionConformanceTests` (filtered) | 5 passed / 0 failed |
| **Full** `Hexalith.Folders.IntegrationTests` | **604 passed / 0 failed** (baseline 603 → +1) |
| **Full** `Hexalith.Folders.Contracts.Tests` | **268 passed / 0 failed** (baseline 263 → +5) |
| `dotnet format whitespace --verify-no-changes` (new/edited files) | clean (LF endings, no CRLF) |

All generated tests pass; no regressions in the affected projects.

## Checklist disposition (`checklist.md`)

- API tests generated — N/A (infrastructure refactor; no REST surface). The composition/component contract is
  the analogue and is covered.
- E2E tests generated — N/A (no new UI).
- Standard framework APIs — ✅ xUnit v3 + Shouldly + YamlDotNet, matching existing patterns.
- Happy path covered — ✅ exact 5-sidecar topology + correct YAML semantics.
- 1–2 critical error/guard cases — ✅ no admin resource, no forbidden scope, no runtime project ref (regression guards).
- All tests run successfully — ✅ (see Validation).
- Proper/semantic locators — ✅ `FoldersAspireModule` constants + YAML node keys (not positional/string indices).
- Clear descriptions — ✅ AC-referencing names + comments.
- No hardcoded waits/sleeps — ✅ none.
- Independent / no order dependency — ✅ each test builds its own composition / reads files independently.

## Next steps

- Runs in CI via the existing contract-spine / integration lanes — no new gate script required (both tests live
  in already-wired test projects).
- Live `aspire run` boot sign-off (AC6) should be re-run in a DCP-capable environment / CI once the Aspire CLI is
  aligned to 13.4.6 (per the story's Dev Agent Record).
