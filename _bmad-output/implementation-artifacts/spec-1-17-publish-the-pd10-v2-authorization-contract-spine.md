---
title: 'Publish the PD10 v2 Authorization Contract Spine'
type: 'feature'
created: '2026-09-22'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '19c29e00eb6679bbf7a0d20a8b4dbf30182243cf'
execution_lane:
  branch: 'feat/story-1-17-pd10-v2-closure'
  worktree: '/home/administrator/projects/hexalith/folders-story-1-17'
  main_merge: 'forbidden-except-atomic-1.17-G-closure-change'
execution_authorization:
  gate_id: 'DEC-EXEC-1.17'
  record: '_bmad-output/planning-artifacts/authority-relock/2026-09-17/STORY-1-17-EXECUTION-AUTHORIZATION-APPROVAL-2026-09-23.yaml'
  record_sha256: '45ad4fc46f7e74aacae3a9c90d1ec535d97fb5ff615d513c1d588aed16df52be'
  preparation_sha256: '4b3cf91f1ea7b7febd66848ece317fd2e9260518773fa383f5a3ed82637c0443'
tracker_policy: 'no-automatic-transition'
renegotiation_approval:
  gate_id: '1.17-SPEC-RENEGOTIATION'
  approved_on: '2026-09-23'
  record: '_bmad-output/implementation-artifacts/renegotiation-1-17-2026-09-23/approval-record.yaml'
  approved_proposal_sha256: 'f53bdc1ff5b61a05e77719812ea6ffddb3852af03e1ee382f334944cf43a41bd'
  approved_diff_sha256: '2e7e0fdf25befc488864825e9e7c3b4934265ee903dba7c24b91da1ab23d7841'
  replaced_spec_sha256: '409d1535ab9e54d7ad175236347c37a9b683249417bcb97ba2d0ee51ddc20a04'
  effective_scope: 'branch feat/story-1-17-pd10-v2-closure from 19c29e0 only'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/planning-story-manifest.yaml'
  - '{project-root}/_bmad-output/planning-artifacts/epics.md'
  - '{project-root}/_bmad-output/planning-artifacts/authority-relock/2026-09-17/STORY-1-17-EXECUTION-AUTHORIZATION-PREPARATION-2026-09-23.yaml'
  - '{project-root}/_bmad-output/implementation-artifacts/review-1-17-generate-scoped-a6b-2026-09-22.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 1.17 is `authorized-scoped` under DEC-EXEC-1.17 (record `45ad4fc4…52be`, preparation `4b3cf91f…0443`), but it cannot close yet. The A6b-approved candidate (conformance `4c8f33f0…`, candidate set `c5bea429…`, 196 artifacts, matrix `2.0.0` `d5daa483…`) still has eight must-fix defects: F03, F04, F08, F11, F13, F14, F15 and F16. The `1.17-GENERATE` delivery evidence is open. External v1 consumer discovery dates from 2026-09-19. Production maps only historical v1 (SF-1). Any fix changes bound bytes and reopens A6b and A8 if it reaches main (SF-2).

**Approach:** Work only on branch `feat/story-1-17-pd10-v2-closure` (worktree `folders-story-1-17`, baseline `19c29e0`). Fix the eight must-fix findings red-first. Refresh external v1 consumer discovery. Prepare the v2 production cutover without activating it. Then regenerate, reseal and run the full closure check. Finally, assemble the 1.17-G closure bundle as preparation packages for human decision. Main receives nothing until one atomic 1.17-G change carries every required approval, and that change also requires a separate v2 exposure/routing decision.

## Boundaries & Constraints

**Always:**
- Before and after every session, prove that main still holds these exact bytes: the 196 artifacts of conformance `4c8f33f0…`, matrix `d5daa483…`, sprint-status `230889ef…`, register `ed316064…`, manifest `927641dd…`, this spec `409d1535…` (until the approved replacement is recorded) and the GENERATE spec `5472e48e…`.
- Red-first: write each must-fix test before any production edit. Observe it failing at the lane baseline with an assertion failure, and record the exact command and output. F08 tests pin fixes that already exist, so prove them by mutation-red: revert the covered production lines locally, observe the failure, and restore. A must-fix test that passes at baseline is a stop: record it as "not reproduced" and escalate. Never drop, weaken or re-scope a finding.
- Keep the established candidate properties:
  - v1 bytes `3c3c6680…` and OQ3 evidence stay unchanged.
  - The 49/14/11 denominators hold.
  - Canonical 401, byte-equivalent 404 and retryable 503 are decided before any protected lookup.
  - Audit happens before the effect, and task mutations are reauthorized at the end.
  - CLI exits 73/77 and MCP `concurrency_conflict` stay paired.
  - UI denial and unavailable stay distinct.
  - Contract, runtime catalog, SDK and parity outputs stay generator-owned and byte-reproducible.
  - Event 1018 is still emitted.
- Access-state resolution does not depend on claim order. Any unusable-evidence state wins as the retryable 503. Otherwise any fresh negative state wins as the canonical 404.
- Each gateway effect's final reauthorization evaluates the same layer set, `OperationScope` and authenticated principal as its initial authorization.
- The generated contract admits every reachable response, and no reachable declared tuple collapses into the canonical 503.
- Authorization-failure logging is metadata-only: event id, operation id and exception type. It never logs the exception message, stack or inner exceptions.
- Stop and escalate on any deployed external v1 consumer. Never invent a migration window.
- Preserve unrelated worktree changes. Root-declared submodules may be initialized non-recursively in the worktree. Never change a pin or dependency version.

**Ask first (stop and escalate to Jerome):** a must-fix that does not reproduce; any semantic edit to `docs/contract/authorization-matrix.md`; any edit outside the 196-artifact story-owned set, other than new test files and their allowlist entries; any consumer-discovery hit; any v2 production cutover that needs data-plane (Epic 12) work, a deployment/profile change or an AppHost change.

**Never:**
- Merge, cherry-pick or rebase onto main, push any ref or open a pull request without explicit authorization.
- Activate v2. Do not change deployment, profile or AppHost exposure. Do not publish a release, tag, package or container. The R5 cutover exists only on the branch and must not reach main without a separate recorded v2 exposure/routing decision (DEC-V2-ROUTING-1.17).
- Edit or regenerate `sprint-status.yaml` on any ref. The only exception is the single approved row delta inside the 1.17-G bundle.
- Let any BMAD workflow (dev, build, loop, sprint-planning, code-review, retrospective, correct-course) transition the 1.17 tracker row or the manifest lifecycle.
- Record, infer or pre-fill any approval: A6b, a Section 9 pass, A8, DEC-V2-ROUTING-1.17, the tracker delta, GENERATE evidence acceptance or Story 1.17 closure.
- Start follow-up findings F01, F05, F06, F07, F09 or F10. Do not admit Story 1.18, re-scope 11.6 or 13.2, or start any other story.
- Edit the GENERATE spec beyond the R2 disposition Jerome approves. Do not revive `tests/tools/pd10-v2-contract-generator/`. Do not hand-edit generated output.
- Close Story 1.17 or mark this spec `done` before the 1.17-G change merges.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Unauthenticated | No actor | No protected lookup | Canonical `401` |
| Fresh negative authority | Denied, hidden, absent, or out of scope | Zero protected reads | Byte-equivalent `404` |
| Unusable authority | Stale, unavailable, conflicting, or incomplete | Zero protected reads | Retryable `503` |
| Mixed access states (F03) | `[revoked, stale]` and `[stale, revoked]` | Identical outcome and body for both orders | Unusable evidence present → retryable `503` |
| ACL revoked between checks (F04) | Folder create, repository-backed create, provider binding | No append and no provider or repository effect | Canonical `404` |
| Authorization evaluation throws (F11) | Exception text carries tenant or principal values | Canonical `503`; event 1018 written | Log holds event id, operation id, exception type only |
| Archive final reauthorization (F13) | Actor-safe id absent or different from the authenticated principal | Final check evaluates the authenticated principal | Missing principal → canonical `503`, never a check of a placeholder |
| Provider readiness success (F14) | Server returns `audience: authorized_operator` | Generated SDK returns the operator shape | No exception on a declared `200` |
| UI read (F15) | Any UI page calls a v2 operation | Sends only that operation's accepted freshness | No `400` after cutover |
| Repository-backed failure (F16) | `422 provider_readiness_failed`; `finalState` on `reconciliation_required`/`unknown_provider_outcome` | Declared tuple crosses the seam unchanged | Never rewritten to `503` |
| External v1 consumer | Deployed consumer outside this repo | Stop | Escalate; no invented migration window |
| Production host on branch after R5 | `Program.cs` composition | `/api/v2` served; `/api/v1` not externally reachable | External `/api/v1` → canonical `404` |
| Production host on main | `Program.cs` at `19c29e0` | Historical endpoints only | Unchanged until the 1.17-G change and DEC-V2-ROUTING-1.17 |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Folders.Server/Pd10V2CandidateCompatibilitySeam.cs` -- `CanonicalAccessState` (F03, `negativeStates[0]` at :1547); event-1018 `LogAuthorizationEvaluationFailed` (F11, call :192, declaration :1608); path rewrite to historical handlers at :280 (R5 design constraint).
- `src/Hexalith.Folders/Aggregates/Folder/FolderCreationService.cs:118`, `RepositoryBackedFolderCreationService.cs:129`, `src/Hexalith.Folders/Aggregates/Organization/ConfigureProviderBindingService.cs:111` -- final `ReauthorizeMutationAsync(..., includeFolderAcl: false, ...)` versus initial `AuthorizeAsync` (includes the ACL layer) (F04).
- `src/Hexalith.Folders/Authorization/LayeredFolderAuthorizationService.cs:300-320` -- `ReauthorizeMutationAsync` drops `OperationScope` when `includeFolderAcl` is false (F04).
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs:523-541`, `src/Hexalith.Folders/Authorization/LayeredFolderAuthorizationAllowedContext.cs` -- archive final check passes `ActorSafeIdentifier` as `PrincipalId`. The allowed context carries no principal, and `SafeActorIdentifier` substitutes `actor_present` (F13).
- `scripts/generate-pd10-v2-contract.py`, `src/Hexalith.Folders.Client/Generation/` (post-processor) -- the `ProviderReadiness` `oneOf`/discriminator is bound to `ProviderReadinessConsumer` (F14); declared tuples and `finalState` detail keys (F16).
- `src/Hexalith.Folders.Server/FolderProblemDetailsFactory.cs:36-39`, `Pd10V2RuntimeResponseCatalog.g.cs` -- `finalState` producer and runtime tuple catalog (F16; the catalog stays generated).
- `src/Hexalith.Folders.UI/Components/Pages/{Workspace,Provider,AuditTrail,OperationTimeline,IncidentStream,ProviderSupport,IndexingStatus}.razor.cs`, `FolderDetail.razor` -- hard-coded `ReadConsistencyClass.Eventually_consistent` (F15).
- `src/Hexalith.Folders.Server/Program.cs`, `FoldersServerModule.cs:107-117` -- production composition (R5, branch only).
- `tests/Hexalith.Folders.IntegrationTests/EndToEnd/GoldenLifecycleParityTests.cs` -- real seam and generated client host. The pinned SDK readiness throw at :2340-2362 becomes the F14 red test.
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/Pd10V2CandidateContractTests.cs` -- the v1 byte pin stays. `CandidateCorrectsFolderAndTaskScopeWithoutRoutingProduction` changes only in R5 commits.
- `docs/contract/pd10-v2-consumer-discovery.md` -- existing 2026-09-19 evidence (a bound artifact), refreshed by R4.
- `scripts/pd10-v2-story-owned-paths.txt`, `scripts/generate-pd10-v2-conformance-set.py`, `_bmad-output/planning-artifacts/generated-v2-conformance-set-2026-09-17.yaml` -- reseal (R6). New test files are appended to the allowlist.
- Unchanged on every ref: `hexalith.folders.v1.yaml`, OQ3 records, `sprint-status.yaml`, the register, the manifest, the A6b/A8/Section 9/DEC-EXEC records, and the GENERATE spec, until the 1.17-G change.

## Tasks & Acceptance

**Notation.** `RUN(P, FQN)` means these two commands, run from the worktree root:
`dotnet build tests/P/P.csproj --configuration Debug -p:UseNuGetDeps=false -m:1`
`tests/P/bin/Debug/net10.0/P -method "FQN"`
Namespaces: `Hexalith.Folders.Tests.Authorization`, `Hexalith.Folders.IntegrationTests.EndToEnd`, `Hexalith.Folders.IntegrationTests`, `Hexalith.Folders.Cli.Tests`, `Hexalith.Folders.Mcp.Tests`, `Hexalith.Folders.Contracts.Tests.OpenApi`, `Hexalith.Folders.UI.Tests`.
Record every red run (command, failing assertion, commit or worktree state) under Implementation Notes before its green edit.

**Execution:**
- [ ] T0 Lane guard -- run the R7.0 main-digest check. Initialize root-declared submodules with `git -c submodule.recurse=false submodule update --init`. Build the dev profile (R7.1) and record the baseline suite counts. No pin changes.
- [ ] T1 F03 red -- `RUN(Hexalith.Folders.IntegrationTests, Hexalith.Folders.IntegrationTests.EndToEnd.GoldenLifecycleParityTests.CandidateMixedAccessStatesResolveIndependentlyOfClaimOrder)`. Theory over `[revoked,stale]`, `[stale,revoked]`, `[disabled,stale]` and `[stale,wrong-tenant]`: both orders produce the byte-identical canonical `503`. Expected red: `[revoked,stale]` yields `404`. Green: make `CanonicalAccessState` order-independent under the frozen precedence rule.
- [ ] T2 F04 red -- in the new class `Hexalith.Folders.Tests.Authorization.GatewayMutationFinalReauthorizationTests`: `RUN(Hexalith.Folders.Tests, …GatewayMutationFinalReauthorizationTests.FolderCreationFinalReauthorizationDetectsFolderAclRevocationBetweenChecks)`, and likewise `RepositoryBackedFolderCreationFinalReauthorizationDetectsFolderAclRevocationBetweenChecks` and `ConfigureProviderBindingFinalReauthorizationDetectsFolderAclRevocationBetweenChecks`. An evidence provider allows on the initial check and reports `revoked` on the final check. Assert a canonical rejection, zero appended events, and final policy layers plus `OperationScope` equal to the initial ones. Expected red: the append happens. Green: the final check mirrors the initial layer set in all three services.
- [ ] T3 F13 red -- `RUN(Hexalith.Folders.IntegrationTests, Hexalith.Folders.IntegrationTests.ArchiveFolderProcessWiringTests.ArchiveFinalReauthorizationEvaluatesTheAuthenticatedPrincipal)`. The upstream authorization has `PrincipalId` ≠ `ActorSafeIdentifier` (and a null actor-safe variant). A recording evidence provider captures the principal used by the final check, which must be the authenticated principal. Expected red: it sees `ActorSafeIdentifier` or `actor_present`. Green: carry the principal on `LayeredFolderAuthorizationAllowedContext` and use it. A missing principal gives the canonical `503`.
- [ ] T4 F11 red -- `RUN(Hexalith.Folders.IntegrationTests, Hexalith.Folders.IntegrationTests.EndToEnd.GoldenLifecycleParityTests.CandidateAuthorizationEvaluationFailureLogsMetadataOnly)`. A throwing tenant accessor carries synthetic tenant and principal sentinels in its message and inner exception. Assert that event 1018 is logged with a null `Exception` and that no captured log field contains either sentinel. The response must be the canonical `503`. Expected red: the exception is attached. Green: log event id, operation id and exception type only.
- [ ] T5 F16 red -- `RUN(Hexalith.Folders.IntegrationTests, …GoldenLifecycleParityTests.CandidateRepositoryBackedProviderReadinessFailureKeepsDeclared422)` (Theory: `CreateRepositoryBackedFolder`, `BindRepository`) and `…CandidateUnknownProviderOutcomeKeepsFinalStateDetail` (Theory: `reconciliation_required`, `unknown_provider_outcome`). Each theory drives the real seam and the generated SDK. Expected red: canonical `503`. Green: declare the tuples and the `finalState` detail key in `generate-pd10-v2-contract.py`, then regenerate the contract, runtime catalog, SDK helpers and parity oracle. Never hand-edit generated files.
- [ ] T6 F14 red -- replace the pinned throw at `GoldenLifecycleParityTests.cs:2348` with `RUN(Hexalith.Folders.IntegrationTests, …GoldenLifecycleParityTests.SdkValidateProviderReadinessBindsAuthorizedOperatorAudience)`. It asserts that the SDK returns the operator-audience shape for a real `200`. Expected red: `HexalithFoldersApiException` with status 200. Green: make the generator or post-processor bind the `ProviderReadiness` `oneOf` discriminator, then regenerate.
- [ ] T7 F15 red -- `RUN(Hexalith.Folders.UI.Tests, Hexalith.Folders.UI.Tests.UiOperationFreshnessConformanceTests.EveryPageSendsEachOperationsAcceptedFreshness)` (Theory: the eight pages in the Code Map). A recording client compares each sent freshness with the v2 contract's accepted value for that `operationId`. Expected red: `eventually_consistent` sent to a restricted operation. Green: the pages send the accepted value for each operation. Keep Fluent UI V5 and FrontComposer usage unchanged.
- [ ] T8 F08 mutation-red regressions -- for each test, record the mutation that turns it red:
  (a) `Hexalith.Folders.Cli.Tests.CommandOptionsFreshnessTests.KnownFreshnessNotAcceptedByOperationIsUsageFailureBeforeDispatch`, `Hexalith.Folders.Mcp.Tests.ToolInputsTests.KnownFreshnessNotAcceptedByOperationIsRejectedBeforeDispatch`;
  (b) `…GoldenLifecycleParityTests.CandidateFileMutationLockConflictRemapsToDeclared423`, `…CandidateFileMutationPreconditionFailureRemapsToDeclared428`;
  (c) `…GatewayMutationFinalReauthorizationTests.RepositoryBindingFinalReauthorizationDetectsRevocationBetweenChecks`, `…BranchRefPolicyConfigurationFinalReauthorizationDetectsRevocationBetweenChecks`, `…FolderAccessMutationFinalReauthorizationDetectsRevocationBetweenChecks`;
  (d) `…GoldenLifecycleParityTests.CandidateCaseVariantDuplicateSchemaPropertyIsCanonicalValidationFailure`;
  (e) `Hexalith.Folders.Contracts.Tests.OpenApi.ParityOracleGeneratorTests.OperationAbsentFromHistoricalV1TakesTheDeclaredBranch`;
  (f) `…ParityOracleGeneratorTests.InitializeBaselineRejectsDuplicateOperationIdsBeforeWriting`;
  (g) `Hexalith.Folders.Mcp.Tests.FailureKindProjectionTests.LockConflictProjectsToClosedMcpFailureKind`.
- [ ] D-7 F17 pin (accepted residual; approved 2026-09-23) -- add `RUN(Hexalith.Folders.Client.Tests, Hexalith.Folders.Client.Tests.ClientGenerationTests.CanonicalErrorCodeNumberingIsPinned)`, which pins every `CanonicalErrorCode` member-to-value pair of the resealed SDK. Record the renumbering for release notes in the 1.17-G preparation. This is a baseline pin, not a red-first fix. Do not change F12.
- [ ] T9 R4 external v1 consumer discovery -- rerun and date these checks, then rewrite `docs/contract/pd10-v2-consumer-discovery.md`, which is a bound artifact:
  - `gh search code '"/api/v1/folders"' --owner Hexalith`
  - `gh search code '"Hexalith.Folders.Client"' --owner Hexalith`
  - `gh api repos/Hexalith/Hexalith.Folders/deployments`
  - `gh release list -R Hexalith/Hexalith.Folders`
  - `curl -s 'https://azuresearch-usnc.nuget.org/query?q=packageid:Hexalith.Folders.Client&prerelease=true'`
  - `git grep -n "/api/v1/folders"` in each root-declared `references/*` checkout and in the local `Hexalith.Projects` checkout, if present.
  Any hit outside `Hexalith/Hexalith.Folders` means stop and escalate.
- [ ] T10 R5 cutover, prepared on the branch only and kept in dedicated commits that can be excluded:
  - Production composition applies the v2 seam.
  - Historical `/api/v1` endpoints stay mapped only for seam-internal dispatch. An endpoint filter keyed to a seam-set dispatch marker returns the canonical `404` to external callers.
  - Guard tests replace `CandidateCorrectsFolderAndTaskScopeWithoutRoutingProduction`:
    - `Hexalith.Folders.Server.Tests.ProductionCompositionRoutingTests.ProductionCompositionServesV2AndNoExternalV1Route`
    - `…ProductionCompositionHasNoV1SelectingClientTarget` (SDK, CLI, MCP and UI base paths).
  - The guard tests assert on the composed endpoint data source, because `Program.cs` refuses to start in Production without a repository (Epic 12). Do not weaken that assertion.
  - Nothing is deployed or activated.
- [ ] T11 R6 regenerate and reseal -- run the R7.3 generator chain. Append each new test file to `scripts/pd10-v2-story-owned-paths.txt`. Run `--initialize-baseline` only after an intentional contract change. Generate the conformance set twice and compare all three copies. Record the new conformance, candidate set, matrix and artifact-count digests. The sealed header stays as generated. Its `approval_status`/`governance_status` wording changes only if Jerome chooses to do so in the 1.17-G change.
- [ ] D-8 Section 9 validator -- in T12, commit the governance validator under `scripts/` before running R7.9, and record its sha256 in the evidence.
- [ ] T12 R7 closure check -- run R7.0 through R7.9 in order against the resealed bytes. Every command must pass. The only permitted non-pass is the intentionally unchanged human-owned A6b approval-register digest assertion, recorded as expected.
- [ ] T13 R2 GENERATE evidence -- write the disposition proposal (see Design Notes) as `_bmad-output/implementation-artifacts/renegotiation-1-17-2026-09-23/generate-evidence-closure-proposal.md`. Apply nothing until Jerome decides.
- [ ] T14 R8 bundle assembly -- prepare, but do not approve, the 1.17-G preparation packages defined in Design Notes. Then stop for human decisions.

**Acceptance Criteria:**
- Given any must-fix finding, when its task completes, then a named test was recorded red at baseline (mutation-red for F08) and is green after the fix, and no other test regressed.
- Given access-state claims in any order, when the seam resolves them, then the outcome and body are identical and follow the frozen precedence rule.
- Given a revocation between the initial and final checks of any gateway effect, when the effect boundary is reached, then no effect is applied and the canonical denial is returned.
- Given an authorization evaluation exception, when it is logged, then no tenant, principal, message or stack content appears in any log field.
- Given each of the 49 operations, when enumerated through the real seam and the generated SDK, then every reachable response, including `422 provider_readiness_failed`, `finalState` and the operator-audience `200`, is declared and consumable, and UI sends only accepted freshness values.
- Given the refreshed discovery evidence, when any external v1 consumer is found, then work stops and escalates.
- Given the branch after T10, when the production composition is inspected, then `/api/v2` is served and no external route or client target selects `/api/v1`; main is unchanged.
- Given the reseal, when the conformance set is regenerated twice, then all three copies are byte-identical and v1 remains `3c3c6680…`.
- Given the session ends at any point before the 1.17-G change, when main is checked with R7.0, then every bound digest is unchanged, no approval is recorded, and Story 1.17 stays `backlog` in both the manifest and the tracker.

## Implementation Notes

### T0 lane guard (2026-09-23)

- Main HEAD `19c29e0`; clean. Main SHA-256: conformance `4c8f33f0cb00afac3a5541eab7b9d30ec15b832d6114316ff672338cc02f5be7`, matrix `d5daa48323c3a117cb3e700b3ef3876ef29566bd6c4fa81a3edeec4d94c4d420`, sprint-status `230889efd5d5e5cfa18b5bbd307e093a581f2434656eae85d6f12ff9573b7c26`, register `ed3160645e9225b1ffff535f950ec9e583991d464e68a3d6dd775de50a0f49f7`, manifest `927641dd75a7a7a0175beae7b36d665adb43b470eea968cb1dd4f19fb27888d7`, publish spec `409d1535ab9e54d7ad175236347c37a9b683249417bcb97ba2d0ee51ddc20a04`, GENERATE spec `5472e48e72abb597e2cc28c10d3f9e93ff5fda59bed19efbf20177411695b136`. All 196 listed artifacts matched length and SHA-256; scratch regeneration of the conformance set matched with `cmp`.
- Branch HEAD `d33dafe`; clean before test editing. Branch spec SHA-256 `91b9513cebd971b4068ebacb3fdb8871f3fd9b1bd193c55fe9e42d8fcb369601`; frozen block byte-identical to approved proposal `f53bdc1ff5b61a05e77719812ea6ffddb3852af03e1ee382f334944cf43a41bd`. `git -c submodule.recurse=false submodule update --init` initialized only the eight root-declared submodules and changed no gitlink.
- R7.1 Debug project-reference restore and build passed, zero warnings and errors. Baseline full suites: Folders.Tests 1976 total, 0 failed, 1 skipped; Server.Tests 757/0/0; IntegrationTests 703/0/0; Cli.Tests 718/0/0; Mcp.Tests 667/0/0; UI.Tests 525/0/0; Client.Tests 324/0/0; Contracts.Tests 340 total, 1 failed, 0 skipped. The pre-existing Contracts failure is `NfrTraceabilityConformanceTests.NfrTraceabilityGatePreservesNativeFailureExitWithoutEchoingOutput`: expected native exit 37, observed 1. Aspire AppHost started in isolated mode; `aspire describe` found waiting resources and `memories` finished; stopped before test builds.

### T1 F03 red

- Worktree state: `d33dafe` plus only the named test in `GoldenLifecycleParityTests.cs`; no production edit. `dotnet build tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj --configuration Debug -p:UseNuGetDeps=false -m:1` passed. `tests/Hexalith.Folders.IntegrationTests/bin/Debug/net10.0/Hexalith.Folders.IntegrationTests -method "Hexalith.Folders.IntegrationTests.EndToEnd.GoldenLifecycleParityTests.CandidateMixedAccessStatesResolveIndependentlyOfClaimOrder"` exited 1: 4 total, 4 failed. Assertion excerpt: `response.StatusCode should be HttpStatusCode.ServiceUnavailable but was HttpStatusCode.NotFound` for `(first: "revoked", second: "stale")`; the same mismatch occurred for the remaining theory rows. This is the specified claim-order failure.
- Red test commit: `1f49615d99894b656c01dbbdba3d936f0ea24cea`.

### T1 F03 green

- `CanonicalAccessState` now selects stale evidence first and selects fresh negative evidence deterministically. The earlier repeated-negative test was renamed and aligned with the approved stale precedence. The named `RUN` test passed: 4 total, 0 failed. Full `tests/Hexalith.Folders.IntegrationTests/bin/Debug/net10.0/Hexalith.Folders.IntegrationTests` passed: 707 total, 0 failed, 0 skipped. No generated file changed.

## Spec Change Log

- 2026-09-23 (draft renegotiation): Proposed a replacement for the "Decision: stop" frozen intent under DEC-EXEC-1.17. Lane: `feat/story-1-17-pd10-v2-closure` from `19c29e0`.
- 2026-09-23 (approved, 1.17-SPEC-RENEGOTIATION): Jerome approved the frozen intent and task list for Product, Architecture, Security and Delivery, each authority stated separately, exactly as proposed (proposal `f53bdc1f…`, diff `2e7e0fdf…`, replacing `409d1535…`). The approval takes effect only on this branch; main keeps `409d1535…`. The frozen block is byte-identical to the approved proposal. Only non-frozen recording fields changed: `status` moved to `ready-for-dev`, and the `renegotiation_approval` frontmatter, this entry and the Design Notes decisions were added. Decisions:
  - D-2: option A, applied inside the 1.17-G change.
  - D-3: unusable-evidence precedence (`503`).
  - D-4: branch-local commits allowed, no push.
  - D-5: v1 stays seam-internal, external `/api/v1` gets the canonical `404`, and guards assert on composed endpoints.
  - D-6: the 1.17-G change lands as a single `--no-ff` merge.
  - D-7: add the F17 pin; do not fix F12.
  - D-8: commit the Section 9 validator.
  Not approved: closing Story 1.17, v2 exposure or routing (DEC-V2-ROUTING-1.17), A6b, Section 9, A8, GENERATE evidence acceptance, any sprint-status or tracker edit, Story 1.18, re-scoping 11.6/13.2, any other story, and any dependency or submodule change.

## Review Triage Log

## Design Notes

**F03 precedence.** `authorization-matrix.md` defines stale as "stale, conflicting, or incomplete" evidence and routes only fresh negative facts to `404`. A claim set that holds both a stale and a fresh negative state is therefore conflicting evidence, so it resolves to `503` whatever the claim order. This follows the existing matrix text and needs no matrix edit. Decided as D-3 on 2026-09-23.

**R5 constraint.** The seam serves v2 by rewriting the request to its historical path and calling the v1 handler (`Pd10V2CandidateCompatibilitySeam.cs:280`). v1 endpoints therefore stay mapped, but only for seam dispatch. "Not exposed" means external requests to `/api/v1` return the canonical `404`. It does not mean the v1 handlers are removed.

**1.17-G closure bundle.** This is one change to main: a single `--no-ff` merge, so main's first-parent history holds one commit. It carries all of the following together:
1. The branch code, generated and test changes from T1–T11, including the R4 evidence and the R5 cutover.
2. The resealed conformance set, plus the matrix if its bytes changed.
3. An A6b re-approval (Product, Architecture, Security) bound to the new matrix and conformance digests, with its own preparation package.
4. A new Section 9 result (S9-01..S9-11) evaluated against the final bundle bytes, including items 5–8.
5. A corrective A8 confirmation (Product, Architecture, Delivery) bound to the new digests and the post-delta sprint-status digest.
6. The DEC-V2-ROUTING-1.17 record. This is a separate v2 exposure/routing decision, and the bundle cannot merge without it.
7. The approved R2 disposition. Under the recommended option, the manifest `1.17-GENERATE.delivery_evidence_status` goes from `open` to `accepted`, and the GENERATE spec is closed with a final change-log entry that cites `c5bea429…`/`4c8f33f0…`.
8. The single-row tracker delta with its own explicit approval. `1-17-publish-the-pd10-v2-authorization-contract-spine` goes from `backlog` to `done`. `git diff --numstat` on `sprint-status.yaml` must equal `1 1`, and no other byte may change. The manifest `1.17` `story_lifecycle_status` and `delivery_evidence_status` change in the same commit.
9. The register records for items 3–8, and the manifest rebound to the final register digest.

Order: finish and self-validate every byte, including the tracker delta, before requesting A6b. Then run Section 9, then A8, DEC-V2-ROUTING-1.17 and the tracker-delta approval, then the post-approval validation, and only then merge. If any approval is revoked or refused, nothing reaches main.

## Verification

**Commands** (from the worktree root unless marked main):
- R7.0 (main and worktree): `sha256sum` of `_bmad-output/planning-artifacts/generated-v2-conformance-set-2026-09-17.yaml`, `docs/contract/authorization-matrix.md`, `_bmad-output/implementation-artifacts/sprint-status.yaml`, `_bmad-output/planning-artifacts/planning-authority-relock-approval-register.yaml` and `_bmad-output/planning-artifacts/planning-story-manifest.yaml`. The expected values are `4c8f33f0…`, `d5daa483…`, `230889ef…`, `ed316064…` and `927641dd…` on main. Also rehash all 196 listed artifacts on main, and on main run `python3 scripts/generate-pd10-v2-conformance-set.py --repository-root . --output <scratch>/conf-main.yaml` followed by `cmp` against the committed set.
- R7.1 dev profile: `dotnet restore Hexalith.Folders.slnx -p:Configuration=Debug -p:UseNuGetDeps=false -m:1`, then `dotnet build Hexalith.Folders.slnx --configuration Debug -p:UseNuGetDeps=false --no-restore -m:1`.
- R7.2 CI profile: `dotnet restore Hexalith.Folders.CI.slnx -p:Configuration=Release -p:UseNuGetDeps=true -m:1`, then `dotnet build Hexalith.Folders.CI.slnx --configuration Release -p:UseNuGetDeps=true --no-restore -warnaserror -m:1`. Then force-rebuild the five gate projects with `dotnet build <Contracts|Client|Cli|Mcp|Integration test csproj> --configuration Release -p:UseNuGetDeps=true --no-restore --no-incremental -warnaserror -m:1`.
- R7.3 generators, each run to scratch and compared with `cmp` against the committed file:
  - `python3 scripts/generate-pd10-v2-contract.py --source src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml --matrix docs/contract/authorization-matrix.md --output <scratch>/v2.yaml`
  - `python3 scripts/generate-pd10-v2-runtime-catalog.py --contract src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v2.yaml --output <scratch>/catalog.g.cs`
  - `dotnet msbuild src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj /t:VerifyHexalithFoldersGeneratedArtifacts /p:Configuration=Debug`
  - `dotnet run --project tests/tools/parity-oracle-generator/Hexalith.Folders.ParityOracleGenerator.csproj -- --repository-root . --contract src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v2.yaml --output <scratch>/parity.yaml`
  - `python3 scripts/generate-pd10-v2-conformance-set.py --repository-root . --output <scratch>/conf-run{1,2}.yaml`, then a three-way `cmp` against the committed set
  - `sha256sum src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` must print `3c3c668071cfaad3e6318626c03051039d00771a4d1afe29ab49df28f71ae4e2`.
- R7.4 full suites, each assembly run with no filter: `tests/P/bin/Debug/net10.0/P` for P in `Hexalith.Folders.Contracts.Tests`, `Hexalith.Folders.Server.Tests`, `Hexalith.Folders.Tests`, `Hexalith.Folders.Client.Tests`, `Hexalith.Folders.Cli.Tests`, `Hexalith.Folders.Mcp.Tests`, `Hexalith.Folders.UI.Tests` and `Hexalith.Folders.IntegrationTests`, plus `samples/Hexalith.Folders.Sample.Tests`.
- R7.5 closure families. Contract: `Pd10V2ContractTests`, `Pd10V2CandidateContractTests`, `AuthorizationMatrixContractTests`, `Pd10ConformanceSetTests`. Golden: `Pd10V2GeneratorTests`, `ParityOracleGeneratorTests`. Safe-denial, authority-unavailable and seam: `GoldenLifecycleParityTests`, `Pd10ProtectedOperationCatalogTests`, `ProductionCompositionRoutingTests`. Adapter: the full Cli and Mcp suites. Each class runs with `-class`.
- R7.6 gates: `pwsh ./tests/tools/run-contract-parity-ci-gates.ps1 -NoRestore` (12/12), `pwsh ./tests/tools/run-contract-spine-gates.ps1`, `pwsh ./tests/tools/run-nightly-drift-gates.ps1`, `pwsh ./tests/tools/run-security-redaction-ci-gates.ps1`, `pwsh ./tests/tools/run-baseline-ci-gates.ps1`, `pwsh ./tests/tools/run-safety-invariant-gates.ps1 -SkipRestoreBuild`, `pwsh ./tests/tools/run-governance-completeness-gates.ps1 -SkipRestoreBuild` and `python3 -m unittest discover -s scripts/tests -p 'test_*.py'`.
- R7.7 UI gates (F15 changes UI): `pwsh ./tests/install-playwright.ps1`, then `pwsh ./tests/tools/run-accessibility-ci-gates.ps1 -SkipBrowserInstall` and `pwsh ./tests/tools/run-e2e-ci-gates.ps1 -SkipBrowserInstall` (full 63, blocking).
- R7.8 hygiene: `dotnet format Hexalith.Folders.slnx --verify-no-changes` and `git diff --check`.
- R7.9 governance validation: the Section 9 manifest, tracker, approval, digest, graph, lifecycle and frozen-spec validator. The run must use a committed validator whose sha256 is recorded; the 2026-09-22 run used `validate_final.py`, sha `acc625e9…`, which is not in the repository (see open decision D-8). Finish with R7.0 on main.
