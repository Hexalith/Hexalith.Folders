---
stepsCompleted: [1, 2, 3, 4, 5, 6]
inputDocuments: []
workflowType: 'research'
lastStep: 6
research_type: 'technical'
research_topic: 'Hexalith.EventStore domain aggregate management in Hexalith.Folders'
research_goals: 'Find everything needed to use Hexalith.EventStore to manage domain aggregates in this application: Organizations, Repositories/Folders, and related entities.'
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

This research evaluates how `Hexalith.Folders` should use `Hexalith.EventStore` to manage domain aggregates for organizations, folders/repositories, and related workflows. The work combines local source analysis of the `Hexalith.EventStore` submodule with current web verification from Microsoft architecture guidance, DAPR documentation, Aspire documentation, OpenTelemetry documentation, and NuGet package metadata.

The key conclusion is that `Hexalith.Folders` should start with two EventStore-backed aggregate roots: `OrganizationAggregate` for folder-specific organization configuration and `FolderAggregate` for folder lifecycle, storage mode, repository binding, workspace readiness, ACL overrides, and file-operation metadata. Repository concepts should initially remain part of folder state unless they gain independent commands, ACLs, lifecycle, or workflows.

The full synthesis at the end of this document provides the executive summary, final architecture, implementation roadmap, risks, and source verification. The recommended approach is incremental: define contracts and aggregate tests first, wire the Folder domain service through `AddEventStore()`/`UseEventStore()`, add projections and query paths, then introduce Git/workspace workers and production DAPR hardening.

---

<!-- Content will be appended sequentially through research workflow steps -->

## Technical Research Scope Confirmation

**Research Topic:** Hexalith.EventStore domain aggregate management in Hexalith.Folders
**Research Goals:** Find everything needed to use Hexalith.EventStore to manage domain aggregates in this application: Organizations, Repositories/Folders, and related entities.

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

## Technology Stack Analysis

### Programming Languages

`Hexalith.Folders` should model its aggregate domain in C# on the same .NET stack as `Hexalith.EventStore`. The local `Hexalith.EventStore` source uses .NET 10 package baselines, C# records for immutable command/event payloads, and ASP.NET Core Minimal API hosting for the sample domain service. The current public GitHub README describes `Hexalith.EventStore` as a DAPR-native event sourcing server for .NET and shows aggregate logic as typed C# `Handle(Command, State?) -> DomainResult` methods.

For folder management, the aggregate implementation should therefore be C# records/classes organized around command, event, state, aggregate, projection, and query types:

- Commands: `CreateOrganization`, `ConfigureGitProvider`, `CreateFolder`, `ConvertFolderToGitBacked`, `WriteFile`, `MoveFile`, `ArchiveFolder`, `GrantFolderAccess`, etc.
- Events: `OrganizationCreated`, `GitProviderConfigured`, `FolderCreated`, `FolderArchived`, `FolderGitRepositoryBound`, `FileWritten`, `FileMoved`, `FolderAccessGranted`, etc.
- State classes: `OrganizationState`, `FolderState`, and possibly separate provider/repository policy state when the aggregate grows.
- Aggregate classes: `OrganizationAggregate : EventStoreAggregate<OrganizationState>` and `FolderAggregate : EventStoreAggregate<FolderState>`.

_Popular Languages:_ C#/.NET is the concrete implementation language in this workspace.
_Emerging Languages:_ Not relevant for the domain aggregate implementation. DAPR makes non-.NET subscribers possible, but the aggregate processor should stay .NET for local consistency.
_Language Evolution:_ Use modern C# records for command/event payloads and classes for mutable replay state, following the EventStore sample.
_Performance Characteristics:_ Aggregate decision logic is CPU-light; most performance risk sits in replay length, snapshot policy, file/Git side effects, and projection fan-out rather than language choice.
_Sources:_ https://github.com/Hexalith/Hexalith.EventStore, local files `Hexalith.EventStore/README.md`, `Hexalith.EventStore/samples/Hexalith.EventStore.Sample/Counter/CounterAggregate.cs`, `Hexalith.EventStore/samples/Hexalith.EventStore.Sample/Counter/State/CounterState.cs`.

### Development Frameworks and Libraries

The central framework dependency is `Hexalith.EventStore`. Public package metadata from the NuGet flat-container API shows the current `Hexalith.EventStore.Contracts`, `Client`, `Server`, and `Aspire` package lines include version `3.11.1` as of 2026-05-05. The local package guide defines the roles:

- `Hexalith.EventStore.Contracts`: command/event/result/identity contracts.
- `Hexalith.EventStore.Client`: aggregate/projection registration and `IDomainProcessor` support.
- `Hexalith.EventStore.Server`: aggregate actors, command routing, DAPR state/pub-sub integration.
- `Hexalith.EventStore.Testing`: builders and fakes for unit/integration tests.
- `Hexalith.EventStore.Aspire`: Aspire orchestration helpers.
- `Hexalith.EventStore.SignalR`: read-model invalidation client support.

`Hexalith.Folders` should install/reference `Contracts` and `Client` in the folder domain service; `Server` in the EventStore host if it owns hosting; `Testing` in tests; `Aspire` in the AppHost; and optionally `SignalR` in UI clients that need projection invalidation.

The aggregate framework discovers aggregate and projection classes via `builder.Services.AddEventStore()` and activates them with `app.UseEventStore()`. Domain names are convention-derived by stripping suffixes such as `Aggregate` or `Projection` and converting type names to kebab-case. Use `[EventStoreDomain("organization")]` and `[EventStoreDomain("folder")]` if the default names do not match the desired routing contract.

_Major Frameworks:_ ASP.NET Core Minimal APIs, Hexalith.EventStore Client/Server/Contracts/Testing/Aspire/SignalR, DAPR, MediatR, FluentValidation, .NET Aspire.
_Micro-frameworks:_ `EventStoreAggregate<TState>` for aggregate dispatch, `EventStoreProjection<TReadModel>` for replay-based read models, keyed DI registration for domain routing.
_Evolution Trends:_ The public GitHub repository reports latest release `v3.11.1` on 2026-05-05; local packages and docs should be treated as the authoritative implementation baseline for this workspace.
_Ecosystem Maturity:_ The EventStore module already includes docs, generated API reference, samples, testing fakes, admin UI/API, and Aspire deployment samples.
_Sources:_ https://github.com/Hexalith/Hexalith.EventStore, https://api.nuget.org/v3-flatcontainer/hexalith.eventstore.contracts/index.json, https://api.nuget.org/v3-flatcontainer/hexalith.eventstore.client/index.json, https://api.nuget.org/v3-flatcontainer/hexalith.eventstore.server/index.json, https://api.nuget.org/v3-flatcontainer/hexalith.eventstore.aspire/index.json, local files `Hexalith.EventStore/docs/reference/nuget-packages.md`, `Hexalith.EventStore/src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs`, `Hexalith.EventStore/src/Hexalith.EventStore.Client/Registration/EventStoreHostExtensions.cs`.

### Database and Storage Technologies

`Hexalith.EventStore` stores aggregate streams, snapshots, metadata, command status, and idempotency records through DAPR state management. DAPR's official state-management documentation describes key/value state stores, ETag-based optimistic concurrency, transactional writes, actor-state configuration with `actorStateStore: true`, and direct read-only querying caveats. The local EventStore docs add the concrete identity-derived key patterns:

- Actor ID: `{tenant}:{domain}:{aggregateId}`
- Event stream: `{tenant}:{domain}:{aggregateId}:events:{seq}`
- Metadata: `{tenant}:{domain}:{aggregateId}:metadata`
- Snapshot: `{tenant}:{domain}:{aggregateId}:snapshot`
- Pipeline checkpoint: `{tenant}:{domain}:{aggregateId}:pipeline:{correlationId}`
- Pub/sub topic: `{tenant}.{domain}.events`

For `Hexalith.Folders`, domain aggregate state should not be stored directly in a database by the domain service. The domain service returns events; EventStore persists envelopes. Physical file content and Git working-copy data are separate operational storage concerns and should be referenced by event payload metadata, hashes, paths, repository IDs, commit SHAs, or external storage references rather than embedded as large event payloads.

_Relational Databases:_ PostgreSQL is a viable DAPR state-store backend and appears in local EventStore sample component folders.
_NoSQL Databases:_ Cosmos DB and Redis are relevant DAPR-backed state-store options; EventStore docs describe backend portability.
_In-Memory Databases:_ Redis is used for local/sample state and pub/sub in EventStore docs and components.
_Data Warehousing:_ Not part of aggregate management. Analytics should consume published events into projections or external pipelines.
_Sources:_ https://docs.dapr.io/developing-applications/building-blocks/state-management/state-management-overview, https://github.com/Hexalith/Hexalith.EventStore, local files `Hexalith.EventStore/docs/concepts/identity-scheme.md`, `Hexalith.EventStore/docs/concepts/architecture-overview.md`, `Hexalith.EventStore/docs/concepts/event-envelope.md`.

### Development Tools and Platforms

The local workspace uses a multi-module repository with root-level submodules: `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.FrontComposer`, and `Hexalith.AI.Tools`. Existing instructions explicitly prohibit recursive nested submodule initialization unless requested. For `Hexalith.Folders`, keep root-level submodule references only and do not use `git submodule update --init --recursive`.

Local EventStore development is oriented around .NET SDK, Docker, DAPR CLI, and Aspire. Aspire DAPR documentation says the DAPR CLI must be installed and initialized with `dapr init`, and shows `WithDaprSidecar()` plus DAPR state-store registration in the AppHost. EventStore's local docs and `Directory.Packages.props` use Aspire, DAPR packages, MediatR, FluentValidation, OpenTelemetry, xUnit v3, Shouldly, NSubstitute, Testcontainers, and Playwright.

_IDE and Editors:_ Visual Studio, VS Code, or Rider are compatible. Use the repository's `dotnet` commands and generated docs rather than editor-specific assumptions.
_Version Control:_ Git with root-level submodules only by default.
_Build Systems:_ .NET SDK, central package management, Aspire AppHost for distributed local runs.
_Testing Frameworks:_ xUnit v3, Shouldly, NSubstitute, EventStore.Testing fakes/builders, Testcontainers for infrastructure tests.
_Sources:_ https://aspire.dev/integrations/frameworks/dapr/, local files `AGENTS.md`, `Hexalith.EventStore/Directory.Packages.props`, `Hexalith.EventStore/docs/reference/nuget-packages.md`, `Hexalith.EventStore/AGENTS.md`.

### Cloud Infrastructure and Deployment

`Hexalith.EventStore` is DAPR-native. DAPR provides the state management, pub/sub, service invocation, actors, and configuration building blocks used by the EventStore topology. The official DAPR actors documentation confirms actors are identified by actor ID, support virtual activation, persist state through a configured state provider, and use a turn-based access model. That maps directly to EventStore's per-aggregate `AggregateActor` model.

For `Hexalith.Folders`, use the EventStore server as the write-side gateway and host the folder domain service behind DAPR service invocation. The domain service should expose `/process` for command handling and optionally `/project` for projection handlers, following the sample. Deployments can swap Redis/PostgreSQL/Cosmos/Kafka/Service Bus style backends through DAPR components without changing domain aggregate code.

_Major Cloud Providers:_ Azure is implied by the Hexalith/Aspire/DAPR deployment samples, but the domain design should stay provider-neutral.
_Container Technologies:_ Docker and Kubernetes are supported by DAPR and EventStore deployment guides.
_Serverless Platforms:_ Not a primary aggregate-management target. Event consumers could be serverless if they consume CloudEvents-compatible messages.
_CDN and Edge Computing:_ Not relevant to aggregate command processing; file delivery may later need separate storage/CDN decisions outside EventStore.
_Sources:_ https://docs.dapr.io/concepts/building-blocks-concept/, https://docs.dapr.io/developing-applications/building-blocks/actors/actors-overview/, https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-overview/, local files `Hexalith.EventStore/docs/concepts/architecture-overview.md`, `Hexalith.EventStore/docs/concepts/command-lifecycle.md`.

### Technology Adoption Trends

The useful trend for this application is not a broad market trend; it is a local architectural convergence: `Hexalith.Folders` is being designed as a command/event-driven, tenant-scoped module that reuses `Hexalith.EventStore` for aggregate mechanics and `Hexalith.Tenants` for tenant ownership context. The prior BMad brainstorming artifact already states that `Hexalith.Folders` owns organization and folder aggregates, tenants are managed by `Hexalith.Tenants`, aggregate mechanics are through `Hexalith.EventStore`, UI composition is through `Hexalith.FrontComposer`, and events are per aggregate.

The public `Hexalith.GitStorage.Events` package is relevant as a domain reference point: its README says it manages Git storage providers, organizations, and repositories through DDD, CQRS, and event-sourcing patterns, and lists domain events for Git storage accounts, organizations, and repositories. `Hexalith.Folders` should reuse the same conceptual split where useful, but adapt it to the AI workspace storage boundary: organizations own Git credentials/provider bindings; folders represent local or Git-backed workspaces; repositories are backing resources of Git-backed folders, not necessarily the top-level user-facing aggregate.

_Migration Patterns:_ Start with explicit Folder and Organization aggregate streams, then add provider/repository policy projections as requirements stabilize.
_Emerging Technologies:_ DAPR/Aspire integration provides the distributed-app host and sidecar model; EventStore wraps it in domain-specific event sourcing.
_Legacy Technology:_ Avoid direct repository classes that mutate database rows as the source of truth; use projections for read models.
_Community Trends:_ Event-driven and DAPR-backed architectures favor portable infrastructure contracts, but this module should follow the established Hexalith codebase patterns over generic external templates.
_Sources:_ https://www.nuget.org/packages/Hexalith.GitStorage.Events/1.4.0, https://github.com/Hexalith/Hexalith.EventStore, local file `_bmad-output/brainstorming/brainstorming-session-20260505-070846.md`.

## Integration Patterns Analysis

### API Design Patterns

`Hexalith.Folders` should integrate with `Hexalith.EventStore` through the existing command and query APIs rather than creating an aggregate-specific repository API. The write API is asynchronous: callers submit a command to `POST /api/v1/commands`, receive `202 Accepted`, then poll `GET /api/v1/commands/status/{correlationId}` until a terminal command status is returned. The command request contains `messageId`, `tenant`, `domain`, `aggregateId`, `commandType`, `payload`, optional `correlationId`, and optional `extensions`.

Recommended routing coordinates:

- Organization aggregate: `tenant = <tenant-id>`, `domain = organization`, `aggregateId = <organization-id>`.
- Folder aggregate: `tenant = <tenant-id>`, `domain = folder`, `aggregateId = <folder-id>`.
- Repository/provider state: store as organization/folder events first; only promote to separate aggregate if repository lifecycle becomes independently commandable.

Example Folder write request shape:

```json
{
  "messageId": "01HX...",
  "tenant": "acme",
  "domain": "folder",
  "aggregateId": "folder-123",
  "commandType": "CreateFolder",
  "payload": {
    "organizationId": "org-001",
    "name": "Product Docs",
    "storageMode": "git-backed",
    "repositoryName": "product-docs"
  },
  "extensions": {
    "domain-service-version": "v1",
    "x-correlation-origin": "chatbot"
  }
}
```

The read API is synchronous: callers submit a query to `POST /api/v1/queries` with `tenant`, `domain`, `aggregateId`, `queryType`, optional `projectionType`, optional `payload`, and optional `entityId`. Use query endpoints for Folder status/read models, not direct state-store reads. The EventStore API also exposes preflight validation endpoints for command/query authorization checks before UI or chatbot clients submit work.

_RESTful APIs:_ Use EventStore REST endpoints as the public write/read boundary. Avoid direct aggregate-specific HTTP endpoints that bypass EventStore.
_GraphQL APIs:_ Not needed for the write path. If a future UI wants rich composition, expose GraphQL over projections/read models only.
_RPC and gRPC:_ DAPR may use HTTP/gRPC internally, but the domain service contract is the EventStore `/process` HTTP method by convention.
_Webhook Patterns:_ GitHub/Forgejo webhooks should enter as integration events or commands that reconcile Folder/Git state; they should not mutate aggregate state directly.
_Sources:_ https://github.com/Hexalith/Hexalith.EventStore, local files `Hexalith.EventStore/docs/reference/command-api.md`, `Hexalith.EventStore/docs/reference/query-api.md`, `Hexalith.EventStore/src/Hexalith.EventStore/Controllers/CommandsController.cs`, `Hexalith.EventStore/src/Hexalith.EventStore/Controllers/QueriesController.cs`, `Hexalith.EventStore/src/Hexalith.EventStore/Models/SubmitCommandRequest.cs`, `Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Queries/SubmitQueryRequest.cs`.

### Communication Protocols

The integration topology has four distinct communication paths:

1. Client to EventStore Command API: HTTPS + JSON + JWT Bearer token.
2. EventStore AggregateActor to Folder domain service: DAPR service invocation with `DomainServiceRequest(CommandEnvelope, CurrentState)`.
3. EventStore to event consumers/projections: DAPR pub/sub topics using CloudEvents envelopes.
4. UI/read clients: HTTP Query API with optional `If-None-Match`/ETag plus optional SignalR projection-change notifications.

DAPR's service invocation docs describe service invocation as a reverse-proxy-like API with built-in service discovery, tracing, metrics, error handling, and encryption. DAPR pub/sub docs state DAPR wraps messages with CloudEvents by default. CloudEvents 1.0 requires common event metadata fields and gives cross-platform subscribers a standard envelope. This supports non-.NET consumers if a later Folder integration needs to consume events outside Hexalith.

_HTTP/HTTPS Protocols:_ Public command/query and domain `/process` endpoints use JSON over HTTP.
_WebSocket Protocols:_ SignalR is used only for projection invalidation signals, not for command results or event replay.
_Message Queue Protocols:_ Use DAPR pub/sub abstraction; concrete broker can be Redis Streams, RabbitMQ, Kafka, Azure Service Bus, etc. based on deployment.
_grpc and Protocol Buffers:_ Not required in the Folder domain. DAPR can use gRPC internally, but Folder command/event payloads should remain JSON-compatible C# records unless a measured payload-size problem appears.
_Sources:_ https://docs.dapr.io/developing-applications/building-blocks/service-invocation/service-invocation-overview/, https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-overview/, https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md, local files `Hexalith.EventStore/src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs`, `Hexalith.EventStore/src/Hexalith.EventStore.Server/Events/EventPublisher.cs`.

### Data Formats and Standards

The EventStore API expects camelCase JSON over HTTP. Command payloads are serialized to UTF-8 bytes before being wrapped in a `CommandEnvelope`; event payloads are serialized and persisted in `EventEnvelope` records. `EventEnvelope` metadata carries tenant, domain, aggregate ID, sequence number, timestamp, correlation ID, causation ID, user ID, domain service version, event type name, metadata version, and serialization format. For DAPR pub/sub, EventStore publishes the server envelope and adds CloudEvents metadata including event type, source, and ID.

For `Hexalith.Folders`, command/event payloads should be compact and metadata-rich:

- Store file change metadata, path, content hash, byte length, commit SHA, storage reference, actor/principal, and reason/correlation data.
- Do not store full file contents in event payloads except for deliberately tiny metadata/configuration files where the 1 MB EventStore request/event limits are acceptable.
- Use stable IDs in event payloads (`organizationId`, `folderId`, `repositoryId`) and keep human-readable names mutable.
- Use event names as business facts: `FolderCreated`, `FolderArchived`, `RepositoryProvisioned`, `FolderGitBindingChanged`, `FileWritten`, `FileDeleted`, `AclChanged`.

_JSON and XML:_ JSON is the correct command/query/event payload format for this codebase. XML is not used.
_Protobuf and MessagePack:_ Not needed initially; `ISerializedEventPayload` exists for pre-serialized payloads if future performance requirements justify it.
_CSV and Flat Files:_ Suitable only as file contents managed by the Folder service, not aggregate integration format.
_Custom Data Formats:_ Git diffs, patches, and blobs belong in Git/local file storage; events should reference them by path/hash/commit.
_Sources:_ https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md, local files `Hexalith.EventStore/docs/concepts/event-envelope.md`, `Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Events/EventEnvelope.cs`, `Hexalith.EventStore/src/Hexalith.EventStore.Server/DomainServices/DomainServiceOptions.cs`.

### System Interoperability Approaches

Use EventStore as the command API gateway and aggregate execution boundary. `Hexalith.Folders` should be a domain service registered for `organization` and `folder` domains. EventStore resolves a domain service by static registration, config store, wildcard tenant registration, or convention where `AppId = domain` and method `process`. For local development, static registrations are the clearest option; for multi-tenant routing/versioning, use the DAPR config store with keys shaped like `{tenant}:{domain}:{version}`. Restrict write access to the config store because poisoned domain-service registrations can redirect commands.

Interoperability with adjacent modules:

- `Hexalith.Tenants`: owns tenant lifecycle and tenant identity. Folder commands should carry `tenant` and `organizationId`; Organization aggregate state can represent Folder-specific Git/provider/ACL configuration, not global tenant truth.
- `Hexalith.FrontComposer`: consumes projections/status and composes admin/maintenance UI; it should not call domain service `/process` directly.
- Chatbot module: submits commands and queries through EventStore using delegated user context, command preflight checks, and correlation IDs.
- GitHub/Forgejo providers: operational adapters perform provisioning, commits, pushes, webhook validation, and repair. Aggregate events record the durable business fact and provider resource identifiers.

_Point-to-Point Integration:_ Acceptable only for EventStore-to-domain-service invocation and provider adapters. Avoid client-to-domain-service shortcuts.
_API Gateway Patterns:_ EventStore is the gateway for aggregate writes and projection reads.
_Service Mesh:_ DAPR sidecars provide service invocation and infrastructure abstraction. A separate service mesh is optional deployment infrastructure, not a domain design requirement.
_Enterprise Service Bus:_ Not needed; use DAPR pub/sub and explicit command/process-manager flows.
_Sources:_ https://docs.dapr.io/developing-applications/building-blocks/service-invocation/service-invocation-overview/, local files `Hexalith.EventStore/src/Hexalith.EventStore.Server/DomainServices/DomainServiceResolver.cs`, `Hexalith.EventStore/src/Hexalith.EventStore.Server/DomainServices/DomainServiceRegistration.cs`, `_bmad-output/brainstorming/brainstorming-session-20260505-070846.md`.

### Microservices Integration Patterns

The EventStore actor pipeline already implements the important microservice integration mechanics for aggregates: command receipt, advisory status, command archive, actor routing, idempotency by causation ID, tenant validation, state rehydration, domain invocation, event persistence, snapshot creation, pub/sub publishing, projection update trigger, and dead-letter handling for infrastructure failures.

For Folder operations, split business decisions from side effects:

- Aggregate command decides whether a folder/repository operation is allowed and emits intent/fact events.
- Provider adapter or process manager performs external Git/local filesystem side effects when required.
- Completion/failure is recorded through follow-up commands/events such as `RepositoryProvisioningSucceeded`, `RepositoryProvisioningFailed`, `WorkspacePrepared`, or `FolderRepairRequested`.
- File write workflows should use a task/lock process outside the pure aggregate when they involve checkout, write, commit, push, and release.

_API Gateway Pattern:_ EventStore API remains the external write/read gateway.
_Service Discovery:_ DAPR app IDs and EventStore domain-service resolution locate the Folder domain service.
_Circuit Breaker Pattern:_ DAPR resiliency policies should own retries/timeouts/circuit breaking for service invocation and pub/sub; do not hand-roll retries in aggregate code.
_Saga Pattern:_ Use process managers for multi-step Git provisioning and file commit workflows. Keep aggregates pure and deterministic.
_Sources:_ https://docs.dapr.io/developing-applications/building-blocks/service-invocation/service-invocation-overview/, https://docs.dapr.io/developing-applications/building-blocks/actors/actors-overview/, local files `Hexalith.EventStore/docs/concepts/command-lifecycle.md`, `Hexalith.EventStore/src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`, `Hexalith.EventStore/src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs`.

### Event-Driven Integration

EventStore publishes events to topics shaped as `{tenant}.{domain}.events`. For Folder, this means expected topics such as `acme.organization.events` and `acme.folder.events`. Published event envelopes include metadata for ordering, tracing, tenant/domain filtering, and deserialization. Consumers should treat events as immutable facts and build read models/projections from them.

Projection integration should follow the local sample: the domain service can expose `/project`, receive a `ProjectionRequest` with tenant/domain/aggregate ID and ordered events, and return a `ProjectionResponse`. Use projections for:

- Organization readiness and provider binding status.
- Folder status, storage mode, repository binding, archive state, ACL summary, and latest sync/repair state.
- AI-oriented folder query surfaces: available folders, effective permissions, repository readiness, latest file operation summaries.

_Publish-Subscribe Patterns:_ Use tenant/domain topics. Subscribers must be idempotent because message delivery can repeat.
_Event Sourcing:_ Aggregate state is rehydrated from event streams plus snapshots. Do not persist alternate write-side state as authoritative.
_Message Broker Patterns:_ Let DAPR select broker implementation; design subscribers against CloudEvents/EventEnvelope, not a broker-specific message model.
_CQRS Patterns:_ Commands produce events; queries read projections. Folder UI/chatbot queries should not replay aggregate state ad hoc from the client.
_Sources:_ https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-overview/, https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md, local files `Hexalith.EventStore/docs/reference/query-api.md`, `Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Projections/ProjectionRequest.cs`, `Hexalith.EventStore/samples/Hexalith.EventStore.Sample/Counter/Projections/CounterProjectionHandler.cs`.

### Integration Security Patterns

The public EventStore API uses JWT Bearer authentication. Microsoft guidance for ASP.NET Core APIs states bearer tokens are sent in the `Authorization` header, APIs validate tokens and claims, and APIs should return 401 for invalid authentication and 403 for insufficient authorization. The local EventStore code requires a `sub` claim for command/query submission, transforms JWT content into `eventstore:tenant`, `eventstore:domain`, and `eventstore:permission` claims, and supports permissions such as `command:submit`, `commands:*`, `query:read`, and `queries:*`.

For `Hexalith.Folders`, security integration should be explicit:

- Use the tenant claim as the EventStore tenant coordinate.
- Use folder/organization ACLs as domain state and projections, but enforce EventStore-level tenant/domain/permission claims before command processing.
- Use command/query preflight endpoints before rendering privileged actions.
- Include delegated principal/user context through the authenticated JWT `sub`; do not accept caller-supplied user IDs in payloads as authority.
- Store Git credentials as secret references owned by organization/provider configuration; never persist secret values in aggregate events or command extensions.
- Network-isolate DAPR sidecars and restrict config-store writes, matching warnings in the EventStore resolver.

_OAuth 2.0 and JWT:_ Use OIDC/OAuth-issued access tokens, validated by EventStore, with delegated user context for chatbot/file operations.
_API Key Management:_ Do not use API keys for user-level folder operations. Provider tokens belong in secret stores and are referenced by IDs.
_Mutual TLS:_ DAPR/service mesh mTLS can protect service-to-service traffic; still keep JWT/RBAC on public APIs.
_Data Encryption:_ Event payloads are redacted in logs; secrets should remain outside payloads and extensions. Use DAPR/underlying-store encryption where required.
_Sources:_ https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0, local files `Hexalith.EventStore/src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs`, `Hexalith.EventStore/src/Hexalith.EventStore/Pipeline/AuthorizationConstants.cs`, `Hexalith.EventStore/src/Hexalith.EventStore/Authorization/ClaimsRbacValidator.cs`, `Hexalith.EventStore/src/Hexalith.EventStore/Controllers/CommandValidationController.cs`, `Hexalith.EventStore/src/Hexalith.EventStore/Controllers/QueryValidationController.cs`.

## Architectural Patterns and Design

### System Architecture Patterns

`Hexalith.Folders` should be modeled as a DDD/CQRS/event-sourced bounded context that delegates aggregate mechanics to `Hexalith.EventStore`. Microsoft architecture guidance describes event sourcing as an append-only event stream per entity, with the stream used as the system of record and projections/materialized views used for reads. This matches the local EventStore implementation: command submission goes through the EventStore API, the aggregate actor rehydrates state from snapshot plus tail events, the domain service decides command outcomes, and events are persisted and published.

Recommended aggregate boundaries:

- `OrganizationAggregate`: Folder-specific organization configuration, not global tenant lifecycle. It owns Git provider bindings, repository defaults, credential secret references, organization-level ACL baselines, provider capabilities, and archive/readiness status.
- `FolderAggregate`: Folder lifecycle and metadata. It owns create, rename, move, archive, restore, storage mode, repository binding status, workspace readiness, folder ACL overrides, file-operation audit metadata, and repair/sync status.
- `RepositoryAggregate`: do not introduce this first. Treat repositories as backing resources for folders unless repositories gain independent commands, ACLs, lifecycle, or workflows that cannot be cleanly represented as folder events.

The system boundary should keep `Hexalith.Tenants` responsible for tenant lifecycle and keep `Hexalith.Folders` responsible for organization/folder behavior inside a tenant. Cross-aggregate consistency should use process managers or sagas. Do not attempt distributed transactions across organization, folder, Git provider, projection, and external storage resources.

_Sources:_ https://learn.microsoft.com/en-us/azure/architecture/patterns/event-sourcing, https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs, local files `Hexalith.EventStore/docs/concepts/architecture-overview.md`, `Hexalith.EventStore/docs/concepts/command-lifecycle.md`, `Hexalith.EventStore/src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`, `_bmad-output/brainstorming/brainstorming-session-20260505-070846.md`.

### Design Principles and Best Practices

The domain service should stay pure and deterministic. Aggregate `Handle` methods should decide from command payload, current state, and command envelope context, then return `DomainResult.Success`, `DomainResult.Rejection`, or `DomainResult.NoOp`. They should not call DAPR, Git, databases, queues, HTTP APIs, or secret stores directly. External work belongs in process managers, adapters, or workers that react to events and submit follow-up commands.

Events should be immutable business facts, not database patches. Use names such as `FolderCreated`, `FolderGitRepositoryRequested`, `FolderGitRepositoryProvisioned`, `FolderArchived`, and `OrganizationCredentialReferenceSet`. Avoid result-only events such as `FolderStatusChanged` when the domain reason can be captured more clearly. Microsoft event sourcing guidance explicitly recommends intent-focused event design because it improves auditability and future projection flexibility.

State classes should be boring and complete: every event that changes aggregate state needs an `Apply` path. Stable IDs should be authoritative; display names and paths are mutable. Business denials should be persisted as rejection events when they are meaningful audit facts, while infrastructure failures should remain outside aggregate history unless a process manager turns them into a domain fact such as `FolderGitRepositoryProvisioningFailed`.

Event versioning needs discipline from the first release. The local EventStore documentation says event type rename, class move, and deletion are unsafe because old event classes must remain deserializable. Prefer additive optional fields with defaults, tolerate unknown fields in consumers, and record schema/version meaning in event metadata or event type naming conventions when needed.

_Sources:_ https://learn.microsoft.com/en-us/azure/architecture/patterns/event-sourcing, local files `Hexalith.EventStore/docs/concepts/event-versioning.md`, `Hexalith.EventStore/src/Hexalith.EventStore.Aggregates/EventStoreAggregate.cs`, `Hexalith.EventStore/src/Hexalith.EventStore.Contracts/DomainServices/DomainResult.cs`, `Hexalith.EventStore/samples/Hexalith.EventStore.Sample/Counter/CounterAggregate.cs`.

### Scalability and Performance Patterns

DAPR actors provide a natural per-aggregate serialization model: one actor instance processes commands for one aggregate identity. This is a good fit for folder aggregates because most folder decisions should be scoped to one folder stream. Avoid making organization streams responsible for every folder operation; that would turn an organization into a hot aggregate. Keep organization-level commands for organization policy and provider configuration, and keep folder operations on `FolderAggregate`.

Long-lived or high-activity folders will need snapshot tuning. Microsoft event sourcing guidance treats snapshots as an optimization, not the source of truth, and the local EventStore `SnapshotManager` already supports snapshot intervals at default, domain, and tenant-domain levels. Start with conservative intervals for `folder`, then lower the interval for tenants or domains with high file-operation volume.

Do not store file content in events. Store file content in the configured file/Git backing store and put only compact references in events: path, content hash, commit ID, storage provider, operation metadata, actor/user, and correlation IDs. Respect EventStore response limits such as the domain service result event count and event size limits. For bulk file operations, prefer one command with bounded event output or a workflow/task aggregate outside the core folder aggregate if the operation can exceed limits.

Projection performance should be planned separately from write performance. The local projection orchestrator can replay ordered aggregate events to build a projection. That is sufficient for initial read models, but high-volume folder views should track processed sequence and be made idempotent so repeated delivery cannot drift the read model.

_Sources:_ https://docs.dapr.io/developing-applications/building-blocks/actors/actors-overview/, https://learn.microsoft.com/en-us/azure/architecture/patterns/event-sourcing, local files `Hexalith.EventStore/src/Hexalith.EventStore.Server/Events/SnapshotManager.cs`, `Hexalith.EventStore/src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs`, `Hexalith.EventStore/src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs`.

### Integration and Communication Patterns

Use the EventStore API as the write-side gateway. Clients and automation submit commands to `api/v1/commands`; the EventStore server invokes the Folder domain service through DAPR service invocation using a `DomainServiceRequest`; committed events are published to DAPR pub/sub topics; query clients read projections through the EventStore query API.

Multi-step work should be coordinated through event-driven process managers:

- Git repository provisioning: `FolderGitRepositoryRequested` triggers a provisioning worker; the worker calls Forgejo/GitHub/GitStorage; it submits `MarkRepositoryProvisioned` or `MarkRepositoryProvisioningFailed`.
- Workspace preparation: `FolderCreated` or `FolderGitRepositoryBound` triggers workspace setup; the worker submits `MarkWorkspacePrepared` or failure commands.
- File writes: commands produce metadata/audit events, while actual content writes happen in file/Git adapters that submit completion or drift events when required.
- Provider webhooks: translate inbound Git/provider webhooks into authenticated commands or integration events; do not mutate projections directly from webhooks.

This keeps aggregates deterministic and lets DAPR resiliency policies handle timeouts, retries, and circuit breakers around external calls. Event consumers must be idempotent because pub/sub delivery can repeat.

_Sources:_ https://docs.dapr.io/developing-applications/building-blocks/service-invocation/service-invocation-overview/, https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-overview/, https://learn.microsoft.com/en-us/azure/architecture/patterns/saga, local files `Hexalith.EventStore/docs/concepts/service-discovery.md`, `Hexalith.EventStore/src/Hexalith.EventStore.Server/DomainServices/DomainServiceResolver.cs`, `Hexalith.EventStore/src/Hexalith.EventStore.Server/Events/EventPublisher.cs`.

### Security Architecture Patterns

Security should be layered. EventStore should enforce JWT authentication, tenant/domain claims, and command/query permissions before command or query handling. Folder and organization ACLs should exist as domain state and projections, but they should complement, not replace, EventStore-level tenant/domain authorization.

Recommended security model:

- Map the authenticated user `sub` claim to command envelope user context. Do not trust user IDs embedded in command payloads as authority.
- Use `eventstore:tenant`, `eventstore:domain`, and `eventstore:permission` claims for public API access control.
- Use command/query validation endpoints for preflight authorization and UI action enablement.
- Persist only secret references for Git credentials and provider tokens. Actual secrets belong in a secret store or provider-specific credential system.
- Keep DAPR sidecars, config store, state store, and pub/sub endpoints network-restricted. The local EventStore resolver documentation warns that config-store writes can alter domain service routing.
- Keep payload logging redacted. The local `CommandEnvelope` implementation redacts payloads in `ToString`, and folder code should preserve that habit.

_Sources:_ https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0, local files `Hexalith.EventStore/src/Hexalith.EventStore/Pipeline/AuthorizationConstants.cs`, `Hexalith.EventStore/src/Hexalith.EventStore/Authorization/ClaimsRbacValidator.cs`, `Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Commands/CommandEnvelope.cs`, `Hexalith.EventStore/docs/concepts/service-discovery.md`.

### Data Architecture Patterns

Event stream identity should follow the local EventStore identity scheme:

- Organization stream: `tenant=<tenant-id>`, `domain=organization`, `aggregateId=<organization-id>`.
- Folder stream: `tenant=<tenant-id>`, `domain=folder`, `aggregateId=<folder-id>`.
- Published topics: `{tenant}.organization.events` and `{tenant}.folder.events`.

Suggested command/event families:

- Organization commands: `CreateOrganization`, `RenameOrganization`, `ArchiveOrganization`, `ConfigureGitProvider`, `UnbindGitProvider`, `SetRepositoryPolicy`, `SetCredentialReference`, `GrantOrganizationAccess`, `RevokeOrganizationAccess`.
- Organization events: `OrganizationCreated`, `OrganizationRenamed`, `OrganizationArchived`, `GitProviderBound`, `GitProviderUnbound`, `RepositoryPolicyConfigured`, `OrganizationCredentialReferenceSet`, `OrganizationAccessGranted`, `OrganizationAccessRevoked`.
- Folder commands: `CreateFolder`, `RenameFolder`, `MoveFolder`, `ArchiveFolder`, `RestoreFolder`, `ChangeFolderStorageMode`, `RequestGitRepository`, `BindExistingRepository`, `MarkRepositoryProvisioned`, `MarkRepositoryProvisioningFailed`, `PrepareWorkspace`, `MarkWorkspacePrepared`, `WriteFile`, `MoveFile`, `DeleteFile`, `GrantFolderAccess`, `RevokeFolderAccess`, `RequestFolderRepair`.
- Folder events: `FolderCreated`, `FolderRenamed`, `FolderMoved`, `FolderArchived`, `FolderRestored`, `FolderStorageModeChanged`, `FolderGitRepositoryRequested`, `FolderGitRepositoryProvisioned`, `FolderGitRepositoryProvisioningFailed`, `FolderGitRepositoryBound`, `WorkspacePrepared`, `WorkspacePreparationFailed`, `FileWritten`, `FileMoved`, `FileDeleted`, `FolderAccessGranted`, `FolderAccessRevoked`, `FolderRepairRequested`, `FolderDriftDetected`, `FolderSyncCompleted`.

Read projections should be purpose-built: organization readiness, provider binding, repository policy, folder status, folder tree/list view, effective ACLs, repository lifecycle, latest sync/repair state, file operation history, and AI/chatbot query surfaces. Do not use projections as command authority unless the command is explicitly designed for eventual consistency.

_Sources:_ https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs, local files `Hexalith.EventStore/docs/concepts/identity-scheme.md`, `Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Actors/AggregateIdentity.cs`, `Hexalith.EventStore/docs/reference/query-api.md`, `Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Projections/ProjectionRequest.cs`.

### Deployment and Operations Architecture

The deployable shape should be DAPR-native and Aspire-friendly:

- EventStore server hosts the public command/query/validation APIs and aggregate actors.
- Folder domain service registers `OrganizationAggregate` and `FolderAggregate` with `AddEventStore()` and exposes `UseEventStore()` endpoints such as `/process` and `/project`.
- DAPR components provide state store, pub/sub, service invocation, actors, and optional configuration/secret store integration.
- Workers/adapters handle GitHub/Forgejo/GitStorage provisioning, workspace setup, file writes, repair, and sync.
- FrontComposer/Admin UI and chatbot clients submit commands through EventStore and read projections.

For development, static appsettings registrations are sufficient for domain service routing. For multi-tenant or versioned deployments, use EventStore domain-service resolver registrations or DAPR config-store lookups with strict write controls. Use the `domain-service-version` command extension to route commands to `v1`, `v2`, or canary domain services when evolving behavior.

Operational controls should include correlation/causation tracing, dead-letter monitoring, projection lag visibility, replay/rebuild procedures, snapshot health, and explicit handling for Git provider outages. Test strategy should use `Hexalith.EventStore.Testing` for given-past-events/when-command/then-events aggregate tests, plus integration tests for DAPR routing, projections, idempotency, schema evolution, and worker retry behavior.

_Sources:_ https://learn.microsoft.com/en-us/dotnet/aspire/community-toolkit/hosting-dapr, https://docs.dapr.io/concepts/dapr-concepts/building-blocks-concept/, local files `Hexalith.EventStore/docs/concepts/service-discovery.md`, `Hexalith.EventStore/docs/concepts/snapshots.md`, `Hexalith.EventStore/docs/concepts/command-lifecycle.md`, `Hexalith.EventStore/src/Hexalith.EventStore.Aspire/README.md`, `Hexalith.EventStore/tests/Hexalith.EventStore.Testing/README.md`.

## Implementation Approaches and Technology Adoption

### Technology Adoption Strategies

Adopt `Hexalith.EventStore` incrementally. Microsoft modernization guidance recommends phased modernization because it reduces risk, produces measurable value between phases, and lets the team adjust after each phase. For this application, do not begin by building every aggregate, projection, worker, and UI surface. Start with the smallest EventStore-backed vertical slice that proves the write path, read path, and DAPR runtime path.

Recommended adoption sequence:

1. Create the domain contract package for organization/folder commands, events, state, and projections.
2. Implement `OrganizationAggregate` and `FolderAggregate` with pure `Handle` methods and state `Apply` methods.
3. Register the domain service with `AddEventStore()` and `UseEventStore()`, expose `/process`, and wire EventStore server routing for `organization` and `folder`.
4. Add projection handlers for organization readiness and folder status.
5. Add Git/workspace workers that react to events and submit follow-up commands.
6. Add UI/chatbot command clients only after validation/query paths are stable.

This should be treated as a rearchitecture of the Folder write model, but not a full platform rewrite. Keep `Hexalith.Tenants` as the tenant lifecycle source and keep external file/Git storage behind adapters.

_Sources:_ https://learn.microsoft.com/en-us/azure/cloud-adoption-framework/modernize/plan-cloud-modernization, local files `Hexalith.EventStore/README.md`, `Hexalith.EventStore/samples/Hexalith.EventStore.Sample/Program.cs`, `Hexalith.EventStore/samples/Hexalith.EventStore.Sample/Counter/CounterAggregate.cs`.

### Development Workflows and Tooling

Use the current .NET workflow around solution-level builds, package references, and automated tests. `Hexalith.EventStore` already exposes the patterns to mirror:

- Domain service registration: `builder.Services.AddEventStore()`.
- Runtime activation: `app.UseEventStore()`.
- Domain processing endpoint: `MapPost("/process", ...)` accepting `DomainServiceRequest`.
- Projection endpoint: `MapPost("/project", ...)` accepting `ProjectionRequest`.
- Tests with xUnit and EventStore testing utilities.

For local orchestration, use Aspire with DAPR sidecars. The current Aspire DAPR documentation requires the DAPR CLI and `dapr init`, and uses `CommunityToolkit.Aspire.Hosting.Dapr` plus `WithDaprSidecar()` to add sidecars to AppHost resources. This fits the EventStore architecture because the server, Folder domain service, workers, and projection/read model services can all run under one local application graph.

Recommended repo workflow:

- Keep contracts, domain service, workers, and tests as separate projects.
- Use one command/event namespace per aggregate family.
- Keep test fixtures close to the aggregate tests.
- Use root-level submodules only; do not initialize nested submodules unless explicitly needed.
- Add CI gates for build, format/analyzers, unit tests, aggregate compliance tests, and integration tests.

_Sources:_ https://aspire.dev/integrations/frameworks/dapr/, https://learn.microsoft.com/en-us/dotnet/core/testing/?tabs=windows, local files `Hexalith.EventStore/tests/Hexalith.EventStore.Sample.Tests/Counter/CounterAggregateTests.cs`, `Hexalith.EventStore/tests/Hexalith.EventStore.Client.Tests/Registration/AddEventStoreTests.cs`, `Hexalith.EventStore/tests/Hexalith.EventStore.Client.Tests/Registration/UseEventStoreTests.cs`.

### Testing and Quality Assurance

Testing should follow the event-sourced shape of the system:

- Aggregate unit tests: given prior events/state, when command, then expected `DomainResult`.
- Replay tests: every event stream used by tests should replay into state without missing `Apply` methods.
- Rejection tests: invalid business commands should produce rejection events, not exceptions.
- Tombstone tests: terminated organization/folder states should reject subsequent commands through `ITerminatable` behavior.
- Identity tests: tenant/domain/aggregate IDs must match EventStore validation and storage key expectations.
- Projection tests: ordered event lists should build deterministic read models and tolerate duplicate delivery.
- Domain service integration tests: `/process` and `/project` should serialize/deserialize the same way DAPR invocation does.
- Worker tests: Git/file adapters should be idempotent and submit follow-up commands only once per causation/correlation identity.

Microsoft .NET testing guidance separates unit tests from integration tests: unit tests should avoid infrastructure concerns, while integration tests verify components working together. Apply that distinction strictly. Aggregate tests should not use DAPR, databases, network calls, or Git. DAPR, state store, pub/sub, and worker behavior belong in integration tests.

_Sources:_ https://learn.microsoft.com/en-us/dotnet/core/testing/?tabs=windows, local files `Hexalith.EventStore/tests/Hexalith.EventStore.Sample.Tests/Counter/CounterAggregateTests.cs`, `Hexalith.EventStore/src/Hexalith.EventStore.Testing/Assertions/DomainResultAssertions.cs`, `Hexalith.EventStore/src/Hexalith.EventStore.Testing/Assertions/EventSequenceAssertions.cs`, `Hexalith.EventStore/src/Hexalith.EventStore.Testing/Compliance/TerminatableComplianceAssertions.cs`.

### Deployment and Operations Practices

Local development should run through Aspire. Production can target Kubernetes or another DAPR-supported host, but production DAPR must be configured deliberately. DAPR production guidance recommends resource settings for sidecars and control-plane components, high availability for the control plane, and environment-specific tuning based on monitoring baselines.

Operational practices to implement before production:

- DAPR resiliency specs for service invocation, pub/sub, state store, and configuration store calls.
- Timeouts, retries/backoff, and circuit breakers at the DAPR layer, not in aggregate code.
- Health checks for EventStore server, Folder domain service, workers, DAPR sidecars, state store, and pub/sub.
- Dead-letter topic monitoring and replay/drain procedures.
- Projection lag metrics and rebuild procedures.
- Snapshot health monitoring and corruption fallback validation.
- Structured logs that include tenant, domain, aggregate ID, correlation ID, causation ID, and command/event type, while excluding payloads and secrets.

Use OpenTelemetry for .NET telemetry. Current OpenTelemetry .NET documentation marks traces, metrics, and logs as stable signals, and DAPR observability documentation describes tracing, logs, metrics, health checks, and trace propagation through the sidecar.

_Sources:_ https://docs.dapr.io/operations/hosting/kubernetes/kubernetes-production/, https://docs.dapr.io/concepts/resiliency-concept/, https://docs.dapr.io/concepts/observability-concept/, https://opentelemetry.io/docs/languages/dotnet/, local files `Hexalith.EventStore/src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`, `Hexalith.EventStore/src/Hexalith.EventStore.Server/Events/EventPublisher.cs`.

### Team Organization and Skills

The team needs four implementation skill areas:

- Domain modeling: commands, events, aggregate boundaries, invariants, rejections, event schema evolution.
- EventStore/DAPR runtime: domain service registration, service invocation, actors, state store, pub/sub, config store, resiliency, and sidecar operations.
- Git/file workflow engineering: provider APIs, repository provisioning, workspace preparation, idempotent file operations, repair/sync workflows, and secret references.
- Projection/UI integration: read model design, eventual consistency UX, query API use, ETag handling, SignalR/notification patterns if real-time refresh is needed.

Ownership should follow the architecture. One engineer can own command/event contracts and aggregates. Another can own EventStore/DAPR hosting and operational wiring. Another can own Git/file workers. UI/chatbot integration should depend on stable command validation and projections rather than direct aggregate access.

_Sources:_ https://learn.microsoft.com/en-us/azure/well-architected/, https://docs.dapr.io/concepts/overview/, local files `Hexalith.EventStore/docs/concepts/choose-the-right-tool.md`, `Hexalith.EventStore/docs/reference/command-api.md`, `Hexalith.EventStore/docs/reference/query-api.md`.

### Cost Optimization and Resource Management

Cost optimization should be planned by environment. Microsoft Well-Architected cost guidance emphasizes cost-management discipline, environment-specific sizing, cost baselines, dynamic scaling, and continuous review. For this application:

- Use local Aspire/DAPR for developer productivity, but do not assume local topology is production sizing.
- Keep nonproduction environments smaller and ephemeral where possible.
- Avoid over-splitting early services; start with one Folder domain service plus workers before adding separate services for every resource concept.
- Tune DAPR sidecar resources from measured baselines.
- Store file content outside EventStore to avoid state-store growth and expensive replay.
- Use snapshots for hot/long-lived streams to reduce compute during rehydration.
- Track per-tenant event volume, projection volume, Git provider calls, worker queue depth, storage growth, and dead-letter volume as cost indicators.

_Sources:_ https://learn.microsoft.com/en-us/azure/well-architected/cost-optimization/principles, https://docs.dapr.io/operations/hosting/kubernetes/kubernetes-production/, local files `Hexalith.EventStore/docs/concepts/snapshots.md`, `Hexalith.EventStore/docs/concepts/identity-scheme.md`.

### Risk Assessment and Mitigation

Primary implementation risks:

- Aggregate boundary drift: mitigate with explicit aggregate ownership and ADRs for any new aggregate.
- Event schema breaks: mitigate with additive changes, default values, retained event classes, and replay tests.
- Hot aggregates: mitigate by keeping folder operations on `FolderAggregate`, not `OrganizationAggregate`.
- Projection lag or duplicate delivery: mitigate with sequence tracking and idempotent projection handlers.
- Git provider failures: mitigate with process managers, retry policies, failure events, repair commands, and user-visible readiness status.
- Secrets in events: mitigate with secret references only and automated tests/scans for secret-shaped payloads.
- Config-store route poisoning: mitigate with strict DAPR config-store write permissions and audit.
- Local/production drift: mitigate with Aspire for local orchestration and infrastructure manifests for production DAPR components.
- Sidecar resource exhaustion: mitigate with production sidecar resource requests/limits and baseline monitoring.

_Sources:_ https://learn.microsoft.com/en-us/azure/cloud-adoption-framework/modernize/plan-cloud-modernization, https://docs.dapr.io/concepts/resiliency-concept/, https://docs.dapr.io/operations/hosting/kubernetes/kubernetes-production/, local files `Hexalith.EventStore/docs/concepts/event-versioning.md`, `Hexalith.EventStore/docs/concepts/service-discovery.md`, `Hexalith.EventStore/src/Hexalith.EventStore.Server/DomainServices/DomainServiceResolver.cs`.

## Technical Research Recommendations

### Implementation Roadmap

Phase 0: create the solution skeleton and package references. Add projects for contracts, domain service, workers, projections, tests, and AppHost. Verify packages at version `3.11.1` unless the dependency graph requires a newer compatible version.

Phase 1: implement `OrganizationAggregate`, `OrganizationState`, organization commands/events, and aggregate tests. Register with `[EventStoreDomain("organization")]` if the class name or convention is not enough.

Phase 2: implement `FolderAggregate`, `FolderState`, folder commands/events, and aggregate tests. Keep repository as folder state at this phase.

Phase 3: wire Folder domain service with `AddEventStore()`, `UseEventStore()`, `/process`, and static domain-service routing from EventStore server.

Phase 4: implement organization/folder projections and query paths for UI/chatbot use.

Phase 5: implement Git/workspace workers and process managers. Add idempotency, retry, and failure events.

Phase 6: production hardening: DAPR resiliency specs, observability, dead-letter operations, snapshot policy, security validation, performance tests, and cost telemetry.

### Technology Stack Recommendations

Use C#/.NET, `Hexalith.EventStore.Contracts`, `Hexalith.EventStore.Client`, `Hexalith.EventStore.Server`, `Hexalith.EventStore.Testing`, DAPR, Aspire, xUnit, OpenTelemetry, and the selected Git/file provider libraries. Use EventStore as the write gateway and projection/query API as the read gateway. Use external Git/file storage for contents and keep events as compact domain facts.

### Skill Development Requirements

The team should be comfortable with event sourcing, CQRS, DDD aggregate design, DAPR service invocation/pub-sub/actors/state/configuration, Aspire AppHost orchestration, .NET testing, OpenTelemetry, and secure secret-reference patterns. Git provider integration needs separate operational knowledge because provider failures and webhooks will be a major source of edge cases.

### Success Metrics and KPIs

Recommended success metrics:

- Aggregate tests cover all commands, rejections, tombstones, and replay paths.
- Domain service `/process` and `/project` pass serialization integration tests.
- Command acceptance latency and projection lag are measured per tenant/domain.
- Duplicate event delivery does not change projection results.
- Zero secret values are persisted in events, extensions, logs, or projections.
- Git provisioning workflows expose success/failure/repair status through projections.
- Dead-letter topics, snapshot health, and DAPR sidecar health are monitored.
- Nonproduction environments can be started consistently through Aspire.
- Production DAPR sidecars have tuned resource settings and resiliency specs.

# Comprehensive Technical Research: Hexalith.EventStore Domain Aggregate Management in Hexalith.Folders

## Executive Summary

`Hexalith.Folders` should use `Hexalith.EventStore` as the authoritative write-side gateway for folder-domain state. The EventStore implementation already provides the key primitives needed for aggregate management: command envelopes, aggregate identity validation, per-aggregate actors, event persistence, snapshots, pub/sub publication, query/projection APIs, DAPR-based domain service invocation, security checks, and testing helpers.

The recommended first model is intentionally small: implement `OrganizationAggregate` and `FolderAggregate`. `OrganizationAggregate` owns folder-specific organization configuration such as Git provider bindings, repository defaults, secret references, and organization-level ACL baselines. `FolderAggregate` owns folder lifecycle, storage mode, repository binding status, workspace readiness, ACL overrides, and compact file-operation metadata. A separate `RepositoryAggregate` should be deferred until repositories need independent commands, ACLs, lifecycle, or long-running workflows that cannot fit cleanly in folder state.

The strategic implementation path is phased. Start with contracts and aggregate tests, then register the Folder domain service with `AddEventStore()` and `UseEventStore()`, then add projections and query paths, then add Git/workspace workers as event-driven process managers. This keeps aggregate handlers pure and deterministic while preserving auditability, replayability, and a production-ready DAPR integration path.

**Key Technical Findings:**

- EventStore's identity model maps cleanly to `tenant/domain/aggregateId`: `organization` and `folder` should be distinct domains.
- Event-sourced aggregates should emit intent-focused events, not state-diff records.
- Folder content must stay outside events; events should hold references such as path, hash, commit ID, storage provider, and correlation metadata.
- Repository provisioning, workspace preparation, file synchronization, and provider webhooks belong in workers/process managers, not aggregate handlers.
- CQRS projections are required for UI, chatbot, and admin read paths because read clients should not replay aggregate streams directly.
- DAPR sidecars, state store, pub/sub, config store, and service invocation are core runtime dependencies, not incidental infrastructure.

**Technical Recommendations:**

- Implement `OrganizationAggregate : EventStoreAggregate<OrganizationState>` and `FolderAggregate : EventStoreAggregate<FolderState>` first.
- Use `[EventStoreDomain("organization")]` and `[EventStoreDomain("folder")]` where explicit domain naming avoids convention risk.
- Build aggregate tests using the local EventStore sample pattern: given state/events, process a command envelope, assert `DomainResult`.
- Keep all Git/provider credentials as secret references; never store secrets in command payloads, event payloads, extensions, logs, or projections.
- Add DAPR resiliency specs, OpenTelemetry tracing, dead-letter monitoring, projection lag metrics, and snapshot health before production.

## Table of Contents

1. Technical Research Introduction and Methodology
2. Technical Landscape and Architecture Analysis
3. Implementation Approaches and Best Practices
4. Technology Stack Evolution and Current Trends
5. Integration and Interoperability Patterns
6. Performance and Scalability Analysis
7. Security and Compliance Considerations
8. Strategic Technical Recommendations
9. Implementation Roadmap and Risk Assessment
10. Future Technical Outlook and Innovation Opportunities
11. Technical Research Methodology and Source Verification
12. Technical Appendices and Reference Materials

## 1. Technical Research Introduction and Methodology

### Technical Research Significance

Folder management in this application is not just CRUD over folders. The domain includes tenant-scoped organizations, provider configuration, repository backing resources, folder lifecycle, ACLs, workspace readiness, file operations, Git/provider integration, chatbot-driven commands, and eventual read models. These are exactly the kinds of workflows where event sourcing and CQRS can be useful: they preserve intent, support audit and replay, and decouple command decisions from projections and side effects.

Microsoft's event sourcing guidance describes append-only event streams as the system of record and notes that current state is reconstructed by replaying events, with materialized views used for efficient reads. Microsoft CQRS guidance describes separate write and read models, independent scaling, optimized read schemas, and the complexity of eventual consistency. DAPR documentation describes a portable, event-driven runtime with service invocation, pub/sub, state management, actors, and observability building blocks.

_Sources:_ https://learn.microsoft.com/en-us/azure/architecture/patterns/event-sourcing, https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs, https://docs.dapr.io/concepts/overview/

### Technical Research Methodology

The research used three evidence streams:

- Local source analysis of `Hexalith.EventStore`, including contracts, client aggregate APIs, server actor pipeline, domain service invocation, registration tests, sample domain service, projection code, security code, and testing helpers.
- Current web verification using Microsoft architecture docs, DAPR docs, Aspire docs, OpenTelemetry docs, ASP.NET JWT docs, and NuGet metadata.
- Project-specific synthesis from existing brainstorming output and the current `Hexalith.Folders` workspace shape.

**Original Technical Goals:** Find everything needed to use `Hexalith.EventStore` to manage domain aggregates in this application: Organizations, Repositories/Folders, and related entities.

**Achieved Technical Objectives:**

- Identified aggregate boundaries and domain names.
- Mapped command, event, state, projection, and worker responsibilities.
- Documented EventStore registration, routing, identity, security, testing, and DAPR runtime requirements.
- Produced an implementation roadmap and risk mitigation plan.

## 2. Technical Landscape and Architecture Analysis

### Current Technical Architecture Patterns

The target architecture is DDD + event sourcing + CQRS on DAPR. EventStore owns the write-side mechanics: command submission, command envelope validation, aggregate actor routing, state rehydration, domain service invocation, event persistence, snapshot optimization, event publication, and projection orchestration. `Hexalith.Folders` owns domain decisions through aggregate handlers and state application logic.

Recommended domains:

- `organization`: folder-specific organization settings, provider bindings, repository policy, credential references, ACL baseline, readiness/archive status.
- `folder`: folder lifecycle, storage mode, repository binding, workspace readiness, ACL overrides, file operation facts, repair/sync status.

The architecture should avoid a premature `repository` domain. Repository is initially a backing resource for folders. Promote it to an aggregate only if repository commands need independent consistency boundaries.

_Sources:_ https://learn.microsoft.com/en-us/azure/architecture/patterns/event-sourcing, local files `Hexalith.EventStore/docs/concepts/architecture-overview.md`, `Hexalith.EventStore/docs/concepts/identity-scheme.md`, `Hexalith.EventStore/src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`.

### System Design Principles and Best Practices

Aggregate handlers must be pure. They should inspect command payload, current state, and command envelope metadata, then return `DomainResult.Success`, `DomainResult.Rejection`, or `DomainResult.NoOp`. They should not call Git providers, HTTP APIs, DAPR, databases, secret stores, or file systems.

Events should capture business intent. Prefer `FolderGitRepositoryRequested`, `FolderGitRepositoryProvisioned`, `FolderArchived`, and `OrganizationCredentialReferenceSet` over generic state-change events. This matches Microsoft guidance that intent-focused events are more useful than state-focused change logs.

State classes must replay every persisted event. Versioning must be conservative: do not rename, move, or delete persisted event types; add optional/defaulted fields instead and test replay paths.

_Sources:_ https://learn.microsoft.com/en-us/azure/architecture/patterns/event-sourcing, local files `Hexalith.EventStore/docs/concepts/event-versioning.md`, `Hexalith.EventStore/src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs`, `Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Results/DomainResult.cs`.

## 3. Implementation Approaches and Best Practices

### Current Implementation Methodologies

Use the local EventStore sample as the implementation template:

- `builder.Services.AddEventStore()`
- `app.UseEventStore()`
- `MapPost("/process", ...)` for `DomainServiceRequest`
- optional `MapPost("/project", ...)` for `ProjectionRequest`
- aggregate classes inheriting `EventStoreAggregate<TState>`
- xUnit tests that create `CommandEnvelope` and assert `DomainResult`

Suggested project layout:

- `Hexalith.Folders.Contracts`: commands, events, state DTOs, projection DTOs, query DTOs.
- `Hexalith.Folders.Domain`: aggregates, state application, projection handlers.
- `Hexalith.Folders.Service`: domain service host with `/process` and `/project`.
- `Hexalith.Folders.Workers`: Git/repository/workspace/file process managers.
- `Hexalith.Folders.Tests`: aggregate, replay, projection, integration, worker idempotency tests.
- `Hexalith.Folders.AppHost`: Aspire/DAPR orchestration.

_Sources:_ https://learn.microsoft.com/en-us/dotnet/core/testing/?tabs=windows, https://aspire.dev/integrations/frameworks/dapr/, local files `Hexalith.EventStore/samples/Hexalith.EventStore.Sample/Program.cs`, `Hexalith.EventStore/samples/Hexalith.EventStore.Sample/Counter/CounterAggregate.cs`, `Hexalith.EventStore/tests/Hexalith.EventStore.Sample.Tests/Counter/CounterAggregateTests.cs`.

### Quality Assurance Practices

Testing must follow event-sourced behavior:

- Given prior events/state, when command, then expected events/rejections/no-op.
- Replay all event families into state.
- Test terminated states with `ITerminatable`.
- Test identity and storage key isolation.
- Test projection idempotency and duplicate delivery.
- Test DAPR serialization for `/process` and `/project`.
- Test worker idempotency for provider retries and webhook duplicates.

Aggregate tests should remain infrastructure-free. Integration tests should cover EventStore routing, DAPR invocation, projections, pub/sub, and workers.

_Sources:_ https://learn.microsoft.com/en-us/dotnet/core/testing/?tabs=windows, local files `Hexalith.EventStore/src/Hexalith.EventStore.Testing/Assertions/DomainResultAssertions.cs`, `Hexalith.EventStore/src/Hexalith.EventStore.Testing/Compliance/TerminatableComplianceAssertions.cs`.

## 4. Technology Stack Evolution and Current Trends

### Current Technology Stack Landscape

The stack should stay aligned with Hexalith:

- Language/runtime: C#/.NET.
- Event sourcing: `Hexalith.EventStore.Contracts`, `Client`, `Server`, `Testing`, `Aspire`.
- Runtime building blocks: DAPR actors, state, pub/sub, service invocation, config/secrets where required.
- Local orchestration: Aspire with DAPR sidecars.
- Auth/security: ASP.NET Core JWT Bearer and EventStore RBAC claims.
- Observability: OpenTelemetry traces, metrics, logs, plus DAPR sidecar telemetry.

NuGet package metadata checked during the research showed the current `Hexalith.EventStore` package line at `3.11.1` as of 2026-05-05 for the central packages reviewed.

_Sources:_ https://www.nuget.org/packages/Hexalith.EventStore.Contracts, https://docs.dapr.io/concepts/overview/, https://opentelemetry.io/docs/languages/dotnet/, local file `Hexalith.EventStore/docs/concepts/packages.md`.

### Technology Adoption Patterns

Adopt in phases, not as a platform rewrite. Microsoft modernization guidance recommends phased change with clear success criteria and rollback planning. For this workspace, the first success criterion should be a working command-to-event-to-projection vertical slice for one organization and one folder.

_Sources:_ https://learn.microsoft.com/en-us/azure/cloud-adoption-framework/modernize/plan-cloud-modernization

## 5. Integration and Interoperability Patterns

### Current Integration Approaches

The write path should be:

1. Client submits command to EventStore API.
2. EventStore validates JWT, tenant/domain permissions, request shape, command size, and aggregate identity.
3. Aggregate actor rehydrates state.
4. EventStore invokes Folder domain service through DAPR service invocation.
5. Domain service returns `DomainResult`.
6. EventStore persists events, updates snapshots as needed, publishes to `{tenant}.{domain}.events`, and triggers projection updates.

The read path should be:

1. Projections consume or replay ordered events.
2. Query clients call EventStore query API.
3. UI/chatbot uses projection DTOs and ETags rather than aggregate stream replay.

_Sources:_ https://docs.dapr.io/developing-applications/building-blocks/service-invocation/service-invocation-overview/, https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-overview/, local files `Hexalith.EventStore/docs/reference/command-api.md`, `Hexalith.EventStore/docs/reference/query-api.md`.

### Interoperability Standards and Protocols

Use JSON payloads, CloudEvents-compatible pub/sub metadata, HTTPS/JWT for public APIs, DAPR service invocation internally, DAPR pub/sub for event distribution, and OpenTelemetry for trace context. Do not bind domain code to a broker-specific protocol.

_Sources:_ https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md, https://docs.dapr.io/concepts/observability-concept/, https://opentelemetry.io/

## 6. Performance and Scalability Analysis

### Performance Characteristics and Optimization

The most important performance boundary is aggregate granularity. Folder operations should run on `FolderAggregate`, not through an organization-wide stream. This avoids turning organization streams into hot aggregates.

Snapshots are required for long-lived or high-volume folder streams. Microsoft Event Sourcing guidance notes snapshots reduce replay cost, and the local EventStore `SnapshotManager` supports tenant-domain, domain, and default interval configuration.

File content must not enter the event store. Events should be compact metadata and references. Large content belongs in Git/file storage.

_Sources:_ https://learn.microsoft.com/en-us/azure/architecture/patterns/event-sourcing, local files `Hexalith.EventStore/src/Hexalith.EventStore.Server/Events/SnapshotManager.cs`, `Hexalith.EventStore/src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs`.

### Scalability Patterns and Approaches

DAPR actors serialize work per aggregate identity. DAPR pub/sub supports event fan-out to projections and workers. CQRS allows read projections to scale independently from command handling. EventStore result limits and request limits should shape bulk operation design.

_Sources:_ https://docs.dapr.io/developing-applications/building-blocks/actors/actors-overview/, https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs

## 7. Security and Compliance Considerations

### Security Best Practices and Frameworks

Use EventStore's JWT and RBAC model as the first gate. Folder ACLs should be domain state and projections, but command execution should still require EventStore tenant/domain/permission claims. The authenticated JWT `sub` should become command envelope user context; payload-provided user IDs are not authority.

Secret handling is strict: store provider tokens and Git credentials outside events. Events contain secret references only. Logs should include correlation and command/event metadata, not payload bodies.

_Sources:_ https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0, local files `Hexalith.EventStore/src/Hexalith.EventStore/Pipeline/AuthorizationConstants.cs`, `Hexalith.EventStore/src/Hexalith.EventStore/Authorization/ClaimsRbacValidator.cs`, `Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Commands/CommandEnvelope.cs`.

### Compliance and Governance

Event streams are immutable audit records. That is useful for traceability, but sensitive data must be designed out of events to avoid deletion/compliance conflicts. Use references, hashes, and external stores for personal or secret data.

_Sources:_ https://learn.microsoft.com/en-us/azure/architecture/patterns/event-sourcing

## 8. Strategic Technical Recommendations

### Technical Strategy and Decision Framework

Use this decision rule:

- If the concept has independent invariants and command lifecycle, make it an aggregate.
- If it is configuration or status owned by a folder, keep it in `FolderAggregate`.
- If it is a side effect, make it a worker/process manager.
- If it is for display/search/chatbot use, make it a projection.

Initial aggregate boundaries:

- `OrganizationAggregate`: provider bindings, defaults, ACL baseline, organization folder-domain readiness.
- `FolderAggregate`: folder lifecycle, repository binding, storage mode, workspace readiness, ACL overrides, compact file operation facts.

_Sources:_ local files `Hexalith.EventStore/docs/concepts/architecture-overview.md`, `_bmad-output/brainstorming/brainstorming-session-20260505-070846.md`.

### Technology Selection

Recommended packages and runtime:

- `Hexalith.EventStore.Contracts`
- `Hexalith.EventStore.Client`
- `Hexalith.EventStore.Server`
- `Hexalith.EventStore.Testing`
- `Hexalith.EventStore.Aspire`
- DAPR
- Aspire Community Toolkit DAPR hosting
- xUnit
- OpenTelemetry

_Sources:_ https://aspire.dev/integrations/frameworks/dapr/, https://opentelemetry.io/docs/languages/dotnet/

## 9. Implementation Roadmap and Risk Assessment

### Technical Implementation Framework

Phase 0: create projects, references, AppHost, and test scaffolding.

Phase 1: implement organization commands/events/state/aggregate/tests.

Phase 2: implement folder commands/events/state/aggregate/tests.

Phase 3: wire domain service `/process`, EventStore domain routing, and local Aspire/DAPR execution.

Phase 4: add projections for organization readiness, folder status, ACL summary, repository lifecycle, and chatbot-visible folder query data.

Phase 5: add Git/repository/workspace/file workers with idempotent follow-up commands.

Phase 6: production hardening with DAPR resiliency, observability, dead-letter handling, projection lag metrics, snapshot policies, security validation, and load tests.

### Technical Risk Management

Key risks and mitigations:

- Event schema breakage: additive changes only, retained event classes, replay tests.
- Hot organization aggregate: keep folder operations on folder streams.
- Projection lag: expose status and sequence/lag metrics.
- Duplicate pub/sub delivery: idempotent projections and workers.
- Git provider failure: failure events, repair commands, retry/circuit-breaker policies.
- Secret leakage: secret references only, scans/tests for sensitive payloads.
- DAPR config route poisoning: restrict config-store writes and audit changes.
- Production sidecar exhaustion: configure sidecar resources and HA per DAPR production guidance.

_Sources:_ https://docs.dapr.io/concepts/resiliency-concept/, https://docs.dapr.io/operations/hosting/kubernetes/kubernetes-production/, local files `Hexalith.EventStore/docs/concepts/event-versioning.md`, `Hexalith.EventStore/docs/concepts/service-discovery.md`.

## 10. Future Technical Outlook and Innovation Opportunities

### Emerging Technical Direction

The architecture leaves room for later capabilities without changing the write model:

- Introduce `RepositoryAggregate` if repositories become independent domain objects.
- Add versioned domain services using the `domain-service-version` command extension.
- Add richer projections for AI/chatbot queries.
- Add incremental projection handlers when full replay becomes too expensive.
- Add workflow/process-manager abstractions for long-running folder repair and migration tasks.

DAPR's platform-agnostic model and OpenTelemetry's vendor-neutral telemetry keep hosting and observability choices flexible.

_Sources:_ https://docs.dapr.io/concepts/overview/, https://opentelemetry.io/

## 11. Technical Research Methodology and Source Verification

### Source Documentation

Primary sources used:

- Local `Hexalith.EventStore` source, docs, samples, and tests.
- Microsoft Learn Event Sourcing, CQRS, Saga, JWT, Cloud Adoption Framework, Well-Architected Framework, and .NET testing documentation.
- DAPR official documentation for actors, state, pub/sub, service invocation, resiliency, observability, tracing, and Kubernetes production guidance.
- Aspire DAPR integration documentation.
- OpenTelemetry official documentation.
- NuGet package metadata for `Hexalith.EventStore` packages.

Search queries included:

- `Hexalith.EventStore`
- `Hexalith EventStore NuGet`
- `DAPR state management service invocation pubsub actors docs`
- `Microsoft Event Sourcing CQRS architecture`
- `DAPR production observability resiliency`
- `Aspire DAPR integration`
- `.NET testing best practices`
- `OpenTelemetry .NET documentation`

### Research Quality Assurance

Confidence is high for EventStore implementation details because they were verified directly in the local source tree. Confidence is high for DAPR, Aspire, .NET testing, OpenTelemetry, and Microsoft architecture claims because they were verified from official documentation during the workflow. Repository/package availability can change, so NuGet package versions should be rechecked when implementation begins.

## 12. Technical Appendices and Reference Materials

### Recommended Command/Event Catalog

Organization commands: `CreateOrganization`, `RenameOrganization`, `ArchiveOrganization`, `ConfigureGitProvider`, `UnbindGitProvider`, `SetRepositoryPolicy`, `SetCredentialReference`, `GrantOrganizationAccess`, `RevokeOrganizationAccess`.

Organization events: `OrganizationCreated`, `OrganizationRenamed`, `OrganizationArchived`, `GitProviderBound`, `GitProviderUnbound`, `RepositoryPolicyConfigured`, `OrganizationCredentialReferenceSet`, `OrganizationAccessGranted`, `OrganizationAccessRevoked`.

Folder commands: `CreateFolder`, `RenameFolder`, `MoveFolder`, `ArchiveFolder`, `RestoreFolder`, `ChangeFolderStorageMode`, `RequestGitRepository`, `BindExistingRepository`, `MarkRepositoryProvisioned`, `MarkRepositoryProvisioningFailed`, `PrepareWorkspace`, `MarkWorkspacePrepared`, `WriteFile`, `MoveFile`, `DeleteFile`, `GrantFolderAccess`, `RevokeFolderAccess`, `RequestFolderRepair`.

Folder events: `FolderCreated`, `FolderRenamed`, `FolderMoved`, `FolderArchived`, `FolderRestored`, `FolderStorageModeChanged`, `FolderGitRepositoryRequested`, `FolderGitRepositoryProvisioned`, `FolderGitRepositoryProvisioningFailed`, `FolderGitRepositoryBound`, `WorkspacePrepared`, `WorkspacePreparationFailed`, `FileWritten`, `FileMoved`, `FileDeleted`, `FolderAccessGranted`, `FolderAccessRevoked`, `FolderRepairRequested`, `FolderDriftDetected`, `FolderSyncCompleted`.

### Implementation Checklist

- Create contracts and domain projects.
- Add EventStore package references.
- Implement organization and folder state with complete `Apply` coverage.
- Implement aggregates with pure `Handle` methods.
- Add aggregate, replay, rejection, tombstone, and registration tests.
- Add domain service host with `/process` and `/project`.
- Configure EventStore domain service routing.
- Add projections and query DTOs.
- Add Git/workspace workers.
- Add DAPR resiliency, observability, security, and deployment hardening.

## Technical Research Conclusion

`Hexalith.EventStore` is a strong fit for `Hexalith.Folders` if it is used as the aggregate command gateway and event-sourced write model, not as a generic repository abstraction. The main design discipline is boundary control: keep organization and folder aggregates focused, keep repository and file side effects outside aggregate handlers, and build projections for every read-facing use case.

The next engineering step is to create the first vertical slice: `OrganizationAggregate`, `FolderAggregate`, their commands/events/state/tests, a Folder domain service using `AddEventStore()` and `UseEventStore()`, and one projection for folder status. That slice will prove the full command-to-event-to-query path before Git provisioning and worker complexity is added.

**Technical Research Completion Date:** 2026-05-05
**Research Period:** current comprehensive technical analysis
**Source Verification:** local source plus current official documentation and package metadata
**Technical Confidence Level:** High for architecture and implementation direction; package versions should be rechecked at implementation start.

_This comprehensive technical research document serves as the implementation reference for using `Hexalith.EventStore` to manage `Hexalith.Folders` domain aggregates._
