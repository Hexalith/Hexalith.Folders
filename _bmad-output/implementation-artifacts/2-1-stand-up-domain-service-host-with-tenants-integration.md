# Story 2.1: Stand up domain service host with Tenants integration

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a platform engineer,
I want the Folders service hosted with Tenants integration and a fail-closed local tenant projection,
so that every folder operation has tenant identity and availability semantics before domain behavior is added.

## Acceptance Criteria

1. Given the scaffolded `Hexalith.Folders.Server` host exists, when the host starts, then it registers the Folders EventStore domain-service surfaces for `/process` command invocation and `/project` query invocation, keeps external REST routes aligned with the Contract Spine, and does not introduce a second behavior path.
2. Given Tenants integration is wired, when the Folders host runs behind a Dapr sidecar, then it calls `UseCloudEvents()`, exposes `MapSubscribeHandler()`, maps the Tenants subscription route `/tenants/events`, subscribes through pub/sub component `pubsub` to topic `system.tenants.events`, and routes those events through a Folders-owned tenant-access projection pipeline.
3. Given Tenants events are received, when `TenantCreated`, `TenantUpdated`, `TenantDisabled`, `TenantEnabled`, `UserAddedToTenant`, `UserRemovedFromTenant`, `UserRoleChanged`, `TenantConfigurationSet`, or `TenantConfigurationRemoved` envelopes are processed, then `FolderTenantAccessProjection` is updated idempotently using the envelope `MessageId` and per-tenant `SequenceNumber` with metadata-only state needed for folder authorization; duplicate message IDs with divergent envelope metadata are recorded as replay conflicts and must not advance the projection watermark.
4. Given tenant projection data is stale, missing, malformed, replay-conflicting, from a future timestamp, or unavailable, when a mutating folder operation is authorized through REST or EventStore `/process`, then authorization fails closed before folder, workspace, credential, repository, lock, provider, cache, or audit resources are touched.
5. Given Tenants is unavailable but local projection data is within the documented freshness budget, when a read-only folder diagnostic path from the Contract Spine is authorized, then the path can use bounded stale projection data while returning explicit freshness metadata (`projectionWatermark`, `lastEventTimestamp`, `projectionAge`, `freshnessStatus`, and source) without exposing membership lists, raw projection payloads, or unauthorized resource existence; mutations still require fresh authorization or rejection.
6. Given local Aspire topology is started, when AppHost composes EventStore, Tenants, Folders.Server, Folders.Workers, Folders.UI, Keycloak, Redis, and Dapr sidecars, then stable app IDs are used: `eventstore`, `tenants`, `folders`, `folders-workers`, and `folders-ui`.
7. Given tenant identity appears in payloads, routes, query parameters, or client-controlled headers, when the server builds authorization context, then authoritative tenant scope comes only from authentication context and EventStore envelopes; route, body, query, and client-controlled header tenant IDs are validation inputs only and must never establish authorization context.
8. Given tests run without provider credentials, tenant seed data, running Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, or nested submodules, when unit and smoke tests execute, then Tenants projection, fail-closed authorization, endpoint registration, and Aspire wiring are validated with in-memory fakes, a fake clock, a fake projection store, or structural assertions.
9. Given the tenant-access authorizer evaluates projection state, when it returns a result, then outcomes are bounded to allowed, denied, stale_projection, unavailable_projection, unknown_tenant, disabled_tenant, malformed_evidence, tenant_mismatch, missing_authoritative_tenant, and replay_conflict, and diagnostics use stable codes plus metadata-only fields rather than user-facing English parsing.
10. Given `TenantConfigurationSet` or `TenantConfigurationRemoved` events contain configuration keys, when the Folders projection handles them, then it stores only non-secret `folders.*` key names and coarse access flags needed for tenant access, removes or tombstones matching keys on configuration-removal events, and ignores or rejects all other keys without copying arbitrary Tenants configuration values.

## Tasks / Subtasks

- [ ] Extend the server host composition without replacing existing scaffold modules. (AC: 1, 7)
  - [ ] Update `src/Hexalith.Folders.Server/Program.cs` to add service defaults, Folders domain services, Tenants client integration, CloudEvents, Dapr subscribe handler, and the Tenants event subscription endpoint.
  - [ ] Ensure the route and subscription shape matches the sibling Tenants client pattern: `/tenants/events`, `pubsub`, `system.tenants.events`, `UseCloudEvents()`, `MapSubscribeHandler()`, and `WithTopic(options.PubSubName, options.TopicName)`.
  - [ ] Keep `src/Hexalith.Folders.Server/FoldersServerModule.cs` as the module registration surface; do not move runtime wiring into generated client or Contracts code.
  - [ ] Preserve root scaffold smoke behavior until a real health/readiness endpoint supersedes it.
- [ ] Add Folders-owned Tenants projection types. (AC: 2, 3, 4, 5)
  - [ ] Create `src/Hexalith.Folders/Projections/TenantAccess/FolderTenantAccessProjection.cs` for tenant status, membership/role evidence, relevant `folders.*` configuration, projection watermark, and freshness metadata.
  - [ ] Create `src/Hexalith.Folders/Projections/TenantAccess/FolderTenantAccessHandler.cs` or equivalent handlers that consume Tenants client events and update the projection idempotently.
  - [ ] Handle only `TenantCreated`, `TenantUpdated`, `TenantDisabled`, `TenantEnabled`, `UserAddedToTenant`, `UserRemovedFromTenant`, `UserRoleChanged`, `TenantConfigurationSet`, and `TenantConfigurationRemoved`; unknown event types must not grant access.
  - [ ] Deduplicate by `TenantEventEnvelope.MessageId`, apply per-tenant `SequenceNumber` monotonically, and treat replay conflicts, malformed payloads, missing tenant IDs, or future timestamps as fail-closed projection evidence.
  - [ ] Persist deduplication keys, sequence evidence, replay-conflict markers, and projection watermarks through a projection-store abstraction; if that store is unavailable, authorizer calls must return `unavailable_projection` rather than falling back to in-memory success.
  - [ ] Store metadata only: tenant id, principal id, role/group/service-agent ids, event sequence/watermark, timestamps, and non-secret `folders.*` configuration keys.
  - [ ] Reject or ignore non-`folders.*` Tenants configuration keys; do not copy arbitrary Tenants configuration into Folders state.
  - [ ] Apply `TenantConfigurationRemoved` as an explicit remove/tombstone operation for previously projected `folders.*` configuration keys so stale configuration cannot continue to authorize access.
- [ ] Add tenant authorization and freshness services. (AC: 4, 5, 7)
  - [ ] Create `src/Hexalith.Folders/Authorization/TenantAccessAuthorizer.cs` with explicit outcomes for allowed, denied, stale projection, unavailable projection, unknown tenant, disabled tenant, and malformed evidence.
  - [ ] Include explicit outcomes for tenant mismatch, missing authoritative tenant, replay conflict, and future projection timestamps.
  - [ ] Define a configurable projection freshness budget with a test default of five minutes, using an injectable UTC clock; missing, future, expired, malformed, or unavailable projection timestamps fail closed for mutations.
  - [ ] Ensure mutation checks fail closed on stale, missing, malformed, replay-conflicting, future-dated, or unavailable projection data.
  - [ ] Ensure read-only diagnostic checks can use bounded stale data only when the response includes projection freshness evidence and the caller is otherwise authorized.
  - [ ] Keep diagnostic responses metadata-only: expose stable result codes and freshness fields, but not raw Tenants payloads, membership inventories, role lists, configuration values, or whether an unauthorized folder/resource exists.
  - [ ] Do not create a new public diagnostic route outside the Contract Spine; if no read-only diagnostic operation exists at implementation time, test the authorizer response shape instead.
  - [ ] Do not trust tenant ids supplied by request body, route, query, or client-controlled headers; compare them only against authentication or EventStore envelope tenant context.
- [ ] Wire Dapr/Aspire topology using sibling-module patterns. (AC: 2, 6)
  - [ ] Extend `src/Hexalith.Folders.Aspire/FoldersAspireModule.cs` or add an Aspire extension that wires `folders` and `folders-workers` with Dapr sidecars and references the shared Tenants/EventStore state store and pub/sub components.
  - [ ] Update `src/Hexalith.Folders.AppHost/Program.cs` to compose EventStore, Tenants, Folders.Server, Folders.Workers, Folders.UI, Keycloak, Redis/Dapr components, and stable app IDs.
  - [ ] Resolve Dapr access-control files from the AppHost directory, following the Tenants AppHost fallback pattern.
- [ ] Add tests and fixtures for fail-closed behavior. (AC: 2, 3, 4, 5, 6, 8)
  - [ ] Add unit tests under `tests/Hexalith.Folders.Tests` for projection event handling, idempotent replay, ignored non-`folders.*` config, disabled tenant, removed principal, stale projection, and unavailable projection.
  - [ ] Add unit tests for malformed events, missing authoritative tenant context, future timestamps, replay conflicts, duplicate message IDs with divergent metadata, tenant mismatches between request and authoritative context, configuration removals, and bounded stale diagnostic reads.
  - [ ] Add server tests under `tests/Hexalith.Folders.Server.Tests` proving CloudEvents, subscribe handler, Tenants event endpoint registration, topic/pubsub names, and unknown-event behavior are present without starting external Dapr.
  - [ ] Assert endpoint registration through route metadata, endpoint data sources, or equivalent structural surfaces rather than live Dapr discovery calls.
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
- Fail-closed tenant access is the default for every mutation path in this story: REST mutations, EventStore `/process`, malformed authentication context, missing EventStore envelope tenant context, projection store outage, stale projection, replay conflict, disabled tenant, unknown tenant, or tenant mismatch.
- `FolderTenantAccessProjection` is coarse tenant-access evidence only. It must not implement folder ACL semantics, folder lifecycle state, provider readiness, repository binding, workspace behavior, or repair workflows.

### Tenants Client Pattern To Reuse

- Reuse the sibling `Hexalith.Tenants.Client` subscription model rather than inventing a Folders-specific pub/sub protocol. The reference maps `/tenants/events`, resolves `HexalithTenantsOptions`, processes a `TenantEventEnvelope`, and calls `.WithTopic(options.PubSubName, options.TopicName)`.
- The Tenants options default topic is `system.tenants.events` and pub/sub component is `pubsub`.
- The Tenants envelope exposes `MessageId`, `AggregateId`, `TenantId`, `EventTypeName`, `SequenceNumber`, `Timestamp`, `CorrelationId`, `SerializationFormat`, and `Payload`; Folders should use only metadata-safe fields plus deserialized tenant events needed for coarse access.
- The event registry to handle in this story is `TenantCreated`, `TenantUpdated`, `TenantDisabled`, `TenantEnabled`, `UserAddedToTenant`, `UserRemovedFromTenant`, `UserRoleChanged`, `TenantConfigurationSet`, and `TenantConfigurationRemoved`. `GlobalAdministratorSet` and `GlobalAdministratorRemoved` are out of scope unless architecture later requires Folders service-agent access through them.
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
- Do not implement folder ACL semantics, membership policy beyond coarse tenant-access evidence, or tenant administration behavior.
- Do not implement folder creation/lifecycle commands from Stories 2.3-2.8.
- Do not implement provider adapters, repository binding, Git workers, file mutation, commit, context query, CLI, MCP, or UI behavior.
- Do not add repair workflows, local-only folder mode, webhooks, brownfield adoption, multi-organization-per-tenant, or operations-console mutation paths.
- Do not initialize nested submodules or use recursive submodule commands.

### Testing

- Unit tests must run without provider credentials, tenant seed data, production secrets, running Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, or nested submodule initialization. [Source: `_bmad-output/project-context.md#Testing Rules`]
- Use xUnit v3, Shouldly, NSubstitute, and in-memory fakes from `Hexalith.Folders.Testing` when useful.
- Add projection replay tests that prove duplicate Tenants events are idempotent and out-of-order or malformed events fail closed rather than granting access.
- Add authorization tests for `tenant_access_denied`, projection stale/unavailable, disabled tenant, missing principal, removed principal, and payload-tenant mismatch.
- Add endpoint tests for `/process` mutation fail-closed behavior and `/project` or Contract Spine read-only diagnostic freshness metadata where such a diagnostic operation exists.
- Add Dapr subscription tests using structural assertions only; do not require live Dapr, Redis, Keycloak, Tenants service, provider credentials, or network calls.
- Add structural AppHost tests for stable Dapr app IDs and required component references; avoid tests that require live Dapr sidecars unless they are integration-only and explicitly skipped outside integration runs.

### Regression Traps

- Do not make Tenants availability a silent fallback. Mutations must reject when freshness cannot be proven.
- Do not use request body, route, query, or client headers as tenant authority.
- Do not parse localized/user-facing diagnostic sentences in tests; assert stable result codes, enum values, metadata fields, and repository-owned route names.
- Do not log or project raw provider tokens, secrets, file contents, diffs, generated context payloads, or unauthorized resource existence.
- Do not let duplicate `MessageId` values with divergent tenant, sequence, timestamp, event type, or payload metadata advance the local projection.
- Do not expose raw membership, role, or configuration values through stale diagnostic responses; diagnostics should prove freshness and status only.
- Do not create another source of truth for tenant roles inside Folders; the local projection is derived evidence from Hexalith.Tenants.
- Do not use an in-memory Dapr state store for cross-sidecar state in AppHost; sibling Tenants notes explain that per-sidecar in-memory stores break shared command/status behavior.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 2.1`
- `_bmad-output/planning-artifacts/architecture.md#Recommended Project Layout`
- `_bmad-output/planning-artifacts/architecture.md#Authentication & Security`
- `_bmad-output/planning-artifacts/architecture.md#Implementation Sequence`
- `_bmad-output/project-context.md#Critical Implementation Rules`
- `Hexalith.Tenants/src/Hexalith.Tenants.Client/Subscription/TenantEventSubscriptionEndpoints.cs`
- `Hexalith.Tenants/src/Hexalith.Tenants.Client/Subscription/TenantEventEnvelope.cs`
- `Hexalith.Tenants/src/Hexalith.Tenants.Client/Subscription/TenantEventProcessor.cs`
- `Hexalith.Tenants/src/Hexalith.Tenants.Client/Registration/TenantServiceCollectionExtensions.cs`
- `Hexalith.Tenants/src/Hexalith.Tenants.Aspire/HexalithTenantsExtensions.cs`
- `Hexalith.Tenants/src/Hexalith.Tenants.AppHost/Program.cs`

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-17 | Applied advanced-elicitation hardening for replay conflicts, projection-store failure semantics, diagnostic leakage boundaries, configuration removals, and structural endpoint tests. | Codex |
| 2026-05-15 | Applied party-mode review hardening for freshness semantics, Tenants event mapping, Dapr subscription shape, tenant authority, fail-closed outcomes, and offline tests. | Codex |

## Party-Mode Review

- Date/time: 2026-05-15T14:22:02Z
- Selected story key: `2-1-stand-up-domain-service-host-with-tenants-integration`
- Command/skill invocation used: `/bmad-party-mode 2-1-stand-up-domain-service-host-with-tenants-integration; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), Paige (Technical Writer)
- Findings summary:
  - Reviewers agreed the story was directionally sound but needed objective freshness semantics, pinned Dapr subscription shape, concrete Tenants event mapping, explicit projection idempotency/replay behavior, bounded authorizer outcomes, and stronger negative test guidance before development.
  - The main implementation risks were accidentally trusting request-supplied tenant IDs, treating Tenants unavailability as a permissive fallback, pulling Story 2.2 ACL or folder lifecycle policy forward, or making diagnostic stale reads leak tenant membership/resource evidence.
- Changes applied:
  - Pinned `/process`, `/project`, `/tenants/events`, `pubsub`, `system.tenants.events`, `UseCloudEvents()`, and `MapSubscribeHandler()` expectations.
  - Added accepted Tenants event names, envelope metadata usage, `MessageId` deduplication, per-tenant `SequenceNumber` replay semantics, and `folders.*` configuration boundaries.
  - Added a configurable projection freshness budget with a five-minute test default, injectable UTC clock, freshness metadata fields, and fail-closed outcomes for stale, missing, malformed, unavailable, future-dated, replay-conflicting, mismatched, or unknown tenant evidence.
  - Strengthened tenant-authority wording so route, body, query, and client-controlled header tenant IDs are validation-only.
  - Added offline test guidance for malformed CloudEvents, missing authoritative tenant context, endpoint surfaces, Dapr subscription shape, structural Aspire assertions, and bounded stale diagnostic reads.
- Findings deferred:
  - Exact production freshness budget, persistence store choice for projection/deduplication, final diagnostic endpoint availability, and service-agent/global-admin behavior remain implementation or later architecture decisions.
  - Folder ACL semantics, folder lifecycle behavior, provider integrations, repair workflows, CLI/MCP/UI behavior, and Story 2.2 policy decisions remain out of scope.
- Final recommendation: ready-for-dev after applied story clarification pass.

## Advanced Elicitation

- Date/time: 2026-05-17T09:27:11Z
- Selected story key: `2-1-stand-up-domain-service-host-with-tenants-integration`
- Command/skill invocation used: `/bmad-advanced-elicitation 2-1-stand-up-domain-service-host-with-tenants-integration`
- Batch 1 method names: Red Team vs Blue Team; Failure Mode Analysis; Security Audit Personas; Self-Consistency Validation; Pre-mortem Analysis
- Reshuffled Batch 2 method names: Architecture Decision Records; Graph of Thoughts; User Persona Focus Group; Chaos Monkey Scenarios; Critique and Refine
- Findings summary:
  - The highest-risk ambiguity was whether duplicate `MessageId` handling, projection-store failures, and configuration removals could accidentally leave stale positive tenant evidence in place.
  - Diagnostic reads needed an explicit leakage boundary so bounded stale projection use cannot reveal membership, role, configuration, or resource-existence details.
  - Endpoint and Dapr subscription verification needed to remain structural and offline to preserve the story's no-live-Dapr test constraint.
- Changes applied:
  - Added replay-conflict behavior for duplicate message IDs with divergent envelope metadata and required conflict evidence not to advance projection watermarks.
  - Required deduplication keys, sequence evidence, replay-conflict markers, and watermarks to go through a projection-store abstraction, with store unavailability returning `unavailable_projection`.
  - Tightened `TenantConfigurationRemoved` handling so removed `folders.*` keys are deleted or tombstoned and stale configuration cannot continue authorizing access.
  - Clarified metadata-only diagnostic response limits and added tests for duplicate divergent messages, configuration removals, and structural endpoint registration.
- Findings deferred:
  - Exact production freshness budget, durable projection-store implementation choice, final diagnostic operation availability, and service-agent/global-admin integration remain implementation or later architecture decisions.
  - Folder ACL semantics, lifecycle commands, provider integrations, repair workflows, CLI/MCP/UI behavior, and Story 2.2 policy remain out of scope.
- Final recommendation: ready-for-dev after applied advanced-elicitation hardening.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

### Completion Notes List

- Story created by `/bmad-create-story 2-1-stand-up-domain-service-host-with-tenants-integration` equivalent workflow on 2026-05-15.
- Project-context, epics, PRD, architecture, current scaffold files, sibling Tenants implementation patterns, Dapr docs, and Aspire integration catalog were reviewed.

### File List
