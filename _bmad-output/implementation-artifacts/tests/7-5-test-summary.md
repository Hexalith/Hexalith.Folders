# Test Automation Summary

> Canonical latest-run summary for Story 7.5. Durable per-story copy: [`7-5-test-summary.md`](./7-5-test-summary.md).

**Workflow:** `bmad-qa-generate-e2e-tests`
**Story:** `_bmad-output/implementation-artifacts/7-5-consolidate-contract-and-parity-ci-gates.md`
**Feature under test:** Contract/parity PR CI workflow, focused gate script, cross-surface parity inventory, and metadata-only gate report contract.

## Generated Tests

### API Tests

- [x] `tests/Hexalith.Folders.Contracts.Tests/OpenApi/ContractSpineCiGateTests.cs` - Server-vs-spine and previous-spine contract drift coverage.
- [x] `tests/Hexalith.Folders.Contracts.Tests/OpenApi/ParityOracleGeneratorTests.cs` - Parity oracle schema, operation inventory, deterministic output, and fail-closed prerequisite coverage.
- [x] `tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs` - Generated client, stale output, helper generation, and idempotency helper coverage.

### E2E Tests

- [x] `tests/Hexalith.Folders.Contracts.Tests/Deployment/ContractParityCiWorkflowConformanceTests.cs` - Workflow-level CI conformance for the stable `contract-and-parity-gates` job, setup posture, root-only submodule policy, exact filter inventory, report shape, and out-of-scope lane exclusions.
- [x] `tests/Hexalith.Folders.Client.Tests/TransportParityConformanceTests.cs` - SDK/REST transport parity against `tests/fixtures/parity-contract.yaml`.
- [x] `tests/Hexalith.Folders.Cli.Tests/ParityOracleConformanceTests.cs` and `tests/Hexalith.Folders.Cli.Tests/BehavioralParityTests.cs` - CLI oracle-driven behavioral parity and error/exit-code mapping.
- [x] `tests/Hexalith.Folders.Mcp.Tests/ParityOracleConformanceTests.cs`, `PreSdkFailureTests.cs`, `PostSdkMappingTests.cs`, `SourcingTests.cs`, and `FailureKindProjectionTests.cs` - MCP behavioral parity and failure-kind mapping.
- [x] `tests/Hexalith.Folders.IntegrationTests/EndToEnd/GoldenLifecycleParityTests.cs`, `AdapterParity/CrossAdapterBehavioralParityTests.cs`, and `MixedSurfaceHandoff/MixedSurfaceHandoffTests.cs` - Hermetic REST/SDK golden lifecycle and mixed-surface handoff parity.

## Coverage

- Contract/parity workflow file: 1/1 covered.
- Contract/parity gate categories: 11/11 covered (`server-vs-spine`, `previous-spine`, `generated-client`, `idempotency-helpers`, `parity-oracle-schema`, `parity-oracle-determinism`, `sdk-transport-parity`, `rest-sdk-golden-parity`, `cli-behavioral-parity`, `mcp-behavioral-parity`, `mixed-surface-handoff`).
- Test project allow-list: 5/5 covered exactly.
- Cross-surface parity lanes: REST, SDK, CLI, MCP, and mixed-surface workflows covered.
- Excluded lanes: safety/redaction, Dapr policy, governance, container image, capacity, scheduled drift, package publishing, and release artifact upload guarded by conformance assertions.
- Metadata-only report contract: covered by `_bmad-output/gates/contract-parity-ci/latest.json`.

## Validation

- `pwsh tests/tools/run-contract-parity-ci-gates.ps1` passed. VSTest socket setup is denied in this sandbox, and the script's xUnit in-process fallback passed all 11 categories with exit code 0.
- `tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -noLogo -noColor -class Hexalith.Folders.Contracts.Tests.Deployment.ContractParityCiWorkflowConformanceTests` passed: 5 total, 0 failed.
- `_bmad-output/gates/contract-parity-ci/latest.json` reports `status: passed` and all 11 categories passed.
- `git diff --check` passed.
- `rg -n "git submodule update --init --recursive|--recursive" .github tests/tools docs deploy src` returned no matches.

## Checklist Validation

- API tests generated if applicable: passed; contract, server-vs-spine, generated-client, idempotency-helper, and parity-oracle API contract tests are covered.
- E2E tests generated if UI exists: browser UI is not applicable for Story 7.5; workflow/gate and cross-surface parity tests cover the implemented end-to-end CI behavior.
- Standard test framework APIs: passed; xUnit v3, Shouldly, YamlDotNet, and `System.Text.Json`.
- Happy path: passed; workflow wiring, gate execution, report emission, and REST/SDK/CLI/MCP/mixed-surface parity are covered.
- Critical error cases: passed; missing/stale prerequisites, malformed parity schema, operation-count drift, recursive submodule setup, and out-of-scope lane inclusion are guarded.
- Test quality: passed; no hardcoded waits, no sleeps, independent tests, clear descriptions, and semantic YAML/report parsing where appropriate.
- Output: passed; summary created at the workflow default path and durable Story 7.5 path.
