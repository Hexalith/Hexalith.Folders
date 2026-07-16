---
baseline_commit: 39aa1b4
---

# Story 10.2: Build the Folders-owned indexing bridge projection

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a Folders platform engineer,
I want a Folders-owned semantic-indexing bridge projection that tracks each authorized file version's Memories indexing state,
so that later producer and query-facade stories can report indexed, stale, skipped, failed, tombstoned, and reconciliation-required status without treating Memories as authoritative Folders state.

## Context & Scope Boundary

Epic 10 activates the dormant `hexalith-folders -> folders-index` routing delivered by Epic 9. Story 10.1 added the worker-only Memories dependencies, `ISemanticIndexingPort`, metadata-safe request/result records, worker-local routing constants, and worker DI registration. Story 10.2 must add the durable Folders-owned bridge projection/read model that later stories update and query.

In scope:

1. Define a Folders-owned bridge status model for file-version indexing state: `indexed`, `stale`, `skipped`, `failed`, `tombstoned`, and `reconciliation_required`.
2. Add projection/read-model records that map a stable file-version identity to Memories workflow/memory-unit evidence, source URI, content hash, trigger metadata, status, retryability, reason code, correlation ID, task ID, and projection freshness.
3. Persist the bridge read model through Hexalith.EventStore read-model facilities (`IReadModelStore` + `ReadModelWritePolicy`) or a compatible abstraction that can be backed by them. Do not create a new database, direct Dapr state-store wrapper, EF model, file store, or in-memory-only production path.
4. Apply durable Folders events deterministically to the bridge model: `WorkspaceFileMutationAccepted` for add/change/remove triggers, `WorkspaceCommitSucceeded` or `WorkspaceCommitFailed` for commit correlation/status evidence where applicable, and `FolderArchived` for tombstone behavior.
5. Provide a local read/write service for workers and future query code to ask for a single file version and, if needed for the next stories, folder-scoped pending/stale status summaries.
6. Register the bridge services through existing Folders/Workers DI without changing existing tenant-event behavior.
7. Add tests proving deterministic replay, idempotent duplicate delivery, stale-result protection, metadata-only payloads, tenant-prefixed read-model keys, and Workers-only Memories dependency isolation.

Out of scope:

1. Do not publish `SearchIndexEntryChanged` or `SearchIndexEntryRemoved` CloudEvents. That is Story 10.4.
2. Do not read file contents, call Memories, or replace `MemoriesSemanticIndexingPort`'s non-producing adapter shell with real indexing. That is Story 10.3.
3. Do not expose a public REST/SDK/CLI/MCP query facade over Memories or the bridge projection. That is Story 10.5 unless this story only adds an internal read-model interface.
4. Do not add `Hexalith.Memories.*` references to Contracts, core, Server, Client, CLI, MCP, UI, Testing, Aspire, or AppHost. Story 10.1's dependency boundary remains binding.
5. Do not change `deploy/dapr/production/accesscontrol.yaml`, `deploy/dapr/production/pubsub.yaml`, `deploy/dapr/production/sidecar-config-bindings.yaml`, or `tests/fixtures/dapr-policy-conformance.yaml`. Producer invoke/topic authorization is deferred.
6. Do not add raw paths, raw file content, diffs, provider payloads, tokens, repository names, branch names, or commit messages to bridge records, logs, traces, metrics, test failure messages, or story artifacts.

## Acceptance Criteria

1. **Folders-owned bridge contract exists without Memories DTO leakage.** A new semantic-indexing bridge area is added under Folders-owned source, preferably `src/Hexalith.Folders/Projections/SemanticIndexing/` plus `src/Hexalith.Folders/Queries/SemanticIndexing/` if a read interface/result shape is needed. It defines records/enums for file-version identity, bridge status, workflow/memory-unit evidence, source URI, content hash, status reason, retryability, projection freshness, and event watermarks. Public bridge types must not expose `Hexalith.Memories.*` types and must not live in `Hexalith.Folders.Contracts` unless a later public API story explicitly promotes them.

2. **Status vocabulary answers the Epic 10 states exactly.** The bridge model can answer `indexed`, `stale`, `skipped`, `failed`, `tombstoned`, and `reconciliation_required` per file version. The enum should include an `Unknown = 0` or equivalent fail-closed sentinel if it is serialized or persisted. Status code formatting uses ordinal/invariant, stable lowercase strings. `stale` means Folders has a newer durable file-version trigger than the known Memories evidence. `tombstoned` means the file/folder is no longer searchable. `reconciliation_required` means the bridge cannot prove whether Memories accepted the intended state and a retry/reconcile path is required. Do not blindly reuse Story 10.1's worker-port `SemanticIndexingStatus` (`Accepted`, `Deferred`, `Skipped`, `Failed`) as the persisted bridge vocabulary; either extend/map it deliberately or create a bridge-specific status type.

3. **Projection keys are tenant-prefixed and metadata-safe.** Durable read-model keys must start with `{managedTenantId}:` and include stable non-path identity such as folder id and file-version id/content hash. Current `WorkspaceFileMutationAccepted` events do not carry a literal `FileVersionId`; derive the file-version identity deterministically from metadata-safe fields such as managed tenant id, folder id, workspace id, operation id, content hash reference, and path metadata digest. Do not use raw path metadata, local filesystem paths, source text, repository target names, branch names, or commit messages in keys. If the bridge stores a source URI, use the stable non-file URI shape from `SemanticIndexingSourceIdentity` (`folders://...`) or a compatible resource-id form; never a `file://`, absolute, drive-letter, or backslash path.

4. **Durable read-model persistence uses EventStore read-model facilities.** The production bridge writer uses `IReadModelStore` and `ReadModelWritePolicy` from Hexalith.EventStore, or a thin Folders abstraction whose production implementation delegates to them. Read-model update transforms must be idempotent because they can be retried after optimistic-concurrency conflicts. An in-memory implementation is allowed for unit tests and local hermetic tests only, and it must be clearly registered as test/dev-only.

5. **Folders event application is deterministic and fail-closed.** Applying the same ordered Folders event stream from an empty bridge model produces equivalent state every time. Duplicate event delivery must be idempotent when the duplicate carries the same event identity/fingerprint; conflicting or out-of-order evidence must either be ignored by explicit ordering rules or fail loudly with metadata-only diagnostics. Envelope/payload tenant mismatches must be dropped or rejected without mutating another tenant's bridge state.

6. **File mutation and tombstone semantics are explicit.** Add/change `WorkspaceFileMutationAccepted` events create or update a file-version bridge entry as `stale` unless policy/size/type inputs require `skipped`. Remove operations and `FolderArchived` produce `tombstoned` state for affected file-version entries without deleting auditably useful bridge evidence. The bridge must not infer searchable truth from Memories; it records Folders trigger state and Memories evidence only.

7. **Index result updates cannot overwrite newer file versions.** Worker-facing bridge update methods can record future indexing outcomes (`indexed`, `failed`, `skipped`, `reconciliation_required`) with memory-unit id/workflow id, but an older result must not mark a newer content hash/file version as indexed. Matching must include tenant id, folder id, file version id or stable aggregate id, and content hash/source identity.

8. **DI registration is additive and preserves existing workers.** Add a registration method such as `AddFoldersSemanticIndexingBridge` in the core Folders service extensions and call it from `AddFoldersSemanticIndexingWorkers` or the narrowest worker registration path. Existing `AddFoldersTenantEventWorkers`, tenant-event projection handlers, repository provisioning workers, and Story 10.1 semantic-indexing port registration must continue to resolve with `ValidateOnBuild` and `ValidateScopes`.

9. **No production policy or producer behavior changes.** The story must leave Dapr production access-control and pub/sub conformance artifacts unchanged. It must not add a `memories-events` publish scope, a `folders-workers -> memories` allow rule, a new Dapr subscription, or a real `SearchIndexEntryChanged` producer.

10. **Tests cover bridge behavior and boundaries.** Add focused tests under `tests/Hexalith.Folders.Tests/Projections/SemanticIndexing/` for pure replay/status behavior, `tests/Hexalith.Folders.Workers.Tests/` for DI/worker registration, and `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs` only if dependency-boundary assertions need to be extended. Tests must prove metadata-only serialization, tenant-prefixed keys, duplicate/out-of-order handling, stale-result protection, status vocabulary coverage, and no non-worker Memories references.

11. **Verification passes.** `dotnet build Hexalith.Folders.slnx` succeeds. The narrowed tests pass: `dotnet test tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj`, `dotnet test tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj`, and `dotnet test tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj`. If exact commands are blocked by local environment state, record the blocker and do not mark implementation complete.

## Tasks / Subtasks

- [x] Task 1 - Define the bridge model and status vocabulary (AC: 1, 2, 3)
  - [x] Add a semantic-indexing bridge folder under `src/Hexalith.Folders/Projections/SemanticIndexing/` or another Folders-owned, Memories-free location.
  - [x] Add records for file-version identity, bridge entry/snapshot, Memories evidence fields, projection freshness/watermark, and retry/status reason.
  - [x] Add a status enum/value object with `indexed`, `stale`, `skipped`, `failed`, `tombstoned`, `reconciliation_required`, and a fail-closed unknown sentinel when persisted/serialized.
  - [x] Validate public-boundary string inputs with `ArgumentException.ThrowIfNullOrWhiteSpace`; use ordinal comparisons and invariant formatting.

- [x] Task 2 - Add deterministic event application (AC: 5, 6)
  - [x] Apply `WorkspaceFileMutationAccepted` add/change events as `stale` entries keyed by tenant/folder/file version/content hash/source identity.
  - [x] Apply remove events as `tombstoned` entries without storing raw path metadata.
  - [x] Apply `WorkspaceCommitSucceeded` / `WorkspaceCommitFailed` only as correlation/status evidence needed for future producer ordering; do not treat a commit as proof that Memories indexed anything.
  - [x] Apply `FolderArchived` as folder-scoped tombstone behavior for known entries.
  - [x] Drop or reject malformed event envelopes, tenant mismatches, and unsupported event types consistently with existing projection fail-closed patterns.

- [x] Task 3 - Persist through EventStore read-model store (AC: 3, 4, 7)
  - [x] Add a bridge writer/read-model service that delegates production writes to `IReadModelStore` + `ReadModelWritePolicy`.
  - [x] Use tenant-prefixed state keys, for example `{tenantId}:semantic-indexing:file-version:{folderId}:{fileVersionId}` and any folder-summary index keys with the same prefix.
  - [x] Ensure update transforms are idempotent under optimistic-concurrency retry and duplicate delivery.
  - [x] Add stale-result protection so old indexing outcomes cannot overwrite newer file-version/content-hash state.

- [x] Task 4 - Register bridge services without changing worker behavior (AC: 8, 9)
  - [x] Add a registration method such as `AddFoldersSemanticIndexingBridge` in `FoldersServiceCollectionExtensions`.
  - [x] Register `IReadModelStore` through `AddEventStoreReadModelStore` only where a Dapr client is already present or where the composition explicitly registers one.
  - [x] Call the bridge registration from `FoldersWorkersModule.AddFoldersSemanticIndexingWorkers`.
  - [x] Preserve Story 10.1 behavior: `ISemanticIndexingPort` still resolves and remains a non-producing adapter shell until Story 10.3.
  - [x] Confirm no Dapr production policy, pub/sub fixture, or producer wiring changes.

- [x] Task 5 - Add bridge tests (AC: 2, 3, 5, 6, 7, 10)
  - [x] Add pure projection tests for deterministic replay from `WorkspaceFileMutationAccepted`, removal, commit evidence, and `FolderArchived`.
  - [x] Add duplicate and out-of-order tests covering idempotent same-event delivery and metadata-only fail-loud behavior for unsafe conflicts.
  - [x] Add stale-result tests where an older workflow/memory-unit outcome cannot mark a newer content hash indexed.
  - [x] Add serialization/sentinel tests proving bridge entries do not include raw paths, file contents, diffs, provider payloads, branch names, commit messages, tokens, or local absolute paths.
  - [x] Add key-shape tests proving every durable key starts with `{managedTenantId}:`.

- [x] Task 6 - Add DI and dependency-boundary tests (AC: 1, 8, 9, 10)
  - [x] Extend `SemanticIndexingWorkerRegistrationTests` or add a new worker test proving bridge services resolve through `AddFoldersSemanticIndexingWorkers` with validation enabled.
  - [x] Keep `ScaffoldContractTests` green: only `Hexalith.Folders.Workers` may reference `Hexalith.Memories.Client.Rest` and `Hexalith.Memories.Contracts`; AppHost keeps only the existing `Hexalith.Memories.Aspire` topology reference.
  - [x] Add an assertion or diff check proving production Dapr policy/conformance files remain unchanged if the implementation touches nearby infrastructure.

- [x] Task 7 - Build and test (AC: 11)
  - [x] Run `dotnet build Hexalith.Folders.slnx`.
  - [x] Run `dotnet test tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj`.
  - [x] Run `dotnet test tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj`.
  - [x] Run `dotnet test tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj`.

## Dev Notes

### Source facts

- Epic 10 goal: authorized file changes are asynchronously indexed into Memories through a worker-side producer and a Folders-owned bridge projection, activating Epic 9 routing and later exposing an authorized, security-trimmed query facade. [Source: `_bmad-output/planning-artifacts/epics.md` "Epic 10: Folders Worker-Side Semantic-Indexing Producer And Bridge Projection"]
- Story 10.2 seed AC: durable Folders events trigger a bridge projection that tracks `file version -> Memories workflow/memory unit/status` and answers indexed / stale / skipped / failed / tombstoned / reconciliation-required per file version. [Source: `_bmad-output/planning-artifacts/epics.md` "Story 10.2"]
- Architecture treats `Hexalith.Memories` as a separate Dapr-enabled derived semantic index, not an authoritative Folders datastore. File content may be read by workers after authorization and sent to Memories, but file content must not be embedded in Folders events, projections, logs, traces, metrics, audit records, or error responses. [Source: `_bmad-output/planning-artifacts/architecture.md` "Hexalith.Memories integration implications"]
- Architecture requires stable source URIs and idempotency keys based on tenant/folder/file-version/content-hash metadata; raw path metadata is forbidden unless C9 explicitly allows exposure. [Source: `_bmad-output/planning-artifacts/architecture.md` "Hexalith.Memories integration implications"]
- C4 input limits are PM-approved and binding. The bridge should preserve explicit skipped/too-large status rather than trying to index oversized content silently. [Source: `docs/exit-criteria/c4-input-limits.md`]
- C9 sensitive metadata policy classifies paths, repository names, branch names, and commit messages as tenant-sensitive by default. The bridge projection must store digests/classifications/evidence, not raw sensitive metadata. [Source: `_bmad-output/planning-artifacts/epics.md` AR-AUDIT-03; `_bmad-output/planning-artifacts/prd.md` "Observability, Auditability, and Replay"]
- Dapr pub/sub current docs identify v1.18 as latest and keep CloudEvents wrapping, at-least-once delivery, and topic scoping as core pub/sub behavior. That means future producer/result handling must be idempotent and duplicate-safe. [Source: `https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-overview/`]

### Current code state to preserve

- Story 10.1 implemented `src/Hexalith.Folders.Workers/SemanticIndexing/` with `ISemanticIndexingPort`, `SemanticIndexingRequest`, `SemanticIndexingResult`, `SemanticIndexingSourceIdentity`, `SemanticIndexingContentDescriptor`, `SemanticIndexingPolicyOutcome`, `FoldersSemanticIndexingDefaults`, and `MemoriesSemanticIndexingPort`.
- `MemoriesSemanticIndexingPort` currently validates the request/cancellation token and returns `SemanticIndexingStatus.Deferred` with reason `adapter_shell_not_producing`. Do not make it call Memories in this story.
- `FoldersWorkersModule.AddFoldersSemanticIndexingWorkers` currently calls `services.AddMemoriesClient()` and registers `ISemanticIndexingPort`. Add bridge registration here without removing the existing registration.
- `FoldersWorkersModule.AddFoldersTenantEventWorkers` also registers Dapr client, tenant-access projection, Tenants event subscription options, tenant event handlers, and repository provisioning workers. Preserve all of that behavior.
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs` currently maps `/project` to a 501 `projection_not_implemented` result. If this story wires bridge projection through the server `/project` endpoint, replace that deliberately and add server tests. If not, leave the endpoint unchanged and keep bridge projection as worker/internal read-model code.
- Existing pure projection patterns live in `src/Hexalith.Folders/Projections/FolderList/FolderListProjection.cs` and `src/Hexalith.Folders/Projections/FolderAccess/FolderAccessProjection.cs`. Reuse their deterministic ordering, tenant-mismatch guard, ordinal keys, and metadata-only fail-loud style.
- Existing read-model interfaces and in-memory doubles live under `src/Hexalith.Folders/Queries/Folders/` and are registered from `FoldersServiceCollectionExtensions`. They are good test patterns, but production bridge durability must use the EventStore read-model store rather than creating another production-only in-memory store.
- `Hexalith.EventStore.Client.Projections.IReadModelStore` and `ReadModelWritePolicy` provide the approved persisted read-model API and optimistic-concurrency retry loop. `Hexalith.EventStore.Client.Registration.AddEventStoreReadModelStore()` registers the Dapr-backed store when `DaprClient` is registered.
- Tenants' `TenantProjectionHandler` is the current cross-module example of persisted read models through `IReadModelStore` + `ReadModelWritePolicy`, including per-aggregate and singleton index keys.

### Event and identity guidance

- Candidate Folders trigger events:
  - `WorkspaceFileMutationAccepted` carries managed tenant id, organization id, folder id, workspace id, operation id, file operation kind, transport operation, path policy class, path metadata digest, content hash reference, byte/media/type metadata, actor, correlation id, task id, idempotency key/fingerprint, and occurred-at timestamp.
  - `WorkspaceCommitSucceeded` / `WorkspaceCommitFailed` carry commit operation evidence but not file content or raw changed paths.
  - `FolderArchived` carries tenant/folder/archive metadata and should tombstone known bridge entries.
- Because the current file mutation event has operation/content/path digest metadata rather than a literal file-version id, the implementation must introduce one stable derived id and test that the derivation is deterministic, tenant-scoped, content-sensitive, and raw-path-free.
- Use stable identity based on managed tenant id, organization id, folder id, workspace id/task id when needed, operation id, file version id or source aggregate id, content hash reference, and the stable source URI/resource id. Never derive identity from raw path text.
- The bridge status is Folders truth about indexing state, not Memories truth about file truth. Memories can tell Folders that a memory unit/workflow is indexed or failed; Folders still owns authorization, file lifecycle, tombstone state, audit, and query hydration.

### Architecture and dependency guardrails

- Repository configuration is authoritative when planning prose drifts. Current pins are `global.json` SDK `10.0.300`, Dapr packages `1.18.4`, Aspire `13.4.6`, CommunityToolkit Aspire Dapr `13.4.0-preview.1.260602-0230`, xUnit v3 `3.2.2`, Shouldly `4.3.0`, and NSubstitute `5.3.0`. [Source: `global.json`, `Directory.Packages.props`]
- Keep package versions centralized in `Directory.Packages.props`; do not add inline `Version` attributes to `.csproj` files.
- Use root-level sibling module references only. Do not initialize nested submodules and do not add recursive submodule setup instructions.
- Public async read/write paths must accept and propagate `CancellationToken`; library/worker code should use `ConfigureAwait(false)` where nearby code does.
- Logs/traces/metrics/test diagnostics must be metadata-only. If a projection conflict message is needed, include tenant id, folder id, file-version id, status code, event type, correlation id, and reason code; exclude raw paths/content/provider payloads.
- If a status enum is serialized, prefer an unknown/default value and stable string formatting. This mirrors Hexalith fail-closed enum practices.

### Testing guidance

- Use xUnit v3 + Shouldly.
- Use `TestContext.Current.CancellationToken` in async tests.
- Keep tests hermetic: no Dapr sidecars, running Memories server, Keycloak, Redis, provider credentials, tenant seed data, production secrets, network calls, or nested submodule initialization.
- Unit-test projection application directly before testing DI. The bridge projection should be deterministic without service provider setup.
- Add a lightweight fake/in-memory `IReadModelStore` or use existing EventStore testing helpers if available; do not require a Dapr sidecar for unit tests.
- Extend dependency-boundary tests rather than adding an ad hoc scanner if the implementation changes project references.

### Recent Git Intelligence

- `39aa1b4 feat(story-10.1): Add worker semantic indexing port` added the worker-only Memories references, semantic-indexing port, request/result records, DI registration, dependency-boundary tests, and worker tests. Preserve its boundaries.
- `9c8cfc9 docs(epic-9): Record retrospective and version alignment` records the Epic 9 residual actions, including the carry-forward production policy work for Epic 10.
- `2c598c4 feat(story-9.3): Apply Folders to Memories routing config` added the dormant AppHost routing constants/helper and handoff.
- `c7a79ac feat: Update references in ScaffoldContractTests for Hexalith modules` shows dependency-direction tests are the accepted place to encode project reference changes.

### References

- `_bmad-output/planning-artifacts/epics.md` - Epic 10 and Story 10.2.
- `_bmad-output/planning-artifacts/architecture.md` - Memories integration implications, AppHost composition, Dapr access-control decision I-3, sensitive metadata rules.
- `_bmad-output/planning-artifacts/memories-search-index-handoff-2026-06-23.md` - exact Epic 9 routing handoff and future Epic 10 event contract.
- `_bmad-output/planning-artifacts/research/technical-hexalith-memories-semantic-indexing-rag-research-2026-05-11.md` - research on Memories ingestion/search, 1 MB inline ingestion constraint, hybrid retrieval, and worker-side port pattern.
- `docs/exit-criteria/c4-input-limits.md` - approved C4 bounds and metadata-only audit visibility.
- `_bmad-output/implementation-artifacts/10-1-define-worker-side-semantic-indexing-port-and-memories-dependency.md` - previous story scope, implemented files, test evidence, and review learning.
- `src/Hexalith.Folders.Workers/SemanticIndexing/` - current worker-owned semantic-indexing port and adapter shell.
- `src/Hexalith.Folders.Workers/FoldersWorkersModule.cs` - worker DI/module registration.
- `src/Hexalith.Folders/Projections/FolderList/FolderListProjection.cs` - deterministic metadata-only projection pattern.
- `src/Hexalith.Folders/Queries/Folders/InMemoryWorkspaceStatusReadModel.cs` - local read-model interface/double pattern.
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs` - current `/project` endpoint state.
- `Hexalith.EventStore/src/Hexalith.EventStore.Client/Projections/IReadModelStore.cs` - persisted read-model API.
- `Hexalith.EventStore/src/Hexalith.EventStore.Client/Projections/ReadModelWritePolicy.cs` - optimistic-concurrency write policy.
- `Hexalith.Tenants/src/Hexalith.Tenants/Projections/TenantProjectionHandler.cs` - platform persisted read-model usage example.
- `Hexalith.Memories/src/Hexalith.Memories.Contracts/V1/SearchIndexEntryChanged.cs` - future producer payload contract.
- `Hexalith.Memories/src/Hexalith.Memories.Contracts/V1/SearchIndexEntryRemoved.cs` - future producer tombstone payload contract.
- `https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-overview/` - current Dapr pub/sub facts for CloudEvents, at-least-once delivery, and topic scoping.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-06-23: Create-story primary and first fallback stalled; direct Codex fallback created the story artifact and sprint-status moved to ready-for-dev.
- 2026-06-23: Dev primary and first fallback stalled after discovery/status update; implementation completed directly from the generated story context.
- 2026-06-23: First core build failed on a generated `SemanticIndexingProjectionFreshness` constructor syntax issue; fixed before implementation tests.
- 2026-06-23: Moved the production EventStore-backed bridge adapter to Workers and kept core registration in-memory to preserve existing dependency-direction tests.
- 2026-06-23: QA automation added bridge-store persistence tests and extra projection guardrails; a missing test helper from the stalled child session was repaired.
- 2026-06-23: `dotnet build Hexalith.Folders.slnx` succeeded with 0 warnings and 0 errors.
- 2026-06-23: `dotnet test tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj` succeeded: 1327/1327.
- 2026-06-23: Code review fixed remove-event tombstone semantics so no-content-hash remove events tombstone known file-version entries for the same metadata-safe path digest.
- 2026-06-23: `dotnet test tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj` succeeded: 36/36.
- 2026-06-23: `dotnet test tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj` succeeded: 60/60.

### Completion Notes List

- Story context created by BMAD create-story workflow on 2026-06-23.
- Ultimate context engine analysis completed - comprehensive developer guide created.
- Added a Folders-owned semantic-indexing bridge model/projection with stable status codes, tenant-prefixed read-model keys, derived metadata-only file-version identity, Memories workflow/memory-unit evidence, commit evidence, and freshness watermarks.
- Added an in-memory bridge read/write store for core/local hermetic use and an EventStore `IReadModelStore`/`ReadModelWritePolicy`-backed worker adapter with a tenant-prefixed folder index for later folder-scoped commit/archive updates.
- Registered the bridge through `AddFoldersSemanticIndexingBridge` and replaced it with the EventStore-backed implementation from `AddFoldersSemanticIndexingWorkers` without changing Story 10.1's non-producing `ISemanticIndexingPort` shell.
- Added projection/store tests for deterministic identity, status vocabulary, duplicate delivery, tenant mismatch, tombstones, commit metadata, stale-result protection, and metadata-only serialization.
- Extended worker registration tests to prove the EventStore-backed bridge read model/writer and existing Memories client/port registrations resolve under `ValidateOnBuild`/`ValidateScopes`.
- Review fix added metadata-safe path digest tracking to bridge identities so remove events without content hashes tombstone known file-version entries instead of leaving stale searchable bridge state behind.
- No Dapr production policy, pub/sub, or producer files were changed; CloudEvents publishing and real Memories calls remain deferred to later Epic 10 stories.

### File List

- `_bmad-output/implementation-artifacts/10-2-build-folders-owned-indexing-bridge-projection.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/tests/10-2-test-summary.md`
- `_bmad-output/implementation-artifacts/tests/test-summary.md`
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs`
- `src/Hexalith.Folders/Projections/SemanticIndexing/InMemorySemanticIndexingBridgeStore.cs`
- `src/Hexalith.Folders/Projections/SemanticIndexing/ISemanticIndexingBridgeReadModel.cs`
- `src/Hexalith.Folders/Projections/SemanticIndexing/ISemanticIndexingBridgeWriter.cs`
- `src/Hexalith.Folders/Projections/SemanticIndexing/SemanticIndexingBridgeEntry.cs`
- `src/Hexalith.Folders/Projections/SemanticIndexing/SemanticIndexingBridgeKeys.cs`
- `src/Hexalith.Folders/Projections/SemanticIndexing/SemanticIndexingBridgeProjection.cs`
- `src/Hexalith.Folders/Projections/SemanticIndexing/SemanticIndexingBridgeStatus.cs`
- `src/Hexalith.Folders/Projections/SemanticIndexing/SemanticIndexingBridgeValidation.cs`
- `src/Hexalith.Folders/Projections/SemanticIndexing/SemanticIndexingCommitEvidence.cs`
- `src/Hexalith.Folders/Projections/SemanticIndexing/SemanticIndexingEvidence.cs`
- `src/Hexalith.Folders/Projections/SemanticIndexing/SemanticIndexingFileVersionIdentity.cs`
- `src/Hexalith.Folders/Projections/SemanticIndexing/SemanticIndexingProjectionFreshness.cs`
- `src/Hexalith.Folders/Projections/SemanticIndexing/SemanticIndexingResultUpdate.cs`
- `src/Hexalith.Folders.Workers/FoldersWorkersModule.cs`
- `src/Hexalith.Folders.Workers/SemanticIndexing/EventStoreSemanticIndexingBridgeStore.cs`
- `tests/Hexalith.Folders.Tests/Projections/SemanticIndexing/SemanticIndexingBridgeProjectionTests.cs`
- `tests/Hexalith.Folders.Workers.Tests/EventStoreSemanticIndexingBridgeStoreTests.cs`
- `tests/Hexalith.Folders.Workers.Tests/SemanticIndexingWorkerRegistrationTests.cs`

### Change Log

- 2026-06-23: Implemented Story 10.2 semantic-indexing bridge projection/read model, worker EventStore persistence adapter, DI wiring, focused projection/DI tests, and moved story to review after clean build/tests.
- 2026-06-23: Code review fixed remove-event tombstone semantics for existing file versions, added regression coverage, verified exact build/tests, and marked story done.

## Senior Developer Review (AI)

### Review Summary

- Outcome: Approved after auto-fix.
- Git vs story file-list discrepancies: resolved by adding the Story 10.2 QA summary artifacts to the File List; unrelated Epic 8 worktree changes were excluded from this review.
- Critical issues remaining: 0.
- High issues remaining: 0.
- Medium issues remaining: 0.

### Finding Fixed

- High - Remove events with no content hash could leave the existing indexed file-version entry stale. The original bridge derived a separate `no-content-hash` remove identity and did not have path-digest awareness for tombstoning the known added/changed entry. Fixed by storing `PathMetadataDigest` on `SemanticIndexingFileVersionIdentity`, tombstoning matching tenant/organization/folder/workspace/path-digest entries in the pure projection, applying the same behavior in the EventStore-backed worker store, and adding pure plus persisted regression tests.

### Verification

- `dotnet build Hexalith.Folders.slnx` - passed, 0 warnings, 0 errors.
- `dotnet test tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj` - passed 1327/1327.
- `dotnet test tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj` - passed 36/36.
- `dotnet test tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj` - passed 60/60.
