---
project: Folders
date: 2026-09-15
workflow: bmad-correct-course
mode: batch
status: approved
scope: major
approval_required: false
approved_at: 2026-09-15T16:08:21+02:00
approved_by: Jerome
approval_response: continue
approval_scope: sponsor-approval-of-recommendations-A1-through-A8
required_role_signoffs: pending
handoff_status: recorded
source_artifacts_modified: false
execution_freeze: retained-pending-role-signoff-and-validation
trigger: implementation-readiness-report-2026-08-04
---

# Sprint Change Proposal — Planning Authority Reconciliation

## 1. Issue Summary

The 2026-08-04 implementation-readiness assessment failed because planning authority was not an
implementable, internally consistent system. The approved August 4 recovery proposal subsequently added the
missing structural controls, including the planning-story manifest and an execution freeze. Most of that
structural recovery is now present in the PRD, architecture, UX specification, epics, and implementation
artifacts. The manifest, however, still records its August 4 lifecycle snapshot while approved decisions and
implementation evidence continued through September.

The current conflict is therefore not a lack of planning artifacts. It is a split in authority:

- `planning-story-manifest.yaml` still describes several stories and open questions as they stood on August 4;
- `sprint-status.yaml`, dedicated story files, frozen build specifications, approval artifacts, and commits
  contain later and sometimes conflicting states;
- PRD decisions PD1, PD3, PD4, PD5, PD8, PD10, and PD11 remain explicitly unresolved;
- Epics 4, 6, and 10 need foundations owned by Epic 12, so numeric epic order cannot be used as execution order;
- the C3 retention and C6 lifecycle authorities conflict with each other and with the current transition code;
- the canonical authorization matrix identifies wire-contract defects that invalidate the current OQ3
  approval digest if corrected.

The execution freeze remains necessary. This proposal does not remove it and does not change any story
lifecycle status. It defines the exact edits, approvals, and deterministic checks required before the freeze
may be removed.

## 2. Evidence and Authority Used

This proposal reconciles, without replacing, the following evidence:

- current product authority: `prd.md`;
- current technical authority: `architecture.md`;
- current interaction authority: `ux-design-specification.md`;
- current portfolio and acceptance authority: `epics.md`;
- planning control: `planning-story-manifest.yaml`, generated 2026-08-04;
- lifecycle tracker: `sprint-status.yaml`;
- dedicated story files and frozen build specifications for Stories 3.10, 3.12, 10.7–10.9, and 11.2–11.4;
- the approved 2026-08-04 planning reconciliation proposal;
- the implemented 2026-08-24 NFR traceability proposal and its 73/73 authority result;
- OQ1 through OQ4 approval artifacts completed between 2026-09-12 and 2026-09-15;
- the canonical authorization matrix, C3 retention authority, C6 transition mapping, current transition code,
  and their associated evidence gates.

Evidence snapshots remain immutable. A `status` in a frozen build specification is treated as that build
run's workflow result, not as the current story lifecycle value. The regenerated manifest will name one
canonical `story_lifecycle_status` and link all other artifacts as dated evidence.

## 3. Impact Analysis

### Product impact

- Epic 12 becomes an explicit MVP foundation rather than an implicitly later epic.
- Epic 13 becomes an explicit release-hardening epic rather than architecture-only commentary.
- Indexed body-content materialization remains outside the approved MVP unless a later product and security
  decision admits it. Story 10.9 becomes the completed safety guard proving metadata-only behavior.
- Tenant-confidential overrides are written as correlation tokens. Cleartext confidential values are never
  made durable and therefore cannot depend on read-time redaction for safety.
- The PRD completion model governs dirty, staged, inaccessible, retryable-failure, and unknown-outcome states.

### Architecture and contract impact

- C6, C3, the transition implementation, and lifecycle tests must be brought to one model.
- The OpenAPI contract spine and generated surfaces require a breaking security correction before release.
- OQ3 must be reapproved against the new authorization-matrix digest after those corrections.
- C3 requires renewed Legal approval because the cleanup trigger changes from elapsed lock time to terminal
  task closure with no active task.

### Delivery impact

- No completed implementation evidence is discarded.
- Stories 3.10, 3.12, 10.7, 11.2, and 11.3 remain done.
- Story 10.8 is corrected from an overstated tracker value to in-progress, with its completed component
  increments retained.
- Story 10.9 can become done only under the approved, narrowed safety-guard definition in this proposal.
- Story 11.4 remains in review until an explicit review acceptance closes it.
- Epic identifiers remain stable. A new execution-wave rank, rather than epic number, controls sequencing.

### Risk and schedule impact

Classification: **Major**. The proposal changes product scope records, lifecycle semantics, security contract
behavior, durable-state rules, and the implementation sequence. It avoids rollback, but it adds an authority
relock before unrestricted execution can resume.

## 4. Recommended Approach

Use a direct planning correction with an authority relock:

1. approve the product, architecture, security, and Legal decisions in Section 8;
2. apply the source edits in Sections 5–7 without changing immutable evidence snapshots;
3. regenerate the manifest from current authority and evidence;
4. apply the explicitly approved lifecycle reconciliations;
5. validate inventories, statuses, decision digests, and the execution-wave DAG;
6. remove the general execution freeze only when every release condition in Section 9 passes; and
7. rerun `bmad-sprint-planning` only when the criteria in Section 10 are satisfied.

Rollback is not recommended because it would erase valid completed increments. Reducing the MVP is not
recommended beyond retaining body-content indexing as a deliberate non-goal; the durable data plane and the
authorization correction are necessary for the existing MVP to be real and releasable.

## 5. Exact Artifact Edits

### 5.1 `_bmad-output/planning-artifacts/prd.md`

Apply all of the following in one dated revision:

1. In **Current Delivery Posture**, add Epics 12 and 13 to the release inventory. State that epic numbering is
   identity, not execution order, and reference the manifest execution waves.
2. Replace PD1 with a resolved decision that admits Epic 12 and adds OQ11:
   **“Can the MVP demonstrate one restart-safe, multi-replica, end-to-end mutation and query vertical slice
   using the durable data plane and real provider path?”** Owner: Persistence plus Git/Delivery. Approvers:
   Product, Architecture, Security, and Test. OQ5, OQ6, and OQ7 depend on OQ11.
3. Replace PD3 with a resolved decision that admits Epic 13 and adds:
   - OQ12: **“Can one supported deployment profile pass the release security-hardening evidence set?”**
     Owner: Security plus Platform. Approvers: Product, Architecture, Security, Operations, and Test.
   - OQ13: **“Can the supported deployment profile meet the approved performance and capacity envelope?”**
     Owner: Platform plus Performance. Approvers: Product, Architecture, Operations, and Test.
4. Append, without renumbering NFR1–NFR73, these eleven mechanism-neutral requirements as NFR74–NFR84:
   - NFR74: bearer credentials are accepted only over HTTPS or an explicitly approved loopback development
     boundary;
   - NFR75: provider endpoints deny private, loopback, link-local, metadata-service, and otherwise prohibited
     destinations unless an approved deployment policy explicitly allows them;
   - NFR76: protected endpoints and internal service boundaries deny by default when authority is absent,
     stale, malformed, or unavailable;
   - NFR77: local CLI and MCP credential material uses owner-only storage and is never emitted to logs,
     telemetry, diagnostics, or generated artifacts;
   - NFR78: repository and workspace content is untrusted input and must not control commands, paths,
     templates, or rendered active content without validation or neutralization;
   - NFR79: accepted mutations, their state transitions, and required evidence survive process restart;
   - NFR80: supported multi-replica deployments converge on one authoritative state without seed-local or
     replica-local correctness assumptions;
   - NFR81: readiness reports actual dependency health and cannot report ready from configuration or seed
     data alone;
   - NFR82: every release-significant metric and alert has demonstrated emission, an owner, and fault-path
     evidence;
   - NFR83: release evidence is classified as automated, operational, approval-bound, or reference-pending,
     with an owner for every non-automated item; and
   - NFR84: release verification covers edge-security behavior including safe denial, endpoint validation,
     credential handling, and untrusted-content boundaries.
5. Replace PD4 with the approved lifecycle table in Section 6 and record that status changes are evidence-led,
   explicit, and dated.
6. Replace PD5 with the narrowed Story 10.9 decision: metadata-only search is the MVP behavior; the story's
   completion bar is proof that unauthorized or unapproved body content cannot be indexed, hydrated, or
   returned. A future body-content capability needs a new stable requirement and story after C9 approval.
7. Replace PD8 with the event-write decision: confidential overrides are replaced by stable correlation
   tokens before persistence. Remove wording that implies durable cleartext may be made safe later by
   projection-time redaction. Rendering must still distinguish withheld, redacted, unavailable, and absent.
8. Replace PD10 with the authorization-spine correction in Section 7.2 and state that release generation and
   parity gates consume only the corrected, reapproved matrix.
9. Replace PD11 with the lifecycle rules in Section 7.3. Mark operator discard, operator retry-success, and
   operator mark-failed transitions as reserved post-MVP operations that fail closed in the MVP.
10. Update the NFR traceability declaration from 73 to 84 and record the linked PD6 relock. Do not mark any new
    NFR implemented merely because it has been admitted.
11. In the decision log, retain the old PD text as superseded history and add approver, date, proposal path,
    and resulting authority digest fields. Do not delete the historical questions.

### 5.2 `_bmad-output/planning-artifacts/architecture.md`

Apply these changes:

- Add a **Release Authority Overlay — 2026-09-15** identifying the PRD as product authority, architecture as
  mechanism authority, epics as acceptance authority, and the regenerated manifest as lifecycle and
  dependency control.
- Replace numeric-epic sequencing with the execution-wave model in Section 7.1.
- Amend S-6/C9 so confidential overrides become correlation tokens at event-write time; prohibit durable
  cleartext and specify token auditability without reversible payload recovery.
- Replace the C6 transition table with the exact rules in Section 7.3 and identify C3 as the cleanup authority.
- Add the authorization-spine requirements in Section 7.2, including evaluation order, safe-denial envelopes,
  structured scope metadata, and generated-surface parity.
- Admit Epic 13 as the owner of the NFR74–NFR84 release-hardening evidence; do not claim that admission is
  implementation evidence.

### 5.3 `_bmad-output/planning-artifacts/ux-design-specification.md`

Apply only behavior-visible corrections:

- describe a confidential override as a stored correlation reference, never recoverable cleartext;
- preserve visibly distinct states for withheld, redacted, unavailable, and absent values;
- show `unknown_provider_outcome` as automatically recovering while bounded confirmation continues, with
  escalation to `reconciliation_required` only after that process cannot establish the outcome;
- make authority-unavailable and resource-unavailable errors non-disclosing while retaining an operator-safe
  correlation reference; and
- update provenance to the approved proposal and resulting PRD/architecture digests.

### 5.4 `_bmad-output/planning-artifacts/epics.md`

Apply these changes in lockstep with the PRD and architecture:

- mirror NFR74–NFR84 exactly and update the traceability count to 84;
- add execution-wave rank and prerequisite fields to affected Stories 4.18–4.21, 6.12–6.14, 10.8, and
  12.1–12.6, using Section 7.1;
- keep Story 10.7's approved component done-bar: EventStore-backed bridge relocation, deployed-server
  registration, and hermetic restart verification; explicitly state that it does not close FR58 or OQ5;
- retain Story 10.8 as the real produce-index-authorize-hydrate-redact-search round trip and incorporate the
  corrected authorization-spine prerequisites;
- retitle Story 10.9 to **“Preserve Metadata-Only Indexing Until C9 Body-Content Approval”** and replace its
  completion language with the safety-guard scope described in PD5;
- update lifecycle-related acceptance criteria to the Section 7.3 model;
- add Epic 13/OQ12/OQ13 evidence ownership without marking those outcomes complete; and
- retain stable epic and story IDs. Do not renumber completed history to create apparent chronological order.

### 5.5 `_bmad-output/planning-artifacts/planning-story-manifest.yaml`

Regenerate the file; do not hand-edit only the disputed rows. The regenerated manifest must:

- use `generated_on: 2026-09-15` and a new manifest revision;
- add this proposal, the implemented August 24 proposal, and the OQ1–OQ4 approval artifacts to provenance;
- preserve the August 4 manifest as a superseded snapshot, not current authority;
- define `story_lifecycle_status` as the sole current lifecycle field;
- classify frozen specification `status` fields as `workflow_snapshot_status` and link them as evidence;
- implement the Section 6 lifecycle table;
- represent open-question state with separate `decision_status` and `delivery_evidence_status` fields;
- record OQ1–OQ4 as decision-approved with their approved digests, while leaving any distinct runtime evidence
  gates open;
- add OQ11–OQ13 and their owners, approvers, prerequisites, and evidence paths;
- add `execution_waves`, `execution_rank`, and explicit prerequisite edges from Section 7.1;
- fail validation on a cycle or on a dependency from a story to an equal or later execution rank;
- update the NFR inventory to 84 if PD3/linked PD6 is approved;
- replace every stale lifecycle conflict with a dated reconciliation record containing old value, new value,
  evidence, approvers, and proposal path; and
- retain `general_execution_hold: true` until every condition in Section 9 passes. The final validated edit may
  change it to `false` and record the release timestamp and validation evidence.

### 5.6 `_bmad-output/implementation-artifacts/sprint-status.yaml`

After approval, apply only these lifecycle changes:

- Story 10.8: `done` to `in-progress`;
- Story 10.9: `review` to `done`, only after the Story 10.9 title/scope correction is applied and its existing
  safety-guard evidence is explicitly accepted;
- all other rows in Section 6 retain their current sprint value.

Also update `last_updated`, add a reconciliation journal entry for every evaluated row, and keep all epic
statuses unchanged. Close the August 4 planning-recovery action and remove the execution-control freeze only
after Section 9 validates. Until then, leave both open.

### 5.7 Story files, specifications, and implementation evidence

- Do not rewrite frozen specifications or completed evidence to make their historical `status` fields match
  current lifecycle state.
- Keep `_bmad-output/implementation-artifacts/10-8-real-produce-index-authorize-hydrate-redact-search-round-trip.md`
  at `in-progress` and preserve its completed facade, CLI, and parity increments.
- Preserve the frozen Story 10.7 done-bar and all Story 3.10, 3.12, 11.2, 11.3, and 11.4 evidence.
- Add a reconciliation note or manifest link rather than changing historical task checkboxes.
- Refresh implementation contexts for the next executable stories only after the planning sources and
  manifest have new approved digests.

### 5.8 Contract, governance, and validation artifacts

Update these owned authorities in the same correction set:

- `docs/contract/authorization-matrix.md` and the OpenAPI contract spine;
- generated client, CLI/MCP parity, previous-spine snapshot, C13 inventory, and contract evidence;
- `docs/exit-criteria/c6-transition-matrix-mapping.md`, transition code, and lifecycle tests;
- `docs/exit-criteria/c3-retention.md`, retention/deletion governance hash, runbooks, and evidence gate;
- `docs/exit-criteria/nfr-traceability.md`, conformance tests, focused gate, and report for NFR1–NFR84;
- C9/security governance and evidence for event-write correlation tokens; and
- `.memlog.md`, appending approvals, supersession links, lifecycle reconciliations, and freeze-release evidence.

## 6. Lifecycle Status Reconciliation

No row changes until this proposal is approved. On approval, the canonical outcomes are:

| Story | August 4 manifest | Current sprint tracker | Canonical result | Evidence treatment |
| --- | --- | --- | --- | --- |
| 3.10 | `in-progress` | `done` | `done` | Preserve the dedicated story's done state and reviewed provisioning/binding evidence. Its component completion does not close the remaining live-provider work in Epic 3. |
| 3.12 | `backlog` | `done` | `done` | Preserve the implemented-and-reviewed commit evidence. Treat the frozen specification's older workflow state and unchecked task snapshot as historical, not current lifecycle authority. |
| 10.7 | `backlog` | `done` | `done` | Preserve the human-approved component done-bar. It proves bridge relocation, registration, and restart behavior, not FR58/OQ5. |
| 10.8 | `ready-for-dev` | `done` | `in-progress` | Correct the tracker overstatement. Preserve completed facade, CLI, and parity increments; durable produce/hydrate/DCP and OQ5 evidence remain open. |
| 10.9 | `backlog` | `review` | `done` after approval and scope edit | Accept the completed negative-guard evidence only for the renamed metadata-only safety story. Do not represent body-content materialization as done or approved. |
| 11.2 | `review` | `done` | `done` | Preserve 29/29 completed tasks and the accepted platform-prerequisite evidence. |
| 11.3 | `backlog` | `done` | `done` | Preserve 18/18 completed tasks and wire-preserving hygiene evidence. |
| 11.4 | `backlog` | `review` | `review` | Preserve completed implementation tasks but require an explicit review acceptance before a later move to done. |

The Story 10.8 correction is an explicit, approval-bound status reconciliation, not a silent reopening. Story
10.9 closes a narrowed safety obligation, not the previously contemplated body-content capability. Story 11.4
does not close merely because its implementation task list is complete.

## 7. Dependency and Architecture Corrections

### 7.1 Implementable execution waves

Epic IDs remain stable historical identities. `execution_rank` becomes the scheduling authority:

| Rank | Wave | Work and entry conditions |
| --- | --- | --- |
| 0 | Authority relock | Approve this proposal; record OQ1–OQ4 decisions; apply PD8, PD10, and PD11; reapprove changed C3, C6, C9, and OQ3 digests. |
| 10 | Durable core | Story 12.1 first. Stories 12.2 and 12.3 follow 12.1. Story 12.6 may resume after 12.1 but cannot close before OQ8 evidence. |
| 20 | External effects | Story 12.4 follows 12.1–12.3 plus completed Stories 3.11 and 3.13. Story 12.5 follows 12.1–12.3 plus completed Story 10.6. |
| 30 | Product closure | Story 4.18 follows 12.1–12.2 and PD11. Story 4.19 follows 12.1–12.3 and the C6/OQ7 authority. Story 4.20 follows 12.1–12.3, OQ2/OQ3, PD8, and PD10. Story 4.21 follows 12.4 plus 4.19–4.20 and PD11. Stories 6.12–6.13 follow 12.1–12.2 and their owning event flows. Story 6.14 follows 6.12–6.13 plus 4.18–4.21 and OQ9 prerequisites. Story 10.8 follows 12.1–12.3, 12.5, completed 10.7, the Story 11.15 DCP lane, and the corrected authorization projection. |
| 40 | Evidence closure | OQ5 follows the Story 10.8 live round trip. OQ6 follows Stories 4.18 and 6.12–6.14. OQ7 follows Story 4.19/C6. OQ8 follows Story 12.6. OQ9 follows Story 6.14. OQ11 follows the durable 12.x/4.x/provider vertical slice. OQ12 and OQ13 follow Epic 13 security and capacity evidence. |
| 50 | Release calibration | OQ10 is last. Run its calibration plan only after the preceding evidence gates have named outcomes, then rerun implementation readiness. |

Story 10.9 is not a forward capability dependency under the proposed safety-only scope. A future approved
body-content feature must receive a new requirement, story, rank, and dependency path.

Manifest validation must reject cycles and reject any prerequisite whose `execution_rank` is not lower than
the dependent item, except a referenced item already in a terminal accepted state. This makes Epics 4, 6, 10,
and 12 implementable without renumbering or falsifying history.

### 7.2 PD10 authorization-spine correction

Amend the contract spine, regenerate all surfaces, and require these exact semantics:

1. All 49 protected operations evaluate authority before resource lookup. Post-authorization denials expose
   one 404 `tenant_access_denied/resource_unavailable` shape; caller-visible distinctions such as `not_found`,
   `cross_tenant_access_denied`, and `audit_access_denied` are removed from protected operation responses.
2. Authority-service unavailability has one non-disclosing 503 envelope with
   `details.visibility: redacted`; it is available to every protected operation and is evaluated before lookup.
3. `GetReadinessDiagnostics` and `GetProjectionFreshness` receive folder scope rather than remaining
   tenant-only exceptions in a folder-scoped product family.
4. `ListFolderAclEntries` requires folder `administer`.
5. `GetEffectivePermissions` supports self-inspection with folder read authority and an optional task context.
6. `GetTaskStatus` requires current tenant, bound-folder read authority, and task scope.
7. `ValidateProviderReadiness` remains tenant-level and requires folder-create authority; it does not invent a
   folder ACL for a folder that does not yet exist.
8. Authorization metadata becomes structured. Provider, repository, ref, and task dimensions are derived from
   an already authorized folder/task/binding and are never accepted as raw authority-bearing locators.
9. Every error includes the required visibility field. MVP release reasons permit only approved values such as
   `caller_completed`; reserved post-MVP reasons are rejected.
10. Regenerate OpenAPI, generated client, CLI/MCP parity, previous-spine comparison, C13 inventory, docs, and
    tests. Reapprove OQ3 against the resulting matrix digest.

### 7.3 PD11 C6/C3 lifecycle correction

Make the architecture matrix, C6 mapping, C3 policy, code, and tests express one model:

- an originating task re-acquiring a dirty workspace with staged changes transitions
  `dirty + WorkspaceLocked -> changes_staged` with a new lock instance;
- a retryable commit failure with no confirmed remote effect returns `changes_staged -> dirty`; a known
  non-retryable failure transitions to `failed`;
- `changes_staged + AuthRevocationDetected -> inaccessible` while preserving staged changes;
- after readiness is restored, `inaccessible + ProviderReadinessValidated -> dirty` when staged content
  remains within the C3 window, otherwise it transitions to `ready`;
- a clean dirty workspace whose lock becomes stale returns to `ready` and unlocked;
- `unknown_provider_outcome` is automatically recovering during bounded provider checks and escalates to
  `reconciliation_required` only when those checks cannot establish the result;
- operator discard, retry-success, and mark-failed transitions are reserved post-MVP and reject in MVP code;
  and
- C3 temporary cleanup starts only after terminal task closure with no active task, not on lock expiry or
  cancellation alone, and retains the approved seven-day window unless Legal approves a different one.

## 8. Approval Decision Register

Each decision below requires explicit approval. A blanket approval of this proposal records approval of every
recommended option; any exception keeps the associated decision open and the general execution freeze active.

| ID | Recommended decision | Required approval | Consequence if not approved |
| --- | --- | --- | --- |
| A1 / PD1 | Admit Epic 12 and OQ11 to product release authority. | Product, Architecture, Security, Test | Durable-data dependencies remain structurally outside the PRD; readiness stays failed. |
| A2 / PD3 | Admit Epic 13 plus OQ12/OQ13. | Product, Architecture, Security, Operations, Test | Release hardening remains architecture-only and cannot be scheduled authoritatively. |
| A2b / linked PD6 | Append NFR74–NFR84 and relock exact PRD/epics/traceability parity at 84. | Product, Architecture, Security, Operations, Test | PD3 is only partially resolved; do not change the current 73-row inventory or lift the freeze. |
| A3 / PD4 | Adopt the complete lifecycle table in Section 6. | Product, Architecture, Delivery | Conflicting statuses remain unresolved; do not change any row. |
| A4 / PD5 | Narrow and rename Story 10.9 as the metadata-only safety guard; keep body-content indexing post-MVP pending a future C9-backed requirement. | Product, Security, Architecture | Story 10.9 remains in review and body-content capability remains unapproved. |
| A5 / PD8 | Replace confidential overrides with correlation tokens at event-write time. | Product, Architecture, Security | Existing persistence/redaction contradiction remains release-blocking. |
| A6 / PD10 | Amend the spine using all ten rules in Section 7.2, including folder-scoping the two diagnostic operations and optional task context for effective permissions. | Product, Architecture, Security, Contract/Delivery | Do not regenerate clients or reuse the current OQ3 approval for release. |
| A6b / OQ3 | Reapprove the canonical authorization matrix after its digest changes. | Product, Architecture, Security | Corrected contract remains approval-pending and release-blocking. |
| A7 / PD11 | Align C6, C3, code, and tests to the PRD completion model in Section 7.3. | Product, Architecture, Security | Lifecycle authority remains contradictory and affected execution stays frozen. |
| A7b / C3 | Reapprove the terminal-task/no-active-task cleanup trigger and seven-day window. | Legal, Product, Security, Architecture | C3 cannot be relocked; destructive cleanup and lifecycle closure remain blocked. |
| A8 / freeze | Authorize conditional removal of the general execution freeze only after Section 9 passes. | Product, Architecture, Delivery | Freeze remains active even if edits validate. |

### Sponsor approval record

Jerome approved the complete proposal with the response `continue` on 2026-09-15 at 16:08:21+02:00. This
records sponsor approval of the recommended disposition for A1–A8 and authorizes routing to the named owners.
It does not substitute Jerome for the Product, Architecture, Security, Operations, Test, Delivery, or Legal
roles named above. Their role-specific attestations remain required inputs to the freeze-removal gate.

## 9. Freeze-Removal Gate

The freeze is not removed by approving this document alone. The implementing owner must attach evidence that:

- all A1–A8 decisions and required role approvals are recorded;
- PRD, architecture, UX specification, epics, manifest, and sprint tracker carry the new approved provenance;
- PRD and epics contain identical FR1–FR58 and NFR1–NFR84 inventories;
- the NFR traceability gate passes exact 84-row identity, ownership, classification, and negative controls;
- the manifest parses, uses the allowed status vocabulary, contains every canonical story exactly once, and
  agrees with `sprint-status.yaml` for every lifecycle row;
- all lifecycle reconciliations in Section 6 have evidence and no unresolved status conflict remains;
- OQ1–OQ4 approval digests are current and OQ3 has been reapproved after PD10;
- the execution-wave graph is acyclic and has no unresolved equal-rank or forward-rank dependency;
- C3, C6, C9, authorization-spine, generated-surface, and retention gates pass against their approved digests;
- no active implementation context cites the superseded August 4 manifest as current authority; and
- the planning-recovery action is closed in the same final change that sets
  `general_execution_hold: false`, with timestamp and validation evidence.

If any check fails, retain the freeze and the open recovery action. Release-specific OQ5–OQ13 evidence gates
may remain open only when they have an owner, executable story path, and lower-rank prerequisites; they block
release completion, not all authorized work in earlier waves.

## 10. Criteria for Rerunning `bmad-sprint-planning`

Rerun sprint planning only after the freeze-removal gate passes. The rerun must:

1. consume the newly approved digests of PRD, architecture, UX, epics, manifest, and sprint status;
2. confirm all seven PD rows are resolved and every linked approval in Section 8 is recorded;
3. verify exact FR1–FR58 and NFR1–NFR84 lockstep and a green traceability gate;
4. verify the Section 6 canonical lifecycle values and the absence of ambiguous generic `status` authority;
5. validate the execution-wave DAG and confirm the next authorized stories require no invention of a product,
   architecture, security, or Legal decision;
6. confirm refreshed implementation contexts reference the current manifest revision and approval digests;
7. distinguish open delivery-evidence gates from open design decisions; and
8. write a new dated readiness report rather than overwriting the August 4 failure report.

A readiness PASS means the plan is internally consistent and executable. It does not mean OQ5–OQ13 release
evidence is already complete.

## 11. Implementation Handoff

Classification: **Major — Product Manager coordination with Architecture, Security, Legal, Test, Operations,
and Delivery sign-off**.

After approval, the correction should be applied as one governed authority-relock change set. Recommended
ownership:

- Product Manager: PRD decisions, Epic 12/13 admission, Story 10.9 scope, lifecycle approval;
- Architect/Security: PD8, PD10, PD11, C6/C9, authorization matrix, and OQ3 reapproval;
- Legal/Product/Security: C3 cleanup trigger and retention relock;
- Test Architect: FR/NFR identity, manifest/DAG, contract, retention, and readiness evidence;
- Delivery: manifest regeneration, sprint status journal, context refresh, and conditional freeze removal.

No source authority other than this proposal has been modified in this workflow. Implementation must stop and
return for a revised decision if an approver rejects or materially alters any A1–A8 recommendation.

## Change Navigation Checklist Record

- [x] 1.1–1.3 Trigger, root problem, and evidence identified.
- [x] 2.1–2.5 Epics 4, 6, 10, 12, and 13 assessed; execution resequencing and new product authority identified.
- [x] 3.1 PRD conflicts PD1, PD3, PD4, PD5, PD8, PD10, and PD11 reviewed with exact proposed resolutions.
- [x] 3.2 Architecture, C3, C6, C9, authorization, and durable-data impacts reviewed.
- [x] 3.3 UX effects limited to disclosure, recovery-state, and error semantics.
- [x] 3.4 Manifest, sprint tracker, story evidence, generated contracts, traceability, and governance artifacts identified.
- [x] 4.1 Direct correction with authority relock selected; major effort and risk recorded.
- [x] 4.2 Rollback rejected because it would discard valid completed evidence.
- [x] 4.3 MVP scope adjusted only to keep unapproved body-content indexing outside the release.
- [x] 4.4 Recommended path, sequencing, and freeze controls defined.
- [x] 5.1–5.5 Proposal, exact edits, status table, approval register, and handoff completed.
- [x] 6.1–6.2 Proposal checked against the current planning set, approved proposals, and implementation evidence.
- [x] 6.3 User approval recorded from Jerome's `continue` response on 2026-09-15; named-role attestations remain implementation gates.
- [N/A] 6.4 No approval-time sprint inventory edit is required: Epics 12 and 13 and their stories already exist in `sprint-status.yaml`; lifecycle reconciliation belongs to the routed implementation change set.
- [x] 6.5 Major-scope handoff recorded; freeze removal remains conditional on Section 9 validation.

## Current Workflow State

Proposal status: **Approved — routed for implementation**.

Execution freeze: **Retained**.

Source artifacts modified by this workflow: **No**.

## Approval and Workflow Execution Log

- **Final decision:** Approved by Jerome.
- **Approval time:** 2026-09-15T16:08:21+02:00.
- **Approval response:** `continue`.
- **Change scope:** Major.
- **Selected approach:** Direct planning correction with an authority relock.
- **Primary routing:** Product Manager and Solution Architect.
- **Required supporting routing:** Product Owner, Security, Legal, Operations, Test Architect, UX Designer,
  Contract/Delivery, and Developer.
- **Approval boundary:** A1–A8 are sponsor-approved as recommendations; named-role attestations remain pending
  and cannot be inferred from this response.
- **Sprint-status reconciliation:** No lifecycle value changed during approval. Epics 12 and 13 are already
  registered, so no approval-time add/remove/renumber operation is required.
- **Artifacts modified by this workflow:** This finalized Sprint Change Proposal only.
- **Artifacts awaiting implementation:** PRD, architecture, UX specification, epics, planning-story manifest,
  sprint-status reconciliation metadata, active contexts, authorization/C3/C6/C9 authorities, generated
  contracts, NFR traceability, validation evidence, and the workflow memory log.
- **Execution control:** The general freeze remains active. It may be removed only by the routed implementation
  after every Section 9 gate passes.
- **Readiness rerun:** Pending implementation, role attestations, and freeze-removal validation.
