# Metadata-Only Audit and Redaction

This document describes the **metadata-only** audit surfaces (FR53/FR54) and the **visibly-distinct redaction**
behavior (F-5) of the Hexalith.Folders read model. It **reflects** the contracts — the audit projection DTOs
under `src/Hexalith.Folders.Contracts/Projections/Audit/`, the emission record
`src/Hexalith.Folders/Observability/FolderAuditObservation.cs`, and the OpenAPI Contract Spine — it never
redefines them. Every example uses opaque synthetic identifiers (for example `operation-001`, `tenant-001`,
`correlation-001`) and placeholder hosts only.

The MCP audit surface (1 resource `folders://audit-trail/{folderId}` and 4 audit tools) is already documented
by [`docs/sdk/mcp-reference.md`](../sdk/mcp-reference.md) (Story 7.13) — cross-link, not redefined here.

## Audit query operations

The audit family exposes **four GET query operations**, all eventually-consistent and all metadata-only:

<!-- audit-operation-inventory -->

| operationId | Path | Handler |
|---|---|---|
| `ListAuditTrail` | `/api/v1/folders/{folderId}/audit-trail` | ListAuditTrail |
| `GetAuditRecord` | `/api/v1/folders/{folderId}/audit-trail/{auditRecordId}` | GetAuditRecord |
| `ListOperationTimeline` | `/api/v1/folders/{folderId}/operation-timeline` | ListOperationTimeline |
| `GetOperationTimelineEntry` | `/api/v1/folders/{folderId}/operation-timeline/{timelineEntryId}` | GetOperationTimelineEntry |

All four are `GET` operations under the `audit` tag and **reject `Idempotency-Key`** with `400`
`validation_error` / `idempotency_key_not_allowed` (idempotency keys are only meaningful for mutations, and
there are no audit mutations).

### Pagination

List operations page with an **opaque cursor** matching `^cursor_[A-Za-z0-9_-]{8,247}$`. The `limit` parameter
is **accepted in `[1,1000]` but the effective limit is clamped to 100** (= the `entries` `maxItems`). The
clamp is surfaced through `PaginationMetadata.limit` (effective) alongside `requestedLimit` (as asked).
Truncation is signaled by **`isTruncated`** (not `hasMore`) plus an optional `truncatedReason`. The
`PaginationMetadata` fields are `limit`, `isTruncated`, `cursor?`, `requestedLimit?`, `truncatedReason?`.

### Freshness

Every audit/timeline page carries `FreshnessMetadata` with fields `readConsistency`, `observedAt`,
`projectionWatermark?`, `stale`, and `reasonCode?`. Audit reads are eventually-consistent: a stale read
reports `stale: true` with a `reasonCode`, never an error.

### Retention markers (reference-pending)

Each page carries a `retentionClass` marker that is **reference-pending until Legal + PM approval of C3**. The
handlers emit explicit `TODO(reference-pending):` markers —
`AuditTrailQueryHandler.RetentionClassToken` (`TODO(reference-pending): C3 audit metadata retention approval`)
and `OperationTimelineQueryHandler.RetentionClassToken` (`TODO(reference-pending): C3 timeline retention
approval`). This document does **not** present a resolved retention class; see
[`incident-alerting-and-recovery.md`](incident-alerting-and-recovery.md) and
[`docs/exit-criteria/c3-retention.md`](../exit-criteria/c3-retention.md).

## FR54 audit field catalog

The FR54 audit field catalog (actor, tenant, task, operation ID, correlation ID, provider, repository binding,
folder, path metadata, result, timestamp, status, commit reference) is mapped to the **consumer-facing wire
DTOs** by exact camelCase field name. The authoritative source is
`src/Hexalith.Folders.Contracts/Projections/Audit/AuditRecord.cs` and `OperationTimelineEntry.cs`.

### `AuditRecord` fields

<!-- audit-record-fields -->

| Field | Type | Optional |
|---|---|---|
| `auditRecordId` | string | no |
| `actorReference` | RedactableAuditActorReference | no |
| `operationId` | RedactableAuditOperationReference | no |
| `correlationId` | string | no |
| `resultStatus` | string | no |
| `sanitizedErrorCategory` | string | no |
| `retryable` | bool | no |
| `durationMilliseconds` | long | no |
| `evidenceTimestamp` | RedactableAuditTimestamp | no |
| `redaction` | RedactionMetadata | no |
| `freshness` | FreshnessMetadata | no |
| `taskId` | string | yes |
| `changedPathEvidence` | ChangedPathEvidence | yes |

### `OperationTimelineEntry` fields

<!-- operation-timeline-fields -->

| Field | Type | Optional |
|---|---|---|
| `timelineEntryId` | string | no |
| `operationId` | string | no |
| `taskId` | string | no |
| `correlationId` | string | no |
| `workspaceReference` | RedactableDiagnosticIdentifier | no |
| `stateTransition` | DiagnosticStateTransition | no |
| `sanitizedResult` | string | no |
| `retryable` | bool | no |
| `durationMilliseconds` | long | no |
| `evidenceTimestamp` | DateTimeOffset | no |
| `freshness` | FreshnessMetadata | no |

The nested `stateTransition` object carries `fromState`, `toState`, and `disposition` (the operator
disposition, so the timeline never forces a vocabulary switch).

## FR53 operation-kind and result taxonomy

The emission record `FolderAuditObservation` classifies every observed operation by a
`FolderAuditOperationKind` and a `FolderAuditResult`. These cover successful, denied, failed, retried, and
duplicate outcomes. The taxonomy is authoritative in
`src/Hexalith.Folders/Observability/FolderAuditOperationKind.cs` and `FolderAuditResult.cs`.

### `FolderAuditOperationKind` (13 members)

<!-- operation-kind-taxonomy -->

| Operation kind |
|---|
| `Unknown` |
| `RestMutation` |
| `RestQuery` |
| `ProcessCommand` |
| `ProviderReadiness` |
| `EventStoreGateway` |
| `ReadModel` |
| `StateTransition` |
| `FileOperation` |
| `CommitOperation` |
| `LockOperation` |
| `CleanupStatus` |
| `ContextQuery` |

### `FolderAuditResult` (11 members)

<!-- operation-result-taxonomy -->

| Result |
|---|
| `Unknown` |
| `Success` |
| `Denied` |
| `Failed` |
| `Rejected` |
| `Duplicate` |
| `Retried` |
| `Replayed` |
| `Locked` |
| `Stale` |
| `Unavailable` |

## Authorization before observation

Audit reads are authorized **before** any observation reaches the read model, in a fixed deny-by-default order:

1. **JWT validation**.
2. **EventStore claim transform**.
3. **Tenant-access projection freshness**.
4. **Folder ACL** evidence.
5. **Audit-reviewer scope** — `eventstore:permission` `audit:read`, evaluated through the existing
   **`read_metadata` action token. There is no `read_audit` action token.**
6. **Diagnostic-audience partition**, then the **read model**.

Denials are **safe**: the `AuditQueryResultCode` set (`AuthenticationRequired`, `TenantAccessDenied`,
`FolderAclDenied`, `AuditAccessDenied`, `NotFoundSafe`) is externally indistinguishable — an authorization,
tenant, ACL, audit-scope, or not-found denial all surface the same way, so a caller cannot probe for the
existence of a resource it is not entitled to see.

## Redaction behavior (F-5)

The redaction rule (F-5 / architecture concern #11) is the **visibly-distinct** rule:
**redacted MUST be visibly distinct from unknown and from missing**.
Silent truncation or hiding is an operator-facing correctness bug. Two distinct vocabularies express this, and
they are **never conflated**.

### Wire vocabulary — `RedactionVisibility` (2 members)

The wire enum `RedactionVisibility`
(`src/Hexalith.Folders.Contracts/Projections/Audit/RedactionVisibility.cs`) has **exactly 2 members**, plus a
`reasonCode` matching `^[a-z][a-z0-9_]{0,79}$` on `RedactionMetadata`:

<!-- redaction-wire-vocabulary -->

| Visibility (wire) |
|---|
| `metadata_only` |
| `redacted` |

### Presentation vocabulary — `FieldDisclosure` (4 members)

The presentation enum `FieldDisclosure` (`src/Hexalith.Folders.UI/Services/FieldDisclosure.cs`) has **4
members**, each carrying a distinct `data-fc-disclosure` token. It is **not** a wire enum (it carries no
`[EnumMember]` attribute and never crosses the wire); it is the single rendering classification the four
field-shaped SDK redaction signals collapse into, via `RedactionDisclosureMapper`:

<!-- redaction-presentation-vocabulary -->

| Disclosure | data-fc-disclosure | Affordance |
|---|---|---|
| `Visible` | visible | value rendered as-is |
| `Redacted` | redacted | lock-icon affordance + explanatory text |
| `Unknown` | unknown | distinct, no lock icon |
| `Missing` | missing | distinct, no lock icon |

Only `Redacted` renders a lock-icon affordance; `Unknown` and `Missing` render distinctly and carry no lock
icon — that is how redacted stays visibly distinct from unknown and missing.

### Redacted-implies-no-value invariant

The conditional `allOf` on `RedactableAuditActorReference`, `RedactableAuditOperationReference`,
`RedactableDiagnosticIdentifier`, and `RedactableAuditTimestamp` **forbids a `value`** when
`redaction.visibility: redacted`. A redacted reference therefore has no leaked value to disclose — the
`value` field is absent, not blanked.

### Sensitive-metadata tiers (S-6) and the sanitizer blocklist

The S-6 default tier classifies paths, repository names, branch names, and commit messages as
`tenant_sensitive`. A per-tenant `confidential` upgrade is **hashed at the projection layer, never stored or
emitted in cleartext**. The server blocklist `FolderAuditSanitizer.IsSensitiveDiagnosticValue`
(`src/Hexalith.Folders/Observability/FolderAuditSanitizer.cs`) rejects any diagnostic value that contains a
sensitive substring: `token`, `synthetic`, `secret`, `password`, `credential`, `repository`, `repo_`,
`repo-`, a scheme separator (`://`), an at-sign (`@`), a backslash, a forward slash, `diff`, `payload`,
`privatekey`, `private_key`, or `installation`. A value that fails the blocklist (or the identifier/category
pattern) is dropped, never emitted.

### Duration bucketing and the correlation/task exception

On **redacted** records, `durationMilliseconds` is **bucketed to 100 ms** (`FolderAuditSanitizer.DurationBucket`,
`Math.Ceiling(ms / 100) * 100`) as a side-channel mitigation; exact durations are emitted **only on
non-redacted** records.

`correlationId` and `taskId` are an intentional exception. They are **plain `string` fields** on `AuditRecord`
and `OperationTimelineEntry` (not `Redactable*` types), so they are **not subject to the
redacted-implies-no-value invariant** and **remain populated even on records where the redactable fields
(actor, operation, timestamp) are redacted**. They are still **sanitized at emission** by
`FolderAuditSanitizer` like any other identifier. They stay populated specifically to allow **cross-surface
incident stitching**; they are opaque, tenant-scoped, and non-authoritative (they never establish tenant
authority).

### Changed-path evidence and OpenAPI status

`ChangedPathEvidence` is **digest/reference only** — it carries `evidenceKind`, `classification`, an optional
`digest`, and an optional `reference`, and never a raw path, file content, or patch. The OpenAPI status
`redacted` (code `75`) is informational.

Do **not** cite the phantom redaction classes referenced by the 6.1/6.4 artifacts — they do not exist. The
real redaction sources are `FolderAuditSanitizer`, `RedactionDisclosureMapper`, and `FieldDisclosure`.

## Metadata-only policy

This document, the audit projections, and the gate report are output channels subject to the metadata-only
invariant: no secrets, bearer tokens, credential material, raw file contents, base64 file bytes, diffs,
provider payloads, real issuer/audience values, production URLs, environment dumps, stack traces, tenant data,
or host-absolute paths. Examples use opaque synthetic identifiers and placeholder hosts only.

## Local validation

Run the focused gate from the repository root:

```text
pwsh ./tests/tools/run-operations-audit-docs-gates.ps1
```

The gate runs the `OperationsAuditDocsConformanceTests` and writes a metadata-only report to
`_bmad-output/gates/operations-audit-docs/latest.json`. Pass `-SkipRestoreBuild` (alias `-NoRestore`) when the
shared restore/build lane already ran. If the sandbox denies VSTest socket creation, the gate falls back to the
xUnit v3 in-process runner and still enforces the non-vacuous test-count guard.

If submodule working trees are missing, initialize only the root-level modules:

```text
git submodule update --init Hexalith.AI.Tools Hexalith.Commons Hexalith.EventStore Hexalith.FrontComposer Hexalith.Memories Hexalith.Tenants
```

Do not initialize nested submodules.

## Reviewer handoff and rerun rules

A reviewer should run the local validation command above, confirm the report reports `status: passed` with
`diagnostic_policy: metadata-only`, and confirm the audit field catalog, the operation-kind/result taxonomy,
and the redaction vocabularies stay synchronized with the DTOs, `FolderAuditObservation`, `RedactionVisibility`,
and `FieldDisclosure`. Rerun the gate after any change to the audit projection DTOs, the observation enums, the
sanitizer blocklist, or the redaction enums. The static gate runs in the `contract-spine` CI lane and through
the baseline CI Contracts.Tests filter; it is not promoted to a new top-level `ci.yml` lane, to
`release-packages.yml`, or to scheduled workflows.
