---
title: 'Resolve five deferred governance reliability issues'
type: 'bugfix'
created: '2026-10-28'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Complete five related deferred governance-check issues: DW-69, DW-248, DW-250, DW-251 and DW-252. A duplicated criterion should produce one duplicate diagnostic before its row metadata is evaluated. Missing or malformed OpenAPI operations must fail closed with bounded prerequisite-drift diagnostics rather than parser/assertion crashes or silent omission. The report writer must publish complete JSON atomically, fail explicitly on publication errors, and attempt to remove stale results. Read the root target framework from XML, ignoring comments and conditioned definitions; reject missing or ambiguous unconditional declarations instead of guessing conditional MSBuild values. Recognize generated/build/quarantine directory segments regardless of casing while retaining source paths with merely similar names.

Preserve valid governance checks, historical approvals, contracts, fixtures and dependency pins. Add focused positive and negative regressions, close the five ledger entries only after verification, review the changes, then commit and push main as requested.

</frozen-after-approval>

## Implementation Notes

- Small local footprint: governance test helpers and focused tests, PowerShell gate report writer and runner regressions, ledger and this spec. No unresolved product choices or data migrations; the user authorized commit/push.
- Existing governance documentation requires offline checks, fail-closed decisions and metadata-only diagnostics. Keep historical approval evidence unchanged.
- Code map: `tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs` owns all four evaluator/parser/path helpers; make it partial so focused regressions reuse private helpers. `tests/tools/run-governance-completeness-gates.ps1` writes discovery/final reports directly and has no report-failure handler. Add same-directory staging and bounded failure handling.
- Baseline revision: 9b7bda1d4f4ae54cb134adc1814f2e3ef216eabe; main clean. Independent investigation confirmed all five entries still apply.
- Required startup baseline: `aspire start --non-interactive --apphost src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj` exits 2 with Server CS1704: duplicate Hexalith.Commons.UniqueIds source/package references (plus certificate trust warnings). `aspire describe --apphost src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj --format Json` confirms no running AppHost. Validate this offline change through the focused Debug/source test project.
- Implemented all five fixes, added focused partial test files and documented report publication failure semantics. No contracts, approvals, dependencies or submodules changed.
- Initial baseline governance class passed 23/23. Final Debug/source build passes with zero warnings/errors. All three classes selected by the governance runner pass 88/88, zero skipped, including real PowerShell runner subprocesses with controlled test results and injected I/O failures.
- Regressions caught YAML plain null scalars being represented as text and PowerShell command wrappers normalizing exit codes. The loader now distinguishes plain/explicit YAML nulls from quoted strings; the subprocess harness explicitly propagates the invoked script exit code.
- All touched C# files pass focused `dotnet format whitespace --no-restore --verify-no-changes`; `git diff --check` passes. `git fetch origin` confirms no divergence. The Conventional Commit message passes the installed commitlint.

- Final post-review Debug/source build passes with zero warnings/errors. All three governance classes pass 96/96, zero skipped, including explicit YAML string tags, XML namespace isolation, final rename failure after all three mocked classes execute, and denied stale-report deletion. Focused whitespace verification and `git diff --check` pass. Review patched four findings and recorded three inherited follow-ups.

## Verification

- `DOTNET_PROCESSOR_COUNT=2 dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj -c Debug -m:1 -nr:false -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -v:q`: passed with zero warnings/errors.
- `DOTNET_PROCESSOR_COUNT=2 dotnet tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests.dll -class Hexalith.Folders.Contracts.Tests.OpenApi.GovernanceCompletenessGateTests -class Hexalith.Folders.Contracts.Tests.OpenApi.AuthorizationMatrixContractTests -class Hexalith.Folders.Contracts.Tests.OpenApi.ProviderCompatibilityCatalogContractTests -noLogo -noColor`: final post-review result 96/96 passed, zero skipped.
- `DOTNET_PROCESSOR_COUNT=2 dotnet format whitespace tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore --verify-no-changes --include tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.InputTests.cs tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.ReportTests.cs`: passed.
- `git diff --check`: passed.

## Review Triage Log

- Medium, patched: the new YAML null guard rejected explicit `!!str null` and `!!str ~`. Explicit string tags now take precedence; positive regressions preserve tagged and quoted null-like strings.
- Medium, deferred: operation IDs with explicit integer/boolean scalar types remain accepted. Baseline `RequiredScalar` accepts them too; recorded separate scalar-typing follow-up without broadening DW-248.
- Medium, deferred: non-scalar method keys and invalid path keys can evade operation enumeration. Baseline has the same skip/no-key-validation behavior; recorded bounded key-validation follow-up.
- Medium, deferred: the shared file loader preserves the raw YAML parser as an inner exception. Verified the baseline does this for every YAML consumer; recorded shared diagnostic-hardening follow-up.
- Medium, patched: XML LocalName-only matching accepted foreign namespaces. Accept only unqualified/current MSBuild namespaces and require the same namespace at all three declaration levels; added negative namespace regressions.
- Low, patched: report regression coverage lacked a final rename failure after successful discovery and tests. Added a real runner case that obstructs the destination during test invocation and verifies all three classes execute before publication fails.
- Low, patched: report regressions lacked stale-report deletion failure. Added controlled deletion denial to the real runner test, asserting failure status, bounded output, retained previous report and staged-file cleanup.
