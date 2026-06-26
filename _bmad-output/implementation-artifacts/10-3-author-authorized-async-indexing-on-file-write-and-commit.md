---
baseline_commit: 3209d8e
---

# Story 10.3: Author authorized asynchronous indexing on file-write and commit

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

> [!IMPORTANT]
> **Reworked 2026-06-23 (`sprint-change-proposal-2026-06-23-story-10-3-searchindexentrychanged-mechanism.md`).** The previously-reviewed implementation indexed via `MemoriesClient.IngestAsync(...)` — the wrong Memories subsystem (experimental `HXL001` RAG memory-ingestion: LLM embeddings → memory units → workflow instance id). The Memories **search index** that Epic 9's `hexalith-folders → folders-index` routing feeds is updated **only** by publishing `SearchIndexEntryChanged` CloudEvents to `pubsub` / `memories-events` (proven by the live `Hexalith.Tenants` `MemoriesSearchIndexEventPublisher`). This story is reverted `review → ready-for-dev`: rework the **egress + content/metadata model** to the CloudEvent publish; the bridge projection (10.2), `/folders/events` subscription, orchestration (`SemanticIndexingProcessManager`), and authorization gating are **preserved**. See "Mechanism Correction & Dev Guidance" in Dev Notes.

## Story

As a Folders platform engineer,
I want Folders workers to asynchronously index authorized file versions into Memories after file-write and commit evidence is durable,
so that indexing failures are recorded as retryable bridge status without rolling back accepted Folders file operations.

## Context & Scope Boundary

Epic 10 activates the dormant Memories topology shipped in Epic 9. Story 10.1 added the worker-only Memories dependency and the Folders-owned `ISemanticIndexingPort`; Story 10.2 added the Folders-owned semantic-indexing bridge projection/read model. Story 10.3 must now connect durable file-write/commit evidence to a worker-side indexing orchestrator.

In scope:

1. Add a worker-side semantic-indexing event/orchestration path that receives `WorkspaceFileMutationAccepted`, `WorkspaceCommitSucceeded`, `WorkspaceCommitFailed`, and `FolderArchived` evidence without breaking the existing Tenants event subscription path.
2. Apply Folders events through `ISemanticIndexingBridgeWriter`, select eligible `stale` file-version entries, and invoke `ISemanticIndexingPort.IndexFileVersionAsync` only after authorization and policy gates pass.
3. Replace the `MemoriesSemanticIndexingPort` egress with a **Dapr pub/sub publisher** that emits one curated `SearchIndexEntryChanged` CloudEvent per indexed unit (`DaprClient.PublishEventAsync("pubsub", "memories-events", entry, cloudEventMetadata, ct)`; `cloudevent.source = hexalith-folders`, `cloudevent.type = nameof(SearchIndexEntryChanged)`, stable `cloudevent.id`), using metadata-safe curated `Text` + a flat string `Attributes` map, idempotency, correlation, and task evidence. Do **not** call `MemoriesClient.IngestAsync`; drop the `Hexalith.Memories.Client.Rest` reference.
4. Add worker-owned ports for file-content materialization and policy evaluation where current production code has no durable content reader. Default implementations must fail closed or skip with explicit bridge status; tests may use hermetic fakes.
5. Record every terminal indexing outcome back through `ISemanticIndexingBridgeWriter.RecordIndexingResultAsync`: `indexed`, `skipped`, `failed`, or `reconciliation_required`.
6. Preserve metadata-only diagnostics: no raw paths, file content, diffs, provider payloads, repository names, branch names, commit messages, tokens, or local filesystem paths in events, bridge records, logs, traces, metrics, test output, or story artifacts.

Out of scope:

1. Publishing `SearchIndexEntryChanged` (the create/update path) to `memories-events` is now **in scope** for this story (the mechanism correction folded it forward from 10.4). `SearchIndexEntryRemoved` (removal/archive/tombstone deletions) and the live end-to-end `folders-index` round-trip verification remain **Story 10.4**.
2. Do not expose REST/SDK/CLI/MCP/UI search or RAG query surfaces over Memories; that is Story 10.5.
3. Do not add Memories references outside `Hexalith.Folders.Workers`, and do not expose `Hexalith.Memories.*` DTOs through public Folders core, contract, server, client, CLI, MCP, UI, or testing surfaces.
4. Do not make Folders aggregates, REST handlers, CLI, MCP, UI, or Contracts call Memories or read filesystem/provider content directly.
5. Do not add unbounded large-file ingestion, chunking, or reference-based ingestion beyond the approved C4/C9 policy. Oversized, binary-disallowed, unavailable, or redacted content must become explicit `skipped`/`failed`/`reconciliation_required` bridge status.
6. Do not weaken production Dapr deny-by-default policy. If 10.3 needs service invocation authorization for REST ingestion through Dapr, add the minimal `folders-workers -> memories` invoke allow-rule plus negative conformance rows. Pub/sub topic scope for `memories-events` remains deferred to Story 10.4 unless the implementation demonstrably requires it for this story.

## Acceptance Criteria

1. **Folders worker consumes Folders lifecycle evidence without regressing Tenants events.** The workers host adds a Folders semantic-indexing event path for file mutation, commit succeeded/failed, and archive evidence. The existing Tenants subscription constants remain `pubsub` / `system.tenants.events` / `/tenants/events`, and `AddFoldersTenantEventWorkers` continues resolving the Tenants handlers. Current EventStore domain-event plumbing is single-options based, so the implementation must either add a separate Folders event endpoint/options path or otherwise avoid overwriting the Tenants subscription configuration.

2. **Bridge projection remains the durable indexing state source.** File-write and commit events are first applied through `ISemanticIndexingBridgeWriter.ApplyFolderEventsAsync`. The indexing orchestrator only indexes bridge entries whose current status is `stale` and whose commit/tombstone evidence makes them eligible. Tombstoned entries are never sent to Memories. Older indexing outcomes must not overwrite newer file versions; keep Story 10.2 stale-result protection intact.

3. **Authorization and policy order is enforced before any content read or Memories call.** The worker gates indexing in this exact order: tenant access, folder ACL/action authorization, path policy evidence, sensitivity classification, size/type limits, then Memories. If any gate denies, is stale, or is unavailable, the worker records a metadata-only bridge result (`skipped`, `failed`, or `reconciliation_required`) and does not read content or call Memories. Client-controlled tenant/principal/path values remain comparison inputs only; authority comes from authenticated/EventStore evidence and Folders projections.

4. **Content materialization is worker-owned, bounded, and produces the curated index text — not an upload payload.** The worker-side content materialization abstraction accepts stable identity, content hash, path metadata digest/classification, workspace/folder/task evidence, and cancellation token; it returns the **curated, C9-safe `Text`** (descriptor-derived: `Content.IndexingTextDescriptor` + non-sensitive identity tokens + `TypeClassification`; never raw bytes, never raw paths) **plus the flat `Attributes` map** and safe content-type/length evidence, OR an explicit unavailable/redacted/too-large/binary-disallowed result. Materialized bytes stay **inside the worker** for the size-eligibility gate only and are never forwarded to Memories. `MaxInlineIngestionBytes` is reframed as a curated-unit **eligibility threshold** — oversized units are skipped explicitly, not truncated or retried forever. Repurpose (do not delete) the existing `ISemanticIndexingContentMaterializer` as this seam.

5. **`MemoriesSemanticIndexingPort` publishes a curated `SearchIndexEntryChanged` CloudEvent via Dapr pub/sub.** The adapter injects `DaprClient` (not `MemoriesClient`) and calls `PublishEventAsync(PubSubName, EventsTopicName, entry, cloudEventMetadata, ct)`. The published `SearchIndexEntryChanged` carries `TenantId = FoldersSemanticIndexingDefaults.IndexTenant` (`"folders-index"`), `AggregateId` = the stable per-unit id (`CaseId`), `Text` = the curated C9-safe text, `Attributes` = a flat `Dictionary<string,string>` (`StringComparer.Ordinal`) preserving the existing `folders.*` keys **as plain strings** (drop `MetadataField`/`MetadataOrigin`/confidence), and `CorrelationId`/`CausationId`. CloudEvent metadata sets `cloudevent.id` = the stable source URI, `cloudevent.type` = `nameof(SearchIndexEntryChanged)`, `cloudevent.source` = `FoldersSemanticIndexingDefaults.CloudEventsSource` (`"hexalith-folders"`). No `MemoriesClient`, no hand-rolled `HttpClient`, no `IngestAsync`, no `HXL001` suppression, no `IngestedBy` marker. Follow the `Hexalith.Tenants` `MemoriesSearchIndexEventPublisher` precedent.

6. **Stable idempotency and source identity are deterministic.** The `cloudevent.id` (= the stable source URI) and the idempotency key are derived from managed tenant id, organization id, folder id, workspace id, file-version id, content hash reference, and source URI/resource id. They must be deterministic across retries, exclude raw path text and file bytes, and avoid culture-sensitive formatting. Idempotency is achieved by Memories upserting on the `(TenantId, AggregateId)` composite key, so re-publishing the same state is harmless and must produce the same bridge identity/result shape. The publish path returns no Memories-side workflow/memory-unit id; record the published `cloudevent.id` as the post-publish traceability handle (see open design decision (c)).

7. **Outage and remote-error behavior never rolls back Folders operations.** A Memories timeout, `MemoriesRemoteException`, invalid 2xx response, cancellation, or content materialization failure must be recorded as retryable `failed` or `reconciliation_required` bridge status with a stable reason code. The worker must not throw expected remote outages past the event processor in a way that causes a durable Folders file operation to appear rejected or rolled back. Unexpected programmer errors may still fail loudly with metadata-only diagnostics.

8. **File-write and commit evidence both drive indexing state.** Add/change file mutation entries begin as `stale` and are indexable once the durable mutation event exists and all gates pass. A successful commit updates commit evidence and may trigger retry/re-evaluation for still-stale entries; it is not the first source of file-version identity. A failed commit records commit evidence and must not erase prior indexed evidence, mark a newer file version indexed, or roll back the accepted Folders mutation. Remove operations and folder archive evidence keep entries tombstoned and do not call Memories.

9. **Dapr policy: production access-control is already correct; only the conformance test changes.** The producer publishes to `memories-events` via the `pubsub` **component** and makes **no** Dapr service-invoke call to `memories`, so no `folders-workers -> memories` invoke allow-rule is needed (it never was). `deploy/dapr/production/accesscontrol.yaml`, `deploy/dapr/production/sidecar-config-bindings.yaml`, and `tests/fixtures/dapr-policy-conformance.yaml` already model the correct deny-by-default + pub/sub-component posture and **need no edit**. Update only `tests/Hexalith.Folders.Contracts.Tests/OpenApi/DaprPolicyConformanceTests.cs`: keep `memories.DefaultAction == deny` and the empty `folders-workers` invoke/caller policies, assert the `memories-events` publish (`folders-workers`) / subscribe (`memories`) scopes are active, and drop the stale "deferred to Epic 10" framing. Keep the conformance closed-sets in lockstep.

10. **Worker DI remains validated and dependency boundaries stay closed.** `AddFoldersSemanticIndexingWorkers` registers the orchestrator, content materializer, policy evaluator, bridge writer/read model, and Memories port under `ValidateOnBuild`/`ValidateScopes`. `Hexalith.Folders.Workers` remains the only non-AppHost Folders project with `Hexalith.Memories.Client.Rest` and `Hexalith.Memories.Contracts` references. `ScaffoldContractTests` and worker registration tests must fail if Memories leaks elsewhere.

11. **Tests cover orchestration, policy, outages, and boundaries.** Add focused tests under `tests/Hexalith.Folders.Workers.Tests/` for happy-path indexing after file mutation + commit, tenant/ACL/path/sensitivity/size/type denial, content unavailable, Memories outage, duplicate delivery, stale-result protection, tombstone no-op, metadata-only diagnostics, and DI registration. Extend `tests/Hexalith.Folders.Tests/Projections/SemanticIndexing/` only if bridge status behavior changes. Extend Dapr policy conformance tests if production policy changes.

12. **Verification passes.** `dotnet build Hexalith.Folders.slnx` succeeds. The narrowed tests pass: `dotnet test tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj`, `dotnet test tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj`, and `dotnet test tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj`. If Dapr policy files change, also run the focused Dapr policy conformance gate. If exact commands are blocked by local environment state, record the blocker and do not mark implementation complete.

## Tasks / Subtasks

- [x] Task 1 - Add the Folders semantic-indexing event path without breaking Tenants events (AC: 1, 2, 8)
  - [x] Inspect the current EventStore subscription setup before editing: `src/Hexalith.Folders.Workers/FoldersWorkersModule.cs`, `src/Hexalith.Folders.Workers/Program.cs`, and `Hexalith.EventStore` subscription extensions.
  - [x] Add a Folders-event consumer endpoint/options path or equivalent dispatcher that can receive `WorkspaceFileMutationAccepted`, `WorkspaceCommitSucceeded`, `WorkspaceCommitFailed`, and `FolderArchived`.
  - [x] Preserve `FoldersTenantEventSubscription` constants and existing Tenants handler registration.
  - [x] Apply events through `ISemanticIndexingBridgeWriter.ApplyFolderEventsAsync` before indexing.

- [x] Task 2 - Add worker-side indexing orchestration (AC: 2, 3, 6, 7, 8)
  - [x] Add a worker service/process manager under `src/Hexalith.Folders.Workers/SemanticIndexing/`.
  - [x] Select eligible `stale` bridge entries after durable add/change mutation evidence and use commit success/failure as correlation/retry evidence, not as a replacement for file-version identity.
  - [x] Build `SemanticIndexingRequest` from bridge identity and safe content/policy evidence.
  - [x] Map `SemanticIndexingResult` to `SemanticIndexingResultUpdate` and persist it through `ISemanticIndexingBridgeWriter.RecordIndexingResultAsync`.

- [x] Task 3 - Implement authorization and policy evaluation for indexing (AC: 3, 4)
  - [x] Reuse accepted write-path authorization evidence without fabricating async claim-transform authority; require durable mutation evidence and fail closed when required evidence is absent.
  - [x] Reuse path-policy and sensitivity semantics from safe mutation evidence where applicable.
  - [x] Record explicit reason codes for authorization denied, tenant/ACL stale, path denied, redacted sensitivity, unsupported type, oversized content, and unavailable evidence.
  - [x] Assert no content materializer or Memories call is invoked before gates pass.

- [x] Task 4 - Add bounded content materialization (AC: 4, 6)
  - [x] Add a worker-owned content materialization port and safe result records.
  - [x] Keep the default production implementation fail-closed until a real workspace/provider-backed reader is intentionally wired, or wire the existing approved source if one exists at implementation time.
  - [x] Enforce C4 limits and Memories inline ingestion constraints; no silent truncation.
  - [x] Keep raw paths, file bytes, diffs, and provider payloads out of logs, bridge records, test names, and failure messages.

- [x] Task 5 - Publish a curated SearchIndexEntryChanged CloudEvent (AC: 5, 6, 7) — **REWORK from IngestAsync**
  - [x] Rewrite `MemoriesSemanticIndexingPort` to inject `DaprClient` and call `PublishEventAsync(PubSubName, EventsTopicName, entry, cloudEventMetadata, ct)`; remove `MemoriesClient`/`IngestAsync`/`HXL001` suppression/`IngestedBy`.
  - [x] Build `SearchIndexEntryChanged { TenantId = "folders-index", AggregateId = CaseId, Text = curatedText, Attributes = flat string dict, CorrelationId, CausationId }`; set CloudEvent metadata `cloudevent.id`/`type`/`source`.
  - [x] Map the former `MetadataField` entries to plain string `Attributes` (drop `MetadataOrigin`/confidence); re-confirm no C9-sensitive value (raw path) is included.
  - [x] Translate Dapr publish failure to a retryable `Failed` result (`memories_publish_error`); keep `OperationCanceledException` rethrow on caller cancellation.
  - [x] Drop the `Hexalith.Memories.Client.Rest` `ProjectReference` (`.csproj`) and the `AddMemoriesClient()` registration; ship the `ScaffoldContractTests` allowlist edit (L156) in the SAME change set.

- [x] Task 6 - Update the Dapr policy conformance test for the active pub/sub path (AC: 9) — **REWORK**
  - [x] **DECISION OVERRIDE (Jerome, 2026-06-23): wired the pub/sub scope now.** The original sub-bullet ("do NOT edit `deploy/dapr/production/*.yaml`; access-control already correct") rested on a false premise — the production `pubsub.yaml` did **not** authorize `memories-events` (`folders-workers` publish + `memories` subscribe scopes were empty). To make AC9's "assert the scopes are active" literally true and the producer actually authorized in prod, `deploy/dapr/production/pubsub.yaml` was edited: `protectedTopics` += `memories-events`; `publishingScopes` `folders-workers=memories-events`; `subscriptionScopes` `memories=memories-events`. `accesscontrol.yaml` + `dapr-policy-conformance.yaml` (invoke fixture) were left untouched (no invoke path; semantic hash unchanged).
  - [x] Update `tests/Hexalith.Folders.Contracts.Tests/OpenApi/DaprPolicyConformanceTests.cs`: keep `memories.DefaultAction == deny` + empty `folders-workers` invoke policies; assert the `memories-events` publish (`folders-workers`) / subscribe (`memories`) scopes are active; drop the "deferred to Epic 10" framing. (Also updated `ProductionPubSubComponentShouldConstrainTenantEventTopicScopes` for the new active scopes.)
  - [x] Update the now-stale assertions in `SemanticIndexingProcessManagerTests`, `EventStoreSemanticIndexingBridgeStoreTests`, `SemanticIndexingEndpointE2ETests`, and `SemanticIndexingWorkerRegistrationTests` (`WorkflowId`/`MemoryUnitId` → single `PublishedEventId`; port substitutes `DaprClient.PublishEventAsync` via NSubstitute, not `MemoriesClient`).

- [x] Task 7 - Add focused worker and boundary tests (AC: 1, 3, 4, 7, 8, 10, 11)
  - [x] Add worker orchestration tests with fake bridge writer, fake content materializer, fake policy evaluator, and fake port.
  - [x] Add Memories port tests with a fake `HttpMessageHandler` or `MemoriesClient` boundary consistent with the existing client design.
  - [x] Add no-content-read-before-authorization tests for tenant, ACL, path, sensitivity, and size/type denial.
  - [x] Add duplicate delivery, stale result, tombstone no-op, and outage/reconciliation tests.
  - [x] Extend DI and dependency-boundary tests to prove registrations validate and Memories references stay worker-only.

- [x] Task 8 - Build and test (AC: 12)
  - [x] Run `dotnet build Hexalith.Folders.slnx`.
  - [x] Run `dotnet test tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj`.
  - [x] Run `dotnet test tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj`.
  - [x] Run `dotnet test tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj`.
  - [x] Dapr policy files changed (`pubsub.yaml` + `DaprPolicyConformanceTests`, per Jerome's decision) — ran the Dapr policy conformance gate green (9/9) via the full `Hexalith.Folders.Contracts.Tests` lane (274/274).

### Review Follow-ups (AI) — D1 production hardening (2026-06-23, bmad-dev-story continuation)

These close the three D1 production follow-ups that held the story at `in-progress` after the code review.

- [x] **[AI-Review][D1] #2 — wire the production pub/sub `folders.events` scope.** Added `folders.events` to `deploy/dapr/production/pubsub.yaml` (`protectedTopics`; `publishingScopes` `eventstore=folders.events`; `subscriptionScopes` `folders-workers=system.tenants.events,folders.events`) and updated `DaprPolicyConformanceTests.ProductionPubSubComponentShouldConstrainTenantEventTopicScopes` (protectedTopics set, the eventstore publish scope, the folders-workers dual subscribe, and eventstore added to the "no other publishers" exclusion). `accesscontrol.yaml` + the invoke fixture are untouched (no invoke path; semantic hash unchanged).
- [x] **[AI-Review][D1] #1 — carry the EventStore `TopicOverrides:folders=folders.events` override into production and pin both halves.** Extracted `FoldersAspireModule.WithFoldersDomainEventTopicOverride` (+ `FolderDomainEventsTopic` / `EventStorePublisherFolderTopicOverrideKey` consts); the AppHost `Program.cs` now calls the helper. Recorded the override on the `hexalith-eventstore` production Deployment fragment (`sidecar-config-bindings.yaml`) and the operator doc (`container-images-and-dapr-app-ids.md`). Pinned three ways: `AspireTopologyTests` (hermetic Publish-mode env resolution off the eventstore resource), `ContainerImageConformanceTests.ProductionEventStoreDeploymentShouldCarryFolderDomainEventTopicOverride` (prod artifact + doc), and the Workers `WorkerLocalMemoriesRoutingDefaultsShouldMatchAspireConstants` lockstep (`FolderDomainEventsTopic == DomainEventsTopicName`).
- [x] **[AI-Review][D1] #3 — stand up the Tier-3 Aspire cross-process publish→subscribe harness** (per Jerome: "create the aspire testing harness"). New `tests/Hexalith.Folders.AppHost.Tests` project: `AspireFoldersAppHostFixture` boots the full Folders AppHost via `DistributedApplicationTestingBuilder.CreateAsync<Projects.Hexalith_Folders_AppHost>` and waits for the six resources Running; `FoldersTopologyCrossProcessTests` proves the eventstore folder-events publisher + the folders-workers / memories subscribers boot together (the cross-process activation of the dormant Epic 9 routing with the D1 wiring). Opt-in gated (`HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION`) so the hermetic lanes skip cleanly (verified 2 SKIP); runnable on a DCP-capable lane (the Epic 9 live-boot residual). The deeper folder-mutation → worker-receipt assertion layers on this same harness and is moot end-to-end until a real content reader replaces the fail-closed materializer (Task 4). Registered in `Hexalith.Folders.slnx` + `ScaffoldContractTests` (both allowlists + reference policy).

### Review Findings (Code Review 2026-06-26, bmad-code-review chunk)

- [x] [Review][Patch] AC3 still lacks folder ACL/action freshness — Jerome chose fix-now on 2026-06-26: extended bridge evidence with actor/action and enforced folder ACL/action authorization before content materialization or Memories publish.
- [x] [Review][Patch] AC4 materializer contract was shifted into the port — Jerome chose fix-now on 2026-06-26: moved curated C9-safe `Text` plus flat `Attributes` into the materializer result/request path; the port now publishes the materialized document.
- [x] [Review][Patch] Retryable tombstone egress failures lose the delete/archive intent [src/Hexalith.Folders.Workers/SemanticIndexing/SemanticIndexingProcessManager.cs:97]
- [x] [Review][Patch] Malformed envelopes with null event type or payload can still poison-redeliver [src/Hexalith.Folders.Workers/SemanticIndexing/FoldersSemanticIndexingEventProcessor.cs:46]
- [x] [Review][Patch] Tier-3 cross-process harness does not exercise EventStore-to-worker delivery [tests/Hexalith.Folders.AppHost.Tests/FoldersTopologyCrossProcessTests.cs:53]

## Dev Notes

### Mechanism Correction & Dev Guidance (2026-06-23)

**Why this story was reverted:** the reviewed build called `MemoriesClient.IngestAsync(...)` (Memories' experimental `HXL001` RAG memory-ingestion: `POST /api/ingest` → async LLM-embedding workflow → "memory units"). That subsystem is **not** what Epic 9's `hexalith-folders → folders-index` routing ingests. The search index is updated **only** by `SearchIndexEntryChanged` / `SearchIndexEntryRemoved` CloudEvents on `pubsub` / `memories-events`, upserted by `(TenantId, AggregateId)` into RediSearch/BM25. References: `Hexalith.Tenants/samples/Hexalith.Tenants.Sample/Handlers/MemoriesSearchIndexEventPublisher.cs` (publishes the event; never calls `IngestAsync`) and `memories-search-index-handoff-2026-06-23.md` §4–§5.

**Contract (verbatim, `Hexalith.Memories.Contracts.V1`):**

```csharp
public sealed record SearchIndexEntryChanged
{
    public required string TenantId { get; init; }                 // = "folders-index"
    public required string AggregateId { get; init; }              // stable per-unit id (CaseId)
    public required string Text { get; init; }                     // curated, C9-safe searchable text
    public Dictionary<string, string> Attributes { get; init; } = new(StringComparer.Ordinal);
    public string? CorrelationId { get; init; }
    public string? CausationId { get; init; }
}
// SearchIndexEntryRemoved: same TenantId/AggregateId + CorrelationId/CausationId (deletion path; Story 10.4)
```

**Publish pattern (inject `DaprClient`, NOT `MemoriesClient`):**

```csharp
var entry = new SearchIndexEntryChanged {
    TenantId = FoldersSemanticIndexingDefaults.IndexTenant,        // "folders-index"
    AggregateId = CaseId(request),
    Text = curatedText,                                            // descriptor-derived; no raw bytes/paths
    Attributes = new Dictionary<string, string>(StringComparer.Ordinal) {
        ["folders.managedTenantId"] = request.ManagedTenantId,
        ["folders.organizationId"] = request.OrganizationId,
        ["folders.folderId"] = request.FolderId,
        ["folders.fileVersionId"] = request.FileVersionId,
        ["folders.contentHash"] = request.ContentHash,
        ["folders.contentDescriptor"] = request.Content.IndexingTextDescriptor,
        ["folders.sizeClassification"] = request.Content.SizeClassification,
        ["folders.typeClassification"] = request.Content.TypeClassification,
        ["folders.sensitivityClassification"] = request.Policy.SensitivityClassification,
        ["folders.pathPolicyOutcome"] = request.Policy.PathPolicyOutcome,
    },                                                             // plain strings — no MetadataOrigin/confidence
    CorrelationId = request.CorrelationId,
    CausationId = request.TaskId,
};
var metadata = new Dictionary<string, string>(StringComparer.Ordinal) {
    ["cloudevent.id"] = request.Source.ToUriString(),             // stable; echoed back as ScoredResult.SourceUri
    ["cloudevent.type"] = nameof(SearchIndexEntryChanged),
    ["cloudevent.source"] = FoldersSemanticIndexingDefaults.CloudEventsSource, // "hexalith-folders"
};
await _daprClient.PublishEventAsync(
    FoldersSemanticIndexingDefaults.PubSubName, FoldersSemanticIndexingDefaults.EventsTopicName,
    entry, metadata, cancellationToken).ConfigureAwait(false);
```

**Resolved design decisions (carry into the rework):**

- **(a) What populates `Text`:** descriptor-derived, C9-safe only — `Content.IndexingTextDescriptor` + non-sensitive identity tokens (`FileVersionId`, folder display label) + `TypeClassification`. Never raw `ContentBytes`, never raw path segments or sensitivity-restricted names. Empty descriptor → fall back to `"{typeClassification} {fileVersionId}"`, never echo a path.
- **(b) `MaxInlineIngestionBytes`:** keep the constant; reframe from "transport upload cap" to "curated-unit **eligibility** threshold". Bytes are materialized only to evaluate the gate and stay in-worker; oversized → fail-closed `skipped`.
- **(c) Bridge evidence fields:** replace `WorkflowInstanceId`/`MemoryUnitId` with a single `PublishedEventId` (= `cloudevent.id`) across `SemanticIndexingResult`, `SemanticIndexingResultUpdate`, `SemanticIndexingEvidence`, `SemanticIndexingBridgeProjection` as one coordinated edit. If schedule-constrained, leave both null **and record the rename as a tracked follow-up** — do not leave them silently misnamed.
- **(d) Content materializer:** repurpose (don't delete) `ISemanticIndexingContentMaterializer` to also emit the curated `Text` + flat `Attributes`; it stays the only seam that touches bytes (the fail-closed boundary).
- **(e) `Attributes`:** map each former `MetadataField` to a plain string; drop `MetadataOrigin.Human` + the `1.0f` confidence (BM25 has no origin/confidence). `pathPolicyOutcome` is a classification (safe); re-confirm no raw path is among the kept values.

**Coupling risks (ship together; don't half-do):**

- Drop `Hexalith.Memories.Client.Rest` `ProjectReference` (`Hexalith.Folders.Workers.csproj`) **and** remove it from the `ScaffoldContractTests` allowlist (L156) in the **same** change set, or the build/test goes red. Keep `Hexalith.Memories.Contracts`. Confirm `Dapr.Client` is reachable (transitive vs explicit).
- Remove `AddMemoriesClient()` from `FoldersWorkersModule` (`DaprClient` is already registered).
- Move every non-null `WorkflowId`/`MemoryUnitId` assertion to `ShouldBeNull()` (or `PublishedEventId`): `SemanticIndexingProcessManagerTests`, `EventStoreSemanticIndexingBridgeStoreTests`, `SemanticIndexingEndpointE2ETests`, `SemanticIndexingWorkerRegistrationTests`.
- `DaprPolicyConformanceTests` is the only Dapr artifact that changes (AC 9).

### Source facts

- Epic 10 goal: authorized file changes are asynchronously indexed into Memories via a worker-side producer and Folders-owned bridge projection, activating Epic 9 routing and later exposing an authorized, security-trimmed query facade. [Source: `_bmad-output/planning-artifacts/epics.md` "Epic 10"]
- Story 10.3 seed AC: file-write/commit events trigger indexing after tenant -> ACL -> path policy -> sensitivity -> size/type -> Memories gates, using stable source URIs and idempotency keys; Memories outages become retryable status and do not roll back durable Folders file operations. [Source: `_bmad-output/planning-artifacts/epics.md` "Story 10.3"]
- Architecture says Memories is a separate derived semantic index, not Folders truth. File content may be read by workers after authorization and sent to Memories, but must not be embedded in Folders events, projections, logs, traces, metrics, audit records, or errors. [Source: `_bmad-output/planning-artifacts/architecture.md` "Hexalith.Memories integration implications"]
- AppHost routing already sets `hexalith-folders -> folders-index` on the standalone `memories` resource and auto-provisions `folders-index`; end-to-end flow is dormant until Epic 10 producer work lands. [Source: `_bmad-output/planning-artifacts/memories-search-index-handoff-2026-06-23.md`]
- C4 input limits are approved. Relevant bounds include 262144 bytes for a single bounded range, 1048576 aggregate response bytes, and no raw query/file content in audit visibility. [Source: `docs/exit-criteria/c4-input-limits.md`]
- C9/S-6 classifies paths, repository names, branch names, and commit messages as tenant-sensitive by default; per-tenant confidential upgrade hashes at projection write time. [Source: `_bmad-output/planning-artifacts/architecture.md` "S-6"; `docs/operations/audit-and-redaction.md` "Sensitive-metadata tiers"]

### Current code state to preserve

- `src/Hexalith.Folders.Workers/SemanticIndexing/MemoriesSemanticIndexingPort.cs` currently validates request/cancellation and returns `SemanticIndexingStatus.Deferred` with reason `adapter_shell_not_producing`. Story 10.3 should replace that shell with real ingestion, not introduce a second competing port.
- `SemanticIndexingRequest` already carries managed tenant id, organization id, folder id, file version id, content hash, source identity, content descriptor, policy outcome, correlation id, task id, and idempotency key.
- `FoldersSemanticIndexingDefaults` currently pins `CloudEventsSource = "hexalith-folders"`, `IndexTenant = "folders-index"`, `PubSubName = "pubsub"`, and `EventsTopicName = "memories-events"`. Reuse these constants; do not introduce untested duplicate literals.
- `src/Hexalith.Folders/Projections/SemanticIndexing/` owns the bridge status vocabulary and stale-result protection. Do not bypass it with a separate indexing state store.
- `EventStoreSemanticIndexingBridgeStore` already persists through `IReadModelStore`/`ReadModelWritePolicy` and folder indexes. Preserve tenant-prefixed key shapes.
- `FoldersWorkersModule.AddFoldersSemanticIndexingWorkers` registers Dapr client, bridge store, Memories client, and `ISemanticIndexingPort`. Add orchestration here or in a narrow companion method.
- `FoldersWorkersModule.AddFoldersTenantEventWorkers` currently configures EventStore domain events for Tenants with `pubsub`, `system.tenants.events`, and `/tenants/events`; current `MapEventStoreDomainEvents` maps only one options instance. Avoid option clobbering.
- Folders aggregate events currently implement `IFolderEvent`, not `Hexalith.EventStore.Contracts.Events.IEventPayload`. If the implementation uses generic EventStore domain-event subscription for Folders events, add the required marker/dependency deliberately and update dependency tests; otherwise provide a worker-local mapper/endpoint that does not create an unintended EventStore contract dependency in core.
- `WorkspaceFileMutationAccepted` contains safe metadata fields for identity and policy: tenant/org/folder/workspace/operation, file operation kind, transport operation, path policy class, path metadata digest, content hash reference, byte/media/transport evidence, actor, correlation id, task id, idempotency key/fingerprint, and occurred-at.
- `WorkspaceCommitSucceeded`/`WorkspaceCommitFailed` carry commit/provider outcome evidence plus changed-path digest/classification. Commit reference, branch ref, and commit message classification are sensitive metadata; do not expose them in indexing metadata unless represented by safe classification/digest only.
- Accepted file-mutation events prove the original command passed the write path at that time, but they do not carry a full current `EventStoreClaimTransformEvidence` object. If `LayeredFolderAuthorizationService` cannot be reused directly for async revalidation, add a narrow worker indexing-authorization service that combines durable accepted-event evidence with tenant-access and folder-ACL projection freshness. Do not fabricate claim-transform authority; fail closed with explicit bridge status when required evidence is missing or stale.

### Architecture and dependency guardrails

- Repository configuration is authoritative when prose drifts. Current repo pins .NET SDK `10.0.300`, Dapr packages `1.18.4`, Aspire `13.4.6`, CommunityToolkit Aspire Dapr `13.4.0-preview.1.260602-0230`, xUnit v3 `3.2.2`, Shouldly `4.3.0`, and NSubstitute `5.3.0`. [Source: `global.json`; `Directory.Packages.props`]
- Keep package versions centralized in `Directory.Packages.props`; never add inline `Version` attributes.
- Use C# file-scoped namespaces, nullable-safe public boundaries, ordinal comparisons, invariant formatting, cancellation tokens, and `ConfigureAwait(false)` where nearby worker/library code uses it.
- Workers own external side effects. Aggregates remain deterministic and side-effect free; REST handlers remain transport/gateway boundaries.
- Expected business/remote outcomes should be result-shaped and bridge-recorded, not thrown through normal control flow.
- Tests must be hermetic: no running Dapr sidecars, Memories server, Redis, Keycloak, provider credentials, production secrets, network calls, tenant seed data, or nested submodule initialization.

### Previous story intelligence

- Story 10.1 established the worker-only Memories dependency and added boundary tests. Its review fixed a raw Windows drive-letter source identity leak; keep that source identity hardening.
- Story 10.2 established the bridge projection/read model and EventStore-backed worker adapter. Its review fixed remove-event tombstone semantics so no-content-hash removes tombstone known file-version entries for the same path digest. Do not reintroduce stale searchable entries after removes.
- Story 10.2 verification passed: solution build 0 warnings/errors; `Hexalith.Folders.Tests` 1327/1327; `Hexalith.Folders.Workers.Tests` 36/36; `Hexalith.Folders.Testing.Tests` 60/60.
- Epic 9 residual action remains open: carry `folders`/`folders-workers -> memories` invoke authorization into Epic 10 with deny-by-default caller policies and negative-test rows. Story 10.3 owns invoke authorization only if its implementation path needs it; Story 10.4 owns `memories-events` pub/sub scope.

### Latest technical notes

- Dapr v1.18 is the current latest stable docs line as of 2026-06-23; v1.19 is preview. [Source: https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-overview/]
- Dapr pub/sub provides at-least-once delivery and CloudEvents wrapping by default, so event handling and later publish paths must remain idempotent. [Source: https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-overview/]
- Dapr service invocation provides discovery, mTLS-enabled service-to-service security, retries, tracing, and access control. HTTP service-invocation retries are bypassed for streaming requests because request bodies cannot be replayed; 10.3 should keep inline bounded ingestion idempotent and not rely on streaming retries. [Source: https://docs.dapr.io/developing-applications/building-blocks/service-invocation/service-invocation-overview/]
- Dapr service invocation access control is applied to the called app's sidecar; absent policy defaults allow, while explicit policies can deny by default and allow only specific app/operation/verb combinations. [Source: https://docs.dapr.io/operations/configuration/invoke-allowlist/]

### References

- `_bmad-output/planning-artifacts/epics.md` - Epic 10 and Story 10.3.
- `_bmad-output/planning-artifacts/architecture.md` - Memories integration, AppHost composition, Dapr access-control decision I-3, S-6/C9 sensitive metadata.
- `_bmad-output/planning-artifacts/memories-search-index-handoff-2026-06-23.md` - exact Epic 9 routing handoff and Epic 10 activation checklist.
- `_bmad-output/planning-artifacts/research/technical-hexalith-memories-semantic-indexing-rag-research-2026-05-11.md` - Memories ingestion/search research, inline ingestion constraints, and worker-side port recommendation.
- `docs/exit-criteria/c4-input-limits.md` - approved C4 bounds and metadata-only audit visibility.
- `docs/operations/audit-and-redaction.md` - real S-6/C9 redaction sources and blocklist guidance.
- `_bmad-output/implementation-artifacts/10-1-define-worker-side-semantic-indexing-port-and-memories-dependency.md` - worker port and dependency boundary context.
- `_bmad-output/implementation-artifacts/10-2-build-folders-owned-indexing-bridge-projection.md` - bridge projection/read model context and review learnings.
- `src/Hexalith.Folders.Workers/SemanticIndexing/` - current worker semantic-indexing port, adapter, defaults, and EventStore bridge store.
- `src/Hexalith.Folders/Projections/SemanticIndexing/` - bridge model/projection/read interfaces.
- `src/Hexalith.Folders.Workers/FoldersWorkersModule.cs` - worker registration and subscription configuration.
- `src/Hexalith.Folders.Workers/Program.cs` - current worker endpoint composition.
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceFileMutationAccepted.cs` - file mutation event shape.
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceCommitSucceeded.cs` and `WorkspaceCommitFailed.cs` - commit evidence event shapes.
- `Hexalith.EventStore/src/Hexalith.EventStore.Client/Subscriptions/EventStoreDomainEventsOptions.cs` and `Hexalith.EventStore/src/Hexalith.EventStore.DomainService/EventStoreDomainEventsEndpointExtensions.cs` - current single-options domain-event subscription plumbing.
- `Hexalith.Memories/src/Hexalith.Memories.Client.Rest/MemoriesClient.cs` - typed `IngestAsync(...)` client API.
- `Hexalith.Memories/src/Hexalith.Memories.Contracts/V1/IngestionInput.cs` - Memories ingestion contract.
- `Hexalith.Memories/src/Hexalith.Memories.Contracts/V1/SearchIndexEntryChanged.cs` - later Story 10.4 CloudEvent payload contract, not 10.3 scope.

## Dev Agent Record

> [!WARNING]
> **SUPERSEDED 2026-06-23.** The record below documents the original `MemoriesClient.IngestAsync` implementation, which targeted the wrong Memories subsystem and is being reworked to the `SearchIndexEntryChanged` Dapr-publish model (see the rework banner and ACs above). Its green test evidence is obsolete: the `IngestAsync` egress, the `MetadataField` mapping, the `Hexalith.Memories.Client.Rest` reference, the `HXL001` suppression, and the non-null `WorkflowId`/`MemoryUnitId` assertions must all change. The File List below is the pre-rework set; the rework additionally touches the `.csproj`, `ScaffoldContractTests`, `DaprPolicyConformanceTests`, and the bridge-store / endpoint-E2E tests.

### D1 Production-Hardening Continuation (2026-06-23 — bmad-dev-story, Claude Opus 4.8)

Closes the D1 production follow-ups that held the story at `in-progress` after the code review. All in-repo; the
real env injection (image/registry/broker endpoints) stays deployment-tooling-owned per the sanitized-artifact
discipline. See "Review Follow-ups (AI)" above for the per-item summary.

**Debug Log References**

- D1 #2 pub/sub scope: `deploy/dapr/production/pubsub.yaml` += `folders.events` (eventstore publish / folders-workers subscribe); `DaprPolicyConformanceTests` updated. `accesscontrol.yaml` + invoke fixture untouched (no invoke path).
- D1 #1 override: `FoldersAspireModule.WithFoldersDomainEventTopicOverride` helper + consts; AppHost uses it; recorded on `sidecar-config-bindings.yaml` eventstore Deployment + operator doc; pinned by `AspireTopologyTests` (dev, hermetic), `ContainerImageConformanceTests` (prod + doc), and the Workers lockstep test.
- D1 #3 harness: new `tests/Hexalith.Folders.AppHost.Tests` (`AspireFoldersAppHostFixture` + `FoldersTopologyCrossProcessTests`) using `Aspire.Hosting.Testing`; opt-in `HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION`; registered in slnx + `ScaffoldContractTests`.
- Verification: `dotnet build Hexalith.Folders.slnx` 0W/0E; `dotnet format whitespace` + `analyzers` clean over src/tests/samples; Contracts.Tests 275/275, IntegrationTests 612/612, Workers.Tests 51/51, Testing.Tests 60/60, Folders.Tests 1327/1327, AppHost.Tests 2 SKIP (opt-in not set).

**Completion Notes**

- ✅ D1 #1 (prod EventStore override) + #2 (prod pub/sub scope) closed in-repo and gated by deployment conformance; pinned in dev (AppHost helper + hermetic Aspire test) and prod (deployment fragment + operator doc) so the load-bearing `folders.events` wiring cannot silently rot — directly closing the D1 "green tests, broken wiring" trap.
- ✅ D1 #3: the Tier-3 Aspire cross-process publish→subscribe harness is delivered. Its live execution needs a DCP-capable lane (the env-wide Aspire CLI/DCP boot mismatch is the Epic 9 residual, not a defect here); the harness skips cleanly everywhere else so no lane goes red.
- The deeper folder-mutation → worker-receipt assertion layers on `AspireFoldersAppHostFixture`; it is moot end-to-end today because the production content materializer is fail-closed (Task 4) until a real workspace/provider reader is wired (tracked follow-up).
- All 9 code-review findings (D1–D4, P1–P5) are now resolved; the "Review Findings" checkboxes are ticked to match the prior outcome note.

**File List (D1 continuation change set)**

- `src/Hexalith.Folders.Aspire/FoldersAspireModule.cs`
- `src/Hexalith.Folders.AppHost/Program.cs`
- `deploy/dapr/production/pubsub.yaml`
- `deploy/dapr/production/sidecar-config-bindings.yaml`
- `docs/operations/container-images-and-dapr-app-ids.md`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/DaprPolicyConformanceTests.cs`
- `tests/Hexalith.Folders.Contracts.Tests/Deployment/ContainerImageConformanceTests.cs`
- `tests/Hexalith.Folders.IntegrationTests/AspireTopologyTests.cs`
- `tests/Hexalith.Folders.Workers.Tests/SemanticIndexingWorkerRegistrationTests.cs`
- `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs`
- `Hexalith.Folders.slnx`
- `tests/Hexalith.Folders.AppHost.Tests/Hexalith.Folders.AppHost.Tests.csproj` (new)
- `tests/Hexalith.Folders.AppHost.Tests/AspireFoldersAppHostFixture.cs` (new)
- `tests/Hexalith.Folders.AppHost.Tests/FoldersTopologyCrossProcessTests.cs` (new)
- `tests/Hexalith.Folders.AppHost.Tests/README.md` (new)
- `_bmad-output/implementation-artifacts/10-3-author-authorized-async-indexing-on-file-write-and-commit.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

### Rework Dev Record (2026-06-23 — SearchIndexEntryChanged publish; supersedes the IngestAsync record below)

**Agent Model Used:** Claude Opus 4.8 (1M context)

**Debug Log References**

- Rewrote `MemoriesSemanticIndexingPort` to inject `DaprClient` (not `MemoriesClient`) and publish one curated `SearchIndexEntryChanged` per unit via `PublishEventAsync("pubsub", "memories-events", entry, cloudEventMetadata, ct)`; `TenantId="folders-index"`, `AggregateId=CaseId` (`{tenant}:{org}:{folder}`), `CorrelationId`/`CausationId=TaskId`; metadata `cloudevent.id`=source URI, `type`=`nameof(SearchIndexEntryChanged)`, `source`="hexalith-folders". Dropped `IngestAsync`/`HXL001`/`IngestedBy`/`MetadataField`.
- Full `WorkflowId`/`MemoryUnitId` → single `PublishedEventId` (= `cloudevent.id`) rename across `SemanticIndexingResult`, `SemanticIndexingResultUpdate`, `SemanticIndexingEvidence`, `SemanticIndexingBridgeProjection.ApplyIndexingResult`, and `SemanticIndexingProcessManager` (chose design-decision (c)'s full-rename path, not the deferred-null fallback).
- Removed `Hexalith.Memories.Client.Rest` `ProjectReference` + `AddMemoriesClient()`; updated `ScaffoldContractTests` allowlist. `Hexalith.Memories.Contracts` stays (Workers-only). `DaprException` resolves via `using Dapr;` (Dapr.Common assembly, base of Dapr SDK exceptions).
- Per Jerome's decision (AskUserQuestion 2026-06-23), wired the production pub/sub scope now: `deploy/dapr/production/pubsub.yaml` `protectedTopics`+=`memories-events`, `publishingScopes` `folders-workers=memories-events`, `subscriptionScopes` `memories=memories-events`; updated both pub/sub facts in `DaprPolicyConformanceTests`.
- `dotnet build Hexalith.Folders.slnx`: 0 warnings / 0 errors.
- `dotnet test tests/Hexalith.Folders.Workers.Tests`: 50/50. `tests/Hexalith.Folders.Tests`: 1327/1327. `tests/Hexalith.Folders.Testing.Tests`: 60/60. `tests/Hexalith.Folders.Contracts.Tests` (incl. Dapr policy conformance 9/9): 274/274.
- `dotnet format whitespace --verify-no-changes`: clean over Folders-owned `src`/`tests` (only pre-existing `Hexalith.Memories` submodule ENDOFLINE noise, out of scope/read-only).

**Completion Notes**

- ✅ AC5/AC6/AC7 reworked to the Dapr pub/sub `SearchIndexEntryChanged` publish model; the bridge projection (10.2), `/folders/events` subscription, `SemanticIndexingProcessManager` orchestration, and authorization/policy/materialization gating (Tasks 1–4) are preserved unchanged.
- **AC4 vs AC5 reconciliation:** the curated `Text` + flat `Attributes` are built inside the port from `SemanticIndexingRequest` fields exactly as the Dev-Notes verbatim publish pattern shows (design A). `Text` = `IndexingTextDescriptor` + `FileVersionId` + `TypeClassification` (C9-safe, descriptor-derived, never raw bytes/paths), with the `{typeClassification} {fileVersionId}` fallback. Materialized bytes stay strictly in-worker for the size-eligibility gate only — `ContentBytes` was removed from `SemanticIndexingRequest`, so no bytes reach the egress at all (stronger than the literal AC).
- **AC9 / Task 6 sub-bullet 1 deviation (user-directed):** the reworked story claimed `pubsub.yaml` already authorized `memories-events` and said "do not edit yaml", but the file did not (verified). Jerome chose "wire the pub/sub scope now" so the producer is authorized in production and AC9's "scopes are active" is literally true. `memories` stays deny-by-default with no invoke caller policies; only the pub/sub component scopes changed. `accesscontrol.yaml` + invoke conformance fixture untouched (semantic hash unchanged).
- **AggregateId granularity:** kept folder-level `CaseId` (`{tenant}:{org}:{folder}`) per the verbatim AC5 publish pattern; the per-file-version identity is carried by `cloudevent.id` (= source URI, echoed back as `ScoredResult.SourceUri`).
- Added `NSubstitute` to `Hexalith.Folders.Workers.Tests.csproj` (centrally-pinned, project-approved test double) to substitute the abstract `DaprClient`; no new solution dependency.
- Metadata-only preserved: no raw paths, bytes, diffs, provider payloads, or drive-letters in events/attributes/logs/tests (asserted in the port test).

**File List (authoritative — rework change set)**

- `src/Hexalith.Folders/Projections/SemanticIndexing/SemanticIndexingEvidence.cs`
- `src/Hexalith.Folders/Projections/SemanticIndexing/SemanticIndexingResultUpdate.cs`
- `src/Hexalith.Folders/Projections/SemanticIndexing/SemanticIndexingBridgeProjection.cs`
- `src/Hexalith.Folders.Workers/SemanticIndexing/MemoriesSemanticIndexingPort.cs`
- `src/Hexalith.Folders.Workers/SemanticIndexing/SemanticIndexingResult.cs`
- `src/Hexalith.Folders.Workers/SemanticIndexing/SemanticIndexingRequest.cs`
- `src/Hexalith.Folders.Workers/SemanticIndexing/SemanticIndexingProcessManager.cs`
- `src/Hexalith.Folders.Workers/FoldersWorkersModule.cs`
- `src/Hexalith.Folders.Workers/Hexalith.Folders.Workers.csproj`
- `deploy/dapr/production/pubsub.yaml`
- `tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj`
- `tests/Hexalith.Folders.Workers.Tests/SemanticIndexingWorkerRegistrationTests.cs`
- `tests/Hexalith.Folders.Workers.Tests/SemanticIndexingProcessManagerTests.cs`
- `tests/Hexalith.Folders.Workers.Tests/SemanticIndexingEndpointE2ETests.cs`
- `tests/Hexalith.Folders.Workers.Tests/EventStoreSemanticIndexingBridgeStoreTests.cs`
- `tests/Hexalith.Folders.Tests/Projections/SemanticIndexing/SemanticIndexingBridgeProjectionTests.cs`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/DaprPolicyConformanceTests.cs`
- `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs`
- `_bmad-output/implementation-artifacts/10-3-author-authorized-async-indexing-on-file-write-and-commit.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

### Agent Model Used (superseded — original IngestAsync impl)

GPT-5 Codex

### Debug Log References

- 2026-06-23: Added worker-local Folders semantic-indexing event processor and `/folders/events` subscription endpoint on `folders.events`; Tenants subscription remains on `pubsub` / `system.tenants.events` / `/tenants/events`.
- 2026-06-23: Added `SemanticIndexingProcessManager`, accepted-event metadata policy evaluator, worker-owned content materialization port, C4 inline size/type guards, and deterministic indexing idempotency/result fingerprints.
- 2026-06-23: Extended bridge evidence with safe mutation metadata and preserved it across result updates so policy and materialization do not need raw path or file content.
- 2026-06-23: Replaced `MemoriesSemanticIndexingPort` adapter shell with typed `MemoriesClient.IngestAsync(...)` call, narrow `HXL001` suppression, metadata-safe request mapping, and retryable remote/transport failure results.
- 2026-06-23: Direct configured `MemoriesClient` HTTP path used; production Dapr access-control artifacts intentionally unchanged. No `memories-events` pub/sub scopes added in 10.3.
- 2026-06-23: `dotnet test tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj -m:1 -nr:false` passed 46/46.
- 2026-06-23: `dotnet test tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj -m:1 -nr:false` passed 1327/1327.
- 2026-06-23: `dotnet test tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj -m:1 -nr:false` passed 60/60.
- 2026-06-23: `dotnet build Hexalith.Folders.slnx -m:1 -nr:false` passed with 0 warnings/errors.

### Completion Notes List

- Folders semantic-indexing event handling is separated from Tenants event subscription to avoid `EventStoreDomainEventsOptions` clobbering.
- Bridge entries are applied before indexing, and only stale non-tombstoned entries with content hash evidence are considered for indexing.
- Accepted file-mutation evidence now carries safe metadata for path policy class, content length, media type, and transport evidence; no raw path or file bytes are stored in the bridge.
- Content materialization is worker-owned and fail-closed by default until a real authorized workspace/provider reader is intentionally wired.
- Memories ingestion now uses the typed client with safe metadata and deterministic Folders source identity.
- Story is ready for code review; verification passed from the parent process after the stopped fallback session hit tmux-local VSTest socket issues.

### File List

- `src/Hexalith.Folders.Workers/FoldersWorkersModule.cs`
- `src/Hexalith.Folders.Workers/SemanticIndexing/FailClosedSemanticIndexingContentMaterializer.cs`
- `src/Hexalith.Folders.Workers/SemanticIndexing/FailClosedSemanticIndexingPolicyEvaluator.cs`
- `src/Hexalith.Folders.Workers/SemanticIndexing/FoldersSemanticIndexingDefaults.cs`
- `src/Hexalith.Folders.Workers/SemanticIndexing/FoldersSemanticIndexingEventProcessor.cs`
- `src/Hexalith.Folders.Workers/SemanticIndexing/ISemanticIndexingContentMaterializer.cs`
- `src/Hexalith.Folders.Workers/SemanticIndexing/ISemanticIndexingPolicyEvaluator.cs`
- `src/Hexalith.Folders.Workers/SemanticIndexing/MemoriesSemanticIndexingPort.cs`
- `src/Hexalith.Folders.Workers/SemanticIndexing/SemanticIndexingProcessManager.cs`
- `src/Hexalith.Folders.Workers/SemanticIndexing/SemanticIndexingRequest.cs`
- `src/Hexalith.Folders/Projections/SemanticIndexing/SemanticIndexingBridgeProjection.cs`
- `src/Hexalith.Folders/Projections/SemanticIndexing/SemanticIndexingEvidence.cs`
- `tests/Hexalith.Folders.Workers.Tests/SemanticIndexingProcessManagerTests.cs`
- `tests/Hexalith.Folders.Workers.Tests/SemanticIndexingWorkerRegistrationTests.cs`
- `_bmad-output/implementation-artifacts/10-3-author-authorized-async-indexing-on-file-write-and-commit.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## Change Log

- 2026-06-23: Story context created by BMAD create-story workflow. Ultimate context engine analysis completed - comprehensive developer guide created.
- 2026-06-23: Added worker-side semantic-indexing event path, orchestration, content/policy ports, safe bridge mutation evidence, real Memories typed-client ingestion, focused worker tests, and green verification evidence; story moved to review.
- 2026-06-23: CORRECTED via bmad-correct-course (`sprint-change-proposal-2026-06-23-story-10-3-searchindexentrychanged-mechanism`). The `IngestAsync` egress targeted the wrong Memories subsystem (RAG ingestion, not the search index); reverted `review → ready-for-dev`. Scope + ACs 4/5/6/9 + Tasks 5/6 reworked to publish `SearchIndexEntryChanged` via Dapr pub/sub; bridge/subscription/orchestration/gating preserved. Added "Mechanism Correction & Dev Guidance"; prior Dev Agent Record superseded.
- 2026-06-23: REWORK IMPLEMENTED (bmad-dev-story, Claude Opus 4.8). Rewrote `MemoriesSemanticIndexingPort` to publish a curated `SearchIndexEntryChanged` CloudEvent via `DaprClient` pub/sub (`pubsub`/`memories-events`); dropped `Hexalith.Memories.Client.Rest` reference + `AddMemoriesClient()`; renamed bridge evidence `WorkflowId`/`MemoryUnitId` → `PublishedEventId`; removed `ContentBytes` from `SemanticIndexingRequest` (bytes stay in-worker). Per Jerome's decision, wired the production `memories-events` pub/sub scope in `deploy/dapr/production/pubsub.yaml` and updated `DaprPolicyConformanceTests`. Tasks 5 & 6 complete; build 0W/0E; Workers.Tests 50/50, Folders.Tests 1327/1327, Testing.Tests 60/60, Contracts.Tests 274/274. Story → review.
- 2026-06-23: CODE REVIEW (bmad-code-review, Claude Opus 4.8) — adversarial multi-layer review surfaced 2 test-masked production defects (folder-level AggregateId data-loss; inbound topic mismatch) the green suite missed. Applied all 9 findings: **D2** per-file-version `AggregateId`; **D4** invalid-payload → 200 drop + metadata-only log; **D1** AppHost `EventStore:Publisher:TopicOverrides:folders=folders.events` (production deploy override + pub/sub scoping + cross-process integration test = follow-ups); **D3** tenant-access authority gate (folder-ACL freshness = follow-up story); **P1–P5** dedup/idempotency-key removal, `TrySourceFrom`, size-guard, `ToLowerInvariant`. Build 0W/0E; Workers.Tests 51/51, Folders.Tests TenantAccess+SemanticIndexing 36/36, Contracts.Tests DaprPolicy/Scaffold 9/9. Story → in-progress (pending D1 production follow-ups).
- 2026-06-23: D1 PRODUCTION FOLLOW-UPS CLOSED (bmad-dev-story continuation, Claude Opus 4.8). **#2** wired the production `folders.events` pub/sub scope (`deploy/dapr/production/pubsub.yaml`: eventstore publish + folders-workers subscribe) + `DaprPolicyConformanceTests`. **#1** extracted `FoldersAspireModule.WithFoldersDomainEventTopicOverride` (AppHost uses it), recorded the `EventStore__Publisher__TopicOverrides__folders=folders.events` override on the production `hexalith-eventstore` deployment fragment + operator doc, and pinned it dev (AspireTopologyTests, hermetic) + prod (ContainerImageConformanceTests) + worker lockstep. **#3** (per Jerome "create the aspire testing harness") added the Tier-3 `tests/Hexalith.Folders.AppHost.Tests` project (`AspireFoldersAppHostFixture` boots the full topology via `DistributedApplicationTestingBuilder`; `FoldersTopologyCrossProcessTests` proves eventstore→folders.events→folders-workers + memories boot together), opt-in gated + registered in slnx/ScaffoldContractTests. All 9 review findings ticked. Build 0W/0E; format whitespace+analyzers clean; Contracts.Tests 275/275, IntegrationTests 612/612, Workers.Tests 51/51, Testing.Tests 60/60, Folders.Tests 1327/1327, AppHost.Tests 2 SKIP (opt-in). Story → review.
- 2026-06-26: CODE REVIEW CHUNK PATCHES APPLIED (bmad-code-review). Fixed AC3 async authorization by carrying actor/action evidence and checking folder ACL/action freshness before materialization; fixed AC4 by moving curated text/attributes into the materializer result path; preserved tombstone delete/archive routing reason across retryable removal/archive failures; guarded null/empty event envelopes as permanent dropped payloads; added a Tier-3 EventStore-sidecar publish probe for `folders.events`. Verified: `dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore` 0W/0E; worker/test/AppHost test builds with `/p:BuildProjectReferences=false` 0W/0E; Workers.Tests 64/64; SemanticIndexingBridgeProjectionTests 20/20; AppHost.Tests 4 SKIP (opt-in not set). Full normal build/test remains blocked locally by missing nested `Hexalith.Memories/Hexalith.PolymorphicSerializations` submodule per repo policy. Story → done.

## Review Findings (Code Review 2026-06-23, bmad-code-review)

Adversarial multi-layer review (5 reviewers, 3 diverse-lens verifiers per finding) of `3209d8e..b61400d` over `src`/`tests`/`deploy`. 29 raw findings → 4 decision-needed, 5 patch, 0 defer, 16 dismissed (verified false-positive / out-of-scope).

> **Outcome 2026-06-23 (Jerome chose "apply every patch", then resolved D1/D3). All 9 findings ADDRESSED + VERIFIED in code.** Verification after the fixes: full solution build 0W/0E; Workers.Tests 51/51, Folders.Tests TenantAccess+SemanticIndexing 36/36, Contracts.Tests DaprPolicy/Scaffold 9/9.
>
> **D1/D3 implemented (Jerome's decisions):**
> - **D1 (TopicOverride):** AppHost now sets `EventStore__Publisher__TopicOverrides__folders=folders.events` on the EventStore actor host, so all managed tenants' folder events publish to the single `folders.events` topic the worker subscribes to. **Remaining follow-ups (production):** (1) the production EventStore deployment must carry the same `EventStore:Publisher:TopicOverrides:folders` override; (2) add `folders.events` to production `pubsub.yaml` protected-topics/scopes for deny-by-default + update `DaprPolicyConformanceTests`; (3) add a real cross-process publish→subscribe integration test (Tier-3 Aspire/Dapr) — the current E2E only drives the endpoint.
> - **D3 (tenant-access authority gate):** the evaluator now fails closed when the tenant-access projection is missing/disabled/has-no-principals/conflicted, BEFORE any content read (new test asserts this). **Follow-up story:** per-folder ACL freshness needs a `SemanticIndexingBridgeEntry`/`Evidence` schema extension to carry principal+action (not in 10.3 scope).
>
> - **APPLIED:** **D2** per-file-version `AggregateId` `{tenant}/{org}/{folder}/{fileVersionId}` (`/` excluded from segment ids → no cross-tenant collision; port test asserts the new key); **D4** `FailedInvalidPayload` → HTTP 200 drop + metadata-only warning (no poison loop; E2E test updated); **P1** removed the dead in-process dedup + `Duplicate` result (bridge fingerprint owns idempotency; dedup test now drives the real `InMemorySemanticIndexingBridgeStore`); **P2** removed the dead `SemanticIndexingRequest.IdempotencyKey`/`DeriveIdempotencyKey`; **P3** `TrySourceFrom` records `reconciliation_required` instead of throwing; **P4** size guard compares raw declared/observed lengths; **P5** `Hash()` → `ToLowerInvariant()`.
> - **RE-OPENED — D1 (CRITICAL, topic mismatch):** the clean fix is verified-feasible **Folders-only config** — `EventStore:Publisher:TopicOverrides:folders = folders.events` (`EventPublisherOptions.GetPubSubTopic` honours it; no EventStore SDK change). BUT it makes all tenants' folder events share one topic (isolation in-app, not by topic) and needs `folders.events` added to production pub/sub scopes + a real publish→subscribe integration test. **Mechanism + config-location decision required before implementing.**
> - **RE-OPENED — D3 (MEDIUM, policy freshness):** tenant-access freshness IS implementable now (`IFolderTenantAccessProjectionStore` is in worker DI). **Folder-ACL freshness is NOT** — the bridge entry carries no principal/action, so full AC3 needs a `SemanticIndexingBridgeEntry` schema extension (separate story). **Decide:** tenant-access-only now, or defer index-time re-auth to the 10.5 query facade.

### Decision-needed — RESOLVED 2026-06-23 (Jerome; all four → fix-now patches)

> Resolutions: **D1** = fix now, block merge (correct the topic binding + add a real publish→subscribe integration test). **D2** = fix to a per-file-version upsert key (add WorkspaceId+FileVersionId, unambiguous delimiter/hash). **D3** = require tenant-access + folder-ACL freshness now (fail closed when stale). **D4** = return 200 + record metadata-only `failed` for permanent deserialization failures.

- [x] [Review][Patch] **CRITICAL — Inbound subscription topic mismatch: worker never receives managed-tenant folder events** — The new path subscribes to `folders.events` (`FoldersSemanticIndexingDefaults.DomainEventsTopicName`), but `EventStore.EventPublisher` emits domain events to `AggregateIdentity.PubSubTopic` = `{TenantId}.{Domain}.events`, i.e. `{managedTenantId}.folders.events` for managed-tenant folder streams (bare `{domain}.events` is only the `system`-tenant form, which `project-context` forbids for folder streams). No override republishes folder events to a fixed `folders.events`. `SemanticIndexingEndpointE2ETests` only asserts declared `ITopicMetadata` / calls the processor directly — it never exercises the real publish→subscribe binding. Net: indexing never triggers in production for any real folder (AC1, AC8; the 2.8/2.8b "green tests, broken wiring" trap). Verify at runtime (mutate a folder; confirm `/folders/events` is invoked) before merge. [src/Hexalith.Folders.Workers/SemanticIndexing/FoldersSemanticIndexingDefaults.cs:13; FoldersWorkersModule.cs:130-132; Hexalith.EventStore/.../AggregateIdentity.cs:92]
- [x] [Review][Patch] **HIGH — Folder-level `AggregateId` collapses every file in a folder to ONE search-index entry (within-folder data loss); colon-join risks cross-tenant collision** — `MemoriesSemanticIndexingPort.CaseId` = `{ManagedTenantId}:{OrganizationId}:{FolderId}` (no `WorkspaceId`/`FileVersionId`) is used as `SearchIndexEntryChanged.AggregateId`, while one CloudEvent is published per file-version and Memories upserts on `(TenantId, AggregateId)`. Indexing file B overwrites file A under the same folder key — only the last file per folder stays searchable. The dead `DeriveIdempotencyKey` (which DOES include workspace+file-version) confirms per-file-version is the intended grain. Separately, `SegmentPattern` allows `:` inside ids, so colon-joining is ambiguous in the shared `folders-index` tenant → potential cross-tenant `AggregateId` collision (top invariant). Fix: include `WorkspaceId`+`FileVersionId` and use an unambiguous delimiter or hash (as `Hash()` already does with ``). Found independently by 4/5 reviewers. (AC5, AC6.) [src/Hexalith.Folders.Workers/SemanticIndexing/MemoriesSemanticIndexingPort.cs:42,117-120]
- [x] [Review][Patch] **MEDIUM — Default policy evaluator does not re-check tenant-access / folder-ACL freshness; authorizes from the accepted event's `PathPolicyClass` alone** — `FailClosedSemanticIndexingPolicyEvaluator.EvaluateAsync` inspects only `PathPolicyClass`, byte length, and media type, then returns `Allowed`. AC3 + Dev Notes require combining durable accepted-event evidence with tenant-access and folder-ACL projection freshness (or failing closed when stale). The "fail-closed" default effectively fails open w.r.t. access revoked between mutation and indexing. Decide: required in 10.3 or acceptable to defer the freshness re-check to the 10.5 query facade (nothing reads `folders-index` until then)? (AC3.) [src/Hexalith.Folders.Workers/SemanticIndexing/FailClosedSemanticIndexingPolicyEvaluator.cs:16-63]
- [x] [Review][Patch] **MEDIUM — `FailedInvalidPayload` returns HTTP 500 → Dapr redelivery; permanent deserialization failures can poison-loop** — A payload that is deterministically un-deserializable / not an `IFolderEvent` returns 500 (and `ProcessAsync` evicts its `MessageId` from the dedup set), so Dapr NACKs and redelivers forever; asymmetric with `SkippedUnknownEventType` which returns 200/drop. Mitigated only by external dead-letter routing (`deadletter.folders.events` is declared intent; runtime wiring is "supplied outside this repository"). Decide: for permanent failures, drop with 200 + record a metadata-only `failed` bridge status, vs rely on dead-letter routing being guaranteed in deployment. (AC7.) [src/Hexalith.Folders.Workers/FoldersWorkersModule.cs:124-127; FoldersSemanticIndexingEventProcessor.cs:50-71]

### Patch

- [x] [Review][Patch] In-process `_processedMessageIds` dedup is dead — processor is `Transient`, so every Dapr delivery gets a fresh empty set; it never suppresses cross-delivery redelivery and gives a false sense of idempotency (real idempotency is the bridge fingerprint/watermark). Remove it, or make it a bounded `Singleton`. [src/Hexalith.Folders.Workers/SemanticIndexing/FoldersSemanticIndexingEventProcessor.cs:22; FoldersWorkersModule.cs:79]
- [x] [Review][Patch] `DeriveIdempotencyKey` is computed and carried on `SemanticIndexingRequest.IdempotencyKey` but never read at egress (the port dedups via `cloudevent.id` = source URI). Remove the dead field or wire it as publish dedup metadata. [src/Hexalith.Folders.Workers/SemanticIndexing/SemanticIndexingProcessManager.cs:169,228-236]
- [x] [Review][Patch] `SourceFrom` throws `ArgumentException` for a non-absolute source URI inside the allowed/materialized index-build path instead of recording a `failed`/`reconciliation_required` bridge status; would join the poison-loop family if reachable. Currently defensive only (canonical `SourceUri` is always `folders://…`). [src/Hexalith.Folders.Workers/SemanticIndexing/SemanticIndexingProcessManager.cs:215-220]
- [x] [Review][Patch] Redundant/confusing size guard: `expectedLength = ByteLength ?? ObservedByteLength` already folds `ObservedByteLength`, so `expectedLength > Max || ObservedByteLength > Max` only differs when both are non-null with `ByteLength <= Max < ObservedByteLength`. Compare the raw fields explicitly to express "reject if EITHER exceeds". [src/Hexalith.Folders.Workers/SemanticIndexing/FailClosedSemanticIndexingPolicyEvaluator.cs:38,46-47]
- [x] [Review][Patch] Nit: `Hash()` uses `.ToLower(CultureInfo.InvariantCulture)` on uppercase ASCII hex — the lone outlier; every other `Convert.ToHexString` site uses `.ToLowerInvariant()`. Output/allocation-identical; align for consistency. [src/Hexalith.Folders.Workers/SemanticIndexing/SemanticIndexingProcessManager.cs:257]

### Dismissed (verified false-positive / out-of-scope; 16)

Stale-result protection already enforced by the bridge store fingerprint/watermark (F6, F25, F29); generic-exception dedup eviction is intentional for transient redelivery (F8); in-memory dedup volatility is moot once the dead dedup is removed (F9, F21, F27); materialized content size IS re-validated in `ProcessEntryAsync` and null length → `content_descriptor_unavailable` (F11, F12); non-caller-token `OperationCanceledException` is handled by the port (F13); `cloudevent.id` = source URI is stable (F17); overstated variants of the policy-freshness finding (F19, F22); curated reason-code/classification strings are metadata-safe enums (F23); removal/reconcile on commit-fail/archive is Story 10.4 scope (F26); non-Dapr publish exceptions are unexpected programmer errors that may fail loudly per AC7 (F28).
