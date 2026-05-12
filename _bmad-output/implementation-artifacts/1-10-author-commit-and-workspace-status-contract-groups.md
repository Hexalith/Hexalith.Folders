# Story 1.10: Author commit and workspace-status contract groups

Status: ready-for-dev

Created: 2026-05-11

## Story

As an API consumer and adapter implementer,
I want commit and status operations represented in the Contract Spine,
so that clean committed states, failed states, and unknown provider outcomes are reported consistently.

## Acceptance Criteria

1. Given lifecycle command contract groups from Stories 1.7 through 1.9 exist or are explicitly reference-pending, when this story is complete, then `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` contains commit and status operation groups for committing workspace changes, commit evidence, workspace status, task status, provider outcome, retry eligibility, retry-after, and reconciliation status.
2. Given commit is a replay-sensitive mutating command, when `CommitWorkspace` is authored, then it declares `Idempotency-Key`, `x-hexalith-idempotency-key`, lexicographically ordered `x-hexalith-idempotency-equivalence`, `x-hexalith-idempotency-ttl-tier: commit`, required task/correlation/operation identity, branch/ref target metadata, changed-path metadata digest, author metadata reference, commit-message classification metadata, audit metadata keys, canonical error categories, retry eligibility, and adapter parity dimensions.
3. Given commit idempotency TTL inherits C3, when commit metadata is authored, then the contract uses the C3 commit idempotency retention class only when approved and otherwise records `TODO(reference-pending): C3 commit idempotency retention approval` without inventing a binding duration.
4. Given workspace status and task status are non-mutating reads, when `GetWorkspaceStatus` and related status operations are authored, then they do not accept `Idempotency-Key`, declare `read-your-writes` or another approved read-consistency class, expose accepted-command state separately from projected state, include freshness/projection-lag metadata, and preserve safe authorization-denial shapes.
5. Given provider commit results may be known, failed, ambiguous, or pending reconciliation, when schemas and Problem Details are authored, then successful commit, commit failed, known provider failure, provider outcome unknown, reconciliation required, reconciliation completed clean, reconciliation completed dirty, read-model unavailable, retryable transient failure, not retryable terminal failure, and state-transition invalid outcomes are represented without silent retry, discard, duplicate commit, or unauthorized resource-existence leakage.
6. Given the C6 state vocabulary is approved, when commit/status schemas declare lifecycle states, then they use the C6 state catalog and operator-disposition labels from `docs/exit-criteria/c6-transition-matrix-mapping.md` or the OpenAPI foundation if it already consumed C6: `requested`, `preparing`, `ready`, `locked`, `changes_staged`, `dirty`, `committed`, `failed`, `inaccessible`, `unknown_provider_outcome`, and `reconciliation_required`.
7. Given commit and status responses are metadata-only, when examples, schemas, audit metadata, logs, diagnostics, and Problem Details are scanned, then they contain no file contents, diffs, generated context payloads, provider tokens, credential material, raw provider payload bodies, production URLs, local machine paths, email addresses, raw repository URLs, raw branch names with sensitive values, raw commit messages, or unauthorized resource-existence hints.
8. Given commit messages, branch names, repository names, changed paths, and provider correlation IDs are tenant-sensitive metadata by default, when contract schemas include them, then they are represented as classified metadata, digest/reference fields, bounded strings, or redacted shapes as appropriate; raw values are not used in audit, denial, diagnostic, parity, or cross-tenant examples.
9. Given commit requires a prepared workspace, staged changes, and valid task-scoped lock semantics, when the commit group is authored, then it references workspace/lock/file components from Stories 1.8 and 1.9 instead of duplicating them, and it rejects or records reference-pending prerequisite drift if those components are unavailable.
10. Given this is a contract-group authoring story, when implementation is complete, then no runtime REST handlers, EventStore commands, domain aggregate behavior, provider adapters, Git/file-system side effects, workers, generated SDK output, NSwag generation wiring, CLI commands, MCP tools, UI pages, final parity-oracle rows, CI workflow gates, or nested-submodule initialization are added by this story.
11. Given adjacent stories own other capability groups, when this story is complete, then operation paths and schemas remain limited to commit, commit evidence, workspace status, task status, provider outcome, retry eligibility, and reconciliation status concerns and do not implement tenant/folder/provider/repository binding, workspace preparation, lock management, file mutation, context query, audit timeline, operations-console projection queries, cleanup behavior, or runtime reconciliation.
12. Given the Contract Spine is a mechanical source for SDK and parity work, when validation runs, then OpenAPI 3.1 parsing succeeds offline, all `$ref` targets resolve, operation IDs are unique and limited to this story's explicit allow-list, every operation has required `x-hexalith-*` metadata, mutating/read operations satisfy idempotency or read-consistency requirements, no client-controlled tenant authority exists, examples are synthetic and metadata-only, and negative-scope checks prove downstream runtime/adapter/generated artifacts were not added.
13. Given party-mode review hardening, when implementation starts, then the development agent treats the operation inventory, editable file set, reference-pending fallback rules, and validation fixture matrix in Dev Notes as binding story constraints unless a previously approved Contract Spine artifact has frozen a different shape and the story records that mapping explicitly.

## Tasks / Subtasks

- [ ] Confirm prerequisites and preserve scope boundaries. (AC: 1, 3, 9, 10, 11)
  - [ ] Inspect Story 1.6 deliverables: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` and `src/Hexalith.Folders.Contracts/openapi/extensions/`. If absent, record prerequisite drift and create only the narrow commit/status additions this story owns with `TODO(reference-pending)` markers where shared foundation values are missing.
  - [ ] Inspect Stories 1.7, 1.8, and 1.9 deliverables before referencing tenant/folder/provider/repository, workspace/lock, and file/context components. If absent, reference stable planned component names and record the dependency as reference-pending instead of duplicating their scope.
  - [ ] Inspect `docs/contract/idempotency-and-parity-rules.md`, `tests/fixtures/parity-contract.schema.json`, `tests/fixtures/idempotency-encoding-corpus.json`, and `tests/fixtures/previous-spine.yaml`; treat missing files as prerequisite drift, not permission to invent policy.
  - [ ] Inspect `docs/exit-criteria/c3-retention.md`, `docs/exit-criteria/c4-input-limits.md`, `docs/exit-criteria/c6-transition-matrix-mapping.md`, and related S-2 evidence if present; preserve approval state and use reference-pending markers for unapproved values.
  - [ ] Apply reference-pending markers only to named fields or decisions with a cited source path or owner; do not use `TODO(reference-pending)` as a general substitute for required commit/status contract design.
  - [ ] Do not initialize or update nested submodules. Use sibling modules only as read-only references unless explicitly directed otherwise.
  - [ ] Limit allowed story outputs to OpenAPI contract changes, existing Contract project static artifact inclusion if needed, synthetic examples, optional contract notes, and focused offline validation assets.
- [ ] Pin the concrete operation inventory and editable file set before schema authoring. (AC: 1, 10, 11, 12, 13)
  - [ ] Start from the Dev Notes operation inventory seed and confirm each path, method, operation ID, and owning schema section against existing Story 1.6 through 1.9 artifacts.
  - [ ] If an existing Contract Spine artifact already froze different paths or names, preserve the frozen `operationId` where possible and record the mapping in `docs/contract/commit-status-contract-groups.md` or the OpenAPI description.
  - [ ] Keep edits limited to `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`, `src/Hexalith.Folders.Contracts/openapi/extensions/` if extension metadata is missing, `docs/contract/commit-status-contract-groups.md` if notes are needed, and focused `tests/Hexalith.Folders.Contracts.Tests/` or `tests/tools/` validation assets.
  - [ ] Do not edit server, domain, worker, provider, generated client, NSwag, CLI, MCP, UI, or CI workflow files for this story.
- [ ] Author the commit command contract. (AC: 1, 2, 3, 5, 7, 8, 9, 13)
  - [ ] Add or update the commit tag/path under `/api/v1/...` using lowercase hyphen-delimited path segments, plural collection nouns where appropriate, and camelCase path parameters such as `{folderId}` and `{workspaceId}`.
  - [ ] Define stable operation ID `CommitWorkspace` unless Story 1.5, Story 1.6, or an already-authored Contract Spine has frozen a different name; record any mapping in contract notes.
  - [ ] Require task ID, operation ID, workspace ID, folder ID, branch/ref target reference, changed-path metadata digest, author metadata reference, and commit-message classification metadata; tenant authority remains authentication/EventStore-envelope-derived only.
  - [ ] Declare commit idempotency equivalence from Story 1.5 using lexicographic field order: `author_metadata_reference, branch_ref_target, changed_path_metadata_digest, commit_message_classification, operation_id, task_id, tenant_id, workspace_id`.
  - [ ] Treat that equivalence order as normative ordinal/ASCII lexical ordering for extension values unless Story 1.5 or the extension vocabulary has already frozen a different comparator; record any comparator drift explicitly.
  - [ ] Use `x-hexalith-idempotency-ttl-tier: commit`; preserve C3 approval state and do not substitute the 24-hour mutation tier for commit.
  - [ ] Represent accepted asynchronous commit work as an accepted-command/status shape when provider latency exceeds the command-ack budget; do not imply synchronous Git/provider completion.
  - [ ] Model `retryAfter` and retry eligibility as advisory contract metadata only; provider outcome `unknown` or otherwise indeterminate outcomes must never advertise blind commit retry eligibility.
  - [ ] Require prepared workspace, staged changes, held or releasable task-scoped lock semantics per C6, and metadata-only changed-path evidence. Do not encode file contents, diffs, provider payloads, or raw working-copy paths.
  - [ ] Keep actual provider calls, Git commit behavior, EventStore commands, domain state transitions, lock release effects, process managers, and reconciliation workers deferred to Epic 4 implementation stories.
- [ ] Author workspace, task, provider-outcome, and reconciliation status queries. (AC: 1, 4, 5, 6, 7, 8, 13)
  - [ ] Add non-mutating status operations for workspace status, task status, commit evidence, provider outcome, retry eligibility, retry-after, and reconciliation status when not already covered by shared status components.
  - [ ] Define stable operation IDs such as `GetWorkspaceStatus`, `GetTaskStatus`, `GetCommitEvidence`, `GetProviderOutcome`, and `GetReconciliationStatus` unless prior stories have frozen different names.
  - [ ] Declare approved read-consistency classes. Use `read-your-writes` for `GetWorkspaceStatus` per Story 1.5 unless a newer approved source changes it; use `eventually-consistent` only where status is explicitly projection-backed.
  - [ ] Separate accepted command state from projected/read-model state by schema field name, description, and example so consumers cannot treat command acknowledgment as durable provider completion.
  - [ ] Include projection watermark, freshness age, last successful operation, last failure category, retry eligibility, retry-after, current state, terminal state, correlation ID, and task/operation ID metadata where authorized.
  - [ ] Represent `known_failure`, `unknown_provider_outcome`, `reconciliation_required`, `reconciliation_completed_clean`, `reconciliation_completed_dirty`, `failed`, and `committed` as closed contract states or explicit reference-pending values; do not invent runtime transition policy beyond approved C6/source artifacts.
  - [ ] Retrying commit must not be recommended while outcome ambiguity can duplicate upstream commits.
  - [ ] Ensure unauthorized, missing, wrong-tenant, stale-read, unknown-task, unknown-workspace, and hidden-commit cases use safe Problem Details or safe metadata-only response shapes without resource-existence hints.
- [ ] Apply shared OpenAPI conventions consistently. (AC: 2, 4, 6, 7, 8, 12)
  - [ ] Reuse shared headers, parameters, Problem Details, freshness metadata, pagination/filtering conventions, lifecycle/state schemas, and extension vocabulary from Story 1.6 instead of duplicating incompatible shapes.
  - [ ] Use camelCase JSON properties, ISO-8601 UTC timestamps, string enums, opaque ULID identifiers, and tenant-sensitive metadata classification for paths, branch names, repository names, commit messages, and provider correlation IDs.
  - [ ] Ensure the mutating commit operation has `Idempotency-Key`, correlation metadata, task identity, audit metadata, and parity dimensions; ensure all non-mutating status queries omit `Idempotency-Key`.
  - [ ] Ensure no request body field, query parameter, route segment, or client-controlled header is described as authoritative tenant identity; tenant authority remains auth/EventStore-envelope-derived only.
  - [ ] Use RFC 9457 Problem Details plus Hexalith fields: `category`, `code`, `message`, `correlationId`, `retryable`, `clientAction`, and `details`.
  - [ ] Prefer machine-readable status and error codes over adopter-facing English prose fields; descriptions may explain behavior, but client semantics must bind to codes/enums.
  - [ ] Ensure examples are synthetic opaque placeholders only and do not include real tenant IDs, repository URLs, branch names with sensitive values, local paths, provider identifiers, organization names, email addresses, file contents, diffs, raw commit messages, generated context payloads, secrets, or unauthorized resource hints.
  - [ ] Use `TODO(reference-pending): <field-or-decision>` only for unresolved approved-source values, with exact source paths or decision owners when known.
- [ ] Add focused offline validation. (AC: 7, 10, 12, 13)
  - [ ] Add or update the smallest local validator or test that parses `hexalith.folders.v1.yaml` as OpenAPI 3.1 and resolves all local `$ref` targets without network access.
  - [ ] Verify new operation IDs are unique, stable, and limited to this story's operation allow-list: `CommitWorkspace`, `GetWorkspaceStatus`, and any explicitly named task-status, commit-evidence, provider-outcome, retry-eligibility, or reconciliation-status operation added by this story.
  - [ ] Verify all new operations include required `x-hexalith-*` metadata and satisfy idempotency/read-consistency requirements by operation type.
  - [ ] Verify no request payload, query parameter, route segment, or client-controlled header defines authoritative tenant context.
  - [ ] Verify commit TTL tier is `commit` and that C3 approval state is preserved when C3 values are not final.
  - [ ] Verify examples and audit metadata exclude file contents, diffs, raw commit messages, raw changed-path lists where not authorized, raw provider payloads, generated context payloads, provider tokens, credential material, local paths, production URLs, and unauthorized resource-existence hints.
  - [ ] Verify provider-outcome and reconciliation schemas distinguish known failure, unknown provider outcome, reconciliation required, reconciliation completed clean, reconciliation completed dirty, terminal failed, and terminal committed states.
  - [ ] Verify negative scope: no generated SDK files, NSwag generation wiring, REST handlers/controllers, CLI commands, MCP tools, domain aggregate behavior, provider adapters, workers, UI pages, final parity oracle rows, CI workflow gates, or nested-submodule initialization.
  - [ ] Cover the minimum validation matrix: valid OpenAPI 3.1 document, local `$ref` chain, duplicate `operationId`, missing required `x-hexalith-*` metadata, mutating operation without idempotency metadata, read operation without read-consistency metadata, provider outcome unknown, reconciliation required, client-controlled tenant authority, forbidden raw sensitive metadata, and negative-scope file/category additions.
  - [ ] Run `dotnet build Hexalith.Folders.slnx` if the scaffold supports it after focused validation. If blocked by earlier scaffold state, record the exact prerequisite instead of expanding this story.
- [ ] Record downstream authoring notes. (AC: 5, 8, 10, 11)
  - [ ] Add a short note near the OpenAPI file or in `docs/contract/` explaining which commit/status components Stories 1.11, 1.12, 1.13, Epic 4, Epic 5, and Story 6.6 must reuse.
  - [ ] Record deferred owners for runtime commit behavior, C6 state transitions, lock release side effects, idempotency persistence, unknown-outcome reconciliation, provider workers, workspace cleanup, audit timeline, operations-console projections, generated SDK helpers, parity oracle rows, and CI gates.
  - [ ] Record any unresolved C3 approval, Story 1.6 foundation, Story 1.8 workspace/lock, Story 1.9 file/context, Story 1.5 operation naming, or C6 state metadata dependencies as explicit deferred decisions.

## Dev Notes

### Scope Boundaries

- This story authors Contract Spine operation groups for commit and workspace/task status only.
- Contract-only output means OpenAPI contract declarations, synthetic examples, optional contract notes, and focused offline validation. Do not broaden this story into runtime behavior, generated consumers, provider integration, Git side effects, process managers, reconciliation workers, cleanup implementation, or CI workflow wiring.
- Primary files expected:

```text
src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml
src/Hexalith.Folders.Contracts/openapi/extensions/
```

- Optional supporting docs, only if needed:

```text
docs/contract/commit-status-contract-groups.md
```

- Do not add `src/Hexalith.Folders.Client/Generated/`, NSwag configuration, server endpoints, EventStore commands, aggregate logic, provider adapters, Git or filesystem operations, CLI/MCP commands, workers, UI, parity rows, or CI gates.
- Do not add operation groups owned by adjacent stories:
  - Story 1.7: tenant, folder, provider, repository binding, repository creation, and branch/ref policy.
  - Story 1.8: workspace preparation, lock acquisition/release, lock inspection, retry eligibility, and state-transition evidence.
  - Story 1.9: file mutation and context query.
  - Story 1.11: audit trail, operation timeline, readiness/status diagnostics, and operations-console projection queries.
- Runtime commit behavior, lock-release effects, provider calls, reconciliation, cleanup, status projections, and read-model determinism belong to Epic 4 implementation stories.

### Current Repository State

- At story creation time, `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` was not present in the working tree.
- At story creation time, `docs/contract/idempotency-and-parity-rules.md`, `tests/fixtures/parity-contract.schema.json`, `tests/fixtures/idempotency-encoding-corpus.json`, `tests/fixtures/previous-spine.yaml`, `docs/exit-criteria/c3-retention.md`, `docs/exit-criteria/c4-input-limits.md`, `docs/exit-criteria/c6-transition-matrix-mapping.md`, and `docs/exit-criteria/s2-oidc-validation.md` were present.
- At story creation time, C3 values were marked "proposed workshop values - needs human decision before Phase 1 Contract Spine authoring"; implementation must preserve that approval state instead of silently treating proposed values as final if no newer approval exists.
- At story creation time, Story 1.5 was in `review` with implementation notes showing rules and fixture verification completed. The implementation agent must inspect current sprint status and artifacts before editing because earlier ready stories may have completed after this story was created.

### Contract Group Requirements

- Use OpenAPI 3.1 at `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` as the single source of truth when the foundation exists.
- REST paths are URL-versioned under `/api/v1`, use lowercase hyphen-delimited path segments, plural collection nouns where appropriate, and camelCase path parameters.
- Stable operation IDs matter because NSwag generation and the C13 parity oracle will consume them later.
- Tenant authority comes from auth context and EventStore envelopes. Payloads may include resource IDs to validate, but never authoritative tenant context.
- Commit operates on tenant/folder/task/workspace scope after file mutations. It requires operation identity and task identity; it must not infer durable truth from local working-copy paths.
- Commit message, branch/ref target, repository name, changed paths, and provider correlation IDs are tenant-sensitive metadata. Prefer classification, digest, reference, truncation, or redaction fields over raw values outside authorized response bodies.
- `CommitWorkspace` uses the `commit` idempotency TTL tier. Commit idempotency records persist for C3 retention because unknown provider outcomes and audit reconstruction can outlive the 24-hour mutation tier.
- Unknown provider outcome is a first-class state. A contract that recommends blind retry after ambiguous commit violates the duplicate-commit safety invariant.
- Status queries must label accepted command state and projected/read-model state separately so callers do not confuse command acknowledgment with durable provider or projection completion.

### Operation Inventory Seed

Use the operation names below as a starting point unless Story 1.5, Story 1.6, or an already-authored Contract Spine has frozen different names. If names differ, keep the OpenAPI `operationId` stable and record the mapping.

| Operation | Type | Required metadata |
| --- | --- | --- |
| `CommitWorkspace` | Mutating command | idempotency, commit TTL tier, operation ID, task ID, workspace/lock scope, branch/ref target reference, changed-path metadata digest, author metadata reference, commit-message classification, audit keys, correlation, parity dimensions |
| `GetWorkspaceStatus` | Query/status | read-your-writes consistency, accepted command state, projected state, C6 state, freshness/projection lag, lock/dirty/commit/failure summaries where authorized |
| `GetTaskStatus` | Query/status | task identity, current lifecycle state, terminal state, last operation ID, retry eligibility, freshness, safe denial |
| `GetCommitEvidence` | Query/status | commit reference metadata where authorized, commit result status, changed-path digest, provider correlation reference, audit keys, redaction metadata |
| `GetProviderOutcome` | Query/status | known failure, unknown outcome, retry-after, provider correlation reference, sanitized provider status class |
| `GetReconciliationStatus` | Query/status | reconciliation state, last reconciler observation, retry eligibility, final-state evidence, escalation flag |

Do not add provider readiness, workspace preparation, lock, file mutation, context query, audit timeline, operations-console, worker, generated SDK, CLI, or MCP operations in this story.

### Party-Mode Hardening Notes

- Operation allow-list seed, unless an already-authored Contract Spine artifact has frozen a different path or operation name:
  - `POST /api/v1/folders/{folderId}/workspaces/{workspaceId}/commits` -> `CommitWorkspace`.
  - `GET /api/v1/folders/{folderId}/workspaces/{workspaceId}/status` -> `GetWorkspaceStatus`.
  - `GET /api/v1/tasks/{taskId}/status` -> `GetTaskStatus`.
  - `GET /api/v1/folders/{folderId}/workspaces/{workspaceId}/commits/{operationId}/evidence` -> `GetCommitEvidence`.
  - `GET /api/v1/folders/{folderId}/workspaces/{workspaceId}/commits/{operationId}/provider-outcome` -> `GetProviderOutcome`.
  - `GET /api/v1/folders/{folderId}/workspaces/{workspaceId}/reconciliation/{reconciliationId}/status` -> `GetReconciliationStatus`.
- Editable file set for implementation is limited to the OpenAPI spine, its extension metadata folder when needed, optional `docs/contract/commit-status-contract-groups.md` notes, and focused contract validation assets under `tests/Hexalith.Folders.Contracts.Tests/` or `tests/tools/`.
- Cross-story identities and schemas must reuse approved Story 1.5 through 1.9 shapes for tenant authority, folder ID, workspace ID, task ID, provider/binding references, branch/ref references, operation ID, correlation ID, changed-path digest, and reconciliation ID.
- Accepted-command state, projected/read-model state, provider outcome, commit evidence, retry eligibility, and reconciliation status must be distinct schema concepts. Do not collapse them into one lifecycle field.
- `unknown_provider_outcome` and other indeterminate provider outcomes must not expose blind commit retry guidance. `retryAfter` is advisory metadata for safe retryable states only, not scheduler behavior.
- Machine-readable enums and codes are the compatibility contract. Human-readable descriptions must not become the only source of client behavior.
- Negative-scope validation should assert forbidden artifact categories and paths, not incidental current filenames, so the check remains stable as the scaffold grows.

### Error and Audit Requirements

- Required canonical categories for this story include authentication failure, tenant authorization denied, folder ACL denied, cross-tenant access denied, workspace not ready, workspace locked, lock expired, lock not owned, authorization revocation detected, stale workspace, dirty workspace, commit failed, provider failure known, provider outcome unknown, reconciliation required, read-model unavailable, duplicate operation, idempotency conflict, state transition invalid, validation error, not found, redacted, and internal error.
- Use RFC 9457 Problem Details plus Hexalith fields: `category`, `code`, `message`, `correlationId`, `retryable`, `clientAction`, and `details`.
- Safe-denial Problem Details must expose stable safe codes only; they must not reveal folder existence, workspace existence, lock existence, task existence, commit existence, provider state, branch existence, changed-path presence, local paths, or unauthorized resource existence.
- Audit metadata is metadata-only. It may include tenant-scoped actor evidence, folder ID, workspace ID, task ID, operation ID, commit status, commit reference classification, changed-path metadata digest, provider correlation reference, branch/ref policy reference, result, timestamp, duration, retryability, and sanitized error category.
- Do not include raw file contents, diffs, raw commit messages, raw changed-path lists outside authorized metadata, generated context payloads, provider tokens, credential material, raw provider payloads, production URLs, local filesystem paths, or unauthorized resource existence.

### Idempotency, Correlation, And Read Consistency

- `CommitWorkspace` requires caller-supplied `Idempotency-Key` plus a commit operation identity.
- Commit equivalence is tenant-scoped and operation-scoped. Same key across tenants, changed branch/ref target, changed changed-path metadata digest, changed author metadata reference, changed commit-message classification, changed task ID, changed workspace ID, changed operation ID, changed semantic payload, or changed behavior-affecting credential scope is non-equivalent.
- Field lists in `x-hexalith-idempotency-equivalence` must be lexicographically ordered.
- Commit-tier TTL inherits C3 retention. If C3 is still proposed, the OpenAPI must preserve reference-pending approval state.
- Non-mutating status queries do not accept `Idempotency-Key`.
- `GetWorkspaceStatus` uses `read-your-writes` per Story 1.5 unless a newer approved source changes it. Other projection-heavy status queries must declare their exact consistency class and freshness behavior.
- Correlation IDs and task IDs must propagate through REST, SDK, CLI, MCP, EventStore envelopes, projections, audit, logs, and traces.

### Previous Story Intelligence

- Story 1.1 establishes the solution scaffold and dependency direction.
- Story 1.2 establishes root configuration and forbids recursive nested-submodule initialization.
- Story 1.3 seeds shared fixtures: audit leakage corpus, parity schema, previous spine, idempotency encoding corpus, `tests/load`, and artifact templates.
- Story 1.4 owns C3 retention, C4 input limits, S-2 OIDC parameters, and C6 transition mapping. This story consumes C3/C6 values but must preserve approval state and must not invent missing limits, retention decisions, or transitions.
- Story 1.5 defines operation inventory, idempotency equivalence, adapter parity dimensions, parser-policy classifications, and read-consistency expectations. This story must consume its final artifact when available.
- Story 1.6 owns the OpenAPI foundation and shared `x-hexalith-*` vocabulary. This story must reuse it rather than redefine extension shapes.
- Story 1.7 owns tenant, folder, provider, repository binding, and branch/ref operation groups. This story references those components and must not duplicate them.
- Story 1.8 owns workspace and lock operations, lock semantics, retry eligibility, state-transition evidence, and authorization-revocation outcomes. This story references those components and must not duplicate them.
- Story 1.9 owns file mutation and context-query operation groups. This story consumes changed-path metadata and file-operation evidence without adding or changing file mutation operations.
- Recent commits show Story 1.5 implementation and Story 1.8/1.9 story creation have occurred. The implementation agent must inspect current artifacts because contract foundation and operation groups may have changed after this story was created.

### Testing Guidance

- Prefer a focused OpenAPI validation test or script in existing test/tool locations over broad runtime integration work.
- Validation must run offline without Aspire, Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, provider credentials, tenant data, production secrets, network calls, or initialized nested submodules.
- Validate OpenAPI parse, `$ref` resolution, unique operation IDs, exact `x-hexalith-*` metadata presence, commit idempotency TTL tier, C3 reference-pending handling, read-consistency completeness, safe tenant-authority boundaries, C6 state metadata, synthetic examples, safe-denial parity, redaction, and negative scope.
- Include contract-level assertions for commit failed, provider outcome unknown, reconciliation required, retry-after, retry eligibility, read-model unavailable, state transition invalid, and idempotency conflict outcomes.
- Include allow-list assertions for commit/status operation IDs and `/api/v1` path prefixes owned by this story so validation fails if provider readiness, workspace/lock, file/context, audit timeline, operations-console, runtime, generated-client, CLI, MCP, worker, UI, or CI artifacts appear.
- If the full solution is buildable, run `dotnet build Hexalith.Folders.slnx` from the repository root after focused validation. Record exact blockers if build cannot run due to prior scaffold state.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 1.10: Author commit and workspace-status contract groups`
- `_bmad-output/planning-artifacts/epics.md#Epic 1: Bootstrap Canonical Contract For Consumers And Adapters`
- `_bmad-output/planning-artifacts/epics.md#Epic 4: Repository-Backed Workspace Task Lifecycle`
- `_bmad-output/planning-artifacts/architecture.md#Workspace State Transition Matrix`
- `_bmad-output/planning-artifacts/architecture.md#Cross-Cutting Concerns Identified`
- `_bmad-output/planning-artifacts/architecture.md#Adapter Parity Contract`
- `_bmad-output/planning-artifacts/architecture.md#REST endpoint naming`
- `_bmad-output/planning-artifacts/architecture.md#HTTP headers`
- `_bmad-output/planning-artifacts/architecture.md#Requirements to Structure Mapping`
- `_bmad-output/planning-artifacts/prd.md#Command and Query Contract`
- `_bmad-output/planning-artifacts/prd.md#Error Codes`
- `_bmad-output/planning-artifacts/prd.md#Contract and Quality Gates`
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
- `_bmad-output/project-context.md`
- `AGENTS.md#Git Submodules`

## Project Structure Notes

- The Contract Spine operation groups live under `src/Hexalith.Folders.Contracts/openapi/`.
- Human-readable contract notes may live under `docs/contract/`, but the OpenAPI document remains the source of truth.
- Runtime commit behavior belongs later under `src/Hexalith.Folders/Aggregates/Folder/`, `src/Hexalith.Folders.Workers/CommitWorkflows/`, and `src/Hexalith.Folders/Idempotency/` and must not be implemented here.
- Server endpoints belong later under `src/Hexalith.Folders.Server/Endpoints/CommitEndpoints.cs` and must not be implemented here.
- CLI and MCP wrappers belong later under `src/Hexalith.Folders.Cli/` and `src/Hexalith.Folders.Mcp/` and must not be implemented here.
- Operations-console pages that consume workspace status belong later under `src/Hexalith.Folders.UI/` and must not be implemented here.

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-12 | Applied party-mode review hardening for operation inventory, editable file ownership, retry semantics, tenant authority, metadata-only boundaries, status taxonomy, and validation matrix. | Codex |
| 2026-05-11 | Created ready-for-dev story through `bmad-create-story` workflow. | Codex |

## Party-Mode Review

- Date/time: 2026-05-12T00:06:43Z
- Selected story key: `1-10-author-commit-and-workspace-status-contract-groups`
- Command/skill invocation used: `/bmad-party-mode 1-10-author-commit-and-workspace-status-contract-groups; review;`
- Participating BMAD agents: Winston (System Architect), John (Product Manager), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor)
- Findings summary:
  - The story was directionally correct and contract-only, but reviewers found it too ambiguous for immediate development until operation inventory, editable file boundaries, status taxonomy, and validation expectations were pinned.
  - Accepted-command versus projected/read-model status needed sharper schema separation so implementation does not imply runtime EventStore/provider behavior.
  - Cross-story identity reuse needed explicit binding to Story 1.5 through 1.9 shapes for tenant authority, workspace/task/provider/correlation/operation/reconciliation identities, and changed-path metadata.
  - Provider outcome and retry semantics needed a closed model where unknown or indeterminate provider outcomes never recommend blind commit retry and `retryAfter` remains advisory metadata.
  - Tenant authority and metadata-only boundaries needed operation-level checks to prevent caller-controlled tenant identity, raw commit messages, diffs, file contents, provider payloads, secrets, local paths, and unauthorized resource-existence hints.
  - Test strategy needed an explicit fixture matrix for OpenAPI parse/ref resolution, operation ID uniqueness, required `x-hexalith-*` metadata, idempotency/read-consistency separation, tenant-authority rejection, sensitive metadata rejection, provider outcome states, reconciliation states, and negative-scope artifacts.
- Changes applied:
  - Added AC 13 to bind implementation to the party-mode operation inventory, editable file set, reference-pending fallback rules, and validation fixture matrix.
  - Added prerequisite and operation-inventory subtasks that limit editable files and require explicit mapping if prior Contract Spine artifacts froze different paths or names.
  - Added commit/status subtasks for normative idempotency equivalence ordering, advisory retry metadata, no blind retry on unknown provider outcome, separate accepted/projected states, closed provider/reconciliation states, and no client-controlled tenant authority.
  - Added validation subtasks covering the minimum offline fixture matrix and negative-scope artifact categories.
  - Added Dev Notes with operation allow-list seed, editable file set, cross-story reuse rules, status-taxonomy boundaries, and retry semantics.
- Findings deferred:
  - Exact HTTP response status choices, enum terminal/non-terminal semantics, C3 commit retention approval, C6 transition policy, runtime reconciliation behavior, lock-release side effects, idempotency persistence behavior, generated SDK helpers, parity-oracle rows, CI gates, and operations-console projections remain deferred to approved source artifacts or later stories.
  - If existing Story 1.6 through 1.9 Contract Spine artifacts have already frozen paths, operation names, extension fields, or comparator rules, implementation must preserve those approved shapes and record mappings instead of replacing them.
- Final recommendation: ready-for-dev

## Dev Agent Record

### Agent Model Used

TBD by dev-story agent

### Debug Log References

### Completion Notes List

### File List
