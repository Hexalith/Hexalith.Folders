---
baseline_commit: 2c3f116d8b153dbd74c5e6879c8e0cd94931d8b7
---

# Story 3.6: Create a new repository-backed folder

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an authorized actor,
I want to create a new provider repository for an existing logical folder after readiness passes,
so that a tenant folder can become repository-backed through a controlled provisioning path.

## Terms

- `CreateRepositoryBackedFolder` means the mutating repository-creation path for Story 3.6. It is distinct from Story 3.7 `BindRepository`, which binds an existing provider repository and must not share repository-creation failure paths.
- Existing logical folder means a folder already created by Story 2.3 and still active. Story 3.6 must not silently re-implement `CreateFolder` or create a second logical-folder creation path.
- Green provider readiness means Story 3.5 readiness evidence for `repository_creation` is `ready`, fresh enough for a mutating operation, scoped to the same tenant, organization, provider binding, authorization evidence, and correlation/task context where applicable.
- Repository provisioning request means a metadata-only durable request to create a provider repository and attach the resulting repository binding to the folder. It is not raw provider payload storage and it is not direct filesystem workspace preparation.
- Repository binding identity means an opaque tenant-scoped `repositoryBindingId` associated with a `folderId` and `providerBindingRef`; it must not expose owner, repository name, clone URL, installation ID, token material, or unauthorized provider existence.
- C6 state means the architecture workspace state-transition matrix: `RepositoryBindingRequested`, `RepositoryBound`, `RepositoryBindingFailed`, and `ProviderOutcomeUnknown` drive `requested`, `preparing`, `failed`, `unknown_provider_outcome`, or `reconciliation_required` evidence. Story 4.2 owns actual workspace preparation.

## Acceptance Criteria

1. Given an authenticated actor has authoritative tenant evidence, folder access, provider-binding-use authority, and a fresh green provider-readiness result for `repository_creation`, when `CreateRepositoryBackedFolder` is accepted for an existing active folder, then the system records a metadata-only repository provisioning request without reading credentials, constructing a provider adapter, resolving repository targets, touching workspaces, or emitting audit/projection identifiers before authorization succeeds.
2. Given the current Contract Spine already contains `CreateRepositoryBackedFolder`, when implementation starts, then the developer must reconcile the source conflict explicitly: Epic 3.6 and the sprint split require an existing logical folder, while the current OpenAPI request shape contains `folderMetadata` and no explicit `folderId`. If the spine cannot address the existing folder, update the OpenAPI contract, contract tests, parity oracle, and generated client/idempotency helpers together; do not silently implement a new folder-create flow.
3. Given the folder does not exist, is archived, is in a repository-binding mutation already, or cannot be observed safely after authorization, when `CreateRepositoryBackedFolder` is attempted, then the operation returns the existing safe not-found, state-transition, read-model-unavailable, or folder-ACL denial family without exposing whether provider binding, credential reference, repository target, branch policy, or readiness evidence exists.
4. Given readiness is missing, stale, unavailable, failed, degraded for required repository-creation capability, unsupported, reconciliation-required, or tied to a different tenant/provider binding/authorization snapshot, when the command is evaluated, then no folder state is mutated and no provider repository creation is attempted; the caller receives a stable canonical category such as `provider_readiness_failed`, `unsupported_provider_capability`, `reconciliation_required`, `unknown_provider_outcome`, or `read_model_unavailable` as appropriate.
5. Given the command is accepted, when folder state is updated, then the folder records repository-binding metadata only: provider binding reference, repository binding ID, repository profile reference if retained by the contract, branch/ref policy reference, correlation ID, task ID, idempotency key/fingerprint, requested state, and occurred-at timestamp; events, projections, diagnostics, logs, and response bodies exclude raw provider payloads, repository URLs, owner/repository labels, credential values, file contents, diffs, and unauthorized resource names.
6. Given the same idempotency key and equivalent `CreateRepositoryBackedFolder` payload are retried, when the folder stream already contains the equivalent accepted request, then the operation returns the same logical accepted/replay result without appending another request event and without issuing a duplicate provider repository request; given the same key is reused with different folder, provider binding, repository profile, branch/ref policy, credential scope, tenant, or folder metadata equivalence fields, then the result is `idempotency_conflict` and state is unchanged.
7. Given a repository provisioning worker or process manager consumes the requested event, when provider repository creation succeeds or the provider proves an equivalent existing result for this idempotent request, then it records the C6-approved success event (`RepositoryBound` or the implementation's explicitly mapped equivalent) with safe binding evidence and moves observable folder binding status from `requested` toward `preparing`; it must not perform workspace preparation, file operations, commits, or branch/ref policy mutation in this story.
8. Given provider repository creation returns a known failure, when the worker maps the result, then it records `RepositoryBindingFailed` with a stable provider/repository category such as validation failure, provider authentication required, provider permission insufficient, provider conflict, repository conflict, provider rate limited, provider unavailable, known provider failure, or unsupported capability; raw GitHub/Forgejo response bodies and adapter exception text are not product semantics.
9. Given provider repository creation has an ambiguous mutating outcome, timeout after send, cancellation after send, dropped connection, malformed success response, or any result that may have partially applied upstream, when the outcome cannot be proven, then the workflow records `ProviderOutcomeUnknown` or `reconciliation_required` metadata and does not silently retry, issue a second mutating provider call, fabricate success, or emit a duplicate binding.
10. Given GitHub and Forgejo capability differences exist, when repository creation is implemented through the provider port, then differences remain provider capability/result metadata; product workflows must not treat Forgejo as a GitHub base-URL swap or branch public semantics on provider-specific endpoint paths.
11. Given folder lifecycle and list projections are replayed, when repository-binding events are applied, then authorized status reads can expose `requested`, `bound`/`ready`, `failed`, `unknown_provider_outcome`, or `reconciliation_required` with tenant-scoped `repositoryBindingId` and `providerBindingRef`, while unauthorized callers receive safe denial and no resource existence signal.
12. Given implementation touches REST or SDK surfaces, when `POST /api/v1/folders/repository-backed` accepts the command, then it requires `Idempotency-Key`, `X-Correlation-Id`, and `X-Hexalith-Task-Id`, validates them before downstream use, submits through the canonical EventStore/gateway path or an equivalent repository-backed command gate, and returns `202 Accepted` with safe command metadata only.
13. Given unit, contract, server, worker, and safety tests run in a developer machine or CI PR gate, then Story 3.6 validation is hermetic: no live GitHub, live Forgejo, provider credentials, tenant seed data, Aspire, Dapr sidecars, Redis, Keycloak, Docker, network access, or nested submodule initialization is required.

## Tasks / Subtasks

- [x] Reconcile the public contract and generated artifacts before runtime work. (AC: 2, 6, 12)
  - [x] Review `CreateRepositoryBackedFolder` in `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`, `docs/contract/idempotency-and-parity-rules.md`, `tests/fixtures/parity-contract.yaml`, and `TenantFolderProviderContractGroupTests`.
  - [x] Decide from source artifacts whether the request must carry an existing `folderId` path/body field. If yes, update the Contract Spine, parity fixture, previous-spine baseline where required, generated SDK, and idempotency helper outputs through the established generation path.
  - [x] Keep the operation mutating with required `Idempotency-Key`, `X-Correlation-Id`, and `X-Hexalith-Task-Id`.
  - [x] Keep idempotency equivalence lexicographically ordered and scoped to safe fields only. Do not include raw JSON, provider payloads, raw repository names, or credential values in the fingerprint.
  - [x] Do not hand-edit generated SDK files under `src/Hexalith.Folders.Client/Generated/*`; regenerate or update generator inputs.

- [x] Add repository-backed folder command and C6 event model on the folder aggregate. (AC: 1, 3, 5, 6, 11)
  - [x] Add a `CreateRepositoryBackedFolder` domain command or approved local equivalent under `src/Hexalith.Folders/Aggregates/Folder/`, keeping one public type per file.
  - [x] Extend `FolderRepositoryBindingState` and `FolderState` with requested/bound/failed/unknown/reconciliation metadata needed by the current C6 and `RepositoryBinding` contract states.
  - [x] Add folder events for repository binding request and provider outcomes using the C6/Contract Spine event vocabulary (`RepositoryBindingRequested`, `RepositoryBound`, `RepositoryBindingFailed`, `ProviderOutcomeUnknown`) or a single explicitly mapped local vocabulary; do not create duplicate event names for the same transition.
  - [x] Update `FolderStateApply` and `FolderListProjection` so new events replay deterministically and fail loudly on unsupported future events.
  - [x] Reuse `FolderActiveMutationGuard.Evaluate(..., FolderActiveMutationCategory.RepositoryBinding)` so archived/uncreated folders reject before events.
  - [x] Keep aggregate handlers pure: no provider calls, credential resolution, Dapr, HTTP, filesystem, Git, logs, random IDs, or wall-clock reads inside aggregate decision logic.

- [x] Implement authorization, readiness, and idempotency gating before observation. (AC: 1, 3, 4, 6)
  - [x] Add a narrow repository-backed creation gate/service that validates tenant access, claim-transform evidence, folder ACL, active folder state, readiness, idempotency, and append outcome in that order.
  - [x] Treat payload tenant/folder/provider values as comparison inputs only. Authoritative tenant and principal come from authentication/EventStore evidence.
  - [x] Require fresh authorization for this mutating operation; bounded-stale readiness/read-model evidence must fail closed before provider work.
  - [x] Consume Story 3.5 readiness evidence or validation service result for `repository_creation`; do not call GitHub/Forgejo adapters directly from REST endpoints or aggregate handlers.
  - [x] Add no-touch fakes proving denied, stale, unavailable, missing, malformed, mismatched, replay-conflicting, and archived-folder paths make zero calls to provider binding readers, credential resolvers, provider resolvers, repository creation APIs, workspace stores, audit writers, metrics/log enrichment, or diagnostics.
  - [x] Keep idempotency checks tenant/folder scoped and ensure equivalent replay cannot schedule another provider repository creation.

- [x] Extend the provider port only as needed for repository creation. (AC: 7, 8, 9, 10)
  - [x] Add the narrow repository-creation request/result model to `src/Hexalith.Folders/Providers/Abstractions/` only if no approved operation already exists; do not create a second `IGitProvider` or a provider-specific product API.
  - [x] Reuse `ProviderOperationCatalog.RepositoryCreation`, `ProviderFailureCategory`, retryability helpers, safe target evidence, and authorization snapshots from Stories 3.2 through 3.5.
  - [x] Implement GitHub and Forgejo repository-creation behavior behind their existing provider boundaries and fakeable seams; keep Octokit and Forgejo endpoint DTOs out of provider abstractions, Contracts, Client, Server transport DTOs, CLI, MCP, UI, aggregates, and Workers.
  - [x] Map success, equivalent-existing result, validation failure, missing permission, credential/auth failure, repository/name conflict, branch/ref conflict where relevant, rate limit, provider unavailable, known provider failure, and unsupported capability to canonical provider categories.
  - [x] Map ambiguous mutating outcomes to `unknown_provider_outcome`/`reconciliation_required` and never retry automatically without explicit safe idempotency proof from the provider operation.
  - [x] Preserve provider-specific capability differences as metadata; do not flatten Forgejo version/snapshot evidence or GitHub API-version/permission evidence into public DTO semantics.

- [x] Add repository provisioning process-manager behavior if runtime provisioning is in this slice. (AC: 7, 8, 9, 13)
  - [x] Place process-manager code under `src/Hexalith.Folders.Workers/RepositoryProvisioning/` or the established worker area, not in aggregates or REST endpoint handlers.
  - [x] React idempotently to repository-binding requested events by causation/correlation/idempotency identity.
  - [x] Resolve credentials only after tenant/provider/folder authorization and provider binding have been proven for the worker context.
  - [x] Record C6 outcome events back to the folder stream through the approved command/event path.
  - [x] Do not implement `WorkspacePreparationWorkflow`, file changes, commits, cleanup, repair automation, or live drift checks in this story.

- [x] Wire the REST route and domain/gateway plumbing. (AC: 2, 6, 12)
  - [x] Add or extend a focused endpoint for `POST /api/v1/folders/repository-backed`; keep request parsing strict with unknown fields rejected.
  - [x] Validate `Idempotency-Key`, `X-Correlation-Id`, `X-Hexalith-Task-Id`, folder identity, and request schema version before any downstream use.
  - [x] Authenticate and reject the reserved `system` tenant before any folder/provider/readiness lookup.
  - [x] Submit through `IEventStoreGatewayClient` or the established folder command gate with command type constants in `FoldersServerModule`.
  - [x] Map gateway/domain/provider categories to existing safe Problem Details responses and `202 Accepted` shape; never include provider target labels, credential refs beyond approved opaque refs, raw exception messages, or repository URLs.

- [x] Update projections and read models for binding status. (AC: 5, 11)
  - [x] Update `InMemoryFolderRepository.SaveLifecycleSnapshot` and/or projection handlers so binding request/success/failure events write `FolderLifecycleStatusReadModelSnapshot` with correct binding status, `repositoryBindingId`, `providerBindingRef`, freshness, and evidence scope.
  - [x] Keep `FolderLifecycleStatusQueryHandler` compatibility checks intact: tenant, principal, action, task/correlation, and authorization watermark must still match before binding evidence is returned.
  - [x] Ensure `FolderRepositoryBindingStatus.Unbound` with binding identifiers remains malformed/unavailable, not a successful unbound response.
  - [x] Add replay tests proving folder-list and lifecycle projections remain deterministic and metadata-only.

- [x] Add focused tests and guards. (AC: 1-13)
  - [x] Add aggregate tests under `tests/Hexalith.Folders.Tests/Aggregates/Folder/*RepositoryBacked*Tests.cs` for accepted request, missing folder, archived folder, active mutation guard, idempotent replay, conflicting idempotency, provider readiness failure, repository conflict, provider outcome unknown, and replay.
  - [x] Add authorization/order tests with recording fakes proving no pre-auth observation of provider binding, credential reference, provider adapter, repository target, workspace, audit, cache, metric, or diagnostic paths.
  - [x] Add provider abstraction/GitHub/Forgejo tests for repository creation success, equivalent-existing, repository conflict, permission insufficient, rate limited, unavailable, known failure, unsupported capability, timeout/unknown outcome, and leakage sentinels.
  - [x] Add server endpoint tests for route registration, required headers, unsupported schema version, malformed JSON, safe correlation/task echo, idempotency conflict mapping, readiness failure mapping, provider unavailable/rate-limit mapping, and no provider identifier leakage on denial.
  - [x] Add contract/generation/parity tests whenever OpenAPI, generated client, idempotency helper, previous-spine, or parity rows change.
  - [x] Add safety-channel inventory updates only if this story creates a new output channel for repository provisioning or provider diagnostics.
  - [x] Run the narrowest affected tests first, then the relevant contract/safety gates if public artifacts or metadata-only diagnostics changed.

## Dev Notes

### Source Context

- Epic 3 lets authorized actors configure providers, validate readiness, create repository-backed folders, bind existing repositories, define branch/ref policy, and inspect provider evidence without exposing secrets. Story 3.6 is the repository-creation path after readiness passes. [Source: `_bmad-output/planning-artifacts/epics.md#Story 3.6`]
- The sprint split changed the former combined story into Story 3.6 for creating a new Git-backed repository and Story 3.7 for binding an existing repository. Do not merge those paths back together. [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-10.md#Story 3.6-Create-repository-backed-folder`]
- PRD FR16-FR18 require readiness validation before repository-backed folder creation and repository-backed folder creation only when readiness checks pass. FR21-FR23 require metadata-only provider/credential/repository/capability evidence and explicit GitHub/Forgejo differences. [Source: `_bmad-output/planning-artifacts/prd.md#Provider-Readiness-and-Repository-Binding`]
- Architecture assigns provider bindings/readiness policy to `OrganizationAggregate` and provider abstractions, while folder lifecycle, storage mode, repository binding, workspace readiness, and ACL overrides belong to `FolderAggregate`. [Source: `_bmad-output/planning-artifacts/architecture.md#Domain-Layout`]
- C6 defines the relevant transition vocabulary: `RepositoryBindingRequested`, `RepositoryBound`, `RepositoryBindingFailed`, and `ProviderOutcomeUnknown`; unknown provider outcomes enter reconciliation and must not silently retry. [Source: `_bmad-output/planning-artifacts/architecture.md#Workspace-State-Transition-Matrix-C6-Enumerated`]
- AR-WORKER-01 assigns repository provisioning process-manager behavior to Workers reacting to repository-request events and being idempotent by causation/correlation ID. [Source: `_bmad-output/planning-artifacts/epics.md#Workers-Reconciliation-Rate-Limiting`]
- The current OpenAPI operation is `/api/v1/folders/repository-backed` with operationId `CreateRepositoryBackedFolder`, required idempotency/correlation/task headers, and canonical error categories including provider readiness, repository conflict, unsupported capability, unknown outcome, and reconciliation required. [Source: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#CreateRepositoryBackedFolder`]

### Previous Story Intelligence

- Story 3.1 implemented organization-scoped provider binding metadata with opaque credential references, provider kind, naming/branch policies, idempotency/conflict behavior, and secret-shaped input rejection. Story 3.6 should consume this metadata; it must not resolve raw credentials during folder aggregate handling.
- Story 3.2 introduced the internal `IGitProvider` capability port and provider failure taxonomy. Current source shows the port only supports capability discovery and comparison, so repository creation must extend the existing port narrowly if runtime provider creation is required.
- Story 3.3 added the GitHub provider boundary with Octokit isolated behind GitHub-specific seams and no public Contract Spine drift unless explicitly regenerated. Preserve that containment if adding repository creation.
- Story 3.4 added Forgejo provider boundary, supported-version snapshots, fail-closed version selection, safe base URL handling, and drift fixtures. Repository creation must use that manifest/snapshot evidence instead of treating Forgejo as a GitHub URL swap.
- Story 3.5 implemented provider readiness validation at `POST /api/v1/provider-readiness/validations`, safe diagnostics, readiness evidence storage, and no-touch authorization ordering. Story 3.6 must consume readiness outcomes as a precondition and must not invent new readiness categories.
- Story 2.8 archive hardening added `FolderActiveMutationGuard` with `RepositoryBinding` as an active-only mutation category. Reuse it for repository-backed creation so archived folders reject consistently.

### Existing Implementation State

- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs` currently handles `CreateFolder`, folder access grant/revoke, and `ArchiveFolder`. It does not yet handle `CreateRepositoryBackedFolder`.
- `FolderState` currently stores lifecycle state and a single `FolderRepositoryBindingState?`, but `FolderRepositoryBindingState` only has `Unbound`; it does not yet store `repositoryBindingId`, `providerBindingRef`, branch/ref policy, failure category, or unknown-outcome state.
- `FolderStateApply` throws on unknown folder event types, so adding repository-binding events requires replay support in the same change.
- `InMemoryFolderRepository.SaveLifecycleSnapshot` currently writes lifecycle snapshots with `FolderRepositoryBindingStatus.Unbound`, null repository binding, and null provider binding. Repository-binding events must update this projection path or an equivalent projection.
- `FolderLifecycleStatusQueryHandler` already understands binding statuses `BindingRequested`, `Bound`, `Failed`, `UnknownProviderOutcome`, and `ReconciliationRequired`; its tenant/principal/freshness compatibility checks should be preserved.
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderReadinessValidationService.cs` builds readiness from authorized provider binding metadata and capability profiles; it already requires `ProviderOperationCatalog.RepositoryCreation` for repository creation readiness.
- `src/Hexalith.Folders/Providers/Abstractions/IGitProvider.cs` currently exposes only capability discovery and comparison. Do not add a parallel provider abstraction.
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs` maps existing folder endpoints and archive command submission. `src/Hexalith.Folders.Server/ProviderReadinessEndpoints.cs` is a focused example for strict request parsing and safe diagnostics.
- `src/Hexalith.Folders.Workers` currently contains tenant-event projection worker plumbing only; repository provisioning worker behavior is not present yet.

### Required Architecture Patterns

- Use .NET 10, nullable-aware C#, file-scoped namespaces, one public type per file, PascalCase public members, camelCase locals/parameters, and async APIs with `CancellationToken`.
- Keep folder aggregate handlers deterministic and side-effect free. Pass timestamps from gates/processors; do not read time, generate random IDs, call providers, resolve credentials, write logs, or touch files from aggregate code.
- Keep tenant authority from authentication/EventStore evidence. Payload tenant/provider/folder identifiers are comparison inputs only.
- Mutations require fresh authorization. Do not allow bounded-stale tenant evidence, stale claim-transform evidence, or stale provider-readiness evidence to create provider repositories.
- Preserve metadata-only diagnostics across events, projections, audit records, Problem Details, logs, traces, metrics, generated artifacts, docs examples, and test output.
- Preserve stable Dapr app IDs, EventStore `/process` and `/project` routes, and external REST `/api/v1/...` naming.
- Keep package versions centralized in `Directory.Packages.props`; do not add inline package versions.
- Do not initialize or update nested submodules recursively.

### Files To Touch

- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderRepositoryBindingState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderResult.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderResultCode.cs`
- `src/Hexalith.Folders/Aggregates/Folder/*RepositoryBacked*.cs`
- `src/Hexalith.Folders/Aggregates/Folder/*RepositoryBinding*.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/*` only if a narrow readiness-consumer seam is missing
- `src/Hexalith.Folders/Providers/Abstractions/*` only for the repository-creation provider operation required by this story
- `src/Hexalith.Folders/Providers/GitHub/*` and `src/Hexalith.Folders/Providers/Forgejo/*` only behind their existing concrete provider seams
- `src/Hexalith.Folders.Workers/RepositoryProvisioning/*` if runtime provisioning is included in this slice
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs`
- `src/Hexalith.Folders.Server/FoldersServerModule.cs`
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs` or a focused `RepositoryBackedFolderEndpoints.cs`
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` only if the existing-folder contract conflict must be corrected
- `src/Hexalith.Folders.Client/Generated/*` only via regeneration
- `_bmad-output/implementation-artifacts/3-6-create-a-new-repository-backed-folder.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `docs/contract/idempotency-and-parity-rules.md`
- `tests/fixtures/parity-contract.yaml`
- `tests/fixtures/previous-spine.yaml` if the contract spine changes
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/*RepositoryBacked*Tests.cs`
- `tests/Hexalith.Folders.Tests/Providers/{Abstractions,GitHub,Forgejo}/*RepositoryCreation*Tests.cs`
- `tests/Hexalith.Folders.Server.Tests/*RepositoryBacked*EndpointTests.cs`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/TenantFolderProviderContractGroupTests.cs`

### Do Not Touch

- Do not implement Story 3.7 `BindRepository` for existing repositories.
- Do not implement Story 3.8 branch/ref policy mutation.
- Do not implement Story 4.2 workspace preparation, clone/materialization, workspace lock, file operations, commits, cleanup, repair automation, or context queries.
- Do not add CLI commands, MCP tools, UI pages, operations-console mutation controls, or generated SDK hand edits.
- Do not expose raw provider credentials, tokens, private keys, embedded credential URLs, clone URLs, owner/repository labels, installation IDs, raw provider payloads, raw exception text, file contents, diffs, generated context payloads, raw claim bags, or unauthorized resource existence.
- Do not add live GitHub/Forgejo calls to PR-gate tests.
- Do not add broad CI workflow files unless an owning CI story has opened the current Contract Spine negative-scope guard.
- Do not use recursive submodule initialization.

### Testing

- Use xUnit v3 and Shouldly. Prefer small recording fakes over broad mocks.
- Aggregate tests should start from `FolderState.Empty`, seed an existing active folder through `FolderCreated`, apply repository-binding events, and replay through `FolderStateApply`.
- Authorization/order tests should prove denial happens before provider binding, readiness, credential, provider adapter, repository target, workspace, audit, cache, metric, and diagnostics observation.
- Provider tests should stay offline and use the existing GitHub/Forgejo internal seams, fake providers, and pinned Forgejo fixtures.
- Endpoint tests should build a slim `WebApplication`, map server endpoints, and assert route registration plus safe `202`/Problem Details behavior.
- Contract tests must run when OpenAPI, parity, generated SDK, idempotency helper, or previous-spine artifacts change.
- Safety tests must scan any new provider/provisioning diagnostics output channel for secret, repository URL, file content, diff, provider payload, and unauthorized-resource sentinels.

### Regression Traps

- Do not implement repository-backed creation as a second logical-folder creation path just because the current OpenAPI request has `folderMetadata`.
- Do not extend `CreateFolder` to do provider work; Story 2.3 logical folder creation and Story 3.6 repository-backed provisioning are separate lifecycle steps in the approved story split.
- Do not call `ProviderReadinessValidationService` or provider adapters from inside `FolderAggregate`; aggregate code records state transitions only.
- Do not mark readiness green from provider binding configuration alone. Story 3.1 configuration is not readiness.
- Do not query provider binding/readiness evidence before tenant and folder authorization have passed.
- Do not use a global readiness/provisioning cache by provider family, instance URL, credential mode, or repository profile alone; include tenant, folder, organization, provider binding, safe target/profile, authorization freshness, and operation context.
- Do not retry unknown mutating provider outcomes. Unknown repository creation can duplicate repositories and must enter C6 unknown/reconciliation evidence.
- Do not collapse repository conflict, provider permission, provider unavailable, rate limit, unsupported capability, readiness failure, and unknown outcome into one generic provider failure.
- Do not let `FolderLifecycleStatusQueryHandler` expose repository binding IDs unless snapshot tenant, folder, principal, action, task/correlation, and authorization-watermark checks pass.
- Do not add raw provider details to event names, Problem Details messages, log templates, projection keys, metric labels, or test failure messages.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 3.6`
- `_bmad-output/planning-artifacts/epics.md#Workers-Reconciliation-Rate-Limiting`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-10.md#Story 3.6-Create-repository-backed-folder`
- `_bmad-output/planning-artifacts/prd.md#Provider-Readiness-and-Repository-Binding`
- `_bmad-output/planning-artifacts/prd.md#Technical-Success`
- `_bmad-output/planning-artifacts/architecture.md#Workspace-State-Transition-Matrix-C6-Enumerated`
- `_bmad-output/planning-artifacts/architecture.md#Domain-Layout`
- `_bmad-output/planning-artifacts/architecture.md#Requirements-to-Structure-Mapping`
- `_bmad-output/project-context.md#Critical-Dont-Miss-Rules`
- `_bmad-output/implementation-artifacts/3-1-configure-provider-binding-and-credential-reference.md`
- `_bmad-output/implementation-artifacts/3-2-define-igitprovider-port-and-capability-model.md`
- `_bmad-output/implementation-artifacts/3-3-implement-github-provider-adapter.md`
- `_bmad-output/implementation-artifacts/3-4-implement-forgejo-provider-adapter-and-drift-detection.md`
- `_bmad-output/implementation-artifacts/3-5-validate-provider-readiness-with-safe-diagnostics.md`
- `docs/contract/idempotency-and-parity-rules.md`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`
- `tests/fixtures/parity-contract.yaml`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderRepositoryBindingState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderActiveMutationGuard.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderReadinessValidationService.cs`
- `src/Hexalith.Folders/Providers/Abstractions/IGitProvider.cs`
- `src/Hexalith.Folders.Server/ProviderReadinessEndpoints.cs`
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`
- `src/Hexalith.Folders.Workers/FoldersWorkersModule.cs`

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-26 | Created story with existing-folder repository provisioning scope, readiness gate, C6 repository-binding transitions, provider outcome mapping, Contract Spine conflict guardrail, and offline test expectations. | Codex |
| 2026-05-26 | Reconciled `CreateRepositoryBackedFolder` contract with existing-folder `folderId`, regenerated SDK/idempotency helpers, and refreshed parity oracle hash. | Codex |
| 2026-05-26 | Added pure folder aggregate repository-binding command, C6 events, replay state, and folder-list projection support. | Codex |
| 2026-05-26 | Added repository-backed creation service with layered authorization, readiness, active-state, idempotency, and append ordering. | Codex |
| 2026-05-26 | Extended the existing provider port with metadata-only repository creation and GitHub/Forgejo fakeable seam mappings. | Codex |
| 2026-05-26 | Added repository provisioning process-manager behavior for C6 success/failure/unknown outcome recording. | Codex |
| 2026-05-26 | Wired the repository-backed REST endpoint, command authorization mapping, and EventStore domain processor path. | Codex |
| 2026-05-26 | Added lifecycle/read-model replay coverage for repository-binding request state and tightened repository-binding idempotency equivalence. | Codex |

## Dev Agent Record

### Agent Model Used

TBD by dev-story worker.

### Debug Log References

- `dotnet test .\tests\Hexalith.Folders.Contracts.Tests\Hexalith.Folders.Contracts.Tests.csproj --filter FullyQualifiedName~CreateRepositoryBackedFolder_TargetsExistingFolderAndKeepsIdempotencyScoped --no-restore` failed red before the Contract Spine update because `folderId` was missing, then passed after the update.
- `dotnet test .\tests\Hexalith.Folders.Client.Tests\Hexalith.Folders.Client.Tests.csproj --filter FullyQualifiedName~CreateRepositoryBackedFolderHelperIncludesExistingFolderIdentity --no-restore` passed after the client build regenerated SDK artifacts.
- `dotnet test .\tests\Hexalith.Folders.Testing.Tests\Hexalith.Folders.Testing.Tests.csproj --filter FullyQualifiedName~ContractRulesArtifact --no-restore` passed after the docs row was added.
- `dotnet run --project .\tests\tools\parity-oracle-generator\Hexalith.Folders.ParityOracleGenerator.csproj -- --repository-root "D:\Hexalith.Folders"` refreshed the parity oracle hash.
- `dotnet test .\tests\Hexalith.Folders.Tests\Hexalith.Folders.Tests.csproj --filter FullyQualifiedName~FolderRepositoryBackedAggregateTests --no-restore` failed red before aggregate types existed, then passed after command/events/state were implemented.
- `dotnet test .\tests\Hexalith.Folders.Tests\Hexalith.Folders.Tests.csproj --filter FullyQualifiedName~Aggregates.Folder --no-restore` passed with 246 tests.
- `dotnet test .\tests\Hexalith.Folders.Tests\Hexalith.Folders.Tests.csproj --filter FullyQualifiedName~Projections.FolderList --no-restore` passed with 2 tests.
- `dotnet test .\tests\Hexalith.Folders.Tests\Hexalith.Folders.Tests.csproj --filter FullyQualifiedName~FolderRepositoryBackedCreationGateTests --no-restore` failed red before the gate/service seam existed, then passed with 5 tests after implementation.
- `dotnet test .\tests\Hexalith.Folders.Tests\Hexalith.Folders.Tests.csproj --filter FullyQualifiedName~Authorization --no-restore` passed with 110 tests.
- `dotnet test .\tests\Hexalith.Folders.Tests\Hexalith.Folders.Tests.csproj --filter FullyQualifiedName~GitHubProviderTests --no-restore` failed red before provider repository-creation methods were implemented.
- `dotnet test .\tests\Hexalith.Folders.Tests\Hexalith.Folders.Tests.csproj --filter "FullyQualifiedName~GitHubProviderTests|FullyQualifiedName~ForgejoProviderTests" --no-restore` passed with 72 tests after GitHub and Forgejo repository-creation seams were implemented.
- `dotnet test .\tests\Hexalith.Folders.Tests\Hexalith.Folders.Tests.csproj --filter FullyQualifiedName~Providers.Abstractions --no-restore` passed with 33 tests after updating shared provider fakes.
- `dotnet test .\tests\Hexalith.Folders.Workers.Tests\Hexalith.Folders.Workers.Tests.csproj --filter FullyQualifiedName~RepositoryProvisioningProcessManagerTests --no-restore` failed red before the process-manager types existed, then passed with 6 tests after implementation.
- `dotnet test .\tests\Hexalith.Folders.Workers.Tests\Hexalith.Folders.Workers.Tests.csproj --no-restore` initially exposed DI validation for the optional process manager, then passed with 17 tests after deferring `IFolderRepository` resolution to process-manager resolution time.
- `dotnet msbuild .\src\Hexalith.Folders.Client\Hexalith.Folders.Client.csproj '/t:GenerateHexalithFoldersClient;GenerateHexalithFoldersIdempotencyHelpers'` regenerated the SDK and idempotency helpers after adding repository-binding identity to the equivalence fields.
- `dotnet test .\tests\Hexalith.Folders.Server.Tests\Hexalith.Folders.Server.Tests.csproj --filter "FullyQualifiedName~RepositoryBackedFolderEndpointTests|FullyQualifiedName~FolderCommandActionTokenMapperTests" --no-restore` passed with 11 tests after command mapping and route plumbing were added.
- `dotnet test .\tests\Hexalith.Folders.IntegrationTests\Hexalith.Folders.IntegrationTests.csproj --filter FullyQualifiedName~RepositoryBackedFolderRequestShouldRoundTripThroughProcessAndPersistRequestEvent --no-restore` failed first on an unsafe `repo-` idempotency key in the test fixture, then passed after switching to canonical binding identifiers.
- `dotnet test .\tests\Hexalith.Folders.Tests\Hexalith.Folders.Tests.csproj --filter FullyQualifiedName~FolderRepositoryBackedAggregateTests --no-restore` passed with 7 tests after adding `repositoryBindingId` to aggregate idempotency equivalence.
- `dotnet run --project .\tests\tools\parity-oracle-generator\Hexalith.Folders.ParityOracleGenerator.csproj -- --repository-root "D:\Hexalith.Folders"` refreshed the parity oracle after the idempotency-equivalence update.
- `dotnet test .\tests\Hexalith.Folders.Contracts.Tests\Hexalith.Folders.Contracts.Tests.csproj --filter FullyQualifiedName~CreateRepositoryBackedFolder_TargetsExistingFolderAndKeepsIdempotencyScoped --no-restore` passed after the Contract Spine equivalence update.
- `dotnet test .\tests\Hexalith.Folders.Client.Tests\Hexalith.Folders.Client.Tests.csproj --filter FullyQualifiedName~CreateRepositoryBackedFolderHelperIncludesExistingFolderIdentity --no-restore` passed after the generated helper included `branch_ref_policy.repository_binding_id`.
- `dotnet test .\tests\Hexalith.Folders.Testing.Tests\Hexalith.Folders.Testing.Tests.csproj --filter FullyQualifiedName~ContractRulesArtifact --no-restore` passed after parity/docs updates.
- `dotnet test .\tests\Hexalith.Folders.Tests\Hexalith.Folders.Tests.csproj --filter "FullyQualifiedName~FolderLifecycleStatusProjectionTests|FullyQualifiedName~Projections.FolderList" --no-restore` passed with 26 projection/read-model tests.
- `dotnet test .\tests\Hexalith.Folders.Server.Tests\Hexalith.Folders.Server.Tests.csproj --filter FullyQualifiedName~RepositoryBackedFolderEndpointTests --no-restore` passed with 10 endpoint tests after unsupported schema, malformed JSON, conflict, and safe-leakage coverage was added.
- `dotnet test .\tests\Hexalith.Folders.Tests\Hexalith.Folders.Tests.csproj --no-restore` passed with 640 tests.
- `dotnet test .\tests\Hexalith.Folders.Server.Tests\Hexalith.Folders.Server.Tests.csproj --no-restore` passed with 91 tests.
- `dotnet test .\tests\Hexalith.Folders.IntegrationTests\Hexalith.Folders.IntegrationTests.csproj --no-restore` passed with 12 tests.
- `dotnet test .\tests\Hexalith.Folders.Workers.Tests\Hexalith.Folders.Workers.Tests.csproj --no-restore` passed with 17 tests.
- `dotnet test .\tests\Hexalith.Folders.Contracts.Tests\Hexalith.Folders.Contracts.Tests.csproj --no-restore` passed with 82 tests.
- `dotnet test .\tests\Hexalith.Folders.Client.Tests\Hexalith.Folders.Client.Tests.csproj --no-restore` passed with 29 tests.
- `dotnet test .\tests\Hexalith.Folders.Testing.Tests\Hexalith.Folders.Testing.Tests.csproj --no-restore` passed with 43 tests.
- `dotnet test .\Hexalith.Folders.slnx --no-restore` passed the full regression suite with all test projects green and the UI E2E placeholder smoke test skipped by its existing condition.
- `dotnet test .\tests\Hexalith.Folders.Tests\Hexalith.Folders.Tests.csproj --filter "FullyQualifiedName~FolderRepositoryBackedCreationGateTests|FullyQualifiedName~GitHubProviderTests|FullyQualifiedName~ForgejoProviderTests" --no-restore` initially exposed that equivalent repository-binding replay and in-progress repository-binding mutation paths could reach readiness/idempotency observation first, then passed with 116 tests after the gate preflighted pure aggregate state before readiness.
- `dotnet test .\tests\Hexalith.Folders.Workers.Tests\Hexalith.Folders.Workers.Tests.csproj --filter FullyQualifiedName~RepositoryProvisioningProcessManagerTests --no-restore` passed with 7 tests after adding provider-request context and state-unavailable guardrail coverage.
- `dotnet test .\tests\Hexalith.Folders.Server.Tests\Hexalith.Folders.Server.Tests.csproj --filter RepositoryBackedFolderEndpointTests --no-restore` passed with 10 tests.
- `dotnet test .\tests\Hexalith.Folders.IntegrationTests\Hexalith.Folders.IntegrationTests.csproj --filter FullyQualifiedName~RepositoryBackedFolderRequestShouldRoundTripThroughProcessAndPersistRequestEvent --no-restore` passed with 1 test.
- `dotnet test .\tests\Hexalith.Folders.Tests\Hexalith.Folders.Tests.csproj --no-restore` passed with 679 tests.
- `dotnet test .\tests\Hexalith.Folders.Server.Tests\Hexalith.Folders.Server.Tests.csproj --no-restore` passed with 91 tests.
- `dotnet test .\tests\Hexalith.Folders.Workers.Tests\Hexalith.Folders.Workers.Tests.csproj --no-restore` passed with 18 tests.
- `dotnet test .\tests\Hexalith.Folders.IntegrationTests\Hexalith.Folders.IntegrationTests.csproj --no-restore` passed with 12 tests.

### Completion Notes List

- Story created by the local `.agents/skills/bmad-create-story` workflow for `3-6-create-a-new-repository-backed-folder`.
- Project context, sprint status, Epic 3 story source, PRD provider-readiness and repository-binding requirements, architecture C6 matrix, Contract Spine operation shape, idempotency/parity docs, Stories 3.1 through 3.5, current folder aggregate/provider/readiness/server/worker source, recent commits, and neighboring story patterns were reviewed.
- No external web research was added because this story does not introduce a new external library or provider version source; it must consume the provider evidence and version pins already established by Stories 3.3 and 3.4.
- Ultimate context engine analysis completed - comprehensive developer guide created.
- Contract reconciliation completed first: the stable route remains `POST /api/v1/folders/repository-backed`, the request now carries explicit `folderId`, the operation summary no longer describes logical folder creation, and idempotency includes `folder_id` with safe lexicographic fields.
- Folder aggregate work added `CreateRepositoryBackedFolder`, metadata-only C6 repository-binding events, state replay for requested/bound/failed/unknown/reconciliation outcomes, active-folder mutation guarding, and deterministic folder-list projection replay.
- Repository-backed creation gate now uses layered authorization first, compares payload tenant values without trusting them, checks active folder and pure aggregate replay/in-progress state before readiness, consumes the provider-readiness validation result for new repository-creation requests, and only then performs tenant/folder-scoped append-ledger idempotency and append.
- Provider port work extended the existing `IGitProvider` with a narrow repository creation operation, reused safe target fingerprinting and authorization snapshots, and mapped GitHub/Forgejo success, equivalent-existing, known failure, unavailable/rate-limit, unsupported, and unknown mutating outcomes without exposing provider DTOs publicly.
- Repository provisioning worker code now consumes `RepositoryBindingRequested` with a proven provider context, invokes the existing provider port once, and appends metadata-only `RepositoryBound`, `RepositoryBindingFailed`, or `ProviderOutcomeUnknown` outcomes idempotently without workspace preparation.
- REST and domain wiring now exposes `POST /api/v1/folders/repository-backed` with strict JSON parsing, required canonical idempotency/correlation/task headers, reserved-system-tenant rejection, safe gateway Problem Details, and the canonical EventStore command type.
- The folder domain processor now handles the repository-backed command through the layered authorization accessor and repository-backed creation service, so gateway/process execution persists the same metadata-only request event as the direct gate.
- Lifecycle/list projections now replay repository-binding request and provider outcome events into tenant-scoped binding status/read-model metadata, with integration coverage proving an accepted REST/process request surfaces `BindingRequested` without provider target leakage.
- Idempotency equivalence was tightened so `repositoryBindingId` participates in the public helper hash and aggregate fingerprint, causing same-key repository-binding identity changes to return `idempotency_conflict`.
- QA automation added Story 3.6 guardrails for readiness category mapping, in-progress binding short-circuiting, equivalent replay before readiness observation, repository-creation provider failure mappings, provisioning provider-request context, and state-unavailable worker behavior.
- Repository-backed creation service now preflights pure aggregate state before provider-readiness validation so equivalent replay and already-in-progress binding states do not probe provider readiness or append/idempotency seams.

### Implementation Plan

- Keep Story 3.6 scoped to existing-folder repository provisioning: contract/generation first, then pure aggregate C6 state/events, ordered gate/service checks, narrow provider-port repository creation, worker process-manager behavior, REST wiring, projection updates, and focused offline tests.

### File List

- `_bmad-output/implementation-artifacts/tests/test-summary.md`
- `docs/contract/idempotency-and-parity-rules.md`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`
- `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs`
- `src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs`
- `src/Hexalith.Folders.Server/Authorization/FolderCommandActionTokenMapper.cs`
- `src/Hexalith.Folders.Server/FolderCommandRejected.cs`
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs`
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`
- `src/Hexalith.Folders.Server/FoldersServerModule.cs`
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs`
- `src/Hexalith.Folders/Aggregates/Folder/CreateRepositoryBackedFolder.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidationResult.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderRepositoryBindingState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderResult.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderResultCode.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs`
- `src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs`
- `src/Hexalith.Folders/Aggregates/Folder/ProviderOutcomeUnknown.cs`
- `src/Hexalith.Folders/Aggregates/Folder/IRepositoryCreationReadinessValidator.cs`
- `src/Hexalith.Folders/Aggregates/Folder/ProviderReadinessRepositoryCreationValidator.cs`
- `src/Hexalith.Folders/Aggregates/Folder/RepositoryBindingFailed.cs`
- `src/Hexalith.Folders/Aggregates/Folder/RepositoryBindingRequested.cs`
- `src/Hexalith.Folders/Aggregates/Folder/RepositoryBound.cs`
- `src/Hexalith.Folders/Aggregates/Folder/RepositoryBackedFolderCreationRequest.cs`
- `src/Hexalith.Folders/Aggregates/Folder/RepositoryBackedFolderCreationService.cs`
- `src/Hexalith.Folders/Authorization/EffectivePermissionsActionCatalog.cs`
- `src/Hexalith.Folders/Projections/FolderList/FolderListItem.cs`
- `src/Hexalith.Folders/Projections/FolderList/FolderListProjection.cs`
- `src/Hexalith.Folders/Providers/Abstractions/IGitProvider.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderRepositoryCreationRequest.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderRepositoryCreationResult.cs`
- `src/Hexalith.Folders/Providers/Forgejo/ForgejoFailureMapper.cs`
- `src/Hexalith.Folders/Providers/Forgejo/ForgejoHttpApiClient.cs`
- `src/Hexalith.Folders/Providers/Forgejo/ForgejoProvider.cs`
- `src/Hexalith.Folders/Providers/Forgejo/ForgejoRepositoryCreationRequest.cs`
- `src/Hexalith.Folders/Providers/Forgejo/ForgejoRepositoryCreationResult.cs`
- `src/Hexalith.Folders/Providers/Forgejo/ForgejoSafeTargetFingerprint.cs`
- `src/Hexalith.Folders/Providers/Forgejo/IForgejoApiClient.cs`
- `src/Hexalith.Folders/Providers/GitHub/GitHubApiFailureCondition.cs`
- `src/Hexalith.Folders/Providers/GitHub/GitHubFailureMapper.cs`
- `src/Hexalith.Folders/Providers/GitHub/GitHubProvider.cs`
- `src/Hexalith.Folders/Providers/GitHub/GitHubRepositoryCreationRequest.cs`
- `src/Hexalith.Folders/Providers/GitHub/GitHubRepositoryCreationResult.cs`
- `src/Hexalith.Folders/Providers/GitHub/GitHubSafeTargetFingerprint.cs`
- `src/Hexalith.Folders/Providers/GitHub/IGitHubApiClient.cs`
- `src/Hexalith.Folders/Providers/GitHub/OctokitGitHubApiClient.cs`
- `src/Hexalith.Folders.Testing/Providers/FakeGitProvider.cs`
- `src/Hexalith.Folders.Testing/Providers/RecordingProviderCapabilityResolver.cs`
- `src/Hexalith.Folders.Workers/FoldersWorkersModule.cs`
- `src/Hexalith.Folders.Workers/RepositoryProvisioning/RepositoryProvisioningContext.cs`
- `src/Hexalith.Folders.Workers/RepositoryProvisioning/RepositoryProvisioningProcessManager.cs`
- `src/Hexalith.Folders.Workers/RepositoryProvisioning/RepositoryProvisioningResult.cs`
- `src/Hexalith.Folders.Workers/RepositoryProvisioning/RepositoryProvisioningResultCode.cs`
- `tests/fixtures/parity-contract.yaml`
- `tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/TenantFolderProviderContractGroupTests.cs`
- `tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs`
- `tests/Hexalith.Folders.Server.Tests/FolderCommandActionTokenMapperTests.cs`
- `tests/Hexalith.Folders.Server.Tests/RepositoryBackedFolderEndpointTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderCommandFactory.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderRepositoryBackedAggregateTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderRepositoryBackedCreationGateTests.cs`
- `tests/Hexalith.Folders.Tests/Providers/Forgejo/ForgejoProviderReadinessValidationServiceTests.cs`
- `tests/Hexalith.Folders.Tests/Providers/Forgejo/ForgejoProviderTests.cs`
- `tests/Hexalith.Folders.Tests/Providers/GitHub/GitHubProviderTests.cs`
- `tests/Hexalith.Folders.Tests/Providers/GitHub/RecordingGitHubApiClient.cs`
- `tests/Hexalith.Folders.Workers.Tests/RepositoryProvisioningProcessManagerTests.cs`
- `tests/Hexalith.Folders.Testing.Tests/ContractRulesArtifactTests.cs`
