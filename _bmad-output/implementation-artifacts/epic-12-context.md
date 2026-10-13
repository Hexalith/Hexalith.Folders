# Epic 12 Context: Durable Repository-Backed Round Trip

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Authorized developers and AI agents can persist folder lifecycle and file content across restart, read authoritative content, complete a provider-confirmed Git commit, observe terminal task state, and recover asynchronous indexing delivery across replicas. This durable data plane is an MVP foundation; NoOp, unavailable, in-memory, seed-only, and fake-backed substitutes cannot establish completion.

## Stories

- Story 12.1: EventStore-backed folder repository, retire NoOp, and implement projection replay
- Story 12.2: Durable projections and task-completion pipeline
- Story 12.3: Durable workspace file-content store and content-read source
- Story 12.4: Real Git commit executor and provider write path
- Story 12.5: At-least-once Memories egress and reconciler
- Story 12.6: Implement durable all-mutations idempotency and expired-key precedence
- Story 12.7: Protect Confidential Operational Values

## Requirements & Constraints

- Accepted mutations, transitions, task completion, checkpoints, and delivery intent survive restart and converge across supported replicas. Empty-checkpoint replay rebuilds current-semantic read models; readiness reports actual dependency health.
- Establish fresh tenant and folder authority before protected reads, disclosures, or effects. Wrong-tenant requests, stale authority, unavailable or corrupt stores, timeouts, conflicts, and unsupported events produce safe canonical results and metadata-only evidence.
- Persist staged and committed bodies in one authoritative content store; events remain metadata-only. Verify server-side hashes and byte/media metadata; enforce task, lock, version, and file policy. Removed content is unavailable, and context reads use this authority rather than an index.
- One lock-owning task produces one eligible provider-confirmed commit on its bound GitHub or Forgejo remote/ref. Unknown post-dispatch outcomes require bounded read-only confirmation, then reconciliation if unresolved; never blindly repeat effects. Indexing failure cannot roll back committed Folders truth.
- Every Contract Spine mutation requires durable tenant-scoped idempotency; reads reject keys. Live equivalent intent replays the logical result after current authorization; live different intent conflicts. Both expired cases return `idempotency_key_expired` before protected work without exposing prior intent or executing anew.
- Replace confidential operational values with immutable tenant-scoped correlation tokens before admission or persistence. No durable cleartext, reversible form, or reconstruction material enters events, state, working copies, audit, telemetry, outbox, broker messages, bridge state, indexes, exports, backups, or retries. Cleartext-dependent operations fail before admission.
- Completion requires persisted-state, restart, replay, denial, multi-replica, retention, and real-provider evidence. The durable vertical-slice gate does not replace MVP coverage of both providers across REST, SDK, CLI, and MCP.

## Technical Decisions

- EventStore is the sole domain writer; aggregate state and event append are transactional. One append conflict permits at most one full authorization-and-domain re-evaluation; no blind append, unbounded retry, or provider-effect replay. Workers own external provider and working-copy effects.
- Use EventStore 6.6 metadata V1 with full CLR `EventTypeName` as an immutable storage identifier. `PayloadVersion` is distinct from envelope `MetadataVersion`; absent or 1 means payload version 1, and versions 2–1024 are stamped. Registered platform upcasters handle supported evolution without rewriting retained bytes. Unknown events and unsupported versions fail before state or projection advancement.
- Lifecycle/task state and checkpoints are durable. Working copies are disposable caches rebuilt from authoritative content and provider state; replay cannot create or erase consumed-key authority. Transition, diagnostic, and search projections remain owned by their consuming epics.
- EventStore admission serializes reservation, fencing, descriptor comparison, recovery, replay, and expiry across replicas using managed tenant plus protected key digest. Registered adapters supply trusted versioned semantic descriptors after authorization and validation; callers cannot choose digest or retention tier.
- Replay retention begins at terminal finalization: 24 hours for non-commit mutations and seven calendar years for commits. Persist a monotonic time floor; unresolved states never expire into fresh work. Inclusive expiry atomically compacts result/intent into a minimal consumed-key tombstone retained for tenant lifetime plus 400 days after approved deletion, subject to legal hold.
- Compose provider-private mutation, commit, and status adapters. Memories publication preserves ordered durable intent and stable CloudEvent identity. Alias-safe HMAC rotation preserves the immutable canonical token. Confidential targets require proven `opaque-handle-restartable` capability; `cleartext-required` fails with `confidential_operation_not_durable` before admission.

## UX & Interaction Patterns

The read-only console distinguishes staged work, confirmed commits, unconfirmed provider outcomes, freshness, and recovery without displaying bodies or diffs. Unknown outcomes show bounded automatic confirmation before human escalation, with no retry or repair affordance. Confidential values expose only a safely copyable correlation reference marked withheld; withheld, redacted, missing, unknown, and unavailable remain distinct through text, accessible labels, and non-color cues.

## Cross-Story Dependencies

The historical EventStore 6.6 capability acceptance binds revision `ead4949d30b37d3a976f5c9197789a91701f1a72`. The current consumer compatibility supplement, `_bmad-output/implementation-artifacts/story-12-1-eventstore-consumer-compatibility-2026-10-13.yaml`, verifies gitlink `2a7b80a4ec639bdba7ff8acc74cacb4f8880b24c`. The prior 2026-10-12 supplement remains historical evidence for gitlink `851984f2362ba0b00972da4d03dab8be7e5ef4cb`. These records verify source/SDK compatibility, not deployed durability. Story 12.1 retains the accepted OQ1 lock-timing, OQ2 file-policy, OQ4 provider-compatibility, and approved A6b v2 authorization-matrix prerequisites; its separate execution decision authorizes Story 12.1 only. The current manifest records `general_execution_hold: false`, `v2_exposure_authorized: false`, and `sprint_status_regeneration_authorized: false`; historical approval records and other story authorizations remain unchanged. Delivery follows manifest prerequisites and execution ranks.

Stories 12.2 and 12.3 consume 12.1 streams. Story 12.6 requires accepted OQ8 design; runtime evidence follows implementation. Story 12.7 follows 12.1 and precedes 12.4/12.5. Story 12.4 consumes provider adapters from 3.11/3.13; 12.5 consumes C9-safe documents from 10.6 and Epic 9 routing. Epics 4, 6, and 10 own consuming projections. Story 4.23 consumes 12.1/12.2/12.6/12.7 for canonical cross-folder lock identity. Production recovery remains an external prerequisite for dependent hardening work.

Story 12.1's authorized source-validation continuation and independent review are complete; the sprint handoff is `review`. Current candidate technical hashes were refreshed without changing historical approvals or exposure controls. The recorded Aspire startup timeout and historical live-proof limits remain; this handoff adds no fresh deployed acceptance.
