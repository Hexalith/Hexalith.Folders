# Test Automation Summary — Story 8.1 (Bucket-A REST server routes)

_Workflow:_ `bmad-qa-generate-e2e-tests` · _QA engineer:_ Claude (for Jerome) · _Date:_ 2026-06-23
_Story:_ `_bmad-output/implementation-artifacts/8-1-implement-bucket-a-missing-rest-server-routes.md`

## Framework detected

.NET 10 / C# · **xUnit v3 `3.2.2` + Shouldly `4.3.0`** (project convention; no JS/Playwright lane for these REST routes — the Blazor console is a deferred read-only E2E lane). API/route tests in `tests/Hexalith.Folders.Server.Tests`; no-mock `/process` integration tests in `tests/Hexalith.Folders.IntegrationTests` (per project-context Testing Rules — a `RecordingEventStoreGatewayClient` short-circuit is **not** acceptance evidence for mutating commands).

## Pre-existing coverage (verified, all 8 ops)

The story shipped with strong coverage. Reads (op2/5/6/7/8): 200 contract shape, `Idempotency-Key`→400, unsupported `X-Hexalith-Freshness`→400, 404 safe-not-found, 401 safe denial (+ no-echo of ids/credentials). Mutations (op1/3/4): route shape (202, server-derived/aggregate ids, command type, schema/validation 400s, 401), plus no-mock `/process` round-trips asserting persisted state.

## Gaps discovered & auto-applied

The dev completion note claimed each mutating op had a no-mock test asserting "idempotent-replay / `idempotency_conflict` (409)". Audit of the actual tests showed **idempotency replay/conflict coverage was uneven** — a real hole against **AC2 + AC5** ("idempotency replay/conflict … hold end-to-end"):

| Mutating op | replay (no double-append) | conflict → 409 |
|---|---|---|
| `CreateFolder` (op1) | ✅ already present | ✅ already present |
| `UpdateFolderAclEntry` (op2/4) | ❌ → **added** | ❌ → **added** |
| `ConfigureProviderBinding` (op3) | ✅ already present | ❌ → **added** |

### Added — `tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs` (no-mock `/process`)

- [x] `UpdateFolderAclEntryReplayWithSameKeyShouldNotPersistTwice` — same key + equivalent payload → 202 ×2, exactly **1** event appended.
- [x] `UpdateFolderAclEntrySameKeyDifferentPayloadShouldSurfaceIdempotencyConflict` — same key, `grant` then `revoke` (different canonical fingerprint) → **409**, 1 event appended.
- [x] `ConfigureProviderBindingSameKeyDifferentPayloadShouldSurfaceIdempotencyConflict` — same key, different `nonSecretCredentialReference` → **409** (aggregate-level idempotency guard), 1 organization event appended.

All three drive the real REST → gateway → `/process` → processor → service → aggregate → repository path (no mocked `IEventStoreGatewayClient`).

## Results

```
Build: dotnet build Hexalith.Folders.IntegrationTests  →  0 warnings / 0 errors (warnings-as-errors)
New tests (filtered):                                   →  Passed 3 / Failed 0
Full project: Hexalith.Folders.IntegrationTests         →  Passed 601 / Failed 0 / Skipped 0  (was 598; +3)
```

No `src/` changed (tests-only), so `Hexalith.Folders.Server.Tests` is unaffected.

## Coverage

- Mutating-op idempotency (replay **and** conflict): **3/3 ops** (was effectively 1.5/3).
- AC2 no-mock `/process` acceptance: 3/3 mutating ops. AC5 replay/conflict end-to-end: 3/3 mutating ops.

## Findings for the dev/PM (out of QA test-generation scope — not auto-changed)

1. **Cross-surface contract drift — read-op idempotency rejection code.** `src/Hexalith.Folders.Server/ProviderReadinessEndpoints.cs` (lines 116, 243, 301, incl. op5 `GetProviderBinding`) emits `code: "idempotency_key_not_accepted"`, whereas **DD1** and every other read route (`FoldersDomainServiceEndpoints.cs`) use `idempotency_key_not_allowed`. The op5 test asserts the divergent `_not_accepted`, so it is green but locks in the inconsistency. Recommend reconciling the code to `idempotency_key_not_allowed` (touches prod code + the op5 assertion + any parity/contract fixtures) — left to the dev because it changes a public contract string governed by the spine/parity gates, not a test gap.

## Next steps (optional, not blocking)

- Reconcile finding #1 above.
- Consider a route-level (`Server.Tests`) `ListFolderAclEntries` 200 happy-path asserting entry shape + `aclEntryId` round-trip — currently proven only end-to-end (`UpdateFolderAclEntryShouldPersistAccessOverrideAndRoundTripThroughList`); a unit-level mirror would need ACL-override state seeding.
- Read-op `403` (authenticated-but-denied, distinct from `401`/`404`) and `503` (read-model-unavailable) paths exist per AC1/AC3 but are not yet exercised; both require denial/fault-injection doubles.
