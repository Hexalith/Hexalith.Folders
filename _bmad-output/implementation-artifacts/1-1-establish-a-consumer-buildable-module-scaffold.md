# Story 1.1: Establish a consumer-buildable module scaffold

Status: ready-for-dev

Created: 2026-05-10

## Story

As a platform engineer and downstream consumer,
I want the Hexalith.Folders solution scaffold to build with the approved project layout,
so that consumers and later stories have a stable, convention-compliant module baseline.

## Acceptance Criteria

1. Given an empty Hexalith.Folders module repository, when the scaffold is created, then `Hexalith.Folders.slnx` contains the expected `src`, `tests`, and `samples` projects.
2. Project references follow the architecture dependency direction and every project targets .NET 10 through shared root configuration.
3. `dotnet build Hexalith.Folders.slnx` succeeds for the scaffold without requiring provider credentials, tenant data, production secrets, or initialized nested submodules.
4. The scaffold reuses sibling-module conventions from `Hexalith.Tenants` and `Hexalith.EventStore` instead of introducing a generic or independent project template.
5. Root-level submodules may be used as references, but nested submodules must not be initialized, updated, or required by this story.

## Tasks / Subtasks

- [ ] Create root solution and shared build files. (AC: 1, 2, 4)
  - [ ] Create `Hexalith.Folders.slnx` using the sibling-module `.slnx` convention.
  - [ ] Add `global.json` aligned with `Hexalith.Tenants/global.json` unless a newer installed SDK patch is already required by this repository.
  - [ ] Add `Directory.Build.props` by adapting the `Hexalith.Tenants` file to `Hexalith.Folders`, including `net10.0`, nullable, implicit usings, warnings-as-errors, `LangVersion=latest`, package metadata, and root-level sibling path detection for `Hexalith.EventStore` and `Hexalith.Tenants`.
  - [ ] Add `Directory.Packages.props` with central package management. Start from `Hexalith.Tenants/Directory.Packages.props`; do not invent independent version policy.
- [ ] Create the expected source projects. (AC: 1, 2, 4)
  - [ ] `src/Hexalith.Folders.Contracts`
  - [ ] `src/Hexalith.Folders`
  - [ ] `src/Hexalith.Folders.Server`
  - [ ] `src/Hexalith.Folders.Client`
  - [ ] `src/Hexalith.Folders.Cli`
  - [ ] `src/Hexalith.Folders.Mcp`
  - [ ] `src/Hexalith.Folders.UI`
  - [ ] `src/Hexalith.Folders.Workers`
  - [ ] `src/Hexalith.Folders.Aspire`
  - [ ] `src/Hexalith.Folders.AppHost`
  - [ ] `src/Hexalith.Folders.ServiceDefaults`
  - [ ] `src/Hexalith.Folders.Testing`
- [ ] Create the expected test and sample projects. (AC: 1, 3)
  - [ ] Mirror `src/` with `tests/Hexalith.Folders.*.Tests` projects, including `Contracts`, core `Folders`, `Server`, `Client`, `Cli`, `Mcp`, `UI`, `Workers`, `Testing`, and `IntegrationTests`.
  - [ ] Create `samples/Hexalith.Folders.Sample` and `samples/Hexalith.Folders.Sample.Tests`.
  - [ ] Add minimal scaffolding smoke tests that can run without Dapr, provider credentials, tenant seed data, or initialized nested submodules.
- [ ] Wire project references in dependency direction only. (AC: 2, 4)
  - [ ] `Contracts` owns DTO and contract placeholders only. It must not reference `Hexalith.EventStore`, `Hexalith.Tenants`, Server, Client, CLI, MCP, UI, Workers, or core domain behavior.
  - [ ] `Hexalith.Folders` references `Contracts` and only the sibling Hexalith infrastructure needed for domain compilation.
  - [ ] `Server` references core domain, `Contracts`, and `ServiceDefaults`; it is the future REST and EventStore domain-service host.
  - [ ] `Client` references `Contracts` only unless a compile-only subscription helper requires the explicit Tenants client pattern.
  - [ ] `Cli` and `Mcp` must compile as adapters and must not add independent business logic.
  - [ ] `UI` must compile as a read-only console shell and reference `Client`, not core domain.
  - [ ] `Workers` owns future process managers and reconcilers; keep external provider behavior as placeholders only in this story.
  - [ ] `AppHost` wires local Aspire topology only enough to compile; it must not require running Keycloak, Dapr, Redis, provider credentials, or tenant data for `dotnet build`.
- [ ] Add scaffold-only placeholders and normative directories. (AC: 1, 3)
  - [ ] Add `docs/exit-criteria/_template.md` and `docs/adrs/0000-template.md`.
  - [ ] Add `tests/fixtures/audit-leakage-corpus.json`, `tests/fixtures/parity-contract.schema.json`, `tests/fixtures/previous-spine.yaml`, and `tests/fixtures/idempotency-encoding-corpus.json` as minimal valid placeholders.
  - [ ] Add `tests/load/Hexalith.Folders.LoadTests.csproj` only if it can compile without load infrastructure side effects; otherwise add a placeholder directory note and defer the runnable load project to its capacity story.
  - [ ] Add `tests/tools/parity-oracle-generator/` placeholder structure without implementing the generator.
- [ ] Verify and document scaffold build behavior. (AC: 2, 3, 5)
  - [ ] Run `dotnet build Hexalith.Folders.slnx`.
  - [ ] Confirm the build does not require secrets, provider credentials, tenant data, production Dapr components, or nested submodule initialization.
  - [ ] Record any intentionally empty projects in completion notes so reviewers can distinguish scaffold placeholders from missing implementation.

## Dev Notes

### Scope Boundaries

- This story creates a buildable module scaffold only. It does not author the OpenAPI Contract Spine, parity oracle, idempotency helpers, provider adapters, lifecycle state machine, REST endpoints, CLI commands, MCP tools, UI diagnostic pages, or production workflows.
- Do not turn placeholder projects into partial implementations. Later stories own contract authoring, provider readiness, workspace lifecycle, parity, audit, and operations console behavior.
- Do not initialize or update nested submodules. The scaffold may reference root-level sibling submodules already present in this repository, especially `Hexalith.Tenants` and `Hexalith.EventStore`, but nested submodule state must not be a build prerequisite.

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

## Dev Agent Record

### Agent Model Used

TBD by dev-story agent

### Debug Log References

### Completion Notes List

### File List
