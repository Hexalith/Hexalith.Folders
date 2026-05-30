# Load Tests

This directory contains the hermetic lifecycle capacity harness for repository-backed workspace prepare, lock, mutate, commit, and status-read scenarios.

The harness uses synthetic tenant, folder, workspace, task, operation, correlation, and idempotency identifiers only. It does not require provider credentials, tenant seed data, Aspire, Dapr, Redis, GitHub, Forgejo, Docker, Testcontainers, network services, production secrets, or nested submodule initialization. The release-calibration gate does require a local Git checkout so that calibration evidence can record the full source commit for same-commit traceability.

## Quick Mode

Run a short local verification with a temporary report folder:

```bash
dotnet run --project tests/load/Hexalith.Folders.LoadTests.csproj -- --profile quick --report-folder artifacts/load-reports/quick
```

Run the focused self-checks without starting NBomber:

```bash
dotnet run --project tests/load/Hexalith.Folders.LoadTests.csproj -- --self-check --profile quick --report-folder artifacts/load-reports/self-check
```

## Profiles

- `quick`: tiny local profile for harness verification. It uses no production thresholds and must not be treated as C1, C2, or C5 release evidence.
- `release-calibration`: hermetic release profile for C1, C2, and C5 evidence. It records target comparisons, hardware profile, latency statistics, throughput, and status-read freshness observations.

The Story 7.7 PR capacity-smoke gate uses `--profile quick`, `--run-id capacity-smoke-ci`, and `_bmad-output/gates/capacity-smoke-ci/reports` as its CI-controlled report folder. The stable blocking check is `capacity-smoke-gates`; the local orchestrator is `pwsh ./tests/tools/run-capacity-smoke-ci-gates.ps1`.

Story 7.10 release calibration uses `--profile release-calibration`, `--run-id capacity-calibration`, and `_bmad-output/gates/capacity-calibration/reports`. The stable release check is `capacity-calibration`; the local orchestrator is `pwsh ./tests/tools/run-capacity-calibration-gates.ps1`.

## Evidence

Each run writes `lifecycle-capacity-evidence.json` in the report folder. The sidecar records run metadata, dimensions, load simulation settings, scenario names, measured step names, observed step counts, NBomber version, target framework, result artifact paths, and threshold posture.

Quick evidence stays non-production smoke evidence. Release-calibration evidence additionally records hardware profile, C1/C2/C5 target comparisons, p50/p95/p99 step latency statistics, throughput, and commit-to-status-read freshness observations.

Reports and evidence are local artifacts. Do not commit generated report folders unless a later story intentionally adds a small deterministic fixture.

## Ownership Metadata

- owner_workstream: Capacity smoke CI and release-readiness calibration stories.
- future_test_use: Capacity smoke checks, C1/C2/C5 evidence, and release calibration artifacts.
- known_omissions: No production thresholds, provider scenarios, environments, or release-calibration artifacts are defined here.
- mutation_rules: Keep load assets hermetic, metadata-only, and free of provider credentials, network services, production secrets, or recursive submodule commands.
- non_policy_placeholder: false
- synthetic_data_only: true
