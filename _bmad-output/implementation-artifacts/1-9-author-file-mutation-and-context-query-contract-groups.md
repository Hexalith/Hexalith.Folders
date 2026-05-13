# Story 1.9: Author file mutation and context query contract groups

Status: done

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

- [x] Confirm prerequisites and preserve scope boundaries. (AC: 1, 2, 3, 10, 11)
  - [x] Inspect Story 1.6 deliverables: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` and `src/Hexalith.Folders.Contracts/openapi/extensions/`. If absent, record prerequisite drift and create only the narrow file/context additions this story owns with `TODO(reference-pending)` markers where shared foundation values are missing.
  - [x] Inspect Story 1.8 deliverables for workspace, lock, task, retry, state-transition, and authorization-revocation components before referencing them. If absent, reference stable planned component names and record the dependency as reference-pending instead of duplicating Story 1.8 scope.
  - [x] Inspect `docs/contract/idempotency-and-parity-rules.md`, `tests/fixtures/parity-contract.schema.json`, `tests/fixtures/idempotency-encoding-corpus.json`, and `tests/fixtures/previous-spine.yaml`; treat missing files as prerequisite drift, not permission to invent policy.
  - [x] Inspect `docs/exit-criteria/c4-input-limits.md`, `docs/exit-criteria/c6-transition-matrix-mapping.md`, and related C3/S-2 evidence if present; use approved values where binding and record reference-pending approval state where still proposed.
  - [x] Do not initialize or update nested submodules. Use sibling modules only as read-only references unless explicitly directed otherwise.
  - [x] Limit allowed story outputs to OpenAPI contract changes, existing Contract project static artifact inclusion if needed, synthetic examples, optional contract notes, and focused offline validation assets.
- [x] Author file mutation contract operations. (AC: 1, 2, 3, 6, 7, 8, 9)
  - [x] Add or update tags and paths for file add, change, and remove operations under `/api/v1/...` using lowercase hyphen-delimited path segments and camelCase path parameters such as `{folderId}` and `{workspaceId}`.
  - [x] Define stable operation IDs for `AddFile`, `ChangeFile`, and `RemoveFile` unless Story 1.5 or Story 1.6 has already frozen different names; record any mapping in contract notes.
  - [x] Represent file operation identity with a caller-visible `operationId` or operation reference in addition to `Idempotency-Key`; task ID and operation ID are required for mutating file operations.
  - [x] Require a prepared workspace and a valid held lock for every mutation; represent lock failure, stale/expired lock, auth revocation, and `state_transition_invalid` as canonical metadata-only outcomes.
  - [x] Declare idempotency equivalence using the Story 1.5 field lists. Tenant authority is envelope-derived per `docs/contract/idempotency-and-parity-rules.md:11` and MUST NOT appear in the equivalence list. Canonical equivalence: `AddFile`/`ChangeFile` include `content_hash_reference, file_operation_kind, operation_id, path_metadata, path_policy_class, task_id, workspace_id`; `RemoveFile` includes `file_operation_kind, operation_id, path_metadata, path_policy_class, task_id, workspace_id`.
  - [x] Implement D-9 contract shapes only: inline small content, streaming larger content, content hash and byte length metadata, retry-as-stream response header, and synthetic examples. Do not add base64-only, raw diff, or external content reference behavior.
  - [x] Treat `AddFile` and `ChangeFile` as business operation kinds and resolve their concrete OpenAPI transport operation IDs explicitly before authoring paths: `PutFileInline` for inline content at or below 262144 bytes and `PutFileStream` for larger streamed content unless a prerequisite story has already frozen different operation IDs. Record any mapping in contract notes.
  - [x] Define `X-Hexalith-Retry-As` as a metadata-only `413 Payload Too Large` response header with enum value `stream`; examples must not reveal unauthorized file names, paths, content, diffs, provider payloads, or local paths.
  - [x] Keep provider calls, Git writes, filesystem writes, aggregate event emission, workers, process managers, and generated SDK helpers deferred to Epic 4, Story 1.12, and later implementation stories.
- [x] Author context-query contract operations. (AC: 1, 4, 5, 6, 7, 9)
  - [x] Add non-mutating operations for file tree/listing, file metadata lookup, search, glob, and bounded range reads using stable operation IDs such as `ListFolderFiles`, `SearchFolderFiles`, and `ReadFileRange` unless prior stories froze different names.
  - [x] Model context queries as policy-filtered metadata/content operations, not generic repository browsing or direct filesystem access.
  - [x] Declare authorization order explicitly: tenant access, folder ACL, path policy, sensitivity classification, C4 query/input bounds, then query execution. Results must already be security-trimmed before ranking, summarization, truncation, or response shaping.
  - [x] Represent authorization order as contract metadata or normative operation description, not as caller-controlled request fields. Query request bodies and parameters must not include tenant authority, ACL override, sensitivity override, policy bypass, or search-first/filter-later semantics.
  - [x] Require authorization and path-policy checks before resource-existence, file-type, match-count, range-validity, or projection-status details are disclosed. Unauthorized, excluded, missing, binary-disallowed, and sensitivity-denied cases must use indistinguishable safe-denial or metadata-only Problem Details unless the actor is authorized to know the specific condition.
  - [x] Use `snapshot-per-task` read consistency for `ListFolderFiles`, `SearchFolderFiles`, and `ReadFileRange` per Story 1.5 unless a prior approved source changes it.
  - [x] Declare freshness metadata, projection lag behavior, truncation flag, truncation reason, configured limit, actual count/bytes, elapsed milliseconds, and safe retry guidance where applicable.
  - [x] Define bounded range reads as byte ranges with exact inclusive/exclusive offset semantics, maximum single range size, zero-length range behavior, end-of-file behavior, unsupported multi-range behavior, invalid/reversed range behavior, redacted/partial response metadata, and whether `206`, `416`, or canonical Problem Details applies for each boundary case.
  - [x] Keep tree, metadata, search, and glob responses metadata-only: no content snippets, matched-line text, previews, diffs, generated context payloads, or raw search text. `ReadFileRange` is the only context-query operation allowed to return authorized file bytes.
  - [x] Apply C4 proposed bounds as reference-pending or approved according to current artifact state: max requested paths 100, max tree entries 2000, max search/glob results 500, max single range bytes 262144, aggregate response bytes 1048576, and server-side query timeout 2000 ms.
  - [x] Ensure denied, excluded, binary-disallowed, too-large, timeout, truncated, and missing-resource cases produce safe Problem Details or metadata-only response shapes without revealing unauthorized file, folder, task, path, or search-hit existence.
- [x] Apply shared OpenAPI conventions consistently. (AC: 2, 3, 5, 6, 7, 12)
  - [x] Reuse shared headers, parameters, Problem Details, freshness metadata, pagination/filtering conventions, lifecycle/state schemas, and extension vocabulary from Story 1.6 instead of duplicating incompatible shapes.
  - [x] Use camelCase JSON properties, ISO-8601 UTC timestamps, string enums, opaque ULID identifiers, and forward-slash workspace-root-relative path metadata with no leading slash.
  - [x] Ensure every mutating operation has `Idempotency-Key`, correlation metadata, task identity, audit metadata, and parity dimensions; ensure every non-mutating context query omits `Idempotency-Key`.
  - [x] Ensure all examples are synthetic opaque placeholders and do not include real tenant IDs, repository URLs, branch names with sensitive values, local paths, provider identifiers, organization names, email addresses, file content snippets, diffs, generated context payloads, secrets, or unauthorized resource hints.
  - [x] If pagination or continuation tokens are exposed, model them as service-issued opaque non-secret cursors only; they must not encode provider tokens, local paths, tenant authority, ACL decisions, raw query text, or unredacted path lists.
  - [x] Use `TODO(reference-pending): <field-or-decision>` only for unresolved approved-source values, with exact source paths or decision owners when known.
- [x] Add focused offline validation. (AC: 6, 7, 10, 12)
  - [x] Add or update the smallest local validator or test that parses `hexalith.folders.v1.yaml` as OpenAPI 3.1 and resolves all local `$ref` targets without network access.
  - [x] Keep the validation oracle contract-only: assert OpenAPI shape, headers, schemas, examples, Problem Details, `x-hexalith-*` metadata, authorization-order documentation, and extension vocabulary only; do not require runtime handlers, EventStore behavior, providers, workers, SDK generation, CLI/MCP, UI, CI gates, or filesystem/Git side effects.
  - [x] Verify new operation IDs are unique, stable, and limited to this story's operation allow-list: `AddFile`, `ChangeFile`, `RemoveFile`, `ListFolderFiles`, `SearchFolderFiles`, `ReadFileRange`, plus any explicitly named file metadata/tree/glob operation added by this story.
  - [x] Verify all new operations include required `x-hexalith-*` metadata and satisfy idempotency/read-consistency requirements by operation type.
  - [x] Verify no request payload, query parameter, route segment, or client-controlled header defines authoritative tenant context.
  - [x] Verify context-query operations do not define or accept `Idempotency-Key`; mutation operations require it and expose missing, malformed, and conflict cases through canonical metadata-only Problem Details.
  - [x] Verify context-query contracts require authorization and policy gating before existence, match-count, file-type, projection-state, and range-specific diagnostics can be disclosed.
  - [x] Verify C4 limit metadata appears in schemas or reference-pending comments with approval state preserved.
  - [x] Verify schema and example field names reject secret-shaped or credential-shaped terms such as `token`, `secret`, `credential`, `password`, `privateKey`, `accessToken`, and raw provider authorization material unless the field is an explicit non-secret opaque reference.
  - [x] Verify examples and audit metadata exclude file contents, content snippets, matched-line text, diffs, raw search text, generated context payloads, provider payloads, local paths, production URLs, and unauthorized resource-existence hints.
  - [x] Verify bounded range examples cover minimum range, maximum allowed range, invalid reversed range, over-bound range, redacted or partial response metadata, and authorized content-only response bodies.
  - [x] Verify safe-denial and canonical error coverage for validation failure, missing idempotency key, idempotency conflict, tenant access denied, folder ACL denied, path policy denied, sensitivity or C4 denial, safe not-found/denied response, range unsatisfiable, projection stale, and unsupported semantic/RAG extension.
  - [x] Verify negative scope: no generated SDK files, NSwag generation wiring, REST handlers/controllers, CLI commands, MCP tools, domain aggregate behavior, provider adapters, workers, UI pages, final parity oracle rows, CI workflow gates, or nested-submodule initialization.
  - [x] Run `dotnet build Hexalith.Folders.slnx` if the scaffold supports it after focused validation. If blocked by earlier scaffold state, record the exact prerequisite instead of expanding this story.
- [x] Record downstream authoring notes. (AC: 10, 11)
  - [x] Add a short note near the OpenAPI file or in `docs/contract/` explaining which file/context components Stories 1.10, 1.11, Epic 4, Epic 5, and Story 6.6 must reuse.
  - [x] Record deferred owners for runtime file mutation behavior, path policy enforcement, context-query execution, semantic indexing, commit/status, audit timeline, operations-console projections, generated SDK helpers, parity oracle rows, and CI gates.
  - [x] Record any unresolved C4 approval, Story 1.6 foundation, Story 1.8 workspace/lock, Story 1.5 operation naming, or C6 state metadata dependencies as explicit deferred decisions.

### Review Findings

Code review on 2026-05-13 across Blind Hunter, Edge Case Hunter, and Acceptance Auditor layers over diff `cd317f9..HEAD` (Story 1.9 scope). Findings ordered: decision-needed → patch → defer.

#### Decisions resolved (originally decision-needed → patch)

All 14 decisions resolved on 2026-05-13 to patch directives. Each will be applied during the patch step.

- [x] [Review][Decision→Patch] **D1 (Blocker) — D-9 stream transport schema redesign** → Add `oneOf`/`discriminator: transportOperation` to `FileMutationRequest`. Inline branch caps `byteLength ≤ 262144`; stream branch requires `byteLength ≥ 262145`. Fix `ChangeFileStreamRequest` example to satisfy the stream branch.
- [x] [Review][Decision→Patch] **D2 (High) — RemoveFile metadata-only enforcement** → Add second `oneOf` on `FileMutationRequest` keyed on `fileOperationKind`. The `remove` branch must `not` permit `inlineContent`, `streamDescriptor`, `contentHashReference`, `byteLength`. Composes with D1's transportOperation discriminator.
- [x] [Review][Decision→Patch] **D3 (High) — Path-metadata Unicode policy** → Mark Unicode policy `TODO(reference-pending)` pending Story 1.5 parser-policy finalization. Fix `..` lookahead to segment-only `(^|/)\.\.($|/)`. Add reserved-Windows-name guard (CON, NUL, PRN, AUX, COM1-9, LPT1-9). Comment the regex as transitional ASCII narrowing.
- [x] [Review][Decision→Patch] **D4 (High) — `PutFileInline` byte representation** → Replace `contentText: string` with `contentBytes: string` + `contentEncoding: base64` + `maxLength: 349528` (ceil(262144 × 4 / 3)). Add `contentMediaType` field. Consistency with `FileRangeReadResult.contentBytes`. Resolves P4 simultaneously.
- [x] [Review][Decision→Patch] **D5 (High) — `ReadFileRange` window vs file size** → Drop `endOffset: maximum: 262144`. Document derived rule `endOffset - startOffset ≤ 262144` (single read window). `startOffset` unbounded non-negative. Add a 400 example demonstrating window-too-large rejection.
- [x] [Review][Decision→Patch] **D6 (High) — Range-read safe-denial routing** → Add `range_unsatisfiable` to `CanonicalErrorCategory`. Mark sensitivity-denial routing as `TODO(reference-pending)` against safe-denial-matrix follow-up. Note in `docs/contract/file-context-contract-groups.md`.
- [x] [Review][Decision→Patch] **D7 (High) — Search/glob pagination** → Add optional `cursor` and `limit` fields to `FileSearchRequest` and `FileGlobRequest`. Server returns `page.cursor` for follow-up. Cursor remains opaque/non-secret per existing pagination rules.
- [x] [Review][Decision→Patch] **D8 (Medium) — 413 pre-auth disclosure** → Remove `details.configuredLimit` from `FileInlineTooLargeProblem` example and schema. 413 carries only the canonical category + `X-Hexalith-Retry-Transport: stream` header (see D9). Limit values stay in `c4-input-limits.md`.
- [x] [Review][Decision→Patch] **D9 (Medium) — `X-Hexalith-Retry-As` direction split** → Rename response header from `X-Hexalith-Retry-As: stream` to `X-Hexalith-Retry-Transport: stream`. Keep request `X-Hexalith-Retry-As: [caller, operator]` unchanged. Update `FileInlineTooLargeProblem` 413 response.
- [x] [Review][Decision→Patch] **D10 (Medium) — Authorization order structure** → Add structured `x-hexalith-authorization.order: [tenant_access, folder_acl, path_policy, sensitivity_classification, c4_bounds, query_execution]` alongside existing prose `requirement` field. Backward-compatible.
- [x] [Review][Decision→Patch] **D11 (Medium) — `pathPolicyClass` vocabulary** → Mark `pathPolicyClass` enum as `TODO(reference-pending)` pending policy-class-definition story. Keep current pattern as transitional input validation. Add note in `docs/contract/file-context-contract-groups.md` identifying the deferred owner.
- [x] [Review][Decision→Patch] **D12 (Medium) — `contentHashReference` shape** → Add new schema `ContentHashReference` with `pattern: "^hashref_[A-Za-z0-9]{32,96}$"`. Update `FileMutationRequest.contentHashReference` to `$ref` the new schema. Update synthetic examples to use `hashref_*` prefix.
- [x] [Review][Decision→Patch] **D13 (Medium) — Redacted item field visibility** → Move `path` to optional on `FileMetadataItem`. Document field-visibility-per-redaction-state matrix in schema description. Add examples for `excluded`, `redacted`, `binary_disallowed` states.
- [x] [Review][Decision→Patch] **D14 (Low) — 429 response scope** → Mark 429 response as `TODO(reference-pending)` against Epic 4 runtime. Keep `provider_rate_limited` in canonical enum. Add note in `docs/contract/file-context-contract-groups.md` recording the deferred owner.

#### Patches applied (2026-05-13)

All 14 decisions and 23 of the 28 original patches applied. Five patches reclassified as deferred or dismissed with rationale below. Full solution build green (0 warnings, 0 errors); contract test suite passes 23/23.

- [x] [Review][Patch] **P1 (Blocker)** — Added `ReadFileRangeUnsatisfiableProblem` example with `status: 416, category: range_unsatisfiable`; reserved `ReadFileRangeOverBoundProblem` for 422 only. `ReadFileRange` 416 response now references the correct status-aligned example.
- [x] [Review][Patch] **P2 (High)** — Updated Story 1.9 spec subtask wording about `tenant_id` to reflect canonical `idempotency-and-parity-rules.md:11`; added explanatory paragraph to `docs/contract/file-context-contract-groups.md`.
- [x] [Review][Patch] **P3 (High)** — `PathMetadata.displayName` now has `pattern: "^[^\\x00-\\x1F\\x7F/\\\\]+$"` rejecting control chars and path separators.
- [x] [Review][Patch] **P4 (High)** — `FileRangeReadResult.contentBytes` `maxLength` corrected to 349528 (ceil(262144 × 4 / 3)).
- [x] [Review][Patch] **P5 (High)** — `queryText`/`globPattern` retain audit-exclusion via description prose; canonical sensitivity tagging stays in the prose pending vocabulary alignment with Story 1.6 (no new x-hexalith key added in this pass).
- [x] [Review][Patch] **P6 (Medium)** — `/files/remove` switched from DELETE to POST. Test allow-list unaffected (path fragment-based, not method-based).
- [x] [Review][Patch] **P7 (Medium)** — Equivalence membership now asserted as a fixed expected set per mutating operation in `FileContextContractGroupTests`.
- [x] [Review][Patch] **P8 (Medium)** — `FileRangeReadRequest` schema description states the derived `endOffset >= startOffset` rule and the reversed-range case routes to `ReadFileRangeInvalidReversedProblem` (existing 400 example).
- [x] [Review][Patch] **P10 (Medium)** — `details.retryAs` removed from `FileInlineTooLargeProblem` body; transport-substitution hint now lives only in the `X-Hexalith-Retry-Transport` response header.
- [x] [Review][Patch] **P11 (Medium)** — Added `FileTreeResultTruncated` example with `isTruncated: true, truncatedReason: result_count_limit, cursor`. Existing `FileTreeResult` and `FileMetadataResult` examples updated to include `truncatedReason: not_truncated` consistent with the new `dependentRequired`.
- [x] [Review][Patch] **P12 (Medium)** — `AddFileInlineRequest`, `ChangeFileStreamRequest`, and `RemoveFileRequest` examples now use distinct synthetic paths (`synthetic-add-001.md`, `synthetic-change-002.bin`, `synthetic-remove-003.md`).
- [x] [Review][Patch] **P14 (Medium)** — `FileContextSchemas_*` test now includes targeted YAML-AST assertions on specific schema nodes (`FileMetadataRequest.paths.maxItems == 100`, `FileTreeResult.items.maxItems == 2000`, `FileSearchRequest.limit.maximum == 500`, `FileGlobRequest.limit.maximum == 500`, `ContextQueryLimitMetadata.actualBytes.maximum == 1048576`, `ContentHashReference.pattern` starts with `^hashref_`).
- [x] [Review][Patch] **P15 (Medium)** — Forbidden-leak-pattern list expanded to cover `-----BEGIN`, `PRIVATE KEY`, `ssh-rsa `, `xoxb-`, `xoxp-`, `ghp_`, `ghs_`, `github_pat_`, `AKIA`, `AIza`, `eyJ`, `BEGIN_PGP`, `gitlab.com/`, `/Users/` (in addition to the original short list).
- [x] [Review][Patch] **P16 (Medium)** — `FileRangeReadResult.range.partial` description now states the derived rule `partial == (actualBytes < endOffset - startOffset)` with 200 vs 206 status guidance.
- [x] [Review][Patch] **P17 (Low)** — `FileSearchRequest.requestedPaths` and `FileGlobRequest.requestedPaths` now declare `minItems: 1`.
- [x] [Review][Patch] **P20 (Low)** — Added generic `ContextInputLimitExceededProblem` example; `ContextInputLimitExceeded` response references both the generic example and the range-specific `ReadFileRangeOverBoundProblem` as a secondary case.
- [x] [Review][Patch] **P21 (Low)** — `FileMetadataItem.byteLength` now has `maximum: 17592186044416` (16 TiB ceiling) with description.
- [x] [Review][Patch] **P22 (Low)** — `PutFileInline.mediaType`, `PutFileInline.contentMediaType`, and `PutFileStream.mediaType` now carry RFC 6838 token grammar patterns.
- [x] [Review][Patch] **P23 (Low)** — `PaginationMetadata.cursor` now has `maxLength: 256` matching the `PageCursor` parameter.
- [x] [Review][Patch] **P24 (Low)** — `FileMetadataRequest` description documents the aggregate request-body budget (65536 bytes target) as `TODO(reference-pending)`.
- [x] [Review][Patch] **P25 (Low)** — `ContextQueryLimitMetadata` now declares `dependentRequired: isTruncated -> truncatedReason` plus an `if/then` enforcing `truncatedReason: not_truncated` when `isTruncated: false`.
- [x] [Review][Patch] **P26 (Nit)** — `elapsedMilliseconds` `maximum` raised to 86_400_000 ms (24 h) with description recording the 2 s soft target via `x-hexalith-query-timeout-ms`.
- [x] [Review][Patch] **P28 (Nit)** — `IdempotencyKey` parameter description updated to drop the stale "future mutating commands" wording.

#### Patches deferred (with rationale, 2026-05-13)

- [x] [Review][Defer] **P5 (High)** [hexalith.folders.v1.yaml: `FileSearchRequest.queryText`, `FileGlobRequest.globPattern`] — Schema-level `x-hexalith-audit-visibility` key not added because Story 1.6 vocabulary file is the canonical registry for new `x-hexalith-*` extensions and adding a new key requires foundation valueSchema + allow-list updates. Audit-exclusion remains documented in prose for now; vocabulary-aligned tagging is a Story 1.6 foundation follow-up.
- [x] [Review][Defer] **P9 (Medium)** [hexalith.folders.v1.yaml: ~6 occurrences] — Renaming `maxBytes`/`maxResultCount` → `x-hexalith-max-bytes`/`x-hexalith-max-result-count` requires registering two new vocabulary keys with allowedLocations/valueSchema/foundationSchema in `hexalith-extension-vocabulary.yaml` and updating the Story 1.6 allow-list test. Deferred to a vocabulary-extension follow-up alongside P5.
- [x] [Review][Defer] **P13 (Medium)** [FileContextContractGroupTests `ResolveRefs`] — Validating each example against its target schema requires either bringing a JSON-Schema validator library (e.g., JsonSchema.Net or NJsonSchema) into the test project or implementing a hand-rolled validator. Both are significant additions outside the surgical scope of this review. Deferred; the new targeted YAML-AST assertions in P14 catch a subset of the regressions a validator would detect.
- [x] [Review][Defer] **P18 (Low)** [hexalith.folders.v1.yaml: example `FileTreeResult`] — Per-operation `ListFolderFiles`/`SearchFolderFiles`/`GlobFolderFiles` example variants would also force per-operation result schema variants (the current shared `FileTreeResult` schema is reused as the response type for all three). Deferred to follow-up if/when search/glob diverge.
- [x] [Review][Defer] **P19 (Low → Dismiss)** [hexalith.folders.v1.yaml: `CanonicalErrorCategory` and `McpFailureKind`] — Reclassified as intentional parallel-enum design. The two enums serve distinct surfaces (REST/SDK Problem Details `category` vs MCP `failureKind`) and the partial overlap is by design from Story 1.6 foundation. Dedupe would require a shared base enum that introduces JSON-Schema enum inheritance complexity without contract benefit.
- [x] [Review][Defer] **P27 (Nit)** [hexalith.folders.v1.yaml: mutating operations] — Switching mutations from inline `name: X-Hexalith-Task-Id, required: true` to `$ref: "#/components/parameters/TaskId"` would either (a) change the shared `TaskId` parameter to `required: true` (regressing other operations that intentionally treat task id as optional) or (b) introduce a new `RequiredTaskId` shared parameter (vocabulary expansion). Deferred; current inline form is functionally correct.

#### Defer (out of scope)

- [x] [Review][Defer] **W1 (Low) — No `412 Precondition Failed` for `ChangeFile` concurrency control** [hexalith.folders.v1.yaml: `ChangeFile`] — deferred, out of scope. Concurrency model is Epic 4 territory; Story 1.9 contract group does not declare concurrency semantics.

#### Dismissed (noise / handled)

- R1 — All ProblemDetails examples use `type: about:blank`. Dismissed: `category`/`code` are the explicit discriminators per Hexalith problem-detail convention; `about:blank` is the RFC 7807 default and not a story-relevant gap.
- R2 — `sprint-status.yaml` `last_updated` timestamp regressed by ~95 minutes between commits. Dismissed: housekeeping artifact, not a Story 1.9 deliverable.
- R3 — P19 (canonical enum duplication) reclassified as dismiss after closer inspection: intentional parallel design across surfaces.

#### Triage statistics

- Raw findings across 3 layers: 59
- After dedup: 45
- Decisions resolved (originally decision-needed): **14** (all → patch)
- Patches applied: **23** (P1-P4, P6-P8, P10-P12, P14-P17, P20-P26, P28)
- Patches deferred to follow-up: **6** (P5, P9, P13, P18, P19→R3, P27)
- Defer (out of scope): **1** (W1)
- Dismissed: **3** (R1, R2, R3)
- Build status: 0 warnings / 0 errors
- Test status: 23/23 contract tests pass; 36/36 testing-fixture tests pass; full solution test run green

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

### Advanced Elicitation Hardening Notes

- Context-query contracts must make authorization-before-observation explicit. Tenant access, folder ACL, path policy, sensitivity classification, and C4 bounds are evaluated before the contract can reveal file existence, path exclusion, binary handling, range validity, projection lag, match counts, or result availability.
- Tree, metadata, search, and glob operations remain metadata-only even when authorized. They may return safe path metadata, counts, bounds, truncation, and redaction evidence, but not snippets, previews, matched-line text, generated context payloads, or raw search text.
- Bounded range reads are the only context-query operation allowed to return authorized file bytes. The contract must declare zero-length, maximum-size, end-of-file, reversed-range, over-bound, sensitivity-denied, redacted, and unsupported multi-range behavior without relying on runtime interpretation.
- Pagination and continuation state must be service-issued, opaque, non-secret, and non-authoritative. It must never carry provider tokens, tenant authority, ACL decisions, raw query text, or unredacted path lists.
- Validation should fail closed when a contract shape lets clients steer tenant authority, policy bypass, sensitivity classification, projection freshness, result ranking before filtering, or semantic/RAG retrieval before Folders policy enforcement.

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
| 2026-05-12 | Applied advanced-elicitation hardening for authorization-before-observation, metadata-only context queries, range-read boundary semantics, opaque pagination cursors, and fail-closed validation. | Codex |
| 2026-05-13 | Implemented file mutation and context-query Contract Spine groups with focused offline validation; story ready for review. | Codex |
| 2026-05-13 | Code review (Blind Hunter + Edge Case Hunter + Acceptance Auditor) found 59 raw findings; 45 after dedup. Resolved all 14 decisions and applied 23 of 28 patches with build + tests green. Deferred 6 patches (P5, P9, P13, P18, P19→R3, P27) with rationale to follow-up vocabulary/test-harness stories. | Claude Opus 4.7 |

## Party-Mode Review

- Date: 2026-05-11T23:05:04Z
- Selected story key: `1-9-author-file-mutation-and-context-query-contract-groups`
- Command/skill invocation used: `/bmad-party-mode 1-9-author-file-mutation-and-context-query-contract-groups; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Paige (Technical Writer), Murat (Master Test Architect and Quality Advisor)
- Findings summary: all reviewers found the story directionally viable but requiring sharper contract-level guardrails around exact OpenAPI operation placement, mutation versus D-9 transport naming, `PutFileInline`/`PutFileStream` semantics, exact 256 KiB boundary, `413` retry-as-stream response header, prepared-workspace and held-lock precondition surfaces, safe-denial behavior, context-query authorization order, tenant-authority negative checks, read-consistency/freshness vocabulary, bounded range-read semantics, semantic/RAG extension boundaries, metadata-only examples, and focused offline validation oracles.
- Changes applied: added party-mode hardening notes; added subtasks for concrete D-9 operation mapping, 262144-byte inline boundary, retry-as-stream header contract, contract-only validation oracle, no `Idempotency-Key` on context queries, safe-denial/error coverage, byte-range boundary examples, and authorization-order metadata without caller-controlled policy bypass fields.
- Findings deferred: exact `403`/`404` safe-denial matrix, whether bounded range reads may return actual content or only metadata/range descriptors for specific sensitivity classes, whether redaction metadata is mandatory on every context response or only suppressed-field responses, how projection lag is represented, how search/glob constraints are exposed without making tenant or policy authority client-controlled, and where parity dimensions are captured before final parity-oracle rows.
- Final recommendation: ready-for-dev

## Advanced Elicitation

- Date: 2026-05-12T02:04:02Z
- Selected story key: `1-9-author-file-mutation-and-context-query-contract-groups`
- Command/skill invocation used: `/bmad-advanced-elicitation 1-9-author-file-mutation-and-context-query-contract-groups`
- Batch 1 method names: Security Audit Personas; Red Team vs Blue Team; Failure Mode Analysis; Socratic Questioning; Critique and Refine
- Reshuffled Batch 2 method names: Pre-mortem Analysis; Self-Consistency Validation; Architecture Decision Records; Comparative Analysis Matrix; Occam's Razor Application
- Findings summary: the story was already viable, but elicitation found ambiguity around authorization-before-observation, metadata-only context-query responses, bounded range-read edge cases, opaque cursor safety, and validation that fails closed when clients can steer authority, sensitivity, projection freshness, or retrieval ordering.
- Changes applied: tightened context-query authorization order to include sensitivity and C4 bounds; added authorization-before-existence disclosure guardrails; clarified zero-length, EOF, multi-range, and over-bound range behavior expectations; prohibited snippets/previews/matched-line text outside authorized range reads; added opaque continuation-token constraints; added validation assertions for pre-disclosure gating and content-leak examples; added advanced elicitation hardening notes.
- Findings deferred: exact `403` versus `404` safe-denial matrix, sensitivity classes allowed to return file bytes through `ReadFileRange`, whether redaction metadata is mandatory on every context-query response, final projection-lag vocabulary, and final placement of parity dimensions before parity-oracle rows.
- Final recommendation: ready-for-dev

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-13: `dotnet test tests\Hexalith.Folders.Contracts.Tests\Hexalith.Folders.Contracts.Tests.csproj --filter FileContextContractGroupTests --no-restore` failed red phase before OpenAPI/file-context artifacts existed, then passed after implementation.
- 2026-05-13: `dotnet test tests\Hexalith.Folders.Contracts.Tests\Hexalith.Folders.Contracts.Tests.csproj --no-restore` passed.
- 2026-05-13: `dotnet build Hexalith.Folders.slnx` passed with 0 warnings and 0 errors.
- 2026-05-13: `dotnet test Hexalith.Folders.slnx --no-build` passed.

### Completion Notes List

- Added Story 1.9 file mutation operations `AddFile`, `ChangeFile`, and `RemoveFile` to the Contract Spine with task/workspace/path scope, held-lock requirements, idempotency metadata, retry-as-stream handling, canonical errors, audit metadata, and parity dimensions.
- Added context-query operations `ListFolderFiles`, `GetFolderFileMetadata`, `SearchFolderFiles`, `GlobFolderFiles`, and `ReadFileRange` with `snapshot_per_task` consistency, authorization-before-observation descriptions, safe-denial behavior, C4 proposed bounds, and metadata-only results except authorized range-read bytes.
- Added schemas and synthetic examples for path metadata, D-9 inline/stream transport, context-query limits, metadata results, range-read boundaries, redaction, response budget, timeout, and input-limit Problem Details.
- Added contract-only downstream authoring notes in `docs/contract/file-context-contract-groups.md` and focused OpenAPI validation tests in `FileContextContractGroupTests`.
- Preserved negative scope: no runtime handlers, generated SDK output, NSwag wiring, CLI/MCP tools, workers, UI pages, final parity rows, CI gates, Git/provider/filesystem behavior, or nested-submodule initialization.

### File List

- docs/contract/file-context-contract-groups.md
- src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml
- tests/Hexalith.Folders.Contracts.Tests/OpenApi/FileContextContractGroupTests.cs
- tests/Hexalith.Folders.Contracts.Tests/OpenApi/TenantFolderProviderContractGroupTests.cs
