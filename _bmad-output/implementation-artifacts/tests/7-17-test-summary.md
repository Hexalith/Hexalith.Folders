# Test Automation Summary

**Workflow:** `bmad-qa-generate-e2e-tests`
**Story:** `_bmad-output/implementation-artifacts/7-17-publish-adr-set-and-maintenance-runbooks.md`
**Feature under test:** The ADR/runbook handoff documentation set: six retrospective ADRs, seven maintenance
runbooks, exact ADR/runbook indexes, focused `AdrRunbookDocsConformanceTests`, the
`run-adr-runbook-docs-gates.ps1` runner/report, and the contract-spine/baseline wiring.

This is a documentation + static conformance story. There are no browser or HTTP API flows to automate; the
test surface is xUnit v3 + Shouldly static conformance plus the PowerShell gate.

## Generated Tests

### API Tests

- [x] Not applicable — Story 7.17 publishes static ADR/runbook evidence and CI gate wiring, not runtime endpoints.

### E2E / Conformance Tests

- [x] `tests/Hexalith.Folders.Contracts.Tests/Deployment/AdrRunbookDocsConformanceTests.cs` — **10 facts**.

Coverage includes:

- [x] Gate-script source-of-truth manifest is non-vacuous: six ADR areas and seven runbook topics exactly once.
- [x] New ADRs have required sections, accepted status text, no residual `PLACEHOLDER`, and real architecture decision IDs.
- [x] `0000-template.md` and existing `0001` are preserved.
- [x] New runbooks carry the AC5 section contract; preserved `tenant-deletion.md` keeps its established section set.
- [x] ADR and runbook indexes are exact against directory inventories.
- [x] New ADR/runbook/index/report diagnostics remain metadata-only and reject host-absolute paths, tokens/JWTs, and non-placeholder hosts.
- [x] Gate script has strict PowerShell posture, `utf8NoBOM` report writes, xUnit fallback, fail-closed vacuous guard, and `$runnerMethods == [Fact]` lockstep.
- [x] CI wiring is restricted to `contract-spine.yml` and baseline Contracts.Tests filter; PR `ci.yml`, nightly drift, policy-conformance, release-packages workflow, and release-package gate do not run this focused gate.
- [x] AC10 negative controls route through the same parsers/scanners for placeholder ADRs, missing ADR/runbook sections, bad status, absent decision IDs, missing/orphan index entries, malformed Markdown/YAML/JSON, unsafe evidence, and recursive-submodule commands.

QA hardening applied during the automation pass:

- [x] Extended lane-separation assertions to prove release-package workflow/gate remain free of the ADR/runbook focused gate.
- [x] Strengthened runbook-index negative controls to use exact-set parser comparisons for missing and orphan runbook rows.

## Validation

| Check | Command | Result |
| ----- | ------- | ------ |
| Build | `dotnet build Hexalith.Folders.slnx --no-restore -m:1` | passed, 0 warnings / 0 errors |
| Focused tests | `dotnet test tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-build --filter FullyQualifiedName~Hexalith.Folders.Contracts.Tests.Deployment.AdrRunbookDocsConformanceTests` | 10/10 passed |
| Focused gate | `pwsh ./tests/tools/run-adr-runbook-docs-gates.ps1 -SkipRestoreBuild` | 10/10 passed; report `status: passed` |
| Format/analyzers | `dotnet format whitespace ... --include AdrRunbookDocsConformanceTests.cs` and `dotnet format analyzers ... --include AdrRunbookDocsConformanceTests.cs` | passed |
| Baseline CI | `pwsh ./tests/tools/run-baseline-ci-gates.ps1 -SkipRestoreBuild` | passed; Contracts.Tests filter executes 118 facts |
| Diff hygiene | `git diff --check` | clean |
| Submodule policy scan | `rg -n -- '--recursive|git submodule update --init --recursive'` over modified executable/docs surfaces | clean |

## Files Changed

- `tests/Hexalith.Folders.Contracts.Tests/Deployment/AdrRunbookDocsConformanceTests.cs`
- `tests/tools/run-adr-runbook-docs-gates.ps1`
- `_bmad-output/gates/adr-runbook-docs/latest.json`
- `_bmad-output/gates/baseline-ci/latest.json`
- `.github/workflows/contract-spine.yml`
- `tests/tools/run-baseline-ci-gates.ps1`

## Next Steps

- Run code review. No QA-only blocker remains.
