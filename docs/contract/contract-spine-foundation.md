# Contract Spine Foundation Notes

The canonical Contract Spine foundation is `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`. The canonical machine-readable extension vocabulary is `src/Hexalith.Folders.Contracts/openapi/extensions/hexalith-extension-vocabulary.yaml`.

This story keeps operation groups deferred. Stories 1.7 through 1.11 must add concrete paths, request schemas, response schemas, operation-specific error mappings, and operation-specific audit metadata. Story 1.12 owns NSwag SDK generation. Story 1.13 owns parity oracle rows.

## Downstream Authoring Rules

- Use unique stable `operationId` values because SDK generation and parity evidence depend on them.
- Do not add request payload, query parameter, or client-controlled header fields that define tenant authority. Tenant authority comes from authenticated principal claims and EventStore envelopes.
- Prefer shared `$ref` components from the foundation for Problem Details, correlation, task identity, freshness, status, pagination, lifecycle state, and canonical error vocabulary.
- Mutating operations must declare `Idempotency-Key`, `x-hexalith-idempotency-key`, `x-hexalith-idempotency-equivalence`, and `x-hexalith-idempotency-ttl-tier`.
- Query operations must not accept idempotency keys and must declare read-consistency behavior.
- Examples must use synthetic opaque placeholders only. Do not include real paths, provider names, credential-shaped values, tokens, file content, diffs, deployment hosts, or unauthorized resource hints.

## Prerequisite Status

- `docs/exit-criteria/c3-retention.md`: present, but values are proposed workshop values and need Legal + PM approval before binding retention defaults are encoded.
- `docs/exit-criteria/c4-input-limits.md`: present, but values are proposed workshop values and need PM approval before binding schema limits are encoded.
- `docs/exit-criteria/s2-oidc-validation.md`: present, with approved validation rules and reference-only `.invalid` placeholders; real issuer and audience pins remain deployment configuration.
- `docs/exit-criteria/c6-transition-matrix-mapping.md`: present, with approved vocabulary and mapping plan; aggregate transition implementation remains deferred.
- `docs/contract/idempotency-and-parity-rules.md`: present and consumed for shared vocabulary shape only.

## Deferred Owners

- Story 1.7: tenant, folder, provider, and repository binding contract groups.
- Story 1.8: workspace and lock contract groups.
- Story 1.9: file mutation and context query contract groups.
- Story 1.10: commit and workspace status contract groups.
- Story 1.11: audit and operations-console query contract groups.
- Story 1.12: NSwag SDK generation and generated idempotency helpers.
- Story 1.13: parity oracle generation.
- Stories 1.14 and later: drift, safety invariant, exit-criteria, and parity completeness CI gates.
