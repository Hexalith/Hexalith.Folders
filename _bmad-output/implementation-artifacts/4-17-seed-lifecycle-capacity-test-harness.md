---
discovery:
  generated_at: 2026-05-27T17:34:00+02:00
  story_key: 4-17-seed-lifecycle-capacity-test-harness
  loaded_inputs:
    - _bmad-output/planning-artifacts/epics.md
    - _bmad-output/planning-artifacts/prd.md
    - _bmad-output/planning-artifacts/architecture.md
    - _bmad-output/planning-artifacts/ux-design-specification.md
    - _bmad-output/project-context.md
    - Hexalith.Commons/_bmad-output/project-context.md
    - Hexalith.EventStore/_bmad-output/project-context.md
    - Hexalith.Tenants/_bmad-output/project-context.md
    - Hexalith.Memories/_bmad-output/project-context.md
    - Hexalith.FrontComposer/_bmad-output/project-context.md
    - _bmad-output/implementation-artifacts/4-16-validate-lifecycle-security-boundaries.md
baseline_commit: eb76922
---

# Story 4.17: Seed lifecycle capacity test harness

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer,
I want the NBomber lifecycle capacity harness seeded with prepare, lock, mutate, and commit scenarios,
so that lifecycle scenarios capture capacity dimensions early and provide reusable evidence for release calibration.

## Acceptance Criteria

1. Given `tests/load/README.md` currently reserves the capacity lane, when this story is implemented, then a runnable `tests/load` .NET load-test project is added, included in `Hexalith.Folders.slnx`, uses central package management, and builds without provider credentials, tenant seed data, Aspire, Dapr, Redis, GitHub, Forgejo, network services, or nested submodule initialization.
2. Given canonical lifecycle services exist, when the harness runs in quick/local mode, then NBomber scenarios exercise workspace prepare, lock acquisition, file mutation, and commit using production lifecycle services, command/result types, validators, idempotency behavior, state transitions, and metadata-only events; harness-local fakes may provide repository/readiness/content/commit seams but must not reimplement the lifecycle state machine.
3. Given capacity thresholds are not yet approved, when the harness runs, then it uses parameterized non-production profiles and produces evidence without final p95, throughput, C1, C2, or C5 pass/fail thresholds.
4. Given release calibration needs multi-dimensional evidence, when scenario output is emitted, then the harness records tenant count, folders per tenant, workspaces per tenant, tasks per workspace, operations per task, scenario profile, load simulation settings, run ID, git commit, target framework, NBomber version, and result artifact paths.
5. Given lifecycle safety invariants from Stories 4.15 and 4.16, when the harness scenarios run concurrently, then synthetic tenant/folder/workspace/task/operation identifiers remain tenant-scoped, idempotency keys are unique per mutating operation unless an explicit replay profile is selected, and no raw file contents, diffs, provider payloads, tokens, credentials, local absolute paths, or unauthorized resource hints are written to logs, reports, evidence files, or failure messages.
6. Given Story 7.7 and Story 7.10 consume this harness later, when this story is complete, then README/configuration notes explain how to run quick mode locally, how future CI capacity-smoke can select the scenario/profile, and which threshold fields remain intentionally reference-pending for release calibration.
7. Given this story only seeds the harness, when validation is performed, then no runtime REST endpoints, provider adapters, workers, UI pages, CLI/MCP commands, SDK hand edits, generated client files, production Dapr components, production secret stores, final capacity thresholds, GitHub Actions capacity-smoke gates, or release-calibration artifacts are introduced.

## Tasks / Subtasks

- [x] Add the load-test project scaffold under `tests/load`. (AC: 1, 6, 7)
  - [x] Add `tests/load/Hexalith.Folders.LoadTests.csproj` targeting `net10.0`, with `<IsPackable>false</IsPackable>` and no inline package versions.
  - [x] Add the project to `Hexalith.Folders.slnx` under the `/tests/` or `/tests/load/` grouping.
  - [x] Add `NBomber` to `Directory.Packages.props` under the Testing group; use central package management only.
  - [x] Reference only the minimum local projects needed, expected candidates being `src/Hexalith.Folders/Hexalith.Folders.csproj` and `src/Hexalith.Folders.Testing/Hexalith.Folders.Testing.csproj`.
  - [x] Update `tests/load/README.md` from placeholder-only to runnable-harness guidance while preserving the safety language about no provider credentials, sidecars, tenant seed data, network services, or nested submodules.

- [x] Implement the hermetic lifecycle capacity driver. (AC: 2, 5, 7)
  - [x] Create scenario code under `tests/load/Scenarios/`, for example `LifecycleCapacityScenario.cs`, `LifecycleCapacityProfile.cs`, and `LifecycleCapacityEvidenceWriter.cs`.
  - [x] Reuse `WorkspacePreparationService`, `WorkspaceLockAcquisitionService`, `WorkspaceFileMutationService`, `WorkspaceCommitService`, `FolderAggregate`, `FolderCommandValidator`, `WorkspacePathPolicyValidator`, and production request/result types.
  - [x] Provide harness-local or `Hexalith.Folders.Testing` fakes for `IFolderRepository`, readiness validators, path evidence, content staging, delete staging if needed, commit execution, authorization evidence, and fixed time. These fakes may record counts and durable keys; they must delegate lifecycle decisions to production code.
  - [x] Seed repository-backed, branch-policy-configured folder state per tenant/folder/workspace before each full lifecycle iteration or profile partition. Avoid shared mutable state that makes cross-tenant interference invisible.
  - [x] Generate synthetic IDs with ordinal-stable, metadata-only formats such as `tenant-0001`, `folder-0001`, `workspace-0001`, `task-0001`, `operation-0001`, `correlation-0001`, and `idempotency-0001`.

- [x] Define NBomber scenarios and profiles. (AC: 2, 3, 4)
  - [x] Add at least one full-lifecycle scenario whose measured steps are `prepare_workspace`, `acquire_workspace_lock`, `mutate_workspace_file`, and `commit_workspace`.
  - [x] Add narrow step scenarios or profile switches if useful, but keep the canonical full lifecycle as the primary reusable scenario.
  - [x] Provide a quick/local profile with tiny counts and short duration so developers can verify the harness without treating results as capacity claims.
  - [x] Keep final thresholds absent. If NBomber threshold APIs are used for sanity checks, they must be clearly non-production and must not claim C1, C2, or C5 compliance.
  - [x] Ensure failed lifecycle results return failed NBomber responses with safe status codes/categories, not raw serialized command or event payloads.

- [x] Emit reusable capacity evidence. (AC: 3, 4, 5, 6)
  - [x] Write a structured evidence sidecar such as `tests/load/reports/lifecycle-capacity-evidence.json` or a caller-provided report folder output.
  - [x] Include run metadata: run ID, UTC timestamp, git commit when available, target framework, NBomber package version, scenario names, profile name, dimensions, load simulations, report paths, and explicit `thresholds: reference_pending`.
  - [x] Include scenario dimensions for tenant/folder/workspace/task/operation concurrency and operation counts. Do not include file contents, diffs, provider payloads, tokens, credential material, raw unsafe paths, local absolute paths, or unauthorized resource hints.
  - [x] Keep generated report folders ignored or documented as local artifacts unless a small deterministic sample is intentionally committed.

- [x] Add focused harness tests or self-checks. (AC: 1-7)
  - [x] Add lightweight tests or a quick-mode command path proving scenario/profile parsing, evidence-shape generation, safe identifier formatting, and threshold-reference-pending behavior.
  - [x] Assert no recursive submodule command appears in new load docs/scripts.
  - [x] Assert quick mode can run without Dapr sidecars, Aspire, provider credentials, Docker/Testcontainers, network calls, or production secrets.
  - [x] Prefer xUnit tests if reusable assertions are valuable; otherwise make the quick NBomber run itself the focused validation command.

- [x] Validate the focused build and harness. (AC: 1-7)
  - [x] Run `DOTNET_ROOT=/home/administrator/.dotnet DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 PATH=/home/administrator/.dotnet:$PATH dotnet build tests/load/Hexalith.Folders.LoadTests.csproj --no-restore --tlp:off -v:minimal /nr:false /m:1`.
  - [x] Run the quick/local harness command documented in `tests/load/README.md` with a temporary report folder.
  - [x] Run any focused load-harness unit tests added by this story.
  - [x] Run `DOTNET_ROOT=/home/administrator/.dotnet DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 PATH=/home/administrator/.dotnet:$PATH dotnet build Hexalith.Folders.slnx --no-restore --tlp:off -v:minimal /nr:false /m:1` if the solution file is edited.
  - [x] Run `git diff --check`.

## Dev Notes

### Source Context

- Epic 4 requires the repository-backed lifecycle to support prepare, lock, mutate, commit, deterministic status/failure/idempotency/redaction behavior, and early capacity dimensions. Story 4.17 specifically seeds the NBomber harness for prepare, lock, mutate, and commit without production thresholds. [Source: `_bmad-output/planning-artifacts/epics.md#Story 4.17: Seed lifecycle capacity test harness`]
- The PRD performance/scalability targets remain bounded but not fully calibrated: command submission p95 is 1 second, status/audit p95 is 500 ms, context query p95 is 2 seconds, and MVP capacity must avoid assuming one tenant, one repository, or one active workspace. These are not Story 4.17 thresholds; they are context for evidence shape. [Source: `_bmad-output/planning-artifacts/prd.md#Performance and Query Bounds`; `_bmad-output/planning-artifacts/prd.md#Scalability and Capacity`]
- C1 and C5 are release-readiness capacity criteria; architecture assigns the measurement tool to an NBomber harness in `tests/load/`, with target hardware and final numbers pinned later. [Source: `_bmad-output/planning-artifacts/architecture.md#Exit Criteria Operations Plan`]
- The architecture source tree explicitly reserves `tests/load/Hexalith.Folders.LoadTests.csproj` and `tests/load/Scenarios/` for workspace prepare to lock to mutate to commit capacity scenarios. [Source: `_bmad-output/planning-artifacts/architecture.md#Unified Project Structure`]
- `docs/exit-criteria/c0-c13-governance-evidence.yaml` currently marks C1 and C5 as `reference_pending` and points to `tests/load/README.md`; this story should improve the harness evidence but not mark release capacity criteria approved. [Source: `docs/exit-criteria/c0-c13-governance-evidence.yaml#C1`; `docs/exit-criteria/c0-c13-governance-evidence.yaml#C5`]
- Story 7.7 later wires a lightweight capacity-smoke PR CI gate, and Story 7.10 later calibrates C1/C2/C5 targets. Story 4.17 must leave clear handoff points for both without doing their work. [Source: `_bmad-output/planning-artifacts/epics.md#Story 7.7: Add capacity-smoke CI gate`; `_bmad-output/planning-artifacts/epics.md#Story 7.10: Calibrate capacity tests and pin C1/C2/C5 targets`]

### Previous Story Intelligence

- Story 4.16 completed lifecycle security boundary coverage and established that lifecycle tests must stay hermetic, metadata-only, tenant-scoped, and free of runtime endpoints, provider adapters, Dapr sidecars, Keycloak/Redis/Testcontainers, package upgrades beyond story scope, generated-client hand edits, repair automation, and nested submodule initialization.
- Story 4.16 added direct cross-tenant lock acquisition, lock release, and commit isolation coverage after review. Story 4.17 scenarios should keep overlapping-looking identifiers possible across tenant partitions so tenant scoping remains measurable.
- Story 4.16 validation showed the pinned SDK path may be required: use `DOTNET_ROOT=/home/administrator/.dotnet` with `/home/administrator/.dotnet/dotnet` to reach SDK `10.0.302`.
- Story 4.15 added `FolderLifecycleReplayFixture` for deterministic lifecycle streams. Use it as a seed/reference pattern, but do not depend on internals from `tests/Hexalith.Folders.Tests` unless helpers are promoted to `src/Hexalith.Folders.Testing`.

### Existing Implementation State

- `tests/load/README.md` is currently a placeholder declaring runnable load infrastructure deferred, synthetic data only, and no provider credentials, tenant seed data, Aspire, Dapr, Redis, GitHub, Forgejo, or nested-submodule prerequisites.
- `Directory.Packages.props` has central package management and no NBomber entry yet. Add a central `PackageVersion Include="NBomber"` entry; do not add inline versions in the load project.
- `Hexalith.Folders.slnx` currently lists test projects but not a load project. If adding `tests/load/Hexalith.Folders.LoadTests.csproj`, update the solution explicitly.
- `WorkspacePreparationService`, `WorkspaceLockAcquisitionService`, `WorkspaceFileMutationService`, and `WorkspaceCommitService` already implement the service-level lifecycle paths that the harness should call. They perform authorization, validation, state loading, idempotency, provider/readiness/content/commit seams, and append behavior.
- `RecordingFolderRepository`, `FolderLifecycleReplayFixture`, and the Story 4.16 service tests show useful fake patterns, but they are in `tests/Hexalith.Folders.Tests` and mostly `internal`. For the load project, either create harness-local fakes or promote reusable general-purpose fakes to `src/Hexalith.Folders.Testing` with focused tests.
- `WorkspaceFileMutationService` stages add/change content through `IWorkspaceFileContentStore` and remove through `IWorkspaceFileDeleteOperationStore`. Harness fakes should record counts/bytes/digests only and never write file contents.
- `WorkspaceCommitService` calls `IWorkspaceCommitReadinessValidator` and `IWorkspaceCommitExecutor`; the harness commit executor should return synthetic commit references such as `commitref_capacity_000001`, not call GitHub, Forgejo, or a local Git working copy.

### Architecture Compliance

- Do not create a second lifecycle state machine. NBomber scenarios are workload drivers around production services and aggregates, not a replacement implementation.
- Keep aggregate purity intact: no clocks, logging, metrics, filesystem, provider calls, Dapr calls, or NBomber references inside aggregate handlers/state/apply code.
- Keep all durable keys tenant-scoped. Scenario data must include tenant in repository/idempotency/cache-like keys and evidence records.
- Maintain authorization-before-observation: denied or malformed scenario operations must not touch repository streams, provider/readiness seams, content stores, commit executors, context sources, or audit resources.
- Use metadata-only evidence. Report dimensions and counts, not raw file contents, diffs, provider payloads, credentials, tokens, commit messages, branch names, local paths, or unauthorized resource existence.
- Treat C1, C2, and C5 as reference-pending. This story creates measurement capability; release calibration remains owned by Epic 7.

### Project Structure Notes

- Expected new or updated files:
  - `Directory.Packages.props`
  - `Hexalith.Folders.slnx`
  - `tests/load/README.md`
  - `tests/load/Hexalith.Folders.LoadTests.csproj`
  - `tests/load/Program.cs`
  - `tests/load/Scenarios/LifecycleCapacityScenario.cs`
  - `tests/load/Scenarios/LifecycleCapacityProfile.cs`
  - `tests/load/Scenarios/LifecycleCapacityEvidenceWriter.cs`
  - Optional: `tests/load/Infrastructure/*` for harness-local fakes and safe ID generators.
  - Optional: `src/Hexalith.Folders.Testing/*` only if reusable fakes are promoted out of existing test internals.
- If a `tests/load/reports/` output folder is used, add an ignore rule or document it as local generated output. Do not commit bulky NBomber HTML/CSV/JSON run output unless a tiny deterministic fixture is intentionally required.

### Files To Inspect First

- `tests/load/README.md`
- `Directory.Packages.props`
- `Hexalith.Folders.slnx`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspacePreparationService.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceLockAcquisitionService.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceFileMutationService.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceCommitService.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspacePathPolicyValidator.cs`
- `src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderLifecycleReplayFixture.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/RecordingFolderRepository.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderWorkspacePreparationServiceTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderWorkspaceLockAcquisitionServiceTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderWorkspaceFileMutationServiceTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderWorkspaceCommitServiceTests.cs`
- `docs/exit-criteria/c0-c13-governance-evidence.yaml`

### Do Not Touch

- Do not implement production REST endpoints, provider adapters, workers, UI pages, CLI/MCP commands, SDK convenience helpers, generated client files, production projection stores, Dapr components, production secret stores, GitHub Actions capacity-smoke gates, release-calibration artifacts, or alerting.
- Do not call live GitHub/Forgejo, Dapr sidecars, Aspire AppHost, Keycloak, Redis, Docker/Testcontainers, EventStore actors, network services, local filesystem working copies, provider credentials, or production secrets from the quick/local harness.
- Do not set final C1, C2, or C5 thresholds, hardware profiles, release pass/fail gates, or product scalability claims in this story.
- Do not weaken existing safe-denial, idempotency, C6 transition, metadata-only audit, read-only route, authorization-order, replay determinism, projection determinism, or safety invariant behavior.
- Do not add recursive submodule initialization or nested submodule update commands.

### Latest Technical Context

- Local repository pins are authoritative: .NET SDK `10.0.302`, `net10.0`, C# latest, central package management, xUnit v3 `3.2.2`, Shouldly `4.3.0`, and repository-owned package versions. [Source: `global.json`; `Directory.Packages.props`; `_bmad-output/project-context.md`]
- NBomber is not currently referenced in this repository. NuGet lists `NBomber` latest stable as `6.4.1`, published shortly before this story was created, and shows computed `net10.0` compatibility. Use this version unless restore/package policy reveals a reason to pin differently. [Source: [NuGet NBomber 6.4.1](https://www.nuget.org/packages/NBomber/)]
- NBomber `Scenario.Create` models a workflow run by virtual users; `Step.Run` measures individual user actions inside a scenario. Use steps for prepare, lock, mutate, and commit so each lifecycle phase has separate measurements. [Source: [NBomber Scenario docs](https://nbomber.com/docs/nbomber/scenario/); [NBomber Step docs](https://nbomber.com/docs/nbomber/step)]
- NBomber supports `WithInit`, `WithClean`, warm-up configuration, `WithoutWarmUp`, `WithLoadSimulations`, and `WithMaxFailCount`. For this story, prefer deterministic init/cleanup, quick local load simulation, and no production thresholds. [Source: [NBomber Scenario docs](https://nbomber.com/docs/nbomber/scenario/)]

### Testing

- Use the pinned SDK path if default `dotnet` resolves the wrong SDK.
- Build the load project and solution with `--no-restore` after restore is available.
- The quick harness run should be tiny, hermetic, and safe for local execution. It must not require sidecars, credentials, Docker, network, or nested submodules.
- If adding xUnit tests for harness helpers, use xUnit v3 and Shouldly, and keep assertions over shape, configuration, safe identifiers, and evidence redaction rather than timing thresholds.
- Run `git diff --check`.

### Regression Traps

- Do not make the harness pass by bypassing production lifecycle services or by directly appending final-state events.
- Do not reuse the same idempotency key across concurrent mutating operations except in a named replay/idempotency profile.
- Do not make tenant/folder/workspace/task IDs globally unique in ways that hide tenant scoping; include overlapping-looking IDs across tenants in at least one profile.
- Do not record raw command/event JSON in reports if it includes sensitive or attacker-controlled fields. Evidence should be dimensions, counts, safe categories, and result codes.
- Do not add NBomber reports to blocking PR CI in this story. Story 7.7 owns the CI capacity-smoke gate.
- Do not treat quick/local results as C1/C2/C5 evidence. They prove harness execution only.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-27: Created via BMAD create-story workflow in YOLO mode with inputs from sprint status, Epic 4 Story 4.17, PRD, architecture, UX specification, root and sibling project contexts, Story 4.16, current lifecycle service/test code, `tests/load/README.md`, package/solution files, recent git history, and NBomber NuGet/docs research.
- 2026-05-27: Discovery loaded whole planning artifacts from `_bmad-output/planning-artifacts`; no sharded planning docs were present. Persistent facts loaded from root and sibling `project-context.md` files.
- 2026-05-27: Validation checklist applied during drafting; guardrails added for avoiding duplicate lifecycle implementations, preserving hermetic synthetic-only execution, keeping C1/C2/C5 reference-pending, recording capacity dimensions, and preventing sensitive data in reports.
- 2026-05-27: Dev implementation added the NBomber load project, full lifecycle scenario, quick profile, metadata-only fakes, evidence writer, self-check mode, central package wiring, solution entry, and README run guidance.
- 2026-05-27: Child dev session stalled after initial restore/build feedback; parent fixed NBomber API usage, analyzer null guards, branch/ref and author metadata validation, then validated the load project and quick harness directly.
- 2026-05-27: Focused load project restore/build passed. Self-check passed. Quick NBomber run passed with 3 full-lifecycle iterations, 0 failures, and evidence `thresholds: reference_pending`.
- 2026-05-27: Automation added focused xUnit v3 harness tests for direct lifecycle driver execution, safe identifier/idempotency shaping, tenant-scoped recorder counts, evidence redaction, and fail-fast profile parsing.
- 2026-05-27: Parent fixed automation feedback by recording explicit prepare and lock operation IDs instead of collapsing them onto the workspace ID; focused test build passed, xUnit v3 in-process tests passed (5 total, 0 failed), self-check passed, and quick NBomber run passed with 3 full-lifecycle iterations, 0 failures, 12 measured operations, and 12 idempotency keys.
- 2026-05-27: Code review fixed deterministic readiness freshness timestamps, stricter safe ID prefixes, local-artifact README commands, generated report ignores, and NBomber log sanitization for report-folder lines. Final focused restore/build/test, self-check, quick NBomber run, generated-log redaction scan, and `git diff --check` passed.
- 2026-05-27: Full `Hexalith.Folders.slnx --no-restore` build remains blocked by existing restored asset fallback folders pointing at `C:\Program Files (x86)\Microsoft Visual Studio\Shared\NuGetPackages`; this affects many existing projects outside the new load harness.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Story file prepared for dev-story execution.
- Story status set to ready-for-dev.
- Added hermetic NBomber lifecycle capacity harness for prepare, lock, mutate, and commit with quick/local profile.
- Added metadata-only evidence sidecar generation with run dimensions, NBomber version, target framework, report paths, observed counts, and `thresholds: reference_pending`.
- Added self-check mode for safe identifiers, unique idempotency keys, reference-pending evidence shape, and no recursive submodule command in load docs.
- Added focused xUnit v3 harness tests for the lifecycle driver, synthetic IDs, tenant-scoped recorder counts, metadata-only evidence, and invalid profile/ordinal handling.
- Added generated report ignore coverage and NBomber log sanitization for report-folder path lines.
- Focused validation passed; full solution build caveat recorded.

### File List

- `_bmad-output/implementation-artifacts/4-17-seed-lifecycle-capacity-test-harness.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/tests/test-summary.md`
- `_bmad-output/story-automator/orchestration-3-20260526-203745.md`
- `.gitignore`
- `Directory.Packages.props`
- `Hexalith.Folders.slnx`
- `tests/Hexalith.Folders.LoadTests.Tests/Hexalith.Folders.LoadTests.Tests.csproj`
- `tests/Hexalith.Folders.LoadTests.Tests/LifecycleCapacityHarnessTests.cs`
- `tests/load/README.md`
- `tests/load/Hexalith.Folders.LoadTests.csproj`
- `tests/load/Program.cs`
- `tests/load/Scenarios/LifecycleCapacityDriver.cs`
- `tests/load/Scenarios/LifecycleCapacityEvidenceWriter.cs`
- `tests/load/Scenarios/LifecycleCapacityFakes.cs`
- `tests/load/Scenarios/LifecycleCapacityIteration.cs`
- `tests/load/Scenarios/LifecycleCapacityProfile.cs`
- `tests/load/Scenarios/LifecycleCapacityRunRecorder.cs`
- `tests/load/Scenarios/LifecycleCapacityScenario.cs`
- `tests/load/Scenarios/SafeOrdinalId.cs`
