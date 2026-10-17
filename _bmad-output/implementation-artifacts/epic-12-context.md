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
- One authoritative content store supplies staged/committed bodies and context reads. Verify hashes and byte/media metadata; enforce task, lock, version, and file policy. Removed content is unavailable; indexes never become content authority. Events, audit, and telemetry exclude file bodies.
- Staging does not auto-commit. One authorized lock-owning task produces exactly one eligible provider mutation and a confirmed commit on the bound GitHub or Forgejo remote/ref. Unknown outcomes permit bounded read-only confirmation, then reconciliation; never blindly repeat effects. Indexing outages cannot roll back committed truth.
- Every mutation uses durable tenant-scoped idempotency; reads reject keys. Equivalent live intent replays after current authorization, different live intent conflicts, and expired keys always fail before protected work without revealing prior intent or becoming new intent.
- Confidential values become irreversible tenant-scoped correlation tokens before admission or persistence. No durable cleartext, reversible form, or reconstruction material may enter events, state, working copies, audit, telemetry, outbox, broker messages, bridge state, indexes, exports, backups, or retries.
- Completion requires persisted-state, restart, replay, denial, multi-replica, retention, and real-provider evidence. The narrower durable vertical-slice gate does not replace both-provider coverage across REST, SDK, CLI, and MCP. Source checks and startup health do not establish deployed acceptance.

## Technical Decisions

- EventStore is the sole domain writer. Command-state and event append are transactional; a conflict permits at most one bounded full authorization-and-domain re-evaluation. Blind append, unbounded retries, and provider-effect replay are forbidden. Exhausted conflict preserves HTTP 409, CLI exit 77, and MCP `concurrency_conflict`.
- Use the accepted EventStore 6.6 evolution contract: immutable full CLR `EventTypeName`, separate envelope `MetadataVersion` and event `PayloadVersion`; absent or 1 means payload version 1 under declared-version rules, while versions 2–1024 carry explicit stamps. Platform upcasters handle supported changes without rewriting retained bytes. Unknown events or unsupported versions fail before state/checkpoint advancement; retained aliases need exact byte fixtures.
- Lifecycle/task projections and checkpoints are durable. Working copies are disposable caches rebuilt from authoritative content and provider state. Replay cannot create or erase consumed-key authority. Reuse platform SDK persistence/projection seams rather than implementing local platform infrastructure.
- EventStore admission partitions by managed tenant and protected key digest, serializing reservation, fencing, comparison, recovery, replay, and expiry across replicas. Trusted versioned semantic descriptors follow authoritative authorization and canonical validation; callers cannot select the digest or retention tier. Replay-result retention starts at durable terminal finalization: 24 hours for ordinary mutations or seven calendar years for commits. Persist a monotonic time floor across restart. Inclusive expiry compacts results into minimal consumed-key tombstones retained for tenant lifetime plus 400 days after approved deletion, with legal-hold pause/resume. Unresolved states never become fresh work.
- Compose provider-private mutation/commit/status adapters. Memories egress preserves ordered durable intent and stable CloudEvent/idempotency identity. Alias-safe HMAC rotation preserves immutable canonical tokens. Confidential targets require proven `opaque-handle-restartable` capability; `cleartext-required` operations fail before admission with HTTP 422 and `confidential_operation_not_durable`.

## UX & Interaction Patterns

The read-only console distinguishes staging, confirmed commits, unknown provider outcomes, freshness, and recovery without bodies or diffs. Bounded confirmation precedes human escalation without retry/repair/discard controls. Confidential values expose a copyable correlation reference marked withheld, with distinct text, accessible labels, and non-color cues. Withheld, redacted, missing, and unknown remain distinct from read-model availability.

## Cross-Story Dependencies

Follow the manifest prerequisite graph and ranks. OQ1 timing, OQ2 file policy, OQ4 provider compatibility, and the approved A6b v2 authorization matrix remain prerequisites; historical OQ3 v1 does not govern. The manifest records `general_execution_hold: false`, `v2_exposure_authorized: false`, and `sprint_status_regeneration_authorized: false`; `DEC-EXEC-12.1` separately authorizes Story 12.1 only. Historical A6b/A8 approvals bind their exact evidence bytes. The regenerated v2 conformance set remains `candidate-awaiting-a6b`, production-disabled and unrouted; historical approval does not approve changed candidate bytes or close Story 1.17.

The historical capability acceptance binds EventStore `ead4949d30b37d3a976f5c9197789a91701f1a72`. The 2026-10-15 consumer supplement, `story-12-1-eventstore-consumer-compatibility-2026-10-15.yaml` beside this file, binds `6394d0500e6e277b8975cb397c9f0389f9a16929`: Folders consumer checks and no-build Aspire startup passed, while a named asynchronous-dispatch test failed upstream. The observed root gitlink and EventStore HEAD are now `77d9af18a54d92506e310fe5a45021a395e66f60`; compatibility must be re-established at this revision. Neither prior record establishes fresh deployed durability, restart recovery, live ETag races, broker recovery, operator repair, or rollout acceptance. Preserve historical approvals and observations.

Stories 12.2/12.3 consume 12.1 streams. Story 12.6 requires accepted OQ8 design and produces later OQ8 runtime evidence. Story 12.7 follows 12.1 and approved A5/C9/PD8, preceding 12.4/12.5. Story 12.4 consumes 3.11/3.13 provider adapters; 12.5 consumes 10.6 safe documents and Epic 9 routing. Epics 4, 6, and 10 retain transition, diagnostic, and search-projection ownership. Story 4.23 consumes 12.1/12.2/12.6/12.7 for canonical lock identity. Production recovery remains an external prerequisite for dependent hardening work.
