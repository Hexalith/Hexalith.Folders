# Hexalith.Folders UI End-to-End Tests

This project hosts the **Playwright-on-.NET** lane for the read-only operations console. It compiles and is part of the solution today, but contains only a skipped placeholder test until Epic 6 ships the operations console scaffold (story 6-2).

## Status

- **Lane:** deferred-active. The project is built and discovered by `dotnet test`, but every test is `[Fact(Skip = "...")]` until UI routes and selectors stabilize.
- **Blocking prerequisite:** Epic 6, story 6-2 (`scaffold-frontcomposer-hosted-read-only-operations-console`).
- **Risk if enabled early:** flakiness debt against a moving target. Do not write E2E tests against routes that have not landed in `main`.

## When to enable

Replace the placeholder smoke test only after **all** of the following are true:

1. Story 6-2 has merged a working operations console host into `main`.
2. The console exposes at least one stable route with documented intent.
3. The console exposes `data-testid` attributes on the elements being asserted (see Route and Selector Contract below).
4. A host fixture exists that stands up the console deterministically through `Aspire.Hosting.Testing` or equivalent — no test may target a hand-started `dotnet run` process.

## Route and Selector Contract

E2E tests under this project must follow the contract below. Any deviation is a contract bug and should be fixed in the UI, not worked around in the test.

### Selectors

- **`data-testid` only** for E2E-targeted elements. CSS classes, nth-child, and text content are forbidden as primary selectors.
- `data-testid` values are kebab-case, scoped by region: `console-nav-{area}`, `console-page-{name}-root`, `console-action-{verb}-{noun}`, `console-status-{kind}`, `console-table-{entity}-row`, `console-table-{entity}-cell-{column}`.
- Selectors are part of the UI contract. Removing or renaming one requires a paired test update in the same change.

### Routes

- Routes are documented as part of story 6-2 deliverables. Until that doc exists, no route may be hardcoded in tests except through the host fixture's `BaseAddress` plus a path constant defined in this project.
- Path constants live in a future `Routes/ConsoleRoutes.cs` (do not create until the first real test needs it).
- Tests must not rely on undocumented redirects, default landing pages, or environment-specific routing tables.

### Network discipline

- **Intercept-before-navigate** for any request the test asserts on. Use `IBrowserContext.RouteAsync` before `IPage.GotoAsync` — never after.
- The operations console is **read-only** in MVP. Tests must not exercise mutation paths; if a test appears to mutate, it is testing the wrong thing.
- Capture HAR for failed runs only. Long-running HAR captures inflate flake debt and leak metadata.

### Wait discipline

- Use Playwright's built-in auto-waiting through locators and `expect(...).ToBeVisibleAsync()` style assertions.
- Never use `Task.Delay`, `Thread.Sleep`, or arbitrary timeouts.
- For eventual-consistency assertions across the backend, prefer the existing `Hexalith.Folders.Testing.Polling.Eventually` helper over Playwright-level waits.

### Accessibility

- Every page-level smoke test must include an accessibility scan. The scanning library (`Deque.AxeCore.Playwright` or successor) is selected when the first real test lands.
- Accessibility findings of severity `serious` or `critical` fail the test.

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

The placeholder smoke test currently reports as skipped — that is expected.

## CI posture

- This lane is **not** in the blocking CI gate today. Wiring it in is part of Epic 6 / Epic 7 work, not Epic 1-3.
- When wired, it must run against an Aspire-managed host, headless Chromium, and a tight burn-in budget (trace + screenshot + video on failure only).
- A failing UI E2E test must never be silently retried more than the Playwright default (typically 2 in CI). Retries hide flake; flake is critical technical debt.

## Project layout

```text
tests/Hexalith.Folders.UI.E2E.Tests/
├── Fixtures/
│   ├── PlaywrightCollection.cs          # xUnit collection definition
│   └── PlaywrightFixture.cs             # IPlaywright + IBrowser lifecycle
├── Smoke/
│   └── OperationsConsolePlaceholderSmokeTests.cs   # skipped until Epic 6
└── README.md                            # this file
```

Add new directories (`Routes/`, `Pages/`, `Accessibility/`, `Hosting/`) when the first real test in that area lands. Do not create empty placeholder folders.

## Knowledge references

- BMAD TEA fragments: `selector-resilience`, `network-first`, `playwright-config`, `visual-debugging`, `webhook-testing-fundamentals` (for any future asynchronous event-driven panels), `test-quality`, `risk-governance`, `confidence-gate`.
- Microsoft Learn: Playwright for .NET documentation (current).
- Project context: `_bmad-output/project-context.md` — operations console is read-only in MVP; redacted fields must be visibly distinct from unknown/missing fields; never assert on sensitive payloads.
