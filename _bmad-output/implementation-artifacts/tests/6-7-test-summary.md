# Test Automation Summary — Story 6.7 (Provider readiness & support diagnostic pages)

**Workflow:** `bmad-qa-generate-e2e-tests` · **Role:** QA automation engineer (test generation only) · **Date:** 2026-05-29
**Feature under test:** the Provider view (§3.3) of the operations console — `Provider.razor` (folder-scoped readiness) and `ProviderSupport.razor` (tenant-scoped capability matrix, FR57), plus the AC #11 link-out wiring.

## Approach

Story 6.7 already shipped a strong suite (baseline **349/349** green). This run was a **gap fill**, not a green-field generation: an adversarial multi-agent gap sweep (5 finder dimensions → independent verify pass) surfaced 24 candidate gaps; **23 were confirmed** against the implementation source and **1 was rejected** (its proposed selector `data-fc-slot` does not exist — the real attribute is `data-fc-badge-slot`, PascalCase). All 23 confirmed gaps were auto-applied.

No separate HTTP/API test layer exists for this feature — the console reads projections through the generated SDK `IClient`. Coverage is therefore **bUnit component/page tests** (rendering + invariants) and **Playwright E2E smoke** (route load), matching the project's existing patterns (`DiagnosticTestContext`, `ShouldHaveNoMutationAffordances`, total `[Theory]` enum coverage, `FolderRouteSmokeTests`).

## Generated Tests (23 new)

### bUnit — `tests/Hexalith.Folders.UI.Tests`

`ProviderPageTests.cs` (+10) — Provider readiness view §3.3
- [x] `RendersTenantScopeBanner_ScopeFirst_OnProviderPage` — AC #2 scope-before-evidence (tenant from context, not route)
- [x] `CorrelationSection_RendersCorrelationAndProviderCorrelation_AsSafeCopyId` — AC #7 identifiers as monospace SafeCopyId
- [x] `SensitiveMetadataTier_RendersResolvedLabel_WhenPresent` — concern #17
- [x] `FreshnessCurrent_RendersCurrent_NotStaleNorUnavailable` — AC #4 / AC #8
- [x] `StaleEvidence_RendersStaleFreshness_WithReasonCode_NeverCurrentOrUnavailable` — UX-DR26 (stale never shown as current)
- [x] `UnavailableAvailability_RendersUnavailableFreshness_StillRendersSections` — AC #8 stale ≠ unavailable (distinct from read-model-unavailable empty state)
- [x] `UnknownAvailability_RendersUnknownFreshness_NeverCurrent` — UX-DR26 reference-pending C5 fallthrough
- [x] `ReadinessReasonUnknown_RendersUnknownAffordance_DispositionStillPrimary` — AC #4 disposition-primary / reason-secondary
- [x] `IdentityFieldsUnknown_RenderUnknownAffordance_WhenBindingAbsent_NotRedacted` — AC #8 redacted ≠ unknown ≠ missing
- [x] `WorkspaceContextWithoutOperation_RendersSync_ButHonestUnknownFailure` — AC #5 operation-scoped honesty boundary

`ProviderSupportPageTests.cs` (+4) — capability matrix (FR57)
- [x] `PresentCapabilityProfileRef_RendersMonospaceSafeCopyId` — AC #7
- [x] `MissingCapabilityProfileRef_RendersUnknownAffordance_NotEmptyOrLock` — AC #3 / AC #8
- [x] `SameCapability_DifferentSupportStatePerProfile_RendersDistinctBadges` — AC #6 / FR57 differences read from evidence, never inferred
- [x] `StaleEvidence_RendersStaleFreshness_NotCurrent` — UX-DR26 on the support matrix

`ProviderStatusTextTests.cs` (+5) — mapper slot/label drift sentinels
- [x] `ResolveCapabilityStateSlot_TemporarilyUnavailable_IsWarning_NotDanger` — AC #8 transient ≠ failed
- [x] `ResolveBindingStateSlot_Requested_IsInfo_InFlight` — AC #4 / AC #8
- [x] `ResolveReadinessStatusSlot_MapsReadyToSuccess_DegradedToWarning_FailedToDanger` — AC #8 degraded ≠ unavailable
- [x] `ResolveOutcomeStateSlot_ReconciliationRequiredIsWarning_AndKnownFailureIsDanger` — AC #8 awaiting-human treatment
- [x] `ResolveOutcomeStateLabel_UnknownProviderOutcome_IsHonestDistinctLabel_NotNeutralUnknown` — AC #8 not collapsed to neutral "Unknown"

`WorkspacePageTests.cs` / `FolderDetailPageTests.cs` / `ShellCompositionTests.cs` (+3) — AC #11 link-out regression guards
- [x] `ProviderReadinessSection_ResolvesPlaceholder_IntoProviderLink_WithFolderScopedHref` — pending placeholder resolved → real link
- [x] `ProviderBindingRow_RendersProviderReadinessLink_WithFolderScopedHref`
- [x] `Home_RendersProviderSupportNavLink_PointingToProvidersSupport`

### E2E (Playwright) — `tests/Hexalith.Folders.UI.E2E.Tests/Smoke`

`ProviderRouteSmokeTests.cs` (+1)
- [x] `ProviderPageWithOperationContext_Loads_AndExposesConsolePageProviderRoot` — AC #5 / AC #12 optional `?WorkspaceId&OperationId` query string loads without crashing on the backend-less hermetic host (page-root + single `<h1>`)

## Coverage

| Surface | Before | After | Note |
|---|---|---|---|
| UI bUnit lane (`Hexalith.Folders.UI.Tests`) | 349 | **371** | +22, all green |
| Provider freshness zone branches (current/stale/unavailable/unknown) | 0/4 | **4/4** | UX-DR26 + AC #8 now anchored on both ends |
| Provider page AC #2 banner / AC #7 correlation / concern #17 tier | untested | covered | |
| Provider identity & readiness "Unknown" honesty branches | untested | covered | redacted ≠ unknown ≠ missing |
| Support matrix freshness-stale / missing-profile / FR57 differences | untested | covered | |
| Mapper specific slot/label drift sentinels | partial | covered | totality theories already existed |
| AC #11 link-out resolution (Workspace / FolderDetail / Home) | 0/3 | **3/3** | navigation contract guarded |
| Provider E2E smoke routes | 2 | **3** | + operation-context query string |

## Verification

- `dotnet test tests/Hexalith.Folders.UI.Tests` → **371 passed, 0 failed, 0 skipped** (WSL Windows SDK `/mnt/c/Program Files/dotnet/dotnet.exe`, SDK 10.0.302).
- `dotnet build tests/Hexalith.Folders.UI.E2E.Tests` → **build succeeded, 0 warnings, 0 errors** (warnings-as-errors). Playwright smoke tests are not executed in this lane (deferred hermetic E2E lane); they compile and follow the established backend-tolerant pattern.
- Guards green: `NavigationContractTests.Console_DoesNotRegisterAnyDomainCommandManifest` passes within the 371; `git diff --stat src/Hexalith.Folders.Client/Generated/` is **empty**; no source, `Directory.Packages.props`, or OpenAPI-spine edits — only test files added.

## Next Steps

- Run the deferred Playwright E2E lane (`tests/install-playwright.ps1`) when the hermetic host is available to execute the new provider smoke test.
- Story 6.11 owns the formal no-mutation + WCAG 2.2 AA verification sweep; these tests build defensively toward it but do not replace that matrix.

## Checklist validation (`.agents/skills/bmad-qa-generate-e2e-tests/checklist.md`)

- **Test Generation** — API tests: **N/A** (no separate API layer; SDK-`IClient`-backed UI); E2E/bUnit generated ✅; standard framework APIs (xUnit v3 / bUnit / Shouldly / NSubstitute / Playwright) ✅; happy path ✅; critical error cases (denied, transport failure, unknown/redacted, missing context) ✅.
- **Test Quality** — all run successfully (371/371) ✅; semantic/accessible locators (`data-testid`, roles, `data-fc-disclosure`, `data-fc-badge-slot`) ✅; clear descriptions ✅; no hardcoded waits/sleeps (`WaitForAssertion` / Playwright `WaitForAsync`) ✅; tests independent (each builds its own `DiagnosticTestContext`) ✅.
- **Output** — summary created ✅; tests saved to appropriate directories ✅; coverage metrics included ✅.
