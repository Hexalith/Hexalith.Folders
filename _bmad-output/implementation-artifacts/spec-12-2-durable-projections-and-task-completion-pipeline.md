---
title: 'Story 12.2: Durable projections and task-completion pipeline'
type: 'feature'
created: '2026-10-18'
status: 'in-progress'
baseline_commit: 'c22b13137202f7bf8c60b80c095a69748435bd6e'
route: 'dispatch'
review_loop_iteration: 7
approved_review_loop_iteration: 7
enrollment_baseline_commit: '91c0b89801a114962433f6dc6efd54d308d9a548'
context:
  - '_bmad-output/implementation-artifacts/epic-12-context.md'
  - 'docs/exit-criteria/c3-retention.md'
  - '_bmad-output/implementation-artifacts/proposal-12-2-acceptance-prerequisites.md'
  - 'docs/contract/authorization-matrix.md'
  - '_bmad-output/implementation-artifacts/proposal-12-2-current-sdk-security-enrollment.md'
  - '_bmad-output/implementation-artifacts/proposal-12-2-current-sdk-source-head-transport.md'
  - '_bmad-output/implementation-artifacts/proposal-12-2-mutation-evidence-deadline.md'
  - '_bmad-output/implementation-artifacts/proposal-12-2-live-tenant-freshness-prerequisite.md'
  - '_bmad-output/implementation-artifacts/proposal-12-2-foreign-task-scope-proof.md'
  - '_bmad-output/implementation-artifacts/review-12-2-round5-keep.md'
  - '_bmad-output/implementation-artifacts/proposal-12-2-bound-source-workload-authority.md'
  - '_bmad-output/implementation-artifacts/proposal-12-2-sdk-shutdown-stage-tracing.md'
  - '_bmad-output/implementation-artifacts/evidence/story-12-2-sdk-shutdown-scope-approval-20261114.json'
  - '_bmad-output/implementation-artifacts/review-12-2-round6-disposition.md'
  - '_bmad-output/implementation-artifacts/evidence/story-12-2-review6-continuation-approval-20261114.json'
  - '_bmad-output/implementation-artifacts/review-12-2-round7-disposition.md'
  - '_bmad-output/implementation-artifacts/evidence/story-12-2-review7-continuation-approval-20261114.json'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Production lifecycle/task readers remain in memory, losing status and terminal evidence after restart.

**Approach:** Materialize lifecycle, lock, workspace, cleanup, task and commit-status metadata from ordered events using EventStore projection/batch APIs. Share deterministic folding between live delivery and empty-checkpoint rebuild; persist completion before acknowledging delivery.

## Boundaries & Constraints

**Always:** Authorize before protected observation. Preserve existing routes, lifecycle vocabulary and event CLR identities. Derive task closure from source events: clean explicit release closes as `ready`, confirmed commit as `committed`; preserve failed/inaccessible evidence and approved recovery semantics. Deny subsequent non-replay writes for a closed binding using source-folded authority, never a projection. Keep unknown/reconciliation outcomes nonretryable and never infer provider success from acceptance.

**Approved prerequisite exceptions (2026-10-18):** The owner authorized the bounded provider binding/readiness, additive v1 task folder hint, and minimal SDK projection-mapping qualification/consumption described in [the prerequisite proposal](proposal-12-2-acceptance-prerequisites.md). These exceptions permit the required minimal SDK dependency scope; public v2 remains disabled.

**Approved SDK recovery prerequisites (2026-10-20):** The user approved [source-deadline expiry and pending scratch-fence recovery](proposal-12-2-sdk-expiry-and-scratch-recovery.md) for separate EventStore-owned implementation, qualification, and local source consumption here. These two capabilities extend the minimal SDK exception only as described in that proposal. No package or submodule pin update, publication, deployment, commit, push or public v2 exposure is authorized. The separate bounded owner-identity prerequisite retains its existing disposition.

**Approved current SDK enrollment (2026-11-01):** The user approved [the bounded Folders host-security migration](proposal-12-2-current-sdk-security-enrollment.md). Explicit human/channel/canonical endpoint classification, supported SDK security/catalog registration, authenticated root and historical health aliases, anonymous exact `/health`, `/alive`, `/ready` probes, and corresponding monitoring/runbook/test changes are authorized exceptions to preserving prior route authentication/probe behavior. Preserve domain handlers, layered authorization, canonical v1 outcomes, workload/channel/catalog/replay guards and disabled public v2. The prior minimal SDK capability authorization permits any necessary bounded public-composition repair in its owning repository; it does not permit bypassing the newer SDK boundary. No deployment, publication, package/gitlink update, commit, push or performance redesign is authorized.

**Approved native authority read-path performance (2026-11-02):** The user authorized performance work on the Tenants/EventStore native administrator source read path to complete Story 12.2's strict real-provider acceptance. Preserve every required source read, proof and final observation check, the existing five-second domain-service deadline, authorization and fail-closed behavior. This approval supersedes the prior performance-redesign exclusion only for that path; it does not authorize publication, dependency or gitlink updates, commit, push, public v2 exposure or weaker acceptance assertions.

**Approved bootstrap-path performance (2026-11-02):** After a strict run stopped at bootstrap, the user explicitly answered “Yes, optimize bootstrap too” to a request to optimize that command path. This permits bounded bootstrap-path performance work while preserving Dapr's existing five-second attempt limit, durable/catalog/security guards and every unchanged strict assertion. It does not authorize treating a failed HTTP response as accepted, changing admission semantics, or weakening source/projection proof.

**Approved bounded SDK shutdown investigation and correction (2026-11-14):** Jerome answered “approve” to [the concrete shutdown proposal](proposal-12-2-sdk-shutdown-stage-tracing.md), including its exact SDK and fixture patches. Apply and qualify the default-off SDK stage tracing, diagnose the actual stop wait, and implement the smallest internal coordination correction only after causal evidence and a failing/passing regression identify it. This supersedes the earlier generic SDK shutdown exclusion only for that bounded work. Preserve existing host-command budgets, resource restart order, retries, authorization, source proofs, exact lease/remote-call ownership and durable outcomes. General performance redesign and policy changes, publication, deployment, package/gitlink/pin changes, commits, pushes, public v2 exposure and weakened acceptance remain excluded. Prior pending-decision notes are historical; no repeated approval is needed for work inside this approved scope.

**Never:** Write Dapr/domain storage directly, invent provider/cleanup success, or replace retained event bytes. Exclude bodies and credentials. Keep transition evidence, diagnostics and search projections with Epics 4/6/10; content, provider commit execution, idempotency admission, confidentiality and destructive cleanup remain with 12.3–12.7/4.22. No deployment or public v2 exposure. Dependency changes are limited to the approved minimal SDK capability.

**Decisions (2026-10-20, Jerome; review round 4):**
- *Provider profile references:* a v1 `capabilityProfileRef` must name a host-configured profile (`Folders:ProviderBindings:Profiles`) for the authoritative tenant and provider kind. Otherwise configuration is rejected as an invalid policy and nothing is appended; an unknown reference never creates a profile-less binding. Hosts and test hosts that create bindings must configure profiles.
- *Expired operational status:* derived operational rows expire 400 days after their source event time, never after write time; rows already past that boundary are not written. A read past the boundary, or of a view whose rows expired together, returns the existing safe v1 not-found outcome (non-retryable) with no new contract value. A view missing while its evidence survives remains canonical unavailable. Source-folded closure authority still prevents task resurrection.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Delivery/replay | Ordered retained events | Equivalent per-workspace/task views, commit metadata and source checkpoints | Exact duplicates do not advance twice |
| Task closure | Clean release or confirmed commit | Durable terminal result; later new work denied | Equivalent replay retains original result |
| Conflict/crash | Gap, different bytes, concurrent writers, interrupted batch | No false completion or checkpoint advance; recover retained batch | Canonical conflict/unavailable result |
| Denial/storage | Foreign/revoked authority, corrupt/missing state, timeout | Safe distinction between absence, stale and unavailable | No protected disclosure or acknowledgment of failed persistence |
| Retention | Expired/rebuilt view, terminal source history | C3 status/cleanup metadata: 400 days; views: 400 days or rebuild | No resurrected task or content deletion |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs`: reuse mapping intent; remove fabricated provider references, zero lag and cleanup-success assumptions from production mapping.
- `src/Hexalith.Folders.Server/FolderEventReplay.cs`: reuse identity/order validation and governed evolution; named SDK projection input is already upcast; the protected legacy `/project` receives original stored bytes.
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs`: status registrations; workspace status includes commit outcome.
- EventStore `SharedProjectionEpochCoordinator`, `IAsyncDomainSharedProjectionStreamingRebuildHandler`, partitioned rebuild interfaces and `DomainSharedProjectionRebuildDispatcher`: select logical views, journal live deliveries, stream bounded operations and promote recoverable generations. Keep the SDK-owned manifest/receipt protocol and 100-operation/one-MiB limits; the domain supplies source folds and metadata only.
- `tests/Hexalith.Folders.AppHost.Tests/FolderDurabilityAcceptanceFixture.cs`: reuse authenticated isolated topology, store inspection, changed-PID restart and replay.


- Existing bounded SDK candidates (ordinary source files, not skills): `references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Projections/SharedProjectionEpochCoordinator.cs` exposes generation-selected reads/writes, durable journaling, streaming staging, commit and catch-up; `IAsyncDomainSharedProjectionStreamingRebuildHandler`, `IAsyncDomainSharedProjectionPartitionedRebuildHandler`, and `IAsyncDomainSharedProjectionEpochHandler` under `...DomainService/` expose SDK-owned bounded shared rebuild. `DomainSharedProjectionRebuildDispatcher.cs` consumes StreamFinalizedOperationsAsync via the coordinator. Ordinary single DomainProjectionRebuildPlan still promotes one batch and cannot hold 101 operations. Use the bounded SDK path and proper discovery for large histories; do not merely increase limits or disable retained manifests.
- Provider configuration already retains a semantic IdempotencyFingerprint: `OrganizationProviderBindingCommandValidator.Fingerprint` binds CommandType/tenant/organization/binding/provider/credential/naming/branch policy; it intentionally ignores actor tracing. Reuse that source semantic evidence for read validation, preserving its v1 meaning and raw event bytes.
- Known rejection intake must account for pre-creation source positions. `FolderDomainProcessor` emits UnsupportedCommandType/validation rejections before a FolderCreated anchor, and FolderCommandRejected.Create intentionally permits normalized unknown command names.
- Round-4 command path: EventStore awaits legacy `/project` inside every accepted command with the full 1..head stream (`SubmitCommandHandler.cs:499-505`, `TriggerProjectionUpdateAsync` logs failures and does not fail the command). `FoldersDomainServiceEndpoints.cs:132-150` currently runs a tenant-wide shared rebuild there through `src/Hexalith.Folders.Server/OperationalStatusRebuild.cs` (fresh operation id per call, no Abort). Its only other user is `tests/Hexalith.Folders.Tests/Projections/OperationalStatus/OperationalStatusRebuildFixture.cs`. SM4 (`prd.md:210`): command acknowledgement ≤1 s p95, status summaries ≤500 ms p95.
- SDK rebuild protocol (`references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/DomainSharedProjectionRebuildAction.cs`) has Verify and Abort; the dispatcher seals `SourceHighWatermarks` at Begin. `OperationalStatusProjectionHandler.cs:58-60` instead reads the live head during partition preparation.
- Workers: `FoldersWorkersModule.cs:190-216` runs provider, organization, folder-permission and operational stages inside `PermissionProjectionDelivery`'s one-second deadline (`PermissionProjectionDelivery.cs:11`) and runs semantic indexing only after all succeed; stage labels at :193-201,:213 are wrong.
- Retention: writer `OperationalStatusProjectionWriter.cs:135-138,239-241` (TTL on views only, fresh 400 days on terminal rewrites); reader `EventStoreOperationalStatusReadModel.cs:261-283` (expiry and known-missing throw → retryable 503); safe not-found already maps at `TaskStatusQueryHandler.cs:185`. `OperationalStatusCheckpoint.ObservedAt` is the latest source event time.
- Clock checks: `OperationalStatusEventReader.cs:73` compares Folders-host `OccurredAt` against EventStore `Timestamp`; reader :261,:280 compare stamps against the local clock with no tolerance.
- Provider profiles: `ConfigureProviderBindingService.cs:83-91` already enforces the strict decision; keep it. `ProviderBindingProfile.cs:11,13` default to the shared `OrganizationProviderBindingPolicy.Empty`, which configuration binding mutates. Bindings are write-once (`OrganizationAggregate.cs:120-133`). Profile drift throws at `ConfiguredProviderReadinessBindingReader.cs:20-37` and maps to retryable `retry_later` at `ProviderReadinessValidationService.cs:132-138`. `tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs:844,959` send `profile-a` without configuring it; `FolderDurabilityAcceptanceFixture.cs:575` shows the env-var profile shape. Operator docs: `docs/operations/provider-integration-and-testing.md`, `docs/operations/retention-and-tenant-deletion.md`; doc gates assert raw substrings, so append rather than reword pinned lines.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Folders/Projections/OperationalStatus/OperationalStatusFold.cs` and adjacent single-type files: implement deterministic per-workspace/task history, metadata validation, source sequence/digest, schema and event-time freshness. Persist all views/checkpoints through SDK batches with recovery and collision-safe tenant keys.
- [x] `src/Hexalith.Folders/Queries/Folders/EventStoreOperationalStatusReadModel.cs`: implement existing lifecycle/lock/workspace/cleanup/task interfaces, including commit status; validate stored identity and rebind request evidence after authorization.
- [x] `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs`, `FolderStateApply.cs`, `FolderAggregate.cs`: derive closure evidence from existing events and enforce it after authorized equivalent-replay handling. Retain historical task results when later workspace state changes.
- [x] `src/Hexalith.Folders.Workers/FoldersOperationalStatusEventProcessor.cs` and `FoldersWorkersModule.cs`: compose version-aware operational delivery into the existing authenticated subscription before acknowledgment; failed/indeterminate writes remain retryable.
- [x] `src/Hexalith.Folders.Server/OperationalStatusProjectionHandler.cs` and `FoldersServerHostComposition.cs`: register SDK named replay/rebuild and production readers; share the fold/batch writer without changing legacy projection responses.
- [x] `tests/Hexalith.Folders.Tests/Projections/OperationalStatus/OperationalStatusFoldTests.cs`, corresponding Server/Workers tests, and `tests/fixtures/event-evolution/`: cover the matrix, retained event-name/version inventory, every applicable upcaster chain and closed missing-version entry. Explicitly inventory an empty historical set rather than inventing history.
- [x] `tests/Hexalith.Folders.AppHost.Tests/OperationalStatusDurabilityAcceptanceTests.cs`, its `README.md`, and `_bmad-output/implementation-artifacts/evidence/story-12-2-durability.json`: prove persisted state, restart, rebuild, two concurrent replica processes, crash recovery, denials, storage faults and retention boundaries; record revisions/commands/results.

- [x] Approved provider prerequisite: replace production unconfigured target resolution/process-local readiness-binding source with the durable owner-approved binding/readiness path; qualify a real disposable supported remote and authenticated preparation/lock/clean-release flow without injecting provider success.
- [x] Approved task prerequisite: add optional v1 `folderId` target hint and its contract/consumer binding; preserve existing task claim checks and establish fresh layered folder read authority before the folder-scoped task lookup. Missing scope fails closed; public v2 remains disabled.
- [x] Approved SDK prerequisite: add/qualify and consume the minimum SDK-owned selective named projection/rebuild mapper, preserving workload/replay guards and existing process/query/replay-state surfaces. Work from the root-declared EventStore repository, preserve its current HEAD/gitlink, and do not initialize nested submodules. No publication or remote operation.


- [x] Review correction: `OperationalStatusCheckpoint.cs`, `OperationalStatusFold.cs`, and adjacent single-type evidence helpers: retain canonical source-derived integrity evidence for checkpoint state and every workspace/task snapshot; reject invalid enums, malformed closures/positions, altered historical workspace rows, and stale/nonterminal task rows paired with a newer checkpoint. Preserve original source timestamps and permissible older rows after pure rejections.
- [x] Review correction: `OperationalStatusProjectionWriter.cs`: rebuild source-derived terminal metadata completely; compare/correct CurrentState, operation/failure/retry/freshness/evidence fields instead of skipping all rows with a matching terminal token and operation. A successful repair must restore an available truthful result.
- [x] Review correction: `OperationalStatusFold.cs`: consistently select the original earliest task closure by retained sequence across all workspace bindings, reads, live slices, complete rebuild, and JSON round trips; later binding closure must not replace or conflict with the original task result. Keep domain write guards per workspace/task binding.
- [x] Review correction: `OperationalStatusEventReader.cs`, `OperationalStatusCheckpoint.cs`, and fold: require each persisted message identity to occupy exactly one sequence; exact same-position duplicates remain idempotent. Reject undefined numeric rejection codes. Test named SDK intake as well as Worker delivery.
- [x] Review correction: `OperationalStatusProjectionHandler.cs`, protected legacy rebuild composition, and/or writer: establish the authoritative current folder stream head through the existing SDK sequence reader before publishing a complete-prefix rebuild; truncated input with an empty checkpoint cannot return successful completion. Do not introduce a local stream reader or direct Dapr access. Unit planning can use an explicit supplied head; production always resolves actual authority. (Superseded in round 4: completeness binds to the SDK-sealed capture, and the legacy route no longer rebuilds.)
- [x] Review correction: GitHub readiness tests: use the existing RecordingGitHubApiClient seam to assert preparation capability support and denial from actual mapper output and successful readiness consumption when required permissions are present.
- [x] Review correction verification: cover damaged terminal repair, old dirty/current unknown task race, malformed checkpoint state/closures, altered historical workspace, repeated TaskId across workspace closures, repeated message at another position, undefined rejection code, and truncated rebuild. Rebuild and re-run the individual verification projects and real provider acceptance on final source; append evidence without overwriting prior stages.


- [x] Capacity correction: `Projections/OperationalStatus/` plus Server/Workers/readers: use existing SDK bounded generation/partition/rebuild and reader/writer-selection seams so a complete rebuild with at least 98 historical tasks and a 10,000-position history does not exceed a recoverable batch's 100 operations or one-MiB canonical manifest. Keep those SDK limits and recoverable-manifest guarantees intact; do not impose a new task/history product limit, drop historical required views/evidence, or add a local platform protocol.
- [x] Bounded evidence correction: keep each persisted operational checkpoint/receipt/view-evidence value bounded independently of event and historical task/workspace count. Retain original source digest/message identity, exact duplicate/conflicting-position handling, every historical view's full integrity evidence, earliest terminal result, safe known-missing distinction and source-owned per-binding closure authority. Reuse SDK logical storage/partition/generation facilities; domain mutation authority remains in the actor's source fold. Qualify read selection, live catch-up and crash/concurrent recovery through the SDK's existing protocol.
- [x] Prefix correction: permit a valid known rejection-only prefix before FolderCreated to persist source evidence without publishing any fabricated lifecycle/workspace/task view. A later single-event FolderCreated at its real next position must converge with complete replay. Unknown-command rejections are legitimate retained events (FolderCommandRejected.Create deliberately normalizes them); do not strand those sources with a command-name allowlist.
- [x] Source integrity correction: verify provider Binding fields against the retained source-derived semantic fingerprint (the existing OrganizationProviderBindingCommandValidator fingerprint includes provider/credential/policies), or equivalent canonical source evidence. Altered credential or policy metadata must fail before private credential/provider use. Cover known cross-tenant and cross-provider host-profile mismatches through ConfigureProviderBindingService and assert no configuration append.
- [x] Protocol correction: task folder denials must use existing v1 CanonicalErrorCategory values and the appropriate safe 401/403/404/503 shape. Preserve the original task claim plus existing metadata grant; add actual endpoint schema assertions for these denial branches without widening v1's error enum.
- [x] Source fold correction: a WorkspaceLockReleased event must match the retained holder as well as its task/lock identity before clearing a lock or closing a task. Reject an Accepted result masquerading as rejection while preserving legitimate UnsupportedCommandType/unknown-command rejection histories. Cover no checkpoint/view advancement on malformed inputs.
- [x] Metadata correction: scrub unused command-key/fingerprint fields still nested in checkpoint state (repository binding, archive and BranchRefPolicy) and assert serialized sentinel absence. Preserve original retained source bytes and domain actor idempotency authority.
- [x] Repair correction: a protected complete-current-source rebuild must conditionally repair a damaged derived operational checkpoint without undocumented manual deletion. Validate complete authoritative source, preserve conflict rejection for contradictory valid source anchors, and publish repaired views/checkpoint through SDK concurrency/generation controls. Do not use damaged checkpoint content as authority. (Round 4: only the SDK-driven named transport performs this rebuild.)
- [x] Retry correction: concurrent provider-binding rebuild/delivery or an advancing source head must yield canonical retryable conflict/unavailable behavior, rather than HTTP 422 projection_unreadable. Add overlapping persistence/rebuild coverage.
- [x] Discovery correction: expose the operational named/shared rebuild capabilities through the SDK-owned metadata factory/mapping and real production metadata endpoint; qualify ordinary discovery/enrollment and the selected bounded SDK rebuild transport. Preserve bespoke process/query/replay-state/legacy routes and workload/source/replay guards. Local SDK edits qualify/expose the minimal projection mapping/discovery seams and correct the existing owner captured-prefix comparison required for that same rebuild capability; reuse existing bounded SDK protocols, public APIs, limits and storage schemas.
- [x] Retention evidence correction: qualify an otherwise valid view immediately before the inclusive 400-day boundary and at the boundary using the existing controlled clock. Live timestamp-corruption alone qualifies integrity failure, not expiration; correct that claim and verify the real persisted retention/rebuild boundary through task-owned state/SDK evidence without altering retained events, production clocks or shared limits.
- [x] Final correction verification: add executable capacity-boundary, rejection-before-create, provider-corruption/profile-mismatch, holder mismatch, canonical-denial, corrupt-checkpoint repair, discovery and concurrency regressions. Rebuild the Debug/source solution, run the affected projects individually and real provider acceptance on final assemblies, then append evidence preserving all previous exact-source stages. No publication, package pin change, remote operation or public v2 exposure.

- [x] SDK qualification correction: `references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Projections/SharedProjectionEpochCoordinator.cs` and focused SDK regression tests: preserve existing sequence/position/CAS and complete-vector validation, verify every previously captured event identity against the matching prefix of the newly complete captured vector, and conditionally extend an identical shorter prefix through the existing CAS path. Reject contradictory prefixes, malformed/truncated identity vectors and stale/conflicting requests. Qualify the real-Dapr reproduction: rebuild prefix 1, ordinary catch-up through 8, rebuild full 8, then duplicate replay and further catch-up. This is the minimum internal correctness repair necessary to qualify the already-approved named/shared rebuild capability; no public API/protocol/schema/limit/pin/publication change or local workaround. Rebuild and requalify exact source/binaries after correction.

- [x] Review-three completion/repair: `OperationalStatusProjectionWriter.cs` and persistence/capacity regressions: validate all required preserved current views on fresh rejection and the historical views required by a duplicated original event, using bounded SDK logical evidence and original source identity. Missing/corrupt required views cannot be acknowledged. Repair both selected and inherited missing derived checkpoints from complete-current source through SDK controls, preserving valid source conflicts and earliest terminal/history semantics.
- [x] Review-three provider evidence: `ProviderBindingProjection.cs`, `EventStoreProviderReadinessBindingReadModel.cs` and service/Worker/rebuild tests: bind the selected latest configured Binding to its original configured-event sequence/digest and canonical full configuration evidence; coherent older Binding cannot be accepted beside newer receipts. Authoritative complete-current source may conditionally repair malformed derived configuration; valid contradictory source anchors still reject. Recheck source head after conditional rebuild persistence and return retryable concurrency on advancement. Reject Accepted/undefined rejection codes before provider receipts advance.
- [x] Review-three actual target readiness: Forgejo readiness/provider and workspace preparation seams/tests must observe the exact authorized bound repository metadata/required permission before advertising usable WorkspacePreparation and appending WorkspacePrepared. Reuse existing target resolver/HTTP binding/observation seams where applicable. Generic authenticated-user/version flags alone are insufficient. Deleted/inaccessible/wrong target or denied permission cannot append prepared success; keep private context outside source/projection/evidence and preserve unsupported/create/bind discovery behavior. Cover real target denial in the disposable-provider lane.
- [x] Review-three direct source corrections: reject null/blank identifiers for every workspace-bearing retained event and invalid acquisition/effective/expiry/lease consistency before persistence; do not change lawful retained identities. Confirmed reconciliation to committed clears superseded failure metadata while preserving earliest source-owned closure. Add red/green durable reader/Worker/named-intake regressions for these demonstrated cases.
- [x] Review-three verification gaps: source→persisted durable reader→public lifecycle query tests pin repository failed/unknown/reconciliation vocabulary; normal authenticated Worker route tests assert operational checkpoint/views before successful acknowledgement and a retry response on injected operational persistence failure. Keep current real opted-in acceptance strict.
- [x] Task 36 / approved SDK bounded owner-identity consumption: verify the exact separately implemented EventStore candidate against its source/binary packet before building; replace the obsolete expected-refusal diagnostic with positive qualification through the production named/shared dispatcher and existing Folders handler. Use Debug project references, supported AccumulatePart/AccumulateComplete transport and MaxRebuildEventCount=40000. Prove complete 10,001/32,769-position histories, further growth/repeated rebuild, historical duplicate/conflicting identities, multiple original owners, catch-up, concurrency, inherited generations, interrupted promotion/process replacement. Reuse independently bound SDK coverage explicitly where appropriate. Preserve original histories, identity/continuity/CAS/generation checks and 100-operation/one-MiB limits. Run relevant consumer regressions and authenticated live acceptance; retain exact commands/results, loaded identities, hashes, PIDs and cleanup in a new packet. Keep SDK broad-regression failure, package/PD10, SM4 and independent-review gates distinct; whole story stays in-progress.

- [x] Round-four command path (replace, do not patch): remove the operational branch from legacy `/project` in `FoldersDomainServiceEndpoints.cs` and delete `OperationalStatusRebuild.cs` with its fixture. `/project` returns the legacy response regardless of operational state. Workers live delivery is the only live writer; rebuild and repair run only through the SDK-driven named `/project/rebuild/shared/v1` transport, which owns session Begin/Abort/Verify. Move tests that relied on `/project` repair to that transport.
- [x] Round-four named rebuild: in `OperationalStatusProjectionHandler.cs`, validate each partition history as a contiguous 1..N prefix bound to the SDK-sealed capture. Never compare it with a live head; appends after capture reach the new generation through SDK catch-up.
- [x] Round-four Workers: in `FoldersWorkersModule.cs`, run the operational stage after the permission/provider chain, outside the one-second deadline. Run semantic indexing even when the operational stage fails, then return 500 so the delivery retries. Indexing stays idempotent on redelivery. Label and log each stage by its own name.
- [x] Round-four retention (frozen decision): writer TTL = source event time + 400 days − now for each view, its evidence and that position's receipt/message rows. Skip writes already past the boundary; terminal rewrites keep the original expiry. The bounded checkpoint keeps no TTL. Writer: a duplicate older than retention is acknowledged without writes, and a required view whose view and evidence are both absent counts as expired, not missing. Reader: return not-found for age ≥ 400 days or a view whose view and evidence are both absent (lifecycle may use checkpoint `ObservedAt`). Evidence without a view stays unavailable.
- [x] Round-four clock skew: remove the `OccurredAt > Timestamp` rejection in `OperationalStatusEventReader.cs:73`. Allow one fixed five-minute tolerance constant for stamp-versus-local-clock checks in the reader.
- [x] Round-four provider fixes:
  - `ProviderBindingProfile.cs`: initialize fresh empty policies so bound profiles never share or mutate `OrganizationProviderBindingPolicy.Empty`.
  - Profile drift: map to the non-retryable `fix_provider_configuration` remediation and log it, using a dedicated internal exception.
  - Provider `/project` rebuild: reject Accepted or undefined rejection codes, as Workers does (`FoldersProviderBindingEventProcessor.cs:46-50`).
  - Provider rebuild race (`EventStoreProviderReadinessBindingReadModel.cs:104-108`): throw `PermissionProjectionConcurrencyException`.
  - `/project` 503 branch: log the exception type, and classify `ProjectionDispatchValidationException` with the 422 branch.
  - Missing folder hint (`FoldersDomainServiceEndpoints.cs:5544-5550`): report `retryable:false`.
- [x] Round-four profile decision wiring: configure a `profile-a` test profile for the tenant and provider in the `ArchiveFolderProcessWiringTests` host so its three ConfigureProviderBinding tests pass unchanged in intent. Document the required `Folders:ProviderBindings:Profiles:<ref>` section, and the rejection of unknown references, in `docs/operations/provider-integration-and-testing.md`. Document the expired-status not-found outcome and the named rebuild for bootstrap and stalled folders in `docs/operations/retention-and-tenant-deletion.md`.
- [x] Round-four tests:
  - [x] Accepted profile path: service output consumed through `ConfiguredProviderReadinessBindingReader`.
  - [x] Two bound profiles stay isolated and `Empty` stays empty.
  - [x] Forgejo bound-target plumbing and `ConfiguredProviderRepositoryTargetResolver` mapping/rejections.
  - [x] `/project` returns the legacy body and writes no operational rows.
  - [x] Named rebuild persists rows; interrupted retained scratch safely retries or Aborts without an active session, then a fresh named rebuild succeeds. The approved SDK qualification covers real Dapr, fresh/populated selections and exited writer/recovery OS processes; current Folders consumer cases and the full live run pass.
  - [x] Workers route persists the binding projection, and its bytes equal the provider `/project` rebuild.
  - [x] A throwing readiness reader yields `provider_binding_authority_unavailable`.
  - [x] Production-host registration and options binding for the configured provider reader/resolver.
  - [x] Retention: boundary, all-expired, evidence-only and duplicate-after-retention.
  - [x] Clock skew within and beyond tolerance.
  - [x] Indexing still runs after an operational failure.
- [x] Round-four performance: use one static `JsonSerializerOptions` in `EventStoreOperationalStatusReadModel.cs`. In the opted-in real-Dapr acceptance, record p50/p95 for the command acknowledgement, the Workers operational delivery and the status read against SM4. If over budget, record it as an open finding; do not redesign.
- [x] Round-four artifacts:
  - Restore the anchors dropped from `epic-12-context.md` (409/exit 77/`concurrency_conflict`, 24-hour/seven-year replay retention, 422 `confidential_operation_not_durable`, readiness dependency health, Story 4.23 dependency) from `prd.md`, `epics.md` and `architecture.md`.
  - Correct the stale prose in `proposal-12-2-acceptance-prerequisites.md:49-51` and `tests/Hexalith.Folders.AppHost.Tests/README.md:248-249`: stage five, 3,908 cases, and the local unpushed SDK commit `d4d05f871991ec2af305267f59dcefeae3f127ed` on gitlink `84bb115a0ced2497ca4315644a677b273cb44edf`.
  - Align the sprint-status line-3 comment date with `last_updated`.

**Approved enrollment continuation:**

The first five rows are complete on the current source graph, with broad contract failures recorded separately. The [current intake packet](evidence/story-12-2-latest-eventstore-20261101T122955Z/README.md) supersedes the earlier source-build and signed-command blockers. Full live qualification remains open; earlier packets retain their exact-source dispositions without claiming whole-story completion.

- [x] Approved current SDK enrollment: `FoldersServerHostComposition.cs`, `FoldersServerServiceCollectionExtensions.cs` and the owning EventStore public composition seams: adopt supported security/catalog registration while retaining the bespoke keyed processor and process/query/legacy projection handlers. Apply the public canonical-route convention to custom callbacks; never register internal SDK types, forge guard metadata or bypass catalog checks. If a bounded public SDK registration repair is necessary, implement/verify it in the root-declared EventStore repository under the existing approved minimal capability scope, without nested submodule or pin changes.
- [x] Approved current SDK enrollment: explicitly classify all observed human v1/content/provider/console/audit endpoints and `/` with the SDK named bearer-only human policy, Dapr discovery/subscriptions with the actual-token channel policy, and canonical callbacks with their catalog policies. Preserve layered domain authorization, safe v1 errors and zero protected observation/effects on denial; mixed and wrong-kind credentials fail closed.
- [x] Approved current SDK enrollment: `src/Hexalith.Folders.ServiceDefaults/Extensions.cs`, tracked health-path consumers, `deploy/observability/production/observability.yaml` and affected runbooks/tests: expose exact anonymous `/health`, `/alive`, `/ready`; preserve existing health aliases as authenticated endpoints and existing readiness checks. Update monitored paths and verify both Server and Workers. No deployment.
- [x] Approved current SDK enrollment: production Server/Workers security tests and existing callback/routing suites: require both SDK inventories to be empty; exercise actual catalog guards, positive flows, every route group, exact probes, aliases/root, actual sidecar tokens, mixed/wrong-kind credentials, weak canonical overrides and late unclassified endpoints through production pipelines. Preserve strict source/task/replay assertions.
- [x] Approved current SDK enrollment: build isolated Debug/all-project-reference runtime with consistent assembly identities; execute affected Server, Workers, Integration, Contracts, health/observability and operational histories individually. Requalify affected 10,001/32,769 production-handler histories when source/runtime changes prevent exact reuse. If SDK public registration changes, run affected owner registration/security regressions. Record broad failures separately rather than weakening assertions or substituting test-only production bypasses.
- [x] Current-source live composition correction: `src/Hexalith.Folders.EventStore/Program.cs` and `tests/Hexalith.Folders.EventStore.Tests/FoldersEventStoreHostCompositionTests.cs`: consume the current SDK gateway's supported HTTP host enrollment, framework channel conventions/actor callback metadata, projection-notification convention and explicit human documentation/hub policies. Mirror the owning SDK host composition; retain Folders idempotency adapters, safe denials, commands and receipt/admission semantics. Prove empty public inventories and real host startup/credential separation. No internal registration or bypass.
- [x] Current-source live composition correction: `src/Hexalith.Folders.AppHost/Program.cs` and affected topology/composition tests: bind the already-composed Tenants project and its own sidecar to one SDK-generated per-run app-channel token, as the current public Tenants helper requires. Use the existing public token API; preserve resource/store isolation, real workload/human authentication and strict live assertions/deadlines. Never retain generated tokens in evidence.
- [x] Approved current SDK enrollment: run the owner-approved strict authenticated real-provider durability acceptance on final audited runtime; retain commands/results, source/runtime identities, failed attempts, SM4 measurements and exact owned-resource cleanup in a new evidence packet. Preserve all prior packets and distinguish this migration's completion from whole-story SDK broad-regression, Release/package/PD10, SM4 and independent-review gates. The only strict-body exception is the exact approved two-probe patch qualified on2026-11-04.

**Review-five correction tasks (current):**

- [x] `src/Hexalith.Folders/Projections/OperationalStatus/OperationalStatusCompaction.cs`,
  `OperationalStatusFold.cs` and adjacent projection-only helpers: make persisted current fold state
  bounded for valid native lease and content/file histories as well as legacy/rejection histories.
  Do not retain unbounded primary content/publication/file/erasure/commit/released-fence dictionaries
  in the checkpoint. Preserve necessary current operational state and lawful subsequent native/file
  delivery; simply clearing data needed by the next primary Apply is not a correction. Keep primary
  FolderState/command authority, source events, native release/complete distinctions and SDK limits.
  Qualify at least4000native acquire/release cycles and distinct file/publication history, continued
  live delivery, complete rebuild and actual mutation/value bounds through the existing SDK path.
- [x] `OperationalStatusCompaction.cs`, `OperationalStatusFold.cs`,
  `OperationalStatusProjectionWriter.cs` and persistence/retention tests: preserve original task
  closure/expiry authority across compaction and physical C3 row/evidence expiry. A later unrelated
  valid event must not recreate an expired terminal task as fresh/nonterminal. Live catch-up and
  complete-source rebuild must agree for legacy and native completion, earliest closure and reused
  task/binding histories. Use existing SDK logical evidence/selection/batch facilities and bounded
  domain metadata; no local platform protocol, retained-byte replacement, status TTL extension,
  dropped history, new product cap or primary source-guard change. Add the demonstrated401-day
  clean-release→paired expiry→FolderAccessGranted regression and boundary/replay/growth controls.
- [x] `OperationalStatusFold.cs:ValidateWorkspaceEvent` and adjacent validation helpers: validate
  supported native acquisition metadata before folding/persistence, including nonblank lock/task
  identities, positive consistent lease and ordered effective/expiry values. Follow actual lawful
  native acquire/renew/release/complete producer semantics; do not blindly impose legacy-only field
  equalities on another operation. Malformed source must neither publish views nor advance a
  checkpoint/acknowledgment. Qualify direct writer, named intake and valid native continuation.
- [x] `src/Hexalith.Folders/Queries/Folders/EventStoreOperationalStatusReadModel.cs` and reader/query
  tests: positive source-head lag must precede an unproved missing-task/workspace conclusion.
  Checkpoint8/head9 plus an absent new task must be canonical stale/unavailable, not404. Preserve
  frozen safe absence for positively expired views and paired absent rows at a current checkpoint;
  surviving evidence without its view remains unavailable. Keep authorization before protected
  observation, exact folder keys, source proof/zero distinctions and all existing canonical outcomes.
- [x] `OperationalStatusProjectionWriter.cs`,
  `src/Hexalith.Folders.Server/OperationalStatusProjectionHandler.cs` and existing rebuild tests:
  observe the already-supplied cancellation while enumerating source/materialized operations and
  serializing chunks, without a new public signature or changing uncanceled canonical output.
  Demonstrate cancellation arriving during admitted large-history work stops that work without
  false completion or weakened SDK Abort/recovery/continuity/CAS behavior.
- [x] `tests/tools/Task36FoldObserver/LifecycleRequestCorrelation.cs` and
  `tests/Hexalith.Folders.Server.Tests/Task36LifecycleRequestCorrelationTests.cs`: reconcile an
  unidentified/unmatched end at a confirmed drain, so available snapshots contain no phantom active
  requests. Incomplete/contended observations must stay explicitly unavailable until drain, then
  later observations can recover. Add deterministic actual contention/non-waiting, poisoned snapshot,
  drain/recovery and unmatched/unavailable end tests. Preserve existing framework Activity controls,
  opaque-only privacy, advisory/nonblocking behavior, bounds and all lifecycle/shutdown limits.
- [x] Run affected focused Domain/Server/Workers/observer/source-scanner and pending-inventory checks
  serially with both source flags and Debug assets. Refresh only changed/new compile inputs in the
  pending inventory; preserve dated PD10 and every sealed packet. Hand off exact phase patches,
  guards/results and released outputs. Parent owns final runtime/Memories/source/PDB/assets audit,
  current consumer/matrix/capacity checks and unchanged d29 real-provider qualification. Preserve the
  prior396.332s pass as exact old-source evidence, not a pass for modified source. Package/SM4 and
  independent review remain separate; no publication/pin/deployment/Git operation is authorized.

**Review-six corrections — one additional cycle approved 2026-11-14:**

- [x] `EventStoreOperationalStatusReadModel.cs`, bounded domain evidence and relevant read/persistence
  tests: reject a coherent older task/workspace row plus its matching older evidence when contradicted
  by current source-derived authority. The parent reproduction restores the sequence-7 locked task
  pair after a sequence-8 ready closure and receives Available/locked/nonterminal. An old valid
  receipt proves a former value, not its present applicability. Reuse SDK logical storage,
  generation selection and bounded domain metadata; preserve lawful older rows after pure rejections,
  historical tasks/workspaces, original earliest closure, authorization before observation and the
  frozen paired-absence/400-day outcomes. Do not equate every view watermark with the current
  checkpoint: unrelated positions may legitimately leave a view unchanged. Qualify coherent rollback,
  current unknown/terminal outcomes, legitimate historical reads, pure rejections, source-head lag,
  missing/corrupt authority, selected/inherited generations and authoritative source repair.
- [x] `OperationalStatusProjectionWriter.cs` and durable persistence regressions: never preserve a
  prior terminal row based only on its ready/committed token when its integrity evidence is absent.
  The parent reproduction forges LastOperationId, removes evidence and sends a lawful next policy
  event; current code acknowledges Completed and writes new proof for forged-operation. Trust valid
  prior evidence or restore the row from existing validated source-derived completion; otherwise fail
  closed for controlled source repair. Retain exact earliest closure, fields, source timestamps,
  original expiry and current valid conflict guards. Add the demonstrated red/green regression.
- [x] `FoldersRejectedCommandClassifier.cs`, its production permission-stage callers and actual
  Workers subscription regressions: permit lawful normalized unknown-command rejection positions
  from FolderDomainProcessor to traverse permission/nonpermission processing and operational
  persistence. The current classifier throws before operational intake for UnsupportedCommandType.
  Determine owner kind from validated source scope while retaining known wrong-kind/cross-tenant
  refusals, invalid rejection-code/version checks and zero protected effects on denial. Test the
  actual authenticated subscription: unknown-command rejection-only prefix, later creation/delivery,
  replay and malformed/wrong-owner controls; acknowledgement requires durable operational state.
- [x] `OrganizationAclOperationalSource.cs`, `OperationalStatusProjectionHandler.cs`, adjacent finite
  source recognizers and named/shared production-handler tests: validate all legitimate non-Folder
  prefixes reaching the domain-wide folders route before returning no Folder operations. A valid
  provider-binding partition currently throws Unknown operational source event; an organization
  FolderCommandRejected-only prefix throws InvalidFolderId, and mixed ACL/rejection histories fail.
  Cover configured/rejected provider-binding streams and accepted/rejected organization ACL prefixes
  through live and bounded named/shared intake, including complete rebuild and catch-up. Preserve
  exact owner/contract/version/position/source-binding validation, safe malformed/foreign refusal,
  unchanged original bytes and no fabricated Folder/provider result.
- [x] EventStore-owned `NamedProjectionDispatchCoordinatorTests.Shutdown.cs`: independently hold the
  actual final lifecycle completion after successful dispatch when ApplicationStopping fires. The
  existing test only holds the earlier Retryable-admission branch. Restoring None at the successful
  cleanup site must fail the new assertion. Preserve caller-independent cleanup, actual-task await,
  retained unknown catalog fence, original retry work and both qualified SDK shutdown corrections.
- [x] Rebuild affected Debug/source projects with normal analyzers and both source flags, version
  5.0.1 and story12-2-qualification suffix; refresh only current mutable compile inputs and run its
  controls. Run affected domain/Server/Workers/SDK regressions individually, with current matrix and
  meaningful capacity/source/retention coverage. Requalify the final coherent Root/Memories producer,
  current issuer component cases, relevant package/contract gates and unchanged strict acceptance;
  retain exact input-based reuse and limits rather than silently reusing modified producers. Preserve
  all three protected strict bodies, deadlines, restart order, all failures and SM4 record-only scope.
  Release every owned process/container/scratch and provide new metadata-only evidence for parent
  diff/matrix/identity verification and the following independent review.

**Review-seven corrections — one additional cycle approved 2026-11-14 (current):**

- [x] `src/Hexalith.Folders/Queries/Folders/EventStoreOperationalStatusReadModel.cs`,
  `src/Hexalith.Folders/Projections/OperationalStatus/` and persistence/read regressions:
  establish independently source-bound current applicability for historical nonterminal,
  native-release and failed tasks. Restoring an older coherent row/evidence/applicability triple
  after task B must not return task A's obsolete locked state. Use existing SDK/source authority
  with bounded records/checkpoints; no self-attesting extra copy, unbounded checkpoint map or
  retention extension. Cover actual native release, failed transitions, historical reads,
  generation inheritance/interleaving and authorized complete-source repair.
- [x] `OperationalStatusProjectionWriter.cs` and durable duplicate regressions: validate original
  retained task completion/source authority for every historical required view before returning
  AlreadyCompleted. Reproduce task A closure, task B advancement, rollback of A's preclosure triple
  and redelivery of A's original acquisition. The writer must not acknowledge a required view the
  durable reader finds unavailable. Preserve lawful older rows, paired absence and positive expiry.
- [x] `src/Hexalith.Folders.Server/FolderEventReplay.cs`, actor-owned Folder/Organization folds and
  source-reconstruction tests: accept normalized unknown-command rejection prefixes using validated
  owner scope, consistently with operational intake. Cover subsequent ordinary commands for each
  supported owner; retain known wrong-owner, malformed scope, code, version and position refusals.
- [x] `src/Hexalith.Folders.Workers/FoldersProviderBindingEventProcessor.cs`, folder-permission intake
  and actual authenticated subscription tests: remove remaining known-command-only rejection
  divergence after validated owner classification. Provider/organization accepted and lawful unknown
  rejection prefixes must agree between live and named processing and permit subsequent valid
  configuration, with no fabricated provider/Folder output or durable acknowledgement on refusal.
- [x] `OperationalStatusProjectionWriter.cs`, bounded source identity validation and C3/named rebuild
  regressions: reject reuse of an old MessageId at a new source position after its 400-day derived
  receipt expires. Reconcile live and complete-source reconstruction through existing SDK/source
  authority; absence of expired evidence never proves uniqueness. Preserve original expiry,
  bounded metadata and SDK limits; do not add an unbounded domain ledger or local storage protocol.
  If the existing public SDK cannot supply the required authority, finish independent corrections
  and report the exact missing capability and concrete owner prerequisite without weakening behavior.
- [x] Apply the three surviving patches: compare retained checkpoint-tail MessageId/EventDigest
  before the expired-replay shortcut in `OperationalStatusProjectionWriter.cs`; consistently reject
  Accepted/Created/IdempotentReplay/AlreadyApplied as rejection codes in `FoldersRejectionSource.cs`
  and relevant callers; add authenticated organization-owner unknown rejection prefix, duplicate,
  persisted LastSequence and next-sequence baseline coverage in
  `tests/Hexalith.Folders.Workers.Tests/WorkersOrganizationAclRouteTests.cs`.
- [x] Requalify affected Debug/source runtime with normal analyzers and the existing both-source
  flags, version 5.0.1 and story12-2-qualification suffix. Run focused red/green regressions first,
  affected consumer/SDK/capacity/issuer/package/inventory gates individually, and unchanged
  tracing-off strict real-provider acceptance after current producer/source/PDB preflight. Bind
  reused evidence explicitly to unchanged inputs. Audit all five frozen matrix rows, preserve
  failures/SM4 record-only results and all three protected strict bodies, release owned
  processes/containers/scratch and retain new metadata-only evidence for independent parent review.

- [x] Current qualification cancellation correction: `PermissionProjectionCatchUp.cs` must request
  cancellation of its owned deadline on exhaustion before disposing it. The current actual-model
  diagnostic observes four of 72 unavailable outcomes with both source tokens still uncanceled
  and both final drain signals absent. Preserve the two-second enabled budget, every existing
  outcome/source check, caller behavior and actual read ownership; qualify exact-source red/green
  controls, the unchanged permission class, ordinary Server suite and final producer/runtime.


**Acceptance Criteria:**

- Given authorized source events, when delivery, duplicates, restart and empty-checkpoint rebuild run, then all six status concerns converge with truthful freshness and unchanged retained hashes.
- Given clean release, when the real authenticated path closes a task, then terminal evidence survives restart and later non-replay mutation is denied. Given retained commit-success evidence, when folded, then status reports `committed`; real provider execution remains Story 12.4.
- Given conflicting, unsupported, denied or failed input, when processing runs, then neither view completion nor checkpoint falsely advances; reads return canonical metadata-only outcomes.
- Given C3 boundaries, when views expire/rebuild, then terminal authority survives and legal-hold/cleanup ownership is preserved. Fake-only, skipped or unavailable lanes cannot establish completion.
- Given concurrent accepted commands on different folders of one tenant, when EventStore calls legacy `/project`, then each call returns the unchanged legacy response, starts no shared rebuild session, and Workers delivery converges operational status.
- Given a v1 provider configuration whose profile reference is unknown, or names another tenant's or provider's profile, when processed, then it is rejected as an invalid policy with no append. Given a matching configured profile, when processed, then it is accepted and readiness consumes that profile.
- Given an operational view whose source event time is at least 400 days old, when read or redelivered, then reads return the safe v1 not-found and no row is written past the boundary. Given evidence without its view, when read, then the outcome is canonical unavailable.
- Given a Folders host clock up to five minutes ahead of EventStore, when its events are delivered, then they are accepted and status stays available.

## Implementation Notes

- Local verification uses Debug/source references: `-p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false`. The isolated Aspire baseline reached healthy Folders/Workers/EventStore/security and was stopped through the exact-target lifecycle. Release/package qualification is separate.
- Operational live delivery journals original source identity through the SDK tenant/family `operational-status` epoch coordinator, catches up retained work, and verifies selected persisted completion before acknowledgment. Schema-2 checkpoints contain only scrubbed current state, the tail receipt/message and rolling source digest. Position/message receipts and full canonical evidence for each historical workspace/task occupy separate SDK logical records. The actor's complete source fold retains independent per-binding terminal authority.
- Rebuild uses the SDK's existing copy-on-write generations, partitioned streaming, staging and promotion; the recoverable 100-operation/one-MiB batch limits are unchanged. The 10,000-position/98-historical-task regression passed with a bounded checkpoint, preserved historical reads, duplicate handling and subsequent catch-up. Complete current-source rebuild repairs corrupt derived checkpoints through SDK generation controls while rejecting contradictory valid source anchors. A valid rejection-only prefix publishes no fabricated views and converges when FolderCreated arrives.
- Production readers use SDK-selected multi-key reads and canonical source-derived row evidence, then rebind current authorized request tracing. Known missing/corrupt rows fail closed and expired rows follow the round-4 retention decision; valid historical rows after pure rejections remain readable. Earliest original task closure is stable across later bindings/rebuild, and malformed holder/message/rejection evidence cannot advance completion. Legacy workspace actor snapshots without closure fields fail closed until complete source replay.
- The optional v1 `folderId` task hint preserves `read_task_status` and establishes fresh layered folder `read_metadata` authority before lookup. Denials use the active v1 canonical categories and safe 401/403/404/503 shapes. Public v2 stays disabled.
- Provider configuration/readiness is durable at its exact source head, including rejection positions. Its fields are checked against the original retained semantic fingerprint before private profile enrichment or credential resolution. Host profiles retain private repository/ref/base-URL context outside source/projection bytes; mismatched tenant/provider or changed profile digest fails closed. Rebuild/delivery CAS overlap and advancing source heads remain retryable without replacing newer configuration.
- Metadata preparation appends WorkspacePrepared only after real provider metadata readiness and final fresh authorization; Requested remains intent. No clone/content/provider-commit/cleanup success is inferred. Source-confirmed commit events qualify committed status folding; provider execution remains Story 12.4.
- The minimal selective projection and metadata mappers are implemented in the root-declared EventStore working tree. A keyed folders alias resolves the same scoped bespoke processor, enabling genuine SDK discovery and the protected `/project/rebuild/shared/v1` transport. Orphan handlers do not gain capabilities. Existing workload/replay guards and process/query/replay-state/legacy routes are preserved. EventStore HEAD/gitlink stays `84bb115a0ced2497ca4315644a677b273cb44edf`; this task performed no dependency update, Git mutation, remote operation, CI or publication. External commits advanced the root to `efb98b1e5215ad73b4591f2a2a8ff00f8b8c794a`, updated the Builds catalog to EventStore 3.125.1 and formatted three owner-read test files before the qualified builds/tests. They are preserved and audited separately in the current evidence stage. The four uncommitted SDK changes still require separately authorized Release/package qualification and any required release/pin update.
- Current live acceptance stages a sealed private SDK streamed generation before promotion, changes four real host PIDs, resumes that same session and verifies ordinary authenticated replay. It claims no live mid-install kill. Two additional distinct Workers receive overlapping original retained positions in a fresh shared fixture-owned derived-state prefix; automatic pubsub is denied for those proof resources. Retained events and SDK receipts are preserved. Component tests additionally cover batch installation/commit/compaction/receipt faults.
- Retention qualification separates a controlled clock's one-tick-before/inclusive 400-day boundary from actual selected Redis TTL observations after ordinary writes and rebuild. Live timestamp mutation checks integrity only. C3 legal-hold/destructive-cleanup ownership is unchanged. The fixture-only Forgejo transport logging filter does not qualify general production confidentiality/redaction (Story 12.7).
- Historical PD10 v1 generator input, the dated conformance manifest and disabled v2 candidate preserve their exact bytes/hashes. Current v1 additive contract checks pass; the separate current-source inventory explicitly records pending v2 requalification and supplies no approval/exposure authority.
- Historical stage-four component checks passed 3,877 tests, with two pre-existing optional Domain skips; source/SDK/runtime/test builds passed with zero warnings/errors. Seventeen exact SDK copies are qualified in two matching groups (eight DomainService, nine Client) by 92 mapper and 107 shared-projection cases. Uninstrumented real-provider acceptance passed 1/1 with zero failures/skips in 310.346 seconds. All 30 tasks and five frozen matrix rows have executed passing coverage. Stage four in [the evidence record](evidence/story-12-2-durability.json) records the current source, external commits, assemblies, commands and cleanup; its three historical stages remain unchanged. Review three found the corrections below; current acceptance is open until re-derivation, requalification and review finish.
- Current task authority maps stale/unavailable folder evidence to canonical v1 503 without changing the original retryability decision or observing a task row. Fixture readiness observations wait for existing source/ACL/operational heads; they do not qualify a platform admission fix. Two concurrent Workers compare all permission semantics exactly while bounding processing-time ProjectedAt separately; operational original timestamps and source hashes remain exact. Failed acceptance attempts are retained as diagnostics only.

- Review-three completion validation now binds every original position/message receipt to its source-folded workspace/task view identifiers and a canonical integrity digest. Missing legacy binding evidence, altered binding evidence, or missing/corrupt preserved and historical required views fail closed. Complete-current source repairs those derived rows, including a checkpoint inherited from another owner generation, while preserving original source/message identities.
- Review-three provider validation binds each configuration to its original configured-event sequence/digest and canonical full value. Authoritative rebuild conditionally repairs corrupt derived configuration and rechecks the actual source head after saving. Forgejo preparation resolves and observes the exact authorized repository and its required permission before claiming preparation capability. The real disposable-provider test revokes and restores collaboration for the same nonadmin observer credential and private repository; the denied attempt retains ProviderPermissionInsufficient and no preparation events, using the existing safe public v1 tenant_access_denied category.
- Review-three direct-source validation rejects missing workspace identifiers and malformed acquisition intervals before delivery completion. Confirmed reconciliation clears superseded commit failure metadata; the regression pins the nested provider outcome success category and was shown to fail when that correction alone was temporarily removed. Durable reader/public lifecycle and normal authenticated Worker route tests now assert the demonstrated outcome vocabulary and operational persistence acknowledgement gate.
- Historical stage-five source qualification passed 3,908 component tests with two pre-existing optional Domain skips; the real provider acceptance passed 1/1, zero failures/skips, in 311.385 seconds. Current fixture helpers and changed-file whitespace/analyzer checks pass. Exact commands, final assembly/source hashes, logs, XML, independent diagnostic attempts and confirmed topology cleanup are retained in stage five of [the evidence record](evidence/story-12-2-durability.json); all four prior stages remain canonically unchanged. The private test runner is retained for review, with no active topology.
- At stage five, Tasks 1–35 were implemented and qualified within their then-approved scope; the round-four corrections below require current-source qualification. Task 36 and full Story 12.2 acceptance remain open: ordinary growth to 10,001 succeeds, but the actual production shared dispatcher cannot complete that history because the public SDK owner-capture API rejects its exactly sized identity vector. The focused public call pins ArgumentException/capturedEventDigests independently of the generic dispatcher failure. [The proposed generic bounded owner-identity prerequisite](proposal-12-2-sdk-bounded-owner-identity.md) records the missing SDK capability without new approval, API/protocol/schema changes, relaxed limits or a domain history cap. Independent review and separately authorized SDK release/package consumption also remain open.

- Parent verification independently checked all 130 source hashes, seven test assemblies, all 17 SDK copies and runtime copies, current XML/log hashes and summaries, six TTL observations, distinct concurrent Worker PIDs, eight recorded process replacements, privacy sentinels and post-run cleanup. [The independent qualification record](evidence/story-12-2-parent-qualification.json) confirms 3,908 component passes and the 311.385-second live pass while retaining full_acceptance=false for the proposed SDK prerequisite. No additional SDK scope approval is recorded.

- Round-four implementation uses Workers for live operational persistence and only the SDK named transport for operational rebuild/repair. Partition folding binds to SDK-sealed original positions and validates the contiguous owner prefix without a live-head query. Legacy `/project` keeps the old response and provider projection repair. Independent profile objects, strict profile test-host wiring, typed profile-drift remediation, provider rejection/race handling, stage-specific logs, five-minute local clock tolerance and a shared reader serializer are implemented.
- Round-four retention planning writes source-deadline TTLs for views, integrity evidence and original position/message receipts, keeps the bounded checkpoint, skips expired rows and acknowledges expired duplicates without writes. A receipt remains until the latest original source time used by that position's dependent views, including a host-ahead closure; this preserves original timestamps. Paired physical row/evidence expiry is safe absence through SDK generation reads; surviving evidence without its row stays unavailable.
- Historical stage-six retention blocker (superseded for current source by the approved SDK qualification below): the SDK seals relative TTLs in `ReadModelBatchOperation`/`SharedProjectionMutation`, then reapplies them during delayed staging/compaction (`ReadModelBatchProtocol.cs:621–625,661–665`; `DaprReadModelBatchStateAccessor.cs:99–102`). The focused `OperationalStatusRetentionTests.DiagnosticStagedSdkManifestRetainsRelativeTtlAcrossSourceDeadlineAsync` stages a correct one-day remaining TTL, crosses the source deadline with a controlled clock, then proves the SDK commits the old one-day TTL. Fresh Folders planning omits those expired rows and reads remain source-age bounded, but physical retention after delayed recovery needs a separately authorized EventStore absolute-expiry capability. The retained manifest protocol, SDK scope and Task 36 disposition are unchanged.

- Interrupted retained scratch-batch acceptance remains open. `OperationalStatusCapacityTests.DiagnosticRetryAndAbortCannotReconcileSdkPendingScratchFenceAsync` injects `BeforeCommit` during SDK `TryWriteScratchAsync` (`SharedProjectionEpochCoordinator.cs:622–634`). Initial Accumulate and same-action retries return `Accumulating/Indeterminate/projection_handler_failure`; Abort also returns that outcome and the durable epoch remains Building without selected rows. `AbortAsync` restores the retained pending scratch batch before `RequireOperation` (`SharedProjectionEpochCoordinator.cs:2261`), which rejects its restored phase; `DomainSharedProjectionRebuildDispatcher.FencedAbortAsync:2842` reaches the same failure. This is diagnostic evidence, not passing interrupted-batch acceptance. `NamedRebuildAbortsPreDispatchFailureWithoutPublishingRowsAsync` separately proves a clean pre-dispatch failure can abort and then rebuild; it does not substitute for retained-batch recovery. A generic SDK pending-fence recovery capability is required; the existing protocol is unchanged.

- Round-four live diagnostics preserve the strict HTTP 403 bound-target denial requirement. Two initial runs received HTTP 202 after `idempotency_admission_unavailable`/`DaprApiException` retries and failed; their disposable stores were removed before retained command-status inspection, so no terminal outcome is claimed for those attempts. The diagnostic retry directly observed the leased nonadmin credential receiving HTTP 404 with pull=false after revocation, then received the required 403, retained `ProviderPermissionInsufficient`, and no preparation-success events. SDK `SubmitCommandHandler.cs:130–133` can return a bare admission receipt for Pending without executing the domain; this is a possible explanation for the earlier 202s, not an observed classification of their destroyed state. A bounded read-only diagnostic hook now reports retained command status and event types/codes before the unchanged assertion if it recurs.

- Stage six records the final Debug/source build (zero warnings/errors), 4,193 component passes with the two existing optional Domain skips, and the final opted-in real-provider acceptance pass (1/1, zero failures/skips, 299.439 seconds). All prior five evidence stages remain canonically unchanged. Final command acknowledgement p50/p95 was 251.065/31,330.090 ms (9 successful requests, including request retries), so SM4 acknowledgement latency remains an open finding against 1,000 ms. Workers callback p50/p95 was 119.751/452.781 ms (26 samples, 1,000 ms diagnostic budget); public status read was 30.082/45.861 ms (12 samples, 500 ms budget). The earlier diagnostic live pass was under budget; the final over-budget observation is retained without redesign. Full acceptance remains open for Task 36, source-deadline expiry through delayed SDK recovery, retained scratch-batch retry/Abort recovery, SM4 acknowledgement latency, package consumption and independent review.

## Spec Change Log

- 2026-11-14 current review-seven qualification: the unchanged permission cancellation case fails
  at its final **two-second source-drain wait**, line 172, after the five-second pending wait and
  unavailable/reason assertions have completed. Two ordinary full Server runs and one serialized
  diagnostic retain this failure; the focused nine-case class passes. A private actual-model
  reproduction uses the existing budgets and records four of 72 exhausted results with both
  actual source input tokens cancelable but uncanceled and both stop signals absent; all stores
  remain unchanged, and diagnostic cleanup starts only after outcome capture. The parent authorized
  the smallest owned-deadline cancellation correction within existing source-call ownership and
  verification requirements. This is a cancellation correctness repair, not a deadline, authority,
  performance-policy or review-loop change. Preserve all failures, source/PDB bindings, current
  iteration 7 and both independent source-authority prerequisites.

- 2026-11-04 review-five loopback (`review_loop_iteration: 5`): every final finding from the three
  independent layers is recorded separately in Review Triage Log. Reproduced native history makes
  a306073-byte checkpoint exceed the existing256KiB SDK value bound; malformed native acquisition
  advances/persists; parent probing confirms compacted closure loss recreates an expired task as
  fresh/nonterminal after an unrelated grant. Missing-view lag precedence is also made explicit.
  These are bad nonfrozen implementation instructions, not new intent. Added seven actionable
  correction/qualification tasks for projection-specific bounded state, closure/expiry preservation,
  native source-shape checks, freshness ordering and surviving cancellation/observer/test patches.
  KEEP is [recorded separately](review-12-2-round5-keep.md), with source copies/hash inventory under
  `/tmp/story12-2-foreign-scope-20261104-s76ae741/round5-keep`. Affected production paths are already
  unchanged from preserved current HEAD, so there is no uncommitted production implementation to
  revert; re-derive the affected portions in place without reverting committed/user work or restoring
  old planning/evidence. Preserve the exact d29 strict body, captured intent/frozen block, all
  deadlines/guards, correct native release/complete semantics, SDK protocols/limits and sealed pass.
  Modified code requires fresh source/runtime and live qualification. Lower non-patch entries remain
  moot on this loopback; the two unverified performance claims are not silently promoted to defects.
  No publication, pin, deployment, commit, push or additional capability approval is recorded.

- 2026-10-18: Jerome explicitly approved [the bounded acceptance prerequisites](proposal-12-2-acceptance-prerequisites.md): production provider binding/readiness, an additive folder hint for the existing v1 task-status route, and qualification/consumption of the minimal selective SDK projection mapping capability. These three exceptions extend the original boundary; public v2, provider commit execution, content storage, idempotency admission, confidentiality and destructive cleanup remain excluded. No deployment, dependency publication, commit or push was authorized.


- 2026-10-18 review loop 1: non-frozen implementation instructions now explicitly require complete checkpoint/view integrity, successful repair of damaged terminal rows, consistent earliest task closure selection, message-position uniqueness, and current-head complete-prefix rebuild. Existing intent/matrix already require truthful corrupt/stale/conflicting behavior; no intent or public surface change was needed. KEEP: preserve the qualified deterministic six-concern fold, original event bytes/name/version inventory, source-owned per-binding closure guards and equivalent-replay precedence, original observation times, opaque keys, SDK batch/CAS retained-manifest recovery with fresh invocation IDs, lifecycle-first/checkpoint-last anchors and all existing receipts, real private-profile/provider preparation path, layered v1 task authority, disabled v2/frozen PD10 provenance, and the existing real-provider/replica/restart/fault harness. Reference snapshot for positive preservation: `/tmp/story12-2-review-keep-psf24zv_/README.md`; implementation may reuse successful code from its `approved-working-tree` after checking it against these corrections. Never restore the snapshot's planning/evidence files over the current spec/triage. Prior acceptance remains evidence for its exact source hashes only.


- 2026-10-18 review loop 2: clarified non-frozen bounded checkpoint/rebuild design, source-only pre-creation prefixes, controlled checkpoint repair and normal SDK discovery. Concrete SDK limits exposed valid-history failures at 98 tasks and 10,000 positions; the previous small-history acceptance remains exact-source evidence, not a scale claim. KEEP all prior confirmed invariants, canonical full-row integrity, earliest closure, true source-head checks, workspace commit isolation, named/workload guards, paired-source SDK scope and real-provider harness. Positive preservation reference: `/tmp/story12-2-round2-keep-pjl9t7sg/README.md`. Reuse existing SDK epoch/partition/streamed-generation/read/catch-up seams, with domain-owned metadata only; no new task limit, discarded required history, relaxed SDK limits, disabled recovery manifests, generic local protocol, or extra dependency/platform API scope. If these existing SDK seams cannot supply the required bounded atomic publication/read selection, report the exact missing public capability and safe concrete prerequisite rather than inventing a local substitute. Preserve all three recorded evidence stages and current spec/triage.

- 2026-10-18 SDK qualification: the exact real-provider Commit exception was captured privately at SharedProjectionEpochCoordinator.SeedCapturedOwnerSequenceAsync:454. A real-Dapr public-SDK reproduction with unchanged original DTOs proves that prefix rebuild 1 followed by ordinary growth through 8 leaves OwnerSequenceNumber=8 with one captured identity, then rejects a valid full-8 rebuild. The minimum internal prefix-extension correctness repair is necessary to qualify the existing approved SDK named/shared rebuild capability; it adds no independent platform feature or public protocol. This implementation clarification stays within the existing bounded SDK qualification/consumption authorization; no new human approval is recorded. Preserve all original source identities and contradiction/CAS guards. No SDK publication, package pin or remote operation is authorized.

- 2026-10-19 review loop 3: all14 findings were individually verified/triaged above. Amend non-frozen instructions for preserved/historical completion validation, inherited checkpoint repair, source-bound latest provider configuration and conditional repair/head races, actual bound-target readiness, demonstrated malformed source/reconciliation metadata and default-lane assertions. The production SDK hard10,000-position identity cap is an unresolved prerequisite, not a domain product limit or a reason to disable identity evidence. KEEP all positive invariants in `/tmp/story12-2-round3-keep-hvjso36v/README.md` and all four historical exact-source evidence stages. Safe source snapshot: `/tmp/story12-2-round3-keep-hvjso36v/approved-working-tree`. Preserve external root commits `5e05e9d15f392cd4634a4718568cbc55daf8e8db` and `efb98b1e5215ad73b4591f2a2a8ff00f8b8c794a` and the original baseline; never restore snapshot planning/evidence over current artifacts. Requalification is required after changes; no new owner approval, SDK protocol/schema/public surface or release operation is recorded.

- 2026-10-19 SDK prerequisite approval and review routing: Jerome approved [the bounded owner-identity prerequisite](proposal-12-2-sdk-bounded-owner-identity.md) as separate EventStore-owned work, built in an EventStore repository story and session, not from Folders. Task 36 stays open. It is blocked on that work landing, being qualified for consumption and being requalified here; no Folders-side workaround or history cap is authorized. Jerome chose to run review round 4 on the committed round-three corrections (`0fa612f1f435bb580e28230ade0d86f6df5973a8` on main) without a new implementation pass, since Tasks 1–35 have no outstanding work. The SDK changes reviewed are the unpushed local EventStore commit `d4d05f871991ec2af305267f59dcefeae3f127ed` against gitlink `84bb115a0ced2497ca4315644a677b273cb44edf`. KEEP all prior change-log constraints and evidence stages.

- 2026-10-20 review loop 4 (`review_loop_iteration: 4`):
  - **Triggers.** Two intent gaps: v1 profile references (verification-other) and the expired-view outcome (blind-3/4, edge-8/9). Both were resolved by Jerome and recorded as frozen decisions: strict profiles, and safe not-found after source-time 400-day expiry. Bad-spec findings: per-command tenant rebuild in `/project` (edge-3), a rebuild session that was never aborted (blind-12, edge-2), comparison against the live head during rebuild (edge-16), indexing gated on operational success (edge-13) and unmeasured cost (blind-22, edge-12).
  - **Amended.** Code Map, the round-four tasks and acceptance criteria, annotations on the two superseded rebuild tasks, and one implementation note.
  - **Known-bad state avoided.** Every command rebuilt the whole tenant in the awaited path, leaving 503s, stuck Building epochs and blocked deliveries. Idle entities returned a retryable 503 forever. Unknown references silently created bindings that could never resolve a repository.
  - **Re-derivation basis (Jerome, 2026-10-20).** Start from the committed HEAD `b49b0b206238056b3aa3c963edc44b1b6fc0f5cc` as the KEEP reference, without a physical revert. Rewrite only the parts built on the superseded design (Folders-driven rebuild in `/project`, `OperationalStatusRebuild.cs`, the live-head check, Workers stage ordering). Apply the round-four patches in place. Edge-30 is resolved by deleting that code.
  - **KEEP.** Every invariant from earlier change-log entries. The deterministic six-concern fold and original event bytes and identities. Bounded checkpoints and SDK logical evidence. Earliest closure and per-binding source authority. Rejection-only prefixes. Provider semantic fingerprints and source-bound repair. Real bound-target readiness. The task folder hint and canonical denials. The named SDK discovery and mapper. All five evidence stages and the parent qualification record. Task 36 stays open, blocked on the approved EventStore owner-identity work. No SDK protocol, schema, limit, pin, publication or public v2 change.

- 2026-10-20 SDK expiry/recovery approval: the user responded “I approve” to the concrete prerequisite proposal. Record the two bounded capabilities as approved EventStore-owned work and qualify their local source consumption afterward. Historical diagnostics remain unchanged; full story acceptance stays open for the existing remaining gates.

- 2026-11-14 parent review six: current source/runtime/matrix verification and the strict pass are retained. Four runtime correction areas plus the successful-dispatch shutdown test gap remain; all 14 findings are triaged before grouping. The next loop exceeds the rendered limit, so no code is reverted or re-derived pending the human decision.

- 2026-11-14 review-six continuation: the user answered yes to one additional correction/review cycle beyond the five-loop guard. Iteration 6 is authorized once; preserve its counter and the original baseline. Re-derive affected functions in place under the logged KEEP constraints, without a physical rollback of approved work. The four runtime correction areas and successful-dispatch shutdown test patch are specified above; captured intent/frozen matrix stay unchanged. A further bad-spec/intent-gap loop is not implicitly authorized.

- 2026-11-14 review-seven continuation: Jerome answered "approve" to the concrete request for one
  additional correction/review cycle under [the round-seven plan](review-12-2-round7-disposition.md).
  [The approval receipt](evidence/story-12-2-review7-continuation-approval-20261114.json) authorizes
  iteration 7 once; preserve review_loop_iteration=7 and the original c22b baseline. Seven current
  tasks above address five separate demonstrated authority/adoption defects, the three surviving
  patches and exact-source qualification. Re-derive only affected functions in place, with no
  physical rollback of committed/user work. KEEP every round-seven preservation instruction,
  frozen intent/matrix/body hash, bounded SDK protocol, original C3 deadline, source bytes,
  authorization and prior successful shutdown correction. Avoid coherent historical rollback,
  false duplicate acknowledgement, legitimate unknown-prefix refusal and post-expiry MessageId
  reuse; add no unbounded ledger or self-attesting authority. A missing SDK capability requires a
  precise owner prerequisite, not a local substitute. Content/12.3 remains separately owned.
  No release/Git/public-v2/performance authority or further loop exception is inferred.

## Review Triage Log

| Round / finding | Verdict | Route | Verified evidence |
| --- | --- | --- | --- |
| 1 / blind-1 metadata catalog | false | reject | The mapped SDK endpoint invokes the identity-options DispatchAsync overload (DomainProjectionDispatcher:318), which recomputes the local catalog directly; it does not invoke the registry overload cited by the reviewer. Missing discovery may limit a future operator feature, but does not make current named dispatch unusable. |
| 1 / blind-2 terminal repair | medium | bad_spec | OperationsAsync skips a ready/committed row based only on terminal state and operation. A row with those two values intact and damaged CurrentState or freshness is preserved by a successful rebuild; the durable reader then rejects it indefinitely. Rebuild must repair full source-derived terminal metadata. |
| 1 / blind-3 unsuccessful task parity | medium | bad_spec | TaskEvidence accepts every no-closure snapshot except successful terminal tokens, without checking current state/operation/retry metadata. A saved old or altered task row can be accepted with a newer checkpoint; task status may advertise retry during an unknown provider outcome. |
| 1 / blind-4 checkpoint state validation | medium | bad_spec | Valid checks the event receipt chain but never lifecycle enum validity or closure/position correspondence. Those fields can be changed without changing EventDigests/SourceDigest, then used by folds and reads. Validate the source-folded checkpoint and its derived evidence before use. |
| 1 / blind-5 historical workspace parity | medium | bad_spec | MatchesCurrentWorkspace returns true as soon as a workspace is historical. The prefix receipt digest is independent of row fields, so a modified historical lock/status/cleanup/provider row passes. Retain source-derived evidence for every materialized workspace, including historical bindings. |
| 1 / blind-6 closure ordering | medium | bad_spec | Task materialization takes the first FrozenDictionary closure for TaskId while reads order by original closure sequence. Multiple workspace bindings can reuse a task and dictionary enumeration is not chronological authority. Use original earliest closure consistently and preserve each binding's independent source guard. |
| 1 / blind-7 reused message identity | medium | bad_spec | Folder permission delivery rejects message reuse upstream, but the newly named SDK handler has no equivalent guard and accepts contiguous distinct events sharing MessageId. Persist and check message-position identity for every operational intake. |
| 1 / blind-8 future upcaster digest | false | reject | The closed executable inventory establishes that all currently retained payloads are v1 and there are no applicable upcasters or historical generations. Stored and effective bytes are identical in every supported lane. The claimed conflict requires a future payload evolution that is absent from this change and must qualify its provenance when introduced. |
| 1 / blind-9 checkpoint size/cost | maybe-false | defer (moot on loopback) | Dictionary copying and validation are linear per delivery, and the SDK has finite manifest/rebuild bounds. A supported stream size or measured latency/failure threshold was not established. A realistic maximum-size history benchmark against the SDK limits and a defined supported folder capacity would settle the claimed user impact; potential medium severity remains unverified. |
| 1 / blind-10 checkpoint retention | false | reject | The finding treats retained source-fold authority as a refreshed status result. Checkpoints preserve closure identity needed to prevent resurrection, while public status reads use the original event/closure timestamps and reject the inclusive 400-day boundary. C3 explicitly allows source rebuild of views; it does not expire the authoritative event fold or authorize deleting completion authority. No old result becomes fresh through checkpoint TTL renewal. |
| 1 / blind-11 incomplete rebuild | medium | bad_spec | PrepareRebuildAsync checks an existing prefix but not the authoritative stream head. Named SDK legacy history validation checks contiguity only; an empty checkpoint plus a truncated current stream can return Completed. Require the actual source head at the production complete-prefix rebuild boundary before publishing. |
| 1 / edge-1 old dirty task | medium | bad_spec (group with blind-3) | ReadAsync fetches the task row before checkpoint/head and permits a lower watermark. A concurrent update can pair the dirty retryable row with a later unknown-provider checkpoint, and TaskEvidence accepts it. Validate source-derived task metadata against the checkpoint generation. |
| 1 / edge-2 numeric rejection code | medium | patch (survives loopback) | Enum.TryParse accepts undefined numeric strings such as 999. OperationalStatusEventReader therefore accepts a malformed rejection after a valid folder anchor. Require a defined result code before advancing any checkpoint. |
| 1 / verification-1 GitHub preparation coverage | medium | patch (survives loopback) | Pre-verified gap: GitHubReadinessMapper adds preparation capability, but GitHub discovery tests never assert that row or its readiness consumer; generic tests fake rows and the live lane uses Forgejo. Use RecordingGitHubApiClient to cover the permission-dependent row and readiness result. |


| 2 / blind-1 rebuild operation limit | high | bad_spec | Lifecycle + one workspace + 98 historical tasks + checkpoint is 101 operations. ReadModelBatchProtocol.ValidateLimits hard-rejects retained manifests above DefaultMaxOperations=100, even when options are raised. Valid repeated lock/release tasks can reach this history, leaving rebuild unavailable after view loss. |
| 2 / blind-2 receipt manifest limit | high | bad_spec | Verified minimal JSON for 10,000 digest/message pairs is 1,097,821 bytes (1,463,764 base64), before state/view/manifest overhead. Recoverable manifests are hard-limited to 1,048,576 bytes. This new concrete capacity evidence settles the earlier unverified growth concern; current checkpoint layout eventually blocks valid delivery. |
| 2 / blind-3 task denial contract | medium | patch (survives loopback) | The new task branch delegates to FolderAuthorizationDenialMapper, which emits not_found_to_caller/policy_denied/authorization_denied; these are absent from the active v1 CanonicalErrorCategory enum. Map these new branches through the task's existing canonical problem shape without widening the contract. |
| 2 / blind-4 released holder | medium | patch (survives loopback) | A release with the current lock ID and mutually matching TaskId/HolderTaskId for another task passes the new fold's validation, then clears the original lease and closes the wrong task. The named intake does not run the command authorization guard; validate the retained holder before applying. |
| 2 / blind-5 rejection classification | medium | patch (survives loopback) | Accepted is defined and currently passes rejection parsing, so malformed rejection evidence can advance. The proposed unknown-command allowlist is rejected: FolderCommandRejected.Create explicitly preserves normalized unknown-command rejections from UnsupportedCommandType. Preserve those lawful positions while rejecting the demonstrated success-as-rejection case. |
| 2 / blind-6 provider binding integrity | medium | patch (survives loopback) | Current-head/receipt checks do not bind Binding.CredentialReferenceId or policy fields to source semantics; changing only the credential ref passes and reaches credential resolution. The existing retained command fingerprint already binds those fields and can support a direct correction without a new public protocol. |
| 2 / blind-7 checkpoint command keys | low | patch (survives loopback) | Scrubbing only IdempotencyFingerprints leaves RepositoryBindingIdempotencyKey, ArchiveIdempotencyKey and BranchRefPolicy.IdempotencyKey serialized. These are unused by operational materialization and contradict its deliberate command-key omission; directly remove the copied fields and assert sentinel absence. |
| 2 / blind-8 corrupt checkpoint repair | medium | bad_spec | PrepareRebuildAsync rejects every malformed stored checkpoint before a valid complete-current source can replace it; reads/live folds reject it too. A controlled rebuild must repair this derived corruption using actual source authority and conditional publication, preserving valid-prefix conflict guards. |
| 2 / blind-9 provider rebuild concurrency | medium | patch (survives loopback) | PermissionProjectionConcurrencyException derives from InvalidOperationException, so the legacy catch maps ordinary provider rebuild/head races to permanent 422. Treat this demonstrated retryable persistence condition as conflict/unavailable. |
| 2 / blind-10 retention evidence isolation | low | patch (survives loopback) | The live aged-row mutation also invalidates full-row integrity, so its 503 is not independent expiry proof. Existing valid controlled-clock coverage does fail at 400 days; add the immediate-before boundary and make live evidence precise, supplementing real retention persistence/rebuild evidence. |
| 2 / edge-1 rejection before creation | high | bad_spec | A valid known rejection at position 1 reaches the fold's !state.IsCreated failure and stores nothing; the later FolderCreated at position 2 then fails the gap guard. Such rejections are emitted by the processor, and the permission projection already accepts them. Persist the source-only prefix without invented views. |
| 2 / edge-2 rebuild operation limit | high | bad_spec (group with blind-1) | Same verified defect: 98 task rows plus lifecycle/workspace/checkpoint exceed the SDK retained-manifest 100-operation hard limit. |
| 2 / edge-3 receipt manifest limit | high | bad_spec (group with blind-2) | Same verified defect: all source receipts/messages are embedded and rewritten in one recoverable value, which eventually exceeds the hard one-MiB manifest limit. |
| 2 / edge-4 pre-anchor attempt residue | low | reject | A crash before the first discovery anchor is installed leaves no reader-visible mutation or checkpoint advancement. A fresh conditional attempt can complete the same source safely; the described retained metadata residue does not strand delivery or invent completion. A new local attempt journal adds protocol complexity for a rare metadata-only residue; SDK metadata lifecycle owns it. |
| 2 / edge-5 provider credential integrity | medium | patch (group with blind-6) | Same verified defect: changing only the projected credential ref preserves source-head/receipt/profile checks and reaches readiness. Reuse retained semantic evidence before enrichment/resolution. |
| 2 / edge-6 named discovery | medium | bad_spec | Production metadata uses the query-only SDK factory overload and advertises no named/shared operational capability. The identity-based dispatcher does work directly (so prior blind-1 remains refuted), and live20 used actual named SDK rebuild; normal discovery/enrollment still omits the route, which must be corrected when selecting the bounded SDK protocol. |
| 2 / verification-1 profile mismatch tests | medium | patch (survives loopback) | Pre-verified gap: service tests omit CapabilityProfileRef, the reader test constructs fingerprints directly and live acceptance tests only missing/valid same-tenant Forgejo profiles. Add service cases for known foreign-tenant and wrong-provider profiles, asserting InvalidPolicy and no append. |
| 2 / verification-other credential integrity | medium | patch (group with blind-6) | Verified against the reader and semantic validator: CredentialReferenceId is not checked against Binding.IdempotencyFingerprint before private profile enrichment/provider readiness, while that retained semantic fingerprint includes the original ref. |

| 3 / blind-1 SDK owner history limit | high | bad_spec | OperationalStatusRebuild sends the entire owner history to the dispatcher. SeedCapturedOwnerSequenceAsync:404 and CanPromoteCapturedJournalOwnerAsync:538 hard-limit captured identity vectors to10,000. Ordinary delivery accepts10,001; the capacity test stops before the next rebuild. Existing source cannot qualify unlimited required rebuild by raising dispatch options. |
| 3 / blind-2 inherited checkpoint repair | medium | patch (survives loopback) | PrepareRebuildAsync:187 catches only selected-value loss. ReadGenerationAsync can resolve inherited checkpoint values after another owner promotes the tenant generation, then throws the distinct inherited-value-missing error. Complete-source repair is blocked; include the demonstrated inherited missing-value case while retaining other unavailable/conflict failures. |
| 3 / blind-3 coherent older provider binding | medium | bad_spec | Validate compares Binding only to its own semantic fingerprint; it never binds the selected configuration to the retained configured-event position/digest. A complete older Binding with its valid fingerprint passes alongside newer Messages/LastSequence and current source head, allowing obsolete credential use. |
| 3 / blind-4 corrupt provider rebuild | medium | bad_spec | RebuildAsync:98 validates malformed prior derived state unconditionally. A changed credential reference fails reads and every authoritative rebuild. Complete-current source plus conditional save must repair damaged derived state while valid retained source anchors still constrain replacement. |
| 3 / blind-5 fresh rejection damaged view | medium | bad_spec (completion validation) | PrepareDeliveryAsync:118 folds fresh rejections and writes lifecycle/receipts/checkpoint, but no workspace views and no validation of the preserved workspace row. Damaged current workspace state therefore remains unavailable after an acknowledged fresh rejection. |
| 3 / blind-6 duplicate historical task loss | medium | bad_spec (completion validation) | Duplicate intake calls RequireCurrentViewsAsync, which reads only checkpoint.State current workspace/task. Replaying an older task release after a later task becomes current can return AlreadyCompleted while the replayed task row is missing. Validate the retained views required by the replayed source before acknowledgement. |
| 3 / blind-7 repository-specific Forgejo readiness | medium | bad_spec | ForgejoHttpApiClient.GetReadinessAsync observes authenticated user and server version only, then hardcodes metadata/binding flags true. The new Supported WorkspacePreparation row is consumed by WorkspacePreparationService to append WorkspacePrepared. No repository observation intervenes, so deleted/inaccessible bound targets can receive fabricated preparation success. |
| 3 / blind-8 reconciliation stale failure | medium | patch (survives loopback) | ApplyWorkspaceLifecycleEvent:311 sets success on a transition to Committed without clearing WorkspaceCommitFailureCategory. Failed→reconciliation-required→committed is valid in FolderStateTransitions and produces known_success with provider_failure_known in the new durable metadata; clear superseded failure evidence on confirmed recovery. |
| 3 / blind-9 null workspace identifier | medium | patch (survives loopback) | ValidateWorkspaceEvent:169 gates its check on workspace is not null; nullable deserialization can produce a WorkspaceLockAcquired with null WorkspaceId, and WithWorkspaceTransition falls back to the prior workspace. Require identifiers for every demonstrated workspace-bearing event before any fold/persistence. |
| 3 / blind-10 invalid lease interval | medium | patch (survives loopback) | The acquired-event guard checks event/holder/lock identity but no positive duration or ordered lease bounds. A retained acquisition expiring before acquisition/effective time is applied and materialized. Validate existing lease semantics before accepting this proven malformed state. |
| 3 / edge-1 provider rebuild advancing head | medium | patch (survives loopback) | RebuildAsync checks head once before loading/saving. Source may advance while its delivery is pending, allowing the old-prefix ETag save to succeed and return completion. A post-save authoritative head check must return the already mapped retryable concurrency outcome. |
| 3 / edge-2 provider rejection code | medium | patch (survives loopback) | FoldersProviderBindingEventProcessor checks rejection CommandType/version but never Code, unlike operational intake. Accepted/undefined rejection codes can therefore advance provider source receipts and be acknowledged. Validate defined non-success rejection codes before ApplyAsync. |
| 3 / verification-1 lifecycle outcome assertions | medium | patch (survives loopback) | Pre-verified gap: convergence tests compare the same fold and public lifecycle vocabulary tests supply manual snapshots. Add source→persisted reader→public query assertions for repository failure, unknown outcome and reconciliation-required so wrong fold mappings fail. |
| 3 / verification-2 Worker acknowledgement gate assertions | medium | patch (survives loopback) | Pre-verified default-lane gap: route tests assert permission/indexing only, while direct operational processor tests bypass composition. Current opted-in live acceptance did execute/pass combined persistence checks, but ordinary route regressions should also assert operational persisted state before success and retry on injected operational persistence failure. |

| 4 / blind-1 v2 seam task authority | low | defer (intent excludes public v2 exposure) | Pd10V2CandidateCompatibilitySeam.cs:591-598 omits AuthorizedFolderId/AuthorizationWatermark, so the durable reader fails v2 GetTaskStatus closed (EventStoreOperationalStatusReadModel.cs:167-169). Reachable only with ApiRouting Coexistence/V2Only (default V1Only, FoldersApiRouting.cs:24); the seam is hash-bound in the v2 candidate set, so the fix belongs to v2 requalification. |
| 4 / blind-2 missing folder hint retryable | low | patch (survives loopback) | The v1 503 for a missing hint is documented (hexalith.folders.v1.yaml:3888-3913), but FoldersDomainServiceEndpoints.cs:5544-5550 reports retryable:true through `?? true` for a fixed client error. Return retryable:false when no FolderAuthorizationDenial is present. |
| 4 / blind-3 aged view outcome | medium | intent_gap (group: expired-view outcome) | EventStoreOperationalStatusReadModel.cs:281 and :267-269 throw for age ≥400 days or a missing known view; :45-47/:195-197 map that to retryable read_model_unavailable, so an idle entity returns a retryable 503 indefinitely. The matrix does not say which safe outcome an expired view receives. |
| 4 / blind-4 derived-row retention | low | intent_gap (group with blind-3) | Writer :239-241 sets TTL on views only; receipts, messages, evidence and the checkpoint have null TTL. An expired view therefore leaves evidence that makes reads 503 (:267-269) and the writer throw (:133-134). |
| 4 / blind-5 legacy snapshot | false | reject | Folders never returns PostCommandStateJson (FoldersDomainServiceRequestHandler.cs:196-197) and maps no /replay-state, so no snapshot can reach FolderEventReplay.cs:259-261. |
| 4 / blind-6 closure map growth | low | reject | CloseTask re-freezes the closure map (FolderStateApply.cs:420-427) and the fold loops closures per event. The retained closures are required authority, the cost is negligible at supported history sizes, and the fix adds structure beyond a direct correction. |
| 4 / blind-7 provider digest divergence | false | reject | DomainProjectionDispatcher.UpcastRequest (called at FoldersDomainServiceEndpoints.cs:130) rejects any non-v1 ProviderBindingConfigured before RebuildAsync, and Workers hashes the raw payload, so the digests match. The duplicated recipe has no diverging caller. |
| 4 / blind-8 silent 503 catch-alls | low | patch (survives loopback) | The /project 503 branch (FoldersDomainServiceEndpoints.cs:176-181) logs nothing, unlike the 422 branch. ProjectionDispatchValidationException (derives from Exception) now becomes a retryable 503. Log the exception type and classify that validation exception with the 422 branch; the other catch-alls follow the existing fail-closed convention. |
| 4 / blind-9 SDK message matching | low | reject | The literals match SharedProjectionEpochCoordinator.cs:1837,2841,2846, and OperationalStatusPersistenceTests.cs:135,383 exercise them against the real coordinator, so a rewording fails tests. JsonException handling is the approved repair path. |
| 4 / blind-10 cross-host clock skew | high | patch (survives loopback) | OperationalStatusEventReader.cs:73 rejects OccurredAt (Folders host clock, e.g. WorkspaceLockAcquisitionService.cs:92) later than Timestamp (EventPersister.cs:146 UtcNow) with no tolerance. That permanently poisons a legitimate retained event; Workers then return 500 and skip indexing. Remove the cross-process comparison; allow bounded skew on the ObservedAt-vs-clock checks. |
| 4 / blind-11 delivery stage labels | low | patch (survives loopback) | ACL stall refuted: earlier stages persist and dedupe on redelivery. Labelling is wrong: FoldersWorkersModule.cs:198-199,213 report provider-binding failures as permission_projection and log every failure as permission_projection_failed. |
| 4 / blind-12 rebuild session lifecycle | high | bad_spec (group: shared rebuild session) | Each /project call uses a fresh GenerateCanonicalMessageId (FoldersDomainServiceEndpoints.cs:143-144), and OperationalStatusRebuild.cs:35-55 never aborts. Any failure after Begin leaves the tenant epoch Building; later Begins fail "Another shared rebuild is active" (SharedProjectionEpochCoordinator.cs:194-205) and ordinary deliveries are backpressured. :53-55 returns 503 after a promoted generation. |
| 4 / blind-13 profile drift | medium | patch (survives loopback) | ConfiguredProviderReadinessBindingReader.cs:20-37 throws InvalidOperationException on profile removal or mismatch. ProviderReadinessValidationService.cs:132-138 maps every exception to retryable retry_later with no logger. Map the known mismatch to the existing non-retryable fix_provider_configuration remediation and log it. |
| 4 / blind-14 task authorization mapping | false | reject | read_task_status gates IsSupported (LayeredFolderAuthorizationService.cs:170). The matrix already requires a folder read grant for GetTaskStatus (authorization-matrix.md:127,323), and its hash still equals the requalification binding. |
| 4 / blind-15 WorkspacePrepared scope | false | reject | WorkspacePrepared belongs to done Story 4.2. The approved prerequisite requires proving bind/prepare/lock/release, and OperationId=WorkspaceId is the baseline convention (WorkspacePreparationService.cs:182 at baseline). |
| 4 / blind-16 cleanup placeholders | low | defer (intent assigns cleanup to 12.3–12.7/4.22) | OperationalStatusFold.cs:268-278 emits fail-safe status_only cleanup and unavailable provider-reference values and never claims eligibility. The reduced vocabulary is not recorded as a limitation; the eligibility rules belong to 4.22/FR30. |
| 4 / blind-17 epic context loss | medium | patch (survives loopback) | The epic-12-context.md rewrite in commit 0fa612f1 dropped concrete anchors that Epic 12 builds load instead of raw planning docs: 409/exit 77/concurrency_conflict, 24-hour/seven-year replay retention, 422 confidential_operation_not_durable, readiness dependency health, and the 4.23 dependency. Regenerate it from the planning artifacts. |
| 4 / blind-18 stale qualification prose | low | patch (survives loopback) | proposal-12-2-acceptance-prerequisites.md:49-51 still reports stage four, 3,877 cases, "implemented and qualified" and "uncommitted" SDK edits. AppHost README:248-249 says the HEAD is unchanged at 84bb115a, but EventStore HEAD is the local unpushed d4d05f87. Rejected sub-claims: non-retained /tmp logs (low, hashes recorded) and the commit label (needs history rewriting). |
| 4 / blind-19 SDK revision binding | low | patch (group with blind-18) | Same stale-prose defect: the README and proposal still describe uncommitted SDK edits at an unchanged HEAD. |
| 4 / blind-20 selective metadata mapper | false | reject | Folders registers no owner-proof or replay-recovery registrations, and its legacy handler declines enrollment, so BindMetadata leaves the response unchanged (DomainProjectionOwnerProof.cs:127-129). No other host uses the selective mapper. |
| 4 / blind-21 literals/dead signatures | low | reject | All items verified cosmetic; the 16-operation/32 KiB chunks stay within the SDK checks (DomainSharedProjectionRebuildDispatcher.cs:939-947). The fix changes public signatures. |
| 4 / blind-22 per-operation cost | medium | bad_spec (group: unmeasured cost budgets) | Each read does two ReadManyAsync, one head call and five JsonSerializerOptions allocations (EventStoreOperationalStatusReadModel.cs:234-254). Per position, the writer registers, catches up twice and prepares twice (OperationalStatusProjectionWriter.cs:50-78) before the duplicate short-circuit. No evidence measures this against the 500 ms target (prd.md:210). |
| 4 / blind-23 generator prior hash | false | reject | Pd10ConformanceSetTests.cs:22-27 pins both the on-disk prior manifest hash and the manifest scalar, so a changed prior file fails. The fixed date is the dated generation input. |
| 4 / blind-24 sprint-status comment | low | patch (survives loopback) | The line-3 comment says last_updated 2026-10-18 while the field is 2026-10-19; correct it at the next status sync. The `# needs eventstore` comment is still accurate for 12.2 and is preserved. |
| 4 / verification-1 accepted profile path | medium | patch (survives loopback) | Pre-verified gap: no test runs ConfigureProviderBindingService with a matching profile and consumes its NamingPolicy/BranchPolicy output through ConfiguredProviderReadinessBindingReader. |
| 4 / verification-2 Forgejo bound-target plumbing | medium | patch (survives loopback) | Pre-verified gap: no normal-lane test covers Bound* to RepositoryTarget, the ForgejoProvider resolver handoff, or ConfiguredProviderRepositoryTargetResolver mapping and rejections. |
| 4 / verification-3 /project operational repair | medium | patch (survives loopback) | Pre-verified gap: ProductionProcessWireEventTests asserts only HTTP 200 and the legacy body. It never asserts operational rows after success, or a 503 with no committed rows on injected batch failure. |
| 4 / verification-4 provider delivery vs repair digest | medium | patch (survives loopback) | Pre-verified gap: no Workers route test asserts the persisted binding projection, and no test feeds worker-produced state into the /project rebuild and compares bytes. |
| 4 / verification-5 readiness reader exception | medium | patch (survives loopback) | Pre-verified gap: no reader double throws, and provider_binding_authority_unavailable has no test hit. |
| 4 / verification-6 production host provider wiring | medium | patch (survives loopback) | Pre-verified gap: no production-host assertion covers ConfiguredProviderReadinessBindingReader/ConfiguredProviderRepositoryTargetResolver registration or options binding (FoldersServerHostComposition.cs:35-37). |
| 4 / verification-other v1 provider binding regression | high | intent_gap | `dotnet test --project tests/Hexalith.Folders.IntegrationTests --no-build --filter-method "*.ArchiveFolderProcessWiringTests.ConfigureProviderBinding*"`: 3 total, 3 failed (400 instead of 202). FolderDomainProcessor.cs:556 now forwards the v1-required capabilityProfileRef, and ConfigureProviderBindingService.cs:83-91 rejects refs that are not host-configured. No shipped host config defines profiles, and at baseline the ref was ignored. CI runs this assembly (ci.yml:49). Whether v1 may now require a host-configured profile is not settled by the intent. |
| 4 / edge-1 shared Empty policy | high | patch (survives loopback) | ProviderBindingProfile.cs:11,13 default to the shared OrganizationProviderBindingPolicy.Empty, and the reflection binder (FoldersServerHostComposition.cs:35) mutates that instance. With two profiles, every policy aliases Empty with merged metadata (reproduced against the built assembly). Profile-less configures (ConfigureProviderBindingService.cs:100,106) then persist another profile's private metadata into retained events. Initialize fresh instances. |
| 4 / edge-2 Building epoch never aborted | high | bad_spec (group with blind-12) | Begin moves the tenant scope to Building and nothing aborts it; ordinary deliveries get Backpressure (SharedProjectionEpochCoordinator.cs:697-700,793-796). A deterministic trigger: owner history above 10,000 fails commit seeding after Begin. |
| 4 / edge-3 per-command full rebuild | high | bad_spec | EventStore invokes the domain /project after every accepted command, with the full 1..head stream (SubmitCommandHandler.cs:499-505; ProjectionUpdateOrchestrator.cs:186-263). FoldersDomainServiceEndpoints.cs:132-150 runs a tenant-scoped shared rebuild each time, inside the awaited command path. Concurrent folders in one tenant then get 503, and ordinary deliveries are backpressured. |
| 4 / edge-4 first-delivery gap | false | reject | Gap recovery is automatic today: the per-command full-history /project rebuild restores the checkpoint, and Workers redelivery then dedupes (OperationalStatusProjectionWriter.cs:60,108-115). Re-derivation of edge-3 must keep an equivalent recovery path. |
| 4 / edge-5 named gap classification | false | reject | Named dispatch routes epoch-enabled handlers to DispatchFencedSharedAsync, which never calls ProjectAsync (DomainProjectionDispatcher.cs:733-740); only tests call it. |
| 4 / edge-6 domain clock ahead | high | patch (group with blind-10) | Same defect as blind-10 at OperationalStatusEventReader.cs:73. |
| 4 / edge-7 reader clock lag | low | patch (group with blind-10) | EventStoreOperationalStatusReadModel.cs:261,280 return a retryable 503 while the reader clock lags EventStore stamps. It clears once the clocks converge. |
| 4 / edge-8 terminal TTL refresh | medium | intent_gap (group with blind-3) | Writer :135,:138,:246 rewrites terminal rows with a fresh 400-day TTL and the original ObservedAt (OperationalStatusFold.cs:292-293). Reads after 400 days fail with a retryable 503 (:281), while the rows outlive the C3 window. |
| 4 / edge-9 expired view after idle | medium | intent_gap (group with blind-3) | The read-side 503 for idle entities is real (EventStoreOperationalStatusReadModel.cs:267-269,281). The writer side recovers through the per-command rebuild. |
| 4 / edge-10 missing evidence dereference | low | reject | Requires derived-state corruption, since row and evidence are written in one batch (writer :243-248). It still fails closed and is repaired; the fix adds a guard. |
| 4 / edge-11 dead known-view plumbing | low | reject | sourceEvidence is unused, and requireKnownView reads maps that Compact empties. NotFound occurs only after partial derived-state loss with a surviving checkpoint; the fix adds a guard. |
| 4 / edge-12 one-second delivery deadline | maybe-false | bad_spec (group with blind-22) | The operational chain shares PermissionProjectionDelivery's one-second deadline (PermissionProjectionDelivery.cs:11), but the real-Dapr acceptance passed through the composed route. Settling this needs p50/p99 wall time on a production-like store. |
| 4 / edge-13 indexing behind operational failure | medium | bad_spec | Any operational-stage exception returns 500 before semantic indexing (FoldersWorkersModule.cs:203-216). After a permanent failure, every later event fails the gap check (OperationalStatusFold.cs:47-50), so indexing for that folder stops until a rebuild or dead-lettering. The spec mandated pre-acknowledgement composition without isolating indexing. |
| 4 / edge-14 provider stage label | low | patch (group with blind-11) | Same labelling defect at FoldersWorkersModule.cs:198-199. |
| 4 / edge-15 missing GlobalPosition | false | reject | EventPublisher.cs:205 always copies the positive GlobalPosition assigned by DaprGlobalPositionAllocator (EventPersister.cs:213-241). |
| 4 / edge-16 live head during rebuild | high | bad_spec | Writer :189 compares against the live head read during Accumulate (OperationalStatusProjectionHandler.cs:58-60). A concurrent append fails the partition after Begin, leaving the session Building (see blind-12). Compare against the SDK-captured watermark instead. |
| 4 / edge-17 stored vs effective catch-up | false | reject | Same premise refuted in 1/blind-8 at a different location: there are no upcasters and the closed inventory is all v1, so stored and effective bytes are identical. |
| 4 / edge-18 provider rebuild versions | false | reject | Admission (DomainProjectionEvolutionAdmission.cs:20-31) and the registry (EventPayloadEvolutionRegistry.cs:120-127) reject unregistered versions before line 165 runs. |
| 4 / edge-19 created-repository fingerprint | false | reject | Production creation-target resolution is unconfigured (ConfiguredProviderRepositoryTargetResolver.cs:11-13), so no created Forgejo folder reaches Bound. |
| 4 / edge-20 Forgejo invalid target category | false | reject | The production resolver validates with the same predicate as IsForgejoRepositoryTargetValid (ForgejoProvider.cs:1126-1129), and no resolver failure carries RetryAfter. |
| 4 / edge-21 credential NotFound mapping | low | reject | Every non-PermissionDenied DaprException maps to Unavailable(30s). At baseline, gRPC DaprException escaped unhandled, so this is a net improvement. It affects only occasional misconfiguration, and the fix adds branches. |
| 4 / edge-22 profile edit before retry | false | reject | Equivalent retries are answered by EventStore admission Replay (SubmitCommandHandler.cs:68-130) or the actor duplicate cache. The domain re-runs only when the first attempt never persisted. |
| 4 / edge-23 profile drift | medium | patch (group with blind-13) | Same location and outcome as blind-13. |
| 4 / edge-24 legacy snapshot | false | reject | Same refutation as blind-5. |
| 4 / edge-25 reconciliation by another task | false | reject | No current producer emits a reconciliation-to-Committed lifecycle event. FolderWorkspaceLifecycleEventRecorded is constructed only by WorkspacePreparationService.cs:154,190. |
| 4 / edge-26 closure growth | low | reject (group with blind-6) | Same as blind-6. |
| 4 / edge-27 metadata endpoint double map | false | reject | The only caller maps the endpoint once (FoldersServerHostComposition.cs:137-139), and Folders never calls the full mapper. |
| 4 / edge-28 selective mapper source guard | false | reject | Same refutation as blind-20. |
| 4 / edge-29 prefix extension identities | false | reject | PrepareRebuildAsync rejects sources whose rolling digest contradicts the retained checkpoint across delivered positions (OperationalStatusProjectionWriter.cs:204-211), and pending journal entries are still compared (SharedProjectionEpochCoordinator.cs:519-545). |
| 4 / edge-30 head mismatch 422 | low | patch (survives loopback) | FoldersDomainServiceEndpoints.cs:137-140 throws InvalidOperationException, so an advancing head becomes 422 projection_unreadable, contrary to the Retry correction. Return 503 projection_unavailable. |
| 4 / edge-31 provider rebuild race 422 | low | patch (survives loopback) | The head is checked at EventStoreProviderReadinessBindingReadModel.cs:95 and the row read at :99. A concurrent ApplyAsync makes :104-108 throw InvalidOperationException, which becomes 422. Throw PermissionProjectionConcurrencyException instead. |
| 4 / edge-32 provider rebuild rejection code | medium | patch (survives loopback) | FolderEventReplay.cs:199-203 validates rejection shape only, and RebuildAsync never checks Code (EventStoreProviderReadinessBindingReadModel.cs:83-115), while Workers refuses Accepted or undefined codes (FoldersProviderBindingEventProcessor.cs:46-50). Apply the same code check before RebuildAsync. |
| 4 / edge-33 GitHub preparation | false | reject | Production OctokitGitHubApiClient.GetReadinessAsync throws NotImplementedException (OctokitGitHubApiClient.cs:49-62), so no Supported preparation row exists in production. |
| 4 / edge-34 head reader | false | reject | DaprOrganizationStreamSequenceReader is unchanged since baseline and was already registered; no SDK sequence reader exists. |
| 4 / edge-35 metadata identity mismatch | false | reject | EventStore sends AppId=registration.AppId and ServiceVersion "v1" (AdminOperationalIndexHostedService.cs:162-167), matching the Folders defaults. |

### Review five verdicts — 2026-11-04

Coverage: the mandated baseline diff contains10,249sections/13.1million lines, including unrelated
history and generated evidence. All three lenses indexed it and traced selected behavioral source,
tests and callers; none claims exhaustive review of every historical artifact. Every final finding
is classified below before grouping. Temporary probes use existing Debug assemblies only.

| Round / finding | Verdict | Route | Verified evidence |
| --- | --- | --- | --- |
| 5 / blind-1 missing view before lag | medium | bad_spec | ReadAsync returns missing at266–271 before the positive head>checkpoint guard at285. A checkpoint8/head9 with a never-materialized task cannot establish absence for the complete current prefix; this is distinct from a positively expired row or paired absence at a current checkpoint. Specify stale-before-unproved-absence while preserving the frozen expired-view outcome. |
| 5 / blind-2 expired task resurrection | medium | bad_spec | Parent probe first persists the clean-release prefix, removes its expired task/evidence pair and advances401days, then delivers a lawful FolderAccessGranted. Live delivery completes and persists task-a with TerminalState=null and a fresh2027-11-23timestamp; complete-source fold retains ready at2026-10-18 and omits it as expired. Clearing closure authority from compacted state permits this new result. |
| 5 / blind-3 native checkpoint growth | high | bad_spec | Compact clears TaskClosures but retains native content/file/erasure/commit/release dictionaries. The reviewed valid4000native acquire/release history yields306073checkpoint bytes and actual SDK PartitionMutations rejects the value. This violates the per-value bounded contract below supported source-count limits; separate projection state from unrelated primary-history collections. |
| 5 / blind-4 rebuild peak memory | maybe-false | defer (potential medium, unverified) | PreparePartitionChunksAsync retains a full plan plus serialized chunk list, so allocations scale with owner output. The SDK API itself returns IReadOnlyList chunks, and current10001/32769qualification passes; no supported distinct-view history, measured peak/limit or failure threshold was supplied. A realistic maximum-history/distinct-view memory benchmark plus a supported budget would settle impact. |
| 5 / blind-5 provider history growth | maybe-false | defer (potential medium, unverified) | Messages append, but lawful accepted bindings are write-once, so repeated accepted Configurations are not established by this caller. Rejected-position history can grow; the direct single-value store path does not use the cited shared-mutation cap. Repeated lawful rejection/configuration history measured against actual transport/store limits is needed to establish user impact. |
| 5 / blind-6 dead known-view plumbing | low | reject (carried) | carried4/edge-11: the same unused callbacks/compacted maps remain, and the logged low/reject verdict and route are retained. No duplicate patch or defer is introduced. |
| 5 / blind-7 cancellation during rebuild | medium | patch (survives loopback) | The existing cancellation argument is not observed by synchronous source folding/materialization/serialization loops before the next awaited store call. Large admitted histories continue CPU/allocation after cancellation. Observe the existing token while enumerating source/operations without adding a public signature, changing canonical bytes or weakening recovery. |
| 5 / blind-8 phantom drained request | medium | patch (survives loopback) | Parent probe observes a valid start then an unmatched end with inFlight=0; Snapshot reports available with one active request. Reconcile confirmed drain and mark incomplete observation explicitly instead of retaining a phantom identity. No request/context or behavior change is needed. |
| 5 / blind-9 denied-safe response assertions | false | reject | A foreign-folder missing result takes TaskStatusQueryHandler.SafeResult, which explicitly nulls task/terminal/operation/failure fields. Only the authorized different folder's keys are read; the executed counting-store control proves the original folder keys are never observed. The proposed extra assertion does not demonstrate an untested reachable disclosure. |
| 5 / blind-10 reader diagnostics | low | reject | Canonical unavailable intentionally withholds internal error detail; the claim supplies no failed ordinary-user outcome beyond optional operator diagnosis of corruption/faults. Adding classified production logging requires extra plumbing, and captured intent assigns general diagnostics to Epics4/6/10. Keep the safe public refusal; no complex low-impact enhancement here. |
| 5 / edge-1 native checkpoint cap | high | bad_spec (group with blind-3) | Reviewed executable probe confirms4000released native lease fences remain after compaction and exceed the SDK256KiB single-mutation cap. This is the same retained-primary-history defect, not another limit to raise. |
| 5 / edge-2 native acquisition validation | medium | bad_spec | ValidateWorkspaceEvent validates lease/lock bounds only for WorkspaceLockAcquired. WorkspaceNativeTaskChanged acquire passes through the owner-admitted apply path with blank lock, lease=-1 and expiry before effective time; the probe confirms sequence9 and successful SDK writer persistence. Apply source-shape checks to supported native metadata as well, preserving lawful native operation semantics. |
| 5 / verification-1 contention coverage | medium | patch (survives loopback) | Pre-verified gap: no current test forces Monitor.TryEnter failure and proves incomplete inventory remains unavailable until drain. Removing the gap poison leaves checked serial/held-request cases passing. Add deterministic non-waiting contention, stopping-snapshot and recovery coverage; trust the filed searches/evidence. |

Surviving root causes: missing-view freshness precedence; expired-task closure loss; retained primary
native history in bounded checkpoints (blind3+edge1); native acquisition source-shape validation.
These are nonfrozen implementation/spec gaps; captured intent already requires truthful source,
bounded metadata and no resurrection. No intent decision or frozen amendment is needed. Cancellation,
phantom-drain correction and contention-test patches survive re-derivation. Two unverified performance
entries remain separate potential-medium defers. Other findings are rejected as recorded.

### Review six — 2026-11-14

All three required layers returned. The complete preserved-baseline diff has 10443 sections and
1.79 GB; reviewers indexed it and traced selected behavior, without claiming exhaustive historical
coverage. Parent isolated probes independently reproduce both corruption defects and the three
source/refusal paths. Useful source, commands, outputs and binary hashes survive outside the removed
run-with-temp scratch. [Full disposition](review-12-2-round6-disposition.md) and
[machine-readable triage](evidence/story-12-2-review6-triage-20261114.json) retain every finding.

| Round / finding | Verdict | Route | Verified evidence |
| --- | --- | --- | --- |
| 6 / blind-1 | medium | bad_spec | `src/Hexalith.Folders/Queries/Folders/EventStoreOperationalStatusReadModel.cs:280`: Reader accepts coherent older task row and evidence beside newer closure. Parent probe returns Available/locked/nonterminal for seq7 after seq8 ready closure. Same old receipt is genuine but is insufficient current-view authority; distinct from prior paired-absence/dead-callback triage. |
| 6 / blind-2 | medium | patch | `src/Hexalith.Folders/Projections/OperationalStatus/OperationalStatusProjectionWriter.cs:176`: Writer preserves a forged LastOperationId from a ready row with missing evidence, then writes fresh proof after a lawful next event. Parent probe returns Completed/forged-operation. Existing source-derived completion can restore the row; prior terminal state alone must not override it. |
| 6 / blind-3 | false | reject | `src/Hexalith.Folders/Queries/Folders/EventStoreOperationalStatusReadModel.cs:266`: The frozen 2026-10-20 decision explicitly permits not-found when view and evidence are both absent; round-five instructions also preserve paired absence at a current checkpoint. Requiring an independent expiry timestamp would change that decision. |
| 6 / blind-4 | medium | bad_spec | `src/Hexalith.Folders.Workers/FoldersRejectedCommandClassifier.cs:47`: FolderDomainProcessor emits normalized unknown-command UnsupportedCommandType rejections. Folder permission processing calls the strict command-name classifier before operational persistence and throws for that lawful position. Parent classifier probe confirms the refusal; later source positions can remain stale. |
| 6 / blind-5 | maybe-false | carried-defer | `src/Hexalith.Folders/Queries/ProviderReadiness/EventStoreProviderReadinessBindingReadModel.cs:72`: carried 5/blind-5: same Messages append/validation and direct single-value store path remain. No new lawful-history measurement against actual transport/store limits was supplied. Retain potential medium unverified disposition; do not duplicate its patch or defer. |
| 6 / blind-6 | medium | reject-out-of-scope | `_bmad-output/implementation-artifacts/evidence/story-12-2-sdk-shutdown-implementation-20261114.json`: Acknowledgment p95 8642.996ms exceeds 1000ms and remains an open gate. Existing instructions require recording rather than general performance redesign; the full retry-wrapper metric does not attribute cost to an approved native/bootstrap subpath. No passing SM4 claim is made. |
| 6 / blind-7 | medium | reject-out-of-scope | `_bmad-output/implementation-artifacts/evidence/story-12-2-sdk-shutdown-implementation-20261114.json`: Status p95 1291.141ms exceeds 500ms and remains open. Preserve this real finding under the same explicit record-only/general-redesign exclusion; Workers timing does not close it. |
| 6 / blind-8 | false | reject | `.github/workflows/ci.yml:94`: The specialized job explicitly runs git submodule update --init --checkout, rather than a strategy-free update. Git documents that --checkout overrides submodule.<name>.update; update=none therefore does not establish this predicted failure. No submodule was changed to verify it. |
| 6 / blind-9 | maybe-false | defer | `.github/workflows/ci.yml:25`: No checked-in invocation was found in Root/Platform workflow sources, and the default CI whitelist is explicit. The current producer separately executes all 11+5 issuer cases. A mandatory automatic-gate requirement or the external owner automation/policy would settle whether this deliberate manual/component partition creates a false-green defect; potential medium remains unverified. |
| 6 / blind-10 | low | patch-artifact | `.artifacts/story-12-2-review6-20261114/review-artifact-index.json`: The canonical preserved-baseline diff is 1788748446 bytes/10443 sections, chiefly historical evidence. A supplementary 4650235-byte runtime/test view and immutable index/hash were retained for human use without changing the original baseline or pretending exhaustive historical review. |
| 6 / edge-1 | medium | bad_spec | `src/Hexalith.Folders.Server/OperationalStatusProjectionHandler.cs:93`: Operational handler is registered for the whole folders domain, but no-ops only three ACL contracts. A valid configured provider-binding partition reaches Unknown operational source event; parent probe validates the provider command then reproduces this refusal. Workers already skip the reserved binding owner. |
| 6 / edge-2 | medium | bad_spec | `src/Hexalith.Folders.Server/OrganizationAclOperationalSource.cs:23`: A lawful organization FolderCommandRejected alone is not recognized as ACL and reaches FolderStreamName tenant==folder rejection; parent probe reproduces InvalidFolderId. Mixed ACL/rejection histories also fail the three-contract-only loop. Validate legitimate non-Folder prefixes before an empty fold without weakening bad-scope refusal. |
| 6 / edge-3 | medium | bad_spec | `src/Hexalith.Folders/Queries/Folders/EventStoreOperationalStatusReadModel.cs:280`: Same verified coherent rollback as blind-1. Independent old evidence and receipt only prove a former valid value; ReadAsync never applies the supplied sourceEvidence predicate or retained task completion authority to reject the rolled-back nonterminal row. |
| 6 / verification-1 | medium | patch | `references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Projections/NamedProjectionDispatchCoordinator.cs:1186`: Pre-verified gap accepted as filed: new host-stop test forces Retryable admission and independently holds only the early completion call. Successful dispatch cleanup at line1186 lacks a held-call cancellation assertion. Add that case without changing production semantics or relaxing actual-call/fence/work ownership. |
| SM4 transport / blind-1 non-domain hosts | low | defer | Hosts that register `AddEventStoreGatewayClient` without `AddEventStoreDomainService` keep the inherited pipeline. The EventStore samples are examples. Per the verification layer they send no idempotency key, so the Pending 202 cannot occur there; resubmission there is pre-existing. |
| SM4 transport / blind-2, edge-6 implicit client name | low | reject | Upstream `88c8b27b7` now registers the typed client with explicit `nameof(IEventStoreGatewayClient)`, so both sites bind one symbol. A host-chosen custom name keeps its own policy, which is not a regression; the comment now names the binding. |
| SM4 transport / blind-3 wiring and isolation untested | medium | patch | Claim 4 had no test. `GatewayClientAloneUsesTheBoundedSingleAttemptBudget` now pins the gateway pipeline at 30/30 and the inherited `-standard` defaults. An outer inherited handler is excluded because its 10 s attempt would fail the held-submission test. |
| SM4 transport / blind-4 Folders registration order | medium | patch | Folders registers the client before `AddEventStoreDomainService`. The held-submission test is now a theory over both orders; both pass. |
| SM4 transport / blind-5, edge-3 untranslated total timeout | medium | defer | `SendTranslatingAsync` does not translate Polly `TimeoutRejectedException`. The inherited 30 s total timeout already raised it; this change makes it the main slow-command failure. Fixing it in Client needs Polly awareness. |
| SM4 transport / blind-6, edge-4 fixed 30 s budget | medium | defer | A gateway command slower than 30 s is still abandoned, and a caller retry can then meet Pending. The limit predates this change. Sizing it from the gateway's own command budget is a separate SDK decision. |
| SM4 transport / blind-7, edge-2 Retry-After at budget | low | defer | A `Retry-After: 30` sidecar-unavailable reply consumes the whole budget before the retry. The inherited pipeline behaved identically. |
| SM4 transport / blind-8, edge-8 late 5xx retry | low | patch | A reply ends the HTTP attempt, not necessarily the command. After Begin, recovery marking usually leads a retry into reconciliation rather than Pending. The test summary overstated this and was reworded. |
| SM4 transport / blind-9, edge-1 ambiguous transport failure | medium | defer | A connection reset after delivery is still retried and can meet Pending. This is pre-existing standard-pipeline behavior. Limiting command retries to pre-send failures would change retry behavior, which this change preserves. |
| SM4 transport / blind-10, edge-5, gap-other reads lose 10 s retry | low | reject | Gateway reads now use one bounded 30 s attempt. A slow-but-progressing read finishes sooner than with a cancelled-and-retried attempt; only a stalled connection loses the rescue. Scoping by path adds a second pipeline. The comment now states the policy covers every call. |
| SM4 transport / blind-11 stale policy summary | low | patch | The `BoundedInvocationResilienceExtensions` summary omitted the gateway client; updated. |
| SM4 transport / blind-12 weak held-test assertions | low | reject | The elapsed-time check added nothing and was removed. The test failed on the unchanged SDK (202 accepted) and passes with the change; faking time is not needed to detect the regression. |
| SM4 transport / blind-13 Folders details in SDK test | low | patch | The SDK test now states the invariant and uses neutral command values. |
| SM4 transport / edge-7 claim beyond 30 s | low | reject | True only up to the 30 s budget. The fix is wording in the claims and spec, and spec edits are rejected by triage. The spec already states one attempt owns the 30 s budget. |

Surviving bad-spec root groups are current-view authority (blind-1 + edge-3), Worker rejection
intake (blind-4), and legitimate non-Folder named-input coverage (edge-1 + edge-2). The terminal
restoration and successful-dispatch shutdown-test patches remain required but are lower-order work
until re-derivation. Provider-history growth carries its existing unverified defer; the CI automation
claim remains unverified. Paired absence and CI initialization are refuted; existing SM4 measurements
stay open under the record-only performance instruction. No new intent gap is established.

Before the next implementation loop, review_loop_iteration advances from 5 to 6. The rendered
step-04 limit requires HALT and human escalation at this point. No code is reverted, no baseline is
rewritten, and no further implementation loop has started. One additional correction/review cycle
within the existing intent requires a human decision; the report states its concrete scope and
positive preservation constraints.

## Design Notes

Intent gaps: none. Irreversibles: none; live checks own disposable state. Footprint: fold/guards, SDK readers/writer, host/Workers wiring and tests. Baseline: clean `main`, Folders `c22b13137202f7bf8c60b80c095a69748435bd6e`, EventStore `84bb115a0ced2497ca4315644a677b273cb44edf`. Rebuild current source; 12.1 acceptance used an older pin. Preserve historical manifest evidence; no sprint-status regeneration.

Round four: operational status has one live writer (Workers) and one rebuild path (the SDK-driven named transport). That keeps tenant-wide rebuild sessions out of the command path, which EventStore awaits but does not fail on. Trade-off: a dead-lettered delivery, or history from before enablement, leaves that folder honestly `projection_stale` until an operator runs the named rebuild. The retention docs task records this. The strict profile and source-time expiry rules are frozen decisions. The five-minute skew tolerance and the static serializer options are implementation choices.

### Review seven — 2026-11-14

All three required lenses returned. Parent verified every non-gap claim against source/callers and retained isolated probes; the verification-gap claim is accepted as pre-verified. Coverage is selective across the original-baseline 1.79 GB diff. [Disposition and concrete correction plan](review-12-2-round7-disposition.md) and [machine-readable triage](evidence/story-12-2-review7-triage-20261114.json) preserve every finding.

| Round / finding | Verdict | Route | Verified evidence |
| --- | --- | --- | --- |
| 7 / blind-1 | medium | bad_spec | `EventStoreOperationalStatusReadModel.cs:314`: Parent existing-assembly probe: after native acquire/release and task B, restoring task A’s earlier row/evidence/applicability returns Available/locked instead of the source-derived Available/ready. Native release creates no TaskCompletion; schema 4 retains only task B’s digest, leaving the historical task without independent current authority. |
| 7 / blind-2 | medium | bad_spec | `OperationalStatusProjectionWriter.cs:217`: Parent probe closes task A, advances to workspace/task B, restores A’s preclosure row/evidence/applicability and redelivers A’s original acquisition. Writer returns AlreadyCompleted while the durable reader returns Unavailable. RequireCurrentViewsAsync never consults A’s independently retained completion. |
| 7 / blind-3 | medium | patch | `OperationalStatusProjectionWriter.cs:62`: At day 401, parent probe redelivers the checkpoint’s exact retained sequence with a different MessageId. Writer returns AlreadyCompleted despite retaining the original tail MessageId/EventDigest. Check the existing tail identity before the expired-replay shortcut, preserving legitimate expired duplicate acknowledgement and original TTLs. |
| 7 / blind-4 | false | reject | `OperationalStatusProjectionWriter.cs:109`: Production writer and SDK DomainProjectionDispatcher construct payload and delivery headers from the same event. Dispatcher validates positive/contiguous/increasing positions; SharedProjectionEpochCoordinator hashes headers with payload and verifies that digest during restoration. No production mismatch path was demonstrated; reviewer withdrew the defensive-asymmetry claim after caller tracing. |
| 7 / blind-5 | medium | bad_spec | `FolderEventReplay.cs:83`: Parent probe passes a normalized future_command/UnsupportedCommandType rejection to the production legacy classifier and gets Unclassifiable Folders rejection event. The same finite mapper is used in actor-owned FoldFolder/FoldOrganization, which FolderDomainProcessor primes before ordinary command processing; new operational/Workers acceptance does not repair primary reconstruction. |
| 7 / blind-6 | medium | bad_spec | `FoldersProviderBindingEventProcessor.cs:47`: Parent probe classifies the normalized rejection as ProviderBinding and gets true from the named complete-prefix recognizer, but the actual provider Worker processor throws Provider configuration rejection is invalid. Its extra ConfigureProviderBinding-only check, repeated by folder-permission intake, makes live and supported named handling diverge. |
| 7 / blind-7 | medium | patch | `FoldersRejectionSource.cs:56`: Parent probes show Created, IdempotentReplay and AlreadyApplied accepted by ReadOwner and persisted as Completed by the actual operational writer. FolderDomainProcessor explicitly classifies those three plus Accepted as successes; rejection guards currently exclude only Accepted. Reject all four existing success codes consistently without a new public contract. |
| 7 / blind-8 | false | reject | `AuthoritativeWorkspaceFileContextSource.cs:113`: PaginationMetadata/FileSearchPaginationMetadata make cursor issuance optional, including truncated results. No valid issued cursor is rejected because this implementation issues none; request echo support does not require issuance. Owning Story 12.3 B7 records the same refutation, and the reviewer withdrew this claim. |
| 7 / blind-9 | high | defer (moot on loopback) | `AuthoritativeWorkspaceFileContextSource.cs:106`: Range and collection paths await source.ReadAsync after their last authorization-bearing store call and return after only Folder-head/policy checks. WorkspaceContentStateReader performs authenticated workload source reconstruction, with no final human organization/tenant reauthorization, so revocation during that await can escape the disclosure check. This pre-existing authoritative-content implementation belongs to Story 12.3 and is expressly excluded by 12.2’s frozen intent; preserve it for that owner, without changing it in this cycle. |
| 7 / blind-10 | low | reject | `Oq2WireObjectConverter.cs:194`: Parent existing-assembly probe confirms numeric ownerFileId is coerced to string and a 129-character value is accepted by incoming and outgoing validation. This affects malformed content-client objects, is outside ordinary valid generated-owner use, and server owner matching still rejects an invalid target. Additional validation branches/tests exceed a direct correction for this negligible, content-owned case; no 12.2 patch is introduced. |
| 7 / edge-1 | medium | bad_spec | `OperationalStatusProjectionWriter.cs:129`: Retained isolated probe: initial positions 1/2 complete; after day 401 and expiry of position 1’s receipt/message evidence, new position 3 reuses its MessageId and completes with checkpoint 3. The identical full source then throws Operational source sequence gap. Compaction retains only the tail identity and SDK journal guards bind positions/payloads without a MessageId uniqueness index; C3 must not make live and rebuild identity validation disagree. |
| 7 / verification-1 | medium | patch | `WorkersOrganizationAclRouteTests.cs`: Pre-verified gap accepted as filed: unknown-command cases cover Folder owners only, while organization route/inventory/non-Folder cases use known commands. Add an authenticated organization-owner unknown rejection prefix, duplicate acknowledgement, persisted LastSequence and next-sequence baseline success; no production semantics need change. |

Five distinct bad-spec entries require another nonfrozen derivation; three patches survive. The current implementation/strict pass and all KEEP constraints are preserved. The workflow counter is incremented to 7; the explicit approved exception remains 6. HALT at the review-loop limit pending a new human exception. No code is reverted, no seventh-cycle implementation starts, and no content-owned change or duplicate defer is applied.

## Verification

- `dotnet restore Hexalith.Folders.slnx -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false` established the source graph; final `dotnet build Hexalith.Folders.slnx -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false --no-restore -m:1 -nr:false -v:quiet` is recorded in the appended evidence stage.
- Run domain, Server and Workers test projects individually; use built xUnit assemblies with `-class` for focused checks.
- `dotnet test --project tests/Hexalith.Folders.IntegrationTests --no-build` must pass, including the three `ArchiveFolderProcessWiringTests.ConfigureProviderBinding*` tests that failed in round four. Run `pwsh -File tests/tools/run-contract-spine-gates.ps1` and the docs conformance tests after the docs edits; v1 contract bytes stay unchanged.
- Run opted-in AppHost acceptance with zero failures/skips; assert stored contents, checkpoints, changed PIDs and replica convergence. Record blockers separately from passing evidence.

## Historical Stage7 approved SDK source qualification — 2026-10-20

The approved immutable source-deadline and pending owner-scratch capabilities are implemented in the
EventStore owner repository and consumed through `WriteExpiring` in Folders. The named partition
round trip preserves the absolute deadline; the bounded checkpoint stays non-expiring. Delayed commit
regressions now require physical absence of expired rows, and named scratch retry/Abort permits a fresh
rebuild. Historical failed diagnostics and all six earlier evidence stages remain unchanged.

[Stage7 source qualification](evidence/story-12-2-approved-sdk-source-qualification-20261020/qualification.json)
binds 1,406 consumer source/config files, 62 assembly/PDB files, 4,195 component passes (two existing
optional Domain skips) and a full opted-in Aspire/Dapr/provider pass: 1/1, zero skips, 295.344 seconds.
The SDK packet independently records 1,736 Client passes (two existing optional skips), 1,005
DomainService passes, 36 live outer cases and four distinct-process child runs, all required live cases
without skips. Parent audits found zero source/binary/PDB mismatches. Independent review of this
approved SDK/consumer delta is pending at this qualification seal.

The Stage7 acknowledgement p95 is 31,600.244 ms across nine successful requests, including retries,
against SM4's 1,000 ms target. Workers p95 is 348.094 ms (26 samples); status p95 is 50.824 ms
(12 samples). The acknowledgement finding remains open, including the prior stage-six observation.
The Release/package gate fails CS0117 because EventStore 3.125.1 lacks `WriteExpiring`; its
`-NoRestore` fallback retains the three frozen PD10 inventory/generator failures.

Whole Story 12.2 and Task 36 remain in progress: the separately owned >10,000 captured-identity
capability, qualified consumption, package/PD10 requalification, acknowledgement performance and
whole-story independent review retain their existing dispositions. No dependency pin, Git mutation,
publication, deployment, source event, content cleanup or public v2 change is included.

Independent SDK review two reopened the prerequisite acceptance rows after Stage7: it reproduced
Prepared/no-envelope Abort, cleanup self-recovery and expired-prefix compensation failures, plus
legacy validation/retry defects. The Stage7 pass remains exact-source history; owner review loop2
re-derives the SDK from amended nonfrozen requirements before Folders consumption is restored and
requalified. Frozen intent, all historical evidence, Task36 and the separate open gates are unchanged.


## Historical Stage8 approved SDK source qualification — 2026-10-20

Review-two SDK corrections now settle original Prepared/no-envelope scratch, resume deterministic own
cleanup, preserve expired-prefix compensation and validate legacy epochs/retries. The immutable
source deadline and Folders named partition conversion remain unchanged.

[Stage8 qualification](evidence/story-12-2-approved-sdk-source-qualification-review2/qualification.json)
binds 1,661 consumer source/config files and 62 assembly/PDB files. A serialized
Debug/source build completed with zero warnings/errors; 4,195 components passed with two existing
optional Domain skips. The full opted-in Aspire/Dapr/provider durability test passed 1/1 with zero skips
in 306.591 seconds. All ten inspected SDK runtime PDBs match current
sources; all captured source/assembly hashes stayed unchanged through final execution.

SDK qualification records 1,756 Client passes with two optional skips and 1,057 Domain passes in a
serialized same-binary full run. Its failed parallel Domain run remains preserved. 51 unique required
live cases are qualified across exact recorded groups; the expiry/scratch 37-case union retains the
initial 34/37 attempt and affected final 9/9 rerun, rather than claiming one all 37 pass. Twelve final
child invocations cover six distinct exited-owner/recovery PID pairs, including Prepared/no-envelope
cuts. Parent independently verified 171 SDK packet artifacts, 3,751 final source/config hashes and the
six decoded Open/Aborted/private-cleaned worker end states.

Current acknowledgement p95 is 4359.855 ms against 1,000 ms; Workers p95 is
368.766 ms and status p95 is 60.804 ms. The acknowledgement finding remains
open. Package/PD10 failures are retained against the unchanged WriteExpiring API and frozen candidate;
no package/pin change or requalification was authorized. All seven prior canonical stages and sealed
historical packets remain unchanged. Independent review of this approved scope is pending at packet
seal; whole Story 12.2 remains in-progress for the separately owned >10,000 identity capability,
package/PD10 requalification, SM4 and whole-story review.


Independent approved-scope review three reopened the SDK prerequisite rows after Stage8. Parent
reproduced an old commit-CAS worker undoing Abort, a late installer leaving markerless scratch,
false-resolved Prepared transaction recovery, orphan external manifests, ordinary save/expiry and
omitted-key settlement defects. DI clock and bounded-cleanup/bulk-read corrections are included in
owner loop3. Stage8 remains exact-source passing test history rather than completed acceptance;
its packet and all earlier canonical stages stay unchanged. Folders source/test/docs were positively
preserved then restored only until the new SDK handoff. Whole-story open gates remain unchanged.


## Historical Stage9 approved SDK source qualification — 2026-10-20

Review-three corrections preserve delayed legacy-worker fences, settle supported transaction conflicts,
reclaim interrupted external manifests and preserve conditional guards across expiry. Production clock
registration, bulk final-value reads and bounded promoted maintenance are covered. The SDK owner records
its internal terminal-marker identity decision; payload-free fencing metadata remains distinct from
private payload cleanup. Folders continues to use the supported immutable source-deadline API.

[Stage9 qualification](evidence/story-12-2-approved-sdk-source-qualification-review3/qualification.json)
binds 1,661 consumer source/config files and
62 assembly/PDB files. Serialized Debug/source build completed with
zero warnings/errors. 4,195 consumer components passed with two
existing optional Domain skips. Full opted-in Aspire/Dapr/provider acceptance passed 1/1 with zero skips
in 337.824 seconds. SDK runtime PDBs match current source and captured
source/binary inventories remained unchanged through final consumer execution.

The first consumer live attempt failed during its second EventStore restart after DCP timed out
stopping the process. The second timed out waiting WorkspaceLockReleased: Lock completed, while
Release's successful domain evaluation was followed by terminal Rejected at EventsStored with a
redacted DaprApiException amid actor429 authority/archive errors. Both complete failed XML/logs,
the command timeline and zero-drift source/binary audits remain in the packet. The final pass uses
the same binaries, assertions and limits. Exact persistence-error and delayed-shutdown causes remain
unclassified; the rerun does not erase those failures. Other workspaces' applications are preserved,
with fixture-owned cleanup recorded separately.

The final SDK packet records 1,814 Client passes with 2 optional skips,
1,059 serialized Domain passes with zero skips, 56 unique required
live cases and 12 passing child invocations across
6 distinct exited-owner/recovery process pairs.
Parent independently checked 381 packet artifacts, final source hashes,
binary/PDB groups and raw worker end-state. Failed and interrupted earlier attempts remain evidence.

Current acknowledgement p95 is 4654.709 ms against 1,000 ms; Workers p95 is
1222.518 ms against 1,000 ms and status p95 is 639.668 ms against 500 ms.
All three observations exceed their budgets. Performance acceptance remains open
with prior observations retained. Package/PD10 gates retain their earlier failures against the unchanged
API and frozen candidate. All eight previous canonical stages and sealed packets remain unchanged.
Independent approved-scope review is pending at packet seal. Whole Story12.2 remains in-progress for
the separately owned >10,000 identity capability, package/PD10 requalification, SM4 and whole-story review.


## Approved SDK review4 follow-up

Stage9 stays historical exact-source local qualification, including Client1814/Domain1059/56 SDK live
cases and Folders4195/full live337.824s, plus its two preserved failed consumer live attempts. The
independent scoped review verified expired own-envelope settlement, late Completed/Aborted installers,
legacy compensation capacity and silent maintenance failure defects. SDK owner loop4 re-derives only
owned source from its positive76-file snapshot; all nine canonical stages and prior packets remain.
Parent must restore the nine consumer files after SDK qualification and correct the actual
OperationalStatusProjectionWriter registration/construction so its coordinator uses the configured
deadline clock coherently with the store and writer. Preserve existing constructors/callers where
possible; use the minimum SDK/domain DI seam, no consumer storage protocol. Add real service-registration
coverage exercising expired selected/inherited COW state, inclusive absence and a normal subsequent
delivery/rebuild; reflection alone is not acceptance. Requalify affected consumer components and opted-in
full real-Dapr fixture against final source/PE/PDB hashes, preserving failed attempts and performance
observations honestly. Parent owns this consumer correction; the SDK implementation child must not edit
Folders. Scope approval is unchanged. >10000 capture identity, package/PD10, all three latest over-budget
latencies and whole-story review remain open; no release/Git/pin/dependency work is authorized.


## Historical Stage10 approved SDK source and clock qualification — 2026-10-20

[Stage10 qualification](evidence/story-12-2-approved-sdk-source-qualification-review4/qualification.json) binds 1,663
consumer source/config inputs and 62 assembly/PDB files. Serialized
Debug/source restore/build passes with zero warnings/errors. All twelve owned C# files pass whitespace
and analyzer checks. 4,201 consumer cases pass, with two unchanged
optional Domain skips; the full opted-in Aspire/Dapr/provider acceptance passes 1/1 with zero skips in
334.315 seconds. Captured source/binary hashes and runtime PDB documents
match final execution. Six actual root/Server/manual construction x selected/inherited clock regressions
first failed against the old product at pre-deadline duplicate delivery, then passed after correction.
The writer now uses the SDK registered coordinator in DI and the supplied IUtcClock for manual construction.
Tests include inclusive absence, expired-source named rebuild, ordinary delivery and further named growth.
No public constructor or consumer storage protocol was added.

The SDK packet has 1,828 Client passes plus two unchanged optional skips, 1,061 serialized Domain passes,
66 unique required live cases and 20 passing distinct-PID child invocations: six production named
pending-owner/recovery pairs and four primitive late-installer/recovery pairs. The latter prove a fresh
epoch cycle; separately executed production named tests prove late accepted-owner cleanup after terminal
cleanup flags, ordinary delivery and a fresh named rebuild. Parent independently verified 315 artifacts,
3,838 source inputs, XML/binary groups, 31 PDBs and decoded raw worker states. Repeated named Abort is
the production late-scratch reconciliation trigger. Bounded payload-free fencing metadata follows the
SDK guide's retention/legal-hold, restore and drained-writer purge rules.

All nine prior canonical stages and sealed packets remain unchanged, including earlier failed consumer
live attempts and their unclassified causes. Current latency observations are retained without causal
attribution: command_acknowledgement p95=4784.157ms against1000ms, workers_operational_delivery p95=601.608ms against1000ms, status_read p95=120.761ms against500ms.
Independent approved-scope review is pending at seal. Whole Story12.2 remains in-progress for the already
approved separately owned >10,000 identity capability and its consumer qualification, package/PD10,
performance acceptance and whole-story review. Approval and Git/release/pin boundaries are unchanged.


## Approved SDK review5 follow-up

Independent scope review identified retained competing-owner CAS retry nonconvergence, lawful legacy
retirement size refusal, original scope/proof/pointer validation gaps, missing historical owner inventory
being treated as cleanup success, dependency cancellation misclassification and a late transport-fragment
coverage gap. Owner loop5 addresses the eight roots without changing frozen intent or approval. All14
Folders source/test/docs files, including the passing root/Server/manual clock fix, are positively preserved
for parent restoration after SDK handoff. All ten canonical stages and prior sealed packets stay exact.
Another session merged source changes during review; they are preserved, including external SDK authority/
selection and Folders file-identity/query changes. Current merged source differs from historical Stage10,
so it needs fresh source/PE/PDB/component/full live qualification. Do not reuse old counts as current results.
Already-approved separately owned >10000 identity consumption, package/PD10, performance and whole-story
review remain open. No Git/release/pin/dependency/content/performance redesign is authorized.


## Stage11 approved SDK review-five source consumption — 2026-10-20

[Stage11 qualification](evidence/story-12-2-approved-sdk-source-qualification-review5/qualification.json)
records restoration of all14 positively preserved consumer files, including the existing root/Server/manual
clock correction, followed by current merged-source qualification. Serialized Debug/source restore/build
passes with zero warnings/errors; all12 owned C# files pass whitespace and analyzer checks. Current
consumer components pass4248 cases with the same two optional Domain skips. The full opted-in
Aspire/Dapr/real-provider durability scenario passes1/1, zero failures/skips, in351.901 seconds.
The original assertions, deadlines and retry limits are unchanged. Retention and interrupted named
scratch retry/Abort acceptance rows above are now checked against this qualified source. Task36 stays open.

The packet freezes11998 ordinary source/config inputs across the current root-declared source graph
and546 runtime/test PE/PDB files. All271 portable PDBs match current source and their PE debug identities.
Final source/binary/submodule-head drift is zero. Incoming external commits advanced Folders to
`34f25c477d0497ebf5c115e7c7a58afb2f88245a` and EventStore to
`7217b5bf740b80ae92858378785b496e6b564285` during the build; all3881 source/config inputs
from the SDK review-five qualification still match. Two Client and two DomainService PE/PDB identity
groups are retained explicitly, each with identical complete source-document/hash sets across its groups.
No byte-identical-copy or new SDK source qualification is inferred from those revision metadata differences.

SDK handoff evidence retains Client1908 passes with two optional skips, Domain1072 passes,
73 required live cases and20 exited-worker invocations with20 distinct PIDs. Those SDK results are
linked separately from current consumer execution. The consumer live run proves stored TTLs,
source-preserving rebuild, changed host PIDs, interrupted generation resume, two concurrent Workers,
denials and storage faults. Fixture-owned resources are gone; unrelated applications were preserved.
An unrelated preexisting container disappeared concurrently; no operation against it was issued here.
The initial wrong-AppHost baseline selection and overbroad orphan-generator PDB inventory are retained
as nonqualifying diagnostics. There was no failed current-source consumer test attempt.

Current p95 observations are command acknowledgement2072.554ms against1000ms, Workers
operational delivery817.798ms against1000ms, and status read62.052ms against500ms.
Acknowledgement remains over budget, without causal attribution or redesign. All ten prior canonical
stages and all earlier sealed packets remain unchanged, including previous failures and latency findings.
The incoming Builds catalog now names EventStore3.126.0; historical3.125.1/PD10 failures are preserved
as historical results, and no current Release/package/PD10 requalification is claimed.

Whole Story12.2 remains in-progress for the separately owned beyond10000 captured-identity capability
and consumer qualification, Release/package/PD10, performance acceptance and independent approved-scope/
whole-story review. No Git mutation, dependency/pin change, publication, deployment, retained-event
replacement, content cleanup or publicv2 change was performed by this task.


## Continuation verification — 2026-10-21

[The continuation record](evidence/story-12-2-continuation-20261021.json) retains the exact focused
capacity command and raw result: one passing diagnostic, zero failures/skips, 11.147 seconds. It
confirms the existing SDK refusal after ordinary growth to 10,001; it does not close Task36. The
approved bounded captured-identity capability still requires a separate EventStore story/session,
followed by qualified Folders consumption. No further consumer implementation gap was identified.

The initial independent audit matched all 11,998 Stage11 source/config hashes, all 546 assembly/PDB
hashes, submodule HEADs, packet checksums and executed coverage for all five frozen matrix rows.
Concurrent external activity then advanced submodule checkouts and changed captured source/config
files; the final audit retains every difference. All captured binary hashes and sealed
packet checksums still match. No new integrated-source, package or live qualification is claimed.

Existing user changes, Stage11 and all prior evidence remain preserved. Only this continuation
evidence and note were added. Story12.2 and Task36 remain in-progress/open; package/PD10, SM4
acknowledgement performance and independent review remain open. No product source, frozen intent,
sprint status, dependency pin, retained event, public v2 surface or Git state was changed by this run.


## Active Task 36 resumption — 2026-10-21

The user explicitly resumed only Folders source-consumption qualification after the separate bounded
owner-identity implementation. Read the SDK spec and final evidence README under
`references/Hexalith.EventStore/_bmad-output/implementation-artifacts/`. The supported final SDK packet
is `evidence/folders-12-2-bounded-owner-identity/run-20261021T152119Z` there. Its 74/74 mandatory
cases and six passing process scenarios are SDK evidence; its broad regression gate remains failed
(`qualified: false`). Do not report those as newly executed Folders coverage.

Before any build in this resumption, the parent verified all 3,654 source hashes, 86 binary/PDB hashes
and 108 packet hashes with zero drift. Exact prebuild results and original inventories are preserved in
[evidence/story-12-2-bounded-owner-consumption-20261021/sdk-prebuild-audit.json](evidence/story-12-2-bounded-owner-consumption-20261021/sdk-prebuild-audit.json).
New consumer evidence belongs in that new directory; preserve every historical packet and user change.
The starting Folders HEAD is `12ed2391955960efe696078bd0c56e79c88edf9e`; SDK HEAD is
`ae917d3e4dbab20b8982c5222a13611c3e3faf35` plus its captured uncommitted prerequisite.
The supported full Debug/source graph must remain separate from package/release. No SDK source edits,
dependency pins, staging, commits, push, publication, deployment or public v2 exposure are authorized.
Do not rewrite retained source events, split owners, add a Folders persistence protocol or impose a
history cap. Use the existing Folders production handler. Complete Task 36 qualification and its
evidence now; preserve other already-qualified behavior and report separately open package/PD10,
SM4 and independent whole-story review gates. Relevant component and authenticated live acceptance
checks must execute on current binaries. Existing SDK process coverage may establish generic protocol
properties with explicit provenance; Folders coverage must establish its actual handler/dispatcher
and supported transport at the two required sizes.


Task 36 coverage composition clarification (2026-10-21): the user explicitly permits reuse of
SDK evidence. Both required sizes must execute through the real Folders handler and production
dispatcher with supported bounded transport; the current consumer component tests do this using
the SDK protocol DTOs and enforced batch implementation. The exact SDK buffered-owner Dapr cases
independently qualify persistence and true interrupted handoff at both sizes. The unchanged strict
Folders authenticated real-provider acceptance must also pass. An additional synthetic Folders HTTP
case at 10,001 is useful consumer integration evidence. A second synthetic HTTP case at 32,769 is
not a separate required gate; if not executed, report it explicitly and retain the component/SDK
coverage distinction. This clarifies the nonfrozen execution note; it changes no required size,
identity, source continuity, CAS, generation, batch, live acceptance or whole-story gate.

## Task 36 implementation checkpoint — 2026-10-21 (qualification incomplete)

The [new consumer packet](evidence/story-12-2-bounded-owner-consumption-20261021/README.md)
retains the bounded-part transport and durable-phase resume implementation, positive production-handler
component cases at10,001/32,769 positions, and4,249 frozen-graph component passes with the same two
optional Domain skips. No complete authenticated live case passed. Partial staging/recovery/Abort
observations are explicitly diagnostic; Task36 remains unchecked and Story12.2 stays in-progress.

Strict live attempts retained internal actor429, a canonical process-stop timeout and Dapr watcher
ENOSPC failures without weakened assertions/deadlines/retry limits. Shared inotify headroom fell to35;
no quota or unrelated process was changed. Complete strict and added10,001 HTTP acceptance remain open.

Concurrent external `/pushall` activity advanced root/SDK HEAD/index and dependency gitlinks, with
substantive source changes after the frozen build. Observed-at heads are root
`4b8822289b774b351d5c520ec1b6a25e404eb8b0` and SDK
`c91f520270388080b950feb869c68b43964285f3`. All546 frozen PE/PDB files remain unchanged; exact
original drifted inputs and generated source bytes are preserved. The current checkout is not qualified.
The implementation agent issued no staging, commit, push, branch or dependency-update operations
and made no SDK production-source edits.
SDK broad regressions, package/PD10, SM4 and independent scoped/whole-story review remain separate gates.

## Task 36 current integrated-source continuation — 2026-10-21

The [continuation packet](evidence/story-12-2-bounded-owner-consumption-20261021/continuation-20261021T2310/README.md)
records a current Debug/source solution restore/build with zero warnings/errors and 4,249 consumer
passes, zero failures and the same two optional Domain skips. Both 10,001/32,769 component cases pass.
All 12,118 frozen source/config hashes and 546 PE/PDB hashes remain unchanged through execution;
271 PDBs match 37,909 ordinary source-document copies. Generated PDB documents are outside that audit.
The SDK externally advanced through a planning-only commit; the exact head difference is retained.

Strict authenticated acceptance failed after 240.562 seconds waiting WorkspacePreparationRequested.
Logs directly observe internal actor HTTP429 at IdempotencyAdmissionActor/MarkRecoveryAsync,
admission-unavailable retries and a later HTTP202 without the event within the unchanged90-second wait.
No causal fix or retained terminal command outcome is inferred. The separate10,001 HTTP case failed
after1280.421 seconds at its first historical duplicate after an independent-owner generation.
Large staging, four actual managed-host replacements, same-session Commit/Verify and independent-owner
promotion passed; growth/repeated rebuild and final conflict assertions were not reached.

The [SDK handoff](evidence/story-12-2-bounded-owner-consumption-20261021/continuation-20261021T2310/sdk-inherited-owner-handoff.md)
retains tiny reproductions in distinct frozen Domain-test and actual live Server DomainService groups.
The latter matches all five production modules to live runtime hashes; its sixth module is the test harness.
Identical canonical bytes and an unchanged
captured owner checkpoint return AlreadyCaptured before inheritance, then IdentityConflict afterward;
named dispatch reports Failed/delivery_identity_conflict without entering the Folders fold callback.
The current capture map omits the inherited owner, causing the SDK journal's next-sequence branch
to reject its historical duplicate. Evolution-enabled and disabled cases reproduce the same result.
The live helper did not retain its ReasonCode; the exact layer/reason is independently established
by this frozen-DLL reproduction. SDK production edits remain outside this consumer session.

Exact fixture-owned cleanup passed; the unrelated Tenants AppHost kept its original PID.
No Folders or SDK product source, dependency pin, Git index, quota, assertion, deadline or retry limit
was changed. Task36 remains unchecked and Story12.2 remains in-progress for this SDK-owned correction,
exact consumer/strict live requalification, and the separately open broad SDK, package/PD10, SM4 and
independent review gates. No new complete authenticated live pass or32,769 Folders HTTP run is claimed.

## Current continuation checkpoint — 2026-10-22

The user resumed build 12.2 after pushing the qualification work; the starting clean HEAD is
`0a693eb48b37faed255c9f560ecbba691e3f1bf1`. The completed narrow
[inherited-owner qualification](spec-12-2-task-36-inherited-owner-requalification.md) and its
[handoff](evidence/story-12-2-inherited-owner-requalification-20261022T084922Z/handoff.md) supersede
the inherited-owner failure above for their exact source and runtime identities. That packet records
the complete authenticated 10,001-position recovery/inheritance case passing, both required consumer
component sizes passing, and fresh SDK controls; do not repeat the expensive passing capacity lane
unless source drift or a new failure requires it. Task 36 remains unchecked because strict authenticated
real-provider acceptance failed at BranchRefPolicyConfigured with internal actor429/admission-unavailable.

Continue the outstanding consumer qualification using the existing Debug project-reference seam to
`/home/administrator/projects/hexalith/eventstore` and the exact source/binary selection and audits in
that handoff. Check current source and runtime identities before executing; preserve all sealed evidence.
Investigate the strict admission failure and execute the unchanged strict real-provider lane on verified
outputs if feasible. Apply a necessary consumer correction only when the retained failure and current
source establish it; do not weaken admission, assertions, deadlines or retry limits. SDK implementation
remains separately owned: an SDK-owned defect requires an exact actionable handoff, rather than a
Folders workaround or SDK source edits in this session. Record current commands, outcomes and fixture
cleanup in a new evidence directory, and leave any unestablished gate open.

The existing no-publication/no-Git-mutation/no-pin-change scope remains in force. Package/PD10, SM4
and independent whole-story review retain their separate dispositions; this continuation does not
authorize a performance redesign or SDK release. Keep the original baseline and frozen intent intact.

## Strict consumer continuation result — 2026-10-22

The [continuation handoff](evidence/story-12-2-strict-continuation-20261022T100829Z/README.md)
retains two unchanged strict commands, their XML, actual runtime identities, sidecar launch arguments
and cleanup. Both failed in fixture initialization at `DaprClient.CheckHealthAsync` after the existing
100-second HttpClient timeout: 0 passed/1 failed/0 skipped in 226.062 and 211.058 seconds respectively.
Neither run reached authenticated commands or consumer acceptance. Sidecars handled SIGHUP during
startup and subsequently forced shutdown; signal sender and cause remain unclassified. The retained
component files and launch arguments establish no configuration correction. These startup failures
do not re-execute or classify the historical actor429/admission failure.

Parent independently checked all 546 sealed packet files, both XML outcomes, 15,098 current source/config
hashes, 584 actual runtime artifacts and 6,503 historical evidence hashes, with zero drift. The expanded
runtime inventory includes the actual launched Tenants executable directory. Exact fixture-owned cleanup
passed and the unrelated Tenants AppHost remained running. No product/SDK source, qualified binary,
dependency pin, assertion, deadline, retry policy or admission setting changed.

Task 36 remains unchecked and Story 12.2 remains in-progress. Next diagnose the sidecar signal/restart
sequence before rerunning strict acceptance, then address the separately owned historical SDK admission
failure if it recurs. Package/PD10, SM4 and independent whole-story review remain separate open gates.

## Task 36 selected-candidate consumer qualification — 2026-10-22

The [startup correction and qualification packet](evidence/story-12-2-sidecar-startup-20261022T105749Z/README.md)
identifies the earlier signal source: Dapr 1.18.7's hot-reload reconciler discovers the co-located
`accesscontrol` configuration as CREATED and sends SIGHUP to itself. Native controls using identical
deny-by-default configuration reproduce this after an unrelated watched-directory event; separating
configuration from resources prevents it while keeping HotReload enabled. Only the fixture's
`StartAsync` materialization paths changed. Policies, admission, assertions, deadlines and retries are unchanged.

The unchanged strict authenticated real-provider case now passes 1/1, zero failures/skips, in 343.629
xUnit seconds; the existing HTTP/rebuild helpers pass 22/22. All six concerns, clean-release closure,
replay/closed-binding denial, scoped task reads, two concurrent Workers, interrupted generation recovery,
changed host PIDs, rebuild, retention and safe storage/denial failures execute. The log has no
configuration-triggered SIGHUP or admission-unavailable message. The two new earlier initialization
failures remain retained: an initial compile/runtime reference-version mismatch, then a 90-second
disposable-provider readiness timeout. No cause or SDK admission repair is inferred from the final pass.

Task 36 is checked only for the selected qualified candidate. The prior sealed packet's passing 10,001
authenticated recovery/inheritance lane, both 10,001/32,769 production-handler component sizes and
explicitly bound SDK process evidence are reused; they were not rerun or counted as new coverage.
No 32,769 Folders HTTP run is claimed. All production/SDK runtime artifacts remain byte-identical to
that selection. Only the fixture test DLL/PDB were rebuilt against the exact selected references.
Seven pre-existing external SDK source deltas are preserved and excluded from any current-source
qualification claim; their newer runtime is neither built nor adopted here.

The final audit retains 15,098 source/config hashes, 584 runtime artifacts and one manifest with zero
execution drift; 186 actual module observations match, all nine sidecars separate configuration from
watched resources, and the new fixture PDB matches 20 ordinary/five generated documents. Both reused
packets and all 6,503 historical evidence hashes remain unchanged. Parent independently verifies the
same runtime/XML/diff/cleanup facts. Exact fixture-owned resources are gone; the unrelated Tenants
AppHost remains at PID 67730.

Whole Story 12.2 stays in-progress. Acknowledgement p95 is 7792.582 ms against 1000 ms; Workers p95
is 258.854 ms and status p95 is 79.891 ms. SM4 acknowledgement, Release/package/PD10, the separately
owned SDK broad-regression/current-external-source dispositions and independent whole-story review
remain open. No SDK source, dependency pin, Git index, release, retained event, content cleanup or
public v2 change is included.

## Current consumer verification — 2026-11-01

The [current consumer packet](evidence/story-12-2-current-consumer-20261031/README.md) records
zero-warning/error isolated Debug/source builds and 87 passing operational regressions, including
both required 10,001/32,769-position histories. Initial broad Server/Workers failures are retained.
Repo-local artifacts resolve fixture-path discovery; matching the local assembly Version to the
existing SDK catalog resolves the demonstrated Commons identity collision, with all four affected
Workers cases then passing. Every selected Hexalith dependency remains a project reference.

Current production discovery still fails before host startup: the SDK selective mapper now enters
the generic HTTP security boundary, whose endpoint source is unregistered in the bespoke Folders
host. A public-registration control removes that exception but the SDK inventory rejects 58 existing
endpoint classifications. The exact one-case reproduction, public seam probe, loaded assembly
identities and source/runtime hashes are retained. No compatible one-line consumer correction was
established; qualifying a selective SDK enrollment contract or a scoped whole-host security migration
remains an owning integration decision. No SDK workaround, internal registration or policy bypass
was applied, and no new full live qualification is claimed.

All 17,022 captured source/config inputs, 406 original and 403 corrected runtime/config artifacts
and the 174-file prior selected-candidate packet seal remain unchanged. Task runner/probe processes
exited; no live topology was started. The historical checked Task 36 result stays bound to its
selected candidate. Story 12.2 remains in-progress for current-source integration, SDK broad
regressions, Release/package/PD10, SM4 acknowledgement and independent review. Product/SDK source,
frozen intent, existing evidence, dependency pins, sprint status, public v2 and the Git index are preserved.

## Current selective enrollment boundary — 2026-11-01

The [fresh reproduction](evidence/story-12-2-selective-enrollment-20261101/README.md) confirms the
same startup blocker on unchanged source/runtime: production discovery has 0 passes, 1 failure,
0 skips in 0.334 xUnit seconds; public HTTP registration removes the missing-service exception but
reports 58 endpoint-classification findings across 55 paths. Audits retain zero drift across the
17,022 source/config inputs, 406 original and 403 corrected runtime/config artifacts, and 138 actual
probe output files. No new passing consumer or live qualification is claimed.

The owning SDK's approved 2026-10-29 Story 5.14 frozen boundary explicitly denies unclassified
endpoints even for valid credentials and forbids silent classification. Story 5.5 additionally retains
exact anonymous probes and the effective canonical callback policies. The earlier selective SDK
prerequisite approval still permits compatible capability repair; it does not authorize removing those
newer security guards. Satisfying them changes existing Folders root, probe, framework and implicit
human-endpoint behavior, beyond a missing-service registration correction.

The [bounded enrollment proposal](proposal-12-2-current-sdk-security-enrollment.md) recommends adopting
the existing SDK boundary through explicit Folders classification, with the exact route groups,
authentication/probe changes, owning files and qualification gates recorded for the owner decision.
Product/SDK source and frozen intent remain unchanged pending that decision. All diagnostic child
processes exited; no live topology started. Historical Task 36 remains bound to its selected candidate;
current-source integration, SDK broad regressions, Release/package/PD10, SM4 acknowledgement and
independent whole-story review retain their separate open dispositions.


## Approved current SDK enrollment checkpoint — 2026-11-01

The [enrollment packet](evidence/story-12-2-security-enrollment-20261101/README.md) records supported public SDK security/catalog registration, explicit human/channel/canonical classification, safe v1 denial envelopes, exact anonymous probes and authenticated historical aliases, and aligned monitoring/runbooks. The original one-parameter health mapping API remains available. Folders retains its bespoke processor and domain handlers; only the actual operational projection is declared in the product catalog. SDK guard internals are registered by its owning public composition seam.

On the selected project-reference dependency binaries, the final Server run has 1,296 passes and one failure out of 1,297, zero infrastructure errors/skips. The unchanged signed workload/human-receipt command-positive test remains failed because its isolated host has no catalog guard dependencies. Workers passes 162/162; operational histories pass 87/87, including 10,001/32,769 positions. Actual production-pipeline JWT/channel controls and real SDK projection/query catalog lifecycle controls pass; synthetic handler-fixture authentication is explicitly separate. SDK focused security passes 17/17 and public composition passes 1/1. Focused health/observability/docs passes 26/26; isolated testing defaults passes 2/2. Full Contracts retains 11 failures: six pending PD10 candidate/inventory/generator gates and five expectations affected by an external immutable CI pin. Frozen candidates/manifests and CI pins were not changed.

Current integrated-source qualification remains blocked: external commits advanced root-declared dependencies during execution, and Server/Integration/AppHost builds now fail CS0246 for Tenants' `RouteOperationContract` against the selected EventStore source. Final consumer fallback builds explicitly use already-selected project-reference dependency outputs with `BuildProjectReferences=false`; they do not qualify that current complete source graph. External commits also recorded some task changes; this task issued no Git mutation or dependency update. Exact heads, source/runtime hashes and commands are retained.

No new strict authenticated real-provider durability command was run, and no topology was launched. The initial exact-target Aspire baseline failed before startup with a mixed Commons CS1704; current AppHost compilation is blocked separately. Transient ENOSPC attempts remain retained alongside later complete RAM-TMPDIR results. Accurate command/admission declaration compatibility and genuine catalog activation/propagation still require the [owning integration handoff](evidence/story-12-2-security-enrollment-20261101/command-catalog-handoff.md); no guard or admission semantics were weakened. Story 12.2 stays in-progress for those gates, SDK broad regressions, Release/package/PD10, SM4 acknowledgement and independent whole-story review. Historical Task 36 remains bound to its selected candidate, with fresh selected-dependency component capacity coverage recorded separately.

## Current EventStore intake — 2026-11-01

The user requested pulling latest EventStore and identified its sibling-Tenants fallback. The non-recursive fast-forward-only pull is already current at `f704401ff9033919b1e9925564d814d7410bfe3d`. The SDK resolves `references/Hexalith.Tenants/src` beside its own checkout when nested Tenants source is absent; no nested submodule was initialized. Current SDK source contains `RouteOperationContract`, so the earlier CS0246 source-compatibility blocker is superseded.

Current-source Debug builds of Server.Tests, IntegrationTests, AppHost.Tests, core Tests, Workers.Tests and SDK DomainService.Tests pass with zero warnings/errors. Use `-p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:HexalithEventStoreVersion=3.130.2 -m:1 -nr:false`; the explicit existing catalog version prevents a Tenants-edge 3.129.0 assembly from replacing a 3.130.2 SDK runtime. No package/catalog file changes or `BuildProjectReferences=false` fallback are used. SDK tests need artifacts beneath the owning EventStore directory so source guardrails can find that repository.

Exact commands, failed initial runtime/path controls and current results are under `.artifacts/story-12-2-latest-eventstore-20261101T122955Z/verification/`. Current consumer outputs are under that directory's sibling `coherent/`; SDK outputs are under `references/Hexalith.EventStore/.artifacts/story-12-2-latest-eventstore-20261101T122955Z/`. The selection descriptor is `.artifacts/story-12-2-latest-eventstore-selection.json`. Preserve all earlier sealed packets.

SDK sibling/authoring/composition controls pass 38/38; SDK HTTP security passes 6/6; Server enrollment/composition controls pass 38/38; the previously failed signed-command positive passes 1/1. Workers passes 162/162; operational histories pass 87/87 (including 10,001/32,769); AppHost HTTP/rebuild helpers pass 22/22. The initial full Server run passed 1,314/1,315 with a stale source-text assertion looking for `routes.MapFoldersServerEndpoints()` instead of `app.MapFoldersServerEndpoints()`. Only that identifier was corrected, retaining authentication-before-authorization-before-mapping checks; the corrected full run passes 1,315/1,315 with zero errors/skips. No live topology has started.

**Integration corrections completed:** the initial current Integration suite had 781 passes and 72 failures, zero errors/skips. Four isolated fixtures were corrected while preserving all existing assertions, SDK credential-kind requirements and genuine production security qualification. The full corrected suite passes 853/853, zero failures/errors/skips; focused authentication/behavior passes 170/170 and topology passes 14/14. No SDK/admission redesign, production policy change or nested submodule initialization was needed.

- `tests/Hexalith.Folders.IntegrationTests/Routing/RoutingModeTestHost.cs`: the named human policy retains the SDK requirements and uses the fixture's existing explicit authentication scheme. Fixed-domain-context clients supply test authorization; authentication before rewriting, v1/v2 isolation, telemetry and disabled production v2 are preserved.
- `tests/Hexalith.Folders.IntegrationTests/Authorization/ContextReadAuthorityTests.cs`: explicit authentication and authorization middleware restore the second layered-authority evaluation without changing call-count, unavailable/denial or zero-observation assertions.
- `tests/Hexalith.Folders.IntegrationTests/EndToEnd/GoldenLifecycleParityTests.cs` and `MixedSurfaceHandoff/MixedSurfaceHandoffTests.cs`: the slim hosts compose authentication before the seam and authorization after routing. Transport-terminal, correlation, state and privacy assertions are unchanged.
- Topology needed only a corrected task-owned output depth: `.artifacts/story-12-2-latest-eventstore-20261101T122955Z-integration` matches the existing fixed-depth Memories lookup. All 14 topology cases pass; no topology, Memories or dependency-source edit was made. All 21 selected Integration Hexalith dependencies are project references.

The [current intake packet](evidence/story-12-2-latest-eventstore-20261101T122955Z/README.md) retains exact commands, failure controls, source/runtime fingerprints and all final results. Current Contracts builds cleanly and its full run has 623 passes and the same 11 pending PD10/CI conformance failures; frozen candidates and CI pins are unchanged. The approved qualification tasks and remaining live/governance/release/performance dispositions stay authoritative. Full live acceptance remains a separate unexecuted lane; its current fixture still assumes conventional output paths, so an isolated build alone cannot qualify loaded live host artifacts. Story 12.2 remains in-progress.

## Current-source live composition and remaining catalog prerequisite — 2026-11-01

The [new live packet](evidence/story-12-2-latest-eventstore-live-20261101T132347Z/README.md) records conventional Debug/source builds with the existing SDK version applied consistently, supported gateway HTTP/security/framework composition and one SDK-generated app-channel token shared by Tenants and its own sidecar. The original strict run failed startup in 249.092 seconds; the gateway lacked HTTP enrollment and Tenants discovery reported `channel-unconfigured`. Those composition gaps are corrected without changing the strict live test, its deadlines/retries, SDK guards, domain behavior or admission semantics.

Focused actual gateway startup/security and safe-denial checks pass 5/5, retaining both real SDK HTTP/authorization startup validators and empty public inventories. AppHost HTTP/rebuild/model checks pass 23/23. The full gateway test project remains compile-blocked at `NativeTaskAdmissionTests.cs:49`, CS7036, because its registry construction uses the obsolete public API; the current SDK additionally requires genuine catalog authority and declared admission adapters. The fixture is preserved, and the narrow fallback project's exact linked inputs are retained rather than represented as a full-suite pass.

The corrected unchanged strict real-provider run passes readiness for all seven sidecars, then fails 1/1 in 35.590 seconds with zero errors/skips: authenticated `BootstrapGlobalAdmin` expected HTTP 202 but received 503, code `unavailable`. The precise SDK `RouteCatalogUnavailableException` says an independently bound local catalog host identity and profile are required; the stack traverses the real route-catalog invocation and workload-assertion handlers. No post-bootstrap Folders commands or six-concern durability/SM4 assertions execute. Genuine catalog identity/profile, activation/propagation and compatible command/admission declarations remain an owning integration prerequisite; no authority was fabricated or guard weakened.

The corrected run retains zero drift across 18,833 source/config inputs, 969 runtime files in eight output groups and all 13 repository heads, plus 35 actual mapped-module records. Both disposable attempts removed their exact owned containers and temporary directories; Aspire reports no running AppHosts. Raw live stdout/XML remain in ignored local artifacts; only sanitized diagnostics, summaries, paths and hashes enter the sealed packet. The prior packets remain unchanged. Story 12.2, the final live-qualification task and the separate broad SDK, contract/governance, Release/package/PD10, SM4 acknowledgement and independent whole-story review gates remain open.


## Authorized catalog/admission compatibility continuation — 2026-11-01

The user explicitly authorizes bounded compatibility work to resolve the current live SDK refusal,
“An independently bound local catalog host identity and profile are required.” Resume from this spec
and [the current live packet](evidence/story-12-2-latest-eventstore-live-20261101T132347Z/README.md).
This authorization is recorded before implementation and supplements the existing prerequisite scope;
all other frozen requirements and the original baseline remain unchanged.

Authorized work: configure genuine SDK catalog identity/profile, activation and propagation through
supported public APIs; migrate affected legacy admission adapters to supported declared adapters
while preserving canonical intent, operation identities, retention and replay semantics; update
`NativeTaskAdmissionTests` for the supported registry API without weakening any assertion. This is
compatibility work only: no fabricated authority, guard bypass, new admission behavior, nested
submodule initialization, dependency-pin update, staging, commit, push, publication or deployment.
SDK-owned public capability work must be performed from its owning repository if necessary; stop
for an owner decision only if preserving existing semantics proves impossible.

### Active continuation tasks

Implement only this continuation over the preserved existing working tree; do not redo historical
completed tasks or rederive frozen decisions. Load the frontmatter context before implementation.
Inspect tracked guidance/configuration/tests in each owning repository before editing it.

- [x] Configure genuine independently bound local SDK catalog host identity/profile, activation and
  propagation for every affected actual runtime host, through supported public SDK composition.
  Preserve genuine workload/human/channel credentials, catalog receiver and invocation guards,
  production handlers and all strict live assertions/deadlines/retries.
- [x] Migrate affected legacy adapters to the SDK declared-adapter API with demonstrably identical
  canonical intent bytes/digests, operation/adapter/version identities, nullable semantics, semantic
  options, task/credential scope, retention and replay outcomes. No invented admission declaration.
- [x] Update `tests/Hexalith.Folders.EventStore.Tests/NativeTaskAdmissionTests.cs` for the supported
  registry API, preserving all original assertions and adding meaningful compatibility checks where
  needed. Run the complete gateway test project and affected regressions.
- [x] Build coherent conventional Debug project-reference outputs using
  `-p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:HexalithEventStoreVersion=3.130.2
  -m:1 -nr:false`; do not use `BuildProjectReferences=false`. Build actual Tenants/Memories runtime
  hosts as required by the unchanged fixture. Preserve all failed attempts.
- [ ] Rerun the unchanged strict authenticated real-provider durability acceptance after a pre-run
  source/runtime/repository-head audit. Retain actual mapped module identities and exact owned
  resource cleanup, commands/results, sanitized failure diagnostics and SM4 observations in a new
  sealed packet. Existing runner/audit scripts under `.artifacts/story-12-2-current-live-composition-20261101T133424Z/`
  and `.artifacts/story-12-2-latest-eventstore-live-20261101T132347Z/` may be adapted as task tooling;
  never alter prior sealed packets or publish raw secrets/protected bodies.

Implementation delegation executes the product/test changes and focused verification sequentially,
then reports exact changed files, commands, results and remaining work to the parent. The parent
performs final broad regression/live execution, independent diff/identity/cleanup auditing and packet
sealing. No simultaneous builds may mutate selected outputs. Keep the separate contract/governance,
Release/package/PD10, performance/SM4 and independent whole-story review gates explicit; story and
sprint status remain in-progress until their actual requirements pass.


### Catalog compatibility finding: qualification observer identity — 2026-11-01

The first new strict attempt is retained under `.artifacts/story-12-2-catalog-admission-20261101T141540Z/strict-real-provider.*`.
It fails initialization at the unchanged deadline: the existing test startup observer replaces the
actual named handler with a `DispatchProxy`, so the real SDK correctly refuses to match it to the
compiled `OperationalStatusProjectionHandler` declaration. All other cohort observations are
present. Source/runtime/head drift is zero; exact observed processes, containers, fixture directory
and SDK coordination directory are gone afterward.

Within the already authorized compatibility work, replace this test instrumentation with opt-in
`System.Diagnostics.DiagnosticListener` observation at the actual handler's `FoldCatchUpAsync` entry.
Keep the real handler installed and the SDK's exact owner/type guard. Emit only source-stream and
position metadata, only when the exact event is enabled; preserve the observer's existing
`registered` and `fold/source/position/processId` records and every capacity callback-count assertion.
Do not observe the writer's additional internal/Workers calls, change fold/persistence behavior or
admission, or remove the independent content-clock setup. Add focused observer/real-handler identity
and exact-count controls; rerun affected regressions and strict acceptance on the audited correction.
This is an execution correction to preserve the existing test semantics, not a new owner approval
or a change to frozen intent. The parent retains broad/live/audit/sealing ownership; implementation
delegation makes this focused correction and focused checks sequentially, then hands outputs back.


### Catalog compatibility finding: preserve existing Folders human admission — 2026-11-01

The second strict run commits all eight genuine SDK host readbacks, then the unchanged human
`BootstrapGlobalAdmin` call receives 403/unavailable. Tenants independently pins this operation to
internal ingress; the SDK requires genuine workload/service admission for an internal operation.
Its existing bound-workload bootstrap is a different flow and cannot transparently preserve the
fixture's direct human POST assertion. Preserve that owner policy and the strict fixture pending
an owner decision; do not manufacture service claims, reopen the operation, or substitute a receipt
for the unchanged HTTP assertion.

Before concluding this bounded continuation, correct our new Folders operation metadata against
its actual existing caller: `FoldersGatewayHumanBearerHandler` relays the validated public human
bearer for shipped v1 commands. Declare those existing commands as public ingress, retaining
existing downstream layered authorization and eligibility for previously supported human callers;
leave computed owner queries and legacy/named projections internal. This corrects inaccurate new
metadata; it does not authorize new routes, grants, public v2, CLI/MCP exposure or frozen candidate
changes. Add independent human-forwarder/actual operation-policy and disabled-v2 checks, retaining
all original assertions and canonical intent byte goldens. Rerun the gateway and affected regressions,
then the unchanged strict command on the corrected audited catalog, retaining its actual outcome.


## Catalog/admission compatibility qualification and owner boundary — 2026-11-01

The [sealed catalog/admission packet](evidence/story-12-2-catalog-admission-20261101T141540Z/README.md)
retains the authorized public-SDK compatibility repair, all failed attempts and the final audited
Debug/project-reference selection. All eight actual catalog hosts commit independently observed
identity/profile and generation readbacks. All 18 adapters retain their original canonical methods;
36 original-binary cases prove identical bytes, options, nullable values, scope, identities and
retention. Original native-task assertions and strict/capacity acceptance sources are unchanged.

Final full results: gateway 73/73, Server 1,324/1,324, Workers 162/162, Integration 853/853, operational
histories 87/87 (including 10,001/32,769 positions), AppHost helpers 32/32; focused SDK contract/codec
125/125 and observation/registry 24/24. Builds have zero warnings/errors. The final symbol audit
resolves the retained stale Workers.Tests-copy control; all 106 actually mapped modules have exact
SHA-256/PDB/MVID identities, all 36,451 ordinary PDB document records match current source, every
selected SDK copy is version 3.130.2 and all 28 SDK artifact names have consistent bytes. Ten checked
consumer/runtime project graphs contain no Hexalith package edges. No package release is claimed.

The unchanged strict command was run four times: first initialization failure (249.412 seconds)
from the obsolete test proxy, then three retained bootstrap failures after actual catalog commits
(36.262, 37.004 and final 36.009 seconds). The proxy was replaced by opt-in metadata diagnostics at
the real handler entry; original callback-count assertions remain. Folders command operation
metadata now reflects its existing public human bearer forwarding, with computed owner queries
and projections internal and public v2 still disabled. The final run has zero source/runtime/head
drift across its captured inputs, all 13 heads unchanged, and exact cleanup of every observed
owned process/container/fixture directory/SDK directory; Aspire reports no running AppHosts.

**Owner decision is now required under the user's no-new-admission-behavior constraint.**
Authenticated human `BootstrapGlobalAdmin` still expects HTTP 202 but gets 403/unavailable.
Tenants explicitly declares bootstrap internal, independently pins that in its owner tests, and
the SDK requires genuine workload/service admission for internal operations. The unchanged
public command endpoint is human-only. No host/catalog configuration can preserve both this
manual human admission and the existing owner/SDK policies. The packet's `owner-decision.json`
identifies exact sources and the existing verified-human bound-workload bootstrap alternative.
Do not change Tenants ingress/grants, synthesize service claims or origin proof, replace the
HTTP assertion with a fabricated response/receipt, or weaken a guard. A fixture bootstrap-flow
migration to the existing owner implementation needs an explicit decision; it must retain real
administrator validation/attribution and reconcile actual isolated Dapr targets. No such migration
was performed. No post-bootstrap Folders command, six-concern strict body or new SM4 measurement
executed, and no new 10,001/32,769 authenticated HTTP coverage is claimed.

The original live-qualification row stays unchecked; Story 12.2 and sprint status remain in-progress.
Contracts retain exactly the prior 11 PD10/immutable-CI-pin failures (623/634 passed). Contract/
governance, Release/package/PD10, SDK broad regressions, performance/SM4 and independent whole-story
review remain separate open gates. The original baseline and all frozen text, existing packets,
retained event identities, dependency pins, staged contents and Git refs are preserved. No staging,
commit, push, branch change, nested submodule initialization/update, publication or deployment.


## Approved verified administrator bootstrap migration — 2026-11-01

The user answered **“approve”** to the proposed migration of fixture bootstrap to Tenants' existing
bound workload flow with verified administrator attribution. This approval resolves the owner
decision in [the catalog/admission packet](evidence/story-12-2-catalog-admission-20261101T141540Z/owner-decision.json)
and is recorded before implementation. It permits replacing the fixture's obsolete manual human
`BootstrapGlobalAdmin` POST/202 expectation with the actual owner-hosted, cryptographically verified
human-to-bound-workload bootstrap. It does not permit changing Tenants' internal ingress, grants,
SDK guards, canonical intent/identities/retention/replay, or the strict durability test body.

### Active approved bootstrap continuation

Resume only these tasks over the preserved working tree. Prior tasks/evidence remain authoritative
for their exact selection; do not redo or rewrite historical work. Preserve the original baseline,
frozen block, original capacity/strict test sources and all domain/replay/retention assertions.

- [x] Configure the actual Tenants host's existing supported bootstrap settings and real OIDC
  administrator credential, workload assertion, configured RSA proof key and private actual
  delivery receipt. The existing installed bearer handler must validate exact administrator
  subject/admin/payload and credential kind before it issues bound proof. The gateway must verify
  the existing exact proof/caller/domain scope through `/api/v1/commands/bound/workload`. Never
  synthesize a successful response, subject, service surface, authority or origin proof in Folders.
- [x] Migrate `FolderDurabilityAcceptanceFixture.BootstrapAsync` to observe the real owner flow's
  accepted HTTP status, attempt/root/principal binding, terminal delivery and original persisted
  source/admin authority before tenant creation. A private receipt is delivery evidence, never
  the authority or a fabricated replacement HTTP response. Preserve existing command/status
  assertions outside the approved bootstrap transport change, and preserve existing deadlines/
  retries and the strict six-concern durability body.
- [x] Reconcile actual fixture-isolated Dapr targets through supported public configuration/API
  seams; preserve canonical defaults and logical workload audiences. If a missing target-selection
  setting requires bounded compatibility in Tenants or the SDK, inspect and edit from that owning
  root-declared repository and test preserved defaults plus exact selected target. Keep source-read
  and bootstrap guards intact; do not initialize nested submodules or alter dependency pins.
- [x] Add focused positive and refusal controls for genuine administrator attribution, receipt/
  generation binding, missing/wrong credentials or proof, original direct-human bootstrap denial,
  exact target selection and terminal persisted-source checks. Keep all earlier adapter byte/digest/
  scope/retention assertions and disabled public v2. Real signed surface profile configuration for
  existing human/service callers may be aligned through the actual issuer's supported settings;
  provision only the exact existing owner-required workload client/scopes in the new isolated issuer;
  never widen business/domain authority, assign the service account the human administrator role/
  subject, or relabel a human as a workload to pass a guard.
- [x] Build coherent conventional Debug/project-reference outputs using the existing
  `-p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:HexalithEventStoreVersion=3.130.2
  -m:1 -nr:false`. Build actual Tenants/Memories runtime hosts as required. Run complete gateway
  tests and affected Folders/owner regressions; no binary-only fallback or pin update.
- [x] Rerun the unchanged strict authenticated real-provider durability class on final audited
  assemblies. Retain every failed attempt, actual source/runtime/host identities, real committed
  catalog/owner bootstrap evidence, SM4 observations and exact owned cleanup in a new sealed packet.

Delegated implementation handles product/test changes and focused checks sequentially, then hands
selected outputs back. Parent owns final broad/live/audit/sealing. Do not run simultaneous builds
on selected outputs. Task tooling for this approval is `.artifacts/story-12-2-bound-bootstrap-20261101T164412Z/`; existing
`run.py`, `audit.py`, `live.py`, `summarize_live.py`, `verification-plan.json` are copied there for
adaptation without modifying old packets. Selection descriptor: `.artifacts/story-12-2-bound-bootstrap-selection.json`.

Continue autonomously within this approved migration, keeping the remaining contract/governance,
Release/package/PD10, SDK broad-regression, performance/SM4 and independent whole-story review
gates separate and open until actually satisfied. All no-staging/no-commit/no-push/no-publication/
no-deployment/no-pin/no-nested-initialization constraints persist. Stop for a new owner decision
only if another necessary change cannot preserve the authorized semantics.

### Supported transport timeout compatibility within the approved bootstrap

The retained `strict-real-provider-bound-import` attempt committed all eight catalogs but the
owner rejected `HttpClient.Timeout == Timeout.InfiniteTimeSpan` before administrator validation.
The installed standard HTTP resilience handler deliberately uses that transport representation;
its existing finite total-request timeout remains in named `HttpStandardResilienceOptions`.
Resolve that actual configured budget through public options only for the unnamed client's
infinite transport timeout, and pass it to the existing positive finite catalog-readiness guard.
Preserve existing finite-client timeout behavior, installed handlers/retries, cancellation,
credential/proof/domain guards, and all fixture deadlines. Do not invent a fallback budget or
accept unbounded readiness. An actual production-default registration control must bind the
options name to the public pipeline name and prove the configured budget, cancellation and
expiry. This is necessary compatibility for the approved existing owner bootstrap, not an
authorization-policy or acceptance-body change; it is recorded before the repair.

### Genuine isolated Dapr transport and caller binding

The retained `strict-real-provider-bound-timeout` attempt produced a real owner HTTP 403 receipt.
Actual sidecars reported mTLS disabled; exact-version Dapr source shows that unidentified peers
receive the SDK-rendered global deny policy before application delivery. This is a source-backed
diagnosis, not a captured Dapr rejection body. The existing signed-caller check also requires the
real isolated physical AppId; canonical `tenants` alongside `tenants-<runId>` cannot pass it.

Continue the approved compatibility work by configuring genuine task-owned local Dapr identity
infrastructure through the public Platform/Sentry APIs, with real issued peer certificates and the
actual isolated namespace/trust domain bound consistently to SDK-rendered policies. Preserve global
deny and every existing operation/domain grant. Use the real isolated Tenants OIDC client identity
for its assertion, proof caller and exact gateway allow-list/public-key entries; retain the canonical
`tenants` defaults and logical `eventstore` audience. Select owner source-read proof caller from that
same existing issuer configuration, defaulting to `tenants`; never drop the physical-caller check.
The human administrator, canonical command/intent/operation/event identities and retention/replay
inputs remain unchanged. No preexisting authority is inferred or forged.

Keep existing startup/request/status deadlines and command retry behavior. Verify the actual model
and positive/refusal identity controls, then run the affected regressions and unchanged strict class.
Retain exact issued-infrastructure source/runtime identities and returned private directory paths;
after application disposal remove only those task-created paths and prove their absence. Certificates,
keys, tokens and protected bodies stay private. This continuation records the required transport
configuration and default-preserving proof selection before implementation.

Use Platform's canonical consumer source links and public local infrastructure helper. Its application
namespace is currently hardcoded to `default` in both early and generated-sidecar callbacks; add one
optional public application namespace parameter defaulting to `default`, thread it through the canonical
helper/subscriber, and test both preserved defaults and selected/refused values from the owning Platform
repository. Control-plane namespace remains `default`. Preserve the SDK workload ACL trust domain
`public`; Sentry's `localhost` control-plane trust is a separate setting, as verified against the installed
exact-version Dapr source. Do not guess or unify these distinct trust domains.

### Bounded request framing compatibility

The retained mTLS attempt reached successful `/process` and `/project`, one stored/published event and
actor completion, then the owner observed HTTP 400. The unchanged Dapr policy logged an endpoint
retry after its existing five-second attempt timeout. Exact-version source treats unknown-length
`JsonContent` as streaming with no replay buffer; the consumed-body retry explanation is an inference
because the actual rejection body was not captured. Cleanup also hit the existing Docker infrastructure
deadline; retain that failure independently and do not extend it.

Before the next run, buffer the existing exact bound bootstrap JSON through public `HttpContent`
APIs, bounded by the endpoint's existing 1,048,576-byte request limit and caller cancellation. Verify
unchanged serialized bytes, valid proof and repeatable content with a computed positive Content-Length.
Preserve the unnamed client, installed handlers, every timeout/retry policy, original message/correlation
IDs, canonical intent and retention/replay semantics. Add no owner retry, admission key, grant or
substitute receipt. The next isolated run must still observe real HTTP acceptance, terminal delivery
and exactly one original human-attributed event/projection; completion from this failed attempt cannot
substitute for acceptance. This transport-only compatibility repair is recorded before implementation.

### Existing durable bootstrap projection configuration

The buffered and diagnostic attempts establish genuine admission, one source event and cached exact
message replay, but synchronous compatible legacy projection delivery exceeds the unchanged five-second
Dapr attempt budget before mediator exit. Preserve every transport timeout/retry and all guards. Select
the SDK's existing durable polling mode only for the disposable fixture's `global-administrators` domain:
`EventStore:Projections:Domains:global-administrators:RefreshIntervalMs=1000`. This matches the existing
one-second activation recovery cadence; other domains, production defaults and all Folders delivery modes
remain unchanged. This is existing supported configuration, not a new admission path or projection engine.

The actual production pipeline writes the mandatory activation outbox before both legacy and fenced actor
routing, including null admission keys; polling awaits persisted identity registration and executes the same
guarded delivery with host cancellation. Do not disable delivery, add a detached task, change admission keys,
or bypass finalization/persistence checks. HTTP 202 remains actual command acceptance, never proof of
projection completion. Require terminal command completion and original retained human-attributed source,
then observe initially absent administrator projection within the SAME existing 90-second deadline before
`CreateTenant`. Any present malformed/contradictory authority still fails closed, and successful authority
must satisfy the unchanged exact original membership/version/time assertions. Record this fixture-only
existing scheduling selection and meaningful configuration/absence/refusal controls before implementation;
the frozen persistence-before-delivery-ack requirement and strict six-concern body are preserved.

### Natural framing for isolated proof-refusal controls

The retained `strict-real-provider-durable-bootstrap` attempt obtained actual HTTP 202 and terminal
Completed. Its newly added missing-proof control observed 403; the wrong-proof control then observed
500. Gateway logs show repeated actual `ForbidResult` responses, followed by the real owning Dapr
sidecar's bound-endpoint circuit opening. Exact installed Dapr source confirms that the unchanged
unmatched retry policy retries buffered application 403 responses, counts them against the breaker,
and maps the open circuit to HTTP 500. This is transport behavior, not failed proof validation.

Preserve all configured policies, attempt counts, deadlines and both exact 403 assertions. Only these
two intentionally invalid fixture probes use fresh natural `JsonContent` framing through the supported
HTTP API, rather than the general helper's prebuffered `ByteArrayContent`. Add an optional content
factory to the existing helper so every eligible existing outer retry still gets identical JSON with
the same message/principal/header/catalog identities; retain its 75-second bound, the caller's existing
90-second bound and its unchanged 429/eligible-503 rules. Dapr forwards the actual application refusal
for non-replayable streaming content. Valid owner admission remains bounded and buffered with all
existing retries and exactly-one retained-event checks. Verify natural framing, identical fresh content
on eligible retries, cancellation and single 403 observation with focused controls. No guard, grant,
admission behavior or frozen strict/capacity test body changes. Recorded before implementation.

### Preserve the catalog-bound owner source runtime address

The retained `strict-real-provider-proof-framing` attempt passed genuine bootstrap acceptance,
terminal completion, all credential/proof refusals, exactly-one original human-attributed source and
persisted administrator authority. Its subsequent authenticated `CreateTenant` failed because the
Tenants domain-service administrator verifier could not read current native source authority. The
actual exception is `RouteCatalogUnavailableException: The v2 invocation uses a different Dapr
runtime endpoint.` The legacy owner source HTTP registration replaces configured `localhost` with
`127.0.0.1`; the SDK intentionally binds the exact configured scheme/host/port and refuses that alias.

Within the already approved public transport/catalog compatibility, remove only this legacy hostname
rewrite and use the actual configured Dapr HTTP endpoint/port with the existing canonical fallback.
Read these through the host configuration used by the SDK's state coordinates; preserve physical
gateway target selection, logical audiences, proof scope, all retries/deadlines and every native source
proof/final observation check. Do not accept aliases in the SDK guard or fall back to projection-only
authority. Verify preserved no-override defaults, exact localhost/IPv4 endpoint selection and refusal
on a catalog/address mismatch. Record this bounded owner registration repair before implementation;
the original strict and capacity bodies and all other frozen requirements remain unchanged.


## Saved continuation checkpoint — 2026-11-01

Implementation stopped at the user's explicit “stop and save” request. The subsequent “push all
changes” instruction authorizes staging, Conventional Commits and Git push of the saved working
changes and their workspace references. It supersedes the earlier no-commit/no-push restriction for
this Git operation only; package-version pins, nested initialization, publication and deployment
restrictions remain. The evidence describes the executed pre-commit selection, not a later merge HEAD.

The [new sealed checkpoint](evidence/story-12-2-bound-bootstrap-20261101T164412Z/README.md) and its
[resume guide](evidence/story-12-2-bound-bootstrap-20261101T164412Z/RESUME.md) retain all nine live
attempts, earlier build/test/preflight failures, exact commands, actual catalog/owner delivery evidence,
source/PE/PDB/native/container identities and exact cleanup. All prior evidence seals remain unchanged.

The supported bound owner bootstrap is verified: actual 202, terminal Completed, all four credential/
proof refusals, original retained source exactly once with the real human attribution, committed
catalog generation binding and persisted administrator membership. The final unchanged strict class
still fails during fixture `CreateTenant`: the first native authority head read returns 200 in
2516.8947 ms; the second read is pending when the existing five-second domain-service budget expires.
The strict six-concern body was not entered. No observation, proof, final head check, timeout or guard
was removed or relaxed. The repeated handler source frame is explained by async lease cleanup/rethrow,
not demonstrated duplicate registration. No further bounded composition mismatch or justified repair
was found; the retained timing/IL checkpoint explicitly does not prove all executions inherently exceed
the budget. Resume diagnosis from this evidence rather than rerunning unchanged code for a lucky pass.

Final executed regressions: gateway 73/73, Folders Server 1324/1324, Workers 162/162, integration
853/853, operational core/history 87/87, Tenants Server 1666/1666, preserved Tenants AppHost defaults
3/3, combined fixture helpers 82/82, SDK descriptor/codec/validator 125/125 and registry/observation
24/24. Contracts remain 623/634 with the same 11 PD10/immutable CI-pin failures as before.

The final selection audit binds 108 actual mapped identities, 2713 unique runtime files and 43494
ordinary PDB source records. All ordinary checksums match; five generated metadata-only build profiles
reconstruct exactly from PE attributes. Actual live SDK copies are byte-identical at 3.130.2.0; separately
built regression metadata profiles remain explicit. All 15 checked project asset graphs contain only
Hexalith project edges. Source/runtime/head drift during the final run is zero; every observed owned
process, container and temporary directory is gone. The exact task-created PDB tool directory was
also removed after its source was preserved.

The original live-qualification acceptance row remains unchecked and Story/sprint stay in-progress.
Contract/governance, Release/package/PD10, broader SDK and Platform regressions, performance/SM4 and
independent whole-story review remain separate open gates. No whole-story review or release approval
is implied by the successful bootstrap or by the requested Git push. No new performance result exists.

## Native authority read continuation — 2026-11-02

The user approved bounded performance work on the Tenants/EventStore native administrator source read,
with every source/proof/final observation, the five-second domain-service deadline, the SDK guards and
the unchanged strict assertions preserved. EventStore now holds one durable catalog fence across that
complete Tenants read and revalidates local catalog facts for each exact workload stream-read request.
The supported AppHost EventStore sidecar port is configurable for disposable live runs; its default is
unchanged. After the user's bootstrap-path continuation, EventStore also computes each declaration
digest once per fresh local catalog observation, preserving the same exact inventory checks. Focused
SDK, Tenants and AppHost checks passed as recorded in the [new evidence packet](evidence/story-12-2-native-read-performance-20261102/README.md).
The EventStore native stream route now groups its same ordered actor proof inspections and bounded
range into one actor turn; the external head/page/head and final observation remain unchanged.

Two strict class attempts failed before `CreateTenant`. The default port collided with an already
running local AppHost. With an isolated port, a real `BootstrapGlobalAdmin` actor completed in
5,478.4952 ms, beyond Dapr's existing five-second attempt, and bootstrap retries ended with HTTP 500
`ERR_DIRECT_INVOKE`. A third current-source run with isolated port 35992 passed genuine bootstrap
(202, terminal Completed, retained source, projected authority) and reached `CreateTenant`. Its native
head returned 200 in 869 ms and page in 2,446 ms, but the next head was pending at the unchanged
five-second domain-service deadline. `CreateTenant` returned HTTP 500 `unavailable`; the strict
six-concern body was not entered. The third run used byte-identical SDK copies and cleaned its
owned resources.

A fourth current-source run with the grouped actor reads passed genuine bootstrap, completed
`CreateTenant` in 4,396.9103 ms, and entered the strict body. Its tenant-authority assertion passed.
The first `InitializeOrganizationAclBaseline` command then exceeded the unchanged 40-second test
client timeout. The actor completed the command; the gateway spent 16,801 ms on projection delivery
and additional time on repeated execution-fence awaits before the client canceled. The body stopped
before the six durability concerns and SM4 measurement. The [packet](evidence/story-12-2-native-read-performance-20261102/README.md)
records source/runtime hashes, exact command outcomes and cleanup. General command/projection
delivery optimization lies outside the approved native-read and bootstrap scopes. The strict task,
SM4, broad SDK/Release/package/PD10 and whole-story review gates remain open; Story and sprint status
remain in-progress.

## Further performance continuation — 2026-11-02

The user subsequently approved broader EventStore command/projection-path optimization, preserving
the existing 40-second HTTP limit, durable guards and strict assertions. That approval supersedes
the prior scope exclusion above. The disposable fixture now selects the SDK worker-backed response
path for `folders` only; Tenants and global-administrators retain their synchronous behavior.
EventStore's optional domain set preserves the switch's previous all-domain behavior when omitted.
Catalog structural validation is reused only for a previously validated exact activation binding
and freshly recaptured complete local identity. A native page's fresh range metadata read now
validates its inside proof, retaining the initial and post-read proofs and all event checks.
Immutable loaded-assembly route declarations are computed once by the domain observer while the
executable bytes, installed receivers, component, ACL, configuration and identity are freshly
checked each time. Focused affected EventStore tests passed 61/61 in the isolated candidate.

Further strict runs still have no passing live qualification. Bootstrap completed in 1,682.1 ms
with the catalog cache, but `CreateTenant` then exceeded the unchanged five-second native-read
budget. A later run completed `CreateTenant` in 4,843.9272 ms; `AddUserToTenant` exceeded the same
budget before the first organization command. Two other attempts failed during Dapr startup,
before code acceptance could be assessed. A trial two-second projection retry interval was
removed because its run failed during startup. The exact runs, source/runtime hashes, private
log hashes and cleanup are in the [evidence packet](evidence/story-12-2-native-read-performance-20261102/README.md).
The shared merged checkout's targeted EventStore test build is separately blocked by 11 existing
`CS0246 PlatformScope` errors from its pinned Tenants/EventStore graph. The unchanged strict six
durability concerns and SM4 measurement remain unqualified; Story and sprint remain in-progress.


## Exact-byte catalog validation continuation — 2026-11-02

Within the approved native-read and broader command/projection performance scope, reduce repeated
deterministic canonical catalog validation in the owning EventStore codec. Retain one private bounded
validated encoding and reuse semantic/canonical validation only for exact copied-byte equality. Parse
each call into a fresh independent model; compare every supplied expected root. Changed buffers,
returned-model mutation, malformed alternate documents, all supported schema generations and concurrent
callers must preserve existing refusal/encoding semantics. Backend reads, fresh independent host capture,
trust checks, durable lease/CAS transitions and every native proof/final observation remain unchanged.
This is an implementation continuation, with focused and live qualification still pending; all prior
evidence packets, strict/capacity test bodies, deadlines and separate whole-story gates remain unchanged.


## Current canonical bootstrap fixture continuation — 2026-11-02

The audited current-SDK strict run reached ready sidecars and committed catalog hosts, then timed out
waiting for the owner bootstrap receipt. The current owner requires the exact signed human permission
`global-administrators:bootstrap`; the disposable imported administrator has only the legacy generic
command permissions. Align only that already-designated real administrator's disposable issuer claims
with the exact existing bootstrap/create/add-user and platform-status read operations, preserving its
subject/admin marker, other users and workload separation. Remove its retired public `system` grant;
retained source/actor/projection storage identities stay unchanged.

Use the supported explicit platform namespaces for the existing fixture tenant commands, bootstrap
refusal controls and status reads; tenant status reads name the exact managed tenant. Observe the
original retained administrator source through the existing owner-bound workload stream-read route,
using the actual isolated owner sidecar, real issued assertion, existing configured RSA scope key and
committed catalog binding. Preserve all original acceptance/denial assertions, strict/capacity bodies,
source checks, deadlines/retries, business effects and production authentication/proof guards. Qualify
the actual realm transformation and request construction seams before the parent's next audited live
run. This fixture compatibility work adds no production grant, admission path or storage protocol.

Focused qualification reproduces the original missing signed permission from the actual realm import
(one expected failing control), then passes 94/94 fixture realm/scope/native-proof/HTTP/rebuild/catalog/
mTLS cases and 69/69 unchanged current-source Tenants bound-bootstrap/readiness/command-transport
cases, with zero errors or skips. Final Debug/source builds pass with zero warnings/errors. The earlier
new-test null-check analyzer failure is retained separately. No runtime product source or strict/capacity
body changed in this fixture continuation, and no live topology was started. The parent still owns the
audited real-provider strict rerun, SM4 and final evidence sealing; the live acceptance row stays open.


### Current canonical command model validation compatibility

The third audited strict run observed the real owner receipt and terminal delivery, but the unchanged direct-human refusal
probe received 400 before its required 403. The production MVC model filter resolves the existing
`SubmitCommandRequestValidator`, whose tenant-only regex rejects the now-required `platform:`
namespace before committed catalog/ingress authorization. Bound bootstrap wraps a different DTO and
did not traverse that validator. The owning SDK's approved canonical-scope migration explicitly
retains cataloged platform commands. Align only this structural rule with the supported canonical
platform namespace grammar, preserving existing field bounds, tenant/domain validation and every
subsequent catalog/permission/credential/ingress guard. Reproduce the actual model-filter path and
qualify direct-human internal refusal, public platform-command controls, missing/wrong permissions,
unknown/malformed/mixed scopes and reserved storage refusal with zero denied command effects. No
fixture assertion, production authorization policy, original intent or deadline changes are permitted.

The exact launched Gateway binary independently reproduces a single structural failure on `Tenant`
for that canonical bootstrap DTO; the candidate accepts its structure. The unchanged authorization
pipeline then produces the required human-bootstrap 403. Before correction, the production MVC/JWT/
catalog component matrix had five scope-related 400 failures in ten cases. Final affected SDK checks
pass 51/51, zero failures/errors/skips, including permitted public platform command controls and all
credential/permission/scope refusals with zero denied command effects. Final source builds pass with
zero warnings/errors. Component terminals are counted test doubles; no live domain acceptance is
inferred. The parent still owns rebuilt-runtime strict execution and every original source assertion.


## Nonfrozen independent source-head resume checkpoint — 2026-11-02

The fourth audited current-source strict operational attempt passed genuine owner bootstrap (202,
terminal Completed, all four original refusals, retained native source and projected authority), tenant
creation/membership and organization baseline/grant commands. It entered the unchanged body and failed
at the first unknown-profile `ConfigureProviderBinding` PUT's required 403 control, with outer
`HttpRequestException` / `HttpIOException: ResponseEnded`. This was not folder creation. The attempt
finished with one failure, zero errors/skips in 233.011 seconds; no SM4 measurement was reached. The
parent retains exact private logs/XML, runtime/source/head no-drift receipts and empty owned cleanup.

Read-only diagnosis separately established that the Folders independent organization source-head
reader makes a raw `GetCurrentSequenceAsync` actor invocation. The mandatory current SDK catalog
handler refuses it before authority acquisition/transport because it has no proven typed catalog
scope. The source trace contains one structured refusal event with the phrase repeated in three
fields. Domain `/process` then returns 503 and the actor records Rejected. The retained observations
do not connect that refusal to the outer response ending prematurely; `ResponseEnded` remains an
independent unresolved cause. No speculative framing, retry or timeout repair is justified.

The [source-head prerequisite proposal](proposal-12-2-current-sdk-source-head-transport.md) requests
an owner decision for a public independently configured read-only SDK composition, actual Folders
workload/key/grant enrollment and authoritative-zero/absence distinctions. Existing public workload
stream/proof components are available, but the current raw metadata registration also fails closed,
and the gateway adapter's null result cannot establish zero. Changing the default human gateway or
reusing another host's identity would change authority. This capability is distinct from the listed
approved security-enrollment/registration compatibility scope; it has not been implemented or
approved. SDK/catalog/authentication guards and all Folders product code remain unchanged in this
diagnostic phase.

Seven focused component controls now pass (zero failures/errors/skips): actual raw-adapter guard
refusal before transport/authority, missing-projection/unavailable preservation, explicit zero input's
existing safe not-found result, and gateway missing/unsupported/backend evidence remaining null with
the exact metadata-only native-proof request. These controls deliberately provide component
dependencies; they prove neither successful live workload transport nor authoritative absence nor a
fixed outer EOF. The Server.Tests source build passes with zero warnings/errors and `git diff --check`
is clean. The initial diagnostic build failure on an undeclared mocking-library using is retained;
the final controls use a local counted proxy with no package change.

Product/test outputs were handed back to the parent after focused qualification, with no further
product/test edits or builds. The parent owns remaining broad current-output checks and a portable
evidence packet before presenting the concrete owner decision. No live topology was started in this
diagnostic phase. Original strict/capacity bodies, all source/proof/final observations, authentication,
denials, deadlines and retries remain unchanged. The original live acceptance row, six-concern
qualification and SM4 stay open; Story and sprint remain in-progress, alongside the separate SDK,
Release/package/PD10 and whole-story review gates.


## Parent current-source verification and owner decision — 2026-11-02

The [new sealed packet](evidence/story-12-2-current-sdk-source-head-20261102T213710Z/README.md) records the codec optimization, current-issuer/canonical-scope fixture migration and platform-command validation repair. The local warm codec probe was 5.73× faster with 88.7% fewer allocations; no SM4 result follows. Current consumer runs passed 5,545 tests across Domain, Server, Workers, Integration, Gateway, Contracts and client generation, with the two existing Domain operator skips. The initial three diagnostic-inventory failures were corrected by adding the new compile inputs and regenerating only the pending source inventory; Contracts then passed 636/636. Historical conformance/spine bytes and disabled v2 exposure are preserved.

Four audited runs of the unchanged strict operational class are retained. The final run passed genuine bootstrap (202, Completed, all four refusals, original source and projected authority), tenant creation/enrollment and organization grants. The unknown-profile provider-binding PUT then ended with premature HTTP EOF. One structured event independently proves the organization source-head raw actor invocation was refused, followed by /process 503 and actor rejection. It does not prove the outer EOF cause. No six-concern live pass or SM4 measurement exists.

The [source-head proposal](proposal-12-2-current-sdk-source-head-transport.md) remains unapproved. Its independent source-only SDK composition, own workload/key/grant enrollment and authoritative zero/absence contract exceed the approved host-enrollment exception. No such production capability or enrollment was added. The seven refusal/distinction controls pass; no null, unsupported proof, 404 or projection became source authority.

The required Release conformance command failed restore with NU1102 for Hexalith.Tenants.Client >= 7.0.0 (nearest published 6.0.0). Debug source fallback checks passed and the source graph was restored; no package/CI gate was weakened. The packet binds unchanged full repository heads, strict/capacity bodies, frozen intent, actual mappings and exact owned cleanup. Original acceptance rows, Story/sprint in-progress status and separate broad SDK, Release/package, live capacity identity, SM4 and independent-review gates remain open. No Git mutation, publication, deployment, dependency pin/gitlink update or public v2 exposure occurred.


## Approved independent source-head continuation — 2026-11-03

The user replied **approve** to the [source-head prerequisite](proposal-12-2-current-sdk-source-head-transport.md). Its bounded SDK-owned read-only composition, actual Folders workload/key/grant enrollment and consumer migration are now authorized. This extends only the previously recorded SDK/host-enrollment boundary as described in that proposal. Prior sealed packets, original baseline, strict bodies and every existing guard, source/proof/final observation, deadline and excluded operation remain unchanged.

Implement this continuation over the preserved working tree; do not redo historical completed tasks. Load the newly approved proposal from frontmatter context. Establish its evidence contract before changing production code: positively proved zero/absence must remain distinct from unsupported, missing proof, erased, malformed, denied or unavailable source. If the existing authenticated metadata route cannot establish that contract within the approved no-new-persistence/no-actor-schema bounds, retain executable evidence and return the precise remaining SDK prerequisite before production implementation. Do not manufacture zero to make the legacy Task<long> seam fit.

- [x] In proposal-12-2-current-sdk-source-head-transport.md and focused owner tests under references/Hexalith.EventStore/tests/Hexalith.EventStore.Client.Tests/Streams/ and ...Server.Tests/Streams/, specify and qualify the supported positive/zero/absence/refusal evidence contract. Bind every conclusion to current source and executed controls; distinguish a missing actor from erased or unsupported evidence.
- [x] If that first gate succeeds, implement the smallest independent read-only public SDK composition in references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Streams/, ...Client/Registration/NativeSourceMetadataServiceCollectionExtensions.cs and/or ...DomainService/EventStoreGatewayWorkloadTransportExtensions.cs. Reuse supported authenticated metadata/proof protocols, preserve the default human-command client, and add focused registration/credential/catalog/proof/result controls. No domain-local platform protocol or internal SDK registration.
- [x] Inventory reachable Server/Workers source-head consumers, then wire the authorized actual Folders caller's own issuer identity/signing material and least-privilege metadata grant through supported host/catalog configuration. Update src/Hexalith.Folders.AppHost/ and the disposable fixture only as required; do not enroll both hosts by default or borrow another host's identity.
- [x] Migrate src/Hexalith.Folders/Authorization/DaprOrganizationStreamSequenceReader.cs and its Server/Workers registrations through the approved SDK seam. Preserve exact own-actor shortcuts, different-aggregate freshness, existing canonical missing/stale/unavailable outcomes, cancellation and the three-/five-/forty-second ceilings. Add production-path positive and denial controls without relaxing the original strict/capacity assertions.
- [x] Sequentially build current Debug project-reference runtime, run affected owner/consumer checks individually, and execute owner-approved strict real-provider acceptance with audited identities and exact owned cleanup. Diagnose ResponseEnded independently if it recurs. Refresh only the pending source inventory for new compile inputs; preserve historical evidence and disabled v2. Retain all failed attempts and separate SDK broad, Release/package, live capacity, SM4 and independent-review gates. Completed with the exact approved two-probe exception on2026-11-04; historical failed runs remain unchanged.

The implementation agent owns source changes and focused checks, then returns selected outputs free of active builds/tests. The parent owns final runtime rebuild/audit, broader checks, unchanged strict live execution and a new evidence packet. No simultaneous builds may mutate selected outputs.

### Nonfrozen source-head implementation handoff — 2026-11-03

The approved evidence gate passed before production implementation: real actor inspection through
the existing signed workload/RSA-proof HTTP route returns existing valid native positive/zero evidence;
missing/unactivated, unsupported legacy, erased and invalid evidence stays unavailable. Reads mint no
source anchor. The supported contract deliberately does not treat every physical absence as zero.
The proposal records the exact existing-zero support limit and retained component evidence.

The generic stream grant was separately reproduced accepting an event range with a metadata request's
resource proof or a resource-bound JWT alone. Implement the approved least-privilege metadata grant
with explicit trusted receiver enrollment, exact metadata-only shape and mandatory actual resource
proof for those new callers. The restriction participates in independent catalog authentication capture.
Generic readers, current route/proof/JWT/actor/persistence schemas and all catalog guards are preserved.
The new public SDK client is separately named and configured; the ordinary human gateway is unchanged.

Both Server and Workers have reachable fresh source reads, including Workers' semantic-indexing
authorization. Each distinct actual caller has its own issuer secret and RSA key, stream-read-only
issuer scopes and `folders` metadata-only receiver enrollment. Replicas sharing a physical AppId use
that actual workload; distinct proof Workers receive independent identities. Caller allowlists merge
through the existing public SDK composition without replacing original/Tenants membership. Aspire
secrets remain parameters; no keys/tokens enter tracked source or evidence. The consumer retains exact
own-actor shortcuts, proof/identity validation, unavailable/zero distinctions and all existing ceilings.

Final focused selections pass 334 cases, zero errors/failures/skips: SDK Server 131, DomainService 15,
Client 62, Folders Server 84, Workers production host security 1, and actual AppHost realm/model/
bootstrap/scope controls 41. All final builds have zero warnings/errors; both diff checks pass.
Overlapping earlier subsets are separate history, not additional final cases. Intermediate compile/
assets and profile-inventory failures are retained in the continuation handoff.

Implementation released selected outputs with no active build/test/topology and no subsequent product/
test edits or builds. Parent qualification remains required: final runtime rebuild/audit, broad suites,
pending inventory refresh and the unchanged real-provider strict class. No new live pass or SM4 result
is claimed. The earlier outer `ResponseEnded` cause remains independently unresolved. Prior sealed
packets, original strict/capacity bodies, source/proof/final observations, authentication/denial guards,
deadlines/retries, frozen intent and every separate open gate remain unchanged; Story/sprint stay
in-progress and the original live acceptance row remains unchecked.

### Nonfrozen outgoing-dispatch diagnostic handoff — 2026-11-03

The parent's audited source-head strict run passed full genuine bootstrap, then the same first
unknown-profile provider-binding PUT returned 503 `provider_unavailable`, rather than the required
403. It failed once, zero errors/skips, in 286.598 seconds. The earlier premature EOF and raw-actor
refusal did not recur. No transport fix or six-concern/SM4 pass follows from that difference.

The retained trace proves source issuer discovery/JWKS/token requests returned 200 immediately before
the named native metadata POST began at 09:45:16.974 UTC. No source response/receiver action is recorded.
The public human gateway's existing ten-second attempt timeout fired twice, then its thirty-second
total timeout fired at 09:45:33.745, when the endpoint returned `provider_unavailable`. The command actor
records Rejected; the original profile-validation result is not established. A later issuer 400 names
the unrelated unhosted sample's `domain-service:metadata` operation, not the source request. Source
cancellation is consistent with the enclosing canceled invocation; the prior log does not identify
whether source catalog acquisition, transport or release was pending. No issuer/DI/physical-target/
enrollment correction is demonstrated by these observations.

Within the authorized safe diagnostics, the existing opt-in payload-free SDK recorder now distinguishes
outgoing `InvocationAcquire`, `InvocationValidation`, `InvocationTransport` and `InvocationRelease`.
The owning HTTP filter supplies optional logging; existing public construction and default logging
behavior remain available. Original guards, request/response bytes, lease ownership/retention,
cancellation and independent owned release are preserved. The fixture alone enables the relevant
outgoing/technical-receiver/domain-invoker Debug categories. No new read, prewarming, retry, deadline,
source proof or business behavior is added. Transport entry proves entry into the owned transport;
it does not itself prove that the receiving application observed a request.

Focused controls pass 48/48 across new outgoing diagnostics and existing native-scope/transport/
receiver controls; fixture realm/model checks pass 2/2, all zero failures/errors/skips. They prove
canceled acquisition has zero transport calls, actual transport and release remain distinct, original
exceptions and retained fences survive, and disabled/throwing logging is advisory. Final builds pass
with zero warnings/errors and both diff checks are clean; the initial new-test logging-filter compile
failure is retained. Selected outputs were released without active builds/tests/topology and without
further product/test edits or builds. Parent owns the next final rebuild/audit and unchanged strict
run to classify the actual pending stage; the live acceptance row and all separate gates remain open.


### Nonfrozen catalog acquisition performance handoff — 2026-11-03

The next audited strict run passed genuine bootstrap but failed authenticated CreateTenant with
500/unavailable before the strict body or new Folders source call. The retained trace shows the
existing domain five-second timeout expiring during the fourth actual Tenants native transport.
The first three native transports took 604/2127/1362 ms; the fourth was canceled after 590 ms.
Outgoing native lease revalidations took 12/14/16/32 ms, while receiver/native newly constructed
leases included 168–900 ms acquisition spans. The proof callbacks themselves took 1–4 ms.
This establishes an enclosing native-authority deadline failure, not a source enrollment refusal;
it does not yet distinguish queue, backend/CAS, capture, trust or snapshot cost inside acquisition.
The earlier ResponseEnded cause remains separately unresolved and did not recur.

Pure component probes rejected an activation-cache or serializer-options change on the present
evidence. A real captured SDK Gateway activation had 18,636 catalog bytes and 33 technical actions:
ComputeDigest was about 2.77 ms per call. A bounded four-receiver technical cohort was about 3.04 ms
for ComputeDigest and 4.51 ms for snapshot construction. Options cloning was below 0.001 ms.
Fresh full Gateway receiver observation was 31–50 ms in these short local batches; image observation
accounted for about 18.85 ms and 14.03 MB of allocation, while 41-type schema generation plus Complete
accounted for about 3.55 ms. These microbenchmarks do not prove the live bottleneck or a capacity pass.

The actual Folders/Tenants owner corpus has 46 declarations. The gateway now applies the domain
host's existing lazy frozen compiled-inventory pattern to its exact loaded owner assemblies and
passes those declarations into ActorQueryTargets. It retains fresh actual actor/invoker/query checks,
receiver/image/schema/policy observations, configuration/component/ACL capture, identity, owner trust,
backend reads, CAS, source proofs, final observations, and all existing bounds. The cohort has no
projection-actor declaration; an early provisional four-read measurement overcounted its branch.
The retained final probe invokes the actual cohort branch rather than using that provisional estimate.

Opt-in payload-free acquisition diagnostics now separate local admission wait, durable state read,
CAS, activation digest, fresh host capture, trust and immutable snapshot construction. Logging lookup,
creation and writing are advisory; the original public authority constructor and lease/control/token
semantics are preserved. Only the fixture enables the new Debug category. There is no new cache,
prewarming, backend/source read, retry, deadline, topology or guard exception.

This phase's final focused SDK selection passes 56/56, and fixture realm/model checks pass 2/2,
zero errors/failures/skips. These counts are separate from the earlier 334 source-head and 48 outgoing
diagnostic selections. They include actual runtime registration/invoker drift after frozen inventory,
queue cancellation, original capture/trust exceptions, advisory disabled/throwing logging and real DI
logger-resolution failure. Final SDK and fixture builds have zero warnings/errors. Prior probe setup
failures, the superseded 55-case selection and provisional component estimates remain retained.

Selected outputs are released after final focused checks, with no active build/test/topology. Parent
owns the full runtime rebuild (including all SDK copies), source/module audit, remaining broader checks,
and unchanged isolated strict execution. The new SDK diagnostic test is a pending compile-inventory
input. Original strict/capacity body hashes, frozen intent, historical packets, disabled v2 and all
open SDK broad/Release/package/PD10/governance/review/live/SM4 gates are preserved; no live acceptance
or story-completion claim is made.

### Parent acquisition-diagnostic runtime result — 2026-11-03

The rebuilt current-source solution and Memories runtime passed; all 92 selected modules matched
their PDB/source checksums, selected SDK identities had one variant and selected Hexalith assets were
project references. The unchanged strict class on isolated port 36023 failed once, zero errors/skips,
in 423.991 seconds. Genuine bootstrap, tenant creation/membership and organization baseline/grants
completed. The first unknown-profile ConfigureProviderBinding PUT again required 403 but returned
503/provider_unavailable. Neither ResponseEnded nor the raw-actor refusal appeared.

This attempt stopped before domain dispatch: its first gateway attempt spent 680 ms in SubmitAcquire,
6125 ms in Admission and 582 ms in ExecutionCapability before ExecutionBegin was canceled at the
existing ten-second attempt limit. The second attempt expired during Admission; the third was still
in Admission when the existing thirty-second gateway total limit produced 503. No source-reader
transport/authentication result or domain five-second timeout is established by this trace. Its
partial acquisition sample shows local admission wait at median 230/p90 518 ms and fresh capture at
median 65/p90 155 ms; read/CAS/digest/trust/snapshot medians were 1–3 ms. These are partial diagnostic
distributions, not SM4 results.

Source/head/runtime drift was zero. Observed owned descendants/containers were gone and the exact
fixture Dapr directory was removed. Four separately observed unattributed containers and one new
unattributed Dapr directory were preserved; the original global-observation receipt and separate
owned-cleanup audit retain that distinction. Exact private logs/XML, closed timing metadata, source
patches and audit receipts remain retained for the new continuation packet. The original sealed
packet is unchanged and its verifier still passes. Live acceptance and all separate gates remain open.


### Nonfrozen fresh image allocation handoff — 2026-11-03

The completed strict-3 diagnostic trace establishes the existing human gateway's ten-second attempt
and thirty-second total timeout path before domain dispatch. Its first attempt reaches ExecutionBegin;
the second and third stop in Admission. It establishes neither a new source-reader transport/auth
result nor a domain five-second timeout. Final acquisition distributions show local admission wait
median 331/p90 718 ms and fresh capture median 76/p90 176 ms; backend read/CAS, digest, trust and
snapshot medians remain 1–4 ms. These timing records are diagnostic evidence, not a live acceptance pass.

The approved bounded allocation correction changes only TechnicalHttpActionObservation.AssemblyIdentity:
a private pool retains at most one buffer per bucket up to 16 MiB (less than 32 MiB total retained).
Every invocation still opens and reads a fresh privately owned image snapshot. PE/MVID validation and
SHA-256 use only its exact observed-length bytes; the buffer returns after the PE reader is disposed,
including refusal paths. Larger images keep the existing unpooled read without a new size limit.
Complete still rereads and compares the current image, including changed bytes with an unchanged MVID.
No image/digest cache, stat shortcut, observer omission, queue, authority, protocol, timeout or retry
change is introduced. All original source/proof/final observations and authentication guards remain.

The short paired real full-Gateway Observe probe (20 fresh passes) measured baseline 698.08 ms wall,
902.21 ms process CPU and 358.01 MB allocated versus pooled 410.50 ms, 497.90 ms and 78.00 MB.
That is 78.2% less allocation and 44.8% less process CPU in this local component batch. The separate
read-only-owner-options batch retained the same allocation reduction. The seven-image corpus alone
reduced allocation by 99.43%. The earlier baseline and first candidate batches are also retained;
changing host load is not asserted to explain previous failures. These results establish a bounded
component benefit, not a complete acquisition/deadline fix, live provider result, capacity or SM4 pass.

This phase's SDK selection passes 83/83 in 16.911 seconds, zero failures/errors/skips, with a clean
18.34-second build and zero warnings/errors. Six added controls compare exact hashes, poisoned pool
tails and returned buffers, changed module identity, malformed/missing images, larger-image fallback,
file cleanup and the unchanged Complete refusal. Existing real MVC transport/receiver, acquisition,
native-source and installed-observation controls also pass. These 83 cases are separate from earlier
56/2, 48 and 334 selections. Only the required forwarding-method XML summary changed after the build.

Selected outputs are released with no active build/test/topology and no further product/test/spec
edits or builds. Parent owns the current full runtime/SDK-copy rebuild, source/module audit and next
unchanged strict execution. TechnicalHttpImageObservationTests.cs is a new SDK-only pending compile
input; no root pending inventory change is required for it. Prior packets, failed attempts, original
strict/capacity bodies, frozen intent, disabled v2 and all separate open gates remain preserved.

### Parent pooled runtime result and pending deadline decision — 2026-11-03

The pooled runtime and Memories builds and 92-module source/PDB/project-reference audit passed.
The unchanged strict class on isolated port 36024 failed once, zero errors/skips, in 212.395 seconds
at the original unknown-profile provider-binding PUT. The final exception was TaskCanceledException
after the existing 75-second overall retry budget; the separate per-request HTTP limit remains
40 seconds. Returned 503s preceded that exception. Neither ResponseEnded nor the raw-actor refusal
appeared. Source/head/runtime drift was zero; owned processes/containers and the exact Dapr directory
were gone. One separately observed unattributed container was preserved.

The actual independent source POST acquired/validated the caller catalog and entered transport.
The receiving gateway canceled during its second fresh catalog capture, before ReceiverNext or
source actor observation. The enclosing **500 ms mutation-evidence catch-up budget** expired; the
SDK two-second, reader three-second, domain five-second and gateway ten-/thirty-second limits did
not expire on this path. The process returned a trusted 503 normally. A completed source proof or
metadata response is still unqualified; DI, physical target, authentication or deadlock correction
is not established by this failure.

The [bounded deadline proposal](proposal-12-2-mutation-evidence-deadline.md) requests an explicit
500 ms→two-second exception for the enabled mutation-evidence read/retry budget. Its exact one-line
production change, affected scopes, focused controls, preserved limits and contention risk are
reviewable. Approval was requested asynchronously; no approval or production deadline change is
assumed. The existing 500 ms constant and late-authority refusal remain unchanged while independent
checks continue. Current full domain tests passed 2364 with two operator skips and Server tests
passed 1345; the Workers selection exposed four bare-container host-environment fixture failures
(158 passed), retained separately for authorized correction. Story/sprint and the live row stay open.

### Approved mutation-evidence deadline continuation — 2026-11-03

The user explicitly selected **Approve 2-second mutation budget (Recommended)** for the
[deadline proposal](proposal-12-2-mutation-evidence-deadline.md). This is the sole exception to the
previous deadline-preservation constraint: the whole enabled mutation-evidence read/retry budget
changes from 500 ms to two seconds. SDK two-second, reader three-second, domain five-second,
per-request HTTP forty-second and fixture seventy-five-second limits remain unchanged, along with
all proofs, fresh observations, poll interval, fail-closed exhaustion and original strict bodies.

- [x] `src/Hexalith.Folders/Authorization/PermissionProjectionCatchUp.cs`: apply the exact approved
  Budget constant change; retain all control flow, cancellation, stale/unavailable outcomes,
  late-result rejection, 25 ms polling and immediate-read behavior.
- [x] `tests/Hexalith.Folders.Server.Tests/EventStoreOrganizationEffectivePermissionsReadModelTests.cs`
  and adjacent focused tests: cover organization/folder scopes with valid delayed authority between
  500 ms and two seconds, exhausted/noncooperative late authority, stale evidence, caller cancellation
  and unchanged immediate reads. Update only corresponding old unit deadline expectations.
- [x] Build/run focused current-source controls, then hand back without active output mutations for
  parent full runtime rebuilding/audit, affected broader checks and unchanged strict real-provider
  acceptance. Preserve every failed run and keep SDK fixed-catalog parity/25 red-phase skips,
  Release/package, capacity, PD10/governance, SM4 and whole-story review dispositions separate.

The independent source-head task's final qualification row and the original live acceptance remain
unchecked. Worker and Integration fixture corrections have since passed their full suites (162/854);
they introduce no production, grant or deadline change. No additional deadline exception is implied.


### Nonfrozen approved mutation-evidence implementation handoff — 2026-11-03

The exact approved production change is implemented: PermissionProjectionCatchUp's enabled-read
Budget is two seconds. Its control flow, monotonic accounting, 25 ms poll interval, cancellation,
late-result refusal, stale/unavailable classifications and immediate-read behavior are unchanged.
Every other source/SDK/domain/HTTP deadline and all authentication/proof/fresh-observation checks
remain unchanged. The historical 500 ms failures remain retained and are not reclassified as success.

The focused Folders Server selection passes 83/83 in 56.662 seconds with zero failures/errors/skips;
its current Debug/project-reference build passes with zero warnings/errors. Existing fast positive
cases remain, and both organization and folder mutation scopes now also qualify a valid750 ms source
observation followed by current projected authority. Both scopes reject noncooperative authority at
the enabled deadline; later available completion cannot change that returned unavailable result.
Immediate reads remain pending beyond two seconds until their dependency completes. Both stale
exhaustion and caller cancellation retain their safe original outcomes and token. Existing native
proof, own-actor scope, raw-guard and source-boundary controls also pass. Only the corresponding old
unit timing expectations changed; no strict/capacity assertion was edited.

Selected outputs are released without active builds/tests/topology and without further product/test
edits or builds. The parent owns the full runtime/SDK-copy rebuild, source/PDB/module audit, affected
broader checks and unchanged strict real-provider run. No new compile input was introduced. The
current-source source-head qualification row and original live acceptance remain open; no completed
live native proof, provider/task/durability or SM4 result follows from these component passes. The
separate SDK fixed-catalog parity mismatch and25 existing red-phase skips, Release/package, capacity,
PD10/governance and independent-review gates retain their dispositions. Prior packets and failed
attempts, original strict/capacity hashes, frozen intent and disabled public v2 remain preserved.

### Parent source-head and approved mutation-budget qualification — 2026-11-03

The approved two-second enabled mutation-evidence budget is implemented and component-qualified.
Its focused 83-case selection passes; all other deadlines, the 25 ms poll interval, late-authority
refusal and source/proof/final-observation checks remain unchanged. Current full consumer selections
pass 5,583 cases with two operator skips: Domain 2,379, Server 1,354, Workers 162, Integration 854,
Gateway 123, Contracts 636 and client generation 75. These are phase-attributed full selections,
not a sum of overlapping focused runs. The later fixture-only existing-service observer adds nine
new passing controls within a 16-case focused rerun; it does not change production behavior.

Final Debug/project-reference runtime and Memories builds pass. Ninety-two selected assemblies and
the fixture observer match current source/PDB hashes; selected SDK copies have one hash/MVID variant
per assembly and selected Hexalith assets are project references. Both repository diff checks pass.
All three original strict/capacity body hashes, frozen intent and the historical PD10 manifest remain
unchanged. Only the pending source inventory was refreshed for new compile inputs. A first inventory
attempt omitted two new test helpers; that failure and the successful correction are retained.

Eight unchanged strict live attempts are retained. Attempt eight fails once, zero errors/skips,
in 442.310 seconds at the post-restart task read (`RequireRealTaskClosureAsync:346`). Before that it
passes genuine bootstrap, provider profile/target refusals and real preparation, clean task release,
exact six-concern rebuild and TTL checks, interrupted-generation recovery after process replacement,
two concurrent Workers and every main-restart PID assertion. Both EventStore stop cycles reach
`ApplicationStopped`; a shutdown blocker is not reproduced in this attempt. Earlier stop failures
remain failures with their exact pending downstream owner unproven; no speculative shutdown fix,
force stop, ignored command result or stop-timeout change was introduced.

Payload-free task diagnostics establish the latest failure: bounded diagnostic tenant access allows
stale evidence, then nested strict authorization denies at `TenantAccessFreshness` with
`tenant_projection_stale` before reading the protected task model. The first denial records age 309 seconds; 17 actual 503 replies continue through age
379 seconds. This is the unchanged five-minute policy, not proof of lost durable task state.
Every parent command has zero source/head/runtime drift; all observed owned processes, containers
and exact Dapr directories are removed. Unattributed resources were preserved.

Current SDK full selections pass 12,599 cases, fail three retained-catalog parity tests and skip 30
cases. The parity mismatch is tied to clean current Tenants owners adding one query and explicit
platform admissions against the retained SDK catalog; no pristine-baseline execution is claimed.
The 30 skips comprise 25 existing DW1 red-phase server specs, two isolated-Dapr recovery cases, one
content-live case and two package-contract cases without a supplied release inventory. Fixed catalog
bytes, admission declarations, test expectations and dependency pins were preserved. The exact
Release conformance command now fails with NU1109: Tenants.Client 7.0.0 requires EventStore Client
and Contracts >= 4.0.4 while central pins remain 4.0.0. Debug source restore subsequently passes.

[The sealed continuation packet](evidence/story-12-2-source-head-and-mutation-budget-20261103T140301Z/README.md)
retains safe receipts, phase source trees, source patches, actual runtime identities, source/PDB/assets
audits, executed matrix coverage, focused handoffs, lifecycle records and all failure dispositions.
Raw logs/XML and disposable credentials remain private. The earlier sealed packet is unchanged.

The existing fixture administrator lacks `tenants:update`. The approved scope excludes grant
expansion, and repeating an existing member addition is rejected. The
[fixture freshness proposal](proposal-12-2-live-tenant-freshness-prerequisite.md) therefore requests one
explicit fixture-only permission exception and genuine authenticated tenant metadata activity,
with normal source/projection verification and unchanged authority semantics. No such grant,
maintenance command, direct state write or freshness-policy change has been applied.

- [x] Owner decision on the fixture-only `tenants:update` exception: the user replied **approve**
  on 2026-11-03. This supersedes only that fixture permission exclusion.

The strict qualification rows and Story/sprint stay in-progress. Current-cohort live capacity, SDK
parity/red-phase/optional lanes, Release/package, PD10/governance, SM4 and independent whole-story
review remain separate open gates. No staging, commit, push, package/gitlink update, publication,
deployment or public v2 exposure occurred.


### Approved foreground fixture tenant maintenance — 2026-11-03

The user approved [the exact fixture freshness proposal](proposal-12-2-live-tenant-freshness-prerequisite.md).
Implement this bounded exception without changing frozen intent. The prior sealed packet and its
pending-decision snapshot remain immutable. Production five-minute freshness, approved two-second
mutation evidence, all other grants/deadlines, original strict/capacity bodies and folder retained
prefix assertions remain unchanged. No other SDK, production or dependency change is included.

- [x] `tests/Hexalith.Folders.AppHost.Tests/FolderDurabilityBootstrapConfiguration.cs`: add exactly
  `tenants:update` once to the existing disposable administrator, retaining all other realm semantics.
- [x] `tests/Hexalith.Folders.AppHost.Tests/FolderDurabilityAcceptanceFixture.cs` and focused single-type
  helpers: maintain only the fixture's own tenant through the existing canonical authenticated
  `UpdateTenant`, with its original name/description and administrator. Use bounded foreground
  positive-evidence/restart hooks, a conservative two-minute trigger, and caller cancellation.
  Require terminal Completed, the actual matching original TenantUpdated source event, and normal
  Workers delivery reaching that source sequence/timestamp with identical enabled state, principals,
  roles and configuration. Missing/malformed/conflicting/failed/undelivered evidence fails the fixture;
  no background heartbeat, global client hook, direct source/derived write, old-event restamp, grant
  churn or stale authorization is permitted. Reuse existing source/catalog/issuer guards.
- [x] Adjacent AppHost fixture-policy/realm tests: qualify the exact added permission and unchanged
  other identities/grants; no command when sufficiently fresh; own-tenant canonical command shape;
  rejected/failed or unobserved source/projection cannot establish freshness; unchanged authority;
  bounded cancellation. Preserve existing production stale-denial controls and all original bodies.
- [x] Build/run focused current-source controls only and hand source/outputs back without an active
  command. Parent then refreshes only pending compile-input inventory, rebuilds/audits runtime, and
  reruns the unchanged strict class with private traces and exact owned cleanup. Retain every failed
  attempt; keep SDK parity/red-phase/optional lanes, Release/package, capacity, PD10/governance, SM4
  and whole-story review dispositions separate. No publication, Git mutation or v2 exposure.

Source inspection established that normal UpdateTenant always emits TenantUpdated for an authorized
active tenant and changes only name/description. Its exact `tenants:update` admission has no admin
bypass. Repeating AddUserToTenant for an existing member is rejected. This exception qualifies an
actively maintained disposable tenant and does not qualify indefinite idle-tenant access.


### Foreground tenant maintenance focused handoff — 2026-11-03

Implemented only the approved disposable administrator permission and foreground fixture path.
The fixture reads its authenticated canonical platform Tenants source and normal Workers projection
before positive folder-evidence and before/after host-restart phases. At a native source age of at
least two minutes, it submits the original metadata through UpdateTenant, requires matching Completed
status, original correlation/causation and human attribution, unchanged retained Tenants prefix, then
exact Workers message/sequence/native timestamp evidence with unchanged enabled state, principals,
roles and configuration. A ninety-second whole-maintenance linked deadline includes gate wait and
all command/source/projection I/O. Existing callers retain the existing submit/status defaults;
production five-minute freshness, the approved two-second mutation budget, all other existing limits,
original strict/capacity bodies, folder retained-prefix assertions and production realm are unchanged.
The only new log is a closed payload-free maintenance PASS record after the complete barrier.

The final focused selection passes 101 cases (47 new maintenance controls plus 54 retained
bootstrap/realm/scope/source-reader controls), zero failures/errors/skips, in 5.525 seconds. The
unchanged current-source Domain output passes 77 tenant/layered/task authorization controls,
including the existing stale-denial cases, in 0.222 seconds. These are separate selections; earlier
95-case execution overlaps the final fixture selection and is not added to it. AppHost test builds
passed with zero warnings/errors in 59.91 and 51.70 seconds. The first build's six new-test CA2007
errors were corrected and its receipt retained. A final XML-summary-only rebuild then encountered
three external SDK CS8300 conflict-marker errors in SubmitCommandRequestValidator.cs; no SDK source
was edited in this phase, and the markers had already disappeared at immediate read-only capture.
The final focused run used the preceding successful output containing all 47 new controls. The
comment-only source addition and external concurrent SDK/output activity require the parent's full
current-source runtime rebuild and source/PDB/dependency audit before live qualification.

Private phase receipts, before/after hashes, exact commands, source patch and runner handoff are in
`/tmp/story12-2-tenant-maintenance-implementation-20261103/`. No live test ran in this implementation
phase, so the parent runtime/audit/live checkbox remains open. The earlier strict failures and sealed
packets are unchanged. This qualifies the fixture policy for an actively maintained tenant; it does
not claim indefinite idle-tenant access, live acceptance, capacity, SM4, SDK/package parity or story
completion. This phase changed no production policy, authority guard, grant beyond the single fixture
exception, dependency pin, frozen artifact, Git state or public v2 exposure. The parent subsequently
confirmed an external EventStore merge in progress and externally advanced SDK HEAD; the prior broad
SDK/consumer runs do not qualify that new producer cohort. No external merge/source edits were resolved
or incorporated by this fixture phase; coherent producer qualification remains with the parent.


### Current-cohort qualification follow-up — 2026-11-03

External work merged a new EventStore producer cohort while this fixture implementation was being
qualified. The parent preserved that work, restored with both source flags, rebuilt runtime/Memories
with zero warnings/errors, and verified all92selected modules plus the observer against source/PDB,
one SHA/MVID per selected assembly, and project-only selected assets. Rebuilt101fixture and77stale
controls pass. One build observed an unrelated McpCli HEAD-only commit; its source map did not change.
The existing sealed packets remain historical and unchanged.

The unchanged strict class failed twice before reaching the new tenant-maintenance hook: once on a
retained UnavailableProjection rejection where ValidationFailed is required (137.433s), and once at
the unknown-profile HTTP retry deadline (195.562s). The latter has a positively identified five-second
ConfigureProviderBinding domain invocation deadline:78ms acquisition and4922ms transport in the failed
span. Its receiver/handler/reply boundary is not observed by the enabled categories. No deadline,
authorization, mapper, retained outcome or strict assertion was changed to accept either failure.
All owned resources are removed; unrelated resources were preserved.

- [x] `tests/Hexalith.Folders.AppHost.Tests/FolderDurabilityTenantMaintenanceAcceptanceTests.cs` and
  `FolderDurabilityAcceptanceFixture.cs`: add a separate real-AppHost control for the approved
  maintenance path, since the strict class currently stops earlier. Expose only the existing hook
  internally with its bool result. Establish the real verified baseline, snapshot persisted Workers
  authority/evidence, wait for the ORIGINAL native timestamp to reach the existing two-minute trigger
  using real elapsed time/caller cancellation, and require genuine maintenance and exactly one new
  persisted message/watermark/timestamp with unchanged authority and prior evidence. Immediate
  fresh execution must be a no-op with identical persisted state. No fake clock, forced renewal flag,
  direct write, new permission, new source-reader exposure or replacement of strict/capacity bodies.
  This separate control can qualify active fixture maintenance, never full durability acceptance.
- [x] In the root-declared EventStore owning repository, correct fixed test-only credential-shaped
  values in `tests/Hexalith.EventStore.DomainService.Tests/NativeSourceMetadataCompositionTests.cs`
  and `tests/Hexalith.EventStore.Server.Tests/Streams/NativeSourceMetadataCatalogObservationTests.cs`
  using generated per-test ephemeral values and exact shared expectations. Keep malformed-key
  refusal meaningful and all authentication/catalog assertions intact. Current SecretsProtectionTests
  reports only these prior story-owned test lines. Do not exempt paths or weaken the scanner.
- [x] Build/run focused controls serially after parent broad qualification releases outputs. Retain
  the new SDK published-package authenticity failure separately: its pinned expected SHA is
  e21f5ac7b5f332f1921f70a249ab2032b9978d56ffdd6641a0a4b94d04850bdc, but the restored archive is
  81a33bb7562507004d4dfb8da2365cdea1fb874ff53798787bff110c0d9573ef. Do not replace its expected
  hash, skip it, change package pins, or claim the later ABI assertions executed.
- [x] Parent rebuild/audit and execute the new real maintenance control; preserve the failed strict
  runs and keep the original strict row open until it actually passes. A further same-code strict
  diagnosis may enable only existing receiver/ASP.NET timing categories to locate the observed remote
  interval; it does not authorize timeout, guard, outcome or assertion changes. Remaining SDK/package,
  capacity, governance/PD10, SM4 and whole-story review gates retain their separate dispositions.


### Current-cohort follow-up focused handoff — 2026-11-03

Added the separate real-AppHost maintenance control and exposed only the existing fixture hook's
internal bool result. The control establishes the real verified baseline, waits for its original
native timestamp to age naturally to the unchanged two-minute threshold, requires genuine source
maintenance, independently reads persisted Workers evidence/authority, and requires an immediate
fresh no-op with identical persisted state. It adds no credential exposure, forced-renewal flag,
fake clock, background heartbeat, permission, source write or strict/capacity-body change. Discovery
finds the new class/method; no live execution or acceptance claim occurred in this implementation phase.

The two prior-owned SDK tests now use generated per-test values with exact shared expectations.
The malformed key is generated but deterministically invalid DER; valid, missing-key, bad-key and
foreign-domain transport/issuer refusal assertions remain intact. The catalog observation still
requires that the exact private sentinel is absent. Scenario selection moved outside the credential
assignment after the unchanged scanner correctly continued to report its literal-valued expression.
Final SDK selections pass four composition cases in 0.283s and 275 catalog/scanner cases in 5.548s,
zero failures/errors/skips. Earlier overlapping executions and the intermediate one-case scanner
failure are retained, not added to these counts. SDK test builds pass with zero warnings/errors.
The first SDK build encountered duplicate packaged/source Commons assets; focused restores with both
source flags corrected generated assets without changing sources or dependency pins.

The full AppHost test build is blocked by an external production edit: untouched
FoldersDevelopmentOwnerMetadata.cs:34 references BoundOwnerNativeSourceMetadataReader, which is
absent from the selected SDK. No external production or SDK capability repair was attempted.
The permitted narrow fallback with BuildProjectReferences=false compiles the fixture/control against
existing referenced outputs with zero warnings/errors in 5.71s; its 101 retained fixture controls pass
in 5.321s. This checks the changed test code, not a coherent current-source runtime. Parent must resolve
or coordinate the separately owned producer/composition prerequisite before a full runtime rebuild,
source/PDB/dependency audit and real standalone control. No SDK package authenticity hash/assertion,
scanner exemption, product guard, mapper, deadline, frozen artifact or Git state changed in this phase.
The earlier published Tenant7 authenticity failure before ABI assertions remains separate and open.

Exact phase commands, failure receipts, before/after source and selected-output hashes, root/SDK
patches and runner handoff are private under `/tmp/story12-2-current-cohort-followup-20261103/`.
Parent retains current broad results and owns pending inventory refresh for external/new compile
inputs and SourceSHA, Contracts rerun, runtime rebuild and actual standalone execution. The parent
execution checkbox above, original strict acceptance, capacity, SDK/package, PD10/governance, SM4 and
whole-story review gates remain open with their separate dispositions. No new live topology was run.


### Bounded current public-composition repair — 2026-11-03

The external Root commit9e26eb8f4296fe2fdef70cd5858341a55f7ea9e4 adds a Development-only owner
metadata helper that references BoundOwnerNativeSourceMetadataReader, absent from the current SDK.
The coherent source build now fails CS0246 at FoldersDevelopmentOwnerMetadata.cs:34. Do not revive
a private transport or mint/relabel a caller. The already-approved supported public-composition
repair applies: FoldersServerServiceCollectionExtensions already registers the public
AddEventStoreGatewayNativeSourceMetadataReader("folders") before this private helper is invoked.

- [x] `src/Hexalith.Folders.Server/FoldersDevelopmentOwnerMetadata.cs`: reuse that existing public
  SDK reader registration/factory with its existing lifetime, rather than constructing the unavailable
  class or a separate fixed-caller/fixed-destination HTTP client. Preserve the explicit enabled flag,
  Debug/source restriction, Development/catalog-directory guards, optional diagnostics guards and
  GatewayOrganizationStreamSequenceReader selection. Preserve actual configured workload/key/AppId,
  metadata-only shape/domain, human default transport, all proofs/denials and existing deadlines.
  Any unexpected/missing composition must fail closed; no source/actor/persistence protocol change.
- [x] Adjacent Server composition controls: qualify the guarded public SDK composition, exact normal
  reader/factory/lifetime and diagnostic decoration, unchanged disabled/non-Development behavior and
  preserved default human transport. Build/run focused checks serially, retain the preceding CS0246
  and stale-output fallback limits, then release to parent for full coherent rebuild/source audit.

This is the previously approved minimal supported public-composition repair; it adds no new caller,
grant, endpoint, public v2, timeout exception, package/gitlink update or publication authority. Keep
all external product edits beyond this precise unavailable-type composition point untouched.


### Supported public-composition repair focused handoff — 2026-11-03

The bounded helper repair reuses the already registered public SDK metadata-reader factory and its
transient lifetime. It removes the unavailable class and unused fixed caller/destination HTTP client;
it neither registers a different source capability nor changes the SDK. The configured SDK workload,
key, AppId, exact metadata-only domain/shape and independent human transport remain owned by that
original factory. Missing, ambiguous, keyed, wrong-lifetime or non-factory registrations fail closed;
a factory returning a different adapter also refuses. Optional decoration keeps the original enable,
Debug/source, Development, catalog and diagnostic guards, and advisory logger resolution cannot
replace the original reader. GatewayOrganizationStreamSequenceReader selection remains unchanged.
Every other external product edit is preserved.

The full current-source Server test build passes with zero warnings/errors in 10.20s, and 36 focused
composition/source-boundary cases pass with zero failures/errors/skips in 3.230s. These include 15
composition controls for real public factory reuse, transient resolution, default human transport,
optional diagnostics/logger failure, disabled/environment/catalog guards and unexpected-composition
refusals. The complete current-source AppHost test build now passes with zero warnings/errors in
88.50s; 101 retained fixture controls pass in 5.487s. Discovery identifies the new standalone control;
it has not been executed. Earlier fallback/intermediate results overlap these selections and are
retained as history, not added to the final counts. SDK composition four-case and catalog/scanner
275-case passes from this same follow-up remain separately recorded; no full-suite producer or ABI
success is inferred.

The prior CS0246 and stale-output fallback are retained but superseded as compile dispositions by
this supported repair and successful full build. Parent now owns full runtime/Memories rebuild,
source/PDB/dependency audit, pending-only compile inventory/SourceSHA refresh, Contracts rerun and
actual natural-age standalone execution. Original strict/capacity bodies and package authenticity
pins/scanner assertions are unchanged. The original strict failures, producer/package authenticity,
capacity, PD10/governance, SM4 and whole-story review remain distinct open gates; standalone success
would qualify only active tenant maintenance. No live topology, Git mutation, dependency change,
frozen-artifact update or publication occurred in this implementation phase.


### Native catalog admission contention qualification — 2026-11-03

The approved real maintenance control passed once with zero failures/errors/skips in183.007s,
including natural two-minute source age, a genuine matching TenantUpdated/unchanged retained prefix,
normal persisted Workers authority/evidence and an immediate exact fresh no-op. Root HEAD advanced
externally during that run but all captured compile-source maps were identical; runtime bytes and
owned cleanup were clean. Original full strict acceptance remains separate and open.

Strict attempt3 passed provider binding/rebuild and reached repository binding. Its failed native
metadata acquisition performed25 fresh state/validation/capture attempts and24 failed CAS attempts
inside2007ms, then canceled before the25th CAS and before any transport/receiver for that invocation.
The actor subsequently returned a safe rejection; cached retries retained the result. Earlier fast
receiver records belong to different invocations. This diagnoses acquisition contention for this
request without mislabeling it as transport latency, missing durable task state or tenant staleness.
All original assertions and configured deadlines remain unchanged. SDK source advanced externally
across14 Admin-related files during the run; the actual pre-run producer is explicitly bound.

The existing frozen2026-11-02 native-authority performance approval permits a bounded correction
that preserves every required observation/proof/final check and all existing deadlines. No new
permission, trust cache, protocol, lifecycle authority or dependency/publication scope is included.
A concrete source/ETag rebase candidate is under read-only assessment: after each complete fresh
acquisition validation, re-read strong authority state immediately before lease CAS, reuse that
validation only for the exact unchanged committed activation binding/epoch with no pending
transition, and apply all current lease identity, inventory, overflow and sequence checks to the
fresh state. The activation binding digest excludes TrustProof, so exact opaque TrustProof and
canonical catalog bytes must also agree; a proof-only change cannot inherit a prior validation. A changed authority must restart fresh validation. Generic lifecycle update paths,
remote verifier ordering, durable fences/release and default human gateway composition remain intact.

- [x] Assess that exact candidate against SDK authority/observation contracts and deterministic
  lifecycle/lease races; record whether it stays within the approved observation and security scope.
  Do not implement a stale snapshot/cache or omit a required read/check to accept the failed run.
- [x] If that assessment succeeds, bind the smallest exact implementation and meaningful focused
  regressions here before source edits, then rebuild and qualify the affected owner/consumer/native
  acquisition path serially. Keep prior failures, real maintenance pass, fresh package failures and
  independent whole-story/SM4 gates separate.


### Exact bounded admission rebase implementation — 2026-11-03

Implement only after the immediately preceding read-only assessment confirms the exact store and
activation-ownership contracts. The existing native-authority performance approval covers this
internal acquisition correction; it adds no package, trust, protocol, deadline or publication scope.
The parent has released all source/output ownership; no parent build or topology remains active.

- [x] In the owning root-declared EventStore repository, change only the dispatch acquisition path
  in `src/Hexalith.EventStore.ServiceDefaults/Routing/RouteCatalogAuthority.cs` (small adjacent
  single-type internal helpers only if required). On every attempt retain the existing fresh
  state read, local identity/profile/host observation, activation binding and independent trust
  validation. Privately freeze the complete validated activation before asynchronous work so no
  aliased mutable arrays/lists can establish later equality. Then perform a new strong state/ETag
  read immediately before constructing the lease CAS. A rebase is permitted only with no pending
  transition and identical epoch, canonical catalog bytes/root, full activation binding and exact
  ordinal opaque TrustProof. Never equate proof by binding digest alone. Any changed activation,
  proof or epoch restarts full fresh validation (or preserves the existing safe refusal for an
  active pending transition); no changed candidate inherits earlier validation.
- [x] Apply every existing admission invariant to the newly read state: exact local identity and
  generation, own lease identity/idempotency, maximum4096leases, checked LastLease+1, exact fence
  epoch/root/host. Preserve every concurrent foreign fence and all other fresh state fields. CAS
  only the fresh ETag through the unchanged store primitive. Failed CAS restarts complete fresh
  validation under the existing96attempt/caller bounds and conflict scheduling. Cancellation or
  backend failure before CAS cannot create a fence. Do not move generic Prepare/Ready/Commit/
  Rollback/Reconcile or release/verifier behavior; retain final native-read and remote proof checks,
  durable lease cleanup, default human gateway composition and every2s/3s/5s/40s limit.
- [x] Add meaningful deterministic owner controls in
  `tests/Hexalith.EventStore.Server.Tests/Routing/CatalogAdmissionRebaseTests.cs` and adjacent
  single-type test helpers: lease-only contention during every host capture must preserve peers and
  acquire using the refreshed ETag; pending/committed/epoch/full binding/TrustProof-only changes
  cannot reuse validation; mutable activation aliases cannot pass; lease cap/overflow/current
  sequence, cancellation, refresh failure and sustained CAS conflict remain fail-closed. Retain
  existing lifecycle/coordinate/native-read controls. Update only existing read-count assertions
  that necessarily include the additional strong read, with explicit expectations; never weaken
  an original outcome, safety or finite-attempt assertion.
- [x] Build and run focused owner controls serially with both source flags, plus relevant existing
  catalog contention/lifecycle/installed-observation/native-source suites. Retain intermediate
  failures. Hand back exact source patch/hashes/results and released outputs before parent
  rebuild/audit/live qualification. No shared cache, package pin, Git, publication or deployment
  mutation; original Folders strict/capacity bodies and frozen intent stay byte-identical.


### Admission rebase focused handoff — 2026-11-03

The strong-read and private activation ownership assessment passed. Acquisition alone now freezes
its candidate before awaited validation, gives observers/validators a separate owned copy, and
performs an additional strong state/ETag read before admission CAS. Rebase requires the exact epoch,
canonical bytes/root, full activation binding and ordinal opaque TrustProof. Changed authority
restarts fresh validation; pending still refuses new fences. An already recorded exact own fence
can be recovered and released alongside pending, preserving response-loss draining semantics.
Fresh lease inventory supplies the checked sequence,4096 limit and complete retained state; CAS
uses only its new ETag. Local identity/profile/coordinates are rechecked after the added read, and
the winning fence/snapshot retain the validated local binding. Generic lifecycle updates, release,
final native/remote checks,96 attempts, conflict scheduling and every caller deadline remain intact.

The owner test build passes with zero warnings/errors in13.02s using both source flags. The final
18-class focused selection passes222/222 with zero failures/errors/skips in20.415s, including27 new
deterministic admission races and retained lifecycle/quiescence, installed-observation, coordinates,
backend, native-scope and native-source controls. Read-count assertions now explicitly require two
reads per attempted new admission: conflict-then-refusal3 instead of2, cancellation after CAS2
instead of1, permanent96 attempts192 instead of96, and two closed diagnostic read pairs. Existing
outcome/safety/attempt assertions are retained. Earlier overlapping35-case and222-case runs each
had one assertion failure; both raw logs/XML and the exact corrections are retained independently.
No results are added across overlapping selections.

The exact private assessment, phase-only patch, before/after source hashes, commands, build/test
receipts and output hashes are retained under `/tmp/story12-2-admission-rebase-20261103`.
Original strict/capacity bodies remain byte-identical. No live topology or real contention latency
qualification occurred here; the extra read adds I/O and does not guarantee progress under sustained
CAS contention. Parent owns current producer rebuild/audit and any isolated live qualification.
The prior real maintenance pass, original strict failures, packaged4.0.4 source-capability mismatch
and whole-story/SM4 gates remain separate; this focused pass does not close them.


### Current strict foreign-scope contract boundary — 2026-11-03

Parent requalified the admission correction on a coherent Debug/source runtime plus Memories with
all92selected source/PDB/module identities and project assets clean. The owner222cases and parent
36reader/101fixture cases pass. Generic SDK lifecycle/release/final-native code remains unchanged.
The fourth unchanged strict attempt failed once, zero errors/skips, in362.951s. It passed genuine
tenant maintenance, all six concerns, interrupted generation, concurrent Workers, process replacement,
post-restart ready task reads, equivalent replay, closed-binding denial and retained prefix checks.
Its final `folderId=absent-folder` probe expected404 but received503/read_model_unavailable at
FolderAcl. Tenant was Fresh, source/head/runtime drift zero, and owned cleanup complete. Unrelated
Parties containers were preserved. The actual native request acquired in70ms and returned receiver
HTTP503 in380ms uncanceled; the earlier acquisition-starvation failure did not recur at this boundary.

Current SDK owner inspection returns Unsupported/Invalid with no proof for missing/unactivated
metadata. The approved source-head contract explicitly requires unavailability for this case; valid
existing zero remains separate. There is no supported read-only authoritative-absence seam. No
missing-proof-to-zero conversion, source seeding, non-native fallback, relaxed mapping or changed
assertion was implemented to accept the failed run. The original strict hash remains
f5743f9fa2b33803c8a3a29f9a55e5751b5bcce9a31897d1982a1ccd99316b90.

A prepared [bounded test-contract proposal](proposal-12-2-foreign-task-scope-proof.md) and
[unapplied exact patch](proposal-12-2-foreign-task-scope-proof.patch) distinguish genuine foreign
task scope404 from unsupported physical absence503. It creates a real different folder via existing
authenticated v1 creation and current Workers/source evidence, preserving task-key isolation and the
404 assertion; the old literal remains as a separate503/no-protected-observation control. This is
an explicit proposed strict-body exception, not an implemented change. The user decision is pending.
The patch passes `git apply --check`. Independent current-producer broad rechecks continue serially;
no whole-story completion, SM4, package, governance or independent review pass is implied.


### Broad-gate test hygiene follow-up — 2026-11-03

The current admission producer's broad SDK Server run has7612passes,1failure and25existing skips.
The sole failure is the unchanged SecretsProtection scanner reporting an externally added fixed
bearer literal at SDK Admin `DaprStreamQueryServiceTests.cs:220`. The current Folders Server run has
1394passes/1failure: `ParallelPermissionObservationTests.CallerOrCatchupCancellationDrainsBothSourcesAndAcceptsNoLateAuthority`
times out at its test-only2s WaitAsync, exactly the approved2s production catch-up deadline. The
case's expected timeout reason, no-late-authority, token drain and unchanged-store assertions remain
required. Prior full1395pass is retained independently; no production cause is inferred.

After the parent serial broad sequence releases outputs, perform only these bounded test corrections:

- [x] `references/Hexalith.EventStore/tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprStreamQueryServiceTests.cs`:
  replace only the health operator's fixed bearer literal with a generated ephemeral local token and
  use the same variable in the existing exact forwarding assertion. Preserve all externally added
  scope/permission/filter behavior, every other identity and the scanner's rules. No shared cache edit.
- [x] `tests/Hexalith.Folders.Server.Tests/ParallelPermissionObservationTests.cs`: change only the
  outer test harness guard for awaiting the2s catch-up result from2s to5s, matching its existing5s
  source-start guard. This resolves two equal wall-clock timers racing under a loaded runner. Keep
  the actual production catch-up2s and all SDK2s/reader3s/domain5s/HTTP40s limits unchanged. Retain
  exact unavailable reason, both sources drained, cancellation identity and unchanged-store checks.
  This is not a change to either original strict/capacity body or a product timeout.
- [x] Build/run affected owning Admin and Folders Server classes, plus the unchanged full secret
  scanner, serially with both source flags. Retain the broad failures and focused rechecks separately.
  Refresh only pending source inventory hashes if this existing compile input requires it; preserve
  the immutable dated PD10 artifact. Hand back without active source/output mutations.

The unrelated proposed foreign-task-scope strict-body exception remains pending the user's decision;
none of these hygiene tasks applies that patch or changes the original strict hash.


### Broad-gate hygiene focused handoff — 2026-11-03

The health operator fixture now generates one ephemeral local bearer value and uses that same value
in its exact forwarding assertion. All external Admin scope/permission/filter and denial checks are
preserved. The permission-observation test changes only the outer await guard from2s to5s; actual
production catch-up remains2s and every result/reason, cancellation token, source-drain and unchanged
store assertion is retained. Scanner rules, product source, credentials/grants/pins and both original
strict/capacity bodies are unchanged. The proposed foreign-task scope patch remains unapplied.

The initial Admin no-restore build correctly refused missing assets with NETSDK1004; a focused
restore using both source flags resolved that local preparation prerequisite. Subsequent Admin and
Folders Server test builds pass with zero warnings/errors in2.91s and4.92s. Serial focused execution
passes37Admin cases in0.355s,9permission-observation cases in2.185s, all274unchanged secrets-scanner
cases in5.436s, and18pending-inventory controls in6.159s, with zero failures/errors/skips. These are
separate focused results; the preceding broad failures remain retained and no new full-suite pass
is inferred. Only the pending inventory's edited test-source SHA and set digest changed; historical
conformance/spine bytes and disabled-v2 status are preserved.

Exact phase-only patch, before/after hashes, commands, output identities and build/test receipts are
retained in `/tmp/story12-2-broad-hygiene-20261103`. Source/output ownership is released to the parent
with no active build/test/topology. Whole-story, original strict contract decision, live capacity,
packaged SDK capability and independent review/SM4 gates remain separate.


### Approved continuation closeout and pending strict-scope decision — 2026-11-03

The new [portable continuation evidence](evidence/story-12-2-tenant-maintenance-20261103T211501Z/README.md) binds all closed phases,
source/module identities, owner handoffs, failed runs, focused corrections and the pending proposal.
The real maintenance component passed separately (1pass183.007s) and executed repeatedly inside
strict4. The catalog acquisition correction passed222owner cases (27new); final rebuilt source
reader36 and fixture101 controls pass. Final92module source/PDB/single-SDK/project-assets audit is
clean; frozen intent, all three original strict/capacity bodies and dated PD10 bytes remain unchanged.

Current-producer broad results: Domain2384pass/2existing skips; SDK Server7612pass/1scanner failure/
25existing skips; Folders Server1394pass/1test-guard failure; Workers162, Integration854, Gateway123,
Contracts636 and client generation75pass. The sole scanner finding was an external Admin test's fixed
bearer literal, now generated with its unchanged exact forwarding assertion (37Admin cases and
274unchanged scanner cases pass). The outer test guard equal to the approved2s catch-up deadline was
corrected to5s while production remains2s; all9affected source-drain/cancellation cases pass.
18pending-inventory checks pass after only the necessary source SHA/set-digest update. These focused
rechecks overlap broad suites and are never added to them or relabeled as full broad reruns.
The previous public-package cache discrepancy was independently resolved using the official pinned
archive in an isolated fixture cache; original ABI controls pass, including in the latest broad SDK
run (whose only failure was the scanner). Shared cache content/pins were not manually replaced.

Fresh exact Release contract-spine gate progressed past the historical NU1109 and fails CS1061: the
unchanged published SDK4.0.4 lacks AddEventStoreGatewayNativeSourceMetadataReader at Folders Server67
and Workers87. No dependency/publication scope was broadened. Debug/source assets were restored and
requalified. Required10001/32769handler/dispatcher component cases passed on the current admission
producer; no extra32769Folders HTTP run is claimed or required by the recorded composition decision.

Unchanged strict4 remains failed at its unsupported physical-absence404 expectation after passing
post-restart task durability/replay/source guards. The exact test-contract proposal and unapplied
patch are ready; the user decision is pending. No revised body is built or executed, and no physical
absence is converted into native zero. All owned live resources were removed; unrelated resources
were preserved. Step03/story/sprint stay in-progress; full strict, package/governance, SM4 and whole-
story review are not marked complete. Prior sealed packets still verify unchanged.


### Approved real foreign task scope qualification — 2026-11-04

The user replied **approve** to [the exact two-probe proposal](proposal-12-2-foreign-task-scope-proof.md).
Apply only that explicit original-body exception: genuine different authorized folder404, separate
never-created unsupported-source503 and canonical code. The approved patch SHA256 is
c832f58f6c1b05516c1b62f97e2bc8a820d6ed2b4a51f6006952db28a16d7856. The strict body is still
f5743f9fa2b33803c8a3a29f9a55e5751b5bcce9a31897d1982a1ccd99316b90 at approval; the exact proposed
after hash is d29afa3ca0649744e84413c37bfbb41c9de0bd106160c900cb2a8c8e970c1b82. This is the sole
new body exception; other strict/capacity bodies, frozen intent and every production deadline/guard
remain unchanged. No SDK absence capability, source seed, proof conversion or permission expansion.

External sessions committed the prior work and advanced Root to9198d9213f6ea54c629148322f05e7ded994822d
and SDK to aeec5012fa3e1e07426a8fdd145709bce93f619a. Preserve this current clean tracked cohort and all
other story work; no Git mutation is authorized. Prior sealed packets stay immutable. The workflow
continues step03 from its existing rendered snapshot; do not rerender or replace baseline_commit.

- [x] Apply the exact approved patch to `tests/Hexalith.Folders.AppHost.Tests/OperationalStatusDurabilityAcceptanceTests.cs`.
  Use the existing ordinary authenticated v1 creation, fresh key/default task header and normal
  current source/Workers barrier. Retain404 on the original task under the different real folder,
  no original-folder disclosure; separately assert503/read_model_unavailable for literal absent-folder.
  Keep all timing samples identifiable, original budgets and all remaining checks.
- [x] Extend meaningful reader/handler/counting-store controls beside
  `tests/Hexalith.Folders.Tests/Projections/OperationalStatus/OperationalStatusPersistenceTests.cs`
  and/or `tests/Hexalith.Folders.Tests/Queries/Folders/TaskStatusQueryHandlerTests.cs` to prove the
  different authorized folder's task/checkpoint/evidence keys only; original folder task key never
  observed; unsupported native absence never becomes zero or triggers task lookup before current
  folder authority. Reuse existing controls where they already prove a requirement. No production
  behavior change; if actual behavior disagrees, return the finding before widening this scope.
- [x] Build/run appropriate focused Domain/AppHost controls serially, both source flags, Debug,
  individual built xUnit assemblies. Refresh only pending inventory paths/hashes for changed/new
  compile inputs; preserve dated immutable PD10. Parent owns full coherent runtime+Memories rebuild,
  current source/PDB/producer/asset audit, further strict live and exact cleanup. Hand off exact
  patch/guard hashes/results with no active source/output commands.

Implementation agent owns source/outputs until explicit release; parent performs no competing build
or live run. Fresh private handoff/evidence workspace: `/tmp/story12-2-foreign-scope-20261104-s76ae741`. Parent retains the approved body-hash
transition in new audit tooling; no old receipt/verifier is edited. Story/sprint remain in-progress;
package, governance, SM4 and whole-story review gates stay separate until actually satisfied.


### Real foreign task scope focused handoff — 2026-11-04

The exact owner-approved patch is applied. The strict source hash transitions from
f5743f9fa2b33803c8a3a29f9a55e5751b5bcce9a31897d1982a1ccd99316b90 to
d29afa3ca0649744e84413c37bfbb41c9de0bd106160c900cb2a8c8e970c1b82, exactly as approved.
The final probe creates a genuine different folder with the existing authenticated v1 helper and
normal current source/Workers barrier, retains404 for the original task under that folder, and
checks that its response omits the original folder. The separate unchanged absent-folder literal
requires503/read_model_unavailable. Other strict/capacity bodies, frozen intent, production guards,
grants, source protocols and all deadlines remain unchanged. No live execution is claimed here.

The persistent-reader control now writes a real create-only second-folder projection through the
existing SDK writer, verifies its current checkpoint has no task evidence, and records selected
physical reads. Only that folder's task/checkpoint/evidence values are selected; the original
folder's corresponding keys are never observed and its original terminal row is unchanged. The
new handler control uses the actual gateway source adapter and actual folder permission reader
with unsupported native metadata. It remains unavailable rather than zero, produces the existing
FolderAcl denial, and observes neither the counted task model nor the downstream validator.
Existing current-authority, task scope and stale/denial controls remain intact.

Current Debug/project-reference Domain and AppHost builds pass with zero warnings/errors in8.11s
and81.93s. Serial focused execution passes103Domain cases in1.425s,130fixture/HTTP/shared-rebuild
cases in5.412s, and18pending-inventory controls in4.403s, zero failures/errors/skips. The Domain
selection includes the upgraded persistent-reader case, one new unsupported-native handler case,
and existing current-authority/layered/task-scope controls. The first new-test build's CS1061 on an
incorrect decision-property reference is retained; it was corrected before successful qualification.
Only the pending compile-input allowlist/inventory was refreshed for the new test helper and changed
test hashes. Dated immutable PD10 evidence is unchanged; no full-suite or live pass is inferred.

Exact before/after files, approved patch/body/guard hashes, commands, build/test receipts and output
identities are retained under `/tmp/story12-2-foreign-scope-20261104-s76ae741`. Source/outputs are
released with no active build/test/topology. Parent owns the coherent full runtime/Memories rebuild,
current source/PDB/asset/producer audit, revised strict live qualification and exact cleanup. Original
failed runs and sealed packets remain immutable. Story/sprint and independent package/governance,
capacity-identity, SM4 and whole-story review gates remain in-progress or retain their prior outcomes.

### Current SDK human-command fixture compatibility — 2026-11-04

Parent full Debug/source runtime and Memories builds pass; the92-module source/PDB/producer/assets
audit matches the approved d29 body and unchanged companion/frozen hashes. Fresh Domain broad
execution passes2385cases with2existing optional skips; Server broad passes1395, zero failures/errors.
All five frozen matrix rows and both10001/32769 production-handler component histories pass.
These are separate from full live qualification.

The first fresh strict attempt stopped during scheduler health startup before bootstrap in249.385s.
No permanent endpoint mismatch was established; the same-code isolated retry passed that boundary
and genuine bootstrap, then failed the first InitializeOrganizationAclBaseline with HTTP400 in62.605s.
The current SDK CommandsController rejects X-Hexalith-Tenant on human command POSTs, while the
fixture SubmitAsync supplies it. Human commands already bind their canonical tenant in the body and
validated bearer. Command-status GETs independently require the explicit tenant/platform header.
Both attempts retain exact source/runtime receipts, failed results and all-zero owned cleanup.

- [x] In tests/Hexalith.Folders.AppHost.Tests/FolderDurabilityGatewayScope.cs and
  FolderDurabilityAcceptanceFixture.cs, use the supported human-command scope shape for SubmitAsync:
  preserve the original canonical body tenant, human bearer, bytes, tracing and task/idempotency
  identities; omit only the redundant ordinary tenant header. Preserve explicit platform command
  scope, status GET headers, native source request proofs and invalid/mixed-scope refusal.
- [x] Add meaningful command/status shape and preservation controls in
  FolderDurabilityGatewayScopeTests.cs; execute focused fixture and existing SDK command/status
  owner controls serially. Refresh pending inventory only where its existing denominator changes.
  Preserve the approved d29 strict body, companion/capacity bodies and every production policy,
  source proof, deadline and assertion. Hand off changes/results before parent rebuild/audit/live.

This is a fixture compatibility correction within the approved current SDK enrollment, with no
production policy or authorization change. No repeated startup retry or budget change is authorized
by these observations. Story/sprint and independent Release/package/PD10, SM4 and review gates remain
open; the revised foreign-folder and unsupported-absence probes have not yet executed live.

### Human-command fixture compatibility focused handoff — 2026-11-04

The fixture now constructs ordinary human command POSTs with their unchanged canonical body tenant
and bearer, omitting the redundant X-Hexalith-Tenant header rejected by the current SDK. Explicit
platform command namespaces remain intact. Status GETs keep their original explicit tenant/platform
headers; native source requests keep their existing bound proof. Invalid or mixed scopes still refuse.
The approved strict body remains d29afa3ca0649744e84413c37bfbb41c9de0bd106160c900cb2a8c8e970c1b82;
companion/capacity bodies, production policy, identities, grants and every deadline are unchanged.

Debug/source AppHost and SDK Server test builds pass with zero warnings/errors in66.05s and11.80s.
Serial focused execution passes141fixture/HTTP/shared-rebuild controls in5.375s, including11new exact
command-byte/identity/header preservation and invalid/mixed-scope refusal cases, then97unchanged SDK
command/status/platform/validation owner controls in3.694s. Both selections have zero failures,
errors or skips. The affected AppHost compile inputs are outside the existing pending PD10
denominator; pending and historical inventory files are unchanged. No further live pass is claimed.

The two failed live runs, scheduler startup limitation and exact current-SDK400 provenance remain
preserved. New phase before/after files, patch, guard/output hashes and focused receipts are retained
under `/tmp/story12-2-foreign-scope-20261104-s76ae741` as `scope-compatibility-handoff.json` and
`scope-compatibility-phase.patch`. Parent owns current source/PDB/producer/assets audit and any further
strict live run after source/output release. Independent Release/package, PD10, SM4 and review gates
remain open until separately qualified.

### Bounded stop-correlation diagnostic continuation — 2026-11-04

Current consumer selections pass5630cases with2existing optional skips. Current SDK Server broad
passes7684cases with25exactly unchanged optional skips; its three published ABI controls pass with
the unchanged official fixture hash. The current Release gate independently failsCS1061 because
published SDK4.0.4 lacks AddEventStoreGatewayNativeSourceMetadataReader. Debug/source assets were
restored and the92-module producer/source/PDB/assets audit passes. Package changes remain excluded.

Strict4 passes genuine bootstrap, provider configuration/rebuild, tenant maintenance, actual target
denial, six-concern integrity/rebuild/retention, interrupted-generation recovery and concurrent real
Workers. It fails319.939s at the supported EventStore stop command before main restart/revised probes.
DCP reports process-stop timeout11.996s after replacement PID1801241 began stopping, with one observed
in-flight request. Original PID1728410 stopped cleanly in2.049s. Counts alone cannot identify the
outstanding request or establish a hosted-service/native-authority cause. Source/head/runtime drift
and owned cleanup remainders are zero. All four failed attempts remain independent exact evidence.

- [x] Extend only tests/tools/Task36FoldObserver/LifecycleDiagnosticObserver.cs and adjacent
  single-type helpers to correlate existing framework request-start/end IDs with already-existing
  opaque Activity trace/span IDs and primitive framework activity IDs. Preserve original counts,
  lifecycle observations, bounded sink/drop reporting and advisory/nonblocking behavior. Do not
  start activities, read Payload/PayloadNames/Message, request/context objects, routes/URLs/query,
  headers, bodies, identities, tags, baggage or trace-state. Bound active identity tracking; report
  unavailable/unmatched correlation explicitly rather than inventing a join. Keep the SDK's raw
  request-log privacy floor intact. No production source, pipeline, behavior or timing change.
- [x] In tests/Hexalith.Folders.Server.Tests/Task36LifecycleDiagnosticObserverTests.cs and/or
  adjacent tests, verify actual framework request activity correlation with a controlled in-flight
  request, lifecycle matching/drain, bounded/unavailable cases and absence of dynamic payload/tag
  sentinels. Retain existing lifecycle/hosted-service/privacy controls. Build/run affected individual
  Debug/source projects with both flags; refresh only affected pending inventory inputs. Preserve
  strict d29, companion/capacity/frozen hashes and every timeout/retry/shutdown limit.
- [x] Hand off exact patch, source/output/guard hashes and focused results with selected outputs
  released. Parent audits the observer plus coherent selected runtime, then runs the unchanged d29
  class with the new opaque diagnostic and retains exact loaded identities, failures and cleanup.
  Attribute a remaining stop blocker only if the actual correlation joins; otherwise retain the
  uncertainty. No force stop, ignored command result, fabricated quiescence or speculative fix.

This continues the existing fixture-only payload-free diagnostics within the authorized acceptance
work. It adds no route-family observation, source authority, publication or approval exception.
Strict live/probe/SM4, package/PD10 and independent review gates remain open until actually satisfied.

### Opaque stop-correlation focused handoff — 2026-11-04

The actual framework control exposed that the early EventSource RequestStart precedes request
Activity creation. Its failed18/19selection is retained. Matching .NET10.0.14 hosting source supports
the later fixed DiagnosticListener HttpRequestIn.Start/Stop pair around an existing Activity. The
observer now subscribes only to those two keys, explicitly refusing the unsuffixed HttpRequestIn
predicate that participates in creation. It never reads notification Value or EventSource payload,
request/context data, activity tags/baggage/trace-state, routes or credentials. No Activity is started.
Raw framework counts/opaque IDs and later Activity correlation remain separate; unavailable or
unmatched observations are explicit. Active identity tracking is bounded at64 and listener tracking
at8; overflow/contention degrades correlation without waiting or changing request/host behavior.
The existing64-record advisory queue, one-second snapshots and all host/shutdown limits remain intact.

The in-process real Kestrel control independently enables framework Activities, holds a request
across actual stopping, matches its existing trace/span in start/snapshot/end records, then releases
and verifies drain and private-sentinel absence. A second real Kestrel control disables independent
creation and proves observation does not create an Activity. Focused controls also retain existing
hosted-service gates and verify invalid/ambiguous/unavailable/capacity cases, ignored notification
values, callback failure and disposal. Final Debug/source Server build (including the observer)
passes with zero warnings/errors in10.76s;28focused cases pass in5.611s and18pending-inventory cases
pass in4.035s, zero failures/errors/skips. The initial two CA2007 test-helper build errors are retained
and corrected. Only affected pending compile inputs/hashes were refreshed; dated PD10 is unchanged.

The fixture selects the observer directly from tests/tools/Task36FoldObserver/bin/Debug/net10.0;
its AppHost ProjectReference has ReferenceOutputAssembly=false. No AppHost source changed in this
phase and the source-mode Server build produced the selected observer output. Exact phase patch,
source/output/guard hashes, primary-source assessment and all focused receipts are retained in
`/tmp/story12-2-foreign-scope-20261104-s76ae741` as `stop-correlation-handoff.json` and
`stop-correlation-phase.patch`. Parent audits the actual observer plus current runtime and owns any
further unchanged d29 strict run. This diagnostic establishes no stop-cause correction, main restart,
revised task-scope probe or full acceptance pass. All independent open gates retain their disposition.

### Parent approved strict qualification — 2026-11-04

The owner-approved d29 strict class passes1/1, zero failures/errors/skips, in396.332s on root
9198d9213f6ea54c629148322f05e7ded994822d and SDKaeec5012fa3e1e07426a8fdd145709bce93f619a.
It creates a genuine independently authorized different folder through ordinary v1 creation, observes
normal current source/Workers evidence, and receives404 for the original task with no original-folder
disclosure. The separate unchanged never-created absent-folder probe returns503/read_model_unavailable;
missing-hint503 remains. The full remaining real-provider pipeline, changed-PID restarts, exact retained
prefix/replay, closed-binding denial, all six concerns, rebuild/recovery, concurrency, retention and
safe storage failures also pass. No earlier failed run is reclassified.

Initial consumer selections pass5630cases with2existing optional Domain skips; current SDK Server broad
passes7684with25exactly unchanged optional skips and all3published ABI controls. Final observer controls
pass28, pending inventory18, and unchanged source secret-scanner controls274, all zero failures/errors/
skips. Focused selections overlap broad suites and are not added. All five frozen matrix rows and
10001/32769 production-handler component histories have executed passing coverage. The original full
runtime/Memories Debug/source build and the final98-module observer/runtime/source/PDB/assets audit
pass, with one SDK hash/MVID per assembly and project-only selected Hexalith edges. Guards, frozen
intent, dated PD10, companion/capacity bodies, native proof, authorization and every deadline remain.

Five strict attempts are retained: scheduler startup failure, deterministic human-command header
mismatch (fixture corrected), Sentry startup failure, DCP process-stop timeout, then the full pass.
Opaque diagnostics actually emit2987exact Activity pairs;703observation_busy records remain explicit.
All four observed EventStore lifecycles in the final run reach ApplicationStopped with in-flight0.
This successful execution supplies no failed-stop cause or shutdown-fix claim. Selected observer
DLL/PDB/source are qualified and actual new behavior is recorded on all four PIDs; its loaded module
MVID/SHA was not separately captured, so that visibility limit is preserved. The253actual saved SDK
observations match the audited producer. All parent commands have zero source/head/runtime drift;
all owned processes, containers and exact Dapr directories are gone, with no unrelated-resource action.

Final measured SM4 p50/p95 milliseconds: command acknowledgement6802.849/8626.066(count10,budget1000),
Workers delivery108.189/266.760(count27,diagnostic budget1000), status read817.379/1085.010(count12,
budget500). Command and status p95 remain open findings; no sample/budget change or general performance
redesign is made. Fresh exact Release contract-spine gate still failsCS1061 at Server67/Workers87:
published SDK4.0.4 lacks AddEventStoreGatewayNativeSourceMetadataReader. Debug/source assets were
restored and audited afterward. Package publication/pin changes are outside this continuation.

The [new qualification packet](evidence/story-12-2-foreign-task-scope-20261104T110926Z/README.md) retains
exact approval/body transitions, phase patches, commands, source/module checksums, all failed attempts,
closed diagnoses, safe test summaries, cleanup and privacy/seal verification. All three prior sealed
packets remain unchanged and independently verify. Raw logs/XML and parameter display payloads stay
private. The bounded approved implementation and current strict source qualification are complete;
whole-story/sprint stay in-progress for Release/package, PD10/governance, SM4 and independent review.


### Review-five implementation focused handoff — 2026-11-04

Projection checkpoint schema3 now retains current operational state without primary content, File,
publication, erasure, committed-reference or native released-fence dictionaries. Projection-specific
file folding preserves lifecycle/provider-work semantics and source-local binding consistency;
historical File/version/erasure authority remains in the unchanged primary fold. A source-lawful
4000native acquire/release history plus256distinct native publications passes the production named
SDK rebuild, bounded mutation/checkpoint assertions, subsequent live older-incarnation version2
change/renew/complete, grown rebuild and original-source duplicate checks. Native release remains
nonterminal; native completion retains its original terminal result.

Bounded SDK logical completion records retain each task's earliest original closure position, time
and source prefix independently of C3 status/evidence expiry. Legacy and native401-day paired-expiry
then FolderAccessGranted controls preserve safe absence, original closure and complete-source parity;
reused tasks in later workspace bindings retain the same earliest result. Missing physical completion
values retain the SDK pointer and fail unavailable through its existing selected-value guarantee;
corrupt completion also refuses without checkpoint advance. Named complete-source repair restores
the original evidence and valid delivery resumes without recreating an expired task.

Older schema2 compacted checkpoints cannot establish complete closure coverage: new live delivery
refuses without acknowledgment until the existing protected named complete-source rebuild publishes
schema3. Intact source-bound old rows remain readable under existing freshness/retention policy.
The migration control validates the old source anchor, refused direct/named delivery, named repair,
resumed delivery and exact source digest. Operator guidance uses that existing transport and requires
no manual storage mutation or new privilege.

Native acquisition metadata now validates supported lock/task/lease/interval shape before persistence,
while actual acquire/renew/release/complete semantics, including zero-interval expiry recovery release,
remain lawful. Eighteen malformed publication-binding cases additionally preserve source-local scope,
command/item key, file/operation/kind/path/content/fence/version/policy/removal consistency. Absent
new task/workspace rows at checkpoint8/head9 are stale; current paired absence and positively expired
rows retain their safe outcomes. Existing tokens stop source folding, operation materialization,
chunk serialization and final-operation enumeration. Opaque diagnostic tracking reconciles unmatched
or unidentified confirmed drain; real non-waiting contention poisons snapshots until drain, then
subsequent matching recovers. Existing real framework Activity/privacy controls remain passing.

Final serial Debug/source project builds pass with zero warnings/errors. Executed individual-assembly
selections pass163Domain operational/task-query cases in56.470s (including10001/32769production-handler
histories),32Server/observer cases in2.861s,162Workers cases in4.346s,26pending-inventory/retention-doc
cases in4.444s and274unchanged source-scanner cases in5.488s; zero failures/errors/skips. Earlier
overlapping selections remain separate. The initial null-guard analyzer build failure and six new-test
record/dictionary reference-comparison failures are retained; exact serialized comparisons corrected
those assertions before qualification. Only six new compile inputs and changed pending-input hashes
were refreshed; dated immutable PD10, strictd29, companion/capacity and frozen hashes remain unchanged.

Exact before/after files, phase-only patch, commands, all intermediate/final receipts, source/output
identities and guard audit are retained in `/tmp/story12-2-round5-implementation-20261104` as
`handoff.json` and `round5-phase.patch`. Source/outputs are released with no active build/test/topology.
Parent owns coherent runtime/Memories/source/PDB/assets audit, remaining current consumer/matrix/
capacity qualification and unchanged d29 real-provider execution. The prior396.332s pass remains
exact old-source evidence. Story/sprint remain in-progress for current live qualification and separate
Release/package, PD10/governance, SM4 and independent review gates; no whole-story pass is inferred.


### Fixture-only lifecycle observer control — 2026-11-04

Current parent qualification passes5681consumer tests with2existing optional skips, all five frozen
matrix rows and10001/32769production-handler histories. Coherent runtime/Memories builds and98-module
source/PDB/assets audits pass. Four current strict attempts fail actual supported EventStore stop
commands (315.760s,274.703s,274.922s diagnostic,270.423s); none is a current-source full pass. All own
cleanup/source/head/runtime/status remainders are zero. Actual opaque request joins and subsequent
snapshots prove requests drain before the deadline, while ApplicationStopped is absent. A method-only
stack capture identifies no specific async shutdown await; its background OTLP retry is not proved
causal. Normal SDK logging verbosity also reproduces the failure. SDK source/guards/deadlines remain
unchanged. Prior396.332s old-source pass and all sealed packets remain immutable.

The fixture always injects a nonempty lifecycle path. Task36HostingStartup also selects the folder
fold observer when that path is empty, so clearing it is not a valid observer-only comparison.
Implement the following bounded qualification control over the preserved completed review-five work;
no production implementation, SDK, strict body, shutdown ordering/timeout/retry, authorization, source
proof, telemetry policy, service replacement or public route change is authorized by this task.

- [x] In `tests/tools/Task36FoldObserver/Task36HostingStartup.cs` and
  `tests/Hexalith.Folders.AppHost.Tests/FolderDurabilityAcceptanceFixture.cs`, add an explicit opt-out
  for the optional EventStore lifecycle diagnostic only, using an exact per-resource environment
  flag such as `FOLDERS_TASK36_LIFECYCLE_DIAGNOSTICS_DISABLED=true`. Default/invalid/missing values
  preserve existing observation. Forward the flag only to the fixture-owned EventStore resource.
  Disabling must omit only LifecycleDiagnosticObserver registration; retain the nonempty shutdown
  path's existing fold-registration semantics, the actual production services and Folders' mandatory
  fold evidence. No dummy sink, private handler, service proxy or forced Activity is permitted.
- [x] Add meaningful focused control(s) in existing Tool/Server/AppHost test locations proving
  default-on and explicit-off service registration/behavior, invalid-value default, retained
  Folders fold observation and absence of extra fold observer on EventStore. Build/run affected
  individual Debug/source projects serially with both flags; refresh only affected pending compile
  inventory hashes/paths and run its controls plus unchanged source privacy scanner as appropriate.
  Preserve strictd29, companion/capacity, mutation2s, datedPD10 and frozen497c hashes.
  Verified on the revisions recorded in the November 26 qualification below.
- [x] Hand off a new exact phase patch, before/after/guard/output hashes, commands and all results
  under `/tmp/story12-2-observer-control-20261104`; never overwrite prior handoffs. Release all
  selected outputs with no active build/test/topology. Parent owns coherent runtime/Memories audit,
  any affected requalification and one unchanged strict class with the supported observer opt-out.
  Treat that comparison separately; one outcome alone does not prove causality or fix prior stops.

This is a fixture diagnostic control for required acceptance qualification, within the existing
payload-free advisory diagnostic work. It does not authorize generic SDK shutdown/performance repair
or policy changes. Story/sprint remain in-progress; current strict, package, PD10/governance, SM4 and
independent review gates stay open. Parent performs no competing build/test/live while delegated
implementation owns selected source/outputs. Existing rendered workflow/baseline are preserved.

### Current fixture-only observer verification — 2026-11-11

The requested exact-true lifecycle opt-out and its controls are already committed at root
`dc7198ae6bcd14dd383c642e6f86da061080ea45`; no product, fixture, Tool or SDK source change is
needed for this control. Current SDK HEAD is `c94e6fffaf2d689c94bb288f151a65e10a2fea82`.
Server/Tool, AppHost, Contracts and SDK Server Debug/source builds pass with zero warnings/errors.
Use both source flags and the current catalog's explicit `HexalithEventStoreVersion=4.2.0`
throughout the graph. The first default-property build mixed Client/Contracts4.1 with SDK4.2;
its observer selection had41passes/2assembly-load failures. That exact attempt is retained.
The coherent final observer selection passes43/43 and actual fixture resource-wiring controls
pass4/4, zero failures/errors/skips. These prove default/invalid/exact-true registration,
unmodified existing services, retained Folders fold evidence and EventStore-only forwarding.

The controls checkbox remains open for preexisting broader gates. Pending inventory controls
have14passes/4failures: the allowlist omits OwnerSourceCompositionAssertionIssuer.cs and eight
listed inputs have stale hashes. The unchanged source privacy scanner has273passes/1failure,
reporting only four path/line findings in SDK payload-protection/generator tests. No credential
value is retained in this note. Neither the inventory, scanner nor those unrelated tests was
changed in this bounded phase. The parent owns the minimal current-inventory repair and separate
disposition of the scanner failure, preserving dated PD10 and scanner rules.

Exact commands, all results, before/after source and guard hashes, output identities and the
phase-only documentation patch are retained under `/tmp/story12-2-observer-control-20261104`.
The d29 strict, companion/capacity, mutation2s, dated PD10 and frozen497c guards are unchanged.
Selected outputs are released with no active build/test/topology. Parent owns coherent runtime/
Memories/source/PDB/assets qualification and one unchanged strict class with the supported
opt-out. No new live pass, stop-cause diagnosis or shutdown correction is claimed. Whole-story,
Release/package, PD10/governance, SM4 and independent-review gates retain their separate status.


### Current source composition and acceptance result — 2026-11-11

- [x] `Directory.Build.props` and `src/Hexalith.Folders.Contracts/Hexalith.Folders.Contracts.csproj`:
  select the root-declared McpCli abstractions source in local source mode, matching the existing
  Hexalith dependency policy. Actual MSBuild evaluation passes for default/Debug source mode and
  Release/CI package mode. Existing package versions, gitlinks and public contracts are unchanged.
- [x] `tests/Hexalith.Folders.Contracts.Tests/McpCli/PackedContractsClosureTests.cs`: give the
  Release package build its own artifacts/restore directory and explicit package mode. Preserve
  every package/framework/assembly dependency assertion and assert the workspace assets remain
  byte-identical. The initial source-mode package-test failure is retained; the corrected test
  passes in both Debug and Release.
- [x] `scripts/pd10-v2-story-owned-paths.txt` and the pending source inventory: include the existing
  owner-composition assertion helper, bind previously declared additions and refresh stale input
  hashes. The deterministic inventory now binds 1,608 artifacts. Dated immutable PD10 is unchanged;
  `pending-a6b`, `candidate-awaiting-a6b`, disabled exposure and false closure claims remain.
- [x] Current consumer, matrix, capacity and producer qualification: final serial Debug/source
  solution and Memories builds pass with zero warnings/errors using both source flags,
  `HexalithEventStoreVersion=4.2.0` and stable local `VersionSuffix=story12-2-qualification`.
  Six individual consumer assemblies pass 5,664 tests with 2 existing optional Domain skips and no
  failures. All five frozen matrix rows and 10,001/32,769 histories through the production handler have executed
  passing coverage. Final Release/package solution build and 422 contract/75 client generation checks
  pass, zero failures/skips; the additional Release package-closure control passes 1/1.
- [ ] Current strict real-provider acceptance: the unchanged d29 class with the exact supported
  lifecycle opt-out fails 1/1, zero skips, in 194.190 seconds. Authenticated bootstrap reaches 202/Completed
  with original source 1 and catalog/human binding. The initial unknown-profile provider-configuration
  safe-denial request is canceled before its expected HTTP 403 assertion completes. Task closure, main
  restart/rebuild and full SM4 measurements are not reached. The unchanged 75-second HTTP retry
  policy and every production deadline/guard remain. Catalog/delivery availability warnings are
  retained privately; no causal attribution, observer comparison conclusion or shutdown fix is
  established by this failed attempt.

The source audit passes for 99 selected assemblies with matching DLL/PDB/document identities, project-only
selected Hexalith edges and one hash/MVID per SDK assembly. Final source restoration after the Release
checks produces exactly the same 99 binary/symbol identities as the live producer. All 24 saved actual SDK
observations match that producer. No mapped observer module identity was captured; selected observer
source/output qualification is separate. The actual run has no source/head/runtime/status drift and
no remaining owned descendants, containers or observed Dapr/fixture directories. Unrelated resources
are untouched. Initial graph/PDB and intermediate failed test attempts remain separately recorded.

The [current metadata-only qualification](evidence/story-12-2-current-qualification-20261111.json)
retains commands, phase results, exact source/guard bindings, matrix coverage, failure and cleanup.
Raw logs/XML, bodies and private provider context stay under the private receipt directory named
there. The first/third observer handoff tasks remain verified; its broader controls checkbox stays
open because the unchanged SDK privacy scanner still has 273 passes/1 failure at four existing SDK test
locations. Pending inventory failures are now resolved. Whole-story and sprint remain in-progress
for the current live failure, SDK privacy gate, PD10/governance, existing SM4 findings and independent
review. Historical live passes keep their exact-source scope; none qualifies this current failed run.

### Current workload issuance boundary — 2026-11-11

The retained current strict failure now has an executable source-issuance diagnosis. Its actual
Folders process logs `bindings-unsupported` during the first provider-configuration request; the
actor rejects after617.112ms, before the outer request exhausts its unchanged retry budget. Current
SDK source credentials require signed tenant/domain bindings, while the configured ordinary
Keycloak client-credentials profile cannot issue them. The two new Server/Workers-identity controls
exercise the actual issuer and public SDK metadata composition, refuse before token/source HTTP
transport, and preserve unavailable authority rather than inventing zero. No production guard,
issuer configuration, permission, SDK source, strict body or deadline was changed.

The [precise remaining prerequisite](proposal-12-2-bound-source-workload-authority.md) separates
already-authorized local workload/key/receiver wiring from missing actual external authority and
owner-provisioned trust/grant inputs. Shared issuer/receiver trust also affects existing unbound
callbacks, query-authority digests and Tenants bootstrap/source calls; their separately exact grants
must survive any migration. Enabling an option alone, substituting an unbound assertion, or adding
the SDK's nonconforming Development issuer cannot establish this acceptance authority. No existing
supported source-only issuer selection or configured conforming external profile was identified.

Final focused Debug/source build and scoped whitespace verification pass. Source/composition/observer
controls pass68/68, and pending-inventory controls pass18/18, zero failures/errors/skips. Existing
SDK issuer/authority controls pass130/130; the unchanged source privacy scanner remains273passes/
1failure at the same four SDK test locations. Only this test input's pending hash/size and set digest
changed; the dated PD10 and all three protected acceptance bodies/frozen497c remain unchanged.
The24actually observed focused modules pass DLL/PDB/source audit. A carried99-module full-runtime
audit separately finds nine old runtime-copy generated AssemblyInfo mismatches after the focused
build; it supplies no new coherent-runtime/live qualification. SDK variants/project assets and all
guards remain valid. No new strict/live run or SM4 result is claimed.

[Metadata-only phase evidence](evidence/story-12-2-workload-issuer-boundary-20261111.json) and the
private phase patch/receipts retain commands, intermediate whitespace failure, final outcomes and
the exact audit limit. Source/outputs are released without active build/test/topology or owned
resource remainders. Existing local wiring remains authorized once actual owner inputs exist;
whole-story/sprint stay in-progress for that prerequisite/current live failure, SDK privacy,
PD10/governance, SM4 and independent review. Every earlier packet and failed run is preserved.

### External workload source prerequisite — 2026-11-11

The approved [external workload authority spec](spec-12-2-external-workload-authority.md)
now has a Platform-owned Keycloak 26.6.4 provider, protected enrollment consumer, Folders
AppHost/fixture integration before catalog capture and an enrollment runbook. Source work
keeps actual activation disabled without protected owner inputs. Human authentication,
independent RSA proofs, source authorization, strict acceptance bodies and all deadlines
remain unchanged; Memories is outside the enrollment.

[Component qualification](evidence/story-12-2-external-workload-authority-20261111/verification.json)
records three successful Debug/source builds, 23 Java policy tests, 11 actual-issuer SDK
controls, five live consumer controls, eight AppHost configuration/source controls and
23 existing Server controls, with zero failures/skips in these final selections. Fifteen
additional provider refusal controls, separate callback/query grants, live grant replacement
and observed zero task-owned cleanup remainders passed. These are disposable component
results; their signer and TLS fixture establish no approved enrollment authority.

The parent diff/hash audit verified all 19 source bindings, all three protected test bodies
and the retained earlier-source failed strict result. DomainService, Client, Contracts and
ServiceDefaults copies in both selected test outputs match their Debug/source binaries;
this comparison does not establish a current Folders/Workers topology audit.

Actual protected ManifestPath/ManifestSha256 references, approved TLS/signer trust and
complete current grants for the actual cohort remain absent. After those inputs are
supplied, qualify the enrolled runtime and run the unchanged strict real-provider class.
The parent and child remain in-progress; privacy, PD10/governance, SM4 and independent
whole-story review retain their separate open dispositions. No commit, push, dependency
pin update, publication or deployment was performed.

### Run-owned workload authority decision — 2026-11-13

A recheck found the owner enrollment references still absent. Investigation showed they could
never match this story's strict fixture. `FolderDurabilityAcceptanceFixture` draws a random run
identity for every run and embeds it in every caller AppId and in `TenantId`, and the Platform
consumer refuses any manifest that doesn't name exactly that cohort and tenant. Jerome approved
[sprint-change-proposal-2026-11-13](../planning-artifacts/sprint-change-proposal-2026-11-13.md).
Inside the disposable strict fixture, enrollment now comes from a Platform run-owned
authority (Platform Story 2.12, backlog). It is enrolled for that run only and verified by the
unchanged consumer. The renegotiated scope, the run-owned evidence label and the single
authorized Platform gitlink update live in the
[external workload authority spec](spec-12-2-external-workload-authority.md)'s frozen intent.
This parent's frozen block (`frozen497c`) is intentionally unchanged.

Owner enrollment remains the only activation path outside the fixture; its proof moves to
Platform Story 10.1 in staging. The new pending external node
`EXT-PLATFORM-RUN-OWNED-WORKLOAD-AUTHORITY` is a prerequisite of 12.2. The previously failed
strict run's canceled 403 control is not attributed to enrollment and remains open. Strict
bodies, deadlines, retries and budgets are unchanged. The parent and child remain in-progress.

### Selected latest source focused qualification — 2026-11-13

The user selected **keep latest** after the changing-checkout checkpoint. Qualification resumes on
clean root `6fed61e27f316a41a63d9810de1f8936a929acea`, with Platform
`a7f901ae79ed5fd7ad2f04f0779b21108a4cdee9` and EventStore
`fd07d0ab33858818d836e43bf8c112c5d4827534`. The run-owned producer, final-cohort fixture adoption,
unchanged enrollment consumer and receipt-refusal controls are already present. No product or test
source correction was required in this focused phase. Existing pins and the original story baseline
are preserved.

Five individual Debug/source test-project builds pass with zero warnings/errors, using both source
flags, current `HexalithEventStoreVersion=5.0.1`, `VersionSuffix=story12-2-qualification` and the
selected graph's `net11.0` target. Direct assembly selections pass 81 AppHost configuration/scope/
maintenance/observer controls, 163 Domain operational/task-query controls, 68 Server source/composition/
observer controls and 162 Workers cases, all with zero failures/errors/skips. The Domain selection
executes both 10,001/32,769-position production-handler histories.

The initial current pending-inventory selection has 19 passes and two failures from stale metadata
for 15 existing selected inputs. Its existing deterministic generator refreshes only the mutable
2026-11-12 pending manifest; the 1,610 paths are unchanged. The final selection passes 21/21 with zero
failures/errors/skips. Both older immutable inventories, pending A6b/disabled exposure/false closure
flags, strict d29, companion/capacity bodies, mutation budget and frozen497c remain unchanged.

[Metadata-only focused evidence](evidence/story-12-2-latest-source-focused-20261113.json) binds commands,
results, source/repository guards and selected output hashes. All 22,038 captured technical inputs
and 13 repository heads remain stable. Exact receipts, raw logs/XML and the output inventory remain
private under the directory named in that evidence. No live topology or run-owned authority was
launched, so no new enrollment, strict acceptance, loaded-runtime or SM4 result is claimed. Source
and outputs are released without an active build/test/topology; the parent owns coherent runtime/
Memories qualification, actual loaded-identity audit, unchanged strict execution and owned cleanup.
Whole-story/sprint, SDK privacy, package/governance, SM4 and independent review retain their separate
open dispositions. Earlier sealed evidence remains unchanged.

### Latest runtime qualification and pending audience correction — 2026-11-13

Qualification continues on the user's selected latest dependency graph. A solution build first
fails because the load-test project explicitly targets net10.0 while its references target net11.0;
removing that stale override restores the full Debug/source build. Memories builds independently,
and the current provider's 23 policy tests pass. Six broad consumer assemblies execute: five pass;
Contracts records 683 passes/six failures from obsolete net10 package paths, pattern metadata and
the governance report fixture. Updating those current-framework inputs yields 239/239 affected
contract/governance/inventory passes. Earlier failing results are retained, not relabeled.

The loaded-module audit then identifies four old net10 metadata artifacts loaded by
`FoldersDevelopmentCatalog.Load`. It now derives the target framework directory from the selected
AppHost build, retaining the existing configuration, existence and hash checks. The composition
control and 81 existing fixture controls pass. Three newly owned configuration/runtime inputs
are added explicitly to the mutable pending inventory, producing 1,613 paths; its 21 controls pass.
The older inventories, pending A6b, disabled exposure and false closure flags remain intact.
The unchanged current SDK source privacy scanner now passes 286/286, zero failures/skips; this
supersedes the old-source scanner failure only for that executed selection, not full SDK regression.

The final producer audit passes for 99 assemblies, matching ordinary/generated sources, PDBs,
project assets and SDK variants. The current actual-loaded audit matches every scoped observation,
including all 24 observed SDK copies. The prior strict attempt's seven observations from old net10
metadata artifacts retain their failed qualification scope; they are not used as current runtime proof.

Real run-owned consumer controls pass for eight physical resources and six workloads. The unchanged
d29 strict scenario still fails 1/1, zero skips, in 142.301 seconds during owner-bootstrap observation;
its original deadline cancels before the scenario body, original 403 control, task/restart/rebuild
proof and SM4 measurements. Verified enrollment is observed. No source/head/runtime/status drift
occurs during that run, and observed owned descendants, containers and directories are all absent
after cleanup. No provider success or source zero is inferred from the failure.

An independent run-owned issuer probe accepts the configured physical gateway audience (HTTP 200)
but refuses the native SDK's logical `eventstore` audience (HTTP 400), using the same actual caller,
operation and binding set. The [canonical-audience proposal](proposal-12-2-canonical-workload-audience.md)
contains a five-file Platform patch and required real-SDK regression/strict qualification. Its
applicability check passes; it has not been applied. Frozen child intent requires an unchanged
consumer, so the audience-selection correction awaits explicit approval. Platform Story 2.12 is
still in-progress; no external prerequisite or whole-story acceptance is declared complete.

[Metadata-only runtime evidence](evidence/story-12-2-latest-runtime-20261113.json) retains exact
commands, phases, source/output identities, failures and cleanup. Root changed externally from
`6fed61e27f316a41a63d9810de1f8936a929acea` to
`db40bda8a107fcb03405e074810be16c0d47c85f` after live qualification; that commit changes only
README migration-inventory citations and is preserved separately. Dependency heads remain stable.
The original story baseline, frozen497c, all three protected acceptance bodies and every budget
remain unchanged. Story/sprint stay in-progress for the pending audience decision/current strict
failure, separately unqualified full SDK/Release-package gates, PD10/governance, SM4 and independent
whole-story review. No commit, push, dependency update or deployment was performed by this workflow.

### Approved audience continuation and remaining stop failure — 2026-11-14

Jerome's “approve” authorized the bounded canonical gateway-audience correction in the
[proposal](proposal-12-2-canonical-workload-audience.md). The selected latest checkouts are
preserved, including root `db40bda8a107fcb03405e074810be16c0d47c85f` and Platform
`a7f901ae79ed5fd7ad2f04f0779b21108a4cdee9`. The correction is applied in Platform's working tree:
gateway grants and receiver expect exactly the native SDK audience `eventstore`, while actual
caller/client IDs, physical Dapr targets, callback audiences, exact operations/bindings and
grant counts stay unchanged. The earlier pending-approval notes are historical and superseded
by this approval. The child frozen intent has only the approved exception; this parent frozen
block remains SHA-256 `497cba1a5e7c3670502f2c49e396ac77fbfa66a337ae199aafaeab6313226422`.

The [focused evidence](evidence/story-12-2-canonical-audience-focused-20261113.json) records
passing provider/Python, native SDK issuance and physical-audience substitution refusal controls,
including the actual run-owned authority, independent assertion validation, exact source bindings
and existing RSA source proofs. Final counts are 23 provider, 44 Python, 36 Platform inventory/
refusal, one Platform live, one bridge-cancellation, one Folders live, 11 existing actual-issuer
SDK, five consumer, ten enrollment-model and 23 source-boundary cases. The ordinary Platform
test-project build retains an unrelated 57-error analyzer blocker; focused selection preserves
normal analyzers and generated/package registrations without suppression.

The [current runtime evidence](evidence/story-12-2-canonical-audience-runtime-20261114.json)
binds the full Folders and Memories Debug/source builds (zero warnings/errors), 286 unchanged
SDK privacy cases, 21 pending-inventory cases and 82 fixture controls. It retains all earlier
attempts with their exact source scope. The latest 99-assembly source/PDB/assets audit passes;
all 116 actual SDK observations in the decisive live run match its saved producer. No source,
head, working-tree status or runtime-file drift occurs during that run. The mutable inventory
now binds 1,615 inputs; A6b approval, production exposure and closure flags remain pending/disabled.

The first post-audience strict run fails after 271.290 seconds at discovery with an expired
cached fixture assertion, after real bootstrap, the original 403 denial control and terminal-ready
workflow assertions pass. The fixture now renews the same assertion before ordinary discovery
and each new shared-rebuild action, using its existing issuer/helper. A 90-second preflight
window protects the unchanged 75-second action; issuer maximum remains 120 seconds, authority
lease remains 900 seconds, and retries never renew headers or retry 401. A subsequent 291.099-second
attempt fails with two recorded filesystem/console `No space left on device` errors and canceled
status polling; sole causality is not inferred. No unrelated disk resources were removed.

The fresh final invocation is the unchanged strict class under the disposable authority wrapper:

```text
python3 -B references/Hexalith.Platform/infrastructure/keycloak-workload-binding/run_owned.py --owner disposable-folders-acceptance --lifetime-seconds 900 -- dotnet tests/Hexalith.Folders.AppHost.Tests/bin/Debug/net11.0/Hexalith.Folders.AppHost.Tests.dll -class Hexalith.Folders.AppHost.Tests.OperationalStatusDurabilityAcceptanceTests -showLiveOutput -result-xml <private-receipts>/strict-refreshed-recheck-private.xml
```

Result: exit 1, one failed test, zero errors/skips, **474.271 seconds**. There are no storage
errors in this attempt. The original provider 403 control, prepare/lock/clean-release/terminal-ready
flow, all six concerns' integrity and complete-source rebuild, source-time retention TTLs,
interrupted sealed-generation recovery across changed PIDs, resumed promotion/replay and two
concurrent Workers' exact convergence pass before failure. The first complete four-host restart
succeeds; the second restart's EventStore stop command returns false after DCP reports
`timed out waiting for process to stop`. Existing command-result validation rejects that failure.
Later restart/fault controls and complete SM4 qualification are not reached. This is partial
assertion evidence in a failed test, not a current strict pass.

The supported fixture-only lifecycle observer opt-out is retained. Read-only SDK inspection
confirms logger-provider registration; an isolated synthetic host stops/disposes successfully,
but does not identify the full EventStore/DCP shutdown cause. The existing scope says:
“It does not authorize generic SDK shutdown/performance repair or policy changes.” No SDK
shutdown source, command timeout, order, retry, telemetry policy, strict body or acceptance
budget was changed. All observed owned descendants, containers and temporary paths are absent
after each run; no owner-enrollment qualification is claimed.

Child approved frozen SHA-256 remains
`a31c07d7cb62a688a0ab1e0999a790800f5f9213588663215763230b5adecb03`; all three protected test
body hashes remain unchanged. No commit, push, dependency update or deployment was performed.
Story/sprint remain **in-progress at step 03**. Current strict acceptance, the external
prerequisite disposition, full SDK/Release-package gates, PD10/A6b governance, complete SM4 and
independent whole-story review remain open.


### Latest-checkout continuation — 2026-11-14

After the changing-checkout checkpoint, Jerome answered “yes” to continuing on the latest
checkout while preserving Platform’s seven existing changes. The selected root is
`b915d3ec5180f45c4c477cbeffb9b98df91082d0`; EventStore is
`51dd68b86a690ffe9e11c565dccec49b2d401a94` and Platform is
`2a1a343c35d58e6368cd4c34288dc3f9c567f8b9`. Preserve all other current dependency heads,
the existing Platform audience correction, the original baseline and every frozen/strict guard.
This supersedes the older non-frozen checkout-selection notes only; no Git mutation or new
SDK repair scope is authorized.

Resume step 03 on this source graph. Finish loading the specification and frontmatter context;
do not render or dispatch the parent workflow again. Investigate the existing strict EventStore
stop failure using preserved private receipts, source inspection and bounded diagnostics before
selecting a correction. Build/test/runtime work is sequential and belongs to the implementation
handoff until released. Keep protected values in private receipts and record only metadata in
repository evidence. The prior exclusion of generic SDK shutdown/performance repair and policy
changes still applies. If the demonstrated correction crosses that boundary, prepare a concrete,
reviewable proposal with the identified owner, affected files, regression and unchanged strict
qualification plan; do not apply it without a further human scope decision. Do not infer a strict
pass from the retained partial results or change timeouts, ordering, retries or acceptance bodies.


### Latest-checkout stop qualification — 2026-11-14

The user-selected root `b915d3ec5180f45c4c477cbeffb9b98df91082d0`, EventStore
`51dd68b86a690ffe9e11c565dccec49b2d401a94` and Platform
`2a1a343c35d58e6368cd4c34288dc3f9c567f8b9` remain selected. All seven existing
Platform changes are preserved. No production, test or SDK source correction was made.

Fresh serialized Folders solution and Memories Debug/source builds pass with zero warnings/errors,
using both source flags, `HexalithEventStoreVersion=5.0.1` and
`VersionSuffix=story12-2-qualification`. The pre-live producer audit passes for 99 assembly files;
four additional actually observed UI/FrontComposer metadata files pass their subsequent ordinary/
generated-source, PE/PDB and source-project asset audit. All 572 scoped actual observations,
including 232 SDK observations, match the resulting 103-file producer scope. The selected observer
source/output is audited; no actual loaded observer identity is inferred where none was saved.

The first unchanged strict attempt fails 1/1, zero errors/skips, in **302.545 seconds** at the first
EventStore stop, after six-concern rebuild assertions. Its intended diagnostic capture misses the
actual launcher shape; no process suspension occurs. This attempt and its matcher failure remain
retained. Eleven private matcher controls then qualify selection from the fixture's exact resource/
process binding, current mapped host, owned ancestry and process-start identity, with refusal of
wrong-host, unowned and reused process identities.

The corrected unchanged strict invocation passes **1/1**, zero failures/errors/skips, in
**423.055 assembly seconds** (**351.412 test-body seconds**). It runs through the existing disposable
run-owned authority wrapper and completes all original provider/task/restart/rebuild/replay/retention/
concurrency/denial/fault assertions. It was launched as a diagnostic attempt, but none of its three
recognized EventStore stops reaches the eight-second capture trigger: **no live heap capture or
process suspension occurs**. Source, heads, working-tree status and runtime files do not drift during
execution. Every observed owned descendant, container and temporary directory is absent afterward.
This current strict pass does not identify a stop cause, establish a shutdown repair or erase any
retained intermittent failure.

A private synthetic capture preflight confirms the supported diagnostic-client route and a compatible
private analyzer; its .NET 11 async-completeness warning remains explicit. A separate exact-service-DLL
cancellation probe finds a later logger can remain unclaimed when stop returns, but current telemetry
provider disposal receives a finite 5,000 ms budget. It establishes no unbounded hang or strict-failure
causality. No SDK shutdown, policy, ordering, timeout, retry or acceptance body was changed.

Current SM4 p50/p95 milliseconds remain over budget for command acknowledgment
**6527.249/9641.575** (10 samples, 1000 ms p95 budget) and status read
**752.982/908.407** (12 samples, 500 ms). Workers delivery is **111.688/264.381**
(27 samples, unchanged 1000 ms diagnostic budget). The command stopwatch includes the existing HTTP
retry wrapper; the explicit one-second fixture delay and owner bootstrap are outside it. Retained
successful gateway metadata ranks BindRepository at 9166.227 ms, PrepareWorkspace at 7946.583 ms and
ConfigureProviderBinding at 7208.961 ms; BindRepository's actor summary is 4080.438 ms. The unlabelled
metric samples retain no exact command association or retry-delay split, so these observations do
not locate the dominant time in the approved native authority/bootstrap subpaths. Record the open
findings and preserve the existing instruction to avoid redesign.

[Metadata-only qualification evidence](evidence/story-12-2-latest-checkout-stop-qualification-20261114.json)
retains commands, guards, phase distinctions, source/loaded identities, failed attempts, timing,
privacy limits and owned cleanup. All earlier packets remain unchanged. Current consumer suites,
required capacity evidence, full SDK and Release/package consumption gates and independent whole-story
review remain available for their existing authorized validation; none is relabeled by this phase.
The retained Platform analyzer blocker is not rerun here. PD10/A6b governance, owner enrollment outside
the disposable fixture, and any future generic SDK shutdown/policy correction retain their separate
owner/scope limits. No causal correction is identified for a new repair proposal. Story/sprint remain
**in-progress at step 03**; no publication, deployment, pin update or Git mutation was performed.
Selected source/outputs are released with no active build/test/topology.


### Final consumer and package qualification continuation — 2026-11-14

The parent completed all six current consumer assemblies (5673 passes, two existing optional
skips and eleven explicit issuer cases initially unrun), current core SDK Client/DomainService/
ServiceDefaults/Server assemblies, focused package/Release conformance, and the actual issuer
component wrapper. Eleven explicit issuer/source-proof and five consumer cases passed through
Platform's existing verifier. The SDK Server run's sole failure was the selected unsigned
Tenants 7.0.0 peer archive. The official NuGet archive matches the existing pinned SHA-256 exactly;
materializing that same version through the existing supported fixture target from a private
cache makes all three authenticity/ABI/materialization controls pass. No expected hash, pin,
SDK source or shared package cache was changed. Preserve the original failed result separately.

One Release client-generation scaffold still targeted net10.0 while referencing the net11.0
client. In `tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.InputValidation.cs`, the
parent derives the temporary probe framework from its actual test host. Existing generated-helper
null/presence assertions are unchanged. That input is added explicitly to the mutable story-owned
inventory; its 1616-path pending manifest keeps A6b/exposure/closure flags unchanged. All 500
original package/Release conformance selections, 75 Debug generation cases and 21 pending-inventory
cases now pass. The narrow fallback also builds the existing Release parity tool required by those
selectors. The full CI solution build hit ENOSPC; only ignored Release/net11.0 output directories
created by this phase were reclaimed, then final Debug/source builds passed. Retain both that
broad-gate blocker and the passing narrow evidence. No broader performance redesign is authorized.

The private parent receipts are under directory name
`story12-2-final-validation-20261114-z8scqn4k`. All commands/results and intermediate failures
are retained there. The original pass packet remains immutable. The final byte comparison finds
one changed producer module pair: the native `Hexalith.Tenants.dll`/PDB under its source Debug
output. Its repository/head and ordinary generated source hashes are unchanged, but the prior
strict pass cannot silently qualify this new binary identity. The other 102 producer module pairs
match after SHA-256 hex normalization. Direct source builds of Memories and native Tenants have
completed successfully with the same source flags and pinned qualification suffix.

Final implementation handoff: reuse the available private source/PDB audit and run monitor,
without rendering the workflow. Audit the complete 103-module final producer, actual SDK variants,
project-only Hexalith assets (include native Tenants), all three protected bodies and frozen497c.
Then execute one unchanged strict class with the existing run-owned authority wrapper and supported
observer opt-out. No live dump, process suspension, deadline/order/retry or test-body alteration is
needed for this qualification. Preserve every failure and source scope; verify actual loaded
identities, drift and exact owned cleanup, retain SM4 observations, and release all outputs.
The parent performs no competing build/test/topology while this handoff owns them. Report a failure
as a failure; one success establishes no causal repair of the intermittent stop problem. Add a new
metadata-only qualification file and an append-only spec note, keeping raw receipts private.
Story/sprint remain in-progress while the full CI disk blocker, SM4, governance and whole-story
review dispositions remain unresolved. Do not request new approval for already authorized validation.


### Final-producer strict qualification result — 2026-11-14

The selected root and dependency heads remain unchanged. The final producer audit passes for all
103 previously selected files plus three additional UI dependency copies (106 module files), including
native Tenants, ordinary/generated source, PE/PDB identity, one SDK variant per assembly, and source-only
Hexalith assets. All three protected bodies and frozen497c match. The changed native
`Hexalith.Tenants.dll`/PDB pair is observed in the actual runtime and matches this final producer;
the other 102 prior producer pairs remain identical after hash-case normalization. The earlier
423.055-second passing packet is preserved and is not reused to qualify the changed pair.

The single unchanged strict class under the existing run-owned authority wrapper and supported
observer opt-out fails **0 passed / 1 failed**, zero errors/skips, in **411.136 assembly seconds**
(**335.046 test-body seconds**). Genuine owner bootstrap, provider-binding checks and all six-concern
integrity/complete-source rebuild assertions pass before the first EventStore stop fails at
`RequireAllConcernRebuildAndRecoveryAsync:567`. Its application logs shutdown at
02:31:02.806 UTC; DCP reports `timed out waiting for process to stop` at 02:31:14.805 UTC.
Interrupted-generation recovery, concurrent Workers and later main/fault assertions are not reached.
No dump, process suspension, source correction, deadline/order/retry or test-body change occurs.

All 288 scoped actual observations, including 112 SDK observations and the changed native Tenants
identity, match the final producer. Post-live source/PDB/assets/guard audits pass. Source, heads,
working-tree status and runtime files do not drift during execution; the parent's framework/inventory
changes and all seven existing Platform changes remain intact. Every observed owned descendant,
container and temporary directory is absent afterward, and Aspire reports no running AppHosts.
Filesystem headroom temporarily drops to 184 MiB then recovers without a deletion by this handoff;
this attempt records no `No space left on device` line. No storage or shutdown cause is inferred.

[The new metadata-only failed qualification](evidence/story-12-2-final-producer-qualification-20261114.json)
binds the final producer, exact failure, partial assertions, unchanged guards, private receipts and
owned cleanup. All earlier evidence remains preserved. Complete SM4 measurement is not reached;
prior over-budget findings retain their exact producer scope. The full-CI ENOSPC blocker, governance,
external owner-enrollment limits and independent whole-story review remain separate. Current final-
producer strict acceptance is failed, not complete. No causal SDK repair is established or applied.
Selected outputs are released without an active build/test/topology; no further live run is launched
by this handoff. Story/sprint remain **in-progress at step 03**.


### Bounded RAM-backed stop diagnosis — 2026-11-14

The completed parent verification is now recorded separately in
[evidence/story-12-2-current-consumer-sdk-package-gates-20261114.json](evidence/story-12-2-current-consumer-sdk-package-gates-20261114.json).
It retains six consumer suites, four core SDK suites and the resolved same-version public-peer input
fault, the real issuer controls, the helper framework correction and the final 500 passing focused
Release conformance cases. Full CI solution build remains disk-blocked; narrow evidence does not
relabel that gate. The 106-module final producer's failed strict run remains authoritative for its
exact scope; earlier successful runs are not promoted to it.

Further bounded diagnostics are already within the current continuation. The root filesystem is
small, while `/dev/shm` has roughly 41 GiB available and system available memory roughly 68 GiB.
Use a new private 0700 RAM directory for at most one 512 MiB target heap, with a 1 GiB free-space
floor on that RAM filesystem and strict verification of target PID, start identity, ancestry and
mapped EventStore host before collection. Reuse the eleven-control-qualified capture selector
and the privately installed compatible diagnostic client/analyzer. Keep heap, raw stack/async
analysis and credentials private with 0600 files; no raw object values enter repository evidence.
Do not alter a protected body, host/command deadline, retry, shutdown order, service implementation,
policy or selected producer. No source or package/pin update is authorized by this diagnostic.

Run one unchanged strict diagnostic class under the existing authority wrapper and observer opt-out,
with the validated eight-second stop trigger. A capture may suspend the process, so this run is
explicitly timing-perturbing diagnostic evidence, never a production acceptance pass. Preserve its
outcome, exact source/runtime identities, drift and owned cleanup separately. If a heap is captured,
inspect positive managed-thread and async-wait frames to locate the actual stop await; the current
analyzer's .NET 11 completeness warning remains a limit, and missing frames prove nothing. Do not
repeat the disproved unbounded-telemetry hypothesis or infer causality from a generic await alone.

After evidence is obtained, make the smallest isolated private diagnostic needed to confirm a
specific causal wait. If the demonstrated correction is within existing fixture scope, implement
and verify it under the unchanged acceptance contract. If it crosses the explicit generic SDK
shutdown/performance/policy exclusion, prepare a concrete owner/file/regression proposal and an
unapplied reviewable patch, preserve exact lease/remote-call ownership and every security/source
proof, and request the human scope decision before application. If no cause is established, report
the precise evidence and limit instead of inventing a repair. Parent performs no competing build,
test or topology while the handoff owns selected outputs; release every owned process/container
and diagnostic target when finished. Preserve all earlier packets and keep story/sprint in-progress.


### RAM-backed stop diagnostic result — 2026-11-14

The unchanged final 106-file producer passes its pre/post source, PE/PDB, project-assets, SDK-variant
and protected-body/frozen497c audits. A new private 0700 RAM directory holds receipts and the attempted
heap; a short private RAM TMPDIR uses the existing disposable temp-file seam. No production/test/SDK
source, selected producer, package pin, deadline, retry, service, policy or shutdown order changes.

The one unchanged strict diagnostic class fails **0 passed / 1 failed**, zero errors/skips, in
**298.549 assembly seconds** (**228.449 test-body seconds**) at the first EventStore stop in
`RequireAllConcernRebuildAndRecoveryAsync:567`. Genuine owner bootstrap, provider binding and the
six-concern integrity/rebuild checks pass before that failure. The exact fixture resource/PID,
process-start identity, owned ancestry and mapped EventStore host qualify the eight-second trigger.
The selector functions are identical to the previously eleven-control-qualified versions.

The target reports **407332 KiB RSS**. Its heap writer crosses the **536870912-byte (512 MiB)** polling
threshold in **0.500 seconds**; the observed partial is **550985728 bytes**, an overshoot of
**14114816 bytes**. The guard is a polling threshold rather than a kernel hard file-size limit.
Only the verified owned dump worker is terminated, and the partial heap is removed. Roughly 42.8 GiB
remains free on RAM. No usable heap remains, so no managed-thread/async wait analysis or causal stop
finding is established. Target RSS did not bound dump-file size. The run is explicitly timing-
perturbing diagnostic evidence and supplies no production acceptance pass or new complete SM4 result.

All 285 scoped actual observations, including 110 SDK observations, match the final producer. Source,
heads, working-tree status and runtime files do not drift; all observed owned process/container/
fixture-directory remainders are zero. The phase-owned RAM temp directory and partial heap are absent.
An unrelated Projects AppHost was observed initially and absent at the final read; this handoff issued
no command against it and attributes no external lifecycle cause. The parent separately reclaimed only
its phase-created ignored Release outputs, with an independent zero-mismatch check of the selected
Debug producer. That overlap is retained privately and changes no source/Debug file or service behavior.

[The metadata-only RAM diagnostic](evidence/story-12-2-ram-stop-diagnostic-20261114.json) records the
failed run, exact cap behavior, analysis limit, identity/guard checks, parent cleanup overlap and owned
cleanup. All earlier packets remain unchanged. No speculative product patch or repair proposal is
created because no specific causal await was obtained. The parent will record any next bounded
storage-estimate correction before a further run; none is launched automatically here. Generic SDK
shutdown/performance/policy repair retains its explicit scope limit. Current strict acceptance, full-
CI disk, retained SM4, governance and whole-story review remain open. Source/outputs are released with
no active owned build/test/topology; story/sprint remain **in-progress at step 03**.


### RAM capture sizing correction — 2026-11-14

The first RAM-backed diagnostic reached the actual stop wait but retained no usable heap: the
407332 KiB resident target's heap dump exceeded the parent's 512 MiB estimate. The poll observed
550985728 bytes before abort; the partial was removed. Its failed result, overshoot, zero causal
finding and all cleanup/identity evidence remain sealed in the prior diagnostic packet. This is
an instrumentation-size failure, not evidence for an SDK repair.

Continue the already-authorized bounded diagnosis with a corrected private RAM abort threshold
of **2 GiB**, a **4 GiB free-space floor** on that RAM filesystem, and at least **8 GiB available
system memory** at preflight. This changes only the parent's self-imposed diagnostic storage
estimate; it changes no service/command/host timeout, shutdown order, retry, source proof, producer
or test-body contract. State that the file-size guard is polled and retain any measured overshoot.
Use at most one retained heap and the same validated selector/eight-second trigger, current private
analyzer, short private RAM TMPDIR, protected modes and expanded owned-temp discovery. Reuse the
unchanged 106-file producer after a fresh audit and record the unrelated Projects AppHost as outside
this run's ownership. Do not touch unrelated applications, source, shared caches or prior receipts.

Run one new unchanged strict diagnostic class. If the larger capture succeeds, analyze positive
thread/async wait evidence and perform the smallest private causal confirmation, retaining the
.NET 11 completeness limit. The existing handoff's requirement for a demonstrated correction,
concrete owner/file/regression proposal and unapplied patch before any generic SDK shutdown/policy
repair remains unchanged. Do not infer acceptance from this timing-perturbing diagnostic. Preserve
all earlier failures and keep the parent free of competing builds/tests/topologies until release.


### Corrected RAM sizing diagnostic result — 2026-11-14

A fresh audit again passes the unchanged 106-file producer, source/PE/PDB/project assets, SDK variants,
all three protected bodies and frozen497c. Preflight observes 42976374784 RAM-filesystem bytes free
and 74974064640 system-memory bytes available. The new private RAM monitor uses a **2 GiB polled
abort threshold**, **4 GiB RAM free-space floor**, **8 GiB system-memory minimum** and the unchanged
validated selector/eight-second stop trigger. No service timeout, retry, order, policy, proof,
source, selected output or test-body change occurs.

The one unchanged strict diagnostic class passes **1/1**, zero failures/errors/skips, in
**416.448 assembly seconds** (**341.201 test-body seconds**), completing the original assertions.
All three observed EventStore stops cease to be eligible before the eight-second collection
trigger. Thus **no capture is attempted, no heap is created and no process suspension occurs**.
The larger size estimate is not exercised. Under this explicit diagnostic/RAM-temp handoff the
execution is not promoted to production acceptance. The precise remaining diagnostic limit is that
the long stop did not reproduce in this one bounded run; no positive causal wait evidence exists.
No speculative isolated causal probe or unapplied product-repair patch is justified or created.

All 626 scoped actual observations, including 260 SDK observations, match the final producer.
Source, heads, working-tree status and runtime files do not drift. Owned process/container/fixture
directory remainders are zero; the phase-owned RAM temp directory is removed, no heap/dump worker
remains, and no AppHost is running at the final read. Unrelated applications and earlier receipts
are untouched.

Diagnostic p50/p95 milliseconds are command acknowledgment **6732.786/8135.412** (10 samples,
1000 ms p95 budget), Workers delivery **107.841/314.903** (27 samples, unchanged 1000 ms diagnostic
budget), and status read **876.545/1109.949** (12 samples, 500 ms p95 budget). Command and status
remain over budget; sampling, budgets and the existing record-without-redesign instruction remain.

[The metadata-only sizing recheck](evidence/story-12-2-ram-stop-sizing-recheck-20261114.json) preserves
the exact scope, preflight, unexercised capture guard, passing diagnostic, identities, timing and
cleanup. The final-producer failed qualification, aborted 512 MiB attempt and every earlier packet
remain unchanged. No shutdown cause or correction is established. Generic SDK shutdown/performance/
policy repair retains its existing proposal/decision boundary. Current production acceptance, the
full-CI disk blocker, SM4, governance and whole-story review remain open. Source/outputs are released
without an active owned build/test/topology; no further run is launched by this handoff. Story/sprint
remain **in-progress at step 03**.


### SDK shutdown observability proposal preparation — 2026-11-14

The 2 GiB diagnostic passed without any long-stop trigger or suspension and produced no heap.
It establishes neither the causal stop await nor a repair. Keep the ordinary final-producer failed
strict result and both diagnostic outcomes separate. Further generic SDK source work remains outside
the current approved scope.

Prepare a concrete scope proposal at
`proposal-12-2-sdk-shutdown-stage-tracing.md` with an **unapplied** companion `.patch`. This is
proposal preparation, not permission to modify EventStore source or apply a repair. Explain the
actual evidence and limits, the proposed owner/files, minimal opt-in payload-free stage tracing
around existing SDK stop/drain awaits, the default-off behavior, privacy/failure/cancellation
controls and current unchanged-strict verification plan. Prefer the existing worker, telemetry and
logging seams; introduce no service proxy, dummy sink, forced Activity, new endpoint, timeout,
restart policy, business identifier or body logging. Tracing must preserve source authorization,
actual lease/remote-call ownership, durable outcomes and existing stop results. Validate patch
applicability read-only against the exact current SDK revision; do not alter shared caches, pins,
source or generated SDK runtime outputs. Use a whitespace-clean patch format without unrelated
repository configuration changes.

The proposal should ask for the narrowly bounded SDK shutdown investigation and evidence-confirmed
repair scope needed to unblock this story, with this initial diagnostic patch reviewable now.
Identify explicitly which existing exclusion would be superseded; any proposed change in internal
SDK stop coordination must be justified by causal evidence, while existing host-command budgets,
resource restart order, retries, authorizations and source/lease guarantees remain fixed. Do not
invent or claim a product repair where none is established. No further live run is needed to prepare
this scope decision. Leave the frozen intent unchanged until the human answers, and release files
for parent verification before the final question. Story and sprint remain in-progress.


### SDK shutdown scope proposal prepared — 2026-11-14

[The concrete scope proposal](proposal-12-2-sdk-shutdown-stage-tracing.md),
[unapplied SDK tracing patch](proposal-12-2-sdk-shutdown-stage-tracing.patch) and separate
[unapplied Root fixture forwarding patch](proposal-12-2-sdk-shutdown-stage-tracing.fixture.patch)
are released for parent verification and the human scope decision. No patch is applied to either
actual repository. The proposal identifies the exact generic SDK shutdown/performance/policy
exclusion and asks only for bounded shutdown investigation and a causally demonstrated internal
correction, with existing host-command budgets, restart order, retries, authorization, source proof,
exact lease/remote-call ownership and durable outcomes fixed. No product repair is claimed.

The default-off SDK draft observes the worker host/execution/actual remote-call drains, sequential
telemetry provider calls and registered admin-index stop/cancellation/refresh waits. Closed stage
names, timing, process-local pairing and observed-only token flags flow through existing logging.
The internal Server bridge uses existing friendships and exposes no new public surface. The Root
draft forwards a separate test-process opt-in only to its owned EventStore resource before catalog
composition, preserving existing observer behavior. Full logs stay private and the proposal states
the limits of synchronous/enriched/unavailable logging and of interpreting a missing event.

Both exact-input read-only `git apply --check --unidiff-zero --whitespace=error-all` checks pass.
A private RAM source/output overlay, with the actual workspace/shared caches read-only and network
disabled, builds ServiceDefaults, Server, EventStore and Server.Tests with normal analyzers: four
exit-0 results. The final patch's new controls pass **11/11**, zero failures/errors/skips, in
**0.121 assembly seconds** (**0.666 wrapper seconds**). An earlier isolated CLI attempt stalled
before compiler output and was stopped; its signal-associated exit 0 is not counted as a build
success. The Root forwarding patch is applicability-checked only, not compiled or exercised.
No live topology, heap capture or strict run occurs in this preparation phase.

The actual SDK stays clean at `51dd68b86a690ffe9e11c565dccec49b2d401a94`. All 4987 ordinary SDK
C# files and 7142 actual SDK DLL/PDB files match their pre-preparation hashes; all 106 selected
final-producer DLLs match the retained audit. Frozen497c and all three protected bodies match.
The phase-owned 2593767553-byte RAM overlay is removed, retaining only private proposed output
pairs, exact sources/patches and receipts. [The metadata-only preparation receipt](evidence/story-12-2-sdk-shutdown-tracing-proposal-20261114.json)
binds the proposal, patch hashes, checks, unsuccessful CLI attempt, unchanged-input audits and
cleanup. Earlier qualification/diagnostic packets and Platform changes remain untouched.

The frozen intent and SDK scope exclusion remain unchanged pending the human answer. No further
run is launched. Current strict acceptance, full-CI disk qualification, SM4 over-budget findings,
governance and whole-story review remain open. Source/outputs are released with no active owned
build/test/topology; story and sprint remain **in-progress at step 03**.


### Approved SDK shutdown implementation handoff — 2026-11-14

Jerome's latest “approve” authorizes the concrete proposal and both prepared patches. The bounded
exception is recorded in the frozen intent; its before/after SHA-256 and exact patch identities are
in [the approval receipt](evidence/story-12-2-sdk-shutdown-scope-approval-20261114.json). Keep the
original baseline and every historical packet unchanged; use the new approved frozen hash for
subsequent guards. Earlier no-SDK/pending-approval notes are superseded only within this scope.
Do not render or dispatch the parent workflow again.

- [x] Apply the exact SDK patch in the owning root-declared EventStore repository and the fixture
  patch in Folders, after current-head/input/whitespace checks. Preserve every earlier Root change
  and all seven Platform changes. The selected root remains b915d3ec5180f45c4c477cbeffb9b98df91082d0,
  SDK 51dd68b86a690ffe9e11c565dccec49b2d401a94 and Platform 2a1a343c35d58e6368cd4c34288dc3f9c567f8b9.
- [x] Run meaningful production-seam, privacy, cancellation and lifetime regressions for affected
  SDK worker/catalog/telemetry/admin stop paths; run the new eleven controls with normal analyzers.
  Check absent/invalid/true fixture forwarding, exact owned resource and unchanged observer/security
  annotations through the existing configuration test seams. Refresh only affected mutable compile
  inventory inputs and execute its controls; keep A6b/exposure/closure flags and old inventories fixed.
- [x] Build and audit the final coherent Debug/source runtime and Memories using both source flags,
  HexalithEventStoreVersion=5.0.1 and VersionSuffix=story12-2-qualification, with root-declared
  dependency paths. Disk is constrained; use private RAM/temp/output layouts where supported,
  preserve actual selected outputs and avoid broad duplicate Release output generation. Never
  initialize nested submodules, suppress analyzers or weaken a gate to hide an environment failure.
- [x] Diagnose one unchanged strict class with the exact test-specific SDK tracing opt-in, existing
  supported observer opt-out and private receipts. The eleven-control-qualified monitor and
  compatible analyzer/capped RAM capture helpers may be reused as useful; retain all source/loaded
  identity, PID/start, drift, performance and exact owned-cleanup evidence. A tracing pass or a
  missing marker alone establishes no cause. Full logs and enriched scopes stay private.
- [x] Once positive evidence identifies the wait, make the smallest deterministic held-await
  reproduction at that actual seam and identify the causal owner/files/ownership chain. Implement
  only its bounded correction and require the regression to fail before and pass after. The
  approval already covers this evidence-confirmed repair; no further permission is needed while
  every fixed guarantee remains intact. No guessed repair, forced lease release, discarded work,
  success inference, added deadline/retry or changed resource restart order is permitted.
- [x] Rebuild/audit affected producer/runtime identities and relevant consumer/capacity gates, then
  run unchanged strict acceptance with SDK tracing off. Retain failed attempts and flakiness; one
  pass does not prove a repair. Keep full-CI disk, SM4 redesign, governance and independent review
  dispositions separate. Record new metadata-only evidence/spec notes and release every owned
  process/container/temp fixture plus selected outputs for parent verification.

The parent performs no competing source/output mutation, build, test or topology during this
implementation handoff. Existing private receipt directories and seals remain intact. Root disk and
RAM headroom must be checked before allocating new artifacts. Do not touch unrelated AppHosts or
shared caches. Source/lease/security guards and all three protected strict bodies remain unchanged.
Story/sprint remain in-progress until actual acceptance is satisfied; report a precise blocker
without inventing completion if an independent requirement cannot be fulfilled within this scope.


### Approved SDK shutdown implementation and current qualification — 2026-11-14

The six implementation-handoff items above are complete within Jerome's approved bounded scope.
[Current metadata-only evidence](evidence/story-12-2-sdk-shutdown-implementation-20261114.json)
and [the current coverage matrix](evidence/story-12-2-sdk-shutdown-implementation-matrix-20261114.json)
retain the exact commands, source/producer identities, failed/canceled attempts, ownership controls,
measurements and cleanup. Earlier packets remain immutable. The approved frozen SHA remains
`ba0f887340b68da1029f3c412d6d38b9a8639c20839d0cc65bee049f147f183f`; the selected Root,
EventStore and Platform heads and all three protected strict bodies are unchanged.

Both exact approved patches were applied in their owning repositories, followed by the redundant
Dispose-summary cleanup and focused controls. Default-off SDK tracing observes the existing worker,
telemetry and admin-index awaits through the existing logging/friendship seams. The Root fixture
forwards its separate opt-in only to the owned EventStore resource before catalog composition;
existing observer/security annotations remain intact. Final scope/privacy/ownership controls pass
13/13 and fixture configuration controls 10/10. The mutable 1616-path compile inventory's 21 controls
pass; A6b/exposure/closure flags and historical inventories remain unchanged.

Two separate positive ownership chains justified two bounded corrections:

- The initial traced failure (0/1, 423.993 seconds) connected Host.StopAsync to the retry worker's
  actual coordinator admission-denied `CompleteDeliveryWriteAsync`. The worker/coordinator tokens
  were canceled while the actual actor invocation received CancellationToken.None. The coordinator
  now forwards the existing host ApplicationStopping token to its lifecycle-completion calls.
  Identical held-actual-transport regression bytes pass the ordinary-caller control and fail only
  host cancellation before the correction (1/2); both pass afterward (2/2). A still-held call and
  unknown completion retain the real catalog fence and original retry work.
- That correction alone did not qualify shutdown: tracing-off strict failed 0/1 in 322.011 seconds.
  A later complete diagnostic heap connected Host.StopAsync to **ProjectionPollerService's own
  ExecuteTask**, then compatible replay state 42 and the actual `CompleteDeliveryWriteAsync`.
  The poller/orchestrator stopping tokens were canceled, but its original 30-second finalization
  token remained live. A coordinator `BeginDeliveryWriteAsync` in a separate async stack is not
  attributed to that stop. The legacy finally now links the existing TimeProvider-backed deadline
  with host ApplicationStopping for lifecycle completion and the same conditional intent cleanup.
  Ordinary caller independence, the 30-second deadline, actual task awaits, unknown fences and
  original persisted work remain intact. Identical held-call controls pass caller/deadline and
  fail only host cancellation before (2/3); all pass afterward (3/3, 0.721 seconds).

The failed tool-before-attachment diagnostic (0/1, 327.790 seconds), normally canceled child-env
attempt (exit 58, 231.847 seconds; no target suspension), and successful-capture failing diagnostic
(0/1, 372.554 seconds) remain separate. The corrected capture job installed/probed the compatible
analyzer within its own cleaned temporary lifetime, validated the actual child environment and five
refusal cases, and retained its capped RAM heap. The .NET 11 analyzer's completeness limit is explicit;
positive method/token/task fields corroborate the connected chains. Missing trace boundaries alone
establish no cause. No stop-command success is inferred from a later PID exit.

All five final Debug/source builds pass with normal analyzers and the prescribed source/version
flags, including Root and Memories. Current affected SDK legacy/poller/coordinator/worker/ownership/
privacy/capacity checks pass **571/571**; catalog cancellation **5/5**, admin index **22/22** and
fixture configuration **10/10** also pass. Six current consumer assemblies pass **5673**, with zero
failures/errors, the same two operator skips and eleven issuer cases explicitly NotRun in the default
Server suite. The refreshed actual-issuer wrapper separately passes those **11/11** and **5/5**
consumer cases. Rebuild initially recopied the existing nonauthentic Tenants peer fixture (2/3);
using the unchanged supported copy target with the retained official same-version read-only cache
restores **3/3** authenticity/ABI controls. No expected hash, pin or shared cache was changed.

The complete Release/package CI build and original conformance selections now pass **500/500**,
resolving the earlier ENOSPC qualification blocker. Its unchanged 2190 owning Root source inputs,
package-mode graph and pinned 5.0.1 payloads bind reuse after the second SDK-only correction. That
lane does not claim execution of unpublished SDK source; its original receipt lacked a PE hash
snapshot. The current Debug/source producer supplies the separate SDK qualification.

Final unchanged strict acceptance with SDK tracing **off** passes **1/1**, exit 0, zero failures/
errors/skips, in **443.780 assembly seconds**. Native Tenants owner bootstrap, exact process
replacement, interrupted SDK generation/resume, real supported provider task pipeline, complete
source rebuild, two-process delivery, retention and safe failures are positively observed. No
capture/suspension occurred and no SDK trace messages were emitted. All **568** scoped actual-loaded
observations, including **240** SDK observations, match the audited **106-module** producer.
Source/head/status/runtime drift and post-run DLL/PDB mismatches are zero. One current pass does not
erase prior flakiness or establish absence of every shutdown failure mode.

SM4 remains open. Current p50/p95 milliseconds are command acknowledgment **7139.309/8642.996**
(10 samples, p95 budget 1000), Workers delivery **126.498/356.683** (27 samples, diagnostic budget
1000), and status read **987.164/1291.141** (12 samples, p95 budget 500). The acknowledgment stopwatch
still surrounds the complete HTTP retry wrapper; the explicit one-second pre-call delay/bootstrap
is outside it. Existing successful-stage metadata and unlabelled samples cannot assign an exact
command/retry-delay/subpath split. The frozen instruction remains **record findings; do not redesign**.

Remaining work is partitioned explicitly: the parent may now independently audit this diff, matrix,
identities and acceptance in the already-rendered step 04. Current broad findings are the two SM4
over-budget measurements; the historical unrelated Platform 57-error analyzer gate was not rerun
and is not relabeled a current failure. PD10/A6b governance, public v2 exposure/closure, owner
enrollment outside this disposable fixture, publication/deployment, pins and general performance/
policy redesign retain their human/external scope limits. Whole-story acceptance is not inferred.

Every owned descendant, container and temporary fixture directory is gone; run-with-temp jobs wait
for children and remove their scratch. Two unrelated external fixture containers observed during
final strict remain untouched. After retaining useful exact source/output pairs and private hash
inventories, only this phase's disposable SDK overlay and old scratch were removed (2699565068
bytes). Full logs, useful heaps and producer receipts remain private under `h12impl-kmdf_6ag`, with
an integrity seal; earlier private packets are preserved. No commit, push, submodule update, pin
change, publication or deployment occurred. Sources and selected outputs are released with no active
owned build/test/topology. Story/sprint remain **in-progress** pending independent acceptance.


### Parent review six handoff — 2026-11-14

Parent verification passes all 2252 sealed private entries, 106 selected DLL/PDB pairs, 17 changed
source hashes, raw strict XML and all five frozen matrix rows. All review child jobs and parent
probe jobs have exited; their scratch is removed. The two bounded SDK shutdown corrections and
unchanged tracing-off strict pass (1/1, 443.780 seconds) remain exact-source evidence. No production
code or selected output was changed during review.

Independent review nonetheless reproduces stale coherent task rollback and forged terminal
metadata re-attestation, plus legitimate provider/organization partition and unknown-command
Worker refusals. These current defects prevent whole-story acceptance despite the passing existing
suites. [The review disposition](review-12-2-round6-disposition.md) and
[all individual verdicts](evidence/story-12-2-review6-triage-20261114.json) are authoritative for the
next correction pass. A supplementary indexed source/test view is retained alongside the complete
canonical diff, preserving the original baseline.

The review counter is now 6, beyond the rendered limit of 5. Workflow work stops before any physical
rollback or re-derivation, pending the human decision on one additional correction/review cycle.
No override has been inferred from the earlier bounded SDK shutdown approval. Frozen intent, every
protected test body, all prior evidence, source/lease guarantees and disabled public v2 remain fixed.
Story stays in-review and sprint stays in-progress; no completion, release or Git mutation is claimed.


### Approved review-six correction handoff — 2026-11-14

The user's latest **yes** approves the concrete additional correction/review cycle requested in
[the review disposition](review-12-2-round6-disposition.md). The decision is recorded in
[the continuation receipt](evidence/story-12-2-review6-continuation-approval-20261114.json).
It supersedes the last workflow-halt note only for iteration 6 and its following review. Keep
review_loop_iteration=6 and the original c22b baseline; do not reset either, rerender the workflow,
physically roll back approved code or treat this as release/Git/public-v2/performance authority.

The six new Review-six tasks in Tasks & Acceptance are current implementation instructions.
Earlier checked tasks remain historical evidence for their exact source; their core invariants
must survive these corrections. Follow all existing Spec Change Log constraints and re-derive
only the demonstrated input/authority defects in place. Preserve both independently justified SDK
shutdown fixes, default-off diagnostics, source proofs, actual remote-call/catalog ownership and
all successful round-five compaction/retention/closure/native-validation/observer corrections.
The three protected bodies retain d29afa3c / f5e8767a / 2ae31131 identities. Public v2 stays disabled.

The parent probes and indexed review artifacts are useful retained results under
`../../.artifacts/story-12-2-review6-20261114/`: claims-probe.Program.cs/log/receipt reproduce both
corruption defects, source-scope-probe-v3.Program.cs/log/receipt reproduce the provider/organization/
unknown-command refusals, and probe-input-hashes.json binds their unchanged helpers and binaries.
The full canonical diff, compact runtime/test view and immutable index are retained together;
review6-triage records all fourteen verdicts. Do not patch/refile the carried provider-history
claim or infer a new CI automation/performance requirement from rejected/unverified findings.

New ad hoc verification jobs that create disposable workspaces must use run-with-temp. Place
copies, feeds and disposable build outputs below its TMPDIR; retain useful logs/results outside,
wait for children before wrapper exit and remove every owned topology/scratch. Commands with
existing reliable scratch cleanup may retain it. Selected reviewable runtime artifacts are useful
results, not disposable workspace; preserve/audit them explicitly. Never rely on an old named /tmp
helper surviving. Reuse the latest proven production AppHost baseline only after source/output/head
preflight; do not start or stop unrelated applications or resources. Keep raw logs/private data out
of tracked evidence. Root disk currently has ample headroom, so do not delete unrelated outputs.

If the existing SDK cannot establish bounded current-view authority within these fixed guarantees,
complete the independent corrections and report the exact missing public capability and concrete
owner prerequisite; do not invent a generic local protocol or weaken the expected behavior.

The implementation agent owns sources and selected outputs serially until released. Parent does
no competing edits/build/test/topology, then independently verifies the actual diff, matrix and
runtime evidence before the next required review. Story/sprint are in-progress. No further human
confirmation is needed for these approved corrections within existing intent.


### Review-six implementation and current qualification — 2026-11-14

The six approved correction tasks are implemented and qualified on the final current source.
[The metadata-only implementation packet](evidence/story-12-2-review6-implementation-20261114.json)
and [current five-row matrix](evidence/story-12-2-review6-implementation-matrix-20261114.json)
retain the exact commands, source/producer bindings, failures, component results and live limits.
The review iteration remains 6 and the original c22b baseline is unchanged. Parent independent
review remains required; story and sprint remain in-progress.

Schema 4 keeps only one active workspace digest and one active task digest in the bounded
checkpoint. Per-key applicability uses ordinary SDK batches/generations and the identical original
source-time +400-day deadline as its status/evidence. It does not extend C3 retention or replace
the existing independently retained original task-completion/closure authority. Reads validate that
authority, active source evidence and an unchanged selected generation/checkpoint across reads.
Paired absence and positively expired views retain their frozen outcomes without interpreting
irrelevant malformed applicability. Missing/corrupt surviving authority fails closed; an authorized
complete-source named rebuild repairs schema 2/3 and damaged derived records without changing source
bytes, original deadlines or valid source-conflict guards. Terminal preservation requires validated
original source completion. Current/historical coherent rollback, forged proof, pure rejection,
inherited generation and interleaved reads are covered.

Validated owner scope now permits lawful normalized unknown-command rejections through actual
authenticated Workers intake and legitimate provider/organization accepted/rejected prefixes through
live and bounded named/shared processing. Foreign/wrong-kind/malformed/version/position/source-binding
controls still refuse without fabricated Folder/provider results or durable acknowledgment.

The successful final-dispatch cleanup regression independently fails when its exact host-stop token
is restored to None, and passes after exact byte restoration. A later bounded diagnostic retained
one complete 751181824-byte private heap. Its positive chain connected Host.StopAsync through
GenericWebHostService/Kestrel's one outstanding transport and the exact inbound request context to
ReconcileBoundReplayCompletionAsync -> OwnsPublicationAsync -> OwnsDeliveryLeaseAsync with an
uncancelable actual weak/JSON actor RPC. The correction connects only that reconciliation proof call
to ApplicationStopping. The existing constructor, other publication methods, both prior SDK fixes,
ordinary caller independence, actual-task await, unknown catalog fence and original committed receipt
remain intact. The production-DI/actual-transport actor class changes from 52/53 to 53/53; cancellation
does not abandon the held transport, actor task or catalog ownership before actual completion.

Final normal-analyzer Debug/source builds pass for Root, Memories and the three affected SDK test
projects with both source flags, version 5.0.1 and story12-2-qualification suffix. The six Root suites
pass 5704 cases, with two unchanged optional Domain skips and 11 issuer cases explicitly NotRun in the
default lane. The current issuer wrapper separately passes 11+5 cases. Affected SDK coordination,
capacity, shutdown and actor checks pass 626/626; catalog 5, admin-index 22, fixture 10, authentic published
peer ABI 3 and mutable inventory 21 also pass. The same-version peer fixture-copy failure is retained;
the supported target rematerializes only the hash-verified official archive, without changed expected
hashes, pins or shared cache. Root/package gate coverage remains 500/500 with explicit unchanged Root
inputs and package-mode limits; it does not qualify unpublished SDK source as released packages.

The final 106-module producer audit has no PE/PDB/source/assets issue or SDK variant. All three
protected body hashes and frozen ba0f8873...f183f remain exact. Tracing-off unchanged strict acceptance
passes 1/1, zero failures/errors/skips, 477.194 assembly seconds and 391.808 body seconds. All three
restart/recovery assertions execute, actual loaded identities match (246 scoped SDK observations),
and source/head/runtime drift and owned process/container/scratch remainders are zero. Prior strict,
diagnostic, issuer startup, fixture and compilation failures remain failures in the retained phase;
one earlier overwritten full compiler log is explicitly identified by its preserved failed receipt.
No missing trace or PID exit is used as proof of stop/recovery success.

SM4 remains record-only and open: acknowledgment p95=8438.024ms exceeds 1000ms; status-read
p95=1277.472ms exceeds 500ms; Workers operational delivery p95=220.032ms is within 1000ms. No budget,
retry, restart order, protected assertion, authorization, source proof, public-v2, package/gitlink/pin,
Git or deployment change is made. PD10/A6b/governance/exposure/closure, production owner enrollment,
separately authorized SDK release consumption and whole-story acceptance remain independent gates.


### Approved review-seven correction handoff — 2026-11-14

Jerome's latest "approve" authorizes the concrete additional correction/review cycle requested
after review seven. The new [approval receipt](evidence/story-12-2-review7-continuation-approval-20261114.json)
and seven current Review-seven tasks in Tasks & Acceptance are authoritative. This supersedes
earlier halt/approval-needed notes only for iteration 7 and its following independent review.
Keep review_loop_iteration=7, approved_review_loop_iteration=7 and the original c22b baseline.
The skill has already been rendered; do not rerender it or reset review history. Re-derive the
demonstrated defects in place without physical rollback, preserving every KEEP constraint in
[the disposition](review-12-2-round7-disposition.md). Earlier checked tasks/passes remain exact-source
history; they do not qualify changed runtime. Story/sprint remain in-progress.

The prior parent probes, source inputs, logs and assembly bindings remain useful results in
`.artifacts/story-12-2-review7-20261114/` at the repository root. The sealed phase and earlier
receipt directories remain intact. Inspect existing phase scripts/receipts before reusing them;
never assume an old named /tmp helper survives. Check actual source/output/head identity before
reusing the proven production AppHost baseline. Honor the baseline Aspire preflight using the
matching lifecycle skill when a topology is needed; use only owned exact-target isolated resources.
Ad hoc disposable verification jobs use run-with-temp with copies, feeds and outputs under its
TMPDIR. Useful logs/results stay outside, with raw logs/private data excluded from tracked evidence.
Root disk has 542 GiB available at handoff; preserve unrelated caches, outputs and applications.

The implementation agent owns sources and selected outputs serially until it releases them.
The parent performs no competing source/output mutation, builds, tests or topology during this
handoff. Preserve default-off tracing and all three causal SDK shutdown corrections, actual task/
remote-call/catalog/fence/work ownership, original source/lease/security guarantees, frozen
ba0f8873...f183f and all three protected acceptance bodies. No source-byte replacement, retention
extension, SDK limit relaxation, unbounded local ledger, guessed success, new deadline/retry,
changed restart order, release/Git/pin/deployment/public-v2 operation or general performance
redesign is authorized. Content-owned findings stay with 12.3. If existing public source APIs
cannot establish the required bounded authority, complete all independent work and report a
precise capability/prerequisite blocker, preserving expected behavior and all evidence.

Return exact changed files, red/green commands/results, final input-bound producer/loaded identities,
five-row matrix coverage, failures/SM4 limits and cleanup with new metadata-only evidence. Then
release sources/selected outputs for the parent's actual-diff audit and required independent review.

- Review-seven cleanup follow-up (2026-11-14): the final 72-case actual-model run had **71 drained and one undrained**, correcting the earlier provisional 72-pass claim. A subsequent controlled actual-timer probe captured the deadline in `NotifyingState` while both dependent source tokens remained live: .NET 11 `CancelAsync` does not join an already-started timer notification, and disposal cleared its pending registrations. The bounded correction registers an owned completion signal before source callbacks and awaits it before exhausting/disposal; the two-second budget, original caller priority and safe stale/unavailable outcomes remain unchanged. Previous passing suites/strict receipts remain bound to their previous producer and do not qualify this correction.

Review-seven final notification qualification preflight (2026-11-14): the owned deadline now also joins an already-running timer notification and performs the same cleanup before preserving original-caller cancellation. New notification/caller-first controls are red6/3 and green9/9; the unchanged permission class is9/9, the unchanged72-case actual-model program has72 fully canceled/drained outcomes, and the identical controlled timer program fails against recovered historical red bytes then passes against current source. All four historical Core/ServerTests DLL/PDB hashes were recovered exactly and independently source/PDB audited. The previous71/72 final attempt and earlier strict pass remain superseded evidence, with their original bindings retained.

The refreshed ordinary consumer suites pass5747 cases (Domain2461, Server1434, Workers186, Integration854, Contracts689, EventStore123), zero failures/errors, two unchanged optional Domain skips and11 default issuer NotRun. The actual issuer component separately passes11 Server plus5 AppHost cases; fixture configuration passes10, package/CI passes500, and72 unchanged SDK input files bind the prior626/30/5/22/3 capacity/native/catalog/admin-index/ABI cases. Root/Memories normal-analyzer source builds and106 producer PE/PDB/assets pairs pass. Current source still reproduces both unchecked authority defects; the [concrete owner prerequisite](proposal-12-2-historical-source-authority-prerequisite.md) records exact existing APIs, grants, bounds and safe handoff. No matrix row or whole-story completion follows from these executed positive checks. Final tracing-off strict qualification is pending at this preflight.

Final review-seven notification qualification — 2026-11-14: the unchanged tracing-off strict class passes1/1, zero failures/errors/skips,434.997 seconds. All106 post-live PE/PDB/assets pairs and241 scoped SDK loaded observations match the audited current producer, with no observations outside the selected scope. Source/head/runtime drift is zero, the frozen block and three protected bodies remain exact, and all task-owned descendants/containers/directories are gone. The raw cleanup snapshot also records one newly observed, unattributed SDK container; read-only inspection establishes no owner, and it is left untouched. No global-empty cleanup or external-owner claim is made. All preexisting container IDs remain present.

SM4 remains open: command acknowledgment p95=9237.965ms against1000ms; status read p95=999.025ms against500ms; Workers delivery p95=222.652ms against1000ms. These are unchanged-budget record-only results. The [current implementation packet](evidence/story-12-2-review7-implementation-20261114.json) and [five-row matrix](evidence/story-12-2-review7-implementation-matrix-20261114.json) bind executed passing coverage separately from the still-reproduced historical-applicability and post-C3 MessageId authority defects. Both source-authority tasks remain unchecked, every affected acceptance row remains incomplete, and Story12.2/sprint/whole-story closure remain in progress. Prior failed/flaky attempts, the superseded71/72 claim and previous strict producer are preserved explicitly. Parent independent review, owner capability/enrollment, SM4 and the existing governance/release/exposure gates remain open.

### Historical source-authority prerequisite qualified — 2026-11-15

The separately authorized [source-authority prerequisite](spec-12-2-historical-source-authority.md) is complete. EventStore supplies bounded authenticated native scans; exact Folders Server/Workers grants and independent original-source checks close both remaining review-seven tasks above. Current reviewed qualification rebuilds the source graph, passes 6,489 affected tests, both retained reproductions and all three unchanged strict bodies, and verifies 290 producer copies plus 1,415 loaded observations. All confirmed independent-review and parent patch findings are fixed; no finding is deferred. Exact inputs, failures, scope inventories, source/PDB/assets identities and cleanup are retained in the [reviewed evidence](evidence/story-12-2-source-authority-reviewed-20261115.json).

The protected bodies and this frozen block remain exact. Capacity evidence for the explicitly selected EventStore.Testing original-primary source is identified separately from production actor evidence. SM4 record-only p95 remains above its unchanged budgets; whole Story 12.2 and its performance, release and governance gates remain in-progress. This is source-only work: no publication, deployment, package/gitlink/pin update, commit, push or public-v2 exposure.

### Platform gitlink qualification attempt — 2026-11-22

Folders `d58d5d7a54f63f7223b972f22f11c58510a94119` selects Platform `0aacf13244fa6b1d9e08d627149d72cf12229167`. On these revisions the Folders and Platform live enrollment checks passed, including Platform cancellation. The provider passed 23 Java tests. System Python passed 58 of 59 checks. The UID 1001 portability check requires a supported runner with permission to switch user. [Component results, revisions and assembly hashes](evidence/story-12-2-platform-pin-qualification-20261122.json)

Bounded history, operational durability and folder durability each failed during fixture startup. Codeberg served the Forgejo 16.0.3 image index and returned HTTP 404 for its referenced Linux/AMD64 manifest. All three exited before their durability assertions, so this revision has no SM4 measurements. Platform and Folders fixture maintainers need the digest-matched image for the rerun. Story 12.2 remains in progress and the Platform prerequisite remains pending. Hosted push CI and contract-spine passed on the Folders commit. The push workflow skipped its Aspire and performance jobs. [CI](https://github.com/Hexalith/Hexalith.Folders/actions/runs/45002607113) · [Contract spine](https://github.com/Hexalith/Hexalith.Folders/actions/runs/45002606908)

### Current pinned-source qualification — 2026-11-26

Folders `a2f4dbcfe5c6a98e698e8fc5b1036e2f6d86b88a` selects EventStore `943be126d81fc82761c27d856c3c8d85cbeb2c99` and Platform `3848af49f9afc91b174b4c716751fe49cb15dbba`. The recovered Forgejo 16.0.3 Linux/AMD64 manifest matches the digest recorded in the November 22 failure. The unchanged operational durability, folder durability and bounded-history tests each passed with zero failures or skips. The bounded run exercised authenticated rebuild from 10,001 through 10,002 synthetic original-primary positions, including four changed process IDs, inherited replay and conflicting delivery. All 1,299 observed assembly bindings match the 236 audited producer copies. Source and runtime hashes stayed stable, and all observed owned processes, containers and directories were removed.

Six Debug/source component suites passed 6,158 tests with two existing optional Domain skips. Passing cases cover all five frozen matrix rows and the 10,001- and 32,769-position production-handler rebuilds. The 11 explicit Server issuer cases passed with five live AppHost consumer controls. The provider passed 23 Java and 59 Python cases, including the previously blocked UID 1001 portability control. The exact-cohort Folders enrollment control, eight AppHost configuration controls and 300 focused EventStore scanner and source-authorization cases also passed. [Current qualification evidence](evidence/story-12-2-current-source-qualification-20261126.json)

The fixture lifecycle observer verification is complete on this cohort. All 43 Server observer cases and 21 PD10 conformance controls passed, including the current source inventory checks. The privacy scanner passed all 299 cases. A supplemental `run-with-temp dotnet tests/Hexalith.Folders.AppHost.Tests/bin/Debug/net11.0/Hexalith.Folders.AppHost.Tests.dll -class '*FolderDurabilityObserverConfigurationTests' -noLogo` run on assembly SHA-256 `4870e64ee36a52bfbfcda8e4c7749c0889e19251c3225455b6fa3088570ef105` passed all 10 cases with zero failures or skips.

The local Release build passed with zero warnings and errors. The dry run validated six package and symbol pairs and four package-only consumers. All 47 selected PD10 and authorization-matrix checks passed. [Release qualification evidence](evidence/story-12-2-release-qualification-20261126.json)

SM4 remains open under its approved measurement-only scope. Command acknowledgement p95 was 8,071.427 ms against 1,000 ms, and status read p95 was 1,046.687 ms against 500 ms. Workers delivery p95 was 7,907.817 ms against its 1,000 ms diagnostic budget.

### Platform owner acceptance on 2026-11-26

Jerome explicitly replied “accept” to the Platform owner approval request on 2026-11-26. The decision satisfies `EXT-PLATFORM-RUN-OWNED-WORKLOAD-AUTHORITY` for disposable source qualification on Folders `a2f4dbcfe5c6a98e698e8fc5b1036e2f6d86b88a` and its Platform gitlink `3848af49f9afc91b174b4c716751fe49cb15dbba`. The receipts cover live enrollment and issuer controls, standalone Platform fresh-host adoption and cancellation, all three strict durability passes, runtime assembly bindings and observed cleanup. [Approval request](story-12-2-platform-owner-approval-request-2026-11-26.md) · [Source qualification](evidence/story-12-2-current-source-qualification-20261126.json) · [Platform control receipt](evidence/story-12-2-platform-owner-handoff-20261126.json) · [External dependency tracker](../planning-artifacts/planning-story-manifest.yaml)

Story 12.2 and the sprint remain in progress. SM4, publication, staging evidence, deployed-consumer evidence, PD10/A6b governance and independent whole-story acceptance remain open.

### SM4 remediation continuation — 2026-11-26

Jerome requested a fix for SM4 after reviewing the over-budget results. This authorizes bounded performance remediation on the existing command acknowledgement and status read paths. Use the existing budgets, synchronous command outcomes, layered authorization, durable execution and projection guards, and all three unchanged strict acceptance bodies. Keep the measured November 26 result as the comparison baseline.

The retained passing trace records 7,691 ms for one folder creation, including 2,746 ms across seven execution-fence validations, 2,572 ms routing and 1,143 ms admission. For a typical 653 ms lifecycle read, the two parallel ACL metadata calls took up to 356 ms and the subsequent fresh source observation took 217 ms. A fresh unchanged strict run failed before measurement. The first real repository binding timed out during routing and returned a reconciled 409 where the test expected 202. The source and runtime were stable and the disposable run cleaned up all observed owned resources. Local receipt: `.artifacts/story-12-2-sm4-7bff5X/baseline-receipt.json`.

- [x] In EventStore `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs`, restrict the projection replay fence check to the branch that actually performs synchronous replay. The owned worker deferral branch has already admitted and rearmed the durable activation and performs no inline replay. Keep the pre-effect fence checks and final validation before durable completion. Extend `SubmitCommandHandlerIdempotencyAdmissionTests` to show that protected worker-backed execution removes exactly the unused observation while an unqualified worker path still validates before replay and both paths fence completion.
- [x] In `src/Hexalith.Folders/Queries/Folders/EventStoreOperationalStatusReadModel.cs`, reuse the SDK's ETag-guarded lifecycle, evidence and checkpoint selection. The lifecycle read validates selected state against the checkpoint and the subsequent fresh source head. Historical task and workspace reads retain their position-receipt checks. The focused regression counts the selected lifecycle and receipt reads, and the lifecycle, historical-evidence and mid-read race tests pass.
- [x] Run the affected Debug source tests and the unchanged live operational class on audited outputs. The two regressions reproduced the redundant work before the edits. The candidate passed 73 EventStore tests and 204 Folders tests, and the full Debug build had zero warnings or errors.

The first candidate run passed repository binding and lock release, then failed during restart when authenticated `UpdateTenant` returned HTTP 500 `unavailable`. The retry passed the unchanged operational class. All 623 loaded assembly observations matched the producer audit, and no observed task-owned resources remained. The baseline rerun used Folders `a878668e185f0e5d7492556b7a675da1ed33a157`. The candidate uses `b9ac1212aa2e65fe10ed50379bb4a04b3cebac77`, which landed during this work and includes task provisioning. Both runs retain their exact source and runtime identities. [SM4 remediation evidence](evidence/story-12-2-sm4-remediation-20261126.json)

SM4 remains open. Candidate retry p95 was 36,418.406 ms for command acknowledgement against 1,000 ms, 1,003.751 ms for status reads against 500 ms, and 7,073.676 ms for Workers delivery against the 1,000 ms diagnostic budget. The first repository-binding HTTP request spent 10,969 ms across three admission attempts and 8,166 ms routing. One 1,003 ms lifecycle response included two parallel ACL source observations of 570 and 576 ms, followed by a 322 ms source observation. These timings record the remaining cost under the existing authority and response contracts.

The pending PD10 inventory now includes all 21 provisioning inputs required by its runtime and test coverage checks. The regenerated 1,714-artifact set passed all 47 selected contract and authorization checks. A6b, publication, staging evidence, deployed-consumer evidence and independent whole-story acceptance remain open. [Pending PD10 inventory](evidence/story-12-2-v2-source-requalification-pending-2026-11-12.yaml)

### Same-revision SM4 investigation — 2026-11-26

Both arms use Folders `b9ac1212aa2e65fe10ed50379bb4a04b3cebac77`, EventStore `943be126d81fc82761c27d856c3c8d85cbeb2c99` and Platform `3848af49f9afc91b174b4c716751fe49cb15dbba`, including the two retained local optimizations. Across all 14 repositories, the candidate changes only EventStore's `RouteCatalogAuthority.cs` and `ParallelCatalogObservationTests.cs`. All 1,714 entries still match the pending PD10 inventory.

In the retained slow binding request, admissions took 1,529, 5,173 and 4,267 ms. The first routing call spent 7,827 of 8,166 ms in the fenced actor RPC. The following attempts spent 2,278 and 911 ms in reconciliation. On the ordinary path to a new catalog lease, admission recomputed the binding digest of the frozen original twice after it had already been validated. The candidate keeps that digest within the attempt. It still computes the digest of the observed copy and the fresh committed activation, checks host identity and trust, and uses the fresh ETag for the durable fence. The focused regression reproduced two digest computations between the fresh state read and the write on the baseline and one on the candidate. All 202 EventStore and 204 Folders regression cases passed.

On the passing default candidate, a 13,097 ms repository-binding request included two admissions taking 1,773 and 2,271 ms. Routing took 4,337 ms, including 4,289 ms in the fenced actor RPC. A 1,668 ms lifecycle response included parallel folder and organization ACL observations of 678 and 861 ms, then a 696 ms fresh folder-head observation after checkpoint and view selection. The ACL calls establish authority over different streams. The later observation detects an advance during selection, so reusing the earlier folder head would lose that proof.

The unchanged real-provider operational test reported these nearest-rank p95 values. Each passing run measured 10 accepted commands and 12 successful lifecycle reads. The client timers include retries and backoff.

| Local qualification profile | Baseline command p95 | Candidate command p95 | Baseline read p95 | Candidate read p95 |
| --- | ---: | ---: | ---: | ---: |
| Default | 36,418.406 ms | 14,453.364 ms | 1,003.751 ms | 1,673.355 ms |
| Private Development catalog computation reuse | 6,896.454 ms | 22,062.313 ms | 401.838 ms | 1,604.724 ms |

The default baseline is the retained passing run. Its source maps, revisions and all 237 audited producer identities match the baseline verified for this comparison. Three fresh default baseline runs stopped before measurement. Two returned HTTP 500 on authenticated `CreateTenant` and one returned HTTP 500 on authenticated `UpdateTenant` during restart. Two default candidate attempts stopped when the unknown-provider denial returned 503 and 202 respectively instead of 403. The subsequent default candidate passed, including the denial, source, restart and fencing checks. Its slowest accepted command was a folder creation with two admissions and a 3,689 ms fenced actor call. All 557 loaded assembly observations matched the producer audit.

The separate private comparison passed both arms in the same disposable checkout with `ScopedDecodeReuse` and `RetainValidatedCatalogBytes` explicitly enabled in both. The candidate was slower on both metrics there. These settings were confined to the test processes. The results establish the removed digest work but do not establish a reproducible end-to-end speedup. **SM4 remains open** against the 1,000 ms command and 500 ms lifecycle budgets. [Same-revision source, traces and receipts](evidence/story-12-2-sm4-same-revision-20261126.json)

### Gateway transport resubmission — 2026-11-26

The unknown-provider 202 came from transport, not the domain. Folders Server's `IEventStoreGatewayClient` inherited the SDK standard pipeline (10 s attempt, 30 s total, POST retries). The original submission was still in its non-cancellable fenced actor call when the attempt was cancelled. Its resubmission's admission read the original's `Pending` reservation and EventStore answered `Accepted`. The rejection was persisted after that reply. The same path produced the earlier 503 and 409 outcomes and the second admissions in the slow binding traces. The baseline takes it too: baseline-1 reproduced the 202 live.

EventStore now applies its existing bounded single-attempt policy to the gateway client in `AddEventStoreDomainServiceCore`. One attempt owns the 30 s budget, early transient replies still retry, and other clients keep the inherited pipeline. `GatewayCommandTransportTests` failed on the unchanged SDK and pass with the change. DomainService.Tests (1,434 passed, 1 skipped), 264 focused Server.Tests and 26 Folders host-composition tests pass. No Folders source changed.

On identical revisions (Folders `c00bad4d`), all three baseline and all three candidate default-profile runs of the unchanged operational durability test failed, so neither arm has a p95. Baselines failed on the denial (202, then 503) and on a resubmitted binding. Candidate-1 returned 403 from one attempt. Its next folder creation then needed about 28 s inside EventStore and missed the 30 s budget. Candidates 2 and 3 failed fixture bootstrap with `AddUserToTenant` 500. Concurrent sessions kept load averages at 15–71 on 14 cores, about double for the candidate runs. **SM4 remains open.** The remaining cost is per-RPC latency: about 0.3–2.8 s per execution fence and seven sequential 0.16–0.55 s source-head reads inside `/process`. Lifecycle reads keep their three independent observations. A concurrent session committed and pushed the change to EventStore main as `528696d93`. The base is now `88c8b27b7`, so live qualification must be repeated on it. Review follow-ups are uncommitted. [Transport remediation evidence](evidence/story-12-2-sm4-transport-20261126.json)
