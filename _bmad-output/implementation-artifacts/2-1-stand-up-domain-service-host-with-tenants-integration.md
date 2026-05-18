# Story 2.1: Stand up domain service host with Tenants integration

Status: done

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

- [x] Extend the server host composition without replacing existing scaffold modules. (AC: 1, 7)
  - [x] Update `src/Hexalith.Folders.Server/Program.cs` to add service defaults, Folders domain services, Tenants client integration, CloudEvents, Dapr subscribe handler, and the Tenants event subscription endpoint.
  - [x] Ensure the route and subscription shape matches the sibling Tenants client pattern: `/tenants/events`, `pubsub`, `system.tenants.events`, `UseCloudEvents()`, `MapSubscribeHandler()`, and `WithTopic(options.PubSubName, options.TopicName)`.
  - [x] Keep `src/Hexalith.Folders.Server/FoldersServerModule.cs` as the module registration surface; do not move runtime wiring into generated client or Contracts code.
  - [x] Preserve root scaffold smoke behavior until a real health/readiness endpoint supersedes it.
- [x] Add Folders-owned Tenants projection types. (AC: 2, 3, 4, 5)
  - [x] Create `src/Hexalith.Folders/Projections/TenantAccess/FolderTenantAccessProjection.cs` for tenant status, membership/role evidence, relevant `folders.*` configuration, projection watermark, and freshness metadata.
  - [x] Create `src/Hexalith.Folders/Projections/TenantAccess/FolderTenantAccessHandler.cs` or equivalent handlers that consume Tenants client events and update the projection idempotently.
  - [x] Handle only `TenantCreated`, `TenantUpdated`, `TenantDisabled`, `TenantEnabled`, `UserAddedToTenant`, `UserRemovedFromTenant`, `UserRoleChanged`, `TenantConfigurationSet`, and `TenantConfigurationRemoved`; unknown event types must not grant access.
  - [x] Deduplicate by `TenantEventEnvelope.MessageId`, apply per-tenant `SequenceNumber` monotonically, and treat replay conflicts, malformed payloads, missing tenant IDs, or future timestamps as fail-closed projection evidence.
  - [x] Persist deduplication keys, sequence evidence, replay-conflict markers, and projection watermarks through a projection-store abstraction; if that store is unavailable, authorizer calls must return `unavailable_projection` rather than falling back to in-memory success.
  - [x] Store metadata only: tenant id, principal id, role/group/service-agent ids, event sequence/watermark, timestamps, and non-secret `folders.*` configuration keys.
  - [x] Reject or ignore non-`folders.*` Tenants configuration keys; do not copy arbitrary Tenants configuration into Folders state.
  - [x] Apply `TenantConfigurationRemoved` as an explicit remove/tombstone operation for previously projected `folders.*` configuration keys so stale configuration cannot continue to authorize access.
- [x] Add tenant authorization and freshness services. (AC: 4, 5, 7)
  - [x] Create `src/Hexalith.Folders/Authorization/TenantAccessAuthorizer.cs` with explicit outcomes for allowed, denied, stale projection, unavailable projection, unknown tenant, disabled tenant, and malformed evidence.
  - [x] Include explicit outcomes for tenant mismatch, missing authoritative tenant, replay conflict, and future projection timestamps.
  - [x] Define a configurable projection freshness budget with a test default of five minutes, using an injectable UTC clock; missing, future, expired, malformed, or unavailable projection timestamps fail closed for mutations.
  - [x] Ensure mutation checks fail closed on stale, missing, malformed, replay-conflicting, future-dated, or unavailable projection data.
  - [x] Ensure read-only diagnostic checks can use bounded stale data only when the response includes projection freshness evidence and the caller is otherwise authorized.
  - [x] Keep diagnostic responses metadata-only: expose stable result codes and freshness fields, but not raw Tenants payloads, membership inventories, role lists, configuration values, or whether an unauthorized folder/resource exists.
  - [x] Do not create a new public diagnostic route outside the Contract Spine; if no read-only diagnostic operation exists at implementation time, test the authorizer response shape instead.
  - [x] Do not trust tenant ids supplied by request body, route, query, or client-controlled headers; compare them only against authentication or EventStore envelope tenant context.
- [x] Wire Dapr/Aspire topology using sibling-module patterns. (AC: 2, 6)
  - [x] Extend `src/Hexalith.Folders.Aspire/FoldersAspireModule.cs` or add an Aspire extension that wires `folders` and `folders-workers` with Dapr sidecars and references the shared Tenants/EventStore state store and pub/sub components.
  - [x] Update `src/Hexalith.Folders.AppHost/Program.cs` to compose EventStore, Tenants, Folders.Server, Folders.Workers, Folders.UI, Keycloak, Redis/Dapr components, and stable app IDs.
  - [x] Resolve Dapr access-control files from the AppHost directory, following the Tenants AppHost fallback pattern.
- [x] Add tests and fixtures for fail-closed behavior. (AC: 2, 3, 4, 5, 6, 8)
  - [x] Add unit tests under `tests/Hexalith.Folders.Tests` for projection event handling, idempotent replay, ignored non-`folders.*` config, disabled tenant, removed principal, stale projection, and unavailable projection.
  - [x] Add unit tests for malformed events, missing authoritative tenant context, future timestamps, replay conflicts, duplicate message IDs with divergent metadata, tenant mismatches between request and authoritative context, configuration removals, and bounded stale diagnostic reads.
  - [x] Add server tests under `tests/Hexalith.Folders.Server.Tests` proving CloudEvents, subscribe handler, Tenants event endpoint registration, topic/pubsub names, and unknown-event behavior are present without starting external Dapr.
  - [x] Assert endpoint registration through route metadata, endpoint data sources, or equivalent structural surfaces rather than live Dapr discovery calls.
  - [x] Add AppHost/Aspire structural tests under `tests/Hexalith.Folders.IntegrationTests` or a focused Aspire test project for stable app IDs and component references without requiring live provider credentials.
  - [x] Add negative tests proving a request-supplied tenant id cannot authorize a mutation when the authenticated/EventStore tenant differs.

### Review Findings

_Code review of commit a2a301e on 2026-05-18 — Blind Hunter + Edge Case Hunter + Acceptance Auditor (parallel adversarial layers)._

_Decisions resolved 2026-05-18 (best-practice defaults):_
- **D1** → Add `ITenantContextAccessor` reading `HttpContext.User`; bind authoritative tenant from claims in this story.
- **D2** → Defer JWT middleware to Story 7.2 but add startup guard in `AddFoldersServer` that throws if no auth scheme is registered.
- **D3** → Drop out-of-order events silently (do not flag `MalformedEvidence` on `SequenceNumber ≤ Watermark`); keep latches only for genuinely malformed payloads and divergent `MessageId` evidence.
- **D4** → Add `Version` (etag-style) to `FolderTenantAccessProjection`; `IFolderTenantAccessProjectionStore.SaveAsync` rejects on stale version and the handler retries.
- **D5** → `/project` returns `501 Not Implemented` until real projection handlers arrive (Story 2.3+).

- [x] [Review][Patch] **AC7 — replace `Command.TenantId` with claim-derived authoritative tenant** [src/Hexalith.Folders.Server/FoldersDomainServiceRequestHandler.cs:20-22] — Introduce `ITenantContextAccessor` (reading `HttpContext.User`'s tenant claim); `FoldersDomainServiceRequestHandler` resolves `AuthoritativeTenantId` from the accessor and `RequestedTenantId` from `request.Command.TenantId`. The two now differ on a forged request and the `TenantMismatch` branch becomes live. Register a default `HttpContext`-backed accessor in `AddFoldersServer`; tests inject a fake.
- [x] [Review][Patch] **Startup guard for missing auth scheme** [src/Hexalith.Folders.Server/FoldersServerModule.cs] — In `AddFoldersServer`, after registrations, throw `InvalidOperationException` at host build time if `IAuthenticationSchemeProvider` has no schemes and `ASPNETCORE_ENVIRONMENT` is not `Development`. Prevents accidental insecure deployment without taking on Story 7.2 OIDC scope.
- [x] [Review][Patch] **Stop flagging out-of-order delivery as `MalformedEvidence`** [src/Hexalith.Folders/Projections/TenantAccess/FolderTenantAccessHandler.cs:38-43] — When `SequenceNumber ≤ Watermark` and `MessageId` is not in `ProcessedMessages`, drop silently (log structured warning with metadata-only fields). Keep `MalformedEvidence` only for genuinely malformed payloads (missing fields, future timestamps).
- [x] [Review][Patch] **Optimistic concurrency on projection store** [src/Hexalith.Folders/Projections/TenantAccess/FolderTenantAccessProjection.cs, IFolderTenantAccessProjectionStore.cs, FolderTenantAccessHandler.cs] — Add a `long Version` (or string etag) to the projection. `SaveAsync` accepts an expected version and throws `TenantAccessConcurrencyException` on mismatch. Handler catches and retries the read-modify-write up to N attempts before returning to the dispatcher.
- [x] [Review][Patch] **`/project` returns 501 Not Implemented** [src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs:22-23] — Replace the empty-array stub with `Results.Problem(statusCode: 501, title: "Projection endpoint not implemented")`. Keeps AC1 route shape; removes the public unauthenticated empty-success oracle.
- [x] [Review][Patch] **403 response body leaks projection metadata to unauthorized callers** [src/Hexalith.Folders.Server/FoldersDomainServiceRequestHandler.cs:27] — Returns full `TenantAccessAuthorizationResult` (with `TenantId`, `ProjectionWatermark`, `LastEventTimestamp`, `ProjectionAge`) on every deny. Tenant-existence and freshness probe oracle. Fix: return stable `code` only via RFC 9457 Problem Details, no projection metadata.
- [x] [Review][Patch] **CorrelationId in dedup evidence causes false ReplayConflict on legitimate Dapr retries** [src/Hexalith.Folders/Projections/TenantAccess/FolderTenantEventEvidence.cs:9, Handler:29] — Dapr at-least-once redelivery typically rotates correlation id; structural inequality flips `ReplayConflict` permanently. Remove `CorrelationId` from the evidence record (or from equality comparison).
- [x] [Review][Patch] **Broad `catch (Exception)` swallows `OperationCanceledException`** [src/Hexalith.Folders/Authorization/TenantAccessAuthorizer.cs:43-50] — Cancellation surfaces as `unavailable_projection`; real defects (NRE, ArgumentException) masked. Fix: `catch (Exception ex) when (ex is not OperationCanceledException)`.
- [x] [Review][Patch] **`InMemoryFolderTenantAccessProjectionStore` ignores `CancellationToken`** [src/Hexalith.Folders/Projections/TenantAccess/InMemoryFolderTenantAccessProjectionStore.cs] — Add `cancellationToken.ThrowIfCancellationRequested()` at entry of `GetAsync`/`SaveAsync`.
- [x] [Review][Patch] **TenantId silently coerced to empty when envelope/event tenant mismatch** [src/Hexalith.Folders.Server/FoldersTenantEventHandler.cs:105-115, Handler:11-14] — Mismatch is silently dropped (no log, no malformed marker). Producer-side smuggling attempts are invisible. Emit structured warning and persist projection evidence (malformed or new envelope-mismatch outcome).
- [x] [Review][Patch] **PayloadFingerprint includes raw payload values (potential PII)** [src/Hexalith.Folders.Server/FoldersTenantEventHandler.cs:56,68,82,94,106] — `@event.Name`, `@event.Key`, etc. flow into the evidence record. Tenant Name can be PII; "metadata-only" invariant violated. Fix: hash the fingerprint (SHA256) before storing.
- [x] [Review][Patch] **Loop in handler returns after first processor** [src/Hexalith.Folders.Server/FoldersDomainServiceRequestHandler.cs:30-34] — `foreach (var processor) { return ... }` silently ignores additional registrations. Use `Single()` (with a clear error when zero or multiple), or define a selection strategy.
- [x] [Review][Patch] **AC7 negative test bypasses production composition** [tests/Hexalith.Folders.Tests/Authorization/TenantAccessAuthorizerTests.cs] — `MutationShouldRejectInvalidAuthorityOrPrincipal` constructs context directly with distinct tenant ids — a state the production handler can never produce. Add a true server-level test through `/process` driving body tenant ≠ auth tenant (after the AC7 decision lands).
- [x] [Review][Patch] **Missing server tests for `/process` fail-closed, unknown-event handling, and `.WithTopic` metadata** [tests/Hexalith.Folders.Server.Tests/ServerEndpointRegistrationTests.cs] — Task list required these explicitly; only route-presence + string constants are asserted today. Add: (a) `/process` denies when projection evidence is missing; (b) `FoldersTenantEventHandler` does not grant access on unknown event types; (c) the `/tenants/events` endpoint metadata carries the expected Topic attribute.
- [x] [Review][Patch] **AppHost structural test only asserts app-id constants** [tests/Hexalith.Folders.IntegrationTests/AspireTopologyTests.cs] — Task asked for component-reference assertions. Load `DistributedApplication.CreateBuilder` in-process and assert sidecar / shared state-store / pubsub references on each resource.
- [x] [Review][Patch] **`TenantAccessOptions` is not bound to `IConfiguration`** [src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs] — Registered as `new TenantAccessOptions()` singleton; story said "configurable projection freshness budget". Bind to `IConfiguration.GetSection("Folders:TenantAccess")` or expose `IOptions<TenantAccessOptions>`.
- [x] [Review][Patch] **No clock-skew tolerance on future-timestamp check** [src/Hexalith.Folders/Projections/TenantAccess/FolderTenantAccessHandler.cs:117] — Strict `@event.Timestamp > clock.UtcNow`; 1 ms producer drift bricks the tenant (under the permanent-latch behavior). After the F7 decision, allow a small tolerance (e.g., 5 s) before classifying as malformed.
- [x] [Review][Defer] **accesscontrol.yaml ships `defaultAction: allow` with no environment guard** [src/Hexalith.Folders.AppHost/DaprComponents/accesscontrol.yaml] — deferred, local-dev scaffold; production deny-by-default belongs to Story 7.1.
- [x] [Review][Defer] **JWT audience hard-coded to `hexalith-eventstore` for all services; empty `SigningKey`; `RequireHttpsMetadata=false`** [src/Hexalith.Folders.AppHost/Program.cs:51-53] — deferred, local-dev AppHost composition; production OIDC/secret-store integration belongs to Story 7.2. Audience-per-service correctness should be revisited there.
- [x] [Review][Defer] **`Workers/Program.cs` is an empty host with no `IHostedService`** [src/Hexalith.Folders.Workers/Program.cs] — deferred, workers do nothing in Story 2.1; Story 2.9 ("react to Tenants events through worker handlers") owns the subscription pipeline.
- [x] [Review][Defer] **`RemovedConfigurationKeys` can grow unboundedly** [src/Hexalith.Folders/Projections/TenantAccess/FolderTenantAccessHandler.cs:111] — deferred, no AC bounds it; durable projection store choice (later story) will determine retention strategy.
- [x] [Review][Defer] **Hard-coded `localhost:6379` Redis with no `AddRedis()` resource** [src/Hexalith.Folders.Aspire/FoldersAspireModule.cs] — deferred, distributed deployment wiring is Story 7.x; local dev runs work today.

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
| 2026-05-18 | Implemented domain-service host composition, Tenants projection pipeline, fail-closed authorizer, Aspire topology, worker host, and structural/unit coverage. | Codex |
| 2026-05-18 | Applied code-review patches: claim-derived authoritative tenant via `ITenantContextAccessor`, deny responses stripped of projection metadata, `/project` returns 501, startup auth-scheme guard, out-of-order events dropped silently, optimistic concurrency on the projection store, hashed payload fingerprints, clock-skew tolerance, `OperationCanceledException` propagation, cancellation in the in-memory store, envelope-mismatch logging, fail-on-multi/zero-processor semantics, `TenantAccessOptions` bound to `IConfiguration`, server tests for `/process` fail-closed and `.WithTopic` metadata, AppHost structural component-reference test. All 168 tests pass. | Claude |

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

- `dotnet test tests\Hexalith.Folders.Tests\Hexalith.Folders.Tests.csproj`
- `dotnet test tests\Hexalith.Folders.Server.Tests\Hexalith.Folders.Server.Tests.csproj`
- `dotnet test tests\Hexalith.Folders.IntegrationTests\Hexalith.Folders.IntegrationTests.csproj`
- `dotnet test Hexalith.Folders.slnx`
- `dotnet build src\Hexalith.Folders.AppHost\Hexalith.Folders.AppHost.csproj`

### Implementation Plan

- Kept core tenant-access logic in `Hexalith.Folders` without Tenants/EventStore project references so the scaffold dependency boundary remains intact.
- Added a server-owned Tenants event adapter that maps the Tenants client event registry into Folders-owned projection events.
- Registered `/process`, `/project`, Dapr CloudEvents, `/dapr/subscribe`, and `/tenants/events` through the server module surface.
- Added fail-closed mutation authorization and bounded-stale diagnostic authorization with stable result codes and metadata-only freshness evidence.
- Added Aspire resource wiring for stable `eventstore`, `tenants`, `folders`, `folders-workers`, and `folders-ui` app IDs sharing Dapr state/pubsub components.

### Completion Notes List

- Story created by `/bmad-create-story 2-1-stand-up-domain-service-host-with-tenants-integration` equivalent workflow on 2026-05-15.
- Project-context, epics, PRD, architecture, current scaffold files, sibling Tenants implementation patterns, Dapr docs, and Aspire integration catalog were reviewed.
- Implemented Folders-owned tenant-access projection state, event evidence, idempotent handling, replay-conflict detection, `folders.*` configuration boundaries, and removed-configuration tombstones.
- Implemented tenant access authorization outcomes for allowed, denied, stale/unavailable/unknown/disabled/malformed evidence, tenant mismatch, missing authoritative tenant, and replay conflict.
- Wired the server host with service defaults, EventStore domain-service surfaces, Tenants client integration, CloudEvents, Dapr subscribe handler, and `/tenants/events` subscription endpoint.
- Wired Aspire/AppHost topology with stable app IDs, shared Dapr state/pubsub components, AppHost access-control resolution, Keycloak environment wiring, and an executable workers host.
- Added offline unit/structural tests for projection replay, configuration filtering/removal, fail-closed authorizer outcomes, route registration, app ID constants, and scaffold reference governance.

### File List

- `_bmad-output/implementation-artifacts/2-1-stand-up-domain-service-host-with-tenants-integration.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Folders.AppHost/DaprComponents/accesscontrol.yaml`
- `src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj`
- `src/Hexalith.Folders.AppHost/Program.cs`
- `src/Hexalith.Folders.Aspire/FoldersAspireModule.cs`
- `src/Hexalith.Folders.Aspire/HexalithFoldersResources.cs`
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`
- `src/Hexalith.Folders.Server/FoldersDomainServiceRequestHandler.cs`
- `src/Hexalith.Folders.Server/FoldersServerModule.cs`
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs`
- `src/Hexalith.Folders.Server/FoldersTenantEventHandler.cs`
- `src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj`
- `src/Hexalith.Folders.Server/Program.cs`
- `src/Hexalith.Folders.Workers/Hexalith.Folders.Workers.csproj`
- `src/Hexalith.Folders.Workers/Program.cs`
- `src/Hexalith.Folders/Authorization/TenantAccessAuthorizationContext.cs`
- `src/Hexalith.Folders/Authorization/TenantAccessAuthorizationResult.cs`
- `src/Hexalith.Folders/Authorization/TenantAccessAuthorizer.cs`
- `src/Hexalith.Folders/Authorization/TenantAccessOptions.cs`
- `src/Hexalith.Folders/Authorization/TenantAccessOutcome.cs`
- `src/Hexalith.Folders/Authorization/TenantProjectionFreshnessStatus.cs`
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs`
- `src/Hexalith.Folders/Hexalith.Folders.csproj`
- `src/Hexalith.Folders/Projections/TenantAccess/FixedUtcClock.cs`
- `src/Hexalith.Folders/Projections/TenantAccess/FolderTenantAccessEvent.cs`
- `src/Hexalith.Folders/Projections/TenantAccess/FolderTenantAccessEventKind.cs`
- `src/Hexalith.Folders/Projections/TenantAccess/FolderTenantAccessHandler.cs`
- `src/Hexalith.Folders/Projections/TenantAccess/FolderTenantAccessProjection.cs`
- `src/Hexalith.Folders/Projections/TenantAccess/FolderTenantEventEvidence.cs`
- `src/Hexalith.Folders/Projections/TenantAccess/FolderTenantPrincipalEvidence.cs`
- `src/Hexalith.Folders/Projections/TenantAccess/IFolderTenantAccessProjectionStore.cs`
- `src/Hexalith.Folders/Projections/TenantAccess/InMemoryFolderTenantAccessProjectionStore.cs`
- `src/Hexalith.Folders/Projections/TenantAccess/IUtcClock.cs`
- `src/Hexalith.Folders/Projections/TenantAccess/SystemUtcClock.cs`
- `tests/Hexalith.Folders.IntegrationTests/AspireTopologyTests.cs`
- `tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj`
- `tests/Hexalith.Folders.Server.Tests/ServerEndpointRegistrationTests.cs`
- `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs`
- `tests/Hexalith.Folders.Tests/Authorization/TenantAccessAuthorizerTests.cs`
- `tests/Hexalith.Folders.Tests/Projections/TenantAccess/FolderTenantAccessHandlerTests.cs`
