---
title: 'Resolve five deferred contract gate diagnostics items'
type: 'bugfix'
created: '2026-11-22'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Make contract verification give consistent results and actionable, bounded diagnostics across checkouts and build modes. Resolve DW-237, DW-241, DW-242, DW-243, and the consumer-docs YAML boolean follow-up from `spec-five-deferred-governance-prerequisites.md`.

- Given scan paths that differ only by case in a file or a parent directory, the safety gate reports bounded prerequisite drift. Repeated declarations of the same spelling continue to work.
- Given a pending safety inventory channel, its optional `remediation_hint` appears in the diagnostic when it meets the existing metadata and length limits. An omitted hint uses the existing guidance. An invalid hint fails with a bounded message.
- Given a missing prebuilt safety test assembly, first-party dependency, or .NET entry-point manifest, the safety runner publishes `prerequisite_drift` and exits nonzero before launching the test host. A complete test layout preserves ordinary test exit codes and report statuses.
- Given a missing safety marker in a present artifact, the gate emits `SAFETY-REQUIRED-MARKER-ABSENT`. Missing context-query operations remain prerequisite drift. Diagnostics omit source content and the absent marker text.
- Given a pattern example, consumer docs and governance both require the same YAML 1.2 core boolean `true`. Quoted untagged values, integers, and false fail validation.

Use the existing offline verification gates and record the five resolutions in the deferred ledger. Keep the pending PD10 candidate inventory synchronized with its changed verification inputs. Push the completed change to `main`.

</frozen-after-approval>

## Implementation Notes

- Baseline is `8ae7e9430612cb53c7289b43fe48f24da13368a7` on `origin/main`. The work affects offline test tooling, its documentation, and the pending candidate inventory.
- The safety class registers declared sources and discovered files with one case-insensitive lookup across channels. It normalizes dot segments and repeated separators in declarations, and keeps exact spellings for file access and diagnostics checks.
- The safety runner checks five build outputs before invoking xUnit. The contract fixtures exercise both runner modes with isolated test layouts. The governance and consumer-docs checks share the YAML core scalar reader.
- `aspire run --non-interactive` initially failed to create the user-scoped .NET shared-memory directory. With the existing NuGet data location, it timed out during the AppHost build. `aspire describe --format Json` reported no running AppHost. The offline contract gates use a separate Release package-mode build.
- The Release build had zero warnings or errors. The real PowerShell gates passed 58 safety and 267 governance tests. Consumer-docs and PD10 tests passed 70 checks. No tests were skipped in those runs.
- The unfiltered assembly passed 700 of 753 tests on the first run. Its 53 failures came from a missing Release parity-generator build and a PowerShell data-directory override inherited by the package restore. After building the generator and confining that override to PowerShell, all 69 parity and packaging tests passed, including every initial failure.
- Repository-configured commitlint, touched-file whitespace verification, and `git diff --check` passed.

## Review Triage Log

- Low, rejected. Scans use paths from the checkout and declared inventory. Detecting undeclared names already collapsed by a case-insensitive checkout would make the offline gate depend on Git metadata. A separate tracked-name audit found zero collisions in the generated-client, OpenAPI, contract-docs and test-tool roots.
- Medium, patched. Direct-file exclusions and the case registry use the same normalization for redundant separators and dot segments.
- Medium, patched. The inventory preflight rejects conflicting declarations across channels before filesystem checks. Regressions cover cross-channel declarations, exact repeats, file names, and parent directories. The real safety gate exercises recursive scanning of the canonical inventory.
- Medium, patched. Safe-state and safe-denial markers use fixed context labels and bounded missing-marker errors. Missing operations and missing operation IDs report prerequisite drift. Canonical operation-ID casing remains required, as in the baseline assertion.
- Medium, patched. Runner test failures omit raw subprocess output.
- Medium, patched. Runner tests cover both runner modes and preserve ordinary exit codes. The real PowerShell gate exercised the native test host and report publication.
- Low, patched. The guide lists PowerShell 7 and records current Story 4.14 channel coverage.
- Medium, patched. The regenerated PD10 inventory matches a second generation byte for byte. Its qualification remains pending and production exposure remains disabled.
- Low, patched. The final safety and governance PowerShell runs published `passed` reports. The failed report from the first safety run cleared after restoring the expected assembly-path literal.
