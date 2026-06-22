# Test Automation Summary — Story 8.2 (Ops-Console Diagnostics REST Routes, Bucket B)

**Workflow:** `bmad-qa-generate-e2e-tests` · **Date:** 2026-06-23 · **Engineer:** QA automation (for Jerome)
**Story:** `_bmad-output/implementation-artifacts/8-2-implement-ops-console-diagnostics-rest-routes.md`
**Feature under test:** the 7 read-only ops-console diagnostics REST routes (`src/Hexalith.Folders.Server/OpsConsoleDiagnosticsEndpoints.cs`).
**Framework:** xUnit + `Microsoft.AspNetCore.TestHost` (`WebApplication` + `GetTestClient`) — the project's existing API-conformance pattern. No new framework introduced.

## Generated Tests

### API Tests — `tests/Hexalith.Folders.Server.Tests/OpsConsoleDiagnosticsEndpointTests.cs`

Existing contract/route tests retained; the following coverage **gaps were discovered and auto-applied**:

- [x] **403 `denied_safe`** — `TenantScopedDiagnosticsShouldDenySafeWhenClientControlledTenantMismatch` (Theory ×2: readiness, projection-freshness). A client-supplied `X-Hexalith-Tenant-Id` that disagrees with the authoritative tenant fails closed; the attempted tenant id is never reflected. *(Previously the 403 branch had no test.)*
- [x] **503 `read_model_unavailable`** — `DiagnosticsShouldReturnServiceUnavailableWhenReadModelThrows` (Theory ×4: readiness, projection-freshness, lock, provider-status). A throwing backing read model surfaces as a retryable 503 — never 200, never a leaked exception. *(Previously the 503 branch had no test.)*
- [x] **400 `validation_error`** — `DiagnosticsShouldRejectNonCanonicalPathId` (Theory ×3: bad folderId, bad workspaceId, folder-only route). Non-canonical path segments fail the anti-injection canonical-id guardrail; the offending value is never echoed. *(Covers both `TryReadWorkspacePreflight` and `TryReadFolderPreflight`.)*
- [x] **400 `unsafe_correlation_id`** — `DiagnosticsShouldRejectSensitiveCorrelationId` (Theory ×2: readiness, lock). A secret-looking correlation id (token/secret/credential/URL) is rejected and never reflected. *(Secret-leakage guardrail, AC2.)*
- [x] **409 `projection_stale` (folder-scoped)** — `GetLockDiagnosticsShouldReturnConflictWhenFolderProjectionStale`. Folder/workspace counterpart of the existing tenant-scoped 409 test; confirms the layered authorizer maps a stale tenant projection to 409 (not 503) for diagnostics.
- [x] **Per-route guardrail breadth** — `DiagnosticsShouldRejectIdempotencyKey`, `DiagnosticsShouldRejectUnsupportedFreshness`, `DiagnosticsShouldUseSafeDenialForUnauthenticatedCaller` broadened from 2 representative routes to **all 7** (each route is independently registered → guards against a per-route regression slipping through).

### E2E (UI) Tests

- N/A — Story 8.2 ships REST server routes only (no UI surface). The read-only ops console consumes these routes via REST; UI E2E is out of scope for this story.

## Coverage

Spine status codes for the 7 diagnostics: `200 / 401 / 403 / 404 / 409 / 503`.

| Outcome / guardrail | Before | After |
|---|---|---|
| 200 contract shape (per-op) | 7/7 | 7/7 |
| 401 unauthenticated safe denial | 2 routes | **7 routes** |
| 403 `denied_safe` | ❌ none | **✅ tenant-scoped (×2)** |
| 404 not-found-safe | lock | lock |
| 409 `projection_stale` | tenant-scoped | **+ folder-scoped** |
| 503 `read_model_unavailable` | ❌ none | **✅ (×4 routes)** |
| 400 `idempotency_key_not_allowed` | 2 routes | **7 routes** |
| 400 `unsupported_read_consistency` | 2 routes | **7 routes** |
| 400 `validation_error` (canonical path id) | ❌ none | **✅ (×3)** |
| 400 `unsafe_correlation_id` | ❌ none | **✅ (×2)** |
| No lookup-key / id / secret leakage | partial | retained + extended |

- **Diagnostics endpoint tests:** 17 → **39** (`OpsConsoleDiagnosticsEndpointTests`).
- **Full `Hexalith.Folders.Server.Tests` project:** **525 / 525 passing** (was 503; +22), 0 skipped — no regressions.
- Every spine status code (`200/401/403/404/409/503`) now has at least one route-level test; both authorization paths (tenant-scoped + folder-scoped) and the diagnostics-specific `projection_stale → 409` mapping are exercised.

## Validation

| Check | Command | Result |
| ----- | ------- | ------ |
| Diagnostics suite | `dotnet test tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --filter "FullyQualifiedName~OpsConsoleDiagnosticsEndpointTests"` | 39/39 passed |
| Full Server.Tests project | `dotnet test tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj` | 525/525 passed, 0 skipped |

## Files Changed

- `tests/Hexalith.Folders.Server.Tests/OpsConsoleDiagnosticsEndpointTests.cs` (gap tests added; `BuildApp` now accepts `IOpsConsoleDiagnosticsReadModel`; added `ThrowingOpsConsoleDiagnosticsReadModel` fake)
- `_bmad-output/implementation-artifacts/tests/8-2-test-summary.md` (this file)
- `_bmad-output/implementation-artifacts/tests/test-summary.md` (canonical latest-run pointer)

## Next Steps

- Run in CI alongside the existing `Contracts.Tests` parity oracle (REST 47/47).
- When a production projection-backed `IOpsConsoleDiagnosticsReadModel` lands, add a `ProjectionUnavailable → 503 projection_unavailable` test (the in-memory seam currently exercises `read_model_unavailable`; the distinct `projection_unavailable` code is reachable only through the projection-backed authorizer path).
