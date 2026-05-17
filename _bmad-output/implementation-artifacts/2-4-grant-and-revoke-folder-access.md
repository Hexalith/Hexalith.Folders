# Story 2.4: Grant and revoke folder access

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a tenant administrator,
I want to grant and revoke folder access for permitted principals,
so that access to folders can evolve without changing repository bindings.

## Terms

- Authoritative tenant context means the managed tenant ID supplied by the authenticated execution context or EventStore envelope. Tenant IDs in request bodies, routes, query strings, or client-controlled headers are validation inputs only.
- Folder ACL administration evidence means the fail-closed authorization evidence proving the actor can mutate ACL entries for the target folder. Prefer the existing Story 2.1 tenant evidence and Story 2.2 organization ACL evidence; if no action token exists for this permission, add a single lower-snake-case domain token such as `manage_folder_access` to the same Hexalith.Folders ACL vocabulary rather than creating a parallel permission model.
- Folder ACL entry identity is the tuple of authoritative tenant ID, folder ID, principal kind, principal ID, action, and scope source. Principal kinds are namespace-separated so the same raw ID under `user`, `group`, `role`, and `delegated_service_agent` never collides.
- Effective ACL metadata means deterministic, metadata-only grant/revoke state that can be replayed from folder ACL events and later consumed by Story 2.5 effective-permission inspection. This story updates the projection state but does not add public inspection endpoints.
- ACL mutation key means any idempotency, duplicate-detection, cache, or operation key introduced for grant/revoke. It must be constructed only after tenant, folder-existence, and ACL-administration gates pass, and must include authoritative tenant ID plus opaque folder ID.
- Revocation freshness evidence means stable metadata proving when a revoke event was accepted, projected, and available to authorization decisions. It must use the configured C7/status-freshness budget rather than a hard-coded time value.

## Acceptance Criteria

1. Given the `FolderAggregate` exists from Story 2.3, when this story is implemented, then folder ACL grant/revoke commands, events, result codes, and projection apply logic are added under the existing folder domain surface without introducing a second folder identity, tenant authority, or permission vocabulary.
2. Given authoritative tenant context, Story 2.1 tenant-access evidence, an existing active folder, and folder ACL administration evidence are all present in that order, when a grant is accepted, then a metadata-only folder ACL grant event is recorded in the `{managedTenantId}:folders:{folderId}` stream and projected into deterministic effective ACL metadata.
3. Given the same ordered gates are present, when a revoke is accepted, then a metadata-only folder ACL revoke event is recorded in the folder stream, the projection removes or tombstones the affected effective ACL metadata, and authorization decisions can observe the revocation within the configured C7/status-freshness budget.
4. Given tenant identity appears in command payloads, routes, query parameters, or client-controlled headers, when grant/revoke command context is built, then tenant authority comes only from authentication context or EventStore envelopes; request-supplied tenant IDs are comparison values only and never select the aggregate stream, idempotency scope, cache key, projection key, diagnostic lookup, or audit lookup.
5. Given the folder does not exist, belongs to another tenant, is inaccessible, has malformed replay evidence, or cannot be loaded after authorization gates pass, when grant/revoke is evaluated, then the command returns stable metadata-only evidence such as `folder_not_found`, `folder_inaccessible`, `folder_replay_conflict`, or `folder_load_unavailable` without exposing whether an unauthorized folder exists.
6. Given tenant evidence is stale, unavailable, disabled, unknown, malformed, future-dated, replay-conflicting, tenant-mismatched, denied, or missing authoritative tenant context, when grant/revoke is evaluated, then it rejects before stream-name construction, ACL mutation key construction, stream loading, append, projection update, diagnostic lookup, audit lookup, provider readiness checks, repository operations, or effective-permission disclosure.
7. Given folder ACL administration evidence is denied, unavailable, malformed, stale, or cannot be evaluated from stable Story 2.2 result evidence, when grant/revoke is evaluated, then it rejects with stable metadata-only evidence before ACL mutation key construction, stream loading, append, projection update, diagnostic lookup, audit lookup, provider readiness checks, repository operations, or effective-permission disclosure.
8. Given grant/revoke payload entries contain principal kind, principal ID, and action, when entries are canonicalized, then only lower-snake-case domain action tokens are accepted; localized labels, aliases, provider-specific verbs, mixed-case variants, unknown actions, empty IDs, `:` characters, control characters, raw emails, display names, and credential-like values are rejected before stream access or event append.
9. Given one command contains repeated ACL entries, when entries are processed, then exact duplicate grant or revoke entries collapse deterministically while same-tuple conflicting grant/revoke intent rejects before stream access or event append with stable conflict evidence.
10. Given an equivalent grant or revoke is retried with the same idempotency key after all gates pass, when the command is processed, then the same logical result is returned without duplicating events; given the same idempotency key and a materially different canonical payload, then the command rejects as `idempotency_conflict`; given idempotency evidence is unavailable after authorization succeeds, then the command fails closed before append.
11. Given a grant already exists or a revoke targets a missing grant, when a different idempotency key is used, then the command returns deterministic `already_applied` or `missing_entry` evidence without appending duplicate or misleading events unless the local EventStore convention requires an explicit no-op event.
12. Given grant/revoke result evidence is produced for accepted, no-op, rejected, duplicate, idempotency-conflicted, idempotency-unavailable, projection-stale, or projection-unavailable outcomes, when callers or tests inspect it, then stable result codes and safe metadata are available without parsing localized text, event type names, stack traces, exception messages, or diagnostic strings.
13. Given folder ACL events are replayed from an empty projection, when grants and revokes are applied in causal event order, then effective ACL metadata is deterministic by tenant, folder, principal kind, principal ID, action, and source, with no dependency on external clocks except explicit freshness fields.
14. Given tests run without provider credentials, tenant seed data, production secrets, running Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, or nested submodules, when unit and smoke tests execute, then grant/revoke validation, tenant gates, ACL-administration gates, idempotency behavior, duplicate/conflict handling, revocation freshness evidence, projection replay, and metadata-only leakage boundaries are covered with in-memory fakes and spies.
15. Given this story manages folder ACL metadata only, when implementation is complete, then it does not implement public effective-permission query endpoints, folder archive behavior, provider readiness, repository creation, repository binding, workspace preparation, locks, file mutation, commits, CLI/MCP/UI commands, workers, production Dapr policy mapping, repair workflows, local-only folder mode, webhooks, brownfield adoption, or multi-organization-per-tenant behavior.

## Tasks / Subtasks

- [ ] Extend the existing Folder aggregate ACL surface. (AC: 1, 2, 3, 5, 13)
  - [ ] Add grant/revoke command types under `src/Hexalith.Folders/Aggregates/Folder/` using local EventStore-oriented naming.
  - [ ] Add metadata-only events such as `FolderAclPrincipalGranted` and `FolderAclPrincipalRevoked`, or equivalent names aligned with local conventions.
  - [ ] Add result/rejection codes for `granted`, `revoked`, `already_applied`, `missing_entry`, `duplicate_entry`, `conflicting_entry`, `unsupported_action`, `invalid_principal`, `invalid_folder`, `folder_not_found`, `folder_inaccessible`, `folder_replay_conflict`, `tenant_access_denied`, `folder_acl_admin_denied`, `acl_evidence_unavailable`, `idempotency_conflict`, `idempotency_unavailable`, `projection_stale`, and `projection_unavailable`.
  - [ ] Keep folder ACL events in the existing `{managedTenantId}:folders:{folderId}` stream shape from Story 2.3.
  - [ ] Keep event payloads metadata-only: tenant ID, folder ID, principal kind, principal ID, action, operation intent, idempotency/correlation/task IDs, safe actor/principal ID, event version/sequence when available, and reason/result code.
- [ ] Add fail-closed tenant, folder, and ACL-administration gates. (AC: 2, 4, 5, 6, 7)
  - [ ] Consume Story 2.1 tenant-access authorizer/projection evidence before constructing stream names or durable keys.
  - [ ] Consume Story 2.2 organization ACL evidence for folder ACL administration; if a permission token is missing, add one token to the same domain ACL vocabulary and cover it with strict lower-snake-case tests.
  - [ ] Treat tenant IDs from route/body/query/header as comparison values only, never as authority.
  - [ ] Load the folder only after tenant and ACL-administration gates pass; unauthorized paths must not reveal folder existence.
  - [ ] Add side-effect spies proving denied paths do not construct stream names, load streams, append events, mutate aggregate state, update projections, query diagnostics, query audit resources, call providers, or inspect repositories.
- [ ] Implement ACL entry validation and canonicalization. (AC: 8, 9, 12)
  - [ ] Validate principal kinds as `user`, `group`, `role`, or `delegated_service_agent`.
  - [ ] Validate principal IDs as opaque metadata-only identifiers and reject emails, display names, raw headers, tokens, credential-shaped values, empty segments, `:` characters, control characters, and unsupported normalization forms.
  - [ ] Validate action tokens as strict lower-snake-case domain values; do not coerce aliases, localized labels, mixed casing, or provider verbs.
  - [ ] Canonicalize entries culture-invariantly by tenant, folder ID, principal kind, principal ID, action, and operation intent.
  - [ ] Collapse exact duplicate entries and reject conflicting same-tuple grant/revoke operations before stream access.
- [ ] Add idempotency and deterministic no-op handling. (AC: 10, 11, 12)
  - [ ] Reuse Epic 1 idempotency equivalence rules and generated client helper semantics; do not hand-edit `src/Hexalith.Folders.Client/Generated/*`.
  - [ ] Include command type, operation intent, authoritative tenant ID, folder ID, principal kind, principal ID, action, and safe actor/principal ID in canonical idempotency payloads.
  - [ ] Scope ACL mutation keys by authoritative tenant ID and opaque folder ID after gates pass.
  - [ ] Return the same logical result for equivalent replay without appending duplicate grant/revoke events.
  - [ ] Reject same-key materially different payloads as `idempotency_conflict`.
  - [ ] Fail closed as `idempotency_unavailable` when introduced idempotency evidence cannot prove equivalence after authorization succeeds.
  - [ ] Return deterministic `already_applied` or `missing_entry` evidence for same-state operations without leaking unauthorized state.
- [ ] Add effective ACL metadata projection support. (AC: 3, 12, 13)
  - [ ] Add or extend a minimal folder ACL projection that can rebuild grant/revoke state from folder ACL events.
  - [ ] Derive tenant scope from stream/envelope metadata; event payload tenant fields are comparison inputs only if both are present.
  - [ ] Track revocation projection evidence needed to prove the configured C7/status-freshness budget.
  - [ ] Cover out-of-order, duplicate, malformed, tenant-mismatched, missing-grant, and conflicting events deterministically.
  - [ ] Keep projection output metadata-only and tenant-scoped; do not add public effective-permission query endpoints in this story.
- [ ] Add tests and fixtures. (AC: 1-15)
  - [ ] Add tests under `tests/Hexalith.Folders.Tests/Aggregates/Folder/` or an equivalent local path for grant, revoke, duplicate grant, revoke missing grant, unsupported action, invalid principal, reserved tenant, folder stream shape, and replay apply logic.
  - [ ] Add authorization gate tests for stale/unavailable/disabled/unknown/malformed/future/replay-conflicting tenant evidence, tenant mismatch, missing authoritative tenant, denied ACL administration evidence, unavailable ACL evidence, malformed ACL evidence, and inaccessible folder evidence.
  - [ ] Add idempotency tests for equivalent grant replay, equivalent revoke replay, same key plus changed principal/action/folder/intent, idempotency evidence unavailable after authorization, and tenant-prefixed ACL mutation keys.
  - [ ] Add intra-command duplicate/conflict tests covering exact duplicate grants, exact duplicate revokes, mixed grant/revoke intent for the same tuple, mixed principal-kind collision attempts, and action casing/alias rejection.
  - [ ] Add metadata leakage tests with sentinel values for auth tokens, provider tokens, credential material, user emails, group display names, repository names, branch names, file paths, file contents, diffs, generated context payloads, arbitrary tenant configuration values, and unauthorized resource names.
  - [ ] Add projection replay tests proving grant/revoke events rebuild deterministic effective ACL metadata, revocations remove or tombstone access, and same folder IDs in different tenants remain isolated.
  - [ ] Extend `src/Hexalith.Folders.Testing/Factories/*` only with reusable ACL mutation builders that delegate to production validation rules.
  - [ ] Add conformance tests in `tests/Hexalith.Folders.Testing.Tests` if new testing helpers are introduced.
  - [ ] Use pure in-memory fakes only for EventStore seams, tenant evidence, ACL evidence, clock/time, validators, idempotency records, projections, diagnostics sinks, and audit sinks.

## Dev Notes

### Source Context

- Epic 2 objective: tenant administrators and authorized actors can create folders, manage access, inspect effective permissions, archive folders, and receive safe authorization evidence with cross-tenant isolation enforced before any resource access. [Source: `_bmad-output/planning-artifacts/epics.md#Epic 2: Tenant-Scoped Folder Access And Lifecycle`]
- Story 2.4 requires grant/revoke operations to record and project effective ACL metadata and honor revocation within the C7 freshness budget. [Source: `_bmad-output/planning-artifacts/epics.md#Story 2.4`]
- PRD FR5 requires folder access grants to users, groups, roles, and delegated service agents; FR6 moves public effective-permission inspection to the next story; FR8-FR10 require scoped authorization evidence and safe denials without unauthorized resource detail. [Source: `_bmad-output/planning-artifacts/prd.md#Authorization and Tenant Boundary`]
- Journey 9 frames the user value as tenant-administrator operational confidence for granting, revoking, verifying, and later archiving folder access with metadata-only audit posture. [Source: `_bmad-output/planning-artifacts/prd.md#Journey 9: Tenant Administrator Manages Folder Access and Lifecycle`]
- Architecture requires one layered authorization path: JWT/authoritative tenant claim, local tenant-access projection, folder ACL, EventStore validators, and production Dapr policy. [Source: `_bmad-output/planning-artifacts/architecture.md#Cross-Cutting Concerns`]

### Previous Story Intelligence

- Story 2.1 defines the Folders host, Tenants event subscription, local fail-closed tenant-access projection, and tenant-access authorizer outcomes. Grant/revoke must consume those outcomes before folder stream selection.
- Story 2.1 advanced elicitation hardened replay conflicts, projection-store unavailable semantics, configuration-removal tombstones, diagnostic leakage boundaries, and structural endpoint tests. Reuse stable outcome codes and evidence fields.
- Story 2.2 defines the organization ACL baseline, lower-snake-case action parsing, namespace-separated principal kinds, idempotency canonicalization, and side-effect negative controls. This story extends or consumes that same ACL vocabulary; it must not create a parallel folder permission model.
- Story 2.3 defines the folder aggregate, opaque folder identity, active logical lifecycle, stream shape, metadata-only events/results, tenant and create ACL gates, idempotency behavior, duplicate handling, and minimal projection replay. Grant/revoke must extend that folder stream and projection model rather than introducing a separate access store.

### Existing Implementation State

- `src/Hexalith.Folders/FoldersModule.cs` currently exposes scaffold metadata only; no folder aggregate or ACL domain surface exists in the checked-in source yet.
- `src/Hexalith.Folders.Testing/Factories/TestFolderContext.cs` validates stream-name segments and exposes `FolderStreamName` as `{ManagedTenantId}:folders:{FolderId}`. Reuse or extend this pattern instead of creating a second stream naming convention.
- `src/Hexalith.Folders.Testing/Factories/TestAuthorizationContext.cs` provides simple managed tenant and permission test context data. Extend it only if folder ACL tests need reusable builders.
- `src/Hexalith.Folders.Client/Generated/*` contains generated client/idempotency helpers. Do not hand-edit generated files.
- Active development may already be modifying Story 1.15 safety invariant files and sprint status. Treat those changes as unrelated unless the implementation branch explicitly depends on them.

### Required Architecture Patterns

- Keep `Hexalith.Folders.Contracts` behavior-free. Folder ACL command validation, authorization gates, idempotency equivalence, EventStore handling, and projection replay belong in `Hexalith.Folders` and tests unless the Contract Spine workflow explicitly requires a contract change.
- Keep aggregates pure. The aggregate applies validated commands, state, and events; Dapr, provider, filesystem, Git, UI, CLI, MCP, workers, diagnostics, audit, and projection-store side effects stay outside aggregate state transitions.
- Authorization order for mutating folder ACL paths is authoritative tenant context, Story 2.1 tenant evidence, folder existence/lifecycle evidence, Story 2.2 ACL-administration evidence, then idempotency/stream load/mutation. Never search, load, append, diagnose, audit, or project first and filter later.
- Metadata validation and principal/action canonicalization happen before ACL mutation key construction, stream loading, duplicate lookup, append, projection update, diagnostics, audit lookup, and aggregate mutation.
- Use stable result codes, enum values, and safe metadata fields in tests. Do not assert localized or user-facing diagnostic text.
- Events, logs, traces, metrics, projections, audit records, and errors must remain metadata-only and must not include raw credentials, provider tokens, file contents, diffs, generated context payloads, repository names, branch names, raw paths, user emails, group display names, arbitrary tenant configuration, or unauthorized resource existence.
- Cache keys and durable operational keys must carry tenant scope. A missing tenant prefix is a correctness and security bug.
- Use C# file-scoped namespaces, nullable-aware records/classes, one public type per file, PascalCase types/members, camelCase locals, and async APIs where I/O boundaries are introduced.

### Files To Touch

- `src/Hexalith.Folders/Aggregates/Folder/*Acl*.cs`
- `src/Hexalith.Folders/Aggregates/Folder/*Grant*.cs`
- `src/Hexalith.Folders/Aggregates/Folder/*Revoke*.cs`
- `src/Hexalith.Folders/Aggregates/Folder/*Result*.cs`
- `src/Hexalith.Folders/Authorization/*` only for narrow seams needed to consume Story 2.1 tenant evidence and Story 2.2 ACL-administration outcomes.
- `src/Hexalith.Folders/Idempotency/*` only if no existing reusable equivalence helper exists for domain command payloads.
- `src/Hexalith.Folders/Projections/FolderAcl/*` only for minimal grant/revoke replay evidence.
- `src/Hexalith.Folders.Testing/Factories/*` only for reusable folder ACL test builders.
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/*Acl*Tests.cs`
- `tests/Hexalith.Folders.Testing.Tests/*` only when new testing helpers are added.

### Do Not Touch

- Do not edit generated SDK files in `src/Hexalith.Folders.Client/Generated/`.
- Do not add or modify OpenAPI Contract Spine operations unless implementation discovers a blocking mismatch that is explicitly handled through the contract workflow.
- Do not implement public effective-permission inspection; Story 2.5 owns that query shape.
- Do not implement folder archive behavior, provider readiness, provider adapters, repository creation, repository binding, workspace preparation, locks, file mutation, commits, context query, CLI, MCP, UI, workers, production Dapr policy, repair workflows, local-only folder mode, webhooks, brownfield adoption, or multi-organization-per-tenant behavior.
- Do not initialize nested submodules or use recursive submodule commands.

### Testing

- Unit tests must run without provider credentials, tenant seed data, production secrets, running Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, or nested submodule initialization.
- Use xUnit v3 and Shouldly for aggregate tests; use NSubstitute only where an actual seam needs substitution.
- Add focused tests named `FolderAclCommandValidationTests`, `FolderAclTenantEvidenceGateTests`, `FolderAclAdministrationGateTests`, `FolderAclIdempotencyTests`, `FolderAclProjectionReplayTests`, `FolderAclMetadataLeakageTests`, and `FolderAclStreamShapeTests`, or equivalent locally consistent names.
- Include side-effect negative controls proving rejected tenant evidence, rejected ACL evidence, missing folder evidence, invalid principal/action metadata, duplicate conflict, idempotency conflict, and idempotency unavailability do not construct stream names, load streams, append events, mutate aggregate state, update projections, query diagnostics, query audit resources, call provider readiness, or create repositories.
- Include replay tests proving `FolderAclPrincipalGranted` and `FolderAclPrincipalRevoked` deterministically produce the same effective ACL metadata from event history and that revocations are visible through freshness evidence.
- Include positive and negative metadata tests: allowed diagnostics may include tenant ID, folder ID, principal kind, principal ID, action, result code, correlation/task/idempotency IDs, projection watermark, and safe actor/principal ID; forbidden diagnostics must not include raw auth tokens, provider payloads, command bodies, repository/branch/path values, diffs, file contents, generated context, stack traces with sensitive values, user emails, group names, arbitrary tenant configuration, or unauthorized resource identifiers.

### Regression Traps

- Do not grant or revoke access from a payload tenant ID, route tenant ID, query tenant ID, or client-controlled header.
- Do not silently allow ACL mutation when tenant evidence, folder evidence, or ACL-administration evidence cannot be proven.
- Do not create a second permission vocabulary for folder ACLs. Extend or consume the Story 2.2 lower-snake-case domain vocabulary.
- Do not expose whether an unauthorized folder, grant, principal, group, role, repository, provider, workspace, audit record, or idempotency record exists.
- Do not implement effective-permission query endpoints in this story. Projection state is a prerequisite for Story 2.5.
- Do not let grant/revoke replay append duplicate events or let same-key different-payload requests pass as equivalent.
- Do not make user emails, display names, group names, provider names, repository names, branch names, file paths, or folder display names part of ACL entry identity.
- Do not log or project raw command payloads, raw request headers, provider identifiers, repository URLs, branches, paths, file contents, diffs, generated context payloads, secrets, or unauthorized resource existence.
- Do not add public list/get endpoints, UI actions, CLI commands, MCP tools, or workers unless a later story owns the surface.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 2.4`
- `_bmad-output/planning-artifacts/prd.md#Authorization and Tenant Boundary`
- `_bmad-output/planning-artifacts/prd.md#Journey 9: Tenant Administrator Manages Folder Access and Lifecycle`
- `_bmad-output/planning-artifacts/architecture.md#Cross-Cutting Concerns`
- `_bmad-output/project-context.md#Critical Implementation Rules`
- `_bmad-output/implementation-artifacts/2-1-stand-up-domain-service-host-with-tenants-integration.md`
- `_bmad-output/implementation-artifacts/2-2-implement-organization-aggregate-acl-baseline.md`
- `_bmad-output/implementation-artifacts/2-3-create-folders-within-a-tenant.md`
- `src/Hexalith.Folders.Testing/Factories/TestFolderContext.cs`
- `src/Hexalith.Folders.Testing/Factories/TestAuthorizationContext.cs`

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-17 | Created story with folder ACL grant/revoke commands, tenant and ACL-administration gates, idempotency, revocation projection freshness, metadata-only evidence, and offline tests. | Codex |

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

### Completion Notes List

- Story created by `/bmad-create-story 2-4-grant-and-revoke-folder-access` equivalent workflow on 2026-05-17.
- Project context, Epic 2, PRD, architecture, Stories 2.1 through 2.3, testing factories, recent commits, and story-creation lessons were reviewed.
- Preflight had an active-dev-story soft warning for Story 1.15 review changes; those files were left untouched by this story creation.

### File List
