# Story 3.4: Implement Forgejo provider adapter and drift detection

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a platform engineer,
I want a Forgejo provider adapter with version snapshots and schema drift detection,
so that Forgejo support is verified against pinned API behavior.

## Terms

- Forgejo provider adapter means the concrete `Hexalith.Folders.Providers.Forgejo` implementation behind the internal `IGitProvider` port from Story 3.2.
- Typed Forgejo HTTP wrapper means hand-written adapter-internal request/response code using `HttpClient`, source-generated or explicit JSON models, and per-version OpenAPI snapshot evidence. It must not become a product-facing provider abstraction.
- Forgejo version snapshot means a pinned `swagger.v1.json` file under `tests/contracts/forgejo/<version>/swagger.v1.json` plus manifest metadata in `tests/contracts/forgejo/supported-versions.json`.
- Schema drift detection means a hermetic snapshot comparison and nightly upstream check that classifies additive changes as warnings and breaking changes as failures according to the architecture policy.
- Forgejo binding means tenant/organization-scoped provider binding metadata and an opaque credential reference configured by Story 3.1.
- Forgejo credential reference means an opaque non-secret reference. This story may resolve credential material only after authorization succeeds and only through the approved credential seam; raw tokens must never be stored, logged, projected, returned, compared, or snapshotted.
- Canonical provider result means the internal Folders provider result/failure taxonomy from Story 3.2, not raw Forgejo response bodies, endpoint paths, status text, or transport exception messages.
- Unknown provider outcome means Forgejo accepted, may have accepted, or timed out during a mutating operation where safe retry could duplicate repositories, branch/ref mutations, file changes, commits, audit evidence, or readiness state.

## Acceptance Criteria

1. Given Story 3.1 provider binding metadata and Story 3.2 `IGitProvider` abstractions exist, when the Forgejo adapter is added, then it lives under a concrete Forgejo provider area such as `src/Hexalith.Folders/Providers/Forgejo/`, implements only provider-port members needed for readiness, repository creation/binding, branch/ref inspection, file operation support, commit support, status query, and failure behavior, uses an adapter-internal typed HTTP seam for offline tests, maps only into the existing Story 3.2 provider result/capability models, and does not change public REST, SDK, CLI, MCP, UI, EventStore command, or Contract Spine semantics unless a named gap is documented and regenerated.
2. Given package references or HTTP infrastructure are added, when the solution restores and dependency guards run, then Forgejo behavior uses the architecture-approved typed `HttpClient` wrapper and central package management; `Hexalith.Folders.Providers.Forgejo` and Forgejo-specific tests are the only places that may know Forgejo endpoint paths, OpenAPI snapshot shapes, scoped-token headers, manifest readers, snapshot models, readiness mappers, failure mappers, or Forgejo-specific response DTOs; provider abstractions, Contracts, Client, CLI, MCP, UI, Server transports, aggregates, Workers, and public provider-port models do not gain Forgejo client, OpenAPI snapshot, or endpoint dependencies.
3. Given an actor is unauthorized, tenant evidence is disabled/stale/unavailable/malformed/mismatched/replay-conflicting, or provider binding metadata cannot be observed safely, when a Forgejo provider operation is attempted through an application-facing workflow, then authorization and tenant/organization/provider-binding scoping complete before resolving `IGitProvider`, provider options, adapter construction, credential lookup, `HttpClient` creation, base URL resolution, repository lookup, branch/ref lookup, capability cache lookup, snapshot selection, readiness probing, audit writing, metrics/log enrichment, or diagnostics; denied and missing paths return existing safe denial/read-model unavailable families without confirming whether the Forgejo binding, credential reference, instance URL, owner, repository, branch, or capability evidence exists.
4. Given a Forgejo credential reference is authorized for use, when the adapter creates a request context, then token material remains in memory only for the call boundary, is passed by approved authorization header semantics, is never sent through URL query parameters, and is never stored in aggregate state, events, projections, profiles, exceptions, logs, traces, metrics, diagnostics, test snapshots, command arguments, OpenAPI snapshot metadata, snapshot file names, or failure metadata.
5. Given supported Forgejo versions are listed, when the supported-version manifest is evaluated, then it includes at least the latest stable release, latest LTS release, n-1 minor release, and any pinned customer instance versions; each entry includes a dated source, support class, snapshot path, expected API compatibility posture, reviewer/owner metadata, and manifest hash or equivalent reviewable integrity evidence; as of story creation on 2026-05-20, official Forgejo release pages identify `15.0.2` as both latest and LTS, `11.0.14` as an older still-supported LTS line, and `14.0.5` as the n-1 discontinued minor reference, but implementation must make this manifest explicit and reviewable rather than depending on ambient "latest" behavior.
6. Given Forgejo readiness is checked, when the adapter validates capability evidence, then it maps Forgejo version, instance API settings, token scope evidence, repository visibility constraints, branch/ref support, contents/file limits, commit/status support, rate-limit posture, snapshot compatibility, and response categories into the internal capability/readiness model from Story 3.2 without exposing raw provider payloads, endpoint URLs containing secrets, token strings, or unauthorized resource existence.
7. Given Forgejo repository creation or binding is requested by a later workflow, when Forgejo returns success, existing-equivalent state, validation failure, name conflict, missing permission, missing owner, deleted/missing repository, branch protection conflict, redirect, rate limit, timeout, cancellation, 5xx, malformed response, unsupported capability, version-incompatible response, or unexpected transport failure, then the adapter returns stable canonical provider result categories equivalent to GitHub where product semantics match, Forgejo-specific capability metadata where they differ, and a named Story 3.2 gap when the existing provider model cannot carry required safe evidence.
8. Given an operation may have partially applied on Forgejo, when the adapter cannot prove the outcome, then it returns `unknown_provider_outcome` with `reconciliation_required` metadata in the existing provider result/failure shape and includes only safe provider family, operation identifier, provider binding reference, target repo/branch/ref category when already authorized and safe, correlation/request ID when safe, status category, snapshot/version evidence, retryability posture, and sanitized reconciliation reason; it must not silently retry, issue a second mutating call, create duplicate repositories, change branch refs twice, duplicate file mutations, emit success telemetry, or fabricate success.
9. Given branch/ref, file, commit, and status operations are implemented through the provider port, when Forgejo capabilities differ from GitHub, then those differences are represented as capability metadata, snapshot evidence, and canonical result categories rather than Forgejo-specific branches in product workflows, public DTOs, CLI/MCP behavior, or UI semantics.
10. Given Forgejo API behavior assumptions are used, when the adapter sets base paths, headers, authentication style, pagination, request models, or endpoint mappings, then the chosen Forgejo version snapshots and required token scopes are pinned in source constants or manifest data, covered by tests, and recorded as compatibility evidence; implementation must not depend on a live instance's current `/swagger.v1.json` for local builds or PR-gate tests.
11. Given Forgejo OpenAPI snapshots are added or updated, when contract tests run, then every supported version snapshot parses, the manifest points at existing files, required endpoint/operation coverage exists for readiness, repository, branch/ref, file, commit, and status operations, and local Forgejo-only schema drift classification distinguishes supported, additive-compatible, breaking-incompatible, and unknown/unclassified changes without requiring live Forgejo during PR-gate tests or leaking raw Swagger fragments outside Forgejo provider tests/tooling.
12. Given nightly live drift checks run in CI infrastructure, when upstream Forgejo snapshots or instance `swagger.v1.json` documents differ from the pinned manifest, then the job reports provider version, fixture set, drift classification, redaction scan result, and sanitized artifact retention status; additive changes are warnings, breaking changes are failures, unsupported or failing versions are not marked ready, and local builds and story completion do not require live Forgejo, provider credentials, or network access.
13. Given the adapter emits telemetry, diagnostics, audit evidence, readiness evidence, projection/cache evidence, exceptions, provider results, snapshot reports, or test output, when sentinel values for provider tokens, credentials, repository URLs with userinfo, raw Forgejo payloads, branch names with secrets, file contents, diffs, owner names, repository names, emails, display names, base URLs, and unauthorized resource names are present in inputs or mocked responses, then all observable outputs remain metadata-only and fail tests if forbidden values appear.
14. Given this story is complete, when unit and contract tests run in a developer machine or CI PR gate, then Forgejo adapter tests pass offline with fake Forgejo seams and pinned OpenAPI fixtures and no live Forgejo, GitHub, provider credentials, tenant seed data, Aspire, Dapr sidecars, Redis, Keycloak, or nested submodule initialization; offline test groups cover denied-path no-touch behavior, canonical failure mapping, redaction, dependency guards, snapshot manifest validation, schema-drift classification, pagination/limits, and permission/scope mapping.

## Tasks / Subtasks

- [ ] Add the concrete Forgejo provider package boundary. (AC: 1, 2, 9)
  - [ ] Create `src/Hexalith.Folders/Providers/Forgejo/` with `ForgejoProvider` and one-public-type-per-file support models needed by the existing `IGitProvider` port.
  - [ ] Add an adapter-internal typed HTTP seam wrapping Forgejo API calls so offline tests can exercise Forgejo responses without leaking endpoint DTOs into `IGitProvider` or public provider models; keep the seam internal to the Forgejo provider assembly and expose it to tests only through approved InternalsVisibleTo or provider-level test helpers.
  - [ ] Use central package management for any added HTTP, resilience, JSON, or OpenAPI tooling packages; do not place package versions directly in project files.
  - [ ] Add a dependency guard test or build check proving Forgejo endpoint DTOs, snapshot models, manifest readers, readiness mappers, failure mappers, and typed HTTP seams are limited to the concrete Forgejo provider implementation and Forgejo-specific tests.
  - [ ] Add dependency guard tests or architecture checks proving the Forgejo provider area does not reference Forgejo/GitHub SDKs, CLI wrappers, MCP/UI/EventStore/Contract Spine packages, Aspire hosting, Dapr, Keycloak, Redis, or credential-store implementation projects except through approved abstractions already allowed by Story 3.2.
  - [ ] Keep `src/Hexalith.Folders/Providers/Abstractions/` free of Forgejo DTOs, endpoint names, OpenAPI snapshot shapes, Forgejo-specific exception types, and Forgejo-specific package references.
  - [ ] If Story 3.2 abstractions or Story 3.3 GitHub oracle behavior are not yet implemented when development begins, implement only against the approved Story 3.2 port contract and record any missing equivalence oracle gap instead of inventing a competing port shape.
- [ ] Implement authorized Forgejo request construction and credential handling. (AC: 3, 4, 10, 13)
  - [ ] Resolve credential references only through the approved authorized credential seam after tenant, organization, ACL, provider-binding, and target-instance authorization succeeds.
  - [ ] Build typed HTTP clients from authorized binding/base-URL metadata and short-lived in-memory credential material; do not accept base URLs, owner names, repository names, or token values from untrusted request payloads as authority.
  - [ ] Use approved `Authorization` header semantics for tokens; do not send `token` or `access_token` query parameters even though Forgejo supports them.
  - [ ] Treat instance URL, owner, repository, branch/ref, token labels, and credential labels as sensitive before authorization and safe metadata only when explicitly allowed by the internal provider evidence model.
  - [ ] Add tests proving denied paths make zero calls to credential resolution, HTTP client factories, Forgejo API seams, repository lookup, branch lookup, snapshot selection, readiness probes, audit writers, metrics, logs, diagnostics, and capability caches.
  - [ ] Add request/log tests proving no `token` or `access_token` query parameters are produced and authorization values do not appear in logs, metrics, diagnostics, snapshot names, drift artifacts, or failure metadata.
- [ ] Add supported-version snapshots and manifest validation. (AC: 5, 10, 11, 12)
  - [ ] Create `tests/contracts/forgejo/supported-versions.json` with explicit version entries, support class, source URL, snapshot path, expected API compatibility posture, and owner/reviewer metadata.
  - [ ] Add pinned `tests/contracts/forgejo/<version>/swagger.v1.json` snapshots for latest stable, latest LTS, n-1 minor, and pinned customer versions.
  - [ ] Add manifest tests proving every listed snapshot exists, parses as OpenAPI/Swagger JSON, names the expected Forgejo version family, and contains the endpoint groups needed by the current provider port.
  - [ ] Add coverage matrix tests mapping each required provider operation to one or more snapshot paths and fake responses.
  - [ ] Ensure snapshot updates require reviewer-visible diffs and never contain token values, private instance URLs, customer repository names, credentials, or live response payloads beyond the public schema.
  - [ ] Keep at least one checked-in manifest entry and matching pinned `swagger.v1.json` fixture in the first implementation slice before marking the adapter behavior complete.
- [ ] Implement Forgejo readiness and capability evidence mapping. (AC: 6, 9, 10)
  - [ ] Convert Forgejo version, token scope evidence, API settings, response limits, pagination, branch/ref behavior, file operation limits, commit/status support, and repository visibility constraints into the Story 3.2 capability profile.
  - [ ] Include safe metadata for Forgejo provider family/key, target product/version, snapshot version, API surface version, provider binding reference, capability profile schema/version, supported operations, unsupported or partial operations, rate-limit posture, retryability hints, and sanitized reason codes.
  - [ ] Preserve Forgejo-specific capability differences as metadata consumed by downstream readiness/repository workflows instead of adding product workflow branches that assume GitHub semantics.
  - [ ] Validate required token scopes and endpoint assumptions with hermetic fake responses and snapshot fixtures.
- [ ] Implement canonical result and failure mapping. (AC: 7, 8)
  - [ ] Implement the Forgejo failure mapping matrix in this story and keep each mapped case covered by an offline fixture or fake seam response.
  - [ ] Map HTTP status categories, Forgejo validation errors, authentication failures, permission failures, missing resources, repository conflicts, branch protection conflicts, rate limits, timeouts, cancellations, 5xx failures, malformed responses, unsupported operations, version-incompatible payloads, and schema drift to canonical provider categories.
  - [ ] Add precedence rules and tests for timeout after request body send, 404 repository versus 404 branch/path, 403 missing scope versus forbidden repository, 409 existing-equivalent versus real conflict, 422 validation versus schema incompatibility, 301/302/307 redirects, 204 or empty bodies, invalid JSON, wrong content type, and HTML proxy error pages.
  - [ ] Separate known provider failures from unknown outcomes; unknown or ambiguous mutating results must route to reconciliation metadata instead of unsafe retry.
  - [ ] Retry only known idempotent read operations and known transient categories when the port provides retry permission; never automatically retry authorization failures, validation failures, not-found/hidden-resource responses, unsupported capability, version drift, or unknown mutating outcomes.
  - [ ] Carry only safe remediation category, retry-after metadata where safe, correlation ID, operation ID, provider binding reference, snapshot/version evidence, and sanitized reason code.
- [ ] Add drift detection tooling and CI hooks. (AC: 11, 12)
  - [ ] Add local test/tool configuration for schema comparison under `tests/contracts/forgejo/` or `tests/tools/forgejo-drift/` following the architecture-approved oasdiff classifier policy.
  - [ ] Add a hermetic PR-gate test that compares checked-in snapshots against expected operation coverage and classification fixtures without network access.
  - [ ] Classify additive field, removed field, type change, enum or new string value, nullability change, error shape change, pagination shape change, and auth/rate-limit header shape change with expected severity.
  - [ ] Add or update `.github/workflows/nightly-drift.yml` or the existing provider-drift workflow only if it can be done without disrupting unrelated CI; otherwise record the exact workflow gap as deferred implementation work in this story's dev record.
  - [ ] Ensure drift reports redact private instance URLs, tokens, owner/repository names, branch names, file paths, and raw response samples.
- [ ] Add Forgejo operation coverage behind the provider port. (AC: 1, 6, 7, 8, 9)
  - [ ] Implement the Forgejo operations currently required by `IGitProvider` for readiness, repository create/bind evidence, branch/ref inspection, file mutation support, commit support, and status query.
  - [ ] Keep operation implementations idempotency-aware where the port requires idempotency evidence, but leave cross-workflow orchestration, worker process managers, and reconciliation scheduling to later stories unless the current port explicitly owns a narrow return model.
  - [ ] Do not implement GitHub behavior, local Git working-copy behavior, webhooks, broad repair workflows, CLI/MCP commands, UI pages, or generated SDK edits in this story.
- [ ] Add offline tests, snapshots, and dependency guards. (AC: 2, 3, 7, 8, 11, 13, 14)
  - [ ] Add tests under `tests/Hexalith.Folders.Tests/Providers/Forgejo/` or the established provider test location using fake Forgejo seams and pinned snapshots.
  - [ ] Split offline tests into deterministic groups for denied-path no-touch behavior, canonical failure mapping, redaction, dependency guards, snapshot manifest validation, schema drift classification, pagination/limit mapping, and token-scope mapping.
  - [ ] Add a fixture inventory covering repository list pagination, repository metadata, branch list, commit lookup, tree/list contents, file content, create/update/delete mutation success, unauthorized-hidden versus authorized-missing resources, rate-limit response, validation error, malformed payload, and schema-incompatible payload.
  - [ ] Add pagination edge fixtures for partial pagination, duplicate items across pages, missing or invalid next links, and provider caps lower than requested limits.
  - [ ] Cover success, equivalent existing repository, validation failure, authentication failure, permission failure, missing owner/repository, repository conflict, branch protection conflict, rate limit, timeout, cancellation, 5xx, malformed response, unsupported capability, version-incompatible response, unknown outcome, and reconciliation-required cases.
  - [ ] Add leakage tests using sentinel token, JWT, PEM, credential URL, embedded-credential repository URL, raw Forgejo payload, query string, authorization header, request body, response body, exception message, structured log scope, activity tag, base URL, branch name with secret, file content, diff, email, display name, owner, repository, and unauthorized resource values.
  - [ ] Add dependency/architecture guard tests proving only the concrete Forgejo provider area references Forgejo endpoint DTOs, OpenAPI snapshot models, and Forgejo-specific typed HTTP seams, and proving offline PR-gate tests do not require live Forgejo/GitHub credentials, Aspire, Dapr, Redis, Keycloak, Docker, network access, or nested submodules.

### Forgejo Failure Mapping Matrix

| Forgejo / HTTP condition | Canonical provider result | Retry posture | Required safe metadata |
|---|---|---|---|
| Success or existing-equivalent state | success or equivalent success | no retry | provider family, operation ID, provider binding reference, snapshot/version evidence |
| 400 / validation failure / malformed request | validation failure | do not retry automatically | sanitized reason code, operation ID, snapshot version |
| 401 / revoked or invalid credential | provider authentication required | do not retry automatically | provider binding reference and credential-reference category only |
| 403 missing scope or permission | provider permission insufficient | do not retry automatically | permission/scope family, snapshot evidence, sanitized reason code |
| 404 missing, private, or hidden resource | safe not-found or permission-insufficient category according to authorized context | do not infer existence; do not retry automatically | sanitized reason code with no owner/repository names |
| 409 repository, branch, or state conflict | provider conflict | do not retry automatically | conflict category and operation ID |
| 422 validation/name conflict/rule violation | validation failure or provider conflict | do not retry automatically | sanitized validation/conflict category |
| 301 / 302 / 307 redirect from API endpoint | provider configuration invalid or provider readiness failed according to authorized context | do not forward credentials across untrusted redirects | sanitized redirect category and snapshot/base-URL evidence only |
| 429 or provider-specific rate limit | provider rate limited | retry only for known idempotent reads when safe retry-after metadata exists | safe retry-after category and rate-limit posture |
| 5xx / transient Forgejo outage | provider unavailable | retry only for known idempotent reads and bounded transient policy | provider family, operation ID, status category |
| Timeout, cancellation, dropped connection, or ambiguous transport failure during mutation | unknown provider outcome with reconciliation required | do not retry automatically | provider family, operation ID, correlation/request ID when safe, reconciliation reason |
| Unsupported endpoint/capability for target version | unsupported provider capability or provider readiness failed | do not retry automatically | target version, snapshot version, capability key |
| Snapshot/schema-incompatible payload | provider readiness failed, known provider failure, or reconciliation required according to severity | do not retry automatically | snapshot version, drift classification, operation ID |
| Unexpected transport exception or unmapped response shape | unknown provider outcome with reconciliation required | do not retry automatically | sanitized exception category only, no raw message/body |

## Dev Notes

### Source Context

- Epic 3 requires provider configuration, readiness validation, repository-backed folder creation/binding, branch/ref policy, and provider support evidence without exposing secrets. Story 3.4 is the concrete Forgejo adapter and drift-detection slice behind that provider port. [Source: `_bmad-output/planning-artifacts/epics.md#Story 3.4`]
- PRD FR15-FR23 require provider bindings, readiness diagnostics, repository binding, branch/ref policy, provider/credential/repository/capability metadata, and explicit GitHub/Forgejo capability differences without secrets. [Source: `_bmad-output/planning-artifacts/prd.md#Provider Readiness and Repository Binding`]
- Architecture requires Forgejo to use a typed `HttpClient` wrapper fed by per-version `swagger.v1.json` snapshots and a `supported-versions.json` manifest; generic Gitea clients, generated clients, and unstructured contract tests are rejected because they hide version skew and drift risk. [Source: `_bmad-output/planning-artifacts/architecture.md#API & Communication Patterns`]
- Architecture requires known provider failures to be distinguished from unknown outcomes; unknown outcomes enter reconciliation instead of silent retry. [Source: `_bmad-output/planning-artifacts/epics.md#AR-PROVIDER-05`]
- Official Forgejo release pages checked during story creation show `v15.0.2` as the latest LTS release and `v11.0.14` as an older still-supported LTS line on 2026-05-20; official API docs state the API is major-version compatible, Swagger is enabled by default, and each instance exposes API docs at `/api/swagger` plus OpenAPI at `/swagger.v1.json`. [Source: `https://forgejo.org/releases/`; `https://forgejo.org/docs/v15.0/user/api-usage/`]

### Previous Story Intelligence

- Story 3.1 defines organization-scoped provider bindings, opaque credential references, authorization-before-observation, secret-shaped input rejection, idempotency/conflict behavior, and metadata-only binding projections. The Forgejo adapter consumes those bindings but must not bypass their authorization order.
- Story 3.2 defines the N-provider `IGitProvider` port, capability profiles, canonical operation identifiers, provider failure categories, deterministic metadata fingerprints, scoped cache dimensions, malformed-profile rejection, and fake-provider conformance expectations. The Forgejo adapter must implement that port rather than reshaping product semantics around Forgejo endpoints.
- Story 3.3 defines GitHub adapter boundaries and an equivalence oracle for canonical provider semantics where GitHub and Forgejo behavior should match. The Forgejo adapter must not become a GitHub base-URL swap and must express differences as capability metadata.
- Story 2.8 is active in this working tree and `sprint-status.yaml` has active development changes. Do not overwrite active archive-story artifact or status changes while developing this story.
- Story 1.13 generated the C13 parity oracle. Any public contract or parity fixture change must go through the established Contract Spine path.

### Existing Implementation State

- `src/Hexalith.Folders/Providers/Forgejo/` does not exist in the current source inventory.
- `tests/contracts/forgejo/` does not appear in the current test inventory and should be created by this story if still absent.
- `Directory.Packages.props` uses central package management; current package inventory already includes `Microsoft.Extensions.Http` and `Microsoft.Extensions.Http.Resilience`, but no Forgejo-specific package is present.
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/TenantFolderProviderContractGroupTests.cs` and `tests/fixtures/parity-contract.yaml` guard provider binding/readiness/support evidence and parity behavior.
- Recent repository commits include active Story 2.8 lifecycle-status work; inspect current source before editing provider-adjacent tests or shared fixtures.

### Required Architecture Patterns

- Use .NET 10, file-scoped namespaces, nullable-aware C#, one public type per file, PascalCase public members, camelCase locals/parameters, and async APIs with `CancellationToken` for provider I/O.
- Keep provider abstractions in core/domain code and concrete Forgejo behavior in the Forgejo provider area. Do not leak Forgejo endpoint DTOs, OpenAPI snapshot types, or HTTP seam details into abstractions or public contracts.
- Keep the adapter-internal Forgejo HTTP seam private to the concrete provider area. It exists to isolate HTTP behavior and enable fake responses; it must not become a second provider port or a product workflow dependency.
- Keep aggregates pure. `OrganizationAggregate` and `FolderAggregate` must not call Forgejo, `HttpClient`, Dapr, secret stores, filesystem working copies, EventStore service clients, clocks, random, or workers directly.
- Use metadata-only structured logs and provider evidence. Never log raw provider responses, credential material, repository URLs with credentials, file contents, diffs, branch secrets, token values, owner/repository names before authorization, emails, display names, or unauthorized existence details.
- Keep Forgejo capability support as provider metadata. Product workflows should branch on canonical capability/result categories, not raw Forgejo endpoint details or HTTP status text.
- Pin Forgejo version snapshots and token-scope assumptions explicitly because provider compatibility drift must be visible.
- Treat Forgejo schema drift classification as local provider evidence with `supported`, `additive-compatible`, `breaking-incompatible`, and `unknown-unclassified` outcomes. Do not promote raw Swagger fragments or provider DTOs into projections, events, public contracts, or logs.
- Freeze the Story 3.2 contract surface during this story. If an implementation needs a new provider capability/result field, record a named gap instead of expanding public REST, SDK, CLI, MCP, UI, EventStore, Contract Spine, or provider-port models in this story.

### Files To Touch

- `src/Hexalith.Folders/Providers/Forgejo/ForgejoProvider.cs`
- `src/Hexalith.Folders/Providers/Forgejo/*Forgejo*.cs`
- `src/Hexalith.Folders/Providers/Forgejo/*Http*.cs`
- `src/Hexalith.Folders/Providers/Forgejo/*Failure*.cs`
- `src/Hexalith.Folders/Providers/Forgejo/*Readiness*.cs`
- `src/Hexalith.Folders/Providers/Forgejo/*Credential*.cs` only for non-secret credential-use seams
- `tests/Hexalith.Folders.Tests/Providers/Forgejo/*`
- `tests/contracts/forgejo/supported-versions.json`
- `tests/contracts/forgejo/<version>/swagger.v1.json`
- `tests/tools/forgejo-drift/*` or equivalent existing drift tooling location
- `.github/workflows/nightly-drift.yml` only if the workflow exists or can be added without disrupting unrelated CI
- `Directory.Packages.props` only if new centrally managed package versions are required
- `src/Hexalith.Folders/Hexalith.Folders.csproj` or a dedicated provider project if Story 3.2 introduced one
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/TenantFolderProviderContractGroupTests.cs` only if public contract assertions need tightening
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` and `tests/fixtures/parity-contract.yaml` only if an intentional public Contract Spine change is required

### Do Not Touch

- Do not implement GitHub adapter behavior, local Git working-copy behavior, broad repository reconciliation queues, webhooks, CLI/MCP commands, UI pages, repair workflows, generated SDK clients, or public Contract Spine changes unless an explicit gap is documented and regenerated.
- Do not resolve credentials before authorization or store credential material after a call boundary.
- Do not use `token` or `access_token` query parameters for Forgejo API calls.
- Do not expose raw Forgejo response bodies, raw HTTP exceptions, instance URLs before authorization, owner/repository names before authorization, clone URLs, branch secrets, file contents, diffs, emails, display names, or unauthorized resource existence.
- Do not depend on a live Forgejo instance, live `swagger.v1.json`, provider credentials, tenant seed data, Aspire, Dapr sidecars, Redis, Keycloak, or initialized nested submodules for local builds or PR-gate tests.
- Do not build a generic multi-provider drift framework in this story; keep Forgejo drift tooling under the Forgejo provider test/tooling area unless a later architecture story creates shared infrastructure.
- Do not add EventStore or audit schema changes for Forgejo evidence. If existing safe metadata shapes cannot carry the required result, document the gap instead of changing schemas here.
- Do not initialize nested submodules or use recursive submodule commands.

### Testing

- Use xUnit v3 and Shouldly. Use NSubstitute only where a real seam needs substitution.
- Forgejo adapter tests must run offline with fake typed HTTP seams and pinned snapshot fixtures. They must not require live Forgejo, GitHub, provider credentials, tenant seed data, Aspire, Dapr sidecars, Redis, Keycloak, or nested submodule initialization.
- Add authorization/order tests proving denied paths make zero calls to credential resolution, HTTP client construction, Forgejo APIs, repository/branch/status lookup, snapshot selection, readiness probes, cache/projection/audit stores, metrics, logs, and diagnostics.
- Treat denied-path no-touch tests as a security oracle: they must also prove no provider HTTP call occurs, no telemetry dimension is derived from target repository/path values, and no cached provider lookup is read before authorization.
- Add manifest and snapshot tests proving `supported-versions.json` points at existing snapshots, classifies latest stable/LTS/n-1/customer-pinned versions, parses schemas, and maps required provider operations to fixtures.
- Add schema drift tests with checked-in before/after fixtures covering additive warning, breaking failure, removed endpoint, removed field, changed required field, changed response shape, and unknown operation cases.
- Add canonical mapping tests for 400/401/403/404/409/422/429/5xx, timeout, cancellation, unsupported endpoint, version-incompatible payload, branch protection conflict, missing repository, deleted repository, malformed response, and unknown outcome cases. Split 404 behavior into unauthorized-hidden, authorized-missing-repository, authorized-missing-branch/path, and unknown when authorization context is unavailable.
- Add mutation timeout/cancellation tests that verify `unknown_provider_outcome`, no success telemetry, no success event/audit emission, and no automatic second mutating request unless the Story 3.2 port provides explicit safe idempotency proof.
- Add leakage tests that serialize provider requests/results/profiles/drift reports and capture logs/exceptions where applicable, then assert forbidden sentinel values are absent.
- Add dependency guard tests so Forgejo endpoint DTOs, OpenAPI snapshot models, and typed HTTP seam details are referenced only by the Forgejo provider implementation/tests and never by abstractions, Contracts, Client, CLI, MCP, UI, Server transports, aggregates, or Workers.

### Regression Traps

- Do not treat Forgejo as a GitHub base-URL variation or a Gitea-client compatibility assumption.
- Do not generate a client from one live Forgejo instance and assume it covers all supported versions.
- Do not let raw Forgejo endpoint names, response bodies, or transport exception text become product semantics.
- Do not select a snapshot from untrusted request payload values; supported versions come from manifest/configuration and authorized provider binding evidence.
- Do not retry mutating Forgejo calls after timeouts or ambiguous responses unless the provider port supplies safe idempotency/reconciliation proof.
- Do not automatically retry authorization failures, validation failures, not-found/hidden-resource responses, unsupported capabilities, drift failures, or unknown mutating outcomes.
- Do not map every 404 to product `not_found`; before authorization it may need a safe denial/read-model unavailable family to avoid existence leakage.
- Do not classify rate limits, credential revocation, branch protection conflicts, repository conflicts, schema drift, and unsupported capabilities as one generic provider failure.
- Do not cache capability or readiness evidence globally by provider family or instance URL; include tenant/organization, provider binding, safe target metadata, snapshot version, profile schema/version, and authorization-evidence freshness dimensions.
- Do not include raw Forgejo payloads "for debugging" in logs, traces, diagnostics, exceptions, profile metadata, audit records, drift reports, or test snapshots.
- Do not let snapshot tests become tautological by generating expected fixtures from adapter output. Pinned fixtures must be reviewed as provider facts and tied back to manifest entries.
- Do not compare whole Swagger JSON blobs blindly for drift decisions. Compare known contract surfaces, classify unknown additive data separately, and keep raw schema details inside Forgejo test/tooling outputs.
- Do not manually edit generated SDK files or parity rows.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 3.4`
- `_bmad-output/planning-artifacts/epics.md#AR-PROVIDER-03`
- `_bmad-output/planning-artifacts/epics.md#AR-PROVIDER-04`
- `_bmad-output/planning-artifacts/epics.md#AR-PROVIDER-05`
- `_bmad-output/planning-artifacts/epics.md#AR-PROVIDER-06`
- `_bmad-output/planning-artifacts/prd.md#Provider Readiness and Repository Binding`
- `_bmad-output/planning-artifacts/architecture.md#Provider boundaries`
- `_bmad-output/planning-artifacts/architecture.md#Recommended Project Layout`
- `_bmad-output/planning-artifacts/architecture.md#External integrations`
- `_bmad-output/project-context.md#Critical Don't-Miss Rules`
- `_bmad-output/implementation-artifacts/3-1-configure-provider-binding-and-credential-reference.md`
- `_bmad-output/implementation-artifacts/3-2-define-igitprovider-port-and-capability-model.md`
- `_bmad-output/implementation-artifacts/3-3-implement-github-provider-adapter.md`
- `Directory.Packages.props`
- `src/Hexalith.Folders/Hexalith.Folders.csproj`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/TenantFolderProviderContractGroupTests.cs`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`
- `tests/fixtures/parity-contract.yaml`
- `https://forgejo.org/releases/`
- `https://forgejo.org/docs/v15.0/user/api-usage/`

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-20 | Created story with Forgejo typed HTTP adapter boundary, version snapshots, schema drift detection, authorization-before-observation, canonical failure mapping, unknown-outcome reconciliation, and offline snapshot tests. | Codex |
| 2026-05-20 | Applied party-mode review hardening for provider boundary containment, authorization-before-resolution, manifest and drift semantics, failure precedence, fixture inventory, redaction coverage, and offline guard tests. | Codex |

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

### Completion Notes List

- Story created by `/bmad-create-story 3-4-implement-forgejo-provider-adapter-and-drift-detection` equivalent workflow on 2026-05-20.
- Project context, Epic 3, PRD provider-readiness requirements, architecture provider boundaries, Stories 3.1 through 3.3, current package/source/test inventory, recent commits, and story-creation lessons were reviewed.
- Preflight working-tree failure was classified as an active-dev-story soft warning because Story 2.8 is `in-progress` in both its artifact and sprint status; active development changes were left untouched.
- Official Forgejo release and API documentation were checked for current release/LTS references, major-version compatibility, authentication posture, Swagger/OpenAPI locations, and per-instance API settings.
- Ultimate context engine analysis completed - comprehensive developer guide created.
- Party-mode review completed on 2026-05-20T09:44:29+02:00 with Winston, Amelia, Murat, and John; coherent low-risk findings were applied inline and scope/architecture/product decisions were recorded as deferred.

## Party-Mode Review

- ISO date and time: 2026-05-20T09:44:29+02:00
- Selected story key: 3-4-implement-forgejo-provider-adapter-and-drift-detection
- Command/skill invocation used: `/bmad-party-mode 3-4-implement-forgejo-provider-adapter-and-drift-detection; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), John (Product Manager)
- Findings summary: The story is ready for development after tightening the provider-only Forgejo boundary, freezing Story 3.2 contract surface, making authorization-before-resolution testable, pinning manifest semantics, keeping drift classification local, defining canonical failure precedence, expanding offline fixture/redaction coverage, and preventing the story from becoming a broad provider platform expansion.
- Changes applied: Clarified Forgejo DTO/snapshot/manifest/mapper containment; made denied paths block provider/options/credential/client/cache/snapshot/readiness/audit/metrics resolution; required internal typed HTTP seams and explicit dependency guards; added manifest integrity evidence; tightened unknown-outcome reconciliation metadata; added redirect/empty-body/malformed-response/failure precedence cases; added fixture inventory, pagination, redaction, drift-classification, and no-touch security-oracle tests; recorded local-only drift and Story 3.2 contract-freeze guardrails.
- Findings deferred: Live Forgejo compatibility matrix and smoke tests; generic multi-provider drift framework; EventStore or audit schema changes; public REST, SDK, CLI, MCP, UI, or Contract Spine expansion; broad repository lifecycle workflow/productization beyond the Story 3.2 port; exact CI implementation form for nightly drift as long as PR gates remain hermetic.
- Final recommendation: ready-for-dev

### File List
