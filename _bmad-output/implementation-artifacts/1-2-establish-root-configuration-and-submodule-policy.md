# Story 1.2: Establish root configuration and submodule policy

Status: done

Created: 2026-05-10

## Story

As a maintainer,
I want root repository configuration and root-level submodule policy established,
so that builds are reproducible and nested submodules are not initialized accidentally.

## Acceptance Criteria

1. Given the scaffolded repository, when root configuration is verified or added, then `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `nuget.config`, `.editorconfig`, `.gitmodules`, and `Hexalith.Folders.slnx` exist at the repository root and contain the minimal functional configuration needed for deterministic .NET 10 restore/build. Existing valid root-file content is preserved unless a change is required by this story; `.gitmodules` remains an inventory of root-level submodule entries only.
2. Root configuration follows the Hexalith.Tenants sibling-module conventions for .NET 10, central package management, deterministic package restore, analyzer/build settings, package metadata, and sibling Hexalith module path detection. Alignment is limited to SDK versioning, central package management shape, deterministic restore/build metadata, analyzer/style conventions, package metadata shape, and solution layout conventions; do not copy tenant-specific package references, provider settings, CI secrets, runtime service assumptions, or feature projects.
3. `.gitmodules`, `AGENTS.md`, `CLAUDE.md`, the root README/setup section, and `tests/README.md` document root-level submodule initialization only and explicitly forbid recursive nested-submodule initialization unless the user requests nested submodules. `.gitmodules` remains a root-level submodule inventory limited to `Hexalith.AI.Tools`, `Hexalith.Commons`, `Hexalith.EventStore`, `Hexalith.FrontComposer`, `Hexalith.Memories`, and `Hexalith.Tenants`; policy prose belongs in README/agent docs unless the format safely supports comments.
4. `dotnet restore Hexalith.Folders.slnx` and `dotnet build Hexalith.Folders.slnx --no-restore` succeed from the repository root without provider credentials, tenant data, production secrets, running Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, sibling-module build dependency state beyond optional root-level submodules, or initialized nested submodules.
5. A deterministic root-level repository-policy test, script, or documentation check proves the submodule policy is discoverable and fails when default setup guidance, agent instructions, or setup scripts contain `git submodule update --init --recursive`, reordered equivalents such as `git submodule update --recursive --init`, `git submodule foreach --recursive`, `git clone --recurse-submodules`, bare `--recursive` or `--recurse-submodules` submodule setup, or equivalent recursive nested-submodule initialization unless clearly framed as a user-requested nested-submodule opt-in path.

## Tasks / Subtasks

- [x] Inspect the Story 1.1 scaffold outputs before changing root files. (AC: 1, 2, 4)
  - [x] Verify whether `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `nuget.config`, `.editorconfig`, `.gitmodules`, and `Hexalith.Folders.slnx` already exist.
  - [x] Preserve valid Story 1.1 project references and placeholder project structure; this story hardens root configuration rather than rebuilding the scaffold.
  - [x] Record any missing root files in the dev completion notes before adding them.
- [x] Harden shared build and restore configuration. (AC: 1, 2, 4)
  - [x] Adapt `Hexalith.Tenants/Directory.Build.props` to `Hexalith.Folders`, preserving `net10.0`, nullable, implicit usings, warnings-as-errors, `LangVersion=latest`, deterministic build settings, package metadata, and sibling path detection.
  - [x] Ensure sibling path detection is conditional and optional; missing sibling directories or uninitialized nested submodules must not break restore/build.
  - [x] Adapt `Hexalith.Tenants/Directory.Packages.props` for central package management; package references in project files must not carry versions unless a project-local version is explicitly justified.
  - [x] Add or align `global.json` with the .NET 10 SDK roll-forward convention used by Hexalith.Tenants.
  - [x] Add `nuget.config` only for deterministic restore sources and package mapping needed by this repository; do not add credentials or private feed secrets.
  - [x] Verify `nuget.config` contains no clear-text credentials, password entries, token placeholders, or machine/user-specific feed paths.
  - [x] Add or align `.editorconfig` with sibling-module formatting and analyzer conventions.
- [x] Confirm solution and root configuration agree. (AC: 1, 2, 4)
  - [x] Verify `Hexalith.Folders.slnx` includes every scaffold project produced by Story 1.1 and no non-root nested submodule project paths.
  - [x] Confirm root props do not require running Aspire, Dapr, Keycloak, Redis, GitHub, Forgejo, or tenant seed data at build time.
  - [x] Ensure package metadata points to Hexalith.Folders and does not retain copied Hexalith.Tenants package IDs, repository URLs, descriptions, or tags where they would be misleading.
- [x] Establish root-level submodule policy. (AC: 3, 5)
  - [x] Keep `.gitmodules` limited to root-level entries: `Hexalith.AI.Tools`, `Hexalith.Commons`, `Hexalith.EventStore`, `Hexalith.FrontComposer`, `Hexalith.Memories`, and `Hexalith.Tenants`.
  - [x] Ensure setup guidance in `AGENTS.md`, `CLAUDE.md`, and any root README/setup section says to initialize/update only root-level submodules by default and includes short imperative setup steps with copy-pasteable commands.
  - [x] Include the canonical root-only command block and one-sentence rationale in the root README/setup section and agent-facing policy docs.
  - [x] Remove or rewrite any setup instruction that uses `git submodule update --init --recursive` unless it is explicitly framed as user-requested nested-submodule work.
  - [x] Cover reordered recursive flags, `git clone --recurse-submodules`, `git submodule foreach --recursive`, and script-level recursive setup variants in the guard test.
  - [x] Add a lightweight documentation or repository-policy test that fails when setup guidance reintroduces recursive submodule initialization as the default path while allowing deny-list warning text.
- [x] Verify reproducibility. (AC: 2, 4, 5)
  - [x] Run `dotnet restore Hexalith.Folders.slnx`.
  - [x] Run `dotnet build Hexalith.Folders.slnx --no-restore`.
  - [x] Run the submodule-policy guard test or equivalent documentation check.
  - [x] Record verification commands and results in the dev completion notes.

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
git submodule update --init Hexalith.AI.Tools Hexalith.Commons Hexalith.EventStore Hexalith.FrontComposer Hexalith.Memories Hexalith.Tenants

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

- The current repository root-level submodule entries are `Hexalith.AI.Tools`, `Hexalith.Commons`, `Hexalith.EventStore`, `Hexalith.FrontComposer`, `Hexalith.Memories`, and `Hexalith.Tenants`. The original story scope listed four; `Hexalith.Commons` and `Hexalith.Memories` were ratified as part of this story during the 2026-05-12 review (see Change Log) because both are required for downstream extension paths that the canonical setup command must surface.
- The current repository already has `AGENTS.md` and `CLAUDE.md` policy text forbidding recursive nested-submodule initialization. The implementation should preserve or strengthen that policy when adding README/setup guidance.
- There is no discoverable `project-context.md`; use planning artifacts, Story 1.1, and sibling module files as implementation context.

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-10 | Applied `bmad-advanced-elicitation` hardening for optional sibling detection, NuGet credential safety, recursive-command variants, and repository-policy guard precision. | Codex |
| 2026-05-10 | Applied `bmad-party-mode` review hardening for root-only submodule policy, guard-test criteria, and deterministic build verification. | Codex |
| 2026-05-10 | Created ready-for-dev story through `bmad-create-story` workflow. | Codex |
| 2026-05-11 | Implemented root configuration hardening, root-only submodule setup guidance, and repository-policy guard tests. | Codex |
| 2026-05-12 | Code-review hardening: ratified Hexalith.Commons + Hexalith.Memories in canonical root inventory (AC3); aligned tests/README.md and extended policy guard coverage (AC5); tightened submodule-recursion detection (whitespace, bare flag, multi-line, config-key, regex), strengthened warning-context heuristic, made nuget credential check structural, fixed XLinq case-sensitivity, added empty-file guard, and corrected `.editorconfig` line-ending and severity-suffix issues. | Claude Opus 4.7 |

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

Codex GPT-5

### Debug Log References

- `dotnet test .\tests\Hexalith.Folders.Testing.Tests\Hexalith.Folders.Testing.Tests.csproj --no-restore --filter FullyQualifiedName~ScaffoldContractTests -v:minimal` failed red phase as expected because `.editorconfig`, `nuget.config`, and canonical setup guidance were missing.
- `dotnet test .\tests\Hexalith.Folders.Testing.Tests\Hexalith.Folders.Testing.Tests.csproj --no-restore --filter FullyQualifiedName~ScaffoldContractTests -v:minimal` passed after adding root files, setup guidance, and policy checks.
- `dotnet restore .\Hexalith.Folders.slnx` passed.
- `dotnet build .\Hexalith.Folders.slnx --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test .\Hexalith.Folders.slnx --no-build -v:minimal` passed: 16 tests across 11 test assemblies.
- `git diff --check` reported no whitespace errors; Git displayed line-ending normalization warnings for touched text files.
- `rg -n 'PackageReference[^>]*Version=|packageSourceCredentials|cleartextpassword|password|token|%userprofile%|\$HOME' -g '*.csproj' -g 'nuget.config' -g '!Hexalith.EventStore/**' -g '!Hexalith.FrontComposer/**' -g '!Hexalith.Tenants/**' -g '!Hexalith.AI.Tools/**'` found no inline package versions or NuGet credential/token/user-path markers.

### Completion Notes List

- Story 1.1 scaffold outputs were inspected before root-file changes. `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `.gitmodules`, and `Hexalith.Folders.slnx` already existed; `.editorconfig` and `nuget.config` were missing and were added.
- Preserved Story 1.1 project structure and solution membership while adding `.editorconfig`, `.gitmodules`, and `nuget.config` to solution items.
- Hardened root build metadata with deterministic build settings while preserving `net10.0`, nullable, implicit usings, warnings-as-errors, `LangVersion=latest`, Hexalith.Folders package metadata, and optional root-level sibling detection.
- Added `nuget.config` with only the public NuGet v3 source and package-source mapping; no credentials, password entries, token placeholders, or machine/user-specific feed paths were added.
- Added root `.editorconfig` aligned with sibling-module formatting, naming, namespace, using-placement, and analyzer conventions.
- Added canonical root-only submodule setup guidance to `AGENTS.md`, `CLAUDE.md`, and `README.md`, including the root-level command block, deny-list example, and rationale for avoiding recursive initialization.
- Added deterministic repository-policy smoke tests that check required root files, NuGet source safety, canonical setup text, and recursive submodule setup variants while allowing warning/deny-list examples.
- `.gitmodules` remains a root-level inventory for `Hexalith.Tenants`, `Hexalith.AI.Tools`, `Hexalith.EventStore`, and `Hexalith.FrontComposer`; no nested submodule initialization was run or required.

### File List

- `.editorconfig`
- `AGENTS.md`
- `CLAUDE.md`
- `Directory.Build.props`
- `Hexalith.Folders.slnx`
- `README.md`
- `nuget.config`
- `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs`

### Review Findings

Code review run 2026-05-12 via `/bmad-code-review`, full-spec mode, diff `eb52d15..HEAD` filtered to story 1-2 file set (9 files, +394/-1). Three parallel reviewers: Blind Hunter (adversarial, diff-only), Edge Case Hunter (boundary walk, project read), Acceptance Auditor (vs AC + Dev Notes).

#### Decision-needed (HIGH/MEDIUM intent ambiguity)

- [x] [Review][Decision] `.gitmodules` adds `Hexalith.Memories` + `Hexalith.Commons` beyond Story 1.2's spec-named root inventory — Spec line 105 + line 152 + task line 40 explicitly name only `Hexalith.AI.Tools / Hexalith.EventStore / Hexalith.FrontComposer / Hexalith.Tenants`. The 6-module canonical command is now locked into `AGENTS.md`, `CLAUDE.md`, `README.md`, and the `SubmodulePolicyIsDiscoverableAndForbidsRecursiveDefaultSetup` test (`ScaffoldContractTests.cs:261`). Decide: (a) revert the additions out of story 1-2 scope, (b) ratify the spec drift by amending Story 1.2 AC3 + Dev Notes, or (c) split into a follow-up story for the new submodules.
- [x] [Review][Decision] `Directory.Build.props` flipped `IsPackable` default from `true` to `false` while the comment claims "Contracts, core, Client, Testing, ServiceDefaults" flip it back — but **no csproj in the diff flips it back**, and sibling `Hexalith.Tenants/Directory.Build.props` uses `true` as default. AC2 requires alignment with Tenants for "package metadata shape". Decide: (a) revert to `true` default for Tenants alignment, (b) add `<IsPackable>true</IsPackable>` to the named library csproj files now, or (c) accept the deviation and document it (the conditional `<ItemGroup Condition="'$(IsPackable)' == 'true'">` block at `Directory.Build.props:36-38` is currently dead for every project).
- [x] [Review][Decision] `tests/README.md` line 11 still documents the **4-module** init command while root docs use the **6-module** version — a maintainer reading the tests README won't init `Memories`/`Commons`. The policy guard does NOT scan `tests/README.md` (`PolicyDocumentPaths` walks only root + `docs/`), so the drift is silent. Resolution depends on D1 outcome: (a) align `tests/README.md` to 6-module list and extend the guard to scan it, (b) revert root docs to 4-module list, or (c) explicitly carve `tests/README.md` out of policy scope.

#### Patch (unambiguous fixes)

- [x] [Review][Patch] `ContainsRecursiveSubmoduleSetup` boolean precedence has dead clauses — clause 3 (`--recurse-submodules` alone) subsumes clause 2 (`git clone + --recurse-submodules`); clause 1 (`git submodule + --recursive`) subsumes clause 4 (`git submodule foreach + --recursive`). [`tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs:464-474`]
- [x] [Review][Patch] `ContainsRecursiveSubmoduleSetup` doesn't detect bare `--recursive` (AC5 explicitly lists "bare `--recursive` or `--recurse-submodules`") — the function requires `--recursive` to be paired with `git submodule`, so `RECURSE_FLAG=--recursive` in a setup script bypasses the guard. [`ScaffoldContractTests.cs:464-474`]
- [x] [Review][Patch] `ContainsRecursiveSubmoduleSetup` fails on whitespace/global-flag variants — `git -C subdir submodule update --recursive`, `git --git-dir=... submodule …`, or a tab between `git` and `submodule` all bypass the literal `"git submodule"` substring match. Use `\bgit\b.*\bsubmodule\b` regex or normalize whitespace. [`ScaffoldContractTests.cs:464-474`]
- [x] [Review][Patch] `ContainsRecursiveSubmoduleSetup` misses multi-line continuation (`git submodule update --init \\` + newline + `--recursive`) and config-based recursion (`git config submodule.recurse true`). Join continuation lines before scanning; consider adding a `submodule.recurse` literal check. [`ScaffoldContractTests.cs:464-474`]
- [x] [Review][Patch] `IsWarningOrNestedOptInContext` keyword list ("nested submodule", "explicitly requests") is permanently satisfied by the canonical block sentence "Nested submodules must only be initialized when a user explicitly requests nested submodule work." Any future recursive command added within ±4 lines of that sentence is silently exempted. Tighten by scoping to the same code-fence block or paragraph as the violation. [`ScaffoldContractTests.cs:476-488`]
- [x] [Review][Patch] `IsWarningOrNestedOptInContext` doesn't recognize contractions ("don't") or "should not"/"prohibited"/"deprecated" — a documented warning using a contraction won't be matched, producing false positives. [`ScaffoldContractTests.cs:476-488`]
- [x] [Review][Patch] `RecursiveDefaultSetupViolations` context window is asymmetric (4 lines before, 2 after) and joins lines with spaces — flattens markdown paragraphs so unrelated prose leaks into context, and misses warning text that lives more than 4 lines before the violation. [`ScaffoldContractTests.cs:433-452`]
- [x] [Review][Patch] `IsPolicyDocument` exclusion list omits the new submodules `Hexalith.Commons` and `Hexalith.Memories` — once those are initialized, their nested markdown will be scanned by the policy guard. [`ScaffoldContractTests.cs:454-462`]
- [x] [Review][Patch] `IsPolicyDocument` last clause `!relative.Contains("/Hexalith.Tenants/")` is missing the `StringComparison.Ordinal` argument that all siblings have — minor consistency bug. [`ScaffoldContractTests.cs:461`]
- [x] [Review][Patch] `IsPolicyDocument` checks the absolute path against literal segments like `"/_bmad"` and `"/Hexalith.AI.Tools/"` — if the repo is ever cloned under a path containing those segments (e.g., `D:/_bmadwork/repo`), every policy doc gets excluded and the guard silently passes. Use `Path.GetRelativePath(root, path)` first. [`ScaffoldContractTests.cs:454-462`]
- [x] [Review][Patch] `ContainsRecursiveSubmoduleSetup` lowercases input then uses `StringComparison.Ordinal` against lowercase needles — fragile; any future needle with uppercase silently fails to match. Drop the explicit `Ordinal` or skip the `ToLowerInvariant()` and use `OrdinalIgnoreCase`. [`ScaffoldContractTests.cs:466, 478`]
- [x] [Review][Patch] `SubmodulePolicyIsDiscoverableAndForbidsRecursiveDefaultSetup` does a literal substring match on the 6-module canonical command — cosmetic doc edits (whitespace, alphabetical reorder of a new module) break the test without semantic change. Tokenize submodule names and assert as a set. [`ScaffoldContractTests.cs:380-407`]
- [x] [Review][Patch] `NuGetConfigurationUsesPublicSourceWithoutCredentials` deny-list `password`/`token` will trip on benign comments or legitimate URL paths containing those substrings; doesn't catch inline `user:pass@host` URLs. Parse XML and inspect credential-bearing elements (`packageSourceCredentials`, `apikeys`, `clientCertificates`) and URL credential syntax. [`ScaffoldContractTests.cs:359-377`]
- [x] [Review][Patch] `ProjectsDoNotOverrideRootBuildConfigurationLocally` drift list omits the very settings this diff added to root — `Deterministic`, `ContinuousIntegrationBuild`, `IsPackable`, `IsPublishable` — so subprojects could silently override the new root policy. [`ScaffoldContractTests.cs:302-322`]
- [x] [Review][Patch] `XDocument.Descendants("TargetFramework")` and siblings are case-sensitive on element name; lowercase/mixed-case `<targetframework>` bypasses the check. Compare on `LocalName` with ordinal-ignore-case. [`ScaffoldContractTests.cs:324-335`]
- [x] [Review][Patch] `RequiredRootConfigurationFilesExist` checks only `File.Exists` — an empty zero-byte file satisfies it but breaks restore. Add `new FileInfo(p).Length > 0`. [`ScaffoldContractTests.cs:337-356`]
- [x] [Review][Patch] `NuGetConfigurationUsesPublicSourceWithoutCredentials` reads the same file twice (`File.ReadAllText` + `XDocument.Load(path)`). Use `XDocument.Parse(content)` once. [`ScaffoldContractTests.cs:363-364`]
- [x] [Review][Patch] `ProjectReferencesFollowAllowedDependencyDirection` looks up `references["..."]` for many newly-asserted project names; if any name isn't in `ExpectedSolutionProjects`, the dictionary throws `KeyNotFoundException` rather than failing the test with a descriptive message. Use `TryGetValue` + a clean assertion. [`ScaffoldContractTests.cs:69-237`]
- [x] [Review][Patch] `.editorconfig` forces `end_of_line = crlf` globally — hostile to shell scripts, Dockerfiles, and YAML on Linux. Either set global default to `lf` or add per-pattern overrides (`[*.sh]`, `[Dockerfile]`, `[*.yml]`). [`.editorconfig:14`]
- [x] [Review][Patch] `.editorconfig` line `csharp_new_line_before_open_brace = all:warning` — the `:warning` severity suffix isn't a valid form for this setting; analyzer may silently ignore the severity or the whole line. Use the documented syntax. [`.editorconfig:55`]
- [x] [Review][Patch] `PolicyDocumentPaths` doesn't scan `tests/README.md` or root setup scripts (`*.ps1`, `*.sh`), but AC5 / Testing Guidance (spec line 128) explicitly requires "root setup scripts, and root-level onboarding/developer setup docs when present". Combined with D3, this hides the 4-vs-6 module drift. [`ScaffoldContractTests.cs:416-431`]

#### Resolution (2026-05-12)

- **Decision 1** (submodule scope drift): ratified the 6-module canonical root inventory. Updated AC3, Dev Notes Submodule Policy required canonical command block, Project Structure Notes, and the Tasks/Subtasks entry to include `Hexalith.Commons` and `Hexalith.Memories`.
- **Decision 2** (IsPackable opt-in): **moot — already satisfied**. A grep of `src/**/*.csproj` confirms all five named libraries (`Contracts`, core `Hexalith.Folders`, `Client`, `Testing`, `ServiceDefaults`) already carry `<IsPackable>true</IsPackable>`, plus `Cli` opts in for tool distribution. The Acceptance Auditor missed this because those csproj edits originated in commit `6a427f7` (story 1-1 follow-up) and weren't in the story 1-2 chunk filter. No code change needed; documenting here for traceability.
- **Decision 3** (`tests/README.md` drift): aligned the file to the 6-module canonical command, extended `PolicyDocumentPaths` to scan `tests/README.md` + root setup scripts (`*.ps1`, `*.sh`, `*.cmd`, `*.bat`) and equivalents under `tests/`, and added a discoverability assertion that each scanned policy document carries both the canonical command and the prohibition wording.

All 21 reviewer-raised patch items applied in `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs` and `.editorconfig`. Highlights:

- Submodule recursion detection rewritten as a regex set covering whitespace/global-flag variants (`git -C dir submodule …`), bare `--recurse-submodules`, `submodule.recurse` git-config form, and `--recursive` without explicit `git` prefix. Multi-line continuation (`\` + newline) is joined before scanning while preserving the original line number in the violation report.
- Warning-context heuristic switched to **preceding-prose-only** scope (8 lines, bounded by markdown headings/horizontal rules; skips blank lines and code-fence markers) instead of the symmetric ±4-line window that previously leaked context across paragraphs. Keyword list extended with contractions (`don't`, `shouldn't`, `mustn't`), `should not`, `must not`, `prohibit`, `deprecated`, `discouraged`, `not use`, `user-requested`.
- Canonical command assertion tokenized: any line containing `git submodule update --init` plus all six named submodules satisfies the check, so cosmetic ordering or whitespace edits no longer break the test.
- NuGet credential check restructured to parse the XML once and assert *structurally* (no `<packageSourceCredentials>`, `<apikeys>`, `<clientCertificates>`; no `://user:pass@` URLs; no machine-path interpolation) instead of relying on substring deny-lists like `"password"` or `"token"` that produce false positives on benign content.
- `IsPolicyDocument` now operates on the **relative** path (not absolute), so a repo cloned under a directory containing `/_bmad` no longer silently excludes every policy doc. Exclusion list now covers all six root-level submodule directories.
- `XDocument` element lookups use a `DescendantsByLocalName` helper that is case-insensitive on the local-name, so lowercase/mixed-case element names cannot bypass drift checks.
- `RequiredRootConfigurationFilesExist` asserts both existence **and** non-empty file size.
- `ProjectsDoNotOverrideRootBuildConfigurationLocally` drift list extended with `Deterministic` and `ContinuousIntegrationBuild` (the new root-owned settings); `IsPackable` and `IsPublishable` are intentionally excluded because they are designed as opt-in per-project policy.
- `ProjectReferencesFollowAllowedDependencyDirection` and `ForbiddenReferencesAreNotIntroduced` now route through `TryGetValue` helpers (`AssertReferences`, `RequireReferences`) so a missing project name yields a descriptive failure instead of `KeyNotFoundException`.
- `.editorconfig`: removed the invalid `:warning` severity suffix from `csharp_new_line_before_open_brace`; added per-pattern `end_of_line = lf` overrides for `*.sh|bash|zsh`, `Dockerfile`, and `*.yml|yaml` so POSIX scripts and CI artifacts are not silently corrupted by the Windows-default `crlf`.

#### Deferred (real but not actionable in this story)

- [x] [Review][Defer] `.editorconfig` `async_methods_should_end_with_async` rule may flag controller actions and Blazor lifecycle overrides at feature-implementation time — deferred to first feature story where this surfaces. [`.editorconfig:41-49`]
- [x] [Review][Defer] Private-field naming rule covers `private` accessibility only; `protected`/`internal` field naming silently allowed — deferred until those modifiers appear. [`.editorconfig:31-39`]
- [x] [Review][Defer] `CA1062`, `CA2007` set to `warning` combined with root `TreatWarningsAsErrors=true` could mass-fail builds when real code lands. Builds passed per Dev Notes today, so deferred — revisit if a feature story trips it. [`.editorconfig:59-61`]
- [x] [Review][Defer] Submodule policy text is triplicated across `AGENTS.md`, `CLAUDE.md`, `README.md` — drift risk but intentional per spec for discoverability. Deferred to a future single-source-of-truth refactor (e.g., generated includes).
- [x] [Review][Defer] `nuget.config` uses `<clear/>` then only nuget.org — destructive to corporate-mirror users but matches AC2 "no private feed assumptions". Deferred.
- [x] [Review][Defer] `Deterministic=true` paired with `ContinuousIntegrationBuild` gated to `'$(CI)' == 'true'` means local PDBs still carry absolute paths. Matches the gated intent; deferred.
- [x] [Review][Defer] Spec File List (line 240-247) omits `.gitmodules` from the touched files, even though `.gitmodules` was modified in the diff. Record-keeping inconsistency, not a code defect. Deferred for the next dev-record housekeeping pass.
- [x] [Review][Defer] `ScaffoldContractTests.ProjectReferencesFollowAllowedDependencyDirection` now locks down the entire 24-project dependency graph — properly Story 1.1's territory and brittle. Test wording is acceptable per Story 1.2's "solution/dependency smoke test" allowance; revisit ownership in a Story 1.1 review iteration.
