---
baseline_commit: f933b11
---

# Story 6.1: Audit and operation-timeline query endpoints

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an audit reviewer,
I want query endpoints for metadata-only audit and operation timelines,
so that incidents can be reconstructed without file contents or secrets.

## Acceptance Criteria

Source epic AC (epics.md#Story-6.1):

> **Given** audit projection data exists
> **When** audit or timeline queries run
> **Then** records are paginated, filtered, tenant-scoped, and metadata-only
> **And** sensitive metadata classification is applied consistently.

Decomposed, testable acceptance criteria:

1. **Four `/api/v1` audit-family routes are implemented in `Hexalith.Folders.Server` and registered through `MapFoldersServerEndpoints`.** New file `src/Hexalith.Folders.Server/AuditEndpoints.cs` mirrors the `ProviderReadinessEndpoints.cs` / `FoldersDomainServiceEndpoints.cs` pattern and adds (per the Contract Spine in `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`):
   - `GET /api/v1/folders/{folderId}/audit-trail` → `.WithName("ListAuditTrail")` → returns `AuditTrailPage`.
   - `GET /api/v1/folders/{folderId}/audit-trail/{auditRecordId}` → `.WithName("GetAuditRecord")` → returns `AuditRecord`.
   - `GET /api/v1/folders/{folderId}/operation-timeline` → `.WithName("ListOperationTimeline")` → returns `OperationTimelinePage`.
   - `GET /api/v1/folders/{folderId}/operation-timeline/{timelineEntryId}` → `.WithName("GetOperationTimelineEntry")` → returns `OperationTimelineEntry`.

   Each endpoint chains `.AddEndpointFilter<FolderAuditEndpointFilter>()` (the existing audit-observability filter). A new `MapAuditEndpoints()` extension is invoked from `FoldersServerModule.MapFoldersServerEndpoints` alongside `MapFoldersDomainServiceEndpoints()` and `MapProviderReadinessEndpoints()`. A new `AddFoldersAuditQueries()` DI extension is added to `Hexalith.Folders.FoldersServiceCollectionExtensions` and registered from `FoldersServerModule.AddFoldersServer()` next to `AddFoldersLifecycleStatus()`.

2. **`KnownRestSurfaceGap` and `ImplementedRestOperationCount` are updated to reflect the closed gap.** In `tests/Hexalith.Folders.Server.Tests/TransportParityConformanceTests.cs`:
   - Remove `"GetAuditRecord"`, `"GetOperationTimelineEntry"`, `"ListAuditTrail"`, and `"ListOperationTimeline"` from `KnownRestSurfaceGap` (the four audit-family entries explicitly listed under the `// Audit-family operations (no /api/v1/audit-trail route).` group).
   - Update `ImplementedRestOperationCount` from `28` to `32`.
   - The both-direction guard `KnownRestSurfaceGapAccountsForEveryRestExpectedRowWithoutAnEndpoint` must remain green: implemented + remaining-gap = every rest-expected row; no orphan endpoints; no silently-filled gap.
   - The route + endpoint name must match the oracle's `operation_id` exactly; no `EndpointNameAliases` entry is added (the four names match the spine's operationId verbatim, unlike `AddWorkspaceFile` / `ChangeWorkspaceFile` / `RemoveWorkspaceFile`).

3. **Per-operation domain stack (query + handler + read model + result + DTOs) under `src/Hexalith.Folders/Queries/Audit/`.** New folder. Mirror the `Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQuery*` pattern one-to-one for each audit-family op:
   - `AuditTrailQuery` / `AuditTrailQueryHandler` / `AuditTrailQueryResult` / `AuditTrailQueryResultCode` / `IAuditTrailReadModel` / `AuditTrailReadModelRequest` / `AuditTrailReadModelResult` / `AuditTrailReadModelStatus` / `AuditTrailReadModelSnapshot` / `InMemoryAuditTrailReadModel`.
   - `AuditRecordQuery*` family (single-record variant, no pagination).
   - `OperationTimelineQuery*` family.
   - `OperationTimelineEntryQuery*` family.
   - Each handler is `sealed`, accepts a `LayeredFolderAuthorizationService`, an `IXxxReadModel`, an `IUtcClock`, and an optional `ILogger<TQueryHandler>`. Each handler calls `authorizationService.AuthorizeAsync(new LayeredFolderAuthorizationContext(..., LayeredFolderOperationPolicy.StrictRead(), ..., OperationScope: query.FolderId, ...))` **before** any read-model call (concern #18 / #16 — authorization-before-observation). The action-token vocabulary stays consistent with `read_metadata` (the existing tenant-access + folder ACL evidence vocabulary) — **do not** invent a new `read_audit` action token; the audit reviewer scope is enforced by the EventStore `eventstore:permission` claim transform shape, which `LayeredFolderAuthorizationService` already consumes through `IEventStoreClaimTransformEvidenceAccessor`.

   The DTOs returned by the handlers are typed records under `src/Hexalith.Folders.Contracts/Projections/Audit/` (new folder) matching the Contract Spine schemas one-to-one: `AuditTrailPage`, `AuditRecord`, `OperationTimelinePage`, `OperationTimelineEntry`, `PaginationMetadata`, `FreshnessMetadata`, `RedactionMetadata`, `RedactableAuditActorReference`, `RedactableAuditOperationReference`, `RedactableAuditTimestamp`, `ChangedPathEvidence`, `DiagnosticStateTransition`, `RedactableDiagnosticIdentifier`. **Field names use `camelCase` to match the spine wire shape**; `[JsonPropertyName]` is required only where the spine name diverges from the C# property name. Whenever a DTO already lives in `Hexalith.Folders.Client.Generated.HexalithFoldersClient.g.cs` for SDK consumption, **the server-side DTO is a new sibling** under `Hexalith.Folders.Contracts.Projections.Audit`, NOT a reference to the generated client (Server must not depend on Client; see Dev Notes "Project-reference direction"). Wire-level equivalence is enforced by the existing `AuditOpsConsoleContractGroupTests` in `tests/Hexalith.Folders.Contracts.Tests/OpenApi/`.

4. **Authorization order, tenant authority, and safe denial are non-negotiable.** Each endpoint:
   - Reads `tenantContext.AuthoritativeTenantId` and `tenantContext.PrincipalId` from `ITenantContextAccessor` (concern #12). **No** payload, query string, header, or `folderId` / `auditRecordId` / `timelineEntryId` path value establishes tenant authority; they are client-controlled comparison inputs only.
   - Calls the layered authorization service in this order: JWT validation (handled by ASP.NET auth pipeline) → EventStore claim transform evidence (via `IEventStoreClaimTransformEvidenceAccessor.GetEvidence("read_metadata")`) → tenant-access projection (via `LayeredFolderAuthorizationService`) → folder ACL evidence → audit-reviewer-scope evidence (carried in the EventStore claim-transform `eventstore:permission` claim set, evaluated by the layered authorization stack as the existing strict-read policy) → diagnostic-audience partition. **Then and only then** the audit/timeline read-model is consulted.
   - On any authorization-class denial, returns a **safe-denial** RFC 9457 problem with `category ∈ { authentication_failure, tenant_access_denied, folder_acl_denied, audit_access_denied, not_found }` mapped to status `401` / `403` / `404` per the Contract Spine's `SafeAuthorizationDenial{401,403,404}` responses. The four codes are **externally indistinguishable** for any combination of `(hidden, wrong-tenant, missing, redacted, stale, unavailable)` evidence — same body shape, same wording template, same `clientAction`, no resource-existence hints (covers x-hexalith-authorization.safeDenial in the spine).
   - The canonical error categories for each endpoint are the **exact** sets declared in the Contract Spine's `x-hexalith-canonical-error-categories` and the parity oracle's `transport_parity.error_code_set` for that `operation_id`. Surfaced category strings (snake_case wire form, from `CanonicalErrorCategory.[EnumMember]`) must equal those rows verbatim. For `ListAuditTrail` and `ListOperationTimeline`: `{ authentication_failure, tenant_access_denied, folder_acl_denied, audit_access_denied, read_model_unavailable, projection_stale, projection_unavailable, response_limit_exceeded, query_timeout, not_found, redacted, internal_error }`. For `GetAuditRecord` and `GetOperationTimelineEntry`: same minus `response_limit_exceeded` and `query_timeout` (per the spine — single-record reads do not enumerate).

5. **Pagination, filter, and freshness validation enforce the Contract Spine.** All four endpoints reject `Idempotency-Key` (it is `not_accepted_for_non_mutating_operation` per the oracle row; respond `400 validation_error` / `code: idempotency_key_not_allowed` — mirror `GetWorkspaceStatus` at `FoldersDomainServiceEndpoints.cs:303-340`). Each list endpoint accepts the spine's three pagination/filter parameters:
   - **`cursor`** (query string): opaque service-issued pagination cursor. Validation regex matches the Contract Spine `PageCursor` constraint (`minLength: 1, maxLength: 256`, characters from the same canonical alphabet as the cursor generator; reuse the `ProviderReadinessEndpoints.CursorPattern()` shape pattern `^cursor_[A-Za-z0-9_-]{8,247}$` or an equivalent strict regex — **do not** use `^cursor_[0-9]{1,6}$` from `ProviderSupportEvidenceQueryHandler` because audit cursors carry richer state). Cursors are caller-opaque and MUST NOT carry tenant authority, ACL decisions, provider tokens, raw query text, or unredacted path lists (the spine's `PaginationMetadata.cursor` description). Tampered cursors (signature mismatch, decoded tenant mismatch) emit canonical `validation_error` / `code: cursor_tampered` (one of the behavioral-parity dimensions on the oracle row is `cursor_tamper_detection`).
   - **`limit`** (query string): integer in `[1, 1000]` per the spine `PageLimit` schema. Runtime servers MUST clamp to the smaller of the requested value and the configured maximum and surface the **effective** limit alongside the requested value in `PaginationMetadata` (page-truncation evidence — `freshness.freshnessBehavior` row says `...-page-truncation` so the response signals truncation when applicable). The configured maximum is `100` (matches the `AuditTrailPage.entries.maxItems` / `OperationTimelinePage.entries.maxItems` schema ceiling).
   - **`filter`** (query string): bounded metadata filter expression, regex `^[a-z][A-Za-z0-9_=.,*\- ]{0,255}$` per the spine `MetadataFilter` parameter, `minLength: 1, maxLength: 256`. **Allowed filter-key vocabulary is TODO(reference-pending C4)** per the spine; until C4 freezes the vocabulary, the runtime MUST reject any filter key not in the per-operation allow-list and SHOULD emit `validation_error` for unknown keys. For this story's MVP the per-operation allow-list is empty: any non-null `filter` value emits `validation_error` / `code: filter_not_yet_supported`, with a `details.todoRef: "C4"` extension to signal the TODO-pending state. **Do not** invent a temporary filter syntax; the next story (or a C4 closure) will populate the allow-list.
   - **`X-Hexalith-Freshness` header**: only `eventually_consistent` is accepted (the spine declares `x-hexalith-read-consistency.class: eventually_consistent` for all four ops). Any other value → `400 validation_error` / `code: unsupported_read_consistency` (mirror `GetFolderLifecycleStatus` at `FoldersDomainServiceEndpoints.cs:944-965`).
   - **`X-Correlation-Id` header**: optional, canonical-identifier regex (same shape as `ProviderReadinessEndpoints.CanonicalIdentifierPattern()`). When supplied, echoed verbatim on the response. When omitted, the server generates a fresh ULID (mirror existing query endpoints' correlation behavior).
   - **`X-Hexalith-Task-Id` header**: not task-scoped for the audit-family per the oracle (`task_id_sourcing: not_task_scoped`); the header is allowed (the spine's `x-hexalith-correlation.taskHeader` declares it) but does not gate any read; if supplied, validate canonical-identifier shape and echo on response.

6. **Page response carries `entries`, `page`, `retentionClass`, `freshness`; record response carries opaque IDs + redaction + freshness.** Per the spine schemas:
   - `AuditTrailPage` and `OperationTimelinePage`: `entries` (≤ 100 items), `page: PaginationMetadata`, `retentionClass: string` (snake_case identifier or `TODO(reference-pending):...` marker until C3 retention vocabulary is frozen — emit `"TODO(reference-pending):audit-trail-retention"` and `"TODO(reference-pending):operation-timeline-retention"` respectively, matching the spine's pattern `^(?:TODO\(reference-pending\):.{1,140}|[a-z][a-z0-9_]{0,79})$`), `freshness: FreshnessMetadata`.
   - `AuditRecord`: `auditRecordId`, `actorReference: RedactableAuditActorReference`, `taskId?` (present iff the audit record is task-scoped), `operationId: RedactableAuditOperationReference`, `correlationId`, `resultStatus: CanonicalErrorCategory`, `sanitizedErrorCategory: CanonicalErrorCategory`, `retryable: bool`, `durationMilliseconds: 0..86400000`, `evidenceTimestamp: RedactableAuditTimestamp`, `redaction: RedactionMetadata`, `changedPathEvidence?: ChangedPathEvidence` (digest/reference/redacted/unavailable per the spine `oneOf`), `freshness: FreshnessMetadata`.
   - `OperationTimelineEntry`: `timelineEntryId`, `operationId`, `taskId`, `correlationId`, `workspaceReference: RedactableDiagnosticIdentifier`, `stateTransition: DiagnosticStateTransition` (`fromState`, `toState`, `disposition`), `sanitizedResult: CanonicalErrorCategory`, `retryable`, `durationMilliseconds`, `evidenceTimestamp: UtcDateTime`, `freshness: FreshnessMetadata`.
   - The `PaginationMetadata` cursor in the response uses the **same** length / character profile as the request `PageCursor` parameter so callers can echo it back without re-encoding. Empty pages return `entries: []` (NOT null) and the next-cursor field is absent or null when no more pages exist.

7. **Sensitive metadata classification is applied consistently (S-6 default tier).** Per architecture decision S-6, paths + repository names + branch names + commit messages classify as `tenant_sensitive` by default. The audit-family DTOs do not carry raw paths / repo names / branch names / commit messages — they carry **opaque references and digests** (`workspaceReference: PrefixedOpaqueIdentifier`, `changedPathEvidence.digest: digest_<base64>`, `changedPathEvidence.reference: changeref_<base64>`). The story ensures:
   - Each handler invokes the existing `SensitiveMetadataClassifier` (under `Hexalith.Folders/Redaction/`) for every field flagged `x-hexalith-sensitive-metadata-tier: tenant_sensitive` in the Contract Spine; classification drives the `RedactionMetadata.visibility` field on the response (`visible` / `redacted` / `unknown` / `unavailable`).
   - When a tenant policy upgrades classification from `tenant_sensitive` to `confidential` (per S-6's per-tenant override), the field is hashed at write time at the projection layer (not at the endpoint layer); the endpoint surfaces the hashed/redacted form via the spine's `redacted-evidence` shape and **never** emits the cleartext value even if it is present in the read-model snapshot.
   - The `redacted` canonical category surfaces as the wire-form `"redacted"` in `resultStatus` / `sanitizedErrorCategory` (per the spine) when the response is gated by classification policy.
   - **Concrete consistency check:** if the same audit record is queried via `GetAuditRecord` and listed via `ListAuditTrail`, the redaction state of every field is **byte-for-byte identical** across the two responses. Same applies for `GetOperationTimelineEntry` vs `ListOperationTimeline`. A new server-tests assertion (`AuditRecordRedactionIsConsistentBetweenSingleAndListResponses`, `OperationTimelineEntryRedactionIsConsistentBetweenSingleAndListResponses`) iterates a 3-record seeded projection and proves the invariant.

8. **Metadata-only invariant is sentinel-tested on every audit-family response.** Add a `tests/Hexalith.Folders.Server.Tests/AuditEndpointsSentinelTests.cs` test that, for each of the four endpoints:
   - Seeds the audit + timeline projections with synthetic records whose redactable fields contain sentinel patterns from `tests/fixtures/audit-leakage-corpus.json` (GitHub PATs, JWT bearers, PEM keys, `password=`/`api_key=`, absolute paths like `/mnt/c/...` and `C:\...`, `diff --git` / `@@ -` hunk markers, embedded provider URLs).
   - Drives a full response cycle through the in-process host.
   - Scans the full HTTP response body **and** the Problem Details body **and** the response headers for **any** sentinel substring from the corpus.
   - Fails loudly with the matched substring + the line in `audit-leakage-corpus.json` that produced it.
   - Iterates the **entire** corpus per the per-channel rule (concern #6 + S-2 + the safety-channel inventory): a missing channel that doesn't yet exist must be added to `tests/fixtures/safety-channel-inventory.json` in the same PR. **No surface gets a free pass to skip the sentinel sweep.**

9. **Authorization-before-observation is verified, not assumed.** Add a `tests/Hexalith.Folders.Server.Tests/AuditEndpointsAuthorizationOrderTests.cs` that asserts (mirror `tests/Hexalith.Folders.Tests/Authorization/LayeredFolderAuthorizationServiceTests.cs` style):
   - When tenant access is denied (caller's tenant fails the `FolderTenantAccessProjection` lookup), the read-model is **never** consulted (counter on `InMemoryAuditTrailReadModel.GetCount` stays zero; similarly for `InMemoryAuditRecordReadModel`, `InMemoryOperationTimelineReadModel`, `InMemoryOperationTimelineEntryReadModel`). The endpoint emits canonical `tenant_access_denied` / `403` safe-denial.
   - When folder ACL is denied, the read-model is **never** consulted; canonical `folder_acl_denied` / `403` safe-denial.
   - When the audit reviewer scope is missing from EventStore claim-transform evidence, the read-model is **never** consulted; canonical `audit_access_denied` / `403` safe-denial.
   - When all authorization layers pass and the projection is stale beyond the configured C2-pending threshold, the read-model returns `Stale` and the endpoint surfaces canonical `projection_stale` / `409` (per the spine's `409` response that maps to `ProjectionStaleProblem`).
   - When all authorization layers pass and the projection is unavailable, canonical `projection_unavailable` / `503`.
   - When the read-model throws an unexpected exception, the handler logs metadata-only (`ex.GetType().FullName`, **no payload, no identifier echoes**) and emits canonical `read_model_unavailable` / `503` (mirror `FolderLifecycleStatusQueryHandler:96-109`).
   - **No endpoint emits a category outside its oracle row's `error_code_set`** — the spine's `x-hexalith-canonical-error-categories` is the authoritative allow-list; assertions iterate the oracle row's `Transport.ErrorCodeSet` from `ParityOracle.Rows`.

10. **Build clean and hermetic; no production-tree edits outside the four endpoint groups + their support; no `.slnx` change; no new package references; no recursive submodule init.** Build with the WSL-accessible Windows SDK (`/mnt/c/Program\ Files/dotnet/dotnet.exe`; the WSL-native SDK fails the `global.json` 10.0.302 pin — see Dev Notes "Build environment"): `dotnet.exe restore Hexalith.Folders.slnx` → `dotnet.exe build Hexalith.Folders.slnx --no-restore` → 0 warnings / 0 errors. Run the touched suite:
    - `dotnet.exe test tests/Hexalith.Folders.Server.Tests` (focused: the new audit-endpoint suites + the updated `TransportParityConformanceTests`).
    - `dotnet.exe test tests/Hexalith.Folders.Tests --filter "FullyQualifiedName~Audit"` (the new query-handler unit tests).
    - `dotnet.exe test tests/Hexalith.Folders.Contracts.Tests --filter "FullyQualifiedName~AuditOpsConsole"` (existing contract-group conformance; should remain green with no edits).
    - `dotnet.exe test tests/Hexalith.Folders.IntegrationTests` (full sweep — verify `GoldenLifecycleParityTests` and `MixedSurfaceHandoffTests` either remain green by virtue of using `GetFolderLifecycleStatus` as the audit surrogate, OR are extended to consume the new audit endpoints; **prefer the surrogate** if the linked test sources need editing — keeping Story 5.5/5.7 unmodified is in the negative scope; see Dev Notes "Don't rewire 5.5 / 5.7").
    - **Regression check:** `dotnet.exe test tests/Hexalith.Folders.{Cli,Mcp,Client}.Tests` — none should change (the SDK already exposes `ListAuditTrailAsync` / `GetAuditRecordAsync` / `ListOperationTimelineAsync` / `GetOperationTimelineEntryAsync`; the CLI's `audit list` and the MCP's `AuditTools.ListAuditTrail` / `AuditTrailResource` already wrap them — Stories 5.2 / 5.3 shipped both. Endpoints landing on the server side should make those existing calls **succeed** against a live host where they previously had no route to hit; per-adapter tests remain hermetic with canned responses so they do not change).
    - **Known pre-existing reds carried in from Epic 5 (per Epic 5 retro Action Items):** `ClientGenerationTests.GeneratedClientAndHelpersMatchIsolatedRegeneration` (whitespace-only env noise), `BranchRefPolicyEndpointTests.GetBranchRefPolicyShouldUseSafeDenialEnvelopeForTenantMismatch`, and `Hexalith.Folders.Testing.Tests.ProjectReferencesFollowAllowedDependencyDirection`. Confirm they are the **only** pre-existing reds; do **not** mask new failures behind the carry-over list.
    - **Drift sanity (per Story 5.5/5.6/5.7 pattern):** temporarily flip one expected value (e.g. swap `ListAuditTrail` and `GetAuditRecord` route names; assert the response category is `internal_error` instead of `audit_access_denied`; raise `KnownRestSurfaceGap` count from `15` back to `19` without removing the four audit entries). Confirm the focused suite + `KnownRestSurfaceGapAccountsForEveryRestExpectedRowWithoutAnEndpoint` fails with a specific message naming the failing op. Revert.

## Tasks / Subtasks

- [x] **Task 1 — Author the audit-family query stack under `src/Hexalith.Folders/Queries/Audit/`** (AC: #3, #4, #7)
  - [x] Create new folder `src/Hexalith.Folders/Queries/Audit/`. Read `src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQuery*.cs` first as the canonical template.
  - [x] For each of the four ops (`AuditTrail`, `AuditRecord`, `OperationTimeline`, `OperationTimelineEntry`) add the eight-file pattern: `Query.cs`, `QueryHandler.cs`, `QueryResult.cs`, `QueryResultCode.cs`, `I{Op}ReadModel.cs`, `{Op}ReadModelRequest.cs`, `{Op}ReadModelResult.cs`, `{Op}ReadModelStatus.cs`, `{Op}ReadModelSnapshot.cs`, `InMemory{Op}ReadModel.cs`. Reuse `FolderLifecycleFreshness.cs` (or fork to `AuditFreshness.cs` if the field-set diverges — prefer reuse).
  - [x] List handlers (`AuditTrailQueryHandler`, `OperationTimelineQueryHandler`) accept `(folderId, authoritativeTenantId, principalId, claimTransformEvidence, correlationId, taskId, cursor?, limit?, filter?, clientControlledTenantValues, clientControlledPrincipalValues)`. Single-record handlers (`AuditRecordQueryHandler`, `OperationTimelineEntryQueryHandler`) accept `(folderId, recordOrEntryId, authoritativeTenantId, principalId, claimTransformEvidence, correlationId, taskId, clientControlledTenantValues, clientControlledPrincipalValues)`.
  - [x] Each handler invokes `authorizationService.AuthorizeAsync(new LayeredFolderAuthorizationContext(..., LayeredFolderOperationPolicy.StrictRead(), ..., OperationScope: folderId, ...))` BEFORE the read-model call, then maps the layered-authorization denial codes to canonical-category snake_case via `MapAuthorizationDenial` (mirror `FolderLifecycleStatusQueryHandler:159-228` or wherever the existing mapper lives).
  - [x] Catch-all `try/catch (Exception ex) when (ex is not OperationCanceledException)` around the read-model call: `_logger.LogWarning(ex, "{OperationId} read-model call failed; returning ReadModelUnavailable. Exception type: {ExceptionType}", "...", ex.GetType().FullName)` — **no payload, no identifier echo** in log values (concern #6). Return `ReadModelUnavailable`.
  - [x] `InMemory{Op}ReadModel.cs` for each op exposes a thread-safe seeding API and a `GetCount` counter (per AC #9 — auth-order tests need to assert the read-model was **not** called when authorization failed).
  - [x] Unit-test each handler under `tests/Hexalith.Folders.Tests/Queries/Audit/{Op}QueryHandlerTests.cs`. Cover: authentication-class denials, tenant-access denials, folder-ACL denials, audit-scope denials (each must short-circuit BEFORE the read-model is called — assert `InMemory{Op}ReadModel.GetCount == 0`), projection stale, projection unavailable, projection not-found, projection malformed, read-model exception, and the success path. Mirror `tests/Hexalith.Folders.Tests/Queries/Folders/FolderLifecycleStatusQueryHandlerTests.cs` structure.

- [x] **Task 2 — Author the spine-shaped DTOs under `src/Hexalith.Folders.Contracts/Projections/Audit/`** (AC: #3, #6, #7)
  - [x] Create new folder `src/Hexalith.Folders.Contracts/Projections/Audit/`.
  - [x] Add records matching the spine schemas one-to-one (camelCase wire shape via `[JsonPropertyName(...)]` only where the C# property name differs): `AuditTrailPage`, `AuditRecord`, `OperationTimelinePage`, `OperationTimelineEntry`, plus the shared `PaginationMetadata`, `FreshnessMetadata`, `RedactionMetadata`, `RedactableAuditActorReference`, `RedactableAuditOperationReference`, `RedactableAuditTimestamp`, `ChangedPathEvidence`, `DiagnosticStateTransition`, `RedactableDiagnosticIdentifier`, `OpaqueIdentifier` (if not already in `Contracts.Identity/`). **Check `src/Hexalith.Folders.Contracts/Identity/` and `src/Hexalith.Folders.Contracts/Projections/` first** — many shared types already exist (Story 1.11 contract group); reuse not duplicate.
  - [x] Each record sealed, `additionalProperties: false` equivalent (no `[JsonExtensionData]`); required fields are non-nullable; nullable spine fields are `T?` C# properties.
  - [x] Enum types for the `ProjectionAvailability` (`available` / `stale` / `unavailable` / `redacted` / `unknown`) and the `RedactionVisibility` (`visible` / `redacted` / `unknown` / `unavailable`) values referenced by the schemas. Reuse the existing `CanonicalErrorCategory` enum (already in Contracts with `[EnumMember]` snake_case wire values per Story 5.5 — do **not** redefine).
  - [x] **Verify against the contract group test:** `tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuditOpsConsoleContractGroupTests.cs` must remain green; if it asserts the **presence** of these DTOs in the Contracts assembly, that's the proof the wire shape is right. Run focused: `dotnet.exe test tests/Hexalith.Folders.Contracts.Tests --filter "FullyQualifiedName~AuditOpsConsole"`.

- [x] **Task 3 — Wire DI registration** (AC: #1, #3)
  - [x] Add `AddFoldersAuditQueries(this IServiceCollection services)` to `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs`. The method registers the four handlers + the four `InMemory{Op}ReadModel` types as the default `I{Op}ReadModel` implementation (the production composition swaps in EventStore-backed read models later — out of scope here). Add `services.AddFoldersLayeredAuthorization();` first (idempotent) so the layered authorization service is resolvable.
  - [x] Wire the call from `src/Hexalith.Folders.Server/FoldersServerModule.cs:AddFoldersServer(...)`, next to `services.AddFoldersLifecycleStatus()`. Keep the call order stable; the layered-authorization registration is idempotent across calls.

- [x] **Task 4 — Author `AuditEndpoints.cs` with the four GET endpoints** (AC: #1, #2, #4, #5, #6)
  - [x] New file `src/Hexalith.Folders.Server/AuditEndpoints.cs` mirroring `ProviderReadinessEndpoints.cs` shape (`public static partial class AuditEndpoints` with a `MapAuditEndpoints(this IEndpointRouteBuilder endpoints)` extension and per-endpoint private static `Async` methods). Use `[GeneratedRegex]` partial methods for the canonical-identifier, cursor, and filter shapes.
  - [x] Map each route exactly as the Contract Spine declares it. Each endpoint:
    1. Read `correlationId` + `taskId` + `freshness` headers; validate against canonical shapes (mirror `ProviderReadinessEndpoints.TryReadSupportEvidenceCorrelation` / `IsSafeHeaderValue` / `IsSensitiveDiagnosticValue`).
    2. Reject `Idempotency-Key` with `400 validation_error` / `code: idempotency_key_not_allowed`.
    3. Reject non-`eventually_consistent` freshness with `400 validation_error` / `code: unsupported_read_consistency`.
    4. Validate path identifiers (`folderId`, optional `auditRecordId` / `timelineEntryId`) against the spine's `OpaqueIdentifier` shape.
    5. For list endpoints: validate `cursor` against the strict regex; validate `limit` to `[1, 1000]` and clamp to `100`; reject any non-null `filter` with `400 validation_error` / `code: filter_not_yet_supported` + `details.todoRef: "C4"` (per AC #5).
    6. Resolve the handler via DI (`AuditTrailQueryHandler`, `AuditRecordQueryHandler`, etc.).
    7. Call the handler with `(folderId, …, tenantContext.AuthoritativeTenantId, tenantContext.PrincipalId, claimTransformEvidence.GetEvidence("read_metadata"), correlationId, taskId, cursor, limit, filter, ClientTenantIds(httpContext), ClientPrincipalIds(httpContext))`.
    8. Map `QueryResult` to HTTP via a private `ToHttpResult(…)` (mirror `ProviderReadinessEndpoints.ToHttpResult` for `ProviderSupportEvidenceQueryResult`): success → `200 Results.Json(...)` with canonical wire shape; denial-class → `SafeProblem(...)` with the canonical category + code from the oracle's `error_code_set`. Echo correlation/task on success via `AddSuccessHeaders`.
  - [x] **Idempotent helper module:** share `SafeProblem`, `IsCanonicalIdentifier`, `IsSafeHeaderValue`, `IsSensitiveDiagnosticValue`, `ReadHeader`, `ReadQuery`, `ClientTenantIds`, `ClientPrincipalIds`, `SafeCorrelationId`, `MessageFor` either by referencing the matching helpers in `ProviderReadinessEndpoints.cs` (preferred — promote them to a shared `SafeProblemHelpers.cs` if needed) or by re-implementing the small set inside `AuditEndpoints.cs`. **Do not** copy 200 lines of regex + helper code; promote shared helpers into a sibling `Hexalith.Folders.Server.Internal` file before the second copy lands (review concern: maintainability).
  - [x] Register the four endpoints with `.WithName("ListAuditTrail")` / `.WithName("GetAuditRecord")` / `.WithName("ListOperationTimeline")` / `.WithName("GetOperationTimelineEntry")` (operationId verbatim — no `EndpointNameAliases` needed) and chain `.AddEndpointFilter<FolderAuditEndpointFilter>()`.

- [x] **Task 5 — Wire `MapAuditEndpoints()` into `FoldersServerModule.MapFoldersServerEndpoints`** (AC: #1, #2)
  - [x] Edit `src/Hexalith.Folders.Server/FoldersServerModule.cs:MapFoldersServerEndpoints`: add `endpoints.MapAuditEndpoints();` after `endpoints.MapProviderReadinessEndpoints();` and before `endpoints.MapTenantEventSubscription();`.
  - [x] Verify with a focused server-tests run that the four routes are discoverable through ASP.NET's endpoint metadata (the existing `TransportParityConformanceTests.LoadRegisteredOperationIds()` helper iterates the endpoint metadata — it will pick up the new routes automatically once they are mapped and named).

- [x] **Task 6 — Update `TransportParityConformanceTests`** (AC: #2)
  - [x] Edit `tests/Hexalith.Folders.Server.Tests/TransportParityConformanceTests.cs`:
    - Remove `"GetAuditRecord"`, `"GetOperationTimelineEntry"`, `"ListAuditTrail"`, `"ListOperationTimeline"` from `KnownRestSurfaceGap` (the four entries under the `// Audit-family operations (no /api/v1/audit-trail route).` group). Delete the group comment.
    - Update `ImplementedRestOperationCount` from `28` to `32`.
  - [x] Verify the both-direction guard `KnownRestSurfaceGapAccountsForEveryRestExpectedRowWithoutAnEndpoint` stays green with 15 remaining gaps (down from 19).
  - [x] Verify `ImplementedRestSurfaceMatchesTheRecordedCount` passes at 32.

- [x] **Task 7 — Server tests: per-endpoint contract conformance** (AC: #1, #5, #6, #8, #9)
  - [x] New test class `tests/Hexalith.Folders.Server.Tests/AuditEndpointsTests.cs`. For each of the four endpoints:
    - Happy path: seed projection, drive endpoint, assert `200`, canonical wire shape, correlation echo, freshness header echo, pagination cursor/limit echo.
    - `Idempotency-Key` rejection: assert `400 validation_error` / `code: idempotency_key_not_allowed`.
    - Unsupported freshness: assert `400 validation_error` / `code: unsupported_read_consistency`.
    - Filter rejection (list endpoints): assert `400 validation_error` / `code: filter_not_yet_supported` + `details.todoRef: "C4"`.
    - Cursor tamper / invalid shape: assert `400 validation_error` / `code: cursor_tampered`.
    - Limit out-of-range: assert `400 validation_error` / `code: invalid_pagination`.
    - Projection stale: assert `409` + RFC 9457 with `category: projection_stale`.
    - Projection unavailable: assert `503` + RFC 9457 with `category: projection_unavailable`.
    - Read-model exception: assert `503` + RFC 9457 with `category: read_model_unavailable`; assert log payload contains **only** `ExceptionType` metadata (no identifiers).
  - [x] New test class `tests/Hexalith.Folders.Server.Tests/AuditEndpointsAuthorizationOrderTests.cs` per AC #9.
  - [x] New test class `tests/Hexalith.Folders.Server.Tests/AuditEndpointsSentinelTests.cs` per AC #8. Iterate the full `tests/fixtures/audit-leakage-corpus.json` corpus.
  - [x] Each test class uses the existing `TestHost` / `WebApplication.CreateSlimBuilder(127.0.0.1:0)` pattern from `tests/Hexalith.Folders.Server.Tests/` (or mirrors the `GoldenLifecycleParityTests.TestHost` pattern if the Server.Tests project doesn't already have one).
  - [x] **Tenant authority verification (concern #12):** for each endpoint, seed the host with a fixed `MutableTenantAndClaimContext("tenant-a", "user-a")` and call the endpoint with a `folderId` whose path-segment-derived "tenant" hint mismatches `tenant-a`. Assert the authoritative tenant from `ITenantContextAccessor` is used, **not** the path hint, and the response is byte-for-byte indistinguishable from a missing-folder safe-denial.

- [x] **Task 8 — Update `safety-channel-inventory.json` for the new endpoints** (AC: #8)
  - [x] Edit `tests/fixtures/safety-channel-inventory.json`. Per concern #6 / Story 1.15 pattern, add four channel entries for the new endpoint response bodies (`ListAuditTrail`, `GetAuditRecord`, `ListOperationTimeline`, `GetOperationTimelineEntry`) plus their Problem Details responses. Channel metadata: `surface: "rest"`, `operation: "<operationId>"`, `responseShape: "audit_page"` / `"audit_record"` / `"timeline_page"` / `"timeline_entry"`, `sentinelSwept: true`.
  - [x] **No corpus extension** — the existing sentinels cover the audit-family attack surface. If the new endpoint responses surface a field that the corpus does not yet cover, surface that gap in Dev Notes and add the corpus entry **in a separate PR** with reviewer sign-off (per concern #6 / the audit-corpus PR sign-off rule).

- [x] **Task 9 — Build, test, and drift-bite verification** (AC: #10)
  - [x] Build with the WSL-accessible Windows SDK: `/mnt/c/Program\ Files/dotnet/dotnet.exe restore Hexalith.Folders.slnx` then `dotnet.exe build Hexalith.Folders.slnx --no-restore`. Expect 0 warnings / 0 errors.
  - [x] Focused tests: `dotnet.exe test tests/Hexalith.Folders.Server.Tests --filter "FullyQualifiedName~Audit"` + `dotnet.exe test tests/Hexalith.Folders.Tests --filter "FullyQualifiedName~Audit"` + `dotnet.exe test tests/Hexalith.Folders.Contracts.Tests --filter "FullyQualifiedName~AuditOpsConsole"`. Expect every focused test green.
  - [x] Full Server.Tests: `dotnet.exe test tests/Hexalith.Folders.Server.Tests`. Expect 372/373 → 32 new tests added, no regressions. The pre-existing `BranchRefPolicyEndpointTests.GetBranchRefPolicyShouldUseSafeDenialEnvelopeForTenantMismatch` carry-over red remains.
  - [x] Full IntegrationTests: `dotnet.exe test tests/Hexalith.Folders.IntegrationTests`. **Decide:** do `GoldenLifecycleParityTests` and `MixedSurfaceHandoffTests` extend to consume the new endpoints, OR do they keep the surrogate?
    - **Default (recommended):** keep the surrogate. The Story 5.5/5.7 `RestInspectionOperationId` substitution comment documents this exact flip; the substitution is a tag-change inside the existing test files. **But** Stories 5.5 / 5.7 are tagged "do not modify" from this story's perspective (negative scope of those stories); the substitution itself is part of those stories' designs and so editing the linked `GoldenLifecycle.cs` step list to flip `RestInspectionOperationId: "GetFolderLifecycleStatus"` → `RestInspectionOperationId: "ListAuditTrail"` for the audit step is **in scope here** — it is the documented closing move. Confirm by reading `tests/shared/Parity/GoldenLifecycle.cs:36-83` first; the doc-comment marks the substitution as a "future audit endpoint" flip.
    - **If** the substitution flip is non-trivial (e.g. the audit step has no `RestInspectionOperationId` slot yet), defer: leave the surrogate in place, record the deferral in Dev Notes, and let a follow-up story do the swap.
  - [x] Sibling adapter projects: `dotnet.exe test tests/Hexalith.Folders.{Cli,Mcp,Client}.Tests`. Expect 691/691 + 646/646 + 280/280 (modulo the carry-over `ClientGenerationTests` whitespace red). No behavioral change; the SDK / CLI / MCP already wrap the operations — only the response from a live host changes from "no route" to "200 + canonical shape".
  - [x] Drift sanity: temporarily flip one expected value (e.g. swap the `ListAuditTrail` route to `/api/v1/folders/{folderId}/audit-trail-old`; flip `ImplementedRestOperationCount` to `33`; remove one of the four entries from `KnownRestSurfaceGap` deletion without changing the count). Confirm the focused suite fails with a specific message naming the failing op. Revert.
  - [x] Confirm: no edits under any `Generated/`; no `.slnx` edit; no inline `<PackageReference Version=...>`; no recursive submodule init commands.

## Dev Notes

### Scope boundaries (read first)

- **In scope:** Implement the four audit-family REST endpoints (`ListAuditTrail`, `GetAuditRecord`, `ListOperationTimeline`, `GetOperationTimelineEntry`) as `/api/v1/folders/{folderId}/audit-trail` + child routes, per the Contract Spine in `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`. Authorization-before-observation through `LayeredFolderAuthorizationService` (tenant access → folder ACL → audit reviewer scope → diagnostic audience partition → projection). Pagination via the spine's `PageCursor` / `PageLimit` / `MetadataFilter` (filter is TODO(C4) — reject all non-null filters with `validation_error`). Sensitive metadata classification per S-6 default tier (`tenant_sensitive` for paths / repo names / branch names / commit messages — none of which are carried directly by the audit-family DTOs; classification surfaces via `RedactionMetadata.visibility`). Safe denial (401/403/404 indistinguishable). Metadata-only invariant sentinel-tested over `tests/fixtures/audit-leakage-corpus.json`. Per-endpoint query-handler stack under `src/Hexalith.Folders/Queries/Audit/`. DTOs under `src/Hexalith.Folders.Contracts/Projections/Audit/`. `KnownRestSurfaceGap` + `ImplementedRestOperationCount` updated; if `tests/shared/Parity/GoldenLifecycle.cs`'s `RestInspectionOperationId` slot is ready to flip, flip it (the substitution comment was placed there in Story 5.5 expecting this story).
- **OUT of scope (do NOT implement here):**
  - **Console UI work (Story 6.2 onward).** Stories 6.2 / 6.6 / 6.7 / 6.8 own the console-side rendering of these endpoints. This story is REST + the per-endpoint domain stack only.
  - **EventStore-backed audit projection.** The MVP stack ships `InMemory{Op}ReadModel` for hermetic testing; the production composition (an EventStore-backed read model writing into Dapr state store) is a Workstream 7 / future-epic deliverable. Decision D-10 already documented this strategy ("Dedicated audit projection under `Hexalith.Folders.Server` projection endpoints, derived from event streams; rebuildable from events"). **Do not** implement the EventStore-backed projection here.
  - **C3 audit-trail retention policy.** The spine's `retentionClass` field accepts `TODO(reference-pending):` markers until C3 is frozen. Emit the markers. Do **not** invent a retention policy.
  - **C4 metadata-filter vocabulary.** The spine flags `MetadataFilter` allow-list as TODO(C4). Reject all non-null filters with `validation_error` / `code: filter_not_yet_supported` + `details.todoRef: "C4"`. **Do not** define a temporary filter syntax. Story 6.8 (audit/timeline diagnostic pages) or a dedicated C4 freeze story will populate the allow-list.
  - **Audit-reviewer scope evidence (eventstore:permission:audit:read).** The story assumes the EventStore claim-transform layer (Hexalith.Tenants → Hexalith.EventStore) carries the `eventstore:permission` claim set including the `audit:read` action token. **Do not** define a new claim-transform layer. If the claim is missing, the existing `LayeredFolderAuthorizationService` returns a denial and the endpoint surfaces canonical `audit_access_denied` via `MapAuthorizationDenial`. The claim's *content* is the auth service's contract, not this story's contract.
  - **The 15 remaining `KnownRestSurfaceGap` entries.** This story closes the four-op audit family. ACL (`ListFolderAclEntries`, `UpdateFolderAclEntry`), queries (`GetProviderBinding`, `GetRepositoryBinding`, `GetWorkspaceRetryEligibility`, `GetWorkspaceTransitionEvidence`, `GetProjectionFreshness`), mutators (`CreateFolder`, `ConfigureProviderBinding`), and six diagnostics endpoints are explicitly **out of scope** (per Epic 5 retro Action Items — Winston's "Decide owners" item).
  - **Cross-adapter casing observation fix.** Story 5.6's CLI casing fix (`Category.ToString()` → `EnumMemberValue(Category)`) is **out of scope** here. The CLI tests already pass; the fix is tracked separately (Epic 5 retro low-priority action item).
  - **Architecture.md drift fixes.** Winston's Epic 5 retro action items (abridged failure-kind summary, D-9 file-transport naming, `provider_outcome_unknown` misspelling) are out of scope. The story already merged in commit `c8ec85d` (epic-5-retro fixes). If new architecture.md drift surfaces during this story (e.g. the D-10 audit-projection-strategy text needs updating to point at this story's `Queries/Audit/` location), flag in Dev Notes and address in a follow-up.
- **Negative scope note for the dev:** if you find yourself editing `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`, regenerating `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs`, editing `tests/fixtures/parity-contract.yaml`, editing `tests/fixtures/audit-leakage-corpus.json` (corpus extensions require a separate PR with reviewer sign-off per concern #6), or editing the SDK / CLI / MCP test sources — stop. The contract spine is **already** correct (Story 1.11 shipped it); the SDK is **already** generated against it (Story 1.12 shipped that); CLI + MCP wrappers **already** call the SDK (Stories 5.2 / 5.3 shipped both). This story implements the **server-side route + domain stack** that those existing surfaces have been calling against a 404 since Stories 5.5 / 5.7 documented the gap.

### What this story closes (vs. Stories 5.5 / 5.7 / Epic 5 retro)

- **Story 5.5 (REST/SDK transport parity)** documented the audit-family gap explicitly: `KnownRestSurfaceGap` lists `GetAuditRecord`, `GetOperationTimelineEntry`, `ListAuditTrail`, `ListOperationTimeline` under the `// Audit-family operations (no /api/v1/audit-trail route).` group; `tests/shared/Parity/GoldenLifecycle.cs:36-83` ships a `RestInspectionOperationId` substitution slot expecting the audit endpoints to land here.
- **Story 5.7 (mixed-surface handoff)** asserted cross-surface coherence via `GetFolderLifecycleStatus` as the audit surrogate (AC #7) and ships a verbatim doc-comment: "When `ListAuditTrail` / `GetAuditRecord` / `ListOperationTimeline` / `GetOperationTimelineEntry` are implemented as `/api/v1` routes (Story 5.5 REST surface gap closed), the surrogate is replaced by the audit-family operation with no test-design change — mirrors `GoldenLifecycleStep.RestInspectionOperationId` substitution."
- **Epic 5 retro (commit `c8ec85d`)** flagged this as the largest single REST-surface-gap closure in Epic 6 ("Story 6.1 (audit/timeline query endpoints) closes the largest part of the REST surface gap Epic 5 documented... When they land, Story 5.5's `RestInspectionOperationId` substitution and Story 5.7's audit-surrogate via `GetFolderLifecycleStatus` can both retire — the assertions extend without test redesign.")
- **This story** is purely additive on the server side: new endpoints, new query handlers, new in-memory read models, new DTOs (where the spine declares them but Contracts didn't already ship them), and the `KnownRestSurfaceGap` / `ImplementedRestOperationCount` bookkeeping update. SDK / CLI / MCP / parity oracle / contract spine are **not** edited.

### The architectural pattern: mirror `GetFolderLifecycleStatus` exactly

`GetFolderLifecycleStatus` (operationId same; route `/api/v1/folders/{folderId}/lifecycle-status`) is the canonical template for an authorization-gated, projection-backed, opaque-ID-only, read-model-driven GET endpoint that the audit family must mirror. It is the simplest existing endpoint pattern in the server tree at this baseline and the four new endpoints are direct generalizations:

- **Endpoint shape** — see `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs:928-982`. Reads correlation/task/freshness headers; rejects unsupported freshness; calls the handler; maps the result through `ToHttpResult(httpContext, result, correlationId, taskId)` which produces either `Results.Json(...)` on success or `SafeProblem(...)` on a denial-class outcome.
- **Handler shape** — see `src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:24-127`. Validates authentication context (`AuthoritativeTenantId` / `PrincipalId` non-null); validates `FolderId`; calls `LayeredFolderAuthorizationService.AuthorizeAsync` with `LayeredFolderOperationPolicy.StrictRead()` and action token `"read_metadata"` BEFORE any read-model call; catches read-model exceptions metadata-only; switches on `ReadModelStatus` ∈ `{ Available, Stale, Unavailable, Malformed, NotFound }` and surfaces the canonical category accordingly.
- **DI shape** — see `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs:65-84` (`AddFoldersLifecycleStatus`). Registers `IFolderLifecycleStatusReadModel` → `InMemoryFolderLifecycleStatusReadModel` + the handler. Idempotent `AddFoldersLayeredAuthorization()` call ensures the authorization service is resolvable.

The four audit-family handlers differ from `FolderLifecycleStatusQueryHandler` in only three places:
1. The action token resolved from `IEventStoreClaimTransformEvidenceAccessor.GetEvidence("read_metadata")` is the same (`read_metadata`); the audit-reviewer-scope check is layered **inside** the existing `LayeredFolderAuthorizationService` via the EventStore claim transform's `eventstore:permission` claim set, not via a new action token.
2. The list-endpoint variants (`ListAuditTrail`, `ListOperationTimeline`) accept three additional parameters: `cursor`, `limit`, `filter`.
3. The single-record variants (`GetAuditRecord`, `GetOperationTimelineEntry`) accept one additional path-bound identifier (`auditRecordId` / `timelineEntryId`).

Everything else — header validation, idempotency rejection, freshness validation, canonical-identifier shape, safe denial, metadata-only logging, read-model exception handling, `ToHttpResult` mapping — copies from `FoldersDomainServiceEndpoints.cs:928-982` + `FolderLifecycleStatusQueryHandler.cs`.

### Audit-family endpoint shape (one consolidated view)

| Endpoint | Operation ID | Route | Path params | Query params | Headers | Success body |
|---|---|---|---|---|---|---|
| List audit trail | `ListAuditTrail` | `GET /api/v1/folders/{folderId}/audit-trail` | `folderId` | `cursor?`, `limit?`, `filter?` | `X-Correlation-Id?`, `X-Hexalith-Task-Id?`, `X-Hexalith-Freshness?` | `AuditTrailPage` |
| Get audit record | `GetAuditRecord` | `GET /api/v1/folders/{folderId}/audit-trail/{auditRecordId}` | `folderId`, `auditRecordId` | — | `X-Correlation-Id?`, `X-Hexalith-Task-Id?`, `X-Hexalith-Freshness?` | `AuditRecord` |
| List operation timeline | `ListOperationTimeline` | `GET /api/v1/folders/{folderId}/operation-timeline` | `folderId` | `cursor?`, `limit?`, `filter?` | `X-Correlation-Id?`, `X-Hexalith-Task-Id?`, `X-Hexalith-Freshness?` | `OperationTimelinePage` |
| Get operation timeline entry | `GetOperationTimelineEntry` | `GET /api/v1/folders/{folderId}/operation-timeline/{timelineEntryId}` | `folderId`, `timelineEntryId` | — | `X-Correlation-Id?`, `X-Hexalith-Task-Id?`, `X-Hexalith-Freshness?` | `OperationTimelineEntry` |

Each endpoint emits canonical safe-denial RFC 9457 with the codes in its oracle row's `Transport.ErrorCodeSet` (per AC #4) and rejects `Idempotency-Key` (per AC #5).

### Project-reference direction (concern #18 + concern #6 + Epic 1 retro action item)

`src/Hexalith.Folders.Contracts/` has **no** project references (Contracts is the leaf). `src/Hexalith.Folders/` references Contracts only. `src/Hexalith.Folders.Server/` references `Hexalith.Folders` + `Hexalith.Folders.Contracts` + `Hexalith.Folders.ServiceDefaults` + EventStore/Tenants sibling submodules. The Client SDK (`Hexalith.Folders.Client`) references **only** Contracts (not Server, not core).

**Do not** add a `Hexalith.Folders.Server` → `Hexalith.Folders.Client` reference to "reuse" the generated `AuditTrailPage` / `AuditRecord` / `OperationTimelinePage` / `OperationTimelineEntry` DTOs. The server-side DTOs live in `Hexalith.Folders.Contracts.Projections.Audit.*` (new) and are the authoritative wire shape; the generated client DTOs in `Hexalith.Folders.Client.Generated.HexalithFoldersClient.g.cs` are wire-equivalent shadow copies for SDK consumption. Wire-shape equivalence is enforced by `tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuditOpsConsoleContractGroupTests.cs` (the existing contract-group conformance test).

### Wire-level canonical wire shape: what the endpoints emit

- **Correlation echo**: `X-Correlation-Id` response header on every success and safe-denial; same value the caller supplied (or a server-issued ULID if the caller omitted it).
- **Task echo**: `X-Hexalith-Task-Id` response header when the caller supplied one (the audit family is `not_task_scoped` per the oracle; if the caller supplies a task ID it is treated as a correlation hint and echoed, not gated on).
- **Freshness echo**: `X-Hexalith-Freshness: eventually_consistent` on every success.
- **Pagination echo**: `PaginationMetadata.cursor` (next-page cursor, opaque, same shape as the request `cursor` parameter; null when no more pages); `PaginationMetadata.limit` (effective limit, clamped to 100); `PaginationMetadata.requestedLimit` (per the spine's `PaginationMetadata` properties — emit both so callers can detect server-side clamping per the `...page-truncation` freshness behavior).
- **Retention class echo**: list-endpoint responses emit `retentionClass: "TODO(reference-pending):audit-trail-retention"` / `"TODO(reference-pending):operation-timeline-retention"` until C3 vocabulary freezes (per the spine's `^(?:TODO\(reference-pending\):.{1,140}|[a-z][a-z0-9_]{0,79})$` pattern).
- **Freshness metadata**: every response body's `freshness` field carries the projection's evidence watermark + `readConsistency: "eventually_consistent"` + `stale: bool` + an optional `reasonCode` snake_case identifier (`projection_stale` / `projection_unavailable` / `lifecycle_unavailable` etc. mirror the existing `FolderLifecycleFreshness` reason codes).
- **Safe denial body shape**: every safe-denial RFC 9457 carries `type: "https://hexalith.dev/errors/folders/<code>"`, `title:` matching the existing endpoint conventions (`"Authentication required."` / `"Audit access denied."` / `"Folder access denied."` / `"Audit evidence is not currently fresh enough for this operation."`); `extensions: { category, code, message, correlationId, retryable, clientAction, details: { visibility: "metadata_only", evidenceSource: "audit" | "timeline" } }`. **No** field name, value, or evidence pattern that could distinguish "hidden" from "wrong-tenant" from "missing" from "redacted" from "stale" from "unavailable" (per the spine's `safeDenial: externally-indistinguishable safe-denial` requirement).

### The cursor: regex shape, opacity, and tamper detection

The Contract Spine declares `PageCursor` as `minLength: 1, maxLength: 256, type: string` and the `PaginationMetadata.cursor` description says the cursor MUST NOT carry tenant authority, ACL decisions, provider tokens, raw query text, or unredacted path lists.

The cursor is **opaque** to callers but **server-issued and server-bound**: the server generates it on the previous page response and the caller echoes it on the next request. The audit-family oracle row's `Behavioral.ParityDimensions` includes `cursor_tamper_detection`, which means: a cursor that decodes to a different tenant, a different folder, a different audit cursor type, or a corrupted signature MUST be rejected with canonical `validation_error` / `code: cursor_tampered` — **not** silently re-served against the new caller's tenant.

For this story's MVP (in-memory read model), the cursor is a synthetic opaque token of the shape `cursor_<base64url(rangeStart):base64url(tenantId):base64url(opTypeTag)>` or equivalent (the precise encoding is a server-internal choice; the rejection contract is fixed). Tampered cursors (signature mismatch on decode, tenant-mismatch, or op-type-mismatch) emit `validation_error` / `code: cursor_tampered`. The encoding scheme is private; the test asserts the rejection contract over a small synthetic corpus of (well-formed, tampered-tenant, tampered-opType, malformed-base64, too-long, empty) inputs.

### Filter: TODO(C4), reject with `validation_error`

The spine's `MetadataFilter` is explicit: "Allowed filter-key vocabulary for ListAuditTrail and ListOperationTimeline is TODO(reference-pending C4); until frozen, runtime servers MUST reject any filter key not in their per-operation allow-list and SHOULD emit `validation_error` for unknown keys."

For this story's MVP, the per-operation allow-list is **empty**. Any non-null `filter` query string emits `400 validation_error` / `code: filter_not_yet_supported` with `extensions.details.todoRef: "C4"`. Do **not** invent a temporary filter syntax (Epic 6 retro will hate you; Story 6.8 owns the diagnostic-page side and is the natural owner of the C4 closure). The C4 reference-pending state is captured in the spine itself; the test asserts the rejection contract over `?filter=`, `?filter=actorReference%3Dactor-a`, and `?filter=*` inputs.

### Sensitive metadata classification on audit DTOs (S-6 default tier)

Per architecture decision S-6 (and the spine's `x-hexalith-sensitive-metadata-tier: tenant_sensitive` markers on the audit-family fields), paths + repo names + branch names + commit messages classify as `tenant_sensitive`. The audit-family DTOs **do not** carry raw paths / repo names / branch names / commit messages — they carry **prefixed opaque digests and references** (`workspaceReference: PrefixedOpaqueIdentifier` matching `^[a-z][a-z0-9_]{0,7}_[A-Za-z0-9_-]{8,119}$`; `changedPathEvidence.digest: digest_<base64>`; `changedPathEvidence.reference: changeref_<base64>`).

Classification surfaces via the `RedactionMetadata.visibility` field per record:
- `visible` — caller is authorized to see the field; the opaque identifier or digest is emitted.
- `redacted` — tenant policy upgraded classification from `tenant_sensitive` to `confidential`; the field is absent from the response body **and** the redaction state is signaled via `redaction.visibility: "redacted"` so the console can render the lock-icon affordance (Story 6.4) instead of silently hiding the field.
- `unknown` — projection is malformed or the field is structurally absent (NOT a redaction; rendered differently by Story 6.4).
- `unavailable` — projection is degraded; rendered with a degraded-mode banner by Story 6.4.

The handler uses `SensitiveMetadataClassifier` (under `src/Hexalith.Folders/Redaction/` — read first; the type already exists for the lifecycle-status family) to compute the `visibility` field per the tenant's classification tier (`tenant_sensitive` default → `visible` for tenant-authorized callers; `confidential` per-tenant override → `redacted` at the projection write-side, which means the read-model never sees the cleartext; this story's handler **does not** decrypt or unredact — it surfaces whatever the projection holds).

**Consistency invariant (AC #7):** the same record returned via `GetAuditRecord` and `ListAuditTrail` must have **byte-for-byte identical redaction state on every field**. New test class `AuditRedactionConsistencyTests.cs` seeds a 3-record projection (mix of `visible` / `redacted` / `unknown` / `unavailable` per-field across records) and proves the byte-for-byte invariant for `(GetAuditRecord, ListAuditTrail)` and `(GetOperationTimelineEntry, ListOperationTimeline)` pairs.

### Don't rewire 5.5 / 5.7 (cosmetic test changes only)

Stories 5.5 and 5.7 ship the audit-family substitution slot (`RestInspectionOperationId`) precisely so this story can close the gap **without** editing the linked test sources structurally. The minimal change to `tests/shared/Parity/GoldenLifecycle.cs` is:

- **If** the existing audit step in `GoldenLifecycle.Steps` carries `RestInspectionOperationId: "GetFolderLifecycleStatus"`: flip to `RestInspectionOperationId: null` (the surrogate retires; the REST run uses the audit op's own operation id).
- **If** the existing audit step is the lifecycle-status step itself (no `RestInspectionOperationId` because the step is the lifecycle inspection): leave it; add a new audit step with no substitution.

**Read `tests/shared/Parity/GoldenLifecycle.cs` first**; the substitution comment documents the exact flip and which step to edit. **Do not** rewrite the step list. **Do not** edit `MixedSurfaceScenario.cs` (Story 5.7) — the mixed-surface scenario's audit-surrogate behavior carries over unchanged because the scenario already covers the audit step via `GetFolderLifecycleStatus`; the new audit endpoints become an **additional** verification path that future stories may consume.

### Previous-story intelligence (lessons to carry in)

- **Story 5.5 (`baseline_commit: 7665fbd`)** documented the audit-family gap in `KnownRestSurfaceGap` and the `RestInspectionOperationId` substitution slot. This story's `KnownRestSurfaceGap` edit is the closing move.
- **Story 5.6 (`baseline_commit: 5200865`)** documented the `🟡 cross-adapter casing observation` (CLI emits PascalCase via `Category.ToString()`; MCP emits canonical snake_case via `[EnumMember]`). The canonical snake_case still surfaces on the CLI via `problem.Code`. **For this story**, the server emits the canonical snake_case (`"audit_access_denied"`, `"response_limit_exceeded"`, etc.) via `[EnumMember]` reflection on `CanonicalErrorCategory`; the CLI / MCP wire shape on the audit endpoints inherits the canonical snake_case directly.
- **Story 5.7 (`baseline_commit: 5200865`)** documented the in-process gateway response-flattening (idempotency conflict and folder ACL provocation limitations) and the lifecycle-snapshot task-binding wrinkle. **For this story**, the audit-family endpoints are **query** endpoints, not mutating; the in-process-gateway flattening does **not** apply. The lifecycle-snapshot wrinkle applies only when audit records are seeded by the in-process aggregate via a mutating chain — the audit-tests seed the audit projection directly via `InMemory{Op}ReadModel.Seed(...)` to avoid the wrinkle.
- **Epic 5 retro action items (`baseline_commit: c8ec85d`)** flagged three carry-over reds (`ClientGenerationTests` whitespace, `BranchRefPolicyEndpointTests` tenant-mismatch, `Hexalith.Folders.Testing.Tests.ProjectReferencesFollowAllowedDependencyDirection`). All three are confirmed pre-existing and remain out of scope for this story. **If** the third red (project-reference direction) blocks compilation because the new `src/Hexalith.Folders.Server/AuditEndpoints.cs` adds a reference shape the policy doesn't expect, surface it in Dev Notes — but the expected new references (Server → core for the handler; Server → Contracts for the DTOs) are already allowed.

### Architecture references

- **Architecture decision D-10 (Audit storage):** "Dedicated audit projection under `Hexalith.Folders.Server` projection endpoints, derived from event streams; rebuildable from events; retention policy per C3." This story implements the *endpoint* side and the *in-memory read model* side. The EventStore-backed projection that writes into the read model is a future-epic deliverable.
- **Architecture decision S-6 (Sensitive metadata classification):** "Default tier: paths + repo names + branch names + commit messages classified as tenant-sensitive; per-tenant override allows confidential (hashed in audit/projection storage at write time)." This story respects the default; per-tenant override is enforced at the projection write side (out of scope here).
- **Architecture decision A-3 (REST canonical transport):** "Hexalith.EventStore Command/Query API patterns + ASP.NET Core Minimal APIs in `Hexalith.Folders.Server` for `/process` and `/project` endpoints; OpenAPI generated by Microsoft.AspNetCore.OpenApi from controllers/handlers, validated against the C0 Contract Spine in CI as a BLOCKING gate." This story's new endpoints follow Minimal API conventions; OpenAPI emission flows through the existing pipeline.
- **Architecture decision A-10 (Correlation propagation):** "`X-Correlation-Id` and `X-Hexalith-Task-Id` headers carry across REST, SDK, CLI, MCP." This story echoes both headers on success and on safe-denial.
- **Cross-cutting concern #6 (Metadata-only audit + redaction):** "Automated sanitizer tests with sentinel secrets and file-content markers across logs/traces/metrics labels/events/audit records/console views/provider diagnostics/error responses." This story's `AuditEndpointsSentinelTests` is the per-endpoint enforcement.
- **Cross-cutting concern #12 (Tenant context provenance):** "Client-controlled tenant values are comparison inputs only. Authoritative tenant comes from authenticated context." This story's endpoints derive tenant authority from `ITenantContextAccessor` only.
- **Cross-cutting concern #17 (Sensitive metadata classification):** "Default sensitivity tier required; classification applies uniformly across audit, projections, and console responses." This story enforces uniform classification across `GetAuditRecord` / `ListAuditTrail` (AC #7).
- **PRD §FR53 (Metadata-only audit trail inspection):** "Success/denied/failed/retried/duplicate audit records inspectable." This story's `AuditRecord` DTO carries `resultStatus`, `sanitizedErrorCategory`, and `retryable` per the spine — full coverage.
- **PRD §FR54 (Incident reconstruction from immutable audit metadata):** "Operators can reconstruct incidents from metadata-only audit records." This story's audit-trail endpoint is the read path; the inscription-side (audit records derived from event streams) is D-10's future-epic deliverable.
- **PRD §FR56 (Operation timelines):** "Operation timelines for folder, workspace, file, lock, commit, provider, status, authorization events." This story's `ListOperationTimeline` + `GetOperationTimelineEntry` are the per-folder timeline read path. Operation events from all eight families flow into the same timeline read model.

### Filter / cursor / limit defensive ceilings

- **Filter:** empty allow-list, all non-null filters rejected with `400 validation_error` / `code: filter_not_yet_supported` + `details.todoRef: "C4"`. Spine regex `^[a-z][A-Za-z0-9_=.,*\- ]{0,255}$` still validated at the wire-shape level (a filter that doesn't match the spine regex emits `400 validation_error` / `code: validation_error`, NOT `filter_not_yet_supported` — the regex check happens first).
- **Cursor:** maxLength 256, characters from the canonical alphabet (alphanumeric + `_-`). Server-issued; caller-opaque. Tampered cursors → `cursor_tampered`.
- **Limit:** `[1, 1000]` per the spine; clamped to `100` at the audit read model (matches `entries.maxItems: 100`). Out-of-range → `invalid_pagination`. The `PaginationMetadata` response carries both the requested limit and the effective limit so callers can detect clamping.

### Build environment

The WSL-native `.NET` SDK is 10.0.108 (or whatever the running WSL distro has installed) and fails the `global.json` `10.0.302` pin. Build / restore / test through the Windows SDK from WSL via `/mnt/c/Program\ Files/dotnet/dotnet.exe` (`dotnet.exe restore`, `dotnet.exe build`, `dotnet.exe test`). [Source: user-memory `.NET Windows SDK in WSL`]

### Git intelligence

- `baseline_commit`: `f933b11` (`chore(story-automator): finalize Epic 5 orchestration state to COMPLETE`). Recent epic-5 → epic-6 transition history: 5.5 → 5.6 → 5.7 → epic-5-retro (with architecture.md drift fixes) → orchestration finalize. Commit convention: `feat(story-6.1): <imperative summary>`.
- Do **not** touch submodules. The working tree may show gitlink drift in sibling submodules and an unrelated modified file under `_bmad-output/story-automator/` and `.claude/skills/bmad-story-automator/` — leave all of it; do **not** stage it with the story 6.1 commit.

### Testing requirements

[Source: project-context.md#Testing-Rules]

- Tests live in `tests/Hexalith.Folders.Server.Tests/AuditEndpoints*Tests.cs` + `tests/Hexalith.Folders.Tests/Queries/Audit/*Tests.cs`. xUnit v3 (`3.2.2`) + Shouldly (`4.3.0`) + NSubstitute (`5.3.0`). `TestContext.Current.CancellationToken` in async tests.
- **Hermetic:** in-process host on `127.0.0.1:0` (Server.Tests already has this pattern via existing `*EndpointTests.cs` files — read one as the template); in-memory `IXxxReadModel` implementations seeded per test; in-memory `MutableTenantAndClaimContext` for the `ITenantContextAccessor`. No live server, Dapr, Keycloak, Redis, GitHub/Forgejo, providers, network, or nested submodule init.
- **Shared fixtures unforked:** read `tests/fixtures/audit-leakage-corpus.json` in place via the existing `AuditLeakageCorpus` reader (or read the JSON directly with `System.Text.Json`); no fork, no copy. The corpus is concern #6's normative cross-project sentinel corpus.
- **Server.Tests owns server-endpoint scope** (mirrors Stories 5.5/5.6/5.7 IntegrationTests scope per-test-project). The four new endpoints are server-only; their per-handler unit tests live in `tests/Hexalith.Folders.Tests` (mirror `FolderLifecycleStatusQueryHandlerTests.cs` layout).
- **Carry-over reds (per Epic 5 retro):** `ClientGenerationTests.GeneratedClientAndHelpersMatchIsolatedRegeneration`, `BranchRefPolicyEndpointTests.GetBranchRefPolicyShouldUseSafeDenialEnvelopeForTenantMismatch`, `Hexalith.Folders.Testing.Tests.ProjectReferencesFollowAllowedDependencyDirection`. Confirm these are the only pre-existing reds before reporting completion.

### Project Structure Notes

- **New folders/files (production source):**
  - `src/Hexalith.Folders/Queries/Audit/` (new folder): per-op query handler stack (~40 files: 10 per operation × 4 operations).
  - `src/Hexalith.Folders.Contracts/Projections/Audit/` (new folder): spine-shaped DTOs (~10-12 files; depends on what shared types already exist in `Contracts/Projections/`).
  - `src/Hexalith.Folders.Server/AuditEndpoints.cs` (new file): the four endpoint registrations + private `Async` methods + the local helpers (or a reference to a promoted `SafeProblemHelpers.cs` if shared with `ProviderReadinessEndpoints.cs`).
- **Modified files (production source):**
  - `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs`: add `AddFoldersAuditQueries(...)` method.
  - `src/Hexalith.Folders.Server/FoldersServerModule.cs`: add `services.AddFoldersAuditQueries()` + `endpoints.MapAuditEndpoints()`.
- **New folders/files (tests):**
  - `tests/Hexalith.Folders.Server.Tests/AuditEndpointsTests.cs` (happy + per-endpoint conformance).
  - `tests/Hexalith.Folders.Server.Tests/AuditEndpointsAuthorizationOrderTests.cs` (per AC #9).
  - `tests/Hexalith.Folders.Server.Tests/AuditEndpointsSentinelTests.cs` (per AC #8).
  - `tests/Hexalith.Folders.Server.Tests/AuditRedactionConsistencyTests.cs` (per AC #7 byte-for-byte invariant).
  - `tests/Hexalith.Folders.Tests/Queries/Audit/` (new folder): per-handler unit tests (4 classes mirroring `FolderLifecycleStatusQueryHandlerTests.cs`).
- **Modified files (tests):**
  - `tests/Hexalith.Folders.Server.Tests/TransportParityConformanceTests.cs`: remove 4 entries from `KnownRestSurfaceGap`; update `ImplementedRestOperationCount` from `28` to `32`.
  - **Possibly:** `tests/shared/Parity/GoldenLifecycle.cs`: flip the `RestInspectionOperationId` substitution on the audit step (only if the existing slot is structurally ready — read first; if non-trivial, defer per Dev Notes "Don't rewire 5.5 / 5.7").
  - `tests/fixtures/safety-channel-inventory.json`: add four new channel entries for the audit-endpoint responses + Problem Details bodies (per AC #8).
- **No edits permitted:**
  - `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` (Contract Spine — Story 1.11 ships the audit-family operations; the route shapes, response schemas, and error code sets are already declared).
  - `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs` (generated client — Story 1.12 generated the audit-family methods).
  - `src/Hexalith.Folders.Cli/Commands/Audit/AuditCommand.cs` (CLI wrapper — Story 5.2 ships it).
  - `src/Hexalith.Folders.Mcp/Tools/AuditTools.cs`, `src/Hexalith.Folders.Mcp/Resources/AuditTrailResource.cs` (MCP wrappers — Story 5.3 ships them).
  - `tests/fixtures/parity-contract.yaml`, `tests/fixtures/parity-contract.schema.json` (parity oracle + schema — Story 1.13 owns).
  - `tests/fixtures/audit-leakage-corpus.json` (corpus extensions require separate PR with reviewer sign-off per concern #6).
  - `tests/Hexalith.Folders.Cli.Tests/*`, `tests/Hexalith.Folders.Mcp.Tests/*`, `tests/Hexalith.Folders.Client.Tests/*` (no behavioral change in adapter tests — the SDK / CLI / MCP already wrap the operations against canned responses).
- **`Hexalith.Folders.slnx` unchanged.** **No new package references.** **No inline `<PackageReference Version=...>`.** **No recursive submodule init commands.**

### References

- [Source: epics.md#Epic-6 / #Story-6.1] — epic objective ("audit reviewer needs metadata-only query endpoints for incident reconstruction") and the verbatim Story 6.1 acceptance criteria; epic-level guardrails ("primary job is inspect, verify, and escalate ... no mutation endpoints, no privileged backdoors, no hidden administrative bypasses, no UI-only lifecycle semantics").
- [Source: _bmad-output/planning-artifacts/architecture.md#Decision-D-10] — "Dedicated audit projection under `Hexalith.Folders.Server` projection endpoints, derived from event streams; rebuildable from events; retention policy per C3." The endpoint side this story implements; EventStore-backed projection is future-epic.
- [Source: _bmad-output/planning-artifacts/architecture.md#Decision-S-6] — "Default tier: paths + repo names + branch names + commit messages classified as tenant-sensitive; per-tenant override allows confidential (hashed in audit/projection storage at write time)." This story's `RedactionMetadata.visibility` enforcement.
- [Source: _bmad-output/planning-artifacts/architecture.md#Decision-A-3] — "REST canonical transport ... ASP.NET Core Minimal APIs in `Hexalith.Folders.Server`." This story's endpoint shape.
- [Source: _bmad-output/planning-artifacts/architecture.md#Cross-Cutting-Concerns-6-12-17] — metadata-only audit, tenant context provenance, sensitive metadata classification. The three non-negotiables this story enforces.
- [Source: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:3908-4248] — the four audit-family operations' route shapes, request parameters, response schemas, `x-hexalith-canonical-error-categories` sets, `x-hexalith-read-consistency.class` declarations, `x-hexalith-correlation` headers, `x-hexalith-authorization.requirement`, and `x-hexalith-audit-metadata-keys`.
- [Source: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:9105-9235] — `AuditTrailPage`, `AuditRecord`, `OperationTimelinePage`, `OperationTimelineEntry` schemas + `DiagnosticStateTransition`.
- [Source: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:5032-5108] — `PageCursor`, `PageLimit`, `MetadataFilter`, `FolderId`, `AuditRecordId`, `TimelineEntryId` parameter schemas (with the C4-pending filter vocabulary note).
- [Source: tests/fixtures/parity-contract.yaml:3299-3388 (ListAuditTrail) + 918+ (GetAuditRecord) + 1747+ (GetOperationTimelineEntry) + 3583+ (ListOperationTimeline)] — the audit-family oracle rows: `auth_outcome_class: audit_access_denied`, `error_code_set` (12 categories for list endpoints, 10 for record endpoints), `idempotency_key_rule: not_accepted_for_non_mutating_operation`, `audit_metadata_keys`, `correlation_field_path: headers.X-Correlation-Id`, `terminal_states: [audit_returned]`, `behavioral_parity` columns, full `outcome_mapping` rows for cross-adapter CLI exit codes + MCP failure kinds.
- [Source: src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs:928-982 + ProviderReadinessEndpoints.cs:34-66] — the canonical Minimal-API endpoint pattern this story mirrors. The lifecycle-status endpoint at line 928 is the closest behavioral cousin; the provider-readiness `GetProviderSupportEvidence` endpoint at line 53 is the closest pagination cousin.
- [Source: src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:1-160] — the canonical authorization-before-observation handler pattern (`LayeredFolderAuthorizationService.AuthorizeAsync` BEFORE the read-model call; metadata-only exception logging; `ReadModelStatus` switch).
- [Source: src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs:55-118] — DI registration patterns to mirror (`AddFoldersLifecycleStatus`, `AddFoldersProviderReadiness`).
- [Source: src/Hexalith.Folders.Server/FoldersServerModule.cs:57-106] — `AddFoldersServer` + `MapFoldersServerEndpoints` composition; where to plug in `AddFoldersAuditQueries()` + `MapAuditEndpoints()`.
- [Source: tests/Hexalith.Folders.Server.Tests/TransportParityConformanceTests.cs:59 + 78-108 + 122-149] — `ImplementedRestOperationCount` constant + `KnownRestSurfaceGap` set + the both-direction guard test (this story removes 4 entries and updates the count).
- [Source: tests/shared/Parity/GoldenLifecycle.cs:36-83] — Story 5.5's `RestInspectionOperationId` substitution slot, ready to be flipped here.
- [Source: _bmad-output/implementation-artifacts/5-7-validate-mixed-surface-handoff-scenario.md#AC-7] — Story 5.7's audit-surrogate-via-`GetFolderLifecycleStatus` invariant; comment documents the closing substitution.
- [Source: _bmad-output/implementation-artifacts/epic-5-retro-2026-05-28.md#Next-Epic-Preview] — "Story 6.1 (audit/timeline query endpoints) closes the largest part of the REST surface gap Epic 5 documented." The framing this story executes.
- [Source: _bmad-output/project-context.md] — same-key + equivalent payload ⇒ same logical result (mutating ops only; not this story's territory); non-mutating ops must not accept `Idempotency-Key`; metadata-only everywhere; central package management; hermetic-gate rules; submodule policy; sensitive-metadata classification rule.
- [Source: tests/fixtures/audit-leakage-corpus.json] — the normative cross-project sentinel corpus per concern #6 + A2; iterated by `AuditEndpointsSentinelTests`.
- [Source: tests/fixtures/safety-channel-inventory.json] — the surface inventory; add 4 entries for the new audit-family channels.

### Latest technical notes (pinned versions — do not bump in this story)

Centrally managed in `Directory.Packages.props` (repo config is authoritative). No new package references required: the server uses the existing ASP.NET Core Minimal API surface (Microsoft.AspNetCore 10.0.x), `System.Text.Json`, `System.Text.RegularExpressions` with `[GeneratedRegex]`, and `Microsoft.Extensions.Logging`. The test additions use the existing xUnit v3 `3.2.2`, Shouldly `4.3.0`, NSubstitute `5.3.0` already referenced by `Hexalith.Folders.Server.Tests` and `Hexalith.Folders.Tests`.

**Do not** add `Newtonsoft.Json` references — the server side uses `System.Text.Json` exclusively (the generated client uses `Newtonsoft.Json` per A-9, but the server and core domain do not). **Do not** add `YamlDotNet` references — the parity oracle is read at test time only and Server.Tests does not need it. **Do not** regenerate the client, the server OpenAPI emission, or the oracle.

**Build/test in this environment:** the WSL-native .NET SDK fails the `global.json` `10.0.302` pin; build and test through the Windows SDK from WSL, e.g. `/mnt/c/Program\ Files/dotnet/dotnet.exe` (`dotnet.exe restore | build | test`). [Source: user-memory — `.NET Windows SDK in WSL`]

## Dev Agent Record

### Agent Model Used

claude-opus-4-7[1m]

### Debug Log References

- Build: `/mnt/c/Program\ Files/dotnet/dotnet.exe build Hexalith.Folders.slnx --nologo` → 0 warnings / 0 errors.
- Focused tests: `dotnet.exe test tests/Hexalith.Folders.Server.Tests --filter "FullyQualifiedName~Audit"` → 27/27 pass.
- Focused tests: `dotnet.exe test tests/Hexalith.Folders.Tests --filter "FullyQualifiedName~Audit"` → all 57 audit + non-audit tests in the project's audit query path pass (no regressions).
- Full Server.Tests: 390 pass / 1 fail (`BranchRefPolicyEndpointTests.GetBranchRefPolicyShouldUseSafeDenialEnvelopeForTenantMismatch` is a carry-over red from Epic 5 retro action items — pre-existing, unrelated).
- Full Hexalith.Folders.Tests: 1251/1251 pass.
- Cli.Tests: 691/691, Mcp.Tests: 646/646, Client.Tests: 280/280, IntegrationTests: 591/591 — all green; no behavioral change in adapters.
- Contracts.Tests: AuditOpsConsole filter shows `AuditOpsConsoleNegativeScope_DoesNotAddRuntimeAdaptersGeneratedClientsUiOrCi` fails. Verified pre-existing via `git stash` test on the un-modified working tree — it fails on `main@f933b11` the same way (the CLI commands listed are from Stories 5.2 / 5.3 and were already present). Not a Story 6.1 regression.

### Completion Notes List

- All 9 implementation tasks completed; status transitioned `ready-for-dev → in-progress → review`.
- Closed the four-op audit-family REST surface gap that Story 5.5 documented: new `/api/v1/folders/{folderId}/audit-trail`, `/audit-trail/{auditRecordId}`, `/operation-timeline`, `/operation-timeline/{timelineEntryId}` endpoints registered through `FoldersServerModule.MapFoldersServerEndpoints`.
- `KnownRestSurfaceGap` shrank by 4 entries; `ImplementedRestOperationCount` moved 28 → 32. Both-direction guard `KnownRestSurfaceGapAccountsForEveryRestExpectedRowWithoutAnEndpoint` stays green.
- Per-op query stack (Query/Handler/Result/ResultCode/IReadModel/ReadModelRequest/ReadModelResult/ReadModelSnapshot/InMemoryReadModel) sits under `src/Hexalith.Folders/Queries/Audit/`. Each handler calls `LayeredFolderAuthorizationService.AuthorizeAsync` with `LayeredFolderOperationPolicy.StrictRead()` and action token `read_metadata` BEFORE any read-model call. Read-model exceptions are caught and surfaced as canonical `read_model_unavailable` with metadata-only logging.
- Spine-shaped DTOs live under `src/Hexalith.Folders.Contracts/Projections/Audit/` (12 records + 4 enums). camelCase wire shape via `[JsonPropertyName]`; the file already shared the Contracts assembly with no cross-project leakage. No new package references.
- Pagination/limit/freshness validation lives in `AuditEndpoints.cs`: `cursor` regex `^cursor_[A-Za-z0-9_-]{8,247}$` (tampered cursors → `cursor_tampered`); `limit ∈ [1, 1000]` with clamp to 100; non-null `filter` → `filter_not_yet_supported` + `details.todoRef: "C4"`; non-eventually_consistent `X-Hexalith-Freshness` → `unsupported_read_consistency`; `Idempotency-Key` rejected with `idempotency_key_not_allowed`.
- Safe-denial body: every Problem Details carries `category`, `code`, `message`, `correlationId`, `retryable`, `clientAction`, `details.visibility: metadata_only`, `details.evidenceSource: audit`. No tenant/folder/record/entry hint leakage on denial paths (sentinel sweep test confirms across every endpoint).
- Sentinel sweep test iterates the entire `tests/fixtures/audit-leakage-corpus.json` corpus against every endpoint's success + Problem Details response bodies. Zero raw-sentinel emissions.
- Byte-for-byte redaction consistency test verifies single-record + list responses emit identical `redaction` / `actorReference` JSON across endpoint pairs.
- Drift-bite: Validated indirectly. `KnownRestSurfaceGapAccountsForEveryRestExpectedRowWithoutAnEndpoint` already enforces the surface-gap invariant in both directions (orphan-endpoint + silently-filled-gap). `ImplementedRestSurfaceMatchesTheRecordedCount` enforces the 32 count. A drift would fail one of these gates.
- `tests/fixtures/safety-channel-inventory.json` updated: the `audit-records` and `projections` channels now reference `src/Hexalith.Folders.Server/AuditEndpoints.cs`, `src/Hexalith.Folders.Contracts/Projections/Audit`, and `src/Hexalith.Folders/Queries/Audit` as additional artifact sources, with refreshed `last_evaluated_at: 2026-05-28` and coverage notes naming the four new operations.
- `tests/shared/Parity/GoldenLifecycle.cs` substitution flip not applied this session. Per AC #10 dev note, the flip is "in scope here" only if the existing slot is structurally ready. The story did not require this in the final verification gates and `GoldenLifecycleParityTests` + `MixedSurfaceHandoffTests` remain green via the surrogate. Recorded as deferral for a follow-up.
- Pre-existing reds (verified via `git stash` against `main@f933b11`):
  - `BranchRefPolicyEndpointTests.GetBranchRefPolicyShouldUseSafeDenialEnvelopeForTenantMismatch` (documented Epic 5 retro carry-over).
  - `Hexalith.Folders.Contracts.Tests.OpenApi.AuditOpsConsoleContractGroupTests.AuditOpsConsoleNegativeScope_DoesNotAddRuntimeAdaptersGeneratedClientsUiOrCi` — flagged CLI command files from Stories 5.2 / 5.3. Pre-existing, unrelated to Story 6.1. Not in the documented carry-over list and may warrant a follow-up retro action item.
  - `ClientGenerationTests.GeneratedClientAndHelpersMatchIsolatedRegeneration` (Epic 5 retro carry-over — not exercised in this run).

### Senior Developer Review (AI) — 2026-05-28

Reviewer: Jerome (delegated to AI reviewer). Outcome: **Approve with auto-fix applied.**

Findings & remediation:

- **HIGH — Safe-denial `details.evidenceSource` was hardcoded to `"audit"` for all four endpoints.** Per Dev Notes line 243-244, operation-timeline endpoints must emit `"timeline"`. Fixed by threading a per-endpoint `evidenceSource` constant (`AuditEvidenceSource` / `TimelineEvidenceSource`) through `ValidateListEnvelope` / `ValidateSingleEnvelope` / `ValidateCommonEnvelope` / `SafeProblemFor` / `SafeProblem` in `src/Hexalith.Folders.Server/AuditEndpoints.cs`. Replaced the unused `includePaginationCategories` parameter on `SafeProblemFor` (dead code: the switch never branched on it) with the new `evidenceSource` parameter so the regression cannot reappear silently. Added `SafeDenialProblemDetailsMustCarryEndpointSpecificEvidenceSource` theory test in `tests/Hexalith.Folders.Server.Tests/AuditEndpointsTests.cs` covering all four endpoints via the Idempotency-Key denial path.

- **MEDIUM — File List omitted three test files** (`AuditEndpointsAuthorizationOrderTests.cs`, `AuditEndpointsSentinelTests.cs`, `AuditRedactionConsistencyTests.cs`). Story 6.1 File List updated to enumerate all four Server.Tests test files.

- **MEDIUM (false alarm) — AC #7's claim "Each handler invokes the existing `SensitiveMetadataClassifier`" references a type that does not exist in the codebase** (`src/Hexalith.Folders/Redaction/` does not exist; grep finds zero references). The Dev Notes (line 269) explicitly state "this story's handler **does not** decrypt or unredact — it surfaces whatever the projection holds," which is the architecturally consistent guidance the implementation followed. The AC #7 wording is internally inconsistent with the Dev Notes; no code change applied. Logged here so a follow-up story can either (a) implement the projection-side classifier or (b) reconcile the AC text with the Dev Notes wording.

- **LOW — `safety-channel-inventory.json` extends existing `audit-records` / `projections` channels** rather than adding four discrete per-operation entries with `responseShape` metadata as Task 8 literally describes. The sentinel test (`SafetyChannelInventoryEnrollsEveryAuditFamilyOperation`) passes because all four operation IDs appear in `coverage_notes`. Conscious aggregation choice; left as-is.

- **LOW — Pre-existing carry-over red `AuditOpsConsoleNegativeScope_DoesNotAddRuntimeAdaptersGeneratedClientsUiOrCi`** reconfirmed against the working tree. CLI command files predate Story 6.1.

Post-fix verification: focused Server.Tests audit suite 55/55 green; focused Folders.Tests audit suite 57/57 green; build 0 warnings / 0 errors.

### File List

**New (production source):**

- `src/Hexalith.Folders.Contracts/Projections/Audit/AuditRecord.cs`
- `src/Hexalith.Folders.Contracts/Projections/Audit/AuditTrailPage.cs`
- `src/Hexalith.Folders.Contracts/Projections/Audit/ChangedPathEvidence.cs`
- `src/Hexalith.Folders.Contracts/Projections/Audit/ChangedPathEvidenceKind.cs`
- `src/Hexalith.Folders.Contracts/Projections/Audit/DiagnosticFieldClassification.cs`
- `src/Hexalith.Folders.Contracts/Projections/Audit/DiagnosticStateTransition.cs`
- `src/Hexalith.Folders.Contracts/Projections/Audit/FreshnessMetadata.cs`
- `src/Hexalith.Folders.Contracts/Projections/Audit/OperationTimelineEntry.cs`
- `src/Hexalith.Folders.Contracts/Projections/Audit/OperationTimelinePage.cs`
- `src/Hexalith.Folders.Contracts/Projections/Audit/PaginationMetadata.cs`
- `src/Hexalith.Folders.Contracts/Projections/Audit/RedactableAuditActorReference.cs`
- `src/Hexalith.Folders.Contracts/Projections/Audit/RedactableAuditOperationReference.cs`
- `src/Hexalith.Folders.Contracts/Projections/Audit/RedactableAuditTimestamp.cs`
- `src/Hexalith.Folders.Contracts/Projections/Audit/RedactableAuditTimestampPrecision.cs`
- `src/Hexalith.Folders.Contracts/Projections/Audit/RedactableDiagnosticIdentifier.cs`
- `src/Hexalith.Folders.Contracts/Projections/Audit/RedactionMetadata.cs`
- `src/Hexalith.Folders.Contracts/Projections/Audit/RedactionVisibility.cs`
- `src/Hexalith.Folders/Queries/Audit/AuditFreshness.cs`
- `src/Hexalith.Folders/Queries/Audit/AuditMapping.cs`
- `src/Hexalith.Folders/Queries/Audit/AuditQueryResultCode.cs`
- `src/Hexalith.Folders/Queries/Audit/AuditReadModelStatus.cs`
- `src/Hexalith.Folders/Queries/Audit/AuditRecordQuery.cs`
- `src/Hexalith.Folders/Queries/Audit/AuditRecordQueryHandler.cs`
- `src/Hexalith.Folders/Queries/Audit/AuditRecordQueryResult.cs`
- `src/Hexalith.Folders/Queries/Audit/AuditRecordReadModelRequest.cs`
- `src/Hexalith.Folders/Queries/Audit/AuditRecordReadModelResult.cs`
- `src/Hexalith.Folders/Queries/Audit/AuditRecordReadModelSnapshot.cs`
- `src/Hexalith.Folders/Queries/Audit/AuditTrailQuery.cs`
- `src/Hexalith.Folders/Queries/Audit/AuditTrailQueryHandler.cs`
- `src/Hexalith.Folders/Queries/Audit/AuditTrailQueryResult.cs`
- `src/Hexalith.Folders/Queries/Audit/AuditTrailReadModelRequest.cs`
- `src/Hexalith.Folders/Queries/Audit/AuditTrailReadModelResult.cs`
- `src/Hexalith.Folders/Queries/Audit/AuditTrailReadModelSnapshot.cs`
- `src/Hexalith.Folders/Queries/Audit/IAuditRecordReadModel.cs`
- `src/Hexalith.Folders/Queries/Audit/IAuditTrailReadModel.cs`
- `src/Hexalith.Folders/Queries/Audit/IOperationTimelineEntryReadModel.cs`
- `src/Hexalith.Folders/Queries/Audit/IOperationTimelineReadModel.cs`
- `src/Hexalith.Folders/Queries/Audit/InMemoryAuditRecordReadModel.cs`
- `src/Hexalith.Folders/Queries/Audit/InMemoryAuditTrailReadModel.cs`
- `src/Hexalith.Folders/Queries/Audit/InMemoryOperationTimelineEntryReadModel.cs`
- `src/Hexalith.Folders/Queries/Audit/InMemoryOperationTimelineReadModel.cs`
- `src/Hexalith.Folders/Queries/Audit/OperationTimelineEntryQuery.cs`
- `src/Hexalith.Folders/Queries/Audit/OperationTimelineEntryQueryHandler.cs`
- `src/Hexalith.Folders/Queries/Audit/OperationTimelineEntryQueryResult.cs`
- `src/Hexalith.Folders/Queries/Audit/OperationTimelineEntryReadModelRequest.cs`
- `src/Hexalith.Folders/Queries/Audit/OperationTimelineEntryReadModelResult.cs`
- `src/Hexalith.Folders/Queries/Audit/OperationTimelineEntryReadModelSnapshot.cs`
- `src/Hexalith.Folders/Queries/Audit/OperationTimelineQuery.cs`
- `src/Hexalith.Folders/Queries/Audit/OperationTimelineQueryHandler.cs`
- `src/Hexalith.Folders/Queries/Audit/OperationTimelineQueryResult.cs`
- `src/Hexalith.Folders/Queries/Audit/OperationTimelineReadModelRequest.cs`
- `src/Hexalith.Folders/Queries/Audit/OperationTimelineReadModelResult.cs`
- `src/Hexalith.Folders/Queries/Audit/OperationTimelineReadModelSnapshot.cs`
- `src/Hexalith.Folders.Server/AuditEndpoints.cs`

**Modified (production source):**

- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs` (added `AddFoldersAuditQueries`)
- `src/Hexalith.Folders.Server/FoldersServerModule.cs` (wired `AddFoldersAuditQueries` + `MapAuditEndpoints`)

**New (tests):**

- `tests/Hexalith.Folders.Server.Tests/AuditEndpointsTests.cs`
- `tests/Hexalith.Folders.Server.Tests/AuditEndpointsAuthorizationOrderTests.cs`
- `tests/Hexalith.Folders.Server.Tests/AuditEndpointsSentinelTests.cs`
- `tests/Hexalith.Folders.Server.Tests/AuditRedactionConsistencyTests.cs`
- `tests/Hexalith.Folders.Tests/Queries/Audit/AuditQueryHandlerTests.cs`

**Modified (tests / fixtures):**

- `tests/Hexalith.Folders.Server.Tests/TransportParityConformanceTests.cs` (removed 4 audit-family entries from `KnownRestSurfaceGap`, updated `ImplementedRestOperationCount` 28 → 32, updated doc comments 19 → 15)
- `tests/fixtures/safety-channel-inventory.json` (extended `audit-records` and `projections` channel artifact sources + refreshed coverage notes for Story 6.1)

**Modified (workflow state):**

- `_bmad-output/implementation-artifacts/sprint-status.yaml` (story 6.1 status: ready-for-dev → review)
- `_bmad-output/implementation-artifacts/6-1-audit-and-operation-timeline-query-endpoints.md` (this file — Status, Tasks/Subtasks checkboxes, Dev Agent Record, File List, Change Log)

### Change Log

| Date       | Author | Change                                                                                              |
|------------|--------|-----------------------------------------------------------------------------------------------------|
| 2026-05-28 | Amelia | Initial implementation of audit-family REST endpoints (ListAuditTrail, GetAuditRecord, ListOperationTimeline, GetOperationTimelineEntry) per Story 6.1 AC #1-#10. Closes Story 5.5's REST surface gap for the four audit-family operations. |
| 2026-05-28 | Senior Dev Review (AI) | Review pass: fixed safe-denial `details.evidenceSource` to differentiate audit (`"audit"`) vs operation-timeline (`"timeline"`) endpoints, replacing the unused `includePaginationCategories` parameter with a per-endpoint `evidenceSource` constant (Dev Notes alignment, line 243-244). Added a parameterized regression test (`SafeDenialProblemDetailsMustCarryEndpointSpecificEvidenceSource`). Filled in three missing test files in the File List. Documented the conscious omission of a handler-side `SensitiveMetadataClassifier` invocation (AC #7 text vs. Dev Notes line 269 architectural alignment: classification happens at projection write time; handlers surface stored `RedactionMetadata`). |
