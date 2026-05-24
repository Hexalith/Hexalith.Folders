# Story 2.9: React to Tenants events through Worker handlers

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a system component,
I want worker handlers to react to tenant lifecycle and membership events,
so that Folders authorization stays aligned with tenant administration.

## Terms

- Tenants event means a `Hexalith.Tenants.Contracts.Events` payload delivered through the `system.tenants.events` Dapr pub/sub subscription with `TenantEventContext` envelope metadata.
- Worker handler means the Folders-owned tenant-event handling surface hosted by `Hexalith.Folders.Workers`, not a new transport contract and not duplicate business logic in `Hexalith.Folders.Server`.
- Tenant-access projection means `FolderTenantAccessProjection` and its store/handler under `src/Hexalith.Folders/Projections/TenantAccess/`, used by `TenantAccessAuthorizer` to fail closed when tenant evidence is stale, unavailable, malformed, disabled, replay-conflicting, or mismatched.
- Folder authorization metadata means metadata-only tenant lifecycle, membership, role, and `folders.*` configuration evidence that can influence folder authorization. It does not include provider credentials, raw claim bags, group inventories beyond stable principal/role evidence, file contents, repository details, paths, or unauthorized resource existence.
- Event identity means tenant ID, message ID, event type, sequence number, timestamp, and payload fingerprint. Correlation ID is propagated for observability but must not be part of duplicate-delivery equality because Tenants/Dapr redelivery may rotate correlation IDs.
- Tenant-event ownership mode means the deployment/runtime setting that decides whether Server or Workers owns durable `system.tenants.events` projection writes during migration. It must be explicit, mutually exclusive for production writes, covered by tests, and observable through safe metadata only.
- Trusted Tenants event source means events accepted through the configured Hexalith.Tenants client/Dapr subscription path for `system.tenants.events`; worker code must not expose a public raw event ingestion surface, infer authority from caller-supplied payloads, or process tenant events from unrelated topics.

## Acceptance Criteria

1. Given the Folders worker host starts, when services are registered, then `Hexalith.Folders.Workers` subscribes to `system.tenants.events` through the existing Hexalith.Tenants client subscription pattern, uses the stable pub/sub component name `pubsub`, registers only the allowed Tenants event types for that topic, rejects or ignores unrelated topics and raw ingestion paths before projection mutation, and registers tenant event handlers for `TenantCreated`, `TenantUpdated`, `TenantDisabled`, `TenantEnabled`, `UserAddedToTenant`, `UserRemovedFromTenant`, `UserRoleChanged`, `TenantConfigurationSet`, and `TenantConfigurationRemoved` without requiring Aspire, Dapr sidecars, Redis, Keycloak, provider credentials, live Tenants data, or nested submodules for unit tests.
2. Given equivalent Tenants events are delivered through `Hexalith.Folders.Server` and `Hexalith.Folders.Workers` during the migration, when the handlers map events to folder tenant-access evidence, then both hosts use one shared internal mapping/projection implementation and produce the same `FolderTenantAccessProjection` outcome; this story must not create a second divergent tenant-event mapper, must not move behavior into `Hexalith.Folders.Contracts`, and must ensure production topology has one authoritative projection writer for `system.tenants.events` or a documented mutually exclusive compatibility switch that fails startup or disables one writer when misconfigured.
3. Given a tenant lifecycle event is received, when `TenantCreated` or `TenantEnabled` is applied, then the tenant projection becomes enabled; when `TenantDisabled` is applied, then the tenant projection becomes disabled and subsequent Folders mutations that require fresh tenant access fail closed through existing authorization results.
4. Given a tenant membership event is received, when `UserAddedToTenant`, `UserRemovedFromTenant`, or `UserRoleChanged` is applied, then only stable principal ID and role metadata update the local projection idempotently, removed users no longer authorize folder operations through tenant membership evidence, and raw membership inventories, auth headers, JWTs, claim bags, emails, display names, or secret-bearing attributes are not persisted or logged.
5. Given a tenant configuration event is received, when the key starts with `folders.`, then the handler records or tombstones only that `folders.*` key as metadata-only authorization configuration evidence; when the key does not start with `folders.`, then it is ignored without changing authorization state, projection freshness, or metadata exposure.
6. Given a duplicate Dapr/Tenants delivery has the same event identity and payload fingerprint, when the worker receives it again with the same or different correlation ID, then it is a no-op and does not advance state incorrectly; given the same message ID arrives with divergent tenant, event type, sequence, timestamp, or payload fingerprint evidence, then `ReplayConflict` is set and authorization fails closed without applying the conflicting mutation.
7. Given events arrive out of order, with future timestamps beyond clock-skew tolerance, non-positive sequence numbers, missing message IDs, missing tenant IDs, missing principal IDs for membership events, tenant envelope/payload mismatch, unsupported event type, unrelated topic, untrusted/raw ingestion path, or unsupported/malformed evidence, when the worker handles them, then the projection marks malformed evidence or drops the unsafe event according to the existing `FolderTenantAccessHandler` safety rules, surfaces retry/ack behavior through the existing Tenants client semantics without inventing a new poison-message policy, does not grant access, and does not construct folder, repository, workspace, provider, audit, cache, or file subjects.
8. Given the projection store detects optimistic concurrency conflicts or transient persistence failures, when concurrent worker deliveries target the same tenant or principal, then the handler retries within `TenantAccessOptions.ConcurrencyRetryAttempts`, preserves deterministic idempotency/order behavior, avoids shared mutable global state across tenants, principals, roles, actions, sequence windows, and configuration evidence, and leaves tenant authorization fail-closed when evidence cannot be durably advanced.
9. Given the worker updates tenant-access evidence, when logs, traces, metrics, diagnostics, exceptions, tests, or projection records are inspected across success, duplicate, replay-conflict, malformed, envelope-mismatch, and concurrency-failure paths, then they contain only tenant ID, event kind, message ID, sequence/watermark, correlation ID, safe outcome, retry count when present, projection version/watermark when present, and sanitized reason metadata; file contents, diffs, provider tokens, credential material, repository URLs/names, branch names, raw payload bodies, raw claims, group inventories, and unauthorized resource existence are absent.
10. Given `TenantAccessAuthorizer` evaluates a mutation after tenant events have been processed, when the local projection is enabled, fresh, non-conflicting, and contains allowed principal evidence, then existing allowed authorization behavior remains available; when tenant evidence is disabled, stale, unavailable, malformed, replay-conflicting, unknown, or mismatched, then existing safe denial/read-model unavailable result codes are preserved without adding new response families or changing folder authorization behavior except where tenant projection evidence changed.
11. Given this story owns worker-side tenant-event handling only, when implementation is complete, then it does not implement provider readiness, repository binding, workspace workflows, folder ACL grant/revoke semantics beyond consuming projection evidence, operations-console UI, CLI/MCP commands, tenant administration APIs, webhooks, repair automation, tenant deletion, legal hold, or hard cleanup.

## Tasks / Subtasks

- [x] Move tenant-event subscription ownership into the worker host without semantic drift. (AC: 1, 2)
  - [x] Add a worker registration extension such as `AddFoldersTenantEventWorkers` or extend `FoldersWorkersModule` to call `AddDaprClient`, `AddFoldersTenantAccess`, and `AddHexalithTenants` with `PubSubName = "pubsub"` and `TopicName = "system.tenants.events"`.
  - [x] Map the Tenants subscription endpoint from the worker host if the existing Tenants client subscription model requires an HTTP callback endpoint in the worker process.
  - [x] Keep the stable worker app ID expectation `folders-workers` aligned with AppHost/Aspire configuration.
  - [x] Reuse shared constants for topic and pub/sub names rather than duplicating string literals between Server and Workers.
  - [x] Preserve server behavior only as needed for compatibility; if handler registration moves out of Server, remove or narrow server registration in the same change so there is one authoritative handler surface and Server/Workers cannot both apply the same durable tenant-access projection in production.
  - [x] If a compatibility switch keeps both host paths buildable, document the mutually exclusive deployment setting and add tests proving duplicate application is either unreachable or converges idempotently.
  - [x] Add startup/options validation proving the production ownership mode cannot enable two durable projection writers for the same Tenants topic; diagnostics must report only safe ownership metadata, never tenant payloads.
  - [x] Verify the worker does not expose a raw public Tenants event ingestion endpoint outside the configured Tenants client subscription path.
- [x] Extract or reuse a single Tenants-to-Folders mapping implementation. (AC: 2, 6, 7, 9)
  - [x] Move the existing `FoldersTenantEventHandler` logic from `src/Hexalith.Folders.Server/` into a shared worker/domain location, or create a shared mapper consumed by both Server and Workers during transition.
  - [x] Keep the shared mapper as internal implementation code in Server/Workers/core boundaries; do not add tenant-event behavior to `Hexalith.Folders.Contracts`, generated clients, OpenAPI, CLI, MCP, or public DTO packages.
  - [x] Preserve current event coverage: tenant created/updated/disabled/enabled, user added/removed/role changed, and configuration set/removed.
  - [x] Whitelist supported Tenants event types at the mapper boundary and keep unknown event types/unrelated topics on a no-mutation path with safe outcome metadata.
  - [x] Preserve the current payload fingerprint rule: deterministic hash over safe event fields, with correlation ID excluded from replay equality.
  - [x] Preserve envelope/payload tenant mismatch handling as a drop/fail-closed path that does not mutate either tenant projection.
  - [x] Keep logs structured and metadata-only.
- [x] Apply lifecycle and membership events through the existing projection handler. (AC: 3, 4, 8, 10)
  - [x] Reuse `FolderTenantAccessHandler`, `FolderTenantAccessEvent`, `FolderTenantAccessProjection`, `FolderTenantEventEvidence`, and `IFolderTenantAccessProjectionStore`.
  - [x] Delegate all projection mutation and safety decisions to `FolderTenantAccessHandler`; worker adapters must not fork idempotency, replay conflict, out-of-order, freshness, malformed-evidence, or concurrency rules.
  - [x] Ensure disabled tenants revoke mutating authorization through the existing `TenantAccessOutcome.DisabledTenant` mapping.
  - [x] Ensure removed users and changed roles update only principal/role evidence and do not leave stale allow entries.
  - [x] Verify stale, unavailable, malformed, replay-conflicting, unknown, disabled, mismatch, and missing-authority outcomes still map through `TenantAccessAuthorizer` without new result families.
- [x] Restrict configuration processing to `folders.*`. (AC: 5)
  - [x] Keep `TenantConfigurationSet` and `TenantConfigurationRemoved` as metadata-only inputs.
  - [x] Ignore non-Folders configuration keys before storing authorization evidence.
  - [x] Tombstone removed `folders.*` keys through `RemovedConfigurationKeys` so replays and audits can distinguish removed from never-seen configuration without storing raw values.
  - [x] Do not persist configuration values unless a later story explicitly defines a safe, typed Folders configuration contract.
- [x] Add worker-focused tests and preserve existing projection tests. (AC: 1-10)
  - [x] Add `tests/Hexalith.Folders.Workers.Tests/Tenants/*` coverage for worker service registration, handler dispatch, stable topic/pubsub names, and offline startup shape.
  - [x] Add startup/registration negative coverage for disabled Tenants integration, missing optional configuration, invalid topic/pubsub configuration, and unrelated Tenants topics proving they do not process folder authorization evidence.
  - [x] Add production ownership-mode tests proving Server/Workers double-writer misconfiguration fails startup or disables exactly one writer before any event is handled.
  - [x] Move or duplicate only as needed the current server tests for envelope mismatch and `TenantUpdated` freshness into worker tests; keep one source of behavior truth.
  - [x] Add mapper parity coverage proving Worker routing uses the shared mapper or produces the same safe evidence as the existing Server mapping during migration.
  - [x] Extend projection tests for duplicate delivery with rotated correlation IDs, divergent replay conflict, out-of-order delivery, future timestamps, missing message IDs, missing principal IDs, unsupported event types, unrelated topics, concurrent saves/retry, transient persistence failure, tenant disabled fail-closed behavior, user removal authorization evidence, role change replacement, and `folders.*` configuration filtering.
  - [x] Add leakage/sentinel tests proving worker logs/projections/exceptions omit secrets, raw payloads, provider data, repository/path data, file contents, claim bags, and membership inventories on success, duplicate, malformed, replay-conflict, unknown-event, unrelated-topic, raw-ingestion, envelope-mismatch, and concurrency-failure paths.
  - [x] Tests must run with in-memory stores/fakes and without Dapr, Aspire, Redis, Keycloak, provider credentials, live Tenants services, or nested submodule initialization.

## Dev Notes

### Source Context

- Epic 2 requires tenant administrators and authorized actors to manage folder lifecycle and access with cross-tenant isolation before any resource access. Story 2.9 specifically requires worker handlers to react to tenant lifecycle and membership events, update local tenant-access projections and folder authorization metadata idempotently, and process only `folders.*` configuration keys. [Source: `_bmad-output/planning-artifacts/epics.md#Story 2.9`]
- Architecture requires Hexalith.Tenants as the source of truth for tenant identity, lifecycle, and membership; Folders consumes `system.tenants.events` through Dapr pub/sub and maintains a local fail-closed tenant-access projection. [Source: `_bmad-output/planning-artifacts/architecture.md#Technology Stack Summary`]
- Architecture names worker tenant handlers under `src/Hexalith.Folders.Workers/Tenants/TenantEventHandlers/` with `TenantDisabledHandler`, `UserRemovedFromTenantHandler`, `UserRoleChangedHandler`, and `TenantConfigurationSetHandler`, and says `TenantConfigurationSetHandler` processes `folders.*` keys only. [Source: `_bmad-output/planning-artifacts/architecture.md#Recommended Project Layout`]
- Architecture requires worker/process-manager side effects to live in `Hexalith.Folders.Workers`; aggregates remain pure and do not call Dapr, HTTP, file I/O, Git, secret stores, databases, clocks, or randomness. [Source: `_bmad-output/planning-artifacts/architecture.md#Process Patterns`]
- PRD states Hexalith.Tenants remains the source of truth for tenant identity, lifecycle, and membership while Hexalith.Folders owns folder-specific policy, ACLs, provider binding references, workspace state, file-operation facts, commit metadata, and operational projections. [Source: `_bmad-output/planning-artifacts/prd.md#Product Scope`]
- Project context requires zero cross-tenant leakage, metadata-only events/logs/traces/metrics/projections/audit, tenant-prefixed keys, fail-closed authorization, and no recursive nested submodule initialization. [Source: `_bmad-output/project-context.md#Critical Don't-Miss Rules`]

### Previous Story Intelligence

- Story 2.1 established Folders service hosting with Tenants integration, subscription to `system.tenants.events`, and local `FolderTenantAccessProjection` semantics. Story 2.9 should move or harden that capability in the worker host rather than inventing a second projection model.
- Story 2.2 established ACL metadata-only safety and tenant evidence gates. Tenant-event handling must feed authorization evidence and avoid leaking membership or credential details.
- Story 2.3 established folder aggregate purity, tenant-scoped IDs, and offline aggregate tests. This story should not touch folder aggregate behavior except through existing authorization evidence consumers.
- Story 2.4 is actively in progress in this working tree. Do not overwrite its uncommitted folder access command/projection/test work; consume its ACL concepts only through stable story guidance and current source after rebasing carefully.
- Story 2.5 defines effective-permission inspection. This story updates evidence consumed by permission/authorization paths but must not add a public permission-query surface.
- Story 2.6 defines layered authorization and safe denials. Tenant worker events must keep those result families stable.
- Story 2.7 defines lifecycle/status observation and stale/unavailable no-fallback behavior. Tenant projection freshness must remain explicit.
- Story 2.8 defines archived lifecycle behavior and mutation guards. Tenant disablement or membership removal must affect active and archived folder authorization consistently without adding archive behavior here.

### Existing Implementation State

- `src/Hexalith.Folders.Server/FoldersTenantEventHandler.cs` already maps Tenants events to `FolderTenantAccessEvent`, computes deterministic fingerprints, drops envelope/payload tenant mismatches by emitting an empty tenant ID, and logs safe structured warnings.
- `src/Hexalith.Folders.Server/FoldersServerModule.cs` currently registers `AddDaprClient`, `AddFoldersTenantAccess`, `AddHexalithTenants`, the tenant event projection handler, and `MapTenantEventSubscription()` using `TenantEventsPubSubName = "pubsub"` and `TenantEventsTopicName = "system.tenants.events"`.
- `src/Hexalith.Folders.Workers/Program.cs` currently only registers `AddFoldersTenantAccess()` and runs a placeholder host.
- `src/Hexalith.Folders.Workers/FoldersWorkersModule.cs` is a placeholder module with only the module name.
- `src/Hexalith.Folders/Projections/TenantAccess/FolderTenantAccessHandler.cs` already enforces idempotency, divergent replay conflict, out-of-order drops, malformed evidence, clock-skew checks, `folders.*` configuration filtering, optimistic concurrency retry, watermarking, and metadata-only projection updates.
- `tests/Hexalith.Folders.Tests/Projections/TenantAccess/FolderTenantAccessHandlerTests.cs` already covers metadata-only membership projection, divergent duplicate replay conflict, non-Folders configuration filtering, removed Folders configuration tombstones, and future timestamp fail-closed behavior.
- `tests/Hexalith.Folders.Server.Tests/FoldersTenantEventHandlerTests.cs` already covers envelope mismatch no-mutation and `TenantUpdated` freshness.
- `tests/Hexalith.Folders.Workers.Tests/WorkersSmokeTests.cs` currently asserts only placeholder worker module identity and must be replaced or extended with meaningful worker registration/handler tests.

### Required Architecture Patterns

- Use .NET 10, C# file-scoped namespaces, nullable-aware code, one public type per file, PascalCase public members, camelCase locals/parameters, and async APIs with `CancellationToken`.
- Keep `Hexalith.Folders.Contracts` behavior-free. Do not add tenant-event behavior, projection logic, or worker registration there.
- Keep `Hexalith.Folders.Workers` as the host for tenant-event worker handling and future process managers. It may reference core/domain and Tenants client abstractions, but it must not duplicate server-only transport semantics.
- Keep `FolderTenantAccessHandler` as the core projection behavior. Add narrow adapters/mappers around it instead of forking projection state or authorization semantics.
- Keep one authoritative production writer for Tenants-to-Folders projection events. Idempotency remains required for redelivery and migration safety, but it is not a substitute for clear Server/Workers ownership.
- Use `Hexalith.Tenants.Client.Handlers.ITenantEventHandler<TEvent>` and `TenantEventContext` for event dispatch; avoid direct Dapr SDK payload parsing in Folders code unless the Tenants client pattern cannot support the worker host.
- Treat Tenants event delivery as at-least-once. Deduplicate by message/evidence identity, tolerate ordinary duplicate delivery, and fail closed on divergent replay.
- Use `TenantAccessOptions` for clock-skew and concurrency retry settings. Do not add hardcoded freshness windows or retry loops.
- Logs and diagnostics must use structured templates and safe metadata only.

### Files To Touch

- `src/Hexalith.Folders.Workers/Program.cs`
- `src/Hexalith.Folders.Workers/FoldersWorkersModule.cs`
- `src/Hexalith.Folders.Workers/Tenants/TenantEventHandlers/*` or a similarly narrow worker tenant-event folder
- `src/Hexalith.Folders.Workers/Hexalith.Folders.Workers.csproj`
- `src/Hexalith.Folders.Server/FoldersTenantEventHandler.cs` only if extracting/moving shared behavior
- `src/Hexalith.Folders.Server/FoldersServerModule.cs` only if removing duplicate server handler ownership or sharing constants
- `src/Hexalith.Folders/Projections/TenantAccess/*` only for narrowly needed projection/evidence extensions
- `tests/Hexalith.Folders.Workers.Tests/*`
- `tests/Hexalith.Folders.Tests/Projections/TenantAccess/*`
- `tests/Hexalith.Folders.Server.Tests/FoldersTenantEventHandlerTests.cs` only to preserve or relocate existing behavior tests

### Do Not Touch

- Do not implement provider readiness, repository binding, workspace preparation, locks, file mutation, commits, context queries, UI, CLI, MCP, tenant administration APIs, webhooks, repair workflows, tenant deletion, legal hold, hard cleanup, or audit browsing.
- Do not modify `src/Hexalith.Folders.Client/Generated/*`.
- Do not add a second OpenAPI operation, SDK method, or public contract for tenant events unless a later contract story explicitly requires it.
- Do not store configuration values, raw Tenants payloads, raw request bodies, raw claims, membership inventories, emails, display names, provider data, repository/path data, file contents, diffs, credentials, or secrets.
- Do not call provider APIs, Git, filesystem working copies, audit browsing APIs, repository APIs, or EventStore aggregate scans from tenant-event handlers.
- Do not initialize nested submodules or use recursive submodule commands.

### Testing

- Use xUnit v3 and Shouldly. Use NSubstitute only where a real seam needs substitution.
- Worker tests must be offline and not require Dapr sidecars, Aspire, Redis, Keycloak, provider credentials, live Tenants services, production secrets, or nested submodules.
- Add registration tests proving the worker host registers the Tenants client subscription, all supported `ITenantEventHandler<TEvent>` implementations, `FolderTenantAccessHandler`, projection store, and the expected topic/pubsub options.
- Add behavior tests for tenant created/enabled/disabled, tenant updated freshness, user added/removed/role changed, configuration set/removed, non-Folders key ignore, duplicate delivery, divergent replay conflict, malformed evidence, envelope mismatch, out-of-order delivery, future timestamp, concurrency retry, and authorization fail-closed outcomes.
- Add leakage tests using sentinel strings to inspect projection state, logs, diagnostics, exceptions, and test output for forbidden content.
- Preserve current server/projection tests during any move so behavior is proven before and after extraction.

### Regression Traps

- Do not leave Server and Workers both applying the same Tenants event to the same durable projection in production topology.
- Do not rely on idempotency alone to compensate for a double-writer production topology; the ownership mode must prevent or fail closed before durable writes begin.
- Do not expose a raw event ingestion path that lets callers bypass the configured Hexalith.Tenants subscription trust boundary.
- Do not compute tenant authority from payload tenant ID when envelope/context disagrees.
- Do not include correlation ID in duplicate-delivery equality.
- Do not process non-`folders.*` configuration keys or store configuration values.
- Do not let `TenantUpdated` grant membership or change roles; it may advance freshness only.
- Do not advance projection state on malformed, replay-conflicting, or envelope-mismatched evidence.
- Do not treat out-of-order redelivery as malicious when the existing watermark already advanced safely.
- Do not broaden tenant disablement into folder archive/delete/cleanup behavior.
- Do not create a new authorization result vocabulary when existing `TenantAccessOutcome` mappings already cover disabled, stale, unavailable, malformed, unknown, mismatch, missing authority, denied, and replay conflict.
- Do not use a global in-memory singleton or cache that can replay tenant decisions across tenants, principals, roles, actions, sequence windows, or configuration evidence.
- Do not log raw payloads or exception details that include forbidden content.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 2.9`
- `_bmad-output/planning-artifacts/prd.md#Product Scope`
- `_bmad-output/planning-artifacts/prd.md#Security and Tenant Isolation`
- `_bmad-output/planning-artifacts/architecture.md#Technology Stack Summary`
- `_bmad-output/planning-artifacts/architecture.md#Recommended Project Layout`
- `_bmad-output/planning-artifacts/architecture.md#Communication Patterns`
- `_bmad-output/planning-artifacts/architecture.md#Process Patterns`
- `_bmad-output/project-context.md#Critical Don't-Miss Rules`
- `_bmad-output/implementation-artifacts/2-1-stand-up-domain-service-host-with-tenants-integration.md`
- `_bmad-output/implementation-artifacts/2-2-implement-organization-aggregate-acl-baseline.md`
- `_bmad-output/implementation-artifacts/2-4-grant-and-revoke-folder-access.md`
- `_bmad-output/implementation-artifacts/2-6-enforce-layered-authorization-with-safe-denials.md`
- `src/Hexalith.Folders.Server/FoldersTenantEventHandler.cs`
- `src/Hexalith.Folders.Server/FoldersServerModule.cs`
- `src/Hexalith.Folders.Workers/Program.cs`
- `src/Hexalith.Folders.Workers/FoldersWorkersModule.cs`
- `src/Hexalith.Folders/Projections/TenantAccess/FolderTenantAccessHandler.cs`
- `src/Hexalith.Folders/Projections/TenantAccess/FolderTenantAccessProjection.cs`
- `src/Hexalith.Folders/Authorization/TenantAccessAuthorizer.cs`
- `tests/Hexalith.Folders.Tests/Projections/TenantAccess/FolderTenantAccessHandlerTests.cs`
- `tests/Hexalith.Folders.Server.Tests/FoldersTenantEventHandlerTests.cs`

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-24 | Adversarial code review (auto-fix): verified all 11 ACs against implementation, build (0 warnings) and tests (Workers 11, core TenantAccess 22, Server tenant 2). Fixed a broken `.gitignore` entry (backslash escape that failed to ignore `.agents/.story-automator-active`) and removed redundant `FoldersTenantEventOptions` bindings in the Server/Workers modules. No critical issues; status → done. | Jérôme Piquot |
| 2026-05-24 | Implemented worker-owned Tenants event subscription, shared tenant-access event mapping, projection writer ownership mode, worker/core tests, and Dapr package alignment for Tenants client compatibility. | Codex |
| 2026-05-19 | Applied advanced elicitation hardening for trusted Tenants source boundaries, ownership-mode startup guards, unsupported/unrelated event handling, transient persistence failure, and sentinel leakage tests. | Codex |
| 2026-05-19 | Applied party-mode review hardening for single-writer Server/Workers ownership, internal shared mapper boundaries, malformed-event disposition, duplicate correlation behavior, and offline leakage/registration tests. | Codex |
| 2026-05-19 | Created story with worker tenant-event subscription, shared Tenants mapping, projection idempotency, `folders.*` filtering, fail-closed authorization, and offline worker tests. | Codex |

## Party-Mode Review

- Date/time: 2026-05-19T09:43:57+02:00
- Selected story key: `2-9-react-to-tenants-events-through-worker-handlers`
- Command/skill invocation used: `/bmad-party-mode 2-9-react-to-tenants-events-through-worker-handlers; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), John (Product Manager)
- Findings summary: The round found the story direction sound but under-specified around Server-to-Worker projection ownership, shared mapper boundaries, duplicate equality, malformed-event disposition, and negative/leakage test coverage.
- Changes applied: Clarified one authoritative production writer or mutually exclusive compatibility switch; required internal shared mapper behavior outside Contracts/generated clients/public transports; required Worker adapters to delegate projection safety rules to `FolderTenantAccessHandler`; clarified correlation ID exclusion for duplicate equality; clarified malformed/future/mismatch handling through existing handler and Tenants client semantics; expanded offline registration, mapper parity, and failure-path leakage tests.
- Findings deferred: Live Dapr pub/sub, Aspire orchestration, Redis persistence, Keycloak auth, live Tenants contract validation, provider-backed integration tests, tenant deletion, repair/backfill automation, and admin-facing workflows remain out of scope for this story.
- Final recommendation: ready-for-dev

## Advanced Elicitation

- Date/time: 2026-05-19T12:02:46+02:00
- Selected story key: `2-9-react-to-tenants-events-through-worker-handlers`
- Command/skill invocation used: `/bmad-advanced-elicitation 2-9-react-to-tenants-events-through-worker-handlers`
- Batch 1 method names: Security Audit Personas; Failure Mode Analysis; Pre-mortem Analysis; Self-Consistency Validation; Critique and Refine
- Reshuffled Batch 2 method names: Red Team vs Blue Team; Chaos Monkey Scenarios; First Principles Analysis; 5 Whys Deep Dive; Architecture Decision Records
- Findings summary: The elicitation found the story was implementation-ready but needed tighter trust-boundary wording for accepted Tenants events, explicit production ownership-mode failure behavior, unknown/unrelated event no-mutation handling, transient persistence failure semantics, and negative leakage coverage.
- Changes applied: Added terms for tenant-event ownership mode and trusted Tenants event source; tightened worker registration and migration ownership acceptance criteria; extended malformed-event and persistence-failure criteria; added startup/options validation, raw-ingestion denial, event-type/topic whitelist, double-writer misconfiguration, rotated-correlation duplicate, transient failure, unrelated-topic, and raw-ingestion leakage test expectations; added regression traps for idempotency-as-topology-control and raw ingestion bypasses.
- Findings deferred: Formal ownership-mode option names, live Dapr/Tenants source-authentication integration tests, durable poison-message disposition policy, cross-host deployment rollout plan, and any new public diagnostics or administration surface remain product/architecture decisions outside this story.
- Final recommendation: ready-for-dev

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet restore Hexalith.Folders.slnx` - passed after aligning Dapr package versions and removing redundant Worker hosting package reference.
- `dotnet build Hexalith.Folders.slnx --no-restore` - passed with 0 warnings and 0 errors.
- `dotnet test tests\Hexalith.Folders.Tests\Hexalith.Folders.Tests.csproj --no-restore --filter FullyQualifiedName~TenantAccess` - passed 22 tests.
- `dotnet test tests\Hexalith.Folders.Workers.Tests\Hexalith.Folders.Workers.Tests.csproj --no-restore` - passed 8 tests.
- `dotnet test tests\Hexalith.Folders.Server.Tests\Hexalith.Folders.Server.Tests.csproj --no-restore` - passed 70 tests.
- `dotnet test Hexalith.Folders.slnx --no-build --no-restore` - passed 661 tests, skipped 1 existing UI E2E placeholder.

### Completion Notes List

- Story created by `/bmad-create-story 2-9-react-to-tenants-events-through-worker-handlers` equivalent workflow on 2026-05-19.
- Project context, Epic 2, PRD, architecture, current tenant projection/server/worker implementation, worker tests, Tenants client patterns, recent commits, and story-creation lessons were reviewed.
- Preflight working-tree failure was classified as an active-dev-story soft warning because Story 2.4 is `in-progress` in both its artifact and sprint status; active development changes were left untouched.
- Ultimate context engine analysis completed - comprehensive developer guide created.
- Advanced elicitation completed on 2026-05-19T12:02:46+02:00; low-risk source-boundary, ownership-mode, failure-disposition, and leakage-test clarifications were applied inline.
- Added `AddFoldersTenantEventWorkers` and worker endpoint mapping for the trusted Tenants client subscription at `pubsub` / `system.tenants.events`, with stable `folders-workers` metadata and no additional raw ingestion route.
- Extracted shared tenant-event subscription constants and shared `FoldersTenantAccessEventMapper`; Server and Workers use the same mapper while `Folders:TenantEvents:ProjectionWriter` (`Workers`, `Server`, or `Disabled`) makes durable projection ownership mutually exclusive.
- Worker handlers now dispatch supported Tenants lifecycle, membership, role, and configuration events into `FolderTenantAccessHandler`; projection safety, idempotency, replay conflict, out-of-order, malformed evidence, and `folders.*` filtering remain centralized in the core handler.
- Extended tenant-access projection retry behavior for explicit transient persistence failures and timeout failures within `TenantAccessOptions.ConcurrencyRetryAttempts`.
- Added worker registration/endpoint/ownership/parity/unsupported-event/sentinel tests and expanded core projection tests for duplicate correlation rotation, out-of-order delivery, missing evidence, user removal, role replacement, tenant disabled fail-closed behavior, concurrency retry, and transient persistence retry.
- Updated scaffold dependency policy for the Worker host to allow the Tenants client/contracts references required by this story and aligned Dapr packages to `1.17.9` to match the current Tenants client dependency floor.

### File List

- `.gitignore`
- `Directory.Packages.props`
- `_bmad-output/implementation-artifacts/2-9-react-to-tenants-events-through-worker-handlers.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Folders.Server/FoldersServerModule.cs`
- `src/Hexalith.Folders.Server/FoldersTenantEventHandler.cs`
- `src/Hexalith.Folders.Workers/FoldersWorkersModule.cs`
- `src/Hexalith.Folders.Workers/Hexalith.Folders.Workers.csproj`
- `src/Hexalith.Folders.Workers/Program.cs`
- `src/Hexalith.Folders.Workers/Tenants/TenantEventHandlers/FoldersTenantEventHandler.cs`
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs`
- `src/Hexalith.Folders/Projections/TenantAccess/FolderTenantAccessHandler.cs`
- `src/Hexalith.Folders/Projections/TenantAccess/FoldersTenantAccessEventMapper.cs`
- `src/Hexalith.Folders/Projections/TenantAccess/FoldersTenantEventOptions.cs`
- `src/Hexalith.Folders/Projections/TenantAccess/FoldersTenantEventOptionsValidator.cs`
- `src/Hexalith.Folders/Projections/TenantAccess/FoldersTenantEventProjectionWriter.cs`
- `src/Hexalith.Folders/Projections/TenantAccess/FoldersTenantEventSubscription.cs`
- `src/Hexalith.Folders/Projections/TenantAccess/TenantAccessTransientPersistenceException.cs`
- `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs`
- `tests/Hexalith.Folders.Tests/Projections/TenantAccess/FolderTenantAccessHandlerTests.cs`
- `tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj`
- `tests/Hexalith.Folders.Workers.Tests/WorkersSmokeTests.cs` (deleted)
- `tests/Hexalith.Folders.Workers.Tests/WorkersTenantEventTests.cs`

## Senior Developer Review (AI)

- Reviewer: Jérôme Piquot
- Date: 2026-05-24
- Outcome: **Approve** (status → done; 0 critical issues)
- Mode: adversarial review with automatic fixes

### Verification performed

- Cross-referenced the story File List against `git status`: every claimed file is present in the working tree (and vice versa); no false "changed" claims. The only undocumented working-tree change to repo source was `.gitignore` (now added to the File List and fixed below).
- Built `Hexalith.Folders.Workers.Tests` (transitively core + Server + Workers): **0 warnings, 0 errors** under `TreatWarningsAsErrors=true`.
- Ran tests: Workers `11/11`, core `~TenantAccess` `22/22`, Server `~FoldersTenantEventHandler` `2/2` — all green.
- Validated each Acceptance Criterion against source:
  - AC1 — `AddFoldersTenantEventWorkers` registers all nine `ITenantEventHandler<T>`, stable `pubsub`/`system.tenants.events`, `/tenants/events` trusted route only, no raw ingestion endpoint; offline registration/endpoint tests confirm. ✓
  - AC2 — single shared `FoldersTenantAccessEventMapper` consumed by both hosts; `FoldersTenantEventProjectionWriter` switch makes durable writes mutually exclusive; mapper lives in core, not Contracts. ✓
  - AC3–AC5 — lifecycle enable/disable, metadata-only membership/role, `folders.*`-only configuration with tombstones, all delegated to `FolderTenantAccessHandler`. ✓
  - AC6–AC8 — duplicate no-op (correlation-id excluded from evidence equality), divergent replay conflict, out-of-order drop, malformed/future/missing-id fail-closed, bounded concurrency + transient-persistence + timeout retry, final-attempt exception propagates (no silent ack). ✓
  - AC9 — log/exception templates interpolate metadata only; sentinel tests confirm secrets/payloads absent in projection state, fingerprints, and HTTP responses. ✓
  - AC10–AC11 — `TenantAccessAuthorizer` outcome families unchanged; no provider/repo/workspace/UI/CLI/MCP scope creep. ✓

### Findings and actions

- **[Fixed] LOW — `.gitignore` entry was inert.** `.agents\.story-automator-active` used a backslash, which git treats as an escape, so the runtime marker was never ignored (`git status` still listed it untracked). Changed to `.agents/.story-automator-active`; `git check-ignore` now matches.
- **[Fixed] LOW — Redundant options binding.** `FoldersWorkersModule` and `FoldersServerModule` re-bound `FoldersTenantEventOptions` even though `AddFoldersTenantAccess()` already binds it with `ValidateOnStart()` and its validator. Removed the duplicate `BindConfiguration` calls (single binding site; behavior unchanged, tests still green).
- **[Accepted, no change] LOW — Duplicated dispatch surface.** Server and Workers each define a near-identical nine-method `FoldersTenantEventHandler`. The locked dependency graph (`ScaffoldContractTests`) forbids a shared host-adjacent project and keeps `ITenantEventHandler<T>` adapters out of core, so the duplication is structurally forced during the transition. Divergence risk is mitigated by `WorkerAndServerHandlersShouldProduceSameProjectionWhenEachOwnsWrites` (parity test).
- **[Accepted, no change] LOW/ops — Default `ProjectionWriter = Workers`.** A Server-only deployment that upgrades without deploying Workers (or setting `Folders:TenantEvents:ProjectionWriter=Server`) will stop writing the projection and fail closed. This is the intended migration default; it is safe (fail-closed, never fail-open) and matches the AppHost topology that deploys Workers. Operators must deploy Workers or set the switch.
- **[Observation] LOW — Log-leakage coverage is structural, not capture-based.** No test captures emitted log output to assert sentinel absence; safety relies on metadata-only templates. Adequate, but a log-capture sentinel test would harden AC9 further (candidate for a follow-up).
