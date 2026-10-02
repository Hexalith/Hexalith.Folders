---
title: 'Publish Folders through NuGet trusted publishing'
type: 'bugfix'
created: '2026-10-02'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'e88aca956ff24cd6371a5fe3446034c45c70e644'
builds_baseline_commit: '3734acbb14d30d06cea401bb26f40b0017702620'
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-git-instructions.md'
  - '{project-root}/docs/operations/release-packages.md'
  - '{project-root}/references/Hexalith.Builds/AGENTS.md'
  - '{project-root}/references/Hexalith.Builds/.github/workflows/domain-release.md'
---

<frozen-after-approval reason="explicit user instruction: use trusted publishing, create the policy">

## Intent

Release run 36973683976 qualified and packed all five packages but failed uploading the first package with NuGet HTTP 403. The user explicitly requests trusted publishing and creation of its policy instead of rotating a long-lived API key. Prepare, validate, and publish the required shared and Folders changes; register the NuGet policy through legitimate authenticated account access; run Release and verify all five NuGet packages when registration is available. Do not claim a local policy document registers a remote policy.

## Boundaries & Constraints

Preserve production protection, normal reviewer approval, exact current-main successful push-CI proof, publication freeze, five-package inventory, same-version package/symbol gates, immutable versions and dependency versions. Keep reusable preparation in Hexalith.Builds. Authenticate and run semantic-release inside a Folders-owned production job because NuGet validates job_workflow_ref against the package repository. The individual NuGet policy creator is supplied through repository variable NUGET_USER; never infer it from GitHub identity or package owner. The policy package owner is Hexalith and grants new-version publication only to the five existing package IDs. No stored-key fallback, token logging, secret transfer between jobs, nested submodule initialization, altered old tags, or duplicate skipping. The failed immutable v1.1.0 tag remains untouched; a subsequent fix commit can produce v1.1.1.

## I/O & Edge-Case Matrix

| Scenario | State | Expected behavior |
|---|---|---|
| Publish authorized | Exact green current main, production approved, registered policy, NUGET_USER set | Pinned NuGet/login mints a temporary key immediately before semantic-release; all five publish |
| Missing policy creator | Empty/whitespace NUGET_USER | Fail before token exchange or semantic-release |
| Frozen publication | Flag absent, TRUE, True, or padded true | Preparation reports frozen; login and publication skip |
| Invalid source | Wrong ref, malformed/mismatched dispatch or checkout SHA, stale live main, wrong/failed CI | Fail before login and publication |
| Unregistered/mismatched policy | NuGet rejects OIDC | Fail without stored API-key fallback or creating another release tag |
| Wrong shared action identity | Nonexact SHA or mismatched action repository/ref | Fail before preparation |

</frozen-after-approval>

## Code Map

- Builds owner is `references/Hexalith.Builds`, clean main fast-forwarded to 3734acb. Read its ordinary guidance/config; never load skills from references. Its pinned Folders gitlink remains 611f7e8 and must be restored after shared work without discarding changes. Do not change package catalogs.
- Builds `.github/workflows/domain-release.yml`: regular job lines 277-475 already supplies approved checkout, root-only bootstrap, SDK/Node, npm signatures, Release restore/build, exact publication freeze and late current-source proof. Existing legacy/governed callers and permissions must remain intact.
- Add shared `Github/prepare-domain-release/action.yml`: preparation only, for ordinary caller-owned jobs using exact-source CI as test authority. Reuse the above behavior, pin all external actions, validate action repository/ref via composite GitHub contexts and builds-execution-sha. Composite run steps require shell. Return publish-enabled output. Validate solution, positive package count, manifest, source main, caller checkout/source, nonblank NuGet creator when enabled. Check exact push CI as well as current main before login. Reuse pinned Builds initialize-build, nonrecursive only. Avoid containers/governed surface expansion.
- Folders `.github/workflows/release.yml`: retain verify-source; replace reusable release job with runs-on ubuntu-latest, timeout60, environment production, minimal contents/issues/PR write, actions read and id-token write. Checkout exact dispatched source with full history and no credentials/submodules; call shared preparation action pinned full SHA; conditional NuGet/login@8d196754b4036150537f80ac539e15c2f1028841 with vars.NUGET_USER; conditional semantic-release env NUGET_API_KEY exclusively login output, GITHUB_TOKEN and existing HEXALITH release identity/preflight inputs. No secrets mapping. Ordinary final hooks still revalidate boundaries.
- During local implementation keep b93e988 as a clearly recorded provisional Builds execution pin; parent will commit/push shared action first then replace every old pin in caller, script and tests with the final approved shared SHA before publishing Folders.
- `deploy/nuget/trusted-publishing-policy.yaml`: concrete repository policy definition, not native NuGet API import. Owner Hexalith; publisher GitHub, owner Hexalith, repository Hexalith.Folders, workflow release.yml, environment production, profile variable NUGET_USER, new-version scope and exact five IDs matching JSON manifest. Document NuGet account registration status separately, never imply registered.
- `scripts/validate-publication-preflight.sh`, `scripts/tests/test_ci_cd_tooling.py`: exact execution pin and behavioral source/package checks. Update wording from reusable workflow identity to shared action identity without weakening checks.
- Contracts Deployment ReleasePackageConformanceTests and NfrTraceabilityConformanceTests: release shape changes require exact composite/action pin, production/permissions, login-to-publication ordering, no secret fallback, policy matching inventory. Search other conformance assertions before changing workflow.
- `docs/operations/release-packages.md`: explain trusted publishing setup, creator vs package owner, exact policy fields, temporary key, production/source authority and failed-tag recovery. Add Builds preparation README and relevant shared CI test registration for meaningful behavioral tests.

## Tasks & Acceptance

- [x] Implement and validate shared preparation action and behavior tests for matrix branches, keeping existing shared release contracts passing.
- [x] Switch Folders Release to caller-owned OIDC publication, add exact policy definition and matching conformance assertions, update operational documentation.
- [x] Run actionlint, shared focused tests, Python tooling and affected Contracts tests. All must pass without weakening unrelated gates.
- [ ] Parent: validate commits with each owner's pinned commitlint, push shared commit, repin Folders and restore original root gitlink working-tree checkout, push Folders, verify exact-source hosted CI.
- [ ] Parent: register actual NuGet policy and set NUGET_USER after creator/access is supplied; dispatch and normally approve Release, verify five version archives and ten GitHub assets. If authenticated NuGet access is unavailable, report the exact remaining account action with ready tested configuration.

## Implementation Notes

The browser tool returns Transport closed and no authenticated NuGet session is currently available. A username question is pending; independent configuration work continues. Registration/publication remains externally blocked until legitimate account access or user registration is available.

The user supplied creator `jpiquot`. The parent configured and read back repository variable `NUGET_USER=jpiquot`. The account UI requires sign-in; repeated browser JavaScript calls and service reset fail before browser execution with `Transport closed`, whose underlying cause is unavailable. The user has been given the exact registration fields; actual remote policy creation is not confirmed.

Implementation finished in both owners with no catalog or legacy reusable-workflow changes. Shared local commit `eb17c93ac377520a1807a34b2f701ca67c36d2e2` has passed its owning pinned commitlint CLI. Caller, preflight, Python fixture and Contracts constant now use that exact execution SHA; the provisional workflow comment was removed. The code review diff excludes the two implementation specifications and the temporary root gitlink checkout state so only the edge-case reviewer receives intent claims.

Before repinning: Release actionlint and synthetic composite lint passed; shared preparation fixtures passed 7 tests; existing shared test-platform contracts passed 169 assertions; root Python tooling passed 37 tests in 102.514 seconds; affected Contracts Release/package-mode build passed with zero warnings/errors; direct ReleasePackageConformanceTests passed 9 facts and NfrTraceabilityConformanceTests passed 17 facts. Both owner whitespace checks passed. Final pin replacement is followed by narrow pin-sensitive verification.

Final-pin verification (2026-10-02): shared commit `eb17c93ac377520a1807a34b2f701ca67c36d2e2` is the live `Hexalith/Hexalith.Builds` `main` tip (`git ls-remote`), and its hosted Commitlint run passed. Its message revalidated with the Builds pinned commitlint CLI. The Folders root gitlink checkout was restored to `611f7e882ec615bad534de73149c7e95431b4cd4`; the Folders change does not bump the gitlink, because the release job consumes the shared action remotely by full SHA. Against the final pin, `actionlint .github/workflows/release.yml` passed, `python3 Tools/test-prepare-domain-release.py` passed 7 tests, and `git diff --check` passed in both owners. The Release-mode Contracts build passed with zero warnings/errors. Direct ReleasePackageConformanceTests plus NfrTraceabilityConformanceTests passed 26/26, and the full Contracts assembly passed 343/343 both before and after the gitlink checkout restore. `python3 -m unittest discover -s scripts/tests -p 'test_*.py'` passed 37 tests in 114.752 seconds. The `NuGet/login` pin `8d196754b4036150537f80ac539e15c2f1028841` is the `v1.2.0`/`v1` tag commit and exposes output `NUGET_API_KEY`. Microsoft Learn's nuget.org trusted publishing page confirms policy scopes, package glob patterns, owner selection, and the optional environment field.

## Spec Change Log

## Review Triage Log

All three independent layers returned before triage. Findings B1-B8 are blind review, E1-E2 edge review, and V1 verification review.

| Finding | Verdict and disposition | Evidence |
| --- | --- | --- |
| B1: repository versus inherited freeze variable | medium; patch documentation | GitHub resolves repository/org variables before the shell sees them, so the new claim that a missing repository value always freezes is inaccurate. Folders currently has its own explicit true value. Describe effective resolution and explicit repository overrides without changing the proven gate. |
| B2: project aliases | low; reject extra guard | The shared guard distinguishes strings, but Folders `release_package_contract.load_manifest` resolves projects and rejects noncanonical/duplicate paths before packing and tag creation. No current publication bypass occurs; additional canonicalization is unnecessary complexity for this auth repair. |
| B3: symlink containment | low; reject extra guard | The same loader resolves each project and requires containment beneath the root-owned src directory. An escaping symlink cannot reach publication; source is independently qualified before entering production. |
| B4: duplicate JSON properties | low; reject extra guard | Folders loads its manifest with reject_duplicate_keys before packing. Ambiguous JSON is rejected before a release tag or package publication; duplicating that parser in the shared preparation guard adds unnecessary complexity. |
| B5: malformed feed responses | high; pre-existing issue, pursued under original CI/CD repair request | The existing HTTP 200 branch puts jq parsing/schema failures inside an if, treating failure as version absence. Add explicit response-schema validation and malformed-response fixtures before any upload. |
| B6: composite orchestration verification | medium; patch, grouped with V1 | Bash fixtures verify producer outputs but not the caller-visible export. Add a test resolving the declared composite output against the executed step outputs; manual actionlint already verifies action syntax. |
| B7: discarded failed-release evidence | medium; patch | The new caller-owned job has no retained report/packages, and the preceding failed release showed why those bytes are needed for publication diagnosis. Retain the exact metadata report and package/symbol allowlists in separate artifacts after failures. |
| B8: NuGet organization membership setup | low; direct documentation patch | An organization-owned policy requires the creator's active membership. Document that conditional prerequisite and removal behavior without asserting membership was independently verified. |
| E1: repeated-slash aliases | low; reject, same cause as B2 | Reproduction confirms shared preparation accepts the strings; the downstream normalized Folders manifest loader rejects the alias before packing. |
| E2: concatenated JSON documents | low; reject extra guard | jq accepts a stream, but Folders JsonDocument CI, local preflight count comparison, and strict json.load packaging reject concatenated documents before publication. Adding another generic parser guard does not improve a reachable current publication path. |
| V1: composite publish-enabled export | medium; patch, grouped with B6 | Mutation of the export to constant false leaves all seven producer tests green and would silently skip login/publication. Resolve the action's declared output binding in the behavior harness and assert enabled and frozen consumer results. |

Shared CI on remotely available eb17c93 passed Commitlint/Python/CodeQL but failed the existing package-version-audit gate. Its snapshot still records an older Dapr version and pre-normalization AppHost declarations. Refresh only affected audit families against the already committed catalog/declarations, with no catalog/version changes, and validate it before repinning the final shared correction.

The incremental refresh rejected inconsistent preserved xUnit consumer evidence. A complete audit refresh corrected the existing snapshot against the unchanged committed catalog and declarations: 300 packages and 146 families. The audit validator and all 15 exception allowlists pass. The only selected-version correction in the evidence is the upstream catalog's already committed Dapr preview; no catalog or dependency version was edited. Shared correction `f1c5f774975e1d9ffb77ef7e70d560f5e9ba8d3f` is pushed, and all four caller/prefight/test pins now use it. The Builds checkout is restored to the original `611f7e8` root gitlink. Review patches also pass eight shared preparation tests, nine publication-preflight tests, and nine Release conformance facts; final full tooling and Contracts verification is in progress. B5 is recorded as resolved in the deferred-work ledger.

Final local verification after all review patches and the final shared pin: `actionlint .github/workflows/release.yml` and `git diff --check` pass; all 38 Python tooling tests pass in 98.409 seconds. A Release/package-mode Contracts build with build servers disabled passes with zero warnings/errors, and an isolated direct assembly run passes all 343 tests without skips. Two earlier local build invocations stalled; only task-owned invocations were stopped, and final verification used the already restored package graph with `--no-restore --disable-build-servers -p:UseNuGetDeps=true -p:UseSharedCompilation=false -p:BuildInParallel=false -m:1 -nodeReuse:false`. Hosted exact-source CI and actual account registration/publication remain pending.

## Verification

Hosted verification on `116464a2e1e4697ae964efb4a0e358e609eb992d`: CI `36983007505`, Commitlint `36983007548`, CodeQL `36983007527`, and contract-spine `36983006930` all passed. Shared `f1c5f77` CI `36982328464` and Commitlint `36982328465` passed. Release `36983740518` passed exact-source proof and received normal production approval, but the runner rejected the composite before checkout: `Unrecognized named-value: vars` for its publication environment expression. No NuGet login, new tag, or upload occurred; all five public indexes still list only `1.0.0`.

The runtime correction adds an optional shared `publication-flag` input defaulting to empty. Folders resolves `vars.HEXALITH_RELEASE_PUBLISH_ENABLED` in the caller and passes that raw value; the composite binds its internal environment exclusively to the input. The exact case-sensitive/untrimmed shell gate and exported enabled/frozen verdict stay unchanged. Regression coverage checks the declaration, environment binding, omitted input freezing, absence of unsupported direct composite vars expressions, and the caller's variable mapping. This is a patch within the existing intent, with no frozen-boundary change.

Shared runtime correction `3639c8d9340fc81d6f8e0a90566a97e56d5d8446` is pushed after pinned commitlint validation. Its nine focused fixtures pass. Folders uses this SHA consistently in the caller, shell preflight, Python fixture, and Contracts constant; the original root gitlink remains unchanged. Final actionlint and whitespace checks pass; the no-restore Release/package-mode build has zero warnings/errors; all 343 Contracts tests pass; all 38 tooling tests pass in 106.054 seconds. A fresh exact-source hosted CI run and Release attempt follow the corrective Folders commit.

- `actionlint .github/workflows/release.yml`; lint the shared composite through a synthetic caller where necessary.
- Shared focused behavior fixtures exercise exact freeze values, identity mismatch, missing creator, invalid source and exact push-CI outcomes without live mutation.
- `python3 -m unittest discover -s scripts/tests -p 'test_*.py'` and focused Contracts project build/direct xUnit class run.
- Retain publication preflight duplicate/staleness/manifest checks and unchanged legacy domain workflow platform contract tests.
