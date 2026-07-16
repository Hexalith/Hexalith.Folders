# Test Automation Summary — Story 6.11 (Verify no-mutation enforcement and accessibility)

**Workflow:** `bmad-qa-generate-e2e-tests` · **Role:** QA automation engineer (test generation only) · **Date:** 2026-05-29
**Story:** `_bmad-output/implementation-artifacts/6-11-verify-no-mutation-enforcement-and-accessibility.md` (Status: review)
**Feature under test:** Operations console — no-mutation enforcement + WCAG 2.2 AA + responsive verification.
**Framework (existing, reused — no new package):** xUnit v3 `3.2.2` + bUnit `2.7.2` + AngleSharp + Shouldly +
NSubstitute (component lane); `Microsoft.Playwright 1.60.0` (E2E lane). Build/test with the **Windows SDK**
(`/mnt/c/Program Files/dotnet/dotnet.exe`, `global.json` pin `10.0.300`); the WSL SDK `10.0.108` fails the pin.

## Approach

This run was a **gap fill**, not green-field generation. Story 6.11's dev pass shipped the no-mutation +
WCAG-structural bUnit sweeps (baseline **519/519** green) but left **Task 3 / AC #8** — the *optional* Playwright
responsive-viewport smoke — **unautomated** (recorded as documented-manual `reference_pending` because no
browser binary was provisioned). The qa-generate-e2e-tests workflow exists precisely to fill that E2E gap; all
discovered gaps were auto-applied.

No separate HTTP/API test layer exists for this feature — the console reads projections through the generated
SDK `IClient`. Coverage is therefore **bUnit** (rendering + invariants) and **Playwright E2E smoke** (route load
+ responsive structure), matching the project's existing patterns.

## Generated / changed tests

### E2E (Playwright) — `tests/Hexalith.Folders.UI.E2E.Tests`
- [x] `Responsive/ResponsiveViewportSmokeTests.cs` (new) — non-brittle responsive smoke: **7 surface routes × 4
  viewport widths** (desktop 1280, tablet 768, mobile-fallback 430 / 360) = **28 cases**. Per case asserts the
  page **root resolves**, exactly **one `<h1>`**, and the **read-only boundary** holds (zero `form` /
  `fluent-dialog` / `[data-fc-command]` / `[data-fc-mutation]`). Presence/structure only — **no**
  pixel/overflow/CSS/text/sleep assertions (project-context). Reuses `AspireConsoleHostFixture` +
  `PlaywrightFixture` + `ConsoleRoutes.cs`; modelled on `StateLabelGalleryE2ETests`.

### bUnit — `tests/Hexalith.Folders.UI.Tests` (regression guards for the defect below)
- [x] `WorkspacePageTests.TransportFailure_RendersReadModelUnavailable`
- [x] `FolderDetailPageTests.TransportFailure_RendersReadModelUnavailable`
  Both assert the §3.8 read-model-unavailable degradation on `HttpRequestException`, mirroring the existing
  `AuditTrailPageTests` precedent. The degradation contract is now asserted uniformly across all **7** SDK-backed
  pages (was 5).

### Test fixture (fixed) — `tests/Hexalith.Folders.UI.E2E.Tests/Fixtures/AspireConsoleHostFixture.cs`
- [x] Now configures a valid-but-unreachable SDK base address (`http://127.0.0.1:1/`) so SDK reads fail as the
  realistic `HttpRequestException` (caught → §3.8 degradation) rather than an unconfigured-base-address
  `InvalidOperationException` (uncaught → 500). This also unblocked the previously-red pre-existing smoke tests.

## Defect found & fixed (production — user-approved scope decision)

The E2E smoke against the hermetic (backend-less) host surfaced a genuine availability defect: **`Workspace` and
`FolderDetail` did not catch `HttpRequestException` / `TaskCanceledException`** on their SDK reads — unlike the
five sibling diagnostic pages — so an **unreachable read model crashed those pages to HTTP 500** instead of
degrading to the §3.8 read-model-unavailable state (operator-facing, esp. under F-6 incident mode). **Fix
(minimal, scoped, mirrored on `AuditTrail`):**
- [x] `src/Hexalith.Folders.UI/Components/Pages/Workspace.razor.cs`
- [x] `src/Hexalith.Folders.UI/Components/Pages/FolderDetail.razor`

Recorded in the release-validation evidence doc (`docs/ux/ops-console-accessibility-and-no-mutation-verification.md`
§7, §12, §13) and the UX-DR31 traceability row.

## Results (Windows SDK, actually run)

| Lane | Before | After | Δ | Status |
| --- | --- | --- | --- | --- |
| bUnit `Hexalith.Folders.UI.Tests` | 519 / 519 | **521 / 521** | +2 regression guards | ✅ 0 failed, 0 warnings |
| E2E `Hexalith.Folders.UI.E2E.Tests` | red (SDK routes 500) | **40 / 40** | +28 responsive cases; +12 pre-existing unblocked | ✅ 0 failed |

## Coverage

| Surface | Before | After | Note |
|---|---|---|---|
| No-mutation / WCAG-structural (bUnit, 12 surfaces) | covered | covered | dev pass, still green |
| Responsive viewport structural (E2E, 7 routes × 4 widths) | untested | **covered** | AC #8 / Task 3 gap filled |
| Read-model-unavailable degradation (bUnit, SDK pages) | 5 of 7 | **7 of 7** | uniform contract |
| Manual (zoom / forced-colors / color-blindness / screen-reader / real-width visual) | reference_pending | reference_pending | method defined; no tool on graph |

## Verification & scope guards

- bUnit: **521 passed / 0 failed / 0 skipped**; E2E: **40 passed / 0 failed**. Build clean (warnings-as-error).
- `git diff --stat` empty for `src/Hexalith.Folders.Client/Generated/`, `Directory.Packages.props`, the OpenAPI
  spine, all `*.csproj`/`*.slnx`; **no** new `<PackageReference>`/`<ProjectReference>`; **no** `.razor.css`.
- `NavigationContractTests.Console_DoesNotRegisterAnyDomainCommandManifest` passes within the 521; the two
  pre-existing `IntegrationTests`/`LoadTests` `ScaffoldContractTests` reds are unrelated baseline reds.

## Next steps

- Wire the deferred-active UI E2E lane into a non-blocking CI stage with `tests/install-playwright.ps1`
  provisioning (the bUnit lane carries the always-run regression guards today).
- Release-time human passes for the `reference_pending` manual checks per the evidence-doc method.

## Checklist validation (`.agents/skills/bmad-qa-generate-e2e-tests/checklist.md`)

- **Test Generation** — API tests **N/A** (SDK-`IClient`-backed UI, no new endpoint); E2E generated ✅; standard
  framework APIs (xUnit/bUnit/Playwright/Shouldly) ✅; happy path (route loads, root + h1 across widths) ✅;
  critical error cases (transport-failure → read-model-unavailable on Workspace + FolderDetail) ✅.
- **Test Quality** — all run successfully (bUnit 521/521, E2E 40/40) ✅; semantic/accessible locators
  (`data-testid`, `data-fc-*`, role-free presence) ✅; clear descriptions ✅; **no** hardcoded waits/sleeps
  (Playwright auto-wait `WaitForAsync`, bUnit `WaitForAssertion`) ✅; tests independent (fresh context/page per
  case) ✅.
- **Output** — summary created (this file + canonical `test-summary.md`) ✅; tests saved to appropriate
  directories ✅; coverage metrics included ✅.
- **Validation** — project test command run; **all generated tests pass** ✅.
