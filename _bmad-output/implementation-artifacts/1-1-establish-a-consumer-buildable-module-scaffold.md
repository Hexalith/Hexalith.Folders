# Story 1.1: Establish a consumer-buildable module scaffold

Status: in-progress

Created: 2026-05-10

## Story

As a platform engineer and downstream consumer,
I want the Hexalith.Folders solution scaffold to build with the approved project layout,
so that consumers and later stories have a stable, convention-compliant module baseline.

## Acceptance Criteria

1. Given an empty Hexalith.Folders module repository, when the scaffold is created, then `Hexalith.Folders.slnx` contains the canonical projects listed in the Solution Inventory under `src`, `tests`, and `samples`.
2. Project references follow the Allowed Reference Graph, every project targets .NET 10 through shared root configuration, and package versions are centrally managed from `Directory.Packages.props`.
3. From a clean checkout with only root-level submodules initialized, `dotnet restore Hexalith.Folders.slnx` and `dotnet build Hexalith.Folders.slnx` succeed without provider credentials, tenant data, private production feeds, production secrets, running Aspire, Dapr, Keycloak, Redis, or initialized nested submodules.
4. The scaffold reuses sibling-module conventions from `Hexalith.Tenants` and `Hexalith.EventStore` instead of introducing a generic or independent project template.
5. Root-level submodules may be used as references, but nested submodules must not be initialized, updated, or required by this story.
6. Scaffold verification treats the canonical solution inventory and allowed reference graph as contract checks: missing required projects, unexpected buildable `Hexalith.Folders.*` projects, forbidden direct references, per-project package versions, and per-project target-framework drift fail the story.

## Tasks / Subtasks

- [x] Create root solution and shared build files. (AC: 1, 2, 4)
  - [x] Create `Hexalith.Folders.slnx` using the sibling-module `.slnx` convention.
  - [x] Add `global.json` aligned with `Hexalith.Tenants/global.json` unless a newer installed SDK patch is already required by this repository.
  - [x] Add `Directory.Build.props` by adapting the `Hexalith.Tenants` file to `Hexalith.Folders`, including `net10.0`, nullable, implicit usings, warnings-as-errors, `LangVersion=latest`, package metadata, and root-level sibling path detection for `Hexalith.EventStore` and `Hexalith.Tenants`.
  - [x] Add `Directory.Packages.props` with central package management. Start from `Hexalith.Tenants/Directory.Packages.props`; do not invent independent version policy.
- [x] Create the expected source projects. (AC: 1, 2, 4)
  - [x] `src/Hexalith.Folders.Contracts`
  - [x] `src/Hexalith.Folders`
  - [x] `src/Hexalith.Folders.Server`
  - [x] `src/Hexalith.Folders.Client`
  - [x] `src/Hexalith.Folders.Cli`
  - [x] `src/Hexalith.Folders.Mcp`
  - [x] `src/Hexalith.Folders.UI`
  - [x] `src/Hexalith.Folders.Workers`
  - [x] `src/Hexalith.Folders.Aspire`
  - [x] `src/Hexalith.Folders.AppHost`
  - [x] `src/Hexalith.Folders.ServiceDefaults`
  - [x] `src/Hexalith.Folders.Testing`
  - [x] Ensure these are the only buildable `src/Hexalith.Folders.*` projects added by this story; later behavior surfaces must remain out of scope.
- [x] Create the expected test and sample projects. (AC: 1, 3)
  - [x] Mirror `src/` with `tests/Hexalith.Folders.*.Tests` projects, including `Contracts`, core `Folders`, `Server`, `Client`, `Cli`, `Mcp`, `UI`, `Workers`, `Testing`, and `IntegrationTests`.
  - [x] Create `samples/Hexalith.Folders.Sample` and `samples/Hexalith.Folders.Sample.Tests`.
  - [x] Add minimal scaffolding smoke tests that can run without Dapr, provider credentials, tenant seed data, or initialized nested submodules.
  - [x] Do not add separate AppHost, Aspire, or ServiceDefaults test projects unless they are compile-only and needed to enforce the scaffold contract.
- [x] Wire project references in dependency direction only. (AC: 2, 4)
  - [x] `Contracts` owns DTO and contract placeholders only. It must not reference `Hexalith.EventStore`, `Hexalith.Tenants`, Server, Client, CLI, MCP, UI, Workers, or core domain behavior.
  - [x] `Hexalith.Folders` references `Contracts` and only the sibling Hexalith infrastructure needed for domain compilation.
  - [x] `Server` references core domain, `Contracts`, and `ServiceDefaults`; it is the future REST and EventStore domain-service host.
  - [x] `Client` references `Contracts` only unless a compile-only subscription helper requires the explicit Tenants client pattern.
  - [x] `Cli` and `Mcp` must compile as adapters and must not add independent business logic.
  - [x] `UI` must compile as a read-only console shell and reference `Client`, not core domain.
  - [x] `Workers` owns future process managers and reconcilers; keep external provider behavior as placeholders only in this story.
  - [x] `AppHost` wires local Aspire topology only enough to compile; it must not require running Keycloak, Dapr, Redis, provider credentials, or tenant data for `dotnet build`.
  - [x] Add a scaffold smoke test for the allowed project-reference graph, including forbidden references from `Contracts` to behavior or infrastructure projects and forbidden dependencies from `Client` to Server, UI, CLI, MCP, or Workers.
  - [x] Implement the reference-graph smoke test from project files or solution metadata, not from hand-maintained string lists that can diverge from the scaffold.
- [x] Add scaffold-only placeholders and normative directories. (AC: 1, 3)
  - [x] Add `docs/exit-criteria/_template.md` and `docs/adrs/0000-template.md`.
  - [x] Add `tests/fixtures/audit-leakage-corpus.json`, `tests/fixtures/parity-contract.schema.json`, `tests/fixtures/previous-spine.yaml`, and `tests/fixtures/idempotency-encoding-corpus.json` as minimal valid placeholders.
  - [x] Add `tests/load/Hexalith.Folders.LoadTests.csproj` only if it can compile without load infrastructure side effects; otherwise add a placeholder directory note and defer the runnable load project to its capacity story.
  - [x] Add `tests/tools/parity-oracle-generator/` placeholder structure without implementing the generator.
- [x] Verify and document scaffold build behavior. (AC: 2, 3, 5)
  - [x] Run `dotnet restore Hexalith.Folders.slnx`.
  - [x] Run `dotnet build Hexalith.Folders.slnx`.
  - [x] Confirm root build configuration provides `net10.0`, central package management, nullable, implicit usings, warnings-as-errors, and `LangVersion=latest` without per-project version drift.
  - [x] Confirm project package references omit `Version` metadata except where the SDK or NuGet tooling requires a documented exception.
  - [x] Confirm the build does not require secrets, provider credentials, tenant data, production Dapr components, or nested submodule initialization.
  - [x] Confirm no recursive submodule command is needed; if submodules must be initialized locally, use only root-level submodules.
  - [x] Record any intentionally empty projects, omitted optional test projects, SDK/package deviations, and non-runnable placeholder directories in completion notes so reviewers can distinguish scaffold placeholders from missing implementation.

## Dev Notes

### Scope Boundaries

- This story creates a buildable module scaffold only. It does not author the OpenAPI Contract Spine, parity oracle, idempotency helpers, provider adapters, lifecycle state machine, REST endpoints, CLI commands, MCP tools, UI diagnostic pages, or production workflows.
- Do not turn placeholder projects into partial implementations. Later stories own contract authoring, provider readiness, workspace lifecycle, parity, audit, and operations console behavior.
- Do not initialize or update nested submodules. The scaffold may reference root-level sibling submodules already present in this repository, especially `Hexalith.Tenants` and `Hexalith.EventStore`, but nested submodule state must not be a build prerequisite.
- Do not introduce generated clients, OpenAPI documents, provider-specific configuration, runnable topology assumptions, CLI verbs, MCP tools, UI pages, or lifecycle models to make the scaffold feel complete.

### Required Project Shape

The architecture selects a sibling-module starter, not a generic template. Use `Hexalith.Tenants` as the baseline for root configuration, EventStore integration, AppHost, Aspire, ServiceDefaults, Testing helpers, and module packaging. Use `Hexalith.EventStore.Admin.*` only for the adapter surface patterns needed by CLI, MCP, and UI.

Expected layout:

```text
Hexalith.Folders.slnx
Directory.Build.props
Directory.Packages.props
global.json
src/
  Hexalith.Folders.Contracts/
  Hexalith.Folders/
  Hexalith.Folders.Server/
  Hexalith.Folders.Client/
  Hexalith.Folders.Cli/
  Hexalith.Folders.Mcp/
  Hexalith.Folders.UI/
  Hexalith.Folders.Workers/
  Hexalith.Folders.Aspire/
  Hexalith.Folders.AppHost/
  Hexalith.Folders.ServiceDefaults/
  Hexalith.Folders.Testing/
tests/
  Hexalith.Folders.Contracts.Tests/
  Hexalith.Folders.Tests/
  Hexalith.Folders.Server.Tests/
  Hexalith.Folders.Client.Tests/
  Hexalith.Folders.Cli.Tests/
  Hexalith.Folders.Mcp.Tests/
  Hexalith.Folders.UI.Tests/
  Hexalith.Folders.Workers.Tests/
  Hexalith.Folders.Testing.Tests/
  Hexalith.Folders.IntegrationTests/
  fixtures/
samples/
  Hexalith.Folders.Sample/
  Hexalith.Folders.Sample.Tests/
docs/
  adrs/
  exit-criteria/
```

### Solution Inventory

The scaffold story owns only these canonical solution projects. Additional functional projects, public API surfaces, commands, tools, endpoints, provider adapters, lifecycle models, or production workflows are out of scope unless a later story adds them.

| Area | Canonical projects |
|---|---|
| `src` | `Hexalith.Folders.Contracts`, `Hexalith.Folders`, `Hexalith.Folders.Server`, `Hexalith.Folders.Client`, `Hexalith.Folders.Cli`, `Hexalith.Folders.Mcp`, `Hexalith.Folders.UI`, `Hexalith.Folders.Workers`, `Hexalith.Folders.Aspire`, `Hexalith.Folders.AppHost`, `Hexalith.Folders.ServiceDefaults`, `Hexalith.Folders.Testing` |
| `tests` | `Hexalith.Folders.Contracts.Tests`, `Hexalith.Folders.Tests`, `Hexalith.Folders.Server.Tests`, `Hexalith.Folders.Client.Tests`, `Hexalith.Folders.Cli.Tests`, `Hexalith.Folders.Mcp.Tests`, `Hexalith.Folders.UI.Tests`, `Hexalith.Folders.Workers.Tests`, `Hexalith.Folders.Testing.Tests`, `Hexalith.Folders.IntegrationTests` |
| `samples` | `Hexalith.Folders.Sample`, `Hexalith.Folders.Sample.Tests` |

`docs/adrs`, `docs/exit-criteria`, `tests/fixtures`, and `tests/tools/parity-oracle-generator` are scaffold directories or fixture/tool placeholders, not separate solution projects unless a later story makes them buildable projects.

The source, test, and sample inventories above are exact for this story. Additional buildable projects under `src`, `tests`, or `samples` must be deferred unless they are required only to keep the listed scaffold projects compiling and are explicitly recorded in completion notes.

### Allowed Reference Graph

- `Hexalith.Folders.Contracts` has no domain, host, adapter, worker, UI, EventStore, Tenants, or behavior dependencies.
- `Hexalith.Folders` may reference `Hexalith.Folders.Contracts` and only sibling Hexalith infrastructure required for domain compilation.
- `Hexalith.Folders.Server` may reference `Hexalith.Folders`, `Hexalith.Folders.Contracts`, and `Hexalith.Folders.ServiceDefaults`.
- `Hexalith.Folders.Client` may reference `Hexalith.Folders.Contracts`; it must not reference Server, CLI, MCP, UI, Workers, AppHost, or provider/runtime projects.
- `Hexalith.Folders.Cli` and `Hexalith.Folders.Mcp` compile as adapters over `Hexalith.Folders.Client`, not as independent business-logic surfaces.
- `Hexalith.Folders.UI` is read-only and client-backed; it must not reference core domain behavior directly.
- `Hexalith.Folders.Workers` owns future provider, Git, working-copy, reconciliation, and rate-limit side effects; scaffold placeholders must not contact external systems.
- `Hexalith.Folders.AppHost`, `Hexalith.Folders.Aspire`, and `Hexalith.Folders.ServiceDefaults` may provide local hosting/build infrastructure, but `dotnet restore` and `dotnet build` must not require running Aspire, Dapr, Keycloak, Redis, provider credentials, tenant data, or production secrets.
- Test projects reference their matching production project and shared testing helpers only where needed; integration tests remain compile-safe placeholders until later topology stories wire runtime behavior.

### Dependency Direction

- `Contracts` is contract-only and behavior-free.
- Core `Hexalith.Folders` depends on `Contracts`, not on transport projects.
- `Server` hosts future REST and EventStore `/process` and `/project` surfaces.
- `Client` is the canonical SDK surface and should remain contract-centered.
- `Cli` and `Mcp` are future adapters over `Client`.
- `UI` is read-only and client-backed.
- `Workers` handles future event-driven process managers.
- `Testing` provides reusable fakes/builders/conformance helpers.
- Tests reference their matching production project and shared testing helpers only where needed.

### Build And Package Constraints

- Target framework is `net10.0` through shared root configuration.
- Use central package management in `Directory.Packages.props`; package references in project files should omit versions.
- Root props must detect sibling root-level submodules with paths that work when `Hexalith.Folders`, `Hexalith.Tenants`, and `Hexalith.EventStore` are checked out side by side under the application repository.
- Build must not require provider credentials, tenant seed data, a running Dapr sidecar, Keycloak, Redis, Aspire dashboard, GitHub, Forgejo, or production secret stores.
- If a project needs an executable entry point for scaffold compilation, keep it minimal and non-operative. Use placeholders that fail closed at runtime rather than contacting external systems.

### Existing Reference Files To Reuse

- `Hexalith.Tenants/Directory.Build.props` for `net10.0`, nullable, warnings-as-errors, package metadata, and sibling EventStore path detection.
- `Hexalith.Tenants/Directory.Packages.props` for central package management and current Hexalith ecosystem package grouping.
- `Hexalith.Tenants/global.json` for SDK roll-forward convention.
- `Hexalith.Tenants/Hexalith.Tenants.slnx` for solution format.
- `Hexalith.Tenants/src/Hexalith.Tenants.*/*.csproj` for Contracts, core, Server, Client, AppHost, Aspire, ServiceDefaults, and Testing project patterns.
- `Hexalith.EventStore/src/Hexalith.EventStore.Admin.Cli/Hexalith.EventStore.Admin.Cli.csproj` for CLI packaging pattern.
- `Hexalith.EventStore/src/Hexalith.EventStore.Admin.Mcp/Hexalith.EventStore.Admin.Mcp.csproj` for MCP host packaging pattern.
- `Hexalith.EventStore/src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj` for Blazor UI host conventions.

### Latest Technical Notes

- .NET 10 uses the `net10.0` target framework moniker in SDK-style projects; keep that as the scaffold target.
- `CommunityToolkit.Aspire.Hosting.Dapr` 13.0.0 is available on NuGet and targets .NET 8 or higher, so it is compatible with a .NET 10 scaffold when pinned centrally.
- Do not upgrade architecture-pinned versions during this story unless the scaffold cannot build. If a version change is required for compilation, record the exact reason in completion notes and keep the change in central package management.

### Testing Guidance

- Add at least one scaffold smoke test that asserts the solution contains the expected project names and that key project references do not point in the wrong direction.
- Prefer parsing `.slnx` and `.csproj` metadata for inventory and reference-graph checks so the tests fail when the actual scaffold drifts from the story contract.
- Keep integration tests compile-only or skipped with an explicit reason until the stories that wire Dapr, EventStore, Tenants, and provider adapters exist.
- The verification command for this story is `dotnet build Hexalith.Folders.slnx` from the repository root.

### Security And Data Handling

- No secrets, provider tokens, remote URLs with embedded credentials, tenant data, file contents, generated context payloads, or production Dapr policies should be committed by this scaffold.
- Placeholder appsettings files, if required, must contain only non-sensitive local development defaults and must not imply production readiness.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 1.1: Establish a consumer-buildable module scaffold`
- `_bmad-output/planning-artifacts/architecture.md#Selected Starter: Hexalith.Tenants Project Structure (baseline) + Hexalith.EventStore.Admin.* Surfaces (Cli/Mcp/UI)`
- `_bmad-output/planning-artifacts/architecture.md#Recommended Project Layout`
- `_bmad-output/planning-artifacts/architecture.md#Architectural Boundaries`
- `_bmad-output/planning-artifacts/architecture.md#File Organization Patterns`
- `_bmad-output/planning-artifacts/architecture.md#Implementation Handoff`
- `_bmad-output/planning-artifacts/prd.md#MVP Contract Summary`
- `_bmad-output/planning-artifacts/prd.md#Contract and Quality Gates`
- `AGENTS.md#Git Submodules`
- `Hexalith.Tenants/Directory.Build.props`
- `Hexalith.Tenants/Directory.Packages.props`
- `Hexalith.EventStore/src/Hexalith.EventStore.Admin.Cli/Hexalith.EventStore.Admin.Cli.csproj`
- `Hexalith.EventStore/src/Hexalith.EventStore.Admin.Mcp/Hexalith.EventStore.Admin.Mcp.csproj`
- `Hexalith.EventStore/src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj`
- Microsoft Learn: https://learn.microsoft.com/dotnet/standard/frameworks
- NuGet: https://www.nuget.org/packages/CommunityToolkit.Aspire.Hosting.Dapr/

## Project Structure Notes

- Root `src`, `tests`, `samples`, `docs/adrs`, `docs/exit-criteria`, and `tests/fixtures` paths do not exist yet in this repository and are expected outputs of the implementation story.
- Root-level sibling modules already exist in the workspace: `Hexalith.Tenants`, `Hexalith.EventStore`, and `Hexalith.FrontComposer`. This story should reuse their patterns but should not modify those sibling modules.
- There is no discoverable `project-context.md`; use planning artifacts and sibling module files as the implementation context.

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-10 | Created ready-for-dev story through `bmad-create-story` workflow. | Codex |
| 2026-05-10 | Party-mode review applied scaffold inventory, dependency graph, clean-build, and submodule guard clarifications. | Codex |
| 2026-05-10 | Advanced elicitation pass applied exact-inventory, metadata-driven smoke-test, compile-only placeholder, and deferral guardrails. | Codex |
| 2026-05-10 | Implemented scaffold, smoke tests, fixtures, docs placeholders, and verified restore/build/test. | Codex |

## Party-Mode Review

- Date/time: 2026-05-10T19:20:54.5388562+02:00
- Selected story key: `1-1-establish-a-consumer-buildable-module-scaffold`
- Command/skill invocation used: `/bmad-party-mode 1-1-establish-a-consumer-buildable-module-scaffold; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), John (Product Manager)
- Findings summary:
  - Tighten AC1 with an explicit canonical solution/project inventory.
  - Tighten AC2 with an allowed project-reference graph and smoke-test expectations.
  - Make consumer-buildable proof concrete with `dotnet restore` and `dotnet build` from a clean checkout using only root-level submodules.
  - Keep load-test, OpenAPI, endpoint, provider, CLI/MCP behavior, UI diagnostics, lifecycle, and idempotency decisions deferred to later stories.
- Changes applied:
  - Updated acceptance criteria for inventory, central package management, allowed references, clean restore/build proof, and runtime/service independence.
  - Added solution inventory and allowed reference graph sections.
  - Added smoke-test and verification tasks for solution membership, target framework/root config, reference direction, central package management, and root-only submodule handling.
- Findings deferred:
  - Do not decide OpenAPI, endpoint shape, provider adapters, MCP tool schema, CLI verbs, lifecycle modeling, or idempotency policy in this scaffold story.
  - Do not add a runnable load-test project unless it compiles with zero external infrastructure and zero behavioral assumptions.
  - Keep integration tests and fixtures compile-safe placeholders without implying EventStore, Tenants, provider, or lifecycle runtime behavior.
- Final recommendation: ready-for-dev

## Advanced Elicitation

- Date/time: 2026-05-10T19:54:57Z
- Selected story key: `1-1-establish-a-consumer-buildable-module-scaffold`
- Command/skill invocation used: `/bmad-advanced-elicitation 1-1-establish-a-consumer-buildable-module-scaffold`
- Batch 1 method names:
  - Red Team vs Blue Team
  - Security Audit Personas
  - Failure Mode Analysis
  - Pre-mortem Analysis
  - Self-Consistency Validation
- Reshuffled Batch 2 method names:
  - Architecture Decision Records
  - Comparative Analysis Matrix
  - Challenge from Critical Perspective
  - First Principles Analysis
  - Critique and Refine
- Findings summary:
  - The story needed a firmer distinction between required scaffold inventory and tempting extra buildable surfaces.
  - The reference-graph and inventory proof needed to be driven by actual solution/project metadata so tests catch scaffold drift.
  - Placeholder and optional-hosting areas needed clearer compile-only rules to avoid accidental runtime, provider, topology, or generated-client scope.
  - Completion evidence needed to call out intentionally empty projects, omitted optional tests, and SDK/package exceptions.
- Changes applied:
  - Added AC6 for exact inventory, forbidden references, package-version, and target-framework drift checks.
  - Added tasks to keep source inventory exact, avoid unnecessary AppHost/Aspire/ServiceDefaults test projects, parse project metadata for smoke tests, and verify package references omit inline versions.
  - Added scope and testing notes that defer generated clients, OpenAPI documents, provider configuration, runnable topology, CLI/MCP behavior, UI pages, and lifecycle models.
  - Expanded completion-note expectations for omitted optional tests, SDK/package deviations, and non-runnable placeholder directories.
- Findings deferred:
  - Do not decide concrete OpenAPI generation, CLI verb names, MCP tool schemas, UI diagnostic routes, provider configuration shape, lifecycle state names, or runnable deployment topology in this scaffold story.
  - Do not require dedicated AppHost, Aspire, or ServiceDefaults test projects unless the implementation discovers a compile-only reason and records it.
- Final recommendation: ready-for-dev

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- `dotnet build .\tests\Hexalith.Folders.Testing.Tests\Hexalith.Folders.Testing.Tests.csproj --no-restore -v:minimal` failed before scaffold completion because the test project had no restored assets.
- `dotnet restore .\Hexalith.Folders.slnx` passed.
- `dotnet build .\Hexalith.Folders.slnx --no-restore` passed with 0 warnings and 0 errors after making `Hexalith.Folders.Server` a minimal executable Web SDK host for AppHost compatibility.
- `dotnet test .\Hexalith.Folders.slnx --no-build` passed: 13 tests across 11 test assemblies.
- Final `dotnet restore .\Hexalith.Folders.slnx` passed and final `dotnet build .\Hexalith.Folders.slnx --no-restore` passed with 0 warnings and 0 errors.

### Completion Notes List

The scaffold delivery for this story spans multiple commits on `main` rather than a single one. The commit decomposition was retroactively captured during code review (2026-05-11):

- `eb52d15` "feat: Add initial scaffolding for Hexalith.Folders solution" — initial scaffold landing: `Directory.Build.props`, `Directory.Packages.props`, `Hexalith.Folders.slnx`, `global.json`, the 12 `src/Hexalith.Folders.*` project skeletons, the `samples/Hexalith.Folders.Sample` project, the `tests/Hexalith.Folders.Testing.Tests` project (with `ScaffoldContractTests.cs`), and the predev preflight artifacts. At this commit alone, `dotnet restore Hexalith.Folders.slnx` cannot succeed because the slnx references several test projects that land in later commits.
- `a89706b` "Commit pending workspace changes" — added `src/Hexalith.Folders.Server/Program.cs` and flipped Server to `Microsoft.NET.Sdk.Web`, plus filled in the mirror test projects required by the slnx so restore/build/test succeed end-to-end. AppHost's `Projects.Hexalith_Folders_Server` reference became valid at this commit.
- `ffe6452` "chore: implement normative fixtures with ownership metadata and parseability tests" — added `tests/fixtures/{audit-leakage-corpus.json, idempotency-encoding-corpus.json, parity-contract.schema.json, previous-spine.yaml}` and the docs templates referenced by the story's task list.
- `ff7ab6a` "chore: add initial test framework setup with data factories, request headers, and polling utilities" — added the shared testing factories under `src/Hexalith.Folders.Testing` and the corresponding tests under `tests/Hexalith.Folders.Testing.Tests` that the story's testing-guidance section assumes.
- `6098b45` "Update sprint status and add exit criteria documents" — added the `tests/load/README.md` and `tests/tools/parity-oracle-generator/README.md` non-runnable placeholders called out in the task list.

Implementation specifics:

- Adapted root build configuration to `Hexalith.Folders` with `net10.0`, nullable, implicit usings, warnings-as-errors, `LangVersion=latest`, central package management, package metadata, and root-level sibling path detection for `Hexalith.EventStore` and `Hexalith.Tenants`.
- The pack policy was set during code review (2026-05-11) to global `IsPackable=false` with explicit opt-ins on `Hexalith.Folders.Contracts`, `Hexalith.Folders` (core), `Hexalith.Folders.Client`, `Hexalith.Folders.Testing`, and `Hexalith.Folders.ServiceDefaults`. Host/adapter/sample projects (`AppHost`, `Aspire`, `Server`, `UI`, `Mcp`, `Workers`, `Sample`) do not produce NuGet packages. `Hexalith.Folders.Cli` packs as a .NET global tool via `PackAsTool=true`.
- `Hexalith.Folders.Server` and `Hexalith.Folders.UI` both opt into container publishing with intentionally distinct `ContainerRepository` values (`folders` and `folders-ui`) so the read-only console can be deployed and scaled independently of the domain-service host.
- `Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs` enforces the canonical inventory, allowed dependency direction across all 24 projects, explicit forbidden references (Contracts→behavior, Client→adapter/host), root-owned target-framework / nullable / langversion / warnings-as-errors (with a drift detector that fails the build if any csproj overrides them locally), central package management, no inline package versions, required root configuration files, NuGet-source hygiene, and submodule policy.
- The load-test and parity-oracle-generator areas are non-runnable placeholder directories because runnable capacity/load infrastructure and the parity oracle generator are deferred to later stories.
- No nested submodule initialization or recursive submodule command was used or required. Restore/build/test completed without provider credentials, tenant data, production secrets, running Aspire, Dapr, Keycloak, Redis, GitHub, or Forgejo.
- No SDK or central package deviations were required.

### File List

- `Directory.Build.props`
- `Directory.Packages.props`
- `Hexalith.Folders.slnx`
- `global.json`
- `docs/adrs/0000-template.md`
- `docs/exit-criteria/_template.md`
- `samples/Hexalith.Folders.Sample/Hexalith.Folders.Sample.csproj`
- `samples/Hexalith.Folders.Sample/Program.cs`
- `samples/Hexalith.Folders.Sample.Tests/Hexalith.Folders.Sample.Tests.csproj`
- `samples/Hexalith.Folders.Sample.Tests/SampleSmokeTests.cs`
- `src/Hexalith.Folders/Hexalith.Folders.csproj`
- `src/Hexalith.Folders/FoldersModule.cs`
- `src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj`
- `src/Hexalith.Folders.AppHost/Program.cs`
- `src/Hexalith.Folders.Aspire/FoldersAspireModule.cs`
- `src/Hexalith.Folders.Aspire/Hexalith.Folders.Aspire.csproj`
- `src/Hexalith.Folders.Client/FoldersClientModule.cs`
- `src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj`
- `src/Hexalith.Folders.Cli/Hexalith.Folders.Cli.csproj`
- `src/Hexalith.Folders.Cli/Program.cs`
- `src/Hexalith.Folders.Contracts/FoldersContractMetadata.cs`
- `src/Hexalith.Folders.Contracts/Hexalith.Folders.Contracts.csproj`
- `src/Hexalith.Folders.Mcp/Hexalith.Folders.Mcp.csproj`
- `src/Hexalith.Folders.Mcp/Program.cs`
- `src/Hexalith.Folders.Server/FoldersServerModule.cs`
- `src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj`
- `src/Hexalith.Folders.Server/Program.cs`
- `src/Hexalith.Folders.ServiceDefaults/Extensions.cs`
- `src/Hexalith.Folders.ServiceDefaults/Hexalith.Folders.ServiceDefaults.csproj`
- `src/Hexalith.Folders.Testing/FoldersTestingModule.cs`
- `src/Hexalith.Folders.Testing/Hexalith.Folders.Testing.csproj`
- `src/Hexalith.Folders.UI/Hexalith.Folders.UI.csproj`
- `src/Hexalith.Folders.UI/Program.cs`
- `src/Hexalith.Folders.Workers/FoldersWorkersModule.cs`
- `src/Hexalith.Folders.Workers/Hexalith.Folders.Workers.csproj`
- `tests/Hexalith.Folders.Client.Tests/ClientSmokeTests.cs`
- `tests/Hexalith.Folders.Client.Tests/Hexalith.Folders.Client.Tests.csproj`
- `tests/Hexalith.Folders.Cli.Tests/CliSmokeTests.cs`
- `tests/Hexalith.Folders.Cli.Tests/Hexalith.Folders.Cli.Tests.csproj`
- `tests/Hexalith.Folders.Contracts.Tests/ContractsSmokeTests.cs`
- `tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj`
- `tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj`
- `tests/Hexalith.Folders.IntegrationTests/IntegrationSmokeTests.cs`
- `tests/Hexalith.Folders.Mcp.Tests/Hexalith.Folders.Mcp.Tests.csproj`
- `tests/Hexalith.Folders.Mcp.Tests/McpSmokeTests.cs`
- `tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj`
- `tests/Hexalith.Folders.Server.Tests/ServerSmokeTests.cs`
- `tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj`
- `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs`
- `tests/Hexalith.Folders.Tests/FoldersModuleSmokeTests.cs`
- `tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj`
- `tests/Hexalith.Folders.UI.Tests/Hexalith.Folders.UI.Tests.csproj`
- `tests/Hexalith.Folders.UI.Tests/UiSmokeTests.cs`
- `tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj`
- `tests/Hexalith.Folders.Workers.Tests/WorkersSmokeTests.cs`
- `tests/fixtures/audit-leakage-corpus.json`
- `tests/fixtures/idempotency-encoding-corpus.json`
- `tests/fixtures/parity-contract.schema.json`
- `tests/fixtures/previous-spine.yaml`
- `tests/load/README.md`
- `tests/tools/parity-oracle-generator/README.md`

## Review Findings

### Review Findings — 2026-05-11 (code-review against commit `eb52d15`)

Layers run: Blind Hunter (adversarial), Edge Case Hunter, Acceptance Auditor. All three layers completed.

#### Decision-Needed (resolved 2026-05-11)

- [x] [Review][Decision] **Spec-vs-delivery gap: 10 csproj files and several placeholders claimed by this story were not delivered in commit `eb52d15`** — **Resolved:** amend story scope to span the multi-commit window (`eb52d15` + `a89706b` + `ffe6452` + `ff7ab6a` + `6098b45`); Completion Notes rewritten to enumerate each commit's contribution.
- [x] [Review][Decision] **`Hexalith.Folders.Server` is not buildable as an Aspire project resource in `eb52d15`** — **Resolved:** bundled with previous decision; Server's `Program.cs` and `Microsoft.NET.Sdk.Web` flip recorded as landing in `a89706b`.
- [x] [Review][Decision] **Container/publish config drift between Server and UI** — **Resolved:** documented as intentional asymmetry; both projects ship containers with distinct `ContainerRepository` values (`folders`, `folders-ui`) so they can be deployed and scaled independently. Comment added in `src/Hexalith.Folders.UI/Hexalith.Folders.UI.csproj`.
- [x] [Review][Decision] **Pack policy is implicit and produces unintended NuGet packages** — **Resolved:** flipped `Directory.Build.props` default to `IsPackable=false`. Opt-ins added on `Hexalith.Folders.Contracts`, `Hexalith.Folders` (core), `Hexalith.Folders.Client`, `Hexalith.Folders.Testing`, and `Hexalith.Folders.ServiceDefaults`.
- [x] [Review][Decision] **Completion Notes do not flag the omissions** — **Resolved:** rewrote the `### Completion Notes List` to enumerate the multi-commit delivery and the pack-policy / container decisions captured during this review.

#### Patch (applied 2026-05-11)

- [x] [Review][Patch] **UI minimal-API endpoint binds `moduleName` from query string, not DI** [`src/Hexalith.Folders.UI/Program.cs:6`] — Removed the dead `AddSingleton(_ => FoldersClientModule.Name)` registration and inlined `FoldersClientModule.Name` directly in the endpoint expression. `GET /` now returns the scaffold marker string without query-string dependence.
- [x] [Review][Patch] **`ScaffoldContractTests.ProjectReferencesFollowAllowedDependencyDirection` omits Aspire, ServiceDefaults, Testing, and `*.Tests` projects** [`tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs`] — Extended `ProjectReferencesFollowAllowedDependencyDirection` to cover all 24 projects in `ExpectedSolutionProjects`. Added a new `ForbiddenReferencesAreNotIntroduced` test that asserts explicit forbidden-reference rules for Contracts, Client, and the CLI/MCP/UI adapters.
- [x] [Review][Patch] **Per-project TFM drift is not tested (Task line 67 mandate)** [`tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs`] — Added `ProjectsDoNotOverrideRootBuildConfigurationLocally` test that walks every project in `ExpectedSolutionProjects` and fails if any locally defines `<TargetFramework>`, `<TargetFrameworks>`, `<Nullable>`, `<ImplicitUsings>`, `<LangVersion>`, or `<TreatWarningsAsErrors>`.
- [x] [Review][Patch] **`Hexalith.Folders.Testing.csproj` is missing `<InternalsVisibleTo Include="Hexalith.Folders.Testing.Tests" />`** [`src/Hexalith.Folders.Testing/Hexalith.Folders.Testing.csproj`] — Added the missing `<InternalsVisibleTo>` entry. Pattern is now consistent across all src projects.
- [x] [Review][Patch] **Completion Notes rewritten** [`### Completion Notes List`] — Replaced the original notes with a multi-commit decomposition + pack-policy + container-policy summary. (Resolution patch for decision items above.)
- [x] [Review][Patch] **UI/Server container asymmetry documented** [`src/Hexalith.Folders.UI/Hexalith.Folders.UI.csproj`] — Added an explanatory comment next to the container properties. (Resolution patch for the container-drift decision.)
- [x] [Review][Patch] **Pack policy flipped to opt-in** [`Directory.Build.props`, `src/Hexalith.Folders.Contracts/*.csproj`, `src/Hexalith.Folders/*.csproj`, `src/Hexalith.Folders.Client/*.csproj`, `src/Hexalith.Folders.Testing/*.csproj`, `src/Hexalith.Folders.ServiceDefaults/*.csproj`] — Default `IsPackable=false` at root; explicit `IsPackable=true` opt-ins on the five library projects. (Resolution patch for the pack-policy decision.)

#### Deferred

- [x] [Review][Defer] `<InternalsVisibleTo>` in src csprojs references test assemblies that didn't exist at `eb52d15` [`src/Hexalith.Folders.*/*.csproj`] — deferred, self-resolves at HEAD as test projects materialize.
- [x] [Review][Defer] `Directory.Build.props` declares `HexalithEventStoreRoot` / `HexalithTenantsRoot` MSBuild properties that no csproj or targets file currently consumes [`Directory.Build.props:23-26`] — deferred, intended for future-story consumption.
- [x] [Review][Defer] Preflight gate `result: "fail"` recorded in `predev-preflight-2026-05-10T200403Z.json` and latest pointer [_bmad-output/process-notes/predev-preflight-latest.json] — deferred, process concern outside code-review scope.
- [x] [Review][Defer] `.gitmodules` declares `Hexalith.Memories` but `Directory.Build.props` has no detector for it [`Directory.Build.props:3-7`] — deferred, dormant until a downstream story consumes Memories.
- [x] [Review][Defer] No `Directory.Build.targets` adapted from Hexalith.Tenants — deferred, acceptable deviation; revisit when stories require SourceLink or pack-time MSBuild logic.

#### Dismissed (6)

- AppHost `Program.cs` "missing usings" — Aspire.AppHost.Sdk supplies implicit usings; build is verified at HEAD.
- AppHost lacks local `<TargetFramework>` override — inheritance from `Directory.Build.props` is the intended pattern.
- `FoldersAspireModule` "over-engineered" — subjective placeholder concern.
- Story 1.1 status flip in sprint-status — subsumed by the Decision-Needed scope item.
- `TreatWarningsAsErrors=true` may trip Aspire generator — speculative; build is verified at HEAD.
- `<ContinuousIntegrationBuild>` conditional missing from `eb52d15` — added in later commit, present at HEAD.

### Review Findings — 2026-05-12 (code-review of story 1.1 scaffold window)

Layers run: Blind Hunter (adversarial), Edge Case Hunter, Acceptance Auditor. All three layers completed.

#### Decision-Needed

- [x] [Review][Decision] **Scaffold review scope includes later contract/idempotency/parity policy tests** — **Resolved 2026-05-12:** keep these later policy tests inside the accepted multi-commit story 1.1 review window. Story 1.1 says fixtures are minimal valid placeholders and defers Contract Spine, parity oracle, idempotency helpers, CLI/MCP behavior, and generated-client semantics to later stories. `tests/Hexalith.Folders.Testing.Tests/ContractRulesArtifactTests.cs:9` enumerates MVP and Phase 1 operation IDs; `tests/Hexalith.Folders.Testing.Tests/ContractRulesArtifactTests.cs:121` requires idempotency/read-consistency rules; `tests/Hexalith.Folders.Testing.Tests/ContractRulesArtifactTests.cs:165` pins parity schema behavior including CLI/MCP failure categories.
- [x] [Review][Decision] **Exit-criteria tests require concrete later-story decision artifacts** — **Resolved 2026-05-12:** keep these later decision artifacts inside the accepted multi-commit story 1.1 review window. Story 1.1 only requires `docs/exit-criteria/_template.md` and defers lifecycle/idempotency/Contract Spine decisions; project context says Phase 1 Contract Spine work is blocked until C3/C4 are resolved. `tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs:9` requires `c3-retention.md`, `c4-input-limits.md`, `s2-oidc-validation.md`, and `c6-transition-matrix-mapping.md`; `tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs:119` pins OIDC validation settings; `tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs:147` pins lifecycle state/event vocabulary.

#### Patch

- [ ] [Review][Patch] **`TestAuthorizationContext.TenantClaimJson` does not JSON-escape tenant IDs** [`src/Hexalith.Folders.Testing/Factories/TestAuthorizationContext.cs:8`]
- [ ] [Review][Patch] **`TestFolderContext` stream-name helpers accept delimiter characters in IDs** [`src/Hexalith.Folders.Testing/Factories/TestFolderContext.cs:11`]
- [ ] [Review][Patch] **`TestRequestHeaders.FromFolderContext` forwards CR/LF or control characters into header values** [`src/Hexalith.Folders.Testing/Http/TestRequestHeaders.cs:19`]
- [ ] [Review][Patch] **`Eventually.UntilAsync` can hang beyond timeout when a probe ignores cancellation** [`src/Hexalith.Folders.Testing/Polling/Eventually.cs:28`]
- [ ] [Review][Patch] **`ScaffoldContractTests` recursively enumerates the whole repository before filtering scaffold areas** [`tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs:42`]
- [ ] [Review][Patch] **Recursive submodule policy test treats broad nearby wording as an exemption** [`tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs:380`]
- [ ] [Review][Patch] **Fixture leakage checks miss `client_secret`-style OAuth secret markers** [`tests/Hexalith.Folders.Testing.Tests/FixtureContractTests.cs:115`]

### Review Findings — 2026-05-12 round 2 (exit-criteria tests and docs hardening)

Layers run: Blind Hunter (adversarial), Edge Case Hunter, Acceptance Auditor. All three layers completed.
Diff reviewed: uncommitted changes to `docs/exit-criteria/c3-retention.md`, `c4-input-limits.md`, `c6-transition-matrix-mapping.md`, and `tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs`.
Dismissed: 2 findings (C4 secondary-table false failure risk — single table in doc, tests pass; vacuous mutual-exclusion observation — correct behavior for current data).

#### Decision-Needed

- [x] [Review][Decision] **`\bTODO\b` in `PlaceholderRegexes` is zero-tolerance with no prose escape hatch** — **Resolved 2026-05-12:** keep zero-tolerance; "TODO" is prohibited from exit-criteria prose; authors must use "Deferred" or "Pending" instead. — `PlaceholderRegexes` includes `new(@"\bTODO\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)`. Any exit-criteria doc containing the word "TODO" in prose (e.g., a "Deferred TODO items" list or a deferred section header) would fail the artifact cleanliness test with a misleading diagnostic. Decide: (a) keep zero-tolerance and prohibit "TODO" from all exit-criteria prose — authors must use "Deferred" or "Pending"; (b) remove `TODO` from the placeholder denylist for exit-criteria docs and rely on code/fixture-file checks only; (c) rename the array to `CodeArtifactPlaceholderRegexes` and apply it only to code/fixture files, not to documentation. [`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs`]

#### Patch

- [x] [Review][Patch] **`ExtractDecisionTableRows` resets `sawSeparatorOnCurrentTable` on any non-pipe line, silently dropping all body rows after a blank line mid-table** [`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs`, `ExtractDecisionTableRows`] — **Applied 2026-05-12:** blank lines no longer reset the flag; only non-blank non-pipe lines do
- [x] [Review][Patch] **`ExtractDecisionTableRows` separator regex `^\|[\s\-:|]+\|\s*$` is too broad — a bare-hyphen data row (e.g., `| - | - |`) is silently consumed as a separator** [`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs`, `ExtractDecisionTableRows`] — **Applied 2026-05-12:** regex now requires 3+ consecutive hyphens per cell (`^\|(?:\s*:?-{3,}:?\s*\|)+\s*$`)
- [x] [Review][Patch] **`IsEmDashOrHyphen` accepts plain ASCII hyphen (`-`) as a blank-cell marker, weakening the em-dash sentinel** [`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs`, `IsEmDashOrHyphen`] — **Applied 2026-05-12:** removed ASCII hyphen `-` from equivalence set; only `—` and `–` accepted
- [x] [Review][Patch] **`ParseFrontMatterValue` searches the entire file — any `key: value` prose line in the body can falsely satisfy the front-matter check** [`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs`, `ParseFrontMatterValue`] — **Applied 2026-05-12:** search now constrained to the block before the first `## ` heading
- [x] [Review][Patch] **Duplicate sentence in `c6-transition-matrix-mapping.md`** — **Dismissed 2026-05-12:** confirmed false positive; targeted git diff shows no duplicate sentence in file
- [x] [Review][Patch] **`C3CoversTheSixMandatedDataClasses` uses document-level substring match — new prose paragraph added in this diff names all six classes, so a future table-row removal would still pass** [`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs`, `C3CoversTheSixMandatedDataClasses`] — **Applied 2026-05-12:** test now uses `ExtractDecisionTableRows` to check table rows only
- [x] [Review][Patch] **`PlaceholderRegexes[1]` pattern `\bT\.B\.D\.?` is missing the closing `\b` word-boundary anchor** [`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs`] — **Applied 2026-05-12:** added `\b` at end: `\bT\.B\.D\.?\b`
- [x] [Review][Patch] **`ProductionUrl()` regex incorrectly flags `.invalid` URLs with fragment identifiers (e.g., `https://auth.invalid#section`) as production URLs** [`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs`, `ProductionUrl`] — **Applied 2026-05-12:** added `#?[` to allowed-after set
- [x] [Review][Patch] **`S2OidcArtifactPinsFrozenJwtBearerSettings` and `S2OidcArtifactDocumentsAuthoritativeClaimProvenanceAndSyntheticPlaceholders` call `File.ReadAllText` without a prior `File.Exists` check** [`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs`] — **Applied 2026-05-12:** added `File.Exists` + `ShouldBeTrue` guard before each `ReadAllText`

#### Deferred

- [x] [Review][Defer] `C6MappingArtifactMirrorsArchitectureVocabularyBidirectionally` checks backtick-wrapped event names in architecture.md — all events do appear backtick-wrapped somewhere in the file so tests pass; the canonical transition table uses bare cell names but the check still provides vocabulary coverage [`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs`] — deferred, design nuance; tests pass
- [x] [Review][Defer] `C3AndC4RowsDeclareProvenanceApprovalConsumerAndReviewDate` now dynamically derives expected date from front-matter `last reviewed` — changes failure semantics from "row must have a concrete date" to "row must match front-matter"; intentional improvement but undocumented scope change [`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs`] — deferred, intentional design improvement
- [x] [Review][Defer] `"diff --git"` in `SecretSubstringDenylist` alongside actual credential patterns produces confusing "must not contain credential material" diagnostic — intent is valid (docs should not embed patch content) but classification is misleading [`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs`] — deferred, cosmetic; intent is correct
- [x] [Review][Defer] `RepositoryRoot` `const int MaxAncestors = 12` is a magic number — could fail on deeply nested CI build paths with an `InvalidOperationException` rather than a clean test failure; error message is improved vs. the old unbounded loop [`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs`, `RepositoryRoot`] — deferred, pre-existing design; 12 is sufficient for known paths
- [x] [Review][Defer] S2 OIDC test split — `.invalid` placeholder check and authoritative claim checks moved to a new test `S2OidcArtifactDocumentsAuthoritativeClaimProvenanceAndSyntheticPlaceholders`; half the original single-test OIDC contract is now invisible unless both tests are read together — deferred, structural coupling concern; both tests pass and cover the full contract
