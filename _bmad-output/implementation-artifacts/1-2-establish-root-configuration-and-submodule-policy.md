# Story 1.2: Establish root configuration and submodule policy

Status: ready-for-dev

Created: 2026-05-10

## Story

As a maintainer,
I want root repository configuration and root-level submodule policy established,
so that builds are reproducible and nested submodules are not initialized accidentally.

## Acceptance Criteria

1. Given the scaffolded repository, when root configuration is verified or added, then `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `nuget.config`, `.editorconfig`, `.gitmodules`, and `Hexalith.Folders.slnx` exist at the repository root and contain the minimal functional configuration needed for deterministic .NET 10 restore/build. Existing valid root-file content is preserved unless a change is required by this story; `.gitmodules` remains an inventory of root-level submodule entries only.
2. Root configuration follows the Hexalith.Tenants sibling-module conventions for .NET 10, central package management, deterministic package restore, analyzer/build settings, package metadata, and sibling Hexalith module path detection. Alignment is limited to SDK versioning, central package management shape, deterministic restore/build metadata, analyzer/style conventions, package metadata shape, and solution layout conventions; do not copy tenant-specific package references, provider settings, CI secrets, runtime service assumptions, or feature projects.
3. `.gitmodules`, `AGENTS.md`, `CLAUDE.md`, and the root README/setup section document root-level submodule initialization only and explicitly forbid recursive nested-submodule initialization unless the user requests nested submodules. `.gitmodules` remains a root-level submodule inventory; policy prose belongs in README/agent docs unless the format safely supports comments.
4. `dotnet restore Hexalith.Folders.slnx` and `dotnet build Hexalith.Folders.slnx --no-restore` succeed from the repository root without provider credentials, tenant data, production secrets, running Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, sibling-module build dependency state beyond optional root-level submodules, or initialized nested submodules.
5. A deterministic root-level repository-policy test, script, or documentation check proves the submodule policy is discoverable and fails when default setup guidance, agent instructions, or setup scripts contain `git submodule update --init --recursive`, reordered equivalents such as `git submodule update --recursive --init`, `git submodule foreach --recursive`, `git clone --recurse-submodules`, bare `--recursive` or `--recurse-submodules` submodule setup, or equivalent recursive nested-submodule initialization unless clearly framed as a user-requested nested-submodule opt-in path.

## Tasks / Subtasks

- [ ] Inspect the Story 1.1 scaffold outputs before changing root files. (AC: 1, 2, 4)
  - [ ] Verify whether `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `nuget.config`, `.editorconfig`, `.gitmodules`, and `Hexalith.Folders.slnx` already exist.
  - [ ] Preserve valid Story 1.1 project references and placeholder project structure; this story hardens root configuration rather than rebuilding the scaffold.
  - [ ] Record any missing root files in the dev completion notes before adding them.
- [ ] Harden shared build and restore configuration. (AC: 1, 2, 4)
  - [ ] Adapt `Hexalith.Tenants/Directory.Build.props` to `Hexalith.Folders`, preserving `net10.0`, nullable, implicit usings, warnings-as-errors, `LangVersion=latest`, deterministic build settings, package metadata, and sibling path detection.
  - [ ] Ensure sibling path detection is conditional and optional; missing sibling directories or uninitialized nested submodules must not break restore/build.
  - [ ] Adapt `Hexalith.Tenants/Directory.Packages.props` for central package management; package references in project files must not carry versions unless a project-local version is explicitly justified.
  - [ ] Add or align `global.json` with the .NET 10 SDK roll-forward convention used by Hexalith.Tenants.
  - [ ] Add `nuget.config` only for deterministic restore sources and package mapping needed by this repository; do not add credentials or private feed secrets.
  - [ ] Verify `nuget.config` contains no clear-text credentials, password entries, token placeholders, or machine/user-specific feed paths.
  - [ ] Add or align `.editorconfig` with sibling-module formatting and analyzer conventions.
- [ ] Confirm solution and root configuration agree. (AC: 1, 2, 4)
  - [ ] Verify `Hexalith.Folders.slnx` includes every scaffold project produced by Story 1.1 and no non-root nested submodule project paths.
  - [ ] Confirm root props do not require running Aspire, Dapr, Keycloak, Redis, GitHub, Forgejo, or tenant seed data at build time.
  - [ ] Ensure package metadata points to Hexalith.Folders and does not retain copied Hexalith.Tenants package IDs, repository URLs, descriptions, or tags where they would be misleading.
- [ ] Establish root-level submodule policy. (AC: 3, 5)
  - [ ] Keep `.gitmodules` limited to root-level entries such as `Hexalith.AI.Tools`, `Hexalith.EventStore`, `Hexalith.FrontComposer`, and `Hexalith.Tenants`.
  - [ ] Ensure setup guidance in `AGENTS.md`, `CLAUDE.md`, and any root README/setup section says to initialize/update only root-level submodules by default and includes short imperative setup steps with copy-pasteable commands.
  - [ ] Include the canonical root-only command block and one-sentence rationale in the root README/setup section and agent-facing policy docs.
  - [ ] Remove or rewrite any setup instruction that uses `git submodule update --init --recursive` unless it is explicitly framed as user-requested nested-submodule work.
  - [ ] Cover reordered recursive flags, `git clone --recurse-submodules`, `git submodule foreach --recursive`, and script-level recursive setup variants in the guard test.
  - [ ] Add a lightweight documentation or repository-policy test that fails when setup guidance reintroduces recursive submodule initialization as the default path while allowing deny-list warning text.
- [ ] Verify reproducibility. (AC: 2, 4, 5)
  - [ ] Run `dotnet restore Hexalith.Folders.slnx`.
  - [ ] Run `dotnet build Hexalith.Folders.slnx --no-restore`.
  - [ ] Run the submodule-policy guard test or equivalent documentation check.
  - [ ] Record verification commands and results in the dev completion notes.

## Dev Notes

### Scope Boundaries

- Story 1.1 owns the initial consumer-buildable scaffold. If Story 1.1 already created a valid root file, update it only where needed to meet this story's reproducibility and submodule-policy acceptance criteria.
- Do not add Contract Spine endpoints, OpenAPI content, parity oracle generation, provider adapters, lifecycle domain logic, UI pages, CLI commands, MCP tools, workers, or CI workflow gates in this story.
- Do not add application, adapter, worker, CLI, MCP, UI, lifecycle, provider, contract-spine implementation, tenant logic, Dapr integration, or runtime feature projects as part of this story. Solution membership may include only existing or explicitly root-scaffold projects required for restore/build validation.
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
- Sibling-module detection should support the repository layout where `Hexalith.Folders`, `Hexalith.Tenants`, `Hexalith.EventStore`, and `Hexalith.FrontComposer` are checked out side by side or as root-level repository entries, using conditional properties so absent siblings do not fail the default build.
- Restore/build must not depend on provider credentials, tenant seed data, production secret stores, running Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, or initialized nested submodules.
- Root config may reference root-level submodules already declared in `.gitmodules`; it must not require nested submodule state from inside those submodules.
- `nuget.config` must not include credentials, token placeholders, clear-text passwords, user-profile paths, or private feed assumptions.

### Submodule Policy

Required policy language:

- Initialize or update only root-level submodules by default.
- Never initialize or update nested submodules recursively unless the user explicitly asks for nested submodules.
- Avoid `git submodule update --init --recursive` and equivalent recursive commands for default setup.
- Treat recursive setup variants as forbidden by default, including reordered `--recursive` flags, `git clone --recurse-submodules`, `git submodule foreach --recursive`, and bare `--recurse-submodules` in setup scripts.
- Recursive initialization can pull nested dependencies unexpectedly, so it is allowed only when intentionally requested for nested-submodule work.

Required canonical command block:

```text
Initialize only root-level submodules:
git submodule update --init Hexalith.AI.Tools Hexalith.EventStore Hexalith.FrontComposer Hexalith.Tenants

Do not use:
git submodule update --init --recursive

Nested submodules must only be initialized when a user explicitly requests nested submodule work.
```

Implementation may satisfy the guard test with a small script, xUnit test, or documentation test. Keep it simple and local: scan root setup docs, agent instruction files, root setup scripts, and root-level onboarding/developer setup docs for forbidden recursive default commands, while allowing deny-list or warning text that forbids the command. The guard must fail on recursive commands presented as executable default setup and must pass when recursive commands appear only in warning or nested-submodule opt-in context.

### Previous Story Intelligence

Story 1.1 created the scaffold story and defined the expected project layout, dependency direction, and no-secret build constraints. Carry forward these guardrails:

- `Contracts` remains contract-only and behavior-free.
- `Hexalith.Folders` core does not depend on transport projects.
- `Client` remains contract-centered; CLI, MCP, and UI wrap client surfaces later.
- Placeholder projects should stay non-operative and fail closed at runtime.
- Build verification is `dotnet build Hexalith.Folders.slnx` from the repository root.

### Testing Guidance

- Prefer a deterministic repository-policy check over a brittle text assertion when possible.
- The policy check should inspect root documentation and instruction files that a maintainer or agent will read before setup, including `AGENTS.md`, `CLAUDE.md`, root `README*`, root setup scripts, and root-level onboarding/developer setup docs when present.
- The policy check should avoid false positives for explicit deny-list or warning examples and avoid false negatives for unqualified recursive commands in default setup instructions.
- The policy check should normalize simple whitespace and flag-order variants so `--recursive --init`, `--init --recursive`, and `--recurse-submodules` default setup commands are treated consistently.
- Do not make the policy test require initialized nested submodules, network access, provider credentials, or a running local topology.
- If a solution/dependency smoke test is added, it should verify the intended layer direction and confirm root projects do not take sibling modules as build dependencies.
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
| 2026-05-10 | Applied `bmad-advanced-elicitation` hardening for optional sibling detection, NuGet credential safety, recursive-command variants, and repository-policy guard precision. | Codex |
| 2026-05-10 | Applied `bmad-party-mode` review hardening for root-only submodule policy, guard-test criteria, and deterministic build verification. | Codex |
| 2026-05-10 | Created ready-for-dev story through `bmad-create-story` workflow. | Codex |

## Party-Mode Review

- Date/time: 2026-05-10T17:32:00Z
- Selected story key: `1-2-establish-root-configuration-and-submodule-policy`
- Command/skill invocation used: `/bmad-party-mode 1-2-establish-root-configuration-and-submodule-policy; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), Paige (Technical Writer)
- Findings summary:
  - Acceptance criteria needed executable guard-test criteria, not only policy intent.
  - Hexalith.Tenants alignment needed a bounded reference surface to avoid over-copying sibling-specific behavior.
  - Restore/build verification needed exact root solution commands and external-runtime exclusion.
  - Setup guidance needed a canonical root-only command block and clear warning-context behavior for forbidden recursive examples.
- Changes applied:
  - Tightened AC1-AC5 around functional root files, bounded sibling alignment, exact restore/build commands, root-only submodule discoverability, and recursive-command guard behavior.
  - Added submodule-policy tasks for canonical setup text, copy-pasteable commands, and deny-list-aware guard validation.
  - Added scope guardrails preventing runtime feature, adapter, CLI, MCP, UI, worker, contract-spine, tenant, or Dapr implementation work in this story.
  - Added testing guidance for scanned policy surfaces, false-positive/false-negative handling, and sibling-module dependency smoke coverage.
- Findings deferred:
  - Exact guard implementation mechanism: script, xUnit test, markdown lint rule, or documentation check.
  - Exact `global.json` roll-forward policy and NuGet source policy.
  - CI gate design and future runtime module boundaries.
  - Whether root-level submodule names are generated from `.gitmodules` or hardcoded in setup guidance.
- Final recommendation: ready-for-dev

## Advanced Elicitation

- Date/time: 2026-05-10T22:03:48Z
- Selected story key: `1-2-establish-root-configuration-and-submodule-policy`
- Command/skill invocation used: `/bmad-advanced-elicitation 1-2-establish-root-configuration-and-submodule-policy`
- Batch 1 method names: Red Team vs Blue Team; Security Audit Personas; Pre-mortem Analysis; Failure Mode Analysis; Critique and Refine
- Reshuffled Batch 2 method names: Expert Panel Review; Architecture Decision Records; Self-Consistency Validation; Comparative Analysis Matrix; Occam's Razor Application
- Findings summary:
  - Recursive submodule setup could re-enter as reordered flags, `git clone --recurse-submodules`, or script-level variants not named in the original guard criteria.
  - Sibling-module detection needed explicit optional behavior so local checkout layout differences do not become hidden restore/build dependencies.
  - `nuget.config` needed a direct credential and machine-specific path exclusion, not just a general no-secrets statement.
  - The repository-policy guard needed normalization guidance to reduce false negatives without broadening the story into CI gate design.
- Changes applied:
  - Clarified `.gitmodules` as root-level inventory and valid root-file preservation.
  - Added optional sibling path detection requirements and absent-sibling build behavior.
  - Added `nuget.config` credential, token placeholder, clear-text password, and user-path checks.
  - Expanded forbidden default recursive setup variants and guard-test normalization expectations.
- Findings deferred:
  - Exact implementation mechanism for the repository-policy guard remains open to the dev-story agent.
  - Exact `global.json` roll-forward policy and NuGet source mapping stay bounded to Hexalith.Tenants alignment during implementation.
  - Whether setup guidance derives root submodule names from `.gitmodules` or keeps the canonical command block hardcoded remains an implementation choice.
- Final recommendation: ready-for-dev

## Dev Agent Record

### Agent Model Used

TBD by dev-story agent

### Debug Log References

### Completion Notes List

### File List
