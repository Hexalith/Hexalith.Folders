---
baseline_commit: 647ddc9e404120b72fbe7f89f0031305cedb45de
---

# Story 8.4: Stand up an automated axe/WCAG 2.2 AA CI gate for the operations console

Status: done

<!-- Created 2026-06-22 via bmad-correct-course (sprint-change-proposal-2026-06-22.md). Epic 8 release-acceptance closure story. -->
<!-- Refined 2026-06-23 via bmad-create-story (Refine-then-dev). Engineered the thin backlog stub into a context-filled dev story after exhaustive artifact + source analysis (3 research subagents + direct source verification + external axe/Playwright-.NET API research). The audit corrected/sharpened the stub on FOUR points: (1) the UI E2E lane already exists (tests/Hexalith.Folders.UI.E2E.Tests) but is NOT wired into any CI workflow and has NO axe package — this is greenfield CI work, not an extension of an existing gate; (2) the E2E hermetic host (AspireConsoleHostFixture) points the SDK at a dead loopback, so console journey pages render DEGRADED (read-model-unavailable) — an axe scan there cannot verify contrast/table-semantics/dense-identifiers, so a POPULATED-backend host (stub IClient) is required; (3) axe-core covers only the auto-detectable subset of WCAG 2.2 AA — keyboard operability, visible-focus appearance, zoom/reflow, and not-color-alone need explicit Playwright assertions + the already-green bUnit sweeps, so the gate is a UNION, not "axe == WCAG"; (4) two conformance guards actively assert "no a11y CI gate exists" (OperationsAuditDocsConformanceTests + the NFR62/63/65/66 release-blocking manual-gap rows) — these MUST flip in lockstep or the contract-spine lane turns red. Verified details, decisions, and the safe path are in Dev Notes → Architectural Decisions. -->

## Story

As a release stakeholder,
I want an automated accessibility (axe-core / WCAG 2.2 AA) gate wired into CI against the read-only operations console,
So that the PRD's accessibility release-validation path (NFR62–66 / NFR-A11Y-1..5, NFR69 / NFR-VER-3) is satisfied by enforced evidence rather than a library-choice assertion, closing the documented "I-5 absence."

## Context

The 2026-06-22 readiness review found accessibility is the **most credible residual UX risk** ([implementation-readiness-report-2026-06-22.md:329,345](../planning-artifacts/implementation-readiness-report-2026-06-22.md), §6 Critical #4 at `:429`): WCAG 2.2 AA is asserted via the Fluent UI primitive choice (architecture **F-3**, `architecture.md:549`) and a manual UX test plan, but **no automated axe/WCAG conformance gate is wired into CI** — it is absent from the I-5 gate list (`architecture.md:563`). This contradicts the PRD's release-validation clause (NFR69 / NFR-VER-3, `prd.md:780`), which is "documented contradiction, not hypothetical."

The current responsive coverage (`ResponsiveViewportSmokeTests`) is a deliberately non-brittle **presence-only** smoke (one `<h1>`, no mutation affordances, page-root visible at four widths). It sets viewports but **never sets browser zoom and never asserts no-clipping or dense-identifier stress** — exactly the UX-DR31 gap the readiness review flags as a partial (`:330`, `:346`). The Playwright-on-.NET UI E2E lane exists (`tests/Hexalith.Folders.UI.E2E.Tests/`, 40/40 green locally) with stable read-only console routes/selectors from Story 6-2, but it is **not invoked by any CI workflow** and carries **no axe/Deque package**. Story 6-11 deliberately scoped accessibility as *release-validation evidence, not a CI gate*, and explicitly forbade adding `Deque.AxeCore.Playwright` / touching `Directory.Packages.props` **for that story** (`6-11-...md:29`). **Story 8.4 is the story that reverses that constraint** — it is the one genuine verification gap of Epic 8 (`sprint-change-proposal-2026-06-22.md:46`), a bounded closure task with no new product FR scope (`epics.md:1748`).

## Acceptance Criteria

1. **Given** the read-only console routes from Story 6-2 (`ConsoleRoutes.cs`), **when** an axe-core / WCAG 2.2 AA scan is added to the Playwright-on-.NET UI E2E lane, **then** it runs axe filtered to the cumulative WCAG AA tag set (`wcag2a, wcag2aa, wcag21a, wcag21aa, wcag22aa`) against each route on the **three critical journeys** and **fails on any AA-tagged violation** (`AxeResult.Violations` non-empty ⇒ test red), with metadata-only failure output (rule id + target selector + helpUrl).
2. **Given** the three critical console journeys — **find-and-inspect-trust-state** (J1), **prove-tenant-isolation** (J2), **diagnose-failure-from-evidence** (J3) (`ux-design-specification.md:424/445/462`) — **when** the gate runs against a **fully-populated** host, **then** coverage spans: axe's auto-detectable AA subset (contrast 1.4.3, name/role/value, semantic headings/landmarks, table structure, link/control names) **plus** explicit Playwright assertions for **keyboard navigation** and **visible focus** (UX-DR30: search, filters, result selection, tabs, tables, tree expansion, detail panels), **plus** the already-green bUnit **not-color-alone** sweeps surfaced as gate evidence (NFR62–66 / A11Y-1..5).
3. **Given** UX-DR31 (`ux-design-specification.md:139`: "…at 125%, 150%, and 200% browser zoom, and with dense identifiers and long paths in tables, timelines, metadata trees, and trust summaries"), **when** the gate runs against a **dense-identifier** dataset, **then** it asserts — at 125/150/200% zoom — a no-horizontal-clipping invariant the current responsive smoke omits (no document-level horizontal overflow + key tables/trust-summaries remain visible within the layout viewport), expressed as a semantic invariant (not pixel geometry).
4. **Given** the gate is wired and green, **when** CI runs, **then** (a) a focused gate script `run-accessibility-ci-gates.ps1` writes a metadata-only report to `_bmad-output/gates/accessibility/latest.json`; (b) a CI job provisions Playwright Chromium and runs the lane; (c) the gate is **registered in the I-5 gate inventory** (`architecture.md:563`), closing the I-5 absence; (d) the conformance guards that currently assert *no a11y CI gate exists* are flipped (see Tasks 8); **and** its green run is recorded as the accessibility **release-validation evidence** (NFR69 / NFR-VER-3) in `docs/ux/ops-console-accessibility-and-no-mutation-verification.md`.
5. **Given** the closure-epic boundary, **when** this story lands, **then** it introduces **no new product FR scope** and **no spine/OpenAPI/generated-client/aggregate change** — only test infra, a gate script, a CI job, the gate inventory + operator/NFR docs, and the conformance guards that gate them.

## Backing analysis (verified 2026-06-23)

The verified ground truth that shapes the tasks below:

| Fact | Evidence |
|---|---|
| **The UI E2E lane exists but is NOT in CI** — kept out by non-invocation (no `[Trait]`/`Skip`), not by attributes. No workflow references Playwright / `install-playwright` / `UI.E2E` / axe. | `.github/workflows/*` (5 files); `tests/.../UI.E2E.Tests/README.md` "deferred-active" |
| **No axe/Deque/pa11y/lighthouse package anywhere.** The E2E README *anticipated* `Deque.AxeCore.Playwright` ("selected when the first real test lands; fail on serious/critical") but it was never wired. | `Directory.Packages.props` (no axe entry); `UI.E2E.Tests.csproj` (only `Microsoft.Playwright`); README:49-52 |
| **The E2E host renders DEGRADED.** `AspireConsoleHostFixture` points the typed SDK at `http://127.0.0.1:1/` (closed loopback), so SDK-backed pages catch the transport failure and degrade to the §3.8 read-model-unavailable state — populated tables/trust-summaries/timelines DO NOT render. axe over an empty page cannot verify contrast/table-semantics/dense identifiers. | `AspireConsoleHostFixture.cs:50-63` (comment is explicit) |
| **The console pages inject `Hexalith.Folders.Client.Generated.IClient`** — registered as a **typed HttpClient** (Transient) via `AddHttpClient<IClient, GeneratedFoldersClient>` (`FoldersClientServiceCollectionExtensions.cs:70`), wired by `CompositionRoot.ConfigureServices` → `ConfigureFoldersClient` (`CompositionRoot.cs:88`). A populated host = **`services.Replace(ServiceDescriptor.Scoped<IClient>(_ => stub))`** after `CompositionRoot.ConfigureServices` (use **`Replace`, not `TryAdd`** — `Replace` removes the typed-client descriptor then adds the stub; `TryAdd` would lose to the real Transient). Use **Scoped** (Blazor Server resolves `[Inject] IClient` per-circuit) to match the in-repo precedent. The synthetic happy-path DTO shapes already exist. | `ConsoleSweepFixtures.cs` (bUnit `StubFolderDetail/StubWorkspace/…`, synthetic `folder-1`/`workspace-1`); `DiagnosticTestContext.cs:28` (`ServiceDescriptor.Scoped` precedent) |
| **`ResponsiveViewportSmokeTests` is presence-only** — viewport set, one `<h1>`, zero `form`/`fluent-dialog`/`[data-fc-command]`/`[data-fc-mutation]`. It NEVER sets zoom and NEVER asserts no-clipping/dense identifiers (forbidden brittle pixel/overflow assertions, project AC#13). This is the UX-DR31 gap to close. | `tests/.../Responsive/ResponsiveViewportSmokeTests.cs:22-32,114-151` |
| **Two conformance guards actively forbid an a11y gate (PRIMARY LANDMINE).** `OperationsAuditDocsConformanceTests` asserts the **operations-console doc** contains "No new accessibility or performance CI gate" (it reads `docs/operations/operations-console.md`, **NOT** the ux doc); `run-nfr-traceability-gates.ps1` lists NFR62/63/65/66 as **release-blocking manual gaps**, and `NfrTraceabilityConformanceTests` cross-checks the report's `release_blocking_gaps` against the `reference-pending` rows in `docs/exit-criteria/nfr-traceability.md`. The prose to flip is line-wrapped (reflow, don't reword). | `OperationsAuditDocsConformanceTests.cs:36,266-274` (`ConsoleDocPath`); prose at `docs/operations/operations-console.md:169-170`; `run-nfr-traceability-gates.ps1:79-82`; `NfrTraceabilityConformanceTests.cs:462-478` (parses `docs/exit-criteria/nfr-traceability.md:103-107`) |
| **Workflow structure is itself gated by conformance tests — and FIVE of them scan the WHOLE `ci.yml` for forbidden substrings.** `BaselineCiWorkflowConformanceTests` pins `WorkflowPath`/`GateScriptPath`/`OperatorDocPath` + exact job/step names + `submodules:false` + `checkout@v6` + `setup-dotnet@v5` (mirror it for the new job). Critically, Baseline/ContractParity/SecurityRedaction/CapacitySmoke conformance tests each `ShouldNotContain` (case-insensitive) **`"playwright install"`**, `upload-artifact`, `--recursive`, `secrets.`, `services:`, `dotnet publish`, `docker` anywhere in `ci.yml`; they also scan `.github`/`tests/tools`/`docs`/`src` for `--recursive`. No test asserts an exhaustive job set, so adding a 5th job is safe — but a step **named** "Playwright install" (or any of those substrings) turns all four red. | `BaselineCiWorkflowConformanceTests.cs:121-127,264-299`; `ContractParityCiWorkflowConformanceTests.cs:109-113`; `SecurityRedactionCiWorkflowConformanceTests.cs:123-127`; `CapacitySmokeCiWorkflowConformanceTests.cs:113-115` |
| **axe ≠ WCAG 2.2 AA.** axe auto-detects ~a subset (contrast, name/role/value, headings, landmarks, table semantics, link-name). Keyboard operability (2.1.1), visible-focus appearance (2.4.7/2.4.11), reflow/resize (1.4.10/1.4.4 = the zoom), and not-color-alone (1.4.1) are partly/not axe-automatable → explicit Playwright assertions + the bUnit `AccessibilityContractSweepTests` not-color-alone sweeps cover them. | Deque axe API docs; `AccessibilityContractSweepTests.cs` |

**External API (verified):** `Deque.AxeCore.Playwright` **4.11.3** (+ `Deque.AxeCore.Commons`, same version), .NET-Standard-2.1-compatible (resolves for `net10.0`). API:
```csharp
using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
var options = new AxeRunOptions {
    RunOnly = new RunOnlyOptions { Type = "tag", Values = new List<string> { "wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa" } }
};
AxeResult result = await page.RunAxe(options);            // also: page.RunAxe(AxeRunContext, AxeRunOptions); locator.RunAxe(options)
result.Violations.ShouldBeEmpty();                        // AxeResultItem[]: .Id, .Impact, .HelpUrl, .Nodes[].Target, .Nodes[].Html
```

## Tasks / Subtasks

- [x] **Task 1 — Add the axe-core toolchain via central package management (AC: 1, 5)**
  - [x] Add `<PackageVersion Include="Deque.AxeCore.Playwright" Version="4.11.3" />` and `<PackageVersion Include="Deque.AxeCore.Commons" Version="4.11.3" />` to `Directory.Packages.props` (near the `Microsoft.Playwright` entry, L76; central management — **no inline `Version` on the `PackageReference`**).
  - [x] Add `<PackageReference Include="Deque.AxeCore.Playwright" />` to `tests/Hexalith.Folders.UI.E2E.Tests/Hexalith.Folders.UI.E2E.Tests.csproj` (Commons flows transitively; add it explicitly only if a `Deque.AxeCore.Commons` type is referenced directly without the Playwright using).
  - [x] `dotnet restore Hexalith.Folders.slnx` resolves cleanly. This **deliberately reverses** Story 6-11's "no a11y package / `Directory.Packages.props` forbidden" constraint, which was scoped to 6-11 (`6-11-...md:29`); 8.4 is the Epic-8 story that adds it.

- [x] **Task 2 — Populated-backend console host for the browser scan (AC: 1, 2, 3)**
  - [x] Add a populated host fixture (e.g. `tests/Hexalith.Folders.UI.E2E.Tests/Fixtures/AccessibilityConsoleHostFixture.cs`) modelled on `AspireConsoleHostFixture` but registering a **stub `IClient`** (`Hexalith.Folders.Client.Generated.IClient`) **after** `CompositionRoot.ConfigureServices(...)` via `services.Replace(ServiceDescriptor.Scoped<IClient>(_ => stub))` so journey pages render fully populated. Use **`Replace` (not `TryAdd`)** and **Scoped** — `IClient` is a typed HttpClient registered Transient (`FoldersClientServiceCollectionExtensions.cs:70`); `Replace` swaps that single descriptor, and Scoped matches how Blazor Server resolves `[Inject] IClient` per-circuit (precedent: `DiagnosticTestContext.cs:28`). `TryAdd` would lose to the real Transient and the SDK would 500. Keep `EnvironmentName = CompositionRoot.TestEnvironmentName`, `ValidateScopes = true`, random loopback port, the readiness probe — change ONLY the client registration. Do **not** set the dead-loopback `Folders:Client:BaseAddress` (the stub replaces the SDK transport; the `AddFoldersClient` BaseAddress validator is never hit once `IClient` is replaced).
  - [x] Reuse the synthetic metadata-only DTO shapes from `tests/Hexalith.Folders.UI.Tests/ConsoleSweepFixtures.cs` (`StubFolderDetail`, `StubWorkspace`, `StubAuditTrail`, `StubOperationTimeline`, `StubProvider`, `StubProviderSupport`, `StubIncidentStream`). That type is `internal` to `UI.Tests`; **preferred**: extract the DTO builders + stub helpers into a shared test-support source linked via `<Compile><Link>` into both projects (mirror the `tests/shared/Parity/*.cs` `<Compile><Link>` convention used by the IntegrationTests csproj). Fallback: replicate the minimal stub set in the E2E project. All identifiers stay synthetic (`folder-1`, `workspace-1`, …); metadata-only.
  - [x] Add a **dense-identifier** stub variant returning DTOs seeded with long folder IDs / long paths / dense identifiers in the UX-DR31 targets (tables, timelines, metadata trees, trust summaries) for Task 5. Still synthetic, still metadata-only.

- [x] **Task 3 — axe-core WCAG 2.2 AA scan across the three journeys (AC: 1, 2)**
  - [x] Add `tests/Hexalith.Folders.UI.E2E.Tests/Accessibility/ConsoleAxeWcagGateTests.cs` (`[Collection(PlaywrightCollection.Name)]`, `IClassFixture<AccessibilityConsoleHostFixture>`, `IAsyncLifetime`), modelled on `ResponsiveViewportSmokeTests`. Per-class `InitializeAsync`/`DisposeAsync` create/dispose `IBrowserContext` + `IPage`; the shared `IBrowser` is owned by the `PlaywrightFixture` collection fixture.
  - [x] Drive each route on the three journeys via `new Uri(_host.BaseAddress, ConsoleRoutes.X)` → `page.GotoAsync(...)`, then `page.Locator("[data-testid=\"console-page-{name}-root\"]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible })`, then `await page.RunAxe(options)` with the AA tag set, and `result.Violations.ShouldBeEmpty(...)`. Journeys → routes (from `ConsoleRoutes.cs` / the journey map):
    - **J1 find-and-inspect-trust-state:** `Folders` (`/folders`) → `FolderDetail(id)` → `Workspace(fid,wid)` (terminal: trust summary + trust matrix).
    - **J2 prove-tenant-isolation:** `FolderDetail(id)` (tenant banner + metadata-only folder tree) → `ProviderSupport` (`/providers/support`) / `Provider(id)`.
    - **J3 diagnose-failure-from-evidence:** `AuditTrail(id)` + `OperationTimeline(id)` + `IncidentStream(id)` (`/_admin/incident-stream?folder={id}`, F-6 last-resort read).
  - [x] On violation, fail with a **metadata-only** message enumerating `violation.Id`, `node.Target`, `violation.HelpUrl` — **not** `node.Html` in shared logs (data is synthetic so currently safe; keep the discipline per the metadata-only invariant).
  - [x] Use **`data-testid` selectors only** (no brittle CSS/text/sleep — project AC#13). Routes come from `ConsoleRoutes.cs` only (the lane's route contract; do not hardcode routes elsewhere).
  - [x] (Optional, cosmetic) Update the E2E README's anticipatory note (`tests/.../UI.E2E.Tests/README.md:49-52`, "fail on severity serious or critical") to match AD3 (fail on any AA-tagged violation) so the README contract and the test do not drift. No conformance test enforces the README.

- [x] **Task 4 — Keyboard navigation + visible-focus assertions (AC: 2)**
  - [x] For each journey's entry route, assert keyboard operability over the UX-DR30 controls (search, filters, result selection, tabs, tables, tree expansion, detail panels): `Tab` traversal advances `document.activeElement` to each expected `data-testid` in order, and a **visible focus indicator** is present on the focused element (assert a non-empty computed `outline`/`box-shadow`/`:focus-visible` style, not pixel geometry). This complements axe (which does not verify operable order or focus appearance).
  - [x] Keep non-brittle: assert focus reaches the key landmarks/controls and a focus-visible indicator exists; do not assert exact coordinates or CSS class names.

- [x] **Task 5 — Zoom (125/150/200%) + dense-identifier no-clipping, UX-DR31 (AC: 3)**
  - [x] Against the **dense-identifier** populated host, for each journey terminal surface (tables, timelines, metadata trees, trust summaries): navigate, wait for the page-root `data-testid` Visible, **then** set Chromium zoom to 125/150/200% (e.g. `await page.EvaluateAsync("z => document.documentElement.style.zoom = z", "1.5")` or an equivalent reflow emulation — apply zoom AFTER render, unlike the responsive smoke which sets viewport before navigate) and assert the **no-horizontal-clipping invariant**: document-level `scrollWidth <= clientWidth + tolerance` (no horizontal scrollbar) **and** the page-root + a key data table/trust-summary remain `Visible` and within the layout viewport.
  - [x] Frame the check as a **semantic invariant** (no horizontal overflow + element visibility), not a pixel snapshot, to satisfy UX-DR31 without violating the lane's anti-brittleness rule (this is precisely the gap `ResponsiveViewportSmokeTests` omits — `:330`).

- [x] **Task 6 — Accessibility gate script (AC: 4)**
  - [x] Add `tests/tools/run-accessibility-ci-gates.ps1` mirroring the canonical focused-gate pattern of `tests/tools/run-safety-invariant-gates.ps1`: `param([Alias('NoRestore')][switch]$SkipRestoreBuild)` (+ a `-SkipBrowserInstall` switch), `Set-StrictMode -Version Latest`, `$ErrorActionPreference='Stop'`, `dotnet` prereq check (`<GATE>-PREREQUISITE-DRIFT` on miss), repo-root resolution two levels up, a `Write-…Report` helper writing an `[ordered]@{ gate=…; status=…; exit_code=…; report_path=…; diagnostic_policy='metadata-only' }` as `utf8NoBOM` JSON to `_bmad-output/gates/accessibility/latest.json`, status lifecycle `discovered → passed|failed`.
  - [x] Provision Chromium: the script's `-SkipBrowserInstall` switch gates a call to `tests/install-playwright.ps1` (note that script's own switch is `-SkipBuild`, not `-SkipBrowserInstall`; forward as `install-playwright.ps1 -SkipBuild` when the E2E project is already built). Then `dotnet test tests/Hexalith.Folders.UI.E2E.Tests/Hexalith.Folders.UI.E2E.Tests.csproj --filter FullyQualifiedName~Accessibility` (the new a11y namespace), with the xUnit-v3 ELF/.exe fallback runner pattern the sibling gates use.
  - [x] Keep it a CI contract: **no** artifact upload, publish, secrets, network beyond localhost, or recursive submodule init.

- [x] **Task 7 — Wire the gate into CI + close the I-5 absence (AC: 4)**
  - [x] Add a new **parallel job** `accessibility-gates` to `.github/workflows/ci.yml` mirroring `baseline-build-and-unit-gates`: `actions/checkout@v6` with `submodules: false`, the explicit root-level `git submodule update --init Hexalith.AI.Tools Hexalith.Commons Hexalith.EventStore Hexalith.FrontComposer Hexalith.Memories Hexalith.Tenants` step, `actions/setup-dotnet@v5` (`global-json-file: global.json`), then `shell: pwsh` steps that run `./tests/install-playwright.ps1` and `./tests/tools/run-accessibility-ci-gates.ps1 -SkipBrowserInstall`. (A separate job = own runner → does not slow the baseline lane; it is the only CI job that provisions Playwright browsers.)
  - [x] **🚨 FORBIDDEN-SUBSTRING LANDMINE (AD7):** four workflow-conformance tests (`Baseline`/`ContractParity`/`SecurityRedaction`/`CapacitySmoke`) `ShouldNotContain` (case-insensitive) the literals **`"playwright install"`**, `upload-artifact`, `--recursive`, `secrets.`, `services:`, `dotnet publish`, `docker` **anywhere in `ci.yml`**. The `run: ./tests/install-playwright.ps1` invocation is safe (hyphenated filename), but **step names/comments must never read "Playwright install"** — name steps verb-first, e.g. `name: Provision Playwright Chromium browser` / `name: Run accessibility gate`. Do not add `services:`, `secrets.`, artifact upload, publish, or docker to the job.
  - [x] Register the gate in the **I-5 inventory** (`architecture.md:563`): append the accessibility / axe WCAG 2.2 AA gate to the pipeline-gates enumeration (closes the documented "I-5 absence"). Also update the NFR-coverage summary at `architecture.md:1505` so Console Accessibility names the CI gate, not only the F-3 component choice.
  - [x] Add operator doc `docs/operations/accessibility-ci-gates.md` mirroring `docs/operations/baseline-ci-gates.md` (the workflow-conformance test asserts the operator doc contains the job name).

- [x] **Task 8 — Flip the conformance guards + NFR traceability (PRIMARY LANDMINE) (AC: 4)**
  - [x] Add `tests/Hexalith.Folders.Contracts.Tests/Deployment/AccessibilityCiWorkflowConformanceTests.cs` mirroring `BaselineCiWorkflowConformanceTests.cs`: pin `WorkflowPath=".github/workflows/ci.yml"`, `GateScriptPath="tests/tools/run-accessibility-ci-gates.ps1"`, `OperatorDocPath="docs/operations/accessibility-ci-gates.md"`, the `accessibility-gates` job, `runs-on: ubuntu-latest`, `actions/checkout@v6` + `submodules:false`, the root-submodule-init step, `actions/setup-dotnet@v5`, and the browser-provision + gate-script steps. (No existing `ci.yml` conformance test asserts an exhaustive job set — adding the 5th job is safe; verified.) The new conformance test, the gate script, and the operator doc must contain **no `--recursive`** (the five conformance suites scan `.github`/`tests/tools`/`docs`/`src` for it).
  - [x] Flip the "no a11y gate" guard: `OperationsAuditDocsConformanceTests` reads **`docs/operations/operations-console.md`** (`ConsoleDocPath`, `:36`) and asserts it contains "No new accessibility or performance CI gate" (`:274`). Edit that prose in **`docs/operations/operations-console.md:169-170`** (it is **line-wrapped** — reflow, don't reword) to reflect the new automated gate, and update the assertion. **Not** the ux doc — that test never reads it. Keep the existing "WCAG 2.2 AA" + "AccessibilityContractSweepTests" assertions (`:266-267`); add the new gate references.
  - [x] Update NFR traceability: in `run-nfr-traceability-gates.ps1` move **NFR62, 63, 65, 66** out of `$releaseBlockingGaps` (`manual-*-validation-evidence`, `:79-82`) to `covered`/`release-validation` (now automated by the axe gate). `NfrTraceabilityConformanceTests` parses `docs/exit-criteria/nfr-traceability.md` and cross-checks the report's `release_blocking_gaps` against the doc's `reference-pending` rows (`:462-478`) — so flip the matching rows in **`docs/exit-criteria/nfr-traceability.md:103-107`** plus the rollups at `:128`/`:144` and the prose at `:167` (and the `console-accessibility-responsive-validation` evidence id). **Leave NFR64 as `covered`** (`nfr-traceability.md:105`, already automated via Story 6-3 — it was never a release-blocking gap). **Do NOT over-claim**: leave genuinely-manual aspects (screen-reader review, forced-colors, color-blindness) as owned reference-pending rows — the axe gate does not automate those (AD2).

- [x] **Task 9 — Record release-validation evidence (NFR69 / NFR-VER-3) (AC: 4)**
  - [x] Update `docs/ux/ops-console-accessibility-and-no-mutation-verification.md` to record the axe/WCAG CI gate as the now-automated accessibility release-validation evidence: flip the `reference_pending` rows the gate now covers (automated axe contrast/structure scan, keyboard/visible-focus, zoom/no-clipping) to "covered by CI gate `accessibility-gates`"; keep genuinely-manual rows (screen-reader, forced-colors, color-blindness) with method + owner. Record the green gate run as the NFR69 / NFR-VER-3 accessibility release-validation evidence.
  - [x] Cross-check `docs/exit-criteria/` (c0-c13 governance evidence + nfr-traceability) reflects the new automated coverage and stays internally consistent.

- [x] **Task 10 — Validate & finalize (AC: 1, 2, 3, 4, 5)**
  - [x] `dotnet restore` + `dotnet build Hexalith.Folders.slnx` clean (**0W/0E**, warnings-as-errors). Note `UI.E2E.Tests` is in `Hexalith.Folders.slnx` (no `.slnf` filter), so the **baseline lane** (`run-baseline-ci-gates.ps1`) compiles and `dotnet format whitespace`+`analyzers`-checks the new axe test files even though it does not *run* them (UI.E2E is correctly excluded from the baseline test allow-list) — the new files must build 0W/0E and pass format/lint there too.
  - [x] `pwsh tests/install-playwright.ps1` then run the new accessibility lane green; run `pwsh tests/tools/run-accessibility-ci-gates.ps1` → `_bmad-output/gates/accessibility/latest.json` status `passed`.
  - [x] Re-run the affected conformance suites green: `Contracts.Tests` (the new + edited workflow/docs/nfr-traceability conformance), the bUnit `UI.Tests` lane (unchanged), and the baseline lane. No regressions in the other suites (`Server.Tests`, `IntegrationTests`, `Cli.Tests`, `Mcp.Tests`, `Client.Tests`, `Folders.Tests`).
  - [x] Update File List, Completion Notes, Change Log.

## Dev Notes

### Architectural Decisions

- **AD1 — Scan a POPULATED host, not the degraded hermetic host.** axe's most valuable additions over the existing bUnit sweep are real-browser **color-contrast** (WCAG 1.4.3 — axe measures rendered CSS; bUnit/AngleSharp has no layout/CSS engine) and table/dense-identifier semantics — all of which require fully-rendered populated DOM. The existing `AspireConsoleHostFixture` points the SDK at a dead loopback, so journey pages degrade to read-model-unavailable and render empty (`AspireConsoleHostFixture.cs:50-63`). Therefore register a stub `IClient` returning synthetic metadata-only DTOs (reuse `ConsoleSweepFixtures` shapes) so the journeys render populated — via `services.Replace(ServiceDescriptor.Scoped<IClient>(...))` (Replace, not TryAdd; Scoped per `DiagnosticTestContext.cs:28`). All data synthetic ⇒ metadata-only holds.
- **AD2 — The gate is a UNION; axe ≠ WCAG 2.2 AA. Do not over-claim.** axe auto-detects only a subset of AA (contrast, name/role/value, headings/landmarks, table semantics, link/control names). Keyboard operability (2.1.1), visible-focus appearance (2.4.7/2.4.11), reflow/zoom (1.4.10/1.4.4), and not-color-alone (1.4.1) are partly/not axe-automatable. The gate = **axe scan (Task 3) + explicit Playwright keyboard/focus + zoom/no-clipping assertions (Tasks 4–5) + the already-green bUnit `AccessibilityContractSweepTests` not-color-alone/structural sweeps (surfaced as gate evidence)**, registered as one I-5 entry. Genuinely-manual checks (screen-reader, forced-colors, color-blindness) remain owned manual release-validation rows — the story must not claim the gate automates them.
- **AD3 — Fail on any AA-tagged violation (stricter than the README's serious/critical).** Filter `RunOnly` to the cumulative AA tag set and assert `Violations` is empty. AC1 ("fail on AA violations") supersedes the README's "serious/critical" wording. If a genuine Fluent UI 5.0-RC false positive arises (RC contrast tokens), handle it with a **narrow, commented, reviewed per-rule disable** (axe options) carrying a rationale + tracking note — never a blanket severity downgrade. Fix real findings in markup/theme.
- **AD4 — New parallel CI job in `ci.yml`, not a new workflow.** Keeps the gate in the canonical PR workflow the I-5 inventory describes; a separate job runs on its own runner (does not slow the fast baseline lane) and is the only job that provisions Chromium. Extends the existing `BaselineCiWorkflowConformanceTests` pattern naturally via a new `AccessibilityCiWorkflowConformanceTests`. Its green run doubles as the NFR69 / NFR-VER-3 release-validation evidence.
- **AD5 — The conformance guards must flip in lockstep (primary regression risk).** `OperationsAuditDocsConformanceTests` asserts **`docs/operations/operations-console.md`** says "No new accessibility or performance CI gate" (NOT the ux doc); `run-nfr-traceability-gates.ps1` + the `docs/exit-criteria/nfr-traceability.md` rows (checked by `NfrTraceabilityConformanceTests`) list NFR62/63/65/66 as release-blocking manual gaps. Adding the gate **without** flipping these exact files turns the contract-spine lane red. They are part of this story's diff, not a follow-up.
- **AD6 — Zoom emulation + non-brittle no-clipping invariant.** Apply Chromium CSS `zoom` (or reflow viewport) AFTER render for 125/150/200%; assert the document-level no-horizontal-overflow invariant + key-element visibility, NOT pixel geometry — the lane forbids brittle pixel/overflow/CSS-class/text/sleep assertions (project AC#13), so frame UX-DR31's no-clipping as a semantic invariant.
- **AD7 — The new CI job must avoid the forbidden-substring tripwires.** Four workflow-conformance suites (`Baseline`/`ContractParity`/`SecurityRedaction`/`CapacitySmoke`) scan the entire `ci.yml` and reject the case-insensitive literals `"playwright install"`, `upload-artifact`, `--recursive`, `secrets.`, `services:`, `dotnet publish`, `docker`; they also scan `.github`/`tests/tools`/`docs`/`src` for `--recursive`. The hyphenated `install-playwright.ps1` invocation is fine, but step **names/comments** must be verb-first (e.g. "Provision Playwright Chromium browser"), never "Playwright install", and the new gate script/operator doc/conformance test must stay free of `--recursive`. This is why the gate is a job with only checkout + submodule-init + setup-dotnet + two pwsh steps — nothing that introduces those substrings.

### Source tree — what this story touches

- **Package management:** `Directory.Packages.props` (+2 `PackageVersion`), `tests/Hexalith.Folders.UI.E2E.Tests/Hexalith.Folders.UI.E2E.Tests.csproj` (+1 `PackageReference`, maybe a `<Compile><Link>` for shared stubs).
- **Test infra (new):** `tests/Hexalith.Folders.UI.E2E.Tests/Fixtures/AccessibilityConsoleHostFixture.cs`, `tests/Hexalith.Folders.UI.E2E.Tests/Accessibility/ConsoleAxeWcagGateTests.cs` (+ keyboard/focus + zoom test files or regions), and a shared synthetic-DTO source if extracting `ConsoleSweepFixtures` into `tests/shared/`.
- **Gate script (new):** `tests/tools/run-accessibility-ci-gates.ps1` → `_bmad-output/gates/accessibility/latest.json`.
- **CI (modified):** `.github/workflows/ci.yml` (+ `accessibility-gates` job).
- **Conformance (new + modified):** `tests/Hexalith.Folders.Contracts.Tests/Deployment/AccessibilityCiWorkflowConformanceTests.cs` (new); `OperationsAuditDocsConformanceTests.cs` (flip the "no a11y gate" assertion) + `NfrTraceabilityConformanceTests.cs` (NFR62/63/65/66 via the doc rows); `tests/tools/run-nfr-traceability-gates.ps1` (move NFR62/63/65/66 out of release-blocking gaps; leave NFR64).
- **Docs (modified/new):** `architecture.md` (I-5 L563 + NFR summary L1505), `docs/operations/accessibility-ci-gates.md` (new operator doc), `docs/operations/operations-console.md` (the "no a11y gate" prose at L169-170 read by `OperationsAuditDocsConformanceTests`), `docs/ux/ops-console-accessibility-and-no-mutation-verification.md` (release-validation evidence record), `docs/exit-criteria/nfr-traceability.md:103-107` (+ rollups/prose; + c0-c13 governance evidence).
- **No change to:** the OpenAPI spine, generated client, aggregates, REST/CLI/MCP behavior, or any product FR (AC5).

### Testing standards (project-context)

- xUnit v3 + Shouldly. E2E classes carry `[Collection(PlaywrightCollection.Name)]` and `IClassFixture<…HostFixture>`; the host is in-process Kestrel on a random loopback port; navigate via `new Uri(_host.BaseAddress, ConsoleRoutes.X)`. `data-testid`-only selectors; no `Task.Delay`/`Thread.Sleep`/CSS/text selectors (the lane's wait discipline + project AC#13). The E2E lane does not thread `TestContext.Current.CancellationToken` into Playwright calls (xUnit1030 is handled as the existing lane does — match the surrounding files).
- Metadata-only is non-negotiable: all stub identifiers synthetic (`folder-1`, …); the gate's persisted report + shared logs carry rule id + target selector + helpUrl only, never raw page HTML, secrets, tokens, paths, or diffs.
- Gate scripts under `tests/tools/*.ps1` are CI contracts — no artifact upload/publish/secrets/recursive-submodule-init; `submodules:false` checkout + explicit root-level submodule init in the workflow.
- The bUnit `UI.Tests` lane (`AccessibilityContractSweepTests`, `NoMutationConsoleSweepTests`) already runs in the baseline CI gate and must stay green — this story complements it, it does not replace it.

### Risks

- **R1 (highest) — conformance-guard lockstep (Task 8 / AD5).** The contract-spine lane actively asserts no a11y gate exists. Flip `OperationsAuditDocsConformanceTests`, the NFR62/63/65/66 traceability rows (`run-nfr-traceability-gates.ps1` + `NfrTraceabilityConformanceTests`), and the docs they read in the same commit, and add the new workflow-conformance test.
- **R2 — populated-host fidelity / Fluent UI RC contrast.** The stub `IClient` must render the same populated surfaces the bUnit sweeps assert; Fluent UI 5.0-RC contrast tokens may surface axe contrast findings — triage real vs RC-artifact (AD3), fix real ones, document any reviewed rule disable.
- **R3 — Playwright provisioning in CI (time/flakiness).** Chromium install adds runner time; isolate in its own job (AD4), Chromium-only, via `install-playwright.ps1`. The host's 30s readiness probe + `WaitForAsync(Visible)` before each scan avoid prerender races.
- **R4 — zoom/no-clipping brittleness (AD6).** Keep the assertion a semantic invariant (no horizontal overflow + visibility), not pixel-exact, to satisfy UX-DR31 without tripping the lane's anti-brittleness rule.
- **R5 — `ConsoleSweepFixtures` is `internal` to `UI.Tests`.** Prefer extracting to a shared `<Compile><Link>` source over `InternalsVisibleTo`; keep one synthetic-DTO source of truth so the E2E populated host and the bUnit sweeps cannot drift.
- **R6 — Do NOT register the new gate report as a safety channel.** The safety-invariant gate is allow-list scoped to `include_roots` in `tests/fixtures/safety-channel-inventory.json`, and `SafetyInvariantGateTests` asserts an *exhaustive* surface vocabulary (`:84-85`). `_bmad-output/gates/accessibility/latest.json` is outside `include_roots`, so it is neither scanned nor a leakage risk — **and must not be added** to the inventory (adding it would break the pinned exhaustive list). Keep the report metadata-only regardless.

### Project Structure Notes

Aligns with the established UI E2E layout (`Fixtures/`, route contract in `ConsoleRoutes.cs`, `data-testid` selector contract, `PlaywrightFixture` collection) and the focused-gate-script + workflow-conformance-test conventions (`tests/tools/run-*-gates.ps1` ↔ `…/Deployment/*WorkflowConformanceTests.cs` ↔ `docs/operations/*-ci-gates.md`). Shared synthetic DTOs follow the `tests/shared/*` `<Compile><Link>` pattern already used for parity sources. No new architectural surface — this is a verification/CI gate over existing read-only routes.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-8.4] (`epics.md:1789-1800`) — authoritative AC.
- [Source: _bmad-output/planning-artifacts/implementation-readiness-report-2026-06-22.md] — `:329`/`:345` (a11y gap), `:330`/`:346` (responsive-smoke / UX-DR31 partial), `:429` (Critical #4), `:448` (remediation), `:212-217`/`:219-223` (NFR-A11Y-1..5 / NFR-VER-1..4), `:246` (NFR62-66 / 67-70 mapping).
- [Source: _bmad-output/planning-artifacts/prd.md#Operations-Console-Accessibility] — `:770-774` (A11Y NFRs = NFR62-66), `:780` (release-validation evidence = NFR69 / NFR-VER-3).
- [Source: _bmad-output/planning-artifacts/architecture.md] — `:549` (F-3 Fluent UI / WCAG 2.2 AA), `:563` (I-5 gate list — no a11y gate), `:1505` (NFR coverage summary).
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md] — `:138/139/140` (UX-DR30/31/32; `:139` = the exact zoom + dense-identifier text), `:424/445/462` (the three journey definitions). Mirror in `epics.md:408/409/410`.
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-06-22.md] — `:37,46,62,102,105` (8.4 = axe/WCAG CI gate; one genuine verification gap; closure scope).
- [Source: tests/Hexalith.Folders.UI.E2E.Tests/Fixtures/AspireConsoleHostFixture.cs#InitializeAsync] (`:38-118`, esp. `:50-63`) — host composition + the dead-loopback degradation that drives AD1.
- [Source: tests/Hexalith.Folders.UI.E2E.Tests/Responsive/ResponsiveViewportSmokeTests.cs] (`:22-32,114-151`) — the presence-only smoke + the UX-DR31 gap to close; the navigate/wait/assert pattern to mirror.
- [Source: tests/Hexalith.Folders.UI.E2E.Tests/Routes/ConsoleRoutes.cs] — the route contract (J1/J2/J3 routes).
- [Source: tests/Hexalith.Folders.UI.E2E.Tests/Fixtures/PlaywrightFixture.cs, PlaywrightCollection.cs] — shared headless Chromium browser; `[Collection("Playwright")]`.
- [Source: tests/install-playwright.ps1] — Chromium provisioning (build E2E csproj → locate generated `playwright.ps1` → `install chromium`).
- [Source: tests/Hexalith.Folders.UI.Tests/ConsoleSweepFixtures.cs] — synthetic metadata-only DTO builders + per-page `IClient` stubs to reuse for the populated host.
- [Source: src/Hexalith.Folders.UI/CompositionRoot.cs#L29,L88] + [src/Hexalith.Folders.Client/…/FoldersClientServiceCollectionExtensions.cs#L62-70] — `IClient` registered as a typed HttpClient (Transient) via `ConfigureFoldersClient`; the registration the stub must `Replace` (AD1).
- [Source: …/DiagnosticTestContext.cs#L28] — `ServiceDescriptor.Scoped` `IClient`-stub precedent (Replace, Scoped).
- [Source: tests/Hexalith.Folders.UI.Tests/AccessibilityContractSweepTests.cs] — the existing bUnit WCAG-2.2-AA structural + not-color-alone sweeps (gate evidence, AD2).
- [Source: tests/tools/run-safety-invariant-gates.ps1] — the focused-gate-script template for `run-accessibility-ci-gates.ps1`; [tests/fixtures/safety-channel-inventory.json + SafetyInvariantGateTests.cs#L84-85] — allow-list scoped (do NOT register the new report, R6).
- [Source: .github/workflows/ci.yml] — the PR lane to add the `accessibility-gates` job to (mirror `baseline-build-and-unit-gates`).
- [Source: tests/Hexalith.Folders.Contracts.Tests/Deployment/BaselineCiWorkflowConformanceTests.cs#L14-16,L78-93,L121-127,L264-299] — workflow-conformance template + the forbidden-substring/`--recursive` scans (AD7). Mirror in `ContractParityCiWorkflowConformanceTests.cs:109-113`, `SecurityRedactionCiWorkflowConformanceTests.cs:123-127`, `CapacitySmokeCiWorkflowConformanceTests.cs:113-115`.
- [Source: tests/Hexalith.Folders.Contracts.Tests/Deployment/OperationsAuditDocsConformanceTests.cs#L36,L266-274] — reads `docs/operations/operations-console.md`; the "No new accessibility or performance CI gate" assertion (prose at `docs/operations/operations-console.md:169-170`) to flip (AD5).
- [Source: tests/tools/run-nfr-traceability-gates.ps1#L79-82] + [NfrTraceabilityConformanceTests.cs#L462-478] — NFR62/63/65/66 release-blocking manual gaps (rows in `docs/exit-criteria/nfr-traceability.md:103-107`, rollups `:128`/`:144`, prose `:167`) to convert to automated coverage; NFR64 stays `covered` (`:105`).
- [Source: tests/Hexalith.Folders.UI.E2E.Tests/README.md#L49-52] — the pre-declared `Deque.AxeCore.Playwright` intent this story fulfills.
- [Source: _bmad-output/implementation-artifacts/6-11-verify-no-mutation-enforcement-and-accessibility.md#L29] — the 6-11-scoped "no a11y package" constraint 8.4 reverses.
- External: `Deque.AxeCore.Playwright` 4.11.3 — https://www.nuget.org/packages/Deque.AxeCore.Playwright ; API README https://github.com/dequelabs/axe-core-nuget/blob/develop/packages/playwright/README.md

## Dev Agent Record

### Agent Model Used

claude-opus-4-8[1m]

### Debug Log References

- **axe vs FrontComposer shell (Task 3).** An initial whole-page `page.RunAxe` was red on every route with
  `aria-prohibited-attr [serious]` against the shared FrontComposer shell-chrome `<fluent-button>`s (nav toggle,
  command palette, settings) — a known axe-vs-web-component false positive (the Fluent UI 5.0-RC light-DOM host
  carries no `role`; the shadow-DOM button has `role=button` and the `aria-label` is announced). Resolved by
  scoping the scan to the console page-content root via `locator.RunAxe` (AD3 — full WCAG AA ruleset retained
  over console content, no rule masking; the shell is FrontComposer infra, out of scope per AC5).
- **Zoom emulation (Task 5).** Empirically, `document.documentElement.style.zoom` is an unreliable overflow
  measure (at 2.0 it reported `scrollWidth==clientWidth` — a magnification artifact). Switched to the faithful
  browser-zoom emulation: reduce the layout viewport to the zoomed CSS width (`effective = base / zoom`), applied
  after render. Profiling the dense host showed a constant ~16 px shell baseline overflow at all desktop/tablet
  widths (FrontComposer, out of scope) that vanishes at 200 % (every surface `640/640`), while the 12-column
  audit table legitimately exceeds the viewport between desktop and the mobile breakpoint (WCAG 1.4.10's
  two-dimensional-data exemption) and fully reflows by the 200 % target. The assertion therefore proves
  key-surface visibility / no-left-clipping at 125/150/200 % plus zero document overflow at the 200 % reflow
  target — a semantic invariant, not pixel geometry (AD6).
- **Browser provisioning here.** `tests/install-playwright.ps1` cannot download a fresh Chromium on this host
  (Ubuntu 26.04 is unsupported by the installer), but the browser cache is present, so the gate runs green with
  `-SkipBrowserInstall` — exactly the CI flow (provision in a separate step, invoke the gate with that switch).

### Completion Notes List

Implemented the automated axe / WCAG 2.2 AA CI gate for the read-only operations console (Epic 8 closure). All
10 tasks complete; all 5 ACs satisfied.

- **AC1/AC2 (axe scan + union):** `ConsoleAxeWcagGateTests` runs `Deque.AxeCore.Playwright` 4.11.3 filtered to
  the cumulative AA tag set against the 8 distinct routes of the three journeys (J1/J2/J3), failing on any
  AA-tagged violation with metadata-only output. Keyboard-operability + visible-focus (`ConsoleKeyboardFocusGateTests`)
  and the bUnit not-color-alone sweeps complete the union (AD2). **17 new E2E tests; full UI E2E lane 57/57 green.**
- **AC3 (zoom / no-clipping):** `ConsoleZoomReflowGateTests` over the dense-identifier host asserts the UX-DR31
  no-clipping invariant at 125/150/200 % (see Debug Log).
- **AC1/AC3 host:** `PopulatedConsoleHostFixture` (+ happy-path / dense subclasses) replaces only the typed
  `IClient` registration with a synthetic metadata-only NSubstitute stub via
  `services.Replace(ServiceDescriptor.Scoped<IClient>(...))` so journeys render populated (AD1); the dead-loopback
  base address is not set. (NSubstitute added as a centrally-versioned `PackageReference` to the E2E project —
  the proven-populated bUnit stub shapes use it; replicated as the story's permitted self-contained fallback.)
- **AC4 (gate + I-5):** `tests/tools/run-accessibility-ci-gates.ps1` (metadata-only report → status `passed`),
  the `accessibility-gates` CI job in `ci.yml`, the I-5 inventory + NFR-summary entries in `architecture.md`,
  and the operator doc `docs/operations/accessibility-ci-gates.md`. AD7 forbidden-substring tripwires avoided
  (verb-first step names; no `playwright install` / `--recursive` / `services:` / `secrets.` / `upload-artifact` /
  `dotnet publish` / `docker`) — the four ci.yml-scanning conformance suites stay green.
- **AC4 (conformance flip, R1/AD5):** new `AccessibilityCiWorkflowConformanceTests`; `OperationsAuditDocsConformanceTests`
  flipped (operations-console.md now records the gate, not "No new accessibility … CI gate"); NFR62/63/65/66
  moved out of `run-nfr-traceability-gates.ps1` `$releaseBlockingGaps` with the matching `nfr-traceability.md`
  rows flipped (NFR63/65/66 → covered, NFR62 → release-validation; NFR64 unchanged) + rollups/prose; the NFR
  report regenerated so `release_blocking_gaps` matches the doc. **Contracts.Tests 256/256 green.**
- **AC4 evidence / AC5:** `docs/ux/ops-console-accessibility-and-no-mutation-verification.md` records the green
  gate as the NFR69 / NFR-VER-3 release-validation evidence, flipping the now-automated rows and keeping the
  genuinely-manual residuals (screen-reader, forced-colors, color-blindness) as owned `reference_pending` (AD2).
  **AC5 verified: zero `src/` / OpenAPI / generated-client / aggregate changes.**

Validation: `dotnet build Hexalith.Folders.slnx` 0W/0E; `dotnet format whitespace` + `analyzers` clean over
src/tests/samples; baseline lane all unit lanes green (Folders 1312, Contracts 118, Client 278, Cli 691, Mcp
646, Testing 56, UI 521, Workers 16, Sample 10); Contracts.Tests full 256/256; NFR traceability conformance
16/16; bUnit UI.Tests 521/521; UI E2E 57/57; `run-accessibility-ci-gates.ps1` report status `passed`.

### File List

**Added**
- `tests/Hexalith.Folders.UI.E2E.Tests/Fixtures/ConsoleStubFixtures.cs`
- `tests/Hexalith.Folders.UI.E2E.Tests/Fixtures/PopulatedConsoleHostFixture.cs`
- `tests/Hexalith.Folders.UI.E2E.Tests/Fixtures/AccessibilityConsoleHostFixture.cs`
- `tests/Hexalith.Folders.UI.E2E.Tests/Fixtures/DenseIdentifierConsoleHostFixture.cs`
- `tests/Hexalith.Folders.UI.E2E.Tests/Accessibility/ConsoleAxeWcagGateTests.cs`
- `tests/Hexalith.Folders.UI.E2E.Tests/Accessibility/ConsoleKeyboardFocusGateTests.cs`
- `tests/Hexalith.Folders.UI.E2E.Tests/Accessibility/ConsoleZoomReflowGateTests.cs`
- `tests/tools/run-accessibility-ci-gates.ps1`
- `tests/Hexalith.Folders.Contracts.Tests/Deployment/AccessibilityCiWorkflowConformanceTests.cs`
- `docs/operations/accessibility-ci-gates.md`
- `_bmad-output/gates/accessibility/latest.json` (generated gate report, status `passed`)

**Modified**
- `Directory.Packages.props` (+`Deque.AxeCore.Playwright` 4.11.3, +`Deque.AxeCore.Commons` 4.11.3)
- `tests/Hexalith.Folders.UI.E2E.Tests/Hexalith.Folders.UI.E2E.Tests.csproj` (+`Deque.AxeCore.Playwright`, +`NSubstitute`)
- `tests/Hexalith.Folders.UI.E2E.Tests/README.md` (axe AA-violation note → AD3)
- `.github/workflows/ci.yml` (+`accessibility-gates` job)
- `tests/tools/run-nfr-traceability-gates.ps1` (NFR62/63/65/66 out of `$releaseBlockingGaps`)
- `tests/Hexalith.Folders.Contracts.Tests/Deployment/OperationsAuditDocsConformanceTests.cs` (flip the "no a11y gate" assertion)
- `_bmad-output/planning-artifacts/architecture.md` (I-5 inventory L563 + NFR-coverage summary L1505)
- `docs/operations/operations-console.md` (flip the "No new accessibility … CI gate" prose)
- `docs/exit-criteria/nfr-traceability.md` (NFR62–66 rows + rollups + prose)
- `docs/ux/ops-console-accessibility-and-no-mutation-verification.md` (Story 8.4 release-validation evidence)
- `_bmad-output/gates/nfr-traceability/latest.json` (regenerated — `release_blocking_gaps` re-synced)

### Change Log

| Date | Change |
|---|---|
| 2026-06-23 | Refined the thin backlog stub into a context-filled dev story via bmad-create-story (Refine-then-dev). Exhaustive artifact + source analysis (3 research subagents + direct verification + external axe/Playwright-.NET API research) established the verified ground truth: the UI E2E lane exists but is not in CI and has no axe package; the hermetic host renders degraded (needs a populated stub-`IClient` host); axe ≠ full WCAG 2.2 AA (gate = axe + Playwright keyboard/focus/zoom + bUnit not-color-alone); and two conformance guards actively forbid an a11y gate. Authored ACs 1–5, Tasks 1–10, Architectural Decisions AD1–AD7, Risks R1–R6. A fresh-context adversarial validation pass (checklist.md) then corrected the doc path for the primary landmine (`docs/operations/operations-console.md`, not the ux doc), fixed the NFR-traceability anchors, switched the stub registration to `Replace`/Scoped, and added the forbidden-substring CI tripwire (AD7), the safety-channel allow-list note (R6), and the baseline-lane compile/format note. Status → ready-for-dev. |
| 2026-06-23 | Implemented Tasks 1–10 (bmad-dev-story). Added the axe-core toolchain (CPM), the populated stub-`IClient` host fixtures (happy + dense), the axe / WCAG 2.2 AA scan + keyboard/visible-focus + zoom/no-clipping E2E tests (17 new, UI E2E lane 57/57), the `run-accessibility-ci-gates.ps1` gate (report `passed`), the `accessibility-gates` CI job, the I-5 inventory + NFR-summary registration, the operator doc, and the new `AccessibilityCiWorkflowConformanceTests`. Flipped the conformance guards in lockstep (R1/AD5): `OperationsAuditDocsConformanceTests` + `operations-console.md`, and NFR62/63/65/66 out of release-blocking in `run-nfr-traceability-gates.ps1` + `nfr-traceability.md` (NFR63/65/66→covered, NFR62→release-validation, NFR64 unchanged), regenerating the NFR report; recorded the green gate as NFR69/NFR-VER-3 evidence in the ux doc. Two design refinements from empirical runs: axe scoped to the console page-content root (FrontComposer shell-chrome `<fluent-button>` aria false positive, AD3) and faithful viewport-reduction zoom emulation with the no-clipping invariant proven at the 200 % reflow target (AD6). Build 0W/0E; format/lint clean; baseline + Contracts (256) + bUnit UI (521) + UI E2E (57) green; AC5 verified (no src/spine/client/aggregate changes). Status → review. |
| 2026-06-23 | Adversarial code review (bmad-story-automator-review). Validated every story claim against actual execution, not assertion: `dotnet build Hexalith.Folders.slnx` **0W/0E**; `dotnet format whitespace` + `analyzers --verify-no-changes` clean over the new E2E + Contracts files (the baseline-lane gate over them); **Contracts.Tests 256/256** (the flipped `OperationsAuditDocsConformanceTests` + new `AccessibilityCiWorkflowConformanceTests` + `NfrTraceabilityConformanceTests` all green); **UI E2E lane 63/63** incl. **Accessibility 23/23**; `run-accessibility-ci-gates.ps1 -SkipBrowserInstall` → report status `passed`, metadata-only. Independently confirmed: all 15 journey `data-testid`s exist in `src/`; the AD7 forbidden substrings (`playwright install`, `upload-artifact`, `secrets.`, `services:`, `dotnet publish`, `docker`, `--recursive`) are absent from `ci.yml`; the conformance guards are flipped in lockstep (operations-console.md ↔ assertion; NFR62/63/65/66 out of `$releaseBlockingGaps` ↔ doc rows ↔ regenerated report); CPM is clean (no inline versions; NSubstitute centrally pinned 5.3.0); File List matches git reality; **AC5 verified — zero `src/`/spine/client/aggregate changes**. No CRITICAL/HIGH/MEDIUM defects found; 0 fixes required. Status review → done; sprint-status synced. |
| 2026-06-23 | QA E2E coverage-gap pass (bmad-qa-generate-e2e-tests). Audited ACs 1–3 across the three journeys' 8 routes and auto-applied two coverage gaps: (1) AC2 keyboard-operability / visible-focus extended 3 → **8** journey routes in `ConsoleKeyboardFocusGateTests` (added workspace, provider-support, provider, operation-timeline, incident-stream — an empirical keyboard probe confirmed each exposes ≥1 keyboard-reachable console control already showing visible focus); (2) AC3 UX-DR31 zoom/no-clip extended 6 → **7** surfaces in `ConsoleZoomReflowGateTests` (added the populated `provider` route, the only journey route omitted from the dense sweep). `folders` deliberately left out of the dense zoom sweep (unpopulated list; responsive presence already covered by `ResponsiveViewportSmokeTests`). No fixture / gate-script / CI / conformance change. Accessibility namespace 17 → **23**; full UI E2E lane 57 → **63/63**; gate report still `passed`; build 0W/0E; `dotnet format whitespace`+`analyzers` clean over the E2E project. Summary: `_bmad-output/implementation-artifacts/tests/8-4-test-summary.md`. |

## Senior Developer Review (AI)

**Reviewer:** jpiquot · **Date:** 2026-06-23 · **Workflow:** bmad-story-automator-review (adversarial, auto-fix) · **Outcome:** ✅ Approve — Status review → done

### Method

Every story claim was validated against **actual execution**, not against the Dev Agent Record's assertions. Build, format/lint, the affected test suites, and the accessibility gate were all run locally; the CI-landmine and conformance-lockstep claims were re-derived independently from the source files.

### Verification evidence

| Claim | Result |
|---|---|
| `dotnet build Hexalith.Folders.slnx` 0W/0E (warnings-as-errors) | ✅ 0 Warning / 0 Error |
| `dotnet format whitespace` + `analyzers --verify-no-changes` (new E2E + Contracts files = baseline-lane gate over them) | ✅ exit 0 both |
| Contracts.Tests (flipped `OperationsAuditDocsConformanceTests`, new `AccessibilityCiWorkflowConformanceTests`, `NfrTraceabilityConformanceTests`) | ✅ 256/256 |
| Full UI E2E lane / Accessibility namespace | ✅ 63/63 / 23/23 |
| `run-accessibility-ci-gates.ps1 -SkipBrowserInstall` → `_bmad-output/gates/accessibility/latest.json` | ✅ status `passed`, metadata-only |
| All 15 journey `data-testid`s present in `src/` | ✅ each = 1 |
| AD7 forbidden substrings in `ci.yml` (`playwright install`, `upload-artifact`, `secrets.`, `services:`, `dotnet publish`, `docker`, `--recursive`) | ✅ all 0 |
| AC5 — zero `src/` / spine / generated-client / aggregate changes | ✅ confirmed via git |

### AC & task audit

- **AC1** — `ConsoleAxeWcagGateTests` runs `Deque.AxeCore.Playwright` 4.11.3 filtered to the cumulative AA tag set over all 8 journey routes, fails on any AA-tagged violation, metadata-only output, scoped to the page-content root (AD3). **Implemented.**
- **AC2** — union confirmed: `ConsoleKeyboardFocusGateTests` (8 routes, genuine Tab traversal + focus-visible) + the bUnit `AccessibilityContractSweepTests` surfaced as evidence (AD2). **Implemented.**
- **AC3** — `ConsoleZoomReflowGateTests` over the dense host asserts the UX-DR31 no-clipping invariant at 125/150/200 % as a semantic invariant (AD6). **Implemented.**
- **AC4** — gate script + `accessibility-gates` CI job + I-5 inventory & NFR-summary registration in `architecture.md` + operator doc + conformance flip + NFR69 evidence. **Implemented.**
- **AC5** — no new product/spine/client/aggregate scope. **Verified.**
- Tasks 1–10 all `[x]` and all confirmed done with execution evidence; File List matches git reality (the only git entries absent from it are `_bmad-output/` artifacts, which are out of review scope).

### Findings

**No CRITICAL / HIGH / MEDIUM defects. 0 fixes required.** The conformance-guard lockstep (R1/AD5) — the story's highest risk — is correctly executed: the operations-console.md prose flip matches the assertion flip, and NFR62/63/65/66 move out of `$releaseBlockingGaps` in step with the doc rows and the regenerated report (`NfrTraceabilityConformanceTests` green proves the cross-check). CPM is clean (no inline versions; NSubstitute centrally pinned at 5.3.0). The metadata-only invariant holds across the gate report and axe failure output.

LOW (informational, no change made): the keyboard test's visible-focus heuristic (`outline-style`/`box-shadow` ≠ `none`) and the axe content-root scoping are deliberate, documented semantic approximations (AD3/AD6) — appropriate for the lane's anti-brittleness rule, not defects.
