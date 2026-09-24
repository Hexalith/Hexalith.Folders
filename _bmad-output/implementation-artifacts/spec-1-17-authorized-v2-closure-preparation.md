---
title: 'Prepare the authorized Story 1.17 v2 closure candidate'
type: 'feature'
created: '2026-09-24'
status: 'in-progress'
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
- `src/Hexalith.Folders.Server/Program.cs`, `docs/contract/pd10-v2-consumer-discovery.md` -- prepare branch-only v2 cutover and refresh external-consumer evidence; do not deploy.
- `tests/fixtures/`, `_bmad-output/planning-artifacts/generated-v2-conformance-set-2026-09-17.yaml` -- seal generator outputs last.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/spec-1-17-generate-pd10-v2-relock-milestone.md` -- reconcile its open GENERATE evidence; record only supported acceptance state.
- [x] `src/Hexalith.Folders.Server/Pd10V2CandidateCompatibilitySeam.cs`, `src/Hexalith.Folders/Authorization/` -- fix F03/F04/F11/F13 with negative-order, revoked-ACL, secret-bearing-log, and archive-identity regressions first.
- [x] `scripts/generate-pd10-v2-contract.py`, `src/Hexalith.Folders.Client/Generation/GeneratedClientPostProcessor.cs` -- fix F14/F16 from source, regenerate v2/catalog/client, and invert the readiness failure fixture to a typed success.
- [x] `src/Hexalith.Folders.UI/Components/Pages/` -- fix F15 and assert exact freshness arguments for all affected calls.
- [x] `tests/Hexalith.Folders.IntegrationTests/EndToEnd/GoldenLifecycleParityTests.cs` and test-project peers -- cover F08: operation-specific freshness, 409 remaps, final reauthorization races, case-variant JSON duplicates, absent-from-v1 parity, baseline-init validation, and MCP lock conflict.
- [ ] `src/Hexalith.Folders.Server/Program.cs`, `docs/contract/pd10-v2-consumer-discovery.md` -- prepare branch-only v2 route and refresh consumer discovery; external deployed v1 consumers block cutover.
- [x] `tests/fixtures/parity-contract.yaml`, `_bmad-output/planning-artifacts/generated-v2-conformance-set-2026-09-17.yaml` -- run affected suites, twelve parity categories and both build profiles; regenerate and reproduce inventory byte-identically last.

**Acceptance Criteria:**
- Given a protected operation, when authority fails or changes, then canonical denial/unavailability precedes protected reads/effects and the final check honors fresh ACL/scope.
- Given F08, F11 and F13, when focused regressions run, then each behavior is pinned through its owner or the real seam.
- Given generated v2 outputs and route preparation, when gates run, then reachable tuples are declared, operator readiness and UI reads succeed, and branch production selects no v1 route.
- Given final generation, when the story-owned inventory is reproduced, then paths and hashes match; historical v1, OQ3, approvals, main, and sprint-status remain unchanged.

## Implementation Notes

- 2026-09-24: Corrected F03/F04/F11/F13 in the candidate seam and final authorization paths; regenerated F14/F16 contract, runtime catalog, and SDK outputs from their owners; aligned F15 UI freshness calls and sample readiness DTO; added F08 regression coverage in the owning test projects. The generated SDK now admits the operator readiness success and the exact repository create/bind reconciliation tuples, including `details.finalState`.
- Consumer discovery records successful production Folders deployments `6546802692`/`6546836679`, published Folders Client 1.0.0, and Projects production deployment `6547910236` at `c767d38d8ae76f9ad949965ac4870a842c699f9c` using the pinned v1 client. See `docs/contract/pd10-v2-consumer-discovery.md`. A consumer-specific migration decision is required before the branch production route can change; `Program.cs` remains unchanged and the route task remains incomplete.
- The [Projects migration decision proposal](story-1-17-projects-v1-consumer-migration-decision-2026-09-24.md) now compares a coordinated switch, a bounded two-route window, and a continued hold. It recommends a seven-day window for approval, with entry, exit, and rollback checks. This is a proposal; the required consumer-specific, exact-digest, and exposure decisions have not been recorded.
- The GENERATE milestone remains `in-progress`; no A6b/A8/exposure or Story 1.17 closure decision was recorded. The unchanged human-owned A6b register still binds an older candidate digest.

## Spec Change Log

## Review Triage Log

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

**Commands:**
- `dotnet restore Hexalith.Folders.slnx && dotnet build Hexalith.Folders.slnx -m:1` -- expected: Debug project-reference graph builds.
- `dotnet build Hexalith.Folders.CI.slnx -c Release -m:1` -- expected: package-reference graph builds.
- `pwsh tests/tools/run-contract-parity-ci-gates.ps1` -- expected: twelve categories pass.
- `python3 scripts/generate-pd10-v2-conformance-set.py --repository-root .` twice -- expected: second run changes no bytes.
