# Story 8.5: Close C3 Legal sign-off and drive the residual test baseline honestly green

Status: backlog

<!-- Created 2026-06-22 via bmad-correct-course (sprint-change-proposal-2026-06-22.md). Epic 8 closure story. -->

## Story

As a release stakeholder,
I want C3 retention to receive Legal sign-off and the residual non-composition test reds resolved or explicitly accepted,
So that the MVP release rests on a fully-approved governance posture and an honestly-green solution test baseline.

## Context

Two remaining release-acceptance threads after the 2026-06-22 course correction:

- **C3 Legal sign-off.** PM approval was recorded 2026-06-22 (Jerome); C3 `status` stays `reference_pending` with `release_blocking_until_legal_approval`. Legal is the **sole remaining gate**. On Legal sign-off, the values cascade is already wired (Story 7.11) — only the approval record and status flip remain.
- **Residual non-composition reds** (NOT the 7.18 composition gap, which closed green 2026-05-31). The full-solution run surfaced: `Testing.Tests` ×4 governance/scaffold failures; `Contracts.Tests` ×4 epic-1 CLI negative-scope failures (Murat's separate retro action item — tests now failing because the CLI exists); `UI.E2E.Tests` ×40 environment failures (Playwright Chromium not installed). The Epic 3 provider-boundary guard reds (Octokit/Dapr dependency-boundary) initially grouped with 7.18 are also non-composition and belong here.

## Acceptance Criteria

1. **Given** PM approval is recorded, **when** Legal signs off on the C3 per-data-class retention values and tenant-deletion dispositions, **then** `docs/exit-criteria/c3-retention.md` and `c0-c13-governance-evidence.yaml` flip C3 to `approved`, the `release_blocking_until_legal_approval` posture clears, and `run-retention-deletion-gates.ps1` reports approved.
2. **Given** the `Testing.Tests` ×4 governance/scaffold failures, **when** triaged, **then** each is fixed or explicitly accepted with a documented rationale (no silent red).
3. **Given** the `Contracts.Tests` ×4 epic-1 CLI negative-scope failures (Murat's retro item), **when** addressed, **then** the negative-scope guards are updated for the now-existing CLI and pass (or are formally retired with rationale).
4. **Given** the Epic 3 provider-boundary guard reds (Octokit in `FoldersServiceCollectionExtensions`, Dapr in provider abstractions), **when** addressed, **then** the dependency-boundary guards pass or are re-scoped with rationale.
5. **Given** the `UI.E2E.Tests` ×40 Playwright-not-installed failures, **when** addressed, **then** CI provisions Chromium via `tests/install-playwright.ps1` (coordinate with Story 8.4) so the lane runs rather than env-fails, or the lane is explicitly marked environment-gated with a documented skip reason.
6. **Given** all of the above, **when** `dotnet test Hexalith.Folders.slnx` runs in CI, **then** the baseline is honestly green (zero unexplained reds) and the result is recorded as release evidence.

## References

- 7.18 Dev Agent Record (residual reds enumeration); readiness review Critical #1 (C3) + addendum (7.18).
- Governance: `docs/exit-criteria/c3-retention.md`, `docs/exit-criteria/c0-c13-governance-evidence.yaml`, `tests/tools/run-retention-deletion-gates.ps1`.
- Sprint Change Proposal: `../planning-artifacts/sprint-change-proposal-2026-06-22.md`.
