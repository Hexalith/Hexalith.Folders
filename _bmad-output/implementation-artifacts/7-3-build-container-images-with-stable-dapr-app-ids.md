---
baseline_commit: 4efa6372b1ca1fa8ce8201620b349da5274df323
---

# Story 7.3: Build container images with stable Dapr app IDs

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a platform operator,
I want one container image per service with stable Dapr app IDs,
so that deployment policy applies consistently across environments.

## Acceptance Criteria

> Epic 7.3 BDD from `_bmad-output/planning-artifacts/epics.md`:
> Given server, workers, and UI projects build
> When container images are produced
> Then image metadata and app IDs are stable for local, staging, and production
> And deployment manifests attach sidecars and preserve access-control assumptions.

Decomposed acceptance criteria:

1. `Hexalith.Folders.Server`, `Hexalith.Folders.Workers`, and `Hexalith.Folders.UI` each publish as a distinct .NET SDK container image without Dockerfile duplication unless a documented SDK limitation blocks the built-in container path.
2. Container repository names are stable and match the deployment contract: `hexalith-folders-server` or an explicitly documented alias for the server image, `hexalith-folders-workers`, and `hexalith-folders-ui`. If retaining the existing server repository value `folders`, document the alias and prove production manifests do not confuse image names with Dapr app ID `folders`.
3. Every service image has deterministic metadata sufficient for promotion: OCI labels for source repository, commit/revision, version, service name, and owning project; no labels, args, env vars, or generated artifacts contain secrets, provider tokens, tenant data, production URLs, or raw file contents.
4. The container publish path supports local and CI/offline validation through `dotnet publish -c Release --os linux --arch x64 /t:PublishContainer` or `-p:PublishProfile=DefaultContainer`, with either archive output or a local OCI daemon path. The story must not require pushing to a live registry.
5. Local Aspire topology, production Dapr sidecar bindings, and deployment manifests use the same Dapr app IDs: `eventstore`, `tenants`, `folders`, `folders-workers`, and `folders-ui`.
6. Production deployment evidence attaches Dapr sidecars to `folders`, `folders-workers`, and `folders-ui` service deployments with `dapr.io/enabled: "true"`, exact `dapr.io/app-id`, and exact `dapr.io/config` values matching `deploy/dapr/production/accesscontrol.yaml`.
7. The UI sidecar contract is explicit. If `folders-ui` remains sidecar-enabled in production, local Aspire must also attach a Dapr sidecar with app ID `folders-ui`; if UI does not need Dapr APIs, production policy and sidecar evidence must be intentionally narrowed instead of leaving a local/production mismatch.
8. Conformance tests prove the image metadata contract, project container settings, stable app IDs, production sidecar bindings, and no-secret/no-recursive-submodule invariants.
9. Given a container-publish failure occurs, when the container-image gate runs, then the contract is **coarse pass/fail**: any non-zero `dotnet publish /t:PublishContainer` exit sets the gate report `status: failed` with the captured `command_exit_code`, and the gate never contacts a live registry. Per-mode classification — registry-unreachable, base-image-pull failure, RID/runtime-restore failure — is an **explicit MVP non-goal** (no distinct handling, assertion, or retry); registry/promotion behavior is owned by CI/release tooling outside the sanitized repo artifacts.

## Tasks / Subtasks

- [x] Normalize container publishing metadata for all service hosts (AC: 1, 2, 3, 4)
  - [x] Review `src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj`, `src/Hexalith.Folders.Workers/Hexalith.Folders.Workers.csproj`, and `src/Hexalith.Folders.UI/Hexalith.Folders.UI.csproj`.
  - [x] Add missing Workers container settings. At minimum set a stable repository such as `hexalith-folders-workers`; use central/shared MSBuild properties if that prevents drift.
  - [x] Decide whether to rename existing Server/UI `ContainerRepository` values from `folders` / `folders-ui` to full image names. If `folders` remains the server image repository, document why it intentionally equals the Dapr app ID.
  - [x] Add deterministic, non-secret OCI labels through MSBuild container properties or a shared target. Include source/revision/version/service labels without real tenant, credential, endpoint, token, or provider payload values.
  - [x] Do not add `Microsoft.NET.Build.Containers`; .NET SDK container publishing is built into modern SDKs used by this repo.

- [x] Add or update a focused container build script/gate (AC: 1, 3, 4, 8)
  - [x] Add a hermetic script under `tests/tools/`, suggested `run-container-image-gates.ps1`, following the style of `run-dapr-policy-conformance-gates.ps1`.
  - [x] Publish the three service projects in Release for Linux x64 with SDK container targets. Prefer `ContainerArchiveOutputPath` for CI/sandbox validation so the gate does not require a registry push.
  - [x] Emit a metadata-only JSON report under `_bmad-output/gates/container-images/latest.json` with service name, project path, repository name, tags/labels asserted, and command exit code. Do not include absolute local paths, registry credentials, tokens, image layer contents, or environment dumps.
  - [x] Keep this gate optional or focused for Story 7.3. Story 7.4 owns consolidation into baseline PR CI.

- [x] Align local Aspire Dapr app IDs and sidecar topology (AC: 5, 7)
  - [x] Inspect `src/Hexalith.Folders.Aspire/FoldersAspireModule.cs`; Server and Workers already use `WithDaprSidecar`, but UI currently only references Server and exposes HTTP endpoints.
  - [x] If production remains sidecar-enabled for UI, add a `foldersUi.WithDaprSidecar(...)` configuration with `AppId = FoldersAspireModule.FoldersUiAppId` and `Config = daprConfigPath`.
  - [x] Preserve existing dependencies: UI references Server and waits for Server; Server and Workers reference/wait for EventStore and Tenants; shared `statestore` and `pubsub` component names stay `statestore` and `pubsub`.
  - [x] Update `tests/Hexalith.Folders.IntegrationTests/AspireTopologyTests.cs` to prove all expected Dapr sidecar app IDs are present in local topology, not just exposed as constants.

- [x] Strengthen production deployment evidence for image-plus-sidecar binding (AC: 2, 5, 6, 8)
  - [x] Update `deploy/dapr/production/sidecar-config-bindings.yaml` or add a narrow companion artifact such as `deploy/containers/production/service-images.yaml` to bind each service deployment to its stable image repository and Dapr app ID.
  - [x] Keep `dapr.io/app-id` values exact: `folders`, `folders-workers`, `folders-ui`. Do not derive app IDs from image repositories, Kubernetes deployment names, GitHub Actions job names, or branch names.
  - [x] Preserve existing `dapr.io/config` values: `hexalith-folders-production-accesscontrol-folders`, `hexalith-folders-production-accesscontrol-folders-workers`, and `hexalith-folders-production-accesscontrol-folders-ui`.
  - [x] Do not add real registry names, production namespaces beyond sanitized placeholders already in production Dapr artifacts, image digests from private registries, Kubernetes Secret manifests, pull secrets, or live cluster references.

- [x] Add conformance tests for container and deployment contracts (AC: all)
  - [x] Add static tests, suggested under `tests/Hexalith.Folders.Contracts.Tests/OpenApi/` or a new `Deployment` folder, that parse service `.csproj` XML semantically and assert expected container repositories, labels, and publishability.
  - [x] Extend Dapr policy/deployment tests or add a dedicated test to parse production deployment YAML and assert image repository to Dapr app ID mapping.
  - [x] Add tests that fail on secret-shaped material in container/deployment artifacts: `ghp_`, `github_pat_`, `client_secret`, `private_key`, `BEGIN .*PRIVATE KEY`, `password=`, `token=`, and production-looking URLs.
  - [x] Add or extend tests to assert no `git submodule update --init --recursive` / `--recursive` setup appears in new scripts, docs, workflows, or deployment artifacts.

- [x] Document operator handoff (AC: 2, 3, 4, 5, 6)
  - [x] Add or update `docs/operations/container-images-and-dapr-app-ids.md`.
  - [x] Document service image names, Dapr app IDs, sidecar config names, expected publish commands, non-secret label keys, and promotion flow from local/archive validation to staging/production registry ownership.
  - [x] Make the naming distinction explicit: image repository names are deployment artifacts; Dapr app IDs are policy identities and must remain stable across environments.

- [x] Verification (AC: all)
  - [x] Run `dotnet restore Hexalith.Folders.slnx`.
  - [x] Run `dotnet build Hexalith.Folders.slnx --no-restore`.
  - [x] Run focused container/deployment conformance tests.
  - [x] Run `tests/tools/run-dapr-policy-conformance-gates.ps1 -SkipRestoreBuild` if PowerShell/VSTest is available; otherwise record the sandbox limitation and run the matching test assembly/filter by the available in-process xUnit path used in Story 7.2.
  - [x] Run SDK container publish checks for Server, Workers, and UI using archive output or local OCI daemon.
  - [x] Run `rg -n "git submodule update --init --recursive|--recursive" .github tests docs deploy src` and confirm new work did not introduce recursive submodule setup.
  - [x] Run a secret-shaped scan across touched deployment/container artifacts and report exact commands and limitations in the Dev Agent Record.

## Dev Notes

### Critical Scope Boundaries

- This story is release-readiness packaging and deployment evidence, not runtime feature work. Do not add REST/SDK/CLI/MCP operations, UI features, provider capabilities, new auth semantics, or new folder/workspace lifecycle behavior.
- Prefer .NET SDK container publishing over Dockerfiles. A Dockerfile is acceptable only if the SDK path cannot meet a required image contract and the reason is documented in the story completion notes.
- Do not push to a live registry, use production credentials, create Kubernetes Secrets, publish package artifacts, upload images, or call live Dapr/Kubernetes/registry endpoints from PR gates.
- Do not weaken Story 7.1 Dapr deny-by-default policy or Story 7.2 reference-only secret-store contract.
- Do not initialize nested submodules or add recursive submodule commands.

### Current State To Preserve

- `src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj` currently has `<IsPublishable>true</IsPublishable>`, `<EnableContainer>true</EnableContainer>`, and `<ContainerRepository>folders</ContainerRepository>`.
- `src/Hexalith.Folders.UI/Hexalith.Folders.UI.csproj` currently has `<IsPublishable>true</IsPublishable>`, `<EnableContainer>true</EnableContainer>`, and `<ContainerRepository>folders-ui</ContainerRepository>`.
- `src/Hexalith.Folders.Workers/Hexalith.Folders.Workers.csproj` currently has `<OutputType>Exe</OutputType>` and `<IsPackable>false</IsPackable>` but no container repository metadata.
- `src/Hexalith.Folders.Aspire/FoldersAspireModule.cs` defines stable IDs `eventstore`, `tenants`, `folders`, `folders-workers`, `folders-ui`; Server and Workers have Dapr sidecars, UI currently does not.
- `deploy/dapr/production/sidecar-config-bindings.yaml` already contains Dapr annotations for all five app IDs, including `folders-ui`; this must either be matched locally or intentionally revised.
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/DaprPolicyConformanceTests.cs` already validates production access-control, mTLS, pub/sub scopes, secret-store scopes, and sidecar binding app IDs/config names.

### Architecture Compliance

- Architecture I-2 requires one container image per service and Dapr sidecars alongside service containers.
- Architecture I-4 makes Dapr app IDs stable contracts across environments: `eventstore`, `tenants`, `folders`, `folders-ui`, and `folders-workers`.
- Dapr production access control remains deny-by-default with mTLS. Deployment evidence must preserve `dapr.io/config` mapping to the matching production `Configuration` document.
- UI remains a read-only operations console. Containerizing it must not add mutation endpoints, credential reveal, file browsing, raw diffs, repair actions, or unrestricted filesystem access.
- Metadata-only is mandatory for gate reports, labels, docs examples, deployment artifacts, logs, traces, and tests.

### Latest Technical Notes

- Microsoft Learn confirms modern .NET SDK container publishing can create images without a Dockerfile and uses `/t:PublishContainer` or `PublishProfile=DefaultContainer`; for console applications, container support may need explicit SDK container support. Use the repository's `global.json` .NET SDK `10.0.302` as the authority for commands.
- Microsoft Learn documents `ContainerRepository` as the override for image names when the default assembly name is not the desired repository name.
- Dapr docs confirm Kubernetes sidecar injection is driven by `dapr.io/enabled`, and Dapr app identity/config map to `dapr.io/app-id` and `dapr.io/config`. Use these exact annotations in conformance artifacts.

### Previous Story Intelligence

- Story 7.1 created production Dapr artifacts under `deploy/dapr/production/` and conformance tests for stable app IDs, deny-by-default policy, mTLS, sidecar binding, and pub/sub scopes. Reuse those tests/artifacts instead of creating a parallel deployment schema.
- Story 7.2 added Dapr secret-store artifacts and proved `folders` and `folders-workers` secret scopes are reference-only and deny-by-default. Container metadata and deployment manifests must not add new secret surfaces.
- Story 7.2 verification noted VSTest/PowerShell can fail in restricted sandboxes with socket permission errors; if that recurs, record the limitation and use the in-process xUnit runner pattern rather than claiming the gate passed.
- Recent commits show Epic 7 is actively producing release evidence: `4efa637 feat: Update orchestration state for story progression from 7.2 to 7.3`, `f433311 feat(story-7.2): configure production OIDC and secret store integration`, and `0355fd1 feat(story-7.1): deploy production Dapr deny-by-default access control`.

### Project Structure Notes

- Likely UPDATE files:
  - `src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj` - normalize repository/label metadata if needed.
  - `src/Hexalith.Folders.Workers/Hexalith.Folders.Workers.csproj` - add missing container publish/repository metadata.
  - `src/Hexalith.Folders.UI/Hexalith.Folders.UI.csproj` - normalize repository/label metadata if needed.
  - `src/Hexalith.Folders.Aspire/FoldersAspireModule.cs` - add UI Dapr sidecar if production sidecar evidence remains.
  - `tests/Hexalith.Folders.IntegrationTests/AspireTopologyTests.cs` - assert local sidecar topology, not constants only.
  - `tests/Hexalith.Folders.Contracts.Tests/OpenApi/DaprPolicyConformanceTests.cs` - extend or complement deployment conformance assertions.
  - `deploy/dapr/production/sidecar-config-bindings.yaml` - extend only if image-to-app binding belongs in this artifact.
- Likely NEW files:
  - `tests/tools/run-container-image-gates.ps1`
  - `docs/operations/container-images-and-dapr-app-ids.md`
  - Optional `deploy/containers/production/service-images.yaml` if a separate sanitized image manifest is cleaner than expanding Dapr sidecar bindings.
  - Optional focused test file such as `tests/Hexalith.Folders.Contracts.Tests/Deployment/ContainerImageConformanceTests.cs`.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-7.3`] - Story statement and BDD acceptance criteria.
- [Source: `_bmad-output/planning-artifacts/architecture.md#Infrastructure-&-Deployment`] - Container-based production hosting, Dapr sidecars, stable app IDs, CI/CD gates, and observability/health expectations.
- [Source: `_bmad-output/planning-artifacts/architecture.md#Deployment-structure`] - One image per service: `hexalith-folders-server`, `hexalith-folders-workers`, `hexalith-folders-ui`; each deploys with a Dapr sidecar.
- [Source: `_bmad-output/project-context.md#Technology-Stack-&-Versions`] - .NET SDK `10.0.302`, Dapr/Aspire package pins, central package management, and no nested submodule initialization.
- [Source: `_bmad-output/project-context.md#Framework-Specific-Rules`] - Stable Dapr app IDs and read-only operations console constraints.
- [Source: `src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj`] - Existing Server publish/container metadata.
- [Source: `src/Hexalith.Folders.Workers/Hexalith.Folders.Workers.csproj`] - Workers currently lacks container repository metadata.
- [Source: `src/Hexalith.Folders.UI/Hexalith.Folders.UI.csproj`] - Existing UI publish/container metadata.
- [Source: `src/Hexalith.Folders.Aspire/FoldersAspireModule.cs`] - Stable Dapr app ID constants and current local sidecar topology.
- [Source: `deploy/dapr/production/sidecar-config-bindings.yaml`] - Production Dapr sidecar annotations and config bindings.
- [Source: `deploy/dapr/production/accesscontrol.yaml`] - Production Dapr access-control configuration names and target app IDs.
- [Source: `tests/Hexalith.Folders.Contracts.Tests/OpenApi/DaprPolicyConformanceTests.cs`] - Existing static YAML conformance pattern to reuse.
- [Source: `_bmad-output/implementation-artifacts/7-2-configure-production-oidc-and-secret-store-integration.md#Previous-Story-Intelligence`] - Prior Epic 7 verification and sandbox limitations.
- [Source: Microsoft Learn, ".NET application publishing overview", checked 2026-05-30] - .NET SDK can publish container images without Dockerfiles and supports `/t:PublishContainer`. https://learn.microsoft.com/dotnet/core/deploying/#container-deployment
- [Source: Microsoft Learn, "Containerize a .NET app reference", checked 2026-05-30] - `ContainerArchiveOutputPath`, `ContainerRegistry`, and local registry/daemon behavior for container publish outputs. https://learn.microsoft.com/dotnet/core/containers/publish-configuration#flags-that-control-the-destination-of-the-generated-image
- [Source: Microsoft Learn, "Containerize a .NET app with dotnet publish", checked 2026-05-30] - `ContainerRepository` overrides the default image name. https://learn.microsoft.com/dotnet/core/containers/sdk-publish#create-net-app
- [Source: Dapr docs, "Dapr sidecar overview", checked 2026-05-30] - Kubernetes sidecar injection watches pods with `dapr.io/enabled`; sidecars run alongside application processes. https://docs.dapr.io/concepts/dapr-services/sidecar/
- [Source: Dapr docs, "Dapr arguments and annotations", checked 2026-05-30] - `dapr.io/app-id` is the app identity, `dapr.io/app-port` tells Dapr the application port, and `dapr.io/config` selects the Dapr `Configuration`. https://docs.dapr.io/reference/arguments-annotations-overview/

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Implementation Plan

- Normalize container metadata across Server, Workers, and UI.
- Align local Aspire sidecar topology with production Dapr sidecar evidence.
- Add metadata-only deployment/container conformance artifacts and tests.
- Add focused container image gate script without live registry, secret, or recursive submodule assumptions.

### Debug Log References

- 2026-05-30: `dotnet restore Hexalith.Folders.slnx` and `dotnet build Hexalith.Folders.slnx --no-restore` initially failed silently under default MSBuild parallelism; reran with `-m:1` and both passed.
- 2026-05-30: `dotnet tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests.dll -noLogo -noColor -namespace Hexalith.Folders.Contracts.Tests.Deployment` passed 5/5.
- 2026-05-30: `dotnet tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests.dll -noLogo -noColor -namespace Hexalith.Folders.Contracts.Tests.OpenApi.DaprPolicyConformance` passed 8/8.
- 2026-05-30: `dotnet tests/Hexalith.Folders.IntegrationTests/bin/Debug/net10.0/Hexalith.Folders.IntegrationTests.dll -noLogo -noColor -class Hexalith.Folders.IntegrationTests.AspireTopologyTests` passed 4/4 after resolving test project paths from the repository root.
- 2026-05-30: `pwsh -NoLogo -NoProfile -File tests/tools/run-dapr-policy-conformance-gates.ps1 -SkipRestoreBuild` was attempted; VSTest aborted with `System.Net.Sockets.SocketException (13): Permission denied`, so the matching Dapr policy tests were run through xUnit v3 in-process.
- 2026-05-30: `pwsh -NoLogo -NoProfile -File tests/tools/run-container-image-gates.ps1` was attempted; SDK container publish reached image creation but failed because the sandbox blocks outbound access to `mcr.microsoft.com` and `api.nuget.org`. The gate emitted `_bmad-output/gates/container-images/latest.json` with metadata-only failed service results.
- 2026-05-30: Broad in-process regression signal: sample, CLI, client, load, and MCP test assemblies passed before known unrelated baseline failures appeared in contract negative-scope tests, integration/server authentication-scheme test hosts, and VSTest socket usage.
- 2026-05-30: `rg -n "git submodule update --init --recursive|--recursive" .github tests docs deploy src` returned only existing guard/test assertions. A focused scan of Story 7.3 container/deployment artifacts returned no recursive submodule setup.
- 2026-05-30: Secret-shaped scan of Story 7.3 container/deployment artifacts returned no matches. Including test source returns expected regex definitions for the forbidden patterns themselves.
- 2026-05-30: `git diff --check` passed.
- 2026-05-30: Re-ran `dotnet restore Hexalith.Folders.slnx -m:1` and `dotnet build Hexalith.Folders.slnx --no-restore -m:1`; both passed.
- 2026-05-30: Re-ran focused Story 7.3 checks through xUnit v3 in-process: deployment conformance passed 5/5, Dapr policy conformance passed 8/8, and Aspire topology passed 4/4.
- 2026-05-30: Re-ran `pwsh -NoLogo -NoProfile -File tests/tools/run-dapr-policy-conformance-gates.ps1 -SkipRestoreBuild`; VSTest still aborted with `System.Net.Sockets.SocketException (13): Permission denied`, while the matching in-process Dapr conformance lane passed.
- 2026-05-30: Re-ran `pwsh -NoLogo -NoProfile -File tests/tools/run-container-image-gates.ps1`; SDK container publish still failed when the sandbox blocked `mcr.microsoft.com` base-image access and `api.nuget.org` runtime-pack access. `_bmad-output/gates/container-images/latest.json` was emitted with metadata-only failed service results.
- 2026-05-30: Re-ran full in-process test assemblies. Story-focused and several non-host lanes passed, but existing broad-suite failures remain in contract negative-scope tests, server/integration authentication-scheme test hosts, missing Playwright browser binaries, worker host socket binding, and unrelated provider/scaffold guard tests.
- 2026-05-30: Re-ran recursive-submodule scan and focused secret-shaped scan for Story 7.3 artifacts. Recursive scan returned only existing guard/test assertions, and focused secret scan returned no matches.
- 2026-05-30: `dotnet restore Hexalith.Folders.slnx -m:1` failed in the restricted sandbox with `NU1900` because NuGet vulnerability audit could not reach `api.nuget.org`; `dotnet restore Hexalith.Folders.slnx -m:1 -p:NuGetAudit=false` passed.
- 2026-05-30: `dotnet build Hexalith.Folders.slnx --no-restore -m:1` passed after the audit-disabled restore.
- 2026-05-30: Re-ran focused Story 7.3 tests after the container gate patch: deployment conformance passed 5/5, Dapr policy conformance passed 8/8, and Aspire topology passed 4/4.
- 2026-05-30: `pwsh -NoLogo -NoProfile -File tests/tools/run-dapr-policy-conformance-gates.ps1 -SkipRestoreBuild` still aborted in VSTest with `System.Net.Sockets.SocketException (13): Permission denied`; matching Dapr policy conformance passed through the in-process xUnit runner.
- 2026-05-30: Patched `tests/tools/run-container-image-gates.ps1` so `-SkipRestoreBuild` no longer appends `--no-restore` to RID-specific SDK container publish commands, and so the offline container gate uses `-p:NuGetAudit=false`.
- 2026-05-30: `pwsh -NoLogo -NoProfile -File tests/tools/run-container-image-gates.ps1 -SkipRestoreBuild` now reaches SDK container publish for Server and Workers and emits all three service results, but still fails in the sandbox on `mcr.microsoft.com` base-image access and NuGet repository-signature/runtime-pack access for UI. `_bmad-output/gates/container-images/latest.json` was emitted with metadata-only failed service results.
- 2026-05-30: `dotnet test Hexalith.Folders.slnx --no-build -m:1` was attempted and every VSTest lane aborted with `System.Net.Sockets.SocketException (13): Permission denied`; focused in-process Story 7.3 tests passed.
- 2026-05-30: Re-ran `rg -n "git submodule update --init --recursive|--recursive" .github tests docs deploy src`; matches were existing guard/test assertions only. Focused Story 7.3 artifact recursive-submodule scan returned no matches.
- 2026-05-30: Re-ran focused secret-shaped scan across Story 7.3 container/deployment artifacts; no matches. `git diff --check` passed.
- 2026-05-31: Resumed deferred Story 7.3 after the rest of Epic 7 completed and reapplied stash `story-7.3-deferred-wip-20260530T1450Z`.
- 2026-05-31: `dotnet restore Hexalith.Folders.slnx -m:1 -p:NuGetAudit=false` passed.
- 2026-05-31: `dotnet build Hexalith.Folders.slnx --no-restore -m:1` passed with 0 warnings and 0 errors.
- 2026-05-31: Focused in-process checks passed: `ContainerImageConformanceTests` 5/5, `DaprPolicyConformance` 8/8, and `AspireTopologyTests` 4/4.
- 2026-05-31: `pwsh -NoLogo -NoProfile -File tests/tools/run-dapr-policy-conformance-gates.ps1 -SkipRestoreBuild` passed 8/8 and regenerated the Dapr policy report.
- 2026-05-31: `pwsh -NoLogo -NoProfile -File tests/tools/run-container-image-gates.ps1 -SkipRestoreBuild` passed and generated local SDK container archives for `hexalith-folders-server`, `hexalith-folders-workers`, and `hexalith-folders-ui`; `_bmad-output/gates/container-images/latest.json` now reports `status: passed` with metadata-only service results.
- 2026-05-31: `dotnet format whitespace` and `dotnet format analyzers` passed for the modified contract and integration test files.
- 2026-05-31: `git diff --check` passed; focused recursive-submodule and secret-shaped scans across Story 7.3 artifacts returned no matches.

### Completion Notes List

- Normalized Server, Workers, and UI container metadata to stable repositories with shared non-secret OCI labels in `Directory.Build.targets`.
- Kept Dapr app IDs distinct from image repositories: `folders`, `folders-workers`, and `folders-ui` remain policy identities while image repositories use `hexalith-folders-*` names.
- Matched local Aspire topology to production sidecar evidence by keeping the UI sidecar enabled with app ID `folders-ui`, and fixed the topology test to resolve project paths correctly from in-process xUnit runs.
- Added sanitized production image-to-Dapr binding evidence and operator handoff documentation for image names, Dapr app IDs, configs, labels, and promotion flow.
- Added focused conformance tests for SDK container metadata, service image bindings, secret-shaped material, recursive submodule setup, Dapr policy bindings, and local sidecar topology.
- Updated the container image gate to write failure reports with empty result sets and to use single-node MSBuild for this repository's sandbox-sensitive build graph.
- Fixed the container gate's `-SkipRestoreBuild` path so SDK container publish can restore RID-specific assets instead of failing early on missing `linux-x64` restore targets, and added a conformance assertion for that behavior.
- Verification is ready for review after the resumed run: the Dapr gate and SDK container archive gate now pass in this environment, and the container report records `status: passed`.

### File List

- `Directory.Build.targets`
- `deploy/containers/production/service-images.yaml`
- `docs/operations/container-images-and-dapr-app-ids.md`
- `src/Hexalith.Folders.Aspire/FoldersAspireModule.cs`
- `src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj`
- `src/Hexalith.Folders.UI/Hexalith.Folders.UI.csproj`
- `src/Hexalith.Folders.Workers/Hexalith.Folders.Workers.csproj`
- `tests/Hexalith.Folders.Contracts.Tests/Deployment/ContainerImageConformanceTests.cs`
- `tests/Hexalith.Folders.IntegrationTests/AspireTopologyTests.cs`
- `tests/tools/run-container-image-gates.ps1`
- `_bmad-output/gates/container-images/latest.json`
- `_bmad-output/implementation-artifacts/7-3-build-container-images-with-stable-dapr-app-ids.md` (story tracking)
- `_bmad-output/implementation-artifacts/tests/7-3-test-summary.md`
- `_bmad-output/implementation-artifacts/tests/test-summary.md` (latest QA summary now points to Story 7.3)

### Change Log

| Date | Change | Author |
| --- | --- | --- |
| 2026-05-30 | Implemented stable SDK container image metadata, local/production Dapr app ID alignment, sanitized service image binding evidence, focused gates/tests, and operator handoff docs for Story 7.3. | Codex |
| 2026-05-30 | Patched container gate failure reporting/single-node MSBuild behavior and fixed Aspire topology test path resolution found during validation. | Codex |
| 2026-05-30 | Patched the container gate skip-restore path to allow RID-specific SDK container publish restore and added test coverage for the offline gate behavior. | Codex |
| 2026-05-31 | Resumed deferred validation, confirmed full build, focused in-process tests, Dapr PowerShell gate, SDK container archive gate, format checks, diff hygiene, recursive-submodule scan, and secret-shaped scan; status -> review. | Codex (parent recovery) |
| 2026-05-31 | Senior review fixed one story-record issue: the verification parent task was still unchecked after all validation subtasks passed. Reconfirmed task checklist, file list, focused gates, and sprint evidence; status -> done. | Senior Developer Review (AI) |

## Senior Developer Review (AI)

**Reviewer:** jpiquot (local fallback after stalled review session) · **Date:** 2026-05-31 · **Outcome:** Approved (minor story-record fix applied)

Empirical review verified the implemented container/Dapr contract against the story acceptance criteria and the actual repository state:

- Server, Workers, and UI projects publish SDK container images with stable repositories `hexalith-folders-server`, `hexalith-folders-workers`, and `hexalith-folders-ui`.
- `Directory.Build.targets` centralizes non-secret OCI labels, `ContainerUser=app`, the .NET 10 Alpine base image, and metadata validation before `PublishContainer`.
- Production evidence maps image repositories to the stable Dapr app IDs `folders`, `folders-workers`, and `folders-ui`, and local Aspire now attaches matching sidecars, including UI.
- Focused conformance coverage is real: `ContainerImageConformanceTests` 5/5, Dapr policy conformance 8/8, and Aspire topology 4/4.
- Both PowerShell gates now pass in this environment: Dapr policy conformance and SDK container archive publish. The container report records `status: passed`.
- File List now covers the tracked story artifacts, including `_bmad-output/gates/container-images/latest.json` and the QA summaries.

Finding fixed:

1. **[LOW story-record] Verification parent task unchecked** - all verification subtasks and validation evidence were complete, but the parent task still showed `[ ]`. Updated it to `[x]` and confirmed no unchecked tasks remain.

No Critical or High findings remain. Re-validated before approval: restore, full build, focused in-process tests, Dapr gate, container image gate, format/analyzers, diff hygiene, recursive-submodule scan, and secret-shaped scan all passed or returned clean.
