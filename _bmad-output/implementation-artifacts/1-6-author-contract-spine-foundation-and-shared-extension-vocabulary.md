# Story 1.6: Author Contract Spine foundation and shared extension vocabulary

Status: ready-for-dev

Created: 2026-05-10

## Story

As an API consumer and adapter implementer,
I want shared OpenAPI conventions and Hexalith extensions defined,
so that every capability group uses the same contract language.

## Acceptance Criteria

1. Given the Phase 0.5 decisions are complete, when the Contract Spine foundation is authored, then `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` exists as an OpenAPI 3.1 document with canonical `info`, `servers`, tags for all PRD capability groups, OIDC bearer security, shared parameters, shared headers, shared responses, and no operation group paths beyond the foundation placeholders needed to validate document shape.
2. Given architecture C0 and A-1, when shared extension vocabulary is defined, then each required `x-hexalith-*` extension is documented and represented under `src/Hexalith.Folders.Contracts/openapi/extensions/`: `x-hexalith-idempotency-key`, `x-hexalith-idempotency-equivalence`, `x-hexalith-idempotency-ttl-tier`, `x-hexalith-correlation`, `x-hexalith-lifecycle-states`, `x-hexalith-parity-dimensions`, `x-hexalith-audit-metadata-keys`, and `x-hexalith-sensitive-metadata-tier`; the documentation defines each extension's allowed OpenAPI location, value type or JSON Schema shape, required/optional status, and one sanitized example.
3. Given the shared conventions are canonical, when the OpenAPI foundation is inspected, then authentication, authoritative tenant context, idempotency, correlation, task identity, pagination, freshness, Problem Details aligned to RFC 9457, lifecycle states, audit metadata, sensitive metadata classification, and safe denial behavior are expressed once as reusable components or extension schemas, with no request payload, query parameter, or client-controlled header defining tenant authority.
4. Given Story 1.5 finalized idempotency and parity rules, when the foundation is authored, then the spine vocabulary can encode lexicographically ordered idempotency-equivalence fields, the two idempotency TTL tiers (`mutation = 24h`, `commit = retention-period(C3)`), read-consistency classes, parity dimensions, CLI exit code and MCP failure-kind mappings, and per-operation completeness expectations without generating `tests/fixtures/parity-contract.yaml`.
5. Given Story 1.4 owns Phase 0.5 decisions, when C3/C4/S-2/C6 inputs are consumed, then the spine uses the documented values if present and records any missing prerequisite as an explicit blocker note without inventing retention durations, input limits, issuer/audience values, or state transitions.
6. Given downstream stories will add operation groups, when this story is complete, then tenant/folder/provider/repository, workspace/lock, file/context, commit/status, audit, and operations-console operations remain deferred to Stories 1.7 through 1.11 except for shared schemas and vocabulary required by every group.
7. Given `Hexalith.Folders.Contracts` is behavior-free, when files are added, then no domain behavior, EventStore references, REST handlers, SDK generated output, CLI commands, MCP tools, worker code, or provider adapter code are implemented in this story.
8. Given future generator and drift gates depend on the foundation, when verification runs, then an OpenAPI-aware offline validator proves the OpenAPI document and extension schema files parse successfully as OpenAPI 3.1, all shared component references resolve, and verification succeeds without Dapr sidecars, Aspire, Keycloak, Redis, GitHub, Forgejo, provider credentials, tenant data, production secrets, network calls, or nested-submodule initialization.
9. Given the foundation must remain a vocabulary story, when verification scans `x-hexalith-*` usage, then the discovered extension allowlist is exactly `x-hexalith-idempotency-key`, `x-hexalith-idempotency-equivalence`, `x-hexalith-idempotency-ttl-tier`, `x-hexalith-correlation`, `x-hexalith-lifecycle-states`, `x-hexalith-parity-dimensions`, `x-hexalith-audit-metadata-keys`, and `x-hexalith-sensitive-metadata-tier`; `x-hexalith-parity-dimensions` defines reusable vocabulary shape only and does not create endpoint parity assertions, provider matrices, or parity rows.
10. Given examples are copied into downstream clients and docs, when any OpenAPI, extension, or foundation-note example is added, then it uses synthetic opaque placeholders only and contains no real paths, provider identifiers, tenant IDs, credential-shaped values, provider tokens, file content snippets, diffs, production URLs, or unauthorized resource hints.
11. Given generators and validators will consume the extension vocabulary, when extension files and foundation notes are authored, then each required `x-hexalith-*` extension has exactly one canonical machine-readable definition with stable field names, and any human-readable notes link to or summarize that definition without redefining conflicting shapes.

## Tasks / Subtasks

- [ ] Confirm prerequisites and preserve scope boundaries. (AC: 1, 5, 6, 7)
  - [ ] Inspect whether `docs/exit-criteria/c3-retention.md`, `docs/exit-criteria/c4-input-limits.md`, S-2 deployment configuration notes, and the C6 state matrix evidence exist before authoring bounded values.
  - [ ] Inspect `docs/contract/idempotency-and-parity-rules.md`, `tests/fixtures/parity-contract.schema.json`, `tests/fixtures/idempotency-encoding-corpus.json`, and `tests/fixtures/previous-spine.yaml` if they exist; treat missing files as prerequisite drift to record, not permission to widen scope.
  - [ ] Record missing prior-story values as `TODO/reference-pending` only in comments, extension documentation, or downstream notes; do not encode them as normative OpenAPI enum values or extension values.
  - [ ] Do not initialize or update nested submodules. Root-level submodules may be used only as read-only references unless the user explicitly asks otherwise.
  - [ ] Do not implement operation groups owned by Stories 1.7-1.11. This story creates the foundation, reusable components, and extension vocabulary only.
- [ ] Create the Contract Spine OpenAPI foundation. (AC: 1, 3, 8)
  - [ ] Create `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`.
  - [ ] Set `openapi: 3.1.0`; use canonical `info` metadata for Hexalith.Folders v1; define URL-versioned `/api/v1` server guidance without environment-specific production URLs.
  - [ ] Define tags for the PRD capability groups: provider-readiness, folders, workspaces, files, commits, query-status, audit, ops-console, and context-queries.
  - [ ] Define the security scheme as HTTP bearer JWT or OpenID Connect according to available S-2 evidence; do not hard-code unresolved issuer or audience values, add authentication middleware, add auth handlers, add policy classes, add validators, or add runtime authentication packages.
  - [ ] Use camelCase JSON names, ISO-8601 UTC date-time formats, lowercase hyphen-delimited REST path conventions, and path parameters such as `{folderId}` where later operations will need them.
  - [ ] Prefer `paths: {}` if the selected validator accepts an empty OpenAPI 3.1 Paths Object; use a clearly marked foundation-only placeholder only when required by local tooling. Do not add real operation paths whose schemas belong to later stories.
- [ ] Define shared OpenAPI components. (AC: 1, 3, 8)
  - [ ] Limit shared components to reusable OpenAPI primitives: security schemes, standard headers, Problem Details, error response envelopes, parameter/header definitions, pagination/freshness metadata, and extension vocabulary scaffolding; defer concrete operation/resource schemas to later stories.
  - [ ] Add reusable parameters and headers for `Idempotency-Key`, `X-Correlation-Id`, `X-Hexalith-Task-Id`, `X-Hexalith-Freshness`, and `X-Hexalith-Retry-As`.
  - [ ] Add shared pagination/filtering conventions for bounded query families without inventing C4 values; reference C4 decisions when present.
  - [ ] Add shared response components for accepted command, projected status, paged results, freshness metadata, safe authorization denial, validation failure, idempotency conflict, and reconciliation-required outcomes.
  - [ ] Add an RFC 9457-compatible Problem Details schema that preserves standard Problem Details members such as `type`, `title`, `status`, `detail`, and `instance` where applicable, and adds required Hexalith canonical fields: `category`, `code`, `message`, `correlationId`, `retryable`, `clientAction`, and `details`.
  - [ ] Add shared enums or schema anchors for lifecycle states, read-consistency classes, operator-disposition labels, sensitive metadata tiers, idempotency TTL tiers, canonical error categories, CLI exit codes, and MCP failure kinds.
  - [ ] Ensure examples use synthetic metadata only: no file contents, diffs, provider tokens, credential material, secrets, production issuer URLs, tenant data, or unauthorized resource hints.
- [ ] Author extension vocabulary schemas. (AC: 2, 3, 4, 8)
  - [ ] Create one schema or documentation file per required extension under `src/Hexalith.Folders.Contracts/openapi/extensions/`, or a single indexed extension vocabulary file if that better fits the toolchain.
  - [ ] Pick one canonical machine-readable vocabulary representation for the eight extensions; if supporting Markdown notes are added, keep them non-authoritative and point back to the canonical schema or index.
  - [ ] For every extension, document allowed OpenAPI locations, value type or JSON Schema shape, required/optional status, one sanitized example, and whether missing prior-story values must remain reference-pending.
  - [ ] Define `x-hexalith-idempotency-key` for mutating command requirements, adapter sourcing behavior, malformed/missing-key handling, and non-use on queries.
  - [ ] Define `x-hexalith-idempotency-equivalence` as an ordered array of semantic field paths; require lexicographic order and tenant-scoped equivalence.
  - [ ] Define `x-hexalith-idempotency-ttl-tier` with only `mutation` and `commit`; do not introduce per-command TTL knobs.
  - [ ] Define `x-hexalith-correlation` for `X-Correlation-Id`, causation linkage, and `X-Hexalith-Task-Id` where task-scoped behavior applies.
  - [ ] Define `x-hexalith-lifecycle-states` for workspace/task states, terminal states, retryability, and operator-disposition labels from C6.
  - [ ] Define `x-hexalith-parity-dimensions` with transport parity and behavioral parity columns required by C13, including adapter-specific pre-SDK and post-SDK behavior.
  - [ ] Keep `x-hexalith-parity-dimensions` to reusable vocabulary and metadata shape only; do not add endpoint parity assertions, provider matrices, status rows, or `tests/fixtures/parity-contract.yaml`.
  - [ ] Define `x-hexalith-audit-metadata-keys` as metadata-only key declarations, including allowed/denied operation evidence, correlation/task IDs, provider references, and sensitive metadata classification.
  - [ ] Define `x-hexalith-sensitive-metadata-tier` using the C9 default policy: paths, repository names, branch names, and commit messages are `tenant-sensitive` unless a stricter tenant policy applies.
- [ ] Encode Story 1.5 idempotency and adapter parity decisions into reusable vocabulary. (AC: 4)
  - [ ] Ensure mutating-operation completeness can later fail when `idempotency_key_rule` or equivalence fields are missing.
  - [ ] Ensure query-operation completeness can later fail when `read_consistency_class` is missing.
  - [ ] Preserve SDK behavior: caller-provided idempotency key or DI provider, no SDK auto-generation; correlation may be caller-provided or generated by SDK provider; task ID is caller-provided for task-scoped operations.
  - [ ] Preserve CLI behavior: `--idempotency-key` or `--allow-auto-key`, `--correlation-id`, required `--task-id` for task-scoped commands, credential precedence, and canonical exit codes 0, 64-75, and 1.
  - [ ] Preserve MCP behavior: required `idempotencyKey` for mutating tools, optional `correlationId`, required `taskId` for task-scoped tools, and canonical failure kinds.
  - [ ] Keep generated `ComputeIdempotencyHash()` helpers, NSwag template customization, parity-oracle rows, CLI command syntax, MCP tool envelopes, and runtime idempotency persistence deferred to later stories.
- [ ] Add focused foundation verification. (AC: 2, 3, 8)
  - [ ] Add a lightweight validation script or test in the existing scaffold location if available; if the scaffold is absent, document the exact missing prerequisite and provide the smallest parseable artifact check possible.
  - [ ] Verify `hexalith.folders.v1.yaml` parses as OpenAPI/YAML, declares `openapi: 3.1.0`, and has zero unresolved `$ref` targets.
  - [ ] Enumerate every `x-hexalith-*` key in the OpenAPI tree and fail unless the discovered set exactly matches the required eight extension names.
  - [ ] Verify required shared headers, Problem Details fields, idempotency TTL tiers, read-consistency classes, CLI exit codes, and MCP failure kinds are present.
  - [ ] Verify shared component `$ref` targets resolve.
  - [ ] Verify no request schema, query parameter, or client-controlled header defines tenant authority; tenant context may appear only as auth/envelope-derived documentation or non-authoritative correlation metadata.
  - [ ] Verify examples and local fixtures are synthetic opaque placeholders and contain no real paths, provider identifiers, tenant IDs, credential-shaped values, provider tokens, file contents, diffs, production URLs, or unauthorized resource hints.
  - [ ] Verify extension definitions are not duplicated with conflicting value shapes across the OpenAPI file, extension vocabulary files, and foundation notes.
  - [ ] Verify negative-scope guardrails: no SDK generated output, REST handlers/controllers, CLI commands, MCP tools, domain aggregate behavior, EventStore/provider calls inside Contracts, runtime auth implementation, CI workflow gates, final `parity-contract.yaml`, operation-group endpoints, or nested-submodule initialization were added by this story.
- [ ] Record implementation notes for downstream stories. (AC: 5, 6)
  - [ ] Add a short foundation note near the OpenAPI file or in `docs/contract/` that tells Stories 1.7-1.11 how to consume shared components and extension vocabulary.
  - [ ] Include downstream authoring rules for unique stable `operationId` values, tenant-authority exclusion from client-controlled inputs, reusable `$ref` usage, idempotency annotation requirements for mutating operations, and read-consistency annotation requirements for query operations.
  - [ ] List any missing Phase 0.5 prerequisites with exact file paths and affected OpenAPI fields.
  - [ ] List downstream owners: Story 1.7 tenant/folder/provider/repository, Story 1.8 workspace/lock, Story 1.9 file/context, Story 1.10 commit/status, Story 1.11 audit/ops-console, Story 1.12 NSwag SDK generation, Story 1.13 parity oracle, Story 1.14+ CI gates.

## Dev Notes

### Scope Boundaries

- This story creates the OpenAPI 3.1 foundation and the reusable `x-hexalith-*` extension vocabulary. It must not become operation-group implementation.
- Expected primary new files:

```text
src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml
src/Hexalith.Folders.Contracts/openapi/extensions/
```

- Optional supporting documentation, if needed:

```text
docs/contract/contract-spine-foundation.md
```

- Do not add `src/Hexalith.Folders.Client/Generated/`, NSwag configuration, generated hash helpers, REST endpoint handlers, CLI commands, MCP tools, domain aggregate behavior, worker behavior, provider adapters, final `tests/fixtures/parity-contract.yaml`, or CI workflow gates in this story.
- Do not add operation paths for the capability groups unless they are non-functional placeholders required only to keep tooling happy. Operation groups belong to Stories 1.7 through 1.11.
- Keep `Hexalith.Folders.Contracts` behavior-free: DTOs, identity value objects, OpenAPI/contract artifacts, and contract extensions only. No references to Hexalith.EventStore, Hexalith.Tenants, Server, Client, CLI, MCP, UI, Workers, or domain behavior.
- File ownership is limited to `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`, files under `src/Hexalith.Folders.Contracts/openapi/extensions/`, and optional `docs/contract/contract-spine-foundation.md`. C# changes outside project metadata or static-content inclusion are out of scope.

### Current Repository State

- At story-creation time, the root working copy contains planning artifacts and sibling submodule directories, but no root `src/` or `tests/` tree was visible. The dev agent should create only the narrow contract paths required for this story if Story 1.1 has not yet materialized them.
- `docs/exit-criteria/` and `docs/contract/` were not visible in the root working copy at story-creation time. If missing during implementation, create only the paths needed for notes or record prerequisite drift; do not fabricate C3/C4 values.
- Existing uncommitted process-note changes were present before this story was created. They are unrelated to Story 1.6 and must not be reverted.

### Contract Foundation Requirements

- The Contract Spine is `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`.
- The architecture pins OpenAPI 3.1 for v1. Do not switch to OpenAPI 3.2, TypeSpec, code-first OpenAPI, or a separate REST source of truth without an explicit architecture update.
- The external REST surface is URL-versioned `/api/v1/...`; server-emitted OpenAPI will later be validated against this Contract Spine as a blocking gate.
- `operationId` values introduced by later stories must be unique and stable because SDK generation and the C13 parity oracle depend on them.
- JSON and schema conventions: camelCase field names, ISO-8601 UTC timestamps, string enums, NFC-normalized forward-slash workspace paths, opaque ULID identifiers, and metadata-only examples.
- REST path conventions: lowercase hyphen-delimited segments, plural collection nouns, capability-group prefixes, and OpenAPI path parameters in camelCase such as `{folderId}`.

### Shared Extension Vocabulary

The vocabulary must cover these extensions exactly:

| Extension | Purpose |
| --- | --- |
| `x-hexalith-idempotency-key` | Declares mutating-operation key requirement, adapter sourcing rules, missing/malformed-key behavior, and non-use on queries. |
| `x-hexalith-idempotency-equivalence` | Declares lexicographically ordered field paths for tenant-scoped semantic equivalence. |
| `x-hexalith-idempotency-ttl-tier` | Declares `mutation` or `commit` only; mutation is 24h, commit inherits C3 retention. |
| `x-hexalith-correlation` | Declares correlation ID, causation linkage, and task ID propagation requirements. |
| `x-hexalith-lifecycle-states` | Declares lifecycle states, terminal states, retryability, and operator-disposition labels. |
| `x-hexalith-parity-dimensions` | Declares transport and behavioral parity dimensions used by REST, SDK, CLI, MCP, and C13. |
| `x-hexalith-audit-metadata-keys` | Declares metadata-only audit evidence keys and safe denial evidence. |
| `x-hexalith-sensitive-metadata-tier` | Declares sensitive metadata classification such as `tenant-sensitive` and stricter tenant overrides. |

Specification extensions are valid OpenAPI patterned fields when prefixed with `x-`. Avoid reserved `x-oai-*` and `x-oas-*` prefixes.

Extension documentation must include allowed placement and shape:

| Extension | Allowed OpenAPI locations | Value shape |
| --- | --- | --- |
| `x-hexalith-idempotency-key` | Operation Object for future mutating commands; extension vocabulary docs. | Object describing requirement, adapter sourcing, and missing/malformed behavior. |
| `x-hexalith-idempotency-equivalence` | Operation Object for future mutating commands; extension vocabulary docs. | Lexicographically ordered array of semantic field paths; tenant-scoped. |
| `x-hexalith-idempotency-ttl-tier` | Operation Object for future mutating commands; extension vocabulary docs. | Enum limited to `mutation` and `commit`; unresolved C3-backed details remain reference-pending, not invented values. |
| `x-hexalith-correlation` | Operation Object, reusable header/parameter components, response metadata schemas, and extension vocabulary docs. | Object describing correlation ID, causation, and task ID propagation. |
| `x-hexalith-lifecycle-states` | Reusable schema/component definitions, future operation metadata, and extension vocabulary docs. | Object or array describing states, terminal states, retryability, and operator-disposition labels without implementing state transitions. |
| `x-hexalith-parity-dimensions` | Operation Object for future operations and extension vocabulary docs. | Array/object describing reusable transport and behavioral parity dimensions only; no parity rows or endpoint assertions. |
| `x-hexalith-audit-metadata-keys` | Operation Object, reusable audit metadata schemas, and extension vocabulary docs. | Array of metadata-only audit key names and sensitivity notes. |
| `x-hexalith-sensitive-metadata-tier` | Schema Object, Property Schema Object, future operation metadata, and extension vocabulary docs. | Enum or object using approved tiers; synthetic examples only. |

### Shared Component Requirements

- Authentication/security: define bearer JWT or OpenID Connect security without hard-coding unresolved issuer/audience values. Tenant authority comes from auth context and EventStore envelope, not request payload, query parameter, or client-controlled header.
- Headers: define `Idempotency-Key`, `X-Correlation-Id`, `X-Hexalith-Task-Id`, `X-Hexalith-Freshness`, and `X-Hexalith-Retry-As`.
- Errors: use RFC 9457 Problem Details (`application/problem+json`) with canonical fields `category`, `code`, `message`, `correlationId`, `retryable`, `clientAction`, and `details`.
- Idempotency: every mutating command later carries `Idempotency-Key`; non-mutating queries do not. Same key plus equivalent payload returns the same logical result; same key plus different payload returns `idempotency_conflict`.
- Correlation: `X-Correlation-Id` and `X-Hexalith-Task-Id` propagate across REST, SDK, CLI, MCP, EventStore envelopes, projections, audit, logs, and traces.
- Lifecycle states: include architecture C6 states and operator-disposition labels. Unlisted state/event pairs reject with `state_transition_invalid` and leave state unchanged.
- Read consistency: support `snapshot-per-task`, `read-your-writes`, and `eventually-consistent`.
- Sensitive metadata: paths, repository names, branch names, and commit messages default to `tenant-sensitive`. No file contents, diffs, secrets, raw provider tokens, credential material, generated context payloads, or unauthorized resource existence in examples or schemas.
- Missing prior-story values: C3/C4/S-2/C6 values that are absent or unresolved must be recorded as `TODO/reference-pending` in docs or comments only. Do not turn unresolved values into normative OpenAPI enum entries, extension values, default limits, issuer/audience examples, or lifecycle state behavior.

### Previous Story Intelligence

- Story 1.1 establishes the solution scaffold and dependency direction. If the scaffold is still absent, this story may create only the narrow contract directory paths it owns.
- Story 1.2 establishes root configuration and forbids recursive nested-submodule initialization unless explicitly requested.
- Story 1.3 seeds normative fixtures: audit leakage corpus, parity schema, previous spine, idempotency encoding corpus, `tests/load`, and artifact templates. This story consumes those fixtures only for foundation validation expectations.
- Story 1.4 owns C3 retention, C4 input limits, S-2 OIDC parameters, and C6 transition mapping. Story 1.6 must not invent any missing value.
- Story 1.5 defines operation inventory, idempotency equivalence, adapter parity dimensions, parity schema expectations, and encoding corpus rules. Story 1.6 translates that rule vocabulary into OpenAPI extension semantics, but does not generate the final parity oracle or SDK helper implementation.
- Recent story workflow has repeatedly hardened stories after creation. If a party-mode or advanced-elicitation pass is run later, preserve this story's scope boundaries and record trace evidence in the story rather than silently expanding implementation.

### Technical Specifics Checked During Story Creation

- OpenAPI 3.1 supports Specification Extensions as `x-` prefixed fields, and schemas align with JSON Schema 2020-12. Use that capability for Hexalith metadata instead of inventing sidecar-only metadata.
- OpenAPI Operation Objects require unique `operationId` values when operations are added later; the foundation should make this an explicit downstream rule.
- OpenAPI Components can hold reusable schemas, parameters, responses, and security schemes. Put shared headers, error shapes, freshness metadata, and extension support in reusable components rather than duplicating them in each future operation group.
- NSwag documentation shows C# client generation from OpenAPI documents and custom document processors/templates can read or inject extension metadata through `ExtensionData`. Later Story 1.12 should use this for `ComputeIdempotencyHash()` generation; this story should only keep the extension data precise and generator-friendly.

### Testing Guidance

- Prefer a small deterministic validator over broad build work if the scaffold is not present. The validator should parse YAML/OpenAPI, assert required extension names, assert shared components, and reject obvious dangling `$ref` targets.
- Validation should be OpenAPI-aware rather than grep-only: parse YAML, assert OpenAPI 3.1, resolve component `$ref` targets, enumerate the exact `x-hexalith-*` allowlist, and run entirely offline from repository-local files.
- If the full solution exists by implementation time, run `dotnet restore Hexalith.Folders.slnx` and `dotnet build Hexalith.Folders.slnx` from the repository root after the focused contract validation.
- Verification must not require Aspire, Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, provider credentials, tenant data, production secrets, or initialized nested submodules.
- Include a negative-scope check that fails if this story adds generated SDK output, runtime handlers, adapter commands/tools, final parity rows, or domain behavior.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 1.6: Author Contract Spine foundation and shared extension vocabulary`
- `_bmad-output/planning-artifacts/epics.md#Contract Spine (Phase 1 - C0)`
- `_bmad-output/planning-artifacts/architecture.md#Architecture Exit Criteria - Targets to Resolve`
- `_bmad-output/planning-artifacts/architecture.md#API & Communication Patterns`
- `_bmad-output/planning-artifacts/architecture.md#Spine Authoring Checklist`
- `_bmad-output/planning-artifacts/architecture.md#Structure Patterns`
- `_bmad-output/planning-artifacts/architecture.md#Enforcement Guidelines`
- `_bmad-output/planning-artifacts/prd.md#Endpoint Specifications`
- `_bmad-output/planning-artifacts/prd.md#Command and Query Contract`
- `_bmad-output/planning-artifacts/prd.md#Error Codes`
- `_bmad-output/planning-artifacts/prd.md#Contract and Quality Gates`
- `_bmad-output/implementation-artifacts/1-1-establish-a-consumer-buildable-module-scaffold.md`
- `_bmad-output/implementation-artifacts/1-2-establish-root-configuration-and-submodule-policy.md`
- `_bmad-output/implementation-artifacts/1-3-seed-minimally-valid-normative-fixtures.md`
- `_bmad-output/implementation-artifacts/1-4-author-phase-0-5-pre-spine-workshop-deliverables.md`
- `_bmad-output/implementation-artifacts/1-5-finalize-idempotency-equivalence-and-adapter-parity-rules.md`
- `_bmad-output/project-context.md`
- `AGENTS.md#Git Submodules`

## Project Structure Notes

- `src/Hexalith.Folders.Contracts/openapi/` is the canonical owner for the Contract Spine and extension vocabulary.
- `docs/contract/` may hold human-readable contract notes if needed, but the OpenAPI foundation remains the source of truth.
- `tests/fixtures/parity-contract.yaml` remains downstream Story 1.13 work. `tests/fixtures/parity-contract.schema.json` may be consumed or referenced, but this story should not generate final oracle rows.
- `Hexalith.Folders.Client/Generated/` remains downstream Story 1.12 work and must not be hand-edited.

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-11 | Advanced elicitation clarified canonical extension definitions, empty-path preference, RFC 9457 fields, validation checks, and downstream authoring rules. | Codex |
| 2026-05-10 | Party-mode review applied extension placement, tenant-authority, validation, and negative-scope guardrails. | Codex |
| 2026-05-10 | Created ready-for-dev story through `bmad-create-story` workflow. | Codex |

## Dev Agent Record

### Agent Model Used

TBD by dev-story agent

### Debug Log References

### Completion Notes List

### File List

## Party-Mode Review

- Date/time: 2026-05-10T23:03:39.1143628+02:00
- Selected story key: `1-6-author-contract-spine-foundation-and-shared-extension-vocabulary`
- Command/skill invocation used: `/bmad-party-mode 1-6-author-contract-spine-foundation-and-shared-extension-vocabulary; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), Paige (Technical Writer)
- Findings summary:
  - Extension vocabulary needed explicit allowed OpenAPI locations, value shapes, required/optional expectations, and sanitized examples so later NSwag and validation work can consume it consistently.
  - Tenant authority needed a story-level prohibition against request payload, query parameter, or client-controlled header authority.
  - Shared components and security scope needed tighter bounds to avoid operation schemas, runtime authentication code, generated clients, adapters, or domain behavior.
  - Validation needed OpenAPI-aware offline parsing, `$ref` resolution, exact extension allowlist checks, tenant-boundary checks, and negative-scope checks.
  - Missing C3/C4/S-2/C6 values needed a non-invention rule so unresolved values remain reference-pending instead of becoming normative contract values.
- Changes applied:
  - Added AC9 and AC10 for exact `x-hexalith-*` allowlist validation, parity-vocabulary-only scope, and synthetic example hygiene.
  - Strengthened AC2, AC3, and AC8 for extension placement/value documentation, RFC 9457 Problem Details, tenant-authority boundaries, OpenAPI-aware offline validation, and no network/service dependency.
  - Added task guidance for missing prerequisite handling, security/runtime boundaries, shared component limits, parity row exclusion, tenant-boundary verification, example hygiene verification, and negative-scope checks.
  - Added Dev Notes table defining allowed extension locations and value shapes.
- Findings deferred:
  - Concrete operation paths, operation-specific schemas, and operation-specific error/status mappings remain Stories 1.7-1.11.
  - NSwag configuration, generated SDK helpers, CLI/MCP wrappers, REST handlers, CI gates, and parity oracle rows remain downstream stories.
  - Final unresolved C3/C4/S-2/C6 values remain dependent on the Phase 0.5 decision artifacts.
- Final recommendation: ready-for-dev

## Advanced Elicitation

- Date/time: 2026-05-11T04:04:23.7473573+02:00
- Selected story key: `1-6-author-contract-spine-foundation-and-shared-extension-vocabulary`
- Command/skill invocation used: `/bmad-advanced-elicitation 1-6-author-contract-spine-foundation-and-shared-extension-vocabulary`
- Batch 1 method names: Expert Panel Review; Security Audit Personas; Failure Mode Analysis; Self-Consistency Validation; Critique and Refine
- Reshuffled Batch 2 method names: First Principles Analysis; Pre-mortem Analysis; Graph of Thoughts; Socratic Questioning; Occam's Razor Application
- Findings summary:
  - Extension vocabulary needed a single canonical machine-readable source to avoid drift between OpenAPI, extension files, and foundation notes.
  - The OpenAPI foundation should prefer an empty Paths Object when tooling allows it so later operation-group stories do not inherit accidental placeholder operations.
  - Problem Details needed explicit preservation of RFC 9457 standard members in addition to Hexalith canonical fields.
  - Verification needed a duplicate/conflicting extension-shape check because exact allowlist scanning alone would not catch split-brain vocabulary definitions.
  - Downstream authoring notes needed to make operationId stability, tenant-authority exclusion, `$ref` reuse, idempotency annotations, and read-consistency annotations explicit for Stories 1.7-1.11.
- Changes applied:
  - Added AC11 requiring exactly one canonical machine-readable definition per required `x-hexalith-*` extension.
  - Clarified empty-path preference, RFC 9457 standard field expectations, and canonical vocabulary ownership in task guidance.
  - Added verification for conflicting duplicate extension definitions.
  - Added downstream authoring-note requirements for future operation groups.
- Findings deferred:
  - Final C3/C4/S-2/C6 values remain dependent on Phase 0.5 evidence and must stay reference-pending if missing.
  - Final security scheme flavor, validator implementation mechanism, and one-file-versus-per-extension vocabulary layout remain implementation choices bounded by available scaffold and tooling.
  - Operation-specific schemas, parity rows, SDK generation, runtime handlers, CLI/MCP tools, and CI gates remain downstream story work.
- Final recommendation: ready-for-dev
