# Test Automation Summary

> Canonical latest-run summary for Story 7.11. Durable per-story copy: [`7-11-test-summary.md`](./7-11-test-summary.md).

**Workflow:** `bmad-qa-generate-e2e-tests`
**Story:** `_bmad-output/implementation-artifacts/7-11-enforce-c3-retention-and-tenant-deletion-behavior.md`
**Feature under test:** C3 retention policy evidence, tenant-deletion disposition matrix, retention/deletion gate evidence, release-package prerequisite wiring, metadata-only diagnostics, and root-level submodule policy.

## Generated Tests

### API Tests

- [x] Not applicable for Story 7.11; no new HTTP API endpoint surface is introduced by the retention/deletion governance story.

### E2E Tests

- [x] `tests/Hexalith.Folders.Contracts.Tests/Deployment/RetentionAndTenantDeletionConformanceTests.cs` - Static end-to-end conformance tests for C3 policy source rows, tenant-deletion runbook classifications, governance evidence, retention/deletion gate script posture, generated latest report shape, release-readiness wiring, metadata-only diagnostics, negative controls, and baseline CI execution wiring.
- [x] `tests/tools/run-retention-deletion-gates.ps1` - End-to-end retention/deletion gate that validates C3 and tenant-deletion artifacts, emits `_bmad-output/gates/retention-deletion/latest.json`, and fails closed on missing, malformed, unsafe, stale, approval-blocked, or recursive-submodule evidence.
- [x] `tests/tools/run-baseline-ci-gates.ps1` - Updated to run `RetentionAndTenantDeletionConformanceTests` so the Story 7.11 conformance suite is not inert in PR baseline automation.

## Coverage

- Required C3 classes: 6/6 covered (`Audit metadata`, `Workspace status`, `Provider correlation IDs`, `Read-model views`, `Temporary working files`, `Cleanup records`).
- Tenant-deletion dispositions: 4/4 covered (`deleted`, `tombstoned`, `retained`, `anonymized`).
- Release evidence paths: retention/deletion latest report, governance evidence, release workflow, release package gate, and package manifest covered.
- Critical error cases: missing C3 class, pending approval, missing disposition, stale source commit, unsafe diagnostic content, absolute path evidence, malformed Markdown/JSON, and recursive submodule setup.
- UI E2E: not applicable; Story 7.11 is a release/governance gate workflow, not a browser workflow.

## Validation

- `pwsh ./tests/tools/run-retention-deletion-gates.ps1` passed with expected `status=release-blocked policy_status=reference_pending`.
- `dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore -m:1 --verbosity minimal` passed.
- `tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -class Hexalith.Folders.Contracts.Tests.Deployment.RetentionAndTenantDeletionConformanceTests -noLogo -noColor` passed: 8 total, 0 failed.
- `tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -class Hexalith.Folders.Contracts.Tests.Deployment.ReleasePackageConformanceTests -noLogo -noColor` passed: 8 total, 0 failed.
- `pwsh ./tests/tools/run-release-package-gates.ps1 -Version 0.0.0-local.1 -SourceRevisionId 3b9fa9fd6fac25acc3119a81c96b15cce635fd20 -SkipRestoreBuild` passed in dry-run mode.
- `git diff --check` passed.
- `rg -n -- "--recursive|git submodule update --init --recursive" .github tests/tools docs deploy src` returned no matches.
- `dotnet test tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --filter FullyQualifiedName~RetentionAndTenantDeletionConformanceTests --verbosity minimal` failed before diagnostics during restore.
- `dotnet test tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore --filter FullyQualifiedName~RetentionAndTenantDeletionConformanceTests --verbosity normal` failed through the VSTest entry point with 0 warnings and 0 errors. The xUnit v3 in-process runner above is the repository's existing sandbox fallback and passed.

## Checklist Validation

- API tests generated if applicable: not applicable; no API endpoint surface was introduced.
- E2E tests generated if UI exists: browser UI is not applicable; workflow/gate coverage exercises the implemented end-to-end release retention/deletion behavior.
- Standard test framework APIs: passed; xUnit v3, Shouldly, YamlDotNet, `System.Text.Json`, and the existing PowerShell gate-script pattern.
- Happy path: passed; C3 artifacts, runbook matrix, governance evidence, release wiring, generated evidence, and dry-run package gate are covered.
- Critical error cases: passed; missing classes/dispositions, pending approval, stale commit, unsafe diagnostics, absolute paths, malformed evidence, and recursive setup are guarded.
- Test quality: passed; tests use semantic YAML/JSON/Markdown parsing where appropriate, clear descriptions, no hardcoded waits, no sleeps, and independent fixture state.
- Output: passed; summary created at the workflow default path and durable Story 7.11 path.
