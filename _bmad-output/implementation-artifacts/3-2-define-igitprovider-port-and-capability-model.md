# Story 3.2: Define IGitProvider port and capability model

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a provider adapter implementer,
I want an N-provider capability-discoverable Git provider port,
so that GitHub, Forgejo, and future providers can expose differences without changing product semantics.

## Terms

- `IGitProvider` means the internal Folders provider port consumed by readiness, repository-binding, workspace, file, commit, status, and reconciliation workflows. It is not a public REST, SDK, CLI, MCP, or OpenAPI contract.
- Provider capability profile means metadata-only evidence describing which canonical operations a provider and target instance can support, partially support, emulate, or reject.
- Provider operation means a named canonical lifecycle capability such as readiness validation, repository creation, repository binding, branch/ref inspection, workspace preparation, file mutation support, commit support, status query, and cleanup/expiration support.
- Provider failure category means the internal mapping of provider responses, timeouts, rate limits, authentication/authorization failures, repository conflicts, unsupported capabilities, drift, and unknown outcomes into canonical Folders result categories.
- Provider target evidence means safe metadata such as provider family, target product/version, API surface version, capability profile reference, rate-limit posture, and correlation ID. It never includes raw provider payloads, tokens, credential values, repository URLs with credentials, installation IDs when sensitive, file contents, diffs, branch secrets, or unauthorized resource existence.
- Fake/test provider means an offline adapter implementation used to validate port behavior and conformance without GitHub, Forgejo, credentials, Dapr sidecars, Aspire, Redis, Keycloak, live Tenants data, or initialized nested submodules.

## Acceptance Criteria

1. Given `Hexalith.Folders` has provider binding metadata from Story 3.1, when the provider port abstraction is introduced, then it defines an N-provider-ready `IGitProvider` surface under the core provider abstraction area and exposes capability discovery as metadata without referencing GitHub, Forgejo, Octokit, Forgejo HTTP clients, Dapr, filesystem working copies, EventStore command handlers, REST endpoints, generated SDK clients, CLI, MCP, or UI code.
2. Given a provider capability profile is queried through the port, when the provider responds successfully, then the result includes a minimum required shape of provider family/key, provider binding reference, capability profile schema/version, safe target/version metadata, supported operation set, unsupported/partial/emulated operation markers, operation limits and constraints, credential mode requirements, retryability hints, rate-limit posture, known failure mappings, correlation ID, and metadata-only evidence; provider family/key and canonical operation identifiers must be normalized from configured metadata instead of user-controlled labels, and optional/reserved fields for adapter-specific later stories must remain metadata-only and must not require live provider probing.
3. Given GitHub, Forgejo, or any future provider has capability differences, when those differences are modeled, then they are represented as data on the capability profile instead of conditional product semantics, hardcoded two-provider switches, base-URL compatibility assumptions, or adapter-specific public contracts.
4. Given the port is asked to perform capability discovery with unsupported, unavailable, stale, malformed, disabled, drifted, ambiguous, duplicate, conflicting, or version-incompatible provider evidence, when the result is returned, then it maps to stable internal provider result/failure categories such as `unsupported_provider_capability`, `provider_unavailable`, `provider_authentication_required`, `provider_configuration_missing`, `provider_permission_insufficient`, `provider_rate_limited`, `provider_validation_failed`, `provider_conflict`, `provider_readiness_failed`, `provider_failure_known`, `provider_transient_failure`, `unknown_provider_outcome`, or `reconciliation_required` as appropriate, without leaking raw provider diagnostics or unauthorized resource existence; stale evidence must not be collapsed into unsupported-capability semantics, ambiguous or duplicate/conflicting capability metadata must not be resolved by last-writer-wins behavior, and these categories must not change public Contract Spine errors unless an explicit Contract Spine gap is documented and regenerated.
5. Given an actor is unauthorized, tenant evidence is disabled/stale/unavailable/malformed/mismatched/replay-conflicting, or provider binding metadata cannot be observed safely, when a caller attempts provider capability discovery through an application service or query wrapper in this story, then authorization and tenant/organization scoping complete before any provider adapter, credential reference, repository, branch, workspace, cache, projection, audit writer, metric/log enrichment, capability comparison, or capability store is touched; denied and missing paths that would reveal existence return the same existing safe denial/read-model unavailable result families without confirming whether provider binding, repository, credential reference, or capability evidence exists.
6. Given a capability discovery attempt carries credential references, when the port receives the request, then it uses only opaque credential reference metadata and explicit credential mode requirements; it must not resolve, fetch, validate, log, serialize, compare, project, or return raw provider credentials, token-shaped strings, PEM/JWT-shaped values, credential URLs, private keys, connection strings, secret-shaped JSON keys, or raw provider payloads.
7. Given capability discovery is retried with the same provider binding and safe target evidence, when the fake/test provider returns equivalent metadata, then the profile result is deterministic and can be compared in tests using a normalized, metadata-only fingerprint that applies stable ordering, casing, null handling, version normalization, and ignored-field rules for correlation IDs, timestamps, diagnostics, and future unknown metadata; the fingerprint and any cache key must include tenant/organization, provider binding, provider family/key, profile schema/version, and safe target dimensions without including raw provider payloads or credential material; given evidence changes, then profile version/fingerprint or result metadata changes without mutating organization/folder aggregate state.
8. Given a provider operation is unsupported, partially supported, emulated, rate-limited, unavailable, or blocked by missing credential mode, when downstream readiness or repository workflows inspect capability metadata, then they can make a decision from the profile without calling provider-specific APIs directly or parsing adapter-specific exception text.
9. Given the Contract Spine already contains provider readiness/support evidence operations, when this story touches contract or parity artifacts, then it preserves OpenAPI 3.1 extension semantics, canonical error categories, operation IDs, parity fixture ownership, and NSwag generation boundaries; no public contract change is expected unless implementation finds a named Contract Spine gap, and generated client files are not manually edited.
10. Given this story is complete, when tests run in a developer machine or CI PR gate, then provider-port unit/conformance tests pass offline using fake/test providers and no GitHub, Forgejo, provider credentials, live provider APIs, tenant seed data, Aspire, Dapr sidecars, Redis, Keycloak, or nested submodule initialization; the tests cover supported, unsupported, partial, emulated, denied, missing, invalid input, conflict, rate-limited, unavailable, transient failure, permanent known failure, unknown outcome, and reconciliation-required cases.
11. Given GitHub and Forgejo adapters are later stories, when this story is implemented, then it must not implement Octokit adapters, Forgejo HTTP clients, live provider readiness checks, repository creation/binding, branch creation, workspace preparation, file operations, commits, webhooks, drift polling, nightly live provider tests, CLI/MCP commands, UI pages, or repair workflows.

## Tasks / Subtasks

- [x] Add the provider abstraction model in the core project. (AC: 1, 2, 3, 8, 11)
  - [x] Create `src/Hexalith.Folders/Providers/Abstractions/IGitProvider.cs` and related one-public-type-per-file models such as `ProviderCapabilityProfile`, `ProviderOperationCapability`, `ProviderOperationSupport`, `ProviderCapabilityDiscoveryRequest`, `ProviderCapabilityDiscoveryResult`, `ProviderCapabilityProfileVersion`, and `ProviderFailureCategory`.
  - [x] Keep the initial `IGitProvider` surface narrow: provider identity plus capability-profile discovery/comparison only; repository, branch, workspace, file, commit, webhook, readiness-execution, credential-resolution, and validation methods belong to later stories.
  - [x] Keep the abstractions behavior-free with respect to public transports; do not add REST endpoint handlers, generated client edits, CLI/MCP tools, UI pages, worker process managers, or provider-specific adapters in this story.
  - [x] Capability discovery in this story is computed from static or provider-declared metadata supplied to the fake/test provider; it must not perform network, credential, filesystem, Dapr, EventStore, SDK, or live provider calls.
  - [x] Model capability support as extensible data keyed by canonical operation identifiers rather than boolean GitHub/Forgejo properties or closed two-provider enums.
  - [x] Normalize provider family/key and operation identifiers through a single comparer/parser so casing, aliases, whitespace, and duplicate/conflicting operation rows are deterministic failures or normalized matches, not adapter-specific behavior.
  - [x] Include safe target/version metadata and capability profile fingerprints so future readiness and drift stories can compare evidence without storing raw provider payloads.
  - [x] Keep provider operation names aligned with the existing Contract Spine and parity vocabulary where those operations already exist.
- [x] Define provider failure and retry semantics at the port boundary. (AC: 4, 8)
  - [x] Add canonical failure categories and retryability hints needed by readiness/repository/workspace stories, including unsupported capability, unavailable provider, rate limit, insufficient permission, credential-reference invalid, known provider failure, unknown provider outcome, reconciliation required, validation error, and internal error.
  - [x] Treat provider failure categories as internal decision categories unless a named public Contract Spine gap is discovered and regenerated; provider-specific HTTP status bodies, SDK exception types, and adapter diagnostics remain opaque metadata.
  - [x] Separate known provider failures from unknown outcomes; unknown outcomes must produce metadata that downstream workflows can route to reconciliation instead of unsafe retry.
  - [x] Treat stale, ambiguous, drifted, duplicate, and internally conflicting capability evidence as validation/conflict/reconciliation outcomes with safe reason codes; do not silently downgrade them to unsupported capability or choose a winner by collection order.
  - [x] Ensure result types can carry safe remediation category, retry-after metadata where safe, profile version, correlation ID, and sanitized reason code.
  - [x] Do not surface raw HTTP status bodies, upstream headers containing sensitive data, provider installation identifiers, repository URLs, branch names, credential labels, or raw exception messages.
- [x] Add authorization-before-observation wrapper seams only where needed. (AC: 5, 6)
  - [x] If this story introduces an application-facing capability query service, place tenant/organization/provider-binding authorization before adapter lookup, credential reference inspection, capability cache lookup, repository lookup, projection read, audit write, or diagnostics.
  - [x] If no application-facing query service is introduced, add or document a narrow test seam proving the future wrapper ordering contract without creating REST, SDK, CLI, MCP, UI, worker, or EventStore handler behavior.
  - [x] Reuse existing tenant-access and ACL result families; do not invent a separate provider authorization vocabulary.
  - [x] Use `configure_provider_binding` only for configuration; define or reuse a narrower read/status permission only if an authorized capability-read path is included.
  - [x] Capture one immutable authorization/evidence snapshot per attempt so retries, duplicate checks, and profile-cache reads do not reuse stale positive authorization.
  - [x] Dimension any future cache/read-model seam by tenant/organization, provider binding, provider family/key, profile schema/version, target metadata, and authorization evidence freshness; denied paths must not warm or read shared capability evidence before authorization succeeds.
  - [x] Use in-memory spies/fakes to prove denied paths make zero calls to provider adapters, credential references, repository lookup, branch/workspace lookup, cache/projection/audit stores, capability comparison, metrics, or diagnostic enrichment.
- [x] Add an offline fake/test provider and conformance tests. (AC: 2, 3, 4, 7, 8, 10)
  - [x] Add a fake provider under test code or `Hexalith.Folders.Testing` that returns deterministic capability profiles for multiple provider families, including at least GitHub-like, Forgejo-like, and third-provider/custom-family examples.
  - [x] Keep fake providers transport-free and adapter-free; they may model capability metadata differences but must not simulate concrete GitHub/Forgejo SDK behavior, live transport errors, credential exchange, or adapter quirks.
  - [x] Add conformance tests proving capability discovery exposes supported, unsupported, partial, emulated, branch/ref, file-limit, credential-mode, rate-limit, retryability, version, and failure-category metadata without live provider calls.
  - [x] Add deterministic comparison tests proving equivalent profiles compare stable across ordering differences where allowed and changed evidence changes the profile fingerprint/version.
  - [x] Add tests for duplicate operation identifiers, conflicting support markers, provider-family alias/casing differences, missing profile schema/version, stale evidence, and target-version drift so the port returns stable safe failures instead of accepting malformed profiles.
  - [x] Add fingerprint tests covering stable collection ordering, case normalization, null handling, version normalization, ignored correlation/timestamp/diagnostic fields, and repeatable serialized shape when serialization is used.
  - [x] Add negative tests for unsupported provider family, disabled capability, malformed target evidence, stale profile version, unknown outcome, rate limit, permission failure, unavailable provider, and drift/incompatible version metadata.
  - [x] Add a guard fake that throws if any test attempts HTTP, Dapr, filesystem working-copy, Git, secret-store, Aspire, Redis, Keycloak, GitHub, Forgejo, or Tenants network access.
- [x] Preserve Contract Spine and parity alignment. (AC: 2, 4, 8, 9)
  - [x] Review `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`, `tests/fixtures/parity-contract.yaml`, and `TenantFolderProviderContractGroupTests` before changing any public provider-readiness/support evidence shape.
  - [x] If contract artifacts change, regenerate/update parity fixtures through the existing generator and update contract tests in the same change.
  - [x] If no public contract changes are needed, add tests or documentation proving the internal port maps to the existing provider readiness/support evidence vocabulary without drift.
  - [x] Do not manually edit generated SDK files under `src/Hexalith.Folders.Client/Generated/*`.
- [x] Add leakage and boundary tests. (AC: 5, 6, 10, 11)
  - [x] Use sentinel values for provider tokens, API keys, PEM/private keys, JWT-like strings, credential URLs, repository URLs with userinfo, raw provider JSON, branch names containing secrets, file contents, diffs, display names, emails, and unauthorized resource names.
  - [x] Assert commands, requests, results, profiles, exceptions, logs, diagnostics, projections if any, and test output do not contain forbidden values.
  - [x] Add dependency/architecture guard tests proving `src/Hexalith.Folders/Providers/Abstractions` does not reference GitHub/Forgejo clients, Octokit, Forgejo HTTP clients, Dapr, EventStore handlers, generated SDK, CLI, MCP, UI, Workers, Aspire, Redis, Keycloak, filesystem working-copy APIs, or live provider transports.
  - [x] Add negative-scope tests or source-shape assertions that this story did not add GitHub/Forgejo adapters, live provider clients, worker side effects, CLI/MCP/UI behavior, or generated SDK edits.

## Dev Notes

### Source Context

- Epic 3 requires provider configuration, readiness validation, repository-backed folder creation/binding, branch/ref policy, and provider support evidence without exposing secrets. Story 3.2 is the internal provider port and capability model needed before concrete GitHub and Forgejo adapters. [Source: `_bmad-output/planning-artifacts/epics.md#Story 3.2`]
- PRD FR15-FR23 require provider bindings, readiness diagnostics, provider/credential/repository/branch/capability metadata, and explicit GitHub/Forgejo capability differences. [Source: `_bmad-output/planning-artifacts/prd.md#Provider Readiness and Repository Binding`]
- Architecture requires GitHub and Forgejo not be treated as API-compatible base-URL swaps; capability differences must be modeled through provider ports and validated through provider contract tests before a provider is marked ready. [Source: `_bmad-output/planning-artifacts/architecture.md#Provider boundaries`]
- Architecture places provider abstractions under `src/Hexalith.Folders/Providers/Abstractions/` with `IGitProvider`, `ProviderCapabilities`, `ProviderReadiness`, and `ProviderFailureCategory`; concrete GitHub and Forgejo adapters are separate later components. [Source: `_bmad-output/planning-artifacts/architecture.md#Recommended Project Layout`]
- Project context requires metadata-only provider evidence, zero cross-tenant leakage, tenant-prefixed durable keys, fail-closed authorization, no raw provider tokens or credential material, and no recursive nested submodule initialization. [Source: `_bmad-output/project-context.md#Critical Don't-Miss Rules`]

### Previous Story Intelligence

- Story 3.1 created the provider binding configuration foundation. Story 3.2 must consume opaque provider binding references and credential-reference metadata without resolving credentials or doing readiness validation.
- Story 2.2 established organization aggregate ACL, idempotency, metadata leakage, stream-shape, and tenant evidence gate patterns. Any application-facing capability query wrapper should reuse those authorization-before-observation patterns.
- Story 2.6 is actively in review in this working tree. Do not overwrite its uncommitted artifact/status/source/test changes; rebase or inspect current source before implementation.
- Story 2.9 established worker-side event handling and one-authoritative-writer guardrails. Provider port code should not add worker process-manager side effects or duplicate event handling.
- Story 1.7 authored provider/repository binding contract groups; Story 1.13 generated the parity oracle. Public contract changes must preserve those governance gates.

### Existing Implementation State

- `src/Hexalith.Folders/Providers/` does not yet contain provider abstractions in the current source inventory.
- `src/Hexalith.Folders/Hexalith.Folders.csproj` already references Contracts and common Microsoft options/logging abstractions; provider abstractions should not require provider-specific packages.
- `tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj` references the core project and `Hexalith.Folders.Testing` with xUnit v3 and Shouldly.
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/TenantFolderProviderContractGroupTests.cs` already guards provider binding/readiness/support evidence operations, idempotency metadata, safe denial shapes, and negative scope.
- `tests/fixtures/parity-contract.yaml` is generated by the parity oracle and must not be hand-edited when public contract rows change.

### Required Architecture Patterns

- Use .NET 10, file-scoped namespaces, nullable-aware C#, one public type per file, PascalCase public members, camelCase locals/parameters, and async APIs with `CancellationToken` for any I/O-capable provider methods.
- Keep provider abstractions in core/domain code and concrete provider clients in later adapter stories. Do not add Octokit or Forgejo HTTP dependencies in this story.
- Keep capability discovery in this story metadata-only and static/fake-provider driven. Live readiness probing, provider SDK calls, credential validation, retry execution, and adapter-specific failure translation are later-story concerns.
- Keep `Hexalith.Folders.Contracts` behavior-free. Contract DTO/OpenAPI changes are allowed only when needed for the existing Contract Spine, not to host provider decision logic.
- Keep aggregates pure. `OrganizationAggregate` and `FolderAggregate` must not call provider ports, Dapr, HTTP, Git, filesystem, secret stores, clocks, random, or EventStore services.
- Use metadata-only structured logs and result fields. Never log raw provider responses, credential material, repository URLs with credentials, file contents, diffs, branch secrets, installation secrets, or unauthorized existence details.
- Treat capability discovery as evidence collection for later readiness decisions, not as repository provisioning or workspace mutation.
- Treat normalized provider identity and canonical operation identifiers as part of the port contract. If an adapter-specific alias is accepted later, it must be mapped before the core profile is constructed so downstream workflows never branch on raw provider labels.
- Capability profile fingerprints and future cache keys must never be global by provider family alone. They need tenant/organization, provider binding, target metadata, profile schema/version, and authorization-evidence freshness dimensions to avoid cross-binding or cross-tenant reuse.

### Files To Touch

- `src/Hexalith.Folders/Providers/Abstractions/IGitProvider.cs`
- `src/Hexalith.Folders/Providers/Abstractions/*Capability*.cs`
- `src/Hexalith.Folders/Providers/Abstractions/*Readiness*.cs` only for metadata/result models needed by this story
- `src/Hexalith.Folders/Providers/Abstractions/*Failure*.cs`
- `src/Hexalith.Folders/Providers/Abstractions/*CredentialMode*.cs`
- `src/Hexalith.Folders/Providers/Abstractions/*BranchRef*.cs`
- `src/Hexalith.Folders/Providers/Abstractions/*FileLimit*.cs`
- `src/Hexalith.Folders.Testing/Providers/*` if reusable fake providers belong in testing utilities
- `tests/Hexalith.Folders.Tests/Providers/Abstractions/*`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/TenantFolderProviderContractGroupTests.cs` only if public contract assertions need tightening
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` and `tests/fixtures/parity-contract.yaml` only if the public provider readiness/support evidence contract intentionally changes

### Do Not Touch

- Do not implement `Hexalith.Folders.Providers.GitHub`, Octokit adapters, Forgejo typed HTTP clients, provider readiness execution, repository creation, repository binding, branch creation, workspace preparation, file mutation, commits, provider webhooks, nightly drift polling, repair workflows, CLI/MCP commands, UI pages, or generated SDK clients.
- Do not resolve or validate live credential references; use only opaque non-secret references and credential mode metadata.
- Do not store raw provider tokens, passwords, private keys, API keys, credential URLs, raw provider payloads, installation secrets, repository URLs with credentials, branch secrets, file contents, diffs, generated context payloads, raw claims, display names, emails, or unauthorized resource existence.
- Do not add direct references from Contracts, Client, CLI, MCP, UI, or Server transports into concrete provider adapters.
- Do not initialize nested submodules or use recursive submodule commands.

### Testing

- Use xUnit v3 and Shouldly. Use NSubstitute only where a real seam needs substitution.
- Provider abstraction tests should use fake providers and in-memory evidence only. They must not require live GitHub, Forgejo, provider credentials, Tenants services, Dapr sidecars, Aspire, Redis, Keycloak, or nested submodule initialization.
- Conformance tests should cover at least three provider-family profiles to prove the model is N-provider-ready and not hardcoded to GitHub/Forgejo.
- Failure tests should cover unsupported capability, partial support, emulated support, malformed target evidence, stale profile version, drift/incompatible version, rate limit, unavailable provider, insufficient permission, unknown outcome, and reconciliation-required classification.
- Malformed-profile tests should cover duplicate canonical operation IDs, conflicting support markers for the same operation, user-controlled provider labels, missing schema/version metadata, and stale target evidence.
- Authorization/order tests should use spies or fakes to assert denied/missing paths make zero calls to adapter lookup, credential reference lookup, repository/branch/workspace lookup, cache/projection/audit stores, capability comparison, metrics, logs, or diagnostic enrichment.
- Leakage tests should serialize provider requests/results/profiles and capture logs/exceptions where applicable, then assert sentinel credentials, provider payloads, repository URLs with credentials, branch secrets, file contents, diffs, display names, emails, and unauthorized resource names are absent.
- Dependency guard tests should fail if the abstraction layer references concrete provider clients, public transports, generated SDK clients, worker process managers, live provider APIs, or runtime infrastructure packages that are out of scope for an internal port model.
- Contract/parity tests should run only when public artifacts change; otherwise add a regression test proving the internal model can map to existing Contract Spine provider-readiness/support evidence terms.

### Regression Traps

- Do not make `IGitProvider` a GitHub-shaped interface with Forgejo as a base-URL variation.
- Do not close provider kind to exactly two values unless a public Contract Spine field explicitly owns that closed set.
- Do not use booleans such as `IsGitHub` or `SupportsForgejo` where a capability profile should carry operation-level support metadata.
- Do not let adapter exception messages become product error categories or public diagnostics.
- Do not resolve credential references, validate live credentials, or infer provider readiness in the provider-port definition story.
- Do not query providers before tenant/organization/provider-binding authorization succeeds in any application-facing wrapper.
- Do not cache capability profiles across tenants or provider bindings without tenant/provider-binding dimensions and authorization freshness.
- Do not build profile equality, fingerprinting, or cache lookup from provider family alone; include provider binding, target evidence, schema/version, and safe authorization snapshot dimensions.
- Do not accept duplicate/conflicting operation capability rows by taking the first or last entry.
- Do not classify stale or drifted evidence as simply unsupported; preserve reconciliation/readiness-failed semantics so later workflows can choose safe recovery.
- Do not include raw provider payloads for "debugging" in profile evidence, logs, traces, or test snapshots.
- Do not let unknown provider outcomes retry automatically in a way that could duplicate repositories, file mutations, commits, or audit records.
- Do not change generated parity rows by hand.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 3.2`
- `_bmad-output/planning-artifacts/prd.md#Provider Readiness and Repository Binding`
- `_bmad-output/planning-artifacts/prd.md#Integration and Contract Compatibility`
- `_bmad-output/planning-artifacts/architecture.md#Provider boundaries`
- `_bmad-output/planning-artifacts/architecture.md#Domain Layout`
- `_bmad-output/planning-artifacts/architecture.md#Recommended Project Layout`
- `_bmad-output/project-context.md#Critical Don't-Miss Rules`
- `_bmad-output/implementation-artifacts/3-1-configure-provider-binding-and-credential-reference.md`
- `_bmad-output/implementation-artifacts/2-2-implement-organization-aggregate-acl-baseline.md`
- `_bmad-output/implementation-artifacts/2-6-enforce-layered-authorization-with-safe-denials.md`
- `_bmad-output/implementation-artifacts/2-9-react-to-tenants-events-through-worker-handlers.md`
- `src/Hexalith.Folders/Hexalith.Folders.csproj`
- `tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/TenantFolderProviderContractGroupTests.cs`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`
- `tests/fixtures/parity-contract.yaml`

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-24 | Adversarial code review (auto-fix): corrected `unsupported_provider_capability` retryability semantics (permanent, not retryable), added real provider-payload sentinel injection/rejection leakage coverage, completed File List documentation. Status → done. | Amelia (AI Review) |
| 2026-05-24 | Implemented internal provider port abstractions, static capability model, offline fake provider conformance tests, authorization-before-observation seam, leakage/dependency guards, and contract negative-scope allowance for provider abstractions. | Codex |
| 2026-05-19 | Applied advanced elicitation hardening for normalized provider/operation identity, cache/fingerprint dimensions, stale/conflicting evidence semantics, and malformed-profile tests. | Codex |
| 2026-05-19 | Applied party-mode review hardening for metadata-only discovery boundaries, internal failure taxonomy, authorization-before-observation seams, deterministic fingerprint rules, and hermetic conformance/leakage tests. | Codex |
| 2026-05-19 | Created story with N-provider-ready provider port, metadata-only capability model, failure taxonomy, offline fake-provider conformance tests, authorization-before-observation guardrails, and Contract Spine alignment. | Codex |

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

### Completion Notes List

- Story created by `/bmad-create-story 3-2-define-igitprovider-port-and-capability-model` equivalent workflow on 2026-05-19.
- Project context, Epic 3, PRD provider-readiness requirements, architecture provider boundaries, Story 3.1, current contract/parity tests, recent commits, and story-creation lessons were reviewed.
- Preflight working-tree failure was classified as an active-dev-story soft warning because Story 2.6 is `review` in both its artifact and sprint status; active development changes were left untouched.
- Ultimate context engine analysis completed - comprehensive developer guide created.
- Party-mode review applied low-risk story hardening for internal-only provider port boundaries, static metadata-only capability discovery, no public Contract Spine drift without explicit gap, authorization-before-observation evidence seams, deterministic profile fingerprint normalization, fake-provider conformance, leakage, and dependency guard tests.
- Advanced elicitation applied low-risk hardening for normalized provider/operation identifiers, malformed-profile rejection, tenant/provider-binding-scoped fingerprints and cache dimensions, stale/drift semantics, and duplicate/conflicting capability evidence tests.
- Implemented `IGitProvider` as a narrow internal port for provider identity, capability discovery, and profile comparison only.
- Added metadata-only capability profile, operation support, credential mode, rate-limit posture, failure category, retryability, safe target evidence, authorization snapshot, and deterministic fingerprint/version models.
- Added normalization and validation for provider family/key and canonical operation identifiers, including deterministic duplicate/conflicting operation failures and stale/incompatible evidence outcomes.
- Added an authorization-before-observation discovery service seam with tests proving denied paths make zero provider/evidence-store calls.
- Added offline fake providers and conformance, fingerprint, failure taxonomy, leakage, and dependency-boundary tests; no public OpenAPI/parity artifacts or generated SDK files were changed.
- Validation completed: `dotnet build Hexalith.Folders.slnx --no-restore`, focused provider abstraction tests, contract tests, and full `dotnet test Hexalith.Folders.slnx --no-build` all passed.

### File List

- `src/Hexalith.Folders/Providers/Abstractions/IGitProvider.cs`
- `src/Hexalith.Folders/Providers/Abstractions/IProviderCapabilityAuthorizer.cs`
- `src/Hexalith.Folders/Providers/Abstractions/IProviderCapabilityEvidenceStore.cs`
- `src/Hexalith.Folders/Providers/Abstractions/IProviderCapabilityResolver.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderAuthorizationEvidenceSnapshot.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderCapabilityAuthorizationResult.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderCapabilityComparisonResult.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderCapabilityDiscoveryRequest.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderCapabilityDiscoveryResult.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderCapabilityDiscoveryService.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderCapabilityOperationRow.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderCapabilityProfile.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderCapabilityProfileFactory.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderCapabilityProfileVersion.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderCredentialMode.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderFailureCategory.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderFailureCategoryExtensions.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderIdentityIdentifier.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderOperationCapability.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderOperationCatalog.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderOperationIdentifier.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderOperationSupport.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderRateLimitPosture.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderTargetEvidence.cs`
- `src/Hexalith.Folders.Testing/Providers/FakeGitProvider.cs`
- `src/Hexalith.Folders.Testing/Providers/ProviderCapabilityTestData.cs`
- `src/Hexalith.Folders.Testing/Providers/RecordingProviderCapabilityAuthorizer.cs`
- `src/Hexalith.Folders.Testing/Providers/RecordingProviderCapabilityEvidenceStore.cs`
- `src/Hexalith.Folders.Testing/Providers/RecordingProviderCapabilityResolver.cs`
- `tests/Hexalith.Folders.Tests/Providers/Abstractions/ProviderCapabilityBoundaryTests.cs`
- `tests/Hexalith.Folders.Tests/Providers/Abstractions/ProviderCapabilityDiscoveryWorkflowTests.cs`
- `tests/Hexalith.Folders.Tests/Providers/Abstractions/ProviderCapabilityFailureTests.cs`
- `tests/Hexalith.Folders.Tests/Providers/Abstractions/ProviderCapabilityProfileTests.cs`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuditOpsConsoleContractGroupTests.cs`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/CommitStatusContractGroupTests.cs`

## Party-Mode Review

- ISO date and time: 2026-05-19T14:28:54+02:00
- Selected story key: 3-2-define-igitprovider-port-and-capability-model
- Command/skill invocation used: `/bmad-party-mode 3-2-define-igitprovider-port-and-capability-model; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), John (Product Manager)
- Findings summary:
  - AC2 needed a clearer minimum capability profile shape and a boundary between required, optional, and later adapter-specific metadata.
  - AC4 needed stable internal provider failure semantics and explicit protection against accidental public Contract Spine drift.
  - AC5 needed a sharper authorization-before-observation boundary covering cache, projection, audit, metrics/logs, diagnostics, and capability comparison.
  - AC7 needed deterministic profile comparison rules for ordering, casing, null handling, version normalization, and ignored correlation/timestamp/diagnostic fields.
  - AC10 needed a stronger hermetic test matrix, fake-provider conformance expectations, leakage sentinel assertions, and dependency guard tests.
- Changes applied:
  - Tightened acceptance criteria for static metadata-only capability discovery, opaque tenant/organization-scoped binding references, internal failure taxonomy, no pre-auth existence leakage, normalized fingerprints, and no expected public contract changes.
  - Added task guidance for a narrow initial `IGitProvider` surface, no live provider probing, zero-call denied-path test seams, fake-provider boundaries, fingerprint stability tests, and dependency guard tests.
  - Added Dev Notes clarifying metadata-only provider port scope, deferred live readiness behavior, authorization/order spy tests, and dependency guard expectations.
- Findings deferred:
  - Concrete GitHub/Forgejo adapter mapping and provider-specific capability vocabulary.
  - Live capability probing, readiness execution, retry/rate-limit execution policy, and adapter-specific failure translation.
  - Credential resolution/validation, repository creation/binding, branch/workspace/file/commit operations, public capability profile exposure, and SDK generation changes.
- Final recommendation: ready-for-dev

## Advanced Elicitation

- ISO date and time: 2026-05-19T17:07:19+02:00
- Selected story key: 3-2-define-igitprovider-port-and-capability-model
- Command/skill invocation used: `/bmad-advanced-elicitation 3-2-define-igitprovider-port-and-capability-model`
- Batch 1 method names: Security Audit Personas; Failure Mode Analysis; Pre-mortem Analysis; Self-Consistency Validation; Critique and Refine
- Reshuffled Batch 2 method names: Red Team vs Blue Team; Chaos Monkey Scenarios; First Principles Analysis; 5 Whys Deep Dive; Architecture Decision Records
- Findings summary:
  - Provider family/key and operation identifiers needed a single normalization boundary so later adapters cannot smuggle user-controlled labels or two-provider switches into core semantics.
  - Capability fingerprints and future cache keys needed explicit tenant, organization, provider-binding, target, schema/version, and authorization-evidence dimensions to avoid cross-tenant or cross-binding reuse.
  - Stale, drifted, duplicate, ambiguous, and conflicting capability evidence needed distinct safe failure semantics instead of last-writer-wins or unsupported-capability fallbacks.
  - Test guidance needed malformed-profile coverage for duplicate operation identifiers, conflicting support markers, missing schema/version, alias/casing normalization, stale evidence, and target-version drift.
- Changes applied:
  - Tightened AC2, AC4, and AC7 for normalized identity, duplicate/conflicting evidence handling, stale/drift semantics, and scoped metadata-only fingerprints/cache keys.
  - Added task guidance for provider/operation comparer behavior, malformed-profile rejection, authorization-scoped cache dimensions, and safe failure classification.
  - Added Dev Notes, testing, and regression-trap guidance covering provider identity normalization, cross-binding cache risks, duplicate/conflicting operation rows, and stale-evidence semantics.
- Findings deferred:
  - Concrete alias tables and provider-family vocabularies for GitHub, Forgejo, and future adapters.
  - Live provider capability probing, cache persistence strategy, readiness recovery policy, and adapter-specific stale/drift detection mechanisms.
  - Any public Contract Spine change for provider capability profile exposure.
- Final recommendation: ready-for-dev

## Senior Developer Review (AI)

- Reviewer: Jérôme Piquot (automated adversarial review)
- Date: 2026-05-24
- Outcome: **Approve** (auto-fixed). 0 Critical remaining; 1 High and 2 Medium found and fixed.
- Review scope: story File List + git-discovered changes for `src/Hexalith.Folders/Providers/Abstractions/*`, `src/Hexalith.Folders.Testing/Providers/*`, `tests/Hexalith.Folders.Tests/Providers/Abstractions/*`, and the two contract-group guard exclusions. `_bmad/`, `_bmad-output/`, and skill/config folders were excluded.

### Findings and resolutions

1. **[HIGH][AC4, AC8] `unsupported_provider_capability` was retryable-by-default.**
   `ProviderFailureCategoryExtensions.IsRetryableByDefault` returned `true` for `UnsupportedProviderCapability`. An unsupported capability is a stable/permanent condition; retrying capability discovery can never succeed and would push downstream readiness/repository workflows into futile retry loops, contradicting AC4's "stable internal failure categories" and the regression-trap guidance on unsafe automatic retry (capability changes flow through `reconciliation_required`, not short-term retry).
   - Fix: removed `UnsupportedProviderCapability` from `IsRetryableByDefault` (`ProviderFailureCategoryExtensions.cs`); corrected the `ProviderCapabilityFailureTests` theory expectation from `true` → `false`.

2. **[MEDIUM] File List incomplete.**
   `tests/Hexalith.Folders.Tests/Providers/Abstractions/ProviderCapabilityDiscoveryWorkflowTests.cs` existed in git and is referenced in `tests/test-summary.md` but was missing from Dev Agent Record → File List.
   - Fix: added the file to the File List.

3. **[MEDIUM][AC6] Trivially-passing leakage test.**
   `ProviderCapabilityBoundaryTests.RequestsAndResultsShouldNotSerializeCredentialOrProviderPayloadSentinels` serialized a clean request that never contained the forbidden sentinels, so the assertions passed trivially. The PEM/JWT/repo-URL-with-userinfo/branch-secret/diff/email sentinels were never exercised through the rejection path.
   - Fix: added `ProviderPayloadShapedEvidenceShouldBeRejectedAndNeverSerializedIntoResults`, which injects each credential/provider-payload-shaped sentinel as provider evidence metadata and asserts the port rejects it (`provider_validation_failed`, null profile) and that the serialized result never contains the sentinel.

### Verification

- `dotnet test ... --filter FullyQualifiedName~Providers.Abstractions` → 33/33 passing (was 32; +1 new leakage test).
- `dotnet test tests/Hexalith.Folders.Tests` → 513/513 passing, 0 failed.
- `dotnet test tests/Hexalith.Folders.Contracts.Tests --filter ...TenantFolderProviderContractGroupTests` → 5/5 passing.

### Notes (no change required)

- AC1/AC3/AC11: `IGitProvider` surface is narrow (identity + discovery + comparison); capability differences are modeled as data rows, not two-provider switches; no adapters, transports, or generated SDK edits were added.
- AC5: `ProviderCapabilityDiscoveryService` authorizes before touching the evidence store, resolver, or provider; denied-path test proves zero downstream calls.
- The `AuditOpsConsoleContractGroupTests`/`CommitStatusContractGroupTests` edits correctly exclude only `Providers/Abstractions/` (the in-scope port) from the "no provider adapters" negative-scope guard; `Providers/**` adapters remain guarded.
