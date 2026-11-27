---
title: '5.14 Serve Folders durable projection and provider reads through Gateway query handlers'
type: 'feature'
created: '2026-11-27'
status: 'done'
baseline_commit: '27f89643d08d389dcf7861abdf31f53cf39e5788'
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
- `src/Hexalith.Folders.Contracts/Routing/FoldersRuntimeCatalogDeclarations.cs:22-69` & `tests/Hexalith.Folders.Server.Tests/FoldersHumanCommandCatalogTests.cs:53-60` -- add 17 public `domain-service`/`projection` routes (matching `ProjectionType`, with mandatory SDK caller verification at the receiver); update catalog assertions.
- `tests/Hexalith.Folders.Server.Tests/FoldersProductionTestHost.cs`, `FoldersCallbackBoundaryTests.cs`, `McpCliProcessAdmissionAndAuthorizationTests.cs` -- reuse `FoldersProductionTestHost` + `QueryCallerAuthorityBinding` + persisted state store for HTTP `POST /query` tests.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Folders.Server/Authentication/VerifiedReceiptEventStoreAuthorizationValidator.cs` & `FoldersDomainServiceEndpoints.cs` -- admit verified `"authenticated-caller"` Gateway queries, reject `Idempotency-Key` on `/query`, and pass `ReadModelUnavailable` without HTTP 503 catalog refusal -- enables deny-before-access Gateway read auth.
- [x] `src/Hexalith.Folders.Server/Queries/GatewayQueryExecutionSupport.cs` (plus single-type helpers) -- centralize verified-caller validation, V01 fixed freshness, V02 paging/filter precedence, strict JSON/null/idempotency rejection, Q13/Q35 reserved actor guard, and recursive metadata-only redaction -- enforces G4/G5/V01/V02/V06 without a `switch`.
- [x] `src/Hexalith.Folders/Queries/Folders/EventStoreOperationalStatusReadModel.cs` & `FoldersServiceCollectionExtensions.cs` -- implement `IBranchRefPolicyReadModel` over `OperationalStatusCheckpoint.State.BranchRefPolicy` -- promotes Q02 to durable EventStore projection.
- [x] `src/Hexalith.Folders.Server/Queries/*GatewayQueryHandler.cs` (17 single-type files) & `FoldersServerServiceCollectionExtensions.cs` -- implement and register the 17 handlers (`Q02, Q03, Q05, Q08, Q09, Q13, Q14, Q18, Q19, Q21–Q25, Q29, Q34, Q35`) with exact `ProjectionType` and `QueryResponseMetadata` -- delivers durable/provider Gateway reads.
- [x] `src/Hexalith.Folders.Contracts/Routing/FoldersRuntimeCatalogDeclarations.cs` & `tests/Hexalith.Folders.Server.Tests/FoldersHumanCommandCatalogTests.cs` -- declare all 17 public agent-eligible `domain-service`/`projection` routes and update catalog assertions -- enables Gateway dispatch.
- [x] `tests/Hexalith.Folders.Server.Tests/McpCliGatewayQueryHandlersTests.cs` -- test all 17 handlers via `FoldersProductionTestHost` `POST /query` with `QueryCallerAuthorityBinding` and real persisted state store across every I/O matrix row -- verifies end-state behavior.
- [x] `scripts/pd10-v2-story-owned-paths.txt` & `_bmad-output/implementation-artifacts/evidence/story-12-2-v2-source-requalification-pending-2026-11-12.yaml` -- register new `.cs` paths sorted/unique and regenerate pending manifest via `scripts/generate-pd10-v2-conformance-set.py` (D1) -- keeps `Pd10ConformanceSetTests` green.

**Acceptance Criteria:**
- Given each of the 17 durable/provider Queries dispatched to `POST /query` with a signed `QueryCallerAuthorityBinding`, then its dedicated `IDomainQueryHandler` serves authorized metadata-only JSON with exact `ProjectionType` and fixed V01 freshness, `/project` is never invoked, and deferred/excluded queries remain unregistered.
- Given unverified, cross-tenant, unauthorized, or revoked envelopes on `POST /query`, then authorization denies before any protected read-model, provider, or Memories lookup and returns byte-identical `safe-denial` with zero protected data disclosed.

## Implementation Notes

- The public schema-v3 catalog is sealed with the compiled Folders declarations in the signed callback tests. EventStore 5.5.1 reserves `RequiresDelegatedContext` for internal operations. The in-repo Gateway enrolls the 17 wire types for signed caller capture, and the receivers independently require the SDK-verified `authenticated-caller` binding.
- Optional task scopes require current durable evidence linking the caller and task to the folder and the supplied workspace. Q21 uses the existing durable task-to-folder binding so retained tasks remain readable within the authorized folder. The indexed search requires an authorized current workspace even when the task is omitted, and its continuation requires a visible hydrated result in the next bounded window.
- Public delegated calls receive safe denial because EventStore's authenticated-caller envelope does not carry independent grants for both principals. The internal owner callbacks retain their existing delegation protocol.
- The Folders Gateway maps the exact unavailable callback sentinel to a retryable HTTP 503 for its enrolled query routes. Shared EventStore ingress owns preservation of empty paging and blank cursor inputs, rejection of the original idempotency header, and the portable retry mapping.
- A Platform-owned Gateway needs the same query authority enrollment and a workload issuer that binds the query. Generic McpCli aggregate routing needs the reserved actor derivation for Q13 and Q35. The integration handoff is documented in [the operation declarations](../../docs/contract/mcpcli-folders-operation-declarations.md#story-514-gateway-runtime-enrollment).

## Spec Change Log

- 2026-11-27. Corrected the Code Map's public route flag to match EventStore's schema-v3 validator. The approved intent and acceptance criteria are unchanged.

## Review Triage Log

| Reviewer | Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- | --- |
| Blind | Platform Gateway authority enrollment | medium | defer | Platform composition currently enrolls Conversations query authority. Without Folders enrollment, it sends no verified caller binding for these wire types. |
| Blind | Generic McpCli provider actor derivation | medium | defer | The generic aggregate accessor uses the public reference and rejects a differing explicit ID. Provider callbacks require the exact derived reserved actor. |
| Blind | Gateway maps unavailable callback to HTTP 500 | medium | patch | EventStore 5.5.1 preserves the closed failure detail on its query exception but classifies it as internal error. The local Folders Gateway can safely recognize the exact sentinel. |
| Blind | Public ingress discards empty paging and blank cursors | medium | defer | EventStore normalizes these values before constructing the callback envelope. Their presence cannot be recovered by the Folders receiver. |
| Blind | Public ingress drops the idempotency header | medium | defer | The EventStore controller ignores the incoming header and creates a separate Dapr callback. The receiver can reject only a header present at its boundary. |
| Blind | Delegated caller admitted by a single-principal check | high | patch | The authenticated-caller binding omits a separately verified delegator grant. Current public checks authorize only the subject. |
| Blind | Q35 accepts historical-only authority | high | patch | The candidate catalog requires `create_folder`. The gateway adapter currently checks `provider_readiness_read` before provider resolution. |
| Blind | Q05 collapses distinct ACL actions before intersecting | high | patch | `manage_folder_access` and `archive_folder` both map to administer, so matching levels can imply an action the requester never held in both sets. |
| Blind | Q21 refuses a retained older task | medium | patch | The current-workspace predicate precedes the durable task reader, which already verifies folder binding, retention, source applicability and completion evidence. |
| Blind | Q34 probes Memories for a foreign workspace without a task | high | patch | The existing ownership check runs only with a supplied task and the search port receives the unverified workspace from the payload. |
| Blind | Q34 continuation reveals a foreign tail | medium | patch | The shared search handler uses raw source totals past a full page. The callback fixture proved that one foreign-only next page yielded a continuation. |
| Blind | Q25 includes commit and reconciliation evidence | high | patch | The adapter serializes the internal provider outcome directly. REST omits those fields and the evidence operations separately require `query_status`. |
| Edge | Task-scoped Q05 can report an unshared permission level | high | patch | Exact ACL and signed actions must be intersected before projection to permission levels. |
| Edge | Q21 denies retained task outcomes | medium | patch | The checkpoint's current task is narrower than the durable task row and its source proof. |
| Edge | Q34 omitted task bypasses workspace membership verification | high | patch | The untrusted workspace is used as search scope even when the task guard does not run. |
| Edge | Provider payload reference equal to tenant | medium | patch | The callback compares the raw reference to the tenant in addition to validating the derived reserved aggregate. Only the aggregate alias needs to fail. |
| Edge | Valid workspace entity rejected | medium | patch | Workspace-shaped payloads already carry an exact scope, but several dedicated handlers do not supply it for the envelope entity check. |
| Edge | Internal owner callbacks receive new header validation | medium | patch | Idempotency validation in the common endpoint intercepts delegated owner traffic before dispatch. |
| Edge | Missing caller proof with idempotency header returns HTTP 400 | medium | patch | The common endpoint validates the header before the SDK establishes caller authority. |
| Edge | Task predicate reads the current checkpoint as authority | false | reject | Complete folder ACL and revocation checks run first. The checkpoint proves the scope within that authorized folder and the requested query view remains untouched when the predicate denies. The task matrix requires a closed denial for mismatch. |
| Verification | Tests bypass real Gateway wire-type enrollment | medium | patch | The callback fixture manufactures a signed origin, so removal of the production configuration would leave it green. |
| Verification | Task test never varies an authorized caller | medium | patch | The test covers bad task and workspace IDs, but its other principal lacks the relevant folder grant. A caller with a valid grant and another actor's task exercises the ownership check. |
| Verification | Q05 permissions array is unasserted | medium | patch | Existing callbacks assert folder and freshness. Scoped and unscoped permission arrays exercise the action intersection. |
| Verification | Q35 missing-binding denial lacks an evidence-state assertion | medium | patch | An unintended evidence write would preserve both the denial body and provider call count. |
| Verification | Q03 test misses the projected digest | medium | patch | The test seeds the digest but checks only the operation ID, so the fallback placeholder could silently replace the evidence. |

## Verification

**Commands:**
- `dotnet build Hexalith.Folders.CI.slnx --configuration Release -p:UseHexalithProjectReferences=false -warnaserror -m:1` -- passed with 0 warnings and 0 errors.
- `dotnet tests/Hexalith.Folders.Server.Tests/bin/Release/net11.0/Hexalith.Folders.Server.Tests.dll -noLogo -noColor` -- 1,586 passed with 0 errors, failures, or skips. The default run did not select 11 pre-existing explicit Keycloak cases.
- `dotnet tests/Hexalith.Folders.Contracts.Tests/bin/Release/net11.0/Hexalith.Folders.Contracts.Tests.dll -noLogo -noColor -namespace Hexalith.Folders.Contracts.Tests.McpCli` -- 51 passed.
- `dotnet tests/Hexalith.Folders.Contracts.Tests/bin/Release/net11.0/Hexalith.Folders.Contracts.Tests.dll -noLogo -noColor -class Hexalith.Folders.Contracts.Tests.OpenApi.Pd10ConformanceSetTests` -- 21 passed.
- `dotnet tests/Hexalith.Folders.Testing.Tests/bin/Release/net11.0/Hexalith.Folders.Testing.Tests.dll -noLogo -noColor -class Hexalith.Folders.Testing.Tests.ScaffoldContractTests` -- 20 passed.

The final Gateway host and retry-mapping checks passed 14 tests. The focused core lane passed 81 tests across branch policy, operational persistence, effective permission scope, and indexed search. The final signed callback matrix and redaction tests passed against Dapr 1.19.1 with SQLite persistence. The disposable database contained 187 state rows, and the matrix checked persisted source values before querying all 17 handlers.

The local topology baseline command `run-with-temp aspire start --non-interactive --apphost src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj` stopped during the build. Tenants reported `Catalog SDK selection '5.5.1' is not qualified.` The Gateway host test verifies actual configured caller capture. A live Platform workload assertion issuer remains outside the verified scope.
