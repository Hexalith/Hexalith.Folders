---
baseline_commit: 4f6b154
---

# Story 6.5: Author console diagnostic wireflow notes

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an operator and accessibility reviewer,
I want lightweight console wireflow notes for primary diagnostic workflows,
so that implementation of diagnostic pages follows reviewed information hierarchy, interaction states, and accessibility expectations.

## Acceptance Criteria

Source epic AC (epics.md#Story-6.5, lines 1366-1382):

> **Given** PRD console requirements, architecture decisions F-1 through F-7, the FrontComposer technical research, and `_bmad-output/planning-artifacts/ux-design-specification.md` exist
> **When** console wireflow notes are authored
> **Then** folder, workspace, provider, audit, incident-mode, redaction, loading, empty, and error states are described under `docs/ux/ops-console-wireflows.md`
> **And** the notes identify FrontComposer shell layout, navigation, projection-view composition, tenant/user context expectations, read-only command-suppression behavior, and generated/custom projection boundaries
> **And** the notes identify UX-DR1 through UX-DR30 implementation expectations, including keyboard-navigation, focus, non-color-only status, zoom readability, responsive fallback, and redaction-vs-missing behavior for Epic 6 stories
> **And** the notes map UX-DR1 through UX-DR32 to owning and supporting stories, marking console-only requirements separately from cross-surface readiness, lifecycle, parity, status, and evidence semantics
> **And** the notes define the shared visible status taxonomy for readiness, locked, prepared, dirty, committed, audited, failed, stale, unavailable, inaccessible, redacted, and unknown states
> **And** primary diagnostic flows answer what happened, who or what caused it, when it happened, from which surface it came, and whether the evidence can be trusted
> **And** Stories 6.6, 6.7, 6.8, 6.9, and 6.10 cannot begin implementation until `docs/ux/ops-console-wireflows.md` exists and has been reviewed against PRD console requirements, architecture decisions F-1 through F-7, the UX design specification, and the FrontComposer technical research.

**This is a documentation-authoring story.** The single deliverable is the markdown design note `docs/ux/ops-console-wireflows.md`. It writes **no C# code** and changes **no build/test/contract/SDK artifact**. The UX design specification itself names this file and its contract: *"`docs/ux/ops-console-wireflows.md` may expand them into screen-level flows, but it must preserve these IDs"* (`ux-design-specification.md` line 105). Story 6.5 is the **blocking gate** for Stories 6.6, 6.7, 6.8, 6.9, and 6.10 (epic line 1382).

**Architecture anchors:** F-1 through F-7 (`architecture.md` lines 545-551) define the console's framework, hosting, component library, operator-disposition state model, redaction affordance, incident-mode read path, and performance/perceived-wait budget. The C6 Workspace State Transition Matrix (`architecture.md` lines 218-279) is the source-of-truth for the 11 lifecycle states and their operator dispositions. Cross-cutting concern #11 (`architecture.md` line 110): the console is read-only, projection-backed, metadata-only, with redacted fields visually distinct from unknown/missing.

**Form factor:** "Wireflow **notes**" — prose, per-state tables, ASCII layout sketches, and Mermaid flow diagrams (the UX spec already uses Mermaid for the three journeys). **Not** pixel-perfect visual mockups, and **not** a component-API spec. The notes capture reviewed *information hierarchy, interaction states, and accessibility expectations* so Stories 6.6-6.10 implement against a reviewed contract rather than inventing UI-only semantics. The notes **describe** what downstream pages must do; they do **not** pre-build those pages.

Decomposed, testable acceptance criteria. (Tests here are **content/structure checks** against the authored markdown — there is no compiled artifact. Each AC names what a reviewer or a markdown-presence check must be able to confirm.)

1. **The deliverable exists at the canonical path with the required document skeleton.**
   - New file `docs/ux/ops-console-wireflows.md` (creates the `docs/ux/` directory, which does not exist yet).
   - Title `# Operations Console Diagnostic Wireflow Notes`.
   - An **Ownership Metadata** section matching the house convention in `docs/adrs/0000-template.md` and `docs/exit-criteria/_template.md` (`owner_workstream`, `future_test_use`, `known_omissions`, `mutation_rules`, `non_policy_placeholder`, `synthetic_data_only`). `synthetic_data_only: true` — the doc contains **no real tenant/folder/credential data**; all examples are synthetic.
   - A **Scope and Boundary** section stating the console is read-only, projection-backed, metadata-only, MVP-bounded (UX-DR11 / F-2 / concern #11), and that the notes are a *reviewed contract* for Stories 6.6-6.10, not an implementation.
   - A **Downstream Gate** statement (verbatim intent of epic line 1382): Stories 6.6, 6.7, 6.8, 6.9, 6.10 are blocked until this doc exists and has been reviewed against the four review sources (PRD console requirements, architecture F-1 through F-7, the UX design specification, and the FrontComposer technical research).
   - A **References** section citing the four review sources with paths and the specific sections/line ranges relied on.

2. **The notes document the FrontComposer hosting model the diagnostic pages must follow** (epic AC clause 2). A section that identifies, each cited to F-1/F-2 and `technical-frontcomposer-integration-for-hexalith-folders-ui-research-2026-05-11.md` and Story 6.2:
   - **Shell layout** — `FrontComposerShell` as the sole primary layout (`MainLayout.razor` collapses to `<FrontComposerShell>@Body</FrontComposerShell>`); the five regions (Header 48px with start/center/end slots, Navigation 220px sidebar / 48px collapsed, Content, Footer, skip-to-navigation anchor); Blazor Web App + **Interactive Server** render mode throughout (never Static SSR); Fluent UI through the FrontComposer/Shell pattern (UX-DR1, UX-DR16). (Architecture F-1 / the UI-tree comment use the older shorthand "Blazor Server"; the as-built and Story-6.2 framing is "Blazor Web App host using Interactive Server rendering." The notes must use the as-built term and reconcile the two in References — do not "correct" it back to the looser wording.)
   - **Navigation** — `FrontComposerNavigation` convention routes `/{boundedContextLowercase}/{projectionTypeNameKebabCase}`, grouped per bounded context, with the diagnostic workflows (tenant selection, folder/workspace detail, provider health, audit/timeline, incident stream) mapped onto routes; state how the navigation auto-populates from registered domain manifests vs. where a custom nav slot is needed.
   - **Projection-view composition** — generated read-only `FluentDataGrid` projection pages (framework-owned, auto-routed) **vs.** custom hand-authored diagnostic/workflow Razor pages; the four override levels (annotation hints → `[ProjectionTemplate]` → `[ProjectionSlot]` → `[ProjectionViewOverride]` — authoritative in the `Hexalith.FrontComposer.Shell`/`.Contracts` submodule source, e.g. `FrontComposerNavigation.razor.cs` for the route convention and the `[ProjectionTemplate]`/`[ProjectionSlot]`/`[ProjectionViewOverride]` attribute files for the override levels; the research doc describes them generically); and the **data path**: an `IQueryService` adapter wrapping `Hexalith.Folders.Client` returning `QueryResult<T>` (`Items`, `TotalCount`, `ETag`, `IsNotModified` — do **not** invent `payload`/`statusCode` members), **not** `AddHexalithEventStore` (deferred until the server ships `/api/v1/queries` + `/hubs/projection-changes`).
   - **Tenant/user context expectations** — the FrontComposer fail-closed `NullUserContextAccessor` default (returns null tenant/user; `LastUsedValueProvider` no-ops on null/blank) and the requirement to `Services.Replace` it with `FoldersUserContextAccessor` (bridging authenticated `HttpContext.User`/`AuthenticationStateProvider` claims) **before** tenant-scoped queries are enabled. Tenant authority comes from authenticated context + EventStore claim-transform evidence, **never** from request payload (concern #12).
   - **Read-only command-suppression behavior** — FrontComposer normally emits `[Command]` forms when `[Command]` types + handlers are registered; the MVP console suppresses mutation by **not defining `[Command]` projections** and **not calling `AddHexalithEventStore`** (no `/api/v1/commands`). First-phase models are metadata-only read-only POCO projections with no action buttons.
   - **Generated-vs-custom projection boundaries** — generated `.g.cs`/`.g.razor` artifacts live in `obj/`, are never hand-edited, and are produced from hand-written `[Projection][BoundedContext("Folders")]` DTOs; custom diagnostic pages are hand-authored Razor outside the generated projection system. The notes must tell each downstream story which boundary its page sits on.

3. **The notes describe each required view and its state set** (epic AC clause 1). For **each** of the nine required topics, a dedicated subsection covering: purpose; information hierarchy (per UX-DR4 scope-before-evidence, UX-DR18 predictable sections, "summary before tables"); the components it composes (existing 6.3 `OperatorDispositionBadge`/`TechnicalStateMetadata`, 6.4 `RedactedField`; and the planned 6.x components — naming them as *to-be-built-by* their owning story, not built here); interaction states; and accessibility expectations. The nine topics and their owning downstream story:
   - **Folder** view → Story 6.6 (Metadata-Only Folder Tree, UX-DR7/UX-DR12).
   - **Workspace** view → Story 6.6 (Workspace Trust Summary UX-DR5, Trust Matrix UX-DR9, predictable sections UX-DR18).
   - **Provider** view → Story 6.7 (provider binding, credential-reference *identifier/status only*, readiness reason, retryability, capability, sync, failure metadata; UX-DR-cross + FR57; no tokens/credential values/embedded-credential URLs/unauthorized repo existence).
   - **Audit** view → Story 6.8 (Diagnostic Timeline UX-DR8, paginated/filtered/tenant-scoped metadata-only records; FR53/FR54/FR56).
   - **Incident-mode** view → Story 6.9 (`/_admin/incident-stream`; the three F-6 guardrails: persistent red `DEGRADED MODE` banner with last-checkpoint UTC, operator-disposition labels alongside raw event types, one-click "copy correlationId + timestamp window"; lock icons from F-5 still apply — redaction does not relax).
   - **Redaction** state → owned-component Story 6.4 (`RedactedField`/`FieldDisclosure`), consumed by every page; the notes specify *where* redacted-vs-unknown-vs-missing appears in each view.
   - **Loading** state → Story 6.10 (F-7: skeleton at 400ms `SkeletonState`, "still loading… [cancel]" at 2s `StillLoadingCancel`; UX-DR25 layout stability + labeled loading).
   - **Empty** state → all pages (UX-DR20: distinguish no-matches / insufficient-filter-scope / unavailable-read-model / denied-access, no leakage).
   - **Error** state → all pages (safe denial UX-DR21 + canonical Problem Details; reason category, allowed correlation-ID evidence, escalation posture, no unauthorized-resource confirmation).

4. **The notes define the shared visible status taxonomy and reconcile it to its sources-of-truth** (epic AC clause 5). A taxonomy table that, at minimum, defines every term in the epic list — **readiness, locked, prepared, dirty, committed, audited, failed, stale, unavailable, inaccessible, redacted, unknown** — and reconciles the four distinct vocabularies the console juxtaposes so downstream pages do **not** invent UI-only state names:
   - the **11 C6 lifecycle (technical) states** (`requested, preparing, ready, locked, changes_staged, dirty, committed, failed, inaccessible, unknown_provider_outcome, reconciliation_required`) — source: `architecture.md` C6 matrix; surfaced as **secondary** metadata (F-4 / `TechnicalStateMetadata`);
   - the **5 operator dispositions** (`auto-recovering, available, degraded-but-serving, awaiting-human, terminal-until-intervention`) — source: C6 disposition column + `DispositionLabelMapper`; the **primary** visual (F-4 / `OperatorDispositionBadge`);
   - the **4 field-disclosure states** (`Visible, Redacted, Unknown, Missing`) — source: Story 6.4 `FieldDisclosure`; the redaction/absence axis (F-5 / `RedactedField`);
   - the broader **UX-DR15 visible-state list** (`ready, locked, dirty, committed, failed, inaccessible, delayed, unknown, redacted, stale, missing, unavailable, denied, archived`).
   - Each taxonomy row states: term, plain-language meaning, **source-of-truth**, which axis it belongs to (disposition / technical-state / field-disclosure / access), and the required **non-color-only** visual treatment (text + icon-or-shape + semantic color + accessible label, per UX-DR14). The table must call out the deliberate overlaps/distinctions (e.g., `stale`/`delayed` = freshness, distinct from `unavailable` = read-model down; `redacted` ≠ `unknown` ≠ `missing` per F-5/concern #11; `denied`/`inaccessible` safe-denial distinction).

5. **The notes record the UX-DR1 through UX-DR30 console implementation expectations** (epic AC clause 3). For each of UX-DR1..UX-DR30, a one-line console implementation expectation (what a diagnostic page must do to satisfy it). The **accessibility cluster** must be explicitly covered, each with a concrete console expectation: keyboard-navigation (search/filters/result selection/tabs/tables/tree-expansion/detail-panels/dialogs), visible focus + focus order + focus restore on dialog close, **non-color-only** status (text+icon+color+label), **zoom readability at 125%/150%/200%**, **responsive fallback** for desktop/tablet/mobile widths, and **redaction-vs-missing-vs-unknown** distinction. IDs are preserved **verbatim** (`UX-DR1`…`UX-DR30`) per the UX-spec mandate (line 105).

6. **The notes map UX-DR1 through UX-DR32 to owning and supporting stories** (epic AC clause 4). A traceability table with one row per `UX-DR1`..`UX-DR32`, each row listing: **owning** story, **supporting** stories, and a **console-only vs cross-surface** flag. Cross-surface rows must name the upstream semantic owner (readiness → Epic 3; lifecycle/status currency → Epic 4; parity → Epic 5; canonical state vocabulary/error taxonomy → Epic 1 Contract Spine; audit/timeline evidence → Story 6.1 + Epic 4). `UX-DR31` and `UX-DR32` are flagged **release-verified via Story 6.11 + Workstream 7** (not implemented by 6.2-6.10). Every ID `UX-DR1`..`UX-DR32` appears exactly once.

7. **The notes show the primary diagnostic flows answer the five trust questions** (epic AC clause 6). The three UX-spec journeys (Journey 1 find-and-inspect-trust; Journey 2 prove-tenant-isolation; Journey 3 diagnose-failure) are documented (prose + Mermaid), and each is annotated to show how it answers: (1) **what happened**, (2) **who or what caused it**, (3) **when it happened**, (4) **from which surface it came**, (5) **whether the evidence can be trusted**. The "from which surface" dimension ties to cross-surface parity (REST/CLI/MCP/SDK identity + audit actor/surface metadata, FR54). State that these three journeys are the "three critical journeys" referenced by UX-DR32 accessibility validation.

8. **The notes record reference-pending inputs the downstream pages must NOT invent or hard-code** (epic AC clause 7 review-readiness). A "Pending and deferred inputs" section listing, with its current status: **C2 status-freshness target** (TBD — pages render a freshness indicator zone but must not assume a numeric lag), **C3 retention vocabulary** (`reference-pending`), **C4 metadata-filter vocabulary** (rejection-only today; all non-null filters rejected — pages must not expose filter UI that implies unsupported filters work), **`ProjectionAvailability` `redacted`/`unknown`** (reference-pending C5 — not mapped by the UI yet), and **French localization** (deferred to Story 6.11 verification; the notes document localization entry points only — do not extend `deferred-work.md`). Each item is a *pending input*, explicitly not resolved by this story.

9. **Scope discipline — documentation only.** Story 6.5 is doc-only:
   - **Adds only** `docs/ux/ops-console-wireflows.md` (and the `docs/ux/` directory). **No** edits under `src/`, `tests/`, `.slnx`, `Directory.Build.props`, `Directory.Packages.props`, `_bmad/`, the OpenAPI Contract Spine, `src/Hexalith.Folders.Client/Generated/`, or any `tests/tools/*.ps1` gate.
   - **Writes no C# and requires no build/test run.** Verify with `git status --short` that the only non-`_bmad-output/` change is `docs/ux/ops-console-wireflows.md` (plus the new `docs/ux/` directory) — and the sprint-status/story-file updates under `_bmad-output/`.
   - The doc itself must **prescribe no mutation, repair, credential-reveal, file-content, raw-diff, or unrestricted-filesystem affordance** for any view (UX-DR11). It must **invent no UI-only state name** outside the AC #4 reconciled taxonomy and its named sources.
   - The doc must **not** redefine, re-number, or fork the UX-DR identifiers, the C6 states, the operator dispositions, or the `FieldDisclosure` members — it references the source-of-truth and preserves IDs/names verbatim.

10. **Author-side review evidence is captured so the AC-7 gate review has a traceability anchor** (epic AC clause 7). Because the gate requires review against the four sources, the Dev Agent Record records a self-check confirming: all `UX-DR1`..`UX-DR32` IDs are present (32 unique); the AC #4 taxonomy defines all twelve epic terms and reconciles the four vocabularies; each of the nine AC #3 state-sets has a section mapped to its owning story; the five AC #7 trust questions are answerable from the documented journeys; and each of the four review sources is cited in References. The dev confirms the doc renders as valid Markdown (headings, tables, Mermaid fenced blocks) and that the gate statement and pending-inputs are present.

## Tasks / Subtasks

- [x] **Task 1 — Read the four review sources and the prior Epic 6 artifacts end-to-end** (AC: #1-#8)
  - [x] Read `architecture.md` F-1 through F-7 (lines 545-551), the C6 Workspace State Transition Matrix + disposition column (lines 218-279), the read-only console boundary (concern #11, line 110), the UI source tree (lines 1173-1202), and the FrontComposer integration notes (lines 144-148).
  - [x] Read `_bmad-output/planning-artifacts/ux-design-specification.md` in full — the Experience Principles (lines 95-101), the **Stable UX Design Requirements** table UX-DR1..UX-DR32 (lines 103-140, note the line-105 "preserve these IDs" mandate), the Component Strategy anatomy (Workspace Trust Summary, Tenant Scope Banner, Metadata-Only Folder Tree, Diagnostic Timeline, Trust Matrix, Redaction State; lines ~500-620), and the **User Journey Flows** (lines 422-491).
  - [x] Read the PRD console requirements: FR31, FR36, FR52-FR57, and the console NFRs (Operations Console Accessibility WCAG 2.2 AA; status-query 500 ms p95; metadata-only security; read-model determinism; status-freshness; projection scope; sensitive-metadata classification). (`prd.md`.)
  - [x] Read `_bmad-output/planning-artifacts/research/technical-frontcomposer-integration-for-hexalith-folders-ui-research-2026-05-11.md` in full — shell regions, navigation convention, projection composition + override levels, `IUserContextAccessor` fail-closed bridge, command suppression, generated-vs-custom boundaries, `IQueryService`/EventStore-adapter data path.
  - [x] Read prior Epic 6 stories for what already exists and its exact paths/parameters: 6.1 (audit/timeline endpoints + DTOs), 6.2 (shell host, `FoldersUserContextAccessor`, bUnit fixture, `data-testid` convention), 6.3 (`OperatorDispositionBadge`, `TechnicalStateMetadata`, `DispositionLabelMapper`), 6.4 (`RedactedField`, `FieldDisclosure`, `RedactionDisclosureMapper`, `FoldersConsoleIcons`).

- [x] **Task 2 — Create `docs/ux/` and the document skeleton** (AC: #1, #9)
  - [x] Create `docs/ux/ops-console-wireflows.md` with the title, the **Ownership Metadata** block (house convention; `synthetic_data_only: true`), the **Scope and Boundary** section (read-only / projection-backed / metadata-only / MVP), the **Downstream Gate** statement (blocks 6.6-6.10 until reviewed against the four sources), and a placeholder **References** section. No code or test files touched.

- [x] **Task 3 — Author the FrontComposer hosting-model section** (AC: #2)
  - [x] Document shell layout, navigation routes, projection-view composition (generated `FluentDataGrid` vs custom pages + the four override levels), the `IQueryService`-over-`Hexalith.Folders.Client` data path (defer `AddHexalithEventStore`), tenant/user context (`Null`→`FoldersUserContextAccessor` `Services.Replace`, fail-closed, claims-not-payload), and read-only command suppression (no `[Command]`/no command endpoint in MVP). Cite F-1/F-2, the FrontComposer research, and Story 6.2.

- [x] **Task 4 — Author the per-view wireflow notes for all nine state-sets** (AC: #3)
  - [x] One subsection each for folder, workspace, provider, audit, incident-mode, redaction, loading, empty, and error. Each: purpose, information hierarchy, components composed (existing 6.3/6.4 + planned 6.x named by owning story), interaction states, accessibility expectations. Map each to its owning downstream story (6.6 folder+workspace; 6.7 provider; 6.8 audit; 6.9 incident-mode; 6.10 loading; redaction=6.4 component; empty/error=all + safe-denial). Use ASCII layout sketches for hierarchy; keep it "notes," not mockups.

- [x] **Task 5 — Author the shared visible status taxonomy** (AC: #4)
  - [x] Build the taxonomy table covering all twelve epic terms, reconciling the 11 C6 states + 5 dispositions + 4 `FieldDisclosure` members + UX-DR15 list. Each row: term, meaning, source-of-truth, axis, non-color-only visual treatment. Call out `stale`/`delayed` vs `unavailable`, `redacted` ≠ `unknown` ≠ `missing`, and `denied` vs `inaccessible` safe-denial distinctions.

- [x] **Task 6 — Author the UX-DR1..UX-DR30 implementation-expectations section** (AC: #5)
  - [x] One line per UX-DR (IDs verbatim) with the console expectation. Explicitly cover the accessibility cluster (keyboard, focus order/visible/restore, non-color-only, zoom 125/150/200%, responsive fallback, redaction-vs-missing-vs-unknown).

- [x] **Task 7 — Author the UX-DR1..UX-DR32 → story traceability map** (AC: #6)
  - [x] One row per UX-DR with owning story, supporting stories, and console-only-vs-cross-surface flag (name the upstream Epic 1-5 semantic owner for cross-surface rows). Flag UX-DR31/UX-DR32 as release-verified via 6.11 + Workstream 7. Confirm 32 unique IDs.

- [x] **Task 8 — Author the three diagnostic journeys with the five-trust-question annotation** (AC: #7)
  - [x] Document Journey 1/2/3 (prose + Mermaid), annotating each with what-happened / who-caused / when / from-which-surface / evidence-trust. Name them as UX-DR32's "three critical journeys."

- [x] **Task 9 — Author the pending/deferred-inputs section and finalize References** (AC: #8, #1)
  - [x] List C2 (freshness TBD), C3 (retention reference-pending), C4 (filter rejection-only), `ProjectionAvailability` (reference-pending C5), and FR localization (deferred to 6.11) as pending inputs the pages must not invent. Complete the References section citing the four review sources with line ranges.

- [x] **Task 10 — Self-review against the gate and record evidence** (AC: #9, #10)
  - [x] `git status --short`: confirm the only non-`_bmad-output/` change is `docs/ux/ops-console-wireflows.md`; no `src/`/`tests/`/`.slnx`/`Directory.*`/spine/generated edits.
  - [x] Confirm all 32 UX-DR IDs present (unique), all 12 taxonomy terms defined, all 9 state-sets present with owning-story mapping, the 5 trust questions answerable, all 4 review sources cited, the gate + pending-inputs sections present, and the Markdown/Mermaid renders. Record this self-check in the Dev Agent Record so the AC-7 review gate has a traceability anchor.

## Dev Notes

### Scope boundaries (read first)

- **In scope:** authoring one Markdown design note, `docs/ux/ops-console-wireflows.md`, that becomes the reviewed contract for Stories 6.6-6.10. It captures: the FrontComposer hosting model the pages must follow; per-view information hierarchy + interaction states + accessibility expectations for the nine required state-sets; the reconciled shared status taxonomy; the UX-DR1..30 console expectations; the UX-DR1..32 → story traceability map; the three diagnostic journeys answering the five trust questions; and the reference-pending inputs the pages must not invent.
- **OUT of scope (do NOT do here):**
  - **Any C# / Razor / test / project-file change.** This story builds no component and runs no build. The components the notes *describe* are built by their owning downstream stories (folder/workspace pages 6.6, provider 6.7, audit/timeline 6.8, incident-mode 6.9, perceived-wait 6.10). The notes name `DegradedModeBanner`, `CorrelationCopyButton`, `IncidentStream`, `SkeletonState`, `StillLoadingCancel`, `HumanFormatter`, `WorkspaceTrustSummary`, `TenantScopeBanner`, `MetadataOnlyFolderTree`, `DiagnosticTimeline`, `TrustMatrix` as *to-be-built-by their owning story* — they are **not** created in 6.5.
  - **Resolving reference-pending platform inputs.** C2 status-freshness target, C3 retention durations, C4 filter vocabulary, `ProjectionAvailability` redacted/unknown freeze, and French localization are owned elsewhere; the notes document them as *pending*, with placeholder-safe rendering guidance, and do not invent values.
  - **Redefining IDs or vocabularies.** UX-DR identifiers, C6 state names, operator-disposition labels, and `FieldDisclosure` members are existing contracts — reference them, preserve them verbatim, never re-number or fork.
  - **Full visual mockups / a component-API spec / an ops-console *architecture* doc.** This is "wireflow notes": prose + per-state tables + ASCII sketches + Mermaid. Detailed service-layer architecture (if needed) is a separate artifact, not 6.5.
  - **Editing the planning artifacts** (`ux-design-specification.md`, `architecture.md`, `prd.md`, `epics.md`) or the FrontComposer submodule. These are read-only inputs.
- **Negative-scope guard for the dev:** if you find yourself editing anything under `src/`, `tests/`, a `.csproj`/`.slnx`/`Directory.*`, the OpenAPI spine, the generated client, a `_bmad/` skill, a `tests/tools/*.ps1` gate, or a sibling submodule — stop. None of those are in 6.5's surface area. The only product-tree artifact this story creates is `docs/ux/ops-console-wireflows.md`.

### Why this story is documentation, not code

Story 6.5 is the deliberate "design before build" gate in Epic 6. Epic line 1382 makes it a hard blocker: Stories 6.6-6.10 (the diagnostic pages, incident-mode path, and perceived-wait UX) "cannot begin implementation until `docs/ux/ops-console-wireflows.md` exists and has been reviewed." The point is to fix the information hierarchy, state taxonomy, and accessibility expectations once — reviewed against PRD + architecture + UX spec + FrontComposer research — so five downstream stories implement against a single reviewed contract instead of each re-deriving (and risking drift in) the status vocabulary, the redaction-vs-unknown rule, the read-only boundary, or the FrontComposer composition model. The UX spec pre-commits the filename and the "preserve these IDs" rule (line 105), so the contract surface is already constrained.

### The four review sources (the authority this doc consumes)

| Source | Path | What 6.5 must consume |
|---|---|---|
| Architecture F-decisions + C6 | `_bmad-output/planning-artifacts/architecture.md` | F-1..F-7 (lines 545-551); C6 matrix + disposition column (218-279); read-only boundary concern #11 (line 110); UI tree (1173-1202); FrontComposer notes (144-148) |
| UX design specification | `_bmad-output/planning-artifacts/ux-design-specification.md` | Experience Principles (95-101); UX-DR1..32 (103-140, **preserve IDs** per line 105); Component Strategy anatomy (~500-620); User Journey Flows (422-491) |
| PRD console requirements | `_bmad-output/planning-artifacts/prd.md` | FR31, FR36, FR52-FR57; console NFRs (WCAG 2.2 AA; 500 ms p95 status queries; metadata-only; read-model determinism; status-freshness; projection scope; sensitive-metadata classification) |
| FrontComposer technical research | `_bmad-output/planning-artifacts/research/technical-frontcomposer-integration-for-hexalith-folders-ui-research-2026-05-11.md` | Shell regions; nav convention; projection composition + 4 override levels; `IUserContextAccessor` fail-closed bridge; command suppression; generated-vs-custom boundaries; `IQueryService` data path |

### What already exists (so the notes describe composition, not reinvention)

These are **built** (Stories 6.1-6.4) and must be referenced by exact name/path; the notes specify how downstream pages compose them, never re-derive them:

- **`OperatorDispositionBadge.razor`** (`src/Hexalith.Folders.UI/Components/`) — F-4 primary visual; param `[EditorRequired] OperatorDispositionLabel Disposition` (+ `ColumnHeader`, `AdditionalCssClass`); wraps `FcStatusBadge`; resolves slot/label via `DispositionLabelMapper`; `data-testid="operator-disposition-badge"`. (Story 6.3.)
- **`TechnicalStateMetadata.razor`** — F-4 **secondary** metadata; `[EditorRequired] LifecycleState State` (+ `ColumnHeader`, `IncludePrefix`); muted `FluentText`; `data-fc-technical-state`. (Story 6.3.)
- **`DispositionLabelMapper.cs`** (`src/Hexalith.Folders.UI/Services/`) — C6 → disposition (`ResolveDisposition(LifecycleState, bool hasProjectionLagEvidence = false)` — the `hasProjectionLagEvidence` flag is what splits `ready` into `available` vs `degraded-but-serving`; do **not** document `ready` as unconditionally `available`), disposition → badge slot (`ResolveSlot`), disposition → label (`ResolveLabel`), state → wire name (`ResolveTechnicalStateLabel`). Drift-guarded by `DispositionLabelParityTests` against server `FolderStateTransitions.GetOperatorDisposition`. (Story 6.3.)
- **`RedactedField.razor`** + **`FieldDisclosure`** (`Visible/Redacted/Unknown/Missing`) + **`RedactionDisclosureMapper`** + **`FoldersConsoleIcons.LockClosed16()`** — F-5 affordance; redacted = lock icon + "Hidden by tenant policy — contact your administrator"; unknown/missing distinct, no icon; value never leaks in redacted state; `data-fc-disclosure` token. (Story 6.4.) The incident-mode notes must reuse this (epic 6.9: redacted renders through the shared component with no relaxed policy).
- **Audit/timeline REST endpoints + DTOs** under `/api/v1/folders/{folderId}/audit-trail` and `/operation-timeline` (`AuditTrailPage`, `AuditRecord`, `OperationTimelinePage`, `OperationTimelineEntry`, `RedactionMetadata`, `RedactableAudit*Reference`, `DiagnosticStateTransition`, `FreshnessMetadata`, `PaginationMetadata`). Authorization-before-observation: `LayeredFolderAuthorizationService.AuthorizeAsync(... StrictRead(), OperationScope = folderId)` runs before any read-model access. (Story 6.1.) The audit-view notes must reflect that pages trust this boundary and never client-pre-filter.
- **Shell host** — Blazor Web App + Interactive Server; `FrontComposerShell` sole layout; `FoldersUserContextAccessor` replaces the fail-closed default; bUnit `BadgeRenderingFixture`; `data-testid="console-page-{name}-root"`; `FocusOnNavigate Selector="h1"` + single `<h1>` per page. (Story 6.2.)

### Status-taxonomy reconciliation (the heart of AC #4)

The console juxtaposes **four** vocabularies; the notes must define one shared visible taxonomy that maps each epic term to its source axis. Do not collapse them — operators read disposition (primary) + technical state (secondary) + field disclosure together:

| Axis | Source-of-truth | Members | Console role |
|---|---|---|---|
| Operator disposition | C6 disposition column + `DispositionLabelMapper` | `auto-recovering`, `available`, `degraded-but-serving`, `awaiting-human`, `terminal-until-intervention` | **Primary** visual (F-4 badge) |
| Technical lifecycle state | C6 matrix (11 states) | `requested, preparing, ready, locked, changes_staged, dirty, committed, failed, inaccessible, unknown_provider_outcome, reconciliation_required` | **Secondary** metadata (F-4) |
| Field disclosure | Story 6.4 `FieldDisclosure` | `Visible, Redacted, Unknown, Missing` | Per-field redaction/absence (F-5) |
| Access / availability | UX-DR10/15/20/21/26 + safe-denial (6.1/Epic 2) + freshness (C2/Epic 4) | `denied, inaccessible, stale/delayed, unavailable, missing, archived` | Cross-cutting evidence semantics |

Epic-line-1380 terms map as: `readiness`→provider/workspace readiness (Epic 3/4 + disposition); `locked`/`prepared`/`dirty`/`committed`→C6 technical states (`prepared` ≈ `ready` after preparation); `audited`→audit-evidence presence (Story 6.1/6.8); `failed`/`inaccessible`→C6 + disposition `terminal-until-intervention`; `stale`→freshness (C2); `unavailable`→read-model down; `redacted`/`unknown`→`FieldDisclosure`. Spell these reconciliations out so 6.6-6.9 cannot drift.

### Form, conventions, and house style for the doc

- **Metadata header:** follow `docs/adrs/0000-template.md` / `docs/exit-criteria/_template.md` — an `## Ownership Metadata` block with `owner_workstream`, `future_test_use`, `known_omissions`, `mutation_rules`, `non_policy_placeholder`, `synthetic_data_only`. Set `synthetic_data_only: true` and use only synthetic example identifiers (`acme`, `tenant-a`, `folder-123`, `task-…`) — never real tenant/folder/credential data (concern #6 metadata-only + the project rule that docs examples are metadata-only).
- **Diagrams:** reuse Mermaid `flowchart` for the three journeys (the UX spec already does, lines 428-482). ASCII boxes for per-view layout hierarchy are fine and keep it "notes."
- **Cite sources inline** the way prior stories' "References" do: `[Source: _bmad-output/planning-artifacts/architecture.md line 549 — F-5 …]`.
- **Markdown hygiene:** the repo uses `.editorconfig` (LF for YAML/shell; the rest CRLF/UTF-8, trailing whitespace trimmed, final newline). New Markdown follows the same `.editorconfig` resolution as other `docs/` files.
- **No localization in the doc body** beyond documenting localization entry points (English; FR parity is deferred to Story 6.11 — this story owns the *note* about it, not the implementation).

### Recent commit signals

```
4f6b154 feat(story-6.4): Implement sensitive metadata redaction affordance   ← baseline; F-5 RedactedField + FieldDisclosure shipped
45b51df feat(story-6.4): update orchestration state to IN_PROGRESS
dccb2c5 feat(story-6.3): Render operator-disposition labels as primary visual ← F-4 badge + DispositionLabelMapper
0a76812 feat(story-6.2): Scaffold FrontComposer-hosted read-only operations console ← shell host, FoldersUserContextAccessor, bUnit fixture
4d6efbd feat(story-6.1): Audit and operation-timeline query endpoints          ← audit/timeline DTOs the 6.8 page renders
```

Commit convention: `feat(story-6.5): <description>`. This story's commit touches `docs/ux/ops-console-wireflows.md` and the `_bmad-output/` story/sprint bookkeeping only.

### Project Structure Notes

- Deliverable lands at `docs/ux/ops-console-wireflows.md` — the exact path the UX spec (line 105) and the epic (lines 1376, 1382) name. Creates the new `docs/ux/` directory alongside existing `docs/{adrs,contract,exit-criteria,sdk}/`.
- Consistent with the established `docs/` convention (ownership-metadata header + section structure mirroring ADR / exit-criteria templates).
- No conflict with the unified project structure: this is a new docs subtree, not a source/test change.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-6.5` (lines 1366-1382) — story statement + acceptance criteria + the 6.6-6.10 blocking gate]
- [Source: `_bmad-output/planning-artifacts/epics.md#Epic-6` (lines 1305-1311) — Epic 6 scope: "implements UX-DR1 through UX-DR30 directly; UX-DR31 and UX-DR32 verified through Story 6.11 and release-evidenced through Workstream 7"; consume shared semantics, do not invent UI-only state]
- [Source: `_bmad-output/planning-artifacts/architecture.md` lines 545-551 — F-1 (Blazor Server) / F-2 (SignalR, projections-only, no aggregate access) / F-3 (Fluent UI, WCAG 2.2 AA) / F-4 (operator-disposition primary visual) / F-5 (redaction lock-icon) / F-6 (incident-mode `/_admin/incident-stream` + 3 guardrails) / F-7 (p95<1.5s, p99<3s, degraded 5s; skeleton 400ms; cancel 2s)]
- [Source: `_bmad-output/planning-artifacts/architecture.md` lines 218-279 — C6 Workspace State Transition Matrix: 11 states + operator-disposition column; `DispositionLabelMapper.cs` sourced from this table (line 279)]
- [Source: `_bmad-output/planning-artifacts/architecture.md` line 110 — cross-cutting concern #11: read-only, projection-backed, metadata-only console; redacted distinct from unknown/missing]
- [Source: `_bmad-output/planning-artifacts/architecture.md` lines 1173-1202 — `Hexalith.Folders.UI` source tree + console component inventory (Pages, Layout, Components, Services)]
- [Source: `_bmad-output/planning-artifacts/ux-design-specification.md` lines 103-140 — UX-DR1..UX-DR32 (line 105: `docs/ux/ops-console-wireflows.md` "must preserve these IDs")]
- [Source: `_bmad-output/planning-artifacts/ux-design-specification.md` lines 422-491 — User Journey Flows 1/2/3 (Mermaid) + Journey Patterns]
- [Source: `_bmad-output/planning-artifacts/ux-design-specification.md` ~lines 500-620 — Component Strategy anatomy: Workspace Trust Summary, Tenant Scope Banner, Metadata-Only Folder Tree, Diagnostic Timeline, Trust Matrix, Redaction-and-Inaccessibility State]
- [Source: `_bmad-output/planning-artifacts/prd.md` — FR31, FR36, FR52-FR57; NFR Operations Console Accessibility (WCAG 2.2 AA, keyboard, non-color-only, focus, zoom); NFR status-query 500 ms p95; NFR security/metadata-only; NFR read-model determinism; NFR status-freshness; NFR ops-console projection scope; NFR sensitive-metadata classification]
- [Source: `_bmad-output/planning-artifacts/research/technical-frontcomposer-integration-for-hexalith-folders-ui-research-2026-05-11.md` — FrontComposerShell regions; FrontComposerNavigation convention; ProjectionRenderer + 4 override levels; `IUserContextAccessor` fail-closed `Null`→`Folders` bridge (`Services.Replace`); command suppression (no `[Command]`/no `AddHexalithEventStore` in MVP); generated `.g.cs` vs custom boundaries; `IQueryService`-over-`Hexalith.Folders.Client` data path]
- [Source: `_bmad-output/implementation-artifacts/6-1-audit-and-operation-timeline-query-endpoints.md` — audit/timeline endpoints + DTOs + authorization-before-observation; redaction-consistency invariant]
- [Source: `_bmad-output/implementation-artifacts/6-2-scaffold-frontcomposer-hosted-read-only-operations-console.md` — shell host, `FoldersUserContextAccessor`, Interactive Server, `data-testid` + `FocusOnNavigate` conventions]
- [Source: `_bmad-output/implementation-artifacts/6-3-render-operator-disposition-labels-as-primary-visual.md` — `OperatorDispositionBadge`, `TechnicalStateMetadata`, `DispositionLabelMapper`, C6 parity test]
- [Source: `_bmad-output/implementation-artifacts/6-4-implement-sensitive-metadata-redaction-affordance.md` — `RedactedField`, `FieldDisclosure`, `RedactionDisclosureMapper`, `FoldersConsoleIcons`; redacted ≠ unknown ≠ missing rule; note: "FR parity is owned by Story 6.5 (wireflow notes) + Story 6.11 (verification)"]
- [Source: `Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Components/Layout/FrontComposerNavigation.razor.cs` — authoritative source for the `/{boundedContextLowercase}/{projectionTypeNameKebabCase}` nav route convention; and `Hexalith.FrontComposer.Contracts/Attributes/` for the `[ProjectionTemplate]`/`[ProjectionSlot]`/`[ProjectionViewOverride]` override levels (the research doc describes these only generically)]
- [Note: architecture F-1 (line 545) and the UI-tree comment (line 1173) say "Blazor Server"; Story 6.2's epic AC and as-built host are a "Blazor Web App host using Interactive Server rendering." The wireflow notes must use the as-built term and reconcile the two — do not revert to the architecture's looser shorthand.]
- [Source: `docs/adrs/0000-template.md` + `docs/exit-criteria/_template.md` — Ownership-Metadata header convention for `docs/` artifacts to mirror]

## Dev Agent Record

### Agent Model Used

claude-opus-4-8[1m] (Claude Opus 4.8, 1M context)

### Debug Log References

- Read phase parallelized via a background workflow (`read-story-6-5-sources`, run `wf_51e59b88-e1a`): six readers over architecture.md, ux-design-specification.md, prd.md, the FrontComposer research doc, prior stories 6.1–6.4, and the FrontComposer submodule. The highest-fidelity sources (UX-DR1..32 table, the three journey Mermaid diagrams, F-1..F-7, the C6 matrix, concern #11/#12, PRD FRs/NFRs) were also read directly to guarantee verbatim IDs/names.
- Scope-discipline gate (**at dev completion**): `git status --short` confirmed the only non-`_bmad-output/` change was `docs/ux/` (the new `ops-console-wireflows.md` + its directory). No `src/`, `.slnx`, `Directory.*`, OpenAPI spine, generated-client, `_bmad/`, or `tests/tools/*.ps1` edits by the dev step. **Note (reconciled at review):** the later `bmad-qa-generate-e2e-tests` QA-automation step subsequently added `tests/Hexalith.Folders.Testing.Tests/OpsConsoleWireflowNotesTests.cs` (the executable content/structure gate). That is expected QA output — not a dev scope violation under AC #9 — and is now recorded in the File List.
- Markdown/structure checks run against the deliverable: 32 unique `UX-DR` IDs (1..32, none missing/duplicated/renumbered); 12 epic taxonomy terms defined; 9 per-view state-sets; 3 `mermaid` flowcharts; balanced code fences; 6 Ownership-Metadata fields; LF line endings (repo `* text=auto` normalization); no trailing whitespace; final newline; no secret/real-data sentinels.

### Completion Notes List

Story 6.5 is a documentation-authoring story; it writes no C# and runs no build/test. The single product-tree deliverable is `docs/ux/ops-console-wireflows.md` (815 lines). Validation is content/structure checks against the authored Markdown, recorded above and below.

AC-7 gate-review self-check (traceability anchor):

- ✅ AC #1 — deliverable exists at the canonical path with Title, Ownership Metadata (`synthetic_data_only: true`), Scope and Boundary, Downstream Gate (verbatim intent of epic line 1382), and a References section citing the four review sources with line ranges.
- ✅ AC #2 — FrontComposer hosting model documented (§1): shell layout (as-built Blazor Web App + Interactive Server, reconciled vs F-1 "Blazor Server"), D2 navigation route convention (confirmed in submodule `FrontComposerNavigation.BuildRoute`), projection-view composition + override levels (as-built: only `[ProjectionTemplate]` is an attribute; slot/view-override are descriptor records — reconciled vs the research doc), `IQueryService`/`QueryResult<T>` data path (defer `AddHexalithEventStore`), tenant/user context (`Null`→`FoldersUserContextAccessor` `Services.Replace`, claims-not-payload), and read-only command suppression.
- ✅ AC #3 — nine per-view state-sets (§3) for folder, workspace, provider, audit, incident-mode, redaction, loading, empty, error, each with purpose / information hierarchy (ASCII sketch) / components composed (shipped 6.3/6.4 + planned 6.x named by owning story) / interaction states / accessibility, mapped to owning stories (6.6/6.6/6.7/6.8/6.9/6.4/6.10/all/all).
- ✅ AC #4 — shared visible status taxonomy (§2): all 12 epic terms defined and the four vocabularies reconciled (5 dispositions + 11 C6 states + 4 `FieldDisclosure` members + UX-DR15 list), with stale/delayed vs unavailable, redacted ≠ unknown ≠ missing, and denied vs inaccessible distinctions called out; `ready`→`available` documented as conditional on `hasProjectionLagEvidence`.
- ✅ AC #5 — UX-DR1..UX-DR30 console implementation expectations (§4), IDs verbatim, with an explicit accessibility cluster (keyboard, focus order/visible/restore, non-color-only, zoom 125/150/200%, responsive fallback, redaction-vs-missing-vs-unknown).
- ✅ AC #6 — UX-DR1..UX-DR32 → story traceability map (§5): one row per ID, owning + supporting stories + console-only/cross-surface flag + upstream semantic owner for cross-surface rows; UX-DR31/UX-DR32 flagged release-verified via Story 6.11 + Workstream 7; 32 unique IDs.
- ✅ AC #7 — three diagnostic journeys (§6, prose + verbatim Mermaid), each annotated with the five trust questions (what happened / who-or-what caused / when / from-which-surface / evidence-trust); named as UX-DR32's "three critical journeys".
- ✅ AC #8 — pending/deferred inputs (§7): C2 freshness TBD, C3 retention reference-pending, C4 filter rejection-only, `ProjectionAvailability` redacted/unknown reference-pending C5, French localization deferred to 6.11 — each as a pending input the pages must not invent.
- ✅ AC #9 — scope discipline: doc-only; at dev completion `git status --short` was clean of any `src/`/`tests/`/project-file/spine/generated edits (the QA step's later `OpsConsoleWireflowNotesTests.cs` is expected test-automation output, recorded in the File List); the doc prescribes no mutation/repair/credential-reveal/file-content/raw-diff/unrestricted-filesystem affordance and invents no UI-only state name outside the §2 taxonomy.
- ✅ AC #10 — this self-check is recorded as the AC-7 review traceability anchor; Markdown/Mermaid render validity confirmed.

### File List

- `docs/ux/ops-console-wireflows.md` — **new**. The single product-tree deliverable: operations-console diagnostic wireflow notes (the reviewed contract for Stories 6.6–6.10). Creates the new `docs/ux/` directory.

Test-automation artifact (added by the later `bmad-qa-generate-e2e-tests` QA step, not the dev step):

- `tests/Hexalith.Folders.Testing.Tests/OpsConsoleWireflowNotesTests.cs` — **new**. 17-fact content/structure validation gate over `docs/ux/ops-console-wireflows.md` (executable form of the AC-7 gate self-review). See `_bmad-output/implementation-artifacts/tests/6-5-test-summary.md`. 17/17 passing.

Bookkeeping (permitted story/sprint tracking, under `_bmad-output/`):

- `_bmad-output/implementation-artifacts/6-5-author-console-diagnostic-wireflow-notes.md` — task checkboxes, Dev Agent Record, Change Log, Status.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — story status ready-for-dev → in-progress → review; `last_updated` bumped.

## Senior Developer Review (AI)

**Reviewer:** Jerome · **Date:** 2026-05-28 · **Outcome:** Approve (auto-fixed) · **Status:** done

**Method.** Adversarial AC-by-AC validation of `docs/ux/ops-console-wireflows.md` against its four
source-of-truth files (PRD, architecture, UX spec, FrontComposer research) plus the FrontComposer
submodule, the as-built `src/Hexalith.Folders.UI`, and the Story 6.1–6.4 artifacts. Eight per-AC
finder passes, each independently re-checked by an adversarial verifier (false-positive guard). The
executable content/structure gate `OpsConsoleWireflowNotesTests` was run before and after fixes:
**17/17 passing** both times.

**Findings: 0 CRITICAL, 2 HIGH, 4 MEDIUM, 3 LOW — all auto-fixed.** (4 candidate findings were
rejected on verification, including all three AC-#1 "C4/C5 misattribution" claims — the doc faithfully
mirrors the OpenAPI spine's own C4/C5 usage and its governing AC — and the AC-#7 journeys came back
clean.)

| Sev | AC | Finding | Fix applied |
| --- | --- | --- | --- |
| HIGH | 2 | §1.4 presented `FoldersClientFacade` + an `IQueryService`/`QueryResult<T>` adapter as the **as-built** data path. Reality: Story 6.2 wired `AddFoldersClient(…)` + `BearerTokenDelegatingHandler` directly (`CompositionRoot.cs:151-158`); no facade/adapter exists in `src/`. `FoldersClientFacade` is only a *planned* tree entry (`architecture.md:1201`). | Rewrote §1.4 into **As-built (6.2 direct SDK)** vs **prescribed future path** (`IQueryService` adapter / `QueryResult<T>` / `FoldersClientFacade`), keeping the verified `QueryResult<T>` members and the `AddHexalithEventStore` deferral. |
| HIGH | 6 | §5 assigned `UX-DR11` (read-only boundary) to verify-only Story 6.11, contradicting the legend ("primarily implemented"), §1.6, and `epics.md:1309`. | Reassigned `UX-DR11` owning → **6.2** (supporting "all 6.x (6.11 verifies)"). |
| MED | 6 | `UX-DR23`/`UX-DR30` likewise owned by 6.11 despite §1.6 (6.2 command-suppression guard) and §4.1 (6.2 `FocusOnNavigate`). | Reassigned `UX-DR23`/`UX-DR30` owning → **6.2** (6.11 verifies). Added a legend clause for scaffold-invariant vs vocabulary-defining ownership. |
| MED | 4 | §2.2 `unknown` row dropped the `awaiting-human`/Warning disposition for C6 `unknown_provider_outcome`, violating its own §2.3 rule. | Split the row's treatment: per-field token (neutral) vs lifecycle state (`awaiting-human` Warning badge). |
| MED | 3 | §3 preamble promised an ASCII sketch for every view; 6 of 9 lacked one. | Added ASCII layout sketches to §3.3 Provider, §3.4 Audit, §3.7 Loading, §3.8 Empty, §3.9 Error; clarified §3.6 redaction is a cross-cutting component. |
| MED | 3 | §3.9 Problem Details shape listed `taskId?`, absent from the canonical A-8 / Story 6.1 error contract. | Removed `taskId?`; aligned the list to A-8 and footnoted that task scope is an audit-DTO field. |
| LOW | 5 | §4.1 zoom bullet anchored only `UX-DR31`. | Re-anchored to `UX-DR30` zoom resilience / `UX-DR31` multi-zoom verification. |
| LOW | 6 | `UX-DR13`/`UX-DR15` owned by doc-only 6.5. | Added a legend clause: for vocabulary-defining requirements, owning = the story that authors the canonical vocabulary contract (6.5). |
| LOW | 8-10 | File List omitted the QA-generated test; the dev scope-discipline self-check read as stale present tense. | Recorded the test in the File List (attributed to the QA step) and qualified the self-check to "at dev completion". |

**Post-fix verification.** Markdown fences balanced (even ` ``` `); exactly 3 Mermaid blocks; §4 = 30
`UX-DR` rows (no 31/32); §5 = 32 unique rows; `OpsConsoleWireflowNotesTests` 17/17 passing. No
CRITICAL issues remain → status **done**.

## Change Log

| Date | Change |
| --- | --- |
| 2026-05-28 | Authored `docs/ux/ops-console-wireflows.md` — FrontComposer hosting model, shared visible status taxonomy (12 terms / 4 vocabularies reconciled), nine per-view wireflow notes, UX-DR1..30 console expectations, UX-DR1..32 → story traceability, three diagnostic journeys answering the five trust questions, and pending/deferred inputs. Story 6.5 complete (doc-only; no code/build/test changes); status set to review. |
| 2026-05-28 | Senior Developer Review (AI, adversarial) — auto-fixed 2 HIGH + 4 MEDIUM + 3 LOW findings in `docs/ux/ops-console-wireflows.md` (as-built data path §1.4, `unknown` disposition §2.2, ASCII sketches + `taskId?` removal §3, zoom anchor §4.1, `UX-DR11`/`23`/`30` ownership + legend §5). Reconciled File List (QA-added `OpsConsoleWireflowNotesTests.cs`) and the scope-discipline self-check. Gate 17/17; 0 CRITICAL. Status review → done. |
