---
project_name: Hexalith.Folders
workflow: bmad-correct-course
source_change_file: _bmad-output/planning-artifacts/implementation-readiness-report-2026-07-07.md
date: 2026-07-07T09:07:42+02:00
mode: batch
status: approved
scope_classification: moderate
prepared_for: Jerome
approved_at: 2026-07-07T09:20:00+02:00
approved_by: Jerome
---

# Sprint Change Proposal - Implementation Readiness Remediation

## 1. Issue Summary

The 2026-07-07 implementation-readiness assessment reported `NEEDS WORK` even though PRD, architecture, epics, and UX artifacts were present and 58 of 58 PRD functional requirements were mapped in epics.

The triggering issues are artifact consistency and story-quality problems, not a product-scope rewrite:

- `implementation-readiness-report-2026-07-07.md` reports 11 issues: 1 critical, 5 major, 3 minor, and 2 warnings.
- The report says Story 8.6 is still `BLOCKED-PENDING-LEGAL`, but implementation evidence shows C3 Legal approval was recorded on 2026-06-24 and Story 8.6 is done.
- PRD includes FR58, while Epic 10 still contains stale wording that says the authorized search facade is a new FR to be added to PRD when scheduled.
- Epic 10 is classified as Phase 2 and contains implementation-complete story work, but Stories 10.1-10.5 are still in non-standard seed-story format.
- Product epics, release closure, platform runway, Phase 2 capability work, and refactoring governance are separated in frontmatter, but the epics body still needs clearer sectioning so readiness reporting does not treat all workstreams as peer product epics.
- UX frontmatter still contains old `D:/Hexalith.Folders/...` absolute paths.

Supporting evidence loaded during this workflow:

- PRD: `_bmad-output/planning-artifacts/prd.md`
- Architecture: `_bmad-output/planning-artifacts/architecture.md`
- Epics: `_bmad-output/planning-artifacts/epics.md`
- UX: `_bmad-output/planning-artifacts/ux-design-specification.md`
- Readiness report: `_bmad-output/planning-artifacts/implementation-readiness-report-2026-07-07.md`
- Approved earlier proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-081620.md`
- Story 8.6 implementation evidence: `_bmad-output/implementation-artifacts/8-6-record-c3-legal-signoff-and-apply-cascade.md`
- Sprint status: `_bmad-output/implementation-artifacts/sprint-status.yaml`
- C3 governance evidence: `docs/exit-criteria/c3-retention.md`, `docs/exit-criteria/c0-c13-governance-evidence.yaml`, `_bmad-output/gates/retention-deletion/latest.json`

## 2. Impact Analysis

### Epic Impact

Product Epics 1-6 remain structurally valid and should not be reopened.

Release/platform/governance workstreams should stay separated from product epics:

- Release Readiness Workstream 7: release evidence, no new product FR scope.
- Epic 8: release closure, no new product FR scope.
- Epic 9: platform runway for AppHost and Memories topology, no new product FR scope.
- Epic 11: platform refactoring/governance workstream, no new product FR scope.

Epic 10 needs wording and format cleanup:

- FR58 is already in the PRD and mapped to Epic 10.
- Epic 10 should not say the FR is still to be added.
- Epic 10 should clarify that its remaining work is release evidence and follow-up action items, not seed-level story discovery.
- Stories 10.1-10.5 need the standard `As a / I want / So that` story frame and an `Acceptance Criteria` heading.

### Story Impact

Story 8.6:

- `epics.md` still says the story is `BLOCKED-PENDING-LEGAL`.
- Implementation evidence says Legal sign-off was recorded on 2026-06-24 by Jerome through the dev-story session, with Legal signer `Jérôme Piquot, Louveciennes`; Story 8.6 status is done, Epic 8 is done, and the retention gate reports `status=passed`, `policy_status=approved`.
- Recommendation: update `epics.md` to reflect done status and cite the implementation story/evidence. Do not fabricate or change legal evidence; only synchronize stale planning text with existing recorded evidence.

Stories 10.1-10.5:

- Existing story content has useful Given/When/Then acceptance criteria but lacks standard story framing.
- Recommendation: wrap each story in persona/value framing and keep the existing Given/When/Then acceptance criteria under `Acceptance Criteria`.

Story 11.2:

- The readiness report correctly notes external shared-module prerequisites.
- The earlier approved Epic 11 proposal already captured those dependencies. No duplicate new epic is needed.
- Recommendation: keep downstream Epic 11 stories blocked by Story 11.2 unless the owning repositories, SHAs/packages, and fallback decisions are recorded.

### Artifact Conflicts

PRD:

- FR58 exists in the PRD.
- Scope language should clarify whether FR58 is current-release scope, Phase 2 scope, or post-MVP scope.
- Recommended decision: treat FR58 as a current PRD requirement already implemented through Epic 10, with release readiness gated by Epic 10 action-item evidence rather than missing PRD inclusion.

Epics:

- `AR-PROPOSAL-06` still says `57/57` FR coverage.
- Epic List text for Epic 10 says the FR is to be added to PRD.
- Story 8.6 body text is stale.
- Epic 10 stories are structurally malformed.
- The body needs explicit product-vs-workstream sectioning.

Architecture:

- Architecture already recognizes 58 FRs, the authorized Folders query facade, C3 approval, C4 approval, and C9 policy evidence.
- No architectural direction change is required.
- Optional cleanup: add a sentence that C4/C9 are approved policy constraints and no longer unsatisfied scope gates for seed-story discovery.

UX:

- UX aligns with PRD/architecture and already contains a correct FR58 addendum.
- Frontmatter and generated-artifact paths should be changed from old `D:/Hexalith.Folders/...` absolute paths to repository-relative paths.

Sprint status:

- No new stories are proposed.
- No sprint-status story state changes are required.
- Existing open action items for Epic 10 live evidence remain valid and should continue to gate clean release-readiness claims.

### Technical Impact

This proposal does not require code changes.

Expected implementation impact after approval:

- One planning-artifact patch across `epics.md`, `prd.md`, and `ux-design-specification.md`.
- Optional small architecture wording patch if the architect wants C4/C9 scope constraints restated.
- No OpenAPI, generated client, aggregate, server, worker, CLI, MCP, or UI behavior changes.
- No submodule changes.
- No sprint-status state mutation unless the team chooses to add a cleanup action item.

## 3. Recommended Approach

Recommended path: Direct Adjustment.

Do not roll back completed work. Do not create another technical epic. Do not reopen product Epics 1-6.

Apply a focused planning-artifact cleanup:

1. Fix stale Story 8.6 status text in `epics.md`.
2. Clarify FR58/Epic 10 scope consistently across PRD and epics.
3. Rewrite Stories 10.1-10.5 into standard story format while preserving acceptance criteria.
4. Add explicit sectioning in `epics.md` between product epics and release/platform/governance/Phase 2 workstreams.
5. Replace old UX absolute paths with repository-relative paths.
6. Keep Epic 10 live-evidence action items open until verified in a DCP-capable lane.

Effort estimate: medium.

Risk level: low-medium. The risk is mostly traceability drift if only one artifact is edited. The product and technical implementation are not changing.

Timeline impact: one documentation/planning cleanup task plus rerun of implementation-readiness assessment.

## 4. Detailed Change Proposals

### PRD Changes

Section: `Functional Requirements > Authorized Search Facade`

OLD:

```markdown
### Authorized Search Facade

- FR58: Developers and AI agents (via API, SDK, MCP, and CLI) can search the content that Folders has indexed into the Memories search index and receive only results they are authorized to see — security-trimmed to their tenant/folder/workspace, hydrated from the authoritative Folders read, and redacted to metadata-only — without Folders ever leaking another managed tenant's content, raw paths, snippets, source URIs, or hidden-resource existence.
```

NEW:

```markdown
### Authorized Search Facade

- FR58: Developers and AI agents (via API, SDK, MCP, and CLI) can search the content that Folders has indexed into the Memories search index and receive only results they are authorized to see — security-trimmed to their tenant/folder/workspace, hydrated from the authoritative Folders read, and redacted to metadata-only — without Folders ever leaking another managed tenant's content, raw paths, snippets, source URIs, or hidden-resource existence.

Scope note: FR58 is part of the current PRD requirements inventory and is implemented through Epic 10's worker-side search-index producer, bridge projection, and authorized Folders query facade. Remaining Epic 10 work is release-readiness evidence and follow-up closure, not a future PRD addition.
```

Rationale: Resolves the readiness ambiguity by making FR58's PRD status explicit.

### Epics Changes

Section: `Approved Readiness-Correction Requirements`

OLD:

```markdown
- AR-PROPOSAL-06: Preserve 57/57 FR coverage while renumbering affected stories and updating intra-document story references after the approved splits.
```

NEW:

```markdown
- AR-PROPOSAL-06: Preserve 58/58 FR coverage while renumbering affected stories and updating intra-document story references after the approved splits, including FR58 for the authorized Memories search-index facade.
```

Rationale: Current PRD and coverage map contain 58 FRs.

Section: `Approved Readiness-Correction Requirements`

OLD:

```markdown
- AR-PROPOSAL-08: Synchronize `D:\Hexalith.Folders\_bmad-output\implementation-artifacts\sprint-status.yaml` after `epics.md` is revised, then rerun implementation readiness before sprint planning proceeds.
```

NEW:

```markdown
- AR-PROPOSAL-08: Synchronize `_bmad-output/implementation-artifacts/sprint-status.yaml` after `epics.md` is revised, then rerun implementation readiness before sprint planning proceeds.
```

Rationale: Removes stale Windows absolute path from the planning source.

Section: `Epic List`

OLD:

```markdown
### Epic 10: Folders Worker-Side Semantic-Indexing Producer And Bridge Projection
Developers and AI agents can have authorized file changes asynchronously indexed into Memories through a worker-side producer and a Folders-owned bridge projection, activating the Epic 9 routing and exposing an authorized, security-trimmed query facade.
**FRs covered:** New FR for an authorized context-query/RAG facade (Phase 2; to be added to PRD when scheduled).
**Created:** 2026-06-22 via bmad-correct-course. Phase 2 — gated on Epic 9 + C4 (large-file) and C9 (path exposure) policy.
```

NEW:

```markdown
### Epic 10: Folders Worker-Side Semantic-Indexing Producer And Bridge Projection
Developers and AI agents can have authorized file changes asynchronously indexed into Memories through a worker-side producer and a Folders-owned bridge projection, activating the Epic 9 routing and exposing an authorized, security-trimmed Folders query facade.
**FRs covered:** FR58 — authorized Memories search-index query facade with Folders-side tenant/folder/workspace trimming, authoritative hydration, and metadata-only redaction.
**Created:** 2026-06-22 via bmad-correct-course. Phase 2 capability track. C4 and C9 are approved policy constraints; remaining Epic 10 work is release-readiness evidence and follow-up closure tracked by sprint action items, not a future PRD addition.
```

Rationale: Removes stale "to be added to PRD" wording and clarifies that FR58 exists now.

Section: `Epic List`, after Epic 6

NEW INSERT:

```markdown
### Release, Platform, Governance, And Phase 2 Workstreams

The following workstreams are not counted as product MVP epics when reporting product-scope completion. They track release evidence, release acceptance closure, platform runway, Phase 2 search-index capability work, and technical refactoring/governance closure with their own readiness criteria.
```

Rationale: Makes the existing frontmatter classification visible in the body.

Section: before `## Release Readiness Workstream 7`

NEW INSERT:

```markdown
## Release, Platform, Governance, And Phase 2 Workstreams

The sections below are managed outside the six product MVP epics. They remain important for readiness and release governance, but they should not distort product-epic completion metrics.
```

Rationale: Separates product delivery from governance/platform/release work in the main body.

Section: `Epic 8: MVP Release Acceptance Closure`

OLD:

```markdown
Story 8.5 was split 2026-06-23 (`sprint-change-proposal-2026-06-23-story-8-5-legal-blocker-split.md`): its dev scope (residual-reds honest-green baseline) stays in 8.5 (done); the externally blocked C3 Legal sign-off became Story 8.6 (blocked-pending-Legal) — the sole remaining MVP-release blocker.
```

NEW:

```markdown
Story 8.5 was split 2026-06-23 (`sprint-change-proposal-2026-06-23-story-8-5-legal-blocker-split.md`): its dev scope (residual-reds honest-green baseline) stays in 8.5 (done). Story 8.6 recorded C3 Legal sign-off on 2026-06-24 (`Jérôme Piquot`, Louveciennes; PM Jerome 2026-06-22), applied the in-lockstep C3 retention cascade, and is done. The retention-deletion gate now reports `status=passed` and `policy_status=approved`.
```

Rationale: Synchronizes `epics.md` with implementation evidence and sprint status.

Section: `Story 8.6`

OLD:

```markdown
_Split 2026-06-23 (bmad-correct-course) from Story 8.5 AC1. **BLOCKED-PENDING-LEGAL** — the sole remaining MVP-release blocker; turnkey the moment Legal signs (the full in-lockstep edit set is documented in the `8-6` story file)._
```

NEW:

```markdown
_Split 2026-06-23 (bmad-correct-course) from Story 8.5 AC1. Completed after recorded Legal sign-off on 2026-06-24 (`Jérôme Piquot`, Louveciennes; PM Jerome 2026-06-22). The in-lockstep C3 retention cascade is applied, C3 is approved in governance evidence, and the retention-deletion gate is non-blocking. See `_bmad-output/implementation-artifacts/8-6-record-c3-legal-signoff-and-apply-cascade.md`._
```

Rationale: Removes a false blocker from the planning artifact without changing the underlying legal evidence.

Section: `Epic 10`

OLD:

```markdown
_Phase 2 — gated on Epic 9 + C4 (large-file guardrail) and C9 (path-exposure policy). Stories 10.1-10.5 now have implementation story files and are marked done in sprint status; remaining Epic 10 work is release-readiness evidence and follow-up closure captured by sprint action items, not seed-level story discovery. Architecture inputs: `architecture.md` Memories integration track._
```

NEW:

```markdown
_Phase 2 capability track for PRD FR58. Epic 9 is complete; C4 and C9 are approved policy constraints. Stories 10.1-10.5 have implementation story files and are marked done in sprint status; remaining Epic 10 work is release-readiness evidence and follow-up closure captured by sprint action items, not seed-level story discovery. Architecture inputs: `architecture.md` Memories integration track._
```

Rationale: Preserves the Phase 2 classification while removing the implication that unresolved C4/C9 gates make FR58 unmapped.

#### Story 10.1 Format Change

OLD:

```markdown
### Story 10.1: Define the worker-side semantic-indexing port and Memories dependency

**Given** the architecture restricts the Memories dependency to `Hexalith.Folders.Workers`
**When** a worker-side search-index publication port is defined and `Hexalith.Folders.Workers` takes a `Hexalith.Memories.Contracts` reference (the `SearchIndexEntryChanged` / `SearchIndexEntryRemoved` CloudEvent contracts) + Dapr pub/sub — NOT `Hexalith.Memories.Client.Rest`, whose `IngestAsync` drives the separate RAG memory-ingestion subsystem, not the search index
**Then** no other project (Contracts, core, CLI, MCP, UI, Server) depends on Memories.
```

NEW:

```markdown
### Story 10.1: Define the worker-side semantic-indexing port and Memories dependency

As a worker maintainer,
I want a worker-owned search-index publication port with a narrow Memories contracts dependency,
So that Folders can publish search-index events without leaking Memories dependencies into unrelated projects.

**Acceptance Criteria:**

**Given** the architecture restricts the Memories dependency to `Hexalith.Folders.Workers`
**When** a worker-side search-index publication port is defined and `Hexalith.Folders.Workers` takes a `Hexalith.Memories.Contracts` reference (the `SearchIndexEntryChanged` / `SearchIndexEntryRemoved` CloudEvent contracts) + Dapr pub/sub — NOT `Hexalith.Memories.Client.Rest`, whose `IngestAsync` drives the separate RAG memory-ingestion subsystem, not the search index
**Then** no other project (Contracts, core, CLI, MCP, UI, Server) depends on Memories.
```

Rationale: Adds persona/value framing and the standard acceptance criteria heading.

#### Story 10.2 Format Change

OLD:

```markdown
### Story 10.2: Build the Folders-owned indexing bridge projection

**Given** durable Folders events as indexing triggers
**When** a bridge projection tracks `file version → Memories search-index entry/status`
**Then** it answers indexed / stale / skipped / failed / tombstoned / reconciliation-required per file version.
```

NEW:

```markdown
### Story 10.2: Build the Folders-owned indexing bridge projection

As an operator and integration maintainer,
I want Folders to own the bridge projection between file versions and Memories search-index state,
So that indexing status remains auditable, tenant-scoped, and authoritative from the Folders side.

**Acceptance Criteria:**

**Given** durable Folders events as indexing triggers
**When** a bridge projection tracks `file version -> Memories search-index entry/status`
**Then** it answers indexed / stale / skipped / failed / tombstoned / reconciliation-required per file version.
```

Rationale: Adds persona/value framing without changing behavior.

#### Story 10.3 Format Change

OLD:

```markdown
### Story 10.3: Author authorized asynchronous indexing on file-write and commit

**Given** a file-write/commit event
**When**, after authorization (tenant → ACL → path policy → sensitivity → size/type limits), the worker publishes one curated `SearchIndexEntryChanged` CloudEvent per indexed unit (source `hexalith-folders`, pub/sub `pubsub` / topic `memories-events`, stable CloudEvent id and idempotency key)
**Then** a Memories/pub-sub outage surfaces as retryable indexing status and never rolls back a durable Folders file operation.
```

NEW:

```markdown
### Story 10.3: Author authorized asynchronous indexing on file-write and commit

As a developer or AI-agent consumer,
I want authorized file-write and commit events to publish curated search-index updates asynchronously,
So that search discovery can be updated without weakening Folders authorization or rolling back durable file operations.

**Acceptance Criteria:**

**Given** a file-write/commit event
**When**, after authorization (tenant -> ACL -> path policy -> sensitivity -> size/type limits), the worker publishes one curated `SearchIndexEntryChanged` CloudEvent per indexed unit (source `hexalith-folders`, pub/sub `pubsub` / topic `memories-events`, stable CloudEvent id and idempotency key)
**Then** a Memories/pub-sub outage surfaces as retryable indexing status and never rolls back a durable Folders file operation.
```

Rationale: Adds standard story framing and keeps the authorization order explicit.

#### Story 10.4 Format Change

OLD:

```markdown
### Story 10.4: Emit SearchIndexEntryRemoved on removal/archive and prove end-to-end routing

**Given** Story 10.3 publishes `SearchIndexEntryChanged` on file-write/commit into `folders-index`
**When** the worker emits `SearchIndexEntryRemoved` CloudEvents (source `hexalith-folders`) for removed/archived/tombstoned units and the `folders-index` round-trip is exercised live against the Epic 9 routing
**Then** removed units leave no stale searchable entry, a syntactic/BM25 query returns exactly one hit per live indexed unit, and routing is proven live end-to-end.
```

NEW:

```markdown
### Story 10.4: Emit SearchIndexEntryRemoved on removal/archive and prove end-to-end routing

As an operator and search-integration maintainer,
I want removed, archived, and tombstoned units to update the Memories search index correctly,
So that authorized search never returns stale live results for content Folders has removed from the active surface.

**Acceptance Criteria:**

**Given** Story 10.3 publishes `SearchIndexEntryChanged` on file-write/commit into `folders-index`
**When** the worker emits `SearchIndexEntryRemoved` CloudEvents (source `hexalith-folders`) for removed/archived/tombstoned units and the `folders-index` round-trip is exercised live against the Epic 9 routing
**Then** removed units leave no stale searchable entry, a syntactic/BM25 query returns exactly one hit per live indexed unit, and routing is proven live end-to-end.
```

Rationale: Makes the user value and evidence goal explicit.

#### Story 10.5 Format Change

OLD:

```markdown
### Story 10.5: Expose an authorized Folders query facade over Memories

**Given** indexed content in Memories
**When** a Folders query facade serves context-query/RAG results
**Then** results are authorized, security-trimmed, and redacted by Folders policy before leaving the API/SDK/MCP boundary, with a new PRD FR + architecture query-facade section + UX "indexing status" console projection added when scheduled.
```

NEW:

```markdown
### Story 10.5: Expose an authorized Folders query facade over Memories

As a developer or AI-agent consumer,
I want to search indexed Folders content through a Folders-owned authorized query facade,
So that results are security-trimmed, hydrated from Folders authority, and redacted to metadata-only before leaving API, SDK, MCP, or CLI surfaces.

**Acceptance Criteria:**

**Given** indexed content in Memories
**When** a Folders query facade serves search-index results
**Then** results are authorized, security-trimmed, and redacted by Folders policy before leaving the API/SDK/MCP/CLI boundary, with PRD FR58, architecture query-facade guidance, and UX FR58 backend-discovery constraints synchronized.
```

Rationale: Removes stale "new PRD FR" wording and avoids implying the facade is an unrestricted RAG/content-preview surface.

### UX Changes

Section: YAML frontmatter `inputDocuments`

OLD:

```yaml
  - "D:/Hexalith.Folders/_bmad-output/planning-artifacts/product-brief-Hexalith.Folders.md"
  - "D:/Hexalith.Folders/_bmad-output/planning-artifacts/prd.md"
  - "D:/Hexalith.Folders/_bmad-output/planning-artifacts/prd-validation-report.md"
  - "D:/Hexalith.Folders/_bmad-output/planning-artifacts/research/technical-frontcomposer-integration-for-hexalith-folders-ui-research-2026-05-11.md"
  - "D:/Hexalith.Folders/_bmad-output/project-context.md"
  - "D:/Hexalith.Folders/Hexalith.FrontComposer/_bmad-output/project-context.md"
  - "D:/Hexalith.Folders/Hexalith.EventStore/_bmad-output/project-context.md"
```

NEW:

```yaml
  - "_bmad-output/planning-artifacts/product-brief-Hexalith.Folders.md"
  - "_bmad-output/planning-artifacts/prd.md"
  - "_bmad-output/planning-artifacts/prd-validation-report.md"
  - "_bmad-output/planning-artifacts/research/technical-frontcomposer-integration-for-hexalith-folders-ui-research-2026-05-11.md"
  - "_bmad-output/project-context.md"
  - "references/Hexalith.FrontComposer/_bmad-output/project-context.md"
  - "references/Hexalith.EventStore/_bmad-output/project-context.md"
```

Rationale: Removes old absolute paths and uses repository-relative paths.

Section: YAML frontmatter `completedArtifacts`

OLD:

```yaml
completedArtifacts:
  uxDesignSpecification: "D:/Hexalith.Folders/_bmad-output/planning-artifacts/ux-design-specification.md"
  designDirections: "D:/Hexalith.Folders/_bmad-output/planning-artifacts/ux-design-directions.html"
```

NEW:

```yaml
completedArtifacts:
  uxDesignSpecification: "_bmad-output/planning-artifacts/ux-design-specification.md"
  designDirections: "_bmad-output/planning-artifacts/ux-design-directions.html"
```

Rationale: Same path cleanup.

Section: design-direction sentence

OLD:

```markdown
The design direction showcase was generated at `D:/Hexalith.Folders/_bmad-output/planning-artifacts/ux-design-directions.html`.
```

NEW:

```markdown
The design direction showcase was generated at `_bmad-output/planning-artifacts/ux-design-directions.html`.
```

Rationale: Removes stale host-specific path.

### Architecture Changes

No required architecture change.

Optional clarification if the architect wants explicit scope reconciliation:

Section: `Hexalith.Memories integration implications`

Add after the Epic 10 live-evidence sentence:

```markdown
FR58 is already part of the current PRD requirements inventory. C4 and C9 are approved policy constraints for the implementation, not unsatisfied gates that make FR58 a future PRD addition.
```

Rationale: Prevents future readiness checks from treating FR58 as both current scope and unscheduled future scope.

### Sprint Status Changes

No sprint-status state change is required.

Optional action item if the team wants this cleanup tracked:

```yaml
  - epic: planning
    action: "Apply the 2026-07-07 implementation-readiness remediation: Story 8.6 stale-blocker cleanup, FR58/Epic 10 scope wording, Epic 10 story-format normalization, workstream sectioning, and UX path cleanup."
    owner: "Paige / Winston / Amelia"
    priority: high
    status: open
```

Rationale: Useful if approval is delayed or the patch is split across owners. Not required if the approved patch is applied immediately.

## 5. Change Navigation Checklist Results

- 1.1 Triggering story: [x] No single implementation story triggered the workflow. The trigger is the 2026-07-07 implementation-readiness report.
- 1.2 Core problem: [x] Artifact consistency and planning-quality gaps. Category: misunderstanding/staleness of existing artifact state, not a technical limitation or product pivot.
- 1.3 Supporting evidence: [x] Readiness report, epics, PRD, UX, architecture, sprint status, C3 evidence, and Story 8.6 implementation file were inspected.
- 2.1 Current epic impact: [x] Epics 1-6 remain valid; Epic 8 stale status text and Epic 10 story quality require cleanup.
- 2.2 Epic-level changes: [x] Add clearer product-vs-workstream sectioning; no new epics.
- 2.3 Remaining epics: [x] Epic 11 already exists from the earlier approved proposal; do not duplicate it.
- 2.4 Obsolete/new epics: [x] No planned epic is obsolete; no new epic needed.
- 2.5 Priority/order: [x] Apply stale-blocker and FR58 wording cleanup before rerunning readiness.
- 3.1 PRD conflicts: [x] Clarify FR58 current PRD scope.
- 3.2 Architecture conflicts: [x] No required architecture change; optional FR58/C4/C9 clarification available.
- 3.3 UX conflicts: [x] UX alignment is valid; only stale absolute paths need cleanup.
- 3.4 Other artifacts: [x] Sprint status does not need state mutation; existing Epic 10 action items remain.
- 4.1 Direct Adjustment: [x] Viable. Effort medium, risk low-medium.
- 4.2 Potential Rollback: [N/A] No rollback justified.
- 4.3 PRD MVP Review: [N/A] MVP product scope does not need reduction.
- 4.4 Recommended path: [x] Direct Adjustment through planning-artifact cleanup.
- 5.1 Issue summary: [x] Included.
- 5.2 Impact and artifact needs: [x] Included.
- 5.3 Recommended path and rationale: [x] Included.
- 5.4 PRD MVP impact/action plan: [x] MVP product scope unchanged; FR58 scope language clarified.
- 5.5 Handoff plan: [x] Included below.
- 6.1 Checklist completion: [x] Complete except explicit user approval.
- 6.2 Proposal accuracy: [x] Drafted from loaded artifacts and current repository evidence.
- 6.3 User approval: [!] Pending.
- 6.4 Sprint-status update: [N/A] No story/epic additions, removals, or state changes proposed.
- 6.5 Handoff confirmation: [!] Pending approval.

## 6. Implementation Handoff

Scope classification: Moderate.

The product scope and implementation behavior do not change, but planning artifacts need coordinated edits and readiness should be rerun afterward.

Recommended handoff:

- Product Owner / PM: approve the FR58 scope clarification: current PRD requirement implemented through Epic 10, with release evidence still tracked by action items.
- Architect: approve optional architecture wording if desired; confirm C4/C9 are approved policy constraints rather than open scope gates.
- Technical Writer / Developer agent: apply the artifact edits in PRD, epics, and UX.
- QA / Test Architect: rerun implementation-readiness after artifact cleanup and verify no new readiness blockers are introduced.

Success criteria:

- `epics.md` no longer says Story 8.6 is `BLOCKED-PENDING-LEGAL`.
- `epics.md` no longer says FR58 is a future FR to add to PRD.
- `epics.md` reports 58/58 FR coverage where the current PRD requires it.
- Stories 10.1-10.5 have standard story frames and `Acceptance Criteria` headings.
- Product epics and release/platform/governance/Phase 2 workstreams are visibly separated in the epics body.
- UX frontmatter and artifact references are repository-relative.
- Existing Epic 10 action items remain open until release evidence is recorded.
- A rerun of implementation-readiness no longer reports the same stale Story 8.6 blocker or Epic 10 formatting/scope issues.

## 7. Approval Status

This proposal is approved by Jerome.

Approved action:

- Apply the proposed planning-artifact cleanup.
- Rerun implementation readiness after cleanup.

Final routing:

- Scope: Moderate.
- Route to: Product Owner / Developer agents, with Architect and QA review.
- Handoff deliverables: approved Sprint Change Proposal, artifact cleanup patch, and refreshed implementation-readiness report.
