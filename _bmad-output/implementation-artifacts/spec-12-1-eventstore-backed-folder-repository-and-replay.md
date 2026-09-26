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

## Open Questions

- **Prerequisite route** — EventStore evolution is pending and Story 12.1 is held. Should this work first deliver EventStore Stories 6.5/6.6 as a separate platform release and record 12.1 admission (then implement Folders), or wait for platform/Delivery to complete those gates (keep this spec draft and Folders unchanged)? Current EventStore code is not an accepted release.

## Code Map

- `src/Hexalith.Folders.Server/FoldersDomainServiceRequestHandler.cs`, `FolderDomainProcessor.cs` — authorized processor path currently returns eventless NoOp.
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs` — `/project` returns 501.
- `src/Hexalith.Folders/Aggregates/Folder/IFolderRepository.cs`, `InMemoryFolderRepository.cs`, `FolderStateApply.cs` — memory contract and existing fold.
- `src/Hexalith.Folders/Aggregates/Organization/IOrganizationProviderBindingRepository.cs`, `OrganizationState.cs` — organization contract and fold.
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs`, `FolderRepositoryStartupAssertion.cs` — memory defaults and Production check.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/IDomainProjectionHandler.cs` — reuse SDK full-replay seam; current envelope lacks required version/registry.

## Tasks & Acceptance

**Execution:**
- [ ] `references/Hexalith.EventStore/_bmad-output/implementation-artifacts/ext-es-event-evolution-v1.yaml` — verify accepted version/digest and published API before Folders edits.
- [ ] `src/Hexalith.Folders/Aggregates/Folder/IFolderRepository.cs`, `src/Hexalith.Folders/Aggregates/Organization/IOrganizationProviderBindingRepository.cs` — expose EventStore-folded state reads without an independent append path; keep memory implementations test/dev only.
- [ ] `src/Hexalith.Folders/Aggregates/Folder/*Service.cs`, `src/Hexalith.Folders/Aggregates/Organization/ConfigureProviderBindingService.cs` — convert repository-writing command services to pure event decisions; only the actor appends.
- [ ] `src/Hexalith.Folders.Server/FolderDomainProcessor.cs`, `src/Hexalith.Folders.Server/FoldersDomainServiceRequestHandler.cs` — emit events after authorization; bound conflict re-evaluation.
- [ ] `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs`, `src/Hexalith.Folders.Server/FolderRepositoryStartupAssertion.cs` — register durable repositories and reject Production memory stores.
- [ ] `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs` — replace 501 with stateless SDK replay.
- [ ] `tests/Hexalith.Folders.Server.Tests/ServerEndpointRegistrationTests.cs`, `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderLifecycleReplayDeterminismTests.cs`, `tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs` — cover matrix, persisted end state, restart, tenants, and version fixtures.

**Acceptance Criteria:**
- Given the accepted event-evolution release and Story 12.1 admission, when an authorized folder or organization command completes, then EventStore contains one ordered, versioned, metadata-only event stream and Production resolves durable state.
- Given retained legacy and current event bytes, when `/project` replays from an empty checkpoint after restart, then current state is rebuilt without rewriting bytes and unsupported history fails closed.
- Given concurrent writers across supported replicas, when append conflicts, then EventStore is the sole transactional writer and at most one full re-evaluation occurs before canonical conflict.
- Given wrong-tenant, denied, unavailable, corrupt, and timeout paths, when a command or replay runs, then it exposes no protected state or confidential value and returns its canonical safe outcome.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `dotnet build Hexalith.Folders.slnx -c Debug` — builds against accepted EventStore API.
- `dotnet build tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj -c Debug` — builds durable lane; run built xUnit v3 assemblies individually with `-class` and inspect persisted end state.
