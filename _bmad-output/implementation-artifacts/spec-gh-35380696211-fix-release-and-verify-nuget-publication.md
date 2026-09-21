---
title: 'Restore green Folders CI and publish the first NuGet release'
type: 'bugfix'
created: '2026-09-19'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'e2881d901b3f12e3f9023617fe622c5f11254916'
context:
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '{project-root}/docs/operations/release-packages.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** CI run `35380696211` failed before tests because `Directory.Build.targets` contained an invalid XML comment. The fix is now on `main`, but exact-source run `35428612624` is still red: 17 Contracts, 5 Testing, 10 Integration, and 36 UI E2E tests fail across release-runner assumptions, stale scaffold assertions, unreconciled C3/C6/OQ3/OQ4 authority artifacts, authorization/wire-contract behavior, and an unreleased FrontComposer heading fix. Release therefore cannot authenticate a successful push CI run for the five Folders packages.

**Approach:** Repair every current blocker in the documented full repository gate without hiding or weakening failures. Publish the existing FrontComposer heading correction when needed for package-mode UI conformance, evaluate and record governance approvals only from complete repository evidence, obtain successful exact-source push evidence, configure the documented release controls, dispatch Release from the unchanged qualified `main` SHA, and verify all five packages plus the immutable GitHub release.

## Boundaries & Constraints

**Always:** Preserve package-mode Release CI, the full repository gate, exact-source `main` authentication, the five-package manifest, immutable Builds workflow pinning, protected-environment approval, duplicate-version failure, and NuGet.org as the only package destination. Treat the first NuGet write, tag, and GitHub release as irreversible publication side effects.

**Never:** Mark incomplete or superseded policy as approved; remove or soften E2E/retention/product gates; switch package qualification to project-reference mode; make unrelated cross-repository changes; use `--skip-duplicate`; retry a partially published version.

**Authorized decisions (2026-09-19):** Preserve the documented full-CI prerequisite and repair every blocker before release. Cross-repository work and publication for the existing FrontComposer heading correction are authorized where required to restore package-mode UI conformance. Governance candidates may be evaluated and their digest-bound C3/C6/OQ3 approval records may be updated on behalf of the named authorities only when the repository evidence is complete; incomplete evidence remains fail-closed and must not be represented as approved.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Green release | Current `main` has successful push CI, production approval, publish variable, and org NuGet secret | Semantic Release publishes exactly five same-version `.nupkg`/`.snupkg` pairs and an immutable GitHub release | Verify IDs, versions, hashes/assets, tag SHA, and run conclusion |
| Stale or red source | `main` advances or exact-source CI is not successful | No release side effects | Stop and rerun CI for the new tip |
| Duplicate or partial publication | Any target version exists or only some pushes succeed | Never overwrite, skip, or retry that version | Record accepted IDs and require a new corrective version |

</frozen-after-approval>

## Code Map

- `Directory.Build.targets` -- invalid `--coverage` XML comment from the linked run is already corrected at `e2881d9`.
- `.github/workflows/ci.yml` -- shared package-mode CI plus blocking Folders baseline, retention, accessibility, and E2E lanes.
- GitHub run `35428612624` -- current authoritative failure set: Contracts 17, Testing 5, Integration 10, and UI E2E 36; restore, build, package consumers, coverage, accessibility, and the other unit projects pass.
- `references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Components/Layout/FrontComposerShell.razor` -- source at `c8beb9d9` renders the banner title as `span`; published `4.4.0` renders it as `h1`.
- `references/Hexalith.Builds/Props/Directory.Packages.props` -- authoritative `HexalithFrontComposerVersion` pin currently selects `4.4.0` for Folders package-mode builds.
- `src/Hexalith.Folders.UI/Components/Pages/*.razor` and `tests/Hexalith.Folders.UI.E2E.Tests` -- route-owned heading contract and 36 package-mode regressions.
- `docs/exit-criteria/*`, `docs/contract/authorization-matrix.md`, `_bmad-output/planning-artifacts/planning-story-manifest.yaml` -- C3/C6/OQ3/OQ4 candidates, digests, authority state, and lifecycle/documentation drift.
- `tests/Hexalith.Folders.Contracts.Tests`, `tests/Hexalith.Folders.Testing.Tests`, `tests/Hexalith.Folders.IntegrationTests` -- Release-path tool-location assumptions, stale scaffold expectations, governance checks, and authorization/wire-contract regressions.
- `.github/workflows/release.yml`, `.releaserc.json`, `tools/release-packages.json` -- exact-source release orchestration, semantic version phases, and five-package inventory.
- `docs/operations/release-packages.md` -- required `production` environment, `HEXALITH_RELEASE_PUBLISH_ENABLED=true`, org `NUGET_API_KEY`, and partial-publication response.

## Tasks & Acceptance

**Execution:**
- [x] In `references/Hexalith.FrontComposer`, validate the existing `FrontComposerShell.razor` heading correction, repair any blocking `.github/workflows/ci.yml` failures, release the corrected Shell package through that repository's governed workflow, and record its published version.
- [x] In `tests/Hexalith.Folders.Contracts.Tests/OpenApi/ParityOracleGeneratorTests.cs` and `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs`, replace stale Debug/SDK/AppHost/solution assumptions with configuration-independent assertions that match the tracked repository.
- [x] In `docs/exit-criteria/*`, `docs/contract/authorization-matrix.md`, `_bmad-output/planning-artifacts/planning-story-manifest.yaml`, and their conformance tests, reconcile lifecycle and governance evidence; write digest-bound C3/C6/OQ3 decisions only when every required artifact is complete, and keep incomplete OQ4 evidence fail-closed.
- [x] In the production code exercised by `ArchiveFolderProcessWiringTests.cs`, `GoldenLifecycleParityTests.cs`, and `MixedSurfaceHandoffTests.cs`, restore authorization, replay/conflict, and wire-contract behavior without weakening the test expectations.
- [x] In `references/Hexalith.Builds/Props/Directory.Packages.props`, advance the authoritative FrontComposer pin to the verified release, commit and push the owning Builds repository, update the Folders Builds gitlink, then run the focused test projects, `tests/tools/run-retention-deletion-gates.ps1`, `tests/tools/run-e2e-ci-gates.ps1 -SkipBrowserInstall`, and `tests/tools/run-release-package-gates.ps1`.
- [ ] Commit the owning-repository changes with validated Conventional Commits, push each `main`, and require the exact pushed Folders SHA's `.github/workflows/ci.yml` run to succeed.
- [ ] Configure `Hexalith/Hexalith.Folders`'s `production` environment to mirror the established Hexalith reviewer/main policy and set repository variable `HEXALITH_RELEASE_PUBLISH_ENABLED=true`.
- [ ] Dispatch `.github/workflows/release.yml` from the unchanged green Folders SHA, satisfy environment approval, monitor to completion, and verify NuGet.org plus the immutable GitHub release.

**Acceptance Criteria:**
- Given the full-CI route is selected, when package-mode UI and governance gates run, then all 63 E2E tests pass, required approvals are digest-bound, and every blocking CI job succeeds without weakened semantics.
- Given the current pushed `main` SHA, when full CI completes, then Release authenticates that exact successful run.
- Given successful publication, when NuGet.org and GitHub are queried, then all five expected IDs share the new version, the release tag resolves to the published SHA, and all ten package/symbol assets are present.

## Implementation Notes

- Local Folders package-mode restore/build is clean. Testing passes 68/68, Integration passes
  695/695, and the UI E2E suite passes 63/63 against the FrontComposer source correction. The
  published FrontComposer `4.4.0` package still reproduces the expected 36 duplicate-heading
  failures.
- C3 and C6 were reconciled only from the existing digest-bound A7/A7b authority records. OQ3
  remains fail-closed: the Contracts suite now has exactly three failures, all requiring the absent
  generated PD10 v2 conformance set owned by the separate draft Story 1.17 specification. No A6b or
  A8 approval was inferred or recorded.
- FrontComposer exact-source CI run `35431134374` exposed a missing .NET SDK setup in the
  dependency-governance job. The local repair installs the `global.json` SDK before affected-module
  builds and adds a governance assertion. Three supplemental Quality regressions were also
  diagnosed: an over-broad Fluent emitter scan, a stale EventStore helper-location assertion, and
  one intentional test-identifier inventory addition; focused repaired checks pass 3/3.
- FrontComposer's remaining supplemental Quality hold is not part of the approved heading-only
  cross-repository scope: Story 11.25 deliberately keeps Gate 2c red until a genuine EventStore
  `run-evidence.v4` / `apphost-smoke.v3` recapture replaces the preserved packet, while current
  EventStore/Builds coordinates have advanced off its sealed tuple. Publication remains stopped.
- Superseding local repository evidence on 2026-09-20 records FrontComposer `v4.5.0` at
  `b2bac5f193130ea08abc22fd78d6a936c2f419f4`, Builds `4.5.0` consumption at
  `2ca5965`, the Folders dependency update at `27cebf6`, and a Folders `v1.0.0` tag at
  `823dce16bca7d1eb9c10c4d871bf581fc9dc61ef`. Remote run, environment, NuGet-index, and
  GitHub-release asset evidence still requires an authorized remote verification pass.

## Spec Change Log

- 2026-09-20: Recorded completed local implementation tasks and refreshed package-mode verification
  at `5174b82c9b69aa1a81d89837c4b14a25ebbd7915`; retained remote publication tasks as open pending
  direct GitHub and NuGet evidence.

## Review Triage Log

## Verification

**Commands:**
- `pwsh ./tests/tools/run-retention-deletion-gates.ps1` -- approved C3 evidence passes.
- `pwsh ./tests/tools/run-e2e-ci-gates.ps1 -SkipBrowserInstall` -- 63/63 pass in package mode.
- `pwsh ./tests/tools/run-release-package-gates.ps1 -Version <dry-run> -SourceRevisionId <sha>` -- exactly five sealed package/symbol pairs validate without publication.
- `gh run view <ci-run> --repo Hexalith/Hexalith.Folders` -- exact-source push CI succeeds.
- `gh run view <release-run> --repo Hexalith/Hexalith.Folders` -- Release succeeds; NuGet flat-container indexes and GitHub release assets match the manifest.

**Current results:**

- PASS -- `dotnet build Hexalith.Folders.CI.slnx` in Release/package mode, zero warnings/errors.
- PASS -- Contracts 332/332, Testing 68/68, Integration 689/689, and package-mode UI E2E 63/63.
- PASS -- retention/deletion gate reports approved at source
  `5174b82c9b69aa1a81d89837c4b14a25ebbd7915`.
- PASS -- dry-run release gate validated five sealed package/symbol pairs and two isolated
  package-only consumers at version `0.0.0-local.20260920`.
- PASS -- release tooling fail-closed coverage passes 11/11 tests, including occupied-version
  rejection without `--skip-duplicate`.
- UNVERIFIED -- exact-source GitHub CI/Release run conclusions, protected-environment configuration,
  all five NuGet.org `1.0.0` indexes, and all ten immutable GitHub release assets require remote
  queries and remain open.
