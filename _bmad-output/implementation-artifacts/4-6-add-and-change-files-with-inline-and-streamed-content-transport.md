---
baseline_commit: fcf31ec
---

# Story 4.6: Add and change files with inline and streamed content transport

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer or AI agent holding the workspace lock,
I want to add or change files through bounded inline and streamed transports,
so that writes are deterministic, retry-safe, and aligned with D-9.

## Acceptance Criteria

1. Given an authorized caller submits `AddFile` or `ChangeFile` with valid `Idempotency-Key`, `X-Correlation-Id`, `X-Hexalith-Task-Id`, route-authoritative `folderId`/`workspaceId`, path metadata accepted by Story 4.5, and an owned unexpired workspace lock, when file mutation intake runs, then authorization, lock/workspace validation, idempotency replay/conflict detection, path policy, and D-9 transport validation complete before any provider, Git, working-copy, or durable mutation side effect.
2. Given `transportOperation == "PutFileInline"`, when inline content is submitted, then request parsing validates `inlineContent.mediaType`, base64 content shape, decoded byte length, `byteLength`, `contentHashReference`, `fileOperationKind` (`add` or `change` only), and the D-9 inline boundary of 262144 bytes; over-bound inline requests return safe `413` with `X-Hexalith-Retry-Transport: stream` and do not disclose file content or unsafe path details.
3. Given `transportOperation == "PutFileStream"`, when streamed content is submitted, then the request must carry actual stream evidence, not only a descriptor: declared length is at least 262145, the observed stream length matches `byteLength`/`declaredLength`, media type is valid, cancellation and incomplete streams fail safely, and content is processed with bounded buffering rather than loading unbounded large files into memory.
4. Given add/change content is accepted, when metadata-only mutation evidence is recorded, then `WorkspaceFileMutationAccepted` or its replacement records operation ID, file operation kind, transport operation, content hash reference, byte length, media type, path metadata digest/reference, path policy class, workspace, task, correlation, and idempotency metadata without file contents, diffs, local filesystem paths, provider payloads, stream bytes, base64 bodies, display names, raw unsafe paths, tokens, credentials, or emails.
5. Given the same idempotency key is replayed with an equivalent add/change payload, including the same content hash reference, operation kind, operation ID, path metadata, path policy class, task ID, workspace ID, byte length, media type, and transport intent, when the request is observed again, then the same logical result is returned without duplicate content staging, duplicate working-copy writes, duplicate events, or duplicate downstream work.
6. Given the same idempotency key is reused with a non-equivalent add/change payload, or the request has invalid schema version, malformed identifiers, missing required headers, route/body workspace mismatch, missing content hash, invalid media type, mismatched declared length, invalid base64, missing stream body, unsupported binary/media policy, missing/expired/non-owned lock, archived folder, non-repository-backed folder, stale/inaccessible/reconciliation state, or denied path policy, when the request is evaluated, then it returns the canonical safe result/problem category and emits no state-changing `FileMutated` event.
7. Given content bytes are staged or materialized for a prepared workspace, when the operation succeeds, then the side-effect boundary is tenant/folder/workspace/task scoped, uses the configured workspace/work root, never relies on caller-supplied local paths, and leaves later commit behavior to Story 4.12.
8. Given Story 4.6 is implemented, when existing folder creation, repository binding, branch/ref policy, provider readiness, workspace preparation, lock acquisition/release, Story 4.5 path policy, C6 replay, authorization, safe denial, Contract Spine, generated client helpers, and focused tests run, then they continue to pass without manually editing generated client files.

## Tasks / Subtasks

- [x] Reconcile D-9 transport contract and runtime behavior. (AC: 2, 3, 8)
  - [x] Treat the current OpenAPI Contract Spine as authoritative unless this story deliberately changes it; if true multipart streaming is introduced to match architecture D-9, update `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`, generated client artifacts, contract tests, parity rows, and endpoint tests together.
  - [x] Do not fake streamed writes from `streamDescriptor` alone. A `PutFileStream` request must have a real stream/body or a validated transient content-staging reference with observed length evidence.
  - [x] Preserve `AddFile`, `ChangeFile`, and `RemoveFile` route identities already present in the Contract Spine. This story implements add/change content transport only; delete ordering remains Story 4.7.
  - [x] Keep the response-side retry transport header name as `X-Hexalith-Retry-Transport`, not the request-side `X-Hexalith-Retry-As`.

- [x] Extend file mutation transport metadata through the server and domain pipeline. (AC: 1, 2, 3, 4, 6)
  - [x] Extend `FileMutationHttpRequest` handling in `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs` so `inlineContent` and `streamDescriptor` are validated, not ignored.
  - [x] Extract and validate media type from `inlineContent.mediaType` or `streamDescriptor.mediaType`; reject parameters such as `;charset=utf-8` unless the Contract Spine is intentionally changed.
  - [x] Validate inline base64 shape and decoded length against `byteLength`; reject invalid base64 with `validation_error` and over-bound inline content with safe `413` plus `X-Hexalith-Retry-Transport: stream`.
  - [x] Validate streamed declared length against `byteLength` and the observed stream length; treat stream interruption or cancellation as a safe non-accepted failure.
  - [x] Add media type and transport evidence to the gateway/domain payload without adding raw bytes, base64 content, local paths, or provider payloads to EventStore command payloads.

- [x] Add content transport/staging support without polluting events. (AC: 1, 3, 4, 5, 7)
  - [x] Add a small tenant-scoped content transport/staging abstraction, for example `IWorkspaceFileContentStore` or `IWorkspaceFileContentTransport`, near the existing file mutation service boundary.
  - [x] Store or expose only bounded content evidence keyed by tenant, folder, workspace, task, operation, and content hash reference; do not key by caller-provided local paths.
  - [x] Ensure transient staged content has bounded lifetime and cleanup behavior; never treat transient staged bytes as authoritative audit or repository truth.
  - [x] If the implementation materializes bytes to a working copy in this story, do it through an injected workspace writer/worker port. Aggregates must remain pure and must not perform filesystem, provider, Git, Dapr, logging, service resolution, or clock I/O.
  - [x] If the configured writer/evidence store is unavailable, fail safely before state-changing mutation acceptance and return the canonical safe category; do not silently accept metadata-only writes that cannot be materialized.

- [x] Extend domain command, validation, and event metadata. (AC: 1, 4, 5, 6, 8)
  - [x] Extend `MutateWorkspaceFile`, `WorkspaceFileMutationRequest`, and `WorkspaceFileMutationAccepted` with media type and any required transport evidence fields.
  - [x] Keep `FolderCommandValidator` idempotency equivalence aligned with the Contract Spine. Add byte length/media type to runtime conflict detection only if the spine/generator equivalence is updated in the same change.
  - [x] Preserve existing equivalence lists for AddFile/ChangeFile unless the contract changes: `content_hash_reference`, `file_operation_kind`, `operation_id`, `path_metadata`, `path_policy_class`, `task_id`, `workspace_id`.
  - [x] Preserve RemoveFile equivalence and behavior for Story 4.7; do not accidentally require content hash or byte length for remove.
  - [x] Keep `FolderAggregate.Handle(MutateWorkspaceFile, ...)` deterministic: validate state/lock/path/transport metadata and emit metadata-only evidence, but never read, decode, hash, stream, or write file content inside the aggregate.

- [x] Implement safe add/change content behavior at the service boundary. (AC: 1, 2, 3, 5, 6, 7)
  - [x] Reuse `WorkspaceFileMutationService` rather than creating a parallel mutation path.
  - [x] Preserve Story 4.5 ordering: authorization first; syntactic path policy before stream/resource observation where possible; aggregate workspace/lock/idempotency precheck before expensive path evidence or content side effects; equivalent replay before duplicate evidence/content work.
  - [x] Ensure `AddFile` and `ChangeFile` are distinct operation kinds but share common validated transport handling.
  - [x] Enforce current `locked -> changes_staged` and `changes_staged -> changes_staged` C6 behavior through `FileMutated`; do not add new lifecycle states unless the C6 matrix and transition tests are updated in the same PR.
  - [x] Do not implement commit, provider push, delete ordering, context query execution, cleanup repair, CLI commands, MCP tools, UI pages, or generated SDK convenience methods unless a contract/build break makes a narrow generated-artifact update unavoidable.

- [x] Wire runtime endpoints and domain processor safely. (AC: 1, 2, 3, 4, 6, 8)
  - [x] Keep existing routes: `POST /api/v1/folders/{folderId}/workspaces/{workspaceId}/files/add` and `PUT /api/v1/folders/{folderId}/workspaces/{workspaceId}/files/change`.
  - [x] Keep route `workspaceId` authoritative. Request-body metadata must never override the route value.
  - [x] Continue using `FoldersServerModule.MutateFilesCommandType` and `FolderDomainProcessor` dispatch unless the Contract Spine is intentionally split.
  - [x] Ensure `FolderDomainProcessor` carries media type and transport evidence through to `WorkspaceFileMutationService` while keeping content bytes out of EventStore envelope payloads, logs, rejection events, and Problem Details.
  - [x] Normalize transport failures to canonical categories: `validation_error`, `input_limit_exceeded`, `path_validation_failed`, `lock_expired`, `lock_not_owned`, `workspace_locked`, `state_transition_invalid`, `idempotency_conflict`, `file_operation_failed`, `unknown_provider_outcome`, or `reconciliation_required` as appropriate.

- [x] Add focused tests for D-9 add/change content transport. (AC: 1-8)
  - [x] Add endpoint tests for valid inline add at 0 bytes, valid inline add/change at 262144 bytes, over-bound inline returning 413 with `X-Hexalith-Retry-Transport: stream`, invalid base64, media type mismatch, missing inline content, stream descriptor without stream/body evidence, streamed change at 262145 bytes, declared-length mismatch, route/body workspace mismatch, and no raw content/path echo in Problem Details.
  - [x] Add service tests proving equivalent replay returns before duplicate content staging/writer work, idempotency conflict does not stage/write, lock-not-owned and wrong-workspace cases do not stage/write, path denial does not stage/write, and content store unavailability fails safely.
  - [x] Add aggregate tests proving accepted add/change events record media type and content metadata only, preserve `locked -> changes_staged`, preserve additional mutation behavior, and never serialize inline bytes or base64 content.
  - [x] Add metadata-only leakage tests using `tests/fixtures/audit-leakage-corpus.json` across events, results, Problem Details, logs/test diagnostics, and docs examples touched by this story.
  - [x] Update `FileContextContractGroupTests` only if the Contract Spine changes; otherwise keep them green as the guard against D-9 drift.

- [x] Preserve focused regression gates. (AC: 8)
  - [x] `dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore --tlp:off -v:minimal`
  - [x] `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - [x] `tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -parallel none -noLogo -class Hexalith.Folders.Tests.Aggregates.Folder.FolderWorkspaceFileMutationAggregateTests -class Hexalith.Folders.Tests.Aggregates.Folder.FolderWorkspaceFileMutationServiceTests -class Hexalith.Folders.Tests.Aggregates.Folder.WorkspacePathPolicyValidatorTests`
  - [x] `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - [x] Focused file mutation endpoint/action-token tests in `tests/Hexalith.Folders.Server.Tests/WorkspaceLockEndpointTests.cs` and `FolderCommandActionTokenMapperTests`.
  - [x] `tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -parallel none -noLogo -class Hexalith.Folders.Contracts.Tests.OpenApi.FileContextContractGroupTests`
  - [x] `git diff --check`

## Dev Notes

### Source Context

- Epic 4 owns the repository-backed workspace task lifecycle: prepare, lock, mutate files safely, query bounded context, commit, and expose deterministic failure/status/idempotency/redaction behavior. [Source: `_bmad-output/planning-artifacts/epics.md#Epic 4: Repository-Backed Workspace Task Lifecycle`]
- Story 4.6 requires add/change through bounded inline and streamed transports, with size, binary, and media limits enforced before provider writes and events recording content hash, byte length, media type, task, operation, and correlation metadata without file contents. [Source: `_bmad-output/planning-artifacts/epics.md#Story 4.6: Add and change files with inline and streamed content transport`]
- PRD FR32-FR33 require authorized file add/change/remove operations in prepared and locked workspaces, with rejection for workspace boundary, path, branch/ref, lock, tenant, provider, or folder policy violations. [Source: `_bmad-output/planning-artifacts/prd.md#Functional Requirements`]
- PRD NFR14 requires idempotency keys for file mutation, and NFR21/NFR25 require accepted lifecycle commands to acknowledge quickly while longer provider/workspace work remains inspectable. [Source: `_bmad-output/planning-artifacts/prd.md#Non-Functional Requirements`]
- Architecture D-9 resolves C11 as a bimodal file-content transport: inline for content at or below 256KB, streamed transport for larger content, with SDK convenience behavior layered on top. [Source: `_bmad-output/planning-artifacts/architecture.md#Decision Log`]
- Architecture JSON/event rules require bytes/blobs never to be inline in event payloads; content is referenced by `contentHash`, `byteLength`, and `mediaType`. [Source: `_bmad-output/planning-artifacts/architecture.md#Format Patterns`]
- C6 allows `locked -> changes_staged` on first `FileMutated` and `changes_staged -> changes_staged` on additional `FileMutated`; unlisted state/event pairs reject with `state_transition_invalid`. [Source: `_bmad-output/planning-artifacts/architecture.md#Workspace State Transition Matrix (C6 -- Enumerated)`]
- AR-WORKER-02 places working-copy storage under configurable ephemeral work roots such as `/var/lib/hexalith-folders/work/{tenantId}/{folderId}/{taskId}`; checkouts are disposable, never authoritative. [Source: `_bmad-output/planning-artifacts/architecture.md#Architecture Requirements`]
- The current Contract Spine already declares AddFile/ChangeFile/RemoveFile, `FileMutationRequest`, `PutFileInline`, `PutFileStream`, the 262144/262145 boundary, content hash reference, media type fields, and `X-Hexalith-Retry-Transport`. [Source: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#FileMutationRequest`]
- Contract notes state that request-side `X-Hexalith-Retry-As` and response-side `X-Hexalith-Retry-Transport` are deliberately distinct. [Source: `docs/contract/file-context-contract-groups.md#D-9 transport headers`]

### Previous Story Intelligence

- Story 4.5 implemented the current file mutation intake path: `MutateWorkspaceFile`, `WorkspaceFileMutationRequest`, `WorkspaceFileMutationAccepted`, `WorkspaceFileMutationService`, Add/Change/Remove REST routes, EventStore domain processor dispatch, path policy validator, path evidence port, and focused tests.
- Story 4.5 senior review fixed the service ordering so equivalent replay and aggregate workspace/lock validation happen before duplicate path-evidence work. Preserve that ordering for content staging/writer work.
- Story 4.5 senior review fixed REST path-metadata denials to map to `path_validation_failed` safely. Repeat the same mapping discipline for transport/size/media failures.
- Story 4.5 intentionally did not implement real inline/streamed content processing, provider/Git/filesystem mutation, delete ordering, commit execution, context queries, CLI, MCP, UI, or workers. Story 4.6 should fill add/change content transport only.
- Known environmental failures from prior stories: broad solution/AppHost builds can try blocked NuGet/network access, full server route tests can hit sandbox Kestrel socket-binding failures, and broad full `Hexalith.Folders.Contracts.Tests` had unrelated parity-oracle drift while `FileContextContractGroupTests` passed. Prefer focused project builds and in-process test classes.

### Existing Implementation State

- `WorkspaceFileMutationService` currently authorizes with action token `mutate_files`, builds `MutateWorkspaceFile`, validates command/path metadata, loads the stream, checks aggregate acceptance, asks `IWorkspacePathPolicyEvidenceProvider`, checks repository idempotency, and appends metadata-only events. It does not carry actual content bytes, media type, inline content, or stream body evidence.
- `FoldersDomainServiceEndpoints.FileMutationAsync` currently validates `transportOperation`, `contentHashReference`, and `byteLength`, but `inlineContent` and `streamDescriptor` are accepted as raw `JsonElement?` and are not fully validated or forwarded.
- `FileMutationGatewayPayload` deliberately omits `inlineContent` and `streamDescriptor`. Keep content bytes out of EventStore payloads; add only safe metadata/evidence.
- `FolderDomainProcessor` currently reconstructs `WorkspaceFileMutationRequest` from metadata-only payload. It must be extended for media type/transport evidence if those fields are added.
- `FolderAggregate.Handle(MutateWorkspaceFile, ...)` is pure and records `WorkspaceFileMutationAccepted`; it already checks created/active/bound state, exact workspace ID, `locked` or `changes_staged`, lock ownership, lock expiry, C6 transition, path policy, and idempotency.
- `WorkspaceFileMutationAccepted` currently records content hash reference and byte length but not media type. Story 4.6 should add media type if runtime evidence needs to satisfy Story 4.6 AC.
- `UnavailableWorkspacePathPolicyEvidenceProvider` is currently the default DI registration. Any new content-store/writer port must fail closed by default, not pretend writes succeeded.
- Generated `FileMutationRequest.ComputeIdempotencyHash` exists and must not be hand-edited. Generator changes belong under `src/Hexalith.Folders.Client/Generation/` and require generated-output tests.

### Architecture Guardrails

- Repository configuration is authoritative when planning artifacts drift. Current pins include .NET SDK `10.0.300`, Dapr packages `1.17.9`, Aspire `13.3.5`/related pins, NSwag.MSBuild `14.7.1`, Newtonsoft.Json `13.0.4`, xUnit v3 `3.2.2`, Shouldly `4.3.0`, YamlDotNet `18.0.0`, and Playwright `1.60.0`. [Source: `global.json`; `Directory.Packages.props`]
- Use .NET 10, nullable-aware C#, file-scoped namespaces, one primary public type per file, PascalCase public members, camelCase locals/parameters, central package versions, xUnit v3, and Shouldly. [Source: `_bmad-output/project-context.md#Technology Stack & Versions`; `_bmad-output/project-context.md#Testing Rules`]
- Aggregate handlers must stay deterministic and side-effect free. Pass timestamps from callers; never perform I/O, provider calls, Dapr calls, logging, filesystem work, service resolution, or clock reads inside aggregate transitions. [Source: `_bmad-output/project-context.md#Language-Specific Rules`]
- Workers own external provider, Git, working-copy, reconciliation, and rate-limit side effects. Aggregates, REST handlers, CLI, MCP, and UI should not perform provider or filesystem mutations directly. [Source: `_bmad-output/project-context.md#Framework-Specific Rules`]
- Authorization order is contractual: JWT validation, EventStore claim transform, tenant-access freshness, folder ACL, EventStore validator, then Dapr deny-by-default policy. Do not reorder or skip layers for file content operations. [Source: `_bmad-output/project-context.md#Critical Don't-Miss Rules`]
- Metadata-only is mandatory across events, projections, logs, traces, metrics, audit records, Problem Details, console responses, provider diagnostics, generated artifacts, docs examples, and test failure messages. [Source: `_bmad-output/project-context.md#Critical Don't-Miss Rules`]
- EventStore owns envelope metadata and domain rejections are normal events/results, not exceptions. Extension metadata must be sanitized at the API boundary before entering the processing pipeline. [Source: `Hexalith.EventStore/_bmad-output/project-context.md#Critical Don't-Miss Rules`]
- Tenants remains the membership source of truth. Tenant authority comes from authenticated context and EventStore claim-transform evidence, not client-controlled payload or headers. [Source: `Hexalith.Tenants/_bmad-output/project-context.md#Authorization (RBAC -- Role-Based Access Control)`]
- Do not initialize or update nested submodules recursively. [Source: `AGENTS.md`]

### Files To Touch

- `src/Hexalith.Folders/Aggregates/Folder/MutateWorkspaceFile.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceFileMutationRequest.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceFileMutationAccepted.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceFileMutationService.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs`
- New content transport/staging/writer abstractions under `src/Hexalith.Folders/Aggregates/Folder/` or a narrower existing concept folder if implementation discovers a better established location.
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs`
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` only if the current Contract Spine cannot represent actual streamed content safely.
- `src/Hexalith.Folders.Client/Generation/` and generated client files only if the Contract Spine changes and generation is rerun.
- Focused tests under `tests/Hexalith.Folders.Tests/Aggregates/Folder/`, `tests/Hexalith.Folders.Server.Tests/`, and `tests/Hexalith.Folders.Contracts.Tests/OpenApi/`.

### Do Not Touch

- Do not implement RemoveFile provider-safe ordering; Story 4.7 owns delete behavior.
- Do not implement commit execution, provider push, unknown-outcome commit reconciliation, context queries, cleanup repair automation, CLI commands, MCP tools, UI pages, or operations console behavior.
- Do not hand-edit `src/Hexalith.Folders.Client/Generated/*`.
- Do not store raw file contents, base64 content, diffs, local filesystem paths, display names, provider payloads, repository URLs, credential material, tokens, emails, raw claim bags, or unauthorized resource existence in events, audit, logs, traces, metrics, Problem Details, test diagnostics, or docs examples.
- Do not trust `PathMetadata.pathPolicyClass`, `contentHashReference`, `byteLength`, media type, or stream declared length as authority without server-side validation.
- Do not use live GitHub, Forgejo, Aspire, Dapr sidecars, Docker, Keycloak, Redis, network-dependent tests, or nested-submodule initialization for focused Story 4.6 validation.
- Do not loosen path metadata Unicode rules or invent a closed `pathPolicyClass` enum in this story; those are explicitly deferred in contract notes.

### Testing

- Recommended focused validation:
  - `dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore --tlp:off -v:minimal`
  - `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - `tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -parallel none -noLogo -namespace Hexalith.Folders.Tests.Aggregates.Folder`
  - Focused file transport/service tests added by this story.
  - `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - Focused file mutation endpoint/action-token tests changed by this story.
  - `tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -parallel none -noLogo -class Hexalith.Folders.Contracts.Tests.OpenApi.FileContextContractGroupTests`
  - `git diff --check`

### Regression Traps

- The current endpoint DTO includes `InlineContent` and `StreamDescriptor`, but Story 4.5 validation only used top-level `transportOperation`, `contentHashReference`, and `byteLength`. Do not claim streamed content works until actual stream evidence is validated.
- Do not put `inlineContent.contentBytes` or any stream bytes into `FileMutationGatewayPayload`, EventStore envelopes, command status, events, projections, logs, traces, metrics, Problem Details, generated examples outside request examples, or test failure messages.
- Do not read or stage content before coarse authentication and input bounds. Do not write or materialize content before full authorization, lock ownership, idempotency, and path policy have passed.
- Equivalent replay must not repeat content staging or writer calls. Story 4.5 explicitly fixed duplicate evidence work; content transport must preserve that fix.
- `byteLength` must be the decoded/raw byte count, not the base64 string length. The Contract Spine's 349528 character bound is for base64 representation of 262144 raw bytes.
- `contentHashReference` is currently a contract reference, not a free pass. If actual digest comparison is implemented, keep the digest metadata-only and update contract/generator/tests if public shape changes.
- Inline over-bound responses must disclose only the retry transport hint, not the configured byte limit in the body, file path, file content, or authorization details.
- Media type validation must use the Contract Spine token grammar. Do not accept arbitrary MIME parameters unless the spine changes.
- If adding a transient content store, every key must be tenant-prefixed and scoped by folder/workspace/task/operation. A global hash-only content cache is a cross-tenant leak risk.
- Existing lock release must continue to reject `changes_staged`; Story 4.6 creates staged changes, but clean release still requires commit or later recovery behavior.
- Do not let generated NSwag oneOf quirks drive runtime safety. Current generated `FileMutationRequest` has loose `object` properties for some oneOf fields; server/domain validation must enforce the real rules.

### Latest Technical Context

- No new external library or live provider API is required for Story 4.6. Use repository-pinned versions in `global.json` and `Directory.Packages.props`.
- Network research was not needed because the story is governed by project-local Contract Spine, D-9 architecture decision, Story 4.5 implementation, EventStore/Tenants project-context rules, and generated client behavior.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 4.6: Add and change files with inline and streamed content transport`
- `_bmad-output/planning-artifacts/epics.md#Epic 4: Repository-Backed Workspace Task Lifecycle`
- `_bmad-output/planning-artifacts/prd.md#Functional Requirements`
- `_bmad-output/planning-artifacts/prd.md#Non-Functional Requirements`
- `_bmad-output/planning-artifacts/architecture.md#Decision Log`
- `_bmad-output/planning-artifacts/architecture.md#Workspace State Transition Matrix (C6 -- Enumerated)`
- `_bmad-output/project-context.md`
- `Hexalith.EventStore/_bmad-output/project-context.md`
- `Hexalith.Tenants/_bmad-output/project-context.md`
- `docs/contract/file-context-contract-groups.md#D-9 transport headers`
- `docs/contract/idempotency-and-parity-rules.md#Mutating Command Equivalence`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#FileMutationRequest`
- `src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs#FileMutationRequest`
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceFileMutationService.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/FileContextContractGroupTests.cs`
- `_bmad-output/implementation-artifacts/4-5-enforce-workspace-path-policy-before-file-mutations.md`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-27: Story created via `bmad-create-story` workflow using explicit Story ID 4.6, sprint status, Epic 4 source, PRD/architecture/UX discovery, root and sibling project-context facts, Story 4.5 implementation record, Contract Spine, contract notes, generated client behavior, local source, focused tests, and recent git history.
- 2026-05-27: Latest external research was not added because Story 4.6 introduces no new external library/API requirement and should use repository-pinned project-local versions.
- 2026-05-27: Checklist validation applied during authoring: flagged the current stream-descriptor-only runtime gap, required no content bytes in EventStore/events/logs, preserved Story 4.5 replay/evidence ordering, required media-type and actual stream evidence, and constrained scope away from delete/commit/provider/CLI/MCP/UI work.
- 2026-05-27: Implemented add/change content transport validation without changing the OpenAPI Contract Spine or generated client files.
- 2026-05-27: Validation run completed: core/server/contracts builds, focused aggregate/service/path tests, focused server endpoint/action-token tests, FileContextContractGroupTests, and `git diff --check` all passed.
- 2026-05-27: Senior review found that `PutFileStream` runtime validation still accepted descriptor-only evidence. Review fix now requires observed transient staging evidence before gateway submission.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Added REST validation for `inlineContent` and `streamDescriptor`, including media type token grammar, inline base64 decoding/byte-length checks, inline 262144-byte boundary handling, safe 413 retry transport hint, and stream declared/observed length evidence metadata.
- Extended `WorkspaceFileMutationRequest`, `MutateWorkspaceFile`, and `WorkspaceFileMutationAccepted` with media type and transport evidence fields while keeping raw bytes/base64 out of gateway payloads, EventStore command payloads, events, results, and Problem Details.
- Added fail-closed `IWorkspaceFileContentStore` staging boundary and wired `WorkspaceFileMutationService` so content staging occurs after authorization, aggregate lock/workspace/idempotency precheck, path policy evidence, and idempotency lookup, but before append.
- Preserved Contract Spine, generated client files, Add/Change/Remove route identities, C6 `FileMutated` transitions, remove behavior, and existing idempotency equivalence fields.
- Added focused aggregate, service, endpoint, and contract regression coverage for D-9 transport validation, metadata-only event evidence, replay/no-duplicate staging, unavailable content store failure, safe Problem Details, and retry transport header behavior.
- Senior review tightened `PutFileStream` so descriptor-only requests are rejected; streamed requests must now include observed length, staging reference, and observed content hash reference before metadata-only command submission.

### File List

- `docs/contract/file-context-contract-groups.md`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderResultCode.cs`
- `src/Hexalith.Folders/Aggregates/Folder/IWorkspaceFileContentStore.cs`
- `src/Hexalith.Folders/Aggregates/Folder/MutateWorkspaceFile.cs`
- `src/Hexalith.Folders/Aggregates/Folder/UnavailableWorkspaceFileContentStore.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceFileContentStoreRequest.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceFileContentStoreResult.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceFileMutationAccepted.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceFileMutationRequest.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceFileMutationService.cs`
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs`
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs`
- `tests/Hexalith.Folders.Server.Tests/WorkspaceLockEndpointTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderWorkspaceFileMutationAggregateTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderWorkspaceFileMutationServiceTests.cs`

### Senior Developer Review (AI)

Reviewer: Codex on 2026-05-27

Outcome: Approved after auto-fix.

Findings:

- HIGH fixed: `PutFileStream` accepted `streamDescriptor` with only media type, declared length, and upload mode. That violated AC3 and the story task that streamed writes must not be faked from a descriptor alone. Fixed in `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs` by requiring observed transient staging evidence (`observedLength`, `stagingReference`, and matching `observedContentHashReference`) before gateway submission.
- MEDIUM fixed: Contract/documentation did not spell out the runtime evidence needed to prevent descriptor-only stream acceptance. Updated `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` and `docs/contract/file-context-contract-groups.md`.
- MEDIUM fixed: Endpoint regression coverage allowed descriptor-only stream acceptance. Added a focused rejection test and updated stream helpers in `tests/Hexalith.Folders.Server.Tests/WorkspaceLockEndpointTests.cs`.

Validation:

- `dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore --tlp:off -v:minimal`
- `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
- `tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -parallel none -noLogo -class Hexalith.Folders.Tests.Aggregates.Folder.FolderWorkspaceFileMutationAggregateTests -class Hexalith.Folders.Tests.Aggregates.Folder.FolderWorkspaceFileMutationServiceTests -class Hexalith.Folders.Tests.Aggregates.Folder.WorkspacePathPolicyValidatorTests`
- `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
- `tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests -parallel none -noLogo -class Hexalith.Folders.Server.Tests.WorkspaceLockEndpointTests`
- `dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
- `tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -parallel none -noLogo -class Hexalith.Folders.Contracts.Tests.OpenApi.FileContextContractGroupTests`
- `git diff --check`

### Change Log

- 2026-05-27: Implemented Story 4.6 add/change inline and streamed transport metadata validation and fail-closed content staging boundary; added focused regression coverage; moved story to review.
- 2026-05-27: Senior review auto-fixed descriptor-only stream acceptance, updated contract notes/OpenAPI stream evidence, reran focused validation, and moved story to done.
