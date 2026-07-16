---
baseline_commit: fb3b6cd
---

# Story 6.11: Verify no-mutation enforcement and accessibility

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a release reviewer,
I want the console verified as read-only and WCAG 2.2 AA conformant,
so that the MVP console satisfies its safety and accessibility promises.

## Acceptance Criteria

> **Epic 6.11 BDD (verbatim, epics.md L1449–1462):**
> **Given** the console is feature complete
> **When** verification runs
> **Then** automated and manual checks confirm no mutation paths, credential reveal, file-content browsing, file editing, raw diff display, hidden repair action, or unrestricted filesystem browsing
> **And** responsive checks cover desktop, tablet, and mobile fallback widths with dense identifiers and long paths in tables, timelines, metadata trees, and trust summaries
> **And** browser zoom checks at 125%, 150%, and 200% confirm text, controls, tables, and key workflows remain readable and usable
> **And** accessibility validation covers automated checks, keyboard-only walkthroughs for the three critical journeys, screen reader review for summary/folder/redaction/audit flows, forced-colors or high-contrast checks where supported, color-blindness review, focus management, semantic headings, readable tables, contrast, and non-color-only indicators against WCAG 2.2 AA expectations.

> **CRITICAL SCOPING FACT #1 — this is a VERIFICATION story with a release-evidence gate, not a CI gate (read before coding).** Per the repo, the **accessibility** NFR and the **operations-console usability** NFR are explicitly in the **release-validation-evidence** bucket, **not** the mandatory-automated-CI-test bucket: `prd.md L780` ("Performance, **accessibility**, retention, backup/recovery, and operations-console usability NFRs must have **release validation evidence before MVP acceptance**") vs `prd.md L779` (the automated-test set, which does **not** include accessibility); architecture echoes this — every NFR needs "at least one CI gate, lint, codegen rule, **or release-validation evidence path**" (`architecture.md L1503`; `L55`; Console-Accessibility NFR coverage at `L1502`). So 6.11's deliverable is **two halves, exactly mirroring how Story 6.10 split F-7**: (a) the **shippable, testable automated portion** that extends the green UI lane — the no-mutation sweep + the WCAG-structural sweep + the rendered-markup leakage assertions, plus an *optional non-brittle* Playwright responsive-viewport smoke — and (b) a **documented release-validation evidence artifact** `docs/ux/ops-console-accessibility-and-no-mutation-verification.md` that records the full UX-DR31/UX-DR32 method and the inherently-manual checks. **Do NOT** build a blocking CI accessibility gate or a console a11y harness (release rollup is Workstream 7). See Dev Notes → "Scope & the gate-type variance".

> **CRITICAL SCOPING FACT #2 — there is NO accessibility tooling on the graph and you may NOT add one.** A repo-wide search confirms **no axe / Deque / pa11y / lighthouse / Playwright-accessibility package exists anywhere** (root `Directory.Packages.props` has none). `Directory.Packages.props` is a **forbidden touch** for UI stories (Story 6.10 doc L208/L259). Therefore **6.11 cannot add `Deque.AxeCore.Playwright` or any a11y/contrast package** — the E2E README anticipates one (`tests/Hexalith.Folders.UI.E2E.Tests/README.md:50–52`) but it was **never wired** and must **not** be added now. The automated verification you CAN build uses only the **already-pinned** `bunit 2.7.2` + AngleSharp (attribute/markup/DOM assertions) and `Microsoft.Playwright 1.60.0` (route/locator/DOM-count structural checks). Browser **zoom (125/150/200%)**, **forced-colors/high-contrast**, **color-blindness**, **screen-reader**, and **real-width responsive visual** checks are inherently manual → **documented evidence**, not new automated tests. See Dev Notes → "Testing requirements".

Decomposed, testable acceptance criteria:

1. **Verification-only scope — no production console behaviour change by default.** This story adds **verification tests** and **one evidence doc**. Production code under `src/Hexalith.Folders.UI/**` is changed **only** if a verification check surfaces a genuine WCAG-2.2-AA or no-mutation **defect**; then the fix is **minimal, scoped to the defect, recorded** in the File List + Change Log, and re-verified. **No** new console features, pages, routes, components, endpoints, SDK methods, or packages. (If a finding is defensive-hardening rather than a defect — e.g. the `SkeletonState` timer-fire-after-`Dispose` `_disposed`-guard noted as an optional 6.11 follow-up in the 6.10 review — it is **optional**, not required, and must not expand scope.)

2. **No-mutation enforcement sweep (UX-DR11 / UX-DR23 / F-2 — the BDD's seven prohibited paths).** A consolidated bUnit verification asserts `ShouldHaveNoMutationAffordances()` — the **five-selector command-suppression guard** (`form`, `fluentinputform`, `fluentdialog`, `[data-fc-command]`, `[data-fc-mutation]`; `ConsoleTestAssertions.cs:16–24`) — renders **empty** on **every** console page: the **ten** operator pages (`Home`, `Tenants`, `Folders`, `FolderDetail`, `Workspace`, `AuditTrail`, `OperationTimeline`, `Provider`, `ProviderSupport`, `IncidentStream`) **and** the **two dev-only galleries** (`RedactionGallery`, `StateLabelGallery`), plus every interactive component. **And** `NavigationContractTests.Console_DoesNotRegisterAnyDomainCommandManifest` (asserts `registry.GetManifests()` is empty — the structural proof the console wires **zero** domain command manifests; `NavigationContractTests.cs:32–43`) stays green. This is the automated proof of "no mutation paths".

3. **No credential / file-content / raw-diff / repair / filesystem leakage in rendered markup (UX-DR11 / UX-DR12 / UX-DR27 / FR55).** bUnit assertions confirm that injected **sentinel** secret / credential / file-content / diff markers **never appear** in any page's rendered DOM — modelled on `RedactedField` defence-in-depth (the value is emitted **only** in the `Visible` branch; `RedactedFieldTests.cs:44–46`, `RedactedField.razor.cs:82–99`); the provider **credential-reference** renders as a non-secret reference identifier only, never a secret (`Provider.razor:126–135`); and **no** raw-diff, file-editing, file-content-browsing, repair, or unrestricted-filesystem affordance exists on any surface. Cite the contract/telemetry-layer backstop `SafetyInvariantGateTests` (the metadata-only gate over OpenAPI examples / generated SDK / audit/telemetry channels) — but note it does **not** cover the **rendered console DOM**; the rendered-DOM assertions are the **new** coverage this story adds.

4. **WCAG 2.2 AA structural sweep (UX-DR30) — the automated bUnit/AngleSharp portion.** A consolidated accessibility-contract test asserts, across **every** page: exactly **one `<h1>`**; `<html lang="en">` (`App.razor:2`) + responsive viewport meta (`App.razor:5`); `<FocusOnNavigate Selector="h1" />` is wired (`Routes.razor:5`); every `<table>` carries a `<caption>` + `scope="col"`/`scope="row"` headers; pagination `<nav>`s have `aria-label`s; loading/live regions carry `aria-busy`/`aria-live` + a label; **all interactive controls are keyboard-reachable native `<button type="button">` / `<a href>` with accessible names** (no click-only `<div>`/`<span>`); and landmark roles are present (`role="alert"`/`"status"`/`"note"`, `<section aria-label>`, `<nav aria-label>`). Much of this already exists per-component (`RedactedFieldTests`, `TrustMatrixTests`, `OperatorDispositionBadgeTests`, `StillLoadingCancelTests`, `SkeletonStateTests`, the per-page `h1`-count + `ShouldHaveNoMutationAffordances` tests) — **consolidate it into an exhaustive per-page contract sweep** so the WCAG-structural invariant is asserted uniformly rather than ad hoc.

5. **Non-color-only indicators + redaction distinction (UX-DR14 / UX-DR10 / UX-DR22 / F-4 / F-5).** Assert every status state — disposition badge, technical-state metadata, freshness, error, empty, redaction — conveys via **text + icon/shape + accessible label, never colour alone**; and `RedactedField` renders **redacted** visibly/semantically distinct from **unknown / missing / unavailable** via distinct `data-fc-disclosure` tokens with the **lock icon on redacted only** (`RedactedField.razor.cs:82–99`). Silent truncation or representing redaction as missing is an operator-facing **correctness bug** — verify it cannot happen.

6. **Three critical journeys — keyboard-only walkthroughs (UX-DR32 / UX-DR30 / UX-DR24).** Documented keyboard-only walkthrough evidence for the **three critical journeys** (`ops-console-wireflows.md` §6 L665–L763; `ux-design-specification.md` L422–L482): **J1 — Find Workspace and Inspect Trust State** (`/folders` → `/folders/{id}` → `/folders/{id}/workspaces/{wid}`, ending on `WorkspaceTrustSummary` + `TrustMatrix`); **J2 — Prove Tenant Isolation and Safe Folder Visibility** (`TenantScopeBanner` + `MetadataOnlyFolderTree` + the denied-without-leakage path; provider readiness at `/providers/support` and `/folders/{id}/provider`); **J3 — Diagnose Workspace Failure from Evidence** (`/folders/{id}/audit-trail` + `/folders/{id}/operation-timeline` + the F-6 last-resort `/_admin/incident-stream`). Each walkthrough records: sensible tab order, always-visible focus, `FocusOnNavigate` landing focus on the `<h1>` after navigation, every interactive element reachable+operable by keyboard, and **no keyboard trap**.

7. **Screen-reader review — summary / folder / redaction / audit flows (UX-DR32).** Documented screen-reader review evidence for the **four** flows the AC names, mapped to surfaces: **summary** → `Workspace.razor` (`WorkspaceTrustSummary`, `TrustMatrix`); **folder** → `FolderDetail.razor` + `MetadataOnlyFolderTree`; **redaction** → `RedactedField` everywhere (+ the isolated `RedactionGallery` proof surface, + `Provider` credential-reference); **audit** → `AuditTrail.razor` (+ `OperationTimeline`, `IncidentStream`). Evidence confirms: headings/landmarks announce structure; status labels are screen-reader-meaningful; redaction announces "Hidden by tenant policy" distinctly from unknown/missing; timeline chronological order is preserved for assistive tech; decorative icons are `aria-hidden`.

8. **Responsive widths + dense identifiers + long paths (UX-DR31 / UX-DR28 / UX-DR29).** Documented responsive evidence at **desktop** (1024 / 1280 / 1440 / wide), **tablet** (768–1023), and **mobile fallback** (~360 / ~430 px) widths (`ux-design-specification.md` L758–L764; breakpoints L714–L723) confirming the tablet/mobile fallback **stacks panels, collapses nav, preserves search/filters, prioritizes tenant/workspace/state/risk, and does not break core lookup/trust review**; AND that **dense identifiers + long paths remain readable/usable** (truncation only where the full value stays available via safe-copy / `title` tooltip / details) in the **four named surfaces**: **tables** (the six `<table>`s), **timelines** (`OperationTimeline`, `IncidentStream`), **metadata trees** (`MetadataOnlyFolderTree`, which indents by depth + `title="@fullPath"`), and **trust summaries** (`WorkspaceTrustSummary` + `TrustMatrix`). **OPTIONAL non-brittle automation:** a Playwright viewport-width smoke over those routes asserting the page **root + single `<h1>` + key testids still resolve** at tablet/mobile widths (testid **presence**, **NOT** pixel/overflow assertions — project-context forbids brittle CSS/text/sleep tests). The real-width **visual** assessment is documented manual evidence.

9. **Browser zoom 125% / 150% / 200% (UX-DR31 / UX-DR30).** Documented zoom evidence at **exactly** 125%, 150%, and 200% (`ux-design-specification.md` L763) confirming text, controls, tables, and the three journeys remain **readable and usable** (no clipping / overlap / loss of function). Manual evidence — no automated zoom tool is available.

10. **Forced-colors / high-contrast (where supported) + color-blindness review (UX-DR32 / UX-DR14).** Documented evidence: forced-colors / high-contrast checks **where the browser supports it** (the AC's "where supported" caveat — the wireflows define **no** per-page forced-colors contract beyond the non-color-only rule, `ops-console-wireflows.md` L657–L658, L121-note); and a color-blindness review confirming semantic states stay distinguishable **without colour** (leaning on the text+icon/shape requirement, UX-DR14). Manual evidence.

11. **Focus management (UX-DR24 / UX-DR30).** Verify visible focus, reading-order focus order, and `FocusOnNavigate` → `<h1>` after navigation (`Routes.razor:5`). The MVP console has **no dialogs** — the no-mutation sweep (AC #2) already asserts zero `fluentdialog`; record that any future dialog must trap + restore focus per UX-DR24. Covered by the keyboard walkthroughs (AC #6) + the structural sweep (AC #4).

12. **Release-validation evidence artifact.** Create `docs/ux/ops-console-accessibility-and-no-mutation-verification.md` (beside `ops-console-wireflows.md` / `ops-console-performance-budget.md`). It records, metadata-only: the **no-mutation** verification result + method (the five-selector sweep + the registry-empty gate + the Playwright DOM-count check); the **WCAG 2.2 AA** conformance evidence; the **three keyboard-only journey** walkthroughs; the **screen-reader** review of the four flows; the **responsive** (named widths) + **dense-id / long-path** checks; **zoom** 125/150/200%; **forced-colors/high-contrast** (where supported) + **color-blindness**; **focus management**; and a **UX-DR31 / UX-DR32** (+ UX-DR11 / UX-DR23 / UX-DR30) → result **traceability table**. It names the **gate type = release-validation evidence before MVP acceptance** (`prd.md L780`). Automated-check results are recorded as **actually run** (test names + the green count); manual checks that require a real browser / assistive-tech pass at release time are recorded as **`reference_pending`** with the **method defined** (matching the 6.10 `reference_pending` discipline and the `docs/exit-criteria/c0-c13-governance-evidence.yaml` convention). Use only **synthetic** identifiers (`acme`, `tenant-a`, `folder-123`, …); no real tenant/folder/credential/path/audit data; **no fabricated pass claims**.

13. **Forbidden touches & scope guards (review-failure flags — do NOT pull neighbouring work in).** **No new package** (`Directory.Packages.props` forbidden → **no Deque/axe/a11y package**). **No** edits to `src/Hexalith.Folders.Client/Generated/**`, `Compat/**`, the OpenAPI spine, or any `*.csproj`/`*.slnx`. **No `.razor.css`.** **No** new console feature / page / route / component / endpoint / SDK method. **No** blocking CI a11y or perf gate (release-validated; Workstream 7 owns the release rollup and C1/C2/C5 capacity numbers — keep them `reference_pending`). **No French strings** — document localization entry points only (English MVP; `ops-console-wireflows.md` L776).

14. **Tests + gates.** The new bUnit verification tests (the no-mutation sweep, the WCAG-structural sweep, the rendered-markup leakage assertions, the non-color-only/redaction-distinction sweep) are **all green**; the optional Playwright responsive-viewport smoke is green (or documented as manual if not automated). The full UI lane stays **green over the 486/486 baseline** plus the new verification tests, **zero warnings** (warnings-as-error). `Console_DoesNotRegisterAnyDomainCommandManifest` stays green. `git diff --stat` is **empty** for `src/Hexalith.Folders.Client/Generated/`, `Directory.Packages.props`, the OpenAPI spine, and all `*.csproj`/`*.slnx`; **no** new `<PackageReference>`/`<ProjectReference>`; **no** `.razor.css`. The two pre-existing `Hexalith.Folders.IntegrationTests`/`LoadTests` `ScaffoldContractTests` reds are **not** 6.11 regressions ([[scaffold-contract-tests-baseline-reds]]). Record the **complete** File List (every source AND test file + the new doc) and the accurate **before/after** test counts in the Dev Agent Record. Build/test with the **Windows SDK** (`/mnt/c/Program Files/dotnet/dotnet.exe`).

## Tasks / Subtasks

- [x] **Task 1 — No-mutation enforcement sweep (the automated read-only proof)** (AC: 1, 2, 3)
  - [x] Add a consolidated verification test (e.g. `tests/Hexalith.Folders.UI.Tests/Verification/NoMutationConsoleSweepTests.cs`) that renders **every** page — the ten operator pages **and** the two dev-only galleries (`RedactionGallery`, `StateLabelGallery`) — and asserts `rendered.ShouldHaveNoMutationAffordances()` (the five-selector guard, `ConsoleTestAssertions.cs:16–24`) on each. Reuse `DiagnosticTestContext.Create(tenantId, userId)` for SDK pages and `BadgeRenderingFixture` for components; set `[SupplyParameterFromQuery]` via `NavigationManager.NavigateTo(...)` and route params via `p.Add(...)` (the established 6.10 page-test pattern).
  - [x] Assert `NavigationContractTests.Console_DoesNotRegisterAnyDomainCommandManifest` stays green (registry-empty proof, `NavigationContractTests.cs:32–43`) — extend/cite, do not duplicate.
  - [x] Add rendered-markup **leakage** assertions: inject sentinel secret/credential/file-content/diff markers through the view models and assert they **never** appear in the page DOM (pattern: `RedactedFieldTests.cs:44–46`); assert the provider credential-reference renders as a reference identifier only (`Provider.razor:126–135`); assert no raw-diff / file-edit / repair / filesystem-browse affordance is present.
  - [x] Cite `SafetyInvariantGateTests` (`tests/Hexalith.Folders.Contracts.Tests/...OpenApi/SafetyInvariantGateTests.cs`) as the contract/telemetry-layer metadata-only backstop and note it does not cover the rendered DOM.
- [x] **Task 2 — WCAG 2.2 AA structural + non-color-only/redaction sweep** (AC: 4, 5, 11)
  - [x] Add a consolidated test (e.g. `AccessibilityContractSweepTests.cs`) asserting per page: single `<h1>`; `<html lang="en">` + viewport meta (`App.razor:2,5`); `FocusOnNavigate Selector="h1"` wired (`Routes.razor:5`); every `<table>` has `<caption>` + `scope` headers; pagination `<nav>` has `aria-label`; live/busy regions labelled; every interactive control is a keyboard-reachable native `<button type="button">`/`<a href>` with an accessible name; landmark roles present.
  - [x] Assert non-color-only on every status surface (disposition badge, technical-state, freshness, error, empty, redaction) — text + icon/shape + accessible label (UX-DR14); reuse the existing per-component a11y assertions (`TrustMatrixTests:42–56`, `OperatorDispositionBadgeTests:48–68`, `RedactedFieldTests:70–101`) and roll them into the page-level sweep.
  - [x] Assert the redaction distinction: `RedactedField` renders redacted distinct from unknown/missing/unavailable via distinct `data-fc-disclosure` tokens with the lock icon on redacted only (`RedactedField.razor.cs:82–99`).
- [x] **Task 3 — (Optional, non-brittle) Playwright responsive-viewport smoke** (AC: 8)
  - [x] In `tests/Hexalith.Folders.UI.E2E.Tests/`, optionally add a viewport-width smoke (desktop 1280, tablet 768–1023, mobile fallback ~360/~430) over the table/timeline/trust-summary routes asserting **root + single `<h1>` + key `data-testid`s still resolve** — **presence only, no pixel/overflow assertions**. Reuse `AspireConsoleHostFixture` (Kestrel hermetic host, no Aspire backend) + `PlaywrightFixture` + `ConsoleRoutes.cs`. Model the no-mutation DOM-count style on `StateLabelGalleryE2ETests.cs:100–123` (note its selectors use hyphenated `fluent-dialog`). If not automated, record the responsive width check as documented manual evidence instead (Task 4).
- [x] **Task 4 — Conduct + record the manual release-validation checks** (AC: 6, 7, 8, 9, 10, 11)
  - [x] Keyboard-only walkthroughs of the **three critical journeys** (J1/J2/J3 — AC #6): tab order, visible focus, `FocusOnNavigate`→h1, no trap. Record per-journey outcomes.
  - [x] Screen-reader review of **summary / folder / redaction / audit** flows (AC #7): heading/landmark announcement, screen-reader-meaningful status labels, redaction announcement distinct from unknown/missing, chronological timeline order.
  - [x] Responsive desktop/tablet/mobile-fallback + dense-id/long-path checks over tables/timelines/metadata trees/trust summaries (AC #8 manual half); browser zoom at 125/150/200% (AC #9); forced-colors/high-contrast where supported + color-blindness review (AC #10); focus management (AC #11).
  - [x] Where a check needs a real browser / assistive-tech pass that is a release-time activity, record it as **`reference_pending`** with the **method** stated — do **not** fabricate a pass.
- [x] **Task 5 — Release-validation evidence artifact** (AC: 12)
  - [x] Create `docs/ux/ops-console-accessibility-and-no-mutation-verification.md` (beside `ops-console-wireflows.md`). Record the no-mutation result + method; the WCAG 2.2 AA evidence; the three keyboard journeys; the screen-reader review; responsive + dense-id/long-path; zoom 125/150/200%; forced-colors/high-contrast + color-blindness; focus management; and a **UX-DR31/UX-DR32 (+ UX-DR11/UX-DR23/UX-DR30) → result** traceability table. Name the gate type = release-validation evidence before MVP acceptance (`prd.md L780`). Automated results as actually-run (test names + green count); manual results as recorded or `reference_pending`. Metadata-only, synthetic identifiers only, no fabricated claims. Document localization entry points (English MVP), add **no French strings**.
- [x] **Task 6 — Verify gates green & record fidelity** (AC: 1, 13, 14)
  - [x] Build/test with the **Windows SDK** (`/mnt/c/Program Files/dotnet/dotnet.exe`); capture the baseline `dotnet test tests/Hexalith.Folders.UI.Tests` count (expect **486/486**), then the final count. Build/run the E2E project if Task 3 added a test (`pwsh tests/install-playwright.ps1` once; headless Chromium against the in-process Kestrel host).
  - [x] Confirm `Console_DoesNotRegisterAnyDomainCommandManifest` green. Confirm `git diff --stat` empty for `src/Hexalith.Folders.Client/Generated/`, `Directory.Packages.props`, OpenAPI spine, all `*.csproj`/`*.slnx`; **no** new `<PackageReference>`/`<ProjectReference>`; **no** `.razor.css`. The two pre-existing IntegrationTests/LoadTests `ScaffoldContractTests` reds are **not** 6.11 regressions ([[scaffold-contract-tests-baseline-reds]]).
  - [x] Optional safety gate: `tests/tools/run-safety-invariant-gates.ps1`.
  - [x] Record the **complete** File List (every source AND test file, including any modified `*PageTests.cs`, plus the new doc) and the accurate before/after test count in the Dev Agent Record.

## Dev Notes

**Scope is verification-only and consumes the already-shipped console.** Stories 6.1–6.10 built the console; **6.11 builds nothing new** — it adds a verification test suite (the automated portion) plus one release-validation evidence doc (the documented portion), and touches production `src/Hexalith.Folders.UI/**` **only** to fix a genuine defect surfaced by verification (minimal + recorded). This is the **last build slice of Epic 6** before the (optional) retrospective; UX-DR31 and UX-DR32 are the two requirements Epic 6 explicitly **defers to this story** for verification (`epics.md L1309`).

### Scope & the gate-type variance (read first)

The AC reads "automated **and** manual checks confirm…". In this repo the verification is **release-validation evidence, not a CI gate**:

- `prd.md L780` puts **accessibility** and **operations-console usability** on the **release-validation-evidence** path; `prd.md L779` (the mandatory-automated-test set) **omits** accessibility. `prd.md L778`: "Each NFR category must have at least one automated verification path **or** documented manual validation path before MVP release."
- `architecture.md L55`/`L1503` confirm the console-accessibility NFR is satisfied via a **release-validation-evidence path** (there is **no** CI a11y gate); `L1502` lists only the F-3/F-4/F-5/F-6/F-7 decisions as its coverage, **no test lane**.
- There is **no automated a11y tool on the dependency graph** and `Directory.Packages.props` is forbidden to touch — so the zoom / forced-colors / color-blindness / screen-reader / real-width responsive portions **cannot** be automated here.

**Therefore:** the shippable, testable deliverable is the **automated portion** (no-mutation sweep + WCAG-structural sweep + rendered-markup leakage assertions + optional non-brittle Playwright viewport smoke) that extends the green UI lane, **plus** a **documented evidence artifact** (`docs/ux/ops-console-accessibility-and-no-mutation-verification.md`) recording the full UX-DR31/UX-DR32 method and the manual checks. **Do not** stand up an a11y CI gate or a console a11y harness, and **do not** add an a11y package — that exactly mirrors how 6.10 split F-7 into a shippable perceived-wait UX + a `reference_pending` budget-evidence doc.

### Technical requirements (dev-agent guardrails)

- **Reuse the established no-mutation guard verbatim.** `ConsoleTestAssertions.ShouldHaveNoMutationAffordances<T>()` (`ConsoleTestAssertions.cs:16–24`) checks exactly five selectors: `form`, `fluentinputform`, `fluentdialog`, `[data-fc-command]`, `[data-fc-mutation]`. Sweep it over **all twelve** pages + every interactive component. The registry-empty proof is `NavigationContractTests.Console_DoesNotRegisterAnyDomainCommandManifest` (`NavigationContractTests.cs:32–43`).
- **The console wires zero command manifests by construction** — the UI references `Hexalith.Folders.Client` only and reads from projections (F-2, `architecture.md L546`; component-boundary enforcement `L1487`); it does **not** call `AddHexalithEventStore`, so no `/api/v1/commands` endpoint exists (`ops-console-wireflows.md §1.6 L200–L208`). Verification asserts this boundary; it does not change it.
- **Read-only controls are native `<button type="button">`/`<a href>`** (e.g. `SafeCopyId`, `CorrelationCopyButton`, `StillLoadingCancel`) precisely so they pass the five-selector guard — keep that pattern when asserting keyboard reachability + accessible names.
- **Freshness honesty + safe-denial shape are part of the surfaces under test** — do not let verification fabricate timestamps (`0001-01-01` → render `Unknown`) or turn `not_found` vs `*_denied` into an existence oracle (`ops-console-wireflows.md §3.9 L530–L534`).
- **Build/test with the Windows SDK** (`/mnt/c/Program Files/dotnet/dotnet.exe`); the WSL SDK (`10.0.108`) fails the `global.json` `10.0.300` pin ([[dotnet-windows-sdk-wsl]]). bUnit timer determinism (for any loading-state assertions) uses the `ControllableTimeProvider` registered by `BadgeRenderingFixture` (keeps `SkeletonState` in the ≤400 ms band).
- **`.ConfigureAwait(false)` on every await** (CA2007 warnings-as-error, even in tests). Target `net10.0`, nullable, `LangVersion=latest`. Submodules root-level only, never `--recursive`.

### Architecture compliance

- **F-1..F-7 (`architecture.md L545–L551`)** — the console decisions being verified: **F-1** Blazor Server; **F-2** Blazor Server + SignalR, reads only from projections via the SDK (no aggregate access) — the structural no-mutation boundary; **F-3** Microsoft Fluent UI Blazor, the accessibility foundation that "satisfies WCAG 2.2 AA targets (focus-visible, target sizes, dragging exemption confirmed)"; **F-4** operator-disposition labels primary, technical state secondary; **F-5** redaction lock-icon affordance, never silent truncation; **F-6** incident-mode `/_admin/incident-stream` with persistent degraded banner + redaction not relaxed; **F-7** perceived-wait UX (shipped by 6.10).
- **Release-validation-evidence hook:** `architecture.md L55` (Verification Expectations NFR — "at least one automated OR release-validation path") + `L1503` ("…or release-validation evidence path") + `L1502` (Console-Accessibility NFR coverage) — 6.11 is the architectural realization of that path for the console.
- **No-mutation hard boundaries (`architecture.md L88–L92`)** — `L91` is the verbatim superset of the BDD's seven prohibited paths; `L90` the metadata-only-in-console-responses rule; `L92` the no-silent-repair rule. Enforcement is **structural** (UI→Client-only, projection-only reads) + UX-spec prohibition (`L166`), **not** a runtime guard component — so verification asserts against the boundary statements + a UI-surface audit, plus the five-selector test.
- **UI file-location layout (`architecture.md L1173–L1202`, FR52 `L1364`)** — the as-built tree places pages under `Components/Pages/` (an accepted variance from the architecture's `Pages/` listing; keep it). New **test** files live under `tests/Hexalith.Folders.UI.Tests/` (bUnit, `L1277`) and `tests/Hexalith.Folders.UI.E2E.Tests/` (Playwright); the new **doc** under `docs/ux/`.
- **C9 sensitive-metadata classification (`architecture.md L187`, ops plan `L210`)** underpins F-5 redaction verification; **C2 status-freshness (`L180`, `L203`, BLOCKS Phase-8 ops-console UX)** and **C1/C5 capacity** stay `reference_pending` — do **not** pin them here.

### Library / framework requirements

**Reuse these (do NOT reinvent, do NOT add packages):**

- **No-mutation:** `ConsoleTestAssertions.ShouldHaveNoMutationAffordances()` (`ConsoleTestAssertions.cs:16–24`); `NavigationContractTests` (`:32–43`); `CompositionRootFactory.Build(...WithAuthority/WithHermeticTestMode)`.
- **bUnit fixtures:** `DiagnosticTestContext.Create(tenantId, userId)` (SDK-page fixture, substitutes `IClient` + `IUserContextAccessor`), `BadgeRenderingFixture.Create()` (the shared bootstrapper: loose JSInterop, `ControllableTimeProvider`, FluentUI, FrontComposer quickstart, the four stubbed shell JS modules incl. `fc-keyboard.js`/`fc-focus.js`).
- **Existing a11y assertions to consolidate (already green):** `RedactedFieldTests` (aria-label per state, lock icon + text, value-never-leaks), `TrustMatrixTests` (svg icon per dimension), `OperatorDispositionBadgeTests` (text label + aria-label + slot), `StillLoadingCancelTests` (type=button + aria-label + role=status + non-color), `SkeletonStateTests` (aria-busy + aria-live), the per-page `h1`-count + `Renders_NoMutationAffordances` tests.
- **E2E (optional Task 3):** `AspireConsoleHostFixture` (Kestrel hermetic host, `Folders:Authentication:Mode=hermetic-test`, bearer `hermetic-test-token` → `tenant-a`/`user-a`, Aspire/Keycloak/EventStore bypassed), `PlaywrightFixture`/`PlaywrightCollection`, `Routes/ConsoleRoutes.cs` (the route-string contract — do not hardcode routes elsewhere), the smoke-test style in `StateLabelGalleryE2ETests.cs:100–123`. `Microsoft.Playwright 1.60.0` already pinned; `pwsh tests/install-playwright.ps1` materializes headless Chromium.
- **Shell a11y you rely on (read-only):** `FrontComposerShell` provides skip-to-content (`#fc-main-content`, `tabindex="-1"`), skip-to-navigation, and the `FcLayoutBreakpointWatcher`/`FcDensityApplier`/`FcCollapsedNavRail`/`FcColumnPrioritizer` responsive infrastructure. `Routes.razor:5` `FocusOnNavigate Selector="h1"`.

**Forbidden touches:** `src/Hexalith.Folders.Client/Generated/**`, `Compat/**`, `Directory.Packages.props` (→ **no Deque/axe/a11y package**), the OpenAPI spine `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`, all `*.csproj`/`*.slnx`. **No `.razor.css`.** `Hexalith.Folders.UI` references only `Hexalith.Folders.Client` + `Hexalith.FrontComposer.Shell`.

### File structure requirements

- New tests: `tests/Hexalith.Folders.UI.Tests/` — the consolidated no-mutation sweep + the WCAG-structural/non-color/redaction sweep + the rendered-markup leakage assertions (suggested under a `Verification/` folder; match the project's existing flat layout if simpler). Optional `tests/Hexalith.Folders.UI.E2E.Tests/` viewport-width smoke (reusing `ConsoleRoutes.cs`).
- New doc: `docs/ux/ops-console-accessibility-and-no-mutation-verification.md` (beside `ops-console-wireflows.md` + `ops-console-performance-budget.md`).
- Production source touched **only** on a verified defect fix (minimal, recorded).
- **Record every created AND modified file** (source AND tests) in the File List — File-List fidelity is the recurring Epic-6 review ding (6.6/6.7/6.8/6.9/6.10 all lost points for omitting modified `*PageTests.cs`).

### Testing requirements

- bUnit 2.7.2 + xUnit v3 + Shouldly + NSubstitute via `DiagnosticTestContext`/`BadgeRenderingFixture`; AngleSharp `QuerySelectorAll`/`GetAttribute` for fine assertions. Set `[SupplyParameterFromQuery]` via `NavigationManager.NavigateTo(...)` **before** `Ctx.Render<Page>()`; route params via `p.Add(...)`. For null-filter matchers use `Arg.Is<string>(f => f == null)` (never bare `null` mixed with `Arg` matchers). Add `#pragma warning disable xUnit1051` when stubbing `CancellationToken` overloads.
- **The automated checks you can build (and must run):** the five-selector no-mutation sweep over all twelve pages; the registry-empty gate; rendered-markup leakage assertions (sentinel never in DOM); the WCAG-structural sweep (single-h1, lang, viewport, table caption+scope, pagination aria-label, live/busy labels, keyboard-reachable native controls, landmark roles); the non-color-only + redaction-distinction sweep; optional non-brittle Playwright viewport smoke (testid presence only).
- **The checks you must NOT try to automate** (no tool, forbidden to add one — record as manual evidence in the doc): browser zoom 125/150/200%; forced-colors/high-contrast; color-blindness; screen-reader review; real-width responsive **visual** assessment. **Do not add brittle CSS/text/sleep-based tests** (project-context) and **do not add `Deque.AxeCore.Playwright`** (forbidden `Directory.Packages.props`).
- Full UI lane must stay green over the **486/486** baseline + the new tests, zero warnings. Capture before/after counts on the Windows SDK.

### Previous story intelligence (6.10 / 6.9 — apply these lessons)

- **6.10 set the precedent for this exact split:** a shippable testable half + a `reference_pending` documented-evidence half, with explicit "do not build a CI gate / do not add the testing package" guardrails. 6.10 also closed the F-7 deferral; 6.10 AC #12/#13 explicitly left "formal WCAG 2.2 AA / no-mutation **release verification**" to **Story 6.11** — this story.
- **6.10 review left two explicit optional 6.11 follow-ups** (not requirements): (1) the `SkeletonState` timer-fire-after-`Dispose` `_disposed`-guard hardening; (2) the undefined `fc-skeleton-*`/`fc-still-loading-*` CSS classes (a FrontComposer **shell** concern, not this UI story, and `.razor.css` is forbidden). Treat both as optional; neither is a WCAG/no-mutation defect.
- **File-List fidelity is the recurring review ding** — record every created and modified file (source and tests) + the accurate test count.
- **No scope creep** — verify, don't rebuild; defer capacity/perf numbers to Workstream 7; if a real defect is found, fix it minimally and record it, otherwise change no production behaviour.

### Git intelligence summary

- HEAD `fb3b6cd feat(story-6.10): Enforce console performance and perceived-wait UX`; preceding 6.9 `6dcfc2d`, 6.8 `90717e4`, 6.7 `f7774d1`, 6.6 `aba57e2`. Each Epic-6 commit added one console slice on the shared component/test scaffold. 6.11 is the **verification** slice — it adds tests + an evidence doc rather than a feature, and is the last story before `epic-6-retrospective` (optional) and the Workstream-7 release rollup.

### Project structure notes

- **Alignment:** the new verification tests live in the existing bUnit/E2E projects; the evidence doc lives beside the two existing `docs/ux/` console docs — both follow established convention. No new project, package, or production component.
- **Variance:** there is **no automated a11y/axe lane, no zoom/responsive/forced-colors/color-blindness/screen-reader test, and no rendered-DOM metadata-only gate** in the repo today (only bUnit aria/role/heading assertions + Playwright route smoke + the five-selector guard + the contract-layer `SafetyInvariantGateTests`). 6.11 closes the automatable gap with new tests and closes the inherently-manual gap with the documented release-validation evidence artifact — the in-domain way to satisfy "automated **and** manual checks confirm…" without touching Workstream-7-owned governance artifacts or adding a forbidden package.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-6.11] (L1449–1462) — story statement + BDD ACs; Epic 6 intro (L1305–1310) — "UX-DR1–UX-DR30 implemented directly; UX-DR31 and UX-DR32 verified through Story 6.11 and release-evidenced through Workstream 7"; FRs/UX coverage (L498–L500).
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md] — UX-DR table L107–L140; **UX-DR11 L119** (read-only boundary — the seven prohibited paths), **UX-DR23 L131** (no-mutation forms), **UX-DR30 L138** (WCAG 2.2 AA enumerated), **UX-DR31 L139** (widths + 125/150/200% zoom + dense-id/long-path in tables/timelines/metadata-trees/trust-summaries), **UX-DR32 L140** (automated + keyboard journeys + screen-reader + forced-colors + color-blindness + focus); UX-DR10/UX-DR14/UX-DR22 (L118/L122/L130) redaction + non-color-only; three critical journeys L422–L482; Accessibility Strategy L740–L752 (WCAG 2.2 AA list); Testing Strategy L756–L773 (widths L758–764, zoom L763, a11y methods L766–773); breakpoints L714–L723; read-only statements L100/L119/L264/L600/L632/L690/L787; redaction-distinction L754.
- [Source: docs/ux/ops-console-wireflows.md] — **§5 UX-DR→story traceability L602–L661** (6.11 verifies UX-DR11 L632 / UX-DR23 L644 / UX-DR30 L651; owns UX-DR31 L652 / UX-DR32 L653; notes L655–661); **§1.6 read-only command suppression L200–L208** (cites `Console_DoesNotRegisterAnyDomainCommandManifest`); **§6 three critical journeys L665–L763**; screen-reader flow sections — summary §3.2 L350–351, folder §3.1 L320–321, redaction §3.6 L457 + tokens L450–455, audit §3.4 L410–411; accessibility cluster §4.1 L578–L598 (keyboard L582–584, focus L585–587, non-color L588–590, zoom L591–593, responsive L594–595, redaction-vs-missing L596–598); shared status taxonomy §2 L212–L280; empty §3.8 / error §3.9 (canonical categories L530–534); French deferred to 6.11 L776; synthetic-data guarantee L36–39; `future_test_use` L18–22.
- [Source: _bmad-output/planning-artifacts/architecture.md] — **F-1..F-7 L545–L551**; release-validation-evidence path **L55 + L1503**, Console-Accessibility NFR coverage **L1502**; no-mutation hard boundaries **L88–L92** (L91 = the seven prohibited paths superset; L90 metadata-only console responses; L92 no-silent-repair); structural enforcement F-2 L546 + component-boundary L1487; UX-spec a11y constraints L166/L168/L170; UI tree L1173–L1202 + FR52 L1364; C2 L180/L203, C9 L187/L210, exit-criteria gate L216/L1503.
- [Source: _bmad-output/planning-artifacts/prd.md] — **accessibility NFR "Operations Console Accessibility" L768–L774** (WCAG 2.2 AA L770; keyboard L771; non-color-only L772; focus/headings/table/contrast L773; zoom/responsive L774); **Verification Expectations L776–L780** (L778 automated-or-manual; L779 automated-test set excludes a11y; **L780 accessibility + console-usability = release-validation evidence before MVP acceptance**); no-mutation L330/L260/L199/L753 + Non-Goals L564–L577 (L566 no repair, L573 no file edit/browse, L574 no raw diff, L576 no secret storage); console FRs FR31 L641, FR36 L649, FR45 L664, FR46 L665, FR52–FR57 L677–L682; redaction sentinel test method L488; secret-exclusion L86/L691/L694.
- **As-built code to verify against (read-only references):**
  - `src/Hexalith.Folders.UI/Components/Pages/` — the **ten operator pages** (`Home`/`Tenants`/`Folders`/`FolderDetail`/`Workspace`/`AuditTrail`/`OperationTimeline`/`Provider`/`ProviderSupport`/`IncidentStream`) + the **two dev galleries** (`RedactionGallery`, `StateLabelGallery`); each page root `console-page-{name}-root`, single `<h1>`.
  - `src/Hexalith.Folders.UI/Components/` — `OperatorDispositionBadge`, `TechnicalStateMetadata`, `RedactedField` (+ tokens `.razor.cs:82–99`), `DegradedModeBanner`, `ConsoleErrorPanel`, `ConsoleEmptyState`, `SkeletonState`, `StillLoadingCancel`, `SafeCopyId`, `CorrelationCopyButton`, `MetadataOnlyFolderTree`, `TrustMatrix`, `WorkspaceTrustSummary`, `TenantScopeBanner`, `Icons/FoldersConsoleIcons.cs`.
  - `src/Hexalith.Folders.UI/Components/Routes.razor:5` (FocusOnNavigate), `App.razor:2,5` (lang + viewport), `Layout/MainLayout.razor:3` (FrontComposerShell), `Provider.razor:126–135` (credential reference, never a secret).
  - `tests/Hexalith.Folders.UI.Tests/` — `ConsoleTestAssertions.cs:16–24` (five-selector guard), `NavigationContractTests.cs:32–43` (registry-empty), `DiagnosticTestContext.cs`, `BadgeRenderingFixture.cs`, `CompositionRootFactory.cs`, `RedactedFieldTests.cs`, `TrustMatrixTests.cs`, `OperatorDispositionBadgeTests.cs`, `StillLoadingCancelTests.cs`, `SkeletonStateTests.cs`.
  - `tests/Hexalith.Folders.UI.E2E.Tests/` — `Routes/ConsoleRoutes.cs`, `Fixtures/AspireConsoleHostFixture.cs`/`PlaywrightFixture.cs`/`PlaywrightCollection.cs`, `Smoke/*`, `StateLabels/StateLabelGalleryE2ETests.cs:100–123` (Playwright no-mutation DOM-count), `README.md:50–52` (the anticipated-but-unwired `Deque.AxeCore.Playwright` — **do not add**); `tests/install-playwright.ps1`.
  - `tests/Hexalith.Folders.Contracts.Tests/...OpenApi/SafetyInvariantGateTests.cs` + `tests/fixtures/audit-leakage-corpus.json` + `tests/fixtures/safety-channel-inventory.json` (contract/telemetry metadata-only backstop); `tests/tools/run-safety-invariant-gates.ps1`.
- [Source: docs/ux/ops-console-performance-budget.md] + [Source: _bmad-output/implementation-artifacts/6-10-enforce-console-performance-and-perceived-wait-ux.md] — the **precedent**: shippable testable half + `reference_pending` documented-evidence half; the **486/486** baseline; the environment + forbidden-touch constants; AC #12/#13 deferring formal WCAG/no-mutation release verification to this story; the two optional 6.11 follow-ups.
- [Source: _bmad-output/project-context.md] — repo-wide rules: read-only console boundary, metadata-only, redaction-vs-unknown distinction, Windows-SDK build, central package management, no brittle CSS/text/sleep tests, submodule policy, `.ConfigureAwait(false)`. See also [[scaffold-contract-tests-baseline-reds]], [[dotnet-windows-sdk-wsl]].

## Dev Agent Record

### Agent Model Used

Claude Opus 4.8 (1M context) — `claude-opus-4-8[1m]`.

### Debug Log References

- Baseline UI lane (Windows SDK `10.0.300`, before any change): **486 passed / 0 failed / 0 skipped**
  (`tests/Hexalith.Folders.UI.Tests`) — matches the story baseline exactly.
- After Task 1 (`NoMutationConsoleSweepTests`): filtered run **19 passed** (12 page renders + 4 leakage facts +
  3 file-affordance cases). One compile fix: bUnit 2.7.2 exposes no non-generic `IRenderedFragment`, so the
  cross-page file-affordance assertions were refactored to a generic `IRenderedComponent<T>` helper.
- After Task 2 (`AccessibilityContractSweepTests`): filtered run **14 passed** (10 operator-page structural +
  app-shell source contract + 3 non-color-only/redaction roll-ups).
- Final full UI lane (after all source + doc changes): **519 passed / 0 failed / 0 skipped**, build clean under
  warnings-as-errors (486 baseline + **33** new).

### Completion Notes List

- **Verification-only outcome (AC #1):** the WCAG-2.2-AA structural sweep and the no-mutation sweep surfaced
  **no defect** in `src/Hexalith.Folders.UI/**`. Every console table already carries a `<caption>` + `scope`
  headers, every pagination `<nav>` an `aria-label`, every control is a native keyboard-reachable element with
  an accessible name, decorative icons are `aria-hidden`, and the five-selector command-suppression guard
  renders empty on all twelve surfaces. **No production console code was changed** — the story added only
  verification tests + one release-validation evidence doc. The optional `SkeletonState` dispose-guard
  hardening noted in the 6.10 review was confirmed to be a non-WCAG/non-mutation defensive item and was
  **deliberately not pulled in** (scope guard, AC #1/#13).
- **Automated half (shippable, green):** `NoMutationConsoleSweepTests` (19) + `AccessibilityContractSweepTests`
  (14) = 33 new bUnit tests on the already-pinned bUnit 2.7.2 + AngleSharp. `<html lang="en">` + responsive
  viewport (`App.razor`) and `FocusOnNavigate Selector="h1"` (`Routes.razor`) are asserted against the source
  contract because they render in the host document, not an isolated bUnit page render.
  `NavigationContractTests.Console_DoesNotRegisterAnyDomainCommandManifest` stays green and is **cited, not
  duplicated**. `SafetyInvariantGateTests` is cited as the contract/telemetry backstop (it does not cover the
  rendered DOM — the DOM leakage assertions are the new coverage).
- **Task 3 (optional Playwright viewport smoke) — documented-manual fallback (AC #8/#14):** no Playwright
  browser binary is provisioned in the build environment and `Directory.Packages.props` is a forbidden touch,
  so per the task's explicit alternative the responsive-viewport check is recorded as documented manual
  evidence (the non-brittle testid-presence smoke **method** is defined in the doc, `reference_pending`). No
  E2E test was added; the UI lane stays green. This is logged, not silently dropped.
- **Manual release-validation half (`reference_pending`, method defined — no fabricated pass):** keyboard-only
  J1/J2/J3 walkthroughs, screen-reader review of summary/folder/redaction/audit, real-width responsive visual,
  browser zoom 125/150/200%, and forced-colors/high-contrast + color-blindness require a real browser /
  assistive tech at release time. Their automated structural prerequisites are verified; the human passes are
  `reference_pending` with the method stated — matching the 6.10 discipline and the C0–C13 governance convention.
- **Gate-type fidelity (AC #12/#13):** the doc names the gate type **release-validation evidence before MVP
  acceptance** (`prd.md L780`); C1/C2/C5 capacity numbers stay `reference_pending` (Workstream 7); no CI a11y
  gate, no a11y package, no French strings (localization entry points documented; English MVP).
- **Forbidden-touch guards (AC #13/#14):** `git diff --stat` is empty for
  `src/Hexalith.Folders.Client/Generated/`, `Compat/`, `Directory.Packages.props`, the OpenAPI spine, and all
  `*.csproj`/`*.slnx`; no new `<PackageReference>`/`<ProjectReference>`; no `.razor.css`. The two pre-existing
  `Hexalith.Folders.IntegrationTests`/`LoadTests` `ScaffoldContractTests` reds are unrelated to 6.11
  (`[[scaffold-contract-tests-baseline-reds]]`).
- **Test count:** before **486/486**, after **519/519** (+33), Windows SDK, zero warnings.

### File List

**Added (tests):**

- `tests/Hexalith.Folders.UI.Tests/ConsoleSweepFixtures.cs` — shared happy-path `IClient` stubs + metadata-only
  synthetic DTO builders + redacted-sentinel builders + the Home `IHostEnvironment` helper, consumed by both
  sweeps.
- `tests/Hexalith.Folders.UI.Tests/NoMutationConsoleSweepTests.cs` — Task 1: the five-selector no-mutation sweep
  over all twelve surfaces + rendered-DOM leakage assertions + no-file-browse/diff/edit/repair/download
  assertions.
- `tests/Hexalith.Folders.UI.Tests/AccessibilityContractSweepTests.cs` — Task 2: the WCAG 2.2 AA structural
  sweep over the ten operator pages + app-shell source contract + the non-color-only / redaction-distinction
  roll-ups.

**Added (docs):**

- `docs/ux/ops-console-accessibility-and-no-mutation-verification.md` — Tasks 4 + 5: the release-validation
  evidence artifact (no-mutation result + method, WCAG 2.2 AA evidence, the three keyboard journeys, the
  screen-reader review, responsive + dense-id/long-path, zoom, forced-colors/high-contrast + color-blindness,
  focus management, the UX-DR31/UX-DR32 (+ UX-DR11/UX-DR23/UX-DR30) → result traceability table, and the
  actually-run automated results).

**Modified (tracking, outside the story file):**

- `_bmad-output/implementation-artifacts/sprint-status.yaml` — `6-11-…` status `ready-for-dev` → `in-progress`
  → `review`; `last_updated` refreshed.

> **QA automation pass (`qa-generate-e2e-tests`, 2026-05-29 — Task 3 / AC #8 gap fill).** The dev pass left the
> *optional* Playwright responsive-viewport smoke unautomated (documented-manual `reference_pending`). The QA
> pass **automated** it; standing it up against the hermetic host surfaced a genuine availability defect, fixed
> minimally with the user's approval. **This amends the "no production source change" statement below.**
>
> **Added (tests):**
> - `tests/Hexalith.Folders.UI.E2E.Tests/Responsive/ResponsiveViewportSmokeTests.cs` — the non-brittle
>   responsive-viewport smoke (7 surface routes × 4 widths = 28 cases; root + single `<h1>` + zero mutation
>   affordances per width). Full UI E2E lane **40/40** green (headless Chromium, Windows SDK).
>
> **Modified (tests):**
> - `tests/Hexalith.Folders.UI.Tests/WorkspacePageTests.cs` — +1 `TransportFailure_RendersReadModelUnavailable` regression guard.
> - `tests/Hexalith.Folders.UI.Tests/FolderDetailPageTests.cs` — +1 `TransportFailure_RendersReadModelUnavailable` regression guard.
> - `tests/Hexalith.Folders.UI.E2E.Tests/Fixtures/AspireConsoleHostFixture.cs` — configure a valid-but-unreachable
>   SDK base address so reads fail as `HttpRequestException` (→ §3.8 degradation), not an unconfigured-base-address
>   `InvalidOperationException` (→ 500); unblocked the pre-existing smoke tests.
>
> **Modified (production — minimal, scoped availability fix surfaced by the E2E smoke):**
> - `src/Hexalith.Folders.UI/Components/Pages/Workspace.razor.cs`
> - `src/Hexalith.Folders.UI/Components/Pages/FolderDetail.razor`
>   Both now catch `HttpRequestException`/`TaskCanceledException` → the existing `ReadModelUnavailable` empty
>   state (was: HTTP 500 on an unreachable read model), mirroring the five sibling diagnostic pages.
>
> **Added (docs):** `_bmad-output/implementation-artifacts/tests/6-11-test-summary.md` (+ canonical
> `tests/test-summary.md`). `docs/ux/ops-console-accessibility-and-no-mutation-verification.md` updated (§7, §12,
> §13, UX-DR31 row) to reflect the now-automated responsive smoke + the fix.
>
> **Counts:** bUnit lane **519 → 521** (+2 regression guards); UI E2E lane **→ 40/40**. Scope guards still
> clean (no `Generated/` / `Directory.Packages.props` / OpenAPI spine / `*.csproj` / `*.slnx` / `.razor.css`
> change; no new package/project reference).

The dev pass itself created/modified no production source (`src/Hexalith.Folders.UI/**`) — verification surfaced
no defect at the bUnit layer. The **QA automation pass** then made the one scoped production availability fix
recorded above (Workspace + FolderDetail) after the E2E smoke surfaced the 500-on-unreachable-read-model defect.
No `*.csproj` was modified (the new `.cs` files are picked up by the existing SDK-style glob).

## Senior Developer Review (AI)

**Reviewer:** Jerome — **Date:** 2026-05-29 — **Mode:** story-automator adversarial review (auto-fix). **Outcome: Approve → done.**

**Issues:** 0 Critical · 0 High · 0 Medium · 3 Low (informational). No code fixes were required and none were fabricated (verification-only scope; AC #1/#13 forbid scope creep).

**Independent verifications performed (claims attacked, not trusted):**

- **Gate re-run, not taken on faith:** re-ran `dotnet test tests/Hexalith.Folders.UI.Tests` on the Windows SDK (`/mnt/c/Program Files/dotnet/dotnet.exe`) → **`Passed: 521, Failed: 0, Skipped: 0`, 0 warnings** (warnings-as-errors), matching the Dev Agent Record exactly. The 2 transport-failure regression guards are inside this green count.
- **New `catch (HttpRequestException)` blocks are reachable, not dead code:** confirmed `HexalithFoldersApiException : System.Exception` (`Generated/HexalithFoldersClient.g.cs:13278`), so it does not shadow the new transport-exception catches; the `OperationCanceledException` filtered catch does not shadow `catch (TaskCanceledException)`. Catch ordering is valid.
- **Fix consistency with the claimed sibling:** `AuditTrail.razor.cs` genuinely carries the same `HttpRequestException`/`TaskCanceledException` catches → `Workspace`/`FolderDetail` now match the §3.8 degradation contract uniformly across all seven SDK-backed pages.
- **Fix lands on a real surface:** `read_model_unavailable` is a real `data-fc-empty-reason` token in `ConsoleEmptyState.razor.cs:37`.
- **Tests assert against real testids:** every load-bearing testid (`incident-degraded-mode-banner`, `console-page-{incident-stream,audit-trail,operation-timeline}-row`, `console-page-provider-credential-reference`) exists in source.
- **No-mutation/a11y sweeps are non-trivially green:** `AssertReadOnlyWhenPopulated`/`AssertStructuralA11yWhenPopulated` wait for the *populated-surface* testid before asserting, so the sweeps cannot pass on an empty/error render; the leakage tests inject real sentinels and assert `Markup.ShouldNotContain(sentinel)`; the redaction test proves four distinct `data-fc-disclosure` tokens with the lock only on `redacted`.
- **Forbidden touches clean (AC #13):** `git status` + a clean E2E project build (0 warnings/0 errors) confirm no change to `*.csproj`/`*.slnx`/`Generated/`/`Compat/`/`Directory.Packages.props`/OpenAPI spine/`.razor.css`; no new package/project reference.
- **File List fidelity (the recurring Epic-6 ding):** every source/test/doc file in git is documented in the File List; the `.claude/` lock deletion and `_bmad-output/*` edits are tooling artifacts (excluded from review).
- **Evidence doc discipline (AC #12):** names the gate type (release-validation evidence before MVP acceptance, `prd.md L780`), uses synthetic identifiers only, marks every manual check `reference_pending` with a defined method, fabricates no pass; counts are internally consistent (486 + 33 + 2 = 521; E2E 40).

**Low (informational — not defects, deliberately not "fixed"):**

1. AC #4's literal "no click-only `<div>`/`<span>`" is asserted only as a proxy (native controls have accessible names + the five-selector guard). Asserting absence of click handlers via rendered DOM is infeasible in bUnit/Blazor Server (delegate `onclick` emits no DOM attribute); the proxy is sound.
2. App-shell facts (`<html lang>`, viewport meta, `FocusOnNavigate`) are asserted against source text, not rendered DOM — a documented limitation (they render in the host document, outside an isolated page render).
3. The E2E `40/40` claim was not independently re-run here (needs Windows-Chromium provisioning; optional/non-blocking lane per AC #14). The E2E project was confirmed to **build** cleanly, and the production fix is independently locked by the two green bUnit regression guards.

## Change Log

| Date | Change |
| --- | --- |
| 2026-05-29 | Senior Developer Review (AI, story-automator adversarial auto-fix). Re-ran the UI lane on the Windows SDK → 521/521, 0 warnings; independently verified catch reachability/ordering, sibling-mirror consistency, real testids/empty-state token, forbidden-touch cleanliness (incl. a clean E2E build), File-List fidelity, and evidence-doc discipline. 0 Critical / 0 High / 0 Medium; 3 Low informational. No code defects → no fixes applied (verification-only scope). Status → done. |
| 2026-05-29 | Story 6.11 dev-story executed. Added the no-mutation enforcement sweep (`NoMutationConsoleSweepTests`), the WCAG 2.2 AA structural + non-color-only/redaction sweep (`AccessibilityContractSweepTests`), and the shared `ConsoleSweepFixtures`; created the release-validation evidence doc `docs/ux/ops-console-accessibility-and-no-mutation-verification.md`. UI lane 486→519 (+33), zero warnings, Windows SDK. No production behaviour change (no defect surfaced). Optional Playwright responsive smoke recorded as documented manual evidence per AC #14. Status → review. |
| 2026-05-29 | QA automation pass (`qa-generate-e2e-tests`, Task 3 / AC #8 gap fill). Automated the optional responsive-viewport smoke (`ResponsiveViewportSmokeTests`, 28 cases). It surfaced an availability defect — `Workspace` + `FolderDetail` returned HTTP 500 on an unreachable read model instead of degrading to §3.8 — fixed minimally (transport-exception catches mirroring `AuditTrail`) with the user's approval, plus a test-fixture base-address fix and 2 bUnit regression guards. bUnit lane 519→521 (+2); UI E2E lane 40/40 green. Evidence doc + traceability updated; test-summary written. Scope guards clean. |
