---
baseline_commit: 1ea2c61
story_key: 10-8-real-produce-index-authorize-hydrate-redact-search-round-trip
---

# Story 10.8: Real produce/index/authorize/hydrate/redact/search round trip

Status: ready-for-dev

Creation note: Ultimate context engine analysis completed - comprehensive developer guide created.

<!-- Per the approved story-authority rule, this dedicated file is Story 10.8 authority. The planning manifest and canonical Epic 10 synchronization remain Task 0 governance gates. -->

> [!IMPORTANT]
> **Load-bearing decisions for this story (read before implementation):**
>
> 1. **FR58 is metadata-token recall, not body-content search.** The finalized PRD limits this release to approved C9 metadata derived from mutations. Raw paths, file bodies, snippets, source URIs, and cross-workspace indexed body recall are forbidden. Story 10.9 is a separately approved, Security + PM-gated future capability and is not a prerequisite for 10.8.
> 2. **This is the non-empty production proof.** Evidence must start with an authenticated public Folders file mutation and traverse the deployed durable EventStore, Workers policy and materializer, Dapr pub/sub, Memories folders-index, the deployed EventStore-backed bridge, and the public Folders search/status facade. Direct index seeding, a hand-published domain envelope, fake authorization, a mocked gateway, or querying Memories directly cannot satisfy this story.
> 3. **Ready-for-dev means context-ready, not gate-free. Do not start implementation until Task 0 is green.** Story 10.7; Stories 12.1, 12.2, 12.3, and 12.5; a production-populated effective-permissions projection; OQ1–OQ4; cleared CI/production-start blockers; and a DCP-capable lane are hard prerequisites. A missing prerequisite is a blocker to record, not permission to seed or bypass it.
> 4. **Authorization has two fail-closed phases.** Request-scope JWT, authoritative tenant freshness, folder ACL/effective permissions, workspace scope, EventStore authorization, and Dapr policy finish before Memories egress. After authorized candidate retrieval, validated opaque identity, exact-scope trimming, and current bridge/path-policy hydration finish before a candidate is counted or returned. Memories is an untrusted shared index, not an authorization authority.
> 5. **OQ5 is a governed exit criterion.** Passing tests is necessary but insufficient. The evidence artifact must carry a version/digest plus dated Product, Security, and Test approvals.

## Story

As a developer or AI-agent consumer using the Folders REST, SDK, CLI, or MCP surface,
I want a real authorized folder mutation to produce a durable, searchable metadata-index result that is security-trimmed, hydrated against current Folders authority, and redacted to metadata-only,
so that I can discover current indexed folder information without false-empty behavior, stale results, cross-tenant disclosure, or sensitive-content leakage.

## Context and authority

The approved 2026-07-15 correction registers Story 10.8 as the FR58 completion story, but epics.md still stops Epic 10 at Story 10.6 and still carries superseded ownership and scope. Under the approved dedicated-story authority rule, this file supplies the missing Story 10.8 definition; the planning manifest and canonical artifact synchronization still must complete before implementation acceptance. Its precedence is:

1. Finalized PRD FR58 and OQ5.
2. Approved 2026-07-15 implementation-readiness correction.
3. Current architecture security, durability, Contract Spine, and production Dapr invariants.
4. Implemented hybrid lifecycle semantics: file removal hard-deletes the index row; folder archive re-sends the document with folders.status=archived and active search excludes it.

The older 2026-07-14 sequence made body-content materialization a predecessor of FR58 completion. The finalized PRD explicitly supersedes that interpretation: current FR58 is indexed metadata-token recall, while live-workspace body search remains FR34–FR35 and cross-workspace indexed body recall is future work.

## Acceptance Criteria

1. **Prerequisite gate is explicit and fail-closed.** Before production-path work begins, record concrete evidence that Story 10.6 metadata materialization; Story 10.7 deployed durable bridge registration; Stories 12.1 durable repository/event replay, 12.2 durable projections/task completion, 12.3 durable workspace content store/read source, and 12.5 at-least-once indexing egress/reconciliation; a production-populated effective-permissions projection; OQ1–OQ4 decisions; cleared CI/production-start blockers; canonical planning/manifest synchronization; and a DCP-capable execution lane are available. The accepted public mutation must emit the durable WorkspaceFileMutationAccepted event consumed by Workers. If any condition is absent, stop and keep Story 10.8 incomplete; no test-only seeding, in-memory ACL replacement, fake gateway, direct event injection, or skipped lane is an alternate pass.

2. **A real authenticated mutation produces a durable index entry.** Given a uniquely identified tenant, organization, folder, workspace, authorized actor, and searchable approved metadata token, an authenticated AddWorkspaceFile or ChangeWorkspaceFile request traverses REST → gateway/processor → durable EventStore → folders.events → folders-workers → layered authorization → MetadataDerivedSemanticIndexingContentMaterializer → SearchIndexEntryChanged → Dapr pub/sub → Memories folders-index. The bridge records Indexed for the resulting opaque file-version identity. The proof uses deployed production registrations and contains no direct SearchIndexEntryChanged seed and no hand-published WorkspaceFileMutationAccepted envelope.

3. **The deployed public facade returns a non-empty hydrated result and truthful status.** SearchFolderIndexedFiles returns at least one authorized metadata-only result for the mutation through the deployed Folders Server, and GetFolderIndexingStatus reports the corresponding real indexed state and freshness. The handler recovers only opaque identity from the untrusted index hit and hydrates current state from the durable Folders-owned bridge; it does not trust Memories snippets, attributes, or state as authorization evidence. The returned search result and status refer to the same current file version.

4. **Authorization completes in two explicit phases.** Before egress, request-scope JWT validation/claim transformation, authoritative tenant projection freshness, folder ACL/effective-permissions evidence, requested workspace scope, EventStore authorization, and production Dapr policy all pass. Only then may Memories return candidates. Before counting or returning each candidate, the facade validates its opaque identity, exact tenant/organization/folder/workspace scope, and current bridge/path-policy authority. Wrong-tenant, wrong-organization, wrong-folder, wrong-workspace, revoked-principal, denied-ACL, hidden, and nonexistent probes yield stable non-disclosing outcomes. Tests prove request-level denial causes no Memories egress and neither phase reveals raw candidate counts, timing classifications, index existence, source identity, or hidden-resource existence.

5. **Hydration and lifecycle pruning use current authority.** Foreign-scope, malformed-identity, hydration-miss, Stale, Tombstoned, Skipped, Failed, ReconciliationRequired, Unknown, revoked, unauthorized, and hidden candidates never leave the active search facade. Current code that treats Stale as visible is corrected and covered. File removal publishes SearchIndexEntryRemoved and converges the bridge to the canonical tombstoned lifecycle while making the entry unsearchable; folder archive is not a bridge enum and instead preserves the indexed record with folders.status=archived while active search excludes it. No stale result survives merely because Memories still holds an older document.

6. **At-least-once delivery, recovery, restart, and replay converge.** Duplicate folder events, duplicate index publications, broker/Memories transient failure, and a crash between publication and bridge-result persistence converge idempotently without requiring a later unrelated mutation. A failed remove cannot leave a document searchable after recovery. Empty-checkpoint rebuild, process restart, and ordered replay reproduce tenant-isolated bridge/search/status state with stable identities and fingerprints. Story 10.8 verifies these guarantees end to end; it does not reimplement Story 10.7 or 12.5.

7. **Unavailable is explicit and fail-safe.** After authorization, unavailable bridge, unavailable Memories/index, timeout, malformed remote result, or hydration failure returns the canonical ReadModelUnavailable/degraded outcome with safe reason, correlation identity, retry eligibility/client action, and no sensitive evidence. An unavailable bridge is detected before external search; it must not degrade to HTTP 500, a false healthy empty result, or a false current/indexed status. Authorization failures still take precedence and must not expose dependency availability.

8. **C9 metadata-only boundary holds everywhere.** Curated index text/attributes, public responses, bridge evidence fields, audit, logs, traces, metrics, errors, and the evidence document contain only the approved metadata-token vocabulary, opaque authorized identity, classifications, status, freshness, and safe availability evidence. The one allowed URI is the sanitized opaque folders:// identity used internally as stable cloudevent.id/index SourceUri and parsed only to recover candidate identity; it never appears in curated text/attributes, public responses, audit/logs/traces, or evidence. No boundary contains a raw path, file body, content bytes, snippet, query secret, provider payload, token/credential, Memories internal identity, or hidden-resource existence. The repository leakage corpus/sentinel scan covers positive, negative, retry, and failure paths.

9. **Contract bounds and cross-surface parity are proven.** Before implementation, Product + Architecture freeze query grammar, token normalization, match semantics, deterministic ordering/tie-breaking, snapshot/cursor consistency, bounded post-authorization paging, and opaque-identity stability in the Contract Spine. Both FR58/C13 operations then preserve correlation/task sourcing, read-consistency/freshness semantics, canonical errors, C4 result/response/time limits, filter-before-truncate behavior, cursor/truncation rules, and read-side rejection of Idempotency-Key. REST, generated SDK, CLI, and MCP expose the finalized search and indexing-status behavior from the same Contract Spine. The current CLI indexing-status gap is closed or an explicit canonical authority correction is approved before acceptance. The provisional semantic_reference_pending query-family identifier is not renamed in one layer; any finalization is a governed Contract Spine change regenerated across OpenAPI, SDK, parity, adapters, and tests.

10. **OQ5 evidence is complete and governed.** Create docs/exit-criteria/fr58-search-evidence.md containing environment and component versions, baseline commit, non-skipped command/run identity, sanitized non-empty search/status evidence, authorization/isolation negatives, stale/archive/removal evidence, unavailable and recovery evidence, duplicate/restart/empty-checkpoint replay evidence, parity and performance results, leakage-scan results, artifact version/digest, and dated Product Manager, Security, and Test approvals. Green automated tests without these approvals do not close OQ5.

11. **The live topology criterion cannot pass by skipping.** The dedicated DCP-capable run boots the required AppHost resources, executes the real authenticated round trip, and finishes successfully. The ordinary hermetic lane may retain its opt-in skip behavior, but the governed evidence run must assert that the FR58 scenario executed rather than skipped. BLOCKED-PENDING, re-carried evidence, a direct-seed component diagnostic, or a topology-only boot is not completion evidence.

12. **Functional bounds and production policy remain within the approved envelope.** One linked 2-second server budget starts when request authorization starts and covers authorization, source paging, hydration, filtering, serialization, and response shaping; internal expiry maps to query_timeout while caller cancellation still propagates. Search returns no more than 500 post-authorization results and 1,048,576 serialized bytes and filters before truncation. The OQ5 artifact records raw search/status timing observations with environment, population, warm-up, repetitions, concurrency, and percentile method, but does not claim p95 release calibration before OQ10 freezes that methodology. Production Dapr remains deny-by-default: Folders Server may invoke Memories only for GET /api/search, Workers remain pub/sub-only, and no local permissive AppHost policy is presented as production-policy evidence.

## Tasks / Subtasks

- [ ] Task 0 — Prove prerequisites and freeze the executable test contract (AC 1, 9, 11)
  - [ ] Confirm the planning story manifest exists, canonical Epic 10 includes Stories 10.7–10.9, and the approved planning synchronization/consistency gates pass while retaining this dedicated file as Story 10.8 authority.
  - [ ] Confirm OQ1–OQ4 are closed with governed decisions; explicitly apply OQ2 file policy and OQ3 authorization-denominator decisions to the mutation/search scenario.
  - [ ] Confirm Story 10.6 is reconciled complete and its emitted metadata vocabulary is the approved FR58 subset. Resolve whether media type and path-policy outcome must be added before using them as search tokens.
  - [ ] Confirm Story 10.7 registers a durable, tenant-isolated, freshness-aware EventStore-backed ISemanticIndexingBridgeReadModel in deployed Server composition and supports deterministic empty-checkpoint replay.
  - [ ] Confirm Stories 12.1, 12.2, and 12.3 provide the durable repository/event replay, projection/task completion, and workspace content/read substrate required by the public file-mutation route. Confirm that route emits durable WorkspaceFileMutationAccepted events. Current FolderDomainProcessor maps accepted results to PayloadNoOpDomainResult; do not compensate in 10.8 with a hand-published envelope.
  - [ ] Confirm Story 12.5 provides retry/reconciliation after a retryable Dapr/Memories publication result. Current processing acknowledgement must not lose a failed index/remove intent.
  - [ ] Identify and verify the production owner and population path for effective-permissions/ACL evidence. Current deployed defaults are in-memory and no source production path calls Save; test-only seeding is not acceptable.
  - [ ] Identify and verify the production IEventStoreAuthorizationValidator implementation and registration. Current deployed composition defaults to DenyAllEventStoreAuthorizationValidator; an AllowingEventStoreAuthorizationValidator or test-only override is not production-path evidence.
  - [ ] Confirm existing CI and production-start blockers are cleared before treating any topology result as acceptance evidence.
  - [ ] Confirm the DCP-capable lane from Story 11.15 (or an explicitly reassigned owner) can run authenticated tests and fail on a skipped FR58 scenario.
  - [ ] Obtain a governed Product + Architecture Contract Spine decision for query grammar, token normalization, match semantics, deterministic ordering/tie-breaking, snapshot/cursor consistency, bounded post-filter paging, and opaque-identity stability.
  - [ ] Reconcile the PRD requirement for indexing status through CLI with the current surface metadata, freeze whether GetFolderIndexingStatus is non-task-scoped or caller-task-sourced across OpenAPI/MCP/parity, and decide the Contract Spine disposition of semantic_reference_pending without a partial rename.
  - [ ] Record prerequisite commits/evidence in the Dev Agent Record. If any prerequisite is absent, stop implementation and leave the story incomplete.

- [ ] Task 1 — Correct facade freshness and unavailability semantics (AC 3, 5, 7)
  - [ ] Update src/Hexalith.Folders/Queries/ContextSearch/ContextSearchQueryHandler.cs so Stale is not facade-visible under current FR58.
  - [ ] After successful authorization and before Memories egress, return the canonical unavailable/degraded result when the authoritative bridge is unavailable.
  - [ ] Preserve authorization-first behavior: no availability probe or remote egress may precede authorization.
  - [ ] Extend tests/Hexalith.Folders.Tests/Queries/ContextSearch/ContextSearchQueryHandlerTests.cs for Stale exclusion, unavailable bridge, authorization precedence, hydration miss, lifecycle states, and metadata-only errors.
  - [ ] Verify the endpoint maps the canonical unavailable result to the existing 503 Contract Spine response without introducing raw provider detail.

- [ ] Task 2 — Build the no-seed production-path AppHost scenario (AC 2, 3, 4, 8, 11)
  - [ ] Add a focused Tier-3 real-roundtrip test under tests/Hexalith.Folders.AppHost.Tests using one public type per file and unique ULID identities.
  - [ ] Extend AspireFoldersAppHostFixture only as needed to enable a real authenticated/test-issued principal and current tenant/ACL projection evidence without bypassing LayeredFolderAuthorizationService.
  - [ ] Perform a real AddWorkspaceFile or ChangeWorkspaceFile call through the public Folders REST endpoint with approved searchable metadata.
  - [ ] Poll only through public Folders search/status operations until bounded convergence; do not query Memories directly for acceptance.
  - [ ] Assert the result is non-empty, current, hydrated, correctly scoped, metadata-only, and correlated to the real durable mutation and bridge status.
  - [ ] Preserve EventStoreSidecarShouldPublishFolderEnvelopeToWorkerSubscriberTopic and SeedRemoveAndArchiveRoundTripAgainstFoldersIndex as narrower routing/index component diagnostics; rename or document them so they cannot be mistaken for Story 10.8 acceptance.
  - [ ] Remove stale fail-closed-materializer comments from the fixture and tests/Hexalith.Folders.AppHost.Tests/README.md.

- [ ] Task 3 — Prove security trimming, hydration, lifecycle, and redaction (AC 4, 5, 7, 8)
  - [ ] Add same-tenant positive and wrong-tenant/organization/folder/workspace negative controls with an egress observer proving denial occurs before Memories lookup.
  - [ ] Cover revoked actor, denied ACL, hidden/nonexistent resource, malformed SourceUri, foreign injected index row, hydration miss, and every non-visible bridge status.
  - [ ] Drive a newer file version and prove the older/stale candidate is dropped.
  - [ ] Drive real file removal and folder archive; prove hard removal and active-search archive exclusion while status evidence remains truthful.
  - [ ] Inject safe dependency outages/timeouts after authorization and verify explicit canonical unavailable behavior; verify an unauthorized caller cannot distinguish those outages.
  - [ ] Scan response, audit, logs, traces, emitted events, and recorded evidence with tests/fixtures/audit-leakage-corpus.json and targeted raw-path/body/snippet/source-URI sentinels.

- [ ] Task 4 — Prove durability, duplicate safety, recovery, and performance (AC 6, 12)
  - [ ] Exercise duplicate domain-event delivery and duplicate Memories publication; assert one stable current result/status and no duplicate disclosure.
  - [ ] Exercise broker/Memories failure and recovery, including remove recovery, without a later unrelated mutation.
  - [ ] Restart relevant processes and rebuild the bridge from an empty checkpoint; compare deterministic status/search identities, classifications, fingerprints, and tenant isolation.
  - [ ] Implement the governed bounded paging/over-fetch algorithm: advance the opaque remote cursor across bounded source pages until the authorized post-hydration result limit is filled, the source is exhausted, or the fixed candidate/time/byte budget is reached. Never expose raw candidate counts. Cover pages containing only malformed, stale, foreign, archived, or hydration-miss rows plus stable ordering/cursor continuation under concurrent updates.
  - [ ] Create one linked 2-second cancellation budget at authorization entry and pass it through authorization, source paging, bridge hydration, filtering, serialization, and shaping. Distinguish internal expiry as query_timeout from caller cancellation, which must propagate. Add boundary tests for expiry in authorization, source paging, hydration, and shaping.
  - [ ] Enforce the 500-result/1-MiB functional search bounds and record raw search/status timings with environment, population, warm-up, repetitions, concurrency, and percentile calculation for later OQ10 calibration; do not label these observations calibrated p95 evidence.
  - [ ] If sequential per-hit hydration misses the fixed limit, implement a bounded parallel or batch strategy without weakening authorization, stable ordering, bounds, cancellation, or fail-safe behavior.

- [ ] Task 5 — Close REST/SDK/CLI/MCP contract parity (AC 9)
  - [ ] Add the missing CLI indexing-status query in src/Hexalith.Folders.Cli/Commands/Context/ContextCommand.cs unless Task 0 records an approved canonical scope correction. If the existing non-task-scoped OpenAPI/MCP contract wins, use CommandFactory.Query with taskIdRequired:false and folder/freshness only—no workspace, body, task, or idempotency option.
  - [ ] Cover search and status in tests/Hexalith.Folders.Cli.Tests/CommandSurfaceE2ETests.cs and retain metadata-only rendering and no Idempotency-Key option.
  - [ ] Verify REST, generated SDK, CLI, and MCP use the same operation contracts, sourcing headers, freshness, errors, and response semantics.
  - [ ] If query-family or surface metadata changes, edit the Contract Spine/OpenAPI source and generator inputs, regenerate the client, parity fixture, and adapter metadata, and update contract tests together. Never hand-edit src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs.

- [ ] Task 6 — Produce governed OQ5 evidence (AC 10, 11, 12)
  - [ ] Create docs/exit-criteria/fr58-search-evidence.md with the required sanitized evidence matrix and explicit statement that the real scenario ran rather than skipped.
  - [ ] Record environment/version identifiers, baseline and prerequisite commits, command/run identity, performance measurements, safety scan, replay/recovery result, and artifact digest.
  - [ ] Obtain and record dated Product Manager, Security, and Test approvals. Do not mark the story done before all three exist.
  - [ ] Update tests/Hexalith.Folders.AppHost.Tests/README.md and any cited Dapr conformance documentation so the evidence description matches deployed behavior.

- [ ] Task 7 — Run the complete verification matrix (AC 1–12)
  - [ ] In local project-reference development, restore and build Hexalith.Folders.slnx in Debug with no restore on the build and zero warnings/errors. Release/package-reference validation belongs to the governed CI/CD lane.
  - [ ] Run the focused Core, Workers, Server, Integration, CLI, MCP, Client, Contracts, Testing, and AppHost test projects individually; do not use a solution-level test shortcut.
  - [ ] Run contract-spine/parity, production Dapr-policy conformance, format/analyzer, and metadata-leakage gates.
  - [ ] Run the DCP lane with HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION=true and verify the Story 10.8 acceptance scenario executed and passed rather than skipped.
  - [ ] Attach governed evidence and re-check every prerequisite and approval before changing Status to done.

## Dev Notes

### Current implementation state

The repository already contains most component slices, but the deployed end-to-end path is intentionally incomplete:

| Area | Current evidence | Story 10.8 treatment |
| --- | --- | --- |
| Metadata materializer | MetadataDerivedSemanticIndexingContentMaterializer emits deterministic metadata-only text and shared security-trim attributes. | Reuse and verify; do not add body content. Resolve the approved token subset in Task 0. |
| Durable public mutation | FolderAggregate can emit WorkspaceFileMutationAccepted, but deployed FolderDomainProcessor currently converts accepted FolderResult values to an eventless PayloadNoOpDomainResult. | Prerequisite; never hide this with event injection. |
| Effective permissions | AddFoldersLayeredAuthorization defaults to InMemoryEffectivePermissionsReadModel and production code does not populate it. | Unowned hard prerequisite requiring a durable population path. |
| EventStore authorization | AddFoldersLayeredAuthorization defaults to DenyAllEventStoreAuthorizationValidator and deployed composition has no production allow-capable override. | Hard prerequisite requiring a production implementation/registration and real authorization evidence. |
| Search bridge | Core and deployed Server retain UnavailableSemanticIndexingBridgeReadModel; Workers own an EventStore store. | Story 10.7 must relocate/register the durable implementation before 10.8. |
| Retry/reconciliation | A retryable port result can still be acknowledged by the event processor. | Story 12.5 prerequisite; 10.8 proves recovery. |
| Search facade | Authorizes before source query, re-trims scope, hydrates through the bridge, discards snippets. | Correct Stale visibility and explicit bridge-unavailable behavior in Task 1. |
| Tier-3 test | Current tests either hand-publish a domain envelope without an outcome or seed SearchIndexEntryChanged and query Memories directly. | Preserve as diagnostics and add a distinct public no-seed acceptance scenario. |
| Surface parity | REST/SDK/MCP expose search and status; CLI exposes index-search but not indexing-status. | Close or obtain explicit authority correction. |

### Required production flow

Authenticated Folders mutation
→ durable Folders domain event
→ Dapr folders.events
→ Workers re-authorization and metadata materialization
→ durable/reconcilable SearchIndexEntryChanged publication
→ Memories folders-index
→ EventStore-backed Folders bridge
→ authorized Folders search
→ current-state hydration and metadata-only redaction
→ REST / SDK / CLI / MCP result and truthful indexing status

Every arrow is part of the proof. Component-level substitutes remain useful diagnostics but do not satisfy the story.

### Security and architecture guardrails

- Preserve two phases: request-scope JWT → authoritative tenant claim → tenant freshness → folder ACL/workspace → EventStore authorization → Dapr policy before egress; then validated identity → exact scope → current bridge/path-policy hydration before counting or returning each candidate.
- The shared folders-index physical tenant does not isolate managed tenants. The only acceptable security boundary is Folders-side authorization, exact per-hit scope trimming, and current authoritative hydration.
- Recover identity only from the validated folders:// SourceUri. Its sole role is internal stable CloudEvent/index identity; never put it in curated text/attributes, echo it to callers/evidence, or trust remote snippet/text/attributes as authority.
- Production Dapr is deny-by-default. Preserve only folders → memories GET /api/search; Workers publish events and receive no Memories invocation permission.
- Derived indexing failure must not roll back the durable folder mutation. It must produce truthful retry/reconciliation evidence and eventually converge.
- Stable CloudEvent identity and composite tenant/aggregate upserts must remain replay-safe.
- Redacted, hidden, missing, stale, and unavailable states must not collapse into a misleading success. Expose distinctions only where the caller is authorized to observe them.
- No UI content-preview or new body-search surface is in scope. Preserve the read-only metadata/status UX contract.

### Scope boundaries

**In scope**

- A mandatory deployed no-seed FR58 round trip and its governed evidence.
- Fixing Stale visibility and bridge-unavailability semantics in the Folders facade.
- Security, lifecycle, duplicate, recovery, restart/replay, performance, parity, and leakage verification.
- CLI indexing-status parity if canonical authority remains unchanged.
- Narrow defects discovered inside already-owned 10.8 facade/evidence behavior.

**Out of scope**

- Body-content indexing, indexed snippets, indexed body recall, or RAG IngestAsync.
- Story 10.7 bridge implementation or Story 12.5 outbox/reconciler implementation.
- Inventing a test-only effective-permissions population path.
- Broad EventStore mutation-substrate work owned by Epic 12.
- Weakening production Dapr policy or using local allow-by-default policy as security evidence.
- Direct Server → Workers references, direct Server → Memories URL calls, or generated-client hand edits.
- UI/file-browser/content-preview scope.

### Likely file impact

**Update**

- src/Hexalith.Folders/Queries/ContextSearch/ContextSearchQueryHandler.cs
- tests/Hexalith.Folders.Tests/Queries/ContextSearch/ContextSearchQueryHandlerTests.cs
- tests/Hexalith.Folders.AppHost.Tests/AspireFoldersAppHostFixture.cs
- tests/Hexalith.Folders.AppHost.Tests/FoldersTopologyCrossProcessTests.cs, only to preserve/relabel its narrower diagnostics if a focused new file is added
- tests/Hexalith.Folders.AppHost.Tests/README.md
- src/Hexalith.Folders.Cli/Commands/Context/ContextCommand.cs, subject to Task 0 authority reconciliation
- tests/Hexalith.Folders.Cli.Tests/CommandSurfaceE2ETests.cs
- Contract Spine/OpenAPI, parity, adapter, and generated artifacts only as one governed change if Task 0 requires it
- docs/operations/dapr-policy-conformance.md only if it remains stale and is cited by OQ5 evidence

**New**

- A focused real-roundtrip test file under tests/Hexalith.Folders.AppHost.Tests
- docs/exit-criteria/fr58-search-evidence.md
- A focused evidence/conformance test only if required to keep OQ5 structure and approvals machine-checkable

Do not modify prerequisite-owned bridge, effective-permissions, mutation-substrate, or reconciler code merely to make a 10.8 test green. Re-home a newly discovered prerequisite defect explicitly, update this story authority, and then resume.

### Update-file instructions and reuse points

| File | Current state | Change | Preserve / reuse |
| --- | --- | --- | --- |
| src/Hexalith.Folders/Queries/ContextSearch/ContextSearchQueryHandler.cs | Authorizes before egress, validates bounds, trims exact scope, hydrates sequentially, and emits metadata-only results; IsVisible currently accepts Indexed and Stale and bridge availability is not checked before source search. | Accept only Indexed for active recall and add an authorization-first bridge-availability failure. Optimize hydration only if governed measurements require it. | Safe-denial precedence, exact scope comparisons, cancellation, cursor/raw-row handling, response budget, redaction mapping, and metadata-only logging. |
| tests/Hexalith.Folders.Tests/Queries/ContextSearch/ContextSearchQueryHandlerTests.cs | Already proves auth-before-source, scope poisoning defenses, hydration misses, most non-live statuses, redaction, source failures, cursor behavior, response bounds, and metadata-only serialization. Its StubBridgeReadModel is always available and Stale is not in the non-live theory. | Make bridge availability configurable; add Stale and unavailable/authorization-precedence cases. | Reuse Handler, Query, Hit, Entry, RecordingFolderSearchSource, and existing safe-denial/bounds assertions instead of creating a parallel harness. |
| tests/Hexalith.Folders.AppHost.Tests/AspireFoldersAppHostFixture.cs | Opt-in Aspire.Hosting.Testing fixture boots six resources, waits for Running, snapshots environment, and disposes cleanly; it disables Keycloak and has stale fail-closed-materializer prose. | Add the smallest authenticated test configuration and any genuinely required resource readiness support; correct the prose. | DistributedApplicationTestingBuilder lifecycle, opt-in hermetic behavior, environment restoration, startup timeout, and async cleanup. |
| tests/Hexalith.Folders.AppHost.Tests/FoldersTopologyCrossProcessTests.cs | Contains topology/route smoke plus a direct SearchIndexEntryChanged → Memories lifecycle diagnostic with bounded eventual polling and Dapr sidecar resolution. | Relabel stale comments and keep these component proofs distinct; add the acceptance scenario in a focused file unless shared helpers must be extracted. | WaitForHitCountAsync and sidecar endpoint-resolution patterns where appropriate; never promote direct seeding or direct Memories query to acceptance evidence. |
| tests/Hexalith.Folders.AppHost.Tests/README.md | Accurately documents opt-in DCP operation but incorrectly says production materialization is still fail-closed. | Document the metadata materializer, prerequisites, governed non-skipped FR58 command, and diagnostic-versus-acceptance distinction. | Existing run command and honest description of hermetic skips. |
| src/Hexalith.Folders.Cli/Commands/Context/ContextCommand.cs | Thin System.CommandLine adapter exposes index-search with folder/workspace/freshness/body and required task sourcing; no indexing-status subcommand. | Add indexing-status as a query through generated IClient if Task 0 confirms the PRD authority. | CommandFactory.Query, shared options/parsing, thin-adapter behavior, metadata-only rendering, and absence of idempotency input. |
| tests/Hexalith.Folders.Cli.Tests/CommandSurfaceE2ETests.cs | Uses CliTestHarness plus an NSubstitute IClient to prove parsing, SDK delegation, exit code, and correlation propagation without network infrastructure. | Add focused index-search/indexing-status parse-and-delegate coverage. | Existing harness and Received assertions; do not create a second CLI host or network test. |

Additional reusable patterns:

- tests/Hexalith.Folders.IntegrationTests/ContextSearch/ContextSearchFacadeWiringTests.cs for REST safe-denial, controlled egress, and metadata-only response assertions below Tier 3.
- tests/Hexalith.Folders.Workers.Tests/MetadataDerivedSemanticIndexingContentMaterializerTests.cs for real materializer-to-port attribute and leakage assertions.
- tests/Hexalith.Folders.Workers.Tests/SemanticIndexingEndpointE2ETests.cs for real endpoint composition with boundary doubles; it is not a substitute for the deployed proof.
- Existing production-policy conformance fixtures for positive and negative Dapr triples. Do not duplicate policy parsing.

### Libraries and versions

Repository configuration, not stale planning prose, is authoritative:

- .NET SDK 10.0.301 and net10.0.
- Aspire.Hosting.Testing 13.4.6.
- CommunityToolkit.Aspire.Hosting.Dapr 13.4.0-preview.1.260602-0230.
- Dapr.Client/Dapr.AspNetCore 1.18.4; the Dapr meta package remains 1.17.9.
- xunit.v3 3.2.2, Shouldly 4.3.0, NSubstitute 6.0.0-rc.1, Microsoft.NET.Test.Sdk 18.7.0, and Testcontainers 4.13.0.
- Central package management only; no inline package versions and no opportunistic upgrades.
- Nullable-safe, warnings-as-errors, deterministic builds, file-scoped namespaces, one primary type per file, and cancellation propagation.

### Testing strategy

Use the narrowest lane that proves each behavior, then run all affected projects:

- Hexalith.Folders.Tests: handler authorization, scope trim, stale/lifecycle pruning, explicit unavailable, bounds, redaction.
- Hexalith.Folders.Workers.Tests: policy/materializer/port/process/bridge duplicate and retry semantics.
- Hexalith.Folders.Server.Tests: Dapr-sidecar search composition and canonical endpoint mapping.
- Hexalith.Folders.IntegrationTests: public endpoint behavior with controlled boundary failures; useful below Tier 3 but not OQ5 completion.
- Hexalith.Folders.Client.Tests, Hexalith.Folders.Cli.Tests, and Hexalith.Folders.Mcp.Tests: contract sourcing and parity.
- Hexalith.Folders.Contracts.Tests and Hexalith.Folders.Testing.Tests: Contract Spine, generated surface, dependency, scaffold, and production Dapr-policy conformance.
- Hexalith.Folders.AppHost.Tests: the only acceptance lane for the deployed no-seed process-level proof.

Representative commands:

    dotnet restore Hexalith.Folders.slnx
    dotnet build Hexalith.Folders.slnx --configuration Debug --no-restore
    dotnet test tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --configuration Debug --no-build
    dotnet test tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj --configuration Debug --no-build
    dotnet test tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --configuration Debug --no-build
    dotnet test tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj --configuration Debug --no-build
    dotnet test tests/Hexalith.Folders.Client.Tests/Hexalith.Folders.Client.Tests.csproj --configuration Debug --no-build
    dotnet test tests/Hexalith.Folders.Cli.Tests/Hexalith.Folders.Cli.Tests.csproj --configuration Debug --no-build
    dotnet test tests/Hexalith.Folders.Mcp.Tests/Hexalith.Folders.Mcp.Tests.csproj --configuration Debug --no-build
    dotnet test tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --configuration Debug --no-build
    dotnet test tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj --configuration Debug --no-build
    HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION=true dotnet test tests/Hexalith.Folders.AppHost.Tests/Hexalith.Folders.AppHost.Tests.csproj --configuration Debug --no-build

The governed DCP run must explicitly record the Story 10.8 test as executed; a successful command containing only configured skips is a failure for this story.

The commands above are the local project-reference lane required by repository instructions. The CI/CD owner separately runs Release against package references; do not mix Release output with the local project-reference graph.

### Previous-story intelligence

- Story 10.6 implemented deterministic metadata-derived materialization and the exact shared folders.* identity/status keys needed for facade filtering. Preserve that behavior; its file and sprint status remain in-progress until the broader reconciliation gate is complete.
- The current AppHost route smoke proves only EventStore-sidecar → broker → Workers delivery and asserts no indexed outcome.
- The current seed/remove/archive test proves Dapr route → Memories lifecycle behavior but bypasses mutation, authorization, materialization, bridge hydration, public redaction, and Folders response contracts.
- The public search handler already performs authorization before source query and trims candidate scope before hydration. Do not regress this ordering while adding bridge availability handling.
- Story 10.6 and its review ledger explicitly record that the Tier-3 proof is deferred, the deployed accepted path is eventless, and production effective permissions are unpopulated. Treat _bmad-output/implementation-artifacts/10-6-replace-fail-closed-content-materializer-with-metadata-derived.md and _bmad-output/implementation-artifacts/deferred-work.md:473-477 as prerequisite evidence, not as completed 10.8 behavior.

### Git intelligence

- 1ea2c61 is the baseline and advances EventStore, FrontComposer, and Memories subproject references.
- da3d111 adds the finalized PRD/reconciliation documents that define metadata-only FR58.
- 0206bdd hardens the metadata materializer and tests.
- 776828d implements metadata-derived indexing content.
- b359bea fixes Dapr sidecar HTTP endpoint resolution in AppHost tests.
- 677d8d2 routes Server Memories search through its Dapr sidecar and records the unavailable bridge limitation.
- 9631820 hardens search-facade malformed-result, pagination, and bounds behavior.

Do not revert or overwrite the current user-owned changes in Story 10.6, the readiness report, its pre-rerun copy, or the July 15 proposal.

### Latest technical guidance

- Aspire.Hosting.Testing is the supported closed-box harness for starting the AppHost as a separate process, waiting for resource lifecycle states, creating resource HTTP clients, and disposing the distributed application. Use it for the real topology proof: https://learn.microsoft.com/dotnet/aspire/testing/overview and https://learn.microsoft.com/dotnet/aspire/testing/manage-app-host
- Dapr pub/sub provides at-least-once delivery. A subscriber failure/non-success causes redelivery, so handlers and upserts must be duplicate-safe; durable retry/reconciliation is still required where application code converts downstream failure into a successful acknowledgement: https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-overview/ and https://docs.dapr.io/developing-applications/building-blocks/pubsub/howto-publish-subscribe/
- Dapr service invocation is through the caller's local sidecar and the called application's ACL evaluates app identity, trust domain, namespace, operation, and verb. Preserve the production allowlist rather than bypassing it with a direct URL: https://docs.dapr.io/developing-applications/building-blocks/service-invocation/howto-invoke-discover-services/ and https://docs.dapr.io/operations/configuration/invoke-allowlist/

### Project-structure notes

- Root submodules only; never initialize nested submodules.
- Keep Memories dependencies within the approved boundary: Workers Contracts for publication and Server Client.Rest/Contracts for the search facade. Other projects remain Memories-free.
- Use IReadModelStore and ReadModelWritePolicy for Dapr state; do not call DaprClient.SaveStateAsync/DeleteStateAsync directly.
- Do not hand-edit generated Client code or parity output; use the repository generator.
- Preserve current user-owned worktree changes and avoid planning-artifact edits outside the new story and sprint tracking entry.

### References

- Product scope: _bmad-output/planning-artifacts/prd.md:370-377, 634, 665, 795-799, 905-913
- Story authority and ownership: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-15.md:274-312, 333-348, 366-394
- Registered sprint key: _bmad-output/implementation-artifacts/sprint-status.yaml:283-286
- Stale older sequence: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-14-implementation-readiness-structural-correction.md:432-444
- Architecture authorization and shared-index boundary: _bmad-output/planning-artifacts/architecture.md:100-101, 117-140, 171-176, 834-844
- Architecture safety, replay, and verification: _bmad-output/planning-artifacts/architecture.md:852-883
- UX metadata/status-only boundary: _bmad-output/planning-artifacts/ux-design-specification.md:118-145
- Deployed bridge defaults: src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs:144-160; src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:78-125
- Production effective-permissions default: src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs:241-252
- Eventless deployed accepted result: src/Hexalith.Folders.Server/FolderDomainProcessor.cs:1257-1276
- Search handler: src/Hexalith.Folders/Queries/ContextSearch/ContextSearchQueryHandler.cs:67-205, 286-306
- Memories gateway: src/Hexalith.Folders.Server/ContextSearch/MemoriesFolderSearchSource.cs:38-178
- Worker policy/order: src/Hexalith.Folders.Workers/SemanticIndexing/FailClosedSemanticIndexingPolicyEvaluator.cs:31-139
- Worker processing/replay identity: src/Hexalith.Folders.Workers/SemanticIndexing/SemanticIndexingProcessManager.cs:158-250, 348-367
- Production Dapr policy: deploy/dapr/production/accesscontrol.yaml:119-146; deploy/dapr/production/pubsub.yaml:12-21
- Current AppHost diagnostics: tests/Hexalith.Folders.AppHost.Tests/AspireFoldersAppHostFixture.cs:20-106; tests/Hexalith.Folders.AppHost.Tests/FoldersTopologyCrossProcessTests.cs:79-176
- Previous-story and deferred runtime evidence: _bmad-output/implementation-artifacts/10-6-replace-fail-closed-content-materializer-with-metadata-derived.md; _bmad-output/implementation-artifacts/deferred-work.md:473-477

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- To be completed by the implementing agent.

### Completion Notes List

- To be completed by the implementing agent.

### File List

- To be completed by the implementing agent.

## Change Log

| Date | Change | Author |
| --- | --- | --- |
| 2026-07-15 | Created implementation-ready Story 10.8 context from the finalized metadata-only FR58 and approved structural correction. | Administrator (via bmad-create-story) |
