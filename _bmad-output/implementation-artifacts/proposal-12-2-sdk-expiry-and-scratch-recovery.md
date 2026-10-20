---
title: Story 12.2 source-deadline expiry and scratch-recovery prerequisites
status: approved-for-separate-eventstore-implementation
created: 2026-10-20
parent_spec: spec-12-2-durable-projections-and-task-completion-pipeline.md
---

# Required EventStore work

The remaining round-four retention and interrupted-batch acceptance rows need two generic SDK capabilities. The current Folders code already applies source-time expiry when it plans operations and uses the production named/shared dispatcher for rebuild. The focused follow-up reran seven retention and seven capacity cases with zero failures or skips. Three passing diagnostics intentionally reproduce missing SDK behavior; they do not establish acceptance. Exact source, assembly and result hashes are in [the follow-up evidence](evidence/story-12-2-followup-qualification.json).

The user approved this proposal on 2026-10-20 with “I approve”, in response to the explicit request to authorize these two prerequisites for separate EventStore implementation. This authorizes the bounded SDK changes and qualification described below, followed by source consumption and requalification in Folders. Folders must consume the resulting supported SDK behavior without a local persistence or recovery protocol.

## Source deadlines through delayed recovery

`OperationalStatusRetentionTests.DiagnosticStagedSdkManifestRetainsRelativeTtlAcrossSourceDeadlineAsync` plans a lifecycle view at source age 399 days, stages its correct one-day remaining TTL, advances the domain clock to source age 401 days, and commits the unchanged SDK batch. The view is still installed with the originally sealed one-day TTL. Fresh domain planning at age 401 days correctly emits only the bounded checkpoint. Existing readers also enforce source age; the failure concerns physical retention during delayed SDK execution.

The SDK retains only relative `TimeToLive` in `ReadModelBatchOperation` and `SharedProjectionMutation`. `ReadModelBatchProtocol.CompactAndCompleteAsync` reapplies that duration during compaction and recovery. `DaprReadModelBatchStateAccessor` converts it to `ttlInSeconds` at write time. Replanning in Folders cannot change an immutable retained manifest or cover SDK-owned recovery after a process replacement.

Authorize the EventStore owner to supply an immutable absolute-expiry capability for recoverable batch operations and shared-generation mutations. The owner selects the smallest compatible API, protocol and storage changes necessary. It must:

- Preserve the original source deadline across staging, selected-generation publication, compaction, recovery, retries and process replacement.
- Avoid installing an already expired value, and ensure a pre-deadline installation cannot receive a fresh relative retention window during later recovery.
- Preserve manifest identity, concurrency fencing, atomic completion, bounded batches and safe missing-versus-unavailable reads. SDK recovery must not overwrite a newer value when expiring an older candidate.
- Apply the same domain deadline to each view, its integrity evidence and its dependent position/message receipts, while retaining the existing non-expiring bounded checkpoint and source-owned closure authority.
- Preserve retained event bytes, existing C3 legal-hold ownership and the exclusion of destructive content cleanup.

Qualification must exercise one tick before and at the inclusive source deadline, delayed Stage/Commit/compaction, interrupted recovery, terminal rewrites, duplicate delivery, inherited generations and concurrent newer values. Include the SDK production dispatcher and a real Dapr-backed store; controlled-clock component evidence alone does not qualify physical expiry. Folders must then rerun its retention matrix on the exact consumed SDK binaries.

## Pending scratch-fence reconciliation

`OperationalStatusCapacityTests.DiagnosticRetryAndAbortCannotReconcileSdkPendingScratchFenceAsync` injects one `BeforeCommit` failure during a private scratch batch. The named dispatcher retries the unchanged Accumulate action and then attempts Abort. Both exhaust with `Accumulating/Indeterminate/projection_handler_failure`; the durable epoch remains Building and no operational checkpoint is selected. The separate clean pre-dispatch failure test does abort successfully and can subsequently rebuild, so it is not a substitute for retained scratch-batch recovery.

`SharedProjectionEpochCoordinator.TryWriteScratchAsync` seals the private scratch write and epoch-state write in the same batch. After the failure, Abort can persist Aborting, but subsequent SDK batch recovery restores the sealed Building state before `AbortAsync` requires Aborting. `DomainSharedProjectionRebuildDispatcher.FencedAbortAsync` reaches the same coordinator path. Changing the Folders fold or reissuing the same transport action does not resolve the retained fence.

Authorize the EventStore owner to reconcile a pending private scratch batch before retrying or advancing its epoch fence. The owner must first assess whether this can be an internal correctness repair within the existing protocol; any necessary public API or storage change requires an explicit owner decision. It must:

- Recover the original immutable batch without regressing a newer epoch/phase or bypassing operation, tenant, generation, identity or CAS guards.
- Let the same named action resume safely, or let Abort finish, remove private staging through SDK controls and reopen the existing active generation.
- Keep incomplete new rows invisible, preserve previously selected rows, and drain admitted ordinary deliveries to the correct generation without losing their identity evidence.
- Permit subsequent ordinary delivery and a new named rebuild after successful reconciliation. A stale or foreign operation must remain unable to resume or abort another operation.
- Preserve the recoverable 100-operation/one-MiB limits and retain manifests until the existing protocol permits reclamation. Folders must not erase SDK state or force an epoch open.

Qualification must cover retained failures before and after scratch installation/commit/compaction, repeated Accumulate and Abort, concurrent writers/aborters, a changed process, multiple source owners and real Dapr storage. A fresh fixture must prove no selected new rows and no Building session after Abort; a previously populated fixture must preserve the old selected generation and resume catch-up. Convert the current diagnostic into successful recovery acceptance only after those guarantees are demonstrated.

# Other open acceptance

The separately [approved bounded owner-identity prerequisite](proposal-12-2-sdk-bounded-owner-identity.md) remains EventStore-owned and unimplemented here: ordinary delivery accepts position 10,001, but complete production rebuild still rejects that lawful captured identity vector. It needs implementation, qualified consumption and a Folders rerun before Task 36 can close.

Stage six also retains command acknowledgement p95 of 31,330.090 ms against SM4's 1,000 ms target across nine successful requests including retries. That is an open observation; this proposal neither redesigns admission nor attributes the delay to either SDK blocker above.

Coordinated package qualification remains required. `pwsh -File tests/tools/run-contract-spine-gates.ps1` is blocked because the published EventStore 3.125.1 package lacks `MapEventStoreDomainProjectionEndpoints` and `MapEventStoreDomainProjectionMetadataEndpoint`. Its recorded `-NoRestore` follow-up also retains three historical PD10 inventory/generator failures. Preserve the frozen historical artifacts; source-inventory requalification and independent review remain separate gates.

The spec and sprint story remain in progress. The approval authorizes the two SDK capabilities above and their local source consumption; it does not authorize a package or submodule pin update, publication, deployment, public v2 exposure, commit or push. The separately approved bounded owner-identity prerequisite retains its existing owner and disposition. Preserve all six historical evidence stages and the existing parent qualification records.

## Execution record

The EventStore-owned implementation spec is [the SDK expiry and scratch-recovery spec](../../references/Hexalith.EventStore/_bmad-output/implementation-artifacts/spec-folders-12-2-expiry-and-scratch-recovery.md). The approval is recorded before any SDK edit. All prior diagnostic evidence remains exact-source history.

The approved prerequisites and Folders adoption are now locally source-qualified.
[The current qualification packet](evidence/story-12-2-approved-sdk-source-qualification-20261020/qualification.json)
records the exact SDK/consumer sources and test binaries, successful pending-scratch OS-process
replacement, 4,195 consumer passes and the full real-provider live pass. Independent review is pending
at the packet seal. Package/PD10 qualification, >10,000 identity completion and acknowledgement
performance remain open. The approval scope and exclusions above are unchanged.
