---
baseline_commit: 6dcfc2d
---

# Story 6.10: Enforce console performance and perceived-wait UX

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an operator,
I want diagnostic pages to meet console performance budgets and show clear loading states,
so that the console remains useful during incidents.

## Acceptance Criteria

> **Epic 6.10 BDD (verbatim, epics.md L1444–1447):**
> **Given** console pages call projection endpoints
> **When** pages load
> **Then** primary diagnostic flows meet p95 and p99 budgets or produce measured release evidence
> **And** skeleton state appears at 400 ms and a cancel affordance appears at 2 seconds for in-flight requests.

> **CRITICAL SCOPING FACT (read before coding):** This is the **F-7 perceived-wait slice** that every prior Epic-6 story deliberately deferred (6.6 AC, 6.8 AC, 6.9 AC #14, 6.2): the seven data pages each ship only a simple `data-testid="console-page-{name}-loading" aria-busy="true"` paragraph today, and this story replaces those with timed **`SkeletonState`** (400 ms) + **`StillLoadingCancel`** (2 s) affordances. The performance-**budget** half ("meet p95/p99 **or** produce measured release evidence") is, per the repo, a **release-validation** concern, **not** a CI gate and **not** an NBomber console harness — F-7 is release-verified (prd.md L778–780; architecture.md L1497, L1556), the NBomber lane is scoped to C1 capacity only (architecture.md L202, L1298), and latency is explicitly **not** a parity dimension (architecture.md L107). So the budget deliverable here is **documented release evidence** recording the F-7 targets + measurement method (numbers `reference_pending`, consistent with `docs/exit-criteria/c0-c13-governance-evidence.yaml`), with the **perceived-wait UX as the shipped, testable mitigation**. **Do not** build a console perf harness or a CI perf gate (that is Workstream 7 / Story 7.10). See Dev Notes → "Scope & the performance-budget variance".

> **SECOND CRITICAL FACT (testing the timers):** The 400 ms / 2 s thresholds must be driven by the **BCL `System.TimeProvider`** (`net10.0`, **no package**) registered as `services.AddSingleton(TimeProvider.System)`. The `Microsoft.Extensions.Time.Testing.FakeTimeProvider` package **cannot** be added because `Directory.Packages.props` is a forbidden touch — instead **hand-roll a controllable `TimeProvider` subclass in the UI test project** (the repo already hand-rolls `FixedTimeProvider : TimeProvider`; see References). See Dev Notes → "Testing requirements".

Decomposed, testable acceptance criteria:

1. **Two reusable perceived-wait components exist** under `src/Hexalith.Folders.UI/Components/` (the architecture-fixed locations, architecture.md L1198–1199, L1373): a **new `SkeletonState`** component (`.razor` + `.razor.cs`) and a **new `StillLoadingCancel`** component. Both are **dumb/presentational, fed by the page** (no `IClient` injection), modelled on the sibling `DegradedModeBanner`/`ConsoleErrorPanel` shape (`@namespace Hexalith.Folders.UI.Components`, doc-comment citing story/AC/F-#/UX-DR, stable `data-testid`, shared `fc-*` CSS classes, **no `.razor.css`**).

2. **Three timing bands, driven by `TimeProvider` (UX-DR25 / §3.7 / F-7):** while a request is in flight, `SkeletonState` renders **(a) ≤ 400 ms: no skeleton and no spinner** (only a minimal labelled `aria-busy="true"` region so assistive tech announces loading and tests keep a stable testid); **(b) 400 ms – 2 s: the layout-stable skeleton**; **(c) ≥ 2 s: skeleton + `StillLoadingCancel`** ("still loading… [Cancel]"). The 400 ms and 2 s transitions are scheduled via the **injected `TimeProvider`** (`CreateTimer`), each firing an `InvokeAsync(StateHasChanged)`; `SkeletonState` implements `IDisposable`/`IAsyncDisposable` and **disposes its timers** on teardown (no leaked circuit timers).

3. **Skeleton is layout-stable and labels what is loading (UX-DR25):** the skeleton preserves the eventual layout (no content jump / no focus loss) and shows a **loading label naming the data** (one of: search results, workspace summary, folder metadata, provider readiness, audit timeline, access evidence, operation timeline, incident stream) supplied by the page via a `Label` parameter. Build the skeleton bars from Fluent UI primitives already on the graph (`FluentSkeleton`/`FluentProgressRing` — `Microsoft.FluentUI.AspNetCore.Components`, already registered) or shared `fc-*` placeholder markup; **do not** add the Fluent UI Icons package (it is deliberately off the graph — reuse `Components/Icons/FoldersConsoleIcons.cs`).

4. **`StillLoadingCancel` affordance at 2 s (F-7 / §3.7):** renders the text **"still loading…"** plus a **Cancel control** that is a **read-only** `<button type="button">`/`<FluentButton>` (modelled on `SafeCopyId`/`CorrelationCopyButton`), **keyboard reachable** with an accessible name (UX-DR30), conveyed by **text + control, never colour alone** (UX-DR14). It exposes `[Parameter] public EventCallback OnCancel { get; set; }` and invokes it on click/Enter/Space. `data-testid="console-still-loading-cancel"`; it must **not** trip the mutation guard (no `form`/`fluentinputform`/`fluentdialog`/`[data-fc-command]`/`[data-fc-mutation]`).

5. **Cancel actually cancels the in-flight request and returns to a stable view, NOT an error (§3.7):** each page holds a `CancellationTokenSource` per load and threads its `Token` into the **`CancellationToken` overload** of the primary `IClient` read (the overloads exist — e.g. `GetWorkspaceStatusAsync(..., CancellationToken)`, `ListOperationTimelineAsync(..., CancellationToken)`; generated client g.cs L76/L859/L1105 etc.) and into the supplementary `TryReadAsync` reads. `OnCancel` → `_cts.Cancel()`. The resulting `OperationCanceledException`/`TaskCanceledException` from the cancelled read is caught and resolved to a **neutral cancelled state** — a stable, non-error idle view (page root + `<h1>` + banners still render) offering a **read-only reload affordance** (an `<a href>` back to the same route). The cancelled state is **distinct from** `_error` (safe-denial / `ConsoleErrorPanel`) and **distinct from** `_unavailable` (transport failure / `ConsoleEmptyState Reason="ReadModelUnavailable"`); it is never rendered as a defect.

6. **Applied across all seven in-flight-read pages, preserving each page's contract:** `Workspace`, `FolderDetail`, `AuditTrail`, `OperationTimeline`, `Provider`, `ProviderSupport`, and `IncidentStream` each render `<SkeletonState>` **in their existing loading branch**, preserving (a) the page's existing branch **order** (some render `_error` before `_loading`, others `_loading` first — do not reorder), and (b) the existing `data-testid="console-page-{name}-loading"` token (move it onto the `SkeletonState` root via a `TestId`/`LoadingTestId` parameter so existing tests and selectors still resolve). **Static nav pages that do no in-flight projection read — `Home`, `Tenants`, `Folders` — are out of scope** (no skeleton).

7. **Incident / degraded flows honour the 5 s p95 band, with no relaxed safety (F-7 / F-6):** the `IncidentStream` page (the F-6 last-resort path) gets the same perceived-wait UX; per F-7 its budget band is the **degraded ≤ 5 s p95** branch (vs 1.5 s p95 primary). The `DegradedModeBanner` (shipped 6.9) still renders **unconditionally** in the loading/skeleton/cancel/cancelled branches, and redaction (`RedactedField`) is **not** relaxed during loading. No raw event content, secrets, paths, or provider payloads appear in any loading/skeleton/cancel state.

8. **Console performance budget recorded as measured release evidence (F-7):** add a concise evidence doc `docs/ux/ops-console-performance-budget.md` that records the F-7 budget contract verbatim — **p95 page-load < 1.5 s for primary diagnostic flows; p99 < 3 s; degraded-mode (incident) flows ≤ 5 s p95** (separate from PRD end-user budgets), and the supporting **status/audit summary 500 ms p95** (NFR Performance) — names the **measurement method** as **release validation** (per prd.md L778–780; numbers `reference_pending`, not pinned in this story), names the **perceived-wait UX (this story) as the shipped mitigation**, and explicitly states there is **no CI perf gate and no NBomber console harness** here (deferred to Workstream 7 / Story 7.10). No actual p95/p99 numbers are fabricated.

9. **`TimeProvider` registered without adding any package:** `CompositionRoot.cs` registers `services.AddSingleton(TimeProvider.System)` (BCL `System.TimeProvider`, `net10.0` — **no `PackageReference`, `Directory.Packages.props` untouched**). `SkeletonState` consumes it via `[Inject] private TimeProvider Clock`.

10. **Read-only / metadata-only boundary preserved (F-2 / concern #11):** every new/changed surface passes `ShouldHaveNoMutationAffordances()`, registers **no** command manifest, and the Cancel control cancels a **read query only** — never a domain mutation, never the lifecycle `Cancelled` state (prd.md L701, which is a different concept). No new safety channel is introduced (no `safety-channel-inventory.json` change); loading/skeleton/cancel/cancelled states render no secrets, file contents, raw diffs, raw/absolute paths, or unauthorized-resource existence.

11. **Empty / error / denial states are unchanged:** `SkeletonState` replaces **only** the loading branch. `ConsoleEmptyState` (the four empty reasons) and `ConsoleErrorPanel` (safe-denial) behaviour, freshness-honesty (`Unknown`/`Stale`/`Current`, never `0001-01-01`), pagination, and the single-`<h1>`-per-page rule are preserved exactly.

12. **Accessibility (UX-DR25 / UX-DR30 / UX-DR14):** loading regions are labelled and `aria-busy="true"`; layout stability avoids focus loss; the Cancel control is keyboard reachable with a visible focus state and an accessible name; status/loading is conveyed by text + shape, **never colour alone**. (Formal WCAG 2.2 AA / no-mutation **release verification** remains Story 6.11 — build accessibly here, but the audit is 6.11.)

13. **Out of scope — do not pull neighbouring stories in (review-failure flags):** **No NBomber console perf harness and no CI perf gate** — the budget is release-validated (Workstream 7 / Story 7.10 owns capacity calibration). **No `FakeTimeProvider` / `Microsoft.Extensions.Time.Testing` package add** — `Directory.Packages.props` is forbidden; hand-roll a controllable `TimeProvider` in the test project. **No formal WCAG/no-mutation release audit** (Story 6.11). **No skeleton on `Home`/`Tenants`/`Folders`** (no in-flight read). **No new endpoint, SDK method, generated client, package, or OpenAPI edit.** Do not pin C1/C2/C5 numbers (Workstream 7).

14. **Tests + gates:** bUnit component tests (`tests/Hexalith.Folders.UI.Tests`) cover `SkeletonState` driven by a **hand-rolled controllable `TimeProvider`** — asserting **no skeleton/spinner ≤ 400 ms**, **skeleton present after the 400 ms timer fires**, **`StillLoadingCancel` present after the 2 s timer fires**, layout-stable + labelled output, the `TestId` passthrough, and **timer disposal** on teardown — and `StillLoadingCancel` (renders the cancel control, read-only, accessible/keyboard-reachable, invokes `OnCancel`). Page tests prove each of the **seven** pages renders `SkeletonState` in its loading branch (testid preserved), that **Cancel → neutral cancelled/reload state (not `_error`, not `_unavailable`)**, and that the in-flight read receives a `CancellationToken`. All touched pages keep `ShouldHaveNoMutationAffordances()`; freshness/empty/denial/pagination behaviour is re-asserted unchanged. An E2E route smoke pass over the existing console routes still succeeds. The full UI lane stays green over the **455/455** post-6.9 baseline, with **zero** edits to `src/Hexalith.Folders.Client/Generated/`, `Directory.Packages.props`, the OpenAPI spine, or any `*.csproj`/`*.slnx`.

## Tasks / Subtasks

- [x] **Task 1 — `StillLoadingCancel` component (F-7 cancel affordance at 2 s)** (AC: 1, 4, 10, 12)
  - [x] Create `src/Hexalith.Folders.UI/Components/StillLoadingCancel.razor` (+ `.razor.cs` only if logic warrants; a small inline `@code` block is acceptable — match `DegradedModeBanner`/`CorrelationCopyButton`). `@namespace Hexalith.Folders.UI.Components`.
  - [x] Parameters: `[Parameter] public EventCallback OnCancel { get; set; }`, `[Parameter] public string? AdditionalCssClass { get; set; }`. No `IClient`.
  - [x] Render: a labelled region with the text **"still loading…"** and a **Cancel** control as `<FluentButton Appearance="Appearance.Neutral">` or `<button type="button">` (modelled on `SafeCopyId`/`CorrelationCopyButton` — must **not** match the mutation selectors). `@onclick="OnCancel"`; keyboard reachable; `aria-label="Cancel loading"`. `data-testid="console-still-loading-cancel"`. Style with shared `fc-*` classes; **no `.razor.css`**. Conveyed by text + control, never colour alone (UX-DR14).
- [x] **Task 2 — `SkeletonState` component (F-7 timed perceived-wait, 400 ms / 2 s)** (AC: 1, 2, 3, 4, 9, 12)
  - [x] Create `src/Hexalith.Folders.UI/Components/SkeletonState.razor` + `SkeletonState.razor.cs` (`public partial class SkeletonState : ComponentBase, IDisposable`, `namespace Hexalith.Folders.UI.Components`).
  - [x] Parameters: `[Parameter, EditorRequired] public string Label { get; set; }` (what is loading), `[Parameter] public string? TestId { get; set; }` (the page's `console-page-{name}-loading` token — render it on the root region), `[Parameter] public EventCallback OnCancel { get; set; }`, `[Parameter] public string? AdditionalCssClass { get; set; }`. `[Inject] private TimeProvider Clock { get; set; } = default!;`.
  - [x] Timing: thresholds `private static readonly TimeSpan SkeletonDelay = TimeSpan.FromMilliseconds(400);` and `private static readonly TimeSpan CancelDelay = TimeSpan.FromSeconds(2);`. In `OnInitialized`, schedule two one-shot timers via `Clock.CreateTimer(_ => InvokeAsync(() => { _showSkeleton = true; StateHasChanged(); }), null, SkeletonDelay, Timeout.InfiniteTimeSpan)` and similarly for `_showCancel` at `CancelDelay`. Hold the `ITimer` handles in fields; `Dispose()` disposes both. (Both timers measure from mount; the 2 s timer is absolute-from-mount, not 2 s-after-skeleton.)
  - [x] Render bands: `≤400 ms` (`!_showSkeleton`) → a minimal `<div data-testid="@TestId" aria-busy="true">` labelled region only (no skeleton bars, no spinner); `400 ms–2 s` (`_showSkeleton && !_showCancel`) → the **layout-stable skeleton** (Fluent UI `FluentSkeleton`/`FluentProgressRing` or `fc-*` placeholder bars) with the `Label` as the accessible loading label; `≥2 s` (`_showCancel`) → skeleton **plus** `<StillLoadingCancel OnCancel="OnCancel" />`. Always keep `aria-busy="true"` + the labelled region for assistive tech.
  - [x] Register the clock: in `src/Hexalith.Folders.UI/CompositionRoot.cs`, add `services.AddSingleton(TimeProvider.System);` near the other UI registrations (AC #9). **Do not** add any package; `Directory.Packages.props` stays untouched.
- [x] **Task 3 — Wire perceived-wait + cancellation into the seven data pages** (AC: 5, 6, 7, 10, 11)
  - [x] For **each** of `Components/Pages/{Workspace,FolderDetail,AuditTrail,OperationTimeline,Provider,ProviderSupport,IncidentStream}.razor(.cs)`: add a `private CancellationTokenSource? _cts;` and a `private bool _cancelled;` field; in the load method, create a fresh `_cts` (dispose any prior), set `_cancelled = false`, and pass `_cts.Token` into the **`CancellationToken` overload** of the primary `IClient` read **and** into the supplementary `TryReadAsync` reads (extend `TryReadAsync` to accept and forward a token). Keep `.ConfigureAwait(false)` on every await (CA2007 is warnings-as-error).
  - [x] Catch the cancellation distinctly: a `catch (OperationCanceledException)` (covers `TaskCanceledException`) sets `_cancelled = true; _loading = false;` and returns — **do not** route it to `_error` or `_unavailable`. (Keep the existing `HttpRequestException`/`TaskCanceledException → _unavailable` split for *transport* failures, but a token-triggered cancel must be distinguished — check `_cts.IsCancellationRequested` to disambiguate, or order the `catch (OperationCanceledException) when (_cts?.IsCancellationRequested == true)` clause first.)
  - [x] Replace the loading `<p data-testid="console-page-{name}-loading" aria-busy="true">…</p>` branch with `<SkeletonState Label="@<what-is-loading>" TestId="console-page-{name}-loading" OnCancel="CancelAsync" />`, preserving the page's existing branch order. Add a `private Task CancelAsync()` that calls `_cts?.Cancel()` (and `StateHasChanged` if needed).
  - [x] Add a **neutral cancelled branch** to each page's render chain (before/after the empty branch, never as an error): when `_cancelled`, render the page root + `<h1>` + banners + a short non-error message and a **read-only reload** `<a href="@<same-route>">Reload</a>` (`data-testid="console-page-{name}-reload"`). For `IncidentStream`, the `DegradedModeBanner` still renders unconditionally (AC #7).
  - [x] Dispose `_cts` with the component: implement `IDisposable` on the page partial classes that newly own a `CancellationTokenSource` (dispose in `Dispose()`), or reuse an existing dispose hook; do not leak token sources across reloads.
- [x] **Task 4 — Performance-budget release evidence doc (F-7)** (AC: 8, 13)
  - [x] Create `docs/ux/ops-console-performance-budget.md` (beside `ops-console-wireflows.md`): record the F-7 budget verbatim (p95 < 1.5 s primary; p99 < 3 s; degraded ≤ 5 s p95) and the NFR status/audit 500 ms p95; cite `architecture.md L551` (F-7), `L109` (NFR budgets), `prd.md L714–718`. State the **measurement method = release validation** (`prd.md L778–780`), numbers **`reference_pending`** (aligned with `docs/exit-criteria/c0-c13-governance-evidence.yaml`), the **perceived-wait UX (Story 6.10) as the shipped mitigation**, and that **no CI perf gate / NBomber console harness** is added here (deferred to Workstream 7 / Story 7.10). Metadata-only; no fabricated numbers.
- [x] **Task 5 — Tests** (AC: 14)
  - [x] `tests/Hexalith.Folders.UI.Tests/`: add a **controllable** `TimeProvider` test double (e.g. `ControllableTimeProvider : TimeProvider` overriding `CreateTimer` to capture the callback + due time and exposing `Advance(TimeSpan)` / `FireDueTimers()`), modelled on the existing `FixedTimeProvider : TimeProvider` pattern (see References) — **no package**. Register it in the bUnit `Services` for tests that render `SkeletonState`.
  - [x] `tests/Hexalith.Folders.UI.Tests/SkeletonStateTests.cs`: assert **no skeleton bars / no spinner before the 400 ms timer fires** (only the labelled `aria-busy` region with the `TestId`); after advancing past 400 ms → skeleton present and `Label` rendered; after advancing past 2 s → `console-still-loading-cancel` present; layout stability (root testid stable across bands); `OnCancel` reachable; and that disposing the component disposes its timers (no callback after teardown). Use `rendered.WaitForAssertion(...)` for post-advance state.
  - [x] `tests/Hexalith.Folders.UI.Tests/StillLoadingCancelTests.cs`: renders `console-still-loading-cancel`, a read-only `<button type="button">`/`<FluentButton>`, accessible name + keyboard reachable; clicking invokes `OnCancel` (assert via an `EventCallback` bound to a flag); `ShouldHaveNoMutationAffordances()`.
  - [x] Page tests: for each of the seven pages (extend the existing `*PageTests` — `WorkspacePageTests`, `FolderDetailPageTests`, `AuditTrailPageTests`, `OperationTimelinePageTests`, `ProviderPageTests`, `ProviderSupportPageTests`, `IncidentStreamPageTests`), add assertions that the loading branch renders `SkeletonState` (testid preserved); that an injected `IClient` stub which throws `OperationCanceledException` (simulating a cancelled token) drives the **neutral cancelled/reload state** (`console-page-{name}-reload` present, **no** `ConsoleErrorPanel`, **no** `read_model_unavailable` empty state); and that the primary read is invoked with a `CancellationToken` (NSubstitute `Arg.Any<CancellationToken>()`). Keep `ShouldHaveNoMutationAffordances()`. Add `#pragma warning disable xUnit1051` where stubbing CT overloads. **Record every modified `*PageTests.cs` file in the File List** (the recurring 6.6/6.7/6.8/6.9 File-List lapse).
  - [x] Confirm the existing E2E smoke lane (`tests/Hexalith.Folders.UI.E2E.Tests`) still builds and the console route smokes still pass (backend-less host still renders root + `<h1>`); no new routes are required by this story.
- [x] **Task 6 — Verify gates green & record fidelity** (AC: 14)
  - [x] Build/test with the **Windows SDK** (`/mnt/c/Program Files/dotnet/dotnet.exe`); capture the baseline `dotnet test tests/Hexalith.Folders.UI.Tests` count (expect **455/455** post-6.9), then the final count. Build `tests/Hexalith.Folders.UI.E2E.Tests`.
  - [x] Confirm `NavigationContractTests.Console_DoesNotRegisterAnyDomainCommandManifest` stays green (the two pre-existing `Hexalith.Folders.IntegrationTests`/`LoadTests` `ScaffoldContractTests` reds — `SolutionContainsOnlyCanonicalBuildableProjects`, `ProjectReferencesFollowAllowedDependencyDirection` — are **not** 6.10 regressions; see [[scaffold-contract-tests-baseline-reds]]). Confirm `git diff --stat src/Hexalith.Folders.Client/Generated/` empty; `Directory.Packages.props`, OpenAPI spine, and all `*.csproj`/`*.slnx` untouched (`git diff --stat -- '*.csproj' '*.slnx' Directory.Packages.props` empty); **no new `<PackageReference>`/`<ProjectReference>`**.
  - [x] Optional safety gate: `tests/tools/run-safety-invariant-gates.ps1`.
  - [x] Record the **complete** File List (every source AND test file, including modified `*PageTests.cs` files and the new doc) and the accurate test count in the Dev Agent Record.

## Dev Notes

**Scope is UI-only and consumes already-shipped SDK reads.** This story adds **no** endpoint, SDK method, generated client, package, or OpenAPI content. It ships two new presentational components (`SkeletonState`, `StillLoadingCancel`), wires them + a `CancellationTokenSource` into the seven existing data pages, registers the BCL `TimeProvider`, and records the F-7 budget as release evidence. The seven pages already load projection data through the typed SDK `IClient`; this story changes **how the wait is presented and cancelled**, not what is read.

### Scope & the performance-budget variance (read first)

The AC reads "primary diagnostic flows meet p95 and p99 budgets **or** produce measured release evidence." In this repo the budget half is **not** buildable as a CI gate in an Epic-6 UI story:

- F-7 is **release-verified, not CI-gated** — `prd.md L778–780` puts performance/accessibility/console-usability NFRs on the **release-validation-evidence** path; `architecture.md L1497`/`L1556` list F-7 under release performance considerations, not a gate.
- The NBomber load lane (`tests/load/`) is scoped to **C1 capacity** (lifecycle), **not** the F-7 console page-load budget (`architecture.md L202, L1298`); `docs/exit-criteria/c1-capacity.md`/`c2-freshness.md` do **not** exist yet, and C1/C2/C5 are `reference_pending` in `docs/exit-criteria/c0-c13-governance-evidence.yaml`.
- Latency is explicitly **not** a cross-surface parity dimension (`architecture.md L107`).

**Therefore:** the shippable, testable deliverable is the **perceived-wait UX** (the part the AC ties to skeleton-at-400 ms + cancel-at-2 s), plus a **documented release-evidence artifact** (`docs/ux/ops-console-performance-budget.md`) that records the F-7 targets and names the measurement method (release validation, numbers `reference_pending`). **Do not** stand up a console perf harness or a CI perf gate, and **do not** pin p95/p99 numbers — that is Workstream 7 / Story 7.10. This mirrors how 6.6–6.9 deferred F-7 to this story while keeping budget-number-pinning out of Epic 6.

### Technical requirements (dev-agent guardrails)

- **Override `OnParametersSetAsync`, not `OnInitializedAsync`** — every data page already does; keep it. The skeleton timing lives in `SkeletonState` (self-timed via `TimeProvider`), so the page's data `await` does **not** need to be restructured into a background task — `SkeletonState` re-renders itself at 400 ms / 2 s while the page's `_loading` is true.
- **Thread the `CancellationToken`** into the **`CancellationToken` overload** of the primary read and the supplementary `TryReadAsync` reads (overloads confirmed in the generated client). `OnCancel` → `_cts.Cancel()`. A cancelled read throws `OperationCanceledException`/`TaskCanceledException`; catch it **distinctly** from transport failure (use `when (_cts?.IsCancellationRequested == true)` or check the flag) and resolve to the **neutral `_cancelled` state**, never `_error`/`_unavailable`.
- **One fresh `CancellationTokenSource` per load**, disposed with the component and replaced on reload; keep the existing one-fresh-`_correlationId`-per-load discipline (`Guid.NewGuid().ToString()`, surfaced via `SafeCopyId`), `ReadConsistencyClass.Eventually_consistent` on every read, `filter: null` on every call (C4), `PageLimit` const where paginated.
- **`.ConfigureAwait(false)` on every await** (CA2007 warnings-as-error). Timer callbacks must marshal back via `InvokeAsync(StateHasChanged)` (Blazor Server circuit affinity).
- **Dispose timers and token sources** — `SkeletonState` disposes its `ITimer`s; pages owning a `CancellationTokenSource` dispose it. No leaked circuit resources.
- **No per-page `@rendermode`** — the app is globally Interactive Server (`Program.cs` / `App.razor`); do not add render-mode attributes.
- **One `<h1>` per page; kebab-case `data-testid`; pagination via `<a href>` only; plain semantic `<table>` (no `FluentDataGrid`).** Preserve these.

### Architecture compliance

- **F-7 (architecture.md L551)** — the authoritative budget + perceived-wait contract: p95 < 1.5 s primary, p99 < 3 s, degraded (incident) ≤ 5 s p95; skeleton at 400 ms; "still loading… [cancel]" at 2 s. This story implements the perceived-wait half and documents the budget half.
- **F-2 (L546)** — Blazor Server + SignalR, reads only from projections via the SDK; perceived-wait timing is over a SignalR round-trip path. **F-1 (L545)** Blazor Server; **F-3 (L547)** Fluent UI provides the accessible primitives backing the skeleton/cancel.
- **F-6 (L550)** — the incident-mode path (`IncidentStream`) is the degraded ≤ 5 s p95 branch; its `DegradedModeBanner` and `RedactedField` policy are **not** relaxed during loading (AC #7).
- **Component file locations are fixed** by the architecture step-3 layout: `SkeletonState.razor` and `StillLoadingCancel.razor` under `Hexalith.Folders.UI/Components/` (architecture.md L1198–1199; cross-cutting concern #11 traceability L1373).
- **C2 status-freshness** (L203, Phase-4 exit, BLOCKS Phase-8 ops-console UX) and **C1/C5 capacity** stay `reference_pending` — do **not** pin them here.
- **Read-only console boundary (concern #11, project-context):** no mutation/repair/credential-reveal/file-content; the Cancel control cancels a read query only.

### Library / framework requirements

**Reuse these shipped components (do NOT reinvent):**

- `DegradedModeBanner.razor` (testid `incident-degraded-mode-banner`) — the **exact dumb-component template** to model `SkeletonState`/`StillLoadingCancel` on (`@namespace`, doc-comment, `data-testid`, `fc-*` classes, inline `@code`, no `IClient`).
- `SafeCopyId.razor` / `CorrelationCopyButton.razor` — the **read-only `<button type="button">` template** that passes the mutation guard; model the Cancel control on these.
- `ConsoleEmptyState.razor` (`EmptyStateReason` `NoMatches`/`InsufficientFilterScope`/`ReadModelUnavailable`/`DeniedAccess`) and `ConsoleErrorPanel.razor` (takes `ConsoleErrorView`) — the loading branch is **adjacent** to these; do not change their behaviour.
- `Components/Icons/FoldersConsoleIcons.cs` (`ErrorCircle16()`/`Warning16()`/`LockClosed16()`) — hand-authored glyphs; the Fluent UI **Icons** package is deliberately **off** the graph — do not add it.
- Fluent UI primitives already on the graph and registered (`Microsoft.FluentUI.AspNetCore.Components`, `services.AddFluentUIComponents()` in `CompositionRoot`): `FluentSkeleton` (+`SkeletonPattern`), `FluentProgressRing`, `FluentProgressBar`, `FluentButton` — usable for the skeleton/cancel visuals (`@using` already in `Components/_Imports.razor`).

**Reuse these services / patterns:** `ConsoleErrorPresenter.FromException`, the `private static TryReadAsync<T>(Func<Task<T>>)` swallow-denial helper (extend to accept a `CancellationToken`), the freshness-honesty helpers (`Unknown`/`Stale`/`Current`, never `0001-01-01`), and the per-load `_correlationId` discipline.

**`TimeProvider` (clock for 400 ms / 2 s):** use the **BCL `System.TimeProvider`** (`net10.0`, no package). Register `services.AddSingleton(TimeProvider.System)`; inject into `SkeletonState`; schedule with `Clock.CreateTimer(...)`. **Do not** add `Microsoft.Extensions.Time.Testing` (`FakeTimeProvider`) — `Directory.Packages.props` is forbidden; tests hand-roll a controllable `TimeProvider` (the repo already hand-rolls `FixedTimeProvider : TimeProvider`).

**Forbidden touches:** `src/Hexalith.Folders.Client/Generated/**`, `src/Hexalith.Folders.Client/Compat/**`, `Directory.Packages.props`, `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`, all `*.csproj`/`*.slnx`. `Hexalith.Folders.UI` may reference only `Hexalith.Folders.Client` + `Hexalith.FrontComposer.Shell`. **No `.razor.css`** anywhere — style via shared `fc-*` classes. Target `net10.0`, nullable, warnings-as-errors, C# `LangVersion=latest`. Submodules root-level only, never `--recursive`.

### File structure requirements

- New components: `src/Hexalith.Folders.UI/Components/SkeletonState.razor` (+ `.razor.cs`), `src/Hexalith.Folders.UI/Components/StillLoadingCancel.razor` (+ `.razor.cs` if needed).
- Edited: `src/Hexalith.Folders.UI/CompositionRoot.cs` (register `TimeProvider.System`); the seven pages under `src/Hexalith.Folders.UI/Components/Pages/` (`Workspace`, `FolderDetail`, `AuditTrail`, `OperationTimeline`, `Provider`, `ProviderSupport`, `IncidentStream`) — `.razor` (loading branch + cancelled branch) and `.razor.cs` (CTS + token threading; `FolderDetail` uses inline `@code`).
- New doc: `docs/ux/ops-console-performance-budget.md`.
- Tests: `tests/Hexalith.Folders.UI.Tests/` — new `SkeletonStateTests.cs`, `StillLoadingCancelTests.cs`, the controllable `TimeProvider` double; edited `*PageTests.cs` for the seven pages. **Record every modified test file.**

### Testing requirements

- bUnit 2.7.2 + xUnit v3 + Shouldly + NSubstitute via the shared `DiagnosticTestContext.Create(tenantId, userId)` (SDK-page fixture) / `BadgeRenderingFixture` (component fixture) and `ConsoleTestAssertions.ShouldHaveNoMutationAffordances()` (the five-selector guard).
- **Deterministic timing without a package:** hand-roll `ControllableTimeProvider : TimeProvider` (override `CreateTimer` to capture callback + due time; expose `Advance(TimeSpan)`/`FireDueTimers()`), modelled on the existing `FixedTimeProvider : TimeProvider` (`tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs:644`, `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderBranchRefPolicyConfigurationServiceTests.cs:298`). Register it in the bUnit `Services` before rendering `SkeletonState`. Wrap post-advance assertions in `rendered.WaitForAssertion(...)`.
- Page tests: set `[SupplyParameterFromQuery]` params via `NavigationManager.NavigateTo(...)` **before** `Ctx.Render<Page>()` (not `p.Add`); set route params via `p.Add(...)`. Stub `IClient` with NSubstitute `.Returns(...)` / `.ThrowsAsync(...)`; simulate cancel with `.ThrowsAsync(new OperationCanceledException())`. For null-filter matchers use `Arg.Is<string>(f => f == null)` (never bare `null` mixed with `Arg` matchers → `AmbiguousArgumentsException`). Add `#pragma warning disable xUnit1051` when stubbing the CT overloads.
- Cover the loading-band states, the neutral cancelled/reload state (distinct from error/unavailable), `CancellationToken` propagation, mutation-guard, and that empty/error/freshness/pagination behaviour is unchanged. Leave the **formal** WCAG/no-mutation audit to Story 6.11.

### Previous story intelligence (6.6 / 6.8 / 6.9 — apply these lessons)

- **F-7 was deferred to here by every prior story.** 6.9 AC #14, 6.8 AC, 6.6, and 6.2 all shipped only the simple `aria-busy` loading paragraph and explicitly said `SkeletonState`/`StillLoadingCancel` + the perf gate are Story 6.10. This story closes that.
- **6.9 IncidentStream pattern to build on:** `OnParametersSetAsync` (not `OnInitialized`), per-load `_correlationId`, `ReadConsistencyClass.Eventually_consistent`, primary read in `try/catch` splitting `HexalithFoldersApiException` (→ `ConsoleErrorPanel`) from `HttpRequestException`/`TaskCanceledException` (→ `ReadModelUnavailable`), supplementary reads via `TryReadAsync`, `DegradedModeBanner` rendered unconditionally. 6.10 adds the cancel-token split **in front of** the transport-failure split.
- **File-List fidelity is the recurring review ding** (6.6/6.7/6.8/6.9 all lost points for omitting modified `*PageTests.cs`). Record every created **and** modified file (source **and** tests) + the accurate test count.
- **Freshness honesty:** never render `0001-01-01` or "Current" for absent freshness; absent → `Unknown`. The loading/cancelled states must not fabricate timestamps.
- **Prefer the server value; do not re-derive.** **No scope creep** — leave formal WCAG / no-mutation audit to 6.11 and capacity/perf-number calibration to Workstream 7.

### Git intelligence summary

- HEAD `6dcfc2d feat(story-6.9): Implement incident-mode last-resort read path`; preceding 6.8 `90717e4`, 6.7 `f7774d1`, 6.6 `aba57e2`. Each Epic-6 commit added one console slice on the shared component/test scaffold; 6.10 follows the same shape (new shared components + page wiring + tests), and is the last build slice before 6.11 (verification) and Workstream 7 (release).

### Project structure notes

- **Alignment:** `SkeletonState.razor` / `StillLoadingCancel.razor` land exactly where the architecture step-3 layout places them (`Hexalith.Folders.UI/Components/`, architecture.md L1198–1199). Dumb-presentational + `fc-*` styling + no `.razor.css` matches every shipped console component.
- **Variance:** the architecture layout lists the incident page under `Pages/_Admin/IncidentStream.razor`, but the as-built convention places all pages under `Components/Pages/` (the 6.9 variance, already accepted) — keep that. The performance-**budget** evidence artifact (`docs/ux/ops-console-performance-budget.md`) is new; it is documentation, not a CI gate, and is the in-domain way to "produce measured release evidence" without touching Workstream-7-owned governance artifacts.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-6.10] (L1436–1447) — story statement + BDD ACs (perceived-wait + budget-or-evidence).
- [Source: _bmad-output/planning-artifacts/architecture.md#F-7] (L551) — **the authoritative budget + perceived-wait contract**; F-2 (L546), F-1 (L545), F-3 (L547), F-6 (L550) console decisions; performance budgets (L109); latency-not-a-parity-dimension (L107); release-verification (L1497, L1556); NBomber=C1-only (L202, L1298); **component file locations** (L1198–1199, L1373); C2 exit criterion (L203, L180, L228).
- [Source: docs/ux/ops-console-wireflows.md#3.7-Loading-state] (L461–484) — **the screen-level contract this story OWNS**: three timing bands, ASCII layout-stability diagram, `SkeletonState`/`StillLoadingCancel`, "Cancel returns to the prior stable view, not an error", accessibility notes. Also §3.8/§3.9 (empty/error, all pages), UX-DR25/UX-DR30 console expectations (L571, L576), traceability (L646 — 6.10 owns UX-DR25; L650 — 6.10 supports UX-DR29).
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md#UX-DR25] (L133) — layout stability + label what is loading; UX-DR30 (L138) keyboard/focus; UX-DR14 (L122, L369) status not colour-only; loading narrative (L696).
- [Source: _bmad-output/planning-artifacts/prd.md#NFR-Performance] (L714–718) — end-user budgets (status/audit 500 ms p95, etc.); release-validation path (L778–780); cancellation-supported lifecycle note (L701, distinct from the UI cancel affordance).
- **As-built code to mirror / reuse (read before coding):**
  - `src/Hexalith.Folders.UI/Components/DegradedModeBanner.razor` — dumb-component template for `SkeletonState`/`StillLoadingCancel`.
  - `src/Hexalith.Folders.UI/Components/SafeCopyId.razor` + `CorrelationCopyButton.razor` — the read-only `<button type="button">` (no-mutation) template for the Cancel control.
  - `src/Hexalith.Folders.UI/Components/Pages/IncidentStream.razor(.cs)` and `OperationTimeline.razor(.cs)` — the load/branch/freshness template + the `console-page-{name}-loading` paragraph being replaced (IncidentStream.razor:30–33 carries the 6.10 deferral comment).
  - `src/Hexalith.Folders.UI/Components/Pages/Workspace.razor.cs` — the multi-read page (primary + many `TryReadAsync`) showing where the token threads through.
  - `src/Hexalith.Folders.UI/CompositionRoot.cs` (~L60–63, 147–159) — where to add `services.AddSingleton(TimeProvider.System)`.
  - `tests/Hexalith.Folders.UI.Tests/DiagnosticTestContext.cs`, `BadgeRenderingFixture.cs`, `ConsoleTestAssertions.cs` — test fixtures + `ShouldHaveNoMutationAffordances()`.
  - `tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs:644` (and `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderBranchRefPolicyConfigurationServiceTests.cs:298`) — the `FixedTimeProvider : TimeProvider` pattern to base the controllable test double on.
- [Source: src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs] — generated SDK `IClient`; **CancellationToken overloads** at L76/L859/L1105 etc. **(read-only reference, do not edit).**
- [Source: _bmad-output/implementation-artifacts/6-9-implement-incident-mode-last-resort-read-path.md] — **the near-exact template** (house style, AC/task shape, File-List-fidelity lesson, **455/455** baseline, environment + forbidden-touch constants, the F-7 deferral that this story resolves).
- [Source: _bmad-output/project-context.md] — repo-wide rules: read-only console boundary, metadata-only, Windows-SDK build, central package management, submodule policy, `.ConfigureAwait(false)`. See also [[scaffold-contract-tests-baseline-reds]], [[dotnet-windows-sdk-wsl]].

## Dev Agent Record

### Agent Model Used

claude-opus-4-8 (1M context) - BMAD dev-story workflow

### Debug Log References

- Build/test executed with the Windows SDK (`/mnt/c/Program Files/dotnet/dotnet.exe`) per [[dotnet-windows-sdk-wsl]]; the WSL SDK fails the `global.json` 10.0.302 pin.
- **Baseline anomaly (and fix):** the as-found tree (production code already present from a prior partial session) was **red — 361 passed / 94 failed / 455**. Root cause: the seven pages were already rewired to call the **`CancellationToken` overloads** of the `IClient` reads, but the existing page-test stubs still configured the **non-CT** overloads, so NSubstitute returned defaults and the data never loaded. Resolving this — updating every page-test stub/verification to the CT overload (`Arg.Any<CancellationToken>()`) — was the bulk of Task 5 and is what restored green.
- bUnit timer determinism proven first on the component tests: `ControllableTimeProvider.Advance(...)` fires the captured one-shot callbacks, which marshal back via `InvokeAsync(StateHasChanged)`; post-advance assertions wrapped in `rendered.WaitForAssertion(...)`.
- The neutral-cancelled page test drives the real chain: a cancel-aware primary-read stub (`await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false)`), advance the clock 2 s so `StillLoadingCancel` renders, click it (→ `_cts.Cancel()`), then assert the reload state. `.ConfigureAwait(false)` is required inside the stub lambda (CA2007 is warnings-as-error even in tests).

### Completion Notes List

- **Result: all 6 tasks complete; UI lane green at 486/486** (post-6.9 baseline 455 + 31 new tests: 7 `SkeletonState`, 4 `StillLoadingCancel`, and 20 page tests = 3 per page × 6 pages + 2 for `ProviderSupport` which has no supplementary read). Zero warnings (warnings-as-error). E2E project (`Hexalith.Folders.UI.E2E.Tests`) builds clean. (Count verified by `dotnet test tests/Hexalith.Folders.UI.Tests` on the Windows SDK during the 6.10 review pass: 486 passed / 0 failed.)
- **Tasks 1–4 (production)** were already implemented in the tree and verified correct here: `StillLoadingCancel` (inline `@code`, read-only `<button type="button">`, `data-testid="console-still-loading-cancel"`), `SkeletonState` (`IDisposable`, two one-shot `TimeProvider` timers at 400 ms / 2 s, three bands, stable `TestId` root + `aria-busy`), `TimeProvider.System` registered in `CompositionRoot`, all seven pages wired with a per-load `CancellationTokenSource`, the cancel-distinct `catch (OperationCanceledException) when (_cts.IsCancellationRequested)` clause ordered ahead of the transport-failure catch, a neutral `_cancelled` reload branch (distinct from `_error`/`_unavailable`), and the `docs/ux/ops-console-performance-budget.md` release-evidence doc.
- **Task 5 (tests) — the work completed this session:** authored `SkeletonStateTests` + `StillLoadingCancelTests` + the `ControllableTimeProvider` double (already present, verified) ; and fixed + extended all **seven** `*PageTests.cs` — every `IClient` read stub/verification migrated to the CT overload, the stale `xUnit1051` pragma comments refreshed, and three tests added per page (`PrimaryRead_ReceivesCancellationToken` proves token propagation; `SupplementaryReads_ReceiveCancellationToken` proves the `TryReadAsync` reads forward the token; `CancelDuringLoad_RendersNeutralCancelledReloadState_NotErrorNorUnavailable` proves Cancel → neutral reload state, not `console-error-panel` and not `read_model_unavailable`) — except `ProviderSupport`, which adds two (it has no supplementary read, so no `SupplementaryReads_ReceiveCancellationToken` test). The IncidentStream cancel test also re-asserts the `incident-degraded-mode-banner` renders in the cancelled branch (AC #7).
- **Gates:** `NavigationContractTests.Console_DoesNotRegisterAnyDomainCommandManifest` green (within the 486). `git diff` of `src/Hexalith.Folders.Client/Generated/`, `Directory.Packages.props`, all `*.csproj`/`*.slnx`, and the OpenAPI spine is **empty**; no new `<PackageReference>`/`<ProjectReference>`. The two pre-existing `ScaffoldContractTests` reds (IntegrationTests/LoadTests) are unrelated to 6.10 per [[scaffold-contract-tests-baseline-reds]]. The optional `run-safety-invariant-gates.ps1` was **not** run this session (PowerShell/host setup); loading/skeleton/cancel/cancelled states render no event content, secrets, paths, or payloads, and the no-mutation guard passes on every touched surface.
- **Scope honoured:** no console perf harness / CI perf gate (release-validated, Workstream 7 / Story 7.10); no `FakeTimeProvider` package add (hand-rolled `ControllableTimeProvider`); no formal WCAG/no-mutation audit (Story 6.11); no skeleton on `Home`/`Tenants`/`Folders`; no p95/p99 numbers pinned (`reference_pending`).

### File List

**New (created):**

- `src/Hexalith.Folders.UI/Components/SkeletonState.razor`
- `src/Hexalith.Folders.UI/Components/SkeletonState.razor.cs`
- `src/Hexalith.Folders.UI/Components/StillLoadingCancel.razor` (inline `@code`; no `.razor.cs`)
- `docs/ux/ops-console-performance-budget.md`
- `tests/Hexalith.Folders.UI.Tests/ControllableTimeProvider.cs`
- `tests/Hexalith.Folders.UI.Tests/SkeletonStateTests.cs`
- `tests/Hexalith.Folders.UI.Tests/StillLoadingCancelTests.cs`

**Modified (source):**

- `src/Hexalith.Folders.UI/CompositionRoot.cs` (register `TimeProvider.System`)
- `src/Hexalith.Folders.UI/Components/Pages/Workspace.razor`
- `src/Hexalith.Folders.UI/Components/Pages/Workspace.razor.cs`
- `src/Hexalith.Folders.UI/Components/Pages/FolderDetail.razor` (inline `@code`)
- `src/Hexalith.Folders.UI/Components/Pages/AuditTrail.razor`
- `src/Hexalith.Folders.UI/Components/Pages/AuditTrail.razor.cs`
- `src/Hexalith.Folders.UI/Components/Pages/OperationTimeline.razor`
- `src/Hexalith.Folders.UI/Components/Pages/OperationTimeline.razor.cs`
- `src/Hexalith.Folders.UI/Components/Pages/Provider.razor`
- `src/Hexalith.Folders.UI/Components/Pages/Provider.razor.cs`
- `src/Hexalith.Folders.UI/Components/Pages/ProviderSupport.razor`
- `src/Hexalith.Folders.UI/Components/Pages/ProviderSupport.razor.cs`
- `src/Hexalith.Folders.UI/Components/Pages/IncidentStream.razor`
- `src/Hexalith.Folders.UI/Components/Pages/IncidentStream.razor.cs`

**Modified (tests):**

- `tests/Hexalith.Folders.UI.Tests/BadgeRenderingFixture.cs` (register a controllable `TimeProvider`)
- `tests/Hexalith.Folders.UI.Tests/WorkspacePageTests.cs`
- `tests/Hexalith.Folders.UI.Tests/FolderDetailPageTests.cs`
- `tests/Hexalith.Folders.UI.Tests/AuditTrailPageTests.cs`
- `tests/Hexalith.Folders.UI.Tests/OperationTimelinePageTests.cs`
- `tests/Hexalith.Folders.UI.Tests/ProviderPageTests.cs`
- `tests/Hexalith.Folders.UI.Tests/ProviderSupportPageTests.cs`
- `tests/Hexalith.Folders.UI.Tests/IncidentStreamPageTests.cs`

**Also updated (tracking):** `_bmad-output/implementation-artifacts/sprint-status.yaml` (6-10 → review).

## Senior Developer Review (AI)

**Reviewer:** Jerome — **Date:** 2026-05-29 — **Outcome: Approve (status → done).**

**Method:** Adversarial multi-agent review (bmad-story-automator-review). Nine parallel finders attacked AC coverage (1–5, 6–10, 11–14), the task-completion audit, the two new components, page-wiring for all seven pages (Workspace/FolderDetail/AuditTrail/OperationTimeline + Provider/ProviderSupport/IncidentStream, incl. degraded/redaction safety), test quality, and the forbidden-touch/File-List/doc boundary. Each raised finding was independently verified against the real code. Ground truth taken from an actual `dotnet test` run on the Windows SDK.

**Verification result — green:** UI lane **486 passed / 0 failed / 486 total**, zero warnings (warnings-as-error). Forbidden paths confirmed untouched (`git diff` empty for `src/Hexalith.Folders.Client/Generated/`, `Compat/`, `Directory.Packages.props`, the OpenAPI spine, all `*.csproj`/`*.slnx`); **no `.razor.css`**; no new `<PackageReference>`/`<ProjectReference>`.

**Acceptance Criteria:** AC 1–14 all validated as **implemented**. Two presentational components exist at the architecture-fixed locations; three `TimeProvider`-driven bands (400 ms / 2 s) with both one-shot timers disposed; layout-stable labelled skeleton; read-only `<button type="button">` cancel that trips no mutation selector; per-load `CancellationTokenSource` threaded into the primary read **and** every `TryReadAsync` across all seven pages; cancel resolved to a neutral `_cancelled` reload state distinct from `_error`/`_unavailable` via an ordered/guarded `catch`; `IncidentStream` keeps `DegradedModeBanner` unconditional and redaction un-relaxed in every branch; `TimeProvider.System` registered with no package; release-evidence doc records the F-7 budget with `reference_pending` (no fabricated numbers); read-only/metadata boundary preserved.

**Findings (6 raised; 5 refuted on verification, 1 confirmed):**

| Sev | Finding | Verdict |
| --- | --- | --- |
| LOW (real) | Dev Agent Record undercounted tests (478 / +23 / "2 per page") | **Fixed** this pass → 486 / +31 / 3 per page (2 for ProviderSupport); AC #14/Task 6 require an accurate count. |
| MEDIUM→ref. | `SkeletonState` timer-fire-after-`Dispose` race (no `_disposed` guard) | **Refuted** — AC #2/Task 2 require disposing the timers (done) and `IDisposable` (the chosen branch of the AC's `IDisposable`/`IAsyncDisposable`). A `_disposed`-guard is defensive hardening that belongs to the deferred verification pass (Story 6.11), not a 6.10 defect. *Optional 6.11 follow-up.* |
| MEDIUM→ref. | Page tests assert `Arg.Any<CancellationToken>()` (matches `None`) | **Refuted** — AC #14/Task 5 *literally prescribe* `Arg.Any<CancellationToken>()`; the live `_cts.Token` is regression-covered by the per-page `CancelDuringLoad_…` test (`Task.Delay(Timeout.Infinite, ct)` only completes on `_cts.Cancel()`). |
| LOW→ref. | Timer-disposal test asserts only timer count, no callback probe | **Refuted** — disposal contract genuinely asserted (count 2→0 fails if a timer is not disposed); callback-counter is defence-in-depth only. |
| LOW→ref. | New `fc-skeleton-*` / `fc-still-loading-*` CSS classes undefined | **Refuted** — identical convention to shipped `DegradedModeBanner` (`fc-degraded-mode-banner` is equally undefined in repo CSS); AC #1/#3 permit "shared `fc-*` placeholder markup"; console CSS lives in the FrontComposer shell and is out of this UI story's scope (and `.razor.css` is forbidden). *Optional shell follow-up.* |

**No CRITICAL or HIGH issues.** Production code and tests were already correct; the only required change was the documentation count, applied here.

## Change Log

| Date | Change |
| --- | --- |
| 2026-05-29 | Story 6.10 implemented: timed perceived-wait UX (`SkeletonState` 400 ms / 2 s + `StillLoadingCancel`) wired with per-load `CancellationTokenSource` cancellation into the seven diagnostic pages; `TimeProvider.System` registered; F-7 performance budget recorded as release evidence. Test lane completed — `SkeletonStateTests`, `StillLoadingCancelTests`, the `ControllableTimeProvider` double, and CT-overload stub fixes + cancel/CT tests across all seven `*PageTests.cs`. UI lane green at 486/486 (was 455; +31 new tests). Status → review. |
| 2026-05-29 | Story 6.10 reviewed (bmad-story-automator-review, adversarial). Build/test re-verified on the Windows SDK: **486/486 UI tests pass, zero warnings**; forbidden paths (`Generated/`, `Directory.Packages.props`, OpenAPI spine, all `*.csproj`/`*.slnx`) confirmed untouched; no `.razor.css`; no new package/project references. Corrected the Dev Agent Record test-count inaccuracy (was 478/+23; actual 486/+31). All adversarial findings on correctness, AC coverage, page wiring, cancellation, redaction/degraded safety, and test quality were verified and either clean or refuted (timer-`_disposed`-guard hardening and console `fc-skeleton` CSS styling noted as out-of-scope follow-ups — 6.11 / shell). No CRITICAL issues. Status → done. |
