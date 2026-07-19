---
source: sprint-change-proposal-2026-07-07-honest-green-gate-baseline.md
source_date: 2026-07-07
source_status: approved-and-applied
reconciled_against:
  - prd.md
  - .memlog.md
addendum_present: false
disposition: no-prd-body-change-approved-verification-governance
---

# Reconciliation — Honest-Green UI E2E + Accessibility Gate Baseline

## Source purpose and status

This approved and applied `bmad-correct-course` proposal closes an Epic 8 action item by making the UI E2E and accessibility gate baseline durable during Epic 11 CI, test-helper, UI, and cleanup refactoring. Existing runtime gates were already green; the change pins their enforcers in the governance register, adds preservation acceptance criteria to Stories 11.3, 11.7, 11.11, and 11.13, adds a consolidated conformance guard, and marks the action item done.

The source explicitly states that all nine downstream edits were user-approved, applied, and verified. It classifies the change as moderate backlog/acceptance-criteria reorganization plus one conformance test, with no PRD, architecture, MVP, production, CI behavior, or wire-contract change.

## PRD-relevant product decisions

1. **Accessibility remains a release obligation.** WCAG 2.2 AA and the approved diagnostic-console accessibility outcomes must be exercised by release evidence rather than treated as advisory prose.
2. **Verification must be honest-green.** CI refactoring must not make required UI E2E or accessibility coverage appear green by narrowing, skipping, masking, or making a required job non-blocking.
3. **The complete approved lane matters.** A focused subset cannot substitute for the full current UI E2E and accessibility release-evidence population.
4. **Gate preservation is behavior-preserving governance.** The proposal changes how evidence is protected, not the product's user-visible scope or semantics.
5. **Evidence wiring must remain traceable across refactors.** Renaming/moving tests or helpers must update their governance pins in the same change so coverage is not silently orphaned.

These are verification consequences of existing product requirements; they do not introduce a new capability.

## Already covered in the current PRD

### Exact identifiers and sections

- **FR36** requires the operations console to remain read-only and excludes editing or file-content browsing, a core boundary the UI E2E lane must preserve.
- **FR52–FR57** define the tenant-scoped operations-console, audit, incident-view, redaction, and provider-readiness behaviors exercised by UI and diagnostic flows.
- **Non-Functional Requirements → Operations Console Accessibility** already requires WCAG 2.2 AA, keyboard navigation, non-color-only indicators, visible focus, semantic headings, readable tables, sufficient contrast, and browser-zoom readability.
- **Non-Functional Requirements → Verification Expectations** already requires accessibility and operations-console usability release evidence before MVP acceptance.
- **API Backend Specific Requirements → Contract and Quality Gates** already treats drift, redaction, security, provider, and contract evidence as blocking quality gates rather than optional checks.
- **MVP Contract Summary** establishes that generated implementations, parity oracles, and tests cannot override the product/contract authorities; weakened tests cannot redefine missing evidence as conformance.
- **Open Release Items → OQ10** requires the release-calibration plan to freeze populations, scenarios, methods, evidence owners, and approval rules before results are accepted. The full-lane/no-narrowing principle is consistent with that evidence-governance model.

The PRD does not give stable numeric IDs to individual accessibility or verification NFR bullets. Naming the exact sections is therefore the correct traceability form.

### Memlog alignment

- The memlog records zero-tolerance safety, a read-only console, a bounded incident-mode exception, and the C13/evidence authority hierarchy. Honest-green gates reinforce these decisions rather than modify them.
- The memlog records OQ10 as the release-calibration evidence blocker. A CI lane that silently narrows its population would conflict with that decision's purpose.
- No memlog entry prescribes job names, PowerShell scripts, test FQNs, forbidden-substring sets, reflection checks, or a fixed UI-test count. Those remain implementation-governance details.

## Genuine PRD gaps

There is **no product-body gap**. The PRD already states the accessibility outcomes and requires release validation evidence. A new FR or NFR that names `e2e-gates`, `accessibility-gates`, `--filter`, CI YAML, specific scripts, conformance classes, or a fixed test count would turn a current implementation baseline into a product contract.

The source is not listed in PRD `inputDocuments` and has no source-specific memlog reconciliation entry. If the wider July update records all user-supplied sprint-change sources, add it as an **approved implementation-governance input** and log that it produced no PRD-body change. This is provenance only, not new product scope.

## Conflicts and supersession

### Fixed “63-test” wording is not a durable product denominator

The approved source repeatedly describes a full 63-test UI E2E lane. That is valid baseline evidence for the applied change, but a growing suite may legitimately contain more tests. The durable invariant is “run the complete current approved lane without narrowing or skipping,” not “always run exactly 63 tests.”

Disposition: preserve the historical 63 count in the proposal and pin map if needed for that baseline; do not copy it into the PRD or treat it as a stable product metric.

### Exact enforcement topology is downstream-owned

The proposal pins two job names, two scripts, five sibling conformance classes, a consolidated test, reflective method assertions, and a forbidden-substring list. None conflicts with the current PRD, but none is a stable product mechanism. Future equivalent enforcement may be re-homed if it remains blocking, complete, and evidence-backed.

Disposition: keep all exact topology in CI/governance artifacts; preserve the outcome, not the mechanism, at PRD level.

### No conflict with current product or memlog decisions

The source is explicitly no-scope-change and no-wire-change. It does not supersede any stable FR, NFR, open release item, or memlog decision.

## Implementation, story, and governance detail that stays out of the PRD

- The `e2e-gates` and `accessibility-gates` job names, their blocking syntax, and the PowerShell script names.
- The exact 63-test baseline and focused verification counts such as 4/4 or 42/42.
- `E2eCiWorkflowConformanceTests`, `AccessibilityCiWorkflowConformanceTests`, `ConsoleAxeWcagGateTests`, `HonestGreenGateBaselineConformanceTests`, and the five-class compile-time pin set.
- The exact no-filter token checks and AD7 forbidden-substring set, including `upload-artifact`, `secrets.`, `services:`, `dotnet publish`, `docker`, `playwright install`, and `--recursive`.
- The use of `[Fact]`, `typeof`, reflection, specific method names, file paths, FQN pins, and deletion/build-break mechanics.
- Story 11.1 pin-map rows and handoff constraints; Story 11.3/11.7/11.11/11.13 acceptance-criteria edits; Epic 8 action-item resolution; sprint-status header changes.
- Developer/Test Architect ownership, current build/test results, and the assertion that no additional dev handoff is required.

No `addendum.md` exists. The approved proposal and its governance register are the right places for this implementation detail; the PRD should retain only the accessibility outcomes and release-evidence obligation already present.

## Recommended stable-ID edits and additions

- **New FRs:** none.
- **FR edits:** none.
- **NFR additions/edits:** none.
- **Renumbering:** none.
- **Downstream traceability:** cite FR36, FR52–FR57, Operations Console Accessibility, Verification Expectations, Contract and Quality Gates, and OQ10.
- **Metadata:** optionally record the source as an approved, applied implementation-governance input with no product-body effect.

## Qualitative ideas at risk

- “Green” means every required lane is blocking and actually ran its complete approved population.
- Refactoring may re-home enforcement but may never narrow, skip, mask, or silently delete it.
- Accessibility coverage and general UI E2E coverage are separate required signals; one cannot substitute for the other.
- Gate tests need protection from accidental deletion or weakening during CI/test-helper renames.
- A full-lane invariant should remain future-proof as test counts grow; the historical 63 count is evidence, not the enduring requirement.
- The change closes a governance gap without changing production behavior or MVP scope.

## Concise disposition

**No PRD body or stable-ID change.** The PRD already requires the relevant console accessibility outcomes and release evidence. Record source provenance if the July inputs are enumerated, and keep exact jobs, scripts, counts, forbidden tokens, conformance classes, Story 11 ACs, and sprint-status mechanics in downstream governance artifacts.
