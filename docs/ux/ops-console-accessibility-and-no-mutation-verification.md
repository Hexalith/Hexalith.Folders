# Operations Console — No-Mutation & WCAG 2.2 AA Verification (Story 6.11 / UX-DR31 / UX-DR32)

**Status:** Release-validation evidence artifact. Records the no-mutation enforcement result and the WCAG 2.2
AA conformance method/result for the read-only operations console. The **gate type is release-validation
evidence before MVP acceptance** (`prd.md L780`) — accessibility and operations-console usability are on the
**release-evidence** path, **not** the mandatory-automated-CI bucket (`prd.md L779` omits accessibility;
`architecture.md L55`/`L1503` confirm the "automated **or** release-validation evidence" path; Console-Accessibility
NFR coverage at `L1502`).

This story adds **no** CI accessibility gate and **no** a11y package — there is no axe / Deque / pa11y /
lighthouse / Playwright-accessibility dependency on the graph and `Directory.Packages.props` is a forbidden
touch. The automated half is built with the already-pinned **bUnit 2.7.2 + AngleSharp** (DOM/attribute
assertions). The inherently-manual half (real-browser zoom, forced-colors, color-blindness, screen-reader,
real-width responsive **visual** assessment) is recorded here as `reference_pending` with the **method
defined** — mirroring the Story 6.10 `reference_pending` discipline and the
`docs/exit-criteria/c0-c13-governance-evidence.yaml` convention. **No pass is fabricated.** All identifiers in
this doc and in the verification tests are **synthetic** (`acme`, `tenant-a`, `folder-1`, `workspace-1`, …); no
real tenant / folder / credential / path / audit data appears.

## Document control

| Field | Value |
| --- | --- |
| Workstream | Story 6.11 (verify no-mutation enforcement and accessibility) |
| Owns | UX-DR31 + UX-DR32 verification; UX-DR11 / UX-DR23 / UX-DR30 console verification |
| Related | architecture.md F-1..F-7 (L545–L551), no-mutation hard boundaries (L88–L92), release-validation path (L55, L1502, L1503); prd.md accessibility NFR (L768–L774), verification expectations (L776–L780); ux-design-specification.md UX-DR30/DR31/DR32 (L138–L140); docs/ux/ops-console-wireflows.md §5 (L602–L661), §6 (L665–L763) |
| Defers to | Workstream 7 (release rollup, C1/C2/C5 capacity numbers — `reference_pending`) |
| Method | Automated (bUnit/AngleSharp, **actually run**) **+** release-validation manual evidence (`reference_pending`, method defined) |
| Gate type | **Release-validation evidence before MVP acceptance** (`prd.md L780`) — **not** a CI gate |

---

## 1. No-mutation enforcement — the seven prohibited paths (UX-DR11 / UX-DR23 / F-2)

**Result: VERIFIED (automated).** The console exposes no mutation path, credential reveal, file-content
browsing, file editing, raw-diff display, hidden repair action, or unrestricted filesystem browse.

**Method (three layers, all green):**

1. **Five-selector command-suppression sweep** — `NoMutationConsoleSweepTests` renders **every** console
   surface in its fully-populated happy-path state and asserts
   `ConsoleTestAssertions.ShouldHaveNoMutationAffordances()` (the five selectors `form`, `fluentinputform`,
   `fluentdialog`, `[data-fc-command]`, `[data-fc-mutation]` all render empty) plus a single `<h1>` and the
   page root, on all **twelve** surfaces: the ten operator pages (`Home`, `Tenants`, `Folders`, `FolderDetail`,
   `Workspace`, `AuditTrail`, `OperationTimeline`, `Provider`, `ProviderSupport`, `IncidentStream`) **and** the
   two dev-only galleries (`RedactionGallery`, `StateLabelGallery`).
2. **Registry-empty structural proof** — `NavigationContractTests.Console_DoesNotRegisterAnyDomainCommandManifest`
   asserts the composition root registers **zero** domain command manifests (`registry.GetManifests()` is
   empty). The console references `Hexalith.Folders.Client` only, reads from projections (F-2), never calls
   `AddHexalithEventStore`, so no `/api/v1/commands` endpoint exists. (Cited, not duplicated.)
3. **Rendered-DOM leakage assertions** — injected redacted **sentinels** never reach the DOM:
   `RedactedActor_NeverLeaksValue_OnAuditTrail`, `RedactedWorkspace_NeverLeaksValue_OnOperationTimeline`,
   `RedactedWorkspace_NeverLeaksValue_OnIncidentStream_DespiteDegradedMode`,
   `RedactedCredentialReference_RendersReferenceIdentifierOnly_NeverTheSecret_OnProvider` (the provider
   credential reference renders as a **non-secret reference identifier**, never a secret). The structural
   absence of file-browse / raw-diff / file-edit / repair / download affordances is asserted by
   `EvidenceTablePage_RendersNoFileBrowse_RawDiff_FileEdit_Repair_OrDownloadAffordance` (no `a[download]`, no
   `textarea`, no `input[type=file]`).

**Backstop (cited):** the contract/telemetry-layer metadata-only gate `SafetyInvariantGateTests`
(`Hexalith.Folders.Contracts.Tests`) scans OpenAPI examples / generated SDK / audit & telemetry channels. It
does **not** cover the rendered console DOM — the rendered-DOM assertions above are the coverage Story 6.11
adds.

## 2. WCAG 2.2 AA — automated structural conformance (UX-DR30)

**Result: VERIFIED (automated).** `AccessibilityContractSweepTests` asserts the WCAG 2.2 AA structural
invariants **uniformly** across all ten operator pages:

| Invariant | Assertion |
| --- | --- |
| Semantic heading | exactly one `<h1>` per page |
| `lang` + responsive viewport | `<html lang="en">` + `name="viewport"` `width=device-width` (`App.razor`, asserted against source) |
| Focus on navigation | `FocusOnNavigate Selector="h1"` wired (`Routes.razor`, asserted against source) |
| Readable tables | every `<table>` has a `<caption>` and every `<th>` carries `scope` |
| Landmark/pagination nav | every `<nav>` carries an `aria-label` |
| Keyboard-reachable controls | every `<button>` / `<a>` / `<input>` is a native element with an accessible name (visible text, `aria-label`/`title`, or an associated `<label for>`); anchors carry `href` |
| Read-only | the five-selector no-mutation guard renders empty |

The dev-only galleries render through `FluentDataGrid` (Fluent UI — **F-3**, the WCAG 2.2 AA foundation that
"satisfies WCAG 2.2 AA targets") rather than hand-authored semantic tables, so they are verified for
no-mutation + heading structure (§1) and inherit Fluent UI's grid accessibility; they are intentionally out of
scope for the hand-authored-markup structural sweep.

## 3. Non-color-only indicators + redaction distinction (UX-DR14 / UX-DR10 / UX-DR22 / F-4 / F-5)

**Result: VERIFIED (automated).**

- `RedactedField_FourStates_DistinctTokens_LockOnlyOnRedacted_AndNeverLeaksValue` — the four `FieldDisclosure`
  states render **distinct** `data-fc-disclosure` tokens (`visible` / `redacted` / `unknown` / `missing`); the
  **lock icon renders only on redacted**; a redacted value never leaks; every non-visible state carries a
  screen-reader-meaningful `aria-label`. Redaction is therefore visibly **and** semantically distinct from
  unknown / missing — silent truncation or representing redaction as missing cannot happen (F-5).
- `OperatorDispositionBadge_EveryDisposition_RendersTextLabel_Slot_AndAriaLabel_NotColorAlone` — every operator
  disposition conveys via a visible **text label** + a non-color **slot token** (shape) + an `aria-label`.
- `TrustMatrix_EveryDimensionState_RendersIconAndLabel_NotColorAlone` — every trust-dimension state conveys via
  an **icon (shape)** + a resolved **text label**.
- Reinforced by the existing per-component a11y tests rolled into the contract: `RedactedFieldTests`,
  `TrustMatrixTests`, `OperatorDispositionBadgeTests`, `StillLoadingCancelTests` (`type=button` + `aria-label` +
  `role=status` + non-color), `SkeletonStateTests` (`aria-busy` + `aria-live`).

## 4. Focus management (UX-DR24 / UX-DR30)

**Result: VERIFIED (automated, structural) + `reference_pending` (manual reading-order walkthrough).** The
`FocusOnNavigate Selector="h1"` wiring (focus lands on the page `<h1>` after navigation) and the
single-`<h1>` + native-focusable-controls invariants are asserted automatically (§2). The MVP console has
**no dialogs** — the no-mutation sweep asserts zero `fluentdialog` on every page, so there is no focus trap to
manage; **any future dialog must trap + restore focus per UX-DR24.** The visible-focus and reading-order
**observation** in a real browser is part of the keyboard walkthroughs (§5).

## 5. Three critical keyboard-only journeys (UX-DR32 / UX-DR30 / UX-DR24)

**Result: `reference_pending` (real-browser keyboard pass) — method defined; structural prerequisites
VERIFIED (automated).** Each journey's pages already satisfy the automated structural prerequisites in §2/§4
(single `<h1>`, `FocusOnNavigate`→`<h1>`, native keyboard-reachable controls with accessible names, no
keyboard trap, no dialog). The release-time human walkthrough records, per journey: sensible tab order,
always-visible focus, `FocusOnNavigate` landing focus on the `<h1>` after navigation, every interactive
element reachable + operable by keyboard, and no keyboard trap.

| Journey | Route path | Terminal evidence surface |
| --- | --- | --- |
| **J1 — Find Workspace & Inspect Trust State** | `/folders` → `/folders/{id}` → `/folders/{id}/workspaces/{wid}` | `WorkspaceTrustSummary` + `TrustMatrix` |
| **J2 — Prove Tenant Isolation & Safe Folder Visibility** | `/folders/{id}` (+ `TenantScopeBanner`, `MetadataOnlyFolderTree`, denied-without-leakage) → `/providers/support` / `/folders/{id}/provider` | provider readiness |
| **J3 — Diagnose Workspace Failure from Evidence** | `/folders/{id}/audit-trail` + `/folders/{id}/operation-timeline` + `/_admin/incident-stream?folder={id}` (F-6) | incident-mode last-resort read |

## 6. Screen-reader review — summary / folder / redaction / audit flows (UX-DR32)

**Result: `reference_pending` (real assistive-tech pass) — method defined; structural prerequisites VERIFIED
(automated).** The headings/landmarks (`<section aria-label>`, `role="status"/"alert"/"note"`,
`<nav aria-label>`), status labels, and redaction announcements are asserted structurally (§2/§3). The
release-time screen-reader review confirms the announcements are meaningful end-to-end on each flow.

| Flow (AC) | Surface(s) | Confirms |
| --- | --- | --- |
| **summary** | `Workspace` (`WorkspaceTrustSummary`, `TrustMatrix`) | headings/landmarks announce structure; status labels meaningful |
| **folder** | `FolderDetail` + `MetadataOnlyFolderTree` | tree depth/landmarks announced; long paths reachable via `title` |
| **redaction** | `RedactedField` everywhere (+ `RedactionGallery`, `Provider` credential reference) | redaction announces "Hidden by tenant policy" distinctly from unknown/missing |
| **audit** | `AuditTrail` (+ `OperationTimeline`, `IncidentStream`) | chronological (newest-first) order preserved for assistive tech; decorative icons `aria-hidden` |

## 7. Responsive widths + dense identifiers + long paths (UX-DR31 / UX-DR28 / UX-DR29)

**Result: structural responsive smoke VERIFIED (automated, E2E) + real-width visual `reference_pending`.** The
real-width **visual** assessment remains documented manual evidence at **desktop** (1024 / 1280 / 1440 / wide),
**tablet** (768–1023), and **mobile fallback** (~360 / ~430 px) widths (`ux-design-specification.md` L758–L764),
confirming the tablet/mobile fallback stacks panels, collapses nav, preserves search/filters, prioritizes
tenant/workspace/state/risk, and does not break core lookup/trust review; and that dense identifiers + long
paths remain readable/usable (truncation only where the full value stays available via safe-copy / `title`
tooltip / details) across the four named surfaces: **tables** (the six `<table>`s), **timelines**
(`OperationTimeline`, `IncidentStream`), **metadata trees** (`MetadataOnlyFolderTree`, indented by depth +
`title="@fullPath"`), and **trust summaries** (`WorkspaceTrustSummary` + `TrustMatrix`).

**Automated (now run — QA E2E automation pass):** `ResponsiveViewportSmokeTests` (the non-brittle Playwright
viewport-width smoke) drives the seven routes that render the four named surfaces — `folders` (tables),
`folder-detail` (metadata tree), `workspace` (trust summary), `audit-trail` / `provider-support` (tables),
`operation-timeline` / `incident-stream` (timelines) — across **desktop 1280**, **tablet 768**, and the **two
mobile-fallback widths (430 / 360)**, asserting at every width that the page **root resolves**, exactly **one
`<h1>`** renders, and the **read-only boundary holds** (zero `form` / `fluent-dialog` / `[data-fc-command]` /
`[data-fc-mutation]`) — **testid/structure presence only, no pixel/overflow/CSS/text/sleep assertions**
(project-context). It reuses `AspireConsoleHostFixture` (hermetic Kestrel host) + `PlaywrightFixture` +
`ConsoleRoutes.cs`, modelled on `StateLabelGalleryE2ETests`. **Result: the full UI E2E lane is 40/40 green**
(headless Chromium, Windows SDK). Browser binaries are provisioned once via `tests/install-playwright.ps1`
(here: `pwsh playwright.ps1 install chromium`).

> **Defect found & fixed by this automated pass (recorded for transparency).** Standing up the responsive smoke
> against the hermetic (backend-less) host surfaced a genuine availability defect: **`Workspace` and
> `FolderDetail` did not catch `HttpRequestException` / `TaskCanceledException`** on their SDK reads (unlike the
> five sibling diagnostic pages), so an **unreachable read model crashed those two pages to HTTP 500** instead of
> degrading to the §3.8 read-model-unavailable state. This is operator-facing — especially under F-6 incident
> mode (inspecting a workspace during an outage). **Fix (minimal, scoped, mirrored on `AuditTrail`):** both pages
> now catch the transport exceptions and fall through to the existing `ReadModelUnavailable` empty state. Locked
> in by two new bUnit regression tests (`WorkspacePageTests.TransportFailure_RendersReadModelUnavailable`,
> `FolderDetailPageTests.TransportFailure_RendersReadModelUnavailable`) — the §3.8 degradation contract is now
> asserted uniformly across all **seven** SDK-backed pages. A test-fixture fix was also applied
> (`AspireConsoleHostFixture` now configures a valid-but-unreachable SDK base address so reads fail as the
> realistic `HttpRequestException` rather than an unrealistic unconfigured-base-address `InvalidOperationException`).

## 8. Browser zoom 125% / 150% / 200% (UX-DR31 / UX-DR30)

**Result: `reference_pending` (manual) — method defined.** At **exactly** 125%, 150%, and 200% browser zoom
(`ux-design-specification.md` L763), confirm text, controls, tables, and the three journeys (§5) remain
**readable and usable** — no clipping, overlap, or loss of function. No automated zoom tool is available
(forbidden to add one).

## 9. Forced-colors / high-contrast + color-blindness (UX-DR32 / UX-DR14)

**Result: `reference_pending` (manual) — method defined.** Forced-colors / high-contrast checks **where the
browser supports it** (the AC's "where supported" caveat — the wireflows define no per-page forced-colors
contract beyond the non-color-only rule, `ops-console-wireflows.md` L657–L658). Color-blindness review confirms
semantic states stay distinguishable **without colour**, leaning on the automated text+icon/shape evidence in
§3 (UX-DR14). Manual evidence — no automated tool available.

## 10. Localization entry points (English MVP)

The console is **English-only for MVP** (`ops-console-wireflows.md` L776); French is deferred. This story adds
**no French strings.** Localization entry points are the existing user-facing string literals in the page/
component `.razor` files under `src/Hexalith.Folders.UI/Components/` (page headings, section labels, disclosure
explanations such as "Hidden by tenant policy", freshness labels, empty/error copy) — the points a future
localization pass would externalize. No localization resource files are introduced by 6.11.

## 11. Traceability — UX-DR → result

| Requirement | Scope | Result | Evidence |
| --- | --- | --- | --- |
| **UX-DR11** | Read-only boundary (seven prohibited paths) | **VERIFIED (automated)** | §1 — `NoMutationConsoleSweepTests`, `NavigationContractTests`, leakage assertions |
| **UX-DR23** | No-mutation forms | **VERIFIED (automated)** | §1 — five-selector sweep |
| **UX-DR30** | WCAG 2.2 AA (headings, tables, contrast, focus, non-color-only, keyboard) | **VERIFIED (automated, structural)** + manual contrast/zoom `reference_pending` | §2, §3, §4, §8 |
| **UX-DR31** | Widths + 125/150/200% zoom + dense-id/long-path | structural responsive smoke **VERIFIED (automated, E2E 40/40)**; real-width **visual** + zoom **`reference_pending`** — method defined | §7, §8 |
| **UX-DR32** | Automated + keyboard journeys + screen-reader + forced-colors + color-blindness + focus | **automated checks VERIFIED**; keyboard/SR/forced-colors/color-blindness **`reference_pending`** — method defined | §2–§6, §9 |
| UX-DR10 / UX-DR14 / UX-DR22 | Redaction distinction + non-color-only | **VERIFIED (automated)** | §3 |
| UX-DR24 | Focus management | **VERIFIED (automated, structural)** + manual reading-order `reference_pending` | §4, §5 |

## 12. Automated verification — actually-run results

Run on the Windows SDK (`/mnt/c/Program Files/dotnet/dotnet.exe`, `global.json` pin `10.0.300`).

```yaml
# Release-validation evidence (Story 6.11). Automated results are actually run; manual checks that require a
# real browser / assistive-tech pass at release time are reference_pending with the method defined above.
ops_console_accessibility_and_no_mutation:
  gate_type: release-validation-evidence-before-mvp-acceptance   # prd.md L780 (not a CI gate)
  automated:
    status: verified
    bunit_lane:
      test_lane: tests/Hexalith.Folders.UI.Tests
      ui_lane_total: 521        # 486 baseline + 33 (Story 6.11 dev) + 2 (QA-pass transport-failure regression guards)
      ui_lane_passed: 521
      ui_lane_failed: 0
      new_tests: 35
      suites:
        - NoMutationConsoleSweepTests            # 19 (12 page renders + 4 leakage + 3 file-affordance cases)
        - AccessibilityContractSweepTests        # 14 (10 operator-page structural + app-shell + 3 non-color/redaction)
        - WorkspacePageTests.TransportFailure_RendersReadModelUnavailable        # +1 (QA-pass regression guard)
        - FolderDetailPageTests.TransportFailure_RendersReadModelUnavailable     # +1 (QA-pass regression guard)
    e2e_lane:
      test_lane: tests/Hexalith.Folders.UI.E2E.Tests
      e2e_lane_total: 40        # full UI E2E lane (headless Chromium, Windows SDK)
      e2e_lane_passed: 40
      e2e_lane_failed: 0
      new_suite: ResponsiveViewportSmokeTests    # 28 cases (7 surface routes x 4 viewport widths: 1280 / 768 / 430 / 360)
      note: "Non-brittle structural responsive smoke (root + single <h1> + zero mutation affordances per width). Provision browsers once via tests/install-playwright.ps1. The deferred-active E2E lane is not in the blocking CI gate (E2E README); the bUnit lane is the always-run regression guard."
    cited_not_duplicated:
      - NavigationContractTests.Console_DoesNotRegisterAnyDomainCommandManifest
      - Hexalith.Folders.Contracts.Tests/.../SafetyInvariantGateTests   # contract/telemetry backstop; does not cover rendered DOM
  production_fixes:                # QA-pass: defect surfaced by the responsive E2E smoke (see §7)
    - "Workspace.razor.cs + FolderDetail.razor now catch HttpRequestException/TaskCanceledException -> §3.8 read-model-unavailable (was: HTTP 500 on unreachable read model). Minimal, mirrored on AuditTrail."
    - "tests/.../AspireConsoleHostFixture.cs configures a valid-but-unreachable SDK base address so reads fail as HttpRequestException, not an unconfigured-base-address InvalidOperationException."
  manual:
    keyboard_journeys_j1_j2_j3:
      status: reference_pending
      note: "TODO(reference-pending): record real-browser keyboard-only walkthrough per journey; structural prerequisites verified automated (FocusOnNavigate->h1, native controls, no dialog/trap)."
    screen_reader_summary_folder_redaction_audit:
      status: reference_pending
      note: "TODO(reference-pending): record assistive-tech review of the four flows; structural landmarks/labels/announcements verified automated."
    responsive_widths_dense_id_long_path:
      status: partially_automated
      automated: "ResponsiveViewportSmokeTests (E2E) verifies structural integrity (root + single <h1> + read-only boundary) at 1280/768/430/360 across the four named surfaces — 40/40 green."
      note: "TODO(reference-pending): real-width VISUAL pass (clip/overlap/loss-of-function, truncation-with-recoverable-full-value) at desktop/tablet/mobile-fallback remains a release-time human check."
    browser_zoom_125_150_200:
      status: reference_pending
      note: "TODO(reference-pending): manual zoom at 125/150/200% — no automated zoom tool available."
    forced_colors_high_contrast_color_blindness:
      status: reference_pending
      note: "TODO(reference-pending): forced-colors/high-contrast where supported + color-blindness review; non-color-only evidence verified automated."
  deferred:
    - "No CI accessibility gate, no a11y/axe package (Directory.Packages.props forbidden)."
    - "C1/C2/C5 capacity numbers — reference_pending (Workstream 7 release rollup)."
```

## 13. Out of scope (deferred)

- **No CI accessibility gate and no a11y/contrast/zoom package** — release-validated; `Directory.Packages.props`
  is a forbidden touch, so `Deque.AxeCore.Playwright` (anticipated but never wired in the E2E README) is **not**
  added.
- **No pinned C1/C2/C5 capacity numbers** — `reference_pending` (Workstream 7 owns the release rollup).
- **Production console behaviour change — one scoped availability fix (QA automation pass).** The bUnit
  no-mutation + WCAG-structural sweeps surfaced **no** defect. The subsequent QA E2E automation pass (the
  responsive smoke against the hermetic host) surfaced an **availability defect** — `Workspace` + `FolderDetail`
  crashed (HTTP 500) on an unreachable read model instead of degrading to the §3.8 state — which was fixed
  minimally (transport-exception catches mirroring the sibling pages) and locked in by regression tests (§7,
  §12). This is a genuine read-model-availability/resilience fix, **not** a new feature, page, route, component,
  endpoint, SDK method, or package; no forbidden artifact was touched.
- **No French strings** — English MVP; localization entry points documented (§10).
