# Test Automation Summary — Story 6.9 (Incident-mode last-resort read path)

**Workflow:** `bmad-qa-generate-e2e-tests` · **Role:** QA automation engineer (test generation only — no code review / story validation).
**Story:** `_bmad-output/implementation-artifacts/6-9-implement-incident-mode-last-resort-read-path.md` (Status: review)
**Feature under test:** F-6 incident-mode last-resort read path — `IncidentStream` page + `DegradedModeBanner` + `CorrelationCopyButton`.
**Date:** 2026-05-29 · **SDK:** Windows `/mnt/c/Program Files/dotnet/dotnet.exe` (per `global.json` 10.0.300 pin).

## Approach

Story 6.9 already shipped a strong suite (baseline **446/446** green). This run was a **gap fill**, not green-field generation. A review of the existing 6.9 tests against the 15 acceptance criteria and the near-identical `OperationTimeline` template (Story 6.8) surfaced **8 coverage gaps**; all were confirmed against the implementation source and **auto-applied**.

No separate HTTP/API test layer exists for this feature — the console reads projections through the generated SDK `IClient`. Coverage is therefore **bUnit page/component tests** (rendering + invariants) plus the **Playwright E2E route smoke** (deferred lane), matching the project's existing patterns (`DiagnosticTestContext`, `ShouldHaveNoMutationAffordances`, `ConsoleRoutes`, `*RouteSmokeTests`).

## Gaps Discovered & Tests Added (8 new)

### bUnit page tests — `IncidentStreamPageTests.cs` (+7 → 17→24 facts)

| Test | Gap closed | AC |
|------|------------|----|
| `WhilePrimaryReadPending_RendersLoadingBusyIndicator_BannerStillPresent` | The `aria-busy` loading branch (`console-page-incident-stream-loading`) was entirely untested; the 6.8 template had it, 6.9 did not. | AC #14 |
| `StaleFreshness_RendersStaleInFooter_NotCurrent` | The evidence-freshness **footer** (`FreshnessLabel()` / `console-page-incident-stream-freshness`) was untested — only the banner checkpoint was. Recurring 6.7/6.8 freshness-honesty review risk. | AC #2 |
| `PresentFreshness_RendersRealCheckpointInBanner_NotUnknown` | Only the *absent* checkpoint ("unknown") was asserted; the happy-path real UTC checkpoint in the banner was never asserted. | AC #2 |
| `MultipleEntries_RenderOneRowEach_NoClientSideHiding` | No-hiding / multi-row rendering untested; the template had it. | AC #10 |
| `CorrelationCopyButton_ReceivesOldestToNewestTimeWindow_FromVisibleEntries` | The page's `TimeWindowLabel()` min/max-over-entries logic (oldest..newest) was untested at page level. | AC #5 |
| `BlankWorkspaceMetadataOnly_RendersMissing_DistinctFromRedacted` | Missing-vs-Redacted distinctness for a blank workspace value was untested; the template had it. | AC #6 |
| `SupplementaryPermissionsDenied_IsSwallowed_TableStillRenders` | The swallow-denial `TryReadAsync` path (supplementary permissions read fails, primary read succeeds → page still renders, no error panel) was untested. | AC #7 |

### bUnit component tests — `CorrelationCopyButtonTests.cs` (+1 → 3→4 facts)

| Test | Gap closed | AC |
|------|------------|----|
| `RendersHumanLabel_NotTheRawPayload_InTheDom` | No-leak guard: the composed `correlationId=…; window=…` payload reaches the clipboard via JS only and must never be rendered into the DOM (a short human label is shown instead). | AC #5 / AC #12 |

### E2E (Playwright) — `tests/Hexalith.Folders.UI.E2E.Tests/Smoke`

- `IncidentRouteSmokeTests.cs` — incident-route smoke (already shipped by dev-story: load 200–399, page root visible, single `<h1>`). **No gap found** — the one-smoke-per-route convention (6.6–6.8) is already satisfied.

## Coverage

| Surface | Before | After | Note |
|---|---|---|---|
| UI bUnit lane (`Hexalith.Folders.UI.Tests`) | 446 | **454** | +8, all green |
| `IncidentStream` loading branch (`aria-busy`) | 0/1 | **1/1** | AC #14 |
| Incident freshness footer (stale / present checkpoint) | 0 | covered | AC #2 / UX-DR26, both ends anchored |
| Multi-row / no-client-side-hiding | untested | covered | AC #10 |
| `TimeWindowLabel()` oldest..newest window | untested | covered | AC #5 |
| Missing ≠ Redacted distinctness (blank workspace) | untested | covered | AC #6 |
| Swallow-denial `TryReadAsync` permissions path | untested | covered | AC #7 |
| `CorrelationCopyButton` no-leak DOM guard | untested | covered | AC #5 / AC #12 |
| Incident E2E smoke route | 1 | 1 | already covered (deferred lane) |

## Verification

- `dotnet test tests/Hexalith.Folders.UI.Tests` → **Passed: 454, Failed: 0, Skipped: 0** (baseline 446 + 8 gap tests). Windows SDK 10.0.300.
- Forbidden-touch verification empty: `src/Hexalith.Folders.Client/Generated/`, `src/Hexalith.Folders.Client/Compat/`, `Directory.Packages.props`, `*.csproj`, `*.slnx`, OpenAPI spine. No new `<PackageReference>`/`<ProjectReference>` — only existing test files extended.
- `NavigationContractTests.Console_DoesNotRegisterAnyDomainCommandManifest` passes within the 454.
- Pre-existing IntegrationTests/LoadTests `ScaffoldContractTests` reds are unrelated to the UI lane and were not touched (documented baseline — see `scaffold-contract-tests-baseline-reds`).

## Files Modified This Run

- `tests/Hexalith.Folders.UI.Tests/IncidentStreamPageTests.cs` (+7 facts; added `using Hexalith.Folders.UI.Components;`)
- `tests/Hexalith.Folders.UI.Tests/CorrelationCopyButtonTests.cs` (+1 fact)

## Next Steps

- Run the deferred Playwright E2E lane (`tests/install-playwright.ps1` + live Aspire host) to execute the incident-route smoke; it is build-verified but not executed in the headless dev loop.
- Story 6.11 owns the formal no-mutation + WCAG 2.2 AA verification sweep; these tests build defensively toward it but do not replace that matrix.
- No further gaps identified for Story 6.9.

## Checklist validation (`.agents/skills/bmad-qa-generate-e2e-tests/checklist.md`)

- **Test Generation** — API tests: **N/A** (no separate API layer; SDK-`IClient`-backed UI); E2E/bUnit generated ✅; standard framework APIs (xUnit v3 / bUnit / Shouldly / NSubstitute / Playwright) ✅; happy path ✅; critical error cases (denied, transport failure, cancellation, missing/redacted, swallowed-permission-denial) ✅.
- **Test Quality** — all run successfully (454/454) ✅; semantic/accessible locators (`data-testid`, `role`, `data-fc-disclosure`) ✅; clear descriptions ✅; no hardcoded waits/sleeps (`WaitForAssertion`) ✅; tests independent (each builds its own `DiagnosticTestContext`/`BadgeRenderingFixture`) ✅.
- **Output** — summary created ✅; tests saved to appropriate directories ✅; coverage metrics included ✅.
