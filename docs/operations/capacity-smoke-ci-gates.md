# Capacity Smoke CI Gates

Story 7.7 adds the PR-blocking `capacity-smoke-gates` check. Branch protection can require that stable check name independently from `baseline-build-and-unit-gates`, `contract-and-parity-gates`, and `security-and-redaction-gates`.

## Local Commands

Run the same restore and build posture used by CI:

```bash
dotnet restore Hexalith.Folders.slnx -m:1 -p:NuGetAudit=false
dotnet build Hexalith.Folders.slnx --no-restore -m:1
pwsh ./tests/tools/run-capacity-smoke-ci-gates.ps1
```

The gate script runs the load harness self-check and the quick smoke profile:

```bash
dotnet run --project tests/load/Hexalith.Folders.LoadTests.csproj -- --self-check --profile quick --report-folder _bmad-output/gates/capacity-smoke-ci/self-check
dotnet run --project tests/load/Hexalith.Folders.LoadTests.csproj -- --profile quick --run-id capacity-smoke-ci --report-folder _bmad-output/gates/capacity-smoke-ci/reports
```

## Smoke Inventory

The quick profile is a hermetic PR smoke lane. It proves the synthetic lifecycle can execute these measured steps:

- `prepare_workspace`
- `acquire_workspace_lock`
- `mutate_workspace_file`
- `commit_workspace`
- `read_workspace_status`

The report path is `_bmad-output/gates/capacity-smoke-ci/latest.json`. The load evidence sidecar is `_bmad-output/gates/capacity-smoke-ci/reports/lifecycle-capacity-evidence.json`.

## Diagnostic Policy

Gate reports and failure summaries are metadata-only. They may include gate name, category names, repository-relative artifact paths, profile name, run id, measured step names, statuses, counts, elapsed duration, threshold posture, and exit codes.

They must not include raw file contents, diffs, provider payloads, token-shaped strings, credential material, tenant data beyond synthetic ordinal IDs, cache-key values, local absolute paths, release or production URLs, stack traces, environment dumps, or unauthorized-resource hints.

## Threshold Posture

The quick profile keeps `thresholds: "reference_pending"`. Passing `capacity-smoke-gates` means the hermetic quick lifecycle and status smoke did not obviously regress. It is not C1, C2, or C5 release evidence.

Story 7.10 owns final capacity calibration, target hardware profile, p95 and throughput thresholds, C1 target approval, C2 freshness target approval, and C5 target approval.

## Submodules

CI checkout uses `submodules: false`, then initializes only root-level build submodules:

```text
git submodule update --init Hexalith.AI.Tools Hexalith.Commons Hexalith.EventStore Hexalith.FrontComposer Hexalith.Memories Hexalith.Tenants
```

Nested recursive submodule initialization is forbidden unless explicitly requested for nested submodule work.
