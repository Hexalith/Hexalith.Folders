# Story 1.7: Author tenant, folder, provider, and repository-binding contract groups

Status: review

Created: 2026-05-11

## Story

As an API consumer and adapter implementer,
I want tenant, folder, provider, and repository-binding operations represented in the Contract Spine,
so that access and provider readiness capabilities are canonical before implementation begins.

## Acceptance Criteria

1. Given the shared Contract Spine foundation and extension vocabulary from Story 1.6 exist or are explicitly reference-pending, when this story is complete, then `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` contains operation groups for tenant-context-safe folder lifecycle, folder ACL, effective permissions, provider binding, provider readiness, repository creation, repository binding, and branch/ref policy.
2. Given tenant authority is never client-controlled, when request schemas, query parameters, headers, and path parameters are authored, then no payload, query parameter, client-controlled header, provider identifier, repository identifier, or cross-tenant lookup field defines authoritative tenant context; tenant authority is documented as coming from authentication context and EventStore envelopes only.
3. Given folder lifecycle and access operations are authored, when the Contract Spine is inspected, then create folder, inspect folder lifecycle/status, archive folder, manage folder ACL entries, and inspect effective permissions have stable `operationId` values, schemas, canonical responses, safe-denial Problem Details, audit metadata declarations, correlation behavior, and parity dimensions.
4. Given provider readiness and binding operations are authored, when the Contract Spine is inspected, then configure or inspect provider binding references, validate provider readiness, expose provider support/capability evidence, create a repository-backed folder, bind an existing repository, and configure or inspect branch/ref policy have schemas without credential material, provider tokens, raw repository secrets, or unauthorized resource existence leaks.
5. Given mutating operations are authored, when their OpenAPI Operation Objects are inspected, then each declares `Idempotency-Key`, `x-hexalith-idempotency-key`, lexicographically ordered `x-hexalith-idempotency-equivalence`, `x-hexalith-idempotency-ttl-tier`, required correlation metadata, canonical error categories, audit metadata keys, and adapter parity dimensions.
6. Given read/query operations are authored, when their OpenAPI Operation Objects are inspected, then each declares read consistency, freshness behavior where applicable, pagination/filtering where applicable, safe authorization denial shape, audit classification, correlation behavior, canonical error categories, and adapter parity dimensions; non-mutating reads do not accept `Idempotency-Key`.
7. Given folder, provider, repository, and branch/ref examples are included, when examples and schemas are scanned, then all examples use synthetic opaque placeholders only and contain no real tenant IDs, repository URLs with credentials, file paths from this machine, branch names with sensitive values, provider identifiers, credential-shaped values, provider tokens, file contents, diffs, production URLs, email addresses, organization names, or unauthorized resource hints.
8. Given provider support must remain capability-tested, when provider and repository schemas are authored, then GitHub and Forgejo are represented through provider-neutral capability and binding metadata; the contract does not assume Forgejo is a GitHub base-URL swap or flatten provider permissions into a lowest-common-denominator model.
9. Given this is a contract-group authoring story, when implementation is complete, then no runtime REST handlers, EventStore commands, domain aggregates, provider adapters, SDK generated output, NSwag generation wiring, CLI commands, MCP tools, workers, UI pages, parity-oracle rows, CI workflow gates, or nested-submodule initialization are added by this story.
10. Given downstream stories own workspace, files, commits, audit, and operations-console groups, when this story is complete, then operation paths and schemas remain limited to tenant/folder/provider/repository/branch-ref concerns and do not implement workspace/lock, file mutation, context query, commit/status, audit-timeline, or ops-console projection groups except for shared references needed by these operations.
11. Given the Contract Spine is a mechanical source for later SDK and parity work, when validation runs, then OpenAPI 3.1 parsing succeeds offline, all `$ref` targets resolve, all new `operationId` values are unique and stable, every operation has required `x-hexalith-*` metadata, no client-controlled tenant authority exists, secret-shaped schema fields are rejected, and negative-scope checks prove generated clients/adapters/runtime behavior were not added.
12. Given prior Story 1.5 rules may not yet be implemented as files, when operation metadata cannot be populated from approved sources, then the OpenAPI records an explicit `TODO(reference-pending): <field-or-decision>` note or deferred-decision comment instead of inventing idempotency equivalence fields, C3/C4-derived limits, S-2 issuer/audience values, or provider capability guarantees.

## Tasks / Subtasks

- [x] Confirm prerequisites and preserve scope boundaries. (AC: 1, 9, 10, 12)
  - [x] Inspect Story 1.6 deliverables: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` and `src/Hexalith.Folders.Contracts/openapi/extensions/`. If absent, create only the operation-group additions this story owns with clear `TODO/reference-pending` markers where shared foundation values are missing.
  - [x] Inspect `docs/contract/idempotency-and-parity-rules.md`, `tests/fixtures/parity-contract.schema.json`, `tests/fixtures/idempotency-encoding-corpus.json`, and `tests/fixtures/previous-spine.yaml` if present; treat missing files as prerequisite drift to record, not permission to invent policy.
  - [x] Inspect `docs/exit-criteria/c3-retention.md`, `docs/exit-criteria/c4-input-limits.md`, and S-2/C6 evidence if present; unresolved values must stay reference-pending.
  - [x] Do not initialize or update nested submodules. Use sibling modules only as read-only references unless explicitly directed otherwise.
  - [x] Do not add runtime code, generated SDK output, CLI/MCP tools, provider adapters, workers, UI, final parity rows, or CI gates.
  - [x] Limit allowed story outputs to OpenAPI contract changes, existing Contract project DTO/identity artifacts only when needed for contract compilation, synthetic examples, optional contract notes, and focused offline validation assets.
- [x] Author tenant and folder lifecycle contract operations. (AC: 1, 2, 3, 5, 6)
  - [x] Add or update tags and paths for folder lifecycle and access operations under `/api/v1/folders...` using lowercase hyphen-delimited path segments and camelCase path parameters such as `{folderId}`.
  - [x] Define stable `operationId` values for create folder, get folder lifecycle/status, archive folder, list/update folder ACL entries, and get effective permissions.
  - [x] Model folder identity as opaque IDs and projected metadata; do not make folder names, hierarchy, or paths aggregate identity.
  - [x] Ensure all authorization-denied responses are safe and metadata-only, without revealing whether a tenant, folder, ACL entry, provider binding, repository binding, or branch/ref policy exists to unauthorized callers.
  - [x] Ensure safe-denial examples share one externally indistinguishable response envelope for unauthorized, absent, cross-tenant, and stale-read resources unless an already-authorized diagnostic endpoint explicitly permits more detail.
  - [x] Ensure mutating operations declare idempotency metadata and reads declare read-consistency/freshness metadata without accepting `Idempotency-Key`.
- [x] Author provider binding and provider readiness contract operations. (AC: 1, 2, 4, 5, 6, 8)
  - [x] Add provider binding reference and readiness operations under provider-readiness or organization/provider capability paths that align with the existing Contract Spine tag model.
  - [x] Represent credential references as non-secret opaque references and readiness evidence as sanitized metadata; never include credential material, raw provider tokens, or secret-store contents.
  - [x] Separate operator-visible readiness diagnostics from consumer-visible safe denials; consumer responses must not reveal credential validity, provider installation identity, provider account ownership, or repository existence.
  - [x] Model provider capabilities for repository creation, existing repository binding, branch/ref handling, file operations, commit/status behavior, provider errors, and failure behavior without hardcoding exactly two providers into schema shape.
  - [x] Distinguish provider readiness failed, credential reference missing/invalid, provider permission insufficient, provider unavailable, provider rate limited, unsupported provider capability, and read-model unavailable canonical categories.
  - [x] Preserve GitHub/Forgejo portability: capability differences are explicit metadata and later contract tests decide support, not assumptions in this story.
- [x] Author repository creation, repository binding, and branch/ref policy operations. (AC: 1, 2, 4, 5, 6, 8)
  - [x] Add contract operations for creating a repository-backed folder and binding an existing repository where supported.
  - [x] Include branch/ref policy request and response schemas with synthetic examples only; avoid real URLs, branch names with sensitive data, credential-shaped values, and provider-specific permission secrets.
  - [x] Define duplicate binding, repository conflict, cross-tenant access denied, provider readiness failed, unsupported provider capability, unknown provider outcome, and reconciliation required error outcomes.
  - [x] Include audit metadata keys for provider, provider binding reference, repository binding identity, folder ID, branch/ref policy, correlation ID, operation ID, task ID where relevant, result, timestamp, and sanitized error category.
  - [x] Keep workspace preparation, locks, file mutation, commit, status, audit timeline, and ops-console projection operations deferred to Stories 1.8 through 1.11.
- [x] Apply shared OpenAPI conventions consistently. (AC: 2, 5, 6, 11, 12)
  - [x] Reuse shared headers, parameters, Problem Details, freshness metadata, pagination/filtering conventions, and extension vocabulary from Story 1.6 instead of duplicating incompatible shapes.
  - [x] Use camelCase JSON properties, ISO-8601 UTC timestamps, string enums, opaque ULID identifiers, forward-slash metadata paths, and metadata-only examples.
  - [x] Ensure every mutating operation has `Idempotency-Key` and every non-mutating operation omits it.
  - [x] Ensure every operation declares canonical error categories, authorization requirement, correlation behavior, audit classification, and parity dimensions.
  - [x] Use `TODO/reference-pending` only for unresolved approved-source values, with exact source paths or decision owners when known.
- [x] Add focused offline validation. (AC: 5, 6, 7, 9, 11)
  - [x] Add or update the smallest local validator or test that parses `hexalith.folders.v1.yaml` as OpenAPI 3.1 and resolves all local `$ref` targets without network access.
  - [x] Verify new operation IDs are unique, stable, and limited to this story's operation groups by checking an explicit allow-list derived from the Operation Inventory Seed.
  - [x] Verify all new operations include required `x-hexalith-*` metadata and that mutating/query operations satisfy their idempotency or read-consistency requirements.
  - [x] Verify no request payload, query parameter, or client-controlled header defines tenant authority.
  - [x] Verify examples are synthetic and metadata-only.
  - [x] Verify safe-denial examples for unauthorized tenant, missing folder, missing provider binding, missing repository binding, and missing branch/ref policy use externally indistinguishable shapes where existence must not be inferred.
  - [x] Verify schema and example field names reject secret-shaped or credential-shaped terms such as `token`, `secret`, `credential`, `password`, `privateKey`, `accessToken`, and raw provider authorization material unless the field is an explicit non-secret opaque reference.
  - [x] Verify provider diagnostic examples are partitioned by audience so consumer-facing contract examples stay redacted while authorized operator-readiness examples remain sanitized and metadata-only.
  - [x] Verify negative scope: no generated SDK files, NSwag generation wiring, REST handlers/controllers, CLI commands, MCP tools, domain aggregate behavior, provider adapters, workers, UI pages, final parity oracle rows, CI workflow gates, or nested-submodule initialization.
  - [x] Run `dotnet build Hexalith.Folders.slnx` if the scaffold supports it after focused validation. If blocked by earlier scaffold state, record the exact prerequisite instead of expanding this story.
- [x] Record downstream authoring notes. (AC: 10, 12)
  - [x] Add a short note near the OpenAPI file or in `docs/contract/` explaining which shared components and extension metadata Stories 1.8 through 1.11 must reuse.
  - [x] Record deferred owners for workspace/lock, file/context, commit/status, audit/ops-console, NSwag generation, parity oracle, and CI gates.
  - [x] Record any unresolved C3/C4/S-2/C6 or Story 1.5 metadata dependencies as explicit deferred decisions.

## Dev Notes

### Scope Boundaries

- This story authors Contract Spine operation groups for tenant, folder, provider, repository binding, repository creation, and branch/ref policy only.
- Contract-only output means OpenAPI contract declarations, synthetic examples, optional contract notes, and focused offline validation. Do not broaden this story into runtime behavior, generated consumers, provider integration, or CI workflow wiring.
- Primary files expected:

```text
src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml
src/Hexalith.Folders.Contracts/openapi/extensions/
```

- Optional supporting docs, only if needed:

```text
docs/contract/tenant-folder-provider-repository-contract-groups.md
```

- Do not add runtime behavior. `Hexalith.Folders.Contracts` remains behavior-free: DTOs, identity value objects, OpenAPI/contract artifacts, and contract extensions only.
- Do not add `src/Hexalith.Folders.Client/Generated/`, NSwag configuration, server endpoints, EventStore commands, aggregate logic, provider adapters, CLI/MCP commands, workers, UI, parity rows, or CI gates.
- Do not add operation groups owned by later stories:
  - Story 1.8: workspace and lock.
  - Story 1.9: file mutation and context query.
  - Story 1.10: commit and workspace status.
  - Story 1.11: audit and operations-console query.

### Contract Group Requirements

- Use OpenAPI 3.1 at `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` as the single source of truth.
- REST paths are URL-versioned under `/api/v1`, use lowercase hyphen-delimited segments, plural collection nouns where appropriate, and camelCase path parameters.
- Stable operation IDs matter because NSwag generation and the C13 parity oracle will consume them later.
- Tenant authority comes from auth context and EventStore envelopes. Payloads may include resource IDs to validate, but never authoritative tenant context.
- Tenant-scoped operations may carry resource identifiers needed to address a route, but examples and schemas must not imply ambient tenant inference, provider-owned tenant authority, or cross-tenant lookup by repository/provider identifiers alone.
- Folder hierarchy, display names, and paths are projected metadata, not aggregate identity. Folder IDs and repository binding IDs should be opaque.
- Provider capability metadata must support more than GitHub and Forgejo. Do not encode a two-provider-only union if a generic capability model can represent additional providers.
- Repository names, branch names, paths, commit messages, provider IDs, and repository binding references are sensitive metadata unless a stricter tenant policy applies.
- Treat provider binding references, repository binding IDs, and branch/ref policy IDs as tenant-scoped opaque values. They may appear in authorized metadata, but they must not become tenant authority, provider account authority, or proof that a protected external repository exists.
- Keep provider readiness evidence audience-scoped: consumer-facing contract responses should expose safe status and retry/action guidance, while any richer operator diagnostics must be explicitly authorized, redacted, and free of credential state, provider installation IDs, raw provider payloads, and unauthorized resource hints.

### Operation Inventory Seed

Use the operation names below as a starting point unless Story 1.6 or Story 1.5 has already frozen different names. If names differ, keep the OpenAPI `operationId` stable and record the mapping.

| Operation | Type | Required metadata |
| --- | --- | --- |
| `CreateFolder` | Mutating command | idempotency, folder ACL authorization, audit keys, correlation, parity dimensions |
| `GetFolderLifecycleStatus` | Query/status | read consistency, freshness, safe denial, lifecycle state metadata |
| `ArchiveFolder` | Mutating command | idempotency, archive policy, audit keys, safe denial |
| `ListFolderAclEntries` | Query | pagination/filtering, safe denial, audit classification |
| `UpdateFolderAclEntry` | Mutating command | idempotency, folder ACL authorization, audit keys |
| `GetEffectivePermissions` | Query | safe denial, authorization outcome class, freshness |
| `ConfigureProviderBinding` | Mutating command | idempotency, credential reference metadata, no secrets |
| `GetProviderBinding` | Query | safe denial, redaction, freshness |
| `ValidateProviderReadiness` | Query/status | readiness evidence, provider diagnostic metadata, canonical provider errors |
| `GetProviderSupportEvidence` | Query/status | capability metadata, provider contract evidence references |
| `CreateRepositoryBackedFolder` | Mutating command | idempotency, provider readiness, repository naming policy, branch/ref policy |
| `BindRepository` | Mutating command | idempotency, duplicate binding handling, provider binding identity, branch/ref policy |
| `GetRepositoryBinding` | Query | safe denial, sensitive metadata tiers, freshness |
| `ConfigureBranchRefPolicy` | Mutating command | idempotency, branch/ref validation, audit keys |
| `GetBranchRefPolicy` | Query | read consistency, safe denial, policy metadata |

### Error and Audit Requirements

- Required canonical categories for this story include authentication failure, tenant authorization denied, folder ACL denied, cross-tenant access denied, provider readiness failed, credential reference missing or invalid, provider permission insufficient, provider unavailable, provider rate limited, repository conflict, duplicate binding, idempotency conflict, unsupported provider capability, validation error, read-model unavailable, reconciliation required, unknown provider outcome, audit access denied, not found, redacted, and internal error.
- Use RFC 9457 Problem Details plus Hexalith fields: `category`, `code`, `message`, `correlationId`, `retryable`, `clientAction`, and `details`.
- Safe-denial Problem Details must expose stable safe codes only; they must not reveal credential state, provider installation IDs, internal provider payloads, authorization reasoning, or whether protected tenant/folder/provider/repository/branch-ref resources exist.
- Audit metadata is metadata-only. It may include tenant-scoped actor evidence, folder ID, provider reference, repository binding ID, branch/ref policy reference, operation ID, correlation ID, task ID where relevant, timestamp, result, duration, and sanitized error category.
- Do not include provider tokens, credential material, file contents, diffs, generated context payloads, production URLs, or unauthorized resource existence.

### Idempotency and Read Consistency

- Mutating operations require caller-supplied `Idempotency-Key`; queries do not accept it.
- Idempotency equivalence is tenant-scoped and operation-scoped. Same key across tenants, changed command intent, changed target resource identity, changed semantic payload, or changed behavior-affecting credential scope is non-equivalent.
- Field lists in `x-hexalith-idempotency-equivalence` must be lexicographically ordered.
- Mutation-tier TTL is 24 hours. Commit-tier TTL belongs to downstream commit work and inherits C3.
- Read operations declare one of the approved read consistency classes from Story 1.6/architecture: `snapshot-per-task`, `read-your-writes`, or `eventually-consistent`.
- Freshness and pagination/filtering should use shared components from Story 1.6. Do not invent C4 numeric limits if `docs/exit-criteria/c4-input-limits.md` is missing.
- Use `TODO(reference-pending): <field-or-decision>` for unresolved approved-source values and include the source path or decision owner when known.

### Previous Story Intelligence

- Story 1.1 establishes the solution scaffold and dependency direction.
- Story 1.2 establishes root configuration and forbids recursive nested-submodule initialization.
- Story 1.3 seeds shared fixtures: audit leakage corpus, parity schema, previous spine, idempotency encoding corpus, `tests/load`, and artifact templates.
- Story 1.4 owns C3 retention, C4 input limits, S-2 OIDC parameters, and C6 transition mapping. This story consumes those values but must not invent them.
- Story 1.5 defines operation inventory, idempotency equivalence, adapter parity dimensions, parity schema expectations, and encoding corpus rules. This story translates the relevant tenant/folder/provider/repository subset into OpenAPI metadata.
- Story 1.6 owns the OpenAPI foundation and shared `x-hexalith-*` vocabulary. This story must reuse it rather than redefine extension shapes.
- At story creation time, `src/Hexalith.Folders.Contracts/Hexalith.Folders.Contracts.csproj` exists but the `openapi/` directory was not present. The implementation agent may create only the narrow OpenAPI paths this story owns if prior stories have not materialized them.
- At story creation time, `docs/contract/` was not present and `tests/fixtures/parity-contract.schema.json` was still minimal. Treat that as dependency context, not permission to widen this story.

### Testing Guidance

- Prefer a focused OpenAPI validation test or script in existing test/tool locations over broad runtime integration work.
- Validation must run offline without Aspire, Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, provider credentials, tenant data, production secrets, network calls, or initialized nested submodules.
- Validate OpenAPI parse, `$ref` resolution, unique operation IDs, exact `x-hexalith-*` metadata presence, idempotency/read-consistency completeness, safe tenant-authority boundaries, synthetic examples, and negative scope.
- Include contract-level assertions for safe-denial parity, secret-shaped field names, and non-secret opaque credential references.
- Include allow-list assertions for the operation IDs and `/api/v1` path prefixes owned by this story so validation fails if workspace/lock, file/context, commit/status, audit timeline, operations-console, runtime, generated-client, CLI, MCP, worker, UI, or CI artifacts appear.
- Include fixture assertions that consumer-visible safe denials for unauthorized, absent, cross-tenant, missing binding, and missing branch/ref policy cases remain externally indistinguishable while authorized readiness diagnostics remain sanitized.
- If the full solution is buildable, run `dotnet build Hexalith.Folders.slnx` from the repository root after focused validation. Record exact blockers if build cannot run due to prior scaffold state.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 1.7: Author tenant, folder, provider, and repository-binding contract groups`
- `_bmad-output/planning-artifacts/epics.md#Epic 1: Bootstrap Canonical Contract For Consumers And Adapters`
- `_bmad-output/planning-artifacts/architecture.md#Provider boundaries`
- `_bmad-output/planning-artifacts/architecture.md#Cross-Cutting Concerns Identified`
- `_bmad-output/planning-artifacts/architecture.md#REST endpoint naming`
- `_bmad-output/planning-artifacts/architecture.md#HTTP headers`
- `_bmad-output/planning-artifacts/architecture.md#Requirements to Structure Mapping`
- `_bmad-output/planning-artifacts/prd.md#Command and Query Contract`
- `_bmad-output/planning-artifacts/prd.md#Error Codes`
- `_bmad-output/planning-artifacts/prd.md#Contract and Quality Gates`
- `_bmad-output/implementation-artifacts/1-1-establish-a-consumer-buildable-module-scaffold.md`
- `_bmad-output/implementation-artifacts/1-2-establish-root-configuration-and-submodule-policy.md`
- `_bmad-output/implementation-artifacts/1-3-seed-minimally-valid-normative-fixtures.md`
- `_bmad-output/implementation-artifacts/1-4-author-phase-0-5-pre-spine-workshop-deliverables.md`
- `_bmad-output/implementation-artifacts/1-5-finalize-idempotency-equivalence-and-adapter-parity-rules.md`
- `_bmad-output/implementation-artifacts/1-6-author-contract-spine-foundation-and-shared-extension-vocabulary.md`
- `_bmad-output/project-context.md`
- `AGENTS.md#Git Submodules`

## Project Structure Notes

- The Contract Spine operation groups live under `src/Hexalith.Folders.Contracts/openapi/`.
- Human-readable contract notes may live under `docs/contract/`, but the OpenAPI document remains the source of truth.
- Provider runtime code belongs later under `src/Hexalith.Folders/Providers/` and must not be implemented here.
- Server endpoints belong later under `src/Hexalith.Folders.Server/Endpoints/` and must not be implemented here.
- CLI and MCP wrappers belong later under `src/Hexalith.Folders.Cli/` and `src/Hexalith.Folders.Mcp/` and must not be implemented here.

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-11 | Advanced elicitation applied operation allow-list, safe-denial fixture, and provider-diagnostic redaction hardening. | Codex |
| 2026-05-11 | Party-mode review applied contract-only boundary, safe-denial, tenant-authority, placeholder, secret-field, and validation guardrails. | Codex |
| 2026-05-11 | Created ready-for-dev story through `bmad-create-story` workflow. | Codex |
| 2026-05-12 | Implemented tenant, folder, provider, repository binding, and branch/ref Contract Spine groups with offline validation. | Codex |

## Party-Mode Review

- Date/time: 2026-05-11T07:07:07Z
- Selected story key: `1-7-author-tenant-folder-provider-and-repository-binding-contract-groups`
- Command/skill invocation used: `/bmad-party-mode 1-7-author-tenant-folder-provider-and-repository-binding-contract-groups; review;`
- Participating BMAD agents: Winston (System Architect), John (Product Manager), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor)
- Findings summary:
  - Tighten the contract-only implementation boundary so the development pass cannot drift into runtime handlers, adapters, generated clients, CLI/MCP, UI, parity rows, CI gates, or nested submodule work.
  - Make tenant-authority and safe-denial requirements testable at the OpenAPI contract level, including externally safe Problem Details and no resource-existence leaks.
  - Preserve provider-neutral capability modeling and avoid GitHub/Forgejo assumptions, provider secret leakage, or hardcoded provider-specific schema shapes.
  - Strengthen offline validation for OpenAPI parsing, `$ref` resolution, unique operation IDs, required `x-hexalith-*` metadata, idempotency/read separation, synthetic examples, secret-shaped fields, and negative scope.
- Changes applied:
  - Added explicit allowed-output language for contract-only work.
  - Clarified tenant authority boundaries and cross-tenant lookup risks.
  - Added safe-denial examples and Problem Details non-leakage guardrails.
  - Added `TODO(reference-pending): <field-or-decision>` placeholder convention.
  - Added secret-shaped field validation and non-secret opaque credential-reference guidance.
- Findings deferred:
  - Runtime authorization behavior, provider token exchange, provider adapters, live provider validation, SDK/CLI/MCP/UI consumers, final parity oracle rows, CI gates, and provider behavior tests remain downstream work.
  - Exact unresolved Story 1.5/1.6/C3/C4/S-2/C6 values remain reference-pending rather than invented in this story.
- Final recommendation: ready-for-dev

## Advanced Elicitation

- Date/time: 2026-05-11T08:04:18Z
- Selected story key: `1-7-author-tenant-folder-provider-and-repository-binding-contract-groups`
- Command/skill invocation used: `/bmad-advanced-elicitation 1-7-author-tenant-folder-provider-and-repository-binding-contract-groups`
- Batch 1 method names:
  - Red Team vs Blue Team
  - Security Audit Personas
  - Failure Mode Analysis
  - Self-Consistency Validation
  - Critique and Refine
- Reshuffled Batch 2 method names:
  - Pre-mortem Analysis
  - Architecture Decision Records
  - Comparative Analysis Matrix
  - Socratic Questioning
  - Occam's Razor Application
- Findings summary:
  - The story already had strong contract-only boundaries, but validation could still pass while accidentally adding a downstream operation unless operation IDs and path prefixes are checked against an explicit allow-list.
  - Safe-denial requirements needed fixture-level coverage for unauthorized, absent, cross-tenant, missing binding, and missing branch/ref policy cases so implementation does not leak resource existence through variant examples.
  - Provider readiness needed clearer audience partitioning so authorized operator diagnostics can exist without contaminating consumer-visible safe-denial responses.
- Changes applied:
  - Added externally indistinguishable safe-denial envelope guidance for folder lifecycle and access operations.
  - Added provider readiness guidance separating consumer-visible safe denials from authorized sanitized diagnostics.
  - Strengthened offline validation with explicit operation allow-list checks, provider diagnostic audience partitioning, and fixture assertions for safe-denial parity.
  - Added Dev Notes clarifying opaque provider/repository/branch-ref identifiers and audience-scoped readiness evidence.
  - Updated the Change Log with the advanced-elicitation hardening pass.
- Findings deferred:
  - Exact operator diagnostic authorization model remains downstream implementation and operations-console work.
  - Any final operation inventory changes frozen by Story 1.5 or Story 1.6 must be consumed by the implementation agent rather than invented here.
  - Runtime authorization behavior, provider adapters, generated SDKs, CLI/MCP/UI consumers, final parity oracle rows, and CI gates remain out of scope.
- Final recommendation: ready-for-dev

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-12: Loaded BMad workflow customization, project config, sprint status, project context, Story 1.7, Story 1.6 OpenAPI foundation, extension vocabulary, prerequisite docs, and existing contract tests.
- 2026-05-12: Confirmed story is a fresh implementation with no Senior Developer Review continuation section.

### Implementation Plan

- Add focused failing contract validation for the Story 1.7 operation allow-list, OpenAPI metadata, tenant-authority boundaries, synthetic examples, safe denials, and negative scope.
- Author only the tenant/folder/provider/repository/branch-ref OpenAPI contract groups and shared schemas needed by those operations.
- Add concise downstream contract notes for Stories 1.8 through 1.11 and record deferred decisions.
- Run the focused contract tests and `dotnet build Hexalith.Folders.slnx`; mark tasks complete only after validations pass.

### Completion Notes List

- Authored 15 Story 1.7 OpenAPI operations for folder lifecycle/access, provider binding/readiness/support evidence, repository-backed folder creation, repository binding, and branch/ref policy.
- Added provider-neutral capability/readiness schemas, non-secret opaque credential reference modeling, canonical provider/repository error categories, operation metadata, synthetic examples, and externally indistinguishable safe-denial examples.
- Added focused offline contract validation for operation allow-list, `$ref` resolution, required `x-hexalith-*` metadata, idempotency/read-consistency separation, tenant-authority boundaries, secret-shaped fields, provider diagnostic audience partitioning, and negative scope.
- Added downstream contract notes for Stories 1.8 through 1.11 and deferred C3/C4/S-2/C6/Story 1.5/1.6 decisions.
- Updated scaffold regression tests for Story 1.7 operation paths and aligned README submodule setup with the current root-level submodule inventory.
- Validation passed: `dotnet test tests\Hexalith.Folders.Contracts.Tests\Hexalith.Folders.Contracts.Tests.csproj --no-restore`; `dotnet build Hexalith.Folders.slnx --no-restore`; `dotnet test Hexalith.Folders.slnx --no-restore`.

### File List

- README.md
- _bmad-output/implementation-artifacts/1-7-author-tenant-folder-provider-and-repository-binding-contract-groups.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- docs/contract/tenant-folder-provider-repository-contract-groups.md
- src/Hexalith.Folders.Contracts/openapi/extensions/hexalith-extension-vocabulary.yaml
- src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml
- tests/Hexalith.Folders.Contracts.Tests/OpenApi/ContractSpineFoundationTests.cs
- tests/Hexalith.Folders.Contracts.Tests/OpenApi/TenantFolderProviderContractGroupTests.cs
- tests/Hexalith.Folders.Testing.Tests/ContractRulesArtifactTests.cs
- tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs
- tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs
