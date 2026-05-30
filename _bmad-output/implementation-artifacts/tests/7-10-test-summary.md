# Test Automation Summary

> Canonical latest-run summary for Story 7.10. Durable per-story copy: [`7-10-test-summary.md`](./7-10-test-summary.md).

**Workflow:** `bmad-qa-generate-e2e-tests`
**Story:** `_bmad-output/implementation-artifacts/7-10-calibrate-capacity-tests-and-pin-c1-c2-c5-targets.md`
**Feature under test:** C1/C2/C5 release capacity calibration, metadata-only calibration evidence, release-calibration load profile, capacity calibration gate, release-package prerequisite wiring, and governance/maintainer handoff.

## Generated Tests

### API Tests

- [x] Not applicable for Story 7.10; no new HTTP API endpoint surface is introduced by the capacity calibration story.

### E2E Tests

- [x] `tests/Hexalith.Folders.Contracts.Tests/Deployment/CapacityCalibrationConformanceTests.cs` - Static conformance tests for C1/C2/C5 exit criteria, governance evidence, release workflow wiring, calibration gate fail-closed behavior, generated latest report shape, metadata-only evidence, documentation handoff, and root-level submodule policy.
- [x] `tests/Hexalith.Folders.LoadTests.Tests/LifecycleCapacityHarnessTests.cs` - Load-harness tests for the `release-calibration` profile, target comparison evidence, latency/freshness/throughput statistics, stale C2 freshness failure, quick-profile preservation, partial execution signals, and unsupported profile rejection.
- [x] `tests/tools/run-capacity-calibration-gates.ps1` - End-to-end release calibration gate that runs the NBomber lifecycle profile, emits `_bmad-output/gates/capacity-calibration/latest.json`, validates same-commit C1/C2/C5 target evidence, and fails closed on missing, malformed, stale, unsafe, non-numeric, or partial evidence.

## Coverage

- C1/C2/C5 exit-criteria artifacts: 3/3 covered.
- Governance evidence rows: 3/3 covered for C1, C2, and C5 approved artifact paths and verification command.
- Required lifecycle steps: 5/5 covered (`prepare_workspace`, `acquire_workspace_lock`, `mutate_workspace_file`, `commit_workspace`, `read_workspace_status`).
- Calibration gate categories: 6/6 covered.
- Critical error cases: stale source commit, missing hardware profile, zero or partial step execution, missing C2 freshness observation, missing throughput rate, non-numeric target, threshold mismatch, malformed evidence JSON, unsafe diagnostics, absolute artifact paths, unsupported profile, and recursive submodule setup.
- UI E2E: not applicable; Story 7.10 is a release gate and load-harness workflow, not a browser workflow.

## Validation

- `dotnet build Hexalith.Folders.slnx --no-restore -m:1 -v:minimal` passed.
- `tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -class Hexalith.Folders.Contracts.Tests.Deployment.CapacityCalibrationConformanceTests -noLogo -noColor` passed: 8 total, 0 failed.
- `tests/Hexalith.Folders.LoadTests.Tests/bin/Debug/net10.0/Hexalith.Folders.LoadTests.Tests -class Hexalith.Folders.LoadTests.Tests.LifecycleCapacityHarnessTests -noLogo -noColor` passed: 10 total, 0 failed.
- `pwsh ./tests/tools/run-capacity-calibration-gates.ps1` passed and wrote `_bmad-output/gates/capacity-calibration/latest.json`.
- `git diff --check` passed.
- `rg -n -- "--recursive|git submodule update --init --recursive" .github tests/tools docs deploy src` returned no matches.
- `dotnet test tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-build --filter FullyQualifiedName~CapacityCalibrationConformanceTests --verbosity minimal` was attempted and blocked by `System.Net.Sockets.SocketException (13): Permission denied` during VSTest socket startup.
- `dotnet test tests/Hexalith.Folders.LoadTests.Tests/Hexalith.Folders.LoadTests.Tests.csproj --no-build --filter FullyQualifiedName~LifecycleCapacityHarnessTests --verbosity minimal` was attempted and blocked by `System.Net.Sockets.SocketException (13): Permission denied` during VSTest socket startup.

## Checklist Validation

- API tests generated if applicable: not applicable; no API endpoint surface was introduced.
- E2E tests generated if UI exists: browser UI is not applicable; workflow/gate/load-harness coverage exercises the implemented end-to-end release calibration behavior.
- Standard test framework APIs: passed; xUnit v3, Shouldly, YamlDotNet, `System.Text.Json`, NBomber, and the existing PowerShell gate-script pattern.
- Happy path: passed; approved artifacts, release-calibration profile, generated evidence, target comparisons, release workflow/package wiring, and documentation are covered.
- Critical error cases: passed; stale source commit, partial execution, missing/malformed evidence, missing freshness/throughput/hardware profile, non-numeric or failed target comparisons, unsafe diagnostics, unsupported profile, and recursive setup are guarded.
- Test quality: passed; tests use semantic YAML/JSON parsing where appropriate, clear descriptions, no hardcoded waits, no sleeps, and independent fixture state.
- Output: passed; summary created at the workflow default path and durable Story 7.10 path.
