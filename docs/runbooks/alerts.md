# Alerts Runbook

This runbook is the operator-facing on-call view of the five operational alert signals. It is metadata-only and uses synthetic examples only. It cross-links the authoritative observability sources rather than restating their thresholds.

## Purpose

Give the on-call operator a single place to interpret the five operational signals, triage their severity, and follow an escalation checklist. The signal thresholds and the observability wiring live in the cross-linked sources and are not duplicated here.

## Preconditions

- The acting principal is authorized for the managed tenant before any tenant-scoped signal evidence is read; tenant authority comes from authenticated context, never from a query parameter.
- An OTLP backend and the `/health/ready` aggregation are reachable; otherwise treat the dashboard as stale and rely on the incident-mode runbook (`./incident-mode.md`).
- No triage step performs cross-tenant search, provider payload inspection, raw file inspection, or credential review.

## Procedure

The five operational signals and their first-response triage:

| Signal | Severity | First operator response |
|---|---|---|
| `projection_lag` | warning | Confirm `/health/ready` reports `degraded-but-serving`; the system is still serving. Watch for sustained lag past the C2 ceiling. |
| `dead_letter_depth` | warning | Inspect dead-letter backlog growth; a sustained climb indicates a stuck consumer in `folders-workers`. |
| `provider_failure` | error | Cross-check the provider-drift runbook (`./provider-drift.md`); distinguish transient failures from drift. |
| `stale_lock` | warning | Confirm whether an orphaned lock has uncommitted changes; this maps to the reconciliation runbook (`./reconciliation.md`). |
| `cleanup_failure` | error | Confirm the disposable working-copy cleanup; escalate if cleanup keeps failing for the same tenant-scoped task. |

Escalation checklist:

1. Capture the signal name, severity, correlation ID, and UTC timestamp window (metadata-only).
2. Confirm whether the signal is transient or sustained before paging.
3. For `error`-class signals that persist, page the on-call operator and attach only the tenant-scoped synthetic identifiers.

Live alert delivery (paging integrations, on-call rotation tooling) is `reference_pending` (NFR54, owner: Operations Runbook, consuming story `7-17`); this runbook documents the operator process, it does not claim live delivery tooling exists.

## Verification

Run the conformance gate `pwsh ./tests/tools/run-adr-runbook-docs-gates.ps1`, which emits metadata-only evidence to `_bmad-output/gates/adr-runbook-docs/latest.json`. The signal inventory and thresholds are validated by `pwsh ./tests/tools/run-production-observability-gates.ps1`. CI checkout keeps `submodules: false`; local setup initializes only root-level submodules with `git submodule update --init references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.Tenants`.

## Escalation and handoff

- Sustained `error`-class signals (`provider_failure`, `cleanup_failure`) escalate to the on-call operator; hand off the correlation ID and UTC window only.
- A signal that cannot be interpreted from the dashboard hands off to the incident-mode runbook (`./incident-mode.md`) for the last-resort read path.

## Related evidence

- `../operations/incident-alerting-and-recovery.md` - the alert-signal inventory, severities, and incident-mode contract.
- `../operations/production-observability.md` - the OTLP wiring, health endpoints, and threshold sources.

## Forbidden evidence

Alert evidence is metadata-only. It must not include credentials, tokens, raw file contents, raw diffs, provider payload bodies, production endpoints, environment dumps, stack traces, host-absolute paths, or tenant data beyond synthetic ordinal identifiers.
