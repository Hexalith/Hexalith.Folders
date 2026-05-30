# C1 Capacity

status: approved release-calibration target
decision owner: Release Readiness
approval authority: Tech Lead
source inputs: Story 7.10, tests/load release-calibration profile, `_bmad-output/gates/capacity-calibration/latest.json`
last reviewed: 2026-05-30
open questions: Production observability in Story 7.12 may add exporters and alerts, but it must not redefine these approved C1 target numbers without a recalibration record.

## Decision

C1 uses the hermetic lifecycle capacity harness with the `release-calibration` profile. The target hardware profile is `github-actions-ubuntu-latest-or-local-hermetic`; release review uses the generated hardware profile in `_bmad-output/gates/capacity-calibration/latest.json` to decide whether a rerun is required.

| Target | Numeric value | Unit | Methodology | Run command | Evidence path | Observed result source | Rationale | Approval state | Owner | Authority | Review date | Rollback or recalibration rule |
|---|---:|---|---|---|---|---|---|---|---|---|---|---|
| Maximum concurrent tenants | 4 | tenants | Run NBomber lifecycle scenario with synthetic tenant ordinals and validate target comparison row `c1.max_concurrent_tenants` | `pwsh ./tests/tools/run-capacity-calibration-gates.ps1` | `_bmad-output/gates/capacity-calibration/latest.json` | `target_comparison.c1.max_concurrent_tenants` | Conservative MVP release target that exercises tenant-scoped authorization, aggregate state, and status read paths without live providers | approved | Release Readiness | Tech Lead | 2026-05-30 | Any runtime/provider change or failed same-commit evidence blocks release until recalibrated |
| Folders per tenant | 2 | folders | Use `release-calibration` profile dimension and validate numeric evidence | `pwsh ./tests/tools/run-capacity-calibration-gates.ps1` | `_bmad-output/gates/capacity-calibration/latest.json` | `target_comparison.c1.folders_per_tenant` | Covers multiple folder streams per tenant while remaining hermetic | approved | Release Readiness | Tech Lead | 2026-05-30 | Recalibrate before increasing C4 bounds, provider fan-out, or workspace concurrency defaults |
| Active workspaces per tenant | 2 | workspaces | Use `release-calibration` profile dimension and measured lifecycle/status steps | `pwsh ./tests/tools/run-capacity-calibration-gates.ps1` | `_bmad-output/gates/capacity-calibration/latest.json` | `target_comparison.c1.active_workspaces_per_tenant` | Pins a numeric workspace concurrency target before MVP instead of relying on qualitative scalability claims | approved | Release Readiness | Tech Lead | 2026-05-30 | Failed or stale evidence reverts release status to blocked until the calibration gate passes |
| Concurrent agent tasks per tenant | 2 | tasks | Use `release-calibration` profile dimension and observed status-read completions | `pwsh ./tests/tools/run-capacity-calibration-gates.ps1` | `_bmad-output/gates/capacity-calibration/latest.json` | `target_comparison.c1.concurrent_agent_tasks_per_tenant` | Matches the current task-scoped workspace lock and idempotency evidence scope | approved | Release Readiness | Tech Lead | 2026-05-30 | Rerun after lock, idempotency, projection, or provider execution semantics change |

## Rationale

These values are intentionally conservative. They are numeric, source-traceable, and measured through the same lifecycle/status path used by the Story 7.7 smoke harness, but the release-calibration profile records target comparisons, hardware profile, latency statistics, throughput, and freshness evidence.

## Verification impact

Release readiness fails closed when this document, the C1 target rows, the calibration gate script, or same-commit calibration evidence is missing, malformed, stale, non-numeric, or unsafe. The quick smoke report remains non-production evidence and is not accepted as release calibration proof.

## Deferred implementation

This document does not add live provider load, Aspire, Dapr, Redis, Docker, Testcontainers, production tenant data, or production alerting. Production metric export and alert wiring are deferred to Story 7.12.
