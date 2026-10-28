---
title: 'Resolve five deferred governance input-shape items'
type: 'bugfix'
created: '2026-10-28'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Resolve five related deferred gate-input items: the three follow-ups from `spec-five-deferred-yaml-governance.md` (structural YAML string keys, collection tags, and known path-item metadata value shapes), the generic malformed/empty `review_by` follow-up from OQ1, and DW-71 encoding/BOM handling. OpenAPI enumeration must treat ordinary, quoted and explicitly string-tagged structural keys consistently, reject semantically duplicate keys and unsupported references, admit only compatible mapping/sequence tags, and require string summary/description and sequence parameters/servers. Empty strings are valid metadata; operation IDs remain nonempty. Extension values remain unrestricted. Metadata validation covers these immediate value shapes, not nested Parameter/Server schema validation. Invalid input produces bounded diagnostics without echoing values. An absent review date stays optional; a present date must be a valid ISO date strictly after today.

Verify DW-71's existing strict UTF-8 reader and optional UTF-8 BOM regressions before closing it; preserve working behavior. Keep canonical contracts, approval artifacts, generated outputs and dependency pins intact. Complete independent review, resolve all five ledger entries with evidence, commit and push to main as requested.

</frozen-after-approval>

## Implementation Notes

- Small reversible footprint: one existing governance helper file, focused partial tests, gate documentation, ledger and this spec; no new public API, migrations or unresolved product choices. Publication is explicitly authorized.
- Baseline: clean main at 0aca95c760d6b1e02c162303df3c2a5bbc2efeca. The focused Debug/source contract-test build passes with zero warnings/errors.
- Code map: `GovernanceCompletenessGateTests.cs` owns `LoadOpenApiOperationIds`, `IsOpenApiString`, and `EvaluateApprovalRecords`; extend OpenAPI-local lookups rather than altering all shared Required*/TryScalar consumers. `GovernanceCompletenessGateTests.YamlLoaderTests.cs` and a focused approval-date partial hold regressions. `SafetyInvariantGateTests.Inputs.cs` already implements DW-71; `SafetyInvariantGateTests.InputTests.cs` covers actual file reads, BOMs and invalid encoding, so reuse those checks.
- YAML tag compatibility follows https://yaml.org/spec/1.2.2/#1032-tag-resolution . Immediate path metadata shapes follow https://spec.openapis.org/oas/v3.1.0.html#path-item-object . Accept standard map/seq tags and the non-specific tags; reject incompatible/custom tags on structural collections.
- Required AppHost baseline: `aspire start --non-interactive --apphost src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj` exits 2 because Server imports both source/package Hexalith.Commons.UniqueIds assemblies (CS1704). This is inherited; gate tests are independent of Aspire. Inspect resource state separately and retain the exact blocker.
- `aspire describe --apphost src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj --format Json` confirms no running AppHost. No resource was left running.
- OpenAPI-local field dictionaries now resolve string keys consistently, reject semantic duplicates, and validate collection tags before enumeration. `IsOpenApiString` became `IsYamlString` so the review-date guard can reuse the same scalar typing. Shared Required*/TryScalar consumers remain unchanged.
- New negative controls exposed tagged-reference, structural collection and metadata-shape gaps before correction. A malformed YAML string in one duplicate-key test was corrected before the final run. The approval-date partial adds 25 cases with a fixed clock; DW-71 reuses its existing decoder and actual-file regressions.
- Four selected governance/safety/authorization/provider classes pass 257/257 with zero skipped. Focused whitespace verification and git diff --check pass. Fetched origin/main matches the baseline with no divergence.
- Independent review exposed a tagged review_by key bypass; the generic evaluator now resolves review-date key meaning, rejects semantic duplicates and invalid key tags, and preserves optional absence. Added quoted/escaped OpenAPI key coverage and a file-backed escaped-duplicate regression. Corrected the diagnostic catalog's claim that missing optional review_by is invalid, and clarified the C7 evaluator exception.
- Final post-review verification passes 265/265 with zero skipped; Debug/source build has zero warnings/errors. Whitespace verification, git diff --check and commitlint all pass. Fetched origin/main remains identical to the baseline. The five selected ledger entries are closed; two inherited review findings are recorded separately.

## Review Triage Log

- Medium, patched: tagged review_by keys bypassed the new value guard. Resolve the optional field by scalar key meaning; validate key/value tags and reject multiple spellings. Positive string-key and duplicate/invalid-key regressions cover the correction.
- Medium, deferred: inherited C7 exact-value evaluation skips review_by entirely; the current approved C7 input has no review date. Recorded a separate follow-up and clarified generic-evaluator documentation; approved evidence and C7 validation remain intact.
- Medium, deferred: inherited LoadYamlStream catches YamlException but invalid Unicode code points can raise ArgumentOutOfRangeException. Recorded a file-backed bounded-diagnostic follow-up; structural tag/shape changes do not alter parsing exceptions.
- Low, patched: diagnostic catalog incorrectly called missing optional review_by invalid. It now distinguishes missing required approved_on from malformed/duplicate present review_by.
- Low, patched: added quoted/escaped structural-key positives and a file-backed duplicate-key rejection, asserting the bounded parser-level diagnostic and absence of input markers.

## Verification

- `DOTNET_PROCESSOR_COUNT=2 dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj -c Debug -m:1 -nr:false -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -v:q`: passes, zero warnings/errors.
- `DOTNET_PROCESSOR_COUNT=2 dotnet tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests.dll -class Hexalith.Folders.Contracts.Tests.OpenApi.GovernanceCompletenessGateTests -class Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests -class Hexalith.Folders.Contracts.Tests.OpenApi.AuthorizationMatrixContractTests -class Hexalith.Folders.Contracts.Tests.OpenApi.ProviderCompatibilityCatalogContractTests -noLogo -noColor`: final post-review pass 265/265, zero skipped.
- `DOTNET_PROCESSOR_COUNT=2 dotnet format whitespace tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore --verify-no-changes --include tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.YamlLoaderTests.cs tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.ApprovalDateTests.cs`: passes.
- `git diff --check`: passes.
- AppHost baseline is blocked by the exact start command and CS1704 recorded above; the focused Debug/source gate build and tests pass independently.
