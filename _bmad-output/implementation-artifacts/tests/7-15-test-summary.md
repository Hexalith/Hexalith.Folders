# Test Automation Summary

> Canonical latest-run summary for Story 7.15. Durable per-story copy: [`7-15-test-summary.md`](./7-15-test-summary.md).

**Workflow:** `bmad-qa-generate-e2e-tests`
**Story:** `_bmad-output/implementation-artifacts/7-15-publish-provider-and-error-documentation.md`
**Feature under test:** Provider integration/testing and canonical error documentation gates — provider port/capability model, credential-reference posture, GitHub/Forgejo behavior and capability differences, provider readiness/retryability, canonical error catalog, retry-after advisory behavior, and CI/gate wiring.

## Generated Tests

### API Tests

- [x] Not applicable as live endpoint tests; Story 7.15 publishes static provider/error documentation and gate wiring, not new runtime endpoints. Inventories are asserted against the provider abstractions, readiness query sources, Forgejo catalog, generated client enums, parity oracle, CLI exit codes, and MCP failure-kind projection.

### E2E Tests

- [x] `tests/Hexalith.Folders.Contracts.Tests/Deployment/ProviderErrorDocsConformanceTests.cs` - Static end-to-end conformance suite with 20 facts. The suite re-derives every inventory from authoritative source and asserts exact-cardinality equality against the published doc marker tables for capability operations, credential modes, readiness result codes, failure categories (and the retryable-by-default trio), Forgejo supported versions, generated canonical categories, parity-oracle-carried categories, client-action tokens, CLI exit codes, MCP failure-kind projection rules, and advisory-only retry-after fields. It also asserts metadata-only output, gate posture, `contract-spine.yml` ordering, `ci.yml` lane separation, baseline filter registration, and negative controls routed through the same scanners that guard real docs.
- [x] `tests/tools/run-provider-error-docs-gates.ps1` - End-to-end provider-error-docs release-readiness gate that runs the conformance suite, emits `_bmad-output/gates/provider-error-docs/latest.json`, enforces a non-vacuous floor equal to the 20-fact set, and fails closed on missing, malformed, vacuous, unsafe, or recursive-submodule evidence.

## Coverage

- Published doc surfaces: 2/2 operations docs (`provider-integration-and-testing`, `canonical-error-catalog`).
- Provider integration/testing: 10 capability operations, 4 reference-only `ProviderCredentialMode` members, 7 `ProviderReadinessResultCode` members, 15 `ProviderFailureCategory` members with the source-derived retryable-by-default trio (`ProviderUnavailable`, `ProviderRateLimited`, `ProviderTransientFailure`), 3 pinned Forgejo versions (`15.0.2`, `14.0.5`, `11.0.14`) with swagger snapshots and drift classification, the N-provider (not GitHub+Forgejo-hardcoded) capability model, GitHub (Octokit) and Forgejo (typed-HTTP) behavior, a source-backed capability-difference table that never claims false parity, and known-failure handling with explicit `unknown_provider_outcome` / `reconciliation_required` and no-silent-retry guarantees.
- Canonical error catalog: 47 generated `CanonicalErrorCategory` members, 43 distinct parity-oracle-carried categories (with the four deliberately-outside-oracle members `success`, `client_configuration_error`, `credential_missing`, `range_unsatisfiable`), 6 `ProblemDetailsClientAction` wire tokens, 14 CLI exit-code values, MCP failure-kind projection rules including the `range_unsatisfiable -> internal_error` fallback, and the advisory-only `retryAfter` fields (`AdvisoryOnly = true`, no mutation/repair/auto-unlock/implicit retry). Cross-links Story 7.13 SDK docs and Story 7.14 ops/audit docs without re-authoring them.
- CI wiring: `contract-spine.yml` gate step (after operations-audit-docs, `submodules: false`, `contents: read`), baseline Contracts.Tests filter registration, lane-separation guard for `ci.yml`, and root-level-only submodule posture.

## Validation

- `pwsh ./tests/tools/run-provider-error-docs-gates.ps1 -SkipRestoreBuild` passed: 20 total, 0 failed, exit 0; regenerated `_bmad-output/gates/provider-error-docs/latest.json` (`status: passed`).
- `dotnet test tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-build --filter FullyQualifiedName~Hexalith.Folders.Contracts.Tests.Deployment.ProviderErrorDocsConformanceTests` passed: 20 total, 0 failed.
- Baseline Contracts.Tests filter (`...ContractsSmokeTests|...BaselineCiWorkflowConformanceTests|...ReleasePackageConformanceTests|...RetentionAndTenantDeletionConformanceTests|...ProductionObservabilityConformanceTests|...ConsumerDocsConformanceTests|...OperationsAuditDocsConformanceTests|...ProviderErrorDocsConformanceTests`) executed 92 total, 0 failed — confirming the 20 new provider-error-docs facts register without dropping any prior facts.
- `git diff -- .github/workflows/contract-spine.yml tests/tools/run-baseline-ci-gates.ps1` shows a single appended gate step and a single appended filter FQN; no lanes broadened.
- Recursive-submodule scan passed; matches are limited to story/test text documenting the forbidden pattern, not executable setup.

## Checklist Validation

- API tests generated if applicable: not applicable for live endpoints; static provider/error conformance is source-backed against provider abstractions, readiness sources, the Forgejo catalog, generated client enums, the parity oracle, CLI exit codes, and the MCP failure-kind projection.
- E2E tests generated if UI exists: browser UI is not applicable; workflow/gate coverage exercises the implemented provider/error documentation release behavior.
- Standard test framework APIs: passed; xUnit v3, Shouldly, YamlDotNet, `System.Text.Json`, `GeneratedRegex`, and the existing PowerShell gate-script pattern.
- Happy path: passed; every required published surface and CI/gate hook is present and source-aligned.
- Critical error cases: passed; negative controls cover missing docs, stale/wrong inventories, leaked absolute paths, bearer/JWT material, non-placeholder hosts, raw-payload-with-host wording, malformed JSON/YAML, and recursive-submodule setup — each routed through the same scanner that guards real evidence.
- Test quality: passed; tests have clear descriptions, no sleeps, no order dependency, and derive inventories from source artifacts rather than hard-coded doc counts.
- Output: passed; summary created at the workflow default path and durable Story 7.15 path.
