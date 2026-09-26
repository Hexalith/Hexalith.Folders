---
title: 'Prepare the authorized Story 1.17 v2 closure candidate'
type: 'feature'
created: '2026-09-24'
status: 'done'
baseline_commit: '19c29e00eb6679bbf7a0d20a8b4dbf30182243cf'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The digest-bound PD10 v2 candidate is not ready for Story 1.17 closure: eight approved must-fix findings remain, production still selects v1, and GENERATE delivery evidence remains open.

**Approach:** On the isolated Story 1.17 branch, fix the candidate, prepare the v2 cutover, refresh consumer discovery, and reseal its inventory for later decisions. DEC-EXEC-1.17 authorizes this scope; the stopped draft remains historical.

## Boundaries & Constraints

**Always:** Preserve v1 evidence and main's approved bytes. Keep 49 operations, fourteen access states, eleven protected families, canonical 401/404/503 outcomes, authorization before observation, and reauthorization before effects. Generate derived outputs from their owners; prove security corrections red-first. Keep Story 1.17 backlog during execution.

**Never:** Merge, activate, expose, or publish v2; close Story 1.17; infer A6b/Section 9/A8 or exposure approval; edit `sprint-status.yaml`; change dependencies or submodule pins; or start another story. Closure needs explicit approvals and a targeted tracker delta.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Mixed authority | Revoked and stale evidence in either order | One order-independent authority-unavailable result before lookup | Canonical retryable 503 |
| Effect boundary | ACL or delegation revoked after initial allow | No repository/provider/event effect | Canonical 404 or 503 from fresh evidence |
| Provider readiness | Authorized operator success | Generated SDK reads the operator variant | Typed 200, no deserialization failure |
| Repository create/bind | Readiness failure or unknown outcome | Declared operation-bound tuple and exact details survive seam | 422 or reconciliation response, not generic 503 |
| UI reads | Each v2 operation | Send its accepted freshness class | No avoidable validation 400 |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Folders.Server/Pd10V2CandidateCompatibilitySeam.cs` -- mixed states, event 1018, operation-bound responses; reuse authorization/audit seam.
- `src/Hexalith.Folders/Authorization/LayeredFolderAuthorizationService.cs`, `src/Hexalith.Folders/Aggregates/Folder/`, `src/Hexalith.Folders/Aggregates/Organization/`, `src/Hexalith.Folders.Server/FolderDomainProcessor.cs` -- final ACL/scope and archive actor; preserve synthetic create scope.
- `scripts/generate-pd10-v2-contract.py`, `scripts/generate-pd10-v2-runtime-catalog.py`, `src/Hexalith.Folders.Client/Generation/GeneratedClientPostProcessor.cs` -- own tuples and readiness union; regenerate derived outputs.
- `src/Hexalith.Folders.UI/Components/Pages/` -- align affected read calls with generated per-operation freshness values; preserve FrontComposer/Fluent UI.
- `tests/Hexalith.Folders.IntegrationTests/EndToEnd/GoldenLifecycleParityTests.cs`, `tests/Hexalith.Folders.{Server,Contracts,Cli,Mcp,UI}.Tests/` -- seam, service, SDK, adapter, generator, UI tests.
- `src/Hexalith.Folders.Server/Program.cs`, `docs/contract/pd10-v2-consumer-discovery.md` -- prepare branch-only v2 cutover and refresh external-consumer evidence; do not deploy. `UsePd10V2CandidateCompatibilitySeam` (seam line ~119) is opt-in middleware that rewrites `/api/v2` onto v1 endpoints. Only test hosts compose it today (`GoldenLifecycleParityTests.cs` ~3053, `MixedSurfaceHandoffTests.cs` ~1082). Reuse the seam's canonical problem writer for the retired-v1 404.
- `tests/fixtures/`, `_bmad-output/planning-artifacts/generated-v2-conformance-set-2026-09-17.yaml` -- seal generator outputs last.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/spec-1-17-generate-pd10-v2-relock-milestone.md` -- reconcile its open GENERATE evidence; record only supported acceptance state.
- [x] `src/Hexalith.Folders.Server/Pd10V2CandidateCompatibilitySeam.cs`, `src/Hexalith.Folders/Authorization/` -- fix F03/F04/F11/F13 with negative-order, revoked-ACL, secret-bearing-log, and archive-identity regressions first.
- [x] `scripts/generate-pd10-v2-contract.py`, `src/Hexalith.Folders.Client/Generation/GeneratedClientPostProcessor.cs` -- fix F14/F16 from source, regenerate v2/catalog/client, and invert the readiness failure fixture to a typed success.
- [x] `src/Hexalith.Folders.UI/Components/Pages/` -- fix F15 and assert exact freshness arguments for all affected calls.
- [x] `tests/Hexalith.Folders.IntegrationTests/EndToEnd/GoldenLifecycleParityTests.cs` and test-project peers -- cover F08: operation-specific freshness, 409 remaps, final reauthorization races, case-variant JSON duplicates, absent-from-v1 parity, baseline-init validation, and MCP lock conflict.
- [x] `src/Hexalith.Folders.Server/Program.cs`, `src/Hexalith.Folders.Server/` (new routing-mode type(s), one type per file), `docs/contract/pd10-v2-consumer-discovery.md` -- prepare the branch-only v2 route under owner-accepted plan B:
  - Add one validated configuration setting (e.g. `Folders:ApiRouting:Mode`) with three values. `V1Only` is the default and the executable hold state, with today's composition. `Coexistence` composes `UsePd10V2CandidateCompatibilitySeam()` before endpoints and keeps v1. `V2Only` keeps the seam and retires external v1.
  - In `V2Only`, an external `/api/v1/*` request gets the canonical 404 before any authorization, lookup or effect. The seam's own internal v1 dispatch keeps working (D-5); distinguish it with a seam-set marker, not a client-controllable header.
  - An unknown or empty mode value fails host startup.
  - Guard tests in `tests/Hexalith.Folders.IntegrationTests/` compose the real `Program` pipeline or its extracted composition method in a bootable environment (Production has no repository, so Production can't boot), and cover all three modes:
    - v2 is not routed in hold;
    - v1 and v2 both work in coexistence;
    - external v1 gets the canonical 404 in retired, while v2 calls that dispatch through v1 still succeed;
    - an invalid mode value is rejected.
  - Refresh consumer discovery to cite plan B, the modes, and the fact that no mode change is deployed. Add new source/test paths to `scripts/pd10-v2-story-owned-paths.txt`.
- [x] `_bmad-output/planning-artifacts/generated-v2-conformance-set-2026-09-17.yaml`, `_bmad-output/implementation-artifacts/story-1-17-current-candidate-technical-readiness-2026-09-24.md` -- reseal last:
  - Reproduce the inventory twice, byte-identically.
  - Rerun the readiness document's A6b and Section 9 checks on the new bytes and append a dated addendum with the new digests. Don't rewrite earlier text or any approval record.
- [x] `tests/fixtures/parity-contract.yaml`, `_bmad-output/planning-artifacts/generated-v2-conformance-set-2026-09-17.yaml` -- run affected suites, twelve parity categories and both build profiles; regenerate and reproduce inventory byte-identically last.

**Acceptance Criteria:**
- Given a protected operation, when authority fails or changes, then canonical denial/unavailability precedes protected reads/effects and the final check honors fresh ACL/scope.
- Given F08, F11 and F13, when focused regressions run, then each behavior is pinned through its owner or the real seam.
- Given generated v2 outputs and route preparation, when gates run, then reachable tuples are declared and operator readiness and UI reads succeed. The composed host defaults to the hold mode, where v1 is routed and v2 is not. Coexistence routes both. In the retired mode, v1 is internal-only, so branch production selects no external v1 route.
- Given final generation, when the story-owned inventory is reproduced, then paths and hashes match; historical v1, OQ3, approvals, main, and sprint-status remain unchanged.

## Implementation Notes

- 2026-09-24: Corrected F03/F04/F11/F13 in the candidate seam and final authorization paths; regenerated F14/F16 contract, runtime catalog, and SDK outputs from their owners; aligned F15 UI freshness calls and sample readiness DTO; added F08 regression coverage in the owning test projects. The generated SDK now admits the operator readiness success and the exact repository create/bind reconciliation tuples, including `details.finalState`.
- Consumer discovery records successful production Folders deployments `6546802692`/`6546836679`, published Folders Client 1.0.0, and Projects production deployment `6547910236` at `c767d38d8ae76f9ad949965ac4870a842c699f9c` using the pinned v1 client. See `docs/contract/pd10-v2-consumer-discovery.md`. A consumer-specific migration decision is required before the branch production route can change; `Program.cs` remains unchanged and the route task remains incomplete.
- The [Projects migration decision proposal](story-1-17-projects-v1-consumer-migration-decision-2026-09-24.md) now compares a coordinated switch, a bounded two-route window, and a continued hold. It recommends a seven-day window for approval, with entry, exit, and rollback checks. This is a proposal; the required consumer-specific, exact-digest, and exposure decisions have not been recorded.
- The GENERATE milestone remains `in-progress`; no A6b/A8/exposure or Story 1.17 closure decision was recorded. The unchanged human-owned A6b register still binds an older candidate digest.
- 2026-09-24 (route preparation, supersedes the earlier "`Program.cs` remains unchanged" note): `Program.cs` now delegates to `FoldersServerHostComposition` (`AddFoldersServerHost` / `UseFoldersServerPipeline`). `FoldersApiRouting.ResolveMode` validates `Folders:ApiRouting:Mode`: an absent value selects `V1Only`, and an empty or non-exact value throws before any registration. `V1Only` keeps the previous pipeline byte-for-byte in order. `Coexistence` composes the seam and then explicit `UseRouting` before `UseAuthorization`. `V2Only` adds `UsePd10HistoricalRouteRetirement` after routing and before authorization. That guard matches `/api/v1` case-insensitively by request path and by selected route pattern, and answers through the seam's canonical 404 writer unless the request carries the seam's object-identity `HttpContext.Items` marker. The seam sets that marker only around its historical dispatch. The file-local `FolderRepositoryStartupAssertion` moved to its own file unchanged. `FoldersProductionAuthenticationTests` now checks the pipeline order in the composition file. Fourteen new or changed paths were added to the story-owned list, which now has 222 entries. The consumer discovery cites plan B, the three modes, and the fact that nothing is deployed.
- 2026-09-24 (reseal): The readiness addendum records A6b passing on the 222-artifact bytes. Section 9 S9-02 fails 19/20 only because the unchanged planning manifest binds the prior conformance-manifest digest, so S9-11 and the A8 technical condition are not met. The spec scopes out editing the execution-authority manifest, so it was not edited; refreshing that one binding needs an owner-directed change.

## Spec Change Log

- 2026-09-24 (human-approved renegotiation, bmad-build): The frozen AC3 said "branch production selects no v1 route". That contradicted owner-accepted plan B, which keeps v1 usable for Projects for up to seven days, while plan C stays the executable hold until entry checks pass. AC3 now requires a three-mode switch that defaults to hold. The route task was made concrete, and a reseal/readiness-addendum task was split out. Prior AC3 text is recorded here. Kept: Never activate or expose v2. The default mode changes no production behavior.

## Review Triage Log

Review 2026-09-24. Layers: blind-hunter (BH), edge-case-hunter (EC), verification-gap (VG). Diff: `19c29e0`..working tree, including owner commits `535ece8` and `2de4279`.

| # | Finding | Verdict | Route | Evidence |
|---|---------|---------|-------|----------|
| VG1 | Composed-host routing tests inject a fixed tenant accessor, so the order of authentication before the seam is untested | medium | patch | Pre-verified; the only pipeline-order test is source-text (`FoldersProductionAuthenticationTests`). |
| VG2 | SDK projection of the new 409 `reconciliation_required`+`finalState` tuple is never exercised | medium | patch | Pre-verified; the parity test uses the raw HttpClient, and the client tests cover only the 503 path. |
| VG3 | Exact-details key-set check moved into `Matches` without an extra-key test | medium | patch | Pre-verified; no mutation adds a `details` key for exact tuples. |
| VG4 | Deny audit on the identity-evaluation-throw path is unasserted | medium | patch | Pre-verified; the test checks status and log only. |
| BH12 | UI freshness literals are not tied to the contract catalog | medium | patch | Pages pass `ReadConsistencyClass` literals; CLI/MCP use `OperationFreshness.TryParse`; no UI test compares them to the catalog. |
| BH6a | Approval package still claims current Section 9 pass and A8 met after the 222-artifact reseal | medium | patch | Package step table says A8 met; the readiness addendum now records S9-02 fail and A8 not met. |
| BH8+BH6c | Consumer discovery omits that a mode change needs a restart, and cites a three-role approval that contradicts the single-owner policy | low | patch | `ResolveMode` runs only at startup; discovery line 18 wording. Fix is a direct text correction. |
| BH6d | `epic-1-context.md` says the v2 candidate "has digest-bound approval" | low | patch | Historical approvals bind the 196-artifact digests (owner commit `2de4279` text); direct correction. |
| BH6b | Planning manifest provenance still binds `e89ee580…`, so S9-02 fails | medium | defer | Real; editing the execution-authority manifest is outside this spec's scope and needs owner direction. |
| BH1 | `references/Hexalith.Projects` is pinned at `3f12e4c`, while docs claim the deployed `c767d38` | medium | defer | Verified via `git ls-tree HEAD`; owner commit `2de4279`. Repin or doc correction is the owner's choice. |
| BH2 | Five other submodule pins bumped without scope or evidence | medium | defer | Verified gitlink changes in `2de4279` versus the spec's no-pin rule; owner-authored. |
| BH3+EC11 | A6b register-coherence assertion removed, so conformance/register drift no longer fails a gate | medium | defer | Removed in owner commit `2de4279` under the single-owner policy. S9-02 provenance still catches manifest drift. Owner decision. |
| BH4+EC9 | `max_age_days` set to 0 and pinned, so approvals never expire | medium | defer | Owner commit `2de4279` governance policy; needs an explicit owner decision to keep or restore expiry. |
| BH5 | Single-owner policy and plan-B acceptance lack the decision note the policy requires; register policy keys dropped | medium | defer | Owner-authored governance in `2de4279`; the note is the owner's to record. |
| BH7 | No version/consumer-attributed route telemetry, and retired-v1 404s are not audited | medium | defer | The migration entry checks need attribution; not in this spec's intent. Existing HTTP request telemetry records paths. |
| BH7b+EC2 | Mode resolved twice, and the `AddFoldersServerHost` return value is discarded | low | reject | Configuration reload between build and pipeline composition is not a realistic path; the fix adds plumbing. |
| EC1 | Mode configured as a section or array boots `V1Only` | low | reject | `V1Only` is the safe hold default; malformed nested config is unlikely; the fix adds a guard. |
| EC3 | Audit-sink failure in the identity-throw path yields 500 | false | reject | Intended fail-closed: an existing parity test asserts 500 when the audit sink fails (`auditSink.Attempts == 1`). |
| EC4 | Untrimmed PrincipalId fails the archive actor equality | low | reject | Principals with surrounding whitespace are not produced by the authenticated identity path; the fix adds normalization. |
| EC5 | Final reauthorization `Covers` fails for folder-less create/binding scope under the seam | false | reject | Services run inside the gateway→`/process` request, where `PreauthorizedRequestContext.Current` is null, so they take `AuthorizeCoreAsync`. |
| EC6 | Generic 409 `finalState` accepts any value | low | reject | Server-provided value; pinning it adds a guard; unlikely in everyday use. |
| EC7+BH11b+VG-other | `ProviderReadinessConsumer` lost `Required.Always` | low | reject | No SDK method returns that DTO any more; the fix changes the post-processor. |
| EC8 | Generator raises KeyError on a missing readiness 200 | low | reject | Loud failure on an invalid source spine is correct. |
| EC10 | Extra-details diagnostic changed from shape to tuple mismatch | low | reject | Diagnostic-string drift only; VG3 pins the new string. |
| EC12 | Spec claims approvals unchanged while the register policy was rewritten | low | reject | The fix is to edit this build's spec. |
| BH9 | Archive actor/principal equality breaks pseudonymous actors; mismatch maps to 400 | false | reject | Production sets `ActorSafeIdentifier = PrincipalId` (`FoldersDomainServiceRequestHandler.cs:68`); the differing values exist only in unit fixtures. The 400 reuses the guard's existing malformed-evidence mapping. |
| BH10 | Negative-state precedence relies on enum order; event 1018 has no exception type | low | reject | `Min()` gives the required order independence; an explicit table adds complexity; omitting the exception is the F11 fix. |
| BH11 | Four OpenAPI components are now unreferenced | low | reject | Cosmetic contract residue. |

## Design Notes

Current approvals bind old bytes. Branch changes require new decisions before closure. Refresh consumer discovery before cutover.

## Verification

**2026-09-24 results:** `dotnet restore Hexalith.Folders.slnx` followed by the Debug solution build passed with zero warnings/errors; `dotnet build Hexalith.Folders.CI.slnx -c Release -m:1` passed with zero warnings/errors. All twelve categories in `run-contract-parity-ci-gates.ps1` passed. Full Core, UI, Integration, and Client suites passed (1,978/1,979 with one environment-skipped Forgejo case; 526/526; 713/713; 326/326). The full Contracts suite passed 341/342; its sole failure is `Oq3AuthorizationMatrixPackageBindsVersionDigestApprovalsAndRuntimePosture`, because the unchanged A6b approval register retains the prior conformance digest. The 200-artifact conformance inventory reproduced byte-identically twice with manifest SHA-256 `3be1e6dad78635fec76dc3935d5aa22cb605931f271178bc54df20e6fc5efa24`, candidate-set SHA-256 `92c5da7553aaa158eb6739885c7398636aae5f7ceb4b78519743ef9a9f06a992`, and historical v1 SHA-256 `3c3c668071cfaad3e6318626c03051039d00771a4d1afe29ab49df28f71ae4e2`. `Program.cs`, historical v1, OQ3, and `sprint-status.yaml` were not edited.

**2026-09-24 follow-up diff audit:** Added an MCP generated-SDK regression for the exact 409 `lock_conflict`/`workspace_locked` projection and a real archive REST→gateway→`/process` regression that injects an actor/principal mismatch after layered authorization and proves the processor returns metadata-only 400 before archive evidence reads or event append. Tightened Provider page assertions to exact per-operation freshness and fixed Workspace cleanup-call indentation. Focused suites passed: MCP `PostSdkMappingTests` 9/9, Integration `ArchiveFolderProcessWiringTests` 22/22, and UI `ProviderPageTests` plus `WorkspacePageTests` 35/35. After these source/test edits, the 200-artifact inventory reproduced byte-identically twice with manifest SHA-256 `f2d99919687c9b7fe9b244e9839bbb629d250a703e8d1905e8b106db642d40e2` and candidate-set SHA-256 `a12027b21833337fdca5d43525ac7d23e06e6b1eda47bc056d3eb5f059387d65`; the required parity gate again passed all twelve categories. The unchanged A6b digest remains a governance failure and the external v1 consumer still blocks production route cutover.

**2026-09-24 pre-matrix diff audit:** Added `ArchiveFolderProcessWiringTests.cs` and five newly split helper files to `scripts/pd10-v2-story-owned-paths.txt`, following the Hexalith one-type-per-file baseline. The four FinalAcl test helpers and the archive mismatch accessor each occupy their own matching `.cs` file; existing nested helpers were untouched. Focused Core `FinalAclReauthorizationTests` passed 2/2 and Integration `ArchiveFolderProcessWiringTests` passed 22/22 after the split. The parity gate passed all twelve categories. That inventory contained 206 artifacts, including all six added paths, with manifest SHA-256 `e13e4773ab7744175da75aff51992a5e10b9b1309029866e3fe4b745311d839d` and candidate-set SHA-256 `db0166cbab55c27f3405b89ce4c19879c759060a8ba0836ef0cfcb4577dfd452`. The A6b governance failure and external v1 consumer route blocker remained open, and `Program.cs` was unchanged.

**2026-09-24 pre-matrix governance check:** `dotnet test tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore --filter FullyQualifiedName~Oq3AuthorizationMatrixPackageBindsVersionDigestApprovalsAndRuntimePosture -v quiet` exited 2 with its single test failing: the human-owned A6b register binds conformance SHA-256 `4c8f33f0cb00afac3a5541eab7b9d30ec15b832d6114316ff672338cc02f5be7`, while the then-current candidate manifest was `e13e4773ab7744175da75aff51992a5e10b9b1309029866e3fe4b745311d839d`. No approval artifact was altered.

**2026-09-24 matrix audit:** Strengthened `CandidateMixedNegativeAndStaleAuthorityIsOrderIndependent` to assert the exact canonical retryable 503 problem for both claim orders, including closed metadata-only details and correlation. The focused Integration test passed 2/2. The 206-artifact inventory reproduced byte-identically twice after this change: current manifest SHA-256 `4e513f83e3cc27412e1e8603503e7488ccf7d25b5709d92473e0f6701674856b`, candidate-set SHA-256 `e26c997ed46b54c3a3872f6e4ef2a9e59aa25ab94125c26c7620c1c48522ed3d`. `Program.cs`, approvals, and sprint-status remain unchanged. The A6b digest and external v1 consumer blockers remain open.

**2026-09-24 current-candidate readiness:** After the owner accepted the [seven-day Projects migration package](story-1-17-projects-v1-to-v2-migration-approval-package-2026-09-24.md), the [current-candidate result](story-1-17-current-candidate-technical-readiness-2026-09-24.md) recorded A6b technical validation, Section 9, and the A8 technical condition as passing under the current single-owner policy. The 206-artifact manifest reproduced twice byte-identically at SHA-256 `e89ee58008e3ff4b34d08eac6e8d6a3d357d7df3ce7b1f13eadd53781e8e500c`; candidate-set SHA-256 is `d2c30653d9292c4d61122620cff5a61ebc1f7de1e2c11a6daf64c11d540a33cd`. Forced Release test-project rebuilds prevented stale assemblies: the full Contracts suite passed 342/342, governance/completeness passed, parity passed 12/12, and both Debug source and Release package solution builds passed with zero warnings/errors. Historical approvals retain their older digest bindings. Projects package/build/test, telemetry, rollback, timing, and deployment entry evidence remains open; `Program.cs`, v2 routing, sprint-status, and the seven-day clock remain untouched.

**2026-09-24 consumer-source pin:** The owner's subsequent instruction to keep a used consumer under `references/` authorizes one exception to this spec's earlier submodule exclusion: the Folders root now declares `references/Hexalith.Projects` and pins the deployed consumer revision `c767d38d8ae76f9ad949965ac4870a842c699f9c`. This is local consumer evidence, not a Folders runtime dependency, a Projects v2 test, or a change to production routing. Nested Projects submodules were not initialized.

**2026-09-24 route preparation and reseal:** `FoldersApiRoutingModeTests` (Integration, 13 cases) boots the real composition in Development on a test server. It covers the hold with the setting absent or `V1Only` (v1 200; v2 unrouted 404 with an empty body and no seam audit), coexistence (v1 and v2 200, one v2 allow audit), retirement (the canonical 404 for exact, upper-case, and bare `/api/v1` paths and a v1 POST, with zero tenant or lifecycle reads, no audit, and no gateway call; v2 lifecycle still 200 through the seam's internal dispatch; `/health/live` 200), a retired-v1 body byte-identical to the seam's undeclared-route 404, and startup rejection of six invalid values. Mutation checks confirmed the tests catch a regression. Removing the seam marker failed the retirement test, and removing the retirement guard failed two tests. The Debug restore and solution build and the Release `Hexalith.Folders.CI.slnx` build passed with zero warnings and errors. `dotnet format` whitespace and analyzers passed for Server, Server.Tests, and Integration. Server passed 757/757, Integration 725/725, and Contracts 342/342 (Release). The parity gate passed 12/12, and the governance gate passed. The 222-artifact manifest reproduced byte-identically three times: SHA-256 `06b13d721cca7407827a69e3cfe20d4654a8098ab7c3759a8e02e1f426edf852`, candidate set `19cc81d1b469d46c5d99f0d3b3703297e4c98f321e98e87d848fdc85ba86a605`, historical v1 `3c3c668071cfaad3e6318626c03051039d00771a4d1afe29ab49df28f71ae4e2`. Historical v1, OQ3, approval records, the planning manifest, and `sprint-status.yaml` are unchanged.

**2026-09-24 review regressions:** Added these regressions:
- Coexistence and V2Only cases that keep the real claims-based accessors behind a test authentication scheme (v2 200 plus one `allow` audit). Moving `UseAuthentication` below the seam fails both.
- A Client theory for the BindRepository and CreateRepositoryBackedFolder 409 `reconciliation_required` tuple with `details.finalState`. Deleting the tuple fails it.
- Extra `details`-key mutations on AuthorityUnavailable, pinned to `exact_problem_tuple_mismatch`. Relaxing the exact key-set check fails them.
- A `deny` / `actor_absent` / `tenant_absent` audit assertion in the golden secret-log test.
- A UI contract test for all 17 page (operation, freshness) pairs through `OperationFreshness.TryParse`.

The regressions pass in focused runs. The consumer discovery now states the restart/redeploy requirement for mode changes and cites the project decision policy. The migration package gained a reseal note. Epic 1 context was corrected on approval scope. Two new paths bring the story-owned list to 222. The resealed manifest reproduced byte-identically: SHA-256 `06b13d721cca7407827a69e3cfe20d4654a8098ab7c3759a8e02e1f426edf852`, candidate set `19cc81d1b469d46c5d99f0d3b3703297e4c98f321e98e87d848fdc85ba86a605`.

**Commands:**
- `dotnet restore Hexalith.Folders.slnx && dotnet build Hexalith.Folders.slnx -m:1` -- expected: Debug project-reference graph builds.
- `dotnet build Hexalith.Folders.CI.slnx -c Release -m:1` -- expected: package-reference graph builds.
- `pwsh tests/tools/run-contract-parity-ci-gates.ps1` -- expected: twelve categories pass.
- `python3 scripts/generate-pd10-v2-conformance-set.py --repository-root .` twice -- expected: second run changes no bytes.
