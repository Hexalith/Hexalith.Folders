---
stepsCompleted: [1, 2, 3, 4, 5]
inputDocuments: []
workflowType: 'research'
lastStep: 1
research_type: 'technical'
research_topic: 'Hexalith.Memories semantic indexing for Hexalith.Folders RAG'
research_goals: 'Analyze how to use Hexalith.Memories in the Hexalith.Folders project to semantically index added files for retrieval-augmented generation.'
user_name: 'Jerome'
date: '2026-05-11'
web_research_enabled: true
source_verification: true
---

# Research Report: technical

**Date:** 2026-05-11
**Author:** Jerome
**Research Type:** technical

---

## Research Overview

[Research overview and methodology will be appended here]

---

<!-- Content will be appended sequentially through research workflow steps -->

## Technical Research Scope Confirmation

**Research Topic:** Hexalith.Memories semantic indexing for Hexalith.Folders RAG
**Research Goals:** Analyze how to use Hexalith.Memories in the Hexalith.Folders project to semantically index added files for retrieval-augmented generation.

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

**Scope Confirmed:** 2026-05-11

## Technology Stack Analysis

### Programming Languages

`Hexalith.Folders` and `Hexalith.Memories` are both C#/.NET services. Folders targets .NET 10 through the repository scaffold and project context, while the local Memories checkout accepts .NET 9 SDK or newer in its README and currently uses .NET 10 package families for hosting, dependency injection, OpenTelemetry, and ASP.NET Core. The recommended Folders integration should therefore be C#/.NET-first: add project/package references from the worker and server edges only, keep `Hexalith.Folders.Contracts` behavior-free, and avoid scripting-language ingestion glue.

For semantic indexing of added files, the implementation language should remain C# because the existing Memories client already exposes `MemoriesClient.IngestAsync(...)`, `SearchAsync(...)`, and `HybridSearchAsync(...)`, and the server accepts `IngestionInput` records over HTTP. This keeps cancellation, typed errors, central package management, and xUnit test patterns consistent with the rest of the Hexalith ecosystem.

_Popular Languages:_ C# for Hexalith integration and service code; JavaScript/Python appear only in external RAG examples and should not become a core dependency.

_Emerging Languages:_ No emerging runtime is needed. Python-heavy RAG tooling is not a fit for the current Hexalith boundary because Folders is already standardized on .NET, Dapr, Aspire, and EventStore.

_Language Evolution:_ The relevant evolution is .NET 10 plus centrally managed package families, not a language migration.

_Performance Characteristics:_ C# async I/O is suitable for worker-side file reads, REST calls, Dapr pub/sub handling, and indexing status updates. The heavy extraction/indexing path should remain inside Memories rather than duplicated in Folders.

_Sources:_ Local files `Directory.Packages.props`, `_bmad-output/project-context.md`, `Hexalith.Memories/README.md`, `Hexalith.Memories/src/Hexalith.Memories.Client.Rest/MemoriesClient.cs`; Dapr .NET/Aspire context from [Aspire Dapr integration](https://aspire.dev/integrations/frameworks/dapr/).

### Development Frameworks and Libraries

The central framework set is Dapr + Aspire + Hexalith.EventStore. Folders already plans workers as the owners of external provider, Git, working-copy, and filesystem side effects. Memories already uses Dapr Workflow for ingestion, Dapr pub/sub for EventStore-style event ingestion, Dapr Conversation API for optional natural-language event descriptions, Redis/RediSearch for syntactic and semantic indexes, FalkorDB for graph traversal, Kreuzberg for content extraction, and OpenTelemetry for traces and metrics.

The best fit is to let Folders publish or call into Memories after a file mutation becomes durable and authorized. The Folders aggregate should emit metadata-only events such as `FileWritten` / `FileDeleted`; a Folders worker should then materialize authorized file bytes from the working copy or provider, send them to Memories as `SourceType.File`, and record indexing status in a Folders projection. This respects the existing rule that aggregates and REST handlers must not perform provider or filesystem side effects directly.

Memories also has an EventStore integration package, but it is optimized for CloudEvents payload ingestion, not file-content indexing. It is useful for future indexing of Folders domain events themselves, but added-file RAG needs file bytes and path metadata, so a worker/client integration is the primary path.

_Major Frameworks:_ ASP.NET Core minimal APIs, Dapr Workflow, Dapr pub/sub, Aspire AppHost, Hexalith.EventStore, Hexalith.Memories REST client/contracts.

_Micro-frameworks:_ Kreuzberg for extraction inside Memories; System.CommandLine and ModelContextProtocol stay adapter-side.

_Evolution Trends:_ Modern RAG systems are moving toward hybrid search and structured retrieval rather than vector-only retrieval. Azure AI Search documents hybrid vector + keyword search as a first-class scenario, and OpenAI's Retrieval API also supports semantic search over vector stores.

_Ecosystem Maturity:_ Memories already provides the specialized RAG substrate; Folders should consume it instead of adding Semantic Kernel or Azure AI Search directly in MVP.

_Sources:_ Local files `Hexalith.Memories/docs/dev/eventstore-integration.md`, `Hexalith.Memories/src/Hexalith.Memories.Server/Workflows/IngestionWorkflow.cs`, `Hexalith.Memories/src/Hexalith.Memories.Contracts/V1/IngestionInput.cs`; [Azure AI Search vector overview](https://learn.microsoft.com/en-us/azure/search/vector-search-overview), [OpenAI Retrieval guide](https://developers.openai.com/api/docs/guides/retrieval).

### Database and Storage Technologies

Folders should continue treating EventStore state, projections, idempotency records, and provider truth as authoritative. File contents should not be stored in Folders events, logs, traces, projections, or audit records. Memories is the derived search/index substrate: Redis Stack/RediSearch stores syntactic and vector indexes, FalkorDB stores graph relationships, and Redis-backed operational stores track retries, tenant provisioning, failures, and telemetry.

Azure AI Search is a useful external comparison: it distinguishes internal chunking/vectorization from external pre-vectorization, supports filtered vector search, and states that hybrid keyword + vector queries often provide better recall than vector-only queries. That aligns with the local Memories design, which fans ingestion into syntactic, semantic, graph, and optional natural-language semantic indexes.

For Folders, the storage decision is not "which vector DB do we add?" but "how do we map file lifecycle to Memories memory units?" The likely model is one memory case per managed folder, with memory units keyed by stable source URIs such as `hexalith-folders://{managedTenantId}/{folderId}/{commitOrTaskId}/{pathHash}` plus metadata for folder id, repository id, branch, path classification, commit id, content hash, task id, and ACL snapshot/version.

_Relational Databases:_ Not recommended for the first integration slice unless Memories later exposes a Postgres-backed adapter. Folders architecture currently favors Redis-compatible state/pubsub for local and baseline production topology.

_NoSQL Databases:_ Redis Stack and FalkorDB are already part of Memories. Dapr state stores remain behind Dapr/EventStore rather than being used directly by Folders domain code.

_In-Memory Databases:_ In-memory test doubles are appropriate for unit tests; production indexing should use Memories' persistent Redis/FalkorDB paths.

_Data Warehousing:_ Out of scope for added-file RAG. Audit and operational evidence remain metadata-only projections, not a RAG data warehouse.

_Sources:_ Local files `Hexalith.Memories/src/Hexalith.Memories.Redis/README.md`, `Hexalith.Memories/src/Hexalith.Memories.Server/Workflows/IngestionWorkflow.cs`, `_bmad-output/planning-artifacts/architecture.md`; [Azure AI Search vector overview](https://learn.microsoft.com/en-us/azure/search/vector-search-overview).

### Development Tools and Platforms

The implementation should use existing Hexalith development tooling: `dotnet restore Hexalith.Folders.slnx`, `dotnet build Hexalith.Folders.slnx`, xUnit v3/Shouldly/NSubstitute for Folders tests, and Aspire for local topology. If Memories is added as a root-level submodule dependency, initialization guidance must remain root-level only and avoid recursive submodule updates.

For local RAG verification, the Memories AppHost already boots Redis Stack, FalkorDB, Memories Server, a Dapr sidecar, and Aspire Dashboard. Folders AppHost should either reference a running Memories service as an external endpoint or add a local `memories-server` resource and Dapr sidecar once the integration story reaches end-to-end testing. The first production-like slice should validate through a worker smoke test with a fake `IMemoriesIndexingClient`, then an Aspire integration test with real Memories only after the contract is stable.

_IDE and Editors:_ Standard .NET IDEs are sufficient; no special RAG IDE dependency is needed.

_Version Control:_ Git/provider state remains Folders' source of durable file truth; Memories indexes derived content.

_Build Systems:_ .NET SDK, `.slnx`, central package management, and AppHost orchestration.

_Testing Frameworks:_ xUnit v3 for Folders; Memories currently uses xUnit 2.x in its own repo, so cross-repo tests should avoid sharing test helper binaries unless version differences are deliberately reconciled.

_Sources:_ Local files `global.json`, `Directory.Packages.props`, `Hexalith.Memories/README.md`, `Hexalith.Memories/Directory.Packages.props`; [Aspire Dapr integration](https://aspire.dev/integrations/frameworks/dapr/).

### Cloud Infrastructure and Deployment

Dapr is the common runtime abstraction. It provides building blocks over HTTP/gRPC, including pub/sub, state, workflows, actors, service invocation, secrets, conversation, and observability. Aspire's Dapr integration adds local sidecars to app-hosted projects and supports Dapr state store component registration. This matches Folders' current architecture and Memories' existing deployment posture.

The deployment stack should keep Folders and Memories as separate services. Folders owns authorization, file lifecycle, workspace locks, commits, and audit evidence. Memories owns extraction, embedding, indexes, graph traversal, and search telemetry. Service-to-service calls should carry correlation IDs, tenant scope, and an indexing idempotency key. Production Dapr policy should explicitly permit only the required Folders-to-Memories paths.

_Major Cloud Providers:_ Azure is the most natural managed deployment target because the surrounding docs and ecosystem point to Aspire, Azure Container Apps, Azure AI Search comparisons, Azure OpenAI embeddings, and Microsoft identity/security controls. Still, Dapr keeps the service boundary portable.

_Container Technologies:_ Aspire local resources and Dapr sidecars; production can target Azure Container Apps or Kubernetes with Dapr sidecars.

_Serverless Platforms:_ Not recommended for the indexing worker itself because file indexing needs durable retries, idempotency, status projection updates, and sidecar-mediated calls.

_CDN and Edge Computing:_ Not relevant for initial semantic indexing of repository-backed files.

_Sources:_ [Dapr building blocks](https://docs.dapr.io/concepts/building-blocks-concept/), [Aspire Dapr integration](https://aspire.dev/integrations/frameworks/dapr/), local files `Hexalith.Memories/docs/dev/eventstore-integration.md`, `_bmad-output/project-context.md`.

### Technology Adoption Trends

The current RAG direction favors hybrid retrieval, metadata filtering, provenance, and bounded context assembly. Azure AI Search frames RAG challenges around query understanding, multi-source access, token constraints, response time, and security/governance; those map directly to Folders. The strongest Folders-specific requirement is security trimming before retrieval: tenant access, folder ACL, and path policy must be applied before query execution, not after results return.

OpenAI's Retrieval API confirms the industry-normal ingestion shape: vector stores act as searchable indexes, files are chunked/embedded/indexed asynchronously, attributes support filtering, and chunking strategies matter. Memories provides a local Hexalith-shaped version of that idea, but with Redis/FalkorDB, Dapr Workflow, and hybrid axes. The current Memories HTTP ingest endpoint has a 1 MB inline content limit, so Folders must plan either bounded file ingestion, chunked pre-ingestion, URL/source-reference ingestion, or a Memories contract extension before indexing large files.

_Migration Patterns:_ Start with worker-driven file ingestion into Memories after `FileWritten`/commit success, then add delete/re-index/ACL-refresh workflows, then expose RAG queries through the Folders SDK/MCP with strict authorization filters.

_Emerging Technologies:_ Semantic Kernel vector store connectors are broad but still preview; they are not needed if Memories remains the Hexalith memory abstraction. They may inform future adapter design if Memories later wants alternate vector stores.

_Legacy Technology:_ Pure keyword search is insufficient for agentic context queries, but BM25 remains valuable as one hybrid axis.

_Community Trends:_ Hybrid search plus metadata filters are the stable center. Vector-only search without tenant/path filtering would be a security and relevance regression for Folders.

_Sources:_ [Azure AI Search RAG overview](https://learn.microsoft.com/en-us/azure/search/retrieval-augmented-generation-overview), [OpenAI Retrieval guide](https://developers.openai.com/api/docs/guides/retrieval), [Semantic Kernel vector store connectors](https://learn.microsoft.com/en-us/semantic-kernel/concepts/vector-store-connectors/out-of-the-box-connectors/), local files `Hexalith.Memories/src/Hexalith.Memories.Server/Activities/Ingestion/IngestionInputValidator.cs`, `_bmad-output/project-context.md`.

## Integration Patterns Analysis

### API Design Patterns

The primary API pattern should be REST over HTTP through typed C# clients. Memories already exposes `POST /api/ingest` for asynchronous file ingestion, `GET /api/ingest/{instanceId}` for workflow status, `GET /api/search?...&axis=...` for syntactic/semantic/graph/hybrid search, and typed client methods around those endpoints. Microsoft's Azure Architecture Center still frames REST as a good default when resource-oriented semantics, HTTP verbs, response codes, idempotency, and interoperability matter; it also notes that gRPC can be faster for backend APIs but introduces protocol/gateway tradeoffs.

For Folders, REST is the right first integration because the existing Memories client is REST-based and already returns workflow instance ids rather than requiring Folders to understand Dapr Workflow internals. Folders should hide Memories behind a local `IFolderSemanticIndexingClient` or similar worker-side port so the core domain and contracts never reference Memories directly. That adapter can use `Hexalith.Memories.Client.Rest` internally and can later switch from direct HTTP base URLs to Dapr service invocation without changing domain code.

Folders should not expose raw Memories search directly to agents or external callers. The Folders API/SDK/MCP should provide an authorized context/RAG query endpoint that first validates tenant access, folder ACL, path policy, and query bounds, then calls Memories with the narrowest available scope, likely `tenantId + caseId`, and finally redacts/snips result content according to Folders policy.

_RESTful APIs:_ Use Memories REST client for ingestion and query. Wrap it behind a Folders worker/query port.

_GraphQL APIs:_ Not recommended. Folders already has an OpenAPI/REST Contract Spine, and GraphQL would not improve the indexing workflow.

_RPC and gRPC:_ Dapr service invocation can use HTTP or gRPC underneath, but Folders should keep a REST/typed-client boundary until a measured performance need appears.

_Webhook Patterns:_ Provider webhooks are outside the first indexing slice. Use Folders' durable file events/workers instead of indexing directly from provider webhooks.

_Source:_ [Azure API design guidance](https://learn.microsoft.com/en-us/azure/architecture/microservices/design/api-design), local files `Hexalith.Memories/src/Hexalith.Memories.Client.Rest/MemoriesClient.cs`, `Hexalith.Memories/src/Hexalith.Memories.Client.Rest/SearchRequest.cs`.

### Communication Protocols

The integration has two separate communication paths. File-content indexing should be request/response: a Folders worker reads authorized file bytes after a durable folder event and sends an `IngestionInput` to Memories. Event-payload indexing can be pub/sub: Memories already ships `Hexalith.Memories.EventStore`, an ASP.NET controller at `/events/ingest`, and Dapr subscription metadata for a single configured topic.

Dapr pub/sub automatically wraps messages in CloudEvents by default, supports explicit delivery outcomes, dead letter topics, routing, TTL, and bulk publishing. CloudEvents itself standardizes event metadata such as `id`, `source`, `type`, and `specversion`; the spec says `source + id` identifies distinct events and allows duplicate detection. Memories uses those attributes to route, deduplicate, and map event payloads into `SourceType.Event` memory units.

This means the recommended split is:

- Use REST client ingestion for added files because file bytes are not present in Folders events and must remain out of EventStore.
- Use Dapr pub/sub CloudEvents only for optional indexing of Folders domain events, such as lifecycle and commit events, where the event payload itself is the indexed content.
- Use Dapr service invocation only as a transport option for service-to-service calls in AppHost/Kubernetes topologies, not as a domain dependency.

_HTTP/HTTPS Protocols:_ Primary protocol for Memories REST ingestion/search and Folders public API.

_WebSocket Protocols:_ Not needed for indexing. Status should flow through projections, polling, or existing SignalR/EventStore projection nudges later.

_Message Queue Protocols:_ Dapr pub/sub over Redis Streams locally; production broker remains a Dapr component choice.

_grpc and Protocol Buffers:_ Possible inside Dapr, but not required for MVP and not exposed by current Memories client.

_Source:_ [Dapr service invocation overview](https://docs.dapr.io/developing-applications/building-blocks/service-invocation/service-invocation-overview/), [Dapr pub/sub overview](https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-overview/), [CloudEvents specification](https://github.com/cloudevents/spec/blob/main/cloudevents/spec.md), local files `Hexalith.Memories/src/Hexalith.Memories.EventStore/EventIngestionController.cs`, `Hexalith.Memories/src/Hexalith.Memories.EventStore/CloudEventToIngestionInputMapper.cs`.

### Data Formats and Standards

Folders and Memories already use JSON contracts. Memories `IngestionInput` carries tenant id, case id, source URI, content bytes, content type, source type, ingested-by, metadata, causation id, and correlation id. Metadata values are `MetadataField` records with value, origin, and confidence, which is a useful fit for Folders because path, content hash, commit id, task id, ACL version, and sensitivity tier can be carried as structured metadata without leaking raw provider credentials or unbounded payloads.

For file RAG, `SourceType.File` should be used with a stable `SourceUri`, content type, and metadata. For domain event RAG, `SourceType.Event` should be used via the CloudEvents mapper, preserving `cloudevent.id`, `source`, `type`, `subject`, `time`, and derived `event.aggregateType`.

The main format gap is large files. Memories' current HTTP file ingestion path requires inline `ContentBytes` and validates a maximum of 1 MB. OpenAI's Retrieval docs show a different managed-vector-store model with file upload, per-file attributes, batch ingestion, and configurable chunking. Folders should not copy that API directly, but it highlights the same missing capability: large-file chunking or streaming/reference ingestion. Until Memories gains that, Folders needs an explicit policy: index only files below the limit, pre-chunk in the worker into multiple memory units, or mark the file as `semantic_index_skipped_large_file`.

_JSON and XML:_ JSON is the established contract format. XML is unnecessary.

_Protobuf and MessagePack:_ Not required unless a future high-throughput internal API replaces REST.

_CSV and Flat Files:_ Only relevant as file contents to extract, not as integration envelopes.

_Custom Data Formats:_ Use stable `hexalith-folders://...` source URIs and Folders metadata keys rather than ad hoc payload conventions.

_Source:_ [OpenAI Retrieval guide](https://developers.openai.com/api/docs/guides/retrieval), local files `Hexalith.Memories/src/Hexalith.Memories.Contracts/V1/IngestionInput.cs`, `Hexalith.Memories/src/Hexalith.Memories.Contracts/V1/MetadataField.cs`, `Hexalith.Memories/src/Hexalith.Memories.Server/Activities/Ingestion/IngestionInputValidator.cs`.

### System Interoperability Approaches

The right interoperability model is ports-and-adapters inside Folders plus service boundaries at runtime. Folders core emits domain events and updates projections; Folders workers observe durable file events, materialize bytes through provider/workspace ports, and invoke the Memories adapter. This avoids a direct dependency from aggregates, REST handlers, contracts, CLI, MCP, or UI into Memories infrastructure.

At runtime, Folders AppHost currently starts only `folders` and `folders-ui`. Memories AppHost already starts Redis Stack, FalkorDB, Memories Server, Dapr sidecars, pub/sub, state store, secret store, and Dapr Conversation component. The Folders AppHost should evolve in two stages: first configure `Memories:Endpoint` for local/manual integration; then add an optional `memories-server` project/resource reference when the end-to-end indexing slice is ready.

_Point-to-Point Integration:_ Good for Folders worker to Memories server ingestion/search through an adapter. Avoid point-to-point from aggregates.

_API Gateway Patterns:_ Folders itself should act as the policy gateway for RAG queries. Do not expose Memories as the public query surface.

_Service Mesh:_ Dapr sidecars plus mTLS/access control cover the initial service-to-service needs without adding a separate mesh.

_Enterprise Service Bus:_ Not needed. Dapr pub/sub is enough for event fan-out.

_Source:_ [Aspire Dapr integration](https://aspire.dev/integrations/frameworks/dapr/), [Dapr service invocation overview](https://docs.dapr.io/developing-applications/building-blocks/service-invocation/service-invocation-overview/), local files `src/Hexalith.Folders.AppHost/Program.cs`, `Hexalith.Memories/src/Hexalith.Memories.AppHost/Program.cs`, `_bmad-output/project-context.md`.

### Microservices Integration Patterns

The integration should be asynchronous for indexing and synchronous for retrieval. Indexing is naturally asynchronous because Memories schedules a Dapr Workflow and returns an instance id. Folders should persist a semantic-indexing status projection keyed by tenant, folder, file path/content hash, and task/commit id. Retrieval is synchronous from the caller's perspective, but Folders should enforce token budgets, result limits, timeout budgets, and partial-failure behavior.

Dapr service invocation provides service discovery, tracing, metrics, error handling, encryption, retries, and access control, but Dapr's docs note streaming HTTP requests bypass retry policies because request bodies cannot be replayed. That matters if Memories later adds streaming file ingestion: idempotency and retry must move to the application-level indexing key rather than relying on sidecar retries.

_API Gateway Pattern:_ Folders API/SDK/MCP are the gateway. Memories remains an internal dependency.

_Service Discovery:_ Use Aspire configuration locally and Dapr app IDs in Dapr-enabled deployments.

_Circuit Breaker Pattern:_ Use Dapr resiliency policies and Folders-side status transitions so Memories outages produce `semantic_index_pending` or `semantic_index_failed_retryable`, not failed file mutations.

_Saga Pattern:_ Treat indexing as a process-manager workflow after the file mutation/commit, not as part of the aggregate transaction. If indexing fails, file write remains durable and indexing can retry/reconcile.

_Source:_ [Dapr resiliency overview](https://docs.dapr.io/operations/resiliency/resiliency-overview/), [Dapr service invocation overview](https://docs.dapr.io/developing-applications/building-blocks/service-invocation/service-invocation-overview/), local files `Hexalith.Memories/src/Hexalith.Memories.Server/Workflows/IngestionWorkflow.cs`, `_bmad-output/planning-artifacts/architecture.md`.

### Event-Driven Integration

Folders already plans `FileWritten`, `FileMoved`, and `FileDeleted` domain events. Those events should trigger indexing workers but should not contain file contents. A worker should derive deterministic indexing commands:

- `FileWritten` or committed file mutation: index current content if policy allows.
- `FileMoved`: either update metadata if Memories supports it later, or re-index under the new source URI and mark the prior source stale.
- `FileDeleted`: delete/deactivate the corresponding memory unit when Memories exposes a stable delete endpoint, or mark a Folders-side projection tombstone and filter it from RAG results until deletion support exists.
- `FolderAccessRevoked` or ACL/path-policy changes: refresh indexing metadata or enforce authorization in Folders before query so stale memory units cannot leak.

Memories' EventStore integration can subscribe to one topic per deployment, route by CloudEvents source prefix to tenant id, optionally auto-create cases, and deduplicate by CloudEvent id. That is valuable but insufficient for added-file indexing because Folders events are intentionally metadata-only. It is a secondary integration for event/audit semantic memory, not the file RAG path.

_Publish-Subscribe Patterns:_ Use Dapr pub/sub for event-payload indexing and worker fan-out where appropriate.

_Event Sourcing:_ Keep Folders events compact and metadata-only. Use them to trigger side effects, not to carry content.

_Message Broker Patterns:_ Redis pub/sub component is the local path; broker selection remains a deployment concern behind Dapr.

_CQRS Patterns:_ Folders projections should track indexing status and query eligibility; Memories indexes are derived read models for search.

_Source:_ [Dapr pub/sub overview](https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-overview/), [CloudEvents specification](https://github.com/cloudevents/spec/blob/main/cloudevents/spec.md), local files `Hexalith.Memories/docs/dev/eventstore-integration.md`, `Hexalith.Memories/src/Hexalith.Memories.EventStore/EventIngestionService.cs`, `_bmad-output/planning-artifacts/research/technical-hexalith-eventstore-domain-aggregates-research-2026-05-05.md`.

### Integration Security Patterns

Security is the dominant integration constraint. Folders' project context requires zero cross-tenant leakage, metadata-only events/logs/traces/projections/audit, tenant-prefixed cache keys, and context-query authorization before search execution. Memories currently has internal/service-oriented endpoints and local docs that still track cross-cutting auth as future work in some areas, so Folders must not delegate authorization to Memories.

Use this posture:

- Folders validates caller identity, tenant access, folder ACL, path policy, sensitivity policy, and query bounds before calling Memories.
- Folders sends only tenant-scoped, policy-approved content to Memories.
- Memory metadata should include only classified metadata: folder id, source URI, content hash, commit/task ids, content type, path classification, ACL policy version, and correlation id. Avoid raw provider tokens, diffs, secrets, and unauthorized resource existence.
- Dapr access control should deny by default and allow only specific app IDs and operations. Dapr's access-control docs state policies can restrict called-application operations and HTTP verbs by caller app identity, namespace, and trust domain.
- mTLS, Dapr API tokens, and app API tokens should be deployment controls; they are not substitutes for Folders authorization.

_OAuth 2.0 and JWT:_ Folders public surfaces should retain JWT/EventStore/Tenants authorization. Memories service calls should be internal and policy-restricted.

_API Key Management:_ Embedding/provider secrets stay inside Memories. Folders should not receive embedding API keys.

_Mutual TLS:_ Use Dapr mTLS and access-control allowlists in production.

_Data Encryption:_ Keep transport encrypted and avoid storing raw sensitive content in Folders telemetry. Memories will store extracted/indexed content, so tenant policy must explicitly allow indexing for eligible paths/files.

_Source:_ [Dapr service invocation access control](https://docs.dapr.io/operations/configuration/invoke-allowlist/), [Dapr security concepts](https://docs.dapr.io/concepts/security-concept/), local files `_bmad-output/project-context.md`, `Hexalith.Memories/src/Hexalith.Memories.AppHost/Program.cs`, `Hexalith.Memories/docs/dev/eventstore-integration.md`.

## Architectural Patterns and Design

### System Architecture Patterns

The recommended architecture is event-sourced Folders plus derived Memories indexes. Folders keeps EventStore as the authoritative write model for folder lifecycle, workspace locks, file mutation metadata, commits, ACLs, path policy, and audit. Memories is a specialized read/index subsystem for semantic retrieval over approved file content. This matches CQRS guidance: command/write models own validation and domain consistency, while read models/projections can use different schemas and storage optimized for queries.

Architecturally, semantic indexing should be a post-commit or post-durable-event process, not part of the aggregate command transaction. The aggregate emits metadata-only events such as `FileWritten`, `FileDeleted`, `CommitSucceeded`, or later `SemanticIndexingRequested`; a worker/process manager materializes bytes and invokes Memories. If Memories is down, file mutation remains durable and indexing status becomes pending/retryable instead of rolling back the folder operation.

Recommended component roles:

- `Hexalith.Folders`: domain aggregates and policy decisions.
- `Hexalith.Folders.Server`: REST transport, EventStore domain service endpoints, authorized context-query facade.
- `Hexalith.Folders.Workers`: process managers for provider/workspace/file side effects and Memories indexing.
- `Hexalith.Folders.Client`, CLI, MCP: wrap the Folders API/SDK, not Memories directly.
- `Hexalith.Memories`: extraction, embeddings, syntactic/semantic/graph indexes, hybrid retrieval, ingestion failure recovery.

_Source:_ [CQRS pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs), local files `_bmad-output/planning-artifacts/architecture.md`, `Hexalith.Memories/src/Hexalith.Memories.Server/Workflows/IngestionWorkflow.cs`.

### Design Principles and Best Practices

Use a hexagonal boundary around Memories. The Folders domain should depend on concepts like `SemanticIndexingRequest`, `SemanticIndexingStatus`, and `AuthorizedContextQuery`, while the adapter knows about `MemoriesClient`, `IngestionInput`, `HybridSearchRequest`, memory unit ids, and Memories errors. This keeps the integration replaceable and testable.

The critical design rule is "authorize first, search second." Azure AI Search security-trimming guidance describes storing principal identifiers and filtering queries so inaccessible documents are removed by the search layer. Folders needs a stricter version because its project context requires tenant access, folder ACL, and path policy before any context query executes. Practically, Folders should:

- Map one Memories case to one folder or one folder-query scope so `caseId` narrows searches.
- Store non-secret metadata fields for folder id, path policy version, ACL version, source URI, content hash, commit/task id, and sensitivity tier.
- Prefer pre-filtering by folder/case and path policy in Folders before calling Memories.
- Treat result filtering after Memories as defense-in-depth, not the primary authorization gate.

This design also supports replay. If the semantic index projection is lost, Folders can replay file events and enqueue re-indexing without changing aggregate history. If Memories has stale data, Folders can use its own indexing projection and source metadata to decide whether results are eligible.

_Source:_ [Security filter pattern for search](https://learn.microsoft.com/en-us/azure/search/search-security-trimming-for-azure-search), [CQRS pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs), local file `_bmad-output/project-context.md`.

### Scalability and Performance Patterns

Indexing should use asynchronous competing-worker style with bounded per-tenant concurrency. Memories already has per-tenant extraction concurrency, Dapr Workflow retry policies, failed-unit registry, and case ingestion counters. Folders should add its own indexing work queue/projection so it can throttle by tenant/folder, observe backlog, and avoid retry storms against Memories.

The performance goal should be split:

- File mutation acknowledgement: do not wait for semantic indexing unless the user explicitly asks for synchronous indexing.
- Indexing completion: eventual consistency, visible via status projection.
- RAG query latency: bounded by Folders context-query budget plus Memories search timeout/token budget.

Microsoft transient-fault guidance warns against duplicated retry layers and endless retries, recommends retry budgets, exponential backoff, and circuit breakers, and notes idempotency is required when operations may repeat. For Folders, this means a deterministic indexing key is load-bearing. Use an idempotency/source key such as `tenantId + folderId + sourcePathCanonicalHash + contentHash + commitId` and persist the mapping from file version to Memories workflow instance and memory unit id.

For RAG quality, Azure AI Search's RAG overview calls out content preparation, chunking, hybrid search, semantic ranking, and vectorization as major design choices. Memories already uses hybrid axes; Folders should focus on content eligibility, source metadata, token budgets, and chunk boundaries for files over the current 1 MB Memories inline limit.

_Source:_ [Transient fault handling](https://learn.microsoft.com/en-us/azure/architecture/best-practices/transient-faults), [Azure AI Search RAG overview](https://learn.microsoft.com/en-us/azure/search/retrieval-augmented-generation-overview), local files `Hexalith.Memories/docs/operations/failure-recovery.md`, `Hexalith.Memories/src/Hexalith.Memories.Server/Activities/Ingestion/IngestionInputValidator.cs`.

### Integration and Communication Patterns

Use a process-manager/saga shape for semantic indexing. The write-side file operation is the pivot: after the file mutation and, preferably, commit are durable, indexing becomes a retryable follow-up action. The Saga pattern is appropriate where distributed services each own their data and local transactions, but traditional ACID across services is not practical. In this case, Folders and Memories should not participate in a distributed transaction; they should coordinate through idempotent messages, status projections, and compensating/cleanup actions where supported.

Suggested flow:

1. Folders command writes/commits a file and emits metadata-only events.
2. Folders projection marks the file version `semantic_index_pending`.
3. Folders worker verifies tenant/folder/path eligibility and materializes content.
4. Worker calls Memories `IngestAsync` with `SourceType.File`, stable `SourceUri`, metadata, and correlation/causation values.
5. Worker records Memories workflow instance id.
6. Status poll/subsequent worker pass observes completion/failure and updates projection.
7. Query endpoint uses only indexed, eligible file versions and applies current authorization before calling Memories.

For deletes and moves, the architecture must handle lifecycle drift. Local Memories Server has a `DELETE /api/tenants/{tenantId}/cases/{caseId}/memory-units/{memoryUnitId}` endpoint, but the current REST client search/integration surface is stronger than the public delete wrapper. Folders should either add a small delete method to the adapter/client or maintain a tombstone projection and filter results until deletion support is first-class. The cleaner architectural target is explicit memory-unit deletion on `FileDeleted`, re-index on content hash change, and source URI renewal on path moves.

_Source:_ [Saga pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/saga), [Dapr service invocation overview](https://docs.dapr.io/developing-applications/building-blocks/service-invocation/service-invocation-overview/), local files `Hexalith.Memories/src/Hexalith.Memories.Server/Program.cs`, `Hexalith.Memories/src/Hexalith.Memories.Client.Rest/MemoriesClient.cs`.

### Security Architecture Patterns

Folders must remain the policy enforcement point. Memories stores extracted content and search indexes, so any content sent there is already within the tenant's semantic index blast radius. Do not index paths excluded by tenant/folder policy. Do not index files that are binary, too large, marked secret, or outside the allowed path scope unless a policy explicitly permits it.

Recommended layers:

- Authentication and tenant provenance: inherited from Folders/EventStore/Tenants, never from request payload.
- Authorization before indexing: tenant access, folder ACL, path policy, sensitivity classification, content-size/type policy.
- Authorization before retrieval: same order, then search.
- Metadata security: store IDs, hashes, policy versions, and classifications; avoid raw secrets, diffs, tokens, credential refs, or unauthorized resource names.
- Runtime security: Dapr mTLS, Dapr deny-by-default access-control policies, app/API tokens where needed, and no public exposure of Memories Server for Folders callers.
- Observability: correlation ids and status, never file contents in Folders telemetry.

This is stricter than generic search security trimming because a result returned from Memories may already include snippets/content. Folders should minimize overbroad Memories queries and should fail closed if it cannot prove current authorization or projection freshness.

_Source:_ [Security filter pattern for search](https://learn.microsoft.com/en-us/azure/search/search-security-trimming-for-azure-search), [Dapr security concepts](https://docs.dapr.io/concepts/security-concept/), local files `_bmad-output/project-context.md`, `_bmad-output/planning-artifacts/architecture.md`.

### Data Architecture Patterns

Use three distinct data classes:

1. Authoritative Folders data: event streams, folder/workspace/ACL/path-policy projections, idempotency records, audit metadata.
2. Derived semantic index data: Memories memory units, Redis/RediSearch hashes, vector hashes, FalkorDB nodes/edges, failed-unit registry.
3. Operational bridge data: Folders semantic-indexing projection mapping `folder file version -> Memories case/workflow/memory unit/status`.

The bridge projection is essential. Without it, Folders cannot answer "is this file version indexed?", "which memory unit should be deleted after file deletion?", "is this search result stale?", or "what should be replayed after tenant restore?" The projection should be tenant-prefixed, deterministic from events plus worker outcomes, and rebuildable from event history plus Memories reconciliation where needed.

Recommended metadata keys in Memories:

- `folders.folderId`
- `folders.organizationId`
- `folders.repositoryId`
- `folders.workspaceTaskId`
- `folders.commitId`
- `folders.pathHash`
- `folders.pathPolicyVersion`
- `folders.aclVersion`
- `folders.contentHash`
- `folders.indexedFromEventId`
- `folders.sourceKind=file`
- `folders.sensitivityTier`

Avoid raw path in Memories metadata unless C9 sensitivity policy permits it. Use source URI and hashed/canonical path metadata for correlation, and render paths from Folders projections after authorization.

_Source:_ [CQRS pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs), local files `Hexalith.Memories/src/Hexalith.Memories.Server/Activities/Indexing/IndexSyntacticActivity.cs`, `Hexalith.Memories/src/Hexalith.Memories.Contracts/V1/MemoryUnit.cs`, `_bmad-output/planning-artifacts/architecture.md`.

### Deployment and Operations Architecture

Run Folders and Memories as separate Dapr-enabled services. Folders AppHost currently starts Folders Server and UI; Memories AppHost starts Redis Stack, FalkorDB, Memories Server, Dapr components, and MCP. The integration architecture should not merge those services. Instead, add configuration and AppHost wiring so Folders workers can reach Memories through a named endpoint or Dapr app id.

Operationally, the Folders console should show semantic indexing state as read-only diagnostic metadata: pending, scheduled, indexing, indexed, failed retryable, failed terminal, skipped by policy, skipped large file, stale/tombstoned, and reconciliation required. It should link the Folders file version and task/correlation id to the Memories workflow instance id, without revealing content or secrets.

Failure recovery should use two levers:

- Memories' own failed-unit registry and re-ingestion endpoints for failures inside the extraction/embedding/index pipeline.
- Folders' indexing projection/reconciler for failures before Memories accepts the job, stale file lifecycle events, ACL policy changes, and replay/backfill.

This produces an operations model where Folders can answer whether RAG is up to date without treating Memories as authoritative for file truth.

_Source:_ [Saga pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/saga), [Transient fault handling](https://learn.microsoft.com/en-us/azure/architecture/best-practices/transient-faults), local files `src/Hexalith.Folders.AppHost/Program.cs`, `Hexalith.Memories/src/Hexalith.Memories.AppHost/Program.cs`, `Hexalith.Memories/docs/operations/failure-recovery.md`.

## Implementation Approaches and Technology Adoption

### Technology Adoption Strategies

Adopt `Hexalith.Memories` incrementally through a strangler-style internal facade rather than wiring it directly throughout Folders. The Azure Strangler Fig pattern recommends gradual replacement or extension behind a stable facade to reduce risk. In Folders, the facade is not for legacy migration; it is an internal semantic-indexing port that allows the worker implementation to evolve without touching aggregates, contracts, CLI, MCP, or UI.

Recommended adoption sequence:

1. Introduce worker-side abstractions and in-memory/fake implementations.
2. Add a Memories REST adapter only in `Hexalith.Folders.Workers`.
3. Add semantic-indexing projection models and statuses.
4. Wire indexing from a narrow file-written/file-committed event slice.
5. Add query facade in Folders Server that authorizes before search.
6. Add Aspire/Dapr integration tests with a real Memories Server once unit contracts are stable.
7. Add cleanup/re-index/backfill workflows after the first happy path works.

This avoids a big-bang RAG feature and keeps each step independently testable.

_Source:_ [Strangler Fig pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/strangler-fig), local files `src/Hexalith.Folders.Workers/Hexalith.Folders.Workers.csproj`, `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs`.

### Development Workflows and Tooling

Implementation should follow the existing Folders workflow: central package management, `.slnx`, warnings-as-errors, dependency-direction tests, xUnit v3, Shouldly, and narrow builds/tests first. Adding Memories packages or project references will intentionally change the scaffold dependency expectations; update `ScaffoldContractTests.ProjectReferencesFollowAllowedDependencyDirection` when the worker edge legitimately references Memories, and keep `Hexalith.Folders.Contracts` dependency-free.

Concrete project changes for the first implementation story:

- `Directory.Packages.props`: add `Hexalith.Memories.Client.Rest` and `Hexalith.Memories.Contracts` package versions, or add controlled project references to the root-level `Hexalith.Memories` submodule during local development only if packaging is not ready.
- `src/Hexalith.Folders.Workers`: add semantic indexing ports, adapter, options, and worker orchestration.
- `src/Hexalith.Folders`: add domain event/status concepts only if they are core lifecycle language.
- `src/Hexalith.Folders.Server`: later add authorized context-query facade.
- `src/Hexalith.Folders.AppHost`: later add optional Memories service endpoint/Dapr wiring.
- `tests/Hexalith.Folders.Workers.Tests`: fake adapter tests, source URI/idempotency tests, policy skip tests, and error mapping tests.

Keep comments and docs focused on non-obvious invariants: authorization order, why content does not enter events, and why indexing failure does not roll back file commits.

_Source:_ [Azure Operational Excellence design principles](https://learn.microsoft.com/en-us/azure/well-architected/operational-excellence/principles), local files `Directory.Packages.props`, `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs`, `Hexalith.Memories/src/Hexalith.Memories.Client.Rest/Hexalith.Memories.Client.Rest.csproj`.

### Testing and Quality Assurance

Use a three-tier test strategy:

1. Unit tests with fake Memories port:
   - Maps Folders file version to stable source URI.
   - Adds required metadata without raw path/content leakage.
   - Skips excluded, binary, large, or sensitive files.
   - Converts Memories adapter failures into retryable/terminal indexing statuses.
   - Preserves cancellation tokens.

2. Contract/adapter tests:
   - Validate `IngestionInput` construction for `SourceType.File`.
   - Validate `HybridSearchRequest`/`SearchRequest` construction for authorized query facade.
   - Validate delete/re-index mapping once memory-unit delete is wrapped.

3. Integration tests:
   - Aspire starts Folders + Memories + Redis + FalkorDB + Dapr components.
   - A committed file becomes searchable through the Folders query facade.
   - Unauthorized user/tenant/path cannot retrieve indexed content.
   - Memories outage leaves file mutation durable and indexing retryable.

.NET unit testing guidance emphasizes small, fast, isolated tests. That fits the first slice: prove mapping, status, and authorization behavior without a running Memories stack, then add a small number of slower integration tests for confidence.

_Source:_ [.NET unit testing best practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices), local files `tests/Hexalith.Folders.Workers.Tests/WorkersSmokeTests.cs`, `Hexalith.Memories/docs/operations/failure-recovery.md`.

### Deployment and Operations Practices

Deploy Folders and Memories as separate services. Folders workers should resolve Memories through configuration or Dapr app id, with bounded timeouts and Dapr resiliency policies where Dapr is active. Dapr resiliency specs support timeouts, retries, circuit breakers, and targets for apps, components, and actors. Use those platform policies, plus application-level indexing idempotency, rather than adding ad hoc retry loops throughout worker code.

Operational fields and signals to add:

- `semantic_index_pending_total`
- `semantic_index_scheduled_total`
- `semantic_index_failed_total{reason}`
- `semantic_index_latency_ms`
- `semantic_index_skipped_total{reason}`
- projection lag between file commit and indexed status
- correlation id from Folders command to Memories workflow id

OpenTelemetry's .NET ecosystem provides traces, metrics, and logs. Folders should emit custom spans/events around "semantic indexing scheduled", "semantic indexing completed", and "authorized RAG query" while ensuring file contents, snippets, diffs, secrets, and raw path values do not enter telemetry.

_Source:_ [Dapr resiliency overview](https://docs.dapr.io/operations/resiliency/resiliency-overview/), [OpenTelemetry .NET documentation](https://opentelemetry.io/docs/languages/dotnet/), local files `src/Hexalith.Folders.ServiceDefaults/Extensions.cs`, `Hexalith.Memories/src/Hexalith.Memories.Telemetry/MemoriesMeter.cs`.

### Team Organization and Skills

This feature crosses domain, worker, search, and operations boundaries. Treat it as a small cross-functional slice rather than a single library task.

Required skills:

- EventStore/CQRS modeling for the file lifecycle and projections.
- Dapr/Aspire operations for local and integration topology.
- Security engineering for tenant, ACL, path policy, and redaction.
- RAG/search engineering for metadata, chunking, hybrid search, and token budgets.
- Test architecture for fake-port unit tests and end-to-end Aspire tests.

Ownership recommendation:

- Folders domain owner: event/status semantics and authorization order.
- Worker owner: indexing adapter and retry/idempotency behavior.
- Memories owner: ingestion limits, delete/re-ingest APIs, chunking/streaming roadmap.
- QA/test owner: parity, sentinel leakage, and integration tests.
- Operations owner: dashboards, alerts, and Dapr policy.

_Source:_ [Azure Operational Excellence design principles](https://learn.microsoft.com/en-us/azure/well-architected/operational-excellence/principles), local files `_bmad-output/project-context.md`, `Hexalith.Memories/docs/operations/failure-recovery.md`.

### Cost Optimization and Resource Management

Costs come from extraction CPU, embedding calls, Redis/FalkorDB storage, vector index size, telemetry volume, and re-index/backfill runs. The implementation should minimize unnecessary indexing:

- Skip files excluded by path policy.
- Skip binary and generated files unless explicitly allowed.
- Use content hashes to avoid re-indexing unchanged bytes.
- Bound per-tenant indexing concurrency.
- Track token budgets and query result limits.
- Avoid storing raw paths/content in Folders projections.
- Keep telemetry low-cardinality and metadata-only.

The current 1 MB Memories inline ingestion limit is a cost and reliability guardrail, but it also blocks large-document RAG. Before raising it, prefer chunked indexing with per-chunk metadata or a Memories streaming/reference ingestion feature so workers do not load large files into memory or retry huge payloads.

_Source:_ [Azure Operational Excellence design principles](https://learn.microsoft.com/en-us/azure/well-architected/operational-excellence/principles), [Azure AI Search RAG overview](https://learn.microsoft.com/en-us/azure/search/retrieval-augmented-generation-overview), local file `Hexalith.Memories/src/Hexalith.Memories.Server/Activities/Ingestion/IngestionInputValidator.cs`.

### Risk Assessment and Mitigation

| Risk | Impact | Mitigation |
| --- | --- | --- |
| Cross-tenant or cross-folder leakage | Critical security failure | Folders authorizes before query; use tenant/case scope; fail closed on stale policy. |
| File contents leak into events/logs/projections | Compliance and audit failure | Keep events metadata-only; sentinel tests across telemetry and projections. |
| Memories unavailable during file commit | User workflow disruption if synchronous | Make indexing asynchronous and retryable; file commit remains durable. |
| Large files exceed Memories inline limit | Missing search coverage | Add skip status first; then chunking or Memories streaming/reference feature. |
| Duplicate memory units on replay/retry | Search noise and storage growth | Stable source URI/idempotency key; content hash; projection mapping. |
| Deleted/moved files remain searchable | Stale or unauthorized context | Memory-unit delete/tombstone projection and query filtering. |
| Retry storms | Downstream overload | Dapr resiliency policies, retry budgets, bounded worker concurrency. |
| Package version drift between Folders and Memories | Build/runtime incompatibility | Central package versions; adapter contract tests; avoid direct references outside Workers. |

_Source:_ [Transient fault handling](https://learn.microsoft.com/en-us/azure/architecture/best-practices/transient-faults), [Dapr resiliency overview](https://docs.dapr.io/operations/resiliency/resiliency-overview/), local files `_bmad-output/project-context.md`, `Hexalith.Memories/docs/operations/failure-recovery.md`.

## Technical Research Recommendations

### Implementation Roadmap

Phase 1: Design the Folders semantic indexing contract.

- Define `SemanticIndexingStatus`.
- Define `FolderSemanticIndexingRequest`.
- Define `IFolderSemanticIndexingClient`.
- Define source URI and metadata key conventions.
- Add fake adapter and unit tests.

Phase 2: Add Memories REST adapter in Workers.

- Reference `Hexalith.Memories.Client.Rest` and `Hexalith.Memories.Contracts`.
- Map file versions to `IngestAsync`.
- Handle Memories workflow instance ids.
- Map remote errors to retryable/terminal statuses.

Phase 3: Add indexing projection and worker orchestration.

- React to file-written/commit events.
- Apply path/security policy before indexing.
- Persist mapping from file version to Memories workflow/memory unit.
- Add skipped statuses.

Phase 4: Add authorized RAG query facade.

- Validate tenant/folder/path policy before search.
- Call Memories hybrid search with case/folder scope.
- Apply result eligibility checks and token budgets.
- Return redacted, source-attributed context.

Phase 5: Add lifecycle cleanup and backfill.

- Delete/tombstone memory units on file delete.
- Re-index on content hash change.
- Reconcile stale mappings.
- Backfill existing folders with bounded concurrency.

Phase 6: Add Aspire integration tests and operational dashboards.

- Local E2E indexing.
- Unauthorized query denial.
- Memories outage behavior.
- Metrics/trace correlation.

### Technology Stack Recommendations

- Keep C#/.NET 10 as the implementation stack.
- Use Folders Workers as the only initial project referencing Memories packages.
- Use Memories REST client first; Dapr service invocation can be a transport detail later.
- Use EventStore events only as triggers and metadata, never as content carriers.
- Use Memories `SourceType.File` for files and `SourceType.Event` only for event payload memory.
- Use hybrid search for RAG query defaults, with token budget and result caps.

### Skill Development Requirements

- Developers need comfort with EventStore/CQRS, Dapr/Aspire local topology, async worker design, and xUnit v3.
- Security reviewers need to review tenant/path policy, metadata classification, and search-result redaction.
- Operations needs Dapr resiliency, OpenTelemetry, Redis/FalkorDB/Memories failure recovery, and dashboard/alert ownership.
- Product/architecture need to decide large-file behavior, path sensitivity defaults, and query consistency expectations.

### Success Metrics and KPIs

- 100% of eligible file-write events produce an indexing status.
- 0 sentinel leaks across events, logs, traces, metrics, projections, audit records, console payloads, and query responses.
- 0 unauthorized RAG retrievals in negative integration tests.
- p95 commit acknowledgement unaffected by Memories availability.
- p95 authorized RAG query within the project context-query budget.
- Indexing backlog drains within the configured per-tenant SLO.
- Duplicate memory-unit rate under replay/retry is zero for equivalent file versions.
- Large/skipped files are visible as explicit statuses, never silently absent.
