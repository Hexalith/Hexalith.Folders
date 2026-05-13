# Story 1.8: Author workspace and lock contract groups

Status: done

Created: 2026-05-11

## Story

As an API consumer and adapter implementer,
I want workspace preparation and lock operations represented in the Contract Spine,
so that task lifecycle entry and concurrency behavior are canonical before implementation begins.

## Acceptance Criteria

1. Given the shared Contract Spine foundation and extension vocabulary from Story 1.6 exist or are explicitly reference-pending, when this story is complete, then `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` contains workspace and lock operation groups for workspace preparation, workspace lock acquisition, workspace lock release, lock inspection, state-transition evidence, retry eligibility, lease/expiry metadata, and authorization-revocation outcomes.
2. Given repository-backed workspace tasks are tenant-scoped, when request schemas, query parameters, headers, and path parameters are authored, then no payload, query parameter, client-controlled header, workspace identifier, lock token, task identifier, branch/ref value, provider identifier, repository identifier, or cross-tenant lookup field defines authoritative tenant context; tenant authority is documented as coming from authentication context and EventStore envelopes only.
3. Given workspace preparation is a replay-sensitive mutating command, when `PrepareWorkspace` is authored, then it declares `Idempotency-Key`, `x-hexalith-idempotency-key`, lexicographically ordered `x-hexalith-idempotency-equivalence`, `x-hexalith-idempotency-ttl-tier: mutation`, required correlation and task identity metadata, safe-denial Problem Details, audit metadata keys, canonical error categories, retry eligibility, accepted-command state, and adapter parity dimensions.
4. Given lock acquisition and release are replay-sensitive mutating commands, when `LockWorkspace` and `ReleaseWorkspaceLock` are authored, then both declare idempotency metadata, tenant/folder/workspace/task scope, single-active-writer lock semantics, lease/expiry/renewal fields, lock-token or owner validation rules, deterministic lock conflict outcomes, audit metadata keys, and adapter parity dimensions.
5. Given lock and workspace inspection are non-mutating operations, when query/status operations are authored, then they declare read consistency, freshness behavior, safe authorization-denial shape, pagination or filtering only where useful, metadata-only responses, retry eligibility, correlation behavior, canonical error categories, audit classification, and parity dimensions; non-mutating reads do not accept `Idempotency-Key`.
6. Given C6 is the approved state-transition vocabulary, when workspace and lock schemas declare lifecycle states, then they use the C6 state catalog and operator-disposition labels from `docs/exit-criteria/c6-transition-matrix-mapping.md` or the OpenAPI foundation if it already consumed C6; unlisted state/event pairs are represented as `state_transition_invalid` with no state mutation.
7. Given workspace and lock operations can expose sensitive operational metadata, when examples and schemas are scanned, then all examples use synthetic opaque placeholders only and contain no real tenant IDs, repository URLs with credentials, local machine paths, branch names with sensitive values, provider identifiers, credential-shaped values, provider tokens, file contents, diffs, production URLs, email addresses, organization names, or unauthorized resource hints.
8. Given provider, repository, folder, and branch/ref prerequisites are authored by earlier stories, when this story references those resources, then it reuses their component names or records `TODO(reference-pending): <field-or-decision>` comments; it does not re-author tenant/folder/provider/repository/branch-ref operation groups or invent missing Story 1.5, 1.6, or 1.7 policy.
9. Given this is a contract-group authoring story, when implementation is complete, then no runtime REST handlers, EventStore commands, domain aggregates, provider adapters, SDK generated output, NSwag generation wiring, CLI commands, MCP tools, workers, UI pages, parity-oracle rows, CI workflow gates, or nested-submodule initialization are added by this story.
10. Given downstream stories own file mutation, context query, commit, workspace status, audit, and operations-console groups, when this story is complete, then operation paths and schemas remain limited to workspace preparation, lock, release, lock inspection, state-transition evidence, retry eligibility, lease/expiry semantics, and prerequisite references needed by these operations.
11. Given the Contract Spine is a mechanical source for later SDK and parity work, when validation runs, then OpenAPI 3.1 parsing succeeds offline, all `$ref` targets resolve, all new `operationId` values are unique and stable, every operation has required `x-hexalith-*` metadata, no client-controlled tenant authority exists, secret-shaped schema fields are rejected, workspace/lock operation IDs match an explicit allow-list, and negative-scope checks prove generated clients/adapters/runtime behavior were not added.
12. Given workspace interruption and provider uncertainty must be inspectable, when failure and retry shapes are authored, then provider readiness failure, workspace preparation failure, lock conflict, stale lock, expired lock, authorization revocation, provider outcome unknown, reconciliation required, state transition invalid, read-model unavailable, and idempotency conflict map to canonical Problem Details without silent repair, discard, commit, or unauthorized resource enumeration.
13. Given each workspace/lock operation must be reviewable independently, when `PrepareWorkspace`, `LockWorkspace`, `ReleaseWorkspaceLock`, `GetWorkspaceLock`, `GetWorkspaceRetryEligibility`, and `GetWorkspaceTransitionEvidence` are authored, then each operation declares its owned scope, idempotency or read-consistency vocabulary, authorization outcomes, canonical Problem Details, audit metadata, synthetic examples, parity dimensions, and reused Story 1.6/1.7 components or explicit `TODO(reference-pending)` markers.
14. Given this is a contract-only story, when OpenAPI descriptions, schemas, examples, notes, and validation assets are authored, then they describe externally observable contract states, evidence shapes, retry eligibility, freshness metadata, lease metadata, and Problem Details only; they do not prescribe worker or process-manager behavior, aggregate logic, event sequencing, storage mechanics, EventStore implementation details, Tenants implementation details, or runtime lock enforcement.
15. Given adopters need stable diagnostics without policy leakage, when C6 transition evidence, retry eligibility, authorization revocation, and lock lease metadata are exposed, then contract shapes include current state, attempted transition where applicable, result, reason code, evidence timestamp, correlation/audit metadata, read freshness, lock identity, lease status, acquired/effective timestamp, expiry timestamp, and holder/audit reference where appropriate, while deferring exact retry policy, lease duration defaults, renewal policy, clock-skew policy, audit taxonomy expansion, and runtime revocation mechanics to approved source documents.
16. Given lock ownership proof can be confused with authentication secrets, when lock owner, holder, or token-shaped fields are authored, then each field is explicitly documented as a non-secret opaque lock proof within tenant/folder/workspace/task scope, never as a bearer credential, provider credential, session secret, refresh token, or authorization override; examples and diagnostics redact or omit the value unless the caller is already authorized for that lock scope.

## Tasks / Subtasks

- [x] Confirm prerequisites and preserve scope boundaries. (AC: 1, 8, 9, 10)
  - [x] Inspect Story 1.6 deliverables: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` and `src/Hexalith.Folders.Contracts/openapi/extensions/`. If absent, record prerequisite drift and create only the narrow workspace/lock additions this story owns with `TODO(reference-pending)` markers where shared foundation values are missing.
  - [x] Inspect Story 1.7 deliverables for tenant/folder/provider/repository/branch-ref components before referencing them. If absent, reference stable planned component names and record the dependency as reference-pending instead of duplicating Story 1.7 scope.
  - [x] Inspect `docs/contract/idempotency-and-parity-rules.md`, `tests/fixtures/parity-contract.schema.json`, `tests/fixtures/idempotency-encoding-corpus.json`, and `tests/fixtures/previous-spine.yaml` if present; treat missing files as prerequisite drift, not permission to invent policy.
  - [x] Inspect `docs/exit-criteria/c3-retention.md`, `docs/exit-criteria/c4-input-limits.md`, `docs/exit-criteria/c6-transition-matrix-mapping.md`, and `docs/exit-criteria/s2-oidc-validation.md` if present; unresolved values must stay reference-pending.
  - [x] Create an operation-by-operation mapping for `PrepareWorkspace`, `LockWorkspace`, `ReleaseWorkspaceLock`, `GetWorkspaceLock`, `GetWorkspaceRetryEligibility`, and `GetWorkspaceTransitionEvidence` covering scope, idempotency or freshness, authorization outcomes, Problem Details, audit metadata, examples, parity dimensions, and component reuse.
  - [x] Record unresolved values per operation as deferred decisions with source path or decision owner; do not hide them in generic TODO text or resolve them by inventing local policy.
  - [x] Do not initialize or update nested submodules. Use sibling modules only as read-only references unless explicitly directed otherwise.
  - [x] Do not add runtime behavior, generated SDK output, NSwag configuration, CLI/MCP tools, provider adapters, workers, UI, final parity rows, or CI gates.
  - [x] Limit allowed story outputs to OpenAPI contract changes, existing Contract project static artifact inclusion if needed, synthetic examples, optional contract notes, and focused offline validation assets.
- [x] Author workspace preparation contract operations. (AC: 1, 2, 3, 6, 8, 12)
  - [x] Add or update tags and paths for workspace preparation under `/api/v1/...` using lowercase hyphen-delimited path segments and camelCase path parameters.
  - [x] Define a stable `operationId` for `PrepareWorkspace` unless Story 1.5 or Story 1.6 has already frozen a different name; record any mapping in contract notes.
  - [x] Model workspace identity as tenant/folder/repository-binding scoped opaque IDs plus task identity; do not make local filesystem path, branch display name, folder hierarchy, or repository URL an aggregate identity.
  - [x] Require task identity for workspace preparation and declare how `X-Hexalith-Task-Id` or the shared task-id component is used.
  - [x] Declare idempotency equivalence fields for preparation using approved source values where available: branch/ref policy reference, folder ID, provider binding reference, repository binding ID, task ID, workspace policy inputs, and workspace scope. Keep fields lexicographically ordered.
  - [x] Represent accepted asynchronous preparation as an accepted-command/status shape, not as a synchronous provider or filesystem operation.
  - [x] Include provider-readiness failed, repository binding missing or unsafe, branch/ref policy invalid, stale read model, provider unavailable, unknown provider outcome, reconciliation required, validation error, tenant authorization denied, and folder ACL denied outcomes.
  - [x] Keep actual provider calls, Git clone behavior, workspace filesystem operations, process managers, and EventStore commands deferred to Epic 4 implementation stories.
- [x] Author workspace lock acquisition and release operations. (AC: 1, 2, 4, 6, 12)
  - [x] Add contract operations for acquiring a task-scoped workspace lock and releasing it when ownership and policy allow.
  - [x] Define stable `operationId` values for `LockWorkspace` and `ReleaseWorkspaceLock` unless prior stories have frozen different names.
  - [x] Declare single-active-writer semantics per tenant/folder/workspace scope and deterministic conflict outcomes for competing tasks.
  - [x] Include lease duration, lease expiry, stale/expired/abandoned/interrupted lock metadata, retry eligibility, and renewal or revalidation hints only as contract metadata; do not implement a lock manager.
  - [x] Declare idempotency equivalence for lock acquisition and release using tenant/folder/workspace scope, task ID, lock intent, lock token or owner where applicable, and behavior-affecting lease policy fields. Keep fields lexicographically ordered.
  - [x] Mark lock ownership proof fields as non-secret opaque lock references in schema descriptions, examples, and validation allow-lists; do not let `token` wording introduce auth, provider, session, refresh, or credential semantics.
  - [x] Model authorization revocation during a held lock as a visible contract outcome using the C6 vocabulary and safe Problem Details; do not silently preserve or discard a lock without inspectable state.
  - [x] Include `workspace_locked`, `lock_conflict`, `lock_expired`, `lock_not_owned`, `state_transition_invalid`, `tenant_access_denied`, `idempotency_conflict`, `read_model_unavailable`, and `reconciliation_required` categories where the canonical error vocabulary supports them; mark any missing category as reference-pending.
- [x] Author lock inspection, state-transition, and retry-eligibility query operations. (AC: 1, 5, 6, 12)
  - [x] Add non-mutating operations for inspecting lock state, workspace preparation state, retry eligibility, lease/expiry metadata, and state-transition evidence if they are not already covered by shared status components.
  - [x] Ensure query/status operations omit `Idempotency-Key` and declare read consistency using approved classes: `snapshot-per-task`, `read-your-writes`, or `eventually-consistent`.
  - [x] Include freshness metadata and projection-lag behavior where results may lag accepted commands.
  - [x] Represent C6 states and operator-disposition labels in reusable schemas or references, including `requested`, `preparing`, `ready`, `locked`, `changes_staged`, `dirty`, `committed`, `failed`, `inaccessible`, `unknown_provider_outcome`, and `reconciliation_required`.
  - [x] Represent retry eligibility as metadata-only: retryable flag, retry-after hint where applicable, canonical reason code, correlation ID, task ID, and current state. Do not expose provider payloads, local paths, raw branch names, file contents, or unauthorized resource existence.
  - [x] Define `GetWorkspaceTransitionEvidence` as contract evidence only: current state, attempted transition, result, reason code, evidence timestamp, correlation/audit metadata, and freshness indicator. Do not expose internal event schemas, storage keys, worker behavior, or payload-carried tenant authority.
  - [x] Ensure unauthorized, absent, cross-tenant, expired, and stale-lock inspection paths cannot be distinguished unless an approved diagnostic contract already establishes caller authority and audience.
  - [x] Add synthetic examples for valid transition evidence, invalid transition Problem Details, revoked authorization, expired lease, retry eligible, and retry blocked.
  - [x] Keep commit evidence, dirty-file status, file-context status, audit timeline, and operations-console projection query groups deferred to Stories 1.9 through 1.11.
- [x] Apply shared OpenAPI conventions consistently. (AC: 2, 3, 4, 5, 7, 11)
  - [x] Reuse shared headers, parameters, Problem Details, freshness metadata, pagination/filtering conventions, and extension vocabulary from Story 1.6 instead of duplicating incompatible shapes.
  - [x] Use camelCase JSON properties, ISO-8601 UTC timestamps, string enums, opaque ULID identifiers, forward-slash metadata paths only when path metadata is required, and metadata-only examples.
  - [x] Ensure every mutating operation has `Idempotency-Key` and every non-mutating operation omits it.
  - [x] Reuse Story 1.6 idempotency and read-freshness/read-consistency headers, schemas, and extensions where available; if exact names are unresolved, add `TODO(reference-pending)` with the approved source path or decision owner instead of inventing a parallel vocabulary.
  - [x] Ensure every operation declares canonical error categories, authorization requirement, correlation behavior, audit classification, lifecycle/state metadata where applicable, and parity dimensions.
  - [x] Add a canonical Problem Details matrix by operation class for invalid transition, lock conflict, expired lease, retry not eligible, authorization revoked, not found or safe denial, stale read or unavailable freshness, idempotency conflict, and validation failure.
  - [x] Use safe-denial examples that are externally indistinguishable for unauthorized, absent, cross-tenant, missing workspace, missing lock, missing task, missing repository binding, and stale-read cases unless an already-authorized diagnostic endpoint explicitly permits more detail.
  - [x] Do not add tenant ID as an authoritative path, query, body, or client-controlled header value; if prerequisite components require a tenant path segment, record the conflict as reference-pending instead of normalizing around it locally.
  - [x] Use `TODO(reference-pending): <field-or-decision>` only for unresolved approved-source values, with exact source paths or decision owners when known.
- [x] Add focused offline validation. (AC: 7, 9, 11, 12)
  - [x] Add or update the smallest local validator or test that parses `hexalith.folders.v1.yaml` as OpenAPI 3.1 and resolves all local `$ref` targets without network access.
  - [x] Verify new operation IDs are unique, stable, and limited to this story's operation allow-list: `PrepareWorkspace`, `LockWorkspace`, `ReleaseWorkspaceLock`, and any explicitly named workspace/lock inspection or retry/status operation added by this story.
  - [x] Verify all new operations include required `x-hexalith-*` metadata and that mutating/query operations satisfy their idempotency or read-consistency requirements.
  - [x] Verify no request payload, query parameter, or client-controlled header defines tenant authority.
  - [x] Verify path or query identifiers only scope resources and never establish authority; tenant-looking response or audit metadata must be derived/display metadata, not caller-controlled authority.
  - [x] Verify examples are synthetic, metadata-only, and contain no file contents, diffs, provider tokens, credential material, production URLs, real organization identifiers, real tenant IDs, or local machine paths.
  - [x] Verify safe-denial examples for unauthorized, absent, cross-tenant, missing workspace, missing lock, missing task, provider uncertainty, and stale-read cases use externally indistinguishable shapes where existence must not be inferred.
  - [x] Verify schema and example field names reject secret-shaped or credential-shaped terms such as `token`, `secret`, `credential`, `password`, `privateKey`, `accessToken`, and raw provider authorization material unless the field is an explicit non-secret opaque reference.
  - [x] Verify every allowed lock ownership proof exception has a non-secret schema description, synthetic opaque example, no provider/auth/session wording, and no appearance in unauthorized Problem Details or audit diagnostics where existence must remain hidden.
  - [x] Verify no forbidden project/runtime references are introduced in the Contract Spine: Server, Client, CLI, MCP, UI, Workers, process managers, domain aggregate behavior, `Hexalith.EventStore`, or `Hexalith.Tenants`.
  - [x] Verify the six operation groups reuse shared Story 1.6/1.7 components where available and that operation parity covers tenant/folder/workspace/task scope, idempotency or freshness, Problem Details, audit metadata, examples, authorization outcomes, and C6 evidence references.
  - [x] Verify negative scope: no generated SDK files, NSwag generation wiring, REST handlers/controllers, CLI commands, MCP tools, domain aggregate behavior, provider adapters, workers, UI pages, final parity oracle rows, CI workflow gates, or nested-submodule initialization.
  - [x] Run `dotnet build Hexalith.Folders.slnx` if the scaffold supports it after focused validation. If blocked by earlier scaffold state, record the exact prerequisite instead of expanding this story.
- [x] Record downstream authoring notes. (AC: 8, 10, 12)
  - [x] Add a short note near the OpenAPI file or in `docs/contract/` explaining which workspace/lock components Stories 1.9, 1.10, 1.11, Epic 4, and Epic 5 must reuse.
  - [x] Record deferred owners for file mutation, context query, commit/status, audit timeline, operations-console projection queries, runtime lock manager, workspace workers, reconciliation, NSwag generation, parity oracle, and CI gates.
  - [x] Record any unresolved C3/C4/S-2/C6, Story 1.5, Story 1.6, or Story 1.7 metadata dependencies as explicit deferred decisions.

### Review Findings

Generated by `bmad-code-review` (2026-05-13). Reviewers: Blind Hunter, Edge Case Hunter, Acceptance Auditor.

#### Decision-needed (resolved 2026-05-13)

- [x] [Review][Decision] D1 → **Authz-first; keep 410.** Document explicitly in Problem Details preamble and in `docs/contract/workspace-lock-contract-groups.md` that 410 `WorkspaceLockExpired`, lock-conflict details, and lock-existence indicators are only reachable after authorization; unauthorized callers always see the safe-denial 404 envelope. Becomes patch P28.
- [x] [Review][Decision] D2 → **Make `lease` optional** in `WorkspaceLockStatus`. Unlocked variant simply omits `lease`. Becomes patch P29.
- [x] [Review][Decision] D3 → **Make `taskId` optional** in `WorkspaceRetryEligibility` and on the `WorkspaceLockStatus.retryEligibility` embedding. `GetWorkspaceLock` stays workspace-scoped (no `X-Hexalith-Task-Id`). Becomes patch P30.
- [x] [Review][Decision] D4 → **Introduce narrow `LockState` enum** (`unlocked` / `locked` / `expired` / `stale` / `revoked`). Retype `WorkspaceLockStatus.lockState` to `LockState`. Becomes patch P31.
- [x] [Review][Decision] D5 → **Fold into 200 body.** Remove 422 `WorkspaceTransitionInvalid` and 409 `WorkspaceAuthorizationRevoked` from `GetWorkspaceTransitionEvidence`; return 200 with the existing `WorkspaceTransitionEvidence` whose `result` field encodes the outcome. Also drop `state_transition_invalid` from `GetWorkspaceLock` and `GetWorkspaceRetryEligibility` canonical-error-categories (a GET cannot cause a transition); keep `stale_workspace` and existing read-failure categories. Becomes patches P32 + P33 + P34.
- [x] [Review][Decision] D6 → **Out-of-scope for this review.** Three submodule pointer changes (`Hexalith.EventStore`, `Hexalith.FrontComposer`, `Hexalith.Tenants`) are deferred to a separate housekeeping change. Story 1.8 commit must exclude them. No patch in this review.
- [x] [Review][Decision] D7 → **Remove `release_reason_code` from `ReleaseWorkspaceLock` idempotency-equivalence.** Reason code is audit metadata, not behavior-affecting. Becomes patch P35.

#### Patches

- [x] [Review][Patch] P1 Make `X-Hexalith-Task-Id` required on `PrepareWorkspace`, `LockWorkspace`, `ReleaseWorkspaceLock` (AC3) [src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: TaskId parameter, three operations]
- [x] [Review][Patch] P2 Extend `WorkspaceTransitionEvidence` with AC15 lock fields (`lockId`, `lease`, `holderRef`) for lock-affecting transitions (AC15) [hexalith.folders.v1.yaml: WorkspaceTransitionEvidence schema]
- [x] [Review][Patch] P3 Add dedicated Problem Detail response components and examples for `ProviderReadinessFailed`, `WorkspacePreparationFailed`, `UnknownProviderOutcome` and wire them to `PrepareWorkspace`/`GetWorkspaceRetryEligibility` (AC12) [hexalith.folders.v1.yaml: components/responses, examples, PrepareWorkspace responses]
- [x] [Review][Patch] P4 Add `lock_conflict` canonical category and 409 response to `PrepareWorkspace` for (locked, Prepare) / (preparing, Prepare) state pairs [hexalith.folders.v1.yaml: PrepareWorkspace]
- [x] [Review][Patch] P5 Split `LockWorkspace` 409 into separate `IdempotencyConflict` and `LockConflict` response components/examples [hexalith.folders.v1.yaml: LockWorkspace responses]
- [x] [Review][Patch] P6 Add `projectionWatermark` / `stale` flag to `GetWorkspaceRetryEligibility` examples since it declares `eventually_consistent` [hexalith.folders.v1.yaml: examples]
- [x] [Review][Patch] P7 Make `WorkspaceRetryEligibility.freshness` required [hexalith.folders.v1.yaml: WorkspaceRetryEligibility]
- [x] [Review][Patch] P8 Extend `releaseReasonCode` enum with `task_cancelled` and `lock_revoked` [hexalith.folders.v1.yaml: releaseReasonCode enum]
- [x] [Review][Patch] P9 Change `WorkspaceTransitionInvalidProblem.details.attemptedTransition` from bare event-name string to structured `WorkspaceTransitionAttempt` shape [hexalith.folders.v1.yaml: WorkspaceTransitionInvalidProblem example]
- [x] [Review][Patch] P10 Add `lock_ownership_proof` to `SensitiveMetadataTier` enum [hexalith.folders.v1.yaml: SensitiveMetadataTier]
- [x] [Review][Patch] P11 Replace single-value `requestSchemaVersion` enum with `const: v1` in all three mutating-command request schemas [hexalith.folders.v1.yaml: PrepareWorkspaceRequest, LockWorkspaceRequest, ReleaseWorkspaceLockRequest]
- [x] [Review][Patch] P12 Make `WorkspaceTransitionAttempt.toState` required [hexalith.folders.v1.yaml: WorkspaceTransitionAttempt]
- [x] [Review][Patch] P13 Permit `date-time` formatted strings in `auditMetadata.additionalProperties` so `evidence_timestamp` can be typed [hexalith.folders.v1.yaml: auditMetadata schema]
- [x] [Review][Patch] P14 Fix `LockWorkspace` 503 response — uses `ReadModelUnavailable` on a write operation; should be `ProviderUnavailable` [hexalith.folders.v1.yaml: LockWorkspace responses]
- [x] [Review][Patch] P15 Add 503 `ProviderUnavailable` response to `ReleaseWorkspaceLock` [hexalith.folders.v1.yaml: ReleaseWorkspaceLock responses]
- [x] [Review][Patch] P16 Add `reconciliation_required` 409 response mapping on `ReleaseWorkspaceLock` so declared canonical category is surfaceable [hexalith.folders.v1.yaml: ReleaseWorkspaceLock responses]
- [x] [Review][Patch] P17 Add `folder_id` to `ReleaseWorkspaceLock` idempotency-equivalence (consistency with sibling operations) [hexalith.folders.v1.yaml: ReleaseWorkspaceLock x-hexalith-idempotency-equivalence]
- [x] [Review][Patch] P18 Add `WorkspaceTransitionEvidenceInvalid` example next to the valid one [hexalith.folders.v1.yaml: examples]
- [x] [Review][Patch] P19 Add `unlocked` and `expired-lease` `WorkspaceLockStatus` examples next to the existing positive example [hexalith.folders.v1.yaml: examples]
- [x] [Review][Patch] P20 Fix `docs/contract/workspace-lock-contract-groups.md` drift: it claims examples (expired-lease, retry-blocked, invalid-transition) that the YAML does not ship [docs/contract/workspace-lock-contract-groups.md]
- [x] [Review][Patch] P21 Align audit-metadata-keys across the six operations (currently `workspace_state` only on some) [hexalith.folders.v1.yaml: x-hexalith-audit-metadata-keys per operation]
- [x] [Review][Patch] P22 Fix `docs/contract/workspace-lock-contract-groups.md` `GetWorkspaceRetryEligibility` audit-metadata row to match YAML [docs/contract/workspace-lock-contract-groups.md]
- [x] [Review][Patch] P23 Strengthen Idempotency-Key absence assertion in `WorkspaceLockContractGroupTests.cs` — match exact ref `#/components/parameters/IdempotencyKey` or `name == "Idempotency-Key" && in == "header"` rather than suffix [tests/Hexalith.Folders.Contracts.Tests/OpenApi/WorkspaceLockContractGroupTests.cs:78-79]
- [x] [Review][Patch] P24 Replace tautological `.Order(StringComparer.Ordinal)` membership assertions with explicit `ShouldBe` over unordered set (e.g., `SetEquals` / `Contains`) so test catches missing or extra operations [tests/Hexalith.Folders.Contracts.Tests/OpenApi/WorkspaceLockContractGroupTests.cs:39]
- [x] [Review][Patch] P25 Add test asserting 401/403/404 safe-denial envelope shapes are byte-equal for workspace/lock group [tests/Hexalith.Folders.Contracts.Tests/OpenApi/WorkspaceLockContractGroupTests.cs]
- [x] [Review][Patch] P26 Add test verifying `lockOwnershipProof` value never appears in any unauthorized Problem Details example for the workspace/lock group [tests/Hexalith.Folders.Contracts.Tests/OpenApi/WorkspaceLockContractGroupTests.cs]
- [x] [Review][Patch] P27 Restore `/locks` to the forbidden-fragment allow-list in `TenantFolderProviderContractGroupTests.cs` (only `/workspaces` should have been removed for this story) [tests/Hexalith.Folders.Contracts.Tests/OpenApi/TenantFolderProviderContractGroupTests.cs]
- [x] [Review][Patch] P28 (from D1) Add an authz-first preamble to the workspace/lock Problem Details documentation explaining that 410/lock-conflict-details are only reachable after authorization; unauthorized callers always see safe-denial 404. Add note to `docs/contract/workspace-lock-contract-groups.md` and a description on `WorkspaceLockConflictProblem` + `WorkspaceLockExpiredProblem`. [hexalith.folders.v1.yaml, docs/contract/workspace-lock-contract-groups.md]
- [x] [Review][Patch] P29 (from D2) Make `lease` optional on `WorkspaceLockStatus`; ensure unlocked example omits `lease` cleanly. [hexalith.folders.v1.yaml: WorkspaceLockStatus]
- [x] [Review][Patch] P30 (from D3) Make `taskId` optional in `WorkspaceRetryEligibility` (remove from `required`). [hexalith.folders.v1.yaml: WorkspaceRetryEligibility]
- [x] [Review][Patch] P31 (from D4) Add narrow `LockState` enum and retype `WorkspaceLockStatus.lockState` from `LifecycleState` to `LockState`. [hexalith.folders.v1.yaml: components/schemas/LockState, WorkspaceLockStatus]
- [x] [Review][Patch] P32 (from D5) Remove 422 `WorkspaceTransitionInvalid` and 409 `WorkspaceAuthorizationRevoked` from `GetWorkspaceTransitionEvidence` responses; have 200 body carry result via `WorkspaceTransitionEvidence.result`. [hexalith.folders.v1.yaml: GetWorkspaceTransitionEvidence]
- [x] [Review][Patch] P33 (from D5) Remove `state_transition_invalid` from `GetWorkspaceLock` and `GetWorkspaceRetryEligibility` `x-hexalith-canonical-error-categories` (GETs cannot cause transitions). [hexalith.folders.v1.yaml]
- [x] [Review][Patch] P34 (from D5) Update `WorkspaceLockContractGroupTests.cs` assertion that every workspace operation includes `state_transition_invalid` — restrict to mutating commands only. [tests/Hexalith.Folders.Contracts.Tests/OpenApi/WorkspaceLockContractGroupTests.cs]
- [x] [Review][Patch] P35 (from D7) Remove `release_reason_code` from `ReleaseWorkspaceLock.x-hexalith-idempotency-equivalence`. [hexalith.folders.v1.yaml: ReleaseWorkspaceLock]

#### Deferred

- [x] [Review][Defer] V1 `allOf:[$ref]+description` style on `holderRef` works under 3.1 but is unidiomatic — deferred, style nit
- [x] [Review][Defer] V2 `ResolveRefs` only checks pointer existence, not type compatibility — deferred, test enhancement
- [x] [Review][Defer] V3 `EnumerateNamedFields` does not walk inline example map keys — deferred, test coverage enhancement
- [x] [Review][Defer] V4 `EnumerateNamedFields` double-yields property names — deferred, harmless inefficiency
- [x] [Review][Defer] V5 `GetOptionalScalar` throws opaque error on non-scalar — deferred, test internals nit
- [x] [Review][Defer] V6 Synthetic ULID-shaped IDs look pseudo-random — deferred, intentional opaque format
- [x] [Review][Defer] V7 `auditMetadata.additionalProperties` permits unbounded keys — deferred, robustness enhancement
- [x] [Review][Defer] V8 Hyphenated vs underscore form of read-consistency tokens between story prose and OpenAPI enum — deferred, prose/enum drift only
- [x] [Review][Defer] V9 Test reads files via repo-root lookup keyed on `Hexalith.Folders.slnx` — deferred, brittle if solution renamed

## Dev Notes

### Scope Boundaries

- This story authors Contract Spine operation groups for workspace preparation and workspace locks only.
- Contract-only output means OpenAPI contract declarations, synthetic examples, optional contract notes, and focused offline validation. Do not broaden this story into runtime behavior, generated consumers, provider integration, process managers, lock managers, workers, or CI workflow wiring.
- Primary files expected:

```text
src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml
src/Hexalith.Folders.Contracts/openapi/extensions/
```

- Optional supporting docs, only if needed:

```text
docs/contract/workspace-lock-contract-groups.md
```

- Do not add `src/Hexalith.Folders.Client/Generated/`, NSwag configuration, server endpoints, EventStore commands, aggregate logic, provider adapters, CLI/MCP commands, workers, UI, parity rows, or CI gates.
- Do not add operation groups owned by adjacent stories:
  - Story 1.7: tenant, folder, provider, repository binding, repository creation, and branch/ref policy.
  - Story 1.9: file mutation and context query.
  - Story 1.10: commit and workspace status.
  - Story 1.11: audit and operations-console query.
- Runtime workspace behavior belongs to Epic 4 stories, especially workspace preparation, task-scoped locks, path policy, mutation, commit, idempotency propagation, reconciliation, canonical errors, audit, and projection determinism.

### Current Repository State

- At story creation time, `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` was not present in the working tree.
- At story creation time, `docs/contract/idempotency-and-parity-rules.md` was not present in the working tree.
- At story creation time, `docs/exit-criteria/c6-transition-matrix-mapping.md`, `docs/exit-criteria/c3-retention.md`, `docs/exit-criteria/c4-input-limits.md`, and `docs/exit-criteria/s2-oidc-validation.md` were present.
- At story creation time, Story 1.5 was `in-progress` and had uncommitted development state. Treat this as active implementation context, not as permission to duplicate or overwrite its deliverables.
- The implementation agent must inspect the current repository state before editing because earlier stories may have completed after this story was created.

### Contract Group Requirements

- Use OpenAPI 3.1 at `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` as the single source of truth when the foundation exists.
- REST paths are URL-versioned under `/api/v1`, use lowercase hyphen-delimited segments, plural collection nouns where appropriate, and camelCase path parameters.
- Stable operation IDs matter because NSwag generation and the C13 parity oracle will consume them later.
- Tenant authority comes from auth context and EventStore envelopes. Payloads may include resource IDs to validate, but never authoritative tenant context.
- Workspace identity should be opaque and scoped by tenant, folder, repository binding, branch/ref policy, and task. Local working-copy paths are disposable cache metadata, never contract identity.
- Lock identity and ownership should be opaque. A lock token or owner reference may prove ownership within a tenant-scoped workspace context, but it must not reveal internal storage keys, local paths, provider details, or unauthorized resource existence.
- Treat any lock token or holder reference as a scoped contract proof, not as an authentication secret or provider credential. Schema descriptions and examples must make this distinction explicit so the forbidden-secret validator can allow only these narrow fields.
- Provider calls and filesystem work are asynchronous side effects owned by workers and process managers. Contract operations should expose accepted task/status metadata and failure categories, not implement the side effects.
- State-transition evidence must respect C6: every listed transition is canonical, and every unlisted state/event pair rejects with `state_transition_invalid` and leaves state unchanged.
- Operator-disposition labels are part of the contract metadata for status consumers. The labels are not decorative UI text; they drive diagnostic and parity expectations.

### Operation Inventory Seed

Use the operation names below as a starting point unless Story 1.5 or Story 1.6 has already frozen different names. If names differ, keep the OpenAPI `operationId` stable and record the mapping.

| Operation | Type | Required metadata |
| --- | --- | --- |
| `PrepareWorkspace` | Mutating command | idempotency, task ID, workspace scope, provider readiness, repository binding, branch/ref policy, audit keys, correlation, parity dimensions |
| `LockWorkspace` | Mutating command | idempotency, task ID, workspace/lock scope, single-active-writer behavior, lease/expiry metadata, audit keys, correlation, parity dimensions |
| `ReleaseWorkspaceLock` | Mutating command | idempotency, task ID, lock token/owner validation, release policy, audit keys, correlation, parity dimensions |
| `GetWorkspaceLock` | Query/status | read consistency, freshness, safe denial, lease/expiry metadata, lock owner metadata class, retry eligibility |
| `GetWorkspaceRetryEligibility` | Query/status | read consistency, current state, retryable flag, retry-after hint, canonical reason code, correlation, safe denial |
| `GetWorkspaceTransitionEvidence` | Query/status | read consistency, C6 state/event evidence, state transition invalid metadata, freshness, audit classification |

Do not add file mutation, context query, commit, audit timeline, or operations-console projection operations in this story.

### Party-Mode Hardening Notes

- Review each operation independently. The implementation must leave a visible mapping from each operation to its scope, idempotency or freshness contract, authorization outcomes, Problem Details, audit metadata, examples, parity dimensions, and reused Story 1.6/1.7 component references.
- Keep the contract/runtime boundary sharp. OpenAPI text may describe externally observable state, evidence, lease, retry, freshness, and error shapes, but must not prescribe worker/process-manager orchestration, aggregate logic, EventStore event sequencing, Tenants internals, storage mechanics, or runtime lock enforcement.
- Tenant authority is never caller-controlled. Path, query, or body identifiers may identify a resource to validate, but they must not establish or override tenant authority from authentication context and EventStore envelopes.
- C6 transition evidence is a contract-visible diagnostic shape. It must include current state, attempted transition where applicable, result, reason code, timestamp, correlation/audit metadata, and freshness, without exposing internal event schemas or durable keys.
- Lock lease metadata must be enough for clients to reason about state: lock identity, lease status, acquired/effective timestamp, expiry timestamp, holder/audit reference where appropriate, and read freshness for inspection responses.
- The exact retry policy matrix, lease duration defaults, renewal and clock-skew policy, expanded audit taxonomy, and runtime authorization-revocation mechanics remain deferred to approved source documents unless already resolved by prerequisite stories.

### Error and Audit Requirements

- Required canonical categories for this story include authentication failure, tenant authorization denied, folder ACL denied, cross-tenant access denied, provider readiness failed, repository binding unavailable, branch/ref policy invalid, workspace preparation failed, workspace locked, lock conflict, lock expired, lock not owned, stale workspace, authorization revocation detected, read-model unavailable, duplicate operation, idempotency conflict, unsupported provider capability, provider unavailable, provider rate limited, unknown provider outcome, reconciliation required, state transition invalid, validation error, not found, redacted, and internal error.
- Use RFC 9457 Problem Details plus Hexalith fields: `category`, `code`, `message`, `correlationId`, `retryable`, `clientAction`, and `details`.
- Safe-denial Problem Details must expose stable safe codes only; they must not reveal credential state, provider installation IDs, internal provider payloads, authorization reasoning, local workspace paths, lock storage keys, or whether protected tenant/folder/provider/repository/workspace/lock/task resources exist.
- Audit metadata is metadata-only. It may include tenant-scoped actor evidence, folder ID, workspace ID, lock ID, task ID, operation ID, correlation ID, provider binding reference, repository binding ID, branch/ref policy reference, timestamp, result, duration, retryability, and sanitized error category.
- Do not include provider tokens, credential material, file contents, diffs, generated context payloads, raw repository URLs, production URLs, local filesystem paths, or unauthorized resource existence.

### Idempotency, Correlation, And Read Consistency

- `PrepareWorkspace`, `LockWorkspace`, and `ReleaseWorkspaceLock` require caller-supplied `Idempotency-Key`.
- Non-mutating lock/workspace status queries do not accept `Idempotency-Key`.
- Equivalence is tenant-scoped and operation-scoped. Same key across tenants, changed command intent, changed target workspace identity, changed task identity, changed semantic payload, changed branch/ref policy, changed repository binding, changed lock owner/token where applicable, or changed behavior-affecting credential scope is non-equivalent.
- Mutation-tier TTL is 24 hours. Commit-tier TTL belongs to downstream commit work and inherits C3.
- Correlation IDs and task IDs must propagate through REST, SDK, CLI, MCP, EventStore envelopes, projections, audit, logs, and traces.
- Read operations declare one of the approved read consistency classes from Story 1.6/architecture: `snapshot-per-task`, `read-your-writes`, or `eventually-consistent`.
- Freshness metadata must make projection lag visible. Do not let status queries imply stronger consistency than the architecture allows.

### Previous Story Intelligence

- Story 1.1 establishes the solution scaffold and dependency direction.
- Story 1.2 establishes root configuration and forbids recursive nested-submodule initialization.
- Story 1.3 seeds shared fixtures: audit leakage corpus, parity schema, previous spine, idempotency encoding corpus, `tests/load`, and artifact templates.
- Story 1.4 owns C3 retention, C4 input limits, S-2 OIDC parameters, and C6 transition mapping. This story consumes those values but must not invent them.
- Story 1.5 defines operation inventory, idempotency equivalence, adapter parity dimensions, parity schema expectations, and encoding corpus rules. This story must consume its final artifact when available and record reference-pending gaps when it is not.
- Story 1.6 owns the OpenAPI foundation and shared `x-hexalith-*` vocabulary. This story must reuse it rather than redefine extension shapes.
- Story 1.7 owns tenant, folder, provider, repository, and branch/ref operations. This story must reference those components rather than duplicate them.
- Story 1.7 party-mode and elicitation traces hardened contract-only boundaries, safe-denial parity, operation allow-lists, provider diagnostic audience partitioning, and synthetic example checks. Apply the same discipline here.

### Testing Guidance

- Prefer a focused OpenAPI validation test or script in existing test/tool locations over broad runtime integration work.
- Validation must run offline without Aspire, Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, provider credentials, tenant data, production secrets, network calls, or initialized nested submodules.
- Validate OpenAPI parse, `$ref` resolution, unique operation IDs, exact `x-hexalith-*` metadata presence, idempotency/read-consistency completeness, safe tenant-authority boundaries, synthetic examples, C6 state metadata, safe-denial parity, and negative scope.
- Include contract-level assertions for lock conflict, expired/stale lock, authorization revocation, provider uncertainty, reconciliation required, and state transition invalid outcomes.
- Include allow-list assertions for the operation IDs and `/api/v1` path prefixes owned by this story so validation fails if file/context, commit/status, audit timeline, operations-console, runtime, generated-client, CLI, MCP, worker, UI, or CI artifacts appear.
- Include contract-level assertions that mutating operations reuse idempotency vocabulary, query operations reuse freshness/read-consistency vocabulary, tenant authority is absent from request payloads and client-controlled parameters, Problem Details components are reused, and synthetic examples cover revoked authorization, invalid transition, expired lease, retry eligible, and retry blocked outcomes.
- Include forbidden-reference checks so the Contract Spine does not add Server, Client, CLI, MCP, UI, Workers, process-manager, aggregate behavior, `Hexalith.EventStore`, or `Hexalith.Tenants` dependencies or descriptions.
- If the full solution is buildable, run `dotnet build Hexalith.Folders.slnx` from the repository root after focused validation. Record exact blockers if build cannot run due to prior scaffold state.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 1.8: Author workspace and lock contract groups`
- `_bmad-output/planning-artifacts/epics.md#Epic 1: Bootstrap Canonical Contract For Consumers And Adapters`
- `_bmad-output/planning-artifacts/epics.md#Epic 4: Repository-Backed Workspace Task Lifecycle`
- `_bmad-output/planning-artifacts/architecture.md#Workspace State Transition Matrix`
- `_bmad-output/planning-artifacts/architecture.md#Cross-Cutting Concerns Identified`
- `_bmad-output/planning-artifacts/architecture.md#Adapter Parity Contract`
- `_bmad-output/planning-artifacts/architecture.md#REST endpoint naming`
- `_bmad-output/planning-artifacts/architecture.md#HTTP headers`
- `_bmad-output/planning-artifacts/prd.md#Command and Query Contract`
- `_bmad-output/planning-artifacts/prd.md#Error Codes`
- `_bmad-output/planning-artifacts/prd.md#Contract and Quality Gates`
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
- `_bmad-output/project-context.md`
- `AGENTS.md#Git Submodules`

## Project Structure Notes

- The Contract Spine operation groups live under `src/Hexalith.Folders.Contracts/openapi/`.
- Human-readable contract notes may live under `docs/contract/`, but the OpenAPI document remains the source of truth.
- Runtime workspace process managers and workers belong later under `src/Hexalith.Folders.Workers/` and must not be implemented here.
- Domain aggregate state-transition behavior belongs later under `src/Hexalith.Folders/Aggregates/Folder/` and must not be implemented here.
- Server endpoints belong later under `src/Hexalith.Folders.Server/Endpoints/` and must not be implemented here.
- CLI and MCP wrappers belong later under `src/Hexalith.Folders.Cli/` and `src/Hexalith.Folders.Mcp/` and must not be implemented here.

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-11 | Created ready-for-dev story through `bmad-create-story` workflow. | Codex |
| 2026-05-11 | Applied party-mode review hardening for operation mapping, contract/runtime boundary, tenant authority, C6 evidence, Problem Details, idempotency/freshness, lock metadata, and validation expectations. | Codex |
| 2026-05-12 | Applied advanced-elicitation hardening for lock ownership proof, safe-denial indistinguishability, deferred-decision precision, and validation exceptions. | Codex |
| 2026-05-13 | Implemented workspace and lock Contract Spine groups with focused offline validation. | Codex |
| 2026-05-13 | Applied code-review patches P1–P35 (35 patches) and resolved decisions D1–D7. All targeted contract tests (18) and full solution tests (62) green; build 0 warnings. | Claude |

## Party-Mode Review

- Date: 2026-05-11T22:10:50Z
- Selected story key: `1-8-author-workspace-and-lock-contract-groups`
- Command/skill invocation used: `/bmad-party-mode 1-8-author-workspace-and-lock-contract-groups; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor)
- Findings summary: all reviewers found the story directionally viable but too ambiguous for immediate development without explicit operation-by-operation obligations, stronger contract-only boundaries, tenant-authority negative checks, reusable idempotency/freshness vocabulary, C6 transition evidence shape, lock lease metadata, canonical Problem Details coverage, and focused OpenAPI validation expectations.
- Changes applied: added acceptance criteria for operation-level reviewability, contract/runtime separation, and diagnostic metadata boundaries; added subtasks for operation mapping, C6 evidence examples, Problem Details matrix, reusable idempotency/freshness vocabulary, tenant-authority validation, forbidden-reference checks, and parity coverage; added dev notes capturing party-mode hardening constraints.
- Findings deferred: exact retry eligibility policy, lease duration defaults, renewal policy, clock-skew policy, expanded audit taxonomy, and runtime authorization-revocation mechanics remain deferred to approved source documents or `TODO(reference-pending)` markers.
- Final recommendation: ready-for-dev

## Advanced Elicitation

- Date: 2026-05-12T01:03:52Z
- Selected story key: `1-8-author-workspace-and-lock-contract-groups`
- Command/skill invocation used: `/bmad-advanced-elicitation 1-8-author-workspace-and-lock-contract-groups`
- Batch 1 method names: Red Team vs Blue Team; Security Audit Personas; Failure Mode Analysis; Self-Consistency Validation; Critique and Refine.
- Reshuffled Batch 2 method names: Pre-mortem Analysis; First Principles Analysis; Socratic Questioning; Comparative Analysis Matrix; Occam's Razor Application.
- Findings summary: elicitation found the story was broadly ready but still had ambiguity around token-shaped lock ownership fields, safe-denial distinguishability during lock inspection, vague deferred TODO handling, and validator exceptions that could accidentally permit credential-shaped names.
- Changes applied: added an acceptance criterion for non-secret opaque lock ownership proof; added subtasks for per-operation deferred-decision tracking, lock ownership proof descriptions, safe-denial indistinguishability, tenant-authority conflict handling, and narrow validator exceptions; added dev guidance clarifying lock proof versus authentication secrets.
- Findings deferred: exact retry eligibility policy, lease duration defaults, renewal policy, clock-skew policy, expanded audit taxonomy, runtime authorization-revocation mechanics, and any prerequisite tenant path conflict remain deferred to approved source documents or explicit `TODO(reference-pending)` markers.
- Final recommendation: ready-for-dev

## Dev Agent Record

### Agent Model Used

GPT-5

### Debug Log References

- 2026-05-13: Loaded project context, sprint status, Story 1.8, Contract Spine foundation, extension vocabulary, Story 1.5 rules, Story 1.7 contract notes, C3/C4/C6/S-2 exit criteria, and existing OpenAPI validation tests.
- 2026-05-13: Red phase: added `WorkspaceLockContractGroupTests`; focused test run failed for missing workspace/lock operations, lease schemas, and notes.
- 2026-05-13: Green/refactor phase: authored workspace/lock OpenAPI paths, schemas, examples, Problem Details responses, canonical categories, audit keys, contract notes, and updated operation allow-list validation.
- 2026-05-13: Validation: `dotnet test .\tests\Hexalith.Folders.Contracts.Tests\Hexalith.Folders.Contracts.Tests.csproj --no-restore --filter "WorkspaceLockContractGroupTests|TenantFolderProviderContractGroupTests|ContractSpineFoundationTests"` passed, 15 tests.
- 2026-05-13: Validation: `dotnet test .\tests\Hexalith.Folders.Contracts.Tests\Hexalith.Folders.Contracts.Tests.csproj --no-restore` passed, 16 tests.
- 2026-05-13: Validation: `dotnet build .\Hexalith.Folders.slnx --no-restore` succeeded with 0 warnings and 0 errors.
- 2026-05-13: Validation: `dotnet test .\Hexalith.Folders.slnx --no-restore` passed, 61 tests across solution projects.
- 2026-05-13: Code-review patches applied (P1–P35). Validation: `dotnet test .\tests\Hexalith.Folders.Contracts.Tests\Hexalith.Folders.Contracts.Tests.csproj --no-restore --filter "WorkspaceLockContractGroupTests|TenantFolderProviderContractGroupTests|ContractSpineFoundationTests"` passed, 17 tests.
- 2026-05-13: Code-review patches applied. Validation: `dotnet build .\Hexalith.Folders.slnx --no-restore` succeeded with 0 warnings and 0 errors.
- 2026-05-13: Code-review patches applied. Validation: `dotnet test .\Hexalith.Folders.slnx --no-restore` passed, 62 tests across solution projects.

### Implementation Plan

- Reuse Story 1.6/1.7 OpenAPI components and extension metadata instead of introducing parallel vocabulary.
- Add only six Story 1.8 operations: `PrepareWorkspace`, `LockWorkspace`, `ReleaseWorkspaceLock`, `GetWorkspaceLock`, `GetWorkspaceRetryEligibility`, and `GetWorkspaceTransitionEvidence`.
- Model workspace/lock responses as metadata-only contract evidence, with accepted command, retry eligibility, lease metadata, C6 transition evidence, and safe Problem Details.
- Keep runtime behavior, generated SDK output, NSwag, CLI/MCP, workers, UI, parity rows, and CI gates deferred to later stories.

### Completion Notes List

- Confirmed Story 1.6/1.7 prerequisites are present and reused the existing OpenAPI foundation, extension vocabulary, safe-denial responses, freshness metadata, idempotency metadata, folder/repository/branch-ref references, and C6 lifecycle vocabulary.
- Added workspace preparation, lock acquisition, lock release, lock inspection, retry eligibility, and transition-evidence operation groups under `/api/v1/folders/{folderId}/workspaces/{workspaceId}/...`.
- Added metadata-only schemas and examples for lease status, retry eligibility, C6 transition evidence, invalid transitions, authorization revocation, lock conflict, and expired locks.
- Added focused OpenAPI tests for operation allow-list, `$ref` resolution, required `x-hexalith-*` metadata, idempotency/read-consistency rules, tenant-authority absence, lock proof exception handling, synthetic examples, and negative scope.
- Added contract notes mapping every Story 1.8 operation to scope, idempotency/freshness, authorization outcomes, Problem Details, audit metadata, examples, parity dimensions, component reuse, and deferred owners.
- No nested submodules were initialized or updated; no runtime handlers, domain aggregates, provider adapters, generated SDK output, NSwag configuration, CLI/MCP tools, workers, UI pages, parity oracle rows, or CI gates were added.

### File List

- `docs/contract/workspace-lock-contract-groups.md`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/TenantFolderProviderContractGroupTests.cs`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/WorkspaceLockContractGroupTests.cs`
