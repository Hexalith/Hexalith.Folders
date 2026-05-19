# Story 3.1: Configure provider binding and credential reference

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a platform engineer,
I want to configure a provider binding and credential reference for a tenant,
so that repository-backed folder creation can be gated by known provider configuration.

## Terms

- Provider binding means tenant-scoped metadata that names a supported Git provider family, an opaque provider binding reference, a non-secret credential reference, repository naming policy, default branch/ref policy, and safe capability/readiness prerequisites.
- Credential reference means an opaque identifier resolvable by Hexalith.Tenants or a configured secret provider. Folders must never store, return, log, project, serialize, or compare raw provider tokens, passwords, private keys, embedded credential URLs, or credential payloads.
- Provider binding configuration permission means the existing organization/folder ACL action `configure_provider_binding` evaluated after authoritative tenant access, never from tenant IDs supplied in the request body alone.
- Naming policy means metadata-only repository naming rules such as prefix, allowed character set, uniqueness scope, and reserved-name handling. It is not repository provisioning.
- Branch policy means metadata-only default branch/ref policy such as default branch name, allowed ref pattern, protected branch posture, and repository binding compatibility prerequisites. It is not branch creation or provider validation.
- Binding identity means `{managedTenantId}:organizations:{organizationId}` plus `providerBindingRef`; it is tenant-scoped, stable, case-sensitive, and not globally discoverable across tenants.

## Acceptance Criteria

1. Given an authenticated actor has tenant access and `configure_provider_binding` permission for the organization, when a provider binding configuration command is accepted, then the organization aggregate records provider kind, provider binding reference, non-secret credential reference ID, repository naming policy reference/details, branch/ref policy reference/details, correlation ID, task ID, idempotency key, configured status, and metadata-only audit evidence without resolving the credential or provider.
2. Given the actor lacks tenant access, folder/organization ACL permission, or authoritative tenant evidence is disabled, stale, unavailable, malformed, mismatched, or replay-conflicting, when a binding configuration command is attempted, then it is denied before any credential, provider, repository, workspace, branch, audit writer, cache, projection, binding, duplicate-detection, or existence lookup and returns the existing safe denial/read-model unavailable result family without confirming whether the binding or credential reference exists.
3. Given a command contains a raw token, password, private key, embedded credential URL, secret-shaped field, provider payload, repository URL with credentials, raw provider response, file content, diff, generated context payload, unauthorized resource detail, PEM/JWT-shaped string, connection string, or nested JSON key such as `password`, `secret`, `token`, or `clientSecret`, when validation runs, then the command is rejected and no event, projection, result, exception, log, trace, metric, diagnostic, or test output contains the forbidden value.
4. Given a binding command is accepted, when events are emitted and replayed, then they contain only metadata-safe values: tenant ID, organization ID, provider binding reference, provider family/kind, non-secret credential reference ID, policy references or safe policy fields, correlation ID, causation ID where available, task ID, idempotency key, occurred-at timestamp, configured status, and safe result code.
5. Given the same idempotency key and equivalent binding payload are submitted more than once, when the aggregate handles the retry, then the same logical result is returned without duplicate binding events; given the same idempotency key is reused with a different provider kind, credential reference, naming policy, branch policy, organization ID, or binding reference, then the command returns `idempotency_conflict` and leaves state unchanged. Equivalent payload comparison uses deterministic canonicalization over the fingerprint fields and ordering-insensitive policy metadata where the contract permits unordered collections.
6. Given the same provider binding reference already exists for the tenant organization, when a command replays equivalent metadata, then the operation is idempotent; when it attempts to replace protected metadata without an explicit replacement command defined by this story, then it returns a stable duplicate/conflict result and leaves the existing binding unchanged. The binding reference is unique within the organization scope and is not a tenant-global or cross-tenant lookup key.
7. Given the provider kind is unsupported, missing, malformed, ambiguous, casing-mismatched, a deprecated alias, structurally valid but disabled, or normalizes to a different value, when validation runs, then the command is rejected with a stable validation category; this story may allow provider families planned by the Contract Spine, but it must not hardcode behavior to exactly two providers or flatten GitHub and Forgejo capability differences.
8. Given a credential reference ID, provider binding reference, policy reference, branch name, prefix, or organization ID violates canonical identifier, length, reserved tenant, or redaction rules, when validation runs, then the command is rejected before state mutation and no sanitized error exposes the rejected value when it is sensitive.
9. Given a binding has been configured, when internal projections or query models expose it to authorized callers, then they return redacted metadata only: provider binding reference, provider family/kind, non-secret credential reference ID or redaction state, policy references, sanitized readiness prerequisite state, correlation ID, and timestamps; they do not expose credential material, provider installation details, raw provider repository details, unauthorized existence, file paths, branch secrets, or provider payload bodies.
10. Given provider readiness validation, provider capability discovery, GitHub/Forgejo adapters, repository creation, repository binding, branch/ref enforcement, workspace preparation, CLI/MCP commands, SDK helpers, and operations-console rendering are later stories, when this story is implemented, then it records the configuration foundation they consume without performing live provider calls, fetching credentials, resolving credential health, validating readiness, or provisioning repositories.
11. Given REST and SDK Contract Spine artifacts already describe provider binding and readiness surfaces, when implementation touches contracts or generated artifacts, then it preserves OpenAPI 3.1 extension semantics, parity fixture expectations, canonical error categories, and NSwag generation boundaries; generated client files are not manually edited.
12. Given tests run on a developer machine or CI PR gate, when this story is complete, then unit and contract tests pass without GitHub, Forgejo, provider credentials, tenant seed data, live Tenants services, Aspire, Dapr sidecars, Redis, Keycloak, or initialized nested submodules.

## Tasks / Subtasks

- [ ] Add organization-level provider binding domain model without creating a second aggregate. (AC: 1, 4, 5, 6, 7, 8)
  - [ ] Extend `src/Hexalith.Folders/Aggregates/Organization/OrganizationAggregate.cs` and `OrganizationState` or add narrow sibling handlers under `Aggregates/Organization/` so provider bindings live with the existing organization stream, not a parallel provider aggregate.
  - [ ] Add command/event/result types such as `ConfigureProviderBinding`, `ProviderBindingConfigured`, `OrganizationProviderBindingResult`, and validation helpers in one-public-type-per-file C# files.
  - [ ] Persist binding state in `OrganizationState` keyed by provider binding reference within the `{managedTenantId}:organizations:{organizationId}` stream; do not create tenant-global binding indexes for authorization decisions.
  - [ ] Preserve existing organization ACL behavior and tests; provider binding additions must not change `GrantOrganizationAclPrincipal` or `RevokeOrganizationAclPrincipal` semantics.
  - [ ] Keep aggregate state pure: no Dapr, HTTP, secret-store, provider API, filesystem, clock, random, or EventStore calls inside aggregate decision logic.
  - [ ] Store idempotency fingerprints in the organization stream with deterministic canonicalization over metadata-safe binding fields: tenant/organization identity, provider binding reference, provider kind, credential reference ID, naming policy, branch/ref policy, correlation/task semantics where contract-owned, and idempotency key.
- [ ] Enforce authorization-before-observation around binding configuration. (AC: 1, 2, 9)
  - [ ] Reuse the existing tenant access and ACL gating patterns from organization/folder access stories before loading or exposing credential, provider, repository, or binding information.
  - [ ] Require `configure_provider_binding` for mutation and a distinct read/status permission only where an authorized metadata read is implemented.
  - [ ] Treat tenant IDs in payloads as validation inputs only; authoritative tenant comes from authentication/EventStore envelope context.
  - [ ] Return existing safe result families for disabled, stale, unavailable, malformed, unknown, tenant mismatch, missing authority, denied, and replay-conflicting tenant evidence.
  - [ ] Add recording fakes or spies to tests so denied paths fail if they touch credential reference resolution, provider records, repository handles, workspace bindings, projections, audit writers, or caches after denial.
- [ ] Define safe binding validation and conflict behavior. (AC: 3, 5, 6, 7, 8)
  - [ ] Validate `providerBindingRef`, `credentialReferenceId`, `providerKind`, naming policy, branch policy, `correlationId`, `taskId`, and `idempotencyKey` with the same canonical identifier posture used by existing organization/folder commands unless a contract field explicitly permits a stricter shape.
  - [ ] Reject raw credential material and secret-shaped strings at validation boundaries; do not rely only on later redaction.
  - [ ] Cover nested and disguised secret-shaped values, including bearer/API keys, PEM/private-key blocks, JWT-shaped strings, embedded-credential URLs, connection strings, provider payload JSON, repository URLs with userinfo, file contents, diffs, generated context payloads, and keys named `password`, `secret`, `token`, or `clientSecret`.
  - [ ] Separate duplicate/equivalent replay from conflicting replacement and idempotency conflict in result codes and tests.
  - [ ] Keep provider kind extensible enough for future providers; GitHub and Forgejo are initial families, not an enum that blocks future provider families unless the Contract Spine explicitly owns that closed set.
  - [ ] Validate branch/naming policies as metadata syntax and policy references only; live provider capability checks belong to Stories 3.2 and 3.5.
- [ ] Project or expose redacted binding metadata for later readiness consumers. (AC: 4, 9, 10, 11)
  - [ ] Add or extend internal projection/read-model types only as needed for provider readiness stories to consume configured binding metadata.
  - [ ] Ensure public REST/query behavior, if touched, matches the existing Contract Spine path `/api/v1/provider-bindings/{providerBindingRef}` and generated parity fixtures.
  - [ ] Keep `Hexalith.Folders.Contracts` behavior-free; DTO and OpenAPI shape updates are allowed only when required by the existing spine, not to host domain logic.
  - [ ] Do not manually edit generated SDK files under `src/Hexalith.Folders.Client/Generated/`.
- [ ] Add focused tests for aggregate, authorization, redaction, and contract alignment. (AC: 1-12)
  - [ ] Add `tests/Hexalith.Folders.Tests/Aggregates/Organization/*ProviderBinding*Tests.cs` for accepted binding, duplicate equivalent replay, duplicate conflicting replacement, idempotency conflict, unsupported provider kind, malformed IDs, reserved tenant, branch/naming policy validation, and event replay.
  - [ ] Add authorization/gate tests proving denied/stale/unavailable/malformed tenant evidence stops before credential, provider, repository, binding, projection, or cache observation.
  - [ ] Add leakage tests using sentinel strings for tokens, passwords, private keys, credential URLs, repository URLs, branch names with token-shaped values, provider payloads, file content, diffs, raw claims, and unauthorized resource names.
  - [ ] Add replay tests proving accepted events and rehydrated projections remain metadata-only and never contain provider-resolved values or credential material.
  - [ ] Add contract/parity tests only if this story changes OpenAPI or parity fixtures; otherwise assert that implementation consumes existing provider-binding contract groups without drift.
  - [ ] Keep all tests offline with in-memory stores/fakes from `Hexalith.Folders.Testing`; any provider adapter test double used here must throw on outbound calls so accidental live provider access fails fast.

## Dev Notes

### Source Context

- Epic 3 lets platform engineers and authorized actors configure Git providers, validate readiness, create repository-backed folders, bind existing repositories, define branch/ref policy, and inspect capability evidence without exposing secrets. Story 3.1 specifically records provider kind, binding ID, credential reference ID, naming policy, and branch policy while never storing or returning token material. [Source: `_bmad-output/planning-artifacts/epics.md#Story 3.1`]
- PRD FR15 requires tenant-scoped organization/provider configuration and credential references; FR16 and later stories validate readiness before repository-backed creation or binding. This story is configuration, not readiness execution. [Source: `_bmad-output/planning-artifacts/prd.md#FR15`]
- Architecture assigns `OrganizationAggregate` ownership of provider bindings, repository policy, credential refs, and ACL baseline; `FolderAggregate` owns lifecycle, storage mode, repository binding, workspace readiness, ACL overrides, and file-operation metadata. [Source: `_bmad-output/planning-artifacts/architecture.md#Domain Layout`]
- Provider boundaries require capability-tested GitHub/Forgejo adapters behind provider ports, credential references and capability metadata only, known-vs-unknown provider failure taxonomy, and no secret material storage in Folders. [Source: `_bmad-output/planning-artifacts/architecture.md#Provider boundaries`]
- The project context requires zero cross-tenant leakage, metadata-only events/logs/traces/metrics/projections/audit, tenant-prefixed durable keys, fail-closed authorization, and no recursive nested submodule initialization. [Source: `_bmad-output/project-context.md#Critical Don't-Miss Rules`]

### Previous Story Intelligence

- Story 2.1 established service hosting and Tenants integration; provider binding implementation must keep builds offline and must not require live Tenants, provider credentials, or Dapr sidecars for unit tests.
- Story 2.2 established `OrganizationAggregate`, ACL command validation, deterministic idempotency fingerprints, tenant evidence gates, and metadata leakage tests. Extend these patterns instead of creating a parallel provider-binding aggregate or bespoke validation style.
- Story 2.3 established folder aggregate purity and tenant-scoped stream names; provider binding state belongs to organization streams and must not infer tenant authority from payload IDs.
- Story 2.4 and Story 2.5 are active/current access-management work. Do not overwrite their uncommitted story/status/source changes; consume stable ACL actions and current source carefully after rebasing.
- Stories 2.6 through 2.9 define layered authorization, lifecycle/status observation, archive mutation guards, and tenant worker evidence. Provider binding configuration must use those safe denial/freshness concepts without adding new response families.

### Existing Implementation State

- `src/Hexalith.Folders/Aggregates/Organization/OrganizationAggregate.cs` currently handles ACL grant/revoke commands only, with deterministic validation, two-pass event emission, and idempotency fingerprints.
- `src/Hexalith.Folders/Aggregates/Organization/OrganizationState.cs` currently stores ACL grants and idempotency fingerprints. Provider binding state is not yet modeled there.
- `src/Hexalith.Folders/Aggregates/Organization/OrganizationAclAction.cs` already includes `configure_provider_binding`, so the action name should be reused rather than inventing a new permission string.
- `tests/Hexalith.Folders.Tests/Aggregates/Organization/` already contains ACL validation, effective permission, idempotency, metadata leakage, stream shape, and tenant evidence gate tests that show the expected test style.
- The Contract Spine already contains provider binding/readiness paths and parity fixture entries under `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` and `tests/fixtures/parity-contract.yaml`; this story should implement against those artifacts or update them with matching tests if drift is intentional.

### Required Architecture Patterns

- Use .NET 10, file-scoped namespaces, nullable-aware C#, one public type per file, PascalCase public members, camelCase locals/parameters, and async APIs with `CancellationToken` for I/O boundaries.
- Keep `Hexalith.Folders.Contracts` behavior-free. Contract DTOs and OpenAPI artifacts can define shapes, but aggregate decisions, authorization, provider binding validation, and projection behavior live outside Contracts.
- Keep provider side effects out of aggregates. This story must not call GitHub, Forgejo, Dapr secret store, Tenants secret APIs, filesystem working copies, or repository APIs.
- Keep credential references opaque and non-secret in this story. Credential lifecycle, rotation, revocation, vault implementation, secret resolution, readiness checks, and credential health are deferred to later provider-readiness work.
- Use metadata-only structured logging with `tenantId`, `correlationId`, `causationId`, `taskId`, `aggregateId`, and event type where available. Never log credential values, raw credential refs, raw provider payloads, repository URLs with secrets, file contents, diffs, or unauthorized resource details.
- Preserve the SDK-as-canonical topology: CLI and MCP wrap `Hexalith.Folders.Client`; REST remains a parallel transport validated against the Contract Spine. Do not add CLI/MCP behavior in this story.

### Files To Touch

- `src/Hexalith.Folders/Aggregates/Organization/OrganizationAggregate.cs`
- `src/Hexalith.Folders/Aggregates/Organization/OrganizationState.cs`
- `src/Hexalith.Folders/Aggregates/Organization/*ProviderBinding*.cs`
- `src/Hexalith.Folders/Aggregates/Organization/OrganizationAclAction.cs` only if an existing action gap is found; `configure_provider_binding` already exists.
- `src/Hexalith.Folders/Projections/ProviderReadiness/*` or `src/Hexalith.Folders/Projections/Organization/*` only if a redacted binding read model is needed for this story.
- `src/Hexalith.Folders.Server/*ProviderBinding*` only if wiring the existing REST Contract Spine endpoint is in scope for the implementation slice.
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` and related contract tests only if existing provider-binding contract groups are incomplete for this story.
- `tests/Hexalith.Folders.Tests/Aggregates/Organization/*ProviderBinding*Tests.cs`
- `tests/Hexalith.Folders.Tests/Authorization/*` or projection tests only for authorization-before-observation and redacted metadata coverage.
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/TenantFolderProviderContractGroupTests.cs` only if contract assertions need to be tightened.

### Do Not Touch

- Do not implement `IGitProvider`, GitHub adapter, Forgejo adapter, provider readiness validation, repository creation, repository binding, branch creation, workspace preparation, locks, file mutation, commits, context queries, CLI commands, MCP tools, UI pages, repair workflows, webhooks, provider drift detection, or live provider contract tests.
- Do not store raw provider tokens, passwords, private keys, credential values, embedded credential URLs, raw provider payloads, repository URLs containing credentials, raw branch protection payloads, file contents, diffs, generated context payloads, raw claim bags, membership inventories, or unauthorized resource existence.
- Do not manually edit generated SDK files under `src/Hexalith.Folders.Client/Generated/*`.
- Do not initialize nested submodules or use recursive submodule commands.

### Testing

- Use xUnit v3 and Shouldly. Use NSubstitute only where a real seam needs substitution.
- Aggregate tests should load `OrganizationState.Empty`, apply accepted events, replay state, and verify idempotency/conflict behavior without EventStore, Dapr, Redis, Tenants, provider credentials, GitHub, or Forgejo.
- Authorization tests should prove tenant and ACL denial happen before any credential/binding/provider lookup. A recording fake should fail the test if a forbidden lookup is attempted after denial.
- Leakage tests should serialize commands, results, events, projections, Problem Details, and captured logs where applicable and assert the audit leakage corpus and provider credential sentinel values are absent.
- Contract tests should remain the guard for OpenAPI/parity drift: if the implementation changes provider-binding DTOs, update the OpenAPI contract, parity fixture, and tests in the same change.

### Regression Traps

- Do not model provider bindings on `FolderAggregate`; organization-level provider configuration is the prerequisite that later folder/repository stories consume.
- Do not resolve credential references during configuration. Storing an opaque reference is enough for Story 3.1; validation of existence/permission belongs to readiness.
- Do not expose different errors for "credential reference missing" versus "binding not found" before authorization succeeds; that leaks unauthorized existence.
- Do not query duplicate binding state, provider records, credential references, projections, caches, audit writers, or repository/workspace resources before tenant and ACL authorization have succeeded.
- Do not treat GitHub and Forgejo as interchangeable base URLs. Provider-specific capability differences are discovered in later provider-port stories.
- Do not add branch/ref policy behavior that silently creates branches or rewrites refs.
- Do not use display names, emails, repository names, branch names, or credential labels as identity.
- Do not let duplicate binding attempts create multiple active bindings for the same provider binding reference unless a future story explicitly defines replacement semantics.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 3.1`
- `_bmad-output/planning-artifacts/prd.md#FR15`
- `_bmad-output/planning-artifacts/prd.md#Security and Tenant Isolation`
- `_bmad-output/planning-artifacts/architecture.md#Domain Layout`
- `_bmad-output/planning-artifacts/architecture.md#Provider boundaries`
- `_bmad-output/planning-artifacts/architecture.md#API & Communication Patterns`
- `_bmad-output/project-context.md#Critical Don't-Miss Rules`
- `_bmad-output/implementation-artifacts/2-2-implement-organization-aggregate-acl-baseline.md`
- `_bmad-output/implementation-artifacts/2-6-enforce-layered-authorization-with-safe-denials.md`
- `_bmad-output/implementation-artifacts/2-9-react-to-tenants-events-through-worker-handlers.md`
- `src/Hexalith.Folders/Aggregates/Organization/OrganizationAggregate.cs`
- `src/Hexalith.Folders/Aggregates/Organization/OrganizationState.cs`
- `src/Hexalith.Folders/Aggregates/Organization/OrganizationAclAction.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Organization/OrganizationAclIdempotencyTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Organization/OrganizationAclMetadataLeakageTests.cs`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`
- `tests/fixtures/parity-contract.yaml`

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-19 | Applied party-mode review hardening for configuration-only scope, denial-before-observation testability, deterministic duplicate/idempotency semantics, secret-shaped input matrices, and deferred readiness decisions. | Codex |
| 2026-05-19 | Created story with organization-level provider binding configuration, credential-reference redaction, authorization-before-observation, idempotency/conflict semantics, contract alignment, and offline tests. | Codex |

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

### Completion Notes List

- Story created by `/bmad-create-story 3-1-configure-provider-binding-and-credential-reference` equivalent workflow on 2026-05-19.
- Project context, Epic 3, PRD, architecture, current organization aggregate/ACL implementation, provider-binding Contract Spine entries, recent commits, and story-creation lessons were reviewed.
- Preflight working-tree failure was classified as an active-dev-story soft warning because Story 2.5 is `in-progress` in both its artifact and sprint status; active development changes were left untouched.
- Ultimate context engine analysis completed - comprehensive developer guide created.
- Party-mode review completed on 2026-05-19T11:00:59+02:00 with Winston, Amelia, Murat, and John; coherent low-risk findings were applied inline and scope/architecture/product decisions were recorded as deferred.

### File List

## Party-Mode Review

- Date/time: 2026-05-19T11:00:59+02:00
- Selected story key: 3-1-configure-provider-binding-and-credential-reference
- Command/skill invocation used: `/bmad-party-mode 3-1-configure-provider-binding-and-credential-reference; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), John (Product Manager)
- Findings summary: The story was already viable as a bounded configuration slice, but needed sharper guardrails around authorization-before-observation, configuration-only scope, organization-scoped binding identity, deterministic idempotency/conflict comparison, secret-shaped input rejection, metadata-only replay evidence, and offline no-live-provider test doubles.
- Changes applied: Clarified accepted metadata and configured-status outputs, expanded denial-before-lookup boundaries, added secret-shaped and nested-value rejection examples, made duplicate/idempotency canonicalization explicit, required organization-scoped binding uniqueness, expanded provider-kind boundary validation, tightened no-live-readiness/provisioning scope, added recording-fake and replay tests, and noted opaque credential-reference lifecycle deferrals.
- Findings deferred: Whether a public redacted binding read model ships in this implementation slice; canonical provider-kind registry/versioning strategy; whether policy validation references an approved catalog or remains syntax-only; exact cross-story result taxonomy names; credential-reference namespace/lifecycle/rotation/health decisions; multi-provider precedence and workflow orchestration; CLI/MCP/UI behavior.
- Final recommendation: ready-for-dev
