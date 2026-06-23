# Test Automation Summary

> Canonical latest-run summary for Story 9.1. Durable per-story copy: [`9-1-test-summary.md`](./9-1-test-summary.md).
> Previous run (Story 8.5): [`8-5-test-summary.md`](./8-5-test-summary.md).

**Workflow:** `bmad-qa-generate-e2e-tests`
**Date:** 2026-06-23
**Story:** `_bmad-output/implementation-artifacts/9-1-adopt-eventstore-and-tenants-platform-aspire-helpers.md`
**Feature under test:** the Story 9.1 AppHost/Aspire topology refactor — EventStore composed **gateway-only** +
Tenants via the platform helpers, with the shared Dapr `statestore`/`pubsub`/`resiliency` components moved to
checked-in `DaprComponents` YAML. Behavior-preserving infra refactor: **no REST API, no UI**, so the test surface
is the Aspire composition contract + Dapr component artifacts (xUnit v3 + Shouldly + YamlDotNet). No new framework.
**Mode:** Auto-apply all discovered gaps.

## Gaps discovered & auto-applied

1. **AC1 — gateway-only never asserted.** `AspireTopologyTests` built the `adminServer: null` composition but
   never *checked* that no `eventstore-admin` / `eventstore-admin-ui` resource results. **Fix:** new
   `GatewayOnlyCompositionShouldNotProduceAnyEventStoreAdminResources` asserts `AdminServer`/`AdminUI` are null
   and the composed Dapr sidecar app-ids are exactly the five stable production app-ids (no admin app-id).
2. **AC3 — the three net-new local-dev Dapr component YAMLs had zero test coverage.** Only `deploy/dapr/production/*`
   and the local `accesscontrol.yaml` were validated. **Fix:** 3 new conformance tests pin `statestore.yaml`
   (`state.redis` + `actorStateStore=true` + `redisHost=localhost:6379` + **`keyPrefix=none`**),
   `pubsub.yaml` (`pubsub.redis`), and `resiliency.yaml` (`Resiliency`, no `scopes:`, targets `eventstore` +
   `statestore`/`pubsub`); both components are scoped to exactly the 5 Folders app-ids with **no**
   `eventstore-admin`/`memories`/`sample` scope.
3. **AC2 — the runtime→`.Aspire` ref switch was enforced only by a passing build.** **Fix:** 2 shape tests pin
   that the AppHost csproj references `Hexalith.EventStore.Aspire` + `Hexalith.Tenants.Aspire` (and the two direct
   runtime refs are gone), and that `Program.cs` no longer uses `Projects.Hexalith_EventStore`/`_Tenants`.

## Generated / extended tests

- [x] `tests/Hexalith.Folders.IntegrationTests/AspireTopologyTests.cs` — new AC1 gateway-only / no-admin test.
- [x] `tests/Hexalith.Folders.Contracts.Tests/Deployment/AppHostPlatformCompositionConformanceTests.cs` — new file:
  3 Dapr-component conformance tests (AC3) + 2 AppHost csproj/program shape tests (AC2). Reuses the existing
  `ContainerImageYamlNodeExtensions` YAML helpers (no duplicate helper).

## Coverage

- ACs newly automated: **AC1, AC2, AC3** (6 tests). AC4/AC5 already covered by the 4 updated topology tests.
- AC6 build half green in CI; live `aspire run` boot half is environment-gated (Aspire CLI/DCP `--tls-cert-file`
  mismatch, documented in the story — not a topology defect).
- Net-new artifacts now under test: `statestore.yaml`, `pubsub.yaml`, `resiliency.yaml` (previously 0 coverage).

## Validation (2026-06-23)

```
AspireTopologyTests (filtered)                      -> 5/5    passed
AppHostPlatformCompositionConformanceTests (filter) -> 5/5    passed
Hexalith.Folders.IntegrationTests (full)            -> 604/0  (baseline 603 -> +1)
Hexalith.Folders.Contracts.Tests  (full)            -> 268/0  (baseline 263 -> +5)
dotnet format whitespace --verify-no-changes        -> clean  (LF endings, no CRLF)
```

All generated tests pass; no regressions in the affected projects.

See [`9-1-test-summary.md`](./9-1-test-summary.md) for the full per-AC gap breakdown and checklist disposition.
