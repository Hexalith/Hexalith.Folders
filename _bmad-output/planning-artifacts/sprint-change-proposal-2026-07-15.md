---
title: "Sprint Change Proposal — Post-Readiness Authority and Delivery Reconciliation"
project: "Hexalith.Folders"
date: "2026-07-15"
preparedFor: "Administrator"
workflow: "bmad-correct-course"
status: "approved"
approvedAt: "2026-07-15T02:02:07+02:00"
approvedBy: "Administrator"
approvalResponse: "yes"
changeScope: "major"
reviewMode: "incremental"
selectedPath: "direct-adjustment-with-structural-reconciliation"
incrementalEditsApproved: 12
sourceArtifactsModified: false
sprintStatusDelta: "none-required-approved-epics-and-closure-stories-already-registered"
handoffStatus: "recorded"
handoffRecipients:
  - "Product Manager"
  - "Solution Architect"
  - "Product Owner"
  - "Developer"
  - "Test Architect"
trigger:
  artifact: "_bmad-output/planning-artifacts/implementation-readiness-report-2026-07-15.md"
  result: "NOT READY"
amends:
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-14-implementation-readiness-structural-correction.md"
preserves:
  - "_bmad-output/planning-artifacts/implementation-readiness-report-2026-07-15.md"
  - "_bmad-output/planning-artifacts/implementation-readiness-report-2026-07-14.md"
---

# Sprint Change Proposal — Post-Readiness Authority and Delivery Reconciliation

## 1. Issue Summary

The 2026-07-15 implementation-readiness assessment found the planning set **NOT READY**. All 58 functional-requirement numbers are mapped, but only 42 requirements are semantically complete. Sixteen retain missing, stale, or contradictory binding clauses. Production diagnostics, workspace-transition evidence, and authorized search remain unavailable, seed-only, or assigned to later technical work. Tenant-administration authority and incident-mode authorization are inconsistent. The canonical epics document also declares 115 stories while containing 116 headings.

The immediate cause is an incomplete artifact synchronization. The approved 2026-07-14 structural-correction proposal already registered reopened product epics and new closure stories in `sprint-status.yaml`, including Stories 4.18–4.21, 6.12–6.14, and 10.7–10.9, plus Epics 12 and 13. Those approved changes were not incorporated into the canonical `epics.md`, architecture, UX, PRD metadata, story-authority manifest, or traceability artifacts. The 2026-07-15 readiness assessment therefore correctly evaluated a stale canonical planning set.

This proposal:

1. requires completion of the already-approved 2026-07-14 structural synchronization;
2. adds the clause-level corrections identified on 2026-07-15;
3. preserves completed implementation and historical evidence;
4. keeps the durable repository-backed MVP unchanged;
5. prevents safe-empty, seed-only, unavailable, or fake-backed paths from being claimed as completed product capability.

### Evidence

- 58 total FRs; 42 fully aligned; 16 partial; 72.4% verified semantic coverage.
- 10 UX/PRD/architecture alignment issues.
- 15 epic-quality issues: 4 critical, 6 major, and 5 minor.
- 10 open release decisions/evidence packages, OQ1–OQ10.
- `epics.md` declares 115 stories but contains 116 story headings.
- `sprint-status.yaml` already contains approved planning entries absent from `epics.md`.
- Architecture explicitly records unpopulated production diagnostics, transition evidence, and deployed search hydration.

## 2. Change Analysis Checklist

### Section 1 — Trigger and Context

| Item | Status | Finding |
| --- | --- | --- |
| 1.1 Triggering story | [N/A] | The trigger is the cross-artifact readiness assessment, not one story. Stories 2.8/2.8b, 3.1, 4.3/4.11, 6.9, 10.5, and 11.10 provide representative evidence. |
| 1.2 Core problem | [x] | Finalized requirements were not reconciled into downstream planning; product completion is overstated by numeric mappings and safe scaffolds. |
| 1.3 Supporting evidence | [x] | The readiness report contains clause-level FR, UX, architecture, dependency, story-size, and release-decision evidence. |

### Section 2 — Epic Impact

| Item | Status | Finding |
| --- | --- | --- |
| 2.1 Current epic | [N/A] | The trigger spans the complete portfolio. |
| 2.2 Epic-level changes | [!] | Reclassify technical tracks, import the approved Epic 12/13 plan, reopen product epics, and move projection ownership to consuming product epics. |
| 2.3 Remaining epics | [x] | Every epic/workstream was reviewed; Epics 3–6, 10, and 12 carry active product closure. |
| 2.4 New/obsolete epics | [!] | No product goal is obsolete. Technical Epics 1, 8, 9, and 11 must be treated as enabling/remediation/refactoring workstreams. |
| 2.5 Order and priority | [!] | Decisions OQ1–OQ4 and HXF operational blockers precede dependent product evidence; production closure precedes readiness rerun. |

### Section 3 — Artifact Conflict and Impact

| Item | Status | Finding |
| --- | --- | --- |
| 3.1 PRD | [!] | MVP remains achievable. UJ2, FR56, delivery posture, and downstream authority need explicit reconciliation; OQ1–OQ10 remain release blockers. |
| 3.2 Architecture | [!] | Canonical lock identity, all-mutations idempotency, state/disposition vocabulary, incident authorization, hosting terminology, production read models, and UI structure require updates. |
| 3.3 UX | [!] | Authorized search scope, exact state dimensions, automatic reconciliation, dual incident authorization, and MVP phase semantics require correction. |
| 3.4 Other artifacts | [!] | Contract Spine, C13, error catalog, sprint status, planning manifest, traceability, exit-criteria evidence, tests, CI, and project context require synchronized changes. |

### Section 4 — Path Forward

| Option | Viability | Effort | Risk | Decision |
| --- | --- | --- | --- | --- |
| Direct adjustment | Viable | High | Medium | Selected, continuing the approved structural replan |
| Rollback | Not viable | High | High | Reject; completed contract/control-plane work remains valuable |
| Reduce/redefine MVP | Not required | Medium | High product risk | Reject; durable repository-backed MVP remains valid |

**Recommended path:** Major Direct Adjustment with structural reconciliation. Complete the approved 2026-07-14 planning synchronization, apply the 2026-07-15 semantic corrections, close OQ1–OQ10, and rerun implementation readiness. No application-code change is authorized by this proposal itself.

## 3. Impact Analysis

### Epic and Story Impact

- **Workstream 1:** retain completed canonical-contract history; classify as enablement, not a product epic. Reopen contract artifacts only where the current PRD requires alignment.
- **Epic 2:** merge Story 2.8b evidence into Story 2.8; strengthen archive eligibility, provider no-touch, mixed-retention, and production-path criteria.
- **Epic 3:** correct tenant-admin configuration authority; add duplicate/alias binding behavior; retain approved provider story splits in sprint status and import them into `epics.md`.
- **Epic 4:** retain approved Stories 4.18–4.21; strengthen lock identity, cleanup, change sets, context queries, idempotency, error taxonomy, and reconciliation evidence.
- **Epic 5:** retain approved CLI/MCP split stories and add the current full-denominator idempotency/error cases to parity evidence.
- **Epic 6:** retain approved Stories 6.12–6.14; enforce dual incident authorization; align UX state, discovery, and production evidence.
- **Workstreams 7–9:** preserve completed governance/remediation/topology history; exclude from product-completion metrics.
- **Epic 10:** rename around authorized Folders search/index lifecycle; retain approved Stories 10.7–10.9; require non-empty production evidence for FR58.
- **Workstream 11:** remove product-projection ownership; retain platform-seam, refactoring, and DCP verification responsibilities only.
- **Epic 12:** import the already-ratified durable repository-backed round-trip epic into canonical `epics.md`.
- **Epic 13:** import already-ratified security/operations hardening into canonical `epics.md`, outside product-completion metrics.

### Technical and Release Impact

- Contract Spine and generated C13 inventory must use the current generated denominator, not hard-coded 47/49 counts.
- Every mutation must obey the same idempotency contract; every read must reject an idempotency key.
- Production projections must be authored, registered, replayable, tenant-isolated, and exercised through deployed paths.
- The release remains blocked by OQ1–OQ10 until evidence, approvals, versions, and digests exist.
- The current 2026-07-15 readiness report remains immutable historical evidence; a new report is generated after corrections.

## 4. Detailed Change Proposals

### 4.1 Tenant-Administrator Configuration Authority — Approved

**Artifacts:** PRD UJ2; epics requirements inventory; Epic 3 summary; Stories 3.1 and 3.8.

**OLD:**

> Platform engineers configure tenant provider bindings and credential references.

**NEW:**

> Tenant administrators own provider bindings, credential references, repository naming/default-ref policy, capability policy, folder ACLs, and archive decisions. Scoped platform engineers validate and diagnose readiness but cannot silently mutate tenant policy.

Story 3.1 becomes tenant-administrator-authored configuration with wrong-tenant, operator-only, stale, revoked, and hidden-resource denials before provider or credential access. Story 3.8 becomes tenant-admin-owned branch/ref policy. Every allow/deny emits metadata-only audit evidence.

### 4.2 Archive as a Production-Safe Vertical Slice — Approved

**Artifacts:** Stories 2.8, 2.8b, and 7.11; sprint status; story manifest.

**OLD:**

> Archive when policy allows; retain audit and status evidence. Production `/process` wiring is a later Story 2.8b.

**NEW:**

> Archive only when no active task or lock exists and no workspace is `changes_staged`, `dirty`, `unknown_provider_outcome`, or `reconciliation_required`. The real REST → gateway → `/process` → domain processor → persistence path appends exactly one event. Archive never deletes or mutates the provider repository. Later mutations deny safely. Separately authorized ACL revocation, legal-hold, and retention-metadata operations remain permitted.

Archived view fields expire independently by C3 class. An expired field is omitted and replaced with its safe retention-expired marker; shorter-lived fields are not extended to match audit retention.

Story 2.8 absorbs Story 2.8b production wiring, no-mock integration evidence, append-conflict reread, and denial table. Story 2.8b remains a historical `superseded_alias`, not a live story heading.

### 4.3 Canonical Repository Identity and Locking — Approved

**Artifacts:** Stories 3.7, 4.3, and 4.4; architecture C6/locking; OQ7 evidence.

**OLD:**

> Locking is variably described as tenant/folder/workspace scoped. Lock-like terms include active, abandoned, interrupted, and released. `unknown_provider_outcome` maps to awaiting-human.

**NEW:**

> The serializing identity is managed tenant + provider instance/product identity + canonical repository identity + normalized target ref. Every folder, binding, alias, workspace, task, and surface resolving to that identity collides on one active writer.

Lock state is exclusively `unlocked`, `locked`, `expired`, `stale`, or `revoked`. Workspace lifecycle, lock state, operation state, and operator disposition remain distinct. `unknown_provider_outcome` is `auto-reconciling` during the bounded evidence-check budget; `awaiting-human` begins at `reconciliation_required`.

Story 4.4 gains clean-owner, live replay, expired-key, non-owner, revoked, dirty/staged, unknown/reconciliation, expiry, and revocation rows. OQ7 closes only when architecture, Contract Spine, C6, projections, parity, and alias-collision tests agree.

### 4.4 Automatic Cleanup Eligibility and Timing — Approved

**Artifacts:** Stories 4.10 and 7.11; architecture cleanup model.

**OLD:**

> Cleanup status follows completed, failed, interrupted, or abandoned tasks; eligibility and timing are implied.

**NEW:**

> Cleanup is platform-owned and automatic only after task-terminal closure and no active task or lock. Temporary working files are deleted at the C3 seven-day boundary. `changes_staged`, `dirty`, `unknown_provider_outcome`, and `reconciliation_required` are ineligible. Failed/inaccessible closure records final metadata-only evidence and disposition before starting the observation window.

Cleanup status is a separate projection: `pending`, `retrying`, `completed`, or `failed`. Failure escalates but cannot delete required evidence. User-triggered cleanup, repair, discard, and takeover remain outside MVP.

### 4.5 Task Change Sets and Live Workspace Context — Approved

**Artifacts:** Stories 1.9, 4.6–4.8, and 4.14; architecture search contracts.

**OLD:**

> Add/change/remove are separate operations; generic policy boundaries cover context queries.

**NEW:**

> One task may submit one or many add/change/remove operations as one change-set command under fresh authority and a valid lock. Validate the entire request before execution. File mutations never auto-commit. Rename/move is add plus remove in the same task, lock, change set, and commit. Partial/unconfirmed execution enters `unknown_provider_outcome`; commit remains denied until every mutation has a known policy-valid result.

Live workspace context explicitly supports tree, metadata, glob, bounded range, and supported body search. Authorization and path policy complete before scanning, lookup, filtering, ranking, counting, snippet generation, truncation, or shaping. Exact C4 bounds and `isTruncated` behavior are contract criteria. Range/file content is never silently truncated. Telemetry never records raw query text, path lists, content, snippets, or hidden existence.

Live workspace body search remains separate from FR58 indexed metadata-token recall.

### 4.6 All-Mutations Idempotency and Expired-Key Precedence — Approved

**Artifacts:** Stories 1.5, 1.6, 4.11, 4.13, and 5.5–5.7; architecture A-9/D-7; OQ8.

**OLD:**

> Idempotency is described mainly for prepare, lock, file mutation, commit, and cleanup. Replay and conflict are covered; expired-key and read-key behavior are incomplete.

**NEW:**

Every current and future mutation is covered, including folder creation, configuration, binding, policy, ACL, archive, preparation, lock acquire/release, file mutations, and commit.

| Case | Required result |
| --- | --- |
| Live record + equivalent intent | Same logical result after current-authorization revalidation; no duplicates |
| Live record + different intent | `idempotency_conflict`, without prior-intent disclosure |
| Expired record + equivalent intent | `idempotency_key_expired`; refresh state; no execution |
| Expired record + different intent | `idempotency_key_expired`; same precedence; no execution |
| Read + key | Canonical read-key rejection before query execution |

The expired-key error is non-retryable with the old key and instructs `refresh_state_then_submit_with_new_key`. Architecture must separate replay-result retention from minimal consumed-key expiry evidence; TTL deletion cannot make an old key appear unused. OQ8 approves the metadata-only digest/tombstone persistence model and retention.

### 4.7 Dual Authorization for Incident Evidence — Approved

**Artifacts:** PRD FR56; Story 6.9; architecture F-6; UX-DR33; Story 11.10 preservation criteria; OQ9.

**OLD:**

> `eventstore:permission=admin` permits incident-stream access during projection degradation.

**NEW:**

> The same actor must hold incident-admin permission **and** fresh current tenant/folder authorization before stream lookup, event counting, checkpoint lookup, filtering, or shaping. Incident-admin grants no global browsing.

Allowed evidence is bounded, metadata-only, C9-redacted, read-only, and accompanied by a persistent degraded warning, checkpoint, time window, and correlation context. Missing-admin, wrong-tenant, revoked, stale, hidden-resource, and folder-denied cases fail before observation and emit one safe denial audit record. No raw event payload, content, credential, diff, provider payload, repair, or mutation control is exposed.

OQ9 closes only with positive and negative evidence plus Security/PM approval and evidence digest.

### 4.8 Authorized Discovery, State Dimensions, and Automatic Reconciliation — Automatically Approved

**Artifacts:** UX-DR2, UX-DR13, UX-DR15, core flows, implementation roadmap; architecture F-1/F-2/F-4; Epic 6.

**OLD:**

> Global search may search tenant, folder, workspace, repository binding, task, correlation, provider, state, and time. State lists mix lifecycle, lock, visibility, and freshness. `unknown_provider_outcome` is awaiting-human. Diagnostic Timeline and Trust Matrix appear in Phase 2 without clarifying release scope.

**NEW:**

> “Global” means global only inside the caller's already-established authorized tenant/folder scope. Authorization and safe scope establishment precede candidate lookup, result counting, suggestions, filtering, and empty-state classification.

UX displays separate dimensions:

- workspace lifecycle: the exact 11 PRD wire states;
- lock state: the exact five lock states;
- operator disposition: `available`, `auto-recovering`, `degraded-but-serving`, `awaiting-human`, `terminal-until-intervention`;
- folder lifecycle;
- projection freshness/availability;
- visibility/redaction.

For `unknown_provider_outcome`, display automatic reconciliation progress, last check, remaining check/time budget, next scheduled check, and safe reason. The system performs at most five read-only checks and finishes within 15 minutes. Retry/takeover controls remain absent. `awaiting-human` appears only after `reconciliation_required`.

UX Phase 1/2 labels are implementation sequencing inside the MVP. Diagnostic Timeline, Trust Matrix, Provider Readiness Evidence, and Access Evidence are not release deferrals.

Architecture F-1/F-2 becomes **Blazor Web App with Interactive Server rendering through `FrontComposerShell`**, replacing stale “Blazor Server” terminology.

### 4.9 Production Evidence Ownership and Forward-Dependency Removal — Automatically Approved

**Artifacts:** architecture limitation sections; Epics 4, 6, 10, and 11; sprint status; OQ5/OQ6.

**OLD:**

> Production transition evidence, seven console diagnostic views, and the deployed search bridge are deferred to technical Story 11.10. Safe-empty/unavailable behavior is described as a documented non-defect.

**NEW:**

Safe-empty/unavailable behavior is mandatory fail-safe behavior but is not completed product capability. Import the already-approved ownership from `sprint-status.yaml` into canonical `epics.md`:

- **4.18:** EventStore-backed workspace transition-evidence projection.
- **4.19:** durable workspace prepare/lock proof.
- **4.20:** durable file mutation and bounded-context proof.
- **4.21:** real commit/retry/conflict/unknown-outcome proof.
- **6.12:** readiness, lock, dirty-state, and failed-operation projections.
- **6.13:** provider-status, sync-status, and projection-freshness projections.
- **6.14:** populated deployed-host diagnostic and transition-evidence journeys.
- **10.7:** EventStore-backed search bridge and deployed Server registration.
- **10.8:** real produce/index/authorize/hydrate/redact/search round trip.
- **10.9:** authorized body-content materialization, C9 gated.

Story 11.10 retains EventStore admission/subscription seam adoption only. Story 11.14 owns Memories client/publication seam refactoring; Story 11.15 owns the DCP-capable verification lane. Workstream 11 cannot be a future dependency for completion of Epics 4, 6, or 10.

Every production read-model story requires durable population, deployed registration, deterministic empty-checkpoint replay, tenant isolation, freshness, safe unavailable behavior, and real-path evidence. Story 10.8 is the FR58 completion story. OQ5/OQ6 remain open until non-empty evidence exists.

### 4.10 Portfolio, Story Authority, and Metadata Consistency — Automatically Approved

**Artifacts:** `epics.md`; `planning-story-manifest.yaml`; sprint status; readiness workflow inputs.

Complete the previously approved 2026-07-14 structural correction:

1. Classify Workstream 1 as canonical-contract enablement; Workstreams 7–9 as governance/remediation/topology history; Workstream 11 as technical refactoring.
2. Product-completion metrics include Epics 2–6, 10, and 12 only.
3. Import Epics 12 and 13 and all already-approved split/closure stories from `sprint-status.yaml` into `epics.md`.
4. Merge live Story 2.8b into Story 2.8 and preserve `2.8b` only as a historical alias.
5. Create `planning-story-manifest.yaml` as the generated inventory of ID, title, classification, lifecycle status, authoritative source, and aliases.
6. Remove manually trusted fixed counts. Generate and validate counts from the manifest.
7. Preserve completed historical batches, including Stories 8.1 and 8.2, as immutable history excluded from active implementation-readiness sizing.

Story authority becomes deterministic:

- Before a dedicated story file exists, the complete authoritative definition is in `epics.md`.
- Once a dedicated implementation story file exists, it is authoritative and `epics.md` becomes a linked synopsis.
- Readiness loads every manifest-selected authoritative source.
- Validation fails on duplicate IDs, multiple authorities, missing sources, stale counts/statuses, missing aliases, forward dependencies, or production-capability completion supported only by fake/seed/unavailable evidence.

### 4.11 Architecture Structure and UX Component Projection — Automatically Approved

**Artifacts:** architecture directory tree, requirements-to-structure mapping, UX implementation mapping.

Add the missing concrete UI structure:

- authorized search/results and scope establishment;
- workspace detail and trust summary;
- tenant scope banner;
- metadata-only folder tree/table;
- diagnostic timeline;
- trust matrix;
- access evidence;
- provider readiness evidence;
- incident evidence authorization gate;
- corresponding query ports and production projections.

Every page/component maps to a product story and authoritative read model. The architecture tree cannot be satisfied while omitting normative UX-DR components. UI remains FrontComposer/Fluent UI, read-only, metadata-only, and content-browser-free.

### 4.12 Close OQ1–OQ10 as Explicit Release Gates — Automatically Approved

| Item | Required closure |
| --- | --- |
| OQ1 | Approve C7 lease renewal, authorization revalidation, and revocation-effect SLO; Architecture + Security approval. |
| OQ2 | Publish canonical file-policy vocabulary and exact behavior; PM + Architecture + Security approval. |
| OQ3 | Publish complete actor/access-state × protected-operation authorization matrix; Security + PM approval. |
| OQ4 | Publish GitHub/Forgejo compatibility catalog and reconciliation-check policy; Provider + Architecture + PM approval. |
| OQ5 | Produce non-empty authorized FR58 search/status evidence through Stories 10.7–10.8; PM + Security + Test approval. |
| OQ6 | Produce populated replayable console/transition evidence through Stories 4.18 and 6.12–6.14; PM + Operations/UX + Test approval. |
| OQ7 | Align canonical lock identity and alias collision evidence per Proposal 4.3; Architecture + Security approval. |
| OQ8 | Align all-mutations idempotency, read-key rejection, expired-key persistence, SDK, and C13; Architecture + Security + Test approval. |
| OQ9 | Prove dual incident authorization per Proposal 4.7; Security + PM approval. |
| OQ10 | Freeze and approve release-calibration populations, exclusions, environments, methods, owners, and acceptance rules; PM + Test/Quality approval. |

An item closes only when the canonical evidence exists, every accountable approver records identity/date, and governance stores approved status plus evidence version/digest. Passing tests or completed code alone is insufficient.

## 5. Requirements Coverage Correction Matrix

| Partial FR | Corrected by |
| --- | --- |
| FR4, FR15 | Proposal 4.1 — tenant-admin policy authority |
| FR13, FR14 | Proposal 4.2 — archive vertical slice and mixed retention |
| FR19 | Proposal 4.3 — duplicate/alias binding identity |
| FR25, FR28, FR29 | Proposal 4.3 — canonical lock identity, vocabulary, release table |
| FR30 | Proposal 4.4 — automatic cleanup eligibility/timing |
| FR32, FR35 | Proposal 4.5 — task change set and exact context contract |
| FR41, FR42, FR44 | Proposal 4.6 — full idempotency/error matrix |
| FR46 | Proposals 4.9 and 4.12 — production failure/index evidence |
| FR56 | Proposal 4.7 — dual incident authorization |

After editing, regenerate the FR coverage map from exact PRD clauses. Numeric FR references without clause-level acceptance evidence do not count as complete.

## 6. Recommended Implementation Sequence

1. Freeze the current baseline and create the planning story manifest.
2. Synchronize the approved 2026-07-14 portfolio/story changes into canonical artifacts.
3. Apply Proposals 4.1–4.12 to PRD, architecture, UX, epics, Contract Spine, C13, error catalog, and traceability.
4. Close decision gates OQ1–OQ4 before dependent implementation acceptance.
5. Resolve the existing CI/production-start blockers recorded by Epics 12/13.
6. Implement durable substrate and product-owned projections in dependency order.
7. Produce OQ5–OQ9 non-empty/live evidence.
8. Freeze OQ10 release-calibration rules and collect calibration evidence.
9. Run planning-consistency, contract, authorization, safety, replay, parity, UI, and governance gates.
10. Rerun implementation readiness into a new non-overwriting report.

### Dependency Spine

```text
OQ1–OQ4 decisions
        |
Epic 12 durable repository/content/task/Git substrate
        |
        +--> Epic 4 durable lifecycle and transition evidence
        +--> Epic 6 populated console diagnostics and incident proof
        +--> Epic 10 deployed search bridge and real authorized round trip
        |
OQ5–OQ9 evidence
        |
OQ10 calibrated release evidence
        |
Implementation Readiness rerun
```

## 7. Effort, Risk, and Timeline Impact

### Estimate

- **Planning/artifact synchronization:** approximately 3–5 focused working days, including manifest/gate work and cross-artifact review.
- **Decision closure OQ1–OQ4:** stakeholder-dependent; should be completed before dependent product stories are accepted.
- **Production implementation/evidence:** high effort and likely multiple sprints; a reasonable planning range is 2–4 sprints depending on team capacity and the existing Epic 12/13 blockers.
- **Readiness rerun:** one focused validation cycle after all artifacts and evidence are synchronized.

These are planning ranges, not delivery commitments. Release timing remains untrustworthy until the authoritative story manifest and dependency graph are generated.

### Risk

| Risk | Level | Mitigation |
| --- | --- | --- |
| Canonical artifacts remain stale while sprint status advances | High | Manifest-selected authority and planning-consistency gate |
| Reopened work is interpreted as invalidating completed work | Medium | Preserve historical completion; add narrowly scoped production-closure stories |
| New ACs duplicate implemented behavior | Medium | Evidence review before assigning status; do not inherit `done` automatically |
| Security decisions remain implicit | High | OQ1–OQ4/OQ7–OQ9 approval gates |
| Safe-empty behavior is counted as capability | High | Require populated production evidence for product completion |
| Cross-repository platform work blocks product epics | Medium | Keep Workstream 11 seam work independent; product epics own behavior |

## 8. Implementation Handoff

**Scope classification:** Major.

| Role | Responsibility |
| --- | --- |
| Product Manager | Own MVP truth, actor authority, FR58 scope, OQ10, and product-epic acceptance. |
| Solution Architect | Own architecture reconciliation, lock/idempotency/incident invariants, dependency direction, and production projection ownership. |
| Product Owner | Own manifest, canonical backlog, story splits, ordering, statuses, aliases, and synchronization. |
| Developer | Assess existing evidence, implement unresolved product stories, and preserve real production paths. |
| Test Architect | Own clause-level coverage, negative matrices, replay/restart/live-path evidence, and readiness-gate integrity. |
| Security/Authorization | Own OQ1/OQ3/OQ7/OQ8/OQ9 approval and denial/no-leak evidence. |
| Provider/Platform | Own OQ4, DCP-capable evidence, provider compatibility, and production topology verification. |

### Success Criteria

1. Every story has one authoritative source selected by the manifest.
2. Canonical artifacts and sprint status contain the same epic/story inventory and statuses.
3. The 16 partial FRs have clause-level acceptance evidence.
4. Product epics contain user outcomes; technical workstreams are excluded from product-completion metrics.
5. No product epic depends on a later technical epic for behavior it claims to deliver.
6. Incident evidence requires both authorities before observation.
7. Every mutation and read follows the current generated idempotency classification.
8. Diagnostic, transition, and search paths produce populated, replayable, tenant-isolated production evidence.
9. OQ1–OQ10 have approved, versioned, digest-bound evidence.
10. A new implementation-readiness report no longer reports stale counts, authority conflicts, forward dependencies, or seed-only completion claims.

## 9. Final Approval and Workflow Execution Log

The trigger, impact analysis, selected path, and Proposals 4.1–4.6 were approved individually. The user then instructed the workflow to always approve; Proposals 4.7–4.12 are therefore recorded as approved incremental edits.

The consolidated proposal was explicitly approved by Administrator on 2026-07-15.

- **Final decision:** Approved.
- **Approval time:** 2026-07-15T02:02:07+02:00.
- **Change scope:** Major.
- **Selected approach:** Direct adjustment with structural reconciliation, amending the approved 2026-07-14 correction.
- **Primary routing:** Product Manager and Solution Architect for the fundamental planning reconciliation.
- **Supporting routing:** Product Owner, Developer, and Test Architect for backlog authority, implementation feasibility, production evidence, and readiness-gate integrity.
- **Sprint-status reconciliation:** No lifecycle mutation is required at approval time. The reopened epics and focused closure stories from the amended 2026-07-14 correction are already registered in `sprint-status.yaml`; Story 2.8b remains historical until the manifest/canonical-backlog synchronization records it as a `superseded_alias`. Status changes based on the strengthened acceptance criteria require evidence review during implementation and must not be inferred by this proposal workflow.
- **Artifacts modified by this workflow:** This finalized Sprint Change Proposal only.
- **Artifacts awaiting implementation:** PRD, architecture, UX, canonical epics, planning story manifest, sprint-status reconciliation metadata, context, traceability, governance decisions, and planning-validation artifacts identified in Sections 4 and 8.
- **Code/infrastructure authorization:** No application code, infrastructure, deployment, package, submodule, or external-repository mutation was performed by this workflow.

The handoff is complete when the routed owners accept the responsibilities in Section 8 and execute the dependency order in Section 6. A new implementation-readiness assessment remains the governing completion gate.
