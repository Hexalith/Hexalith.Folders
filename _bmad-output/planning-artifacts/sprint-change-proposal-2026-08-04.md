---
title: "Sprint Change Proposal — Approved Planning Reconciliation Recovery"
project: "Hexalith.Folders"
date: "2026-08-04"
preparedFor: "Administrator"
workflow: "bmad-correct-course"
reviewMode: "batch"
status: "approved"
changeScope: "major"
recommendedPath: "direct-adjustment-and-authority-recovery"
sourceArtifactsModified: false
approvalRequired: false
approvedAt: "2026-08-04T11:43:45+02:00"
approvedBy: "Administrator"
approvalResponse: "approve"
handoffStatus: "recorded"
sprintStatusUpdated: true
trigger:
  artifact: "_bmad-output/planning-artifacts/implementation-readiness-report-2026-08-04.md"
  result: "NOT READY"
recoversApprovedChange:
  artifact: "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-19.md"
  approvedAt: "2026-07-20T00:14:08+02:00"
  propagationStatus: "incomplete"
preserves:
  - "completed implementation and historical evidence"
  - "the ratified durable repository-backed product MVP"
  - "the 2026-08-04 readiness report as immutable trigger evidence"
---

# Sprint Change Proposal — Approved Planning Reconciliation Recovery

## 1. Issue Summary

The 2026-08-04 implementation-readiness assessment found the selected planning set **NOT READY**. It documented 30 issues: 10 open PRD/release-contract items, 8 UX/architecture alignment or production-support gaps, and 12 epic/story quality findings.

The decisive blocker is structural. `epics.md` still describes the 2026-07-07 control-plane backlog and omits the durable data plane, production projections, and ratified Epic 12/13 work needed for a production vertical slice. Safe-empty, no-op, unavailable, seed-only, in-memory, and fake-backed paths preserve confidentiality, but they do not prove durable persistence, restart recovery, real Git persistence, terminal task state, populated operational evidence, or an authorized non-empty FR58 round trip.

This is also a recurrence of an already-approved correction. The 2026-07-19 Sprint Change Proposal was approved on 2026-07-20, but its `epics.md`, UX, manifest, status-control, and validation changes were not propagated. The evidence is direct:

- `epics.md` is still dated 2026-07-07, declares 11 epics and 115 stories, and ends at Epic 11.
- `ux-design-specification.md` is still dated 2026-07-07 and retains ambiguous global search, stale mixed state vocabulary, and MVP components under a Phase 2 label.
- `sprint-status.yaml` already registers the ratified splits, Epic 12, and Epic 13, creating two competing backlog views.
- Architecture already assigns production projections to Epics 4, 6, and 10, while `epics.md` still assigns them to Story 11.10.
- The prior approved proposal records `sourceArtifactsModified: false` and explicitly routes the unapplied changes to downstream owners.

No new product requirement or MVP reduction is proposed. The correction is to execute the approved reconciliation, add the quality dispositions newly made explicit by the 2026-08-04 assessment, and establish one machine-checkable planning authority before general story execution resumes.

### Immediate execution posture

- Do not create or start additional stories from the current `epics.md`.
- Preserve all completed story evidence and truthful lifecycle states; do not rewrite history to imply the durable product path was already delivered.
- Do not widen the 2026-07-20 execution allowance. Story 12.6 may finish its current in-progress work, and Story 10.8 may remain ready only behind the hard prerequisites already written in its dedicated context.
- Keep Story 3.10 on an execution hold until its `sprint-status.yaml` and dedicated-story status mismatch is resolved from evidence and its canonical split is present in the reconciled backlog.
- Product acceptance and release remain blocked until the dependency spine and OQ1–OQ10 evidence gates are satisfied and readiness is rerun.

## 2. Change Analysis Checklist

### Section 1 — Trigger and Context

| Item | Status | Finding |
| --- | --- | --- |
| 1.1 Triggering story | [N/A] | The trigger is a cross-artifact implementation-readiness assessment, not one failed story. |
| 1.2 Core problem | [x] | A ratified Major correction was recorded but not propagated to the canonical epic and UX authorities. |
| 1.3 Supporting evidence | [x] | The assessment identifies 30 issues; the current files retain stale counts, semantics, ownership, phasing, and missing story inventory. |
| 1.4 Execution boundary | [x] | Preserve the narrow existing allowance and hold all new work from stale authority. |

### Section 2 — Epic Impact

| Item | Status | Finding |
| --- | --- | --- |
| 2.1 Current epic viability | [!] | Epics 2, 4, 5, 6, 8, and 10 cannot support product-completion claims without the Epic 12 durable substrate and owning production projections. |
| 2.2 Epic changes | [!] | Import the complete ratified inventory, classify product versus enabler/release work, narrow umbrella scopes, and strengthen completion gates. |
| 2.3 Remaining portfolio | [x] | The product goal remains valid; completed control-plane work is preserved within its evidenced scope. |
| 2.4 New or obsolete goals | [N/A] | No product goal is obsolete and no additional product epic beyond the ratified portfolio is required. |
| 2.5 Sequencing | [!] | The OQ and Epic 12 dependency spine must govern creation, execution, and completion of downstream stories. |

### Section 3 — Artifact Conflict and Impact

| Artifact | Status | Required disposition |
| --- | --- | --- |
| PRD | [x] | No semantic scope change. Retain `implementationReadiness: not-ready` and OQ1–OQ10 until governed evidence closes them. |
| Architecture | [x] | Treat the current architecture as the technical target. Only provenance/cross-reference cleanup is needed unless validation finds a direct contradiction. |
| `epics.md` | [!] | Rebuild inventory, semantics, classifications, story definitions, ownership, dependencies, acceptance floors, and generated counts. |
| UX specification | [!] | Reconcile lifecycle/lock/disposition vocabulary, incident authorization, authorized search scope, and MVP component phasing. |
| Sprint status | [!] | Reconcile status metadata and execution holds after the canonical story manifest exists; never infer completion from a stale summary. |
| Story contexts | [!] | Regenerate or amend active contexts that cite superseded authority; preserve completed historical evidence. |
| Contract Spine/C13 and OQ evidence | [!] | Keep OQ7/OQ8 contract changes in gate lockstep and map all OQ1–OQ10 closure evidence to owners and stories. |
| Readiness report | [x] | Preserve the 2026-08-04 report; produce a new, non-overwriting report only after reconciliation and validation. |

### Section 4 — Path Forward

| Option | Viability | Effort | Risk | Decision |
| --- | --- | --- | --- | --- |
| Direct adjustment and authority recovery | Viable | High | Medium | **Recommended.** Apply the already-ratified structural correction, plus the explicit quality dispositions below. |
| Roll back to the 2026-07-07 plan | Not viable | High | Critical | Reject. It would knowingly preserve a non-production control plane as the apparent MVP plan. |
| Reduce or redefine the MVP | Not required | Medium | High product risk | Reject. The durable repository-backed MVP was explicitly ratified and remains coherent. |

**Recommended path:** Major Direct Adjustment and Authority Recovery. Reconcile planning truth first, validate it mechanically, then resume implementation in dependency order. This proposal authorizes no source-artifact, application-code, infrastructure, package, deployment, submodule, or external-repository changes until explicit approval is recorded.

## 3. Impact Analysis

### Product and MVP impact

- The product MVP remains the durable repository-backed lifecycle across REST, SDK, CLI, MCP, and the read-only console.
- Completed control-plane, parity, accessibility, governance, and fail-safe behavior remains valid evidence, but it cannot count as positive durable product completion.
- Epics 4, 6, and 10 retain ownership of their user-facing projections and completion evidence; Epic 12 supplies the durable substrate.
- Epic 13 remains release-blocking security/operations hardening and is excluded from product-capability completion metrics.

### Delivery and timeline impact

- Planning must pause for one focused reconciliation, review, and validation cycle before general story creation resumes.
- Implementation effort remains high and multi-sprint. The prior 2–4 sprint planning range remains only an indicative range until the reconciled stories are estimated against current capacity and unfinished OQ evidence.
- The correction does not add the durable work; it makes already-ratified work, dependencies, and acceptance costs visible.

### Team impact

- Product Management owns product truth, actor authority, FR58 scope, and the OQ decision record.
- Architecture owns dependency direction, durable boundaries, and projection ownership.
- Product Ownership owns the canonical manifest, aliases, status reconciliation, and execution controls.
- UX owns the synchronized console contract.
- Engineering and Test Architecture own feasible story boundaries, positive production evidence, and planning/readiness validation.

## 4. Detailed Batch Change Proposals

### 4.1 Establish one dated implementation authority

**OLD**

The PRD and architecture contain July authority, `epics.md` and UX retain July 7 authority, and `sprint-status.yaml` contains story IDs absent from `epics.md`. A previously approved proposal describes the repair but is not itself an executable story inventory.

**NEW**

1. Create `_bmad-output/planning-artifacts/planning-story-manifest.yaml` with one row per story: ID, title, product/enabler/release classification, lifecycle status, authoritative definition, aliases, prerequisites, requirement clauses, and evidence posture.
2. Declare the current PRD as product authority, the current architecture as technical authority, reconciled `epics.md` as pre-context backlog authority, dedicated story files as execution authority after creation, and `sprint-status.yaml` as lifecycle tracking only.
3. Add the 2026-07-14, 2026-07-15, 2026-07-19, and this 2026-08-04 decision record to provenance.
4. Reject missing or multiple authorities, duplicate IDs, status conflicts, missing aliases, and stories present in sprint status but absent from the canonical manifest.

**Rationale:** Another narrative proposal without authority selection would reproduce the same failure.

### 4.2 Rebuild the epic requirements inventory and portfolio classification

**OLD**

`epics.md` declares fixed counts (`epicCount: 11`, `storyCount: 115`, `productEpicCount: 6`), contains 116 story headings, and maps all 58 FR identifiers using materially stale clauses. Its NFR inventory predates the PRD's current 73 normalized statements.

**NEW**

1. Generate epic/story/product counts from the manifest instead of maintaining literals by hand.
2. Regenerate all 58 FR clauses and 73 normalized NFR clauses from the current PRD, preserving stable IDs and clause-level traceability.
3. Correct at minimum:
   - FR4/FR15 tenant-administrator configuration ownership and platform-engineer validation;
   - FR28 exact lock vocabulary: `unlocked`, `locked`, `expired`, `stale`, `revoked`;
   - FR30 cleanup/retention behavior;
   - FR41/FR42 all-mutation idempotency, read-key rejection, consumed-key recognition, and expired-key precedence;
   - FR44 canonical error taxonomy;
   - FR45 all 11 lifecycle states, separate from lock state and operator disposition;
   - FR56 incident-admin plus fresh tenant/folder authorization before observation;
   - FR58 metadata-token recall with current-authority hydration and a non-empty deployed completion path.
4. Classify Product Epics 2–6, 10, and 12 as user-value work. Classify Epic 1 and Epics 9/11 as technical enablers, Workstreams 7/8 as release/quality closure, and Epic 13 as security/operations hardening. Do not count those workstreams as product-capability completion.
5. Preserve Story 2.8b as a superseded alias of Story 2.8 and preserve completed Stories 8.1/8.2 as historical batches, not active implementation-ready definitions.

**Rationale:** Identifier coverage is not semantic coverage, and technical completion must not inflate product completion.

### 4.3 Import the complete ratified story portfolio

**OLD**

The canonical epic plan ends at Epic 11 and omits stories already registered in sprint status and ratified by architecture and approved change records.

**NEW**

Import one canonical definition for each existing ratified item, using the approved July records and current architecture rather than creating competing scopes:

- Epic 3: Stories 3.10–3.14.
- Epic 4: Stories 4.18–4.21.
- Epic 5: Stories 5.8–5.11.
- Epic 6: Stories 6.12–6.14.
- Epic 10: Stories 10.7–10.9.
- Workstream 11: Stories 11.14–11.21.
- Product Epic 12: Stories 12.1–12.6, including the report's missing 12.1–12.5 and the already-active all-mutations idempotency Story 12.6.
- Hardening Epic 13: Stories 13.1–13.6 exactly as registered in sprint status, without duplicating Workstream 11.

Every imported story must include implementable acceptance criteria in `epics.md`; a title in sprint status is not a sufficient story definition.

**Rationale:** Story creation must discover the ratified portfolio from the canonical backlog rather than from historical proposals or comments.

### 4.4 Repair ownership, dependency direction, and completion truth

**OLD**

`epics.md` assigns transition evidence, diagnostics, and the deployed search bridge to Story 11.10; dependent product stories appear complete before their absent durable prerequisites; Epic 10 is labelled Phase 2 and later stories retroactively change the meaning of Stories 10.1–10.5.

**NEW**

Use the governing sequence:

```text
OQ1–OQ4
  -> 12.1 EventStore repository/replay
  -> 12.2 durable task/projection substrate
     + 12.3 durable file-content source
       -> 12.4 real Git write path
       -> 12.5 at-least-once Memories egress/reconciliation
  -> {4.18–4.21 lifecycle evidence,
      6.12–6.14 populated diagnostics/incident evidence,
      10.7–10.8 deployed search bridge/round trip}
  -> OQ5–OQ9
  -> OQ10
  -> implementation-readiness rerun
```

Ownership becomes:

- Epic 4 owns workspace transition evidence and durable prepare/lock/mutation/commit/reconciliation proof.
- Epic 6 owns seven populated diagnostic projections and deployed operator/audit/incident journeys.
- Epic 10 owns the Server-referenceable search bridge, current-authority hydration/redaction/pruning, and the real non-empty FR58 round trip; Story 10.9 remains a separately authorized C9-gated body-content follow-on.
- Epic 12 owns durable source events, authoritative content/state, restart replay, task completion, real Git persistence, and recoverable egress substrate.
- Story 11.10 owns EventStore admission/subscription seam adoption only; Story 11.14 owns Memories publication/search-client seams; Story 11.15 owns the DCP-capable verification lane. Workstream 11 owns no product projection.

Apply one acceptance floor to every positive production capability:

- real deployed composition, not only a test registration;
- restart-surviving durable population and deterministic empty-checkpoint replay where applicable;
- tenant isolation with attached negative evidence;
- positive behavior plus safe denial, conflict, failure, timeout/unknown-outcome, and boundary evidence;
- honest unavailable/degraded behavior without counting it as positive completion;
- no completion claim supported only by NoOp, in-memory, seed, unavailable, safe-empty, or fake evidence.

**Rationale:** A dependent user capability cannot complete before the substrate and owning projection that make it true.

### 4.5 Resolve broad stories without rewriting completed history

**OLD**

The readiness review identifies Stories 1.4, 3.4, 4.8, 4.16, 6.11, 7.17, 8.5, 10.6, 11.2, 11.3, 11.5, 11.7, 11.10, 11.11, and 11.13 as cross-concern, cross-repository, or otherwise too broad.

**NEW**

Use three explicit dispositions in the manifest:

1. **Historical batch — preserve evidence, do not reactivate the umbrella.** Apply to completed Stories 1.4, 4.16, 6.11, 7.17, and 8.5. Link their independently verified outputs/gates; new defects become narrow follow-up stories rather than silent expansion.
2. **Existing ratified split — import and narrow.** Use Stories 3.10–3.14, 4.18–4.21, 5.8–5.11, and 11.14–11.21 to carry independently owned provider, durable lifecycle, adapter, platform-seam, DCP, gate, test-double, UI, ADR, and final-verification work. Narrow 11.2/11.3/11.5/11.7/11.10/11.11/11.13 so each retains only its named owning concern.
3. **Active-story narrowing before resume.** Limit Story 10.6 to metadata-derived materialization and its C4/C9 contract. Move deployed bridge/registration work to 10.7 and live orchestration/round-trip evidence to 10.8. For the old Story 4.8 scope, keep the completed surface as historical and place durable mutation/context proof in 4.20, with C4 limits and each tree/metadata/range/glob/search family independently verifiable in its tasks and evidence.

If decomposition reveals a genuinely independent outcome not represented by the ratified IDs, the Product Owner must add a new manifest ID and obtain targeted approval; alphabetic or undocumented IDs must not be improvised.

**Rationale:** Completed evidence should remain trustworthy while active work becomes small enough to estimate, implement, and verify independently.

### 4.6 Strengthen weak acceptance criteria and startup determinism

**OLD**

Several stories use BDD syntax but omit decisive current behavior, and Story 1.1 refers only to an unspecified approved layout.

**NEW**

- Story 1.1 names the exact sibling Hexalith structure and EventStore Admin CLI/MCP/UI reference patterns, what configuration/dependency conventions are reproduced, and what is explicitly excluded.
- Story 2.3 proves fresh authorization, denied no-side-effect behavior, all-mutation idempotency, durable persistence/replay, and safe lifecycle results.
- Story 3.1 assigns configuration to tenant administrators, validation to platform engineers, and covers wrong-tenant, revoked, stale, hidden, and unauthorized credential-reference cases without observation.
- Story 4.2 proves `unknown_provider_outcome` first, bounded automatic reconciliation checks second, and `reconciliation_required` only after the governed budget is exhausted.
- Story 4.8/4.20 pins exact C4 numeric limits, canonical result/error semantics, tenant/path policy, cancellation, and authoritative content-source behavior.
- Story 6.9 requires incident-admin and fresh tenant/folder authorization before stream lookup, counting, checkpoint access, filtering, or shaping; every denial emits one safe audit record and leaks no existence.
- Story 10.5/10.7/10.8 specifies candidate authorization before observation, current-authority hydration, dropped stale/unauthorized candidates, safe unavailable behavior, removal/archive pruning, and a non-empty deployed round trip.
- Every new/active story links stable definitions for C3/C4/C6/C9, S-2, and OQ evidence instead of relying on an unexplained acronym alone.

**Rationale:** BDD form is useful only when the positive, negative, boundary, failure, and production-runtime outcomes are decisive.

### 4.7 Reconcile the UX implementation contract

**OLD**

The UX specification mixes lifecycle, lock, visibility, and freshness labels; calls search global without stating its authorization boundary; omits pre-observation dual authorization for incident evidence; and places Diagnostic Timeline and Trust Matrix in Phase 2 despite MVP requirements.

**NEW**

1. Define six independent dimensions:
   - lifecycle: `requested`, `preparing`, `ready`, `locked`, `changes_staged`, `dirty`, `committed`, `failed`, `inaccessible`, `unknown_provider_outcome`, `reconciliation_required`;
   - lock: `unlocked`, `locked`, `expired`, `stale`, `revoked`;
   - disposition: `available`, `auto-recovering`, `degraded-but-serving`, `awaiting-human`, `terminal-until-intervention`;
   - folder lifecycle;
   - projection freshness/availability;
   - visibility/redaction.
2. Define global search as global only inside the caller's already-authorized tenant/folder scope. Authorization precedes candidate lookup, result counting, suggestions, filters, and empty-state classification.
3. Add UX-DR33, the Incident-Evidence Authorization Gate: the same actor must hold incident-admin permission and fresh tenant/folder authorization before any observation; evidence remains bounded, C9-redacted, metadata-only, read-only, and denial-audited.
4. Show `unknown_provider_outcome` as automatic reconciliation in progress with safe reason, last check, remaining check/time budget, and next check. Show `awaiting-human` only for `reconciliation_required`; expose no retry/takeover control.
5. Move Diagnostic Timeline, Trust Matrix, Provider Readiness Evidence, Access Evidence, Authorized Search & Results, and Incident-Evidence Authorization Gate into the MVP component set. Keep dark theme and saved filters deferred.
6. Use the architecture's host terminology: Blazor Web App, Interactive Server through `FrontComposerShell`.
7. Preserve read-only, metadata-only, no-content-browser, no-repair, no-secret, no-hidden-existence, responsive, and WCAG 2.2 AA constraints.

**Rationale:** The production projection plan cannot be implemented consistently against stale or ambiguous UX state and authorization semantics.

### 4.8 Preserve the PRD and close OQ1–OQ10 through governed evidence

**OLD**

The PRD clearly identifies ten open release items, but the stale epic plan does not provide one visible dependency/evidence route for all of them.

**NEW**

- Make OQ1–OQ4 explicit prerequisites for the durable substrate where their decisions affect timing, file policy, authorization, and provider reconciliation.
- Map OQ5 to 10.7–10.8, OQ6 to 4.18 and 6.12–6.14, OQ7 to canonical lock identity and alias-collision contracts/tests, OQ8 to Story 12.6 plus Contract Spine/C13 lockstep, OQ9 to Story 6.9/6.14 plus UX-DR33, and OQ10 to the final calibrated release plan.
- Record owner, evidence artifact, approval authority, status, and blocking consumers for every OQ in the manifest or a linked decision register.
- Keep the PRD NOT READY until evidence and approvals actually exist; do not close an OQ because a story title or safe fallback exists.

**Rationale:** Open questions are acceptable only when their closure path and blocking effect are executable and visible.

### 4.9 Reconcile lifecycle status, story contexts, and execution controls

**OLD**

Sprint status contains the ratified portfolio but disagrees with the canonical epic file. Story 3.10 is `in-progress` in sprint status and `ready-for-dev` in its dedicated story file. Older contexts may contain stale actors, states, projection ownership, idempotency scope, or FR58 framing.

**NEW**

1. After the manifest is generated, reconcile each lifecycle status from the dedicated story file and verifiable evidence; record conflicts rather than choosing the most advanced label.
2. Keep completed historical evidence immutable. Reopening a product epic adds closure work and does not retroactively erase completed control-plane stories.
3. Record the general execution hold and the narrow Story 12.6/10.8 allowance in sprint metadata. Resolve Story 3.10's mismatch before it resumes.
4. Regenerate or amend active pre-authority contexts before execution. Do not overwrite finished implementation records.
5. Make the manifest-selected story definition a prerequisite for `bmad-create-story`, sprint planning, and development handoff.

**Rationale:** Reconciliation must alter authoring and scheduling behavior, not merely add another document.

### 4.10 Add planning validation and rerun readiness without overwrite

Add deterministic checks for:

- 58 exact FR clauses and 73 normalized NFR clauses;
- manifest/epic/status/story-file ID and title agreement;
- generated counts and alias handling;
- product/enabler/release classification;
- missing or multiple authorities;
- forbidden stale ownership and vocabulary;
- dependency cycles and hidden forward dependencies;
- required acceptance-floor clauses for durable/read-model/runtime stories;
- completion claims supported only by NoOp, unavailable, seed-only, safe-empty, in-memory, or fake evidence;
- UX lifecycle/lock/disposition vocabulary, tenant-bounded search, UX-DR33, and MVP component phasing.

Only after these checks pass should implementation readiness be rerun into a new dated file. The 2026-08-04 report remains unchanged as trigger evidence.

## 5. Recommended Implementation Sequence

1. **Completed:** Administrator approved this batch proposal on 2026-08-04.
2. Record the execution hold and preserve the narrow current allowance.
3. Create the planning story manifest and authority register.
4. Reconcile `epics.md`: requirements, classifications, full portfolio, story definitions, ownership, dependencies, acceptance floors, and counts.
5. Reconcile the UX specification and add UX-DR33.
6. Reconcile sprint status and active story contexts from evidence.
7. Add and run planning-consistency validation.
8. Close decision prerequisites OQ1–OQ4, then execute the durable sequence 12.1 → 12.2/12.3 → 12.4/12.5.
9. Execute the owning Epic 4/6/10 production closure stories, then close OQ5–OQ9.
10. Close OQ10 and rerun implementation readiness without overwriting prior reports.

## 6. Risks and Mitigations

| Risk | Level | Mitigation |
| --- | --- | --- |
| A third authority competes with the approved July plan | High | Treat this proposal as recovery/amendment; use one manifest and reuse ratified IDs/scopes. |
| Completed work is invalidated or rewritten | Medium | Preserve historical evidence and add narrow production-closure stories. |
| Story work continues from stale text | Critical | Execution hold, manifest-selected authority, and context regeneration before resume. |
| Epic 12 duplicates product projections | High | Limit Epic 12 to durable substrate; keep projections in Epics 4/6/10. |
| FR58 expands to raw body/content recall | High | Keep current MVP at metadata-token recall; Story 10.9 remains separately C9-gated. |
| Incident mode leaks existence | Critical | UX-DR33 and Story 6.9/6.14 pre-observation dual-authorization evidence. |
| Story splitting fabricates new scope or loses status | Medium | Use existing ratified split IDs first; require targeted approval for any genuinely new ID. |
| Planning documents drift again | High | Generated manifest/counts plus deterministic cross-artifact validation. |

## 7. Success Criteria

The correction is complete when:

1. PRD, architecture, UX, `epics.md`, sprint status, and the manifest identify one current authority and agree on MVP posture.
2. `epics.md` contains the full ratified portfolio, including Epic 12/13 and all registered closure/split stories.
3. All 58 FR and 73 NFR clauses are semantically current and trace to independently executable stories/evidence.
4. The durable dependency spine is explicit, acyclic, and enforced.
5. Story 11.10 contains no product-projection ownership.
6. UX carries exact lifecycle/lock/disposition vocabularies, tenant-bounded search, MVP diagnostic components, automatic-reconciliation presentation, and UX-DR33.
7. Broad completed stories are classified as historical batches; active umbrella stories are narrowed or use ratified splits.
8. OQ1–OQ10 each has an owner, evidence artifact, approval path, status, and blocking consumers.
9. No product completion is supported only by fail-safe or test-only substitutes.
10. A new non-overwriting readiness assessment no longer reports planning-authority drift or an absent production vertical-slice plan.

## 8. Implementation Handoff

**Scope classification:** Major — backlog and planning-authority replan; no MVP definition change.

| Role | Responsibility |
| --- | --- |
| Product Manager | Product-MVP truth, actor ownership, FR58 boundary, OQ decisions, and product acceptance. |
| Solution Architect | Dependency spine, durable boundaries, projection ownership, and architecture consistency. |
| Product Owner | Manifest, canonical backlog, aliases, lifecycle reconciliation, execution holds, and ordering. |
| UX Designer | Six-dimension state model, authorized discovery, reconciliation UX, MVP component phasing, and UX-DR33. |
| Developer | Feasibility review, preservation of evidence, active-context regeneration, and dependency-ordered execution. |
| Test Architect | Acceptance floors, semantic traceability, planning-consistency gates, production evidence, and readiness rerun. |
| Security/Authorization | OQ3/OQ7/OQ8/OQ9 decisions and denial/no-leak evidence. |

### Checklist completion posture

| Item | Status |
| --- | --- |
| Change analysis and path selection | [x] |
| Batch proposal drafted | [x] |
| Scope and handoff identified | [x] |
| Stakeholder review | [x] Complete |
| Explicit approval | [x] Administrator, 2026-08-04T11:43:45+02:00 |
| Sprint-status update | [x] Approved recovery action and execution control recorded |
| Source-planning-artifact updates | [!] Routed to Product Manager / Solution Architect; not executed by this workflow |
| Readiness rerun | [!] Pending implementation and validation |

## 9. Review Record

Administrator approved this batch proposal for implementation with the response `approve` on 2026-08-04 at 11:43:45+02:00. The earlier 2026-07-20 approval remains evidence of the ratified product direction; this approval authorizes the recovery edit set and handoff described here.

- Final decision: Approved.
- Selected approach: Major Direct Adjustment and Authority Recovery.
- Routed to: Product Manager and Solution Architect, supported by Product Owner, UX Designer, Developer, Test Architect, and Security/Authorization.
- Source planning artifacts changed by this workflow: none.
- Workflow artifacts changed: this finalized proposal and sprint-status execution-control/action metadata.
- Application code, infrastructure, packages, deployment, submodules, and external repositories changed: none.
- Trigger report preservation: required.
