---
title: '11.7 consolidate deterministic time context and path test helpers'
type: 'refactor'
created: '2026-09-21'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '84d5444ba4480cf8c8b78c818aadd28fff7f0d1b'
story_key: '11-7-consolidate-deterministic-time-context-and-path-test-helpers'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-11-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Tests repeat immutable clocks, tenant/claim accessors, path data, and root walkers. Small differences make seam changes noisy and risk weakening conformance.

**Approach:** Add configurable Testing helpers and migrate equivalent consumers. Move two context interfaces into core so published Testing avoids non-packable Server; add Contracts.Tests -> Testing.

## Boundaries & Constraints

**Always:** Preserve time, identity/evidence, path cases, fallbacks, exceptions, assertions, and conformance references. Keep full E2E/WCAG lanes blocking/unfiltered, authority bytes identical, and sprint state unchanged.

**Never:** Absorb gateway doubles (11.17), provider/repository fakes (11.18), production path policy, wire/generated artifacts, or generator root handling. Do not expose a new API version, touch either 3.14 spec, or change dependencies/versions, package inventory, submodules, authority, lifecycle, CI semantics, commits, or remotes.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Fixed time | Any instant | `UtcNow`/`GetUtcNow()` return it | Mutable/timer clocks stay local |
| Context | Identity, action, permissions/mode | Existing Missing/Allowed result | Preserve validation/intentional empty IDs |
| Path fixture | Default/derived/unsafe row | Exact metadata and 18/10/8 case sets | No assertion broadening |
| Root lookup | App base/CWD/seed and lookup mode | Existing path/fallback behavior | Hard-fail callers still throw |

</frozen-after-approval>

## Code Map

- Server context interfaces -> core `Authorization`; same members, updated imports.
- `src/Hexalith.Folders.Testing/{Time,Authentication,Paths}/` -> five public helpers, one type/file.
- Affected Folders/Server/Integration/Workers/load tests -> canonical clocks/contexts; keep specialized clocks.
- Affected Contracts/Client/Integration/Server/Testing/Folders/Workers tests and shared parity -> matched root/path modes; tool/PowerShell roots and wire literals stay local.
- Contracts.Tests project plus scaffold assertion -> pin its sole new Testing reference; Testing stays core+Contracts.
- HonestGreen/E2E/Accessibility classes and gate files -> validation-only; no rename/narrowing.

## Tasks & Acceptance

**Execution:**
- [ ] `src/Hexalith.Folders.Server/Authentication/{ITenantContextAccessor,IEventStoreClaimTransformEvidenceAccessor}.cs`, `src/Hexalith.Folders/Authorization/{ITenantContextAccessor,IEventStoreClaimTransformEvidenceAccessor}.cs` -- relocate interfaces and imports, preserving contracts/package direction.
- [ ] `src/Hexalith.Folders.Testing/{Time,Authentication,Paths}/*.cs` -- implement five canonical helpers and behavior modes.
- [ ] `tests/Hexalith.Folders.*Tests/**/*.cs`, `tests/load/**/*.cs`, `tests/shared/Parity/ParityOracle.cs` -- migrate in-scope duplicates; preserve specialized variants/conformance text.
- [ ] `tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj`, `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs` -- declare and pin the authorized reference.
- [ ] `tests/Hexalith.Folders.Testing.Tests/Unit/{FixedTimeProvider,TestTenantContextAccessor,TestClaimTransformEvidenceAccessor,CanonicalPathTestData,RepositoryTestPaths}Tests.cs` -- cover time, contexts, exact path cases, roots, sentinels, fallbacks, and corpus anchors.

**Acceptance Criteria:**
- Given equivalent duplicates, when migrated, then each concern has one Testing implementation and no equivalent copy.
- Given specialized behavior, when consolidated, then mutable/timer/hard-fail/tool semantics remain unchanged.
- Given governance gates, when run, then full lanes, no-filter, forbidden strings, and named references stay blocking/green.
- Given boundaries, when diffed, then excluded doubles/fakes and protected artifacts are unchanged.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

Core ownership avoids published Testing depending on non-packable Server. Root lookup keeps explicit strict/lenient/seeded modes.

## Verification

**Commands:**
- `for project in tests/Hexalith.Folders.{Testing,Server,Client,Integration,Workers,LoadTests,Contracts}.Tests/*.csproj tests/Hexalith.Folders.Tests/*.csproj; do dotnet test "$project" -c Debug -p:UseNuGetDeps=false -m:1 || exit; done` -- zero failures.
- `dotnet restore Hexalith.Folders.slnx -p:Configuration=Debug -p:UseNuGetDeps=false -m:1`; `dotnet build Hexalith.Folders.slnx -c Debug -p:UseNuGetDeps=false --no-restore -warnaserror -m:1` -- clean.
- `pwsh ./tests/tools/run-baseline-ci-gates.ps1`; `pwsh ./tests/tools/run-release-package-gates.ps1` -- green without inventory change.
- `for name in HonestGreenGateBaseline E2eCiWorkflow AccessibilityCiWorkflow; do dotnet tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests.dll -noLogo -noColor -class "Hexalith.Folders.Contracts.Tests.Deployment.${name}ConformanceTests" || exit; done` -- full-lane controls pass.
- `pwsh ./tests/install-playwright.ps1 -SkipBuild`; `pwsh ./tests/tools/run-e2e-ci-gates.ps1 -SkipRestoreBuild -SkipBrowserInstall`; `pwsh ./tests/tools/run-accessibility-ci-gates.ps1 -SkipRestoreBuild -SkipBrowserInstall` -- complete unfiltered lanes pass.
- `git diff --check` and scoped protected-file diffs/hashes -- no unauthorized change.
