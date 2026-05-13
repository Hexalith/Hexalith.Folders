---
project_name: 'Hexalith.Folders'
user_name: 'Jerome'
date: '2026-05-10'
workflow: 'bmad-generate-project-context'
status: 'complete'
sections_completed:
  - discovery
  - technology_stack
  - language_rules
  - framework_rules
  - testing_rules
  - code_quality_rules
  - workflow_rules
  - critical_dont_miss_rules
rule_count: 72
optimized_for_llm: true
existing_patterns_found: 12
technology_stack_discovered: true
source_context:
  - '_bmad-output/planning-artifacts/architecture.md'
  - '_bmad-output/planning-artifacts/prd.md'
  - '_bmad-output/implementation-artifacts/1-1-establish-a-consumer-buildable-module-scaffold.md'
  - '_bmad-output/implementation-artifacts/1-2-establish-root-configuration-and-submodule-policy.md'
---

# Project Context for AI Agents

_This file contains critical rules and patterns that AI agents must follow when implementing code in this project. Focus on unobvious details that agents might otherwise miss._

---

## Technology Stack & Versions

- Target .NET 10 with `net10.0`; keep nullable, implicit usings, `LangVersion=latest`, and warnings-as-errors inherited from root build configuration.
- Use `.slnx` solution format and central package management through `Directory.Packages.props`; project files should not carry package versions unless explicitly justified.
- Mirror Hexalith.Tenants and Hexalith.EventStore sibling-module conventions; architecture pins Hexalith.EventStore and Hexalith.Tenants to `3.15.1`.
- Use Dapr for pub/sub, state store, service invocation, actors, access control, and resiliency; do not bypass Dapr/EventStore with an alternate write-side framework.
- Use .NET Aspire AppHost for local topology; builds must not require running Aspire, Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, provider credentials, tenant data, or production secrets.
- Use OpenAPI 3.1 as the Contract Spine in `Hexalith.Folders.Contracts`; SDK generation uses NSwag.
- CLI uses System.CommandLine 2.x and MCP uses ModelContextProtocol C# SDK; both wrap `Hexalith.Folders.Client` rather than implementing independent business semantics.
- Testing baseline is xUnit v3, Shouldly, NSubstitute, Testcontainers, and Aspire test host patterns from sibling modules.

## Critical Implementation Rules

### Language-Specific Rules

- Use C# file-scoped namespaces and one public type per file; file names must match the primary public type.
- Follow sibling-module naming: PascalCase for types, methods, properties, and constants; camelCase for locals and parameters; interfaces begin with `I`; private fields use `_camelCase`; async methods end with `Async`.
- Keep `Hexalith.Folders.Contracts` behavior-free: DTOs, identity value objects, OpenAPI/contract artifacts, and contract extensions only. Do not reference Hexalith.EventStore, Hexalith.Tenants, Server, Client, CLI, MCP, UI, Workers, or domain behavior from Contracts.
- Keep aggregates and domain logic in `Hexalith.Folders`; transports, UI, CLI, MCP, and workers must not duplicate aggregate decisions.
- Keep aggregate IDs opaque and immutable, preferably GUID/ULID wrappers. Folder hierarchy, names, and paths are projected metadata, never aggregate identity.
- Authoritative tenant context comes from authentication context and EventStore envelopes, never from request payloads. Treat tenant IDs in payloads as inputs to validate, not authority.
- Use async APIs for I/O, provider calls, Dapr calls, EventStore calls, and filesystem work; propagate `CancellationToken` through public async paths.
- Keep provider/workspace side effects out of aggregate state transitions. Aggregates return events/results; workers and process managers perform external Git/provider/filesystem work and submit follow-up commands.
- Do not store raw provider tokens, secrets, file contents, diffs, generated context payloads, or unauthorized resource existence in events, logs, traces, metrics, projections, audit records, diagnostics, or errors.

### Framework-Specific Rules

- Use Hexalith.EventStore as the write-side command/query/event/projection framework. Do not introduce a parallel write-side framework or direct database write model for folder state.
- `Hexalith.Folders.Server` owns REST transport and EventStore `/process` and `/project` domain-service endpoints; external REST is `/api/v1/...`, internal EventStore invocation remains `/process` and `/project`.
- Dapr app IDs are stable and meaningful: `eventstore`, `tenants`, `folders`, `folders-ui`, and `folders-workers`.
- Folders subscribes to Hexalith.Tenants events through Dapr pub/sub on `system.tenants.events` and maintains a local fail-closed tenant-access projection.
- Production authorization layering is JWT validation, EventStore claim transform, tenant-access projection freshness, folder ACL, EventStore validators, then Dapr deny-by-default policy with mTLS.
- The SDK is the typed canonical client. CLI and MCP wrap `Hexalith.Folders.Client`; REST is a parallel transport validated against the same Contract Spine.
- The Contract Spine is OpenAPI 3.1 at `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`; server-generated OpenAPI must be validated against it as a blocking gate.
- File upload contract is bimodal: `PutFileInline` for content up to 256KB, `PutFileStream` for streaming larger content, with SDK convenience `UploadFileAsync(stream)`.
- Blazor Server operations console is read-only, projection-backed, and client-backed. It must not expose mutation paths, credential reveal, file browsing, file editing, raw diffs, or repair actions in MVP.
- Provider adapters for GitHub and Forgejo sit behind provider ports. Do not treat Forgejo as a GitHub base-URL swap; capability differences require contract tests.
- Workers own external provider, Git, working-copy, reconciliation, and rate-limit side effects. Aggregates and REST handlers should not perform provider or filesystem operations directly.

### Testing Rules

- Test projects mirror `src/` one-to-one: `tests/Hexalith.Folders.{Area}.Tests` for each production project, plus integration tests and shared fixtures.
- Use xUnit v3, Shouldly, NSubstitute, Testcontainers, and Aspire test-host patterns from sibling modules.
- Scaffold and unit tests must run without provider credentials, tenant seed data, production secrets, running Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, or initialized nested submodules.
- Add scaffold smoke tests for solution shape, dependency direction, and root policy whenever root/project layout changes.
- Contract and parity tests consume `tests/fixtures/parity-contract.yaml`; schema-validate it with `tests/fixtures/parity-contract.schema.json` before tests consume it.
- SDK and REST tests assert transport parity. CLI and MCP tests assert behavioral parity, including pre-SDK errors, credential sourcing, idempotency-key sourcing, correlation sourcing, CLI exit codes, and MCP failure kinds.
- Every workspace state/event pair in the C6 transition matrix must have a defined outcome and test coverage. Adding a state or event requires matrix and test updates in the same change.
- Redaction/sentinel tests must iterate `tests/fixtures/audit-leakage-corpus.json` across logs, traces, metrics labels, events, audit records, projections, provider diagnostics, console views, and error responses.
- Provider contract tests run in two modes: hermetic PR gate with pinned fixtures and live nightly drift checks against real GitHub/Forgejo.
- Cache-key tenant-prefix enforcement is a build/CI gate. Any cache key for in-process, Dapr state, Redis, or distributed cache must carry a tenant prefix.
- Integration tests own Aspire/Dapr/EventStore/Tenants topology behavior; unit tests should use in-memory fakes from `Hexalith.Folders.Testing` and keep external side effects out.

### Code Quality & Style Rules

- Root configuration belongs at repository root: `.editorconfig`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `nuget.config`, and `Hexalith.Folders.slnx`.
- Adapt configuration from `Hexalith.Tenants`, but replace package metadata, repository URLs, descriptions, and tags so they identify Hexalith.Folders.
- Keep package versions centralized in `Directory.Packages.props`; do not add `<PackageReference Version="...">` in project files unless there is an explicit exception.
- Preserve dependency direction: Contracts -> no project behavior references; core references Contracts; Server references core + Contracts; Client references Contracts; CLI/MCP wrap Client; UI references Client; Workers own process managers.
- Organize source by concept area: `Aggregates/{Concept}`, `Projections/{Concept}`, `Providers/{ProviderName}`, `Workers/{ConceptWorkflows}`, `Authorization`, `Idempotency`, `Redaction`, and `Caching`.
- Use shared fixtures under `tests/fixtures/` for normative cross-project data; do not fork parity, redaction, or idempotency corpora into per-test-project copies.
- Keep placeholder code non-operative and fail-closed at runtime. Scaffold stories should not smuggle partial provider, contract, CLI, MCP, UI, or worker implementations.
- Comments should explain domain invariants, security boundaries, or non-obvious compatibility decisions; avoid comments that restate straightforward C#.
- Error responses use RFC 9457 Problem Details with canonical fields: `category`, `code`, `message`, `correlationId`, `retryable`, `clientAction`, and `details`.
- Structured logs, metrics, traces, and audit fields must use correlation/task IDs and metadata-only values; never use file contents, diffs, secrets, tokens, or generated context payloads as telemetry values.

### Development Workflow Rules

- Never initialize or update nested submodules recursively unless the user explicitly asks for nested submodules.
- Initialize or update only root-level submodules by default: `Hexalith.AI.Tools`, `Hexalith.Commons`, `Hexalith.EventStore`, `Hexalith.FrontComposer`, `Hexalith.Memories`, and `Hexalith.Tenants`.
- Do not use `git submodule update --init --recursive` in setup guidance, scripts, or agent instructions unless it is explicitly framed as user-requested nested-submodule work.
- Build verification for scaffold/root changes is `dotnet restore Hexalith.Folders.slnx` followed by `dotnet build Hexalith.Folders.slnx` from the repository root.
- Root setup and scaffold work must not require provider credentials, tenant seed data, production secrets, running Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, or nested submodule initialization.
- Use sibling modules as read-only references for patterns unless the story explicitly asks to modify them.
- Phase 0 scaffold work should create the expected `src`, `tests`, `samples`, `docs/adrs`, `docs/exit-criteria`, `tests/fixtures`, and `tests/tools/parity-oracle-generator` structure before implementing feature behavior.
- Phase 1 Contract Spine work is blocked until C3 retention and C4 input limits are resolved and recorded in `docs/exit-criteria/`.
- Any change to states, error categories, parity row shape, sensitive metadata categories, provider contract snapshots, or Dapr policy must update the corresponding tests/fixtures/gates in the same change.
- Do not add repair workflows, local-only folder mode, webhooks, brownfield adoption, multi-organization-per-tenant, or unrestricted operations-console mutation paths during MVP scaffold/contract stories.

### Critical Don't-Miss Rules

- Zero cross-tenant leakage is the top safety invariant. Deny access before touching file, workspace, credential, repository, lock, commit, provider, audit, or cache resources.
- Events, logs, traces, metrics, projections, audit records, console responses, provider diagnostics, and error messages are metadata-only. Never include file contents, diffs, generated context payloads, provider tokens, credential material, secrets, or unauthorized resource existence.
- All cache keys and durable operational keys must carry tenant scope. A missing tenant prefix is a correctness/security bug, not a naming nit.
- Idempotency is required for workspace preparation, lock acquisition, file mutation, commit, and cleanup. Same key plus equivalent payload returns the same logical result; same key plus different payload returns `idempotency_conflict`.
- Unknown provider outcomes must enter `unknown_provider_outcome` or `reconciliation_required`; do not retry in a way that could duplicate repositories, file changes, commits, or audits.
- Workspace lifecycle must follow the C6 transition matrix exactly. Invalid state/event pairs return `state_transition_invalid` and leave state unchanged.
- Context-query authorization order is tenant access, folder ACL, path policy, then search/glob/partial-read execution. Never search first and filter later.
- Working copies are disposable caches under configured workspace roots. EventStore state, projections, idempotency records, and provider truth are authoritative; do not infer durable truth from local files.
- The operations console is read-only in MVP. Redacted fields must be visibly distinct from unknown/missing fields; do not silently truncate or hide redaction.
- CLI exit codes and MCP failure kinds must map one-to-one to canonical categories. Do not collapse distinct errors for adapter convenience.
- GitHub and Forgejo provider support must be capability-tested. Do not assume API compatibility, permission equivalence, webhook equivalence, or identical default-branch behavior.
- Mid-task authorization revocation must affect held locks within the configured revalidation budget; locks are not exempt from tenant-access changes.
- Aggregate streams must use `{managedTenantId}:folders:{folderId}` and `{managedTenantId}:organizations:{organizationId}`; managed tenant folders must never use the reserved `system` tenant.

---

## Usage Guidelines

**For AI Agents:**

- Read this file before implementing code in Hexalith.Folders.
- Follow all rules exactly as documented; when in doubt, choose the more restrictive option.
- Update this file when new project-specific implementation patterns become stable.

**For Humans:**

- Keep this file lean and focused on rules agents are likely to miss.
- Update it when the technology stack, architecture decisions, or workflow policies change.
- Remove rules that become obvious or mechanically enforced everywhere.

Last Updated: 2026-05-10
