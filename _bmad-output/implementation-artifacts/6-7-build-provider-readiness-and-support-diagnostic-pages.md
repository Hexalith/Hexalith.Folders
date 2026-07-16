---
baseline_commit: aba57e2
---

# Story 6.7: Build provider readiness and support diagnostic pages

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an operator,
I want provider readiness and support diagnostic pages,
so that provider binding, credential-reference status, capability differences, and provider failure evidence are inspectable without secrets.

## Acceptance Criteria

Source epic AC (epics.md#Story-6.7, lines 1397-1408):

> **Given** projection endpoints, provider support evidence, and console wireflow notes exist
> **When** provider diagnostic pages render
> **Then** pages show authorized provider binding, credential-reference identifier/status, readiness reason, retryability, remediation category, capability, sync, and failure metadata
> **And** provider tokens, credential values, embedded credential URLs, and unauthorized repository existence are never displayed.

**Story 6.7 is the *Provider view (§3.3)* of the operations console.** Stories 6.1–6.4 shipped the projection endpoints and the reusable atomic components (`OperatorDispositionBadge`, `TechnicalStateMetadata`, `RedactedField`, `SafeCopyId`, the mappers); Story 6.5 authored the reviewed wireflow contract `docs/ux/ops-console-wireflows.md` that **gates** 6.7 (epics.md:1382); Story 6.6 shipped the Folder/Workspace pages plus `TenantScopeBanner`, `WorkspaceTrustSummary`, `MetadataOnlyFolderTree`, `TrustMatrix`, `ConsoleErrorPresenter`, `ConsoleStatusText`, `ConsoleEmptyState`, `ConsoleErrorPanel` — **all of which 6.7 composes and must not duplicate**. Story 6.7 implements the wireflow **§3.3 Provider view** and resolves the two "ship in Story 6.7" placeholders that 6.6 left in the Workspace page (`console-page-workspace-provider-pending`) and the Trust Matrix provider-readiness cell.

Decomposed, testable acceptance criteria. Each names what a bUnit/E2E test or a code/structure check must confirm. (UX-DR IDs, C6 state names, disposition labels, and `FieldDisclosure` members are **existing contracts — reference and preserve them verbatim; never redefine, renumber, or fork**, per the wireflow `mutation_rules`.)

1. **Two read-only, route-addressable provider pages exist under the shipped console shell.** New Razor pages under `src/Hexalith.Folders.UI/Components/Pages/`, each: a single `<h1>`; a page-root container `data-testid="console-page-{name}-root"`; a `<PageTitle>`; rendered through `FrontComposerShell` (global Interactive Server render mode, no per-page `@rendermode`); relying on the shell's `<FocusOnNavigate Selector="h1" />` (no competing skip link). Both follow the conventions established by 6.6's pages.
   - `Provider.razor` → `@page "/folders/{FolderId}/provider"`, root `console-page-provider-root` — **folder-scoped Provider readiness view (§3.3)**: provider identity, credential-reference status, readiness disposition, sync, and (when a workspace/operation context is resolved) failure metadata for one folder's provider binding.
   - `ProviderSupport.razor` → `@page "/providers/support"`, root `console-page-provider-support-root` — **tenant-scoped Provider support / capability page (FR57)**: the GitHub vs Forgejo capability-support matrix from the tenant-scoped support-evidence projection.
   - Route constants/helpers added to `tests/Hexalith.Folders.UI.E2E.Tests/Routes/ConsoleRoutes.cs` (the single source of route strings; its header notes "Stories 6.6-6.8 add the diagnostic routes" — extend it). Tests must not hardcode provider route strings elsewhere.

2. **`TenantScopeBanner` renders scope first on both pages (UX-DR4/UX-DR6).** Reuse the shipped `Components/TenantScopeBanner.razor` verbatim (tenant sourced from `FoldersUserContextAccessor`, **never** the `{FolderId}` route — proven by 6.6's test; concern #12). The banner is the first content block on both pages (scope-before-evidence, UX-DR18). **Do not** build a second tenant banner.

3. **The Provider view renders provider identity with a non-secret credential-reference identifier only (UX-DR4/UX-DR27/§3.3; FR52).** Provider identity block shows: provider family/binding (`ProviderBinding.ProviderFamilyRef`, `ProviderBinding.ProviderBindingRef`), capability-profile reference (`ProviderBinding.CapabilityProfileRef`), repository binding (`RepositoryBinding.RepositoryBindingId` + `BindingState`), and the **credential-reference identifier**. The credential-reference is rendered as a **monospace safe-copy identifier via `SafeCopyId`**, explicitly labeled `"reference identifier (not a secret)"` (§3.3 accessibility). When `ProviderBinding.Redaction == ProviderBindingRedaction.Credential_reference_redacted`, the credential-reference field renders through the shipped `RedactedField` (lock icon + "Hidden by tenant policy") — **never** the raw value, and visibly distinct from unknown/missing. **No provider tokens, credential values, or embedded-credential URLs are ever rendered** (epics.md:1408; concern #11/#17).

4. **The Provider view renders readiness as disposition-primary with reason secondary (F-4/§3.3).** The **operator-disposition badge is the primary readiness visual**: feed `ProviderStatusDiagnostics.Disposition` (server-computed `OperatorDispositionLabel`) directly into the shipped `OperatorDispositionBadge` — never a bespoke string, never a promoted technical-state headline. The **readiness reason** is secondary metadata sourced from the operator-sanitized projection: `ProviderStatusDiagnostics.Status` plus `Trust.StaleReasonCode`/`Trust.UnavailableReasonCode` (stable metadata reason codes, never raw provider text). A freshness zone shows `ProviderStatusDiagnostics.Freshness` (`ObservedAt`/`ProjectionWatermark`/`Stale`) with stale/unavailable reason from `DiagnosticTrustEvidence`; stale evidence is never presented as current without a freshness label (UX-DR26). Where the page needs to express a readiness state name as secondary metadata, surface the projection's own `Status`/`Trust.Availability` — **do not** invent a `ProviderReadinessStatus`→disposition mapping (the server `Disposition` is authoritative; the client `DispositionLabelMapper` has no provider-readiness-status overload — do not add one without a parity counterpart).

5. **The Provider view surfaces retryability, remediation category, and failure metadata where the passive read path provides them — and renders honest `Unknown` where it does not (epics.md:1407; §3.3).** Retryability and failure metadata are advisory **display only, never an action**: when an operation context is resolved (`operationId` + `workspaceId`), surface `ProviderOutcome` (`State` `ProviderOutcomeState`, `SanitizedStatusClass` `CanonicalErrorCategory`, `RetryEligibility` (`Eligible`/`ReasonCode`/`AdvisoryOnly`), `RetryAfter`, `ProviderCorrelationReference`). When no operation context is available, the page renders an honest "no recent provider operation" / `Unknown` affordance rather than fabricating a retry/remediation value. **The active readiness *validation* probe (`ValidateProviderReadinessAsync` → POST `/provider-readiness/validations`) is out of scope and must NOT be auto-invoked** — it actively probes the provider (`snapshot_per_task`, can return 429/503), which violates the eventually-consistent read-only browsing model (§1.4, concern #19); do not add a "Validate now" button (an action affordance). Remediation-category / sanitized-reason content beyond the projection's operator-sanitized fields is therefore surfaced where `ProviderStatusDiagnostics`/`ProviderOutcome` carry it and rendered as honest `Unknown`/"not applicable" otherwise — documented as a conscious shape gap (do not paper over it).

6. **The Provider support page reports capability differences explicitly, never inferred (FR57/§3.3).** `ProviderSupport.razor` renders the tenant-scoped `GetProviderSupportEvidenceAsync` projection as a capability-support matrix: each `ProviderSupportEvidence` row = `CapabilityProfileRef` × `Capability` (`ProviderCapabilityName`) × `SupportState` (`ProviderCapabilityState`: `supported`/`unsupported`/`temporarily_unavailable`). Capability differences between providers (GitHub vs Forgejo, FR57) are read directly from this evidence and **never inferred from a failed operation** (§3.3, NFR Integration; project-context "GitHub and Forgejo support must be capability-tested … Do not assume API compatibility"). Pagination uses the `PaginationMetadata` cursor from the DTO; the page never client-pre-filters to hide rows (authorization already ran server-side). A freshness zone shows the list's `Freshness`.

7. **Every status indicator is non-color-only and every identifier is monospace safe-copy (UX-DR14, UX-DR27).** Disposition, readiness, capability-support, binding-state, redaction, and freshness indicators each carry text + icon/shape + semantic color + accessible label (inherited from the shipped components; applied to any new status text). Capability `SupportState`, repository `BindingState`, and provider readiness must render through a badge/labelled affordance (icon/shape + color + text), never a bare color span (the 6.6 review fixed exactly this class of bug). Safe identifiers (provider binding ref, provider family ref, capability-profile ref, repository binding id, credential-reference id, correlation id, operation id) render in monospace via `SafeCopyId` (read-only clipboard affordance only) — never a raw editable field; no credential/token values.

8. **The four taxonomy distinctions and the shared visible status taxonomy are preserved (wireflow §2).** The pages reconcile the four vocabularies (disposition / technical-state / field-disclosure / access) and never invent UI-only state names. The four distinctions hold visibly + semantically: `stale`/`delayed` ≠ `unavailable` (provider projection old vs read model down); `redacted` ≠ `unknown` ≠ `missing` (credential-reference redaction via `RedactedField` only; representing redaction as missing/silent-truncation is a correctness bug); `denied` ≠ `inaccessible` (safe-denial-no-existence vs provider-unreachable-but-known); disposition (primary) ≠ readiness/technical state (secondary). A `ProviderOutcomeState.Unknown_provider_outcome` or `RepositoryBindingBindingState.Unknown_provider_outcome` surfaces the `awaiting-human` disposition treatment — never a neutral "Unknown" with no badge.

9. **Empty, error, and safe-denial states are rendered without leakage (§3.8/§3.9), reusing the shipped infra.** Reuse `ConsoleEmptyState`, `ConsoleErrorPanel`, and the `ConsoleErrorPresenter`/`ConsoleStatusText` services. Empty distinguishes the four §3.8 reasons (no matches / insufficient filter scope / unavailable read model / denied access) without confirming unauthorized existence (UX-DR20/21). A thrown `HexalithFoldersApiException` (the SDK surfaces denials as HTTP status + canonical Problem Details, **not** as data fields) is caught and rendered through the existing safe-denial path; `not_found` and `*_denied` are **never** expanded into a provider/repository-existence oracle (epics.md:1408 "unauthorized repository existence … never displayed"). The provider error categories already mapped by `ConsoleStatusText` (incl. the live `FolderAuthorizationDenialMapper` denial tokens 6.6 added) are reused — **add only the genuinely new provider tokens** if the provider endpoints surface a category not already covered, each with existence-neutral safe copy.

10. **The pages are strictly read-only and the command-suppression guards stay green.** No `<form>`, `<FluentInputForm>`, mutation dialog, `data-fc-command`, or `data-fc-mutation` on any 6.7 page (the 5-selector guard mirrored across the existing page tests). **No mutating SDK method is called** — specifically not `ConfigureProviderBindingAsync` (returns `AcceptedCommand`, takes `idempotency_Key`) and **not** `ValidateProviderReadinessAsync` (active probe; see AC #5). No `[Command]` projection is defined and `AddHexalithEventStore` is **not** called, so `NavigationContractTests.Console_DoesNotRegisterAnyDomainCommandManifest` stays green. Forms are limited to read-only navigation and the support-page cursor/paging within the server-accepted vocabulary (UX-DR23; filter vocabulary is rejection-only today — C4 §7). No credential reveal, token, embedded-credential URL, file content, raw diff, repair/retry/validate/configure action, or unrestricted filesystem browse.

11. **The 6.6 provider link-out placeholders are resolved (UX-DR19, connected evidence).** Replace the Workspace page's non-link "Provider readiness (available in Story 6.7)" pending affordance (`console-page-workspace-provider-pending`) and the Trust Matrix provider-readiness cell's pending copy with a real route link to `/folders/{FolderId}/provider`. Add a link from `FolderDetail.razor`'s "Provider binding" row to the provider page. Add a hand-authored primary nav entry for the tenant-scoped provider-support page (these are custom pages, not generated projection grids — wireflow §1.2 "custom … need an explicit nav entry"); keep `Console_DoesNotRegisterAnyDomainCommandManifest` green (the framework nav is manifest-driven; use a hand-authored link as 6.6 did for `/folders`).

12. **bUnit + E2E coverage proves rendering and the read-only/totality invariants; the SDK generated tree is untouched.** New bUnit tests in `tests/Hexalith.Folders.UI.Tests` cover each new component and page (using `BadgeRenderingFixture`, with `IClient` substituted via NSubstitute and `IUserContextAccessor` provided per the `ShellCompositionTests`/6.6 pattern), including a `Renders_NoMutationAffordances` fact per page and a test proving `ValidateProviderReadinessAsync`/`ConfigureProviderBindingAsync` are **never** invoked. Any new switch over an SDK enum (`ProviderCapabilityName`, `ProviderCapabilityState`, `ProviderFailureBehavior`, `ProviderReadinessStatus`, `ProviderReadinessOperatorRemediationCategory`, `ProviderReadinessConsumerRetryHint`, `RepositoryBindingBindingState`, `ProviderBindingRedaction`, `SensitiveMetadataTier`, `ProviderOutcomeState`, `CanonicalErrorCategory` consumers) is a **total** `[Theory]` over `Enum.GetValues<T>()` that throws on unknown — never a silent default. A new E2E smoke test for the provider route follows `FolderRouteSmokeTests`/`ConsoleSmokeTests`. `git diff --stat src/Hexalith.Folders.Client/Generated/` is empty (no generated-client edits) and `Directory.Packages.props`/the OpenAPI spine are untouched.

## Tasks / Subtasks

- [x] **Task 1 — Read the reviewed contract and confirm prerequisites** (AC: all)
  - [x] Read `docs/ux/ops-console-wireflows.md` §1 (esp §1.2 navigation, §1.4 as-built data path), §2 (taxonomy + distinctions), **§3.3 Provider view**, §3.6–§3.9 (redaction/loading/empty/error), §4.1 (accessibility), §7 (pending inputs). This is the gate contract — preserve its IDs/names verbatim.
  - [x] Re-read the shipped components/services you will compose: `TenantScopeBanner`, `OperatorDispositionBadge`, `TechnicalStateMetadata`, `RedactedField`, `SafeCopyId`, `ConsoleEmptyState`, `ConsoleErrorPanel`, `DispositionLabelMapper`, `FieldDisclosure`, `RedactionDisclosureMapper`, `ConsoleErrorPresenter`, `ConsoleStatusText`, `TrustDimensionDeriver`, `FoldersConsoleIcons`, `FoldersUserContextAccessor`, `Workspace.razor(.cs)` (the §3.3 link-out placeholder + Trust Matrix provider cell), `FolderDetail.razor` (provider-binding row).
  - [x] Confirm the SDK read methods + DTO fields in the §"Data path" Dev Note against `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs` (do not trust memory — DTO field names are exact). Confirm `ValidateProviderReadinessAsync` and `ConfigureProviderBindingAsync` are the methods you must **not** call.

- [x] **Task 2 — Add provider status-text / capability mappers** (AC: #4, #6, #7, #8)
  - [x] Extend `Services/ConsoleStatusText.cs` (or a focused new `ProviderStatusText` static helper) with **total** mappers for the new provider enums: `ProviderCapabilityState` → label + `BadgeSlot`; `RepositoryBindingBindingState` → label + slot (`unknown_provider_outcome`/`reconciliation_required` → Warning, `failed` → Danger, `bound` → Success); `ProviderReadinessStatus` → label + slot; `ProviderCapabilityName` → display label; `SensitiveMetadataTier` → label. Each throws `ArgumentOutOfRangeException` on unknown (never a silent default), mirroring the existing `ConsoleStatusText` lock/cleanup pattern.
  - [x] Unit-test each mapper as a **total** `[Theory]` over `Enum.GetValues<T>()`. Force `en-US` for label assertions.

- [x] **Task 3 — Build the Provider readiness view model + assembly** (AC: #3, #4, #5, #8)
  - [x] Add a private UI-assembly view-model record (e.g. `Components/Models/ProviderReadinessModel.cs`) — **UI assembly only, not Contracts, not a registered facade/`IQueryService`** (the as-built direct-SDK path; the HIGH 6.5/6.6 lesson). Assemble it from the SDK calls in the §"Data path" Dev Note: `GetProviderStatusDiagnosticsAsync(folderId)` (primary: disposition + status + trust + freshness + correlation + provider-binding reference), `GetFolderLifecycleStatusAsync(folderId)` (to obtain `ProviderBindingRef` + `RepositoryBindingId`), `GetProviderBindingAsync(providerBindingRef)` (identity + credential-reference redaction), `GetRepositoryBindingAsync(folderId, repositoryBindingId)` (binding state + sensitive-metadata tier).
  - [x] Disposition primary: `ProviderStatusDiagnostics.Disposition` → `OperatorDispositionBadge` directly. Freshness driven by `Freshness.Stale` + `Trust.StaleReasonCode`/`UnavailableReasonCode` (never a hardcoded C2/C5 numeric lag — §7). Credential-reference disclosure: `RedactionDisclosureMapper.FromDiagnosticClassification(...)` for diagnostics fields, and the `ProviderBinding.Redaction == Credential_reference_redacted` marker → `FieldDisclosure.Redacted` for the credential-reference id.

- [x] **Task 4 — Build the `Provider.razor` page (Provider view §3.3)** (AC: #1, #2, #3, #4, #5, #7, #8, #9, #10)
  - [x] `Components/Pages/Provider.razor` `@page "/folders/{FolderId}/provider"`. Order per §3.3: `TenantScopeBanner` → provider identity block (provider family/binding + capability-profile + repository binding + credential-reference id via `SafeCopyId`, labeled "reference identifier (not a secret)") → readiness zone (`OperatorDispositionBadge` primary + reason category secondary + freshness) → capability/sync metadata (sync via `GetSyncStatusDiagnosticsAsync(folderId, workspaceId)` **only when a workspace context is resolved** — same workspace-scoping discipline as the 6.6 folder tree; do not fabricate a workspaceId) → failure metadata (`ProviderOutcome`, advisory-only, only when an operationId context exists). testid `console-page-provider-root`; single `<h1>`; `<PageTitle>`. Empty/error/denied via `ConsoleEmptyState`/`ConsoleErrorPanel`/`ConsoleErrorPresenter`.
  - [x] Per page load generate one `x_Correlation_Id`, pass it to every SDK call, surface it as a `SafeCopyId`; pass `ReadConsistencyClass.Eventually_consistent` (concern #19). Do **not** call `ValidateProviderReadinessAsync` or `ConfigureProviderBindingAsync`.
  - [x] bUnit: identity/readiness/credential-redaction render correctly; `Renders_NoMutationAffordances`; denied path renders safe-denial without provider/repo existence leak; a fact asserting the validate/configure methods are never invoked on the substituted `IClient`.

- [x] **Task 5 — Build the `ProviderSupport.razor` page (capability matrix, FR57)** (AC: #1, #2, #6, #7, #9, #10)
  - [x] `Components/Pages/ProviderSupport.razor` `@page "/providers/support"`. Order: `TenantScopeBanner` → single `<h1>` → capability-support matrix from `GetProviderSupportEvidenceAsync(correlationId, freshness, cursor, limit)`: rows of `CapabilityProfileRef` × `Capability` × `SupportState` rendered through the Task-2 mappers (badge, non-color-only) → pagination footer from `PaginationMetadata` → freshness zone. Capability differences read directly from evidence, never inferred. testid `console-page-provider-support-root`. Empty/error/denied via the shipped infra.
  - [x] bUnit + E2E smoke (new `ConsoleRoutes` entry; follow `FolderRouteSmokeTests`).

- [x] **Task 6 — Wire navigation + resolve 6.6 placeholders** (AC: #1, #11)
  - [x] Add `ConsoleRoutes.Provider(folderId)` and `ConsoleRoutes.ProviderSupport` to `tests/Hexalith.Folders.UI.E2E.Tests/Routes/ConsoleRoutes.cs`.
  - [x] In `Workspace.razor`/`Workspace.razor.cs`: replace the `console-page-workspace-provider-pending` non-link affordance with a real link to `/folders/{FolderId}/provider`; point the Trust Matrix provider-readiness cell's evidence link at the provider page (or keep the on-page anchor and add the route link — preserve the UX-DR19 connected-evidence requirement).
  - [x] In `FolderDetail.razor`: link the "Provider binding" row to `/folders/{FolderId}/provider`.
  - [x] Add a hand-authored primary nav link to `/providers/support` (Home or the shell nav), consistent with how 6.6 added `/folders`. Keep `Console_DoesNotRegisterAnyDomainCommandManifest` green.

- [x] **Task 7 — Tests, guards, and verification** (AC: #10, #12)
  - [x] Per-page `Renders_NoMutationAffordances` (5-selector guard). Confirm `Console_DoesNotRegisterAnyDomainCommandManifest` and `ScaffoldContractTests.ProjectReferencesFollowAllowedDependencyDirection` (UI references Client + FrontComposer.Shell only) stay green.
  - [x] Total `[Theory]` enum-coverage tests for every new SDK-enum switch; never a silent default (drift sentinel).
  - [x] `git diff --stat src/Hexalith.Folders.Client/Generated/` empty; no `Directory.Packages.props` / OpenAPI-spine / generated edits; no new `<PackageReference>` unless justified.
  - [x] Build/test with the WSL-accessible Windows SDK (`/mnt/c/Program Files/dotnet/dotnet.exe`); run `dotnet test tests/Hexalith.Folders.UI.Tests`. Record results and distinguish any pre-existing baseline reds (see Dev Notes) from regressions.

- [x] **Task 8 — Self-review against the wireflow contract and scope** (AC: all)
  - [x] Confirm IDs/names preserved verbatim (no UX-DR / C6 / disposition / `FieldDisclosure` renumber-or-fork); the four §2 distinctions hold; the §7 pending inputs are not invented; the active validate probe was not wired; no audit/incident/perf-gate scope crept in (those are 6.8/6.9/6.10/6.11). Record the self-check in the Dev Agent Record.

## Dev Notes

### Scope boundaries (read first)

- **In scope:** the **Provider view (§3.3)** of the operations console — two read-only pages (`Provider.razor` folder-scoped readiness, `ProviderSupport.razor` tenant-scoped capability matrix) under `src/Hexalith.Folders.UI/`, composing the shipped 6.3/6.4/6.6 components and reading projections through the SDK `IClient`. Surfaced metadata (epics.md:1407): provider binding, credential-reference identifier/status, readiness reason, retryability, remediation category, capability, sync, failure metadata. Resolving the 6.6 provider link-out placeholders.
- **OUT of scope (do NOT build here — owned by siblings):**
  - **Folder/Workspace diagnostic pages → Story 6.6 (done).** Reuse its components; do not rebuild them.
  - **Audit-trail / operation-timeline pages → Story 6.8** (FR53/FR54/FR56; epics.md:1410-1421). Link to audit by route only.
  - **Incident-mode `/_admin/incident-stream` + degraded-mode banner → Story 6.9** (epics.md:1423-1434).
  - **Performance p95/p99 gate + 400ms skeleton + 2s cancel UX → Story 6.10** (epics.md:1436-1447). Build pages that *can* meet the budget; a simple loading indicator is fine — do not implement the timed skeleton/cancel contract or the measured gate.
  - **Formal no-mutation + WCAG 2.2 AA verification sweep → Story 6.11** (epics.md:1449-1462). Satisfy read-only + a11y design on your own pages; the verification matrix is 6.11's.
  - **Editing the planning artifacts, the wireflow notes, the OpenAPI spine, or the generated client.** All read-only inputs.
- **Negative-scope guard:** if you find yourself calling `ConfigureProviderBindingAsync` (returns `AcceptedCommand`, takes `idempotency_Key`) or **`ValidateProviderReadinessAsync`** (active probe; AC #5), defining a `[Command]` projection, calling `AddHexalithEventStore`, editing `src/Hexalith.Folders.Client/Generated/`, adding a project reference beyond Client + FrontComposer.Shell, adding a projection DTO to `Hexalith.Folders.Contracts`, rendering a credential value/token/embedded-credential URL, confirming unauthorized repository existence, or building an audit/incident/perf page — **stop**; none are in 6.7's surface.

### The reviewed contract and prerequisites (all satisfied)

Story 6.7 is unblocked: `docs/ux/ops-console-wireflows.md` exists and was reviewed (Story 6.5, done), and the Folder/Workspace pages + shared components shipped (Story 6.6, done). What each upstream gives you:
- **6.1** — projection/diagnostics endpoints + DTOs (the provider data source).
- **6.2** — the FrontComposer-hosted shell, `FoldersUserContextAccessor`, the `AddFoldersClient` + `BearerTokenDelegatingHandler` data path, the page conventions (`console-page-{name}-root`, single `<h1>`, `FocusOnNavigate`), the bUnit fixture, the command-suppression guards.
- **6.3** — `OperatorDispositionBadge`, `TechnicalStateMetadata`, `DispositionLabelMapper` (F-4 canonical mapper, parity-tested).
- **6.4** — `RedactedField`, `FieldDisclosure`, `RedactionDisclosureMapper`, `FoldersConsoleIcons.LockClosed16()` (F-5).
- **6.6** — `TenantScopeBanner`, `WorkspaceTrustSummary`, `MetadataOnlyFolderTree`, `TrustMatrix`, `SafeCopyId`, `ConsoleEmptyState`, `ConsoleErrorPanel`, `ConsoleErrorPresenter`, `ConsoleStatusText`, `TrustDimensionDeriver`, the page/route conventions, and the provider link-out placeholders this story resolves. **§3.3 is your page spec.**

### What already exists — compose, do not reinvent

**Components** (`src/Hexalith.Folders.UI/Components/`): `TenantScopeBanner`, `OperatorDispositionBadge` (takes a *disposition*, not a state), `TechnicalStateMetadata`, `RedactedField` (`Disclosure`/`Value`/`Monospace`; renders value only when `Visible`; lock icon when `Redacted`), `SafeCopyId` (monospace value + read-only clipboard button that does not trip the mutation guard — use for ALL identifiers), `ConsoleEmptyState` (four §3.8 reasons via `EmptyStateReason`), `ConsoleErrorPanel`.

**Services** (`src/Hexalith.Folders.UI/Services/`) — all `static`, total over their enum, **throw `ArgumentOutOfRangeException` on unknown**:
- `DispositionLabelMapper` — `ResolveDisposition`/`ResolveSlot`/`ResolveLabel`/`ResolveTechnicalStateLabel`. Has **no** provider-readiness-status overload; the server `ProviderStatusDiagnostics.Disposition` is authoritative — do not add a client-only provider→disposition mapping (it would have no parity guard).
- `RedactionDisclosureMapper` — `FromDiagnosticClassification(DiagnosticFieldClassification, bool hasValue)` (Forbidden→Redacted; Consumer_safe/Operator_sanitized→Visible if hasValue else Missing). Use for diagnostics `FieldClassifications`. **Deliberately does NOT map `ProjectionAvailability`** (C5 reference-pending) — do not extend it.
- `ConsoleErrorPresenter` / `ConsoleStatusText` — the safe-denial path + canonical error-category copy (incl. the live `FolderAuthorizationDenialMapper` tokens 6.6 added) + lock/cleanup status text. Extend `ConsoleStatusText` for the new provider enums (Task 2); reuse the error presenter as-is.
- `TrustDimensionDeriver.FromProviderOutcome(ProviderOutcomeState?)` — already maps the provider-readiness Trust Matrix dimension. Reuse it; the provider page is the evidence target the 6.6 Trust Matrix cell links to.
- `FoldersUserContextAccessor` — the authenticated-context bridge (`tenant_id`→TenantId). Source the Tenant Scope Banner's tenant from here, never the route.

**Page conventions:** global Interactive Server render mode; pages carry no `@rendermode`; one `<h1>` + no competing skip link; `_Imports.razor` does **not** include `Hexalith.Folders.Client.Generated` or `Hexalith.Folders.UI.Services` — add explicit `@using` per page (6.6 pages do this). New pages/components are `public`; mapper helpers may be `internal`.

### Data path — the most important section

**The console reads through the generated NSwag client `IClient`, injected directly** (`AddFoldersClient(...)` + `BearerTokenDelegatingHandler` wired in `CompositionRoot.cs:150-158`). **Bind the generated `Hexalith.Folders.Client.Generated` DTOs — NOT server-side `*QueryResult` records.** All read methods take `string x_Correlation_Id, ReadConsistencyClass? x_Hexalith_Freshness` and a `CancellationToken` overload; add `ConfigureAwait(false)` (CA2007 is warnings-as-error in the UI project).

> ⚠ **As-built path (HIGH in 6.5/6.6):** direct SDK `IClient`; there is **no `FoldersClientFacade`/`IQueryService`** in `src/Hexalith.Folders.UI`. Any aggregator is a private UI-assembly record — not a registered service, not in Contracts.

**Provider read surface (all GET, eventually-consistent browsing — pass the eventually-consistent `ReadConsistencyClass`):**

| Method (IClient) | Path args | Response | Role in 6.7 |
|---|---|---|---|
| `GetProviderStatusDiagnosticsAsync` | folderId | `ProviderStatusDiagnostics : DiagnosticBase` (+ `ProviderBindingReference` `RedactableDiagnosticIdentifier`, `ProviderCorrelationReference`) | **PRIMARY readiness source** (ops-console diagnostics, the analog of 6.6's diagnostics path). `Disposition` (`OperatorDispositionLabel`) → `OperatorDispositionBadge`; `Status` (string) + `Trust` (`Availability`/`StaleReasonCode`/`UnavailableReasonCode`) → readiness reason (secondary) + freshness; `FieldClassifications` → `RedactionDisclosureMapper.FromDiagnosticClassification`; `Freshness` |
| `GetFolderLifecycleStatusAsync` | folderId | `FolderLifecycleStatus` | supplies `ProviderBindingRef` + `RepositoryBindingId` the provider/repo reads need |
| `GetProviderBindingAsync` | **providerBindingRef** | `ProviderBinding` (`ProviderBindingRef`, `ProviderFamilyRef`, `CapabilityProfileRef`, `Redaction` `ProviderBindingRedaction.Credential_reference_redacted`, `Freshness`) | provider identity + credential-reference status. `Redaction == Credential_reference_redacted` → render credential-ref through `RedactedField`. The ref values are **non-secret identifiers** (monospace `SafeCopyId`); there is no token/secret on this DTO |
| `GetRepositoryBindingAsync` | folderId, **repositoryBindingId** | `RepositoryBinding` (`BindingState` `RepositoryBindingBindingState` requested/bound/failed/unknown_provider_outcome/reconciliation_required, `SensitiveMetadataTier`, `ProviderBindingRef`, `Freshness`) | repository binding status. **No list endpoint — single GET only**; `repositoryBindingId` from `FolderLifecycleStatus.RepositoryBindingId` |
| `GetProviderSupportEvidenceAsync` | **(none)**; cursor, limit | `ProviderSupportEvidenceList` (`Items` `ProviderSupportEvidence`{`CapabilityProfileRef`, `Capability` `ProviderCapabilityName`, `SupportState` `ProviderCapabilityState`}, `Page` `PaginationMetadata`, `Freshness`) | **capability differences (FR57)** — tenant-scoped, paginated. The `ProviderSupport.razor` primary source. Explicit support evidence, never inferred |
| `GetReadinessDiagnosticsAsync` | **(none)** | `ReadinessDiagnostics : DiagnosticBase` (+ `ProviderSummaryReference`/`FolderSummaryReference`/`WorkspaceSummaryReference` `RedactableDiagnosticIdentifier`) | tenant-scoped readiness rollup; optional context/summary reference. **No folder/workspace path arg** — do not assume one |
| `GetSyncStatusDiagnosticsAsync` | folderId, **workspaceId** | `SyncStatusDiagnostics : DiagnosticBase` (`AcceptedCommandState` [C5/C6 reference-pending], `ProjectedState` `LifecycleState`, `ProviderOutcomeState`) | sync metadata — **workspace-scoped**; show only when a workspace context is resolved (do not fabricate a workspaceId) |
| `GetProviderOutcomeAsync` | folderId, workspaceId, **operationId** | `ProviderOutcome` (`State` `ProviderOutcomeState`, `SanitizedStatusClass` `CanonicalErrorCategory`, `ProviderCorrelationReference`, `RetryEligibility` (`Eligible`/`ReasonCode`/`AdvisoryOnly`), `RetryAfter`, `Freshness`) | **failure metadata + retryability** — needs an operationId; **advisory display only, never a retry button**. Show when an operation context exists; else honest `Unknown` |

**Do NOT call (mutation / active probe):**
- `ConfigureProviderBindingAsync` → `AcceptedCommand`, takes `idempotency_Key` — a mutation.
- `ValidateProviderReadinessAsync` → POST `/api/v1/provider-readiness/validations`, `x-hexalith-read-consistency: snapshot_per_task`, `freshnessBehavior: readiness-evidence-generated-for-request`, can return `429 ProviderRateLimited`/`503 ProviderUnavailable`. It **actively probes the provider** to generate fresh evidence for the request — incompatible with the read-only, eventually-consistent browsing model (§1.4/concern #19). Do not auto-invoke on page load and do not add a "Validate" button. (The generated client also collapses its discriminated `ProviderReadiness` response — operator vs consumer — to `ProviderReadinessConsumer`, so the rich operator fields are not reliably reachable through it anyway.)

**Honest shape gaps to handle (do not paper over — mirror 6.6's discipline):**
- **Operator-rich readiness fields (`safeReasonCode`, `safeRemediationCode`, `remediationCategory`, `retryAfterSeconds`) live on `ProviderReadinessOperator`, which is only returned by the active validations probe** (out of scope, AC #5). On the passive read path, the **readiness reason** is `ProviderStatusDiagnostics.Status` + operator-sanitized `Trust` reason codes; **retryability** and a sanitized failure category come from `ProviderOutcome` when an operation context exists. Where a field has no passive source, render an honest `Unknown`/"not applicable" — never fabricate a remediation code. Document this as a conscious gap in the Completion Notes.
- **Correlation is mostly the request echo.** `ProviderStatusDiagnostics.ProviderCorrelationReference` and `ProviderOutcome.ProviderCorrelationReference` carry one; otherwise surface the `x_Correlation_Id` you sent. Generate one per page load and reuse it across that page's calls.
- **Denials surface as a thrown `HexalithFoldersApiException`** (HTTP status + canonical Problem Details), not as data fields — catch and route through `ConsoleErrorPresenter` (existence-neutral). `not_found`/`*_denied` are never expanded into a provider/repository-existence oracle (epics.md:1408).
- **`RedactableDiagnosticIdentifier`** wraps `Value`/`Classification`/`Redaction` — render via `RedactedField`, never the raw `.Value` when redacted.

### Status taxonomy + the four distinctions to preserve (wireflow §2/§2.3)

Four vocabularies, never collapsed, no UI-only names invented: operator disposition (primary) · technical/readiness state (secondary) · field disclosure (`FieldDisclosure`/`RedactedField`) · access/availability. Preserve verbatim: **`stale`/`delayed` ≠ `unavailable`**; **`redacted` ≠ `unknown` ≠ `missing`** (credential-reference redaction via `RedactedField` only); **`denied` ≠ `inaccessible`** (safe-denial-no-existence vs provider-unreachable-but-known — §2.3 line 251/275); **disposition (primary) ≠ readiness/technical state (secondary)**. `unknown_provider_outcome` (on `ProviderOutcomeState` and `RepositoryBindingBindingState`) → `awaiting-human` treatment, never a neutral "Unknown" with no badge.

### Pending / deferred inputs — must NOT invent or hardcode (wireflow §7)

- **C2 status-freshness target / C5 projection-freshness** — TBD/reference-pending; render a freshness zone + stale/delayed label, drive staleness from `Freshness.Stale` / `Trust` reason codes. Do **not** hardcode "fresh within N seconds" or read `DiagnosticTrustEvidence.FreshnessAgeMilliseconds` / `SyncStatusDiagnostics.AcceptedCommandState` as frozen contract (both flagged reference-pending in the generated client).
- **C4 metadata-filter vocabulary** — rejection-only today; the support page exposes only cursor paging the server accepts; don't imply unsupported filters work.
- **`ProjectionAvailability.redacted`/`.unknown`** — C5 reference-pending; `RedactionDisclosureMapper` deliberately does not map them — do not extend it.
- **French localization** — deferred to 6.11; English MVP. Keep label-producing methods `string`-returning (6.3/6.4/6.6 precedent). Do not add FR strings; do not edit `deferred-work.md`.

### Accessibility (build defensively; 6.11 verifies)

Non-color-only status everywhere (UX-DR14) — capability `SupportState`, `BindingState`, readiness, and redaction each carry text + icon/shape + color + accessible label. Keyboard access for navigation/paging/tables (UX-DR30); visible + reading-order focus (UX-DR24); the credential-reference identifier labeled "reference identifier (not a secret)" (§3.3); zoom resilience 125/150/200% with dense identifiers not clipping (safe truncation + full value via `SafeCopyId`, UX-DR31); responsive desktop-first with tablet/mobile fallback (UX-DR28/29); redacted-vs-unknown-vs-missing visibly + semantically distinct via `RedactedField`; screen-reader-meaningful labels for redacted/inaccessible/unavailable/failed/delayed/ready states; no motion dependency.

### Performance (build to meet; 6.10 owns the gate)

Status/diagnostics queries target **500ms p95** (prd.md:715); provider pages are "primary diagnostic flows" (architecture F-7: p95<1.5s, p99<3s). The support-evidence list is paginated — keep page size bounded. A simple loading indicator is fine; don't implement the 400ms-skeleton / 2s-cancel contract or the measured perf gate (6.10).

### Test plan

- **bUnit** (`tests/Hexalith.Folders.UI.Tests`): reuse `BadgeRenderingFixture`; register an NSubstitute `IClient` + an `IUserContextAccessor` (follow `ShellCompositionTests` / the 6.6 page tests). Per page: single `<h1>`, page-root testid, the 5-selector `Renders_NoMutationAffordances` guard, and a fact asserting `ValidateProviderReadinessAsync`/`ConfigureProviderBindingAsync` were never called (use `Received.InOrder`/`DidNotReceive`). Total `[Theory]` over each consumed SDK enum. Force `en-US` for aria-label assertions. Cover: credential-reference redacted vs visible vs unknown; binding-state badge (incl. `unknown_provider_outcome`); capability-support matrix rows; safe-denial without existence leak; the honest-Unknown failure/retryability path when no operation context.
- **DI/contract**: `Console_DoesNotRegisterAnyDomainCommandManifest` and `ScaffoldContractTests.ProjectReferencesFollowAllowedDependencyDirection` stay green.
- **E2E** (`tests/Hexalith.Folders.UI.E2E.Tests`): a provider-route smoke test following `FolderRouteSmokeTests` (new `ConsoleRoutes` entries; assert 200-399 + page-root visible + `<h1>`).
- **Build/test**: `/mnt/c/Program\ Files/dotnet/dotnet.exe` (WSL-native SDK fails the global.json 10.0.302 pin). Root-level submodules only — never `--recursive`.

### Build/test environment + known pre-existing reds (not regressions)

Verify against the prior-story baseline, not from zero. The 6.6 UI lane finished green at **298/298**. Known reds live **outside** the UI lane (per 6.6): `BranchRefPolicyEndpointTests.GetBranchRefPolicyShouldUseSafeDenialEnvelopeForTenantMismatch`; six Contracts OpenAPI/Safety reds; three `Hexalith.Folders.Testing.Tests` baseline reds (`ProjectReferencesFollowAllowedDependencyDirection` vs IntegrationTests, `DeferredArtifactAreasCarryMachineCheckableOwnershipNotes`, `SolutionContainsOnlyCanonicalBuildableProjects`). If these are red on a clean baseline they are not 6.7 regressions — confirm and note, don't "fix" them under this story.

### Review-cycle lessons to avoid repeating (from 6.5/6.6)

1. **As-built data path:** direct `AddFoldersClient` + `BearerTokenDelegatingHandler`; no `FoldersClientFacade`/`IQueryService`.
2. **Server-computed disposition is the primary visual** — feed `ProviderStatusDiagnostics.Disposition` into the badge; don't leave it dead/unused and fall back to a client derivation (6.6 HIGH).
3. **Safe-copy is load-bearing (UX-DR27):** every identifier through `SafeCopyId`, not a raw `<code>` or editable field.
4. **Error map must cover the *live* denial vocabulary** — reuse the `FolderAuthorizationDenialMapper` tokens `ConsoleStatusText` already maps; add only genuinely new provider tokens with existence-neutral copy.
5. **Totality sentinels:** every SDK-enum switch is a total theory that throws on unknown — never a silent default.
6. **`aria-label` never silent; color/icon never the only signal.** Status (incl. capability-support, binding-state) renders through a badge, not a bare colored span.
7. **Terminology:** "Blazor Web App host using Interactive Server rendering" — do not revert to "Blazor Server".
8. **File List fidelity:** list every shipped file incl. E2E routes/tests and the modified 6.6 files (Workspace/FolderDetail/Home).

### Project Structure Notes

- New pages land under `src/Hexalith.Folders.UI/Components/Pages/`; new components/models under `Components/` (`Components/Models/` for view-model records) and `Services/` — matching the 6.6 on-disk convention (`Components/`-rooted Blazor Web App layout), **not** the flat `Pages/` the architecture diagram draws (architecture.md:1173-1202).
- `Hexalith.Folders.UI` references **only** `Hexalith.Folders.Client` + `Hexalith.FrontComposer.Shell` (enforced by `ScaffoldContractTests`). Do **not** add projection DTOs to `Hexalith.Folders.Contracts`; the provider view-model is a private UI-assembly record.
- Route constants are centralized in `tests/Hexalith.Folders.UI.E2E.Tests/Routes/ConsoleRoutes.cs` — add `Provider(folderId)` + `ProviderSupport`; tests must not hardcode route strings elsewhere.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-6.7` (lines 1397-1408) — story statement + AC]; [Epic 6 scope + console guardrails (496-505, 1305-1311); siblings 6.6/6.8-6.11 (1384-1462); FR57 provider support evidence (468)]
- [Source: `docs/ux/ops-console-wireflows.md` §1.2 navigation incl. provider row (106-131), §1.4 data path (158-186), §2 taxonomy + §2.3 distinctions (212-279), **§3.3 Provider view (353-379)**, §3.6-§3.9 redaction/loading/empty/error (440-536), §4/§4.1 accessibility (540-598), §7 pending inputs (765-776)]
- [Source: `_bmad-output/planning-artifacts/architecture.md` F-1..F-7 (545-551); C6 dispositions + mapper (218-279); concern #11 read-only/metadata-only console (110), #12 tenant provenance (111), #17 sensitive metadata (116), #19 read consistency (118); GitHub/Forgejo capability-tested boundary; FR52 mapping (1364); UI source tree (1173-1202)]
- [Source: `_bmad-output/planning-artifacts/ux-design-specification.md` UX-DR table (103-140; line 105 preserve-IDs mandate) — UX-DR4 scope-visible (112), UX-DR9 Trust Matrix provider dimension (117), UX-DR11 read-only boundary (119), UX-DR14 non-color-only (226), UX-DR18 workspace sections incl. provider readiness (126), UX-DR25 loading labels (133), UX-DR27 safe-copy identifiers incl. credential reference (135); provider readiness in core experience (59, 208-216, 282, 312)]
- [Source: `_bmad-output/planning-artifacts/prd.md` FR52-FR57 (677-682), FR57 provider support evidence; status-query 500ms p95 (715); metadata-only/no-secrets (86, 691, 750); read consistency (118); sensitive-metadata classification (751-752)]
- [Source: `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs` — read methods (`GetProviderStatusDiagnosticsAsync` 1259, `GetProviderBindingAsync` 205, `GetRepositoryBindingAsync` 313, `GetProviderSupportEvidenceAsync` 246, `GetReadinessDiagnosticsAsync` 1146, `GetSyncStatusDiagnosticsAsync` 1287, `GetProviderOutcomeAsync` 963, `GetFolderLifecycleStatusAsync` 65); DTOs `ProviderStatusDiagnostics` (12008), `ProviderBinding` (10070), `RepositoryBinding` (10455), `ProviderSupportEvidenceList`/`ProviderSupportEvidence` (10322/10338), `ReadinessDiagnostics` (11956), `SyncStatusDiagnostics` (12020), `ProviderOutcome` (11411), `ProviderCapabilityEvidence` (10180), `DiagnosticBase`/`DiagnosticTrustEvidence` (11930/11908), `RedactableDiagnosticIdentifier` (11576); enums `ProviderCapabilityName` (10105), `ProviderCapabilityState` (10135), `ProviderFailureBehavior` (10153), `ProviderReadinessStatus` (10214), `ProviderReadinessOperatorRemediationCategory` (12745), `ProviderReadinessConsumerRetryHint` (12721), `RepositoryBindingBindingState` (12805), `ProviderBindingRedaction` (12703), `SensitiveMetadataTier` (9872), `ProviderOutcomeState` (11440), `DiagnosticFieldClassification` (11808), `ProjectionAvailability` (11826); `ConfigureProviderBindingAsync`/`ValidateProviderReadinessAsync` are the do-NOT-call methods (183/225); `HexalithFoldersApiException`]
- [Source: OpenAPI spine `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` — provider-bindings (510), provider-readiness/validations (662, active probe: snapshot_per_task, 429/503), provider-readiness/support-evidence (748), repository-bindings (915, 1022), ops-console/provider-status-diagnostics (4670), ProviderReadiness operator discriminator (7617)]
- [Source: `src/Hexalith.Folders.UI/CompositionRoot.cs:150-158` (AddFoldersClient + BearerTokenDelegatingHandler), `:88-91` (FoldersUserContextAccessor Services.Replace), `:65-67` (FrontComposer quickstart, no AddHexalithDomain)]
- [Source: `src/Hexalith.Folders.UI/Components/{TenantScopeBanner,OperatorDispositionBadge,TechnicalStateMetadata,RedactedField,SafeCopyId,ConsoleEmptyState,ConsoleErrorPanel,TrustMatrix,WorkspaceTrustSummary}.razor(.cs)`, `Components/Icons/FoldersConsoleIcons.cs`, `Components/Pages/{Workspace,FolderDetail,Folders,Home}.razor(.cs)`, `Services/{DispositionLabelMapper,FieldDisclosure,RedactionDisclosureMapper,ConsoleErrorPresenter,ConsoleStatusText,TrustDimensionDeriver,EmptyStateReason,FoldersUserContextAccessor}.cs`]
- [Source: `tests/Hexalith.Folders.UI.Tests/{BadgeRenderingFixture,CompositionRootFactory,NavigationContractTests,ShellCompositionTests,WorkspacePageTests,FolderDetailPageTests,ConsoleErrorPresenterTests,ConsoleStatusTextTests}.cs`; `tests/Hexalith.Folders.UI.E2E.Tests/{Routes/ConsoleRoutes.cs,Smoke/FolderRouteSmokeTests.cs,Smoke/ConsoleSmokeTests.cs}`; `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs`]
- [Source: prior stories `_bmad-output/implementation-artifacts/6-{1,2,3,4,5,6}-*.md` — endpoints/DTOs (6.1), shell + conventions + data path (6.2), disposition components + parity test (6.3), redaction component + mapper + icons (6.4), the reviewed wireflow contract + §1.4 as-built HIGH lesson (6.5), the Folder/Workspace pages + shared components + provider link-out placeholders + the data-path/totality/safe-copy review lessons (6.6)]
- [Source: project-context.md — read-only console boundary (80, 146), metadata-only non-negotiable (139), GitHub/Forgejo capability-tested (82, 150), redacted-distinct-from-unknown (151), no generated-client edits (46, 108, 148), tenant authority from context not payload (76, 137)]
- [Note: architecture F-1/UI-tree say "Blazor Server"; as-built + Story 6.2 is "Blazor Web App host using Interactive Server rendering" — use the as-built term, do not revert.]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.8 (1M context) — `claude-opus-4-8[1m]`

### Debug Log References

- Build/test via the WSL-accessible Windows SDK (`/mnt/c/Program Files/dotnet/dotnet.exe`); the WSL-native SDK fails the `global.json` 10.0.302 pin.
- **Baseline (pre-change):** `dotnet test tests/Hexalith.Folders.UI.Tests` → **298/298 passed, 0 failed** (matches the 6.6 UI-lane baseline in Dev Notes).
- **Final (dev):** `dotnet test tests/Hexalith.Folders.UI.Tests` → **371/371 passed, 0 failed** (+73 over the 298 baseline; zero regressions). `NavigationContractTests.Console_DoesNotRegisterAnyDomainCommandManifest` green. *(The "349/349" figure first recorded here was an undercount — corrected during the Senior Developer Review; see that section.)*
- **Final (post-review):** **372/372 passed, 0 failed** after the review auto-fixes (one bUnit test repurposed 1:1 + one freshness-honesty test added). Generated client / `Directory.Packages.props` / OpenAPI spine still untouched.
- **`ScaffoldContractTests`** → 8/10; the two reds — `ProjectReferencesFollowAllowedDependencyDirection` (drift is in **IntegrationTests**, not the UI project) and `SolutionContainsOnlyCanonicalBuildableProjects` (the **LoadTests** projects) — are two of the three documented pre-existing baseline reds, **not** 6.7 regressions. The UI project still references only `Hexalith.Folders.Client` + `Hexalith.FrontComposer.Shell` and is **not** in either diff.
- **E2E test project compiles clean** (`dotnet build tests/Hexalith.Folders.UI.E2E.Tests`). The new Playwright smoke tests are not executed here (deferred Playwright lane; the hermetic E2E host boots the UI only, no backend) — the provider pages are transport-tolerant (§3.8) so they render their page-root + single `<h1>` on the backend-less host, which is what makes the smoke assertions hold when the lane runs.
- **Guards:** `git diff --stat src/Hexalith.Folders.Client/Generated/` empty; `Directory.Packages.props` and the OpenAPI spine untouched; no new `<PackageReference>`/`<ProjectReference>`.

### Completion Notes List

**What shipped (Provider view §3.3):**

- **`Provider.razor` (`/folders/{FolderId}/provider`)** — folder-scoped readiness. Order per §3.3: `TenantScopeBanner` (scope-first, UX-DR6) → provider identity (family/binding/capability-profile/repository-binding refs via `SafeCopyId`, repository binding-state badge, sensitive-metadata tier) → **credential-reference identifier** labeled "reference identifier (not a secret)" (`SafeCopyId` when visible, `RedactedField` when redacted, honest Unknown otherwise) → readiness (`OperatorDispositionBadge` **primary** fed the server `ProviderStatusDiagnostics.Disposition` verbatim + readiness reason `Status` secondary + freshness zone) → capability/sync metadata (workspace-scoped, only when `?workspaceId=` resolved) → advisory failure metadata (operation-scoped, only when `?workspaceId=&operationId=` resolved). Per-load `x_Correlation_Id`, `ReadConsistencyClass.Eventually_consistent` on every call.
- **`ProviderSupport.razor` (`/providers/support`)** — tenant-scoped capability-support matrix from `GetProviderSupportEvidenceAsync` (FR57): `CapabilityProfileRef × Capability × SupportState` rows with non-color-only badges; cursor pagination via read-only `?cursor=` navigation; freshness zone; four empty/denied/unavailable states via the shipped infra.
- **`ProviderStatusText`** (Task 2) — total, throw-on-unknown mappers for `ProviderCapabilityState`, `RepositoryBindingBindingState`, `ProviderOutcomeState`, `ProviderReadinessStatus`, `ProviderCapabilityName`, `SensitiveMetadataTier`. `unknown_provider_outcome`/`reconciliation_required` → `Warning` (awaiting-human treatment, AC #8) — never an unbadged neutral "Unknown".
- **`ProviderReadinessModel`** (Task 3) — private UI-assembly record (not Contracts, not a registered facade — the as-built direct-SDK path) with a static `Create(...)` assembler; centralizes credential-reference disclosure + freshness derivation. Credential value is never carried onto the model when redacted (defense-in-depth, F-5).
- **Navigation (Task 6)** — resolved both 6.6 provider link-out placeholders: the Workspace `console-page-workspace-provider-pending` span → a route link, and the Trust Matrix provider-readiness cell's evidence href → `/folders/{FolderId}/provider` (comment at the cell updated). `FolderDetail` provider-binding row links out; `Home` gains a hand-authored `/providers/support` nav entry. `Console_DoesNotRegisterAnyDomainCommandManifest` stays green (no manifest, no `AddHexalithEventStore`).

**Conscious shape gaps (documented, not papered over — per Dev Notes discipline):**

- **Operator-rich readiness fields** (`safeReasonCode`/`safeRemediationCode`/`remediationCategory`/`retryAfterSeconds` on `ProviderReadinessOperator`) are only returned by the **out-of-scope active validations probe** (AC #5). On the passive path the readiness reason is `ProviderStatusDiagnostics.Status` + `Trust` reason codes, and retryability/failure category come from `ProviderOutcome` (advisory) when an operation context exists; otherwise an honest "no recent provider operation" / `Unknown` affordance is rendered — never a fabricated remediation/retry value.
- **`ProviderReadinessStatus` mapper** is implemented + totality-tested for contract/parity completeness but is **not rendered** — the enum is not reachable on the passive read path (only via the validations probe), and inventing a `ProviderReadinessStatus`→disposition mapping is forbidden by Dev Notes (the server `Disposition` is authoritative).
- **`ProjectionAvailability.redacted`/`.unknown`** (reference-pending C5, §7) are not mapped to a disclosure state; the freshness zone renders a neutral "Unknown" for them rather than inventing a rendering or claiming "Current". `RedactionDisclosureMapper` was not extended.
- **Transport resilience:** the pages catch `HttpRequestException`/`TaskCanceledException` (read model unreachable) → §3.8 read-model-unavailable, distinct from a canonical `HexalithFoldersApiException` denial → safe-denial panel. This is metadata-only (no transport-error leak) and is what lets the E2E smoke run against the backend-less host.

**Task 8 self-review (against the wireflow contract + scope):** UX-DR / C6 / disposition / `FieldDisclosure` IDs preserved verbatim (none renumbered or forked); the four §2.3 distinctions hold visibly + semantically (`stale`/`delayed` vs `unavailable`; `redacted` vs `unknown` vs `missing` via `RedactedField`; `denied` vs `inaccessible`; disposition-primary vs technical-state-secondary); §7 pending inputs (C2/C5 freshness, C4 filter vocabulary, `ProjectionAvailability` redacted/unknown, French localization) not invented/hardcoded; the active validate probe was **not** wired (proven by the `DidNotReceive` test) and no "Validate"/"Retry"/"Configure" action affordance exists; no audit/incident/perf-gate scope crept in (6.8/6.9/6.10/6.11). `deferred-work.md` not edited.

### File List

**New — source:**

- `src/Hexalith.Folders.UI/Services/ProviderStatusText.cs`
- `src/Hexalith.Folders.UI/Components/Models/ProviderReadinessModel.cs`
- `src/Hexalith.Folders.UI/Components/Pages/Provider.razor`
- `src/Hexalith.Folders.UI/Components/Pages/Provider.razor.cs`
- `src/Hexalith.Folders.UI/Components/Pages/ProviderSupport.razor`
- `src/Hexalith.Folders.UI/Components/Pages/ProviderSupport.razor.cs`

**New — tests:**

- `tests/Hexalith.Folders.UI.Tests/ProviderStatusTextTests.cs`
- `tests/Hexalith.Folders.UI.Tests/ProviderPageTests.cs`
- `tests/Hexalith.Folders.UI.Tests/ProviderSupportPageTests.cs`
- `tests/Hexalith.Folders.UI.E2E.Tests/Smoke/ProviderRouteSmokeTests.cs`

**Modified:**

- `src/Hexalith.Folders.UI/Components/Pages/Workspace.razor` — provider-readiness section pending span → route link to the provider page.
- `src/Hexalith.Folders.UI/Components/Pages/Workspace.razor.cs` — Trust Matrix provider-readiness cell evidence href → `/folders/{FolderId}/provider` + reason copy + comment update.
- `src/Hexalith.Folders.UI/Components/Pages/FolderDetail.razor` — provider-binding row links to the provider page.
- `src/Hexalith.Folders.UI/Components/Pages/Home.razor` — hand-authored `/providers/support` nav link.
- `tests/Hexalith.Folders.UI.E2E.Tests/Routes/ConsoleRoutes.cs` — added `Provider(folderId)` + `ProviderSupport`.
- `tests/Hexalith.Folders.UI.Tests/FolderDetailPageTests.cs` — added `ProviderBindingRow_RendersProviderReadinessLink_WithFolderScopedHref` (AC #11). *(Added during review — was missing from the original File List.)*
- `tests/Hexalith.Folders.UI.Tests/WorkspacePageTests.cs` — added `ProviderReadinessSection_ResolvesPlaceholder_IntoProviderLink_WithFolderScopedHref` proving the 6.6 `console-page-workspace-provider-pending` placeholder is resolved (AC #11). *(Added during review — was missing from the original File List.)*
- `tests/Hexalith.Folders.UI.Tests/ShellCompositionTests.cs` — added `Home_RendersProviderSupportNavLink_PointingToProvidersSupport` (AC #11). *(Added during review — was missing from the original File List.)*

**Modified — story tracking (not code):**

- `_bmad-output/implementation-artifacts/sprint-status.yaml` — `6-7-…` ready-for-dev → in-progress → review.
- `_bmad-output/implementation-artifacts/6-7-build-provider-readiness-and-support-diagnostic-pages.md` — checkboxes, Dev Agent Record, File List, Change Log, Status.

## Senior Developer Review (AI)

**Reviewer:** Jerome · **Date:** 2026-05-29 · **Outcome:** Approve (auto-fix applied) · **Mode:** non-interactive story-automator review, auto-fix all issues.

**Method:** Adversarial validation of every AC and `[x]` task against the actual implementation, the generated SDK client (`HexalithFoldersClient.g.cs`), and the OpenAPI spine. Ground truth established by building + running the UI test lane (exit 0). Files reviewed: all six new source files, the four new test files, the five 6.6/route files modified for AC #11, and the three (previously undocumented) modified test files. Generated client, `Directory.Packages.props`, and the OpenAPI spine confirmed untouched.

**Verdict on claims:** The implementation is strong and faithful to the wireflow contract — disposition-primary readiness fed the server `Disposition` verbatim, total throw-on-unknown enum mappers, the active validation probe provably never invoked (`DidNotReceive`), all read-only/no-mutation guards green, every identifier through `SafeCopyId`, and both 6.6 link-out placeholders genuinely resolved. No CRITICAL or HIGH findings; all 12 ACs are substantively implemented and test-covered.

**Findings & resolution (all auto-fixed):**

| # | Sev | Finding | Resolution |
|---|-----|---------|------------|
| C | MEDIUM | **Credential-reference identifier was mislabeled.** `ProviderReadinessModel` sourced the "credential reference (not a secret)" value from `diagnostics.ProviderBindingReference`, which the OpenAPI example (`value: opaque_…PBR…`, `reasonCode: not_redacted`) and schema prove is the **provider-binding** reference. The real credential ref (`nonSecretCredentialReference`) exists **only** on the forbidden `ConfigureProviderBindingRequest` mutation body, and `ProviderBinding.Redaction` is a required single-valued marker — so the read path never returns a credential-reference *value*. No secret leaked (the borrowed value is non-secret), hence MEDIUM. | `ResolveCredentialReferenceDisclosure(binding)` now derives disclosure purely from the binding's redaction marker: **Redacted** when the binding is loaded, **Unknown** when not — never a visible value, never the provider-binding reference. `CredentialReferenceId` removed; the page's dead "visible" branch removed; bUnit test repurposed to prove the provider-binding reference value is never surfaced as the credential reference. Documented as a conscious shape gap. |
| D | LOW | **Freshness honesty drift.** `ProviderSupport.razor` rendered absent/default freshness as "Current" with a fabricated `0001-01-01` observed-at; the Provider model fabricated `0001` likewise — deviating from 6.6's `ObservedAtOrNull` discipline (UX-DR26). | `FreshnessLabel()`/`ObservedAtLabel()` on the support page now render an honest "Unknown"/"unknown" for absent freshness; `NormObservedAt(...)` added to `ProviderReadinessModel` (applied to diagnostics/sync/outcome). New support-page test locks the behavior. |
| A | MEDIUM | **File List omitted three modified test files** (`FolderDetailPageTests.cs`, `WorkspacePageTests.cs`, `ShellCompositionTests.cs`) — the recurring 6.6 "File List fidelity" lesson. | Added to the File List above with their AC #11 test names. |
| B | LOW | **Dev Agent Record test count inaccurate** — recorded final 349/349 (+51); the true count was 371/371 (+73). | Debug Log corrected; post-review count is 372/372. |

**Post-fix verification:** `dotnet test tests/Hexalith.Folders.UI.Tests` → **372/372 passed, 0 failed**. `git diff --stat src/Hexalith.Folders.Client/Generated/` empty; `Directory.Packages.props` untouched; no new package/project references. 0 CRITICAL issues remain → Status set to **done**.

## Change Log

| Date       | Change |
|------------|--------|
| 2026-05-29 | Implemented Story 6.7 — the Provider view (§3.3): `Provider.razor` (folder-scoped readiness) + `ProviderSupport.razor` (tenant-scoped capability matrix, FR57), `ProviderStatusText` total mappers, the `ProviderReadinessModel` view-model, navigation wiring resolving both 6.6 provider link-out placeholders, and bUnit + E2E coverage. UI test lane green (371/371; baseline 298/298). Status → review. |
| 2026-05-29 | Senior Developer Review (AI), auto-fix: corrected the credential-reference mislabel (sourced the provider-binding reference instead of the never-returned credential value → now Redacted/Unknown only), hardened freshness honesty on the support page + view-model (no fabricated `0001` observed-at), completed the File List (3 modified test files), and corrected the recorded test count. UI lane 372/372 green; generated client / packages / OpenAPI spine untouched. Status → done. |
