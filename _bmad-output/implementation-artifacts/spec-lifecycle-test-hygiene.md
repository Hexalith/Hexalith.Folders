---
title: 'Tighten lifecycle test fixture hygiene'
type: 'refactor'
created: '2026-09-02'
status: 'done'
baseline_revision: '6935132f8cad406d9e33761504b3dee519642bb4'
review_loop_iteration: 0
followup_review_recommended: false
context: []
warnings: [multiple-goals]
deferred:
  - summary: >-
      Four local lifecycle-area query fixtures still synthesize allowed claim evidence from nullable authority values.
    evidence: |-
      This predates the bundle and is outside DW-54's named FolderLifecycleStatusTestSupport surface. TaskStatusQueryHandlerTests.cs, WorkspaceCleanupStatusQueryHandlerTests.cs, WorkspaceLockStatusProjectionTests.cs, and WorkspaceStatusQueryHandlerTests.cs each pass nullable tenant/principal values to EventStoreClaimTransformEvidence.Allowed.
    location: >-
      tests/Hexalith.Folders.Tests/Queries/Folders
    severity: medium
  - summary: >-
      Project-level dotnet test is incompatible with the pinned Microsoft.Testing.Platform invocation on .NET 10.
    evidence: |-
      Both focused project commands fail before test execution because Microsoft.Testing.Platform 2.3.3 rejects the VSTest target under the .NET 10 SDK; direct xUnit v3 assembly execution remains green for the in-scope lanes.
    location: >-
      tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj and tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj
    severity: medium
  - summary: >-
      The unchanged Forgejo provider source blocks a clean rebuild of the core test assembly.
    evidence: |-
      The baseline contains a brace error at ForgejoProvider.cs:808, so the in-scope test assembly was rebuilt only with that pre-existing source problem isolated out of tree. No production source was changed by this bundle.
    location: >-
      src/Hexalith.Folders/Providers/Forgejo/ForgejoProvider.cs:808
    severity: high
  - summary: >-
      Three pre-existing GitHub provider tests fail in the broad core direct-runner lane.
    evidence: |-
      The 1,658-test partial core run failed FreshRepositoryCreationCarryingPriorEvidenceStillExecutesInsteadOfReplaying, ReplaysEquivalentRepositoryCreationWithoutProviderAccess, and ReplaysEquivalentRepositoryBindingWithoutProviderAccess; the lifecycle namespace remained green.
    location: >-
      tests/Hexalith.Folders.Tests/Providers/GitHub/GitHubProviderTests.cs
    severity: medium
  - summary: >-
      The `Hexalith.Folders.Server.Tests` assembly is executed by no CI workflow and no gate script.
    evidence: |-
      `Hexalith.Folders.Server.Tests` appears only in `Hexalith.Folders.slnx`; it is matched by no file under `.github/workflows/` and by no script under `tests/tools/`. The solution build compiles it, but its 645 tests -- including the lifecycle endpoint test this bundle repaired -- never execute in an automated lane. The lifecycle-status route itself remains covered by the integration contract-parity lane.
    location: >-
      tests/tools/run-baseline-ci-gates.ps1 and .github/workflows/ci.yml
    severity: medium
  - summary: >-
      The `BuildApp` helper leaks its built `WebApplication` if endpoint mapping throws before the value is returned.
    evidence: |-
      All seven callers bind the returned application with `await using`, but `BuildApp` itself calls `builder.Build()` and then `app.MapFoldersServerEndpoints()` before returning, so a mapping failure escapes with the built host undisposed. This is the same leak class the bundle fixed at line 34 and predates the bundle.
    location: >-
      tests/Hexalith.Folders.Server.Tests/FolderLifecycleStatusEndpointTests.cs:258
    severity: low
---

<intent-contract>

## Intent

**Problem:** Lifecycle-query tests retain an async-continuation convention that conflicts with the production handler, a route-registration test leaks its built `WebApplication`, and shared query scaffolding can silently construct allowed claim evidence from nullable authority values.

**Approach:** Normalize only the identified lifecycle test awaits, dispose the route-only application asynchronously, and require nullable-authority scenarios to supply claim evidence explicitly while preserving all product and test outcomes.

## Boundaries & Constraints

**Always:** Keep the change test-only; retain `TestContext.Current.CancellationToken`; preserve lifecycle authorization, projection, endpoint, and route assertions; construct default allowed claim evidence only from validated nonblank tenant and principal values; use async disposal for `WebApplication`.

**Block If:** The focused test projects expose a product-behavior dependency on `ConfigureAwait(true)`, or preserving the unauthenticated scenario requires changing production authorization code.

**Never:** Edit the deferred-work ledger; change production code, public contracts, endpoint behavior, test coverage intent, package versions, or unrelated test suites; initialize nested submodules.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Default query evidence | Nonblank default tenant and principal, no evidence override | Fixture creates allowed `read_metadata` evidence for the exact authority values | No error expected |
| Missing authentication | Null tenant with explicit `EventStoreClaimTransformEvidence.Missing()` | Existing handler test returns authentication-required before protected lookups | Existing safe-denial assertions remain unchanged |
| Invalid implicit evidence | Null or blank authority with no evidence override | Fixture rejects the ambiguous setup instead of synthesizing allowed evidence | `ArgumentException` from boundary validation |

</intent-contract>

## Code Map

- `tests/Hexalith.Folders.Tests/Queries/Folders/FolderLifecycleStatusAuthorizationGateTests.cs` -- contains the nullable-tenant authentication case and six `ConfigureAwait(true)` calls; pass missing claim evidence explicitly.
- `tests/Hexalith.Folders.Tests/Queries/Folders/FolderLifecycleStatusMetadataLeakageTests.cs` -- two lifecycle awaits to normalize.
- `tests/Hexalith.Folders.Tests/Queries/Folders/FolderLifecycleStatusNoFallbackTests.cs` -- three lifecycle awaits to normalize.
- `tests/Hexalith.Folders.Tests/Queries/Folders/FolderLifecycleStatusProjectionTests.cs` -- 32 lifecycle awaits to normalize.
- `tests/Hexalith.Folders.Tests/Queries/Folders/TaskStatusQueryHandlerTests.cs` -- seven lifecycle-query awaits to normalize.
- `tests/Hexalith.Folders.Tests/Queries/Folders/WorkspaceCleanupStatusQueryHandlerTests.cs` -- nine lifecycle-query awaits to normalize.
- `tests/Hexalith.Folders.Tests/Queries/Folders/WorkspaceLifecycleProjectionDeterminismTests.cs` -- 27 lifecycle projection awaits to normalize.
- `tests/Hexalith.Folders.Tests/Queries/Folders/WorkspaceLockStatusProjectionTests.cs` -- two lifecycle-query awaits to normalize.
- `tests/Hexalith.Folders.Tests/Queries/Folders/WorkspaceStatusQueryHandlerTests.cs` -- five lifecycle-query awaits to normalize.
- `tests/Hexalith.Folders.Tests/Queries/Folders/FolderLifecycleStatusTestSupport.cs` -- `Query` currently calls `EventStoreClaimTransformEvidence.Allowed` with nullable tenant/principal parameters; centralize validated default evidence creation here.
- `tests/Hexalith.Folders.Server.Tests/FolderLifecycleStatusEndpointTests.cs` -- `MapFoldersServerEndpointsShouldRegisterLifecycleStatusRoute` builds a route-only application without disposal; neighboring endpoint tests already use application lifetime guards.
- `src/Hexalith.Folders/Authorization/EventStoreClaimTransformEvidence.cs` -- read-only evidence: `Allowed` accepts nullable values, while `Missing()` is the explicit absent-evidence representation.
- `src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs` -- read-only evidence: production awaits use `ConfigureAwait(false)`.

## Tasks & Acceptance

**Execution:**
- [x] `tests/Hexalith.Folders.Tests/Queries/Folders/{FolderLifecycleStatusAuthorizationGateTests,FolderLifecycleStatusMetadataLeakageTests,FolderLifecycleStatusNoFallbackTests,FolderLifecycleStatusProjectionTests,TaskStatusQueryHandlerTests,WorkspaceCleanupStatusQueryHandlerTests,WorkspaceLifecycleProjectionDeterminismTests,WorkspaceLockStatusProjectionTests,WorkspaceStatusQueryHandlerTests}.cs` -- replace lifecycle-area `ConfigureAwait(true)` calls with `ConfigureAwait(false)` without altering awaited operations, cancellation, ordering, or assertions.
- [x] `tests/Hexalith.Folders.Tests/Queries/Folders/FolderLifecycleStatusTestSupport.cs` and `FolderLifecycleStatusAuthorizationGateTests.cs` -- validate tenant/principal before default `Allowed` evidence construction and supply `Missing()` explicitly for the null-tenant scenario, making fixture intent unambiguous.
- [x] `tests/Hexalith.Folders.Server.Tests/FolderLifecycleStatusEndpointTests.cs` -- make the route-registration test asynchronous and bind the built app with `await using` so disposal occurs even on assertion failure.

**Acceptance Criteria:**
- Given the nine identified lifecycle-query test files, when async calls execute, then every formerly explicit continuation uses `ConfigureAwait(false)` with its original cancellation token and assertion behavior intact.
- Given route registration is inspected, when the test completes or fails, then the built `WebApplication` is asynchronously disposed and the lifecycle-status route assertion is unchanged.
- Given the shared query fixture receives nonblank authority and no evidence override, when it creates a query, then allowed evidence contains those exact tenant/principal values and `read_metadata` permission.
- Given the unauthenticated authorization test supplies null authority, when its query is created and handled, then it explicitly supplies missing claim evidence and retains the existing authentication-required/no-protected-lookup outcome.
- Given either authority value is null or blank and no evidence override is supplied, when the fixture attempts default evidence construction, then boundary validation rejects the ambiguous setup.
- Given the built test assemblies are exercised with the direct xUnit v3 commands and filters below, when the change is verified, then the lifecycle namespace, authorization-evidence class, endpoint class, and complete server assembly pass without production-file or deferred-ledger changes; broad baseline blockers are reported separately and are not represented as a complete green core run.

## Spec Change Log

- 2026-09-02: Added class-scoped `xUnit1030` suppressions to the eight lifecycle test classes whose test methods use the approved `ConfigureAwait(false)` convention. `WorkspaceLockStatusProjectionTests` needs no suppression because its changed awaits are confined to a private helper.

## Review Triage Log

### 2026-09-02 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 3: (high 0, medium 2, low 1)
- defer: 4: (high 1, medium 3, low 0)
- reject: 8: (high 0, medium 1, low 7)
- addressed_findings:
  - `[low]` `[patch]` Scoped `xUnit1030` suppressions to the eight affected test classes, restored the analyzer afterward, removed the unnecessary workspace-lock suppression, and corrected the explanatory wording.
  - `[medium]` `[patch]` Added symmetric empty and whitespace-only tenant/principal theory rows so the invalid implicit-evidence boundary is fully pinned.
  - `[medium]` `[patch]` Replaced contradictory verification claims with exact direct-runner commands, accurate focused results, and explicit broad baseline blockers.

### 2026-09-02 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 0
- defer: 6: (high 1, medium 4, low 1)
- reject: 20: (high 0, medium 1, low 19)
- addressed_findings:
  - none

## Design Notes

Keep `Query`'s nullable authority parameters because they model authentication-negative cases. Resolve the ambiguity only at default evidence creation: an explicit override remains authoritative; otherwise validate both authority values before calling `Allowed`. Negative tests must opt into `Missing()` (or another deliberate evidence state) rather than inheriting an accidental “allowed with null” object.

## Verification

**Commands:**
- `rg -n "ConfigureAwait\\(true\\)" tests/Hexalith.Folders.Tests/Queries/Folders/*.cs` -- expected: no matches.
- `dotnet tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests.dll -noLogo -noColor -namespace Hexalith.Folders.Tests.Queries.Folders` -- runs the complete lifecycle-query namespace with the xUnit v3 in-process runner.
- `dotnet tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests.dll -noLogo -noColor -class Hexalith.Folders.Tests.Queries.Folders.FolderLifecycleStatusAuthorizationGateTests` -- runs the authorization/evidence test class with the xUnit v3 in-process runner.
- `dotnet tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests.dll -noLogo -noColor -class Hexalith.Folders.Server.Tests.FolderLifecycleStatusEndpointTests` -- runs the lifecycle endpoint test class with the xUnit v3 in-process runner.
- `dotnet tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests.dll -noLogo -noColor` -- runs the complete server test assembly with the xUnit v3 in-process runner.
- `dotnet test tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore` and `dotnet test tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore` -- broad project-level checks whose baseline blockers must be reported rather than treated as green evidence.
- `git diff --check` -- expected: no whitespace errors.

**Results:**
- No `ConfigureAwait(true)` calls remain under `tests/Hexalith.Folders.Tests/Queries/Folders/*.cs`; the diff contains 93 matched `true` removals and 93 `false` additions across the nine named files.
- The root direct xUnit v3 rerun before the final review rows passed 192/192 for the complete `Hexalith.Folders.Tests.Queries.Folders` lifecycle namespace and 11/11 for the authorization/evidence class. After adding the whitespace-only tenant and empty-principal cases and rebuilding the core test assembly with the baseline-broken Forgejo test sources excluded, the same commands passed 194/194 and 13/13 respectively.
- Direct xUnit v3 execution of `FolderLifecycleStatusEndpointTests` passed 8/8, including route registration and async disposal; the complete server assembly passed 645/645.
- Both project-level `dotnet test ... --no-restore` commands fail because Microsoft.Testing.Platform 2.3.3 rejects the VSTest target under the .NET 10 SDK. Direct-runner builds additionally require a temporary, out-of-tree correction for the unchanged baseline brace error at `src/Hexalith.Folders/Providers/Forgejo/ForgejoProvider.cs:808`; these are broad baseline blockers and no production file was modified.
- The broad core direct-runner execution reached 1,658 tests after excluding baseline-broken Forgejo test sources, but it was only partial evidence: it failed `GitHubProviderTests.FreshRepositoryCreationCarryingPriorEvidenceStillExecutesInsteadOfReplaying`, `GitHubProviderTests.ReplaysEquivalentRepositoryCreationWithoutProviderAccess`, and `GitHubProviderTests.ReplaysEquivalentRepositoryBindingWithoutProviderAccess`. It is not a complete green core run; the in-scope lifecycle namespace remained green.
- `git diff --check` passed; no production or deferred-ledger file is changed.

## Auto Run Result

Status: done

### Summary

Normalized 93 lifecycle-area test awaits to `ConfigureAwait(false)` under class-scoped `xUnit1030` suppressions, asynchronously disposed the route-registration `WebApplication`, and made default allowed claim-evidence construction reject nullable or blank authority while the unauthenticated scenario supplies `Missing()` explicitly. Product code and behavior are unchanged. This entry records a second, independent review pass over the finished change; it produced no code edits.

### Files Changed

- `tests/Hexalith.Folders.Server.Tests/FolderLifecycleStatusEndpointTests.cs` -- converted the route-registration test to `async Task` and added `await using` application disposal.
- `tests/Hexalith.Folders.Tests/Queries/Folders/FolderLifecycleStatusAuthorizationGateTests.cs` -- made missing evidence explicit, normalized awaits, and added the nullable/empty/whitespace fixture matrix.
- `tests/Hexalith.Folders.Tests/Queries/Folders/FolderLifecycleStatusTestSupport.cs` -- validates authority before constructing default allowed evidence.
- `tests/Hexalith.Folders.Tests/Queries/Folders/FolderLifecycleStatusMetadataLeakageTests.cs`, `FolderLifecycleStatusNoFallbackTests.cs`, `FolderLifecycleStatusProjectionTests.cs`, `TaskStatusQueryHandlerTests.cs`, `WorkspaceCleanupStatusQueryHandlerTests.cs`, `WorkspaceLifecycleProjectionDeterminismTests.cs`, `WorkspaceLockStatusProjectionTests.cs`, and `WorkspaceStatusQueryHandlerTests.cs` -- normalized explicit await continuation configuration; each suppression region opens immediately before its test class and closes immediately after that class's closing brace, so trailing helper types stay unsuppressed.
- `_bmad-output/implementation-artifacts/spec-lifecycle-test-hygiene.md` -- records intent, plan, verification, both review triage passes, deferred findings, and completion evidence.

### Review Findings

Second review pass (2026-09-02), four layers -- blind hunter, edge-case hunter, verification-gap, intent alignment:

- Patches applied: 0.
- Items deferred: 6 (high 1, medium 4, low 1). Four restate entries already present in this spec's `deferred` list and already routed to the ledger by the orchestrator, so no duplicate frontmatter items were added for them. Two are new: `Hexalith.Folders.Server.Tests` is executed by no CI workflow or gate script, and the `BuildApp` helper leaks its built application when endpoint mapping throws.
- Items rejected: 20 (high 0, medium 1, low 19). The load-bearing rejections were checked against the tree rather than taken on the reviewers' word: the claim that the `xUnit1030` suppressions are namespace-wide or restored mid-file is false (each region wraps exactly one class); the claimed SA1202 member-ordering violation would have failed the warnings-as-errors build that in fact succeeds; the claimed loss of the "explicit override bypasses validation" contract is covered by the existing `Missing()` authentication test; and the surviving `ConfigureAwait(true)` sites elsewhere under `tests/` are excluded by the intent's own "unrelated test suites" boundary. The single medium rejection is the design objection that deleting `.ConfigureAwait(...)` outright would have avoided the analyzer suppressions -- a defensible alternative reading, already deliberated and recorded in the Spec Change Log, with no consequence for a green suite.
- Follow-up review recommendation: false; no finding was triaged `patch`, so the patched counts are high 0, medium 0, low 0 and the score is 0.

### Verification Performed

Re-run in this pass against the committed test sources:

- Lifecycle namespace direct xUnit v3 lane: 194 passed, 0 failed, 0 skipped.
- Lifecycle endpoint class: 8 passed, 0 failed, 0 skipped.
- `grep -n "ConfigureAwait(true)" tests/Hexalith.Folders.Tests/Queries/Folders/*.cs` returned no matches.
- `git diff --name-only <baseline> -- src/` returned nothing: no production file changed.
- `dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj` fails with `CS1513`/`CS1519` at `ForgejoProvider.cs:808`/`:821`; `git show main:...` is byte-identical to the worktree copy, confirming the blocker is pre-existing on `main` and not introduced here.
- `Hexalith.Folders.Server.Tests` matches no file under `.github/workflows/` or `tests/tools/`; it appears only in `Hexalith.Folders.slnx`.

Carried forward from the first pass: authorization/evidence class 13/13, complete server assembly 645/645, and the partial 1,658-test core direct-runner run with three pre-existing `GitHubProviderTests` failures.

### Residual Risks

- The repository's core project does not compile at `main`, so neither test assembly can be rebuilt from the tree as committed; the green numbers above come from assemblies built with that pre-existing brace error corrected out of tree. Until `ForgejoProvider.cs:808` is fixed, no automated lane reaches these tests.
- Both project-level `dotnet test` entry points fail before execution under the pinned Microsoft.Testing.Platform on the .NET 10 SDK, so all evidence is from hand-invoked direct xUnit v3 assembly runs.
- The repaired endpoint test runs in no CI lane; the lifecycle-status route retains separate integration coverage in the contract-parity lane.
- Four other local lifecycle-area query fixtures still accept nullable authority when constructing allowed evidence; they sit outside DW-54's explicitly named shared fixture.
- The `xUnit1030` suppression is deliberate and class-scoped, but it does mean future test methods added to those eight classes inherit it without a fresh analyzer check.
