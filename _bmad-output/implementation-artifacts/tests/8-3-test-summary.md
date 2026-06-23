# Test Automation Summary — Story 8.3 (Wire-exercise cross-surface parity)

**Workflow:** `bmad-qa-generate-e2e-tests`
**Story:** `_bmad-output/implementation-artifacts/8-3-wire-exercise-cross-surface-parity-and-close-claim.md`
**Date:** 2026-06-23
**Engineer:** QA automation (Jerome)
**Framework detected:** xUnit v3 `3.2.2` + Shouldly `4.3.0` (.NET 10; NSubstitute for focused doubles). No JS/Playwright lane in scope — used the project's existing test framework.

## Scope

Story 8.3 was already implemented and in `review` with a large, well-built test surface (the story *is* about tests). This run was a **coverage-gap audit** of the five ACs against the implemented production change, auto-applying discovered gaps (per the request).

## Production surface under test

The only production change in this story is the gateway-exception → REST problem mapping in
`src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`:

- `ToArchiveGatewayProblem` — one new branch: `403 + reasonCode == "folder_acl_denied"` → canonical `folder_acl_denied` problem (not the generic `denied_safe` fallback).
- `SafeGatewayReasonCode` — **six** new aliases all normalizing to `folder_acl_denied`:
  `folder_acl_denied`, `folder-acl-denied`, `FolderAclDenied`, `AclEvidenceMismatch`,
  `AclEvidenceForeignFolder`, `AclEvidenceUnsupportedAction`.

## Gap discovered & auto-applied

| Gap | Evidence | Fix |
|---|---|---|
| **2 of 6 production `SafeGatewayReasonCode` aliases were untested.** The new `ArchiveFolderEndpointShouldSurfaceCanonicalFolderAclDeniedFromGatewayRejection` theory exercised only `FolderAclDenied` / `folder_acl_denied` / `folder-acl-denied` / `AclEvidenceMismatch`. `AclEvidenceForeignFolder` and `AclEvidenceUnsupportedAction` (production lines 3864–3865) had no test driving their normalization branch. | `reasonCode` is normalized via `SafeGatewayReasonCode(exception.ReasonCode)` (endpoint line 3544) before the `== "folder_acl_denied"` branch fires; the two aliases routed through code with zero coverage. | Added two `[InlineData]` rows (`AclEvidenceForeignFolder`, `AclEvidenceUnsupportedAction`) to the existing theory in `tests/Hexalith.Folders.Server.Tests/ArchiveFolderEndpointTests.cs`. Theory now covers all six aliases → full branch coverage of the new production mapping. |

### Coverage already adequate (verified, no gap added — avoided over-engineering)

- **AC1** — `GoldenLifecycleAllNineStepsDriveOverRealRestTransportToTransportTerminalClass` drives all 9 steps over the real REST transport (5 mutating → 202, 3 query → 200, `GetWorkspaceStatus` a documented AD4 seam). No oracle-metadata-only assertions remain.
- **AC2** (folder_acl_denied 403) — `ArchiveFolderEndpointShouldSurfaceCanonicalFolderAclDeniedFromGatewayRejection` (now ×6) at the gateway-hop; verified cross-surface safe-denial (404) parity at the layered-auth path (`CrossSurfaceAclDeniedArchiveSurfacesParitySafeDenialOnEverySurface`).
- **AC3** (idempotency_conflict 409) — `SameIdempotencyKeyWithConflictingPayloadAcrossFourSurfaces…` asserts REST/SDK 409, CLI exit 68, MCP kind `idempotency_conflict`.
- **AC4** (no flattening) — shared `InProcessRejectionPropagatingGatewayClient` adopted across the 3 integration-test files; rejection identity preserved (non-2xx, not 202).
- **Non-over-fire (regression guard)** — `ArchiveFolderEndpointShouldMapGatewayRejectionsToContractSafeShapes` `[InlineData(403, "tenant_access_denied", "denied_safe")]` proves a 403 with a non-matching reason still falls through to `denied_safe` (default branch returns `null`).
- **AC5** (claim gate) — documentation-only across `docs/sdk/{api,mcp,cli}-reference.md` + `docs/contract/contract-parity-ci-gates.md`; not an API/E2E test target.

## Generated Tests

### API / route-level Tests
- [x] `tests/Hexalith.Folders.Server.Tests/ArchiveFolderEndpointTests.cs` — `ArchiveFolderEndpointShouldSurfaceCanonicalFolderAclDeniedFromGatewayRejection` extended from 4 → **6** alias variants (added `AclEvidenceForeignFolder`, `AclEvidenceUnsupportedAction`).

## Coverage

- Production `SafeGatewayReasonCode` `folder_acl_denied` aliases: **6/6 covered** (was 4/6).
- New `ToArchiveGatewayProblem` `folder_acl_denied` branch: positive (×6) + non-over-fire negative covered.
- Story 8.3 ACs 1–4: wire/route-level covered; AC5 is docs.

## Validation run

```
dotnet build tests/Hexalith.Folders.Server.Tests  → 0W / 0E
dotnet test  tests/Hexalith.Folders.Server.Tests  → Passed! 535/0 (was 533 pre-fill; +2 alias variants)
  └ ArchiveFolderEndpointShouldSurfaceCanonicalFolderAclDeniedFromGatewayRejection → 6/6 green
```

The change is additive `[InlineData]` in `Server.Tests` only (no production or cross-project change), so the integration/CLI/MCP suites are unaffected (story baseline: IntegrationTests 603/0, Cli 691/0, Mcp 646/0, Client 280/0, Contracts 250/0, Folders 1314/0).

## Next Steps

- Run in CI alongside the existing contract-parity / mixed-surface gates.
- If future `FolderAclDenied`-family reason codes are added to `SafeGatewayReasonCode`, add a matching `[InlineData]` row to keep alias coverage at 100%.
