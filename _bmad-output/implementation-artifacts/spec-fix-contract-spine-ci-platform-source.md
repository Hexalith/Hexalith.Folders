---
title: 'Fix contract spine CI Platform source preparation'
type: 'bugfix'
created: '2026-11-02'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The contract and generated artifact CI job restores and builds the AppHost after initializing eight build submodules, but the AppHost links two Platform source files that are absent in this checkout, causing CS2001.

**Approach:** Run the existing guarded CI topology preparation script in the contract spine job before restore, preserving the pinned eight-submodule list. Update focused workflow conformance assertions. Leave the pending Story 12.2 source inventory sealed and report that the workflow row becomes stale.

</frozen-after-approval>

## Implementation Notes

- Added a Bash preparation step to `.github/workflows/contract-spine.yml` after the eight-root initializer and before restore. The script is not executable, so the workflow invokes it through `bash`.
- Extended the focused C# workflow assertion and the Python topology-preparation workflow assertion to pin the new step and its ordering.
- Preserved the eight-root command and the pending Story 12.2 source inventory. The workflow and both edited test files have rows in that inventory; all three rows are now stale.
- Review found that the initial Python assertion did not pin the exact eight-root command or enforce preparation after it. Added both checks.
- Validation: 26 Python topology tests and six focused contract-spine C# tests passed; the Contracts test project built with zero warnings; `actionlint` and `git diff --check` passed. CI solution restore passed. The matching local solution build was cancelled after a concurrent build locked `Hexalith.Folders.Server.dll` during copy (`MSB3026`); the AppHost's evaluated Platform root resolves to the checked-out root.

## Review Triage Log

- Low, patched: The pending Story 12.2 inventory lists the workflow at line 26, the Python test at line 122, and the C# test at line 4350. The first note underreported the affected rows; it now records all three.
- Medium, patched: The initial conformance assertions only required preparation before restore. An extra Platform path or preparation before the root initializer could have passed; the Python test now checks the exact command and order.
