# Epic 12 Context: Durable Repository-Backed Round Trip

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Authorized developers and AI agents can retain folder lifecycle and file content across restart, retrieve authoritative content, complete provider-confirmed Git commits, observe terminal tasks and projections, and recover asynchronous indexing delivery. Completion requires evidence from real durable paths; NoOp, unavailable, seed-only, in-memory, safe-empty, and fake-backed substitutes cannot establish positive capability.

## Stories

- Story 12.1: EventStore-backed folder repository, retire NoOp, and implement projection replay
- Story 12.2: Durable projections and task-completion pipeline
- Story 12.3: Durable workspace file-content store and content-read source
- Story 12.4: Real Git commit executor and provider write path
- Story 12.5: At-least-once Memories egress and reconciler
- Story 12.6: Implement durable all-mutations idempotency and expired-key precedence
- Story 12.7: Protect Confidential Operational Values

## Requirements & Constraints

Accepted mutations, terminal task results, checkpoints, retry eligibility, and failure/recovery evidence must survive process and sidecar restart and converge across supported replicas. Empty-checkpoint replay reconstructs equivalent current-semantic state; freshness and stale-lock observations derived from external clocks are outside deterministic equivalence. Status and audit summaries target 500 ms p95, while command acknowledgements target 1 second p95.

Fresh authoritative tenant, membership, delegation, and folder permissions precede protected lookup, disclosure, and effects. Stale, conflicting, incomplete, or unavailable authority yields HTTP 503 `authority_unavailable`; fresh negative facts produce safe HTTP 404. Bounded-stale browsing applies only to non-authoritative read models with explicit freshness metadata. Tenant identity comes from authenticated context and validated envelopes, never payload assertions. Tasks remain scoped to authorized folders.

Durability proof includes duplicate delivery, event ordering conflicts, concurrent writers, corrupt or unavailable stores, timeout/cancellation, empty streams/checkpoints, retention boundaries, and authorization denial. Events, audit, logs, traces, projections, and errors remain metadata-only, excluding bodies, diffs, credentials, secrets, and unauthorized resource existence. Replay covers every retained payload generation, governed upcaster chain, and closed missing-version registry entry without rewriting historical bytes.

One authoritative content source verifies bodies, hashes, metadata, task/lock/version scope, file policy, and deletion. Staging does not commit automatically. Eligible lock-owning tasks produce at most one successful provider-confirmed durable commit. Unknown provider outcomes permit bounded read-only confirmation, then require reconciliation; blind effect replay, mutation, and takeover remain forbidden. Indexing failure cannot roll back committed truth.

Confidential operational values become irreversible tenant-scoped correlation tokens before admission or persistence. Durable cleartext, reversible encryption, reconstruction material, and recoverable working copies are forbidden. Operations needing later confidential cleartext fail before admission unless a proven restartable opaque provider handle exists.

## Technical Decisions

Reuse EventStore domain-service persistence, query, and projection seams. EventStore is the sole domain writer; state and event append are transactional. A conflict permits at most one full authorization-and-domain re-evaluation, preserving canonical HTTP 409, CLI exit 77, and MCP `concurrency_conflict` on exhaustion. External provider effects are never blindly repeated.

Retained full CLR event type names are immutable storage identifiers. Payload versions are separate from envelope metadata versions; missing payload stamps mean version 1 only under the accepted platform's declared-version rules. Governed upcasters produce current semantics; unknown events and unsupported versions fail before state or checkpoint advancement.

Lifecycle, lock, workspace, cleanup, task, and commit-status views have durable recovery contracts; working copies are disposable caches. Cleanup requires terminal closure, no active task, a seven-day window, and no legal hold. Expiry, cancellation, or inaccessibility alone cannot authorize deletion of unresolved work. Readiness must reflect actual dependency health rather than configuration or seeds.

Every mutation uses durable tenant-scoped idempotency, and reads reject idempotency keys. Current authorization precedes replay disclosure. Live equivalent intent returns the same logical result; live different intent conflicts. At inclusive expiry, either intent returns `idempotency_key_expired` before protected work. Terminal finalization starts replay retention: 24 hours for ordinary mutations and seven calendar years for commits. Atomic compaction preserves minimal consumed-key evidence for its separate tenant-lifetime-plus-400-day retention and legal-hold rules; clock rollback, restart, and races cannot resurrect a key. Admission authority is not rebuilt through projection replay.

A single event-write tokenizer supplies immutable confidential correlation identities across locks, events, evidence, exports, and indexing. Alias-safe rotation preserves identity without rewriting history; retirement waits for live coverage and all old leases/retries to close. `cleartext-required` provider operations return HTTP 422 `confidential_operation_not_durable` before admission. Durable egress retains ordered intent and stable CloudEvent/idempotency identity, with recoverable retry and reconciliation status.

## UX & Interaction Patterns

The read-only metadata console distinguishes lifecycle, lock, task outcome, freshness, and disclosure. Unknown provider outcomes show bounded confirmation progress; exhausted uncertainty requires human escalation without repair controls. Confidential `withheld` values expose correlation references and never promise recoverability. Withheld, redacted, unknown, missing, stale, and unavailable remain distinct states. Bodies, diffs, and secrets remain excluded.

## Cross-Story Dependencies

Story 12.2 consumes Story 12.1's durable ordered events and accepted event-evolution compatibility. Story 12.3 additionally requires current authorization and approved file policy. Story 12.4 consumes 12.1–12.3, 12.7, provider-private adapters 3.11/3.13, and current binding/ref policy. Story 12.5 consumes 12.1–12.3, 12.7, C9-safe materialization from 10.6, and Epic 9 routing. Story 12.7 follows 12.1 and the approved confidentiality decisions; it precedes 12.4/12.5. Story 12.6 requires accepted idempotency design and produces its runtime evidence afterward.

Epics 4, 6, and 10 retain transition-evidence, diagnostics, and search-bridge ownership. Production recovery remains an external prerequisite for dependent hardening. Cross-folder lock work in 4.23 consumes this durable foundation and preserves canonical repository/ref identity and authorization timing.

Approved timing, file-policy, and provider-compatibility decisions remain authoritative. Historical v1 compatibility is superseded by the Story 1.17/A6b v2 target. Planning hold and execution-manifest ancestry must be reconciled with revision-bound owner decisions; historical evidence alone does not prove current conformance, enable public v2, authorize exposure, or regenerate sprint status.
