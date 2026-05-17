# Story 2.3: Create folders within a tenant

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an authorized actor,
I want to create logical folders inside my tenant,
so that repository-backed workspace tasks have a tenant-scoped logical home.

## Terms

- Authoritative tenant context means the managed tenant ID supplied by the authenticated execution context or EventStore envelope. Tenant IDs in request bodies, routes, query strings, or client-controlled headers are validation inputs only.
- Tenant and ACL gates mean the ordered authorization boundary of authoritative tenant context, Story 2.1 tenant-access evidence, and Story 2.2 `create_folder` ACL evidence.
- Safe metadata means stable metadata fields that are allowed in events, results, logs, diagnostics, audit, and projections: tenant ID, folder ID, lifecycle/result code, safe display name, optional safe description/tags/path label, correlation/task/idempotency IDs, safe actor/principal ID, and event version/sequence when available.
- Opaque folder ID means an immutable generated or validated identifier that is never derived from tenant name, display name, path label, repository name, provider name, stream name, or folder hierarchy.
- Minimal replay projection means the smallest tenant-scoped read model needed to prove creation event replay for existence, lifecycle state, and safe metadata. It is not a public folder listing/query feature.
- Durable folder-create key means any idempotency, duplicate-detection, cache, or operation key introduced for this command. It must be derived only after tenant and ACL gates pass and must include authoritative tenant ID plus opaque folder ID.
- Concurrent duplicate means a same-tenant same-folder creation race observed through expected-version, append-conflict, or already-created state evidence. It is handled as deterministic duplicate/idempotency evidence, not as a second creation event or an infrastructure exception leak.

## Acceptance Criteria

1. Given the `FolderAggregate` does not yet exist, when this story is implemented, then the aggregate, state, command, event, result, and rejection types are introduced under `src/Hexalith.Folders/Aggregates/Folder/` using EventStore-oriented naming and opaque folder identity.
2. Given authoritative managed tenant context, Story 2.1 tenant-access evidence, and Story 2.2 organization ACL permission for `create_folder` are all present in that order, when `CreateFolder` is accepted, then the folder stream is created in the `{managedTenantId}:folders:{folderId}` shape with an active logical lifecycle state and safe metadata needed for later repository binding.
3. Given tenant identity appears in command payloads, routes, query parameters, or client-controlled headers, when create-folder command context is built, then tenant authority comes only from authentication context or EventStore envelopes; payload tenant IDs are validation inputs only.
4. Given folder identity is supplied or generated, when the command is processed, then folder IDs are opaque immutable values and are never derived from display name, path, repository name, provider name, or tenant name.
5. Given folder metadata is supplied, when it is accepted, then display name, optional description, optional projected path label, and optional tags are validated as metadata-only values; invalid metadata rejects before durable/cache key construction, stream loading, duplicate lookup, append, projection update, diagnostics, audit lookup, or mutation; raw file contents, diffs, generated context payloads, provider credential material, repository URLs, branch names, and unauthorized resource identifiers are rejected or omitted.
6. Given tenant-access evidence is stale, unavailable, disabled, unknown, malformed, future-dated, replay-conflicting, tenant-mismatched, denied, or missing authoritative tenant context, when `CreateFolder` is evaluated, then it rejects before stream-name construction, durable/cache key construction, stream loading, duplicate lookup, event append, aggregate mutation, projection update, audit lookup, diagnostic lookup, provider readiness checks, or repository operations.
7. Given ACL evidence does not grant `create_folder`, is unavailable, or cannot be evaluated from stable Story 2.2 result evidence, when `CreateFolder` is evaluated, then it rejects with metadata-only `folder_acl_denied` or equivalent stable evidence before durable/cache key construction, stream loading, duplicate lookup, append, projection update, diagnostics, audit lookup, or mutation.
8. Given the same idempotency key and equivalent canonical create-folder payload are retried after tenant and ACL gates pass, when the command is processed, then the same logical result is returned without duplicating events and with durable idempotency keys scoped by authoritative tenant ID; given the same idempotency key and materially different payload are processed, then the command rejects as `idempotency_conflict`; given idempotency evidence is unavailable after authorization succeeds, then the command fails closed before append without disclosing folder existence; given tenant or ACL gates fail, then prior folder existence and idempotency records are not disclosed.
9. Given a folder already exists for the same opaque folder ID in the same tenant, when create is retried without matching idempotency equivalence or races another same-folder create, then the command returns deterministic duplicate/already-exists evidence without appending a second creation event; the same opaque folder ID in a different authoritative tenant is a different stream; display-name, projected path-label, and parent-folder uniqueness are deferred and must not be introduced as duplicate rules in this story.
10. Given a folder creation result is accepted, rejected, duplicate, unauthorized, idempotency-unavailable, or idempotency-conflicted, when callers or tests inspect it, then stable result codes such as `created`, `idempotent_replay`, `idempotency_conflict`, `idempotency_unavailable`, `duplicate_folder`, `append_conflict`, `invalid_folder_id`, `invalid_folder_metadata`, `tenant_evidence_missing`, `tenant_access_denied`, `folder_acl_denied`, `acl_evidence_unavailable`, and `validation_failed` plus safe metadata are available without parsing localized text, event type names, stack traces, or diagnostic strings.
11. Given tests run without provider credentials, tenant seed data, production secrets, running Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, or nested submodules, when unit and smoke tests execute, then folder creation, validation, tenant evidence gates, ACL gates, idempotency behavior, concurrent duplicate handling, stream shape, metadata-only leakage boundaries, and projection replay are covered with in-memory fakes and spies that fail on forbidden side effects.
12. Given this story creates logical folders only, when implementation is complete, then it does not implement provider readiness, repository creation, repository binding, workspace preparation, locks, file mutation, commits, folder ACL grant/revoke, effective-permission query endpoints, archive behavior, CLI/MCP/UI commands, workers, production Dapr policy mapping, repair workflows, local-only folder mode, webhooks, brownfield adoption, or multi-organization-per-tenant behavior.

## Tasks / Subtasks

- [ ] Create the Folder aggregate domain surface. (AC: 1, 2, 4, 9)
  - [ ] Add `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`.
  - [ ] Add `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs`.
  - [ ] Add `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs` or equivalent local event-application surface if sibling EventStore conventions use a separate apply type.
  - [ ] Add opaque value objects or validated identifiers for managed tenant ID and folder ID where existing project types do not already provide them.
  - [ ] Keep stream names in the `{managedTenantId}:folders:{folderId}` shape and reject empty segments, `:` characters, control characters, non-canonical casing when the project validator requires canonical casing, and the reserved `system` tenant for managed folder streams.
  - [ ] Keep any idempotency, duplicate-detection, cache, or operation keys in the same authoritative tenant scope; tests must fail if a durable folder-create key omits tenant ID or uses request-supplied tenant ID as authority.
  - [ ] Represent the initial lifecycle as active/logical-folder-created while keeping repository binding and workspace state unset or explicitly unbound for later stories.
- [ ] Define create-folder commands, events, and result evidence. (AC: 2, 5, 8, 10)
  - [ ] Add `CreateFolder` command and `FolderCreated` event, or equivalent names aligned with local EventStore naming.
  - [ ] Add result/rejection codes for accepted, already_exists, duplicate_folder, invalid_folder_id, invalid_folder_metadata, reserved_tenant, missing_authoritative_tenant, tenant_access_denied, stale_projection, unavailable_projection, unknown_tenant, disabled_tenant, malformed_evidence, tenant_mismatch, replay_conflict, folder_acl_denied, acl_evidence_unavailable, idempotency_conflict, idempotency_unavailable, append_conflict, and state_transition_invalid.
  - [ ] Keep accepted event payloads metadata-only: tenant ID, folder ID, safe display name, optional safe description/tags, lifecycle state, idempotency/correlation/task IDs, actor/principal safe identifier, version/sequence when available, and reason/result code.
  - [ ] Do not copy raw command payloads, raw request headers, authentication tokens, provider payloads, repository names, branch names, file paths, file contents, diffs, generated context payloads, arbitrary tenant configuration values, or unauthorized resource identifiers into events, results, logs, traces, metrics, audit, projections, or test failure output.
  - [ ] Ensure result evidence is structured around stable result codes and safe metadata; tests must not infer behavior from exception text, localized diagnostics, or event type names.
- [ ] Add tenant and ACL pre-load authorization gates. (AC: 2, 3, 6, 7)
  - [ ] Consume the Story 2.1 tenant-access authorizer/projection boundary before building the folder stream name.
  - [ ] Consume Story 2.2 organization ACL baseline evidence for `create_folder`; if the full Story 2.2 implementation is still in flight, define a narrow interface boundary and tests that model allowed, denied, unavailable, malformed, and stale ACL evidence without duplicating ACL aggregate logic.
  - [ ] Treat tenant IDs from route/body/query/header as comparison values only, never as authority that selects the aggregate stream.
  - [ ] Ensure every non-allowed tenant or ACL result rejects before stream-name construction, durable/cache key construction, stream load, duplicate lookup, append, mutation, projection update, diagnostics lookup, audit lookup, provider readiness checks, or repository operations.
  - [ ] Keep authorization evidence metadata-only and stable-code based. Do not expose membership inventories, role display names, group names, user emails, raw projection payloads, or whether unauthorized folders already exist.
- [ ] Add idempotency and duplicate handling. (AC: 8, 9, 10)
  - [ ] Reuse the project idempotency equivalence rules established in Epic 1 and the client helper semantics already generated in `src/Hexalith.Folders.Client/Generated/`.
  - [ ] Canonicalize idempotency inputs using command type, operation intent, tenant, folder ID, safe folder metadata, actor/principal safe ID, and any explicit parent/organization reference if present.
  - [ ] Treat raw JSON order, casing noise around non-token display metadata, omitted optional metadata, Unicode normalization, culture-specific casing, and exact duplicate tags according to a documented culture-invariant canonicalization rule before comparing payload equivalence.
  - [ ] Return the same logical result for equivalent replay with the same idempotency key without appending duplicate `FolderCreated` events.
  - [ ] Reject same-key materially different payloads as `idempotency_conflict`.
  - [ ] Reject or fail closed with stable `idempotency_unavailable` evidence when an introduced idempotency boundary cannot prove equivalence after tenant and ACL gates pass; do not fall through to append.
  - [ ] Return deterministic `already_exists` or equivalent evidence when a folder stream already contains a creation event for the same folder ID and the request is not an equivalent replay.
  - [ ] Treat expected-version or append conflicts for the same tenant/folder stream as duplicate or idempotency evidence after re-reading safe state, never as a second creation event or as leaked infrastructure exception text.
- [ ] Add folder-list projection and replay support only as needed for creation evidence. (AC: 2, 10, 11)
  - [ ] Add minimal projection/event-apply coverage that can derive tenant-scoped folder existence, lifecycle state, and safe metadata from creation events.
  - [ ] Derive projection tenant scope from the stream/envelope tenant evidence, not from mutable event payload tenant fields if both are present.
  - [ ] Prove replay isolation for two tenants with matching folder names, path labels, and folder-ID-like input values; tenant scope must come from the stream/envelope metadata and cannot collide across tenants.
  - [ ] Cover empty streams, duplicate creation events, malformed metadata-only events, and replay ordering deterministically without external services.
  - [ ] Keep projection output metadata-only and tenant-scoped; do not add public listing/query endpoints unless the Contract Spine already exposes the shape and the implementation can stay within this story's logical-folder scope.
  - [ ] Do not implement hierarchy moves, folder archive, repository binding, provider readiness, workspace state, file context, or effective-permission inspection in this projection.
- [ ] Add tests and fixtures. (AC: 1-12)
  - [ ] Add unit tests under `tests/Hexalith.Folders.Tests/Aggregates/Folder/` or an equivalent local path for aggregate creation, event application, duplicate create, invalid folder ID, invalid metadata, reserved `system` tenant, and stream-name shape.
  - [ ] Add authorization gate tests for allowed tenant plus `create_folder`, stale/unavailable/disabled/unknown/malformed/future/replay-conflicting tenant evidence, tenant mismatch, missing authoritative tenant, denied ACL, unavailable ACL evidence, and malformed ACL evidence.
  - [ ] Add idempotency tests for equivalent replay, same key plus changed folder metadata, same key plus changed folder ID, and already-existing folder without equivalent replay.
  - [ ] Add idempotency and duplicate tests for tenant-prefixed durable keys, idempotency evidence unavailable after authorization, concurrent same-folder create races, and the same opaque folder ID safely existing in two different authoritative tenants.
  - [ ] Add metadata leakage tests with sentinel values for credential material, provider tokens, repository names, branch names, file paths, file contents, diffs, generated context payloads, arbitrary tenant configuration values, user emails, group display names, and unauthorized resource names.
  - [ ] Add sequencing tests named like `RejectsBeforeStreamNameWhenTenantMissing`, `RejectsBeforeLoadWhenTenantNotEvidenced`, and `RejectsBeforeAppendWhenCreateFolderAclMissing`, or equivalent local names that encode the forbidden side-effect boundary.
  - [ ] Use in-memory spies for EventStore access, tenant evidence, ACL evidence, idempotency store, clock/time, validators, diagnostics sink, and audit sink so rejected paths can prove zero stream naming/loading/appending/projection/diagnostic/audit/provider/repository side effects.
  - [ ] Extend `src/Hexalith.Folders.Testing/Factories/*` only with reusable folder creation builders that delegate to production validation rules.
  - [ ] Add conformance tests in `tests/Hexalith.Folders.Testing.Tests` if new testing helpers are introduced.
  - [ ] Use pure in-memory fakes only for EventStore seams, tenant evidence, ACL evidence, clock/time, validators, idempotency records, and diagnostics sinks. These tests must not use Dapr, EventStore server, databases, network calls, generated SDK/OpenAPI, CLI/MCP/UI/workers, provider adapters, or nested submodule initialization.

## Dev Notes

### Source Context

- Epic 2 objective: tenant administrators and authorized actors can create folders, manage access, inspect effective permissions, archive folders, and receive safe authorization evidence with cross-tenant isolation enforced before any resource access. [Source: `_bmad-output/planning-artifacts/epics.md#Epic 2: Tenant-Scoped Folder Access And Lifecycle`]
- Story 2.3 requires `CreateFolder` to create a folder aggregate with an opaque identifier and active lifecycle state; tenant scope comes from auth context, not request payload authority. [Source: `_bmad-output/planning-artifacts/epics.md#Story 2.3`]
- PRD FR11 requires authorized actors to create logical folders within a tenant, while FR9 and NFR2 require denial before exposing folder, repository, credential, lock, file, audit, provider, or context information across tenant boundaries. [Source: `_bmad-output/planning-artifacts/prd.md#Functional Requirements`]
- Architecture maps FR11-FR14 folder lifecycle work to `src/Hexalith.Folders/Aggregates/Folder/` and `src/Hexalith.Folders.Contracts/Folders/{Commands,Events,Queries}/`. This story should implement the domain creation surface first and avoid contract drift unless the existing Contract Spine already requires a change. [Source: `_bmad-output/planning-artifacts/architecture.md#Requirements to Structure Mapping`]
- Architecture and project context require aggregate IDs to be opaque immutable identifiers. Folder hierarchy, names, and paths are projected metadata, never aggregate identity. [Source: `_bmad-output/project-context.md#Critical Implementation Rules`]

### Previous Story Intelligence

- Story 2.1 defines the Folders host, Tenants event subscription, local fail-closed tenant-access projection, and tenant-access authorizer outcomes that must gate this story before folder stream selection.
- Story 2.1 advanced elicitation hardened replay conflicts, projection-store unavailable semantics, configuration-removal tombstones, diagnostic leakage boundaries, and structural endpoint tests. Reuse those stable outcome codes and evidence fields instead of inventing a second tenant authority model.
- Story 2.2 defines the organization ACL baseline and the closed `create_folder` action vocabulary. This story consumes that evidence; it must not reimplement organization ACL grant/revoke semantics.
- Story 2.2 advanced elicitation hardened culture-invariant canonicalization, strict lower-snake-case action parsing, intra-command duplicate/conflict handling, structured result evidence, and side-effect negative controls. Apply the same discipline to create-folder idempotency and rejection paths.

### Existing Implementation State

- `src/Hexalith.Folders/FoldersModule.cs` currently exposes scaffold metadata only; no folder aggregate, creation command, or folder lifecycle state exists yet.
- `src/Hexalith.Folders.Testing/Factories/TestFolderContext.cs` already validates stream-name segments and exposes `FolderStreamName` as `{ManagedTenantId}:folders:{FolderId}`. Reuse or extend this pattern instead of creating a second stream naming convention.
- `src/Hexalith.Folders.Testing/Factories/TestAuthorizationContext.cs` provides simple managed tenant and permission test context data. Extend it only if folder creation tests need reusable builders.
- `tests/Hexalith.Folders.Tests/FoldersModuleSmokeTests.cs` is scaffold-level; this story should add real aggregate and authorization-gate tests without requiring live Dapr, EventStore, Tenants, provider credentials, or initialized nested submodules.
- `src/Hexalith.Folders.Client/Generated/*` contains generated client/idempotency helpers. Do not hand-edit generated files.

### Required Architecture Patterns

- Keep `Hexalith.Folders.Contracts` behavior-free. Folder aggregate logic, command validation, idempotency equivalence, tenant/ACL gates, and EventStore command handling belong in `Hexalith.Folders` and tests, not in Contracts.
- Keep aggregates pure. The aggregate applies commands, validates invariants that do not require I/O, and produces events/results; Dapr, provider, filesystem, Git, UI, CLI, MCP, workers, diagnostics, and projection-store side effects stay outside aggregate state transitions.
- Authorization order for mutation paths is authoritative tenant context, Story 2.1 tenant evidence, Story 2.2 folder-create ACL evidence, then stream load/mutation. Do not construct stream names or load streams before fail-closed gates have passed.
- Idempotency and duplicate lookups happen only after tenant and ACL gates pass. Unauthorized, stale, unavailable, malformed, or denied evidence must not reveal whether a folder stream, idempotency record, or duplicate folder already exists.
- Metadata validation happens before durable folder-create key construction, stream loading, duplicate lookup, append, projection update, diagnostics, audit lookup, and aggregate mutation. Invalid metadata must not be allowed to create durable operation records.
- Expected-version, append-conflict, or same-stream race handling must resolve to stable duplicate/idempotency evidence after authorization and validation. It must not append a second creation event or expose EventStore exception details.
- Use C# file-scoped namespaces, nullable-aware records/classes, one public type per file, PascalCase types/members, camelCase locals, and async APIs where I/O boundaries are introduced.
- Use stable result codes, enum values, and metadata fields in tests. Do not assert localized or user-facing diagnostic text.
- Events, logs, traces, metrics, projections, audit records, and errors must remain metadata-only and must not include raw credential values, provider tokens, file contents, diffs, generated context payloads, repository names, branch names, raw paths, user emails, group display names, or unauthorized resource existence.
- Cache keys and durable operational keys must carry tenant scope. A missing tenant prefix is a correctness and security bug. If folder creation introduces an idempotency or duplicate-detection key, test the tenant prefix.
- Do not add repair workflows, local-only folder mode, webhooks, brownfield adoption, multi-organization-per-tenant, or operations-console mutation paths during this story.

### Files To Touch

- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs`
- `src/Hexalith.Folders/Aggregates/Folder/*Command*.cs`
- `src/Hexalith.Folders/Aggregates/Folder/*Event*.cs`
- `src/Hexalith.Folders/Aggregates/Folder/*Result*.cs`
- `src/Hexalith.Folders/Authorization/*` only for narrow integration seams needed to consume Story 2.1 tenant-access and Story 2.2 ACL outcomes.
- `src/Hexalith.Folders/Idempotency/*` only if no existing reusable equivalence helper exists for domain command payloads.
- `src/Hexalith.Folders/Projections/FolderList/*` only for minimal tenant-scoped creation replay evidence.
- `src/Hexalith.Folders.Testing/Factories/*` only for reusable folder creation test data builders.
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/*Tests.cs`
- `tests/Hexalith.Folders.Testing.Tests/*` only when new testing helpers are added.

### Do Not Touch

- Contracts and SDK: Do not edit generated SDK files in `src/Hexalith.Folders.Client/Generated/`; do not add or modify OpenAPI Contract Spine operations unless implementation discovers a blocking mismatch that is explicitly handled through the contract workflow.
- Storage/provider: Do not implement provider readiness, provider adapters, repository creation, repository binding, branch policy, local-only folder mode, webhooks, or brownfield adoption.
- Workspace/file behavior: Do not implement workspace preparation, locks, file mutation, commits, context query, file browsing, or filesystem paths.
- Permission/lifecycle management: Do not implement folder ACL grant/revoke, effective-permission query endpoints, archive behavior, repair workflows, or multi-organization-per-tenant behavior.
- UX/surface/worker behavior: Do not implement CLI, MCP, UI, workers, operations-console mutation paths, or production Dapr policy.
- Do not make display name, path, repository name, provider name, or tenant name part of folder identity.
- Do not initialize nested submodules or use recursive submodule commands.

### Testing

- Unit tests must run without provider credentials, tenant seed data, production secrets, running Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, or nested submodule initialization.
- Use xUnit v3 and Shouldly for aggregate tests; use NSubstitute only where an actual seam needs substitution.
- Add focused tests named `FolderCreationCommandValidationTests`, `FolderCreationTenantEvidenceGateTests`, `FolderCreationAclGateTests`, `FolderCreationIdempotencyTests`, `FolderCreationMetadataLeakageTests`, `FolderCreationProjectionReplayTests`, and `FolderStreamShapeTests`, or equivalent locally consistent names.
- Include side-effect negative controls proving rejected tenant evidence, rejected ACL evidence, invalid metadata, duplicate conflict, and idempotency conflict do not construct stream names, load streams, append events, mutate aggregate state, query diagnostics, query audit resources, call provider readiness, or create repositories.
- Include side-effect negative controls proving idempotency store unavailability and same-stream append races do not bypass authorization, validation, tenant-scoped keys, or metadata-only result evidence.
- Include positive and negative metadata tests: allowed diagnostics may include tenant ID, folder ID, result code, lifecycle state, correlation/task/idempotency IDs, and safe actor/principal ID; forbidden diagnostics must not include raw auth tokens, provider payloads, command bodies, repository/branch/path values, diffs, file contents, generated context, stack traces with sensitive values, user emails, group names, arbitrary tenant configuration, or unauthorized resource identifiers.
- Include replay tests proving `FolderCreated` deterministically produces the same active logical lifecycle state and safe metadata projection from event history.

### Regression Traps

- Do not grant access from a payload tenant ID, route tenant ID, query tenant ID, or client-controlled header.
- Do not silently allow folder creation when Tenants projection freshness or ACL evidence cannot be proven.
- Do not derive folder IDs from display names, folder paths, repository names, provider names, or tenant names.
- Do not implement repository-backed provisioning in this story. Provider readiness and repository creation start in Epic 3.
- Do not create a folder content tree, file browser, context-query index, workspace directory, or local filesystem path as part of logical folder creation.
- Do not log or project user-friendly names, emails, provider identifiers, repository URLs, branches, raw paths, file contents, diffs, generated context payloads, secrets, or unauthorized resource existence.
- Do not create duplicate folder state through idempotency replay or through an already-existing stream.
- Do not construct idempotency, duplicate-detection, cache, or operation keys before authoritative tenant and ACL gates pass, and never derive those keys from request-supplied tenant authority.
- Do not add public list/get endpoints unless the existing Contract Spine already defines them and the implementation stays metadata-only and tenant-scoped.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 2.3`
- `_bmad-output/planning-artifacts/prd.md#FR11`
- `_bmad-output/planning-artifacts/prd.md#Authorization and Tenant Boundary`
- `_bmad-output/planning-artifacts/architecture.md#Domain Layout`
- `_bmad-output/planning-artifacts/architecture.md#Requirements to Structure Mapping`
- `_bmad-output/project-context.md#Critical Implementation Rules`
- `_bmad-output/implementation-artifacts/2-1-stand-up-domain-service-host-with-tenants-integration.md`
- `_bmad-output/implementation-artifacts/2-2-implement-organization-aggregate-acl-baseline.md`
- `src/Hexalith.Folders.Testing/Factories/TestFolderContext.cs`
- `src/Hexalith.Folders.Testing/Factories/TestAuthorizationContext.cs`

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-17 | Created story with folder aggregate creation, tenant/ACL pre-load gates, idempotency, metadata-only event, projection replay, and offline test guardrails. | Codex |
| 2026-05-17 | Applied party-mode review hardening for terms, gate ordering, idempotency disclosure, duplicate scope, projection replay isolation, and test spies. | Codex |
| 2026-05-17 | Applied advanced-elicitation hardening for durable key scoping, metadata validation ordering, idempotency unavailability, concurrent duplicates, and projection authority. | Codex |

## Party-Mode Review

- ISO date and time: 2026-05-17T10:56:49Z
- Selected story key: `2-3-create-folders-within-a-tenant`
- Command/skill invocation used: `/bmad-party-mode 2-3-create-folders-within-a-tenant; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), Paige (Technical Writer)
- Findings summary: Reviewers agreed the story was in the correct product scope but needed clarification before dev on authoritative tenant source, strict gate order before any stream/idempotency/diagnostic/audit side effects, ACL evidence dependency, idempotency disclosure boundaries, duplicate-folder meaning, safe metadata terms, projection replay isolation, and offline negative-control tests.
- Changes applied: Added a Terms section; tightened AC2, AC6, AC7, AC8, AC9, AC10, and AC11; added sequencing, projection replay, and spy-fixture subtasks; clarified idempotency lookup ordering; grouped exclusions by category; and recorded the trace separately from future advanced elicitation evidence.
- Findings deferred: Folder display-name uniqueness within a tenant; whether projected path label is cosmetic metadata or a future hierarchy locator; final replay projection shape beyond creation evidence; whether denied attempts emit audit records and the exact redacted field set; whether idempotency helper should stay folder-local or become reusable after another aggregate needs it.
- Final recommendation: `ready-for-dev`

## Advanced Elicitation

- ISO date and time: 2026-05-17T11:01:46Z
- Selected story key: `2-3-create-folders-within-a-tenant`
- Command/skill invocation used: `/bmad-advanced-elicitation 2-3-create-folders-within-a-tenant`
- Batch 1 method names: Red Team vs Blue Team; Security Audit Personas; Failure Mode Analysis; Self-Consistency Validation; Critique and Refine.
- Reshuffled Batch 2 method names: Pre-mortem Analysis; First Principles Analysis; Graph of Thoughts; Occam's Razor Application; Active Recall Testing.
- Findings summary:
  - The story had the right security shape, but advanced elicitation found remaining implementation traps around durable key construction happening before authorization, invalid metadata creating operational records, idempotency-store outages falling through to append, concurrent same-folder creates leaking EventStore failures, and projections trusting mutable event payload tenant fields.
  - The acceptance criteria and tasks needed explicit tenant-prefixed durable keys, culture-invariant idempotency canonicalization, same-stream race handling, idempotency-unavailable evidence, cross-tenant same-folder-ID isolation tests, and projection authority from stream/envelope metadata.
- Changes applied: Added durable folder-create key and concurrent duplicate terms; tightened AC5, AC8, AC9, AC10, and AC11; added tasks for tenant-scoped durable keys, metadata canonicalization, idempotency unavailability, append-conflict handling, and projection tenant authority; extended testing and regression guidance for side-effect negative controls and same-stream races.
- Findings deferred: Exact idempotency persistence implementation; whether append-conflict evidence is a distinct public result code or maps to existing duplicate evidence; final reusable idempotency helper shape across aggregates; any public projection/listing contract beyond minimal creation replay evidence.
- Final recommendation: ready-for-dev after applied advanced-elicitation hardening.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

### Completion Notes List

- Story created by `/bmad-create-story 2-3-create-folders-within-a-tenant` equivalent workflow on 2026-05-17.
- Project context, Epic 2, PRD, architecture, Story 2.1, Story 2.2, testing factories, recent commits, and story-creation lessons were reviewed.

### File List
