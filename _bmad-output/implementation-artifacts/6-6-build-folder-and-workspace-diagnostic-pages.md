---
baseline_commit: d0981f7
---

# Story 6.6: Build folder and workspace diagnostic pages

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an operator,
I want folder and workspace diagnostic pages,
so that lifecycle, readiness, lock, dirty state, commit state, failure state, and cleanup status are inspectable.

## Acceptance Criteria

Source epic AC (epics.md#Story-6.6, lines 1384-1395):

> **Given** projection endpoints, reusable status components, and console wireflow notes exist
> **When** folder and workspace diagnostic pages render
> **Then** pages show authorized lifecycle, readiness, lock, dirty, commit, failure, cleanup, freshness, and correlation metadata
> **And** no file editing, file browsing, raw diff, credential reveal, repair action, or mutation control is present.

**This is the first production diagnostic-page implementation story in Epic 6.** Stories 6.1–6.4 shipped the projection endpoints and the reusable atomic components (`OperatorDispositionBadge`, `TechnicalStateMetadata`, `RedactedField`, the mappers); Story 6.5 authored the reviewed wireflow contract `docs/ux/ops-console-wireflows.md` that **gates** 6.6 (epics.md:1382). Story 6.6 is the first page that injects and calls the SDK `IClient`. It builds the **Folder view (§3.1)** and **Workspace view (§3.2)** of the wireflow, composing the shipped components and adding four new domain-evidence components: `TenantScopeBanner`, `WorkspaceTrustSummary`, `MetadataOnlyFolderTree`, `TrustMatrix`.

Decomposed, testable acceptance criteria. Each names what a bUnit/E2E test or a code/structure check must confirm. (UI-DR IDs, C6 state names, disposition labels, and `FieldDisclosure` members are **existing contracts — reference and preserve them verbatim; never redefine, renumber, or fork**, per the wireflow `mutation_rules`.)

1. **Three read-only, route-addressable pages exist under the shipped console shell.** New Razor pages under `src/Hexalith.Folders.UI/Components/Pages/`, each: a single `<h1>`; a page-root container `data-testid="console-page-{name}-root"`; a `<PageTitle>`; rendered through `FrontComposerShell` (global Interactive Server render mode, no per-page `@rendermode`); relying on the shell's `<FocusOnNavigate Selector="h1" />` (no competing skip link).
   - `Folders.razor` → `@page "/folders"`, root `console-page-folders-root` — folder list/orientation for the authenticated tenant (the discovery entry; Journey 1 step B→C→E).
   - `FolderDetail.razor` → `@page "/folders/{FolderId}"`, root `console-page-folder-detail-root` — **Folder view (§3.1)**.
   - `Workspace.razor` → `@page "/folders/{FolderId}/workspaces/{WorkspaceId}"`, root `console-page-workspace-root` — **Workspace view (§3.2)**.
   - Route constants added to `tests/Hexalith.Folders.UI.E2E.Tests/Routes/ConsoleRoutes.cs` (the single source of route strings; its header notes "Stories 6.6-6.8 add the diagnostic routes"). The `Tenants.razor` placeholder copy "Tenant pickers ship in Story 6.6" is resolved (tenant/folder navigation now reaches the folder list).

2. **`TenantScopeBanner` renders authoritative tenant scope (UX-DR6) and is present on both detail pages.** New component `Components/TenantScopeBanner.razor`. Renders: safe tenant identifier, effective access state, principal/delegated-actor summary, policy scope, **last authorization check**. The tenant value comes from the **authenticated context** via the shipped `FoldersUserContextAccessor.TenantId` (the `tenant_id` claim it reads from the circuit `AuthenticationStateProvider`) — **never** from the `{FolderId}` route value or any payload (concern #12). (Note: the `eventstore:tenant` claim is the *server-side* claim-transform name; the UI bridge reads `tenant_id` — do not chase `eventstore:tenant` in UI code.) Effective-access state derives from `GetEffectivePermissionsAsync` → `EffectivePermissions.AuthorizationOutcome` / `.Permissions`. States: allowed, denied, partial, redacted, unknown, stale (UX-DR6 anatomy). **Denied states must not expose unauthorized resource existence.** Banner is keyboard-reachable and screen-reader meaningful.

3. **`WorkspaceTrustSummary` renders the full UX-DR5 field set with disposition-primary visual (F-4).** New component `Components/WorkspaceTrustSummary.razor` placed at the top of `Workspace.razor` (summary-before-tables, UX-DR18). Fields (UX-DR5, verbatim set): tenant, folder, workspace ID, repository binding, provider, task ID, correlation ID, current state, authorization posture, lock state, dirty state, commit reference, latest reason category, freshness timestamp. The **operator-disposition badge is the primary visual**; the technical state name is **secondary** muted metadata — composed from the shipped `OperatorDispositionBadge` + `TechnicalStateMetadata`, never bespoke strings or a promoted technical-state headline. Renders every one of the **11 C6 states** and **5 dispositions** (the matrix is total; no happy-path subset).

4. **`MetadataOnlyFolderTree` renders permitted path metadata as evidence, never a file manager (UX-DR7, UX-DR12).** New component `Components/MetadataOnlyFolderTree.razor`. Columns per §3.1 / UX-DR7: `path | type | size-class | last op | changed | access | redaction`. Per-entry states: permitted, changed, clean, inaccessible, redacted, unknown, binary, excluded-by-policy. **No file open, no file content, no raw diff, no download.** Redaction renders through the shipped `RedactedField` (lock icon + text), distinct from unknown/missing. Tree/table semantics support **keyboard expand/collapse** with clear labels for redacted/inaccessible entries (UX-DR30); if a table is used instead of a tree it preserves hierarchy + accessibility labels (ux-spec:783).
   - **The tree is workspace-scoped** (matches §3.1's identity line "tenant · folder · **workspace** · repo binding" and "if workspace-bound"). Its data source `ListFolderFilesAsync` (+ `GetFolderFileMetadataAsync`) requires `(folderId, workspaceId, x_Correlation_Id, x_Hexalith_Task_Id, …)` — both a `workspaceId` and a task id. **Primary placement is the Workspace view's "Folder metadata" section** (`Workspace.razor` has `{WorkspaceId}` in-route and a task id from `WorkspaceStatus.AcceptedCommandState.TaskId`). On the Folder view (`FolderDetail.razor`, which has only `{FolderId}`) the tree renders **only when a workspace context is resolved**; absent a bound workspace or task id it shows the folder's workspace list + an empty "no workspace-bound content" state — it must **not** fabricate a workspaceId/taskId.

5. **`TrustMatrix` compares the six trust dimensions as grouped evidence (UX-DR9).** New component `Components/TrustMatrix.razor`, rendered (optionally) on `Workspace.razor`. Dimensions: tenant boundary, provider readiness, workspace lifecycle, lock state, folder metadata visibility, audit traceability. Each cell: dimension name, state label, **icon**, reason summary, last-updated time, and a link to supporting evidence (the connected-evidence requirement, UX-DR19). States: ready, warning, failed, inaccessible, unknown, delayed, redacted. Matrix cells are keyboard-reachable and understandable as grouped evidence, not just visual tiles (UX-DR9 accessibility).

6. **Data comes from the SDK `IClient` directly, binding the ops-console diagnostics DTOs as the primary operator-rendering source.** Pages inject `Hexalith.Folders.Client.Generated.IClient` (registered by the shipped `AddFoldersClient(...)` + `BearerTokenDelegatingHandler`). **No `IQueryService` / `FoldersClientFacade` is built** (none exists; the wireflow §1.4 documents the as-built direct-SDK path — see the §"Data path" Dev Note). The **ops-console diagnostics** endpoints are the primary source because they return operator-ready `DiagnosticBase` shapes (server-computed `Disposition`, `FieldClassifications`, `Trust`, `Freshness`); the **plain status DTOs** supply identity/lifecycle/lock-lease/cleanup/permissions detail the diagnostics shapes lack. The exact method→field mapping is in the §"Data path" Dev Note and must be followed (do **not** bind the server-side `*QueryResult` records — they are not on the SDK).

7. **Disposition and freshness are derived by the canonical rules, never hardcoded.** Where a diagnostics DTO is fetched, its server-computed `DiagnosticBase.Disposition` (an `OperatorDispositionLabel`) feeds `OperatorDispositionBadge.Disposition` directly. Where the page derives disposition from a lifecycle state, it calls `DispositionLabelMapper.ResolveDisposition(state, hasProjectionLagEvidence)` and passes `hasProjectionLagEvidence: <dto>.Freshness.Stale` (the boolean staleness signal) — **do not compute or hardcode a numeric C2 lag threshold** (C2 is TBD/reference-pending). A freshness zone shows `FreshnessMetadata` (`ObservedAt`/`ProjectionWatermark`/`Stale`); stale/unavailable reason text comes only from `DiagnosticTrustEvidence.StaleReasonCode`/`UnavailableReasonCode` when a diagnostics DTO is present (`FreshnessMetadata` has no `ReasonCode`). Stale evidence is never presented as current without a freshness label (UX-DR26).

8. **The shared visible status taxonomy and its four deliberate distinctions are preserved (wireflow §2).** The pages reconcile the four vocabularies (disposition / technical-state / field-disclosure / access) and never invent UI-only state names. The four distinctions hold visibly + semantically: `stale`/`delayed` ≠ `unavailable`; `redacted` ≠ `unknown` ≠ `missing` (redaction via `RedactedField` only; representing redaction as missing/silent-truncation is a correctness bug); `denied` ≠ `inaccessible`; disposition (primary) ≠ technical state (secondary). `unknown_provider_outcome` renders the `awaiting-human` disposition badge (Warning) — never a neutral "Unknown" with no badge.

9. **Every status indicator is non-color-only and every identifier is monospace safe-copy (UX-DR14, UX-DR27).** Disposition, redaction, freshness, access, and matrix-cell indicators each carry text + icon/shape + semantic color + accessible label (inherited from the shipped components, applied to new ones). Safe identifiers (tenant, folder, workspace, task ID, operation ID, correlation ID, commit reference, repository-binding ID, provider-binding ref) render in monospace with **safe-copy affordances only** — never a raw editable field; no credential/token values.

10. **Empty, error, and safe-denial states are rendered without leakage (§3.8/§3.9).** Empty distinguishes four reasons (no matches / insufficient filter scope / unavailable read model / denied access) without confirming unauthorized existence (UX-DR20/21). Error maps the canonical categories (`authentication_failure`, `tenant_access_denied`, `folder_acl_denied`, `audit_access_denied`, `read_model_unavailable`, `projection_stale`, `projection_unavailable`, `response_limit_exceeded`, `query_timeout`, `not_found`, `redacted`, `internal_error`) to reason category → safe explanation → correlation-ID evidence (monospace safe-copy) → escalation posture. A thrown `HexalithFoldersApiException` (the SDK surfaces denials as HTTP status + Problem Details, **not** as data fields) is caught and rendered through the safe-denial path. `not_found` and `*_denied` are **never** expanded into a resource-existence oracle. Do **not** read a `taskId` off a Problem Details body (it is not a canonical A-8 extension).

11. **The pages are strictly read-only and the command-suppression guard stays green.** No `<form>`, `<FluentInputForm>`, `<FluentDialog>` mutation form, `data-fc-command`, or `data-fc-mutation` on any 6.6 page (the 5-selector guard mirrored across existing page tests). No mutating SDK method is called (no method returning `AcceptedCommand`/`CommitWorkspaceAccepted` / taking an `idempotency_Key`). No `[Command]` projection is defined and `AddHexalithEventStore` is **not** called, so `NavigationContractTests.Console_DoesNotRegisterAnyDomainCommandManifest` stays green. Forms are limited to search/filter/sort/view-preference within the server-accepted filter vocabulary (UX-DR23; see C4 pending input). No credential reveal, file content, raw diff, repair/retry/release/discard action, or unrestricted filesystem browse.

12. **bUnit + E2E coverage proves rendering and the read-only/totality invariants; the SDK generated tree is untouched.** New bUnit tests in `tests/Hexalith.Folders.UI.Tests` cover each new component and page (using `BadgeRenderingFixture`, with the `IClient` substituted via NSubstitute and `IUserContextAccessor` provided per the `ShellCompositionTests` pattern), including a `Renders_NoMutationAffordances` fact per page. Any new switch over an SDK enum (`LifecycleState`, `OperatorDispositionLabel`, `ProjectionAvailability` consumers, `LockState`, `CleanupStatus`, `CanonicalErrorCategory`) is a **total** `[Theory]` over `Enum.GetValues<T>()` that throws on unknown — never a silent default. A new E2E smoke test for the folder route follows `ConsoleSmokeTests`. `git diff --stat src/Hexalith.Folders.Client/Generated/` is empty (no generated-client edits) and `Directory.Packages.props`/the OpenAPI spine are untouched.

## Tasks / Subtasks

- [x] **Task 1 — Read the reviewed contract and confirm prerequisites** (AC: all)
  - [x] Read `docs/ux/ops-console-wireflows.md` §1 (hosting model, esp §1.4 as-built data path), §2 (taxonomy + distinctions), §3.1 Folder view, §3.2 Workspace view, §3.6–§3.9 (redaction/loading/empty/error), §4.1 (accessibility), §6 Journeys 1 & 2, §7 (pending inputs). This is the gate contract — preserve its IDs/names verbatim.
  - [x] Read the shipped components/services you will compose: `OperatorDispositionBadge`, `TechnicalStateMetadata`, `RedactedField`, `DispositionLabelMapper`, `FieldDisclosure`, `RedactionDisclosureMapper`, `FoldersConsoleIcons`, `FoldersUserContextAccessor`, `CompositionRoot.cs` (data-path lines 150-158).
  - [x] Confirm the SDK read methods + DTO fields in the §"Data path" Dev Note against `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs` (do not trust memory — the DTO field names are exact).

- [x] **Task 2 — Build the `TenantScopeBanner` component** (AC: #2, #8, #9)
  - [x] `Components/TenantScopeBanner.razor` (+ `.razor.cs` if logic warrants). Render safe tenant id (from `FoldersUserContextAccessor`, not the route), effective access (`EffectivePermissions.AuthorizationOutcome`/`.Permissions`), principal/delegated actor, policy scope, last-auth-check. States allowed/denied/partial/redacted/unknown/stale; denied never confirms existence. Non-color-only + keyboard-reachable + accessible label. testid `data-testid="tenant-scope-banner"`.
  - [x] bUnit test incl. `Renders_NoMutationAffordances`; assert tenant comes from context not route.

- [x] **Task 3 — Build the `WorkspaceTrustSummary` component** (AC: #3, #7, #8, #9)
  - [x] `Components/WorkspaceTrustSummary.razor`. Compose `OperatorDispositionBadge` (primary) + `TechnicalStateMetadata` (secondary) + `RedactedField` (for tenant-sensitive fields: repo binding, provider, commit reference, reason category). Render the full UX-DR5 field set; monospace safe-copy identifiers (UX-DR27). Disposition per AC #7 rules. Freshness zone with `Stale` + (diagnostics) reason code. testid `data-testid="workspace-trust-summary"`.
  - [x] bUnit: a **total** `[Theory]` over `Enum.GetValues<LifecycleState>()` asserting each state renders a disposition badge (incl. the `Ready`+lag → `Degraded_but_serving` branch) and that `unknown_provider_outcome` shows the `awaiting-human` badge (never a neutral unknown). `Renders_NoMutationAffordances`.

- [x] **Task 4 — Build the `MetadataOnlyFolderTree` component** (AC: #4, #8, #9)
  - [x] `Components/MetadataOnlyFolderTree.razor`. Component takes `folderId`, `workspaceId`, and `taskId` parameters (all three are required by `ListFolderFilesAsync`/`GetFolderFileMetadataAsync`). Columns `path | type | size-class | last op | changed | access | redaction`. Per-entry states permitted/changed/clean/inaccessible/redacted/unknown/binary/excluded-by-policy. Resolve each cell's `FieldDisclosure` via `RedactionDisclosureMapper.FromFileMetadataRedaction(...)` and render through `RedactedField`. Keyboard expand/collapse; no file open/content/diff/download. testid `data-testid="metadata-only-folder-tree"`. When the host page cannot supply a workspaceId+taskId, the component renders an empty "no workspace-bound content" state — never fabricates ids.
  - [x] bUnit: assert redacted vs inaccessible vs unknown vs excluded entries are visibly/semantically distinct; no content/diff/download control; keyboard expand/collapse works.

- [x] **Task 5 — Build the `TrustMatrix` component** (AC: #5, #8, #9)
  - [x] `Components/TrustMatrix.razor`. Six dimensions × {label, icon, reason, last-updated, evidence link}. States ready/warning/failed/inaccessible/unknown/delayed/redacted. Cells keyboard-reachable + grouped-evidence semantics. testid `data-testid="trust-matrix"`.
  - [x] bUnit incl. `Renders_NoMutationAffordances`.

- [x] **Task 6 — Build the `Folders.razor` list page** (AC: #1, #2, #10, #11)
  - [x] `Components/Pages/Folders.razor` `@page "/folders"`. `TenantScopeBanner` first (scope-before-evidence), then an authorized folder list for the context tenant with disposition-primary status per row. Each row links to `/folders/{folderId}`. Empty/error/denied states per §3.8/§3.9. Add `ConsoleRoutes.Folders` constant; update `Tenants.razor` to link here (resolve the "ship in 6.6" placeholder copy). testid `console-page-folders-root`; single `<h1>`; `<PageTitle>`.
  - [x] bUnit + E2E smoke (new `ConsoleRoutes` constant, follow `ConsoleSmokeTests`).

- [x] **Task 7 — Build the `FolderDetail.razor` page (Folder view §3.1)** (AC: #1, #2, #4, #10, #11)
  - [x] `Components/Pages/FolderDetail.razor` `@page "/folders/{FolderId}"`. Order: `TenantScopeBanner` → folder identity block (tenant · folder · workspace · repo binding from `GetFolderLifecycleStatusAsync`) → optional trust summary if workspace-bound → the folder's workspace list (each row links to `/folders/{folderId}/workspaces/{workspaceId}`). Render `MetadataOnlyFolderTree` **only when a workspace context is resolved** (workspaceId + a task id available); otherwise show the workspace-list + empty "no workspace-bound content" state — do not fabricate ids. testid `console-page-folder-detail-root`. Empty/error/denied per §3.8/§3.9.
  - [x] bUnit: identity + tree compose correctly; `Renders_NoMutationAffordances`; denied path renders safe-denial without existence leak.

- [x] **Task 8 — Build the `Workspace.razor` page (Workspace view §3.2)** (AC: #1, #2, #3, #5, #7, #10, #11)
  - [x] `Components/Pages/Workspace.razor` `@page "/folders/{FolderId}/workspaces/{WorkspaceId}"`. Order: `TenantScopeBanner` → single `<h1>` → `WorkspaceTrustSummary` → predictable sections (UX-DR18 exactly: Overview | Folder metadata | Diagnosis | Audit trail | Provider readiness | Lock/task history | Access evidence) → optional `TrustMatrix`. The **"Folder metadata" section hosts `MetadataOnlyFolderTree`** — this is its primary placement, because the in-route `{WorkspaceId}` plus `WorkspaceStatus.AcceptedCommandState.TaskId` supply the `workspaceId`+`taskId` the file-tree calls require. Sections are navigable but read-only; cross-links to evidence (UX-DR19) — link out to the (later-story) audit/provider pages by route, do not build them here. testid `console-page-workspace-root`.
  - [x] Assemble the trust-summary view-model from the SDK calls in the §"Data path" Dev Note (diagnostics primary + status DTOs). A private UI view-model record is allowed (UI assembly only, **not** Contracts; not a registered facade/`IQueryService`).
  - [x] bUnit + E2E smoke.

- [x] **Task 9 — Wire navigation + correlation/read-consistency plumbing** (AC: #1, #6, #7, #9)
  - [x] Add custom nav entries for the folder list / workspace detail (these are hand-authored pages, not generated projection grids — they need explicit nav slots per wireflow §1.2). Keep `NavigationContractTests.Console_DoesNotRegisterAnyDomainCommandManifest` green.
  - [x] Per page load, generate one `x_Correlation_Id`, pass it to every SDK call, and surface it as a safe-copy field (it is mostly the request echo, not a response field). Pass the eventually-consistent member of `ReadConsistencyClass` for console browsing (concern #19). For `GetWorkspaceCleanupStatusAsync`, supply the task id only when one is available (e.g. `WorkspaceStatus.AcceptedCommandState.TaskId`); otherwise omit the cleanup panel rather than fabricate a task id.

- [x] **Task 10 — Tests, guards, and verification** (AC: #11, #12)
  - [x] Per-page `Renders_NoMutationAffordances` (5-selector guard). Confirm `Console_DoesNotRegisterAnyDomainCommandManifest` and the `ScaffoldContractTests` dependency-direction test stay green (UI references Client + FrontComposer.Shell only).
  - [x] Total `[Theory]` enum-coverage tests for every new SDK-enum switch; never a silent default (drift sentinel).
  - [x] `git diff --stat src/Hexalith.Folders.Client/Generated/` empty; no `Directory.Packages.props` / OpenAPI-spine / generated edits; no new `<PackageReference>` unless justified.
  - [x] Build/test with the WSL-accessible Windows SDK (`/mnt/c/Program Files/dotnet/dotnet.exe`); run the narrowest relevant lanes: `dotnet test tests/Hexalith.Folders.UI.Tests`. Record results and distinguish any pre-existing baseline reds (see Dev Notes) from regressions.

- [x] **Task 11 — Self-review against the wireflow contract and scope** (AC: all)
  - [x] Confirm IDs/names preserved verbatim (no UX-DR / C6 / disposition / `FieldDisclosure` renumber-or-fork); the four §2 distinctions hold; the §7 pending inputs are not invented; no provider/audit/incident/perf-gate scope crept in (those are 6.7/6.8/6.9/6.10/6.11). Record the self-check in the Dev Agent Record.

## Dev Notes

### Scope boundaries (read first)

- **In scope:** the **Folder view (§3.1)** and **Workspace view (§3.2)** of the operations console, plus the folder list discovery entry. Three new pages (`Folders.razor`, `FolderDetail.razor`, `Workspace.razor`) and four new components (`TenantScopeBanner`, `WorkspaceTrustSummary`, `MetadataOnlyFolderTree`, `TrustMatrix`) under `src/Hexalith.Folders.UI/`, composing the shipped 6.3/6.4 atomic components and reading projections through the SDK `IClient`. Surfaced metadata: lifecycle, readiness, lock, dirty, commit, failure, cleanup, freshness, correlation (epics.md:1394).
- **OUT of scope (do NOT build here — owned by siblings):**
  - **Provider readiness / support / credential-reference / sync / capability pages → Story 6.7** (epics.md:1397-1408). The provider/credential-ref/sync/binding slice of FR52 is 6.7's. You may show a *provider readiness* status cell in the Trust Matrix and link out, but do not build the provider diagnostic page.
  - **Audit-trail / operation-timeline pages → Story 6.8** (FR53/FR54/FR56; epics.md:1410-1421). Link to audit from the workspace page (UX-DR19) by route; do not build the timeline/audit pages or call paginated audit query UI here.
  - **Incident-mode `/_admin/incident-stream` + degraded-mode banner → Story 6.9** (epics.md:1423-1434).
  - **Performance p95/p99 gate + 400ms skeleton + 2s cancel UX → Story 6.10** (epics.md:1436-1447). Build pages that *can* meet the 500ms-p95 status-query target; the measured gate and the `SkeletonState`/`StillLoadingCancel` components are 6.10's. (You may show a simple loading indicator, but don't implement the timed skeleton/cancel contract.)
  - **Formal no-mutation + WCAG 2.2 AA verification sweep → Story 6.11** (epics.md:1449-1462). You must satisfy read-only + a11y design on your own pages; the automated+manual verification, responsive/zoom matrix, and screen-reader walkthroughs are 6.11's.
  - **Editing the planning artifacts, the wireflow notes, the OpenAPI spine, or the generated client.** All read-only inputs.
- **Negative-scope guard:** if you find yourself calling a mutating SDK method (anything returning `AcceptedCommand`/`CommitWorkspaceAccepted` or taking `idempotency_Key`), defining a `[Command]` projection, calling `AddHexalithEventStore`, editing `src/Hexalith.Folders.Client/Generated/`, adding a project reference beyond Client + FrontComposer.Shell, adding a projection DTO to `Hexalith.Folders.Contracts`, or building a provider/audit/incident page — **stop**; none are in 6.6's surface.

### The reviewed contract and prerequisites (all satisfied)

Story 6.6 is unblocked: `docs/ux/ops-console-wireflows.md` exists and was reviewed (Story 6.5, done). The five upstream dependencies and what each gives you:
- **6.1** — projection/audit endpoints + DTOs (link-out target for the workspace page; not the primary data here).
- **6.2** — the FrontComposer-hosted shell, `FoldersUserContextAccessor` (`Services.Replace` of the fail-closed `NullUserContextAccessor`), the `AddFoldersClient` + `BearerTokenDelegatingHandler` data path, the `data-testid="console-page-{name}-root"` + single-`<h1>` + `FocusOnNavigate` conventions, the bUnit fixture, and the command-suppression guards.
- **6.3** — `OperatorDispositionBadge`, `TechnicalStateMetadata`, `DispositionLabelMapper` (the F-4 canonical mapper, parity-tested against the server).
- **6.4** — `RedactedField`, `FieldDisclosure`, `RedactionDisclosureMapper`, `FoldersConsoleIcons.LockClosed16()` (the F-5 affordance).
- **6.5** — the wireflow notes themselves (the gate); §3.1/§3.2 are your page specs.

### What already exists — compose, do not reinvent

**Components** (`src/Hexalith.Folders.UI/Components/`):
- **`OperatorDispositionBadge.razor`(+`.cs`)** — `[Parameter,EditorRequired] OperatorDispositionLabel Disposition`; `[Parameter] string? ColumnHeader` (→ aria-label `"{ColumnHeader}: {Label}"`); `[Parameter] string? AdditionalCssClass`. Wraps `FcStatusBadge` (emits `data-fc-badge-slot`, `role="status"`); outer `data-testid="operator-disposition-badge"`. **Takes a disposition, not a state** — map state→disposition first.
- **`TechnicalStateMetadata.razor`(+`.cs`)** — `[Parameter,EditorRequired] LifecycleState State`; `string? ColumnHeader`; `bool IncludePrefix = true` (true → `"state: ready"`, false → `"ready"`). Muted `Typography.Secondary`; `data-testid="technical-state-metadata"`, `data-fc-technical-state`.
- **`RedactedField.razor`(+`.cs`)** — `[Parameter,EditorRequired] FieldDisclosure Disclosure`; `string? Value` (**rendered only when `Visible`** — never leaks otherwise); `string? ColumnHeader`; `RedactedExplanation` (default "Hidden by tenant policy — contact your administrator"), `UnknownText` (default "Unknown"), `MissingText` (default "Not recorded"), `AdditionalCssClass`. Pass `null` (not `""`) to keep defaults. Outer `data-testid="redacted-field"`, `data-fc-disclosure` ∈ {visible,redacted,unknown,missing}; redacted state shows `FoldersConsoleIcons.LockClosed16()` (`aria-hidden`).

**Services** (`src/Hexalith.Folders.UI/Services/`) — all `static`, total over their enum, **throw `ArgumentOutOfRangeException` on unknown** (never silent default):
- **`DispositionLabelMapper`** — `ResolveDisposition(LifecycleState, bool hasProjectionLagEvidence = false)` (the lag flag matters only for `Ready`: true→`Degraded_but_serving`, false→`Available`); `ResolveSlot(OperatorDispositionLabel)→BadgeSlot` (Auto_recovering→Info, Available→Success, Degraded_but_serving→Warning, Awaiting_human→Warning, Terminal_until_intervention→Danger); `ResolveLabel(...)→string`; `ResolveTechnicalStateLabel(LifecycleState)→` snake_case wire name.
- **`FieldDisclosure`** — enum `Visible, Redacted, Unknown, Missing` (presentation-only; never on the wire).
- **`RedactionDisclosureMapper`** — `FromFileMetadataRedaction(FileMetadataItemRedaction)` (Not_redacted→Visible, Redacted→Redacted, Excluded/Binary_disallowed→Missing) — **use this for the folder tree**; `FromDiagnosticClassification(DiagnosticFieldClassification, bool hasValue)` (Forbidden→Redacted; Consumer_safe/Operator_sanitized→Visible if hasValue else Missing) — **use this for diagnostics `FieldClassifications`**; plus `FromAuditVisibility`, `FromTimestampPrecision`, `FromAuditRedaction`. **Deliberately does NOT map `ProjectionAvailability`** (its `redacted`/`unknown` are C5 reference-pending — see Pending inputs).
- **`FoldersConsoleIcons.LockClosed16()`** — hand-authored 16px Fluent core `Icon`. **Do not add the Fluent Icons package** (6.4 AC#7 forbids it).
- **`FoldersUserContextAccessor`** — the authenticated-context bridge; reads `tenant_id` → TenantId, `ClaimTypes.NameIdentifier` → UserId from the circuit `AuthenticationStateProvider`. Source the Tenant Scope Banner's tenant from here.

**Page/scaffold conventions:** global Interactive Server render mode (`App.razor`, `Program.cs:21-22`); pages carry no `@rendermode`. `Routes.razor` has `<FocusOnNavigate Selector="h1" />` — every page renders exactly one `<h1>` and no competing skip link. `MainLayout.razor` = `<FrontComposerShell AppTitle="…">@Body</FrontComposerShell>`. `_Imports.razor` does **not** include `Hexalith.Folders.Client.Generated` or `Hexalith.Folders.UI.Services` — add explicit `@using` per page (gallery pages do this). No data page exists yet — `Tenants.razor` is a static placeholder ("Tenant pickers ship in Story 6.6"); 6.6 is the first SDK-calling page.

### Data path — the most important section

**The console reads through the generated NSwag client `IClient`, injected directly.** `AddFoldersClient(...)` + `.AddHttpMessageHandler<BearerTokenDelegatingHandler>()` are already wired in `CompositionRoot.cs:150-158`. **Bind the generated `Hexalith.Folders.Client.Generated` DTOs — NOT the server-side `*QueryResult` records** (those live in `Hexalith.Folders` and are not on the SDK). All read methods take `string x_Correlation_Id, ReadConsistencyClass? x_Hexalith_Freshness` (some also `x_Hexalith_Task_Id`) and a `CancellationToken` overload.

> ⚠ **This is the exact correction that was a HIGH review finding in Story 6.5:** the as-built path is the **direct SDK** wiring above — there is **no `FoldersClientFacade` and no `IQueryService`** in `src/Hexalith.Folders.UI`. Do not assume one exists or build one unless you genuinely need FrontComposer-native query composition (you do not, for 6.6). If you build a UI view-model aggregator, keep it a private UI-assembly record — not a registered service, not in Contracts.

**Primary operator-rendering source — the ops-console diagnostics DTOs** (subclass `DiagnosticBase`; carry server-computed `Disposition` (`OperatorDispositionLabel` → feeds `OperatorDispositionBadge` directly), `Status` (string), `Trust` (`DiagnosticTrustEvidence`: `Availability` (`ProjectionAvailability`), `FreshnessAgeMilliseconds` [C5 reference-pending], `StaleReasonCode`, `UnavailableReasonCode`), `FieldClassifications` (`DiagnosticFieldClassificationEntry[]` → feeds `RedactionDisclosureMapper.FromDiagnosticClassification`), `Freshness` (`FreshnessMetadata`)). Already consumed by MCP `DiagnosticsTools.cs` — proof these are the console-rendering surface:

| Method (IClient) | Path args | Response | Notes |
|---|---|---|---|
| `GetLockDiagnosticsAsync` | folderId, workspaceId | `LockDiagnostics` (+ `LockReference` RedactableDiagnosticIdentifier) | lock panel of the workspace page |
| `GetDirtyStateDiagnosticsAsync` | folderId, workspaceId | `DirtyStateDiagnostics` (+ `ChangedPathEvidence` — `evidenceKind`/`digest`/`reference` only, **never a path/content/diff**) | dirty panel |
| `GetFailedOperationDiagnosticsAsync` | folderId, workspaceId | `FailedOperationDiagnostics` (`OperationId`, `TaskId`, `SanitizedErrorCategory` `CanonicalErrorCategory`, `RetryEligibility`) | failure panel; retry eligibility is **advisory display only**, never a retry button |
| `GetSyncStatusDiagnosticsAsync` | folderId, workspaceId | `SyncStatusDiagnostics` (`AcceptedCommandState` [C5/C6 reference-pending], `ProjectedState` `LifecycleState`, `ProviderOutcomeState`) | sync panel |
| `GetReadinessDiagnosticsAsync` | **none** (correlation+freshness only) | `ReadinessDiagnostics` (folder/workspace/provider summary references) | **tenant-scoped rollup, not per-workspace** — use for the readiness dimension, do not assume a folder/workspace path arg |
| `GetProjectionFreshnessAsync` | **none** | `ProjectionFreshnessDiagnostics` | tenant-scoped freshness rollup |
| `GetProviderStatusDiagnosticsAsync` | folderId | `ProviderStatusDiagnostics` | provider page is **6.7**; only surface a readiness *cell* + link here |

**Supplementary identity/lifecycle/lock-lease/cleanup/permissions — the plain status DTOs** (bind for the fields the diagnostics shapes lack):

| AC term | Method → DTO | Key fields |
|---|---|---|
| **lifecycle** (folder) | `GetFolderLifecycleStatusAsync` → `FolderLifecycleStatus` | `FolderId`, `LifecycleState`, `Archived`, `RepositoryBindingId`, `ProviderBindingRef`, `Freshness` |
| **lifecycle** (workspace) | `GetWorkspaceStatusAsync` → `WorkspaceStatus` | `CurrentState` (`LifecycleState`), `ProjectedState` (`ProjectedWorkspaceState`: `State`/`StateSource`/`ObservedAt`), `ProviderOutcome`, `ProjectionLag` (`AgeMilliseconds`/`StateSource`), `LastFailureCategory` (`CanonicalErrorCategory`), `RetryEligibility`, `Freshness`, `AcceptedCommandState` (`TaskId`/`OperationId`/`State`) |
| **lock** | `GetWorkspaceLockAsync` → `WorkspaceLockStatus` | `WorkspaceReference` (`RedactableDiagnosticIdentifier`), `LockState` (`LockState`), `Lease` (`LockLeaseMetadata`: `LockId`/`LeaseStatus`/`AcquiredAt`/`ExpiresAt`/`HolderRef`), `RetryEligibility` (`WorkspaceRetryEligibility` — carries this DTO's only `CorrelationId`), `Freshness` |
| **cleanup** | `GetWorkspaceCleanupStatusAsync` (needs `x_Hexalith_Task_Id`) → `WorkspaceCleanupStatus` | `Status` (`CleanupStatus`), `ReasonCode`, `CorrelationId`, `ObservedAt`, `LastAttemptedAt`, `RetryEligibility`, `Freshness` |
| **readiness/access** | `GetEffectivePermissionsAsync` → `EffectivePermissions` | `Permissions` (`FolderPermissionLevel[]`), `AuthorizationOutcome`, `Freshness` |
| **repo binding** | `GetRepositoryBindingAsync` (needs `repositoryBindingId` from `FolderLifecycleStatus.RepositoryBindingId`) → `RepositoryBinding` | `BindingState`, `SensitiveMetadataTier`, `ProviderBindingRef` — **no list endpoint exists; single-binding GET only** |
| **task** | `GetTaskStatusAsync` → `TaskStatus` | `CurrentState`, `TerminalState`, `LastOperationId`, `LastFailureCategory`, `RetryEligibility`, `Freshness` |
| **commit** (ref) | `GetCommitEvidenceAsync` → `CommitEvidence` | `CommitReferenceClassification` (`opaque_reference`/`redacted`/`unavailable`) — **never a raw commit hash**; render the classification, redact via `RedactedField` when `redacted` |
| **folder tree** | `ListFolderFilesAsync` → `FileTreeResult` (+ `GetFolderFileMetadataAsync` → `FileMetadataResult`) | **requires `(folderId, workspaceId, x_Correlation_Id, x_Hexalith_Task_Id, …)`** — both a workspaceId and a task id. path metadata, type, size class, last op, changed status, access state, redaction (`FileMetadataItemRedaction` → `RedactionDisclosureMapper.FromFileMetadataRedaction`). This is a **context query (2s p95 budget)**, not a 500ms status query — keep the tree bounded. Workspace-scoped → primary placement on `Workspace.razor` (see AC #4) |

**Field/shape gaps to handle (do not paper over):**
- **Correlation is mostly the request echo, not a response field.** Only `WorkspaceCleanupStatus.CorrelationId`, the lock DTO's `WorkspaceRetryEligibility.CorrelationId`, and `ProviderOutcome.ProviderCorrelationReference` carry one. For lifecycle/workspace/permissions/task, surface the `x_Correlation_Id` you sent. Generate one correlation id per page load and reuse it across that page's calls.
- **No `AuthorizationOutcome`/`AuthorizationDenial` on most SDK DTOs.** Authorization-before-observation + safe-denial are server-enforced and surface as HTTP status + Problem Details → a thrown `HexalithFoldersApiException`, not a data field. Catch it and render the safe-denial path (AC #10). `EffectivePermissions.AuthorizationOutcome` is the one exception you can read directly.
- **No `FreshnessMetadata.ReasonCode`.** Stale/unavailable reason text comes only from `DiagnosticTrustEvidence.StaleReasonCode`/`UnavailableReasonCode` (diagnostics DTOs).
- **No commit ref / `CommitExecutionStatus` on `WorkspaceStatus`** — commit reaches the SDK only via `GetCommitEvidenceAsync` (above).
- **`RedactableDiagnosticIdentifier`** wraps `Value`/`Classification`/`Redaction` (`RedactionMetadata`) — render via `RedactedField`, never the raw `.Value` when redacted.

**Disposition derivation (AC #7):** prefer the server `DiagnosticBase.Disposition` from a diagnostics DTO as the primary visual when the panel fetches one. For the Workspace Trust Summary headline derived from `WorkspaceStatus.CurrentState`, call `DispositionLabelMapper.ResolveDisposition(state, hasProjectionLagEvidence: workspaceStatus.Freshness.Stale)` — using the boolean `Stale` signal so you **never hardcode the TBD C2 numeric lag**. The client mapper and server disposition are parity-guarded (6.3 `DispositionLabelParityTests`) and must agree.

**Read consistency:** the console is eventually-consistent browsing (concern #19) — pass the eventually-consistent member of `ReadConsistencyClass` on read calls.

### Status taxonomy + the four distinctions to preserve (wireflow §2)

Four vocabularies, never collapsed, no UI-only names invented: operator disposition (primary, `DispositionLabelMapper`/`OperatorDispositionBadge`) · technical lifecycle state (secondary, 11 C6 states, `TechnicalStateMetadata`) · field disclosure (`FieldDisclosure`/`RedactedField`) · access/availability (denied/inaccessible/stale-delayed/unavailable/missing/archived). The 12 epic terms (readiness, locked, prepared, dirty, committed, audited, failed, stale, unavailable, inaccessible, redacted, unknown) each need text + icon/shape + color + accessible label (UX-DR14). Preserve verbatim: **`stale`/`delayed` ≠ `unavailable`** (data old vs read model down); **`redacted` ≠ `unknown` ≠ `missing`** (policy-hidden vs not-known vs never-recorded — redaction via `RedactedField` only, silent truncation/representing-as-missing is a bug); **`denied` ≠ `inaccessible`** (safe-denial-no-existence vs known-but-unreachable); **disposition (primary) ≠ technical state (secondary)** (never promote a state name or drop the disposition; `unknown_provider_outcome` → `awaiting-human` Warning badge, not a neutral "Unknown").

### C6 states (all 11 must render; matrix is total)

`requested`(auto-recovering) · `preparing`(auto-recovering) · `ready`(available, or degraded-but-serving under projection lag) · `locked`(degraded-but-serving) · `changes_staged`(degraded-but-serving) · `dirty`(awaiting-human) · `committed`(auto-recovering) · `failed`(terminal-until-intervention) · `inaccessible`(terminal-until-intervention) · `unknown_provider_outcome`(awaiting-human) · `reconciliation_required`(awaiting-human). The page renders whichever is current — no happy-path subset. **No silent repair/discard/commit/release affordance** even for `dirty`/`failed`/`reconciliation_required` (MVP leaves inspectable state; repair is post-MVP).

### Pending / deferred inputs — must NOT invent or hardcode (wireflow §7)

- **C2 status-freshness target** — TBD; render a freshness zone + stale/delayed label with timestamp, drive the `ready`→`degraded-but-serving` downgrade from `Freshness.Stale`. Do **not** hardcode "fresh within N seconds".
- **C3 retention vocabulary** — reference-pending; surface any `retentionClass` value as-is, invent no durations.
- **C4 metadata-filter vocabulary** — rejection-only today (server rejects non-null filters with `validation_error`/`filter_not_yet_supported`). Expose only search/filter inputs the server accepts; don't imply unsupported filters work.
- **`ProjectionAvailability.redacted`/`.unknown`** — C5 reference-pending; `RedactionDisclosureMapper` deliberately does **not** map them. Treat as not-yet-available; render no disclosure state for them; do not extend the mapper.
- **`SyncStatusDiagnostics.AcceptedCommandState`** and **`DiagnosticTrustEvidence.FreshnessAgeMilliseconds`** — C5/C6 reference-pending candidate sets; do not treat as frozen contract.
- **French localization** — deferred to 6.11; English MVP. Keep label-producing methods `string`-returning so an `IStringLocalizer` wrapper is a later refactor (6.3/6.4 precedent). Do not add FR strings; do not edit `deferred-work.md`.

### Accessibility (build defensively; 6.11 verifies)

Keyboard access for search/filters/result-selection/tabs/tables/**tree expand-collapse**/detail-panels/dialogs (UX-DR30); visible focus + reading-order focus + dialog focus-trap/restore (UX-DR24); non-color-only status everywhere (UX-DR14, inherited from the shipped components — apply to the new ones); zoom resilience at 125/150/200% with dense identifiers/long paths not clipping (UX-DR30/31 — use safe truncation with full value via tooltip/copy, ux-spec:781); responsive desktop-first with tablet/mobile fallback that stacks panels, collapses nav, preserves search/filter and high-level trust review (UX-DR28/29); redacted-vs-unknown-vs-missing visibly + semantically distinct via `RedactedField`; screen-reader-meaningful labels for redacted/inaccessible/unknown/unavailable/failed/delayed/dirty/locked/ready/committed states; no motion dependency.

### Performance (build to meet; 6.10 owns the gate)

Status queries target **500ms p95** (prd.md:715); folder/workspace pages are "primary diagnostic flows" (architecture F-7: p95<1.5s, p99<3s). The folder tree is a context query (2s p95) — keep it bounded. Don't implement the 400ms-skeleton / 2s-cancel contract or the measured perf gate (6.10). A simple loading indicator is fine.

### Test plan

- **bUnit** (`tests/Hexalith.Folders.UI.Tests`): reuse `BadgeRenderingFixture` (`AddFluentUIComponents` + `AddHexalithFrontComposerQuickstart` + storage/theme/JS substitutes). For SDK-calling pages, register an NSubstitute `IClient` and an `IUserContextAccessor` (follow `ShellCompositionTests` lines ~38-44). Per page: assert single `<h1>`, page-root testid, and the 5-selector `Renders_NoMutationAffordances` guard. Total `[Theory]` over each consumed SDK enum. Force `en-US` for aria-label assertions.
- **DI/contract**: `Console_DoesNotRegisterAnyDomainCommandManifest` and `ScaffoldContractTests.ProjectReferencesFollowAllowedDependencyDirection` stay green.
- **E2E** (`tests/Hexalith.Folders.UI.E2E.Tests`): a folder-route smoke test following `ConsoleSmokeTests` (new `ConsoleRoutes` constant; assert 200-399 + page-root visible + `<h1>`).
- **Build/test**: `/mnt/c/Program\ Files/dotnet/dotnet.exe` (WSL-native SDK fails the global.json 10.0.302 pin). Root-level submodules only — never `--recursive`.

### Build/test environment + known pre-existing reds (not regressions)

Verify against the prior-story baseline, not from zero: `BranchRefPolicyEndpointTests.GetBranchRefPolicyShouldUseSafeDenialEnvelopeForTenantMismatch`; six Contracts OpenAPI/Safety reds; three `Hexalith.Folders.Testing.Tests` baseline reds (`ProjectReferencesFollowAllowedDependencyDirection` vs IntegrationTests, `DeferredArtifactAreasCarryMachineCheckableOwnershipNotes`, `SolutionContainsOnlyCanonicalBuildableProjects`). If these are red on a clean baseline they are not 6.6 regressions — confirm and note, don't "fix" them under this story.

### Review-cycle lessons to avoid repeating

1. **As-built data path (HIGH in 6.5):** direct `AddFoldersClient` + `BearerTokenDelegatingHandler`; no `FoldersClientFacade`/`IQueryService`.
2. **Override-level attributes:** only `[Projection]`, `[BoundedContext]`, `[Command]`, `[ProjectionTemplate]` are real attributes; `[ProjectionSlot]`/`[ProjectionViewOverride]` are descriptor records, not attributes. (6.6's pages are custom hand-authored Razor; you likely use none of these.)
3. **File List fidelity:** list every shipped file incl. E2E routes/tests.
4. **Totality sentinels:** every SDK-enum switch is a total theory that throws on unknown — never a silent default (disposition/redaction correctness).
5. **`aria-label` never silent;** color/icon never the only signal.
6. **Terminology:** "Blazor Web App host using Interactive Server rendering" — do not revert to "Blazor Server".

### Project Structure Notes

- New pages land under `src/Hexalith.Folders.UI/Components/Pages/` (matching the shipped `Home.razor`/`Tenants.razor` on-disk convention) — **not** the flat `Pages/` the architecture diagram (architecture.md:1173-1202) draws; follow the actual on-disk `Components/`-rooted Blazor Web App layout. These map to the architecture's `Folders.razor` (folder list) and `FolderDetail.razor` (workspace status/lock/commit); the wireflow's cleaner Folder-view vs Workspace-view split is the authoritative contract and is honored.
- New components land under `src/Hexalith.Folders.UI/Components/`. New components/pages are `public` (Blazor routing/cross-assembly); any mapper-style helper may be `internal`.
- `Hexalith.Folders.UI` references **only** `Hexalith.Folders.Client` + `Hexalith.FrontComposer.Shell` (enforced by `ScaffoldContractTests`). Do **not** add projection DTOs to `Hexalith.Folders.Contracts`; any richer UI view-model is a private UI-assembly record.
- Route constants are centralized in `tests/Hexalith.Folders.UI.E2E.Tests/Routes/ConsoleRoutes.cs` — add `Folders`/`FolderDetail`/`Workspace` there; tests must not hardcode route strings elsewhere.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-6.6` (lines 1384-1395) — story statement + AC]; [Epic 6 scope + console guardrails (lines 496-505, 1305-1311); siblings 6.7-6.11 (1397-1462)]
- [Source: `docs/ux/ops-console-wireflows.md` §1.4 data path (160-186), §2 taxonomy + distinctions (212-279), §3.1 Folder view (296-321), §3.2 Workspace view (323-351), §3.6-§3.9 redaction/loading/empty/error (440-536), §4/§4.1 accessibility (540-598), §5 traceability (602-651), §6 Journeys 1&2 (677-731), §7 pending inputs (765-776)]
- [Source: `_bmad-output/planning-artifacts/architecture.md` F-1..F-7 (545-551); C6 Workspace State Transition Matrix + dispositions (218-279, mapper sourced at 279); concern #11 read-only/metadata-only console (110), #12 tenant provenance (111), #17 sensitive metadata (116), #19 read consistency (118); hard product boundaries (90-92); UX Design Integration Implications (165-172); UI source tree (1173-1202); FrontComposer integration (144-146); FR52 mapping (1364); S-6 default sensitivity tier (480)]
- [Source: `_bmad-output/planning-artifacts/ux-design-specification.md` UX-DR table (103-140, line 105 preserve-IDs mandate); component anatomy Workspace Trust Summary (522-532), Tenant Scope Banner (534-544), Metadata-Only Folder Tree (546-556), Trust Matrix (570-580); workspace detail composition (412-420); Journeys 1/2/3 (424-482); accessibility strategy (740-783); folder-tree keyboard/expansion (783); long-value truncation (781)]
- [Source: `_bmad-output/planning-artifacts/prd.md` FR31 (641), FR36 (649), FR52-FR57 (677-682); status-query 500ms p95 (715); metadata-only/no-secrets (86, 691, 750); read-model determinism (483, 754); status-freshness + C2 TBD (511, 755); ops-console projection scope (330, 343, 496, 753); sensitive-metadata classification (751-752); WCAG console (770-774); scope-creep risk (304)]
- [Source: `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs` — `IClient` read methods + generated DTOs (`FolderLifecycleStatus`, `WorkspaceStatus`, `WorkspaceLockStatus`, `WorkspaceCleanupStatus`, `EffectivePermissions`, `RepositoryBinding`, `TaskStatus`, `CommitEvidence`, `FileTreeResult`; `DiagnosticBase` family — `LockDiagnostics`/`DirtyStateDiagnostics`/`FailedOperationDiagnostics`/`SyncStatusDiagnostics`/`ReadinessDiagnostics`/`ProjectionFreshnessDiagnostics`; `OperatorDispositionLabel`, `LifecycleState`, `CanonicalErrorCategory`, `ProjectionAvailability`, `DiagnosticFieldClassification`, `FreshnessMetadata`, `RedactableDiagnosticIdentifier`); `HexalithFoldersApiException`]; [OpenAPI spine `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` — ops-console tag (29-30), diagnostics paths (4300-4866), read routes]
- [Source: `src/Hexalith.Folders.UI/CompositionRoot.cs:150-158` (AddFoldersClient + BearerTokenDelegatingHandler), `:88-91` (FoldersUserContextAccessor Services.Replace), `:65-67` (FrontComposer quickstart, no AddHexalithDomain)]
- [Source: `src/Hexalith.Folders.UI/Components/{OperatorDispositionBadge,TechnicalStateMetadata,RedactedField}.razor(.cs)`, `Components/Icons/FoldersConsoleIcons.cs`, `Services/{DispositionLabelMapper,FieldDisclosure,RedactionDisclosureMapper,FoldersUserContextAccessor}.cs`, `Components/Pages/{Home,Tenants,StateLabelGallery,RedactionGallery}.razor`, `Components/{Routes,App,Layout/MainLayout,_Imports}.razor`]
- [Source: `tests/Hexalith.Folders.UI.Tests/{BadgeRenderingFixture,CompositionRootFactory,NavigationContractTests,ShellCompositionTests}.cs`; `tests/Hexalith.Folders.UI.E2E.Tests/{Routes/ConsoleRoutes.cs,Smoke/ConsoleSmokeTests.cs}`; `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs`]
- [Source: prior stories `_bmad-output/implementation-artifacts/6-{1,2,3,4}-*.md` — endpoints/DTOs (6.1), shell + conventions + data path (6.2), disposition components + mapper + parity test (6.3), redaction component + mapper + icons (6.4); `6-5-author-console-diagnostic-wireflow-notes.md` — the reviewed contract + the §1.4 as-built-vs-planned HIGH review lesson]
- [Source: MCP `src/Hexalith.Folders.Mcp/Tools/DiagnosticsTools.cs` — proof the `DiagnosticBase` ops-console diagnostics surface is the operator-rendering source already consumed by an adapter]
- [Note: architecture F-1/UI-tree say "Blazor Server"; as-built + Story 6.2 is "Blazor Web App host using Interactive Server rendering" — use the as-built term, do not revert.]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.8 (1M context) — `claude-opus-4-8[1m]`

### Debug Log References

- Build/test via the WSL-accessible Windows SDK (`/mnt/c/Program Files/dotnet/dotnet.exe`); the WSL-native SDK fails the `global.json` 10.0.302 pin.
- Baseline (pre-change) `dotnet test tests/Hexalith.Folders.UI.Tests`: **117 passed, 0 failed** — the UI lane is green on baseline (the known reds live in Contracts / Testing.Tests, not this lane).
- Final `dotnet test tests/Hexalith.Folders.UI.Tests`: **278 passed, 0 failed** (+161 new). E2E project compiles clean (Playwright/Aspire harness required to execute the smoke lane; not run here).
- Compile fixes encountered: `FluentTextField` did not resolve under the Fluent UI v5 RC → replaced the discovery/navigation fields with plain accessible `<label>/<input>/<button type="button">` (read-only navigation, still passes the 5-selector mutation guard); `CA2007` is warnings-as-error in the UI project → added `ConfigureAwait(false)` to all SDK awaits (matching the existing `BearerTokenDelegatingHandler` convention); avoided the `Microsoft.FluentUI.AspNetCore.Components.Icons` package (off the reference graph) by hand-authoring status glyphs on `FoldersConsoleIcons` and reusing the shell's `FcFluentIcons.DocumentSearch48()`.

### Completion Notes List

- **Three read-only pages** (`Folders`, `FolderDetail`, `Workspace`) under `Components/Pages/`, each single-`<h1>`, `<PageTitle>`, `console-page-{name}-root`, no per-page `@rendermode` (global Interactive Server via `FrontComposerShell`), relying on the shell's `<FocusOnNavigate Selector="h1" />`. Route constants + helpers added to `ConsoleRoutes.cs`; `Tenants.razor` placeholder resolved (links to `/folders`).
- **Four new components**: `TenantScopeBanner` (UX-DR6; tenant sourced from `IUserContextAccessor`, never the route — proven by test), `WorkspaceTrustSummary` (UX-DR5/F-4; disposition badge primary + technical state secondary; full field set; `RedactedField` for repo binding/provider/commit ref; freshness zone driven by the boolean `Freshness.Stale`, never a hardcoded C2 lag), `MetadataOnlyFolderTree` (UX-DR7/12; **table variant** per AC #4 / ux-spec:783 with hierarchy via normalized path + a11y labels; no open/content/diff/download; no fabricated workspace/task id), `TrustMatrix` (UX-DR9; six dimensions as grouped evidence with icon + non-color-only badge + connected-evidence links).
- **Data path** = direct SDK `IClient` (no `FoldersClientFacade`/`IQueryService` — the as-built path, the HIGH 6.5 lesson). Diagnostics shapes + plain status DTOs bound per the Data-path note; one `x_Correlation_Id` per page load surfaced as safe-copy; `ReadConsistencyClass.Eventually_consistent` passed on every read (concern #19). Denials surface as thrown `HexalithFoldersApiException` → safe-denial via `ConsoleErrorPresenter` (canonical A-8 metadata only; never the server message, a stack trace, or a `taskId` off the body; `not_found`/`*_denied` never expanded into an existence oracle).
- **Totality sentinels**: every new SDK-enum switch (`LockState`, `CleanupStatus`, `FileMetadataItemKind`, `FileMetadataItemRedaction`, `CommitEvidenceCommitReferenceClassification`, `EffectivePermissionsAuthorizationOutcome`, `OperatorDispositionLabel`, `ProviderOutcomeState`, `CanonicalErrorCategory`) throws on unknown and is covered by a total `[Theory]` over `Enum.GetValues<T>()`. `ProjectionAvailability` is deliberately **not** mapped for disclosure (C5 reference-pending); `RedactionDisclosureMapper` was not extended.
- **Read-only invariants**: per-page `Renders_NoMutationAffordances` (5-selector guard) green; `Console_DoesNotRegisterAnyDomainCommandManifest` green (no manifest registered — the framework nav is manifest-driven so the "explicit nav slot" is a hand-authored primary link on Home/Tenants → `/folders`); `AddHexalithEventStore` not called; no `[Command]` projection; no mutating SDK method invoked. `git diff --stat src/Hexalith.Folders.Client/Generated/` empty; `Directory.Packages.props` and the OpenAPI spine untouched; no new `<PackageReference>`; UI references remain Client + FrontComposer.Shell only.
- **Honest shape gaps (not papered over)**: there is no tenant-wide folder-list or workspace-list SDK endpoint (per-resource GET only) → the folder list page is a banner + folder-id navigation + insufficient-filter-scope empty state, and the folder detail page offers workspace-id navigation; `FileMetadataItem` carries no per-entry "last op"/"changed" signal → those columns render the honest `Unknown` disclosure rather than a fabricated value.
- ✅ **Self-review (Task 11)**: UX-DR / C6 / disposition / `FieldDisclosure` IDs referenced verbatim (none renumbered or forked). The four §2 distinctions hold visibly + semantically: `stale`/`delayed` (freshness zone) ≠ `unavailable` (read-model-unavailable empty state); `redacted` (lock) ≠ `unknown` ≠ `missing` (distinct `RedactedField` tokens); `denied` (safe-denial path, no existence) ≠ `inaccessible` (C6 lifecycle state); disposition (primary badge) ≠ technical state (muted secondary), and `unknown_provider_outcome` → `awaiting-human` badge (never a neutral Unknown). §7 pending inputs not invented (no C2 numeric lag, no C3 durations, no C4 filter vocabulary, no `ProjectionAvailability.redacted/unknown` mapping). No 6.7/6.8/6.9/6.10/6.11 scope crept in — provider/audit are link-outs by route only.

### File List

**New — `src/Hexalith.Folders.UI/`**
- `Services/TenantAccessState.cs`
- `Services/TenantScopeStateMapper.cs`
- `Services/TrustDimensionState.cs`
- `Services/TrustDimensionStateMapper.cs`
- `Services/TrustDimensionDeriver.cs`
- `Services/ConsoleStatusText.cs`
- `Services/ConsoleErrorPresenter.cs`
- `Services/EmptyStateReason.cs`
- `Components/Models/ConsoleErrorView.cs`
- `Components/Models/WorkspaceTrustSummaryModel.cs`
- `Components/Models/TrustMatrixCell.cs`
- `Components/TenantScopeBanner.razor`, `Components/TenantScopeBanner.razor.cs`
- `Components/WorkspaceTrustSummary.razor`, `Components/WorkspaceTrustSummary.razor.cs`
- `Components/MetadataOnlyFolderTree.razor`, `Components/MetadataOnlyFolderTree.razor.cs`
- `Components/TrustMatrix.razor`, `Components/TrustMatrix.razor.cs`
- `Components/ConsoleEmptyState.razor`, `Components/ConsoleEmptyState.razor.cs`
- `Components/ConsoleErrorPanel.razor`, `Components/ConsoleErrorPanel.razor.cs`
- `Components/SafeCopyId.razor` (review fix — UX-DR27/AC #9 read-only copy affordance)
- `Components/Pages/Folders.razor`
- `Components/Pages/FolderDetail.razor`
- `Components/Pages/Workspace.razor`, `Components/Pages/Workspace.razor.cs`

**Modified — `src/Hexalith.Folders.UI/`**
- `Components/Icons/FoldersConsoleIcons.cs` (added hand-authored status glyphs)
- `Components/Pages/Home.razor` (added primary `/folders` nav link)
- `Components/Pages/Tenants.razor` (resolved the "ship in 6.6" placeholder; links to `/folders`)
- `Components/RedactedField.razor`(+`.razor.cs`) (review fix — added backward-compatible `Monospace` option for visible identifiers)

**New — `tests/Hexalith.Folders.UI.Tests/`**
- `ConsoleTestAssertions.cs`, `DiagnosticTestContext.cs`
- `ConsoleStatusTextTests.cs`, `TenantScopeStateMapperTests.cs`, `TrustDimensionMappingTests.cs`, `ConsoleErrorPresenterTests.cs`
- `TenantScopeBannerTests.cs`, `WorkspaceTrustSummaryTests.cs`, `MetadataOnlyFolderTreeTests.cs`, `TrustMatrixTests.cs`
- `FoldersPageTests.cs`, `FolderDetailPageTests.cs`, `WorkspacePageTests.cs`
- `ConsoleEmptyStateTests.cs` (review fix — covers all four §3.8 empty-state reasons incl. denied-access)

**Modified / New — `tests/Hexalith.Folders.UI.E2E.Tests/`**
- `Routes/ConsoleRoutes.cs` (added `Folders` + `FolderDetail`/`Workspace` route helpers)
- `Smoke/FolderRouteSmokeTests.cs` (new folder-route smoke test)

**Tracking**
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (6-6 → in-progress → review)
- `_bmad-output/implementation-artifacts/6-6-build-folder-and-workspace-diagnostic-pages.md` (this story file)

## Senior Developer Review (AI)

**Reviewer:** Jerome · **Date:** 2026-05-29 · **Outcome:** Approve (auto-fix applied) · **Status → done** (0 CRITICAL).

Adversarial review fanned out across all 12 ACs + read-only invariants + test totality + scope, with every finding independently verified against the code before fixing. Baseline confirmed independently: UI lane built green at 278/278. After fixes: **298/298 pass** (+20 regression tests); generated client, `Directory.Packages.props`, and the OpenAPI spine untouched; the 5-selector command-suppression guard and `Console_DoesNotRegisterAnyDomainCommandManifest` stay green. **23 findings confirmed (6 HIGH, 8 MEDIUM, 9 LOW); 22 fixed, 1 accepted-by-design** (16 further raw findings were refuted on verification).

**HIGH — fixed**
- **AC #2 delegated-actor summary was absent** — the banner rendered only the principal. Added an honest "Delegated actor" row (this auth model has no on-behalf-of claim, so it states direct access rather than omitting the dimension). `TenantScopeBanner.razor(.cs)`.
- **AC #5 connected-evidence link missing on 4 of 6 trust-matrix cells (UX-DR19)** — wired all six cells to their existing on-page section anchors. `Workspace.razor.cs`.
- **AC #7 server-computed disposition was dead code** — `WorkspaceTrustSummaryModel.ServerDisposition` was never populated, so the F-4 headline always client-derived. Now fed from the dirty-state diagnostics DTO (`ServerDisposition = _dirty?.Disposition`), falling back to the canonical mapper only when absent. `Workspace.razor.cs`.
- **AC #9 safe-copy affordance was unimplemented** (the load-bearing half of UX-DR27) — added `SafeCopyId.razor` (monospace value + read-only clipboard button that does not trip the mutation guard) and routed every identifier through it (trust summary, folder identity, error correlation).
- **AC #10 error map missed the live denial vocabulary** — the server's `FolderAuthorizationDenialMapper` emits `not_found_to_caller`/`authorization_denied`/`policy_denied`/`policy_evidence_unavailable`/`path_policy_denied`, none of which were mapped (real denials fell to the generic envelope). Added existence-neutral safe copy for each. `ConsoleStatusText.cs`.
- **AC #7 server-disposition override had zero test coverage** — added bUnit + page tests pinning disposition provenance.

**MEDIUM — fixed**
- Excluded entries no longer render their path (defence-in-depth) or a fabricated "empty" size; `changed`/`clean`/`inaccessible` documented as consciously partial (not derivable from `FileMetadataItem`). `MetadataOnlyFolderTree`.
- Trust-matrix `last-updated` coalesces a default/min timestamp to "Unknown" instead of `0001-01-01`. `Workspace.razor.cs` / `TenantScopeBanner.razor.cs`.
- Lock-unknown and dirty-state now render through badges (UX-DR14: icon/shape + colour + label), not bare spans. `WorkspaceTrustSummary`.
- Added page-level render coverage for the server dirty disposition, and cleanup/commit/provider binding through the rendered page; tightened the folder-tree distinctness assertions to exact cardinalities + the redacted access label.

**LOW — fixed**
- Dead `/audit-trail` and `/provider` out-links (404 until 6.7/6.8) replaced with non-link "pending" affordances; trust-matrix evidence links use on-page anchors only.
- Repository-binding/provider identifiers now monospace (backward-compatible `RedactedField.Monospace`); freshness `<time>` only emitted with a machine-readable `datetime` (else plain "Unknown"); healthy "Latest reason category" renders "None" instead of the raw `success` token; the denied-access empty-state branch is now covered by a component test; Problem-Details `category` is guarded by a known-token allowlist before reaching the DOM; new presenter theory exercises the real live denial tokens.

**Accepted-by-design (not changed)**
- **LOW — folder tree hierarchy is presentational indent only, no tree expand/collapse.** The AC explicitly permits a table variant "if it preserves hierarchy + accessibility labels"; the table preserves the normalized path + per-cell labels for redacted/inaccessible entries. A full ARIA tree with keyboard expand/collapse is deferred (it adds little for shallow metadata rows and the 6.11 a11y sweep owns formal verification). Documented in the component remarks.

## Change Log

| Date | Change |
|---|---|
| 2026-05-29 | Implemented Story 6.6: three read-only diagnostic pages (Folders, FolderDetail/§3.1, Workspace/§3.2) + four new components (TenantScopeBanner, WorkspaceTrustSummary, MetadataOnlyFolderTree, TrustMatrix) + supporting safe-denial/empty-state infra and SDK-enum display mappers, all composing the shipped 6.3/6.4 atomic components and reading through the direct SDK `IClient`. Added bUnit + E2E coverage (278 UI-lane tests pass). Status → review. |
| 2026-05-29 | Senior Developer Review (AI, adversarial + auto-fix): 23 verified findings (6 HIGH, 8 MEDIUM, 9 LOW) — 22 fixed, 1 accepted-by-design. Notable: wired the server-computed disposition (AC #7) that was dead code, added the missing safe-copy affordance (`SafeCopyId`, AC #9), mapped the server's live denial categories (AC #10), connected all six trust-matrix evidence links (AC #5), rendered the delegated-actor summary (AC #2), and hardened the folder tree against excluded-path/size leakage. UI lane 278 → **298 pass** (+20 regression tests); generated client / packages / OpenAPI spine untouched. Status → done. |
