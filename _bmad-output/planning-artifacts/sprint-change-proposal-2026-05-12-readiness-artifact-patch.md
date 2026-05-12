# Sprint Change Proposal: Implementation Readiness Artifact Patch

Date: 2026-05-12
Project: Hexalith.Folders
Mode: Batch
Status: Approved by user request and applied

## 1. Issue Summary

The implementation readiness assessment completed on 2026-05-11 found that the planning set was directionally sound but still marked `NEEDS WORK` because several planning artifacts were not precise enough for implementation handoff.

Triggering artifact:

- `_bmad-output/planning-artifacts/implementation-readiness-report-2026-05-11.md`

Primary issues:

- `epics.md` preserved 57/57 functional requirements but had only 67 NFR bullets while the PRD has 70.
- Epic 6 referenced `UX-DR1` through `UX-DR32`, but the UX specification did not define those stable identifiers.
- Story 3.2 implied a dependency on future real provider adapters.
- Story 3.4 did not explicitly mirror GitHub canonical operation coverage for Forgejo.
- Story 4.13 used vague failure-source wording.
- Workstream 7 needed to remain clearly governed as release readiness, not product scope.

## 2. Impact Analysis

### Epic Impact

No epic is invalidated. The existing epic sequence remains viable.

Epic 1 remains foundation/contract-spine work and is now explicitly framed around consumer-verifiable contract evidence.

Epic 3 required story-level precision for provider-port independence and Forgejo canonical operation coverage.

Epic 4 required clearer failure-source and evidence metadata wording in Story 4.13.

Epic 6 required stable UX traceability identifiers.

Release Readiness Workstream 7 remains a governance/evidence track and does not add product FR scope.

### Story Impact

Updated stories:

- Story 3.2: validates the provider capability model through a fake/test provider instead of relying on future GitHub/Forgejo adapters.
- Story 3.4: adds explicit Forgejo readiness, repository creation or binding, branch/ref, file, commit, status, failure, drift, version, and unknown-outcome coverage.
- Story 4.13: names concrete failure sources and required metadata-only evidence fields.
- Story 6.5 and Story 6.11 were already aligned to UX traceability by previous artifact edits and are now backed by stable `UX-DR` IDs in the UX specification.
- Story 7.16 already requires every PRD NFR bullet to map to implementation evidence and now has a 70-item NFR source in `epics.md`.

### Artifact Conflicts

PRD: no edit required. It remains the source of truth for 57 FRs and 70 NFR bullets.

Epics: edited to align NFR numbering and tighten affected story acceptance criteria.

UX Design Specification: edited to define the authoritative `UX-DR1` through `UX-DR32` traceability set.

Sprint Status: no edit required because this correction does not add, remove, or renumber epics or stories.

## 3. Recommended Approach

Use Direct Adjustment.

Rationale:

- The readiness report identified precision gaps, not a product pivot.
- The existing PRD, architecture, UX, and epics remain aligned.
- The corrections are limited to planning traceability and acceptance criteria wording.
- No implementation rollback or backlog restructuring is required.

Scope classification: Minor.

## 4. Detailed Change Proposals

### Proposal 1: Preserve Exact PRD NFR Traceability

Artifact: `_bmad-output/planning-artifacts/epics.md`

Applied changes:

- Updated the NFR inventory from 67 to 70 numbered bullets.
- Restored the PRD's separate performance-target recalibration requirement.
- Split allowed audit metadata classification from sensitive audit metadata protection.
- Restored the explicit retention-duration policy requirement.
- Renumbered downstream NFR bullets to match the PRD.

### Proposal 2: Define Stable UX-DR Identifiers

Artifact: `_bmad-output/planning-artifacts/ux-design-specification.md`

Applied changes:

- Added an authoritative `Stable UX Design Requirements` section.
- Defined `UX-DR1` through `UX-DR32`.
- Clarified that `docs/ux/ops-console-wireflows.md` may expand these requirements but must preserve the IDs.

### Proposal 3: Remove Story 3.2 Forward Dependency

Artifact: `_bmad-output/planning-artifacts/epics.md`

Applied changes:

- Changed the acceptance setup from real provider adapters to the provider port plus fake/test provider adapter.
- Required capability-query validation without depending on future GitHub or Forgejo implementation.

### Proposal 4: Strengthen Forgejo Provider Coverage

Artifact: `_bmad-output/planning-artifacts/epics.md`

Applied changes:

- Added Forgejo readiness, repository creation or binding, branch/ref, file, commit, status, failure mapping, drift, version snapshot, and unknown-outcome coverage.
- Required canonical provider results equivalent to GitHub where product semantics match.

### Proposal 5: Tighten Story 4.13 Failure Evidence Wording

Artifact: `_bmad-output/planning-artifacts/epics.md`

Applied changes:

- Replaced "available by this point" with explicit failure sources.
- Named required metadata-only fields for canonical error and audit/projection consumers.

## 5. Implementation Handoff

Change scope: Minor.

Handoff recipients:

- Developer agent: use the patched planning artifacts for downstream story implementation.
- Product/backlog maintainer: no story renumbering or sprint-status update required.
- Test architect/release reviewer: re-run implementation readiness focusing on NFR numbering, UX-DR traceability, Story 3.2 independence, Story 3.4 Forgejo coverage, and Story 4.13 specificity.

Success criteria:

- `epics.md` contains 70 NFR bullets matching PRD granularity.
- `ux-design-specification.md` defines `UX-DR1` through `UX-DR32`.
- Story 3.2 no longer depends on future real adapters.
- Story 3.4 mirrors canonical provider-operation coverage for Forgejo.
- Story 4.13 names concrete failure sources and evidence fields.
- Workstream 7 remains release governance, not product FR scope.
- No recursive submodule initialization guidance is introduced.

## 6. Checklist Summary

- [x] Trigger and evidence identified from the 2026-05-11 implementation readiness report.
- [x] Epic impact assessed as minor direct adjustment.
- [x] PRD impact assessed as no edit required.
- [x] Epics artifact patched.
- [x] UX artifact patched.
- [N/A] Sprint status update skipped because no stories or epics were added, removed, or renumbered.
- [x] Handoff plan recorded.
