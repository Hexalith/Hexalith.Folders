---
title: 'Fix CI and verify NuGet publication'
type: 'bugfix'
created: '2026-10-08'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Push CI on `main` (`c8e3d99`) is red, so `.github/workflows/release.yml` cannot prove a new exact-source publication. `folders-specialized-gates` fails whitespace, `contract-spine-gates` fails the PD10 generator, and the previous push CI (`37749981464`) also failed the Aspire SDK pin, the stable Dapr inventory assertion, and four repository-backed create calls that return 404.

**Approach:** Restore those gates without changing the published contract, the five-package inventory, or the trusted-publishing workflow. Then perform the publication check chosen below.

## Boundaries & Constraints

**Always:** Keep `hexalith.folders.v2.yaml` byte-for-byte. Make `scripts/generate-pd10-v2-contract.py` emit the committed extensions again. Keep `Aspire.Hosting` `13.6.1` as the catalog pin and `CommunityToolkit.Aspire.Hosting.Dapr` `13.0.0` only for `Hexalith.Folders.Aspire`. Keep the four golden-lifecycle expected statuses. Leave `v1.1.0` and `v1.1.1` immutable.

**Never:** Do not edit the Hexalith.Builds submodule, `tools/release-packages.json`, `release.yml` pins, or `NuGet/login`. Do not add `--skip-duplicate`, a stored NuGet key, or a second Dapr override in the root `Directory.Packages.props`. Do not regenerate the v2 contract down to today's generator output.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Format gate | `MutateFilesIdempotencyIntentAdapterTests.cs` lines 57 and 79 | `dotnet format whitespace --verify-no-changes` exits 0 | Formatter owns the brace break |
| AppHost SDK | Catalog `Aspire.Hosting` `13.6.1`; Builds exception already `13.6.1` | `Hexalith.Folders.AppHost.csproj` SDK is `Aspire.AppHost.Sdk/13.6.1` | Do not retarget other AppHosts |
| Stable Dapr | Catalog property condition for `Hexalith.Folders.Aspire` is `13.0.0` | `StableReleaseShouldUseStableDaprIntegration` passes by reading that property | Missing condition fails the test |
| PD10 generator | Committed v2 includes idempotency behavior, read-key denial, and current `validation_error` order | Generator stdout bytes equal the committed file | Do not delete those extensions |
| Missing repository-backed folder | `POST /api/v2/folders/repository-backed` with a valid new `folderId` | 202, one downstream call, allow audit | 404 before the historical handler is the bug |
| Declared provider outcome | Same route, downstream 422, 409, or 503 | That status and category pass through | Bind-repository cases already pass; leave them |

</frozen-after-approval>

## Open Questions

- Publication check — options: **Read-only** (confirm the five `1.1.1` package indexes at `https://api.nuget.org/v3-flatcontainer/<id>/index.json` still list `1.1.1`, and the local Release pack gates pass; no new version and no `workflow_dispatch`) / **Publish** (after this fix is on `main` with green push CI, dispatch Release and confirm a new five-package version on nuget.org plus ten GitHub assets; versions are immutable and the `production` environment must approve).

## Code Map

- `.github/workflows/ci.yml` — push CI. `folders-specialized-gates` runs `./tests/tools/run-baseline-ci-gates.ps1`, which calls `dotnet format whitespace Hexalith.Folders.CI.slnx --verify-no-changes --include ./src/ ./tests/ ./samples/`.
- `.github/workflows/contract-spine.yml` — reruns the PD10 generator test. No publish step.
- `.github/workflows/release.yml` — `workflow_dispatch` from live `main` only after a successful push CI for that SHA. Reuse; do not retune.
- `docs/operations/release-packages.md` — operator path. Last success is run `37001706122`, version `1.1.1`. `v1.1.0` stays a failed immutable tag.
- `src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj:1` — still `Aspire.AppHost.Sdk/13.6.0`.
- `references/Hexalith.Builds/Props/Directory.Packages.props` — `Aspire.Hosting` `13.6.1`; `HexalithAspireHostingDaprVersion` is the preview for everyone else and `13.0.0` when `MSBuildProjectName` is `Hexalith.Folders.Aspire`. Root `Directory.Packages.props` only imports this file.
- `references/Hexalith.Builds/Tools/package-version-exceptions.json` — Folders AppHost exception version is already `13.6.1`.
- `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs:328` — requires the AppHost SDK to equal the catalog `Aspire.Hosting` version.
- `tests/Hexalith.Folders.Contracts.Tests/Deployment/ReleasePackageConformanceTests.cs:227` — still looks for a local `PackageVersion Update="CommunityToolkit.Aspire.Hosting.Dapr"`, which is why `Single()` throws.
- `scripts/generate-pd10-v2-contract.py` `transform_operation` — drops `x-hexalith-idempotency-behavior`, `x-hexalith-read-idempotency-key`, and moves `validation_error`. Committed v2 has 92 hunks of that drift. v1 does not contain those keys.
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/Pd10V2GeneratorTests.cs:20` — byte compare.
- `src/Hexalith.Folders.Server/Pd10V2CandidateCompatibilitySeam.cs` — v2 `CreateRepositoryBackedFolder` uses `RequestFolder`. A resolved `folderId` goes through layered folder authorization before historical dispatch. Safe denial is HTTP 404.
- `src/Hexalith.Folders.Server/Authorization/Pd10OpaqueIdentifier.cs` — `folder_create_0001` is a valid opaque id. Do not loosen this grammar.
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs:164` — historical `POST /api/v1/folders/repository-backed`. Bind route at line 179 already satisfies the passing theory rows.
- `tests/Hexalith.Folders.IntegrationTests/EndToEnd/GoldenLifecycleParityTests.cs:1578` — the four failures are the create route only. `bindRepository: true` rows passed on run `37749981464`.
- `tests/Hexalith.Folders.EventStore.Tests/MutateFilesIdempotencyIntentAdapterTests.cs:57` and `:79` — whitespace error at column 17.

## Tasks & Acceptance

**Execution:**
- [ ] `tests/Hexalith.Folders.EventStore.Tests/MutateFilesIdempotencyIntentAdapterTests.cs` -- apply the whitespace formatter -- the specialized gate fails before tests.
- [ ] `src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj` -- set the SDK to `Aspire.AppHost.Sdk/13.6.1` -- match the catalog and the existing Builds exception.
- [ ] `tests/Hexalith.Folders.Contracts.Tests/Deployment/ReleasePackageConformanceTests.cs` -- assert the Builds `HexalithAspireHostingDaprVersion` condition for `Hexalith.Folders.Aspire` is `13.0.0` and contains no prerelease marker -- the local props file no longer holds `PackageVersion` rows.
- [ ] `scripts/generate-pd10-v2-contract.py` -- emit the committed idempotency-behavior block, read idempotency-key denial, and `validation_error` order -- the generator test is a byte gate, not a license to shrink the contract.
- [ ] `src/Hexalith.Folders.Server/Pd10V2CandidateCompatibilitySeam.cs` -- let `CreateRepositoryBackedFolder` authorize a folder that does not exist yet and then reach the historical handler -- create calls currently die as 404 while bind calls pass. Touch `FoldersDomainServiceEndpoints.cs` only if the historical handler, not the seam, is what returns 404.
- [ ] Publication evidence -- run the check chosen in Open Questions and record the package ids, version, and run or index URLs in `docs/operations/release-packages.md` only when that check observes a new fact.

**Acceptance Criteria:**
- Given `c8e3d99`'s gate set, when baseline format, the scaffold test, the Dapr test, and `GeneratorReproducesTheCommittedCandidateByteForByte` run, then each exits 0 and `hexalith.folders.v2.yaml` is unchanged.
- Given the golden create tests, when `POST /api/v2/folders/repository-backed` is called for a missing folder and for downstream 422, 409, and 503, then the response matches the existing assertions and the bind-repository rows still pass.
- Given the chosen publication check, when it finishes, then either all five `1.1.1` indexes still advertise `1.1.1`, or a new complete five-package version is visible on nuget.org with ten GitHub assets. A partial publish stops as an incident under the existing release doc.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

The Dapr stable pin already lives in the Builds catalog property. The failing test is stale, so retarget the assertion. The AppHost SDK string is the stale one; the exception inventory is already `13.6.1`.

The generator diff removes contract policy that v1 never had and that the committed v2 still has. Restoring emission keeps the byte gate honest. Rewriting the yaml would publish a smaller contract.

`folder_create_0001` passes `Pd10OpaqueIdentifier`. The 404 is consistent with RequestFolder authorization treating a not-yet-created folder as a safe denial before `HistoricalPath` runs. Bind rows seed an existing folder and use the route id, which is why they passed.

## Verification

**Commands:**
- `dotnet format whitespace Hexalith.Folders.CI.slnx --verify-no-changes --include ./tests/Hexalith.Folders.EventStore.Tests/MutateFilesIdempotencyIntentAdapterTests.cs` -- expected: exit 0
- `dotnet test tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj --filter RootBuildConfigurationOwnsTargetFrameworkAndPackageVersions` -- expected: pass
- `dotnet test tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --filter "FullyQualifiedName~StableReleaseShouldUseStableDaprIntegration|FullyQualifiedName~GeneratorReproducesTheCommittedCandidateByteForByte"` -- expected: pass
- `dotnet test tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj --filter "FullyQualifiedName~CandidateAuthorizesRepositoryBackedCreationOfAMissingFolder|FullyQualifiedName~CandidateRepositoryCreateAndBindPreserveDeclaredProviderOutcome"` -- expected: pass, including the bind rows
