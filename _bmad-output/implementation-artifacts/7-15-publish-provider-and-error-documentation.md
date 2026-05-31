---
baseline_commit: 5637857
discovered_inputs:
  sprint_status: _bmad-output/implementation-artifacts/sprint-status.yaml
  epics: _bmad-output/planning-artifacts/epics.md
  prd: _bmad-output/planning-artifacts/prd.md
  architecture: _bmad-output/planning-artifacts/architecture.md
  previous_story: _bmad-output/implementation-artifacts/7-14-publish-operations-and-audit-documentation.md
  authoritative_sources:
    - src/Hexalith.Folders/Providers/Abstractions/*.cs
    - src/Hexalith.Folders/Providers/GitHub/*.cs
    - src/Hexalith.Folders/Providers/Forgejo/*.cs
    - src/Hexalith.Folders/Queries/ProviderReadiness/*.cs
    - src/Hexalith.Folders.Server/FolderCanonicalErrorMapper.cs
    - src/Hexalith.Folders.Cli/FoldersExitCodes.cs
    - src/Hexalith.Folders.Cli/Errors/ErrorProjection.cs
    - src/Hexalith.Folders.Mcp/Errors/FailureKindProjection.cs
    - src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs
    - tests/fixtures/parity-contract.yaml
    - tests/contracts/forgejo/*/swagger.v1.json
---

# Story 7.15: Publish provider and error documentation

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an operator and integration maintainer,
I want provider integration, retryability, and canonical error documentation published,
so that provider failures and client actions are diagnosable without reading implementation code.

## Acceptance Criteria

Epic 7.15 BDD from `_bmad-output/planning-artifacts/epics.md`:

Given provider contracts and canonical error taxonomy exist
When provider and error documentation is published
Then provider integration/testing, supported versions, drift handling, error catalog, retryability, retry-after behavior, and client action guidance are documented
And GitHub and Forgejo capability differences are explicit.

Decomposed acceptance criteria:

1. Publish a provider integration and contract-testing guide, recommended `docs/operations/provider-integration-and-testing.md`. Document the provider port and capability model from `IGitProvider`, `ProviderCapabilityProfile*`, `ProviderOperationCatalog`, `ProviderOperationCapability`, `ProviderOperationSupport`, `ProviderCredentialMode`, `ProviderRateLimitPosture`, and the readiness query sources. The doc must state that provider capability discovery is N-provider capable, not hardcoded to GitHub plus Forgejo.
2. Document credential-reference behavior as metadata-only: provider credentials are references/resolved through Dapr-backed resolvers and secret-store clients, never stored in Folders state and never published in examples. Pin the 4 `ProviderCredentialMode` members and the safe evidence fields (`ProviderIdentityIdentifier`, `ProviderTargetEvidence`, permission/rate-limit/version evidence) without exposing tokens, repository URLs with embedded credentials, or secret material.
3. Document GitHub integration behavior and capability evidence. Use the real `src/Hexalith.Folders/Providers/GitHub/*` adapter sources, `OctokitGitHubApiClient`, `GitHubFailureMapper`, `GitHubReadinessMapper`, credential resolver/validator, permission/rate-limit evidence, and GitHub provider tests. State the Octokit dependency is the implementation detail from architecture, and document how GitHub provider failures map to stable provider categories rather than raw payloads.
4. Document Forgejo integration behavior, supported versions, and drift handling. Pin the 3 supported Forgejo version entries from `ForgejoSupportedVersionCatalog` and the checked-in `tests/contracts/forgejo/<version>/swagger.v1.json` snapshots. Document typed-HTTP adapter behavior, credential mode validation, `ForgejoFailureMapper`, `ForgejoReadinessMapper`, schema drift classification, and that unsupported/failing provider versions cannot report ready.
5. Document GitHub versus Forgejo capability differences explicitly. The guide must include a source-backed comparison table for supported operations, branch/ref behavior, file limits, credential mode, version/capability metadata, rate-limit posture, readiness behavior, repository create/bind behavior, file/commit/status behavior, unknown outcome handling, and drift evidence. It must not claim parity where the source only records provider-specific evidence.
6. Document provider readiness, retryability, and remediation categories. Pin the 7 `ProviderReadinessResultCode` members and the 15 `ProviderFailureCategory` members, including `ProviderFailureCategoryExtensions.ToCategoryCode()` and `IsRetryableByDefault()`. Document projection stale/unavailable/read-model unavailable handling and the domain services that translate readiness failures into `FolderResultCode` values.
7. Document known provider failure handling: timeout, 401/403, 404/missing repository, 409/repository conflict, 429/rate limit, 5xx/unavailable, branch protection, stale clone, credential revocation, provider drift, unknown outcome, and reconciliation-required. State that unknown or ambiguous provider outcomes enter `unknown_provider_outcome` / `reconciliation_required`; the system must not silently retry if doing so could duplicate repositories, file changes, or commits.
8. Publish a canonical error catalog, recommended `docs/operations/canonical-error-catalog.md`. Document the generated `CanonicalErrorCategory` vocabulary, REST Problem Details fields, HTTP status mapping, retryability, `retryAfter` / retry-eligibility behavior, `ProblemDetailsClientAction` guidance, CLI exit-code behavior, SDK error/result behavior, MCP failure-kind behavior, and audit/logging expectations. Reference Story 7.13 consumer docs, do not duplicate the full SDK/CLI/MCP manuals.
9. The canonical error doc must be source-backed by `tests/fixtures/parity-contract.yaml`, `FolderCanonicalErrorMapper`, `FoldersExitCodes`, `ErrorProjection`, `FailureKindProjection`, generated `CanonicalErrorCategory`, and existing parity tests. Pin the key inventories: generated `CanonicalErrorCategory` has 47 members; parity-oracle outcome mappings currently carry 43 distinct canonical categories; `ProblemDetailsClientAction` has 6 wire tokens; CLI canonical exit table has 14 values; MCP projects oracle categories verbatim except pre-SDK `usage_error` / `credential_missing` and the documented `range_unsatisfiable -> internal_error` fallback.
10. Document retry-after behavior honestly. `WorkspaceStatusRetryAfter` and `WorkspaceStatusRetryEligibility` are advisory-only (`AdvisoryOnly = true`), bounded, metadata-only signals; they never trigger mutation, repair, auto-unlock, or implicit retry. Provider rate-limit retry hints are preserved where available, but live alerting/runbook procedures remain outside this story.
11. Add a focused static conformance test at `tests/Hexalith.Folders.Contracts.Tests/Deployment/ProviderErrorDocsConformanceTests.cs` using xUnit v3 + Shouldly + YamlDotNet + `System.Text.Json` and a `RepositoryPath` walker. The suite must assert exact-cardinality inventory equality against source files and the generated client/openapi/parity oracle, verify docs are metadata-only, verify no recursive submodule setup is present, and include negative controls routed through the same scanners used for real docs/reports.
12. Add `tests/tools/run-provider-error-docs-gates.ps1` and `_bmad-output/gates/provider-error-docs/latest.json`, mirroring the 7.13/7.14 gate posture: `#Requires -Version 7`, `Set-StrictMode -Version Latest`, `$ErrorActionPreference = 'Stop'`, non-vacuous `$runnerMethods` guard equal to the `[Fact]` set, VSTest socket fallback to the xUnit v3 in-process runner, `utf8NoBOM`, `diagnostic_policy: metadata-only`, bounded `surfaces[]`, and fail-closed report writes.
13. Wire CI without broadening lanes: add the provider-error-docs gate step to `.github/workflows/contract-spine.yml` after the operations-audit-docs step, append `ProviderErrorDocsConformanceTests` to the Contracts.Tests filter in `tests/tools/run-baseline-ci-gates.ps1`, and assert `ci.yml`, release, scheduled, and policy workflows do not run the new focused gate. Keep `submodules: false` and do not introduce nested submodule initialization.
14. Keep all new docs, examples, gate output, and test diagnostics metadata-only. Use opaque synthetic identifiers and placeholder hosts only. Do not include file contents, diffs, provider tokens, credential material, secrets, raw provider payload snapshots, embedded credential URLs, production hosts, stack traces, tenant data, or unauthorized resource details.

## Tasks / Subtasks

- [x] Publish provider integration/testing documentation (AC: 1-7, 14)
  - [x] Author `docs/operations/provider-integration-and-testing.md`.
  - [x] Document provider port/capability model, credential-reference posture, GitHub behavior, Forgejo behavior, supported Forgejo versions, drift handling, GitHub-vs-Forgejo capability differences, provider readiness, retryability, remediation categories, and unknown-outcome/reconciliation behavior.
  - [x] Add marker tables for capability operations, credential modes, provider readiness result codes, provider failure categories, supported Forgejo versions, and GitHub/Forgejo capability differences.
  - [x] Add the standard operator boilerplate: gate-run command, metadata-only policy, reviewer/rerun note, and the exact root-level submodule command.

- [x] Publish canonical error catalog documentation (AC: 8-10, 14)
  - [x] Author `docs/operations/canonical-error-catalog.md`.
  - [x] Document canonical category vocabulary, parity oracle outcome mappings, REST Problem Details shape and HTTP status mapping, retryability, retry-after/advisory-only behavior, client action tokens, CLI exit behavior, SDK error/result behavior, MCP failure-kind behavior, and audit/logging expectations.
  - [x] Add marker tables for generated canonical categories, oracle-carried categories, client-action tokens, CLI exit codes, MCP failure-kind projection rules, and retry-after/advisory fields.
  - [x] Cross-link Story 7.13 docs (`docs/sdk/*`) and Story 7.14 ops/audit docs without re-authoring them.

- [x] Add provider-error-docs conformance test (AC: 11, 14)
  - [x] Add `tests/Hexalith.Folders.Contracts.Tests/Deployment/ProviderErrorDocsConformanceTests.cs` as a `public sealed partial class` for `[GeneratedRegex]`.
  - [x] Parse authoritative sources and assert exact inventory equality: provider credential modes (4), readiness result codes (7), provider failure categories (15), Forgejo supported versions (3), generated canonical categories (47), parity oracle distinct categories (43), client actions (6), CLI exit codes (14), and MCP projection behavior including the `range_unsatisfiable -> internal_error` fallback.
  - [x] Assert docs and `latest.json` are metadata-only through shared scanners, and add negative controls for leaked paths, bearer/JWT-like token material, non-placeholder hosts, raw provider payload wording, malformed JSON/YAML, stale inventories, and forbidden recursive submodule commands.
  - [x] Add self-wiring facts for the PowerShell gate, `contract-spine.yml`, `ci.yml` lane separation, and `run-baseline-ci-gates.ps1` filter registration.

- [x] Add focused gate and CI wiring (AC: 12-13)
  - [x] Add `tests/tools/run-provider-error-docs-gates.ps1` with the same non-vacuous, fail-closed, metadata-only posture as `run-consumer-docs-gates.ps1` and `run-operations-audit-docs-gates.ps1`.
  - [x] Generate `_bmad-output/gates/provider-error-docs/latest.json`.
  - [x] Add the gate step to `.github/workflows/contract-spine.yml` after operations-audit-docs.
  - [x] Append the conformance class FQN to the Contracts.Tests baseline filter.
  - [x] Confirm no `ci.yml`, release, scheduled, or policy workflow lane is broadened.

- [x] Verification (AC: all)
  - [x] Run `dotnet restore Hexalith.Folders.slnx -m:1 -p:NuGetAudit=false` if needed.
  - [x] Run `dotnet build Hexalith.Folders.slnx --no-restore -m:1`.
  - [x] Run the focused `ProviderErrorDocsConformanceTests`.
  - [x] Run `pwsh ./tests/tools/run-provider-error-docs-gates.ps1 -SkipRestoreBuild`.
  - [x] Run `pwsh ./tests/tools/run-safety-invariant-gates.ps1 -SkipRestoreBuild`.
  - [x] Run `pwsh ./tests/tools/run-baseline-ci-gates.ps1 -SkipRestoreBuild` and confirm the new conformance facts execute.
  - [x] Run `git diff --check` and a recursive-submodule scan over executable/docs surfaces.

## Dev Notes

### Scope Boundaries

- This story is documentation plus static conformance wiring. It must not change runtime provider behavior, generated clients, OpenAPI contract source, provider adapters, canonical mapper semantics, parity oracle fixtures, Forgejo snapshots, or live drift workflows unless a conformance failure proves an existing doc/gate hook is stale and directly in scope.
- Do not author the NFR traceability bridge (`docs/exit-criteria/nfr-traceability.md`, Story 7.16) or ADR/runbook procedures (`docs/adrs/*`, `docs/runbooks/*`, Story 7.17). Provider drift runbooks and alert/rollback procedures are 7.17; this story documents integration/testing evidence and diagnostic interpretation.
- Do not duplicate the Story 7.13 API/SDK/CLI/MCP manuals. The error catalog should summarize cross-surface behavior and link to `docs/sdk/api-reference.md`, `docs/sdk/cli-reference.md`, `docs/sdk/mcp-reference.md`, and `docs/sdk/authentication.md`.
- Keep every example metadata-only. Use synthetic IDs and placeholder hosts. Do not include real repository URLs, tokens, raw provider responses, stack traces, or tenant-specific data.

### Implementation Pattern

- Mirror Story 7.14: two focused docs under `docs/operations/`, one `Deployment/*ConformanceTests.cs`, one focused PowerShell gate, one `contract-spine.yml` step, one baseline filter append, one metadata-only `latest.json`.
- Recommended gate identity: `provider-error-docs`; script `tests/tools/run-provider-error-docs-gates.ps1`; report `_bmad-output/gates/provider-error-docs/latest.json`; conformance class `ProviderErrorDocsConformanceTests`.
- The conformance test should derive inventories from source, not hard-coded doc counts. Marker tables in the docs are acceptable because the test asserts table values equal parsed source values exactly.
- If parsing generated `CanonicalErrorCategory`, read `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs` but do not modify generated files. The generated enum has 47 members.
- Use `tests/fixtures/parity-contract.yaml` as the source of truth for oracle-carried canonical categories, CLI exit projections, and MCP failure-kind projections. The oracle currently carries 43 distinct canonical categories; some generated categories are intentionally outside the oracle path (`success`, pre-SDK/configuration behavior, and `range_unsatisfiable` fallback).
- Preserve the `range_unsatisfiable -> internal_error` documentation note from `docs/sdk/mcp-reference.md` and adapter parity tests; do not make MCP claim a direct failure kind for it.

### Project Structure Notes

- New docs belong under `docs/operations/` to match the Epic 7 release-readiness documentation cluster.
- Conformance tests belong under `tests/Hexalith.Folders.Contracts.Tests/Deployment/`.
- Gate scripts belong under `tests/tools/`.
- Gate reports belong under `_bmad-output/gates/provider-error-docs/latest.json`.
- No new NuGet packages or inline package versions should be needed.

### Key Source Facts To Preserve

- `ProviderCredentialMode` has 4 members and is reference-only.
- `ProviderReadinessResultCode` has 7 members.
- `ProviderFailureCategory` has 15 members. `IsRetryableByDefault()` returns true for provider unavailable, provider rate-limited, and provider transient failure.
- Forgejo supported versions are exactly 3 in `ForgejoSupportedVersionCatalog`: `15.0.2`, `14.0.5`, and `11.0.14`.
- Generated `CanonicalErrorCategory` has 47 members.
- `tests/fixtures/parity-contract.yaml` currently has 43 distinct oracle-carried canonical categories.
- `ProblemDetailsClientAction` has 6 tokens: `retry`, `revise_request`, `check_credentials`, `wait_for_reconciliation`, `contact_operator`, `no_action`.
- CLI exit codes are projected by `FoldersExitCodes` / `ErrorProjection`; the canonical table has 14 values including success `0`, client/credential/config classes, retry/reconciliation classes, redacted `75`, and internal fallback `1`.
- MCP failure kinds are projected by `FailureKindProjection`: oracle categories project verbatim, pre-SDK `usage_error` and `credential_missing` are explicit, and `range_unsatisfiable` falls back to `internal_error`.
- `WorkspaceStatusRetryAfter` and `WorkspaceStatusRetryEligibility` use `AdvisoryOnly = true`; retry hints do not imply mutation or automatic repair.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-7.15`] - Story statement and BDD acceptance criteria; Epic 7 documentation cluster boundaries.
- [Source: `_bmad-output/planning-artifacts/epics.md#FR15-FR23`] - Provider binding/readiness/capability requirements.
- [Source: `_bmad-output/planning-artifacts/epics.md#FR43-FR46`] - Canonical error taxonomy, retryability, and failure evidence requirements.
- [Source: `_bmad-output/planning-artifacts/architecture.md#AR-PROVIDER-01`] - Provider port and N-provider capability model.
- [Source: `_bmad-output/planning-artifacts/architecture.md#AR-PROVIDER-02`] - GitHub adapter and Octokit implementation note.
- [Source: `_bmad-output/planning-artifacts/architecture.md#AR-PROVIDER-03`] - Forgejo typed HTTP adapter and pinned snapshots.
- [Source: `_bmad-output/planning-artifacts/architecture.md#AR-PROVIDER-05`] - Known vs unknown provider failure handling and no silent retry.
- [Source: `_bmad-output/planning-artifacts/architecture.md#AR-DOC-03`] - Provider integration and provider contract testing guide deliverable.
- [Source: `_bmad-output/planning-artifacts/architecture.md#AR-DOC-04`] - Error catalog deliverable.
- [Source: `src/Hexalith.Folders/Providers/Abstractions/IGitProvider.cs`] - Provider port.
- [Source: `src/Hexalith.Folders/Providers/Abstractions/ProviderCapability*.cs`] - Capability discovery/profile/evidence model.
- [Source: `src/Hexalith.Folders/Providers/Abstractions/ProviderCredentialMode.cs`] - Credential mode inventory.
- [Source: `src/Hexalith.Folders/Providers/Abstractions/ProviderFailureCategory.cs`] - Provider failure category inventory.
- [Source: `src/Hexalith.Folders/Providers/Abstractions/ProviderFailureCategoryExtensions.cs`] - Category code and default retryability.
- [Source: `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderReadinessResultCode.cs`] - Readiness result inventory.
- [Source: `src/Hexalith.Folders/Providers/GitHub/*`] - GitHub adapter, credential resolver, failure/readiness mapping, permission and rate-limit evidence.
- [Source: `src/Hexalith.Folders/Providers/Forgejo/*`] - Forgejo adapter, version evidence, credential resolver, failure/readiness mapping, and supported version catalog.
- [Source: `tests/contracts/forgejo/*/swagger.v1.json`] - Pinned Forgejo API snapshots.
- [Source: `tests/Hexalith.Folders.Tests/Providers/*`] - Provider capability, GitHub, Forgejo, readiness, and drift behavior tests.
- [Source: `src/Hexalith.Folders.Server/FolderCanonicalErrorMapper.cs`] - Result-code to canonical category/status/client-action mapping.
- [Source: `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs`] - Generated `CanonicalErrorCategory` and `ProblemDetailsClientAction` wire enums.
- [Source: `tests/fixtures/parity-contract.yaml`] - Canonical parity oracle category/CLI/MCP outcome mapping.
- [Source: `src/Hexalith.Folders.Cli/FoldersExitCodes.cs`, `src/Hexalith.Folders.Cli/Errors/ErrorProjection.cs`] - CLI exit behavior.
- [Source: `src/Hexalith.Folders.Mcp/Errors/FailureKindProjection.cs`] - MCP failure-kind behavior.
- [Source: `tests/Hexalith.Folders.IntegrationTests/AdapterParity/CrossAdapterBehavioralParityTests.cs`] - Cross-surface canonical error parity and fallback assertions.
- [Source: `docs/sdk/api-reference.md`, `docs/sdk/cli-reference.md`, `docs/sdk/mcp-reference.md`] - Existing consumer references to cross-link, not duplicate.
- [Source: `_bmad-output/implementation-artifacts/7-13-publish-api-sdk-cli-and-mcp-consumer-references.md`] - Consumer-docs gate pattern.
- [Source: `_bmad-output/implementation-artifacts/7-14-publish-operations-and-audit-documentation.md`] - Operations/audit-docs gate pattern and review learning about scanner-backed negative controls.

## Dev Agent Record

### Agent Model Used

Claude Opus 4.8 create-story fallback was terminated after a stalled background workflow; parent story automator completed the story context from the child session's primary-source findings and local source checks.

### Debug Log References

- 2026-05-31: Create-story child session identified the correct backlog story, loaded the 7.13/7.14 doc-gate pattern, and independently verified key source inventories: `ProviderFailureCategory` 15, `ProviderCredentialMode` 4, `ProviderReadinessResultCode` 7, Forgejo supported versions 3, generated `CanonicalErrorCategory` 47, parity-oracle distinct categories 43, client-action tokens 6, CLI exit table 14, MCP projection behavior, and advisory-only retry-after sources. The child then stalled waiting for one background analysis agent at 3/4; parent terminated the session and wrote this story file directly to keep orchestration moving.

### Completion Notes List

- Published `docs/operations/provider-integration-and-testing.md` (AC 1-7, 14): provider port/capability model (N-provider, not hardcoded to GitHub+Forgejo), metadata-only credential references, GitHub (Octokit) and Forgejo (typed-HTTP) behavior, the three pinned Forgejo versions with swagger snapshots and drift classification, a source-backed GitHub-vs-Forgejo difference table that never claims false parity, the readiness/failure-category/retryability model, and known-failure handling with explicit no-silent-retry on unknown/ambiguous outcomes. Marker tables: capability operations (10), credential modes (4), readiness result codes (7), failure categories (15), supported Forgejo versions (3), and the capability-difference table. Operator boilerplate (gate command, metadata-only policy, reviewer/rerun note, exact root-level submodule command) included.
- Published `docs/operations/canonical-error-catalog.md` (AC 8-10, 14): generated category vocabulary, oracle outcome mappings, RFC 9457 Problem Details shape and HTTP status mapping, retryability, advisory-only `retryAfter`, client-action tokens, CLI exit behavior, SDK error/result behavior, MCP failure-kind behavior, and audit/logging expectations. Marker tables: generated canonical categories (47), oracle-carried categories (43), client-action tokens (6), CLI exit codes (14), MCP projection rules (incl. `range_unsatisfiable -> internal_error`), and retry-after/advisory fields. Cross-links Story 7.13 SDK docs and Story 7.14 ops/audit docs without re-authoring them.
- Added `tests/Hexalith.Folders.Contracts.Tests/Deployment/ProviderErrorDocsConformanceTests.cs` (AC 11, 14): a `public sealed partial class` with 20 `[Fact]` facts that re-derive every inventory from source (provider abstractions, readiness enum, Forgejo catalog, generated client enums, parity oracle via YamlDotNet, `FoldersExitCodes`, `FailureKindProjection`, advisory retry records) and assert exact-cardinality equality against the doc marker tables. Docs and `latest.json` route through the shared metadata-only scanners; negative controls cover missing docs, wrong/stale inventories, leaked absolute paths, bearer/JWT material, non-placeholder hosts, raw-payload-with-host wording, malformed JSON and YAML, and forbidden recursive submodule commands. Self-wiring facts assert the gate script posture, `contract-spine.yml` ordering, `ci.yml` lane separation, and the baseline filter registration.
- Added `tests/tools/run-provider-error-docs-gates.ps1` and `_bmad-output/gates/provider-error-docs/latest.json` (AC 12) mirroring the 7.13/7.14 posture: `#Requires -Version 7`, `Set-StrictMode`, `$ErrorActionPreference = 'Stop'`, non-vacuous `$runnerMethods` guard equal to the 20-fact set, VSTest socket fallback to the xUnit v3 in-process runner, `utf8NoBOM`, `diagnostic_policy: metadata-only`, bounded `surfaces[]`, and fail-closed report writes.
- Wired CI without broadening lanes (AC 13): added the provider-error-docs step to `.github/workflows/contract-spine.yml` after operations-audit-docs (`submodules: false`, `contents: read`) and appended `ProviderErrorDocsConformanceTests` to the Contracts.Tests baseline filter; `ci.yml`/release/scheduled/policy lanes untouched.
- Validation evidence (all from repo root): `dotnet build Hexalith.Folders.slnx --no-restore -m:1` succeeded (0 warnings, 0 errors); focused `ProviderErrorDocsConformanceTests` 20/20 passed; `run-provider-error-docs-gates.ps1 -SkipRestoreBuild` exit 0 (`status: passed`); `run-safety-invariant-gates.ps1 -SkipRestoreBuild` 11/11 passed; `run-baseline-ci-gates.ps1 -SkipRestoreBuild` exit 0 with Contracts.Tests now executing 92 facts (the new 20 registered) and format/lint clean; `git diff --check` clean; recursive-submodule scan over docs/gate/test/workflow surfaces found none.

### File List

- `_bmad-output/implementation-artifacts/7-15-publish-provider-and-error-documentation.md`
- `docs/operations/provider-integration-and-testing.md`
- `docs/operations/canonical-error-catalog.md`
- `tests/Hexalith.Folders.Contracts.Tests/Deployment/ProviderErrorDocsConformanceTests.cs`
- `tests/tools/run-provider-error-docs-gates.ps1`
- `_bmad-output/gates/provider-error-docs/latest.json`
- `.github/workflows/contract-spine.yml`
- `tests/tools/run-baseline-ci-gates.ps1`
- `_bmad-output/gates/baseline-ci/latest.json` (modified - baseline Contracts.Tests filter re-run now includes `ProviderErrorDocsConformanceTests`)
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/tests/7-15-test-summary.md` (new - durable QA automation summary)
- `_bmad-output/implementation-artifacts/tests/test-summary.md` (modified - latest QA automation summary points to Story 7.15)

### QA Automation Results

**Workflow:** `bmad-qa-generate-e2e-tests` (QA automation / guardrail verification)
**Date:** 2026-05-31
**Outcome:** Coverage verified intact; no implementation gap found. Status remains `review` for senior review.

The existing 20-fact `ProviderErrorDocsConformanceTests` suite and the `run-provider-error-docs-gates.ps1` gate already cover every acceptance criterion with source-derived, exact-cardinality inventory equality. A focused validation re-ran the suite and the gate against current source/docs and confirmed all inventories still equal the authoritative sources, so no additional automation was added (per the preserve-the-existing-pattern boundary).

- `pwsh ./tests/tools/run-provider-error-docs-gates.ps1 -SkipRestoreBuild` passed: 20 total, 0 failed, exit 0; `_bmad-output/gates/provider-error-docs/latest.json` `status: passed`.
- Focused `ProviderErrorDocsConformanceTests` re-run: 20 total, 0 failed.
- Baseline Contracts.Tests filter executed 92 total, 0 failed, confirming the 20 new facts register without dropping prior facts (72 → 92).
- CI-wiring diff is narrowly scoped: one appended `contract-spine.yml` gate step (after operations-audit-docs) and one appended baseline-filter FQN; no lanes broadened, `submodules: false` preserved.
- Recursive-submodule scan over docs/gate/test/workflow surfaces found only documented-forbidden-pattern text, no executable recursive setup.
- QA automation summaries written: `tests/7-15-test-summary.md` (durable) and `tests/test-summary.md` (latest pointer).

### Change Log

| Date       | Version | Description | Author |
| ---------- | ------- | ----------- | ------ |
| 2026-05-31 | 0.1     | Initial story context created after stalled create-story child recovery. Status -> ready-for-dev. | Story Automator |
| 2026-05-31 | 1.0     | Implemented provider integration + canonical error docs, 20-fact static conformance test, focused gate + report, contract-spine step, and baseline filter. All focused/safety/baseline gates green; format/lint clean. Status -> review. | Amelia (dev-story) |
| 2026-05-31 | 1.1     | QA automation verified coverage: 20/20 focused conformance + provider-error-docs gate green, baseline Contracts.Tests filter 92/92, CI-wiring diff confirmed narrowly scoped. No gap found; no new automation added. Wrote QA automation summaries. Status stays review. | BMAD qa-automate |
| 2026-05-31 | 1.2     | Adversarial senior review: independently re-derived every inventory from source and ran the 20-fact conformance suite (20/20). All 14 ACs implemented, all [x] tasks verified done, cross-links resolve, no recursive-submodule commands, `git diff --check` clean. Fixed 1 MEDIUM (File List omitted the regenerated `baseline-ci/latest.json`) and 1 LOW (misleading negative-control comment). 0 Critical remain. Status -> done. | Senior Review (AI) |

## Senior Developer Review (AI)

**Reviewer:** jpiquot (automated adversarial review) on 2026-05-31
**Outcome:** Approve — Status set to `done` (0 Critical/High issues remain after fixes).

### Scope and method

Read every File List entry plus the authoritative sources, cross-referenced the story File List against `git status`, re-derived all inventories directly from source (not from the docs), and executed the focused `ProviderErrorDocsConformanceTests` suite independently. Specifically re-checked the areas flagged for targeted review: docs marker tables, conformance-test negative controls, the gate-script non-vacuous guard, CI/baseline wiring, the metadata-only scanners, and the recursive-submodule posture.

### Acceptance Criteria

All 14 ACs verified implemented and source-backed; all are enforced by the static gate. Inventories independently confirmed against source: capability operations (10), credential modes (4), readiness result codes (7), failure categories (15) with the retryable-by-default trio (`ProviderUnavailable`, `ProviderRateLimited`, `ProviderTransientFailure`), Forgejo supported versions (3), generated `CanonicalErrorCategory` (47), parity-oracle distinct categories (43), `ProblemDetailsClientAction` tokens (6), CLI exit codes (14), and the MCP `range_unsatisfiable -> internal_error` fallback. Every `[x]` task corresponds to real, verifiable work.

### Verification evidence

- Focused `ProviderErrorDocsConformanceTests`: **20/20 passed** (re-run after review fixes; 20/20 again).
- Gate report `_bmad-output/gates/provider-error-docs/latest.json`: `status: passed`, `diagnostic_policy: metadata-only`, valid metadata-only JSON.
- Gate script `$runnerMethods` array (20 entries) equals the `[Fact]` set; non-vacuous guard fails closed on both the VSTest and xUnit-fallback paths.
- CI wiring: `contract-spine.yml` step added after operations-audit-docs (`submodules: false`, `contents: read`); baseline filter appended; no other lane (`ci.yml`, release, scheduled, policy) references the gate script — lane separation holds.
- Cross-links in both docs resolve to existing files; no `--recursive` submodule command in any doc/gate/test/workflow surface; `git diff --check` clean.

### Findings and fixes applied

- **MEDIUM (fixed):** Story File List omitted `_bmad-output/gates/baseline-ci/latest.json`, which this story's baseline-gate re-run regenerated (diff = the appended `ProviderErrorDocsConformanceTests` filter). Added to the File List.
- **LOW (fixed):** Negative-control item 3 in `ProviderErrorDocsConformanceTests.cs` carried a comment ("oracle-derived complement") that did not match the code (it asserts the stale inventory against the source-derived `generated` set). Comment clarified; behavior unchanged.
- No runtime, generated-client, OpenAPI, provider-adapter, parity-oracle, or Forgejo-snapshot files were touched — the documentation-plus-static-conformance scope boundary was respected.
