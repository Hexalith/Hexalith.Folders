# Accessibility CI Gates

Story 8.4 stands up the automated accessibility (axe-core / WCAG 2.2 AA) gate for the read-only operations
console. The stable required status-check name is `accessibility-gates`; branch protection is configured
outside this repository, but this job name is intentionally stable so maintainers can require it. This gate is
the I-5 accessibility entry — it closes the prior "no automated axe/WCAG conformance gate is wired into CI"
absence the 2026-06-22 readiness review flagged.

The workflow is `.github/workflows/ci.yml`. It runs for `pull_request` and pushes to `main`, `next`, `alpha`,
and `beta`. Checkout uses `submodules: false`; the workflow then initializes only the documented root-level
build submodules and never initializes nested submodules recursively:

```text
git submodule update --init Hexalith.AI.Tools Hexalith.Commons Hexalith.EventStore Hexalith.FrontComposer Hexalith.Memories Hexalith.Tenants
```

The `accessibility-gates` job runs on its own runner — a separate job, so the Playwright Chromium provisioning
does not slow the fast `baseline-build-and-unit-gates` lane. It is the only CI job that provisions a browser.

## What the gate verifies

The gate is a **union** — axe-core does not cover all of WCAG 2.2 AA (keyboard operability, visible-focus
appearance, reflow/zoom, and not-color-alone are partly or not axe-automatable), so it composes:

1. **axe-core / WCAG 2.2 AA scan** (`ConsoleAxeWcagGateTests`) filtered to the cumulative AA tag set
   (`wcag2a, wcag2aa, wcag21a, wcag21aa, wcag22aa`), failing on **any** AA-tagged violation. Covers axe's
   auto-detectable subset: color-contrast (1.4.3), name/role/value, semantic headings/landmarks, table
   structure, and link/control names.
2. **Keyboard navigation + visible focus** (`ConsoleKeyboardFocusGateTests`) — genuine `Tab` traversal asserts
   the console content is keyboard-operable and every focused control shows a visible focus indicator (WCAG
   2.1.1 / 2.4.7 / 2.4.11).
3. **Zoom + dense-identifier no-clipping** (`ConsoleZoomReflowGateTests`) — at 125 % / 150 % / 200 % browser
   zoom over dense identifiers, key surfaces stay visible and un-clipped, with full reflow and no horizontal
   overflow at the 200 % reflow target (UX-DR31 / WCAG 1.4.10).
4. The already-green bUnit `AccessibilityContractSweepTests` not-color-alone / structural sweeps (run in the
   `baseline-build-and-unit-gates` lane) surfaced as gate evidence.

The scans run against a **populated** console host: the UI E2E host registers a synthetic, metadata-only stub
`IClient` so the three critical journeys render their full read-only evidence surfaces (the dead-loopback
hermetic host degrades to the read-model-unavailable state, which axe cannot meaningfully scan). The three
journeys are find-and-inspect-trust-state (J1), prove-tenant-isolation (J2), and diagnose-failure-from-evidence
(J3). Each axe scan is scoped to the console page-content root; the shared FrontComposer shell chrome is out of
this story's scope.

## Gate script

`tests/tools/run-accessibility-ci-gates.ps1` provisions Chromium and runs the `Accessibility` namespace of the
UI E2E lane:

- `-SkipBrowserInstall`: skip the `tests/install-playwright.ps1` provisioning call (CI provisions Chromium in a
  separate step, then invokes the gate with this switch).
- `-SkipRestoreBuild` / `-NoRestore`: skip the solution restore/build when a shared lane has already built it.

It is a CI contract: no artifact upload, package publish, secrets, network beyond localhost, or recursive
submodule initialization. UI E2E work requires the matching Playwright browser binaries; provision them through
`tests/install-playwright.ps1` (the NuGet package alone is not sufficient).

## Manual residuals (not automated by this gate)

The gate does not over-claim. Genuinely-manual WCAG checks — screen-reader review, forced-colors mode, and
color-blindness review — remain owned release-validation evidence in
`docs/ux/ops-console-accessibility-and-no-mutation-verification.md`; the axe gate does not automate them.

## Diagnostics

The gate writes a metadata-only report to `_bmad-output/gates/accessibility/latest.json`. The report may contain
the gate name, status, exit code, the relative report path, the diagnostic policy, and the validation namespace.
It must not contain absolute local paths, secrets, tokens, tenant data, provider payloads, raw page HTML,
diffs, or environment dumps. axe failure output is metadata-only: rule id, target selector, and helpUrl only.

## Local validation

Run from the repository root (provision the browser once per machine first):

```text
pwsh ./tests/install-playwright.ps1
pwsh ./tests/tools/run-accessibility-ci-gates.ps1 -SkipBrowserInstall
```
