# Audit and Ops-Console Contract Groups

Story 1.11 adds only OpenAPI Contract Spine declarations for audit trail, operation timeline, read-only diagnostics, and projection freshness. The contracts are metadata-only and tenant scoped; they do not define runtime behavior, provider adapters, UI surfaces, adapter wrappers, or release gates.

## Operation Inventory

| Operation | Group | Scope | Read consistency | Notes |
|---|---|---|---|---|
| `ListAuditTrail` | AuditQueries | folder audit page | eventually_consistent | Page and filter inputs select candidate evidence only after authorization. |
| `GetAuditRecord` | AuditQueries | single audit record | eventually_consistent | Safe-denial hides missing, wrong-tenant, redacted, and unavailable records. |
| `ListOperationTimeline` | OperationTimelineQueries | folder timeline page | eventually_consistent | Uses C6 state and operator disposition vocabulary. |
| `GetOperationTimelineEntry` | OperationTimelineQueries | single timeline entry | eventually_consistent | Exposes sanitized state transition metadata only. |
| `GetReadinessDiagnostics` | OpsConsoleDiagnostics | tenant readiness summary | eventually_consistent | Audience partition prevents consumer callers from seeing operator-only evidence. |
| `GetLockDiagnostics` | OpsConsoleDiagnostics | folder/workspace lock summary | eventually_consistent | Lock identifiers remain opaque and safe-denied before authorization. |
| `GetDirtyStateDiagnostics` | OpsConsoleDiagnostics | folder/workspace dirty-state summary | eventually_consistent | Changed-path evidence is digest/reference metadata. |
| `GetFailedOperationDiagnostics` | OpsConsoleDiagnostics | folder/workspace failure summary | eventually_consistent | Failure evidence is sanitized category plus retry/escalation posture. |
| `GetProviderStatusDiagnostics` | OpsConsoleDiagnostics | folder provider status summary | eventually_consistent | Provider evidence is sanitized and classified. |
| `GetSyncStatusDiagnostics` | OpsConsoleDiagnostics | folder/workspace sync summary | eventually_consistent | Accepted command state, projection state, and provider outcome stay separate. |
| `GetProjectionFreshness` | ProjectionFreshness | tenant projection freshness | eventually_consistent | C5 targets remain `TODO(reference-pending): C5 projection freshness target`. |

## Evidence Map

| Evidence | Source | Contract use | Approval state |
|---|---|---|---|
| C3 retention | `docs/exit-criteria/c3-retention.md` | Audit and timeline retention class fields | Proposed workshop value; Legal and PM approval still reference-pending. |
| C4 limits | `docs/exit-criteria/c4-input-limits.md` | Page size, elapsed time, truncation, and response-bound metadata | Proposed workshop value; PM approval still reference-pending. |
| C5 freshness | No approved artifact found for audit/diagnostic freshness targets | Projection freshness fields and stale/unavailable reason codes | `TODO(reference-pending): C5 projection freshness target`. |
| C6 lifecycle | `docs/exit-criteria/c6-transition-matrix-mapping.md` | Lifecycle state and operator-disposition reuse | Approved vocabulary; runtime transitions remain deferred. |
| Story 1.6 vocabulary | `src/Hexalith.Folders.Contracts/openapi/extensions/hexalith-extension-vocabulary.yaml` | `x-hexalith-*` declarations, correlation, authorization, redaction, parity, and metadata classification | Reused rather than redefined. |

## Audience And Field Classification Matrix

| Field family | Consumer audience | Authorized operator audience | Forbidden |
|---|---|---|---|
| Status code, disposition, retry posture | `consumer_safe` | `operator_sanitized` | English display text as API logic |
| Folder, workspace, task, operation, lock, provider references | Redacted or omitted unless already authorized | `operator_sanitized` opaque identifiers | Tenant authority, ACL override, or existence probe |
| Changed-path evidence | Digest/reference count only when authorized | Digest/reference metadata and sensitivity tier | Visible path lists, file contents, or patch payloads |
| Provider diagnostics | Sanitized status class only | Sanitized capability and correlation references | Credential material, provider payloads, account names, repository locations |
| Audit/timeline records | Safe-denial if audit scope is missing | Metadata-only actor/task/operation/correlation/state evidence | Event bodies, payload excerpts, prompts, generated context payloads, stack traces |
| Projection freshness | Availability, stale/unavailable code, watermark age | Same plus operator-only sanitized identifiers | Tenant selection, permission decisions, or hidden-resource counts inside cursors |

## Safe-Denial and Cursor Rules

Unauthorized, wrong-tenant, hidden, redacted, unknown, missing, stale, projection-unavailable, tampered-cursor, changed-filter, tenant/principal-mismatch, invalid-sort, boundary-duplicate, and empty-page continuation cases all preserve safe-denial parity. Cursors are service-issued opaque non-authoritative values; they cannot encode tenant authority, ACL decisions, raw query text, provider material, or unredacted path lists.

## Deferred Decisions

- Final C5 freshness/performance targets remain reference-pending.
- C3 retention values remain proposed until Legal and PM approval.
- Hidden-resource equivalence and cursor invalidation semantics are recorded at the contract level and remain implementation-owned by later runtime stories.
- Localization keys may be added later as presentation metadata; machine-readable codes remain the API semantics.

## Verification

Focused validation lives in `tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuditOpsConsoleContractGroupTests.cs`. It checks operation allow-lists, local `$ref` resolution, non-mutating read consistency, safe-denial metadata, synthetic examples, audience/field classification, evidence notes, and negative-scope boundaries.
