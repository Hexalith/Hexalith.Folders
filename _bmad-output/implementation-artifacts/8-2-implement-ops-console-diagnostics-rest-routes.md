---
baseline_commit: 2df3c410bec5eb6543766fa256bbacbd06d46345
---

# Story 8.2: Implement the 7 ops-console diagnostics REST server routes (Bucket B)

Status: done

<!-- Created 2026-06-22 via bmad-correct-course (sprint-change-proposal-2026-06-22.md). Epic 8 closure story. -->

## Story

As an operator,
I want the ops-console diagnostics operations to have working REST server routes,
So that the read-only operations console and any REST consumer can retrieve diagnostics evidence instead of hitting unimplemented endpoints.

## Context

Verified 2026-06-22 (adversarial parity workflow): the 7 diagnostics operations are present on **SDK + MCP only**. CLI absence is **intentional** (diagnostics is MCP-only by design — do NOT add CLI commands). The gap is the **REST server route** (and the console consumes REST). The spine declares all 7. This story closes **Bucket B**; with Story 8.1 it brings REST to **47/47**.

## Operations to implement (all FR52; spine line refs verified)

| operationId | Method / Path | Spine line |
|---|---|---|
| `GetReadinessDiagnostics` | GET `/api/v1/ops-console/readiness-diagnostics` | 4304 |
| `GetLockDiagnostics` | GET `.../ops-console/lock-diagnostics` | 4390 |
| `GetDirtyStateDiagnostics` | GET `.../ops-console/dirty-state-diagnostics` | 4483 |
| `GetFailedOperationDiagnostics` | GET `.../ops-console/failed-operation-diagnostics` | 4577 |
| `GetProviderStatusDiagnostics` | GET `.../ops-console/provider-status-diagnostics` | 4674 |
| `GetSyncStatusDiagnostics` | GET `.../ops-console/sync-status-diagnostics` | 4768 |
| `GetProjectionFreshness` | GET `/api/v1/ops-console/projection-freshness` | 4864 |

## Acceptance Criteria

1. **Given** the spine declares the 7 diagnostics operations and the SDK + MCP wrap them, **when** REST server routes are added under `Hexalith.Folders.Server`, **then** all 7 respond on REST with metadata-only, read-only, projection-backed responses matching the spine.
2. **Given** diagnostics are read-only ops-console reads, **when** invoked, **then** they enforce authorization-before-observation, fail closed on stale projection/tenant-access revocation, and never expose secrets, raw provider payloads, or unauthorized-resource existence.
3. **Given** CLI is intentionally diagnostics-free, **when** the routes land, **then** no CLI command is added (CLI stays 40/47 by design) and the parity oracle reflects the MCP-only family rule.
4. **Given** Story 8.1 closed Bucket A, **when** Bucket B lands, **then** REST coverage reaches **47/47** and the parity oracle + contract-spine drift gate pass.

## References

- Verified parity result: workflow `verify-epic5-parity-gap` (2026-06-22).
- MCP diagnostics: `src/Hexalith.Folders.Mcp/Tools/DiagnosticsTools.cs`.
- SDK: `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs`.
- Sprint Change Proposal: `../planning-artifacts/sprint-change-proposal-2026-06-22.md`.

## Verified spine paths (full; the table above abbreviates with `.../ops-console/...`)

| operationId | Method / Path (spine, verified) | Auth scope | Params |
|---|---|---|---|
| `GetReadinessDiagnostics` | GET `/api/v1/ops-console/readiness-diagnostics` | tenant-scoped | correlationId, freshness |
| `GetLockDiagnostics` | GET `/api/v1/folders/{folderId}/workspaces/{workspaceId}/ops-console/lock-diagnostics` | folder+workspace | folderId, workspaceId |
| `GetDirtyStateDiagnostics` | GET `/api/v1/folders/{folderId}/workspaces/{workspaceId}/ops-console/dirty-state-diagnostics` | folder+workspace | folderId, workspaceId |
| `GetFailedOperationDiagnostics` | GET `/api/v1/folders/{folderId}/workspaces/{workspaceId}/ops-console/failed-operation-diagnostics` | folder+workspace | folderId, workspaceId |
| `GetProviderStatusDiagnostics` | GET `/api/v1/folders/{folderId}/ops-console/provider-status-diagnostics` | folder | folderId |
| `GetSyncStatusDiagnostics` | GET `/api/v1/folders/{folderId}/workspaces/{workspaceId}/ops-console/sync-status-diagnostics` | folder+workspace | folderId, workspaceId |
| `GetProjectionFreshness` | GET `/api/v1/ops-console/projection-freshness` | tenant-scoped | correlationId, freshness |

All 7 are GET, read-only, read-consistency class `eventually_consistent`, status codes 200/401/403/404/409/503. Per the spine, **`projection_stale` maps to 409** (not 503); `read_model_unavailable` / `projection_unavailable` map to 503. Backing audit (workflow `verify-epic5-parity-gap` + this story's mapping pass): **none of the 7 has any existing server route, query handler, or read model** — all built from scratch, mirroring Story 8.1's read-route pattern (`ProviderReadinessEndpoints.cs` / `GetProviderBindingQueryHandler` / `WorkspaceTransitionEvidenceQueryHandler` + seedable in-memory read model). SDK response DTOs (`ReadinessDiagnostics`, `LockDiagnostics`, `DirtyStateDiagnostics`, `FailedOperationDiagnostics`, `ProviderStatusDiagnostics`, `SyncStatusDiagnostics`, `ProjectionFreshnessDiagnostics` over `DiagnosticBase`) define the wire shape; the server emits matching metadata-only System.Text.Json records.

## Tasks / Subtasks

- [x] **Task 1 — Diagnostics read-model + query layer (`src/Hexalith.Folders/Queries/OpsConsole/`)** (AC: 1, 2)
  - [x] Shared types: `DiagnosticReadResultCode` (Allowed/AuthenticationRequired/AuthorizationDenied/NotFoundSafe/ProjectionStale/ProjectionUnavailable/ReadModelUnavailable), `DiagnosticReadFreshness`, generic `OpsConsoleDiagnosticReadResult<TPayload>`.
  - [x] Metadata-only wire view records (serialized directly): `DiagnosticTrustEvidenceView`, `DiagnosticFieldClassificationView`, `RedactionMetadataView`, `RedactableDiagnosticIdentifierView`, `ChangedPathEvidenceView`, `RetryEligibilityView`.
  - [x] 7 diagnostics view records (op payloads; lookup keys `[JsonIgnore]`-d so they never serialize): `ReadinessDiagnosticsView`, `LockDiagnosticsView`, `DirtyStateDiagnosticsView`, `FailedOperationDiagnosticsView`, `ProviderStatusDiagnosticsView`, `SyncStatusDiagnosticsView`, `ProjectionFreshnessDiagnosticsView`.
  - [x] `IOpsConsoleDiagnosticsReadModel` + seedable `InMemoryOpsConsoleDiagnosticsReadModel` (production hosts replace with projection-backed impl).
- [x] **Task 2 — Tenant-scoped diagnostics handler (readiness, projection-freshness)** (AC: 1, 2)
  - [x] `TenantScopedDiagnosticsQueryHandler` mirroring `GetProviderBindingQueryHandler`: `TenantAccessAuthorizer.AuthorizeDiagnosticReadAsync`, claim-transform evidence check, system-tenant + client-controlled-mismatch fail-closed, safe denial (snapshot tenant mismatch → NotFoundSafe).
- [x] **Task 3 — Folder/workspace-scoped diagnostics handler (lock, dirty, failed, provider-status, sync)** (AC: 1, 2)
  - [x] `FolderScopedDiagnosticsQueryHandler` mirroring `WorkspaceTransitionEvidenceQueryHandler`: `LayeredFolderAuthorizationService.AuthorizeAsync` (StrictRead, `read_metadata`, OperationScope=folderId), layered-denial→code mapping, safe denial on tenant/folder/workspace mismatch.
- [x] **Task 4 — REST routes (`src/Hexalith.Folders.Server/OpsConsoleDiagnosticsEndpoints.cs`)** (AC: 1, 2, 3)
  - [x] Self-contained `static partial class` mirroring `ProviderReadinessEndpoints`: 7 `MapGet` routes at the verified spine paths, each `.WithName(<operationId>).AddEndpointFilter<FolderAuditEndpointFilter>()`.
  - [x] Read-op guardrails per Story 8.1 DD1: reject `Idempotency-Key`→400 `idempotency_key_not_allowed`; validate `X-Hexalith-Freshness` == `eventually_consistent` else 400 `unsupported_read_consistency`; canonical-id path validation; safe correlation handling.
  - [x] `ToHttpResult` mapping: Allowed→200 Json(view); AuthenticationRequired→401; AuthorizationDenied→403 `denied_safe`; NotFoundSafe→404 `not_found`; **ProjectionStale→409 `projection_stale`**; ProjectionUnavailable→503; ReadModelUnavailable→503. Metadata-only `SafeProblem` (evidenceSource `ops_console_diagnostics`), no path-id echo.
  - [x] Wire into `MapFoldersServerEndpoints` (`FoldersServerModule.cs`).
- [x] **Task 5 — DI registration** (AC: 1)
  - [x] `AddFoldersOpsConsoleDiagnostics()` (read model + 2 handlers; ensures layered-auth + tenant-access present) in `FoldersServiceCollectionExtensions.cs`; call from `AddFoldersServer`.
- [x] **Task 6 — Tests + parity oracle** (AC: 1, 2, 3, 4)
  - [x] `OpsConsoleDiagnosticsEndpointTests.cs`: per-op route registration, authorized 200 contract shape, `Idempotency-Key`→400, unsupported-freshness→400, safe-not-found→404 (no id echo), unauthenticated safe denial→401 (no id echo), projection-stale→409.
  - [x] `TransportParityConformanceTests`: `ImplementedRestOperationCount` 40→**47**; empty `KnownRestSurfaceGap`; update drift-doc comments (15/7 → 0). CLI stays 40/47 (diagnostics MCP-only by design — AC3).
- [x] **Task 7 — Validate & finalize** (AC: 4)
  - [x] `dotnet build Hexalith.Folders.slnx` clean (warnings-as-errors); run `Server.Tests`, `IntegrationTests`, `Contracts.Tests` (contract-spine drift + C13 parity oracle → REST 47/47), `Folders.Tests`.
  - [x] Update File List, Change Log, Completion Notes; Status → review; sprint-status 8-2 → review.

## Dev Agent Record

### Agent Model Used

claude-opus-4-8[1m]

### Debug Log References

- Baseline full-solution build green at `2df3c41` (Story 8.1 close).
- Final verification (2026-06-23): full-solution `dotnet build Hexalith.Folders.slnx` clean (0 warnings / 0 errors, warnings-as-errors); `Server.Tests` 525/0, `Contracts.Tests` 250/0 (contract-spine drift + C13 parity oracle), `Folders.Tests` 1314/0, `IntegrationTests` 601/0; `Cli.Tests` 691/0, `Mcp.Tests` 646/0, `Client.Tests` 280/0.
- Senior-review verification (2026-06-23): re-ran all gates green and added review fixes — `Server.Tests` now **529/0** (+4 tenant-access-revocation cases); full-solution build still 0W/0E.
- Parity oracle baseline updated 40 → **47** (`TransportParityConformanceTests.ImplementedRestOperationCount`); `KnownRestSurfaceGap` reduced from 7 → **0** (Bucket-B diagnostics removed) → REST 47/47, full parity.

### Completion Notes List

All 7 Bucket-B ops-console diagnostics now respond on REST; REST coverage 40 → **47/47** (full parity, AC4). Implementation notes:

- **Reads (7), all `eventually_consistent`, metadata-only, projection-backed.** Built from scratch (backing audit: no prior server route/handler/read model existed for any of the 7). A single seedable `IOpsConsoleDiagnosticsReadModel` + `InMemoryOpsConsoleDiagnosticsReadModel` (production hosts replace with projection-backed impls) backs all seven; per-op view records mirror the SDK `DiagnosticBase`-derived DTOs. Lookup keys (tenant/folder/workspace) are `[JsonIgnore]`-d so they never reach the wire.
- **Two authorization paths.** `TenantScopedDiagnosticsQueryHandler` (readiness, projection-freshness) mirrors `GetProviderBindingQueryHandler` — `TenantAccessAuthorizer.AuthorizeDiagnosticReadAsync`, reserved-`system`-tenant + client-controlled-mismatch + claim-transform fail-closed. `FolderScopedDiagnosticsQueryHandler` (lock, dirty-state, failed-operation, provider-status, sync-status) mirrors `WorkspaceTransitionEvidenceQueryHandler` — `LayeredFolderAuthorizationService` StrictRead over the folder scope (`read_metadata`); the workspace id is a read-model selector only. Both enforce safe denial: a diagnostic owned by another tenant/folder/workspace is indistinguishable from missing (→ 404 `not_found`).
- **Diagnostics-specific status mapping (DD).** Per the spine, `projection_stale` → **409** (not 503, unlike Story 8.1's reads); `projection_unavailable` / `read_model_unavailable` → 503. Read-op guardrails per Story 8.1 DD1: `Idempotency-Key` → 400 `idempotency_key_not_allowed`; `X-Hexalith-Freshness` ≠ `eventually_consistent` → 400 `unsupported_read_consistency`; canonical path-id validation. `SafeProblem` is metadata-only (`evidenceSource: ops_console_diagnostics`) and never echoes the requested folder/workspace id.
- **CLI untouched (AC3).** No CLI command added — diagnostics remain MCP-only by design; CLI stays 40/47. The parity oracle already encodes the MCP-only family rule (Contracts.Tests green); only the REST surface count moved (40 → 47).
- **MVP limitation (documented).** The in-memory diagnostics read model is the dev/test default (seed-backed); production hosts override each diagnostic with its projection-backed read model. Diagnostic freshness is projection-sourced (carried on the seeded view), matching the spine `freshnessBehavior` (reports the projection watermark), not stamped at read time.

### File List

**Added — `src/Hexalith.Folders/Queries/OpsConsole/`**
- `DiagnosticReadResultCode.cs`, `DiagnosticReadFreshness.cs`, `OpsConsoleDiagnosticReadResult.cs`, `DiagnosticReadRequest.cs`
- `DiagnosticTrustEvidenceView.cs`, `DiagnosticFieldClassificationView.cs`, `RedactionMetadataView.cs`, `RedactableDiagnosticIdentifierView.cs`, `ChangedPathEvidenceView.cs`, `RetryEligibilityView.cs`
- `ReadinessDiagnosticsView.cs`, `LockDiagnosticsView.cs`, `DirtyStateDiagnosticsView.cs`, `FailedOperationDiagnosticsView.cs`, `ProviderStatusDiagnosticsView.cs`, `SyncStatusDiagnosticsView.cs`, `ProjectionFreshnessDiagnosticsView.cs`
- `IOpsConsoleDiagnosticsReadModel.cs`, `InMemoryOpsConsoleDiagnosticsReadModel.cs`
- `TenantScopedDiagnosticsQueryHandler.cs`, `FolderScopedDiagnosticsQueryHandler.cs`

**Added — `src/Hexalith.Folders.Server/`**
- `OpsConsoleDiagnosticsEndpoints.cs` (7 routes + generic `ToHttpResult` + metadata-only `SafeProblem`/helpers)

**Modified — `src/`**
- `Hexalith.Folders/FoldersServiceCollectionExtensions.cs` (`AddFoldersOpsConsoleDiagnostics`)
- `Hexalith.Folders.Server/FoldersServerModule.cs` (`AddFoldersOpsConsoleDiagnostics` + `MapOpsConsoleDiagnosticsEndpoints` wiring)

**Added — `tests/Hexalith.Folders.Server.Tests/`**
- `OpsConsoleDiagnosticsEndpointTests.cs`

**Modified — `tests/`**
- `Hexalith.Folders.Server.Tests/TransportParityConformanceTests.cs` (recorded REST count 40→47; `KnownRestSurfaceGap`→empty; drift-doc comments)

**Modified — tracking**
- `_bmad-output/implementation-artifacts/8-2-implement-ops-console-diagnostics-rest-routes.md`, `_bmad-output/implementation-artifacts/sprint-status.yaml`

### Change Log

| Date | Change |
|---|---|
| 2026-06-23 | Engineered the thin backlog stub into a context-filled dev story (corrected/verified full spine paths; backing audit → all 7 built from scratch; DD: `projection_stale`→409 for diagnostics) and began implementation. |
| 2026-06-23 | Implemented all 7 Bucket-B ops-console diagnostics REST routes + supporting query/read-model layer and tests; REST 40→47/47 (full parity); parity oracle + contract-spine gates green; status → review. |
| 2026-06-23 | Senior Developer Review (AI) — adversarial review of all 7 routes against the SDK/spine wire contract (enum vocabularies cross-checked) and ACs 1–4. All gates re-verified green. Auto-fixed 3 findings (see review notes): tenant defense-in-depth in `MatchesWorkspace`, missing AC2 revocation test, stale Debug-Log test count. Status → done. |

## Senior Developer Review (AI)

**Reviewer:** jpiquot · **Date:** 2026-06-23 · **Outcome:** Approve (auto-fixes applied)

**Scope:** All 7 Bucket-B ops-console diagnostics REST routes and the supporting query/read-model layer. Verified each AC against the implementation, cross-checked every response wire shape against the SDK-generated DTOs (`HexalithFoldersClient.g.cs`) including the enum vocabularies (`DiagnosticAudience`, `OperatorDispositionLabel`, `ProjectionAvailability`, `DiagnosticFieldClassification`, `RedactionMetadataVisibility`, `ChangedPathEvidenceEvidenceKind`, `CanonicalErrorCategory`, `SyncStatusDiagnosticsAcceptedCommandState`, `LifecycleState`, `ProviderOutcomeState`, `ReadConsistencyClass`), and re-ran every gate.

**Verification (all green):** full-solution build 0W/0E (warnings-as-errors); Server.Tests 529/0, Contracts.Tests 250/0 (contract-spine drift + C13 parity oracle), Folders.Tests 1314/0, IntegrationTests 601/0, Cli.Tests 691/0, Mcp.Tests 646/0, Client.Tests 280/0.

- **AC1 (REST + spine match):** ✅ 7 GET routes registered at the verified spine paths; metadata-only, read-model-backed; every emitted field/enum value is a valid member of the corresponding SDK DTO/enum.
- **AC2 (authz-before-observation, fail-closed, no leakage):** ✅ both handlers authorize before any read-model access; stale→409, read-model failure→503, safe denial 401/403/404 with no id/secret echo; `[JsonIgnore]` lookup keys never reach the wire.
- **AC3 (CLI untouched, MCP-only family):** ✅ no CLI files changed; Cli/Mcp parity green.
- **AC4 (REST 47/47):** ✅ `ImplementedRestOperationCount = 47`, `KnownRestSurfaceGap` empty, drift gate green.

**Findings (all auto-fixed):**

1. **[Med] AC2 tenant-access-revocation clause had no direct test.** The existing 403 test only exercised the client-mismatch short-circuit (returns before `AuthorizeDiagnosticReadAsync`); the actual revocation path (`TenantAccessOutcome.Denied`/layered tenant-access denial → 403) was unverified. *Fix:* added `DiagnosticsShouldDenySafeWhenTenantAccessRevoked` (Theory ×4 over both auth paths) with a revoked-principal projection; confirms 403 `denied_safe`, no id echo, on all four representative routes.
2. **[Low] Defense-in-depth: `FolderScopedDiagnosticsQueryHandler.MatchesWorkspace` did not verify the view tenant equals the authorized tenant** (only folder+workspace+non-empty tenant), whereas the sibling `GetProviderStatusAsync` path did. Harmless given the tenant-keyed read model, but inconsistent. *Fix:* `MatchesWorkspace` now compares `viewTenantId == authoritativeTenantId`.
3. **[Low] Stale Debug-Log test count.** Story Debug Log reported `Server.Tests 503/0`; the QA test-summary and measured run were 525/0. *Fix:* corrected the Debug Log (and re-stated 529/0 after the new review test).

**File List delta (review fixes):** modified `src/Hexalith.Folders/Queries/OpsConsole/FolderScopedDiagnosticsQueryHandler.cs` and `tests/Hexalith.Folders.Server.Tests/OpsConsoleDiagnosticsEndpointTests.cs` (both already in the File List).
