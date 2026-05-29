---
baseline_commit: f7774d1
---

# Story 6.8: Build audit and operation-timeline diagnostic pages

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an audit reviewer and operator,
I want audit and operation-timeline diagnostic pages,
so that incidents can be reconstructed from metadata-only evidence.

## Acceptance Criteria

> **Epic 6.8 BDD (verbatim, epics.md L1410–1421):**
> **Given** audit projection endpoints and console wireflow notes exist
> **When** audit and timeline pages render
> **Then** records are paginated, filtered, tenant-scoped, and show actor, task, operation, correlation, folder, provider, timestamp, result, duration, state transition, and sanitized error category where authorized
> **And** sensitive metadata classification and redaction affordances are applied consistently.

Decomposed, testable acceptance criteria:

1. **Audit-trail page** exists at `@page "/folders/{FolderId}/audit-trail"` (route param `FolderId`), rendering `ListAuditTrailAsync` results as a paginated, newest-first list. Page root `data-testid="console-page-audit-trail-root"`, exactly one `<h1>`.
2. **Operation-timeline page** exists at `@page "/folders/{FolderId}/operation-timeline"`, rendering `ListOperationTimelineAsync` results as a paginated, newest-first **Diagnostic Timeline (UX-DR8)**. Page root `data-testid="console-page-operation-timeline-root"`, exactly one `<h1>`.
3. **Audit-trail row fields** (each `AuditRecord`, where authorized): evidence **timestamp**, **actor**, **operation** id, **task** id, **correlation** id, **result** (`ResultStatus`), **sanitized error category** (`SanitizedErrorCategory`), **retryable** posture (advisory, never an action), **duration** (ms), **changed-path evidence** (metadata-only digest/reference), and the per-record redaction marker.
4. **Timeline row fields** (each `OperationTimelineEntry`, where authorized): evidence **timestamp**, **operation** id, **task** id, **correlation** id, **workspace reference** (folder/workspace), **state transition** (`from → to`) with the **operator-disposition** badge as the primary visual, **sanitized result** (`SanitizedResult`), **retryable** posture, and **duration** (ms).
5. **Pagination** is cursor-based and read-only: a `[SupplyParameterFromQuery] string? Cursor` parameter drives the load; a "Next page" `<a href>` (never a button/form) appears when `Page.IsTruncated` and `Page.Cursor` is present, otherwise an "End of results" marker. No client-side filtering/hiding of returned rows.
6. **"Filtered" = server capability only — NO filter UI.** The `filter` query parameter is rejection-only today (C4). Pages pass `filter: null`/empty on every call and MUST NOT render any filter control that implies unsupported filtering works.
7. **Tenant scope** is rendered via `<TenantScopeBanner>` at the top of each page (scope-before-evidence, UX-DR4/DR6), sourced from `IUserContextAccessor.TenantId` — **never** from the `{FolderId}` route, cursor, or any client input.
8. **Redaction affordances applied consistently (F-5 / UX-DR10/22):** every redactable field renders through `<RedactedField>` fed by `RedactionDisclosureMapper`. Redactable fields are: actor (`RedactableAuditActorReference`), operation (`RedactableAuditOperationReference`), **audit-page** evidence timestamp (`RedactableAuditTimestamp` — note the **timeline** `EvidenceTimestamp` is a plain non-redactable `DateTimeOffset`, formatted directly), workspace reference (`RedactableDiagnosticIdentifier`), and changed-path evidence (`ChangedPathEvidence`). `Redacted` ≠ `Unknown` ≠ `Missing` ≠ `Visible` are visibly and semantically distinct; only `Redacted` shows the lock icon; a redacted value is never emitted to the DOM.
9. **Disposition is the primary visual (F-4):** timeline state transitions render `<OperatorDispositionBadge Disposition="@entry.StateTransition.Disposition" />` (server-computed disposition passed through directly) with `<TechnicalStateMetadata>` as muted secondary metadata for `FromState`/`ToState`.
10. **Freshness honesty (UX-DR26):** each page shows a freshness label + observed-at timestamp. Absent/default freshness renders "Unknown"/"unknown" — never "Current", never a fabricated `0001-01-01`. `FreshnessMetadata.Stale == true` renders "Stale" (advisory only, no retry action).
11. **Safe denial (UX-DR21):** a `HexalithFoldersApiException` on the primary read renders `<ConsoleErrorPanel>` (via `ConsoleErrorPresenter.FromException`) and suppresses the evidence table; the canonical category token is shown, never raw server text, never a `not_found`-vs-`*_denied` existence oracle.
12. **Empty / unavailable states (UX-DR20):** zero authorized rows → `<ConsoleEmptyState Reason="EmptyStateReason.NoMatches" />`; a transport failure (`HttpRequestException`/`TaskCanceledException`) or projection-down → `<ConsoleEmptyState Reason="EmptyStateReason.ReadModelUnavailable" />`. The page root + single `<h1>` still render in every state.
13. **Read-only / metadata-only (F-2 / concern #11):** both pages pass `ShouldHaveNoMutationAffordances()` (no `form`/`fluentinputform`/`fluentdialog`/`[data-fc-command]`/`[data-fc-mutation]`), register no command manifest, and never render file contents, raw diffs, raw/absolute paths, secrets, credential values, or unauthorized-resource existence. `ChangedPathEvidence` shows only `EvidenceKind` + digest/reference + classification.
14. **"Provider" / "folder" fields rendered honestly:** the audit/timeline DTOs carry **no** distinct provider field — folder context is the `{FolderId}` route (+ timeline `WorkspaceReference`). Do not fabricate a provider column; surface folder/workspace identity only.
15. **Workspace placeholder resolved:** the 6.6 `console-page-workspace-audit-trail-pending` span in `Workspace.razor` becomes a real folder-scoped `<a href="/folders/{FolderId}/audit-trail">` link, and the Trust Matrix "Audit traceability" cell points its evidence href at the audit-trail route (mirroring 6.7's provider-placeholder resolution). The `console-page-workspace-section-audit-trail` `data-testid` stays stable.
16. **Connected evidence (UX-DR19):** the audit-trail and operation-timeline pages cross-link each other and are reachable from `FolderDetail`/`Workspace`; audit is not isolated on a disconnected page.
17. **Tests:** bUnit page tests in `tests/Hexalith.Folders.UI.Tests` cover render, field set, redaction distinctness, disposition-primary, freshness honesty, pagination, safe denial, empty/unavailable, and no-mutation; any new enum switch has a totality `[Theory]`; an E2E route smoke test is added. The full UI lane stays green (post-6.7 baseline **372/372**) with **zero** edits to `src/Hexalith.Folders.Client/Generated/`, `Directory.Packages.props`, or the OpenAPI spine.

## Tasks / Subtasks

- [x] **Task 1 — AuditTrail page (code-behind style)** (AC: 1, 3, 5, 6, 7, 8, 10, 11, 12, 13, 14)
  - [x] Create `src/Hexalith.Folders.UI/Components/Pages/AuditTrail.razor` + `AuditTrail.razor.cs` (`public partial class AuditTrail : ComponentBase`, `namespace Hexalith.Folders.UI.Components.Pages`).
  - [x] `[Parameter] public string FolderId { get; set; } = default!;` and `[Parameter, SupplyParameterFromQuery] public string? Cursor { get; set; }`.
  - [x] `[Inject] private IClient Client { get; set; } = default!;` and `[Inject] private IUserContextAccessor UserContext { get; set; } = default!;`.
  - [x] In `OnParametersSetAsync`: reset state, `_correlationId = Guid.NewGuid().ToString()`, `freshness = ReadConsistencyClass.Eventually_consistent`; primary read `Client.ListAuditTrailAsync(FolderId, _correlationId, freshness, Cursor, PageLimit, filter: null).ConfigureAwait(false)`; `catch (HexalithFoldersApiException ex)` → `_error = ConsoleErrorPresenter.FromException(ex, _correlationId)`; `catch (HttpRequestException)`/`catch (TaskCanceledException)` → `_unavailable = true`.
  - [x] Supplementary read for the scope banner: `_permissions = await TryReadAsync(() => Client.GetEffectivePermissionsAsync(FolderId, _correlationId, freshness))` (swallows denial → null).
  - [x] Render order: `<TenantScopeBanner Permissions="@_permissions" />` → `<h1>Audit · @FolderId</h1>` → branch (loading → error → unavailable/null → NoMatches → table) → pagination + freshness footer.
  - [x] Render the semantic `<table>` (plain `<table>`/`<caption>`/`<thead scope="col">`/`<tbody>` `@foreach`) with columns per AC 3, routing every redactable field through `RedactedField` + `RedactionDisclosureMapper`, identifiers through `SafeCopyId`, canonical categories through `ConsoleStatusText`.
- [x] **Task 2 — OperationTimeline page (Diagnostic Timeline, UX-DR8)** (AC: 2, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14)
  - [x] Create `Components/Pages/OperationTimeline.razor` + `.razor.cs`, mirroring Task 1 but calling `ListOperationTimelineAsync` and binding `OperationTimelineEntry`.
  - [x] State transition cell: `<TechnicalStateMetadata State="@entry.StateTransition.FromState" IncludePrefix="false" ColumnHeader="From" />` → `<TechnicalStateMetadata State="@entry.StateTransition.ToState" IncludePrefix="false" ColumnHeader="To" />` + `<OperatorDispositionBadge Disposition="@entry.StateTransition.Disposition" ColumnHeader="Disposition" />`.
  - [x] Timeline `EvidenceTimestamp` is a plain `DateTimeOffset` — format `ToString("u", CultureInfo.InvariantCulture)`; `WorkspaceReference` is redactable → `RedactedField` via `FromAuditRedaction`.
- [x] **Task 3 — Optional UI-assembly view-models** (AC: 8, 10, 14)
  - [x] If row markup gets dense, add `Components/Models/AuditRecordView.cs` / `OperationTimelineEntryView.cs` as `internal sealed record`s (a namespace-scoped record cannot be `private`) with a static `Create(...)` factory that pre-resolves each `FieldDisclosure`, normalizes blank→null (`NullIfBlank`) and default-timestamp→null (`ObservedAtOrNull`), and never carries a redacted value. Mirror `Components/Models/ProviderReadinessModel.cs` (which is `public sealed record` — match its assembler shape). These are UI-assembly only: **never** add them to `Hexalith.Folders.Contracts` or register them as a service.
- [x] **Task 4 — Resolve the Workspace 6.6 placeholder + cross-links** (AC: 15, 16)
  - [x] `Components/Pages/Workspace.razor` (L63–67): replace the `console-page-workspace-audit-trail-pending` span with `<a href="/folders/@FolderId/audit-trail">` (and an operation-timeline link); keep the `console-page-workspace-section-audit-trail` section `data-testid` stable.
  - [x] `Components/Pages/Workspace.razor.cs` (`BuildTrustCells`, L222–228): point the "Audit traceability" `TrustMatrixCell` `EvidenceHref` at `/folders/{FolderId}/audit-trail`, update its reason copy; optionally derive `TrustDimensionState` from real evidence (else keep an honest neutral).
  - [x] `Components/Pages/FolderDetail.razor`: add an audit-trail / operation-timeline cross-link (mirror 6.7's provider-link addition). Cross-link the two new pages to each other.
  - [x] Note: audit/timeline are **folder-scoped only** (no tenant-wide audit endpoint) — entry is via FolderDetail/Workspace, **not** a `Home.razor` tenant landing link.
- [x] **Task 5 — Routes + tests** (AC: 17)
  - [x] `tests/Hexalith.Folders.UI.E2E.Tests/Routes/ConsoleRoutes.cs`: add `AuditTrail(string folderId)` and `OperationTimeline(string folderId)` builders (`string.Create(CultureInfo.InvariantCulture, $"…")`, like `Provider(folderId)`). This file is the single source of route strings.
  - [x] `tests/Hexalith.Folders.UI.Tests/AuditTrailPageTests.cs` + `OperationTimelinePageTests.cs`: use `DiagnosticTestContext.Create()`, stub `IClient` with NSubstitute, render via `ctx.Render<TPage>(p => p.Add(c => c.FolderId, "folder-1"))`. Cover: root + single `<h1>` + scope-banner-first ordering; full field set rendered; redaction distinctness (`data-fc-disclosure` redacted/unknown/missing; redacted value `ShouldNotContain`); disposition-primary; freshness honesty (stale→"Stale", absent→"Unknown", never "0001"); pagination (truncated→`…-next` with `cursor=`; not truncated→`…-end`); safe denial (`console-error-category` = canonical token, no table); transport failure → `read_model_unavailable`; `ShouldHaveNoMutationAffordances()`. Add `#pragma warning disable xUnit1051`.
  - [x] If any new enum switch is introduced (e.g. `ChangedPathEvidenceEvidenceKind`, `PaginationMetadataTruncatedReason`), add a totality `[Theory]` over `Enum.GetValues<T>()` (force `en-US`) and throw-on-unknown in the mapper. Prefer extending `ConsoleStatusText` over adding a new service.
  - [x] `tests/Hexalith.Folders.UI.Tests/WorkspacePageTests.cs`: add a test proving the `console-page-workspace-audit-trail-pending` placeholder is resolved into a real folder-scoped link.
  - [x] `tests/Hexalith.Folders.UI.E2E.Tests/Smoke/AuditRouteSmokeTests.cs`: mirror `ProviderRouteSmokeTests.cs` (backend-less host → page degrades to read-model-unavailable but still renders root + single `<h1>`; assert status 200–399).
- [x] **Task 6 — Verify gates green & record fidelity** (AC: 17)
  - [x] Build/test with the **Windows SDK** (`/mnt/c/Program Files/dotnet/dotnet.exe`); run `dotnet test tests/Hexalith.Folders.UI.Tests` (capture baseline first, then final count). Build `tests/Hexalith.Folders.UI.E2E.Tests`.
  - [x] Confirm `NavigationContractTests.Console_DoesNotRegisterAnyDomainCommandManifest` and `ScaffoldContractTests.ProjectReferencesFollowAllowedDependencyDirection` stay green; `git diff --stat src/Hexalith.Folders.Client/Generated/` empty; `Directory.Packages.props` and OpenAPI spine untouched; no new `<PackageReference>`/`<ProjectReference>`.
  - [x] Record the **complete** File List (every source AND test file, incl. modified test files — the recurring 6.6/6.7 review finding) and the accurate test count in the Dev Agent Record.

## Dev Notes

**Scope is UI-only.** The audit + operation-timeline query endpoints, SDK methods, and DTOs already shipped in Story 6.1 (status: done) and were frozen by the Contract Spine / NSwag generation (Stories 1.11/1.12). This story builds two Blazor pages in `Hexalith.Folders.UI` that consume the existing typed SDK and reuse the shipped console components — it does **not** add or change any endpoint, SDK method, generated client, package, or OpenAPI content.

### Technical requirements (dev-agent guardrails)

- **Bind to the SDK (NSwag) types, NOT the server records.** `Hexalith.Folders.UI` references `Hexalith.Folders.Client` only — so the UI uses `Hexalith.Folders.Client.Generated.*`, where `AuditRecord.ResultStatus`/`SanitizedErrorCategory` and `OperationTimelineEntry.SanitizedResult` are `CanonicalErrorCategory` **enums**, `DiagnosticStateTransition.FromState`/`ToState` are `LifecycleState` enums, and `.Disposition` is an `OperatorDispositionLabel` enum. **No `Enum.Parse`/string-parsing is needed** — the generated client deserializes wire strings into these enums for you. (There is a parallel server-side `Hexalith.Folders.Contracts.Projections.Audit.*` record set with `string` fields — ignore it; the UI never references Contracts.)
- **SDK methods to call** (interface `IClient`, `Hexalith.Folders.Client.Generated`; call the **no-`CancellationToken`** overloads, as all existing pages do):
  - `Task<AuditTrailPage> ListAuditTrailAsync(string folderId, string x_Correlation_Id, ReadConsistencyClass? x_Hexalith_Freshness, string cursor, int? limit, string filter)`
  - `Task<OperationTimelinePage> ListOperationTimelineAsync(string folderId, string x_Correlation_Id, ReadConsistencyClass? x_Hexalith_Freshness, string cursor, int? limit, string filter)`
  - (`GetAuditRecordAsync(folderId, auditRecordId, …)` / `GetOperationTimelineEntryAsync(folderId, timelineEntryId, …)` exist for optional detail routes — **not required by this story's ACs**; render per-record detail inline in the list rows. Add detail pages only if explicitly desired, keeping scope tight.)
- **Call discipline (mirror `Provider.razor.cs` / `ProviderSupport.razor.cs` exactly):**
  - One fresh correlation id per page load: `_correlationId = Guid.NewGuid().ToString()`, passed to every call and surfaced via `SafeCopyId`.
  - `ReadConsistencyClass.Eventually_consistent` on every read (eventually-consistent browsing — never the active snapshot probe).
  - `filter: null` on every call (C4 rejection-only — a non-null filter returns `400 validation_error`/`filter_not_yet_supported`). `limit: PageLimit` (const, 50; server clamps to max 100). The generated client omits null query params; never send a populated filter.
  - **`.ConfigureAwait(false)` on every await** (CA2007 is warnings-as-error in the UI project).
  - Override **`OnParametersSetAsync`** (not `OnInitializedAsync`) — route/query params drive the load and it re-runs on `Cursor` change.
  - Primary read in `try/catch`: `HexalithFoldersApiException` → `ConsoleErrorPresenter.FromException(ex, _correlationId)` → `<ConsoleErrorPanel>`; `HttpRequestException`/`TaskCanceledException` → `_unavailable = true` → `<ConsoleEmptyState Reason="EmptyStateReason.ReadModelUnavailable" />`. Supplementary reads via a private static `TryReadAsync<T>(Func<Task<T>>)` that swallows those exceptions → `null` so a denied side-panel degrades to honest Unknown instead of breaking the page.
- **Pagination** (cursor-based; see `ProviderSupport.razor` for the exact pattern): `[Parameter, SupplyParameterFromQuery] string? Cursor`; `NextPageHref()` returns `null` unless `_page?.Page is { IsTruncated: true }` with a non-blank `Cursor`, else builds `/folders/{Uri.EscapeDataString(FolderId)}/audit-trail?cursor={Uri.EscapeDataString(page.Cursor)}` with `string.Create(CultureInfo.InvariantCulture, …)`. Navigate by `<a href>` only — no button/form (keeps the no-mutation guard green).
- **Freshness honesty (UX-DR26)** — copy `ProviderSupport`'s discipline exactly:
  ```csharp
  private string FreshnessLabel()  => _page?.Freshness is not { } f ? "Unknown" : f.Stale ? "Stale" : "Current";
  private string ObservedAtLabel() => _page?.Freshness is { ObservedAt: var o } && o != default ? o.ToString("u", CultureInfo.InvariantCulture) : "unknown";
  ```
  Never present `default(DateTimeOffset)`/`0001-01-01` as a real time. Use the `ObservedAtOrNull(FreshnessMetadata?)` guard (Workspace.razor.cs) where applicable.
- **Redaction mapping (F-5) — reuse, do not reinvent:** resolve a `FieldDisclosure` then pass it to `<RedactedField>`:
  - `RedactableAuditActorReference` / `RedactableAuditOperationReference` / `RedactableDiagnosticIdentifier` (timeline `WorkspaceReference`): `RedactionDisclosureMapper.FromAuditRedaction(ref.Redaction, ref.Value)`.
  - `RedactableAuditTimestamp` (`AuditRecord.EvidenceTimestamp`): `RedactionDisclosureMapper.FromTimestampPrecision(ts.Precision)`; render `ts.Value.ToString("u", …)` only when `Visible`.
  - `ChangedPathEvidence` (`AuditRecord.ChangedPathEvidence`, nullable, typed `ChangedPathEvidence2`): `RedactionDisclosureMapper.FromDiagnosticClassification(ev.Classification, hasValue)`; surface `EvidenceKind` (`Digest`/`Reference`/`Redacted`/`Unavailable`) + the digest/reference — **never a raw path or content**. `forbidden` classification renders Redacted (not Missing). Null `ChangedPathEvidence` → render Missing/"Not recorded".
  - Plain `string` ids (timeline `OperationId`/`TaskId`/`CorrelationId`, audit `CorrelationId`): `SafeCopyId`. Audit `TaskId` is nullable → `RedactedField` Missing when null.
  - `RedactionDisclosureMapper` does **not** map `ProjectionAvailability` (its `redacted`/`unknown` are reference-pending C5) — do not bind to it.
- **Disposition primary (F-4):** pass the server-computed `entry.StateTransition.Disposition` straight to `<OperatorDispositionBadge>` (prefer server value, matching `WorkspaceTrustSummary`'s rule — do **not** re-run `DispositionLabelMapper.ResolveDisposition` over `ToState`). `<TechnicalStateMetadata>` renders the secondary muted lifecycle name.
- **Canonical categories:** `ConsoleStatusText.ResolveReasonCategoryLabel(CanonicalErrorCategory)` (`Success`→"None") and `ResolveErrorReasonToken(...)` produce operator labels for `ResultStatus`/`SanitizedErrorCategory`/`SanitizedResult`. `audit_access_denied` is already mapped in `ConsoleStatusText._errorExplanations`; extend `ConsoleStatusText` only if a genuinely new token surfaces.
- **`RetentionClass`** (`AuditTrailPage`/`OperationTimelinePage`) may be a `TODO(reference-pending):` marker (C3) — surface as-is, never present it as a frozen policy/duration.
- **Tenant authority** is server-side from authenticated claims + EventStore envelope; the UI sends no tenant id. `TenantScopeBanner` reads `IUserContextAccessor.TenantId` for display only — never the `{FolderId}` route.

### Architecture compliance

- **F-1/F-2/F-3:** Blazor Server (Interactive Server, set once globally in `App.razor` — **no per-page `@rendermode`**), reads only from projections via the `Hexalith.Folders.Client` SDK, Fluent UI via the FrontComposer/Shell pattern. No direct EventStore aggregate access; no `AddHexalithEventStore`.
- **F-4 (status visual):** operator-disposition primary, technical state secondary. **F-5 (redaction):** visible lock-icon affordance, never silent truncation. The architecture explicitly sanctions a custom **"Diagnostic Timeline"** component for this surface (UX-DR8, owned by 6.8).
- **Out of scope — do not pull neighboring stories in (review-failure flags):**
  - **F-7 perceived-wait UX (skeleton at 400 ms, "still loading…[cancel]" at 2 s) is Story 6.10** — render a simple labelled `data-testid="console-page-{name}-loading" aria-busy="true"` paragraph; do **not** build `SkeletonState`/`StillLoadingCancel`. (6.8 must not regress the F-7 p95 < 1.5 s budget, but does not implement the affordances.)
  - **Incident-mode `/_admin/incident-stream` (F-6) is Story 6.9.**
  - **WCAG/no-mutation release verification is Story 6.11.** Build accessibly (one `<h1>`, non-color-only status via the shipped components, keyboard-reachable links), but the formal audit is 6.11.
- **No-mutation / metadata-only is non-negotiable:** pass `ShouldHaveNoMutationAffordances()`, keep `IFrontComposerRegistry.GetManifests()` empty, render no file contents/diffs/paths/secrets/credential values/unauthorized-existence. The safety-invariant gate scans the `console-payloads` channel against `tests/fixtures/audit-leakage-corpus.json` sentinels — no sentinel value may reach the DOM. A pure read-render page consuming existing DTOs introduces no new safety channel (no `safety-channel-inventory.json` change needed).

### Library / framework requirements

- **Target:** `net10.0`, C# `LangVersion=latest`, nullable enabled, warnings-as-errors. Build/test only with the **Windows SDK** `/mnt/c/Program Files/dotnet/dotnet.exe` — the WSL-native SDK fails the `global.json` 10.0.300 pin.
- **Reuse these shipped components (do NOT reinvent), all `Hexalith.Folders.UI.Components` unless noted:**
  - `OperatorDispositionBadge` — `[EditorRequired] OperatorDispositionLabel Disposition`, `string? ColumnHeader`, `string? AdditionalCssClass`. `data-testid="operator-disposition-badge"`.
  - `TechnicalStateMetadata` — `[EditorRequired] LifecycleState State`, `string? ColumnHeader`, `bool IncludePrefix = true` (use `false` in dense cells). `data-fc-technical-state`.
  - `RedactedField` — `[EditorRequired] FieldDisclosure Disclosure`, `string? Value` (rendered only when `Visible`), `string? ColumnHeader`, `string? RedactedExplanation`, `string? UnknownText`, `string? MissingText`, `string? AdditionalCssClass`, `bool Monospace`. `data-testid="redacted-field"`, `data-fc-disclosure="visible|redacted|unknown|missing"`.
  - `SafeCopyId` — `[EditorRequired] string? Value`, `string? ColumnHeader`, `string? CodeTestId`. Read-only clipboard copy (does not trip the mutation guard). Wrapper `data-testid="safe-copy"`, button `safe-copy-button`; pass an explicit `CodeTestId` per identifier column so tests can target the `<code>` element.
  - `TenantScopeBanner` — `EffectivePermissions? Permissions`. `data-testid="tenant-scope-banner"`.
  - `ConsoleEmptyState` — `[EditorRequired] EmptyStateReason Reason` (`NoMatches`/`InsufficientFilterScope`/`ReadModelUnavailable`/`DeniedAccess`).
  - `ConsoleErrorPanel` — `[EditorRequired] ConsoleErrorView Error`.
  - `FcStatusBadge` (`Hexalith.FrontComposer.Shell.Components.Badges`) — `[EditorRequired] BadgeSlot Slot`, `[EditorRequired] string Label`, `string? ColumnHeader` — for any non-disposition status cell.
  - `FoldersConsoleIcons` (`Hexalith.Folders.UI.Components.Icons`) for any icon (the `Microsoft.FluentUI…Icons` package is deliberately off the reference graph — do **not** add it).
- **Reuse these services (`Hexalith.Folders.UI.Services`):** `RedactionDisclosureMapper`, `FieldDisclosure`, `ConsoleStatusText`, `ConsoleErrorPresenter`, `DispositionLabelMapper`, `EmptyStateReason`. All status mappers are total and throw `ArgumentOutOfRangeException` on unknown — preserve that pattern in any new mapper.
- **Forbidden touches:** `src/Hexalith.Folders.Client/Generated/**`, `src/Hexalith.Folders.Client/Compat/ChangedPathEvidenceShim.cs`, `Directory.Packages.props`, `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`. No new package or project references (UI may reference only `Hexalith.Folders.Client` + `Hexalith.FrontComposer.Shell`).

### File structure requirements

- Pages: `src/Hexalith.Folders.UI/Components/Pages/` (`AuditTrail.razor`(+`.cs`), `OperationTimeline.razor`(+`.cs`)) — code-behind `partial class : ComponentBase`, `namespace Hexalith.Folders.UI.Components.Pages`. Match the existing convention (the architecture file's literal `AuditTrail.razor` path is illustrative; pages live under `Components/Pages/`).
- Optional view-models: `src/Hexalith.Folders.UI/Components/Models/`.
- **No `.razor.css`** anywhere — styling uses shared `fc-*` classes from `Hexalith.FrontComposer.Shell`. Do not add scoped CSS.
- Explicit per-page `@using` (the `_Imports.razor` does NOT import `…Client.Generated` or `…UI.Services`): `System.Globalization`, `Hexalith.Folders.Client.Generated`, `Hexalith.Folders.UI.Components`, `Hexalith.Folders.UI.Components.Models`, `Hexalith.Folders.UI.Services`, `Hexalith.FrontComposer.Shell.Components.Badges`.
- Page-root + heading contract (load-bearing for tests): one outer `<div data-testid="console-page-{name}-root">`, exactly one `<h1>` (shell focuses it via `<FocusOnNavigate Selector="h1" />`; add no competing skip link), kebab-case `data-testid` prefixed `console-page-`.
- Tables: plain semantic `<table>` with `<caption>`/`<thead>`(`<th scope="col">`)/`<tbody>` + `@foreach`. **No `FluentDataGrid`** (none is used anywhere in the project). Copy `ProviderSupport.razor`'s table verbatim as the structural template.

### Testing requirements

- **bUnit lane — `tests/Hexalith.Folders.UI.Tests/`** (bUnit + xUnit v3 + NSubstitute + Shouldly). Use `DiagnosticTestContext.Create(tenantId = "tenant-a", userId = "user-a")` → `(BunitContext Ctx, IClient Client, IUserContextAccessor UserContext)`; stub the SDK with `client.ListAuditTrailAsync(Arg.Any<…>()…).Returns(page)` / `.ThrowsAsync(new HexalithFoldersApiException("denied", 403, body, EmptyHeaders, null))`. Render route params via `ctx.Render<TPage>(p => p.Add(c => c.FolderId, "folder-1"))`; set `[SupplyParameterFromQuery]` `Cursor` by `NavigationManager.NavigateTo("/folders/folder-1/audit-trail?cursor=…")` before render. Wrap async assertions in `rendered.WaitForAssertion(...)`. Add `#pragma warning disable xUnit1051`.
- **Assertion vocabulary to cover:** page-root testid + exactly one `<h1>` + `tenant-scope-banner` ordered before `<h1>` before the first evidence section; full field set rendered; `ShouldHaveNoMutationAffordances()` (`ConsoleTestAssertions.cs`); redaction distinctness (`[data-fc-disclosure="redacted|unknown|missing"]`, redacted value `Markup.ShouldNotContain(...)`); disposition-primary badge present; freshness honesty (stale→"Stale", absent→"Unknown", never "0001"); pagination (`…-next` href contains `cursor=` when truncated, else `…-end`); safe denial (`console-error-category` = canonical token, table absent); transport failure (`HttpRequestException`) → `[data-fc-empty-reason="read_model_unavailable"]` with root + single `<h1>` still rendered.
- **Totality `[Theory]`** over `Enum.GetValues<T>()` (force `en-US`) for any new enum switch; mapper throws on unknown (pattern in `RedactionDisclosureMapperTests.cs`/`ConsoleStatusTextTests.cs`).
- **E2E lane — `tests/Hexalith.Folders.UI.E2E.Tests/`** (Playwright, deferred): add `Smoke/AuditRouteSmokeTests.cs` mirroring `Smoke/ProviderRouteSmokeTests.cs` (`[Collection(PlaywrightCollection.Name)]`, `IClassFixture<AspireConsoleHostFixture>`); navigate `new Uri(_host.BaseAddress, ConsoleRoutes.AuditTrail("smoke-folder"))`, assert `response.Status.ShouldBeInRange(200, 399)`, page-root visible, `h1` count 1. The backend-less host degrades to read-model-unavailable — which is exactly why catching `HttpRequestException`/`TaskCanceledException` is mandatory.
- **Gates that must stay green:** `NavigationContractTests.Console_DoesNotRegisterAnyDomainCommandManifest`; `ScaffoldContractTests.ProjectReferencesFollowAllowedDependencyDirection` (two unrelated pre-existing baseline reds in IntegrationTests/LoadTests are not 6.8 regressions). `git diff --stat src/Hexalith.Folders.Client/Generated/` empty. Optional safety gate: `tests/tools/run-safety-invariant-gates.ps1`.

### Previous story intelligence (6.6 / 6.7 — apply these lessons)

- **File List fidelity is the recurring review finding** (6.6 and 6.7 both lost points for omitting modified test files). Record **every** created and modified file — source AND tests — in the Dev Agent Record File List, and the **accurate** test count (6.7 first mis-stated 349 vs the true 371).
- **Freshness honesty was a 6.7 review fix:** never fabricate `0001-01-01` observed-at or render "Current" for absent freshness — use the `FreshnessLabel()`/`ObservedAtLabel()`/`ObservedAtOrNull` discipline above.
- **Don't invent mappings unreachable on the passive read path; prefer the server value.** 6.7 was dinged for sourcing the wrong reference; here, pass `StateTransition.Disposition` through directly rather than re-deriving.
- **Transport resilience pattern** (6.7): distinguish `HexalithFoldersApiException` (safe-denial panel) from `HttpRequestException`/`TaskCanceledException` (read-model-unavailable empty state) — this is what lets the E2E smoke pass on the backend-less host.
- **Placeholder-resolution pattern** (6.7 resolved the provider pending span): resolve the `console-page-workspace-audit-trail-pending` span into a real link and add a `WorkspacePageTests` proof; keep the section `data-testid` stable so existing tests pass.
- **No scope creep:** 6.7 explicitly kept 6.8/6.9/6.10/6.11 concerns out. Do the same in reverse — no incident-mode (6.9), no skeleton/cancel perceived-wait (6.10), no formal WCAG audit (6.11), no filter UI (C4).

### Git intelligence summary

Recent epic-6 commits are strictly additive UI work on `src/Hexalith.Folders.UI/**` + `tests/Hexalith.Folders.UI.Tests/**` + `tests/Hexalith.Folders.UI.E2E.Tests/**`, plus `docs/ux/ops-console-wireflows.md` (authored by 6.5). 6.6 added `Workspace`/`FolderDetail`/`Folders` pages, the `TrustMatrix`/`WorkspaceTrustSummary`/`MetadataOnlyFolderTree` components, and the `ConsoleStatusText`/`ConsoleErrorPresenter`/`TrustDimension*` services. 6.7 added `Provider`/`ProviderSupport` pages + `ProviderStatusText` + `ProviderReadinessModel`, resolving the 6.6 provider placeholders. No commit in this range touched the generated client, `Directory.Packages.props`, or the OpenAPI spine — keep that streak. Story 6.8 follows the identical shape: new pages + tests, resolve the audit placeholder, zero backend/contract changes.

### Project structure notes

- Alignment: two new folder-scoped pages under `Components/Pages/` matching the `/folders/{FolderId}/…` route family (`/folders/{FolderId}/audit-trail`, `/folders/{FolderId}/operation-timeline`), consuming `IClient` directly (no facade/`IQueryService` exists in MVP — that is the as-built path, confirmed in 6.7 Dev Notes).
- Variance: the architecture file lists the page as `AuditTrail.razor` at a flat path and references a generated-vs-custom projection boundary; the as-built convention places custom pages under `Components/Pages/` and composes shipped custom components — follow the as-built convention. The operation-timeline page is the architecture-sanctioned custom "Diagnostic Timeline" (UX-DR8).
- Single-page-with-tabs is an acceptable alternative to two routes, but two folder-scoped routes are the recommended design (cleanest match to the two SDK resources and the existing route convention).

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 6.8] (L1410–1421) — story statement + AC; Epic 6 context L1305–1311.
- [Source: _bmad-output/planning-artifacts/architecture.md#Frontend Architecture (Read-Only Operations Console)] (L543–551) — decisions F-1…F-7; UI/projection access L143–149; custom-component allow-list incl. "Diagnostic Timeline" L167; audit projection D-10 L469; sensitive-metadata tier S-6/C9 L480/L187; metadata-only boundaries L90–91; console boundary concern #11 L110; must-distinguish states L166/L168; perf budget F-7 L551/L1497.
- [Source: docs/ux/ops-console-wireflows.md#§3.4 Audit view] — Diagnostic Timeline (UX-DR8) information hierarchy + ASCII sketch; §2 status taxonomy; §3.6 redaction; §3.7 loading; §3.8 empty (4 reasons); §3.9 safe error; §4 UX-DR console expectations; §6 five trust questions; §7 pending inputs (C2/C3/C4/C5 + French deferral). C4 filter rejection-only is §3.4 + §7.
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md] — UX-DR1…UX-DR32 (L107–140); Diagnostic Timeline anatomy (L558–568); responsive/accessibility strategy (L702–787); audit timeline in the screen-reader review list (L770).
- [Source: _bmad-output/implementation-artifacts/6-1-audit-and-operation-timeline-query-endpoints.md] — the implemented server contract; Dev Notes assign console rendering to Story 6.8.
- [Source: _bmad-output/implementation-artifacts/6-7-build-provider-readiness-and-support-diagnostic-pages.md] + `6-6-…md` — the page pattern, freshness-honesty fix, placeholder-resolution pattern, File-List-fidelity lesson, 372/372 baseline.
- SDK (read-only reference, **do not edit**): `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs` — `IClient` audit/timeline methods + `AuditTrailPage`/`AuditRecord`/`OperationTimelinePage`/`OperationTimelineEntry`/`DiagnosticStateTransition`/`PaginationMetadata`/`FreshnessMetadata`/`RedactableAudit*`/`ChangedPathEvidence2` and the `CanonicalErrorCategory`/`LifecycleState`/`OperatorDispositionLabel`/`RedactionMetadataVisibility`/`DiagnosticFieldClassification`/`RedactableAuditTimestampPrecision`/`ChangedPathEvidenceEvidenceKind`/`PaginationMetadataTruncatedReason`/`ReadConsistencyClass` enums.
- Reuse targets: `src/Hexalith.Folders.UI/Components/{OperatorDispositionBadge,TechnicalStateMetadata,RedactedField,SafeCopyId,TenantScopeBanner,ConsoleEmptyState,ConsoleErrorPanel}.razor`; `src/Hexalith.Folders.UI/Services/{RedactionDisclosureMapper,FieldDisclosure,ConsoleStatusText,ConsoleErrorPresenter,DispositionLabelMapper,EmptyStateReason}.cs`. Seam to resolve: `Components/Pages/Workspace.razor` L63–67 + `Workspace.razor.cs` L222–228.
- Test infra: `tests/Hexalith.Folders.UI.Tests/{DiagnosticTestContext,BadgeRenderingFixture,ConsoleTestAssertions,NavigationContractTests}.cs`; `tests/Hexalith.Folders.UI.E2E.Tests/{Routes/ConsoleRoutes.cs,Smoke/ProviderRouteSmokeTests.cs}`. Safety fixtures: `tests/fixtures/{audit-leakage-corpus.json,safety-channel-inventory.json}`; gate `tests/tools/run-safety-invariant-gates.ps1`.
- [Source: _bmad-output/project-context.md] — repository-wide rules (metadata-only everywhere, tenant-scope authority, read-only console, central package management, root-level-only submodules, build via `Hexalith.Folders.slnx`).

## Dev Agent Record

### Agent Model Used

Claude Opus 4.8 (claude-opus-4-8, 1M context)

### Debug Log References

- Build/test executed with the Windows SDK `/mnt/c/Program Files/dotnet/dotnet.exe` (the WSL-native SDK fails the `global.json` 10.0.300 pin), per the repo policy.
- Baseline before implementation: `dotnet test tests/Hexalith.Folders.UI.Tests` → **372/372** passed (matches post-6.7 baseline).
- Final: `dotnet test tests/Hexalith.Folders.UI.Tests` → **420/420** passed, 0 skipped (+48 new tests over the **372** post-6.7 baseline), clean build under warnings-as-errors. (Earlier interim runs mis-stated the lane as 399/399; the accurate total is 420, and a follow-up corrected four `AuditTrail`/`OperationTimeline` `Received(...)` assertions that mixed a bare `null` filter literal with `Arg` matchers — NSubstitute `AmbiguousArgumentsException` — by switching the filter assertion to `Arg.Is<string>(f => f == null)`; no production code changed.)
- `dotnet build tests/Hexalith.Folders.UI.E2E.Tests` → Build succeeded, **0 Warning(s), 0 Error(s)** (Playwright lane is deferred; only compilation is required).
- `NavigationContractTests.Console_DoesNotRegisterAnyDomainCommandManifest` → green (within the 420 UI lane; console registers no command manifest).
- `git diff --stat` is **empty** for `src/Hexalith.Folders.Client/Generated/`, `src/Hexalith.Folders.Client/Compat/ChangedPathEvidenceShim.cs`, `Directory.Packages.props`, and `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`. `git diff --stat -- '*.csproj' '*.slnx'` is **empty** → no new `<PackageReference>`/`<ProjectReference>`, no solution-membership change.

### Completion Notes List

- **Two new read-only Blazor pages** mirror the shipped `Provider`/`ProviderSupport` pattern exactly (`OnParametersSetAsync`, one fresh correlation id per load, `ReadConsistencyClass.Eventually_consistent`, `.ConfigureAwait(false)`, `filter: null` on every C4 call, `try/catch` splitting `HexalithFoldersApiException` → safe-denial panel from `HttpRequestException`/`TaskCanceledException` → read-model-unavailable, supplementary `TryReadAsync<T>` for the scope-banner permissions read).
- **AuditTrail** binds `AuditRecord` (newest-first, paginated). **OperationTimeline** is the architecture-sanctioned custom "Diagnostic Timeline" (UX-DR8) binding `OperationTimelineEntry`, with `OperatorDispositionBadge` as the PRIMARY visual (server-computed `StateTransition.Disposition` passed through verbatim — never re-derived) and `TechnicalStateMetadata` (`IncludePrefix=false`) as muted secondary from→to.
- **Redaction (F-5 / AC #8):** every redactable field routes through `RedactedField` fed by `RedactionDisclosureMapper`. Per the SDK type shapes, `AuditRecord.EvidenceTimestamp` is a `RedactableAuditTimestamp` (mapped via `FromTimestampPrecision`) while `OperationTimelineEntry.EvidenceTimestamp` is a plain `DateTimeOffset` (formatted directly); `AuditRecord.OperationId` is a `RedactableAuditOperationReference` while `OperationTimelineEntry.OperationId` is a plain string. Actor/operation/workspace references map via `FromAuditRedaction(ref.Redaction, ref.Value)`; changed-path evidence via `FromDiagnosticClassification`. View-models **never carry a value once it is non-Visible** (defense-in-depth — a redacted value can't reach the DOM); tests assert the redacted value is absent from `Markup`.
- **Freshness honesty (UX-DR26 / AC #10):** `FreshnessLabel()` returns `Stale`/`Current`/`Unknown` and was strengthened beyond the literal ProviderSupport copy to also treat a **default/min `ObservedAt` as "Unknown"** (never "Current"), and the audit evidence timestamp degrades a default value to Unknown — so no fabricated `0001-01-01` is ever rendered.
- **New total enum mapper:** added `ConsoleStatusText.ResolveChangedPathEvidenceKindLabel(ChangedPathEvidenceEvidenceKind)` (total switch, throws on unknown) with a totality `[Theory]` + throw-on-undefined `[Fact]`, following the established `ConsoleStatusText` drift-sentinel pattern. `ChangedPathEvidence2` is the hand-written compat shim deriving from `ChangedPathEvidence` (left untouched).
- **View-models (Task 3):** `AuditRecordView` / `OperationTimelineEntryView` are `public sealed record`s with a static `Create(...)` factory mirroring `ProviderReadinessModel`'s assembler shape (the only concrete sibling in `Components/Models/`, which is public) and using `NullIfBlank`/default-timestamp normalization. Chose `public` to match every existing file in `Components/Models/` (the story's "internal" suggestion conflicted with "mirror ProviderReadinessModel which is public"; matching the real sibling is the consistent call). They are UI-assembly only — not added to `Contracts`, not registered as a service.
- **Tenant scope (AC #7):** rendered via `<TenantScopeBanner Permissions="@_permissions" />`, which sources tenant identity from `IUserContextAccessor.TenantId` internally — never from `{FolderId}`/cursor/client input. The pages do not separately inject `IUserContextAccessor` (matching the `Provider` analog, which delegates tenant scope to the banner and does not duplicate the injection — avoids an unused dependency).
- **Placeholder resolved + connected evidence (AC #15/#16):** `Workspace.razor`'s `console-page-workspace-audit-trail-pending` span is replaced with real folder-scoped audit-trail + operation-timeline `<a href>` links (section `data-testid` kept stable); the Trust Matrix "Audit traceability" cell `EvidenceHref` now points at `/folders/{FolderId}/audit-trail`; `FolderDetail.razor` gains audit/timeline cross-links; the two pages cross-link each other.
- **Out of scope kept out:** no incident-mode (6.9), no skeleton/“still loading…[cancel]” perceived-wait affordance (6.10 — a simple `aria-busy` loading paragraph is used), no formal WCAG audit (6.11), and no filter UI (C4 rejection-only — `filter: null` always).
- **Pre-existing baseline reds (NOT 6.8 regressions):** `ScaffoldContractTests.SolutionContainsOnlyCanonicalBuildableProjects` and `ScaffoldContractTests.ProjectReferencesFollowAllowedDependencyDirection` fail in `tests/Hexalith.Folders.Testing.Tests`, but the failures are about `Hexalith.Folders.IntegrationTests` reference drift (extra `Cli`/`Client`/`Mcp` refs) and the `Hexalith.Folders.LoadTests` projects' solution membership — projects this story never touched. Confirmed pre-existing: `git diff --stat -- '*.csproj' '*.slnx'` is empty, so the tests' inputs are byte-identical to baseline `f7774d1`. The story explicitly anticipates these as "two unrelated pre-existing baseline reds in IntegrationTests/LoadTests are not 6.8 regressions."

### File List

**Source — created**

- `src/Hexalith.Folders.UI/Components/Pages/AuditTrail.razor`
- `src/Hexalith.Folders.UI/Components/Pages/AuditTrail.razor.cs`
- `src/Hexalith.Folders.UI/Components/Pages/OperationTimeline.razor`
- `src/Hexalith.Folders.UI/Components/Pages/OperationTimeline.razor.cs`
- `src/Hexalith.Folders.UI/Components/Models/AuditRecordView.cs`
- `src/Hexalith.Folders.UI/Components/Models/OperationTimelineEntryView.cs`

**Source — modified**

- `src/Hexalith.Folders.UI/Services/ConsoleStatusText.cs` (added `ResolveChangedPathEvidenceKindLabel`)
- `src/Hexalith.Folders.UI/Components/Pages/Workspace.razor` (resolved `console-page-workspace-audit-trail-pending` placeholder into audit-trail + operation-timeline links)
- `src/Hexalith.Folders.UI/Components/Pages/Workspace.razor.cs` (Trust Matrix "Audit traceability" cell `EvidenceHref` → audit-trail route; updated connected-evidence comment)
- `src/Hexalith.Folders.UI/Components/Pages/FolderDetail.razor` (added audit-trail + operation-timeline cross-links)

**Tests — created**

- `tests/Hexalith.Folders.UI.Tests/AuditTrailPageTests.cs`
- `tests/Hexalith.Folders.UI.Tests/OperationTimelinePageTests.cs`
- `tests/Hexalith.Folders.UI.E2E.Tests/Smoke/AuditRouteSmokeTests.cs`

**Tests — modified**

- `tests/Hexalith.Folders.UI.Tests/ConsoleStatusTextTests.cs` (added `ChangedPathEvidenceKind` totality `[Theory]` + throw-on-undefined `[Fact]`)
- `tests/Hexalith.Folders.UI.Tests/WorkspacePageTests.cs` (added audit-link-resolved test)
- `tests/Hexalith.Folders.UI.Tests/FolderDetailPageTests.cs` (added `FolderDetail_RendersAuditTrailAndOperationTimelineLinks_WithFolderScopedHrefs` — proves the AC #16 / UX-DR19 FolderDetail → audit-trail + operation-timeline cross-links; **added during AI review — was omitted from the original File List**)
- `tests/Hexalith.Folders.UI.E2E.Tests/Routes/ConsoleRoutes.cs` (added `AuditTrail(folderId)` + `OperationTimeline(folderId)` route builders)

**Story tracking — modified**

- `_bmad-output/implementation-artifacts/6-8-build-audit-and-operation-timeline-diagnostic-pages.md` (this file: `baseline_commit`, Status, checkboxes, Dev Agent Record)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (`6-8-…` status, `last_updated`)

## Senior Developer Review (AI)

**Reviewer:** Jerome · **Date:** 2026-05-29 · **Outcome:** ✅ Approve (auto-fix applied)

**Method:** Adversarial review fanned out across 5 dimensions (ACs 1–6, ACs 7–12 safety, ACs 13–17, code quality, test quality + File-List fidelity); every candidate finding independently re-verified by a skeptic against the actual code. Result: **0 Critical, 0 High, 4 Medium, 4 Low** confirmed; 3 candidate findings refuted.

**ACs 1–17:** all satisfied. Verified against the SDK type shapes (`AuditRecord`/`OperationTimelineEntry`/`RedactableAudit*`/`ChangedPathEvidence2` and the enums) — redactable fields route through `RedactedField`, redacted values are nulled in the view-models before render (never reach the DOM), disposition is the primary timeline visual (server value passed through), freshness is honest (no fabricated `0001-01-01`), safe denial / read-model-unavailable / empty states all render root + single `<h1>`, no mutation affordance, no filter UI, no fabricated provider column, the 6.6 audit placeholder is resolved into connected-evidence links. Forbidden-touch gate clean: zero edits to `src/Hexalith.Folders.Client/Generated/`, `Directory.Packages.props`, the OpenAPI spine, or any `*.csproj`/`*.slnx`.

**Findings auto-fixed (no prompting, per the review invocation):**

1. **[Medium · File-List fidelity]** `tests/Hexalith.Folders.UI.Tests/FolderDetailPageTests.cs` was modified for AC #16 (the FolderDetail cross-link test) but omitted from the File List — the exact recurring 6.6/6.7 lapse the Dev Notes flag. **Fixed:** added to "Tests — modified" above.
2. **[Low · AC #8 affordance consistency]** `AuditRecordView.ResolveChangedPath` drove the changed-path disclosure off `Classification` only, so a `redacted`/`unavailable` `EvidenceKind` paired with a non-`forbidden` classification (digest/reference absent by SDK schema) would render the kind label "Redacted"/"Unavailable" next to a contradictory "Not recorded" Missing affordance (no value leaked). **Fixed:** `EvidenceKind` is now authoritative — `Redacted → Redacted` (lock), `Unavailable → Unknown`, value-bearing kinds keep the classification-gated disclosure. Added `RedactedOrUnavailableChangedPathKind_WithNonForbiddenClassification_RendersConsistentAffordance`.
3. **[Low · test gap]** No positive assertion existed for the audit-trail *visible* timestamp value. **Fixed:** added `data-testid="console-page-audit-trail-timestamp"` to the cell and `VisibleTimestamp_RendersFormattedUtcValue`.
4. **[Low · tracking hygiene]** `sprint-status.yaml` `last_updated` had regressed ~9h backward. **Fixed:** set forward/monotonic during the review sync.

**Refuted (no action):** duplicate `@using Hexalith.Folders.UI.Components` (codebase convention + spec-sanctioned per Dev Notes L144); "Trust Matrix audit-href untested" (it *is* asserted at `TrustMatrixTests.cs:37`); totality `[Theory]` missing `en-US` (labels are ASCII constants and AC #17 has no culture clause — cosmetic prose-vs-test mismatch, matches every sibling theory in the file).

**Gates after fixes:** `dotnet test tests/Hexalith.Folders.UI.Tests` → **422/422** passed, 0 skipped (+2 review tests over the 420 dev baseline), clean under warnings-as-errors. `dotnet build tests/Hexalith.Folders.UI.E2E.Tests` → **0 Warning(s), 0 Error(s)**. (Two unrelated pre-existing IntegrationTests/LoadTests `ScaffoldContractTests` reds remain — not 6.8 regressions; the story's inputs are byte-identical to baseline `f7774d1`.)

## Change Log

| Date | Change |
| --- | --- |
| 2026-05-29 | Implemented Story 6.8: built the folder-scoped AuditTrail and OperationTimeline (Diagnostic Timeline) read-only diagnostic pages + UI-assembly view-models, added the `ChangedPathEvidenceKind` label mapper, resolved the Workspace 6.6 audit placeholder into connected-evidence links, and added bUnit page tests + E2E route smoke tests. UI lane 372→420 green; zero edits to the generated client, package management, or the OpenAPI spine. |
| 2026-05-29 | Follow-up: fixed four failing bUnit tests (`AuditTrailPageTests`/`OperationTimelinePageTests` cursor + null-filter `Received(...)` assertions) that threw NSubstitute `AmbiguousArgumentsException` by mixing a bare `null` filter literal with `Arg` matchers — switched the filter assertion to `Arg.Is<string>(f => f == null)`. Test-only change; no production code touched. UI lane 420/420 green. |
| 2026-05-29 | Senior Developer Review (AI): adversarial review (0 Critical/0 High/4 Medium/4 Low). Auto-fixed: File List omission of `FolderDetailPageTests.cs`; changed-path `EvidenceKind`-vs-affordance consistency in `AuditRecordView` (+ test); audit visible-timestamp test gap (+ `console-page-audit-trail-timestamp` testid); sprint-status `last_updated` regression. UI lane 420→**422/422** green, E2E builds clean, forbidden-touch gate clean. Status → done. |
