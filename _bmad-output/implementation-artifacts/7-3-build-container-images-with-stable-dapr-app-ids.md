---
baseline_commit: 4efa6372b1ca1fa8ce8201620b349da5274df323
---

# Story 7.3: Build container images with stable Dapr app IDs

Status: in-progress

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

## Tasks / Subtasks

- [ ] Normalize container publishing metadata for all service hosts (AC: 1, 2, 3, 4)
  - [ ] Review `src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj`, `src/Hexalith.Folders.Workers/Hexalith.Folders.Workers.csproj`, and `src/Hexalith.Folders.UI/Hexalith.Folders.UI.csproj`.
  - [ ] Add missing Workers container settings. At minimum set a stable repository such as `hexalith-folders-workers`; use central/shared MSBuild properties if that prevents drift.
  - [ ] Decide whether to rename existing Server/UI `ContainerRepository` values from `folders` / `folders-ui` to full image names. If `folders` remains the server image repository, document why it intentionally equals the Dapr app ID.
  - [ ] Add deterministic, non-secret OCI labels through MSBuild container properties or a shared target. Include source/revision/version/service labels without real tenant, credential, endpoint, token, or provider payload values.
  - [ ] Do not add `Microsoft.NET.Build.Containers`; .NET SDK container publishing is built into modern SDKs used by this repo.

- [ ] Add or update a focused container build script/gate (AC: 1, 3, 4, 8)
  - [ ] Add a hermetic script under `tests/tools/`, suggested `run-container-image-gates.ps1`, following the style of `run-dapr-policy-conformance-gates.ps1`.
  - [ ] Publish the three service projects in Release for Linux x64 with SDK container targets. Prefer `ContainerArchiveOutputPath` for CI/sandbox validation so the gate does not require a registry push.
  - [ ] Emit a metadata-only JSON report under `_bmad-output/gates/container-images/latest.json` with service name, project path, repository name, tags/labels asserted, and command exit code. Do not include absolute local paths, registry credentials, tokens, image layer contents, or environment dumps.
  - [ ] Keep this gate optional or focused for Story 7.3. Story 7.4 owns consolidation into baseline PR CI.

- [ ] Align local Aspire Dapr app IDs and sidecar topology (AC: 5, 7)
  - [ ] Inspect `src/Hexalith.Folders.Aspire/FoldersAspireModule.cs`; Server and Workers already use `WithDaprSidecar`, but UI currently only references Server and exposes HTTP endpoints.
  - [ ] If production remains sidecar-enabled for UI, add a `foldersUi.WithDaprSidecar(...)` configuration with `AppId = FoldersAspireModule.FoldersUiAppId` and `Config = daprConfigPath`.
  - [ ] Preserve existing dependencies: UI references Server and waits for Server; Server and Workers reference/wait for EventStore and Tenants; shared `statestore` and `pubsub` component names stay `statestore` and `pubsub`.
  - [ ] Update `tests/Hexalith.Folders.IntegrationTests/AspireTopologyTests.cs` to prove all expected Dapr sidecar app IDs are present in local topology, not just exposed as constants.

- [ ] Strengthen production deployment evidence for image-plus-sidecar binding (AC: 2, 5, 6, 8)
  - [ ] Update `deploy/dapr/production/sidecar-config-bindings.yaml` or add a narrow companion artifact such as `deploy/containers/production/service-images.yaml` to bind each service deployment to its stable image repository and Dapr app ID.
  - [ ] Keep `dapr.io/app-id` values exact: `folders`, `folders-workers`, `folders-ui`. Do not derive app IDs from image repositories, Kubernetes deployment names, GitHub Actions job names, or branch names.
  - [ ] Preserve existing `dapr.io/config` values: `hexalith-folders-production-accesscontrol-folders`, `hexalith-folders-production-accesscontrol-folders-workers`, and `hexalith-folders-production-accesscontrol-folders-ui`.
  - [ ] Do not add real registry names, production namespaces beyond sanitized placeholders already in production Dapr artifacts, image digests from private registries, Kubernetes Secret manifests, pull secrets, or live cluster references.

- [ ] Add conformance tests for container and deployment contracts (AC: all)
  - [ ] Add static tests, suggested under `tests/Hexalith.Folders.Contracts.Tests/OpenApi/` or a new `Deployment` folder, that parse service `.csproj` XML semantically and assert expected container repositories, labels, and publishability.
  - [ ] Extend Dapr policy/deployment tests or add a dedicated test to parse production deployment YAML and assert image repository to Dapr app ID mapping.
  - [ ] Add tests that fail on secret-shaped material in container/deployment artifacts: `ghp_`, `github_pat_`, `client_secret`, `private_key`, `BEGIN .*PRIVATE KEY`, `password=`, `token=`, and production-looking URLs.
  - [ ] Add or extend tests to assert no `git submodule update --init --recursive` / `--recursive` setup appears in new scripts, docs, workflows, or deployment artifacts.

- [ ] Document operator handoff (AC: 2, 3, 4, 5, 6)
  - [ ] Add or update `docs/operations/container-images-and-dapr-app-ids.md`.
  - [ ] Document service image names, Dapr app IDs, sidecar config names, expected publish commands, non-secret label keys, and promotion flow from local/archive validation to staging/production registry ownership.
  - [ ] Make the naming distinction explicit: image repository names are deployment artifacts; Dapr app IDs are policy identities and must remain stable across environments.

- [ ] Verification (AC: all)
  - [ ] Run `dotnet restore Hexalith.Folders.slnx`.
  - [ ] Run `dotnet build Hexalith.Folders.slnx --no-restore`.
  - [ ] Run focused container/deployment conformance tests.
  - [ ] Run `tests/tools/run-dapr-policy-conformance-gates.ps1 -SkipRestoreBuild` if PowerShell/VSTest is available; otherwise record the sandbox limitation and run the matching test assembly/filter by the available in-process xUnit path used in Story 7.2.
  - [ ] Run SDK container publish checks for Server, Workers, and UI using archive output or local OCI daemon.
  - [ ] Run `rg -n "git submodule update --init --recursive|--recursive" .github tests docs deploy src` and confirm new work did not introduce recursive submodule setup.
  - [ ] Run a secret-shaped scan across touched deployment/container artifacts and report exact commands and limitations in the Dev Agent Record.

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

- Microsoft Learn confirms modern .NET SDK container publishing can create images without a Dockerfile and uses `/t:PublishContainer` or `PublishProfile=DefaultContainer`; for console applications, container support may need explicit SDK container support. Use the repository's `global.json` .NET SDK `10.0.300` as the authority for commands.
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
- [Source: `_bmad-output/project-context.md#Technology-Stack-&-Versions`] - .NET SDK `10.0.300`, Dapr/Aspire package pins, central package management, and no nested submodule initialization.
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

### Completion Notes List

### File List
