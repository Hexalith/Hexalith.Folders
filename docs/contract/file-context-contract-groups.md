# File Mutation And Context Query Contract Groups

Status: Story 1.9 contract-only authoring note.

This note records how the Story 1.9 Contract Spine additions must be reused by downstream stories. The OpenAPI document remains the source of truth; this file is only a small map for future implementation owners.

## Reuse Points

- Story 1.10 must reuse the file mutation `operationId`, task, workspace, path metadata, retry eligibility, unknown outcome, and reconciliation vocabulary when commit and workspace status contracts are authored.
- Story 1.11 must reuse the metadata-only audit keys and safe-denial behavior for audit timeline contracts.
- Epic 4 owns runtime file mutation behavior, prepared-workspace checks, held-lock enforcement, path policy execution, context-query execution, provider/Git/filesystem side effects, and reconciliation.
- Epic 5 owns SDK, CLI, and MCP behavioral parity over these operation groups.
- Story 6.6 must reuse the same metadata-only context-query and workspace diagnostic labels in the read-only operations console.

## Deferred Owners

- Runtime file mutation behavior: Epic 4.
- Path policy enforcement and authorization-before-observation execution: Epic 4.
- Context-query tree, metadata, search, glob, and range-read execution: Epic 4.
- Semantic indexing and RAG retrieval integration: downstream Memories integration work; this story reserves only extension-safe vocabulary and does not implement semantic indexing.
- Commit/status contracts: Story 1.10.
- Audit timeline contracts: Story 1.11.
- Operations-console projections: Story 6.6 and adjacent Epic 6 stories.
- Generated SDK helpers and NSwag wiring: Story 1.12.
- Final parity oracle rows and CI gates: Story 1.13 and later release-readiness stories.

## Reference-Pending Decisions

- C4 input limits are copied from `docs/exit-criteria/c4-input-limits.md` as proposed workshop values with PM approval still pending.
- Story 1.6 foundation vocabulary and Story 1.8 workspace/lock references are reused rather than duplicated.
- C6 state metadata is referenced from `docs/exit-criteria/c6-transition-matrix-mapping.md`; aggregate implementation is deferred.

## Idempotency-equivalence and tenant authority partition

`x-hexalith-idempotency-equivalence` lists for `AddFile`, `ChangeFile`, and `RemoveFile` deliberately omit `tenant_id`. Tenant authority is envelope-derived per `docs/contract/idempotency-and-parity-rules.md:11`: it MUST NOT appear in equivalence lists because the idempotency cache key is server-scoped to `(envelope_tenant_id, idempotency_key)`. The Story 1.9 spec subtask wording referencing `tenant_id` in the equivalence list reflects pre-canonical-rule drafting; the contract is correct as authored and the canonical doc is authoritative.

## Authorization-order vocabulary

Context-query operations declare authorization order both as human-readable prose (`x-hexalith-authorization.requirement`) and as a structured array (`x-hexalith-authorization.order`). Parity oracles, adapter generators, and audit-leakage tooling SHOULD consume the array form. The canonical token sequence is:

```
tenant_access -> folder_acl -> path_policy -> sensitivity_classification -> c4_bounds -> query_execution
```

The order is contract-binding. Implementations MUST evaluate stages in this sequence; search-first/filter-later and retrieval-first/filter-later semantics are forbidden across REST, SDK, CLI, MCP, and future Memories integration.

## D-9 transport headers

The request-side header `X-Hexalith-Retry-As: [caller, operator]` signals retry allocation. The response-side header `X-Hexalith-Retry-Transport: [stream]` signals transport substitution and is emitted with `413 Payload Too Large` on inline file mutations. The names are deliberately disjoint so a caller echoing back a response value cannot trip request-side validation. `FileInlineTooLargeProblem` carries the canonical category only; the configured byte limit is not surfaced in the response body to keep 413 disclosure safe for pre-authentication callers.

## Range-read safe-denial routing (deferred)

`ReadFileRange` currently surfaces sensitivity-denied paths under `416` with `category: redacted` and unsatisfiable byte ranges under `416` with `category: range_unsatisfiable`. The final safe-denial routing between `416 redacted` and `404 SafeAuthorizationDenial` is `TODO(reference-pending)` against the safe-denial-matrix follow-up story. Until that story lands, downstream adapters MUST treat both `416 redacted` and `404` as externally-indistinguishable from an unauthorized-existence-disclosure standpoint.

## Path policy class vocabulary (deferred)

`PathMetadata.pathPolicyClass` is a `^[a-z][a-z0-9_]{0,79}$`-bounded string. The closed enum of approved class values is `TODO(reference-pending)` against the path-policy-class definition story. Today the synthetic examples use `tenant_sensitive_document` as a placeholder. Downstream parity oracles MUST NOT hardcode this list; they SHOULD treat any class matching the pattern as opaque until the closed enum ships.

## Path-metadata Unicode policy (deferred)

`PathMetadata.normalizedPath` currently uses an ASCII-only character class. The `unicodeNormalization: NFC` enum is preserved for forward compatibility, but until the parser-policy story finalizes the Unicode allow-list (`\p{L}`, `\p{N}`, etc.), only ASCII paths are representable. Tenants requiring non-ASCII filenames cannot be onboarded until the parser-policy story lands.

## 429 rate-limit (deferred to Epic 4)

`CanonicalErrorCategory` includes `provider_rate_limited`, but Story 1.9 does not declare an HTTP `429 Too Many Requests` response. The 429 response shape with `Retry-After` semantics is `TODO(reference-pending)` against Epic 4 runtime, which owns provider integration. Until then, the category is foundation-only vocabulary; no operation emits 429.

## Negative Scope

This is contract-only work. It does not add REST handlers, EventStore commands, domain aggregate behavior, provider adapters, Git or filesystem side effects, generated SDK output, NSwag generation wiring, CLI commands, MCP tools, workers, UI pages, final parity rows, CI gates, repair automation, or nested-submodule initialization.
