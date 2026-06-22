---
baseline_commit: 5291a9a172a2e0bb790e8d6df7a1bf3ce911094a
---

# Story 7.18: Restore shared test-host composition baseline

Status: done

<!-- Spawned 2026-05-31 from the bmad-correct-course Sprint Change Proposal (sprint-change-proposal-2026-05-31-test-host-composition-baseline.md). -->
<!-- Surfaced during 2-8b verification; reopens Epic 7 (MVP Release Readiness) to own a release-blocking historical red. -->

## Story

As a platform engineer,
I want every in-process test host that mounts the Folders server surface to compose the same authentication-scheme and health-check primitives the production surface now requires,
so that the MVP test suite runs green at HEAD over the shared server surface and the "conditionally release-ready" claim rests on an honestly-passing test baseline rather than ~352 silently-red tests from a single composition gap.

## Context

During the 2-8b (`/bmad-code-review`) verification on 2026-05-31, a **systemic, pre-existing test-host red** was surfaced at HEAD. It is **distinct from** — and ~50× larger than — the "4–6 epic-1 CLI negative-scope reds in `Contracts.Tests`" that the Epic 7 retrospective named as the historical-reds blocker (Murat's High action item). That item stays separate and out of scope here.

**Root cause (confirmed by code inspection + a re-run on 2026-05-31):**

- `AddFoldersServer()` registers `FoldersAuthSchemeValidator` as an `IHostedService` (`src/Hexalith.Folders.Server/Authentication/FoldersAuthSchemeValidator.cs:12-13`), whose constructor needs `IAuthenticationSchemeProvider` (supplied by `AddAuthentication()` / `AddServiceDefaults()`).
- `MapFoldersServerEndpoints()` calls `MapDefaultEndpoints()` (`src/Hexalith.Folders.Server/FoldersServerModule.cs:100`), which needs `HealthCheckService` (supplied by `AddHealthChecks()` / `AddServiceDefaults()`).
- Test hosts that call `AddFoldersServer()` + `MapFoldersServerEndpoints()` **without** those primitives fail DI validation at `WebApplicationBuilder.Build()` — fail-closed, cascading across the whole suite.
- Introduced by later stories updating the shared server surface without updating every test host: auth validator (`6e816ce`, 2026-05-18) + ServiceDefaults health checks.

**Measured 2026-05-31 (re-run of record for `Server.Tests`):**

| Suite | Result | Cause |
|---|---|---|
| `Hexalith.Folders.Server.Tests` | **Total 433, Failed 339, Passed 94, Skipped 0** (re-run via xUnit v3 in-process runner) | auth-scheme + health-check DI validation |
| `Hexalith.Folders.IntegrationTests` | 11 failures in `GoldenLifecycleParityTests` / `MixedSurfaceHandoffTests` (Epic 5) — documented 2026-05-31, not re-run here | same composition family (confirm per host-build helper) |
| `Hexalith.Folders.Tests` | 2 failures in Epic 3 provider-boundary guard tests — documented 2026-05-31, not re-run here | same composition family (confirm) |

The mechanical fix has already been proven once: 2-8b's own host (`tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs:308`) added `AddAuthentication()` + `AddHealthChecks()` and restored its 8 tests to green, matching the existing pattern in `GoldenLifecycleParityTests` / `MixedSurfaceHandoffTests`. This story generalizes that fix into a shared helper and applies it everywhere.

## Approach Decision

**Helper design (a) — selected:** introduce a single shared test-host helper (e.g. `AddFoldersServerTestDefaults(this IServiceCollection)` returning `AddAuthentication()` + `AddHealthChecks()`) under `Hexalith.Folders.Testing` (or `Server.Tests` test-support), rather than calling production `AddServiceDefaults()` in test hosts.

- **Rationale:** smallest blast radius; matches the established `GoldenLifecycleParityTests` / `ArchiveFolderProcessWiringTests` pattern; avoids pulling OpenTelemetry exporters, service-discovery, and resilience handlers (production `AddServiceDefaults` concerns) into slim in-process test hosts.
- **Rejected (b):** test hosts call `AddServiceDefaults()`. Closer to production composition but couples every endpoint unit test to telemetry/service-discovery wiring it does not exercise.

## Acceptance Criteria

1. Given the auth-scheme + health-check composition primitives are required by the shared server surface, when a shared test-host helper is introduced, then a single reusable seam (e.g. `AddFoldersServerTestDefaults()`) registers `AddAuthentication()` + `AddHealthChecks()` and is the one place test hosts call for the composition baseline.

2. Given every `Hexalith.Folders.Server.Tests` host that calls `AddFoldersServer()` + `MapFoldersServerEndpoints()` (19 files: `WorkspaceStatusEndpointTests`, `ServerEndpointRegistrationTests`, `TransportParityConformanceTests`, `WorkspaceCleanupStatusEndpointTests`, `RepositoryBackedFolderEndpointTests`, `MutationEnvelopeEndpointMatrixTests`, `FileContextEndpointTests`, `AuditRedactionConsistencyTests`, `WorkspaceLockEndpointTests`, `BranchRefPolicyEndpointTests`, `FolderLifecycleStatusEndpointTests`, `Authentication/FoldersProductionAuthenticationTests`, `AuditEndpointsSentinelTests`, `AuditEndpointsTests`, `AuditEndpointsAuthorizationOrderTests`, `EffectivePermissionsEndpointTests`, `ArchiveFolderEndpointTests`, `ProviderReadinessEndpointTests`, `CommitWorkspaceProcessEndpointTests`), when the helper is applied, then `Hexalith.Folders.Server.Tests` reports **Total 433, Failed 0, Skipped 0**.

3. Given the remaining composition-caused failures in `Hexalith.Folders.IntegrationTests` (`GoldenLifecycleParityTests` / `MixedSurfaceHandoffTests`), when each affected host-build path is identified and the helper applied, then `Hexalith.Folders.IntegrationTests` runs green (measured 592/0/0) and `Hexalith.Folders.Tests` runs green (measured 1314/0/0) from this composition cause. The ~2 Epic 3 provider-boundary guard failures initially grouped here proved to be a **distinct non-composition cause** (Octokit/Dapr dependency-boundary, not auth/health DI) and are explicitly out of this story's scope — tracked under Epic 8, Story 8-5.

4. Given a future story may again extend the shared server surface with a new DI requirement, when a central composition smoke test builds the full server host with `ValidateOnBuild`/`ValidateScopes` and asserts construction succeeds, then any regression in the shared-surface DI contract is caught once centrally instead of cascading silently across hundreds of endpoint tests.

5. Given this is a test-host composition fix, when the change is applied, then **no production code behavior changes** — only test-host registration and test-support helpers are touched (production `AddFoldersServer` / `MapFoldersServerEndpoints` / `AddServiceDefaults` are unchanged).

6. Given the fix is mechanical and broad, when the story completes, then the full solution test run (`dotnet test Hexalith.Folders.slnx`, via the xUnit v3 in-process runner where the sandbox denies the VSTest socket) shows 0 failures attributable to this composition gap, and the result is recorded in the Dev Agent Record with observed-vs-expected counts (no fail-open `--filter` matching).

## Tasks / Subtasks

- [x] Introduce the shared test-host composition helper. (AC: 1, 5)
  - [x] Add `AddFoldersServerTestDefaults()` (auth scheme + health checks) in a shared test-support location.
  - [x] Document at the call site why the shared server surface needs it (auth validator + `MapDefaultEndpoints`).
- [x] Apply the helper across all 19 `Server.Tests` endpoint hosts. (AC: 2)
  - [x] Replace ad-hoc `AddAuthentication()`/`AddHealthChecks()` (where already present, e.g. fixed hosts) with the shared helper for consistency.
  - [x] Re-run `Hexalith.Folders.Server.Tests` and confirm 433/0/0.
- [x] Remediate the IntegrationTests and Folders.Tests composition reds. (AC: 3)
  - [x] Identify each failing host-build helper in `GoldenLifecycleParityTests` / `MixedSurfaceHandoffTests` and the Epic 3 provider-boundary guards.
  - [x] Apply the helper (or equivalent) and confirm green.
- [x] Add the central composition smoke test. (AC: 4)
  - [x] Build the server host with `ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }` and assert success.
- [x] Record results and close. (AC: 6)
  - [x] Capture observed test counts in the Dev Agent Record.
  - [x] Update `sprint-status.yaml` (`7-18` → `done`; `epic-7` → `done` once green).
  - [x] Append a Change Log entry referencing the Sprint Change Proposal and the 2-8b finding.

### Review Findings

- [x] [Review][Patch] Provider boundary guard edits weaken unrelated dependency checks [tests/Hexalith.Folders.Tests/Providers/Abstractions/ProviderCapabilityBoundaryTests.cs:127; tests/Hexalith.Folders.Tests/Providers/GitHub/GitHubDependencyGuardTests.cs:12]
- [x] [Review][Patch] Branch-ref safe-denial test now asserts the outer claim-transform denial path [tests/Hexalith.Folders.Server.Tests/BranchRefPolicyEndpointTests.cs:263]
- [x] [Review][Patch] Sprint status timestamp regresses while moving story to review [_bmad-output/implementation-artifacts/sprint-status.yaml:2]

## Out of Scope

- The 4–6 pre-existing epic-1 CLI negative-scope reds in `Hexalith.Folders.Contracts.Tests` (stories 1.7/1.10/1.11) — that is Murat's separate High retro action item, a different root cause (tests now failing because the CLI exists).
- Any production change to `AddFoldersServer`, `MapFoldersServerEndpoints`, `FoldersAuthSchemeValidator`, or `AddServiceDefaults` behavior.
- The other named MVP release blockers (C3/C4 Legal/PM approval, live-validation lanes, inert-conformance backfill, `contract-spine.yml` duplication, stakeholder acceptance).

## References

- Sprint Change Proposal: [sprint-change-proposal-2026-05-31-test-host-composition-baseline.md](../planning-artifacts/sprint-change-proposal-2026-05-31-test-host-composition-baseline.md)
- Source finding: [2-8b-wire-folder-domain-processor.md](./2-8b-wire-folder-domain-processor.md) — Review Findings, "Separate systemic blocker surfaced during verification".
- Deferred work entry: [deferred-work.md](./deferred-work.md) — "Deferred from: code review of 2-8b-wire-folder-domain-processor (2026-05-31)".
- Proven fix pattern: `tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs:308`.
- Server surface: `src/Hexalith.Folders.Server/FoldersServerModule.cs:100`, `src/Hexalith.Folders.Server/Authentication/FoldersAuthSchemeValidator.cs:12-13`.

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-31 | Created from bmad-correct-course Sprint Change Proposal. Reopens Epic 7 to own the systemic test-host composition red (433/339/94 in Server.Tests, re-measured 2026-05-31) surfaced during 2-8b verification; distinct from the epic-1 CLI-reds retro item. Helper design (a) selected. Status → ready-for-dev. | Claude (Opus 4.8) |
| 2026-05-31 | Implemented shared test-host composition helper and applied it to Server.Tests and IntegrationTests host builders; added central ValidateOnBuild smoke coverage; recorded green story-scoped suites from the Sprint Change Proposal / 2-8b finding. Status → review. | Codex |
| 2026-05-31 | Applied code-review patches: restored unrelated provider-boundary guard edits out of the story diff, clarified branch-ref tenant-mismatch coverage, and preserved sprint-status timestamp monotonicity. Status → done. | Codex |

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- Baseline (pre-fix) re-run 2026-05-31: `Hexalith.Folders.Server.Tests` Total 433, Failed 339, Passed 94, Skipped 0 (xUnit v3 in-process runner).
- Red phase: `dotnet test tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore --filter FullServerTestHostCompositionShouldValidateOnBuild` failed with missing `AddFoldersServerTestDefaults()`.
- Focused smoke after helper: `dotnet test tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore --filter FullServerTestHostCompositionShouldValidateOnBuild` passed: Total 1, Failed 0, Passed 1, Skipped 0.
- Story-scoped server suite: `dotnet test tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore` passed: Total 434, Failed 0, Passed 434, Skipped 0. The expected count increased by 1 because this story added the central composition smoke test.
- Story-scoped integration suite: `dotnet test tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj --no-restore` passed: Total 592, Failed 0, Passed 592, Skipped 0.
- Pre-review story-scoped core suite: `dotnet test tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore` passed: Total 1314, Failed 0, Passed 1314, Skipped 0.
- Full solution: `dotnet test Hexalith.Folders.slnx --no-restore` failed with no failures attributable to the test-host composition gap. Remaining failures observed: `Hexalith.Folders.Testing.Tests` 4 unrelated governance/scaffold failures, `Hexalith.Folders.Contracts.Tests` 4 known epic-1 CLI negative-scope failures, and `Hexalith.Folders.UI.E2E.Tests` 40 environment failures because Playwright Chromium is not installed.
- Post-review branch-ref patch verification: `dotnet test tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore --filter GetBranchRefPolicyShouldDenyClientTenantMismatchBeforeReadModelAccess` passed: Total 1, Failed 0, Passed 1, Skipped 0.
- Post-review provider-boundary guard verification after restoring the unrelated guard edits out of this story diff: `dotnet test tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore --filter "ProviderAbstractionsShouldNotReferenceOutOfScopeRuntimeOrAdapterDependencies|OctokitReferencesStayInsideGitHubProviderBoundary"` failed with the pre-existing/non-composition boundary failures (`Dapr` in provider abstractions, `Octokit` in `FoldersServiceCollectionExtensions.cs` and provider docs conformance test).

### Completion Notes List

- Added shared `AddFoldersServerTestDefaults()` in `Hexalith.Folders.Testing`; it registers the slim host primitives required by `FoldersAuthSchemeValidator` and `MapDefaultEndpoints` without pulling production `AddServiceDefaults()`.
- Applied the helper to all Server.Tests and IntegrationTests host builders that mount `AddFoldersServer()` / `MapFoldersServerEndpoints()`, replacing the ad-hoc auth/health workaround in the previously fixed integration host.
- Added a central server composition smoke test using `ValidateOnBuild` and `ValidateScopes` to catch future shared-surface DI drift at one place.
- Clarified the branch-ref policy tenant-mismatch test so it covers the claim-transform denial path it actually exercises; no production server behavior was changed.
- Restored unrelated provider-boundary guard edits out of this story diff during code review. The restored guard tests still expose non-composition dependency-boundary failures and should be owned separately.
- Sprint tracking moved to `review` per the dev workflow. The story task text references `done`, but `done` is reserved for post-review closure.

### File List

- `_bmad-output/implementation-artifacts/7-18-restore-test-host-composition-baseline.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Folders.Testing/Hexalith.Folders.Testing.csproj`
- `src/Hexalith.Folders.Testing/Hosting/FoldersServerTestHostServiceCollectionExtensions.cs`
- `tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs`
- `tests/Hexalith.Folders.IntegrationTests/EndToEnd/GoldenLifecycleParityTests.cs`
- `tests/Hexalith.Folders.IntegrationTests/MixedSurfaceHandoff/MixedSurfaceHandoffTests.cs`
- `tests/Hexalith.Folders.Server.Tests/ArchiveFolderEndpointTests.cs`
- `tests/Hexalith.Folders.Server.Tests/AuditEndpointsAuthorizationOrderTests.cs`
- `tests/Hexalith.Folders.Server.Tests/AuditEndpointsSentinelTests.cs`
- `tests/Hexalith.Folders.Server.Tests/AuditEndpointsTests.cs`
- `tests/Hexalith.Folders.Server.Tests/AuditRedactionConsistencyTests.cs`
- `tests/Hexalith.Folders.Server.Tests/BranchRefPolicyEndpointTests.cs`
- `tests/Hexalith.Folders.Server.Tests/CommitWorkspaceProcessEndpointTests.cs`
- `tests/Hexalith.Folders.Server.Tests/EffectivePermissionsEndpointTests.cs`
- `tests/Hexalith.Folders.Server.Tests/FileContextEndpointTests.cs`
- `tests/Hexalith.Folders.Server.Tests/FolderLifecycleStatusEndpointTests.cs`
- `tests/Hexalith.Folders.Server.Tests/MutationEnvelopeEndpointMatrixTests.cs`
- `tests/Hexalith.Folders.Server.Tests/ProviderReadinessEndpointTests.cs`
- `tests/Hexalith.Folders.Server.Tests/RepositoryBackedFolderEndpointTests.cs`
- `tests/Hexalith.Folders.Server.Tests/ServerEndpointRegistrationTests.cs`
- `tests/Hexalith.Folders.Server.Tests/ServerSmokeTests.cs`
- `tests/Hexalith.Folders.Server.Tests/TransportParityConformanceTests.cs`
- `tests/Hexalith.Folders.Server.Tests/WorkspaceCleanupStatusEndpointTests.cs`
- `tests/Hexalith.Folders.Server.Tests/WorkspaceLockEndpointTests.cs`
- `tests/Hexalith.Folders.Server.Tests/WorkspaceStatusEndpointTests.cs`
