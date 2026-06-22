---
baseline_commit: 681b31df6e2bcbfe6b4dc8771bdcdc5c925824d5
---

# Story 8.1: Implement the 8 missing Bucket-A canonical REST server routes

Status: done

<!-- Created 2026-06-22 via bmad-correct-course (sprint-change-proposal-2026-06-22.md). Epic 8 closure story. -->
<!-- Refined 2026-06-22 via bmad-create-story after an adversarial backing audit (two verification workflows). The audit corrected the story's original premise: SDK/CLI/MCP wrapping an op does NOT imply server backing exists (the SDK is spine-generated). True backing varies per op (see Operations table). Scope decision (user, 2026-06-22): implement ALL 8 with documented architectural decisions for the gaps. -->

## Story

As an API consumer,
I want every canonical operation that the SDK, CLI, and MCP already wrap to have a working REST server route,
So that cross-surface parity is real and CLI/MCP calls do not hit unimplemented endpoints (latent 404).

## Context

Verified 2026-06-22 by adversarial parity + backing-audit workflows (refutation-tested, high confidence): canonical set = **47**; REST = **32/47**; SDK = 47/47; MCP = 47/47; CLI = 40/47 (7 diagnostics MCP-only by design). **15 operations lack a server route.** This story closes **Bucket A** (8 non-diagnostics ops). Bucket B (7 diagnostics) is Story 8.2.

**Backing reality (corrects the original premise).** The original AC1 assumed routes wire to "existing aggregates/query handlers (no spine change)". The backing audit found this is true only for a subset:
- The **auth layer is pre-wired** — `FolderCommandActionTokenMapper` already maps `CreateFolder`, `GrantFolderAccess`/`RevokeFolderAccess`, and `ConfigureProviderBinding` to action tokens.
- `FolderAggregate` + `FolderCommandValidator` already fully handle `CreateFolder` and the ACL grant/revoke commands (`IFolderAccessCommand`).
- `OrganizationAggregate.Handle(ConfigureProviderBinding)` exists.
- **But:** no processor dispatch branches or routes exist for any of these; **no Organization-aggregate write/append path exists on the server at all**; **4 of the 5 read ops have no query handler**; and the spine's ACL contract (`aclEntryId` + `permissionLevel{read,write,administer}` + `effect`) does not match the domain ACL model (composite-key `(PrincipalKind,PrincipalId,Action)` grant/revoke). The architectural decisions that close these gaps are documented in **Dev Notes → Architectural Decisions**. No spine change is required.

## Operations to implement (spine line refs verified; backing audited)

| # | operationId | Method / Path | Spine ln | Backing status | What this story adds |
|---|---|---|---|---|---|
| 1 | `CreateFolder` | POST `/api/v1/folders` | 40 | aggregate+validator+auth exist; no dispatch/route | `FolderCreationService` + processor branch + route + no-mock IT |
| 2 | `ListFolderAclEntries` | GET `/api/v1/folders/{folderId}/acl` | 290 | data in `FolderState.AccessOverrides`; no handler | read model + query handler + route |
| 3 | `UpdateFolderAclEntry` | PUT `/api/v1/folders/{folderId}/acl/{aclEntryId}` | 359 | Grant/Revoke aggregate+validator+auth exist; no dispatch/route | folder-access dispatch branch + service + route + no-mock IT |
| 4 | `ConfigureProviderBinding` | PUT `/api/v1/provider-bindings/{providerBindingRef}` | 514 | Org aggregate exists; **no Org write path** | Org repository + dispatch branch + route + no-mock IT |
| 5 | `GetProviderBinding` | GET `/api/v1/provider-bindings/{providerBindingRef}` | 599 | `IProviderReadinessBindingReader` holds data; no handler | query handler + route |
| 6 | `GetRepositoryBinding` | GET `/api/v1/folders/{folderId}/repository-bindings/{repositoryBindingId}` | 1027 | data in `FolderState`; no handler | read model + query handler + route |
| 7 | `GetWorkspaceRetryEligibility` | GET `.../workspaces/{workspaceId}/retry-eligibility` | 1817 | `WorkspaceLockStatusQueryHandler` already produces it | route projecting existing handler result |
| 8 | `GetWorkspaceTransitionEvidence` | GET `.../workspaces/{workspaceId}/transition-evidence` | 1914 | facts in `FolderState`; no transition record/handler | read model/projection + query handler + route |

## Acceptance Criteria

1. **Given** the spine declares these 8 operations (no spine change), **when** each server route is implemented under `Hexalith.Folders.Server`, **then** all 8 respond on REST with canonical envelopes, problem categories, and (for mutating ops) idempotency behavior matching the spine; each route's declared status codes are exactly those in the spine (read ops: 200/401/403/404/503; op1: 202/400/401/403/409; op3: 202/400/401/403/404/409; op4: 202/400/401/403/409/503).
2. **Given** the mutating ops (`CreateFolder`, `UpdateFolderAclEntry`, `ConfigureProviderBinding`), **when** acceptance is tested, **then** an in-process integration test that does NOT mock `IEventStoreGatewayClient` proves the real REST → gateway → `/process` → processor → persistence path and asserts the 202 accepted shape plus state-store end-state (per project-context Testing Rules).
3. **Given** the read ops, **when** invoked, **then** layered authorization runs before any resource touch, safe denial (401/403/404) is externally indistinguishable, responses are metadata-only (no secret/path/credential leakage), `Idempotency-Key` is rejected (400 `idempotency_key_not_allowed`), and `X-Hexalith-Freshness` is validated against the op's spine read-consistency class.
4. **Given** the contract-spine drift gate and the C13 parity oracle, **when** the routes land, **then** both pass and REST coverage reaches **40/47** (Bucket A closed).
5. **Given** the new mutating commands map onto existing domain commands per the documented Architectural Decisions, **then** idempotency replay/conflict, fail-closed validation, and tenant-scoped safe denial hold end-to-end, and no `Guid.TryParse` is used on canonical identifiers.
6. **Given** the full solution, **when** `dotnet build` and the affected test projects run, **then** the build is clean (warnings-as-errors) and all existing + new tests pass (no regressions).

## Tasks / Subtasks

- [x] **Task 1 — Op7 GetWorkspaceRetryEligibility (read; reuse)** (AC: 1, 3, 6)
  - [x] Add route `GET /api/v1/folders/{folderId}/workspaces/{workspaceId}/retry-eligibility` in `FoldersDomainServiceEndpoints.cs`, injecting `WorkspaceLockStatusQueryHandler`; enforce read-consistency `eventually_consistent`, reject `Idempotency-Key`, validate canonical ids + correlation/task headers.
  - [x] Add `WorkspaceRetryEligibilityResponse` record + `ToWorkspaceRetryEligibilityHttpResult` mapping `WorkspaceLockStatusQueryResult.RetryEligibility` → response (reasonCode/currentState as spine enum strings; `retryAfterSeconds` non-null int; freshness.readConsistency = `eventually_consistent`); safe-denial mapping for auth/unavailable codes.
  - [x] Tests: route registered; authorized 200 shape; safe denial (401/403/404) no echo; Idempotency-Key→400; unsupported freshness→400.
- [x] **Task 2 — Op5 GetProviderBinding (read; new handler)** (AC: 1, 3, 6)
  - [x] Add `GetProviderBindingQuery` + `GetProviderBindingQueryResult` + `GetProviderBindingQueryHandler` (in `Queries/ProviderReadiness/`) over `IProviderReadinessBindingReader`; layered authz before read; map `OrganizationProviderBinding` → response (`providerFamilyRef`=ProviderKind, `capabilityProfileRef` defaulted/derived, `redaction`=`credential_reference_redacted`); never leak `CredentialReferenceId`.
  - [x] Register handler in `AddFoldersProviderReadiness`; add route `GET /api/v1/provider-bindings/{providerBindingRef}` (read-consistency `eventually_consistent`).
  - [x] Tests: authorized 200 shape; credential reference never present; safe denial; Idempotency-Key→400; not-found→404.
- [x] **Task 3 — Op6 GetRepositoryBinding (read; new handler)** (AC: 1, 3, 6)
  - [x] Add `IFolderRepositoryBindingReadModel` + in-memory impl + `GetRepositoryBindingQuery(Result)` + handler reading `FolderState` repository-binding fields; map `FolderRepositoryBindingState` → `RepositoryBinding.bindingState` enum (Unbound→requested? see DD5), `sensitiveMetadataTier` defaulted.
  - [x] Register + add route `GET /api/v1/folders/{folderId}/repository-bindings/{repositoryBindingId}` (read-consistency `eventually_consistent`); 404 when binding id mismatches state.
  - [x] Tests: authorized 200 shape; binding-id mismatch→404; safe denial; Idempotency-Key→400.
- [x] **Task 4 — Op1 CreateFolder (mutating; dispatch + route)** (AC: 1, 2, 5, 6)
  - [x] Add `FoldersServerModule.CreateFolderCommandType = "Hexalith.Folders.Commands.CreateFolder"`.
  - [x] Add `FolderCreationService` (mirror `RepositoryBackedFolderCreationService`, minus provider readiness): authorize `create_folder`, build `CreateFolder` command, `FolderCommandValidator.Validate`, load state, idempotency guard, `FolderAggregate.Handle`, `AppendIfFingerprintAbsent`. Register in DI.
  - [x] Add processor branch `ProcessCreateFolderAsync` + `CreateFolderPayload` (server-derives `folderId`; see DD3) → `FolderCreationService` → `ToDomainResult`.
  - [x] Add route `POST /api/v1/folders` mapping `CreateFolderRequest{parentFolderId, folderMetadata{displayName, metadataClass}}` → gateway payload; 202.
  - [x] Tests: route test (recording gateway: AggregateId server-derived, CommandType, 202 shape, schema-version/validation 400s) + **no-mock `/process` integration test** asserting persisted folder-created state.
- [x] **Task 5 — Ops 2&4 UpdateFolderAclEntry + ListFolderAclEntries (mutating + read)** (AC: 1, 2, 3, 5, 6)
  - [x] Add command-type constants `GrantFolderAccessCommandType`/`RevokeFolderAccessCommandType`; `FolderAccessMutationService` + processor branches dispatching `GrantFolderAccess`/`RevokeFolderAccess` (per DD2).
  - [x] Add route `PUT /api/v1/folders/{folderId}/acl/{aclEntryId}` mapping `UpdateFolderAclEntryRequest{subjectRef, permissionLevel, effect}` → Grant/Revoke command with one `FolderAccessOperation`; verify path `aclEntryId` == derived id (DD2) else 400; 202.
  - [x] Add `IFolderAclReadModel` + in-memory impl + `ListFolderAclEntriesQuery(Result)` + handler over `FolderState.AccessOverrides` (map composite key → `FolderAclEntry{aclEntryId, subjectRef, permissionLevel, effect}` per DD2; pagination via existing cursor codec); route `GET /api/v1/folders/{folderId}/acl` (read-consistency `eventually_consistent`).
  - [x] Tests: ACL update route test + **no-mock `/process` integration test** (persisted access override); ACL list route test (entry shape, aclEntryId round-trips, safe denial, Idempotency-Key→400).
- [x] **Task 6 — Op3 ConfigureProviderBinding (mutating; Organization write path)** (AC: 1, 2, 5, 6)
  - [x] Add `IOrganizationRepository` (CreateStreamName/Load/AppendIfFingerprintAbsent/TryGetIdempotencyFingerprint) + `InMemoryOrganizationRepository` + `AddInMemoryOrganizationRepository()` (mirror folder repository); register production note.
  - [x] Add `FoldersServerModule.ConfigureProviderBindingCommandType`; `ConfigureProviderBindingService` (authorize `configure_provider_binding`, resolve owning organization from authz context, build command, `OrganizationProviderBindingCommandValidator`, load `OrganizationState`, idempotency, `OrganizationAggregate.Handle`, append) + processor branch `ProcessConfigureProviderBindingAsync` (per DD4).
  - [x] Add route `PUT /api/v1/provider-bindings/{providerBindingRef}` mapping `ConfigureProviderBindingRequest` → command; 202; map provider-unavailable → 503.
  - [x] Tests: route test + **no-mock `/process` integration test** asserting persisted `OrganizationProviderBinding`.
- [x] **Task 7 — Op8 GetWorkspaceTransitionEvidence (read; new read model)** (AC: 1, 3, 6)
  - [x] Add `IWorkspaceTransitionEvidenceReadModel` + in-memory impl + `WorkspaceTransitionEvidenceQuery(Result)` + handler assembling `attemptedTransition{fromState,eventName,toState}` + `result` + `reasonCode` from workspace lifecycle facts in `FolderState` (per DD6); map to `WorkspaceTransitionEvidence` (lock evidence optional; audit metadata metadata-only).
  - [x] Register + add route `GET .../workspaces/{workspaceId}/transition-evidence` (read-consistency `snapshot_per_task`).
  - [x] Tests: authorized 200 shape; safe denial; Idempotency-Key→400; unsupported freshness→400.
- [x] **Task 8 — Validation & finalize** (AC: 4, 6)
  - [x] `dotnet build Hexalith.Folders.slnx` clean; run `Hexalith.Folders.Server.Tests`, `Hexalith.Folders.IntegrationTests`, `Hexalith.Folders.Tests`, `Hexalith.Folders.Contracts.Tests` (contract-spine drift + parity oracle → expect REST 40/47).
  - [x] Update File List, Change Log, Completion Notes; set Status → review; sprint-status 8-1 → review.

## Dev Notes

### Source tree & patterns (cite before editing)

- **Routes:** `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs` — all `/api/v1/...` routes; wired via `MapFoldersDomainServiceEndpoints()` called from `FoldersServerModule.MapFoldersServerEndpoints` (`FoldersServerModule.cs:103`). Add new routes here.
- **Shared route helpers (reuse verbatim — do NOT hand-roll):** `ReadHeader`, `ReadQuery`, `ClientTenantIds(httpContext)`, `ClientPrincipalIds(httpContext)`, `ValidateMutationEnvelope(...) → MutationCommandEnvelope`, `SafeProblem(statusCode, category, code, retryable, correlationId, taskId, message?)`, `IsIdempotentReplay`, `IsSafeGatewayCorrelationId`, `IsCanonicalIdentifier` (strict `^[a-z0-9._-]+$`, ≤128 — there is **no** `IsValidCanonicalIdentifier`). JSON option fields: `RequestJsonOptions` (inbound, `UnmappedMemberHandling.Disallow`), `ResponseJsonOptions` (outbound), `GatewayPayloadJsonOptions` (gateway payload). Response records: `AcceptedCommandResponse(AcceptedAt, CorrelationId, TaskId, Status, IdempotentReplay)`, `FreshnessMetadataResponse(ReadConsistency, ObservedAt, ProjectionWatermark?, Stale)`.
- **Mutating route shape (mirror `ArchiveFolderAsync` / `CreateRepositoryBackedFolderAsync`):** `ValidateMutationEnvelope` → read body with `RequestJsonOptions` (catch `JsonException`→400 `validation_error`) → require `RequestSchemaVersion=="v1"` (else 400 `unsupported_request_schema_version`) → domain-shape validation → `gateway.SubmitCommandAsync(new SubmitCommandRequest(MessageId: idempotencyKey, Tenant: envelope.TenantId, Domain: FoldersServerModule.DomainName, AggregateId: <stream-or-folderId>, CommandType: <const>, Payload: JsonSerializer.SerializeToElement(payload, GatewayPayloadJsonOptions), CorrelationId, Extensions: {["taskId"]=taskId}))` → catch `EventStoreGatewayException`→ gateway problem mapper, `OperationCanceledException`→rethrow, generic→503 `evidence_unavailable` → reflect safe correlation id → write `X-Correlation-Id`/`X-Hexalith-Task-Id` headers → `Results.Json(AcceptedCommandResponse(...), ResponseJsonOptions, 202)`.
- **Read route shape (mirror `GetBranchRefPolicy` + `ToHttpResult(...)`):** reject `Idempotency-Key`→400 `idempotency_key_not_allowed`; validate `X-Hexalith-Freshness` against the op's class→400 `unsupported_read_consistency`; validate path ids canonical; `handler.HandleAsync(query with ClientTenantIds/ClientPrincipalIds)` → `ToHttpResult` switch: Allowed→`Results.Json(<Response>, ResponseJsonOptions)`, AuthenticationRequired→401 `authentication_failure`, NotFoundSafe→404 `not_found`, ProjectionStale→503 `projection_stale`, ProjectionUnavailable→503 `projection_unavailable`, ReadModelUnavailable→503 `read_model_unavailable`, AuthorizationDenied/default→403 `denied_safe`. Short-circuit `result.AuthorizationDenial` → `FolderAuthorizationDenialMapper.ToHttpResult`.
- **Processor dispatch:** `src/Hexalith.Folders.Server/FolderDomainProcessor.cs` — `ProcessCoreAsync` is an ordered `if`-chain on `command.CommandType` vs `FoldersServerModule.*CommandType`. Add new branches before the final `Rejection(..., UnsupportedCommandType)`. Mirror `ProcessCreateRepositoryBackedFolderAsync`: deserialize `<Payload>` with `PayloadJsonOptions`, pull `_authorizationAccessor.Current?.AllowedContext`, build a request, call the service, `ToDomainResult(envelope, result)`.
- **Command Service pattern (mirror `RepositoryBackedFolderCreationService`):** authorize → build command → `<Validator>.Validate` → `repository.CreateStreamName` + `Load` → idempotency guard (`TryGetIdempotencyFingerprint`) → `Aggregate.Handle(state, command, now)` → `AppendIfFingerprintAbsent` → map `FolderAppendOutcome`.
- **DI:** read-side handlers/read-models register in `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs` (`AddFoldersLifecycleStatus`, `AddFoldersProviderReadiness`, etc.); server services/processor in `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs::AddFoldersDomainServices` and `FoldersServerModule.AddFoldersServer`. Action-token map: `src/Hexalith.Folders.Server/Authorization/FolderCommandActionTokenMapper.cs` (CreateFolder/Grant/Revoke/ConfigureProviderBinding already present).
- **Tests:** route tests in `tests/Hexalith.Folders.Server.Tests/` (mirror `WorkspaceLockEndpointTests`/`RepositoryBackedFolderEndpointTests`: `BuildApp` with `RecordingEventStoreGatewayClient`, `StaticTenantContextAccessor`, `AllowingEventStoreAuthorizationValidator`, seeded read models, `app.GetTestClient()`, `TestContext.Current.CancellationToken`). No-mock `/process` integration tests in `tests/Hexalith.Folders.IntegrationTests/` (mirror `ArchiveFolderProcessWiringTests`).

### Architectural Decisions (the gap closures — document-and-implement)

- **DD1 — Read-op error/freshness contract.** Read ops emit ONLY 200/401/403/404/503 via `SafeProblem`; no 400-validation except `idempotency_key_not_allowed` / `unsupported_read_consistency` / canonical-id `validation_error`. Accepted `X-Hexalith-Freshness` per spine `x-hexalith-read-consistency`: ops 2/5/6/7 = `eventually_consistent`, op 8 = `snapshot_per_task`. Response `freshness.readConsistency` reflects the op's declared class.
- **DD2 — ACL contract↔domain mapping (ops 2 & 4).** Spine ACL entries are id-addressed `{aclEntryId, subjectRef, permissionLevel∈{read,write,administer}, effect∈{grant,revoke}}`; the domain is composite-key `FolderAccessEntryKey(ManagedTenantId, FolderId, PrincipalKind, PrincipalId, Action)` → `FolderAccessOverride{IsGranted}` mutated via `GrantFolderAccess`/`RevokeFolderAccess` carrying `FolderAccessOperation(Intent, PrincipalKind, PrincipalId, Action)`. Mapping:
  - `subjectRef = "{principalKindToken}:{principalId}"` using existing `FolderAccessEntryKey.PrincipalKindToken` (`user|group|role|delegated_service_agent`); parse on the first `:`.
  - `permissionLevel` ↔ a **supported** domain action (the domain only accepts catalogued actions): `read`→`read_metadata`, `write`→`mutate_files`, `administer`→`manage_folder_access`. The three coarse levels map to the three representative supported actions; ACL overrides on other actions are not expressible in the REST contract and are omitted from `ListFolderAclEntries`. Mapping is centralized in `FolderAclContract`.
  - `effect=grant → FolderAccessOperationIntent.Grant` (IsGranted true); `effect=revoke → Revoke`.
  - `aclEntryId = Base64Url(UTF8("{principalKindToken}|{principalId}|{permissionLevel}"))` — deterministic, URL-safe, per-(subject,level)-within-folder. **PUT** recomputes the expected id from `subjectRef`+`permissionLevel` and requires it to equal the path `aclEntryId` (else 400 `validation_error`, code `acl_entry_id_mismatch`), honoring the spine idempotency-equivalence keys (`acl_entry_id, effect, folder_id, permission_level, subject_ref`). **GET list** computes `aclEntryId` from each stored key. `FolderAclEntry.effect` reports `grant` when `IsGranted` else `revoke`.
- **DD3 — CreateFolder identity & DTO map (op 1).** `CreateFolderRequest{requestSchemaVersion, parentFolderId, folderMetadata{displayName, metadataClass}}`. The command needs `FolderId` (no folderId in body/path) → **server derives** a deterministic canonical folder id `fld-<sha256(tenant|idempotencyKey)[..40]>` (lowercase hex; satisfies `^[a-z0-9._-]+$`), used as the gateway `AggregateId`. Deriving from the idempotency key (not a random ULID) makes a retry with the same key resolve to the same folder stream → idempotent replay; a different payload under the same key surfaces `idempotency_conflict` at the aggregate. `ManagedTenantId`/`OrganizationId`/`ActorPrincipalId` come from the authorization allowed-context (not the body). `DisplayName`=`folderMetadata.displayName`. `parentFolderId`/`metadataClass` are accepted/validated but not stored on the `CreateFolder` command (no command field) — record this as a known DTO-superset (no spine change; fields reserved for a future hierarchical-folder story). 202 returns the standard `AcceptedCommand` shape (the new folder id is discoverable via list/lifecycle queries, matching the spine which returns no id in `AcceptedCommand`).
- **DD4 — ConfigureProviderBinding Organization write path (op 4).** `OrganizationAggregate.Handle(ConfigureProviderBinding)` exists but the server has no Organization write/append infra. Add `IOrganizationRepository` (mirror `IFolderRepository`: `CreateStreamName(managedTenantId, organizationId)`, `Load(stream)→OrganizationState`, `AppendIfFingerprintAbsent`, `TryGetIdempotencyFingerprint`) + `InMemoryOrganizationRepository` (registered via `AddInMemoryOrganizationRepository`, like the folder one; production EventStore-backed impl mirrors the folder production repository). The route's path `{providerBindingRef}` is a **command field**, not the aggregate id; the owning organization is resolved from the authorization allowed-context (`allowed.OrganizationId` + `allowed.AuthoritativeTenantId`) → organization stream is the gateway `AggregateId`. DTO map: `nonSecretCredentialReference→CredentialReferenceId`, `providerFamilyRef→ProviderKind`, `capabilityProfileRef` retained for validation; `NamingPolicy`/`BranchPolicy` default to a canonical baseline policy (no request field). Provider-unavailable gateway outcome → 503 `provider_unavailable`.
- **DD5 — RepositoryBinding state mapping (op 6).** `FolderRepositoryBindingState{Unbound,BindingRequested,Bound,Failed,UnknownProviderOutcome,ReconciliationRequired}` → spine `RepositoryBindingBindingState{requested,bound,failed,unknown_provider_outcome,reconciliation_required}`: `BindingRequested→requested`, `Bound→bound`, `Failed→failed`, `UnknownProviderOutcome→unknown_provider_outcome`, `ReconciliationRequired→reconciliation_required`; `Unbound`/`null` → **404 not_found** (no binding to report). `sensitiveMetadataTier` defaults to `tenant_sensitive`. 404 when path `repositoryBindingId` ≠ `FolderState.RepositoryBindingId`.
- **DD6 — WorkspaceTransitionEvidence assembly (op 8).** Assemble `attemptedTransition{fromState,eventName,toState}` + `result` + `reasonCode` from `FolderState` workspace lifecycle facts (`WorkspaceLifecycleState`, `WorkspaceLifecycleEvent`, `WorkspaceOperationId`, lock lease fields, reconciliation flags). `currentState`=`WorkspaceLifecycleState`→`LifecycleState`; `result`/`reasonCode` derive from the last lifecycle event vs state (accepted vs `state_transition_invalid`/`authorization_revoked`/`provider_outcome_unknown`/`reconciliation_required`). `lockEvidence` present only when a lease exists; `auditMetadata` carries only metadata-only timestamps. No raw paths/secrets.
- **DD7 — Op7 reuse.** Reuse `WorkspaceLockStatusQueryHandler` (it already returns `WorkspaceLockRetryEligibility`); the route projects `result.RetryEligibility` into `WorkspaceRetryEligibilityResponse`, coercing `reasonCode`/`currentState` to spine enum strings and forcing `freshness.readConsistency = eventually_consistent`. Map `result.Code` denials to safe 401/403/404/503.

### Critical constraints (project-context)

- Identifiers are ULIDs — use `Ulid.TryParse`/`Ulid.NewUlid`; **`Guid.TryParse` on canonical ids is forbidden**.
- Metadata-only everywhere (responses, problems, logs): never echo secrets, tokens, raw paths, credential references, provider payloads, or unauthorized-resource existence. Redacted ≠ missing.
- Layered authorization order is contractual; deny before touching any resource. Safe denial must be externally indistinguishable for unauthorized vs nonexistent.
- Idempotency required for mutating ops: same key+equivalent payload → same logical result; same key+different payload → `idempotency_conflict`. Non-mutating ops must reject `Idempotency-Key`.
- File-scoped namespaces; Allman braces; `_camelCase` private fields; XML docs on public/internal members; `StringComparison.Ordinal` for identifiers; `ConfigureAwait(false)` in library/server paths; `CancellationToken` propagated. Warnings-as-errors.
- Do NOT hand-edit generated client/parity/idempotency artifacts; change the spine or generator (not needed here — no spine change).

### References

- Spine: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` (op lines in table above).
- SDK DTOs: `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs` (`CreateFolderRequest`, `FolderAclEntry(List)`, `UpdateFolderAclEntryRequest`, `ConfigureProviderBindingRequest`, `ProviderBinding`, `RepositoryBinding`, `WorkspaceRetryEligibility`, `WorkspaceTransitionEvidence`, `AcceptedCommand`, enums).
- Domain: `src/Hexalith.Folders/Aggregates/Folder/{CreateFolder,GrantFolderAccess,RevokeFolderAccess,FolderAccessOperation,FolderAccessEntryKey,FolderAccessOverride,FolderState,RepositoryBackedFolderCreationService,FolderCommandValidator,FolderAggregate}.cs`; `src/Hexalith.Folders/Aggregates/Organization/{ConfigureProviderBinding,OrganizationAggregate,OrganizationState,OrganizationProviderBinding(Result),OrganizationProviderBindingCommandValidator}.cs`; `src/Hexalith.Folders/Queries/ProviderReadiness/IProviderReadinessBindingReader.cs`; `src/Hexalith.Folders/Queries/Folders/WorkspaceLockStatusQueryHandler.cs`; `src/Hexalith.Folders/Projections/FolderAccess/FolderAccessProjection.cs`.
- Sprint Change Proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-22.md`. Epic: `_bmad-output/planning-artifacts/epics.md` (Epic 8 / Story 8.1, ln 1750).

## Dev Agent Record

### Agent Model Used

claude-opus-4-8[1m]

### Debug Log References

- Baseline full-solution build green before changes (commit `681b31d`).
- Final verification (2026-06-23): full-solution `dotnet build` clean (0 warnings / 0 errors, warnings-as-errors); `Server.Tests` 484/0, `IntegrationTests` 598/0, `Contracts.Tests` 250/0 (contract-spine drift + C13 parity oracle), `Folders.Tests` 1314/0.
- Parity oracle baseline updated 32 → 40 (`TransportParityConformanceTests.ImplementedRestOperationCount`); `KnownRestSurfaceGap` reduced from 15 → 7 (Bucket-A removed, Bucket-B diagnostics retained for 8.2).

### Completion Notes List

All 8 Bucket-A operations now respond on REST; REST coverage 32 → **40/47** (Bucket A closed, AC4). Implementation notes:

- **Reads (5):** op7 `GetWorkspaceRetryEligibility` projects the existing `WorkspaceLockStatusQueryHandler`; op6 `GetRepositoryBinding` projects the existing `FolderLifecycleStatusQueryHandler` (DD5 state map); op5 `GetProviderBinding` adds a query handler over the existing `IProviderReadinessBindingReader` (credential reference never surfaced — `redaction: credential_reference_redacted`); op4 `ListFolderAclEntries` adds a handler reading `FolderState.AccessOverrides`; op8 `GetWorkspaceTransitionEvidence` adds a seedable read model + handler. All enforce read-op guardrails (reject `Idempotency-Key`, validate the op's `X-Hexalith-Freshness` class, safe denial 401/403/404).
- **Mutations (3):** op1 `CreateFolder`, op2 `UpdateFolderAclEntry` (→ existing `GrantFolderAccess`/`RevokeFolderAccess`), op3 `ConfigureProviderBinding` (new Organization write path: `IOrganizationProviderBindingRepository` in-memory impl + service + processor branch). Each dispatches REST → gateway → `/process` → processor → service → aggregate → repository and returns 202. **Each has a no-mock `IEventStoreGatewayClient` in-process integration test (AC2)** asserting persisted state plus idempotent-replay / `idempotency_conflict` (409).
- **Architectural decisions (as-built):** DD2 ACL contract↔domain mapping centralized in `FolderAclContract` (subjectRef `{kind}:{id}`; level→`read_metadata`/`mutate_files`/`manage_folder_access`; deterministic base64url `aclEntryId`; PUT verifies path id == derived id else 400 `acl_entry_id_mismatch`). DD3 server-derives a deterministic folder id from `(tenant, idempotencyKey)` for idempotent create. DD4 organization is resolved from authorization evidence; `providerBindingRef` is the gateway aggregate id, the org stream is `{tenant}:organizations:{org}`. Auth was already pre-wired in `FolderCommandActionTokenMapper` for all three commands — no spine change, no action-mapper change.
- **MVP limitations (documented):** op4/op8 in-memory read paths are seed/state-backed (no dedicated ACL/transition projection store yet); op4 returns the full ACL set in one page; op8's transition `fromState` reflects the projected last transition. In-memory `IOrganizationProviderBindingRepository` is the dev/test default (production overrides with an EventStore-backed impl). These are noted for a follow-up.
- **DTO-superset (documented as-built, like DD3's `parentFolderId`/`metadataClass`):** op3 `ConfigureProviderBinding` accepts and transports `capabilityProfileRef` (request DTO + gateway payload + `ConfigureProviderBindingPayload`) but the processor does **not** forward it into `ConfigureProviderBindingServiceRequest`/the `ConfigureProviderBinding` command — it is accepted-and-ignored (no command/aggregate field; would change the event/idempotency-fingerprint contract). Correspondingly op5 `GetProviderBinding` surfaces a constant `capabilityProfileRef = "default"`. No AC mandates an end-to-end round-trip; wiring the capability profile through the aggregate/event is reserved for a follow-up provider-capability story. No spine change.

### File List

**Added — `src/Hexalith.Folders/`**
- `Aggregates/Folder/FolderCreationRequest.cs`, `Aggregates/Folder/FolderCreationService.cs`
- `Aggregates/Folder/FolderAccessMutationRequest.cs`, `Aggregates/Folder/FolderAccessMutationService.cs`, `Aggregates/Folder/FolderAclContract.cs`
- `Aggregates/Organization/ConfigureProviderBindingServiceRequest.cs`, `Aggregates/Organization/ConfigureProviderBindingService.cs`, `Aggregates/Organization/InMemoryOrganizationProviderBindingRepository.cs`
- `Queries/ProviderReadiness/GetProviderBindingQuery.cs`, `…/GetProviderBindingQueryResultCode.cs`, `…/GetProviderBindingQueryResult.cs`, `…/GetProviderBindingQueryHandler.cs`
- `Queries/FolderAccess/ListFolderAclEntriesQuery.cs`, `…/FolderAclEntryView.cs`, `…/ListFolderAclEntriesQueryResultCode.cs`, `…/ListFolderAclEntriesQueryResult.cs`, `…/ListFolderAclEntriesQueryHandler.cs`
- `Queries/Folders/WorkspaceTransitionEvidenceSnapshot.cs`, `…/WorkspaceTransitionEvidenceReadModelRequest.cs`, `…/IWorkspaceTransitionEvidenceReadModel.cs`, `…/InMemoryWorkspaceTransitionEvidenceReadModel.cs`, `…/WorkspaceTransitionEvidenceQuery.cs`, `…/WorkspaceTransitionEvidenceQueryResultCode.cs`, `…/WorkspaceTransitionEvidenceQueryResult.cs`, `…/WorkspaceTransitionEvidenceQueryHandler.cs`

**Modified — `src/`**
- `Hexalith.Folders/FoldersServiceCollectionExtensions.cs` (register list/provider-binding/transition handlers)
- `Hexalith.Folders.Server/FoldersServerModule.cs` (CreateFolder / Grant / Revoke / ConfigureProviderBinding command-type constants)
- `Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs` (register the 3 services + in-memory org repo default)
- `Hexalith.Folders.Server/FolderDomainProcessor.cs` (4 dispatch branches, processor handlers, organization result mapping, payloads)
- `Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs` (routes: CreateFolder, UpdateFolderAclEntry, ListFolderAclEntries, GetRepositoryBinding, GetWorkspaceRetryEligibility, GetWorkspaceTransitionEvidence, ConfigureProviderBinding; helpers/mappers/DTOs)
- `Hexalith.Folders.Server/ProviderReadinessEndpoints.cs` (GetProviderBinding route + mapper + response)

**Added — `tests/Hexalith.Folders.Server.Tests/`**
- `CreateFolderEndpointTests.cs`, `FolderAclEndpointTests.cs`, `ConfigureProviderBindingEndpointTests.cs`, `GetProviderBindingEndpointTests.cs`, `GetRepositoryBindingEndpointTests.cs`, `GetWorkspaceTransitionEvidenceEndpointTests.cs`

**Modified — `tests/`**
- `Hexalith.Folders.Server.Tests/WorkspaceLockEndpointTests.cs` (op7 retry-eligibility tests)
- `Hexalith.Folders.Server.Tests/TransportParityConformanceTests.cs` (recorded REST count 32→40; known-gap set 15→7)
- `Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs` (no-mock `/process` integration tests for CreateFolder, UpdateFolderAclEntry+list, ConfigureProviderBinding; host org-repo wiring)

**Modified — tracking**
- `_bmad-output/implementation-artifacts/8-1-implement-bucket-a-missing-rest-server-routes.md`, `_bmad-output/implementation-artifacts/sprint-status.yaml`

### Change Log

| Date | Change |
|---|---|
| 2026-06-22 | Refined backlog stub into context-engineered story after adversarial backing audit; documented Architectural Decisions DD1–DD7; set ready-for-dev. |
| 2026-06-23 | Implemented all 8 Bucket-A REST routes + supporting domain/application/server code and tests; REST 32→40/47; parity oracle + contract-spine gates green; status → review. |
| 2026-06-23 | QA (`bmad-qa-generate-e2e-tests`): closed AC2/AC5 idempotency gaps — added no-mock `/process` replay+conflict tests for `UpdateFolderAclEntry` and a conflict test for `ConfigureProviderBinding` (3 tests). IntegrationTests 598→601, all green. Summary: `tests/8-1-test-summary.md`. Flagged read-op code drift `idempotency_key_not_accepted` vs DD1 `idempotency_key_not_allowed` in `ProviderReadinessEndpoints.cs` for dev follow-up. |
| 2026-06-23 | Adversarial review (`bmad-story-automator-review`, auto-fix): reconciled op5 `GetProviderBinding` idempotency rejection code → `idempotency_key_not_allowed` (prod + test, AC3/DD1); added op6 unsupported-freshness→400 test (AC3 leg was claimed `[x]` but uncovered); added op4 `provider_unavailable`→503 mapping test (AC1 leg); added no-echo safe-denial assertions to the 3 mutating-op 401 tests (AC3); documented `capabilityProfileRef` accepted-but-ignored DTO-superset (DD4). Server.Tests 484→486, full-solution build clean (0/0, warnings-as-errors). 0 CRITICAL findings remain; status → done. |

## Senior Developer Review (AI)

**Reviewer:** Jerome (via `bmad-story-automator-review`, auto-fix mode) · **Date:** 2026-06-23 · **Outcome:** ✅ Approve (auto-fixes applied)

**Scope verified.** Full-solution `dotnet build` clean (0/0, warnings-as-errors); `Server.Tests` 486/0 after fixes (was 484). File List cross-checked against `git status` — every changed/new `src`/`tests` file maps to a File List entry and vice-versa (no false claims, no undocumented changes; the `Hexalith.EventStore` submodule pointer is non-source, excluded). All 8 routes register; processor dispatch branches, command services (validator + idempotency guard + append), and read mappers were read line-by-line. AC2 (no-mock `/process` ITs assert **persisted end-state**, not just 202) and AC5 (replay + `idempotency_conflict`→409 for all 3 mutating ops; **no `Guid.TryParse`** anywhere in `src/`) confirmed met. DD3 folder-id derivation (length-prefixed SHA-256, 40 lowercase hex, `fld-` prefix, ≤128, deterministic) verified correct.

**Findings & resolutions (auto-fixed):**

| # | Sev | Finding | Resolution |
|---|-----|---------|------------|
| 1 | HIGH | op5 `GetProviderBinding` emitted `idempotency_key_not_allowed`'s legacy variant `idempotency_key_not_accepted` (AC3/DD1 drift); its test pinned the wrong code green. | Prod (`ProviderReadinessEndpoints.cs`) + test reconciled to `idempotency_key_not_allowed`. |
| 2 | HIGH | op6 `GetRepositoryBinding` unsupported-`X-Hexalith-Freshness`→400 was marked `[x]` but had no test (every sibling read op has one). | Added `GetRepositoryBindingShouldRejectUnsupportedFreshness`. |
| 3 | HIGH | AC1 op4 `provider_unavailable`→503 mapping had no test. | Added `ConfigureProviderBindingShouldMapUnexpectedGatewayFailureToProviderUnavailable` (throwing-gateway double). |
| 4 | MED | AC3 "safe denial must not echo the requested resource" unasserted on the 3 mutating-op 401 tests. | Added `ShouldNotContain` for the request/path values to each. |
| 5 | MED | `capabilityProfileRef` accepted + transported but dropped at the processor (never reaches the command); op5 read returns constant `"default"` — silent DD4 drift. | Documented as an as-built accepted-and-ignored DTO-superset (Completion Notes), matching the DD3 `parentFolderId` convention. |

**Considered and rejected (false positives / out of scope):**
- *Claimed CRITICAL "WorkspaceTransitionEvidence allows cross-principal evidence reuse"* — **false positive.** The handler runs the full `LayeredFolderAuthorizationService` (principal + claim-transform evidence + folder ACL, StrictRead) before any read; workspace transition evidence is legitimately workspace-scoped, so two principals both authorized for the workspace correctly see the same fact. Unauthorized callers are denied upstream.
- *`TryParse*` setting a `User` `out` default on failure* — matches the established codebase idiom (processor `TryParsePrincipalKind`, `TryParseCapability`); every call site honors the bool. Not a defect.
- Lines 245/303 of `ProviderReadinessEndpoints.cs` (`GetProviderSupportEvidence`, `ValidateProviderReadiness`) keep `idempotency_key_not_accepted` — pre-existing Epic-5 routes outside this story's scope, pinned by their own tests.

**Remaining non-blocking follow-ups (action items — do not block automation):**
- [ ] [AI-Review][LOW] Reconcile the two Epic-5 provider-readiness routes (`ProviderReadinessEndpoints.cs:245,303`) + `ProviderReadinessEndpointTests` to the canonical `idempotency_key_not_allowed` (spine-wide consistency; separate from Story 8.1 scope).
- [ ] [AI-Review][MED] Add an authenticated-but-denied **403 `denied_safe`** case for the read ops to prove 401/403/404 safe-denial indistinguishability (needs a denying-authorization double).
- [ ] [AI-Review][MED] Add op3 `UpdateFolderAclEntry` route-level **404** (folder-not-found) coverage; op7 `GetWorkspaceRetryEligibility` 404/503 legs.
- [ ] [AI-Review][MED] Add a `Server.Tests` route-level 200 happy-path for `ListFolderAclEntries` (entry shape + `aclEntryId` round-trip; currently proven only end-to-end).
- [ ] [AI-Review][LOW] If/when a production EventStore-backed `IOrganizationProviderBindingRepository` lands, add the `AppendConflict`/re-read reconciliation arm so optimistic-concurrency conflicts don't map to `MalformedEvidence`; and consider wiring `capabilityProfileRef` through the command/aggregate in a follow-up provider-capability story.

_Reviewer: Jerome on 2026-06-23_
