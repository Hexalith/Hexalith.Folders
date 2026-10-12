---
title: 'Story 12.1: EventStore-backed folder repository and projection replay'
type: 'feature'
created: '2026-09-26'
status: 'in-progress'
baseline_commit: '09c964353c1d7c15244b3a1c5f4066c361ada994'
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

**Approach:** After accepting the completed EventStore 6.6 capability and Story 12.1 admission, make EventStore the sole writer of versioned folder/organization events and replay them through its projection seam. Preserve REST, authorization, and Contract Spine behavior.

**Decisions (2026-10-11, owner renegotiation):** Adopt the completed EventStore 6.6 metadata V1 `EventTypeName` and `PayloadVersion` contract for Folders 12.1. Preserve the full CLR event name as a stable storage identifier; an absent payload-version stamp means version 1. The Folders-owned compatibility acceptance record replaces the unissued URI-based `ext-es-event-evolution-v1.yaml` gate. Record `DEC-EXEC-12.1` against the current source revision and checks before implementation. The 2026-09-29 URI decision is superseded for this story; its historical approvals and bytes remain historical evidence.

## Boundaries & Constraints

**Always:** A6b v2 authorization precedes lookup and append. EventStore AggregateActor transactionally advances state and events; one conflict permits one full authorization/domain re-evaluation. Persist full CLR `EventTypeName` values as frozen storage identifiers; use EventStore 6.6's `PayloadVersion` (absent or 1 means version 1, 2–1024 stamped) separately from `MetadataVersion`. Register pure SDK upcasters for supported evolution and byte fixtures for retained historical aliases. Persist metadata only; fail closed on unknown Folders stream events, unsupported known versions, and store failures. Production resolves durable repositories and replay handlers. Prove restart, replay, replica conflict, denial, timeout, idempotency, and leakage exclusion on the deployed path.

**Never:** Hand-roll Dapr state or upcasting in Folders, rewrite historical bytes, blindly append/repeat provider effects, allow Production NoOp/memory fallback, or count fake/seed results as durable proof. Keep frozen A6b artifacts and sprint-status unchanged without separate approval.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Accepted command | Authorized folder/organization intent | One ordered metadata-only event stream and folded state | Same key/equivalent intent replays; same key/different intent conflicts |
| Replay | Empty checkpoint or restarted host | Current-semantic projections rebuilt from retained events without byte rewrite | Unknown Folders event, unsupported known version, or invalid stamp fails closed; absent stamp is version 1 |
| Concurrent write | Two replicas, stale expected version | One transactional winner; at most one full re-evaluation | Exhaustion maps to HTTP 409, CLI 77, MCP `concurrency_conflict` |
| Protected failure | Wrong tenant, denial, outage, timeout | No mutation or existence disclosure | Canonical metadata-only result |

</frozen-after-approval>

## Code Map

- `_bmad-output/planning-artifacts/planning-story-manifest.yaml:1340,5807`, `_bmad-output/planning-artifacts/architecture.md:706` — align the external gate with completed EventStore 6.6 and the 2026-10-11 D-12 decision; preserve historical approval digests.
- `src/Hexalith.Folders.Server/FoldersDomainServiceRequestHandler.cs:27`, `FolderDomainProcessor.cs:84,1305` — retain authorization and scoped result access; consume actor state and replace accepted NoOp results with events.
- `src/Hexalith.Folders/Aggregates/Folder/IFolderRepository.cs`, `FolderState.cs:67`, `src/Hexalith.Folders/Aggregates/Organization/IOrganizationProviderBindingRepository.cs`, `OrganizationState.cs:11` — reuse folds; stop Production independent append. Memory repositories serve development/tests.
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:26`, `FoldersServerHostComposition.cs:30`, `FolderRepositoryStartupAssertion.cs:18`, `FoldersDomainServiceEndpoints.cs:94` — wire Production durability and SDK `IDomainProjectionHandler`; current `/project` is 501.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/story-12-1-eventstore-6-6-acceptance-2026-10-11.yaml` — verify the pinned EventStore 6.6 API, focused compatibility checks, and `DEC-EXEC-12.1`; stop on mismatch before Folders code edits.
- [ ] `src/Hexalith.Folders/Aggregates/Folder/IFolderRepository.cs`, `src/Hexalith.Folders/Aggregates/Organization/IOrganizationProviderBindingRepository.cs` — replace Production append with actor-folded reads.
- [ ] `src/Hexalith.Folders/Aggregates/Folder/*Service.cs`, `src/Hexalith.Folders/Aggregates/Organization/ConfigureProviderBindingService.cs` — return metadata-only event decisions without repeating provider effects.
- [ ] `src/Hexalith.Folders.Server/FolderDomainProcessor.cs`, `FoldersDomainServiceRequestHandler.cs` — emit events after authorization; allow one full conflict re-evaluation.
- [ ] `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs`, `FoldersServerHostComposition.cs`, `FolderRepositoryStartupAssertion.cs` — bind durable Production state; reject memory fallback.
- [ ] `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`, new `FolderDomainProjectionHandler.cs` — replace 501 with stateless SDK replay.
- [ ] `tests/Hexalith.Folders.Server.Tests/ServerEndpointRegistrationTests.cs`, `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderLifecycleReplayDeterminismTests.cs`, `tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs`, `tests/fixtures/event-evolution/` — prove the frozen event-name inventory, version-1 unstamped bytes, retained bytes, persisted end state, restart, conflict, denials, and failure safety.

**Acceptance Criteria:**
- Given accepted EventStore 6.6 compatibility and 12.1 admission, when Production accepts a folder or organization command, then one metadata-only stream using the frozen event-name and payload-version contract persists and survives restart.
- Given retained events and an empty checkpoint, when `/project` replays, then current state rebuilds without byte rewrite and unsupported versions fail closed.
- Given concurrent replicas, when append conflicts, then at most one authorized re-evaluation occurs before canonical conflict.
- Given denial, wrong tenant, corrupt or unavailable store, or timeout, when command/replay runs, then no protected state or confidential value leaks.

## Implementation Notes

Gate: verify the EventStore 6.6 consumer acceptance record and conditional `DEC-EXEC-12.1` before Folders code edits. The former URI-based gate was superseded by the owner on 2026-10-11.

2026-10-11 implementation checkpoint: the source now includes actor-folded Production repository bindings, event-bearing accepted results, SDK projection dispatch, replay validation, local workload credentials, and retained version-1 fixtures. This is an in-progress slice. The live isolated Aspire run reached healthy EventStore and Folders resources, but EventStore's `/process` invocation received HTTP 401 `authentication_failure` from Folders. Its signed workload assertion proves the EventStore caller, audience, and `domain-service:process` operation; it does not carry the human `eventstore:tenant` and permission evidence required by the A6b authorization gate. The gateway surfaced HTTP 500, and a separate idempotency-key submission returned HTTP 503 `idempotency_admission_unavailable`. No durable append, stored end state, restart recovery, or replica conflict was observed. Do not derive human authority from the command envelope. The EventStore-to-Folders verified human authorization handoff needs a separately reviewed contract before this story can satisfy its deployed-path criteria. JSON snapshot replay also remains unproven. The temporary Aspire stack was stopped.

## Spec Change Log

- 2026-10-11 (human-approved renegotiation): "apply recommended" accepted EventStore 6.6's full CLR `EventTypeName` plus `PayloadVersion` contract for Story 12.1. The previous frozen URI/always-stamped requirement and external-release acceptance path are superseded; all durability, authorization, replay, and failure acceptance criteria remain in scope. See `story-12-1-eventstore-6-6-decision-2026-10-11.md`.

## Review Triage Log

## Verification

**Commands:**
- `dotnet build Hexalith.Folders.slnx -c Debug` — builds against accepted EventStore API.
- `dotnet build tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj -c Debug` — build durable lane; run focused xUnit v3 assemblies and inspect persisted state.

**2026-10-11 observed:** The EventStore 6.6 checks and Folders baseline build are recorded in `story-12-1-eventstore-6-6-acceptance-2026-10-11.yaml`. After the source edits, `dotnet build Hexalith.Folders.slnx -c Debug -m:1 -v:q` passed with zero warnings/errors. The focused Server.Tests and IntegrationTests builds also passed. Direct xUnit v3 checks passed: `FolderEventReplayTests` 11/11, `ServerEndpointRegistrationTests` 6/6, `FolderLifecycleReplayDeterminismTests` 17/17, `ArchiveFolderProcessWiringTests` 22/22, `AspireTopologyTests` 14/14, and `GovernanceCompletenessGateTests` 23/23. `git diff --check` passes for tracked source, tests, and our planning edits; the whole-tree form is affected by a pre-existing user-owned trailing space in `sprint-status.yaml`. Earlier `--no-restore` solution attempts failed on inconsistent dependency assets; the normal solution build restored and passed. The deployed-path acceptance rows remain open as described above.
