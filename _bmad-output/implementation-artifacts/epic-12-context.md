# Epic 12 Context: Durable Repository-Backed Round Trip

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Authorized developers and AI agents can persist folder lifecycle/content across restart, retrieve authoritative content, complete provider-confirmed Git commits, observe terminal tasks, and recover indexing delivery across replicas. Completion requires real persisted-state and deployed-path evidence; substitutes are insufficient.

## Stories

- Story 12.1: EventStore-backed folder repository, retire NoOp, and implement projection replay
- Story 12.2: Durable projections and task-completion pipeline
- Story 12.3: Durable workspace file-content store and content-read source
- Story 12.4: Real Git commit executor and provider write path
- Story 12.5: At-least-once Memories egress and reconciler
- Story 12.6: Implement durable all-mutations idempotency and expired-key precedence
- Story 12.7: Protect Confidential Operational Values

## Requirements & Constraints

- Accepted mutations, lifecycle/task state, terminal results, checkpoints, retry eligibility, and failure/recovery evidence survive restart and converge across supported replicas. Empty-checkpoint replay reconstructs equivalent current-semantic views; clock-derived freshness and stale-lock observations are outside deterministic state equivalence. Status/audit freshness targets 500 ms; distinguish delayed, failed, stale, and unavailable evidence.
- Fresh authoritative tenant/folder authorization precedes protected lookup, disclosure, and effects. Unavailable/stale authority produces `authority_unavailable` HTTP 503; fresh negative authority uses safe HTTP 404. Bounded-stale browsing requires non-authoritative data and freshness metadata. Tasks belong to the authorized folder.
- Duplicate delivery, ordering conflicts, corrupt/unavailable stores, timeouts, empty streams/checkpoints, retention boundaries, and tenant denial need canonical outcomes and metadata-only evidence. Replay covers every retained payload generation, governed upcaster chain, and closed missing-version-registry entry without changing historical bytes.
- One authoritative content store supplies verified bodies and context reads. Enforce hashes, metadata, task/lock/version, file policy, and deletion; events/audit/telemetry/indexes exclude bodies.
- Staging does not auto-commit. Authorized lock-owning work produces one eligible provider mutation and confirmed commit. Unknown outcomes allow bounded read-only confirmation, then reconciliation; never blindly repeat effects. Indexing outages cannot roll back committed truth.
- Mutations use durable tenant-scoped idempotency; reads reject keys. Authorize before replay disclosure. Live equivalent intent replays; different intent conflicts; expired keys fail before protected work. Compaction cannot resurrect keys.
- Confidential operational values become irreversible tenant-scoped correlation tokens before admission or persistence. No durable cleartext, reversible form, or reconstruction material may enter state, working copies, evidence, backups, or egress.

## Technical Decisions

- Reuse EventStore domain-service SDK persistence, query, and projection seams. EventStore is the sole domain writer; state and event append are transactional. Allow at most one full authorization/domain re-evaluation after conflict; never replay external effects. Exhaustion preserves canonical concurrency errors.
- Aggregate concurrency remains observable as HTTP 409, CLI exit 77, and MCP kind/category/code `concurrency_conflict`. EventStore may re-evaluate the full command once after authoritative rehydration; it never blindly repeats external effects (architecture D-11).
- Mutation replay-result retention is exactly 24 hours (`PT24H`); commit replay-result retention is seven calendar years (`P7Y`). Durable terminal finalization starts the replay clock, and `now >= expiresAt` atomically compacts results/intent to consumed-key evidence. That evidence has its independent tenant-lifetime-plus-400-day C3 retention and legal-hold rules (PRD idempotency; architecture D-7).
- Immutable full CLR `EventTypeName` identifies stored events. EventStore 6.6 keeps `PayloadVersion` separate from envelope `MetadataVersion`: absent or 1 means version 1 under declared-version rules; versions 2–1024 carry explicit stamps. Use platform upcasters and historical-alias byte fixtures. Unknown events or unsupported versions fail before state/checkpoint advancement. Verify compatibility against the actual implementation revision.
- Lifecycle, lock, workspace, cleanup, task, and commit-status projections/checkpoints are durable; working copies are disposable caches. Cleanup requires terminal closure, no active task, a seven-day window, and no legal hold. Expiry/cancellation/inaccessibility alone cannot trigger deletion of unresolved work.
- EventStore admission serializes tenant/key fencing across replicas. Fixed replay retention, monotonic time, inclusive expiry, tombstones, and legal holds preserve non-reuse.
- Confidential provider operations classified `cleartext-required` fail before EventStore admission or provider effects with HTTP 422 `confidential_operation_not_durable`, non-retryable `validation_error` and `correct_input`; restartable execution requires a proven opaque handle (architecture PD8). Public v2 remains disabled pending its existing approvals.
- Readiness must report actual dependency health; configuration and seed data alone cannot establish readiness (PRD NFR81; architecture release-hardening evidence).
- Compose provider-private adapters. Egress preserves ordered durable intent and stable CloudEvent/idempotency identity. Alias-safe HMAC rotation preserves canonical tokens. Confidential targets require restartable opaque handles; cleartext-dependent operations fail before admission.

## UX & Interaction Patterns

The read-only metadata console distinguishes lifecycle, lock, task result, disposition, freshness, and disclosure. Unknown provider outcomes show bounded confirmation progress without retry/repair/discard controls. Withheld values expose accessible copyable correlation references. Withheld, redacted, missing, and unknown remain separate from availability; bodies/diffs stay excluded.

## Cross-Story Dependencies

Story 12.2 consumes Story 12.1 durable ordered streams and accepted `EXT-ES-EVENT-EVOLUTION`; rank 21 includes `DEC-A8-HOLD` ancestry. Reconcile held/open planning markers with current owner instructions and later revision-bound 12.1 completion evidence; historical supplements do not automatically describe the current checkout.

Story 12.6 consumes accepted OQ8 design and produces later runtime evidence. Story 12.7 follows 12.1 and approved confidentiality decisions, preceding 12.4/12.5. Story 12.4 consumes 12.1–12.3 and adapters 3.11/3.13; 12.5 consumes 10.6 safe documents and Epic 9 routing. Epics 4, 6, and 10 retain transition-evidence, seven diagnostic, and search-bridge projection ownership. Production recovery remains external for dependent hardening. Story 4.23 consumes 1.17, 4.22, 12.1, 12.2, 12.6, 12.7, 11.15 and accepted OQ1/C7 before Story 4.19; it owns canonical cross-folder lock identity, fencing, renewal and C7 authorization timing (epics Story 4.23).

OQ1 timing, OQ2 file policy, and OQ4 compatibility retain authority; historical OQ3 v1 is superseded by the v2 target. The regenerated v2 set remains candidate-awaiting-A6b, disabled and unrouted; prior approvals do not establish current conformance or close 1.17. Hold removal grants neither exposure nor sprint-status regeneration.
