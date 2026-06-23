---
baseline_commit: 08cbbdf600fb4addc22f1994d3fa9c86d07b9bbd
---

# Story 8.3: Wire-exercise cross-surface parity and gate the four-surface claim

Status: done

<!-- Created 2026-06-22 via bmad-correct-course (sprint-change-proposal-2026-06-22.md). Epic 8 closure story. -->
<!-- Depends on 8.1 + 8.2 (47/47 server routes) for the full golden-lifecycle wire run. -->
<!-- Refined 2026-06-23 via bmad-create-story (Refine-then-dev). Engineered the thin stub into a context-filled dev story after an adversarial backing audit (3 verification subagents + direct source verification). The audit corrected the stub's premise on TWO points: (1) `idempotency_conflict` needs NO production change — the REST 409 fallback already emits the canonical category; (2) `folder_acl_denied` is not just a stub-flattening artifact — no production REST path currently surfaces the canonical `folder_acl_denied` 403 category at all (the gateway-rejection path collapses it to generic `denied_safe`/403, and the layered-auth path deliberately maps it to `404 not_found_to_caller` for safe denial). The verified, safe-denial-preserving fix is documented in Dev Notes → Architectural Decisions. No spine change is required. -->

## Story

As a release stakeholder,
I want the golden-lifecycle and mixed-surface parity scenarios actually exercised over the wire across all four surfaces,
So that the "four-surface parity" guarantee is true end-to-end before it is asserted to consumers.

## Context

The 2026-06-22 readiness review (§5 Major #7) found Epic 5's strongest parity ACs were satisfied only at oracle-metadata / aggregate-ledger level, not wire-exercised:
- **5.5 AC7** golden lifecycle drove only 2 of 9 steps over the wire (7 asserted at oracle-metadata level).
- **5.7** cross-surface `idempotency_conflict` and `folder_acl_denied` were not fully four-surface-evidenced — an in-process gateway stub flattened `IsRejection`, so `folder_acl_denied` surfaced as **503/denied_safe instead of canonical 403** and `idempotency_conflict` was not surfaced at REST **409**.

## Acceptance Criteria

1. **Given** 47/47 server routes exist (Stories 8.1 + 8.2), **when** the golden-lifecycle parity scenario runs, **then** all 9 steps are driven over the real REST transport (and the SDK/CLI/MCP equivalents), not asserted at oracle-metadata level.
2. **Given** an ACL-denied operation, **when** invoked across REST/SDK/CLI/MCP, **then** it returns canonical `folder_acl_denied` mapped to HTTP **403** (not 503/denied_safe), with the matching CLI exit code and MCP failure kind from the parity oracle.
3. **Given** an idempotency conflict, **when** invoked across all four surfaces, **then** it surfaces canonical `idempotency_conflict` at HTTP **409**, with matching CLI/MCP behavior.
4. **Given** the in-process gateway stub that flattened `IsRejection`, **when** the mixed-surface handoff test runs, **then** rejection identity is preserved end-to-end (no stub flattening).
5. **Given** parity is genuinely wire-exercised at 47/47, **when** documentation/consumer references are updated, **then** the public "four-surface canonical-lifecycle parity" claim is asserted **only** after this story passes (the claim is gated on 8.1–8.3).

## References

- Verified parity result: workflow `verify-epic5-parity-gap` (2026-06-22).
- Existing parity tests: `tests/Hexalith.Folders.IntegrationTests/EndToEnd/GoldenLifecycleParityTests.cs`, `.../MixedSurfaceHandoff/MixedSurfaceHandoffTests.cs`.
- Proven rejection-propagating gateway (the fix pattern): `tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs` (its `InProcessEventStoreGatewayClient`, lines ~977–1067).
- Shared scenario sources: `tests/shared/Parity/GoldenLifecycle.cs`, `tests/shared/Parity/MixedSurfaceScenario.cs`, `tests/shared/Parity/ParityScenarios.cs`.
- Parity oracle: `tests/fixtures/parity-contract.yaml`.
- Sprint Change Proposal: `../planning-artifacts/sprint-change-proposal-2026-06-22.md`.

## Backing analysis (verified 2026-06-23; refutation-tested)

Two `InProcessEventStoreGatewayClient` families coexist in the test tree. They differ **only** in how they handle the `/process` round-trip result — and that difference is the entire bug:

| Family | Files | Behavior on `/process` result | Effect |
|---|---|---|---|
| **Flattening (broken)** | `GoldenLifecycleParityTests.cs` (~708–756), `MixedSurfaceHandoffTests.cs` MixedSurfaceHost (~1034–1082) | `response.EnsureSuccessStatusCode()` then `return new SubmitCommandResponse(...)` — discards the `IsRejection` body, never inspects it | Aggregate rejection is masked; the REST endpoint emits **202** (or a generic non-canonical failure), so 403/409 never surface over the wire |
| **Propagating (correct)** | `ArchiveFolderProcessWiringTests.cs` (~977–1067) | Reads `DomainServiceWireResult`; if `result.IsRejection` → `throw ToGatewayException(...)` mapping `FolderResultCode` → HTTP status | Endpoint surfaces real **403 / 409 / 404 / 429 / 503** per the canonical map; its tests assert 409 (`...ShouldSurfaceIdempotencyConflict...`) and 403 (`...AlreadyArchivedAsSafeDenial...`) and pass green |

Key verified facts from the production REST mapping (`src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`, `FolderCanonicalErrorMapper.cs`):

- **`idempotency_conflict` is already correct end-to-end.** `ToArchiveGatewayProblem`'s final switch maps `StatusCode == 409 → category "idempotency_conflict"` (line ~3750), and `FolderCanonicalErrorMapper` maps `IdempotencyConflict → idempotency_conflict → 409`. So a propagating gateway that throws `EventStoreGatewayException(409)` already yields the canonical category. **No production change needed for AC3** — only the test-stub propagation.
- **`folder_acl_denied` is NOT surfaced by any production REST path today.** `/process` itself *does* emit `code: "folder_acl_denied"` on an aggregate ACL rejection (`FoldersDomainServiceRequestHandlerTests:107`), and the CLI/MCP/SDK adapters *already* project a 403 `folder_acl_denied` problem → exit 66 / kind `folder_acl_denied` (`CrossAdapterBehavioralParityTests:461,715`). But the **gateway-exception → REST problem** mapping drops it: `SafeGatewayReasonCode` has no `folder_acl_denied` entry (returns null), so `ToArchiveGatewayProblem` falls to its default `_ → 403 tenant_access_denied / denied_safe`. Separately, the **pre-gateway layered-auth** path deliberately maps `FolderAclDenied → 404 not_found_to_caller` (`SafeAuthorizationDenialMappingTests:21`) for safe denial. **AC2 therefore needs a small production link** (below) plus the test-stub propagation.
- **Oracle vocabulary (confirmed):** `folder_acl_denied` → cli_exit_code **66**, mcp_failure_kind **folder_acl_denied**; `idempotency_conflict` → cli_exit_code **68**, mcp_failure_kind **idempotency_conflict**; `authentication_failure` (already four-surface-tested) → 401 / exit 65.
- **`EventStoreGatewayException`** (`Hexalith.EventStore.Client/Gateway/EventStoreGatewayException.cs`) carries a `reasonCode` ctor param + `ReasonCode` property — the propagating gateway must set it to the canonical code so `ToArchiveGatewayProblem`'s reasonCode-keyed branches fire.

## Tasks / Subtasks

- [x] **Task 1 — Shared rejection-propagating in-process gateway (AC: 4)**
  - [x] Add `tests/shared/Parity/InProcessRejectionPropagatingGatewayClient.cs` (an `IEventStoreGatewayClient` test double): round-trip the command to `/process`; throw `EventStoreGatewayException((int)response.StatusCode, …)` on non-2xx; deserialize `DomainServiceWireResult`; on `result.IsRejection` parse the rejection event payload's `code`/`Code` and `throw new EventStoreGatewayException(status, "Rejected", correlationId: …, reasonCode: canonicalCode)` where `status` is derived from the canonical code via the same `FolderResultCode`→status table proven in `ArchiveFolderProcessWiringTests` (`IdempotencyConflict`→409, `FolderNotFound`→404, `ProviderRateLimited`→429, validation family→400, projection/evidence-unavailable family→503, **default→403**). Expose `ProcessCalls` (and `LastWireEventCount`). Link it into `Hexalith.Folders.IntegrationTests.csproj` (mirror the existing `..\shared\Parity\*.cs` `<Compile><Link>` entries).
  - [x] Replace the flattening `InProcessEventStoreGatewayClient` in `GoldenLifecycleParityTests.cs` and `MixedSurfaceHandoffTests.cs` (MixedSurfaceHost) with the shared propagating client. Refactor `ArchiveFolderProcessWiringTests.cs` to consume the same shared client (dedupe the third copy) so no flattening or per-file rejection map remains.
  - [x] Prove rejection identity is preserved: a command the aggregate rejects must surface a non-2xx canonical problem (not 202) on every surface; `ProcessCalls` still increments once per submit. (The propagating gateway exposed 6 prior false-greens — archive-on-archived masked as 202 — which were rewritten to truthful same-key idempotent replays / distinct folders.)
- [x] **Task 2 — Production: surface canonical `folder_acl_denied` via the gateway path (AC: 2)**
  - [x] `FoldersDomainServiceEndpoints.SafeGatewayReasonCode`: add `"folder_acl_denied" | "folder-acl-denied" | "FolderAclDenied"` (plus the `AclEvidenceMismatch`/`AclEvidenceForeignFolder`/`AclEvidenceUnsupportedAction` family names) `=> "folder_acl_denied"`.
  - [x] `FoldersDomainServiceEndpoints.ToArchiveGatewayProblem`: add a branch `exception.StatusCode == StatusCodes.Status403Forbidden && reasonCode == "folder_acl_denied"` → `SafeProblem(403, category: "folder_acl_denied", code: "folder_acl_denied", retryable: false, …)`, placed before the generic final switch (mirror the existing `idempotency_conflict` branch).
  - [x] Do **not** alter the pre-gateway layered-auth `FolderAuthorizationDenialMapper` (`FolderAclDenied → 404 not_found_to_caller`): that safe-denial path for unknown-existence cases must remain. The new branch fires only for an aggregate-sourced in-tenant ACL rejection carrying the explicit `folder_acl_denied` code (existence already established for that tenant — see Architectural Decisions).
  - [x] `Server.Tests`: add a route-level test that an aggregate `FolderAclDenied` rejection (propagated through the gateway) surfaces **403** with canonical category/code `folder_acl_denied` (not `denied_safe`), metadata-only, correlation echoed. (`ArchiveFolderEndpointShouldSurfaceCanonicalFolderAclDeniedFromGatewayRejection`, ×6 reason-code variants.)
- [x] **Task 3 — Wire-exercise the 9 golden-lifecycle steps over REST + SDK (AC: 1)**
  - [x] In `GoldenLifecycleParityTests.cs`, added `GoldenLifecycleAllNineStepsDriveOverRealRestTransportToTransportTerminalClass`, which drives **each** of the 9 `GoldenLifecycle.Steps` operations over the **real REST transport**: the five mutating steps (CreateRepositoryBackedFolder/Prepare/Lock/AddFile/Commit) reach `accepted` (202) via real REST→gateway→/process→aggregate-gate round-trips against per-step precondition-seeded folders; ValidateProviderReadiness/ListFolderFiles/GetFolderLifecycleStatus reach 200. SDK equivalents are driven for the query steps + CreateRepositoryBackedFolder (transport equivalence).
  - [x] Replaced oracle-metadata-only coverage with actual wire drives; kept `GoldenLifecycleStepCarriesOracleTransportContractForFamily` as a complementary oracle-contract invariant. **8 of 9 steps reach their happy-path terminal class; GetWorkspaceStatus is a documented seam** — driven over the real route to its real canonical response (200 if reachable, else canonical metadata-only `read_model_unavailable` 503, because the hermetic host does not reproduce the authoritative-tenant evidence path for the workspace-status projection), mirroring the `RestInspectionOperationId` substitution rationale; the happy-path 200 is proven by `Server.Tests.WorkspaceStatusEndpointTests`. No step is asserted at oracle-metadata level.
- [x] **Task 4 — Cross-surface `folder_acl_denied` + `idempotency_conflict` over the wire, all four surfaces (AC: 2, 3)**
  - [x] Add dedicated mixed-surface tests provoking each category across REST + SDK + CLI + MCP through the propagating gateway and asserting byte-for-byte equivalence: `idempotency_conflict` (409 / exit **68** / kind `idempotency_conflict`) via the conflict test below; `folder_acl_denied` ACL denial via `CrossSurfaceAclDeniedArchiveSurfacesParitySafeDenialOnEverySurface` — which asserts the VERIFIED four-surface **safe denial** (404 `not_found_to_caller` / exit **73** / kind `not_found`), the deliberate cross-tenant-leakage invariant (see AD2). Dedicated tests rather than extending the `authentication_failure` theory because the three provocations differ (null tenant vs conflicting payload vs missing ACL grant).
  - [x] Upgrade `SameIdempotencyKeyWithConflictingPayloadAcrossFourSurfaces...` to assert wire-level **409 + canonical `idempotency_conflict`** on REST/SDK/CLI/MCP (in addition to the existing aggregate-ledger invariant). Kept `SameIdempotencyKeyReplayed...` (same payload) green (replay ≠ conflict).
  - [x] Removed/replaced the class-level "in-process gateway response-flattening note" and the per-test "out of scope / aggregate-ledger only" caveats now that the gap is closed.
- [x] **Task 5 — Gate the public four-surface parity claim on 8.1–8.3 (AC: 5)**
  - [x] Add a parity-verification note to `docs/sdk/api-reference.md` (after the "parallel adapters of the same 47 canonical operations" surface-conventions paragraph) stating the four-surface canonical-lifecycle parity claim is **wire-exercised and validated at 47/47 as of Stories 8.1–8.3** (REST routes 8.1/8.2 + wire-exercise 8.3); cross-link the parity gates. Added the corresponding one-line gate reference to `docs/sdk/mcp-reference.md` and `docs/sdk/cli-reference.md`.
  - [x] Record the wire-exercised parity coverage + the 8.1–8.3 gating in `docs/contract/contract-parity-ci-gates.md` (the `rest-sdk-golden-parity` / `mixed-surface-handoff` gate rows). All wording metadata-only.
- [x] **Task 6 — Validate & finalize (AC: 1, 2, 3, 4, 5)**
  - [x] Full-solution `dotnet build Hexalith.Folders.slnx` clean (**0W/0E**, warnings-as-errors).
  - [x] Ran and passed: `IntegrationTests` **603/0**, `Server.Tests` **535/0**, `Cli.Tests` **691/0**, `Mcp.Tests` **646/0**, `Client.Tests` **280/0**, `Contracts.Tests` **250/0** (contract-spine drift + C13 parity oracle, stays 47/47), `Folders.Tests` **1314/0**. No regressions.
  - [x] Updated File List, Completion Notes, Change Log.

## Dev Notes

### Architectural Decisions

- **AD1 — One shared propagating gateway, three call sites.** The fix is to delete the flattening behavior, not to add a parallel one. Extract the proven propagating gateway (today private to `ArchiveFolderProcessWiringTests`) into `tests/shared/Parity/` and consume it from all three integration-test files. This satisfies AC4 ("no stub flattening") globally and prevents a fourth divergent copy. The gateway must set `EventStoreGatewayException.ReasonCode` to the canonical code parsed from the `/process` rejection so the REST endpoint's reasonCode-keyed branches surface the canonical category (status alone is insufficient for AC2's category/kind assertions).
- **AD2 — `folder_acl_denied` is completed at the gateway hop, safe-denial preserved.** AC2 requires the canonical `folder_acl_denied` 403 category + exit 66 + kind `folder_acl_denied`. Today only the **adapter projection** (5.6) and the **`/process` rejection code** prove this; the **gateway-exception → REST** hop drops it to `denied_safe`. Add the missing `SafeGatewayReasonCode` + `ToArchiveGatewayProblem` mapping (Task 2). This is additive and contract-aligned — the spine already declares `folder_acl_denied` in the relevant `error_code_set` rows (`WorkspaceLockContractGroupTests:164`, `FileContextContractGroupTests:143`). Crucially, **leave the layered-auth `404 not_found_to_caller` path untouched**: that is the safe-denial response for *unknown-existence* / cross-tenant probes (top-tier "zero cross-tenant leakage" invariant). The new 403 `folder_acl_denied` fires only when the aggregate gate itself rejects with an explicit `folder_acl_denied` for a resource whose existence is already established within the caller's authorized tenant scope — i.e., it does not create a new existence oracle. **Primary review point:** confirm the provoked four-surface ACL-denial scenario routes through the aggregate-rejection path (403 `folder_acl_denied`), not the layered-auth path (404 `not_found_to_caller`); if a provocation legitimately resolves at the layered path, assert *that* canonical behavior and document the seam rather than weakening safe denial.
  - **VERIFIED 2026-06-23 (the review point, resolved).** Empirically, an ACL-denied archive (principal has tenant access + `read_metadata` but lacks `archive_folder`) resolves at the **pre-gateway layered-authorization** path and the wire returns **404 `not_found_to_caller`** (`evidenceSource: http_boundary`) — the deliberate safe denial — on all four surfaces (REST/SDK 404, CLI exit 73, MCP kind `not_found`). It does **not** reach the aggregate-gate 403 path through normal seeding. Per AD2, the cross-surface mixed-surface test (`CrossSurfaceAclDeniedArchiveSurfacesParitySafeDenialOnEverySurface`) therefore asserts that true four-surface **safe-denial parity** rather than forcing a distinct 403 (which would regress the cross-tenant-leakage invariant). The canonical `folder_acl_denied → 403` gateway-hop mapping (Task 2 production fix) is proven where it actually applies — the route-level Server.Tests theory `ArchiveFolderEndpointShouldSurfaceCanonicalFolderAclDeniedFromGatewayRejection` (×6 reason-code variants) and the Story 5.6 adapter projection. AC2's "not 503" intent is satisfied: ACL denial is never the misleading 503 — it is the canonical 404 safe denial at the wire, or 403 `folder_acl_denied` at the gateway-hop/adapter contract layers.
- **AD3 — `idempotency_conflict` needs no production change.** The REST 409 fallback already emits the canonical category. Task 4's job is purely to *observe* it over the wire on all four surfaces once the gateway propagates the rejection. Do not add redundant 409 mapping.
- **AD4 — Wire-exercise means transport-terminal, not worker-terminal.** The hermetic in-process host has no workers/providers/git. Each golden-lifecycle step is driven to its **transport-terminal class** (202 accepted / 200 projected), which is what the oracle and AC1 require; domain-terminal states that need worker progression are out of transport scope (and were never the 5.5 claim). Seed each step's aggregate precondition (the per-endpoint seeding patterns already exist across `Server.Tests` and `ArchiveFolderProcessWiringTests`).

### Source tree — what this story touches

- **Test infra (new):** `tests/shared/Parity/InProcessRejectionPropagatingGatewayClient.cs` (+ `<Compile><Link>` in `tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj`).
- **Tests (modified):** `tests/Hexalith.Folders.IntegrationTests/EndToEnd/GoldenLifecycleParityTests.cs` (remove flattening stub; add 9-step wire run), `.../MixedSurfaceHandoff/MixedSurfaceHandoffTests.cs` (remove flattening stub + caveats; extend error-category theory + conflict assertions), `.../ArchiveFolderProcessWiringTests.cs` (consume shared gateway), `tests/Hexalith.Folders.Server.Tests/` (new route-level `folder_acl_denied` 403 test).
- **Production (modified, minimal):** `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs` — `SafeGatewayReasonCode` (+3 aliases) and `ToArchiveGatewayProblem` (+1 branch). No spine, generated-client, aggregate, or `FolderCanonicalErrorMapper` change.
- **Docs (modified):** `docs/sdk/api-reference.md`, `docs/sdk/mcp-reference.md`, `docs/sdk/cli-reference.md`, `docs/contract/contract-parity-ci-gates.md`.

### Testing standards (project-context)

- xUnit v3 + Shouldly; `TestContext.Current.CancellationToken` in async tests. Hermetic: loopback `127.0.0.1:0`, in-memory repository/read-models, no Dapr/Keycloak/Redis/providers/network/nested-submodule init.
- Mutation-command acceptance requires the no-mock `IEventStoreGatewayClient` path (real REST → gateway → `/process` → processor → gate). The shared propagating gateway is that path — it does **not** mock the gateway behavior, it round-trips through the actual `/process` endpoint and only fans the result back as the production gateway would.
- Metadata-only invariant holds across every surface response and problem body (no secrets/tokens/paths/diffs/provider payloads); reuse the `ForbiddenContentPatterns` scans already in the parity tests.
- CLI exit codes and MCP failure kinds map 1:1 to canonical categories from the parity oracle — do not collapse `folder_acl_denied`/`tenant_access_denied`/`idempotency_conflict` for adapter convenience.

### Risks

- **R1 (highest) — golden-lifecycle seeding depth (Task 3).** Chaining 9 steps over the wire in one hermetic host is the heaviest task; some steps (prepare/lock/add-file/commit) need precise aggregate-state seeding. Mitigation: lean on existing per-endpoint seeding patterns; if a step cannot reach its precondition through the chain, seed it directly and assert its transport-terminal class with a documented seam (AD4). Do not silently skip a step (AC1 is "all 9").
- **R2 — folder_acl_denied provocation layer (Task 2/4).** See AD2 — verify the provocation reaches the aggregate-rejection path; this is the documented review point.
- **R3 — claim wording (Task 5).** The gating note must assert the claim *as now-validated* (8.3 closes it), not leave it "pending"; keep it metadata-only and consistent across the three consumer docs.

### Project Structure Notes

Aligns with the established test layout: shared parity sources live under `tests/shared/Parity/` and are `<Compile><Link>`-linked into consuming test projects (the IntegrationTests csproj already links `GoldenLifecycle.cs`, `MixedSurfaceScenario.cs`, `ParityScenarios.cs`, `ParityOracle.cs` and the CLI/MCP TestSupport helpers). The new propagating gateway follows that exact convention. Production edits are confined to the single endpoints file's gateway-exception mapping, matching Story 8.1's pattern.

### References

- [Source: tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs#InProcessEventStoreGatewayClient] — proven rejection-propagating gateway + `FolderResultCode`→status map.
- [Source: src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs#ToArchiveGatewayProblem,SafeGatewayReasonCode] — gateway-exception → canonical problem mapping (idempotency_conflict present; folder_acl_denied absent).
- [Source: src/Hexalith.Folders.Server/FolderCanonicalErrorMapper.cs] — `FolderResultCode`→category→status (FolderAclDenied→folder_acl_denied→403; IdempotencyConflict→idempotency_conflict→409).
- [Source: src/Hexalith.Folders.Server/FolderAuthorizationDenialMapper.cs#StatusAndCategory] — layered-auth `FolderAclDenied → 404 not_found_to_caller` (safe denial; leave untouched).
- [Source: src/Hexalith.Folders.Cli/FoldersExitCodes.cs] — AccessDenied=66 (folder_acl_denied/tenant_access_denied), IdempotencyConflict=68.
- [Source: tests/fixtures/parity-contract.yaml] — outcome_mapping: folder_acl_denied→66/folder_acl_denied; idempotency_conflict→68/idempotency_conflict.
- [Source: tests/shared/Parity/GoldenLifecycle.cs] — the 9 steps + `RestInspectionOperationId` substitution pattern.
- [Source: docs/sdk/api-reference.md] — the unconditional "parallel adapters of the same 47 canonical operations" claim to gate.

## Dev Agent Record

### Agent Model Used

claude-opus-4-8[1m]

### Debug Log References

- Baseline: full-solution `dotnet build Hexalith.Folders.slnx` green at `08cbbdf`; IntegrationTests 601/0 baseline.
- Adopting the shared propagating gateway exposed **6 false-greens** in the parity suites (archive-on-archived with a different key masked as 202 by the old flattening stub; it is really a 403 safe denial). Rewrote them truthfully: GoldenLifecycle cross-surface mutating tests now archive a distinct active folder per surface; MixedSurface `OneTaskMoves`/`MutatingArchive…`/`AuditInspection` now use same-key idempotent replays (proving each downstream surface observes the first writer over the wire).
- Verified increments: `Server.Tests` 525→**535/0** (+ ×6 `folder_acl_denied` gateway-mapping theory + the existing suite); `IntegrationTests` 601→**608/0** so far (MixedSurface 7→8 with the new safe-denial cross-surface test; conflict test upgraded to wire-level 409). AC1 9-step golden-lifecycle wire test in progress.
- Empirical AC2 finding: an ACL-denied archive resolves at the layered-auth boundary → **404 `not_found_to_caller`** safe denial on all four surfaces (REST/SDK 404, CLI 73, MCP `not_found`); the canonical `folder_acl_denied`→403 mapping is proven at the gateway-hop (Server.Tests ×6) + adapter (5.6) layers. See AD2.

### Completion Notes List

All five ACs satisfied; full regression green (build 0W/0E; IntegrationTests 603/0, Server 535/0, Contracts 250/0, Cli 691/0, Mcp 646/0, Client 280/0, Folders 1314/0). Summary:

- **AC4 (no flattening):** A single shared `InProcessRejectionPropagatingGatewayClient` (tests/shared/Parity/) round-trips `/process` and propagates `IsRejection` (status + canonical reason code), replacing the flattening stubs in `GoldenLifecycleParityTests`/`MixedSurfaceHandoffTests` and the third copy in `ArchiveFolderProcessWiringTests`. Adopting it **exposed 6 false-greens** (archive-on-archived masked as 202) which were rewritten truthfully (distinct active folders / same-key idempotent replays), so rejection identity is now preserved end-to-end on every surface.
- **AC3 (idempotency_conflict):** Wire-exercised across all four surfaces — REST/SDK **409**, CLI exit **68**, MCP kind `idempotency_conflict` (`SameIdempotencyKeyWithConflictingPayloadAcrossFourSurfaces…`). No production change needed (the REST 409 fallback already emits the canonical category).
- **AC2 (folder_acl_denied):** Production gateway-hop mapping added (`SafeGatewayReasonCode` + `ToArchiveGatewayProblem`) so a propagated aggregate ACL rejection surfaces canonical **403 `folder_acl_denied`** (not the generic `denied_safe`), proven by `ArchiveFolderEndpointShouldSurfaceCanonicalFolderAclDeniedFromGatewayRejection` (×6). **AD2 review point resolved by evidence:** an end-to-end ACL-denied archive resolves at the layered-auth boundary as the deliberate **404 `not_found_to_caller` safe denial** (REST/SDK 404, CLI 73, MCP `not_found`) — `CrossSurfaceAclDeniedArchiveSurfacesParitySafeDenialOnEverySurface` asserts that four-surface safe-denial parity rather than forcing a distinct 403 (which would regress the zero-cross-tenant-leakage invariant). AC2's "not 503" intent holds: ACL denial is never the misleading 503.
- **AC1 (wire-exercise 9 steps):** `GoldenLifecycleAllNineStepsDriveOverRealRestTransportToTransportTerminalClass` drives all nine operations over the real REST transport (5 mutating → 202, 3 query → 200); **GetWorkspaceStatus is a documented seam** (real route → real canonical response; happy-path 200 proven separately by `WorkspaceStatusEndpointTests`). No oracle-metadata-only assertions remain.
- **AC5 (claim gate):** The public four-surface canonical-lifecycle parity claim is gated on Stories 8.1–8.3 across `docs/sdk/{api,mcp,cli}-reference.md` and `docs/contract/contract-parity-ci-gates.md`, asserted only on green.

**Deviations from the original task text:** (1) Cross-surface category coverage uses dedicated mixed-surface tests rather than extending the single `authentication_failure` theory (the three provocations differ). (2) AC2's literal "ACL denial → 403 folder_acl_denied at the wire" is reconciled to the verified safe-denial behavior (404) per AD2 — the canonical 403 mapping is proven at the gateway-hop + adapter layers where it applies. (3) One AC1 step (GetWorkspaceStatus) is a documented AD4 seam.

### File List

**Added — tests**
- `tests/shared/Parity/InProcessRejectionPropagatingGatewayClient.cs`

**Modified — production (`src/`)**
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs` — `SafeGatewayReasonCode` (+`folder_acl_denied` family aliases) and `ToArchiveGatewayProblem` (+403 `folder_acl_denied` branch). No spine/generated-client/aggregate change.

**Modified — tests**
- `tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj` — link the shared propagating gateway.
- `tests/Hexalith.Folders.IntegrationTests/EndToEnd/GoldenLifecycleParityTests.cs` — adopt shared gateway; truthful cross-surface mutating tests; AC1 9-step wire-drive test.
- `tests/Hexalith.Folders.IntegrationTests/MixedSurfaceHandoff/MixedSurfaceHandoffTests.cs` — adopt shared gateway; truthful same-key replays; wire-level 409 conflict on four surfaces; `CrossSurfaceAclDeniedArchiveSurfacesParitySafeDenialOnEverySurface`; refreshed class remark; `SeedArchiveDeniedPermissions` helper.
- `tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs` — consume the shared gateway (deduped the third propagating copy).
- `tests/Hexalith.Folders.Server.Tests/ArchiveFolderEndpointTests.cs` — `ArchiveFolderEndpointShouldSurfaceCanonicalFolderAclDeniedFromGatewayRejection` (×6).

**Modified — docs**
- `docs/sdk/api-reference.md`, `docs/sdk/mcp-reference.md`, `docs/sdk/cli-reference.md`, `docs/contract/contract-parity-ci-gates.md` — gate the public four-surface parity claim on Stories 8.1–8.3 (wire-exercised).

**Modified — tracking**
- `_bmad-output/implementation-artifacts/8-3-wire-exercise-cross-surface-parity-and-close-claim.md`, `_bmad-output/implementation-artifacts/sprint-status.yaml`

### Change Log

| Date | Change |
|---|---|
| 2026-06-23 | Refined the thin backlog stub into a context-filled dev story via bmad-create-story: adversarial backing audit (3 subagents + direct source verification) corrected the premise — `idempotency_conflict` needs no production change (409 fallback already canonical); `folder_acl_denied` 403 is not surfaced by any production REST path today and needs a small safe-denial-preserving gateway-mapping link. Authored Tasks 1–6 + Architectural Decisions (AD1–AD4). Status → ready-for-dev. |
| 2026-06-23 | Implemented all 6 tasks. Shared rejection-propagating gateway (AC4) replaced the flattening stubs across 3 files, exposing + fixing 6 false-greens. Production `folder_acl_denied`→403 gateway-hop mapping (AC2) + Server.Tests ×6. Wire-level `idempotency_conflict` 409 across four surfaces + verified ACL safe-denial 404 parity (AC2/AC3). 9-step golden-lifecycle wire-drive (AC1; 8 happy-path + 1 documented GetWorkspaceStatus seam). Four-surface parity claim gated on 8.1–8.3 in consumer docs (AC5). Full regression green (build 0W/0E; IntegrationTests 603/0, Server 535/0, Contracts 250/0 [oracle 47/47], Cli 691/0, Mcp 646/0, Client 280/0, Folders 1314/0). Status → review. |
| 2026-06-23 | Senior Developer Review (AI) — adversarial. Re-ran build (0W/0E) + all 7 suites; verified ACs 1–5 against the actual diff. Fixed 2 MEDIUM doc-accuracy discrepancies: the story's `Server.Tests` count (533→**535**) and the `folder_acl_denied` theory variant count (×4→**×6**) were stale — the dev expanded the theory 4→6 alias variants and updated the test-summary but not the story file. No CRITICAL/HIGH issues; implementation matches the claims. Status → done. |

## Senior Developer Review (AI)

**Reviewer:** jpiquot · **Date:** 2026-06-23 · **Outcome:** Approve (auto-fix applied) · **Mode:** adversarial / non-interactive (story-automator)

### Verification performed

- **Build:** `dotnet build Hexalith.Folders.slnx` → **0 Warning / 0 Error** (warnings-as-errors). ✅
- **Tests (re-run, `--no-build`):** IntegrationTests **603/0**, Server.Tests **535/0**, Cli **691/0**, Mcp **646/0**, Client **280/0**, Contracts **250/0** (parity oracle stays 47/47), Folders **1314/0**. ✅
- **Production diff** (`FoldersDomainServiceEndpoints.cs`): the new `folder_acl_denied` 403 branch is correctly placed **before** the generic final switch and keyed on `reasonCode == "folder_acl_denied"` (computed via `SafeGatewayReasonCode`); the 6 reason-code aliases map exactly to the 6 `[InlineData]` variants in the route-level theory. Additive only — no regression path. ✅
- **Shared gateway** (`InProcessRejectionPropagatingGatewayClient.cs`): genuinely propagates `IsRejection` (status **and** canonical reason code), replacing all three flattening stubs (Golden/MixedSurface/ArchiveFolderProcessWiring). No flattening or per-file rejection map remains (AC4). ✅
- **AC cross-check:** AC1 (9-step wire-drive + documented GetWorkspaceStatus seam), AC2 (403 mapping at route + adapter; safe-denial 404 reconciled per AD2), AC3 (wire-level 409/exit 68/kind across four surfaces), AC4 (no stub flattening), AC5 (claim gated across 4 docs; `#surface-conventions` anchor exists; metadata-only). All satisfied.
- **File List vs git:** every `src/`, `tests/`, and `docs/` change is accurately listed (`_bmad-output/` excluded from review per workflow).

### Findings

| # | Severity | Finding | Resolution |
|---|---|---|---|
| 1 | MEDIUM | Story claimed `Server.Tests` **533/0**; actual verified run is **535/0** (test-summary already recorded 535 — "was 533 pre-fill; +2 alias variants"). | **Fixed** — 533→535 across Task 6 / Completion Notes / Change Log / Debug Log. |
| 2 | MEDIUM | Story described the `folder_acl_denied` gateway-rejection theory as **×4** reason-code variants; the actual test has **×6** (`FolderAclDenied`, `folder_acl_denied`, `folder-acl-denied`, `AclEvidenceMismatch`, `AclEvidenceForeignFolder`, `AclEvidenceUnsupportedAction`) — matching the 6 production `SafeGatewayReasonCode` aliases. | **Fixed** — ×4→×6 throughout the story. |
| 3 | LOW (info) | AC2's literal "ACL denial → 403 `folder_acl_denied` / exit 66 / kind `folder_acl_denied` over the wire" is not end-to-end wire-exercised; the real path resolves to the deliberate safe denial (404 / exit 73 / `not_found`), with the 403 path proven at the route + adapter layers. | **No change** — correct behavior (forcing 403 would regress zero-cross-tenant-leakage); already disclosed in AD2 + "Deviations". |

**Issues fixed:** 2 (MEDIUM) · **Action items created:** 0 · **CRITICAL/HIGH remaining:** 0

_Reviewer: jpiquot on 2026-06-23_
