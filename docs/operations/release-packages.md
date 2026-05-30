# Release Packages

Story 7.9 publishes the NuGet package lane only from a GitHub release event. Package publishing is not part of PR CI, scheduled drift, policy conformance, contract-spine validation, or container archive validation.

## Package Set

The release package manifest is `deploy/nuget/release-packages.yaml`. The pushed package set is explicit and ordered:

| Package ID | Project path | Purpose |
| --- | --- | --- |
| `Hexalith.Folders.Contracts` | `src/Hexalith.Folders.Contracts/Hexalith.Folders.Contracts.csproj` | Contract assembly and Contract Spine metadata. |
| `Hexalith.Folders` | `src/Hexalith.Folders/Hexalith.Folders.csproj` | Core package required for `Hexalith.Folders.Testing` dependency closure. |
| `Hexalith.Folders.Client` | `src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj` | Canonical typed SDK generated from the OpenAPI Contract Spine. |
| `Hexalith.Folders.Aspire` | `src/Hexalith.Folders.Aspire/Hexalith.Folders.Aspire.csproj` | Reusable Aspire orchestration helpers for local and release validation topologies. |
| `Hexalith.Folders.Testing` | `src/Hexalith.Folders.Testing/Hexalith.Folders.Testing.csproj` | Shared consumer test helpers. |

`Hexalith.Folders.ServiceDefaults` and `Hexalith.Folders.Cli` remain packable for their own distribution concerns, but they are excluded from the Story 7.9 push set. Host, server, workers, UI, MCP, samples, and tooling projects stay out of package publishing unless the manifest adds an explicit supported package role.

## Release Trigger And Version Policy

The release workflow is `.github/workflows/release-packages.yml`. It runs on `release: published` and supports `workflow_dispatch` dry-run validation. Release publishing requires an immutable tag in strict `v`-prefixed SemVer form such as `v1.2.3`, `v1.2.3-alpha.1`, or `v1.2.3+build.5`.

Branch names, mutable labels, `latest`, non-`v` tags, blank versions, and invalid SemVer are rejected before packing or publishing. The package version is the release tag without the leading `v`.

## Local Dry Run

Run the same package gate locally without feed credentials:

```powershell
pwsh ./tests/tools/run-release-package-gates.ps1 -Version 0.0.0-local.1 -SourceRevisionId 0123456789abcdef0123456789abcdef01234567
```

The gate restores and builds by default, packs only manifest-listed projects, validates `.nupkg` and `.snupkg` outputs, and writes package artifacts under `_bmad-output/gates/release-packages/packages/`.

When restore/build already ran in the same job, use:

```powershell
pwsh ./tests/tools/run-release-package-gates.ps1 -Version 0.0.0-local.1 -SourceRevisionId 0123456789abcdef0123456789abcdef01234567 -SkipRestoreBuild
```

The evidence report is `_bmad-output/gates/release-packages/latest.json`.

## Feed Configuration And Publish Shape

GitHub Packages publishing uses `GITHUB_TOKEN` with minimum permissions:

```yaml
permissions:
  contents: read
  packages: write
```

The release publish job calls `dotnet nuget push` with an explicit source, API key environment variable, and `--skip-duplicate`. Duplicate skipping is intentional so rerunning the same immutable release can complete when packages already exist on the configured feed.

For NuGet.org or another feed, configure an explicitly named repository secret and pass its environment variable name to `-ApiKeyEnvironmentVariable`. Do not put credentials in `nuget.config`, package metadata, evidence reports, docs examples, or logs.

## Traceability Contract

Every package is traced to:

- release tag and package version;
- full source commit through `RepositoryCommit` and `SourceRevisionId`;
- `FoldersContractMetadata.ContractVersion` from `src/Hexalith.Folders.Contracts/FoldersContractMetadata.cs`;
- OpenAPI spine path `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`;
- package manifest `deploy/nuget/release-packages.yaml`;
- release evidence report `_bmad-output/gates/release-packages/latest.json`;
- same-run gate evidence paths for baseline, contract/parity, security/redaction, capacity smoke, retention/deletion, safety, and governance checks.

`ContractVersion = "0.0.0-scaffold"` is allowed for local dry-run package validation, but it blocks live `Publish` mode until the contract version is advanced intentionally.

## Failure Categories

The release package gate fails closed under these categories:

- `version-policy`: invalid SemVer, mutable labels, or release tag mismatch.
- `source-revision-policy`: blank, short, `local`, `NO_VCS`, or non-SHA source revision.
- `manifest-package-set`: missing package, wrong push set, missing project, or unintentional packability drift.
- `restore-build`: restore/build prerequisite failure when not skipped by same-run CI setup.
- `package-build`: `dotnet pack` failure.
- `package-metadata`: missing repository, license, readme, source commit, or other required NuGet metadata.
- `symbol-packages`: missing `.snupkg` output for a pushed package.
- `dependency-closure`: a pushed package depends on another `Hexalith.Folders*` package outside the push set.
- `release-evidence`: missing Contract Spine evidence, stale release evidence, C3 retention approval blocking live publish, or placeholder contract version in live publish mode.
- `metadata-only-report`: absolute paths or unsafe diagnostic material in generated evidence.
- `publish`: missing feed source, missing API key environment variable, or `dotnet nuget push` failure.

## Submodule Policy

CI checkout uses `submodules: false`. Initialize only root-level build submodules:

```text
git submodule update --init Hexalith.AI.Tools Hexalith.Commons Hexalith.EventStore Hexalith.FrontComposer Hexalith.Memories Hexalith.Tenants
```

Nested recursive submodule initialization is forbidden unless explicitly requested for nested submodule work.

## Metadata-Only Evidence

Reports, docs, workflow logs, packages, and generated evidence must stay metadata-only. Do not include feed credentials, tenant data, provider payloads, raw file contents, local absolute paths, environment dumps, or raw diffs.
