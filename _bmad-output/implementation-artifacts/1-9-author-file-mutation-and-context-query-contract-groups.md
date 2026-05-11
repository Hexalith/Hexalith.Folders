# Story 1.9: Author file mutation and context query contract groups

Status: ready-for-dev

Created: 2026-05-11

## Story

As an API consumer and adapter implementer,
I want file mutation and context query operations represented in the Contract Spine,
so that file changes and read-only context access preserve the same policy boundaries across surfaces.

## Acceptance Criteria

1. Given workspace and lock contract groups from Story 1.8 exist or are explicitly reference-pending, when this story is complete, then `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` contains file mutation and context-query operation groups for add file, change file, remove file, file tree/listing, file metadata, search, glob, and bounded range-read operations, and includes extension-safe vocabulary for future semantic/RAG context-query families without implementing Memories integration in this story.
2. Given file mutations require a prepared workspace and a held task-scoped workspace lock, when `AddFile`, `ChangeFile`, and `RemoveFile` are authored, then each operation declares tenant/folder/task/workspace/path scope, lock requirement, operation identity, `Idempotency-Key`, `x-hexalith-idempotency-key`, lexicographically ordered `x-hexalith-idempotency-equivalence`, `x-hexalith-idempotency-ttl-tier: mutation`, required correlation/task metadata, audit metadata keys, canonical error categories, retry eligibility, and adapter parity dimensions.
3. Given file content transport is bimodal, when add/change file contracts are authored, then inline content and streaming upload shapes follow the D-9 decision: `PutFileInline` for content up to 256KB, `PutFileStream` for streaming larger content, `X-Hexalith-Retry-As: stream` on inline `413`, and no base64-only or external-content-reference model is introduced.
4. Given context queries are controlled workspace operations, when tree, metadata, search, glob, bounded range-read, or future semantic/RAG contracts are authored, then authorization order is tenant access, folder ACL, path policy, sensitivity classification, query/input limits, then query execution; the contract does not allow search-first/filter-later or semantic-retrieval-first/filter-later behavior.
5. Given context queries are non-mutating reads, when their Operation Objects are inspected, then they do not accept `Idempotency-Key`, declare read consistency, freshness and projection-lag behavior, pagination or result bounds where applicable, safe authorization-denial shape, redaction behavior, audit metadata keys, canonical error categories, correlation behavior, and parity dimensions.
6. Given path policy and input limits protect tenant data, when schemas and examples are authored, then workspace-root-relative paths use forward slashes and NFC-normalized metadata, traversal, symlink escape, reserved names, case collisions, binary/large-file policy, range limits, result limits, response budgets, query duration, truncation behavior, and included/excluded audit visibility are declared using approved C4 values or `TODO(reference-pending)` markers when approval state requires it.
7. Given responses and audit records must remain metadata-only unless authorized content is explicitly returned by bounded range reads, when examples, schemas, Problem Details, audit metadata, logs, and diagnostic fields are scanned, then they contain no file contents outside approved response bodies, no diffs, no generated context payloads, no provider tokens, no credential material, no production URLs, no local machine paths, no raw search text in audit examples, and no unauthorized resource-existence hints.
8. Given `RemoveFile` is a metadata-only file mutation, when removal schemas and audit examples are authored, then removed file contents, diffs, previous content samples, or provider payloads are never represented; only safe changed-path metadata, content hash references where applicable, and path policy classification are allowed.
9. Given file mutation may produce known provider/workspace failure or unknown provider outcome, when failure shapes are authored, then path validation failure, workspace locked, state transition invalid, file operation failed, provider failure known, provider outcome unknown, reconciliation required, read-model unavailable, input limit exceeded, response limit exceeded, query timeout, redacted, validation error, idempotency conflict, tenant access denied, folder ACL denied, not found, and internal error map to canonical RFC 9457 Problem Details without silent repair, duplicate writes, or unsafe retry.
10. Given this is a contract-group authoring story, when implementation is complete, then no runtime REST handlers, EventStore commands, domain aggregate behavior, provider adapters, Git/file-system side effects, generated SDK output, NSwag generation wiring, CLI commands, MCP tools, workers, UI pages, final parity-oracle rows, CI workflow gates, or nested-submodule initialization are added by this story.
11. Given adjacent stories own different capability groups, when this story is complete, then operation paths and schemas remain limited to file mutation and context-query concerns and do not implement workspace/lock operations, commit/status operations, audit timeline operations, operations-console projection queries, provider readiness, folder lifecycle, repository binding, or branch/ref policy except as references required by this story.
12. Given the Contract Spine is a mechanical source for SDK and parity work, when validation runs, then OpenAPI 3.1 parsing succeeds offline, all `$ref` targets resolve, operation IDs are unique and limited to this story's explicit allow-list, every operation has required `x-hexalith-*` metadata, mutating/read operations satisfy idempotency or read-consistency requirements, no client-controlled tenant authority exists, examples are synthetic and metadata-only, and negative-scope checks prove downstream runtime/adapter/generated artifacts were not added.

## Tasks / Subtasks

- [ ] Confirm prerequisites and preserve scope boundaries. (AC: 1, 2, 3, 10, 11)
  - [ ] Inspect Story 1.6 deliverables: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` and `src/Hexalith.Folders.Contracts/openapi/extensions/`. If absent, record prerequisite drift and create only the narrow file/context additions this story owns with `TODO(reference-pending)` markers where shared foundation values are missing.
  - [ ] Inspect Story 1.8 deliverables for workspace, lock, task, retry, state-transition, and authorization-revocation components before referencing them. If absent, reference stable planned component names and record the dependency as reference-pending instead of duplicating Story 1.8 scope.
  - [ ] Inspect `docs/contract/idempotency-and-parity-rules.md`, `tests/fixtures/parity-contract.schema.json`, `tests/fixtures/idempotency-encoding-corpus.json`, and `tests/fixtures/previous-spine.yaml`; treat missing files as prerequisite drift, not permission to invent policy.
  - [ ] Inspect `docs/exit-criteria/c4-input-limits.md`, `docs/exit-criteria/c6-transition-matrix-mapping.md`, and related C3/S-2 evidence if present; use approved values where binding and record reference-pending approval state where still proposed.
  - [ ] Do not initialize or update nested submodules. Use sibling modules only as read-only references unless explicitly directed otherwise.
  - [ ] Limit allowed story outputs to OpenAPI contract changes, existing Contract project static artifact inclusion if needed, synthetic examples, optional contract notes, and focused offline validation assets.
- [ ] Author file mutation contract operations. (AC: 1, 2, 3, 6, 7, 8, 9)
  - [ ] Add or update tags and paths for file add, change, and remove operations under `/api/v1/...` using lowercase hyphen-delimited path segments and camelCase path parameters such as `{folderId}` and `{workspaceId}`.
  - [ ] Define stable operation IDs for `AddFile`, `ChangeFile`, and `RemoveFile` unless Story 1.5 or Story 1.6 has already frozen different names; record any mapping in contract notes.
  - [ ] Represent file operation identity with a caller-visible `operationId` or operation reference in addition to `Idempotency-Key`; task ID and operation ID are required for mutating file operations.
  - [ ] Require a prepared workspace and a valid held lock for every mutation; represent lock failure, stale/expired lock, auth revocation, and `state_transition_invalid` as canonical metadata-only outcomes.
  - [ ] Declare idempotency equivalence using the Story 1.5 field lists: `AddFile`/`ChangeFile` include `content_hash_reference, file_operation_kind, operation_id, path_metadata, path_policy_class, task_id, tenant_id, workspace_id`; `RemoveFile` includes `file_operation_kind, operation_id, path_metadata, path_policy_class, task_id, tenant_id, workspace_id`.
  - [ ] Implement D-9 contract shapes only: inline small content, streaming larger content, content hash and byte length metadata, retry-as-stream response header, and synthetic examples. Do not add base64-only, raw diff, or external content reference behavior.
  - [ ] Treat `AddFile` and `ChangeFile` as business operation kinds and resolve their concrete OpenAPI transport operation IDs explicitly before authoring paths: `PutFileInline` for inline content at or below 262144 bytes and `PutFileStream` for larger streamed content unless a prerequisite story has already frozen different operation IDs. Record any mapping in contract notes.
  - [ ] Define `X-Hexalith-Retry-As` as a metadata-only `413 Payload Too Large` response header with enum value `stream`; examples must not reveal unauthorized file names, paths, content, diffs, provider payloads, or local paths.
  - [ ] Keep provider calls, Git writes, filesystem writes, aggregate event emission, workers, process managers, and generated SDK helpers deferred to Epic 4, Story 1.12, and later implementation stories.
- [ ] Author context-query contract operations. (AC: 1, 4, 5, 6, 7, 9)
  - [ ] Add non-mutating operations for file tree/listing, file metadata lookup, search, glob, and bounded range reads using stable operation IDs such as `ListFolderFiles`, `SearchFolderFiles`, and `ReadFileRange` unless prior stories froze different names.
  - [ ] Model context queries as policy-filtered metadata/content operations, not generic repository browsing or direct filesystem access.
  - [ ] Declare authorization order explicitly: tenant access, folder ACL, path policy, then query execution. Results must already be security-trimmed before ranking, summarization, snippets, truncation, or response shaping.
  - [ ] Represent authorization order as contract metadata or normative operation description, not as caller-controlled request fields. Query request bodies and parameters must not include tenant authority, ACL override, sensitivity override, policy bypass, or search-first/filter-later semantics.
  - [ ] Use `snapshot-per-task` read consistency for `ListFolderFiles`, `SearchFolderFiles`, and `ReadFileRange` per Story 1.5 unless a prior approved source changes it.
  - [ ] Declare freshness metadata, projection lag behavior, truncation flag, truncation reason, configured limit, actual count/bytes, elapsed milliseconds, and safe retry guidance where applicable.
  - [ ] Define bounded range reads as byte ranges with exact inclusive/exclusive offset semantics, maximum single range size, invalid/reversed range behavior, redacted/partial response metadata, and whether `206`, `416`, or canonical Problem Details applies for each boundary case.
  - [ ] Apply C4 proposed bounds as reference-pending or approved according to current artifact state: max requested paths 100, max tree entries 2000, max search/glob results 500, max single range bytes 262144, aggregate response bytes 1048576, and server-side query timeout 2000 ms.
  - [ ] Ensure denied, excluded, binary-disallowed, too-large, timeout, truncated, and missing-resource cases produce safe Problem Details or metadata-only response shapes without revealing unauthorized file, folder, task, path, or search-hit existence.
- [ ] Apply shared OpenAPI conventions consistently. (AC: 2, 3, 5, 6, 7, 12)
  - [ ] Reuse shared headers, parameters, Problem Details, freshness metadata, pagination/filtering conventions, lifecycle/state schemas, and extension vocabulary from Story 1.6 instead of duplicating incompatible shapes.
  - [ ] Use camelCase JSON properties, ISO-8601 UTC timestamps, string enums, opaque ULID identifiers, and forward-slash workspace-root-relative path metadata with no leading slash.
  - [ ] Ensure every mutating operation has `Idempotency-Key`, correlation metadata, task identity, audit metadata, and parity dimensions; ensure every non-mutating context query omits `Idempotency-Key`.
  - [ ] Ensure all examples are synthetic opaque placeholders and do not include real tenant IDs, repository URLs, branch names with sensitive values, local paths, provider identifiers, organization names, email addresses, file content snippets, diffs, generated context payloads, secrets, or unauthorized resource hints.
  - [ ] Use `TODO(reference-pending): <field-or-decision>` only for unresolved approved-source values, with exact source paths or decision owners when known.
- [ ] Add focused offline validation. (AC: 6, 7, 10, 12)
  - [ ] Add or update the smallest local validator or test that parses `hexalith.folders.v1.yaml` as OpenAPI 3.1 and resolves all local `$ref` targets without network access.
  - [ ] Keep the validation oracle contract-only: assert OpenAPI shape, headers, schemas, examples, Problem Details, `x-hexalith-*` metadata, authorization-order documentation, and extension vocabulary only; do not require runtime handlers, EventStore behavior, providers, workers, SDK generation, CLI/MCP, UI, CI gates, or filesystem/Git side effects.
  - [ ] Verify new operation IDs are unique, stable, and limited to this story's operation allow-list: `AddFile`, `ChangeFile`, `RemoveFile`, `ListFolderFiles`, `SearchFolderFiles`, `ReadFileRange`, plus any explicitly named file metadata/tree/glob operation added by this story.
  - [ ] Verify all new operations include required `x-hexalith-*` metadata and satisfy idempotency/read-consistency requirements by operation type.
  - [ ] Verify no request payload, query parameter, route segment, or client-controlled header defines authoritative tenant context.
  - [ ] Verify context-query operations do not define or accept `Idempotency-Key`; mutation operations require it and expose missing, malformed, and conflict cases through canonical metadata-only Problem Details.
  - [ ] Verify C4 limit metadata appears in schemas or reference-pending comments with approval state preserved.
  - [ ] Verify schema and example field names reject secret-shaped or credential-shaped terms such as `token`, `secret`, `credential`, `password`, `privateKey`, `accessToken`, and raw provider authorization material unless the field is an explicit non-secret opaque reference.
  - [ ] Verify examples and audit metadata exclude file contents, diffs, raw search text, generated context payloads, provider payloads, local paths, production URLs, and unauthorized resource-existence hints.
  - [ ] Verify bounded range examples cover minimum range, maximum allowed range, invalid reversed range, over-bound range, redacted or partial response metadata, and authorized content-only response bodies.
  - [ ] Verify safe-denial and canonical error coverage for validation failure, missing idempotency key, idempotency conflict, tenant access denied, folder ACL denied, path policy denied, sensitivity or C4 denial, safe not-found/denied response, range unsatisfiable, projection stale, and unsupported semantic/RAG extension.
  - [ ] Verify negative scope: no generated SDK files, NSwag generation wiring, REST handlers/controllers, CLI commands, MCP tools, domain aggregate behavior, provider adapters, workers, UI pages, final parity oracle rows, CI workflow gates, or nested-submodule initialization.
  - [ ] Run `dotnet build Hexalith.Folders.slnx` if the scaffold supports it after focused validation. If blocked by earlier scaffold state, record the exact prerequisite instead of expanding this story.
- [ ] Record downstream authoring notes. (AC: 10, 11)
  - [ ] Add a short note near the OpenAPI file or in `docs/contract/` explaining which file/context components Stories 1.10, 1.11, Epic 4, Epic 5, and Story 6.6 must reuse.
  - [ ] Record deferred owners for runtime file mutation behavior, path policy enforcement, context-query execution, semantic indexing, commit/status, audit timeline, operations-console projections, generated SDK helpers, parity oracle rows, and CI gates.
  - [ ] Record any unresolved C4 approval, Story 1.6 foundation, Story 1.8 workspace/lock, Story 1.5 operation naming, or C6 state metadata dependencies as explicit deferred decisions.

## Dev Notes

### Scope Boundaries

- This story authors Contract Spine operation groups for file mutation and context queries only.
- Contract-only output means OpenAPI contract declarations, synthetic examples, optional contract notes, and focused offline validation. Do not broaden this story into runtime behavior, generated consumers, provider integration, process managers, search indexing, semantic indexing, or CI workflow wiring.
- Memories-backed semantic indexing and RAG retrieval are downstream integration work. This story may define reusable query-family vocabulary and safe extension points only; it must not add Memories package references, workers, indexing projections, semantic index schemas, retrieval runtime behavior, or RAG response assembly.
- Primary files expected:

```text
src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml
src/Hexalith.Folders.Contracts/openapi/extensions/
```

- Optional supporting docs, only if needed:

```text
docs/contract/file-context-contract-groups.md
```

- Do not add `src/Hexalith.Folders.Client/Generated/`, NSwag configuration, server endpoints, EventStore commands, aggregate logic, provider adapters, Git or filesystem operations, CLI/MCP commands, workers, UI, parity rows, or CI gates.
- Do not add operation groups owned by adjacent stories:
  - Story 1.7: tenant, folder, provider, repository binding, repository creation, and branch/ref policy.
  - Story 1.8: workspace preparation, lock acquisition/release, lock inspection, retry eligibility, and state-transition evidence.
  - Story 1.10: commit, commit evidence, workspace status, task status, provider outcome, and reconciliation status.
  - Story 1.11: audit trail, operation timeline, readiness/status diagnostics, and operations-console projection queries.
- Runtime file mutation, path policy enforcement, context-query execution, reconciliation, and provider/workspace side effects belong to Epic 4 implementation stories.

### Current Repository State

- At story creation time, `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` was not present in the working tree.
- At story creation time, `docs/contract/idempotency-and-parity-rules.md`, `tests/fixtures/parity-contract.schema.json`, `tests/fixtures/idempotency-encoding-corpus.json`, `tests/fixtures/previous-spine.yaml`, and `docs/exit-criteria/c4-input-limits.md` were present.
- At story creation time, C4 values were marked "proposed workshop values - needs human decision before Phase 1 Contract Spine authoring"; implementation must preserve that approval state instead of silently treating proposed limits as final if no newer approval exists.
- The implementation agent must inspect the current repository state before editing because earlier ready stories may have completed after this story was created.

### Contract Group Requirements

- Use OpenAPI 3.1 at `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` as the single source of truth when the foundation exists.
- REST paths are URL-versioned under `/api/v1`, use lowercase hyphen-delimited segments, plural collection nouns where appropriate, and camelCase path parameters.
- Stable operation IDs matter because NSwag generation and the C13 parity oracle will consume them later.
- Tenant authority comes from auth context and EventStore envelopes. Payloads may include resource IDs to validate, but never authoritative tenant context.
- File paths in contracts are workspace-root-relative metadata, forward-slash separated, NFC-normalized, and never leading-slash local or absolute paths.
- File content is not event, audit, log, trace, metric, projection, or diagnostic data. Content may appear only in the explicitly authorized add/change request body or bounded range-read response body.
- `PutFileInline` handles small content at or below the 256KB boundary. `PutFileStream` handles larger content. SDK convenience upload remains Story 1.12 and must not be implemented here.
- Context queries cover tree/listing, metadata, search, glob, and bounded range reads. They do not expose unrestricted repository browsing, raw provider payloads, direct filesystem paths, or repair actions.
- Search and glob results are authorized and path-policy-filtered before result shaping. A contract that permits search-first/filter-later violates the tenant isolation invariant.
- Future semantic/RAG context queries must follow the same context-query authorization order: tenant access, folder ACL, path policy, sensitivity classification, C4 bounds, then retrieval. Folders remains the policy enforcement point; derived indexes such as Hexalith.Memories are never authoritative for tenant, folder, file, or authorization truth.

### Party-Mode Hardening Notes

- Operation placement must be explicit in the OpenAPI document. Use distinct file-mutation and context-query tags or path groups, keep paths under `/api/v1`, and record any prerequisite-driven operation ID mapping in `docs/contract/file-context-contract-groups.md` or an adjacent contract note.
- D-9 transport is contract-visible only. `PutFileInline` handles content at or below 262144 bytes before transport encoding; `PutFileStream` handles larger streamed content; inline `413` responses expose `X-Hexalith-Retry-As: stream` and metadata-only Problem Details.
- Mutating file operations require prepared-workspace and held-lock preconditions as headers, extension metadata, response descriptions, or canonical Problem Details. Do not define runtime lock coordination, workspace orchestration, EventStore command flow, provider behavior, Git behavior, or filesystem behavior in this story.
- Context queries MUST follow tenant access, folder ACL, path policy, sensitivity classification, C4 bounds, then query execution. Implementations MUST NOT search, glob, rank, summarize, retrieve, or assemble semantic context first and filter later.
- Safe-denial responses must use stable machine-readable codes and generic localizable messages. They must not reveal folder, workspace, lock, task, path, excluded-path, search-hit, provider-state, local-path, or unauthorized resource existence.
- Future semantic/RAG vocabulary is reserved extension vocabulary for downstream stories. It must not imply embedding generation, vector-search runtime, Memories integration, prompt assembly, RAG workers, provider adapters, or generated context payloads in this story.
- Parity dimensions for every file/context operation are authorization order, correlation, canonical errors, read consistency or idempotency, freshness/projection lag where applicable, redaction/safe denial, audit metadata, localization-safe Problem Details, and synthetic examples.

### Operation Inventory Seed

Use the operation names below as a starting point unless Story 1.5, Story 1.6, or an already-authored Contract Spine has frozen different names. If names differ, keep the OpenAPI `operationId` stable and record the mapping.

| Operation | Type | Required metadata |
| --- | --- | --- |
| `AddFile` | Mutating command | idempotency, operation ID, task ID, workspace/lock scope, path metadata, content hash reference, path policy class, audit keys, correlation, parity dimensions |
| `ChangeFile` | Mutating command | idempotency, operation ID, task ID, workspace/lock scope, path metadata, content hash reference, path policy class, audit keys, correlation, parity dimensions |
| `RemoveFile` | Mutating command | idempotency, operation ID, task ID, workspace/lock scope, path metadata, path policy class, metadata-only removal evidence, audit keys, correlation, parity dimensions |
| `ListFolderFiles` | Context query | snapshot-per-task read consistency, path policy, result bounds, truncation metadata, freshness, safe denial, audit keys, correlation |
| `SearchFolderFiles` | Context query | snapshot-per-task read consistency, path policy, search/glob result bounds, truncation metadata, no raw search text in audit, safe denial, correlation |
| `ReadFileRange` | Context query | snapshot-per-task read consistency, byte range bounds, binary/large-file policy, authorized content response only, safe denial, audit keys, correlation |

Do not add commit, workspace status, audit timeline, operations-console, runtime worker, generated SDK, CLI, or MCP operations in this story.

### Error and Audit Requirements

- Required canonical categories for this story include authentication failure, tenant authorization denied, folder ACL denied, cross-tenant access denied, workspace not ready, workspace locked, lock expired, lock not owned, authorization revocation detected, stale workspace, path validation failed, file operation failed, input limit exceeded, response limit exceeded, query timeout, read-model unavailable, provider failure known, provider outcome unknown, reconciliation required, state transition invalid, validation error, idempotency conflict, not found, redacted, and internal error.
- Use RFC 9457 Problem Details plus Hexalith fields: `category`, `code`, `message`, `correlationId`, `retryable`, `clientAction`, and `details`.
- Safe-denial Problem Details must expose stable safe codes only; they must not reveal folder existence, workspace existence, lock existence, task existence, path existence, excluded-path matches, binary-file presence, search-hit counts, provider state, local paths, or unauthorized resource existence.
- Audit metadata is metadata-only. It may include tenant-scoped actor evidence, folder ID, workspace ID, task ID, operation ID, query family, path policy class, content hash reference, byte count, result count, truncation flag, configured limit, elapsed milliseconds, correlation ID, timestamp, result, retryability, and sanitized error category.
- Do not include raw file contents, diffs, raw search text, path lists in cross-tenant diagnostics, generated context payloads, provider tokens, credential material, raw provider payloads, production URLs, local filesystem paths, or unauthorized resource existence.

### Idempotency, Correlation, And Read Consistency

- `AddFile`, `ChangeFile`, and `RemoveFile` require caller-supplied `Idempotency-Key` plus an operation identity for each logical file change.
- Non-mutating context queries do not accept `Idempotency-Key`.
- Equivalence is tenant-scoped and operation-scoped. Same key across tenants, changed file operation kind, changed operation ID, changed path metadata, changed path policy class, changed task ID, changed workspace ID, changed content hash reference for add/change, or changed semantic payload is non-equivalent.
- Field lists in `x-hexalith-idempotency-equivalence` must be lexicographically ordered.
- Mutation-tier TTL is 24 hours. Commit-tier TTL belongs to downstream commit work and inherits C3.
- Context queries use `snapshot-per-task` read consistency per Story 1.5 unless a newer approved source changes it.
- Correlation IDs and task IDs must propagate through REST, SDK, CLI, MCP, EventStore envelopes, projections, audit, logs, and traces.

### Previous Story Intelligence

- Story 1.1 establishes the solution scaffold and dependency direction.
- Story 1.2 establishes root configuration and forbids recursive nested-submodule initialization.
- Story 1.3 seeds shared fixtures: audit leakage corpus, parity schema, previous spine, idempotency encoding corpus, `tests/load`, and artifact templates.
- Story 1.4 owns C3 retention, C4 input limits, S-2 OIDC parameters, and C6 transition mapping. This story consumes C4/C6 values but must preserve approval state and must not invent missing limits or transitions.
- Story 1.5 defines operation inventory, idempotency equivalence, adapter parity dimensions, parser-policy classifications, and read-consistency expectations. This story must consume its final artifact when available.
- Story 1.6 owns the OpenAPI foundation and shared `x-hexalith-*` vocabulary. This story must reuse it rather than redefine extension shapes.
- Story 1.7 hardened contract-only boundaries, safe-denial parity, provider diagnostic audience partitioning, and operation allow-list validation. Apply the same discipline here.
- Story 1.8 owns workspace and lock operations, lock semantics, retry eligibility, state-transition evidence, and authorization-revocation outcomes. This story references those components and must not duplicate them.

### Testing Guidance

- Prefer a focused OpenAPI validation test or script in existing test/tool locations over broad runtime integration work.
- Validation must run offline without Aspire, Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, provider credentials, tenant data, production secrets, network calls, or initialized nested submodules.
- Validate OpenAPI parse, `$ref` resolution, unique operation IDs, exact `x-hexalith-*` metadata presence, idempotency/read-consistency completeness, safe tenant-authority boundaries, C4 limit metadata, D-9 content transport metadata, synthetic examples, safe-denial parity, redaction, and negative scope.
- Include allow-list assertions for file/context operation IDs and `/api/v1` path prefixes owned by this story so validation fails if workspace/lock, commit/status, audit timeline, operations-console, runtime, generated-client, CLI, MCP, worker, UI, or CI artifacts appear.
- Include contract-level assertions for traversal rejection, binary/large-file policy, response truncation, timeout, unauthorized path/query denial, no raw search text in audit examples, and no removed-file content leakage.
- If the full solution is buildable, run `dotnet build Hexalith.Folders.slnx` from the repository root after focused validation. Record exact blockers if build cannot run due to prior scaffold state.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 1.9: Author file mutation and context query contract groups`
- `_bmad-output/planning-artifacts/epics.md#Epic 1: Bootstrap Canonical Contract For Consumers And Adapters`
- `_bmad-output/planning-artifacts/epics.md#Epic 4: Repository-Backed Workspace Task Lifecycle`
- `_bmad-output/planning-artifacts/architecture.md#API & Communication Patterns`
- `_bmad-output/planning-artifacts/architecture.md#Authorization order`
- `_bmad-output/planning-artifacts/architecture.md#Adapter Parity Contract`
- `_bmad-output/planning-artifacts/architecture.md#REST endpoint naming`
- `_bmad-output/planning-artifacts/architecture.md#HTTP headers`
- `_bmad-output/planning-artifacts/architecture.md#Requirements to Structure Mapping`
- `_bmad-output/planning-artifacts/prd.md#Endpoint Specifications`
- `_bmad-output/planning-artifacts/prd.md#Command and Query Contract`
- `_bmad-output/planning-artifacts/prd.md#Performance and Query Bounds`
- `_bmad-output/planning-artifacts/prd.md#Observability, Auditability, and Replay`
- `docs/contract/idempotency-and-parity-rules.md`
- `docs/exit-criteria/c4-input-limits.md`
- `docs/exit-criteria/c6-transition-matrix-mapping.md`
- `_bmad-output/implementation-artifacts/1-1-establish-a-consumer-buildable-module-scaffold.md`
- `_bmad-output/implementation-artifacts/1-2-establish-root-configuration-and-submodule-policy.md`
- `_bmad-output/implementation-artifacts/1-3-seed-minimally-valid-normative-fixtures.md`
- `_bmad-output/implementation-artifacts/1-4-author-phase-0-5-pre-spine-workshop-deliverables.md`
- `_bmad-output/implementation-artifacts/1-5-finalize-idempotency-equivalence-and-adapter-parity-rules.md`
- `_bmad-output/implementation-artifacts/1-6-author-contract-spine-foundation-and-shared-extension-vocabulary.md`
- `_bmad-output/implementation-artifacts/1-7-author-tenant-folder-provider-and-repository-binding-contract-groups.md`
- `_bmad-output/implementation-artifacts/1-8-author-workspace-and-lock-contract-groups.md`
- `_bmad-output/project-context.md`
- `AGENTS.md#Git Submodules`

## Project Structure Notes

- The Contract Spine operation groups live under `src/Hexalith.Folders.Contracts/openapi/`.
- Human-readable contract notes may live under `docs/contract/`, but the OpenAPI document remains the source of truth.
- Runtime context-query handlers belong later under `src/Hexalith.Folders/Queries/Context/` and must not be implemented here.
- Path policy enforcement belongs later under `src/Hexalith.Folders/Authorization/PathPolicyAuthorizer.cs` and must not be implemented here.
- Server endpoints belong later under `src/Hexalith.Folders.Server/Endpoints/{File,ContextQuery}Endpoints.cs` and must not be implemented here.
- CLI and MCP wrappers belong later under `src/Hexalith.Folders.Cli/` and `src/Hexalith.Folders.Mcp/` and must not be implemented here.
- Commit/status, audit/timeline, operations-console, worker, provider, and generated SDK behavior are downstream story scope.

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-11 | Created ready-for-dev story through `bmad-create-story` workflow. | Codex |
| 2026-05-11 | Applied party-mode review hardening for operation mapping, D-9 transport semantics, safe denial, context-query authorization order, bounded range reads, semantic/RAG boundaries, and validation oracles. | Codex |

## Party-Mode Review

- Date: 2026-05-11T23:05:04Z
- Selected story key: `1-9-author-file-mutation-and-context-query-contract-groups`
- Command/skill invocation used: `/bmad-party-mode 1-9-author-file-mutation-and-context-query-contract-groups; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Paige (Technical Writer), Murat (Master Test Architect and Quality Advisor)
- Findings summary: all reviewers found the story directionally viable but requiring sharper contract-level guardrails around exact OpenAPI operation placement, mutation versus D-9 transport naming, `PutFileInline`/`PutFileStream` semantics, exact 256 KiB boundary, `413` retry-as-stream response header, prepared-workspace and held-lock precondition surfaces, safe-denial behavior, context-query authorization order, tenant-authority negative checks, read-consistency/freshness vocabulary, bounded range-read semantics, semantic/RAG extension boundaries, metadata-only examples, and focused offline validation oracles.
- Changes applied: added party-mode hardening notes; added subtasks for concrete D-9 operation mapping, 262144-byte inline boundary, retry-as-stream header contract, contract-only validation oracle, no `Idempotency-Key` on context queries, safe-denial/error coverage, byte-range boundary examples, and authorization-order metadata without caller-controlled policy bypass fields.
- Findings deferred: exact `403`/`404` safe-denial matrix, whether bounded range reads may return actual content or only metadata/range descriptors for specific sensitivity classes, whether redaction metadata is mandatory on every context response or only suppressed-field responses, how projection lag is represented, how search/glob constraints are exposed without making tenant or policy authority client-controlled, and where parity dimensions are captured before final parity-oracle rows.
- Final recommendation: ready-for-dev

## Dev Agent Record

### Agent Model Used

TBD by dev-story agent

### Debug Log References

### Completion Notes List

### File List
