---
title: 'Share audit leakage corpus checks'
type: 'bugfix'
created: '2026-11-23'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Audit leakage tests duplicate corpus loading and disagree on what constitutes a match. Close DW-321, DW-323, DW-326, DW-327, and DW-332 by sharing test-only fixtures and matching logic, applying the corpus channel rules to the accepted archive checks, and reporting failures by sample ID.

</frozen-after-approval>

## Implementation Notes

- Shared linked test sources load the labelled corpus and scan decoded JSON and text. The contract gate still uses its bounded UTF-8 and duplicate-key validation before mapping samples.
- Artifact scans keep token boundaries. Runtime scans detect embedded values. The archive sweep checks the fixture's per-sample channel policy and keeps its separate actor-evidence injection rows.
- Corpus-driven endpoint and aggregate tests report sample IDs. Workspace status scans validate the response status and scan each body independently.

## Review Triage Log

- Fixed the representation-dependent token-boundary match. JSON matching uses decoded nodes.
- Fixed unknown archive channels and missing map entries. The reader validates fixture channels and the archive checks pin all eight expected entries.
- Fixed header diagnostics and coverage. The sweep checks the reason phrase, header names, and values with sample-ID messages.
- Fixed the safety gate loader regression. Corpus and quarantine inputs retain the gate's strict path and parsing behavior.
- Fixed quarantine coverage. Each control must generate its own expected sample finding.
- Fixed collection diagnostics. The telemetry vocabulary assertion evaluates a boolean and never stringifies the corpus samples.
- Fixed examples diagnostics. The shape checks report their fixed marker without rendering the examples buffer.
- Fixed rejection and operational tag diagnostics. Command-type equality is boolean and tag names and values are scanned.
- Fixed silent short-sample exclusion. Both audit endpoint sweeps use every sample.
- Fixed endpoint response prerequisites. Both workspace sweeps check for a success and a Bad Request Problem Details response.
- Deferred the artifact manifest's existing safe-provenance scope policy under DW-239. The corpus contract requires every sample to participate in positive scans.
- Deferred decoded YAML artifact scans. The matcher handles the selected text and JSON inputs.
- Existing accepted archive fixtures characterize safe defaults. Production channel injection remains tracked under DW-328.

## Verification

- The four affected projects built in package-based Release mode. Class-filtered xUnit runs passed 72 contract tests, 174 core tests, 190 server tests, and 9 integration tests.
- `pwsh -NoProfile -File tests/tools/run-safety-invariant-gates.ps1 -SkipRestoreBuild` passed all 62 safety-gate tests.
- The Debug source builds of the core, server, and integration test projects stopped with CS1704. The dependency graph loaded both project and package copies of `Hexalith.Commons.UniqueIds`.
