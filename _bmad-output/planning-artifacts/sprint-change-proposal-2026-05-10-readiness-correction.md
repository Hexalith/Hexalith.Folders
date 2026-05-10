---
project: Hexalith.Folders
date: 2026-05-10
workflow: bmad-correct-course
mode: incremental
status: approved
triggerArtifact: D:\Hexalith.Folders\_bmad-output\planning-artifacts\implementation-readiness-report-2026-05-10.md
approvedEditProposalCount: 16
---

# Sprint Change Proposal - Readiness Correction

## 1. Issue Summary

### Trigger

The implementation readiness reassessment completed on 2026-05-10 found that the planning artifacts are aligned at the requirements level but are not yet cleanly implementation-ready.

This was discovered before implementation. No completed code needs rollback.

### Core Problem

The PRD, architecture, and epics preserve the intended MVP and FR coverage, but `epics.md` contains backlog execution defects:

- Some acceptance criteria depend on future story outputs.
- Several stories are too large for safe implementation and review.
- Epic 1 reads as technical foundation work unless its consumer value is made explicit.
- Epic 7 is release-readiness/NFR validation work and should be framed as a release gate.
- Story 3.6 combines repository creation and existing-repository binding.
- PRD NFR bullets and epics NFR numbering need a release-gate traceability bridge.

### Evidence

Evidence comes from `implementation-readiness-report-2026-05-10.md`, especially the reassessment addendum:

- PRD FRs extracted: 57.
- PRD NFR bullets extracted: 70.
- Epic FR coverage: 57/57.
- Missing standalone UX document, with UX requirements embedded in PRD, architecture, and Epic 6.
- Critical sequencing issues in Stories 4.3, 4.11, 6.3, and 6.4.
- Oversized stories identified: 1.6, 1.9, 4.5, 4.13, 5.5, 7.4, and 7.8.
- Story 3.6 recommended for split.
- Epic 1 and Epic 7 recommended for reframing.

## 2. Impact Analysis

### Epic Impact

Epic 1 remains valid, but should be reframed as a greenfield bootstrap exception that delivers consumer value through a stable canonical contract and generated-surface consistency.

Epic 2 requires no scope change.

Epic 3 remains valid, but Story 3.6 should split into separate repository creation and existing-repository binding stories.

Epic 4 remains valid, but needs forward-reference cleanup and story decomposition around file mutation and lifecycle validation.

Epic 5 remains valid, but the full cross-surface parity story should split into transport parity, adapter behavioral parity, and mixed-surface handoff.

Epic 6 remains valid, but Stories 6.3 and 6.4 should become independently completable reusable component stories. Story 6.6 should own incident-stream integration.

Epic 7 remains valid only if framed as an MVP release-readiness gate rather than a normal feature epic. CI/CD and documentation stories should be split.

### Artifact Impact

PRD: no product-scope change required. MVP remains achievable.

Architecture: no technology, component, or decision change required. Existing architecture already supports the correction through C0-C13, Phase 0.5, C6, C13, F-4 through F-7, and release-validation gates.

UX: no standalone UX artifact exists. Epic 6 continues as the UX source of truth for the read-only operations console.

Sprint status: `D:\Hexalith.Folders\_bmad-output\implementation-artifacts\sprint-status.yaml` must be synchronized after approved story splits and renumbering are applied.

## 3. Recommended Approach

Selected path: Direct Adjustment.

Effort: Medium.

Risk: Low to Medium.

Rationale: this is a backlog executability issue, not a product pivot or architecture reversal. The right correction is to revise `epics.md`, preserve the current epic order and 57/57 FR coverage, then synchronize sprint tracking and rerun implementation readiness.

No rollback is needed because implementation has not started.

No PRD MVP review is needed because the core MVP remains intact.

## 4. Detailed Change Proposals

### Proposal 1: Fix Story 4.3 Forward Dependency

Story: 4.3 Acquire task-scoped workspace lock with deterministic single-active-writer enforcement.

Change: replace the acceptance criterion that says lock metadata is exposed via the workspace-status query from Story 4.7.

New wording:

```markdown
**And** folder transitions `ready -> locked`; lock state, owner, age, expiry, and retry eligibility are captured in `FolderState` and the emitted event metadata so later query/projection stories can expose them without re-deriving lock ownership
```

Rationale: Story 4.3 owns command/state/event behavior. Story 4.7 owns workspace-status query exposure.

### Proposal 2: Fix Story 4.11 Forward Dependency

Story: 4.11 Surface canonical error taxonomy, workspace states, and operational evidence after any failure.

Change: remove dependency on audit from Story 4.12.

New wording:

```markdown
**When** the lifecycle command or status surface returns the failure result available by this point in Epic 4
**Then** the response explains the final state per the C6 matrix, retry eligibility (boolean), retry-after hint when known, correlation ID, and the categorized reason
**And** the failure result includes metadata needed by later audit/projection stories to reconstruct the causation chain without changing the canonical error shape
```

Rationale: Story 4.11 owns canonical error shape and status-visible failure evidence. Story 4.12 later proves metadata-only audit reconstruction.

### Proposal 3: Fix Story 6.3 Incident-Stream Forward Dependency

Story: 6.3 Render operator-disposition labels as the primary visual with technical state as secondary metadata.

Change: make Story 6.3 a reusable mapper/component story and move incident-stream integration to Story 6.6.

New Story 6.3 wording:

```markdown
**And** the badge and metadata components expose reusable parameters/events that allow later diagnostic views, including incident-mode views, to render disposition labels consistently without duplicating mapping logic
```

Add to Story 6.6:

```markdown
**And** `IncidentStream.razor` uses the shared `OperatorDispositionBadge` and `TechnicalStateMetadata` components from Story 6.3 alongside raw event types so operators do not switch vocabularies mid-incident
```

### Proposal 4: Fix Story 6.4 Incident-Stream Forward Dependency

Story: 6.4 Implement sensitive-metadata redaction affordance with lock-icon.

Change: make Story 6.4 a reusable redaction component story and move incident-stream application to Story 6.6.

New Story 6.4 wording:

```markdown
**And** the redaction component exposes reusable rendering semantics for later diagnostic views so redaction behavior stays consistent without duplicating policy interpretation in each page
```

Add to Story 6.6:

```markdown
**And** `IncidentStream.razor` renders redacted values through the shared `RedactedField` component from Story 6.4; redaction rules do not relax under degraded mode
```

### Proposal 5: Split Story 3.6 Into Two Provider Workflows

Replace Story 3.6 with:

```markdown
### Story 3.6: Create a new repository-backed folder
```

Add:

```markdown
### Story 3.7: Bind an existing repository to a folder
```

Renumber current Story 3.7 to 3.8 and current Story 3.8 to 3.9.

Rationale: repository creation and repository binding have different provider calls, idempotency concerns, and failure modes.

### Proposal 6: Split Story 1.6 Contract Spine

Replace Story 1.6 with:

```markdown
### Story 1.6: Author Contract Spine foundation and shared extension vocabulary
```

```markdown
### Story 1.7: Author tenant, folder, provider, and repository-binding contract groups
```

```markdown
### Story 1.8: Author workspace, lock, file-operation, context-query, commit, status, and audit contract groups
```

Renumber current Story 1.7 to 1.9, current Story 1.8 to 1.10, and split current Story 1.9 starting at 1.11.

Rationale: the Contract Spine should land in reviewable capability slices while preserving one canonical OpenAPI artifact.

### Proposal 7: Split Story 1.9 CI Gates

Replace the CI mega-story with:

```markdown
### Story 1.11: Wire Contract Spine drift and generated-client CI gates
```

```markdown
### Story 1.12: Wire safety invariant CI gates
```

```markdown
### Story 1.13: Wire exit-criteria and parity-oracle completeness gates
```

Rationale: contract drift, safety invariants, and governance/completeness gates are different CI concerns and should be independently reviewable.

### Proposal 8: Split Story 4.5 File Mutation Scope

Replace Story 4.5 with:

```markdown
### Story 4.5: Enforce workspace path policy before file mutations
```

```markdown
### Story 4.6: Add and change files with inline and streamed content transport
```

```markdown
### Story 4.7: Remove files with metadata-only events and provider-safe ordering
```

Renumber following Epic 4 stories.

Rationale: path security, write/update transport, and delete semantics are independently testable.

### Proposal 9: Split Story 4.13 Lifecycle Validation

Replace the lifecycle validation mega-story with:

```markdown
### Story 4.x: Validate replay and projection determinism
```

```markdown
### Story 4.x: Validate security, redaction, path, and encoding invariants
```

```markdown
### Story 4.x: Seed lifecycle capacity test harness
```

Assign final numbers after Epic 4 renumbering.

Rationale: determinism, safety invariants, and capacity harness setup are separate validation efforts.

### Proposal 10: Split Story 5.5 Cross-Surface Parity

Replace Story 5.5 with:

```markdown
### Story 5.5: Validate golden lifecycle parity across REST and SDK
```

```markdown
### Story 5.6: Validate behavioral parity across CLI and MCP
```

```markdown
### Story 5.7: Validate mixed-surface handoff scenario
```

Rationale: transport parity, adapter behavioral parity, and mixed-surface handoff should fail independently.

### Proposal 11: Reframe Epic 7 As Release-Readiness Gate

Rename:

```markdown
## Epic 7: MVP Release-Readiness Gate
```

Use this intro:

```markdown
This release-readiness workstream validates the integrated system against production invariants that cannot be proven from local-dev alone: deny-by-default Dapr access control with mTLS, pluggable OIDC, container deployment, capacity targets at scale, retention enforcement, observability exporters, documentation completeness, tenant-deletion runbooks, and release-package traceability. It does not add new product FRs; it gates MVP release by proving the NFR and operational evidence required by the PRD and architecture.
```

Rationale: Epic 7 is valuable, but it is a release gate, not a feature epic.

### Proposal 12: Split Story 7.4 CI/CD And Release Publishing

Replace Story 7.4 with:

```markdown
### Story 7.4: Consolidate PR CI workflow gates
```

```markdown
### Story 7.5: Wire scheduled drift and policy-conformance workflows
```

```markdown
### Story 7.6: Publish traceable NuGet release packages
```

Renumber later Epic 7 stories.

Rationale: PR CI, scheduled/policy checks, and package publishing have different triggers and review paths.

### Proposal 13: Split Story 7.8 Documentation And ADR Deliverables

Replace Story 7.8 with:

```markdown
### Story 7.x: Publish API, SDK, CLI, and MCP consumer references
```

```markdown
### Story 7.x: Publish operations, audit, provider, and error documentation
```

```markdown
### Story 7.x: Publish ADR set and maintenance runbooks
```

Assign final numbers after Epic 7 renumbering.

Rationale: consumer docs, operator/audit/provider docs, and ADR/runbook material serve different readers and should be reviewed independently.

### Proposal 14: Reframe Epic 1 As Bootstrap With Consumer Value

Rename:

```markdown
## Epic 1: Bootstrap Canonical Contract For Consumers And Adapters
```

Use this intro:

```markdown
API consumers, adapter implementers, and maintainers can rely on a scaffolded Hexalith.Folders module with a canonical OpenAPI v1 Contract Spine that drives every surface (REST, SDK, CLI, MCP) before feature work begins. The bootstrap work is a greenfield exception: it delivers consumer value by preventing contract drift, generated-client mismatch, unsafe examples, missing exit criteria, and cross-surface parity ambiguity before downstream features depend on them.
```

Rationale: Epic 1 remains a bootstrap exception, but its value is explicit.

### Proposal 15: Reword Story 4.4 Future Commit Reference

Story: 4.4 Inspect lock state and release the workspace lock.

Replace:

```markdown
**And** if mutations have been applied (folder is in `changes_staged`), release is rejected with `state_transition_invalid` -- the actor must commit (Story 4.10) or accept that lock-lease expiry will move the folder to `dirty`
```

With:

```markdown
**And** if mutations have been applied (folder is in `changes_staged`), release is rejected with `state_transition_invalid`; the state model requires the actor to complete the commit path before clean release, or the lock-lease expiry path will move the folder to `dirty`
```

Rationale: preserves the state rule without a forward-story reference.

### Proposal 16: Reconcile NFR Traceability Without Changing PRD Intent

Add after the epics NFR inventory:

```markdown
> NFR Traceability Note: The PRD expresses 70 non-functional requirement bullets. This epics inventory consolidates closely related bullets into NFR1-NFR67 for implementable planning. Before release gating, maintain a traceability table mapping every PRD NFR bullet to one of: an epic/story acceptance criterion, an architecture exit criterion artifact, an automated test gate, or documented release-validation evidence. No PRD NFR may remain unmapped.
```

Add to the release-readiness documentation/traceability story:

```markdown
**And** `docs/exit-criteria/nfr-traceability.md` maps every PRD NFR bullet to the corresponding epic/story, architecture exit criterion, automated test gate, or release-validation evidence artifact; release fails if any PRD NFR bullet is unmapped
```

Rationale: preserves PRD wording and current epic NFR numbering while closing the release-gate traceability gap.

## 5. Implementation Handoff

### Scope Classification

Moderate change.

This should be routed to Product Owner / backlog curator plus Developer / implementation planning agent. It does not require PM-level MVP rewrite or architecture reversal.

### Handoff Recipients

Product Owner / backlog curator:

- Apply approved story splits and epic reframes to `epics.md`.
- Renumber affected stories consistently.
- Update all intra-document story references after renumbering.
- Preserve 57/57 FR coverage.
- Add NFR traceability note and release-gate acceptance criterion.

Developer / implementation planning agent:

- Validate each revised story is independently completable.
- Synchronize `sprint-status.yaml` after `epics.md` changes.
- Rerun `bmad-check-implementation-readiness`.
- Proceed to sprint planning only when readiness passes or residual risks are explicitly accepted.

### Success Criteria

- No story acceptance criterion depends on future story outputs.
- Oversized stories are split into independently verifiable units.
- Epic 1 communicates contract-consumer value.
- Epic 7 is clearly an MVP release-readiness gate.
- Story 3.6 is split into repository creation and existing-repository binding.
- NFR traceability maps every PRD NFR bullet to implementation or release evidence.
- FR coverage remains 57/57.
- `sprint-status.yaml` matches the approved revised backlog.

## 6. Checklist Status

| Section | Status | Notes |
| --- | --- | --- |
| 1. Understand Trigger and Context | Done | Trigger is readiness reassessment before implementation. |
| 2. Epic Impact Assessment | Done | Current epic order remains viable with direct adjustments. |
| 3. Artifact Conflict and Impact Analysis | Done | PRD and architecture remain valid; epics and sprint status are impacted. |
| 4. Path Forward Evaluation | Done | Direct Adjustment selected. |
| 5. Proposal Components | Done | Sixteen incremental proposals approved in conversation. |
| 6. Final Review and Handoff | Done | Approved by Jerome on 2026-05-10. |

## 7. Approval

Approved by Jerome on 2026-05-10.

### Route

**Change scope:** Moderate

**Routed to:** Product Owner / backlog curator and Developer / implementation planning agent

### Workflow Execution Log

- Trigger addressed: Implementation readiness reassessment found forward-story dependencies, oversized stories, NFR traceability mismatch, unclear Epic 1/Epic 7 value framing, and a combined repository creation/binding story.
- Artifacts modified by this workflow: Sprint Change Proposal only.
- Artifacts proposed for implementation: `D:\Hexalith.Folders\_bmad-output\planning-artifacts\epics.md` and, after that update, `D:\Hexalith.Folders\_bmad-output\implementation-artifacts\sprint-status.yaml`.
- Handoff complete: Apply approved edits to `epics.md`, synchronize `sprint-status.yaml`, rerun `bmad-check-implementation-readiness`, then proceed to sprint planning only if readiness passes or residual risks are explicitly accepted.
