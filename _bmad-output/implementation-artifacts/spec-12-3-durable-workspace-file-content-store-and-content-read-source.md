---
title: 'Story 12.3: Durable workspace content authority'
type: 'feature'
created: '2026-10-22'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '28513763ec5c0405ab942b1e5266a483573f451d'
story_key: '12-3-durable-workspace-file-content-store-and-content-read-source'
context:
  - 'AGENTS.md'
  - 'references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '_bmad-output/implementation-artifacts/epic-12-context.md'
  - 'docs/contract/file-context-contract-groups.md'
  - 'docs/governance/approval-policy.md'
  - '_bmad-output/implementation-artifacts/story-12-3-content-prerequisite-proposal-2026-10-22.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Inline bytes are discarded, streamed evidence is caller asserted, and production content stores/read sources are unavailable. Accepted metadata cannot establish durable file truth.

**Approach:** Consume an EventStore-owned authoritative body capability; bind verified bytes to event-sourced file versions. Use that authority for staging, deletion, restart recovery and bounded context reads.

## Boundaries & Constraints

**Always:** Preserve OQ2 1.1.0, current authorization before protected observation, task/lock/version fencing, exact bytes, metadata-only events and evidence, EventStore sole-writer ownership, and approved confidentiality rules. Apply change sets atomically. Keep valid historical event bytes unchanged.

**Never:** Persist bodies in events, projections or diagnostics; add Folders-owned database/blob/file persistence; infer truth from indexes or working copies; accept descriptor-only proof; auto-commit. Confidential operational values require Story 12.7 tokens or proven irreversible handles; unsupported execution fails before admission. This story does not implement Git execution, indexing, the full tokenizer, or public v2 exposure.

## I/O & Edge-Case Matrix

| Scenario | Input / state | Expected behavior | Failure |
| --- | --- | --- | --- |
| Stage/read | Authorized add/change and actual bytes | Verify hash, length/media, bind immutable version; same bytes after restart | OQ2 evidence/size routing |
| Replay/race | Equivalent/conflicting request; stale lock/version | Same logical result or conflict; no partial publication | Canonical conflict |
| Delete | Authorized removal | Tombstone hides bytes; replay cannot resurrect them | Governed physical deletion |
| Policy | Hidden path, traversal, link, alias, policy drift | Reject before disclosure/effect | OQ2 safe denial/503 |
| Bytes | Binary, invalid UTF-8, boundary sizes | Preserve mutation bytes; restrict reads to content-eligible files | OQ2 400/413/422 |
| Fault | Missing/corrupt body, timeout, cancellation, crash | No false acceptance or empty success; bounded recovery | Canonical unavailable |

## Approved Decisions

On 2026-10-22 Jerome said "approve both and continue" after reviewing the linked prerequisite proposal and this spec. This authorizes the generic EventStore content capability and Folders consumption, plus the proposal's explicit authoritative-body retention rule: preserve live references without automatic TTL, suppress removed content immediately, and physically delete only after the qualifying P7D epoch with fresh terminal/no-active/reference/hold checks. Unresolved publication stays protected; resume cancels the epoch; hold release requires a fresh epoch. No deployment, publication, dependency upgrade or version-control recording is authorized.

</frozen-after-approval>

## Code Map

- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`: inline decode is discarded; stream validation trusts descriptors; gateway payload is metadata only.
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceFileMutationService.cs`: reuse authorization, aggregate guards and staging order; content/delete requests lack version/fence/body bindings.
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs`: retain actor-folded state and transactional metadata-event decisions.
- `src/Hexalith.Folders/Queries/FileContext/IWorkspaceFileContextSource.cs`: existing context seam; no separate content-read interface exists. Handler already supplies authorization, paths and bounds.
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs`: replace unavailable content/delete/path/context registrations only with qualified implementations.

## Tasks & Acceptance

**Execution:**
- [x] `references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/`: after scope approval, deliver and qualify the generic capability described in the proposal before dependent Folders work; preserve unrelated submodule changes/pins.
- [x] `docs/exit-criteria/c3-retention.md`, `docs/operations/retention-and-tenant-deletion.md`: record the selected authoritative-body disposition separately from disposable caches.
- [x] `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`, `src/Hexalith.Folders.Client/Convenience/FileStreamStagingEvidence.cs`: ingest actual bounded bytes through supported transport and propagate only verified opaque receipts; synchronize affected contracts/generation.
- [x] `src/Hexalith.Folders/Aggregates/Folder/WorkspaceFileMutationService.cs`, `WorkspaceFileContentStoreRequest.cs`, `WorkspaceFileDeleteOperationStoreRequest.cs`: bind tenant/folder/workspace/task, file incarnation, lock fence and expected version; add SDK adapters and metadata events/folds for publication/removal.
- [x] `src/Hexalith.Folders/Queries/FileContext/`: implement authoritative context/path evidence, immutable policy snapshots, content classification and OQ2 bounds; register in production.
- [x] `tests/Hexalith.Folders.Tests/`, `tests/Hexalith.Folders.Server.Tests/`: exercise the matrix and unchanged denial/transport contracts.
- [x] `tests/Hexalith.Folders.IntegrationTests/WorkspaceContentProcessWiringTests.cs`, `tests/Hexalith.Folders.AppHost.Tests/WorkspaceContentDurabilityAcceptanceTests.cs` (new): drive real gateway/process/persistence, assert stored bytes, restart, replay, replica races, deletion/hold and fault windows; retain metadata-only evidence.

**Acceptance Criteria:**
- Given Story 12.1 streams and current authority/policy, when content is staged, read, restarted or replayed, then verified bytes and metadata remain authoritative with task/lock/version enforcement and deletion suppression.
- Given crash, conflicting replay, denial or dependency failure, when the real persistence path executes, then no partial change set or unauthorized body becomes visible.
- Given confidential operational values, when durable execution is evaluated, then only approved tokens/handles persist and unsupported cleartext-dependent work is rejected before admission.

## Implementation Notes

- 2026-10-22: Owner approved both prerequisite scope and retention and requested implementation in this session. The spec was reread unchanged before approval was recorded. Story 12.2 is not a direct graph dependency. Follow the separate upstream repository's guidance; never load skills from `references/`.

- 2026-10-22: Implemented and qualified the EventStore prerequisite before dependent Folders edits. The SDK owns physical bodies, publication recovery, content holds and governed erasure; Folders supplies current source authority and actor-serialized erasure grants. No dependency pin changed.
- 2026-10-22: Added actual bounded inline/multipart ingestion and a stream client helper, immutable file/version/fence bindings, atomic internal batches, authoritative context/path/policy adapters and strict byte classification. Historical event bytes and OQ2 remain unchanged; public v2 remains disabled. Explicit non-confidential policy is required; unsupported confidential execution fails closed.
- 2026-10-22: Added separate content administration contract/operations guidance and C3 authoritative-body retention. Qualified immediate suppression, hold/release, resumed-work epoch cancellation and real P7D physical deletion. Historical descriptor-only metadata does not manufacture durable body authority; legacy-body reconstruction is not claimed.
- 2026-10-22: Retained metadata-only commands/results, failed-attempt distinctions, source hashes and runtime identities in `docs/evidence/story-12-3-workspace-content/`. Final independent review and qualification are recorded below.

## Spec Change Log

- 2026-10-22: Updated execution/verification evidence only; the frozen human intent was preserved.

## Review Triage Log

| Finding | Verdict | Evidence | Route |
| --- | --- | --- | --- |
| B1 | high | WorkspaceFilePolicySnapshot concatenates label tokens and values without section counts; includes [**,exclude]/excludes [safe.txt] and includes [**]/excludes [exclude,safe.txt] yield the identical hashed sequence despite changed eligibility. | patch |
| B2 | high | UploadAsync checks the ledger before an awaited body read/create; CompleteErasure deletes that body key, allowing an already-authorized late create-only retry to recreate bytes before its final ledger check rejects. Erased disposition then returns without removing the restored bytes. | patch |
| B3 | medium | ApplyDispositionAsync reads pending publications but never calls RecoverPendingAsync. A retained rejected outcome remains pending until a separate read/recover call, so the advertised apply operation cannot start its eligible cleanup epoch. | patch |
| B4 | medium | The source-proven replay branch calls VerifyUploadAsync, whose Upload authorization requires the old task to be open and its lease unexpired. This blocks an equivalent retained replay before gateway idempotency handling. | patch |
| B5 | high | HasLiveReference places both latest-stage and committed-receipt checks under file.Active. Staging removal makes that identity inactive before committed references are replaced; current committed content is therefore misclassified as unreferenced. | patch |
| B6 | high | Production source emits result_limit for both page and cost truncation metadata; current contracts/converter enumerate result_count_limit, response_budget_limit and query_timeout, so bounded successful responses are invalid. | patch |
| B7 | false | The schemas accept only a service-issued prior cursor and make issuance optional, including for truncated pages; this implementation has issued none. No valid issued cursor is rejected. The documented bounded behavior asks callers to refine queries, so an added continuation implementation is not required by these optional fields. | rejected |
| B8 | high | The 416 branch depends on visible file length after awaited sensitivity work but precedes the final fresh authorization/source/policy checks used by successful responses; revocation or removal during that await can disclose a namespace-dependent outcome. | patch |
| B9 | high | The aggregate checks the lease before SDK preparation; later awaited authorization/path evidence and final batch preparation can pass the expiry instant without another lease decision before returned events. Layered reauthorization checks access rather than the lease clock. | patch |
| B10 | high | OQ2 declares sensitivity-denied paths restricted for mutations. Production path evidence checks configured globs but omits the registered sensitivity classifier that context reads honor, so a denied/unavailable classification can still pass ingress and body persistence. | patch |
| E1 | medium | Same verified old-task replay failure as B4: a retained matching publication still traverses Upload verification and its live-lease guard before replay admission. | patch; grouped with B4 |
| E2 | medium | ProcessMutateFilesAsync performs RootElement.TryGetProperty on valid JSON arrays/primitives/null outside a ValueKind guard; its catch handles JsonException only. The public processor entry regresses from structured invalid-shape rejection to InvalidOperationException. Normal canonical admission filters objects, but processor callers and malformed callback payload handling still require a DomainResult. | patch |
| E3 | medium | The batch precondition calls schema.GetString without checking String and the item loop calls AsObject on JsonNode.Parse(null) with no null guard. These valid JSON shapes throw outside the intended structured rejection path. | patch |
| V1 | medium | Pre-verified regression gap: endpoint/handler search and glob tests substitute a canned source; live content acceptance exercises range only. Neither production match condition is asserted against multiple real published files. | patch |
| V2 | high | Pre-verified regression gap: no test references CommittedContentReferences or protects receipt A after a bound commit and newer staged receipt B. Existing commit fixtures contain no content binding and SDK tests receive synthetic dispositions. | patch |
| VO1 | high | ValidateSearch allows only the old five metadata item fields, while production emits additive fileId/version. The reviewer executed built-client deserialization: identity-bearing response fails, otherwise identical response without these fields succeeds. | patch |
| VO2 | high | Production freshness uses decimal source head as projectionWatermark; OpaqueIdentifier requires at least 16 characters and the existing converter enforces it. The built client rejects watermark 15; range and collection successes share this producer. | patch |
| VO3 | high | Same verified undeclared truncation literal as B6. Built-client deserialization rejects result_limit; result count and response-byte bounds require their distinct declared reasons. | patch; grouped with B6 |
| P1 | medium | Parent follow-through: inline replay bytes are hashed by ValidateInlineContent, but actual multipart bytes are hashed only in the non-replay upload branch. A retained matching descriptor can therefore replay with different streamed bytes unnoticed. Verify every supplied multipart body before selecting the replay branch. | patch |
| P2 | medium | A valid legacy search item without optional identity/version deserializes successfully, but its generated nonnullable Version defaults to zero and serializes as version:0. The focused client roundtrip fails at ClientGenerationTests.cs:1375; the declared positive-version constraint then rejects that output. Omit only the optional CLR default through deterministic serialization attributes, preserving positive values and wire validation. | patch |

## Verification

The following original broad results describe the pre-audit source snapshot; current correction evidence follows below.

- Debug/source full solution restore and build passed with 0 warnings/errors (60-project graph; build 24.29s); no solution-level test run.
- Core: 2,218 total, 0 failed, 2 existing Forgejo operator skips. Server: 1,066/1,066; Client: 336/336; Contracts: 360/360; Folders EventStore host: 66/66. Focused process wiring: 1/1; grant/mapping: 9/9; NFR gate regression: 17/17. All focused/Server/Client/Contracts/host lanes had zero skips.
- Original final opted-in real AppHost class: 1/1, 0 failures/skips, 241.744s. Both mutation-serving Folders replicas, Workers and EventStore changed process identities. Actual Redis bodies were inspected. The retained race loser was `AppendConflict`, specifically mapped to canonical 409; one persisted version-1 winner was recorded. Hold/release, resume, P7D physical erasure, missing/corrupt/paused storage and atomic batches passed.
- At the original snapshot, all 30 distinct loaded runtime module paths matched its post-solution-build outputs. Upstream prerequisite: 11 protocol tests plus 1 Redis/Dapr live test, 0 failures/skips; all 33 qualified source/helper hashes matched that snapshot. Exact commands, revisions, source inventory, SHA/MVID/PDB identities, assertion markers and prior failures are retained in [Story 12.3 evidence](../../docs/evidence/story-12-3-workspace-content/README.md).
- Initial contract fixture/generator drift and the NFR stub's Release-artifact prerequisite were corrected; ordinary CI Release/package defaults and existence/execution-count checks remain enforced. One task-owned Aspire stop failure did not recur on the unchanged retry and remains recorded as a lifecycle flake.
- OQ2 SHA-256 remains `b7123536cc243c52e853980313ff8ebee5abb9936b331e71f30ad6d554975713`; the immutable historical v1 baseline remains unchanged. The source candidate manifest records pending v2 requalification and disabled production exposure; no release approval or packaged-consumption qualification is implied.

### Parent-audit correction qualification

- Durable unique CAS claims plus irreversible SDK deletion authorization prevent stale successful/failed cleanup visitors from reopening physical erasure. Before commitment, resumed/source-changed work cancels old proof and requires a fresh P7D epoch. Source grants permanently dispose the receipt against later references; unrelated new tasks remain permitted. The upstream guide and retention policy describe this boundary.
- Active schema/examples/generated models/converters require observed version and positive lock fence. Builders/helpers and CLI/MCP adapters reject missing/invalid observations safely. Private erasure requests validate null/canonical workspace, receipt, assigned task and generation. Production confidential inline/multipart endpoint regressions prove no persistence/admission and safe output.
- Sequential correction gates passed: upstream protocol16/16, real Redis/Dapr1/1, existing affected helper consumers49/49; Server58/58, Client342/342, CLI31/31, MCP15/15, Contracts360/360 and in-process wiring1/1; all zero failures/skips. No new blanket solution/all-suite cycle was run.
- Current complete production live class1/1, zero failures/skips,258.288s. Both serving Folders processes and other owning hosts changed identities; 30 DLL and30 PDB paths across174 runtime bindings match final outputs. Exact persisted bytes, current replica race202/409 with one version-1 winner, suppression/hold/resume/P7D physical deletion, faults and atomic publication passed. The current race had no retained domain rejection marker; historical AppendConflict evidence remains separate.
- [Current correction evidence](../../docs/evidence/story-12-3-workspace-content/audit-fixes/README.md) records exact commands/log digests,118 current root source/support paths,34 upstream paths, the29 root correction delta paths, matrix coverage, runtime identities and failed attempts. Original evidence remains historical. Frozen intent/OQ2/historical v1 remain unchanged; candidate v2 remains disabled. This snapshot preceded independent review; final resolution follows below.

### Independent review resolution and final parent qualification

- Three independent review lenses returned 18 findings; parent follow-through added P1/P2. All 20 individual verdicts remain in the triage log: 19 patched, one optional-cursor claim refuted. No deferred or unresolved finding remains. Frozen intent and review iteration 0 are preserved.
- Final patches qualify counted policy hashing, permanent byte-free erasure/create fencing, rejected-preparation reconciliation, source-proven replay after expiry/closure, committed-body protection, canonical typed context responses, current checks before 416, final single/batch lease guards, sensitivity denial before writes, malformed JSON, production search/glob and legacy optional-version roundtrips.
- Parent Debug/source solution restore and current build passed with zero warnings/errors. Passing test lanes: Core 2,218 passed plus two existing optional Forgejo skips; Server 1,100; Client 342; Contracts 360; Folders EventStore host 66; CLI 31; MCP 15; in-process content wiring 1. Upstream protocol 19, real Redis/Dapr persistence 1 and affected helper consumers 49 passed. Including the live class, these lanes total 4,203 passed, zero failed and two pre-existing skips; every Story 12.3 acceptance check executed without skips.
- Final real live class passed 1/1 in 248.730s. Both serving Folders processes and the worker/EventStore owners changed identities. All 30 DLL and 30 PDB paths across 141 runtime bindings match final build outputs. The current replica race returned 202/409, retained AppendConflict and exactly one persisted version-1 winner. Exact inline/multipart bytes after restart, suppression, holds/resume/fresh P7D epochs, byte erasure with retained create fence, faults and atomic publication passed.
- The first final Contracts run identified a cache-lint violation in one new test fixture. It was corrected with a strong byte-state/ETag read and exact scoped-key/end-state assertions; the current solution build, full Server and full Contracts were rerun. Parent lanes ran sequentially. Production sources remained unchanged throughout; all 179 source/configuration/support paths remained unchanged over the final corrected-source interval. No edit/build/test overlapped the live class.
- [Final review evidence](../../docs/evidence/story-12-3-workspace-content/review-fixes/README.md) records exact commands/log digests, 138 current root paths, 41 upstream paths, review resolutions, matrix coverage, runtime identities and separate failed attempts. Earlier evidence remains historical. All 229 pending candidate inventory entries match current hashes; public v2 remains disabled. Approved intent, OQ2 and historical v1 remain unchanged. EventStore is qualified from source; Release/package consumption/publication remains separate.
