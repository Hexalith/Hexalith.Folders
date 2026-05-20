# ADR 0001: Folder domain processor persistence ownership

Date: 2026-05-20

## Status

Accepted for Story 2.8b implementation.

## Context

Story 2.8 added archive command validation, decision-bound idempotency, ACL/policy checks, and append-conflict reread inside `FolderArchiveTenantGate`, but round-2 review found that no production `/process` callback invoked the gate. Story 2.8b exists to wire that callback.

The EventStore `IDomainProcessor` contract returns `DomainResult` events that the EventStore framework persists. The existing Folders gates (`FolderCreateTenantGate`, `FolderAccessTenantGate`, and `FolderArchiveTenantGate`) already persist through `IFolderRepository.AppendIfFingerprintAbsent` so they can atomically pair stream append with the local idempotency fingerprint ledger and race reread behavior.

## Decision

Select Option B: the Folders gates keep persistence ownership, and `FolderDomainProcessor` invokes the relevant gate from `/process`. The processor returns `DomainResult.NoOp()` for gate-owned accepted mutations and idempotent replays so the EventStore framework does not double-write folder events. Domain rejections are returned as a single metadata-only `FolderCommandRejected` rejection event.

Option A was rejected for this story because the current EventStore append path does not expose the same folder-specific idempotency fingerprint ledger and append-conflict reread semantics. Option C was rejected because unifying `IFolderEvent` with `IEventPayload` has a much larger blast radius and is not required to repair the production archive path.

## Result Mapping

`FolderResultCode.Accepted`, `Created`, `IdempotentReplay`, and `AlreadyApplied` map to `DomainResult.NoOp()`.

All other `FolderResultCode` values map to `DomainResult.Rejection([FolderCommandRejected])`. The rejection payload carries only safe metadata: code, tenant/folder/organization when already sanitized by `FolderResult`, correlation/task/idempotency evidence, and command type.

Because EventStore currently drops `ResultPayload` for no-op domain results, accepted archive responses preserve the existing operation/correlation/task evidence but do not depend on `ResultPayload` to signal idempotent replay. A future EventStore enhancement may add no-op result payload support; that is outside this story.

## Cancellation Boundary

`IDomainProcessor.ProcessAsync(CommandEnvelope, object?)` has no `CancellationToken`. IO-bearing work remains in the endpoint/gateway/request-handler path where cancellation is available. The processor and evidence providers used inside it are intentionally bounded, in-memory/scoped operations:

- layered authorization has already run with the request `CancellationToken`;
- archive ACL evidence reads the current scoped layered authorization result;
- baseline archive policy evidence is a deterministic in-memory policy stub;
- gate persistence uses `IFolderRepository`, whose current contract is synchronous.

Extending `IDomainProcessor` with cancellation would require EventStore contract work and is deferred.

## Layered Authorization Evidence Handoff

`FoldersDomainServiceRequestHandler` stores the approved `LayeredFolderAuthorizationResult` in a scoped accessor immediately before invoking the domain processor and clears it in a `finally` block. Evidence providers consume this accessor and never rerun JWT validation or claim-transform checks inside the processor. Sequential requests in the same host must not observe stale authorization evidence.

## Access Gate Impact

`FolderAccessTenantGate` keeps the same gate-owned persistence pattern as archive. This story also aligns its invariant-failure behavior with archive by returning a safe `MalformedEvidence` result instead of throwing when `TenantAccessOutcome.Allowed` arrives with `IsAllowed == false`.

## Consequences

The production archive path now follows REST endpoint -> EventStore gateway -> `/process` -> `FolderDomainProcessor` -> `FolderArchiveTenantGate` -> `IFolderRepository`. This deliberately bypasses EventStore framework event persistence for folder mutation events to preserve the existing Folders idempotency and race semantics. The tradeoff is documented here so a later architecture story can revisit Option A or C deliberately.
