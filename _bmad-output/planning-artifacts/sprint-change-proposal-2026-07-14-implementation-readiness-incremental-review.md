---
title: "Sprint Change Proposal — Implementation-Readiness Incremental Review"
project: "Hexalith.Folders"
date: "2026-07-14"
preparedAt: "2026-07-14T20:11:20+02:00"
preparedFor: "Administrator"
workflow: "bmad-correct-course"
reviewMode: "incremental"
status: "approved"
approvedAt: "2026-07-14T20:15:02+02:00"
approvedBy: "Administrator"
approvalResponse: "approve"
changeScope: "major"
selectedPath: "hybrid-mvp-truth-reconciliation-and-direct-planning-adjustment"
trigger:
  artifact: "_bmad-output/planning-artifacts/implementation-readiness-report-2026-07-14.md"
  result: "NOT READY"
incrementalEditsApproved: 8
sourceArtifactsModified: false
sprintStatusDelta: "none-required-approved-epics-and-stories-already-registered"
handoffStatus: "recorded"
handoffRecipients:
  - "Product Manager"
  - "Solution Architect"
  - "Product Owner"
  - "Developer"
  - "Test Architect"
preservedArtifacts:
  - "_bmad-output/planning-artifacts/implementation-readiness-report-2026-07-14.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-14.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-14-implementation-readiness-structural-correction.md"
---

# Sprint Change Proposal — Implementation-Readiness Incremental Review

## 1. Issue Summary

### Trigger

The 2026-07-14 implementation-readiness assessment found the planning set **NOT READY** despite complete document-category coverage, 58/58 functional-requirement traceability, 70 extracted non-functional requirements, and Given/When/Then criteria for all 116 story headings.

The change was discovered through a portfolio-level readiness assessment rather than one implementation story. Story 11.10 is the central convergence point, but the evidence spans Stories 2.8/2.8b, Epics 4–6, Epics 8–10, and the Epic 12 durable-data-plane decision.

### Core problem

The product direction and core architecture remain valid. The delivery plan is structurally inconsistent:

1. Earlier product epics depend on later technical closure or refactoring stories for behavior required by their own outcomes.
2. Completed control-plane, contract, safety, accessibility, and fallback work is not cleanly separated from completed production capability.
3. Authorization, idempotency, correlation, audit, and production-path verification are sequenced after stories that already depend on them.
4. Product epics, enabling workstreams, release remediation, governance, and refactoring are mixed in completion reporting.
5. `epics.md`, implementation story files, sprint status, architecture, UX, and traceability do not share a deterministic source-of-truth policy.
6. Safe-empty or unavailable deployed read models preserve confidentiality but cannot populate the primary console or authorized-search journeys.
7. Several stories combine multiple providers, surfaces, repositories, or risk families and include fail-open completion language.

### Evidence

- Epic 5 claimed complete parity, while Epic 8 later added 15 missing REST routes and the first real four-surface wire gate.
- Epic 4 transition evidence, Epic 6 diagnostics, and Epic 10 search hydration were assigned to later Story 11.10.
- Deployed diagnostic and transition-evidence read models are empty seed-backed seams.
- The deployed semantic-index bridge defaults to unavailable, yielding zero hydrated search results.
- Story 2.8 required 2.8b to wire the real `/process` production path.
- `epics.md` declares 115 stories while containing 116 headings and omits Epics 12 and 13 already registered in sprint status.
- The PRD still marks approved C3/C4 decisions as TBD, and architecture still concludes `READY WITH MINOR GAPS` and `Critical Gaps: None`.
- The authoritative UX specification lacks a stable requirement for the privileged incident-stream flow.

## 2. Impact Analysis

### Epic impact

| Epic/workstream | Impact |
| --- | --- |
| 1 | Reclassify as enabling contract/adapter foundation; preserve completed evidence. |
| 2 | Absorb 2.8b production-path criteria into 2.8; preserve 2.8b as an alias. |
| 3 | Reopen and split provider capability, provisioning, mutation/commit, and terminal asynchronous outcome work. |
| 4 | Reopen for production transition evidence and durable prepare/lock/mutation/context/commit proof. |
| 5 | Reopen and split CLI/MCP capability slices; retain parity stories as regression evidence. |
| 6 | Reopen for production-populated diagnostic projections and deployed-console proof. |
| 7 | Retain as completed release-governance history, not product scope. |
| 8 | Retain as completed release-remediation history, not product scope. |
| 9 | Retain as completed FR58 topology enablement. |
| 10 | Rename around authorized content search; add deployed bridge, real round trip, and C9-gated body materialization. |
| 11 | Retain as technical refactoring; remove product-projection ownership and split catch-all stories. |
| 12 | Incorporate as the durable repository-backed product round trip. |
| 13 | Incorporate as security/operations hardening outside product-completion metrics. |

### Story impact

- Reorder Epic 4 foundations logically before mutation behavior.
- Add focused production-closure stories 3.10–3.14, 4.18–4.21, 5.8–5.11, 6.12–6.14, 10.7–10.9, and split Workstream 11 stories.
- Preserve completed story history; no new split child inherits `done` without matching evidence.
- Remove forward-story acceptance criteria and alternate passing branches for blocked mandatory outcomes.
- Require real production-path, replay, conflict, failure, restart, terminal-state, audit, and no-leak evidence where applicable.

### Artifact conflicts

| Artifact | Conflict | Required correction |
| --- | --- | --- |
| PRD | Stale readiness, C3/C4, architecture-decision, and FR58 posture | Record current delivery truth without changing the durable product scope. |
| Architecture | `READY WITH MINOR GAPS` contradicts documented production limitations | Mark implementation readiness not-ready and relocate production ownership. |
| UX | Safe fallback rendering can be mistaken for capability evidence; incident flow lacks stable traceability | Add evidence boundary, five dispositions, FR58 boundary, and UX-DR33. |
| Epics | Mixed classifications, stale counts, forward dependencies, oversized active stories | Add authority policy, portfolio classification, focused stories, and generated inventory. |
| Sprint status | Structural correction is partially registered ahead of source planning artifacts | Reconcile only after the authoritative source backlog is updated. |
| Traceability/context | FR57/FR58 and NFR51/NFR52 ownership is inconsistent | Separate production, presentation, topology, durable-state, and verification ownership. |

### Technical impact

This workflow changes planning and tracking artifacts only. It does not authorize application-code, infrastructure, deployment, submodule, package, or external-repository mutations.

Future implementation impact is high because the corrected plan requires durable persistence, real Git behavior, production projections, authorized search, restart/replay evidence, and hardening. Existing valid contract and control-plane work is preserved.

## 3. Recommended Approach

### Selected path

**Hybrid — MVP truth reconciliation plus direct planning adjustment**

- Preserve the durable repository-backed lifecycle as the product MVP.
- Recognize completed contract, adapter, authorization, governance, accessibility, and safe-fallback work as a completed control-plane increment.
- Do not represent that increment as product-MVP completion.
- Incorporate the ratified Epic 12 durable data plane and Epic 13 hardening work.
- Repair classifications, dependencies, ownership, story authority, and acceptance boundaries without rolling back valid implementation.

### Alternatives

| Option | Effort | Risk | Decision |
| --- | --- | --- | --- |
| Direct planning adjustment only | Medium-high | Medium | Used as part of the hybrid. |
| Roll back completed work | High | High | Rejected; discards valid foundations without closing production gaps. |
| Reduce MVP to a control plane | Medium | High product risk | Rejected; contradicts the ratified durable product purpose. |
| Durable product MVP plus truth reconciliation | High implementation effort | Medium-high delivery risk | Selected. |

### Timeline impact

Planning synchronization is a bounded replan. Product-readiness timing becomes dependent on Epic 12 and the production-closure stories in Epics 4, 6, and 10. No release or implementation date should be claimed until the reconciled dependency graph is estimated by Product Management, Architecture, and Development.

## 4. Detailed Change Proposals

All eight proposals below were approved individually in incremental review. The consolidated proposal was explicitly approved by Administrator on 2026-07-14.

### 4.1 Story and acceptance-criteria authority

**Artifact:** `epics.md`; new `planning-story-manifest.yaml`

**OLD**

`epics.md` says its acceptance criteria are terse planning criteria and that implementation story files hold authoritative as-built criteria. Backlog stories without files and conflicting status sources have no deterministic authority rule.

**NEW**

1. A backlog story without a dedicated file is fully authoritative in `epics.md`.
2. Once a dedicated story file exists, it becomes authoritative and `epics.md` becomes a linked synopsis.
3. `planning-story-manifest.yaml` records every story ID, title, status, classification, authoritative source, and superseded alias.
4. Readiness assessment loads sources selected by the manifest.
5. Validation fails on duplicate IDs, missing files, conflicting statuses, stale counts, untracked headings, or multiple authoritative sources.
6. Story 2.8b becomes a superseded alias of Story 2.8 after its production criteria are absorbed.

**Rationale:** Every later reclassification, split, status change, and readiness judgment needs one deterministic inventory.

### 4.2 PRD truth and MVP status

**Artifact:** `prd.md`

**OLD**

- Status is `complete`, last edited 2026-05-07, with no current readiness metadata.
- C3 and C4 remain TBD despite approved artifacts.
- Architecture decisions are described as unresolved.
- Metadata-token recall is not clearly separated from complete FR58 content search.

**NEW**

Add:

```yaml
lastEdited: '2026-07-14'
implementationReadiness: not-ready
implementationReadinessAssessedAt: '2026-07-14'
productMvpDecision: durable-repository-round-trip-required
productMvpDecisionRatifiedAt: '2026-07-14'
```

Add a current-delivery section distinguishing the completed control-plane increment from product-MVP completion. Product acceptance requires Epic 12 evidence for durable content, restart survival, authoritative retrieval, real Git commit, terminal task state, and production read-model rebuilds.

Correct C3/C4 to approved status and replace stale decision language with implementation-conformance and deployed-evidence work. Clarify that FR58 requires a real produce/index/authorize/hydrate/redact/search round trip; metadata-token recall is an enabling increment and body content remains C9-gated.

**Rationale:** Preserve product scope while aligning delivery claims with deployed evidence.

### 4.3 Architecture readiness and ownership

**Artifact:** `architecture.md`

**OLD**

- Final assessment: `READY WITH MINOR GAPS`.
- Critical gaps: none.
- Story 11.10 owns product projections needed by Epics 4, 6, and 10.
- Safe-empty behavior is treated as sufficient production evidence.
- FR58 is missing from requirements-to-structure mapping.

**NEW**

Set current implementation readiness to `NOT READY` while retaining the architecture as directionally valid. Record durable persistence/Git, populated projections, deployed search, forward dependencies, FR58 mapping, and planning authority as critical gaps.

Move ownership:

- Epic 4: workspace transition evidence.
- Epic 6: seven production diagnostic projections.
- Epic 10: search bridge, registration, hydration, trimming, and live proof.
- Epic 12: durable source events, content/state, task completion, restart replay, and real Git persistence.
- Workstream 11: shared platform-seam adoption and verification only.

Add FR58 structure mapping and use five canonical dispositions: `available`, `auto-recovering`, `degraded-but-serving`, `awaiting-human`, and `terminal-until-intervention`.

Correct NFR51/NFR52 posture: safe-empty behavior proves confidentiality and honest failure, not populated production capability or replay readiness.

**Rationale:** Remove Story 11.10 as a convergence bottleneck and align readiness with production reality.

### 4.4 UX evidence, disposition, search, and incident-mode boundary

**Artifact:** `ux-design-specification.md`

**OLD**

- Safe-empty rendering is not distinguished from capability evidence.
- Canonical operator dispositions are absent.
- Operational global search can be confused with FR58 content search.
- The incident stream has no stable UX requirement.

**NEW**

Add an implementation-evidence boundary: fixtures, seed data, screenshots, and in-memory `Save(...)` calls prove rendering only; production acceptance requires durable population, deployed registration, deterministic empty-checkpoint rebuild, and rendered-console evidence.

Adopt the five operator dispositions and distinguish healthy `available` from unavailable data. Clarify that FR58 adds no file browser, content preview, snippets, raw paths, or source URIs; any UI result remains metadata-only and security-trimmed.

Add `UX-DR33` for `/_admin/incident-stream`, covering privileged ACL enforcement, degraded-mode warning, raw event metadata, canonical dispositions, redaction, correlation/time-window copy, safe-empty/no-leak behavior, responsive behavior, keyboard access, focus, and screen-reader semantics.

Retain FrontComposer Shell and Fluent UI Blazor V5 requirements and the read-only boundary.

**Rationale:** Separate safe fallback UX from production evidence and close the architecture-to-UX incident-flow gap.

### 4.5 Portfolio classification

**Artifact:** `epics.md`

**OLD**

Product, enabling, release, remediation, governance, and refactoring work are mixed into one completion inventory.

**NEW**

| ID | Classification | Revised title |
| --- | --- | --- |
| 1 | Enabling workstream | Canonical Contract and Adapter Foundation |
| 2 | Product epic | Tenant-Scoped Folder Access and Lifecycle |
| 3 | Product epic | Provider Readiness and Repository Binding |
| 4 | Product epic | Repository-Backed Workspace Task Lifecycle |
| 5 | Product epic | Cross-Surface Workflow Parity |
| 6 | Product epic | Read-Only Workspace Trust Console and Audit Review |
| 7 | Release-governance workstream | MVP Release Readiness and Operational Evidence |
| 8 | Release-remediation workstream | MVP Release Acceptance Closure |
| 9 | Completed FR58 enabler | AppHost and Memories Search-Index Topology |
| 10 | Product epic | Authorized Folders Content Search and Index Lifecycle |
| 11 | Technical-refactoring workstream | Domain-Focus Platform Refactoring and Governance Closure |
| 12 | Product epic | Durable Repository-Backed Round Trip |
| 13 | Security/operations workstream | Security and Operational Hardening |

Product-completion metrics include only Epics 2–6, 10, and 12. Preserve stable IDs and historical evidence. Generate counts from the manifest.

**Rationale:** Keep technical and governance work visible without treating it as delivered product capability.

### 4.6 Dependency and production-closure repair

**Artifacts:** `epics.md`, architecture ownership references

**OLD**

- Epics 4, 6, and 10 borrow production behavior from Story 11.10.
- Story 2.8 required later Story 2.8b for the real production path.
- Idempotency/audit foundations follow dependent mutations.
- Story 10.6 includes future-story preservation criteria.
- Mandatory production proof can be re-carried as an alternate passing result.

**NEW**

```text
12.1 Durable EventStore repository
├── 12.2 Durable task/projection substrate
└── 12.3 Durable file-content source
    └── 12.4 Real Git write path
        ├── Epic 4 production closure
        ├── Epic 6 populated diagnostics
        └── Epic 10 real search closure
```

- Absorb 2.8b into 2.8.
- Add Epic 4 Stories 4.18–4.21 for transition projection and durable lifecycle proof.
- Add Epic 6 Stories 6.12–6.14 for populated projections and deployed journeys.
- Add Epic 10 Stories 10.7–10.9 for deployed bridge, real round trip, and C9-gated body materialization.
- Limit Story 10.6 to behavior it delivers.
- Narrow Story 11.10 to EventStore admission/subscription-seam adoption and separate Memories/DCP work.
- Treat idempotency, correlation, canonical failure evidence, and metadata-only audit as logical prerequisites before mutation slices.
- A blocked mandatory criterion keeps a story incomplete unless removed through approved scope change.

**Rationale:** Make every product epic completable from earlier prerequisites without borrowing behavior from later refactoring.

### 4.7 Oversized story splits

**Artifact:** `epics.md`

**OLD**

Active stories combine multiple providers, surfaces, repositories, and risk families.

**NEW**

Epic 3 separates GitHub and Forgejo discovery/readiness, provisioning/binding/ref, mutation/commit/status/failure, and terminal asynchronous creation/binding. Epic 5 separates CLI and MCP foundation, workspace/lock, and file/context/commit/status/error/audit slices. Workstream 11 separates prerequisite inventory, hygiene, behavioral gates, test helpers, gateway conformance, provider fakes, FrontComposer shell adoption, Fluent UI conformance, documentation cleanup, ADRs, and final verification.

No split child inherits `done` automatically. Completed Stories 8.1 and 8.2 remain immutable historical batches excluded from the active implementation-ready backlog. Other completed oversized stories remain historical evidence, with focused closure stories carrying new work.

**Rationale:** Give each active story one independently demonstrable outcome without destroying historical traceability.

### 4.8 Status, traceability, and validation

**Artifacts:** planning manifest, sprint status, deferred work, project/Epic 11 context, FR/NFR/UX traceability

**OLD**

Sprint status partially reflects the correction while source planning artifacts do not. Counts and ownership remain contradictory. No deterministic planning-consistency gate exists.

**NEW**

| Epic/workstream | Corrected status |
| --- | --- |
| 1 | done — enabling history |
| 2 | done |
| 3–6 | in-progress — reopened |
| 7 | done — governance history |
| 8 | done — remediation history |
| 9 | done — FR58 topology enabler |
| 10–11 | in-progress |
| 12–13 | backlog |

Synchronize the manifest, epics, sprint status, deferred work, Epic 11 context, project context, FR/NFR traceability, and `UX-DR33` ownership.

Correct ownership:

- FR57: Epic 3 produces provider evidence; Epic 6 presents it.
- FR58: Epic 10 owns the capability; Epic 12 supplies durable state/egress; Workstream 9 supplies topology enablement.
- NFR51/NFR52: distinguish structural safe-read behavior from populated production/replay evidence.

Add a planning-consistency gate for duplicate/untracked IDs, multiple authoritative sources, missing files, status conflicts, stale counts, missing aliases, later-delivery dependencies, technical work classified as product scope, and completed production claims backed only by no-op, unavailable, seed-only, or fake-only evidence.

**Rationale:** Make the correction enforceable and prevent renewed artifact drift.

## 5. Implementation Handoff

### Scope classification

**Major** — the change corrects product-MVP truth, portfolio classification, production ownership, dependency ordering, authoritative story sources, UX traceability, and readiness gates across multiple planning artifacts.

### Routing

| Role | Responsibility |
| --- | --- |
| Product Manager | Own product-MVP truth, FR58/C9 scope, portfolio classification, acceptance, and schedule re-estimation. |
| Solution Architect | Own production boundaries, dependency direction, projection ownership, durable substrate integration, and architecture synchronization. |
| Product Owner | Own backlog ordering, manifest authority, story splitting, lifecycle status, and source reconciliation. |
| Developer | Validate implementation feasibility and execute future stories only from authoritative sources. |
| Test Architect | Own production-path, restart/replay, populated-read-model, live-round-trip, no-leak, and readiness-gate evidence. |

### Recommended implementation sequence

1. Freeze the current planning baseline.
2. Create the planning story manifest and authority validation.
3. Synchronize PRD, architecture, and UX truth/provenance.
4. Reclassify the portfolio and incorporate Epics 12 and 13 into `epics.md`.
5. Add focused stories and complete authoritative acceptance criteria.
6. Reconcile sprint status, aliases, deferred work, project context, Epic 11 context, and traceability.
7. Add and run the planning-consistency gate.
8. Rerun implementation readiness into a new non-overwriting report.
9. Estimate and execute the corrected dependency graph, beginning with the approved prerequisite ordering.

### Success criteria

The correction is complete when:

1. Every story has exactly one authoritative source.
2. Counts and statuses are generated or deterministically validated.
3. Product epics are distinguished from enabling, governance, remediation, refactoring, and hardening work.
4. No product story depends on later delivery for behavior it claims.
5. Epics 4, 6, 10, and 12 cannot complete without production-path evidence.
6. FR58 cannot complete without a real authorized, durable, hydrated, redacted result.
7. UX incident mode has stable traceability and production evidence is distinguished from rendering evidence.
8. PRD, architecture, UX, epics, sprint status, context, and traceability agree.
9. A new implementation-readiness assessment supersedes the current `NOT READY` decision only after these changes are applied and validated.

## 6. Incremental Review Record

| Proposal | Decision |
| --- | --- |
| Story and acceptance-criteria authority | Approved |
| PRD truth and MVP status | Approved |
| Architecture readiness and ownership | Approved |
| UX evidence and incident-mode boundary | Approved |
| Portfolio classification | Approved |
| Dependency and production-closure repair | Approved |
| Oversized story splits | Approved |
| Status, traceability, and validation | Approved |

The consolidated proposal is `approved`. Approval authorizes the implementation handoff described in Section 5; it does not claim that the listed source-artifact edits or future application-code work have already been performed.

## 7. Final Approval and Workflow Execution Log

- **Final decision:** Approved by Administrator.
- **Approval time:** 2026-07-14T20:15:02+02:00.
- **Change scope:** Major.
- **Selected approach:** Hybrid MVP truth reconciliation plus direct planning adjustment.
- **Primary routing:** Product Manager and Solution Architect.
- **Supporting routing:** Product Owner, Developer, and Test Architect.
- **Sprint-status reconciliation:** No additional status mutation was required. The approved reopened epics and focused story registrations are already present in `sprint-status.yaml`; duplicating them would introduce drift.
- **Artifacts modified by this workflow:** This finalized Sprint Change Proposal only.
- **Artifacts awaiting implementation:** PRD, architecture, UX, epics, planning manifest, context, deferred-work, sprint-status reconciliation metadata, and FR/NFR/UX traceability artifacts listed above.
- **Code/infrastructure authorization:** No code, infrastructure, deployment, package, submodule, or external-repository mutation was performed by this workflow.
