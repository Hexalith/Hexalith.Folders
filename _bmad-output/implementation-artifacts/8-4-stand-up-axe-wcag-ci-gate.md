# Story 8.4: Stand up an automated axe/WCAG 2.2 AA CI gate for the operations console

Status: backlog

<!-- Created 2026-06-22 via bmad-correct-course (sprint-change-proposal-2026-06-22.md). Epic 8 closure story. -->

## Story

As a release stakeholder,
I want an automated accessibility (axe-core / WCAG 2.2 AA) gate wired into CI against the read-only operations console,
So that the PRD's accessibility release-validation path (NFR-A11Y-1..5, NFR-VER-3) is satisfied by enforced evidence rather than a library-choice assertion.

## Context

The 2026-06-22 readiness review (Critical #4) found WCAG 2.2 AA is asserted via the Fluent UI primitive choice (architecture F-3) and a manual UX test plan, but **no automated axe/WCAG conformance gate is wired into CI** (absent from gate list I-5). The current responsive coverage (`ResponsiveViewportSmokeTests`) is a deliberately non-brittle smoke and does not assert no-clipping/zoom/dense-identifier stress (UX-DR31). Story 6-2 provides the stable read-only console routes and selectors this gate targets; the UI E2E lane uses Playwright on .NET.

## Acceptance Criteria

1. **Given** the read-only console routes from Story 6-2, **when** an automated accessibility gate is added, **then** it runs axe-core (or equivalent WCAG 2.2 AA ruleset) against each primary console route and fails CI on AA violations.
2. **Given** the accessibility-critical console flows (find-and-inspect-trust-state, prove-tenant-isolation, diagnose-failure-from-evidence), **when** the gate runs, **then** it covers keyboard navigation, visible focus, semantic headings/table structure, contrast, and not-color-alone status indicators (NFR-A11Y-1..5).
3. **Given** the Playwright-on-.NET E2E lane, **when** the gate is wired, **then** it integrates with the existing UI E2E harness (`tests/install-playwright.ps1` browser provisioning) and is registered in the CI gate inventory (closes the I-5 absence).
4. **Given** the PRD release-validation requirement (NFR-VER-3), **when** the gate is green, **then** its run is the recorded accessibility release-validation evidence (no longer "asserted via component choice").
5. **Given** UX-DR31 responsive requirements, **when** the gate runs, **then** it adds at least the zoom (125/150/200%) and dense-identifier no-clipping assertions the current responsive smoke omits.

## References

- Readiness review §4 (accessibility gap) and Critical #4.
- Architecture F-3 (Fluent UI), gate list I-5.
- Console routes/selectors: Story 6-2; verification: Story 6-11.
- Sprint Change Proposal: `../planning-artifacts/sprint-change-proposal-2026-06-22.md`.
