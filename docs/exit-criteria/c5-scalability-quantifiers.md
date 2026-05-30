# C5 Scalability Quantifiers

status: approved release-calibration target
decision owner: Release Readiness
approval authority: Tech Lead
source inputs: Story 7.10, C1 capacity targets, tests/load release-calibration profile, `_bmad-output/gates/capacity-calibration/latest.json`
last reviewed: 2026-05-30
open questions: Larger production capacity targets require a new calibration run and approval record before release claims change.

## Decision

C5 replaces vague scalability language with machine-readable quantifiers derived from C1. Release review must find the `target_comparison.c5` section in `_bmad-output/gates/capacity-calibration/latest.json`; missing C5 rows fail closed even when C1 rows exist.

| Quantifier | Numeric value | Unit | Derived from | Methodology | Run command | Evidence path | Observed result source | Rationale | Approval state | Owner | Authority | Review date | Rollback or recalibration rule |
|---|---:|---|---|---|---|---|---|---|---|---|---|---|---|
| Tenant scale units | 4 | tenants | C1 maximum concurrent tenants | Validate `target_comparison.c5.tenant_scale_units` | `pwsh ./tests/tools/run-capacity-calibration-gates.ps1` | `_bmad-output/gates/capacity-calibration/latest.json` | `target_comparison.c5.tenant_scale_units` | Makes tenant scalability reviewable by governance automation | approved | Release Readiness | Tech Lead | 2026-05-30 | Missing, failed, or stale same-commit evidence blocks release |
| Folder scale units per tenant | 2 | folders | C1 folders per tenant | Validate `target_comparison.c5.folder_scale_units_per_tenant` | `pwsh ./tests/tools/run-capacity-calibration-gates.ps1` | `_bmad-output/gates/capacity-calibration/latest.json` | `target_comparison.c5.folder_scale_units_per_tenant` | Converts folder scale into an explicit release number | approved | Release Readiness | Tech Lead | 2026-05-30 | Recalibrate after folder stream, repository binding, or provider-readiness changes |
| Workspace scale units per tenant | 2 | workspaces | C1 active workspaces per tenant | Validate `target_comparison.c5.workspace_scale_units_per_tenant` | `pwsh ./tests/tools/run-capacity-calibration-gates.ps1` | `_bmad-output/gates/capacity-calibration/latest.json` | `target_comparison.c5.workspace_scale_units_per_tenant` | Converts workspace concurrency into a governed release target | approved | Release Readiness | Tech Lead | 2026-05-30 | Recalibrate after workspace lifecycle, cleanup, or projection changes |
| Agent task scale units per tenant | 2 | tasks | C1 concurrent agent tasks per tenant | Validate `target_comparison.c5.agent_task_scale_units_per_tenant` | `pwsh ./tests/tools/run-capacity-calibration-gates.ps1` | `_bmad-output/gates/capacity-calibration/latest.json` | `target_comparison.c5.agent_task_scale_units_per_tenant` | Pins task-level concurrency for MVP release evidence | approved | Release Readiness | Tech Lead | 2026-05-30 | Recalibrate after lock, task, or idempotency semantics change |
| Minimum lifecycle iteration rate | 1 | operations per second | Release-calibration profile | Validate `target_comparison.c5.minimum_lifecycle_iterations_per_second` | `pwsh ./tests/tools/run-capacity-calibration-gates.ps1` | `_bmad-output/gates/capacity-calibration/latest.json` | `throughput.lifecycle_iterations_per_second` | Ensures scalability evidence includes a rate, not only dimensions | approved | Release Readiness | Tech Lead | 2026-05-30 | A lower observed rate blocks release until target or implementation is recalibrated and approved |

## Rationale

C5 inherits C1 for core dimensions but exposes separate C5 rows so governance automation can detect missing scalability quantifiers. The values describe the MVP release target, not a production maximum claim.

## Verification impact

Static conformance tests and release gates must parse this artifact, the governance evidence YAML, the calibration script, and the latest calibration report. They must fail on narrative-only C5 claims, non-numeric values, missing C5 target comparison rows, stale source commits, unsafe diagnostics, or absolute artifact paths.

## Deferred implementation

This document does not publish packages, change PR smoke thresholds, create live provider tests, initialize nested submodules, or deploy production observability.
