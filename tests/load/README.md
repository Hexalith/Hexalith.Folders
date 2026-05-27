# Load Tests

This directory contains the hermetic lifecycle capacity harness for repository-backed workspace prepare, lock, mutate, and commit scenarios.

The harness uses synthetic tenant, folder, workspace, task, operation, correlation, and idempotency identifiers only. It does not require provider credentials, tenant seed data, Aspire, Dapr, Redis, GitHub, Forgejo, Docker, Testcontainers, network services, production secrets, local Git working copies, or nested submodule initialization.

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

Future CI capacity-smoke work can select the same scenario/profile shape with `--profile quick` and a CI-controlled `--report-folder`. Story 7.7 owns any GitHub Actions gate. Story 7.10 owns final p95, throughput, C1, C2, and C5 calibration.

## Evidence

Each run writes `lifecycle-capacity-evidence.json` in the report folder. The sidecar records run metadata, dimensions, load simulation settings, scenario names, NBomber version, target framework, result artifact paths, and `thresholds: "reference_pending"`.

Reports and evidence are local artifacts. Do not commit generated report folders unless a later story intentionally adds a small deterministic fixture.

## Ownership Metadata

- owner_workstream: Capacity smoke CI and release-readiness calibration stories.
- future_test_use: Capacity smoke checks, C1/C2/C5 evidence, and release calibration artifacts.
- known_omissions: No production thresholds, provider scenarios, environments, CI gates, or release-calibration artifacts are defined here.
- mutation_rules: Keep load assets hermetic, metadata-only, and free of provider credentials, network services, production secrets, or recursive submodule commands.
- non_policy_placeholder: false
- synthetic_data_only: true
