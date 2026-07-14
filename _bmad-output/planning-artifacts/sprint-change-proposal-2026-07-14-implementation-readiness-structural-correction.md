---
title: "Sprint Change Proposal — Implementation-Readiness Structural Correction"
project: "Hexalith.Folders"
date: "2026-07-14"
preparedFor: "Administrator"
workflow: "bmad-correct-course"
status: "approved"
approvedAt: "2026-07-14"
approvedBy: "Administrator"
changeScope: "major"
selectedPath: "hybrid-mvp-truth-reconciliation-and-direct-planning-adjustment"
trigger:
  artifact: "_bmad-output/planning-artifacts/implementation-readiness-report-2026-07-14.md"
  result: "NOT READY"
preservedArtifacts:
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-14.md"
  - "_bmad-output/planning-artifacts/implementation-readiness-report-2026-07-14.md"
---

# Sprint Change Proposal — Implementation-Readiness Structural Correction

## 1. Executive Summary

The 2026-07-14 implementation-readiness assessment found the planning set **NOT READY** despite complete mapping of all 58 functional requirements. The blocking problem is structural: several product capabilities are described as complete while their deployed production paths are unavailable, seed-only, fake-backed, or dependent on later stories.

This proposal preserves the existing product direction, contract foundation, completed control-plane work, and previously ratified durable-data-plane decision. It corrects the planning model so that:

- customer capabilities are separated from enabling, release, remediation, and refactoring workstreams;
- every story has one authoritative source;
- forward dependencies are removed from product completion claims;
- production projections belong to their consuming product capabilities;
- Epic 12 becomes the durable repository-backed product round trip;
- FR58 is completed only by a real authorized search round trip;
- lifecycle status and product-MVP claims reflect deployed evidence;
- planning consistency becomes mechanically validated.

The selected path is a **hybrid of MVP truth reconciliation and direct planning adjustment**. No rollback is proposed. The durable repository-backed product MVP remains the approved target.

## 2. Trigger and Core Problem

### Trigger

The triggering artifact is:

    _bmad-output/planning-artifacts/implementation-readiness-report-2026-07-14.md

It reported:

- 27 total issues;
- 7 critical structural violations;
- complete 58/58 FR-to-epic mapping but an independently unexecutable backlog;
- production-empty diagnostic and transition-evidence read models;
- an unavailable deployed FR58 bridge read model;
- forward dependencies across Stories 2.8/2.8b, Epic 4, Epic 6, Epic 9/10, and Story 11.10;
- technical milestones represented as product epics;
- ambiguous story and acceptance-criteria authority;
- stale or contradictory PRD, architecture, UX, and sprint-status conclusions.

### Core problem

The architecture and product intent remain directionally valid. The failure is in delivery structure and status truth:

1. Completed control-plane, contract, safety, and fallback behavior has been conflated with completed production capability.
2. Behavior stories rely on foundations, projections, or production wiring assigned to later stories.
3. Product epics and engineering workstreams are mixed in the same completion metric.
4. Story acceptance criteria are split between planning and implementation artifacts without a deterministic authority rule.
5. Planning documents do not agree on revisions, story counts, epic inventory, status, or production readiness.

## 3. Impact Assessment

### Epic impact

- Epic 1 is reclassified as contract/platform enablement while retaining Story 1.1 as the starter scaffold.
- Epic 2 absorbs Story 2.8b into Story 2.8.
- Epic 3 is split by GitHub and Forgejo capability group; repository provisioning gains a terminal asynchronous outcome.
- Epic 4 is reopened for production transition evidence and durable lifecycle proof.
- Epic 5 is split by CLI and MCP capability groups.
- Epic 6 is reopened for populated production diagnostic projections and deployed console evidence.
- Workstream 7 remains release governance, not product scope.
- Epic 8 remains historical release remediation, not product scope.
- Epic 9 becomes completed enabling topology within the FR58 initiative.
- Epic 10 is renamed around authorized content search and gains production bridge, body-materialization gate, and real round-trip stories.
- Epic 11 remains technical refactoring; Story 11.10 is split and loses product-projection ownership.
- Epic 12 is incorporated as the durable repository-backed product round trip.
- Epic 13 is incorporated as security and operational hardening outside product completion metrics.

### Artifact impact

- **PRD:** revision and readiness metadata, current delivery posture, C3/C4 decisions, architecture-decision posture, FR58 scope, and Epic 12 product-MVP truth.
- **Architecture:** readiness conclusion, critical gaps, FR58 mapping, projection ownership, production limitation language, canonical dispositions, and provenance.
- **UX:** implementation-evidence boundary, canonical dispositions, FR58 console boundary, production projection acceptance, and provenance.
- **Epics:** authority policy, portfolio classification, Epic 12/13 incorporation, story splits, corrected sequencing, complete acceptance criteria, and generated counts.
- **Sprint status:** reopened product epics, newly added stories, resolved status contradictions, and preserved historical workstream status.
- **Traceability/context:** FR57/FR58 ownership, NFR51/NFR52 evidence state, deferred-work ownership, Epic 11 context, and project planning rules.

### Code and infrastructure impact

This correct-course workflow changes planning and tracking artifacts only. It does not authorize or apply application-code, infrastructure, deployment, submodule, or external-repository mutations.

## 4. Recommended Path Forward

### Selected approach

**Hybrid — MVP truth reconciliation plus direct planning adjustment**

- Preserve the durable repository-backed lifecycle as the product MVP.
- Recognize completed contract, adapter, authorization, governance, accessibility, and fallback work as a completed control-plane increment.
- Do not represent that increment as product-MVP completion.
- Incorporate the already-ratified Epic 12 durable data plane and Epic 13 hardening work.
- Repair the current backlog rather than rolling back implementation or reducing the product to a control-plane-only MVP.

### Alternatives considered

| Option | Viability | Decision |
|---|---|---|
| Direct adjustment only | Viable; medium-high effort and medium risk | Used as part of the hybrid |
| Rollback | Not viable; would discard valid contract and control-plane work | Rejected |
| Reduce MVP to control plane | Technically possible but contradicts the ratified durable product purpose | Rejected |
| Durable product MVP plus planning repair | Best alignment with approved product intent | Selected |

### Scope and routing

- **Scope:** Major
- **Primary routing:** Product Manager and Solution Architect
- **Supporting roles:** Product Owner, Developer, and Test Architect
- **Reason:** Cross-artifact product-status correction, epic reclassification, new product stories, production ownership changes, and revised readiness gates.

## 5. Approved Detailed Changes

### 5.1 Canonical planning and story authority

Add the following policy to epics.md:

> ## Story and Acceptance-Criteria Authority
>
> Each story has exactly one authoritative definition:
>
> 1. A backlog story without a dedicated story file is fully authoritative in
>    epics.md, including its complete acceptance criteria.
> 2. Once a dedicated implementation story file is created, that file becomes the
>    authoritative story and acceptance-criteria source. The corresponding
>    epics.md entry becomes a non-authoritative synopsis and must link to it.
> 3. _bmad-output/planning-artifacts/planning-story-manifest.yaml inventories
>    every story ID, title, lifecycle status, owning product epic or enabling
>    workstream, authoritative source, and superseded aliases.
> 4. Implementation readiness must load every source selected by the manifest.
> 5. Validation fails on duplicate IDs, missing authoritative files, conflicting
>    statuses, stale counts, untracked story headings, or more than one
>    authoritative source for a story.
>
> Completed historical IDs remain stable. Story 2.8b is absorbed into Story
> 2.8 as production-path acceptance evidence and retained only as a
> superseded_alias for traceability.

Create:

    _bmad-output/planning-artifacts/planning-story-manifest.yaml

The manifest will identify:

- authoritative PRD, architecture, UX, epic, story, and sprint-status sources;
- the previously ratified 2026-07-14 production audit proposal;
- this structural-correction proposal;
- story lifecycle status and owning portfolio classification;
- source aliases and superseded IDs;
- generated story, epic, and product-epic counts.

### 5.2 PRD truth and MVP status

Update PRD frontmatter:

    status: complete
    completedAt: '2026-05-07'
    lastEdited: '2026-07-14'
    implementationReadiness: not-ready
    implementationReadinessAssessedAt: '2026-07-14'
    productMvpDecision: durable-repository-round-trip-required
    productMvpDecisionRatifiedAt: '2026-07-14'

Add:

> ### Current Delivery Posture
>
> The contract, adapter, authorization, governance, and diagnostic fallback
> foundations constitute a completed control-plane increment. They do not by
> themselves complete or release the product MVP.
>
> The product MVP remains the durable repository-backed lifecycle defined in this
> PRD. It is not complete until Epic 12 proves that an authorized agent can
> persist content, survive process restart, retrieve the authoritative content,
> complete a real Git commit, observe terminal task state, and rebuild production
> read models without substituting in-memory or unavailable test seams.
>
> Completed control-plane, release-gate, or accessibility work remains valid
> evidence but must not be represented as product-MVP acceptance.

Replace the C3/C4 rows:

| Criterion | Corrected decision |
|---|---|
| C3 | Approved: PM 2026-06-22; Legal 2026-06-24. Authoritative values and evidence: docs/exit-criteria/c3-retention.md. |
| C4 | Approved: PM 2026-06-22. Authoritative values and evidence: docs/exit-criteria/c4-input-limits.md. |

Replace stale “decisions needed next” language:

> ### Architecture Decision and Verification Posture
>
> The architecture has resolved the content transport, provider capability,
> workspace-state, idempotency, lock, query-bound, and sensitive-metadata
> decisions needed by the canonical contract. Remaining work concerns
> implementation conformance and deployed evidence, including durable
> persistence, real Git round-trip behavior, production projection population,
> DCP-capable topology evidence, and separately owned reference-pending NFR
> verification.

Correct FR58:

> Scope note: FR58 remains the product requirement for authorized search over
> Folders-indexed content. Metadata-token recall is an enabling, metadata-only
> increment and does not by itself complete body-content search. Body-text
> materialization requires explicit C9 Security and Product approval.
>
> FR58 is complete only when the capability can produce, index, remove/archive,
> authorize, hydrate from authoritative durable Folders state, redact, and return
> a real deployed result. Epic 10 owns the search capability; Epic 12 supplies the
> durable source events and authoritative content/state on which it depends.

The existing 58 functional requirements and durable product scope remain unchanged.

### 5.3 Architecture readiness and production ownership

Add current provenance and mark implementation readiness not-ready as of 2026-07-14.

Replace the final readiness conclusion:

> ### Architecture Readiness Assessment
>
> **Overall Status:** NOT READY — the architecture remains directionally valid,
> but the implementation plan does not yet provide independently completable
> production vertical slices.
>
> The blocking gaps are production persistence and Git evidence, unpopulated
> diagnostic and transition-evidence projections, an unavailable deployed search
> bridge, forward story dependencies, incomplete FR58 mapping, and inconsistent
> planning authority. The authoritative finding is recorded in
> implementation-readiness-report-2026-07-14.md.
>
> Safe-empty and fail-closed implementations preserve confidentiality and honest
> failure semantics. They are not evidence that the associated product capability
> is complete or release-ready.

Replace “Critical Gaps: None” with the readiness blockers and this approved correction path.

Production projection ownership becomes:

- Epic 4: workspace-transition evidence and lifecycle replay evidence.
- Epic 6: seven production-populated operations-console diagnostic projections.
- Epic 10: search bridge projection, Server registration, authorization, hydration, pruning, and live search/status proof.
- Epic 12: durable source events, file content/state, restart replay, task completion, and real Git persistence.
- Workstream 11: EventStore/Memories platform seam adoption and DCP-capable cross-repository verification only.

Story 11.10 is no longer a convergence dependency for Epics 4, 6, or 10.

Add FR58 to Requirements-to-Structure Mapping:

| FR block | Lives in |
|---|---|
| FR58 Authorized content search | src/Hexalith.Folders/Search/ for policy, facade, authorization, security trimming, and authoritative hydration; src/Hexalith.Folders.Workers/SearchIndexing/ for materialization, publishing, removal/archive, and reconciliation; deployed bridge projections under src/Hexalith.Folders/Projections/Search/; REST, SDK, CLI, and MCP surfaces plus real round-trip integration tests. |

Expand F-4 to five canonical operator dispositions:

1. available
2. auto-recovering
3. degraded-but-serving
4. awaiting-human
5. terminal-until-intervention

The healthy ready state maps to available.

Correct the NFR posture:

> NFR51 is structurally satisfied because console reads use read-model ports.
> Safe-empty behavior verifies confidentiality but does not satisfy production
> capability acceptance. NFR52 and release readiness remain open for these views
> until their production projections rebuild deterministically from durable
> events, are registered in deployed hosts, and return populated evidence.

### 5.4 UX evidence and FR58 boundary

Update provenance and mark implementation readiness not-ready as of 2026-07-14.

Add:

> ### Implementation Evidence Boundary
>
> Safe-empty, unavailable, denied, inaccessible, and redacted states are mandatory
> security and usability behavior. Demonstrating those states proves only that the
> console fails safely; it does not prove that the underlying diagnostic
> capability is complete.
>
> Production acceptance of a diagnostic or transition-evidence view requires its
> read model to be populated from durable events, registered in the deployed host,
> rebuildable from an empty checkpoint, and exercised through the rendered
> console. Seed data, fixtures, screenshots, and in-memory Save(...) calls prove
> rendering behavior only.

Adopt the five operator dispositions from the architecture. Keep the healthy available disposition distinct from the unavailable data-visibility state.

Add:

> ### FR58 Search UX Boundary
>
> “Global search” elsewhere in this specification means workspace and operational
> evidence discovery. It must not be treated as acceptance of FR58 authorized
> content search.
>
> FR58 does not introduce a file browser, content preview, snippet display, or raw
> path/source-URI exposure. If search results are surfaced in the operations
> console, they remain metadata-only and security-trimmed. A real authorized,
> durably hydrated result must still be proven through the product capability even
> when no new console screen is required.

Require production projection-backed records for the seven diagnostic views and workspace transition evidence. Safe-empty rendering proves only genuinely empty or unavailable conditions. No visual redesign or mutation surface is introduced.

### 5.5 Portfolio classification

| ID | Classification | Revised title |
|---|---|---|
| 1 | Enabling workstream | Canonical Contract and Adapter Foundation |
| 2 | Product epic | Tenant-Scoped Folder Access and Lifecycle |
| 3 | Product epic | Provider Readiness and Repository Binding |
| 4 | Product epic | Repository-Backed Workspace Task Lifecycle |
| 5 | Product epic | Cross-Surface Workflow Parity |
| 6 | Product epic | Read-Only Workspace Trust Console and Audit Review |
| 7 | Release-governance workstream | MVP Release Readiness and Operational Evidence |
| 8 | Release-remediation workstream | MVP Release Acceptance Closure |
| 9 | Completed FR58 enabling workstream | AppHost and Memories Search-Index Topology |
| 10 | Product epic | Authorized Folders Content Search and Index Lifecycle |
| 11 | Technical-refactoring workstream | Domain-Focus Platform Refactoring and Governance Closure |
| 12 | Product epic | Durable Repository-Backed Round Trip |
| 13 | Security/operations workstream | Security and Operational Hardening |

Stable epic-* IDs remain unchanged. Product-completion metrics include only Epics 2–6, 10, and 12.

Epic 9 remains completed historical topology evidence and gains no new feature scope.

Epic 10 becomes:

> ## Product Epic 10: Authorized Folders Content Search and Index Lifecycle
>
> Developers and AI agents can publish, remove, reconcile, authorize, search, and
> hydrate Folders-indexed information through Memories while Folders remains the
> authoritative source and prevents cross-tenant or sensitive-data disclosure.
>
> **FRs covered:** FR58.
>
> Metadata-derived indexing is an enabling increment. Epic completion requires a
> deployed, production-populated bridge read model and a real authorized search
> round trip. Body-text recall remains C9-gated.

Add:

> ## Product Epic 12: Durable Repository-Backed Round Trip
>
> Authorized developers and AI agents can persist folder lifecycle and file
> content across process restart, retrieve authoritative content, complete a real
> Git commit, observe terminal task and projection state, and recover asynchronous
> indexing delivery without no-op, unavailable, or fake-backed substitutions.

Epic 12 stories:

1. 12.1 EventStore-backed folder repository and projection replay.
2. 12.2 Durable projections and task-completion pipeline.
3. 12.3 Durable workspace file-content store and content-read source.
4. 12.4 Real Git commit executor and provider write path.
5. 12.5 At-least-once Memories egress and reconciler.

Add Epic 13 and Stories 13.1–13.6 exactly as already ratified in sprint-status.yaml. It remains outside product completion metrics and must not duplicate Epic 11.

Remove fixed epic/story counts and generate them from the planning manifest.

### 5.6 Forward-dependency repair and production closure

#### Delivery sequence

    12.1 durable EventStore repository
      ├─> 12.2 durable task/projection substrate
      └─> 12.3 durable file-content source
            └─> 12.4 real Git write path
                  ├─> Epic 4 production closure
                  ├─> Epic 6 populated diagnostics
                  └─> Epic 10 real search closure

Story 12.2 excludes transition, diagnostic, and search-bridge projections owned by the consuming product epics.

#### Epic 2

- Story 2.8 absorbs the production /process wiring and evidence from 2.8b.
- Story 2.8b remains only as a superseded alias.

#### Epic 4

Logical foundation order:

    4.1  state machine
    4.11 idempotency, correlation, and task identity
    4.13 canonical error and failure projection
    4.14 metadata-only audit and observability
    4.2–4.10 behavior slices
    4.12 commit and reconciliation
    4.15–4.17 verification

Add:

- 4.18 EventStore-backed workspace transition-evidence projection.
- 4.19 Prove durable workspace prepare and lock lifecycle.
- 4.20 Prove durable file mutation and bounded-context lifecycle.
- 4.21 Prove real commit, retry, conflict, and unknown-outcome reconciliation.

Each production mutation story proves the real REST → gateway → processor → authorization gate → EventStore/projection path without a mocked gateway or no-op repository.

#### Epic 6

Add:

- 6.12 Populate readiness, lock, dirty-state, and failed-operation projections.
- 6.13 Populate provider-status, sync-status, and projection-freshness projections.
- 6.14 Prove populated deployed-host diagnostic and transition-evidence journeys.

Stories 6.12 and 6.13 own replay, Server registration, tenant isolation, freshness, unavailable behavior, and empty-checkpoint rebuild. Story 6.14 exercises the existing UI against real records.

#### Epic 10

Stories 10.1–10.5 remain completed component increments, not FR58 completion evidence.

Corrected sequence:

    10.6 Metadata-derived search-document materializer
    10.7 EventStore-backed search bridge and deployed Server registration
    12.5 At-least-once indexing egress and reconciliation substrate
    10.9 Authorized body-content materialization — C9 gated
    10.8 Real produce/index/authorize/hydrate/redact/search round trip

Story 10.8 is the FR58 completion story. Until Story 10.9 is authorized—or the PRD is explicitly rescoped—body-content search and FR58 remain incomplete.

#### Workstream 11

Replace the former Story 11.10 catch-all with:

- 11.10 Adopt EventStore admission and subscription-mapping seams.
- 11.14 Adopt Memories publication and search-client seams.
- 11.15 Maintain the DCP-capable cross-repository verification lane.

Remove product-projection ownership from Workstream 11.

#### Mandatory story-level acceptance

Affected mutation stories require explicit scenarios for:

- authorized production-path success;
- authorization or policy denial;
- equivalent idempotent replay;
- conflicting replay;
- known provider failure;
- unknown outcome and reconciliation;
- restart-surviving state;
- terminal status and retry eligibility;
- metadata-only audit evidence;
- sensitive-data exclusion.

Read-model stories require production registration, durable population, deterministic empty-checkpoint rebuild, tenant isolation, freshness behavior, and honest unavailable-state handling.

### 5.7 Oversized story splits

No split child inherits done automatically. The manifest may mark it done only when linked evidence satisfies its narrowed acceptance criteria.

#### Epic 3

- 3.3 GitHub capability discovery and safe readiness.
- 3.10 GitHub repository provisioning, binding, and branch/ref behavior.
- 3.11 GitHub file mutation, commit, status, and failure behavior.
- 3.4 Forgejo capability discovery, safe readiness, and contract-drift detection.
- 3.12 Forgejo repository provisioning, binding, and branch/ref behavior.
- 3.13 Forgejo file mutation, commit, status, and failure behavior.
- Rename 3.6 to “Request asynchronous creation of a repository-backed folder.”
- Add 3.14 “Complete asynchronous repository creation and binding.”

#### Epic 5

- 5.2 CLI tenant, folder, provider-readiness, and binding commands.
- 5.8 CLI workspace preparation and lock lifecycle.
- 5.9 CLI file, context, commit, status, error, and audit behavior.
- 5.3 MCP tenant, folder, provider-readiness, and binding tools/resources.
- 5.10 MCP workspace preparation and lock lifecycle.
- 5.11 MCP file, context, commit, status, error, and audit behavior.

Stories 5.4–5.7 remain cross-slice regression evidence and cannot complete missing behavior.

#### Workstream 11

Narrow Story 11.2 to:

    11.2 Inventory, assign, and pin platform prerequisites

It owns the availability manifest, repository ownership, upstream issue references, and release/SHA evidence. It does not claim to implement upstream repository code.

Additional splits:

- 11.3 Apply wire-preserving repository hygiene.
- 11.16 Replace brittle governance and CI pins with behavioral gates.
- 11.7 Consolidate deterministic time, context, and path test helpers.
- 11.17 Consolidate EventStore gateway doubles and rejection conformance.
- 11.18 Consolidate provider and repository fakes in Folders.Testing.
- 11.11 Adopt FrontComposer user-context, token, OIDC, and shared-shell helpers.
- 11.19 Adopt Fluent UI tables, controls, icons, loading, and layout primitives.
- 11.13 Delete obsolete local code and synchronize planning/maintenance documents.
- 11.20 Record AppHost, ServiceDefaults, query-handler, and tenant-semantic ADRs.
- 11.21 Run final boundary, package, test, E2E, accessibility, and governance verification.

Completed Stories 8.1 and 8.2 remain immutable historical remediation batches tagged historical_batch: true and excluded from the active implementation-ready backlog.

### 5.8 Status, traceability, and validation

Apply epic status:

| Epic/workstream | Status after correction |
|---|---|
| 1 | done — enabling history |
| 2 | done |
| 3 | in-progress — reopened |
| 4 | in-progress — reopened |
| 5 | in-progress — reopened |
| 6 | in-progress — reopened |
| 7 | done — release-governance history |
| 8 | done — release-remediation history |
| 9 | done — completed FR58 topology enablement |
| 10 | in-progress |
| 11 | in-progress |
| 12 | backlog |
| 13 | backlog |

Add Stories 3.10–3.14, 4.18–4.21, 5.8–5.11, 6.12–6.14, 10.7–10.9, and 11.14–11.21 as backlog unless existing evidence justifies review.

- Story 10.6 remains in-progress until its implementation story completes review. Its materializer action item remains done.
- Story 11.2 remains review.

Correct traceability:

- FR57: Epic 3 produces provider evidence; Epic 6 presents it.
- FR58: Epic 10 owns the user capability; Epic 12 supplies durable authoritative state and egress; Workstream 9 supplies completed topology enablement.
- NFR51/NFR52: distinguish architectural/read-model structure from populated production and replay evidence.

Synchronize:

- _bmad-output/implementation-artifacts/deferred-work.md
- _bmad-output/implementation-artifacts/epic-11-context.md
- _bmad-output/project-context.md
- NFR and FR traceability artifacts

Remove Story 11.10 as the recorded owner of Epic 4, 6, or 10 product projections.

Add a planning-consistency gate that fails on:

- duplicate or untracked story IDs;
- multiple authoritative sources;
- missing authoritative files;
- invalid or conflicting lifecycle status;
- stale counts;
- missing aliases;
- story headings absent from the manifest;
- forward dependencies on later delivery;
- technical milestones classified as product epics;
- a completed production capability backed only by no-op, unavailable, seed-only, or fake-only evidence.

## 6. Artifact Edit Inventory

### Create

- _bmad-output/planning-artifacts/planning-story-manifest.yaml
- _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-14-implementation-readiness-structural-correction.md
- A new non-overwriting implementation-readiness report after all approved edits are applied.

### Modify after final approval

- _bmad-output/planning-artifacts/prd.md
- _bmad-output/planning-artifacts/architecture.md
- _bmad-output/planning-artifacts/ux-design-specification.md
- _bmad-output/planning-artifacts/epics.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- _bmad-output/implementation-artifacts/deferred-work.md
- _bmad-output/implementation-artifacts/epic-11-context.md
- _bmad-output/project-context.md
- Relevant FR/NFR traceability and planning-validation artifacts

### Preserve unchanged

- _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-14.md
- _bmad-output/planning-artifacts/implementation-readiness-report-2026-07-14.md
- Completed historical story IDs and their implementation evidence

## 7. Implementation Sequence

1. Ratify this proposal.
2. Freeze the current planning baseline and create the story manifest.
3. Update PRD, architecture, and UX truth/provenance.
4. Reclassify the portfolio and incorporate Epics 12 and 13 in epics.md.
5. Apply story sequencing, ownership, and split definitions with complete authoritative acceptance criteria.
6. Reconcile sprint status and historical aliases.
7. Synchronize deferred work, Epic 11 context, project context, and traceability.
8. Add and run planning-consistency validation.
9. Run structural checks for duplicate IDs, missing sources, status conflict, forward dependency, and stale count.
10. Rerun implementation readiness into a new report without overwriting the 2026-07-14 trigger report.
11. Do not begin a gated story until its prerequisites point only to completed earlier work and its authoritative source is selected by the manifest.

## 8. Risks and Mitigations

| Risk | Mitigation |
|---|---|
| Reopening completed epics is interpreted as invalidating prior work | Preserve completed story history and label the missing production closure precisely |
| New split stories duplicate already-implemented behavior | Require evidence review before assigning status; do not inherit done automatically |
| Manifest and sprint status drift | Add deterministic cross-artifact validation |
| FR58 remains blocked by C9 | Keep Story 10.9 explicitly gated; do not claim FR58 complete |
| Epic 12 overlaps product projection stories | Limit 12.2 to the durable core substrate; keep consumer projections in Epics 4, 6, and 10 |
| Workstream 11 collides with product delivery | Remove product-projection ownership and split platform seams from capability work |
| Current code cannot satisfy new AC immediately | Treat the corrected backlog as the implementation contract; do not falsify status |

## 9. Success Criteria

The correction is successful when:

1. Every story has one authoritative source selected by the manifest.
2. Counts and lifecycle statuses are generated or validated, not manually trusted.
3. Product epics contain user-valued capabilities; technical/release work is classified separately.
4. No product story depends on a later story for behavior it claims to deliver.
5. Epics 4, 6, 10, and 12 cannot reach done without production-path evidence.
6. FR58 cannot reach done without a real authorized, durable, hydrated, redacted result.
7. PRD, architecture, UX, epics, sprint status, traceability, and context agree on MVP posture and ownership.
8. Implementation readiness is rerun and its new result becomes the governing readiness decision.

## 10. Handoff

Because this is a Major change:

- Product Manager owns product-MVP truth, FR58/C9 scope, and product-epic acceptance.
- Solution Architect owns production boundaries, dependency direction, projection ownership, and architectural consistency.
- Product Owner owns backlog ordering, story-source authority, and lifecycle-status reconciliation.
- Developer owns implementation feasibility and future execution against authoritative story sources.
- Test Architect owns production-evidence expectations, replay/restart/live-path verification, and readiness-gate integrity.

## 11. Approval Record

The trigger/context analysis, epic impact, artifact impact, selected path, and all eight incremental edit proposals were approved interactively on 2026-07-14.

The consolidated proposal was explicitly approved by Administrator on 2026-07-14.

**Major-scope handoff recorded:** Product Manager and Solution Architect own the fundamental replan; Product Owner and Developer support backlog realization; Test Architect owns production-evidence and readiness-gate integrity. Sprint status is updated by this workflow to register the approved epic and story changes. The routed roles own the remaining source-artifact edits listed in Section 6.
