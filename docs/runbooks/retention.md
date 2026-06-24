# Retention Runbook

This runbook is the operator-facing entry point for routine retention and cleanup cadence. It is metadata-only and uses synthetic examples only. It cross-links the authoritative retention sources rather than restating their policy tables.

## Purpose

Give operators the recurring-cadence view of retention: when read-model compaction, working-file cleanup, and idempotency-record expiry run, and how to confirm they are healthy. The authoritative per-class durations and the deletion disposition matrix live in the cross-linked sources and are not duplicated here.

## Preconditions

- The acting principal is authorized for the managed tenant before any tenant-scoped retention evidence is read; tenant authority comes from authenticated context, never from a query parameter.
- C3 retention durations are `approved`: explicit Legal + PM approval evidence is present (PM 2026-06-22; Legal 2026-06-24, Louveciennes), so the durations are production policy.
- No retention step performs cross-tenant search, provider payload inspection, raw file inspection, or credential review.

## Procedure

1. Confirm the retention class inventory against `../exit-criteria/c3-retention.md`; do not re-author the durations here.
2. Confirm the deletion disposition behavior against the preserved `./tenant-deletion.md` disposition matrix; this runbook does not duplicate that matrix.
3. Confirm operator cleanup and compaction cadence against `../operations/retention-and-tenant-deletion.md`: read-model views are rebuildable and may be dropped, temporary working files are disposable cache cleaned after terminal state, and audit and commit idempotency records are retained for the audit window.
4. For a tenant under review, record only tenant-scoped synthetic identifiers (for example `tenant-001`, `folder-001`, `operation-001`); never copy raw records.

## Verification

Run the conformance gate `pwsh ./tests/tools/run-adr-runbook-docs-gates.ps1`, which emits metadata-only evidence to `_bmad-output/gates/adr-runbook-docs/latest.json`. The retention policy evidence itself is validated by `pwsh ./tests/tools/run-retention-deletion-gates.ps1`. CI checkout keeps `submodules: false`; local setup initializes only root-level submodules with `git submodule update --init Hexalith.AI.Tools Hexalith.Commons Hexalith.EventStore Hexalith.FrontComposer Hexalith.Memories Hexalith.Tenants`.

## Escalation and handoff

- C3 approval evidence is recorded (PM 2026-06-22; Legal 2026-06-24); should it ever go missing or stale, escalate to Legal + PM rather than silently treating the criterion as covered.
- A failed cleanup or compaction cadence escalates to the on-call operator named in the alerts runbook (`./alerts.md`) with the tenant-scoped synthetic identifiers only.

## Related evidence

- `../exit-criteria/c3-retention.md` - authoritative per-class retention durations and the C3 approval state.
- `../operations/retention-and-tenant-deletion.md` - the retention/deletion operations posture and data classes.
- `./tenant-deletion.md` - the preserved tenant-deletion disposition matrix (not duplicated here).

## Forbidden evidence

Retention evidence is metadata-only. It must not include credentials, tokens, raw file contents, raw diffs, provider payload bodies, production endpoints, environment dumps, stack traces, host-absolute paths, or tenant data beyond synthetic ordinal identifiers.
