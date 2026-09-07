---
title: 'Story 12.6: Wire durable EventStore admission on Folders mutation submit'
type: 'feature'
created: '2026-09-07'
status: 'done'
baseline_commit: 'f3337bf05fe2b53abededd0489151f6b0afb1703'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '_bmad-output/implementation-artifacts/epic-12-context.md'
  - '_bmad-output/implementation-artifacts/12-6-implement-durable-all-mutations-idempotency-and-expired-key-precedence.md'
  - 'docs/exit-criteria/oq8-idempotency-design.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Folders REST still submits mutations with `MessageId` equal to the caller idempotency key and omits `SubmitCommandRequest.IdempotencyKey`, so EventStore 3.102.0 never admits. Providers keep hard-coding `Fresh`.

**Approach:** Register Folders trusted canonical-intent adapters on the EventStore command host, send a ULID `MessageId` plus the opaque `IdempotencyKey` after auth/validation, and pass EventStore admission into provider work instead of `Fresh`.

## Decisions

- **Split:** Admission on the mutation submit path only. Contract leftovers and the OQ8 matrix stay deferred. Story 12.6, OQ8, and Story 3.10 stay in-progress.
- **Envelope:** `IdempotencyKey` is the caller opaque key; `MessageId` is a new ULID. Do not send the key until adapters resolve on the command host.
- **Adapter grain:** One adapter per `FoldersServerModule` mutation `CommandType` (Grant/Revoke separate; Add/Change/Remove share `MutateFiles`). `CommitWorkspace` uses the commit tier; others use the mutation tier.
- **Packaging:** Folders.Server stays on EventStore DomainService, Client, and Contracts. It must not reference `Hexalith.EventStore.Server`.
- **EventStore seam now (Q1-A):** EventStore work is authorized in this session through the root-declared `references/Hexalith.EventStore` submodule. Expose `IIdempotencyIntentAdapter` and `AddIdempotencyIntentAdapter` from DomainService (or equivalent), compose Folders adapters into the EventStore gateway/command host, then Folders sends `IdempotencyKey`. Do not wait for a NuGet pin.
- **Sole ledger (Q2-A):** EventStore is the only mutation admission authority. Bypass `TryGetIdempotencyFingerprint` on those mutation paths so the in-memory folder/org ledger cannot disagree.

## Boundaries & Constraints

**Always:** Auth and canonical validation before admission. EventStore owns reservation, fence, replay, conflict, expiry, and tombstones. Fail closed on missing adapter, unavailable admission, or corrupt/legacy records. Map `idempotency_admission_unavailable` as 503, not conflict or expired. Keep the shipped expired-key mapping. Metadata-only errors; never log raw keys, fingerprints, payloads, paths, refs, or tokens.

**Never:** A Folders-owned idempotency store or dual fingerprint ledger on admitted mutations. Hand-edit generated SDK/C13. Close OQ8 or Stories 12.6 / 3.10. Implement Story 12.1. Rewrite read-key rejection. Expand docs inventory or generator leftovers. Send `IdempotencyKey` without a registered adapter for that `CommandType`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| First write | Adapters registered, new key | EventStore `Execute`; processor once; keyed ULID envelope | N/A |
| Live equivalent | Same tenant+key, equivalent intent, authorized | EventStore `Replay`; no second processor/readiness/provider/path/Git | Denial if auth now fails; record unchanged |
| Live different | Same tenant+key, different intent | 409 `idempotency_conflict`; no execution | No prior-intent leak |
| Expired | Consumed/expired key, any intent | 409 `idempotency_key_expired` | Do not execute as fresh |
| No adapter | Keyed submit, command type unregistered | 503 `idempotency_admission_unavailable`; no domain/provider work | Do not map to conflict/expired |
| Provider first write | Bind/provision after `Execute` | `ProviderIdempotencyAdmission` is not `Fresh` | DW-295/296 seam required |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs` — 12 `SubmitCommandRequest` sites set `MessageId: idempotencyKey` and omit `IdempotencyKey`. `SafeGatewayReasonCode` does not allow-list `idempotency_admission_unavailable`.
- `src/Hexalith.Folders.Server/FoldersServerModule.cs` — adapter `CommandType` strings; `/process`.
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs` — gateway client + `FolderDomainProcessor`; no adapter DI. `.csproj` is DomainService/Client/Contracts only.
- `src/Hexalith.Folders.AppHost/Program.cs:20` — command host is `AddHexalithEventStoreGatewayProject`, not Folders.Server.
- `src/Hexalith.Folders/Aggregates/Folder/RepositoryBindingService.cs:191-195` — hard-coded `Fresh`. `RepositoryProvisioningProcessManager.cs:79-84` — `Fresh` fallback.
- Folder/Org mutation services still call `TryGetIdempotencyFingerprint`; some do readiness/path before that lookup.
- EventStore `3.102.0` (`4ae9cee1`): adapters live in `Hexalith.EventStore.Server`. `SubmitCommandHandler` admits only when `IdempotencyKey` is set. `AddEventStoreDomainAdmissionStage` is domain auth stages, not this seam.

## Tasks & Acceptance

**Execution:**
- [x] EventStore DomainService (or approved equivalent) — expose adapter registration on the gateway/command host Folders actually runs.
- [x] Folders trusted adapters — one per mutation `CommandType`; `CreateIntent` from Contract Spine fields only.
- [x] Compose adapters into that EventStore host — unknown/duplicate adapters fail closed at startup.
- [x] `FoldersDomainServiceEndpoints.cs` — ULID `MessageId` + `IdempotencyKey`; map `idempotency_admission_unavailable` as 503.
- [x] `RepositoryBindingService.cs` and `RepositoryProvisioningProcessManager.cs` — stop defaulting `Fresh` once admission is on the path.
- [x] Mutation services/gates — bypass `TryGetIdempotencyFingerprint` on admitted mutation paths so the in-memory ledger is not an admission authority.
- [x] Focused Server/domain/worker tests for the I/O matrix. No generated OQ8 matrix.

**Acceptance Criteria:**
- Given this slice lands, when sprint status is inspected, then Story 12.6 and OQ8 stay in-progress and Story 3.10 is not marked done.

## Implementation Notes

- 2026-09-07: EventStore `IIdempotencyIntentAdapter` / `AddIdempotencyIntentAdapter` moved to DomainService; Folders command host is `src/Hexalith.Folders.EventStore` with 13 adapters and a fail-closed catalog. REST submits ULID `MessageId` + opaque `IdempotencyKey`. Binding uses `Execute`; provisioning requires a caller-supplied admission (null is mismatch, not `Fresh`). `TryGetIdempotencyFingerprint` removed from admitted mutation paths; `AppendIfFingerprintAbsent` remains append-time only.
- 2026-09-07 review patches: CreateRepositoryBackedFolder reads `branchRefPolicy.repositoryBindingId`; MutateFiles canonicalizes `pathMetadata`; provisioning rejects non-Execute; Forgejo Execute tests added.
- 2026-09-07 post-review verification: Folders EventStore adapters 12/12, Workers process-manager 17/17, Forgejo 100/100, `git diff --check` clean. Story 12.6 / OQ8 / 3.10 left in-progress.

## Spec Change Log

## Review Triage Log

- `false` — Blind: missing `*|folders|v1` registration. EventStore convention fallback is `AppId = domain`, `MethodName = process`; Folders app id is `folders`. The copied EventStore `appsettings.json` also omits a folders registration.
- `false` — Blind/edge: `RepositoryBindingService` hard-codes `Execute` so Replay/Conflict/Expired never reach the provider. Those decisions never invoke `/process`; only Execute/Recoverable do.
- `defer` — Blind: `RepositoryProvisioningProcessManager` has no production `HandleAsync` caller. Pre-existing; this slice only removed the `Fresh` fallback.
- `defer` — Blind/edge: readiness/path/Git still run before `AppendIfFingerprintAbsent`. EventStore Replay is what skips `/process`; first Execute still must do that work.
- `defer` — Blind/edge: `AppendIfFingerprintAbsent` remains a residual in-memory append-time check after EventStore admission. Pre-existing store; lookup fast-path was the Q2-A removal.
- `high` — Blind: `CreateRepositoryBackedFolderIdempotencyIntentAdapter` reads root `repositoryBindingId`, which is not on the request. Spine field is `branch_ref_policy.repository_binding_id` (`branchRefPolicy.repositoryBindingId`). Different bindings can look equivalent.
- `medium` — Blind/edge: `MutateFilesIdempotencyIntentAdapter` fingerprints `pathMetadata` via `GetRawText()`, so property order changes the digest. Spine lists `path_metadata`.
- `false` — Blind/edge: Grant/Revoke only hash `operations[0]`. REST always submits a one-element operations array from `UpdateFolderAclEntry`.
- `medium` — Blind/VG: non-CreateFolder adapters have no `CreateIntent` tests. Catalog tests never call `CreateIntent`.
- `false` — Blind: REST matrix is envelope-only. Archive maps 409 conflict, 409 expired, and 503 `idempotency_admission_unavailable`; EventStore `TrustedIdempotencyIntentTests` cover Replay/conflict at admission.
- `defer` — Blind: `OrganizationAclTenantGate` still looks up fingerprints. Not one of the 13 admitted Folders command types.
- `defer` — Blind: EventStore submodule is dirty (`4ae9cee1-dirty`). Pin/commit is git workflow, not runtime.
- `false` — Blind: unrelated Story 3.13 Forgejo transport edits. Not in this diff. AppHost Memories `AddDaprComponent` is required to compile current Memories.Aspire.
- `false` — Blind: empty Spec Change Log / missing CI image notes. Change log waits for a spec loopback; deploy/CI was not this slice.
- `false` — Edge: empty/non-JSON payload → 503. Adapter throw is fail-closed `idempotency_admission_unavailable`.
- `medium` — Edge/VG: provisioning still dispatches when context admission is `Fresh` with a matching fingerprint. Spec first-write is `Execute`, not `Fresh`.
- `false` — Edge: `TryAddEnumerable` swallows a second same `TAdapter`. Duplicate command types still fail in `FoldersIdempotencyIntentAdapterCatalog`.
- `medium` — VG: Forgejo create/bind tests still send `Fresh`. Dropping the `Execute` arm would expire Forgejo first-write while GitHub tests stay green.
- `medium` — VG: process-manager tests do not pin null admission, fingerprint mismatch, or a non-default forwarded disposition.
## Design Notes

`AddIdempotencyIntentAdapter` must run in the EventStore gateway host. Folders.Server only submits through `IEventStoreGatewayClient` and later handles `/process`.

## Verification

**Commands:**
- Focused `Hexalith.Folders.Server.Tests` / domain / worker tests for the I/O matrix -- expected: listed tests pass
- `git diff --check` -- expected: clean on files this spec changes
