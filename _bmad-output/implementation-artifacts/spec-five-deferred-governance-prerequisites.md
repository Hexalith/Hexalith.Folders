---
title: 'Resolve five deferred governance prerequisite items'
type: 'bugfix'
created: '2026-11-22'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Make the Folders governance completeness gate reject invalid or missing inputs with bounded diagnostics and a usable report. Resolve DW-67, DW-70, DW-249, and the C7 review-date and YAML Unicode follow-ups from `spec-five-deferred-yaml-shapes.md`.

- Given a C7 approval with an optional `review_by` field, the exact C7 evaluator applies the shared date rule. A missing or future date passes that check. A malformed or duplicate field yields `approval_date_invalid`, and a date on or before the evaluation date yields `approval_stale`.
- Given a YAML file containing an invalid Unicode code point, the loader reports `yaml-malformed` without including parser detail or the original value.
- Given a pattern example's `synthetic_data_only` declaration, the gate requires a YAML 1.2 boolean. Integer `0` and `1`, legacy truthy strings, and other non-boolean values fail with a bounded diagnostic.
- Given a checkout path containing wildcard characters or reached through a directory alias, the runner uses the checkout from which its script was invoked and writes its report there.
- Given a missing repository marker, SDK, test assembly, a recognized test-side prerequisite failure, or an empty test selection, the runner writes `prerequisite_drift` and exits nonzero. Ordinary test failures preserve their exit code and `failed` report status.

Use the existing gate and runner, canonical fixtures, and isolated regression tests. Record the results in the ledger and push the completed change to `main`.

</frozen-after-approval>

## Implementation Notes

- Baseline is `9646487abf59b5b61cfa4a57e4876d00477eca46`. The batch is confined to the governance test helper, its focused regression files, the PowerShell runner, and its documentation and ledger. It needs no new public API or migration.
- `GovernanceCompletenessGateTests.cs` owns `EvaluateApprovalRecords`, `EvaluateC7DecisionEvidence`, `LoadYamlStream`, and the pattern manifest check. Reuse the approval-date and file-backed YAML regression tests. `GovernanceCompletenessGateTests.ReportTests.cs` invokes a copy of the actual runner against controlled prerequisites and native exit codes.
- The shared optional date check distinguishes absent, malformed, duplicate, stale, and future C7 dates. The YAML loader handles out-of-range escape failures at the parser boundary. YAML 1.2 core boolean parsing rejects numeric input and string declarations with a bounded diagnostic.
- The runner publishes categorized prerequisite failures for the selected checkout and checks the xUnit assembly summary for executed tests. An isolated checkout with an unavailable SDK exited 155 and produced `prerequisite_drift` without printing the SDK probe output.
- The PD10 generator refreshed only the five changed candidate entries and their aggregate digest. The inventory remains pending after qualified source pins, with production exposure disabled.
- The Release Contracts test project built with zero warnings or errors. The PowerShell governance runner passed 267 tests and published `passed`. Safety, PD10, and consumer docs passed a further 81 tests with zero skips.
- `aspire run --non-interactive` timed out after 50 seconds during the AppHost build. `aspire describe --format Json` found no running AppHost. The governance checks run independently of that topology.

## Review Triage Log

- Medium, patched. The runner now resolves the checkout's SDK before local restore and reports an unavailable SDK as prerequisite drift. The prebuilt lane uses its installed .NET host to execute the assembly.
- Medium, patched. The runner recognizes governance prerequisite exceptions and preserves the xUnit exit code. Root lookup, malformed YAML, and invalid booleans have separate regressions that check diagnostic bounds.
- Medium, patched. The C7 theory removes any existing review-date keys before injecting the case, so a future canonical review date cannot spoil the regression fixtures.
- Low, patched. The Unix alias case asserts that the runner actually selects the aliased checkout. Windows retains bracketed-path coverage.
- Medium, patched. The assembly summary must show actual executions. The regression suite includes an unrelated positive total beside a zero-test summary and an all-skipped or not-run summary.
- Low, deferred. The consumer docs gate still compares the pattern manifest flag with literal lowercase `true`. Its lookup predates the governance change and the canonical fixture satisfies both gates. The ledger records the remaining inconsistency.
