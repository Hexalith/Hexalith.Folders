---
baseline_commit: 6dd0b9d89f76ffad4831806325c1849d76c6b272
eventstore_baseline_commit: 539dca2b277f37b2ee6babe8c486f002a8ec3991
---

# Story 12.6: Implement durable all-mutations idempotency and expired-key precedence

Status: ready-for-dev

## Story

As an authorized caller,
I want every mutation to use one durable tenant-scoped idempotency contract that remembers consumed keys after replay results expire,
so that retries, conflicts, restarts, concurrent replicas, and expired-key reuse can never duplicate domain or external effects.

## Acceptance Criteria

1. Given Story 12.6 begins, when Task 0 completes, then Architecture A-9/D-7, ADR-0004, C3 retention evidence, the Contract Spine, and OQ8 governance freeze one compatible design for: tenant-scoped key partitioning; canonical intent fingerprinting; reservation/pending/recoverable/unknown/terminal/expired states; replay-result retention; consumed-key tombstone retention and tenant-deletion/legal-hold disposition; digest/key-rotation/collision handling; the exact `now == expiresAt` boundary; which deterministic failures consume a key; the stable HTTP/CLI/MCP mapping for `idempotency_key_expired`; and a trusted EventStore admission API. The record includes dated Architecture, Security, and Test approval identities plus evidence version and digest. Production implementation does not guess any unresolved value.
2. Given any current or future Contract Spine mutation, when contract generation and runtime admission are evaluated, then it requires `Idempotency-Key`, declares canonical equivalence fields and one fixed retention tier, carries a server-trusted canonical intent descriptor into EventStore, and exposes both live conflict and expired-key outcomes. The canonical ledger partition is managed tenant plus opaque idempotency key; operation, canonical target identity, normalized semantic payload/options, policy version, delegated task scope, and behavior-affecting credential scope participate in intent equivalence, while correlation, authentication-token, clock, and transport-retry metadata do not. Reusing the same opaque key in another tenant is isolated.
3. Given an unexpired live record and equivalent intent, when the caller is still currently authorized, then REST, SDK, CLI, and MCP return the same logical result and replay evidence without a second aggregate execution, event, provider/readiness/credential lookup, repository or file write, Git operation, projection change, audit mutation, task scheduling action, or idempotency record. If current authorization is revoked, stale, wrong-tenant, wrong-folder, or otherwise denied, safe denial occurs before any protected replay result or key-disposition disclosure and the stored record remains intact.
4. Given an unexpired live record and different intent under the same tenant-scoped key, when the request is authorized and admitted, then it returns canonical `idempotency_conflict` before domain or external execution and does not disclose the prior operation, target, payload, path, repository, credential scope, provider identity, result, or other protected intent. The real EventStore gateway must compare the canonical intent descriptor; same message ID and command type alone are not proof of equivalence.
5. Given replay-result retention has expired, when the old key is reused with equivalent or different intent, then both forms return the same stable `idempotency_key_expired` outcome before aggregate state/readiness/provider/path/content/Git/audit work, with `retryable = false` and `clientAction = refresh_state_then_submit_with_new_key`. Expiry takes precedence over current aggregate/precondition drift and submitted-intent equivalence; the old request never falls through as fresh work and never becomes `Missing` merely because the replay payload was compacted.
6. Given a mutation is first submitted, retried concurrently, interrupted, or observed by another replica, when admission, reservation, execution outcome, replay-result recording, unknown-outcome recovery, expiry, and compaction occur, then the authoritative EventStore-backed state transition is atomic and exactly one execution can cross the side-effect boundary. Pending, recoverable, `unknown_provider_outcome`, terminal, and expired states survive process restart and multi-replica access; clock rollback, compaction races, and host failover cannot resurrect a consumed key.
7. Given D-7/C3 tiers are applied, when time is just before, exactly at, or after the boundary, then non-commit mutation replay results remain live for the approved 24-hour tier, commit replay results remain live for the approved seven-year C3 tier, and the independently approved minimal consumed-key evidence remains recognizable for its full governed lifetime. Persisted tombstones contain only versioned metadata needed for safe lookup and retention; they contain no raw key, request/result payload, canonical fingerprint, content, path, diff, repository URL/name/ref, credential/token, commit message, provider body, unauthorized identity, or protected prior-intent hint.
8. Given the generated C13 inventory currently contains 49 operations, when completeness gates run, then all 14 generated mutation rows cover new-key, live-equivalent, live-different, expired-equivalent, and expired-different behavior, and all 35 generated read rows reject a supplied key as `idempotency_key_not_allowed` before query/projection/source execution. The denominator is generated from the Contract Spine rather than frozen as a handwritten count, so a new mutation or read fails the gate until its complete behavior matrix exists.
9. Given canonical errors cross the public surfaces, when `idempotency_key_expired` is emitted by EventStore or Folders, then OpenAPI, generated client models/helpers, REST Problem Details, gateway exceptions, domain result codes, CLI exit projection, MCP failure kind, documentation, and C13 preserve the exact approved code, status, retryability, client action, correlation rules, and no-leak shape. It is not collapsed into `idempotency_conflict`, `command_identity_conflict`, generic validation, not-found, or infrastructure failure.
10. Given Story 12.6 is complete, when production-path verification runs against the approved durable state store, then evidence proves tenant isolation, restart survival, multi-host concurrency, expiry boundaries, replay-payload compaction, unavailable/corrupt/legacy-record fail-closed behavior, crash/unknown-outcome recovery, and zero duplicate effects for every mutation family. Tests assert durable store end state as well as responses and call counts. OQ8 closes only after the canonical contract, versioned C13 snapshot, consumed-key retention evidence, evidence digest/version, and dated Architecture, Security, and Test approvals exist; green code or tests alone do not close it.

## Tasks / Subtasks

- [ ] Complete the OQ8 architecture and governance entry gate before production implementation. (AC: 1, 5, 7, 9, 10)
  - [ ] Update Architecture concern #3, concern #21, A-9, D-7, and the process pattern from the stale subset/TTL wording to every mutation, all reads, tenant-scoped key partitioning, authorization-aware replay, explicit expired precedence, and separate replay-result versus consumed-key retention.
  - [ ] Update `docs/adrs/0004-per-command-canonical-idempotency.md` with the approved EventStore-owned state machine and trusted descriptor boundary; retain the Contract Spine as equivalence authority.
  - [ ] Add a distinct consumed-key evidence class to `docs/exit-criteria/c3-retention.md`, including exact lifetime, cleanup trigger, tenant deletion, legal hold, anonymization/deletion disposition, digest/key rotation, collision response, and observable evidence. Do not infer this duration from the 24-hour or seven-year replay-result tiers.
  - [ ] Freeze the exact expiry boundary, clock authority, deterministic-failure consumption policy, replay-result storage/rehydration rule, HTTP status, CLI exit code, MCP kind, and safe Problem Details mapping for `idempotency_key_expired`.
  - [ ] Record dated Architecture, Security, and Test decisions with artifact version and digest. If any required owner declines or leaves a value unresolved, stop before code and keep OQ8 open.

- [ ] Deliver the EventStore platform prerequisite in its owning repository and consume a released/pinned version. (AC: 2, 4-7, 9, 10)
  - [ ] Create or approve the upstream Hexalith.EventStore work item for tenant-scoped atomic idempotency admission, canonical intent conflict detection, fixed retention-tier selection, current-authorization-aware replay, explicit expired denial, and durable minimal tombstones.
  - [ ] Replace the current expiry path that removes `idempotency:{messageId}` and then falls through to command execution. `Expired` must be terminal for the old key and must atomically compact or resolve to durable consumed-key evidence.
  - [ ] Extend the platform identity/admission contract so a trusted domain adapter supplies the canonical intent descriptor and fixed tier before the actor decision. Public `SubmitCommandRequest.Extensions` values are untrusted and cannot select a digest, scope, or retention duration without server-side validation.
  - [ ] Ensure a live same-key/different-intent request conflicts even when message ID and command type match, and ensure the same tenant key cannot be reused silently against another aggregate/target.
  - [ ] Preserve pipeline resume and recoverable/unknown-outcome semantics without blind provider/domain re-execution; define versioned migration for legacy raw-key/full-result records and fail closed on corruption or unknown versions.
  - [ ] Remove raw idempotency keys from persisted tombstones and diagnostic logs. Use the Security-approved digest/partition design and redaction rules.
  - [ ] Implement EventStore unit, actor, gateway, and live-sidecar tests for exact duplicate, intent conflict, expiry, compaction, restart, multi-replica/concurrent admission, unavailable state, migration, and persisted state-store end state.
  - [ ] Make the upstream change from the EventStore repository under its own guidance, review, release, and verification. Do not edit `references/Hexalith.EventStore` from the Folders repository without explicit submodule authorization; consume the approved release or root-declared pin afterward.

- [ ] Integrate the durable admission contract into Folders after the durable repository prerequisites exist. (AC: 2-7, 9, 10)
  - [ ] Sequence final integration after Story 12.1 provides the EventStore-backed `IFolderRepository` and `/project` replay path. Add or expand the durable Organization persistence prerequisite so `ConfigureProviderBinding` and organization ACL mutations do not remain process-local exceptions.
  - [ ] Add explicit live-equivalent, live-conflict, expired, missing, and unavailable/corrupt outcomes to the Folders and Organization idempotency abstractions/result codes. Remove ambiguous bool lookups and eternal fingerprint-only production semantics.
  - [ ] Register one Folders canonical-descriptor adapter over the approved EventStore seam. Do not add a Folders-owned `DaprClient` state wrapper, database, ORM, file/blob ledger, cache-as-authority, or parallel idempotency store.
  - [ ] Keep the public Contract Spine denominator at 14 mutations. Apply the same ingress invariant to internal Organization ACL commands if callable, without pretending they are public C13 rows.
  - [ ] Keep projection rebuild separate from consumed-key authority: `/project` replay may rebuild read models but must neither resurrect replay payloads nor erase or recreate tombstones as unused keys.
  - [ ] Make production dependency injection fail closed when durable admission, folder persistence, or organization persistence is absent; in-memory implementations remain test/development evidence only.

- [ ] Enforce authorization, validation, admission, and side-effect ordering for every mutation family. (AC: 2-6, 10)
  - [ ] Enforce the order: authentication and authoritative tenant/organization/folder/action authorization; canonical structural/semantic validation; trusted intent construction; durable admission; then aggregate/readiness/provider/path/content/Git/audit work.
  - [ ] Move repository-backed creation readiness access and workspace file path-policy evidence behind durable admission where safe, while preserving the rule that authorization and validation occur before any key-disposition disclosure.
  - [ ] Cover `CreateFolder`, `ArchiveFolder`, `UpdateFolderAclEntry`, `ConfigureProviderBinding`, `CreateRepositoryBackedFolder`, `BindRepository`, `ConfigureBranchRefPolicy`, `PrepareWorkspace`, `LockWorkspace`, `ReleaseWorkspaceLock`, `AddFile`, `ChangeFile`, `RemoveFile`, and `CommitWorkspace` through the common seam.
  - [ ] Coordinate worker delivery and in-flight external effects so duplicate `RepositoryProvisioningProcessManager`, commit, file, or provider deliveries cannot both cross the side-effect boundary before outcome recording.
  - [ ] Preserve bounded read-only reconciliation for unknown external outcomes; never turn an expired, pending, recoverable, or unknown key into permission for blind retry.

- [ ] Synchronize the Contract Spine, equivalence rules, generated SDK, and C13. (AC: 2, 5, 7-10)
  - [ ] Update `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` so every generated mutation declares the complete equivalence/tier/error behavior and every read declares key rejection. Add the approved expired-key response/example and canonical error vocabulary.
  - [ ] Expand `docs/contract/idempotency-and-parity-rules.md` from its stale mutation subset to the complete generated inventory, including `UpdateFolderAclEntry`, `ConfigureProviderBinding`, and `ConfigureBranchRefPolicy`, live/expired precedence, and consumed-key rules.
  - [ ] Update the extension vocabulary, canonical error catalog, generator inputs/templates, parity schema, parity generator, governed previous-spine snapshot, contract rules, and completeness gates together.
  - [ ] Regenerate the client, helpers, and `tests/fixtures/parity-contract.yaml` with repository tooling. Do not hand-edit generated files or hard-code 14/35/49 as the future denominator.
  - [ ] Add a generator failure for any mutation missing key requirement, canonical equivalence, tier, conflict, expired result, and matrix evidence, or any read missing canonical key rejection.

- [ ] Preserve one canonical expired-key result across REST, SDK, CLI, MCP, and domain processing. (AC: 3-5, 8, 9)
  - [ ] Map the approved EventStore expired reason through gateway exceptions, `FolderDomainProcessor`, Folder/Organization result codes, `FolderCanonicalErrorMapper`, and RFC 9457 Problem Details without loss or category collapse.
  - [ ] Update generated SDK error/result models, CLI `ErrorProjection`/exit mapping, MCP `FailureKindProjection`, and adapter parity tests with the approved status/code/retry/client-action values.
  - [ ] Centralize read-key rejection so every generated read rejects `Idempotency-Key` before query handler, read model, provider, Memories, audit stream, or diagnostic source execution.
  - [ ] Prove expired-equivalent and expired-different responses are indistinguishable except approved request correlation fields and reveal no prior-intent metadata.

- [ ] Generate and execute the full OQ8 behavior matrix. (AC: 3-10)
  - [ ] For every generated mutation: test first/new key, live equivalent, live different, expired equivalent, and expired different; current authorization allow/revoked/wrong-tenant/wrong-target; state drift after the original result; state store unavailable/corrupt/legacy; and no duplicate side effects.
  - [ ] For every generated read: test `Idempotency-Key` rejection as `idempotency_key_not_allowed` before query/projection/source calls.
  - [ ] Use `TimeProvider` and test mutation/commit tiers at expiry minus one tick, exact expiry, and plus one tick; define and test calendar behavior for the seven-year tier and prove clock rollback cannot resurrect a tombstone.
  - [ ] Test concurrent equivalent first writers, concurrent different writers, expiry/compaction/lookups racing, old expired key versus a fresh new key, and multiple hosts sharing the same real state store. Exactly one eligible request executes.
  - [ ] Test crash/cancel windows from reservation through external dispatch, durable event append, result finalization, unknown-outcome recovery, replay compaction, and host restart.
  - [ ] Assert no second events, provider/readiness/credential/target calls, content/path/delete staging, Git/commit work, projections, audits, reconciliation scheduling, or new ledger records on replay, conflict, or expiry as applicable.
  - [ ] Inspect persisted records, events, logs, traces, metrics, exceptions, diagnostics, Problem Details, and adapter output with the leakage corpus. Tombstones and diagnostics must contain no raw key, fingerprint, payload/result, path/content/diff, repository/ref, credential/token, commit message, provider body, or protected prior-intent hint.

- [ ] Produce production evidence, close governance, and preserve regression gates. (AC: 8-10)
  - [ ] Run the narrow Folders core/server/contracts/client/CLI/MCP tests, EventStore platform tests in its owning repository, and production-path live-sidecar/restart/concurrency lane with persisted state assertions.
  - [ ] Regenerate and validate the Contract Spine hash, C13 snapshot/version/digest, SDK drift checks, canonical error catalog, retention evidence, and OQ8 evidence package.
  - [ ] Obtain and record dated Architecture, Security, and Test approval of the final evidence version/digest before marking OQ8 or this story done.
  - [ ] Re-run Story 3.10 AC7 expiry/provider-no-touch coverage against the durable path; Story 3.10 remains blocked until this evidence exists and is not expanded or silently marked done here.
  - [ ] Run `git diff --check` in each owning repository and report exact commands/results. Do not commit, push, bump dependencies, or mutate submodule pins unless separately authorized.

## Dev Notes

### Source Context and Authority

- PRD FR41 requires every mutating Contract Spine operation to provide live equivalent replay and expired-key rejection without duplicate effects; FR42 fixes live conflict, expired-key precedence regardless of submitted intent, and read-key rejection; FR44 fixes code `idempotency_key_expired`, non-retryability with the old key, and `refresh_state_then_submit_with_new_key`. [Source: `_bmad-output/planning-artifacts/prd.md#Functional Requirements`]
- Approved 2026-07-15 Proposal 4.6 expands idempotency to all current/future mutations, fixes the four live/expired intent outcomes, and requires replay-result retention to be separate from minimal consumed-key evidence. [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-15.md#4.6 All-Mutations Idempotency and Expired-Key Precedence`]
- OQ8 remains open until Architecture, Contract Spine, generated SDK, C13, storage/retention evidence, every mutation/read cell, and dated Architecture/Security/Test approvals agree. [Source: `_bmad-output/planning-artifacts/prd.md#Open Release Items`]
- Architecture A-9/D-7 and its process pattern are stale: they still describe a subset of mutations and two replay-result TTL tiers without the consumed-key lifecycle. They are inputs to update, not permission to delete an expired record. [Source: `_bmad-output/planning-artifacts/architecture.md#Data Architecture`; `_bmad-output/planning-artifacts/architecture.md#API & Communication Patterns`; `_bmad-output/planning-artifacts/architecture.md#Process Patterns`]
- Epic 12 and Stories 12.1-12.5 are ratified in sprint status by the approved 2026-07-14 durable-product correction but are not yet synchronized into canonical `epics.md`. This dedicated story is authoritative once created; the planning inventory drift remains a separate reconciliation action. [Source: `_bmad-output/implementation-artifacts/sprint-status.yaml#Epic 12`; `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-15.md#4.10 Portfolio, Story Authority, and Metadata Consistency`]

### Dependencies and Execution Order

1. Resolve Task 0 and obtain the named design/retention/error-mapping approvals.
2. Deliver the EventStore capability in the EventStore repository under its own workflow and explicit authority; release or pin it through the root-declared dependency mechanism.
3. Complete Story 12.1's durable Folder repository/replay prerequisite and add the missing durable Organization persistence coverage.
4. Integrate the approved platform seam into Folders, synchronize contracts/generated surfaces, and execute the generated OQ8 matrix.
5. Produce OQ8 evidence/approvals, then resume Story 3.10 AC7.

The sprint's existing HXF-OPS-001 instruction/CI blocker remains a prerequisite to starting Epic 12 code. Creating this story does not authorize starting code on a known-red baseline.

### Current Platform Defect to Remove

- At EventStore baseline `539dca2b`, `IdempotencyChecker.ClassifyAsync` calls `TryRemoveStateAsync` when `ExpiresAt <= now` and returns `IdempotencyCheckOutcome.Expired`.
- `AggregateActor` handles exact duplicate/recoverable/migration and identity conflict, but it does not terminate on `Expired`; processing continues toward pipeline/domain execution. A later request then sees a miss. This is the exact old-key-reuse defect OQ8 forbids.
- `CommandProcessingIdentity` proves only message ID, causation ID, and command type. Folders' gateway currently sets message ID to the idempotency key and normalizes causation from it, so same key + same command type + different semantic payload can be returned as a cached duplicate before the Folders fingerprint ledger sees it.
- `IdempotencyRetentionOptions` supplies one global 24-hour terminal duration. It does not select the Contract Spine's mutation versus commit tiers or preserve a separate consumed-key record.
- EventStore actor state is scoped by aggregate identity. OQ8's tenant-scoped key rule therefore needs an approved platform partition/admission design so same-tenant key reuse against a different target cannot bypass conflict detection.

Likely EventStore update surfaces include `SubmitCommandRequest`/envelope/pipeline contracts, a domain-neutral trusted idempotency-descriptor/admission seam, `CommandProcessingIdentity`, `IIdempotencyChecker`, `IdempotencyChecker`, outcomes/results/records/retention options, `AggregateActor`, gateway reason mapping, configuration, actor tests, and live-sidecar tests. Exact names are architecture-owned; behavior is normative.

### Current Folders State

- `IFolderRepository.TryGetIdempotencyFingerprint` returns only `Missing`, `Found`, or `Unavailable`; `InMemoryFolderRepository` stores an untimed `stream|key -> fingerprint`. No expiry timestamp, replay-result lifetime, consumed-key tombstone, or production durability exists.
- Organization provider binding and ACL repositories use separate bool/fingerprint paths, remain in-memory, and cannot express expired or unavailable consistently. `ConfigureProviderBinding` is one of the 14 public mutations, so Organization persistence cannot remain an exception.
- `FolderResultCode`, Organization result codes, `FolderCanonicalErrorMapper`, OpenAPI, generated clients, CLI/MCP mappings, and C13 contain live conflict but no runtime `idempotency_key_expired` result.
- `RepositoryBackedFolderCreationService` performs readiness work before its local ledger lookup; `WorkspaceFileMutationService` obtains path-policy evidence before lookup. The new common ordering must prevent those calls on authorized replay/conflict/expired paths after safe validation.
- Story 12.1 is the production durability prerequisite. In-memory fakes can verify logic but cannot establish Story 12.6 completion.

### Generated Contract Denominator

The current C13 snapshot derives 49 operations from the Contract Spine. The current 14 mutations are:

`AddFile`, `ArchiveFolder`, `BindRepository`, `ChangeFile`, `CommitWorkspace`, `ConfigureBranchRefPolicy`, `ConfigureProviderBinding`, `CreateFolder`, `CreateRepositoryBackedFolder`, `LockWorkspace`, `PrepareWorkspace`, `ReleaseWorkspaceLock`, `RemoveFile`, and `UpdateFolderAclEntry`.

The current 35 reads are:

`GetFolderLifecycleStatus`, `ListFolderAclEntries`, `GetEffectivePermissions`, `GetProviderBinding`, `ValidateProviderReadiness`, `GetProviderSupportEvidence`, `GetRepositoryBinding`, `GetBranchRefPolicy`, `GetWorkspaceLock`, `GetWorkspaceRetryEligibility`, `GetWorkspaceTransitionEvidence`, `ListFolderFiles`, `GetFolderFileMetadata`, `SearchFolderFiles`, `SearchFolderIndexedFiles`, `GetFolderIndexingStatus`, `GlobFolderFiles`, `ReadFileRange`, `GetWorkspaceStatus`, `GetWorkspaceCleanupStatus`, `GetTaskStatus`, `GetCommitEvidence`, `GetProviderOutcome`, `GetReconciliationStatus`, `ListAuditTrail`, `GetAuditRecord`, `ListOperationTimeline`, `GetOperationTimelineEntry`, `GetReadinessDiagnostics`, `GetLockDiagnostics`, `GetDirtyStateDiagnostics`, `GetFailedOperationDiagnostics`, `GetProviderStatusDiagnostics`, `GetSyncStatusDiagnostics`, and `GetProjectionFreshness`.

These lists describe the baseline for review. Tests must enumerate the live generated inventory so later operations cannot bypass the rule.

### Previous Story Intelligence

- Story 4.11 established shared mutation-envelope validation, live equivalent replay/live conflict behavior inside current services, read-key rejection, no-duplicate-effect assertions, leakage checks, append-conflict reread, and C13 parity foundations. Reuse those tests/helpers rather than creating a parallel contract parser.
- Story 4.11 did not implement retention, expired-key precedence, consumed-key evidence, commit-tier selection, production restart durability, or the gateway-level canonical-intent comparison.
- Story 3.10 correctly halted AC7: its repository flow can prove live replay/conflict and provider no-touch, but the authoritative lookup has no expired outcome. Its restart-named tests recreate orchestration while retaining one in-memory repository and therefore do not prove durable restart behavior.
- Story 3.10 remains in progress and blocked; do not mark its expiry task or story complete until the 12.6 production path passes.

### Architecture and State-Management Guardrails

- Hexalith.EventStore is the mandated domain-state platform. Do not hand-roll Dapr state, direct database persistence, file/blob storage, or an independent ledger in Folders. Use EventStore domain services/actors and `IReadModelStore` only for persisted read models.
- The EventStore repository forbids unsolicited submodule edits. Platform changes must be made in its owning repository with explicit authorization, its own baseline/project context, focused tests, senior review, and package/pin workflow.
- Dapr's current state-management documentation says TTL-expired state cannot be retrieved after expiration. Therefore TTL on the full replay record cannot itself preserve OQ8's consumed-key knowledge; a separately retained tombstone or equivalent platform record is required. Dapr also supports atomic transactional write sets and optimistic concurrency, but the selected component capability must be verified rather than assumed. [Source: https://docs.dapr.io/developing-applications/building-blocks/state-management/state-store-ttl/; https://docs.dapr.io/developing-applications/building-blocks/state-management/state-management-overview/]
- Use the repository-pinned Dapr/.NET versions. No package or framework upgrade is required by this story.

### Security and Leakage Rules

- Authorization must complete before replay result or key disposition is revealed. Ledger lookup must remain tenant-scoped and cannot become a cross-target existence oracle.
- The consumed-key record may prove that a key is no longer reusable, but it must not preserve or disclose protected semantic intent. A digest format is not automatically safe: keying, collision handling, rotation, lookup partitioning, and observability all require Security approval.
- Expired-equivalent and expired-different responses must be indistinguishable apart from approved correlation data.
- Never record raw keys or sensitive intent in logs, traces, metrics labels, error details, audit output, persisted tombstones, or `ToString()` output.

### Testing Strategy

- Generate the functional matrix from the Contract Spine/C13 inventory rather than adding 14 bespoke hand-maintained lists.
- Use `TimeProvider`, deterministic boundary instants, and actual shared state-store instances. An in-memory dictionary plus a recreated service object is not restart evidence.
- For Tier 2/3 platform tests, assert persisted state: partition/digest, schema version, state/disposition, approved timestamps/tier, replay payload presence before compaction, payload absence after compaction, and tombstone survival. A returned status alone is insufficient.
- Test concurrent equivalent/different first writers and expiry/compaction races through the real admission path. Lookup followed by external work followed by append is not sufficient atomicity across replicas.
- Run focused project tests individually; do not use solution-level `dotnet test`. EventStore requires `ConfigureAwait(false)` and treats warnings as errors.

### Files Likely to Change

- Planning/architecture/evidence: `_bmad-output/planning-artifacts/architecture.md`, `docs/adrs/0004-per-command-canonical-idempotency.md`, `docs/exit-criteria/c3-retention.md`, `docs/exit-criteria/c0-c13-governance-evidence.yaml`, and an OQ8 evidence artifact selected in Task 0.
- Contract/generation: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`, extension vocabulary, `docs/contract/idempotency-and-parity-rules.md`, `docs/operations/canonical-error-catalog.md`, `tests/tools/parity-oracle-generator/Program.cs`, `tests/fixtures/parity-contract.schema.json`, governed snapshots, generated client/helper outputs, and contract tests.
- Folders runtime: Folder and Organization repository/outcome/result abstractions, services/gates, `FoldersDomainServiceEndpoints`, `FolderDomainProcessor`, `FolderCanonicalErrorMapper`, dependency injection, CLI/MCP projections, and focused tests.
- EventStore prerequisite: contracts/envelope/admission, actor idempotency state machine, retention configuration, gateway error mapping, migration, tests, and live-sidecar evidence in the EventStore repository.

### Do Not Touch / Do Not Claim

- Do not expand Story 3.10 or implement provider behavior in this story beyond no-touch/regression evidence.
- Do not add a second idempotency ledger, a Folders direct Dapr state store, or an in-memory production fallback.
- Do not trust caller-provided digest/tier/expiry metadata or let clients select retention durations.
- Do not delete an expired record before durable tombstone replacement, and do not map expired or corrupt state to `Missing`.
- Do not hand-edit generated SDK/C13 artifacts.
- Do not claim OQ8 closed, durable restart behavior, or production capability from source-text, fake-only, seed-only, unavailable, or structurally asserted evidence.
- Do not modify or bump the EventStore submodule without explicit authorization and the owning repository workflow.

## Project Structure Notes

- This is a cross-repository capability coordinated from the Folders product story. Folders owns its Contract Spine, generated surfaces, canonical intent adapter, runtime mapping, and OQ8 evidence. EventStore owns durable admission/tombstone mechanics. Story 12.1 owns the Folders durable repository/replay prerequisite.
- No UI feature is required. Any generated UI/client model change is contract propagation only; the operations console remains read-only.
- Canonical `epics.md` still omits Epic 12 despite approved sprint registration. Preserve this story as the dedicated authority and route portfolio synchronization separately rather than editing unrelated epic history during implementation.

### References

- [Source: `_bmad-output/planning-artifacts/prd.md#Command and Query Contract`]
- [Source: `_bmad-output/planning-artifacts/prd.md#Functional Requirements`]
- [Source: `_bmad-output/planning-artifacts/prd.md#Open Release Items`]
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-15.md#4.6 All-Mutations Idempotency and Expired-Key Precedence`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Data Architecture`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#API & Communication Patterns`]
- [Source: `docs/adrs/0004-per-command-canonical-idempotency.md`]
- [Source: `docs/contract/idempotency-and-parity-rules.md`]
- [Source: `docs/exit-criteria/c3-retention.md`]
- [Source: `tests/fixtures/parity-contract.yaml`]
- [Source: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`]
- [Source: `_bmad-output/implementation-artifacts/4-11-propagate-idempotency-keys-correlation-and-task-ids.md`]
- [Source: `_bmad-output/implementation-artifacts/3-10-github-repository-provisioning-binding-and-branch-ref-behavior.md`]
- [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Actors/IdempotencyChecker.cs`]
- [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`]
- [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Actors/CommandProcessingIdentity.cs`]
- [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Actors/IdempotencyRetentionOptions.cs`]
- [Source: https://docs.dapr.io/developing-applications/building-blocks/state-management/state-store-ttl/]
- [Source: https://docs.dapr.io/developing-applications/building-blocks/state-management/state-management-overview/]

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-07-19: Created via BMAD create-story workflow from the final PRD, approved 2026-07-15 all-mutations/expired-key correction, architecture A-9/D-7, C3 evidence, Contract Spine/C13 inventory, Stories 4.11 and 3.10, current Folders repositories/services/mappers, current EventStore actor idempotency code, repository project contexts, recent Git history, and current official Dapr state TTL/concurrency documentation.
- 2026-07-19: Discovery loaded whole planning artifacts where required and selectively loaded the authoritative OQ8/retention/architecture sections; no sharded planning artifacts were present. Epic 12 remains approved in sprint status but absent from canonical `epics.md`.
- 2026-07-19: Three read-only analysis agents independently audited product requirements/placement, current Folders/EventStore code and platform gaps, and prior-story/test evidence. No agent edited files.
- 2026-07-19: Validation added hard gates for the unapproved consumed-key lifetime and error projections, EventStore owning-repository work, Story 12.1 plus durable Organization persistence, authorization-before-disclosure, generated 14-mutation/35-read completeness, real restart/concurrency/state-store evidence, and OQ8 approval metadata.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Story 12.6 registered as the dedicated OQ8 implementation authority without expanding Story 3.10.
- Story is ready to begin Task 0; production code remains gated by named architecture, retention, platform, and governance decisions.
- Story 3.10 remains in progress and blocked on this substrate.

### File List

- `_bmad-output/implementation-artifacts/12-6-implement-durable-all-mutations-idempotency-and-expired-key-precedence.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

### Change Log

- 2026-07-19: Created Story 12.6 for EventStore-owned durable all-mutations admission, canonical intent conflicts, consumed-key tombstones, expired-key precedence, generated C13 completeness, cross-surface parity, production evidence, and OQ8 approval closure.
