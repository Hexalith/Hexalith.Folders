---
baseline_commit: 9c8cfc9
---

# Story 10.1: Define the worker-side semantic-indexing port and Memories dependency

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a Folders platform engineer,
I want a worker-owned semantic-indexing port and the Memories client/contracts dependencies isolated to `Hexalith.Folders.Workers`,
so that later Epic 10 stories can index authorized file versions without leaking Memories dependencies into the Folders contract, core, server, client, CLI, MCP, or UI surfaces.

## Context & Scope Boundary

Epic 10 activates the dormant Memories routing delivered by Epic 9. Story 10.1 is the dependency and abstraction foundation only. It must make the worker project capable of talking to Memories through a narrow Folders-owned port, while preserving the existing architecture rule that Memories is a derived index, not an authoritative Folders datastore.

In scope:

1. Add the `Hexalith.Memories.Client.Rest` and `Hexalith.Memories.Contracts` references to `src/Hexalith.Folders.Workers/Hexalith.Folders.Workers.csproj` only.
2. Define a worker-side semantic-indexing port under `src/Hexalith.Folders.Workers/SemanticIndexing/` with request/result/status records shaped for future file-version indexing.
3. Register the port through `FoldersWorkersModule` without changing existing tenant-event worker behavior.
4. Add focused tests proving the port is registered and the Memories dependency remains isolated to Workers.

Out of scope:

1. Do not emit `SearchIndexEntryChanged` or `SearchIndexEntryRemoved` CloudEvents; that is Story 10.4.
2. Do not build the durable bridge projection (`file version -> Memories workflow/memory-unit/status`); that is Story 10.2.
3. Do not subscribe to file-write/commit events or index real file content; that is Story 10.3.
4. Do not expose a Folders query facade over Memories; that is Story 10.5.
5. Do not change `deploy/dapr/production/*`, `tests/fixtures/dapr-policy-conformance.yaml`, or Dapr policy conformance rows in this story. The `folders`/`folders-workers -> memories` invoke authorization and `memories-events` pub/sub scopes are intentionally deferred to the producer/authorization stories.

## Acceptance Criteria

1. **Workers-only Memories dependency.** `src/Hexalith.Folders.Workers/Hexalith.Folders.Workers.csproj` references `$(HexalithMemoriesRoot)\src\Hexalith.Memories.Client.Rest\Hexalith.Memories.Client.Rest.csproj` and `$(HexalithMemoriesRoot)\src\Hexalith.Memories.Contracts\Hexalith.Memories.Contracts.csproj`. No direct `Hexalith.Memories.Client.Rest`, `Hexalith.Memories.Contracts`, `Hexalith.Memories.Server`, or core `Hexalith.Memories` reference is added to `Hexalith.Folders.Contracts`, `Hexalith.Folders`, `Hexalith.Folders.Server`, `Hexalith.Folders.Client`, `Hexalith.Folders.Cli`, `Hexalith.Folders.Mcp`, `Hexalith.Folders.UI`, `Hexalith.Folders.Testing`, `Hexalith.Folders.Aspire`, or `Hexalith.Folders.AppHost` beyond the existing AppHost-only `Hexalith.Memories.Aspire` topology reference.

2. **Worker-owned semantic-indexing port exists.** A new `SemanticIndexing` area under `src/Hexalith.Folders.Workers/` defines a Folders-owned abstraction such as `ISemanticIndexingPort` with an async method that accepts a cancellation token and returns typed result data. The request/result records include the future indexing identity and policy inputs needed by later stories: managed tenant id, organization id, folder id, file version/content hash identity, stable source URI or source URI parts, indexing text/content descriptor, size/type classification, sensitivity/path-policy outcome, correlation id, task id, and idempotency key. The port contract must not expose raw filesystem paths as public identity and must not require callers outside Workers to know Memories DTOs.

3. **Memories client registration is encapsulated in Workers.** `FoldersWorkersModule` adds a semantic-indexing registration method, for example `AddFoldersSemanticIndexingWorkers`, and `AddFoldersTenantEventWorkers` invokes it. The registration uses the Memories client package's public API (`AddMemoriesClient(...)`, `MemoriesClientOptions`, and typed `MemoriesClient`) rather than a hand-rolled `HttpClient` wrapper. The implementation may be an adapter shell that maps Folders request/result types to Memories contracts later; it must not start indexing real files in this story.

4. **Constants reuse Epic 9 routing values.** Any worker-side defaults for the CloudEvents source or index tenant use the existing `FoldersAspireModule.MemoriesSourceId` (`hexalith-folders`) and `FoldersAspireModule.MemoriesIndexTenant` (`folders-index`) constants, or define worker-local constants with tests that assert exact parity with those values if referencing `Hexalith.Folders.Aspire` from Workers would violate dependency direction. Do not duplicate untested string literals for `hexalith-folders`, `folders-index`, `pubsub`, or `memories-events`.

5. **Boundary tests fail closed.** Update `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs` so `ProjectReferencesFollowAllowedDependencyDirection` allows the two Memories project references on `Hexalith.Folders.Workers` and still forbids Memories client/contracts references everywhere else. Add an explicit forbidden-reference assertion for `Hexalith.Memories.Client.Rest` and `Hexalith.Memories.Contracts` on non-worker Folders projects so future leakage fails the scaffold contract test.

6. **Worker tests cover DI without side effects.** Add tests under `tests/Hexalith.Folders.Workers.Tests/` proving the semantic-indexing services register through `AddFoldersTenantEventWorkers` / the new registration method and can be resolved from a service provider with validation enabled. The test must not require Dapr sidecars, a running Memories server, provider credentials, tenant seed data, Keycloak, Redis, network calls, or nested submodule initialization.

7. **Production policy remains untouched.** `deploy/dapr/production/accesscontrol.yaml`, `deploy/dapr/production/pubsub.yaml`, `deploy/dapr/production/sidecar-config-bindings.yaml`, and `tests/fixtures/dapr-policy-conformance.yaml` remain unchanged unless a later Epic 10 story explicitly adds producer authorization and its negative-control rows. Existing conformance assertions that `memories` has no callers and no pub/sub scopes remain valid after 10.1.

8. **Verification passes.** `dotnet build Hexalith.Folders.slnx` succeeds. The narrowed tests pass: `dotnet test tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj` and `dotnet test tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj`. If restore/build is blocked by local environment state, record the exact blocker and do not mark implementation complete.

## Tasks / Subtasks

- [x] Task 1 - Add Workers-only Memories references (AC: 1, 5)
  - [x] Add `ProjectReference` entries in `src/Hexalith.Folders.Workers/Hexalith.Folders.Workers.csproj` for `$(HexalithMemoriesRoot)\src\Hexalith.Memories.Client.Rest\Hexalith.Memories.Client.Rest.csproj` and `$(HexalithMemoriesRoot)\src\Hexalith.Memories.Contracts\Hexalith.Memories.Contracts.csproj`.
  - [x] Do not add inline package versions. Do not add Memories package references to central package management unless the team intentionally switches from sibling project references to NuGet packages.
  - [x] Do not add the Memories projects to `Hexalith.Folders.slnx` unless build tooling requires it; existing sibling references are resolved through `Directory.Build.props`.

- [x] Task 2 - Define the worker-side semantic-indexing port (AC: 2)
  - [x] Add `src/Hexalith.Folders.Workers/SemanticIndexing/ISemanticIndexingPort.cs`.
  - [x] Add request/result/status records in the same area. Keep them Folders-owned and metadata-safe.
  - [x] Include `CancellationToken` on the async port method and validate public-boundary arguments with `ArgumentNullException.ThrowIfNull` / `ArgumentException.ThrowIfNullOrWhiteSpace`.
  - [x] Use ordinal comparisons and invariant formatting for identifiers, source URI parts, idempotency keys, and status codes.

- [x] Task 3 - Add a Memories-backed adapter shell and DI registration (AC: 3, 4)
  - [x] Add a worker-local adapter such as `MemoriesSemanticIndexingPort` that depends on `MemoriesClient` and any worker-local options needed to configure the endpoint.
  - [x] Register the Memories typed client through `services.AddMemoriesClient(...)`; do not instantiate `HttpClient` directly.
  - [x] Add `AddFoldersSemanticIndexingWorkers` to `FoldersWorkersModule` and call it from `AddFoldersTenantEventWorkers`.
  - [x] Keep the adapter shell non-producing in this story: no Dapr publish, no file event subscription, no filesystem reads, and no production policy changes.

- [x] Task 4 - Harden dependency-boundary tests (AC: 1, 5)
  - [x] Update `ScaffoldContractTests.ProjectReferencesFollowAllowedDependencyDirection` so `Hexalith.Folders.Workers` is the only Folders project whose allowed references include `Hexalith.Memories.Client.Rest` and `Hexalith.Memories.Contracts`.
  - [x] Extend `ForbiddenReferencesAreNotIntroduced` or add a new test to assert those Memories references are absent from Contracts, core, Server, Client, CLI, MCP, UI, Testing, Aspire, and AppHost, except for the existing AppHost `Hexalith.Memories.Aspire` topology reference.
  - [x] Keep existing `Hexalith.Memories.Aspire` allowances in AppHost and IntegrationTests unchanged.

- [x] Task 5 - Add worker DI tests (AC: 3, 6)
  - [x] Add a focused test file under `tests/Hexalith.Folders.Workers.Tests/`, for example `SemanticIndexingWorkerRegistrationTests.cs`.
  - [x] Build a service provider with `ValidateOnBuild = true` and `ValidateScopes = true` for the new registration path.
  - [x] Assert `ISemanticIndexingPort` resolves and the existing tenant-event constants/tests remain green.
  - [x] Use `TestContext.Current.CancellationToken` for async test paths.

- [x] Task 6 - Verify unchanged deferred policy artifacts (AC: 7)
  - [x] Confirm no diff in `deploy/dapr/production/accesscontrol.yaml`, `deploy/dapr/production/pubsub.yaml`, `deploy/dapr/production/sidecar-config-bindings.yaml`, or `tests/fixtures/dapr-policy-conformance.yaml`.
  - [x] Confirm no new `memories-events` topic scope or `folders-workers -> memories` allow-rule is added in 10.1.

- [x] Task 7 - Build and test (AC: 8)
  - [x] Run `dotnet build Hexalith.Folders.slnx`.
  - [x] Run `dotnet test tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj`.
  - [x] Run `dotnet test tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj`.

## Dev Notes

### Source facts

- Epic 10 goal: authorized file changes are asynchronously indexed into Memories through a worker-side producer and a Folders-owned bridge projection, activating Epic 9 routing and later exposing an authorized, security-trimmed query facade. [Source: `_bmad-output/planning-artifacts/epics.md` "Epic 10: Folders Worker-Side Semantic-Indexing Producer And Bridge Projection"]
- Story 10.1 seed AC: Memories dependency is restricted to `Hexalith.Folders.Workers`; when the worker-side semantic-indexing port is defined and Workers references `Hexalith.Memories.Client.Rest` + `Hexalith.Memories.Contracts`, no other Folders project depends on Memories. [Source: `_bmad-output/planning-artifacts/epics.md` "Story 10.1"]
- Memories is a separate Dapr-enabled derived semantic index, not an authoritative Folders datastore. File content may be read by workers after authorization and sent to Memories for indexing, but it must not be embedded in Folders events, projections, logs, traces, metrics, audit records, or error responses. [Source: `_bmad-output/planning-artifacts/architecture.md` "Hexalith.Memories integration implications"]
- Architecture requires a worker-side semantic-indexing port before any direct Memories reference; only `Hexalith.Folders.Workers` may initially depend on `Hexalith.Memories.Client.Rest` / `Hexalith.Memories.Contracts`. [Source: `_bmad-output/planning-artifacts/architecture.md` "Hexalith.Memories integration implications"]
- Authorization and policy order for future indexing is tenant access -> folder ACL -> path policy -> sensitivity classification -> size/type limits -> Memories. Story 10.1 should shape request data for that order but must not implement the event-driven indexing flow yet. [Source: `_bmad-output/planning-artifacts/architecture.md` "Hexalith.Memories integration implications"]
- C4 input limits are PM-approved and binding: max requested paths 100, max search/glob results 500, max single bounded range bytes 262144, max aggregate response bytes 1048576, max query duration 2 seconds, with metadata-only audit visibility. Later indexing stories must respect these bounds. [Source: `docs/exit-criteria/c4-input-limits.md`]
- C9 default sensitive metadata tier classifies paths, repo names, branch names, and commit messages as tenant-sensitive; avoid raw path metadata unless C9 explicitly allows exposure. [Source: `_bmad-output/planning-artifacts/architecture.md` "S-6 Sensitive metadata classification"]

### Current code state to preserve

- `src/Hexalith.Folders.Workers/Hexalith.Folders.Workers.csproj` currently references Folders core/contracts/service defaults, EventStore DomainService, Tenants client/contracts, and `Dapr.AspNetCore`. Add Memories references here only.
- `FoldersWorkersModule.AddFoldersTenantEventWorkers` currently registers Dapr client, tenant-access projection, Tenants event subscription options, tenant event projection handlers, and repository provisioning workers. Preserve this behavior and add semantic-indexing registration without changing tenant-event route/topic constants.
- `src/Hexalith.Folders.Workers/Program.cs` composes service defaults, calls `AddFoldersTenantEventWorkers`, maps CloudEvents and subscribe handler, maps worker endpoints, and maps default endpoints. Do not add file indexing endpoints or new Dapr topic subscriptions in 10.1.
- `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs` is the existing dependency-direction gate. Update it deliberately rather than adding a second ad hoc project scanner.
- `Hexalith.Memories.Client.Rest` exposes `AddMemoriesClient(...)`, `MemoriesClientOptions`, typed `MemoriesClient`, and `MemoriesRemoteException`. Use that client registration API; do not duplicate its `HttpClient` setup.
- `Hexalith.Memories.Contracts.V1` already contains `SearchIndexEntryChanged`, `SearchIndexEntryRemoved`, `MemoryUnit`, `MemoryUnitStatus`, `SearchResult`, `ScoredResult`, `IngestionInput`, and `IngestionResult`. For 10.1, depend on contracts only where the worker adapter shell needs the type reference; do not let Memories DTOs escape the worker-owned port.

### Epic 9 carry-forward intelligence

- Story 9.3 shipped `FoldersAspireModule.MemoriesSourceId = "hexalith-folders"` and `FoldersAspireModule.MemoriesIndexTenant = "folders-index"` plus `WithFoldersMemoriesSourceRouting()`. The AppHost routing is present and dormant until the Epic 10 producer emits `SearchIndexEntryChanged`.
- The Memories handoff states the future producer publishes to pub/sub component `pubsub`, topic `memories-events`, with CloudEvent type `SearchIndexEntryChanged`, source `hexalith-folders`, and stable CloudEvent id. Story 10.1 does not publish this event, but the port request should carry enough identity for later idempotent upsert-shaped emission.
- Epic 9 intentionally left `memories` deny-by-default in production with no caller allow-rules and no pub/sub topic scopes. Do not weaken this in 10.1.
- Live `aspire run` boot remains unverified due to an environment-wide Aspire CLI/DCP `--tls-cert-file` blocker. Do not treat that as a topology defect if encountered; rely on build and structural tests unless a DCP-capable lane is available.

### Architecture and dependency guardrails

- Repository configuration is authoritative when planning artifacts drift. Use `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `.csproj`, and `Hexalith.Folders.slnx` over older prose. Current `Directory.Packages.props` pins Dapr `1.18.4`; `project-context.md` contains older Dapr package text and should not override the actual repo files.
- Use root-level sibling module project references through `$(HexalithMemoriesRoot)`, which is already defined in `Directory.Build.props`. Do not initialize nested submodules and do not add recursive submodule setup instructions.
- Keep source organized by concept area. Use `src/Hexalith.Folders.Workers/SemanticIndexing/` instead of placing worker abstractions in Contracts, core, Server, or Client.
- Public async worker paths must accept and propagate `CancellationToken`; library/worker code should use `ConfigureAwait(false)` where nearby code does.
- Worker-side external effects belong in Workers process managers or adapters. Aggregates, REST handlers, CLI, MCP, UI, and Contracts must not perform Memories calls.
- Problem responses, logs, metrics, traces, and test failure messages remain metadata-only. Never log raw file content, raw paths, diffs, provider payloads, secrets, tokens, or unauthorized resource existence.

### Testing guidance

- Prefer `tests/Hexalith.Folders.Workers.Tests` for worker DI and port tests, using xUnit v3 and Shouldly.
- Keep tests hermetic: no Dapr sidecars, running Memories server, Keycloak, Redis, provider credentials, tenant seed data, production secrets, network calls, or nested submodule initialization.
- Use the existing `ScaffoldContractTests` pattern for project reference checks. This prevents the main implementation risk: adding a Memories dependency to an adapter or contract project because it was convenient.
- If an adapter shell maps Memories exceptions to Folders result codes in this story, keep the mapping metadata-only and retryable/status-shaped. Do not throw for expected remote outage results unless the port contract explicitly models an unexpected infrastructure failure.

### Recent Git Intelligence

- `9c8cfc9 docs(epic-9): Record retrospective and version alignment` closed Epic 9 and records residual live-boot and policy follow-up actions.
- `2c598c4 feat(story-9.3): Apply Folders to Memories routing config` added the dormant `hexalith-folders -> folders-index` routing constants/helper and handoff.
- `c7a79ac feat: Update references in ScaffoldContractTests for Hexalith modules` shows dependency-direction tests are the accepted place to encode project reference changes.

### References

- `_bmad-output/planning-artifacts/epics.md` - Epic 10 and Story 10.1.
- `_bmad-output/planning-artifacts/architecture.md` - Memories integration implications, AppHost composition, Dapr access-control decision I-3, C9 sensitive metadata policy.
- `_bmad-output/planning-artifacts/memories-search-index-handoff-2026-06-23.md` - exact Epic 9 routing handoff and future Epic 10 event contract.
- `_bmad-output/implementation-artifacts/9-3-apply-folders-to-memories-routing-config-and-sync-artifacts.md` - previous story scope boundaries and no-policy-change precedent.
- `_bmad-output/implementation-artifacts/epic-9-retro-2026-06-23.md` - Epic 9 residual actions and routing activation target.
- `src/Hexalith.Folders.Workers/FoldersWorkersModule.cs` - worker DI/module registration.
- `src/Hexalith.Folders.Workers/Hexalith.Folders.Workers.csproj` - target project for Memories references.
- `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs` - dependency-direction and root build-policy gate.
- `Hexalith.Memories/src/Hexalith.Memories.Client.Rest/MemoriesClientServiceCollectionExtensions.cs` - `AddMemoriesClient(...)` registration API.
- `Hexalith.Memories/src/Hexalith.Memories.Contracts/V1/SearchIndexEntryChanged.cs` - future producer payload contract.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-06-23: Added red scaffold dependency-boundary expectation; `dotnet test tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj --filter ProjectReferencesFollowAllowedDependencyDirection -m:1` compiled but VSTest aborted because the sandbox denied local socket creation (`System.Net.Sockets.SocketException (13): Permission denied`).
- 2026-06-23: Added red worker semantic-indexing tests; `dotnet build tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj -m:1 -v minimal` failed as expected before implementation because `Hexalith.Folders.Workers.SemanticIndexing` did not exist.
- 2026-06-23: `dotnet build tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj -m:1 -v minimal` passed after implementation.
- 2026-06-23: `dotnet build tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj -m:1 -v minimal` passed after implementation.
- 2026-06-23: Exact `dotnet build Hexalith.Folders.slnx` failed in this sandbox with `Build FAILED` and `0 Warning(s), 0 Error(s)`; serialized `dotnet build Hexalith.Folders.slnx -m:1` passed.
- 2026-06-23: Exact required `dotnet test` commands failed during MSBuild node startup with `MSB1025` / `System.Net.Sockets.SocketException (13): Permission denied`; serialized no-restore retries compiled both test assemblies and then VSTest aborted on the same socket restriction while opening its local TCP listener.
- 2026-06-23 (Task 7, unrestricted env): `dotnet build Hexalith.Folders.slnx` succeeded with `0 Warning(s), 0 Error(s)` (parallel build; no socket restriction in this environment).
- 2026-06-23 (Task 7): First `dotnet test tests/Hexalith.Folders.Workers.Tests/...` run was RED 23/24 — `SemanticIndexingRequestShouldRejectBlankPublicBoundaryIdentifiers` expected ParamName `managedTenantId` but got `sourceAuthority`. Root cause was the test's own `CreateRequest` helper threading `managedTenantId` into the nested `SemanticIndexingSourceIdentity` `sourceAuthority` arg, so the inner validation fired before the `SemanticIndexingRequest` boundary check (C# evaluates ctor args before the outer ctor body). Production code was correct; fixed the helper to pass a fixed valid `sourceAuthority` so the assertion isolates the parameter under test.
- 2026-06-23 (Task 7): `dotnet test tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj` GREEN — Failed: 0, Passed: 24, Total: 24.
- 2026-06-23 (Task 7): `dotnet test tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj` GREEN — Failed: 0, Passed: 60, Total: 60.
- 2026-06-23 (Task 7): Confirmed no other project composes `AddFoldersTenantEventWorkers`; the only callers outside `Hexalith.Folders.Workers` live in `Hexalith.Folders.Workers.Tests` (existing `WorkersTenantEventTests` + new registration tests), all green, so the DI change introduces no cross-project regression. Full-solution build compiled every project with 0 errors.

### Completion Notes List

- Story context created by BMAD create-story workflow on 2026-06-23.
- Ultimate context engine analysis completed - comprehensive developer guide created.
- Implemented the worker-only Memories project references without inline package versions or solution changes.
- Added the `SemanticIndexing` worker area with a Folders-owned async port, request/source/content/policy/result/status records, validation, ordinal-safe source checks, and invariant status formatting.
- Added `MemoriesSemanticIndexingPort` as a non-producing adapter shell that depends on typed `MemoriesClient` but does not publish Dapr events, subscribe to file events, read files, or call Memories.
- Added `AddFoldersSemanticIndexingWorkers` and invoked it from `AddFoldersTenantEventWorkers` while preserving existing tenant-event worker behavior.
- Hardened scaffold dependency tests so only `Hexalith.Folders.Workers` may reference `Hexalith.Memories.Client.Rest` and `Hexalith.Memories.Contracts`.
- Added worker DI/port tests covering registration, provider validation, non-producing deferred result behavior, boundary validation, and parity of worker-local routing constants with Aspire source/index constants.
- Deferred production policy artifacts remain unchanged; no `memories-events` scope or `folders-workers -> memories` allow-rule was added.
- Task 7 completed in an unrestricted environment: full-solution build is clean (0 warnings/0 errors) and both required test projects pass (Workers.Tests 24/24, Testing.Tests 60/60). All 8 acceptance criteria are satisfied.
- Fixed one real defect surfaced by running the previously-unexecutable tests: the `SemanticIndexingWorkerRegistrationTests.CreateRequest` helper reused `managedTenantId` as the nested source authority, masking the request-level boundary validation. Decoupled the helper (fixed valid `sourceAuthority`) so the negative test isolates `managedTenantId`; the assertion `ParamName.ShouldBe("managedTenantId")` was preserved, not weakened. Production code was unchanged.

### File List

- `src/Hexalith.Folders.Workers/Hexalith.Folders.Workers.csproj`
- `src/Hexalith.Folders.Workers/FoldersWorkersModule.cs`
- `src/Hexalith.Folders.Workers/SemanticIndexing/FoldersSemanticIndexingDefaults.cs`
- `src/Hexalith.Folders.Workers/SemanticIndexing/ISemanticIndexingPort.cs`
- `src/Hexalith.Folders.Workers/SemanticIndexing/MemoriesSemanticIndexingPort.cs`
- `src/Hexalith.Folders.Workers/SemanticIndexing/SemanticIndexingContentDescriptor.cs`
- `src/Hexalith.Folders.Workers/SemanticIndexing/SemanticIndexingPolicyOutcome.cs`
- `src/Hexalith.Folders.Workers/SemanticIndexing/SemanticIndexingRequest.cs`
- `src/Hexalith.Folders.Workers/SemanticIndexing/SemanticIndexingResult.cs`
- `src/Hexalith.Folders.Workers/SemanticIndexing/SemanticIndexingSourceIdentity.cs`
- `src/Hexalith.Folders.Workers/SemanticIndexing/SemanticIndexingStatus.cs`
- `src/Hexalith.Folders.Workers/SemanticIndexing/SemanticIndexingValidation.cs`
- `tests/Hexalith.Folders.Workers.Tests/SemanticIndexingWorkerRegistrationTests.cs`
- `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs`
- `_bmad-output/implementation-artifacts/10-1-define-worker-side-semantic-indexing-port-and-memories-dependency.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

### Change Log

- 2026-06-23: Implemented worker-owned semantic-indexing foundation and kept story in progress because required test execution is blocked by sandbox socket permissions.
- 2026-06-23: Completed Task 7 in an unrestricted environment — clean full-solution build and both required test projects green (Workers.Tests 24/24, Testing.Tests 60/60). Fixed a test-helper defect in `SemanticIndexingWorkerRegistrationTests` (decoupled `sourceAuthority` from `managedTenantId`) surfaced by the first real test run. All ACs satisfied; story moved to review.
- 2026-06-23: Adversarial review (story-automator-review). Independently re-verified all 8 ACs: full-solution build clean (0W/0E), `Hexalith.Folders.Workers.Tests` 30/30, `Hexalith.Folders.Testing.Tests` 60/60; deferred Dapr policy artifacts confirmed unchanged with `memories` still deny-by-default (no `memories-events` scope, no `folders-workers -> memories` allow-rule). Fixed one LOW boundary-hardening gap in `SemanticIndexingSourceIdentity`: a Windows drive-letter path using forward slashes (e.g. `C:/Users/secret`) bypassed the backslash/leading-slash guards, weakening AC2's "must not expose raw filesystem paths as public identity." Added an `^[A-Za-z]:` drive-letter rejection plus a regression assertion in `SemanticIndexingSourceIdentityShouldRejectRawFilesystemIdentity`. Re-ran Workers.Tests 30/30 green. No CRITICAL/HIGH/MEDIUM issues found; story moved to done.

## Senior Developer Review (AI)

**Reviewer:** jpiquot · **Date:** 2026-06-23 · **Outcome:** Approve (auto-fix applied)

### Scope verified

Reviewed only application source per workflow policy (excluded `_bmad/`, `_bmad-output/`, IDE config). Git-changed source matched the story File List exactly (csproj, `FoldersWorkersModule.cs`, the 10 `SemanticIndexing/*` files, `SemanticIndexingWorkerRegistrationTests.cs`, `ScaffoldContractTests.cs`) — no undocumented or phantom file claims.

### Acceptance Criteria — all IMPLEMENTED

| AC | Verdict | Evidence |
| --- | --- | --- |
| 1 Workers-only Memories dependency | ✅ | `Hexalith.Folders.Workers.csproj` adds the two `$(HexalithMemoriesRoot)` references; `ScaffoldContractTests` allow-list + new forbidden-reference loop over 10 non-worker projects; clean solution build proves no leakage. |
| 2 Worker-owned port | ✅ | `ISemanticIndexingPort.IndexFileVersionAsync(request, ct)` + request/source/content/policy/result/status records carry tenant/org/folder/version/hash/source-URI/content/size-type/sensitivity-path-policy/correlation/task/idempotency. Raw-path identity blocked; reflection test proves no `Hexalith.Memories.*` DTO is exposed. |
| 3 Client registration encapsulated | ✅ | `AddFoldersSemanticIndexingWorkers` calls `services.AddMemoriesClient()` (no hand-rolled `HttpClient`) and `TryAddTransient<ISemanticIndexingPort, MemoriesSemanticIndexingPort>`; invoked from `AddFoldersTenantEventWorkers`. Adapter is non-producing (`Deferred`/`adapter_shell_not_producing`). |
| 4 Constants reuse Epic 9 values | ✅ | `FoldersSemanticIndexingDefaults` holds `hexalith-folders`/`folders-index`/`pubsub`/`memories-events`; parity test reads live `FoldersAspireModule` constants. |
| 5 Boundary tests fail closed | ✅ | Allowed-direction map updated + explicit forbidden-reference assertion for both Memories packages on every non-worker Folders project. |
| 6 Hermetic DI tests | ✅ | `ValidateOnBuild`/`ValidateScopes` providers; no Dapr/Memories/Keycloak/Redis/network; `TestContext.Current.CancellationToken` used. |
| 7 Production policy untouched | ✅ | `accesscontrol.yaml`/`pubsub.yaml`/`sidecar-config-bindings.yaml`/`dapr-policy-conformance.yaml` show no git diff; `memories=` scopes remain empty. |
| 8 Verification passes | ✅ | Re-ran here: build 0W/0E; Workers 30/30; Testing 60/60. |

### Tasks/Subtasks audit

All 7 tasks marked `[x]` confirmed genuinely done against the implementation — no false completions.

### Findings

- **LOW (fixed):** `SemanticIndexingSourceIdentity` accepted a forward-slash Windows drive path (`C:/...`) under a non-`file` scheme, a residual raw-filesystem-path leak relative to AC2. Added drive-letter rejection + regression test. Re-verified green.
- No CRITICAL, HIGH, or MEDIUM issues found. Code is metadata-safe (no raw content/path logging), uses ordinal/invariant formatting, validates public boundaries, and keeps Memories isolated to Workers.

### Action items

None — the only finding was auto-fixed in this pass.
