# Story 2.5: Inspect effective permissions

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an authorized actor,
I want to inspect effective permissions for a folder or task context,
so that I can explain who can perform work before a task begins.

## Terms

- Effective permissions means the read-only, metadata-only result of evaluating authoritative tenant context, Story 2.1 tenant-access evidence, Story 2.2 organization ACL baseline, Story 2.4 folder ACL overrides, folder lifecycle state, and optional task/workspace scope.
- Principal source means the safe origin of an allowed action, such as organization baseline, folder override grant, delegated service agent evidence, or direct actor evidence. It must not include user names, group display names, emails, role display names, raw membership inventories, or tenant configuration payloads.
- Authorization outcome means a stable sanitized result such as `allowed`, `denied_safe`, `projection_stale`, or `projection_unavailable`. It must not reveal whether an unauthorized folder, ACL entry, repository, workspace, lock, audit record, or provider resource exists.
- Freshness metadata means the projection watermark, effective timestamp, read-consistency class, and stale/unavailable evidence needed to explain whether the authorization answer is current enough for the requested query.
- Task context means optional metadata-only workspace/task identifiers used to narrow the permission explanation for later workspace lifecycle operations. It is not permission to inspect files, locks, provider state, audit payloads, commits, or workspace contents.
- Effective-permission inspection is a query. It must not mutate folder state, create ACL entries, repair projections, refresh Tenants data as a side effect, acquire locks, call providers, inspect repositories, or read files.
- Safe denial envelope means the existing Contract Spine authorization failure responses for this operation: `SafeAuthorizationDenial401`, `SafeAuthorizationDenial403`, `SafeAuthorizationDenial404`, and `ReadModelUnavailable` for `503`. Cross-tenant, unauthorized, not-found-to-caller, stale, and unavailable paths must use those existing shapes without adding resource-existence evidence.
- C7 revocation freshness means the folder ACL override projection can prove the latest relevant revoke watermark before returning an allowed action. If freshness cannot be proven from `FreshnessMetadata.projectionWatermark`, `observedAt`, `stale`, or a local equivalent, the query returns safe denied/stale/unavailable evidence rather than an allowed permission.
- Canonical permission response means the query emits deterministic, deduplicated, lower-snake-case action tokens and stable principal-source classes sorted by contract-defined order, without membership counts, display labels, per-source identifiers, or projection input ordering artifacts.

## Acceptance Criteria

1. Given the Contract Spine already declares `GET /api/v1/folders/{folderId}/effective-permissions` with `operationId: GetEffectivePermissions`, when this story is implemented, then the domain/query handler, server endpoint wiring, read-model contracts, and tests satisfy that existing operation without hand-editing generated SDK files or creating a second permission-inspection contract.
2. Given authoritative tenant context and Story 2.1 tenant-access evidence are allowed, when an authorized actor requests effective permissions for a folder, then the query resolves tenant authority from authentication context or EventStore envelope first, verifies Story 2.1 evidence second, and only then may construct folder stream names, read-model keys, cache keys, folder projections, ACL projections, lifecycle projections, task/workspace projections, diagnostics, audit records, provider state, repository state, or file/workspace resource handles.
3. Given tenant identity appears in route values, query parameters, request headers, generated client arguments, task context metadata, forwarded headers, metadata bags, or client-controlled envelope fields, when the query context is built, then tenant authority comes only from authentication context or EventStore envelope/projection authority; mismatched client-supplied tenant values reject with the existing safe denial envelope before any resource lookup.
4. Given tenant-access evidence is stale beyond query policy, unavailable, disabled, unknown, malformed, future-dated, replay-conflicting, tenant-mismatched, denied, or missing authoritative tenant context, when effective permissions are requested, then the query returns stable metadata-only denial or unavailable evidence using the existing `401`, `403`, `404`, or `503` Contract Spine responses without revealing folder existence, ACL entries, organization baseline, task/workspace existence, audit records, provider readiness, repository bindings, locks, files, or tenant-specific identifiers from other tenants.
5. Given Story 2.2 organization ACL baseline and Story 2.4 folder ACL override metadata are available after tenant access passes, when permissions are computed, then allowed actions are derived deterministically from strict lower-snake-case action tokens and safe principal-source classes in this order: organization baseline grants, folder override grants, folder override revocations, C7 revocation freshness gate, lifecycle constraints, and task-context narrowing where applicable. Folder revocations win over both organization baseline grants and folder grants for the same tenant, folder, principal, and action; equivalent inputs produce the same canonical response regardless of projection input ordering, duplicate projection rows, duplicate principal memberships, or mixed grant/revoke ordering.
6. Given a Story 2.4 folder revoke exists for the same tenant, folder, principal, and action, when effective permissions are computed, then the revoked action is absent or marked denied according to the existing `EffectivePermissions` contract, and freshness metadata carries the revocation projection watermark, observed timestamp, stale flag, and local unavailable/stale reason needed to prove C7 revocation propagation evidence. If revoke freshness cannot be proven, the query must not return the action as allowed.
7. Given the actor requests task-context permissions, when task/workspace identifiers are supplied, then they are treated as opaque metadata-only scope constraints after tenant and folder ACL authorization; valid task context can only reduce effective permissions, never expand them. Invalid, outside-tenant, outside-folder, inaccessible, stale, unavailable, or unauthorized task/workspace scope returns safe evidence without revealing task, lock, workspace, repository, file, commit, provider, or audit existence.
8. Given folder lifecycle state is active, archived, missing to the authorized caller, missing to an unauthorized caller, projection-stale, projection-unavailable, or malformed, when effective permissions are requested, then the query returns stable outcome codes and read-consistency/freshness evidence without changing lifecycle state, repairing projections, scanning aggregates directly, or exposing internal event names, exception text, timing-sensitive existence evidence, or different response bodies for unauthorized/not-found/cross-tenant cases.
9. Given safe results are returned, when callers inspect the response, then it contains only folder ID, deduplicated allowed action tokens, principal source classes, authorization outcome, optional task/workspace opaque IDs already authorized for the caller, freshness metadata, correlation/task IDs, operation ID, result code, version/sequence/watermark, and timestamps. The response must not expose whether a permission came from a specific user, group, role, ACL row, projection row, cache entry, or replayed event.
10. Given events, logs, traces, metrics, projections, audit records, error responses, Problem Details, generated client exceptions, or test output are produced, when metadata is inspected, then raw auth headers, provider tokens, credential material, repository names, branch names, file paths, file contents, diffs, generated context payloads, user emails, group names, role display names, tenant configuration payloads, membership inventories, unauthorized resource identifiers, and raw command/query bodies are absent.
11. Given the effective-permissions read model is stale or unavailable after authorization succeeds, when the query cannot prove an allowed answer within policy, then it returns `projection_stale`, `projection_unavailable`, `read_model_unavailable`, safe `denied_safe`, or local equivalent metadata-only evidence rather than falling back to direct aggregate scans, projection repair, compensating writes, provider calls, repository calls, audit queries, filesystem reads, cached permissions from another principal/task scope, or permissive defaults.
12. Given tests run without provider credentials, tenant seed data, production secrets, running Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, or nested submodule initialization, when unit, server, and contract-alignment tests execute, then tenant gates, ACL layering, folder override revocation, task-scope narrowing, safe denial, freshness metadata, read-model unavailable/stale behavior, response shape, parity metadata, and leakage boundaries are covered with in-memory fakes and spies, including matrices for cross-tenant folder IDs, unauthorized same-tenant folder IDs, stale revoke evidence, unavailable permission projections, unauthorized task-context IDs, and metadata-only logs/errors.
13. Given this story owns effective-permission inspection only, when implementation is complete, then it does not implement ACL grant/revoke mutation, organization ACL mutation, folder creation, folder archive, provider readiness, repository binding, workspace preparation, locks, file mutation, commits, context query, audit browsing, CLI/MCP/UI commands, workers, production Dapr policy changes, repair workflows, local-only folder mode, webhooks, brownfield adoption, or multi-organization-per-tenant behavior.

## Tasks / Subtasks

- [x] Implement the effective-permissions query surface. (AC: 1, 8, 9, 11)
  - [x] Add a domain/query handler such as `GetEffectivePermissions` under `src/Hexalith.Folders/Authorization/` or `src/Hexalith.Folders/Queries/Permissions/`, aligned with existing project structure when Stories 2.1-2.4 land.
  - [x] Wire the existing `GET /api/v1/folders/{folderId}/effective-permissions` operation in `Hexalith.Folders.Server` only if the server endpoint is not already generated/wired by earlier stories.
  - [x] Return a metadata-only result compatible with the Contract Spine `EffectivePermissions` schema: required `folderId`, `permissions`, `authorizationOutcome`, and `freshness`; `authorizationOutcome` values remain `allowed` or `denied_safe`.
  - [x] Preserve existing operation responses and casing exactly: `200` `EffectivePermissions`, `401` `SafeAuthorizationDenial401`, `403` `SafeAuthorizationDenial403`, `404` `SafeAuthorizationDenial404`, and `503` `ReadModelUnavailable`.
  - [x] Preserve `x-hexalith-read-consistency.class: read_your_writes` and freshness behavior that reports the authorization projection watermark through `FreshnessMetadata.readConsistency`, `observedAt`, optional `projectionWatermark`, and `stale`.
  - [x] Do not hand-edit `src/Hexalith.Folders.Client/Generated/*`; regenerate only through the established Contract Spine toolchain if generated output is legitimately stale.
- [x] Enforce authorization-before-observation gates. (AC: 2, 3, 4, 7)
  - [x] Consume authoritative tenant context before constructing folder stream names, read-model keys, cache keys, diagnostic keys, audit keys, or task/workspace lookup keys.
  - [x] Consume Story 2.1 tenant-access evidence before folder, ACL, lifecycle, task, workspace, audit, provider, repository, lock, or file resources are observed.
  - [x] Treat route/query/header/client tenant IDs, forwarded headers, metadata bags, and client-controlled envelope fields as comparison values only; mismatches return the existing safe denial evidence.
  - [x] Add in-memory spies proving denied tenant evidence prevents folder projection lookup, ACL projection lookup, task/workspace lookup, diagnostics, audit, provider, repository, and filesystem access.
  - [x] Keep safe denial envelopes externally indistinguishable across cross-tenant folder IDs, tenant denial, folder ACL denial, missing folder to an unauthorized caller, and unavailable protected resources.
- [x] Compute permission layers deterministically. (AC: 5, 6, 8, 9)
  - [x] Consume Story 2.2 organization ACL baseline as an input, not as state to mutate.
  - [x] Consume Story 2.4 folder override grant/revoke metadata and apply precedence as organization baseline grants, folder override grants, folder override revocations, C7 revocation freshness gate, lifecycle constraints, and task-context narrowing.
  - [x] Prove revokes win over organization baseline grants and folder grants for the same tenant/folder/principal/action tuple, including conflicting stale grant and fresh revoke evidence.
  - [x] Use strict lower-snake-case action tokens from the MVP folder ACL vocabulary: `configure_provider_binding`, `prepare_workspace`, `lock_workspace`, `read_metadata`, `read_file_content`, `mutate_files`, `commit`, `query_status`, `query_audit`, and `view_operations_console`; include `create_folder` only as organization-baseline evidence when explaining why it is not a folder override.
  - [x] Preserve principal source classes without exposing names, emails, display labels, membership counts, ACL row identifiers, projection row identifiers, cache keys, or raw membership inventories.
  - [x] Canonicalize outputs by deduplicating actions and principal-source classes, sorting them by contract-defined order, and proving duplicate or reordered projection inputs do not change the serialized response.
  - [x] Include revocation sequence/watermark/effective timestamp where needed so later C7 lock revalidation can prove freshness.
- [x] Add read-model freshness and unavailable behavior. (AC: 6, 8, 11)
  - [x] Define the local equivalent of `projection_stale`, `projection_unavailable`, and `read_model_unavailable` for this query if not already present.
  - [x] Ensure stale/unavailable permission projections fail closed for allowed answers rather than falling back to direct aggregate/event scans, projection repair, compensating writes, or provider/audit/filesystem lookups that bypass projection policy.
  - [x] Distinguish authorized stale/unavailable evidence from unauthorized safe denial without exposing protected resource existence to unauthorized callers.
  - [x] Include freshness metadata in success and authorized unavailable/stale outcomes: watermark, effective timestamp, consistency class, and stale/unavailable reason code.
  - [x] Add explicit behavior for fresh revoke, stale revoke evidence, unavailable revocation evidence, stale permission read model, unavailable permission read model, and conflicting stale grant/fresh revoke evidence.
  - [x] Scope any permission cache or read-model memoization by authoritative tenant, folder, principal, task/workspace scope, revocation watermark, and read-consistency class; never reuse an allowed result across principals, tenants, folders, or task scopes.
- [x] Add task-context narrowing without workspace behavior. (AC: 7, 13)
  - [x] Accept only opaque task/workspace scope identifiers already authorized for the caller, if the current contract or local handler supports task-context inspection.
  - [x] Narrow actions only by metadata-only task/workspace state that is safe to reveal after authorization; task context must never broaden permissions.
  - [x] Return safe evidence for no task context, valid narrowing context, outside-tenant context, outside-folder context, unauthorized context, unavailable context, and a context that removes all effective permissions.
  - [x] Do not inspect locks, workspace directories, file paths, provider state, repository state, commits, diffs, audit payloads, or worker state.
  - [x] Return stable safe evidence for invalid, mismatched, unauthorized, stale, or unavailable task-context metadata.
- [x] Add tests and fixtures. (AC: 1-13)
  - [x] Add unit tests such as `EffectivePermissionsAuthorizationGateTests`, `EffectivePermissionsLayeringTests`, `EffectivePermissionsRevocationFreshnessTests`, `EffectivePermissionsTaskScopeTests`, `EffectivePermissionsReadModelFreshnessTests`, and `EffectivePermissionsMetadataLeakageTests`.
  - [x] Add server/contract alignment tests proving route, status codes, casing, nullable fields, empty collections, error envelope, authorization failure semantics, and Problem Details categories match the existing OpenAPI operation without requiring live Dapr, EventStore, Tenants, Redis, Keycloak, GitHub, Forgejo, provider credentials, or nested submodules.
  - [x] Add negative-control tests named like `RejectsBeforeFolderProjectionWhenTenantMissing`, `RejectsBeforeAclProjectionWhenTenantDenied`, `RejectsBeforeTaskLookupWhenFolderAclDenied`, and `DoesNotFallbackToProviderWhenPermissionProjectionUnavailable`, or local equivalents.
  - [x] Add ACL precedence matrix tests for organization baseline grant, folder override grant, folder override revoke, revoke-over-grant, multiple principals, inherited-only access, direct-only access, empty ACL, and input-order independence.
  - [x] Add canonicalization tests for duplicate grants, duplicate revokes, repeated principal memberships, unordered projection rows, unordered action tokens, and stable serialization of equivalent permission evidence.
  - [x] Add C7 freshness tests for fresh revoke, stale read-model evidence, unavailable evidence, conflicting stale grant/fresh revoke, and safe denial/default-unavailable behavior when freshness cannot be established.
  - [x] Add task-context matrix tests for no task context, valid narrowing context, context outside tenant, context outside folder, context not authorized, context unavailable/stale, and context that removes all permissions.
  - [x] Add cross-tenant tests with matching folder IDs, principal IDs, task IDs, and action tokens to prove tenant scope comes from authoritative context and projection/envelope authority.
  - [x] Add revocation tests proving a Story 2.4 revoke removes or denies the action and carries C7 freshness evidence.
  - [x] Add leakage sentinel tests for foreign tenant IDs, folder IDs, principal IDs, ACL labels, task IDs, permission names, raw auth headers, provider tokens, credential material, repository names, branch names, file paths, file contents, diffs, generated context payloads, user emails, group names, role display names, tenant configuration payloads, membership inventories, unauthorized resource IDs, raw query bodies, logs, traces, exception messages, and Problem Details.
  - [x] Extend `src/Hexalith.Folders.Testing/Factories/*` only with reusable effective-permission builders that delegate to production validation rules; add `tests/Hexalith.Folders.Testing.Tests` coverage if new helpers are introduced.

## Dev Notes

### Source Context

- Epic 2 objective: tenant administrators and authorized actors can create folders, manage access, inspect effective permissions, archive folders, and receive safe authorization evidence with cross-tenant isolation enforced before any resource access. [Source: `_bmad-output/planning-artifacts/epics.md#Epic 2: Tenant-Scoped Folder Access And Lifecycle`]
- Story 2.5 foundation: an authorized actor inspects effective permissions for a folder or task context; the response shows allowed actions and principal sources while omitting unauthorized resource existence and secret material. [Source: `_bmad-output/planning-artifacts/epics.md#Story 2.5`]
- PRD FR6 requires authorized effective-permission inspection; FR9 requires denial before exposing folder, repository, credential, lock, file, audit, provider, or context information; FR10 requires allowed and denied authorization evidence without unauthorized resource details. [Source: `_bmad-output/planning-artifacts/prd.md#Authorization and Tenant Boundary`]
- The Contract Spine already defines `GET /api/v1/folders/{folderId}/effective-permissions`, `operationId: GetEffectivePermissions`, read-your-writes consistency, safe denial, and metadata-only audit keys. [Source: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#/paths/~1api~1v1~1folders~1{folderId}~1effective-permissions`]
- Architecture maps FR4-FR10 authorization and tenant boundary work to `src/Hexalith.Folders/Authorization/`, tenant-access projections, EventStore authorization boundaries, and server tenant context middleware. [Source: `_bmad-output/planning-artifacts/architecture.md#Requirements to Structure Mapping`]
- Project context requires zero cross-tenant leakage, metadata-only authorization evidence, tenant-prefixed durable/cache keys, and authorization before file/workspace/credential/repository/lock/commit/provider/audit access. [Source: `_bmad-output/project-context.md#Critical Don't-Miss Rules`]

### Previous Story Intelligence

- Story 2.1 defines the Folders host, Tenants event subscription, local fail-closed tenant-access projection, and tenant-access authorizer outcomes. Reuse its evidence vocabulary and do not create a second tenant authority model.
- Story 2.2 defines organization-level ACL baseline grants/revokes for `user`, `group`, `role`, and `delegated_service_agent` principals plus strict lower-snake-case action parsing. This story consumes that baseline as permission evidence.
- Story 2.3 defines `FolderAggregate`, opaque folder identity, `{managedTenantId}:folders:{folderId}` stream names, active lifecycle state, and tenant/ACL pre-load gates for folder creation. This story must not infer identity from display names, paths, repository names, or provider names.
- Story 2.4 defines folder-level ACL grants/revokes, folder override metadata, revocation projection evidence, and C7 freshness metadata. This story is the first public read surface that explains those effective permissions.

### Existing Implementation State

- The current baseline repo still has scaffold-oriented production files; Stories 2.1-2.4 may land domain and authorization surfaces before this story starts. Read the current implementations first and extend them rather than duplicating seams.
- `src/Hexalith.Folders.Testing/Factories/TestFolderContext.cs` validates stream-name segments and exposes `FolderStreamName` as `{ManagedTenantId}:folders:{FolderId}` plus `OrganizationStreamName` as `{ManagedTenantId}:organizations:{OrganizationId}`.
- `src/Hexalith.Folders.Testing/Factories/TestAuthorizationContext.cs` provides managed tenant and permission test context data. Extend it only if reusable effective-permission test builders need safe fixture data.
- The OpenAPI Contract Spine includes `EffectivePermissions`, `FolderPermissionLevel`, `FreshnessMetadata`, canonical safe authorization denials, and canonical error categories. Prefer aligning implementation/tests to these shapes over editing the spine.
- Generated SDK files under `src/Hexalith.Folders.Client/Generated/` are generated outputs and must not be hand-edited.

### Required Architecture Patterns

- Keep `Hexalith.Folders.Contracts` behavior-free. Effective-permission computation, authorization ordering, projection freshness policy, and safe-denial logic belong in `Hexalith.Folders`, `Hexalith.Folders.Server`, and tests.
- Keep the query read-only. It may evaluate metadata and projections after authorization, but it must not mutate aggregates, append events, repair projections, acquire locks, call providers, read repositories, inspect files, or query audit payloads as a fallback.
- Authorization order is authoritative tenant context, Story 2.1 tenant access, folder ACL/effective-permission read permission, optional task/workspace scope authorization, then protected projection lookup.
- Unauthorized, stale, unavailable, malformed, tenant-mismatched, folder-mismatched, task-mismatched, and projection-unavailable paths must not reveal folder existence, ACL entry existence, principal membership, workspace existence, lock holder, audit record existence, provider readiness, repository binding, file path, or file content.
- Use stable result codes and metadata fields in tests. Do not assert localized text, exception strings, event class names, diagnostic messages, display labels, or generated client exception text.
- Use C# file-scoped namespaces, nullable-aware records/classes, one public type per file, PascalCase types/members, camelCase locals/parameters, and async APIs for I/O boundaries.
- Any cache/read-model/durable key introduced for permission inspection must include authoritative tenant scope. Missing tenant prefix is a correctness and security bug.

### Files To Touch

- `src/Hexalith.Folders/Authorization/FolderAclAuthorizer.cs`
- `src/Hexalith.Folders/Authorization/EffectivePermissions*`
- `src/Hexalith.Folders/Authorization/AuthorizationOrder.cs`
- `src/Hexalith.Folders/Queries/Permissions/*` if the repo has or adopts a query-folder convention.
- `src/Hexalith.Folders/Projections/FolderAccess/*`
- `src/Hexalith.Folders/Projections/TenantAccess/*` only for consuming existing tenant evidence; do not fork the projection model.
- `src/Hexalith.Folders.Server/Endpoints/*` or local Minimal API modules only to wire the existing Contract Spine route.
- `src/Hexalith.Folders.Testing/Factories/*` only for reusable safe fixtures.
- `tests/Hexalith.Folders.Tests/Authorization/*EffectivePermissions*Tests.cs`
- `tests/Hexalith.Folders.Server.Tests/*EffectivePermissions*Tests.cs`
- `tests/Hexalith.Folders.Testing.Tests/*` only when new testing helpers are added.

### Do Not Touch

- Do not hand-edit generated SDK files in `src/Hexalith.Folders.Client/Generated/`.
- Do not add a second OpenAPI operation or change the Contract Spine unless implementation discovers a blocking mismatch handled through the contract workflow.
- Do not implement ACL grant/revoke mutation, organization ACL mutation, folder creation, folder archive, provider readiness, repository binding, branch policy, workspace preparation, locks, file mutation, commits, context query, audit browsing, CLI, MCP, UI, workers, production Dapr policy, repair workflows, local-only folder mode, webhooks, brownfield adoption, or multi-organization-per-tenant behavior.
- Do not expose membership inventories, display names, emails, repository names, branch names, provider identifiers, raw paths, file contents, diffs, generated context payloads, secrets, tenant configuration payloads, or unauthorized resource existence.
- Do not initialize nested submodules or use recursive submodule commands.

### Testing

- Tests must run offline without provider credentials, tenant seed data, production secrets, running Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, or nested submodules.
- Use xUnit v3 and Shouldly for unit/server tests; use NSubstitute only where a seam requires substitution.
- Include side-effect negative controls for every early denial and stale/unavailable projection path.
- Include response-shape tests aligned with `EffectivePermissions`, `FreshnessMetadata`, and the safe authorization denial components in the Contract Spine.
- Include cross-tenant same-identifier tests and revocation freshness tests.
- Include leakage tests using sentinel forbidden values and assert those values are absent from responses, Problem Details, logs/diagnostic sinks, projections, audit metadata, and test failure surfaces.

### Regression Traps

- Do not compute permissions from request-supplied tenant authority.
- Do not look up folder, ACL, task, workspace, audit, provider, repository, or file resources before tenant and folder authorization pass.
- Do not let stale or unavailable permission projections default to allowed.
- Do not bypass Story 2.4 revocation metadata; revoked actions must disappear or return denied evidence with freshness metadata.
- Do not expose principal display names, user emails, group names, role labels, membership inventories, raw query bodies, provider identifiers, repository names, branch names, paths, file contents, diffs, secrets, or unauthorized resource existence.
- Do not repair projections, refresh Tenants state, or scan aggregates directly as a query fallback.
- Do not expand this story into CLI/MCP/UI parity work; later stories own those adapters.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 2.5`
- `_bmad-output/planning-artifacts/prd.md#Authorization and Tenant Boundary`
- `_bmad-output/planning-artifacts/prd.md#Security and Tenant Isolation`
- `_bmad-output/planning-artifacts/architecture.md#Requirements to Structure Mapping`
- `_bmad-output/planning-artifacts/architecture.md#Integration Points`
- `_bmad-output/project-context.md#Critical Don't-Miss Rules`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`
- `_bmad-output/implementation-artifacts/2-1-stand-up-domain-service-host-with-tenants-integration.md`
- `_bmad-output/implementation-artifacts/2-2-implement-organization-aggregate-acl-baseline.md`
- `_bmad-output/implementation-artifacts/2-3-create-folders-within-a-tenant.md`
- `_bmad-output/implementation-artifacts/2-4-grant-and-revoke-folder-access.md`
- `src/Hexalith.Folders.Testing/Factories/TestFolderContext.cs`
- `src/Hexalith.Folders.Testing/Factories/TestAuthorizationContext.cs`

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-19 | Implemented effective-permission query handler, read-model seam, server route, deterministic permission layering, freshness handling, task narrowing, and focused offline tests. | Codex |
| 2026-05-19 | Applied advanced-elicitation hardening for canonical response ordering, duplicate evidence handling, principal/task-scope cache isolation, and no-leakage stale/unavailable paths. | Codex |
| 2026-05-18 | Applied party-mode review hardening for tenant-before-lookup ordering, deterministic ACL precedence, C7 revocation freshness, safe denial/contract alignment, task-context narrowing, and offline leakage tests. | Codex |
| 2026-05-18 | Created story with effective-permission query contract alignment, fail-closed authorization-before-observation gates, ACL layering, revocation freshness, read-model unavailable behavior, and offline leakage tests. | Codex |

## Party-Mode Review

- ISO date and time: 2026-05-18T13:04:40+02:00
- Selected story key: `2-5-inspect-effective-permissions`
- Command/skill invocation used: `/bmad-party-mode 2-5-inspect-effective-permissions; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor)
- Findings summary:
  - Reviewers agreed the story was directionally correct but needed sharper executable boundaries before development around tenant-before-lookup ordering, externally indistinguishable safe denial, deterministic ACL precedence, C7 revocation freshness, task-context narrowing, and exact Contract Spine response alignment.
  - The main risks were leaking folder or task existence through lookup-before-deny behavior, allowing stale/unavailable read models to fall back to aggregate scans or repair behavior, returning allowed permissions when revocation freshness could not be proven, and letting task context broaden permissions.
  - Contract and test risk centered on mismatched response shapes for `GetEffectivePermissions`, ambiguous result codes for stale/unavailable/not-found cases, and insufficient leak sentinels for denied/stale/unavailable paths.
- Changes applied:
  - Added safe denial and C7 revocation freshness terms tied to existing Contract Spine responses and `FreshnessMetadata`.
  - Tightened acceptance criteria for tenant authority resolution, resource lookup ordering, client-supplied tenant rejection, safe denial envelopes, ACL precedence, revoke-over-grant behavior, read-model stale/unavailable handling, task-context narrowing, and no aggregate fallback/projection repair.
  - Strengthened tasks for exact OpenAPI response alignment, existing `401`/`403`/`404`/`503` response use, deterministic ACL layer tests, C7 freshness cases, task-context matrix coverage, and metadata leakage sentinel assertions across responses, Problem Details, logs, traces, and exception messages.
- Findings deferred:
  - Broader reusable contract-test harness cleanup and expanded cross-story permission scenario catalogs can wait until implementation, provided Story 2.5 includes the inline operation-specific tests.
  - CLI, MCP, UI, provider-specific permission mapping, workspace behavior, repair tooling, and operations-console behavior remain out of scope for later stories.
  - Exact implementation class names and projection adapter boundaries may follow the codebase once Stories 2.1-2.4 land, as long as the story's ordering and contract behavior are preserved.
- Final recommendation: ready-for-dev

## Advanced Elicitation

- ISO date and time: 2026-05-19T02:03:25+02:00
- Selected story key: `2-5-inspect-effective-permissions`
- Command/skill invocation used: `/bmad-advanced-elicitation 2-5-inspect-effective-permissions`
- Batch 1 method names: Security Audit Personas; Failure Mode Analysis; Pre-mortem Analysis; Self-Consistency Validation; Critique and Refine
- Reshuffled Batch 2 method names: Red Team vs Blue Team; Chaos Monkey Scenarios; First Principles Analysis; 5 Whys Deep Dive; Architecture Decision Records
- Findings summary:
  - The story already covered the major party-mode risks, but the effective-permission response still needed sharper canonicalization expectations so duplicate projection rows, duplicate memberships, and projection input ordering cannot produce distinct serialized answers.
  - Security and failure-mode passes identified cache/read-model reuse across principals, tenants, folders, task scopes, revocation watermarks, or consistency classes as a hidden cross-tenant and stale-authorization risk.
  - The stale/unavailable path needed explicit protection against using cached allowed permissions from another principal or task context when the current projection cannot prove freshness.
- Changes applied:
  - Added a canonical permission response term covering deterministic, deduplicated action tokens and principal-source classes without membership counts, display labels, per-source identifiers, or projection-order artifacts.
  - Tightened acceptance criteria for duplicate evidence handling, principal-source leakage boundaries, stale/unavailable no-fallback behavior, and cache isolation across principal and task scope.
  - Added implementation tasks and tests for output canonicalization, stable serialization, duplicate grant/revoke evidence, repeated memberships, unordered projection rows, and cache/read-model memoization scope.
- Findings deferred:
  - The exact contract-defined ordering table for action tokens and source classes can be finalized during implementation from the existing Contract Spine vocabulary and generated model shape.
  - Broader shared authorization-cache infrastructure belongs to implementation once Stories 2.1-2.4 surfaces are available; this story only requires that any local reuse is scoped safely.
- Final recommendation: ready-for-dev

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-19T10:41:53+02:00 - Started implementation; loaded project context, sprint status, and story requirements.
- 2026-05-19T10:47:00+02:00 - Red phase confirmed with missing effective-permissions query/server types.
- 2026-05-19T10:55:04+02:00 - Full regression validation passed with `dotnet test Hexalith.Folders.slnx --no-restore`.

### Completion Notes List

- Story created by `/bmad-create-story 2-5-inspect-effective-permissions` equivalent workflow on 2026-05-18.
- Project context, Epic 2, PRD, architecture, existing Contract Spine effective-permissions operation, Stories 2.1-2.4, testing factories, recent commits, and story-creation lessons were reviewed.
- Ultimate context engine analysis completed - comprehensive developer guide created.
- Implemented `EffectivePermissionsQueryHandler` with tenant-before-read-model ordering, client-controlled tenant mismatch rejection, deterministic lower-snake-case action layering, folder revoke precedence, C7 revocation freshness fail-closed behavior, lifecycle fail-closed handling, and metadata-only task-scope narrowing.
- Added an `IEffectivePermissionsReadModel` seam plus in-memory implementation scoped by authoritative tenant and folder for offline tests and local wiring; no aggregate scan, projection repair, provider, repository, audit, lock, workspace directory, or file fallback was introduced.
- Wired `GET /api/v1/folders/{folderId}/effective-permissions` to the existing Contract Spine operation shape, returning `EffectivePermissions`-compatible JSON for safe results and safe Problem Details for `401`, `403`, `404`, and `503`.
- Added focused unit and server tests covering authorization gate ordering, ACL layering, revocation freshness, stale/unavailable read models, task-scope narrowing, response shape, and leakage sentinels.
- No generated SDK files under `src/Hexalith.Folders.Client/Generated/` were edited.

### File List

- `_bmad-output/implementation-artifacts/2-5-inspect-effective-permissions.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/process-notes/predev-preflight-latest.json` (generated by pre-dev hardening job while story was in progress)
- `_bmad-output/process-notes/predev-preflight-2026-05-19T084153Z.json` (generated by pre-dev hardening job while story was in progress)
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs`
- `src/Hexalith.Folders/Authorization/AuthorizationOrder.cs`
- `src/Hexalith.Folders/Authorization/EffectivePermissionEvidenceRow.cs`
- `src/Hexalith.Folders/Authorization/EffectivePermissionEvidenceSource.cs`
- `src/Hexalith.Folders/Authorization/EffectivePermissionLevel.cs`
- `src/Hexalith.Folders/Authorization/EffectivePermissionPrincipal.cs`
- `src/Hexalith.Folders/Authorization/EffectivePermissionPrincipalKind.cs`
- `src/Hexalith.Folders/Authorization/EffectivePermissionsActionCatalog.cs`
- `src/Hexalith.Folders/Authorization/EffectivePermissionsFolderLifecycleState.cs`
- `src/Hexalith.Folders/Authorization/EffectivePermissionsFreshness.cs`
- `src/Hexalith.Folders/Authorization/EffectivePermissionsQuery.cs`
- `src/Hexalith.Folders/Authorization/EffectivePermissionsQueryHandler.cs`
- `src/Hexalith.Folders/Authorization/EffectivePermissionsQueryResult.cs`
- `src/Hexalith.Folders/Authorization/EffectivePermissionsReadModelRequest.cs`
- `src/Hexalith.Folders/Authorization/EffectivePermissionsReadModelResult.cs`
- `src/Hexalith.Folders/Authorization/EffectivePermissionsReadModelSnapshot.cs`
- `src/Hexalith.Folders/Authorization/EffectivePermissionsReadModelStatus.cs`
- `src/Hexalith.Folders/Authorization/EffectivePermissionsResultCode.cs`
- `src/Hexalith.Folders/Authorization/EffectivePermissionsTaskScope.cs`
- `src/Hexalith.Folders/Authorization/EffectivePermissionsTaskScopeStatus.cs`
- `src/Hexalith.Folders/Authorization/IEffectivePermissionsReadModel.cs`
- `src/Hexalith.Folders/Authorization/InMemoryEffectivePermissionsReadModel.cs`
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`
- `src/Hexalith.Folders.Server/FoldersServerModule.cs`
- `tests/Hexalith.Folders.Tests/Authorization/EffectivePermissionsAuthorizationGateTests.cs`
- `tests/Hexalith.Folders.Tests/Authorization/EffectivePermissionsLayeringTests.cs`
- `tests/Hexalith.Folders.Tests/Authorization/EffectivePermissionsMetadataLeakageTests.cs`
- `tests/Hexalith.Folders.Tests/Authorization/EffectivePermissionsReadModelFreshnessTests.cs`
- `tests/Hexalith.Folders.Tests/Authorization/EffectivePermissionsRevocationFreshnessTests.cs`
- `tests/Hexalith.Folders.Tests/Authorization/EffectivePermissionsTaskScopeTests.cs`
- `tests/Hexalith.Folders.Tests/Authorization/EffectivePermissionsTestSupport.cs`
- `tests/Hexalith.Folders.Server.Tests/EffectivePermissionsEndpointTests.cs`
