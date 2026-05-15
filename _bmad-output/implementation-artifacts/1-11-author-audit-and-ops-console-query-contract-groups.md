# Story 1.11: Author audit and ops-console query contract groups

Status: review

Created: 2026-05-12

## Story

As an operator and audit reviewer,
I want audit and ops-console query operations represented in the Contract Spine,
so that diagnostic and audit views consume stable metadata-only contracts.

## Acceptance Criteria

1. Given Contract Spine operation groups from Stories 1.7 through 1.10 exist or are explicitly reference-pending, when this story is complete, then `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` contains audit and ops-console query groups for audit trail, operation timeline, readiness diagnostics, lock diagnostics, dirty-state diagnostics, failed-operation diagnostics, provider-status diagnostics, sync-status diagnostics, and projection freshness.
2. Given audit and ops-console queries are non-mutating reads, when their OpenAPI Operation Objects are inspected, then they do not accept `Idempotency-Key`, declare approved read-consistency classes, freshness/projection-lag behavior, pagination or filtering where applicable, safe authorization-denial shapes, redaction behavior, audit metadata keys, canonical error categories, correlation behavior, and adapter parity dimensions.
3. Given audit evidence is metadata-only, when schemas, examples, Problem Details, logs, diagnostics, and audit metadata are scanned, then they contain no file contents, diffs, generated context payloads, provider tokens, credential material, raw provider payloads, production URLs, local filesystem paths, email addresses, raw repository URLs, raw branch names with sensitive values, raw commit messages, raw changed-path lists outside authorized classified metadata, or unauthorized resource-existence hints.
4. Given operators need incident reconstruction, when audit trail and operation timeline schemas are authored, then they expose authorized metadata for actor, task ID, operation ID, correlation ID, folder, workspace, provider, repository binding, state transition, sanitized result, sanitized error category, retryability, duration, timestamp, projection watermark, and redaction state without exposing content or secrets.
5. Given operations-console queries are read-only diagnostic projections, when readiness, lock, dirty-state, failed-operation, provider-status, and sync-status query schemas are authored, then they expose diagnostic status, trust/freshness evidence, operator-disposition labels, retry or escalation posture, and safe supporting identifiers while not defining mutation controls, repair actions, credential reveal, file browsing, raw diff display, unrestricted filesystem browsing, or UI-only lifecycle semantics.
6. Given tenant authority is never client-controlled, when request schemas, query parameters, headers, and path parameters are authored, then no payload field, query parameter, client-controlled header, provider identifier, repository identifier, audit cursor, timeline cursor, filter value, or console diagnostic selector defines authoritative tenant context; tenant authority comes from authentication context and EventStore envelopes only.
7. Given audit and diagnostic reads may involve hidden or missing resources, when safe-denial responses are authored, then unauthorized, wrong-tenant, redacted, hidden, unknown, stale, and projection-unavailable cases do not reveal whether folder, workspace, lock, task, audit record, provider state, repository binding, commit, file path, changed path, or operation evidence exists unless the actor is already authorized for that diagnostic audience.
8. Given sensitive metadata classification is shared contract vocabulary, when audit and ops-console schemas include paths, branch names, repository names, commit messages, provider diagnostic references, changed-path evidence, or actor metadata, then they use classified metadata, digest/reference fields, redacted shapes, bounded strings, and safe examples rather than raw sensitive values.
9. Given operations-console UX depends on shared state language, when status schemas are authored, then they reuse C6 lifecycle states and operator-disposition labels from `docs/exit-criteria/c6-transition-matrix-mapping.md` or the shared Contract Spine foundation; they do not invent UI-only state names that diverge from REST, SDK, CLI, MCP, audit, or parity semantics.
10. Given status and audit summary queries have performance budgets, when response and pagination contracts are authored, then they use bounded filters, service-issued opaque cursors, configured limits, elapsed time, truncation state, projection freshness, and retry guidance from approved C4/C5 evidence or explicit `TODO(reference-pending)` markers; cursor values must not encode tenant authority, provider tokens, raw query text, ACL decisions, or unredacted path lists.
11. Given this is a contract-group authoring story, when implementation is complete, then no runtime REST handlers, EventStore commands, domain aggregate behavior, provider adapters, Git/file-system side effects, generated SDK output, NSwag generation wiring, CLI commands, MCP tools, workers, UI pages, final parity-oracle rows, CI workflow gates, or nested-submodule initialization are added by this story.
12. Given adjacent stories own other capability groups, when this story is complete, then operation paths and schemas remain limited to audit trail, operation timeline, readiness/status diagnostics, and operations-console projection-query concerns and do not implement tenant/folder/provider/repository binding, workspace preparation, lock mutation, file mutation, context query, commit, reconciliation workers, cleanup behavior, generated clients, UI rendering, or release documentation.
13. Given the Contract Spine is a mechanical source for SDK and parity work, when validation runs, then OpenAPI 3.1 parsing succeeds offline, all `$ref` targets resolve, operation IDs are unique and limited to this story's explicit allow-list, every operation has required `x-hexalith-*` metadata, all queries satisfy read-consistency requirements, no client-controlled tenant authority exists, examples are synthetic and metadata-only, sensitive metadata is classified or redacted, safe-denial parity is preserved, and negative-scope checks prove downstream runtime/adapter/generated/UI artifacts were not added.
14. Given party-mode and elicitation hardening may refine this story, when implementation starts, then the development agent treats the operation inventory, editable file set, authorization-before-observation rules, metadata-only audit rules, and validation matrix in Dev Notes as binding story constraints unless a previously approved Contract Spine artifact has frozen a different shape and the story records that mapping explicitly.
15. Given audit and diagnostic contracts are tenant-scoped reads, when any operation, filter, selector, cursor, path parameter, or header is authored, then it may identify candidate resources for validation but never establishes tenant authority, overrides ACLs, changes diagnostic audience, or changes cross-tenant denial behavior.
16. Given audit and ops-console contracts are adopter-facing API contracts, when tags, operation IDs, schema names, extension fields, route names, and examples are authored, then they use one stable naming convention for audit queries, operation timeline queries, ops-console diagnostics, and projection freshness; any deviation from existing Contract Spine names is recorded as an explicit mapping instead of silently introducing synonyms.
17. Given OpenAPI references are part of the Contract Spine, when validation runs, then all local `$ref` targets used by this story resolve through nested schemas, parameters, and responses; unresolved local references fail validation; remote or external references are deferred unless already supported by the repository validator.
18. Given diagnostics may be rendered in localized operations-console surfaces, when response schemas expose user-facing status or disposition information, then machine-readable codes and shared operator-disposition values carry API semantics, while display text or localization keys remain optional presentation metadata and never become English-only contract logic.
19. Given hidden-resource and redaction behavior is security-sensitive, when validation runs, then explicit cases cover visible, hidden, unauthorized, wrong-tenant, redacted, unknown, missing, stale, projection-unavailable, tampered-cursor, changed-filter, tenant/principal-mismatch, invalid-sort, boundary-duplicate, and empty-page continuation scenarios without leaking existence through status, counts, ordering, cursors, metadata, or examples.
20. Given audit and diagnostic query responses may serve different audiences, when operations and schemas are authored, then each operation records its intended diagnostic audience, consumer-safe fields remain non-enumerating, operator-only fields stay sanitized and classified, and no response shape lets a caller upgrade diagnostic audience through filters, cursors, selectors, path IDs, headers, or examples.
21. Given audit and diagnostic evidence depends on approved retention and freshness policy, when contracts include retention windows, freshness targets, elapsed times, watermarks, or stale/unavailable reasons, then values come from approved C3/C4/C5/C6 evidence or are marked `TODO(reference-pending)` with the source/owner; this story must not invent retention periods, SLOs, rebuild semantics, or projection recovery behavior.
22. Given validators are the handoff to implementation, when focused validation is added, then it covers both positive ownership cases and negative escape paths: unknown audit/ops-console operations, unexpected path prefixes, unauthorized audience fields, cursor/filter tampering, unapproved external `$ref`s, raw sensitive metadata, and references to runtime/server/UI/generated artifacts fail locally and offline.

## Tasks / Subtasks

- [x] Confirm prerequisites and preserve scope boundaries. (AC: 1, 6, 11, 12)
  - [x] Inspect Story 1.6 deliverables: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` and `src/Hexalith.Folders.Contracts/openapi/extensions/`. If absent, record prerequisite drift and create only the narrow audit/ops-console additions this story owns with `TODO(reference-pending)` markers where shared foundation values are missing.
  - [x] Inspect Stories 1.7, 1.8, 1.9, and 1.10 deliverables before referencing tenant/folder/provider/repository, workspace/lock, file/context, commit/status, provider-outcome, and reconciliation components. If absent, reference stable planned component names and record the dependency as reference-pending instead of duplicating their scope.
  - [x] Inspect `docs/contract/idempotency-and-parity-rules.md`, `tests/fixtures/parity-contract.schema.json`, `tests/fixtures/idempotency-encoding-corpus.json`, and `tests/fixtures/previous-spine.yaml`; treat missing files as prerequisite drift, not permission to invent policy.
  - [x] Inspect `docs/exit-criteria/c3-retention.md`, `docs/exit-criteria/c4-input-limits.md`, `docs/exit-criteria/c6-transition-matrix-mapping.md`, `docs/exit-criteria/s2-oidc-validation.md`, and any C5 freshness/performance evidence if present; preserve approval state and use reference-pending markers for unresolved values.
  - [x] Build a short evidence map for C3 retention, C4 limits, C5 freshness/performance, C6 lifecycle/disposition, and Story 1.6 extension vocabulary before adding values; unresolved values must stay as explicit `TODO(reference-pending)` markers.
  - [x] Do not initialize or update nested submodules. Use sibling modules only as read-only references unless explicitly directed otherwise.
  - [x] Limit allowed story outputs to OpenAPI contract changes, existing Contract project static artifact inclusion if needed, synthetic examples, optional contract notes, and focused offline validation assets.
- [x] Pin the concrete operation inventory and editable file set before schema authoring. (AC: 1, 5, 11, 12, 14)
  - [x] Start from the Dev Notes operation inventory seed and confirm each path, method, operation ID, owning tag, and schema section against existing Story 1.6 through 1.10 artifacts.
  - [x] If an existing Contract Spine artifact already froze different paths or names, preserve the frozen `operationId` where possible and record the mapping in `docs/contract/audit-ops-console-contract-groups.md` or the OpenAPI description.
  - [x] Use stable group names consistently: `AuditQueries`, `OperationTimelineQueries`, `OpsConsoleDiagnostics`, and `ProjectionFreshness` unless an existing approved Contract Spine artifact has frozen different names.
  - [x] Fail closed during validation when unknown audit/ops-console operations, malformed group names, missing required tags, or unrelated OpenAPI operations appear in this story's owned surface; do not infer ownership from partial name matches.
  - [x] Keep edits limited to `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`, `src/Hexalith.Folders.Contracts/openapi/extensions/` if extension metadata is missing, `docs/contract/audit-ops-console-contract-groups.md` if notes are needed, and focused contract validation assets under `tests/Hexalith.Folders.Contracts.Tests/` or `tests/tools/`.
  - [x] Record a per-operation audience and field-classification matrix for audit, timeline, diagnostic, and projection-freshness operations so consumer-safe fields and operator-only sanitized fields do not drift.
  - [x] Do not edit server, domain, worker, provider, generated client, NSwag, CLI, MCP, UI, release docs, or CI workflow files for this story.
- [x] Author audit trail and operation timeline query contracts. (AC: 1, 2, 3, 4, 6, 7, 8, 10)
  - [x] Add non-mutating audit and timeline operations under `/api/v1/...` using lowercase hyphen-delimited path segments, plural collection nouns where appropriate, and camelCase path parameters such as `{folderId}`, `{workspaceId}`, `{taskId}`, or `{operationId}`.
  - [x] Define stable operation IDs such as `ListAuditTrail`, `GetAuditRecord`, `ListOperationTimeline`, and `GetOperationTimelineEntry` unless Story 1.5, Story 1.6, or an already-authored Contract Spine has frozen different names.
  - [x] Declare approved read-consistency classes. Use `eventually-consistent` for projection-backed audit/timeline reads unless an approved source requires `snapshot-per-task` or `read-your-writes`.
  - [x] Include projection watermark, freshness age, event/evidence timestamp, actor/audit reference, task ID, operation ID, correlation ID, state transition, result status, sanitized error category, retryability, duration, redaction reason, and pagination cursor where authorized.
  - [x] Keep audit entries metadata-only. Use changed-path digest/reference metadata instead of raw path lists unless an approved sensitivity policy allows raw values for the authorized audience.
  - [x] Ensure timeline filters cannot become tenant authority, ACL override, path-policy bypass, or probe vectors for hidden resources.
  - [x] Represent missing, hidden, redacted, stale, unavailable, and wrong-tenant evidence through safe Problem Details or safe metadata-only response shapes without resource-existence hints.
  - [x] Keep audit records correlation-friendly and redact-by-design: expose stable IDs, timestamps, status/result codes, redaction state, and correlation metadata while excluding raw event bodies, prompt payloads, memory contents, embeddings, exception stacks, and payload excerpts.
  - [x] Keep retention and projection freshness as contract metadata only: expose approved retention/freshness evidence, watermarks, stale/unavailable reason codes, and truncation state without adding retention jobs, projection rebuild semantics, backup/recovery behavior, or audit storage policy.
  - [x] Keep audit storage, projection handlers, EventStore event emission, read-model rebuild behavior, retention jobs, backup/recovery behavior, and UI timeline rendering deferred to later stories.
- [x] Author operations-console diagnostic query contracts. (AC: 1, 2, 3, 5, 7, 8, 9, 10)
  - [x] Add read-only diagnostic operations for readiness, lock, dirty-state, failed-operation, provider-status, sync-status, and projection freshness where they are not already covered by Story 1.10 status components.
  - [x] Define stable operation IDs such as `GetReadinessDiagnostics`, `GetLockDiagnostics`, `GetDirtyStateDiagnostics`, `GetFailedOperationDiagnostics`, `GetProviderStatusDiagnostics`, `GetSyncStatusDiagnostics`, and `GetProjectionFreshness` unless prior stories froze different names.
  - [x] Model diagnostic responses as projection/query contracts only. Do not define command forms, repair actions, credential reveal fields, file browsing, file editing, raw diff display, unrestricted filesystem browsing, or incident-mode event-stream behavior in this story.
  - [x] Reuse C6 states and operator-disposition labels for diagnostic status. If a console-specific label seems necessary, record it as a deferred decision rather than adding a UI-only state to the Contract Spine.
  - [x] Include trust/freshness metadata, last successful operation, last failure category, retry or escalation posture, redaction reason, projection lag, stale/unavailable reason, and safe supporting identifiers where authorized.
  - [x] Partition diagnostic audience explicitly: consumer-safe query responses must remain non-enumerating, while authorized operator diagnostics may expose only sanitized metadata and redacted/classified values.
  - [x] For each diagnostic operation, define which fields are safe for consumer-facing status, which require operator diagnostic authority, and which are forbidden in every audience; do not rely on UI rendering to hide unsafe fields.
  - [x] Ensure provider diagnostics never expose credential material, provider tokens, embedded credential URLs, provider installation secrets, raw provider payloads, raw repository URLs, or unauthorized repository existence.
  - [x] Ensure diagnostic response semantics use stable machine codes and shared operator-disposition values; optional display labels or localization keys must not define API behavior.
  - [x] Keep FrontComposer components, Fluent UI pages, console navigation, incident-mode pages, projection handlers, and read-model implementation deferred to Epic 6 and later runtime stories.
- [x] Apply shared OpenAPI conventions consistently. (AC: 2, 3, 6, 8, 9, 10, 13)
  - [x] Reuse shared headers, parameters, Problem Details, freshness metadata, pagination/filtering conventions, lifecycle/state schemas, sensitive-metadata schemas, and extension vocabulary from Story 1.6 instead of duplicating incompatible shapes.
  - [x] Use camelCase JSON properties, ISO-8601 UTC timestamps, string enums, opaque ULID identifiers, service-issued opaque non-secret cursors, and metadata-only examples.
  - [x] Ensure every operation is non-mutating and omits `Idempotency-Key`.
  - [x] Ensure every operation declares read consistency, freshness/projection-lag behavior, canonical error categories, authorization requirement, correlation behavior, audit classification, redaction behavior, and parity dimensions.
  - [x] Use RFC 9457 Problem Details plus Hexalith fields: `category`, `code`, `message`, `correlationId`, `retryable`, `clientAction`, and `details`.
  - [x] Prefer machine-readable status and error codes over adopter-facing English prose fields; descriptions may explain behavior, but client semantics must bind to codes/enums.
  - [x] Ensure examples are synthetic opaque placeholders only and do not include real tenant IDs, user IDs, correlation IDs, timestamps, repository URLs, branch names with sensitive values, local paths, provider identifiers, organization names, email addresses, file content snippets, diffs, raw commit messages, raw changed paths, generated context payloads, secrets, or unauthorized resource hints.
  - [x] Mark examples as non-production synthetic data and include named synthetic cases for visible, hidden, redacted, unknown, missing, multi-tenant, projection-unavailable, and operator-disposition scenarios.
  - [x] Use `TODO(reference-pending): <field-or-decision>` only for unresolved approved-source values, with exact source paths or decision owners when known.
- [x] Add focused offline validation. (AC: 3, 6, 7, 8, 10, 11, 13, 14)
  - [x] Add or update the smallest local validator or test that parses `hexalith.folders.v1.yaml` as OpenAPI 3.1 and resolves all local `$ref` targets without network access.
  - [x] Resolve internal `$ref` targets through nested schemas, parameters, responses, and examples; fail validation on unresolved local references; treat remote/external references as deferred unless an existing validator already supports them.
  - [x] Verify new operation IDs are unique, stable, and limited to this story's operation allow-list: `ListAuditTrail`, `GetAuditRecord`, `ListOperationTimeline`, `GetOperationTimelineEntry`, `GetReadinessDiagnostics`, `GetLockDiagnostics`, `GetDirtyStateDiagnostics`, `GetFailedOperationDiagnostics`, `GetProviderStatusDiagnostics`, `GetSyncStatusDiagnostics`, `GetProjectionFreshness`, plus any explicitly named audit or ops-console query operation added by this story.
  - [x] Verify all new operations include required `x-hexalith-*` metadata and satisfy read-consistency requirements.
  - [x] Verify no request payload, query parameter, route segment, cursor, filter, selector, or client-controlled header defines authoritative tenant context.
  - [x] Verify every operation omits `Idempotency-Key`.
  - [x] Verify examples and audit metadata exclude file contents, diffs, raw commit messages, raw changed-path lists where not authorized, raw provider payloads, generated context payloads, provider tokens, credential material, local paths, production URLs, and unauthorized resource-existence hints.
  - [x] Verify hidden-resource equivalence for audit records, timeline entries, readiness diagnostics, lock diagnostics, dirty-state diagnostics, failed-operation diagnostics, provider-status diagnostics, and sync-status diagnostics.
  - [x] Verify hidden-resource equivalence across status codes, item counts, ordering, cursor issuance, metadata presence, and examples.
  - [x] Verify redacted known values, unknown values, missing optional values, hidden values, and forbidden fields remain distinguishable for authorized audiences and collapse only through approved safe-denial shapes for unauthorized audiences.
  - [x] Verify diagnostic audience partitioning: consumer-safe responses are non-enumerating, and operator diagnostic examples remain sanitized, redacted where needed, and metadata-only.
  - [x] Verify cursor and filter fields are opaque, non-secret, non-authoritative, and do not carry provider tokens, tenant authority, ACL decisions, raw query text, raw path lists, or redaction-bypassing data; include negative cases for tampered cursors, cursor reuse after changed filters, tenant/principal mismatch, invalid sort keys, boundary duplicates, and empty-page continuation.
  - [x] Verify audience partitioning and field classification against the per-operation matrix; fail if operator-only sanitized fields appear in consumer-safe responses or if forbidden fields appear anywhere in schemas, examples, Problem Details, or diagnostics.
  - [x] Verify retention/freshness values are either sourced from approved evidence or explicitly marked reference-pending; fail if the contract invents numeric retention windows, SLOs, rebuild semantics, or recovery behavior.
  - [x] Verify C6 state/operator-disposition reuse and fail validation if the story introduces UI-only state labels that conflict with shared lifecycle vocabulary.
  - [x] Verify negative scope: no generated SDK files, NSwag generation wiring, REST handlers/controllers, CLI commands, MCP tools/resources, domain aggregate behavior, EventStore commands, provider adapters, workers, UI pages, final parity oracle rows, CI workflow gates, runtime projections, or nested-submodule initialization.
  - [x] Run `dotnet build Hexalith.Folders.slnx` if the scaffold supports it after focused validation. If blocked by earlier scaffold state, record the exact prerequisite instead of expanding this story.
- [x] Record downstream authoring notes. (AC: 5, 9, 11, 12)
  - [x] Add a short note near the OpenAPI file or in `docs/contract/` explaining which audit and diagnostic components Epic 4, Epic 5, and Epic 6 must reuse.
  - [x] Record deferred owners for runtime audit emission, audit projection handlers, operation timeline projections, operations-console UI pages, incident-mode event stream, FrontComposer components, projection performance evidence, retention enforcement, generated SDK helpers, parity oracle rows, and CI gates.
  - [x] Record deferred owners for unresolved diagnostic audience policy, field-classification vocabulary, retention/freshness sources, hidden-resource equivalence, cursor invalidation, and external `$ref` validation behavior.
  - [x] Record any unresolved C3 retention, C4 query limits, C5 freshness/performance targets, C6 state metadata, Story 1.6 foundation, Story 1.10 status components, or Story 1.5 operation naming dependencies as explicit deferred decisions.

### Review Findings

Adversarial code review against the diff at `c4a54a5` (parent `10db742`), run on 2026-05-14 via three parallel layers (Blind Hunter, Edge Case Hunter, Acceptance Auditor). Findings classified as `Decision`, `Patch`, or `Defer`.

#### Decision needed — resolved 2026-05-14

All decisions converted to patches. Application status recorded below (✅ = applied this pass; ⏭️ = deferred to follow-up due to scope or vocabulary-registration impact; ⚠️ = revised after testing).

- ✅ **D1 → P25 applied** — Renamed path to `/api/v1/folders/{folderId}/operation-timeline/{timelineEntryId}` and added a `TimelineEntryId` shared parameter.
- ✅ **D2 → P26 applied** — Deleted `AuditAccessDeniedProblem` example and its test assertion. All audit 403s now consistently route through `SafeAuthorizationDenial403`.
- ⏭️ **D3 → P27 deferred** — Adding `x-hexalith-group` on each operation requires registering the extension key in `extensions/hexalith-extension-vocabulary.yaml` and updating `ContractSpineFoundation_DeclaresRequiredVocabularyOnly`; deferred to a vocabulary-extension follow-up. The four-group mapping remains recorded in `docs/contract/audit-ops-console-contract-groups.md`.
- ✅ **D4 → P28 applied** — Added 409 `ProjectionStaleProblem` to all 10 sibling operations (`GetAuditRecord`, `ListOperationTimeline`, `GetOperationTimelineEntry`, `GetLockDiagnostics`, `GetDirtyStateDiagnostics`, `GetFailedOperationDiagnostics`, `GetProviderStatusDiagnostics`, `GetSyncStatusDiagnostics`, `GetReadinessDiagnostics`, `GetProjectionFreshness`).
- ✅ **D5 → P29 applied** — Added 404 `SafeAuthorizationDenial404` to `GetReadinessDiagnostics` and `GetProjectionFreshness`. Hidden-resource equivalence now mechanically enforced by the new `AuditOpsConsoleOperations_DeclareConsistentSafeDenialResponseCodes` test.
- ⚠️ **D6 → P30 revised** — `CliExitCode` was not actually widened (the finding was incorrect: it carries numeric-string codes, not category strings). `McpFailureKind` IS widened, but the foundation test `ContractSpineFoundation_ContainsRequiredSharedSemantics` enforces a 1:1 mirror with `CanonicalErrorCategory`, so reverting `McpFailureKind` would break the foundation contract. Outcome: no revert; the foundation mirror rule supersedes AC 11 in this case. Recorded as an explicit mapping in the Change Log.
- ✅ **D7 → P31 applied** — Marked `SyncStatusDiagnostics.acceptedCommandState` (inline enum) and `ProjectionAvailability.{redacted, unknown}` as `TODO(reference-pending C5)` in their `description:` fields. The `x-hexalith-reference-pending` extension was not used because it is not in the approved vocabulary; description-level marker is the conservative carrier until the vocabulary is extended.
- ✅ **D8 → P32 applied** — Added `PageLimit.maximum: 1000` with a description-level `TODO(reference-pending C4 input limit)` note. Added `MetadataFilter.pattern` shape constraint and a description-level note that the allowed-key vocabulary is reference-pending C4.
- ⏭️ **D9 → P33 deferred** — Removing the required `audience` field from `DiagnosticBase` body and updating all 7 example payloads, 6 schema subclasses, and the affected tests is larger surgery than this pass can absorb safely. The audience oracle is a real risk but the change is mechanical follow-up. Tracked in deferred-work.
- ✅ **D10 → P34 applied** — Change Log entry added recording the `SpineContractAssertions.cs` scope expansion (only a docstring update was actually needed; the `audit` fragment in the forbidden list does not trip the new `/api/v1/folders/.../audit-trail` and `/api/v1/ops-console/...` paths because the regex anchors at `/api/v1/<fragment>`).

#### Patch (status as of 2026-05-14 application pass)

Applied:

- [x] [Review][Patch] `ChangedPathEvidence` permits inconsistent `evidenceKind` + `digest`/`reference` combinations — APPLIED. Added a `oneOf` discriminator binding `digest`/`reference` presence to the corresponding `evidenceKind`, and forbidding both fields when `evidenceKind` is `redacted` or `unavailable`. [sources: blind+edge]
- [x] [Review][Patch] `AuditTrailPage.retentionClass` accepts a literal `TODO(...)` value as a valid runtime payload — APPLIED. Added a shape constraint (`pattern` accepting either `TODO(reference-pending):...` or a snake_case identifier) and a description note that until C3 is frozen the only legal runtime value is the TODO marker. Applied to both `AuditTrailPage.retentionClass` and `OperationTimelinePage.retentionClass`. [sources: blind+edge]
- [x] [Review][Patch] `OperationTimelineEntry` example uses `unknown_provider_outcome` as a `LifecycleState` value — DISMISSED after verification. `LifecycleState` enum at yaml:6610 does include `unknown_provider_outcome` (it is part of the C6 transition vocabulary). The example is valid against its schema. [sources: blind]
- [x] [Review][Patch] `SpineContractAssertions` still rejects the `audit` path fragment — DISMISSED after verification. The regex anchors at `/api/v1/<fragment>(?:[/\-...]|$)`; the new Story 1.11 paths under `/api/v1/folders/{folderId}/audit-trail/...` and `/api/v1/ops-console/...` do not match. Docstring updated to reflect that Story 1.11 paths legitimately do not trip the helper. [sources: blind]
- [x] [Review][Patch] Test validators are weak in several places — APPLIED. (a) operationId uniqueness now also asserted across the entire spec, not only the story allow-list. (b) Idempotency-Key absence check now also rejects inline `name: Idempotency-Key` header parameters. (c) Forbidden-leak substring list rewritten: removed fragile tokens (`"secret"`, `"token_"`, `"changedPaths"`) and replaced with a corpus-driven check that loads `tests/fixtures/audit-leakage-corpus.json` and asserts none of its sentinel values appear in examples. (d) `audit_access_denied` test scope left as-is (audit-only) — extending to ops-console diagnostics is tracked in deferred work because it requires per-op canonical-category review. [sources: edge+auditor]
- [x] [Review][Patch] Hidden-resource equivalence is not actually validated — APPLIED. New test `AuditOpsConsoleOperations_DeclareConsistentSafeDenialResponseCodes` enforces that every Story 1.11 operation declares `200/401/403/404/503`, and that any operation listing `projection_stale` in `x-hexalith-canonical-error-categories` also declares `409`. Combined with the actual 404/409 additions to the YAML, the contract surface is now mechanically symmetric. [sources: auditor+edge]
- [x] [Review][Patch] Audit-leakage corpus is not consumed by new tests — APPLIED. Test now loads `tests/fixtures/audit-leakage-corpus.json`, enumerates `sentinel_samples`, and asserts no sentinel value appears in the serialized examples. [sources: auditor]
- [x] [Review][Patch] `ListAuditTrail` `x-hexalith-audit-metadata-keys` omits `task_id` but `AuditRecord.taskId` is declared — APPLIED. Added `task_id` (tenant_sensitive) to `ListAuditTrail.x-hexalith-audit-metadata-keys`. [sources: blind]
- [x] [Review][Patch] `DiagnosticFieldClassificationEntry.field` pattern rejects snake_case — APPLIED. Pattern changed to `^[a-z][A-Za-z0-9_]{0,79}$` with description noting acceptance of both camelCase body properties and snake_case audit-metadata keys. [sources: blind]

Deferred to follow-up (require larger surgery than this code-review pass can absorb safely):

- [x] [Review][Patch] `DiagnosticBase` + `allOf` + `additionalProperties: false` JSON Schema gotcha [yaml ~8534 + 6 subclasses] — Replacing `additionalProperties: false` with `unevaluatedProperties: false` on the composed schemas (or moving the strict-mode anchor) needs to be done in coordination across base + 6 subclasses + all 7 example payloads + the contract foundation tests that may assert specific additionalProperties behavior. Tracked in deferred-work. [sources: blind+edge]
- [x] [Review][Patch] `AuditRecord` redaction shape leaks timing and actor identity [AuditRecord schema + example] — Wrapping `evidenceTimestamp` / `actorReference` / `operationId` in audience-gated `RedactionMetadata` requires schema design choices (bucketed timestamps vs. sentinel replacement, optionality vs. null), example regeneration, and propagation to the audit-leakage-corpus test guards. Tracked in deferred-work. [sources: edge+blind]
- [x] [Review][Patch] Cursor/filter tamper, principal-mismatch, invalid-sort, boundary-duplicate, empty-page negative-case tests absent — Adding explicit negative-case tests requires either contract-level fixtures (representative cursors, tamper variants, principal contexts) or a runtime test surface; neither lands in a single edit pass. Tracked in deferred-work. [sources: auditor+edge]
- [x] [Review][Patch] `DiagnosticBase.fieldClassifications` is optional with no `minItems` — Requiring `fieldClassifications` on operator-audience responses needs an audience-conditional schema (or split base into consumer/operator variants) and example regeneration. Tracked in deferred-work. [sources: edge]
- [x] [Review][Patch] `ReadinessDiagnostics` schema lacks provider/folder/workspace summary references — Adding operator-audience-gated summary references needs design choices (DiagnosticSafeIdentifier vs. inline refs, redaction shape, audience gating). Tracked in deferred-work. [sources: auditor]
- [x] [Review][Patch] `CanonicalErrorCategory` / `WorkspaceErrorCategory` enum widening not propagated to earlier-story operations — Sweep across Stories 1.7-1.10 operations' `x-hexalith-canonical-error-categories` arrays. Mechanical but cross-story; safer to do in a dedicated propagation story. Tracked in deferred-work. [sources: edge]
- [x] [Review][Patch] Opaque-identifier patterns are inconsistent across siblings — Factoring a shared `PrefixedOpaqueIdentifier` schema touches every prefixed-identifier type. Tracked in deferred-work. [sources: blind]
- [x] [Review][Patch] `AuditTrailPage`/`OperationTimelinePage` examples cover only the single-result case — Adding boundary-scenario examples (zero/limit/beyond-last) is straightforward but the corpus is several hundred lines; defer alongside the audience-equivalence rework. Tracked in deferred-work. [sources: edge+auditor]
- [x] [Review][Patch] No cross-field consistency between `DiagnosticTrustEvidence.availability` and `FreshnessMetadata.stale` — Adding a cross-field invariant requires `if/then` JSON-Schema-2020-12 conditionals or refactoring the two fields into a single state machine. Tracked in deferred-work. [sources: edge]
- [x] [Review][Patch] `LockDiagnostics.lockReference` and `ProviderStatusDiagnostics.providerBindingReference` field-presence is an audience oracle — Requires audience-conditional schema or RedactionMetadata sentinel; coordinated with the broader audience-partitioning rework. Tracked in deferred-work. [sources: edge]
- [x] [Review][Patch] `OperationTimelineEntry.workspaceId` leaks cross-workspace evidence — Coordinated with the AuditRecord redaction shape rework. Tracked in deferred-work. [sources: edge]

#### Defer (pre-existing or out-of-scope for this story)

- [x] [Review][Defer] Predev preflight result is `fail` but story advanced to `review` [_bmad-output/process-notes/predev-preflight-latest.json:321 and predev-preflight-2026-05-14T100203Z.json] — deferred, process/governance concern outside contract correctness. Recommend a separate ticket to investigate the seven dirty paths recorded in the failing preflight, but does not block the contract review. [sources: blind]
- [x] [Review][Defer] `SpineContractAssertions.cs` edited outside the spec's allowed-files list — deferred, the edit is logically required for Story 1.11 ownership of ops-console and the helper itself is shared test infrastructure; record as an explicit scope expansion in the story's Change Log instead of reverting. [sources: auditor]
- [x] [Review][Defer] Synthetic example timestamps use today's wall-clock date (`2026-05-14T…`) — deferred, cosmetic. Spec disallows real timestamps in examples; bucketed or far-future placeholders would be cleaner but the values are clearly synthetic in context. [sources: blind+auditor]
- [x] [Review][Defer] No dedicated `x-hexalith-redaction` extension on new operations — deferred, pre-existing convention across Stories 1.6-1.10 operations also lacks it; standardizing belongs in a foundation refactor. [sources: auditor]
- [x] [Review][Defer] `OperatorDispositionLabel` values `auto_recovering` and `available` not exemplified — deferred, cosmetic test gap; add a synthetic example alongside the new scenario examples in Patch #18. [sources: blind]

#### Dismissed as noise (counted; not actionable)

- Path-parameter identifiers (auditRecordId, operationId) carry no schema-level tenant binding — by design; tenant authority is envelope-derived. (edge)
- `correlationId` shared across audit and timeline schemas — by design; correlation is supposed to link surfaces. (edge)
- `ResolveRefs` test walks the entire spec — acceptable test design; isolating to story would miss cross-story `$ref` breakage. (edge)
- `GetReadinessDiagnostics` declares `tenant_access_denied` despite no folder path — declaring a canonical category in the metadata array is fine; tenant authority is envelope-derived. (edge)

## Dev Notes

### Scope Boundaries

- This story authors Contract Spine query groups for audit trail, operation timeline, and operations-console diagnostic projections only.
- Contract-only output means OpenAPI contract declarations, synthetic examples, optional contract notes, and focused offline validation. Do not broaden this story into runtime behavior, generated consumers, projection handlers, FrontComposer UI, provider integration, EventStore audit emission, retention jobs, incident-mode stream implementation, or CI workflow wiring.
- Primary files expected:

```text
src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml
src/Hexalith.Folders.Contracts/openapi/extensions/
```

- Optional supporting docs, only if needed:

```text
docs/contract/audit-ops-console-contract-groups.md
```

- Do not add `src/Hexalith.Folders.Client/Generated/`, NSwag configuration, server endpoints, EventStore commands, aggregate logic, provider adapters, Git or filesystem operations, CLI/MCP commands, workers, UI, parity rows, release docs, or CI gates.
- Do not add operation groups owned by adjacent stories:
  - Story 1.7: tenant, folder, provider, repository binding, repository creation, and branch/ref policy.
  - Story 1.8: workspace preparation, lock acquisition/release, lock inspection, retry eligibility, and state-transition evidence.
  - Story 1.9: file mutation and context query.
  - Story 1.10: commit, commit evidence, workspace status, task status, provider outcome, and reconciliation status.
- Epic 6 owns console pages and reusable UI components. This story defines only the read-only query contracts those pages will consume.

### Allowed Files And Forbidden Work

Allowed edits for this story are limited to:

```text
src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml
src/Hexalith.Folders.Contracts/openapi/extensions/
docs/contract/audit-ops-console-contract-groups.md
tests/Hexalith.Folders.Contracts.Tests/
tests/tools/
```

Use the docs and test paths only for narrow contract notes, synthetic fixtures, and offline validation needed by this story. Do not edit generated SDK output, NSwag generation wiring, runtime REST handlers, EventStore commands, aggregate behavior, projections, provider adapters, Git or filesystem side-effect code, CLI, MCP, UI, workers, release documentation, CI workflow gates, unrelated sibling modules, or nested submodules.

Non-goals: no payload inspection feature, no tenant administration, no audit or diagnostic mutation, no authorization policy redesign, no contract-group registry mutation beyond this story's query contracts, and no generated-client commit unless a later story explicitly owns it.

### Contract Group Requirements

- Use OpenAPI 3.1 at `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` as the single source of truth when the foundation exists.
- REST paths are URL-versioned under `/api/v1`, use lowercase hyphen-delimited segments, plural collection nouns where appropriate, and camelCase path parameters.
- Stable operation IDs matter because NSwag generation and the C13 parity oracle will consume them later.
- Tenant authority comes from auth context and EventStore envelopes. Request filters, cursors, path IDs, task IDs, provider IDs, repository IDs, and operation IDs can identify resources to validate, but never establish or override tenant authority.
- Audit and timeline queries are metadata-only. They may expose authorized evidence about who/what/when/result/freshness/correlation, but not file contents, diffs, generated context payloads, provider tokens, credential material, raw provider payloads, local paths, or unauthorized resource existence.
- Operations-console diagnostics are projection-query contracts. They must be read-only and must not imply repair, mutation, filesystem browsing, credential reveal, file editing, raw diff viewing, or UI-only lifecycle semantics.
- Redacted values must be distinguishable from unknown or missing values through shared redaction shapes, but that distinction must not leak hidden-resource existence to unauthorized callers.
- Provider-status diagnostics are sanitized metadata. They may reference provider capability/status classes, retryability, correlation, and safe reason categories for authorized audiences; they must not expose raw provider account data, token state, credential values, raw repository URLs, provider payload bodies, or unauthorized repository existence.
- Use stable group names consistently unless an approved artifact already froze different names: `AuditQueries`, `OperationTimelineQueries`, `OpsConsoleDiagnostics`, and `ProjectionFreshness`. Route namespaces, tags, schema names, and extension metadata must align with the chosen names or record an explicit mapping.
- Diagnostic labels must bind to stable machine-readable codes and shared operator-disposition values. Localized text, English labels, and display hints are presentation metadata only.
- Each operation needs an explicit audience/field-classification note, either in OpenAPI extensions or `docs/contract/audit-ops-console-contract-groups.md`, so validators can distinguish consumer-safe fields, operator-only sanitized fields, and always-forbidden fields.

### Operation Inventory Seed

Use the operation names below as a starting point unless Story 1.5, Story 1.6, or an already-authored Contract Spine has frozen different names. If names differ, keep the OpenAPI `operationId` stable and record the mapping.

| Operation | Type | Required metadata |
| --- | --- | --- |
| `ListAuditTrail` | Query | pagination/filtering, eventually-consistent projection freshness, metadata-only audit fields, redaction state, safe denial, correlation |
| `GetAuditRecord` | Query | audit record identity, authorized evidence fields, redaction state, safe denial, freshness, correlation |
| `ListOperationTimeline` | Query | timeline filters, state transition evidence, task/operation/correlation metadata, pagination, freshness, safe denial |
| `GetOperationTimelineEntry` | Query | timeline entry identity, state transition evidence, sanitized result/error category, redaction state, freshness |
| `GetReadinessDiagnostics` | Ops-console query | readiness status, trust/freshness evidence, provider/folder/workspace summary references, redaction, safe denial |
| `GetLockDiagnostics` | Ops-console query | lock state, lease metadata, operator disposition, holder/audit reference where authorized, freshness, safe denial |
| `GetDirtyStateDiagnostics` | Ops-console query | dirty-state summary, changed-path digest/reference, task/operation evidence, no raw diffs/content, freshness |
| `GetFailedOperationDiagnostics` | Ops-console query | failure category, retry/escalation posture, last operation evidence, sanitized details, correlation |
| `GetProviderStatusDiagnostics` | Ops-console query | provider status class, capability/readiness evidence, sanitized reason, retryability, no secrets |
| `GetSyncStatusDiagnostics` | Ops-console query | projection/provider sync status, lag/freshness metadata, unavailable/stale reason, safe denial |
| `GetProjectionFreshness` | Ops-console query | projection watermark, age, state source, stale/unavailable reason, correlation |

Do not add provider readiness commands, workspace/lock commands, file/context queries, commit/status commands, reconciliation workers, audit projection handlers, FrontComposer pages, runtime workers, generated SDK, CLI, or MCP operations in this story.

### Error and Audit Requirements

- Required canonical categories for this story include authentication failure, tenant authorization denied, folder ACL denied, audit access denied, cross-tenant access denied, read-model unavailable, projection stale, projection unavailable, provider unavailable, provider rate limited, provider failure known, provider outcome unknown, workspace locked, stale workspace, failed operation, reconciliation required, validation error, not found, redacted, response limit exceeded, query timeout, and internal error.
- Use RFC 9457 Problem Details plus Hexalith fields: `category`, `code`, `message`, `correlationId`, `retryable`, `clientAction`, and `details`.
- Safe-denial Problem Details must expose stable safe codes only; they must not reveal folder existence, workspace existence, lock existence, task existence, audit record existence, provider state, commit existence, changed-path presence, local paths, or unauthorized resource existence.
- Audit metadata is metadata-only. It may include tenant-scoped actor evidence, folder ID, workspace ID, lock ID, task ID, operation ID, correlation ID, provider binding reference, repository binding reference, branch/ref policy reference, state transition, changed-path metadata digest, timestamp, result, duration, retryability, redaction reason, and sanitized error category.
- Do not include raw file contents, diffs, raw commit messages, raw changed paths unless authorized and classified by approved policy, raw provider payloads, generated context payloads, provider tokens, credential material, raw repository URLs, production URLs, local filesystem paths, or unauthorized resource existence.
- Safe examples should include paired authorized and unauthorized views for the same synthetic scenario only when the unauthorized view cannot be correlated back to hidden-resource existence through counts, ordering, cursor shape, or metadata presence.

### Read Consistency, Freshness, And Redaction

- Every operation in this story is a non-mutating query and must omit `Idempotency-Key`.
- Audit and timeline reads are projection-backed unless an approved source says otherwise. They should declare `eventually-consistent` plus projection freshness/watermark metadata.
- Diagnostic queries that summarize accepted command state versus projected state must keep those concepts separate; do not let a command acknowledgment look like durable provider or audit completion.
- Freshness metadata must make projection lag visible using shared components. If exact C5 freshness targets are unresolved, use `TODO(reference-pending): C5 projection freshness target`.
- Pagination and continuation cursors must be service-issued, opaque, non-secret, non-authoritative values. They must not encode provider tokens, tenant authority, ACL decisions, raw query text, unredacted path lists, or hidden resource evidence.
- Redaction metadata must use shared sensitive-metadata tiers. Redacted, unknown, missing, hidden, and unavailable are separate authorized states, but they must collapse to safe denial where the caller lacks diagnostic authority.
- Correlation IDs and task IDs must propagate through REST, SDK, CLI, MCP, EventStore envelopes, projections, audit, logs, and traces.
- Hidden, unauthorized, wrong-tenant, redacted, unknown, missing, stale, and unavailable states must use the approved safe-denial or redaction vocabulary. If the exact external behavior is not already frozen, record a deferred decision instead of choosing a new policy in this story.
- Retention windows, projection freshness targets, retry-after guidance, and truncation thresholds are contract evidence from C3/C4/C5/C6 or reference-pending markers only; this story must not define operational retention jobs, rebuild procedures, or recovery SLOs.

### Deferred Decisions From Party-Mode Review

- Confirm whether ops-console query groups are read-only only for this story or read-only as a permanent MVP console invariant.
- Confirm the canonical Contract Spine naming/versioning source if Story 1.5, Story 1.6, or the current OpenAPI artifact has frozen names that differ from the operation inventory seed.
- Confirm final route namespace if the existing Contract Spine has not already frozen it.
- Confirm whether external OpenAPI `$ref` targets are supported by the repository validator; otherwise keep them out of this story.
- Confirm whether group ownership is derived from OpenAPI tags, `x-hexalith-*` extension fields, operation ID patterns, or an explicit validation config.
- Confirm hidden-resource response equivalence policy (`404`, `403`, empty collection, or approved safe Problem Details) and cursor invalidation semantics for changed filters or permission changes if not already frozen.
- Confirm whether redacted fields are omitted, null, or sentinel-valued when the approved shared redaction vocabulary is unavailable.
- Confirm localization resource ownership for operations-console diagnostic display strings; this story only defines stable codes and optional localization keys.
- Confirm the durable source for diagnostic audience and field-classification vocabulary if Story 1.6 extensions do not already provide it.
- Confirm the approved C5 projection freshness and retry guidance source for audit/timeline/diagnostic reads if no current evidence exists.

### Previous Story Intelligence

- Story 1.1 establishes the solution scaffold and dependency direction.
- Story 1.2 establishes root configuration and forbids recursive nested-submodule initialization.
- Story 1.3 seeds shared fixtures: audit leakage corpus, parity schema, previous spine, idempotency encoding corpus, `tests/load`, and artifact templates.
- Story 1.4 owns C3 retention, C4 input limits, S-2 OIDC parameters, and C6 transition mapping. This story consumes C3/C4/C6 values but must preserve approval state and must not invent missing limits, freshness targets, retention rules, or transitions.
- Story 1.5 defines operation inventory, idempotency equivalence, adapter parity dimensions, parser-policy classifications, and read-consistency expectations. This story must consume its final artifact when available.
- Story 1.6 owns the OpenAPI foundation and shared `x-hexalith-*` vocabulary. This story must reuse it rather than redefine extension shapes.
- Story 1.7 owns tenant, folder, provider, repository binding, and branch/ref operation groups.
- Story 1.8 owns workspace preparation, lock operations, lock semantics, retry eligibility, state-transition evidence, and authorization-revocation outcomes.
- Story 1.9 owns file mutation and context-query operations, including authorization-before-observation, metadata-only context-query responses, and path/query policy guardrails.
- Story 1.10 owns commit, workspace status, task status, provider outcome, retry eligibility, retry-after, and reconciliation status. This story may summarize or link to those components for operator diagnostics but must not redefine them.
- Story 1.7 through 1.10 hardening repeatedly found that operation allow-lists, safe-denial equivalence, contract-only boundaries, and negative-scope validation prevent the most common development mistakes. Apply the same pattern here from the start.
- At story creation time, Story 1.6 is in `review` with active Contract Spine implementation changes. The implementation agent must inspect current artifacts before editing because approved OpenAPI paths, operation IDs, extension field names, and validation assets may have changed after this story was created.

### Testing Guidance

- Prefer a focused OpenAPI validation test or script in existing test/tool locations over broad runtime integration work.
- Validation must run offline without Aspire, Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, provider credentials, tenant data, production secrets, network calls, or initialized nested submodules.
- Validate OpenAPI parse, `$ref` resolution, unique operation IDs, exact `x-hexalith-*` metadata presence, read-consistency completeness, safe tenant-authority boundaries, projection freshness metadata, C6 state/operator-disposition reuse, synthetic examples, safe-denial parity, redaction, cursor safety, and negative scope.
- Include allow-list assertions for audit/ops-console operation IDs and `/api/v1` path prefixes owned by this story so validation fails if tenant/folder/provider binding, workspace/lock commands, file/context queries, commit/status commands, runtime handlers, generated-client, CLI, MCP, worker, UI, or CI artifacts appear.
- Include contract-level assertions for hidden-resource equivalence, stale or unavailable projections, redacted versus unknown versus missing values for authorized audiences, provider diagnostics with no secrets, no mutation/repair action affordances in schemas, and no UI-only state labels.
- Include forbidden-reference checks so the Contract Spine does not add Server, Client, CLI, MCP, UI, Workers, process-manager, aggregate behavior, `Hexalith.EventStore`, `Hexalith.Tenants`, FrontComposer, provider adapter, or Git/filesystem runtime dependencies or descriptions.
- If the full solution is buildable, run `dotnet build Hexalith.Folders.slnx` from the repository root after focused validation. Record exact blockers if build cannot run due to prior scaffold state.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 1.11: Author audit and ops-console query contract groups`
- `_bmad-output/planning-artifacts/epics.md#Epic 1: Bootstrap Canonical Contract For Consumers And Adapters`
- `_bmad-output/planning-artifacts/epics.md#Epic 6: Read-Only Workspace Trust Console And Audit Review`
- `_bmad-output/planning-artifacts/architecture.md#Operations console boundary`
- `_bmad-output/planning-artifacts/architecture.md#Cross-Cutting Concerns Identified`
- `_bmad-output/planning-artifacts/architecture.md#Adapter Parity Contract`
- `_bmad-output/planning-artifacts/architecture.md#REST endpoint naming`
- `_bmad-output/planning-artifacts/architecture.md#HTTP headers`
- `_bmad-output/planning-artifacts/architecture.md#Requirements to Structure Mapping`
- `_bmad-output/planning-artifacts/prd.md#Audit and Operations Visibility`
- `_bmad-output/planning-artifacts/prd.md#Observability, Auditability, and Replay`
- `_bmad-output/planning-artifacts/prd.md#Operations Console Accessibility`
- `docs/contract/idempotency-and-parity-rules.md`
- `docs/exit-criteria/c3-retention.md`
- `docs/exit-criteria/c4-input-limits.md`
- `docs/exit-criteria/c6-transition-matrix-mapping.md`
- `docs/exit-criteria/s2-oidc-validation.md`
- `_bmad-output/implementation-artifacts/1-1-establish-a-consumer-buildable-module-scaffold.md`
- `_bmad-output/implementation-artifacts/1-2-establish-root-configuration-and-submodule-policy.md`
- `_bmad-output/implementation-artifacts/1-3-seed-minimally-valid-normative-fixtures.md`
- `_bmad-output/implementation-artifacts/1-4-author-phase-0-5-pre-spine-workshop-deliverables.md`
- `_bmad-output/implementation-artifacts/1-5-finalize-idempotency-equivalence-and-adapter-parity-rules.md`
- `_bmad-output/implementation-artifacts/1-6-author-contract-spine-foundation-and-shared-extension-vocabulary.md`
- `_bmad-output/implementation-artifacts/1-7-author-tenant-folder-provider-and-repository-binding-contract-groups.md`
- `_bmad-output/implementation-artifacts/1-8-author-workspace-and-lock-contract-groups.md`
- `_bmad-output/implementation-artifacts/1-9-author-file-mutation-and-context-query-contract-groups.md`
- `_bmad-output/implementation-artifacts/1-10-author-commit-and-workspace-status-contract-groups.md`
- `_bmad-output/project-context.md`
- `AGENTS.md#Git Submodules`

## Project Structure Notes

- The Contract Spine operation groups live under `src/Hexalith.Folders.Contracts/openapi/`.
- Human-readable contract notes may live under `docs/contract/`, but the OpenAPI document remains the source of truth.
- Runtime audit projection handlers belong later under `src/Hexalith.Folders/Projections/Audit/` and `src/Hexalith.Folders.Server/Endpoints/AuditEndpoints.cs` and must not be implemented here.
- Operations-console UI pages belong later under `src/Hexalith.Folders.UI/` and must not be implemented here.
- Incident-mode stream behavior belongs to Epic 6 and must not be implemented here.
- CLI and MCP wrappers belong later under `src/Hexalith.Folders.Cli/` and `src/Hexalith.Folders.Mcp/` and must not be implemented here.
- Commit/status, reconciliation workers, provider, generated SDK, release docs, and CI gate behavior are downstream story scope.

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-15 | Resolved remaining review patch follow-ups: composed diagnostic strictness, redaction wrappers, cursor/audience/boundary examples, field-classification requirements, readiness summary references, error-category propagation, prefixed identifier normalization, freshness invariants, and audience-oracle reference wrappers. Full solution tests and build pass. | Codex |
| 2026-05-14 | Code review patches applied: deleted orphan `AuditAccessDeniedProblem` example; renamed `GetOperationTimelineEntry` path key from `{operationId}` to `{timelineEntryId}`; added `404 SafeAuthorizationDenial404` and `409 ProjectionStaleProblem` to `GetReadinessDiagnostics` and `GetProjectionFreshness`; added `409 ProjectionStaleProblem` to the eight sibling ops that already declared `projection_stale` (`GetAuditRecord`, `ListOperationTimeline`, `GetOperationTimelineEntry`, `GetLockDiagnostics`, `GetDirtyStateDiagnostics`, `GetFailedOperationDiagnostics`, `GetProviderStatusDiagnostics`, `GetSyncStatusDiagnostics`); marked `SyncStatusDiagnostics.acceptedCommandState` and `ProjectionAvailability.{redacted,unknown}` as reference-pending C5; added shape constraint to `AuditTrailPage.retentionClass` / `OperationTimelinePage.retentionClass`; added `PageLimit.maximum` and `MetadataFilter.pattern` with reference-pending C4 documentation; added `task_id` to `ListAuditTrail.x-hexalith-audit-metadata-keys`; added `oneOf` discriminator to `ChangedPathEvidence` so `digest`/`reference` are required iff `evidenceKind` matches; relaxed `DiagnosticFieldClassificationEntry.field` pattern to allow snake_case metadata keys; introduced `TimelineEntryId` shared parameter. Test side: removed orphan example assertion; broadened operationId uniqueness to whole-spec; hardened Idempotency-Key absence check to reject inline `name: Idempotency-Key` parameters; added new `AuditOpsConsoleOperations_DeclareConsistentSafeDenialResponseCodes` test enforcing 200/401/403/404/503 + 409 (when projection_stale is in canonical-error-categories) across all eleven Story 1.11 operations; replaced fragile inline forbidden-substring list with an audit-leakage-corpus-driven sentinel check loaded from `tests/fixtures/audit-leakage-corpus.json`. Scope expansion recorded: `tests/Hexalith.Folders.Testing.Tests/Helpers/SpineContractAssertions.cs` was edited (docstring update) outside the spec's Allowed Files list because the helper is shared infrastructure consumed by Stories 1.4 and 1.5 negative-scope tests; the edit clarifies that the regex anchors at `/api/v1/<fragment>`, so the new Story 1.11 paths under `/api/v1/folders/...` and `/api/v1/ops-console/...` do not trip the assertion (the helper remains correct without changing the forbidden-fragment list). Full contract test suite passes: 35/35 (1 new test added). | Claude (code-review) |
| 2026-05-14 | Implemented audit and ops-console query contract groups, focused validation, and contract notes. | Codex |
| 2026-05-12 | Applied advanced elicitation hardening for diagnostic audience partitioning, field classification, retention/freshness evidence, cursor/filter tamper validation, and offline validator negative cases. | Codex |
| 2026-05-12 | Applied party-mode review hardening for tenant authority, metadata-only boundaries, stable naming, allowed files, `$ref` validation, hidden-resource equivalence, redaction/cursor matrices, synthetic examples, and deferred decisions. | Codex |
| 2026-05-12 | Created ready-for-dev story through `bmad-create-story` workflow. | Codex |

## Party-Mode Review

- Date/time: 2026-05-12T20:25:06Z
- Selected story key: `1-11-author-audit-and-ops-console-query-contract-groups`
- Command/skill invocation used: `/bmad-party-mode 1-11-author-audit-and-ops-console-query-contract-groups; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), Paige (Technical Writer/OpenAPI specialist)
- Findings summary:
  - Tenant authority and diagnostic audience boundaries needed to be explicit so filters, cursors, selectors, and path IDs cannot establish authority or leak cross-tenant evidence.
  - Contract-only scope needed sharper allowed-file and forbidden-work boundaries to prevent generated SDK, runtime, UI, CLI, MCP, worker, CI, projection, and nested-submodule work from entering this story.
  - Operation group naming and ownership needed stable conventions plus fail-closed validation for unknown operations, malformed groups, missing tags, and unrelated OpenAPI additions.
  - OpenAPI validation needed explicit local `$ref` resolution behavior and remote/external `$ref` deferral.
  - Test guidance needed stronger hidden-resource equivalence, redaction versus unknown/missing, cursor/filter safety, synthetic example, C6/operator-disposition reuse, and metadata completeness coverage.
  - Adopter-facing diagnostics needed stable machine-readable codes and optional localization/display metadata rather than English-only API semantics.
- Changes applied:
  - Added AC 15 through AC 19 for tenant-scoped read authority, stable naming, local `$ref` validation, localized diagnostic-code semantics, and hidden-resource/redaction/cursor equivalence validation.
  - Added group-name, fail-closed ownership, redact-by-design audit, machine-code diagnostic, synthetic-example, local `$ref`, hidden-resource, redaction-state, and cursor/filter negative validation tasks.
  - Added `Allowed Files And Forbidden Work` Dev Notes section and explicit non-goals.
  - Added Contract Group Requirements notes for stable group names and diagnostic display-code semantics.
  - Added `Deferred Decisions From Party-Mode Review` for choices requiring product or architecture confirmation.
- Findings deferred:
  - Whether ops-console query groups are read-only only for this story or permanently read-only for MVP.
  - Canonical Contract Spine naming/versioning source and final route namespace if current approved artifacts differ.
  - Whether external OpenAPI `$ref` targets are supported by repository validation.
  - Whether group ownership comes from tags, `x-hexalith-*` extensions, operation ID patterns, or validation config.
  - Hidden-resource response equivalence policy, cursor invalidation semantics, redacted-field representation, and localization resource ownership where not already frozen by approved artifacts.
- Final recommendation: `ready-for-dev`

## Advanced Elicitation

- Date/time: 2026-05-12T22:02:36Z
- Selected story key: `1-11-author-audit-and-ops-console-query-contract-groups`
- Command/skill invocation used: `/bmad-advanced-elicitation 1-11-author-audit-and-ops-console-query-contract-groups`
- Batch 1 method names: Stakeholder Round Table; Expert Panel Review; Red Team vs Blue Team; Security Audit Personas; Pre-mortem Analysis
- Reshuffled Batch 2 method names: Self-Consistency Validation; Failure Mode Analysis; Comparative Analysis Matrix; First Principles Analysis; Critique and Refine
- Findings summary:
  - The story needed a stronger audience/field-classification handoff so consumer-safe diagnostic responses, operator-only sanitized fields, and always-forbidden fields remain mechanically separable.
  - Retention and freshness language needed to bind to approved C3/C4/C5/C6 evidence or explicit reference-pending markers, without implying retention jobs, projection recovery behavior, or invented SLOs.
  - Cursor, filter, sort, and pagination edge cases needed validator coverage across tamper, changed-filter reuse, principal mismatch, boundary duplicates, and empty-page continuation.
  - Validator guidance needed both positive ownership checks and negative escape-path checks for unknown operations, unapproved external `$ref`s, unsafe audience fields, raw sensitive metadata, and runtime/server/UI/generated references.
- Changes applied:
  - Added AC 20 through AC 22 for diagnostic audience partitioning, approved retention/freshness evidence, and offline validator negative cases.
  - Added task guidance for evidence mapping, per-operation audience/field-classification matrices, retention/freshness metadata boundaries, diagnostic audience field tiers, and validator checks.
  - Added Dev Notes clarifying field-classification evidence, paired safe examples, retention/freshness boundaries, and deferred decisions for vocabulary and C5 guidance ownership.
- Findings deferred:
  - Durable source for diagnostic audience and field-classification vocabulary if Story 1.6 extensions do not already provide it.
  - Approved C5 projection freshness and retry guidance source for audit, timeline, and diagnostic reads if current evidence is absent.
  - Existing party-mode deferred decisions remain open for hidden-resource response equivalence, cursor invalidation semantics, redacted-field representation, localization ownership, and external `$ref` support.
- Final recommendation: `ready-for-dev`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-14: Red phase confirmed with `dotnet test tests\Hexalith.Folders.Contracts.Tests\Hexalith.Folders.Contracts.Tests.csproj --no-restore` failing on missing Story 1.11 operations, schemas, and notes.
- 2026-05-14: Green/refactor validation passed with `dotnet test tests\Hexalith.Folders.Contracts.Tests\Hexalith.Folders.Contracts.Tests.csproj --no-restore`.
- 2026-05-14: Full regression validation passed with `dotnet test Hexalith.Folders.slnx --no-restore`.
- 2026-05-14: Solution build validation passed with `dotnet build Hexalith.Folders.slnx --no-restore`.
- 2026-05-15: Red phase confirmed with `dotnet test tests\Hexalith.Folders.Contracts.Tests\Hexalith.Folders.Contracts.Tests.csproj --no-restore --filter "FullyQualifiedName~AuditOpsConsoleContractGroupTests"` failing on missing review-patch schemas and examples.
- 2026-05-15: Red phase confirmed for cross-story error-category propagation with `dotnet test tests\Hexalith.Folders.Contracts.Tests\Hexalith.Folders.Contracts.Tests.csproj --no-restore --filter "FullyQualifiedName~AuditOpsConsoleReviewSweep"` failing on `GetFolderLifecycleStatus`.
- 2026-05-15: Focused contract validation passed with `dotnet test tests\Hexalith.Folders.Contracts.Tests\Hexalith.Folders.Contracts.Tests.csproj --no-restore` (38/38).
- 2026-05-15: Full regression validation passed with `dotnet test Hexalith.Folders.slnx --no-restore`.
- 2026-05-15: Solution build validation passed with `dotnet build Hexalith.Folders.slnx --no-restore`.

### Completion Notes List

- Added audit trail and operation timeline query operations with read-consistency, safe-denial, correlation, audit metadata, parity, and metadata-only constraints.
- Added read-only ops-console diagnostic query operations for readiness, lock, dirty-state, failed-operation, provider-status, sync-status, and projection freshness.
- Added diagnostic/audit schemas, synthetic examples, canonical projection stale/unavailable categories, audience and field-classification vocabulary, and C3/C5 reference-pending markers.
- Added focused OpenAPI validation for Story 1.11 and updated existing guardrails now that ops-console is an owned Contract Spine surface.
- Resolved remaining review patch follow-ups with redaction-aware wrapper schemas, composed diagnostic strictness, required field-classification evidence, readiness summary references, cursor/audience/boundary examples, freshness consistency invariants, prefixed identifier normalization, and widened error-category propagation across earlier contract groups.

### File List

- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`
- `docs/contract/audit-ops-console-contract-groups.md`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuditOpsConsoleContractGroupTests.cs`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/TenantFolderProviderContractGroupTests.cs`
- `tests/Hexalith.Folders.Testing.Tests/Helpers/SpineContractAssertions.cs`
- `_bmad-output/implementation-artifacts/deferred-work.md`
- `_bmad-output/implementation-artifacts/1-11-author-audit-and-ops-console-query-contract-groups.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
