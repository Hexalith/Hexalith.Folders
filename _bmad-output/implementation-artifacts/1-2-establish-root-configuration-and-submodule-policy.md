# Story 1.2: Establish root configuration and submodule policy

Status: ready-for-dev

Created: 2026-05-10

## Story

As a maintainer,
I want root repository configuration and root-level submodule policy established,
so that builds are reproducible and nested submodules are not initialized accidentally.

## Acceptance Criteria

1. Given the scaffolded repository, when root configuration is verified or added, then `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `nuget.config`, `.editorconfig`, `.gitmodules`, and `Hexalith.Folders.slnx` exist at the repository root.
2. Root configuration follows the Hexalith.Tenants sibling-module conventions for .NET 10, central package management, deterministic package restore, analyzer/build settings, package metadata, and sibling Hexalith module path detection.
3. `.gitmodules`, `AGENTS.md`, `CLAUDE.md`, and setup guidance document root-level submodule initialization only and explicitly forbid recursive nested-submodule initialization unless the user requests nested submodules.
4. `dotnet restore Hexalith.Folders.slnx` and `dotnet build Hexalith.Folders.slnx` succeed without provider credentials, tenant data, production secrets, running Dapr sidecars, or initialized nested submodules.
5. A root-level repository guard test or documentation check proves the submodule policy is discoverable and prevents accidental use of `git submodule update --init --recursive` in setup instructions.

## Tasks / Subtasks

- [ ] Inspect the Story 1.1 scaffold outputs before changing root files. (AC: 1, 2, 4)
  - [ ] Verify whether `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `nuget.config`, `.editorconfig`, `.gitmodules`, and `Hexalith.Folders.slnx` already exist.
  - [ ] Preserve valid Story 1.1 project references and placeholder project structure; this story hardens root configuration rather than rebuilding the scaffold.
  - [ ] Record any missing root files in the dev completion notes before adding them.
- [ ] Harden shared build and restore configuration. (AC: 1, 2, 4)
  - [ ] Adapt `Hexalith.Tenants/Directory.Build.props` to `Hexalith.Folders`, preserving `net10.0`, nullable, implicit usings, warnings-as-errors, `LangVersion=latest`, deterministic build settings, package metadata, and sibling path detection.
  - [ ] Adapt `Hexalith.Tenants/Directory.Packages.props` for central package management; package references in project files must not carry versions unless a project-local version is explicitly justified.
  - [ ] Add or align `global.json` with the .NET 10 SDK roll-forward convention used by Hexalith.Tenants.
  - [ ] Add `nuget.config` only for deterministic restore sources and package mapping needed by this repository; do not add credentials or private feed secrets.
  - [ ] Add or align `.editorconfig` with sibling-module formatting and analyzer conventions.
- [ ] Confirm solution and root configuration agree. (AC: 1, 2, 4)
  - [ ] Verify `Hexalith.Folders.slnx` includes every scaffold project produced by Story 1.1 and no non-root nested submodule project paths.
  - [ ] Confirm root props do not require running Aspire, Dapr, Keycloak, Redis, GitHub, Forgejo, or tenant seed data at build time.
  - [ ] Ensure package metadata points to Hexalith.Folders and does not retain copied Hexalith.Tenants package IDs, repository URLs, descriptions, or tags where they would be misleading.
- [ ] Establish root-level submodule policy. (AC: 3, 5)
  - [ ] Keep `.gitmodules` limited to root-level entries such as `Hexalith.AI.Tools`, `Hexalith.EventStore`, `Hexalith.FrontComposer`, and `Hexalith.Tenants`.
  - [ ] Ensure setup guidance in `AGENTS.md`, `CLAUDE.md`, and any root README/setup section says to initialize/update only root-level submodules by default.
  - [ ] Remove or rewrite any setup instruction that uses `git submodule update --init --recursive` unless it is explicitly framed as user-requested nested-submodule work.
  - [ ] Add a lightweight documentation or repository-policy test that fails when setup guidance reintroduces recursive submodule initialization as the default path.
- [ ] Verify reproducibility. (AC: 2, 4, 5)
  - [ ] Run `dotnet restore Hexalith.Folders.slnx`.
  - [ ] Run `dotnet build Hexalith.Folders.slnx`.
  - [ ] Run the submodule-policy guard test or equivalent documentation check.
  - [ ] Record verification commands and results in the dev completion notes.

## Dev Notes

### Scope Boundaries

- Story 1.1 owns the initial consumer-buildable scaffold. If Story 1.1 already created a valid root file, update it only where needed to meet this story's reproducibility and submodule-policy acceptance criteria.
- Do not add Contract Spine endpoints, OpenAPI content, parity oracle generation, provider adapters, lifecycle domain logic, UI pages, CLI commands, MCP tools, workers, or CI workflow gates in this story.
- Do not initialize or update nested submodules. Do not run `git submodule update --init --recursive`. If a submodule is needed, initialize/update root-level submodules only.
- Do not modify the sibling modules `Hexalith.Tenants`, `Hexalith.EventStore`, `Hexalith.FrontComposer`, or `Hexalith.AI.Tools`; use them as read-only references for patterns.

### Required Root Files

The root configuration set for this story is:

```text
.editorconfig
.gitmodules
Directory.Build.props
Directory.Packages.props
Hexalith.Folders.slnx
global.json
nuget.config
AGENTS.md
CLAUDE.md
README.md
```

`AGENTS.md`, `CLAUDE.md`, and README/setup content are included because this story's user-facing policy is only effective if future agents and maintainers can discover it before running submodule commands.

### Configuration Requirements

- Target framework remains `net10.0` through root configuration.
- Central package management must remain enabled through `Directory.Packages.props`.
- Root build files should mirror Hexalith.Tenants conventions where applicable, but all package metadata must identify Hexalith.Folders.
- Sibling-module detection should support the repository layout where `Hexalith.Folders`, `Hexalith.Tenants`, `Hexalith.EventStore`, and `Hexalith.FrontComposer` are checked out side by side or as root-level repository entries.
- Restore/build must not depend on provider credentials, tenant seed data, production secret stores, running Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, or initialized nested submodules.
- Root config may reference root-level submodules already declared in `.gitmodules`; it must not require nested submodule state from inside those submodules.

### Submodule Policy

Required policy language:

- Initialize or update only root-level submodules by default.
- Never initialize or update nested submodules recursively unless the user explicitly asks for nested submodules.
- Avoid `git submodule update --init --recursive` and equivalent recursive commands for default setup.

Implementation may satisfy the guard test with a small script, xUnit test, or documentation test. Keep it simple and local: scan root setup docs and agent instruction files for forbidden recursive default commands, while allowing a sentence that forbids the command.

### Previous Story Intelligence

Story 1.1 created the scaffold story and defined the expected project layout, dependency direction, and no-secret build constraints. Carry forward these guardrails:

- `Contracts` remains contract-only and behavior-free.
- `Hexalith.Folders` core does not depend on transport projects.
- `Client` remains contract-centered; CLI, MCP, and UI wrap client surfaces later.
- Placeholder projects should stay non-operative and fail closed at runtime.
- Build verification is `dotnet build Hexalith.Folders.slnx` from the repository root.

### Testing Guidance

- Prefer a deterministic repository-policy check over a brittle text assertion when possible.
- The policy check should inspect root documentation and instruction files that a maintainer or agent will read before setup.
- Do not make the policy test require initialized nested submodules, network access, provider credentials, or a running local topology.
- If build fails because Story 1.1 scaffold work is incomplete, record the exact missing scaffold prerequisite instead of broadening this story into scaffold creation.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 1.2: Establish root configuration and submodule policy`
- `_bmad-output/planning-artifacts/epics.md#Solution Scaffolding (Phase 0 - sibling-module starter pattern)`
- `_bmad-output/planning-artifacts/architecture.md#Complete Project Directory Structure`
- `_bmad-output/planning-artifacts/architecture.md#File Organization Patterns`
- `_bmad-output/planning-artifacts/architecture.md#Development Workflow Integration`
- `_bmad-output/implementation-artifacts/1-1-establish-a-consumer-buildable-module-scaffold.md`
- `AGENTS.md#Git Submodules`
- `CLAUDE.md#Git Submodules`
- `.gitmodules`
- `Hexalith.Tenants/Directory.Build.props`
- `Hexalith.Tenants/Directory.Packages.props`
- `Hexalith.Tenants/global.json`

## Project Structure Notes

- The current repository already has root-level submodule entries for `Hexalith.AI.Tools`, `Hexalith.EventStore`, `Hexalith.FrontComposer`, and `Hexalith.Tenants`.
- The current repository already has `AGENTS.md` and `CLAUDE.md` policy text forbidding recursive nested-submodule initialization. The implementation should preserve or strengthen that policy when adding README/setup guidance.
- There is no discoverable `project-context.md`; use planning artifacts, Story 1.1, and sibling module files as implementation context.

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
