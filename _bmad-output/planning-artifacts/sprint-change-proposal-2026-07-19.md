---
title: "Sprint Change Proposal — Canonical Planning Reconciliation"
project: "Hexalith.Folders"
date: "2026-07-19"
preparedFor: "Administrator"
workflow: "bmad-correct-course"
status: "approved"
approvedAt: "2026-07-20T00:14:08+02:00"
approvedBy: "Administrator"
approvalResponse: "approve all"
changeScope: "major"
reviewMode: "incremental"
recommendedPath: "direct-adjustment-under-ratified-authority"
incrementalEditsApproved: 5
sourceArtifactsModified: false
handoffStatus: "recorded"
trigger:
  artifact: "_bmad-output/planning-artifacts/implementation-readiness-report-2026-07-19.md"
  result: "NOT READY"
amends:
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-14-implementation-readiness-structural-correction.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-15.md"
preserves:
  - "_bmad-output/planning-artifacts/implementation-readiness-report-2026-07-19.md"
  - "completed implementation and historical evidence"
executionAllowance:
  - "12-6-implement-durable-all-mutations-idempotency-and-expired-key-precedence"
  - "10-8-real-produce-index-authorize-hydrate-redact-search-round-trip"
---

# Sprint Change Proposal — Canonical Planning Reconciliation

## 1. Issue Summary

The 2026-07-19 implementation-readiness assessment found the planning set **NOT READY** because the ratified 2026-07-14/15 durable-MVP structural correction was propagated unevenly. The PRD was reconciled on 2026-07-15, architecture was regenerated on 2026-07-19, and `sprint-status.yaml` contains the approved portfolio. The canonical `epics.md` and `ux-design-specification.md` remain frozen at their 2026-07-07 authority.

This is one propagation failure with four visible effects:

1. `epics.md` maps all 58 FR identifiers but materially misstates 21 requirements (36%).
2. It omits Epics 12 and 13 plus approximately 28 ratified closure and split stories.
3. It hides the dependency of Epic 4, Epic 6, and Epic 10 completion on the Epic 12 durable substrate and retains obsolete product-projection ownership in Story 11.10.
4. The UX specification omits the six independent display dimensions, canonical state/disposition vocabularies, and UX-DR33 incident-evidence authorization gate required for honest Epic 6 readiness.

No new product requirement or MVP redefinition is proposed. This proposal completes propagation of already-approved authority and prevents new story contexts from being authored from stale planning text.

### Immediate execution posture

- Story 12-6 (`in-progress`) and Story 10-8 (`ready-for-dev`) may proceed because their dedicated story files postdate and cite the ratified authority.
- All other implementation work waits for the reconcile pass.
- Story 3-10 is currently marked `in-progress` in `sprint-status.yaml`. Preserve that truthful lifecycle status, but place it on an explicit execution hold until its canonical Epic 3 split definition is reconciled; do not roll it back or discard completed evidence.

## 2. Change Analysis Checklist

### Section 1 — Trigger and Context

| Item | Status | Finding |
| --- | --- | --- |
| 1.1 Triggering story | [N/A] | The trigger is a cross-artifact readiness assessment, not a failed implementation story. |
| 1.2 Core problem | [x] | Ratified product and structural authority was not propagated to the two canonical downstream planning artifacts. |
| 1.3 Supporting evidence | [x] | 21 stale FR clauses, two absent epics, approximately 28 absent stories, hidden forward dependencies, obsolete Story 11.10 ownership, and missing UX state/authorization semantics. |
| 1.4 Execution boundary | [x] | Only Stories 12-6 and 10-8 may proceed before reconciliation. |

### Section 2 — Epic Impact

| Item | Status | Finding |
| --- | --- | --- |
| 2.1 Current epic completion | [!] | Epics 4, 6, and 10 cannot complete honestly before the relevant Epic 12 durable substrate exists. |
| 2.2 Epic-level changes | [x] | Import already-ratified Epics 12 and 13; reopen/retain the approved product closure stories and technical splits. |
| 2.3 Remaining portfolio | [x] | Product Epics 2–6, 10, and 12 remain the product-completion set; Workstreams 1, 7–9, 11, and 13 retain enabling/hardening classifications. |
| 2.4 New or obsolete epics | [N/A] | No new epic is proposed beyond the already-ratified Epic 12/13 inventory; no product goal is obsolete. |
| 2.5 Order and priority | [!] | The Epic 12 dependency spine and the FR58 completion chain must be visible and enforceable in `epics.md`. |

### Section 3 — Artifact Conflict and Impact

| Item | Status | Finding |
| --- | --- | --- |
| 3.1 PRD | [x] | No semantic edit required. The 2026-07-15 PRD already carries current actor authority, lock/idempotency/state/incident/FR58 semantics and NOT READY posture. |
| 3.2 Architecture | [x] | No semantic edit required. The 2026-07-19 architecture already carries current dependencies, ownership, state, incident, hosting, and durable-MVP posture. |
| 3.3 UX | [!] | Refresh vocabulary, authorized discovery, six display dimensions, automatic reconciliation, required components, and UX-DR33. |
| 3.4 Epics | [!] | Regenerate FR inventory/coverage from the PRD, import the ratified portfolio, repair ownership/dependencies, and remove stale counts. |
| 3.5 Contract Spine/C13 | [!] | OQ7/OQ8 follow-up remains required in gate lockstep; it is not a reason to delay the planning reconcile. |
| 3.6 Story contexts | [!] | Pre-2026-07-19 contexts containing superseded authority must be regenerated before execution; dedicated 12-6 and 10-8 contexts are excepted. |

### Section 4 — Path Forward

| Option | Viability | Effort | Risk | Decision |
| --- | --- | --- | --- | --- |
| Direct adjustment | Viable | High | Medium | **Recommended** — finish the already-ratified downstream reconciliation. |
| Rollback | Not viable | High | High | Reject — it would discard valid completed foundations and contradict the durable-MVP decision. |
| Reduce/redefine MVP | Not required | Medium | High product risk | Reject — the ratified product goal and PRD remain valid. |

**Recommended path:** Major Direct Adjustment under the ratified 2026-07-14/15 authority. Reconcile planning truth first, then resume story creation and execution in dependency order. This proposal does not authorize application-code, infrastructure, package, submodule, deployment, or external-repository changes.

## 3. Impact Analysis

### Product and delivery impact

- Completed implementation remains valid within its evidenced scope; reopened epics add production closure rather than erase history.
- `epics.md` becomes safe again as the authoring source before a dedicated story file exists.
- Epic 12 supplies durable repository, task/projection, content, Git, egress, and idempotency substrate; consuming product epics own their product projections and completion evidence.
- Product completion and release remain blocked until the corrected backlog is delivered and implementation readiness is rerun.

### Timeline and team impact

- Planning reconciliation: one focused cross-artifact pass plus validation and a non-overwriting readiness rerun.
- Product implementation: unchanged in substance from the ratified plan; the correction makes existing work and dependency cost visible rather than adding it.
- Immediate scheduling impact: retain the two-story allowance; hold Story 3-10 and do not open other stale-context stories.

## 4. Detailed Change Proposals

Each proposal was explicitly approved by Administrator on 2026-07-20. Approval authorizes the named planning/documentation edits only.

### 4.1 Rebuild the `epics.md` Authority Header and Requirements Inventory — Approved

**Artifacts:** `epics.md` frontmatter, input provenance, Requirements Inventory, FR coverage map, UX requirements inventory.

**OLD:**

> `epicCount: 11`, `storyCount: 115`, `productEpicCount: 6`, counts reviewed 2026-07-07.

> FR15 assigns provider binding configuration to platform engineers; FR28 lists `active`, `expired`, `stale`, `abandoned`, `interrupted`, and `released`; FR45 lists only six workspace states; FR41/FR42 cover a subset of retry behavior; FR56 is a generic operation timeline; FR58 searches indexed content.

**NEW:**

1. Add the 2026-07-14/15 proposals, the current PRD, the 2026-07-19 architecture, and the 2026-07-19 readiness report to provenance.
2. Remove hand-maintained fixed epic/story counts. Derive and validate inventory from the canonical story manifest.
3. Regenerate all 58 FR clauses and their coverage entries from the current PRD, preserving stable IDs. At minimum this carries:
   - FR15 tenant-administrator configuration and platform-engineer validation;
   - FR25 canonical tenant/provider/repository/ref lock identity with alias collision;
   - FR28 exact `unlocked`, `locked`, `expired`, `stale`, `revoked` vocabulary;
   - FR41/FR42 all-mutations idempotency, expired-key precedence, and read-key rejection;
   - FR45 all 11 workspace lifecycle states, separate from lock/disposition;
   - FR56 incident-admin plus fresh tenant/folder authorization before observation;
   - FR58 authorized metadata-token recall/status only, never raw paths, file bodies, snippets, or source URIs.
4. Refresh UX inventory from the reconciled UX specification, including UX-DR33.
5. Regenerate clause-level coverage so numeric FR presence alone cannot be reported as semantic coverage.

**Rationale:** The canonical epic file must not invert actors or weaken safety/lifecycle contracts already fixed in the PRD and architecture.

### 4.2 Import the Ratified Portfolio and Story Splits into `epics.md` — Approved

**Artifacts:** epic/story structure and story synopses in `epics.md`; future `planning-story-manifest.yaml`.

**OLD:**

> The canonical epic file ends at Epic 11 and omits the durable data plane, security/operations hardening, and approved closure/split stories already registered in sprint status.

**NEW:**

1. Add **Epic 12: Durable Repository-Backed Round Trip** with Stories 12.1–12.6:
   - 12.1 EventStore-backed folder repository and projection replay;
   - 12.2 durable projections and task-completion pipeline;
   - 12.3 durable workspace file-content store and content-read source;
   - 12.4 real Git commit executor and provider write path;
   - 12.5 at-least-once Memories egress and reconciler;
   - 12.6 durable all-mutations idempotency and expired-key precedence.
2. Add **Epic 13: Security and Operational Hardening** with Stories 13.1–13.6 exactly as registered in `sprint-status.yaml`; keep it outside product-completion metrics and non-duplicative of Workstream 11.
3. Add product closure stories 4.18–4.21, 6.12–6.14, and 10.7–10.9.
4. Apply the ratified splits and narrowings:
   - Epic 3: 3.3/3.10/3.11, 3.4/3.12/3.13, rename 3.6, add 3.14;
   - Epic 5: 5.2/5.8/5.9 and 5.3/5.10/5.11;
   - Workstream 11: narrow 11.2 and add 11.14–11.21 with the ratified ownership boundaries.
5. Preserve Story 2.8b only as a superseded alias of Story 2.8.
6. Preserve completed Stories 8.1 and 8.2 as immutable historical batches excluded from the active implementation-ready backlog.
7. Create `planning-story-manifest.yaml` selecting one authority per story, recording title, classification, lifecycle status, authoritative source, aliases, and generated counts.

**Rationale:** Story creation currently cannot find the approved portfolio and can regenerate open work from superseded scopes.

### 4.3 Repair Ownership, Dependencies, and Completion Semantics — Approved

**Artifacts:** epic charters, story prerequisites/acceptance, dependency diagrams, product/workstream classification in `epics.md` and the story manifest.

**OLD:**

> Story 11.10 owns the search bridge, ops-console diagnostics, and workspace transition-evidence projections. Epic 4/6/10 dependencies on later durable work are absent. FR58 is framed as indexed body/content search.

**NEW:**

```text
12.1 durable EventStore repository
  +--> 12.2 durable task/projection substrate
  +--> 12.3 durable file-content source
          +--> 12.4 real Git write path
                  +--> Epic 4 production closure
                  +--> Epic 6 populated diagnostics
                  +--> Epic 10 real search closure
```

- Epic 4 Story 4.18 owns workspace transition evidence; Stories 4.19–4.21 own durable prepare/lock, mutation/context, and real commit/reconciliation proof.
- Epic 6 Stories 6.12–6.14 own populated diagnostic projections and deployed journeys.
- Epic 10 owns the search lifecycle:

```text
10.6 metadata-derived materializer
  --> 10.7 EventStore-backed bridge and deployed registration
  --> 12.5 at-least-once indexing egress/reconciliation substrate
  --> 10.8 authorized non-empty metadata-token round trip (= FR58 completion)
  --> 10.9 separately authorized C9-gated body-content follow-on (non-blocking for current FR58)
```

- Story 11.10 owns EventStore admission/subscription-mapping seam adoption only; 11.14 owns Memories publication/search-client seams; 11.15 owns the DCP-capable cross-repository verification lane.
- Product completion cannot be supported only by no-op, unavailable, safe-empty, seed-only, or fake-only evidence.
- Production mutation stories prove REST → gateway → processor → authorization gate → durable repository/projection behavior, including restart, replay, conflicts, known/unknown provider failure, terminal status, audit, and sensitive-data exclusion.
- Read-model stories prove deployed registration, durable population, deterministic empty-checkpoint rebuild, tenant isolation, freshness, and honest unavailable behavior.

**Rationale:** Product capabilities must own the projections and evidence that complete them; enabling work cannot remain an invisible future dependency.

### 4.4 Reconcile UX Vocabulary, Components, and Incident Authorization — Approved

**Artifacts:** UX frontmatter/provenance, UX-DR2, UX-DR13, UX-DR15, new UX-DR33, journeys, component strategy, state presentation, implementation roadmap.

**OLD:**

> “Global search” is not explicitly bounded by pre-established authority; state vocabulary is a flat mixed list; `unknown_provider_outcome` has no automatic-reconciliation presentation; the spec ends at UX-DR32; hosting uses stale terminology and no Incident-Evidence Authorization Gate exists.

**NEW:**

1. Describe the host as **Blazor Web App, Interactive Server through `FrontComposerShell`**.
2. Establish authorized tenant/folder scope before candidate lookup, result counting, suggestions, filtering, or empty-state classification.
3. Present six independent dimensions:
   - workspace lifecycle: `requested`, `preparing`, `ready`, `locked`, `changes_staged`, `dirty`, `committed`, `failed`, `inaccessible`, `unknown_provider_outcome`, `reconciliation_required`;
   - lock state: `unlocked`, `locked`, `expired`, `stale`, `revoked`;
   - operator disposition: `available`, `auto-recovering`, `degraded-but-serving`, `awaiting-human`, `terminal-until-intervention`;
   - folder lifecycle;
   - projection freshness/availability;
   - visibility/redaction.
4. Show `unknown_provider_outcome` as automatic reconciliation in progress, including safe reason, last check, remaining check/time budget, and next check. Show `awaiting-human` only at `reconciliation_required`; expose no retry or takeover control.
5. Pin the architecture-required components: Authorized Search & Results, Access Evidence, Provider Readiness Evidence, Metadata Folder Tree/Table, Diagnostic Timeline, Trust Matrix, and Incident-Evidence Authorization Gate.
6. Add **UX-DR33**:

> Before incident evidence performs stream lookup, event counting, checkpoint lookup, filtering, or shaping, the same actor must hold incident-admin permission and fresh current tenant/folder authorization. The gate exposes only bounded C9-redacted metadata with a persistent degraded warning, checkpoint, time window, and correlation context; all denial cases fail before observation and reveal no hidden-resource existence.

7. Keep the console read-only and metadata-only; preserve WCAG 2.2 AA and existing no-mutation/no-content-browser constraints.

**Rationale:** Epic 6 cannot be implementation-ready while the UX specification cannot distinguish lifecycle, lock, disposition, freshness, and visibility or express the mandatory dual authorization gate.

### 4.5 Apply Execution Holds, Context Regeneration, and Gate-Lockstep Follow-up — Approved

**Artifacts:** `sprint-status.yaml` metadata/comments, story manifest, pre-2026-07-19 story contexts, Contract Spine/C13 follow-up, readiness output.

**OLD:**

> Sprint status contains the ratified inventory but Story 3-10 is in progress outside the readiness report's two-story allowance. Older contexts may cite tenant/folder/workspace lock scope, subset idempotency, four dispositions, “Blazor Server,” or Story 11.10 product-projection ownership.

**NEW:**

1. Record an execution hold for all work except Stories 12-6 and 10-8 until the reconcile pass completes. Keep Story 3-10 lifecycle `in-progress` but suspend further execution; preserve its existing file and evidence.
2. Regenerate affected pre-2026-07-19 story contexts before development resumes. Do not overwrite completed historical evidence.
3. Finish 12-6, then author/execute 12.1 → 12.2/12.3 → 12.4 → 12.5 from reconciled authority; open Epic 4/6/10 production-closure stories only when prerequisites are satisfied. Story 10-8 may continue within its authored prerequisite gates.
4. Reconcile Contract Spine and C13 in lockstep for OQ7/OQ8: all mutations, read-key rejection before query execution, `idempotency_key_expired`, canonical alias-colliding lock identity, and generated denominators.
5. Add planning-consistency validation for duplicate/untracked IDs, multiple/missing authorities, conflicting statuses, stale counts, missing aliases, absent story headings, forward dependencies, product/workstream misclassification, and fake/seed/unavailable-only completion evidence.
6. Rerun implementation readiness into a new dated, non-overwriting report after approved edits and validation.

**Rationale:** Reconciliation must change actual authoring and scheduling behavior, not only document the drift.

## 5. Recommended Implementation Sequence

1. Approve this proposal's incremental edits.
2. Freeze story creation/execution outside the two-story allowance; annotate the Story 3-10 hold.
3. Reconcile `epics.md` requirements, portfolio, story definitions, ownership, dependency spine, and provenance.
4. Reconcile the UX specification and add UX-DR33.
5. Create the story manifest and run planning-consistency validation.
6. Reconcile/regenerate affected story contexts and perform OQ7/OQ8 Contract Spine/C13 work in gate lockstep.
7. Resume implementation in the durable dependency order.
8. Rerun implementation readiness into a new report without overwriting the 2026-07-19 trigger.

## 6. Risks and Mitigations

| Risk | Level | Mitigation |
| --- | --- | --- |
| Story work advances from stale authority | High | Two-story allowance plus explicit Story 3-10 hold and manifest-selected authority. |
| Reopened work is read as invalidating completed work | Medium | Preserve historical completion and add narrowly scoped production-closure acceptance. |
| Epic 12 duplicates product projections | Medium | Limit Epic 12 to durable substrate; Epics 4/6/10 own consumer projections and evidence. |
| FR58 scope expands back to body recall | High | Pin current metadata-token completion in FR inventory, Epic 10 chain, and UX. |
| Incident view leaks existence before authorization | High | UX-DR33 plus architecture/FR56 pre-observation dual authorization. |
| Manifest and canonical artifacts drift again | High | Generated counts and deterministic cross-artifact validation. |
| Contract changes are edited out of gate lockstep | High | Keep OQ7/OQ8 Contract Spine, C13, SDK, error, tests, and governance edits atomic. |

## 7. Success Criteria

The correction is complete when:

1. `epics.md`, the PRD, architecture, UX, sprint status, and the story manifest agree on authority, MVP posture, ownership, and inventory.
2. All 58 FR clauses in `epics.md` match current PRD semantics; clause-level validation reports no material drift.
3. Epics 12/13 and every ratified closure/split story have one canonical definition and one lifecycle record.
4. The Epic 12 dependency spine and Epic 10 FR58 chain are visible and mechanically validated.
5. Story 11.10 has no product-projection ownership.
6. UX expresses all six display dimensions, exact vocabularies, automatic reconciliation, required components, and UX-DR33.
7. No new implementation outside Stories 12-6 and 10-8 proceeds from stale authority; Story 3-10 is held without evidence loss until reconciled.
8. OQ7/OQ8 planning and contract surfaces agree in gate lockstep.
9. A new implementation-readiness report supersedes the NOT READY trigger only after validation and contains no stale-authority finding.

## 8. Implementation Handoff

**Scope classification:** Major.

| Role | Responsibility |
| --- | --- |
| Product Manager | Own product-MVP truth, actor/FR58 scope, and product-epic acceptance. |
| Solution Architect | Own dependency direction, durable boundaries, projection ownership, and OQ7/OQ8 architecture consistency. |
| Product Owner | Own canonical backlog, story authority/manifest, aliases, statuses, execution holds, and ordering. |
| UX Designer | Own six-dimension presentation, authorized discovery, automatic-reconciliation UX, required components, and UX-DR33. |
| Developer | Preserve existing evidence, regenerate stale contexts, and execute only against manifest-selected authority. |
| Test Architect | Own clause-level coverage, planning-consistency gates, restart/replay/live-path evidence, and the readiness rerun. |
| Security/Authorization | Own incident dual-authorization and OQ7/OQ8 denial/no-leak evidence. |

## 9. Approval Record

Administrator approved Proposals 4.1–4.5 and the consolidated Major-scope correction with the response `approve all` on 2026-07-20.

- **Final decision:** Approved.
- **Approval time:** 2026-07-20T00:14:08+02:00.
- **Change scope:** Major.
- **Selected approach:** Direct adjustment under the ratified 2026-07-14/15 durable-MVP authority.
- **Primary routing:** Product Manager and Solution Architect own the fundamental planning reconciliation.
- **Supporting routing:** Product Owner, UX Designer, Developer, Test Architect, and Security/Authorization own backlog authority, UX reconciliation, implementation feasibility, production evidence, and gate integrity.
- **Execution allowance:** Stories 12-6 and 10-8 may proceed. All other implementation waits for the reconcile pass; Story 3-10 retains its truthful `in-progress` lifecycle and evidence under an explicit execution hold.
- **Source artifacts changed by this workflow:** None; this finalized Sprint Change Proposal only.
- **Artifacts awaiting implementation:** `epics.md`, `ux-design-specification.md`, the planning story manifest, execution-hold/status metadata, affected story contexts, OQ7/OQ8 gate-lockstep artifacts, planning validation, and the non-overwriting readiness rerun.
- **Code/infrastructure authorization:** No application code, infrastructure, deployment, package, submodule, or external-repository mutation was performed by this workflow.

The Correct Course handoff is complete. The routed owners now execute Section 5 in order; a new implementation-readiness assessment remains the governing completion gate.
