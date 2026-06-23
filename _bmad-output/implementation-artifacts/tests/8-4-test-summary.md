# Test Automation Summary — Story 8.4 (Automated axe/WCAG 2.2 AA CI gate)

**Workflow:** `bmad-qa-generate-e2e-tests`
**Story:** `_bmad-output/implementation-artifacts/8-4-stand-up-axe-wcag-ci-gate.md`
**Date:** 2026-06-23
**Engineer:** QA automation (Jerome)
**Framework detected:** xUnit v3 `3.2.2` + Microsoft.Playwright `1.60.0` (.NET 10) + Shouldly `4.3.0` + `Deque.AxeCore.Playwright` `4.11.3` + NSubstitute (stub `IClient`). Used the project's existing E2E lane (`tests/Hexalith.Folders.UI.E2E.Tests/Accessibility/`). No new product API in this story (CI/test-infra closure story) → **E2E-only pass, no API tests in scope.**

## Scope

Story 8.4 was already implemented and in `review`: the gate *is* the test surface (axe scan + keyboard/visible-focus + zoom/no-clipping over the read-only operations console, run against a populated stub-`IClient` host). This run was a **coverage-gap audit** of ACs 1–3 against the three critical journeys (J1/J2/J3) and their 8 distinct routes, auto-applying discovered gaps (per the request). ACs 4–5 are CI-wiring / conformance / docs — not E2E targets.

## Coverage matrix at start of pass (baseline 17 tests)

| Route | axe (AC1) | keyboard/focus (AC2) | zoom/no-clip (AC3) |
|---|---|---|---|
| folders | ✅ | ✅ | — (unpopulated list; covered by responsive smoke) |
| folder-detail | ✅ | ✅ | ✅ |
| workspace | ✅ | **❌ gap** | ✅ |
| provider-support | ✅ | **❌ gap** | ✅ |
| provider | ✅ | **❌ gap** | **❌ gap** |
| audit-trail | ✅ | ✅ | ✅ |
| operation-timeline | ✅ | **❌ gap** | ✅ |
| incident-stream | ✅ | **❌ gap** | ✅ |

axe (AC1) already covered all 8 routes; the gaps were in the two complementary-assertion dimensions that make the gate a UNION (AD2).

## Gaps discovered & auto-applied

| Gap | Evidence | Fix |
|---|---|---|
| **AC2 keyboard-operability / visible-focus covered only 3 of 8 journey routes.** `ConsoleKeyboardFocusGateTests` asserted only the three journey *entry* routes (folders, folder-detail, audit-trail). UX-DR30 names "tabs" and the timeline/provider tables as operable controls, but **workspace, provider-support, provider, operation-timeline, incident-stream** had no keyboard-reachability / focus-visible assertion. | An empirical keyboard probe (temporary diagnostic, since removed) tabbed through each of the 5 routes against the populated host and confirmed **every one exposes ≥1 keyboard-reachable console-content control, all already showing a visible focus indicator** (workspace 5, incident-stream 4, operation-timeline 2, provider-support 1, provider 1). So the invariant genuinely applies to all of them — not a forced red. | Extended `ConsoleKeyboardFocusGateTests` theory from **3 → 8 `[InlineData]` routes** (full journey coverage) + the 5 matching `ResolveRoute` cases + class-doc update. |
| **AC3 zoom / no-horizontal-clipping (UX-DR31) omitted the `provider` route.** `ConsoleZoomReflowGateTests` covered 6 surfaces but not folder-scoped provider readiness, even though it is axe-scanned, populated (`console-page-provider-section-identity`), and carries the dense binding refs (`DenseProviderBindingRef`, `DenseRepoBindingId`) the dense host seeds. | `provider` is the only **populated** journey route absent from the UX-DR31 sweep; the dense identifiers route through its identity section. | Added `provider` to the `TerminalSurfaces` theory (**6 → 7**) + the matching `ResolveRoute` case. Verified the no-clipping + 200 % reflow invariants hold for it. |

### Coverage already adequate (verified, no change — avoided over-engineering)

- **AC1 (axe scan):** all 8 journey routes scanned with the cumulative AA tag set (`wcag2a, wcag2aa, wcag21a, wcag21aa, wcag22aa`), failing on any AA-tagged violation, scoped to the console page-content root (FrontComposer shell-chrome false positive, AD3). No route gap.
- **`folders` route excluded from zoom (deliberate non-gap):** the folders discovery list is not populated by the stub `IClient` (no list method) so it renders no dense rows on this host; its responsive presence is already covered at 4 widths by `ResponsiveViewportSmokeTests`. Adding it to the dense zoom sweep would assert nothing meaningful.
- **not-color-alone / structural (AC2 union):** owned by the already-green bUnit `AccessibilityContractSweepTests` (surfaced as gate evidence, AD2) — not re-implemented in the E2E lane.
- **No new fixtures/hosts needed:** the existing `AccessibilityConsoleHostFixture` (happy-path) and `DenseIdentifierConsoleHostFixture` (dense) already render every added route populated.

## Generated / extended Tests

### E2E Tests (`tests/Hexalith.Folders.UI.E2E.Tests/Accessibility/`)
- [x] `ConsoleKeyboardFocusGateTests.cs` — keyboard-operability + visible-focus extended from **3 → 8** routes (added workspace, provider-support, provider, operation-timeline, incident-stream).
- [x] `ConsoleZoomReflowGateTests.cs` — UX-DR31 zoom/no-clip extended from **6 → 7** surfaces (added provider).

(No fixture, gate-script, CI-workflow, or conformance-test change — the additions reuse the existing hosts, route contract, and `data-testid` selectors.)

## Coverage

- **AC2 keyboard/visible-focus:** journey-route coverage **8/8** (was 3/8).
- **AC3 zoom/no-clip:** populated-journey-route coverage **7/7** (was 6/7; `folders` intentionally excluded as unpopulated).
- **AC1 axe scan:** **8/8** (unchanged — already complete).
- Accessibility namespace: **23 tests** (was 17 → +6). Full UI E2E lane: **63/63** (was 57 → +6).

## Validation run

```
dotnet build tests/Hexalith.Folders.UI.E2E.Tests          → 0W / 0E
dotnet format whitespace --verify-no-changes (E2E project) → clean (exit 0)
dotnet format analyzers  --verify-no-changes (E2E project) → clean (exit 0)
dotnet test  Accessibility filter                          → Passed! 23/0 (was 17)
dotnet test  full UI E2E lane                              → Passed! 63/63 (was 57; no regressions)
pwsh tests/tools/run-accessibility-ci-gates.ps1 -SkipRestoreBuild -SkipBrowserInstall
                                                           → report status "passed", exit 0
```

(Browser provisioning uses the cached Chromium with `-SkipBrowserInstall`, matching the CI flow and the story's Debug-Log note that the installer cannot fetch a fresh Chromium on this host.)

## Next Steps

- Runs in CI under the existing `accessibility-gates` job (no workflow change needed — the new tests live in the already-scanned `Accessibility` namespace).
- If a future journey adds a new route to `ConsoleRoutes`, add it to all three theories (axe / keyboard-focus / zoom) to keep the three dimensions in lockstep across the journey set.
- The genuinely-manual residuals (screen-reader review, forced-colors, color-blindness) remain owned `reference_pending` rows per AD2 — the axe gate does not automate them.
