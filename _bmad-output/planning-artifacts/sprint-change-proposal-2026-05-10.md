---
project: Hexalith.Folders
date: 2026-05-10
workflow: bmad-correct-course
mode: incremental
status: approved
triggerArtifact: D:\Hexalith.Folders\_bmad-output\planning-artifacts\implementation-readiness-report-2026-05-10.md
approvedProposalCount: 11
---

# Sprint Change Proposal - Hexalith.Folders

## 1. Issue Summary

### Trigger

The implementation readiness assessment completed on 2026-05-10 found that the planning artifacts are close, but not yet implementation-ready. The change trigger is not a new product requirement and not a failed implementation approach. It is a planning-quality correction discovered before sprint planning.

### Core Problem

The PRD, architecture, and epics are well-aligned at the requirements level, but the epics/stories need execution cleanup before implementation starts:

- Some acceptance criteria depend on future stories, making those stories not independently completable in sequence.
- Several stories are too large for safe implementation and review.
- Epic 1 and Epic 7 are valid workstreams, but their value framing reads too much like technical phase management.
- One provider story combines two distinct workflows: creating a new repository-backed folder and binding an existing repository.

### Evidence

Evidence comes from `implementation-readiness-report-2026-05-10.md`:

- PRD extraction found 57 functional requirements and 70 non-functional requirements.
- Epic coverage validation found 57 of 57 PRD FRs covered by epics.
- UX alignment found no standalone UX spec, but architecture and epics capture console UX decisions through F-1 through F-7 and AR-UI-01 through AR-UI-07.
- Epic quality review found forward-story acceptance criteria in Stories 4.3, 4.11, 6.3, and 6.4.
- Epic quality review found oversized stories: 1.6, 1.9, 4.5, 4.13, 5.5, 7.4, and 7.8.
- Epic quality review recommended reframing Epic 1 and Epic 7 and splitting Story 3.6.

## 2. Impact Analysis

### Epic Impact

**Epic 1:** Keep the work, but reframe from a technical foundation epic to a contract-value epic. Split the broad Contract Spine and CI-gate stories into smaller reviewable stories.

**Epic 2:** No scope change required.

**Epic 3:** Split Story 3.6 into two workflows: repository creation and existing repository binding. Renumber following Epic 3 stories and update references.

**Epic 4:** Remove forward references from Story 4.3 and Story 4.11. Split the large file-operations story and the large lifecycle-validation story into smaller stories. Renumber following Epic 4 stories and update references.

**Epic 5:** Split the cross-surface parity scenario into a reusable harness story and a scenario-validation story.

**Epic 6:** Remove forward references from Story 6.3 and Story 6.4. No additional scope change required.

**Epic 7:** Reframe as a release-readiness/NFR-validation gate. Split release pipeline/package publishing and documentation/ADR stories.

### Story Impact

Stories requiring direct modification:

- Story 1.6
- Story 1.9
- Story 3.6
- Story 4.3
- Story 4.5
- Story 4.11
- Story 4.13
- Story 5.5
- Story 6.3
- Story 6.4
- Story 7.4
- Story 7.8

Stories requiring renumbering/reference updates:

- Epic 1 stories after Story 1.6
- Epic 3 stories after Story 3.6
- Epic 4 stories after Story 4.5
- Epic 5 stories after Story 5.5
- Epic 7 stories after Story 7.4

### Artifact Conflicts

**PRD:** No PRD modification required. MVP scope and FRs remain intact.

**Architecture:** No architecture decision changes required. Architecture remains the source for F-1 through F-7, AR decisions, C0-C13, and the implementation sequence. Minor reference cleanup may be needed if story numbers are cited in future architecture-derived handoff text.

**UX:** No standalone UX document exists. The correction preserves the current approach: AR-UI-01 through AR-UI-07 and architecture F-1 through F-7 are the UX source of truth for the operations console.

**Epics:** `epics.md` is the primary artifact requiring updates.

**Sprint status:** No sprint-status update is currently required because sprint planning has not yet started. If a sprint plan is generated before these changes are applied, it must be regenerated afterward.

### Technical Impact

No production code changes are implied by this proposal. The change affects backlog quality, sequencing, and story granularity before implementation.

## 3. Recommended Approach

### Selected Path

**Direct Adjustment**

### Rationale

The artifacts do not need a PRD reset, architecture rewrite, or MVP reduction. Requirements coverage is strong and architecture alignment is sound. The cheapest and safest correction is to adjust the epics/stories before sprint planning.

### Alternatives Considered

**Potential Rollback:** Not applicable. No implementation sprint has started and no completed implementation work needs to be reverted.

**PRD MVP Review:** Not needed. MVP scope remains achievable; the issue is execution planning, not product scope.

### Effort and Risk

**Effort:** Medium. The changes are mostly document restructuring, but careful renumbering and reference updates are needed.

**Risk:** Medium if performed manually without validation. Low after a follow-up implementation-readiness check confirms traceability and sequencing.

### Scope Classification

**Moderate.** This requires backlog reorganization and likely a Product Owner / Developer handoff, but not a fundamental PM/Architect replan.

## 4. Detailed Change Proposals

All proposals below were reviewed incrementally and approved by Jerome.

### Proposal 1: Remove Forward-Story Acceptance Dependencies

#### Story 4.3 Acceptance Criteria

OLD:

```markdown
**And** folder transitions `ready -> locked`; lock state, owner, age, expiry, and retry eligibility are exposed via the workspace-status query (Story 4.7)
```

NEW:

```markdown
**And** folder transitions `ready -> locked`; the `WorkspaceLocked` event captures lock state, owner, age/expiry basis, and retry-eligibility metadata needed by later status projections
```

#### Story 4.11 Acceptance Criteria

OLD:

```markdown
**When** the actor inspects status (Story 4.7) or audit (Story 4.12)
```

NEW:

```markdown
**When** the actor inspects lifecycle status or failure evidence available up to this point in the implementation sequence
```

#### Story 6.3 Acceptance Criteria

OLD:

```markdown
**And** the disposition label appears alongside raw event types in incident-stream views (Story 6.6) so operators do not switch vocabularies mid-incident
```

NEW:

```markdown
**And** the disposition badge component exposes a reusable rendering contract that later incident-stream views can use without redefining state-label behavior
```

#### Story 6.4 Acceptance Criteria

OLD:

```markdown
**And** the lock icon affordance also applies to incident-stream views (Story 6.6); redaction rules do not relax under degraded mode
```

NEW:

```markdown
**And** the redacted-field component exposes a reusable rendering contract for degraded-mode views; redaction rules are centralized so later views cannot relax them
```

Rationale: Current criteria require future story surfaces before the current stories can be accepted.

### Proposal 2: Split Story 3.6 Into Two Workflows

OLD:

```markdown
### Story 3.6: Create repository-backed folder and bind existing repository

As an authorized actor,
I want to either create a new Git-backed repository for an existing folder via `CreateRepositoryBackedFolder` (or extend `CreateFolder` from Story 2.3) or bind a folder to an existing repository via `BindRepository`, gated by a green provider-readiness check,
So that the folder transitions from `requested` to `preparing` to `ready` in the C6 workspace state machine and subsequent workspace tasks have a real Git target to operate on.
```

NEW:

```markdown
### Story 3.6: Create repository-backed folder

As an authorized actor,
I want to create a new Git-backed repository for an existing logical folder via `CreateRepositoryBackedFolder`, gated by a green provider-readiness check,
So that a tenant folder can become repository-backed through a controlled provisioning path before workspace tasks begin.

### Story 3.7: Bind folder to an existing repository

As an authorized actor,
I want to bind an existing logical folder to an existing provider repository via `BindRepository`, gated by provider readiness, repository access validation, and branch/ref policy compatibility,
So that brownfield or pre-created repositories can participate in the same canonical workspace lifecycle without sharing the repository-creation failure path.
```

Renumbering impact:

- Old Story 3.7 becomes Story 3.8.
- Old Story 3.8 becomes Story 3.9.
- Story 4.2 branch/ref reference changes from Story 3.7 to Story 3.8.
- Story 6.5 provider-readiness aggregation reference changes from Story 3.8 to Story 3.9.

Rationale: Repository creation and repository binding have different provider behavior, idempotency risks, and failure modes.

### Proposal 3: Reframe Epic 1 From Technical Foundation to Contract Value

OLD:

```markdown
## Epic 1: Foundation - Solution Scaffolding & Contract Spine

Platform engineers can scaffold the module against the Hexalith ecosystem (Hexalith.Tenants baseline + Hexalith.EventStore.Admin.* surfaces) and lock a canonical OpenAPI v1 Contract Spine that drives every cross-surface generation (REST, SDK, CLI, MCP) - with CI gates preventing drift before any feature lands. The deliverables of this epic are an empty-but-coherent codebase, a frozen contract artifact, a parity oracle, and a CI pipeline that every subsequent epic builds on.
```

NEW:

```markdown
## Epic 1: Canonical Contract Spine & Generated Surface Baseline

API consumers, adapter implementers, and platform engineers can rely on a frozen OpenAPI v1 Contract Spine, generated SDK baseline, parity oracle, and drift-prevention gates before feature implementation begins. The epic still includes greenfield solution scaffolding, but its value is contract reliability: REST, SDK, CLI, and MCP work from one source of truth instead of diverging as implementation starts.
```

Rationale: Keeps the greenfield bootstrap while making user value explicit.

### Proposal 4: Reframe Epic 7 as Release Readiness / NFR Validation

OLD:

```markdown
## Epic 7: Production Hardening & Release Readiness

This epic does NOT add user-facing features. It validates the integrated system against production invariants that cannot be proven from local-dev alone: deny-by-default Dapr access control with mTLS, pluggable OIDC, container deployment, capacity targets at scale, retention enforcement, observability exporters, and the full documentation surface - all while pinning the deferred quantitative exit criteria (C1, C2, C3, C5) with measurement evidence.
```

NEW:

```markdown
## Epic 7: Production Readiness Gate - NFR Validation & Release Evidence

This release-readiness epic does not add new functional scope. It proves that the integrated MVP is safe to operate beyond local development by validating deny-by-default Dapr access control with mTLS, pluggable OIDC, container deployment, calibrated capacity targets, retention enforcement, observability exporters, release packaging, documentation, and the deferred quantitative exit criteria (C1, C2, C3, C5) with measurable evidence.
```

Also update the Epic List summary:

```markdown
OLD:
### Epic 7: Production Hardening & Release Readiness

NEW:
### Epic 7: Production Readiness Gate - NFR Validation & Release Evidence
```

Rationale: Epic 7 is a release-readiness gate, not a normal feature epic.

### Proposal 5: Split Story 4.5 File Operations

OLD:

```markdown
### Story 4.5: Add, change, and remove files with workspace-root confinement and path policy enforcement
```

NEW:

```markdown
### Story 4.5: Enforce workspace path confinement and canonical path policy

As a developer or AI agent holding the workspace lock,
I want every file operation path to be normalized and validated against workspace-root, traversal, absolute-path, separator, reserved-name, symlink, Unicode, and case-collision policies,
So that no file operation can escape the workspace or create ambiguous provider-specific paths.

### Story 4.6: Add and change files through bounded inline and stream transports

As a developer or AI agent holding the workspace lock,
I want to add or change files via bounded inline and multipart stream transports with binary/large-file limits enforced before provider writes,
So that file writes are deterministic, bounded, retry-safe, and aligned with the D-9 transport contract.

### Story 4.7: Remove files with lock, path, tenant, and branch/ref policy enforcement

As a developer or AI agent holding the workspace lock,
I want to remove files through the same policy pipeline used by add/change operations,
So that deletes are auditable, idempotent, and cannot bypass workspace or tenant boundaries.
```

Renumbering impact:

- Current Story 4.6 becomes Story 4.8.
- Current Story 4.7 becomes Story 4.9.
- Current Story 4.10 becomes Story 4.12.
- Current Story 4.12 becomes Story 4.14.
- Current Story 4.13 becomes Story 4.15 before later split.

Rationale: File operation policy, write transport, and delete semantics are independently verifiable.

### Proposal 6: Split Story 1.9 CI Gates

OLD:

```markdown
### Story 1.9: Wire CI gates (server-vs-spine, golden-file, sentinel, encoding, exit-criteria-presence, pattern-examples, cache-key prefix)
```

NEW:

```markdown
### Story 1.9: Wire contract drift CI gates

As a maintainer,
I want server-vs-spine validation, symmetric drift detection, NSwag golden-file consistency, and parity-oracle schema validation wired into CI,
So that contract drift fails before implementation surfaces diverge.

### Story 1.10: Wire security and redaction sentinel CI gates

As a maintainer,
I want sentinel-corpus redaction tests and forbidden-field scanning wired into CI,
So that events, logs, traces, metrics, audit records, console payloads, provider diagnostics, and generated artifacts cannot leak secrets or file content.

### Story 1.11: Wire encoding, examples, exit-criteria, and cache-key policy CI gates

As a maintainer,
I want idempotency-encoding equivalence, pattern-example compilation, exit-criteria presence, and tenant-prefixed cache-key lint wired into CI,
So that implementation cannot proceed with missing release evidence, stale examples, unstable idempotency encoding, or unsafe tenant cache keys.
```

Reference update rule:

- Contract/parity/drift references point to Story 1.9.
- Sentinel/redaction references point to Story 1.10.
- Encoding, examples, exit criteria, and cache-key references point to Story 1.11.

Rationale: Each CI-gate family should have its own review and failure signal.

### Proposal 7: Split Story 1.6 Contract Spine

OLD:

```markdown
### Story 1.6: Author OpenAPI v1 Contract Spine with extension vocabulary
```

NEW:

```markdown
### Story 1.6: Define OpenAPI extension vocabulary and shared contract conventions

As an API consumer and adapter implementer,
I want the OpenAPI Contract Spine to define shared conventions for auth, idempotency, correlation IDs, pagination, freshness, errors, lifecycle-state metadata, and `x-hexalith-*` extensions,
So that every capability group uses the same contract language before endpoint paths are authored.

### Story 1.7: Author core lifecycle OpenAPI operations and schemas

As an API consumer and adapter implementer,
I want the folder, provider-readiness, workspace, lock, file-operation, commit, context-query, status, audit, and ops-console operations represented in the OpenAPI Contract Spine with request/response schemas,
So that REST, SDK, CLI, and MCP surfaces can be generated from complete canonical operations.

### Story 1.8: Validate Contract Spine completeness against PRD FR coverage

As a maintainer,
I want a contract-completeness check that maps Contract Spine operations and schemas back to FR coverage expectations,
So that missing lifecycle operations are found before SDK generation and implementation begin.
```

Final Epic 1 sequence:

```markdown
1.6 OpenAPI extension vocabulary and shared conventions
1.7 Core lifecycle OpenAPI operations and schemas
1.8 Contract Spine completeness validation
1.9 NSwag SDK generation
1.10 C13 parity oracle generation
1.11 Contract drift CI gates
1.12 Security/redaction sentinel CI gates
1.13 Encoding/examples/exit-criteria/cache-key CI gates
```

Rationale: OpenAPI conventions, operations, and completeness validation are separate deliverables.

### Proposal 8: Split Story 4.13 Lifecycle Validation

Assumption: Proposal 5 is applied first, so old Story 4.13 has already shifted to Story 4.15.

OLD:

```markdown
### Story 4.13: Validate canonical lifecycle through replay, projection, sentinel, path-security, encoding, isolation, and capacity tests
```

NEW:

```markdown
### Story 4.15: Validate lifecycle replay and projection determinism

As a maintainer,
I want replay tests and projection determinism tests for the canonical lifecycle event stream,
So that aggregate state and read models can be rebuilt consistently from durable events.

### Story 4.16: Validate lifecycle security boundaries

As a maintainer,
I want sentinel-redaction, path-security, encoding-equivalence, and cross-tenant isolation tests for the canonical lifecycle,
So that lifecycle behavior is mechanically checked for secret safety, path safety, encoding stability, and tenant isolation.

### Story 4.17: Seed lifecycle capacity harness for later calibration

As a maintainer,
I want the NBomber lifecycle capacity harness seeded with prepare -> lock -> mutate -> commit scenarios but parameterized without final production thresholds,
So that Epic 7 can later calibrate the same harness against measured C1/C2/C5 targets.
```

Rationale: Replay/projection, security boundaries, and capacity harnessing are different risk families.

### Proposal 9: Split Story 7.4 Release Pipeline From Package Publishing

OLD:

```markdown
### Story 7.4: Wire full CI/CD pipeline and publish NuGet release packages
```

NEW:

```markdown
### Story 7.4: Wire full CI/CD quality pipeline

As a maintainer,
I want `.github/workflows/ci.yml`, `contract-tests.yml`, `nightly-drift.yml`, and `policy-conformance.yml` covering build, format, lint, unit, contract, parity, sentinel, drift, policy-conformance, and capacity gates,
So that every PR and scheduled validation run enforces the product's quality and safety gates without manual orchestration.

### Story 7.5: Publish versioned NuGet release packages

As a maintainer,
I want `release.yml` publishing `Hexalith.Folders.Contracts`, `Hexalith.Folders.Client`, `Hexalith.Folders.Aspire`, and `Hexalith.Folders.Testing` to NuGet or the configured Hexalith feed on tagged release,
So that downstream consumers receive traceable, semver-versioned packages only after all release gates pass.
```

Renumbering impact:

- Existing Story 7.5 becomes Story 7.6.
- Existing Story 7.6 becomes Story 7.7.
- Existing Story 7.7 becomes Story 7.8.
- Existing Story 7.8 becomes Story 7.9 before later split.

Rationale: Quality-pipeline verification and package-publishing verification have different done criteria.

### Proposal 10: Split Story 7.8 Documentation Deliverables From ADR Set

Assumption: Proposal 9 is applied first, so old Story 7.8 has already shifted to Story 7.9.

OLD:

```markdown
### Story 7.8: Publish full documentation deliverables and ADRs
```

NEW:

```markdown
### Story 7.9: Publish consumer and operator documentation deliverables

As a downstream consumer, operator, auditor, or new team member,
I want the full documentation deliverable set published per AR-DOC-01..04: rendered OpenAPI v1 reference, getting-started guide, auth/tenant/folder-ACL guide, lifecycle diagrams, CLI reference, MCP reference, SDK reference and quickstart, provider integration/testing guide, operations console/audit guide, and error catalog,
So that users can understand and operate the product without code archaeology.

### Story 7.10: Publish ADR set and lifecycle runbooks

As a future maintainer or architect,
I want the ADR set and required lifecycle runbooks published per AR-DOC-05,
So that design rationale, tenant deletion, retention, alerts, rollback, and operational decisions survive handoff and release pressure.
```

Rationale: Consumer/operator documentation and ADR/runbook deliverables have different reviewers and acceptance criteria.

### Proposal 11: Split Story 5.5 Cross-Surface Parity

OLD:

```markdown
### Story 5.5: End-to-end cross-surface parity scenario and cross-adapter invariants
```

NEW:

```markdown
### Story 5.5: Build cross-surface parity test harness

As a stakeholder validating the MVP "one canonical workflow contract" claim,
I want a reusable parity test harness that can drive REST, CLI, MCP, and SDK surfaces with the same operation identity, tenant context, task ID, correlation ID, and idempotency inputs,
So that cross-surface scenarios can be authored once and executed consistently across every adapter.

### Story 5.6: Validate canonical lifecycle through cross-surface parity scenario

As a stakeholder validating the MVP "one canonical workflow contract" claim,
I want the canonical task lifecycle scenario (provider readiness -> create/bind folder -> prepare -> lock -> write files -> query context -> commit -> inspect status -> release) executed through REST, CLI, MCP, and SDK in one test run,
So that the four-surface promise is proven by a scenario that fails loudly if any surface drifts in error category, idempotency replay, correlation propagation, audit emission, or terminal state.
```

Rationale: The reusable parity harness and the canonical scenario should be independently implemented and reviewed.

## 5. Implementation Handoff

### Handoff Classification

**Moderate change.**

This proposal should be routed to Product Owner / Developer agents for backlog reorganization. It does not require a PM-level MVP rewrite or architect-level technical reversal.

### Handoff Recipients

**Product Owner / backlog curator:**

- Apply the approved story splits and epic reframes to `epics.md`.
- Renumber affected stories consistently.
- Update intra-document story references after renumbering.
- Preserve the existing FR coverage map and update it if story coverage shifts.

**Developer / implementation planning agent:**

- Validate that each revised story remains independently completable.
- Run a follow-up `bmad-check-implementation-readiness` after the changes.
- Only proceed to sprint planning if the follow-up readiness check passes or has only accepted residual warnings.

### Success Criteria

- No story acceptance criteria depend on future story outputs.
- Oversized stories are split into independently verifiable implementation units.
- Epic 1 communicates contract value rather than only technical scaffolding.
- Epic 7 is clearly identified as a release-readiness/NFR-validation gate.
- Story 3.6 is split into repository creation and repository binding.
- FR coverage remains 57/57.
- A follow-up implementation readiness check no longer reports the previous critical sequencing violations.

## 6. Checklist Status

| Section | Status | Notes |
| --- | --- | --- |
| 1. Understand Trigger and Context | Done | Trigger is readiness-report finding before sprint planning. |
| 2. Epic Impact Assessment | Done | Existing epics remain viable with modifications. |
| 3. Artifact Conflict and Impact Analysis | Done | PRD and architecture remain valid; epics are impacted. |
| 4. Path Forward Evaluation | Done | Direct Adjustment selected. |
| 5. Proposal Components | Done | Eleven incremental proposals approved. |
| 6. Final Review and Handoff | In Progress | Awaiting user review/approval of this complete proposal. |

## 7. Approval

Approved by Jerome on 2026-05-10.

### Route

**Change scope:** Moderate

**Routed to:** Product Owner / backlog curator and Developer / implementation planning agent

### Workflow Execution Log

- Trigger addressed: Implementation readiness report found forward-story dependencies, oversized stories, unclear Epic 1/Epic 7 value framing, and a combined repository creation/binding story.
- Artifacts modified by this workflow: Sprint Change Proposal only.
- Artifacts proposed for later modification: `epics.md`.
- Handoff complete: Apply approved edits to `epics.md`, rerun `bmad-check-implementation-readiness`, then proceed to sprint planning only if readiness passes.
