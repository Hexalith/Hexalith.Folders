---
baseline_commit: 3956209dc9bde9f9b6258d5c3fc3cef8ea9b0e2a
---

# Story 3.8: Define branch and ref policy

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an authorized actor,
I want to define or select branch/ref policy for repository-backed tasks,
so that workspace preparation and commits use predictable refs.

## Terms

- `ConfigureBranchRefPolicy` means the mutating branch/ref policy path at `PUT /api/v1/folders/{folderId}/branch-ref-policy`. It updates metadata for an already repository-backed folder; it must not create folders, create repositories, bind repositories, prepare workspaces, or run Git operations.
- `GetBranchRefPolicy` means the read path at `GET /api/v1/folders/{folderId}/branch-ref-policy`. It returns projection/read-model metadata only after tenant, folder, and branch/ref-policy read authorization pass.
- Branch/ref policy metadata means `repositoryBindingId`, `policyRef`, `defaultRef`, `allowedRefPatterns`, `protectedRefPatterns`, correlation/task/idempotency evidence, freshness, and timestamps. Raw Git refs are tenant-sensitive metadata and must use the Contract Spine `branch_ref_*` safe token shape; never store or emit provider-specific branch names, URLs, owner/repository names, credential data, raw provider payloads, file contents, or diffs.
- Provider capability validation means proving the folder's provider binding supports `ProviderOperationCatalog.BranchRefInspection` for `ProviderReadinessRequestedCapability.BranchRefPolicy`. This story must not require repository creation or existing-repository binding readiness to configure a policy for an already bound repository.
- Repository-backed folder means an active folder whose current repository-binding state is `Bound` for the same tenant/folder/repository binding identity. Policy configuration against unbound, binding-requested, failed, unknown-outcome, reconciliation-required, archived, or nonexistent folders must fail closed with stable categories.

## Acceptance Criteria

1. Given an authenticated actor has authoritative tenant evidence, folder access, provider-binding-use or branch-policy-management authority, and a repository-backed active folder, when `ConfigureBranchRefPolicy` is accepted, then the system stores branch/ref policy metadata for that folder and repository binding without reading credentials, constructing provider adapters, touching workspaces, listing refs, committing, or emitting policy/projection identifiers before authorization succeeds.
2. Given the current Contract Spine already declares `PUT /api/v1/folders/{folderId}/branch-ref-policy` and `GET /api/v1/folders/{folderId}/branch-ref-policy`, when implementation starts, then the developer must reconcile runtime behavior with the existing operation shape: route `folderId`, required idempotency/correlation/task headers, `repositoryBindingId`, `policyRef`, `defaultRef`, `allowedRefPatterns`, and optional `protectedRefPatterns` must be sufficient for aggregate, idempotency, SDK, parity, and read-model behavior; if not, update OpenAPI, contract tests, parity fixture, previous-spine baseline, generated client/idempotency helpers, and docs together.
3. Given a repository-backed folder exists and the configured `repositoryBindingId` matches the folder's current bound repository binding, when branch/ref policy is configured, then the policy metadata is recorded and projected with the selected `policyRef`, default ref token, allowed pattern tokens, protected pattern tokens, correlation ID, task ID, idempotency key/fingerprint, actor evidence, and occurred-at timestamp.
4. Given the folder does not exist, is archived, is unbound, is only binding-requested, has failed binding, is in unknown-provider-outcome or reconciliation-required state, or the supplied `repositoryBindingId` does not match the bound folder state, when `ConfigureBranchRefPolicy` is attempted, then no state is mutated and the caller receives safe not-found, folder-ACL denial, validation, read-model-unavailable, repository-binding-unavailable, or `state_transition_invalid` behavior without exposing provider binding, repository target, credential reference, policy existence, or unauthorized folder existence.
5. Given provider readiness evidence is missing, stale, unavailable, failed, degraded for branch/ref handling, unsupported, reconciliation-required, unknown, or tied to a different tenant/provider binding/authorization snapshot, when the command is evaluated, then no folder state is mutated and no provider adapter check is attempted; the caller receives a stable category such as `provider_readiness_failed`, `unsupported_provider_capability`, `read_model_unavailable`, `unknown_provider_outcome`, or `reconciliation_required` as appropriate.
6. Given the branch/ref policy payload is malformed, too large, has unknown JSON members, has duplicate or empty patterns, uses values outside the Contract Spine safe token shape, crosses repository binding identity, or contains raw branch names/URLs/path-like/credential-shaped/provider-shaped metadata, when REST or domain validation runs, then the request is rejected before gateway/domain mutation and no unsafe value is echoed in Problem Details, logs, events, projections, or test failures.
7. Given the same idempotency key and equivalent `ConfigureBranchRefPolicy` payload are retried, when the folder stream already contains the equivalent accepted policy result, then the operation returns the same logical replay result without appending another event or revalidating provider readiness; given the same key is reused with different folder, repository binding, policy ref, default ref, allowed/protected pattern set, tenant, credential scope, or authoritative evidence, then the result is `idempotency_conflict` and state is unchanged.
8. Given `GetBranchRefPolicy` is called by an authorized actor, when the read model has current policy metadata compatible with the caller's tenant, folder, principal, action, task/correlation, and authorization watermark, then the response returns `BranchRefPolicy` with freshness metadata and redacted/metadata-only fields; unauthorized callers receive safe denial and no policy, folder, provider, or repository existence signal.
9. Given GitHub and Forgejo capability differences exist, when branch/ref policy readiness is evaluated, then differences remain provider metadata through the existing capability model; product workflows must not treat Forgejo as a GitHub base-URL swap or leak provider-specific endpoint paths into contracts, responses, logs, or tests.
10. Given branch/ref policies are later consumed by workspace preparation and commits, when this story records policy metadata, then it preserves enough deterministic evidence for Epic 4 to validate requested refs against the selected policy without needing raw refs, provider payloads, live provider calls, or unbounded repository inspection in this story.
11. Given unit, contract, server, projection, readiness, client-generation, parity, and safety tests run locally or in CI, then Story 3.8 validation is hermetic: no live GitHub, live Forgejo, provider credentials, tenant seed data, Aspire, Dapr sidecars, Redis, Keycloak, Docker, network calls, or nested submodule initialization is required.

## Tasks / Subtasks

- [x] Reconcile the public contract and generated artifacts before runtime work. (AC: 2, 6, 7, 8)
  - [x] Review `ConfigureBranchRefPolicy` and `GetBranchRefPolicy` in `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`, `docs/contract/idempotency-and-parity-rules.md`, `docs/contract/sdk-generation-and-idempotency-helpers.md`, `tests/fixtures/parity-contract.yaml`, `tests/fixtures/previous-spine.yaml`, and `TenantFolderProviderContractGroupTests`.
  - [x] Keep `ConfigureBranchRefPolicy` mutating with required `Idempotency-Key`, `X-Correlation-Id`, and `X-Hexalith-Task-Id`; keep `GetBranchRefPolicy` non-mutating and reject idempotency keys if the read path grows explicit validation.
  - [x] Preserve the generated helper shape documented for `BranchRefPolicyRequest.ComputeIdempotencyHash(string folderId)`: `repositoryBindingId` is sourced from the DTO property, not from a helper method parameter.
  - [x] Keep idempotency equivalence lexicographically aligned with the spine: allowed ref patterns, default ref, policy ref, protected ref patterns, folder ID, and repository binding ID. If runtime needs a different field list, update spine, helper tests, generated SDK, parity rows, and docs together.
  - [x] Do not hand-edit generated SDK files under `src/Hexalith.Folders.Client/Generated/*`; regenerate through the established NSwag/helper pipeline if the spine changes.

- [x] Add branch/ref policy command, event, state, and validation on the folder aggregate. (AC: 1, 3, 4, 6, 7, 10)
  - [x] Add `ConfigureBranchRefPolicy` and a request record under `src/Hexalith.Folders/Aggregates/Folder/`, keeping one public type per file and implementing `IFolderCommand`.
  - [x] Add a metadata-only event such as `BranchRefPolicyConfigured` only if no existing event fits; include tenant, organization, folder, repository binding ID, policy ref, safe default ref token, allowed/protected safe pattern tokens, actor/correlation/task/idempotency evidence, fingerprint, and occurred-at.
  - [x] Extend `FolderState` with current branch/ref policy metadata separately from the existing `BranchRefPolicyRef` recorded during repository creation/binding, or deliberately reuse the existing field only if it does not lose default/pattern metadata needed by Epic 4.
  - [x] Extend `FolderStateApply` to replay the new event deterministically and fail loudly on unknown future events.
  - [x] Extend `FolderCommandValidator` with strict branch/ref policy validation: schema version `v1`, matching repository binding ID, opaque `policyRef`, safe `branch_ref_*` default/pattern tokens, max 16 allowed/protected patterns, no duplicate pattern semantics after ordinal normalization, and metadata blocklist protections.
  - [x] Ensure aggregate decision logic requires an existing active bound repository state and rejects unbound/requested/failed/unknown/reconciliation/archived/missing folders without provider/readiness observation.
  - [x] Preserve pure aggregate rules: no provider calls, credential resolution, Dapr, HTTP, filesystem, Git, logs, random IDs, or wall-clock reads inside aggregate code.

- [x] Implement authorization, readiness, idempotency, and append ordering in a focused service. (AC: 1, 4, 5, 7, 9)
  - [x] Add a focused service such as `BranchRefPolicyConfigurationService`, sibling to `RepositoryBackedFolderCreationService` and `RepositoryBindingService`, with an action token such as `configure_branch_ref_policy`.
  - [x] Validate layered authorization before reading folder state, readiness, provider binding, policy stores, projections, diagnostics, metrics, or provider capability details.
  - [x] Load folder state after authorization and reject non-bound repository states before provider readiness validation.
  - [x] Check equivalent idempotency replay before readiness/provider observation so retries cannot create unnecessary provider-readiness probes.
  - [x] Validate provider readiness with `ProviderReadinessRequestedCapability.BranchRefPolicy`; correct `ProviderReadinessValidationService.RequiredOperations` so this capability requires branch/ref inspection and common lifecycle support but does not incorrectly require repository creation.
  - [x] Map readiness and authorization outcomes to existing `FolderResultCode` values where possible; add narrow result codes only with Problem Details mapping, contract/parity/docs/tests, and generated artifacts updated together.
  - [x] Append through `IFolderRepository.AppendIfFingerprintAbsent` or the established EventStore command path and resolve append races as idempotent replay or idempotency conflict without leaking policy metadata.

- [x] Wire REST, domain processor, and DI plumbing. (AC: 1, 2, 6, 7, 8)
  - [x] Add `FoldersServerModule.ConfigureBranchRefPolicyCommandType = "Hexalith.Folders.Commands.ConfigureBranchRefPolicy"` and map it in `FolderCommandActionTokenMapper`.
  - [x] Map `PUT /api/v1/folders/{folderId}/branch-ref-policy` in `FoldersDomainServiceEndpoints`; validate route `folderId`, headers, tenant context, reserved `system` tenant, request schema version, strict JSON, and branch/ref policy body before gateway submission.
  - [x] Map `GET /api/v1/folders/{folderId}/branch-ref-policy` to a projection/query handler, not aggregate state or provider APIs.
  - [x] Add gateway payload records for configure policy without forwarding unknown fields or raw unsafe values.
  - [x] Extend `FolderDomainProcessor` to deserialize the command payload, bind authoritative tenant/principal evidence, build the service request, and submit to the new branch/ref policy service.
  - [x] Register the service, readiness validator dependency, projection/read-model store, and query handler in `FoldersServerServiceCollectionExtensions` / `FoldersServiceCollectionExtensions` following existing scoped/singleton patterns.

- [x] Add branch/ref policy projection/read model. (AC: 3, 8, 10)
  - [x] Add a read model and query handler for `BranchRefPolicy`, likely under `src/Hexalith.Folders/Queries/Folders/` or a focused `Queries/BranchRefPolicy/` concept area.
  - [x] Save projection snapshots when branch/ref policy events are appended, including tenant/folder/repository binding identity, policy metadata, freshness, and evidence scope.
  - [x] Enforce compatibility checks equivalent to lifecycle status reads: tenant, principal, action, task/correlation, authorization watermark, and freshness must match before policy metadata is returned.
  - [x] Keep redacted, unknown, missing, inaccessible, and stale states distinct; do not silently collapse them into an empty policy response.
  - [x] Ensure authorized reads never expose raw branch names, clone URLs, provider payloads, credential refs beyond approved opaque refs, or unauthorized existence.

- [x] Add focused tests and guards. (AC: 1-11)
  - [x] Add aggregate tests under `tests/Hexalith.Folders.Tests/Aggregates/Folder/*BranchRefPolicy*Tests.cs` for accepted configure, missing folder, archived folder, unbound folder, binding-requested folder, failed/unknown/reconciliation states, repository binding mismatch, malformed policy, duplicate patterns, idempotent replay, and conflicting idempotency.
  - [x] Add service/order tests with recording fakes proving denial, non-bound state, malformed evidence, equivalent replay, and idempotency conflict make zero calls to provider binding readers, readiness readers, credential resolvers, provider adapters, workspaces, audit, cache, metrics, logs, or diagnostics.
  - [x] Add readiness tests proving `ProviderReadinessRequestedCapability.BranchRefPolicy` does not require `ProviderOperationCatalog.RepositoryCreation` and does require `ProviderOperationCatalog.BranchRefInspection`.
  - [x] Add endpoint tests for `PUT /api/v1/folders/{folderId}/branch-ref-policy`: route registration, required headers, unsupported schema version, malformed JSON, unknown fields, route/body folder behavior, invalid branch/ref token rejection, pattern limits, safe correlation/task echo, idempotency conflict mapping, readiness failure mapping, and no unsafe value leakage.
  - [x] Add query endpoint tests for `GET /api/v1/folders/{folderId}/branch-ref-policy`: authorized success with freshness, unsupported freshness header if applicable, safe denial, read-model unavailable/stale, and no policy existence leakage.
  - [x] Add projection replay tests proving branch/ref policy snapshots are deterministic and metadata-only.
  - [x] Run client-generation/helper tests and contract/parity tests if OpenAPI, generated SDK, parity rows, previous-spine, or idempotency helper artifacts change.
  - [x] Run safety-channel scans if this story creates a new diagnostics/projection output channel.

## Dev Notes

### Source Context

- Epic 3 lets authorized actors configure providers, validate readiness, create repository-backed folders, bind existing repositories, define branch/ref policy, and inspect provider evidence without exposing secrets. Story 3.8 is specifically branch/ref policy selection for repository-backed tasks. [Source: `_bmad-output/planning-artifacts/epics.md#Story 3.8`]
- PRD FR20 requires authorized actors to define or select the branch/ref policy used by repository-backed folder tasks. FR21 requires branch/ref metadata exposure without secrets, and FR23 requires provider readiness evidence for branch/ref handling. [Source: `_bmad-output/planning-artifacts/prd.md#Provider-Readiness-and-Repository-Binding`]
- PRD FR24 makes branch/ref policy a prerequisite for workspace preparation. Story 3.8 records policy metadata only; Story 4.2 owns workspace preparation. [Source: `_bmad-output/planning-artifacts/prd.md#Workspace-and-Lock-Lifecycle`]
- Architecture maps FR15-FR23 to provider readiness, repository binding, contracts, and providers. Folder lifecycle and repository-backed state remain under `src/Hexalith.Folders/Aggregates/Folder/`. [Source: `_bmad-output/planning-artifacts/architecture.md#Requirements-to-Structure-Mapping`]
- Contract Spine already declares `ConfigureBranchRefPolicy` and `GetBranchRefPolicy` at `/api/v1/folders/{folderId}/branch-ref-policy`. `ConfigureBranchRefPolicy` is mutating, requires idempotency/correlation/task headers, and has equivalence over branch/ref policy fields, folder ID, and repository binding ID. [Source: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#ConfigureBranchRefPolicy`]
- `BranchRefPolicyRequest` requires `requestSchemaVersion`, `repositoryBindingId`, `policyRef`, `defaultRef`, and `allowedRefPatterns`; `protectedRefPatterns` is optional. Default and pattern values must match `^branch_ref_[a-z0-9_]{3,80}$` with max 16 entries per pattern collection. [Source: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#BranchRefPolicyRequest`]
- Generated SDK currently has `ConfigureBranchRefPolicyAsync`, `GetBranchRefPolicyAsync`, and `BranchRefPolicyRequest.ComputeIdempotencyHash(string folderId)`; helper tests assert the current declared field order. [Source: `tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs#BranchRefPolicyHelperUsesDeclaredSpineFields`]

### Previous Story Intelligence

- Story 3.1 implemented provider binding metadata with naming and branch policy refs; Story 3.8 should consume opaque provider/binding evidence and must not store credentials or raw provider payloads in folder state.
- Story 3.2 introduced the internal `IGitProvider` capability port and `ProviderOperationCatalog.BranchRefInspection`; Story 3.8 should reuse the capability model rather than adding a second provider abstraction.
- Story 3.3 and 3.4 isolated GitHub and Forgejo behavior behind provider adapters. This story should validate provider capability/readiness through existing services, not provider-specific endpoints or DTOs.
- Story 3.5 implemented provider readiness validation and already has `ProviderReadinessRequestedCapability.BranchRefPolicy`; however current `RequiredOperations` adds repository creation for every capability except existing repository binding. Story 3.8 must fix that so branch/ref policy readiness is not coupled to repository creation.
- Story 3.6 and 3.7 created and bound repository-backed folders, respectively. Both record `BranchRefPolicyRef` during repository binding, but they do not store the full policy metadata or expose the branch/ref policy endpoints.
- Story 3.7 review fixed provider-binding validation and completed with status `done`. Its implementation added `RepositoryBindingService`, `BindRepository`, `ExistingRepositoryBindingRequested`, REST route `POST /api/v1/folders/{folderId}/repository-bindings`, provider binding validation, and focused tests. Reuse the service and endpoint patterns, but keep branch/ref policy as a separate operation.

### Existing Implementation State

- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs` maps archive, repository-backed creation, existing repository binding, effective permissions, and lifecycle status routes. It does not currently map `PUT` or `GET /api/v1/folders/{folderId}/branch-ref-policy`.
- `FoldersDomainServiceEndpoints` already contains `BranchRefPolicyHttpRequest`, `IsValidBranchRefPolicy`, and `BranchRefPolicyRegex`, currently used by repository-backed creation and existing repository binding request validation. These helpers are a starting point, but Story 3.8 needs a standalone endpoint and command flow. Check the helper against the spine: `policyRef` is an opaque identifier, while `defaultRef` and pattern entries use the `branch_ref_*` shape.
- `src/Hexalith.Folders.Server/FoldersServerModule.cs` has command type constants for archive, create repository-backed folder, and bind repository. It does not yet have a `ConfigureBranchRefPolicy` command type.
- `src/Hexalith.Folders.Server/Authorization/FolderCommandActionTokenMapper.cs` maps create, archive, repository-backed creation, bind repository, access, provider binding, and future workspace/file/commit commands. It does not yet map configure branch/ref policy.
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs` processes archive, create repository-backed folder, and bind repository payloads. It does not yet process configure branch/ref policy.
- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs` handles `CreateFolder`, folder access, `ArchiveFolder`, `CreateRepositoryBackedFolder`, and `BindRepository`. It does not yet handle a branch/ref policy configuration command.
- `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs` stores repository binding metadata including `BranchRefPolicyRef`, but not full default/allowed/protected policy metadata.
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs` replays folder creation, access, archive, repository binding request/success/failure, and unknown outcome. It does not yet replay branch/ref policy configuration.
- `src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs` updates lifecycle status snapshots for folder lifecycle and repository binding state. There is no branch/ref policy read-model snapshot path yet.
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderReadinessRequestedCapability.cs` includes `BranchRefPolicy`.
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderReadinessValidationService.cs` maps required operations for readiness. Current logic incorrectly adds repository creation for every requested capability except `ExistingRepositoryBinding`; branch/ref policy should not require repository creation.
- `src/Hexalith.Folders/Providers/Abstractions/ProviderOperationCatalog.cs` includes `BranchRefInspection`, and `ProviderReadinessCapabilityEvidence` already has a branch/ref policy evidence field.
- Dirty worktree note at story creation time: `Hexalith.Builds` and `_bmad-output/story-automator/orchestration-3-20260526-203745.md` were already modified and are unrelated to this story file.

### Required Architecture Patterns

- Use .NET 10, nullable-aware C#, file-scoped namespaces, one public type per file, PascalCase public members, camelCase locals/parameters, async APIs with `CancellationToken`, and centralized package versions.
- Keep tenant authority from authentication/EventStore evidence. Payload, route, query, header, provider, folder, repository binding, and policy values are comparison inputs only.
- Preserve authorization-before-observation. No provider binding, readiness, policy, repository, credential, workspace, audit, cache, log enrichment, metric, or diagnostic observation before tenant and folder authorization passes.
- Keep aggregate handlers deterministic and side-effect free. Pass timestamps from gates/processors; do not read time, generate IDs, call providers, resolve credentials, write logs, or touch files from aggregate code.
- Preserve metadata-only diagnostics across events, projections, audit records, Problem Details, logs, traces, metrics, generated artifacts, docs examples, and test output.
- Treat branch names, ref names, policy names, repository names, and provider diagnostics as tenant-sensitive metadata. Use safe opaque `branch_ref_*` tokens at public boundaries.
- Preserve stable Dapr app IDs, EventStore `/process` and `/project` routes, and external REST `/api/v1/...` naming.
- Do not initialize or update nested submodules recursively.

### Files To Touch

- `src/Hexalith.Folders/Aggregates/Folder/ConfigureBranchRefPolicy.cs`
- `src/Hexalith.Folders/Aggregates/Folder/BranchRefPolicyConfigurationRequest.cs`
- `src/Hexalith.Folders/Aggregates/Folder/BranchRefPolicyConfigurationService.cs`
- `src/Hexalith.Folders/Aggregates/Folder/BranchRefPolicyConfigured.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidationResult.cs` if new accepted metadata is needed
- `src/Hexalith.Folders/Aggregates/Folder/FolderResult.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderResultCode.cs` only if existing result codes cannot express required outcomes
- `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs`
- `src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs` or a new branch/ref policy projection store
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderReadinessValidationService.cs`
- `src/Hexalith.Folders/Queries/Folders/*BranchRefPolicy*.cs` or a focused branch/ref policy query folder
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs`
- `src/Hexalith.Folders.Server/FoldersServerModule.cs`
- `src/Hexalith.Folders.Server/Authorization/FolderCommandActionTokenMapper.cs`
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs`
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` only if runtime shape must change
- `src/Hexalith.Folders.Client/Generated/*` only via regeneration
- `docs/contract/idempotency-and-parity-rules.md` if semantics or equivalence change
- `docs/contract/sdk-generation-and-idempotency-helpers.md` if helper shape changes
- `tests/fixtures/parity-contract.yaml`
- `tests/fixtures/previous-spine.yaml` if the Contract Spine changes
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/*BranchRefPolicy*Tests.cs`
- `tests/Hexalith.Folders.Tests/Queries/*BranchRefPolicy*Tests.cs`
- `tests/Hexalith.Folders.Tests/Queries/ProviderReadiness/*BranchRefPolicy*Tests.cs`
- `tests/Hexalith.Folders.Server.Tests/*BranchRefPolicy*EndpointTests.cs`
- `tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs` if helper fields change
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/TenantFolderProviderContractGroupTests.cs` if contract metadata changes

### Do Not Touch

- Do not implement Story 4.2 workspace preparation, clone/materialization, workspace lock, file operations, commits, cleanup, repair automation, or context queries.
- Do not create or bind repositories in the branch/ref policy operation.
- Do not call provider adapters or provider APIs from aggregates, REST endpoints, query handlers, or the branch/ref policy command service.
- Do not add CLI commands, MCP tools, UI pages, operations-console mutation controls, or generated SDK hand edits.
- Do not expose raw provider credentials, tokens, private keys, embedded credential URLs, clone URLs, owner/repository labels, installation IDs, raw provider payloads, raw exception text, raw branch names, file contents, diffs, generated context payloads, raw claim bags, or unauthorized resource existence.
- Do not add live GitHub/Forgejo calls to PR-gate tests.
- Do not add broad CI workflow files unless an owning CI story explicitly targets CI.
- Do not use recursive submodule initialization.

### Testing

- Use xUnit v3 and Shouldly. Prefer small recording fakes over broad mocks.
- Aggregate tests should seed an active bound folder through existing `FolderCreated` plus `RepositoryBound` or accepted repository-binding events, then apply `ConfigureBranchRefPolicy`.
- Service/order tests should prove denial and non-bound-state paths happen before readiness, provider binding, credential, provider adapter, workspace, audit, cache, metric, log, or diagnostics observation.
- Endpoint tests should build a slim `WebApplication`, map server endpoints, and assert route registration plus safe `202`/Problem Details/read-model behavior.
- Read-model tests should assert authorized success, safe denial, stale/unavailable projection handling, freshness metadata, and redaction/unknown/missing distinction.
- Contract tests must run when OpenAPI, parity, generated SDK, idempotency helper, or previous-spine artifacts change.
- Safety tests must scan any new policy diagnostics/read-model output channel for branch names, repository URLs, file content, diffs, provider payloads, credentials, and unauthorized-resource sentinels.

### Regression Traps

- Do not require `ProviderOperationCatalog.RepositoryCreation` when validating `ProviderReadinessRequestedCapability.BranchRefPolicy`; that would block policy configuration for folders bound to existing repositories or providers without repository-creation capability.
- Do not accept branch/ref policy configuration before a repository binding is `Bound`.
- Do not let `repositoryBindingId` in the request select or override a repository binding from another folder or tenant; it must match the authorized folder's current bound state.
- Do not store raw branch names just because the field is called `defaultRef`; the current contract requires safe `branch_ref_*` metadata tokens.
- Do not treat `protectedRefPatterns` omission and an empty list as the same if the helper/spine treats presence differently; follow generated idempotency semantics.
- Do not sort or deduplicate allowed/protected patterns silently unless the Contract Spine and helper semantics are updated; otherwise replay hashes and runtime equivalence will drift.
- Do not use lifecycle status projection as the branch/ref policy read model unless it can preserve policy metadata, evidence scope, and freshness without overloading repository-binding status semantics.
- Do not let `GetBranchRefPolicy` expose policy existence to unauthorized callers through different 404/403/body/freshness shapes.
- Do not add raw provider details to event names, Problem Details messages, log templates, projection keys, metric labels, or test failure messages.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 3.8`
- `_bmad-output/planning-artifacts/prd.md#Provider-Readiness-and-Repository-Binding`
- `_bmad-output/planning-artifacts/prd.md#Technical-Success`
- `_bmad-output/planning-artifacts/architecture.md#Requirements-to-Structure-Mapping`
- `_bmad-output/project-context.md#Critical-Dont-Miss-Rules`
- `_bmad-output/implementation-artifacts/3-5-validate-provider-readiness-with-safe-diagnostics.md`
- `_bmad-output/implementation-artifacts/3-6-create-a-new-repository-backed-folder.md`
- `_bmad-output/implementation-artifacts/3-7-bind-an-existing-repository-to-a-folder.md`
- `docs/contract/idempotency-and-parity-rules.md`
- `docs/contract/sdk-generation-and-idempotency-helpers.md`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`
- `tests/fixtures/parity-contract.yaml`
- `tests/fixtures/previous-spine.yaml`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs`
- `src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs`
- `src/Hexalith.Folders/Aggregates/Folder/RepositoryBackedFolderCreationService.cs`
- `src/Hexalith.Folders/Aggregates/Folder/RepositoryBindingService.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderReadinessValidationService.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderOperationCatalog.cs`
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs`
- `src/Hexalith.Folders.Server/FoldersServerModule.cs`
- `src/Hexalith.Folders.Server/Authorization/FolderCommandActionTokenMapper.cs`
- `tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs`

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-27 | Created story with branch/ref policy runtime scope, concrete contract/runtime gaps, readiness capability correction, projection/read-model expectations, and offline validation guardrails. | Codex |
| 2026-05-27 | Implemented branch/ref policy aggregate/service/REST/read-model path and focused tests; validation blocked locally because the installed .NET SDK is 10.0.108 while global.json requires 10.0.300. | Codex |
| 2026-05-27 | Fixed branch/ref policy projection freshness clamping and added a focused read-model regression test; validation remains blocked by the local SDK mismatch. | Codex |
| 2026-05-27 | Fixed a branch/ref policy read-model test compile issue and reran validation; build is now blocked by unavailable NuGet packages/network in the sandbox. | Codex |
| 2026-05-27 | Fixed branch/ref policy compile gaps, expanded aggregate guard tests, refreshed generated helper hashes through the existing build pipeline, and reran serial builds; test execution is blocked by sandbox socket denial. | Codex |
| 2026-05-27 | Completed dev-story bookkeeping after parent validation passed with SDK 10.0.300; story is ready for review. | Codex |
| 2026-05-27 | Senior review fixed branch/ref policy read-model request scoping, added a regression endpoint test, reconciled File List gaps, and marked the story done. | Codex |

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-27: Resolved `bmad-dev-story` customization; no activation prepend/append steps. Loaded `_bmad-output/project-context.md` plus sibling module project contexts.
- 2026-05-27: Contract reconciliation found existing `ConfigureBranchRefPolicy`/`GetBranchRefPolicy` OpenAPI operations, parity rows, previous-spine entries, generated SDK methods, and `BranchRefPolicyRequest.ComputeIdempotencyHash(string folderId)` helper shape. No Contract Spine or generated SDK edits were made.
- 2026-05-27: `dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore` failed before MSBuild because `global.json` requests SDK `10.0.300`; only SDK `10.0.108` is installed.
- 2026-05-27: `dotnet test tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore`, `dotnet test tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore`, and the focused client helper test command all failed for the same SDK-resolution blocker.
- 2026-05-27: Re-ran validation after the projection freshness fix. `dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore`, `dotnet test tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore`, `dotnet test tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore`, and `dotnet test tests/Hexalith.Folders.Client.Tests/Hexalith.Folders.Client.Tests.csproj --no-restore --filter BranchRefPolicyHelperUsesDeclaredSpineFields` all failed before MSBuild/test discovery because the installed SDK is `10.0.108` and `global.json` requires `10.0.300`.
- 2026-05-27: Fixed `tests/Hexalith.Folders.Tests/Queries/BranchRefPolicyReadModelTests.cs` to cast the appended event to `BranchRefPolicyConfigured` before reading `IdempotencyFingerprint`.
- 2026-05-27: `git diff --check` passed.
- 2026-05-27: Re-ran validation with `DOTNET_CLI_HOME=/tmp/sa-codex-dotnet-home DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1`. `dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore` failed because NuGet restore/package resolution attempted to reach `https://api.nuget.org/v3/index.json` and the sandbox has no usable local package cache; reported `NU1801` and `NU1101` for `Microsoft.Extensions.*` packages and `Octokit`. The `dotnet test --no-build` invocations are not counted as validation because the current source could not be built.
- 2026-05-27: Current SDK availability is now correct (`10.0.300`). Fixed `BranchRefPolicyConfigurationService` by importing `Hexalith.Folders.Providers.Abstractions` for `ProviderFailureCategory`.
- 2026-05-27: Fixed branch/ref policy test compile gaps by importing provider abstractions in `FolderBranchRefPolicyConfigurationServiceTests` and the aggregate test helper namespace in `BranchRefPolicyReadModelTests`.
- 2026-05-27: Expanded `FolderBranchRefPolicyAggregateTests` with missing, archived, unbound, binding-requested, failed, unknown-provider-outcome, reconciliation-required, idempotent replay, and idempotency conflict guards.
- 2026-05-27: Normalized generated local `obj/project.assets.json` paths to the mounted Windows NuGet cache for validation only; no source or tracked contract artifact was edited for this.
- 2026-05-27: `dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore` passed.
- 2026-05-27: `dotnet build src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1` passed.
- 2026-05-27: `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1` passed.
- 2026-05-27: `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1` passed.
- 2026-05-27: `dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1` passed.
- 2026-05-27: `NUGET_PACKAGES=/mnt/c/Users/JeromePiquot/.nuget/packages NuGetAudit=false TreatWarningsAsErrors=false dotnet build tests/Hexalith.Folders.Client.Tests/Hexalith.Folders.Client.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1` passed after restoring the helper generator from the local package cache with NuGet audit disabled for offline validation.
- 2026-05-27: `dotnet test ... --no-build -p:IsTestProject=true` for core, server, and client focused test filters all aborted before discovery/execution with `System.Net.Sockets.SocketException (13): Permission denied` because VSTest attempts to open a local TCP listener and the sandbox denies sockets.
- 2026-05-27: `git diff --check` passed after the compile fixes.
- 2026-05-27: Inspected current source and tests against the remaining story checklist. Parent validation with SDK `10.0.300` passed the focused core, server, and client test filters plus `git diff --check`; local child validation confirmed SDK `10.0.300`, `git diff --check`, focused core/server filters, contract group filter, and safety invariant filter exit `0`. The focused client filter remains blocked only by child VSTest socket creation (`SocketException (13): Permission denied`).
- 2026-05-27: Senior review found that the in-memory branch/ref policy read model reused the configure command's correlation/task evidence as the read scope, making later authorized reads with a different request scope fail compatibility. Fixed the read model to scope returned snapshots to the current authorized read request and added `GetBranchRefPolicyShouldAllowLaterAuthorizedReadsWithNewRequestScope`.
- 2026-05-27: Senior review validation: `dotnet --version` returned `10.0.300`; `git diff --check` passed; `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1` passed; `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1` passed; focused `dotnet test --no-build` commands for branch/ref policy returned exit code `0` in this sandbox.

### Completion Notes List

- Added branch/ref policy command, request, event, state metadata, aggregate decision logic, strict branch/ref token validation, idempotency fingerprinting aligned to the existing spine field order, and replay application.
- Added `BranchRefPolicyConfigurationService` with authorization-before-observation, bound-folder checks before readiness, idempotency replay/conflict checks before readiness, `ProviderReadinessRequestedCapability.BranchRefPolicy`, and append-through-repository behavior.
- Corrected provider readiness required operations so `BranchRefPolicy` requires branch/ref inspection and common lifecycle support without requiring repository creation.
- Wired REST `PUT` and `GET /api/v1/folders/{folderId}/branch-ref-policy`, server command type/action-token mapping, domain processor deserialization, DI registration, and an in-memory branch/ref policy read model/query handler.
- Added focused aggregate, service/order, projection/read-model, endpoint, and readiness tests. These are unverified locally because package restore/build is blocked by NuGet network/cache availability.
- Fixed in-memory branch/ref policy projection freshness so a later repository observation timestamp cannot be dropped behind an older policy event timestamp, and added a focused regression test for that behavior.
- Fixed the branch/ref policy read-model tests to use the concrete `BranchRefPolicyConfigured` event before reading its idempotency fingerprint.
- Fixed current compile gaps in the branch/ref policy service and tests, and expanded aggregate guard coverage for all invalid repository-binding states required by the story.
- Current source builds pass for core, server, core tests, server tests, contracts tests, and client tests. Focused parent validation passed for the Story 3.8 core, server, and client test filters; child validation rechecked the available lanes and confirmed the only remaining local blocker is VSTest socket creation in the client test lane.
- Story 3.8 checklist is complete and the story is ready for code review.
- Senior review fixed branch/ref policy read-model request scoping so authorized policy reads are not pinned to the original configure command's correlation/task identifiers.
- Senior review corrected File List omissions for endpoint and action-catalog tests and completed sprint/story status sync.

### File List

- `_bmad-output/implementation-artifacts/3-8-define-branch-and-ref-policy.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs`
- `src/Hexalith.Folders/Aggregates/Folder/BranchRefPolicyConfigurationRequest.cs`
- `src/Hexalith.Folders/Aggregates/Folder/BranchRefPolicyConfigurationService.cs`
- `src/Hexalith.Folders/Aggregates/Folder/BranchRefPolicyConfigured.cs`
- `src/Hexalith.Folders/Aggregates/Folder/BranchRefPolicyMetadata.cs`
- `src/Hexalith.Folders/Aggregates/Folder/ConfigureBranchRefPolicy.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderResult.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs`
- `src/Hexalith.Folders/Aggregates/Folder/IBranchRefPolicyReadinessValidator.cs`
- `src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs`
- `src/Hexalith.Folders/Aggregates/Folder/ProviderReadinessBranchRefPolicyValidator.cs`
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs`
- `src/Hexalith.Folders/Queries/Folders/BranchRefPolicyQuery.cs`
- `src/Hexalith.Folders/Queries/Folders/BranchRefPolicyQueryHandler.cs`
- `src/Hexalith.Folders/Queries/Folders/BranchRefPolicyQueryResult.cs`
- `src/Hexalith.Folders/Queries/Folders/BranchRefPolicyQueryResultCode.cs`
- `src/Hexalith.Folders/Queries/Folders/BranchRefPolicyReadModelRequest.cs`
- `src/Hexalith.Folders/Queries/Folders/BranchRefPolicyReadModelResult.cs`
- `src/Hexalith.Folders/Queries/Folders/BranchRefPolicyReadModelSnapshot.cs`
- `src/Hexalith.Folders/Queries/Folders/BranchRefPolicyReadModelStatus.cs`
- `src/Hexalith.Folders/Queries/Folders/IBranchRefPolicyReadModel.cs`
- `src/Hexalith.Folders/Queries/Folders/InMemoryBranchRefPolicyReadModel.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderReadinessValidationService.cs`
- `src/Hexalith.Folders.Server/Authorization/FolderCommandActionTokenMapper.cs`
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs`
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`
- `src/Hexalith.Folders.Server/FoldersServerModule.cs`
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs`
- `tests/Hexalith.Folders.Server.Tests/BranchRefPolicyEndpointTests.cs`
- `tests/Hexalith.Folders.Server.Tests/RepositoryBackedFolderEndpointTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderBranchRefPolicyAggregateTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderBranchRefPolicyConfigurationServiceTests.cs`
- `tests/Hexalith.Folders.Tests/Authorization/BranchRefPolicyActionCatalogTests.cs`
- `tests/Hexalith.Folders.Tests/Providers/Readiness/ProviderReadinessValidationServiceTests.cs`
- `tests/Hexalith.Folders.Tests/Queries/BranchRefPolicyReadModelTests.cs`

## Senior Developer Review (AI)

Reviewer: Codex on 2026-05-27

### Findings Fixed

- [HIGH] `GetBranchRefPolicy` compatibility was tied to the original configure command scope. `InMemoryFolderRepository` saved branch/ref policy snapshots with the configure event's `TaskId` and `CorrelationId`, while `BranchRefPolicyQueryHandler` requires the snapshot evidence scope to match the current read request. A later authorized read with a new task/correlation therefore returned unavailable instead of policy metadata. Fixed by having `InMemoryBranchRefPolicyReadModel.GetAsync` scope the returned snapshot to the authorized read request, and added endpoint regression coverage.
- [MEDIUM] Story File List omitted new implementation files present in git: `tests/Hexalith.Folders.Server.Tests/BranchRefPolicyEndpointTests.cs` and `tests/Hexalith.Folders.Tests/Authorization/BranchRefPolicyActionCatalogTests.cs`. Fixed by adding both files to the File List.
- [LOW] Review validation evidence was stale after the read-model scope fix. Updated the Dev Agent Record with the senior-review validation commands and results.

### Validation Checklist

- [x] Story file loaded from `_bmad-output/implementation-artifacts/3-8-define-branch-and-ref-policy.md`
- [x] Story Status verified as reviewable (`review`) before review and updated to `done`
- [x] Epic and Story IDs resolved (`3.8`)
- [x] Story Context located or warning recorded
- [x] Epic Tech Spec located or warning recorded
- [x] Architecture/standards docs loaded (`_bmad-output/project-context.md`)
- [x] Tech stack detected and documented (.NET 10 / C# / xUnit / Shouldly)
- [x] MCP doc search performed or web fallback: not applicable; no external API/library behavior needed review, and network is restricted
- [x] Acceptance Criteria cross-checked against implementation
- [x] File List reviewed and validated for completeness
- [x] Tests identified and mapped to ACs; gap fixed for later read request scope
- [x] Code quality review performed on changed files
- [x] Security review performed on changed files and dependencies
- [x] Outcome decided: Approve after fixes
- [x] Review notes appended under "Senior Developer Review (AI)"
- [x] Change Log updated with review entry
- [x] Status updated according to settings
- [x] Sprint status synced
- [x] Story saved successfully
