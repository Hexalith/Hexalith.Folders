# Test Automation Summary — Story 9.3 (Folders→Memories routing config)

**Workflow:** `bmad-qa-generate-e2e-tests` · **Date:** 2026-06-23 · **Engineer:** Jerome (QA automation pass)
**Feature under test:** the `hexalith-folders → folders-index` source→index routing on the standalone
Memories search-index server — two stable contract constants (`FoldersAspireModule.MemoriesSourceId` /
`MemoriesIndexTenant`) plus the production helper `WithFoldersMemoriesSourceRouting`, applied in
`src/Hexalith.Folders.AppHost/Program.cs`.

**Framework:** xUnit v3 `3.2.2` + Shouldly `4.3.0` (the project's existing test stack — no new framework
introduced). This story is Aspire-topology / infrastructure wiring, so coverage is **structural composition
tests**, not HTTP API or Playwright UI tests (neither applies — see *Scope* below).

## Coverage State at Start (already shipped by dev-story)

- `FoldersAspireModuleShouldExposeStableDaprAppIdsAndComponentNames` — pins the two contract constants
  (`MemoriesSourceId == "hexalith-folders"`, `MemoriesIndexTenant == "folders-index"`).
- `MemoriesServerShouldCarryFoldersToFoldersIndexSourceRoutingWhenRoutingApplied` — happy path: drives the
  production `WithFoldersMemoriesSourceRouting()` over a composed Memories server and asserts both routing
  env vars resolve (`...SourceToTenantMap__hexalith-folders == "folders-index"`,
  `...AutoProvisionRoutedTenants == "true"`) via a Publish-mode `EnvironmentCallbackContext`.

## Gaps Discovered & Auto-Applied

Both gaps were on the public surface of the `WithFoldersMemoriesSourceRouting` helper (AC2). Tests only —
**no production code changed.**

### 1. Critical error case — null-argument guard (new test)

The helper's `ArgumentNullException.ThrowIfNull(memoriesServer)` public-boundary guard had **zero coverage**;
only the happy path existed. The QA checklist requires 1–2 critical error cases.

- [x] `WithFoldersMemoriesSourceRoutingShouldThrowArgumentNullExceptionWhenServerIsNull` — asserts the helper
  throws `ArgumentNullException` with `ParamName == "memoriesServer"` for a null server resource.

### 2. Documented return contract — chaining (extended existing happy-path test)

The helper's `<returns>The same resource builder for chaining.</returns>` contract — the property the
canonical Tenants AppHost relies on to fluently chain its two `.WithEnvironment(...)` routing calls — was
never asserted.

- [x] Added `routedMemories.ShouldBeSameAs(memories.Server);` to
  `MemoriesServerShouldCarryFoldersToFoldersIndexSourceRoutingWhenRoutingApplied`.

## Generated / Modified Tests

### Structural (Aspire topology) tests — `tests/Hexalith.Folders.IntegrationTests/AspireTopologyTests.cs`

- [x] `WithFoldersMemoriesSourceRoutingShouldThrowArgumentNullExceptionWhenServerIsNull` — **new** error-case `[Fact]`.
- [x] `MemoriesServerShouldCarryFoldersToFoldersIndexSourceRoutingWhenRoutingApplied` — **extended** with the
  return-contract (`ShouldBeSameAs`) assertion.

### API tests

- N/A — Story 9.3 adds no HTTP endpoint or REST surface. Routing is a local AppHost env-var concern consumed
  server-side by the Memories router; there is no request/response path to exercise.

### E2E / UI tests

- N/A — no UI surface in this story. End-to-end ingestion/search is **gated on the Epic 10 producer**
  (`SearchIndexEntryChanged`, source `hexalith-folders`); the routing is dormant until then by design, so a
  live E2E flow is not yet exercisable (recorded in `memories-search-index-handoff-2026-06-23.md`).

## Coverage

- `WithFoldersMemoriesSourceRouting` public surface: **3/3 behaviors covered** — happy-path env-var wiring,
  null-argument guard, same-builder return contract.
- Routing contract constants (`MemoriesSourceId` / `MemoriesIndexTenant`): **2/2 pinned.**
- `AspireTopologyTests`: 10 → **11 tests**, all green.
- Full `tests/Hexalith.Folders.IntegrationTests`: 609 → **610 tests**, all green.

## Validation

```
dotnet build tests/Hexalith.Folders.IntegrationTests  → 0 Warning(s) / 0 Error(s)
dotnet test  tests/Hexalith.Folders.IntegrationTests
  → AspireTopologyTests:  Passed 11 / Failed 0 / Skipped 0
  → full suite:           Passed 610 / Failed 0 / Skipped 0
```

All generated tests pass ✅ — semantic locators N/A (no UI), no hardcoded waits/sleeps, each test composes its
own builder so there is no order dependency, descriptions are explicit, standard framework APIs only. No
production-policy / deploy artifact touched (parity with the story's deny-by-default guardrail).

## Next Steps

- Run in CI alongside the existing `Hexalith.Folders.IntegrationTests` lane.
- **Epic 10:** when the worker-side producer ships, add the end-to-end ingestion test
  (`hexalith-folders`-sourced `SearchIndexEntryChanged` → `folders-index`) that this Phase-1 routing config
  enables — the deferred verification checklist lives in
  `_bmad-output/planning-artifacts/memories-search-index-handoff-2026-06-23.md`.
</content>
</invoke>
