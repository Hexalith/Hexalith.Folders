# Story 7.18: Restore shared test-host composition baseline

Status: ready-for-dev

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

3. Given the remaining `Hexalith.Folders.IntegrationTests` failures (≈11 in `GoldenLifecycleParityTests` / `MixedSurfaceHandoffTests`) and `Hexalith.Folders.Tests` failures (≈2 in Epic 3 provider-boundary guards), when each affected host-build path is identified and the helper (or equivalent) applied, then both suites run green with 0 failures from this composition cause.

4. Given a future story may again extend the shared server surface with a new DI requirement, when a central composition smoke test builds the full server host with `ValidateOnBuild`/`ValidateScopes` and asserts construction succeeds, then any regression in the shared-surface DI contract is caught once centrally instead of cascading silently across hundreds of endpoint tests.

5. Given this is a test-host composition fix, when the change is applied, then **no production code behavior changes** — only test-host registration and test-support helpers are touched (production `AddFoldersServer` / `MapFoldersServerEndpoints` / `AddServiceDefaults` are unchanged).

6. Given the fix is mechanical and broad, when the story completes, then the full solution test run (`dotnet test Hexalith.Folders.slnx`, via the xUnit v3 in-process runner where the sandbox denies the VSTest socket) shows 0 failures attributable to this composition gap, and the result is recorded in the Dev Agent Record with observed-vs-expected counts (no fail-open `--filter` matching).

## Tasks / Subtasks

- [ ] Introduce the shared test-host composition helper. (AC: 1, 5)
  - [ ] Add `AddFoldersServerTestDefaults()` (auth scheme + health checks) in a shared test-support location.
  - [ ] Document at the call site why the shared server surface needs it (auth validator + `MapDefaultEndpoints`).
- [ ] Apply the helper across all 19 `Server.Tests` endpoint hosts. (AC: 2)
  - [ ] Replace ad-hoc `AddAuthentication()`/`AddHealthChecks()` (where already present, e.g. fixed hosts) with the shared helper for consistency.
  - [ ] Re-run `Hexalith.Folders.Server.Tests` and confirm 433/0/0.
- [ ] Remediate the IntegrationTests and Folders.Tests composition reds. (AC: 3)
  - [ ] Identify each failing host-build helper in `GoldenLifecycleParityTests` / `MixedSurfaceHandoffTests` and the Epic 3 provider-boundary guards.
  - [ ] Apply the helper (or equivalent) and confirm green.
- [ ] Add the central composition smoke test. (AC: 4)
  - [ ] Build the server host with `ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }` and assert success.
- [ ] Record results and close. (AC: 6)
  - [ ] Capture observed test counts in the Dev Agent Record.
  - [ ] Update `sprint-status.yaml` (`7-18` → `done`; `epic-7` → `done` once green).
  - [ ] Append a Change Log entry referencing the Sprint Change Proposal and the 2-8b finding.

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

## Dev Agent Record

### Agent Model Used

_(to be filled by dev)_

### Debug Log References

- Baseline (pre-fix) re-run 2026-05-31: `Hexalith.Folders.Server.Tests` Total 433, Failed 339, Passed 94, Skipped 0 (xUnit v3 in-process runner).

### Completion Notes List

_(to be filled by dev)_

### File List

_(to be filled by dev)_
