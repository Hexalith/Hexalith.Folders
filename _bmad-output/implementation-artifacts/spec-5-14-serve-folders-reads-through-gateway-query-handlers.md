---
title: '5.14 Serve Folders durable projection and provider reads through Gateway query handlers'
type: 'feature'
created: '2026-11-27'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-5-context.md'
  - '{project-root}/docs/contract/mcpcli-folders-operation-declarations.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** None of Story 5.12's decorated Folders Queries have registered Gateway `IDomainQueryHandler` implementations or public route-catalog entries, `VerifiedReceiptEventStoreAuthorizationValidator` only admits internal `"delegated-owner-query"` calls, and `Q02 GetBranchRefPolicy` still reads `InMemoryBranchRefPolicyReadModel` despite durable `OperationalStatusCheckpoint` storage.

**Approach:** Stand up the shared Gateway `/query` auth, V01 freshness, V02 paging, strict JSON, and redaction pipeline; promote `Q02` onto durable `EventStoreOperationalStatusReadModel`; and register 17 `IDomainQueryHandler` implementations plus public route-catalog entries for all durable projection and provider queries (`Q02, Q03, Q05, Q08, Q09, Q13, Q14, Q18, Q19, Q21–Q25, Q29, Q34, Q35`).

## Boundaries & Constraints

**Always:**
- Implement one `IDomainQueryHandler` per query type (17 handlers under `Hexalith.Folders.Queries.*`) behind `POST /query`; never use `/project` as a query dispatcher or write a query-type `switch`.
- Preserve REST v1/v2 read routes, the 3 internal owner `IDomainQueryHandler`s, and `FoldersCallbackBoundaryTests` behavior.
- Enforce deny-before-access auth via `VerifiedQueryCaller` (`Origin == "authenticated-caller"`, verified `QueryCallerAuthorityBinding` digest, tenant/principal/permission check against layered ACL and revocation) before any read-model, provider, or Memories lookup; unverified, unauthorized, cross-tenant, hidden, or revoked reads return `QueryResult.Failure("safe-denial")` (HTTP 404) with zero protected reads.
- Apply fixed V01 freshness (`read_your_writes`: Q05/Q22/Q23/Q25; `snapshot_per_task`: Q35; `eventually_consistent`: Q02/Q03/Q08/Q09/Q13/Q14/Q18/Q19/Q21/Q24/Q29/Q34) and return exact `ProjectionType` on every successful `QueryResult`.
- Enforce V02 paging/filter precedence on `Q29` (`limit` 1..1000, `cursor`, `filter`) and `Q34` (`limit` 1..500, `cursor`): accept single-source or equal payload/envelope values, reject conflicts (`InvalidCursor`/`InvalidEnvelope`), reject `Offset`, reject `QueryEnvelope.Paging` on the 15 non-paged queries, and reject unsupported `filter` values identically to REST.
- Reject unmapped JSON members, explicit JSON `null`, and `idempotencyKey`/`Idempotency-Key` before source access; enforce Q13/Q35 reserved `ProviderBindingActorId` derivation and tenant-alias rejection; recursively strip `contentBytes`, `inlineContent`, `streamDescriptor`, secrets, credential refs, and unauthorized paths.
- Follow single-type-per-file C# rules, XML docs, and zero warnings (`TreatWarningsAsErrors`).

**Never:**
- Edit `references/`, add Folders-specific McpCli host/adapter code, implement the 5 deferred file-context queries (`Q07, Q27, Q30, Q32, Q33`), the 6 deferred `InMemory*` queries (`Q01, Q11, Q16, Q26, Q28, Q31`), or the 7 excluded diagnostics (`Q04, Q06, Q10, Q12, Q15, Q17, Q20`), or activate `V2Only`.
- Return `InvalidPage` on HTTP 400 (collapses to 500 in `DaprDomainQueryInvoker`); use `InvalidEnvelope` or `InvalidCursor`.
- Edit `planning-story-manifest.yaml`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| 17 durable/provider queries | Valid `QueryEnvelope`, verified `"authenticated-caller"`, valid v2 JSON | Dedicated handler returns metadata-only JSON, exact `ProjectionType`, fixed V01 freshness, and `Paging` metadata (Q29/Q34) | N/A |
| Deny-before-access isolation | Unverified caller, forged `TenantId`/`UserId`, missing permission, revoked ACL, cross-tenant/missing resource | Zero protected read-model, provider, or Memories lookups | HTTP 404 `safe-denial`; zero payload/metadata leak |
| Stale/unavailable authority or store | Projection lags head, stale authority, or read store throws | Fails closed without disclosing protected state | Retryable `ReadModelUnavailable` (never HTTP 503 callback error) |
| V02 paging & filter conflicts | Matching vs conflicting payload/envelope `cursor`/`limit` (Q29/Q34); `Offset`; `Paging` on non-paged query; bad bounds; `filter` | Single-source/equal succeeds; conflicts, `Offset`, extra `Paging`, bad bounds, or unsupported `filter` fail pre-read | HTTP 400 `InvalidCursor` / `InvalidEnvelope` |
| Strict JSON & idempotency | Explicit JSON `null`, unknown member, `idempotencyKey` in payload, or `Idempotency-Key` header | Rejected before source access | HTTP 400 `InvalidEnvelope` |
| Q13/Q35 reserved actor | `AggregateId` vs `ProviderBindingRef`, tenant alias (`AggregateId == TenantId`), non-reserved alias | Derives `ProviderBindingActorId.Create(ProviderBindingRef)`, redacts credential ref | Reject alias/mismatch pre-read (`InvalidEnvelope` / `safe-denial`) |
| Task scope & opaque IDs | Uppercase ULID `CorrelationId`, mixed-case opaque IDs, optional `taskId` (Q05/Q08/Q22/Q24/Q34), required `taskId` (Q21) | Preserved without lowercasing; invalid/mismatched task fails closed | `InvalidEnvelope` / `safe-denial` |

## Decisions (owner, 2026-11-27)

- **D1 — Scoped authorization & PD10 manifest:** Authorized ahead of `12.2`/`12.3`/`1.17` closure (matching Story 5.13 D1). Register all new `.cs` paths in `scripts/pd10-v2-story-owned-paths.txt` and regenerate `_bmad-output/implementation-artifacts/evidence/story-12-2-v2-source-requalification-pending-2026-11-12.yaml` via `scripts/generate-pd10-v2-conformance-set.py` so `Pd10ConformanceSetTests` stays green. Keep `planning-story-manifest.yaml` and `V1Only` unchanged.
- **D2 — Narrowed scope (split):** Deliver core `/query` auth/V01/V02/redaction/catalog infrastructure, durable `Q02` upgrade, and the 17 durable projection/provider `IDomainQueryHandler`s (`Q02, Q03, Q05, Q08, Q09, Q13, Q14, Q18, Q19, Q21–Q25, Q29, Q34, Q35`). The 5 live file-context queries (`Q07, Q27, Q30, Q32, Q33`) and 6 residual `InMemory*` queries (`Q01, Q11, Q16, Q26, Q28, Q31`) are deferred to `_bmad-output/implementation-artifacts/deferred-work.md`.

</frozen-after-approval>

## Code Map

- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs:90-109` -- `POST /query` + `ToQueryHttpResult`; reject `Idempotency-Key`, preserve `400`/`404` for `FoldersCallbackBoundaryTests`, return `ReadModelUnavailable` as HTTP 200 failure (`DaprDomainQueryInvoker.cs:96-97` treats 503 as catalog refusal).
- `src/Hexalith.Folders.Server/Authentication/VerifiedReceiptEventStoreAuthorizationValidator.cs:23-44` -- admit `VerifiedQueryCaller` with `Origin == "authenticated-caller"` (domain, tenant, principal, action permission, folder/provider scope); preserve `"delegated-owner-query"` and bearer paths.
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:78-86` -- register all 17 `IDomainQueryHandler`s explicitly (pre-registered handlers skip `AddEventStoreDomainService` scan); use `Scoped` when depending on scoped services.
- `src/Hexalith.Folders/Queries/Folders/EventStoreOperationalStatusReadModel.cs:15-208` & `OperationalStatusFold.cs:103` -- durable operational projection (Q03/Q09/Q14/Q18/Q19/Q21–Q25); implement `IBranchRefPolicyReadModel` over `OperationalStatusCheckpoint.State.BranchRefPolicy` for Q02 in `AddFoldersOperationalStatusPersistence`.
- `src/Hexalith.Folders/Authorization/EventStoreFolderEffectivePermissionsReadModel.cs`, `Projections/SemanticIndexing/EventStoreSemanticIndexingBridgeStore.cs`, `Queries/ProviderReadiness/EventStoreProviderReadinessBindingReadModel.cs` -- durable projections for Q05/Q29, Q08/Q34, Q13/Q35.
- `src/Hexalith.Folders.Contracts/Routing/FoldersRuntimeCatalogDeclarations.cs:22-69` & `tests/Hexalith.Folders.Server.Tests/FoldersHumanCommandCatalogTests.cs:53-60` -- add 17 public `domain-service`/`projection` routes (`RequiresDelegatedContext = true`, matching `ProjectionType`); update catalog assertions.
- `tests/Hexalith.Folders.Server.Tests/FoldersProductionTestHost.cs`, `FoldersCallbackBoundaryTests.cs`, `McpCliProcessAdmissionAndAuthorizationTests.cs` -- reuse `FoldersProductionTestHost` + `QueryCallerAuthorityBinding` + persisted state store for HTTP `POST /query` tests.

## Tasks & Acceptance

**Execution:**
- [ ] `src/Hexalith.Folders.Server/Authentication/VerifiedReceiptEventStoreAuthorizationValidator.cs` & `FoldersDomainServiceEndpoints.cs` -- admit verified `"authenticated-caller"` Gateway queries, reject `Idempotency-Key` on `/query`, and pass `ReadModelUnavailable` without HTTP 503 catalog refusal -- enables deny-before-access Gateway read auth.
- [ ] `src/Hexalith.Folders.Server/Queries/GatewayQueryExecutionSupport.cs` (plus single-type helpers) -- centralize verified-caller validation, V01 fixed freshness, V02 paging/filter precedence, strict JSON/null/idempotency rejection, Q13/Q35 reserved actor guard, and recursive metadata-only redaction -- enforces G4/G5/V01/V02/V06 without a `switch`.
- [ ] `src/Hexalith.Folders/Queries/Folders/EventStoreOperationalStatusReadModel.cs` & `FoldersServiceCollectionExtensions.cs` -- implement `IBranchRefPolicyReadModel` over `OperationalStatusCheckpoint.State.BranchRefPolicy` -- promotes Q02 to durable EventStore projection.
- [ ] `src/Hexalith.Folders.Server/Queries/*GatewayQueryHandler.cs` (17 single-type files) & `FoldersServerServiceCollectionExtensions.cs` -- implement and register the 17 handlers (`Q02, Q03, Q05, Q08, Q09, Q13, Q14, Q18, Q19, Q21–Q25, Q29, Q34, Q35`) with exact `ProjectionType` and `QueryResponseMetadata` -- delivers durable/provider Gateway reads.
- [ ] `src/Hexalith.Folders.Contracts/Routing/FoldersRuntimeCatalogDeclarations.cs` & `tests/Hexalith.Folders.Server.Tests/FoldersHumanCommandCatalogTests.cs` -- declare all 17 public agent-eligible `domain-service`/`projection` routes and update catalog assertions -- enables Gateway dispatch.
- [ ] `tests/Hexalith.Folders.Server.Tests/McpCliGatewayQueryHandlersTests.cs` -- test all 17 handlers via `FoldersProductionTestHost` `POST /query` with `QueryCallerAuthorityBinding` and real persisted state store across every I/O matrix row -- verifies end-state behavior.
- [ ] `scripts/pd10-v2-story-owned-paths.txt` & `_bmad-output/implementation-artifacts/evidence/story-12-2-v2-source-requalification-pending-2026-11-12.yaml` -- register new `.cs` paths sorted/unique and regenerate pending manifest via `scripts/generate-pd10-v2-conformance-set.py` (D1) -- keeps `Pd10ConformanceSetTests` green.

**Acceptance Criteria:**
- Given each of the 17 durable/provider Queries dispatched to `POST /query` with a signed `QueryCallerAuthorityBinding`, then its dedicated `IDomainQueryHandler` serves authorized metadata-only JSON with exact `ProjectionType` and fixed V01 freshness, `/project` is never invoked, and deferred/excluded queries remain unregistered.
- Given unverified, cross-tenant, unauthorized, or revoked envelopes on `POST /query`, then authorization denies before any protected read-model, provider, or Memories lookup and returns byte-identical `safe-denial` with zero protected data disclosed.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `dotnet build Hexalith.Folders.CI.slnx --configuration Release -p:UseHexalithProjectReferences=false -warnaserror -m:1` -- expected: 0 warnings, 0 errors.
- `dotnet tests/Hexalith.Folders.Server.Tests/bin/Release/net11.0/Hexalith.Folders.Server.Tests.dll -noLogo -noColor` -- expected: all passed, 0 failed.
- `dotnet tests/Hexalith.Folders.Contracts.Tests/bin/Release/net11.0/Hexalith.Folders.Contracts.Tests.dll -noLogo -noColor -namespace Hexalith.Folders.Contracts.Tests.McpCli -class Hexalith.Folders.Contracts.Tests.OpenApi.Pd10ConformanceSetTests` -- expected: all passed, 0 failed.
- `dotnet tests/Hexalith.Folders.Testing.Tests/bin/Release/net11.0/Hexalith.Folders.Testing.Tests.dll -noLogo -noColor -class Hexalith.Folders.Testing.Tests.ScaffoldContractTests` -- expected: all passed, 0 failed.
