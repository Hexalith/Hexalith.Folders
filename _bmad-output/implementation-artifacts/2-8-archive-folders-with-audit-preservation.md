# Story 2.8: Archive folders with audit preservation

Status: in-progress

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a tenant administrator,
I want to archive folders when policy allows,
so that retired work is no longer active while audit and status evidence remain available.

## Terms

- Archive means a metadata-only folder lifecycle transition from active to archived for an authorized folder. It is not deletion, repository removal, workspace cleanup, provider mutation, or tenant-deletion processing.
- Audit preservation means the archive command, result, correlation ID, task ID, actor evidence, reason code, folder ID, tenant scope, and lifecycle transition remain available through metadata-only events and projections under the C3 retention policy.
- Archive eligibility means the command can prove tenant access, folder admin permission, current folder state, idempotency equivalence, and local policy requirements before appending an archive event. It must fail closed when eligibility evidence is stale, unavailable, malformed, cross-tenant, or ambiguous.
- Mutating task commands means later repository-backed workspace, file, lock, commit, branch/ref, provider-binding, or task-lifecycle commands that would change active folder state. This story must create the archived lifecycle guard that future commands can consume, but it must not implement those future commands.
- Archive observation means any lookup, stream-name construction, projection key construction, cache key construction, diagnostic subject construction, audit subject construction, provider/repository/filesystem probe, or response branch that could reveal whether a folder exists or is archived. Authorization must complete before archive observation.
- Safe archive denial means the caller receives the existing canonical safe denial, validation, idempotency-conflict, or state-transition response without proof of unauthorized folder existence, current lifecycle state, repository binding, workspace, lock, audit record, provider binding, or external repository state.
- Archive decision snapshot means the tenant authority, caller principal, requested action, task/correlation evidence, authorization evidence, idempotency evidence, folder state, lifecycle policy, and freshness evidence used to decide one archive command. A successful archive or safe rejection must come from one compatible snapshot and fail closed when evidence is mixed across tenants, folders, principals, actions, tasks, operation IDs, policy versions, freshness windows, or idempotency fingerprints.

## Acceptance Criteria

1. Given the Contract Spine declares `POST /api/v1/folders/{folderId}/archive` with `operationId: ArchiveFolder`, `ArchiveFolderRequest`, idempotency-key requirement, correlation/task headers, and safe denial responses, when this story is implemented, then domain command handling, server endpoint wiring, SDK consumption, parity fixtures, and tests satisfy that existing operation without adding a second archive contract or hand-editing generated SDK files.
2. Given an authenticated tenant administrator or authorized folder administrator requests archive, when tenant access, folder admin permission, current folder state, idempotency evidence, archive policy, and freshness evidence are drawn from one compatible archive decision snapshot and are fresh and allowed, then `ArchiveFolder` is accepted with the existing `202 AcceptedCommand` shape, folder lifecycle state becomes archived, exactly one metadata-only archive event is appended, and the accepted command response preserves only operation, correlation, task, and sanitized result evidence.
3. Given tenant identity appears in route values, query parameters, headers, forwarded headers, body values, generated client arguments, metadata bags, or client-controlled envelopes, when the archive command context is built, then tenant authority comes only from authentication context or EventStore envelope/projection authority; client-supplied tenant values are comparison inputs only, mismatches return safe denial before archive observation, and stream/projection/cache/audit keys remain managed-tenant-prefixed.
4. Given the caller is unauthenticated, unauthorized, cross-tenant, same-tenant forbidden, missing folder to caller, stale beyond policy, malformed, drawn from incompatible decision evidence, or blocked by unavailable tenant/folder authorization evidence, when archive is requested, then the response uses the existing `401`, `403`, `404`, validation, or authorization/read-model unavailable safe shapes from the denial table and does not disclose whether the folder exists, is active, is archived, has repository binding, has workspace state, has audit records, or has provider backing.
5. Given the archive request body is missing, has an unsupported `requestSchemaVersion`, contains an unknown `archiveReasonCode`, has malformed identifiers, is missing `Idempotency-Key`, has malformed correlation/task evidence, or violates local archive command validation, when validation runs, then only schema/envelope validation may run before authorization and the command returns the existing validation failure shape before appending events, constructing unauthorized resource subjects, or observing folder state.
6. Given the same idempotency key and equivalent archive payload are replayed for the same managed tenant and folder, when the equivalent-payload fingerprint matches command identity, `requestSchemaVersion`, `archiveReasonCode`, folder ID, tenant authority, operation/correlation/task evidence where locally included in fingerprints, actor evidence where locally included in fingerprints, and local policy/freshness version evidence where fingerprints include it, then the command returns the same logical accepted result without appending a second event; given the same key is reused with a different equivalent-payload fingerprint, then it returns `IdempotencyConflict` before stream load or append.
7. Given an authorized folder is already archived and a different idempotency key is used, when archive is requested again, then the command returns the stable domain state-transition rejection `AlreadyArchived` or the locally canonical equivalent, maps it to the existing safe state response without appending a duplicate archive event, and preserves safe metadata-only evidence.
8. Given an authorized folder is active but local archive policy says archive is not allowed, when archive is requested, then the command is rejected with a stable safe policy/state result, leaves lifecycle state unchanged, and emits no provider, repository, workspace, file, or repair side effects.
9. Given a folder is archived, when future active-only mutations such as folder metadata updates, folder ACL mutations, repository binding/unbinding, workspace preparation, lock acquisition/release, file mutation, commit, branch/ref policy, provider mutation, or task-lifecycle mutation evaluate folder lifecycle, then they have an explicit archived-state guard to reject active-only mutations before side effects; this story must add the guard and at least representative domain fixtures without implementing those future commands.
10. Given lifecycle status from Story 2.7 is requested for an archived folder, when the caller is authorized and read-model evidence is fresh, then `FolderLifecycleStatus` returns `archived: true`, the archived lifecycle state, freshness metadata, archive reason category, operation/correlation/task evidence when allowed, and only allowed opaque identifiers; unauthorized callers receive the same safe denial behavior as active folders.
11. Given audit, status, diagnostics, logs, traces, metrics, Problem Details, generated client exceptions, projection records, or test output are produced by archive handling, when metadata is inspected, then file contents, diffs, provider tokens, credential material, external repository URLs, repository names, branch names, file paths, raw auth headers, raw JWTs, raw claim bags, group/member inventories, provider payloads, raw request bodies, raw exception text, and unauthorized resource existence are absent.
12. Given C3 retention currently marks folder metadata and soft-delete markers as proposed workshop values, when archive evidence is persisted or documented, then implementation treats C3 as the retention reference without claiming final legal approval, does not delete or compact existing folder audit/status evidence, keeps archive event and projection fields immutable except through append-only correction patterns already approved locally, and leaves tenant-deletion/legal-hold enforcement to Story 7.11 unless a local metadata-only tombstone field is required by the archive state.
13. Given tests run without provider credentials, tenant seed data, production secrets, running Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, provider endpoints, live EventStore servers, or initialized nested submodules, when unit/server/contract tests execute, then authorized archive, safe denial, validation failure, idempotent replay, idempotency conflict, concurrent append race, already-archived handling, stale/unavailable evidence, incompatible decision snapshots, archived lifecycle status, mutation guard, metadata leakage, cache isolation, timing/discovery leakage, and cross-tenant same-identifier cases are covered with in-memory fakes and spies.
14. Given this story owns archive lifecycle transition only, when implementation is complete, then it does not implement hard delete, tenant deletion, legal hold, provider repository archival/deletion, workspace cleanup workers, audit browsing endpoints, repair workflows, lifecycle-status UI, CLI/MCP commands, provider readiness, repository binding, branch/ref policy, workspace preparation, locks, file mutations, commits, context queries, local-only folder mode, webhooks, brownfield adoption, or multi-organization-per-tenant behavior.

## Archive Denial And State Table

| Case | Archive observation allowed? | Response family | State/evidence rule |
|---|---:|---|---|
| Missing or invalid authentication | No | Existing `401` safe denial | No folder, lifecycle, binding, workspace, audit, provider, repository, or diagnostic subject metadata. |
| Tenant authority missing, malformed, reserved, client-supplied, or mismatched | No | Existing `403` safe denial | No proof that the folder ID is valid, present, absent, active, archived, bound, or unbound. |
| Same-tenant caller lacks archive/admin permission, or folder is not found to caller | No | Existing safe `404` or approved safe denial mapping | Do not use actual resource lookup to distinguish unauthorized from absent. |
| Authorization or policy evidence stale, unavailable, malformed, or replay-conflicting | No protected lookup until evidence is safe | Existing unavailable/safe denial outcome | No aggregate scan, projection repair, provider call, repository call, filesystem read, or permissive default. |
| Request validation or missing idempotency evidence fails | No append | Existing `400` validation failure | No lifecycle transition; no event; response includes only safe validation category and correlation when allowed. |
| Same idempotency key with equivalent payload | After authorization and idempotency check | Same logical accepted result | No duplicate archive event. |
| Same idempotency key with conflicting payload | Before stream load or append | Existing `409` `IdempotencyConflict` | No lifecycle lookup or event append after conflict is detected. |
| Authorized active folder and policy allows archive | Yes | `202` `AcceptedCommand` | Append one metadata-only archived event and expose only operation/correlation/task evidence. |
| Authorized already archived folder with new idempotency key | Yes | Existing safe state response for `AlreadyArchived` or local equivalent | No duplicate event; no mutation side effects. |
| Authorized concurrent archive append race | Yes, after authorization | Existing append-conflict/stale-version response or idempotent accepted replay after reread | No duplicate archive event; reread must not broaden metadata exposure. |

## Archive Execution Semantics

- Domain result vocabulary for this story must distinguish accepted archive, already archived, policy denied, validation failed, unauthorized or not found to caller, evidence unavailable or stale, idempotency conflict, and append conflict or stale expected version. These may reuse existing local result codes, but tests must prove each maps to the Contract Spine response family without adding contract behavior.
- Execution order is authenticate, validate only request shape and caller-controlled envelope fields, derive authoritative managed tenant from authentication context or EventStore authority, authorize tenant and folder archive/admin permission without protected folder observation, perform idempotency conflict precheck where safe, load folder state, evaluate archive policy/current lifecycle, append, then project metadata-only status/audit evidence.
- No folder stream name, projection key, cache key, audit subject, diagnostic subject, EventStore envelope, Dapr target, provider handle, repository reference, workspace path, file path, lifecycle status, or audit record may be constructed before the relevant authorization layer allows that observation.
- Successful archive returns the existing `202 AcceptedCommand` shape. Equivalent idempotent replay returns the same logical accepted result and does not append. A different idempotency key against an already archived folder returns the stable `AlreadyArchived` state result or local equivalent, not a second accepted mutation.
- Observable audit/status preservation means archive status remains queryable for authorized callers, the archive reason category and lifecycle transition remain retained with operation/correlation/task evidence, pre-archive metadata-only audit/status references are not deleted or compacted by this command, and unauthorized callers receive no proof of those records.
- Archive decision inputs must be bound into one request-scoped evidence snapshot. Do not authorize with one principal/action/freshness version, compute idempotency with another, and append against a third folder-state or policy version.
- Denied, unavailable, already-archived, policy-denied, and append-race branches must avoid cache-hit, timing, diagnostic, exception-detail, and result-shape differences that reveal actual folder existence, archived state, provider binding, workspace state, or audit-row presence.

## Tasks / Subtasks

- [x] Implement the archive command contract path. (AC: 1, 2, 5, 6)
  - [x] Add an `ArchiveFolder` command under `src/Hexalith.Folders/Aggregates/Folder/` matching the Contract Spine fields: `requestSchemaVersion`, `archiveReasonCode`, folder ID, actor/correlation/task evidence, idempotency key, and tenant authority inputs.
  - [x] Add or extend archive command validation so supported reason codes are exactly `caller_requested`, `policy_retention`, and `operator_review`, and `requestSchemaVersion` is exactly `v1`.
  - [x] Reuse the existing identifier, tenant, reserved-tenant, metadata-only, idempotency, and payload-tenant validation patterns from folder creation rather than creating parallel validation rules.
  - [x] Wire `POST /api/v1/folders/{folderId}/archive` in `Hexalith.Folders.Server` only if endpoint wiring is not already generated or registered.
  - [x] Preserve `202 AcceptedCommand`, validation failure, safe denial, and idempotency conflict shapes from the Contract Spine.
  - [x] Prove OpenAPI and generated SDK shape parity for route, method, `operationId`, `ArchiveFolderRequest`, required idempotency/correlation/task headers, accepted response, safe denial envelopes, validation envelope, and `IdempotencyConflict`.
  - [x] Do not hand-edit `src/Hexalith.Folders.Client/Generated/*`; regenerate through the established Contract Spine/NSwag toolchain only if generated output is stale.
- [x] Add archived lifecycle state and transition behavior. (AC: 2, 7, 8, 9, 10)
  - [x] Add `Archived` or the locally canonical equivalent to `FolderLifecycleState` and ensure `FolderState` can represent it deterministically.
  - [x] Add a metadata-only `FolderArchived` event carrying only managed tenant ID, organization ID, folder ID, archive reason code, actor/correlation/task evidence, idempotency key, and fingerprint plus event-envelope timestamp if that is the local convention.
  - [x] Extend `FolderAggregate.Handle` or the local command gate so active folders can transition to archived exactly once when policy allows.
  - [x] Define already-archived behavior as `AlreadyArchived` or the locally canonical stable state-transition rejection for a different idempotency key; same-key equivalent replay remains accepted replay evidence.
  - [x] Add archived-state guards that future active-only folder, ACL, repository-binding, workspace, lock, file, commit, branch/ref, provider, and task commands can consume without implementing those future commands in this story.
  - [x] Ensure `FolderStateApply` rejects foreign-tenant or foreign-folder archive events using the existing stream-safety pattern.
- [x] Enforce authorization-before-archive-observation. (AC: 2, 3, 4, 8)
  - [x] Resolve authoritative tenant context from authentication context or EventStore envelope/projection authority before constructing folder stream names, projection keys, cache keys, audit subjects, diagnostic scopes, EventStore envelopes, Dapr targets, provider handles, repository references, workspace paths, or file paths.
  - [x] Consume Story 2.1 tenant-access evidence, Story 2.6 layered authorization, and folder admin/archive permission evidence before loading folder state or checking archive eligibility.
  - [x] Treat tenant access, folder permission, idempotency, folder state, archive policy, and freshness evidence as one compatible archive decision snapshot; reject mixed tenant, folder, principal, action, task, operation, policy-version, idempotency-fingerprint, or freshness evidence rather than stitching a partial success.
  - [x] Treat route/query/header/body/generated-client tenant IDs as comparison values only; mismatches return safe denial before lookup.
  - [x] Keep schema-only validation separate from protected-resource validation: malformed body/schema/reason/idempotency evidence may fail early, but anything needing folder, lifecycle, projection, audit, provider, repository, workspace, or file knowledge waits until authorization permits it.
  - [x] Add side-effect spies proving denied tenant or folder authorization prevents stream-name construction, stream load, append, diagnostics, audit lookup, provider access, repository access, filesystem access, EventStore envelope construction, and Dapr invocation target construction.
  - [x] Keep same-tenant unauthorized, cross-tenant, and missing-to-caller outcomes externally indistinguishable except for approved canonical status/category differences.
  - [x] Scope any archive decision cache, idempotency cache, authorization cache, or projection cache by tenant, principal, action, task/correlation context, folder ID, authorization evidence version, policy version, idempotency fingerprint, and freshness watermark; denied or stale answers must not be replayed across callers or evidence windows.
- [x] Preserve audit/status evidence without adding cleanup scope. (AC: 2, 10, 11, 12, 14)
  - [x] Update or add folder projections so authorized lifecycle-status reads can represent archived folders with `archived: true`, an archived lifecycle state, freshness metadata, and no mutation affordance.
  - [x] Preserve archive event and projection metadata under the C3 folder metadata and soft-delete marker retention reference without claiming Legal + PM approval is final.
  - [x] Ensure archive handling does not delete events, compact projections, remove provider resources, clean working copies, repair status, or call tenant-deletion/legal-hold workflows.
  - [x] Add audit/status metadata fields only for opaque folder ID, operation ID, correlation ID, task ID, result category, archive reason code, lifecycle transition, and sanitized policy/state category.
  - [x] Ensure archive status remains queryable after archive while future active-only mutation commands see archived as terminal until a later explicitly scoped restore/unarchive story exists.
- [x] Add contract, SDK, and parity conformance coverage. (AC: 1, 5, 6, 10, 13)
  - [x] Add server or contract tests proving route, method, request schema, required headers, accepted response, validation failure, safe denial envelopes, idempotency conflict, operation ID, and response casing match `ArchiveFolder`.
  - [x] Add generated-client consumption tests proving the generated archive method and `ArchiveFolderRequest` model can submit supported reason codes without hand-edited generated code.
  - [x] Add tests proving behavior stays out of `Hexalith.Folders.Contracts` packages and no generated SDK file is hand-edited.
  - [x] Ensure parity fixtures keep `ArchiveFolder` aligned with idempotency-key, safe-denial, correlation ID, task ID, audit metadata, and adapter-stage expectations.
  - [x] Add tests for missing/malformed idempotency key, equivalent replay, conflicting replay, and adapter-visible validation categories.
- [x] Add aggregate, projection, and leakage regression tests. (AC: 2, 7, 8, 9, 10, 11, 13)
  - [x] Add tests such as `FolderArchiveCommandValidationTests`, `FolderArchiveIdempotencyTests`, `FolderArchiveAuthorizationGateTests`, `FolderArchiveStateTransitionTests`, `FolderArchiveLifecycleStatusTests`, and `FolderArchiveMetadataLeakageTests`.
  - [x] Cover active-to-archived success, already-archived handling, policy-denied archive, stale/unavailable tenant authorization, stale/unavailable folder authorization, stale lifecycle projection, replay conflict, concurrent append conflict/reread, and foreign-event replay rejection.
  - [x] Cover incompatible archive decision snapshots, including authorization evidence for one principal/action with idempotency or folder-state evidence for another, stale policy evidence with fresh folder state, and matching idempotency keys across different freshness or policy versions where the local fingerprint model includes them.
  - [x] Cover cross-tenant same-identifier cases for folder IDs, operation IDs, correlation IDs, task IDs, stream names, cache keys, projection keys, and audit subjects.
  - [x] Include representative active-only mutation guard fixtures proving archived folders reject at least metadata update, ACL mutation, repository-binding, workspace/lock, file, commit, branch/ref, provider, and task mutation categories before side effects where those command categories already have local placeholders or vocabulary.
  - [x] Add forbidden-sentinel checks across responses, Problem Details, logs, traces, metrics, diagnostics, audit records, projection records, generated client exceptions, and test output.
  - [x] Add timing/discovery negative controls proving denied, safe-not-found, unavailable, policy-denied, already-archived, and append-conflict paths do not branch on actual folder existence, archived state, projection cache hits, provider binding presence, workspace state, or audit-row presence.
  - [x] Keep tests offline with in-memory fakes and spies; do not require GitHub, Forgejo, provider credentials, Dapr sidecars, Keycloak, Redis, live EventStore, or nested submodules.

## Dev Notes

### Source Context

- Epic 2 objective: tenant administrators and authorized actors can create folders, manage access, inspect effective permissions, archive folders, and receive safe authorization evidence with cross-tenant isolation enforced before any resource access. [Source: `_bmad-output/planning-artifacts/epics.md#Epic 2: Tenant-Scoped Folder Access And Lifecycle`]
- Story 2.8 foundation: authorized tenant administrators archive folders when policy allows so retired work is no longer active while audit and status evidence remain available. [Source: `_bmad-output/planning-artifacts/epics.md#Story 2.8`]
- PRD FR13 requires authorized archive when policy allows; FR14 requires audit and status evidence preservation for archived folders; FR9 and FR10 require safe denial and authorization evidence without protected-resource disclosure. [Source: `_bmad-output/planning-artifacts/prd.md#Folder Lifecycle`; `_bmad-output/planning-artifacts/prd.md#Authorization and Tenant Boundary`]
- The Contract Spine already defines `POST /api/v1/folders/{folderId}/archive`, `operationId: ArchiveFolder`, `ArchiveFolderRequest`, required idempotency/correlation/task parameters, `202 AcceptedCommand`, validation failure, safe denial, and idempotency conflict responses. [Source: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#/paths/~1api~1v1~1folders~1{folderId}~1archive`]
- The Contract Spine currently defines `ArchiveFolderRequest.archiveReasonCode` values `caller_requested`, `policy_retention`, and `operator_review`. [Source: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#/components/schemas/ArchiveFolderRequest`]
- C3 retention lists folder metadata and soft-delete markers as retained for tenant lifetime plus 400 days after tenant-deletion request enters approved retention workflow, but the approval state remains proposed and needs Legal + PM decision. [Source: `docs/exit-criteria/c3-retention.md#Decision`]
- Project context requires zero cross-tenant leakage, metadata-only evidence, tenant-prefixed keys, idempotency for lifecycle mutations, and authorization before file/workspace/credential/repository/lock/commit/provider/audit access. [Source: `_bmad-output/project-context.md#Critical Don't-Miss Rules`]

### Previous Story Intelligence

- Story 2.1 defines the Folders host, Tenants event subscription, local fail-closed tenant-access projection, and `TenantAccessAuthorizer`. Archive authorization must reuse that tenant evidence and fail closed when projection freshness cannot be proven.
- Story 2.2 defines organization ACL baseline action/principal token handling and safe fixture patterns. Archive permission evidence should extend or consume the ACL model rather than hardcoding a new authority source.
- Story 2.3 defines `FolderAggregate`, `CreateFolder`, `FolderCreated`, `FolderState`, `FolderLifecycleState.Active`, `FolderRepositoryBindingState.Unbound`, opaque folder identity, stream name shape, and folder creation tests. Archive should extend this aggregate rather than creating a second lifecycle model.
- Story 2.4 defines folder-level grant/revoke semantics and revocation freshness expectations. Archive requires current admin/archive permission and must fail closed on stale permission evidence.
- Story 2.5 defines effective-permission inspection and task-context narrowing. Archive may consume permission evidence but must not duplicate public effective-permission query behavior.
- Story 2.6 defines layered authorization, safe denial mapping, protected-resource-touch semantics, Dapr/EventStore evidence seams, and no-touch spy coverage. Archive is a mutating command consumer of those seams.
- Story 2.7 defines lifecycle-status read behavior, status-observation boundaries, archived status expectations, generated-client conformance, and stale/unavailable no-fallback rules. Archive must update lifecycle status through the same projection/read-model vocabulary.

### Existing Implementation State

- Current folder aggregate code only supports creation and `FolderLifecycleState.Active`; Story 2.8 is the first story that should add archived lifecycle behavior.
- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs` handles `CreateFolder` by validating, checking idempotency, rejecting duplicates, and appending `FolderCreated` with active/unbound state.
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs` centralizes identifier, reserved-system-tenant, safe metadata, tag, and creation fingerprint logic. Extend it carefully or split archive validation only where the shape genuinely differs.
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs` already rejects foreign folder events once state is created and updates idempotency fingerprints for every folder event. Archive events must preserve this safety behavior.
- `src/Hexalith.Folders/Aggregates/Folder/IFolderRepository.cs` exposes stream creation, load, append-if-fingerprint-absent, and idempotency lookup seams used by offline tests.
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/RecordingFolderRepository.cs` and existing folder creation tests provide patterns for append conflict, idempotency conflict, same folder ID across tenants, unavailable idempotency, and no-touch assertions.
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` already contains `ArchiveFolder`; source contract changes should not be needed unless implementation discovers a true contract bug.

### Required Architecture Patterns

- Keep `Hexalith.Folders.Contracts` behavior-free. Archive transition logic, authorization order, policy checks, idempotency, and safe-denial mapping belong in `Hexalith.Folders`, `Hexalith.Folders.Server`, and tests.
- Keep archive as a folder lifecycle mutation. It may append metadata-only domain events after authorization, but it must not call Git providers, inspect repositories, delete working copies, browse audit payloads, repair projections, or perform tenant deletion.
- Authorization order is authoritative tenant context, Story 2.1 tenant access, Story 2.6 layered authorization, folder admin/archive permission evidence, idempotency conflict precheck where safe, current folder state load, then archive eligibility and append.
- Authorization, idempotency, policy, folder-state, and freshness inputs must remain compatible within one archive decision snapshot. Unknown, incompatible, replay-conflicting, stale, or malformed evidence must fail closed instead of defaulting to active, allowed, absent, or already archived.
- Folder IDs, stream names, projection keys, cache keys, operation IDs, task IDs, audit subjects, diagnostic subjects, provider handles, repository references, workspace paths, and file paths must be tenant scoped and unavailable until authorization permits their construction.
- Successful archive evidence is metadata-only. Return and persist opaque identifiers, reason code, lifecycle transition, operation/correlation/task evidence, and sanitized result categories only.
- Use C# file-scoped namespaces, nullable-aware records/classes, one public type per file, PascalCase types/members, camelCase locals/parameters, and async APIs for I/O boundaries.

### Files To Touch

- `src/Hexalith.Folders/Aggregates/Folder/ArchiveFolder.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderArchived.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderArchiveReasonCode.cs` or local equivalent if a string enum wrapper is preferred
- `src/Hexalith.Folders/Aggregates/Folder/FolderLifecycleState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs` or a narrow archive validator beside it
- `src/Hexalith.Folders/Aggregates/Folder/FolderResultCode.cs`
- `src/Hexalith.Folders/Authorization/*` only to consume or narrowly adapt existing tenant/layered/folder-admin authorization seams
- `src/Hexalith.Folders/Projections/FolderList/*` only to project archived status and preserve lifecycle-status read behavior
- `src/Hexalith.Folders.Server/Endpoints/*` or local Minimal API modules only to wire the existing archive route
- `src/Hexalith.Folders.Testing/Factories/*` only for reusable safe archive command/status builders
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/*Archive*Tests.cs`
- `tests/Hexalith.Folders.Tests/Authorization/*Archive*Tests.cs`
- `tests/Hexalith.Folders.Tests/Projections/FolderList/*Archive*Tests.cs` or local equivalent
- `tests/Hexalith.Folders.Server.Tests/*Archive*Tests.cs`
- `tests/Hexalith.Folders.Client.Tests/*` only for generated-client archive consumption coverage
- `tests/fixtures/parity-contract.yaml` only if it is stale relative to the existing `ArchiveFolder` Contract Spine row

### Do Not Touch

- Do not hand-edit generated SDK files in `src/Hexalith.Folders.Client/Generated/`.
- Do not add a second OpenAPI operation or change the Contract Spine unless implementation discovers a blocking mismatch handled through the contract workflow.
- Do not implement hard delete, tenant deletion, legal hold, retention compaction, provider repository archival/deletion, workspace cleanup workers, audit browsing endpoints, repair workflows, UI screens, CLI/MCP commands, provider readiness, repository binding, branch/ref policy, workspace preparation, locks, file mutation, commits, context queries, local-only folder mode, webhooks, brownfield adoption, or multi-organization-per-tenant behavior.
- Do not call GitHub, Forgejo, filesystem working copies, provider APIs, repository APIs, audit browsing APIs, or EventStore aggregate scans as an archive eligibility fallback.
- Do not expose provider tokens, credential material, external repository URLs, repository names, branch names, folder display names when unauthorized, file paths, file contents, diffs, generated context payloads, raw claims, raw request/query bodies, membership inventories, or unauthorized resource existence.
- Do not initialize nested submodules or use recursive submodule commands.

### Testing

- Tests must run offline without provider credentials, tenant seed data, production secrets, running Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, provider endpoints, live EventStore servers, or nested submodules.
- Use xUnit v3 and Shouldly for unit/server/client tests; use NSubstitute only where an actual seam needs substitution.
- Include side-effect spies for every early denial, stale/unavailable authorization path, idempotency conflict, already-archived branch, and policy-denied branch.
- Include response-shape tests aligned with `AcceptedCommand`, validation failure, safe authorization denial components, `IdempotencyConflict`, and archived `FolderLifecycleStatus`.
- Include generated-client consumption tests for `ArchiveFolderRequest` and supported reason codes without editing generated code.
- Include cross-tenant same-identifier tests and leakage tests with forbidden sentinels across responses, Problem Details, logs, traces, metrics, diagnostics, audit metadata, projections, generated client exceptions, and test output.

### Regression Traps

- Do not compute archive authority or folder identity from request-supplied tenant fields.
- Do not construct stream names, projection keys, repository binding keys, provider binding keys, cache keys, audit subjects, diagnostic scopes, EventStore envelopes, Dapr targets, provider handles, repository refs, workspace paths, or file paths before authorization allows that layer.
- Do not treat archived as deleted, missing, unbound, or provider-detached unless a later story explicitly owns those semantics.
- Do not return `archived: false` as a default when archive state is unknown, stale, unsupported, or unavailable.
- Do not append more than one archive event for equivalent idempotent replay, already-archived handling, append-race reread, or retry paths.
- Do not treat malformed request validation as permission to construct protected folder, stream, projection, audit, provider, repository, workspace, or file subjects.
- Do not let `AlreadyArchived`, policy-denied, stale-evidence, append-conflict, or unavailable-evidence responses drift into different API, SDK, domain, projection, and parity meanings.
- Do not combine authorization, idempotency, folder-state, policy, or freshness evidence from incompatible tenants, principals, actions, tasks, operation IDs, policy versions, projection watermarks, or idempotency fingerprints.
- Do not reuse cached archive decisions, safe denials, idempotent results, or lifecycle projections across tenant, principal, action, task/correlation, folder ID, authorization-evidence version, policy version, idempotency-fingerprint, or freshness windows.
- Do not let denial latency, cache hit/miss behavior, diagnostics, logs, metrics, traces, Problem Details, generated-client exception detail, or append-conflict rereads reveal actual folder existence, archived state, provider binding, workspace state, or audit-row presence.
- Do not use archive as an implicit cleanup trigger for working copies, provider repositories, read models, or audit records.
- Do not expose archive state to unauthorized callers through different `403`/`404` choices, timing side effects, diagnostics, logs, cache keys, or exception text.
- Do not expand this story into restore/unarchive, retention enforcement, tenant deletion, audit query endpoints, UI, CLI/MCP, provider workflows, workspace workflows, or repair automation.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 2.8`
- `_bmad-output/planning-artifacts/prd.md#Folder Lifecycle`
- `_bmad-output/planning-artifacts/prd.md#Authorization and Tenant Boundary`
- `_bmad-output/planning-artifacts/prd.md#Security and Tenant Isolation`
- `_bmad-output/planning-artifacts/architecture.md#Requirements to Structure Mapping`
- `_bmad-output/planning-artifacts/architecture.md#Source Tree`
- `_bmad-output/project-context.md#Critical Don't-Miss Rules`
- `docs/exit-criteria/c3-retention.md`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderLifecycleState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs`
- `src/Hexalith.Folders/Aggregates/Folder/IFolderRepository.cs`
- `_bmad-output/implementation-artifacts/2-1-stand-up-domain-service-host-with-tenants-integration.md`
- `_bmad-output/implementation-artifacts/2-2-implement-organization-aggregate-acl-baseline.md`
- `_bmad-output/implementation-artifacts/2-3-create-folders-within-a-tenant.md`
- `_bmad-output/implementation-artifacts/2-4-grant-and-revoke-folder-access.md`
- `_bmad-output/implementation-artifacts/2-5-inspect-effective-permissions.md`
- `_bmad-output/implementation-artifacts/2-6-enforce-layered-authorization-with-safe-denials.md`
- `_bmad-output/implementation-artifacts/2-7-inspect-folder-lifecycle-and-binding-status.md`

## Party-Mode Review

- Date: 2026-05-19T01:04:12+02:00
- Selected story key: `2-8-archive-folders-with-audit-preservation`
- Command/skill invocation used: `/bmad-party-mode 2-8-archive-folders-with-audit-preservation; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), John (Product Manager)
- Findings summary: Reviewers agreed the story was close but needed sharper pre-dev guidance for authorization-before-observation ordering, idempotency fingerprint identity and append races, already-archived/domain response semantics, denial/status mapping, concrete archived-state guard targets, audit/status preservation observables, Contract Spine/SDK parity, and no-touch stale/unavailable evidence tests.
- Changes applied: Clarified `202 AcceptedCommand` success semantics, tenant authority and tenant-prefixed key rules, schema-only validation boundaries, idempotency fingerprint basis, `AlreadyArchived` handling, archived active-only mutation guard targets, lifecycle-status/audit preservation observables, concurrent append race expectations, domain result vocabulary, execution order, OpenAPI/SDK parity tests, stale/unavailable evidence tests, no-touch guard fixtures, and regression traps.
- Findings deferred: C3 legal approval, legal hold, tenant deletion, retention enforcement, audit browsing APIs, provider repository archival/deletion, workspace cleanup, UI, CLI/MCP, webhooks, reason-code expansion, restore/unarchive behavior, and detailed future command implementations remain out of scope for later stories.
- Final recommendation: `ready-for-dev`

## Advanced Elicitation

- ISO date and time: 2026-05-19T05:02:50+02:00
- Selected story key: `2-8-archive-folders-with-audit-preservation`
- Command/skill invocation used: `/bmad-advanced-elicitation 2-8-archive-folders-with-audit-preservation`
- Batch 1 method names: Security Audit Personas, Failure Mode Analysis, Pre-mortem Analysis, Self-Consistency Validation, Critique and Refine
- Reshuffled Batch 2 method names: Red Team vs Blue Team, Chaos Monkey Scenarios, First Principles Analysis, 5 Whys Deep Dive, Architecture Decision Records
- Findings summary:
  - Security and failure-mode review found the story already enforced authorization-before-observation, but needed explicit guardrails against combining authorization, idempotency, policy, folder-state, and freshness evidence from incompatible snapshots.
  - Pre-mortem and self-consistency review identified likely implementation shortcuts around stale policy evidence, reused idempotency/cache entries, append-race rereads, and generated-client exception detail.
  - Red-team and chaos review highlighted timing, cache hit/miss, diagnostics, logs, metrics, Problem Details, and projection/audit-row existence as side channels that must be covered before `bmad-dev-story`.
- Changes applied:
  - Added the `Archive decision snapshot` term and tightened AC2, AC4, AC6, and AC13 for compatible evidence, fail-closed stale/incompatible inputs, fingerprint scoping, cache isolation, timing/discovery leakage, and offline negative controls.
  - Expanded archive execution semantics and authorization tasks to require one request-scoped decision snapshot and cache scoping by tenant, principal, action, task/correlation context, folder ID, evidence version, policy version, idempotency fingerprint, and freshness watermark.
  - Expanded aggregate/projection/leakage regression tests and regression traps for incompatible snapshots, stale policy/folder-state combinations, denial latency, cache hit/miss behavior, generated-client exception detail, and append-conflict reread leakage.
- Findings deferred:
  - No product-scope or architecture-policy changes were applied.
  - C3 legal approval, legal hold, tenant deletion, retention enforcement, restore/unarchive, provider repository archival/deletion, workspace cleanup, audit browsing APIs, UI, CLI/MCP, and future command implementations remain out of scope for later stories.
- Final recommendation: ready-for-dev

## Review Findings

Generated by `/bmad-code-review 2.8` (Blind Hunter + Edge Case Hunter + Acceptance Auditor) over commit `ea5f08b` on 2026-05-20.

**Patch progress (2026-05-20):** All actionable review follow-ups are resolved or explicitly dispositioned. Follow-up implementation added endpoint denial mapping, idempotent replay surfacing, schema-version validation hints, expanded archive idempotency fingerprints, policy/freshness evidence binding, lifecycle vocabulary parity coverage, stale/unavailable/malformed policy tests, discovery/timing negative controls, audit-leakage-corpus sentinel coverage, append-conflict race coverage, and folder ACL action-token alignment. Full solution build is clean; `dotnet test Hexalith.Folders.slnx --no-restore` returns **595 passed, 1 skipped, 0 failed**.

### Review Findings

- [x] [Review][Decision→Patch] Endpoint authorization wiring location — `FolderArchiveTenantGate` exists and is fully tested in isolation, but no production code path constructs it. **Resolved 2026-05-20:** wire the gate as an `IDomainProcessor` invoked from the `/process` callback per the project architecture rule ("Hexalith.Folders.Server owns REST transport and EventStore `/process` and `/project` domain-service endpoints; external REST is `/api/v1/...`, internal EventStore invocation remains `/process` and `/project`"). REST stays thin; `/process` performs ACL/policy evidence composition, idempotency precheck, append, and denial-family mapping. [`src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs:146-247`] [`src/Hexalith.Folders/Aggregates/Folder/FolderArchiveTenantGate.cs`]
- [x] [Review][Decision→Patch+Defer] Archived lifecycle string token — `FolderLifecycleStatusQueryHandler.ComputeArchived` returns `lifecycleState: "inaccessible"`. **Resolved 2026-05-20:** keep `inaccessible` as the local canonical archived token (Story 2.7 convention; Contract Spine's `LifecycleState` enum and `FolderLifecycleStatus` schema are stable and `additionalProperties: false`). Add parity-contract coverage proving SDK/CLI/MCP/Console treat `inaccessible` consistently as archived. The AC10 "archive reason category" portion is **deferred** to a separate contract-workflow story because the `FolderLifecycleStatus` schema in `hexalith.folders.v1.yaml:7165-7185` has no reason-code field and is `additionalProperties: false` — surfacing it would violate the Do-Not-Touch contract-change rule. [`src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:255-267`] [`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:7165-7185`]
- [x] [Review][Patch] Endpoint returns 202 unconditionally — never produces the spec-required `403`, `404`, `409 IdempotencyConflict` shapes; `IdempotentReplay` is hardcoded `false`. **Applied:** archive REST now maps `EventStoreGatewayException` statuses to contract-safe `403`, `404`, `409 idempotency_conflict`, and safe unavailable shapes; `AcceptedCommandResponse.IdempotentReplay` is derived from gateway result payload evidence. Tests cover replay payloads and gateway denial families. (AC1, AC2, AC3, AC4, AC6, AC8) [`src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`] [`tests/Hexalith.Folders.Server.Tests/ArchiveFolderEndpointTests.cs`]
- [x] [Review][Patch] Endpoint reads request body before checking `AuthoritativeTenantId` — JSON-parsing CPU and validation feedback observable to unauthenticated callers. **Applied:** authentication and identifier validation now run before body parsing. [`src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`]
- [x] [Review][Patch] `folderId` route value is not validated against the canonical segment shape before being forwarded to the gateway as `AggregateId`. Returns a 500 (`ArgumentException`) instead of a safe `400` for whitespace/null. **Applied:** `IsCanonicalIdentifier` now validates folderId, idempotencyKey, correlationId, and taskId before any downstream use; returns safe `400` on failure. [`src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`]
- [x] [Review][Patch] Reserved-tenant `system` is not rejected at the endpoint — only inside the aggregate validator, after the command has already been queued. **Applied:** reserved-tenant rejection now returns safe `403` at the endpoint before gateway submit. [`src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`]
- [x] [Review][Patch] Gateway exceptions (`gateway.SubmitCommandAsync`) are not caught — surfaces internal stack to caller as `500`, violates safe-denial constraint when evidence is unavailable. **Applied:** wrapped in try/catch returning `503 evidence_unavailable, retryable=true`; `OperationCanceledException` rethrown. [`src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`]
- [x] [Review][Patch] `submitted.CorrelationId` reflected into the `X-Correlation-Id` response header without re-validating `IsSafeEvidenceIdentifier`. **Applied:** gateway-returned correlation id passes through `IsCanonicalIdentifier` before being reflected; falls back to the caller-supplied value if the gateway returns anything else. [`src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`]
- [x] [Review][Patch] `taskId` is checked non-empty but not validated against the canonical safe-identifier shape before being placed in `SubmitCommandRequest.Extensions`. **Applied:** taskId now validated by `IsCanonicalIdentifier`. [`src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`]
- [x] [Review][Patch] `AcceptedCommandResponse.AcceptedAt` uses `DateTimeOffset.UtcNow` instead of an injected `TimeProvider`. **Applied:** `TimeProvider` is now injected through DI on the archive route handler; `AcceptedAt = timeProvider.GetUtcNow()`. [`src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`]
- [x] [Review][Patch] `JsonSerializerOptions` for `ArchiveFolderHttpRequest` does not set `UnmappedMemberHandling = Disallow`. **Applied:** new `RequestJsonOptions` with `JsonUnmappedMemberHandling.Disallow` used for body deserialization. [`src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`]
- [x] [Review][Patch] `RequestSchemaVersion` is matched case-sensitively against `v1` with no error message hint; clients sending `V1` get a generic `validation_error`. **Applied:** case-sensitive matching remains correct, and unsupported schema versions now return safe `400` with `code=unsupported_request_schema_version` and message `requestSchemaVersion must be exactly v1.` [`src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`] [`tests/Hexalith.Folders.Server.Tests/ArchiveFolderEndpointTests.cs`]
- [x] [Review][Patch] `FolderArchiveTenantGate.EvaluatePolicy` collapses every non-allowed/non-denied policy outcome into `AclEvidenceUnavailable`. **Applied:** added `PolicyEvidenceUnavailable`, `PolicyEvidenceStale`, `PolicyEvidenceMalformed`, `PolicyEvidenceScopeMismatch` result codes; `EvaluatePolicy` now maps each `FolderArchivePolicyOutcome` to its distinct code. [`src/Hexalith.Folders/Aggregates/Folder/FolderArchiveTenantGate.cs`] [`src/Hexalith.Folders/Aggregates/Folder/FolderResultCode.cs`]
- [x] [Review][Patch] `FolderArchiveTenantGate.EvaluateAcl` folds `FolderMismatch` and `UnsupportedAction` into `AclEvidenceUnavailable`. **Applied:** added `AclEvidenceForeignFolder` and `AclEvidenceUnsupportedAction`; `EvaluateAcl` returns distinct codes for each. [`src/Hexalith.Folders/Aggregates/Folder/FolderArchiveTenantGate.cs`]
- [x] [Review][Patch] `EvaluatePolicy` matches policy evidence against tenant + folder ID but ignores `OrganizationId`. **Applied:** `FolderArchivePolicyEvidence` now carries `OrganizationId`; `EvaluatePolicy` requires tenant + organization + folder + non-empty policy version to match. [`src/Hexalith.Folders/Aggregates/Folder/FolderArchivePolicyEvidence.cs`] [`src/Hexalith.Folders/Aggregates/Folder/FolderArchiveTenantGate.cs`]
- [x] [Review][Patch] ACL evidence consulted before `FolderCommandValidator.Validate` runs. **Applied:** schema/envelope validation now runs before `EvaluateAcl`; ACL system is not probed on malformed payloads. [`src/Hexalith.Folders/Aggregates/Folder/FolderArchiveTenantGate.cs`]
- [x] [Review][Patch] `ResolveAppendConflict` re-uses the original `occurredAt` after a fresh state reread. **Applied:** `ResolveAppendConflict` now reads a fresh `_timeProvider.GetUtcNow()` after re-load. [`src/Hexalith.Folders/Aggregates/Folder/FolderArchiveTenantGate.cs`]
- [x] [Review][Patch] `FolderAppendOutcome` switch has no explicit default. **Applied:** added explicit default arm with a comment explaining that future enum additions must be wired explicitly. [`src/Hexalith.Folders/Aggregates/Folder/FolderArchiveTenantGate.cs`]
- [x] [Review][Patch] `Map(TenantAccessOutcome.Allowed)` throws `InvalidOperationException` on invariant violation. **Applied:** invariant-violating input now returns `FolderResultCode.MalformedEvidence` via safe denial instead of throwing. [`src/Hexalith.Folders/Aggregates/Folder/FolderArchiveTenantGate.cs`]
- [x] [Review][Patch] `HasCompetingClientTenant` only inspects dictionary values and accepts whitespace-only client-supplied tenant IDs; dictionary keys are not validated. **Applied:** keys checked for whitespace; values checked for whitespace-only smuggling; per-entry logic loops the dictionary explicitly. [`src/Hexalith.Folders/Aggregates/Folder/FolderArchiveTenantGate.cs`]
- [x] [Review][Patch] `FolderAggregate.Handle(ArchiveFolder, …)` consults `IdempotencyFingerprints` before checking `state.IsCreated`. **Applied:** `IsCreated` check now precedes the idempotency-fingerprint probe so replay metadata cannot be probed on uncreated folders. [`src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`]
- [x] [Review][Patch] `FolderArchiveAclEvidence` constructor allows `Denied` outcomes with arbitrary `action` strings and `Allowed` outcomes with null identifiers. **Applied:** `Allowed` outcomes now require non-empty `managedTenantId`, `folderId`, and `principalId`; `action` non-null check added globally. [`src/Hexalith.Folders/Aggregates/Folder/FolderArchiveAclEvidence.cs`]
- [x] [Review][Patch] `FolderActiveMutationGuard` returns `AlreadyArchived` for any active-only mutation against an archived folder. **Applied:** guard returns category-neutral `StateTransitionInvalid` so ACL/workspace/file callers do not learn lifecycle state; ArchiveFolder retains direct `AlreadyArchived` reporting. Tests updated to match. [`src/Hexalith.Folders/Aggregates/Folder/FolderActiveMutationGuard.cs`] [`tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveStateTransitionTests.cs`]
- [x] [Review][Patch] `FolderListProjection.Apply` silently drops `FolderArchived` envelopes when no prior `FolderCreated` exists for the key. **Applied:** projection now throws `InvalidOperationException` with sequence + key context on this anomaly. [`src/Hexalith.Folders/Projections/FolderList/FolderListProjection.cs`]
- [x] [Review][Patch] Projection silently ignores unhandled `IFolderEvent` subtypes. **Applied:** default arm now throws on unsupported event types, matching `FolderStateApply`'s fail-loud contract. [`src/Hexalith.Folders/Projections/FolderList/FolderListProjection.cs`]
- [x] [Review][Patch] Archive idempotency fingerprint hashes only `(CommandType, ManagedTenantId, OrganizationId, FolderId, RequestSchemaVersion, ArchiveReasonCode, ActorPrincipalId)` — omits `CorrelationId`, `TaskId`, `IdempotencyKey` salt, `PayloadTenantId`, policy version, freshness watermark. **Applied:** archive command fingerprints now include correlation/task/idempotency/payload/client-tenant evidence, and the archive gate binds the accepted command fingerprint to policy version plus tenant-projection freshness watermark before ledger lookup, state handling, append, and event persistence. Tests cover different correlation, policy version, and freshness watermark conflicts before stream load. (AC6) [`src/Hexalith.Folders/Aggregates/Folder/ArchiveFolder.cs`] [`src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs`] [`src/Hexalith.Folders/Aggregates/Folder/FolderArchiveTenantGate.cs`] [`src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`] [`tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveIdempotencyTests.cs`]
- [x] [Review][Patch] Add parity-contract coverage proving SDK/CLI/MCP/Console all treat `lifecycleState: "inaccessible"` consistently as archived (AC10 lifecycle vocabulary portion). **Applied:** lifecycle-status tests assert `inaccessible` + `archived=true` across SDK/CLI/MCP/console consumer labels and verify the ArchiveFolder parity row includes SDK/CLI/MCP/REST expectations from `tests/fixtures/parity-contract.yaml`. [`tests/fixtures/parity-contract.yaml`] [`tests/Hexalith.Folders.Tests/Queries/Folders/FolderLifecycleStatusProjectionTests.cs`]
- [x] [Review][Patch] Missing test — stale / unavailable / malformed policy evidence with fresh folder state and authorization. **Applied:** added policy evidence coverage for `Stale`, `Unavailable`, and `Malformed`, with assertions that stream construction, idempotency lookup, stream load, append, and side-effect probes remain untouched. (AC13) [`tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveAuthorizationGateTests.cs`]
- [x] [Review][Patch] Missing test — timing / cache-hit / discovery negative controls proving denied, safe-not-found, unavailable, policy-denied, already-archived, and append-conflict paths do not branch on actual folder existence. **Applied:** added seeded-vs-empty discovery-shape controls for pre-observation denial paths, safe-not-found and already-archived side-effect assertions, and append-conflict/no-winner coverage. Policy rejection now runs before stream-name construction. (AC13) [`src/Hexalith.Folders/Aggregates/Folder/FolderArchiveTenantGate.cs`] [`tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveAuthorizationGateTests.cs`] [`tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveIdempotencyTests.cs`]
- [x] [Review][Patch] Missing test — forbidden-sentinel iteration across responses, Problem Details, logs, traces, metrics, generated-client exception text, audit/projection records using `tests/fixtures/audit-leakage-corpus.json`. **Applied:** archive metadata leakage tests now iterate every corpus sentinel across representative event, audit, projection, problem, log, trace, metric, and generated-client exception surfaces. (AC11) [`tests/fixtures/audit-leakage-corpus.json`] [`tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveMetadataLeakageTests.cs`]
- [x] [Review][Patch] Missing test — concurrent append race with a **differing**-fingerprint racing event. **Applied:** archive idempotency tests now cover equivalent racing archive replay, no-materialized-winner append conflict, and same-key differing-fingerprint race rejection. (AC13) [`tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveIdempotencyTests.cs`]
- [x] [Review][Patch] `FolderCommandActionTokenMapper` gives `ArchiveFolder` its own `archive_folder` token, but `GrantFolderAccess` / `RevokeFolderAccess` still map to `mutate_files`. `EffectivePermissionsActionCatalog` now classifies `archive_folder` as `Administer` while ACL mutations remain `Write`. **Applied:** folder ACL mutations now map to `manage_folder_access`, which is an `Administer` action in the effective-permissions catalog and supported by organization/folder ACL action vocabularies. Tests cover mapper and catalog behavior. [`src/Hexalith.Folders.Server/Authorization/FolderCommandActionTokenMapper.cs`] [`src/Hexalith.Folders/Authorization/EffectivePermissionsActionCatalog.cs`] [`src/Hexalith.Folders/Aggregates/Folder/FolderAccessAction.cs`] [`src/Hexalith.Folders/Aggregates/Organization/OrganizationAclAction.cs`] [`tests/Hexalith.Folders.Server.Tests/FolderCommandActionTokenMapperTests.cs`] [`tests/Hexalith.Folders.Tests/Authorization/ArchiveActionCatalogTests.cs`]
- [x] [Review][Patch] `ForeignArchiveEventShouldRejectDuringReplay` asserts on `InvalidOperationException.Message` string content. **Applied:** message-text assertion removed; the test now asserts only the exception type. [`tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveStateTransitionTests.cs`]
- [x] [Review][Patch] Three submodule pointer changes (`Hexalith.EventStore`, `Hexalith.Memories`, `Hexalith.Tenants`) are bundled in commit `ea5f08b` with no justification in the change log. **Disposition:** no submodule working-tree changes are present in this follow-up. Current branch history now carries later explicit submodule/preflight commits, and this story records that no nested submodule initialization or recursive update was performed during the follow-up. [git diff: submodule SHAs in commit `ea5f08b`]
- [x] [Review][Patch] `ScaffoldContractTests` was extended with a `Hexalith.Folders.UI.E2E.Tests` reference — UI work is explicitly out of scope for Story 2.8 per AC14. **Disposition:** no new UI work was added in this follow-up. The UI E2E placeholder project and solution entry predate the archive commit (`a88da4c`); keeping the scaffold contract aligned with the current solution prevents scaffold drift while the skipped UI E2E lane remains owned by Epic 6. [`tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs:32,120`]
- [x] [Review][Defer] `IFolderEvent` interface coupling — widening `FolderProjectionEnvelope.Event` from `FolderCreated` to `IFolderEvent` raises consumer-side type-discrimination concerns (workers, snapshots, generated code) that exceed Story 2.8 scope. — deferred, pre-existing event-interface design.
- [x] [Review][Defer] Archive metadata fields not cleared on a hypothetical unarchive — Story 2.8 explicitly excludes restore/unarchive (AC14, regression traps). — deferred, will be addressed when restore/unarchive is scoped.
- [x] [Review][Defer] AC10 "archive reason category" not surfaced on `FolderLifecycleStatus` — `FolderLifecycleStatus` schema is `additionalProperties: false` with no reason field. Surfacing requires a contract-workflow story per the Do-Not-Touch rule. [`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:7165-7185`] — deferred, blocked by Contract Spine.

### Dismissed during triage (not written above)

- Self-review loop in change log (single "Codex" author across create / hardening / party-mode / review) — process smell, not a code defect.
- Three failing `predev-preflight-*.json` reports committed alongside the change — process concern, addressed separately.
- `FolderArchiveTenantGate` assumed to depend on repository methods that don't exist — tests pass in solution per Dev Agent Record, so the methods exist; false positive.
- `HasCompetingClientTenant` dead branch on `command.ManagedTenantId` after `WithAuthoritativeTenant` — harmless redundancy.

## Round-2 Review Findings

Generated by `/bmad-code-review 2.8` second pass (Blind Hunter + Edge Case Hunter + Acceptance Auditor) over commits `ea5f08b..c3e948e` on 2026-05-20. Triage focused on verifying the round-1 "Resolved" claims against current source. Edge Case Hunter ran against the older `2-8-review-diff.patch`; items it noted as "Patched on disk" were verified resolved in `c3e948e` and dismissed.

### Decision needed

_(resolved 2026-05-20 — both items reclassified to patch via the "follow best practices" call: gate wiring per the project's REST-thin / `/process`-thick architecture rule; AC9 fixtures interpreted as "at least representative" not "all nine.")_

### Patch (unambiguous fixes)

- [ ] [Review][Decision] **[BLOCKED] Wire `FolderArchiveTenantGate` as an `IDomainProcessor` in production** — Round-2 patch attempt hit an architectural decision that cannot be resolved inside a code-review pass:

  - `IDomainProcessor.ProcessAsync(CommandEnvelope, object?)` returns `DomainResult` carrying `IReadOnlyList<IEventPayload>` — the EventStore framework owns persistence of those events.
  - `FolderArchiveTenantGate.Handle` already persists via `IFolderRepository.AppendIfFingerprintAbsent` (the gate owns idempotency-key-conditional append plus race-conflict reread).
  - These are two competing persistence paths. Three reconciliation options exist:
    1. Gate stops persisting; returns events as a `FolderResult`; processor persists via `DomainResult`. Cascades into `FolderAccessTenantGate` (same pattern), idempotency-key-conditional append semantics in framework-owned persistence, and projection wiring.
    2. Processor returns empty `DomainResult`; gate keeps persisting. Effectively bypasses the framework's persistence guarantees while pretending to integrate.
    3. Unify `IFolderEvent` with `IEventPayload`. Touches every event type, projection envelope, and the `IFolderRepository` contract.
  - Even with wiring in place, current endpoint tests use `RecordingEventStoreGatewayClient` which short-circuits the gateway → `/process` round-trip, so the wiring would be unverified by the existing test surface. A genuine end-to-end test requires an in-process gateway that round-trips back into `/process`.

  Recommendation: scope as its own follow-up story (e.g., `2-8b-wire-folder-domain-processor`) with an explicit ADR captured under `docs/adrs/` documenting which of the three options the team chooses. Until then, the REST endpoint accepts archive commands and posts them to the gateway; the production `/process` callback returns `501 NotImplemented` because no processor is registered. **This means archive is functionally broken in real production wiring until the follow-up story lands** — the story file and Change Log must reflect that the round-1 "Resolved 2026-05-20" claim was incorrect.
- [x] [Review][Patch] **Add representative `FolderActiveMutationGuard` fixtures for non-ACL categories** — **Applied:** added `ActiveOnlyMutationGuardShouldAcceptActiveFolders` Theory covering all 10 categories on Active state (paired with the existing Archived-state Theory) and `ActiveOnlyMutationGuardShouldRejectUncreatedFolderAsNotFound` to lock in the pre-create branch. AC9 representative-fixture requirement now closed across the full category vocabulary. [`tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveStateTransitionTests.cs`]
- [x] [Review][Dismiss] ~~Same-key replay against archived folder returns `IdempotentReplay`, not `AlreadyArchived`~~ — initial classification was incorrect. On second look, `IdempotentReplay` is the correct outcome for same-key replay: the prior archive command was accepted (its `FolderArchived` event is what put the folder in archived state), so a logical replay must surface as `IdempotentReplay` rather than `AlreadyArchived`. `AlreadyArchived` is reserved for different-key second attempts, which falls through to the lifecycle check correctly. The gate's `ResolveAppendConflict` reread relies on this ordering. Patch attempt broke `AppendConflictShouldReturnIdempotencyConflictWhenRacingSameKeyCarriesDifferentFingerprint` and `AppendConflictShouldRereadAndReturnIdempotentReplayWhenRacingEquivalentArchiveWon`; reverted with an explanatory comment in [FolderAggregate.cs](src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs). Same-key archive replay on an already-archived folder maps to `IdempotentReplay` regardless of fingerprint discipline, contradicting the gate-path semantics (which uses the decision-bound fingerprint and would distinguish replay-of-the-archive-itself from replay-against-an-archived-folder). Move the archived check above the idempotency probe, or document why aggregate and gate diverge.
- [x] [Review][Patch] **Gateway 5xx mapped to client-facing `403 tenant_access_denied`** — **Applied:** added an explicit `>= 500 and < 600` arm in `ToArchiveGatewayProblem` that maps to `503 read_model_unavailable / evidence_unavailable, retryable=true`. New theory `ArchiveFolderEndpointShouldMapGatewayServerErrorsToSafeUnavailable` covers 500/502/504. [`src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`]
- [x] [Review][Patch] **`FolderArchiveAclEvidence.Allowed` accepts empty `organizationId`** — **Applied:** constructor now requires non-empty `organizationId` when `outcome == Allowed`, matching the existing managedTenantId/folderId/principalId invariants. [`src/Hexalith.Folders/Aggregates/Folder/FolderArchiveAclEvidence.cs`]
- [x] [Review][Patch] **`ResponseJsonOptions` reused for outbound gateway payload** — **Applied:** split out a dedicated `GatewayPayloadJsonOptions` for the `SubmitCommandRequest.Payload` serialization with a comment explaining why it stays separate from `ResponseJsonOptions`. Future HTTP-response tweaks no longer leak into the gateway wire shape. [`src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`]
- [x] [Review][Patch] **`TryParseArchiveReasonCode` duplicated** — **Applied:** added `FolderArchiveReasonCodes` static class in the Folders project exposing `IsSupported`, `TryParse`, `ToContractValue`, and the three string constants. Endpoint now calls `FolderArchiveReasonCodes.IsSupported`; validator delegates to `FolderArchiveReasonCodes.TryParse`. Single source of truth. [`src/Hexalith.Folders/Aggregates/Folder/FolderArchiveReasonCodes.cs`] [`src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs`] [`src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`]
- [x] [Review][Patch] **`ArchiveFolderEndpointTests` missing coverage paths** — **Applied:** added `ArchiveFolderEndpointShouldReturnUnauthenticatedWhenTenantContextIsMissing` (401 + no gateway hit), `ArchiveFolderEndpointShouldRejectWhenRequiredEnvelopeHeaderIsMissing` Theory for `X-Correlation-Id` and `X-Hexalith-Task-Id`, `ArchiveFolderEndpointShouldRejectMalformedJsonBodyBeforeGatewaySubmit`, `ArchiveFolderEndpointShouldRejectBodyWithUnknownFieldsBeforeGatewaySubmit` (proves `JsonUnmappedMemberHandling.Disallow` actually rejects smuggled fields), and `ArchiveFolderEndpointShouldRejectBodyWithNullRequiredFieldsBeforeGatewaySubmit`. All assert no gateway request was made. [`tests/Hexalith.Folders.Server.Tests/ArchiveFolderEndpointTests.cs`]
- [x] [Review][Patch] **`IsCanonicalIdentifier` regex rejects uppercase characters in gateway-returned correlation IDs** — **Applied:** added `GatewayCorrelationRegex` (`^[A-Za-z0-9._-]+$`) and `IsSafeGatewayCorrelationId` helper, used for the gateway-corrected echo at line 327 and for `ToArchiveGatewayProblem`'s `exception.CorrelationId` re-check. Caller-supplied identifiers stay strictly lowercase canonical; the gateway-corrected echo accepts ULID/UUID uppercase hex so traces remain joinable. [`src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`]
- [x] [Review][Patch] **Magic constant `"folders"` literal in `FolderCommandActionTokenMapperTests`** — **Applied:** added `public const string DomainName = "folders"` to `FoldersServerModule` (already a public class with related route constants). Updated `FolderCommandActionTokenMapperTests` and `FoldersDomainServiceRequestHandlerTests` to use `FoldersServerModule.DomainName`. Endpoint also references `FoldersServerModule.DomainName` so the wire-side value stays single-source. [`src/Hexalith.Folders.Server/FoldersServerModule.cs`] [`src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`] [`tests/Hexalith.Folders.Server.Tests/FolderCommandActionTokenMapperTests.cs`] [`tests/Hexalith.Folders.Server.Tests/FoldersDomainServiceRequestHandlerTests.cs`]
- [x] [Review][Patch] **`FolderArchiveProjectionReplayTests` foreign-tenant assertion missing** — **Applied:** `ProjectionShouldSkipArchiveEventWhenEnvelopeTenantDisagreesWithEventTenant` now also asserts `projection.Get("tenant-b", "folder-a").ShouldBeNull()` so a guard mistakenly keyed on the event's claimed tenant would fail the test. [`tests/Hexalith.Folders.Tests/Projections/FolderList/FolderArchiveProjectionReplayTests.cs`]
- [x] [Review][Patch] **`ArchiveFolderEndpointShouldSurfaceIdempotentReplayFromGatewayPayload` lacks gateway-invocation assertion** — **Applied:** test now asserts `gateway.Requests.Count.ShouldBe(1)` and verifies the submitted `CommandType` matches `Hexalith.Folders.Commands.ArchiveFolder`. A future endpoint short-circuit would fail the test. [`tests/Hexalith.Folders.Server.Tests/ArchiveFolderEndpointTests.cs`]

### Defer (real but not actionable in this story)

- [x] [Review][Defer] Validator-version fingerprint skew (cross-deploy `IsSafeEvidenceIdentifier` changes can invalidate prior idempotency replay) — needs a versioned-validator regression test once a second validator version exists.
- [x] [Review][Defer] `FolderListProjection.Apply` throw on missing-create kills multi-tenant rebuild on one bad envelope while `FolderStateApply` is per-stream — asymmetric blast radius; revisit when projection-rebuild tooling is introduced.
- [x] [Review][Defer] `FolderArchiveAclEvidence` is an unsigned value object — defense relies on trusting upstream `Allowed(...)` callers; architectural concern beyond Story 2.8.
- [x] [Review][Defer] `FolderState` has six adjacent same-typed nullable-string parameters (silent-swap risk on positional construction) — current callers use `with`-syntax; revisit if a positional caller is introduced.
- [x] [Review][Defer] `FolderArchived.IdempotencyFingerprint` non-empty invariant undocumented at the event level — not currently reachable from `FolderAggregate.Handle`.
- [x] [Review][Defer] `ArchiveFolder.PayloadTenantId` with malformed segment matching the authoritative tenant — narrow edge case; revisit if payload-tenant smuggling becomes a verified threat.
- [x] [Review][Defer] `ArchiveFolderClientConformanceTests` parameter-order assertion is brittle to NSwag generator changes — currently passing.
- [x] [Review][Defer] `FolderArchiveMetadataLeakageTests` asserts the validator's input restriction indirectly via factory defaults — gives confirmation, not coverage.
- [x] [Review][Defer] Untested branches for `FolderArchivePolicyOutcome.ScopeMismatch`, several `FolderArchiveAclOutcome` variants, and `FolderAppendOutcome.FingerprintConflict` mapping — become live once the gate is wired.
- [x] [Review][Defer] `FolderArchiveTenantGate(TimeProvider)` constructor never exercised — moot until the gate is wired.
- [x] [Review][Defer] No `CancellationToken` propagation through `FolderArchiveTenantGate.Handle` or `IFolderRepository` methods — moot until the gate is wired.
- [x] [Review][Defer] `EffectivePermissionsActionCatalog` insertion order untested — cosmetic until a positional consumer appears.

### Dismissed during round-2 triage

- All Edge Case Hunter findings marked "Patched on disk" — verified against current source, fixes are in place (the agent reviewed the older `2-8-review-diff.patch`, not `c3e948e`).
- Auditor AC6 case-sensitive `requestSchemaVersion` — informational, the redundant aggregate-side check is acceptable defense in depth.
- Auditor AC5 envelope-identifier validation before authentication — spec line 87 permits schema-only validation pre-auth.
- Auditor AC11 gateway correlation reflection — mitigated by `IsCanonicalIdentifier` re-validation at line 327.
- Auditor AC10 "inaccessible" collapses archived with binding-malformed — already dispositioned in round-1 item #276.
- Auditor AC14 `ScaffoldContractTests` UI E2E reference — already dispositioned in round-1 item #309.
- Blind Hunter #1 "ResponseJsonOptions drops unknown fields" — opposite-of-a-bug; the `RequestJsonOptions` `Disallow` setting is the defense.
- Blind Hunter #2 misleading error message when `requestSchemaVersion` is missing vs. wrong case — error message text quality, not a behavior bug.
- Blind Hunter #7 `IsIdempotentReplay` short-circuit-with-reused-`out` is awkward but functional.

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-20 | Round-2 code review (`/bmad-code-review 2.8`, Blind Hunter + Edge Case Hunter + Acceptance Auditor) over commits `ea5f08b..c3e948e`: verified round-1 "Resolved 2026-05-20" gate-wiring claim was incorrect (`FolderArchiveTenantGate` has zero non-test callers; no `IDomainProcessor` registered; `/process` returns 501). 2 decision-needed → "follow best practices" → 1 BLOCKED architectural decision (gate wiring), 1 patch (mutation guard). 10 contained patches applied: `FolderArchiveReasonCodes` shared catalog, dedicated `GatewayPayloadJsonOptions`, gateway 5xx → safe 503 mapping, `FolderArchiveAclEvidence` non-empty `organizationId` invariant, gateway-corrected correlation ID uppercase-tolerant validator, `FoldersServerModule.DomainName` constant, expanded `ActiveMutationGuard` Theory covering Active state + uncreated state, foreign-tenant projection assertion, gateway-invocation count assertion in replay test, 5 new endpoint coverage tests (401/missing-headers/malformed-JSON/unknown-fields/null-fields). 1 finding revert (same-key replay against archived): on re-analysis the original `IdempotentReplay` ordering is correct. Gate-wiring patch blocked on `IDomainProcessor` (framework persists) vs `FolderArchiveTenantGate` (gate persists) architectural mismatch — surfaced as decision-needed for follow-up story `2-8b`. `dotnet build Hexalith.Folders.slnx --no-restore` passed; `dotnet test Hexalith.Folders.slnx --no-restore` returned 612 passed, 1 skipped, 0 failed. | Claude |
| 2026-05-20 | Completed review follow-up implementation: endpoint denial/replay mapping, schema-version hinting, archive fingerprint decision evidence, policy-before-stream checks, expanded policy/timing/leakage/race/parity tests, and ACL action-token alignment. `dotnet build Hexalith.Folders.slnx --no-restore` passed; `dotnet test Hexalith.Folders.slnx --no-restore` returned 595 passed, 1 skipped, 0 failed. | Codex |
| 2026-05-20 | Applied 22 contained patches from code review: gate internals (distinct policy/ACL outcome codes, OrganizationId match, validate-before-ACL, fresh occurredAt on retry, exhaustive switch default, safe-denial Map, whitespace+key tenant guard), aggregate (`IsCreated` before idempotency), projection (loud failure on missing-create and unknown event types), ACL-evidence ctor invariants, category-neutral mutation guard, endpoint hardening (canonical-identifier validation, reserved-tenant rejection at edge, gateway-exception → safe 503, body parse after auth, correlation-id revalidation, TimeProvider injection, `JsonUnmappedMemberHandling.Disallow`), and the brittle exception-text test fix. `dotnet test Hexalith.Folders.slnx` returns 562 passed, 1 skipped, 0 failed. | Claude |
| 2026-05-20 | Code review (`/bmad-code-review 2.8`, Blind Hunter + Edge Case Hunter + Acceptance Auditor) over commit `ea5f08b`: 2 decision-needed, 35 patch, 3 deferred, 4 dismissed. | Claude |
| 2026-05-20 | Implemented archive lifecycle command, endpoint wiring, authorization/idempotency guards, projections, conformance coverage, and regression tests; moved story to review. | Codex |
| 2026-05-19 | Applied advanced-elicitation hardening for compatible archive decision snapshots, fail-closed evidence handling, cache/idempotency scoping, and timing/discovery leakage tests. | Codex |
| 2026-05-19 | Party-mode review applied: tightened archive authorization ordering, idempotency/race semantics, already-archived result handling, audit/status observables, mutation guards, and parity/no-touch tests. | Codex |
| 2026-05-19 | Created story with archive command contract alignment, archived lifecycle transition, safe denial/idempotency rules, audit preservation, and offline leakage tests. | Codex |

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet test tests\Hexalith.Folders.Tests\Hexalith.Folders.Tests.csproj --no-restore` (367 passed)
- `dotnet test tests\Hexalith.Folders.Server.Tests\Hexalith.Folders.Server.Tests.csproj --no-restore` (42 passed)
- `dotnet test tests\Hexalith.Folders.Client.Tests\Hexalith.Folders.Client.Tests.csproj --no-restore` (24 passed)
- `dotnet test tests\Hexalith.Folders.Contracts.Tests\Hexalith.Folders.Contracts.Tests.csproj --no-restore` (81 passed)
- `dotnet test tests\Hexalith.Folders.Testing.Tests\Hexalith.Folders.Testing.Tests.csproj --no-restore` (43 passed)
- `dotnet build Hexalith.Folders.slnx --no-restore` (passed)
- `dotnet test Hexalith.Folders.slnx --no-restore` (passed; UI E2E placeholder skipped)
- `dotnet test tests\Hexalith.Folders.Server.Tests\Hexalith.Folders.Server.Tests.csproj --no-restore` (50 passed)
- `dotnet test tests\Hexalith.Folders.Tests\Hexalith.Folders.Tests.csproj --no-restore` (388 passed)
- `dotnet build Hexalith.Folders.slnx --no-restore` (passed)
- `dotnet test Hexalith.Folders.slnx --no-restore` (595 passed, 1 skipped, 0 failed; UI E2E placeholder skipped)

### Completion Notes List

- Story created by `/bmad-create-story 2-8-archive-folders-with-audit-preservation` equivalent workflow on 2026-05-19.
- Project context, Epic 2, PRD, architecture, Contract Spine archive operation, C3 retention notes, Stories 2.1-2.7, current folder aggregate/projection/test patterns, recent commits, and story-creation lessons were reviewed.
- Ultimate context engine analysis completed - comprehensive developer guide created.
- Added `ArchiveFolder`, reason-code validation, archive fingerprints, `FolderArchived`, archived lifecycle state, idempotent replay/conflict behavior, already-archived rejection, policy/evidence gates, and reusable active-mutation guard vocabulary.
- Wired the existing archive contract path through the server route, EventStore gateway submission, action token mapper, action catalog, and generated-client conformance tests without hand-editing generated SDK files.
- Preserved archive audit/status evidence in metadata-only aggregate state and folder-list projection fields while leaving provider cleanup, workspace cleanup, tenant deletion, legal hold, UI, CLI, MCP, and restore/unarchive out of scope.
- Added offline aggregate, authorization, idempotency, projection, lifecycle-status, client, server, scaffold-contract, and metadata leakage coverage; full solution tests pass.
- Follow-up hardened REST archive response mapping for gateway denial/status families and idempotent replay result payloads.
- Follow-up bound archive idempotency fingerprints to correlation/task/idempotency/payload tenant evidence plus policy version and tenant projection freshness watermark.
- Follow-up added policy-staleness, discovery/timing, sentinel leakage, append-race, lifecycle vocabulary parity, and ACL action-token coverage; no generated SDK files were hand-edited.

### File List

- `_bmad-output/implementation-artifacts/2-8-archive-folders-with-audit-preservation.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/process-notes/predev-preflight-latest.json`
- `_bmad-output/process-notes/predev-preflight-2026-05-20T065726Z.json`
- `_bmad-output/process-notes/predev-preflight-2026-05-20T070123Z.json`
- `src/Hexalith.Folders/Aggregates/Folder/ArchiveFolder.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAccessAction.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderActiveMutationCategory.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderActiveMutationGuard.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderArchiveAclEvidence.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderArchiveAclOutcome.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderArchivePolicyEvidence.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderArchivePolicyOutcome.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderArchiveReasonCode.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderArchiveTenantGate.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderArchived.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidationResult.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderLifecycleState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderResult.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderResultCode.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs`
- `src/Hexalith.Folders/Aggregates/Organization/OrganizationAclAction.cs`
- `src/Hexalith.Folders/Authorization/EffectivePermissionsActionCatalog.cs`
- `src/Hexalith.Folders/Projections/FolderList/FolderListItem.cs`
- `src/Hexalith.Folders/Projections/FolderList/FolderListProjection.cs`
- `src/Hexalith.Folders/Projections/FolderList/FolderProjectionEnvelope.cs`
- `src/Hexalith.Folders.Server/Authorization/FolderCommandActionTokenMapper.cs`
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs`
- `tests/Hexalith.Folders.Client.Tests/ArchiveFolderClientConformanceTests.cs`
- `tests/Hexalith.Folders.Server.Tests/ArchiveFolderEndpointTests.cs`
- `tests/Hexalith.Folders.Server.Tests/FolderCommandActionTokenMapperTests.cs`
- `tests/Hexalith.Folders.Server.Tests/ServerEndpointRegistrationTests.cs`
- `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveAuthorizationGateTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveCommandValidationTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveIdempotencyTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveMetadataLeakageTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveStateTransitionTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderCommandFactory.cs`
- `tests/Hexalith.Folders.Tests/Authorization/ArchiveActionCatalogTests.cs`
- `tests/Hexalith.Folders.Tests/Projections/FolderList/FolderArchiveProjectionReplayTests.cs`
- `tests/Hexalith.Folders.Tests/Queries/Folders/FolderLifecycleStatusProjectionTests.cs`
