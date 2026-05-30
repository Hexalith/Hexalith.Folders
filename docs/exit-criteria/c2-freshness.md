# C2 Freshness

status: approved release-calibration target
decision owner: Release Readiness
approval authority: Tech Lead
source inputs: Story 7.10, workspace status read model, tests/load release-calibration profile, `_bmad-output/gates/capacity-calibration/latest.json`
last reviewed: 2026-05-30
open questions: Story 7.12 owns production metric export and alert wiring for this target, not the numeric target itself.

## Decision

C2 pins a maximum acceptable lag of 500 milliseconds from accepted lifecycle commit evidence to status-read visibility under normal hermetic operation. The release-calibration harness measures the real wall-clock interval from committed status visibility to the completed status read through `WorkspaceStatusQueryHandler` and `InMemoryWorkspaceStatusReadModel`, recording each sample in `freshness_observations.commit_to_status_read_ms`. Because the hermetic read model resolves synchronously in-process, the observed lag is expected to be a few milliseconds at most — far below the 500 millisecond ceiling — but it is a measured value that traces to observed harness work and lets the C2 comparison fail closed if the status-read path regresses. Audit-view lag and production exporter/alert latency are explicitly deferred to Story 7.12.

| Target | Numeric value | Unit | Methodology | Run command | Evidence path | Observed result source | Rationale | Approval state | Owner | Authority | Review date | Rollback or recalibration rule |
|---|---:|---|---|---|---|---|---|---|---|---|---|---|
| Maximum commit-to-status-read freshness lag | 500 | milliseconds | Run `release-calibration`, record `commit_to_status_read_ms`, and require `target_comparison.c2.max_commit_to_status_read_freshness_ms.passed = true` | `pwsh ./tests/tools/run-capacity-calibration-gates.ps1` | `_bmad-output/gates/capacity-calibration/latest.json` | `freshness_observations.commit_to_status_read_ms` and `target_comparison.c2.max_commit_to_status_read_freshness_ms` | Aligns with the PRD status/audit summary p95 target while honestly limiting this story to hermetic status-read visibility | approved | Release Readiness | Tech Lead | 2026-05-30 | Any projection, status, audit, or read-model runtime change requires recalibration before release |

## Rationale

The target is numeric and release-blocking now. The scope is narrower than production observability: the hermetic harness measures status-read freshness after commit through the production query handler path (a small, synchronous read-your-writes interval), while Story 7.12 may add OpenTelemetry exporters, alert rules, and production dashboards without redefining the 500 millisecond C2 target. The 500 millisecond value is reused as a conservative freshness ceiling inspired by the PRD status/audit summary p95 bound; note that the PRD figure is a status/audit query-execution latency bound while C2 measures commit-to-status-read visibility lag, so the two are related but not identical quantities.

## Verification impact

Governance and release-package gates must fail if C2 points only to Story 7.12, if the target value is missing or non-numeric, or if calibration evidence is stale, failed, missing freshness observations, or missing the C2 target comparison row.

## Deferred implementation

Story 7.12 now implements the repository-local production observability intent for this target: the OpenTelemetry projection-lag metric instrument (`folders.projection.lag`), the `/health/ready` degraded-but-serving disposition keyed to the 500 millisecond C2 ceiling, and the sanitized exporter/health/alert-rule intent under `deploy/observability/production/`, validated by `pwsh ./tests/tools/run-production-observability-gates.ps1`. It reuses — and does not redefine — the 500 millisecond C2 target.

This document still does not implement live alert routing, audit-view SLO dashboards, live endpoints, provider credentials, Aspire, Dapr, Redis, Docker, Testcontainers, or tenant seed data. Live exporter/alert firing against a real backend remains reference-pending outside this repository per the operations runbook.
