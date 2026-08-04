# ADR 0004: EventStore-owned canonical idempotency admission

Date: 2026-07-19

Original date: 2026-05-31; amended for OQ8

Decision identifiers: `A-9`, `D-7`. Implementing epics: Epic 1 and Epic 4 (idempotency). This is a retrospective ADR; it records a decision already implemented across Epics 1-7, it does not propose new design.

## Status

Accepted and amended by OQ8 design version 1.0.0. Per-command canonical hashing remains the intent-equivalence authority, while durable admission, replay, conflict, recovery, expiry, and consumed-key evidence are owned by Hexalith.EventStore for every mutation.

## Context

Every mutating Contract Spine operation can be retried by clients, replayed by transports, or redelivered by Dapr pub/sub. Without a deterministic equivalence rule and tenant-wide admission identity, a retry could duplicate an aggregate event, repository, file operation, commit, task, projection, or audit record, and the four surfaces could disagree about what "the same request" means.

Architecture decision `A-9` defines a per-command payload-equivalence rule: a canonical hash over the fields listed in `x-hexalith-idempotency-equivalence` in lexicographic order. Decision `D-7` fixes two replay-result retention tiers in `x-hexalith-idempotency-ttl-tier`, separates them from consumed-key retention, and disallows a free-form per-command knob. OQ8 closes the former aggregate-scoped gap by introducing an EventStore-owned admission actor partitioned by managed tenant plus protected key digest.

## Decision

Every mutating command carries an idempotency key and a server-trusted canonical intent descriptor derived from the Contract Spine.

- `A-9`: the canonical descriptor applies length-prefixed, type-tagged field encoding, ordinal field ordering, duplicate-JSON-property rejection, schema-declared normalization, and a keyed SHA-256 intent digest over operation, canonical target identity, normalized semantic payload/options, policy version, delegated task scope, and behavior-affecting credential scope. Correlation, authentication-token, clock, trace, delivery-attempt, and transport-retry metadata are excluded.
- Public request extensions are untrusted. A registered Folders adapter constructs the descriptor and fixed retention tier after authentication, authorization, and canonical validation; EventStore validates the adapter, operation, descriptor version, and tier before admission.
- The EventStore admission actor is partitioned by managed tenant plus an HMAC-SHA-256 digest of the opaque key. It atomically owns `reserved`, `pending`, `recoverable`, `unknown_provider_outcome`, `terminal`, and `expired` transitions and issues a fencing token before side effects.
- A live equivalent request replays the same logical result after current authorization; a live different request returns `idempotency_conflict`. At `now >= expiresAt`, either intent returns `idempotency_key_expired` and never executes.
- `D-7`: terminal replay results use `PT24H` for non-commit mutations and `P7Y` for commit. Minimal metadata-only consumed-key tombstones live for the active managed-tenant lifetime plus 400 days after approved tenant deletion, with legal hold pausing the deletion countdown.
- Structural validation and authorization failures occur before admission and do not consume a key. Deterministic post-admission failures do consume it; recoverable and unknown external outcomes preserve their durable state and cannot authorize blind retry.
- Non-mutating operations reject `Idempotency-Key` before query or source execution while preserving correlation, authorization parity, safe-denial shape, and read-consistency classification.

Raw keys are never persisted or logged. Versioned tenant digest keys and a domain-separated verification tag support rotation and fail-closed collision detection. Replay compaction atomically removes the result and live intent digest but preserves the tombstone. The complete normative state, boundary, error mapping, and retention rules are frozen in `docs/exit-criteria/oq8-idempotency-design.md`.

## Consequences

- Replays are safe and deterministic across REST, SDK, CLI, and MCP because canonicalization is generated from one contract and admission is serialized on tenant/key identity rather than aggregate identity.
- A conflicting reuse of a key is a first-class, metadata-only rejection (`idempotency_conflict`) rather than an ambiguous duplicate.
- An expired key is recognizable without retaining protected prior intent and maps identically to HTTP 409, CLI exit 76, and MCP `idempotency_key_expired` with `retryable = false` and `clientAction = refresh_state_then_submit_with_new_key`.
- The cost is that adding or changing a command requires updating the equivalence fields, fixed tier, canonicalization, adapter descriptor, result mapping, generated matrix, and tests together. Digest-reader keys must remain available until governed tombstones are promoted or deleted.

## Alternatives Considered

- A client-supplied opaque key with no payload-equivalence check was rejected because it cannot distinguish a safe replay from a conflicting reuse, so it cannot return `idempotency_conflict`.
- Per-command free-form TTLs were rejected by `D-7` because they make audit-retention reasoning unpredictable; two fixed tiers keep commit evidence aligned with the `C3` retention window.
- Aggregate-scoped records were rejected because the same tenant key could be reused against another target without collision.
- Deleting a replay record at TTL was rejected because it resurrects a consumed key. Keeping full results for the tombstone lifetime was rejected because it unnecessarily retains protected intent and response data.
- A Folders-owned Dapr state wrapper, database, file ledger, or cache authority was rejected because durable domain admission belongs in Hexalith.EventStore.

## Verification

This decision is conformance-checked by `pwsh ./tests/tools/run-adr-runbook-docs-gates.ps1` plus `GovernanceCompletenessGateTests.Oq8DesignPackageBindsApprovedDecisionsAndExactDigest`. Runtime verification covers EventStore unit/actor/gateway/live-sidecar lanes, Folders cross-surface parity, generated mutation/read completeness, boundary time, restart, multi-host concurrency, persisted state-store end state, and leakage scanning. CI checkout keeps `submodules: false`; local setup initializes only root-level submodules with `git submodule update --init references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.PolymorphicSerializations references/Hexalith.Tenants`.
