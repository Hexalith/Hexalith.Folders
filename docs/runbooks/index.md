# Maintenance Runbooks

This index enumerates the maintenance runbooks under `docs/runbooks/`, one row per file, mapped to the BDD topic it covers. It is metadata-only.

<!-- runbook-index -->

| Runbook | BDD topic | Notes |
|---|---|---|
| `tenant-deletion.md` | tenant deletion | Preserved C3 tenant-deletion disposition matrix. |
| `retention.md` | retention | Operator retention and cleanup cadence; cross-links the C3 sources. |
| `alerts.md` | alerts | On-call triage of the five operational signals; live delivery `reference_pending`. |
| `rollback.md` | rollback | Release-package and container-image revert with post-rollback health verification. |
| `provider-drift.md` | provider drift | Operator response to additive, breaking, and unknown oasdiff drift. |
| `reconciliation.md` | reconciliation | Decision tree for ambiguous provider outcomes with no silent retry. |
| `incident-mode.md` | incident-mode operations | Last-resort `/_admin/incident-stream` read path and disposition labels. |

<!-- /runbook-index -->

## Maintenance

This index is conformance-checked by `pwsh ./tests/tools/run-adr-runbook-docs-gates.ps1`, which emits metadata-only evidence to `_bmad-output/gates/adr-runbook-docs/latest.json`. The gate fails closed if the index drifts from the files on disk (a missing entry or an orphan entry). CI checkout keeps `submodules: false`; local setup initializes only root-level submodules with `git submodule update --init Hexalith.AI.Tools Hexalith.Commons Hexalith.EventStore Hexalith.FrontComposer Hexalith.Memories Hexalith.Tenants`.
