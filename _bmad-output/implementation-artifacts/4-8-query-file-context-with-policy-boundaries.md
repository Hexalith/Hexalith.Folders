---
baseline_commit: 9afacf6
---

# Story 4.8: Query file context with policy boundaries

Status: done

## Story

As a developer or AI agent,
I want file tree, metadata, search, glob, bounded range-read, and extension-safe semantic context-query behavior,
so that task context is useful without unbounded scans, stale derived-index authority, or secret exposure.

## Acceptance Criteria

1. Given an authorized caller requests file context for a repository-backed folder workspace, when the query runs, then tenant access, folder ACL, path policy, sensitivity classification, C4 input/response bounds, and projection/workspace freshness checks complete before any provider, Git, working-copy, filesystem, Memories, semantic index, or derived context observation.
2. Given a caller requests tree, metadata, search, glob, or range-read context, when the request is valid, then the runtime uses the existing OpenAPI route shapes and operation IDs: `ListFolderFiles`, `GetFolderFileMetadata`, `SearchFolderFiles`, `GlobFolderFiles`, and `ReadFileRange`.
3. Given a context query is denied by tenant access, folder ACL, path policy, sensitivity classification, input limits, response limits, unavailable read model, unavailable context source, stale workspace evidence, timeout, binary/large-file policy, or redaction policy, when the response is produced, then it uses the canonical safe category and emits no file content, raw provider payload, local path, repository URL, credential material, token, email, unauthorized-resource existence hint, or unbounded diagnostic detail.
4. Given context is returned for tree, metadata, search, and glob queries, when the response is serialized, then entries are metadata-only and include only safe path metadata/digests, file type/size/hash/status metadata where allowed, truncation/limit metadata, query freshness, correlation/task context, and policy evidence required by the Contract Spine.
5. Given `ReadFileRange` is requested, when authorization and policy pass, then only this operation may return file bytes, the returned bytes are bounded by C4 range and response budgets, the response includes range/limit metadata, and no bytes are returned for denied, redacted, binary-disallowed, over-bound, unsatisfiable, stale, or unavailable cases.
6. Given search or glob would require scanning too much state, when limits would be exceeded, then execution stops deterministically with `input_limit_exceeded`, `response_limit_exceeded`, or `query_timeout` and metadata-only evidence rather than continuing an unbounded scan.
7. Given semantic/RAG retrieval is configured now or later, when a query requests semantic context, then Folders authorization, folder ACL, path policy, sensitivity classification, workspace/file truth, and C4 bounds are authoritative; Memories or any derived index may be invoked only after those checks pass and must never be treated as authority for tenant access, ACLs, file truth, workspace state, or audit truth.
8. Given Story 4.8 is implemented, when existing folder creation, repository binding, branch/ref policy, provider readiness, workspace preparation, lock acquisition/release, file mutation stories 4.5-4.7, Contract Spine, generated-client guardrails, safe-denial tests, and focused context-query tests run, then they continue to pass without manually editing generated client files.

## Tasks / Subtasks

- [x] Preserve the existing context-query contract shape. (AC: 2, 8)
  - [x] Reuse the OpenAPI paths under `/api/v1/folders/{folderId}/workspaces/{workspaceId}/context/*`.
  - [x] Preserve operation IDs `ListFolderFiles`, `GetFolderFileMetadata`, `SearchFolderFiles`, `GlobFolderFiles`, and `ReadFileRange`.
  - [x] Do not add idempotency-key requirements to read-only context queries.
  - [x] Change OpenAPI, generated client, parity docs, or contract tests only if implementation finds real contract/runtime drift that must be fixed together.

- [x] Add a context-query runtime boundary. (AC: 1, 3, 4, 5, 6, 7)
  - [x] Introduce a small query service/handler layer under `src/Hexalith.Folders/Queries` or the nearest existing query namespace.
  - [x] Keep query execution read-only and side-effect free except for safe audit/diagnostic evidence already supported by the project.
  - [x] Add a context source port for file tree, metadata, search, glob, and bounded range-read execution.
  - [x] Provide a fail-closed default implementation that returns the closest canonical safe unavailable category instead of pretending context exists.
  - [x] Keep provider/Git/filesystem/Memories details behind ports; do not put direct provider or filesystem calls in REST handlers.

- [x] Enforce authorization-before-observation order. (AC: 1, 3, 7, 8)
  - [x] Apply tenant access and folder ACL checks before any context source call.
  - [x] Apply syntactic path policy and path evidence/sensitivity classification before any matching, search, glob, range, provider, working-copy, filesystem, or Memories call.
  - [x] Apply C4 input bounds before query execution and C4 response bounds during/after execution.
  - [x] Ensure denied queries do not call the context source and do not disclose whether the path, repository, file, workspace, semantic result, or indexed unit exists.

- [x] Implement bounded query semantics. (AC: 4, 5, 6)
  - [x] Tree and metadata queries must cap requested path counts and returned item counts.
  - [x] Search and glob queries must cap query text/pattern size, result count, response bytes, and timeout.
  - [x] Range-read must reject reversed, over-bound, and unsatisfiable ranges safely.
  - [x] Range-read must return bytes only after all policy checks pass and only within configured maximum range bytes.
  - [x] Binary, large-file, redacted, stale, or unavailable cases must return canonical safe categories without bytes.

- [x] Preserve metadata-only output and safe-denial behavior. (AC: 3, 4, 5, 7)
  - [x] Do not include file contents outside successful `ReadFileRange`.
  - [x] Do not include diffs, provider payloads, local filesystem paths, repository URLs, credential material, tokens, emails, raw claim bags, raw Memories payloads, generated context payloads, or unauthorized-resource hints in results, Problem Details, logs, traces, metrics, audit records, docs examples, or test diagnostics.
  - [x] Include truncation, freshness, retry, and limit metadata only when it is safe and contract-compatible.
  - [x] Treat derived semantic indexes as advisory data filtered by current Folders policy, never as authorization or file-truth authority.

- [x] Wire REST endpoints and DI safely. (AC: 1-8)
  - [x] Reuse `FoldersDomainServiceEndpoints` or established endpoint modules rather than creating a conflicting API surface.
  - [x] Derive tenant/principal authority from authenticated context and EventStore claim-transform evidence, not request body values.
  - [x] Keep route `folderId` and `workspaceId` authoritative.
  - [x] Register new context query services and fail-closed ports in DI.
  - [x] Preserve existing file mutation endpoint behavior.

- [x] Add focused context-query tests. (AC: 1-8)
  - [x] Add service tests proving authorization, path policy, sensitivity, and C4 bounds occur before context source observation.
  - [x] Add service tests for tree, metadata, search, glob, and range-read success using recording context sources.
  - [x] Add denial tests for tenant/folder ACL denial, path denial, sensitivity redaction, input limit, response limit, timeout, read-model/source unavailable, stale evidence, binary/large-file policy, and range errors.
  - [x] Add endpoint tests for route-authoritative folder/workspace, safe Problem Details, no unsafe echo, successful metadata-only query responses, and successful bounded range-read response.
  - [x] Add metadata leakage tests using `tests/fixtures/audit-leakage-corpus.json` across results, Problem Details, logs/test diagnostics, and docs examples touched by this story.
  - [x] Keep `FileContextContractGroupTests` green; update only if the Contract Spine intentionally changes.

- [x] Preserve focused regression gates. (AC: 8)
  - [x] `dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore --tlp:off -v:minimal`
  - [x] `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - [x] Run focused query, authorization, path-policy, and file-mutation tests added or touched by this story.
  - [x] `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - [x] Run focused server endpoint tests added or touched by this story.
  - [x] `dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - [x] `tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -parallel none -noLogo -class Hexalith.Folders.Contracts.Tests.OpenApi.FileContextContractGroupTests`
  - [x] `git diff --check`

## Dev Notes

### Source Context

- Epic 4 requires file tree, metadata, search, glob, bounded range-read, and extension-safe semantic context-query behavior with policy boundaries. [Source: `_bmad-output/planning-artifacts/epics.md#Story 4.8: Query file context with policy boundaries`]
- PRD FR34-FR36 require file context queries with bounded retrieval, search/glob/metadata operations, and policy-safe behavior. [Source: `_bmad-output/planning-artifacts/prd.md#Functional Requirements`]
- PRD NFRs require tenant isolation on every query, C4 input/response bounds, no unbounded workspace scans, and metadata-only audit/diagnostic evidence. [Source: `_bmad-output/planning-artifacts/prd.md#Non-Functional Requirements`]
- Contract notes define the binding authorization order for context queries: `tenant_access -> folder_acl -> path_policy -> sensitivity_classification -> c4_bounds -> query_execution`. [Source: `docs/contract/file-context-contract-groups.md#Authorization-order vocabulary`]
- `FileContextContractGroupTests` already verifies context operations omit idempotency, declare read consistency, use safe-denial vocabulary, and preserve C4 bounds/range semantics. [Source: `tests/Hexalith.Folders.Contracts.Tests/OpenApi/FileContextContractGroupTests.cs`]
- OpenAPI already declares context-query routes under `/api/v1/folders/{folderId}/workspaces/{workspaceId}/context/*`. Runtime endpoints may not exist yet. [Source: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`]
- Memories/RAG research says Folders must remain the policy enforcement point and Memories/semantic indexes must be invoked only after current Folders authorization and policy checks. [Source: `_bmad-output/planning-artifacts/research/technical-hexalith-memories-semantic-indexing-rag-research-2026-05-11.md`]

### Previous Story Intelligence

- Stories 4.5-4.7 established the runtime ordering discipline for file operations: tenant/folder authority first, aggregate/precheck before expensive observation, path policy/evidence before side effects, idempotency before staging, and fail-closed ports for unavailable side-effect evidence.
- Story 4.7 added a fail-closed side-effect boundary and review found that request metadata must carry explicit operation kind. Apply the same explicit metadata style to context-query ports.
- Existing endpoint tests use ASP.NET Core TestServer and recording fakes. Prefer extending those patterns over live provider/Dapr/Aspire lanes.

### Existing Implementation State

- `FoldersDomainServiceEndpoints` currently maps domain service, lifecycle/status, lock, and file mutation routes. It may not yet map context query routes despite OpenAPI declaring them.
- `FoldersServerModule.MapFoldersServerEndpoints` maps `MapFoldersDomainServiceEndpoints`, provider readiness, and tenant event subscription.
- Query patterns already exist under `src/Hexalith.Folders/Queries/Folders` for lifecycle, branch/ref, and lock status handlers/read models.
- Authorization query patterns already exist under `src/Hexalith.Folders/Authorization` with effective permissions and folder permission evidence.
- `WorkspacePathPolicyValidator`, `IWorkspacePathPolicyEvidenceProvider`, and related tests are the nearest reusable path policy runtime surface.

### Files To Touch

- `src/Hexalith.Folders/Queries/` for context query request/result/handler/service/port types.
- `src/Hexalith.Folders/Aggregates/Folder/WorkspacePathPolicyValidator.cs` only if a reusable path policy helper needs a narrowly scoped extension.
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs` or a sibling endpoint module for REST mapping.
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs` for DI registration.
- `src/Hexalith.Folders.Server/FoldersServerModule.cs` only if a new endpoint module must be mapped.
- `tests/Hexalith.Folders.Tests/Queries/` for query service/handler tests.
- `tests/Hexalith.Folders.Server.Tests/` for endpoint tests.
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/FileContextContractGroupTests.cs` only if contract drift is intentional.

### Do Not Touch

- Do not implement file mutation behavior, commit execution, provider push, cleanup repair automation, CLI commands, MCP tools, UI pages, or operations console behavior.
- Do not implement full semantic indexing, Memories ingestion, Memories deletion, RAG ranking, vector search, or indexing workers in this story.
- Do not make derived Memories or semantic-index state authoritative for tenant access, folder ACL, file truth, workspace state, or audit truth.
- Do not return file content from tree, metadata, search, or glob queries.
- Do not add unbounded recursive scans, live provider calls, local filesystem path trust, or search-first/filter-later behavior.
- Do not hand-edit generated client files.
- Do not initialize or update nested submodules recursively.

### Testing

- Recommended focused validation:
  - `dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore --tlp:off -v:minimal`
  - `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - run focused query/path/authorization tests added or touched by this story
  - `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - run focused server endpoint tests added or touched by this story
  - `dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
  - `tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -parallel none -noLogo -class Hexalith.Folders.Contracts.Tests.OpenApi.FileContextContractGroupTests`
  - `git diff --check`

### Regression Traps

- Authorization and path/sensitivity checks must happen before query execution. Search-first/filter-later is a security regression.
- Context-query endpoints are read-only and must not require idempotency keys.
- Tree, metadata, search, and glob responses must remain metadata-only.
- Range-read is the only context query that may return bytes, and only after policy and bounds pass.
- Do not let safe-denial Problem Details echo requested raw paths, query text, glob patterns, local paths, provider payloads, or content bytes.
- Do not treat Memories or any semantic index as the source of authorization or file truth.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-27: Story created manually by story-automator after two create-story child sessions stalled after context gathering without writing the artifact.
- 2026-05-27: Context loaded from Epic 4 story, PRD, architecture, file-context contract notes, OpenAPI Contract Spine, FileContextContractGroupTests, Memories/RAG research, existing query/endpoint/path-policy source, and Stories 4.5-4.7 implementation patterns.
- 2026-05-27: Implemented context-query boundary and REST routes without changing OpenAPI, generated clients, parity docs, or contract tests.
- 2026-05-27: Focused validation passed for new query service tests, endpoint tests, path-policy tests, authorization tests, file-mutation service tests, contract file-context tests, required builds, and `git diff --check`.
- 2026-05-27: Broader full-suite attempts: `Hexalith.Folders.Tests` passed 1008/1008; full `Hexalith.Folders.Server.Tests` has unrelated Kestrel socket permission failures in archive endpoint tests; full `Hexalith.Folders.Contracts.Tests` has pre-existing parity oracle byte-stability/order failures outside files touched by this story.

### Completion Notes List

- Added `WorkspaceFileContextQueryHandler` and supporting request/result/source/sensitivity types under `src/Hexalith.Folders/Queries/FileContext`.
- Added `IWorkspaceFileContextSource` plus fail-closed unavailable default to keep provider, Git, filesystem, Memories, and derived-index observations behind a port.
- Enforced layered authorization before path/sensitivity/C4 checks and before context source calls; denied paths do not call the source.
- Added bounded tree, metadata, search, glob, and range-read routing through existing `/context/*` OpenAPI shapes and operation names.
- Kept read-only context routes free of idempotency-key requirements and rejected `Idempotency-Key` before body parsing/source observation.
- Added focused service and endpoint tests for ordering, route registration, metadata-only results, bounded range-read bytes, safe Problem Details, and leakage-corpus non-echo.

### File List

- `_bmad-output/implementation-artifacts/4-8-query-file-context-with-policy-boundaries.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/tests/test-summary.md`
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs`
- `src/Hexalith.Folders/Queries/FileContext/IWorkspaceFileContextSource.cs`
- `src/Hexalith.Folders/Queries/FileContext/IWorkspaceFileSensitivityClassifier.cs`
- `src/Hexalith.Folders/Queries/FileContext/UnavailableWorkspaceFileContextSource.cs`
- `src/Hexalith.Folders/Queries/FileContext/WorkspaceFileContextItem.cs`
- `src/Hexalith.Folders/Queries/FileContext/WorkspaceFileContextLimits.cs`
- `src/Hexalith.Folders/Queries/FileContext/WorkspaceFileContextPage.cs`
- `src/Hexalith.Folders/Queries/FileContext/WorkspaceFileContextQuery.cs`
- `src/Hexalith.Folders/Queries/FileContext/WorkspaceFileContextQueryHandler.cs`
- `src/Hexalith.Folders/Queries/FileContext/WorkspaceFileContextQueryKind.cs`
- `src/Hexalith.Folders/Queries/FileContext/WorkspaceFileContextQueryPath.cs`
- `src/Hexalith.Folders/Queries/FileContext/WorkspaceFileContextQueryResult.cs`
- `src/Hexalith.Folders/Queries/FileContext/WorkspaceFileContextRange.cs`
- `src/Hexalith.Folders/Queries/FileContext/WorkspaceFileContextResultCode.cs`
- `src/Hexalith.Folders/Queries/FileContext/WorkspaceFileContextSourceRequest.cs`
- `src/Hexalith.Folders/Queries/FileContext/WorkspaceFileContextSourceResult.cs`
- `src/Hexalith.Folders/Queries/FileContext/WorkspaceFileContextSourceStatus.cs`
- `src/Hexalith.Folders/Queries/FileContext/WorkspaceFileSensitivityClassifier.cs`
- `src/Hexalith.Folders/Queries/FileContext/WorkspaceFileSensitivityDecision.cs`
- `src/Hexalith.Folders/Queries/FileContext/WorkspacePathSensitivityResult.cs`
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`
- `src/Hexalith.Folders.Server/FoldersServerModule.cs`
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs`
- `tests/Hexalith.Folders.Tests/Queries/FileContext/WorkspaceFileContextQueryHandlerTests.cs`
- `tests/Hexalith.Folders.Server.Tests/FileContextEndpointTests.cs`

### Senior Developer Review (AI)

Reviewer: GPT-5 Codex on 2026-05-27

Outcome: Approved after auto-fixes. Status moved to `done`.

Findings fixed:

- HIGH: Metadata requests with no paths were accepted and could reach the context source despite the OpenAPI `FileMetadataRequest.paths` `minItems: 1` contract. Fixed with handler validation and endpoint regression coverage.
- HIGH: Search/glob requests without the contract-required `limit` field were accepted and defaulted internally, drifting from the OpenAPI request shape. Fixed with handler validation and endpoint regression coverage.
- HIGH: A successful range-read source result could return range metadata exceeding the requested byte window as long as it stayed below the global C4 maximum. Fixed by validating returned range offsets, actual byte count, and partial semantics before emitting bytes.
- MEDIUM: Unsatisfiable range-read responses used `category: validation_error` instead of the canonical `range_unsatisfiable` category from the Contract Spine examples. Fixed the HTTP mapping and added endpoint coverage.

Validation:

- `dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore --tlp:off -v:minimal`
- `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
- `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
- `tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -parallel none -noLogo -class Hexalith.Folders.Tests.Queries.FileContext.WorkspaceFileContextQueryHandlerTests`
- `tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests -parallel none -noLogo -class Hexalith.Folders.Server.Tests.FileContextEndpointTests`
- `dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal`
- `tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -parallel none -noLogo -class Hexalith.Folders.Contracts.Tests.OpenApi.FileContextContractGroupTests`
- `git diff --check`

Notes:

- Review used the local Contract Spine, contract notes, project context, story artifact, and changed source/tests. No external MCP/web documentation search was used because this review did not change external library/API usage and network access is restricted in this session.
- Git contains unrelated dirty changes inside the root-level `Hexalith.Builds` submodule. Per repository instructions, no submodule initialization/update or nested-submodule work was performed.

### Change Log

- 2026-05-27: Implemented Story 4.8 context-query runtime boundary, REST endpoints, DI, safe-denial behavior, and focused tests; moved story to review.
- 2026-05-27: Senior review auto-fixed context-query input-shape validation, range-read result bounds enforcement, canonical range-unsatisfiable HTTP mapping, and focused regression tests; moved story to done.
