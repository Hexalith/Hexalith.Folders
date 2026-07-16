---
discovery:
  generated_at: 2026-05-27T16:18:35+02:00
  story_key: 4-16-validate-lifecycle-security-boundaries
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
    - _bmad-output/implementation-artifacts/4-15-validate-lifecycle-replay-and-projection-determinism.md
baseline_commit: 0aa4e1e
---

# Story 4.16: Validate lifecycle security boundaries

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer,
I want sentinel-redaction, path-security, encoding-equivalence, and cross-tenant isolation tests for the lifecycle,
so that secret safety, path safety, encoding stability, and tenant isolation are checked mechanically.

## Acceptance Criteria

1. Given canonical repository-backed lifecycle commands, events, read models, audit observations, errors, diagnostics, and test failure messages exist, when sentinel-redaction tests serialize every security-relevant lifecycle output touched by this story, then no value from `tests/fixtures/audit-leakage-corpus.json` appears except through allowed provenance such as sample ID, bounded classification, redaction marker, content hash, rule ID, or operation ID where explicitly allowed.
2. Given file mutation and context-query path metadata can contain traversal, absolute paths, mixed separators, percent-encoded dot segments, reserved platform names, empty segments, trailing-space/dot ambiguity, invisible format characters, non-NFC input, and case/encoding ambiguity, when path-security tests run, then unsafe inputs are rejected before provider, content-store, delete-order, context-source, or unauthorized workspace observation, and denial results never echo raw unsafe paths.
3. Given `tests/fixtures/idempotency-encoding-corpus.json` and `tests/fixtures/idempotency-encoding-corpus-consumption.yaml` define normalization, casing, ordering, malformed-key, duplicate-JSON-key, and percent-encoding expectations, when lifecycle idempotency/security tests run, then the existing generated helper and parser-policy tests remain authoritative, parser-rejected cases do not reach lifecycle side effects, and Story 4.16 only adds lifecycle-level assertions that are not already covered by `ClientGenerationTests` or `GovernanceCompletenessGateTests`.
4. Given tenant A and tenant B have overlapping-looking folder, workspace, task, lock, path, repository-binding, branch/ref, operation, correlation, and idempotency identifiers, when prepare, lock, mutate, remove, commit, status, cleanup, task-status, context-query, audit, and projection paths are exercised in parallel or with wrong-tenant inputs, then tenant B cannot read, infer, lock, mutate, commit, audit, or alter tenant A state, and the wrong-tenant attempt short-circuits before protected resource access.
5. Given parallel tenant/task scenarios include lock contention, stale-lock behavior, interrupted/dirty lifecycle attempts, duplicate/replayed operations, unknown provider outcomes, and cross-tenant identifiers, when tests run, then contention and stale/interrupted results are deterministic, retry eligibility is metadata-only, and no losing task or wrong tenant mutates another task or tenant workspace.
6. Given denied operations are produced by authorization, folder ACL, path policy, stale projection, lock ownership, state-transition, idempotency, content-store, delete-order, provider, and read-model failures, when audit and error evidence is emitted or serialized, then the shape is safe, metadata-only, non-enumerating, and includes only stable categories, correlation/task/operation references, retry/client-action metadata, redaction state, and bounded authorization/projection watermarks.
7. Given Story 4.16 is complete, when validation is performed, then no runtime endpoints, provider adapters, live GitHub/Forgejo calls, Dapr sidecars, Keycloak/Redis/Testcontainers dependencies, UI pages, CLI/MCP commands, SDK hand edits, package upgrades, generated-client hand edits, production secret stores, repair automation, broad integration harness, or nested submodule initialization is introduced.

## Tasks / Subtasks

- [x] Extend lifecycle security fixtures without duplicating production behavior. (AC: 1, 4, 5, 6)
  - [x] Add focused test support near `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderLifecycleReplayFixture.cs`, or private helper methods in the new test classes, to build tenant A/tenant B lifecycle streams with overlapping safe identifiers.
  - [x] Reuse production aggregate handlers, `WorkspaceFileMutationService`, `WorkspaceLockAcquisitionService`, read models, and authorization services rather than hand-rolling alternate lifecycle state machines.
  - [x] Keep fixture values synthetic and metadata-only. Do not insert real paths, real repository URLs, raw branch names, raw commit messages, provider payloads, tokens, credentials, emails, file contents, diffs, or generated context payloads into fixtures.

- [x] Add sentinel-redaction lifecycle tests. (AC: 1, 6)
  - [x] Serialize representative lifecycle command results, emitted events, read-model snapshots, audit observations, problem/error evidence, and bounded diagnostics touched by the tests.
  - [x] Iterate `tests/fixtures/audit-leakage-corpus.json` and fail on raw sentinel values for all relevant forbidden surfaces.
  - [x] Assert allowed provenance stays bounded: sample IDs, classifications, redaction markers, hashes, rule IDs, operation IDs, safe categories, and safe watermarks only.
  - [x] Reuse the scanner concepts from `tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs`; do not weaken corpus size/category assertions or inventory scanning.

- [x] Expand path-security coverage for lifecycle file mutations and context queries. (AC: 2, 6)
  - [x] Extend `WorkspacePathPolicyValidatorTests` and service/query tests for traversal, absolute paths, mixed separators, percent-encoded dot segments, reserved names, empty segments, trailing-space/dot ambiguity, invisible format characters, non-NFC paths, non-ASCII ambiguity, and display-name separator attempts.
  - [x] Add lifecycle service assertions proving syntactic path denials occur before stream-name construction, stream load, idempotency lookup, path evidence provider calls, content staging, delete ordering, append, and context-source calls where the current pipeline promises that ordering.
  - [x] Cover evidence-layer denials such as symlink escape and unavailable evidence after state/lock checks but before content/delete side effects.
  - [x] Assert denials return `PathPolicyDenied` or `PolicyEvidenceUnavailable` as appropriate and never expose the raw unsafe path via result, exception, audit, or assertion text.

- [x] Add encoding-equivalence lifecycle guardrails. (AC: 3)
  - [x] Consume existing corpus expectations indirectly through `ClientGenerationTests` and `GovernanceCompletenessGateTests`; do not duplicate the generated hasher contract in lifecycle tests.
  - [x] Add lifecycle parser-boundary assertions for malformed idempotency keys, duplicate-key JSON payloads where routed through existing parser helpers, non-NFC path metadata, zero-width/invisible characters, percent-encoded path separators/dot segments, and casing-sensitive task/path identifiers.
  - [x] Preserve current behavior: default idempotency hashing does not normalize Unicode unless a field declares that policy; parser-rejected inputs must fail before lifecycle side effects.

- [x] Add cross-tenant and cross-task isolation tests. (AC: 4, 5, 6)
  - [x] Prove wrong authoritative tenant, payload tenant mismatch, client-controlled tenant mismatch, wrong principal, unknown tenant, stale tenant projection, disabled tenant, replay conflict, and unavailable projection short-circuit before protected resource access.
  - [x] Exercise prepare, lock acquisition, lock release, file add/change/remove, commit, workspace status, lock status, cleanup status, task status, context query, folder access projection, tenant-access projection, and audit observation where the existing code exposes a hermetic unit-test seam.
  - [x] Build parallel tenant/task cases where tenant A and tenant B reuse the same `folder-a`, `workspace-a`, `task-a`, `operation-a`, and idempotency-key-shaped values under different tenant scopes; assert stream names, ledgers, read-model snapshots, and audit observations stay tenant-scoped.
  - [x] Cover lock contention, lock-not-owned, stale/expired lock, dirty/interrupted lifecycle, duplicate/replayed operations, and unknown-provider/reconciliation outcomes without adding live providers or repair automation.

- [x] Verify safe denial and metadata-only audit evidence. (AC: 1, 6)
  - [x] Add assertions for safe result codes and bounded evidence on authorization, folder ACL, path policy, stale projection, lock ownership, state-transition, idempotency, content-store, delete-order, provider, and read-model failures.
  - [x] Ensure denied audit observations include operation kind, result, tenant/task/operation/correlation references when authorized, redaction state, sanitized category, retry/idempotency flags, and classifications, without raw payloads or unauthorized resource hints.
  - [x] Keep test failure messages safe: include stable result codes, event type names, scenario IDs, hashes, and sample IDs, not serialized raw command/event payloads containing sentinel values.

- [x] Keep scope narrow and hermetic. (AC: 7)
  - [x] Do not add or modify runtime REST endpoints, provider adapters, workers, UI pages, CLI/MCP commands, generated client files, package versions, target frameworks, Dapr components, live provider credentials, or repair/discard/retry automation.
  - [x] Do not initialize or update nested submodules. Existing root-level submodules may be read for patterns only.

- [x] Validate focused gates. (AC: 1-7)
  - [x] Run focused core build: `DOTNET_ROOT=/home/administrator/.dotnet DOTNET_CLI_HOME=/tmp/sa-codex-home-fbbf34ce DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 /home/administrator/.dotnet/dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore --tlp:off -v:minimal /nr:false /m:1`.
  - [x] Run focused test build: `DOTNET_ROOT=/home/administrator/.dotnet DOTNET_CLI_HOME=/tmp/sa-codex-home-fbbf34ce DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 /home/administrator/.dotnet/dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore --tlp:off -v:minimal /nr:false /m:1`.
  - [x] Run focused xUnit v3 tests for the new Story 4.16 test classes through the in-process runner if VSTest socket permissions are blocked.
  - [x] Run relevant existing tests: `WorkspacePathPolicyValidatorTests`, `FolderWorkspaceFileMutationServiceTests`, `FolderWorkspaceLockAcquisitionServiceTests`, `FolderWorkspaceLockReleaseServiceTests`, `FolderWorkspaceCommitServiceTests`, `WorkspaceFileContextQueryHandlerTests`, `TenantAccessAuthorizerTests`, `LayeredFolderAuthorizationServiceTests`, `FolderAuditObservationTests`, `FolderLifecycleReplayDeterminismTests`, `WorkspaceLifecycleProjectionDeterminismTests`, `SafetyInvariantGateTests`, `ClientGenerationTests`, and `GovernanceCompletenessGateTests`.
  - [x] Run `git diff --check`.

## Dev Notes

### Source Context

- Epic 4 requires the repository-backed lifecycle to provide deterministic failure, status, idempotency, redaction, lock, file, commit, cleanup, and audit behavior. Story 4.16 specifically requires sentinel-redaction, path-security, encoding-equivalence, and cross-tenant negative tests. [Source: `_bmad-output/planning-artifacts/epics.md#Story 4.16: Validate lifecycle security boundaries`]
- The PRD makes metadata-only behavior non-negotiable: file contents, diffs, generated context payloads, provider tokens, credential material, secrets, and unauthorized resource existence must not appear in events, logs, traces, metrics, projections, diagnostics, audit records, provider payload snapshots, exception messages, command arguments, or console responses. [Source: `_bmad-output/planning-artifacts/prd.md#Security and Tenant Isolation`]
- PRD quality gates require tenant isolation tests for no cross-tenant read/write/lock/commit/provider/audit/projection access, path-security tests for traversal/absolute/mixed/encoded/reserved/Unicode/symlink/case behavior, and redaction tests that inject sentinel secrets and file-content markers across output channels. [Source: `_bmad-output/planning-artifacts/prd.md#Contract and Quality Gates`]
- Architecture process patterns require authorization before observation, sentinel tests over `tests/fixtures/audit-leakage-corpus.json`, aggregate purity, idempotency replay/conflict behavior, deterministic locks, and unknown provider outcomes entering reconciliation rather than unsafe retries. [Source: `_bmad-output/planning-artifacts/architecture.md#Process Patterns`]
- Architecture structure maps FR4-FR10 to `src/Hexalith.Folders/Authorization/` and tenant-access projections, FR32-FR36 to file/context query path policy, and concern #6 to redaction plus the audit leakage corpus. [Source: `_bmad-output/planning-artifacts/architecture.md#Requirements to Structure Mapping`]
- UX requirements matter indirectly: later operations-console trust views depend on backend evidence distinguishing redacted, inaccessible, denied, unknown, stale, missing, failed, dirty, locked, ready, and committed states without leaking contents or unauthorized resources. [Source: `_bmad-output/planning-artifacts/ux-design-specification.md#Stable UX Design Requirements`]

### Previous Story Intelligence

- Story 4.15 added deterministic aggregate replay and projection rebuild tests plus `FolderLifecycleReplayFixture`. Reuse those fixtures and patterns for Story 4.16 instead of creating a separate lifecycle model.
- Story 4.15 review fixed atomic duplicate-seed failure behavior in `InMemoryFolderRepository.Seed(...)`, narrowed freshness normalization so tenant-access `ProjectionWatermark` remains assertion-significant, and added cleanup-status determinism for committed, failed, and dirty states. Do not reintroduce over-normalization or partial-state mutation on rejected duplicate delivery.
- Story 4.15 validation used `DOTNET_ROOT=/home/administrator/.dotnet` to reach SDK `10.0.300`; default repo-root `dotnet` may resolve only SDK `10.0.108` and fail before MSBuild. Report this exactly if it recurs.
- Story 4.14 established `FolderAuditObservation`, `FolderAuditObservationBuilder`, `FolderAuditSanitizer`, `InMemoryFolderAuditObserver`, and low-cardinality telemetry naming. Story 4.16 should test those outputs and safe diagnostics; it should not create a second audit model.

### Existing Implementation State

- `WorkspacePathPolicyValidator` denies missing metadata, non-`NFC` declared normalization, invalid path policy class, invalid display names, overlength paths, non-NFC path content, control/invisible characters, non-ASCII path ambiguity, mixed separators, absolute/device paths, percent-encoded dot smuggling, empty/dot/traversal segments, Windows reserved base names, trailing space/dot ambiguity, and path-pattern escapes. Accepted paths produce a SHA-256 `pathmeta_...` digest and path policy class, not a raw path echo.
- `WorkspaceFileMutationService` authorizes first, validates command/path policy, loads the folder stream only after syntactic path acceptance, checks aggregate lock/state, asks path evidence, performs idempotency lookup, stages content or delete metadata, and appends if the idempotency fingerprint is absent. Its tests already assert many ordering boundaries; Story 4.16 should add missing security negative cases rather than rewriting the pipeline.
- `WorkspaceFileContextQueryHandlerTests` already prove tenant denial, path policy, sensitivity redaction, input bounds, metadata/range validation, and source unavailable/stale/timeout cases return before context-source observation. Extend this coverage for corpus/path/security cases without adding a real context source.
- `LayeredFolderAuthorizationServiceTests` and `TenantAccessAuthorizerTests` already assert canonical authorization order and fail-closed behavior for missing JWT evidence, claim-transform mismatch, client-controlled tenant mismatch, stale/disabled/unavailable/replay-conflicted tenant projections, folder ACL failures, EventStore validator denial, and Dapr policy denial.
- `SafetyInvariantGateTests` owns the authoritative corpus shape, safety channel inventory validation, negative-control quarantine scanning, OpenAPI/context-query metadata-only checks, safe denial examples, and offline safety gate workflow/documentation checks. Do not weaken these tests; reuse scanner concepts locally if needed.
- `ClientGenerationTests` already consumes `idempotency-encoding-corpus.json` for generated-helper/parser-policy behavior. Lifecycle tests should reference this contract and add side-effect-boundary assertions only where lifecycle code receives encoded or malformed values.

### Architecture Compliance

- Aggregates remain pure and deterministic. Do not add clocks, logging, Activity, Meter, random IDs, provider calls, Dapr calls, filesystem access, audit observer calls, or sanitizer calls inside aggregate handlers/state/apply code.
- Path and authorization denials must happen before protected-resource observation. For syntactic path rejection, no stream load, evidence call, content stage, delete stage, provider call, or append should occur.
- Tenant authority comes from authenticated context and EventStore claim-transform evidence. Payload, query, route, and header tenant/principal values are comparison inputs only.
- Tenant isolation must be enforced before file, workspace, credential, repository, lock, commit, provider, audit, cache, or context-source access.
- Metadata-only is non-negotiable across fixtures, results, serialized snapshots, audit observations, errors, assertion messages, and diagnostics.
- Idempotency is required for mutating lifecycle operations. Equivalent replay returns the same logical result; same key plus different payload returns conflict before side effects.
- Unknown provider outcomes and reconciliation-required evidence must stay inspectable and must not trigger blind retries or duplicate provider/file/commit actions.

### Project Structure Notes

- Expected new tests should live under focused existing test areas, for example:
  - `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderLifecycleSecurityBoundaryTests.cs`
  - `tests/Hexalith.Folders.Tests/Aggregates/Folder/WorkspacePathSecurityBoundaryTests.cs`
  - `tests/Hexalith.Folders.Tests/Queries/FileContext/WorkspaceFileContextSecurityBoundaryTests.cs`
  - `tests/Hexalith.Folders.Tests/Observability/FolderLifecycleRedactionBoundaryTests.cs`
- Prefer private helper methods or small test-only helpers near `FolderLifecycleReplayFixture`. Avoid production abstractions unless a real production bug is exposed.
- If a production bug is found, fix the owning production file and keep the regression test in this story. Do not hide bugs by loosening assertions, broadening freshness normalization, or treating forbidden sentinel output as acceptable.

### Files To Inspect First

- `tests/fixtures/audit-leakage-corpus.json`
- `tests/fixtures/safety-channel-inventory.json`
- `tests/fixtures/idempotency-encoding-corpus.json`
- `tests/fixtures/idempotency-encoding-corpus-consumption.yaml`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs`
- `tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspacePathPolicyValidator.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceFileMutationService.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceLockAcquisitionService.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceLockReleaseService.cs`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceCommitService.cs`
- `src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs`
- `src/Hexalith.Folders/Authorization/TenantAccessAuthorizer.cs`
- `src/Hexalith.Folders/Authorization/LayeredFolderOperationPolicy.cs`
- `src/Hexalith.Folders/Queries/FileContext/WorkspaceFileContextQueryHandler.cs`
- `src/Hexalith.Folders/Observability/FolderAuditSanitizer.cs`
- `src/Hexalith.Folders/Observability/FolderAuditObservation.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/WorkspacePathPolicyValidatorTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderWorkspaceFileMutationServiceTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderWorkspaceLockAcquisitionServiceTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderWorkspaceLockReleaseServiceTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderWorkspaceCommitServiceTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderLifecycleReplayFixture.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderLifecycleReplayDeterminismTests.cs`
- `tests/Hexalith.Folders.Tests/Queries/Folders/WorkspaceLifecycleProjectionDeterminismTests.cs`
- `tests/Hexalith.Folders.Tests/Queries/FileContext/WorkspaceFileContextQueryHandlerTests.cs`
- `tests/Hexalith.Folders.Tests/Authorization/TenantAccessAuthorizerTests.cs`
- `tests/Hexalith.Folders.Tests/Authorization/LayeredFolderAuthorizationServiceTests.cs`
- `tests/Hexalith.Folders.Tests/Observability/FolderAuditObservationTests.cs`

### Do Not Touch

- Do not implement runtime REST endpoints, provider adapters, workers, UI pages, CLI/MCP commands, SDK convenience helpers, generated-client files, production projection stores, Dapr state stores, repair/discard/retry automation, release packaging, alerting, or live provider drift checks.
- Do not add package references, upgrade central package versions, change target frameworks, alter `global.json`, or modify submodule setup.
- Do not call live GitHub/Forgejo, Dapr sidecars, Keycloak, Redis, EventStore actors, network services, local filesystem working copies, provider credentials, or production secrets from these tests.
- Do not weaken existing safe-denial, idempotency, C6 transition, metadata-only audit, read-only route, authorization-order, replay determinism, projection determinism, or safety invariant behavior.
- Do not add recursive submodule initialization or nested submodule update commands.

### Latest Technical Context

- Local repository pins are authoritative: .NET SDK `10.0.300`, `net10.0`, C# latest, central package management, Dapr `1.17.x`, Aspire `13.x`, Microsoft.Extensions `10.x`, OpenTelemetry `1.15.x`, xUnit v3 `3.2.2`, Shouldly `4.3.0`, and repository-owned package versions. [Source: `global.json`; `Directory.Packages.props`; `_bmad-output/project-context.md`]
- No external web research is required for this story. Story 4.16 is hermetic lifecycle security test hardening over existing repository code, pinned fixtures, and generated-helper contracts.

### Testing

- Use xUnit v3 and Shouldly. Avoid raw `Assert.*` unless an existing file’s pattern already requires it.
- Use `TestContext.Current.CancellationToken` in async tests.
- Keep tests hermetic under `tests/Hexalith.Folders.Tests`, `tests/Hexalith.Folders.Contracts.Tests`, or `tests/Hexalith.Folders.Client.Tests` as appropriate. Do not require Aspire, Dapr, Docker, TestServer, sockets, provider credentials, network access, or broad solution state.
- Use fixed timestamps (`FixedUtcClock`, `FixedTimeProvider`, `ConstantTimeProvider`) where comparisons include freshness or duration evidence.
- Prefer existing fakes such as `RecordingFolderRepository`, in-memory read models, `InMemoryFolderTenantAccessProjectionStore`, `InMemoryFolderAuditObserver`, and recording context/content/delete/evidence providers.
- If VSTest is blocked by sandbox socket permissions, run xUnit v3 in-process through the built test assembly and record the command/output summary.

### Regression Traps

- Do not satisfy sentinel tests by removing coverage from `safety-channel-inventory.json`.
- Do not add raw sentinel values to test assertion messages, scenario names, exception messages, snapshots, or debug logs.
- Do not treat `safe-provenance` as a blanket exemption; it is only allowed for explicitly declared provenance channels.
- Do not normalize away tenant-access projection watermarks, state, IDs, lock holder, task/correlation evidence, retry eligibility, provider outcome, sanitized category, path digest, or redaction classification.
- Do not test path security by asserting only `WorkspacePathPolicyValidator` behavior; lifecycle service ordering is part of the security boundary.
- Do not make wrong-tenant tests pass by using globally unique folder/workspace/task IDs. Reuse overlapping identifiers across tenants so tenant scoping is actually proven.
- Do not let parser-rejected or path-denied inputs reach stream load, idempotency lookup, provider/content/delete stores, context sources, audit resource queries, or appends.
- Do not add real absolute paths, real URLs, real emails, real-looking secrets, provider payloads, file contents, or diffs to fixtures. Use existing synthetic corpus values or safe placeholders only.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-27: Created via BMAD create-story workflow in YOLO mode with inputs from sprint status, Epic 4 Story 4.16, PRD, architecture, UX specification, root and sibling project contexts, Story 4.15, current lifecycle/path/auth/audit/safety code, fixtures, and recent git history.
- 2026-05-27: Discovery loaded whole planning artifacts from `_bmad-output/planning-artifacts`; no sharded planning docs were present. Persistent facts loaded from root and sibling `project-context.md` files.
- 2026-05-27: Validation checklist applied during drafting; guardrails added for sentinel-redaction, path-service ordering, encoding corpus boundaries, cross-tenant overlapping identifiers, safe denial evidence, metadata-only audit, and strict scope exclusions.
- 2026-05-27: Dev implementation added lifecycle security boundary tests, cross-tenant overlapping stream/ledger coverage, expanded path-policy/context-query unsafe path cases, malformed-idempotency short-circuit coverage, and audit redaction/bounded denial assertions.
- 2026-05-27: Validation initially hit the known VSTest socket permission issue and one fixture clock issue where the lock had expired before mutation. Parent validation fixed the test clock to stay inside the seeded lease and re-ran xUnit v3 in-process gates successfully.
- 2026-05-27: Parent validation passed focused core/test builds, Story 4.16 security/path/context/audit/lifecycle xUnit v3 in-process tests (143 total, 0 failed), Story 4.15 replay/projection determinism tests (25 total, 0 failed), authorization tests (29 total, 0 failed), contract safety/governance tests (22 total, 0 failed), and `git diff --check`.
- 2026-05-27: `tests/Hexalith.Folders.Client.Tests` validation is not clean in this workspace: the test project build tries to reach NuGet for `Hexalith.Folders.Client.Generation.Shared.csproj`, and direct in-process `ClientGenerationTests` execution reports pre-existing generated-output freshness drift. Story 4.16 did not edit OpenAPI, generated client files, SDK helper sources, or package versions.

### Senior Developer Review (AI)

Reviewer: Jerome on 2026-05-27

Outcome: Approved after auto-fix.

Findings fixed:

- [AI-Review][High] Cross-tenant lifecycle coverage was narrower than the completed task claimed. The new Story 4.16 test proved overlapping tenant IDs for file mutation, but did not directly exercise lock acquisition, lock release, or commit paths with overlapping tenant/folder/workspace/task/operation/idempotency identifiers. Fixed by adding tenant-scoped ready and staged lifecycle fixtures plus direct regression tests for lock acquisition ledger scoping, lock release isolation, and commit execution isolation in `FolderLifecycleSecurityBoundaryTests`.

Git vs story notes:

- Source File List covers the Story 4.16 source/test files reviewed. The workspace also has unrelated root submodule pointer/worktree changes and BMAD orchestration/test-summary artifacts; those are outside the source review surface for this workflow and were not changed by this review.

Validation:

- `DOTNET_ROOT=/home/administrator/.dotnet DOTNET_CLI_HOME=/tmp/sa-codex-home-fbbf34ce DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 /home/administrator/.dotnet/dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore --tlp:off -v:minimal /nr:false /m:1` - passed.
- `DOTNET_ROOT=/home/administrator/.dotnet DOTNET_CLI_HOME=/tmp/sa-codex-home-fbbf34ce DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 /home/administrator/.dotnet/dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore --tlp:off -v:minimal /nr:false /m:1` - passed.
- xUnit v3 in-process focused Story 4.16 classes: `FolderLifecycleSecurityBoundaryTests`, `FolderWorkspaceFileMutationServiceTests`, `WorkspacePathPolicyValidatorTests`, `FolderAuditObservationTests`, and `WorkspaceFileContextQueryHandlerTests` - passed.
- xUnit v3 in-process related lifecycle/auth/replay/projection class set - passed.
- `DOTNET_ROOT=/home/administrator/.dotnet DOTNET_CLI_HOME=/tmp/sa-codex-home-fbbf34ce DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 /home/administrator/.dotnet/dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore --tlp:off -v:minimal /nr:false /m:1` - passed.
- xUnit v3 in-process contract safety/governance classes: `SafetyInvariantGateTests` and `GovernanceCompletenessGateTests` - passed.
- `git diff --check` - passed.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Story file prepared for dev-story execution.
- Story status set to ready-for-dev.
- Added Story 4.16 lifecycle security boundary coverage across tenant scoping, path policy, context query path denial, mutation side-effect ordering, malformed idempotency keys, audit redaction, and bounded denial evidence.
- Parent validation passed with the pinned SDK path exported; story status set to review.
- Residual validation risk: generated-client freshness tests remain red in this workspace independently of the Story 4.16 touched files.
- Senior review auto-fixed missing direct cross-tenant lock acquisition, lock release, and commit isolation coverage; story status set to done.

### Change Log

- 2026-05-27: Added Story 4.16 lifecycle security tests and fixture support.
- 2026-05-27: Expanded path-policy/context-query/audit coverage and moved story to review after parent validation.
- 2026-05-27: Senior review added cross-tenant lock acquisition, lock release, and commit isolation regression tests; moved story to done.

### File List

- `_bmad-output/implementation-artifacts/4-16-validate-lifecycle-security-boundaries.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderLifecycleReplayFixture.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderLifecycleSecurityBoundaryTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderWorkspaceFileMutationServiceTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/WorkspacePathPolicyValidatorTests.cs`
- `tests/Hexalith.Folders.Tests/Observability/FolderAuditObservationTests.cs`
- `tests/Hexalith.Folders.Tests/Queries/FileContext/WorkspaceFileContextQueryHandlerTests.cs`
