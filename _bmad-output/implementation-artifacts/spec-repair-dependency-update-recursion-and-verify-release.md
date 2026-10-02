---
title: 'Repair dependency-update recursion and verify the NuGet release'
type: 'bugfix'
created: '2026-10-02'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '92be75261e789139e4ded95faf173a982de52718'
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '{project-root}/docs/operations/release-packages.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

The user requests CI/CD inspection, correction of workflow errors, execution of Release, and verification of NuGet publication without additional confirmation. Current main passes CI, Commitlint, CodeQL and contract-spine gates. Dependabot npm and GitHub Actions updates fail while cloning circular source-submodule references, before any dependency update. Prevent implicit cloning of the seven cyclic root source submodules while keeping all declared root dependencies available through explicit, nonrecursive initialization. Complete remote qualification and publication through the existing release workflow after the correction is committed.

## Boundaries & Constraints

Preserve every test, coverage, browser and governance gate, Release/NuGet dependency mode, root submodule inventory and gitlink revisions, package pins, five-package release manifest, immutable Builds release reference and publication checks. The existing authenticated reviewer may approve production through GitHub's normal review API. No environment protection removal or invented approval evidence is authorized or needed. The user has authorized the release, so a redundant skill confirmation is unnecessary; commit and push the validated fix as required to execute it in GitHub Actions.

Do not initialize or update actual nested submodules, change another repository, upgrade packages, weaken source-mode probes, retry a partially occupied package version, or add duplicate-skipping publication. Only generated temporary fixture repositories may contain synthetic nested modules for isolated regression testing.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected behavior |
|---|---|---|
| Implicit updater clone | Root source submodules configured with update=none | Cyclic roots stay uninitialized; cloning stops before nested sources |
| Explicit source initialization | Nonrecursive update --init with --checkout override | All selected root sources are present; nested sources remain uninitialized |
| Package-only shared lane | Plain nonrecursive update --init | The four acyclic roots including Builds remain available and package-mode tests remain unchanged |
| Unsafe setup documentation | Recursive/remote flags, duplicate checkout override or wrong root operands | Canonical setup validation rejects the command |
| Complete release | Qualified current main and valid production review | Existing workflow publishes exactly five same-version packages and ten release assets |

</frozen-after-approval>

## Code Map

- `.gitmodules`: eleven root dependencies. Seven cyclic modules are Tenants, EventStore, FrontComposer, Memories, Projects, Platform and McpCli. AI.Tools, Builds, Commons and PolymorphicSerializations form an acyclic reference graph and supply package-mode catalog files.
- `.github/workflows/{ci,contract-spine,nightly-drift,policy-conformance}.yml`: local initialization commands; add --checkout without recursive or remote flags. The shared CI/release workflows keep their pinned/current initialization behavior.
- `tests/Hexalith.Folders.Contracts.Tests/Deployment/*CiWorkflowConformanceTests.cs`: seven exact-command assertions bind local root initialization. Scheduled workflow assertions also require bounded root operands.
- `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs`: canonical setup parser at IsCanonicalRootInitCommand must accept precisely one checkout override and still reject unsafe/noncanonical variants; CanonicalInitCommand produces test examples.
- `README.md`, `tests/README.md`, and `docs/**/*.md`: current operator setup snippets. Append an explicit checkout override to safe initialization snippets while preserving prohibited recursive examples and approved policy artifacts. Explain implicit default versus full root-source setup in README and release documentation.
- `scripts/tests/test_ci_cd_tooling.py`: behavioral tooling suite; isolated Git fixtures can prove implicit skip, explicit root checkout and no nested initialization.
- `.github/workflows/release.yml`, `.releaserc.json`, `tools/release-packages.json`: already validated publication pipeline; preserve these publication contracts.

## Tasks & Acceptance

- [x] Configure update=none for the seven cyclic source roots in `.gitmodules`, retaining all paths, URLs and revisions.
- [x] Add nonrecursive --checkout to local workflow source initialization, relevant setup documentation and exact workflow conformance assertions. Retain all selectors and prohibitions.
- [x] Update the canonical command parser and its examples to require a single explicit checkout override and reject unsafe alternatives.
- [x] Add isolated behavioral Git regression tests covering implicit source skip, explicit checkout override, acyclic default availability and nested-source absence. Tests must not initialize real nested dependencies.
- [x] Run actionlint, Python tooling tests, focused full Release Contracts and Testing suites, and package-mode validation appropriate to the changed setup. Record exact commands/results; no gate suppression.
- [ ] Root operator: validate a Conventional Commit, commit/push the correction, wait for successful exact-source CI, run both scheduled lanes, dispatch Release, approve the existing production review as the authorized account, and verify all five NuGet versions plus ten GitHub release assets and source tag. Implementation agent must leave this remote task to the root operator.

Acceptance: Given the cyclic reference graph, automatic dependency-update checkout terminates before those roots; given explicit source setup, the same source-mode probes still pass and no actual nested checkout is created. Given a current green main SHA, Release completes and the exact manifest package set is independently downloadable from NuGet.org.

## Implementation Notes

- Read-only audit found no errors in release phase wiring. Actionlint passes; Python tooling passes 25/25 and npm verifies 504 registry signatures and 127 attestations.
- Initial exact-source CI 36968015711, contract-spine 36968014916, Commitlint 36968015510 and CodeQL 36968015498 pass. NuGet indexes currently expose 1.0.0 for every manifest ID.
- Dependabot logs demonstrate git clone --recurse-submodules --shallow-submodules and repeated EventStore/FrontComposer recursion; both updater jobs failed before updating dependencies.

### Local implementation and verification (2026-10-02)

- Added `update = none` to exactly the seven cyclic roots. Updated local workflow setup, current operator setup snippets and their conformance assertions to use `--checkout` before root operands. The canonical parser requires that single executable override and the unchanged exact root inventory; unsafe flags, duplicate overrides and wrong operands remain rejected.
- Added four temporary Git-fixture tests. They exercise the updater's recursive clone flags, shared default nonrecursive initialization, explicit bounded setup after Git copies the skip policy, and the local workflow command. Every source fixture contains a synthetic nested module; explicit setup checks each root's exact commit and verifies nested checkout absence. No actual nested submodule was initialized or updated.
- The first Python run exposed Git's option ordering: a trailing `--checkout` after module operands is interpreted as a pathspec. Moved the override before operands and updated the exact documentation assertions; all four fixture tests then passed.
- The first complete Contracts run had 339 passes and three failures because the pending PD10 candidate manifest binds changed workflow/documentation/test bytes. Regenerated the candidate using its existing generator. Only byte counts/hashes for six existing artifacts and the aggregate digest changed; every artifact path, approval state, production exposure/routing flag and historical/authorization digest stayed unchanged. Final complete Contracts run passed 342/342.
- The first complete Testing run had 67 passes and one failure because the expanded release documentation moved the directed nested-initialization warning away from its snippets. Restored the warning immediately before each snippet without changing the prohibition detector; final complete Testing run passed 68/68.
- The root operator independently verified the default four-root graph at exact pinned gitlink commits: eight unique repository/commit nodes, all acyclic. Root inventory, gitlinks, package pins, shared workflow references, publication workflow/configuration and release inventory remain unchanged.

Exact final verification commands and results:

```text
actionlint
# exit 0
python3 -m unittest discover -s scripts/tests -p 'test_ci_cd_tooling.py' -k RootSubmoduleCheckoutTests
# exit 0; 4 tests passed
python3 -m unittest discover -s scripts/tests -p 'test_*.py'
# exit 0; 29 tests passed, zero skips; 112.976 seconds
dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --configuration Release -p:UseNuGetDeps=true -warnaserror -m:1
# exit 0; zero warnings/errors
dotnet tests/Hexalith.Folders.Contracts.Tests/bin/Release/net10.0/Hexalith.Folders.Contracts.Tests.dll -noLogo -noColor
# exit 0; 342 passed, zero errors/failures/skips/not-run
dotnet build tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj --configuration Release -p:UseNuGetDeps=true -warnaserror -m:1
# exit 0; zero warnings/errors
dotnet tests/Hexalith.Folders.Testing.Tests/bin/Release/net10.0/Hexalith.Folders.Testing.Tests.dll -noLogo -noColor
# exit 0; 68 passed, zero errors/failures/skips/not-run
dotnet restore tests/Hexalith.Folders.UI.Tests/Hexalith.Folders.UI.Tests.csproj -p:Configuration=Release -p:UseNuGetDeps=true -m:1
# exit 0
dotnet build tests/Hexalith.Folders.UI.Tests/Hexalith.Folders.UI.Tests.csproj --configuration Release -p:UseNuGetDeps=true --no-restore -warnaserror -m:1
# exit 0; zero warnings/errors
dotnet tests/Hexalith.Folders.UI.Tests/bin/Release/net10.0/Hexalith.Folders.UI.Tests.dll -noLogo -noColor
# exit 0; 544 passed, zero errors/failures/skips/not-run
dotnet format whitespace Hexalith.Folders.CI.slnx --verify-no-changes --no-restore --include tests/Hexalith.Folders.Contracts.Tests/Deployment/ tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs
# exit 0; generic workspace-load warning, no whitespace failures
python3 scripts/generate-pd10-v2-conformance-set.py --repository-root .
# exit 0; deterministic candidate metadata regeneration
git diff --check
# exit 0
```

The following exact evaluations also exited 0. Their JSON properties were checked against the existing baseline expectations: default/Debug use all five source probes; explicit Release and standard `CI=true` use package references with every source probe empty.

```text
dotnet msbuild tests/Hexalith.Folders.UI.Tests/Hexalith.Folders.UI.Tests.csproj -getProperty:Configuration,UseHexalithProjectReferences,UseNuGetDeps,HexalithEventStoreFromSource,HexalithTenantsFromSource,HexalithMemoriesFromSource,HexalithFrontComposerFromSource,HexalithFrontComposerTestingFromSource -p:CI=false
dotnet msbuild tests/Hexalith.Folders.UI.Tests/Hexalith.Folders.UI.Tests.csproj -getProperty:Configuration,UseHexalithProjectReferences,UseNuGetDeps,HexalithEventStoreFromSource,HexalithTenantsFromSource,HexalithMemoriesFromSource,HexalithFrontComposerFromSource,HexalithFrontComposerTestingFromSource -p:CI=false -p:Configuration=Debug
dotnet msbuild tests/Hexalith.Folders.UI.Tests/Hexalith.Folders.UI.Tests.csproj -getProperty:Configuration,UseHexalithProjectReferences,UseNuGetDeps,HexalithEventStoreFromSource,HexalithTenantsFromSource,HexalithMemoriesFromSource,HexalithFrontComposerFromSource,HexalithFrontComposerTestingFromSource -p:Configuration=Release -p:UseNuGetDeps=true
dotnet msbuild tests/Hexalith.Folders.UI.Tests/Hexalith.Folders.UI.Tests.csproj -getProperty:Configuration,UseHexalithProjectReferences,UseNuGetDeps,HexalithEventStoreFromSource,HexalithTenantsFromSource,HexalithMemoriesFromSource,HexalithFrontComposerFromSource,HexalithFrontComposerTestingFromSource -p:CI=true
```

The remote commit/push, exact-source CI and scheduled lane qualification, protected Release approval, and five-package/ten-asset publication verification remain assigned to the root operator.


## Review Triage Log

All three independent layers completed against the same implementation diff. Each finding was assessed before grouping; the arithmetic item from the blind reviewer is its required finding-floor calculation rather than a defect.

| Finding | Verdict | Evidence and route |
|---|---|---|
| Blind 1: retention script expects obsolete setup text | high | Verified `Assert-TenantDeletionDocs` still requires the exact old command at line 343 while the operations document uses the new flag; CI calls this blocking gate. Patch its expected executable command. |
| Blind 2: uppercase checkout option accepted | low | The new flag uses OrdinalIgnoreCase although Git accepts the lowercase option only; the edge reviewer reproduced acceptance through the built assembly. Patch that comparison and add its negative example. |
| Blind 3: local fixture URLs suppress shallow cloning | low | Git ignores depth for local-path clones, so the recursive fixture exercises traversal but not the updater's shallow transport. Patch fixture URLs to file URIs, use the updater's root clone flags and assert shallow roots. |
| Blind 4: fixtures do not audit future dependency graphs | low | The fixtures deliberately model checkout policy rather than arbitrary future gitlink graphs. The exact current pinned four-root graph was separately audited across eight unique repository/commit nodes and is acyclic. Reject adding a new dynamic graph auditor: it would introduce extra behavior to guard an unobserved future dependency change; future gitlink changes require their own qualification. |
| Blind 5: fixture gitlinks always equal upstream HEAD | low | Exact SHA assertions currently cannot distinguish the recorded revision from branch-tip selection. Patch the fixture by advancing upstream after recording gitlinks and assert exact recorded revisions for default and explicit setup. |
| Blind 6: skipped object databases are not asserted absent | low | Working-tree absence alone would not establish that the original clone failure path was avoided. Patch a direct assertion that skipped roots have no object databases under the superproject's Git directory. |
| Blind 7: default fixture lacks harmless nested dependencies | low | The plain-init test has no nested package root to reveal accidental recursive initialization. Add a finite leaf module to package fixtures and assert its absence specifically after nonrecursive initialization. |
| Blind 8: existing local update strategy precedence omitted | low | Git copies .gitmodules settings only for unconfigured roots; an existing explicit local checkout strategy takes precedence. Add the concrete precedence note and a small behavioral fixture. Preserve all actual user Git settings. |
| Blind 9: full-source example lacks explicit operands | medium | An operand-free command obeys submodule.active and can omit selected roots. Replace the new full-source documentation example with the canonical explicit root inventory. Hosted runners have no custom active selection and keep their existing source setup scope. |
| Blind 10: subsequent source update command omitted | low | A one-shot checkout override leaves stored update=none intact, so a later plain update skips changed source gitlinks. Document the repeated canonical checkout command and exercise a subsequent recorded revision change in the temporary fixture. |
| Edge 1: uppercase checkout option accepted | low | Same reproduced bad outcome as Blind 2 at the new option comparison. Group with that patch; retain this separate finding row. |
| Verification 1: retention documentation command mismatch | high | Independently reproduced by executing Assert-TenantDeletionDocs; the CI caller and exact stale command are present. Group with Blind 1; retain this separate finding row. |

The surviving entries are direct corrections or small regression-fixture improvements with no new public interface. The existing implementation agent is assigned their minimal patches. No intent or spec loopback is required and no publication gate is relaxed.


### Review corrections verified

All accepted direct corrections are applied. The retention script requires the executable checkout command, and the canonical parser rejects uppercase `--CHECKOUT`. Temporary fixtures now use actual shallow file transports, advance upstream beyond pinned gitlinks, check clone database absence, include finite nested package leaves, and exercise existing local strategy precedence and subsequent gitlink updates. Full source documentation uses explicit canonical root operands. Pending candidate regeneration produced identical bytes after these corrections.

Post-review results: actionlint and diff checks pass; the root operator reran all six Git fixtures (3.822 seconds), full Contracts (342/342), and full Testing (68/68), with no failures or skips. The implementation agent separately ran `pwsh -NoProfile -File tests/tools/run-retention-deletion-gates.ps1` successfully, the 16 Scaffold tests and three pending-candidate tests. Conventional commit validation succeeded with `npx --no-install commitlint --edit /tmp/folders-ci-recursion-commit-message.txt`. There are no unpatched accepted findings. The proposed future graph auditor was rejected with the current pinned acyclic graph evidence recorded above; no deferred ledger entry is needed.
