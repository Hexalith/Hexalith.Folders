# Story 2.7: Inspect folder lifecycle and binding status

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an authorized actor,
I want to inspect folder lifecycle and binding status,
so that I can tell whether a folder is active, archived, unbound, or repository-backed.

## Terms

- Folder lifecycle status means the read-only, metadata-only answer for an authorized folder's current lifecycle state, archive flag, repository binding reference when present, provider binding reference when present, and freshness evidence.
- Binding status means the safe state of the folder's repository backing: unbound, binding requested, bound, failed, unknown provider outcome, or reconciliation required. It must not reveal external repository existence to unauthorized callers.
- Repository-backed means the folder has an authorized repository binding identifier and provider binding reference. It does not mean the query may call providers, dereference repositories, inspect branches, or verify credential material.
- Unbound means the folder is logical-only or has no repository binding yet. It must be represented explicitly rather than by missing, null, or error-prone provider metadata.
- Lifecycle read permission means the caller has passed authoritative tenant context, Story 2.1 tenant-access evidence, Story 2.6 layered authorization, and the folder action token that allows `read_metadata` or the locally equivalent lifecycle-status query permission.
- Safe status evidence means response, audit, diagnostic, log, trace, metric, generated-client exception, and test metadata that includes only allowed identifiers, state labels, freshness, correlation/task IDs, result codes, and sanitized reason categories.
- Freshness metadata means the read-model consistency class, observed timestamp, projection watermark or equivalent version, stale flag, and unavailable/stale reason needed to explain whether the lifecycle and binding answer is current enough for the requested query.
- Status observation means any lifecycle lookup, binding lookup, projection key construction, cache key construction, stream-name construction, timestamp comparison, provider/repository/filesystem probe, audit subject construction, diagnostic subject construction, or branch whose response could disclose whether the folder or binding exists. Authorization and tenant resolution must complete before status observation.
- Lifecycle status snapshot means the tenant, folder, lifecycle projection, binding projection, authorization evidence, and freshness evidence used to answer one request. A successful answer must come from one compatible snapshot and must fail closed when evidence is mixed across tenants, folders, principals, actions, tasks, projection versions, or stale/fresh boundaries.

## Acceptance Criteria

1. Given the Contract Spine declares `GET /api/v1/folders/{folderId}/lifecycle-status` with `operationId: GetFolderLifecycleStatus`, when this story is implemented, then the domain/query handler, server endpoint wiring, read-model contract, SDK consumption path, and tests satisfy that existing operation without adding a second lifecycle-status contract or hand-editing generated SDK files; if generated output is stale, the source contract must be regenerated through the established toolchain rather than manually patched.
2. Given authoritative tenant context and Story 2.1 tenant-access evidence are allowed, when an authorized actor requests folder lifecycle status, then the implementation evaluates layered authorization from Story 2.6 before status observation, including before constructing folder stream names, folder projection keys, repository binding keys, provider binding keys, cache keys, audit subjects, diagnostic scopes, EventStore envelopes, Dapr invocation targets, provider handles, repository references, branch references, workspace paths, or file paths.
3. Given tenant identity appears in route values, query parameters, request headers, forwarded headers, metadata bags, generated client arguments, body values, or client-controlled envelope fields, when the lifecycle-status query context is built, then tenant authority comes only from authentication context or EventStore envelope/projection authority; mismatches return the existing safe denial envelope before any protected resource lookup.
4. Given the caller is unauthenticated, unauthorized, cross-tenant, same-tenant forbidden, missing folder to caller, stale beyond policy, malformed, or blocked by unavailable tenant/folder authorization evidence, when folder lifecycle status is requested, then the response uses existing `401`, `403`, `404`, or `503` safe denial/read-model-unavailable shapes and does not disclose whether the folder, archive state, repository binding, provider binding, workspace, lock, audit record, credential reference, branch/ref policy, file, or external repository exists. `404` must mean safe not-found-to-caller and must not prove actual absence or presence.
5. Given an authorized active folder has no repository binding, when lifecycle status is requested, then the response returns `FolderLifecycleStatus` with the existing Contract Spine fields, `lifecycleState` set to the canonical active/ready equivalent, `archived: false`, no repository binding identifier, no provider binding reference, and freshness metadata proving the read-model version used.
6. Given an authorized active folder has a repository binding, when lifecycle status is requested, then the response returns only safe opaque binding metadata already present in the read model: `repositoryBindingId`, `providerBindingRef`, binding state where locally modeled, freshness metadata, correlation/task IDs, and sanitized result code. It must not return provider tokens, credential material, external repository URLs, repository names, branch names, owner names, provider payloads, or raw capability diagnostics.
7. Given an authorized archived folder is inspected before Story 2.8 archive behavior is implemented, when archive state is not yet supported by production state, then tests and code must represent the unsupported or absent archived state deterministically without treating it as active by default; after Story 2.8 lands, archived folders must return `archived: true`, an archived lifecycle state, metadata-only status evidence, and no mutation affordance.
8. Given lifecycle or binding read models are stale, delayed, missing, unavailable, malformed, replay-conflicting, tenant-mismatched, folder-mismatched, version-inconsistent, or drawn from incompatible projection watermarks, when the query cannot prove a safe answer within policy, then it returns `projection_stale`, `projection_unavailable`, `read_model_unavailable`, `safe_not_found`, or a local equivalent safe outcome rather than falling back to direct aggregate scans, event-stream probing, projection repair, provider calls, repository calls, filesystem reads, audit browsing, or permissive defaults.
9. Given repository binding evidence reports requested, bound, failed, unknown provider outcome, reconciliation required, unbound, unsupported, or unknown enum/string states, when lifecycle status is requested, then each recognized state maps to deterministic metadata-only result fields and safe retry/escalation posture where already modeled; unrecognized states fail closed with a safe unavailable/unknown outcome, and state labels must match the Contract Spine and parity vocabulary rather than UI-only or provider-specific names.
10. Given logs, traces, metrics, audit entries, diagnostics, test output, Problem Details, generated client exceptions, or projection evidence are produced by the lifecycle-status query, when metadata is inspected, then raw auth headers, JWTs, claim bags, provider tokens, credential material, external repository URLs, repository names, branch names, file paths, file contents, diffs, generated context payloads, user emails, group names, role labels, membership inventories, tenant configuration payloads, raw request bodies, raw query bodies, unauthorized resource IDs, provider payloads, and raw exception text are absent.
11. Given SDK, CLI, MCP, and operations-console surfaces later consume lifecycle status, when this story completes, then shared query/result/denial semantics are implemented in domain/server seams and validated through contract-style conformance tests; this story does not implement CLI, MCP, UI screens, provider readiness workflows, or adapter-specific business logic.
12. Given tests run without provider credentials, tenant seed data, production secrets, running Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, provider endpoints, live EventStore servers, or initialized nested submodules, when unit/server/contract tests execute, then authorization-before-observation, safe denial, active/unbound, active/bound, archived-or-unsupported, stale/unavailable, cross-tenant same-identifier, incompatible snapshot, cache-isolation, metadata leakage, timing/discovery-leakage, Contract Spine shape, and generated-client consumption are covered with in-memory fakes and spies.
13. Given this story owns lifecycle and binding status inspection only, when implementation is complete, then it does not implement folder creation, archive mutation, ACL grant/revoke mutation, effective-permissions computation beyond consuming authorization evidence, provider readiness, repository binding mutation, branch/ref policy mutation, workspace preparation, locks, file mutation, commits, context query execution, audit browsing, CLI/MCP/UI commands, workers, production Dapr policy deployment, repair workflows, local-only folder mode, webhooks, brownfield adoption, or multi-organization-per-tenant behavior.

## Denial And Status Truth Table

| Case | Allowed status observation? | Response family | Leakage rule |
|---|---:|---|---|
| Missing/invalid authentication | No | Existing `401` safe denial | No lifecycle, binding, tenant, projection, provider, repository, filesystem, audit, or diagnostic subject metadata. |
| Tenant authority missing, malformed, reserved, client-supplied, or mismatched | No | Existing `403` safe denial | No proof that the folder ID is valid, present, absent, archived, bound, or unbound. |
| Same-tenant caller lacks lifecycle read permission, or folder is not found to caller | No | Existing safe `404` or approved safe denial mapping | Do not use actual resource lookup to distinguish unauthorized from absent; no lifecycle or binding metadata. |
| Authorized read model is stale, unavailable, malformed, or version-inconsistent | Only after authorization | Existing `503` `ReadModelUnavailable` or approved metadata-only stale/unavailable outcome | No aggregate scan, projection repair, provider/repository/filesystem/audit fallback, or permissive active/unbound default. |
| Authorized fresh active/unbound folder | Yes | `200` `FolderLifecycleStatus` | Metadata-only lifecycle, archived flag, freshness, correlation/task IDs, and no binding fields. |
| Authorized fresh active/bound folder | Yes | `200` `FolderLifecycleStatus` | Only opaque binding identifiers and safe binding state already present in the read model; no external repository/provider details. |

## Tasks / Subtasks

- [ ] Implement the lifecycle-status query surface. (AC: 1, 5, 6, 7, 8, 9)
  - [ ] Add a query handler such as `GetFolderLifecycleStatus` under `src/Hexalith.Folders/Queries/Folders/`, `src/Hexalith.Folders/Projections/FolderList/`, or the locally established query convention once current Epic 2 implementation files land.
  - [ ] Wire the existing `GET /api/v1/folders/{folderId}/lifecycle-status` operation in `Hexalith.Folders.Server` only if endpoint wiring is not already present.
  - [ ] Return a metadata-only result compatible with the Contract Spine `FolderLifecycleStatus` schema: required `folderId`, `lifecycleState`, `archived`, and `freshness`; optional `repositoryBindingId` and `providerBindingRef` only when authorized and present.
  - [ ] Preserve existing response shapes and casing exactly: `200` `FolderLifecycleStatus`, `401` `SafeAuthorizationDenial401`, `403` `SafeAuthorizationDenial403`, `404` `SafeAuthorizationDenial404`, and `503` `ReadModelUnavailable`.
  - [ ] Preserve `x-hexalith-read-consistency.class: eventually_consistent` and freshness behavior through `FreshnessMetadata.readConsistency`, `observedAt`, optional `projectionWatermark`, and `stale`.
  - [ ] Do not hand-edit `src/Hexalith.Folders.Client/Generated/*`; regenerate only through the established Contract Spine toolchain if generated output is legitimately stale.
- [ ] Enforce authorization-before-observation gates. (AC: 2, 3, 4)
  - [ ] Resolve authoritative tenant context from authentication context or EventStore envelope/projection authority before constructing any folder, projection, binding, cache, audit, diagnostic, provider, repository, workspace, file, EventStore, or Dapr resource subject.
  - [ ] Consume Story 2.1 tenant-access evidence and Story 2.6 layered authorization before folder lifecycle, binding, archive, repository, provider, audit, workspace, lock, or file evidence is observed.
  - [ ] Treat route/query/header/body/generated-client tenant IDs and forwarded metadata as comparison values only; mismatches return safe denial before lookup.
  - [ ] Add in-memory spies proving denied tenant or folder authorization prevents projection lookup, binding lookup, diagnostics, audit lookup, provider access, repository access, filesystem access, EventStore stream-name construction, and Dapr invocation target construction.
  - [ ] Keep same-tenant unauthorized, cross-tenant, and not-found-to-caller outcomes externally indistinguishable except for allowed canonical status/category differences.
  - [ ] Add explicit denial mapping tests for unauthenticated `401`, tenant/authority denied `403`, safe not-found-to-caller `404`, dependency unavailable `503`, and authorized `200` cases without using resource existence to choose the denial branch.
- [ ] Model lifecycle and binding status deterministically. (AC: 5, 6, 7, 9)
  - [ ] Define or consume one local lifecycle-state mapper aligned to the Contract Spine `LifecycleState` vocabulary.
  - [ ] Define or consume one local repository-binding status mapper aligned to `RepositoryBinding.bindingState` values when binding state is present.
  - [ ] Represent unbound folders explicitly and safely without using provider calls or external repository probes.
  - [ ] Represent archived or not-yet-supported archive behavior explicitly; do not silently classify unknown archive state as active.
  - [ ] Include only opaque binding references, freshness metadata, and sanitized state/reason values in successful responses.
  - [ ] Avoid provider-specific or UI-only labels in domain/server result semantics.
- [ ] Add read-model freshness and unavailable behavior. (AC: 4, 8, 12)
  - [ ] Define local stale, unavailable, malformed, replay-conflicting, tenant-mismatched, and version-inconsistent outcomes if current projection code lacks them.
  - [ ] Treat lifecycle projection, binding projection, authorization evidence, and freshness metadata as one compatible evidence snapshot; reject mixed tenant, folder, principal, action, task, watermark, or stale/fresh evidence rather than stitching a partial success.
  - [ ] Ensure stale/unavailable lifecycle or binding projections fail closed for allowed answers rather than falling back to aggregate scans, event replay, projection repair, compensating writes, provider calls, repository calls, audit queries, or filesystem reads.
  - [ ] Include freshness metadata in success and authorized stale/unavailable outcomes where the Contract Spine allows it.
  - [ ] Test active/unbound, active/bound, archived/supported, archived-not-yet-supported, stale lifecycle projection, unavailable lifecycle projection, stale binding projection, unavailable binding projection, malformed projection, conflicting lifecycle/binding versions, incompatible projection watermarks, and unknown state labels.
- [ ] Add contract and generated-client conformance tests. (AC: 1, 9, 11, 12)
  - [ ] Add server or contract tests proving route, status codes, response casing, nullable optional fields, headers, safe denial envelopes, read-model-unavailable response, and example-compatible JSON match the existing OpenAPI operation.
  - [ ] Add generated-client consumption tests proving `GetFolderLifecycleStatusAsync` and the generated `FolderLifecycleStatus` model can carry active/unbound, active/bound, archived, stale/unavailable, and optional binding-field cases without hand-edited generated code.
  - [ ] Add generated-client conformance coverage for route, method, path parameter, operation ID, denial/status mappings, and response model shape after regeneration from the source Contract Spine.
  - [ ] Ensure parity fixtures keep the `GetFolderLifecycleStatus` row aligned with read consistency, safe denial, operation ID, correlation ID, and audit metadata expectations.
- [ ] Add metadata leakage and regression tests. (AC: 2, 4, 6, 8, 10, 12)
  - [ ] Add unit tests such as `FolderLifecycleStatusAuthorizationGateTests`, `FolderLifecycleStatusProjectionTests`, `FolderLifecycleStatusBindingMetadataTests`, `FolderLifecycleStatusFreshnessTests`, and `FolderLifecycleStatusMetadataLeakageTests`.
  - [ ] Add negative-control tests named like `RejectsBeforeFolderProjectionWhenTenantDenied`, `RejectsBeforeBindingLookupWhenFolderAclDenied`, `DoesNotFallbackToAggregateWhenLifecycleProjectionUnavailable`, and `DoesNotCallProviderForBindingStatus`, or local equivalents.
  - [ ] Add cross-tenant same-identifier tests for folder IDs, repository binding IDs, provider binding refs, task IDs, operation IDs, audit IDs, cache keys, and projection keys; include tenant A and tenant B sharing folder-ID-like values so only auth/EventStore envelope authority selects observable metadata.
  - [ ] Add cache-isolation tests proving authorized lifecycle-status responses are scoped by tenant, principal, action, task/correlation context, folder ID, authorization evidence version, and projection watermark; stale denied or authorized answers must not be replayed across callers or freshness windows.
  - [ ] Add stale/unavailable matrix tests for fresh lifecycle metadata, missing lifecycle metadata, missing binding metadata, stale lifecycle metadata, stale binding metadata, unavailable status source, and no provider/repository/filesystem/audit fallback.
  - [ ] Add timing/discovery negative controls proving denied, not-found-to-caller, and unavailable paths do not branch on actual folder existence, binding existence, provider existence, projection cache hits, or external repository state.
  - [ ] Add leakage sentinels for foreign tenant IDs, unauthorized folder IDs, provider tokens, credential material, external repository URLs, repository names, branch names, file paths, file contents, diffs, generated context payloads, raw claims, membership labels, raw request/query bodies, provider payloads, exception messages, logs, traces, metrics, diagnostics, audit records, Problem Details, and generated client exceptions.

## Dev Notes

### Source Context

- Epic 2 objective: tenant administrators and authorized actors can create folders, manage access, inspect effective permissions, archive folders, and receive safe authorization evidence with cross-tenant isolation enforced before any resource access. [Source: `_bmad-output/planning-artifacts/epics.md#Epic 2: Tenant-Scoped Folder Access And Lifecycle`]
- Story 2.7 foundation: an authorized actor inspects folder lifecycle and binding status so they can tell whether a folder is active, archived, unbound, or repository-backed; provider credentials, tokens, and embedded credential URLs must never be returned. [Source: `_bmad-output/planning-artifacts/epics.md#Story 2.7`]
- PRD FR12 requires authorized folder lifecycle and binding inspection; FR9 and FR10 require safe denial and authorization evidence without exposing protected resources. [Source: `_bmad-output/planning-artifacts/prd.md#Folder Lifecycle`; `_bmad-output/planning-artifacts/prd.md#Authorization and Tenant Boundary`]
- The Contract Spine already defines `GET /api/v1/folders/{folderId}/lifecycle-status`, `operationId: GetFolderLifecycleStatus`, `FolderLifecycleStatus`, optional binding fields, safe denial responses, `ReadModelUnavailable`, and `eventually_consistent` freshness behavior. [Source: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#/paths/~1api~1v1~1folders~1{folderId}~1lifecycle-status`]
- Architecture maps FR11-FR14 folder lifecycle work to `src/Hexalith.Folders/Aggregates/Folder/` and folder contracts, and maps FR12 read visibility to `src/Hexalith.Folders/Projections/FolderList/`. [Source: `_bmad-output/planning-artifacts/architecture.md#Requirements to Structure Mapping`; `_bmad-output/planning-artifacts/architecture.md#Source Tree`]
- Project context requires zero cross-tenant leakage, metadata-only status evidence, tenant-prefixed keys, and authorization before file/workspace/credential/repository/lock/commit/provider/audit access. [Source: `_bmad-output/project-context.md#Critical Don't-Miss Rules`]

### Previous Story Intelligence

- Story 2.1 defines the Folders host, Tenants event subscription, local fail-closed tenant-access projection, and `TenantAccessAuthorizer`. Reuse its tenant evidence and freshness behavior rather than creating a second tenant authority model.
- Story 2.2 defines organization ACL baseline action/principal token handling and safe test fixtures. Lifecycle-status authorization must consume read permission evidence rather than mutating organization ACL state.
- Story 2.3 defines `FolderAggregate`, opaque folder identity, active lifecycle state, and `{managedTenantId}:folders:{folderId}` stream names. Do not derive identity or authority from display names, paths, provider identifiers, repository names, or client-controlled tenant fields.
- Story 2.4 defines folder-level grants/revokes and revocation freshness evidence. Lifecycle status requires current read permission and must fail closed when permission freshness cannot be proven.
- Story 2.5 defines effective-permission inspection, ACL precedence, task-context narrowing, and safe permission evidence. Use its evidence as an input when available; do not duplicate public effective-permission query behavior.
- Story 2.6 defines layered authorization, safe denial mapping, protected-resource-touch semantics, Dapr/EventStore evidence seams, and no-touch spy coverage. This story is a direct read-query consumer of those seams.

### Existing Implementation State

- The current repo has active implementation work for Story 2.2 in progress; read current production and test files before implementation and preserve in-flight patterns.
- `src/Hexalith.Folders/Authorization/TenantAccessAuthorizer.cs` and related `TenantAccess*` types provide fail-closed tenant projection behavior and should remain the tenant-access foundation.
- `src/Hexalith.Folders/Projections/TenantAccess/*` contains local projection store and handler patterns for offline tests.
- `src/Hexalith.Folders.Testing/Factories/TestFolderContext.cs` exposes safe test stream names using `{ManagedTenantId}:folders:{FolderId}` and organization stream names.
- `src/Hexalith.Folders.Testing/Factories/TestAuthorizationContext.cs` and `OrganizationAclTestDataFactory.cs` provide safe authorization and ACL fixture patterns; extend only when reusable lifecycle-status builders are needed.
- `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs` already includes generated `GetFolderLifecycleStatusAsync` and `FolderLifecycleStatus`; generated files must not be hand-edited.

### Required Architecture Patterns

- Keep `Hexalith.Folders.Contracts` behavior-free. Lifecycle-status computation, authorization order, projection freshness policy, and safe-denial logic belong in `Hexalith.Folders`, `Hexalith.Folders.Server`, and tests.
- Keep the query read-only. It may read authorized projections after authorization, but it must not mutate aggregates, append events, repair projections, acquire locks, call providers, inspect repositories, read files, or browse audit payloads as a fallback.
- Authorization order is authoritative tenant context, Story 2.1 tenant access, Story 2.6 layered authorization, folder read permission/effective-permission evidence, then protected lifecycle/binding projection lookup.
- Treat lifecycle, binding, authorization, and freshness inputs as one request-scoped decision snapshot. Do not combine a fresh lifecycle projection with stale binding evidence, a tenant-authorized result with a different principal/action/task, or one folder's binding metadata with another folder's lifecycle state.
- Folder IDs, repository binding IDs, provider binding refs, task IDs, operation IDs, audit IDs, stream names, cache keys, projection keys, and diagnostic subjects must be tenant scoped and unavailable until authorization permits their construction.
- Successful responses are metadata-only. Return opaque identifiers and state/freshness evidence only; never return credential material, external repository URLs, repository names, branch names, provider payloads, file paths, or file contents.
- Use C# file-scoped namespaces, nullable-aware records/classes, one public type per file, PascalCase types/members, camelCase locals/parameters, and async APIs for I/O boundaries.

### Files To Touch

- `src/Hexalith.Folders/Projections/FolderList/*` or the locally established lifecycle projection folder.
- `src/Hexalith.Folders/Queries/Folders/*GetFolderLifecycleStatus*` if the repo adopts a query-folder convention.
- `src/Hexalith.Folders/Aggregates/Folder/*` only for consuming existing lifecycle state/events or adding behavior required by the query read model; do not add archive mutation here unless Story 2.8 is being implemented separately.
- `src/Hexalith.Folders/Authorization/*` only to consume existing tenant/layered/folder-read authorization seams or add narrow lifecycle-status evidence adapters.
- `src/Hexalith.Folders.Server/Endpoints/*` or local Minimal API modules only to wire the existing Contract Spine route.
- `src/Hexalith.Folders.Testing/Factories/*` only for reusable safe lifecycle/binding status builders.
- `tests/Hexalith.Folders.Tests/Projections/FolderList/*` or local equivalent projection tests.
- `tests/Hexalith.Folders.Tests/Authorization/*FolderLifecycleStatus*Tests.cs`
- `tests/Hexalith.Folders.Server.Tests/*FolderLifecycleStatus*Tests.cs`
- `tests/Hexalith.Folders.Client.Tests/*` only for generated-client consumption coverage.
- `tests/Hexalith.Folders.Testing.Tests/*` only when new testing helpers are introduced.

### Do Not Touch

- Do not hand-edit generated SDK files in `src/Hexalith.Folders.Client/Generated/`.
- Do not add a second OpenAPI operation or change the Contract Spine unless implementation discovers a blocking mismatch handled through the contract workflow.
- Do not implement folder creation, archive mutation, ACL grant/revoke mutation, effective-permissions public query behavior, provider readiness, repository binding mutation, branch/ref policy mutation, workspace preparation, locks, file mutation, commits, context query execution, audit browsing, CLI, MCP, UI screens, workers, production Dapr policy deployment, repair workflows, local-only folder mode, webhooks, brownfield adoption, or multi-organization-per-tenant behavior.
- Do not call GitHub, Forgejo, filesystem working copies, provider APIs, repository APIs, audit browsing APIs, or EventStore aggregate scans as a lifecycle-status fallback.
- Do not expose provider tokens, credential material, external repository URLs, repository names, branch names, folder display names when unauthorized, file paths, file contents, diffs, generated context payloads, raw claims, raw request/query bodies, membership inventories, or unauthorized resource existence.
- Do not initialize nested submodules or use recursive submodule commands.

### Testing

- Tests must run offline without provider credentials, tenant seed data, production secrets, running Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, provider endpoints, live EventStore servers, or nested submodules.
- Use xUnit v3 and Shouldly for unit/server/client tests; use NSubstitute only where an actual seam needs substitution.
- Include side-effect spies for every early denial and stale/unavailable projection path.
- Include response-shape tests aligned with `FolderLifecycleStatus`, `FreshnessMetadata`, safe authorization denial components, and `ReadModelUnavailable` in the Contract Spine.
- Include generated-client consumption tests for optional binding fields and freshness metadata without editing generated code.
- Include cross-tenant same-identifier tests and leakage tests with forbidden sentinels across responses, Problem Details, logs, traces, metrics, diagnostics, audit metadata, projections, generated client exceptions, and test output.

### Regression Traps

- Do not compute lifecycle or binding status from request-supplied tenant authority.
- Do not construct stream names, projection keys, repository binding keys, provider binding keys, cache keys, audit keys, diagnostic subjects, EventStore envelopes, Dapr targets, provider handles, repository refs, workspace paths, or file paths before authorization allows that layer.
- Do not return `archived: false` merely because archive support is not implemented yet; explicitly test unsupported or absent archive state behavior until Story 2.8 lands.
- Do not represent unbound folders as errors when the authorized folder exists and is legitimately unbound.
- Do not let stale or unavailable lifecycle/binding projections default to active, unbound, or allowed.
- Do not coerce unknown lifecycle or binding labels into active, unbound, bound, or archived states; treat unknown labels as safe unavailable/unknown evidence until the Contract Spine vocabulary is updated deliberately.
- Do not reuse cached lifecycle-status answers across tenant, principal, action, task/correlation, authorization-evidence version, folder ID, projection watermark, or freshness-policy boundaries.
- Do not let denial latency, diagnostics, cache hit/miss behavior, or generated-client exception detail reveal whether a folder, binding, provider, repository, or projection row exists.
- Do not expose provider identifiers, repository URLs, repository names, branch names, credential references beyond safe opaque identifiers, file paths, raw provider payloads, raw exceptions, or unauthorized resource existence.
- Do not expand this story into repository binding mutation, provider readiness, workspace lifecycle, archive mutation, or UI/adapter rollout.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 2.7`
- `_bmad-output/planning-artifacts/prd.md#Folder Lifecycle`
- `_bmad-output/planning-artifacts/prd.md#Authorization and Tenant Boundary`
- `_bmad-output/planning-artifacts/prd.md#Security and Tenant Isolation`
- `_bmad-output/planning-artifacts/architecture.md#Requirements to Structure Mapping`
- `_bmad-output/planning-artifacts/architecture.md#Source Tree`
- `_bmad-output/project-context.md#Critical Don't-Miss Rules`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`
- `tests/fixtures/parity-contract.yaml`
- `_bmad-output/implementation-artifacts/2-1-stand-up-domain-service-host-with-tenants-integration.md`
- `_bmad-output/implementation-artifacts/2-2-implement-organization-aggregate-acl-baseline.md`
- `_bmad-output/implementation-artifacts/2-3-create-folders-within-a-tenant.md`
- `_bmad-output/implementation-artifacts/2-4-grant-and-revoke-folder-access.md`
- `_bmad-output/implementation-artifacts/2-5-inspect-effective-permissions.md`
- `_bmad-output/implementation-artifacts/2-6-enforce-layered-authorization-with-safe-denials.md`
- `src/Hexalith.Folders/Authorization/TenantAccessAuthorizer.cs`
- `src/Hexalith.Folders.Testing/Factories/TestFolderContext.cs`
- `src/Hexalith.Folders.Testing/Factories/TestAuthorizationContext.cs`

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-18 | Created story with lifecycle-status contract alignment, authorization-before-observation gates, safe binding metadata, freshness handling, and offline leakage tests. | Codex |
| 2026-05-18 | Applied party-mode review hardening for status-observation boundaries, denial/status truth table, generated-client conformance, cross-tenant same-identifier tests, and stale/unavailable no-fallback coverage. | Codex |
| 2026-05-19 | Applied advanced-elicitation hardening for compatible evidence snapshots, unknown state fail-closed behavior, cache isolation, and timing/discovery leakage controls. | Codex |

## Party-Mode Review

- ISO date and time: 2026-05-18T23:16:45+02:00
- Selected story key: `2-7-inspect-folder-lifecycle-and-binding-status`
- Command/skill invocation used: `/bmad-party-mode 2-7-inspect-folder-lifecycle-and-binding-status; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), John (Product Manager)
- Findings summary:
  - Reviewers agreed the story was in the right product and architecture scope, but implementers needed sharper language that lifecycle status is not a discovery surface.
  - The main risks were status lookup before authorization, using actual resource existence to choose `403` versus `404`, treating missing or stale metadata as active/unbound, probing providers or repositories as helpful fallbacks, and letting generated SDK files be patched by hand.
  - Test coverage needed a denial/status truth table, no-touch spies, cross-tenant same-identifier cases, stale/unavailable matrices, and generated-client conformance checks tied to the existing Contract Spine route and operation ID.
- Changes applied:
  - Added a `Status observation` term covering lifecycle/binding lookup, protected key construction, timestamp comparison, provider/repository/filesystem probing, and audit/diagnostic subject construction.
  - Tightened AC1, AC2, and AC4 for regeneration-only SDK updates, authorization-before-status-observation, and safe `404` semantics that do not prove resource absence or presence.
  - Added a denial/status truth table for `401`, `403`, safe `404`, `503`, authorized active/unbound, and authorized active/bound cases.
  - Expanded tasks for explicit denial mapping tests, generated-client conformance, cross-tenant same-identifier coverage, and stale/unavailable no-fallback matrices.
- Findings deferred:
  - UX copy, dashboard display, CLI/MCP exposure, provider diagnostics, archive mutation behavior, audit enrichment, and remediation workflows remain out of scope for later stories.
  - Broader lifecycle vocabulary cleanup is deferred unless implementation finds the existing Contract Spine vocabulary cannot represent the required metadata-only states.
  - Full provider, Keycloak, Dapr, Redis, GitHub, Forgejo, and live EventStore integration coverage remains deferred because this story requires offline deterministic tests.
- Final recommendation: ready-for-dev

## Advanced Elicitation

- ISO date and time: 2026-05-19T04:01:58+02:00
- Selected story key: `2-7-inspect-folder-lifecycle-and-binding-status`
- Command/skill invocation used: `/bmad-advanced-elicitation 2-7-inspect-folder-lifecycle-and-binding-status`
- Batch 1 method names: Security Audit Personas, Failure Mode Analysis, Pre-mortem Analysis, Self-Consistency Validation, Critique and Refine
- Reshuffled Batch 2 method names: Red Team vs Blue Team, Chaos Monkey Scenarios, First Principles Analysis, 5 Whys Deep Dive, Architecture Decision Records
- Findings summary:
  - Security and failure-mode review found that the story already blocks provider/repository fallback, but needed sharper safeguards against stitching lifecycle, binding, authorization, and freshness evidence from incompatible snapshots.
  - Pre-mortem and self-consistency review identified unknown lifecycle/binding labels, stale cache reuse, and generated-client exception detail as likely implementation shortcuts that could turn safe status inspection into discovery.
  - Red-team and chaos review highlighted timing, diagnostics, cache hit/miss, and projection-row existence side channels that must be covered with no-touch and negative-control tests before `bmad-dev-story`.
- Changes applied:
  - Added the `Lifecycle status snapshot` term and tightened AC8, AC9, and AC12 for incompatible projection watermarks, unknown state labels, cache isolation, timing/discovery leakage, and generated-client-safe behavior.
  - Expanded freshness/unavailable tasks to require compatible evidence snapshots and tests for incompatible watermarks and unknown labels.
  - Expanded metadata-leakage tests to include cache isolation and timing/discovery negative controls.
  - Added developer guardrails and regression traps for snapshot compatibility, unknown enum fail-closed behavior, cache scoping, denial latency, diagnostics, and generated-client exception detail.
- Findings deferred:
  - No product-scope or architecture-policy changes were applied.
  - Broader lifecycle vocabulary changes remain deferred until implementation proves the existing Contract Spine cannot represent required metadata-only states.
  - CLI, MCP, UI, provider readiness, repository binding mutation, archive mutation, and remediation workflows remain out of scope for later stories.
- Final recommendation: ready-for-dev

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

### Completion Notes List

- Story created by `/bmad-create-story 2-7-inspect-folder-lifecycle-and-binding-status` equivalent workflow on 2026-05-18.
- Project context, Epic 2, PRD, architecture, Contract Spine lifecycle-status operation, Stories 2.1-2.6, current tenant authorization/projection/test factory files, recent commits, and story-creation lessons were reviewed.
- Ultimate context engine analysis completed - comprehensive developer guide created.

### File List
