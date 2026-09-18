# Release Packages

Folders uses the shared Hexalith manual semantic-release path. Ordinary pushes and pull requests run CI only; publication starts only from `.github/workflows/release.yml` through `workflow_dispatch`.

## Package Inventory

`tools/release-packages.json` is the single source of truth for the exact five-package release set:

| Package | Project |
| --- | --- |
| `Hexalith.Folders.Contracts` | `src/Hexalith.Folders.Contracts/Hexalith.Folders.Contracts.csproj` |
| `Hexalith.Folders` | `src/Hexalith.Folders/Hexalith.Folders.csproj` |
| `Hexalith.Folders.Client` | `src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj` |
| `Hexalith.Folders.Aspire` | `src/Hexalith.Folders.Aspire/Hexalith.Folders.Aspire.csproj` |
| `Hexalith.Folders.Testing` | `src/Hexalith.Folders.Testing/Hexalith.Folders.Testing.csproj` |

`deploy/nuget/release-packages.yaml` is the deployment policy and points to that JSON inventory; it does not repeat the package list. `Hexalith.Folders.Cli` and `Hexalith.Folders.ServiceDefaults` remain outside the public inventory. Adding either requires a separate product decision.

## Release Preconditions

The release fails closed unless all of these conditions hold:

- the dispatch selected the current `main` tip;
- the exact-source commit has a successful completed `push` run of `ci.yml`;
- the reusable `domain-release.yml` reference and `builds-execution-sha` are the same reviewed full Hexalith.Builds commit;
- the protected `production` environment grants operator approval;
- the repository-level `HEXALITH_RELEASE_PUBLISH_ENABLED` variable is exactly the lowercase string `true`;
- the `NUGET_API_KEY` secret is available explicitly to the reusable workflow;
- the caller declares exactly five packages and `tools/release-packages.json` still contains exactly five unique IDs/projects;
- NuGet.org does not already contain the proposed version for any package.

The protected environment is the publication authority for this repository. The reusable workflow therefore declares `require-publication-authority: false` explicitly; partial or accidental use of the separate issue-comment authority mode is rejected by the local preflight.

Publication remains frozen until maintainers configure the repository variable, protected environment, and secret. This implementation does not create or configure them. As delivered, no package, tag, or GitHub Release has been created.

## Semantic Release Lifecycle

Conventional Commits determine the next version and release notes. Commit messages and prospective squash titles are checked by `.github/workflows/commitlint.yml`. Semantic Release uses `v<version>` tags and runs these phases:

1. `verifyRelease` verifies the NuGet credential and re-proves live `main`, exact-source push CI, immutable Builds identity, protected-environment mode, manifest count, and destination absence.
2. `prepare` invokes `tests/tools/run-release-package-gates.ps1` to pack and seal all five Release/package-mode packages.
3. `publish` repeats the external preflight immediately before the first write, then publishes the prepared packages to NuGet.org.
4. `@semantic-release/github` creates the GitHub Release and attaches all five `.nupkg`/`.snupkg` pairs.

The publisher deliberately does not use `--skip-duplicate`. An occupied version is immutable evidence of a release collision, so publication stops instead of skipping or overwriting it. GitHub Packages is not a publication destination.

## Local Dry Run

Restore and build Release/package-mode assets, then run the same manifest-driven validation without publication credentials:

```powershell
dotnet restore Hexalith.Folders.CI.slnx -p:Configuration=Release -p:UseNuGetDeps=true -m:1
dotnet build Hexalith.Folders.CI.slnx --configuration Release -p:UseNuGetDeps=true --no-restore -warnaserror -m:1
pwsh ./tests/tools/run-release-package-gates.ps1 -Version 0.0.0-local.1 -SourceRevisionId 0123456789abcdef0123456789abcdef01234567 -SkipRestoreBuild
```

The gate delegates packing and validation to:

- `scripts/pack-release-packages.py` for manifest-driven Release/package-mode packing;
- `scripts/validate-nuget-packages.py` for exact inventory, metadata, symbols, archive safety, and dependency closure;
- `scripts/validate-consumer-package-references.py` for isolated package-only consumer restore/build validation.

Exactly five `.nupkg` and five `.snupkg` files are written to `nupkgs/`. The metadata-only report is `_bmad-output/gates/release-packages/latest.json`.

## CI and Supply-Chain Policy

`.github/workflows/ci.yml` delegates standard Release/Microsoft.Testing.Platform build, test, coverage, and consumer validation to Hexalith.Builds. The Folders contract/parity, security/redaction, capacity smoke and calibration, retention/deletion, NFR traceability, safety, governance, accessibility, and end-to-end gates remain additive and blocking for the same commit. CI and release builds select centrally pinned NuGet dependencies through the standard `CI=true` MSBuild property; local Debug development may retain source dependencies.

Checkout uses `submodules: false`, then initializes only root-declared dependencies with the command below. Do not initialize nested submodules by default.

```text
git -c submodule.recurse=false submodule update --init
```

NuGet audit stays enabled. Dependabot covers NuGet, npm, and GitHub Actions; CodeQL and dependency review use the shared Hexalith workflows.

## Failure Handling

- A non-main or stale dispatch, missing exact-source CI proof, or changed Builds identity stops before protected credentials are available.
- A missing environment, release variable, or `NUGET_API_KEY` prevents publication.
- Any manifest, package metadata, symbols, archive, dependency closure, or consumer validation drift stops before publication.
- Any existing NuGet.org version stops the release; do not add `--skip-duplicate` or move an existing tag to bypass the collision.
- NuGet.org has no atomic multi-package transaction. If a transient service failure occurs after one or more packages were accepted, stop the run and treat the version as an immutable partial-publication incident. Do not retry or skip the occupied packages. Record the accepted package IDs, deprecate the incomplete version where possible, create a corrective Conventional Commit so semantic-release calculates a new version, and publish the complete five-package set under that new version.
- If `main` advances while a release is pending, the reusable workflow and local preflight fail it as stale. Run CI for the new tip before dispatching again.

Diagnostics and retained reports are metadata-only: never include credentials, tenant data, provider payloads, raw file content, environment dumps, local absolute paths, or diffs.
