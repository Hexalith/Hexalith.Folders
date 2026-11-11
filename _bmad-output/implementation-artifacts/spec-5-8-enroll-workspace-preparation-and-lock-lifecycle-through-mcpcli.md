---
title: '5.8 Enroll workspace preparation and lock lifecycle through McpCli'
type: 'feature'
created: '2026-11-11'
status: 'in-progress'
route: 'dispatch'
story_key: '5-8-enroll-workspace-preparation-and-lock-lifecycle-through-mcpcli'
baseline_commit: 'fb3187bfcc31a03d2febb3a6ddc41532cf3670b6'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-5-context.md'
  - '{project-root}/docs/contract/mcpcli-folders-operation-declarations.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Workspace preparation and lock parity lacks deployed Hexalith.McpCli evidence. The October draft is superseded.

**Approach:** Execute the three workspace Commands and five inspection Queries through both common heads; compare canonical semantics with REST/SDK and assert persisted effects, restart and replay.

## Boundaries & Constraints

**Always:** Require accepted deployed Story 4.19 and EXT-MCPCLI-FOLDERS-ENROLLMENT, including 5.13/5.14. Cover C11 prepare, C09 acquire, C12 release and Q22–Q26 cleanup, lock, retry eligibility, status and transition evidence. Preserve authorization before access/replay, independent caller keys, task/lease ownership, C6, C7, exact lock states and metadata-only disclosure. Apply the approved 5.12 losses: uppercase ULID caller keys, McpCli profiles, JSON and operational exit 2. Preserve taskId profile allowlists and reconcile payload/extension context. Bind every result to the enrolled release and inventory.

**Never:** Expand legacy adapters, simulate enrollment, import module implementations into McpCli, change dependencies or other repositories, publish, retire binaries, invent approvals, or implement prerequisite stories here. Mocks, seeding, NoOp, unavailable/safe-empty responses, in-memory state and skipped tests cannot prove completion.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Prepare/acquire/inspect/release | Authorized task and ready binding | Same logical identity and persisted lifecycle across heads | Canonical category/code/retryable/clientAction |
| Key reuse | Equivalent, changed or expired key | Original result, conflict or expired rejection; no duplicate effect | No implicit key conversion or new execution |
| Isolation/ownership | Wrong tenant, denied/revoked actor, mismatched task/proof | Denial with unchanged protected state | No protected lookup/disclosure |
| Collision/timing | Repository/ref aliases, competing tasks, lease boundaries | Serialized writer; unlocked/locked/expired/stale/revoked per C7 | No takeover/reactivation |
| Failure/unknown | Provider failure, cancellation, timeout | Canonical failure or unknown/reconciliation and retry eligibility | No blind retry |
| Read/input bounds | Missing/invalid context, read key, stale evidence | Oracle rejection/freshness; no source access when forbidden | Safe metadata-only errors |

</frozen-after-approval>

## Code Map

- Reuse the eight types in `src/Hexalith.Folders.Contracts/{Commands,Queries}/` and the owner declaration table.
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs` emits real preparation/lock/release events; `FoldersServerHostComposition.cs` registers ActorFoldedFolderRepository. Old NoOp/repository claims are stale. Uppercase envelope admission, caller-key separation and DTO translation remain 5.13 work.
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs` registers only three owner/file Gateway queries; workspace query routing belongs to 5.14. Canonical repository/ref collision and C7 enforcement belong to 4.23, a 4.19 prerequisite.
- `tests/Hexalith.Folders.AppHost.Tests/FolderDurabilityAcceptanceFixture.cs` supplies authenticated provider setup, ReadStreamAsync and RestartHostsAsync. Reuse supported observations; its seeded/rebuild diagnostics are not acceptance.
- McpCli `tests/Hexalith.McpCli.Integration.Tests/BoundaryHeads.cs` in the root reference demonstrates real CLI/MCP processes. Current enrollment covers Parties/Tenants only.

## Tasks & Acceptance

**Execution:**
- [ ] `_bmad-output/planning-artifacts/planning-story-manifest.yaml` — read-only gate: verify execution authority, accepted 4.19 evidence, enrolled release, approved inventory/vectors and Gateway prerequisites; stop dependent work when absent.
- [ ] `tests/fixtures/parity-contract.schema.json`, `tests/tools/parity-oracle-generator/Program.cs`, `tests/fixtures/parity-contract.yaml`, `tests/shared/Parity/ParityOracle.cs` — derive bounded mcpcli_operation mappings from accepted enrollment inputs and expose them to tests; regenerate, preserve legacy columns and reject missing/mismatched workspace mappings without inventing dispositions for other operations.
- [ ] `tests/Hexalith.Folders.Contracts.Tests/OpenApi/ParityOracleGeneratorTests.cs` — verify deterministic mapping generation, schema validation and drift failures.
- [ ] `tests/Hexalith.Folders.AppHost.Tests/McpCliHeadProcess.cs` — run the enrolled CLI send/query and stdio MCP send_command/run_query; capture complete channels and protocol failures with bounded cancellation and owned cleanup.
- [ ] `tests/Hexalith.Folders.AppHost.Tests/Hexalith.Folders.AppHost.Tests.csproj` — link the shared oracle and use centrally versioned test dependencies only; never reference McpCli implementation assemblies.
- [ ] `tests/Hexalith.Folders.AppHost.Tests/McpCliWorkspaceLifecycleTests.cs` — drive both heads and REST/SDK with equivalent vectors; cover every matrix row, persisted event/projection end-state, restart and replay; fail governed runs on missing prerequisites or skips.
- [ ] `docs/evidence/story-5-8-mcpcli-workspace/README.md` — retain secret-scanned version/Catalog/inventory/vector/environment bindings, exact commands, persisted observations and unresolved gates.

**Acceptance Criteria:**
- Given accepted prerequisites, when all eight operations run through both common heads, then canonical identity, errors, lifecycle, lock state, read freshness and evidence match REST/SDK and C13.
- Given a restarted host or cross-head replay, when persisted state is inspected, then ownership and operation identity survive without duplicate events/effects.
- Given an absent prerequisite, failed cell or skipped governed scenario, when completion is evaluated, then 5.8 remains incomplete.

## Implementation Notes

- 2026-11-11: Jerome approved this spec and requested continuation ("approve and continue"). The frozen intent is locked. Approval authorizes this story's scoped work; prerequisite acceptance and external enrollment are separate evidence gates.

- 2026-11-11: Implementation dispatch verified the prerequisite gate and stopped dependent work. Stories 4.19/5.13/5.14 have open delivery evidence; external enrollment is pending; current McpCli heads enroll only Parties/Tenants. Recorded [blocked evidence](../../docs/evidence/story-5-8-mcpcli-workspace/README.md) with exact commands and seven input hashes. The parent independently reproduced gate exit 2, query-registration exit 0 and disclosure-scan exit 0, and verified every retained hash. No runtime tests, code changes or acceptance claims; remaining tasks and matrix coverage stay incomplete. Spec and sprint status remain in-progress.

## Spec Change Log

## Review Triage Log

## Verification

Build Contracts.Tests and AppHost.Tests individually with `dotnet build <project.csproj> -c Debug`; run their built xUnit assemblies directly. Run `ParityOracleGeneratorTests` first, then `HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION=true dotnet tests/Hexalith.Folders.AppHost.Tests/bin/Debug/net10.0/Hexalith.Folders.AppHost.Tests.dll -class Hexalith.Folders.AppHost.Tests.McpCliWorkspaceLifecycleTests -showLiveOutput`. Start/inspect Aspire before code edits through the orchestration skill. Report exact blockers separately from focused passes; run affected existing suites before completion.

Investigation: 5.12 is done; 4.19/5.13/5.14 are backlog, enrollment pending and 5.8 execution held. No build or deployed acceptance run performed.
