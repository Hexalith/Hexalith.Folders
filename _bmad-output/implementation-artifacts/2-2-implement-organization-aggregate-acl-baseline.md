# Story 2.2: Implement Organization aggregate ACL baseline

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a tenant administrator,
I want organization-level folder access controls represented in the domain,
so that folder permissions can be granted consistently to users, groups, roles, and delegated service agents.

## Acceptance Criteria

1. Given the `OrganizationAggregate` does not yet exist, when this story is implemented, then the aggregate, state, command, event, and rejection types are introduced under `src/Hexalith.Folders/Aggregates/Organization/` using EventStore-oriented naming and opaque organization identity.
2. Given an authoritative managed tenant context and authorized tenant administrator principal are present, when ACL baseline commands are accepted, then allowed users, groups, roles, and delegated service agents are persisted as metadata-only organization events.
3. Given tenant identity appears in command payloads, routes, query parameters, or client-controlled headers, when the aggregate command context is built, then tenant authority comes only from authentication context or EventStore envelopes; payload tenant IDs are validation inputs only.
4. Given an ACL baseline command is missing authoritative tenant context, references the reserved `system` tenant for a managed organization, contains malformed principal identifiers, duplicates entries with conflicting semantics, or attempts to grant unsupported permissions, when it is processed, then it is rejected with stable metadata-only rejection evidence and no state mutation.
5. Given ACL metadata is stored or emitted, when events, rejections, logs, traces, metrics, audit records, or test failure messages are produced, then they include only tenant ID, organization ID, principal IDs, principal kind, permission/action names, correlation/task/idempotency IDs, version, and reason codes; credential material, provider tokens, file contents, repository names, branch names, unauthorized resource existence, and arbitrary tenant configuration values are never included.
6. Given the same idempotency key and equivalent ACL payload are processed again, when command handling is retried, then the same logical result is returned without duplicating events; given the same idempotency key and a materially different ACL payload are processed, then the command is rejected as an idempotency conflict.
7. Given Story 2.1 supplies fail-closed tenant-access projection evidence, when organization ACL commands are authorized, then stale, unavailable, disabled, unknown, malformed, replay-conflicting, future-dated, or tenant-mismatched projection evidence rejects before aggregate stream loading or resource-specific ACL changes are attempted.
8. Given organization ACL baseline events exist, when projections or test fixtures consume them, then effective permissions can be derived deterministically by tenant, organization, principal kind, principal ID, and action without relying on localized diagnostic text.
9. Given tests run without provider credentials, tenant seed data, production secrets, running Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, or nested submodules, when unit and smoke tests execute, then ACL command validation, event application, rejection paths, idempotency behavior, and metadata-only leakage boundaries are covered with in-memory fakes.
10. Given the organization ACL baseline records grant state only, when this story is implemented, then it does not implement folder inheritance, folder ACL overrides, public effective-permission query endpoints, runtime authorization enforcement beyond the pre-load tenant evidence gate, provider-specific subject resolution, or production Dapr policy mapping.
11. Given ACL entries are compared, when duplicates, replays, grants, and revokes are evaluated, then the entry identity is the tuple of tenant, organization, principal kind, principal ID, and action; principal kinds remain namespace-separated so the same raw ID under different kinds never collides.
12. Given Story 2.1 tenant-access evidence is consumed, when evidence is not `allowed`, then the command rejects before stream-name construction, stream loading, event append, aggregate mutation, or diagnostic/audit resource lookup, and tests prove those side effects did not occur.
13. Given one ACL command contains repeated entries, when entries are canonicalized, then exact duplicate tuples collapse to one deterministic operation while same-tuple conflicting operations in the same command reject before stream access or event append.
14. Given idempotency equivalence is evaluated, when command type, tenant, organization, principal kind, principal ID, action, and operation intent differ after canonicalization, then the payloads are materially different even if raw JSON order, casing noise around identifiers, or duplicate exact entries differ.
15. Given organization ACL result evidence is produced for accepted, no-op, rejected, or conflict outcomes, when callers or tests inspect it, then the result exposes stable codes and safe identifiers only and never requires parsing event names, localized messages, stack traces, or diagnostic text.

## Tasks / Subtasks

- [ ] Create the Organization aggregate domain surface. (AC: 1, 2, 4, 8)
  - [ ] Add `src/Hexalith.Folders/Aggregates/Organization/OrganizationAggregate.cs`.
  - [ ] Add `src/Hexalith.Folders/Aggregates/Organization/OrganizationState.cs`.
  - [ ] Add opaque value objects or validated identifiers for managed tenant ID, organization ID, principal ID, and ACL action where existing project types do not already provide them.
  - [ ] Keep aggregate stream names in the `{managedTenantId}:organizations:{organizationId}` shape and reject empty segments, `:` characters, control characters, non-canonical casing when the project validator requires canonical casing, and the reserved `system` tenant for managed organization streams.
- [ ] Define ACL baseline commands and metadata-only events. (AC: 2, 3, 5, 6)
  - [ ] Add commands for initializing the organization ACL baseline and granting/revoking baseline permissions for user, group, role, and delegated service-agent principals.
  - [ ] Add accepted events such as `OrganizationAclBaselineInitialized`, `OrganizationAclPrincipalGranted`, and `OrganizationAclPrincipalRevoked`, or equivalent names aligned with local EventStore conventions.
  - [ ] Add rejection events or result types with stable codes for unauthorized tenant context, invalid principal, unsupported action, duplicate/conflicting entry, stale tenant projection, unavailable tenant projection, disabled tenant, unknown tenant, tenant mismatch, and idempotency conflict.
  - [ ] Ensure event payloads are metadata-only and do not copy request payloads wholesale.
  - [ ] Define the initial closed ACL action vocabulary as domain-owned value objects or enum values in `Hexalith.Folders`: `create_folder`, `configure_provider_binding`, `prepare_workspace`, `lock_workspace`, `read_metadata`, `read_file_content`, `mutate_files`, `commit`, `query_status`, `query_audit`, and `view_operations_console`. Do not add provider-specific or folder-override action names in this story.
  - [ ] Define command/result inventory for `InitializeOrganizationAclBaseline`, `GrantOrganizationAclPrincipal`, and `RevokeOrganizationAclPrincipal` or equivalent local names, with result codes for accepted, already_applied, duplicate_entry, missing_entry, unsupported_action, invalid_principal, invalid_organization, invalid_tenant, reserved_tenant, tenant_access_denied, stale_projection, unavailable_projection, unknown_tenant, disabled_tenant, malformed_evidence, tenant_mismatch, missing_authoritative_tenant, replay_conflict, and idempotency_conflict.
  - [ ] Treat ACL action values as ordinal, lower-snake-case domain tokens; reject localized labels, display names, aliases, mixed-case variants, provider-specific verbs, and unknown action strings rather than normalizing them into accepted permissions.
  - [ ] Ensure stable result evidence is structured around result codes and metadata fields; tests must not infer behavior from event type names, exception text, stack traces, or localized diagnostics.
- [ ] Add fail-closed authorization integration points without pulling in later folder policy. (AC: 3, 4, 7)
  - [ ] Depend on the tenant-access authorizer/projection from Story 2.1 when available; if Story 2.1 implementation is still in flight, add an interface boundary and tests that model the fail-closed outcomes without duplicating Story 2.1 projection logic.
  - [ ] Check tenant projection evidence before organization stream loading or ACL mutation.
  - [ ] Treat tenant IDs from route/body/query/header as comparison values only, never as the authority that selects the aggregate stream.
  - [ ] Require the authorization seam to receive authoritative managed tenant context from authentication context or EventStore envelopes plus Story 2.1 evidence fields: outcome code, projection watermark, last event timestamp, projection age/freshness status, sequence/version when available, and correlation/task/idempotency metadata. Future-dated evidence must reject through the Story 2.1 stable evidence code instead of creating a permissive fallback.
  - [ ] Do not implement folder-level ACL overrides, folder lifecycle, provider readiness, repository binding, workspace, CLI, MCP, UI, or worker behavior in this story.
- [ ] Add idempotency and deterministic derivation support. (AC: 6, 8)
  - [ ] Reuse the project idempotency equivalence rules established in Epic 1; compare canonical ACL payload shape rather than raw JSON order.
  - [ ] Ensure duplicate equivalent commands do not append duplicate ACL events.
  - [ ] Provide deterministic state application for grant/revoke ordering and duplicate entries.
  - [ ] Add effective-permission derivation helpers only for organization baseline state; Story 2.5 owns public effective-permission inspection.
  - [ ] Treat the same idempotency key plus the same canonical payload as the same logical result with no duplicate event; the same idempotency key plus a different canonical payload rejects as `idempotency_conflict`; a different idempotency key plus an already-present grant returns deterministic `already_applied` evidence or equivalent no-op result without appending a duplicate event.
  - [ ] Derive organization-baseline permissions by set membership over tenant, organization, principal kind, principal ID, and action. Replaying events in any order that preserves causal version order must produce the same state; role/group inheritance and folder override precedence remain out of scope.
  - [ ] Include command type and operation intent in the canonical idempotency payload so a grant and revoke for the same ACL tuple can never share an equivalence class.
  - [ ] Canonicalize repeated entries before event creation: exact duplicates reduce to one entry; conflicting same-tuple grant/revoke or action interpretation rejects as `duplicate_entry`, `replay_conflict`, or a locally equivalent stable conflict code before append.
- [ ] Add tests and fixtures. (AC: 1-12)
  - [ ] Add unit tests under `tests/Hexalith.Folders.Tests` for aggregate initialization, grant/revoke by principal kind, duplicate grants, revoke of missing grant, unsupported action, malformed IDs, and reserved tenant rejection.
  - [ ] Add authorization tests for stale/unavailable/disabled/unknown/malformed/replay-conflicting/future tenant projection evidence and tenant mismatch.
  - [ ] Add idempotency tests for equivalent replay and conflicting replay.
  - [ ] Add leakage tests that scan event/rejection/debug strings for credential, token, file content, repository, branch, and unauthorized-resource sentinel values.
  - [ ] Extend `src/Hexalith.Folders.Testing` factories only with reusable organization/ACL builders that delegate to production validation rules.
  - [ ] Add conformance tests in `tests/Hexalith.Folders.Testing.Tests` if new testing helpers are introduced.
  - [ ] Add focused tests named `OrganizationAclCommandValidationTests`, `OrganizationAclTenantEvidenceGateTests`, `OrganizationAclIdempotencyTests`, `OrganizationAclEffectivePermissionTests`, `OrganizationAclMetadataLeakageTests`, and `OrganizationAclStreamShapeTests` or equivalent locally consistent names.
  - [ ] Add negative controls proving rejected tenant evidence does not construct stream names, read streams, append events, mutate aggregate state, or query diagnostic/audit resources.
  - [ ] Add intra-command duplicate/conflict tests covering exact duplicate grants, exact duplicate revokes, same idempotency key with reordered entries, same key with grant-vs-revoke intent changes, and mixed principal-kind collision attempts.
  - [ ] Add structured-result tests proving accepted, already-applied, missing-entry, unsupported-action, tenant-evidence, replay-conflict, and idempotency-conflict outcomes can be asserted from stable codes and safe metadata without parsing diagnostic text.
  - [ ] Use pure in-memory fakes only for EventStore seams, tenant evidence, clock/time, validators, and diagnostics sinks. These tests must not use Dapr, EventStore server, databases, network calls, generated SDK/OpenAPI, CLI/MCP/UI/workers, provider adapters, or nested submodule initialization.

## Dev Notes

### Source Context

- Epic 2 objective: tenant administrators and authorized actors can create folders, manage access, inspect effective permissions, archive folders, and receive safe authorization evidence with cross-tenant isolation enforced before any resource access. [Source: `_bmad-output/planning-artifacts/epics.md#Epic 2: Tenant-Scoped Folder Access And Lifecycle`]
- Story 2.2 foundation: organization-level folder access controls must be represented in the domain so permissions can be granted consistently to users, groups, roles, and delegated service agents. [Source: `_bmad-output/planning-artifacts/epics.md#Story 2.2`]
- PRD authorization requirements require tenant administrators to configure minimum tenant and folder access controls, authorized actors to inspect effective permissions, and the system to deny unauthorized or cross-tenant operations before exposing folder, repository, credential, lock, file, audit, provider, or context information. [Source: `_bmad-output/planning-artifacts/epics.md#Authorization and Tenant Boundary`]
- Architecture defines two aggregate roots: `OrganizationAggregate` for provider bindings, repository policy, credential references, and ACL baseline; and `FolderAggregate` for lifecycle, storage mode, repository binding, workspace readiness, ACL overrides, and file-operation metadata. [Source: `_bmad-output/planning-artifacts/architecture.md#Domain Layout`]
- Aggregate identity must use `{managedTenantId}:organizations:{organizationId}` for organization streams and must never use the reserved `system` tenant for managed tenant folders or organizations. [Source: `_bmad-output/project-context.md#Critical Don't-Miss Rules`]

### Previous Story Intelligence

- Story 2.1 defines the Folders host, Tenants event subscription, local fail-closed tenant-access projection, and tenant-access authorizer outcomes that this story must consume before organization ACL mutation.
- Story 2.1 explicitly defers Organization ACL commands to this story. Do not move folder ACL semantics or folder lifecycle behavior into Story 2.1 code while implementing this one.
- Story 2.1 party-mode review hardened the tenant authority and freshness semantics: route/body/query/header tenant IDs are validation-only; stale, missing, malformed, unavailable, replay-conflicting, future-dated, disabled, unknown, or mismatched tenant evidence fails closed for mutations.

### Existing Implementation State

- `src/Hexalith.Folders/FoldersModule.cs` currently exposes scaffold metadata only; no organization aggregate or domain command surface exists yet.
- `src/Hexalith.Folders.Testing/Factories/TestFolderContext.cs` already validates stream-name segments and exposes `OrganizationStreamName` as `{ManagedTenantId}:organizations:{OrganizationId}`. Reuse or extend this pattern instead of creating a second stream naming convention.
- `src/Hexalith.Folders.Testing/Factories/TestAuthorizationContext.cs` provides simple managed tenant and permission test context data. Extend it only if the ACL tests need reusable builders.
- `tests/Hexalith.Folders.Tests/FoldersModuleSmokeTests.cs` is still scaffold-level; this story should add real aggregate tests without requiring live Dapr, EventStore, Tenants, provider credentials, or initialized nested submodules.
- Active development work may already be modifying Story 1.12 and Story 1.13 files and contract/client tests. Treat those changes as unrelated unless the implementation branch explicitly rebases on them.

### Party-Mode Clarifications

- `ACL baseline` means organization-level grant/revoke membership state only. It does not implement folder inheritance, folder-specific overrides, public effective-permission endpoints, runtime authorization enforcement beyond the pre-load tenant evidence gate, provider-specific identity resolution, Keycloak group mapping, production Dapr policy, or cross-surface UI/CLI/MCP behavior.
- Supported action names for this story are a closed domain vocabulary derived from the PRD folder ACL verbs: `create_folder`, `configure_provider_binding`, `prepare_workspace`, `lock_workspace`, `read_metadata`, `read_file_content`, `mutate_files`, `commit`, `query_status`, `query_audit`, and `view_operations_console`. Validation and semantics belong in `Hexalith.Folders`; Contracts may expose serializable names only if a later contract workflow explicitly requires it.
- Principal kinds are `user`, `group`, `role`, and `delegated_service_agent`. Principal IDs are opaque, metadata-only identifiers; reject null, empty, whitespace-only, `:` characters, control characters, and any identifier that an existing project validator rejects. The principal kind is part of the identity key, so `user:abc` and `group:abc` are different entries even if the raw ID segment matches.
- ACL entry identity is tenant, organization, principal kind, principal ID, and action. Re-granting the same entry is a deterministic no-op or `already_applied` result; revoking a missing entry is a deterministic `missing_entry` or equivalent no-op rejection; changing the action set requires explicit grant/revoke entries rather than rewriting prior event payloads.
- The reserved managed tenant identifier is `system`; reject it before stream access, including casing or whitespace variants after invariant normalization. Also reject empty segments, `:` characters, and control characters in stream-name segments.
- Organization ACL baseline may be initialized by the first accepted ACL baseline command if no prior organization-created event exists. This does not create multi-organization-per-tenant semantics or provider/repository binding behavior.
- The pure aggregate must not call authorizers, projections, repositories, Dapr, EventStore, clock/random providers, tenant services, provider adapters, or diagnostics sinks. Application/domain-service seams perform tenant evidence and stream-loading guards before invoking pure aggregate state transitions.
- Metadata-only events may carry stable replay identifiers needed to rebuild ACL state: tenant ID, organization ID, principal kind, principal ID, action, idempotency/correlation/task IDs, version/sequence, and reason/result code. They must not carry user names, display names, emails, auth tokens, raw headers, provider payloads, command bodies, repository names, branch names, file paths, diffs, generated context, arbitrary tenant configuration, or unauthorized resource identifiers.
- Tenant evidence must use Story 2.1 outcome names and fields instead of a new authority model. Any non-`allowed` result, including stale, unavailable, disabled, unknown, malformed, replay-conflicting, future-dated, tenant-mismatched, denied, or missing-authoritative-tenant evidence, rejects before stream-name construction, stream loading, append, mutation, diagnostic lookup, or audit resource lookup.
- Do not add cache or durable operational keys in this story. If implementation discovers a necessary durable key, it must be tenant-scoped and covered by tests, but that should be treated as a scope signal rather than the default path.

### Advanced Elicitation Hardening

- Canonicalization must be explicit and culture-invariant. Tenant, organization, principal kind, principal ID, action, command type, and operation intent are the semantic idempotency inputs; raw JSON order, repeated exact entries, and transport formatting are not.
- ACL action parsing is intentionally strict. The closed action vocabulary is accepted only as domain-owned lower-snake-case tokens; aliases, localized labels, display names, provider verbs, mixed-case variants, and unknown strings are rejected rather than coerced.
- Intra-command duplicate handling happens before stream access or event append. Exact duplicate ACL tuples collapse deterministically; same-tuple conflicting grant/revoke intent or conflicting action interpretation rejects with stable conflict evidence.
- Result evidence must be assertable without text parsing. Accepted, already-applied, missing-entry, unsupported-action, tenant evidence, replay-conflict, and idempotency-conflict outcomes expose stable codes plus safe identifiers and correlation/task/idempotency metadata.
- Negative controls should prove every fail-closed path prevents stream-name construction, stream loading, append, aggregate mutation, diagnostic lookup, and audit-resource lookup, including duplicate/conflict and idempotency-conflict paths, not only tenant-evidence failures.

### Required Architecture Patterns

- Keep `Hexalith.Folders.Contracts` behavior-free. Organization aggregate logic, ACL command validation, EventStore command handling, and authorization checks belong in `Hexalith.Folders` and tests, not in Contracts.
- Keep aggregates pure. Aggregates should apply commands, validate invariants, and produce events/results; Dapr, provider, filesystem, Git, UI, CLI, MCP, and worker side effects stay outside aggregate state transitions.
- Use C# file-scoped namespaces, nullable-aware records/classes, one public type per file, PascalCase types/members, camelCase locals, and async APIs where I/O boundaries are introduced.
- Use stable result codes, enum values, and metadata fields in tests. Do not assert localized or user-facing diagnostic text.
- Events, logs, traces, metrics, projections, audit records, and errors must remain metadata-only and must not include raw credential values, provider tokens, file contents, diffs, generated context payloads, repository names, branch names, or unauthorized resource existence.
- Cache keys and durable operational keys must carry tenant scope. A missing tenant prefix is a correctness and security bug.
- Do not add repair workflows, local-only folder mode, webhooks, brownfield adoption, multi-organization-per-tenant, or operations-console mutation paths during this story.

### Files To Touch

- `src/Hexalith.Folders/Aggregates/Organization/OrganizationAggregate.cs`
- `src/Hexalith.Folders/Aggregates/Organization/OrganizationState.cs`
- `src/Hexalith.Folders/Aggregates/Organization/*Command*.cs`
- `src/Hexalith.Folders/Aggregates/Organization/*Event*.cs`
- `src/Hexalith.Folders/Aggregates/Organization/*Result*.cs`
- `src/Hexalith.Folders/Authorization/*` only for integration seams needed to consume Story 2.1 tenant-access outcomes.
- `src/Hexalith.Folders.Testing/Factories/*` only for reusable organization/ACL test data builders.
- `tests/Hexalith.Folders.Tests/*Organization*Tests.cs`
- `tests/Hexalith.Folders.Testing.Tests/*` only when new testing helpers are added.

### Do Not Touch

- Do not edit generated SDK files in `src/Hexalith.Folders.Client/Generated/`.
- Do not add or modify OpenAPI Contract Spine operations unless implementation discovers a blocking mismatch that is explicitly handled through the contract workflow.
- Do not implement folder creation, folder ACL overrides, effective-permission query endpoints, folder lifecycle/archive behavior, provider adapters, repository binding, workspace locking, file mutation, commit, context query, CLI, MCP, UI, workers, or production Dapr policy.
- Do not initialize nested submodules or use recursive submodule commands.

### Testing

- Unit tests must run without provider credentials, tenant seed data, production secrets, running Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, or nested submodule initialization.
- Use xUnit v3 and Shouldly for aggregate tests; use NSubstitute only where an actual seam needs substitution.
- Test aggregate event application directly and through command-handling seams where available.
- Include negative tests for malformed principal IDs, unsupported principal kinds/actions, duplicate/conflicting ACL entries, missing authoritative tenant context, reserved `system` tenant, tenant mismatch, and every fail-closed tenant projection outcome consumed from Story 2.1.
- Include metadata leakage tests with sentinel values for credential material, provider tokens, repository names, branch names, file contents, diffs, generated context payloads, and unauthorized resource names.
- Include subject collision tests proving the same raw ID can exist under different principal kinds without collision.
- Include idempotency tests for exact command replay, same key plus different payload, different key plus same already-applied grant, and stale command sequence behavior.
- Include positive and negative metadata tests: allowed diagnostics may include stable IDs/codes and correlation/task/idempotency metadata, while forbidden diagnostics must not include names, emails, raw auth tokens, provider payloads, command bodies, repository/branch/path values, diffs, generated context, stack traces with sensitive values, or unauthorized resource identifiers.

### Regression Traps

- Do not grant access from a payload tenant ID or a client-controlled header.
- Do not silently allow ACL mutations when Tenants projection freshness cannot be proven.
- Do not implement multi-organization-per-tenant support. Architecture lists it as post-MVP.
- Do not store provider credential references in ACL events unless they are non-secret references explicitly required by a later provider-binding story.
- Do not make folder ACL overrides part of the organization baseline state. Folder-specific ACL behavior belongs to Story 2.4 and effective-permission inspection belongs to Story 2.5.
- Do not create a second permission vocabulary outside the Contract Spine and architecture terminology. If new action names are needed, capture the gap instead of inventing local-only names.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 2.2`
- `_bmad-output/planning-artifacts/architecture.md#Domain Layout`
- `_bmad-output/planning-artifacts/architecture.md#Authentication & Security`
- `_bmad-output/planning-artifacts/architecture.md#Implementation Patterns & Consistency Rules`
- `_bmad-output/project-context.md#Critical Implementation Rules`
- `_bmad-output/implementation-artifacts/2-1-stand-up-domain-service-host-with-tenants-integration.md`
- `src/Hexalith.Folders.Testing/Factories/TestFolderContext.cs`
- `src/Hexalith.Folders.Testing/Factories/TestAuthorizationContext.cs`

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-17 | Applied advanced-elicitation hardening for ACL canonicalization, strict action parsing, duplicate/conflict handling, structured result evidence, and side-effect negative controls. | Codex |
| 2026-05-17 | Applied party-mode review hardening for ACL vocabulary, principal identity, tenant evidence gating, idempotency semantics, metadata-only events, and offline test controls. | Codex |
| 2026-05-16 | Created story with aggregate, ACL, tenant-authority, idempotency, and metadata-only guardrails. | Codex |

## Party-Mode Review

- Date/time: 2026-05-17T07:31:38Z
- Selected story key: `2-2-implement-organization-aggregate-acl-baseline`
- Command/skill invocation used: `/bmad-party-mode 2-2-implement-organization-aggregate-acl-baseline; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), Paige (Technical Writer)
- Findings summary:
  - Reviewers agreed the story was directionally sound but needed executable definitions for tenant evidence, ACL subject identity, supported action vocabulary, duplicate/conflict semantics, idempotency equivalence, metadata-only event content, and offline tests before development.
  - The main risks were accidentally trusting payload/header/route tenant IDs, inventing a parallel permission vocabulary, loading or mutating streams before fail-closed evidence checks, leaking descriptive identity/provider/resource data in diagnostics, and creating tests that pass against incompatible ACL interpretations.
- Changes applied:
  - Added acceptance criteria and task guidance for ACL baseline scope, principal/action identity, pre-stream-load tenant evidence gates, and no-op/conflict idempotency outcomes.
  - Added a closed initial organization ACL action vocabulary derived from PRD folder ACL verbs, plus namespace-separated principal kinds and opaque principal ID validation rules.
  - Added metadata-only event/rejection guidance that permits replay-critical stable identifiers while forbidding names, emails, tokens, raw headers, provider payloads, command bodies, repository/branch/path values, diffs, generated context, arbitrary tenant configuration, and unauthorized resource identifiers.
  - Added offline test controls, named test groups, tenant evidence negative controls, subject collision tests, idempotency scenarios, and positive/negative leakage assertions.
- Findings deferred:
  - Folder inheritance and override precedence, public effective-permission query shape, provider-specific subject resolution, Keycloak group mapping, production Dapr policy mapping, generated SDK/OpenAPI contract naming, and delegated service-agent trust beyond baseline ACL entry validation remain out of scope for later stories or architecture decisions.
  - Exact implementation class names may follow local EventStore conventions as long as the required commands, events, result codes, and metadata fields are covered.
- Final recommendation: ready-for-dev after applied story clarification pass.

## Advanced Elicitation

- Date/time: 2026-05-17T09:35:37Z
- Selected story key: `2-2-implement-organization-aggregate-acl-baseline`
- Command/skill invocation used: `/bmad-advanced-elicitation 2-2-implement-organization-aggregate-acl-baseline`
- Batch 1 method names: Red Team vs Blue Team; Security Audit Personas; Failure Mode Analysis; Self-Consistency Validation; Pre-mortem Analysis
- Reshuffled Batch 2 method names: First Principles Analysis; Graph of Thoughts; Chaos Monkey Scenarios; Comparative Analysis Matrix; Critique and Refine
- Findings summary:
  - The story already constrained the major ACL boundary, but advanced elicitation found remaining implementation traps around culture-sensitive canonicalization, accepting action aliases, collapsing intra-command duplicates too late, letting grant and revoke share idempotency equivalence, and forcing tests to parse diagnostic text instead of stable result evidence.
  - The most important failure mode is any rejected command path that still constructs stream names, loads streams, appends events, mutates aggregate state, or queries diagnostics/audit resources before rejection evidence is produced.
- Changes applied:
  - Added acceptance criteria for intra-command duplicate/conflict handling, canonical idempotency payload inputs, and structured result evidence.
  - Added task guidance for strict lower-snake-case action parsing, command intent in idempotency equivalence, exact duplicate collapse, grant-vs-revoke conflict rejection, and stable-code result assertions.
  - Added advanced hardening notes and tests for duplicate/conflict, idempotency conflict, and side-effect negative controls across non-tenant rejection paths.
- Findings deferred:
  - Optimistic concurrency policy details, generated contract names, public effective-permission query response shape, provider-specific subject resolution, and production Dapr policy mapping remain out of scope for later stories or architecture decisions.
- Final recommendation: ready-for-dev after applied advanced-elicitation hardening.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

### Completion Notes List

- Story created by `/bmad-create-story 2-2-implement-organization-aggregate-acl-baseline` equivalent workflow on 2026-05-16.
- Project context, Epic 2, architecture domain layout, Story 2.1, testing factories, and story-creation lessons were reviewed.

### File List
