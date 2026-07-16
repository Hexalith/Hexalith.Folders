---
baseline_commit: 45b51df
---

# Story 6.4: Implement sensitive-metadata redaction affordance

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an operator,
I want redacted metadata to render differently from unknown or missing data,
so that policy-hidden fields do not look like system defects.

## Acceptance Criteria

Source epic AC (epics.md#Story-6.4, lines 1359-1364):

> **Given** sensitive metadata is redacted by policy
> **When** the UI renders the field
> **Then** a visible lock-icon affordance and explanatory text are shown
> **And** the redaction component exposes reusable rendering semantics verified by this story's tests so diagnostic views can distinguish redacted, unknown, and missing values consistently.

Architecture anchor — **F-5** (`architecture.md` line 549): *"redacted fields render with a visible lock-icon affordance ('your tenant policy hides this; contact your administrator'); never silent truncation."* Cross-cutting concern #11 (line 110): *"redacted fields are visually distinguished from unknown/missing fields … never as silent truncation, because silent redaction during an incident reads as a system bug and operators lose time chasing ghosts."* Critical project rule: *"Redacted values must be visibly distinct from unknown or missing values. Silent truncation or hiding redaction is treated as an operator-facing correctness bug."*

Decomposed, testable acceptance criteria:

1. **`FieldDisclosure` is the presentation-level disclosure vocabulary the console uses to render any audience-gated field.** New file `src/Hexalith.Folders.UI/Services/FieldDisclosure.cs`:
   - A `public enum FieldDisclosure` with exactly four members: `Visible`, `Redacted`, `Unknown`, `Missing`.
   - **This is a presentation concept, NOT a duplicate of an SDK wire enum.** The SDK carries *field-specific* redaction signals (`RedactionMetadataVisibility`, `DiagnosticFieldClassification`, `RedactableAuditTimestampPrecision`, `FileMetadataItemRedaction` — see AC #2). `FieldDisclosure` is the single rendering classification those signals are *mapped into* so every diagnostic page renders redacted-vs-unknown-vs-missing consistently. Document this rationale in the enum's XML doc so a reviewer does not mistake it for a wire-enum copy. **Do NOT** add `[EnumMember]` attributes or any wire serialization to it — it never crosses the wire.
   - XML doc each member: `Visible` = the value is disclosed and rendered; `Redacted` = a tenant/audience policy hid the value (lock-icon affordance, F-5); `Unknown` = the value is not yet known to the read model (projection-pending / not-yet-observed); `Missing` = no value exists for this field (not applicable / never recorded). `Redacted`, `Unknown`, and `Missing` MUST render distinctly (AC #4, AC #5).

2. **`RedactionDisclosureMapper` translates every SDK redaction wire vocabulary into a `FieldDisclosure`, total over each enum, never silently defaulting.** New file `src/Hexalith.Folders.UI/Services/RedactionDisclosureMapper.cs`:
   - Static class, `public` (consumed by the component, the dev gallery, and future diagnostic pages in the same assembly). `using Hexalith.Folders.Client.Generated;`.
   - Exposes these overloads, each a switch expression that throws `ArgumentOutOfRangeException` (offending value included) on an unrecognized enum member — **never** a silent default:
     - `public static FieldDisclosure FromAuditVisibility(RedactionMetadataVisibility visibility)`
       - `Metadata_only` → `Visible` (the record is metadata-only and its value is disclosed)
       - `Redacted` → `Redacted`
     - `public static FieldDisclosure FromTimestampPrecision(RedactableAuditTimestampPrecision precision)`
       - `Exact` → `Visible`
       - `Bucketed` → `Visible` (a bucketed timestamp is an *aggregated* disclosed value, not a redaction)
       - `Redacted` → `Redacted`
     - `public static FieldDisclosure FromFileMetadataRedaction(FileMetadataItemRedaction redaction)`
       - `Not_redacted` → `Visible`
       - `Redacted` → `Redacted`
       - `Excluded` → `Missing`
       - `Binary_disallowed` → `Missing`
     - `public static FieldDisclosure FromDiagnosticClassification(DiagnosticFieldClassification classification, bool hasValue)`
       - `Forbidden` → `Redacted` (must-not-appear is rendered as policy-hidden, never as missing — F-5)
       - `Consumer_safe` → `hasValue ? Visible : Missing`
       - `Operator_sanitized` → `hasValue ? Visible : Missing`
     - `public static FieldDisclosure FromAuditRedaction(RedactionMetadata redaction, string? value)` — convenience for the redactable audit references (`RedactableAuditActorReference`, `RedactableAuditOperationReference`, `RedactableDiagnosticIdentifier`) which each carry a `RedactionMetadata.Visibility` plus a nullable `Value`:
       - `ArgumentNullException.ThrowIfNull(redaction)` first (public-boundary guard per project rules).
       - `redaction.Visibility == RedactionMetadataVisibility.Redacted` → `Redacted`; otherwise `string.IsNullOrEmpty(value) ? Missing : Visible`.
   - **Scope boundary — do NOT map `ProjectionAvailability`.** Its `redacted`/`unknown` members are explicitly `TODO(reference-pending C5)` in the generated client ("Consumers MUST treat the latter two as reference-pending and not as a frozen contract", `HexalithFoldersClient.g.cs` ~line 11824). Mapping it now would bind the UI to an unapproved contract. A future story adds that overload after C5 freezes the vocabulary; note this deferral in Dev Notes.

3. **`FoldersConsoleIcons` provides a Folders-owned lock-closed icon built on Fluent UI's core `Icon` abstraction — with zero new package references.** New file `src/Hexalith.Folders.UI/Components/Icons/FoldersConsoleIcons.cs`:
   - Static factory mirroring FrontComposer's `FcFluentIcons` (`Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Components/Icons/FcFluentIcons.cs`): a hand-authored SVG `<path>` const + a `Create(name, size, content) => new Icon(name, IconVariant.Regular, size, content)` helper, `using Microsoft.FluentUI.AspNetCore.Components;`.
   - Exposes `public static Icon LockClosed16()` returning a closed-padlock icon at `IconSize.Size16`.
   - **Why hand-authored and not the Fluent System Icons package:** `Microsoft.FluentUI.AspNetCore.Components.Icons` (`4.14.0`) is **NOT** referenced by `Hexalith.Folders.UI` — the UI gets only the *core* `Microsoft.FluentUI.AspNetCore.Components` package transitively through `Hexalith.FrontComposer.Shell` (verified: `Hexalith.FrontComposer.Shell.csproj` references only the core package). Adding the Icons package would add a new `<PackageReference>`, which AC #7 forbids. FrontComposer solved the identical problem with `FcFluentIcons`; mirror it.
   - Reference path (a valid 16-px closed padlock; the dev may refine the geometry but MUST keep it a recognizable closed lock and verify it renders): `"<path d=\"M8 1a3 3 0 0 0-3 3v2H4.5A1.5 1.5 0 0 0 3 7.5v6A1.5 1.5 0 0 0 4.5 15h7a1.5 1.5 0 0 0 1.5-1.5v-6A1.5 1.5 0 0 0 11.5 6H11V4a3 3 0 0 0-3-3Zm2 5H6V4a2 2 0 1 1 4 0v2Z\"/>"`.
   - The `wwwroot/icons/` directory the architecture mentions (line 1180) is satisfied by this in-code `Icon` factory; **do not** add an `<img>`-based raster asset — a `FluentIcon`-rendered vector inherits Fluent UI sizing/theming and screen-reader semantics, and adds no static-asset pipeline. Document this substitution in Dev Notes (the architecture inventory is descriptive, not prescriptive against an equivalent vector approach).

4. **`RedactedField` is the F-5 affordance component: it renders each `FieldDisclosure` state distinctly, with the lock icon AND explanatory text for the redacted case, and text always present (never color/icon alone).** New file `src/Hexalith.Folders.UI/Components/RedactedField.razor` + `RedactedField.razor.cs`:
   - **Parameters** (code-behind):
     - `[Parameter, EditorRequired] public FieldDisclosure Disclosure { get; set; }` — primary input; pages resolve it via `RedactionDisclosureMapper`.
     - `[Parameter] public string? Value { get; set; }` — rendered **only** when `Disclosure == Visible`. The component MUST NOT render `Value` for any other state (defense-in-depth: a redacted field must never leak its value even if a caller mistakenly passes one).
     - `[Parameter] public string? ColumnHeader { get; set; }` — optional; used to build the `aria-label` as `"{ColumnHeader}: {announced-text}"` (parallel to `FcStatusBadge`).
     - `[Parameter] public string? RedactedExplanation { get; set; }` — optional override for the redacted explanatory text; defaults to the F-5 copy (see below).
     - `[Parameter] public string? UnknownText { get; set; }` and `[Parameter] public string? MissingText { get; set; }` — optional overrides for the unknown/missing copy; default to `"Unknown"` and `"Not recorded"` respectively.
     - `[Parameter] public string? AdditionalCssClass { get; set; }` — optional, appended to the outer wrapper; no structural styling here (UX-DR16 — restrained Fluent foundations).
   - **Default English copy** (hard-coded constants on the component; FR parity is deferred — see Dev Notes "Scope boundaries", consistent with Story 6.3):
     - Redacted: `"Hidden by tenant policy — contact your administrator"` (F-5 intent, line 549).
     - Unknown: `"Unknown"`. Missing: `"Not recorded"`.
   - **Rendering per state** (compute display text + a `data-fc-disclosure` token in `OnParametersSet`, mirroring `OperatorDispositionBadge`/`TechnicalStateMetadata` caching):
     - `Visible` → `<span>@Value</span>`; `data-fc-disclosure="visible"`.
     - `Redacted` → a `<FluentIcon Value="@FoldersConsoleIcons.LockClosed16()" aria-hidden="true" />` **followed by** the explanatory text in a muted Fluent typography (`<FluentText>` with the same secondary style `TechnicalStateMetadata` uses — `Size="@Typography.Secondary.Size" Weight="@Typography.Secondary.Weight" As="@Typography.Secondary.Tag"`). The icon is decorative (`aria-hidden`); the text carries the meaning so color/icon is **never** the only signal (UX-DR14 / UX-DR30 commitment #1). `data-fc-disclosure="redacted"`.
     - `Unknown` → muted text `UnknownText`; NO lock icon; `data-fc-disclosure="unknown"`.
     - `Missing` → muted text `MissingText`; NO lock icon; `data-fc-disclosure="missing"`.
   - **Outer element contract** (every state): `<span data-testid="redacted-field" data-fc-disclosure="@_disclosureToken" aria-label="@_ariaLabel" class="@AdditionalCssClass">`. The three non-visible states are distinguishable both by the visible text/icon AND by the `data-fc-disclosure` attribute value (Story 6.6-6.9 DataGrid filters and Story 6.11 selectors hook this).
   - **`aria-label` contract** — screen readers must announce redaction, never silence it:
     - `Visible` → `ColumnHeader is null ? Value : "{ColumnHeader}: {Value}"` (omit when `Value` is null/empty).
     - `Redacted` → `ColumnHeader is null ? RedactedExplanation : "{ColumnHeader}: {RedactedExplanation}"`.
     - `Unknown` → `ColumnHeader is null ? UnknownText : "{ColumnHeader}: {UnknownText}"`.
     - `Missing` → `ColumnHeader is null ? MissingText : "{ColumnHeader}: {MissingText}"`.
   - **Metadata-only guard:** the component renders only `Value`, the fixed English copy, and the `ColumnHeader` the caller supplies. It MUST NOT render a `ReasonCode`, raw payloads, paths, or any free-form server text — reason codes are stable tokens but surfacing them is a Story 6.6-6.8 page-composition decision, not this atomic renderer's job. Do not add a `ReasonCode` parameter in this story.
   - **No mutation, no tooltip/popover/dialog.** Like Story 6.3's badge, `RedactedField` is a pure renderer. The explanatory text is inline, not a hover tooltip (UX-DR: status detail must be perceivable without pointer hover; a 3 AM operator on keyboard must read it). Story 6.6+ owns any richer disclosure affordance.

5. **bUnit tests prove every disclosure state renders distinctly, the mapper is total over every SDK enum, and the redacted case shows both lock + text.** New test classes under `tests/Hexalith.Folders.UI.Tests/` (reuse `BadgeRenderingFixture.Create()` — it already wires FluentUI + FrontComposer quickstart + storage/theme substitutes; `FluentIcon` and `FluentText` need that `AddFluentUIComponents()` registration):
   - `RedactionDisclosureMapperTests.cs`:
     - `FromAuditVisibility_IsTotal_ForEveryRedactionMetadataVisibility` — `[Theory]` over every `RedactionMetadataVisibility` value; asserts the expected `FieldDisclosure` (hard-coded expectations: `Metadata_only`→`Visible`, `Redacted`→`Redacted`) and that no value throws.
     - `FromTimestampPrecision_IsTotal_ForEveryPrecision` — `[Theory]` over every `RedactableAuditTimestampPrecision`; `Exact`/`Bucketed`→`Visible`, `Redacted`→`Redacted`.
     - `FromFileMetadataRedaction_IsTotal_ForEveryRedaction` — `[Theory]` over every `FileMetadataItemRedaction`; `Not_redacted`→`Visible`, `Redacted`→`Redacted`, `Excluded`→`Missing`, `Binary_disallowed`→`Missing`.
     - `FromDiagnosticClassification_MapsForbiddenToRedacted_AndHonorsHasValue` — `[Theory]` over every `DiagnosticFieldClassification` × `{hasValue: true, false}`; asserts `Forbidden`→`Redacted` regardless of `hasValue`, `Consumer_safe`/`Operator_sanitized`→`Visible` when `hasValue` else `Missing`.
     - `FromAuditRedaction_ResolvesRedactedThenValuePresence` — covers: `{Visibility=Redacted, value=null}`→`Redacted`; `{Visibility=Redacted, value="x"}`→`Redacted` (redaction wins even if a value leaked in); `{Visibility=Metadata_only, value=null/""}`→`Missing`; `{Visibility=Metadata_only, value="x"}`→`Visible`. Also asserts `FromAuditRedaction(null, "x")` throws `ArgumentNullException`.
     - **Totality sentinel:** each `From*` `[Theory]` enumerates `Enum.GetValues<TEnum>()` so a future SDK enum addition that the mapper does not handle fails the test (the analog of Story 6.3's C6 coverage test). There is **no** cross-side parity sentinel here — these are pure SDK→presentation maps owned solely by the UI, with no server-side counterpart to drift against (contrast Story 6.3's `DispositionLabelParityTests`). State this explicitly in Dev Notes.
   - `RedactedFieldTests.cs` (bUnit):
     - `Visible_RendersValue_AndNoLockIcon` — `Disclosure=Visible, Value="acme/widgets"`; asserts rendered text contains `"acme/widgets"`, `data-fc-disclosure="visible"`, and zero `<fluent-icon>`/`fluenticon` elements.
     - `Redacted_RendersLockIconAndExplanatoryText` — `Disclosure=Redacted`; asserts the rendered DOM contains a Fluent icon element AND the text `"Hidden by tenant policy — contact your administrator"` AND `data-fc-disclosure="redacted"`. **Negative:** asserts the supplied `Value` is NOT in the markup even when one is passed (`Value="secret-branch"` → markup must not contain `"secret-branch"`) — the redacted-implies-no-value invariant.
     - `Unknown_RendersDistinctly_FromRedacted` — `Disclosure=Unknown`; asserts text `"Unknown"`, `data-fc-disclosure="unknown"`, and **no** lock icon (proves redacted ≠ unknown visually — the core F-5 correctness rule).
     - `Missing_RendersDistinctly_FromRedactedAndUnknown` — `Disclosure=Missing`; asserts text `"Not recorded"`, `data-fc-disclosure="missing"`, no lock icon.
     - `Redacted_Unknown_Missing_ProduceThreeDistinctTokens` — renders all three; asserts the three `data-fc-disclosure` values are pairwise distinct AND the redacted markup is the only one containing a lock icon (defends concern #11 / the critical "visibly distinct" rule in a single test).
     - `Renders_AriaLabel_WithColumnHeader_ForEachState` — `[Theory]` over the four states with `ColumnHeader="Branch"`; asserts `aria-label` equals `"Branch: {announced-text}"` (redacted announces the explanation, never an empty string).
     - `Exposes_RedactedField_DataTestId` — asserts the outer element carries `data-testid="redacted-field"`.
     - `Renders_NoMutationAffordances` — for `Disclosure=Redacted`: zero `<form>`, `[data-fc-command]`, `[data-fc-mutation]`, `fluentdialog`.
   - `RedactionGalleryTests.cs` (bUnit):
     - `Gallery_RendersOneRowPerDisclosureState` — asserts the DataGrid renders exactly `Enum.GetValues<FieldDisclosure>().Length` `[data-testid="redacted-field"]` rows.
     - `Gallery_RedactedRow_IsTheOnlyRowWithALockIcon` — proves the visual distinction is observable on the rendered page, not just in unit assertions.
     - `Gallery_RendersWithoutMutationControls` — same shape as `StateLabelGalleryTests.Gallery_RendersWithoutMutationControls`.
     - `Gallery_ExposesPageRootDataTestId` — asserts `data-testid="console-page-redaction-gallery-root"`.

6. **A development-only gallery makes the redacted-vs-unknown-vs-missing distinction eyeballable, mirroring Story 6.3's `StateLabelGallery`.** New file `src/Hexalith.Folders.UI/Components/Pages/RedactionGallery.razor`:
   - Route `@page "/dev/redaction-gallery"`; `<PageTitle>Redaction Gallery — Hexalith Folders</PageTitle>`; `<h1>Redaction Gallery</h1>`; root container `data-testid="console-page-redaction-gallery-root"`.
   - Renders a read-only `<FluentDataGrid>` (UX-DR23) with one row per `FieldDisclosure` value, each row showing the state name (plain text) and a `<RedactedField Disclosure="@row.Disclosure" Value="example/value" ColumnHeader="Example" />`. For the `Visible` row, `Value="acme/widgets"` so the disclosed path is visible; the redacted row's `Value` is irrelevant (must not appear).
   - **No SDK call, no mutation controls** — fed entirely by `Enum.GetValues<FieldDisclosure>()` (parallel to `StateLabelGallery`, which feeds itself from the enum and does not reference `IFoldersClient`).
   - Update `src/Hexalith.Folders.UI/Components/Pages/Home.razor`: inside the existing `@if (Env.IsDevelopment())` block, add a **second** dev link to `/dev/redaction-gallery` in its own `<p data-testid="console-page-home-dev-redaction-gallery-link">`. Leave the existing `console-page-home-dev-gallery-link` (Story 6.3) untouched. `IHostEnvironment` is already injected in `Home.razor`; no new injection needed.

7. **Project-reference direction and package set stay exactly as Story 6.3 left them.** Story 6.4 is UI-only:
   - **Does NOT** add any `<ProjectReference>` or `<PackageReference>` to `src/Hexalith.Folders.UI/Hexalith.Folders.UI.csproj`. All redaction enums (`RedactionMetadataVisibility`, `DiagnosticFieldClassification`, `RedactableAuditTimestampPrecision`, `FileMetadataItemRedaction`) and records (`RedactionMetadata`, `RedactableAuditActorReference`) flow through the existing `Hexalith.Folders.Client` reference; the core Fluent UI `Icon`/`FluentIcon`/`FluentText` flow through the existing `Hexalith.FrontComposer.Shell` reference.
   - **Does NOT** add any reference to `tests/Hexalith.Folders.UI.Tests/Hexalith.Folders.UI.Tests.csproj` (bunit + NSubstitute + Shouldly + xunit.v3 already present from Story 6.2/6.3).
   - **Does NOT** touch `.slnx`, `Directory.Packages.props`, `Directory.Build.props`, or any `ScaffoldContractTests` allow-list. The `ProjectReferencesFollowAllowedDependencyDirection` test must not gain a new violation; if it fails, fix the offending edge — do not loosen the assertion.

8. **Negative scope is enforced by tests, not prose.** Story 6.4 adds Folders-domain UI only:
   - **No production-tree edits outside `src/Hexalith.Folders.UI/` and `tests/Hexalith.Folders.UI.Tests/`.** No server, domain, contracts, client, workers, CLI, MCP, AppHost, or Aspire edits. The redaction *classification* logic (`src/Hexalith.Folders/Redaction/SensitiveMetadataClassifier.cs`, `RedactingFormatter.cs`) and the audit query endpoints (Story 6.1) already exist and already emit the wire vocabulary; Story 6.4 only *renders* it. If a Generated client type appears missing, the cause is a stale local artifact — clean & rebuild; do NOT regenerate or hand-edit the SDK.
   - **No `<form>`, `<FluentDialog>`, mutation-bound `<FluentButton>`, `data-fc-command`, `data-fc-mutation`, file/diff display, credential reveal, or unrestricted-filesystem affordance** in any new component/page (UX-DR11). Each new page test replicates Story 6.2/6.3's no-mutation assertion.
   - **No projection DTOs in `Hexalith.Folders.Contracts`**, **no `AddHexalithEventStore`/`AddHexalithDomain` call from the UI**, **no `[Command]`/`[ProjectionTemplate]` markers** (Story 6.2/6.3 inherited rules). `FieldDisclosure` is a UI presentation enum, not a contract DTO.
   - **No edit to `src/Hexalith.Folders.Client/Generated/`** — verify byte-identical with `git diff --stat src/Hexalith.Folders.Client/Generated/` (must be empty).

9. **Build clean and hermetic; focused tests + regression sweep green.**
   - Build with the WSL-accessible Windows SDK (`/mnt/c/Program\ Files/dotnet/dotnet.exe`; the WSL-native SDK fails the `global.json` 10.0.302 pin — Memory: `dotnet-windows-sdk-wsl.md`): `dotnet.exe restore Hexalith.Folders.slnx` → `dotnet.exe build Hexalith.Folders.slnx --no-restore` → 0 warnings / 0 errors (warnings-as-errors is on).
   - Focused tests:
     - `dotnet.exe test tests/Hexalith.Folders.UI.Tests` — every new test (mapper + field + gallery) green; the Story 6.2/6.3 tests (shell, badge, metadata, state-label gallery, nav, user-context) remain green.
     - `dotnet.exe test tests/Hexalith.Folders.UI.E2E.Tests` — Story 6.2 home-page smoke remains green (Story 6.4 adds no new E2E; the redaction gallery is Development-only and out of production E2E scope).
   - **Regression sweep:** `dotnet.exe test tests/Hexalith.Folders.{Server,Cli,Mcp,Client,Contracts,Workers}.Tests` — zero behavioral change (Story 6.4 touches none of those trees).
   - **Carry-over reds:** `tests/Hexalith.Folders.Testing.Tests` pre-existing failures (the `ProjectReferencesFollowAllowedDependencyDirection` carry-over against `Hexalith.Folders.IntegrationTests` and the two unrelated reds noted in Story 6.2/6.3 debug logs) remain the **only** failures there. Story 6.4 must NOT add a new one.

10. **Redaction-distinction "bite" verification (do this and record it in the debug log).** The headline correctness rule is *redacted is visibly distinct from unknown/missing*. Prove the tests actually catch a regression:
    - Temporarily make `RedactedField` render `Unknown` with the same lock icon as `Redacted`; confirm `RedactedFieldTests.Unknown_RendersDistinctly_FromRedacted` and `Redacted_Unknown_Missing_ProduceThreeDistinctTokens` fail. Revert.
    - Temporarily make `RedactionDisclosureMapper.FromDiagnosticClassification` map `Forbidden`→`Missing`; confirm `FromDiagnosticClassification_MapsForbiddenToRedacted_AndHonorsHasValue` fails. Revert.
    - Temporarily let `RedactedField` render `Value` in the `Redacted` branch; confirm `Redacted_RendersLockIconAndExplanatoryText`'s negative assertion fails. Revert.

## Tasks / Subtasks

- [x] **Task 1 — Read the F-5 contract, the SDK redaction wire vocabulary, and the FrontComposer icon/typography patterns end-to-end** (AC: #1-#4)
  - [x] Read `architecture.md` line 549 (F-5), line 110 (cross-cutting concern #11), line 168 (UX state-distinction requirement), and lines 1173-1202 (UI source tree — note `Components/RedactedField.razor` and `wwwroot/icons/`).
  - [x] Read the SDK redaction types in `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs` (DO NOT edit): `RedactionMetadataVisibility` (~line 13213), `RedactableAuditTimestampPrecision` (~13225), `FileMetadataItemRedaction` (~13045), `DiagnosticFieldClassification` (~11808), `RedactionMetadata` (~11560), `RedactableAuditActorReference`/`RedactableAuditOperationReference` (~11595). Confirm enum member spelling and `[EnumMember]` wire values. Note `ProjectionAvailability` (~11808-region) carries reference-pending `redacted`/`unknown` — out of scope.
  - [x] Read `FcFluentIcons.cs` (the hand-authored-SVG icon-factory pattern to mirror) and a usage site (`FcSettingsButton.razor` → `<FluentIcon Value="@FcFluentIcons.Settings20()" />`). Confirm `Hexalith.FrontComposer.Shell.csproj` references only the core FluentUI package (no Icons package) — this is *why* AC #3 hand-authors the lock.
  - [x] Read `src/Hexalith.Folders.UI/Components/TechnicalStateMetadata.razor`(.cs) for the muted secondary `FluentText` typography pattern (`Typography.Secondary.Size/Weight/Tag`) and the `data-testid` + `aria-label` + `OnParametersSet`-caching conventions to reuse.

- [x] **Task 2 — Implement `FieldDisclosure` enum** (AC: #1)
  - [x] Create `src/Hexalith.Folders.UI/Services/FieldDisclosure.cs` with `Visible`, `Redacted`, `Unknown`, `Missing` and the XML docs / rationale per AC #1. No `[EnumMember]`, no wire serialization.

- [x] **Task 3 — Implement `RedactionDisclosureMapper`** (AC: #2)
  - [x] Create `src/Hexalith.Folders.UI/Services/RedactionDisclosureMapper.cs` (static, `public`).
  - [x] Implement the five overloads per AC #2 as switch expressions; each throws `ArgumentOutOfRangeException` on an unknown enum value (never a silent default). `FromAuditRedaction` guards null with `ArgumentNullException.ThrowIfNull`.
  - [x] Do NOT add a `ProjectionAvailability` overload (reference-pending; see Dev Notes).

- [x] **Task 4 — Implement `FoldersConsoleIcons.LockClosed16()`** (AC: #3)
  - [x] Create `src/Hexalith.Folders.UI/Components/Icons/FoldersConsoleIcons.cs` mirroring `FcFluentIcons` (const SVG path + `Create(name, size, content)` helper + `LockClosed16()`).
  - [x] Verify it renders via `<FluentIcon Value="@FoldersConsoleIcons.LockClosed16()" />` against the core FluentUI package only (no new package reference).

- [x] **Task 5 — Implement `RedactedField` component** (AC: #4)
  - [x] Create `src/Hexalith.Folders.UI/Components/RedactedField.razor` + `RedactedField.razor.cs`.
  - [x] Code-behind: parameters per AC #4; constants for the English copy; `OnParametersSet` computes `_disclosureToken`, the display text, and `_ariaLabel`. Render `Value` ONLY in the `Visible` branch.
  - [x] Razor: outer `<span data-testid="redacted-field" data-fc-disclosure="@_disclosureToken" aria-label="@_ariaLabel" class="@AdditionalCssClass">`; per-state body (decorative `aria-hidden` lock `<FluentIcon>` + muted `<FluentText>` for redacted; muted `<FluentText>`/`<span>` for unknown/missing/visible). Text always present alongside any icon.

- [x] **Task 6 — Implement `RedactionGallery` dev page + Home link** (AC: #6)
  - [x] Create `src/Hexalith.Folders.UI/Components/Pages/RedactionGallery.razor` per AC #6 (one row per `FieldDisclosure`, read-only `FluentDataGrid`, page-root `data-testid`).
  - [x] Add the second dev link to `Home.razor` inside the existing `@if (Env.IsDevelopment())` block with `data-testid="console-page-home-dev-redaction-gallery-link"`; leave the Story 6.3 link untouched.

- [x] **Task 7 — bUnit tests** (AC: #5)
  - [x] `RedactionDisclosureMapperTests.cs` — the five `[Theory]`/`[Fact]` groups per AC #5, each enumerating `Enum.GetValues<TEnum>()` for totality.
  - [x] `RedactedFieldTests.cs` — the eight tests per AC #5, including the redacted-implies-no-value negative and the three-states-distinct test. Reuse `BadgeRenderingFixture.Create()`.
  - [x] `RedactionGalleryTests.cs` — the four tests per AC #5.

- [x] **Task 8 — Build, focused tests, regression sweep, redaction-distinction bites** (AC: #9, #10)
  - [x] Build with the WSL-accessible Windows SDK; 0 warnings / 0 errors.
  - [x] Run `tests/Hexalith.Folders.UI.Tests` (all new + Story 6.2/6.3 tests green) and `tests/Hexalith.Folders.UI.E2E.Tests` (smoke green).
  - [x] Run the regression sweep (`Server`, `Cli`, `Mcp`, `Client`, `Contracts`, `Workers`); zero change. Confirm `Hexalith.Folders.Testing.Tests` adds no new red.
  - [x] Run the AC #10 bites (redacted≡unknown icon; Forbidden→Missing; value-in-redacted-branch); confirm the named tests fail, then revert each.

- [x] **Task 9 — Negative-scope and selector audit** (AC: #7, #8)
  - [x] `git diff --stat src/Hexalith.Folders.Client/Generated/` is empty.
  - [x] No `<ProjectReference>`/`<PackageReference>` added to `Hexalith.Folders.UI.csproj` or `Hexalith.Folders.UI.Tests.csproj`; no `.slnx`/`Directory.Packages.props`/`ScaffoldContractTests` edits.
  - [x] Grep the production-tree diff for `[Command]`, `AddHexalithDomain`, `AddHexalithEventStore`, `[ProjectionTemplate]`, `<form>`, `<FluentDialog>`, `data-fc-command`, `data-fc-mutation` — zero hits.
  - [x] Confirm selectors exist via the bUnit assertions: `[data-testid="redacted-field"]`, the three distinct `data-fc-disclosure` tokens, `[data-testid="console-page-redaction-gallery-root"]`, `[data-testid="console-page-home-dev-redaction-gallery-link"]`.

## Dev Notes

### Scope boundaries (read first)

- **In scope:** the `FieldDisclosure` presentation vocabulary, the `RedactionDisclosureMapper` (SDK wire-vocabulary → `FieldDisclosure`), a hand-authored `FoldersConsoleIcons.LockClosed16()`, the `RedactedField` F-5 affordance component (lock + explanatory text for redacted; distinct rendering for unknown/missing; text never icon/color-only), a development-only `/dev/redaction-gallery` for visual confirmation, and bUnit tests locking every disclosure state + mapper totality. The result is a reusable atomic primitive that Stories 6.6-6.8 (diagnostic pages) and Story 6.9 (incident-mode read path — which per epic line 1434 renders redacted values **through the shared redaction component from Story 6.4 with no relaxed policy**) compose into Folder/Workspace/Provider/Audit/Incident views.
- **OUT of scope (do NOT implement here):**
  - **Server-side redaction classification.** `src/Hexalith.Folders/Redaction/SensitiveMetadataClassifier.cs` and `RedactingFormatter.cs`, plus the audit/file-context query result codes (`Redacted`, etc.), already exist and already emit the wire vocabulary. Story 6.4 only *renders* it. No server/domain/contracts edits.
  - **Diagnostic pages (Stories 6.6/6.7/6.8) and the incident stream (6.9).** Those are the first production callers of `RedactedField`. Story 6.4 ships the primitive + a dev gallery, not a production page.
  - **Surfacing `RedactionMetadata.ReasonCode` or any per-field reason text.** Reason codes are stable tokens, but deciding whether/where to show them is page-composition (6.6-6.8). The atomic `RedactedField` takes no `ReasonCode` parameter.
  - **`ProjectionAvailability` redaction mapping.** Its `redacted`/`unknown` members are `TODO(reference-pending C5)` in the generated client and MUST be treated as not-frozen. A future story adds that overload once C5 freezes the freshness/availability vocabulary.
  - **French localization.** Like Story 6.3, the copy ships in English (`string`-returning constants / parameters), so a future `IStringLocalizer`-backed wrapper is a refactor, not a rewrite. FR parity is owned by Story 6.5 (wireflow notes) + Story 6.11 (verification). Document here only — do NOT extend `deferred-work.md`.
  - **Tooltip/popover/dialog on the field.** `RedactedField` is a pure inline renderer; the explanation is inline text (keyboard/screen-reader perceivable), not a hover affordance.
  - **The redaction badge on the disposition badge.** Per Story 6.3 Dev Notes: disposition is operator-visible by policy and is **never** redacted. Do not wrap `OperatorDispositionBadge` in `RedactedField`.
- **Negative-scope guard for the dev:** if you find yourself editing anything under `src/Hexalith.Folders.{Server,Cli,Mcp,Client,Contracts,Workers,AppHost,Aspire}/` or `src/Hexalith.Folders/`, regenerating the SDK, adding a package reference, adding a `[Command]`/`[ProjectionTemplate]` marker, or loosening a `ScaffoldContractTests` assertion — stop. None of those are in Story 6.4's surface area.

### Build environment

- WSL-native .NET SDK does not satisfy `global.json` 10.0.302. Use `/mnt/c/Program\ Files/dotnet/dotnet.exe` for restore/build/test (Memory: `dotnet-windows-sdk-wsl.md`).
- Settings/hook paths use `/mnt/d/...`, not `D:\...` (Memory: `wsl-windows-hook-paths.md`).
- FrontComposer is already initialized at `Hexalith.FrontComposer/`. Per `CLAUDE.md`, only root-level submodules; **never** `git submodule update --init --recursive`. Story 6.4 adds no new submodule dependency.

### The SDK redaction wire vocabulary (the authority the UI consumes)

Redaction is **not** a single wire enum — it is field-shaped. `RedactedField` unifies these into one presentation vocabulary (`FieldDisclosure`) so every diagnostic surface renders consistently. The signals the mapper consumes (all in `Hexalith.Folders.Client.Generated`, all `[EnumMember]`-attributed snake_case on the wire):

| SDK type (`HexalithFoldersClient.g.cs`) | Members (wire) | Carried on | → `FieldDisclosure` |
|---|---|---|---|
| `RedactionMetadataVisibility` (~13213) | `metadata_only`, `redacted` | `RedactionMetadata.Visibility` (audit references) | `metadata_only`→Visible; `redacted`→Redacted |
| `DiagnosticFieldClassification` (~11808) | `consumer_safe`, `operator_sanitized`, `forbidden` | `RedactableAuditActorReference.Classification`, etc. | `forbidden`→Redacted; others→Visible if value else Missing |
| `RedactableAuditTimestampPrecision` (~13225) | `exact`, `bucketed`, `redacted` | `RedactableAuditTimestamp.Precision` | `exact`/`bucketed`→Visible; `redacted`→Redacted |
| `FileMetadataItemRedaction` (~13045) | `not_redacted`, `redacted`, `excluded`, `binary_disallowed` | `FileMetadataItem.Redaction` | `not_redacted`→Visible; `redacted`→Redacted; `excluded`/`binary_disallowed`→Missing |
| `RedactionMetadata` (~11560) | record `{ Visibility, ReasonCode }` | `RedactableAuditActorReference.Redaction`, etc. | via `FromAuditRedaction` |

`SensitiveMetadataTier` (~9872: `public_metadata`/`tenant_sensitive`/`credential_sensitive`/`secret`) is a *classification* tier carried on `FolderMetadata.MetadataClass`, **not** a per-field redaction signal — Story 6.4 does **not** map it (it answers "how sensitive is this metadata category", not "is this field hidden right now"). A diagnostic page may display the tier as plain metadata in a later story.

### Why a UI-owned presentation enum is NOT wheel-reinvention

Story 6.3 warned against inventing a UI-local copy of an SDK enum (the SDK `OperatorDispositionLabel` is the wire vocabulary). `FieldDisclosure` is the opposite case: there is **no single SDK enum** for "visible / redacted / unknown / missing" — the four field-specific SDK enums above each encode redaction *along their own axis*. `FieldDisclosure` is the rendering classification those axes collapse into, exactly as Story 6.3's `BadgeSlot` mapping collapses many dispositions into five appearance slots. It never serializes; it has no wire counterpart to drift against; and centralizing it is what makes "redacted ≠ unknown ≠ missing" enforceable in one place (concern #11). Document this in the enum's XML doc so the reviewer does not flag it.

### Why hand-author the lock icon

`Hexalith.Folders.UI` references only the **core** `Microsoft.FluentUI.AspNetCore.Components` package (transitively via `Hexalith.FrontComposer.Shell`); the `…Components.Icons` (`4.14.0`) package is **not** on its reference graph (FrontComposer deliberately hand-rolls SVG paths in `FcFluentIcons` for the same reason). Pulling in the Icons package to get `Icons.Regular.Size16.LockClosed` would add a `<PackageReference>` — forbidden by AC #7. Mirroring `FcFluentIcons` with a single hand-authored `<path>` keeps the change dependency-free and consistent with the codebase. The core `Icon` type and `FluentIcon` component are in the core package (confirmed: `FcSettingsButton.razor` renders `<FluentIcon Value="@FcFluentIcons.Settings20()" />` with the core package only).

### Why no cross-side parity sentinel (contrast Story 6.3)

Story 6.3 installed `DispositionLabelParityTests` in `Hexalith.Folders.Tests` because the C6 disposition mapping exists on **two** sides (server `FolderStateTransitions.GetOperatorDisposition` and UI `DispositionLabelMapper`) and must not drift. Story 6.4's `RedactionDisclosureMapper` has **no** server-side counterpart — the server emits the wire enums; the UI's job is purely *render-time interpretation*. So there is nothing to compare against, and the test project stays UI-only. The protection instead is the **totality `[Theory]`** over each SDK enum: if the OpenAPI spine adds a redaction enum value, the mapper's unhandled-value throw turns the new value into a failing test rather than a silent default — which is the right failure mode for a redaction surface.

### Accessibility & the "never silent" rule

- The critical project rule and F-5 both forbid silent truncation/hiding. Concrete consequences baked into the ACs: (a) the redacted state always renders **text** ("Hidden by tenant policy…") alongside the lock icon — color/icon is never the only signal (UX-DR14, UX-DR30 #1); (b) the `aria-label` for redacted announces the explanation, never an empty string; (c) `Redacted`, `Unknown`, and `Missing` carry distinct `data-fc-disclosure` tokens AND distinct visible renderings (only redacted has the lock). The `Redacted_Unknown_Missing_ProduceThreeDistinctTokens` test is the single guardrail for this whole rule.
- The lock `FluentIcon` is decorative (`aria-hidden="true"`) because the adjacent text carries the meaning — this avoids a screen reader announcing "lock graphic" with no semantic payload while still meeting the visible-affordance requirement for sighted users.

### Story 6.2/6.3 patterns to inherit

- **`data-testid="console-page-{name}-root"`** page-root convention (6.2 AC #4/#10). `RedactionGallery` → `console-page-redaction-gallery-root`.
- **bUnit fixture:** reuse `tests/Hexalith.Folders.UI.Tests/BadgeRenderingFixture.cs` (`BunitContext` + `AddLogging` + `AddFluentUIComponents` + `AddHexalithFrontComposerQuickstart` + `InMemoryStorageService` + substituted `IThemeService` + JS module stubs). `FluentIcon`/`FluentText`/`FluentDataGrid` all need the `AddFluentUIComponents()` registration it provides — do not roll a new context.
- **Component conventions:** `OnParametersSet`-cached private fields, `[Parameter, EditorRequired]` primary input, optional `ColumnHeader`/`AdditionalCssClass`, outer `<span data-testid=…>`, `data-fc-*` value attribute for DataGrid/selector hooks — all established by `OperatorDispositionBadge`/`TechnicalStateMetadata`. Match them exactly.
- **Dev gallery pattern:** `StateLabelGallery.razor` (enum-fed, read-only `FluentDataGrid`, no SDK call, no mutation) + the `@if (Env.IsDevelopment())` Home link guarded by the already-injected `IHostEnvironment`. `RedactionGallery` is the parallel for `FieldDisclosure`. The existing `Home_RendersDevGalleryLink_InDevelopmentOnly` / `Home_HidesDevGalleryLink_InProduction` tests use `CreateHomeContext(environmentName)` which already registers an `IHostEnvironment` substitute — adding a second link in the same dev block does not break them, but add parallel coverage for the new `console-page-home-dev-redaction-gallery-link` if you assert link visibility.
- **No SDK regression:** `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs` must be byte-identical before/after (verify with `git diff --stat src/Hexalith.Folders.Client/Generated/`).
- **`InternalsVisibleTo`:** `Hexalith.Folders.UI.csproj` already exposes internals to `Hexalith.Folders.UI.Tests`. Default to `public` for the component, the page, and the mapper (the gallery and future pages consume them); `internal` is acceptable for the mapper if no cross-assembly consumer needs it — but `public` is simpler and matches `DispositionLabelMapper`.

### Recent commit signals

```
45b51df feat(story-6.4): update orchestration state to IN_PROGRESS and resume from Story 6.4  ← orchestration only
8ebe3f1 feat(agent-config): add uniform-claude preset configuration
dccb2c5 feat(story-6.3): Render operator-disposition labels as primary visual  ← the direct template: mapper + thin components + dev gallery + bUnit
0a76812 feat(story-6.2): Scaffold FrontComposer-hosted read-only operations console  ← console shell, CompositionRoot, bUnit fixture, data-testid convention
4d6efbd feat(story-6.1): Audit and operation-timeline query endpoints  ← emits the redactable audit DTOs Story 6.8 will feed into RedactedField
```

Story 6.4 builds directly on `dccb2c5` (Story 6.3): same `Components/`/`Services/` layout, same `BadgeRenderingFixture`, same dev-gallery + Home-dev-link pattern, same `data-testid`/`data-fc-*`/`OnParametersSet` conventions. No production-tree file outside `Hexalith.Folders.UI` changes.

### Project Structure Notes

- Component lands at `src/Hexalith.Folders.UI/Components/RedactedField.razor` (+ `.razor.cs`) — matches architecture line 1196 (`RedactedField.razor # F-5 lock-icon affordance`).
- Presentation enum + mapper land in `src/Hexalith.Folders.UI/Services/` (`FieldDisclosure.cs`, `RedactionDisclosureMapper.cs`) — consistent with `DispositionLabelMapper.cs` placement (architecture line 1201-1202).
- Icon factory lands at `src/Hexalith.Folders.UI/Components/Icons/FoldersConsoleIcons.cs` — mirrors FrontComposer's `Components/Icons/FcFluentIcons.cs`; satisfies the architecture's `wwwroot/icons/` lock-icon intent (line 1180) with a dependency-free vector `Icon` instead of a raster asset (documented substitution).
- Dev gallery lands at `src/Hexalith.Folders.UI/Components/Pages/RedactionGallery.razor` alongside `StateLabelGallery.razor` (the `Pages/` inventory at architecture line 1185-1192 is descriptive of production pages; a dev-only addition is structurally consistent, exactly as Story 6.3 established).
- No conflict with the unified project structure.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-6.4` (lines 1353-1364) — story statement + epic acceptance criteria]
- [Source: `_bmad-output/planning-artifacts/architecture.md` line 549 — F-5 redaction affordance: visible lock icon + "your tenant policy hides this; contact your administrator"; never silent truncation]
- [Source: `_bmad-output/planning-artifacts/architecture.md` line 110 — cross-cutting concern #11: redacted fields visually distinguished from unknown/missing; silent redaction reads as a bug]
- [Source: `_bmad-output/planning-artifacts/architecture.md` line 168 — UX requirement: redacted/inaccessible/unknown/missing/unavailable states must be visually & semantically distinct; color supplemental only]
- [Source: `_bmad-output/planning-artifacts/architecture.md` lines 480, 116 — S-6 / concern #17: sensitive-metadata classification (paths, branch names, repo names, commit messages) feeds the redaction-vs-unknown UX rule]
- [Source: `_bmad-output/planning-artifacts/architecture.md` lines 1173-1202 — UI source tree: `Components/RedactedField.razor`, `wwwroot/icons/`, `Services/`]
- [Source: `_bmad-output/planning-artifacts/ux-design-specification.md#Design-Requirements` — UX-DR11 (no mutation), UX-DR14/UX-DR30 (color never the only signal), UX-DR16 (restrained Fluent foundations), UX-DR23 (read-only grids)]
- [Source: `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs` — `RedactionMetadataVisibility` (~13213), `DiagnosticFieldClassification` (~11808), `RedactableAuditTimestampPrecision` (~13225), `FileMetadataItemRedaction` (~13045), `RedactionMetadata` (~11560), `RedactableAuditActorReference` (~11595); SDK wire vocabulary — **do not edit the generated file**]
- [Source: `src/Hexalith.Folders.UI/Services/DispositionLabelMapper.cs` + `Components/{OperatorDispositionBadge,TechnicalStateMetadata}.razor(.cs)` — the Story 6.3 mapper + thin-component pattern to mirror]
- [Source: `src/Hexalith.Folders.UI/Components/Pages/StateLabelGallery.razor` + `Components/Pages/Home.razor` — dev-gallery + `@if (Env.IsDevelopment())` Home-link pattern to mirror]
- [Source: `tests/Hexalith.Folders.UI.Tests/BadgeRenderingFixture.cs` + `OperatorDispositionBadgeTests.cs` + `StateLabelGalleryTests.cs` — bUnit fixture + test patterns to reuse]
- [Source: `Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Components/Icons/FcFluentIcons.cs` — hand-authored-SVG `Icon` factory pattern to mirror; core-FluentUI-only icon rendering]
- [Source: `Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Components/Badges/FcStatusBadge.razor.cs` — `ColumnHeader`-driven aria-label template + `OnParametersSet` caching reference]

## Dev Agent Record

### Agent Model Used

claude-opus-4-8 (Claude Opus 4.8, 1M context) — BMAD dev-story workflow

### Debug Log References

- Build (`/mnt/c/Program Files/dotnet/dotnet.exe build src/Hexalith.Folders.UI/Hexalith.Folders.UI.csproj`): **0 warnings / 0 errors** (warnings-as-errors active).
- `dotnet.exe test tests/Hexalith.Folders.UI.Tests` → **114 passed, 0 failed** (the 26 new Story-6.4 tests — mapper totality, RedactedField, RedactionGallery, two new Home redaction-link tests — plus all Story 6.2/6.3 carry-over).
- `dotnet.exe test tests/Hexalith.Folders.UI.E2E.Tests` → **5 passed, 0 failed** (Story 6.2 home-page smoke; Story 6.4 adds no E2E — the redaction gallery is Development-only).
- `dotnet.exe test tests/Hexalith.Folders.Tests` → **1284 passed, 0 failed** (this project references `Hexalith.Folders.UI` since Story 6.3; the additive Story-6.4 UI types break nothing).
- Regression sweep — identical to the Story 6.3 baseline carry-over set (verified there via `git stash`); Story 6.4 added **zero** new reds:
  - `Cli` 691✓, `Mcp` 646✓, `Client` 280✓, `Workers` 19✓ — all green.
  - `Server` — only the pre-existing red `BranchRefPolicyEndpointTests.GetBranchRefPolicyShouldUseSafeDenialEnvelopeForTenantMismatch` (418 passed / 1 failed).
  - `Contracts` — only the six pre-existing OpenAPI/Safety-invariant reds (77 passed / 6 failed: `CommitStatusContractGroupTests`, `SafetyInvariantGateTests` ×2, `FileContextContractGroupTests`, `TenantFolderProviderContractGroupTests`, plus one more contract-group note). All filesystem-include-root / safe-denial environmental, unrelated to UI.
  - `Hexalith.Folders.Testing.Tests` — still the same three Story-6.2-baseline reds (`ScaffoldContractTests.ProjectReferencesFollowAllowedDependencyDirection`, `FixtureContractTests.DeferredArtifactAreasCarryMachineCheckableOwnershipNotes`, `ScaffoldContractTests.SolutionContainsOnlyCanonicalBuildableProjects`); **no new violation** — Story 6.4 added no `<ProjectReference>`.
- **AC #10 redaction-distinction "bite" verification** (each temporary edit, run, revert):
  1. Made `RedactedField` render the lock icon for `Unknown` too → `RedactedFieldTests.Unknown_RendersDistinctly_FromRedacted` AND `Redacted_Unknown_Missing_ProduceThreeDistinctTokens` both **failed** (svg present where empty expected). Reverted. (This run also confirmed `<FluentIcon>` renders an `<svg>` / `SvgSvgElement`, validating the test's `svg` selector.)
  2. Mapped `DiagnosticFieldClassification.Forbidden → Missing` → `FromDiagnosticClassification_*` assertions **failed** (`Forbidden` resolved to `Missing` not `Redacted`). Reverted.
  3. Rendered `Value` in the `Redacted` branch → `Redacted_RendersLockIconAndExplanatoryText` negative assertion **failed** (`"secret-branch"` leaked into markup). Reverted.
  - Full UI suite re-run after all reverts → 114 passed, 0 failed.
- Negative-scope audit: `git diff --stat src/Hexalith.Folders.Client/Generated/` **empty**; no `.csproj`/`.slnx`/`Directory.Packages.props`/`ScaffoldContractTests` changes; forbidden-marker grep over the new production files **zero hits** (the single `ProjectionAvailability` occurrence is the doc-comment recording its intentional non-mapping per AC #2).

### Completion Notes List

- Implemented the F-5 redaction affordance primitive as a dependency-free, UI-only Story (no production-tree edits outside `src/Hexalith.Folders.UI/` and `tests/Hexalith.Folders.UI.Tests/`).
- `FieldDisclosure` (presentation enum, no `[EnumMember]`/wire serialization) is the single rendering classification the four field-shaped SDK redaction enums collapse into — rationale documented in the enum's XML doc so it is not mistaken for a wire-enum copy.
- `RedactionDisclosureMapper` exposes the five total switch-expression overloads; each throws `ArgumentOutOfRangeException` on an unrecognized member (never a silent default). `ProjectionAvailability` is deliberately **not** mapped — its `redacted`/`unknown` members are `TODO(reference-pending C5)` in the generated client; a future story adds that overload once C5 freezes the vocabulary.
- No cross-side parity sentinel exists (contrast Story 6.3's `DispositionLabelParityTests`): the mapper has no server-side counterpart — the server emits the wire enums and the UI purely interprets them at render time. The protection is the per-enum totality `[Theory]`, which turns any future SDK enum addition into a failing test rather than a silent miss.
- `FoldersConsoleIcons.LockClosed16()` hand-authors a closed-padlock SVG path on the core Fluent UI `Icon` abstraction (mirroring `FcFluentIcons`), satisfying the architecture's `wwwroot/icons/` lock-icon intent with a dependency-free vector — avoiding the `…Components.Icons` package reference that AC #7 forbids.
- `RedactedField` renders each disclosure state distinctly: redacted = decorative `aria-hidden` lock `<FluentIcon>` + muted `<FluentText>` explanatory copy ("Hidden by tenant policy — contact your administrator"); unknown/missing = distinct muted text, no lock; visible = the value. Text always carries the meaning (color/icon is never the only signal), `Value` is rendered ONLY in the `Visible` branch (redacted-implies-no-value defense-in-depth), and the three non-visible states carry distinct `data-fc-disclosure` tokens for Story 6.6+ filters/selectors. Pure inline renderer — no mutation, no tooltip/popover/dialog.
- `aria-label` announces the redaction explanation (never silence); omitted only for a Visible field with no value.
- `RedactionGallery` (`/dev/redaction-gallery`, Development-only) feeds itself from `Enum.GetValues<FieldDisclosure>()` with no SDK call, mirroring `StateLabelGallery`; the second dev link was added to `Home.razor` inside the existing `@if (Env.IsDevelopment())` block without disturbing the Story 6.3 link.
- French localization is deferred (English `string` constants/parameters, refactor-not-rewrite) per Story 6.3 precedent — documented here only, `deferred-work.md` untouched.

### File List

New (production — `src/Hexalith.Folders.UI/`):
- `src/Hexalith.Folders.UI/Services/FieldDisclosure.cs`
- `src/Hexalith.Folders.UI/Services/RedactionDisclosureMapper.cs`
- `src/Hexalith.Folders.UI/Components/Icons/FoldersConsoleIcons.cs`
- `src/Hexalith.Folders.UI/Components/RedactedField.razor`
- `src/Hexalith.Folders.UI/Components/RedactedField.razor.cs`
- `src/Hexalith.Folders.UI/Components/Pages/RedactionGallery.razor`

New (tests — `tests/Hexalith.Folders.UI.Tests/`):
- `tests/Hexalith.Folders.UI.Tests/RedactionDisclosureMapperTests.cs`
- `tests/Hexalith.Folders.UI.Tests/RedactedFieldTests.cs`
- `tests/Hexalith.Folders.UI.Tests/RedactionGalleryTests.cs`

Modified:
- `src/Hexalith.Folders.UI/Components/Pages/Home.razor` (added the `/dev/redaction-gallery` dev link)
- `tests/Hexalith.Folders.UI.Tests/ShellCompositionTests.cs` (added two redaction-gallery-link visibility tests)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (story status → in-progress → review)
- `_bmad-output/implementation-artifacts/6-4-implement-sensitive-metadata-redaction-affordance.md` (this story file)

## Senior Developer Review (AI)

**Reviewer:** Jerome — 2026-05-28 (story-automator-review, adversarial / auto-fix mode)
**Outcome:** ✅ **Approved → done.** 0 Critical, 0 High, 0 Medium, 2 Low (both auto-fixed). The implementation is faithful to every acceptance criterion and the negative-scope rules; the story's own claims were independently re-verified, not taken on trust.

### Claims independently verified

- **Build:** `dotnet.exe build tests/Hexalith.Folders.UI.Tests` → **0 warnings / 0 errors** (warnings-as-errors active). Confirmed, not assumed.
- **Tests:** `dotnet.exe test tests/Hexalith.Folders.UI.Tests --no-build` → **114 passed / 0 failed** as claimed (now **117** after the two review additions below).
- **Generated SDK untouched:** `git diff --stat src/Hexalith.Folders.Client/Generated/` is **empty** (AC #8).
- **No dependency drift:** zero `.csproj` / `.slnx` / `Directory.Packages.props` / `Directory.Build.props` changes in the working tree (AC #7).
- **SDK wire vocabulary matches the mapper exactly:** `RedactionMetadataVisibility {Metadata_only, Redacted}`, `RedactableAuditTimestampPrecision {Exact, Bucketed, Redacted}`, `FileMetadataItemRedaction {Not_redacted, Redacted, Excluded, Binary_disallowed}`, `DiagnosticFieldClassification {Consumer_safe, Operator_sanitized, Forbidden}`, and `RedactionMetadata.Visibility` — all present at the cited line ranges; mapper arms are total and the `_ => throw` default is intact on the four switch overloads.
- **Negative-scope markers:** grep of the six new production files for `[Command]`, `AddHexalithDomain`, `AddHexalithEventStore`, `[ProjectionTemplate]`, `<form>`, `<FluentDialog>`, `data-fc-command`, `data-fc-mutation` → **zero hits** (the lone `ProjectionAvailability` mention is the documented non-mapping rationale, AC #2). `ProjectionAvailability` is correctly **absent** from the mapper's switch surface.
- **File List vs git reality:** exact match — no undocumented production change. The only extra working-tree file is `_bmad-output/story-automator/orchestration-*.md` (orchestration state, out of review scope).
- **AC #10 "bite" tests genuinely bite:** inspected — `Unknown_RendersDistinctly_FromRedacted` asserts no `svg`; `FromDiagnosticClassification_*` asserts `Forbidden→Redacted`; `Redacted_RendersLockIconAndExplanatoryText` asserts the value never leaks. Each would fail under the corresponding regression.
- **Redacted-implies-no-value invariant:** `Value` is assigned to `_displayText` only in the `Visible` branch and the redacted `aria-label` is built from the explanation, so the value can leak through neither the body nor the label. Tested directly.

### Findings (2 Low — auto-fixed, test-only, zero production-code change)

1. **[Low][Latent-safety] `FromAuditRedaction` lacked the totality sentinel the story's design depends on.** Dev Notes ("Why no cross-side parity sentinel") stake the redaction surface's safety on a per-enum totality `[Theory]` — "a new SDK enum value becomes a failing test, never a silent default." Four overloads have that protection; `FromAuditRedaction` does not — it consumes `RedactionMetadataVisibility` via an *equality check* (`== Redacted ? … : …`), so a future visibility member would be silently treated as "not redacted → Visible/Missing" (a potential value leak on the redaction surface) with **no failing test**. **Fix:** added `FromAuditRedaction_IsTotal_ForEveryRedactionMetadataVisibility` (`[Theory]` over `Enum.GetValues<RedactionMetadataVisibility>()`) in `RedactionDisclosureMapperTests.cs`, giving this overload the same drift sentinel as the other four. Production behavior unchanged (the AC specifies the equality form deliberately; the gap was test coverage, not the mapping).
2. **[Low][Coverage] AC #4's "omit `aria-label` when `Visible` and `Value` is null/empty" branch was untested.** The existing aria-label `[Theory]` always supplies a value for `Visible`. **Fix:** added `Visible_WithoutValue_OmitsAriaLabel` in `RedactedFieldTests.cs` asserting the attribute is absent — locking the "never announce a stale/empty label" half of the never-silent contract.

Re-ran after both additions: **build 0/0, UI suite 117 passed / 0 failed.**

### Notes for downstream stories (no action required in 6.4)

- `FoldersConsoleIcons` is a `static` factory matching `FcFluentIcons`; when Stories 6.6–6.9 compose `RedactedField`, prefer resolving `Disclosure` through `RedactionDisclosureMapper` so the redacted-vs-unknown-vs-missing distinction stays centralized (concern #11).
- The `RedactedExplanation` / `UnknownText` / `MissingText` overrides use `?? default` (null-coalescing), matching house style; callers should pass `null` (not `""`) to take the default copy — an explicit empty string would render blank, which the never-silent rule forbids. Worth a guard if a future caller ever needs to pass empty strings, but no current/intended caller does, so left as-is.

## Change Log

| Date | Version | Description | Author |
|------|---------|-------------|--------|
| 2026-05-28 | 0.1 | Implemented Story 6.4: `FieldDisclosure` presentation enum, `RedactionDisclosureMapper` (total SDK→presentation maps), hand-authored `FoldersConsoleIcons.LockClosed16()`, the F-5 `RedactedField` affordance component, the Development-only `RedactionGallery`, and 26 bUnit tests. Build clean (0/0); UI 114✓, E2E 5✓; regression sweep shows only the documented pre-existing carry-over reds. Status → review. | Jerome (dev-story / Claude Opus 4.8) |
| 2026-05-28 | 0.2 | Adversarial code review (story-automator-review, auto-fix). Independently re-verified build (0/0), UI tests (114✓), empty generated-SDK diff, no dependency drift, exact SDK-enum match, clean negative-scope grep, and accurate File List. 0 Critical/High/Medium; 2 Low auto-fixed (test-only): added `FromAuditRedaction` totality `[Theory]` (closing the one mapper overload missing the drift sentinel) and a `Visible`-without-value aria-label-omission test. UI suite now 117✓ / 0 failed. Status → done. | Jerome (story-automator-review / Claude Opus 4.8) |
