# Tenant Deletion Runbook

This runbook defines the C3 tenant-deletion evidence posture for release review. It is metadata-only and uses synthetic examples only.

## Authorization prerequisites

- Confirm the tenant-deletion request is approved by the authorized operator workflow.
- Confirm Legal + PM approval state for C3 before treating retention durations as production policy.
- Confirm the acting principal is authorized for the managed tenant before reading tenant-scoped evidence.
- Confirm no manual step performs cross-tenant search, provider payload inspection, raw file inspection, or credential review.

## Disposition matrix

| Data class | Disposition | Manual or automated step | Retention behavior | Metadata-only audit reconstruction | Tenant isolation rule | Synthetic evidence example |
|---|---|---|---|---|---|---|
| Audit metadata | retained | automated retention, manual approval review | Keep C3 audit duration and remove display aliases when anonymization applies | Preserve operation category, outcome, timestamps, correlation ID, and tenant-scoped operation ID | Query only by authorized managed tenant | `tenant-001` counted `operation-001` |
| Workspace status | tombstoned | automated compaction, manual exception review | Keep terminal status metadata for C3 status duration and tombstone task-local labels | Preserve completed, failed, denied, retried, duplicate, and interrupted status categories | Status keys remain tenant and folder scoped | `tenant-001` tombstoned `task-001` |
| Provider correlation IDs | retained | automated provider-diagnostics compaction | Keep bounded request and operation identifiers without provider bodies | Preserve upstream status class and correlation category only | Correlation lookup requires tenant authorization | `tenant-001` retained `provider-ref-001` |
| Read-model views | deleted | automated projection drop or rebuild | Delete rebuildable projection rows when events can rebuild authorized state | Reconstruction comes from retained events and audit metadata, not stale projection rows | Projection keys include tenant scope and fail closed when revoked | `tenant-001` deleted `projection-001` |
| Temporary working files | deleted | automated cleanup worker after terminal state | Delete disposable working-copy cache after C3 working-file duration | Retain only cleanup metadata, task ID, outcome category, and timestamp | Working paths are tenant, folder, and task scoped | `tenant-001` deleted `workspace-cache-001` |
| Cleanup records | retained | automated retention compaction, manual failed-cleanup review | Keep cleanup attempts, failures, retryability, reason code, and correlation ID | Preserve cleanup lifecycle evidence without file contents | Cleanup evidence stays tenant-prefixed | `tenant-001` retained `cleanup-001` |
| Folder metadata and soft-delete markers | tombstoned | automated archive workflow, manual legal-hold review | Tombstone folder identity and hierarchy metadata | Preserve audit-safe identifiers required by approved retention workflow | Folder identifiers stay opaque and tenant scoped | `tenant-001` tombstoned `folder-001` |
| Auth claims copied into metadata | anonymized | automated alias removal, manual approval review | Retain transformed tenant claim and permission category; remove display alias | Preserve authoritative ID linkage while removing display alias | Payload tenant values are comparison inputs only | `tenant-001` anonymized `principal-001` |
| Diagnostics and rejected-command records | retained | automated error-record compaction | Keep canonical error category, retryability, client action, and bounded metadata | Preserve denial evidence without protected resource disclosure | Denials remain indistinguishable across unauthorized and nonexistent resources | `tenant-001` retained `diagnostic-001` |
| Commit idempotency records | retained | automated idempotency retention | Keep commit operation IDs for the audit metadata duration | Preserve replay and reconciliation evidence for commit outcomes | Idempotency keys and operation IDs remain tenant scoped | `tenant-001` retained `commit-001` |

## Manual review checklist

- Verify C3 remains `reference_pending` unless explicit approval records are present.
- Verify deleted and tombstoned records are counted by tenant-scoped synthetic IDs only.
- Verify retained records are metadata-only.
- Verify anonymized records remove display aliases while preserving authoritative tenant-scoped IDs.
- Verify audit and commit idempotency evidence remain available for replay and reconciliation.
- Verify temporary working files are treated as disposable cache.
- Verify cleanup records describe attempts and outcomes without raw file contents.

## Automated validation

Run:

```powershell
pwsh ./tests/tools/run-retention-deletion-gates.ps1
```

The gate validates the C3 matrix, this runbook, the operations document, governance evidence, release package wiring, current source commit, metadata-only diagnostics, and root-level submodule policy.

## Forbidden evidence

Tenant-deletion evidence must not include credentials, raw file contents, raw diffs, provider payload bodies, production endpoints, environment dumps, stack traces, local absolute paths, or tenant data beyond synthetic ordinal IDs.
