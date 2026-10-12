# Story 12.1: verified human authorization handoff proposal

**Status:** approved by the owner on 2026-10-12; implementation in progress.
**Scope:** EventStore gateway → AggregateActor → Folders `/process` for a human-initiated command.

## Observed gap

The isolated Aspire run recorded in the Story 12.1 spec reached healthy EventStore and Folders resources, then Folders rejected EventStore's `/process` invocation with `authentication_failure` (401). EventStore's `DomainServiceWorkloadAssertionHandler` deliberately strips the human bearer and issues only an EventStore workload assertion. The `DomainServiceRequest` contains the command and actor state, but no verified human identity or permission evidence. Folders' `FoldersDomainServiceRequestHandler` obtains authoritative tenant, principal, and `eventstore:permission` evidence from its authenticated HTTP principal. Its command envelope values are treated as client-controlled. Passing `CommandEnvelope.UserId` or `TenantId` as that evidence would violate the A6b authorization gate.

## Recommended contract

Add a platform-owned, versioned **command-bound human authority receipt**. The EventStore gateway issues it only after validating the caller's human JWT and extracting tenant, subject, and permission claims. The receipt is signed for the Folders domain-service audience and binds those claims to the canonical command identity and digest, issue/expiry times, and a unique invocation identity. It travels in a dedicated trusted field through the actor invocation and `DomainServiceRequest`; it is never accepted from client command payload, extensions, or headers. EventStore's workload assertion remains the separate proof of the invoking service.

Folders `/process` must require both proofs. It verifies the EventStore workload identity and the receipt signature, issuer, audience, lifetime, and exact command binding before constructing a human principal. Its existing layered gate then refreshes tenant and folder authority before any protected state read or append, including the single permitted conflict re-evaluation. Missing, expired, mismatched, replayed outside the same fenced command, or unverifiable receipts fail closed with a canonical metadata-only denial. The receipt must not contain the original bearer token, be written into event/actor/idempotency/audit/log/projection state, or be forwarded to a provider. The platform must define how a pending command gets fresh human authorization after receipt expiry; it may only resume after a newly authenticated caller supplies equivalent intent under the same idempotency key.

## Required implementation and evidence

1. In `references/Hexalith.EventStore`, define the receipt format, issuer, verifier, actor transport, and lifetime/retention rules. Preserve the existing workload assertion and its removal of forwarded human bearers. Prove that a client cannot inject or replay a receipt for another command, tenant, principal, action, or domain.
2. In Folders, bind `/process` to the verified dual-proof context and keep `FoldersDomainServiceRequestHandler`'s command-envelope values untrusted. Prove current tenant and ACL re-evaluation, safe denials, and no read or append after proof failure.
3. Run the deployed Aspire path to a persisted EventStore state-store end state, restart Folders, replay from an empty checkpoint, exercise two replicas and one conflict retry, and verify timeout, idempotency, and leakage behavior. Static inspection found that the local EventStore host had no enabled idempotency key ring; the AppHost now supplies a persistent generated development key, but the prior HTTP 503 `idempotency_admission_unavailable` must be rerun before the idempotency row can pass. Story 12.6's registered adapters alone do not establish a working deployed admission path.

## Decision and current evidence

The owner approved the proposed receipt design on 2026-10-12. The EventStore and Folders source now carries and verifies the command-bound receipt, with focused codec and verifier tests passing. The deployed local run reached the Folders layered gate after receipt verification. Full Story 12.1 acceptance remains open: the live command is denied at the separate effective-permissions gate, and restart, replica conflict, timeout, and persisted folder state have not yet been proven.
