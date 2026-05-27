---
baseline_commit: 0562a78
---

# Story 4.5: Enforce workspace path policy before file mutations

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer or AI agent holding the workspace lock,
I want every file path normalized and validated before mutation,
so that no file operation can escape the workspace or create ambiguous provider-specific paths.

## Acceptance Criteria

1. Given an authorized caller submits `AddFile`, `ChangeFile`, or `RemoveFile` with a valid `Idempotency-Key`, `X-Correlation-Id`, `X-Hexalith-Task-Id`, canonical route `folderId`/`workspaceId`, and `FileMutationRequest`, when file mutation intake runs, then authorization, held-lock, workspace-state, and path-policy checks complete before any provider, Git, working-copy, filesystem, query, or content side effect.
2. Given a request contains a valid workspace-root-relative `PathMetadata.normalizedPath`, when path policy accepts it, then the accepted command uses NFC-normalized, forward-slash, workspace-relative metadata and records only safe path evidence: `operation_id`, `file_operation_kind`, `path_policy_class`, path metadata digest/reference, task, correlation, idempotency, workspace, and folder identifiers.
3. Given path validation detects traversal, absolute path, empty segment, mixed separators, reserved platform name, control/invisible character, Unicode normalization ambiguity, percent/dot-segment smuggling, workspace-root escape, symlink escape, or case collision, when the request is evaluated, then the operation rejects with canonical category/code `path_policy_denied` or the existing contract-compatible `path_validation_failed` mapping and does not echo the unsafe raw path.
4. Given two paths would address the same target under the provider or workspace case-sensitivity policy, when the second mutation is submitted, then it is rejected as a case collision before any mutation event or side effect is produced.
5. Given a path targets a symlink or reparse-point escape outside the prepared workspace root, when path policy evaluates the canonicalized target, then the mutation is denied without revealing the host-local target path.
6. Given the same idempotency key is replayed with an equivalent file mutation payload (`content_hash_reference` when applicable, `file_operation_kind`, `operation_id`, `path_metadata`, `path_policy_class`, `task_id`, `workspace_id` in lexicographic order), when the request is observed again, then the same logical metadata-only result is returned without duplicate `FileMutated` evidence or duplicate downstream work.
7. Given the same idempotency key is reused with a non-equivalent payload, or the request has an invalid schema version, malformed identifier, missing required header, operation kind/transport mismatch, missing path metadata, wrong task, wrong workspace, missing or expired lock, non-owned lock, archived folder, non-repository-backed folder, stale/inaccessible/reconciliation state, or denied path policy, when the request is evaluated, then the operation returns the canonical safe result/problem category and emits no state-changing mutation event.
8. Given existing folder creation, repository binding, branch/ref policy, provider readiness, workspace preparation, lock acquisition/release, C6 replay, authorization, safe denial, Contract Spine, generated client helpers, and focused tests exist, when Story 4.5 is implemented, then those behaviors and tests continue to pass without manually editing generated client files.

## Tasks / Subtasks

- [x] Add path-policy domain vocabulary and validator. (AC: 1, 2, 3, 4, 5, 7)
  - [x] Add `PathMetadata`, `WorkspacePathPolicyDecision`, `WorkspacePathPolicyResult`, and a deterministic `WorkspacePathPolicyValidator` under `src/Hexalith.Folders/Aggregates/Folder/` or a narrower existing concept folder if one exists during implementation.
  - [x] Validate `PathMetadata.normalizedPath`, `displayName`, `pathPolicyClass`, and `unicodeNormalization` against the Contract Spine constraints before aggregate handling.
  - [x] Enforce forward slashes only, no leading slash, no drive/root/UNC/device paths, no empty segments, no `.` or `..` path segments, no backslashes, no control characters, no invisible format characters, no trailing-space/dot ambiguity, and NFC normalization.
  - [x] Reject Windows reserved base names (`CON`, `NUL`, `PRN`, `AUX`, `COM1`-`COM9`, `LPT1`-`LPT9`) case-insensitively and with optional extensions, while keeping rejection details sanitized.
  - [x] Add explicit denial reasons as internal/test-visible vocabulary, but map external responses to canonical safe categories without raw path echoing.

- [x] Add file mutation command/request/event scaffolding for path-policy acceptance only. (AC: 1, 2, 6, 7)
  - [x] Add `MutateWorkspaceFile` or separate `AddFile`, `ChangeFile`, and `RemoveFile` commands only as far as needed to accept/reject path-policy-checked mutation intent.
  - [x] Add immutable request records for route-authoritative folder/workspace, operation ID, file operation kind, path metadata, optional content hash reference, optional byte length, transport operation, actor, correlation, task, idempotency, and optional payload-tenant comparison evidence.
  - [x] Add a metadata-only event such as `WorkspaceFileMutationAccepted` or `WorkspaceFilePathPolicyAccepted` that records path evidence digest/reference, operation kind, path policy class, content hash reference when applicable, byte length when applicable, workspace/task/correlation/idempotency metadata, and occurred-at timestamp. Do not include file contents, diffs, local filesystem paths, provider payloads, repository URLs, branch names, emails, display names, tokens, or raw unsafe paths.
  - [x] Reuse `FolderWorkspaceLifecycleEvent.FileMutated` for the C6 transition only after path policy and held-lock checks pass.
  - [x] Do not implement actual file add/change content writes, streamed transport processing, provider/Git writes, delete ordering, commit behavior, context queries, cleanup, CLI, MCP, UI, or workers in this story.

- [x] Implement deterministic aggregate handling. (AC: 1, 2, 3, 4, 6, 7, 8)
  - [x] Extend `FolderCommandValidator` for the file mutation command: require `requestSchemaVersion == "v1"`, canonical `workspaceId`, canonical `operationId`, canonical task/correlation/idempotency identifiers, valid operation kind, valid transport-operation pairing, and valid path metadata.
  - [x] Compute file mutation idempotency fingerprints from the Contract Spine fields in lexicographic order. For `AddFile`/`ChangeFile`: `content_hash_reference`, `file_operation_kind`, `operation_id`, `path_metadata`, `path_policy_class`, `task_id`, `workspace_id`. For `RemoveFile`: `file_operation_kind`, `operation_id`, `path_metadata`, `path_policy_class`, `task_id`, `workspace_id`.
  - [x] Keep tenant authority outside OpenAPI equivalence fields; tenant scoping belongs to the repository/idempotency key partition and authoritative EventStore/auth context.
  - [x] Require created, active, repository-bound state, exact current `WorkspaceId`, held active lock, matching lock holder task, unexpired lock, and C6 state `locked` or `changes_staged` before accepting path-policy-passed mutation.
  - [x] Transition `locked -> changes_staged` on the first accepted `FileMutated` and keep `changes_staged -> changes_staged` on additional accepted file mutations.
  - [x] Reject `ready`, `dirty`, `committed`, `failed`, `inaccessible`, `unknown_provider_outcome`, and `reconciliation_required` states without changing aggregate state.
  - [x] Preserve aggregate purity: no filesystem inspection, symlink resolution, provider calls, Git calls, Dapr calls, logging, service resolution, clock reads, read-model writes, lock timers, background renewal, or content decoding inside aggregate transitions.

- [x] Add service-level path-policy orchestration. (AC: 1, 3, 4, 5, 7)
  - [x] Add a `WorkspaceFileMutationService` following the shape of `WorkspaceLockAcquisitionService` and `WorkspaceLockReleaseService`: layered authorization first, command validation, path-policy validation, stream load, aggregate handling, repository idempotency lookup, atomic append, append-conflict reread, and metadata-only rejections.
  - [x] Use action token `mutate_files`; update action-token mapping, effective-permission tests, and endpoint authorization wiring if needed.
  - [x] Keep filesystem-dependent checks, such as symlink/reparse-point target and case-collision evidence, behind a small injected policy/evidence interface so unit tests can prove ordering without touching real workspaces.
  - [x] If no workspace filesystem abstraction exists yet, add only the minimal port needed to answer path policy evidence (`NoEscape`, `SymlinkEscape`, `CaseCollision`, `Unavailable`) and return safe failures. Full working-copy mutation belongs to later stories.
  - [x] Treat policy evidence unavailability as a safe denial/failure before mutation, not as permission to proceed.

- [x] Wire runtime endpoints and EventStore processor dispatch for mutation intake. (AC: 1, 6, 7, 8)
  - [x] Add command type constants for `AddFile`, `ChangeFile`, and `RemoveFile` in `FoldersServerModule` only if runtime dispatch uses distinct command types; otherwise document the shared command type and operation-kind switch in tests.
  - [x] Add REST handlers for `POST /api/v1/folders/{folderId}/workspaces/{workspaceId}/files/add`, `PUT /api/v1/folders/{folderId}/workspaces/{workspaceId}/files/change`, and `POST /api/v1/folders/{folderId}/workspaces/{workspaceId}/files/remove` that match the existing OpenAPI routes.
  - [x] Reject missing/malformed `Idempotency-Key`, `X-Correlation-Id`, `X-Hexalith-Task-Id`, malformed route identifiers, unknown JSON fields, invalid request schema, operation-kind/route mismatch, and malformed path metadata before gateway dispatch.
  - [x] Use the route `workspaceId` as authoritative; request body path/workspace metadata must not override the route.
  - [x] Extend `FolderDomainProcessor` to deserialize file mutation payloads and dispatch through the service with authoritative tenant, actor, organization, task, correlation, and idempotency evidence from the EventStore envelope.
  - [x] Normalize gateway/domain reason codes for path policy and file mutation failures to canonical REST problem categories without leaking raw paths or protected resource existence.
  - [x] Do not manually edit `src/Hexalith.Folders.Client/Generated/*`; if generated artifacts drift, update the OpenAPI spine/generator inputs and regenerate through the established pipeline.

- [x] Add focused tests for path-policy safety and mutation intake. (AC: 1-8)
  - [x] Add validator tests for accepted canonical path, traversal, absolute path, Windows drive path, UNC/device path, mixed separators, empty segment, `.`/`..` segment, encoded traversal, reserved name with extension, control characters, invisible Unicode format characters, non-NFC input, trailing-space/dot ambiguity, and over-length path/display name.
  - [x] Add service tests proving authorization runs before path policy and path policy runs before aggregate append/provider/filesystem mutation.
  - [x] Add service tests for symlink escape, case collision, policy evidence unavailable, lock-not-owned, expired lock, wrong task, wrong workspace, stale/reconciliation states, and no raw path echo in rejections.
  - [x] Add aggregate tests for first mutation `locked -> changes_staged`, additional mutation `changes_staged -> changes_staged`, idempotent replay, idempotency conflict, invalid schema, archived folder, missing workspace, wrong workspace, missing lock, and non-mutable C6 states.
  - [x] Add endpoint tests for Add/Change/Remove route shape, route-authoritative `workspaceId`, required headers, malformed JSON, unknown fields, operation-kind/route mismatch, safe denial, 413 inline-too-large mapping if the handler evaluates D-9 metadata early, path-policy denial, lock conflict, idempotency conflict, and reason-code normalization.
  - [x] Add metadata-only leakage tests using `tests/fixtures/audit-leakage-corpus.json`; ensure unsafe path strings, file contents, diffs, provider payloads, local absolute paths, credentials, tokens, emails, display names, and generated context markers do not appear in events, results, Problem Details, logs/test diagnostics, or examples.

- [x] Preserve contract and regression gates. (AC: 8)
  - [x] Run focused aggregate tests under `tests/Hexalith.Folders.Tests/Aggregates/Folder`.
  - [x] Run focused path-policy validator/service tests added by this story.
  - [x] Run focused server endpoint tests changed by this story.
  - [x] Run `tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -parallel none -noLogo -class Hexalith.Folders.Contracts.Tests.OpenApi.FileContextContractGroupTests` if built, or the equivalent `dotnet test` filter.
  - [x] Run focused action catalog/action-token mapper tests if command mapping changes.
  - [x] Run `git diff --check`.

## Dev Notes

### Source Context

- Epic 4 owns the repository-backed workspace task lifecycle: prepare workspaces, acquire locks, mutate files safely, query bounded context, commit changes, and expose deterministic failure/status/idempotency/redaction behavior. [Source: `_bmad-output/planning-artifacts/epics.md#Epic 4: Repository-Backed Workspace Task Lifecycle`]
- Story 4.5 requires normalization and validation before mutation, rejecting traversal, absolute paths, mixed separators, reserved names, symlink escapes, Unicode ambiguity, and case collisions with `path_policy_denied` and no unsafe path echoing. [Source: `_bmad-output/planning-artifacts/epics.md#Story 4.5: Enforce workspace path policy before file mutations`]
- PRD file operations require workspace-root confinement, path canonicalization, traversal rejection, symlink policy, binary/large-file policy, encoding policy, and case-collision handling. [Source: `_bmad-output/planning-artifacts/prd.md#Endpoint Specifications`]
- PRD FR32-FR33 require authorized add/change/remove operations in a prepared and locked workspace, and rejection of operations that violate workspace boundary, path, branch/ref, lock, tenant, provider, or folder policy. [Source: `_bmad-output/planning-artifacts/prd.md#File Operations and Context Queries`]
- PRD FR44 requires the error taxonomy to distinguish path policy denial/path validation failure from validation, tenant, folder ACL, lock, stale workspace, file operation, and idempotency failures. [Source: `_bmad-output/planning-artifacts/prd.md#Error, Status, and Diagnostics Contract`]
- Architecture concern 7 defines path security as workspace-root confinement, canonicalization, traversal rejection, symlink policy, binary/large-file policy, encoding/Unicode normalization, reserved-name handling, and case-collision handling. [Source: `_bmad-output/planning-artifacts/architecture.md#Cross-Cutting Concerns`]
- C6 defines `locked -> changes_staged` on first `FileMutated` and `changes_staged -> changes_staged` on additional `FileMutated`; unlisted state/event pairs reject with `state_transition_invalid`. [Source: `_bmad-output/planning-artifacts/architecture.md#Workspace State Transition Matrix (C6 -- Enumerated)`]
- D-9 defines two REST content transports: `PutFileInline` at or below 262144 bytes and `PutFileStream` above that boundary, with SDK convenience behavior deferred to generated/helper code. Story 4.5 should not implement full content transport side effects; it may validate metadata needed before path-policy-safe acceptance. [Source: `_bmad-output/planning-artifacts/architecture.md#Decision Log`]
- The Contract Spine already declares `AddFile`, `ChangeFile`, and `RemoveFile` routes, required idempotency/correlation/task headers, `FileMutationRequest`, D-9 transport metadata, canonical error categories, and mutation equivalence fields. [Source: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#/api/v1/folders/{folderId}/workspaces/{workspaceId}/files/add`]
- `PathMetadata` requires `normalizedPath`, `displayName`, `pathPolicyClass`, and `unicodeNormalization`; it is workspace-root-relative, forward-slash, no leading slash, NFC, and metadata-only. [Source: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#PathMetadata`]
- Contract notes explicitly say Epic 4 owns runtime file mutation behavior, prepared-workspace checks, held-lock enforcement, path policy execution, context-query execution, provider/Git/filesystem side effects, and reconciliation. [Source: `docs/contract/file-context-contract-groups.md#Reuse Points`]
- Contract notes warn that the closed `pathPolicyClass` vocabulary and non-ASCII Unicode allow-list remain deferred; current `PathMetadata.normalizedPath` is transitional ASCII-only even though `unicodeNormalization: NFC` is preserved. [Source: `docs/contract/file-context-contract-groups.md#Path policy class vocabulary (deferred)`; `docs/contract/file-context-contract-groups.md#Path-metadata Unicode policy (deferred)`]
- Generated idempotency helpers already include `FileMutationRequest.ComputeIdempotencyHash` and derive `path_policy_class` from `PathMetadata.PathPolicyClass`; do not hand-edit generated helpers. [Source: `src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs#FileMutationRequest`]

### Previous Story Intelligence

- Story 4.4 implemented lock inspection and release, added release command/service/event handling, REST routes, domain processor dispatch, focused tests, and action-token mapping. Reuse its service pattern for authorization-first mutation intake. [Source: `_bmad-output/implementation-artifacts/4-4-inspect-lock-state-and-release-the-workspace-lock.md#Dev Agent Record`]
- Story 4.4 confirms current runtime patterns for command services: layered authorization, command validation, stream load, aggregate handling, repository idempotency lookup, atomic append, append-conflict reread, and metadata-only rejections. [Source: `src/Hexalith.Folders/Aggregates/Folder/WorkspaceLockReleaseService.cs`]
- Story 4.4 added lock status and release routes but no file mutation routes. Story 4.5 should add only Add/Change/Remove runtime intake needed for path-policy enforcement; full content/provider mutation remains later. [Source: `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`]
- Story 4.4 senior review fixed reason-code normalization drift for release-specific errors. Repeat the same discipline for path-policy denials so gateway reason codes, REST Problem Details, tests, and contract categories stay aligned. [Source: `_bmad-output/implementation-artifacts/4-4-inspect-lock-state-and-release-the-workspace-lock.md#Previous Story Intelligence`]
- Known environmental failures from prior stories: broad solution/AppHost builds can try blocked NuGet network access, and full server route tests can fail in this sandbox on Kestrel socket binding. Prefer focused project builds and in-process xUnit classes unless this story explicitly needs live hosts. [Source: `_bmad-output/implementation-artifacts/4-4-inspect-lock-state-and-release-the-workspace-lock.md#Previous Story Intelligence`]

### Existing Implementation State

- `FolderStateTransitions` already includes `FileMutated`, with `Locked -> ChangesStaged` and `ChangesStaged -> ChangesStaged`; tests already assert the C6 matrix vocabulary and no raw path metadata in transition state. Extend this rather than adding a parallel mutation state machine.
- `FolderWorkspaceLifecycleEvent.FileMutated` exists, but there is no concrete file mutation command/event/service yet under `src/Hexalith.Folders/Aggregates/Folder/`.
- `FolderAggregate` currently handles `PrepareWorkspace`, `LockWorkspace`, and `ReleaseWorkspaceLock` with deterministic validation, idempotency, active-mutation guards, C6 transition checks, and metadata-only events. Add file mutation handling beside these methods.
- `FolderCommandValidator` already contains canonical identifier checks, NFC metadata canonicalization, SHA-256 length-prefixed fingerprinting, release fingerprinting, and metadata leakage blocklists. Add path-policy validation and file mutation fingerprinting here or in a pure helper invoked from here.
- `FolderResultCode` currently includes `LockConflict`, `LockNotOwned`, `LockExpired`, `UnknownProviderOutcome`, `ReconciliationRequired`, `ValidationFailed`, and `StateTransitionInvalid`, but no explicit `PathPolicyDenied`/`PathValidationFailed` result. If adding a result code, update server mapping, rejection events, tests, and contract category mapping together.
- `FoldersDomainServiceEndpoints` maps workspace preparation, lock acquisition, lock inspection, and lock release. It does not yet map Add/Change/Remove file mutation endpoints.
- `FolderDomainProcessor` dispatches archive, repository binding, branch policy, prepare, lock, and release command types. It does not yet dispatch file mutation command types.
- `FolderAccessAction` and `EffectivePermissionsActionCatalog` already include `mutate_files`; reuse it for file mutation authorization instead of inventing a new action token.
- Provider readiness profiles already expose `file_mutation_support`, but Story 4.5 should not perform live provider mutation or assume GitHub/Forgejo path behavior beyond policy evidence supplied to the service.

### Architecture Guardrails

- Use .NET 10, nullable-aware C#, file-scoped namespaces, one primary public type per file, PascalCase public members, camelCase locals/parameters, central package versions, xUnit v3, and Shouldly. [Source: `_bmad-output/project-context.md#Technology Stack & Versions`; `_bmad-output/project-context.md#Testing Rules`]
- Aggregate handlers must stay deterministic and side-effect free. Pass timestamps from callers; never perform I/O, provider calls, Dapr calls, logging, filesystem work, service resolution, or clock reads inside aggregate transitions. [Source: `_bmad-output/project-context.md#Language-Specific Rules`]
- Authorization order is contractual: JWT validation, EventStore claim transform, tenant-access freshness, folder ACL, EventStore validator, then Dapr deny-by-default policy. Do not reorder or skip layers for file mutation. [Source: `_bmad-output/project-context.md#Critical Don't-Miss Rules`]
- Client-controlled tenant/principal values from headers, query strings, or payloads are comparison inputs only. Authoritative tenant and principal values come from authenticated context and EventStore claim-transform evidence. [Source: `_bmad-output/project-context.md#Framework-Specific Rules`]
- Never search, glob, list, partially read, inspect, or mutate paths before tenant access, folder ACL, and path policy pass. [Source: `_bmad-output/project-context.md#Critical Don't-Miss Rules`]
- Metadata-only is mandatory across events, projections, logs, traces, metrics, audit records, problem details, docs examples, and tests. File contents, diffs, provider payloads, raw unsafe paths, and local filesystem paths are forbidden outside authorized request bodies. [Source: `_bmad-output/project-context.md#Critical Don't-Miss Rules`]
- EventStore domain logic should return typed `DomainResult`/`FolderResult` rejections rather than throwing for business rule violations; EventStore owns envelope metadata, and command extension metadata must be sanitized at the API boundary. [Source: `Hexalith.EventStore/_bmad-output/project-context.md#Critical Don't-Miss Rules`]
- Tenants remains the source of tenant membership truth; consumers must preserve tenant isolation and never trust user-supplied claims for authorization. [Source: `Hexalith.Tenants/_bmad-output/project-context.md#Critical Don't-Miss Rules`]
- Do not initialize nested submodules or use recursive submodule commands. [Source: `AGENTS.md`]

### Files To Touch

- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidationResult.cs` if new accepted metadata is required.
- `src/Hexalith.Folders/Aggregates/Folder/FolderResult.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderResultCode.cs` if a path-specific result code is added.
- `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs`
- `src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs` if projection/replay needs mutation evidence materialized.
- New file mutation command/request/event/service/path-policy types under `src/Hexalith.Folders/Aggregates/Folder/`.
- `src/Hexalith.Folders.Server/FoldersServerModule.cs`
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs`
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs`
- `src/Hexalith.Folders.Server/Authorization/FolderCommandActionTokenMapper.cs` if new command type mappings are added.
- Focused tests under `tests/Hexalith.Folders.Tests/Aggregates/Folder/`, `tests/Hexalith.Folders.Server.Tests/`, `tests/Hexalith.Folders.Contracts.Tests/OpenApi/`, and authorization/action catalog tests if mapping changes.

### Do Not Touch

- Do not implement full inline/streamed content processing, provider writes, Git writes, delete ordering, commit execution, context query execution, cleanup automation, lock renewal scheduler, background expiry processing, CLI commands, MCP tools, UI pages, or repair workflows.
- Do not hand-edit `src/Hexalith.Folders.Client/Generated/*`.
- Do not loosen `PathMetadata` contract constraints to onboard non-ASCII filenames in this story; contract notes say the Unicode allow-list is still deferred.
- Do not treat `displayName` as authoritative path input. `normalizedPath` plus server-side policy evidence drives path decisions.
- Do not log, emit, or return unsafe raw path strings on denials. Use sanitized reason codes/classes and digests/references where evidence is needed.
- Do not trust client-supplied `pathPolicyClass` as authority. It may participate in idempotency equivalence, but server-side policy must compute/confirm the class before accepting mutation.
- Do not allow read/query/list/glob/search as a side effect of validating path policy except through a constrained policy evidence port that returns metadata-only decisions.
- Do not add package versions directly to `.csproj` files.
- Do not use live GitHub, Forgejo, Aspire, Dapr sidecars, Docker, Keycloak, Redis, network-dependent tests, or nested-submodule initialization.

### Testing

- Recommended focused validation:
  - `dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore --tlp:off -v:minimal`
  - `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - `tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -parallel none -noLogo -namespace Hexalith.Folders.Tests.Aggregates.Folder`
  - focused path-policy validator/service tests added by this story
  - `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - focused server endpoint tests changed by this story
  - `tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -parallel none -noLogo -class Hexalith.Folders.Contracts.Tests.OpenApi.FileContextContractGroupTests`
  - focused action-token mapper/action catalog tests if command mappings change
  - `git diff --check`

### Regression Traps

- Path policy must run after authorization but before any provider, Git, working-copy, filesystem mutation, context query, or event append that would imply mutation acceptance.
- The route `workspaceId` is authoritative. A body value or generated helper value must not override the route.
- `PathMetadata.pathPolicyClass` is currently opaque and pattern-bounded, not a closed enum. Do not hardcode a final class list unless this story explicitly creates and tests that list.
- Current contract path metadata is ASCII-only. Accepting arbitrary Unicode paths without a normative allow-list would break the contract and security tests.
- Case-collision checks must use the configured provider/workspace policy, not the host OS default. Windows, Linux, GitHub, and Forgejo assumptions can diverge.
- Symlink/reparse-point checks must not expose host-local target paths. Denials should say policy denied, not where the link points.
- File mutation idempotency must include `path_policy_class` and the canonical path metadata object exactly as contract helpers expect. Excluding either can make unsafe retry changes look equivalent.
- `content_hash_reference` participates for Add/Change but not Remove. Including it for Remove or excluding it for Add/Change will drift from generated helpers and contract tests.
- `FileMutated` changes C6 state. Do not emit it for a path-policy denial, validation failure, idempotency conflict, missing/expired/non-owned lock, or authorization failure.
- Existing lock release behavior must keep rejecting staged changes; Story 4.5 will create `changes_staged`, so do not accidentally make release discard or unlock staged work.
- Existing generated SDK methods and idempotency helpers already model file mutation requests. Runtime code should consume those contracts, not fork new wire shapes.

### Latest Technical Context

- No new external library or live provider API is required for Story 4.5. Implementation should use repository-pinned .NET 10, EventStore, Dapr, OpenAPI, NSwag, xUnit v3, and Shouldly versions already captured in repository configuration and `_bmad-output/project-context.md`.
- Network research was not needed because the story is governed by project-local Contract Spine, PRD, architecture C6/D-9 decisions, prior runtime patterns, and generated helper contracts.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 4.5: Enforce workspace path policy before file mutations`
- `_bmad-output/planning-artifacts/epics.md#Epic 4: Repository-Backed Workspace Task Lifecycle`
- `_bmad-output/planning-artifacts/prd.md#Endpoint Specifications`
- `_bmad-output/planning-artifacts/prd.md#File Operations and Context Queries`
- `_bmad-output/planning-artifacts/prd.md#Error, Status, and Diagnostics Contract`
- `_bmad-output/planning-artifacts/architecture.md#Workspace State Transition Matrix (C6 -- Enumerated)`
- `_bmad-output/planning-artifacts/architecture.md#Decision Log`
- `docs/contract/file-context-contract-groups.md`
- `docs/contract/idempotency-and-parity-rules.md#Mutating Command Equivalence`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#PathMetadata`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#FileMutationRequest`
- `src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs#FileMutationRequest`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/FileContextContractGroupTests.cs`
- `_bmad-output/implementation-artifacts/4-4-inspect-lock-state-and-release-the-workspace-lock.md`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-27: Story created via `bmad-create-story` workflow using explicit Story ID 4.5, sprint status, Epic 4 source, PRD/architecture/UX discovery, root and sibling project-context facts, Story 4.4 implementation record, Contract Spine, contract notes, local source, focused tests, and recent git history.
- 2026-05-27: Latest external research was not added because Story 4.5 introduces no new external library/API version and should use repository-pinned project-local versions.
- 2026-05-27: Checklist validation applied during authoring: constrained scope to path policy before file mutation, added anti-reinvention notes for generated SDK/idempotency helpers, required authorization-before-observation, identified current runtime gaps, added path-specific leakage traps, and separated Story 4.5 from content transport/provider-write work in Stories 4.6 and 4.7.
- 2026-05-27: Ran `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`: passed, 0 warnings, 0 errors.
- 2026-05-27: Ran `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`: passed, 0 warnings, 0 errors.
- 2026-05-27: Ran new focused mutation tests with `Hexalith.Folders.Tests -parallel none -noLogo -class ...WorkspacePathPolicyValidatorTests -class ...FolderWorkspaceFileMutationAggregateTests -class ...FolderWorkspaceFileMutationServiceTests`: 40 passed.
- 2026-05-27: Ran focused server endpoint/action-token tests with `Hexalith.Folders.Server.Tests -parallel none -noLogo -class Hexalith.Folders.Server.Tests.WorkspaceLockEndpointTests -class Hexalith.Folders.Server.Tests.FolderCommandActionTokenMapperTests`: 45 passed.
- 2026-05-27: Ran `Hexalith.Folders.Tests -parallel none -noLogo -namespace Hexalith.Folders.Tests.Aggregates.Folder`: 489 passed.
- 2026-05-27: Ran `Hexalith.Folders.Tests -parallel none -noLogo -namespace Hexalith.Folders.Tests.Authorization`: 76 passed.
- 2026-05-27: Ran `Hexalith.Folders.Contracts.Tests -parallel none -noLogo -class Hexalith.Folders.Contracts.Tests.OpenApi.FileContextContractGroupTests`: 5 passed.
- 2026-05-27: Ran full `Hexalith.Folders.Tests -parallel none -noLogo`: 978 passed.
- 2026-05-27: Ran `git diff --check`: passed.
- 2026-05-27: Broad full `Hexalith.Folders.Server.Tests` remains blocked in this sandbox by known Kestrel socket permission failures from prior stories; focused in-process server tests passed. Broad full `Hexalith.Folders.Contracts.Tests` still has unrelated parity-oracle drift outside Story 4.5; the file-context contract group passed.
- 2026-05-27: Senior review loaded story, project context, Contract Spine references, C6/D-9/idempotency docs, and performed MCP resource discovery; no MCP resources were configured, so review used project-local docs and source.
- 2026-05-27: Senior review auto-fixed file-mutation service ordering so filesystem-style path evidence runs only after aggregate workspace/lock validation and equivalent replays return before duplicate evidence work.
- 2026-05-27: Senior review auto-fixed REST path-metadata rejection mapping so path validation denials return `path_validation_failed` without unsafe path echo.
- 2026-05-27: Senior review reran `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`: passed, 0 warnings, 0 errors.
- 2026-05-27: Senior review reran `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`: passed, 0 warnings, 0 errors.
- 2026-05-27: Senior review reran focused mutation tests with `Hexalith.Folders.Tests -parallel none -noLogo -class ...WorkspacePathPolicyValidatorTests -class ...FolderWorkspaceFileMutationAggregateTests -class ...FolderWorkspaceFileMutationServiceTests`: 41 passed.
- 2026-05-27: Senior review reran focused server endpoint/action-token tests with `Hexalith.Folders.Server.Tests -parallel none -noLogo -class Hexalith.Folders.Server.Tests.WorkspaceLockEndpointTests -class Hexalith.Folders.Server.Tests.FolderCommandActionTokenMapperTests`: 49 passed.
- 2026-05-27: Senior review reran `Hexalith.Folders.Contracts.Tests -parallel none -noLogo -class Hexalith.Folders.Contracts.Tests.OpenApi.FileContextContractGroupTests`: 5 passed.
- 2026-05-27: Senior review reran `git diff --check`: passed.

### Completion Notes List

- Added deterministic path metadata validation and internal path policy decision vocabulary with sanitized denial reasons for traversal, absolute/rooted paths, reserved names, control/invisible characters, NFC ambiguity, encoded traversal, trailing ambiguity, case collision, symlink escape, and policy evidence unavailability.
- Added metadata-only file mutation command/request/event handling that requires authorization, valid command metadata, current repository-backed workspace, held unexpired task lock, path policy acceptance, and C6 `FileMutated` transition before append.
- Added `WorkspaceFileMutationService`, a minimal injected path-policy evidence port, REST mutation intake routes, EventStore processor dispatch, and `mutate_files` action-token mapping.
- Added focused validator, service, aggregate, endpoint, and mapper coverage for safe ordering, idempotency replay/conflict, route-authoritative workspace handling, sanitized denials, and mutation-state behavior.
- Senior review corrected the service evidence-ordering gate and REST path-validation problem category without adding provider/Git/filesystem mutation behavior.
- No generated client files, provider/Git/filesystem mutation logic, streamed content processing, CLI, MCP, UI, or workers were changed.

### Change Log

- Implemented Story 4.5 path-policy gate and metadata-only file mutation acceptance path across domain, service, runtime dispatch, and REST intake.
- Added safe failure mappings for `path_policy_denied` and `path_validation_failed` without raw unsafe path echoing.
- Updated tests and regression gates for the new mutation intake behavior, preserving generated Contract Spine artifacts.
- Senior review fixed two verified issues: path evidence no longer runs before held-lock/workspace-state validation or equivalent replay detection, and REST path-metadata denials now use the canonical `path_validation_failed` category.

### Senior Developer Review (AI)

Reviewer: GPT-5 Codex on 2026-05-27

Outcome: Approved after auto-fix. Status set to `done`; sprint status synced to `done`.

Evidence loaded:

- Story file, Dev Agent File List, git status/diff, `_bmad-output/project-context.md`, local planning artifacts, Contract Spine references, file-context contract docs, idempotency/parity docs, changed source/test files, and focused test outputs.
- MCP resource discovery was performed; no MCP resources were available in this session, so no external/web lookup was needed.

Git vs story observations:

- Story File List covered the application source and tests changed for Story 4.5.
- Extra working-tree changes existed outside the source review scope: `Hexalith.Builds`, `_bmad-output/implementation-artifacts/tests/test-summary.md`, and `_bmad-output/story-automator/orchestration-3-20260526-203745.md`. They were not reviewed as Story 4.5 application source.

Issues found and fixed:

- HIGH: `WorkspaceFileMutationService` requested path-policy evidence before loading aggregate state and proving current workspace/held lock. Because the evidence port represents symlink/case-collision checks that can be filesystem-backed, this violated the story ordering requirement for held-lock/workspace-state checks before filesystem-style observation. Fixed by moving evidence lookup after aggregate validation and before idempotency lookup/append. Added service assertions that aggregate rejections and equivalent replays do not invoke evidence.
- HIGH: Equivalent replay still invoked path-policy evidence before the aggregate idempotency replay path could return, so a replay could perform duplicate downstream evidence work or fail differently if evidence became unavailable. Fixed by the same ordering change and a focused replay regression test.
- MEDIUM: REST path-metadata denials returned generic `validation_error` instead of the contract-compatible path category required by AC3. Fixed by classifying path metadata validation failures as `path_validation_failed` without echoing the unsafe raw path.

Validation:

- `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`: passed.
- `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`: passed.
- `tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -parallel none -noLogo -class Hexalith.Folders.Tests.Aggregates.Folder.WorkspacePathPolicyValidatorTests -class Hexalith.Folders.Tests.Aggregates.Folder.FolderWorkspaceFileMutationAggregateTests -class Hexalith.Folders.Tests.Aggregates.Folder.FolderWorkspaceFileMutationServiceTests`: 41 passed.
- `tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests -parallel none -noLogo -class Hexalith.Folders.Server.Tests.WorkspaceLockEndpointTests -class Hexalith.Folders.Server.Tests.FolderCommandActionTokenMapperTests`: 49 passed.
- `tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -parallel none -noLogo -class Hexalith.Folders.Contracts.Tests.OpenApi.FileContextContractGroupTests`: 5 passed.
- `git diff --check`: passed.

### File List

- `_bmad-output/implementation-artifacts/4-5-enforce-workspace-path-policy-before-file-mutations.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderResult.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderResultCode.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs`
- `src/Hexalith.Folders/Aggregates/Folder/IWorkspacePathPolicyEvidenceProvider.cs`
- `src/Hexalith.Folders/Aggregates/Folder/MutateWorkspaceFile.cs`
- `src/Hexalith.Folders/Aggregates/Folder/PathMetadata.cs`
- `src/Hexalith.Folders/Aggregates/Folder/UnavailableWorkspacePathPolicyEvidenceProvider.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceFileMutationAccepted.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceFileMutationRequest.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceFileMutationService.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspacePathPolicyDecision.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspacePathPolicyEvidenceDecision.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspacePathPolicyEvidenceRequest.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspacePathPolicyEvidenceResult.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspacePathPolicyResult.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspacePathPolicyValidator.cs`
- `src/Hexalith.Folders.Server/Authorization/FolderCommandActionTokenMapper.cs`
- `src/Hexalith.Folders.Server/FolderCommandRejected.cs`
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs`
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`
- `src/Hexalith.Folders.Server/FoldersServerModule.cs`
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderWorkspaceFileMutationAggregateTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderWorkspaceFileMutationServiceTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/WorkspacePathPolicyValidatorTests.cs`
- `tests/Hexalith.Folders.Server.Tests/FolderCommandActionTokenMapperTests.cs`
- `tests/Hexalith.Folders.Server.Tests/WorkspaceLockEndpointTests.cs`
