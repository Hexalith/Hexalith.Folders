# Test Automation Summary — Story 6.11 (Verify no-mutation enforcement and accessibility)

> Canonical latest-run summary (per `bmad-qa-generate-e2e-tests` `default_output_file`). Durable per-story copy: [`6-11-test-summary.md`](./6-11-test-summary.md).

**Workflow:** `bmad-qa-generate-e2e-tests` · **Role:** QA automation engineer (test generation only) · **Date:** 2026-05-29
**Story:** `_bmad-output/implementation-artifacts/6-11-verify-no-mutation-enforcement-and-accessibility.md` (Status: review)
**Feature under test:** Operations console — no-mutation enforcement + WCAG 2.2 AA + responsive verification.
**Framework (existing, reused — no new package):** xUnit v3 `3.2.2` + bUnit `2.7.2` + AngleSharp + Shouldly +
NSubstitute (component lane); `Microsoft.Playwright 1.60.0` (E2E lane). Build/test with the **Windows SDK**
(`/mnt/c/Program Files/dotnet/dotnet.exe`, `global.json` pin `10.0.300`); the WSL SDK `10.0.108` fails the pin.

## Approach

Gap fill, not green-field generation. Story 6.11's dev pass shipped the no-mutation + WCAG-structural bUnit
sweeps (baseline **519/519**) but left **Task 3 / AC #8** — the *optional* Playwright responsive-viewport smoke
— **unautomated** (`reference_pending`, no browser provisioned). This run automated it and auto-applied every
discovered gap. Console reads go through the generated SDK `IClient`, so there is no separate HTTP/API layer —
coverage is bUnit (rendering/invariants) + Playwright E2E smoke (route load + responsive structure).

## Generated / changed tests

### E2E (Playwright) — `tests/Hexalith.Folders.UI.E2E.Tests`
- [x] `Responsive/ResponsiveViewportSmokeTests.cs` (new) — **7 surface routes × 4 widths** (1280 / 768 / 430 /
  360) = **28 cases**; per case: root resolves + single `<h1>` + zero mutation affordances. Presence/structure
  only — no pixel/overflow/CSS/text/sleep. Reuses `AspireConsoleHostFixture` + `PlaywrightFixture` +
  `ConsoleRoutes.cs`.

### bUnit — `tests/Hexalith.Folders.UI.Tests` (regression guards)
- [x] `WorkspacePageTests.TransportFailure_RendersReadModelUnavailable`
- [x] `FolderDetailPageTests.TransportFailure_RendersReadModelUnavailable`
  §3.8 read-model-unavailable degradation on `HttpRequestException`, mirroring `AuditTrailPageTests`. Contract
  now uniform across all **7** SDK-backed pages.

### Test fixture (fixed)
- [x] `tests/.../Fixtures/AspireConsoleHostFixture.cs` — configures a valid-but-unreachable SDK base address so
  reads fail as `HttpRequestException` (caught → §3.8) not an unconfigured-base-address `InvalidOperationException`
  (uncaught → 500); unblocked the previously-red pre-existing smoke tests.

## Defect found & fixed (production — user-approved)

`Workspace` + `FolderDetail` crashed (HTTP 500) on an unreachable read model instead of degrading to §3.8 (they
didn't catch `HttpRequestException`/`TaskCanceledException`, unlike the 5 sibling pages). Fixed minimally,
mirrored on `AuditTrail`:
- [x] `src/Hexalith.Folders.UI/Components/Pages/Workspace.razor.cs`
- [x] `src/Hexalith.Folders.UI/Components/Pages/FolderDetail.razor`

Recorded in `docs/ux/ops-console-accessibility-and-no-mutation-verification.md` (§7, §12, §13) + UX-DR31 row.

## Results (Windows SDK, actually run)

| Lane | Before | After | Δ | Status |
| --- | --- | --- | --- | --- |
| bUnit `Hexalith.Folders.UI.Tests` | 519 / 519 | **521 / 521** | +2 | ✅ 0 failed, 0 warnings |
| E2E `Hexalith.Folders.UI.E2E.Tests` | red (SDK routes 500) | **40 / 40** | +28 new; +12 unblocked | ✅ 0 failed |

## Coverage

| Surface | Before | After |
|---|---|---|
| No-mutation / WCAG-structural (bUnit, 12 surfaces) | covered | covered |
| Responsive viewport structural (E2E, 7 routes × 4 widths) | untested | **covered** |
| Read-model-unavailable degradation (bUnit, SDK pages) | 5 of 7 | **7 of 7** |
| Manual (zoom / forced-colors / color-blindness / screen-reader / real-width visual) | reference_pending | reference_pending (method defined) |

## Verification & scope guards

- bUnit **521/521**, E2E **40/40**; build clean (warnings-as-error). `git diff --stat` empty for
  `Generated/`, `Directory.Packages.props`, OpenAPI spine, `*.csproj`/`*.slnx`; no new package/project refs; no
  `.razor.css`. `Console_DoesNotRegisterAnyDomainCommandManifest` green; the two `ScaffoldContractTests`
  baseline reds are unrelated.

## Next steps

- Wire the deferred-active UI E2E lane into non-blocking CI with `tests/install-playwright.ps1` provisioning.
- Release-time human passes for the `reference_pending` manual checks per the evidence-doc method.

## Checklist validation (`.agents/skills/bmad-qa-generate-e2e-tests/checklist.md`)

- **Test Generation** — API tests **N/A** (SDK-backed UI); E2E generated ✅; standard framework APIs ✅; happy
  path ✅; critical error case (transport-failure → read-model-unavailable) ✅.
- **Test Quality** — all run successfully (521/521 + 40/40) ✅; semantic/accessible locators ✅; clear
  descriptions ✅; no hardcoded waits/sleeps (auto-wait) ✅; tests independent ✅.
- **Output** — summary created ✅; tests saved to appropriate directories ✅; coverage metrics included ✅.
- **Validation** — project test command run; all generated tests pass ✅.
