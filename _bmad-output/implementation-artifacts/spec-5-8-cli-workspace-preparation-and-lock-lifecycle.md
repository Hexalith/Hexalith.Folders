---
title: '5.8 CLI workspace preparation and lock lifecycle'
type: 'feature'
created: '2026-10-06'
status: 'draft'
route: 'dispatch'
story_key: '5-8-cli-workspace-preparation-and-lock-lifecycle'
baseline_commit: '7fe68319c34ad8eaaed4edb2bf2a496e6ba8879d'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-5-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** CLI preparation and task-lock operations lack deployed durable parity evidence. Their existing Folders adapter is an obsolete migration source under the approved McpCli course correction.

**Approach:** Supply Folders-owned decorated Contracts, Gateway dispatch, and conformance evidence for the four canonical operations. Complete CLI verification through Hexalith.McpCli after the durable prerequisite and shared adapter seams are available.

## Boundaries & Constraints

**Always:** Preserve v2 oracle identity, authorization before access/replay, task ownership, mutation idempotency, C6 lifecycle, C7 timing, exact lock states, safe errors, and metadata-only output. Reuse EventStore persistence and existing domain services. Require Story 4.19's deployed path before completion.

**Never:** Expand proprietary adapters, implement local persistence, invent approvals, silently change parity, publish/enroll packages without authorization, retire historical adapters prematurely, or claim completion from mocks, seeding, memory, unavailable defaults, or infrastructure health.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Prepare/acquire/release | Authorized actor, valid task and key | Canonical acceptance followed by durable lifecycle outcome | Preserve operation/correlation/task identity |
| Inspect | Authorized workspace read, no key | Exact unlocked/locked/expired/stale/revoked state and freshness | Oracle-defined safe availability outcome |
| Replay/conflict/expiry | Same key with equivalent/changed intent or expired retention | Original logical result; conflict; expired rejection | No duplicate mutation or new execution |
| Isolation/collision | Wrong tenant, denied/revoked authority, competing owner or repository/ref alias | Safe denial or canonical lock conflict | No disclosure, takeover, or protected access |
| Failure/boundaries | Invalid inputs, provider failure, cancellation, timeout/unknown | Canonical exit/error/retry guidance | No secret/content leakage or blind retry |

</frozen-after-approval>

## Open Questions

1. **Delivery sequence:** Complete durable Story 4.19 and shared McpCli parity prerequisites first, then implement the full story; or deliver Folders-owned migration preparation now and leave Story 5.8 incomplete until deployed verification is possible.
2. **Inventory approval:** Approve the four-operation inclusion proposal below with existing task, key, lease, ownership-proof, correlation, and read-freshness behavior; or provide a different owner-approved inventory. Unsupported platform behavior requires explicit follow-up rather than omission.

| Canonical ID | Proposed McpCli operation |
|---|---|
| PrepareWorkspace | folders.prepare-workspace |
| LockWorkspace | folders.lock-workspace |
| GetWorkspaceLock | folders.get-workspace-lock |
| ReleaseWorkspaceLock | folders.release-workspace-lock |

## Code Map

- `src/Hexalith.Folders.Cli/Commands/Workspace/WorkspaceCommand.cs` and `tests/fixtures/parity-contract.yaml`: historical SDK mappings and canonical oracle; retain as comparison evidence.
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs`: existing three command processors, required `taskId` extension, and strict payload parsing. Accepted successes currently return an empty-event `PayloadNoOpDomainResult`.
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`: existing authorized lock-read mapping; reuse `WorkspaceLockStatusQueryHandler` rather than bypass authorization.
- `src/Hexalith.Folders.Server/FoldersServerHostComposition.cs` and `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs`: development repository/read models are in-memory; production durable registration is absent.
- `src/Hexalith.Folders.EventStore/`: existing prepare/lock/release idempotency adapters preserve equivalence; release reason is audit metadata.
- `references/Hexalith.McpCli/_bmad-output/implementation-artifacts/folders-migration-inventory-draft.md`: ordinary reference document; all four rows remain Pending.
- `references/Hexalith.McpCli/src/Hexalith.McpCli.Abstractions/`: existing module/command/query attributes. `Core/Execution/OperationExecutor.cs` builds Gateway requests. Read-only reference; shared exit/error, query-correlation, output, key-generation, and identifier compatibility gaps remain.

## Tasks & Acceptance

**Execution:**
- [ ] `docs/contract/mcpcli-workspace-operation-inventory.md` — record four canonical mappings, variants, owners, approval reference, and prerequisite/evidence status.
- [ ] `src/Hexalith.Folders.Contracts/Commands/{PrepareWorkspace,LockWorkspace,ReleaseWorkspaceLock}.cs`, `src/Hexalith.Folders.Contracts/Queries/GetWorkspaceLock.cs`, and `src/Hexalith.Folders.Contracts/FoldersModuleMetadata.cs` — add individually documented decorated contracts; reconcile strict payload and envelope bindings before implementation.
- [ ] `src/Hexalith.Folders.Contracts/Hexalith.Folders.Contracts.csproj` — consume the approved abstraction dependency through repository build configuration.
- [ ] `src/Hexalith.Folders.Server/GetWorkspaceLockDomainQueryHandler.cs` and `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs` — register Gateway inspection using existing authorization and freshness handling after the shared contract is settled.
- [ ] `tests/Hexalith.Folders.Contracts.Tests/McpCli/WorkspaceContractConformanceTests.cs` and `tests/Hexalith.Folders.Server.Tests/WorkspaceGatewayConformanceTests.cs` — verify schemas, binding, dispatch, and matrix boundaries against shared oracle rows.
- [ ] `tests/Hexalith.Folders.AppHost.Tests/CliWorkspacePreparationLockLifecycleTests.cs` — execute the released McpCli head through authenticated deployed composition; assert persisted end-state, restart/replay, isolation, alias collision, expiry, failure, and unknown outcomes.

**Acceptance Criteria:**
- Given accepted durable and platform prerequisites, when the four CLI operations execute, then every required oracle cell and matrix row passes through deployed composition.
- Given restart or cross-surface replay, when state is inspected, then persisted lifecycle/lock ownership and logical operation identity remain coherent.
- Given missing prerequisite evidence or a skipped governed scenario, when completion is evaluated, then Story 5.8 remains incomplete.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

- Aspire start and the 30-second Folders readiness check succeeded; all 17 resources became healthy. This Development baseline proves infrastructure only. The started AppHost was stopped successfully.
- After implementation, build affected projects individually in Debug and run their xUnit assemblies with `-class` filters. Run the AppHost test project with `HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION=true`; skips cannot satisfy acceptance. Record exact commands and persisted evidence.

The current source lacks durable preparation completion, canonical repository/ref lock serialization, populated authorization/read models, and Gateway lock-query registration. These are prerequisite gaps, not CLI completion evidence. A8 removed the general hold without authorizing ordinary execution. Approval records and the tracker remain unchanged.
