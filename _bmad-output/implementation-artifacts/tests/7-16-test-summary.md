# Test Automation Summary

> Durable per-story copy for Story 7.16. Canonical latest-run summary: [`test-summary.md`](./test-summary.md).

**Workflow:** `bmad-qa-generate-e2e-tests`
**Story:** `_bmad-output/implementation-artifacts/7-16-publish-nfr-traceability-bridge.md`
**Feature under test:** The NFR traceability bridge — `docs/exit-criteria/nfr-traceability.md` (70-row PRD↔epics
NFR1–NFR70 trace table, nine-category rollup, six-class BDD evidence rollup, owned reference-pending gaps),
its focused gate (`tests/tools/run-nfr-traceability-gates.ps1` + `_bmad-output/gates/nfr-traceability/latest.json`),
and the contract-spine / baseline / release-packages wiring that makes missing NFR evidence block release review.

This is a **documentation + static release-readiness conformance** story: no UI and no HTTP API, so there are
no browser/E2E surfaces to drive. The feature is enforced by xUnit v3 + Shouldly static-conformance facts that
re-derive every inventory from source. The QA pass **audited and hardened** the existing conformance suite.

## Generated Tests

### API Tests

- [x] Not applicable — Story 7.16 publishes a static traceability document and gate wiring, not runtime
  endpoints. "Error cases" are exercised as negative controls routed through the same production parsers/scanners.

### E2E / Conformance Tests

`tests/Hexalith.Folders.Contracts.Tests/Deployment/NfrTraceabilityConformanceTests.cs` — **14 → 16 facts**.

Method: a multi-agent gap audit (5 finder dimensions → adversarial verification of every candidate) over the
14 ACs + the QA checklist. 17 candidates → **10 confirmed** real gaps → consolidated to **8 fixes** (three
`surfaces[]` findings collapsed). 7 candidates rejected with cause (e.g., AC7 "no new ADRs/runbooks" is a
review-time scope constraint, not statically testable without a baseline-commit diff; empty marker block is
already caught by the 70-row count assertion).

Applied (each closes a regression that previously left all facts green while violating an AC):

- [x] **G1 / AC1** — new fact `NfrTraceabilityDocNamesItsSourceAuthorities`: the doc must name PRD, epics,
  `c0-c13-governance-evidence.yaml`, `_bmad-output/gates/`, and `docs/exit-criteria/` source authorities.
- [x] **G2 / AC11** — bounded `surfaces[]` exact-set assertion added to the gate-report fact (catches a
  dropped surface *and* an unbounded/extra surface).
- [x] **G3 / AC12** — lane separation extended to assert `nightly-drift.yml` (scheduled) and
  `policy-conformance.yml` (scheduled + policy) never run the focused gate, not just `ci.yml`.
- [x] **G4 / AC13** — structural staleness guard (`Mode -eq 'Publish' -and source_commit -ne SourceRevisionId`)
  asserted inside the NFR-scoped block and required to precede the `stale-nfr-traceability-evidence` Fail-Gate.
- [x] **G5 / AC10** — tampered-row-hash negative control routed through the same `row.Hash.ShouldBe(StableHash(prd))`
  comparison the production fact uses (replaces a tautological hash-function check).
- [x] **G6 / AC10** — wrong-column-count malformed-table negative control through the real `ParsePipeRows` + the
  exact 10-column predicate.
- [x] **G7 / AC11** — repository-root resolution pattern (`Split-Path -Parent $MyInvocation.MyCommand.Path`,
  `Resolve-Path`, `Join-Path $toolsParent`) asserted in the gate-script fact.
- [x] **G8 / AC13** — new fact `NfrTraceabilityGateRunsOnlyInReleasePrerequisiteJob`: parses
  `release-packages.yml` as YAML and pins the gate to `release-prerequisite-gates`, asserting it is absent from
  `release-package-conformance` and `publish-packages`.

Supporting edits: helper `JobRunCommands` (per-job YAML step parsing), regex `NfrStaleSameCommitGuard`,
`ExpectedReportSurfaces` set, two workflow-path constants. The two new `[Fact]` methods were appended to
`$runnerMethods` in `run-nfr-traceability-gates.ps1` so the fail-closed vacuous guard and the
`$runnerMethods == [Fact] set` assertion stay exact (14 → 16).

## Coverage

- ACs with strengthened/added automated coverage: **AC1, AC10, AC11, AC12, AC13** (5 of 14).
- `NfrTraceabilityConformanceTests` facts: **16** (all green).
- Every AC now has at least one failing-on-regression assertion. The single non-automatable constraint (AC7
  scope) remains a documented code-review check.

## Validation

| Check | Command | Result |
| ----- | ------- | ------ |
| Build | `dotnet build tests/Hexalith.Folders.Contracts.Tests/...csproj -m:1` | 0 warnings / 0 errors |
| Focused gate | `pwsh ./tests/tools/run-nfr-traceability-gates.ps1 -SkipRestoreBuild` | **16/16 passed**; report `status: passed`, bounded `surfaces[]`, 40-hex `source_commit` |
| Baseline CI | `pwsh ./tests/tools/run-baseline-ci-gates.ps1 -SkipRestoreBuild` | **passed**; Contracts.Tests filter executes all 16 NFR facts |

All new assertions pass against the real AC-satisfying state (non-vacuous) and target a specific regression.
Everything stays metadata-only; the `.trx` output is gitignored.

## Files Changed

- `tests/Hexalith.Folders.Contracts.Tests/Deployment/NfrTraceabilityConformanceTests.cs` — +2 facts,
  +6 strengthened assertions, +2 helpers/regex, +constants/sets.
- `tests/tools/run-nfr-traceability-gates.ps1` — `$runnerMethods` updated to the 16-fact set.
- `_bmad-output/gates/nfr-traceability/latest.json`, `_bmad-output/gates/baseline-ci/latest.json` — regenerated.

## Next Steps

- Runs in CI via the existing `contract-spine` lane and the baseline Contracts.Tests filter (already wired).
- Consider a future baseline-diff check to automate the AC7 scope constraint (no new ADRs/runbooks).
