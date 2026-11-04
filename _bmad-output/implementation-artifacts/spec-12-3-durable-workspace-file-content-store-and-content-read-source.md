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

### Review Findings (2026-11-04, chunk 1 of 3: src)

Review of commit `d6fda13a` (its parent is `baseline_commit` `28513763`), limited to `src/**`: 72 files, +1,967/−96. Tests (3,463 diff lines) and docs/scripts (722 diff lines) are follow-up chunks, and `docs/evidence` is excluded. The EventStore content capability was read from upstream `d0eb3d895`; it is not part of this repository's diff. Blind Hunter, Edge Case Hunter, Verification Gap and Acceptance Auditor all ran, and none failed. All 82 raw findings were verified claim by claim at `d6fda13a` and then checked against later `main` (`46214293`). Line numbers refer to `d6fda13a`. Every open item below is still present on `main` unless noted.

- [x] [Review][Decision] An orphaned Pending content publication blocks every later mutation in the workspace.
  - **Behavior.** `GetPublicationOutcomeAsync` returns Pending unconditionally for an actor-owned read (`WorkspaceContentAuthority.cs:172`). Outside the actor it resolves only on a gateway `IsRejected` (`:173-176`). A command that was prepared but never appended or domain-rejected therefore stays Pending forever. That happens on a crash between `PrepareAsync` and append, an infrastructure append failure, or an expired command status.
  - **Impact.** The SDK refuses every `PrepareAsync` while any publication in the scope is Pending (`DaprAuthoritativeContentStore.PrepareAsync`). Every later add, change or remove in that workspace then returns 409. The approved retention rule keeps unresolved publications protected from deletion. It does not require blocking new preparations.
  - **Options.**
    1. Resolve as Rejected when the gateway reports a terminal non-accepted status, or the status is missing past a bounded recovery window. This is a Folders-side change and needs the terminal-status vocabulary.
    2. Add an SDK-owned bounded recovery deadline that converts an unresolved Pending to Rejected after proving there was no append. This needs an EventStore story under Model B.
    3. Recover only by operator action through the disposition endpoint, plus a runbook. Resolved 2026-11-04: Jerome chose option (1), Folders resolves an orphaned Pending from terminal gateway status; tracked as a patch below.
- [x] [Review][Decision] The v1 file routes emit outcomes that the v1 Contract Spine does not declare.
  - **Behavior.** `ContentProblem` (`FoldersDomainServiceEndpoints.Content.cs:65-66`) emits 409 `concurrency_conflict`, and 422 `confidential_operation_not_durable` with `clientAction: correct_input`.
  - **Contract.** v1 `CanonicalErrorCategory` (`hexalith.folders.v1.yaml:11126-11169`) has neither value, and `correct_input` appears nowhere. AddFile/ChangeFile declare 409 only as `IdempotencyConflict` and 422 only as `FileContentLimitExceeded422` (`:2218-2225`). At baseline an AppendConflict mapped to the declared `repository_conflict`.
  - **Mandate.** Epic 12 context and architecture PD8/D-11 require these codes.
  - **Options.**
    1. Extend the v1 spine with the new enum values and response refs, regenerate the client and helpers, and accept the PD10 and parity digest churn.
    2. Remap v1 to already-declared outcomes and carry the mandated codes on v2 only.
  - **Rejected sub-claim.** That the move from 416 to 404 for sensitivity-redacted ranges is a regression: OQ2 forbids 416 for a sensitivity denial. Resolved 2026-11-04: Jerome chose option (1), extend the v1 spine; tracked as a patch below.
- [x] [Review][Decision] JSON `PutFileStream` mode can never succeed.
  - **Behavior.** A non-receipt or unknown `stagingReference` reaches `VerifyUploadAsync`, which throws `ContentUnavailableException`. The endpoint maps that to a retryable 503 `file_policy_unavailable` (`FoldersDomainServiceEndpoints.cs:3360-3371`).
  - **Impact.** No Folders route issues a receipt; `UploadAsync` is called only on the inline and multipart paths (`:3356`). `FileStreamStagingEvidence` and the contract's own `ChangeFileStreamRequest` example can therefore only ever be told to retry.
  - **Options.**
    1. Add a receipt-issuing upload route. This is new public surface: contract plus client.
    2. Retire JSON stream mode in favour of multipart.
  - In either case, a malformed or unknown receipt should become a non-retryable 400 `content_evidence_invalid`. Resolved 2026-11-04: Jerome chose option (2), retire JSON stream mode in favour of multipart; tracked as a patch below.
- [x] [Review][Decision] Each erasure grant restarts every other receipt's P7D cleanup epoch.
  - **Behavior.** The disposition generation is the raw folder stream head (`WorkspaceContentAuthority.cs:186`). The SDK resets the epoch whenever the generation differs (`DaprAuthoritativeContentStore.ApplyDispositionAsync`). Every grant appends an event (`WorkspaceContentErasure.cs:30`).
  - **Impact.** N mature receipts in one folder take about N×7 days to erase, and any unrelated folder event restarts them all.
  - **Options.**
    1. Derive the generation from receipt-relevant source state only: this receipt's references, task closure, grant and holds.
    2. Keep the head-based generation (the most conservative reading of the approved rule) and grant all mature receipts in one event.
    3. Accept and document the slower cadence. Resolved 2026-11-04: Jerome chose option (1), a receipt-relevant generation; tracked as a patch below.
- [x] [Review][Decision] Replay detection matches on `operationId` alone across the whole folder.
  - **Behavior.** `ValidateIngressAsync` uses `ContentPublications.Values.SingleOrDefault(b => b.OperationId == operationId)` across every workspace and task (`WorkspaceContentAuthority.cs:32-39`).
  - **Impact.** A later task that reuses a client-chosen operationId gets a permanent 409; the CLI `--operation-id` option is free-form. Duplicate bindings can be created through the batch and gateway-direct paths, which skip ingress. `SingleOrDefault` then throws, and that surfaces as a retryable 503.
  - **Options.**
    1. Scope uniqueness to (workspace, task) and match replay by task plus operationId.
    2. Enforce folder-lifetime uniqueness on every admission path, with a deterministic non-retryable conflict.
  - In either case, replace `SingleOrDefault`. Resolved 2026-11-04: Jerome chose option (1), scope uniqueness to (workspace, task); tracked as a patch below.
- [x] [Review][Decision] The operational status state keeps content command keys and receipt ids.
  - **Behavior.** The sanitizer (`OperationalStatusFold.cs:91-102`) strips command keys ("Do not duplicate command keys…"). It does not strip `ContentPublications` (keyed by idempotency key; holds CommandId and ReceiptId), `CommittedContentReferences` or `ContentErasureGrants`.
  - **Impact.** These collections persist unbounded in operational storage. Simply clearing them breaks later folds (`FolderStateApply.cs:418-420`).
  - **Options.**
    1. Re-key or digest these collections in the operational projection.
    2. Fold a separate minimal content view and exclude this bookkeeping from operational state.
    3. Accept them as operational metadata and record that in C3. Resolved 2026-11-04: Jerome chose option (1), re-key/digest in the operational projection; tracked as a patch below.
- [x] [Review][Decision] A breaking v1 contract and Client change shipped as a `fix:` commit and went out as patch releases.
  - **Behavior.** `d6fda13a` (titled `fix: story 12.2`, although it delivered 12.3) makes `expectedFileVersion` and `expectedLockFence` required on v1 `FileMutationRequest` (`hexalith.folders.v1.yaml:9109-9128`). The server rejects requests that lack them (`FoldersDomainServiceEndpoints.cs:3317-3318`). The commit also adds a route, a command and an event.
  - **Release.** `.releaserc.json` uses the default commit analyzer, and tags v1.3.2 and v1.3.3 contain this commit.
  - **Options.**
    1. Publish a breaking-change and migration notice for 1.3.2 and later.
    2. Cut a corrective major release.
    3. Accept as is, on the basis that there are no external consumers yet. Resolved 2026-11-04: Jerome chose option (1), a breaking-change and migration notice; tracked as a patch below.
- [ ] [Review][Patch] Resolve an orphaned Pending content publication as Rejected when the gateway reports a terminal non-accepted command status (failed or timed out); a missing status stays Pending for operator recovery — owner decision 2026-11-04 [`src/Hexalith.Folders.Server/Content/WorkspaceContentAuthority.cs:172`]
- [ ] [Review][Patch] Extend the v1 Contract Spine with `concurrency_conflict` (CanonicalErrorCategory), `correct_input` (clientAction), `confidential_operation_not_durable`, and the matching 409/422 response refs on the AddFile/ChangeFile/RemoveFile routes. Then regenerate the client and helpers and reconcile the PD10 and parity digests — owner decision 2026-11-04 [`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:11126`]
- [ ] [Review][Patch] Retire JSON `PutFileStream` mode. Return a non-retryable 400 `content_evidence_invalid` for any JSON `stagingReference`, keep multipart as the only stream transport, and mark `FileStreamStagingEvidence` / `BuildStreamedFileMutation` unsupported — owner decision 2026-11-04 [`src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs:3360`]
- [ ] [Review][Patch] Derive the erasure disposition generation from source events relevant to the receipt (its references, task closure or resume, its grant, holds) instead of the raw folder head, so unrelated events and other receipts' grants no longer restart its P7D epoch. Resume and hold cycles must still change the generation — owner decision 2026-11-04 [`src/Hexalith.Folders.Server/Content/WorkspaceContentAuthority.cs:186`]
- [ ] [Review][Patch] Match a content replay by (workspace, task, operationId), and replace `SingleOrDefault` with an explicit duplicate check that returns a deterministic non-retryable conflict — owner decision 2026-11-04 [`src/Hexalith.Folders.Server/Content/WorkspaceContentAuthority.cs:32`]
- [ ] [Review][Patch] In the operational projection, re-key `ContentPublications` by digest and replace receipt ids and command keys in `ContentPublications`, `CommittedContentReferences` and `ContentErasureGrants` with digests, keeping what the fold's version checks need — owner decision 2026-11-04 [`src/Hexalith.Folders/Projections/OperationalStatus/OperationalStatusFold.cs:91`]
- [ ] [Review][Patch] Publish a breaking-change and migration notice for v1.3.2 and later (CHANGELOG and release notes) covering the new required `expectedFileVersion` and `expectedLockFence` on v1 file mutations — owner decision 2026-11-04 [`CHANGELOG.md`]
- [ ] [Review][Patch] Run path evidence and sensitivity classification before the existence/version checks, so a sensitivity-denied path cannot be probed through 409 vs 404. This is the OQ2 evaluation order, and the same ordering appears at both sites [`src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs:3333`, `src/Hexalith.Folders.Server/Content/WorkspaceContentAuthority.cs:48`, `src/Hexalith.Folders/Aggregates/Folder/WorkspaceFileMutationService.cs:135`]
- [ ] [Review][Patch] Make `ContentProblem` emit the v1 exact file-problem tuples. The SDK's `Oq2WireObjectConverter.ValidateExactProblem` currently rejects every one of these responses. [`src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.Content.cs:56`]
  - Use the contract titles and messages, and `visibility: redacted` for 404 and 503.
  - Never emit a null correlation id, and drop the extra `taskId` member.
  - Fix the test that pins `"visibility":"metadata_only"` (`FileContextEndpointTests.cs:292`).
- [ ] [Review][Patch] Catch the SDK-internal `OperationCanceledException` in `ProcessContentBatchAsync` after `PrepareAsync` and return a rejection, so a timed-out preparation is recorded as Rejected rather than left Pending, which feeds the workspace-wedge decision above [`src/Hexalith.Folders.Server/FolderDomainProcessor.ContentBatch.cs:91`]
- [ ] [Review][Patch] Sync the AddFile/ChangeFile/RemoveFile `x-hexalith-idempotency-equivalence` lists with the server fingerprint (`expectedFileVersion`, `expectedLockFence`, `ownerFileId`) and regenerate the helpers. Today a retry with a new fence or version derives the same key and gets a permanent `idempotency_conflict` [`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:2242`]
- [ ] [Review][Patch] When the retained rejection cannot be matched uniquely, narrow the match by correlation, task and command type. If it is still ambiguous, return a non-retryable rejection rather than a retryable 503 `file_policy_unavailable`. Log the exception that the empty `catch` currently swallows [`src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs:3417`]
- [ ] [Review][Patch] Map ingress lock-holder, closed-task, expired-lease and inactive-lifecycle failures to the declared 410 `WorkspaceLockExpired` / 409 `lock_not_owned` outcomes, not 409 `concurrency_conflict`. The domain and batch paths already do this [`src/Hexalith.Folders.Server/Content/WorkspaceContentAuthority.cs:42`]
- [ ] [Review][Patch] Catch SDK-internal timeouts in the mutation endpoint's content block (`when (!cancellationToken.IsCancellationRequested)`) and return the canonical 503 instead of an unhandled 500 [`src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs:3371`]
- [ ] [Review][Patch] On a rejected batch, return the item's canonical `FolderResultCode` (LockExpired, LockNotOwned, AppendConflict, PathPolicyDenied) instead of `ValidationFailed` [`src/Hexalith.Folders.Server/FolderDomainProcessor.ContentBatch.cs:70`]
- [ ] [Review][Patch] Fail a context read as Unavailable when a source-active, receipt-bound file version is missing from the SDK ledger, rather than silently leaving it out (an empty success or a safe 404) [`src/Hexalith.Folders.Server/Content/AuthoritativeWorkspaceFileContextSource.cs:57`]
- [ ] [Review][Patch] Fold the cumulative batch events against actor state before returning. Order-dependent apply checks, such as the 10,000-identity bound or two adds binding the same Projects reference, then fail the decision instead of poisoning replay after append [`src/Hexalith.Folders.Server/FolderDomainProcessor.ContentBatch.cs:88`]
- [ ] [Review][Patch] Evaluate inline routing for 262,145–1,048,576 bytes (413) before the length/hash mismatch check (400), per OQ2 content-failure precedence [`src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs:3760`]
- [ ] [Review][Patch] Add `InvalidDataException` (malformed multipart framing, or part headers over 16,384 bytes) to the 400 exception filter; today it falls through to a 500 [`src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs:3276`]
- [ ] [Review][Patch] Report a source-head change during a context read as source unavailability, not `PolicyUnavailable` (`file_policy_unavailable`) [`src/Hexalith.Folders.Server/Content/AuthoritativeWorkspaceFileContextSource.cs:92`]
- [ ] [Review][Patch] Report a duplicate batch operationId as `ValidationFailed`, not `PathPolicyDenied` [`src/Hexalith.Folders.Server/FolderDomainProcessor.ContentBatch.cs:39`]
- [ ] [Review][Patch] Reuse the deserialized `mutation.OperationId` instead of a case-sensitive `GetProperty("operationId")`. A differently cased key currently throws KeyNotFound, which becomes a retryable PolicyEvidenceUnavailable [`src/Hexalith.Folders.Server/FolderDomainProcessor.ContentBatch.cs:67`]
- [ ] [Review][Patch] Send the operator's validated correlation id with the erasure-grant submission, instead of the synthetic `erase-…` id [`src/Hexalith.Folders.Server/Content/WorkspaceContentErasure.cs:43`]
- [ ] [Review][Patch] Skip the `ownerFileId` replay compare for `add`. The service ignores that field and mints its own id, so today an identical add retry gets a 409 instead of replaying [`src/Hexalith.Folders.Server/Content/WorkspaceContentAuthority.cs:39`]
- [ ] [Review][Patch] Add a post-processor rewrite for `LockLeaseMetadata.LockFence`, like the one P2 added for `version`, so an omitted fence does not round-trip as `lockFence: 0`, which violates `minimum: 1` [`src/Hexalith.Folders.Client/Generation/GeneratedClientPostProcessor.cs:98`]
- [ ] [Review][Patch] Forward `descriptor.ContentMediaType` in the raw-stream helper's inline payload [`src/Hexalith.Folders.Client/Convenience/WorkspaceFileStreamUploadExtensions.cs:49`]
- [ ] [Review][Patch] Use `FileUpload.MaximumFileBytes` and `InlineTransportBoundaryBytes` instead of duplicated literals [`src/Hexalith.Folders.Client/Convenience/WorkspaceFileStreamUploadExtensions.cs:28`]
- [ ] [Review][Patch] Test preparation and publication outcome through the real `WorkspaceContentAuthority`; every normal-lane test currently swaps in a seed, transport or source authority. Cover stale fence, stale head, stale lease and an erasure-granted receipt being rejected, and Pending during the actor decision becoming Accepted after the fold [`src/Hexalith.Folders.Server/Content/WorkspaceContentAuthority.cs:119`]
- [ ] [Review][Patch] Extend `WorkspaceContentProcessWiringTests` with three cases [`src/Hexalith.Folders/Aggregates/Folder/WorkspaceFileMutationService.cs:127`]:
  - a processed change with `ownerFileId` omitted, producing version 2;
  - a remove, producing a tombstone and an inactive identity;
  - a stale-version change, returning 409 with no append.
- [ ] [Review][Patch] Assert that adapter `FailureCode` values propagate (AppendConflict → 409) for both the content and delete stores [`src/Hexalith.Folders/Aggregates/Folder/WorkspaceFileMutationService.cs:225`]
- [ ] [Review][Patch] Add endpoint tests for the gateway `folder-command-rejected` branch (a retained rejection maps to its canonical code), the 403/404 → 404 remap and the 503 → `file_policy_unavailable` remap [`src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs:3417`]
- [ ] [Review][Patch] Add processor tests for `ProcessContentErasure`, plus tests for `AuthorizeContentErasureIdempotencyIntentAdapter` and the `manage_folder_access` token mapping [`src/Hexalith.Folders.Server/FolderDomainProcessor.cs:1317`]. Processor cases:
  - accept;
  - idempotent re-grant;
  - newer-grant conflict;
  - live reference;
  - active task;
  - unclosed assigned task;
  - head mismatch.
- [ ] [Review][Patch] Add endpoint tests for the content disposition route [`src/Hexalith.Folders.Server/Content/WorkspaceContentRetentionEndpoints.cs:25`]:
  - hold, release, abandon and apply;
  - invalid operation or receipt;
  - unauthorized → 404;
  - conflict → 409;
  - its middleware bypass.
- [ ] [Review][Patch] Add transport tests for the bearer-relay branches [`src/Hexalith.Folders.Server/Authentication/FoldersGatewayHumanBearerHandler.cs:14`]:
  - stream reads and status calls relay when the content action item is set;
  - a submit with only the action item does not relay;
  - the retention flag relays.
- [ ] [Review][Patch] Pin lock-fence generation (it must increase across acquire, release and re-acquire) and the `Lease.LockFence` projection. Later lock-endpoint tests on `main` partly cover this [`src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs:447`]
- [ ] [Review][Patch] Assert `fileId` and `version` on produced authoritative context items [`src/Hexalith.Folders.Server/Content/AuthoritativeWorkspaceFileContextSource.cs:125`]
- [ ] [Review][Patch] Test case-alias and file/directory prefix collisions at upload admission and at context read [`src/Hexalith.Folders.Server/Content/WorkspaceContentPathEvidenceProvider.cs:145`]
- [ ] [Review][Patch] Add a handler theory for invalid globs (`[ab]`, `../x`, `/abs`, `src/**.cs`) that expects ValidationFailed with no source call [`src/Hexalith.Folders/Queries/FileContext/WorkspaceFileContextQueryHandler.cs:228`]
- [ ] [Review][Patch] Add HTTP tests that a denying principal on the context and files routes, and a NotFoundSafe source, return 404 `tenant_access_denied`/`resource_unavailable` [`src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs:4473`]
- [ ] [Review][Patch] Add client tests that `ownerFileId` reaches the wire for change through all three helpers, and that `UploadWorkspaceFileAsync` change uses PUT `/files/change` [`src/Hexalith.Folders.Client/Convenience/FoldersFileUploadExtensions.cs:59`]
- [x] [Review][Defer] A redelivered batch whose items all replay is rejected as AppendConflict [`src/Hexalith.Folders.Server/FolderDomainProcessor.ContentBatch.cs:69`] — deferred: unverified medium. It depends on whether EventStore re-dispatches an already-appended MutateFiles command id to the processor (Story 12.6 admission semantics).
- [x] [Review][Defer] An erasure on an uncreated aggregate could append a null-organization event that breaks the operational fold [`src/Hexalith.Folders.Server/FolderDomainProcessor.cs:1335`] — deferred: unverified medium. It depends on whether layered authorization admits `manage_folder_access` for a nonexistent folder on a gateway-direct submit.
- [x] [Review][Defer] A context read replays the full stream three times per query, and each fold rebuilds `ContentPublications` per event (O(n²)), all inside the 2 s deadline [`src/Hexalith.Folders.Server/Content/AuthoritativeWorkspaceFileContextSource.cs:41`] — deferred: this needs snapshot-aware or head-only rechecks through the platform head/stream-metadata seam, which the Story 12.1 review already decided to obtain from EventStore under Model B. The per-event rebuild pattern predates this story.
- [x] [Review][Defer] New `FolderState` members change the operational checkpoint integrity digest, so checkpoints written before a deploy fail as malformed [`src/Hexalith.Folders/Projections/OperationalStatus/OperationalStatusIntegrity.cs:10`] — deferred: the checkpoint design predates this story; checkpoint schema versioning belongs to Story 12.2 durable projections, and no production checkpoints exist.

Resolved on `main` after the reviewed commit; no action needed:

- **Lock fence missing from public responses.** No public lock-status response emitted a lock fence, so callers could not build `expectedLockFence` at all. Fixed by `21d9c38e`.
- **Erasure rejections stalled Workers.** `AuthorizeWorkspaceContentErasure` rejections made the Workers rejection classifier throw "command type is unknown", which stalled the folder permission projection. Fixed by `9a818bb6`.
- **Golden lifecycle test failed.** `GoldenLifecycleAllNineStepsDriveOverRealRestTransportToTransportTerminalClass` was a normal-lane `[Fact]` that the new hash and fence checks made fail at `d6fda13a`. Fixed by `c901c8be`. The story's qualification ran only the filtered content-wiring integration lane and did not claim the full lane.

Rejected:

- `false` — **expectedHead fallback** to `IdempotencyFingerprints.Count`. `FolderActorSequenceScope` is always entered for folder commands (`FoldersDomainServiceRequestHandler.cs:161`). Otherwise `ValidatePreparationAsync` fails closed on `!ActorOwned`.
- `false` — **Replay throw makes the aggregate unloadable.** Throwing is the existing fail-closed replay pattern. Single-item events are test-folded before append, and `1.1.0` is the only policy version the snapshot accepts. The batch case is covered by the cumulative-fold patch.
- `false` — **Service-locator lookups in the batch.** They cause no behavioral harm.
- `false` — **Missing WorkspaceId check on erasure.** Grants are keyed by receipt. A `state.WorkspaceId` check would block erasure of receipts from earlier workspaces.
- `false` — **The 10 s erasure poll throws OperationCanceledException.** It maps to a retryable 503, and the next apply takes over the abandoned claim, so the delay is bounded.
- `false` — **Orphaned uploads after a failed submit.** Uploads are task-bound and follow the approved terminal → P7D path, and the SDK upload is create-only per OperationId.
- `false` — **Bearer forwarding by URL substring.** The request URIs are built internally on the gateway client. The retention flag is set only inside erasure authorization, whose submit needs the human credential.
- `false` — **Fence skipped for a replacement authority.** Production registers `WorkspaceContentAuthority`. The domain pre-check and `ValidatePreparationAsync` still fence, and the Read operation runs mutation reauthorization through ActionItem.
- `false` — **No shipped policy configuration.** OQ2 requires failing closed with no permissive default. The setting is documented in `docs/operations/workspace-content-authority.md:8`, and the AppHost is owned by the platform repo.
- `false` — **Remove commands not updated.** CLI/MCP remove take the full `RemoveFileRequest` JSON, which carries the new fields, and `CommandSurfaceE2ETests` covers this.
- `false` — **Placeholder stagingReference.** The multipart branch uploads the actual bytes and ignores the field.
- `false` — **Hard-coded NFC.** OQ2 1.1.0 is ASCII-only, and NFC is the only accepted value.
- `false` — **Enum members inserted at position 0.** Neither enum is persisted or serialized numerically, and the new default is the fail-closed value.
- `false` — **HasActiveTask blocks erasure forever.** Unclosed failed or released work counts as unresolved and protected under the approved retention rule.
- `false` — **BaseAddress handling in the stream helper.** It resolves URIs exactly as the generated client does, and a null BaseAddress fails loudly inside HttpClient.
- `false` — **Post-processor rewrite could silently do nothing.** `GeneratedClientAndHelpersMatchIsolatedRegeneration` diffs the output, and `ClientGenerationTests.cs:1375` pins the legacy version omission.
- `false` — **Upload bytes never scanned for confidential cleartext.** Byte-level tokenization is Story 12.7 scope. The approved 12.3 mechanism is an explicit `non_confidential` policy that fails closed otherwise.
- `low` — **Fingerprint fence/version presence markers.** Already in the deferred-work ledger from the Story 11.5 follow-up review, pending a versioned-fingerprint decision. The public route requires both fields.
- `low` — **Disposition hold, release and abandon leave no audit event.** This is an operator-only route with a separate admin contract, apply already appends a source event, and new events would be new design.
- `low` — **Confidential check runs after admission on the batch and gateway-direct paths.** Nothing is persisted because upload fails closed, and a pre-admission hook is not a direct fix.
- `low` — **`DateTimeOffset.UtcNow` on erasure event metadata.** It is only a metadata stamp; the epoch uses the SDK clock.
- `low` — **Hard-coded context sensitivity labels and the search bound.** The labels can't be reached with the shipped classifier, search is bounded by the C4 2 s deadline, and the cursor claim repeats B7.
- `low` — **Optional client fence/version parameters throw at runtime.** The first call fails loudly with a descriptive message. Making them required would be another breaking signature change to a published package.
- `low` — **The lock 202 response carries no fence.** This is ordinary eventual consistency with a canonical retry, and returning the fence adds public surface.
- `low` — **The raw-stream helper skips client validation.** The server re-validates and returns 400.
- `low` — **The raw-stream helper buffers the stream.** Buffering is capped at 1 MiB and is needed to hash before sending.
- `low` — **ownerFileId not typed as OpaqueIdentifier.** The server mints conforming ULIDs and rejects malformed values with 409. Fixing this needs contract regeneration.
- `low` — **Disposition with an unknown receipt returns 503.** The SDK uses one exception for both not-found and unavailable, and the route is operator-only.
- `low` — **Path limit lowered from 512 to 500.** This matches OQ2 1.1.0 and v1 `maxLength: 500`. Out-of-contract identities exist only in pre-12.3 non-production data.
- `low` — **Raw headers reflected into the disposition problem body.** Kestrel limits bound their size, and the response shape is covered by the ContentProblem patch.
- `low` — **Legacy files without bindings can't be changed or removed.** Pre-12.3 production stores were unavailable placeholders, so no production legacy identities exist, and the spec disclaims legacy reconstruction.
