---
title: 'Fix CI and verify NuGet publication'
type: 'bugfix'
created: '2026-10-08'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '4affd6e530756b094b8081e7f9afb138856a03af'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Push CI on `main` (`c8e3d99`) is red, so `.github/workflows/release.yml` cannot prove a new exact-source publication. `folders-specialized-gates` fails whitespace, `contract-spine-gates` fails the PD10 generator, and the previous push CI (`37749981464`) also failed the Aspire SDK pin, the stable Dapr inventory assertion, and four repository-backed create calls that return 404.

**Approach:** Restore those gates without changing the committed v2 contract, authorization guarantees, five-package inventory, or trusted-publishing workflow. Perform the read-only publication check selected by the user on 2026-10-08.

## Boundaries & Constraints

**Always:** Preserve concurrent changes, keep `V1Only` active, and leave Story 1.17 open until its remaining migration gates pass. Keep `hexalith.folders.v2.yaml` byte-for-byte. Make `scripts/generate-pd10-v2-contract.py` emit the committed extensions again. Keep `Aspire.Hosting` `13.6.1` as the catalog pin and `CommunityToolkit.Aspire.Hosting.Dapr` `13.0.0` only for `Hexalith.Folders.Aspire`. Keep the four golden-lifecycle expected statuses. Leave `v1.1.0` and `v1.1.1` immutable.

**Never:** Do not edit the Hexalith.Builds submodule, `tools/release-packages.json`, `release.yml` pins, or `NuGet/login`. Do not add `--skip-duplicate`, a stored NuGet key, or a second Dapr override in the root `Directory.Packages.props`. Do not regenerate the v2 contract down to today's generator output.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Format gate | `MutateFilesIdempotencyIntentAdapterTests.cs` lines 57 and 79 | `dotnet format whitespace --verify-no-changes` exits 0 | Formatter owns the brace break |
| AppHost SDK | Catalog `Aspire.Hosting` `13.6.1`; Builds exception already `13.6.1` | `Hexalith.Folders.AppHost.csproj` SDK is `Aspire.AppHost.Sdk/13.6.1` | Do not retarget other AppHosts |
| Stable Dapr | Catalog property condition for `Hexalith.Folders.Aspire` is `13.0.0` | `StableReleaseShouldUseStableDaprIntegration` passes by reading that property | Missing condition fails the test |
| PD10 generator | Committed v2 includes idempotency behavior, read-key denial, and current `validation_error` order | Generator stdout bytes equal the committed file | Do not delete those extensions |
| Repository-backed creation | Valid request for an existing folder with fresh administer authority | 202, one downstream call, allow audit | Missing or unauthorized folder remains safe-denial 404 without downstream dispatch |
| Declared provider outcome | Same route, downstream 422, 409, or 503 | That status and category pass through | Bind-repository cases already pass; leave them |

**Publication decision:** Read-only: verify all five NuGet `1.1.1` indexes and local Release pack/consumer gates. No new version, dispatch, deployment, or tag mutation.

**Authorization decision:** The user's requirement to preserve the committed v2 contract and authorization guarantees controls the repair. `CreateRepositoryBackedFolder` creates a backing repository for an existing folder; correct stale parity fixture prerequisites while retaining 202/422/409/503 expectations. Do not restore the removed tenant-only bypass.

</frozen-after-approval>

## Code Map

- `tests/Hexalith.Folders.EventStore.Tests/MutateFilesIdempotencyIntentAdapterTests.cs` -- formatter owns two `with` brace breaks.
- `src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj` -- SDK 13.6.0 is stale; Builds catalog and exception require 13.6.1.
- `tests/Hexalith.Folders.Contracts.Tests/Deployment/ReleasePackageConformanceTests.cs` -- inspect `references/Hexalith.Builds/Props/Directory.Packages.props`: conditioned `HexalithAspireHostingDaprVersion` equals 13.0.0 only for Folders.Aspire; the PackageVersion consumes it. Do not add a root override or modify Builds.
- `scripts/generate-pd10-v2-contract.py:transform_operation` -- restore committed idempotency extensions at their ordered positions and leading validation_error category. Write generated checks to temporary paths only.
- `tests/Hexalith.Folders.IntegrationTests/EndToEnd/GoldenLifecycleParityTests.cs` -- seed existing folder and `manage_folder_access` with `SeedFolder` / `SeedPermissionsForAction` for four creation rows; bind rows already seed these. Retain downstream statuses and assert call counts/audit. Add missing, cross-tenant, absent/administer-denied, write-only, revoked, stale, and unavailable-authority coverage where absent, including known and unknown Content-Length. Assert canonical resource_unavailable redacted denial, one deny audit, and zero downstream calls.
- `src/Hexalith.Folders.Server/Authorization/Pd10ProtectedOperationCatalog.cs` and `Pd10V2CandidateCompatibilitySeam.cs` -- RequestFolder + FolderAdministration authorization is intentional. Commit `a20127c` removed the tenant bypass. Do not change authorization to make these tests pass.
- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs:Handle(CreateRepositoryBackedFolder)` -- requires IsCreated and Unbound. Committed v2 summary and authorization requirement explicitly require an existing folder.
- `docs/operations/release-packages.md`, `tools/release-packages.json`, `tests/tools/run-release-package-gates.ps1` -- existing five-package read-only/dry-run path.

## Tasks & Acceptance

**Execution:**
- [x] `tests/Hexalith.Folders.EventStore.Tests/MutateFilesIdempotencyIntentAdapterTests.cs` -- apply scoped whitespace formatter.
- [x] `src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj` -- align SDK to catalog 13.6.1.
- [x] `tests/Hexalith.Folders.Contracts.Tests/Deployment/ReleasePackageConformanceTests.cs` -- assert conditioned Builds stable Dapr property, consumption, and absence of duplicate local override.
- [x] `scripts/generate-pd10-v2-contract.py` -- reproduce committed v2 bytes, preserving extensions and order.
- [x] `tests/Hexalith.Folders.IntegrationTests/EndToEnd/GoldenLifecycleParityTests.cs` -- correct authorized existing-folder fixtures, retain four expected statuses and bind outcomes, prove missing/unauthorized requests never dispatch.
- [x] `_bmad-output/planning-artifacts/generated-v2-conformance-set-2026-09-17.yaml` -- regenerate the deterministic inventory for the changed generator and golden test hashes; retain pending A6b, disabled production exposure, false closure/routing, the full path inventory, and immutable v1/matrix hashes. This is candidate metadata, not approval resealing.
- [x] `docs/operations/release-packages.md` -- record read-only five-index observation plus local dry-run evidence with truthful source attribution.

**Acceptance Criteria:**
- Given the failing gate set, when baseline, scaffold, stable-Dapr and PD10 generator checks run, then they pass and the committed v2 bytes remain unchanged.
- Given authorized existing-folder creation, when the downstream returns 202, 422, 409, or 503, then the existing expected status/category passes through with one dispatch and allow audit; bind rows still pass.
- Given missing, cross-tenant, or insufficient folder authority, when repository creation is requested, then safe-denial 404 occurs with zero downstream calls; unusable authority remains 503.
- Given read-only publication verification, when all checks finish, then all five indexes advertise 1.1.1 and local Release package/consumer gates pass without remote writes.

## Implementation Notes

- 2026-10-08: User-directed continuation resolved read-only publication and preservation of existing-folder authorization. Applied only scoped formatting, SDK pin, conditioned Dapr assertion, generator emission, authorized fixture prerequisites and denial regressions; runtime authorization remained unchanged. The regenerated 224-path candidate inventory changed only generator/test hashes and combined digest `299d5cf79ccf38edda84adda8ce847e8068b361e7daae311007297d969c7e720`.
- Final gates: baseline 36/36 checks; contract-spine 141/141 OpenAPI plus 34/34 generation; parity 12/12 categories; golden lifecycle 82/82 cases including 16 new denials; release conformance 9/9. Package dry run produced five nupkg and five snupkg and passed two isolated consumer builds. All five read-only NuGet indexes advertised 1.1.1 at 2026-10-08T13:11:40Z. Gate-generated parity/release reports were retained as fresh evidence.
- Parent independently read the complete working-tree diff, verified all seven tasks and every acceptance/matrix row against passing gates, validated all 224 raw-byte artifact hashes, and compared protected files to baseline HEAD. v1/v2, authorization matrix/runtime, package inventory/catalog/workflow, routing and Story 1.17/tracker were unchanged. V1Only remains active; migration approval, exposure, observation, retirement and closure remain pending. No staging, commit, push, release dispatch or dependency/submodule update occurred.
- Aspire baseline startup did not reach a running AppHost and was cancelled; `aspire ps --non-interactive --format Json` returned an empty list afterward. This does not establish topology readiness. Local 0.0.0-local.1 package metadata carries baseline revision `4affd6e530756b094b8081e7f9afb138856a03af` while archives include uncommitted repair bytes; no exact-source publication is claimed.

- Final review patches parameterized successful creation for both body lengths and asserted complete provider problem tuples. Parent reran baseline (36/36), contract-spine (141/141 plus 34/34), parity (12/12 categories, golden 83/83), and package dry-run gates against the final tree; all exited 0. Python CI/CD tooling tests also passed 38/38. Final candidate digest is `1363d306d5901a5807a3a4cf57ba4b0d31c885580fa36f76b8688754e456c65f`; all 224 entries match final bytes. Two pre-existing review follow-ups were recorded in the deferred-work ledger; neither grants migration authority. Clarification: the expressly requested AppHost SDK pin changed to 13.6.1; catalog and submodule dependency pins did not change.

- Workflow complete: CI repair spec is done; Story 1.17 and sprint status remain unchanged. Repository instructions reserve staging/committing for an explicit request, so the reviewed repair remains uncommitted. The skill's default local-commit step was not applied.

## Spec Change Log

- 2026-10-08: Required contract-spine checks found only the two repaired candidate artifact hashes stale. Expanded the agent-owned task list to regenerate their deterministic conformance inventory. KEEP pending A6b, disabled production exposure, false closure/routing, v2 bytes and authorization guarantees. No signed approval or Story 1.17 state changes.

## Review Triage Log

| Finding | Verdict / route | Evidence |
| --- | --- | --- |
| B1 effective stable Dapr evaluation | false / reject | The requested assertion targets the conditioned Builds property and checks its actual PackageVersion consumption plus absence of root overrides. The built Aspire nupkg also contains Dapr 13.0.0 and isolated consumers restore it; no divergent assignment/import occurs in the unchanged graph. |
| B2 stable override exclusivity | false / reject | Exact project-name equality limits the stable property to Folders.Aspire; the unchanged catalog's unconditional default remains preview. Root broad overrides are rejected; no current downgrade of the AppHost/test graph was shown. |
| B3 missing generator ordering anchors | low / reject | Omitting anchors in a modified source can omit policy emission, but the authoritative historical v1 is immutable and contains every anchor. Current generation matches all committed bytes; adding guards for malformed alternate sources would add complexity for a case outside ordinary use. |
| B4 inherited generator keys | low / reject | Injecting conflicting derived extensions into a custom source can overwrite generated values. Immutable historical v1 contains neither extension; no production caller supplies the altered source. New input sanitization branches are unnecessary for this repair. |
| B5 authorized unknown-length success coverage | low / patch | New denial rows cover streaming bodies, while the corrected success fixture only covers known length. Parameterize the same success case to verify legitimate streamed creation dispatches once after body authorization. |
| B6 foreign permission snapshot boundary | medium / defer | The partitioned cross-tenant fixture correctly returns safe denial, but the unchanged EffectivePermissionsFolderPermissionEvidenceProvider does not compare returned snapshot tenant/folder identity to the request. A fixed reader returning foreign fresh evidence can therefore pass its grant checks. This pre-existing reader-boundary gap was not introduced by these fixture repairs; record separate scope-validation follow-up. |
| B7 malformed/unproven revocation authority coverage | false / reject | FolderPermissionEvidenceProviderTests already cover future/malformed evidence and unproven revocation freshness for strict/mutation policies; Pd10ProtectedOperationExecutorTests checks incomplete/stale canonical 503 with zero protected reads. These ran in the baseline suites; unchanged layering maps those results fail-closed. |
| B8 full provider response tuple assertions | low / patch | Corrected create/bind fixtures still asserted a subset of downstream problem fields. Add code, correlation, retryability and visibility assertions so restored outcomes exercise the complete declared tuple. |
| E1 missing operation policy anchor | low / reject | Same malformed-alternate-source case as B3; committed source has required anchors and the current byte gate passes. Extra rejection guards would add complexity without a reachable ordinary input regression. |
| E2 missing root policy anchor | low / reject | The immutable historical root contains the TTL-tier anchor, and both root extension declarations reproduce exactly. Removing it changes the forbidden historical input; no current contract omission occurs. |
| V1 AppHost running-topology verification | medium / defer | Pre-verified gap: topology boot test skips unless HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION is enabled; normal CI leaves it unset and the local startup probe was unestablished. Existing DCP lane work belongs to Story 11.15. Keep this limitation separate from passing local repair gates. |

## Design Notes

The original draft's missing-folder acceptance conflicts with the committed contract, domain aggregate, and the later security fix. User-directed contract preservation resolves it through fixture repair, retaining every downstream outcome assertion. No runtime authorization change is needed. Intent gaps: none. Irreversibles: none. Footprint: six repair/evidence files, their generated candidate conformance manifest, plus this spec; no new public API.

## Verification

- `python3 -m unittest discover -s scripts/tests -p 'test_*.py'` -- 38 CI/CD tooling tests pass.
- `pwsh tests/tools/run-baseline-ci-gates.ps1` -- Release restore/build, format, analyzers, hermetic suites and dependency modes pass.
- `pwsh tests/tools/run-contract-spine-gates.ps1 -NoRestore` -- generator and generated artifacts pass.
- `pwsh tests/tools/run-contract-parity-ci-gates.ps1 -NoRestore` -- all twelve categories pass.
- `dotnet tests/Hexalith.Folders.Contracts.Tests/bin/Release/net10.0/Hexalith.Folders.Contracts.Tests.dll -class '*ReleasePackageConformanceTests'` -- full stable-Dapr and release policy class passes.
- `dotnet tests/Hexalith.Folders.IntegrationTests/bin/Release/net10.0/Hexalith.Folders.IntegrationTests.dll -class '*GoldenLifecycleParityTests'` -- creation, bind and denial coverage passes.
- `pwsh tests/tools/run-release-package-gates.ps1 -Version 0.0.0-local.1 -SourceRevisionId 4affd6e530756b094b8081e7f9afb138856a03af -SkipRestoreBuild` -- five packages, five symbols and package-only consumers pass. This is an unpublished working-tree dry run, not exact committed-source publication.

Baseline Aspire probe was started with `aspire start --apphost src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj --isolated --non-interactive --format Json`; record its result separately from hermetic gates. Do not install dependencies, initialize nested submodules, commit, stage, push, or overwrite concurrent edits. Run required Release gates sequentially to avoid shared build-output races. Use xUnit v3 direct assembly selectors; project `--filter` is unreliable under Microsoft.Testing.Platform.
