# Tenant, Folder, Provider, And Repository Contract Groups

Story 1.7 adds only Contract Spine declarations for tenant-context-safe folder, ACL, provider binding, provider readiness, repository binding, repository-backed folder creation, and branch/ref policy operations.

The canonical machine-readable source remains `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`. These notes are downstream authoring guidance, not runtime behavior.

## Reuse Requirements

- Stories 1.8 through 1.11 must reuse `ProblemDetails`, `SafeAuthorizationDenial`, `FreshnessMetadata`, `PaginationMetadata`, `OpaqueIdentifier`, correlation headers, and the `x-hexalith-*` operation metadata vocabulary from the Contract Spine.
- Mutating operations must keep `Idempotency-Key`, `x-hexalith-idempotency-key`, lexicographically ordered `x-hexalith-idempotency-equivalence`, and `x-hexalith-idempotency-ttl-tier`.
- Query and status operations must keep `x-hexalith-read-consistency` and must not accept `Idempotency-Key`.
- Tenant authority remains authentication context plus EventStore envelopes. Route identifiers, provider binding references, repository binding identifiers, and branch/ref policy references are addressable resource references only.
- Consumer-facing denials must use the externally indistinguishable safe-denial envelope where protected resource existence cannot be revealed.
- Provider readiness diagnostics must stay audience-partitioned: consumer examples remain redacted, while authorized-operator examples remain sanitized and metadata-only.

## Deferred Owners

- Story 1.8 owns workspace and lock operation groups.
- Story 1.9 owns file mutation and context query operation groups.
- Story 1.10 owns commit and workspace status operation groups.
- Story 1.11 owns audit timeline and operations-console projection groups.
- Story 1.12 owns NSwag SDK generation and generated idempotency helpers.
- Story 1.13 owns parity oracle rows and generated parity evidence.
- Stories 1.14 through 1.16 own drift, safety invariant, exit-criteria, and parity completeness CI gates.

## Deferred Decisions

- C3 retention values remain proposed workshop values until Legal and PM approval; mutation-tier idempotency is declared here, while commit-tier retention remains downstream.
- C4 input limits remain proposed workshop values until PM approval; Story 1.7 does not bind new numeric provider, repository, or branch/ref limits beyond narrow placeholder bounds.
- S-2 issuer and audience values remain deployment configuration. The contract records tenant authority provenance without embedding real issuers or audiences.
- C6 transition implementation remains deferred to aggregate stories. Story 1.7 may reference lifecycle vocabulary but does not implement transitions.
- Story 1.5 parity rows and Story 1.6 extension vocabulary are consumed as contract metadata; final generated parity rows remain deferred.
