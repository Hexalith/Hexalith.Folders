# Hexalith.Folders UI End-to-End Tests

This project hosts the **Playwright-on-.NET** lane for the read-only operations console. It is the live 63-test blocking lane documented in `docs/operations/e2e-ci-gates.md`.

## Status

- **Lane:** live and blocking. 63 Playwright tests across Accessibility (23), Responsive (28), Smoke (8), and StateLabels (4).
- **CI:** required as `e2e-gates` (full lane) and `accessibility-gates` (the 23-test Accessibility subset). See `docs/operations/e2e-ci-gates.md`.
- **Host:** in-process console host on localhost; provision Chromium with `tests/install-playwright.ps1`. There is no external network, provider, secret, or Dapr dependency.

## What the lane covers

The full `Hexalith.Folders.UI.E2E.Tests` surface — all 63 tests across the four namespaces:

1. **Accessibility** (23 tests) — the axe-core / WCAG 2.2 AA scan, keyboard / visible-focus, and zoom / reflow assertions (also covered by the focused `accessibility-gates` job; re-run in `e2e-gates` so the full lane is a single honest green).
2. **Responsive** (28 cases) — viewport / responsive-layout smoke over the read-only console routes.
3. **Smoke** (8 tests) — route smoke tests asserting the console, folder, provider, audit, and incident routes load and render their page roots against the hermetic backend-less host.
4. **StateLabels** (4 tests) — operator disposition-label gallery rendering.

Each test drives a real Playwright Chromium browser against an in-process console host bound to localhost.

## Route and Selector Contract

E2E tests under this project must follow the contract below. Any deviation is a contract bug and should be fixed in the UI, not worked around in the test.

### Selectors

- **`data-testid` only** for E2E-targeted elements. CSS classes, nth-child, and text content are forbidden as primary selectors.
- `data-testid` values are kebab-case, scoped by region: `console-nav-{area}`, `console-page-{name}-root`, `console-action-{verb}-{noun}`, `console-status-{kind}`, `console-table-{entity}-row`, `console-table-{entity}-cell-{column}`.
- Selectors are part of the UI contract. Removing or renaming one requires a paired test update in the same change.

### Routes

- No route may be hardcoded in tests except through the host fixture's `BaseAddress` plus a path constant defined in this project.
- Path constants live in `Routes/ConsoleRoutes.cs`.
- Tests must not rely on undocumented redirects, default landing pages, or environment-specific routing tables.

### Hosting

- A host fixture must stand the console up deterministically. No test may target a hand-started `dotnet run` process.

### Network discipline

- **Intercept-before-navigate** for any request the test asserts on. Use `IBrowserContext.RouteAsync` before `IPage.GotoAsync` — never after.
- The operations console is **read-only** in MVP. Tests must not exercise mutation paths; if a test appears to mutate, it is testing the wrong thing.
- Capture HAR for failed runs only. Long-running HAR captures inflate flake debt and leak metadata.

### Wait discipline

- Use Playwright's built-in auto-waiting through locators and `expect(...).ToBeVisibleAsync()` style assertions.
- Never use `Task.Delay`, `Thread.Sleep`, or arbitrary timeouts.
- For eventual-consistency assertions across the backend, prefer the existing `Hexalith.Folders.Testing.Polling.Eventually` helper over Playwright-level waits.

### Accessibility

- The console journeys carry an axe-core / WCAG 2.2 AA scan (`Deque.AxeCore.Playwright`, Story 8.4) under the `Accessibility/` namespace, run against a populated stub-`IClient` host.
- axe is filtered to the cumulative WCAG AA tag set (`wcag2a, wcag2aa, wcag21a, wcag21aa, wcag22aa`) and the test fails on **any** AA-tagged violation (Story 8.4 AD3 — stricter than the earlier `serious`/`critical` intent). Failure output is metadata-only (rule id + target selector + helpUrl, never raw HTML).
- The gate is a union: the axe scan plus explicit Playwright keyboard-operability / visible-focus and zoom / no-clipping assertions, complemented by the bUnit `AccessibilityContractSweepTests` not-color-alone sweeps.

### Redaction

- Tests must never assert on raw credentials, tokens, file contents, diffs, or generated context payloads. The operations console is metadata-only by design; if such content is visible in any page, that is a redaction bug — open a story, do not encode the leak in a test.

## Bootstrap

Run once per developer machine after first build:

```powershell
pwsh tests\install-playwright.ps1
```

The script builds this project to materialize the Playwright runtime, then invokes the generated `playwright.ps1` to install the Chromium browser. Other browsers are not in scope for MVP.

## Local execution

```powershell
.\tests\run-tests.ps1 -Mode UiE2E
```

Or directly:

```powershell
dotnet test tests\Hexalith.Folders.UI.E2E.Tests\Hexalith.Folders.UI.E2E.Tests.csproj
```

CI-equivalent local gates (from the repository root, once Bootstrap above has provisioned Chromium). Both
CI jobs have a script; run both to reproduce the blocking lane:

```powershell
pwsh tests\tools\run-e2e-ci-gates.ps1 -SkipBrowserInstall
pwsh tests\tools\run-accessibility-ci-gates.ps1 -SkipBrowserInstall
```

## CI posture

- This lane is **blocking**. `.github/workflows/ci.yml` runs `e2e-gates` (all 63 tests) and `accessibility-gates` (the 23-test Accessibility subset). Both jobs provision Playwright Chromium. See `docs/operations/e2e-ci-gates.md`.
- Tests run against an in-process console host and headless Chromium, under the same hermetic posture as the Status section above.
- A failing UI E2E test must never be silently retried more than the Playwright default (typically 2 in CI). Retries hide flake; flake is critical technical debt.

## Project layout

```text
tests/Hexalith.Folders.UI.E2E.Tests/
├── Accessibility/                       # 23-test WCAG / keyboard / zoom subset
├── Fixtures/
│   ├── AccessibilityConsoleHostFixture.cs
│   ├── AspireConsoleHostFixture.cs
│   ├── ConsoleStubFixtures.cs
│   ├── DenseIdentifierConsoleHostFixture.cs
│   ├── PlaywrightCollection.cs          # xUnit collection definition
│   ├── PlaywrightFixture.cs             # IPlaywright + IBrowser lifecycle
│   └── PopulatedConsoleHostFixture.cs
├── Responsive/                          # 28 viewport / layout cases
├── Routes/
│   └── ConsoleRoutes.cs                 # path constants for the operations console
├── Smoke/                               # 8 route-root smoke tests
├── StateLabels/                         # 4 disposition-label gallery tests
└── README.md                            # this file
```

Do not create empty placeholder folders; add a directory when the first real test in that area lands.

## Knowledge references

- BMAD TEA fragments: `selector-resilience`, `network-first`, `playwright-config`, `visual-debugging`, `webhook-testing-fundamentals` (for any future asynchronous event-driven panels), `test-quality`, `risk-governance`, `confidence-gate`.
- Microsoft Learn: Playwright for .NET documentation (current).
- Project context: `_bmad-output/project-context.md` — operations console is read-only in MVP; redacted fields must be visibly distinct from unknown/missing fields; never assert on sensitive payloads.
