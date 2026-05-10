---
project: Hexalith.Folders
date: 2026-05-10
workflow: bmad-correct-course
mode: batch
status: approved
approvedAt: 2026-05-10
triggerArtifact: D:\Hexalith.Folders\_bmad-output\planning-artifacts\implementation-readiness-report-2026-05-10.md
changeScope: moderate
---

# Sprint Change Proposal - Readiness Story Split

## 1. Issue Summary

### Trigger

The implementation readiness assessment completed on 2026-05-10 produced an overall status of `NEEDS WORK`.

This is a pre-implementation backlog correction. No completed code needs rollback.

### Core Problem

The PRD, architecture, and epics are aligned at the requirements level, but the backlog still contains execution-quality defects that should be corrected before sprint planning:

- Four stories are too large to implement, review, and verify as independent units.
- Two early enabling stories need stronger demonstrable value and validation criteria.
- One lifecycle capacity story is worded as work for a future epic rather than immediate validation readiness.
- Operations-console UX requirements are embedded in architecture and epics but do not yet have a lightweight flow-level review artifact.

### Evidence

Evidence comes from `implementation-readiness-report-2026-05-10.md`:

- PRD FR coverage: 57/57.
- Missing FR coverage: 0.
- Critical issues: 0.
- Major issues: 5.
- Minor issues: 3.
- Overall readiness status: `NEEDS WORK`.

The remaining issues are story sizing and implementation readiness defects, not product-scope or architecture defects.

## 2. Impact Analysis

### Epic Impact

Epic 1 remains valid, but Stories 1.1 and 1.3 should be reframed so they prove consumer/maintainer value, not only artifact creation. Story 1.8 should split into smaller Contract Spine authoring stories.

Epic 2 remains unchanged.

Epic 3 remains unchanged.

Epic 4 remains valid. Story 4.17 should be reworded to make the lifecycle capacity harness valuable immediately, while still feeding Epic 7 calibration later.

Epic 5 remains unchanged.

Epic 6 remains valid, but should gain a lightweight console wireflow/design-notes story before diagnostic page implementation. Story 6.5 should split into separate diagnostic page stories.

Epic 7 remains valid, but Story 7.4 and Story 7.11 should split into independently reviewable release-readiness and documentation stories.

### Artifact Impact

PRD: no product scope change required.

Architecture: no technology, component, or decision change required. Existing decisions already support this correction, especially Contract Spine decisions, operations-console decisions F-1 through F-7, and release-readiness gates.

UX: no standalone UX spec exists. Add lightweight console wireflow notes through Epic 6 so implementation can be reviewed without creating a full separate UX workflow.

Epics: update `epics.md` story list, affected acceptance criteria, and story numbering. Preserve the existing 57/57 FR coverage map.

Sprint status: after proposal approval and epic edits, synchronize `D:\Hexalith.Folders\_bmad-output\implementation-artifacts\sprint-status.yaml`.

## 3. Recommended Approach

Selected path: Direct Adjustment.

Effort: Medium.

Risk: Low to Medium.

Rationale: the MVP remains achievable and the architecture remains compatible. The fastest safe correction is to split oversized stories, strengthen acceptance criteria, add one lightweight UX review story, synchronize sprint tracking, and rerun readiness.

Alternatives considered:

- Rollback: not applicable; implementation has not started.
- MVP review: not needed; no PRD scope conflict was found.
- New epic: not needed; the affected work fits naturally inside existing epics.

## 4. Detailed Change Proposals

### Proposal 1: Reframe Story 1.1 Around Consumer-Buildable Scaffold

Story: 1.1

Section: Title and acceptance criteria

OLD:

```markdown
### Story 1.1: Scaffold solution skeleton mirroring Hexalith.Tenants

As a platform engineer,
I want a solution skeleton with the architecture-approved projects and test structure,
So that every subsequent story has a stable, convention-compliant home.

**Then** `Hexalith.Folders.slnx` contains the expected src, test, and sample projects
**And** project references follow the architecture dependency direction and target .NET 10.
```

NEW:

```markdown
### Story 1.1: Establish a consumer-buildable module scaffold

As a platform engineer and downstream consumer,
I want the Hexalith.Folders solution scaffold to build with the approved project layout,
So that consumers and later stories have a stable, convention-compliant module baseline.

**Then** `Hexalith.Folders.slnx` contains the expected src, test, and sample projects
**And** project references follow the architecture dependency direction and target .NET 10
**And** `dotnet build` succeeds for the scaffold without requiring provider credentials, tenant data, or initialized nested submodules.
```

Rationale: keeps the greenfield setup story, but makes the user-visible outcome a buildable baseline.

### Proposal 2: Strengthen Story 1.3 Placeholder Acceptance

Story: 1.3

Section: Title and acceptance criteria

OLD:

```markdown
### Story 1.3: Seed normative fixtures and placeholder artifacts

As a maintainer,
I want normative fixture and artifact placeholders created,
So that later contract, parity, redaction, encoding, and load tests have stable paths.

**Then** the audit leakage corpus, parity schema, previous spine, and idempotency encoding corpus exist under `tests/fixtures`
**And** `tests/load`, `tests/tools/parity-oracle-generator`, `docs/exit-criteria/_template.md`, and `docs/adrs/0000-template.md` exist.
```

NEW:

```markdown
### Story 1.3: Seed minimally valid normative fixtures

As a maintainer,
I want normative fixtures and artifact templates to be minimally valid and owned by later gates,
So that contract, parity, redaction, encoding, and load tests have stable inputs rather than empty placeholders.

**Then** the audit leakage corpus, parity schema, previous spine, and idempotency encoding corpus exist under `tests/fixtures` with minimal valid content
**And** fixture schemas or smoke validation prove the files are parseable and intentionally incomplete where applicable
**And** `tests/load`, `tests/tools/parity-oracle-generator`, `docs/exit-criteria/_template.md`, and `docs/adrs/0000-template.md` exist with ownership notes linking them to later CI or release-readiness stories.
```

Rationale: prevents placeholder-only completion that gives later stories paths but no meaningful validated inputs.

### Proposal 3: Split Story 1.8 Into Four Contract-Spine Stories

Story: 1.8

Section: replace story with four smaller stories and renumber following Epic 1 stories.

OLD:

```markdown
### Story 1.8: Author workspace, lock, file, context, commit, status, and audit contract groups

As an API consumer and adapter implementer,
I want workspace task lifecycle operations represented in the Contract Spine,
So that REST, SDK, CLI, and MCP surfaces can be generated from complete canonical operations.

**Given** the shared contract vocabulary exists
**When** lifecycle contract groups are authored
**Then** prepare, lock, release, file add/change/remove, context query, commit, status, audit, and ops-console query operations have schemas
**And** schemas preserve the content boundary by excluding file contents from events, audit, logs, diagnostics, and console payloads.
```

NEW:

```markdown
### Story 1.8: Author workspace and lock contract groups

As an API consumer and adapter implementer,
I want workspace preparation and lock operations represented in the Contract Spine,
So that task lifecycle entry and concurrency behavior are canonical before implementation.

**Given** the shared contract vocabulary exists
**When** workspace and lock contract groups are authored
**Then** prepare, lock, release, lock-inspection, state-transition, and retry-eligibility operations have schemas
**And** each operation declares authorization, idempotency, error, audit, and parity metadata where applicable.

### Story 1.9: Author file mutation and context query contract groups

As an API consumer and adapter implementer,
I want file mutation and context query operations represented in the Contract Spine,
So that file changes and read-only context access preserve the same policy boundaries across surfaces.

**Given** workspace and lock contract groups exist
**When** file and context contract groups are authored
**Then** file add/change/remove, tree, metadata, search, glob, and bounded range-read operations have schemas
**And** path, binary, range, result-limit, content-boundary, and secret-safe response rules are declared.

### Story 1.10: Author commit and workspace-status contract groups

As an API consumer and adapter implementer,
I want commit and status operations represented in the Contract Spine,
So that clean committed states, failed states, and unknown provider outcomes are reported consistently.

**Given** lifecycle command contract groups exist
**When** commit and status contract groups are authored
**Then** commit, commit evidence, workspace status, task status, provider outcome, and reconciliation status operations have schemas
**And** final state, retry eligibility, retry-after, correlation, and canonical error metadata are declared.

### Story 1.11: Author audit and ops-console query contract groups

As an operator and audit reviewer,
I want audit and ops-console query operations represented in the Contract Spine,
So that diagnostics and incident reconstruction use metadata-only read models.

**Given** lifecycle status contract groups exist
**When** audit and ops-console query contract groups are authored
**Then** audit trail, operation timeline, readiness, lock, dirty-state, failed-operation, provider-status, and sync-status queries have schemas
**And** schemas exclude file contents, diffs, provider tokens, credential material, secrets, and unauthorized resource existence.
```

Renumber current Epic 1 stories after the split:

- Current Story 1.9 becomes Story 1.12.
- Current Story 1.10 becomes Story 1.13.
- Current Story 1.11 becomes Story 1.14.
- Current Story 1.12 becomes Story 1.15.
- Current Story 1.13 becomes Story 1.16.

Rationale: preserves scope while making each contract area independently reviewable.

### Proposal 4: Reword Story 4.17 To Remove Future-Epic Framing

Story: 4.17

Section: value statement

OLD:

```markdown
So that Epic 7 can later calibrate measured C1, C2, and C5 targets.
```

NEW:

```markdown
So that lifecycle scenarios capture capacity dimensions early and provide reusable evidence for release calibration.
```

Rationale: the story should deliver immediate lifecycle validation value. Epic 7 can still consume the evidence later.

### Proposal 5: Add Console Wireflow Notes Before Diagnostic Page Build

Epic: 6

Section: insert before current Story 6.5.

NEW:

```markdown
### Story 6.5: Author console diagnostic wireflow notes

As an operator and accessibility reviewer,
I want lightweight console wireflow notes for primary diagnostic workflows,
So that implementation of diagnostic pages follows reviewed information hierarchy, interaction states, and accessibility expectations.

**Acceptance Criteria:**

**Given** PRD console requirements and architecture decisions F-1 through F-7 exist
**When** console wireflow notes are authored
**Then** folder, workspace, provider, audit, incident-mode, redaction, loading, empty, and error states are described under `docs/ux/ops-console-wireflows.md`
**And** the notes identify keyboard-navigation, focus, non-color-only status, zoom readability, and redaction-vs-missing expectations for Epic 6 stories.
```

Rationale: addresses the missing standalone UX document without requiring a full UX-design workflow.

### Proposal 6: Split Story 6.5 Into Three Diagnostic Page Stories

Story: current 6.5

Section: replace after adding new wireflow story.

OLD:

```markdown
### Story 6.5: Build diagnostic pages for folder, workspace, provider, and audit state

As an operator,
I want diagnostic pages for folder detail, workspace status, provider health, and audit trail,
So that readiness, lock, dirty state, commit, failure, and provider support evidence are inspectable.

**Given** projection endpoints and reusable components exist
**When** diagnostic pages render
**Then** pages show authorized metadata for lifecycle, readiness, lock, dirty, commit, failure, provider support, and audit
**And** no file editing, file browsing, raw diff, credential reveal, or mutation control is present.
```

NEW:

```markdown
### Story 6.6: Build folder and workspace diagnostic pages

As an operator,
I want folder and workspace diagnostic pages,
So that lifecycle, readiness, lock, dirty state, commit state, failure state, and cleanup status are inspectable.

**Given** projection endpoints, reusable status components, and console wireflow notes exist
**When** folder and workspace diagnostic pages render
**Then** pages show authorized lifecycle, readiness, lock, dirty, commit, failure, cleanup, freshness, and correlation metadata
**And** no file editing, file browsing, raw diff, credential reveal, repair action, or mutation control is present.

### Story 6.7: Build provider readiness and support diagnostic pages

As an operator,
I want provider readiness and support diagnostic pages,
So that provider binding, credential-reference status, capability differences, and provider failure evidence are inspectable without secrets.

**Given** projection endpoints, provider support evidence, and console wireflow notes exist
**When** provider diagnostic pages render
**Then** pages show authorized provider binding, credential-reference identifier/status, readiness reason, retryability, remediation category, capability, sync, and failure metadata
**And** provider tokens, credential values, embedded credential URLs, and unauthorized repository existence are never displayed.

### Story 6.8: Build audit and operation-timeline diagnostic pages

As an audit reviewer and operator,
I want audit and operation-timeline diagnostic pages,
So that incidents can be reconstructed from metadata-only evidence.

**Given** audit projection endpoints and console wireflow notes exist
**When** audit and timeline pages render
**Then** records are paginated, filtered, tenant-scoped, and show actor, task, operation, correlation, folder, provider, timestamp, result, duration, state transition, and sanitized error category where authorized
**And** sensitive metadata classification and redaction affordances are applied consistently.
```

Renumber remaining Epic 6 stories:

- Current Story 6.6 becomes Story 6.9.
- Current Story 6.7 becomes Story 6.10.
- Current Story 6.8 becomes Story 6.11.

Rationale: separates distinct operator workflows and adds a minimal UX review artifact before page implementation.

### Proposal 7: Split Story 7.4 Into Four CI Gate Stories

Story: 7.4

Section: replace story and renumber later Epic 7 stories.

OLD:

```markdown
### Story 7.4: Consolidate PR CI workflow gates

As a maintainer,
I want PR CI workflows consolidated across build, format, lint, unit, contract, parity, sentinel, and capacity-smoke gates,
So that every pull request gets the same quality signal.

**Given** feature implementation projects exist
**When** `.github/workflows/ci.yml` runs
**Then** build, format, lint, unit, contract, parity, sentinel, redaction, cache-key, and capacity-smoke gates execute
**And** failures block merge.
```

NEW:

```markdown
### Story 7.4: Consolidate baseline build and unit CI gates

As a maintainer,
I want baseline build, format, lint, and unit gates consolidated in PR CI,
So that every pull request proves the solution is mechanically healthy.

**Given** feature implementation projects exist
**When** `.github/workflows/ci.yml` runs
**Then** restore, build, format, lint, and unit-test gates execute with stable caching and clear failure categories
**And** failures block merge.

### Story 7.5: Consolidate contract and parity CI gates

As a maintainer,
I want contract and parity gates consolidated in PR CI,
So that public surface drift is caught before merge.

**Given** Contract Spine, generated client, and parity oracle artifacts exist
**When** `.github/workflows/ci.yml` runs
**Then** server-vs-spine validation, generated-client consistency, parity-oracle schema validation, and cross-surface parity checks execute
**And** failures block merge with actionable artifact names.

### Story 7.6: Consolidate security and redaction CI gates

As a maintainer and security reviewer,
I want sentinel, redaction, forbidden-field, and tenant cache-key gates consolidated in PR CI,
So that leaks of file contents, secrets, provider tokens, credential material, or tenant data block merge.

**Given** security fixtures and redaction pipelines exist
**When** `.github/workflows/ci.yml` runs
**Then** sentinel-corpus, redaction, forbidden-field, and tenant-prefixed cache-key checks execute
**And** failures identify the emitting channel without exposing sensitive payloads.

### Story 7.7: Add capacity-smoke CI gate

As a maintainer,
I want a lightweight capacity-smoke gate in PR CI,
So that obvious lifecycle performance regressions are caught before release calibration.

**Given** lifecycle capacity harness scenarios exist
**When** `.github/workflows/ci.yml` runs
**Then** smoke scenarios for prepare, lock, mutate, commit, and status paths execute with non-production thresholds
**And** failures block merge while final C1, C2, and C5 targets remain owned by release calibration.
```

Renumber current Epic 7 stories after the split:

- Current Story 7.5 becomes Story 7.8.
- Current Story 7.6 becomes Story 7.9.
- Current Story 7.7 becomes Story 7.10.
- Current Story 7.8 becomes Story 7.11.
- Current Story 7.9 becomes Story 7.12.
- Current Story 7.10 becomes Story 7.13.
- Current Story 7.11 is replaced by Proposal 8.
- Current Story 7.12 becomes the final runbook/ADR story after Proposal 8.

Rationale: separates unrelated gate families and makes failures easier to own and review.

### Proposal 8: Split Story 7.11 Into Documentation And NFR Traceability Stories

Story: current 7.11

Section: replace story.

OLD:

```markdown
### Story 7.11: Publish operations, audit, provider, and error documentation

As an operator or audit reviewer,
I want operations, audit, provider, and error documentation published,
So that production diagnosis and escalation are repeatable.

**Given** operations surfaces and provider contracts exist
**When** operator documentation is published
**Then** operations console, metadata-only audit, provider integration/testing, error catalog, retryability, client action, alerting, and backup/recovery guidance are documented
**And** `docs/exit-criteria/nfr-traceability.md` maps every PRD NFR bullet to story, architecture exit criterion, automated gate, or release evidence.
```

NEW:

```markdown
### Story 7.14: Publish operations and audit documentation

As an operator or audit reviewer,
I want operations-console and metadata-only audit documentation published,
So that production diagnosis and incident reconstruction are repeatable.

**Given** operations console and audit surfaces exist
**When** operations and audit documentation is published
**Then** console workflows, metadata-only audit fields, redaction behavior, incident-mode use, alerting handoff, and backup/recovery expectations are documented
**And** examples avoid file contents, provider tokens, credential material, secrets, and unauthorized resource details.

### Story 7.15: Publish provider and error documentation

As an operator and integration maintainer,
I want provider integration, retryability, and canonical error documentation published,
So that provider failures and client actions are diagnosable without reading implementation code.

**Given** provider contracts and canonical error taxonomy exist
**When** provider and error documentation is published
**Then** provider integration/testing, supported versions, drift handling, error catalog, retryability, retry-after behavior, and client action guidance are documented
**And** GitHub and Forgejo capability differences are explicit.

### Story 7.16: Publish NFR traceability bridge

As a release reviewer,
I want every PRD NFR bullet mapped to implementation evidence,
So that MVP acceptance can prove non-functional coverage rather than rely on narrative claims.

**Given** release gates, architecture exit criteria, and story evidence exist
**When** `docs/exit-criteria/nfr-traceability.md` is published
**Then** every PRD NFR bullet maps to story IDs, architecture exit criteria, automated gates, manual validation evidence, or release artifacts
**And** missing NFR evidence fails the release-readiness review.
```

Rationale: documentation and NFR evidence are different review surfaces and should be accepted independently.

### Proposal 9: Synchronize Story Numbering And FR Coverage Map

Artifact: `epics.md`

Change:

- Update the Epic List story counts after splits.
- Update any intra-document references affected by renumbering.
- Preserve FR coverage at 57/57.
- Keep Epic 7 as cross-cutting validation with no new product FR scope.

Rationale: the split changes story IDs and must not create traceability drift.

### Proposal 10: Synchronize Sprint Status After Approved Epic Edits

Artifact: `D:\Hexalith.Folders\_bmad-output\implementation-artifacts\sprint-status.yaml`

Change:

- Update story entries for Epic 1, Epic 6, and Epic 7 after `epics.md` is revised.
- Preserve current statuses for unaffected stories.
- Add new split stories as `backlog` unless an existing status already applies to their originating work.

Rationale: sprint planning must consume the corrected story list, not the pre-split list.

## 5. Checklist Summary

| Checklist Item | Status | Notes |
| -------------- | ------ | ----- |
| 1.1 Triggering story | [x] | Trigger is readiness report, not a single implementation story. |
| 1.2 Core problem | [x] | Backlog executability/story sizing issue. |
| 1.3 Evidence | [x] | Readiness report plus affected story excerpts. |
| 2.1 Current epic impact | [x] | Affects Epics 1, 4, 6, and 7. |
| 2.2 Epic-level changes | [x] | Modify existing epic stories; no new epic. |
| 2.3 Remaining epic review | [x] | Epics 2, 3, and 5 unchanged. |
| 2.4 Future epic invalidation | [x] | No epic invalidated. |
| 2.5 Epic order | [x] | No epic resequencing required. |
| 3.1 PRD conflict | [x] | No PRD scope change. |
| 3.2 Architecture conflict | [x] | No architecture change required. |
| 3.3 UI/UX impact | [x] | Add lightweight console wireflow notes. |
| 3.4 Other artifacts | [x] | Sprint status needs synchronization after approval. |
| 4.1 Direct adjustment | [x] | Viable; medium effort, low-medium risk. |
| 4.2 Rollback | [N/A] | No implementation has started. |
| 4.3 MVP review | [N/A] | MVP remains achievable. |
| 4.4 Path selected | [x] | Direct Adjustment. |
| 5.1 Issue summary | [x] | Included. |
| 5.2 Impact analysis | [x] | Included. |
| 5.3 Recommendation | [x] | Included. |
| 5.4 MVP action plan | [x] | Included. |
| 5.5 Handoff plan | [x] | Included below. |
| 6.1 Checklist review | [x] | Complete for draft proposal. |
| 6.2 Proposal accuracy | [x] | Ready for user review. |
| 6.3 User approval | [x] | Approved by Jerome on 2026-05-10. |
| 6.4 Sprint status update | [x] | `sprint-status.yaml` synchronized after approved epic edits. |
| 6.5 Next steps | [x] | Rerun implementation readiness, then proceed to sprint planning if clean. |

## 6. Implementation Handoff

Scope classification: Moderate.

Recommended routing:

- Product Owner / Developer agent: apply the `epics.md` story splits, renumber affected stories, and preserve FR coverage.
- Developer agent: synchronize `sprint-status.yaml` after the backlog edits are approved and applied.
- Readiness workflow: rerun `bmad-check-implementation-readiness` after edits.
- Sprint planning: run `bmad-sprint-planning` only after readiness returns `READY` or the remaining risk is explicitly accepted.

Success criteria:

- Oversized stories identified in the readiness report are split.
- Story IDs and references are internally consistent.
- FR coverage remains 57/57.
- Console wireflow notes exist before diagnostic page implementation.
- `sprint-status.yaml` matches the revised story list.
- Readiness assessment can proceed without the same major story-sizing findings.

## 7. Approval Needed

This proposal was approved by Jerome on 2026-05-10 and applied to the planning artifacts.

Approval question:

Applied changes:

- Revised `epics.md` story splits and renumbering for Epics 1, 6, and 7.
- Updated Story 4.17 value wording.
- Synchronized `sprint-status.yaml` with the revised story list.

Next recommended step: rerun `bmad-check-implementation-readiness`.
