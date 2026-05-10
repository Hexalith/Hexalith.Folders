---
project_name: Hexalith.Folders
user_name: Jerome
date: 2026-05-10
status: approved
mode: batch
trigger: implementation-readiness-report-2026-05-10
change_scope: moderate
---

# Sprint Change Proposal - Post-Readiness Backlog Cleanup

## 1. Issue Summary

Implementation readiness completed on 2026-05-10 with status **NEEDS WORK**. The report found that the planning package has strong requirements coverage but needs backlog-structure cleanup before sprint planning.

### Trigger

No implementation story triggered this issue. The trigger was the readiness workflow itself:

- `implementation-readiness-report-2026-05-10.md`
- Overall readiness status: `NEEDS WORK`
- Functional requirement coverage: 57/57 PRD FRs covered
- Blocking concern: backlog form mixes product increments, technical enablement, and release governance

### Core Problem

The MVP requirements are traceable, but the backlog is not yet clean enough for sprint planning. Three issues need correction:

1. Epic 7 is a release-readiness gate, not a normal product capability epic.
2. Stories 1.7-1.11 use ambiguous "where applicable" acceptance language for contract metadata.
3. Epic 6 diagnostic pages depend on console wireflows, but the dependency should be explicit and treated as a page-implementation gate.

Issue type: readiness correction discovered during planning validation.

## 2. Checklist Findings

### Section 1: Understand The Trigger And Context

- [x] 1.1 Trigger identified: readiness assessment, not an implementation story.
- [x] 1.2 Core problem defined: backlog structure and acceptance clarity require cleanup.
- [x] 1.3 Evidence gathered: readiness report, FR coverage map, affected story text, and Epic 7 scope statement.

### Section 2: Epic Impact Assessment

- [x] 2.1 Current affected epic can continue with edits: Epic 1 needs acceptance-criteria tightening.
- [x] 2.2 Epic-level changes needed: Epic 7 should be reclassified or reframed as release-readiness work.
- [x] 2.3 Remaining epics reviewed: Epic 6 needs an explicit Story 6.5 gate before diagnostic page stories.
- [x] 2.4 No planned product epic is obsolete; no new product FR epic is needed.
- [x] 2.5 Epic order should remain unchanged: product capability work remains Epics 1-6, release readiness remains after product capability work.

### Section 3: Artifact Conflict And Impact Analysis

- [x] 3.1 PRD impact: no PRD requirement change required. MVP scope remains achievable.
- [x] 3.2 Architecture impact: no architecture decision change required. Architecture already supports F-1 through F-7, C0, C6, C9, C13, and NFR evidence requirements.
- [x] 3.3 UX impact: no standalone UX spec exists. The change should make `docs/ux/ops-console-wireflows.md` a gating deliverable for Epic 6 page implementation.
- [x] 3.4 Other artifact impact: sprint planning and any future `sprint-status.yaml` should treat release-readiness work separately from product capability stories.

### Section 4: Path Forward Evaluation

Option 1: Direct Adjustment

- Status: Viable
- Effort: Low to Medium
- Risk: Low
- Rationale: The necessary changes are localized to `epics.md` wording and sprint-planning classification.

Option 2: Potential Rollback

- Status: Not viable
- Effort: Not applicable
- Risk: Medium
- Rationale: No implementation has started and no completed story needs rollback.

Option 3: PRD MVP Review

- Status: Not viable
- Effort: Medium
- Risk: Medium
- Rationale: The PRD scope is not the problem; the readiness report confirms complete FR coverage.

Recommended path: **Direct Adjustment with Moderate backlog reorganization**

## 3. Impact Analysis

### Epic Impact

Epic 1 remains valid but needs clearer acceptance criteria in Stories 1.7 and 1.8. Stories 1.9-1.11 are mostly acceptable but should gain explicit metadata/constraint expectations to avoid the same ambiguity recurring across contract groups.

Epic 6 remains valid. Story 6.5 should be explicitly marked as the gating UX artifact for Stories 6.6-6.10.

Epic 7 should no longer be treated as an ordinary FR-bearing product epic. It should become a release-readiness workstream or be explicitly reframed around release-reviewer/operator acceptance.

### Story Impact

Affected stories:

- Story 1.7
- Story 1.8
- Story 1.9
- Story 1.10
- Story 1.11
- Story 6.5
- Stories 6.6-6.10
- Epic 7 heading and intro

No story removal is recommended. No new PRD functional requirement is recommended.

### Artifact Conflicts

PRD: no conflict.

Architecture: no conflict.

UX: missing standalone UX spec remains an acknowledged warning. The mitigation is to make Story 6.5 a required wireflow artifact before page work.

Sprint planning: should avoid treating release-readiness work as the same type of product-capability epic as Epics 1-6.

## 4. Recommended Approach

Use a low-risk direct adjustment:

1. Preserve all 57/57 FR coverage.
2. Keep product capability sequencing intact.
3. Reframe Epic 7 as a release-readiness workstream or release-acceptance epic.
4. Replace ambiguous metadata language in Contract Spine stories with explicit operation metadata expectations.
5. Make Story 6.5 a formal gate before Stories 6.6-6.10.
6. Rerun implementation readiness before sprint planning.

Scope classification: **Moderate**. The edits are text-only, but they affect backlog organization and sprint planning semantics.

## 5. Detailed Change Proposals

### Proposal A: Reclassify Epic 7 As Release Readiness

Artifact: `epics.md`

Section: Epic 7 heading and intro

OLD:

```markdown
## Epic 7: MVP Release-Readiness Gate

Maintainers can prove the MVP is safe to operate beyond local development through NFR validation, CI/CD release gates, capacity/retention evidence, observability, documentation, ADRs, runbooks, package traceability, and a complete NFR traceability bridge.
```

NEW:

```markdown
## Release Readiness Workstream 7: MVP Operational Acceptance And Evidence

Release reviewers, operators, and maintainers can accept the MVP for production only when safety, contract, deployment, observability, retention, documentation, package-traceability, and NFR evidence are complete.

This workstream is not a product FR-bearing epic. It is a release-readiness gate that consumes evidence from Epics 1-6 and blocks MVP acceptance when required evidence is missing. Sprint planning must treat these items as release governance and hardening work, not as a peer product capability increment.
```

Rationale:

This resolves the readiness finding without removing necessary NFR and release work. It keeps numbering stable while making the backlog semantics explicit.

### Proposal B: Tighten Story 1.7 Contract Metadata

Artifact: `epics.md`

Story: 1.7

Section: Acceptance Criteria

OLD:

```markdown
**And** each operation declares error, audit, idempotency, and parity metadata where applicable.
```

NEW:

```markdown
**And** each operation declares its required metadata explicitly:
- all operations declare canonical error categories, authorization requirements, audit classification, correlation ID behavior, and parity dimensions
- mutating operations declare idempotency-key requirements and idempotency-equivalence fields
- read/query operations declare freshness, pagination/filtering, authorization-denial shape, and read-consistency expectations
```

Rationale:

"Where applicable" leaves the main contract decision to implementer interpretation. The replacement makes applicability explicit.

### Proposal C: Tighten Story 1.8 Contract Metadata

Artifact: `epics.md`

Story: 1.8

Section: Acceptance Criteria

OLD:

```markdown
**And** each operation declares authorization, idempotency, error, audit, and parity metadata where applicable.
```

NEW:

```markdown
**And** workspace and lock operations declare authorization requirements, idempotency-key requirements, idempotency-equivalence fields, canonical error categories, audit classification, correlation ID behavior, retry eligibility, lease/expiry semantics, and parity dimensions.
```

Rationale:

Workspace and lock behavior is safety-critical. The contract must name the required metadata instead of deferring applicability.

### Proposal D: Add Explicit Metadata Expectations To Story 1.9

Artifact: `epics.md`

Story: 1.9

Section: Acceptance Criteria

OLD:

```markdown
**And** path, binary, range, result-limit, content-boundary, and secret-safe response rules are declared.
```

NEW:

```markdown
**And** path, binary, range, result-limit, content-boundary, and secret-safe response rules are declared
**And** mutating file operations declare idempotency and audit metadata, while context queries declare freshness, pagination or result bounds, redaction behavior, authorization-denial shape, and parity dimensions.
```

Rationale:

Story 1.9 did not use "where applicable," but the same ambiguity can occur unless file mutation and context-query metadata are separated.

### Proposal E: Add Explicit Metadata Expectations To Story 1.10

Artifact: `epics.md`

Story: 1.10

Section: Acceptance Criteria

OLD:

```markdown
**And** final state, retry eligibility, retry-after, correlation, and canonical error metadata are declared.
```

NEW:

```markdown
**And** final state, retry eligibility, retry-after, correlation, canonical error metadata, audit evidence, provider unknown-outcome handling, reconciliation status, idempotency behavior for commit commands, and parity dimensions are declared.
```

Rationale:

Commit/status contracts are where unknown provider outcomes and reconciliation semantics become visible. These must be first-class contract metadata.

### Proposal F: Add Explicit Metadata Expectations To Story 1.11

Artifact: `epics.md`

Story: 1.11

Section: Acceptance Criteria

OLD:

```markdown
**And** schemas exclude file contents, diffs, provider tokens, credential material, secrets, and unauthorized resource existence.
```

NEW:

```markdown
**And** schemas exclude file contents, diffs, provider tokens, credential material, secrets, and unauthorized resource existence
**And** audit and ops-console query schemas declare sensitive-metadata classification, redaction shape, authorization-denial shape, pagination/filtering, freshness, correlation ID behavior, and parity dimensions.
```

Rationale:

The console and audit surfaces are metadata-heavy. The contract should specify redaction, freshness, pagination, and authorization behavior rather than relying on later implementation judgment.

### Proposal G: Make Story 6.5 A Formal Gate

Artifact: `epics.md`

Story: 6.5

Section: Acceptance Criteria

OLD:

```markdown
**And** the notes identify keyboard-navigation, focus, non-color-only status, zoom readability, and redaction-vs-missing expectations for Epic 6 stories.
```

NEW:

```markdown
**And** the notes identify keyboard-navigation, focus, non-color-only status, zoom readability, and redaction-vs-missing expectations for Epic 6 stories
**And** Stories 6.6, 6.7, 6.8, 6.9, and 6.10 cannot begin implementation until `docs/ux/ops-console-wireflows.md` exists and has been reviewed against PRD console requirements and architecture decisions F-1 through F-7.
```

Rationale:

The readiness report identified missing standalone UX documentation as a risk. This makes the mitigation explicit before page implementation.

### Proposal H: Replace Future-Oriented Wording In Stories 6.3 And 6.4

Artifact: `epics.md`

Story: 6.3

OLD:

```markdown
**And** the badge and metadata components expose reusable parameters for later diagnostic views without duplicating mapping logic.
```

NEW:

```markdown
**And** the badge and metadata components expose reusable parameters verified by this story's tests so diagnostic views can use the mapping without duplicating logic.
```

Story: 6.4

OLD:

```markdown
**And** the redaction component exposes reusable rendering semantics for later diagnostic views.
```

NEW:

```markdown
**And** the redaction component exposes reusable rendering semantics verified by this story's tests so diagnostic views can distinguish redacted, unknown, and missing values consistently.
```

Rationale:

This removes non-blocking but future-oriented wording flagged by readiness, while preserving the implementation intent.

## 6. PRD, Architecture, And UX Modifications

### PRD

No PRD edit is recommended. MVP scope, user journeys, FRs, and NFRs remain valid.

### Architecture

No architecture edit is recommended. Existing architecture already supports the correction:

- C0 Contract Spine
- C6 workspace state and operator-disposition mapping
- C9 sensitive metadata classification
- C13 parity oracle
- F-1 through F-7 operations console decisions

### UX

No standalone UX document exists today. This proposal does not create the UX artifact directly; it makes Story 6.5 the formal gate that creates `docs/ux/ops-console-wireflows.md`.

## 7. Implementation Handoff

Scope: **Moderate**

Recommended handoff:

- Product Owner / Developer: apply the proposed `epics.md` backlog edits.
- Developer: preserve story IDs unless the team explicitly decides to remove Epic 7 from sprint-status tracking.
- Readiness reviewer: rerun `bmad-check-implementation-readiness` after edits.
- Sprint planner: run `bmad-sprint-planning` only after readiness returns a clean or accepted status.

### Success Criteria

The correction is successful when:

1. Epic 7 is no longer presented as a normal FR-bearing product epic.
2. Stories 1.7-1.11 no longer contain ambiguous "where applicable" metadata language.
3. Story 6.5 explicitly gates Stories 6.6-6.10.
4. The FR coverage map still covers all 57 PRD FRs.
5. Implementation readiness is rerun and no new traceability gaps appear.

## 8. Approval Request

Approved on 2026-05-10 and applied to `epics.md`.

Recommended approval condition:

> Approved to apply the post-readiness backlog cleanup edits to `epics.md`, preserving 57/57 FR coverage and treating Release Readiness Workstream 7 as release governance rather than product FR scope.
