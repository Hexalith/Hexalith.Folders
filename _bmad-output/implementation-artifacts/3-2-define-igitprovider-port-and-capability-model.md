# Story 3.2: Define IGitProvider port and capability model

Status: ready-for-dev

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
2. Given a provider capability profile is queried through the port, when the provider responds successfully, then the result includes provider family/kind, provider binding reference, safe target/version metadata, supported operation set, unsupported/partial/emulated operation markers, branch/ref behavior, file-size and file-operation limits, credential mode requirements, retryability hints, rate-limit posture, known failure mappings, capability profile version, correlation ID, and metadata-only evidence.
3. Given GitHub, Forgejo, or any future provider has capability differences, when those differences are modeled, then they are represented as data on the capability profile instead of conditional product semantics, hardcoded two-provider switches, base-URL compatibility assumptions, or adapter-specific public contracts.
4. Given the port is asked to perform capability discovery with unsupported, unavailable, stale, malformed, disabled, drifted, ambiguous, or version-incompatible provider evidence, when the result is returned, then it maps to stable provider result/failure categories such as `unsupported_provider_capability`, `provider_unavailable`, `provider_rate_limited`, `provider_permission_insufficient`, `provider_readiness_failed`, `provider_failure_known`, `unknown_provider_outcome`, or `reconciliation_required` as appropriate, without leaking raw provider diagnostics or unauthorized resource existence.
5. Given an actor is unauthorized, tenant evidence is disabled/stale/unavailable/malformed/mismatched/replay-conflicting, or provider binding metadata cannot be observed safely, when a caller attempts provider capability discovery through an application service or query wrapper in this story, then authorization and tenant/organization scoping complete before any provider adapter, credential reference, repository, branch, workspace, cache, projection, audit writer, or capability store is touched; denied paths return existing safe denial/read-model unavailable result families without confirming whether provider binding or capability evidence exists.
6. Given a capability discovery attempt carries credential references, when the port receives the request, then it uses only opaque credential reference metadata and explicit credential mode requirements; it must not resolve, fetch, validate, log, serialize, compare, project, or return raw provider credentials, token-shaped strings, PEM/JWT-shaped values, credential URLs, private keys, connection strings, secret-shaped JSON keys, or raw provider payloads.
7. Given capability discovery is retried with the same provider binding and safe target evidence, when the fake/test provider returns equivalent metadata, then the profile result is deterministic and can be compared in tests without depending on ordering of capabilities where the model defines collections as unordered; given evidence changes, then profile version/fingerprint or result metadata changes without mutating organization/folder aggregate state.
8. Given a provider operation is unsupported, partially supported, emulated, rate-limited, unavailable, or blocked by missing credential mode, when downstream readiness or repository workflows inspect capability metadata, then they can make a decision from the profile without calling provider-specific APIs directly or parsing adapter-specific exception text.
9. Given the Contract Spine already contains provider readiness/support evidence operations, when this story touches contract or parity artifacts, then it preserves OpenAPI 3.1 extension semantics, canonical error categories, operation IDs, parity fixture ownership, and NSwag generation boundaries; generated client files are not manually edited.
10. Given this story is complete, when tests run in a developer machine or CI PR gate, then provider-port unit/conformance tests pass offline using fake/test providers and no GitHub, Forgejo, provider credentials, live provider APIs, tenant seed data, Aspire, Dapr sidecars, Redis, Keycloak, or nested submodule initialization.
11. Given GitHub and Forgejo adapters are later stories, when this story is implemented, then it must not implement Octokit adapters, Forgejo HTTP clients, live provider readiness checks, repository creation/binding, branch creation, workspace preparation, file operations, commits, webhooks, drift polling, nightly live provider tests, CLI/MCP commands, UI pages, or repair workflows.

## Tasks / Subtasks

- [ ] Add the provider abstraction model in the core project. (AC: 1, 2, 3, 8, 11)
  - [ ] Create `src/Hexalith.Folders/Providers/Abstractions/IGitProvider.cs` and related one-public-type-per-file models such as `ProviderCapabilityProfile`, `ProviderOperationCapability`, `ProviderOperationSupport`, `ProviderCapabilityDiscoveryRequest`, `ProviderCapabilityDiscoveryResult`, `ProviderCapabilityProfileVersion`, and `ProviderFailureCategory`.
  - [ ] Keep the abstractions behavior-free with respect to public transports; do not add REST endpoint handlers, generated client edits, CLI/MCP tools, UI pages, worker process managers, or provider-specific adapters in this story.
  - [ ] Model capability support as extensible data keyed by canonical operation identifiers rather than boolean GitHub/Forgejo properties or closed two-provider enums.
  - [ ] Include safe target/version metadata and capability profile fingerprints so future readiness and drift stories can compare evidence without storing raw provider payloads.
  - [ ] Keep provider operation names aligned with the existing Contract Spine and parity vocabulary where those operations already exist.
- [ ] Define provider failure and retry semantics at the port boundary. (AC: 4, 8)
  - [ ] Add canonical failure categories and retryability hints needed by readiness/repository/workspace stories, including unsupported capability, unavailable provider, rate limit, insufficient permission, credential-reference invalid, known provider failure, unknown provider outcome, reconciliation required, validation error, and internal error.
  - [ ] Separate known provider failures from unknown outcomes; unknown outcomes must produce metadata that downstream workflows can route to reconciliation instead of unsafe retry.
  - [ ] Ensure result types can carry safe remediation category, retry-after metadata where safe, profile version, correlation ID, and sanitized reason code.
  - [ ] Do not surface raw HTTP status bodies, upstream headers containing sensitive data, provider installation identifiers, repository URLs, branch names, credential labels, or raw exception messages.
- [ ] Add authorization-before-observation wrapper seams only where needed. (AC: 5, 6)
  - [ ] If this story introduces an application-facing capability query service, place tenant/organization/provider-binding authorization before adapter lookup, credential reference inspection, capability cache lookup, repository lookup, projection read, audit write, or diagnostics.
  - [ ] Reuse existing tenant-access and ACL result families; do not invent a separate provider authorization vocabulary.
  - [ ] Use `configure_provider_binding` only for configuration; define or reuse a narrower read/status permission only if an authorized capability-read path is included.
  - [ ] Capture one immutable authorization/evidence snapshot per attempt so retries, duplicate checks, and profile-cache reads do not reuse stale positive authorization.
- [ ] Add an offline fake/test provider and conformance tests. (AC: 2, 3, 4, 7, 8, 10)
  - [ ] Add a fake provider under test code or `Hexalith.Folders.Testing` that returns deterministic capability profiles for multiple provider families, including at least GitHub-like, Forgejo-like, and third-provider/custom-family examples.
  - [ ] Add conformance tests proving capability discovery exposes supported, unsupported, partial, emulated, branch/ref, file-limit, credential-mode, rate-limit, retryability, version, and failure-category metadata without live provider calls.
  - [ ] Add deterministic comparison tests proving equivalent profiles compare stable across ordering differences where allowed and changed evidence changes the profile fingerprint/version.
  - [ ] Add negative tests for unsupported provider family, disabled capability, malformed target evidence, stale profile version, unknown outcome, rate limit, permission failure, unavailable provider, and drift/incompatible version metadata.
  - [ ] Add a guard fake that throws if any test attempts HTTP, Dapr, filesystem working-copy, Git, secret-store, Aspire, Redis, Keycloak, GitHub, Forgejo, or Tenants network access.
- [ ] Preserve Contract Spine and parity alignment. (AC: 2, 4, 8, 9)
  - [ ] Review `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`, `tests/fixtures/parity-contract.yaml`, and `TenantFolderProviderContractGroupTests` before changing any public provider-readiness/support evidence shape.
  - [ ] If contract artifacts change, regenerate/update parity fixtures through the existing generator and update contract tests in the same change.
  - [ ] If no public contract changes are needed, add tests or documentation proving the internal port maps to the existing provider readiness/support evidence vocabulary without drift.
  - [ ] Do not manually edit generated SDK files under `src/Hexalith.Folders.Client/Generated/*`.
- [ ] Add leakage and boundary tests. (AC: 5, 6, 10, 11)
  - [ ] Use sentinel values for provider tokens, API keys, PEM/private keys, JWT-like strings, credential URLs, repository URLs with userinfo, raw provider JSON, branch names containing secrets, file contents, diffs, display names, emails, and unauthorized resource names.
  - [ ] Assert commands, requests, results, profiles, exceptions, logs, diagnostics, projections if any, and test output do not contain forbidden values.
  - [ ] Add negative-scope tests or source-shape assertions that this story did not add GitHub/Forgejo adapters, live provider clients, worker side effects, CLI/MCP/UI behavior, or generated SDK edits.

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
- Keep `Hexalith.Folders.Contracts` behavior-free. Contract DTO/OpenAPI changes are allowed only when needed for the existing Contract Spine, not to host provider decision logic.
- Keep aggregates pure. `OrganizationAggregate` and `FolderAggregate` must not call provider ports, Dapr, HTTP, Git, filesystem, secret stores, clocks, random, or EventStore services.
- Use metadata-only structured logs and result fields. Never log raw provider responses, credential material, repository URLs with credentials, file contents, diffs, branch secrets, installation secrets, or unauthorized existence details.
- Treat capability discovery as evidence collection for later readiness decisions, not as repository provisioning or workspace mutation.

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
- Leakage tests should serialize provider requests/results/profiles and capture logs/exceptions where applicable, then assert sentinel credentials, provider payloads, repository URLs with credentials, branch secrets, file contents, diffs, display names, emails, and unauthorized resource names are absent.
- Contract/parity tests should run only when public artifacts change; otherwise add a regression test proving the internal model can map to existing Contract Spine provider-readiness/support evidence terms.

### Regression Traps

- Do not make `IGitProvider` a GitHub-shaped interface with Forgejo as a base-URL variation.
- Do not close provider kind to exactly two values unless a public Contract Spine field explicitly owns that closed set.
- Do not use booleans such as `IsGitHub` or `SupportsForgejo` where a capability profile should carry operation-level support metadata.
- Do not let adapter exception messages become product error categories or public diagnostics.
- Do not resolve credential references, validate live credentials, or infer provider readiness in the provider-port definition story.
- Do not query providers before tenant/organization/provider-binding authorization succeeds in any application-facing wrapper.
- Do not cache capability profiles across tenants or provider bindings without tenant/provider-binding dimensions and authorization freshness.
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

### File List
