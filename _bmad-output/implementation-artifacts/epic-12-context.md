# Epic 12 Context: Durable Repository-Backed Round Trip

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Authorized developers and AI agents can persist folder lifecycle and file content across restart, retrieve authoritative content, complete a provider-confirmed Git commit, observe terminal task state, and recover asynchronous indexing delivery across replicas. This durable data plane is an MVP foundation; NoOp, unavailable, in-memory, seed-only, safe-empty, and fake evidence cannot establish completion.

## Stories

- Story 12.1: EventStore-backed folder repository, retire NoOp, and implement projection replay
- Story 12.2: Durable projections and task-completion pipeline
- Story 12.3: Durable workspace file-content store and content-read source
- Story 12.4: Real Git commit executor and provider write path
- Story 12.5: At-least-once Memories egress and reconciler
- Story 12.6: Implement durable all-mutations idempotency and expired-key precedence
- Story 12.7: Protect Confidential Operational Values

## Requirements & Constraints

- Accepted mutations, state transitions, task results, checkpoints, and delivery intent survive restart and converge across supported replicas. Empty-checkpoint replay rebuilds current-semantic read models. Readiness reflects actual dependency health.
- Establish current tenant and folder authority before protected reads, disclosures, or effects. Denial, unavailable authority, corrupt stores, timeouts, conflicts, and unsupported events produce canonical, non-disclosing results and metadata-only evidence.
- One authoritative content store supplies staged/committed bodies and context reads. Verify hashes and byte/media metadata; enforce task, lock, version, and file policy. Removed content is unavailable; indexes never become content authority. Events, audit, and telemetry remain metadata-only and never contain file bodies.
- Staging does not auto-commit. During commit execution, one authorized lock-owning task produces exactly one eligible provider mutation and a confirmed commit on the bound GitHub or Forgejo remote/ref. Unknown outcomes permit bounded read-only confirmation, then reconciliation; never blindly repeat effects. Indexing outages cannot roll back committed truth.
- Every mutation uses durable tenant-scoped idempotency; reads reject keys. Equivalent live intent replays after current authorization, different live intent conflicts, and expired keys always fail before protected work without revealing prior intent or becoming new intent.
- Replace confidential values with irreversible tenant-scoped correlation tokens before admission or persistence. No durable cleartext, reversible form, or reconstruction material may enter events, state, working copies, audit, telemetry, outbox, broker messages, bridge state, indexes, exports, backups, or retries.
- Completion requires real persisted-state, restart, replay, denial, multi-replica, retention, and provider evidence. The narrower durable vertical-slice gate does not replace both-provider coverage across REST, SDK, CLI, and MCP.

## Technical Decisions

- EventStore is the sole domain writer. Required concurrency behavior is transactional command-state/event append, with at most one bounded full authorization-and-domain re-evaluation after conflict. Blind append, unbounded retries, and provider-effect replay are forbidden. Exhausted conflict preserves HTTP 409, CLI exit 77, and MCP `concurrency_conflict`.
- Use the accepted EventStore 6.6 evolution contract: immutable full CLR `EventTypeName`, separate envelope `MetadataVersion` and event `PayloadVersion`; absent or 1 means payload version 1 under declared-version rules, while versions 2–1024 carry explicit stamps. Platform upcasters handle supported changes without rewriting retained bytes. Unknown events or unsupported versions fail before state or checkpoint advancement; retained aliases need byte fixtures.
- Lifecycle/task projections and checkpoints are durable. Working copies are disposable caches rebuilt from authoritative content and provider state. Replay cannot create or erase consumed-key authority. Reuse platform SDK persistence/projection seams rather than implementing local platform infrastructure.
- EventStore admission partitions by managed tenant plus protected key digest and serializes reservation, fencing, semantic comparison, recovery, replay, and expiry across replicas. Trusted versioned semantic descriptors follow authoritative authorization and canonical validation; callers cannot select digest or retention tier. Replay-result retention begins at durable terminal finalization: 24 hours for ordinary mutations or seven calendar years for commits. Persist a monotonic time floor across restart. Inclusive expiry compacts results into consumed-key tombstones retained for tenant lifetime plus 400 days after approved deletion, with legal-hold pause/resume. Unresolved states never become fresh work.
- Compose provider-private mutation/commit/status adapters. Memories egress retains ordered durable intent and stable CloudEvent/idempotency identity. Alias-safe HMAC rotation preserves immutable canonical tokens. Confidential targets require proven `opaque-handle-restartable` capability; `cleartext-required` operations fail before admission with HTTP 422 and `confidential_operation_not_durable`.

## UX & Interaction Patterns

The read-only console distinguishes staging, confirmed commits, unknown provider outcomes, freshness, and recovery without bodies or diffs. Bounded confirmation precedes human escalation without retry/repair/discard actions. Confidential values expose a copyable correlation reference marked withheld, with distinct text, accessible labels, and non-color cues. Withheld, redacted, missing, and unknown remain distinct from read-model availability.

## Cross-Story Dependencies

Delivery follows the current manifest's prerequisite graph and execution ranks. OQ1 timing, OQ2 file policy, OQ4 provider compatibility, and the approved A6b v2 authorization matrix remain governing design prerequisites; historical OQ3 v1 does not govern. The manifest records `general_execution_hold: false`, `v2_exposure_authorized: false`, and `sprint_status_regeneration_authorized: false`. The separate execution decision authorizes Story 12.1 only.

The historical EventStore capability acceptance binds `ead4949d30b37d3a976f5c9197789a91701f1a72`. The current consumer supplement, `_bmad-output/implementation-artifacts/story-12-1-eventstore-consumer-compatibility-2026-10-15.yaml`, verifies source pin `6394d0500e6e277b8975cb397c9f0389f9a16929` and passing Folders consumer checks. It records healthy no-build Aspire startup separately from an upstream named asynchronous-dispatch test failure. These records establish source/SDK compatibility and startup health, not fresh deployed durability, restart recovery, live ETag races, broker recovery, operator repair, or rollout acceptance. Historical approvals remain unchanged.

Stories 12.2/12.3 consume 12.1 streams. Story 12.6 requires accepted OQ8 design and produces later runtime evidence. Story 12.7 follows 12.1 and precedes 12.4/12.5. Story 12.4 consumes 3.11/3.13 provider adapters; 12.5 consumes 10.6 safe documents and Epic 9 routing. Epics 4, 6, and 10 retain transition, diagnostic, and search-projection ownership. Story 4.23 consumes 12.1/12.2/12.6/12.7 for canonical lock identity. Production recovery remains an external prerequisite for dependent hardening work.
