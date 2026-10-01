---
title: 'Story 12.1: EventStore-backed folder repository and projection replay'
type: 'feature'
created: '2026-09-26'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '_bmad-output/implementation-artifacts/epic-12-context.md'
  - '_bmad-output/planning-artifacts/planning-story-manifest.yaml'
  - 'references/Hexalith.AI.Tools/hexalith-state-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Accepted folder and organization commands mutate in-memory state, return eventless NoOp results, and leave `/project` at 501. Production cannot replay lifecycle state after restart.

**Approach:** After EventStore event-evolution release and Story 12.1 admission, make EventStore the sole writer of versioned folder/organization events and replay them through its projection seam. Preserve REST, authorization, and Contract Spine behavior.

**Decisions (2026-09-29):** Platform first: EventStore 6.5c, 6.5 approval, 6.6, and the `ext-es-event-evolution-v1.yaml` acceptance record are delivered in the EventStore repository under its own sessions, never authored from Folders. Folders stays unchanged until that record exists. `DEC-EXEC-12.1` is recorded after that platform gate, on the Story 1.17 authorization pattern, and binds the digests current at that time.

## Boundaries & Constraints

**Always:** A6b v2 authorization precedes lookup and append. EventStore AggregateActor transactionally advances state and events; one conflict permits one full authorization/domain re-evaluation. Events use stable `urn:hexalith:folders:event:<kebab-name>` types, positive payload versions distinct from `MetadataVersion`, and closed legacy aliases with byte fixtures. Persist metadata only; fail closed on bad versions/store failures. Production resolves durable repositories and replay handlers. Prove restart, replay, replica conflict, denial, timeout, idempotency, and leakage exclusion on the deployed path.

**Never:** Hand-roll Dapr state or upcasting in Folders, rewrite historical bytes, blindly append/repeat provider effects, allow Production NoOp/memory fallback, or count fake/seed results as durable proof. Keep frozen A6b artifacts and sprint-status unchanged without separate approval.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Accepted command | Authorized folder/organization intent | One ordered metadata-only event stream and folded state | Same key/equivalent intent replays; same key/different intent conflicts |
| Replay | Empty checkpoint or restarted host | Current-semantic projections rebuilt from retained events without byte rewrite | Missing/future/invalid version or unregistered alias fails closed |
| Concurrent write | Two replicas, stale expected version | One transactional winner; at most one full re-evaluation | Exhaustion maps to HTTP 409, CLI 77, MCP `concurrency_conflict` |
| Protected failure | Wrong tenant, denial, outage, timeout | No mutation or existence disclosure | Canonical metadata-only result |

</frozen-after-approval>

## Code Map

- `_bmad-output/planning-artifacts/planning-story-manifest.yaml` — A6b/A8 approved; 12.1 held by the absent `EXT-ES-EVENT-EVOLUTION` acceptance record.
- `src/Hexalith.Folders.Server/FoldersDomainServiceRequestHandler.cs`, `FolderDomainProcessor.cs` — authorization precedes processing; processor ignores actor state and discards accepted result events into NoOp.
- `src/Hexalith.Folders/Aggregates/Folder/IFolderRepository.cs`, `FolderStateApply.cs`; `src/Hexalith.Folders/Aggregates/Organization/IOrganizationProviderBindingRepository.cs`, `OrganizationState.cs` — memory append contracts and reusable event folds.
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs`, `FoldersServerHostComposition.cs`, `FolderRepositoryStartupAssertion.cs` — memory organization default; Production lacks durable folder registration.
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`; `references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/IDomainProjectionHandler.cs` — replace `/project` 501 using SDK replay after the accepted version API arrives; actor already owns transactional append.

## Tasks & Acceptance

**Execution:**
- [ ] `references/Hexalith.EventStore/_bmad-output/implementation-artifacts/ext-es-event-evolution-v1.yaml` — verify accepted release digest/API and 12.1 admission before Folders edits.
- [ ] `src/Hexalith.Folders/Aggregates/Folder/IFolderRepository.cs`, `src/Hexalith.Folders/Aggregates/Organization/IOrganizationProviderBindingRepository.cs` — remove Production independent append; read actor-folded state.
- [ ] `src/Hexalith.Folders/Aggregates/Folder/` (`FolderCreationService.cs`, `FolderAccessMutationService.cs`, `RepositoryBackedFolderCreationService.cs`, `RepositoryBindingService.cs`, `BranchRefPolicyConfigurationService.cs`, `WorkspacePreparationService.cs`, `WorkspaceLockAcquisitionService.cs`, `WorkspaceLockReleaseService.cs`, `WorkspaceFileMutationService.cs`, `WorkspaceCommitService.cs`); `src/Hexalith.Folders/Aggregates/Organization/ConfigureProviderBindingService.cs` — return event decisions; preserve authorization and avoid repeating provider effects.
- [ ] `src/Hexalith.Folders.Server/FolderDomainProcessor.cs`, `FoldersDomainServiceRequestHandler.cs` — emit result events after authorization and bound full conflict re-evaluation to one attempt.
- [ ] `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs`, `FoldersServerHostComposition.cs`, `FolderRepositoryStartupAssertion.cs` — register durable folder/organization state and reject Production memory.
- [ ] `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs` — use stateless versioned SDK projection replay.
- [ ] `tests/Hexalith.Folders.Server.Tests/ServerEndpointRegistrationTests.cs`, `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderLifecycleReplayDeterminismTests.cs`, `tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs`, `tests/fixtures/event-evolution/` — prove the matrix, retained bytes, persisted end state, restart, conflict, and safe failures.

**Acceptance Criteria:**
- Given accepted EventStore evolution and 12.1 admission, when Production accepts a folder or organization command, then one versioned metadata-only stream persists and survives restart.
- Given retained events and an empty checkpoint, when `/project` replays, then current state rebuilds without byte rewrite and unsupported versions fail closed.
- Given concurrent replicas, when append conflicts, then at most one authorized re-evaluation occurs before canonical conflict.
- Given denial, wrong tenant, corrupt or unavailable store, or timeout, when command/replay runs, then no protected state or confidential value leaks.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `dotnet build Hexalith.Folders.slnx -c Debug` — builds against accepted EventStore API.
- `dotnet build tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj -c Debug` — build durable lane; run focused xUnit v3 assemblies and inspect persisted state.
