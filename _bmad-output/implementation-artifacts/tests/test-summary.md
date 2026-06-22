# Test Automation Summary

> Canonical latest-run summary for Story 8.2. Durable per-story copy: [`8-2-test-summary.md`](./8-2-test-summary.md).

**Workflow:** `bmad-qa-generate-e2e-tests`
**Story:** `_bmad-output/implementation-artifacts/8-2-implement-ops-console-diagnostics-rest-routes.md`
**Feature under test:** the 7 read-only ops-console diagnostics REST routes (`src/Hexalith.Folders.Server/OpsConsoleDiagnosticsEndpoints.cs`).
**Framework:** xUnit + `Microsoft.AspNetCore.TestHost` (`WebApplication` + `GetTestClient`) — the project's existing API-conformance pattern. No new framework introduced.

## Generated Tests

### API Tests — `tests/Hexalith.Folders.Server.Tests/OpsConsoleDiagnosticsEndpointTests.cs`

Existing contract/route tests retained; the following coverage **gaps were discovered and auto-applied**:

- [x] **403 `denied_safe`** — `TenantScopedDiagnosticsShouldDenySafeWhenClientControlledTenantMismatch` (Theory ×2). Client-supplied tenant override that disagrees with the authoritative tenant fails closed; attempted tenant id never reflected. *(403 branch had no prior test.)*
- [x] **503 `read_model_unavailable`** — `DiagnosticsShouldReturnServiceUnavailableWhenReadModelThrows` (Theory ×4). Throwing read model → retryable 503, never 200 / never a leaked exception. *(503 branch had no prior test.)*
- [x] **400 `validation_error`** — `DiagnosticsShouldRejectNonCanonicalPathId` (Theory ×3). Non-canonical path segments fail the anti-injection canonical-id guardrail; offending value never echoed.
- [x] **400 `unsafe_correlation_id`** — `DiagnosticsShouldRejectSensitiveCorrelationId` (Theory ×2). Secret-looking correlation id rejected and never reflected.
- [x] **409 `projection_stale` (folder-scoped)** — `GetLockDiagnosticsShouldReturnConflictWhenFolderProjectionStale`. Folder/workspace counterpart of the tenant-scoped 409 test.
- [x] **Per-route guardrail breadth** — idempotency-key / unsupported-freshness / unauthenticated-safe-denial Theories broadened from 2 representative routes to **all 7**.

### E2E (UI) Tests

- N/A — Story 8.2 ships REST server routes only; no UI surface to automate.

## Coverage

- **Diagnostics endpoint tests:** 17 → **39** (`OpsConsoleDiagnosticsEndpointTests`).
- **Full `Hexalith.Folders.Server.Tests` project:** **525 / 525 passing** (was 503; +22), 0 skipped — no regressions.
- Every spine status code (`200/401/403/404/409/503`) now has at least one route-level test; both authorization paths (tenant-scoped + folder-scoped) and the diagnostics-specific `projection_stale → 409` mapping are exercised.

## Validation

| Check | Command | Result |
| ----- | ------- | ------ |
| Diagnostics suite | `dotnet test tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --filter "FullyQualifiedName~OpsConsoleDiagnosticsEndpointTests"` | 39/39 passed |
| Full Server.Tests project | `dotnet test tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj` | 525/525 passed, 0 skipped |

## Files Changed

- `tests/Hexalith.Folders.Server.Tests/OpsConsoleDiagnosticsEndpointTests.cs`
- `_bmad-output/implementation-artifacts/tests/8-2-test-summary.md`
- `_bmad-output/implementation-artifacts/tests/test-summary.md`

## Next Steps

- Run in CI alongside the `Contracts.Tests` parity oracle (REST 47/47). Full per-story detail: [`8-2-test-summary.md`](./8-2-test-summary.md).
