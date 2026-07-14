# Capacity Calibration

Story 7.10 pins the release targets for C1 capacity, C2 status freshness, and C5 scalability quantifiers. This lane is separate from the Story 7.7 `capacity-smoke-gates` PR check.

## Local command

Build first, then run the calibration gate:

```powershell
dotnet restore Hexalith.Folders.slnx -m:1 -p:NuGetAudit=false
dotnet build Hexalith.Folders.slnx --no-restore -m:1
pwsh ./tests/tools/run-capacity-calibration-gates.ps1
```

The gate runs:

```powershell
dotnet run --no-build --project tests/load/Hexalith.Folders.LoadTests.csproj -- --profile release-calibration --run-id capacity-calibration --report-folder _bmad-output/gates/capacity-calibration/reports
```

## Release targets

| Criterion | Target | Numeric value | Evidence field |
|---|---|---:|---|
| C1 | Maximum concurrent tenants | 4 | `target_comparison.c1.max_concurrent_tenants` |
| C1 | Folders per tenant | 2 | `target_comparison.c1.folders_per_tenant` |
| C1 | Active workspaces per tenant | 2 | `target_comparison.c1.active_workspaces_per_tenant` |
| C1 | Concurrent agent tasks per tenant | 2 | `target_comparison.c1.concurrent_agent_tasks_per_tenant` |
| C2 | Commit-to-status-read freshness lag | 500 milliseconds | `target_comparison.c2.max_commit_to_status_read_freshness_ms` |
| C5 | Tenant scale units | 4 | `target_comparison.c5.tenant_scale_units` |
| C5 | Folder scale units per tenant | 2 | `target_comparison.c5.folder_scale_units_per_tenant` |
| C5 | Workspace scale units per tenant | 2 | `target_comparison.c5.workspace_scale_units_per_tenant` |
| C5 | Agent task scale units per tenant | 2 | `target_comparison.c5.agent_task_scale_units_per_tenant` |
| C5 | Minimum lifecycle iteration rate | 1 operation per second | `target_comparison.c5.minimum_lifecycle_iterations_per_second` |

## Hardware and methodology

The target hardware profile is `github-actions-ubuntu-latest-or-local-hermetic`. Each run records the actual runner profile, processor count, process architecture, OS architecture, OS family, .NET target framework, and NBomber version in `_bmad-output/gates/capacity-calibration/latest.json`.

The `release-calibration` profile remains hermetic. It uses synthetic ordinal tenant, folder, workspace, task, operation, correlation, and idempotency identifiers. It does not require provider credentials, tenant seed data, Aspire, Dapr, Redis, GitHub, Forgejo, Docker, Testcontainers, production secrets, live endpoints, or nested submodule initialization. It does require a local Git checkout so the gate can stamp and verify the full source commit for same-commit evidence traceability.

## How targets were derived

The C1, C2, and C5 numbers are intentionally conservative MVP release targets, not production maximum claims. Each one traces to an observed `target_comparison` row in `_bmad-output/gates/capacity-calibration/latest.json` produced by the `release-calibration` harness run, so the value is measured rather than assumed. The per-target derivation rationale (why each number was chosen and how it inherits from C1) is recorded in the "Rationale" sections of `docs/exit-criteria/c1-capacity.md`, `docs/exit-criteria/c2-freshness.md`, and `docs/exit-criteria/c5-scalability-quantifiers.md`; release reviewers should read those sections for the derivation, and re-derive new numbers through a fresh calibration run when raising any target.

## Evidence reviewed for release

Release reviewers inspect:

- `docs/exit-criteria/c1-capacity.md`
- `docs/exit-criteria/c2-freshness.md`
- `docs/exit-criteria/c5-scalability-quantifiers.md`
- `docs/exit-criteria/c0-c13-governance-evidence.yaml`
- `_bmad-output/gates/capacity-calibration/latest.json`
- `_bmad-output/gates/capacity-calibration/reports/lifecycle-capacity-evidence.json`

The latest report must have `status: passed`, `profile_name: release-calibration`, the current full source commit, hardware profile, all five measured steps, nonzero observed counts, p50/p95/p99 latency statistics, throughput, C2 freshness observations, and passing C1/C2/C5 target comparisons.

## Smoke versus release calibration

`capacity-smoke-gates` is a PR confidence check. It runs the `quick` profile and proves the harness can execute the lifecycle/status path with metadata-only evidence. It does not carry release targets and cannot satisfy C1, C2, or C5.

`capacity-calibration` is release evidence. It runs the `release-calibration` profile and fails closed when target artifacts or same-commit evidence are missing, malformed, stale, unsafe, non-numeric, or inconsistent.

## C2 ownership

Story 7.10 owns the C2 numeric freshness target and release evidence. Story 7.12 may add production metric exporters, alert rules, dashboards, and notification routing, but it must not redefine the approved 500 millisecond target without a recalibration update.

The hermetic harness measures the real wall-clock commit-to-status-read interval, but because the in-process status read model resolves synchronously the observed `commit_to_status_read_ms` is expected to be only a few milliseconds — well under the 500 millisecond ceiling. Reviewers should read a small (non-degenerate) freshness observation as expected hermetic behavior; the 500 millisecond target is a forward-looking ceiling, and production commit-to-visibility lag measurement under asynchronous projection is owned by Story 7.12.

## Failure categories

The calibration gate blocks release for:

- Missing build output or load harness project.
- Missing C1, C2, C5, governance, or operations artifacts.
- Malformed JSON or target artifacts.
- Source commit mismatch.
- Missing hardware profile.
- Missing measured lifecycle/status step.
- Zero or partial execution.
- Missing latency, throughput, or freshness statistics.
- Missing, failed, or non-numeric target comparison.
- Absolute paths or unsafe diagnostic fields.

## Recalibration rule

Rerun calibration after runtime, provider, workspace lifecycle, lock, idempotency, projection, status, audit, NBomber, .NET SDK, or runner profile changes. If a target is changed, update the C1/C2/C5 artifact, governance evidence, conformance tests, and latest calibration report together.

## Submodule policy

Use only root-level submodule initialization:

```text
git submodule update --init references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.PolymorphicSerializations references/Hexalith.Tenants
```

Do not initialize nested submodules for this lane.
