---
baseline_commit: 6dacc6b
---

# Story 4.7: Remove files with metadata-only events and provider-safe ordering

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer or AI agent holding the workspace lock,
I want to remove files through the same policy pipeline as writes,
so that deletes are auditable, idempotent, and cannot bypass workspace or tenant boundaries.

## Acceptance Criteria

1. Given an authorized caller submits `RemoveFile` with valid `Idempotency-Key`, `X-Correlation-Id`, `X-Hexalith-Task-Id`, route-authoritative `folderId`/`workspaceId`, `fileOperationKind == "remove"`, `transportOperation == "metadataOnlyRemoval"`, path metadata accepted by Story 4.5, and an owned unexpired workspace lock, when remove-file intake runs, then authorization, lock/workspace validation, idempotency replay/conflict detection, path policy, and delete-order validation complete before any provider, Git, working-copy, or durable mutation side effect.
2. Given a remove request carries add/change-only payload fields (`contentHashReference`, `byteLength`, `mediaType`, `transportEvidenceKind`, `observedByteLength`) or a non-`metadataOnlyRemoval` transport operation, when validation runs, then the command is **rejected** with canonical `validation_failed` (fail-closed — fields are NOT silently stripped) before EventStore command submission, and file contents, previous content samples, diffs, provider payloads, and caller local paths never appear in events, results, Problem Details, logs, traces, metrics, audit records, docs examples, or test diagnostics.
3. Given the path policy and path evidence checks pass for a remove request, when delete work is accepted, then the delete operation is ordered with the same tenant/folder/workspace/task mutation sequence as add/change work and cannot overtake, duplicate, or reorder relative to prior accepted task mutations.
4. Given a delete operation is accepted, when metadata-only mutation evidence is recorded, then `WorkspaceFileMutationAccepted` or its replacement records operation ID, file operation kind `remove`, transport operation `metadataOnlyRemoval`, path metadata digest/reference, path policy class, workspace, task, correlation, and idempotency metadata without content hash, byte length, media type, transport evidence, raw path, removed file content, diff, provider payload, local filesystem path, repository URL, credential, token, email, or unauthorized-resource existence hints.
5. Given the same idempotency key is replayed with an equivalent remove payload, including the same operation ID, path metadata, path policy class, task ID, and workspace ID, when the request is observed again, then the same logical result is returned without duplicate delete work, duplicate working-copy mutation, duplicate events, or duplicate downstream work.
6. Given the same idempotency key is reused with a non-equivalent remove payload, or the request has invalid schema version, malformed identifiers, route/body workspace mismatch, content fields, add/change transports, missing/expired/non-owned lock, archived folder, non-repository-backed folder, stale/inaccessible/reconciliation state, provider-side ordering evidence unavailable, or denied path policy, when the request is evaluated, then it returns the canonical safe result/problem category and emits no state-changing `FileMutated` event.
7. Given delete work is materialized or queued for a prepared workspace, when the operation succeeds, then the side-effect boundary is tenant/folder/workspace/task scoped, uses configured workspace/work roots, never relies on caller-supplied local paths, and leaves commit/provider push behavior to Story 4.12.
8. Given Story 4.7 is implemented, when existing folder creation, repository binding, branch/ref policy, provider readiness, workspace preparation, lock acquisition/release, Story 4.5 path policy, Story 4.6 add/change content transport, C6 replay, authorization, safe denial, Contract Spine, generated client helpers, and focused tests run, then they continue to pass without manually editing generated client files.

## Tasks / Subtasks

- [x] Preserve the existing RemoveFile contract shape. (AC: 1, 2, 8)
  - [x] Keep route `POST /api/v1/folders/{folderId}/workspaces/{workspaceId}/files/remove` and operation ID `RemoveFile`.
  - [x] Keep `FileMutationRequest` remove branch as `fileOperationKind: remove` plus `transportOperation: metadataOnlyRemoval`.
  - [x] Preserve the current RemoveFile idempotency equivalence fields: `file_operation_kind`, `operation_id`, `path_metadata`, `path_policy_class`, `task_id`, and `workspace_id`.
  - [x] Do not add content hash, byte length, media type, inline content, or stream descriptor requirements to RemoveFile.
  - [x] Change the OpenAPI spine, generated client, parity rows, and contract tests only if implementation discovers a real contract/runtime drift that must be corrected together.

- [x] Harden REST remove intake and metadata stripping. (AC: 1, 2, 6, 8)
  - [x] Reuse `FoldersDomainServiceEndpoints.FileMutationAsync`; do not create a parallel remove endpoint pipeline.
  - [x] Keep route `workspaceId` authoritative. Request body metadata must never override the route value.
  - [x] Ensure remove rejects any add/change payload fields before gateway submission: `contentHashReference`, `byteLength`, `inlineContent`, `streamDescriptor`, and any newly surfaced transport evidence fields.
  - [x] Assert gateway payload for remove omits content fields rather than serializing them as unsafe null-bearing payload data when repository conventions allow omission.
  - [x] Normalize endpoint failures to safe categories such as `validation_error`, `path_validation_failed`, `workspace_transition_invalid`, `lock_expired`, `lock_not_owned`, `idempotency_conflict`, `file_operation_failed`, `unknown_provider_outcome`, or `reconciliation_required`.

- [x] Add provider-safe delete ordering at the service boundary. (AC: 1, 3, 5, 6, 7)
  - [x] Reuse `WorkspaceFileMutationService` as the single file mutation orchestration point.
  - [x] Introduce the smallest delete-aware side-effect boundary that fits the existing pattern, such as extending the content/operation staging abstraction or adding a sibling workspace mutation operation store.
  - [x] The delete ordering boundary must be tenant/folder/workspace/task/operation scoped and must not key by raw path, local path, provider path, or global content hash.
  - [x] Execute delete ordering only after authorization, aggregate workspace/lock/idempotency precheck, syntactic path policy, path evidence, and repository idempotency lookup pass.
  - [x] Equivalent replay must return before duplicate path evidence, duplicate delete-order work, or duplicate append.
  - [x] Idempotency conflict, path denial, lock denial, workspace mismatch, unavailable delete-order evidence, and unavailable side-effect storage must not enqueue/materialize delete work and must not append a mutation event.

- [x] Keep aggregate behavior pure and metadata-only. (AC: 1, 3, 4, 5, 6, 8)
  - [x] Preserve `FolderAggregate.Handle(MutateWorkspaceFile, ...)` as a deterministic pure function with no filesystem, Git, provider, Dapr, logging, content lookup, delete execution, clock read, or service resolution.
  - [x] Preserve C6 `locked -> changes_staged` for first `FileMutated` and `changes_staged -> changes_staged` for additional `FileMutated`.
  - [x] Preserve `FolderCommandValidator` remove validation: `metadataOnlyRemoval`, no content hash, no byte length, and no add/change transport.
  - [x] Extend metadata-only event evidence only if needed for delete ordering, and never add raw path, content, diff, provider payload, local path, or previous-file evidence.
  - [x] Ensure `FolderStateApply` replay remains deterministic and duplicate delivery remains idempotent.

- [x] Wire domain processor and DI safely. (AC: 1, 3, 6, 7, 8)
  - [x] Keep `FoldersServerModule.MutateFilesCommandType` and `FolderDomainProcessor` dispatch unless the Contract Spine intentionally changes.
  - [x] Ensure `FolderDomainProcessor` reconstructs remove requests without inventing content metadata or accepting client-controlled tenant/principal authority.
  - [x] Register any new delete-order boundary with a fail-closed default implementation. A missing real implementation must return `file_operation_failed` or the closest existing canonical safe category, not pretend delete ordering succeeded.
  - [x] Do not let the default fail-closed implementation break add/change Story 4.6 behavior when a test explicitly supplies the recording content/operation store.

- [x] Add focused remove-file tests. (AC: 1-8)
  - [x] Add aggregate tests proving remove accepts `metadataOnlyRemoval`, records metadata-only evidence, leaves content fields null/absent, transitions `locked -> changes_staged`, and preserves additional mutation behavior.
  - [x] Add aggregate and validator tests rejecting remove with `contentHashReference`, `byteLength`, media/transport evidence, add/change transport, invalid schema, wrong workspace, wrong task, expired lock, and non-mutable states.
  - [x] Add service tests proving delete-order work happens only after authorization, aggregate precheck, path policy evidence, and idempotency lookup; equivalent replay and conflicts must not repeat delete-order work.
  - [x] Add service tests for unavailable delete-order boundary, path evidence denial, lock-not-owned, wrong workspace, and wrong tenant where no append and no delete work occurs.
  - [x] Add endpoint tests for valid remove payload, route-authoritative workspace, rejection of content fields, rejection of inline/stream descriptors, safe Problem Details, safe gateway reason mapping, and no unsafe payload echo.
  - [x] Add metadata-only leakage tests using `tests/fixtures/audit-leakage-corpus.json` across events, results, Problem Details, logs/test diagnostics, and docs examples touched by this story.
  - [x] Update `FileContextContractGroupTests` only if the Contract Spine changes; otherwise keep it green as the guard against RemoveFile drift.

- [x] Preserve focused regression gates. (AC: 8)
  - [x] `dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore --tlp:off -v:minimal`
  - [x] `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - [x] `tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -parallel none -noLogo -class Hexalith.Folders.Tests.Aggregates.Folder.FolderWorkspaceFileMutationAggregateTests -class Hexalith.Folders.Tests.Aggregates.Folder.FolderWorkspaceFileMutationServiceTests -class Hexalith.Folders.Tests.Aggregates.Folder.WorkspacePathPolicyValidatorTests`
  - [x] `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - [x] `tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests -parallel none -noLogo -class Hexalith.Folders.Server.Tests.WorkspaceLockEndpointTests`
  - [x] `dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - [x] `tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -parallel none -noLogo -class Hexalith.Folders.Contracts.Tests.OpenApi.FileContextContractGroupTests`
  - [x] `git diff --check`

## Dev Notes

### Source Context

- Epic 4 owns the repository-backed workspace task lifecycle: prepare, lock, mutate files safely, query bounded context, commit, and expose deterministic failure/status/idempotency/redaction behavior. [Source: `_bmad-output/planning-artifacts/epics.md#Epic 4: Repository-Backed Workspace Task Lifecycle`]
- Story 4.7 requires remove operations to run through the same policy pipeline as writes, order provider-safe delete work with task changes, and keep emitted events metadata-only and idempotent. [Source: `_bmad-output/planning-artifacts/epics.md#Story 4.7: Remove files with metadata-only events and provider-safe ordering`]
- PRD FR32-FR33 require authorized add/change/remove operations only inside prepared and locked workspaces, with rejection for workspace boundary, path, branch/ref, lock, tenant, provider, or folder policy violations. [Source: `_bmad-output/planning-artifacts/prd.md#Functional Requirements`]
- PRD FR38-FR42 require task, operation, correlation, changed-path metadata, stable failed/duplicate/retried/conflicting operation evidence, idempotent retries, and duplicate intent rejection. [Source: `_bmad-output/planning-artifacts/prd.md#Functional Requirements`]
- PRD FR44-FR46 require canonical error taxonomy, canonical workspace/task states, final-state explanation, retry eligibility, and operational evidence after file-operation failure. [Source: `_bmad-output/planning-artifacts/prd.md#Functional Requirements`]
- PRD NFR14 requires idempotency keys for file mutation, NFR17 requires deterministic tenant-scoped locking, and NFR25 allows longer provider/workspace work to continue asynchronously with operation identity and status visibility. [Source: `_bmad-output/planning-artifacts/prd.md#Non-Functional Requirements`]
- Architecture C6 allows `locked -> changes_staged` on first `FileMutated` and `changes_staged -> changes_staged` on additional `FileMutated`; unlisted state/event pairs reject with `state_transition_invalid`. [Source: `_bmad-output/planning-artifacts/architecture.md#Workspace State Transition Matrix (C6 -- Enumerated)`]
- Architecture process patterns require aggregate purity, idempotency equivalence via generated helpers, held lock plus auth revalidation on every mutation, unknown provider outcomes entering reconciliation, and sentinel-redaction tests for every output channel touched. [Source: `_bmad-output/planning-artifacts/architecture.md#Process Patterns`]
- Architecture structure places file commands in `src/Hexalith.Folders/Aggregates/Folder/`, REST file endpoints under `Hexalith.Folders.Server`, and workers/process managers as the owners of Git/working-copy/provider side effects. [Source: `_bmad-output/planning-artifacts/architecture.md#Requirements to Structure Mapping`]
- The current OpenAPI spine already declares `RemoveFile`, `metadataOnlyRemoval`, and a RemoveFile branch that forbids inline content, stream descriptor, content hash reference, and byte length. [Source: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#/paths/~1api~1v1~1folders~1{folderId}~1workspaces~1{workspaceId}~1files~1remove`; `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#/components/schemas/FileMutationRequest`]
- Contract notes state RemoveFile equivalence uses file operation kind, operation ID, path metadata, path policy class, task ID, and workspace ID; removed file contents never appear and path metadata is the only file evidence. [Source: `docs/contract/idempotency-and-parity-rules.md#Operation-Level Notes`]

### Previous Story Intelligence

- Story 4.6 implemented add/change content transport validation and explicitly preserved RemoveFile behavior for Story 4.7. [Source: `_bmad-output/implementation-artifacts/4-6-add-and-change-files-with-inline-and-streamed-content-transport.md#Do Not Touch`]
- Story 4.6 established the current ordering discipline: authorization first; syntactic path policy before expensive observation where possible; aggregate workspace/lock/idempotency precheck before path evidence or side effects; repository idempotency lookup before content staging; equivalent replay before duplicate evidence/content work. Preserve this ordering for delete work.
- Story 4.6 senior review fixed descriptor-only stream acceptance and added a pattern for fail-closed side-effect evidence. Apply the same stance to delete ordering: metadata-only request acceptance is not enough if required delete ordering evidence is unavailable.
- Story 4.5 implemented the shared file mutation intake path, including `MutateWorkspaceFile`, `WorkspaceFileMutationRequest`, `WorkspaceFileMutationAccepted`, `WorkspaceFileMutationService`, Add/Change/Remove REST routes, path policy validator, path evidence port, EventStore domain processor dispatch, and focused tests.
- Known environmental constraints from prior stories: broad solution/AppHost builds can try blocked NuGet/network access, full route tests can hit sandbox Kestrel socket-binding failures, and broad contract tests may surface unrelated drift. Prefer the focused build/test commands listed in this story.

### Existing Implementation State

- `FoldersDomainServiceEndpoints` already registers `/files/remove`, validates `remove` plus `metadataOnlyRemoval`, rejects inline/stream/content hash/byte length for remove, and sends metadata-only gateway payload to `MutateFiles`.
- `FolderDomainProcessor` reconstructs `WorkspaceFileMutationRequest` from metadata-only payload and routes it through `WorkspaceFileMutationService`; it must continue to derive tenant/principal authority from EventStore envelope/claim evidence, not payload fields.
- `WorkspaceFileMutationService` is the single mutation orchestration point. It authorizes with action token `mutate_files`, validates command/path metadata, loads aggregate state, runs aggregate precheck, asks `IWorkspacePathPolicyEvidenceProvider`, checks repository idempotency, stages add/change content through `IWorkspaceFileContentStore`, and appends metadata-only events.
- Current `WorkspaceFileMutationService` intentionally stages content only for `add` and `change`. `remove` can currently append metadata-only mutation events without a delete-specific ordering/materialization boundary. Story 4.7 should close that gap.
- `IWorkspaceFileContentStore` and `UnavailableWorkspaceFileContentStore` are add/change-oriented: their request requires content hash, byte length, media type, and transport evidence. Do not force those fields onto remove. Add a delete-aware operation boundary or refactor carefully if extending this abstraction.
- `FolderAggregate.Handle(MutateWorkspaceFile, ...)` already checks folder existence, active mutation guard, idempotency, repository binding, exact workspace ID, mutable workspace state, lock ownership, lock expiry, C6 transition, and path policy before emitting `WorkspaceFileMutationAccepted`.
- `FolderCommandValidator` already treats `remove` as valid only when `TransportOperation == "metadataOnlyRemoval"` and `ContentHashReference`/`ByteLength` are null. It excludes content hash from the RemoveFile fingerprint.
- `FolderStateApply.ApplyWorkspaceFileMutationAccepted` records idempotency and applies the C6 `FileMutated` transition without storing file content or raw path details.
- Tests already include a minimal aggregate acceptance case for remove and endpoint coverage for supported RemoveFile route payloads. They do not yet prove delete-order side-effect behavior, replay-before-delete-order behavior, or fail-closed unavailable delete-order evidence.

### Architecture Guardrails

- Repository configuration is authoritative when planning artifacts drift. Current pins include .NET SDK `10.0.302`, Dapr packages `1.17.7`, Aspire package family `13.x`, NSwag.MSBuild `14.7.1`, Newtonsoft.Json `13.0.4`, xUnit v3 `3.2.2`, Shouldly `4.3.0`, YamlDotNet `17.1.0`, and Playwright `1.59.0`. [Source: `global.json`; `Directory.Packages.props`; `_bmad-output/project-context.md#Technology Stack & Versions`]
- Use .NET 10, nullable-aware C#, file-scoped namespaces, one primary public type per file, PascalCase public members, camelCase locals/parameters, central package versions, xUnit v3, and Shouldly. [Source: `_bmad-output/project-context.md#Technology Stack & Versions`; `_bmad-output/project-context.md#Testing Rules`]
- Aggregate handlers must stay deterministic and side-effect free. Pass timestamps from callers; never perform filesystem, provider, Git, Dapr, logging, service resolution, or clock reads inside aggregate transitions. [Source: `_bmad-output/project-context.md#Language-Specific Rules`]
- Workers own external provider, Git, working-copy, reconciliation, and rate-limit side effects. Aggregates, REST handlers, CLI, MCP, and UI should not perform provider or filesystem mutations directly. [Source: `_bmad-output/project-context.md#Framework-Specific Rules`]
- Authorization order is contractual: JWT validation, EventStore claim transform, tenant-access freshness, folder ACL, EventStore validator, then Dapr deny-by-default policy. Do not reorder or skip layers for file mutations. [Source: `_bmad-output/project-context.md#Critical Don't-Miss Rules`]
- Metadata-only is mandatory across events, projections, logs, traces, metrics, audit records, Problem Details, console responses, provider diagnostics, generated artifacts, docs examples, and test failure messages. [Source: `_bmad-output/project-context.md#Critical Don't-Miss Rules`]
- EventStore owns envelope metadata and domain rejections are normal events/results, not exceptions. Extension metadata must be sanitized at the API boundary before entering the processing pipeline. [Source: `Hexalith.EventStore/_bmad-output/project-context.md#Critical Don't-Miss Rules`]
- Tenants remains the membership source of truth. Tenant authority comes from authenticated context and EventStore claim-transform evidence, not client-controlled payload or headers. [Source: `Hexalith.Tenants/_bmad-output/project-context.md#Authorization (RBAC -- Role-Based Access Control)`]
- Do not initialize or update nested submodules recursively. [Source: `AGENTS.md`; `_bmad-output/project-context.md#Development Workflow Rules`]

### Files To Touch

- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceFileMutationService.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceFileMutationRequest.cs` only if delete-order evidence needs safe request metadata.
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceFileMutationAccepted.cs` only if delete-order evidence needs a metadata-only event field.
- `src/Hexalith.Folders/Aggregates/Folder/MutateWorkspaceFile.cs` only if delete-order evidence needs safe command metadata.
- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs`
- New or changed delete-aware side-effect boundary under `src/Hexalith.Folders/Aggregates/Folder/`, reusing the existing content/operation staging pattern where practical.
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs`
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs` or `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs` for DI registration, depending on where the boundary belongs.
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` only if runtime/contract drift is discovered and fixed in the same change.
- `src/Hexalith.Folders.Client/Generation/` and generated client files only if the Contract Spine changes and generation is rerun.
- Focused tests under `tests/Hexalith.Folders.Tests/Aggregates/Folder/`, `tests/Hexalith.Folders.Server.Tests/`, and `tests/Hexalith.Folders.Contracts.Tests/OpenApi/`.

### Do Not Touch

- Do not implement commit execution, provider push, unknown-outcome commit reconciliation, context query execution, cleanup repair automation, CLI commands, MCP tools, UI pages, or operations console behavior.
- Do not make RemoveFile require `contentHashReference`, `byteLength`, `mediaType`, `inlineContent`, `streamDescriptor`, content bytes, previous-file hash, or provider payload.
- Do not hand-edit `src/Hexalith.Folders.Client/Generated/*`.
- Do not store raw removed file contents, base64 content, diffs, local filesystem paths, display names, provider payloads, repository URLs, credential material, tokens, emails, raw claim bags, or unauthorized resource existence in events, audit, logs, traces, metrics, Problem Details, test diagnostics, or docs examples.
- Do not trust `PathMetadata.pathPolicyClass`, normalized paths, operation IDs, task IDs, workspace IDs, or client tenant/principal values as authority without server-side validation and EventStore/authorization evidence.
- Do not use live GitHub, Forgejo, Aspire, Dapr sidecars, Docker, Keycloak, Redis, network-dependent tests, or nested-submodule initialization for focused Story 4.7 validation.
- Do not loosen path metadata Unicode rules or invent a closed `pathPolicyClass` enum in this story; those are outside the delete-order gap.
- Do not add new lifecycle states unless the C6 matrix, OpenAPI spine, parity oracle, generated client, state transition code, and transition tests are updated together.

### Testing

- Recommended focused validation:
  - `dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore --tlp:off -v:minimal`
  - `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - `tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -parallel none -noLogo -class Hexalith.Folders.Tests.Aggregates.Folder.FolderWorkspaceFileMutationAggregateTests -class Hexalith.Folders.Tests.Aggregates.Folder.FolderWorkspaceFileMutationServiceTests -class Hexalith.Folders.Tests.Aggregates.Folder.WorkspacePathPolicyValidatorTests`
  - `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - `tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests -parallel none -noLogo -class Hexalith.Folders.Server.Tests.WorkspaceLockEndpointTests`
  - `dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - `tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -parallel none -noLogo -class Hexalith.Folders.Contracts.Tests.OpenApi.FileContextContractGroupTests`
  - `git diff --check`

### Regression Traps

- Do not mistake current RemoveFile route acceptance for full Story 4.7 completion. The missing behavior is delete-specific ordering/materialization evidence comparable to Story 4.6 add/change staging.
- Equivalent replay must not repeat delete-order side effects. Story 4.5 and 4.6 explicitly hardened replay-before-duplicate-evidence/staging ordering; preserve that fix.
- Remove must remain content-free. Adding content hash, byte length, media type, inline content, or stream descriptors to remove is a contract regression and will break generated helper assumptions.
- Gateway and EventStore payloads must omit raw request shapes for remove. Do not serialize `inlineContent`, `streamDescriptor`, raw path values beyond validated path metadata, or caller local paths.
- Delete work must be scoped by tenant/folder/workspace/task/operation. A global operation ID, path-only key, or hash-only key risks cross-tenant collision or leakage.
- A fail-open delete-order boundary is worse than no boundary. If delete ordering cannot be proven, reject safely before append.
- Existing lock release must continue to reject `changes_staged`; remove creates staged changes, but clean release still requires commit or later recovery behavior.
- Do not let generated NSwag enum quirks drive runtime safety. Current generated helpers synthesize RemoveFile behavior around NSwag output; runtime validation must enforce real remove semantics.
- Do not broaden tests into live provider/Dapr/Aspire lanes unless this story deliberately changes those surfaces.

### Latest Technical Context

- No new external library or live provider API is required for Story 4.7. Use repository-pinned versions in `global.json` and `Directory.Packages.props`.
- External network research was not needed because this story is governed by project-local Contract Spine, C6 architecture, Story 4.5/4.6 implementation patterns, EventStore/Tenants project-context rules, generated client behavior, and local source.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 4.7: Remove files with metadata-only events and provider-safe ordering`
- `_bmad-output/planning-artifacts/epics.md#Epic 4: Repository-Backed Workspace Task Lifecycle`
- `_bmad-output/planning-artifacts/prd.md#Functional Requirements`
- `_bmad-output/planning-artifacts/prd.md#Non-Functional Requirements`
- `_bmad-output/planning-artifacts/architecture.md#Workspace State Transition Matrix (C6 -- Enumerated)`
- `_bmad-output/planning-artifacts/architecture.md#Process Patterns`
- `_bmad-output/project-context.md`
- `Hexalith.EventStore/_bmad-output/project-context.md`
- `Hexalith.Tenants/_bmad-output/project-context.md`
- `_bmad-output/implementation-artifacts/4-5-enforce-workspace-path-policy-before-file-mutations.md`
- `_bmad-output/implementation-artifacts/4-6-add-and-change-files-with-inline-and-streamed-content-transport.md`
- `docs/contract/file-context-contract-groups.md`
- `docs/contract/idempotency-and-parity-rules.md#Operation-Level Notes`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#FileMutationRequest`
- `src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs#FileMutationRequest`
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceFileMutationService.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderWorkspaceFileMutationAggregateTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderWorkspaceFileMutationServiceTests.cs`
- `tests/Hexalith.Folders.Server.Tests/WorkspaceLockEndpointTests.cs`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/FileContextContractGroupTests.cs`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-27: Story created via `bmad-create-story` workflow using explicit Story ID 4.7, sprint status, Epic 4 source, PRD/architecture/UX discovery, root and sibling project-context facts, Story 4.6 implementation record, Contract Spine, contract notes, generated client behavior, local source, focused tests, and recent git history.
- 2026-05-27: Discovery loaded whole planning documents: `epics.md`, `prd.md`, `architecture.md`, `ux-design-specification.md`; persistent facts loaded from root Folders project context plus EventStore/Tenants project contexts relevant to this backend story.
- 2026-05-27: Latest external research was not added because Story 4.7 introduces no new external library/API requirement and should use repository-pinned project-local versions.
- 2026-05-27: Checklist validation applied during authoring: identified current remove route/aggregate support, flagged missing delete-order side-effect boundary, preserved Story 4.5/4.6 replay and side-effect ordering, required metadata-only delete evidence, and constrained scope away from commit/provider/CLI/MCP/UI work.

### Completion Notes List

- Added a remove-specific delete operation staging boundary with a fail-closed default implementation.
- Kept remove mutations metadata-only while tightening validator rejection for add/change transport evidence fields.
- Ordered delete staging after authorization, aggregate precheck, path evidence, and idempotency lookup, and before event append.
- Added focused aggregate, service, and endpoint guardrail tests for metadata-only remove behavior, replay/conflict short-circuiting, and unsafe content payload rejection.
- Parent validation passed on 2026-05-27 with core/server/contracts builds, focused aggregate/service/path tests, endpoint tests, contract tests, and `git diff --check`.
- Senior review auto-fix added explicit remove operation kind to delete-order requests and strengthened remove-specific fail-closed/path-evidence tests.

### File List
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs`
- `src/Hexalith.Folders/Aggregates/Folder/IWorkspaceFileDeleteOperationStore.cs`
- `src/Hexalith.Folders/Aggregates/Folder/UnavailableWorkspaceFileDeleteOperationStore.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceFileDeleteOperationStoreRequest.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceFileDeleteOperationStoreResult.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceFileMutationService.cs`
- `tests/Hexalith.Folders.Server.Tests/WorkspaceLockEndpointTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderWorkspaceFileMutationAggregateTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderWorkspaceFileMutationServiceTests.cs`

### Senior Developer Review (AI)

Reviewer: GPT-5 Codex on 2026-05-27

Outcome: Approved after auto-fix. No critical issues remain.

Findings fixed:

- [Medium] Delete-order requests carried tenant/folder/workspace/task/operation and transport metadata but not explicit `fileOperationKind`, unlike the add/change staging request and Story 4.7 metadata requirements. Fixed by adding `FileOperationKind` to `WorkspaceFileDeleteOperationStoreRequest` and passing/asserting `remove`.
- [Medium] Remove-specific path evidence denial was not directly covered by the service tests. Added `RemovePathEvidenceDenialShouldNotOrderDeleteOrAppend`.
- [Medium] The fail-closed default delete-order boundary was registered and implemented but not directly covered by a service test. Added `RemoveDefaultDeleteOrderBoundaryShouldFailClosedBeforeAppend`.

Review notes:

- Story File List matches the source files changed for Story 4.7. Git also contains non-source automation/status artifacts and the `Hexalith.Builds` submodule marker, which are outside application source review scope.
- No MCP documentation resources were configured; review used project-local story, sprint status, project context, architecture/PRD references, Contract Spine, and source/test evidence.
- No separate epic tech spec artifact was found beyond the loaded PRD, architecture, epics, and project-context documents.

Validation:

- `dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore --tlp:off -v:minimal`
- `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
- `tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -parallel none -noLogo -class Hexalith.Folders.Tests.Aggregates.Folder.FolderWorkspaceFileMutationAggregateTests -class Hexalith.Folders.Tests.Aggregates.Folder.FolderWorkspaceFileMutationServiceTests -class Hexalith.Folders.Tests.Aggregates.Folder.WorkspacePathPolicyValidatorTests` - 60 passed
- `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
- `tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests -parallel none -noLogo -class Hexalith.Folders.Server.Tests.WorkspaceLockEndpointTests` - 59 passed
- `dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
- `tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -parallel none -noLogo -class Hexalith.Folders.Contracts.Tests.OpenApi.FileContextContractGroupTests` - 5 passed
- `git diff --check`

### Change Log

- 2026-05-27: Implemented Story 4.7 remove-file metadata-only delete ordering and focused regression coverage.
- 2026-05-27: Senior review auto-fixed delete-order request metadata and remove-specific service coverage; status set to done.
