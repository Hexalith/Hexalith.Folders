# Story 3.3: Implement GitHub provider adapter

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a platform engineer,
I want a GitHub provider adapter using Octokit,
so that tenants can create and bind GitHub repositories through the canonical provider port.

## Terms

- GitHub provider adapter means the concrete `Hexalith.Folders.Providers.GitHub` implementation behind the internal `IGitProvider` port from Story 3.2.
- Octokit means the .NET GitHub API client package pinned by architecture as `Octokit` 14.0.0 and referenced through central package management only.
- GitHub binding means the tenant/organization-scoped provider binding metadata and opaque credential reference configured by Story 3.1.
- GitHub credential reference means an opaque non-secret reference. This story may request a credential through the approved credential seam only after authorization succeeds, but it must never persist, log, project, return, compare, or snapshot token material.
- Canonical provider result means the internal Folders provider result/failure taxonomy from Story 3.2, not raw Octokit exception text or GitHub response bodies.
- Unknown provider outcome means GitHub accepted, may have accepted, or timed out during an operation where safe retry could duplicate a repository, branch/ref mutation, file change, commit, audit, or readiness state.

## Acceptance Criteria

1. Given Story 3.1 provider binding metadata and Story 3.2 `IGitProvider` abstractions exist, when the GitHub adapter is added, then it lives under a concrete GitHub provider area such as `src/Hexalith.Folders/Providers/GitHub/`, implements only the provider-port members needed for GitHub readiness, repository, branch/ref, file, commit, and status operations defined by the current port, and does not change public REST, SDK, CLI, MCP, UI, EventStore command, or Contract Spine semantics unless a named gap is documented and regenerated.
2. Given package references are added, when the solution restores, then `Octokit` is referenced through `Directory.Packages.props` at the architecture-pinned version `14.0.0` and consumed only by the concrete GitHub provider implementation or its GitHub-specific tests; provider abstractions, Contracts, Client, CLI, MCP, UI, Server transports, aggregates, and Workers do not gain direct Octokit dependencies.
3. Given an actor is unauthorized, tenant evidence is disabled/stale/unavailable/malformed/mismatched/replay-conflicting, or the provider binding cannot be observed safely, when a GitHub provider operation is attempted through an application-facing workflow, then authorization and tenant/organization/provider-binding scoping complete before adapter construction, credential lookup, Octokit client creation, repository lookup, branch/ref lookup, capability cache lookup, readiness probing, audit writing, metrics/log enrichment, or diagnostics; denied and missing paths return existing safe denial/read-model unavailable families without confirming whether the GitHub binding, credential reference, installation, owner, repository, branch, or capability evidence exists.
4. Given a GitHub credential reference is authorized for use, when the adapter creates an Octokit client or installation/user access token context, then token material remains in memory only for the call boundary, is never stored in aggregate state, events, projections, profiles, exceptions, logs, traces, metrics, diagnostics, test snapshots, or command arguments, and any GitHub App installation identifiers or owner/repository names are treated as sensitive unless already authorized and explicitly modeled as safe metadata.
5. Given GitHub readiness is checked, when the adapter validates capability evidence, then it maps GitHub App/fine-grained permission evidence, repository visibility constraints, branch/ref support, contents access, commit/status support, rate-limit posture, API version evidence, and Octokit/GitHub response categories into the internal capability/readiness model from Story 3.2 without exposing raw provider payloads, endpoint URLs containing secrets, or unauthorized resource existence.
6. Given a GitHub repository creation or binding operation is requested by a later workflow, when GitHub returns success, existing-equivalent state, validation failure, name conflict, missing permission, missing owner, deleted/missing repository, branch protection conflict, rate limit, timeout, 5xx, abuse/secondary-rate-limit response, or unexpected transport failure, then the adapter returns stable canonical provider result categories such as validation failure, provider authentication required, provider permission insufficient, provider conflict, provider rate limited, provider unavailable, known provider failure, unknown provider outcome, or reconciliation required as appropriate; raw Octokit exception messages and GitHub response bodies are not product semantics.
7. Given an operation may have partially applied on GitHub, when the adapter cannot prove the outcome, then it returns `unknown_provider_outcome` or `reconciliation_required` with safe metadata for later reconciliation instead of silently retrying, issuing a second mutating call, creating duplicate repositories, changing branch refs twice, duplicating file mutations, or fabricating success.
8. Given branch/ref, file, commit, and status operations are implemented through the provider port, when GitHub-specific capabilities differ from Forgejo or future providers, then those differences are represented as capability metadata and canonical result categories rather than GitHub-specific branches in product workflows, public DTOs, CLI/MCP behavior, or UI semantics.
9. Given GitHub REST API behavior assumptions are used, when the adapter sets request headers or maps endpoint behavior, then the chosen GitHub API version and required GitHub App/fine-grained permissions are pinned in configuration or source constants, covered by tests, and recorded as compatibility evidence; implementation must not depend on ambient "latest" behavior.
10. Given the adapter emits telemetry, diagnostics, audit evidence, readiness evidence, or test output, when sentinel values for provider tokens, JWTs, PEM/private keys, credential URLs, repository URLs with userinfo, raw GitHub payloads, branch names with secrets, file contents, diffs, owner names, repository names, emails, installation IDs, and unauthorized resource names are present in inputs or mocked responses, then all observable outputs remain metadata-only and fail tests if forbidden values appear.
11. Given this story is complete, when unit and contract tests run in a developer machine or CI PR gate, then GitHub adapter tests pass offline with fake Octokit/GitHub seams or recorded hermetic fixtures and no live GitHub, Forgejo, provider credentials, tenant seed data, Aspire, Dapr sidecars, Redis, Keycloak, or nested submodule initialization.
12. Given live GitHub drift or real installation validation is needed, when this story is implemented, then those checks are deferred to the provider contract/live-nightly drift path and are not required for PR-gate unit tests, local builds, or story completion.

## Tasks / Subtasks

- [ ] Add the concrete GitHub provider package boundary. (AC: 1, 2, 8)
  - [ ] Create `src/Hexalith.Folders/Providers/GitHub/` with `GitHubProvider` and one-public-type-per-file support models needed by the existing `IGitProvider` port.
  - [ ] Add `Octokit` to central package management at version `14.0.0` and reference it only from the project that owns concrete provider adapters.
  - [ ] Keep `src/Hexalith.Folders/Providers/Abstractions/` free of Octokit, GitHub DTOs, GitHub endpoint names, GitHub-specific exception types, and GitHub-specific package references.
  - [ ] Keep aggregates, Contracts, generated clients, REST endpoints, CLI, MCP, UI, and Workers from referencing Octokit or concrete GitHub adapter types directly.
  - [ ] If Story 3.2 abstractions are not yet implemented when development begins, pause or implement only against the approved Story 3.2 port contract; do not create a competing `IGitProvider` shape in this story.
- [ ] Implement authorized GitHub client construction and credential handling. (AC: 3, 4, 9, 10)
  - [ ] Resolve credential references only through the approved authorized credential seam after tenant, organization, ACL, and provider-binding authorization succeeds.
  - [ ] Build Octokit clients from short-lived in-memory credential material and dispose or release any credential-bearing objects according to the local abstraction.
  - [ ] Set explicit product/user-agent and GitHub API version behavior according to the pinned compatibility decision; do not rely on ambient defaults that can drift silently.
  - [ ] Treat owner, repository, installation, branch/ref, and credential labels as sensitive before authorization and as safe metadata only when explicitly allowed by the internal provider evidence model.
  - [ ] Add tests proving denied paths make zero calls to credential resolution, Octokit client factories, GitHub API seams, repository lookup, branch lookup, readiness probes, audit writers, metrics, logs, diagnostics, and capability caches.
- [ ] Map GitHub readiness and capability evidence. (AC: 5, 8, 9)
  - [ ] Convert GitHub App/fine-grained permission evidence into the Story 3.2 capability profile without storing raw permission payloads.
  - [ ] Include safe metadata for GitHub API version, provider family/key, provider binding reference, capability profile schema/version, supported operations, unsupported or partial operations, rate-limit posture, retryability hints, and sanitized reason codes.
  - [ ] Preserve GitHub-specific capability differences as metadata consumed by downstream readiness/repository workflows instead of adding product workflow branches that assume GitHub semantics.
  - [ ] Validate required permissions for repository creation, contents read/write, branch/ref inspection, commit/status operations, and metadata access through hermetic tests and documented compatibility evidence.
- [ ] Implement canonical result and failure mapping. (AC: 6, 7)
  - [ ] Map Octokit exceptions, HTTP status categories, timeout/cancellation behavior, validation failures, permission failures, missing resources, repository conflicts, branch protection conflicts, rate limits, abuse/secondary-rate-limit responses, 5xx failures, and malformed responses to canonical provider categories.
  - [ ] Separate known provider failures from unknown outcomes; unknown or ambiguous mutating results must route to reconciliation metadata instead of unsafe retry.
  - [ ] Carry only safe remediation category, retry-after metadata where safe, correlation ID, operation ID, provider binding reference, and sanitized reason code.
  - [ ] Do not include raw GitHub response bodies, raw headers that may contain sensitive data, repository clone URLs, owner/repository names before authorization, branch secrets, installation secrets, or exception stack traces in provider results.
- [ ] Add GitHub operation coverage behind the provider port. (AC: 1, 5, 6, 7, 8)
  - [ ] Implement the GitHub operations currently required by `IGitProvider` for readiness, repository create/bind evidence, branch/ref inspection, file mutation support, commit support, and status query.
  - [ ] Keep operation implementations idempotency-aware where the port requires idempotency evidence, but leave cross-workflow orchestration, worker process managers, and reconciliation scheduling to later stories unless the current port explicitly owns a narrow return model.
  - [ ] Do not implement Forgejo behavior, local Git working-copy behavior, webhooks, background drift polling, CLI/MCP commands, UI pages, repair workflows, or generated SDK edits in this story.
- [ ] Add offline tests and dependency guards. (AC: 2, 3, 6, 7, 10, 11, 12)
  - [ ] Add tests under `tests/Hexalith.Folders.Tests/Providers/GitHub/` or the established provider test location using fake Octokit/GitHub seams.
  - [ ] Cover success, equivalent existing repository, validation failure, authentication failure, permission failure, missing owner/repository, repository conflict, branch protection conflict, rate limit, secondary rate limit, timeout, 5xx, malformed response, unknown outcome, and reconciliation-required cases.
  - [ ] Add leakage tests using sentinel token, JWT, PEM, credential URL, embedded-credential repository URL, raw GitHub payload, branch name with secret, file content, diff, email, display name, installation ID, owner, repository, and unauthorized resource values.
  - [ ] Add dependency/architecture guard tests proving only the concrete GitHub provider area references Octokit and GitHub-specific types.
  - [ ] Add hermetic fixture tests for GitHub API version and permission mapping assumptions; live GitHub drift tests are deferred to nightly/provider-contract infrastructure.

## Dev Notes

### Source Context

- Epic 3 requires provider configuration, readiness validation, repository-backed folder creation/binding, branch/ref policy, and provider support evidence without exposing secrets. Story 3.3 is the concrete GitHub adapter slice behind that provider port. [Source: `_bmad-output/planning-artifacts/epics.md#Story 3.3`]
- PRD FR15-FR23 require provider bindings, readiness diagnostics, repository binding, branch/ref policy, provider/credential/repository/capability metadata, and explicit GitHub/Forgejo capability differences without secrets. [Source: `_bmad-output/planning-artifacts/prd.md#Provider Readiness and Repository Binding`]
- Architecture requires `Hexalith.Folders.Providers.GitHub` to use Octokit 14.0.0 with GitHub Apps fine-grained permissions and to stay behind `IGitProvider`. [Source: `_bmad-output/planning-artifacts/epics.md#AR-PROVIDER-02`; `_bmad-output/planning-artifacts/architecture.md#External integrations`]
- Architecture requires known provider failures to be distinguished from unknown outcomes; unknown outcomes enter reconciliation instead of silent retry. [Source: `_bmad-output/planning-artifacts/epics.md#AR-PROVIDER-05`]
- Official NuGet metadata checked during story creation shows `Octokit` 14.0.0 as the current package version and compatible with computed `net10.0` targets. [Source: `https://www.nuget.org/packages/Octokit/14.0.0`]
- Current GitHub REST documentation states that the REST API is versioned and GitHub App endpoints require specific token contexts; GitHub App permission docs describe endpoint permissions and the `X-Accepted-GitHub-Permissions` header for permission discovery. [Source: `https://docs.github.com/en/rest/apps/apps`; `https://docs.github.com/en/rest/authentication/permissions-required-for-github-apps?apiVersion=latest`]

### Previous Story Intelligence

- Story 3.1 defines organization-scoped provider bindings, opaque credential references, authorization-before-observation, secret-shaped input rejection, idempotency/conflict behavior, and metadata-only binding projections. The GitHub adapter consumes those bindings but must not bypass their authorization order.
- Story 3.2 defines the N-provider `IGitProvider` port, capability profiles, canonical operation identifiers, provider failure categories, deterministic metadata fingerprints, and fake-provider conformance expectations. The GitHub adapter must implement that port rather than reshaping product semantics around Octokit.
- Story 2.6 is active in this working tree and Story 2.7 is `in-progress`; do not overwrite active authorization/status implementation artifacts or source changes while developing this story.
- Story 2.9 established worker-side event handling and one-authoritative-writer guardrails. The GitHub adapter should not create worker process-manager side effects, reconciliation loops, or event handlers unless a later story owns them.
- Story 1.7 authored provider/repository binding contract groups and Story 1.13 generated the parity oracle. Any public contract or parity fixture change must go through the established Contract Spine path.

### Existing Implementation State

- `Directory.Packages.props` currently uses central package management and does not yet list `Octokit`.
- `src/Hexalith.Folders/Hexalith.Folders.csproj` currently references Contracts and Microsoft options/logging abstractions, not Octokit.
- Current source inventory does not show a concrete `src/Hexalith.Folders/Providers/GitHub/` area yet.
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/TenantFolderProviderContractGroupTests.cs` and `tests/fixtures/parity-contract.yaml` guard provider binding/readiness/support evidence and parity behavior.
- Generated preflight for this run reported an active-dev-story soft warning only for Story 2.7 and `sprint-status.yaml`; those active development changes are outside this story creation operation.

### Required Architecture Patterns

- Use .NET 10, file-scoped namespaces, nullable-aware C#, one public type per file, PascalCase public members, camelCase locals/parameters, and async APIs with `CancellationToken` for provider I/O.
- Keep provider abstractions in core/domain code and concrete GitHub behavior in the GitHub provider area. Do not leak Octokit types into abstractions or public contracts.
- Keep aggregates pure. `OrganizationAggregate` and `FolderAggregate` must not call GitHub, Octokit, Dapr, secret stores, filesystem working copies, EventStore service clients, clocks, random, or workers directly.
- Use metadata-only structured logs and provider evidence. Never log raw provider responses, credential material, clone URLs with credentials, file contents, diffs, branch secrets, installation secrets, emails, display names, or unauthorized existence details.
- Keep GitHub capability support as provider metadata. Product workflows should branch on canonical capability/result categories, not raw GitHub endpoint details or Octokit exception types.
- Pin GitHub API version and permission assumptions explicitly because provider compatibility drift must be visible.

### Files To Touch

- `Directory.Packages.props`
- `src/Hexalith.Folders/Hexalith.Folders.csproj` or a dedicated provider project if Story 3.2 introduced one
- `src/Hexalith.Folders/Providers/GitHub/GitHubProvider.cs`
- `src/Hexalith.Folders/Providers/GitHub/*GitHub*.cs`
- `src/Hexalith.Folders/Providers/GitHub/*Octokit*.cs`
- `src/Hexalith.Folders/Providers/GitHub/*Failure*.cs`
- `src/Hexalith.Folders/Providers/GitHub/*Readiness*.cs`
- `src/Hexalith.Folders/Providers/GitHub/*Credential*.cs` only for non-secret credential-use seams
- `tests/Hexalith.Folders.Tests/Providers/GitHub/*`
- `tests/Hexalith.Folders.Tests/Providers/Abstractions/*` only if conformance tests need a GitHub fixture
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/TenantFolderProviderContractGroupTests.cs` only if public contract assertions need tightening
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` and `tests/fixtures/parity-contract.yaml` only if an intentional public Contract Spine change is required

### Do Not Touch

- Do not implement Forgejo adapter behavior, Forgejo schema snapshots, live nightly drift jobs, local Git working copies, workspace process managers, repository reconciliation queues, webhooks, CLI/MCP commands, UI pages, repair workflows, or generated SDK clients.
- Do not resolve credentials before authorization or store credential material after a call boundary.
- Do not expose raw GitHub response bodies, raw Octokit exceptions, installation secrets, owner/repository names before authorization, clone URLs, branch secrets, file contents, diffs, emails, display names, or unauthorized resource existence.
- Do not change public Contract Spine semantics unless implementation finds a named gap and updates OpenAPI, parity fixtures, generated artifacts, and contract tests through the established process.
- Do not initialize nested submodules or use recursive submodule commands.

### Testing

- Use xUnit v3 and Shouldly. Use NSubstitute only where a real seam needs substitution.
- GitHub adapter tests must run offline with fake Octokit/GitHub seams or hermetic fixtures. They must not require live GitHub, Forgejo, provider credentials, tenant seed data, Aspire, Dapr sidecars, Redis, Keycloak, or nested submodule initialization.
- Add authorization/order tests proving denied paths make zero calls to credential resolution, Octokit client construction, GitHub APIs, repository/branch/status lookup, readiness probes, cache/projection/audit stores, metrics, logs, and diagnostics.
- Add canonical mapping tests for 400/401/403/404/409/422/429/5xx, timeout, cancellation, secondary rate limit, branch protection conflict, missing repository, deleted repository, malformed response, and unknown outcome cases.
- Add leakage tests that serialize provider requests/results/profiles and capture logs/exceptions where applicable, then assert forbidden sentinel values are absent.
- Add dependency guard tests so Octokit is referenced only by the GitHub provider implementation/tests and never by abstractions, Contracts, Client, CLI, MCP, UI, Server transports, aggregates, or Workers.
- Add API-version and permission-mapping fixture tests so future GitHub API drift is visible without requiring live network calls.

### Regression Traps

- Do not shape `IGitProvider` around Octokit or make Forgejo adapt to GitHub semantics later.
- Do not treat GitHub App, fine-grained PAT, and installation-token permission models as interchangeable without explicit capability metadata.
- Do not use raw owner/repository/branch labels as identity before authorization.
- Do not retry mutating GitHub calls after timeouts or ambiguous responses unless the provider port supplies safe idempotency/reconciliation proof.
- Do not map every Octokit `NotFound` to product `not_found`; before authorization it may need a safe denial/read-model unavailable family to avoid existence leakage.
- Do not classify rate limits, secondary abuse limits, credential revocation, branch protection conflicts, and repository conflicts as one generic provider failure.
- Do not cache capability or readiness evidence globally by provider family; include tenant/organization, provider binding, safe target metadata, API version, profile schema/version, and authorization-evidence freshness dimensions.
- Do not include raw GitHub payloads "for debugging" in logs, traces, diagnostics, exceptions, profile metadata, audit records, or test snapshots.
- Do not manually edit generated SDK files or parity rows.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 3.3`
- `_bmad-output/planning-artifacts/epics.md#AR-PROVIDER-02`
- `_bmad-output/planning-artifacts/epics.md#AR-PROVIDER-05`
- `_bmad-output/planning-artifacts/prd.md#Provider Readiness and Repository Binding`
- `_bmad-output/planning-artifacts/architecture.md#Provider boundaries`
- `_bmad-output/planning-artifacts/architecture.md#Recommended Project Layout`
- `_bmad-output/planning-artifacts/architecture.md#External integrations`
- `_bmad-output/project-context.md#Critical Don't-Miss Rules`
- `_bmad-output/implementation-artifacts/3-1-configure-provider-binding-and-credential-reference.md`
- `_bmad-output/implementation-artifacts/3-2-define-igitprovider-port-and-capability-model.md`
- `Directory.Packages.props`
- `src/Hexalith.Folders/Hexalith.Folders.csproj`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/TenantFolderProviderContractGroupTests.cs`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`
- `tests/fixtures/parity-contract.yaml`
- `https://www.nuget.org/packages/Octokit/14.0.0`
- `https://docs.github.com/en/rest/apps/apps`
- `https://docs.github.com/en/rest/authentication/permissions-required-for-github-apps?apiVersion=latest`

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-19 | Created story with GitHub Octokit adapter boundary, authorization-before-observation, credential redaction, canonical failure mapping, unknown-outcome reconciliation, API-version/permission compatibility evidence, and offline tests. | Codex |

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

### Completion Notes List

- Story created by `/bmad-create-story 3-3-implement-github-provider-adapter` equivalent workflow on 2026-05-19.
- Project context, Epic 3, PRD provider-readiness requirements, architecture provider boundaries, Stories 3.1 and 3.2, current package/source inventory, recent commits, and story-creation lessons were reviewed.
- Preflight working-tree failure was classified as an active-dev-story soft warning because Story 2.7 is `in-progress` in both its artifact and sprint status; active development changes were left untouched.
- Official NuGet and GitHub REST/GitHub Apps documentation were checked for Octokit version, API versioning, and GitHub App permission evidence.
- Ultimate context engine analysis completed - comprehensive developer guide created.

### File List
