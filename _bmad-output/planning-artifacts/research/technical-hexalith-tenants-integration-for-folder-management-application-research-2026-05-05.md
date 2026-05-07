---
stepsCompleted: [1, 2, 3, 4, 5, 6]
inputDocuments: []
workflowType: 'research'
lastStep: 6
status: 'complete'
completedAt: '2026-05-05'
research_type: 'technical'
research_topic: 'Hexalith.Tenants integration for Folder management application'
research_goals: 'Find all needed architectural, implementation, dependency, configuration, and integration information so the Folder app can use tenant capabilities correctly.'
user_name: 'Jerome'
date: '2026-05-05'
web_research_enabled: true
source_verification: true
---

# Research Report: technical

**Date:** 2026-05-05
**Author:** Jerome
**Research Type:** technical

---

## Research Overview

This research identifies the concrete architecture, dependencies, runtime wiring, security boundaries, and implementation steps needed to integrate `Hexalith.Tenants` into the `Hexalith.Folders` Folder management application. The analysis combines local source inspection across `Hexalith.Tenants`, `Hexalith.EventStore`, and `Hexalith.FrontComposer` with current public verification from NuGet, GitHub, Microsoft architecture guidance, Dapr documentation, CloudEvents, RFC 9457, and OWASP API Security references.

The central finding is that `Hexalith.Tenants` should be integrated as a platform tenant-management bounded context, not embedded into Folder domain logic. Folder should own Folder aggregates and projections under the managed tenant ID, while Tenants remains the source of truth for tenant lifecycle, membership, roles, global administration, and tenant configuration. Folder should consume Tenants events through Dapr pub/sub, maintain a local tenant-access projection, and enforce fail-closed tenant/object authorization on every Folder command and query.

The full synthesis at the end of this document provides the implementation roadmap, dependency recommendations, risk register, and source verification notes.

---

## Technical Research Scope Confirmation

**Research Topic:** Hexalith.Tenants integration for Folder management application
**Research Goals:** Find all needed architectural, implementation, dependency, configuration, and integration information so the Folder app can use tenant capabilities correctly.

**Technical Research Scope:**

- Architecture Analysis - design patterns, frameworks, system architecture
- Implementation Approaches - development methodologies, coding patterns
- Technology Stack - languages, frameworks, tools, platforms
- Integration Patterns - APIs, protocols, interoperability
- Performance Considerations - scalability, optimization, patterns

**Research Methodology:**

- Current web data with rigorous source verification
- Multi-source validation for critical technical claims
- Confidence level framework for uncertain information
- Comprehensive technical coverage with architecture-specific insights

**Scope Confirmed:** 2026-05-05

---

## Technology Stack Analysis

### Programming Languages

The integration target is a .NET/C# stack. `Hexalith.Tenants` sets `TargetFramework` to `net10.0`, enables nullable reference types, implicit usings, `LangVersion=latest`, and treats warnings as errors in `Hexalith.Tenants/Directory.Build.props`. The current public NuGet package `Hexalith.Tenants.Contracts` is version `3.11.1`, targets `.NET 10.0`, and was last updated on 2026-05-05. The root `Hexalith.Folders` repository currently contains only a minimal README plus submodules, so the Folder management application still needs its own source solution/projects or must be integrated into another existing host.

_Primary language:_ C# on .NET 10.0.
_Supporting files:_ YAML for Dapr components, JSON for configuration, PowerShell/bash scripts for local demo automation.
_Source:_ `Hexalith.Tenants/Directory.Build.props`, `Hexalith.Tenants/Directory.Packages.props`, NuGet `Hexalith.Tenants.Contracts` https://www.nuget.org/packages/Hexalith.Tenants.Contracts

### Development Frameworks and Libraries

`Hexalith.Tenants` is built around ASP.NET Core, Dapr, .NET Aspire, Hexalith.EventStore, MediatR, FluentValidation, OpenTelemetry, and xUnit v3. Public package metadata confirms `Hexalith.Tenants.Server 3.11.1` depends on `Dapr.Actors`, `Dapr.Actors.AspNetCore`, `Dapr.Client`, `FluentValidation`, `Hexalith.EventStore.Server`, `Hexalith.Tenants.Contracts`, and `MediatR`. `Hexalith.Tenants.Aspire 3.11.1` depends on `Aspire.Hosting` and `CommunityToolkit.Aspire.Hosting.Dapr`.

For a Folder management app, this implies two separate integration roles:

- Folder domain service/server-side code should reference `Hexalith.Tenants.Contracts` for shared tenant identity, commands, events, roles, and query DTOs.
- Folder consuming services should register tenant client services with `builder.Services.AddHexalithTenants()` and map the Dapr subscription endpoint with `app.UseCloudEvents()`, `app.MapSubscribeHandler()`, and `app.MapTenantEventSubscription()`.

The README advertises `Hexalith.Tenants.Client`, but NuGet did not return the package page during verification on 2026-05-05 while the local source project exists at `src/Hexalith.Tenants.Client`. The Folder app should use a project reference to the submodule client project until package publication is confirmed.

_Major frameworks:_ ASP.NET Core, Dapr ASP.NET Core integration, Dapr actors/client, .NET Aspire, CommunityToolkit Aspire Dapr hosting, Hexalith.EventStore.
_Source:_ `Hexalith.Tenants/src/Hexalith.Tenants.Client/Registration/TenantServiceCollectionExtensions.cs`, `Hexalith.Tenants/src/Hexalith.Tenants.Client/Subscription/TenantEventSubscriptionEndpoints.cs`, NuGet `Hexalith.Tenants.Server` https://www.nuget.org/packages/Hexalith.Tenants.Server, NuGet `Hexalith.Tenants.Aspire` https://www.nuget.org/packages/Hexalith.Tenants.Aspire

### Database and Storage Technologies

The local Aspire extension provisions Dapr state store and pub/sub components. `HexalithTenantsExtensions.AddHexalithTenants()` creates a Dapr component named `statestore` using `state.redis`, sets `actorStateStore=true`, sets `redisHost=localhost:6379`, and creates a Dapr pub/sub component named `pubsub`. The comments explicitly state that Redis-backed state is required so EventStore and Tenants sidecars share command/projection state; per-sidecar in-memory state would break status polling.

Tenant projection data is stored under keys prefixed with `projection:tenants:` and the tenant index model is also maintained through Dapr state. The Folder application should not create a separate tenant authority store unless it is intentionally replacing the shared tenant service. Folder-specific data should be scoped by tenant ID in its own domain storage/projections, while tenant membership/status/configuration comes from `Hexalith.Tenants`.

_Relational databases:_ none required by the tenant module as inspected.
_NoSQL/state store:_ Dapr `state.redis` for actor state and read models.
_Pub/sub broker:_ Dapr pub/sub component named `pubsub`, Redis in local topology.
_Source:_ `Hexalith.Tenants/src/Hexalith.Tenants.Aspire/HexalithTenantsExtensions.cs`, `Hexalith.Tenants/src/Hexalith.Tenants/Actors/TenantsProjectionActor.cs`, Dapr pub/sub overview https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-overview/

### Development Tools and Platforms

The current local stack expects .NET 10 SDK, Dapr CLI/runtime, Docker, and an Aspire AppHost. `docs/quickstart.md` says local development uses the `system` tenant for tenant-management commands and the AppHost handles local `system` tenant configuration. Integration tests that use Dapr require Dapr initialization.

The root workspace uses Git submodules for `Hexalith.Tenants`, `Hexalith.EventStore`, `Hexalith.FrontComposer`, and `Hexalith.AI.Tools`. Per repository instructions, only root-level submodules should be initialized/updated unless nested submodules are explicitly requested. `Hexalith.Tenants/Directory.Build.props` already supports the sibling-submodule layout by detecting `..\Hexalith.EventStore`.

_IDE/build:_ standard `dotnet build` / `dotnet test`; solution file is `Hexalith.Tenants.slnx`.
_Required local tools:_ .NET 10 SDK, Dapr CLI/runtime, Docker Desktop, Aspire AppHost.
_Source:_ `Hexalith.Tenants/docs/quickstart.md`, `Hexalith.Tenants/Directory.Build.props`, `.gitmodules`

### Cloud Infrastructure and Deployment

The local topology is Aspire-first. Public Aspire documentation confirms the Dapr integration uses `CommunityToolkit.Aspire.Hosting.Dapr` and attaches Dapr sidecars to Aspire project resources. Dapr documentation confirms pub/sub integrates through a sidecar and supports ASP.NET Core subscription endpoints discovered through app routes/subscription metadata.

The Tenants AppHost wires:

- `eventstore` with Dapr app ID `eventstore`, shared state store, and shared pub/sub.
- `tenants` with Dapr app ID `tenants`, shared state store, and shared pub/sub.
- `sample` subscriber with Dapr app ID `sample` and pub/sub only.
- optional Keycloak local identity provider for JWT testing.
- EventStore Admin server/UI for inspection.

The Folder management AppHost should mirror this pattern: add EventStore, Tenants, Folder service, and any Folder UI/API resources; attach Dapr sidecars with unique `AppId` values; reference the shared Tenants/EventStore state store and pub/sub where needed; and avoid hard-coded Dapr HTTP ports.

_Local deployment:_ Aspire AppHost plus Docker-backed Redis/Dapr sidecars.
_Cloud deployment considerations:_ keep Dapr component names stable (`statestore`, `pubsub`) and map them to production-grade state/pubsub backends; preserve app IDs used in access-control policies.
_Source:_ `Hexalith.Tenants/src/Hexalith.Tenants.AppHost/Program.cs`, Aspire Dapr integration https://aspire.dev/integrations/frameworks/dapr/, Dapr subscription methods https://docs.dapr.io/developing-applications/building-blocks/pubsub/subscription-methods/

### Technology Adoption Trends

The current Hexalith direction is modular, event-sourced, Dapr-mediated, and Aspire-orchestrated. Tenants is not a library-only concern; it is a platform service that owns tenant lifecycle and publishes tenant events. Folder management should therefore integrate by consuming tenant contracts/events and enforcing tenant context in its own domain, not by duplicating tenant membership state as an independent source of truth.

Important integration constraints:

- Tenant management commands always run under platform tenant `system`, domain `tenants`, with canonical identity `system:tenants:{aggregateId}`.
- Tenant events are published on `system.tenants.events`; infrastructure failures may use `deadletter.tenants.events`.
- Dapr pub/sub is at-least-once; Folder handlers must be idempotent.
- Tenant role enums are serialized as integers in events and as strings by query endpoints, so Folder consumers should handle both when bridging event and HTTP query data.
- The current local tenant client projection store is in-memory, suitable for samples/tests but not durable production authorization state.

_Confidence:_ High for local codebase facts and public package metadata; medium for package availability of `Hexalith.Tenants.Client` because local source exists but public NuGet verification did not find a package page.
_Source:_ `Hexalith.Tenants/docs/event-contract-reference.md`, `Hexalith.Tenants/docs/idempotent-event-processing.md`, GitHub `Hexalith.Tenants` https://github.com/Hexalith/Hexalith.Tenants

---

## Integration Patterns Analysis

### API Design Patterns

The Hexalith tenant integration uses three API surfaces:

- **EventStore Command API** for writes: Folder-related tenant administration flows should submit tenant commands through `/api/v1/commands`, not call Tenants aggregates directly. Tenant management commands use `tenant = "system"`, `domain = "tenants"`, and aggregate IDs such as the managed tenant ID or `global-administrators`.
- **Tenants query HTTP API** for tenant read access: Tenants exposes `GET /api/tenants`, `GET /api/tenants/{tenantId}`, `GET /api/tenants/{tenantId}/users`, and `GET /api/users/{userId}/tenants`. `GET /api/tenants/{tenantId}/audit` exists but is currently routed to a not-implemented query path in the MVP.
- **Dapr pub/sub subscription API** for reactive local state: consuming services map `/tenants/events` through `MapTenantEventSubscription()` and Dapr discovers it through the ASP.NET Core subscription handler.

For Folder management, the correct pattern is:

1. Folder commands and queries remain in the Folder bounded context, likely with `domain = "folders"` and tenant-specific aggregate IDs.
2. Folder access checks use tenant membership/status projected from `Hexalith.Tenants` events, or Tenants query endpoints for authoritative reads when eventual consistency is unacceptable.
3. Tenant lifecycle/user-role/configuration changes are sent to the Tenants module, not modeled as Folder commands.

_RESTful APIs:_ EventStore command/query APIs and Tenants query controller.
_Webhook/event API:_ Dapr pub/sub route `/tenants/events`.
_Source:_ `Hexalith.EventStore/docs/reference/command-api.md`, `Hexalith.EventStore/docs/reference/query-api.md`, `Hexalith.Tenants/src/Hexalith.Tenants/Controllers/TenantsQueryController.cs`, Dapr subscription methods https://docs.dapr.io/developing-applications/building-blocks/pubsub/subscription-methods/

### Communication Protocols

The integration uses HTTP/HTTPS for command and query APIs, Dapr service invocation for internal service-to-service calls, Dapr pub/sub for domain events, SignalR only if the Folder UI participates in EventStore projection invalidation, and JWT bearer authentication for protected HTTP endpoints.

`TenantBootstrapHostedService` demonstrates the internal command submission pattern: it creates a Dapr service invocation request to app ID `eventstore`, method `api/v1/commands`, and posts a `BootstrapGlobalAdmin` command. This is directly relevant to Folder bootstrapping if the Folder app wants to seed tenant defaults or verify the platform admin exists during local development.

Dapr documentation confirms pub/sub provides a platform-agnostic API with at-least-once delivery. That means Folder event consumers must tolerate duplicates and out-of-order operational timing. The built-in `TenantEventProcessor` deduplicates in-process by `MessageId`, but production scaled-out Folder services should add external deduplication if tenant event handling mutates durable Folder authorization state.

_HTTP/HTTPS:_ command/query APIs and tenant REST reads.
_Dapr service invocation:_ internal bootstrap and service-to-service calls by app ID.
_Dapr pub/sub:_ tenant event delivery on `system.tenants.events`.
_SignalR:_ optional projection invalidation through EventStore, not tenant data transport.
_Source:_ `Hexalith.Tenants/src/Hexalith.Tenants/Bootstrap/TenantBootstrapHostedService.cs`, `Hexalith.Tenants/src/Hexalith.Tenants.Client/Subscription/TenantEventProcessor.cs`, Dapr pub/sub overview https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-overview/

### Data Formats and Standards

All practical integration payloads are JSON. Tenant commands are submitted as JSON command envelopes to the EventStore Command API. Tenant event payloads are JSON-serialized `IEventPayload` implementations wrapped in EventStore event envelopes and delivered through Dapr/CloudEvents. Tenants query endpoints serialize read DTOs as JSON.

Important format details for Folder:

- Event payload enums use integer values. `TenantRole`: `0 = TenantOwner`, `1 = TenantContributor`, `2 = TenantReader`; `TenantStatus`: `0 = Active`, `1 = Disabled`.
- Query responses may serialize enums as strings because the query endpoint uses `JsonStringEnumConverter`.
- `TenantEventEnvelope.Payload` is a byte array containing serialized JSON for the event payload. `EventTypeName` is used to resolve the CLR event type.
- Event consumers should ignore unknown future fields and handle unknown enum values fail-closed or as lowest permission.

CloudEvents is a CNCF-standard event metadata format for interoperability across services and protocols; RFC 9457 is the current IETF problem-details standard and obsoletes RFC 7807. Hexalith docs mention RFC 7807 in some places, while EventStore docs reference RFC 9457. Folder code should accept `application/problem+json` style error responses and avoid depending on only the old RFC number.

_JSON:_ commands, queries, event payloads, error bodies.
_CloudEvents:_ Dapr/EventStore event envelope interoperability.
_Problem Details:_ RFC 9457-compatible HTTP errors.
_Source:_ `Hexalith.Tenants/docs/event-contract-reference.md`, CloudEvents spec https://github.com/cloudevents/spec, RFC 9457 https://www.rfc-editor.org/rfc/rfc9457

### System Interoperability Approaches

The interoperability model is intentionally loose-coupled:

- EventStore is the command gateway and owns command status, event persistence, publishing, and query dispatch contracts.
- Tenants is a domain service for `system|tenants|v1`, registered under `EventStore:DomainServices:Registrations`.
- Folder should become a separate domain service, likely `tenant-id|folders|v1` or a configured equivalent, with its own command processing endpoint and projection endpoint.
- Folder should consume Tenants events into a local membership/status/configuration projection for fast authorization checks.
- Folder should not import `Hexalith.Tenants.Server` unless it is hosting tenant domain processing. Most Folder services need `Contracts`, the local `Client` source/package, and possibly `Testing`.

The `Hexalith.Tenants.Client` registration is the key consuming-service integration point. `AddHexalithTenants()` registers `DaprClient`, tenant options, `ITenantProjectionStore`, `TenantProjectionEventHandler`, event handler interfaces for tenant lifecycle/member/configuration events, an event type registry, and `TenantEventProcessor`.

_Point-to-point integration:_ only for explicit command/query HTTP calls.
_API gateway pattern:_ EventStore Command/Query API is the system command/query gateway.
_Local projection pattern:_ Folder maintains local tenant-aware state from events.
_Source:_ `Hexalith.Tenants/src/Hexalith.Tenants.Client/Registration/TenantServiceCollectionExtensions.cs`, `Hexalith.Tenants/src/Hexalith.Tenants/appsettings.json`

### Microservices Integration Patterns

The local AppHost demonstrates the required microservice composition:

- Add Tenants as an Aspire project resource.
- Call `builder.AddHexalithTenants(tenants, accessControlConfigPath)` to provision Dapr `statestore` and `pubsub`, wire Tenants with app ID `tenants`, and return shared resources.
- Wire EventStore with app ID `eventstore` and references to the same state store and pub/sub.
- Wire consuming services with unique Dapr app IDs and a pub/sub reference.
- Avoid fixed Dapr HTTP ports; current code intentionally leaves them dynamic to avoid conflicts.

For Folder:

```csharp
IResourceBuilder<ProjectResource> folders = builder.AddProject<Projects.Hexalith_Folders>("folders");
_ = folders.WithDaprSidecar(sidecar => sidecar
    .WithOptions(new DaprSidecarOptions {
        AppId = "folders",
        Config = accessControlConfigPath,
    })
    .WithReference(tenantsResources.PubSub)
    .WithReference(tenantsResources.StateStore)); // only if Folder needs Dapr state/actors
```

Use the state store reference only if Folder uses Dapr state/actors. A pure event subscriber that persists to its own database only needs pub/sub. If Folder has its own EventStore projections/commands, it will also need the EventStore domain-service registration and projection endpoints following the EventStore patterns.

_Service discovery:_ Aspire resource references plus Dapr app IDs.
_Circuit breaker/resilience:_ Dapr resiliency YAML exists locally and should be carried into production equivalents.
_Saga/distributed transaction:_ no current Tenants saga requirement; use compensating commands for tenant lifecycle reversals.
_Source:_ `Hexalith.Tenants/src/Hexalith.Tenants.AppHost/Program.cs`, `Hexalith.Tenants/src/Hexalith.Tenants.Aspire/HexalithTenantsExtensions.cs`, Aspire Dapr integration https://aspire.dev/integrations/frameworks/dapr/

### Event-Driven Integration

Folder should subscribe to the tenant event topic when it needs authorization, tenant status, or tenant configuration without synchronous calls on every request.

Minimum Folder service code:

```csharp
using Hexalith.Tenants.Client.Registration;
using Hexalith.Tenants.Client.Subscription;

builder.Services.AddHexalithTenants(options => {
    options.PubSubName = "pubsub";
    options.TopicName = "system.tenants.events";
});

WebApplication app = builder.Build();
app.UseCloudEvents();
app.MapSubscribeHandler();
app.MapTenantEventSubscription();
```

Folder-specific handlers can be added alongside the built-in projection handler:

```csharp
builder.Services.AddSingleton<ITenantEventHandler<UserRemovedFromTenant>, FolderTenantAccessRevokedHandler>();
builder.Services.AddSingleton<ITenantEventHandler<TenantDisabled>, FolderTenantDisabledHandler>();
builder.Services.AddSingleton<ITenantEventHandler<TenantConfigurationSet>, FolderTenantConfigurationHandler>();
```

Recommended Folder reactions:

- `TenantCreated`: create Folder tenant defaults only if Folder owns per-tenant default folder structure.
- `TenantDisabled`: deny new Folder commands and optionally hide tenant content from normal queries.
- `TenantEnabled`: re-enable Folder access if local suspension was caused by tenant status.
- `UserAddedToTenant`, `UserRemovedFromTenant`, `UserRoleChanged`: update local ACL/membership projection.
- `TenantConfigurationSet/Removed`: process only Folder-owned keys such as `folders.*`.
- `GlobalAdministratorSet/Removed`: do not automatically grant Folder tenant-level data access unless Folder explicitly supports global operators.

Because Dapr pub/sub is at-least-once, handlers must be idempotent. For production, replace or supplement in-memory deduplication with Redis/database deduplication keyed by `MessageId` if duplicate side effects are possible.

_Publish-subscribe pattern:_ Tenants publishes, Folder subscribes.
_Event sourcing/CQRS:_ EventStore persists tenant events; Folder projects tenant access state.
_Source:_ `Hexalith.Tenants/src/Hexalith.Tenants.Client/Subscription/TenantEventSubscriptionEndpoints.cs`, `Hexalith.Tenants/src/Hexalith.Tenants.Client/Handlers/TenantProjectionEventHandler.cs`, `Hexalith.Tenants/docs/idempotent-event-processing.md`, Dapr pub/sub API reference https://docs.dapr.io/reference/api/pubsub_api/

### Integration Security Patterns

Authentication is JWT bearer. Tenants query endpoints use `[Authorize]` and expect the authenticated user ID in the `sub` claim. EventStore command/query endpoints require bearer tokens and enforce tenant claims/authorization in the gateway pipeline.

The Tenants local `accesscontrol.yaml` is explicitly development-only with `defaultAction: allow`; it warns that production must use deny-by-default with mTLS. The Folder app must not copy the local permissive policy into production. Instead, production should:

- assign stable Dapr app IDs (`eventstore`, `tenants`, `folders`, and UI/admin IDs);
- configure deny-by-default Dapr access-control policies;
- allow Folder to subscribe to `pubsub` and invoke only required services;
- avoid giving the Folder service direct permission to mutate tenant state unless it intentionally manages tenants;
- validate tenant ID from route/command payload against authenticated claims and local tenant projection;
- fail closed when tenant status or membership is unknown and no authoritative query can be made.

FrontComposer guidance also says tenant isolation is host-owned and fail-closed. That aligns with this integration: generated or composed Folder UI can expose tenant-aware commands, but the Folder host/domain must enforce tenant and policy boundaries.

_OAuth/JWT:_ bearer tokens with `sub`, tenant, and domain claims.
_Dapr access control:_ app-ID policies, production deny-by-default, mTLS.
_Problem Details:_ use machine-readable API errors for validation and authorization failures.
_Source:_ `Hexalith.Tenants/src/Hexalith.Tenants.AppHost/DaprComponents/accesscontrol.yaml`, `Hexalith.Tenants/src/Hexalith.Tenants/Controllers/TenantsQueryController.cs`, `Hexalith.FrontComposer/docs/skills/frontcomposer/security/tenant-and-policy-boundaries.md`

---

## Architectural Patterns and Design

### System Architecture Patterns

The Folder management application should treat `Hexalith.Tenants` as a platform capability and a neighboring bounded context, not as embedded Folder domain logic. The Tenants PRD and architecture describe a standalone event-sourced microservice that manages tenant lifecycle, user-role membership, global administration, and tenant configuration for Hexalith.EventStore applications. Microsoft microservice boundary guidance recommends that a microservice generally not span more than one bounded context; that matches the local architecture. Folder should own folder aggregates, folder commands, folder projections, and folder UI behavior. Tenants should own tenant membership/status/configuration and publish those facts.

Recommended bounded contexts:

| Context | Owns | Does not own |
| --- | --- | --- |
| Tenants | Tenant lifecycle, tenant roles, global admins, tenant configuration, tenant read model | Folder hierarchy, folder ACL exceptions, document/folder metadata |
| Folders | Folder tree, folder permissions derived from tenant role, folder-specific configuration, folder projections | Tenant creation, tenant user-role source of truth |
| EventStore | Command/query gateway, event persistence, pub/sub publication, command status, projection dispatch | Domain invariants for tenants or folders |
| FrontComposer/UI | Generated/composed UI metadata and tenant-aware affordances | Security enforcement or tenant source of truth |

The root `Hexalith.Folders` workspace currently lacks Folder source projects, so the first architectural action is to define the Folder solution shape before code integration: `Hexalith.Folders.Contracts`, `Hexalith.Folders.Server`, `Hexalith.Folders`, `Hexalith.Folders.Client` if needed, `Hexalith.Folders.Aspire`, AppHost, tests, and samples. Mirror EventStore/Tenants patterns where useful, but keep Folder package boundaries aligned with Folder behavior.

_Source:_ `Hexalith.Tenants/_bmad-output/planning-artifacts/architecture.md`, `Hexalith.Tenants/_bmad-output/planning-artifacts/prd.md`, Microsoft microservice boundaries https://learn.microsoft.com/en-us/azure/architecture/microservices/model/microservice-boundaries

### Design Principles and Best Practices

The core design should be DDD + CQRS + event sourcing only where the complexity is justified. Microsoft’s event sourcing guidance explicitly warns that event sourcing adds trade-offs around concurrency, schema evolution, querying, and migration. It is justified here because the Hexalith ecosystem already standardizes on EventStore and because tenant/folder access changes need auditability, replay, and downstream projections.

Folder design principles:

- **Separate write and read models:** Folder commands should validate invariants and emit events; Folder queries should read projections. Do not query event streams directly from UI paths.
- **Keep aggregates tenant-scoped:** every Folder aggregate identity should include the actual managed tenant ID, not `system`, except for platform/admin aggregates if any are introduced.
- **Do not duplicate Tenants invariants:** local Folder projections may cache membership/status, but they are not authoritative for tenant membership management.
- **Pure domain functions:** follow EventStore/Tenants `Handle` and `Apply` purity so unit tests can exercise domain decisions without Dapr.
- **Fail closed:** if tenant status or membership is missing/stale and the operation is security-sensitive, deny or perform an authoritative query rather than allow.
- **Use additive event evolution:** Folder event contracts should tolerate unknown fields and reserve breaking schema changes for explicit versioning.

_Source:_ `Hexalith.EventStore/_bmad-output/planning-artifacts/architecture.md`, `Hexalith.Tenants/_bmad-output/planning-artifacts/architecture.md`, Microsoft CQRS pattern https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs, Microsoft event sourcing pattern https://learn.microsoft.com/en-us/azure/architecture/patterns/event-sourcing

### Scalability and Performance Patterns

Tenant operations are administrative-frequency by design. Folder operations may be higher frequency, so Folder must not synchronously call Tenants for every read/write unless required by a high-risk security decision. The scalable pattern is:

1. Subscribe to Tenants events.
2. Maintain a local durable Folder tenant-access projection.
3. Authorize most Folder commands against the local projection.
4. Reconcile or refresh from Tenants query endpoints when projections are stale or missing.
5. Use EventStore command/query validation endpoints for UI preflight checks where available.

For Folder event processing, avoid the current Tenants client’s in-memory deduplication as the only production safeguard if the service is scaled out. Use a bounded local cache plus shared deduplication in Redis/database keyed by `MessageId`, or make all handlers fully idempotent and side-effect-free. For high-volume folder trees, avoid putting entire tenant folder catalogs under one state key; use per-folder or per-projection partitioning with short projection type names to keep ETags and keys compact.

Use these resilience patterns:

- **Bulkhead isolation:** separate tenant-event handling from Folder command processing so a backlog in tenant projection updates does not starve normal Folder operations.
- **Retry/circuit breaker:** rely on Dapr resiliency policies and framework policies; avoid duplicate ad hoc retry loops in handlers.
- **Backpressure:** cap tenant event handler concurrency per tenant or per projection key to avoid write conflicts.
- **Snapshots/projections:** use snapshots for long event streams; use read projections for UI/tree traversal.

_Source:_ Microsoft bulkhead pattern https://learn.microsoft.com/en-us/azure/architecture/patterns/bulkhead, Microsoft microservices design patterns https://learn.microsoft.com/en-us/azure/architecture/microservices/design/patterns, `Hexalith.EventStore/docs/reference/query-api.md`

### Integration and Communication Patterns

The chosen architecture is event-driven with synchronous APIs only where necessary:

- **Synchronous command submission:** Folder and Tenants writes go to EventStore `/api/v1/commands`, returning `202 Accepted` plus command status polling.
- **Synchronous reads:** Folder UI can call Folder query endpoints for folder projections and Tenants query endpoints for tenant admin screens.
- **Asynchronous propagation:** Tenant membership/status/configuration changes reach Folder through Dapr pub/sub.
- **Projection invalidation:** if Folder UI uses EventStore/FrontComposer real-time patterns, SignalR is an invalidation hint only; clients must re-query authoritative projections.

Recommended Folder command envelope pattern:

```json
{
  "tenant": "acme-corp",
  "domain": "folders",
  "aggregateId": "root",
  "commandType": "CreateFolder",
  "payload": {
    "parentFolderId": "root",
    "folderId": "engineering",
    "name": "Engineering"
  }
}
```

Recommended Tenants command envelope pattern for tenant management from a Folder admin UI:

```json
{
  "tenant": "system",
  "domain": "tenants",
  "aggregateId": "acme-corp",
  "commandType": "AddUserToTenant",
  "payload": {
    "tenantId": "acme-corp",
    "userId": "jane-doe",
    "role": 1
  }
}
```

The distinction is critical: Folder domain commands target the managed tenant; tenant-management commands target the platform tenant `system` and the `tenants` domain.

_Source:_ `Hexalith.EventStore/docs/reference/command-api.md`, `Hexalith.Tenants/docs/quickstart.md`, `Hexalith.Tenants/docs/event-contract-reference.md`, Microsoft event-driven architecture style https://learn.microsoft.com/en-us/azure/architecture/guide/architecture-styles/event-driven

### Security Architecture Patterns

Security must be layered because Folder has object identifiers in routes and commands, and OWASP identifies broken object-level authorization as the top API security risk in the API Security Top 10. Folder cannot rely on a user-provided `tenantId`, `folderId`, route parameter, or UI-disabled button. Every command/query must check whether the authenticated subject is allowed to access that tenant and object.

Recommended Folder security layers:

1. **JWT validation:** authenticate every HTTP command/query request.
2. **Tenant claim validation:** verify the requested tenant appears in authorized tenant claims where those claims are used.
3. **Tenant projection validation:** verify the local Tenants projection says the user belongs to the tenant and the tenant is active.
4. **Folder object authorization:** verify the user role is sufficient for the folder operation, including any folder-specific ACLs.
5. **EventStore pipeline authorization:** use EventStore validators or future Tenants authorization plugin when available.
6. **Dapr access control:** production deny-by-default sidecar policies and mTLS.

Fail-closed rules:

- Unknown tenant: deny, unless a privileged admin route explicitly performs authoritative Tenants lookup.
- Disabled tenant: deny state-changing Folder commands immediately.
- Missing membership: deny state-changing commands and private folder reads.
- Global administrator: allow only explicitly designed platform/admin operations; do not automatically bypass all Folder data policies unless product requirements require it.

_Source:_ OWASP API Security Project https://owasp.org/www-project-api-security/, OWASP API Top 10 2023 release https://owasp.org/blog/2023/07/03/owasp-api-top10-2023, `Hexalith.FrontComposer/docs/skills/frontcomposer/security/tenant-and-policy-boundaries.md`

### Data Architecture Patterns

The data architecture should have two sources of truth:

- **Tenant facts:** Tenants event stream and Tenants read model.
- **Folder facts:** Folder event streams and Folder read models.

Folder should store tenant-derived data only as a projection/cache, with provenance fields such as tenant event `MessageId`, sequence number, timestamp, and correlation ID. This makes local authorization state auditable and debuggable. Store only the minimum tenant data needed for Folder decisions: tenant ID, tenant status, user-role map, optional Folder-owned configuration keys.

Suggested Folder local projection:

```csharp
public sealed class FolderTenantAccessProjection
{
    public required string TenantId { get; init; }
    public TenantStatus Status { get; set; }
    public Dictionary<string, TenantRole> Members { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> FolderConfiguration { get; } = new(StringComparer.Ordinal);
    public string? LastTenantEventMessageId { get; set; }
    public long LastTenantEventSequenceNumber { get; set; }
    public DateTimeOffset LastUpdatedAt { get; set; }
}
```

Suggested Folder aggregate identity:

```text
{managedTenantId}:folders:{folderId}
```

Avoid these data mistakes:

- Do not store Folder content under the Tenants `system:tenants:{tenantId}` aggregate.
- Do not use the `system` tenant for normal Folder domain data.
- Do not query all tenants and filter on the client.
- Do not make tenant events the only authorization check for first-time access without a defined stale/missing projection policy.

_Source:_ `Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Identity/TenantIdentity.cs`, `Hexalith.Tenants/src/Hexalith.Tenants.Client/Handlers/TenantProjectionEventHandler.cs`, Microsoft event sourcing pattern https://learn.microsoft.com/en-us/azure/architecture/patterns/event-sourcing

### Deployment and Operations Architecture

The Folder AppHost should compose EventStore, Tenants, Folder API/service, optional Folder UI, optional EventStore Admin, Keycloak for local auth, Dapr components, and shared observability.

Minimum AppHost topology:

```text
keycloak? ─┬─ eventstore + dapr(appId=eventstore)
           ├─ tenants + dapr(appId=tenants)
           ├─ folders + dapr(appId=folders)
           └─ folder-ui/admin-ui

dapr components:
  statestore  shared by eventstore/tenants/folders if needed
  pubsub      shared for tenant and folder events
  resiliency  shared policies
  accesscontrol deny-by-default in production
```

Operational requirements:

- Initialize only root-level Git submodules unless nested submodules are explicitly requested.
- Keep Dapr component names (`statestore`, `pubsub`) stable across services.
- Use unique Dapr app IDs and reflect them in access-control YAML.
- Carry over Tenants bootstrap configuration (`Tenants:BootstrapGlobalAdminUserId`) for first local/platform deployment when needed.
- Add OpenTelemetry instrumentation for Folder command latency, projection lag, tenant-event handling latency, and authorization denials.
- Test startup order with `WaitFor` in Aspire where one service depends on another.
- Add integration tests for tenant disabled, user removed, duplicate event delivery, stale projection, wrong-tenant route, and cross-tenant folder ID guessing.

_Source:_ `Hexalith.Tenants/src/Hexalith.Tenants.AppHost/Program.cs`, `Hexalith.Tenants/src/Hexalith.Tenants.Aspire/HexalithTenantsExtensions.cs`, `Hexalith.Tenants/src/Hexalith.Tenants/Configuration/TenantBootstrapOptions.cs`, Aspire Dapr integration https://aspire.dev/integrations/frameworks/dapr/

---

## Implementation Approaches and Technology Adoption

### Technology Adoption Strategies

Adopt `Hexalith.Tenants` incrementally. The Folder workspace currently has no Folder source projects at the root, so a "big bang" integration is premature. Use a staged adoption plan:

1. Scaffold the Folder solution and bounded context.
2. Add Tenants/EventStore/FrontComposer submodule references and verify build layout.
3. Add tenant event consumption to the Folder service.
4. Add local tenant-access projection and fail-closed authorization.
5. Add Folder commands/queries and AppHost topology.
6. Add UI/FrontComposer integration only after backend security boundaries work.

This is consistent with modernization guidance favoring phased adoption over big-bang replacement when multiple services and operational dependencies are involved. Even though this is greenfield Folder work, the principle still applies because the integration spans Tenants, EventStore, Dapr, Aspire, auth, and UI composition.

_Source:_ AWS strangler fig pattern https://docs.aws.amazon.com/prescriptive-guidance/latest/cloud-design-patterns/strangler-fig.html, Microsoft Operational Excellence https://learn.microsoft.com/en-us/azure/well-architected/operational-excellence/

### Development Workflows and Tooling

Recommended Folder project structure:

```text
src/
  Hexalith.Folders.Contracts
  Hexalith.Folders.Server
  Hexalith.Folders
  Hexalith.Folders.Client
  Hexalith.Folders.Aspire
  Hexalith.Folders.AppHost
  Hexalith.Folders.ServiceDefaults
  Hexalith.Folders.Testing
tests/
  Hexalith.Folders.Contracts.Tests
  Hexalith.Folders.Server.Tests
  Hexalith.Folders.Client.Tests
  Hexalith.Folders.IntegrationTests
samples/
  Hexalith.Folders.Sample
```

Recommended dependencies:

| Folder project | Tenants dependency | Reason |
| --- | --- | --- |
| `Contracts` | `Hexalith.Tenants.Contracts` only if public DTOs expose tenant role/status | Shared enums/contracts |
| `Server` | `Hexalith.Tenants.Contracts` | Domain checks and event types |
| `Client` / host | `Hexalith.Tenants.Client` project reference or package | DI and event subscription |
| `Testing` | `Hexalith.Tenants.Testing` | Infrastructure-free tenant setup |
| `AppHost` | `Hexalith.Tenants.Aspire` | Tenants topology helper |

Current package caution: `Hexalith.Tenants.Contracts`, `Server`, and `Aspire` verify publicly at `3.11.1`; `Hexalith.Tenants.Client` exists locally but did not verify as a public NuGet package during research. Use a local project reference until the package is published or confirmed.

Folder services should register Tenants client integration as follows:

```csharp
builder.Services.AddHexalithTenants(options => {
    options.PubSubName = "pubsub";
    options.TopicName = "system.tenants.events";
});

WebApplication app = builder.Build();
app.UseCloudEvents();
app.MapSubscribeHandler();
app.MapTenantEventSubscription();
```

_Source:_ `Hexalith.Tenants/src/Hexalith.Tenants.Client/Registration/TenantServiceCollectionExtensions.cs`, `Hexalith.Tenants/src/Hexalith.Tenants.Client/Subscription/TenantEventSubscriptionEndpoints.cs`, .NET testing docs https://learn.microsoft.com/en-us/dotnet/core/testing/

### Testing and Quality Assurance

Testing should be layered:

| Tier | Purpose | Infrastructure |
| --- | --- | --- |
| Unit | Folder aggregate `Handle/Apply`, tenant access policy functions | none |
| Contract/client | Tenant event handlers, enum handling, payload deserialization | none |
| Fake-backed integration | Folder behavior with `InMemoryTenantService` | none |
| Dapr integration | `/tenants/events` route, duplicate delivery, projection writes | Dapr |
| Aspire E2E | EventStore + Tenants + Folder + auth + pub/sub | Docker, Dapr, Aspire |

Tests required before considering integration complete:

- Tenant disabled blocks Folder state-changing commands.
- Tenant enabled restores access when local projection updates.
- User removed from tenant loses Folder access.
- User role downgraded loses write privileges.
- Duplicate `UserRemovedFromTenant` does not corrupt Folder state.
- `TenantConfigurationSet` with `folders.*` changes Folder behavior; unrelated config keys are ignored.
- Wrong-tenant route or aggregate ID is rejected.
- Cross-tenant folder ID guessing returns forbidden/not found without leaking object existence.
- Missing tenant projection fails closed.
- Public Tenants query enum strings and event enum integers both deserialize safely where both are consumed.

Use the Tenants conformance testing pattern as a model: reflection-based discovery catches new command/event types that need test coverage. The local `Hexalith.Tenants.Testing` package proves its in-memory fake delegates to production aggregate logic; Folder should mirror that approach for its own fakes.

_Source:_ `Hexalith.Tenants/src/Hexalith.Tenants.Testing/Fakes/InMemoryTenantService.cs`, `Hexalith.Tenants/tests/Hexalith.Tenants.Testing.Tests/Conformance/TenantConformanceTests.cs`, Microsoft .NET testing https://learn.microsoft.com/en-us/dotnet/core/testing/

### Deployment and Operations Practices

Operational excellence requirements for the Folder integration:

- Use Aspire for local topology and repeatable startup.
- Use infrastructure-as-code or checked-in Dapr component templates for every deployable environment.
- Use production Dapr access control with deny-by-default and mTLS.
- Use OpenTelemetry for Folder command duration, query duration, tenant-event processing latency, projection lag, authorization denials, and dead-letter events.
- Define runbooks for Dapr pub/sub outage, Tenants unavailable, stale Folder tenant projection, bootstrap failure, and Keycloak/token failures.
- Use safe deployment practices: deploy Folder event handlers before enabling write paths that depend on new tenant events.
- Preserve Dapr component names and app IDs through environment promotion unless explicitly migrated.

Suggested observability metrics:

| Metric | Why |
| --- | --- |
| `folders.tenant_event.duration` | event handler latency |
| `folders.tenant_projection.lag` | freshness of local authorization projection |
| `folders.authorization.denied` | security and support signal |
| `folders.command.duration` | command p95/p99 |
| `folders.query.duration` | read model performance |
| `folders.tenant_event.duplicate` | deduplication/load behavior |

_Source:_ Azure Well-Architected Operational Excellence https://learn.microsoft.com/en-us/azure/well-architected/operational-excellence/principles, `Hexalith.Tenants/src/Hexalith.Tenants.AppHost/DaprComponents/accesscontrol.yaml`

### Team Organization and Skills

Implementation needs skills in:

- .NET 10/C# and ASP.NET Core.
- Hexalith.EventStore command/query/domain service patterns.
- Dapr sidecars, pub/sub, state stores, access control, and resiliency.
- .NET Aspire AppHost composition.
- Event sourcing, CQRS, and eventual consistency.
- JWT/OIDC authorization and object-level access control.
- FrontComposer command/projection annotations if a generated UI is used.

Team ownership should be explicit:

| Area | Owner |
| --- | --- |
| Folder domain model | Folder backend |
| Tenant source of truth | Tenants module |
| EventStore gateway/topology | Platform/backend |
| Dapr production policy | Platform/DevOps |
| Folder UI composition | Frontend/FrontComposer |
| Security tests | Backend + QA |

FrontComposer-specific note: command docs say tenant and user identity should come from host context, not user-entered command fields. Some current Counter sample command types still expose `TenantId`; for Folder, prefer host-derived tenant identity where the source generator and host support it, and treat any visible tenant input as privileged/admin-only.

_Source:_ `Hexalith.FrontComposer/docs/skills/frontcomposer/domain/commands.md`, `Hexalith.FrontComposer/src/Hexalith.FrontComposer.SourceTools/Parsing/CommandParser.cs`

### Cost Optimization and Resource Management

Local development can share Redis-backed Dapr state/pubsub through Aspire. Production cost should be managed by selecting Dapr backing services appropriate to workload scale:

- Start with Redis-compatible state/pubsub for dev/test and low-volume environments.
- Move to managed production backends when durability, HA, or query support require it.
- Avoid storing large Folder tree projections as one state value; partition by tenant and projection purpose.
- Keep tenant local projection minimal; do not mirror full Tenants read models unless Folder needs them.
- Avoid synchronous Tenants lookups on every Folder request because they add latency and cross-service dependency cost.
- Use short projection type names to keep ETags and headers compact.

_Source:_ Microsoft CQRS pattern https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs, `Hexalith.EventStore/docs/reference/query-api.md`

### Risk Assessment and Mitigation

| Risk | Impact | Mitigation |
| --- | --- | --- |
| `Hexalith.Tenants.Client` package not public | Build/integration blocker for package-only consumers | Use project reference; publish/fix package before release |
| Folder app source not scaffolded | Cannot integrate code yet | Create solution structure first |
| Stale tenant projection allows access | Security incident | Fail closed; add freshness checks and authoritative fallback |
| Dapr event duplicates cause side effects | Incorrect ACL/projection | Idempotent handlers plus external dedup store |
| Using `system` for Folder data | Cross-tenant isolation bug | Use managed tenant ID for Folder aggregate identity |
| Copying dev access-control YAML to prod | Lateral movement/security exposure | Production deny-by-default policy review |
| Global admin over-broad access | Data exposure | Explicit admin policy; do not auto-bypass Folder object ACLs |
| Enum format mismatch | Deserialization/authorization bugs | Test integer and string enum inputs |
| Hidden nested submodule updates | Workspace churn/build risk | Only initialize/update root-level submodules by default |

_Source:_ OWASP API Security Project https://owasp.org/www-project-api-security/, `AGENTS.md`, `Hexalith.Tenants/docs/event-contract-reference.md`

## Technical Research Recommendations

### Implementation Roadmap

1. **Scaffold Folder solution:** create `src`, `tests`, AppHost, shared props/packages, and build CI.
2. **Add dependencies:** project-reference `Hexalith.Tenants.Client`; package/project-reference `Contracts`, `Testing`, and `Aspire` as needed.
3. **Compose AppHost:** EventStore, Tenants, Folders, Dapr state/pubsub, Keycloak optional, access control.
4. **Implement tenant event consumer:** `AddHexalithTenants`, subscription endpoint, local projection store.
5. **Implement authorization service:** fail-closed checks for tenant active, membership, role, folder object policy.
6. **Implement Folder commands/queries:** use managed tenant identity, domain `folders`, EventStore command/query patterns.
7. **Add tests:** unit, fake-backed, Dapr integration, Aspire E2E security.
8. **Add operations:** OpenTelemetry metrics, runbooks, production Dapr policies.
9. **Add UI:** generated/composed UI only after backend security and command/query contracts stabilize.

### Technology Stack Recommendations

- Target `net10.0` and mirror Tenants/EventStore central package management.
- Use Dapr pub/sub/state through sidecars only.
- Use Aspire for local/developer topology.
- Use `Hexalith.Tenants.Contracts` for command/event/role/status types.
- Use `Hexalith.Tenants.Client` for event subscription wiring.
- Use `Hexalith.Tenants.Testing` for fast tests.
- Avoid depending on `Hexalith.Tenants.Server` from Folder runtime unless intentionally hosting tenant domain logic.

### Skill Development Requirements

The implementing engineer should be comfortable with:

- EventStore command envelopes and status polling.
- Dapr pub/sub and ASP.NET Core subscription endpoints.
- Tenant projection freshness and eventual consistency.
- JWT claim extraction and object-level authorization.
- AppHost resource wiring and Dapr access policies.
- Idempotent event handler design.

### Success Metrics and KPIs

| KPI | Target |
| --- | --- |
| Folder service subscribes to Tenants events | working in AppHost |
| Tenant removal revokes Folder access | verified E2E |
| Disabled tenant blocks Folder commands | verified unit + E2E |
| Duplicate tenant events are safe | verified integration |
| Cross-tenant folder access leaks | zero |
| Tenant-event projection lag | observable and alertable |
| Time to run local AppHost | documented and repeatable |
| Production Dapr access policy | deny-by-default reviewed |

---

## Research Synthesis

# Integrating Hexalith.Tenants into Hexalith.Folders: Comprehensive Technical Research

## Executive Summary

`Hexalith.Tenants` is a .NET 10, Dapr, Aspire, and Hexalith.EventStore-based tenant-management module. It owns tenant lifecycle, tenant roles, global administrators, tenant configuration, and tenant read models. The Folder management application should not duplicate these responsibilities. Instead, it should become a separate Folder bounded context that consumes tenant facts and enforces them in Folder-specific commands, queries, projections, and UI flows.

The most important implementation decision is identity separation. Tenant-management commands use `tenant = "system"` and `domain = "tenants"` because Tenants manages tenants at the platform level. Normal Folder data must use the managed tenant ID, for example `{managedTenantId}:folders:{folderId}`. Mixing those two identity schemes is the highest-risk architectural mistake because it can produce cross-tenant data leaks or platform-tenant pollution.

The practical integration path is incremental: scaffold the Folder solution, wire the AppHost with EventStore/Tenants/Folder services, subscribe Folder to `system.tenants.events`, build a durable local tenant-access projection, enforce fail-closed authorization, then add Folder commands/queries and UI composition.

**Key Technical Findings:**

- The root `Hexalith.Folders` repository currently has submodules and planning output but no Folder source projects.
- `Hexalith.Tenants.Contracts`, `Hexalith.Tenants.Server`, and `Hexalith.Tenants.Aspire` verified publicly as NuGet `3.11.1`; `Hexalith.Tenants.Client` exists locally but did not verify as a public NuGet package during research.
- Tenants publishes events on `system.tenants.events`; Dapr pub/sub is at-least-once, so Folder handlers must be idempotent.
- Folder should maintain a local tenant-access projection for fast authorization, with an authoritative fallback or fail-closed behavior for stale/missing projection data.
- Production Dapr access control must be deny-by-default with mTLS; the local Tenants `accesscontrol.yaml` is explicitly development-only.

**Top Recommendations:**

1. Scaffold `Hexalith.Folders` as a separate EventStore-compatible bounded context before adding Tenants wiring.
2. Use a project reference to `Hexalith.Tenants.Client` until package publication is confirmed.
3. Add `AddHexalithTenants()`, `UseCloudEvents()`, `MapSubscribeHandler()`, and `MapTenantEventSubscription()` to the Folder service.
4. Store only minimal tenant-derived state in Folder: tenant status, member roles, Folder-owned tenant configuration keys, and event provenance.
5. Add security tests for disabled tenants, removed users, duplicate events, stale projections, wrong-tenant route IDs, and cross-tenant folder ID guessing.

## Table of Contents

1. Research Scope and Methodology
2. Technology Stack and Dependencies
3. Integration Model
4. Architecture Decisions
5. Implementation Roadmap
6. Security and Isolation Requirements
7. Testing and Operations
8. Risk Register
9. Source Verification and Confidence
10. Final Recommendation

## 1. Research Scope and Methodology

The research goal was to find all needed information to integrate `Hexalith.Tenants` into a Folder management application. The analysis covered:

- local source files in `Hexalith.Tenants`, `Hexalith.EventStore`, and `Hexalith.FrontComposer`;
- package metadata and public repository availability;
- Dapr pub/sub, Aspire, CloudEvents, and Problem Details standards;
- EventStore command/query API references;
- OWASP API object-level authorization risk guidance;
- operational and migration practices from Microsoft/Azure architecture guidance.

The local repository state is significant: `Hexalith.Folders` is currently an integration workspace with submodules, not yet a working Folder application. That shapes the recommendation toward scaffolding first, then integrating.

## 2. Technology Stack and Dependencies

Recommended Folder stack:

- `net10.0`, nullable enabled, warnings as errors, centralized package management.
- ASP.NET Core for service/API hosts.
- Hexalith.EventStore for command/query/event infrastructure.
- Dapr for service invocation, pub/sub, state store, access control, and resiliency.
- .NET Aspire for local topology and AppHost orchestration.
- OpenTelemetry for traces and metrics.
- xUnit v3, Shouldly, and Dapr/Aspire integration tests.

Recommended Tenants dependencies:

| Folder area | Dependency |
| --- | --- |
| Shared roles/events/status | `Hexalith.Tenants.Contracts` |
| Runtime event subscription | `Hexalith.Tenants.Client` project reference or package |
| Local/test tenant setup | `Hexalith.Tenants.Testing` |
| AppHost topology | `Hexalith.Tenants.Aspire` |
| Tenant domain hosting | Avoid unless Folder intentionally hosts tenant logic |

## 3. Integration Model

Folder should integrate through four seams:

- **Command API:** Tenant management writes go through EventStore `/api/v1/commands` with `tenant = "system"`, `domain = "tenants"`.
- **Folder domain commands:** Folder writes go through EventStore with the managed tenant ID and `domain = "folders"`.
- **Tenant events:** Folder subscribes to `system.tenants.events` through Dapr and maps `/tenants/events`.
- **Tenant reads:** Folder admin/query flows may call Tenants query endpoints when authoritative current state is needed.

Minimum service wiring:

```csharp
builder.Services.AddHexalithTenants(options => {
    options.PubSubName = "pubsub";
    options.TopicName = "system.tenants.events";
});

WebApplication app = builder.Build();
app.UseCloudEvents();
app.MapSubscribeHandler();
app.MapTenantEventSubscription();
```

Folder-specific handlers should be added for `UserAddedToTenant`, `UserRemovedFromTenant`, `UserRoleChanged`, `TenantDisabled`, `TenantEnabled`, and `TenantConfigurationSet/Removed` for keys under `folders.*`.

## 4. Architecture Decisions

The core architecture is a bounded-context composition:

| Context | Responsibility |
| --- | --- |
| Tenants | tenant lifecycle, roles, global admins, tenant config |
| Folders | folder hierarchy, folder events, folder projections, folder-specific policies |
| EventStore | command/query gateway, event persistence, publication, status |
| Dapr/Aspire | service topology, sidecars, pub/sub, state, local orchestration |

Folder aggregate identity should follow:

```text
{managedTenantId}:folders:{folderId}
```

Do not store Folder data under:

```text
system:tenants:{tenantId}
```

That identity is reserved for tenant-management state.

Suggested local projection:

```csharp
public sealed class FolderTenantAccessProjection
{
    public required string TenantId { get; init; }
    public TenantStatus Status { get; set; }
    public Dictionary<string, TenantRole> Members { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> FolderConfiguration { get; } = new(StringComparer.Ordinal);
    public string? LastTenantEventMessageId { get; set; }
    public long LastTenantEventSequenceNumber { get; set; }
    public DateTimeOffset LastUpdatedAt { get; set; }
}
```

## 5. Implementation Roadmap

1. Scaffold Folder projects: `Contracts`, `Server`, service host, `Client`, `Aspire`, `AppHost`, `ServiceDefaults`, `Testing`, and tests.
2. Add sibling submodule/project references without recursive nested submodule updates.
3. Compose AppHost with EventStore, Tenants, Folder service, optional Folder UI, Dapr state/pubsub, and optional Keycloak.
4. Register Tenants client services and subscription endpoint in Folder.
5. Implement durable Folder tenant-access projection.
6. Implement fail-closed authorization service using local projection plus optional authoritative Tenants queries.
7. Implement Folder command/query contracts using managed tenant IDs.
8. Add tenant-event handlers for membership, status, and `folders.*` configuration.
9. Add test tiers: unit, fake-backed, Dapr integration, Aspire E2E.
10. Add OpenTelemetry metrics, production Dapr policies, and runbooks.
11. Add FrontComposer UI only after backend security is proven.

## 6. Security and Isolation Requirements

Folder must enforce object-level authorization. OWASP identifies broken object-level authorization as the top API security risk for APIs, which is directly relevant because Folder routes and commands will expose tenant and folder identifiers.

Fail-closed rules:

- Unknown tenant: deny unless an admin flow performs authoritative Tenants lookup.
- Disabled tenant: deny Folder state-changing commands.
- Missing membership: deny private Folder reads and writes.
- Stale local projection: deny high-risk operations or re-query authoritative Tenants state.
- Global administrator: do not automatically bypass Folder object policies unless explicitly specified.

Required layers:

- JWT validation.
- Tenant claim validation where applicable.
- Local tenant projection validation.
- Folder object policy validation.
- EventStore command/query validation.
- Production Dapr deny-by-default access control with mTLS.

## 7. Testing and Operations

Required tests:

- disabled tenant blocks Folder command;
- enabled tenant restores access after event processing;
- user removal revokes Folder access;
- role downgrade removes write permission;
- duplicate Tenants events are idempotent;
- `folders.*` configuration keys are applied and unrelated keys ignored;
- wrong-tenant route or command aggregate is rejected;
- folder ID guessing across tenants does not leak existence;
- missing/stale tenant projection fails closed;
- integer event enums and string query enums are both handled safely where needed.

Operational metrics:

| Metric | Purpose |
| --- | --- |
| `folders.tenant_event.duration` | event handler latency |
| `folders.tenant_projection.lag` | tenant-access projection freshness |
| `folders.authorization.denied` | security/support signal |
| `folders.command.duration` | write-path performance |
| `folders.query.duration` | read-path performance |
| `folders.tenant_event.duplicate` | duplicate delivery visibility |

## 8. Risk Register

| Risk | Severity | Mitigation |
| --- | --- | --- |
| Folder source not scaffolded | High | Scaffold before integration |
| `Hexalith.Tenants.Client` package unavailable | High | Use local project reference; publish package before release |
| Stale tenant projection allows access | High | Fail closed; add freshness checks and authoritative fallback |
| Dapr duplicate event side effects | High | Idempotent handlers plus shared dedup store |
| Folder data stored under `system` | High | Enforce managed tenant aggregate identity |
| Dev Dapr access control copied to prod | High | Production deny-by-default review |
| Global admin over-bypass | Medium | Explicit admin policy and tests |
| Enum serialization mismatch | Medium | Test event integers and query strings |
| Nested submodule update churn | Medium | Only root-level submodule updates by default |

## 9. Source Verification and Confidence

Primary local sources:

- `Hexalith.Tenants/README.md`
- `Hexalith.Tenants/docs/quickstart.md`
- `Hexalith.Tenants/docs/event-contract-reference.md`
- `Hexalith.Tenants/docs/idempotent-event-processing.md`
- `Hexalith.Tenants/src/Hexalith.Tenants.Client/*`
- `Hexalith.Tenants/src/Hexalith.Tenants.Aspire/HexalithTenantsExtensions.cs`
- `Hexalith.Tenants/src/Hexalith.Tenants.AppHost/Program.cs`
- `Hexalith.EventStore/docs/reference/command-api.md`
- `Hexalith.EventStore/docs/reference/query-api.md`
- `Hexalith.FrontComposer/docs/skills/frontcomposer/*`

Primary public sources:

- NuGet `Hexalith.Tenants.Contracts`: https://www.nuget.org/packages/Hexalith.Tenants.Contracts
- NuGet `Hexalith.Tenants.Server`: https://www.nuget.org/packages/Hexalith.Tenants.Server
- NuGet `Hexalith.Tenants.Aspire`: https://www.nuget.org/packages/Hexalith.Tenants.Aspire
- GitHub `Hexalith.Tenants`: https://github.com/Hexalith/Hexalith.Tenants
- Dapr pub/sub overview: https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-overview/
- Dapr subscription methods: https://docs.dapr.io/developing-applications/building-blocks/pubsub/subscription-methods/
- Aspire Dapr integration: https://aspire.dev/integrations/frameworks/dapr/
- CloudEvents spec: https://github.com/cloudevents/spec
- RFC 9457 Problem Details: https://www.rfc-editor.org/rfc/rfc9457
- Microsoft CQRS pattern: https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs
- Microsoft Event Sourcing pattern: https://learn.microsoft.com/en-us/azure/architecture/patterns/event-sourcing
- Microsoft event-driven architecture: https://learn.microsoft.com/en-us/azure/architecture/guide/architecture-styles/event-driven
- Microsoft microservice boundaries: https://learn.microsoft.com/en-us/azure/architecture/microservices/model/microservice-boundaries
- Azure Well-Architected Operational Excellence: https://learn.microsoft.com/en-us/azure/well-architected/operational-excellence/
- OWASP API Security Project: https://owasp.org/www-project-api-security/
- OWASP API Top 10 2023 release: https://owasp.org/blog/2023/07/03/owasp-api-top10-2023

Confidence levels:

- **High:** local code facts, dependency patterns, Dapr/Aspire wiring, identity model, event topic, testing needs.
- **Medium:** public availability of every Tenants package because `Hexalith.Tenants.Client` did not verify publicly during research.
- **Medium:** final Folder project structure because no Folder source currently exists; the structure is inferred from EventStore/Tenants conventions.

## 10. Final Recommendation

Proceed with Tenants integration only after scaffolding the Folder application structure. The correct first implementation story is not event handler code; it is solution structure and AppHost composition. Once that is in place, implement the Tenants consumer endpoint and local tenant-access projection before building Folder commands that depend on tenant authorization.

The safe target architecture is:

```text
EventStore = gateway/source-of-truth pipeline
Tenants    = source of truth for tenant facts
Folders    = source of truth for folder facts
Dapr       = sidecar integration and pub/sub
Aspire     = local topology and developer orchestration
```

This keeps responsibilities clean, preserves tenant isolation, supports event-driven access revocation, and gives Folder a clear path to production hardening.

**Technical Research Completion Date:** 2026-05-05
**Source Verification:** Current public sources plus local source inspection
**Technical Confidence Level:** High for architecture and integration path; medium for `Hexalith.Tenants.Client` package availability and final Folder project shape

<!-- Content will be appended sequentially through research workflow steps -->
