---
baseline_commit: 7f29f80
discovered_inputs:
  sprint_status: _bmad-output/implementation-artifacts/sprint-status.yaml
  epics: _bmad-output/planning-artifacts/epics.md
  prd: _bmad-output/planning-artifacts/prd.md
  architecture: _bmad-output/planning-artifacts/architecture.md
  ux: _bmad-output/planning-artifacts/ux-design-specification.md
  project_context:
    - _bmad-output/project-context.md
    - Hexalith.Commons/_bmad-output/project-context.md
    - Hexalith.EventStore/_bmad-output/project-context.md
    - Hexalith.FrontComposer/_bmad-output/project-context.md
    - Hexalith.Memories/_bmad-output/project-context.md
    - Hexalith.Tenants/_bmad-output/project-context.md
  previous_story: _bmad-output/implementation-artifacts/7-8-wire-scheduled-drift-and-policy-conformance-workflows.md
  latest_technical_sources:
    - https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-pack
    - https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-nuget-push
    - https://learn.microsoft.com/en-us/nuget/create-packages/symbol-packages-snupkg
    - https://docs.github.com/en/actions/use-cases-and-examples/publishing-packages/about-packaging-with-github-actions
    - https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows
    - https://docs.github.com/en/actions/using-workflows/workflow-syntax-for-github-actions
---

# Story 7.9: Publish traceable NuGet release packages

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a downstream consumer,
I want versioned release packages published only after release gates pass,
so that consumers receive traceable and semver-versioned packages.

## Acceptance Criteria

> Epic 7.9 BDD from `_bmad-output/planning-artifacts/epics.md`:
> Given a tagged release is created and gates pass
> When release publishing runs
> Then Contracts, Client, Aspire, and Testing packages are published to the configured feed
> And package metadata traces back to source commit, contract version, and release evidence.

Decomposed acceptance criteria:

1. Add a release-only package workflow, recommended path `.github/workflows/release-packages.yml`, triggered by `release: published` and optionally `workflow_dispatch` for dry-run validation. Do not add package publishing to PR CI, scheduled drift, policy-conformance, or container-image workflows.
2. The workflow must prove release gates before pushing packages. At minimum it must run or consume same-run evidence from restore, build, contract/parity, security/redaction, capacity smoke, governance/safety, and release-package conformance checks. It must fail closed if any prerequisite gate is missing, failed, skipped, or represented only by a stale repository artifact.
3. Package version must be derived from an immutable release tag matching strict SemVer such as `v1.2.3`, `v1.2.3-alpha.1`, or `v1.2.3+build.5`. Reject branch names, mutable labels, `latest`, non-`v` tags, and invalid SemVer before packing or pushing.
4. Publish the configured Folders NuGet package set to the configured feed after gates pass. The minimum named package set from the epic is `Hexalith.Folders.Contracts`, `Hexalith.Folders.Client`, `Hexalith.Folders.Aspire`, and `Hexalith.Folders.Testing`.
5. Resolve the current packability drift explicitly: `Hexalith.Folders`, `Hexalith.Folders.ServiceDefaults`, and `Hexalith.Folders.Cli` currently opt into package/tool pack behavior or root comments describe them as packageable, while the epic's named release set omits them. The implementation must add a machine-readable package manifest and tests that either include them with a documented dependency rationale or exclude them from push without accidentally breaking project-reference package dependencies. Do not blindly `dotnet pack Hexalith.Folders.slnx` and push every `.nupkg`.
6. `Hexalith.Folders.Aspire` must become packageable if it is part of the release set. Keep host, server, workers, UI, MCP, samples, test projects, and tooling projects out of package publishing unless a manifest entry explicitly declares a supported package role.
7. Package metadata must include repository URL/type, source commit, package version, license expression, readme, project URL, description, tags, and deterministic CI build settings. Trace metadata must include `RepositoryCommit`/`SourceRevisionId` from the release SHA and must not fall back to `local`, `NO_VCS`, or a short hash in release mode.
8. Package metadata or a generated release evidence manifest must trace each package to the Contract Spine version from `src/Hexalith.Folders.Contracts/FoldersContractMetadata.cs`, the OpenAPI spine path `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`, the release tag, the source commit, and the gate evidence paths.
9. Release package artifacts must include `.nupkg` packages and `.snupkg` symbol packages where supported. Local/dry-run package validation must inspect package contents and metadata without publishing to a live feed.
10. Push must use the configured feed and token only in the release publish job. For GitHub Packages, use `GITHUB_TOKEN` with `contents: read` and `packages: write`. For NuGet.org or another feed, use an explicitly named repository secret and never write credentials to `nuget.config`, reports, package metadata, docs examples, or logs.
11. Push must use `dotnet nuget push` with an explicit `--source`, API key/token input, and `--skip-duplicate` only when duplicate package handling is intentionally documented for rerunnable releases. Do not use interactive auth in CI.
12. Add a focused release-package gate script, recommended path `tests/tools/run-release-package-gates.ps1`, that can run locally without secrets or live publishing. It should build packages into `_bmad-output/gates/release-packages/packages/`, emit `_bmad-output/gates/release-packages/latest.json`, and fail closed on invalid version, missing package, missing symbol package, wrong publish set, package metadata drift, missing evidence, credential-shaped content, recursive submodule setup, or zero/partial test selection.
13. Add static conformance tests, recommended path `tests/Hexalith.Folders.Contracts.Tests/Deployment/ReleasePackageConformanceTests.cs`, to parse workflow YAML, MSBuild project files, package manifest, release evidence report, and generated package metadata. Tests must assert package-set determinism, release-only triggers, permissions, root-level submodule policy, metadata-only reports, and no package publishing in non-release workflows.
14. Add maintainer documentation, recommended path `docs/operations/release-packages.md`, covering package set, release trigger, tag/version policy, local dry-run command, feed configuration, permissions/secrets, metadata contract, evidence report path, rerun behavior, and failure categories.
15. Reports, docs, workflow logs, packages, and generated evidence must remain metadata-only: no tokens, feed credentials, tenant data, provider payloads, raw file contents, local absolute paths, environment dumps, or raw diffs.
16. Existing PR and scheduled lanes remain usable and scoped: `baseline-build-and-unit-gates`, `contract-and-parity-gates`, `security-and-redaction-gates`, `capacity-smoke-gates`, `nightly-drift-gates`, `policy-conformance-gates`, `contract-generated-artifact-gates`, and container archive validation must not be weakened, renamed, or converted into package-publishing lanes.

## Tasks / Subtasks

- [x] Define the release package contract and reconcile package scope (AC: 4, 5, 6)
  - [x] Add a machine-readable package manifest, suggested path `deploy/nuget/release-packages.yaml` or `docs/operations/release-packages.yaml`, with package IDs, project paths, role, publish mode, dependency rationale, and whether the package is pushed in Story 7.9.
  - [x] Include the epic-mandated package IDs: `Hexalith.Folders.Contracts`, `Hexalith.Folders.Client`, `Hexalith.Folders.Aspire`, and `Hexalith.Folders.Testing`.
  - [x] Decide and document the treatment of currently packable `Hexalith.Folders`, `Hexalith.Folders.ServiceDefaults`, and `Hexalith.Folders.Cli`. If any are included, explain why they are required for dependency closure or consumer use; if excluded, ensure the push script does not pick them up accidentally.
  - [x] Set `<IsPackable>true</IsPackable>` and package metadata on `src/Hexalith.Folders.Aspire/Hexalith.Folders.Aspire.csproj` if Aspire is published.
  - [x] Keep host/deployable projects non-packageable: `AppHost`, `Server`, `Workers`, `UI`, and `Mcp` are not NuGet release packages for this story.

- [x] Harden root package metadata and traceability (AC: 3, 7, 8, 9, 15)
  - [x] Preserve central metadata in `Directory.Build.props`: repository URL/type, license, project URL, package tags, package readme, deterministic build, and CI build settings.
  - [x] Add only narrowly needed release metadata properties, such as `PublishRepositoryUrl`, `EmbedUntrackedSources`, `IncludeSymbols`, `SymbolPackageFormat=snupkg`, or SourceLink support, after verifying generated packages.
  - [x] Ensure release mode passes full commit SHA through `RepositoryCommit` and `SourceRevisionId`; reject `local`, `NO_VCS`, blank, and short hashes.
  - [x] Include `README.md` and license metadata in every published package. Do not duplicate license files unless the NuGet metadata strategy requires it.
  - [x] Record package-to-contract trace evidence using `FoldersContractMetadata.ContractVersion` and the OpenAPI spine path. If `ContractVersion` remains `0.0.0-scaffold`, decide whether this story advances it or blocks release with a clear failure category.

- [x] Add the local release-package gate script (AC: 2, 3, 4, 7-12, 15)
  - [x] Add `tests/tools/run-release-package-gates.ps1` following the established script style: `#Requires -Version 7`, `Set-StrictMode -Version Latest`, `$ErrorActionPreference = 'Stop'`, repository-root resolution from script path, `$LASTEXITCODE` propagation, and `utf8NoBOM` JSON report output.
  - [x] Default mode must be dry-run/package-validation only and must not require credentials or push to a live feed.
  - [x] Validate tag/version input with strict SemVer and fail before `dotnet pack` for invalid or mutable versions.
  - [x] Pack only manifest-listed projects with `dotnet pack -c Release --no-restore` after restore/build, writing packages under `_bmad-output/gates/release-packages/packages/`.
  - [x] Pass `PackageVersion`, `Version`, `RepositoryCommit`, `SourceRevisionId`, `ContinuousIntegrationBuild=true`, and symbol-package properties consistently to every pack operation.
  - [x] Inspect generated `.nupkg`/`.snupkg` files as zip archives to verify package ID, version, repository metadata, readme, license, dependency closure, symbols, and absence of credential-shaped content.
  - [x] Emit `_bmad-output/gates/release-packages/latest.json` with package IDs, versions, project paths, source commit, contract version, evidence paths, category statuses, publish mode, and exit codes only.
  - [x] Support an explicit publish mode only for CI release execution, requiring feed URL/source name and token environment variable. Keep local docs and tests on dry-run unless secrets are intentionally provided by the workflow.

- [x] Add the release GitHub Actions workflow (AC: 1, 2, 3, 10, 11, 16)
  - [x] Add `.github/workflows/release-packages.yml` with `release: types: [published]` and optional `workflow_dispatch` dry-run inputs.
  - [x] Use the established checkout/setup posture: `actions/checkout@v6`, `fetch-depth: 0` or enough history to resolve the release tag and commit, `submodules: false`, explicit root-level submodule initialization only, `actions/setup-dotnet@v5` with `global-json-file: global.json`, and stable NuGet cache inputs.
  - [x] Set minimum permissions. Dry-run validation should need `contents: read`; GitHub Packages publishing should need `contents: read` and `packages: write`. Do not request `id-token`, `deployments`, `pull-requests`, `checks`, or `statuses` unless a documented, tested reporting need is added.
  - [x] Verify the release tag points at the checked-out commit before packing. Fail if the release event SHA, tag commit, and package metadata commit disagree.
  - [x] Run release gates before publish in the same workflow or consume same-run outputs; do not trust checked-in `_bmad-output/gates/*/latest.json` files as proof by themselves.
  - [x] Push packages with `dotnet nuget push` and explicit source/API key arguments. Use `--skip-duplicate` only if docs/tests define rerun behavior.
  - [x] Avoid broad artifact upload. If package artifacts are uploaded for dry-run diagnostics, keep retention short and prove the upload is limited to generated package files and metadata-only evidence.

- [x] Add static conformance coverage (AC: all)
  - [x] Add `tests/Hexalith.Folders.Contracts.Tests/Deployment/ReleasePackageConformanceTests.cs`.
  - [x] Parse the release workflow YAML and assert release-only triggers, optional dry-run dispatch, stable job names, setup posture, permissions, root-level submodule initialization, no recursive setup, no PR/push triggers, and no publishing in non-release workflows.
  - [x] Parse package manifest and project files to assert the package set matches the story contract and project `IsPackable` settings are intentional.
  - [x] Assert `Directory.Build.props` and any package-specific project metadata contain required NuGet traceability fields and do not store credentials.
  - [x] Assert the release gate script validates SemVer, full commit SHA, package IDs, `.nupkg`, `.snupkg`, source commit, contract version, evidence paths, metadata-only diagnostics, and `$LASTEXITCODE`.
  - [x] Assert generated release reports, when present, contain only metadata-only fields and repository-relative paths.
  - [x] Assert `.github/workflows/ci.yml`, `nightly-drift.yml`, `policy-conformance.yml`, `contract-spine.yml`, and container-image gates do not push packages or request package write permissions.

- [x] Document maintainer and consumer handoff (AC: 4, 8, 10, 11, 14, 15)
  - [x] Add `docs/operations/release-packages.md`.
  - [x] Document package IDs, project paths, purpose, dependency closure, release tag format, release workflow trigger, dry-run command, publish command shape, feed configuration, GitHub Packages permissions, non-GitHub feed secret naming, and rerun policy.
  - [x] Document traceability: source commit, release tag, package version, contract version, OpenAPI spine path, gate evidence path, and release evidence report path.
  - [x] State explicitly that package publishing is not part of PR CI, scheduled drift, policy conformance, or container archive validation.
  - [x] Include the canonical root-level submodule command and state that nested recursive initialization is forbidden unless explicitly requested.

- [x] Verification (AC: all)
  - [x] Run `dotnet restore Hexalith.Folders.slnx -m:1 -p:NuGetAudit=false`.
  - [x] Run `dotnet build Hexalith.Folders.slnx --no-restore -m:1`.
  - [x] Run `pwsh ./tests/tools/run-release-package-gates.ps1 -Version 0.0.0-local.1 -SourceRevisionId <full-test-sha>` or the final script's equivalent dry-run command.
  - [x] Run the focused release package conformance tests.
  - [x] Run existing deployment conformance tests that guard Epic 7 lanes: baseline CI, contract/parity CI, security/redaction CI, capacity smoke CI, container image conformance, and scheduled drift/policy workflow conformance.
  - [x] Run `git diff --check`.
  - [x] Run `rg -n -- "--recursive|git submodule update --init --recursive" .github tests/tools docs deploy src` and confirm new work did not introduce recursive setup outside guard/test assertions.
  - [x] If live package publishing cannot be exercised in the sandbox because secrets are unavailable, record dry-run package evidence and leave publish execution to the GitHub release workflow; do not mark live push as locally passed.

## Dev Notes

### Critical Scope Boundaries

- This story adds release package publishing. It does not publish containers, deploy production infrastructure, change Dapr policy semantics, run live provider drift, publish consumer docs, or add product endpoints.
- Do not add package publishing to `.github/workflows/ci.yml`, `.github/workflows/nightly-drift.yml`, `.github/workflows/policy-conformance.yml`, or `.github/workflows/contract-spine.yml`.
- Do not blindly publish every `*.nupkg` found under the repository. Use an explicit package manifest and exact expected package IDs.
- Do not initialize nested submodules recursively. The allowed setup command is root-level only:

```text
git submodule update --init Hexalith.AI.Tools Hexalith.Commons Hexalith.EventStore Hexalith.FrontComposer Hexalith.Memories Hexalith.Tenants
```

### Current State To Preserve

- `Directory.Build.props` already centralizes deterministic build settings, package license, project URL, repository URL/type, tags, readme inclusion, and default `IsPackable=false`.
- Current packable projects include `src/Hexalith.Folders.Contracts`, `src/Hexalith.Folders`, `src/Hexalith.Folders.Client`, `src/Hexalith.Folders.Cli` as a .NET tool, `src/Hexalith.Folders.ServiceDefaults`, and `src/Hexalith.Folders.Testing`.
- `src/Hexalith.Folders.Aspire/Hexalith.Folders.Aspire.csproj` is currently not packable but is named by the epic acceptance criteria; this is the highest-risk project-file change in the story.
- `src/Hexalith.Folders.Testing/Hexalith.Folders.Testing.csproj` references `Hexalith.Folders` and `Hexalith.Folders.Contracts`, so package dependency closure must be tested before excluding the core package from push.
- `src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj` generates client and idempotency helper files from the OpenAPI Contract Spine before compile. Do not hand-edit generated files to make packages pass.
- `src/Hexalith.Folders.Contracts/FoldersContractMetadata.cs` currently reports `ContractVersion = "0.0.0-scaffold"`. Release package traceability must intentionally handle or update that value.
- `nuget.config` currently contains only public nuget.org restore source and no credentials. Keep publish credentials out of this file.
- Existing Epic 7 gate scripts emit metadata-only reports under `_bmad-output/gates/<gate>/latest.json`; follow that pattern for release packages.

### Architecture Compliance

- Architecture I-5 names GitHub Actions as the CI/CD mechanism and separates PR gates, scheduled drift/policy gates, and release evidence.
- PRD NFR release readiness requires build, dependency, package, and generated SDK artifacts to be traceable to source and free of secrets or tenant data.
- Project context says pack policy is opt-in, versions are centralized in `Directory.Packages.props`, and package metadata/diagnostics must be metadata-only.
- The SDK is the typed canonical client. CLI, MCP, and UI wrap `Hexalith.Folders.Client`; do not duplicate SDK behavior while changing package metadata.
- Source and generated artifacts are public contracts. Package version, error category names, OpenAPI spine, generated client, parity rows, and idempotency helpers must remain synchronized.

### Latest Technical Notes

- `dotnet pack` creates `.nupkg` packages and can create symbols/source packages; for CI use `--no-build` only when restore/build already ran, otherwise let the pack step build deliberately.
- Starting with .NET 10, `dotnet pack` can pack `.nuspec` files or file-based apps, but this repository should keep SDK-style project packing unless a manifest-specific reason is documented.
- NuGet's recommended modern symbol package format is `.snupkg`; use `SymbolPackageFormat=snupkg` rather than legacy `.symbols.nupkg`.
- `dotnet nuget push` pushes an existing package; it does not create packages. Use explicit `--source` and token/API-key input in CI, and prefer `--skip-duplicate` only for documented rerun semantics.
- GitHub release workflows can run on `release` events such as `published`; draft releases do not trigger every release activity type consistently, so use `published` for the release package lane.
- GitHub Packages publishing through Actions should use `GITHUB_TOKEN` with `contents: read` and `packages: write`; avoid broad token permissions.

### Previous Story Intelligence

- Story 7.8 established the current Epic 7 pattern: separate workflow, stable job name, root-only submodules, setup-dotnet from `global.json`, focused PowerShell script under `tests/tools`, metadata-only report under `_bmad-output/gates/<gate>/latest.json`, static conformance tests, and operations documentation.
- Story 7.8 intentionally excluded package publishing, container publishing, release upload, and semantic-release from scheduled workflows. Preserve that lane separation.
- Story 7.7 and 7.8 both guard against vacuous `dotnet test --filter` success. Release package tests and scripts must prove expected checks actually executed.
- Recent commits show Epic 7 is consolidating release-readiness gates one lane at a time: `7f29f80 feat(story-7.8): Wire scheduled drift and policy-conformance workflows`, `d003e60 feat(story-7.7): Add capacity-smoke CI gate`, `5e72383 feat(story-7.6): Consolidate security and redaction CI gates`, `d93f1fd feat(story-7.5): Consolidate contract and parity CI gates`, and `e9f59a3 feat(story-7.4): consolidate baseline build and unit CI gates`.

### Project Structure Notes

- Likely NEW files:
  - `.github/workflows/release-packages.yml`
  - `deploy/nuget/release-packages.yaml` or `docs/operations/release-packages.yaml`
  - `tests/tools/run-release-package-gates.ps1`
  - `tests/Hexalith.Folders.Contracts.Tests/Deployment/ReleasePackageConformanceTests.cs`
  - `docs/operations/release-packages.md`
  - `_bmad-output/gates/release-packages/latest.json` generated by local/release runs
  - `_bmad-output/gates/release-packages/packages/*.nupkg` generated by local/release runs
  - `_bmad-output/gates/release-packages/packages/*.snupkg` generated by local/release runs
- Likely UPDATE files:
  - `Directory.Build.props` for narrowly needed package traceability properties.
  - `src/Hexalith.Folders.Aspire/Hexalith.Folders.Aspire.csproj` to opt into packaging and package-specific metadata.
  - Potential package-scope updates in `src/Hexalith.Folders/Hexalith.Folders.csproj`, `src/Hexalith.Folders.ServiceDefaults/Hexalith.Folders.ServiceDefaults.csproj`, or `src/Hexalith.Folders.Cli/Hexalith.Folders.Cli.csproj` only after manifest-driven scope resolution.
  - `src/Hexalith.Folders.Contracts/FoldersContractMetadata.cs` if release policy requires contract version to match the package release version.
  - `docs/exit-criteria/c0-c13-governance-evidence.yaml` only to record release-package evidence paths/commands without prematurely approving unrelated criteria.
  - `_bmad-output/implementation-artifacts/sprint-status.yaml` during workflow status changes.
- Do not update generated clients, parity oracle rows, OpenAPI operation contracts, Dapr policy YAML, provider drift fixtures, container image bindings, scheduled workflow scripts, or security/redaction fixtures unless a focused release-package conformance failure proves they are directly stale.

### Testing Requirements

- Use repository-pinned .NET SDK `10.0.302` from `global.json` and central package versions from `Directory.Packages.props`.
- Use xUnit v3, Shouldly, YamlDotNet, XML parsing, and zip inspection patterns already present in deployment and scaffold conformance tests.
- Tests should parse YAML and XML rather than relying only on string contains where practical.
- Release-package script tests should fail closed if the package manifest and actual generated packages disagree.
- Generated package validation should inspect `.nuspec` content inside `.nupkg` files and verify no credentials, local absolute paths, tenant data, or raw source/diff payloads appear in package metadata or release evidence.
- If live feed publishing is not available locally, validate dry-run package output and ensure workflow-level publish steps are statically tested.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-7.9`] - Story statement and BDD acceptance criteria.
- [Source: `_bmad-output/planning-artifacts/prd.md#Non-Functional-Requirements`] - Build, dependency, package, and generated SDK artifacts must be traceable to source and secret-free.
- [Source: `_bmad-output/planning-artifacts/architecture.md#Infrastructure-And-Deployment`] - GitHub Actions gate separation and release-readiness posture.
- [Source: `_bmad-output/project-context.md#Technology-Stack-And-Versions`] - .NET SDK, package management, generated client, and test tooling versions.
- [Source: `_bmad-output/project-context.md#Code-Quality-And-Style-Rules`] - Pack policy is opt-in and package metadata must remain metadata-only.
- [Source: `Directory.Build.props`] - Current root package metadata and default `IsPackable=false`.
- [Source: `Directory.Build.targets`] - Container publishing uses `SourceRevisionId`; release packages should use the same full-commit trace discipline.
- [Source: `src/Hexalith.Folders.Contracts/FoldersContractMetadata.cs`] - Current contract version constant.
- [Source: `nuget.config`] - Restore sources are public and credential-free.
- [Source: `_bmad-output/implementation-artifacts/7-8-wire-scheduled-drift-and-policy-conformance-workflows.md#Previous-Story-Intelligence`] - Epic 7 workflow/script/report pattern and package-publishing exclusion.
- [Source: Microsoft Learn, `dotnet pack`, checked 2026-05-30] - `dotnet pack` creates NuGet packages and supports symbol/source package options. https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-pack
- [Source: Microsoft Learn, `dotnet nuget push`, checked 2026-05-30] - Push publishes existing packages using configured server/credential details and explicit source options. https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-nuget-push
- [Source: Microsoft Learn, NuGet `.snupkg`, checked 2026-05-30] - `.snupkg` is the modern NuGet symbol package format. https://learn.microsoft.com/en-us/nuget/create-packages/symbol-packages-snupkg
- [Source: GitHub Docs, publishing packages with Actions, checked 2026-05-30] - GitHub Packages publishing should use `GITHUB_TOKEN` with package write permission where applicable. https://docs.github.com/en/actions/use-cases-and-examples/publishing-packages/about-packaging-with-github-actions
- [Source: GitHub Docs, events that trigger workflows, checked 2026-05-30] - Release workflows can use release activity types such as `published`. https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows
- [Source: GitHub Docs, workflow syntax, checked 2026-05-30] - Workflow/job `permissions` define `GITHUB_TOKEN` access. https://docs.github.com/en/actions/using-workflows/workflow-syntax-for-github-actions

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- 2026-05-30: Created story context from BMAD `bmad-create-story` workflow, Story 7.9 request, Epic 7 BDD, PRD release package traceability NFRs, architecture CI/CD guidance, project context, current package/project files, existing Epic 7 workflows/scripts/tests/docs, Story 7.8 previous-story intelligence, recent git history, and current official GitHub/Microsoft/NuGet documentation.
- 2026-05-30: Validation pass checked story for common implementation traps: solution-wide package push, hidden live publishing in PR/scheduled lanes, ignoring current packability drift, publishing without dependency closure, using mutable/non-SemVer versions, missing full commit trace, stale checked-in gate reports as release proof, credential leakage in `nuget.config` or evidence, missing `.snupkg`, recursive submodule setup, and fabricated live feed evidence.
- 2026-05-30: Implemented manifest-driven release package scope, package metadata hardening, Aspire packability, dry-run/publish release gate script, release-only GitHub Actions workflow, static conformance tests, and maintainer documentation.
- 2026-05-30: Fixed direct package determinism for `Hexalith.Folders.Testing` by pinning project-reference target frameworks and packing with `-m:1`; solution build already passed, while direct parallel project-reference packing failed without diagnostics.
- 2026-05-30: Validation run completed: `dotnet restore Hexalith.Folders.slnx -m:1 -p:NuGetAudit=false`, `dotnet build Hexalith.Folders.slnx --no-restore -m:1`, release package dry-run gate, xUnit v3 in-process release conformance tests, xUnit v3 in-process Epic 7 deployment conformance tests, `git diff --check`, and recursive submodule scan. `dotnet test` through VSTest was attempted and blocked by sandbox socket permissions (`SocketException 13`), so xUnit v3 in-process execution was used for the relevant test classes.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Story is scoped to release-only NuGet package publishing with explicit package manifest, focused dry-run gate, release workflow, static conformance tests, package metadata traceability, and operations documentation.
- Current package scope conflict is called out as an implementation requirement: the epic names Contracts, Client, Aspire, and Testing while current project files also make core, ServiceDefaults, and CLI packable/tool-packable.
- Existing PR, scheduled, contract-spine, and container archive lanes are preserved as constraints and must not become package-publishing lanes.
- Added `deploy/nuget/release-packages.yaml` and resolved scope drift by publishing the epic-mandated packages plus `Hexalith.Folders` for `Hexalith.Folders.Testing` dependency closure; `Hexalith.Folders.ServiceDefaults` and `Hexalith.Folders.Cli` are explicitly excluded from Story 7.9 publishing.
- Added release package gate script that validates SemVer/source SHA policy, packs only manifest-listed projects, inspects `.nupkg` and `.snupkg` metadata, emits `_bmad-output/gates/release-packages/latest.json`, and supports live publish mode only with explicit feed/source and API key environment variable.
- Added release-only GitHub Actions workflow with same-run prerequisite gates and publish gated behind successful release package conformance; PR, scheduled, contract-spine, policy, and container lanes remain non-publishing.
- Live feed publishing was not executed locally because release feed credentials are intentionally unavailable in the sandbox; dry-run package evidence was generated and live push remains delegated to the GitHub release workflow.

### File List

Committed source/config:

- `.github/workflows/release-packages.yml`
- `Directory.Build.props`
- `deploy/nuget/release-packages.yaml`
- `docs/operations/release-packages.md`
- `src/Hexalith.Folders.Aspire/Hexalith.Folders.Aspire.csproj`
- `src/Hexalith.Folders.Testing/Hexalith.Folders.Testing.csproj`
- `tests/Hexalith.Folders.Contracts.Tests/Deployment/ReleasePackageConformanceTests.cs`
- `tests/tools/run-release-package-gates.ps1`
- `tests/tools/run-baseline-ci-gates.ps1` (review fix: wire `ReleasePackageConformanceTests` into the per-PR baseline lane)
- `_bmad-output/implementation-artifacts/7-9-publish-traceable-nuget-release-packages.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

Generated dry-run evidence (NOT committed — `*.nupkg`/`*.snupkg` are `.gitignore`d; the report is regenerated on each gate run):

- `_bmad-output/gates/release-packages/latest.json`
- `_bmad-output/gates/release-packages/packages/Hexalith.Folders{,.Aspire,.Client,.Contracts,.Testing}.<version>.{nupkg,snupkg}`

### Change Log

- 2026-05-30: Implemented Story 7.9 release package lane with manifest-driven NuGet package scope, traceable metadata, dry-run package evidence, release-only GitHub Actions publishing, conformance tests, and operations documentation.
- 2026-05-30: Adversarial code review (auto-fix). Wired AC13 `ReleasePackageConformanceTests` into the per-PR baseline lane, documented the `SetTargetFramework` packing workaround in `Hexalith.Folders.Testing.csproj`, and corrected the File List to mark generated `.nupkg`/`.snupkg` as gitignored evidence. Status set to done (0 critical findings).

## Senior Developer Review (AI)

**Reviewer:** jpiquot — 2026-05-30
**Outcome:** Approve (with applied auto-fixes)
**Scope reviewed:** all 16 ACs and tasks against the actual implementation; full File List read; git reality cross-checked.

### Verification executed during review

- `dotnet test ...Contracts.Tests --filter ...Deployment.ReleasePackageConformanceTests` → 8/8 passed.
- Baseline lane combined filter (smoke + baseline + release conformance) → 16/16 passed (non-vacuous; confirms Fix 1 selects real tests).
- `pwsh ./tests/tools/run-release-package-gates.ps1 -Version 0.0.0-local.1 -SourceRevisionId <HEAD>` → `status=passed`, exit 0 (5 packages + 5 symbol packages, metadata + dependency-closure validated). Re-run after the csproj edit still passes.
- `git diff --check` clean; recursive-submodule scan clean.

> Note: the dev record reported VSTest blocked by sandbox `SocketException 13`; that does not reproduce in this environment, so the conformance tests and the dry-run gate were actually executed here.

### Findings

- **[MEDIUM] AC13 conformance tests were inert in CI — FIXED.** `ReleasePackageConformanceTests` was not referenced by any gate script or workflow, so the release-workflow/manifest/metadata guards never ran in automation (a latent Epic-7 pattern: 7.5–7.8 conformance classes are also unwired; only `BaselineCiWorkflowConformanceTests` runs). Fix scoped to this story: added the class to `tests/tools/run-baseline-ci-gates.ps1` so it runs on every PR alongside the existing baseline conformance test. Strengthens, does not weaken, the baseline lane (AC16 preserved).
- **[MEDIUM] Undocumented `SetTargetFramework` packing workaround — FIXED.** `Hexalith.Folders.Testing.csproj` pinned its two project references to `TargetFramework=net10.0` with no rationale; both refs are already single-target net10.0, so a maintainer could remove it and reintroduce the intermittent direct-pack failure. Added an explanatory comment tying it to the release gate.
- **[LOW] File List listed gitignored build artifacts as deliverables — FIXED.** The ten `*.nupkg`/`*.snupkg` files are `.gitignore`d and never committed; the File List now separates committed source from regenerated dry-run evidence.
- **[LOW] `--no-symbols` push leaves `.snupkg` unpublished + dead `*.symbols.nupkg` filter (NOT changed, acceptable).** With `SymbolPackageFormat=snupkg` there are no `*.symbols.nupkg` files, so the exclusion filter is dead code, and snupkg is intentionally not pushed because GitHub Packages has no snupkg symbol server. Behavior is correct for the configured feed; left as-is to avoid changing release-time push semantics.

### Not findings (verified OK)

- Release-only triggers, fail-closed job ordering (`needs` + `if needs.*.result == 'success'`), minimum permissions (`contents: read` / `packages: write`), strict `v`-SemVer + 40-hex SHA enforcement, manifest-driven scope (Contracts/Client/Aspire/Testing + core for closure; ServiceDefaults/Cli excluded; hosts non-packable), metadata-only evidence, and non-release-workflow no-push assertions all verified against code and passing tests.
