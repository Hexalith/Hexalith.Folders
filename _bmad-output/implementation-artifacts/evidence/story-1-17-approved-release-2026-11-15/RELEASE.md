# Story 1.17 approved release execution

EventStore **5.1.1** was published after the user's explicit approval. The release supplies the native scanner APIs required by Folders and Projects. Conversations Client/Contracts **2.0.0** were already published; their earlier restore failure came from the offline feed. Fresh consumer restores now use NuGet.org directly.

The immutable private qualification handoff and its 593-file manifest remain historical evidence. This receipt supersedes their unpublished-candidate and pending-publication status. Retained one-use job scripts record this execution and are not supported reusable tooling.

## Published artifacts

- Source and tag `v5.1.1`: `f792bba6c0564d3c36dda6ad1c2fc55b0b0a6331`.
- [GitHub release and fourteen NuGet package assets](https://github.com/Hexalith/Hexalith.EventStore/releases/tag/v5.1.1). All fourteen package families are also available on NuGet.org. [Signed public package provenance](../../../../references/Hexalith.EventStore/_bmad-output/implementation-artifacts/evidence/story-1-17-published-release-2026-11-15/published-packages.json) records catalog SHA-512, signature validation and source identity.
- Container `registry.hexalith.com/eventstore:5.1.1`, index digest `sha256:0e6ccefe997f70fb374c23430cd58c24765e08dd2cbd53177e92a4a6a63f2de6`.
- Linux amd64 manifest `sha256:cf936585b5fb97b4d33347f81f3c6f35206128927326de2a56e388e323ec8507`; arm64 manifest `sha256:82b7e04d97423ecdb5e06488858537917f87cd8f85b4834830f7f2097f0c0bec`.

The release workflow authenticated to the registry, validated both architectures and their exact source/version labels, passed Development `/alive` smoke checks, and removed its smoke resources. Retained raw OCI documents were independently checked against their digests. A separate unauthenticated live registry read returned 401; it supplies no additional live verification. See [container verification](container-verification.json) and the extracted [release evidence](release-evidence/).

## Source and publication gates

- [Exact-source CI](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/43719644714) and [commitlint](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/43719644629) succeeded before publication.
- [Release workflow 43723266764](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/43723266764) succeeded on its first attempt. Its source preflight passed and the normal production reviewer approval was recorded under the user's approval. No environment protection settings were changed.
- Fourteen packages and fourteen symbols archives were rebuilt at the committed source and passed the owner package validator and API compatibility checks. The release workflow also passed its API gate. All fourteen NuGet archives match every unsigned ZIP entry in the corresponding GitHub release archives; NuGet signatures account for the archive-byte difference. See [payload comparison](github-nuget-payload-comparison.json).

**Source landing exception:** the scoped push of the five-file EventStore preparation commit to `main` succeeded using existing maintainer permissions. GitHub reported bypassed rule violations requiring a pull request and seven expected status checks. The checks then ran successfully on that exact commit, and publication waited for those results. The [push log](eventstore-push.log) preserves the original server response. No other repository was committed or pushed.

## Consumer verification

The public-package verification uses disposable snapshots, a fresh package cache, only NuGet.org, Release/package mode, audits enabled at all/low, and warnings as errors. Both complete `Hexalith.Projects.CI.slnx` and `Hexalith.Folders.CI.slnx` restore and build successfully. Focused tests pass: Projects adapters **221**, Folders native authority **64**, and Folders routing **20**. Every requested selector matches passing, unskipped cases. Contract parity passes **12/12** categories and governance passes.

Both Builds catalog gates pass, including **254** exact compatibility probes, **17** independent frozen-V1/packaged-host controls, **68** approved package identities, four shared versions and three canonical families. The platform catalog resolves EventStore Aspire 5.1.1 with the required Dapr range. The final consumer receipt verifies **32 actual runtime DLL bindings** against the signed public package members, including Folders 1.4.0's net10 assemblies in Projects' net11 runtime.

See the [public qualification result](../story-1-17-published-release-2026-11-15/result.json), [eleven completed passing commands](../story-1-17-published-release-2026-11-15/completed-commands.json), [runtime bindings](../story-1-17-published-release-2026-11-15/runtime-package-bindings.json) and [independent archive check](independent-public-check.json). The original duplicate catalog attempt was deliberately interrupted only after both full replacement gates and runtime capture succeeded with `DOTNET_CLI_TELEMETRY_OPTOUT=1`; audits, package inputs and source stayed unchanged. Its nonzero exit remains visible in the original `commands.json` and [interruption receipt](duplicate-catalog-interruption.json). The disposable workspaces were removed after all owned commands finished.

## Workspace and remaining boundary

The owning Builds catalog and Projects override select published EventStore 5.1.1. Their scoped catalog changes remain uncommitted: two files in Builds and one file in Projects. The SDK 4.2 catalog views are preserved.

V1Only, the canonical tracker, activation and migration T0 remain unchanged. All 15,016 protected root files and the 593 approved evidence artifacts retain their recorded hashes; see [preservation check](preservation-check.json). No application was deployed, and Story 1.17 was not marked complete. The preexisting `unrepresented-unbounded-query-fallback` runtime completeness refusal remains a separate deployment qualification boundary.

The [machine-readable release receipt](RELEASE.json) records the final outcome. The additive [artifact manifest](artifact-manifest.json) hashes this execution evidence and all four owning public-verification evidence directories; its own checksum is retained in `artifact-manifest.sha256`. The manifest and its checksum are excluded from their own inventory.
