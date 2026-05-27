---
discovery:
  generated_at: 2026-05-27T15:14:16+02:00
  story_key: 4-15-validate-lifecycle-replay-and-projection-determinism
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
    - _bmad-output/implementation-artifacts/4-14-emit-metadata-only-audit-and-observability.md
baseline_commit: dee7099
---

# Story 4.15: Validate lifecycle replay and projection determinism

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer,
I want replay and projection determinism tests for the canonical lifecycle,
so that aggregate state and read models can be rebuilt consistently from durable events.

## Acceptance Criteria

1. Given canonical repository-backed lifecycle event streams exist, when the same ordered stream is replayed from `FolderState.Empty` multiple times, then aggregate state rebuilds to equivalent deterministic state for successful, failed, unknown-outcome, reconciliation-required, duplicate/replayed, lock, file-mutation, commit, cleanup, and audit-observed lifecycle paths.
2. Given in-memory read models and projections rebuild from the same ordered event stream, when projection tests run against empty read models twice, then folder lifecycle, branch/ref policy, workspace lock, workspace status, cleanup status, task status, folder list, access, tenant-access-adjacent evidence, and audit-observation outputs are equivalent after normalizing explicitly nondeterministic freshness fields.
3. Given nondeterministic freshness fields are excluded from determinism assertions, when tests compare rebuilt projections, then only named freshness/watermark/observation-time fields are normalized and any non-freshness drift still fails the assertion.
4. Given duplicate delivery or idempotent replay occurs, when replay and projection tests run, then duplicate domain events do not mutate aggregate state twice, duplicate projection delivery is idempotent or fails loud according to the existing projection contract, and idempotency fingerprints remain stable.
5. Given malformed, foreign-tenant, wrong-folder, out-of-order, missing-apply, or unknown event cases are replayed, when tests execute, then safe failure behavior is deterministic, metadata-only, and cannot silently skip a production event family that should affect state.
6. Given deterministic snapshots, failure diagnostics, assertion messages, and audit observations are produced by these tests, when sentinel scanning runs, then file contents, diffs, raw paths, raw branch/ref names, raw commit messages, repository URLs/names, provider payloads, tokens, credentials, emails, exception stacks, and unauthorized-resource hints are absent.
7. Given Story 4.15 is complete, when validation is performed, then no runtime endpoints, production projection stores, UI pages, CLI commands, MCP tools, SDK convenience helpers, generated-client hand edits, package upgrades, repair automation, live provider calls, broad EventStore integration harness, or nested submodule initialization is introduced.

## Tasks / Subtasks

- [x] Build deterministic lifecycle replay fixtures. (AC: 1, 4, 5, 6)
  - [x] Add focused test support in `tests/Hexalith.Folders.Tests/Aggregates/Folder/` or a nearby `Replay` concept folder; reuse `FolderCommandFactory` and production aggregate handlers rather than constructing unrelated ad hoc state.
  - [x] Cover canonical event families already applied by `FolderStateApply`: `FolderCreated`, `FolderAccessGranted`, `FolderAccessRevoked`, `FolderArchived`, repository binding request/bound/failed/unknown, branch/ref policy, workspace preparation, lifecycle event recorded, lock acquired/released, file mutation accepted, commit succeeded/failed/unknown.
  - [x] Include success, known failure, `unknown_provider_outcome`, `reconciliation_required`, stale/dirty/lock-expired, duplicate idempotency, and cross-tenant/wrong-folder negative streams.
  - [x] Keep fixture values synthetic and metadata-only. Use opaque refs/digests/classifications for paths, commits, branch refs, repository names, providers, and changed paths.

- [x] Add aggregate replay determinism tests. (AC: 1, 3, 4, 5)
  - [x] Replay the same ordered stream from `FolderState.Empty.Apply(events, streamName)` at least twice and assert canonical equality.
  - [x] Verify duplicate event delivery with the same `IdempotencyKey` plus `IdempotencyFingerprint` does not reapply state mutations.
  - [x] Add a coverage guard that fails when a concrete production `IFolderEvent` type exists without an explicit replay expectation in Story 4.15 tests.
  - [x] Assert foreign tenant/folder events still throw the stable safe `TenantMismatch` apply failure and unknown event families fail loud instead of no-oping.

- [x] Add projection/read-model rebuild determinism tests. (AC: 2, 3, 4, 6)
  - [x] Rebuild from empty in-memory models twice using the same ordered events through `InMemoryFolderRepository.Seed(...)` and compare normalized snapshots.
  - [x] Cover `InMemoryFolderLifecycleStatusReadModel`, `InMemoryBranchRefPolicyReadModel`, `InMemoryWorkspaceLockStatusReadModel`, `InMemoryWorkspaceStatusReadModel`, `InMemoryWorkspaceCleanupStatusReadModel`, and `InMemoryTaskStatusReadModel`.
  - [x] Cover existing projection replay surfaces: `FolderListProjection`, `FolderAccessProjection`, and tenant-access handler patterns where relevant.
  - [x] Include `InMemoryFolderAuditObserver` observations only as metadata-only deterministic evidence; do not make audit observations authoritative for aggregate state, ACLs, idempotency, provider outcome, or workspace truth.

- [x] Implement explicit determinism comparison helpers. (AC: 2, 3, 6)
  - [x] Normalize only documented nondeterministic fields such as `FolderLifecycleFreshness.ObservedAt`, projection watermarks generated from observation time, duration evidence, and projection lag fields where the production contract classifies them as freshness evidence.
  - [x] Add a negative test proving the helper fails when a non-freshness field changes, for example workspace state, task ID, provider outcome category, lock holder, commit classification, changed-path digest, sanitized category, or retry eligibility.
  - [x] Prefer local test-only helper methods over production abstractions unless real duplication appears across test files.

- [x] Add metadata-only and sentinel safety assertions. (AC: 5, 6)
  - [x] Serialize replay snapshots, read-model snapshots, normalized comparison payloads, and audit observations touched by this story, then assert the `tests/fixtures/audit-leakage-corpus.json` sentinel values are absent.
  - [x] Keep test failure messages safe: include stable result codes, event type names, and correlation/task references, not raw payload dumps.
  - [x] Do not add new sentinel categories unless this story introduces a genuinely new sensitive pattern; if it does, update the corpus and safety inventory deliberately.

- [x] Validate focused gates. (AC: 1-7)
  - [x] Run focused core build: `dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore --tlp:off -v:minimal /nr:false /m:1`.
  - [x] Run focused test build and xUnit v3 in-process tests for the new replay/determinism test classes under `tests/Hexalith.Folders.Tests`.
  - [x] Run relevant existing replay/projection/safety tests: `FolderCreationProjectionReplayTests`, `FolderAccessProjectionReplayTests`, `FolderRepositoryBindingProjectionReplayTests`, `FolderStateTransitionsTests`, `FolderAuditObservationTests`, and safety invariant tests if fixtures are touched.
  - [x] Run `git diff --check`.

## Dev Notes

### Source Context

- Epic 4 requires deterministic failure, status, idempotency, and redaction behavior for the repository-backed workspace lifecycle. Story 4.15 specifically requires replay/projection tests proving aggregate state and read models rebuild deterministically, while excluding nondeterministic freshness fields from determinism assertions. [Source: `_bmad-output/planning-artifacts/epics.md#Story 4.15: Validate lifecycle replay and projection determinism`]
- PRD NFRs require read-model determinism: rebuilding views from an empty read model must produce equivalent state from the same ordered event stream, excluding explicitly nondeterministic generated values. [Source: `_bmad-output/planning-artifacts/prd.md#Non-Functional Requirements`]
- Architecture testing rules require replay tests for every production event family and projection tests proving ordered event lists build deterministic read models, with duplicate delivery idempotent. [Source: `_bmad-output/planning-artifacts/architecture.md#Process Patterns`]
- Architecture data boundaries make EventStore the authoritative write side, while Folders projections own workspace status, folder list, provider readiness, audit, and tenant-access local read models. Working-copy filesystem and provider state are not authoritative and must not be pulled into these tests. [Source: `_bmad-output/planning-artifacts/architecture.md#Data Boundaries`]
- UX requirements depend on stable diagnostic/timeline/status evidence: later console views must distinguish current state, audit history, stale/unavailable read models, and safe evidence without exposing contents or secrets. Story 4.15 supplies backend proof for that trust. [Source: `_bmad-output/planning-artifacts/ux-design-specification.md#Stable UX Design Requirements`]

### Previous Story Intelligence

- Story 4.14 added the metadata-only audit/telemetry surface in `src/Hexalith.Folders/Observability/`, including `FolderAuditObservation`, `FolderAuditObservationBuilder`, `FolderAuditSanitizer`, `InMemoryFolderAuditObserver`, and low-cardinality telemetry names. Reuse these types for audit determinism evidence; do not add a second audit model.
- Story 4.14 review fixed duplicate successful `/process` observations, audit observer failure propagation, and provider-readiness provider-reference capture. Determinism tests should not reintroduce duplicate audit success semantics or make observer failures affect lifecycle state.
- Story 4.14 validation noted the full solution build may be environment-blocked when the installed SDK does not satisfy `global.json` (`10.0.300`) or when NuGet/Aspire packages are unavailable. Prefer focused core/test validation and report environment blockers exactly if they recur.
- Earlier Epic 4 stories established idempotency keys, correlation IDs, task IDs, C6 state transitions, workspace status, lock, cleanup, commit, canonical errors, and operational evidence. Story 4.15 should verify those existing behaviors, not create new lifecycle semantics.

### Existing Implementation State

- `FolderState.Apply(...)` delegates every event through `FolderStateApply.Apply(...)`, enforces stream tenant/folder identity before state mutation, skips identical idempotency replay, and throws on unhandled event types. This is the primary aggregate replay target.
- `FolderStateTransitions` owns the C6 state/event vocabulary and wire names. Tests should use the production vocabulary instead of hard-coded alternate state machines.
- `InMemoryFolderRepository` materializes aggregate state and saves snapshots into in-memory read models after append/seed. It already clamps freshness timestamps per folder through `_lastObservedAt`; determinism tests must distinguish deterministic business state from freshness evidence.
- Existing replay tests cover folder list creation, access grant/revoke, archive, repository binding, and some lifecycle snapshots, but coverage is spread across story-specific tests. Story 4.15 should add canonical lifecycle-level coverage rather than only more narrow one-off assertions.
- The read-model snapshot records under `src/Hexalith.Folders/Queries/Folders/` are the current status projection surface: lifecycle, branch/ref policy, workspace lock, workspace status, cleanup status, and task status.
- `FolderAuditObservation.ToString()` intentionally emits only bounded metadata. Snapshot/audit serialization in new tests must preserve this safe shape.

### Architecture Compliance

- Aggregates remain pure and deterministic. Do not add clocks, logging, Activity, Meter, random IDs, provider calls, Dapr calls, filesystem access, or audit observer calls inside `FolderAggregate`, `FolderState`, `FolderStateApply`, or `FolderStateTransitions`.
- Projection determinism belongs in tests and projection/read-model replay helpers, not in runtime endpoint code.
- Event order is authoritative for replay. If a projection supports deterministic tie-breakers for duplicate sequence numbers, assert the documented tie-breaker; otherwise do not sort away the input stream.
- Duplicate delivery must be explicit: idempotent duplicate events either no-op by existing state rules or fail loud according to the projection contract. Do not silently tolerate divergent duplicates.
- Tenant isolation remains mandatory in replay: foreign tenant/folder events must not poison aggregate state, projections, snapshots, audit observations, assertion payloads, or test diagnostics.
- Freshness exclusion must be narrow and named. Do not normalize away state, IDs, task/correlation evidence, result categories, provider outcome, lock status, commit evidence, retry eligibility, or redaction classifications.
- Metadata-only is non-negotiable across test fixtures, serialized snapshots, assertion messages, audit observations, logs, and diagnostics.
- No nested submodule initialization or recursive submodule commands.

### Project Structure Notes

- Expected new tests: `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderLifecycleReplayDeterminismTests.cs` and/or `tests/Hexalith.Folders.Tests/Queries/Folders/WorkspaceLifecycleProjectionDeterminismTests.cs`.
- Acceptable shared test support: a focused helper near existing folder aggregate tests, for example `FolderLifecycleReplayFixture.cs` or private helper methods inside the new test class. Avoid production code unless tests reveal a real bug.
- Existing files likely inspected or reused: `FolderCommandFactory.cs`, `FolderState.cs`, `FolderStateApply.cs`, `FolderStateTransitions.cs`, `InMemoryFolderRepository.cs`, read-model snapshot/read-model files under `src/Hexalith.Folders/Queries/Folders/`, `FolderListProjection.cs`, `FolderAccessProjection.cs`, and observability types under `src/Hexalith.Folders/Observability/`.
- If a real missing `Apply` path or projection drift bug is found, fix the production file that owns the behavior and keep the test in the same story. Do not mask it in comparison helpers.

### Files To Inspect First

- `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs`
- `src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs`
- `src/Hexalith.Folders/Projections/FolderList/FolderListProjection.cs`
- `src/Hexalith.Folders/Projections/FolderAccess/FolderAccessProjection.cs`
- `src/Hexalith.Folders/Queries/Folders/InMemoryFolderLifecycleStatusReadModel.cs`
- `src/Hexalith.Folders/Queries/Folders/InMemoryBranchRefPolicyReadModel.cs`
- `src/Hexalith.Folders/Queries/Folders/InMemoryWorkspaceLockStatusReadModel.cs`
- `src/Hexalith.Folders/Queries/Folders/InMemoryWorkspaceStatusReadModel.cs`
- `src/Hexalith.Folders/Queries/Folders/InMemoryWorkspaceCleanupStatusReadModel.cs`
- `src/Hexalith.Folders/Queries/Folders/InMemoryTaskStatusReadModel.cs`
- `src/Hexalith.Folders/Observability/InMemoryFolderAuditObserver.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderCommandFactory.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderCreationProjectionReplayTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderAccessProjectionReplayTests.cs`
- `tests/Hexalith.Folders.Tests/Projections/FolderList/FolderRepositoryBindingProjectionReplayTests.cs`
- `tests/Hexalith.Folders.Tests/Queries/Folders/FolderLifecycleStatusProjectionTests.cs`
- `tests/Hexalith.Folders.Tests/Queries/Folders/WorkspaceLockStatusProjectionTests.cs`
- `tests/Hexalith.Folders.Tests/Observability/FolderAuditObservationTests.cs`
- `tests/fixtures/audit-leakage-corpus.json`

### Do Not Touch

- Do not implement runtime audit query endpoints, ops-console pages, CLI commands, MCP tools, SDK helpers, generated-client files, production projection stores, Dapr state stores, provider adapters, workers, repair/discard/retry automation, release packaging, or alerting.
- Do not add package references, upgrade central package versions, change target frameworks, or modify submodule setup.
- Do not call live GitHub/Forgejo, Dapr sidecars, Keycloak, Redis, EventStore actors, network services, local filesystem working copies, or provider credentials from these tests.
- Do not weaken existing safe-denial, idempotency, C6 transition, metadata-only audit, read-only route, or authorization-order behavior.
- Do not use broad string dumps of event payloads in assertion messages.

### Latest Technical Context

- Local repository pins are authoritative: .NET SDK `10.0.300`, `net10.0`, C# latest, central package management, Dapr `1.17.9`, Aspire `13.x`, Microsoft.Extensions `10.x`, OpenTelemetry `1.15.x`, xUnit v3 `3.2.2`, Shouldly `4.3.0`, and repository-owned package versions. [Source: `global.json`; `Directory.Packages.props`]
- No external web research is required for this story. It is a local test-hardening story over existing aggregate/projection behavior and pinned test frameworks.

### Testing

- Use xUnit v3 and Shouldly. Prefer explicit assertions over reflection-heavy magic, except for the event-family coverage guard.
- Keep tests hermetic and fast under `tests/Hexalith.Folders.Tests`; avoid Aspire, Dapr, TestServer, sockets, provider credentials, network access, or broad solution-level prerequisites.
- Use fixed timestamps through existing fake clocks or `FixedUtcClock` so projection comparisons are repeatable where the test owns the clock.
- Run focused tests through the in-process xUnit v3 runner when VSTest/socket constraints appear in this environment.

### Regression Traps

- Do not treat freshness as business state. `ObservedAt`, projection watermarks, lag samples, and duration evidence can vary; state, IDs, categories, retry eligibility, commit classifications, lock metadata, and redaction classifications must not vary.
- Do not hide real production drift by over-normalizing snapshots.
- Do not add a tolerant catch-all `Apply` path for future events. Unknown production event families must fail loud until deliberately handled and tested.
- Do not construct raw path/branch/commit/repository values in fixtures just because tests are local. The safety corpus scans test diagnostics too.
- Do not make projection rebuild depend on dictionary/enumerator ordering. Use existing deterministic ordering or add explicit ordinal ordering in projection code only if a test exposes real nondeterminism.
- Do not make `InMemoryFolderAuditObserver` or telemetry state authoritative for lifecycle state, ACL checks, provider outcome, or idempotency.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-27: Created via BMAD create-story workflow in YOLO mode with inputs from sprint status, Epic 4 Story 4.15, PRD, architecture, UX specification, root and sibling project contexts, Story 4.14, current aggregate/projection/read-model/audit code, existing replay tests, and recent git history.
- 2026-05-27: Discovery loaded whole planning artifacts from `_bmad-output/planning-artifacts`; no sharded planning docs were present. Persistent facts loaded from root and sibling `project-context.md` files.
- 2026-05-27: Validation checklist applied during drafting; guardrails added for narrow freshness normalization, production `IFolderEvent` coverage, metadata-only fixtures/diagnostics, duplicate delivery/idempotency behavior, aggregate purity, and scope exclusions.
- 2026-05-27: Dev-story implementation added deterministic aggregate replay fixtures, aggregate replay determinism tests, projection/read-model rebuild determinism tests, freshness-only normalization checks, duplicate delivery checks, and sentinel safety assertions.
- 2026-05-27: Attempted `dotnet test tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore --tlp:off -v:minimal --filter "FullyQualifiedName~FolderLifecycleReplayDeterminismTests|FullyQualifiedName~WorkspaceLifecycleProjectionDeterminismTests" /nr:false /m:1`; blocked before MSBuild because `global.json` requires .NET SDK `10.0.300` and only `10.0.108` is installed.
- 2026-05-27: Attempted focused core build and relevant replay/projection/audit test filter; both were blocked by the same .NET SDK `10.0.300` requirement before MSBuild/test discovery.
- 2026-05-27: `git diff --check` passed, including explicit no-index whitespace checks for new untracked test files.
- 2026-05-27: Re-ran focused core build: `dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore --tlp:off -v:minimal /nr:false /m:1`; blocked before MSBuild because `global.json` requires .NET SDK `10.0.300` and only `10.0.108` is installed.
- 2026-05-27: Re-ran focused new determinism test filter: `dotnet test tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore --tlp:off -v:minimal --filter "FullyQualifiedName~FolderLifecycleReplayDeterminismTests|FullyQualifiedName~WorkspaceLifecycleProjectionDeterminismTests" /nr:false /m:1`; blocked by the same SDK resolution failure before test discovery.
- 2026-05-27: Re-ran relevant replay/projection/audit/safety test filter: `dotnet test tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore --tlp:off -v:minimal --filter "FullyQualifiedName~FolderCreationProjectionReplayTests|FullyQualifiedName~FolderAccessProjectionReplayTests|FullyQualifiedName~FolderRepositoryBindingProjectionReplayTests|FullyQualifiedName~FolderStateTransitionsTests|FullyQualifiedName~FolderAuditObservationTests|FullyQualifiedName~Safety" /nr:false /m:1`; blocked by the same SDK resolution failure before test discovery.
- 2026-05-27: Re-ran `git diff --check` and explicit no-index whitespace checks for the three untracked Story 4.15 test files; no whitespace errors were reported.
- 2026-05-27: Re-ran required repo-root focused core build at 2026-05-27T15:52:23+02:00; still blocked before MSBuild because `global.json` requires .NET SDK `10.0.300` and only `10.0.108` is installed.
- 2026-05-27: Standard repo-root `dotnet test` filters for new and relevant replay/projection/audit tests remain blocked by the same SDK resolution failure. Diagnostic test execution outside repo-root with `DOTNET_CLI_HOME=/tmp/sa-codex-home-fbbf34ce` built `tests/Hexalith.Folders.Tests` successfully under installed SDK `10.0.108`; VSTest was blocked by sandbox socket permission, so xUnit v3 in-process runner was used.
- 2026-05-27: Fixed Story 4.15 aggregate replay expectation for reconciliation-required provider/commit outcomes: production replay keeps workspace lifecycle at `UnknownProviderOutcome` and records reconciliation-required evidence in repository binding or commit metadata.
- 2026-05-27: xUnit v3 in-process Story 4.15 determinism run passed: `dotnet tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests.dll -class Hexalith.Folders.Tests.Aggregates.Folder.FolderLifecycleReplayDeterminismTests -class Hexalith.Folders.Tests.Queries.Folders.WorkspaceLifecycleProjectionDeterminismTests` (20 total, 0 failed).
- 2026-05-27: xUnit v3 in-process relevant replay/projection/audit run passed: `dotnet tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests.dll -class Hexalith.Folders.Tests.Aggregates.Folder.FolderCreationProjectionReplayTests -class Hexalith.Folders.Tests.Aggregates.Folder.FolderAccessProjectionReplayTests -class Hexalith.Folders.Tests.Projections.FolderList.FolderRepositoryBindingProjectionReplayTests -class Hexalith.Folders.Tests.Aggregates.Folder.FolderStateTransitionsTests -class Hexalith.Folders.Tests.Observability.FolderAuditObservationTests` (93 total, 0 failed).
- 2026-05-27: `git diff --check` passed after the reconciliation expectation fix; explicit `git diff --no-index --check` whitespace checks for the untracked story/test files also passed.
- 2026-05-27: Parent validation with `DOTNET_ROOT=/home/administrator/.dotnet` passed the required repo-root focused core build, focused test project build, Story 4.15 xUnit v3 in-process tests (20 total, 0 failed), relevant replay/projection/audit xUnit v3 in-process tests (93 total, 0 failed), and `git diff --check`.
- 2026-05-27: Automation added tenant-access projection evidence to the read-model determinism snapshot and `_bmad-output/implementation-artifacts/tests/test-summary.md`; parent revalidation passed focused core/test builds, Story 4.15 xUnit v3 in-process tests (20 total, 0 failed), relevant replay/projection/audit xUnit v3 in-process tests (93 total, 0 failed), and `git diff --check`.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Story file prepared for dev-story execution.
- Story status set to ready-for-dev.
- Added Story 4.15 test-only lifecycle replay fixture and determinism tests for aggregate replay, read-model projection rebuild, narrow freshness normalization, duplicate/idempotent delivery, out-of-order projection failure, and metadata-only sentinel safety.
- Parent validation completed with the pinned SDK path exported; focused core/test builds and xUnit v3 in-process validation pass.
- Corrected reconciliation-required replay assertions to match the production contract: workspace lifecycle remains `UnknownProviderOutcome`, while repository binding and commit metadata carry reconciliation-required evidence.
- Diagnostic child runs could not see SDK `10.0.300`; parent validation used `DOTNET_ROOT=/home/administrator/.dotnet` and passed the required focused gates.
- Automation added tenant-access-adjacent projection evidence to the deterministic projection snapshot without adding runtime endpoints or production projection stores.
- Senior review auto-fixed atomic duplicate-seed failure behavior, narrowed freshness normalization so tenant-access projection watermarks remain assertion-significant, and added explicit cleanup-status determinism coverage for committed, failed, and dirty workspace states.

### Senior Developer Review (AI)

Reviewer: Jerome on 2026-05-27

Outcome: Approved after auto-fixes. Story status was reviewable, Story 4.15 IDs were resolved from the file name, project context and architecture rules were loaded, and no separate Story Context or Epic Tech Spec artifact was present beyond the story/planning artifacts. No external MCP/web documentation was required because this story is local aggregate/projection test hardening over pinned repository APIs.

Findings fixed:

- [HIGH] `InMemoryFolderRepository.Seed(...)` failed loud on duplicate idempotency keys only after assigning the seeded state, leaving partial materialized state after a rejected duplicate-delivery seed. Fixed by validating the seed ledger batch before mutating state or read models, and added an assertion that duplicate seed failure leaves the stream empty.
- [HIGH] The projection determinism normalizer masked any `projectionWatermark` property, including tenant-access projection evidence that is deterministic business state rather than freshness. Fixed by normalizing projection watermarks only under `Freshness`, and added a negative drift test for tenant-access `ProjectionWatermark`.
- [MEDIUM] Cleanup-status determinism was only indirectly covered through the final ready/status-only successful lifecycle. Added deterministic cleanup-status assertions for committed, failed, and dirty workspace lifecycle states.

Git/story discrepancy notes:

- The working tree also contained pre-existing submodule pointer/dirty changes for `Hexalith.Builds`, `Hexalith.EventStore`, and `Hexalith.Memories`, plus `_bmad-output/story-automator/orchestration-3-20260526-203745.md`. These are outside the story source review surface and were not modified.
- Story File List was updated for the production fix in `src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs`.

Validation after fixes:

- `DOTNET_ROOT=/home/administrator/.dotnet DOTNET_CLI_HOME=/tmp/sa-codex-home-fbbf34ce DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 /home/administrator/.dotnet/dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore --tlp:off -v:minimal /nr:false /m:1` passed with 0 warnings and 0 errors.
- `DOTNET_ROOT=/home/administrator/.dotnet DOTNET_CLI_HOME=/tmp/sa-codex-home-fbbf34ce DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 /home/administrator/.dotnet/dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore --tlp:off -v:minimal /nr:false /m:1` passed with 0 warnings and 0 errors.
- Story 4.15 xUnit v3 in-process run passed: 25 total, 0 failed.
- Relevant replay/projection/audit xUnit v3 in-process run passed: 93 total, 0 failed.
- `git diff --check` passed; explicit no-index whitespace checks for Story 4.15 untracked test files produced no whitespace errors.

### Change Log

- 2026-05-27: Added aggregate and projection determinism tests plus shared lifecycle replay fixture for Story 4.15.
- 2026-05-27: Recorded validation blocker for missing .NET SDK `10.0.300`; kept story in-progress rather than marking ready for review.
- 2026-05-27: Fixed reconciliation-required replay expectations and recorded passing diagnostic xUnit v3 in-process validation; kept story in-progress because the pinned repo-root SDK build remains blocked.
- 2026-05-27: Parent validation passed with the pinned SDK path exported; story status moved to review.
- 2026-05-27: Automation added tenant-access projection coverage and test summary; parent revalidation passed.
- 2026-05-27: Senior review auto-fixed duplicate seed atomicity, freshness normalization scope, and cleanup-status determinism coverage; story status moved to done.

### File List

- `_bmad-output/implementation-artifacts/4-15-validate-lifecycle-replay-and-projection-determinism.md`
- `_bmad-output/implementation-artifacts/tests/test-summary.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderLifecycleReplayFixture.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderLifecycleReplayDeterminismTests.cs`
- `tests/Hexalith.Folders.Tests/Queries/Folders/WorkspaceLifecycleProjectionDeterminismTests.cs`
