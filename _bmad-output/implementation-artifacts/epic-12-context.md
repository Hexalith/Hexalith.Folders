# Epic 12 Context: Durable Repository-Backed Round Trip

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Authorized developers and AI agents can persist folder lifecycle and file content across restart, retrieve authoritative content, complete a provider-confirmed Git commit, observe durable task and projection state, and recover asynchronous indexing delivery. This durable data plane is required for MVP acceptance; NoOp, unavailable, in-memory, seed-only, or fake-backed substitutes cannot prove it.

## Stories

- Story 12.1: EventStore-backed folder repository, retire NoOp, and implement projection replay
- Story 12.2: Durable projections and task-completion pipeline
- Story 12.3: Durable workspace file-content store and content-read source
- Story 12.4: Real Git commit executor and provider write path
- Story 12.5: At-least-once Memories egress and reconciler
- Story 12.6: Implement durable all-mutations idempotency and expired-key precedence
- Story 12.7: Protect Confidential Operational Values

## Requirements & Constraints

- Accepted folder and organization changes, task completion, locks, projections, and delivery intent must survive restart and converge across supported replicas. Production must boot with a real repository; empty-checkpoint replay must rebuild current-semantic read models.
- Authorize against the managed tenant before folder, content, provider, commit, idempotency, or index disclosure. Wrong-tenant requests, stale authority, corrupt or unavailable stores, timeout, conflicting writes, duplicate delivery, and unsupported event versions need safe canonical outcomes and metadata-only evidence.
- Store staged and committed file bodies outside EventStore in one authoritative content store. Verify server-side hashes and byte/media metadata, enforce task, lock, version, and path policy, and make removed content unavailable. Context reads use this source. Dirty or unresolved work stays private to the originating lock-owning task.
- A locked workspace may produce one provider-confirmed commit on its bound GitHub or Forgejo remote and ref. Unknown post-dispatch outcomes require bounded read-only confirmation and then reconciliation if unresolved, never a blind repeat. Known retryable failures retain dirty locked work; terminal failure follows the lifecycle contract.
- Indexing failure cannot roll back committed Folders truth. Ordered, at-least-once publication and reconciliation must survive outage, duplicate delivery, and restart while preserving one logical index unit per stable identity. Memories is derived; Folders remains the hydration authority.
- Every Contract Spine mutation uses durable tenant-scoped idempotency. Live equivalent intent returns one logical result, live conflicting intent conflicts, and every expired key returns idempotency_key_expired before protected work; reads reject keys. Recheck current authorization on replay. Mutation results last 24 hours, commit results seven years, and minimal consumed-key tombstones tenant lifetime plus 400 days without protected prior intent.
- Replace classified confidential operational values with immutable tenant-scoped correlation tokens before admission or persistence. No durable cleartext, reversible ciphertext, or reconstructable material may enter events, state, working copies, audit, telemetry, outbox, indexes, exports, backups, or retry evidence. An operation requiring recoverable cleartext fails before admission with confidential_operation_not_durable.
- Completion requires deployed restart, replay, conflict, denial, retention, provider, and recovery evidence. One multi-replica mutation and query slice through a real provider is a foundation gate; broader MVP parity remains separately required.

## Technical Decisions

- Hexalith.EventStore is the sole domain write path: REST gateway, processor, authorization gate, then repository. Aggregates append metadata-only events; Workers process managers perform provider, Git, and working-copy effects. One append conflict permits at most one full authorization-and-domain re-evaluation, never blind append or unbounded retry.
- Event identity is a stable logical URI with a positive payload schema version distinct from EventStore envelope metadata. Validate and deterministically upcast the full historical chain before dispatch. Only named historical aliases with retained byte fixtures may interpret missing payload version as v1; unsupported history fails closed without rewriting old bytes.
- Locks, fencing, idempotency admission and tombstones, checkpoints, and reconciliation tasks require durable authority and fail closed when unreadable. Projection replay cannot recreate or erase consumed-key authority. Working copies are disposable caches rebuilt from durable content and provider state.
- File transport uses inline JSON up to 256 KB or streamed multipart for larger bodies; base64 payloads are rejected. The Git executor composes registered provider-private mutation, commit, and status adapters. Memories egress uses durable commit-then-append ordering with stable CloudEvent identity and metadata-only documents until body indexing is separately approved.
- A versioned tenant-scoped HMAC tokenizer issues one immutable canonical token. Rotation uses lookup aliases that preserve it; incomplete alias coverage or collisions fail closed. Durable provider execution may retain tokens, credential references, and non-reversible opaque handles only. Classify each provider operation as opaque-handle-restartable or cleartext-required before admission.
- Serialize workspace writers on managed tenant plus canonical provider/repository identity plus normalized target ref, not on folder or task ID.

## UX & Interaction Patterns

The operations console stays read-only and metadata-only. Show actual lifecycle, lock, commit reference, freshness, retry eligibility, and recovery state with text and accessible cues. During bounded provider confirmation, show unknown_provider_outcome as automatically recovering; escalate to reconciliation_required only when checks cannot establish the result. Render a confidential value only as a safe-copy correlation reference with withheld status: no durable cleartext exists to reveal. Keep withheld, redacted, missing, unknown, and unavailable distinct.

## Cross-Story Dependencies

Story 12.1 requires the external EventStore event-evolution capability, approved v2 authorization matrix, and execution release. Stories 12.2 and 12.3 consume its durable streams; 12.6 also needs the accepted idempotency design. Story 12.7 follows 12.1 and precedes provider commits and Memories egress in 12.4 and 12.5. Those stories also need production provider adapters or the configured Memories route. Epics 4, 6, and 10 own consuming transition, diagnostics, and search projections; Epic 12 supplies their durable source.
