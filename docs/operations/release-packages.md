# Release Packages

Folders uses the shared Hexalith manual semantic-release path with NuGet trusted publishing. Ordinary pushes and pull requests run CI only; publication starts only from `.github/workflows/release.yml` through `workflow_dispatch`. Preparation stays in the shared Builds action; NuGet login and semantic-release run in the Folders-owned protected job so NuGet receives the package repository's workflow identity.

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
- the shared `Github/prepare-domain-release` action reference and `builds-execution-sha` are the same reviewed full Hexalith.Builds commit;
- the protected `production` environment grants operator approval;
- the effective `vars.HEXALITH_RELEASE_PUBLISH_ENABLED` value supplied to preparation is exactly the lowercase string `true`;
- a matching trusted publishing policy is registered in the individual creator's NuGet account, and repository variable `NUGET_USER` names that creator;
- the caller declares exactly five packages and `tools/release-packages.json` still contains exactly five unique IDs/projects;
- NuGet.org does not already contain the proposed version for any package.

The protected environment is the publication authority for this repository. The caller-owned publication job therefore supplies `HEXALITH_RELEASE_REQUIRE_AUTHORITY: 'false'` explicitly; partial or accidental use of the separate issue-comment authority mode is rejected by the local preflight.

Publication remains frozen whenever the effective publication variable is absent or differs from the exact untrimmed lowercase value `true`, including `TRUE`, `True`, and padded values. An absent repository variable can inherit an organization value, including `true`; absence at repository scope alone does not freeze publication. Set an explicit repository override: `true` to authorize publication or `false` to freeze it regardless of an organization `true`. Frozen preparation concludes successfully and skips NuGet login and semantic-release. A missing or whitespace-only `NUGET_USER` fails an enabled run before token exchange. Rejected OIDC authentication fails before semantic-release can create another release tag; there is no stored-key fallback.

The Folders workflow resolves `vars.HEXALITH_RELEASE_PUBLISH_ENABLED` and passes it through the shared action's `publication-flag` input. The composite reads that input rather than the unavailable `vars` context. An omitted input defaults to empty and leaves publication frozen; the exact untrimmed shell comparison and caller-visible `publish-enabled` verdict remain the publication gate.

## Trusted Publishing Setup

`deploy/nuget/trusted-publishing-policy.yaml` is the concrete repository policy definition to register manually on NuGet.org. It is not a native NuGet API import, and committing this file does not register a remote policy. In the individual creator's [NuGet trusted publishing account page](https://www.nuget.org/account/trustedpublishing), register these exact fields:

| Field | Value |
| --- | --- |
| Policy name | `folders-production` |
| Individual policy creator | `jpiquot` |
| Package owner | `Hexalith` |
| Publisher | `GitHub` |
| GitHub repository owner | `Hexalith` |
| Repository | `Hexalith.Folders` |
| Workflow file | `release.yml` |
| Environment | `production` |
| Scope | `PackagePushVersion` (push new versions of existing packages) |
| Glob Patterns and Packages | The five exact package IDs from the inventory table, one per line; no wildcard |

The individual NuGet policy creator `jpiquot` differs from the package owner `Hexalith` and the GitHub actor. Repository variable `NUGET_USER` must equal `jpiquot`, whose account must register the policy through legitimate authenticated access. The policy grants new-version publication only to the five existing package IDs. Keep normal reviewer approval on the `production` environment and set `HEXALITH_RELEASE_PUBLISH_ENABLED` explicitly at repository scope when publication is authorized.

When selecting `Hexalith` as a NuGet organization owner, `jpiquot` must be an active member of that organization. Removing the creator from the organization makes the policy inactive; restoring their membership reactivates it. Confirm active membership and policy status before dispatching an enabled release. See [NuGet policy ownership requirements](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing#policy-ownership-warnings).

The pinned `NuGet/login` action requests GitHub OIDC and exchanges it for a temporary NuGet key immediately before semantic-release. The same Folders job has `id-token: write`, `actions: read`, and only the semantic-release `contents`, `issues`, and `pull-requests` write permissions. The publication step's `NUGET_API_KEY` environment value comes exclusively from `steps.nuget-login.outputs.NUGET_API_KEY`; never map a stored `NUGET_API_KEY` secret, transfer the temporary key across jobs, or print it.

Registration status: `NUGET_USER=jpiquot` is configured in the GitHub repository. On 2026-10-02 the `jpiquot` account registered the `folders-production` policy on NuGet.org with exactly the fields above. NuGet lists it as active and bound to GitHub repository ID `1229485448` and owner ID `80614290`. Release run `37001706122` then authenticated through `NuGet/login` and published `1.1.1`. If the YAML definition changes, update the registered policy in the same account; the file itself never updates NuGet.org.

## Semantic Release Lifecycle

Conventional Commits determine the next version and release notes. Commit messages and prospective squash titles are checked by `.github/workflows/commitlint.yml`. Semantic Release uses `v<version>` tags and runs these phases:

1. Before the lifecycle begins, shared preparation re-proves live `main` and successful exact-source push CI, then NuGet login mints the temporary credential in the protected Folders job. `verifyRelease` verifies its presence and repeats source proof, immutable shared action identity, protected-environment mode, manifest count, and destination absence.
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

## Read-only Verification on 2026-10-08

At `2026-10-08T13:11:40Z`, read-only GET requests to the five NuGet.org flat-container version indexes returned HTTP 200 and each advertised `1.1.1`:

| Package version index | HTTP status | Advertised version |
| --- | --- | --- |
| [Hexalith.Folders.Contracts](https://api.nuget.org/v3-flatcontainer/hexalith.folders.contracts/index.json) | 200 | `1.1.1` |
| [Hexalith.Folders](https://api.nuget.org/v3-flatcontainer/hexalith.folders/index.json) | 200 | `1.1.1` |
| [Hexalith.Folders.Client](https://api.nuget.org/v3-flatcontainer/hexalith.folders.client/index.json) | 200 | `1.1.1` |
| [Hexalith.Folders.Aspire](https://api.nuget.org/v3-flatcontainer/hexalith.folders.aspire/index.json) | 200 | `1.1.1` |
| [Hexalith.Folders.Testing](https://api.nuget.org/v3-flatcontainer/hexalith.folders.testing/index.json) | 200 | `1.1.1` |

This observation confirms version-index availability for the existing release. It does not prove a new exact-source publication of the CI repairs. No version, release dispatch, deployment, remote policy, or tag was changed; `v1.1.0` and `v1.1.1` remain immutable.

The local Release/package-mode dry run passed with:

```powershell
pwsh tests/tools/run-release-package-gates.ps1 -Version 0.0.0-local.1 -SourceRevisionId 4affd6e530756b094b8081e7f9afb138856a03af -SkipRestoreBuild
```

The same-run baseline gate had already passed Release restore/build, whitespace, analyzers, hermetic suites, and dependency-mode checks. After refreshing the deterministic candidate conformance inventory for the repaired generator and golden tests, contract-spine and all twelve contract-parity categories passed. The release dry run produced exactly five packages and five symbol archives, validated package metadata, archive safety and dependency closure, and built two isolated package-only consumers covering all five packages. `_bmad-output/gates/release-packages/latest.json` records `mode: DryRun`, `status: passed`, and publication as `skipped-dry-run`.

The supplied source revision is baseline HEAD metadata. The local archives include the uncommitted working-tree repairs and are unpublished `0.0.0-local.1` artifacts; this evidence does not establish exact committed-source publication or replace successful push CI for a future release. The existing NuGet `1.1.1` indexes refer to the prior publication.

The separate baseline Aspire probe used `aspire start --apphost src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj --isolated --non-interactive --format Json`. It did not establish a running AppHost: two `aspire describe` checks and `aspire stop` reported no running AppHost, and the owned startup was cancelled with `Stopping Aspire` and exit 0. This is an unestablished startup baseline, not a passing topology gate.

## CI and Supply-Chain Policy

`.github/workflows/ci.yml` delegates standard Release/Microsoft.Testing.Platform build, test, coverage, and consumer validation to Hexalith.Builds. The Folders contract/parity, security/redaction, capacity smoke and calibration, retention/deletion, NFR traceability, safety, governance, accessibility, and end-to-end gates remain additive and blocking for the same commit. CI and release builds select centrally pinned NuGet dependencies through the standard `CI=true` MSBuild property; local Debug development may retain source dependencies.

Checkout uses `submodules: false`. The shared package-only CI and release lanes
retain their nonrecursive initialization command:

Do not initialize nested submodules by default.

```text
git -c submodule.recurse=false submodule update --init
```

The seven cyclic source roots have `update = none`, so implicit updater clones
and that default command skip them. The four acyclic roots (AI.Tools, Builds,
Commons and PolymorphicSerializations), including the shared package catalog,
remain available. Folders CI also prepares the single pinned Memories root through
the integration test project's CI-only build target: packaged Aspire topology
registration needs the server project and its real HTTP launch profile on disk.
Project dependencies still resolve through NuGet, and nested dependencies remain
uninitialized. Local workflow gates and full source setup explicitly override
the skip policy while keeping initialization limited to root dependencies:

Existing `submodule.<name>.update` values in local `.git/config` take precedence
over `.gitmodules` during initialization. The explicit `--checkout` overrides
either strategy. Repeat this canonical command after parent gitlinks change to
select their newly recorded commits; plain updates still skip roots configured
as `none`. Explicit root operands also avoid selection through `submodule.active`.

Do not initialize nested submodules by default.

```text
git submodule update --init --checkout references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.McpCli references/Hexalith.Memories references/Hexalith.Platform references/Hexalith.PolymorphicSerializations references/Hexalith.Projects references/Hexalith.Tenants
```

The checkout override selects root source content without enabling recursive or
remote updates.

NuGet audit stays enabled. Dependabot covers NuGet, npm, and GitHub Actions; CodeQL and dependency review use the shared Hexalith workflows.

## Failure Handling

- A non-main or stale dispatch, missing exact-source CI proof, or changed Builds identity stops before protected credentials are available.
- A missing environment, exact publication variable, creator variable, or registered matching policy prevents publication. Do not rotate or reintroduce a long-lived key to bypass a trusted publishing failure.
- Any manifest, package metadata, symbols, archive, dependency closure, or consumer validation drift stops before publication.
- Any existing NuGet.org version stops the release; do not add `--skip-duplicate` or move an existing tag to bypass the collision.
- NuGet.org has no atomic multi-package transaction. If a transient service failure occurs after one or more packages were accepted, stop the run and treat the version as an immutable partial-publication incident. Do not retry or skip the occupied packages. Record the accepted package IDs, deprecate the incomplete version where possible, create a corrective Conventional Commit so semantic-release calculates a new version, and publish the complete five-package set under that new version.
- If `main` advances while a release is pending, shared preparation and the local preflight fail it as stale. Run CI for the new tip before dispatching again.
- Release run `36973683976` packed the five `1.1.0` packages but failed on the first NuGet upload with HTTP 403. Its immutable `v1.1.0` tag remains untouched. A subsequent corrective Conventional Commit can produce `1.1.1`; do not move or delete the failed tag, skip duplicates, or alter dependency versions to recover. After account setup and exact-source CI, dispatch Release from current `main`, obtain normal production approval, and verify all five NuGet version archives and ten GitHub package/symbol assets. Recovery completed on 2026-10-02 in release run `37001706122`, which tagged `v1.1.1` at `d5f49e96dfab10bf2839ec263a343a6a4c13b06f`. NuGet accepted all ten package and symbol pushes, and the GitHub Release carries all ten assets. Runs `36985567814` and `36992233355` had failed earlier at OIDC token exchange (HTTP 401) because no Folders policy was registered yet, so they created no tag and published nothing.

Diagnostics and retained reports are metadata-only: never include credentials, tenant data, provider payloads, raw file content, environment dumps, local absolute paths, or diffs.
