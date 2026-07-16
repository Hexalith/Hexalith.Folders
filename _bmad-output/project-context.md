---
project_name: 'Hexalith.Folders'
user_name: 'Jerome'
date: '2026-05-20'
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
rule_count: 103
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

- Target .NET SDK `10.0.302` from `global.json` with `rollForward=latestPatch`; all in-scope projects target `net10.0` unless an individual project explicitly scopes otherwise.
- Repository configuration and project files are authoritative when planning artifacts drift: prefer `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `.csproj`, and `Hexalith.Folders.slnx` over older architecture text.
- Use C# `LangVersion=latest`, nullable enabled, implicit usings enabled, deterministic builds, and warnings-as-errors from `Directory.Build.props`.
- Use `.slnx` solution format and central package management through `Directory.Packages.props`; project files must not carry inline package versions.
- Repository reference modules under `references/` are consumed through project references detected in `Directory.Build.props`, especially Hexalith.EventStore and Hexalith.Tenants. Do not replace these with ad hoc package references or initialize nested submodules.
- Dapr .NET packages are pinned to `1.17.9` (latest stable; `1.18.x` is still rc-only — do not adopt without runtime/CLI compatibility verification); Folders uses Dapr sidecars, pub/sub, state store, service invocation, and Dapr configuration includes deny-by-default access-control policy that must be preserved unless intentionally changed.
- Aspire stable packages are aligned on `13.4.6` (`Aspire.Hosting`, `Aspire.Hosting.Azure.AppContainers`, `Aspire.Hosting.Docker`, `Aspire.Hosting.Redis`, `Aspire.Hosting.Testing`); the preview-only Keycloak/Kubernetes integrations track the same line at `13.4.6-preview.1.26319.6`; `CommunityToolkit.Aspire.Hosting.Dapr` is pinned to `13.4.0-preview.1.260602-0230` (aligned in Epic 9 / Story 9.1 to the platform `.Aspire` helpers' pin, resolving an NU1605 transitive downgrade from the prior `13.0.0`). `Directory.Packages.props` is authoritative for these versions. Do not change them without verifying compatibility via restore/build plus the integration/host lanes.
- Service defaults stay on the Microsoft.Extensions `10.x` family and OpenTelemetry packages `1.15.x`; keep OpenTelemetry package versions aligned by family.
- UI uses Microsoft Fluent UI Blazor `5.0.0-rc.3-26138.1` plus Fluent UI icons `4.14.2`; treat Fluent UI APIs as RC-sensitive (stay on the 5.0 RC line — the `4.14.x` "stable" is the previous major, not an upgrade; revisit when 5.0 reaches GA).
- CLI uses System.CommandLine `2.0.8` (latest stable; `3.0` is preview-only); MCP uses ModelContextProtocol `1.4.0`; both wrap `Hexalith.Folders.Client` instead of duplicating business behavior.
- The OpenAPI Contract Spine is `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`; NSwag.MSBuild `14.7.1`, Newtonsoft.Json `13.0.4`, and generated idempotency helpers derive from that spine.
- Do not hand-edit generated client files under `src/Hexalith.Folders.Client/Generated`; change the OpenAPI spine or generation pipeline instead.
- Tests use xUnit v3 `3.2.2`, Shouldly `4.3.0`, NSubstitute `5.3.0`, Testcontainers `4.12.0`, YamlDotNet `18.0.0`, Microsoft.NET.Test.Sdk `18.6.0`, and Microsoft.Playwright `1.60.0`.
- Treat Dapr runtime/CLI, Playwright browser binaries, Aspire preview integrations, and Fluent UI RC APIs as test-runtime compatibility risks; upgrades require focused smoke/regression coverage, not version-only edits.

## Critical Implementation Rules

### Language-Specific Rules

- Use C# file-scoped namespaces with `using` directives outside namespaces; file names must match the primary public type and public surface should stay one primary type per file.
- Follow repository naming rules: PascalCase for types, methods, properties, and constants; camelCase for locals and parameters; interfaces begin with `I`; private fields use `_camelCase`; async methods end with `Async`.
- Validate public boundaries with `ArgumentNullException.ThrowIfNull(...)` and `ArgumentException.ThrowIfNullOrWhiteSpace(...)`; do not let null/blank values drift into aggregate, authorization, hash, or HTTP mapping code.
- Model commands, events, results, and immutable contract values as records or readonly record structs when they are data-shaped; use sealed classes for concrete services and static classes for pure aggregate/validator helpers.
- Domain rejections return typed result codes and events/results; do not throw exceptions for expected business outcomes such as duplicate folders, tenant denial, idempotency replay, or missing ACL entries.
- Use `StringComparison.Ordinal`, `StringComparer.Ordinal`, and `CultureInfo.InvariantCulture` for identifiers, ordering, hashing, and wire-stable formatting. Do not use culture-sensitive comparisons for tenant, folder, action, correlation, or idempotency values.
- Normalize metadata and canonical payloads deliberately before hashing or validation. Preserve NFC normalization, length-prefixing, SHA-256 hashing, duplicate-property rejection, and ordinal field ordering in idempotency code.
- Keep sensitive metadata filtering centralized in validators. Do not duplicate or weaken blocklists for control characters, invisible format characters, paths, URLs, email markers, tokens, diffs, provider payloads, or generated context markers.
- Public async service, query, HTTP, provider, Dapr, EventStore, filesystem, and worker paths must accept and propagate `CancellationToken`; library/server code should use `ConfigureAwait(false)` where nearby code does.
- Aggregate handlers stay deterministic and side-effect free: pass timestamps in from the caller, return accepted/rejected results, and never perform I/O, provider calls, Dapr calls, logging, or filesystem work inside aggregate state transitions.
- When adding a new command or access operation, update the command interface, validator, fingerprint/canonicalization logic, aggregate dispatch switch, result code mapping, event application, and tests together.
- Prefer source-generated regex via `[GeneratedRegex]` for stable validation patterns instead of constructing regexes dynamically in hot validation paths.

### Framework-Specific Rules

- Use Hexalith.EventStore as the write-side command/query/event/projection framework. Do not introduce a parallel write-side framework or direct database write model for folder state.
- `Hexalith.Folders.Server` owns REST transport plus EventStore-compatible `/process` and `/project` endpoints; external REST remains `/api/v1/...`, while internal EventStore invocation routes remain `/process` and `/project`.
- Dapr app IDs and component names are stable contracts: `eventstore`, `tenants`, `memories`, `folders`, `folders-workers`, `folders-ui`, `statestore`, and `pubsub`.
- AppHost must resolve `DaprComponents/accesscontrol.yaml` and fail fast if it is missing. Do not silently run without the Dapr access-control configuration.
- Aspire topology is composed in `Hexalith.Folders.AppHost` via the platform helpers (`AddHexalithEventStore` gateway-only, `AddHexalithTenantsServer`, `AddHexalithMemoriesSearchIndexServer`); do not re-implement EventStore/Tenants/Memories Dapr sidecar or shared state-store/pub-sub wiring locally. `Hexalith.Folders.Aspire` retains only Folders-specific helpers and the stable app-ID/component-name constants (Epic 9).
- Folders subscribes to Hexalith.Tenants events through Dapr pub/sub topic `system.tenants.events` on pubsub `pubsub`; tenant projection handlers must drop envelope/payload tenant mismatches and preserve replay/fingerprint behavior.
- Production authorization layering is JWT validation, EventStore claim transform evidence, tenant-access projection freshness, folder ACL evidence, EventStore validators, then Dapr deny-by-default policy evidence.
- Client-controlled tenant/principal values from headers, query strings, or payloads are comparison inputs only. Authoritative tenant and principal values come from authenticated context and EventStore claim-transform evidence.
- The SDK is the typed canonical client. CLI and MCP must wrap `Hexalith.Folders.Client`; REST is a parallel transport validated against the same OpenAPI Contract Spine.
- The Contract Spine is OpenAPI 3.1 at `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`; server-generated OpenAPI, NSwag output, parity oracle rows, and idempotency helpers must stay synchronized with it.
- Problem responses must remain metadata-only and follow the canonical extension shape: `category`, `code`, `message`, `correlationId`, `taskId` when present, `retryable`, `clientAction`, and `details.visibility`.
- Blazor UI is a read-only operations console scaffold backed by the client. It must not expose mutation paths, credential reveal, file browsing, file editing, raw diffs, repair actions, or unrestricted filesystem access in MVP.
- Workers own external provider, Git, working-copy, reconciliation, and rate-limit side effects. Aggregates, REST handlers, CLI, MCP, and UI should not perform provider or filesystem mutations directly.
- `Hexalith.Folders.Workers` updates the Hexalith.Memories **search index** by publishing `SearchIndexEntryChanged` / `SearchIndexEntryRemoved` CloudEvents to Dapr pub/sub component `pubsub`, topic `memories-events`, CloudEvent source `hexalith-folders` (routed to the `folders-index` partition by Memories' `SourceToTenantMap`, upserting by `(TenantId, AggregateId)`); follow the Hexalith.Tenants `MemoriesSearchIndexEventPublisher` precedent. Do **not** call `Hexalith.Memories.Client.Rest.IngestAsync` — that is the separate RAG memory-ingestion subsystem (experimental `HXL001`), not the search index. Memories references are confined to two projects: `Hexalith.Folders.Workers` (the producer; `Hexalith.Memories.Contracts` only) and — for the Story 10.5 read facade (Option B) — `Hexalith.Folders.Server`, which may reference `Hexalith.Memories.Client.Rest` + `Hexalith.Memories.Contracts` and call `AddMemoriesClient()` / `MemoriesClient.SearchAsync` (the syntactic search index only, never `IngestAsync`), mirroring `Hexalith.Tenants.UI`. Do **not** add a Memories reference or `AddMemoriesClient()` to any other Folders project (core, Contracts, Client, CLI, MCP, UI, Testing stay Memories-free and reach the facade only through the generated SDK over REST).
- Provider adapters for GitHub and Forgejo must sit behind provider ports and capability tests. Do not treat Forgejo as a GitHub base-URL swap.

### Testing Rules

- Test projects mirror production boundaries: `tests/Hexalith.Folders.{Area}.Tests` for each production area, plus integration, UI E2E, fixtures, load, and tooling lanes.
- Use xUnit v3 and Shouldly by default; keep assertions readable in test bodies and use NSubstitute only for focused unit-test doubles.
- Use `TestContext.Current.CancellationToken` in async xUnit tests and propagate cancellation into app code under test.
- Unit tests own pure aggregate, validator, idempotency, authorization, projection, query, and metadata-redaction rules. Integration tests own EventStore, Dapr, Aspire, provider, and REST boundary behavior.
- Every mutation-command story (any command that appends folder events through `/process`) MUST include an in-process integration test that does NOT mock `IEventStoreGatewayClient`; it must drive the real REST → gateway → `/process` → `IDomainProcessor` → gate → persistence path and assert the accepted (202) shape plus the denial-table rows end-to-end. A green unit suite over a `RecordingEventStoreGatewayClient` short-circuit is NOT acceptance evidence — that is the 2.8/2.8b "green tests, broken production wiring" trap. The central `ValidateOnBuild`/`ValidateScopes` server-host composition smoke (Story 7.18) complements this by catching shared-surface DI drift once centrally instead of cascading silently across hundreds of endpoint tests.
- Scaffold, unit, contract, governance, and safety gates must run without provider credentials, tenant seed data, production secrets, running Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, network calls, or nested submodule initialization.
- Shared normative fixtures live under `tests/fixtures/`; do not fork parity, idempotency, previous-spine, safety-channel, redaction, or quarantine corpora into per-test-project copies.
- Contract Spine tests must validate OpenAPI 3.1 shape, server-vs-spine drift, generated client drift, parity oracle rows, previous-spine baseline, and metadata-only diagnostics.
- Safety invariant tests must scan all declared output channels with `tests/fixtures/audit-leakage-corpus.json`, `tests/fixtures/safety-channel-inventory.json`, and quarantined negative controls.
- Layered authorization tests must assert short-circuit order and no protected resource touch before the relevant authorization layer passes.
- Tests for safe denial must prove unauthorized and nonexistent resources stay indistinguishable at the caller-visible boundary.
- Idempotency tests must cover replay, conflict, ordinal field ordering, duplicate JSON property rejection, Unicode/control-character handling, UTC date handling, and tenant-scoped keys.
- UI E2E is a deferred Playwright-on-.NET lane; keep skipped placeholders until Epic 6 story 6-2 provides stable read-only console routes and selectors. Do not add brittle CSS/text/sleep-based tests.
- Use `src/Hexalith.Folders.Testing` factories and HTTP helpers for tenant, folder, task, correlation, idempotency, and authorization contexts; prefer explicit overrides over hidden static fixture state.
- Focused gate scripts under `tests/tools/*.ps1` are CI contracts. Do not add recursive submodule initialization, artifact upload, publish, semantic-release, production endpoints, secrets, or broad network assumptions to them.

### Code Quality & Style Rules

- Root configuration belongs at repository root: `.editorconfig`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `nuget.config`, `.gitignore`, and `Hexalith.Folders.slnx`.
- Preserve `.editorconfig` formatting: spaces, 4-space indentation, CRLF for most files, LF for shell/YAML/container artifacts, UTF-8, trimmed trailing whitespace, and final newline.
- Keep package versions centralized in `Directory.Packages.props`; never add inline `Version` attributes to project `PackageReference`s.
- Preserve dependency direction: Contracts has no behavior/project references; core references Contracts; Server references core, Contracts, ServiceDefaults, EventStore, and Tenants; Client references Contracts; CLI/MCP/UI wrap Client; Workers own process managers.
- Pack policy is opt-in. Only packageable library projects should set `<IsPackable>true</IsPackable>`; host, adapter, sample, worker, and test projects should not produce packages unless explicitly designed to.
- Keep generated client files in `src/Hexalith.Folders.Client/Generated` generated by build targets. Do not manually edit generated `.g.cs` files to fix behavior.
- Keep source organized by concept area, not broad type buckets: `Aggregates/{Concept}`, `Authorization`, `Projections/{Concept}`, `Queries/{Concept}`, provider ports/adapters, workers, idempotency, redaction, and caching.
- Use shared contract/gate docs under `docs/contract/`, exit-criteria evidence under `docs/exit-criteria/`, and ADRs under `docs/adrs/`; do not bury normative rules in ad hoc README text.
- Use shared fixtures under `tests/fixtures/` for normative cross-project data and keep ownership metadata machine-checkable.
- Keep `bin/`, `obj/`, `TestResults/`, coverage, package, Playwright browser cache, logs, and local build artifacts out of source unless they are intentional fixtures.
- Comments should explain domain invariants, security boundaries, compatibility pins, generated-artifact contracts, or story/ADR context; avoid comments that restate straightforward C#.
- Structured diagnostics, test failure messages, logs, metrics, traces, Problem Details, and docs examples must be metadata-only. Never include secrets, tokens, raw file contents, diffs, provider payloads, local absolute paths, or unauthorized resource existence.
- Error/problem shapes, result code names, action tokens, parity dimensions, sensitive metadata categories, and state names are public contracts. Changing one requires corresponding OpenAPI, fixtures, docs, tests, and generated artifacts.

### Development Workflow Rules

- Never initialize or update nested submodules recursively unless the user explicitly asks for nested submodules.
- Initialize/update only repository-declared submodules under `references/` by default: `references/Hexalith.AI.Tools`, `references/Hexalith.Builds`, `references/Hexalith.Commons`, `references/Hexalith.EventStore`, `references/Hexalith.FrontComposer`, `references/Hexalith.Memories`, and `references/Hexalith.Tenants`.
- Do not use `git submodule update --init --recursive` in setup guidance, scripts, CI, docs, or agent instructions unless it is explicitly framed as user-requested nested-submodule work.
- CI checkout should keep `submodules: false`; local setup docs may show the explicit root-level submodule command only.
- Default repository verification starts with `dotnet restore Hexalith.Folders.slnx`, `dotnet build Hexalith.Folders.slnx --no-restore`, then the narrowest relevant `dotnet test` or gate script.
- Use focused gate scripts as workflow contracts: `run-contract-spine-gates.ps1`, `run-safety-invariant-gates.ps1`, and `run-governance-completeness-gates.ps1`.
- Do not add artifact upload, package publish, semantic-release, live provider drift, production endpoint calls, or secret-dependent behavior to focused PR gates unless the story explicitly targets release/live validation.
- Contract Spine, generated client, parity oracle, idempotency helper, safety corpus, and governance evidence changes must update source artifacts, docs, fixtures, generated outputs, and tests together.
- When changing root build policy, solution shape, submodule references, package versions, or CI scripts, run scaffold/root verification and check that recursive submodule commands were not introduced.
- Use sibling modules as read-only references for patterns unless the task explicitly asks to modify a submodule; submodule changes require separate intent and commits inside that submodule.
- AppHost and Aspire topology changes require restart of the running Aspire app before resource wiring can be trusted.
- UI E2E work requires installing matching Playwright browser binaries through `tests/install-playwright.ps1`; do not assume the NuGet package alone is sufficient.
- Keep local developer conveniences out of blocking lanes unless they are hermetic and metadata-only.

### Critical Don't-Miss Rules

- Zero cross-tenant leakage is the top safety invariant. Deny access before touching file, workspace, credential, repository, lock, commit, provider, audit, or cache resources.
- The reserved `system` tenant must never be used for managed tenant folder streams. Folder streams use `{managedTenantId}:folders:{folderId}` and organization streams use `{managedTenantId}:organizations:{organizationId}`.
- Tenant authority comes from authenticated context and EventStore claim-transform evidence only. Payload, query, and header tenant/principal values are comparison inputs, not authority.
- Authorization order is contractual: JWT validation, EventStore claim transform, tenant-access freshness, folder ACL, EventStore validator, then Dapr deny-by-default policy. Do not reorder or skip layers for convenience.
- Metadata-only is non-negotiable across events, logs, traces, metrics, projections, audit records, Problem Details, console responses, provider diagnostics, generated artifacts, docs examples, and test failure messages.
- All durable operational keys and cache keys must carry tenant scope unless covered by an approved entry in `tests/fixtures/cache-key-exceptions.yaml`.
- Idempotency is required for mutating operations. Same key plus equivalent payload returns the same logical result; same key plus different payload returns `idempotency_conflict`.
- Non-mutating operations must not accept `Idempotency-Key`; they still require correlation behavior, authorization parity, safe denial shape, audit metadata, and read-consistency classification.
- Never search, glob, list, partially read, or inspect paths before tenant access, folder ACL, and path policy pass.
- Unknown provider outcomes must enter `unknown_provider_outcome` or `reconciliation_required`; do not retry in ways that could duplicate repositories, file mutations, commits, audits, or idempotency records.
- Workspace lifecycle must follow the C6 matrix. Every state/event pair must have a positive transition or explicit `state_transition_invalid` rejection with state unchanged.
- Read-only operations console means read-only. No mutation, repair, credential reveal, file browsing, file editing, raw diffs, hidden provider payloads, or unrestricted filesystem views in MVP.
- CLI exit codes and MCP failure kinds must map one-to-one to canonical categories from the parity oracle. Do not collapse distinct failures for adapter convenience.
- Do not hand-edit generated parity rows, generated clients, or generated idempotency helpers. Change the OpenAPI Contract Spine or generator inputs and regenerate.
- Do not initialize/update nested submodules recursively by default. This is a repository safety rule, not a setup preference.
- GitHub and Forgejo support must be capability-tested. Do not assume API compatibility, permission equivalence, webhook equivalence, rate-limit equivalence, or identical default-branch behavior.
- Redacted values must be visibly distinct from unknown or missing values. Silent truncation or hiding redaction is treated as an operator-facing correctness bug.

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

Last Updated: 2026-06-09
