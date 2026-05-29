# Test Automation Summary — Story 6.9 (Incident-mode last-resort read path)

> Canonical latest-run summary (per `bmad-qa-generate-e2e-tests` `default_output_file`). Durable per-story copy: [`6-9-test-summary.md`](./6-9-test-summary.md).

**Workflow:** `bmad-qa-generate-e2e-tests` · **Role:** QA automation engineer (test generation only) · **Date:** 2026-05-29
**Story:** `_bmad-output/implementation-artifacts/6-9-implement-incident-mode-last-resort-read-path.md` (Status: review)
**Feature under test:** the Incident-mode view (§3.5 / F-6) of the operations console — `IncidentStream.razor` plus the `DegradedModeBanner` and `CorrelationCopyButton` components, built on the existing folder-scoped `IClient.ListOperationTimelineAsync` read.

## Approach

Story 6.9 already shipped a strong suite (baseline **446/446** green). This run was a **gap fill**, not green-field generation: a review of the existing 6.9 tests against the 15 acceptance criteria and the near-identical Story 6.8 `OperationTimeline` template surfaced **8 coverage gaps**, all confirmed against the implementation source and **auto-applied**.

No separate HTTP/API test layer exists for this feature — the console reads projections through the generated SDK `IClient`. Coverage is therefore **bUnit page/component tests** (rendering + invariants) and **Playwright E2E smoke** (route load), matching the project's existing patterns.

## Generated Tests (8 new)

### bUnit — `tests/Hexalith.Folders.UI.Tests`

`IncidentStreamPageTests.cs` (+7 → 17→24 facts)
- [x] `WhilePrimaryReadPending_RendersLoadingBusyIndicator_BannerStillPresent` — AC #14 untested `aria-busy` loading branch
- [x] `StaleFreshness_RendersStaleInFooter_NotCurrent` — AC #2 / UX-DR26 freshness footer (recurring 6.7/6.8 risk)
- [x] `PresentFreshness_RendersRealCheckpointInBanner_NotUnknown` — AC #2 happy-path checkpoint (complements absent→"unknown")
- [x] `MultipleEntries_RenderOneRowEach_NoClientSideHiding` — AC #10 no client-side hiding
- [x] `CorrelationCopyButton_ReceivesOldestToNewestTimeWindow_FromVisibleEntries` — AC #5 `TimeWindowLabel()` oldest..newest logic
- [x] `BlankWorkspaceMetadataOnly_RendersMissing_DistinctFromRedacted` — AC #6 Missing ≠ Redacted distinctness
- [x] `SupplementaryPermissionsDenied_IsSwallowed_TableStillRenders` — AC #7 swallow-denial `TryReadAsync` path

`CorrelationCopyButtonTests.cs` (+1 → 3→4 facts)
- [x] `RendersHumanLabel_NotTheRawPayload_InTheDom` — AC #5 / AC #12 payload copied via JS only, never rendered to DOM

### E2E (Playwright) — `tests/Hexalith.Folders.UI.E2E.Tests/Smoke`

- `IncidentRouteSmokeTests.cs` — already shipped by dev-story; the one-smoke-per-route convention (6.6–6.8) is satisfied. **No gap.**

## Coverage

| Surface | Before | After | Note |
|---|---|---|---|
| UI bUnit lane (`Hexalith.Folders.UI.Tests`) | 446 | **454** | +8, all green |
| `IncidentStream` loading / freshness-footer / multi-row / window / distinctness / swallow-denial | untested | covered | AC #2/#5/#6/#7/#10/#14 hardened |
| `CorrelationCopyButton` no-leak DOM guard | untested | covered | AC #5 / AC #12 |
| Incident E2E smoke route | 1 | 1 | already covered (deferred lane) |

## Verification

- `dotnet test tests/Hexalith.Folders.UI.Tests` → **454 passed, 0 failed, 0 skipped** (Windows SDK `/mnt/c/Program Files/dotnet/dotnet.exe`, SDK 10.0.300).
- `git diff --stat` of `src/Hexalith.Folders.Client/Generated/`, `Directory.Packages.props`, `*.csproj`, `*.slnx`, and the OpenAPI spine is **empty** — only existing test files extended; no source/package/contract edits.
- `NavigationContractTests.Console_DoesNotRegisterAnyDomainCommandManifest` passes within the 454; pre-existing IntegrationTests/LoadTests `ScaffoldContractTests` reds are unrelated and untouched.

## Next Steps

- Run the deferred Playwright E2E lane (`tests/install-playwright.ps1` + live Aspire host) to execute the incident-route smoke.
- Story 6.11 owns the formal no-mutation + WCAG 2.2 AA verification sweep; these tests build toward it but do not replace that matrix.

## Checklist validation (`.agents/skills/bmad-qa-generate-e2e-tests/checklist.md`)

- **Test Generation** — API tests **N/A** (SDK-`IClient`-backed UI); E2E/bUnit generated ✅; standard framework APIs ✅; happy path ✅; critical error cases (denied, transport failure, cancellation, missing/redacted, swallowed denial) ✅.
- **Test Quality** — all run successfully (454/454) ✅; semantic/accessible locators (`data-testid`, `role`, `data-fc-disclosure`) ✅; clear descriptions ✅; no hardcoded waits/sleeps (`WaitForAssertion`) ✅; tests independent ✅.
- **Output** — summary created ✅; tests saved to appropriate directories ✅; coverage metrics included ✅.
