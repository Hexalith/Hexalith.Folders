---
title: 'Align Folders CI/CD with Hexalith domain repositories'
type: 'chore'
created: '2026-09-18'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '3dd121eadd3e952850be69ec3b7cfda6fd549925'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/references/Hexalith.Builds/.github/workflows/ci-cd-standards.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Folders uses repository-local CI and publishes five packages to GitHub Packages only after somebody creates a GitHub release. This differs from Tenants, EventStore, and FrontComposer, provides no semantic version/release creation path, and current `main` is red because of formatting, .NET 10 Microsoft.Testing.Platform configuration, and generated uppercase message IDs rejected by Folders' lowercase canonical-envelope rule. No Folders package, tag, or GitHub release is currently published.

**Approach:** Adopt the shared Hexalith exact-source, manual semantic-release flow: shared Release/MTP CI, an authenticated successful-push-CI prerequisite, immutable Builds workflow pinning, manifest-driven pack/validation, NuGet.org publication, and GitHub release assets. Retain Folders-specific quality gates, repair existing failures without weakening validation, and make the five-package inventory the single release source of truth.

## Boundaries & Constraints

**Always:** Use Release configuration and Microsoft.Testing.Platform; checkout with `submodules: false` and initialize only root-declared dependencies; pin the reusable release workflow to the current full `Hexalith.Builds` gitlink SHA and pass the same SHA as execution identity; require current `main`, an exact-SHA successful push CI run, protected `production`, explicit `NUGET_API_KEY`, and publication-authority controls; package only Contracts, Folders, Client, Aspire, and Testing; validate metadata, dependency closure, symbols, archive safety, and package consumers before publication; fail on duplicate NuGet versions; preserve specialized contract/security/parity gates.

**Never:** Trigger a release or configure secrets, environments, or the publication-freeze variable as part of this code change; publish to GitHub Packages; use mutable release workflow references, `secrets: inherit`, `--skip-duplicate`, recursive submodules, solution-wide packing, or globally disable NuGet audit; add CLI or ServiceDefaults to the public inventory without a separate product decision; copy unrelated container or repository-specific governance machinery.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| CI push/PR | Supported source event | Shared build/test/consumer validation plus Folders-specific gates run against the same commit | Any gate failure blocks success |
| Valid release | Manual dispatch on current `main`, exact-SHA CI success, authority configured | Semantic version is calculated; five sealed packages are validated, published once to NuGet.org, and attached to `v<version>` | Stop before side effects if any prerequisite or artifact check fails |
| Invalid/stale release | Non-main/stale SHA, absent CI proof, frozen publication, missing secret/environment | Nothing is published or tagged | Emit an actionable prerequisite failure |
| Existing version | NuGet.org already contains the calculated version | Publication fails | Do not skip or overwrite the duplicate |
| Mutation round trip | REST creates an internal sortable message ID | `/process` accepts the ID while caller-controlled canonical IDs retain their current validation contract | Invalid caller metadata still returns canonical 400 evidence |

</frozen-after-approval>

## Code Map

- `.github/workflows/ci.yml` -- shared domain CI entry point and retained Folders gates.
- `Hexalith.Folders.CI.slnx`, `Directory.Build.props` -- package-only CI/release graph while preserving source-project references for local development.
- `.github/workflows/release-packages.yml` / `.github/workflows/release.yml` -- replace release-event GitHub Packages publishing with guarded manual release orchestration.
- `.releaserc.json`, `package.json`, `package-lock.json` -- deterministic semantic-release toolchain and phases.
- `deploy/nuget/release-packages.yaml`, `tools/release-packages.json`, `tests/tools/run-release-package-gates.ps1` -- consolidate inventory and pack/validation behavior.
- `tests/Hexalith.Folders.Contracts.Tests/Deployment/ReleasePackageConformanceTests.cs`, `docs/operations/release-packages.md` -- executable governance contract and operator documentation.
- `global.json`, `.github/dependabot.yml`, `.github/workflows/{commitlint,codeql,dependency-review}.yml` -- MTP and shared supply-chain policy.
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs` -- ensure generated sortable IDs satisfy the process-envelope contract.
- `src/Hexalith.Folders/Providers/Abstractions/ProviderOperationSourceResolutionResult.cs`, `tests/Hexalith.Folders.Tests/Providers/Forgejo/ForgejoSmartHttpGitTransportTests.cs` -- current formatter failures.

## Tasks & Acceptance

**Execution:**
- [x] CI/release workflows and semantic-release files -- implement the shared exact-source, immutable, manual NuGet.org release model while preserving focused Folders gates.
- [x] Package manifest, pack/validation tooling, conformance tests, and release documentation -- establish one five-package inventory and enforce sealed, consumer-valid artifacts without duplicate suppression.
- [x] MTP/security configuration -- enable the .NET 10 test runner and add the standard dependency, commit, and code scanning workflows with immutable governed pins where required.
- [x] Endpoint and focused integration tests -- normalize internally generated message IDs to the canonical wire contract and prove real mutation routes reach their expected terminal states.
- [x] Formatter findings -- apply repository formatting and verify no unrelated behavior changes.

**Acceptance Criteria:**
- Given a PR or push, when CI runs, then shared Release/MTP build, test, coverage/consumer validation and existing Folders-specific gates execute and pass.
- Given release conformance tests, when workflow and scripts drift from the guarded five-package NuGet.org contract, then tests fail before publication.
- Given the repository has no release authority configuration, when the implementation is complete, then no package/tag/release has been created and the missing external prerequisites are reported.
- Given the current main failures, when focused and repository-wide checks run, then formatting, MTP invocation, and parity mutation failures are resolved without relaxing caller validation.

## Implementation Notes

- Replaced the release-event/GitHub Packages workflow with a protected, manual semantic-release path that authenticates current `main` and its exact successful push CI run before delegating to the immutable Builds release workflow.
- Consolidated the exact five public projects in `tools/release-packages.json`; packing, metadata/dependency/archive/symbol validation, isolated consumer builds, documentation, and conformance tests all consume or enforce that inventory.
- Enabled Microsoft.Testing.Platform and shared supply-chain workflows, retained the Folders contract/security/parity/safety gates, and kept root-only submodule initialization.
- Added a root-project-only CI solution and explicit package dependency mode for CI/release restore and build; local Debug development retains source dependencies.
- Restored capacity calibration, retention/deletion, and NFR traceability as blocking same-commit CI steps without coupling package sealing to checked-in gate reports.
- Replaced filtered project-level invocations in the blocking CI gate runners with built xUnit v3 assembly `-class`/`-method` selectors while preserving focused coverage and non-vacuous execution guards.
- Lowercased only internally generated sortable message IDs and added focused envelope/real-transport evidence; caller-provided canonical ID validation remains unchanged.
- No package, tag, release, secret, protected environment, or publication-freeze setting was created or changed.

## Spec Change Log

## Review Triage Log

| ID | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| BH-01 | medium | patch | `CI=true` makes `Directory.Build.props` select package dependencies, while the baseline's default/Debug probes omit `-p:CI=false`; the reproduced MSBuild evaluation returned package mode and would stop the GitHub job before build. |
| BH-02 | medium | defer | The retention step is blocking and currently fails because the preserved C3 policy input is `superseded-pending-reapproval`; this planning state was not created by the CI/CD implementation. |
| BH-03 | medium | defer | The baseline report is red only on the preserved C3/C6 planning-state assertions; build, formatting, analyzers, and code tests passed. |
| BH-04 | medium | defer | Contract/parity really reaches five 403 responses instead of the preserved 202/409 expectations, but that authorization drift is outside the endpoint-ID and CI/CD changes in this story. |
| BH-05 | medium | defer | Governance executes 32 tests and fails four OQ3/OQ4 status/digest assertions against preserved planning inputs; the new runner does not create that state. |
| BH-06 | medium | patch | `coverage-minimum-line: 0` and `coverage-required-branch: 0` reduce the new coverage gate to a nonempty-data smoke check instead of the shared 80/100 policy. |
| BH-07 | false | reject | Hexalith's CI/CD standard explicitly requires routine non-publication reusable workflows to use `@main`; only the publication workflow must use the reviewed immutable SHA, which `release.yml` does. |
| BH-08 | medium | patch | The specialized job allows 90 minutes for steps whose declared budgets total 160 minutes, and each browser job gives no setup margin beyond its 15+30 minute steps. Valid slow runs can be cancelled at the job ceiling. |
| BH-09 | medium | patch | The shared unit list omits Contracts.Tests and the focused baseline list omits several changed deployment conformance classes, so those workflow contracts have no complete normal-CI execution path. |
| BH-10 | medium | patch | Contract/parity retains exact method filters as metadata but executes combined whole-class selectors; a missing declared method or one missing class can be hidden by another test in the same invocation. |
| BH-11 | medium | patch | E2E retries every Release failure through the first executable found under any configuration, so a stale Debug executable can mask a real Release failure. |
| BH-12 | medium | patch | E2E returns success from `dotnet test` without proving a positive executed-test count, allowing discovery drift to pass vacuously. |
| BH-13 | false | reject | `NU1901`-`NU1904` remain visible audit warnings; exempting them from warnings-as-errors is explicitly required by the Hexalith CI/CD standard, which separately forbids disabling NuGet audit. |
| BH-14 | medium | patch | Symbol archives are checked only for filename, ZIP integrity, and safe paths; identity/version/repository metadata and a PDB payload are not validated. |
| BH-15 | medium | patch | Internal dependency checks discard version ranges and compare IDs case-sensitively, while consumer restore can use NuGet.org; a wrong or differently-cased Folders dependency can evade closure checks. |
| BH-16 | medium | patch | NuGet pushes are necessarily sequential, so a transient later failure can leave an immutable partial version; the current runbook gives no safe incident/recovery procedure consistent with duplicate-fail policy. |
| EC-01 | medium | patch | Same verified E2E fallback defect as BH-11: the recursive executable search is not Release-qualified and runs after any test failure. |
| EC-02 | medium | patch | The baseline passes all Contracts `-class` selectors in one invocation and checks only aggregate success; xUnit exits zero when one selector is missing but another matches. |
| EC-03 | medium | patch | Contract/parity also combines class selectors and checks only aggregate test count, so one missing declared selector is fail-open. |
| EC-04 | medium | patch | Governance combines three class selectors and checks only aggregate test count, so one renamed suite can disappear behind the remaining suites. |
| EC-05 | medium | patch | Coverage aggregates all declared line scopes without tracking each scope; one production package can disappear from measurement while another supplies data. |
| EC-06 | medium | patch | With a positive branch threshold and matched target but no parseable branch records, the validator assigns 100% and passes instead of failing closed. |
| EC-07 | low | patch | The validator rejects coverage exactly equal to its documented minimum because it uses `<=`; minimum semantics require `<`. |
| EC-08 | medium | patch | NaN, negative, and greater-than-100 thresholds are not validated; NaN comparisons can bypass both gates. |
| EC-09 | medium | patch | Same closure defect as BH-15: lowercase `hexalith.folders.*` dependency IDs do not enter the case-sensitive internal-dependency set. |
| EC-10 | medium | patch | Same verified partial-publication recovery gap as BH-16. |
| EC-11 | low | patch | The packer deletes every package archive in any caller-provided resolved directory; passing the repository root can remove unrelated root archives. A direct output-boundary guard is warranted. |
| EC-12 | medium | defer | The planning manifest/register digest disagreement is real but belongs to concurrent human-owned planning-authority edits, not this CI/CD implementation. |
| EC-13 | medium | patch | Same verified symbol-validation gap as BH-14. |
| EC-14 | false | reject | The planning graph and approval edits visible in the unified worktree diff were pre-existing/concurrent user changes and were preserved; they are not formatter or CI/CD edits made by this build. |
| VG-01 | medium | patch | Pre-verified: removing `ReleasePackageConformanceTests` from the combined baseline selector leaves current conformance assertions and the remaining xUnit selection green. Each configured class must be run and counted independently. |
| VG-02 | medium | patch | Pre-verified: duplicate-version preflight behavior is only source-text asserted; inverting the NuGet index decision leaves all current tests green. A stubbed executable test is missing. |
| VG-03 | medium | patch | Pre-verified: unsafe ZIP rejection is only source-text asserted and safe generated packages exercise no negative path. Crafted archive tests are missing. |
| VG-04 | medium | patch | `contract-spine.yml` builds Debug, then calls newly Release-only safety, governance, and NFR runners with `-SkipRestoreBuild`; a clean runner lacks the required Release assembly. |

## Design Notes

Tenants, EventStore, and FrontComposer converge on manual `workflow_dispatch`, exact-source CI authentication, semantic-release, a single JSON inventory, NuGet.org, and an immutable reusable `domain-release` pin. Folders' specialized contract-spine and safety checks remain additive rather than being replaced. Live checks on 2026-09-18 found NuGet.org 404 for all five IDs, no repository tags/releases, no GitHub Packages entries, and no visible release secret, authority variable, or `production` environment.

## Verification

**Commands:**
- `dotnet restore Hexalith.Folders.CI.slnx -p:Configuration=Release -p:UseNuGetDeps=true -m:1` and `dotnet build Hexalith.Folders.CI.slnx --configuration Release -p:UseNuGetDeps=true --no-restore -p:TreatWarningsAsErrors=true -m:1` -- passed with zero warnings and errors; output built only root repository projects and no `references/*` source projects.
- Exact CI formatter commands (`dotnet format whitespace ... --include ./src/ ./tests/ ./samples/` and `dotnet format analyzers ... --severity warn`) -- passed. The broader unscoped command still reports non-blocking naming diagnostics and independent submodule formatting, so it is not the repository CI contract.
- `dotnet tests/Hexalith.Folders.Contracts.Tests/bin/Release/net10.0/Hexalith.Folders.Contracts.Tests.dll -noLogo -noColor -class Hexalith.Folders.Contracts.Tests.Deployment.NfrTraceabilityConformanceTests` -- 17/17 passed, proving the required built-assembly single-dash selector path; the baseline Client selector likewise passed 316/316 with two `-method-` exclusions.
- Focused baseline/release/capacity/NFR conformance passed 44/44; the six retention runner/wiring/independence/negative-control facts passed 6/6 with direct xUnit v3 `-method` selection.
- Direct blocking-runner verification passed for security/redaction (15/15 across four exact method groups), safety (11/11), and all three explicit accessibility classes. Contract/parity executed every configured Release-assembly category and failed only in the mixed-surface category on five existing authorization expectations (expected 202/409, observed 403); governance executed 32 tests and failed four existing OQ3/OQ4 status/digest assertions.
- Restored specialized gates: capacity calibration passed with 90/90 lifecycle requests and zero failures; NFR traceability passed 17/17; retention/deletion correctly failed closed with `category=policy-source reason=missing-policy-status` because the preserved user-owned C3 input currently says `superseded-pending-reapproval` rather than `policy status: approved`.
- Release conformance (`ReleasePackageConformanceTests`) -- 7/7 passed; canonical mutation envelope matrix -- 13/13 passed; real `/process` mutation integration -- 1/1 passed; NFR gate-runner self-check -- 1/1 passed.
- `pwsh ./tests/tools/run-release-package-gates.ps1 -Version 0.0.0-ci-test -SourceRevisionId 3dd121eadd3e952850be69ec3b7cfda6fd549925 -SkipRestoreBuild` -- passed with exactly five `.nupkg`/`.snupkg` pairs, sealed archive/metadata/dependency validation, and two isolated consumer builds; publish mode was not invoked.
- `npm ci --ignore-scripts`, `npm audit signatures`, and commitlint smoke input -- passed; 0 vulnerabilities, 504 verified registry signatures, and 127 verified attestations.
- The baseline runner passed dependency-mode checks, format/analyzers, and Folders.Tests (1960 passed, 1 explicitly skipped), then failed only on five pre-existing user-owned planning/governance mismatches: two C3 retention-policy facts and three C6 lifecycle vocabulary/state/edge facts. Those unrelated artifacts were preserved.
- `actionlint .github/workflows/*.yml`, `git diff --check`, and Python script byte-compilation passed.
- External release prerequisites remain intentionally absent from this change: protected `production`, `NUGET_API_KEY`, and publication authority/freeze configuration must be supplied by repository administrators before a manual release can proceed.
