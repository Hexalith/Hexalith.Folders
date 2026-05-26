---
baseline_commit: b0c5efabcef94cfbfefe281dec33a3a82807df85
---

# Story 3.5: Validate provider readiness with safe diagnostics

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a platform engineer,
I want to validate provider readiness before repository-backed creation or binding,
so that configuration failures are caught before workspace tasks begin.

## Terms

- Provider readiness validation means the non-mutating query/status operation `ValidateProviderReadiness` at `POST /api/v1/provider-readiness/validations`; it must not accept `Idempotency-Key`.
- Safe diagnostics means machine-readable, metadata-only readiness evidence: readiness state, safe reason code, retryability, remediation category, opaque provider reference, freshness, and correlation ID. It never includes credential values, provider tokens, raw provider payloads, raw exception text, repository URLs with credentials, branch secrets, file contents, diffs, or unauthorized resource existence.
- Provider reference means an opaque, authorized identifier such as `providerBindingRef` or `capabilityProfileRef`. It is not a provider account, installation ID, repository URL, raw owner/repository label, or credential value.
- Readiness state means the existing Contract Spine readiness vocabulary (`ready`, `degraded`, `failed`) mapped from the Epic 3 ready/failed gate, not a new lifecycle state model.
- Safe reason code means a stable code derived from existing provider failure/result categories and adapter reason codes, such as `provider_configuration_missing`, `provider_permission_insufficient`, `provider_rate_limited`, `unsupported_provider_capability`, `provider_readiness_failed`, `unknown_provider_outcome`, or `reconciliation_required`.
- Remediation category means a bounded safe action hint such as no action, retry later, contact operator, fix credential reference, fix provider configuration, or reconciliation required. It must not embed provider-specific payloads or secret-bearing values.
- Consumer audience means the redacted response shape already defined by the Contract Spine: status, retry hint, and freshness only.
- Authorized operator audience means the sanitized diagnostic response shape already defined by the Contract Spine: provider binding reference, capability profile reference, capability evidence, sanitized failure category, freshness, and any required Story 3.5 fields.

## Acceptance Criteria

1. Given Story 3.1 provider binding metadata, Story 3.2 provider capability abstractions, and Story 3.3/3.4 GitHub and Forgejo adapters exist, when provider readiness validation is implemented, then it reuses those existing models and services instead of creating a second provider readiness, capability, credential, failure, or diagnostic taxonomy.
2. Given an authenticated caller requests readiness validation, when the server handles `POST /api/v1/provider-readiness/validations`, then the operation is treated as a non-mutating query/status path with `x-hexalith-read-consistency`, caller-provided or generated `X-Correlation-Id`, optional freshness header handling, and no `Idempotency-Key` acceptance or idempotency persistence.
3. Given the actor lacks tenant access, provider-readiness read authority, or authoritative tenant evidence is disabled, stale, unavailable, malformed, mismatched, or replay-conflicting, when readiness validation is attempted, then authorization and tenant/organization/provider-binding scoping complete before organization state load, provider binding observation, credential reference inspection, provider resolver lookup, provider adapter construction, capability cache lookup, provider API call, audit/evidence write, metrics/log enrichment, or diagnostics; denied and missing paths do not confirm whether the organization, provider binding, credential reference, provider family, repository, branch/ref, or capability evidence exists.
4. Given a tenant has provider binding metadata, when readiness validation is allowed, then the implementation builds the provider capability discovery request from the authorized organization-scoped binding state, opaque credential reference metadata, branch/ref policy metadata, naming policy metadata, provider family/key, safe target evidence, authorization snapshot, and correlation ID; it does not trust tenant, provider, credential, owner, repository, branch, base URL, or target labels supplied by the request body as authority.
5. Given the provider binding metadata is missing, malformed, disabled, unsupported, stale, mismatched, or references unsupported capability evidence, when readiness validation runs after authorization, then the response returns a safe failed/degraded readiness state with stable reason code, retryability, remediation category, provider reference when authorized, freshness, and correlation ID; it must not reveal raw binding metadata, credential values, provider account identity, or unauthorized resource existence.
6. Given readiness validation reaches provider capability discovery, when `ProviderCapabilityDiscoveryService` and the selected `IGitProvider` return success, then the readiness result reports `ready` only when required MVP operations are available according to the existing capability profile: readiness validation, repository creation or binding as requested, branch/ref inspection, file mutation support, commit support, status query, provider support evidence, safe target evidence, rate-limit posture, and profile schema/version. Partial, unavailable, emulated, stale, or unsupported capability rows must map to safe degraded/failed states according to the existing provider failure categories rather than raw GitHub or Forgejo semantics.
7. Given provider capability discovery returns failure, when the readiness response is mapped, then `ProviderCapabilityDiscoveryResult.FailureCategory`, `CategoryCode`, `ReasonCode`, `SafeRemediationCode`, `Retryable`, `RetryAfter`, `CorrelationId`, and optional profile version are preserved as metadata-only readiness diagnostics. Permanent categories such as unsupported capability, validation failure, permission insufficient, configuration missing, and known non-retryable readiness failures must not be marked retryable; transient provider unavailable, rate limited, and transient failure categories may carry retry hints only when safe.
8. Given GitHub and Forgejo adapter-specific differences exist, when readiness validation evaluates them, then product workflows consume canonical capability/result categories and metadata only. The implementation must not treat Forgejo as a GitHub base-URL swap, must not branch public semantics on provider-specific endpoint details, and must not flatten Forgejo version/drift evidence or GitHub API-version/permission evidence into raw public fields.
9. Given readiness evidence is emitted to HTTP responses, projection/evidence stores, audit metadata, logs, traces, metrics, diagnostics, generated artifacts, or test output, when sentinel values for provider tokens, JWTs, PEM/private keys, credential URLs, repository URLs with userinfo, raw provider payloads, branch names with secrets, file contents, diffs, owner names, repository names, emails, display names, base URLs, installation IDs, and unauthorized resource names are present in inputs or fake provider responses, then all observable outputs remain metadata-only and tests fail if forbidden values appear.
10. Given the current Contract Spine already defines provider-readiness validation, when implementation discovers that the schema or generated client cannot carry Story 3.5 required fields such as safe reason code, retryability, remediation category, provider reference, or correlation ID for the needed audience, then the fix must go through the established Contract Spine path: update `hexalith.folders.v1.yaml`, contract tests, parity oracle rows, and generated client output together. Generated SDK files under `src/Hexalith.Folders.Client/Generated/` must not be hand-edited.
11. Given readiness validation stores or compares evidence, when cache keys, fingerprints, projection keys, or profile references are produced, then they include tenant, organization, provider binding, provider family/key, credential mode, safe target evidence, provider API/snapshot version, profile schema/version, authorization evidence freshness, and correlation/operation context where required; they must never be global by provider family or instance URL alone and must never include raw provider payloads or credentials.
12. Given unit, contract, and server tests run in a developer machine or CI PR gate, then readiness validation tests pass offline with fake provider capability services, fake GitHub/Forgejo seams, and in-memory stores; they do not require live GitHub, live Forgejo, provider credentials, tenant seed data, Aspire, Dapr sidecars, Redis, Keycloak, Docker, network access, or nested submodule initialization.
13. Given this story is complete, when Story 3.6 or Story 3.7 later creates or binds repository-backed folders, then those workflows can consume readiness outcomes as a precondition without re-querying raw provider details, re-resolving credentials before authorization, or inventing new readiness result categories.

## Tasks / Subtasks

- [x] Add the provider-readiness validation application/query service. (AC: 1, 3, 4, 6, 7, 11)
  - [x] Add a focused provider-readiness query/service area under the existing core project, such as `src/Hexalith.Folders/Queries/ProviderReadiness/` or an already established provider-readiness folder if present.
  - [x] Define one-public-type-per-file request/result models for runtime readiness validation only if existing provider abstraction models cannot carry the needed service result cleanly.
  - [x] Use the existing `ProviderCapabilityDiscoveryService`, `ProviderCapabilityDiscoveryRequest`, `ProviderCapabilityDiscoveryResult`, `ProviderCapabilityProfile`, `ProviderOperationCatalog`, `ProviderFailureCategory`, and `ProviderFailureCategoryExtensions`.
  - [x] Do not create a second `IGitProvider`, provider capability profile, credential mode model, or failure category enum.
  - [x] Build discovery requests from authorized organization provider binding state and safe target evidence; do not accept request-body tenant/provider/credential/target labels as authority.
  - [x] Add profile-to-readiness mapping that checks required MVP capability rows for readiness validation, repository creation or binding, branch/ref inspection, file mutation support, commit support, status query, provider support evidence, and rate-limit posture.
  - [x] Preserve `Retryable`, `RetryAfter`, `SafeRemediationCode`, `ReasonCode`, and `CorrelationId` from provider capability discovery results.
- [x] Enforce authorization before observation for readiness validation. (AC: 3, 4, 5, 11)
  - [x] Reuse existing tenant-access and layered authorization concepts before organization/provider-binding lookup.
  - [x] Use the Contract Spine requirement `tenant-context-and-provider-readiness-read`; if concrete action-token mapping is missing, add the narrowest project-local mapping required by current authorization patterns and cover it with tests.
  - [x] Ensure denied, missing, stale, unavailable, malformed, mismatched, and replay-conflicting tenant/authorization paths make zero calls to provider binding repositories, provider resolvers, credential resolvers, GitHub/Forgejo seams, capability/evidence stores, audit writers, metrics, logs, or diagnostics.
  - [x] Treat payload tenant values as comparison inputs only; authoritative tenant comes from authentication context and EventStore/authorization evidence.
  - [x] Keep the reserved `system` tenant out of managed tenant organization streams.
  - [x] Return existing safe denial/read-model unavailable families without exposing whether a provider binding or credential reference exists.
- [x] Wire the canonical REST route without changing operation semantics. (AC: 2, 5, 7, 10)
  - [x] Map `POST /api/v1/provider-readiness/validations` in the server endpoint composition, ideally through a focused endpoint file if the existing `FoldersDomainServiceEndpoints.cs` becomes too broad.
  - [x] Reject `Idempotency-Key` on this non-mutating operation if current request plumbing would otherwise accept it.
  - [x] Support the declared read consistency/freshness behavior from the Contract Spine and reject unsupported freshness classes with safe validation Problem Details.
  - [x] Echo only safe correlation metadata in headers/body and guard against control characters or response-splitting values.
  - [x] Map provider unavailable/rate-limited/readiness-failed outcomes to the canonical Problem Details categories declared in the OpenAPI contract when the operation must fail at HTTP level.
  - [x] Return the existing audience-partitioned readiness response shape for success/degraded/failed results; if the current schema cannot carry required Story 3.5 fields, update the Contract Spine and generated artifacts through the established process.
- [x] Preserve or update Contract Spine, parity, and generated-client artifacts deliberately. (AC: 2, 10)
  - [x] Review `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`, `docs/contract/tenant-folder-provider-repository-contract-groups.md`, `docs/contract/idempotency-and-parity-rules.md`, `tests/fixtures/parity-contract.yaml`, and `TenantFolderProviderContractGroupTests` before changing public shapes.
  - [x] Keep `ValidateProviderReadiness` as a non-mutating POST-as-query operation with no idempotency key.
  - [x] If fields are added or clarified, update examples for both consumer and authorized-operator audiences and keep unauthorized examples free of provider identifiers.
  - [x] Regenerate or update the generated SDK through the existing NSwag pipeline; do not hand-edit `HexalithFoldersClient.g.cs`.
  - [x] Re-run contract/parity/safety tests that guard provider-readiness examples, generated SDK output, and parity rows.
- [x] Add metadata-only readiness/evidence storage only where needed. (AC: 5, 8, 9, 11, 13)
  - [x] Extend or add in-memory provider-readiness evidence/read-model support only if runtime validation needs to persist the result for later repository creation/binding stories.
  - [x] Store only safe dimensions: tenant, organization, provider binding reference, provider family/key, capability profile reference/fingerprint, readiness status, reason code, retryability, remediation category, observed-at timestamp, freshness/projection watermark, and correlation ID.
  - [x] Do not store credential values, credential health payloads, provider installation identity, raw owner/repository labels, raw base URLs, raw provider responses, raw headers, branch secrets, file paths/content, diffs, or exception messages.
  - [x] Ensure later Story 3.6/3.7 consumers can tell whether readiness is green, failed, stale, unavailable, or reconciliation-required without parsing provider-specific adapter data.
- [x] Add offline provider-readiness tests. (AC: 1-13)
  - [x] Add core tests under `tests/Hexalith.Folders.Tests/Providers/Readiness/` or the established provider-readiness test location.
  - [x] Cover ready, degraded, failed, missing binding, unsupported provider, stale authorization evidence, stale target evidence, missing credential reference, permission insufficient, provider unavailable, provider rate limited, provider readiness failed, unsupported capability, known provider failure, unknown provider outcome, reconciliation required, malformed request, and read-model unavailable cases.
  - [x] Add zero-touch authorization tests proving denied paths do not call provider binding repositories, provider capability resolvers, credential resolvers, GitHub/Forgejo API seams, evidence stores, audit writers, metrics/log enrichment, or diagnostics.
  - [x] Add endpoint tests under `tests/Hexalith.Folders.Server.Tests/` proving the route is registered, `Idempotency-Key` is rejected, unsupported freshness is rejected safely, correlation is echoed safely, and safe denial bodies do not include provider binding or credential identifiers.
  - [x] Add contract/generation tests only if public OpenAPI or generated client artifacts change.
  - [x] Add leakage tests that serialize readiness requests/results, Problem Details, evidence records, profile projections, and fake logs/exceptions, then scan for provider secret and raw payload sentinels.
- [x] Preserve scope boundaries. (AC: 1, 8, 12, 13)
  - [x] Do not implement repository creation, repository binding, branch/ref policy mutation, workspace preparation, locks, file mutation, commits, status console pages, CLI commands, MCP tools, UI pages, repair workflows, live provider drift checks, or nightly CI workflows in this story.
  - [x] Do not add live GitHub or live Forgejo calls to PR-gate tests.
  - [x] Do not add package versions directly to `.csproj` files.
  - [x] Do not initialize nested submodules or use recursive submodule commands.

## Dev Notes

### Source Context

- Epic 3 requires provider configuration, readiness validation, repository-backed folder creation/binding, branch/ref policy, and provider capability evidence without exposing secrets. Story 3.5 specifically validates readiness before repository-backed creation or binding and returns safe diagnostics. [Source: `_bmad-output/planning-artifacts/epics.md#Story 3.5`]
- Epic 3 guardrails require explainable readiness states, degraded states, safe blockers, retryability, and secret-safe evidence that Epic 6 can render without UI-only semantics. [Source: `_bmad-output/planning-artifacts/epics.md#Epic 3: Provider Readiness And Repository Binding`]
- PRD provider readiness must validate provider configuration, credential reference availability, required GitHub/Forgejo capabilities, repository provisioning readiness, and default branch policy before repository-backed folder creation. [Source: `_bmad-output/planning-artifacts/prd.md#Technical Success`]
- PRD Journey 2 requires a stable machine-readable reason code and safe diagnosis for missing credential reference, insufficient permission, provider unavailable, or invalid default branch policy; secrets are never displayed. [Source: `_bmad-output/planning-artifacts/prd.md#Journey 2: Platform Engineer Establishes Tenant Provider Readiness`]
- Architecture places FR15-FR23 across `src/Hexalith.Folders/Aggregates/Organization/`, `src/Hexalith.Folders/Providers/{Abstractions,GitHub,Forgejo}/`, and contract/provider surfaces. [Source: `_bmad-output/planning-artifacts/architecture.md#Requirements to Structure Mapping`]
- Architecture requires provider adapters behind `IGitProvider`, capability-tested GitHub and Forgejo support, known-vs-unknown provider failure classification, hermetic PR-gate provider tests, and no silent retry for unknown outcomes. [Source: `_bmad-output/planning-artifacts/epics.md#AR-PROVIDER-01`; `_bmad-output/planning-artifacts/epics.md#AR-PROVIDER-05`; `_bmad-output/planning-artifacts/epics.md#AR-PROVIDER-06`]
- Contract notes state `ValidateProviderReadiness` is a non-mutating POST-as-query operation because the caller passes structured request data; it carries read consistency and must not accept `Idempotency-Key`. [Source: `docs/contract/tenant-folder-provider-repository-contract-groups.md#POST-as-query-Exception`]
- Project context requires zero cross-tenant leakage, metadata-only diagnostics, provider adapters behind provider ports, and no recursive nested submodule initialization. [Source: `_bmad-output/project-context.md#Critical Don't-Miss Rules`]

### Previous Story Intelligence

- Story 3.1 implemented organization-scoped provider bindings on `OrganizationAggregate`, with opaque credential references, provider kind, naming/branch policies, deterministic idempotency/conflict handling, secret-shaped input rejection, and metadata-only replay into `OrganizationState.ProviderBindings`.
- Story 3.1 also added `OrganizationProviderBindingTenantGate` and tests proving tenant/access denial happens before idempotency, duplicate, or binding observation. Story 3.5 should follow that no-touch pattern for readiness reads.
- Story 3.2 implemented the internal `IGitProvider` port as capability-profile discovery and comparison only. It added provider failure categories, retry semantics, deterministic fingerprints, authorization-before-observation seams, fake providers, leakage tests, and no public Contract Spine changes.
- Story 3.2 review fixed `UnsupportedProviderCapability` retryability to permanent/non-retryable. Story 3.5 must not reintroduce retry loops for unsupported provider capability.
- Story 3.3 implemented the GitHub adapter behind `IGitProvider`, including Octokit isolation, credential lease disposal, GitHub API version evidence, safe target fingerprints, permission/rate-limit mapping, canonical failure mapping, and metadata-only leakage tests.
- Story 3.4 implemented the Forgejo adapter behind `IGitProvider`, including typed HTTP seams, supported-version snapshot selection, authorized base URL canonicalization, redirect protections, Forgejo drift evidence, canonical failure mapping, and sanitized local drift report tooling.
- Story 3.4 senior review found a real fail-closed risk around unsupported Forgejo versions and fixed exact manifest matching. Story 3.5 must preserve exact version/snapshot evidence and fail closed when readiness cannot be proven.
- Story 1.7, Story 1.12, and Story 1.13 already own provider-readiness OpenAPI operation declarations, generated SDK boundaries, and parity rows. Public contract changes must use those established gates.

### Existing Implementation State

- `src/Hexalith.Folders/Providers/Abstractions/IGitProvider.cs` currently exposes provider family/key, `DiscoverCapabilitiesAsync`, and profile comparison.
- `ProviderCapabilityDiscoveryService` already authorizes first, records an attempt only after authorization succeeds, resolves the provider by normalized family/key, and delegates to the selected provider.
- `ProviderCapabilityDiscoveryResult` already carries `IsSuccess`, `Profile`, `FailureCategory`, `CategoryCode`, `ReasonCode`, `SafeRemediationCode`, `Retryable`, `RetryAfter`, `CorrelationId`, and `ProfileVersion`.
- `ProviderFailureCategoryExtensions.IsRetryableByDefault()` currently marks only provider unavailable, rate limited, and transient failure as retryable.
- `GitHubProvider` and `ForgejoProvider` already produce metadata-only capability profiles and safe failures through their adapter-specific readiness mappers and failure mappers.
- `OrganizationState` already stores provider bindings keyed by `ProviderBindingRef` and replays `ProviderBindingConfigured` events into metadata-only binding state.
- The server currently maps domain service, archive, effective-permissions, and folder lifecycle-status endpoints in `FoldersDomainServiceEndpoints.cs`; there is no runtime `ValidateProviderReadiness` endpoint wired yet.
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` already declares `/api/v1/provider-readiness/validations`, `ValidateProviderReadiness`, `ProviderReadiness`, consumer/operator readiness examples, and related canonical error categories.
- The generated client currently includes `ValidateProviderReadinessAsync(...)`; if runtime fields required by this story are not represented correctly in generated types, fix the OpenAPI/generation path rather than editing generated code.
- `tests/fixtures/safety-channel-inventory.json` already assigns the `provider-diagnostics` channel to this story and scans OpenAPI/generated-client diagnostic artifacts for leakage.

### Required Architecture Patterns

- Use .NET 10, file-scoped namespaces, nullable-aware C#, one public type per file, PascalCase public members, camelCase locals/parameters, and async APIs with `CancellationToken`.
- Keep `Hexalith.Folders.Contracts` behavior-free. Contract DTOs and OpenAPI artifacts describe wire shape; readiness decisions live in core/server code.
- Keep aggregates pure. `OrganizationAggregate` and `FolderAggregate` must not call providers, resolve credentials, hit Dapr/HTTP/Git/filesystem, write audit, or emit diagnostics directly.
- Keep provider-specific details inside `Providers/GitHub` and `Providers/Forgejo`. Product readiness logic consumes provider capability profiles and canonical categories only.
- Keep external REST path naming lowercase/hyphen-delimited under `/api/v1/provider-readiness`.
- Use `StringComparison.Ordinal` and `StringComparer.Ordinal` for identifiers, capability keys, failure codes, and evidence dictionary keys.
- Preserve metadata-only structured diagnostics. Never log or return raw provider payloads, raw credential references, provider tokens, repository URLs with credentials, file contents, diffs, branch secrets, display names, emails, or unauthorized existence details.
- Treat capability/readiness evidence as tenant/organization/provider-binding scoped. No global readiness cache by provider family, provider key, instance URL, or credential mode alone.
- Keep local and PR-gate validation hermetic. Live provider drift and production readiness smoke tests are outside this story.

### Files To Touch

- `src/Hexalith.Folders/Queries/ProviderReadiness/*` or an existing provider-readiness query/service folder if present
- `src/Hexalith.Folders/Providers/Abstractions/*` only if a narrow Story 3.5 runtime mapping gap is found; avoid changing the `IGitProvider` contract unless unavoidable
- `src/Hexalith.Folders/Aggregates/Organization/IOrganizationProviderBindingRepository.cs` or a read-side equivalent only if a safe authorized binding-read seam is missing
- `src/Hexalith.Folders/Authorization/*` only for the narrow provider-readiness read authorization mapping required by the Contract Spine
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs` for DI registration of provider-readiness services/read models/fakes where appropriate
- `src/Hexalith.Folders.Server/FoldersServerModule.cs`
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs` or a new `ProviderReadinessEndpoints.cs` if the route is split out
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs`
- `tests/Hexalith.Folders.Tests/Providers/Readiness/*`
- `tests/Hexalith.Folders.Server.Tests/*ProviderReadiness*Tests.cs`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/TenantFolderProviderContractGroupTests.cs` only if public provider-readiness contract assertions need tightening
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`, `tests/fixtures/parity-contract.yaml`, and generated client artifacts only if the existing Contract Spine cannot carry required Story 3.5 diagnostics
- `tests/fixtures/safety-channel-inventory.json` only if implementation adds a new provider-diagnostics output channel that must be inventoried

### Do Not Touch

- Do not implement repository creation, repository binding, branch/ref mutation, workspace preparation, locks, file mutation, commit workflows, cleanup, repair automation, CLI commands, MCP tools, UI pages, read-only operations-console pages, or live provider drift workflows.
- Do not create a provider administration platform, generic multi-provider drift framework, or alternate provider capability system.
- Do not add direct references from Contracts, Client, CLI, MCP, UI, Server transport DTOs, aggregates, or Workers to Octokit, Forgejo endpoint DTOs, or provider-specific HTTP response models.
- Do not resolve credentials before authorization or store credential material after the call boundary.
- Do not manually edit generated SDK files under `src/Hexalith.Folders.Client/Generated/*`.
- Do not add package versions directly to `.csproj` files.
- Do not initialize nested submodules or use recursive submodule commands.

### Testing

- Use xUnit v3 and Shouldly. Use NSubstitute only when it is clearer than a small recording fake.
- Provider-readiness core tests should use fake `IProviderCapabilityAuthorizer`, `IProviderCapabilityResolver`, `IProviderCapabilityEvidenceStore`, and fake/read-only provider binding stores.
- Include no-touch denial tests for tenant denied, stale projection, unavailable projection, unknown tenant, disabled tenant, malformed evidence, tenant mismatch, missing authoritative tenant, replay conflict, missing readiness permission, missing binding, and unsupported provider family.
- Include mapping tests for `ProviderCapabilityDiscoveryResult.Success`, provider configuration missing, credential/authentication required, permission insufficient, validation failed, provider conflict, unsupported capability, provider readiness failed, known provider failure, unavailable, rate limited with retry-after, transient failure, unknown provider outcome, and reconciliation required.
- Include response-shape tests for consumer and authorized-operator audiences. Consumer results must not include per-capability evidence, provider binding ref, capability profile ref, credential reference status, installation identity, or raw provider details.
- Include endpoint tests that start a slim `WebApplication`, call `/api/v1/provider-readiness/validations`, and assert route registration, non-mutating idempotency behavior, freshness handling, safe correlation echo, safe denial, provider unavailable/rate-limited mapping, and leakage absence.
- Include contract tests only if OpenAPI/parity/generated client artifacts change.
- Run focused provider-readiness tests, server endpoint tests, provider abstraction tests, GitHub/Forgejo provider tests touched by mapping, and contract/safety gates when public artifacts change.

### Regression Traps

- Do not treat readiness validation as a passive status read when the provider capability evidence is stale or unsafe. It is a gate before repository creation/binding.
- Do not mark readiness `ready` from provider binding configuration alone. Story 3.1 stores metadata; it does not prove credential availability or provider capability.
- Do not bypass `ProviderCapabilityDiscoveryService` and call `GitHubProvider` or `ForgejoProvider` directly from REST endpoints.
- Do not let a denied path differ by whether the provider binding, credential reference, repository, or branch exists.
- Do not use `configure_provider_binding` as the read authorization action if a narrower provider-readiness read action exists or is required by the Contract Spine.
- Do not create readiness cache keys from provider family, provider key, or instance URL alone.
- Do not allow stale authorization evidence, stale target evidence, or unsupported Forgejo version evidence to downgrade into a generic unsupported-capability success path.
- Do not classify `unknown_provider_outcome` or `reconciliation_required` as retryable readiness failures.
- Do not leak raw provider adapter reason text into Problem Details `message`, logs, traces, or examples.
- Do not add `Idempotency-Key` handling because the operation is POST; it is explicitly POST-as-query.
- Do not let NSwag oneOf generation quirks hide required operator diagnostics. Fix the spine/generator path if needed.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 3.5`
- `_bmad-output/planning-artifacts/epics.md#Epic 3: Provider Readiness And Repository Binding`
- `_bmad-output/planning-artifacts/prd.md#Journey 2: Platform Engineer Establishes Tenant Provider Readiness`
- `_bmad-output/planning-artifacts/prd.md#Endpoint Specifications`
- `_bmad-output/planning-artifacts/architecture.md#Provider Adapters`
- `_bmad-output/planning-artifacts/architecture.md#REST endpoint naming`
- `_bmad-output/planning-artifacts/architecture.md#Recommended Project Layout`
- `docs/contract/tenant-folder-provider-repository-contract-groups.md#POST-as-query-Exception`
- `docs/contract/idempotency-and-parity-rules.md#Non-Mutating Read Consistency`
- `_bmad-output/project-context.md#Critical Don't-Miss Rules`
- `_bmad-output/implementation-artifacts/3-1-configure-provider-binding-and-credential-reference.md`
- `_bmad-output/implementation-artifacts/3-2-define-igitprovider-port-and-capability-model.md`
- `_bmad-output/implementation-artifacts/3-3-implement-github-provider-adapter.md`
- `_bmad-output/implementation-artifacts/3-4-implement-forgejo-provider-adapter-and-drift-detection.md`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderCapabilityDiscoveryService.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderCapabilityDiscoveryResult.cs`
- `src/Hexalith.Folders/Providers/GitHub/GitHubProvider.cs`
- `src/Hexalith.Folders/Providers/Forgejo/ForgejoProvider.cs`
- `src/Hexalith.Folders/Aggregates/Organization/OrganizationState.cs`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/TenantFolderProviderContractGroupTests.cs`
- `tests/fixtures/parity-contract.yaml`
- `tests/fixtures/safety-channel-inventory.json`

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-26 | Created story with provider-readiness validation route, authorization-before-observation, safe diagnostics mapping, existing provider capability reuse, Contract Spine drift handling, offline tests, and strict scope boundaries. | Codex |
| 2026-05-26 | Implemented provider-readiness validation query service, REST route, safe diagnostics, Contract Spine/generated client updates, parity refresh, and offline tests. | Codex |

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-26: Story creation context validated by reading the BMAD create-story skill, sprint status, Epic 3 story source, PRD provider-readiness requirements, architecture provider boundaries, Contract Spine provider-readiness operation, provider binding state, provider capability abstractions, GitHub/Forgejo readiness implementations, and neighboring Stories 3.1 through 3.4.
- 2026-05-26: Dev-story workflow activation resolved with no prepend/append steps; baseline commit recorded as `b0c5efabcef94cfbfefe281dec33a3a82807df85`; sprint status moved to `in-progress`.
- 2026-05-26: Red/green implementation completed for core provider-readiness validation tests, endpoint tests, Contract Spine diagnostic-field test, and generated SDK/parity updates.
- 2026-05-26: Validation completed with focused tests, serial full solution build, full solution test pass, Contract Spine gate under PowerShell 7, safety invariant gate, and governance completeness gate.

### Completion Notes List

- Story created by the local `.agents/skills/bmad-create-story` workflow for `3-5-validate-provider-readiness-with-safe-diagnostics`.
- Project context, sprint status, Epic 3, PRD, architecture, contract docs, Contract Spine, provider abstraction source, provider binding source, server endpoint patterns, GitHub/Forgejo adapter tests, recent commits, and neighboring story patterns were reviewed.
- No external web research was added because this story does not introduce a new external library or provider version source; it must consume the existing pinned provider adapter evidence from Stories 3.3 and 3.4.
- Ultimate context engine analysis completed - comprehensive developer guide created.
- Added `ProviderReadinessValidationService` and one-type-per-file readiness request/result/evidence models under `src/Hexalith.Folders/Queries/ProviderReadiness/`.
- Reused `ProviderCapabilityDiscoveryService`, canonical provider profile/result/failure categories, and existing GitHub/Forgejo provider abstractions; added only a default resolver and readiness-specific concrete authorizer/store implementations.
- Enforced authorization and tenant evidence checks before binding/provider/evidence observation; no-touch tests cover denied, stale, unavailable, malformed, replay-conflicting, permission-missing, and malformed request paths.
- Wired `POST /api/v1/provider-readiness/validations` through `ProviderReadinessEndpoints`, rejecting `Idempotency-Key`, enforcing `snapshot_per_task` freshness, echoing sanitized correlation, and mapping rate-limit/unavailable outcomes to safe Problem Details.
- Updated the Contract Spine authorized-operator readiness schema/example with safe reason code, retryability, remediation category, provider reference, and correlation ID; regenerated NSwag client output and parity oracle rows.
- Added offline core, endpoint, contract, leakage, and generated-artifact coverage; no live provider, Aspire, Dapr, Docker, network, or nested submodule initialization was required.

### File List

- `_bmad-output/implementation-artifacts/3-5-validate-provider-readiness-with-safe-diagnostics.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `docs/contract/tenant-folder-provider-repository-contract-groups.md`
- `src/Hexalith.Folders/Aggregates/Organization/OrganizationAclAction.cs`
- `src/Hexalith.Folders/Authorization/EffectivePermissionsActionCatalog.cs`
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs`
- `src/Hexalith.Folders/Providers/Abstractions/DefaultProviderCapabilityResolver.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/IProviderReadinessBindingReader.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/IProviderReadinessEvidenceStore.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/InMemoryProviderCapabilityEvidenceStore.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/InMemoryProviderReadinessBindingReadModel.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/InMemoryProviderReadinessEvidenceStore.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderReadinessBindingReadRequest.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderReadinessCapabilityAuthorizer.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderReadinessCapabilityEvidence.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderReadinessEvidenceRecord.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderReadinessFreshness.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderReadinessRequestedCapability.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderReadinessResultCode.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderReadinessValidationRequest.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderReadinessValidationResult.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderReadinessValidationService.cs`
- `src/Hexalith.Folders.Server/FoldersServerModule.cs`
- `src/Hexalith.Folders.Server/ProviderReadinessEndpoints.cs`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`
- `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs`
- `src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/TenantFolderProviderContractGroupTests.cs`
- `tests/Hexalith.Folders.Server.Tests/ProviderReadinessEndpointTests.cs`
- `tests/Hexalith.Folders.Tests/Providers/Readiness/ProviderReadinessValidationServiceTests.cs`
- `tests/fixtures/parity-contract.yaml`
