---
title: 'Story 12.2: Durable projections and task-completion pipeline'
type: 'feature'
created: '2026-10-18'
status: 'in-progress'
baseline_commit: 'c22b13137202f7bf8c60b80c095a69748435bd6e'
route: 'dispatch'
review_loop_iteration: 4
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
- [ ] Approved current SDK enrollment: run the unchanged strict authenticated real-provider durability acceptance on final audited runtime; retain commands/results, source/runtime identities, failed attempts, SM4 measurements and exact owned-resource cleanup in a new evidence packet. Preserve all prior packets and distinguish this migration's completion from whole-story SDK broad-regression, Release/package/PD10, SM4 and independent-review gates.

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

## Design Notes

Intent gaps: none. Irreversibles: none; live checks own disposable state. Footprint: fold/guards, SDK readers/writer, host/Workers wiring and tests. Baseline: clean `main`, Folders `c22b13137202f7bf8c60b80c095a69748435bd6e`, EventStore `84bb115a0ced2497ca4315644a677b273cb44edf`. Rebuild current source; 12.1 acceptance used an older pin. Preserve historical manifest evidence; no sprint-status regeneration.

Round four: operational status has one live writer (Workers) and one rebuild path (the SDK-driven named transport). That keeps tenant-wide rebuild sessions out of the command path, which EventStore awaits but does not fail on. Trade-off: a dead-lettered delivery, or history from before enablement, leaves that folder honestly `projection_stale` until an operator runs the named rebuild. The retention docs task records this. The strict profile and source-time expiry rules are frozen decisions. The five-minute skew tolerance and the static serializer options are implementation choices.

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
- [ ] Sequentially build current Debug project-reference runtime, run affected owner/consumer checks individually, and execute unchanged strict real-provider acceptance with audited identities and exact owned cleanup. Diagnose ResponseEnded independently if it recurs. Refresh only the pending source inventory for new compile inputs; preserve historical evidence and disabled v2. Retain all failed attempts and separate SDK broad, Release/package, live capacity, SM4 and independent-review gates.

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

- [ ] `tests/Hexalith.Folders.AppHost.Tests/FolderDurabilityBootstrapConfiguration.cs`: add exactly
  `tenants:update` once to the existing disposable administrator, retaining all other realm semantics.
- [ ] `tests/Hexalith.Folders.AppHost.Tests/FolderDurabilityAcceptanceFixture.cs` and focused single-type
  helpers: maintain only the fixture's own tenant through the existing canonical authenticated
  `UpdateTenant`, with its original name/description and administrator. Use bounded foreground
  positive-evidence/restart hooks, a conservative two-minute trigger, and caller cancellation.
  Require terminal Completed, the actual matching original TenantUpdated source event, and normal
  Workers delivery reaching that source sequence/timestamp with identical enabled state, principals,
  roles and configuration. Missing/malformed/conflicting/failed/undelivered evidence fails the fixture;
  no background heartbeat, global client hook, direct source/derived write, old-event restamp, grant
  churn or stale authorization is permitted. Reuse existing source/catalog/issuer guards.
- [ ] Adjacent AppHost fixture-policy/realm tests: qualify the exact added permission and unchanged
  other identities/grants; no command when sufficiently fresh; own-tenant canonical command shape;
  rejected/failed or unobserved source/projection cannot establish freshness; unchanged authority;
  bounded cancellation. Preserve existing production stale-denial controls and all original bodies.
- [ ] Build/run focused current-source controls only and hand source/outputs back without an active
  command. Parent then refreshes only pending compile-input inventory, rebuilds/audits runtime, and
  reruns the unchanged strict class with private traces and exact owned cleanup. Retain every failed
  attempt; keep SDK parity/red-phase/optional lanes, Release/package, capacity, PD10/governance, SM4
  and whole-story review dispositions separate. No publication, Git mutation or v2 exposure.

Source inspection established that normal UpdateTenant always emits TenantUpdated for an authorized
active tenant and changes only name/description. Its exact `tenants:update` admission has no admin
bypass. Repeating AddUserToTenant for an existing member is rejected. This exception qualifies an
actively maintained disposable tenant and does not qualify indefinite idle-tenant access.
