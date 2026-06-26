# Incident Mode, Alerting Handoff, and Recovery Expectations

This document covers **incident-mode use**, the **alerting handoff**, and **backup/recovery expectations** for
the Hexalith.Folders operations surfaces. It documents **use, handoff, and expectations** — it does **not**
author step-by-step procedures (the alert/incident/rollback/reconciliation **runbooks** are Story 7.17). It
**references, does not redefine**, the Story 7.12 observability signals and the Story 7.11 retention artifacts.
Every example uses opaque synthetic identifiers (for example `folder_01HZY7Z6N7J4Q2X8Y9V0FLD001`,
`correlation-001`) and placeholder hosts only.

## Incident-mode use (F-6 / AR-UI-05)

The `/_admin/incident-stream?folder=` page is the **last-resort read path**, used when projections are
degraded. Its folder is supplied through the `?folder=` **query parameter** (see
[`operations-console.md`](operations-console.md)); **tenant authority always comes from the authenticated
context, never from the `?folder=` query value**. Access is **server-enforced** by an
`eventstore:permission=admin` ACL; a denial surfaces as a safe `ConsoleErrorPanel` carrying **only the
canonical category token** (no resource details).

The page is folder-scoped, metadata-only, and read-only, and is built on the **same
`IClient.ListOperationTimelineAsync` read** (Story 6.1) — it is not a separate event-stream reader. It enforces
**three mandatory guardrails**:

1. **Degraded-mode banner** — a persistent degraded-mode banner rendered in every state, showing the last
   projection checkpoint taken from the timeline read's `Freshness.ObservedAt`, formatted as UTC. When the
   checkpoint is absent it renders **`unknown`** — never `0001-01-01`, never `Current`.
2. **Operator disposition beside the raw technical state transition** — so operators read one vocabulary and do
   not switch between disposition labels and raw state names.
3. **One-click copy of `correlationId` + the UTC timestamp window** — for handoff and escalation.

**Redaction does not relax in incident mode.** The F-5 lock-icon affordances still apply; the last-resort path
exposes no additional value.

The deeper F-6 vision — a **projection-independent authoritative event-stream read** — is **`reference_pending`
and is not shipped in the MVP**. This document records the **as-built** capability honestly: incident mode is a
degraded-projection-tolerant read over the existing timeline projection, not an independent stream reader.

## Alerting handoff (Story 7.12 signals)

This section **references, does not redefine**, the five Story 7.12 operational signals. Their thresholds,
severities, and owners are authoritative in [`docs/operations/production-observability.md`](production-observability.md)
and `deploy/observability/production/observability.yaml`. This document adds **the operator action when each
signal fires**, naming concrete read-only next-step surfaces within 7.14 scope.

<!-- alert-signal-inventory -->

| Signal | Severity | Owner | Operator action (read-only next step) |
|---|---|---|---|
| `projection_lag` | warning | folders-server | Check `/health/ready` and open `/_admin/incident-stream?folder={folderId}` for the degraded read. |
| `dead_letter_depth` | warning | folders-workers | Inspect the operation-timeline read for stalled operations; escalate per the 7.17 runbook. |
| `provider_failure` | error | folders-workers | Open the Provider page read and the `GetWorkspaceCleanupStatus` read for affected workspaces. |
| `stale_lock` | warning | folders-server | Inspect the lock-status read for the workspace; there is no console unlock action. |
| `cleanup_failure` | error | folders-workers | Open the `GetWorkspaceCleanupStatus` read for the failed cleanup record. |

The threshold source for `projection_lag` is `docs/exit-criteria/c2-freshness.md` (500 ms). Everything is
**observe-only**: no signal triggers a mutation, an auto-release of a stale lock, or a repair automation.

### Health endpoints

- **`/health/live`** reports process liveness.
- **`/health/ready`** aggregates the Dapr sidecar health, the Tenants degraded-mode flag, and projection lag
  versus the C2 ceiling. When lag exceeds **500 ms** it reports **`degraded-but-serving` (HTTP 200)** rather
  than failing readiness.

**Live alert backends, dashboards, and paging are owned by the operations runbook OUTSIDE this repository and
are `reference_pending`.** This document does not claim that live alerting, dashboards, or on-call rotations
exist. The step-by-step alert/incident-mode runbooks are Story 7.17 — this document covers *use* and *handoff*.

## Backup and recovery expectations

This section captures **expectations**, **without overclaiming automation**. Per PRD NFR and architecture
concern #21 / D-8 / D-3 / Phase 9:

- **Authoritative records that must be preserved:** durable **events**, **audit metadata**, and
  **commit idempotency records**. The in-memory audit projection is append-only, tenant/folder-scoped,
  deterministic, and **rebuildable from operation observations** (Decision D-10).
- **Rebuildable projections:** the status, audit, and timeline projections are
  **rebuildable from the event streams**; read-model views are deleted/rebuildable.
- **Disposable cache:** **working copies are disposable cache** — "acceptably lost on restart", recovered by
  rebuild-from-events-plus-provider (D-8).
- **Durable-with-recovery-contract:** only **locks, idempotency records, and in-flight reconciliation tasks**
  require durable storage with a recovery contract (concern #21).

The **MVP repository ships no backup automation, no snapshot job, and no restore tooling.** This document
captures the *expectation* and what "release-validation evidence" means. Point-in-time recovery and
multi-region durability are a **deferred PostgreSQL-escalation criterion** (D-3, SLA-driven), not an MVP
feature.

### Cleanup-status-without-repair

`GetWorkspaceCleanupStatus` exposes exactly `pending` | `succeeded` | `failed` | `status_only`, plus a reason
code, an **advisory** retry-eligibility flag, a timestamp, and a correlation ID. It offers
**no repair, unlock, or discard** action — status only.

### Unknown-outcome reconciliation

Ambiguous commit outcomes enter `unknown_provider_outcome` (code `71`) or
`reconciliation_required` (code `72`). Both carry the **`awaiting_human`** disposition with metadata-only
reconciliation evidence and **no silent retry** — they wait for a human decision rather than auto-retrying.

### Retention dispositions (reference-pending, cross-linked)

The retention disposition vocabulary and tenant-deletion behavior are **referenced, not re-authored**, from
[`docs/exit-criteria/c3-retention.md`](../exit-criteria/c3-retention.md) and
[`docs/runbooks/tenant-deletion.md`](../runbooks/tenant-deletion.md). The four disposition tokens are:

<!-- retention-disposition-inventory -->

| Disposition |
|---|
| `deleted` |
| `tombstoned` |
| `retained` |
| `anonymized` |

C3 retention is **approved** (PM 2026-06-22; Legal 2026-06-24); per Story 7.11 AC11 this document still does **not** change the
`AuditTrailQueryHandler.RetentionClassToken` / `OperationTimelineQueryHandler.RetentionClassToken`
reference-pending markers, and the C3 `reference_pending_*` retention-class identifiers are likewise preserved (see [`audit-and-redaction.md`](audit-and-redaction.md)).

## Metadata-only policy

This document, the projections it describes, and the gate report are output channels subject to the
metadata-only invariant: no secrets, bearer tokens, credential material, raw file contents, diffs, provider
payloads, real issuer/audience values, production URLs, environment dumps, stack traces, tenant data, or
host-absolute paths. Examples use opaque synthetic identifiers and placeholder hosts only.

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
git submodule update --init references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.Tenants
```

Do not initialize nested submodules.

## Reviewer handoff and rerun rules

A reviewer should run the local validation command above, confirm the report reports `status: passed` with
`diagnostic_policy: metadata-only`, and confirm the alert-signal inventory stays synchronized with
`deploy/observability/production/observability.yaml` and the retention dispositions stay synchronized with the
C3/tenant-deletion sources. Rerun the gate after any change to the observability manifest, the incident-stream
guardrails, or the cross-linked retention artifacts. The static gate runs in the `contract-spine` CI lane and
through the baseline CI Contracts.Tests filter; it is not promoted to a new top-level `ci.yml` lane, to
`release-packages.yml`, or to scheduled workflows.
