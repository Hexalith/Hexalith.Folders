---
title: 'Story 12.1: EventStore-backed folder repository and projection replay'
type: 'feature'
created: '2026-09-26'
status: 'in-review'
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
- [x] `src/Hexalith.Folders/Aggregates/Folder/IFolderRepository.cs`, `src/Hexalith.Folders/Aggregates/Organization/IOrganizationProviderBindingRepository.cs` — replace Production append with actor-folded reads.
- [x] `src/Hexalith.Folders/Aggregates/Folder/*Service.cs`, `src/Hexalith.Folders/Aggregates/Organization/ConfigureProviderBindingService.cs` — return metadata-only event decisions without repeating provider effects.
- [x] `src/Hexalith.Folders.Server/FolderDomainProcessor.cs`, `FoldersDomainServiceRequestHandler.cs` — emit events after authorization; allow one full conflict re-evaluation.
- [x] `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs`, `FoldersServerHostComposition.cs`, `FolderRepositoryStartupAssertion.cs` — bind durable Production state; reject memory fallback.
- [x] `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`, new `FolderDomainProjectionHandler.cs` — replace 501 with stateless SDK replay.
- [x] `tests/Hexalith.Folders.Server.Tests/ServerEndpointRegistrationTests.cs`, `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderLifecycleReplayDeterminismTests.cs`, `tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs`, `tests/fixtures/event-evolution/` — prove the frozen event-name inventory, version-1 unstamped bytes, retained bytes, persisted end state, restart, conflict, denials, and failure safety.

**Acceptance Criteria:**
- Given accepted EventStore 6.6 compatibility and 12.1 admission, when Production accepts a folder or organization command, then one metadata-only stream using the frozen event-name and payload-version contract persists and survives restart.
- Given retained events and an empty checkpoint, when `/project` replays, then current state rebuilds without byte rewrite and unsupported versions fail closed.
- Given concurrent replicas, when append conflicts, then at most one authorized re-evaluation occurs before canonical conflict.
- Given denial, wrong tenant, corrupt or unavailable store, or timeout, when command/replay runs, then no protected state or confidential value leaks.

## Implementation Notes

Gate: verify the EventStore 6.6 consumer acceptance record and conditional `DEC-EXEC-12.1` before Folders code edits. The former URI-based gate was superseded by the owner on 2026-10-11.

2026-10-11 implementation checkpoint: the source now includes actor-folded Production repository bindings, event-bearing accepted results, SDK projection dispatch, replay validation, local workload credentials, and retained version-1 fixtures. This is an in-progress slice. The live isolated Aspire run reached healthy EventStore and Folders resources, but EventStore's `/process` invocation received HTTP 401 `authentication_failure` from Folders. Its signed workload assertion proves the EventStore caller, audience, and `domain-service:process` operation; it does not carry the human `eventstore:tenant` and permission evidence required by the A6b authorization gate. The gateway surfaced HTTP 500, and a separate idempotency-key submission returned HTTP 503 `idempotency_admission_unavailable`. No durable append, stored end state, restart recovery, or replica conflict was observed. Do not derive human authority from the command envelope. The EventStore-to-Folders verified human authorization handoff needs a separately reviewed contract before this story can satisfy its deployed-path criteria. JSON snapshot replay also remains unproven. The temporary Aspire stack was stopped.

2026-10-12 checkpoint: JSON folder and organization snapshots now rebuild state in the focused replay lane; foreign tenant or aggregate identities fail closed. The organization reader reconstructs its `IReadOnlySet` from the stored JSON array because the default serializer cannot instantiate that interface. The focused `FolderEventReplayTests` class passes 13/13 after a Debug test-project build. The local Aspire EventStore host now receives a persistent generated development idempotency key ring; this addresses the missing configuration found during static inspection, but the prior deployed HTTP 503 has not been rerun. This does not prove deployed snapshot creation, stored end state, or restart recovery. The platform trust-boundary proposal is recorded in `story-12-1-human-authorization-handoff-proposal-2026-10-12.md`; no authorization shortcut was added.

## Spec Change Log

- 2026-10-11 (human-approved renegotiation): "apply recommended" accepted EventStore 6.6's full CLR `EventTypeName` plus `PayloadVersion` contract for Story 12.1. The previous frozen URI/always-stamped requirement and external-release acceptance path are superseded; all durability, authorization, replay, and failure acceptance criteria remain in scope. See `story-12-1-eventstore-6-6-decision-2026-10-11.md`.

## Review Triage Log

## Verification

**Commands:**
- `dotnet build Hexalith.Folders.slnx -c Debug` — builds against accepted EventStore API.
- `dotnet build tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj -c Debug` — build durable lane; run focused xUnit v3 assemblies and inspect persisted state.

**2026-10-11 observed:** The EventStore 6.6 checks and Folders baseline build are recorded in `story-12-1-eventstore-6-6-acceptance-2026-10-11.yaml`. After the source edits, `dotnet build Hexalith.Folders.slnx -c Debug -m:1 -v:q` passed with zero warnings/errors. The focused Server.Tests and IntegrationTests builds also passed. Direct xUnit v3 checks passed: `FolderEventReplayTests` 11/11, `ServerEndpointRegistrationTests` 6/6, `FolderLifecycleReplayDeterminismTests` 17/17, `ArchiveFolderProcessWiringTests` 22/22, `AspireTopologyTests` 14/14, and `GovernanceCompletenessGateTests` 23/23. `git diff --check` passes for tracked source, tests, and our planning edits; the whole-tree form is affected by a pre-existing user-owned trailing space in `sprint-status.yaml`. Earlier `--no-restore` solution attempts failed on inconsistent dependency assets; the normal solution build restored and passed. The deployed-path acceptance rows remain open as described above.

**2026-10-12 observed:** `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj -c Debug -m:1 -v:q` passed with zero warnings/errors; `dotnet tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests.dll -class Hexalith.Folders.Server.Tests.FolderEventReplayTests` passed 13/13; `dotnet build src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj -c Debug -m:1 -v:q` passed with zero warnings/errors. The deployed-path acceptance rows remain open.

**2026-10-12 deployed-path checkpoint:** The owner approved the command-bound receipt proposal. EventStore now signs a short-lived receipt from verified human JWT claims after command admission and carries it through the actor invocation; Folders checks both the EventStore workload and the receipt before the A6b gate. Focused receipt codec (4/4) and verifier (3/3) tests pass. A fresh, task-scoped Redis run accepted `BootstrapGlobalAdmin`, `CreateTenant`, and `AddUserToTenant` through the normal EventStore/Tenants path. The first keyed Folders probe used an unregistered short command type and correctly returned `idempotency_admission_unavailable` (503); the registered full command type passed admission. The run exposed two pre-existing deployed wiring gaps: Workers and Server held separate in-memory tenant-access projections, and Workers subscribed to `system.tenants.events` while EventStore published `tenants.events`. Both hosts now use the EventStore SDK read-model store for that projection, and the topic and production pub/sub scopes are aligned. The rerun observed three Workers `/tenants/events` deliveries (HTTP 200) and a persisted tenant-a projection with the caller as `TenantOwner`; Folders `/process` then returned HTTP 404 at the independent effective-permissions gate. The deployed host still uses an in-memory effective-permissions read model with no current `organization_baseline` grant, so no accepted folder event or persisted folder end state has been proven. EventStore currently surfaces that downstream safe denial as HTTP 500. Restart/replay, two-replica conflict, timeout, and leakage evidence remain open. The isolated Aspire stack was stopped, task-scoped Redis removed, and local Dapr component files restored. Relevant AppHost, Server.Tests, Workers.Tests, and Contracts.Tests builds passed; focused Server endpoint tests 6/6, Workers tenant-event tests 11/11, and production Dapr policy tests 9/9 passed.

**2026-10-12 authorization continuation:** The owner approved `story-12-1-organization-baseline-handoff-proposal-2026-10-12.md`: first initialization requires current `TenantOwner` membership plus explicit `manage_organization_acl` permission in the verified human receipt; accepted organization events derive the shared effective-permissions view. A tenant role or JWT claim alone does not grant `create_folder`. The code now routes initialization and grant/revoke through the EventStore actor and checks the view against the actor's current sequence before authorization. The final deployed-path checks remain in progress.

**2026-10-12 deployed ACL and replay probe:** A fresh, task-scoped Redis/Aspire run accepted normal Tenants bootstrap, returned a metadata-only 404 for `CreateFolder` before any grant, accepted `InitializeOrganizationAclBaseline`, and then accepted a `FolderCreated` event. EventStore did not persist a folder event or dead letter for the initial denial. Same-key replay returned its original 404 or 202, and a changed payload with the same key returned 409. Restarting the AppHost preserved the organization grant and accepted a new folder event. An initial `RevokeOrganizationAclPrincipal` probe exposed a same-actor timeout: Folders called `GetCurrentSequenceAsync` on the organization actor while that actor was waiting for Folders `/process`; the gateway returned 500. The handler now scopes the current sequence from the trusted `DomainServiceCurrentState` supplied by EventStore for organization ACL commands, while folder commands still query the actor through Dapr. The fresh rerun accepted revocation (202) with Workers paused; a folder request while the projection lagged returned metadata-only 503. After Workers advanced the durable permission view to sequence 4, the same permission returned 404 and neither denied folder acquired an event stream. A new grant advanced the view to sequence 5 and folder creation returned 202; restarting Folders then accepted another folder creation from the retained ACL view. Wrong-tenant and unauthenticated gateway probes returned 403 and 401. A denied response contained no tenant, folder, display-name, or projection identifiers. The task-scoped stack is still being used for final checks; two-replica conflict and timeout deployment evidence remain open.

**2026-10-12 replica and outage probe:** A separate isolated Aspire topology with two EventStore project resources, two Dapr sidecars using app ID `eventstore`, distinct gateway/sidecar ports, and the same task Redis accepted bootstrap, ACL initialization, and folder creation. Concurrent `CreateFolder` submissions to the two gateway addresses for one aggregate returned 202 and 422. The actor placed on one replica persisted exactly sequence 1 `FolderCreated` and sequence 2 `FolderCommandRejected`; no duplicate `FolderCreated` was stored. Dapr actor placement serialized these live invocations, so the live run did not cause an ETag conflict. The EventStore fault-injection suite exercised one retry after a concurrent winner and conflict exhaustion. Stopping Folders returned a generic metadata-only 500 without a folder event. Pausing the actual Folders process during an invocation returned the same generic 500 after 5.1 seconds; the body contained neither the folder ID nor display name, and no folder event was stored. The returned tenant ID was the caller's own tenant, not a protected lookup result. These outage results prove no mutation or existence disclosure; they do not prove a 503 transport for infrastructure failures. The temporary second replica, Dapr component port changes, and task Redis were removed after the probe.

**2026-10-12 authorization continuation verification:** `dotnet build src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:q` and the corresponding Server.Tests build passed with zero warnings/errors. `dotnet tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests.dll -class Hexalith.Folders.Server.Tests.EventStoreOrganizationEffectivePermissionsReadModelTests` passed 4/4, including same-actor head use and sequence-read timeout translation. The task-scoped live outcomes are recorded above; two-replica conflict and a deployed timeout injection remain to be verified.

**2026-10-12 final focused checks:** `FolderEventReplayTests` passed 13/13; `FoldersDomainServiceRequestHandlerTests` passed 16/16 after asserting the safe-denial transport wrapper and header; `ServerEndpointRegistrationTests` passed 6/6. The EventStore `AggregateActorInfrastructureFailureTests` passed 89/89, `SubmitCommandHandlerTests` 10/10, and `ConcurrencyConflictExceptionHandlerTests` 19/19. `git diff --check` passed in the Folders root and EventStore submodule. The isolated stack was stopped, the task Redis removed, and AppHost Dapr component files restored.
