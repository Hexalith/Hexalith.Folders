# ADR 0004: Per-command canonical idempotency hashing

Date: 2026-05-31

Decision identifiers: `A-9`, `D-7`. Implementing epics: Epic 1 and Epic 4 (idempotency). This is a retrospective ADR; it records a decision already implemented across Epics 1-7, it does not propose new design.

## Status

Accepted. Per-command canonical hashing and the fingerprint ledger have been the idempotency model since the Epic 1 contract work, with the workspace lifecycle commands wired in Epic 4.

## Context

Mutating operations (workspace prepare, lock, file mutation, commit, cleanup) can be retried by clients, replayed by transports, or redelivered by Dapr pub/sub. Without a deterministic equivalence rule, a retry could duplicate a repository, a commit, or an audit record, and the four surfaces could disagree about what "the same request" means.

Architecture decision `A-9` defines a per-command payload-equivalence rule: a canonical hash over the fields listed in `x-hexalith-idempotency-equivalence` in lexicographic order. Decision `D-7` fixes two idempotency-record TTL tiers in `x-hexalith-idempotency-ttl-tier` and disallows a free-form per-command knob.

## Decision

Every mutating command carries an idempotency key and a canonical fingerprint computed the same way on every surface.

- `A-9`: the canonical hash applies type-tagged, delimiter-escaped field encoding (control-character, surrogate, and separator escaping), ordinal field ordering, duplicate-JSON-property rejection, and SHA-256, over exactly the fields named in the command's `x-hexalith-idempotency-equivalence` list. Per-field Unicode normalization (for example NFC-normalized path values) is applied where the spine declares a field normalization-insensitive, not as a blanket pass over the hash input. The NSwag-generated `ComputeIdempotencyHash()` helper means the SDK and server compute identical fingerprints.
- Same key plus equivalent payload returns the same logical result; same key plus different payload returns `idempotency_conflict`.
- `D-7`: idempotency records use two fixed TTL tiers - the mutation tier (`24h`) for prepare, lock, file mutation, and cleanup, and the commit tier (retention-period per `C3`) so commit reconciliation evidence survives for the audit window.
- Non-mutating operations must not accept an `Idempotency-Key`; they still require correlation, authorization parity, safe-denial shape, and read-consistency classification.

Keys and operation IDs are tenant-scoped; the fingerprint ledger pairs the stream append with the local idempotency record so replay and append-conflict reread are atomic.

## Consequences

- Replays are safe and deterministic across REST, SDK, CLI, and MCP because canonicalization is generated from one contract, not hand-written per client.
- A conflicting reuse of a key is a first-class, metadata-only rejection (`idempotency_conflict`) rather than an ambiguous duplicate.
- The cost is that adding or changing a command requires updating the equivalence field list, the canonicalization, the aggregate dispatch, the result-code mapping, and the tests together; the TTL tiers are fixed and not per-command tunable.

## Alternatives Considered

- A client-supplied opaque key with no payload-equivalence check was rejected because it cannot distinguish a safe replay from a conflicting reuse, so it cannot return `idempotency_conflict`.
- Per-command free-form TTLs were rejected by `D-7` because they make audit-retention reasoning unpredictable; two fixed tiers keep commit evidence aligned with the `C3` retention window.

## Verification

This decision is conformance-checked by `pwsh ./tests/tools/run-adr-runbook-docs-gates.ps1`, which emits metadata-only evidence to `_bmad-output/gates/adr-runbook-docs/latest.json`. The idempotency behavior is enforced by the idempotency unit suites and the parity oracle. CI checkout keeps `submodules: false`; local setup initializes only root-level submodules with `git submodule update --init references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.PolymorphicSerializations references/Hexalith.Tenants`.
