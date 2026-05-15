# Story 2.1: Stand up domain service host with Tenants integration

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a platform engineer,
I want the Folders service hosted with Tenants integration and a fail-closed local tenant projection,
so that every folder operation has tenant identity and availability semantics before domain behavior is added.

## Acceptance Criteria

1. Given the scaffolded `Hexalith.Folders.Server` host exists, when the host starts, then it registers the Folders domain-service endpoint surface for EventStore command/query invocation and keeps the external REST surface aligned with the Contract Spine instead of introducing a second behavior path.
2. Given Tenants integration is wired, when the Folders host runs behind a Dapr sidecar, then it subscribes to the Tenants pub/sub component topic `system.tenants.events` and routes those events through a Folders-owned tenant-access projection pipeline.
3. Given Tenants events are received, when tenant lifecycle, membership, role, or `folders.*` configuration events are processed, then `FolderTenantAccessProjection` is updated idempotently with metadata-only state needed for folder authorization.
4. Given tenant projection data is stale, missing, malformed, or unavailable, when a mutating folder operation is authorized, then authorization fails closed before folder, workspace, credential, repository, lock, provider, cache, or audit resources are touched.
5. Given Tenants is unavailable but local projection data is within the documented freshness budget, when a read-only folder diagnostic path is authorized, then the path can use bounded stale projection data while returning explicit freshness metadata; mutations still require fresh authorization or rejection.
6. Given local Aspire topology is started, when AppHost composes EventStore, Tenants, Folders.Server, Folders.Workers, Folders.UI, Keycloak, Redis, and Dapr sidecars, then stable app IDs are used: `eventstore`, `tenants`, `folders`, `folders-workers`, and `folders-ui`.
7. Given tenant identity appears in payloads, routes, query parameters, or client-controlled headers, when the server builds authorization context, then authoritative tenant scope comes only from authentication context and EventStore envelopes; request-supplied tenant IDs are validation inputs, never authority.
8. Given tests run without provider credentials, tenant seed data, running Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, or nested submodules, when unit and smoke tests execute, then Tenants projection, fail-closed authorization, endpoint registration, and Aspire wiring are validated with in-memory fakes or structural assertions.

## Tasks / Subtasks

- [ ] Extend the server host composition without replacing existing scaffold modules. (AC: 1, 7)
  - [ ] Update `src/Hexalith.Folders.Server/Program.cs` to add service defaults, Folders domain services, Tenants client integration, CloudEvents, Dapr subscribe handler, and the Tenants event subscription endpoint.
  - [ ] Keep `src/Hexalith.Folders.Server/FoldersServerModule.cs` as the module registration surface; do not move runtime wiring into generated client or Contracts code.
  - [ ] Preserve root scaffold smoke behavior until a real health/readiness endpoint supersedes it.
- [ ] Add Folders-owned Tenants projection types. (AC: 2, 3, 4, 5)
  - [ ] Create `src/Hexalith.Folders/Projections/TenantAccess/FolderTenantAccessProjection.cs` for tenant status, membership/role evidence, relevant `folders.*` configuration, projection watermark, and freshness metadata.
  - [ ] Create `src/Hexalith.Folders/Projections/TenantAccess/FolderTenantAccessHandler.cs` or equivalent handlers that consume Tenants client events and update the projection idempotently.
  - [ ] Store metadata only: tenant id, principal id, role/group/service-agent ids, event sequence/watermark, timestamps, and non-secret `folders.*` configuration keys.
  - [ ] Reject or ignore non-`folders.*` Tenants configuration keys; do not copy arbitrary Tenants configuration into Folders state.
- [ ] Add tenant authorization and freshness services. (AC: 4, 5, 7)
  - [ ] Create `src/Hexalith.Folders/Authorization/TenantAccessAuthorizer.cs` with explicit outcomes for allowed, denied, stale projection, unavailable projection, unknown tenant, disabled tenant, and malformed evidence.
  - [ ] Ensure mutation checks fail closed on stale, missing, or unavailable projection data.
  - [ ] Ensure read-only diagnostic checks can use bounded stale data only when the response includes projection freshness evidence and the caller is otherwise authorized.
  - [ ] Do not trust tenant ids supplied by request body, route, query, or client-controlled headers.
- [ ] Wire Dapr/Aspire topology using sibling-module patterns. (AC: 2, 6)
  - [ ] Extend `src/Hexalith.Folders.Aspire/FoldersAspireModule.cs` or add an Aspire extension that wires `folders` and `folders-workers` with Dapr sidecars and references the shared Tenants/EventStore state store and pub/sub components.
  - [ ] Update `src/Hexalith.Folders.AppHost/Program.cs` to compose EventStore, Tenants, Folders.Server, Folders.Workers, Folders.UI, Keycloak, Redis/Dapr components, and stable app IDs.
  - [ ] Resolve Dapr access-control files from the AppHost directory, following the Tenants AppHost fallback pattern.
- [ ] Add tests and fixtures for fail-closed behavior. (AC: 2, 3, 4, 5, 6, 8)
  - [ ] Add unit tests under `tests/Hexalith.Folders.Tests` for projection event handling, idempotent replay, ignored non-`folders.*` config, disabled tenant, removed principal, stale projection, and unavailable projection.
  - [ ] Add server tests under `tests/Hexalith.Folders.Server.Tests` proving CloudEvents, subscribe handler, and Tenants event endpoint registration are present without starting external Dapr.
  - [ ] Add AppHost/Aspire structural tests under `tests/Hexalith.Folders.IntegrationTests` or a focused Aspire test project for stable app IDs and component references without requiring live provider credentials.
  - [ ] Add negative tests proving a request-supplied tenant id cannot authorize a mutation when the authenticated/EventStore tenant differs.

## Dev Notes

### Source Context

- Epic 2 objective: tenant administrators and authorized actors can create folders, manage access, inspect effective permissions, archive folders, and receive safe authorization evidence with cross-tenant isolation enforced before any resource access. [Source: `_bmad-output/planning-artifacts/epics.md#Epic 2: Tenant-Scoped Folder Access And Lifecycle`]
- Story 2.1 creates the host and Tenants projection prerequisite before folder ACL and lifecycle behavior arrive in Stories 2.2-2.9. [Source: `_bmad-output/planning-artifacts/epics.md#Story 2.1`]
- Architecture requires Hexalith.Tenants as the source of truth for tenant identity, lifecycle, and membership, consumed through Dapr pub/sub on `system.tenants.events` with a local fail-closed projection. [Source: `_bmad-output/planning-artifacts/architecture.md#Architecture Drivers]
- Tenants integration blocks folder-level authorization tests because fail-closed behavior cannot be validated without the local projection. [Source: `_bmad-output/planning-artifacts/architecture.md#Cross-Component Dependencies`]

### Existing Implementation State

- `src/Hexalith.Folders.Server/Program.cs` is currently a minimal scaffold that maps only `/`.
- `src/Hexalith.Folders.Server/FoldersServerModule.cs` currently exposes scaffold metadata only.
- `src/Hexalith.Folders.AppHost/Program.cs` currently starts `folders` and `folders-ui` only; it does not yet compose EventStore, Tenants, workers, Keycloak, Redis, or shared Dapr components.
- `src/Hexalith.Folders.Aspire/FoldersAspireModule.cs` currently defines only `folders` and `folders-ui` app IDs.
- `src/Hexalith.Folders.Client/Generated/*` is generated from the Contract Spine. Do not hand-edit generated files for this story.

### Required Architecture Patterns

- Keep `Hexalith.Folders.Contracts` behavior-free. Tenants projection, authorization services, Dapr handlers, and domain-service wiring belong in `Hexalith.Folders`, `Hexalith.Folders.Server`, `Hexalith.Folders.Client`, `Hexalith.Folders.Aspire`, or tests, not in Contracts. [Source: `_bmad-output/project-context.md#Critical Implementation Rules`]
- `Hexalith.Folders.Server` owns REST transport plus EventStore `/process` and `/project` domain-service endpoints. External REST remains `/api/v1/...`; internal EventStore invocation remains `/process` and `/project`. [Source: `_bmad-output/project-context.md#Framework-Specific Rules`]
- Authoritative tenant context comes from authentication context and EventStore envelopes. Treat tenant ids in payloads as validation inputs only. [Source: `_bmad-output/project-context.md#Language-Specific Rules`]
- Production authorization order is JWT validation, EventStore claim transform, local tenant-access projection freshness, folder ACL, EventStore validators, then Dapr deny-by-default policy with mTLS. This story implements the host/projection foundation, not the full folder ACL policy from later stories. [Source: `_bmad-output/planning-artifacts/architecture.md#Authentication & Security`]
- Dapr app IDs must remain stable: `eventstore`, `tenants`, `folders`, `folders-ui`, and `folders-workers`. [Source: `_bmad-output/project-context.md#Framework-Specific Rules`]

### Tenants Client Pattern To Reuse

- Reuse the sibling `Hexalith.Tenants.Client` subscription model rather than inventing a Folders-specific pub/sub protocol. The reference maps `/tenants/events`, resolves `HexalithTenantsOptions`, processes a `TenantEventEnvelope`, and calls `.WithTopic(options.PubSubName, options.TopicName)`.
- The Tenants options default topic is `system.tenants.events` and pub/sub component is `pubsub`.
- The Dapr .NET documentation still requires CloudEvents plus Dapr subscribe endpoint registration for pub/sub discovery. For minimal APIs, `[Topic("pubsub", "orders")]` can be used on mapped routes; for endpoint routing, `MapSubscribeHandler()` exposes `/dapr/subscribe`. [Source: Dapr docs via Context7, 2026-05-15]
- The current Aspire integration catalog reports `CommunityToolkit.Aspire.Hosting.Dapr` as the Dapr hosting integration. This repository pins `CommunityToolkit.Aspire.Hosting.Dapr` to `13.0.0`; do not upgrade package versions in this story unless a build failure proves it is necessary. [Source: Aspire integration catalog, 2026-05-15; `Directory.Packages.props`]

### Files To Touch

- `src/Hexalith.Folders.Server/Program.cs`
- `src/Hexalith.Folders.Server/FoldersServerModule.cs`
- `src/Hexalith.Folders/Authorization/TenantAccessAuthorizer.cs`
- `src/Hexalith.Folders/Projections/TenantAccess/FolderTenantAccessProjection.cs`
- `src/Hexalith.Folders/Projections/TenantAccess/FolderTenantAccessHandler.cs`
- `src/Hexalith.Folders.Client` only if the consuming Tenants subscription registration must live in the client package; do not edit generated files.
- `src/Hexalith.Folders.Aspire/FoldersAspireModule.cs`
- `src/Hexalith.Folders.AppHost/Program.cs`
- `tests/Hexalith.Folders.Tests/*`
- `tests/Hexalith.Folders.Server.Tests/*`
- `tests/Hexalith.Folders.IntegrationTests/*`

### Do Not Touch

- Do not implement Organization ACL commands from Story 2.2.
- Do not implement folder creation/lifecycle commands from Stories 2.3-2.8.
- Do not implement provider adapters, repository binding, Git workers, file mutation, commit, context query, CLI, MCP, or UI behavior.
- Do not add repair workflows, local-only folder mode, webhooks, brownfield adoption, multi-organization-per-tenant, or operations-console mutation paths.
- Do not initialize nested submodules or use recursive submodule commands.

### Testing

- Unit tests must run without provider credentials, tenant seed data, production secrets, running Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, or nested submodule initialization. [Source: `_bmad-output/project-context.md#Testing Rules`]
- Use xUnit v3, Shouldly, NSubstitute, and in-memory fakes from `Hexalith.Folders.Testing` when useful.
- Add projection replay tests that prove duplicate Tenants events are idempotent and out-of-order or malformed events fail closed rather than granting access.
- Add authorization tests for `tenant_access_denied`, projection stale/unavailable, disabled tenant, missing principal, removed principal, and payload-tenant mismatch.
- Add structural AppHost tests for stable Dapr app IDs and required component references; avoid tests that require live Dapr sidecars unless they are integration-only and explicitly skipped outside integration runs.

### Regression Traps

- Do not make Tenants availability a silent fallback. Mutations must reject when freshness cannot be proven.
- Do not use request body, route, query, or client headers as tenant authority.
- Do not log or project raw provider tokens, secrets, file contents, diffs, generated context payloads, or unauthorized resource existence.
- Do not create another source of truth for tenant roles inside Folders; the local projection is derived evidence from Hexalith.Tenants.
- Do not use an in-memory Dapr state store for cross-sidecar state in AppHost; sibling Tenants notes explain that per-sidecar in-memory stores break shared command/status behavior.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 2.1`
- `_bmad-output/planning-artifacts/architecture.md#Recommended Project Layout`
- `_bmad-output/planning-artifacts/architecture.md#Authentication & Security`
- `_bmad-output/planning-artifacts/architecture.md#Implementation Sequence`
- `_bmad-output/project-context.md#Critical Implementation Rules`
- `Hexalith.Tenants/src/Hexalith.Tenants.Client/Subscription/TenantEventSubscriptionEndpoints.cs`
- `Hexalith.Tenants/src/Hexalith.Tenants.Client/Registration/TenantServiceCollectionExtensions.cs`
- `Hexalith.Tenants/src/Hexalith.Tenants.Aspire/HexalithTenantsExtensions.cs`
- `Hexalith.Tenants/src/Hexalith.Tenants.AppHost/Program.cs`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

### Completion Notes List

- Story created by `/bmad-create-story 2-1-stand-up-domain-service-host-with-tenants-integration` equivalent workflow on 2026-05-15.
- Project-context, epics, PRD, architecture, current scaffold files, sibling Tenants implementation patterns, Dapr docs, and Aspire integration catalog were reviewed.

### File List

