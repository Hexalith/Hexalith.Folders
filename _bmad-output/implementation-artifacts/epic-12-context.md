# Epic 12 Context: Durable Repository-Backed Round Trip

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Authorized developers and AI agents can persist folder lifecycle and file content across restart, read authoritative content, complete a provider-confirmed Git commit, observe terminal task and projection state, and recover asynchronous indexing delivery. This durable data plane is an MVP foundation; NoOp, unavailable, in-memory, seed-only, and fake-backed substitutes cannot establish completion.

## Stories

- Story 12.1: EventStore-backed folder repository, retire NoOp, and implement projection replay
- Story 12.2: Durable projections and task-completion pipeline
- Story 12.3: Durable workspace file-content store and content-read source
- Story 12.4: Real Git commit executor and provider write path
- Story 12.5: At-least-once Memories egress and reconciler
- Story 12.6: Implement durable all-mutations idempotency and expired-key precedence
- Story 12.7: Protect Confidential Operational Values

## Requirements & Constraints

- Accepted folder and organization changes, task completion, locks, projections, and delivery intent must survive restart and converge across supported replicas. Empty-checkpoint replay must rebuild current-semantic read models; readiness reflects actual dependency health.
- Authorize against current managed-tenant authority before disclosing folder, content, provider, commit, idempotency, or index state. Wrong-tenant requests, stale authority, corrupt or unavailable stores, timeouts, conflicts, and duplicate or unsupported events require safe canonical outcomes and metadata-only evidence.
- Persist staged and committed bodies in one authoritative content store outside EventStore. Verify server-side hashes and byte/media metadata; enforce task, lock, version, and file policy. Removed content is unavailable, and context reads use this source rather than a derived index.
- A lock-owning task may produce one provider-confirmed commit on the bound GitHub or Forgejo remote/ref. Unknown post-dispatch outcomes require bounded read-only confirmation and reconciliation, never a blind repeat. Indexing outages cannot roll back committed Folders truth; durable ordered egress must reconcile duplicates and missed delivery.
- Every Contract Spine mutation requires durable tenant-scoped idempotency, while reads reject keys. An unexpired equivalent request returns the same logical result after current authorization; a different intent conflicts. At expiry, either intent returns idempotency_key_expired before protected work, without revealing prior intent or executing as new work.
- Confidential operational values become immutable tenant-scoped correlation tokens before admission or any durable write. No durable cleartext, reversible form, or reconstructable material may enter events, state, working copies, audit, telemetry, outbox, indexes, exports, backups, or retry evidence. Operations requiring recoverable cleartext fail before admission.
- Completion needs deployed restart, replay, denial, multi-replica, real-provider, retention, and recovery evidence. The durable vertical-slice gate does not replace broader MVP provider and surface parity.

## Technical Decisions

- EventStore is the sole domain writer: REST gateway, processor, authorization gate, then repository. Append versioned, metadata-only events using stable logical URI types; validate and upcast retained history without rewriting it. One append conflict permits at most one full authorization-and-domain re-evaluation. Workers perform provider, Git, and working-copy effects.
- Lifecycle, task, and checkpoint state is durable. Projection replay cannot create or erase consumed-key authority. Working copies are disposable caches rebuilt from durable content and provider state. Product transition, diagnostic, and search projections belong to their consuming epics.
- EventStore owns admission across replicas, partitioned by managed tenant and protected opaque-key digest. After authorization and canonical validation, a registered adapter supplies a trusted versioned intent descriptor. Reservation, fencing, descriptor comparison, terminal result, recovery, expiry, and compaction are serialized; pending and unknown outcomes cannot trigger blind repeat effects.
- Replay-result retention starts at terminal finalization: 24 hours for non-commit mutations and seven calendar years for commits. A persisted monotonic time floor prevents clock rollback from extending validity. At the inclusive expiry boundary, atomically replace result and intent digest with a minimal consumed-key tombstone retained for tenant lifetime plus 400 days after approved deletion, subject to legal hold. Unreadable or unrecognized admission state fails closed.
- The Git executor composes registered provider-private mutation, commit, and status adapters. Memories egress uses durable commit-then-append intent and stable CloudEvent identity. A versioned tenant-scoped HMAC tokenizer preserves one canonical token across rotation through lookup aliases; provider operations must prove an opaque-handle-restartable capability before admitting confidential targets.

## UX & Interaction Patterns

The read-only operations console distinguishes provider-confirmed durable state from staged or unconfirmed work, and shows projection freshness and recovery status without file bodies or diffs. It shows a confidential value only as a safe-copy correlation reference with withheld status. Withheld, redacted, missing, unknown, and unavailable remain distinct in text, accessible labels, and non-color cues.

## Cross-Story Dependencies

Story 12.1 requires the external EventStore event-evolution capability, approved v2 authorization matrix, and execution release. Stories 12.2 and 12.3 consume its durable streams. Story 12.6 also requires the accepted OQ8 design; its runtime evidence follows implementation. Story 12.7 follows 12.1 and precedes the provider and Memories consumers in 12.4 and 12.5. Provider adapters and the configured Memories route are additional inputs. Epics 4, 6, and 10 own the consuming transition, diagnostic, and search projections.
