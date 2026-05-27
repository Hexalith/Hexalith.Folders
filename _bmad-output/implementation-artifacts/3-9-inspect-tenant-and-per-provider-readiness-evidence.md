---
baseline_commit: ea606988c084f2fdc69dccd73fd2c6e3be0aa5e1
---

# Story 3.9: Inspect tenant and per-provider readiness evidence

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a platform engineer,
I want to inspect tenant and provider readiness evidence,
so that I can diagnose provider setup before agents run workspace tasks.

## Terms

- `GetProviderSupportEvidence` means the non-mutating read path already declared at `GET /api/v1/provider-readiness/support-evidence`. It must not validate live provider readiness, mutate readiness records, accept `Idempotency-Key`, create repositories, bind repositories, configure branch/ref policy, prepare workspaces, or call GitHub/Forgejo APIs.
- Provider support evidence means metadata-only capability evidence derived from authorized provider readiness/capability results: capability profile reference, provider binding reference or equivalent safe provider reference, provider family/key where the public contract allows it, capability name, support state, freshness, and pagination evidence.
- Tenant and per-provider scope means the query must only return evidence for the authenticated managed tenant and must preserve enough safe identity for operators to distinguish GitHub and Forgejo readiness posture without seeing provider account identity, raw base URLs, repository names, credential values, installation IDs, or raw provider payloads.
- Safe capability states use the existing Contract Spine `ProviderCapabilityState` vocabulary: `supported`, `unsupported`, and `temporarily_unavailable`. Do not invent new public support states unless the Contract Spine, parity rows, generated client, docs, and tests are updated together.
- Readiness evidence is projection/evidence-store data. If the store is stale, unavailable, malformed, empty, or incompatible with the caller's tenant/authorization evidence, the response must classify that condition explicitly without falling back to live provider calls or leaking whether hidden provider bindings exist.

## Acceptance Criteria

1. Given provider readiness evidence has been produced for the authenticated managed tenant, when an authorized caller requests `GetProviderSupportEvidence`, then the response returns a paginated metadata-only list of provider capability support rows for that tenant and omits evidence belonging to other tenants.
2. Given GitHub and Forgejo capability evidence exists for the same tenant, when support evidence is requested, then rows remain distinguishable by safe provider-scoped references or capability profile references according to the Contract Spine; if the current `ProviderSupportEvidence` schema cannot safely express per-provider diagnosis, update `hexalith.folders.v1.yaml`, parity rows, previous-spine expectations, generated client output, contract docs, and tests together before runtime behavior is finalized.
3. Given the caller is missing authentication, authoritative tenant evidence, tenant access, provider-support read authority, or has client-controlled tenant mismatch, stale/unavailable/malformed/replay-conflicting evidence, when `GetProviderSupportEvidence` is attempted, then authorization fails before evidence-store enumeration, provider binding reads, provider resolver lookup, provider adapter construction, credential observation, audit/evidence writes, metrics/log enrichment, or diagnostics; denial responses do not reveal provider binding, credential, provider family, capability profile, or row existence.
4. Given `GetProviderSupportEvidence` is a non-mutating query/status operation, when the server maps `GET /api/v1/provider-readiness/support-evidence`, then it rejects `Idempotency-Key`, handles `X-Correlation-Id` safely, enforces the declared freshness/read-consistency behavior, supports bounded cursor/limit pagination, and returns canonical Problem Details for authentication, authorization, projection stale/unavailable, read-model unavailable, and provider unavailable categories.
5. Given readiness evidence records from Story 3.5 include `ManagedTenantId`, `OrganizationId`, `ProviderBindingRef`, `ProviderFamily`, `ProviderKey`, `CapabilityProfileRef`, `Status`, `ReasonCode`, `Retryable`, `RemediationCategory`, `ObservedAt`, `FreshnessWatermark`, `CorrelationId`, and diagnostic JSON, when support evidence is projected, then only approved safe dimensions are exposed and raw diagnostic JSON is never passed through as a public payload.
6. Given a readiness validation result contains per-capability evidence, when support evidence rows are materialized, then repository creation, existing repository binding, branch/ref policy, file operations, commit/status, provider errors, and failure behavior are mapped to stable support evidence rows without requiring consumers to parse provider-specific diagnostics.
7. Given readiness evidence is missing for a tenant, provider binding, capability profile, or provider family, when support evidence is requested after authorization succeeds, then the response uses a safe empty list or explicit unavailable/stale classification according to the read-model state; it must not query live providers or confirm unauthorized resource existence.
8. Given stored evidence is stale, malformed, future-dated, tenant-mismatched, missing capability profile references, or contains unsafe diagnostic strings, when the query handler evaluates it, then unsafe rows are redacted or excluded and the result is classified as stale/unavailable/malformed as appropriate without echoing forbidden values in response bodies, headers, logs, traces, metrics, or test output.
9. Given the current generated SDK already includes `GetProviderSupportEvidenceAsync`, when implementation starts, then runtime route behavior must reconcile with the generated operation shape: required correlation behavior, optional freshness header, cursor/limit parameters, response schema, non-idempotency rule, and canonical errors must match the Contract Spine or be changed through the established generation pipeline.
10. Given unit, contract, server, projection/read-model, generated-client, parity, and safety tests run locally or in CI, then Story 3.9 validation is hermetic: no live GitHub, live Forgejo, provider credentials, tenant seed data, Aspire, Dapr sidecars, Redis, Keycloak, Docker, network calls, or nested submodule initialization is required.

## Tasks / Subtasks

- [x] Reconcile the public support-evidence contract before runtime work. (AC: 2, 4, 9)
  - [x] Review `GET /api/v1/provider-readiness/support-evidence` in `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`, `docs/contract/idempotency-and-parity-rules.md`, `tests/fixtures/parity-contract.yaml`, `tests/fixtures/previous-spine.yaml`, generated SDK `GetProviderSupportEvidenceAsync`, and `TenantFolderProviderContractGroupTests`.
  - [x] Decide whether the existing response shape with `capabilityProfileRef`, `capability`, and `supportState` is sufficient for per-provider diagnosis. If not, update OpenAPI, generated client, parity rows, previous-spine, docs, examples, and tests together.
  - [x] Keep the operation non-mutating with no `Idempotency-Key`; reject idempotency headers before authorization/evidence observation.
  - [x] Preserve canonical read consistency, correlation, pagination, and safe-denial semantics from the Contract Spine.

- [x] Add a provider support evidence query/read-model layer. (AC: 1, 3, 5, 6, 7, 8)
  - [x] Extend `IProviderReadinessEvidenceStore` or add a sibling read-model interface for tenant-scoped support evidence queries; do not force callers to downcast to `InMemoryProviderReadinessEvidenceStore`.
  - [x] Add one-public-type-per-file request/result/snapshot models under `src/Hexalith.Folders/Queries/ProviderReadiness/`, such as `ProviderSupportEvidenceQuery`, `ProviderSupportEvidenceQueryHandler`, `ProviderSupportEvidenceReadModelRequest`, `ProviderSupportEvidenceReadModelResult`, and `ProviderSupportEvidenceItem`.
  - [x] Authorize with a narrow provider-support read action matching the Contract Spine requirement `tenant-context-and-provider-support-read`; do not reuse provider configuration mutation authority.
  - [x] Run tenant/claim evidence checks before evidence-store enumeration and prove denied paths make zero calls to provider binding readers, provider capability resolvers, provider adapters, credential resolvers, audit writers, cache, metrics, logs, or diagnostics.
  - [x] Project only safe fields from `ProviderReadinessEvidenceRecord`; never expose or parse-pass raw `DiagnosticJson` into public responses unless it is parsed into an allow-listed, metadata-only shape.
  - [x] Map `ProviderReadinessCapabilityEvidence` fields to Contract Spine capability names and support states with ordinal comparisons and deterministic ordering.
  - [x] Treat missing, stale, malformed, tenant-mismatched, future-dated, unsafe, and incompatible rows as explicit safe states instead of silently producing misleading `supported` evidence.

- [x] Wire the canonical REST route. (AC: 1, 3, 4, 7, 9)
  - [x] Extend `src/Hexalith.Folders.Server/ProviderReadinessEndpoints.cs` to map `GET /api/v1/provider-readiness/support-evidence`.
  - [x] Reuse existing safe correlation/header handling patterns, but keep response-shape code separate enough that validation and support-evidence behavior cannot drift accidentally.
  - [x] Reject unsupported freshness values, invalid cursor/limit values, unsafe correlation IDs, and `Idempotency-Key` before evidence-store observation.
  - [x] Return `ProviderSupportEvidenceList` with `items`, `page`, and `freshness`, including stable empty-list behavior for authorized tenants with no evidence.
  - [x] Map read-model stale/unavailable and authorization failures to canonical Problem Details without provider identifiers.
  - [x] Register the query handler/read model in `FoldersServiceCollectionExtensions` or the focused provider-readiness service registration path.

- [x] Preserve provider adapter boundaries and evidence provenance. (AC: 1, 2, 5, 6, 8)
  - [x] Reuse Story 3.5 readiness evidence and `ProviderCapabilityDiscoveryService` output; do not call `IGitProvider` from the support-evidence query path.
  - [x] Keep GitHub/Forgejo-specific behavior inside provider adapters and readiness mappers. Public support evidence must consume canonical capability names and support states only.
  - [x] Ensure capability profile references, provider binding references, provider family/key labels, and freshness watermarks are tenant-scoped and safe before exposing them.
  - [x] Preserve GitHub vs Forgejo distinctions as safe metadata; do not treat Forgejo as a GitHub base-URL swap or expose Forgejo endpoint paths/base URLs.

- [x] Add focused offline tests and safety guards. (AC: 1-10)
  - [x] Add core query/read-model tests for authorized success, empty list, GitHub and Forgejo rows, pagination, stale projection, unavailable projection, malformed rows, tenant mismatch, future observed-at, unsafe diagnostic payload, and deterministic row ordering.
  - [x] Add no-touch authorization tests proving authentication failure, tenant denied, missing provider-support action, stale/unavailable/malformed claim evidence, reserved `system` tenant, and client-controlled tenant mismatch do not observe evidence records or provider bindings.
  - [x] Add server endpoint tests for route registration, idempotency header rejection, unsupported freshness rejection, cursor/limit validation, safe correlation echo/generation, safe denial bodies, empty-list success, and Problem Details mappings.
  - [x] Add generated-client/contract/parity tests if OpenAPI or generated artifacts change.
  - [x] Add leakage tests scanning support evidence responses, Problem Details, logs/test output, and stored diagnostic projections for provider tokens, credential refs, raw provider payloads, repository URLs, base URLs, installation IDs, branch names, file contents, diffs, emails, and unauthorized resource names.

## Dev Notes

### Source Context

- Epic 3 requires provider configuration, readiness validation, repository-backed folder creation/binding, branch/ref policy, and provider capability evidence without exposing secrets. Story 3.9 is the evidence-inspection capstone for FR21-FR23. [Source: `_bmad-output/planning-artifacts/epics.md#Epic 3: Provider Readiness And Repository Binding`; `_bmad-output/planning-artifacts/epics.md#Story 3.9`]
- PRD FR21 requires provider, credential-reference, repository-binding, branch/ref, and capability metadata exposure without secrets. FR22 requires GitHub and Forgejo capability differences to be explicit. FR23 requires per-provider readiness evidence for readiness, repository binding, branch/ref handling, file operations, commit, status, and failure behavior. [Source: `_bmad-output/planning-artifacts/prd.md#Functional Requirements`]
- The Contract Spine already declares `GetProviderSupportEvidence` at `/api/v1/provider-readiness/support-evidence`, returning `ProviderSupportEvidenceList` with `items`, `page`, and `freshness`; each item currently has `capabilityProfileRef`, `capability`, and `supportState`. [Source: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#/api/v1/provider-readiness/support-evidence`; `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml#ProviderSupportEvidence`]
- Generated SDK already contains `GetProviderSupportEvidenceAsync(string x_Correlation_Id, ReadConsistencyClass? x_Hexalith_Freshness, string cursor, int? limit, CancellationToken cancellationToken)`. Runtime behavior must match this generated shape or regenerate through the established pipeline. [Source: `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs#GetProviderSupportEvidenceAsync`]
- Contract/parity docs classify `GetProviderSupportEvidence` as a non-mutating query/status operation that does not accept idempotency keys, uses eventually-consistent support evidence projection, and must deny without provider account, repository, or credential-reference details. [Source: `docs/contract/idempotency-and-parity-rules.md#GetProviderSupportEvidence`; `tests/fixtures/parity-contract.yaml#GetProviderSupportEvidence`]
- UX requirements for Epic 6 expect provider readiness evidence to support trust summaries, trust matrix, diagnostic evidence, and read-only operations-console views with redacted, unknown, stale, unavailable, failed, and delayed states visibly distinct. [Source: `_bmad-output/planning-artifacts/ux-design-specification.md#Design Requirements`; `_bmad-output/planning-artifacts/ux-design-specification.md#Core Experience`]

### Previous Story Intelligence

- Story 3.5 implemented provider readiness validation with safe diagnostics. It added `ProviderReadinessValidationService`, `ProviderReadinessCapabilityEvidence`, `ProviderReadinessEvidenceRecord`, `IProviderReadinessEvidenceStore`, `InMemoryProviderReadinessEvidenceStore`, and `ProviderReadinessEndpoints` for `POST /api/v1/provider-readiness/validations`.
- Story 3.5 stores tenant-scoped evidence records only after authorization and readiness evaluation. The record shape already includes tenant, organization, provider binding, provider family/key, capability profile ref, status, reason, retryability, remediation, observed-at, freshness watermark, correlation, and diagnostic JSON.
- Story 3.5 senior review fixed no-touch ordering for stale tenant evidence and Forgejo readiness construction. Story 3.9 must preserve authorization-before-observation and must not regress into evidence-store enumeration before tenant/provider-support authorization.
- Story 3.8 added a branch/ref policy read model and query handler with strong snapshot-compatibility checks. Reuse that pattern for read-model status classification, freshness validation, evidence-scope compatibility, metadata-only logging, and safe denial behavior.
- Story 3.8 also fixed `ProviderReadinessRequestedCapability.BranchRefPolicy` so it does not require repository creation. Support evidence should report repository creation support accurately without making branch/ref evidence depend on creation capability.

### Existing Implementation State

- `src/Hexalith.Folders.Server/ProviderReadinessEndpoints.cs` currently maps only `POST /api/v1/provider-readiness/validations`. It already has strict JSON parsing, safe correlation generation, `Idempotency-Key` rejection for validation, `X-Hexalith-Freshness` handling, safe Problem Details, and provider-readiness response mapping. It does not map `GET /api/v1/provider-readiness/support-evidence`.
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderReadinessValidationService.cs` validates tenant/claim evidence before binding/provider observation, discovers canonical provider capabilities, maps required operations, materializes `ProviderReadinessCapabilityEvidence`, and stores `ProviderReadinessEvidenceRecord`.
- `IProviderReadinessEvidenceStore` currently exposes only `StoreAsync`. `InMemoryProviderReadinessEvidenceStore` has a concrete `Records` property but no interface query method; Story 3.9 should add an explicit tenant-scoped read path rather than depending on concrete-type access.
- `ProviderReadinessCapabilityEvidence` stores combined capability states for `RepositoryCreation`, `ExistingRepositoryBinding`, `BranchRefPolicy`, `FileOperations`, `CommitStatus`, `ProviderErrors`, and `FailureBehavior`. Support evidence must map these to the OpenAPI `ProviderCapabilityName`/`ProviderCapabilityState` contract.
- `FoldersServiceCollectionExtensions.AddFoldersProviderReadiness()` registers provider adapters, capability discovery, binding reader, evidence store, and validation service. Add support-evidence query registrations here unless a narrower existing composition point is better.
- `tests/Hexalith.Folders.Server.Tests/ProviderReadinessEndpointTests.cs` already covers validation route registration, safe diagnostics, idempotency rejection, unsupported freshness, correlation sanitization, tenant mismatch denial, and no provider identifier leakage. Extend this test file or add a focused support-evidence endpoint test file.

### Required Architecture Patterns

- Use .NET 10, nullable-aware C#, file-scoped namespaces, one public type per file, PascalCase public members, camelCase locals/parameters, async APIs with `CancellationToken`, and centralized package versions.
- Keep `Hexalith.Folders.Contracts` behavior-free. Query behavior belongs in core/server code; public shape changes start in the OpenAPI Contract Spine and flow through generated artifacts.
- Preserve authorization-before-observation. No evidence-store enumeration, provider binding reads, capability resolver lookup, provider adapter construction, credential inspection, audit writes, metrics/log enrichment, or diagnostics before tenant/provider-support authorization passes.
- Keep provider adapters behind `IGitProvider` and provider capability discovery. The support-evidence read path consumes stored/projection evidence and must not perform live GitHub/Forgejo calls.
- Keep all diagnostics metadata-only. Never expose secrets, tokens, raw credential references, raw provider payloads, raw exception text, repository URLs, owner/repository names, base URLs, installation IDs, branch names, file contents, diffs, emails, display names, raw claim bags, or unauthorized resource existence.
- Use `StringComparison.Ordinal`, `StringComparer.Ordinal`, invariant formatting, deterministic ordering, and bounded pagination for support evidence.
- Preserve root-level submodule policy. Do not initialize or update nested submodules recursively.

### Files To Touch

- `src/Hexalith.Folders/Queries/ProviderReadiness/IProviderReadinessEvidenceStore.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/InMemoryProviderReadinessEvidenceStore.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/*ProviderSupportEvidence*.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderReadinessCapabilityEvidence.cs` only if mapping helpers are needed and fit the existing model
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs`
- `src/Hexalith.Folders.Server/ProviderReadinessEndpoints.cs`
- `tests/Hexalith.Folders.Tests/Providers/Readiness/*ProviderSupportEvidence*Tests.cs`
- `tests/Hexalith.Folders.Server.Tests/*ProviderSupportEvidence*EndpointTests.cs`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/TenantFolderProviderContractGroupTests.cs` only if public contract assertions need tightening
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`, `src/Hexalith.Folders.Client/Generated/*`, `tests/fixtures/parity-contract.yaml`, and `tests/fixtures/previous-spine.yaml` only if the current support-evidence shape changes
- `docs/contract/idempotency-and-parity-rules.md` only if semantics or exposed audit metadata change
- `tests/fixtures/safety-channel-inventory.json` only if a new output channel is introduced

### Do Not Touch

- Do not implement repository creation, repository binding, branch/ref policy mutation, workspace preparation, locks, file mutation, commits, cleanup, repair automation, CLI commands, MCP tools, UI pages, operations-console pages, live drift workflows, or provider administration screens.
- Do not call GitHub, Forgejo, `IGitProvider`, provider capability resolvers, credential stores, filesystem, Dapr sidecars, or EventStore aggregates from the support-evidence read path after evidence has already been projected/stored.
- Do not manually edit generated SDK files under `src/Hexalith.Folders.Client/Generated/*`.
- Do not expose raw provider diagnostics or raw `DiagnosticJson` as response payload.
- Do not add package versions directly to `.csproj` files.
- Do not add live-provider or network-dependent tests to PR-gate suites.
- Do not initialize nested submodules or use recursive submodule commands.

### Testing

- Use xUnit v3 and Shouldly. Prefer small recording fakes over broad mocks.
- Core tests should exercise query/read-model mapping from `ProviderReadinessEvidenceRecord` to support evidence rows, including GitHub and Forgejo evidence in the same tenant.
- No-touch tests should prove denied paths do not enumerate the evidence store or touch provider bindings/resolvers/adapters.
- Endpoint tests should use a slim `WebApplication`, map server endpoints, and assert `GET /api/v1/provider-readiness/support-evidence` registration, headers, pagination, safe denial, empty-list success, and error mappings.
- Contract/parity/generated-client tests must run if OpenAPI, parity rows, previous-spine, generated SDK, or docs change.
- Safety tests must scan support-evidence responses, Problem Details, diagnostic records, logs/test output, and examples for provider/credential/repository/file-content sentinels.

### Regression Traps

- Do not use `InMemoryProviderReadinessEvidenceStore.Records` through a concrete downcast from endpoint code; add a proper interface/read-model path.
- Do not parse raw `DiagnosticJson` into public response DTOs without an allow-list. It is an internal safe diagnostic artifact, not a public schema.
- Do not treat `providerFamily` or `providerKey` as public-safe automatically. Expose them only if the Contract Spine allows that field and values pass safe metadata validation.
- Do not call `ValidateProviderReadiness` or provider adapters while serving support evidence; this query reports existing evidence and projection freshness.
- Do not collapse no evidence, redacted evidence, stale evidence, malformed evidence, unauthorized evidence, and unsupported capability into the same empty response unless the contract explicitly says they are indistinguishable at that boundary.
- Do not leak provider binding references on denial. Authorized success can include only contract-approved safe identifiers.
- Do not hand-author generated parity or SDK output. Regenerate through existing gates if the spine changes.
- Do not broaden Story 3.9 into operations-console diagnostics; Epic 6 owns console pages and richer UI diagnostic views.

### Project Structure Notes

- The story aligns with the existing provider-readiness concept area under `src/Hexalith.Folders/Queries/ProviderReadiness/` and the focused server endpoint file `src/Hexalith.Folders.Server/ProviderReadinessEndpoints.cs`.
- The current public contract has the support-evidence operation and generated SDK method already present, but runtime support is not wired. The main structural gap is a read/query abstraction over readiness evidence records.
- Dirty worktree note at story creation time: `Hexalith.Builds` and `_bmad-output/story-automator/orchestration-3-20260526-203745.md` were already modified and are unrelated to this story file.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 3.9`
- `_bmad-output/planning-artifacts/prd.md#Functional Requirements`
- `_bmad-output/planning-artifacts/architecture.md#Requirements-to-Structure-Mapping`
- `_bmad-output/planning-artifacts/ux-design-specification.md#Design-Requirements`
- `_bmad-output/project-context.md#Critical-Don't-Miss-Rules`
- `_bmad-output/implementation-artifacts/3-5-validate-provider-readiness-with-safe-diagnostics.md`
- `_bmad-output/implementation-artifacts/3-8-define-branch-and-ref-policy.md`
- `docs/contract/idempotency-and-parity-rules.md`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`
- `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs`
- `tests/fixtures/parity-contract.yaml`
- `tests/fixtures/previous-spine.yaml`
- `src/Hexalith.Folders.Server/ProviderReadinessEndpoints.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderReadinessValidationService.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/IProviderReadinessEvidenceStore.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/InMemoryProviderReadinessEvidenceStore.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderReadinessCapabilityEvidence.cs`
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs`
- `tests/Hexalith.Folders.Server.Tests/ProviderReadinessEndpointTests.cs`
- `tests/Hexalith.Folders.Tests/Providers/Readiness/ProviderReadinessValidationServiceTests.cs`

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-27 | Senior review fixed stale mixed-evidence classification, malformed capability evidence handling, duplicate support rows, and support-evidence limit clamping. | Codex |
| 2026-05-27 | Implemented provider support evidence read model/query handler, canonical REST route, DI registration, stale/malformed safety classification, pagination, and offline tests. | Codex |
| 2026-05-27 | Created story with support-evidence runtime scope, authorization-before-observation guardrails, contract reconciliation warning for per-provider evidence, existing Story 3.5 evidence reuse, and offline validation plan. | Codex |

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-27: Resolved `bmad-create-story` customization; no activation prepend/append steps. Loaded `_bmad/bmm/config.yaml`, sprint status, root and sibling project-context facts, Epic 3, PRD/architecture/UX references, previous story 3.8, and provider-readiness implementation files.
- 2026-05-27: Discovery loaded planning artifacts from `_bmad-output/planning-artifacts/` and implementation intelligence from stories 3.5 and 3.8. No sharded planning docs were present.
- 2026-05-27: Git intelligence: HEAD `ea606988c084f2fdc69dccd73fd2c6e3be0aa5e1` is `feat(story-3.8): Define branch and ref policy`; recent work added branch/ref policy runtime and tests.
- 2026-05-27: Latest external research was not added because this story introduces no new external library or provider version source; it must consume existing pinned provider adapter/readiness evidence.
- 2026-05-27: Public contract reconciliation completed. Existing `ProviderSupportEvidence` shape (`capabilityProfileRef`, `capability`, `supportState`) was retained because capability profile references provide the contract-approved safe per-provider distinction; no OpenAPI/generated SDK/parity changes were required.
- 2026-05-27: Implemented `IProviderSupportEvidenceReadModel`, provider support evidence request/result/page/item models, and `ProviderSupportEvidenceQueryHandler` with provider-support read authorization before evidence read-model observation.
- 2026-05-27: Extended `InMemoryProviderReadinessEvidenceStore` with tenant-scoped support-evidence projection from stored readiness diagnostic evidence, deterministic capability ordering, bounded cursor pagination, unsafe diagnostic rejection, future/malformed classification, and stale evidence classification.
- 2026-05-27: Wired `GET /api/v1/provider-readiness/support-evidence` in `ProviderReadinessEndpoints` with safe correlation, eventually-consistent freshness, bounded cursor/limit validation, idempotency-header rejection, safe Problem Details, and response mapping.
- 2026-05-27: Endpoint tests were moved to ASP.NET Core `TestServer` for this file so story 3.9 validation remains hermetic and does not require loopback socket binding.
- 2026-05-27: Validation passed: `ProviderSupportEvidenceQueryHandlerTests` direct xUnit run, full `Hexalith.Folders.Tests` direct xUnit run (795 passed), `ProviderReadinessEndpointTests` direct xUnit run (16 passed), `TenantFolderProviderContractGroupTests` direct xUnit run (6 passed), and focused server/core builds.
- 2026-05-27: Validation limits: `dotnet build Hexalith.Folders.slnx` could not complete restore because sandboxed network access blocked `https://api.nuget.org/v3/index.json` for `Hexalith.Folders.AppHost`; `dotnet test` via VSTest is blocked by sandbox TCP listener permissions, so xUnit assemblies were run directly. Broader server assembly has unrelated pre-existing failures outside story 3.9 (`FolderLifecycleStatusEndpointTests`/`ArchiveFolderEndpointTests` Kestrel socket binding in sandbox and one unrelated `BranchRefPolicyEndpointTests` expectation).

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Story 3.9 file created at `_bmad-output/implementation-artifacts/3-9-inspect-tenant-and-per-provider-readiness-evidence.md`.
- Sprint status updated to mark `3-9-inspect-tenant-and-per-provider-readiness-evidence` as `ready-for-dev`.
- Validation checklist applied manually against the source artifacts, previous story intelligence, current source files, contract artifacts, scope boundaries, and regression traps.
- Implemented metadata-only provider support evidence inspection using stored readiness evidence; no live provider, binding reader, resolver, adapter, credential, repository, Dapr, or EventStore read path is invoked after authorization.
- Retained the existing Contract Spine response shape and generated SDK shape; no contract/generated/parity artifacts were changed.
- Added safe stale/unavailable/malformed/empty classifications, bounded pagination, deterministic support-row ordering, and raw diagnostic leakage guards.
- Added focused core/read-model, no-touch authorization, endpoint, contract, and leakage coverage. Story-focused tests and full core tests pass locally through the xUnit in-process runner.
- Story 3.9 implementation passed senior review after automatic fixes, with the validation caveats recorded below.

### Senior Developer Review (AI)

Reviewer: Codex on 2026-05-27

Outcome: Approved after automatic fixes. No CRITICAL issues remain.

Findings fixed:

- [HIGH] Mixed fresh and stale scoped readiness records could still return an apparently current support-evidence list because freshness was evaluated from the newest record only. Fixed `InMemoryProviderReadinessEvidenceStore` to classify the result as stale when any scoped record is outside the evidence freshness budget. Added mixed stale/fresh coverage.
- [HIGH] Incomplete or invalid capability evidence could be silently mapped to `unsupported`, which made malformed projection rows look like legitimate provider capability results. Fixed capability projection to require all stored evidence fields and valid Contract Spine states before materializing rows. Added incomplete capability-evidence coverage.
- [MEDIUM] Multiple readiness validations for the same capability profile could emit duplicate public support rows. Fixed projection to use the latest record per capability profile before row materialization. Added latest-record coverage.
- [MEDIUM] `GET /api/v1/provider-readiness/support-evidence?limit=101..1000` was rejected even though the Contract Spine `PageLimit` permits values through 1000 and says runtime servers should clamp to the effective maximum. Fixed endpoint pagination to reject only values outside the OpenAPI ceiling and clamp accepted values to the server maximum. Added endpoint coverage for clamping and `1001` rejection.

Validation:

- Passed: `dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore --tlp:off -v:minimal`
- Passed: `dotnet tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests.dll -class Hexalith.Folders.Tests.Providers.Readiness.ProviderSupportEvidenceQueryHandlerTests` (existing in-process assembly, 15 passed)
- Passed: `dotnet tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests.dll -class Hexalith.Folders.Server.Tests.ProviderReadinessEndpointTests` (existing in-process assembly, 21 passed)
- Passed: `git diff --check`
- MCP doc search: no MCP documentation-search tool was available in this session; review used local Contract Spine, parity fixtures, generated client, project context, architecture notes, and source/tests as the fallback references.
- Blocked by sandbox/tooling: `dotnet test ...` through VSTest still fails with `System.Net.Sockets.SocketException (13): Permission denied`.
- Blocked by existing MSBuild/project-reference behavior: focused test/server project `dotnet build` and `dotnet restore` attempts failed without compiler or restore diagnostics (`0 Warning(s), 0 Error(s)`), while the core project build succeeded.

### File List

- `_bmad-output/implementation-artifacts/3-9-inspect-tenant-and-per-provider-readiness-evidence.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Folders/Queries/ProviderReadiness/IProviderSupportEvidenceReadModel.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/InMemoryProviderReadinessEvidenceStore.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderSupportEvidenceItem.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderSupportEvidencePage.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderSupportEvidenceQuery.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderSupportEvidenceQueryHandler.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderSupportEvidenceQueryResult.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderSupportEvidenceQueryResultCode.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderSupportEvidenceReadModelRequest.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderSupportEvidenceReadModelResult.cs`
- `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderSupportEvidenceReadModelStatus.cs`
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs`
- `src/Hexalith.Folders.Server/ProviderReadinessEndpoints.cs`
- `tests/Hexalith.Folders.Tests/Providers/Readiness/ProviderSupportEvidenceQueryHandlerTests.cs`
- `tests/Hexalith.Folders.Server.Tests/ProviderReadinessEndpointTests.cs`
