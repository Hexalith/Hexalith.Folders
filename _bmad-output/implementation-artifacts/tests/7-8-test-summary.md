# Test Automation Summary

> Durable per-story copy for Story 7.8. Canonical latest-run summary: [`test-summary.md`](./test-summary.md).

**Workflow:** `bmad-qa-generate-e2e-tests`
**Story:** `_bmad-output/implementation-artifacts/7-8-wire-scheduled-drift-and-policy-conformance-workflows.md`
**Feature under test:** Scheduled nightly provider drift and policy-conformance workflows, focused gate scripts, metadata-only reports, live-evidence reference boundaries, and PR-lane separation.

## Generated Tests

### API Tests

- [x] Not applicable for Story 7.8; no API endpoint surface is introduced by the scheduled workflow story.

### E2E Tests

- [x] `tests/Hexalith.Folders.Contracts.Tests/Deployment/ScheduledDriftAndPolicyWorkflowConformanceTests.cs` - Workflow/gate conformance for `nightly-drift-gates` and `policy-conformance-gates`, schedule and manual dispatch wiring, root-only submodule policy, setup-dotnet posture, script category inventories, fail-closed guards, report metadata-only shape, documentation handoff, PR-lane separation, and recursive-submodule exclusion.
- [x] `tests/tools/run-nightly-drift-gates.ps1` - End-to-end scheduled drift gate execution across Forgejo manifest integrity, snapshot coverage, drift classification, sanitized report generation, and live provider drift reference-pending evidence.
- [x] `tests/tools/run-scheduled-policy-conformance-gates.ps1` - End-to-end scheduled policy gate execution across static Dapr policy shape, fixture provenance, negative triple coverage, mTLS/sidecar bindings, pub/sub topic scopes, and live kind/Dapr denial reference-pending evidence.

## Coverage

- Scheduled workflow files: 2/2 covered.
- Scheduled gate categories: 11/11 covered.
- Manual dispatch inputs: 2/2 covered with bounded choices and defaults.
- Metadata-only reports: 2/2 covered, including per-category result entries and repository-relative report paths.
- Critical error cases: missing workflow triggers, missing dispatch options/defaults, missing gate scripts, missing fixture inputs, missing test assemblies, zero/partial test selection guards, missing report categories/results, unsafe diagnostics, package/container/release lanes, secrets/write permissions, and recursive submodule setup.
- Excluded lanes: PR CI baseline, contract/parity, security/redaction, capacity-smoke, contract spine, safety, governance completeness, package publishing, container image build, release upload, semantic-release, broad artifact upload, Playwright installation, and recursive submodule initialization.

## Validation

- `dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore -m:1 -v minimal` passed.
- `tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -class Hexalith.Folders.Contracts.Tests.Deployment.ScheduledDriftAndPolicyWorkflowConformanceTests -noLogo -noColor` passed: 7 total, 0 failed.
- `pwsh ./tests/tools/run-nightly-drift-gates.ps1 -SkipRestoreBuild` passed; Forgejo drift fallback executed 7 tests with 0 failed and wrote the sanitized drift report.
- `pwsh ./tests/tools/run-scheduled-policy-conformance-gates.ps1 -SkipRestoreBuild` passed; Dapr policy fallback executed 8 tests with 0 failed.
- `git diff --check` passed.
- `rg -n -- "--recursive|git submodule update --init --recursive" .github tests/tools docs deploy src` returned no matches.
- `dotnet test tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --filter FullyQualifiedName~ScheduledDriftAndPolicyWorkflowConformanceTests -v normal` could not restore in the sandbox; MSBuild reported restore failed without detailed package errors. The xUnit v3 self-executable path above is the repository's existing sandbox fallback.

## Checklist Validation

- API tests generated if applicable: not applicable; Story 7.8 has no API endpoint surface.
- E2E tests generated if UI exists: browser UI is not applicable; workflow/gate conformance and gate-script execution cover the implemented end-to-end scheduled validation behavior.
- Standard test framework APIs: passed; xUnit v3, Shouldly, YamlDotNet, `System.Text.Json`, and the existing PowerShell gate-script pattern.
- Happy path: passed; both scheduled workflows, focused scripts, reports, documentation, and live-reference boundaries are covered.
- Critical error cases: passed; trigger drift, dispatch drift, missing inputs, partial test selection, unsafe diagnostics, forbidden lanes, and recursive setup are guarded.
- Test quality: passed; semantic YAML/report parsing where appropriate, no hardcoded waits, no sleeps, independent tests, and clear descriptions.
- Output: passed; summary created at the workflow default path and durable Story 7.8 path.
