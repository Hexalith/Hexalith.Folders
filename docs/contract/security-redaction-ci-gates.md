# Security Redaction CI Gates

Story 7.6 adds the `security-and-redaction-gates` PR check as the stable branch protection surface for sentinel, redaction, forbidden-field, and tenant cache-key leakage controls. The job is metadata-only, offline, and keeps the Story 7.4/7.5 setup posture: `actions/checkout@v6` with `submodules: false`, explicit root-level submodule initialization, `actions/setup-dotnet@v5`, `global-json-file: global.json`, and NuGet cache inputs.

## Local Command

Run from the repository root after restore/build:

```powershell
.\tests\tools\run-security-redaction-ci-gates.ps1
```

The gate does not require provider credentials, production secrets, tenant seed data, service containers, Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, Playwright browser installation, live provider calls, production endpoints, package publishing, container publishing, release upload, or artifact upload.

## Categories

- `sentinel-corpus`: verifies the synthetic sentinel vocabulary, telemetry surface inventory, quarantined negative controls, and covered source resolution.
- `redaction-channel-scan`: runs output-channel leakage checks across OpenAPI, context query, safe-denial, diagnostic, and runtime artifact inventory surfaces.
- `forbidden-field-diagnostics`: verifies bounded missing-channel diagnostics, safety workflow documentation parity, and redacted OpenAPI wrappers that forbid `value` when redacted.
- `tenant-cache-key-lint`: runs only the tenant cache-key lint and exception-review checks from governance completeness.

## Exact Test Inventory

Project: `tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj`

- `SafetyInvariantGateTests.SentinelCorpusDeclaresAuthoritativeSyntheticVocabulary`
- `SafetyInvariantGateTests.TelemetrySurfaceVocabularyIsExplicitAndInventoryAddressable`
- `SafetyInvariantGateTests.SentinelCorpusAvoidsRealDataAndKeepsNegativeControlsQuarantined`
- `SafetyInvariantGateTests.ChannelInventoryResolvesCoveredSourcesAndBoundsMissingChannels`
- `SafetyInvariantGateTests.SafetyScansDetectQuarantinedControlsWithoutScanningQuarantineAsNormalArtifacts`
- `SafetyInvariantGateTests.OpenApiExamplesAndContextQueriesRemainMetadataOnly`
- `SafetyInvariantGateTests.SafeDenialAndDiagnosticStatesDoNotRevealResourceExistence`
- `SafetyInvariantGateTests.StoryElevenDiagnosticChannelsAreReevaluatedAgainstCurrentArtifacts`
- `SafetyInvariantGateTests.StoryFourteenTelemetryChannelsAreCoveredByRuntimeArtifacts`
- `SafetyInvariantGateTests.MissingChannelDiagnosticsAreEmittedAsBoundedRuntimeEvidence`
- `SafetyInvariantGateTests.WorkflowAndDocumentationExposeSameOfflineSafetyGate`
- `AuditOpsConsoleContractGroupTests.AuditOpsConsoleSchemas_EnforceBehavioralInvariantsFromFollowUpReview`
- `GovernanceCompletenessGateTests.CacheKeyExceptionManifestIsReviewedAndCurrentRepositoryHasNoTenantDataCacheKeysWithoutScope`
- `GovernanceCompletenessGateTests.CacheKeyExceptionApprovalStateFailsClosedForExpiredOrUnknownStatus`
- `GovernanceCompletenessGateTests.CacheKeyLintNegativeControlsClassifyTenantScopeAndExceptionsWithoutEchoingKeyValues`

The script deliberately does not call `run-baseline-ci-gates.ps1`, `run-contract-parity-ci-gates.ps1`, `run-dapr-policy-conformance-gates.ps1`, `run-container-image-gates.ps1`, or `run-governance-completeness-gates.ps1`. Existing focused gates remain usable for their broader lanes.

## Report

The script writes `_bmad-output/gates/security-redaction-ci/latest.json` with gate name, category names, repository-relative artifact paths, test project/filter names, status, and exit code only. Reports and diagnostics identify category, emitting channel where the source test provides it, rule/category, sample ID or exception rule ID, and repository-relative artifact path without echoing raw sentinel values, file contents, provider payloads, token-shaped strings, credential material, tenant data, cache-key values, local absolute paths, production URLs, stack traces, or unauthorized-resource hints.

## Fixture Ownership

- `tests/fixtures/audit-leakage-corpus.json` owns the authoritative synthetic sentinel vocabulary.
- `tests/fixtures/safety-channel-inventory.json` owns safety channel coverage and repository-relative artifact source resolution.
- `tests/fixtures/quarantine/safety-negative-controls.json` owns contaminated negative controls and must remain quarantined.
- `tests/fixtures/cache-key-exceptions.yaml` owns reviewed tenant cache-key lint exceptions.
- `SafetyInvariantGateTests` owns sentinel, redaction, output-channel, missing-channel, and bounded diagnostic leakage checks.
- `GovernanceCompletenessGateTests` owns tenant cache-key lint and exception-review checks.

## Contract Spine Workflow Relationship

`.github/workflows/contract-spine.yml` still runs `run-safety-invariant-gates.ps1 -SkipRestoreBuild`, `run-governance-completeness-gates.ps1 -SkipRestoreBuild`, and `run-dapr-policy-conformance-gates.ps1 -SkipRestoreBuild`. That transitional duplication remains deliberate for Story 7.6 because the older workflow still carries broader safety, governance completeness, and Dapr policy coverage until Stories 7.8 and release governance cleanup decide the final split. Do not narrow `contract-spine.yml` without replacement coverage for safety/redaction, cache-key lint, governance completeness, and Dapr policy gates.

## Submodule Policy

Use only the explicit root-level command:

```text
git submodule update --init references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.PolymorphicSerializations references/Hexalith.Tenants
```

Nested recursive initialization is forbidden unless the user explicitly requests nested submodule work.
