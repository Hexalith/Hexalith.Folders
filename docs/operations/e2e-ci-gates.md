# End-to-End (UI E2E) CI Gates

Story 8.5 (AC5, Option A) closes the genuine environmental gap left after Story 8.4: the `accessibility-gates`
job provisions Playwright Chromium only for the 23-test `Accessibility` subset, leaving the 40 non-accessibility
UI E2E tests (Responsive, Smoke, StateLabels) un-run in any CI lane — so a literal full-solution
`dotnet test Hexalith.Folders.slnx` would still environment-fail on UI.E2E. The stable required status-check name
is `e2e-gates`; branch protection is configured outside this repository, but this job name is intentionally
stable so maintainers can require it.

The workflow is `.github/workflows/ci.yml`. It runs for `pull_request` and pushes to `main`, `next`, `alpha`,
and `beta`. Checkout uses `submodules: false`; the workflow then initializes only the documented root-level
build submodules and never initializes nested submodules recursively:

```text
git submodule update --init references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.Tenants
```

The `e2e-gates` job runs on its own runner — a separate job, so the Playwright Chromium provisioning does not
slow the fast `baseline-build-and-unit-gates` lane. Together with `accessibility-gates` it is one of the two CI
jobs that provision a browser.

## What the gate verifies

The gate runs the **full** `Hexalith.Folders.UI.E2E.Tests` lane — all 63 tests across the four namespaces — so
the full UI E2E surface is proven green in CI rather than assumed:

1. **Accessibility** (23 tests) — the axe-core / WCAG 2.2 AA scan, keyboard / visible-focus, and zoom / reflow
   assertions (also covered by the focused `accessibility-gates` job; re-run here so the full lane is a single
   honest green).
2. **Responsive** (28 cases) — viewport / responsive-layout smoke over the read-only console routes.
3. **Smoke** (8 tests) — route smoke tests asserting the console, folder, provider, audit, and incident routes
   load and render their page roots against the hermetic backend-less host.
4. **StateLabels** (4 tests) — operator disposition-label gallery rendering.

Each test drives a real Playwright Chromium browser against an in-process console host bound to localhost; there
is no external network, provider, secret, or Dapr dependency.

## Honest-green baseline scope

There is **no** CI job that runs a literal full-solution `dotnet test Hexalith.Folders.slnx`. The "honestly green"
MVP baseline is defined as the **union of the focused CI gate lanes** — `baseline-build-and-unit-gates`,
`contract-and-parity-gates`, `security-and-redaction-gates`, `capacity-smoke-gates`, `accessibility-gates`, and
this `e2e-gates` lane. This `e2e-gates` job is the UI.E2E member of that union: it is the lane that exercises the
Playwright-dependent UI E2E tests, which a hermetic unit lane cannot run.

## Gate script

`tests/tools/run-e2e-ci-gates.ps1` provisions Chromium and runs the full `Hexalith.Folders.UI.E2E.Tests` lane:

- `-SkipBrowserInstall`: skip the `tests/install-playwright.ps1` provisioning call (CI provisions Chromium in a
  separate step, then invokes the gate with this switch).
- `-SkipRestoreBuild` / `-NoRestore`: skip the solution restore/build when a shared lane has already built it.

It is a CI contract: no artifact upload, package publish, secrets, network beyond localhost, or recursive
submodule initialization. UI E2E work requires the matching Playwright browser binaries; provision them through
`tests/install-playwright.ps1` (the NuGet package alone is not sufficient).

## Diagnostics

The gate writes a metadata-only report to `_bmad-output/gates/e2e/latest.json`. The report may contain the gate
name, status, exit code, the relative report path, the diagnostic policy, the validation class, and the lane
scope. It must not contain absolute local paths, secrets, tokens, tenant data, provider payloads, raw page HTML,
diffs, or environment dumps.

## Local validation

Run from the repository root (provision the browser once per machine first):

```text
pwsh ./tests/install-playwright.ps1
pwsh ./tests/tools/run-e2e-ci-gates.ps1 -SkipBrowserInstall
```
