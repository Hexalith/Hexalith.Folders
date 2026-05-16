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

## Tasks / Subtasks

- [ ] Create the Organization aggregate domain surface. (AC: 1, 2, 4, 8)
  - [ ] Add `src/Hexalith.Folders/Aggregates/Organization/OrganizationAggregate.cs`.
  - [ ] Add `src/Hexalith.Folders/Aggregates/Organization/OrganizationState.cs`.
  - [ ] Add opaque value objects or validated identifiers for managed tenant ID, organization ID, principal ID, and ACL action where existing project types do not already provide them.
  - [ ] Keep aggregate stream names in the `{managedTenantId}:organizations:{organizationId}` shape and reject empty segments, `:` characters, control characters, and the reserved `system` tenant for managed organization streams.
- [ ] Define ACL baseline commands and metadata-only events. (AC: 2, 3, 5, 6)
  - [ ] Add commands for initializing the organization ACL baseline and granting/revoking baseline permissions for user, group, role, and delegated service-agent principals.
  - [ ] Add accepted events such as `OrganizationAclBaselineInitialized`, `OrganizationAclPrincipalGranted`, and `OrganizationAclPrincipalRevoked`, or equivalent names aligned with local EventStore conventions.
  - [ ] Add rejection events or result types with stable codes for unauthorized tenant context, invalid principal, unsupported action, duplicate/conflicting entry, stale tenant projection, unavailable tenant projection, disabled tenant, unknown tenant, tenant mismatch, and idempotency conflict.
  - [ ] Ensure event payloads are metadata-only and do not copy request payloads wholesale.
- [ ] Add fail-closed authorization integration points without pulling in later folder policy. (AC: 3, 4, 7)
  - [ ] Depend on the tenant-access authorizer/projection from Story 2.1 when available; if Story 2.1 implementation is still in flight, add an interface boundary and tests that model the fail-closed outcomes without duplicating Story 2.1 projection logic.
  - [ ] Check tenant projection evidence before organization stream loading or ACL mutation.
  - [ ] Treat tenant IDs from route/body/query/header as comparison values only, never as the authority that selects the aggregate stream.
  - [ ] Do not implement folder-level ACL overrides, folder lifecycle, provider readiness, repository binding, workspace, CLI, MCP, UI, or worker behavior in this story.
- [ ] Add idempotency and deterministic derivation support. (AC: 6, 8)
  - [ ] Reuse the project idempotency equivalence rules established in Epic 1; compare canonical ACL payload shape rather than raw JSON order.
  - [ ] Ensure duplicate equivalent commands do not append duplicate ACL events.
  - [ ] Provide deterministic state application for grant/revoke ordering and duplicate entries.
  - [ ] Add effective-permission derivation helpers only for organization baseline state; Story 2.5 owns public effective-permission inspection.
- [ ] Add tests and fixtures. (AC: 1-9)
  - [ ] Add unit tests under `tests/Hexalith.Folders.Tests` for aggregate initialization, grant/revoke by principal kind, duplicate grants, revoke of missing grant, unsupported action, malformed IDs, and reserved tenant rejection.
  - [ ] Add authorization tests for stale/unavailable/disabled/unknown/malformed/replay-conflicting/future tenant projection evidence and tenant mismatch.
  - [ ] Add idempotency tests for equivalent replay and conflicting replay.
  - [ ] Add leakage tests that scan event/rejection/debug strings for credential, token, file content, repository, branch, and unauthorized-resource sentinel values.
  - [ ] Extend `src/Hexalith.Folders.Testing` factories only with reusable organization/ACL builders that delegate to production validation rules.
  - [ ] Add conformance tests in `tests/Hexalith.Folders.Testing.Tests` if new testing helpers are introduced.

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
| 2026-05-16 | Created story with aggregate, ACL, tenant-authority, idempotency, and metadata-only guardrails. | Codex |

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

### Completion Notes List

- Story created by `/bmad-create-story 2-2-implement-organization-aggregate-acl-baseline` equivalent workflow on 2026-05-16.
- Project context, Epic 2, architecture domain layout, Story 2.1, testing factories, and story-creation lessons were reviewed.

### File List
