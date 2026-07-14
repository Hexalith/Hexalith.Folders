do best
---
title: 'Restore green build and test validation'
type: 'bugfix'
created: '2026-07-14'
status: 'done'
review_loop_iteration: 0
baseline_commit: 'df7b5f3fd3f8c270051a5544ae8dcb6bab52a8fb'
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The current user-owned dependency-mode migration leaves repository validation red. Direct project tests evaluate an eventual Debug build as NuGet mode before `Configuration` is defaulted, FrontComposer's relocated storage test fake is no longer referenced by Folders UI tests, and scaffold policy tests reject incomplete root agent setup guidance.

**Approach:** Preserve the in-progress dual-mode design while repairing its default evaluation and test-only dependency boundary, restore the required nonrecursive submodule guidance, then run root-owned projects individually and fix only additional failures supported by test evidence.

## Boundaries & Constraints

**Always:** Preserve all pre-existing worktree changes; keep Debug/source and Release/package dependency graphs coherent; run the `.slnx` only for restore/build and test projects individually; keep edits in the root repository unless explicitly authorized; rerun restore after switching dependency modes.

**Ask First:** Any fix that requires changing a referenced submodule, weakening a test/gate, changing public behavior, or discarding/replacing user-owned edits.

**Never:** Initialize nested submodules; use recursive submodule commands; run solution-level `dotnet test`; hand-edit generated clients; hide failures with suppressions, exclusions, or altered assertions; treat unavailable opt-in external infrastructure as a product pass.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Direct default test | `dotnet test <project>` with no configuration property | Dependency mode resolves to Debug source references and compiles | Fail with the original diagnostic; do not silently fall back to mismatched assets |
| Explicit Release build | Fresh restore/build with `Configuration=Release` | Centrally pinned NuGet dependencies are selected and compile | Report package/API drift separately from Debug failures |
| UI test storage fake | Current FrontComposer source or package graph | UI tests resolve `InMemoryStorageService` from `Hexalith.FrontComposer.Testing` | Do not copy or recreate the fake locally |
| Root setup policy | Agent setup documentation | Canonical explicit root submodule command is discoverable and nonrecursive | Keep nested initialization forbidden |

</frozen-after-approval>

## Code Map

- `Directory.Build.props` -- user-owned dual dependency-mode defaults and source-availability flags.
- `src/Hexalith.Folders.{AppHost,Server,UI,Workers}/*.csproj` -- existing conditional source/package consumers that must remain intact.
- `tests/Hexalith.Folders.UI.Tests/Hexalith.Folders.UI.Tests.csproj` -- missing FrontComposer Testing dependency.
- `tests/Hexalith.Folders.UI.Tests/{BadgeRenderingFixture,ShellCompositionTests}.cs` -- consumers of the moved storage fake.
- `AGENTS.md`, `CLAUDE.md` -- scaffold-tested root submodule instructions.
- `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs` -- policy contract that exposed the documentation regression.
- `tests/tools/run-*-gates.ps1` -- authoritative root validation lanes; unchanged unless a script defect is directly proven.

## Tasks & Acceptance

**Execution:**
- [x] `Directory.Build.props` -- make an unqualified direct project invocation resolve the eventual default Debug graph while preserving explicit overrides and Release package mode.
- [x] `tests/Hexalith.Folders.UI.Tests/Hexalith.Folders.UI.Tests.csproj` -- add conditional source/package access to `Hexalith.FrontComposer.Testing` without disturbing production UI dependencies.
- [x] `tests/Hexalith.Folders.UI.Tests/{BadgeRenderingFixture,ShellCompositionTests}.cs` -- import the fake from its current owner.
- [x] `AGENTS.md`, `CLAUDE.md` -- add the canonical explicit root-only initialization command and retain the recursive-init prohibition.
- [x] `src/**`, `tests/**`, `samples/**` -- run the prescribed project/gate matrix and apply only minimal, evidence-backed follow-up fixes if new failures appear.

**Acceptance Criteria:**
- Given a fresh default restore, when the `.slnx` is built and each hermetic root test project is run individually, then all projects compile and all executed tests pass.
- Given explicit Debug and Release evaluations, when dependency-mode properties are inspected, then Debug selects available root submodule sources and Release selects centrally pinned packages.
- Given the authoritative baseline, contract/parity, security/redaction, capacity, accessibility, and E2E gates, when prerequisites are available, then each gate passes without weakening coverage or filters.
- Given an opt-in lane whose external prerequisite is unavailable, when verification completes, then the exact command and blocker are recorded separately from focused passing evidence.

## Spec Change Log

## Design Notes

`Configuration` is observed as `Debug` after project evaluation while the newly added mode property is already fixed to `false`; the repair must account for evaluation timing, not merely add `--configuration Debug` to local commands. FrontComposer's project context explicitly assigns `InMemoryStorageService` to its Testing package, so the consumer should reference that owner rather than duplicate implementation.

## Verification

**Commands:**
- `dotnet restore Hexalith.Folders.slnx -m:1 -p:NuGetAudit=false && dotnet build Hexalith.Folders.slnx --no-restore -m:1` -- expected: default Debug graph builds with zero errors.
- `pwsh ./tests/tools/run-baseline-ci-gates.ps1 -SkipRestoreBuild` -- expected: formatting, analyzers, and baseline per-project tests pass.
- `dotnet test <each remaining root test csproj> --no-restore --no-build` -- expected: full non-browser matrix passes project by project.
- `pwsh ./tests/tools/run-contract-parity-ci-gates.ps1` and `pwsh ./tests/tools/run-security-redaction-ci-gates.ps1` -- expected: focused contract and security lanes pass.
- `pwsh ./tests/tools/run-capacity-smoke-ci-gates.ps1` -- expected: hermetic capacity smoke succeeds.
- `pwsh ./tests/install-playwright.ps1 -SkipBuild && pwsh ./tests/tools/run-e2e-ci-gates.ps1 -SkipRestoreBuild -SkipBrowserInstall` -- expected: browser suite passes when Chromium/localhost prerequisites are available.
- `dotnet restore Hexalith.Folders.slnx -p:Configuration=Release -m:1 -p:NuGetAudit=false && dotnet build Hexalith.Folders.slnx -c Release --no-restore -m:1` -- expected: package dependency graph builds with zero errors.

## Suggested Review Order

**Dependency graph selection**

- Defines default/source/package selection and the dedicated Testing availability flag.
  [`Directory.Build.props:15`](../../Directory.Build.props#L15)

- Selects the exact Testing dependency and fails closed on partial source checkouts.
  [`Hexalith.Folders.UI.Tests.csproj:9`](../../tests/Hexalith.Folders.UI.Tests/Hexalith.Folders.UI.Tests.csproj#L9)

- Verifies default, Debug, Release graphs and a fresh package-mode UI lane.
  [`run-baseline-ci-gates.ps1:139`](../../tests/tools/run-baseline-ci-gates.ps1#L139)

- Consumes the moved storage fake from its FrontComposer Testing owner.
  [`BadgeRenderingFixture.cs:7`](../../tests/Hexalith.Folders.UI.Tests/BadgeRenderingFixture.cs#L7)

**Release packaging**

- Runs restore, build, and pack in one explicit Release/package graph.
  [`run-release-package-gates.ps1:317`](../../tests/tools/run-release-package-gates.ps1#L317)

- Mirrors explicit package mode across conformance and publishing jobs.
  [`release-packages.yml:131`](../../.github/workflows/release-packages.yml#L131)

- Locks packaging workflow commands against configuration drift.
  [`ReleasePackageConformanceTests.cs:108`](../../tests/Hexalith.Folders.Contracts.Tests/Deployment/ReleasePackageConformanceTests.cs#L108)

**Root submodule policy**

- Documents exact root initialization and explicit recursive prohibition.
  [`AGENTS.md:9`](../../AGENTS.md#L9)

- Centralizes the exact eight-submodule root set.
  [`ScaffoldContractTests.cs:98`](../../tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs#L98)

- Applies the canonical nonrecursive command throughout CI.
  [`ci.yml:27`](../../.github/workflows/ci.yml#L27)

- Gives contributors the same root-only setup command.
  [`README.md:14`](../../README.md#L14)

**Operational evidence**

- Explains dependency-mode and package-mode regression evidence.
  [`baseline-ci-gates.md:15`](../../docs/operations/baseline-ci-gates.md#L15)

- Explains consistent Release/package restore, build, and pack behavior.
  [`release-packages.md:33`](../../docs/operations/release-packages.md#L33)

- Records two pre-existing hardening items outside this fix.
  [`deferred-work.md:466`](deferred-work.md#L466)
